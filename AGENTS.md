# AGENTS.md

哔哩哔哩第三方UWP客户端。Fork 自 xiaoyaocz/biliuwp。

## 构建

- 需要 **Visual Studio 2019+**（UWP 工作负载）和 **Windows SDK 10.0.19041.0**
- 打开 `BiliBili.sln`，还原 NuGet 包，生成解决方案
- 主目标：`BiliBili.UWP`（AppContainerExe）；默认 Debug 平台为 `x86`
- Release 构建使用 `.NET Native toolchain`，部分依赖反射的代码行为可能不同
- 无 CI、无测试、无代码检查/格式化配置
- **不要尝试在命令行编译**，在 Visual Studio 中生成即可

## 架构

`BiliBili.sln` 包含三个项目：

| 项目 | 类型 | 用途 |
|---|---|---|
| `BiliBili.UWP` | UWP 应用 | 主客户端 — Pages、ViewModels、API 层、UI 控件 |
| `BiliBili.Background` | winmdobj | 后台任务，磁贴通知（关注动态轮询） |
| `BiliBili.JSBridge` | winmdobj | `[AllowForWeb]` 类，WebView JS 互操作（登录/验证码） |

### 关键目录（`BiliBili.UWP/` 下）

- `Api/` — API 接口定义（`ApiModel` 类）+ HTTP 客户端（`ApiRequest.cs`）
- `Helper/` — 工具类：SQLite、设置、WebClient、Wbi 签名、弹幕、日志、消息中心
- `Modules/` — ViewModel（业务逻辑），全部继承自 `IModules`
- `Pages/` — UI 页面，按功能分类（Home、Live、Music、User、Bangumi、FindMore、Season）
- `Views/` — 顶部导航视图（Home、Live、Bangumi、Channel、Find、Setting、Attention）
- `Models/` — 数据/API 响应模型
- `Controls/` — 自定义 UWP 控件（轮播、对话框等）
- `Converters/` — XAML 值转换器

### 请求/响应模式

1. API 类（如 `WbiAPI`、`HomeAPI`）定义 `ApiModel` 实例，设置 URL、方法、参数
2. 调用 `api.Request()`（扩展方法，位于 `ApiUtils.cs`），该方法添加签名/请求头后转发到 `ApiRequest.Get()`/`Post()`
3. `ApiRequest` 返回 `HttpResults`，可通过 `.GetJson<T>()`、`.GetJObject()`、`.GetResult<T>()`、`.GetData<T>()` 反序列化
4. Bilibili Wbi 签名：在 `ApiModel` 上设置 `api.useWbi = true`，`Request()` 扩展会调用 `ApiHelper.GetWbiSign()` 获取 Wbi keys（从 `/x/web-interface/nav`），再通过 `WbiEncodeHelper.EncWbi()` 应用签名

代码中还有一个**第二个、更老的 HTTP 层**（`Helper/WebClientClass.cs`），部分代码仍在使用。两者都使用 `Windows.Web.Http.HttpClient`。

### 导航

多处使用 `MessageCenter.SendNavigateTo(NavigateMode, Type, params)` 而非直接 `Frame.Navigate()`。`SplashPage` 为启动页；`MainPage` 为主外壳。协议激活（`bilibili://video/...`、`bilibili://live/...`）在 `App.OnActivated` 中处理。

### 播放器

`SYEngine`（NuGet: SYEngine.uwp）是主要媒体播放器，`VLC.MediaElement` 作为备选。播放器逻辑位于 `PlayurlHelper.cs` 和 `PlayerPage.xaml.cs`。视频 URL 解析从 Bilibili 的 playurl API 获取，构建 `SYEngine.Playlist` 对象，支持 DASH 或 FLV 模式。

### 本地存储

- **SQLite** 数据库：`ApplicationData.Current.LocalFolder\RRMJData.db` — 存储观看历史、观看进度、下载 GUID
- **设置**：通过 `SettingHelper`（UWP 项目）使用 `ApplicationData.Current.LocalSettings`。注意：`BiliBili.Background` 中有一个**重复的 `SettingHelper`**，key 常量不同
- **登录状态**：`access_key` 存储在 `LocalSettings` 中

### JS Bridge

WebView 的登录页面通过 `window.biliapp.ValidateLogin(data)` 和 `window.secure.Captcha()` 与原生代码通信，由 `BiliBili.JSBridge` 桥接。

## 代码风格

- 无 MVVM 框架 — 直接通过 `IModules` 基类实现 `INotifyPropertyChanged`
- ViewModel 异步操作返回 `ReturnModel<T>` 或 `ReturnModel`
- Bilibili API 响应使用 `ApiDataModel<T>`（含 `.data` 字段）或 `ApiResultModel<T>`（含 `.result` 字段）— 需根据具体 API 确定用哪个
- 注释主要为中文
- 代码质量较差，技术债严重（作者在 README.old.md 中自行警告）

## 关键陷阱

- `BiliBili.Background` 中的 `SettingHelper` 与 UWP 项目中的**是完全独立的两个类** — 共享 key 名但代码各自维护
- `ApiHelper.access_key` 是静态字段，登录后需手动设置
- `ApiHelper` 中的签名密钥（`AndroidKey`）与 `ApiUtils.AndroidKey` **不同** — `ApiHelper` 使用较新的 key
- `ApiHelper.VideoKey` 的 Appkey 为空（仅有 Secret）— 用于视频 playurl 签名
- `ApiRequest` 在 HTTP 过滤器中添加了 `IgnorableServerCertificateErrors.Expired`
- `CommentV2Control` 评论区外层 `ScrollViewer` 在切换视频时滚动位置不会自动重置。`ClearComment()` 中调用 `GetScollViewer()` 获取 `scrollViewer` 后再 `ChangeView(null, 0, null)`；`LoadComment()` 和 `LoadComment(LoadCommentInfo)` 两个重载也都需在 `GetScollViewer()` 后做复位（`VideoViewPage.LoadVideo()` 实际调用的是有参重载）
- 包标识：`5421.501019FA0C51B`，发布者 `CN=zhou2008`，版本 `3.11.3.0`
