param([ValidateSet('Check', 'Version', 'Publish')][string] $Mode)

$ErrorActionPreference = 'Stop'

function Invoke-NightlyApi {
    param([string] $Path, [string] $Method = 'GET', $Body, [string] $FilePath, [switch] $AllowNotFound)
    $uri = if ($Path.StartsWith('https://')) { $Path } else {
        "$env:GITHUB_API_URL/repos/$env:GITHUB_REPOSITORY/$Path"
    }
    $headers = @{
        Authorization = "Bearer $env:GH_TOKEN"
        Accept = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
    }
    $arguments = @{ Uri = $uri; Method = $Method; Headers = $headers }
    if ($FilePath) {
        $arguments.InFile = $FilePath
        $arguments.ContentType = 'application/octet-stream'
    } elseif ($null -ne $Body) {
        $arguments.Body = [System.Text.Encoding]::UTF8.GetBytes(($Body | ConvertTo-Json -Depth 10 -Compress))
        $arguments.ContentType = 'application/json; charset=utf-8'
    }
    try { Invoke-RestMethod @arguments } catch {
        if ($AllowNotFound -and $null -ne $_.Exception.Response -and
            [int]$_.Exception.Response.StatusCode -eq 404) { return $null }
        throw
    }
}

function Read-NightlyVersion([string] $Value) {
    if ($Value -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Invalid package version: $Value" }
    $version = [version]::Parse($Value)
    if ($version.Major -eq 0 -or ($Value.Split('.') | Where-Object { [long]$_ -gt 65535 })) {
        throw "Package version is outside the supported range: $Value"
    }
    return $version
}

function Get-NightlyAssets($Release) {
    $page = 1
    do {
        $assets = @(Invoke-NightlyApi "releases/$($Release.id)/assets?per_page=100&page=$page" | ForEach-Object { $_ })
        $assets
        $page++
    } while ($assets.Count -eq 100)
}

function Get-NightlyRecord($Release) {
    $assets = @(Get-NightlyAssets $Release)
    $versions = @()
    # 草稿一创建就记录候选版本；即使上传一个包之前中断，也不丢失已分配的修订号。
    if ($Release.body -match '(?m)^- 包版本: (\d+\.\d+\.\d+\.\d+)\r?$') {
        $versions += (Read-NightlyVersion $Matches[1]).ToString()
    }
    foreach ($asset in $assets) {
        if ($asset.name -notmatch '\.(appx|msix|appxbundle|msixbundle)$') { continue }
        if ($asset.name -notmatch '_(?<version>\d+\.\d+\.\d+\.\d+)_(?:x86|x64|ARM64)(?:_[^/]*)?\.(?:appx|msix|appxbundle|msixbundle)$') {
            throw "Cannot read the version of nightly package: $($asset.name)"
        }
        $versions += (Read-NightlyVersion $Matches.version).ToString()
    }
    $versions = @($versions | Select-Object -Unique)
    $commit = if ($Release.body -match '(?m)^- 提交: ([0-9a-f]{40})\r?$') { $Matches[1] } else { $null }
    return [pscustomobject]@{ Release = $Release; Assets = $assets; Versions = $versions; Commit = $commit }
}

function Test-NightlyComplete($Record) {
    if ($null -eq $Record -or $Record.Release.draft -or -not $Record.Release.prerelease -or
        $Record.Versions.Count -ne 1 -or $Record.Assets.Count -ne 4 -or -not $Record.Commit) { return $false }
    if (@($Record.Assets | Where-Object { $_.state -ne 'uploaded' -or $_.size -le 0 }).Count) { return $false }
    foreach ($platform in @('x86', 'x64', 'ARM64')) {
        $pattern = "_$([regex]::Escape($Record.Versions[0]))_$platform\.(appx|msix)$"
        if (@($Record.Assets | Where-Object { $_.name -match $pattern }).Count -ne 1) { return $false }
    }
    return @($Record.Assets | Where-Object { $_.name -eq 'biliuwp-signing.cer' }).Count -eq 1
}

function Get-NightlyState {
    $records = @()
    $page = 1
    do {
        $releases = @(Invoke-NightlyApi "releases?per_page=100&page=$page" | ForEach-Object { $_ })
        foreach ($release in $releases) {
            if ($release.tag_name -match '^nightly(?:$|-backup-\d+$|-staging-\d+-\d+$)') {
                $records += Get-NightlyRecord $release
            }
        }
        $page++
    } while ($releases.Count -eq 100)
    $current = @($records | Where-Object { $_.Release.tag_name -eq 'nightly' })
    if ($current.Count -gt 1) { throw 'Multiple releases use the nightly tag; cannot choose a publication safely.' }
    # commits/<不存在的名字> 返回 422；先用 ref 接口的 404 区分首次发布。
    $tag = Invoke-NightlyApi 'git/ref/tags/nightly' -AllowNotFound
    $commit = if ($tag) {
        if ($tag.object.type -eq 'commit') { $tag.object.sha } else { (Invoke-NightlyApi 'commits/nightly').sha }
    } else { $null }
    return [pscustomobject]@{
        Records = $records
        Current = if ($current.Count) { $current[0] } else { $null }
        TagCommit = $commit
    }
}

function Write-NightlyOutput([string] $Name, [string] $Value) {
    "$Name=$Value" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
}

function Test-NightlySkip {
    if ($env:GITHUB_EVENT_NAME -eq 'workflow_dispatch') { return $false }
    $latest = Invoke-NightlyApi 'releases/latest' -AllowNotFound
    if ($latest) {
        $tag = [uri]::EscapeDataString($latest.tag_name)
        $formal = Invoke-NightlyApi "commits/$tag"
        if ($formal.sha -eq $env:GITHUB_SHA) { return $true }
    }
    $state = Get-NightlyState
    # 标签可能先于 Release 切换成功；还须核对发布说明和完整的三架构附件。
    return $state.TagCommit -eq $env:GITHUB_SHA -and
        (Test-NightlyComplete $state.Current) -and $state.Current.Commit -eq $env:GITHUB_SHA
}

function Set-NightlyPackageVersion {
    $manifestPath = Join-Path $env:GITHUB_WORKSPACE 'BiliBili.UWP/Package.appxmanifest'
    $manifest = [System.Xml.XmlDocument]::new()
    $manifest.PreserveWhitespace = $true
    $manifest.Load($manifestPath)
    $source = Read-NightlyVersion $manifest.Package.Identity.Version
    if ($source.Revision -ne 0) { throw 'The source manifest must use revision 0 for formal releases.' }
    $state = Get-NightlyState
    $versions = @($state.Records | ForEach-Object { $_.Versions } | ForEach-Object { Read-NightlyVersion $_ })
    # 同时读取当前发布、临时备份和未完成草稿，允许自动修复旧逻辑遗留的混合版本。
    if (-not $versions.Count -and ($state.TagCommit -or $state.Records.Count)) {
        throw 'Nightly history exists but has no recoverable package version; refusing to reset the revision.'
    }
    $revision = 1
    if ($versions.Count) {
        $previous = $versions | Sort-Object -Descending | Select-Object -First 1
        $previousBase = [version]::new($previous.Major, $previous.Minor, $previous.Build, 0)
        if ($source -lt $previousBase) { throw "Source version $source is older than nightly history $previous." }
        if ($source -eq $previousBase) {
            if ($previous.Revision -eq 65535) { throw 'Nightly revision has reached 65535; increase the source manifest base version.' }
            $revision = $previous.Revision + 1
        }
    }
    $version = "$($source.Major).$($source.Minor).$($source.Build).$revision"
    $manifest.Package.Identity.SetAttribute('Version', $version)
    $manifest.Save($manifestPath)
    Write-NightlyOutput 'package_version' $version
    Write-Host "Nightly package version: $version"
}

function Set-NightlyTag([string] $Name, [string] $Commit) {
    # 只为当前构建写入 nightly；旧版本用恢复草稿保存，不创建需要 workflows 权限的历史标签。
    git push --force origin "${Commit}:refs/tags/$Name"
    if ($LASTEXITCODE -ne 0) { throw "Cannot push nightly tag: $Name" }
}

function Remove-NightlyTag([string] $Name) {
    $escaped = [uri]::EscapeDataString($Name)
    # 不存在的 ref 在 GET 中返回 404、DELETE 中返回 422；先读再删，不能吞掉所有 422。
    if (Invoke-NightlyApi "git/ref/tags/$escaped" -AllowNotFound) {
        $null = Invoke-NightlyApi "git/refs/tags/$escaped" -Method DELETE
    }
}

function Publish-NightlyRelease {
    $expectedVersion = (Read-NightlyVersion $env:EXPECTED_PACKAGE_VERSION).ToString()
    $files = @($env:NIGHTLY_FILES.Split(',') | ForEach-Object { Get-Item -LiteralPath $_ -ErrorAction Stop })
    if ($files.Count -ne 4 -or @($files | Where-Object { $_.Length -le 0 -or $_.PSIsContainer }).Count) {
        throw 'Nightly publication requires exactly three nonempty packages and one certificate.'
    }
    foreach ($platform in @('x86', 'x64', 'ARM64')) {
        if (@($files | Where-Object { $_.Name -match "_$([regex]::Escape($expectedVersion))_$platform\.appx$" }).Count -ne 1) {
            throw "Missing or duplicate $platform package for $expectedVersion."
        }
    }
    if (@($files | Where-Object { $_.Name -eq 'biliuwp-signing.cer' }).Count -ne 1) { throw 'Missing signing certificate.' }
    $body = [System.IO.File]::ReadAllText((Join-Path $env:GITHUB_WORKSPACE 'nightly-release-notes.md'))
    if ($body -notmatch "(?m)^- 包版本: $([regex]::Escape($expectedVersion))\r?$" -or
        $body -notmatch "(?m)^- 提交: $([regex]::Escape($env:GITHUB_SHA))\r?$") { throw 'Release notes do not match this build.' }
    if ($env:GITHUB_RUN_ID -notmatch '^\d+$' -or $env:GITHUB_RUN_ATTEMPT -notmatch '^\d+$') { throw 'Invalid run identity.' }
    $stagingTag = "nightly-staging-$env:GITHUB_RUN_ID-$env:GITHUB_RUN_ATTEMPT"
    $stage = Invoke-NightlyApi 'releases' -Method POST -Body @{
        tag_name = $stagingTag; target_commitish = $env:GITHUB_SHA
        name = 'Nightly Build'; body = $body; draft = $true; prerelease = $true
    }
    # 先上传到草稿。上传或校验失败时，旧 nightly 和草稿里的版本记录都保留。
    $uploadUrl = $stage.upload_url.Split('{')[0]
    foreach ($file in $files) {
        $name = [uri]::EscapeDataString($file.Name)
        $null = Invoke-NightlyApi "${uploadUrl}?name=$name" -Method POST -FilePath $file.FullName
    }
    $uploaded = @(Get-NightlyAssets $stage)
    if ($uploaded.Count -ne $files.Count) { throw 'Staging release has an unexpected number of assets.' }
    foreach ($file in $files) {
        $asset = @($uploaded | Where-Object { $_.name -eq $file.Name })
        if ($asset.Count -ne 1 -or $asset[0].state -ne 'uploaded' -or $asset[0].size -ne $file.Length) {
            throw "Incomplete staging upload: $($file.Name)"
        }
    }

    $state = Get-NightlyState
    if ($state.Current) {
        $old = $state.Current
        $backupTag = "nightly-backup-$($old.Release.id)"
        # GITHUB_TOKEN 不能为工作流内容不同的历史提交创建新标签。
        # 先转草稿再改名，保留旧附件和版本记录，全程不创建历史 Git ref。
        $null = Invoke-NightlyApi "releases/$($old.Release.id)" -Method PATCH -Body @{ draft = $true }
        $null = Invoke-NightlyApi "releases/$($old.Release.id)" -Method PATCH -Body @{
            tag_name = $backupTag
        }
    }
    # 仍重建 nightly 标签和 Release，以刷新发布日期。
    Remove-NightlyTag 'nightly'
    Set-NightlyTag 'nightly' $env:GITHUB_SHA
    $null = Invoke-NightlyApi "releases/$($stage.id)" -Method PATCH -Body @{
        tag_name = 'nightly'; target_commitish = $env:GITHUB_SHA; draft = $false; prerelease = $true
    }
    $published = Get-NightlyState
    if (-not (Test-NightlyComplete $published.Current) -or $published.Current.Release.id -ne $stage.id -or
        $published.Current.Commit -ne $env:GITHUB_SHA -or $published.TagCommit -ne $env:GITHUB_SHA -or
        $published.Current.Versions[0] -ne $expectedVersion) { throw 'Published nightly did not pass final verification.' }

    # 新发布已完整可用才清理旧记录；清理失败明确报错，保留的备份仍可用于下次恢复。
    foreach ($record in $published.Records) {
        if ($record.Release.id -eq $stage.id) { continue }
        Remove-NightlyTag $record.Release.tag_name
        $null = Invoke-NightlyApi "releases/$($record.Release.id)" -Method DELETE -AllowNotFound
    }
    Write-Host "Published nightly $expectedVersion for $env:GITHUB_SHA."
}

switch ($Mode) {
    'Check' { Write-NightlyOutput 'skip' (Test-NightlySkip).ToString().ToLowerInvariant() }
    'Version' { Set-NightlyPackageVersion }
    'Publish' { Publish-NightlyRelease }
}
