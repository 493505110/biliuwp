# 脚本弹幕平台（mode8 风格）

> **状态：阶段 1（核心渲染）已完成代码与构建验证，待页面级验证；阶段 2-5 未开始。** 见 §实施阶段。
> **本版修订（渲染模型）**：原设计「每帧遍历活跃弹幕调绘制回调」的**立即模式已废弃**，改为**保留模式**——脚本每条只执行一次、逐帧只推进 tween 与重绘脏元素。理由、真实脚本数据与具体形态见 **§3.1**；代码已按此改造并补齐脏矩形擦除 / 寿命语义等收尾（D1~D6），实施记录见 §阶段 1 实施记录 8、9。
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

1. 渲染必须走统一 Canvas/WebGL 循环，**禁止每条弹幕一个 DOM 元素**；且必须是**保留模式**——脚本执行一次建元素、引擎逐帧插值属性，**禁止每帧重跑脚本绘制**（见 §3.1）。
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
        （保留对象树 + tween 补间 + 脏元素重绘；脚本每条只执行一次；TS 转译待接入）
```

**关键决策：交互与拦截的"处理权"全在 PlayerPage，控件只转发。** 控件通过事件把脚本的请求抛给 PlayerPage；PlayerPage 用自己已有字段/方法处理，再调控件方法把结果推回。这样 `PlayerPage` 的 private 成员**无需改可见性**。

## 详细设计

### 1. 数据模型（`Models/ScriptDanmakuModel.cs`）

```csharp
public sealed class ScriptDanmakuModel
{
    public string id { get; set; }
    public double stime { get; set; }      // 秒
    public double duration { get; set; }   // 秒；缺省/非正数 = 不设时间窗
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

- **渲染循环（保留模式）**：主 `<canvas>` + `requestAnimationFrame`。脚本**每条只执行一次**，执行期间通过 `ctx` 建出保留对象树（`ctx.createText/createShape/createImage/createLayer`），并把动画写成声明式 tween 配置；之后每帧只做三件事——推进 tween、更新元素属性、**仅重绘被标记为脏的元素**。**禁止每帧重跑脚本**（理由与真实数据见 §3.1）。**不用 DOM-per-danmaku**。不在播放且没有待推进的补间/逐帧回调时自动停循环（恢复路径天然存在：播放态变化、seek、resize 都会走 `ensureRunning`）。
- **脚本 API（`ctx`）**——阶段 1 实际提供：`ctx.width` / `ctx.height` / `ctx.dpr` / `ctx.duration` / `ctx.stime` / `ctx.id` / `ctx.time` / `ctx.state`、`ctx.createText(text, style)` / `ctx.createShape()` / `ctx.createImage(url)` / `ctx.createLayer(w, h)`、`ctx.addChild(el, parent?)` / `ctx.removeChild(el)`、`ctx.tween(el, config, options?)`、`ctx.onFrame(fn)`（逃生舱，见下）、`ctx.pause()` / `ctx.seek(seconds)` / `ctx.navigate(url)`。元素属性 `x`/`y`/`z`/`alpha`/`scaleX`/`scaleY`/`rotation`（=`rotationZ`）/`visible`/`matrix`/`filters` 可写，直接赋值即标脏。
  > **`ctx.t` 与 `ctx.progress` 恒为 0**：脚本每条只执行一次，执行时没有任何时间推进，这两个量不再有逐帧语义，**不得**用它们驱动逐帧机制（`t`/`progress` 仍在对象上只为兼容立即模式脚本的读取而不报错）。
  > **`ctx.time` / `ctx.state` 是 M8 `Player.time` / `Player.state` 的等价物**：`time` 是播放头位置（**毫秒**，与 M8 的 `Player.time` 同单位，原版脚本可直接移植；注意 `ctx.duration` / `ctx.stime` 仍是秒），`state` 取 `"playing"` / `"pause"` / `"stop"`（宿主没有独立于可见性的停止态，弹幕层隐藏时给 `stop`）。脚本每条只执行一次，激活时读到的是一次快照；逐帧读时间要在 `ctx.onFrame` 回调里读 `frameCtx.time` / `frameCtx.state`——值来自宿主自己的外推时钟，不增加消息往返。
  > **`ctx.g` 已取消**：直接操作画布等于绕开保留模式，脚本只能通过 `createShape`/`createText` 建保留对象。
  > **`ctx.onFrame(fn)` 是逃生舱**：只有 tween 表达不了的效果（如物理演算）才该用它；注册后该条目退回逐帧调用，单条脚本的每帧开销不再只与「脏元素数」相关。**能写成 tween 的不要用 onFrame**。
  > 待接入（阶段 2-4）：`ctx.danmaku`（弹幕快照）、`ctx.intercept.*`、`ctx.filter(...)` / `ctx.send(...)` / `ctx.on(...)`。
- **时间同步**：照搬 `bas-host.html` 已验证的 `state` + `currentPositionMs()` 模式（playing 时 `performance.now()` 外推，`setState`/`seek` 重置基准）。
- **桥协议（JSON）**：
  - 主→宿主：`reset` / `append` / `beginItem` / `appendItemChunk` / `endItem` / `setState` / `seek` / `visible` / `resize`
  - 宿主→主：`ready` / `parsed` / `rendered` / `error` / `action`（含 `pause` / `seek` / `navigate`）
  > 设计稿另列的 `clear` / `danmakuBatch` / `judgeBatch` / `inputEvent` / `playbackAction` / `sendRequest` / `queryDanmaku` / `filter` / `send` / `judgeResult` / `inputVerdict` / `actionVerdict` 属阶段 2-4。

#### 3.1 保留模式：脚本只执行一次，逐帧只插值属性（本版新增，取代原「每帧调绘制回调」）

> 原文写的是「每帧遍历活跃弹幕调绘制回调」，即**立即模式**。该模型已废弃，理由如下。

**为什么必须改**

- **兼容真实作品**：B 站旧版 Flash 播放器的 M8 引擎（反编译 `play_20120501.swf`，`org/tamaki/`）是**保留模式**——`CommentScript.exec()` 一次性 `vm.execute()` 跑完脚本，之后由 BetweenAS3 的 `EnterFrameTicker` 逐帧插值属性（`org/libspark/betweenas3/tickers/EnterFrameTicker.as:203`），`MotionManager` 的 ENTER_FRAME 只用于判定 `lifeTime` 到期。全树检索 `vm.execute()` 只有 `CommentScript.as:186` 一处调用点，**没有任何每帧重跑脚本的驱动源**。
- **实测数据**（av2669196 的 11 条 mode=8 真实脚本，`/root/m8-csharp/scripts-fixtures/`）：主脚本 354KB 含 `var shape =` 93 处、`beginFill/endFill` 123 处、`drawWedge/drawCircle/drawRect` 87 处、`createGlowFilter` 18 处、`createMatrix` 20 处——全是「建对象 → graphics 画一次 → 加滤镜/矩阵」；另一条 50KB 脚本含 `Display.Sprite` 15 处、`addChild` 19 处、`easeIn/easeOut` 22 处。**这些脚本里没有任何每帧重绘的代码**：把老脚本丢进立即模式宿主，画面只会在第一帧出现一次（甚至不出现），之后靠插值的部分全部丢失。
- **表现力**：graphics 路径绘制、滤镜链（glow）、3D、矩阵、字形排版（`fontData`）都是保留对象上的能力；立即模式要等价复刻得把整套图形 API 重做一遍。
- **性能**：保留模式下补间与滤镜交给合成层，同屏几十条无压力；立即模式每帧全屏 `clearRect` + 重跑脚本 + 发光滤镜，在这种体量下反而更重。

**保留模式的具体形态**

- **元素**：脚本执行期间创建 `M8Element` 式保留对象（text / shape / image / layer），进入宿主的元素树；属性含 `x/y/scaleX/scaleY/rotation/alpha/visible/filters/matrix`。
- **动画**：声明式 tween（`fromValue`/`toValue`/`lifeTime`/`startDelay`/`easing`/`repeat`），由统一 ticker 推进。逐帧回调只保留 `ctx.onFrame` 这一条逃生舱，且默认路径不得使用。
- **重绘策略**：元素分两类——**静态元素**（只创建、不动）首次绘制后缓存为位图或留在离屏层，后续帧不再重绘；**动画元素**（有活跃 tween 或被脚本改属性）标记脏，每帧只重绘脏元素。**擦除按「元素包围盒矩形」做，不是每帧整屏 `clearRect`**：元素移动 / 缩放 / 旋转 / 隐藏 / 释放时，把它**上一帧**在画布上的变换后外接矩形入队（`retireElementRect` → `pendingEraseRects`，外扩 `DIRTY_RECT_PADDING = 2` 像素），下一帧先以单位变换擦掉这些矩形、再合成本帧的脏元素。顺序不能反（反了会把刚画好的像素擦掉）；擦除是无差别矩形，可能盖住静止的邻居，所以擦完还要补画「被擦到但不是本帧脏元素」的邻居。整屏 `clearSurface()` **只出现在两条整幅画面作废的路径上**——`tick()` 的 `!visible` 隐藏分支与 `stopRunning()`；逐帧路径出现整屏清空即为回归（那正是立即模式的做法）。
  > **元素摘除的顺序陷阱**：`releaseItemElement` 必须**先** `detachElement(element)`（内部 `markElementMoved` 入队擦除）**再**标 `element.expired = true`。`markElementMoved` 见到 `expired` 为真会直接返回，顺序反了最后一帧的像素就入不了擦除队列，会在画布上永久残留（条目窗口结束时尤其明显）。契约测试 `Host_ReleasesElementCachesOnLifetimeExpiry` 固定了这个先后关系。
- **错误隔离**：脚本与缓动都是脚本作者提供的函数，抛错**不得**逃出 rAF 回调——否则 `running` 停在 `true` 而实际已无排队回调，帧循环永久冻结且 `ensureRunning()` 救不回来。因此 `frame()` 的调度链必须与「本帧是否出错」解耦：`try { tick } catch { reportCompositeError } finally { if (running) 续帧 }`。条目级失败（脚本编译/执行抛错）只停该条目；元素级失败（某个元素的缓动抛错）只标该元素 `failed` 并跳过，同帧其它元素与其它条目继续推进。
- **生命周期**：元素寿命 = **`min(该元素所有 tween 声明的最大 lifeTime（若有）, 条目窗口剩余时间)`**；没有任何 tween 声明 `lifeTime` 时，寿命就等于条目窗口剩余时间（**不是固定 3 秒**）。
  - 同一元素可能被声明多个 tween，寿命取声明值的最大值。声明值必须单独存在 `element.declaredLifeTimeMs` 上，**不能**与已经并入窗口约束的 `lifeTimeMs` 做 `max`——那样窗口值会永远胜出，脚本声明的寿命只能延长、永远无法缩短（`lifeTime: 2` 的元素会活到窗口结束）。
  - 到期由宿主摘除元素并释放离屏缓存，脚本不负责清理。寿命跟随播放器 play/pause 状态累计（暂停不计入）。
  - **seek 重建**：向后 seek 回窗口内时，已激活且在窗口内的条目只让 tween 重新插值（`element.motion.lastElapsedMs = -1`），不重跑脚本；元素已被整批释放的（拖过窗口再拖回来，或元素寿命先于窗口结束）则按条目进度**重建**元素——`rebuildItemElementsForSeek` 会再执行一次脚本，因为不重跑就无从知道脚本建了哪些元素、建在哪个坐标上。重建入口**只有** `resetItemsForSeek` 一处，绝不进入逐帧路径。
- **复合层重建条件**：有子节点的复合元素整棵子树烘到一张视口大小的离屏层（`rebuildComposite`）。父元素的 tween 只作用在这张已烘好的位图上，子树的绘制代码不再重跑。重烘条件是 `!element.painted || element.needsCache || element.compositeDirty || childrenChanged`；**重建后必须清掉 `needsCache` / `compositeDirty`**，否则它下一帧又被判为结构脏，每帧白烘一整张视口层（这是「同屏有动画元素时静止复合元素不重复重建」的落点）。
- **元素缓存失效规则**：叶子元素的内容画一次进离屏位图（`cacheCanvas`），单纯移动 / 缩放 / 旋转只改变换矩阵、**复用位图**。只有影响位图内容的属性（`fontsize` / 颜色 / 文本 / 图形路径…）才让缓存失效：`markPropertyDirty` 与 `markTweenKeyDirty` 都以 `TRANSFORM_ONLY_KEYS` 白名单判定——命中白名单（`x/y/z/alpha/scaleX/scaleY/rotation*/matrix/visible/filters`）只标脏，未命中则调 `invalidateElementCache`。把 `fontsize` 这类内容类属性加进白名单会让字号补间完全看不到效果（缓存尺寸不跟着变）。
- **绘制预算**：主 canvas 每帧仍有一次合成，但**成本是「脏元素数量」而不是「活跃脚本数 × 脚本体量」**；脚本体量只影响它执行那一次的开销。

**与立即模式并存？** 不并存。本分支的宿主统一按保留模式实现；若将来要支持「新式立即模式脚本」，应作为**另一种 `lang`/模式显式声明**（例如 `mode: "immediate"`），而不是让默认路径每帧重跑脚本。

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

- **`Modules/ScriptDanmakuParser.cs`**（纯逻辑，无 UWP 依赖）：`Parse(string)`（Json.NET 反序列化 + 校验）、`Normalize(IEnumerable)`（过滤 `stime<0`、NaN/Infinity、空 `code`；补 `id`/`duration`/`lang`，duration 缺省或非正数归一为 `UnboundedDurationSeconds`（0 = 不设时间窗），显式值钳制到 `[0, 600]`）、`NormalizeLang`（除明确 ts 外一律 js）、常量。
- **`Helper/ScriptDanmakuService.cs`**（UWP 侧，薄封装）：`LoadFromFileAsync(StorageFile)` 读文件后委托 Parser，空结果记 `LogHelper`；`GetBuiltInDemo()` 提供一条 JS 内置示例（文字横移 + 粒子环写在同一条脚本里）。

> **实施注记**：`Parse` 对 JSON 非法返回空列表，单条非法只丢弃该条——保证坏文件不会让整个功能失效，也不会静默产生半截集合。

### 9. csproj 注册

新增 `.cs` 用 `<Compile Include="..." />`，XAML 代码后置加 `<DependentUpon>`；`.xaml` 用 `<Page Include="...">`（含 `Generator` + `SubType`）；新 Assets（`script-danmaku-host.html`、`typescript.js`）用 `<Content Include="Assets\..." />`。

## 实施阶段

| 阶段 | 内容 | 验收 | 状态 |
|---|---|---|---|
| **0. Spike** | 验证两件事：① 官方 `typescript.js` 的 `transpileModule` 在 WebView2 里跑通；② Canvas/WebGL 在同屏几十条粒子/3D 下的帧率 | 可行性结论 | **未做**（TS 暂缓，见下） |
| **1. 核心渲染** | 模型 + 服务 + 宿主运行时 + 控件；加载本地 `.js` → 叠加显示、随播放同步 | 看到代码弹幕效果 | **代码完成（含 §3.1 保留模式改造），待页面级验证** |
| **2. 弹幕链路** | 读取弹幕数据 + 弹幕数据流拦截 | 脚本能读弹幕、能拦弹幕 | 未开始 |
| **3. 输入与操作** | 用户输入拦截 + 播放器操作拦截 | 脚本能消费点击、能阻止播放操作 | 未开始 |
| **4. 控制与发送** | 控制弹幕显示 + 发送弹幕 + 发送拦截 | 三项各验一次 | 未开始 |
| **5. 打磨** | 性能（保留模式脏重绘/图层缓存/活跃剔除/节流）、可见性跟随 `LoadDanmu`、错误提示、懒初始化 | 综合体验 | 未开始（懒初始化已提前落地；保留模式脏重绘与离屏缓存已随阶段 1 落地，见 §阶段 1 实施记录 8） |

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
3. **`ctx` 字段与设计稿的差别**：实际提供 `{ width, height, dpr, duration, stime, id, time, state, t, progress, pause, seek, navigate }` 与保留模式元素 API `{ createText, createShape, createImage, createLayer, addChild, removeChild, tween, onFrame }`。保留模式下 `t` / `progress` 恒为 0（脚本每条只执行一次），只作兼容读取；`ctx.g` 已随立即模式一并取消。`danmaku` / `intercept` / `filter` / `send` / `on` 属阶段 2-4，未实现。
4. **画布按 `devicePixelRatio` 缩放**，脚本拿到的 `width`/`height` 仍是 CSS 像素，脚本作者无需处理 DPI。
5. **懒初始化闸门提前落地**：`pendingItemCount == 0 && !isPageReady && initializationTask == null` 时直接返回，不创建 WebView2。
6. **新增「加载示例代码弹幕」菜单项**（设计稿只有加载/清除两项），便于不准备文件就验证渲染链路。
7. **不做时间窗**：按设计稿，全集 `ReplaceAsync` 交给宿主自调度，未套用 BAS 的 lookback/lookahead 窗口。
8. **渲染模型改为保留模式（已落地）**：阶段 1 最初的宿主是「每帧重跑脚本绘制回调」的立即模式，现已按 §3.1 改为保留模式。落地要点：脚本每条只在进入时间窗那一刻执行一次（`activateItem` 是唯一执行点，整份宿主只有一处 `new Function`）；`ctx` 改为元素工厂 + `tween` 声明，`ctx.g` 与逐帧 `t`/`progress` 语义取消，只留 `ctx.onFrame` 逃生舱；逐帧流程改为「推进 tween → 标脏 → 只重绘脏元素 → 按 dpr 合成」，静态元素首帧后缓存为位图不再重绘；`visible=false` 时合成步骤直接跳过并保持画布空白；`lifeTime` 到期由宿主摘除元素并释放离屏缓存；seek 按新位置重算插值而不重跑脚本。内置示例脚本已重写为「建元素 + tween」形态（`demo-m8-sample`：文字走声明式 tween、粒子环走 `ctx.onFrame` 逃生舱，两种写法写在同一条脚本里），`ScriptDanmakuHostContractTests` 中针对「每帧调绘制回调」的断言已同步改写为保留模式断言。
   > 已知取舍与未做项：glow 滤镜是近似实现（缓存构建时 `blur(4px)` + `lighter` 叠加，非 Flash GlowFilter 的忠实移植）；`tick()` 的 `anyActive` 仍按整个条目列表推导，视频播放期间即使无活跃 tween 也会空转（优先级低，见 §阶段 1 暴露的待办）；`drawGraphicsData` / `drawPath` 显式抛错未支持。
9. **保留模式收尾：脏矩形擦除与寿命语义修正（已落地）**。保留模式首版落地后暴露出六个语义缺陷（D1~D6），已逐条修掉，并新增宿主行为测试套件把它们钉住（见 §验证）：
   - **D1 拖影**：首版只在隐藏 / 停止时整屏 `clearRect`，移动元素在整条路径上留拖影。改为**按元素包围盒擦除**（`computeElementCanvasRect` 取变换后外接矩形、外扩 2px，入队 `pendingEraseRects`，下一帧先擦后合成），整屏 `clearSurface()` 收敛到隐藏 / 停止两条路径。
   - **D2 帧链冻结**：缓动抛错会从 rAF 回调逃逸，`running` 停在 `true` 却已无排队回调。改为 `try/catch/finally`，续帧调度放进 `finally` 并以 `running` 为条件；元素级失败只标该元素。
   - **D3 寿命**：首版缺省硬编码 3 秒、且 `Math.max` 让窗口值永远胜出，`lifeTime: 4` 的滚动文字在屏幕中间就被摘掉、`lifeTime: 2` 又永远无法缩短。改为 `min(声明值, 条目窗口剩余时间)`，声明值与窗口值分开存（`declaredLifeTimeMs` / `lifeTimeMs`）。`ScriptDanmakuService` 的示例注释同步改正。
   - **D4 复合层**：静止复合元素每帧被重烘一整张视口大小的层（60 帧 60 次）。改为只在 `structural || compositeDirty || childrenChanged` 时重烘，并在重建后清掉 `needsCache` / `compositeDirty`。
   - **D5 缓存失效**：`fontsize` 等**内容类**属性被当成变换类复用缓存，字号补间完全不可见。改为按 `TRANSFORM_ONLY_KEYS` 白名单分流，未命中即 `invalidateElementCache`。
   - **D6 seek 与摘除顺序**：元素被整批释放后向后 seek 回窗口内不重建（画面空白），以及 `releaseItemElement` 先标 `expired` 导致最后一帧像素入不了擦除队列（永久残影）。改为 `rebuildItemElementsForSeek` 按进度重建（脚本仍只多跑一次、不逐帧重跑），并把摘除顺序固定为「先 `detachElement` 再标 `expired`」。
10. **内置示例复刻原版 M8 观感（已落地）**。保留模式重写时示例的观感与原版 M8 示例走样，现已改回：文字恢复**从右侧屏幕外滑入、向左移出**（`fromValue: ctx.width + 120` → `toValue: -120`，端点与原版 `(1 - progress) * (ctx.width + 240) - 120` 一致），走声明式 `tween`；粒子改回 **24 个点**、点半径恒定 **6px**，环半径 40 → 200 的同时整环旋转、整体淡出。这里的运动是「半径与角度同时随时间变化」的极坐标路径，用 `scale` 表达扩散会把点一起放大（原版点大小恒定），因此这一条**故意走 `ctx.onFrame` 逃生舱**逐帧只改位置——脚本仍只执行一次、元素树与位图缓存都不重建，正好演示逃生舱的适用边界（`p >= 1` 时把点 `visible=false`，让它们被摘除并擦净）。行为套件 `D7` 的示例副本与断言已同步更新（文字必须用 `tween`，粒子必须用 `onFrame` 且代码里不得出现 `scaleX`）。
11. **内置示例合并为单条脚本（已落地）**。示例最初拆成两条条目（`demo-scroll-text` stime=1/duration=4、`demo-particles` stime=3/duration=5），与原版 M8 示例「一条脚本画完整个效果」的形态不一致。现合并为一条 `demo-m8-sample`（stime=1、duration=7，窗口 1~8s）：文字 `tween` 与粒子 `onFrame` 写在同一条脚本里——tween 挂在元素上、onFrame 挂在条目上，`advanceItem` 两者都会跑，互不冲突。两个落地要点：其一，窗口取原两条窗口的并集（1~8s），文字的 `lifeTime: 4` 仍经 `min(声明值, 窗口剩余)` 收紧到 1~5s，粒子无声明则吃满 1~8s（`p >= 1` 后不可见、窗口结束摘除）；其二，`ctx.onFrame` 回调只给 `elapsedMs`、**没有 delay 参数**（只有 tween 的轨道有 `delay`），粒子要晚 2 秒出现只能自行扣偏移（`var local = elapsedMs - 2000; if (local < 0) { return; }`），并在建点时就置 `visible = false`，避免出现前在原点闪一帧。逐帧核对（无头桩，0.5s 起按 60fps 步进）确认合并前后同一时刻的文字 x、粒子半径/透明度/可见性、以及收尾时刻（文字 5s 摘除、粒子 6s 隐去、8s 窗口结束画布无残留）完全一致；`D7` 用例已改为单条形态，并新增「必须用 `elapsedMs - 2000` 做起始偏移」的断言。

12. **向 M8 靠拢：播放器状态可见 + 时间窗降级为兜底（已落地）**。原版 M8 没有条目窗口，元素寿命由脚本的 `lifeTime` 决定（实测真实脚本里写着 `$.createCanvas({ lifeTime: 810114514 })`，≈9.4 天，等于常驻），因为 Flash 播放器本身就是帧循环主人。本项按同样思路放开两处：其一，`duration` 缺省或非正数改为**不设时间窗**（`ScriptDanmakuParser.UnboundedDurationSeconds = 0`），宿主只保留防呆上限 `MAX_ITEM_WINDOW_MS = 600000`，元素寿命因此完全由脚本的 `lifeTime` 决定；其二，`lifeTime: 0` / 负数按 M8 语义改为**常驻**（声明值取 `Infinity`，实际仍受兜底上限约束），此前宿主把 `lifeTime <= 0` 夹成 `0.001` 秒、元素瞬间消失，与 M8 的 `lifeTime:0`（Galgame 示例里大量使用）正好相反。同时把脚本侧的播放器状态补齐：`ctx.time`（播放头位置，**毫秒**，与 M8 的 `Player.time` 同单位）与 `ctx.state`（`playing`/`pause`/`stop`），等价于 M8 的 `Player.time` / `Player.state`，逐帧读法是在 `ctx.onFrame` 回调里读 `frameCtx.time`——值来自宿主自己的外推时钟，不增加消息往返。连带修正一处判据：`frame()` 的自停条件原为「窗口内有没有条目」，无界窗口下条目会长时间停在窗口内、暂停后帧循环一直空转，现改为「不在播放且没有待推进的补间/逐帧回调」。行为套件新增 `D9`（无界窗口兜底、`lifeTime: 0` 常驻、`ctx.time`/`ctx.state` 逐帧可读）与 `D10`（暂停自停、恢复后重新拉起）。

测试：`tests/BiliBili.Tests/` 下三个文件——`ScriptDanmakuParserTests.cs`（解析/校验契约）、`ScriptDanmakuHostContractTests.cs`（宿主↔控件字符串契约：命令名、消息类型、`ctx` 字段、dpr 缩放、可见性、自停位置、单脚本失败隔离、脏矩形擦除、缓存失效白名单、寿命 min 规则与摘除顺序、seek 重建入口唯一、不引入 BAS 资产、不为每条弹幕建 DOM）、`ScriptDanmakuPlayerPageContractTests.cs`（PlayerPage 接入完整性：倍速重推、可见性重推、PositionChanged 两条分发路径、清理点对称、层叠顺序、菜单处理器、跳转白名单）。

**宿主行为测试（`tests/host/retained-mode.test.js`）**：源码契约测试只能证明「某段代码还在」，证明不了「行为对不对」。宿主的渲染正确性用这个纯 node、**零依赖**（不需要 `npm install`）的套件补：它用 `node:vm` 把宿主 HTML 里的内联 `<script>` 加载进沙箱，桩掉 `document` / `canvas.getContext("2d")` / `requestAnimationFrame` / `performance.now`，按帧驱动并检查真实的画布操作序列。覆盖 D1~D10：

| 用例 | 语义 | 修复前的表现 |
|---|---|---|
| D1 | 移动元素跑 60 帧后不残留旧位置像素（无拖影） | 残留起点像素，`union.x` 停在 0 附近 |
| D2 | 自定义缓动抛错后帧循环存活、其他条目继续渲染、命令仍有效 | 异常逃出 rAF 回调，帧链冻死 |
| D3 | 元素寿命 = min(声明的 lifeTime, 条目窗口剩余时间) | 一律 3000ms，声明只能延长不能缩短 |
| D4 | 同屏有动画元素时，静止复合元素不重复重建整层 | 每帧重烘一次（60 帧 60 次） |
| D5 | `fontsize` 补间后缓存尺寸随之变化，静止元素不被过度失效 | 缓存尺寸不变，字号补间不可见 |
| D6 | 向后 seek 回窗口内：元素被重建、位置是插值结果、脚本不逐帧重跑 | 摘除后残影不擦、重建位置不对 |
| D7 | 内置示例（单条）声明式 tween 与 onFrame 逃生舱并存，能渲染出画面且到点自然收尾 | 示例写的是立即模式，脚本跑完没有动画 |
| D8 | reset 整批作废时必须清画布，换一批弹幕不留旧像素 | 旧像素永久残留（换成新一批后仍在） |
| D9 | 无界窗口按兜底上限兜住、`lifeTime: 0` 常驻、`ctx.time` / `ctx.state` 逐帧可读 | 缺省窗口按 3 秒截断，`lifeTime: 0` 元素瞬间消失 |
| D10 | 暂停且没有待推进的补间时帧循环自停，恢复播放后重新拉起 | 无界窗口下暂停后 60fps 空转 |

运行方式（默认宿主为仓库内 `BiliBili.UWP/Assets/script-danmaku-host.html`，可用参数或环境变量改指）：

```
node tests/host/retained-mode.test.js                       # 跑仓库内宿主
node tests/host/retained-mode.test.js /tmp/prefix-host.html  # 指定宿主文件（对比跑）
SCRIPT_DANMAKU_HOST=/tmp/prefix-host.html node tests/host/retained-mode.test.js
```

> 该套件对**修复前**的宿主必须失败（D1/D2/D3/D5/D6 至少各挂一条），对修复后全绿——这是它的验收硬指标，也是它区别于源码契约测试的地方。取修复前宿主的只读命令：`git show <修复前提交>:BiliBili.UWP/Assets/script-danmaku-host.html > /tmp/prefix-host.html`。
> **桩的四个坑**（改这个套件前务必知道）：① 桩必须实现 canvas 变换（`translate`/`scale`/`setTransform` + `save`/`restore` 栈）并在 `drawImage` 里套用当前矩阵，否则 translate 驱动的移动根本不可见，D1 会**假通过**；② 每个用例都必须重新加载一份干净宿主（新 vm 上下文），且**不能手工清空 rAF 队列**（宿主自己用 `running` 标志管理队列，手工清空会改变被测语义）；③ 读元素内部字段前先用 `Object.keys(element)` 确认字段存在，否则宿主改字段名后测试会静默取到 `undefined` 而"通过"；④ 宿主→主进程的 `postMessage` 载荷是 JSON 字符串，断言前要 `JSON.parse`。

### 阶段 1 代码审查修正（已落地）

首轮实现后做了一次独立审查，发现并修掉三个缺陷：

1. **倍速变更后脚本时间轴永久漂移**（中高）。`slider_Rate_ValueChanged` 只调了 `SyncBasDanmakuPlaybackState()`，漏了脚本侧。宿主 `currentPositionMs()` 按 `state.rate` 外推，rate 不更新则宿主时钟与视频每秒累积偏差；而漂移判据 `expected = lastPos + elapsed * GetScriptDanmakuPlaybackRate()` 用的是媒体**新**倍速、`lastScriptDanmakuPosition` 又每次 `PositionChanged` 刷新，所以 `shouldSeek` 恒为 false，**不存在自愈路径**，只有暂停/seek/换集才会重推。修法：在该处并列 `SyncScriptDanmakuPlaybackState();`。
2. **关闭弹幕开关后脚本弹幕会重新出现**（中）。`MTC_OpenDanmaku(false)` 先发 `SetVisibleAsync(false)`（宿主 `stopRunning()` + 清屏），随后 `SyncScriptDanmakuPlaybackState()` 发 `setState(pos, false, rate)`，而后者的非播放分支无条件 `drawFrame()`——当时 `drawFrame` 不读 `visible`，于是把活跃脚本重画回屏幕，且 `running` 已为 false、没有后续帧清理。窗口缩放走 `resize()` 同理。修法：`drawFrame` 开头清屏后若 `!visible` 直接返回 false。
   > **保留模式下该修法已按此重做**：合成步骤（`tick()` 的 `!visible` 分支）先清屏再直接返回，绝不把脏元素合成回去；判据从「`drawFrame` 返回值」改为「合成步骤是否产出任何元素」。契约测试 `Host_SkipsCompositingWhenHiddenAndKeepsCanvasBlank` 固定了「隐藏分支早于合成且内部无合成调用」。
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
  3.5. **保留模式验收**：脚本每条只执行一次——用 `console.count`/埋点确认「同一条弹幕在播放 10 秒里只执行 1 次」；静态元素（只创建不动的）在首帧之后不再重绘（可用绘制计数或 DevTools 性能面板确认）；暂停时画面停在当前插值位置；seek 后按新位置重算插值而不是重跑脚本。
  4. 四类拦截各验一次：点击被消费、播放操作被阻止、弹幕被拦下、发送被拦下。
  5. 三类交互各验一次：读到弹幕数据、屏蔽一条、发送一条。
  6. 弹幕总开关关闭 → 脚本弹幕隐藏；未加载时不初始化 WebView2。
  7. **未注册任何拦截器时，播放/弹幕/输入路径无可感知开销**（性能回归点）。
- **接口可用性前置**：阶段 4 开工前，先按 `Controls/SendDanmakuDialog.xaml.cs:57` 的参数与签名形态实测 `x/v2/dm/post`，确认可用后再决定抽取方式（见 §6③）。不要先按 `PlayerAPI.SendDanmu` 实现。
- **回归**：BAS 弹幕（mode9）行为不变。
- **测试**：`tests/BiliBili.Tests`（net8.0 + MSTest）。阶段 1 已补 `ScriptDanmakuParserTests`（18 例）、`ScriptDanmakuHostContractTests`（37 例，含保留模式改造后的断言）、`ScriptDanmakuPlayerPageContractTests`（8 例）；另有宿主行为测试 `tests/host/retained-mode.test.js`（纯 node、零依赖，7 例 D1~D7，见上）。**CI 已接入**：`.github/workflows/ci.yml` 的 `test` job 在 `dotnet test` 之前跑 `node tests/host/retained-mode.test.js`（运行器自带 node，无需 `setup-node`）。覆盖不到的部分——实际渲染、时间同步、性能——必须走页面级验证。
- **日志**：`LogHelper` 无脚本弹幕渲染失败。

## 风险

- **性能（最大）**：几十条粒子/3D + 高频拦截 RPC。对策：**保留模式（脚本只执行一次，逐帧只插值属性与重绘脏元素，见 §3.1）**、静态元素离屏缓存、剔除不活跃、批量+节流+无注册零开销、限制同时活跃的 WebGL 层数。**每帧重跑脚本绘制是明确禁止项**——它会让「同屏几十条 + 单条 350KB 脚本」的组合同屏不可用。
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
- **播放中不停帧（已知，未修）**：自停判据已从「窗口内有没有条目」改为「不在播放且没有待推进的补间/逐帧回调」（见实施记录第 12 条），**暂停后的空转已消除**；但视频在播时即使用户只加载了一条早已播完的脚本，仍会以 60fps 出帧——播放本身需要出帧，进一步优化（跳过合成、降频）留待阶段 5。
  > 保留模式下每帧成本已从「清屏 + 重绘全部」降到「只重绘脏元素」，空转帧的实际开销大幅下降，因此该项优先级从「中」降为「低」。
- **脚本无法感知「帧间隔」**（保留模式下不再成立）：原 `ctx.t` 是相对时间，有状态的脚本（粒子等）需自行记录上一帧的 `ctx.t` 求差，示例脚本只能用纯函数形式规避。保留模式下脚本只在创建时执行一次、动画交给 tween 引擎插值，因此**不存在帧间隔感知问题**；代价是「每帧自定义物理演算」这类效果改用 tween 之外的显式机制表达（见 §3.1 末尾关于「不并存」的说明）。
