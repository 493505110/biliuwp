# 无外部依赖的发布状态机测试；API 全部使用本地替身，不访问 GitHub。
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../nightly-release.ps1')
$realApi = ${function:Invoke-NightlyApi}
$realSetTag = ${function:Set-NightlyTag}
$script:checks = 0
$oldSha = 'a' * 40
$newSha = 'b' * 40
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('nightly-tests-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -Path (Join-Path $testRoot 'BiliBili.UWP') -ItemType Directory -Force
$environmentNames = @('GITHUB_API_URL', 'GITHUB_REPOSITORY', 'GITHUB_WORKSPACE', 'GITHUB_SHA',
    'GITHUB_EVENT_NAME', 'GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT', 'GITHUB_OUTPUT',
    'EXPECTED_PACKAGE_VERSION', 'NIGHTLY_FILES', 'GH_TOKEN')
$savedEnvironment = @{}
foreach ($name in $environmentNames) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }

function Assert($Condition, [string] $Message) {
    if (-not $Condition) { throw "Assertion failed: $Message" }
    $script:checks++
}
function Assert-Throws([scriptblock] $Action, [string] $Pattern) {
    $message = $null
    try { & $Action } catch { $message = $_.Exception.Message }
    Assert ($message -and $message -match $Pattern) "Expected error $Pattern; got $message"
}
function Copy-Response($Value) { $Value | ConvertTo-Json -Depth 20 -Compress | ConvertFrom-Json }
function New-Asset([string] $Name, [int] $Size = 4) {
    [pscustomobject]@{ name = $Name; size = $Size; state = 'uploaded' }
}
function Add-Release([string] $Tag, [string] $Version, [string] $Commit, [bool] $Draft = $false) {
    $script:nextId++
    $release = [pscustomobject]@{
        id = $script:nextId; tag_name = $Tag; target_commitish = $Commit
        draft = $Draft; prerelease = $true; name = 'Nightly Build'
        body = "- 提交: $Commit`n- 包版本: $Version"
        upload_url = "https://uploads.test/releases/$script:nextId/assets{?name,label}"
        assets = @(
            New-Asset "BiliBili.UWP_${Version}_x86.appx"
            New-Asset "BiliBili.UWP_${Version}_x64.appx"
            New-Asset "BiliBili.UWP_${Version}_ARM64.appx"
            New-Asset 'biliuwp-signing.cer'
        )
    }
    $script:releases.Add($release)
    return $release
}
function Reset-State([switch] $Empty) {
    $script:releases = [Collections.Generic.List[object]]::new()
    $script:refs = @{}
    $script:nextId = 100
    $script:mutation = 0
    $script:failAt = 0
    $script:failAfter = $false
    $script:failRead = $false
    $script:formal = $null
    $script:assetPages = 0
    $env:GITHUB_EVENT_NAME = 'schedule'
    $env:GITHUB_RUN_ID = '123'
    $env:GITHUB_RUN_ATTEMPT = '1'
    if (-not $Empty) {
        $null = Add-Release 'nightly' '3.16.1.8' $oldSha
        $script:refs.nightly = $oldSha
    }
    [IO.File]::WriteAllText((Join-Path $testRoot 'BiliBili.UWP/Package.appxmanifest'),
        '<Package><Identity Version="3.16.1.0" /></Package>')
    [IO.File]::WriteAllText($env:GITHUB_OUTPUT, '')
}
function Prepare-Files([string] $Version) {
    $env:EXPECTED_PACKAGE_VERSION = $Version
    $names = @('x86', 'x64', 'ARM64') | ForEach-Object { "BiliBili.UWP_${Version}_$_.appx" }
    $names += 'biliuwp-signing.cer'
    $paths = foreach ($name in $names) {
        $path = Join-Path $testRoot $name
        [IO.File]::WriteAllText($path, 'test')
        $path
    }
    $env:NIGHTLY_FILES = $paths -join ','
    [IO.File]::WriteAllText((Join-Path $testRoot 'nightly-release-notes.md'),
        "- 提交: $newSha`n- 包版本: $Version")
}
function Throw-Http([int] $Status) {
    throw [Microsoft.PowerShell.Commands.HttpResponseException]::new("Injected HTTP $Status",
        [Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]$Status))
}
function Invoke-NightlyApi {
    param([string] $Path, [string] $Method = 'GET', $Body, [string] $FilePath, [switch] $AllowNotFound)
    if ($script:failRead -and $Method -eq 'GET') { Throw-Http 500 }
    if ($Method -ne 'GET') {
        $script:mutation++
        if ($script:mutation -eq $script:failAt -and -not $script:failAfter) { Throw-Http 500 }
    }
    $result = $null
    $notFound = $false
    if ($Path -eq 'releases/latest') {
        $result = $script:formal
        $notFound = $null -eq $result
    } elseif ($Path -match '^commits/(.+)$') {
        $tag = [uri]::UnescapeDataString($Matches[1])
        if ($script:refs.ContainsKey($tag)) { $result = @{ sha = $script:refs[$tag] } } else { $notFound = $true }
    } elseif ($Path -match '^releases\?per_page=100&page=(\d+)$') {
        $page = [int]$Matches[1]
        $result = @($script:releases | Select-Object -Skip (($page - 1) * 100) -First 100)
    } elseif ($Path -match '^releases/(\d+)/assets\?per_page=100&page=(\d+)$') {
        $id = [long]$Matches[1]; $page = [int]$Matches[2]
        $script:assetPages++
        $release = $script:releases | Where-Object id -eq $id | Select-Object -First 1
        if ($release) { $result = @($release.assets | Select-Object -Skip (($page - 1) * 100) -First 100) } else { $notFound = $true }
    } elseif ($Path -eq 'releases' -and $Method -eq 'POST') {
        $result = Add-Release $Body.tag_name '0.0.0.0' $Body.target_commitish $Body.draft
        $result.body = $Body.body
        $result.assets = @()
    } elseif ($Path -match '^releases/(\d+)$') {
        $id = [long]$Matches[1]
        $result = $script:releases | Where-Object id -eq $id | Select-Object -First 1
        if (-not $result) { $notFound = $true } elseif ($Method -eq 'PATCH') {
            $draft = if ($Body.ContainsKey('draft')) { $Body.draft } else { $result.draft }
            $target = if ($Body.ContainsKey('target_commitish')) { $Body.target_commitish } else { $result.target_commitish }
            $tag = if ($Body.ContainsKey('tag_name')) { $Body.tag_name } else { $result.tag_name }
            if (-not $draft -and -not $script:refs.ContainsKey($tag) -and $target -ne $newSha) { Throw-Http 403 }
            foreach ($key in $Body.Keys) { $result.$key = $Body[$key] }
        } elseif ($Method -eq 'DELETE') { $null = $script:releases.Remove($result); $result = $null }
    } elseif ($Path -match '^https://uploads.test/releases/(\d+)/assets\?name=(.+)$') {
        $id = [long]$Matches[1]; $name = [uri]::UnescapeDataString($Matches[2])
        $release = $script:releases | Where-Object id -eq $id | Select-Object -First 1
        $result = New-Asset $name (Get-Item -LiteralPath $FilePath).Length
        $release.assets += $result
    } elseif ($Path -match '^git/ref/tags/(.+)$') {
        $tag = [uri]::UnescapeDataString($Matches[1])
        if ($script:refs.ContainsKey($tag)) { $result = @{ object = @{ sha = $script:refs[$tag]; type = 'commit' } } } else { $notFound = $true }
    } elseif ($Path -eq 'git/refs' -and $Method -eq 'POST') {
        $tag = $Body.ref -replace '^refs/tags/', ''
        if ($script:refs.ContainsKey($tag)) { Throw-Http 422 }
        $script:refs[$tag] = $Body.sha
    } elseif ($Path -match '^git/refs/tags/(.+)$') {
        $tag = [uri]::UnescapeDataString($Matches[1])
        if (-not $script:refs.ContainsKey($tag)) { $notFound = $true } elseif ($Method -eq 'DELETE') {
            $script:refs.Remove($tag)
        } elseif ($Method -eq 'PATCH') { $script:refs[$tag] = $Body.sha }
    } else { throw "Unexpected request: $Method $Path" }
    if ($script:mutation -eq $script:failAt -and $Method -ne 'GET' -and $script:failAfter) { Throw-Http 500 }
    if ($notFound) {
        if ($Method -eq 'DELETE' -and $Path -match '^git/refs/') { Throw-Http 422 }
        if ($AllowNotFound) { return $null }
        Throw-Http 404
    }
    if ($null -ne $result) { Copy-Response $result }
}

function Set-NightlyTag([string] $Name, [string] $Commit) {
    if ($Name -ne 'nightly' -or $Commit -ne $newSha) { Throw-Http 403 }
    $script:mutation++
    if ($script:mutation -eq $script:failAt -and -not $script:failAfter) { Throw-Http 500 }
    $script:refs[$Name] = $Commit
    if ($script:mutation -eq $script:failAt -and $script:failAfter) { Throw-Http 500 }
}

try {
    $env:GITHUB_API_URL = 'https://api.test'
    $env:GITHUB_REPOSITORY = 'test/repo'
    $env:GITHUB_WORKSPACE = $testRoot
    $env:GITHUB_OUTPUT = Join-Path $testRoot 'github-output.txt'
    $env:GITHUB_SHA = $newSha
    $env:GH_TOKEN = 'test-only'

    Reset-State
    Prepare-Files '3.16.1.9'
    Publish-NightlyRelease
    $mutationCount = $script:mutation
    Assert ($script:releases.Count -eq 1) 'Successful publication removes obsolete records'
    Assert ($script:refs.Count -eq 1 -and $script:refs.nightly -eq $newSha) 'Only the nightly tag remains'
    $writesBefore = $script:mutation
    Remove-NightlyTag 'nightly-backup-missing'
    Assert ($script:mutation -eq $writesBefore) 'Missing tag cleanup only reads and avoids DELETE 422'
    Assert (Test-NightlySkip) 'Complete matching release is skipped'
    $env:GITHUB_EVENT_NAME = 'workflow_dispatch'
    Assert (-not (Test-NightlySkip)) 'Manual rebuild remains unconditional'

    # 在每个写请求前、以及服务端成功但响应丢失后中断，再从头运行恢复。
    foreach ($after in @($false, $true)) {
        foreach ($index in 1..$mutationCount) {
            Reset-State
            Prepare-Files '3.16.1.9'
            $script:failAt = $index; $script:failAfter = $after
            Assert-Throws { Publish-NightlyRelease } 'Injected HTTP 500'
            $script:failAt = 0
            $state = Get-NightlyState
            $old = $state.Records | Where-Object { $_.Release.id -eq 101 } | Select-Object -First 1
            $oldAssets = if ($old) { @($old.Assets | Where-Object { $_.state -eq 'uploaded' -and $_.size -eq 4 }) } else { @() }
            $oldIntact = $old -and $old.Commit -eq $oldSha -and $oldAssets.Count -eq 4 -and
                @($oldAssets | Where-Object { $_.name -match '_3\.16\.1\.8_(x86|x64|ARM64)\.appx$' }).Count -eq 3
            Assert ($oldIntact -or (Test-NightlyComplete $state.Current)) "Preserves complete previous data or a verified new release at mutation $index (after=$after)"
            if (Test-NightlySkip) {
                Assert ($state.Current.Commit -eq $newSha -and (Test-NightlyComplete $state.Current)) 'Skip implies verified successful publication'
            }
            Set-NightlyPackageVersion
            $version = [xml](Get-Content (Join-Path $testRoot 'BiliBili.UWP/Package.appxmanifest') -Raw)
            $next = [version]$version.Package.Identity.Version
            Assert ($next -gt [version]'3.16.1.8') 'Revision never resets after interruption'
            $null = $state.Records | ForEach-Object {
                foreach ($reserved in $_.Versions) { Assert ($next -gt [version]$reserved) 'Next version exceeds every surviving reservation' }
            }
            Prepare-Files $next.ToString()
            $env:GITHUB_RUN_ATTEMPT = '2'
            Publish-NightlyRelease
            Assert ($script:releases.Count -eq 1 -and (Test-NightlySkip)) 'Next run recovers and removes stale records'
        }
    }
    Write-Host "Recovered every before/after fault at $mutationCount publication writes."

    Reset-State -Empty
    Prepare-Files '3.16.1.1'
    $script:failAt = 2
    Assert-Throws { Publish-NightlyRelease } 'Injected HTTP 500'
    $script:failAt = 0
    $script:refs.nightly = $newSha
    Assert (-not (Test-NightlySkip)) 'First-run tag without a published release is not skipped'
    Set-NightlyPackageVersion
    Assert ((Get-Content $env:GITHUB_OUTPUT -Raw) -match 'package_version=3.16.1.2') 'Empty staging draft preserves the first allocated revision'
    Prepare-Files '3.16.1.2'
    $env:GITHUB_RUN_ATTEMPT = '2'
    Publish-NightlyRelease
    Assert ($script:releases.Count -eq 1 -and (Test-NightlySkip)) 'First publication recovers without an orphan-tag deadlock'

    Reset-State
    $script:refs.nightly = $newSha
    Assert (-not (Test-NightlySkip)) 'Moved tag alone cannot hide the previous release commit'

    Reset-State
    $current = $script:releases[0]
    $current.assets = @($current.assets | Where-Object { $_.name -notmatch '_ARM64.appx$' })
    $current.body = $current.body.Replace($oldSha, $newSha)
    $script:refs.nightly = $newSha
    Assert (-not (Test-NightlySkip)) 'Missing architecture is never skipped'
    $current.assets += New-Asset 'BiliBili.UWP_3.16.1.10_x64.appx'
    Assert (-not (Test-NightlySkip)) 'Mixed versions are never skipped'
    Set-NightlyPackageVersion
    Assert ((Get-Content $env:GITHUB_OUTPUT -Raw) -match 'package_version=3.16.1.11') 'Mixed legacy versions recover above the highest version'

    Reset-State -Empty
    Set-NightlyPackageVersion
    Assert ((Get-Content $env:GITHUB_OUTPUT -Raw) -match 'package_version=3.16.1.1') 'Genuine first publication starts at revision 1'
    Reset-State -Empty
    $script:refs.nightly = $oldSha
    Assert (-not (Test-NightlySkip)) 'An orphan tag is never considered published'
    Assert-Throws { Set-NightlyPackageVersion } 'no recoverable package version'

    Reset-State
    $script:releases[0].body = $script:releases[0].body.Replace('3.16.1.8', '3.16.1.65535')
    Assert-Throws { Set-NightlyPackageVersion } 'reached 65535'
    [IO.File]::WriteAllText((Join-Path $testRoot 'BiliBili.UWP/Package.appxmanifest'),
        '<Package><Identity Version="3.16.2.0" /></Package>')
    Set-NightlyPackageVersion
    Assert ((Get-Content $env:GITHUB_OUTPUT -Raw) -match 'package_version=3.16.2.1') 'New base resets revision even after overflow'

    Reset-State
    $script:formal = [pscustomobject]@{ tag_name = 'v3.16.1' }
    $script:refs['v3.16.1'] = $newSha
    Assert (Test-NightlySkip) 'Formal release covering HEAD still skips scheduled build'
    $script:failRead = $true
    Assert-Throws { Test-NightlySkip } 'Injected HTTP 500'

    Reset-State
    $script:releases[0].assets += @(1..96 | ForEach-Object { New-Asset "extra-$_.txt" })
    $assets = @(Get-NightlyAssets $script:releases[0])
    Assert ($assets.Count -eq 100 -and $script:assetPages -eq 2) 'Asset pagination reads an empty second page after 100 items'

    # 实际 HTTP 包装层只容忍显式允许的 404，不吞掉 403/500。
    function Invoke-RestMethod { param($Uri, $Method, $Headers, $Body, $ContentType, $InFile); Throw-Http $script:httpStatus }
    foreach ($status in @(403, 500)) {
        $script:httpStatus = $status
        Assert-Throws { & $realApi 'git/refs/tags/nightly' -Method DELETE -AllowNotFound } "HTTP $status"
    }
    $script:httpStatus = 404
    Assert ($null -eq (& $realApi 'git/refs/tags/nightly' -Method DELETE -AllowNotFound)) 'Explicitly allowed missing tag is idempotent'
    Assert-Throws { & $realApi 'releases' } 'HTTP 404'
    $script:httpStatus = 422
    Assert-Throws { & $realApi 'git/refs/tags/nightly' -Method DELETE } 'HTTP 422'

    # 实际 Git 传输验证：仅推送到临时裸仓库，不使用 GitHub 凭据。
    $client = Join-Path $testRoot 'git-client'
    $server = Join-Path $testRoot 'git-server'
    $null = git init --quiet $client
    if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize Git fixture.' }
    $null = git init --bare --quiet $server
    if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize bare Git fixture.' }
    Push-Location $client
    try {
        git -c user.name=Fixture -c user.email=fixture@example.invalid commit --quiet --allow-empty --no-gpg-sign -m fixture
        if ($LASTEXITCODE -ne 0) { throw 'Cannot create Git fixture commit.' }
        git remote add origin $server
        $historical = git rev-parse HEAD
        git -c user.name=Fixture -c user.email=fixture@example.invalid commit --quiet --allow-empty --no-gpg-sign -m newer-fixture
        if ($LASTEXITCODE -ne 0) { throw 'Cannot create newer Git fixture commit.' }
        $commit = git rev-parse HEAD
        & $realSetTag 'nightly' $historical
        & $realSetTag 'nightly' $commit
        $ref = git ls-remote origin refs/tags/nightly
        Assert ($ref -match "^$commit\s") 'Git transport creates nightly tag at exact build commit'
        git remote set-url origin (Join-Path $testRoot 'missing-server')
        Assert-Throws { & $realSetTag 'nightly' $commit } 'Cannot push nightly tag'
        # 预期的失败不能让 Actions 的 pwsh 包装器把已通过的测试判为失败。
        $global:LASTEXITCODE = 0
    } finally { Pop-Location }
    Write-Host "Nightly release regression checks passed: $script:checks"
} finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
    $resolvedRoot = [IO.Path]::GetFullPath($testRoot)
    $tempPrefix = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedRoot.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedRoot) -notmatch '^nightly-tests-[0-9a-f]{32}$') { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}
