# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

哔哩哔哩第三方 UWP 客户端。Fork 自 xiaoyaocz/biliuwp（原项目已停止维护）。

## 构建

- 需要 **Visual Studio 2019+**（UWP 工作负载）和 **Windows SDK 10.0.19041.0**（`TargetPlatformMinVersion` 为 10.0.16299.0）
- 打开 `BiliBili.sln`，还原 NuGet 包，生成解决方案
- 主目标：`BiliBili.UWP`（AppContainerExe）；`Any CPU` 会被映射到 `x86`
- 除 `Debug|x64` 外的所有 Release 配置都启用 `.NET Native toolchain`，依赖反射的代码在 Release 下行为可能不同
- 无 CI、无测试、无 lint/格式化配置
- **不要尝试在命令行编译**，在 Visual Studio 中生成即可

## 架构

`BiliBili.sln` 包含三个项目：

| 项目 | 类型 | 用途 |
|---|---|---|
| `BiliBili.UWP` | UWP 应用 | 主客户端 — Pages、ViewModels、API 层、UI 控件 |
| `BiliBili.Background` | winmdobj | 后台任务（`BackgroundTask.cs`），磁贴通知（关注动态轮询） |
| `BiliBili.JSBridge` | winmdobj | `biliapp` / `secure` 两个 `[AllowForWeb]` 类，WebView JS 互操作（登录/验证码） |

### 关键目录（`BiliBili.UWP/` 下）

- `Api/` — API 接口定义（返回 `ApiModel` 的方法）+ HTTP 客户端（`ApiRequest.cs`）+ 签名与扩展方法（`ApiUtils.cs`）
- `Helper/` — 工具类：SQLite、设置、WebClient、Wbi 签名、弹幕、日志、消息中心
- `Modules/` — ViewModel（业务逻辑），全部继承自 `IModules`
- `Pages/` — UI 页面，按功能分类（Home、Live、Music、User、Bangumi、FindMore、Season）
- `Views/` — 顶部导航视图（Home、Live、Bangumi、Channel、Find、Setting、Attention）
- `Models/` — 数据/API 响应模型
- `Controls/`、`Converters/`、`Theme/`、`Themes/` — 自定义控件、XAML 值转换器、样式

### 请求/响应模式

1. API 类（如 `WbiAPI`、`VideoAPI`、`SearchAPI`）的方法返回 `ApiModel` 实例，其中带 `baseUrl`、`method`、`parameter`、`body`、`headers`；`url` 是 `baseUrl + "?" + parameter` 的只读拼接
2. 调用 `api.Request()`（`ApiUtils.cs` 中的扩展方法），它按需注入请求头/Wbi 签名后转发到 `ApiRequest.Get()` / `ApiRequest.Post()`
3. `ApiRequest` 返回 `HttpResults`，用 `.GetJson<T>()`、`.GetJObject()`、`.GetResult<T>()`、`.GetData<T>()` 反序列化
4. Wbi 签名：在 `ApiModel` 上设置 `useWbi = true`，`Request()` 会调用 `ApiHelper.GetWbiSign(api.parameter)` — 它通过 `WbiAPI.GetWbiKey()` 请求 `/x/web-interface/nav`，用正则从 `wbi_img.img_url` / `sub_url` 里抠出 key，再交给 `WbiEncodeHelper.EncWbi()` 签名。**每次调用都会重新拉取 key，没有缓存**（README TODO 里也记着这一项）

`Request()` 里有个特殊分支：`baseUrl` 含 `search` 时会**整体覆盖** `api.headers` 成桌面 Chrome UA，调用方自己设置的 header 会被丢弃。

代码中还有**第二个、更老的 HTTP 层**（`Helper/WebClientClass.cs`），部分代码仍在使用。两者都基于 `Windows.Web.Http.HttpClient`。

### 导航

多处使用 `MessageCenter.SendNavigateTo(NavigateMode mode, Type page, params object[] par)` 而非直接 `Frame.Navigate()`；`NavigateMode` 决定落在哪个 Frame。`SplashPage` 为启动页，`MainPage` 为主外壳。协议激活（`bilibili://video/...`、`bilibili://live/...`）在 `App.OnActivated` 中处理。

注意有些页面存在新旧两个版本并存（`LivePage` / `LiveV2Page`、`SearchPage` / `SearchV2Page`）— 改动前先确认哪个才是当前在用的。

### 播放器

`SYEngine`（NuGet: SYEngine.uwp）是主要媒体播放器，`VLC.MediaElement` 作为备选。播放逻辑在 `Helper/PlayurlHelper.cs` 和 `Pages/PlayerPage.xaml.cs`。视频地址从 Bilibili playurl API 解析后构建 `SYEngine.Playlist`，支持 DASH 与 FLV 两种模式。弹幕用 `NSDanmaku`（原作者的另一个项目）。

### 本地存储

- **SQLite**：`ApplicationData.Current.LocalFolder\RRMJData.db`（`SqlHelper.DbPath`）— 观看历史、播放进度、下载 GUID
- **设置**：`SettingHelper` 走 `ApplicationData.Current.LocalSettings`
- **登录态**：`access_key` 存在 `LocalSettings`

## 代码风格

- 无 MVVM 框架 — `IModules` 基类直接实现 `INotifyPropertyChanged`，调用 `DoPropertyChanged(name)` 通知
- ViewModel 异步操作返回 `ReturnModel` / `ReturnModel<T>`（含 `success`、`message`）；异常统一交给 `IModules.HandelError()`，它会写日志并弹 `MessageDialog`
- Bilibili API 响应用 `ApiDataModel<T>`（`.data`）或 `ApiResultModel<T>`（`.result`）— 用哪个取决于具体接口
- 注释与用户可见文案均为中文
- 代码质量较差、技术债重（原作者在 `README.old.md` 里已自行说明）

## 关键陷阱

- `BiliBili.Background` 里的 `SettingHelper` 与 UWP 项目里的是**两个完全独立的类**，key 名共享但代码各自维护，改一处要记得同步
- `ApiHelper.access_key` 的 getter 只在 `_access_key == ""` 时回落到 `SettingHelper.Get_Access_key()`。字段默认值是 `null`，不等于 `""`，所以**未显式赋值过的情况下会直接返回 null 而不读设置**
- `ApiHelper.AndroidKey` 与 `ApiUtils.AndroidKey` **不是同一个 key**：`ApiHelper` 用的是 TV key（`4409e2ce...`，等于 `ApiUtils.AndroidTVKey`），`ApiUtils` 用的是旧 android key（`1d8b6e7d...`）
- `ApiHelper.VideoKey` 的 Appkey 为空字符串（只有 Secret）— 专用于视频 playurl 签名
- `ApiRequest` 在 HTTP 过滤器里加了 `IgnorableServerCertificateErrors.Expired`
- `ApiUtils.baseUrl` 指向第三方代理 `http://biliapi.iliili.cn`（明文 HTTP）
- `CommentV2Control` 评论区外层 `ScrollViewer` 在切换视频时不会自动复位。`ClearComment()` 需先 `GetScollViewer()` 再 `ChangeView(null, 0, null)`；`LoadComment()` 的两个重载也都要在 `GetScollViewer()` 之后复位（`VideoViewPage.LoadVideo()` 实际调的是带参重载）
- 包标识 `5421.501019FA0C51B`，发布者 `CN=zhou2008`，当前版本 `3.12.0.0`（发版时记得同步 `Package.appxmanifest`）

## 当前进度

`README.md` 里的 TODO 列表是这个 fork 的实际工作清单（已修复的 API 变更、待做的登录、直播弹幕、番剧信息等）。带删除线的条目是明确放弃的方向（频道、话题、音频），不要主动去做。
