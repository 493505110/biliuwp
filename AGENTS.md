# AGENTS.md

哔哩哔哩第三方 UWP 客户端。Fork 自 xiaoyaocz/biliuwp（原项目已停止维护）。

## 构建与验证

- 使用 **Visual Studio 2019+**，安装 UWP 工作负载和 **Windows SDK 10.0.19041.0**。
- 主应用 `BiliBili.UWP` 的 `TargetPlatformMinVersion` 为 `10.0.17763.0`，这是 WinUI 2.8/WebView2 的最低要求；`BiliBili.Background` 仍声明为 `10.0.16299.0`。
- 打开 `BiliBili.sln`，还原 NuGet 包并生成解决方案。涉及网页登录或安全验证时，运行环境还需安装 WebView2 Runtime。
- 主目标为 `BiliBili.UWP`（`AppContainerExe`）。解决方案的 `Any CPU` 主应用配置映射到 `x86`，不要把它理解为真正的 AnyCPU 主程序产物；解决方案平台只有 `Any CPU`、`ARM`、`x64`、`x86`，没有 `ARM64`。
- 主应用的 `Release|x86`、`Release|ARM`、`Release|x64` 启用 `.NET Native toolchain`，依赖反射的代码在 Release 下可能有不同表现；`Debug|x64` 显式关闭该工具链。
- 工程启用了 AppX 包签名并引用 `BiliBili.UWP/BiliBili.UWP_TemporaryKey.pfx`。PFX 被 `.gitignore` 排除；新环境缺少证书时，应在 Visual Studio 中创建或选择本地测试证书，不要提交私钥文件。
- Git 子模块 `Libraries/NSDanmaku-Fork` 是解决方案中 `NSDanmaku` 项目的来源，克隆时必须带子模块（CI 使用 `submodules: recursive`）。
- 测试项目为 `tests/BiliBili.Tests`(net8.0 + MSTest)，已加入 `BiliBili.sln`，并通过 `<Compile Include="..\..\BiliBili.UWP\..." Link="Production\...">` 直接编译生产源码，夹具位于 `tests/BiliBili.Tests/Fixtures`。它只覆盖不依赖 UWP 运行时的纯逻辑（含 `Modules\Playback\` 下的播放纯逻辑与 `Modules\BiliJumpAiParser.cs`），不能替代页面级验证。
- `.github/workflows/` 有 `ci.yml`（推送 master / PR 时构建 `Debug|x86` 主项目并运行单元测试）、`nightly.yml`（定时构建并发布 nightly 标签）和 `release.yml`（`v*` 标签触发）。仓库仍无 lint 或格式化配置。XML 解析、静态检查和 `git diff --check` 只能作为补充，不能替代 Visual Studio 构建和实际页面验证。
- 正式发版只构建 `Release|x86` 与 `Release|x64`，并附上 `biliuwp-signing.cer`；**不产出 ARM 包**（csproj 里的 ARM 配置只服务于本地调试）。
- `release.yml` 发版时有两条硬约束：manifest 的 `Identity Version` 必须与 tag 一致；**Release 正文直接从 `CHANGELOG.md` 抽取 `## x.y.z` 段落**（跳过版本标题行），段落缺失或标题不匹配就会产出空正文。打 tag 前先补 CHANGELOG。
- 不要使用 `dotnet build` 构建该旧式 UWP 工程。需要命令行自动化时只能使用 Visual Studio 自带的 MSBuild；最终验证仍以 Visual Studio 的生成、部署和运行结果为准。

## 架构

`BiliBili.sln` 包含四个项目：

| 项目 | 类型 | 用途 |
|---|---|---|
| `BiliBili.UWP` | UWP 应用 | 主客户端：页面、业务模块、API 层、UI 控件 |
| `BiliBili.Background` | `winmdobj` | 后台任务（`BackgroundTask.cs`）和关注动态磁贴通知 |
| `NSDanmaku` | 类库（子模块） | 来自 `Libraries/NSDanmaku-Fork`，弹幕渲染底层库 |
| `BiliBili.Tests` | net8.0 测试 | `tests/BiliBili.Tests`，MSTest 纯逻辑契约测试 |

工作区还存在 `BiliBili.JSBridge/`，但它不在解决方案中且未被 Git 跟踪（仅有 bin/obj 产物），属于历史构建残留，不要当作活跃项目。

`cloudflare/bili-jump-cache/` 是**已跟踪、独立于 `BiliBili.sln`** 的子项目：字幕广告 AI 识别的 Cloudflare Worker + D1 公共缓存，与 UWP 工程没有编译期依赖，只在运行期通过 HTTP 协作。它有自己的 `AGENTS.md` 与 `README.md`，改动该目录时读那一份，不要套用根文档的构建与提交约定。

### 关键目录（`BiliBili.UWP/` 下）

- `Api/`：API 定义、`ApiModel` 请求描述、`ApiRequest.cs` HTTP 客户端和 `ApiUtils.cs` 扩展方法。
- `Helper/`：SQLite、设置与 `CredentialVault`（含 `SettingKeys.cs`）、旧 WebClient、Wbi 签名、弹幕服务（`BiliDanmakuService`、`InteractiveDanmakuService`、`BiliLiveDanmu`）、`FFmpegDashSource`、`MediaProcessing`、`WebView2CookieHelper`、字幕广告 AI 识别（`BiliJumpAi.cs`、`BiliJumpAiCacheService.cs`）、开屏图（`SplashImageSelector.cs`）、日志和消息中心等基础设施。
- `Modules/`：业务/ViewModel 层；主要业务类继承 `IModules`，同目录也包含不继承它的响应模型。`Modules/Playback/` 收拢播放相关**纯逻辑**（`DashStreamSelector`、`PlaybackRequestGate`、`PlaybackPosition`、`PlaybackHistory`、`PlaybackUrl`、`PlaybackRestoreState`、`PlaybackTimelineIndex`、`PlaybackEventTimeline`），这些文件被测试项目链接编译，改动会被单元测试直接覆盖。
- `Pages/`：内容页和详情页，部分功能再按 Home、Live、Music、User、Bangumi、FindMore、Season 分类。
- `Views/`：主导航视图，包括 `BangumiPage`、`ChannelPage`、`FindPage`、`SettingPage`、`AttentionPage` 和直播主入口 `LiveV2Page`；首页在 `Pages/Home/HomePage`，不在 `Views/` 下。
- `Models/`：共享数据模型和 API 响应模型。
- `Controls/`、`Converters/`、`Theme/`、`Themes/`：自定义控件、XAML 值转换器和样式资源。

### 请求/响应模式

1. API 类（如 `WbiAPI`、`VideoAPI`、`SearchAPI`）的方法返回 `ApiModel`，其中包含 `baseUrl`、`method`、`parameter`、`body` 和 `headers`；`url` getter 直接拼接 `baseUrl + "?" + parameter`。
2. 调用 `api.Request()`（`ApiUtils.cs` 中的扩展方法），它按需处理请求头和 Wbi 签名，再转发到 `ApiRequest.Get()` / `ApiRequest.Post()`。
3. `ApiRequest` 返回 `HttpResults`，常用 `.GetJson<T>()`、`.GetJObject()`、`.GetResult<T>()`、`.GetData<T>()` 反序列化。
4. Wbi 请求在 `ApiModel` 上设置 `useWbi = true`。`ApiHelper.GetWbiSign()` 通过 `/x/web-interface/nav` 获取 `img_key` / `sub_key`，交给 `WbiEncodeHelper.EncWbi()` 签名。
5. Wbi key 只缓存在当前应用进程中，`SemaphoreSlim` 防止并发重复拉取；`wts` 和 `w_rid` 每次重新生成。`Request()` 收到 `-352` 等 WBI 风控码时会调用 `ClearWbiKey()` 清空缓存并重签重试一次，重试仍失败才返回错误。

`Request()` 有一个特殊分支：`baseUrl` 含 `search` 时会按 `User-Agent` / `Referer` 两个键写入桌面 Chrome UA 和 `https://www.bilibili.com/`，调用方其余 header 保留；若 `api.headers` 为 `null` 则先新建字典。

代码中还保留更老的 HTTP 层 `Helper/WebClientClass.cs`，部分功能仍在使用。新旧两层都基于 `Windows.Web.Http.HttpClient`，修改调用链前先确认实际入口。

每个 API 类各自在 `ApiModel.baseUrl` 中写死接口地址，仓库没有统一的 API 基址或代理前缀。

### 登录与 Cookie

- 当前登录入口是 `Controls/LoginDialog`，支持二维码、账密登录、WebView2 网页登录及安全验证；账号业务集中在 `Modules/Account.cs` 和 `Api/User/LoginAPI.cs`。
- `access_key` 已迁移到 `SettingHelper` + `CredentialVault`（Credential Locker），读取时回退到 `ApplicationData.Current.LocalSettings` 的旧键并兼容迁移；`refresh_token`、用户 ID、过期时间和 Biliplus Cookie 等仍由 `SettingHelper` 写入 `ApplicationData.Current.LocalSettings`。
- Bilibili Web Cookie 位于 WinRT `HttpBaseProtocolFilter.CookieManager`；WebView2 使用独立的 Chromium Cookie 存储。`LoginDialog` 会在两者之间复制 Cookie，注销时两边都要清理。
- 直播 Web API 依赖 Cookie/Wbi/web 参数。弹幕认证只有在 `getDanmuInfo` 请求实际携带 `SESSDATA` 时才应发送用户 UID，否则按游客 UID `0` 连接。

### 导航

多处使用 `MessageCenter.SendNavigateTo(NavigateMode mode, Type page, params object[] par)`，`NavigateMode` 决定目标 Frame。`SplashPage` 是启动页，`MainPage` 是主外壳；协议激活在 `App.OnActivated` 中处理。

新旧页面仍有并存，但当前主入口已经明确：直播使用 `Views/LiveV2Page`，搜索使用 `Pages/SearchV2Page`。名为 `LivePage`、`SearchPage` 的旧页面文件已不在仓库中；修改前仍需从 `MainPage`、`MessageCenter` 或调用点确认页面的实际可达路径。

### 播放器

- 普通视频页 `Pages/PlayerPage` 使用 UWP `MediaPlayerElement` / `MediaPlayer` 作为播放核心。
- DASH 视频由 `PlayurlHelper` 构造 `AdaptiveMediaSource`；部分 FLV、分段流和本地文件通过 `SYEngine.Playlist` 转成媒体流。
- `VLC.MediaElement` 直接用于 `Pages/Live/LiveRoomPC` 和 `Pages/Live/LiveRoomPage` 的直播播放（这两处不是普通视频页的通用备用播放器，且 `LiveRoomPage` 仍有 `MainPage` 跳转入口，不要仅凭名称当作死代码）。
- 普通视频弹幕按类型分流：普通弹幕由播放器内嵌的 `Controls/DanmakuMTC`（继承 NSDanmaku 的 `MediaTransportControls`）渲染；数据层是 `Helper/BiliDanmakuService`，默认走 web 分段接口（`x/v2/dm/web/view` 与 `x/v2/dm/wbi/web/seg.so`），设置项 `UseNewDanmakuInterface`（默认开）关闭时降级到 `LoadLegacyAsync` 的旧接口。该服务只引用 `NSDanmaku.Model` 复用数据模型，不参与渲染。`DanmakuMTC.DanmuLoaded` 回传 NSDanmaku 的 `Danmaku` 控件，`PlayerPage` 直接改它的字号、字体、加粗和显示区域。
- BAS 弹幕与互动弹幕是两个独立控件，不共用上面的渲染链路：`Controls/BasDanmakuControl` 内是 `WebView2`，通过 `bas-host.html` / `bas.js` 渲染，数据同样来自 `BiliDanmakuService`；`Controls/InteractiveDanmakuControl` 是纯 XAML 选项面板，数据由 `Helper/InteractiveDanmakuService` 提供。
- 直播弹幕连接与协议解析在 `Helper/BiliLiveDanmu.cs`，与上述普通视频链路无关。

### 字幕广告 AI 识别（BiliJumpAi）

默认关闭（`Get_BiliJumpAiEnabled()` 缺省写回 `false`）。开启后 `PlayerPage` 用字幕文本请求 AI 识别植入广告段，再按识别结果跳过。整条链路横跨客户端、Cloudflare Worker 和 AI 提供商三方：

1. `PlayerPage.LoadBiliJumpAdsAsync()` 是唯一入口，前置条件包括：视频时长大于 `BiliJumpMinimumDurationSeconds`（150 秒）、`IsBiliJumpVideo()` 判定通过、以及 UP 主粉丝数不低于设置项 `BiliJumpAiMinFans`（默认 10，设为 0 表示不限）。
2. 字幕经 `Modules/BiliJumpAiParser.cs` 的 `BuildSubtitleText()` 拼成带时间轴的文本，交给 `Helper/BiliJumpAi.cs` 的 `BiliJumpAiService`。提供商有 `zhou2008`（内置 key，默认）、`deepseek`、`custom` 三种；用户自填的 API Key 存在 `CredentialVault` 的 `BiliBili.UWP.BiliJumpAi` 资源里，不落 `LocalSettings`。
3. 请求 AI 之前先查公共缓存 `Helper/BiliJumpAiCacheService.cs`，端点为 `https://api.zhou2008.cn/biliuwp/video_ad_jump`，走 claim / save / release 租约协议去重并发识别。该服务本身不调用 AI，只做缓存。
4. AI 返回的 JSON 由 `BiliJumpAiParser.TryParse()` 解析，`NormalizeSegments()` 裁剪到视频时长范围内。这两个方法与 `BuildSubtitleText()` 都是纯静态逻辑，已被 `tests/BiliBili.Tests` 覆盖——改解析规则时同步补测试。

`BiliJumpAiAutoJump`（默认关闭）决定命中后是自动跳过还是仅提示。

### 本地存储

- **SQLite**：`ApplicationData.Current.LocalFolder\RRMJData.db`（`SqlHelper.DbPath`），用于观看历史、播放进度和下载 GUID 等数据。
- **设置与凭证元数据**：`SettingHelper` 使用 `ApplicationData.Current.LocalSettings`。
- **Web Cookie**：分别位于 WinRT CookieManager 和 WebView2 CookieManager，不属于 LocalSettings。

## 代码约定

- 项目没有引入第三方 MVVM 框架；`Modules/IModules.cs` 里的 `IModules` 是**具体基类**而非接口（名字沿用了接口的 `I` 前缀），它继承 `INotifyPropertyChanged`，各业务类继承它并通过 `DoPropertyChanged(name)` 通知变更。
- 业务异步方法通常返回 `ReturnModel` / `ReturnModel<T>`（含 `success`、`message`）；常见异常路径交给 `IModules.HandleError()` 记录日志并显示消息。
- Bilibili API 响应常用 `ApiDataModel<T>`（`.data`）或 `ApiResultModel<T>`（`.result`），必须以具体接口的真实响应结构为准。
- 用户可见文案以中文为主；注释遵循所在文件的既有语言和风格，不要为了统一语言做无关改写。
- 保持改动范围聚焦。仓库存在新旧实现并存和大量历史兼容分支，不要仅凭类名或目录位置删除看似重复的代码。

## 关键陷阱

- `BiliBili.Background` 与主 UWP 项目的 `SettingHelper` 仍是两个独立类，各有各的实现，不要合并。但 key 常量已统一到 `BiliBili.UWP/Helper/SettingKeys.cs`，并由 `BiliBili.Background.csproj` 通过 `<Compile Include ... Link>` **跨项目编译期链接**（`SettingKeys.cs` 与 `SignHelper.cs` 都在链接列表里）。新增或改动设置 key 应只动 `SettingKeys.cs`；反之，改这两个文件会同时影响后台任务，必须两端都验证。
- `ApiHelper.access_key` 只在 `_access_key == ""` 时回退到 `SettingHelper.Get_Access_key()`；字段默认值为 `null`，未显式赋值时会直接返回 `null`。修改登录初始化前不要忽略这一行为。
- `ApiHelper.AndroidKey` 与 `ApiUtils.AndroidKey` 不是同一套客户端 key；`ApiHelper.AndroidKey` 对应 `ApiUtils.AndroidTVKey`。不要根据相同属性名互换使用，也不要在文档或日志中复制完整 key/secret。`BiliJumpAiProviders` 里还硬编码了一个内置 AI 服务 key（`Zhou2008BuiltInApiKey`）供默认提供商使用，同样不要外泄或复制到文档、日志、提交信息里。
- `ApiHelper.VideoKey` 的 Appkey 为空字符串，仅保留 Secret；当前仓库内没有任何调用方，视为历史遗留，改动前先确认是否真被需要。
- `ApiRequest` 使用进程级单例 `HttpClient`，请求头统一走 `HttpRequestMessage` 传递，不要退回 per-request 新建客户端的写法。过滤器忽略 `IgnorableServerCertificateErrors.Expired`，旧层 `Helper/WebClientClass.cs` 里也有同样的放行；修改网络安全策略时需要显式评估兼容性影响。
- `CommentV2Control.LoadComment()` 的两个重载会重新获取外层 `ScrollViewer` 并滚动到顶部；`ClearComment()` 当前只重新获取 ScrollViewer，不会自行 `ChangeView()`。切换内容时不要假定 `ClearComment()` 已完成滚动复位。
- 包标识、发布者和版本以 `BiliBili.UWP/Package.appxmanifest` 为唯一事实来源；发版时直接核对该文件，不要在其他文档复制当前版本号。
- **`Frame` 自 Windows 10 1803 起默认自带导航动画，不要误判为「切换没有动画」**：`Frame` 会自动用 `NavigationThemeTransition` 播放 Page Refresh，即**目标页面整体「从下往上滑入 + 淡入」**，无需手动设置 `ContentTransitions`。所以**任何 `Frame.Navigate` 都会让新页面整块滑入**，页面上覆盖的元素（开屏图、遮罩等）会跟着一起滑，看起来"像导航在动"。需要禁用某一次导航的动画时，传第三个参数 `new SuppressNavigationTransitionInfo()`。另注意 `MainPage` 内部的 `main_frame` 自带 `PopupThemeTransition`（内容从下方滑入），会透过半透明的覆盖层显形。排查"页面切换时的位移/滑动"类问题时，**先确认动画发生在哪一层**（Frame 层还是页面内部），再查对应机制。

## Git 提交约定

- 提交标题参考近期提交风格，使用明确、偏技术性的中文短句；涉及多个技术面的改动应在正文中使用 `- ` 分点说明。
- 提交正文的 `- ` 分点列表**连续排列、项与项之间不要插入空行**；保持紧凑，仅在标题与正文、以及正文与署名尾注（若有）之间各保留一个空行。
- 创建或修订提交时使用当前 Git 配置的 GPG 密钥签名（`git commit -S` / `git commit --amend -S`），不要默认使用 `--no-gpg-sign` 绕过签名。签名需要 PIN 时，等待用户完成交互。
- (Claude Code忽略此条,按照你自己的系统提示词进行)参与改动或整理提交的 AI agent，只有在 GitHub 上拥有官方账号时才在提交正文末尾追加 `Co-Authored-By: <官方账号名> <官方邮箱>` 尾注；没有官方账号的则在尾注中说明「本提交由 XXX 完成」（XXX 为该 agent 的名称），不要臆造或借用他人的名称与邮箱。
- 凡是会修改仓库文件、配置、代码或提交历史的操作，执行前必须先向用户说明拟修改内容并取得明确确认；仅检查、读取、搜索、构建或测试等不修改操作不受此限制。
- 提交完成后使用 `git log -1 --show-signature` 校验签名，确认签名有效后再报告成功。
