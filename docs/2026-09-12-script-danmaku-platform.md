# 脚本弹幕平台（mode8 风格）

> **状态：阶段 1（核心渲染）已完成代码与构建验证，待页面级验证；阶段 2-5 未开始。** 见 §实施阶段。
> **规模提示**：本需求已从「加个 mode8 类似的东西」长成一个**平台级改动**（自建 WebView2 脚本运行时 + TS 转译 + 三类交互 + 四类拦截 + 几十条同屏渲染）。建议按下面阶段分批落地，每阶段可独立验证。
> **行号基准**：`PlayerPage.xaml.cs` / `PlayerPage.xaml` 的引用已按**阶段 1 实施后的 HEAD 重新校准**，逐条命中。注意：**这两个文件每实施一个阶段都会整体漂移**（阶段 1 就使 §7 的锚点偏移了 4～239 行不等），动手前请以**符号名**为准、行号仅作快速定位。其余文件（`PlayerAPI.cs` / `ApiHelper.cs` / `DanmakuMTC.cs` / `Generic.xaml` / `SendDanmakuDialog.xaml.cs` / `BiliDanmakuService.cs` / `PlaybackEventTimeline.cs`）行号未受阶段 1 影响。子模块文件行号以 `Libraries/NSDanmaku-Fork` 当前 pin `784d694` 为准。
> **合并带来的既成事实**：`BiliBili.Background` 侧新增 `DynamicFeedApi` / `DynamicFeedParser` / `NotificationBuilder` / `SettingHelper` 等文件（动态磁贴与更新通知功能）。这些是**另一条功能线，与本计划无交集**，但同处 `BiliBili.Background` 目录，实施时注意区分。

## Context

用户希望给这个 UWP 客户端加上「类似 B 站 mode8 代码弹幕」的能力：**弹幕内容是可编程脚本，跑起来产生任意画面**，并能拦截与普通弹幕/播放器的交互。

现状核查：

- 项目已有 BAS 弹幕（B 站 mode9）链路：`Controls/BasDanmakuControl.xaml(.cs)` 用 WebView2 承载 `Assets/bas-host.html` + `bas.js`，在 `PlayerPage.xaml:347` 叠加。但它消费的是 B 站下发的 mode9 数据，**不是用户可编程的框架**。
- mode==8 在 `BiliDanmakuService` 里被当「不支持」丢弃。精确路径：`ParseDanmaku`（`Helper/BiliDanmakuService.cs:856`）用 `TryToLocation(modeValue, out location)`（同文件 `:975` 起）判定位置，该 switch 只处理 1–5 与 9，mode 8 落 default → `unsupportedDanmakuCount++` 后 `return null`。
  > 注意别找错地方：`ParseDanmaku` 自己的 switch 是 **protobuf 字段号**，其中确实有 `case 8:`（`:898`，含义是 `ctime`），与弹幕 mode 无关。

**历史背景（决定本计划定位）**：仓库存在分支 `feature/m8-script-engine`，其中已有一版完整实现——自研脚本解释器（`BiliBili.UWP/Scripting/`，18 个文件，Scanner/Parser/VM）+ M8 语义 API 层（`M8DisplayApi`/`M8Motion`/`M8Tween`/`M8PlayerApi`）+ XAML/Win2D 渲染宿主，相对 master 实测 `27 files changed, +10442/-17`。该分支最终以提交 `25151ba「放弃支持: 大部分mode8代码弹幕api返回不完整」` 收尾，**失败根因是消费 B 站下发的 mode8 数据不可靠**。

本计划因此定位于**全新自建**（不基于该分支的实现），并**自建数据源**以绕开该根因。

## 需求（已确认）

| 维度 | 结论 |
|---|---|
| 画面能力 | 文本特效、矢量图形与运动路径、图片/GIF 动图、粒子/物理、**3D** |
| 脚本语言 | **JavaScript + TypeScript**（内嵌官方 tsc 转译） |
| 创作者 | 用户**自己手写代码** |
| 交互 | 读取弹幕数据、控制普通弹幕显示、发送弹幕 |
| **拦截** | **用户输入事件、播放器操作、弹幕数据流、发送弹幕请求**（四类全要） |
| 规模 | **大量（同屏几十条+）** |
| 与 BAS 关系 | 并列独立宿主，现有 BAS 机制不改 |
| 数据源 | 自建（本地脚本文件），不依赖 B 站 mode8 返回 |

## 选型结论：JS/TS + WebView2（Canvas/WebGL）

- **3D / 粒子 / 动图 / 联网素材 / 几十条同屏** → 只有 WebView2(Chromium) 的 WebGL/Canvas/图片解码/HTTP 覆盖；手写代码需要真语言。
- **Lua（MoonSharp）出局**：渲染能力要全部自建，且 UWP + .NET Native 下 AOT 兼容性不确定。
- **C# 脚本（Roslyn）/ NeoLua 出局**：UWP Release 走 .NET Native AOT，运行时动态代码生成被禁用（`Reflection.Emit` 抛 `PlatformNotSupportedException`）。
- **TypeScript**：不与选型冲突，只是 JS 之上加一层转译；`transpileModule` 跑在 WebView2 里，与 .NET Native 无关。

**硬约束（由需求导出）：**

1. 渲染必须走统一 Canvas/WebGL 循环，**禁止每条弹幕一个 DOM 元素**。
2. 跨进程 RPC 必须**批量 + 节流 + 无注册时零开销**（输入/弹幕流拦截都在高频路径上）。
3. `DispatcherTimer.Tick` 不支持 await，弹幕流拦截必须用「每 tick 一次批量 RPC + 同步查表」，不能让 `ShowDanmaku` 逐条 await。

## 架构

```
脚本文件(.js) ──► ScriptDanmakuService ──► ScriptDanmakuControl(WebView2)
                      │                            │ postMessage 双向桥
                      └ ScriptDanmakuParser        │
                       （校验，可单测）              │
   PlayerPage（弹幕数据 / 屏蔽 / 发送 / 输入 / 播放操作）◄─┘
                              ▼
                   Assets/script-danmaku-host.html
        （Canvas 渲染循环 + 脚本运行时；TS 转译待接入）
```

**关键决策：交互与拦截的"处理权"全在 PlayerPage，控件只转发。** 控件通过事件把脚本的请求抛给 PlayerPage；PlayerPage 用自己已有字段/方法处理，再调控件方法把结果推回。这样 `PlayerPage` 的 private 成员**无需改可见性**。

## 详细设计

### 1. 数据模型（`Models/ScriptDanmakuModel.cs`）

```csharp
public sealed class ScriptDanmakuModel
{
    public string id { get; set; }
    public double stime { get; set; }      // 秒
    public double duration { get; set; }   // 秒，默认 3
    public string lang { get; set; }       // "js" | "ts"，默认 js
    public string code { get; set; }
}

public sealed class ScriptDanmakuDocument
{
    public int version { get; set; } = 1;
    public string title { get; set; }
    public List<ScriptDanmakuModel> items { get; set; }
}
```

### 2. TypeScript 支持

- 资产：`Assets/typescript.js`（官方 tsc，含 `transpileModule`；原始约 8–10 MB，gzip 后约 2–3 MB——**实施时实测**）。
- 流程：`lang=="ts"` 的脚本 → 宿主 `ts.transpileModule(code, { compilerOptions: { target: ES2020, module: None } })` → JS → `new Function('ctx', js)`。`lang=="js"` 跳过转译。
- 转译结果按脚本 id 缓存，只转一次。
- **不做类型检查**（只转译），避免拖慢。
- csproj `<Content Include="Assets\typescript.js" />`。**代价：包体积 +几 MB**，这是「应用内直接写 TS」的必付成本。

### 3. 宿主运行时（`Assets/script-danmaku-host.html`）

> 单文件内联，不拆 `.js`——虚拟主机映射下同目录引用没有额外收益，且少一处 csproj 注册。

- **渲染循环**：主 `<canvas>` + `requestAnimationFrame`，每帧遍历活跃弹幕（`stime <= now <= stime+duration`）调绘制回调。**不用 DOM-per-danmaku**。无活跃脚本且非播放态时自动停循环。
- **脚本 API（`ctx`）**——阶段 1 实际提供：`ctx.t` / `ctx.duration` / `ctx.progress` / `ctx.stime` / `ctx.id`、`ctx.width` / `ctx.height` / `ctx.dpr`、`ctx.g`（Canvas 2D，已按 dpr 预置变换，坐标是 CSS 像素）、`ctx.pause()` / `ctx.seek(seconds)` / `ctx.navigate(url)`。
  > **`ctx.t` 是本条弹幕内的相对时间（0 → duration）**，不是全局播放时间；全局时间由 `ctx.stime + ctx.t` 得到。
  > 待接入（阶段 2-4）：`ctx.createLayer(w,h)`（离屏 canvas，供 WebGL/3D 自绘后合成）、`ctx.danmaku`（弹幕快照）、`ctx.intercept.*`、`ctx.filter(...)` / `ctx.send(...)` / `ctx.on(...)`。
- **时间同步**：照搬 `bas-host.html` 已验证的 `state` + `currentPositionMs()` 模式（playing 时 `performance.now()` 外推，`setState`/`seek` 重置基准）。
- **桥协议（JSON）**：
  - 主→宿主：`reset` / `append` / `beginItem` / `appendItemChunk` / `endItem` / `setState` / `seek` / `visible` / `resize`
  - 宿主→主：`ready` / `parsed` / `rendered` / `error` / `action`（含 `pause` / `seek` / `navigate`）
  > 设计稿另列的 `clear` / `danmakuBatch` / `judgeBatch` / `inputEvent` / `playbackAction` / `sendRequest` / `queryDanmaku` / `filter` / `send` / `judgeResult` / `inputVerdict` / `actionVerdict` 属阶段 2-4。

### 4. 控件（`Controls/ScriptDanmakuControl.xaml(.cs)`）

公开 API 与 `BasDanmakuControl` 同形，并扩展拦截/交互：

```csharp
// 生命周期（同 BasDanmakuControl 形态）—— 阶段 1 已实现
public Task ReplaceAsync(IEnumerable<ScriptDanmakuModel> items, double pos, bool play, bool visible, double rate);
public Task ClearAsync();
public Task SetPlaybackStateAsync(double pos, bool shouldPlay, double rate);
public Task SeekAsync(double pos, bool shouldPlay, double rate);
public Task SetVisibleAsync(bool visible);

// 宿主侧脚本请求播放器动作（阶段 1 已实现）
public event EventHandler<ScriptDanmakuActionEventArgs> ActionRequested;

// 以下属阶段 2-4，尚未实现 —— 交互回推
public Task PushDanmakuBatchAsync(IEnumerable<object> snapshot);

// 拦截询问（返回脚本裁决）
public Task<bool> TryHandleInputAsync(string kind, double normX, double normY, object payload);
public Task<PlaybackActionVerdict> InterceptPlaybackActionAsync(string kind, object payload);
public Task<IReadOnlyCollection<string>> JudgeDanmakuBatchAsync(IEnumerable<object> items); // 返回放行集合
public Task<SendVerdict> InterceptSendAsync(string text, string color, int mode, double pos);
```

内部复用 BAS 控件的成熟机制形态（独立实现）：`SemaphoreSlim` 命令门、`contentVersion` 防竞态、初始化 + 10s 就绪超时、大 payload 分块、一次性失败提示（`Utils.ShowMessageToast` + `LogHelper.WriteLog`）。WebView2 **懒初始化**。

### 5. PlayerPage 接入（不改 BAS）

- `PlayerPage.xaml`：`:347` 后并列 `<c:ScriptDanmakuControl x:Name="scriptDanmakuControl" />`（落在 `:348`，位于 `basDanmakuControl` 之上、`interactiveDanmakuControl` 之下）；`DanmakuMTC.MoreMenuFlyout`（`:322-339`）加「加载代码弹幕 / 加载示例代码弹幕 / 清除代码弹幕」。（第三项为实施期追加，便于不备文件即验证渲染链路。）
- `PlayerPage.xaml.cs`：新增独立命名的 `scriptDanmuPool` / `lastScriptDanmakuPosition` / `SyncScriptDanmakuPosition()` / `SyncScriptDanmakuPlaybackState()` / `SetScriptDanmakuPool()` / `ReplaceScriptDanmakuWindow()` / `ClearScriptDanmaku()` / `GetScriptDanmakuPlaybackRate()`。
- 在既有事件里**并列追加一行**（BAS 方法体一行不改）：
  - `PlaybackSession_PositionChanged`（`:278`、`:291`）→ `SyncScriptDanmakuPosition();`
  - `PlaybackSession_PlaybackStateChanged`（`:506`）→ `SyncScriptDanmakuPlaybackState();`
  - 换集/清理（`:1074`、`:2224`、`:4684`）→ `ClearScriptDanmaku();`
  - 弹幕总开关 `MTC_OpenDanmaku`（`:3733`）→ `SetVisibleAsync` + `SyncScriptDanmakuPlaybackState()`
  - 倍速 `slider_Rate_ValueChanged`（`:4487`）→ `SyncBasDanmakuPlaybackState()` 旁并列 `SyncScriptDanmakuPlaybackState()`（**漏掉会导致时间轴永久漂移，见 §实施记录·审查修正 1**）
  - 宿主动作 `ScriptDanmakuControl_ActionRequested`（`:3747` 起）→ `Pause` / `SeekFromScriptDanmaku` / `NavigateFromScriptDanmakuAsync`（后者按 BAS 同款规则只放行 bilibili.com 的 https 链接）
- **不做时间窗**：脚本弹幕条数少，`ReplaceAsync` 全集注入让宿主自调度。

### 6. 三类交互

**① 读取弹幕数据**：数据源 `PlayerPage.DanMuPool`（`:915`，全量）+ `danmu.GetDanmakus()`（子模块 `Libraries/NSDanmaku-Fork/NSDanmaku/Controls/Danmaku.xaml.cs:1729`，在屏，public）。字段全 public：`text/color/time/sendID/rowID/location/source`。宿主 `queryDanmaku(range)` → PlayerPage 取时间窗快照 → `PushDanmakuBatchAsync`（分块）。`SetDanmakuPool`（`:1548`）/`AppendDanmakuPool`（`:1559`）调用点顺带推增量。

**② 控制普通弹幕显示**：单条屏蔽 `danmu.Remove(model)`（`Danmaku.xaml.cs:1646`，public；**需同一实例、不支持 Position**）——按 `rowID` 查实例；持久屏蔽复用 `DanDis_Add(text, isYonghu)`（**`PlayerPage.xaml.cs:1790`，不是 NSDanmaku 侧**；调用点 `:1753`、`:3580`）；分层隐藏 `danmu.HideDanmaku/ShowDanmaku(location)`（`Danmaku.xaml.cs:1768`/`:1791`）。
> **子模块边界**：`danmu.Remove` / `HideDanmaku` / `ShowDanmaku` 都在子模块 `Libraries/NSDanmaku-Fork` 内。若交互 ② 必须改子模块代码，会牵动子模块指针，需单独决策。**实施时优先评估能否只靠 `GetDanmakus()` 快照 + PlayerPage 侧过滤（即走拦截③的放行集合）实现，避免动子模块。**

**③ 发送弹幕**：**不要复用 `PlayerAPI.SendDanmu`**（`Api/PlayerAPI.cs:117`）——它虽 public 且全仓库无调用点，但走的是 `ApiUtils.AndroidVideoKey` + `ApiUtils.GetSign` 这条与现网发送不同的链路，风控行为未经验证。**现网真实发送路径是 `Controls/SendDanmakuDialog.xaml.cs:57`**：自拼 `https://api.bilibili.com/x/v2/dm/post?access_key=...&appkey={ApiHelper.AndroidKey.Appkey}&...`，用 `ApiHelper.GetSign(url)` 签名，body 含 `msg/mode/progress/color/fontsize/pool/rnd/plat/type`。
实施建议：**以 `SendDanmakuDialog` 为事实来源抽取一个可复用发送方法**（或在 `PlayerAPI` 中新增一个与之一致的 `ApiModel`），并在阶段 4 前先实测该接口可用性。前置：`ApiHelper.IsLogin()`/`access_key`（`Helper/ApiHelper.cs:170`/`:54`）、`playNow.Aid`/`playNow.Mid`。发送后本地注入渲染层，参照 `MTC_SendDanmakued`（`:4371`）。

### 7. 四类拦截

**① 用户输入事件**
- 现状：唯一注册是 `playerSurface.AddHandler(UIElement.TappedEvent, PlayerSurface_Tapped, true)`（`:114-117`）；`PlayerSurface_Tapped`（`:4976`）**当前丢弃了 `TryHandleTapAsync` 返回值、从不设 `e.Handled`**（BAS 的点击消费实际未生效）。
- 设计：补上 `if (await scriptDanmakuControl.TryHandleInputAsync("tap", x, y, ...)) { e.Handled = true; return; }`；并按需在 `playerSurface` 追加 `PointerPressed/PointerReleased/PointerMoved/PointerWheelChanged` 的 `AddHandler(..., handledEventsToo: true)`；手势在 `MTC_ManipulationStarted`（`:3191`）/`Grid_ManipulationDelta`（`:3077`）里加判定。
- **注意**：`handledEventsToo:true` 下设 `Handled` 仍会触达其他已注册处理器——「完全阻断」需在最底层控件上处理，实施时按实际体验决定拦截粒度。

**② 播放器操作**
- 入口：`MTC_*` 处理器（`MTC_DoubleTapped:4318`、`MTC_Next:4361`、`MTC_Previous:4366`、`MTC_FastForward:4559`）、键盘 `PlayerPage_KeyDown`（`:665`，`:668` 处无条件 `Handled=true`；按键映射见 673–796）、程序化 `btn_Play_Click:3056`/`btn_Pause_Click:3062`。
- 设计：在各入口前置一次 `InterceptPlaybackActionAsync(kind, payload)`，按裁决放行/阻止/改写。
- **缺口**：MTC 的播放/暂停按钮**没有事件**（基类模板直接控制 MediaPlayer）。要拦截需在 `Controls/DanmakuMTC.cs` 的 `OnApplyTemplate`（`:110`）后 `GetTemplateChild("PlayPauseButton")` 追加 handler 并抛事件——这会改动 `DanmakuMTC`（共享控件），实施时评估是否必要。
  > 好消息：模板子控件确实叫 `PlayPauseButton`（`Themes/Generic.xaml:823`，另有 `PlayPauseButtonOnLeft` `:686`，需两者都挂），且 `DanmakuMTC` 已有成熟的挂载/解绑范式可直接套用——`AttachClick(name, handler)`（`:203`）+ `templateDetachActions` 列表，在 `OnApplyTemplate` 开头 `DetachTemplateHandlers()`（`:112`）。照抄即可，不必新造机制。

**③ 弹幕数据流**
- 采纳「每 tick 一次批量判定 + 同步查表」，避免逐条 await：
  - `Timer_Date_Tick`（`:1585`）改为带重入保护的 `async void`（100ms tick 重入是真实风险）。
  - 取 `batch.Items` 后一次性把整批发给宿主（一次 `ExecuteScriptAsync` 传 JSON 数组，`Modules/Playback/PlaybackEventTimeline.cs:27` 的 `Advance(double position)` 已整批返回，含 `WasDiscontinuity`）。
  - 拿到放行集合后，`ShowDanmaku`（`:1617`）保持同步，仅在过滤链最前（`:1619` 附近）加 `if (!allowSet.Contains(key)) return;`。
- **零开销原则**：脚本未注册弹幕拦截器时，整条路径直接跳过，不做任何 RPC。
- 需与 `MTC_SendDanmakued` 的自发弹幕路径（不走 `ShowDanmaku`）协调。

**④ 发送弹幕请求**：`MTC_SendDanmakued`（`:4371`）发送前置 `InterceptSendAsync(...)`，按裁决放行/改写/阻止。

### 8. 数据服务

分两层，纯逻辑与 UWP 依赖分离，前者可直接被 `tests/BiliBili.Tests` 编译：

- **`Modules/ScriptDanmakuParser.cs`**（纯逻辑，无 UWP 依赖）：`Parse(string)`（Json.NET 反序列化 + 校验）、`Normalize(IEnumerable)`（过滤 `stime<0`、NaN/Infinity、空 `code`；补 `id`/`duration`/`lang`，duration 钳制到 `[0, 600]` 且缺省 3）、`NormalizeLang`（除明确 ts 外一律 js）、常量。
- **`Helper/ScriptDanmakuService.cs`**（UWP 侧，薄封装）：`LoadFromFileAsync(StorageFile)` 读文件后委托 Parser，空结果记 `LogHelper`；`GetBuiltInDemo()` 提供两条 JS 内置示例（文字横移、粒子环）。

> **实施注记**：`Parse` 对 JSON 非法返回空列表，单条非法只丢弃该条——保证坏文件不会让整个功能失效，也不会静默产生半截集合。

### 9. csproj 注册

新增 `.cs` 用 `<Compile Include="..." />`，XAML 代码后置加 `<DependentUpon>`；`.xaml` 用 `<Page Include="...">`（含 `Generator` + `SubType`）；新 Assets（`script-danmaku-host.html`、`typescript.js`）用 `<Content Include="Assets\..." />`。

## 实施阶段

| 阶段 | 内容 | 验收 | 状态 |
|---|---|---|---|
| **0. Spike** | 验证两件事：① 官方 `typescript.js` 的 `transpileModule` 在 WebView2 里跑通；② Canvas/WebGL 在同屏几十条粒子/3D 下的帧率 | 可行性结论 | **未做**（TS 暂缓，见下） |
| **1. 核心渲染** | 模型 + 服务 + 宿主运行时 + 控件；加载本地 `.js` → 叠加显示、随播放同步 | 看到代码弹幕效果 | **代码完成，待页面级验证** |
| **2. 弹幕链路** | 读取弹幕数据 + 弹幕数据流拦截 | 脚本能读弹幕、能拦弹幕 | 未开始 |
| **3. 输入与操作** | 用户输入拦截 + 播放器操作拦截 | 脚本能消费点击、能阻止播放操作 | 未开始 |
| **4. 控制与发送** | 控制弹幕显示 + 发送弹幕 + 发送拦截 | 三项各验一次 | 未开始 |
| **5. 打磨** | 性能（批渲染/图层缓存/活跃剔除/节流）、可见性跟随 `LoadDanmu`、错误提示、懒初始化 | 综合体验 | 未开始（懒初始化已提前落地） |

### 阶段 1 实施记录

已落地的文件：

| 文件 | 说明 |
|---|---|
| `Models/ScriptDanmakuModel.cs` | `ScriptDanmakuModel` + `ScriptDanmakuDocument` |
| `Modules/ScriptDanmakuParser.cs` | 纯逻辑校验/规范化（可单测） |
| `Helper/ScriptDanmakuService.cs` | 文件读取 + 内置示例 |
| `Controls/ScriptDanmakuControl.xaml(.cs)` | WebView2 宿主控件，API 与 `BasDanmakuControl` 同形 |
| `Assets/script-danmaku-host.html` | Canvas 渲染循环 + 脚本运行时（单文件，未拆 `.js`） |
| `PlayerPage.xaml(.cs)` | 并列托管 + 三个菜单项 + 位置/状态同步 |

与设计稿的差异（均为实施中的实测结论）：

1. **TS 转译暂缓**：按决策先只做 JS，`Assets/typescript.js` 未引入。宿主对 `lang == "ts"` **显式抛错**并上报日志（文案含「TS 转译尚未接入」），不静默降级。
2. **宿主是单文件**：设计稿写 `.html` + `.js` 两个文件，实测内联更简单——`SetVirtualHostNameToFolderMapping` 下同目录引用没有额外收益，且少一处 csproj 注册。
3. **`ctx` 字段与设计稿的差别**：实际提供 `{ g, width, height, dpr, t, duration, progress, stime, id, pause, seek, navigate }`。`t` 是**本条弹幕内的相对时间**（0→duration），全局播放时间另由 `stime + t` 得到。`createLayer` / `danmaku` / `intercept` / `filter` / `send` / `on` 属阶段 2-4，未实现。
4. **画布按 `devicePixelRatio` 缩放**，脚本拿到的 `width`/`height` 仍是 CSS 像素，脚本作者无需处理 DPI。
5. **懒初始化闸门提前落地**：`pendingItemCount == 0 && !isPageReady && initializationTask == null` 时直接返回，不创建 WebView2。
6. **新增「加载示例代码弹幕」菜单项**（设计稿只有加载/清除两项），便于不准备文件就验证渲染链路。
7. **不做时间窗**：按设计稿，全集 `ReplaceAsync` 交给宿主自调度，未套用 BAS 的 lookback/lookahead 窗口。

测试：`tests/BiliBili.Tests/` 下三个文件——`ScriptDanmakuParserTests.cs`（解析/校验契约）、`ScriptDanmakuHostContractTests.cs`（宿主↔控件字符串契约：命令名、消息类型、`ctx` 字段、dpr 缩放、可见性、自停位置、单脚本失败隔离、不引入 BAS 资产、不为每条弹幕建 DOM）、`ScriptDanmakuPlayerPageContractTests.cs`（PlayerPage 接入完整性：倍速重推、可见性重推、PositionChanged 两条分发路径、清理点对称、层叠顺序、菜单处理器、跳转白名单）。

### 阶段 1 代码审查修正（已落地）

首轮实现后做了一次独立审查，发现并修掉三个缺陷：

1. **倍速变更后脚本时间轴永久漂移**（中高）。`slider_Rate_ValueChanged` 只调了 `SyncBasDanmakuPlaybackState()`，漏了脚本侧。宿主 `currentPositionMs()` 按 `state.rate` 外推，rate 不更新则宿主时钟与视频每秒累积偏差；而漂移判据 `expected = lastPos + elapsed * GetScriptDanmakuPlaybackRate()` 用的是媒体**新**倍速、`lastScriptDanmakuPosition` 又每次 `PositionChanged` 刷新，所以 `shouldSeek` 恒为 false，**不存在自愈路径**，只有暂停/seek/换集才会重推。修法：在该处并列 `SyncScriptDanmakuPlaybackState();`。
2. **关闭弹幕开关后脚本弹幕会重新出现**（中）。`MTC_OpenDanmaku(false)` 先发 `SetVisibleAsync(false)`（宿主 `stopRunning()` + 清屏），随后 `SyncScriptDanmakuPlaybackState()` 发 `setState(pos, false, rate)`，而后者的非播放分支无条件 `drawFrame()`——当时 `drawFrame` 不读 `visible`，于是把活跃脚本重画回屏幕，且 `running` 已为 false、没有后续帧清理。窗口缩放走 `resize()` 同理。修法：`drawFrame` 开头清屏后若 `!visible` 直接返回 false。
3. **rAF 自停可能并存两条帧链**（低）。原先在 `drawFrame` 内置 `running = false`，而 `frame()` 随后**无条件**续帧；若在旧句柄触发前（≤1 vsync）发生 `setState(playing=true)`，两条链会各自递归，每帧画两次。修法：`drawFrame` 改为返回「是否有活跃脚本」，自停判定移到 `frame()` 内并在终止分支直接 `return`。

同时删掉一处死代码：`frame()` 里算了 `elapsed` 并钳制到 250ms，但该值从未被使用（`ctx` 没有 delta 字段），`lastFrameTime` / `DEFAULT_FRAME_MS` 一并移除。原契约测试曾断言这段钳制存在，属于「测试通过但保护是空的」，已随之改写为可见性/自停断言。

> 已用「注入缺陷 → 确认对应测试失败 → 恢复」的方式反向验证了新契约测试的有效性（`RateChange_ResyncsBothDanmakuClocks`、`Host_DrawFrameHonoursVisibility` 均如期失败）。
> 另修正一处文档自身的虚假陈述：本节早先写「倍速处已由 `SyncScriptDanmakuPlaybackState()` 覆盖」，实际代码当时并没有——现已补上。

## 验证

- **构建**：VS 打开 `BiliBili.sln`，`Debug|x86` 生成（不要用 `dotnet build`）。
- **页面级验证**（必须，单测覆盖不到）：
  1. 加载 `.js` → 能渲染（用「加载示例代码弹幕」最快）。`.ts` 待 TS 转译接入后再验。
  2. 暂停/seek/倍速/全屏/横竖屏/切集 → 同步正确、不残留、画布尺寸正确。
  3. **同屏几十条粒子/3D → 帧率可接受（性能验收点）**。
  4. 四类拦截各验一次：点击被消费、播放操作被阻止、弹幕被拦下、发送被拦下。
  5. 三类交互各验一次：读到弹幕数据、屏蔽一条、发送一条。
  6. 弹幕总开关关闭 → 脚本弹幕隐藏；未加载时不初始化 WebView2。
  7. **未注册任何拦截器时，播放/弹幕/输入路径无可感知开销**（性能回归点）。
- **接口可用性前置**：阶段 4 开工前，先按 `Controls/SendDanmakuDialog.xaml.cs:57` 的参数与签名形态实测 `x/v2/dm/post`，确认可用后再决定抽取方式（见 §6③）。不要先按 `PlayerAPI.SendDanmu` 实现。
- **回归**：BAS 弹幕（mode9）行为不变。
- **测试**：`tests/BiliBili.Tests`（net8.0 + MSTest）。阶段 1 已补 `ScriptDanmakuParserTests`（18 例）、`ScriptDanmakuHostContractTests`（20 例）、`ScriptDanmakuPlayerPageContractTests`（8 例），共 46 例，全套 199 例通过。覆盖不到的部分——实际渲染、时间同步、性能——必须走页面级验证。
- **日志**：`LogHelper` 无脚本弹幕渲染失败。

## 风险

- **性能（最大）**：几十条粒子/3D + 高频拦截 RPC。对策：Canvas/WebGL 批渲染、剔除不活跃、批量+节流+无注册零开销、限制同时活跃的 WebGL 层数。
- **Tick 重入**：`Timer_Date_Tick` 改 async 后必须加重入保护，否则弹幕顺序错乱。
- **拦截粒度局限**：`handledEventsToo:true` 无法完全阻断已注册处理器；MTC 播放/暂停按钮无事件（需改共享控件 `DanmakuMTC`）。二者都要在实施时按体验取舍。
- **包体积**：内嵌 tsc 使包 +几 MB（换取应用内直接写 TS）。`Assets/typescript.js` **当前不存在，需实施时引入**；`Assets/` 下现有的是 BAS 资产（`bas-host.html`、`bas.js`、`bas-jquery-shim.js`），注册风格见 `BiliBili.UWP.csproj:110-113`。
- **脚本安全**：本地文件 + 本地宿主 + 禁新窗口导航 + 白名单消息；**联网素材是明确需求**，需限定/记录资源请求域名。发送弹幕涉及登录态与风控，复用现有链路、不自行绕过。
- **.NET Native（Release AOT）**：新 C# 代码避免反射/`dynamic`，JSON 用显式模型反序列化。
- **多 WebView2 实例内存**：控件懒初始化。（阶段 1 已落地闸门，见 §实施记录 5。）
- **能力边界**：`danmu.Remove` 不支持 Position、需实例引用——交互 ② 按 `rowID` 查实例并接受该限制。
- **子模块改动**：交互 ② 若被迫修改 `Libraries/NSDanmaku-Fork` 内的 `Danmaku.xaml.cs`，会推进子模块指针并影响 `NSDanmaku` 项目本身。优先用「快照 + PlayerPage 侧过滤」绕开。

### 阶段 1 暴露的待办

- **脚本无沙箱隔离**：`new Function` 在宿主页面上下文里跑，脚本可访问 `window` / `document` / `fetch`。本地文件 + 本地宿主的场景下风险可控，但**联网素材是明确需求**，阶段 5 需评估是否加 CSP 或资源域名白名单。设计稿的「禁新窗口导航 + 白名单消息」尚未落到代码。
- **`LogHelper` 会写出 WebView2 侧上报的细节**：脚本报错内容经 `Truncate` 截到 500 字符后进日志，多次同类错误已做频次上限（编译 8 次、运行时 8 次），避免刷爆日志。
- **暂停时的最后一帧**：`setState(playing=false)` 会补画一帧让画面停在脚本进度上。若脚本依赖 `requestAnimationFrame` 之外的定时器，暂停时不会继续推进——这是预期行为，但需在写示例脚本时注意。
- **播放中即使无活跃脚本也不停循环**（已审查确认，暂不修）：`drawFrame` 的停止条件是 `!anyActive && !state.playing`，所以视频在播时即使用户只加载了一条早已播完的脚本，仍会以 60fps 空转清屏。全集注入（无时间窗）放大了这个常驻开销。若要优化，可在 `now > 最大 endMs` 时也停循环——恢复路径天然存在（seek/播放态变化都会走 `setState` → `ensureRunning`）。留待阶段 5。
- **脚本无法感知「帧间隔」**：`ctx.t` 是相对时间，有状态的脚本（粒子等）需自行记录上一帧的 `ctx.t` 求差。示例脚本用纯函数形式规避了这一点。
