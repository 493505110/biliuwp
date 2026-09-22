# 脚本弹幕平台（mode8 风格）

> **状态：阶段 1（核心渲染）已完成代码与构建验证，待页面级验证；阶段 2-5 未开始。** 见 §实施阶段。
> **本版修订（渲染模型）**：原设计「每帧遍历活跃弹幕调绘制回调」的**立即模式已废弃**，改为**保留模式**——脚本每条只执行一次、逐帧只推进 tween 与重绘脏元素。理由、真实脚本数据与具体形态见 **§3.1**；代码已按此改造并补齐脏矩形擦除 / 寿命语义等收尾（D1~D6），实施记录见 §阶段 1 实施记录 8、9。
> **本版修订（脚本 API 面）**：**自研的 `ctx.*` 脚本 API 已废弃**，脚本环境直接提供原版 M8 的全局名（`$` / `Player` / `$G` / `Global` / `Tween` / `Utils` / `ScriptManager` / `timer` / `interval` / …），目标是让当年的真实 M8 脚本尽量原样跑起来。API 清单（等价实现 / 占位待接 / 不支持）见 **§3**，决定与理由见 §阶段 1 实施记录 13。
> **本轮补做**：实施记录 13 里列为占位的 `Player.play()` / `commentList` / `commentTrigger` / `keyTrigger` / `setMask` 已真实现（含桥协议新增的 `play` 动作与 6 条 C#→宿主命令），仅 `createSound` / `External.*` / `load()` 仍是占位——见 §阶段 1 实施记录 14。
> **本轮补做（Flash DisplayObject）**：`element.transform`（含真实现的 `Matrix3D`/`Vector3D`）、显示列表查询、`blendMode`、**元素级 `mask`** 四类已补齐，另修掉 5 处「不补就出不了画面」的兼容语义（元素属性不可枚举、`foreach` 不遍历原始值、`popEl` 的真正语义、`Event.ENTER_FRAME`、AVM1 读未声明变量返回 undefined）。**av2669196 的 11 条真实脚本现在能在同一宿主实例里 0 报错跑完并真的出画面**——见 §阶段 1 实施记录 15。
> **阅读提示**：§阶段 1 实施记录 3、8、10、11、12 与 §阶段 1 代码审查修正写于这次改动**之前**，其中出现的 `ctx.*` / `ctx.onFrame` / `ctx.time` 是**当时**的 API 面，现已被本版取代（结论与取舍仍然成立，只是名字换了：`ctx.createText`→`$.createComment`/`$.createText`、`ctx.createShape`→`$.createShape`、`ctx.createImage`→`$.createImage`、`ctx.createLayer`→`$.createLayer`、`ctx.tween`→声明式 `opts.motion`、`ctx.onFrame`→M8 的 `interval`、`ctx.time`/`ctx.state`→`Player.time`/`Player.state`、`ctx.pause`/`ctx.seek`/`ctx.navigate`→`Player.pause`/`Player.seek`/`Player.jump`、`ctx.width`/`ctx.height`→`Player.width`/`Player.height`）。
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
- 流程：`lang=="ts"` 的脚本 → 宿主 `ts.transpileModule(code, { compilerOptions: { target: ES2020, module: None } })` → JS → `new Function(<M8 全局名…>, js)`（注入名清单见 §3）。`lang=="js"` 跳过转译。
- 转译结果按脚本 id 缓存，只转一次。
- **不做类型检查**（只转译），避免拖慢。
- csproj `<Content Include="Assets\typescript.js" />`。**代价：包体积 +几 MB**，这是「应用内直接写 TS」的必付成本。

### 3. 宿主运行时（`Assets/script-danmaku-host.html`）

> 单文件内联，不拆 `.js`——虚拟主机映射下同目录引用没有额外收益，且少一处 csproj 注册。

- **渲染循环（保留模式）**：主 `<canvas>` + `requestAnimationFrame`。脚本**每条只执行一次**，执行期间通过 M8 的元件工厂 `$` 建出保留对象树（`$.createComment` / `$.createShape` / `$.createCanvas` / …），并把动画写成声明式 `motion` 或 `Tween` / `interval` 声明；之后每帧只做三件事——推进补间、更新元素属性、**仅重绘被标记为脏的元素**。**禁止每帧重跑脚本**（理由与真实数据见 §3.1）。**不用 DOM-per-danmaku**。不在播放且没有待推进的补间/定时器时自动停循环（恢复路径天然存在：播放态变化、seek、resize 都会走 `ensureRunning`）。
- **脚本 API：直接暴露原版 M8 的 API 面，不再有自研的 `ctx`**（本版决策，见 §阶段 1 实施记录 13）。宿主用 `new Function("$", "Player", "$G", "Global", "Tween", "Utils", "ScriptManager", "timer", "interval", "clearTimer", "trace", "tracex", "stopExecution", "foreach", "clone", "getTimer", code)` 把这些全局名作为**参数**注入脚本作用域，脚本正文因而可以原样书写 `$.createComment(...)` / `Player.time` / `Tween.tween(...)`，不必改写成 `ctx.xxx`；用参数注入而不是给 `window` 挂属性，是为了让脚本对这些名字的赋值只影响自己那一次执行。
  > **真实脚本的可移植性**：这一改动来自对 av2669196 的 11 条 mode=8 真实脚本文本（`/root/m8-csharp/scripts-fixtures/`）的统计——`$.createShape` 93 处、`beginFill/endFill` 123 处、`createGlowFilter` 18 处、`createMatrix` 20 处、`Global` 90 次、`Utils.rgb` 61 次、`ScriptManager.popEl` 4 次、`stopExecution` 1 次。这些脚本没有一行 `ctx.xxx`，自研 API 面等于要求每个作者重写脚本。

**脚本 API 清单（按「等价实现 / 占位待接数据链 / 不支持」三类）**

| 类别 | API | 状态与说明 |
|---|---|---|
| **等价实现** | `$.createComment(text, opts)` / `$.createText` | M8 的文本元件。默认白字 / 黑体 / 25px（与 M8 一致）。`opts` 支持 `x/y/z/alpha/scaleX/scaleY/rotation/rotationX/rotationY/visible/lifeTime/parent/motion/font/fontsize/color/bold/border/borderColor/filters` |
| | `$.createShape(opts)` / `$.createCanvas(opts)` / `$.createSprite(opts)` | 可承载子元件的保留元件（`canvas`/`sprite` 与 `shape` 同义）。`el.graphics.*` 为现有绘图子集（见下） |
| | `$.createButton(opts)` | 近似实现：底色矩形 + 居中文本 + `onclick` 登记。**`onclick` 当前不会触发**——控件 `IsHitTestVisible=False`、主画布 `pointerEvents:none`，没有输入通道（阶段 3 接输入拦截后才会响） |
| | `$.createImage(url, opts)` | 宿主扩展（M8 用 `External.Bitmap.createBitmap`）。直连 URL / `data:` URI |
| | `$.toIntVector(list)` / `$.toNumberVector(list)` | 返回普通数值数组（本宿主的 Vector 就是 Array），供字体数据类脚本原样跑 |
| | `el.remove()` | 从渲染中摘除并退出元素登记表 |
| | `el.setStyle(name, value)` | 文本类属性（`color`/`fontsize`/`font`/`bold`/`border`/`borderColor`）落到文本样式；其余按元素属性赋值 |
| | 可写属性 | `x/y/z/alpha/scaleX/scaleY/rotation`（=`rotationZ`）/`rotationX`/`rotationY`/`visible`/`matrix`/`filters`/`text`/`url`/`fontsize`/`font`/`color`/`bold`/`mask`。直接赋值即标脏；内容类属性（`text`/`fontsize`/`color`/`filters`…）还会让位图缓存失效。**`rotationX`/`rotationY` 是 3D 属性，2D 画布只存储、不呈现**。两个 `mask` 都是已实现的：**元素级 `el.mask`** 作用于被遮罩元素的子树（见下），**播放器级 `Player.setMask`** 作用于整块画布 |
| | `el.parent` / `el.children` | 可写：赋值 `parent` 即换父节点；`children` 是子元件数组 |
| | 文本 `el.length` | 字符数（M8 的 `CommentField.length`） |
| | `el.graphics.*` | `beginFill` / `endFill` / `beginGradientFill` / `lineStyle` / `lineGradientStyle` / `moveTo` / `lineTo` / `curveTo` / `drawRect` / `drawRoundRect` / `drawCircle` / `drawEllipse` / `drawWedge` / `drawPolygon` / `drawPath` / `clear`。渐变按 `$.createMatrix().createGradientBox(w,h,rotation,tx,ty)` 给的渐变框换算成 canvas 的线性/径向渐变；`drawPath` 复用 moveTo/lineTo/curveTo 的路径模型（命令码 1/2/3/4/5 完整，6 取第一个控制点近似）。**`drawGraphicsData` 仍显式抛「尚未支持」**（需要完整 IGraphicsData 对象模型，真实脚本 0 次使用） |
| | `el.transform` | Flash 的 `DisplayObject.transform`。`.matrix` **与元素已有的 `props.matrix` 是同一份对象**（脚本的 `mx = el.transform.matrix; mx.identity()` 原地改法必须作用在同一份数据上），带 `identity/translate/scale/rotate/concat/invert/clone/transformPoint`；`.matrix3D` / `.colorTransform` 可读可写但只存储（2D 画布不呈现）；`.perspectiveProjection` 返回 Flash 默认值的纯数据对象；`.getRelativeMatrix3D(target)` 返回累计 2D 变换铺成的 Matrix3D |
| | `$.createMatrix3D` / `$.createVector3D` | **真算**：`append`/`appendRotation`/`appendTranslation`/`appendScale`/`prepend*`/`transformVector`/`transformVectors`（原地填充目标数组）/`deltaTransformVector`/`invert`/`clone`/`position`，rawData 用 Flash 的列主序布局。Akari 靠 `transformVectors` 把子元件局部坐标投到世界坐标、按 z 排序来定绘制顺序——算错画面顺序就错 |
| | 显示列表查询 | `numChildren`（own property！脚本用 `hasOwnProperty("numChildren")` 判断「是不是显示对象」）/ `getChildAt` / `getChildIndex` / `setChildIndex` / `getChildByName` / `contains` / `addChild` / `addChildAt` / `removeChild` / `removeChildAt` / `removeAllChildren` / `swapChildren`。`getChildAt` 越界返回 `null`（Flash 抛 RangeError，这里选择不炸整条脚本） |
| | `el.blendMode` | 映射到 canvas 的 `globalCompositeOperation`：`normal`/`layer`/`alpha`→`source-over`、`add`→`lighter`、`multiply`→`multiply`、`screen`→`screen`、`overlay`→`overlay`、`difference`/`subtract`→`difference`、`lighten`/`darken`、`hardlight`→`hard-light`、`colordodge`/`colorburn`、`exclusion`、`hue`/`saturation`/`color`/`luminosity`。**未知值一律退回 `normal`，不抛错**（脚本里的取值集合远大于 canvas 能表达的）。脚本读回的 `blendMode` 是它写进去的原值 |
| | `el.mask`（元素级） | Flash 语义 `被遮罩元素.mask = 遮罩元素`：**只裁被遮罩元素自己（含子树）**，不串到整块画布（与 `Player.setMask` 的区别）。遮罩元件自己不参与合成。变换用「烘进路径坐标」的方式施加——不能用 canvas 的 save/restore 压遮罩元件的变换，因为 restore 会把刚建立的裁剪一起去掉 |
| | `el.addEventListener` / `removeEventListener` / `hasEventListener` / `dispatchEvent` | Flash 的 `EventDispatcher`。真正派发的是 **`"enterFrame"`**（Akari 的 `Composition.present()` 就是 `canvas.addEventListener("enterFrame", frameFn)`，整幅画面的每帧更新挂在这上面）；其余事件类型登记了不派发、也不抛错。监听表随条目回收一起清掉 |
| | `ScriptManager.popEl(el)` | M8 语义是「把元件从**自动清理表**里弹出」（让 `clearEl()` 不删它），**不是从显示列表摘除**。Akari 把整幅作品挂在 `popEl` 过的常驻 root 下——实现成 `remove()` 会让整棵树脱离渲染，实测一个像素都画不出来 |
| | 元素属性**不可枚举** | 元素的全部内部字段与访问器都定义成 non-enumerable：Flash 里显示对象的属性挂在原型上，脚本 `foreach(obj, fn)` / `for-in` 遍历显示对象时拿不到任何一项。Akari 的 `Factory.clone` 正是靠 `countProperties === 0` 分叉走「新建 `$.createCanvas` 再逐个拷贝显示属性」那条路；可枚举会让它顺着（成环的）对象图无限递归（`Maximum call stack size exceeded`）。`hasOwnProperty` 不受影响 |
| | `foreach` 只遍历真对象 | 原始值（string / number / boolean）一律不遍历。这不是洁癖：上面那条 clone 递归到字符串时，若去枚举 `"a"` 的字符下标就会 `clone("a")` → `clone("a")` 无限递归。M8 文档把参数标成 `Object`，Flash 的 for-in 对原始值也不产生可枚举属性 |
| | AVM1 宽容语义 | 读**未声明变量**得到 `undefined` 而不抛 `ReferenceError`。真实样本里 `update:function(time){if(time < startTime)...}` 的 `startTime`/`duration` 就是 AS→JS 翻译时丢掉的 `var`——AVM1 下它们读成 undefined（比较恒 false、正文照跑），JS 下直接停摆。实现方式：捕获 `ReferenceError` 认出名字 → 把它**声明到脚本全局对象上**（值 `undefined`）→ 清理并重跑本条脚本（回调里抛的同类错误同样处理，因为有问题的闭包可能来自**别的**条目：entry_10 抛错的那处 `update` 定义在 entry_08 的 Akari 库里，作用域链早已定死，补形参救不了，只有补全局对象才能让已建好的闭包也恢复） |
| | `Player.time` | **实时 getter**，读宿主外推时钟（毫秒）。不是激活时快照——M8 脚本在 `interval` 回调里读它 |
| | `Player.state` | 实时 getter：`playing` / `pause` / `stop`（弹幕层隐藏时给 `stop`） |
| | `Player.width` / `height` | 宿主视口（CSS 像素） |
| | `Player.play()` / `pause()` / `seek(ms)` / `jump(av, page, newwindow)` | 四个动作都走宿主→C# 的 `action` 通道（`play` / `pause` / `seek` / `navigate`，本轮补上 `play`）；`jump` 拼成 `https://www.bilibili.com/video/av{n}/?p={page}`，由 PlayerPage 侧的白名单校验。`newwindow` 忽略（宿主无法开新窗口）。`seek` 的负数入参钳到 0（与 `PlayerPage.SeekFromScriptDanmaku` 的 `Math.Max(0, …)` 一致） |
| | `Player.commentList` | **推入的弹幕快照**，字段与 M8 的 `CommentData` 同名同义：`txt` / `time`（**秒**）/ `color` / `pool` / `mode` / `fontSize`，缺省补齐。由 PlayerPage 在弹幕池落定时推入（`resetComments` + `appendComments`），控件保留快照并在脚本加载后的 `reset` 之后自动补投 |
| | `Player.commentTrigger(f, timeout)` | **监听用户发送弹幕**：PlayerPage 在发送成功回调里经 `pushComment` 推入，宿主投递给在窗口内条目的回调（一条 CommentData 形状的对象）。返回 M8 文档说的数字 id |
| | `Player.keyTrigger(f, timeout, up)` | **监听键盘输入**：PlayerPage 把 `CoreWindow` 的 KeyDown / KeyUp 经 `pushKey` 推入。只投递 M8 文档列出的那组键（小键盘 0-9、方向键、Home/End/PgUp/PgDn、W/S/A/D）——这组键与 Windows `VirtualKey` / DOM `keyCode` 同值，因此 C# 侧整数值透传、宿主侧筛选。`up=true` 只收 keyUp |
| | `Player.setMask(obj)` | **合成期裁剪**：整块脚本弹幕画布裁剪到 mask 元件的形状里（形状元件按它的图元描路径；文本/图片/复合元件退化成外接矩形）。遮罩元件本身不参与渲染（M8 的遮罩对象不在显示列表里），只从渲染树摘除、仍随条目回收。裁剪是合成期行为，**不会让任何元素重建位图缓存** |
| | `Player.refreshRate` | 可读写，钳制到 `[10, 500]`，默认 170（M8 文档）。当前只作兼容存储，不改变本宿主的 rAF 帧率 |
| | `$G._set/_get/_remove` 与 `$G._`、`Global._set/_get/_remove` | 跨条目共享变量；`reset` 时换成新对象（上一代脚本的写入不污染新一代）。`Global` 与 `$G` 是同一个对象（M8 文档里的两个名字，真实脚本两种都在用） |
| | `Tween.tween/to/delay/scale/reverse/repeat/slice/serial/parallel` | 段列表模型上的时间轴运算，返回 ITween 句柄 |
| | ITween：`play` / `stop` / `gotoAndPlay` / `gotoAndStop` / `togglePause` / `stopOnComplete` | 句柄有自己的播放头（`play()` 起算）。组合子（`serial`/`parallel`/`delay`/…）只做时间轴运算，`play()` 时统一装到来源句柄的元件上，来源句柄随即停用 |
| | `Utils.hue/rgb/formatTimes/delay/interval/distance/rand` | `hue(0)=0x0000FF`、`hue(120)=0xFF0000`、`hue(240)=0x00FF00`（按 M8 文档）；`formatTimes` 输出 `m:ss`；`delay`/`interval` 是全局 `timer`/`interval` 的别名 |
| | `timer(fn, delay)` / `interval(fn, delay, times)` | 登记在**条目**上的定时器（见下「定时器生命周期」）。`times=0` 为无限次 |
| | `clearTimer(x)` / `clearInterval` 语义 | 接受 ITimer 句柄或数字 id；无参时清当前条目的全部定时器 |
| | `trace(...)` / `tracex(s)` | 回显到宿主控制台，不抛错、不新增桥消息 |
| | `foreach(obj, f)` / `clone(obj)` / `getTimer()` | `foreach` 回调签名 `(key, value)`；`clone` 浅拷贝且**不复制函数**（M8 文档明说）；`getTimer` 返回宿主启动至今的毫秒数 |
| | `ScriptManager.clearTimer()` / `clearEl()` / `clearTrigger()` / `popEl(el)` | `clearTimer` 清当前条目的定时器；`clearEl` 摘除当前条目的元件；`clearTrigger` 是空实现（触发器登记表为空）；`popEl` 等价于 `el.remove()`（真实脚本 `entry_08` 在用） |
| | `stopExecution()` | 抛出内部信号，`activateItem` 捕获后按「脚本主动终止」处理，**不计为错误**（真实脚本用它做幂等守卫） |
| **占位（不做，且写明原因）** | `Player.createSound(t, onLoad)` | 返回惰性 stub（`play`/`stop`/`close` 都是 no-op），`onLoad` **不调用**。两个硬阻塞：① M8 的 `createSound(t)` 是按**名字**取它内置音效库里的音（`t` 是音效类型而非 URL），这个音效库没有随客户端分发，宿主也没有可用的音频资产（零外部依赖、单文件内联）；② WebView2 的自动播放策略会拦截无用户手势的播放。**刻意不用合成音（振荡器/噪声）冒充原音效**——那会让脚本听起来「生效了」却完全不是原声，比明确的 no-op 更误导。要真做需要先解决音效资产来源 |
| | `External.Storage.loadRank/uploadScore/saveData/loadData` | 名字在、能调用，**不调用任何回调**（不伪造数据，避免脚本按假数据继续跑） |
| | `External.Bitmap.createBitmapData/createBitmap/createRectangle` | `createRectangle` 返回 `{x,y,width,height}`；`createBitmapData` 返回 `null`、`createBitmap` 返回空图片元件（位图管线未接入） |
| | `$.createVector` / `$.createMatrix` / `$.createColorTransform` / `$.createGlowFilter` / `$.createBlurFilter` / `$.createDropShadowFilter` / `$.createBevelFilter` / `$.createGradientBox` / `$.createPoint` / `$.createColor` | 名字在、能调用。`createMatrix` 返回支持 `a/b/c/d/tx/ty` + `createGradientBox`(no-op) 的矩阵对象（元素的 `matrix` 属性会用它）；滤镜工厂返回 `{type, color, alpha, blurX, blurY}` 占位对象，**元素的 `filters` 只识别 `GlowFilter`**（缓存期 `blur(4px)`+`lighter` 近似），其余滤镜不产生视觉效果。`createColorTransform` / `createGradientBox` / `createBitmapData` 返回 `null` |
| | `load(library, onComplete)` | 外部库加载未接入：**不调用 `onComplete`**（否则 `Bitmap.createBitmapData` 之类会立刻 ReferenceError） |
| **不支持** | `drawGraphicsData` | 需要完整的 IGraphicsData 对象模型（IGraphicsPath / IGraphicsStroke / …）；真实脚本 0 次使用，遇到时显式抛错而非静默画错 |
| | `_Galgame` 里的 `TweenEasing` 命名空间 | 不需要：缓动名沿用现有 `easingTable`，`resolveEasing` 已支持 `M8Easing.SineEaseInOut` 这类带前缀的全名 |
| | 字体排版（`fontData` / `advanceHori` / `kernings`） | 真实脚本里占比最大的能力（11 条样本里 7 条是字体数据），需要完整 TrueType 轮廓排版，本阶段不做 |
| | 3D / `transform.matrix3D` | 2D 画布，`rotationX`/`rotationY` 只存储不呈现 |

  > **`ctx.t` / `ctx.progress` / `ctx.g` 已彻底取消**：它们没有 M8 对应物。逐帧回调只剩 M8 的 `interval` 这一条路径，且回调签名是 M8 的「无参数」形式——逐帧状态一律读 `Player.time`（值来自宿主自己的外推时钟，不增加消息往返）。
  > **`Player.time` 必须实时读**：脚本典型写法是先记 `var t0 = Player.time;`，再在 `interval` 回调里用 `Player.time - t0` 判断「脚本开始后过了多久」。宿主内部仍然按条目窗口激活一次脚本，但**时间量不再是激活时的快照**。
  > **定时器生命周期（重点）**：`timer` / `interval` 一律登记在条目上，条目回收 / `reset` / seek 越窗时由宿主统一清掉；`ScriptManager.clearTimer()` 清当前条目的定时器。**定时器绝不会活过条目**——这正是 M8 用 `ScriptManager.clearTimer` 解决的问题，宿主把它做成了兜底而不只依赖脚本自觉。定时器按「条目已播放时间」推进（`advanceItem` 的帧增量），因此暂停时它与画面一起停住。
  > **声明式 `motion` 与 `Tween.*` 是两条路径**：`opts.motion`（以及 `$.create*` 的 `lifeTime`）是**按条目进度 `elapsed` 插值**的声明式补间，seek 回窗口内重建时位置是插值结果；`Tween.tween/to/...` 返回的句柄有自己的播放头（`play()` 起算），用于脚本显式控制播放。两条路径都由同一套段列表插值器驱动，区别只在「时间从哪来」。
- **时间同步**：照搬 `bas-host.html` 已验证的 `state` + `currentPositionMs()` 模式（playing 时 `performance.now()` 外推，`setState`/`seek` 重置基准）。
- **桥协议（JSON）**：
  - 主→宿主（控件 → `window.scriptDanmakuHost.*`）：`reset` / `append` / `beginItem` / `appendItemChunk` / `endItem` / `setState` / `seek` / `visible` / `resize`，以及本轮新增的数据链与输入链——
    - `resetComments()` / `appendComments([...])`：弹幕快照（脚本侧 `Player.commentList`）。分两段推是因为整池可能有几千条：先清空、再按 24KB 的**字节预算**分批追加（与 `append` 的分块上限同一套口径；按预算而不是按固定条数切分——单条弹幕长度可以差一个量级）。
    - `pushComment({txt,time,color,pool,mode,fontSize})`：用户发送了一条弹幕 → 投递给 `commentTrigger`。
    - `pushKey(keyCode, up)`：键盘事件 → 投递给 `keyTrigger`。
  - 宿主→主（`post(type, …)`）：`ready` / `parsed` / `rendered` / `error` / `action`（含 `pause` / `play` / `seek` / `navigate`）。
  > 零开销原则仍然成立：没有任何脚本时 `PushDanmakuBatchAsync` 经懒初始化闸门直接返回，不创建 WebView2、不发任何 RPC；分页加载反复落定弹幕池时，控件按「同一列表实例 + 条数不变」跳过重复投递。
  > 设计稿另列的 `clear` / `danmakuBatch` / `judgeBatch` / `inputEvent` / `playbackAction` / `sendRequest` / `queryDanmaku` / `filter` / `send` / `judgeResult` / `inputVerdict` / `actionVerdict` 属后续阶段（弹幕**拦截**与发送**改写**，与本次的「读取弹幕 / 监听输入」是两回事）。

#### 3.1 保留模式：脚本只执行一次，逐帧只插值属性（本版新增，取代原「每帧调绘制回调」）

> 原文写的是「每帧遍历活跃弹幕调绘制回调」，即**立即模式**。该模型已废弃，理由如下。

**为什么必须改**

- **兼容真实作品**：B 站旧版 Flash 播放器的 M8 引擎（反编译 `play_20120501.swf`，`org/tamaki/`）是**保留模式**——`CommentScript.exec()` 一次性 `vm.execute()` 跑完脚本，之后由 BetweenAS3 的 `EnterFrameTicker` 逐帧插值属性（`org/libspark/betweenas3/tickers/EnterFrameTicker.as:203`），`MotionManager` 的 ENTER_FRAME 只用于判定 `lifeTime` 到期。全树检索 `vm.execute()` 只有 `CommentScript.as:186` 一处调用点，**没有任何每帧重跑脚本的驱动源**。
- **实测数据**（av2669196 的 11 条 mode=8 真实脚本，`/root/m8-csharp/scripts-fixtures/`）：主脚本 354KB 含 `var shape =` 93 处、`beginFill/endFill` 123 处、`drawWedge/drawCircle/drawRect` 87 处、`createGlowFilter` 18 处、`createMatrix` 20 处——全是「建对象 → graphics 画一次 → 加滤镜/矩阵」；另一条 50KB 脚本含 `Display.Sprite` 15 处、`addChild` 19 处、`easeIn/easeOut` 22 处。**这些脚本里没有任何每帧重绘的代码**：把老脚本丢进立即模式宿主，画面只会在第一帧出现一次（甚至不出现），之后靠插值的部分全部丢失。
- **表现力**：graphics 路径绘制、滤镜链（glow）、3D、矩阵、字形排版（`fontData`）都是保留对象上的能力；立即模式要等价复刻得把整套图形 API 重做一遍。
- **性能**：保留模式下补间与滤镜交给合成层，同屏几十条无压力；立即模式每帧全屏 `clearRect` + 重跑脚本 + 发光滤镜，在这种体量下反而更重。

**保留模式的具体形态**

- **元素**：脚本执行期间创建 `M8Element` 式保留对象（text / shape / image / layer），进入宿主的元素树；属性含 `x/y/scaleX/scaleY/rotation/alpha/visible/filters/matrix`。
- **动画**：两条入口，都由统一的段列表插值器推进——① 声明式 `motion`（`opts.motion = {x: {fromValue, toValue, lifeTime, startDelay, easing, repeat}}`，按条目进度 `elapsed` 插值）；② M8 的 `Tween.tween/to/...` 句柄（自带播放头，`play()` 起算，配 `delay/scale/reverse/repeat/slice/serial/parallel` 组合子）。**逐帧回调只剩 M8 的 `interval` 一条路径**（回调无参数，逐帧状态读 `Player.time`），且它同样登记在条目上、随条目一起回收。
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

13. **放弃自研 `ctx` 面，脚本环境直接暴露原版 M8 的 API（已落地）**。这是本分支对脚本 API 的**破坏性重做**，`ctx.*` 全部移除。
    - **动机**：自研 API 面等于要求每个作者重写脚本，而本平台的目标恰恰是「让当年的真实 M8 作品尽量原样跑起来」。11 条真实 mode=8 脚本（`/root/m8-csharp/scripts-fixtures/`）的统计很直白：`$.createShape` 93 处、`beginFill/endFill` 123 处、`drawWedge/drawCircle/drawRect` 87 处、`createGlowFilter` 18 处、`createMatrix` 20 处、`Global` 90 次、`Utils.rgb` 61 次、`ScriptManager.popEl` 4 次、`stopExecution` 1 次——**没有一行 `ctx.xxx`**。更关键的是，前一版分支 `feature/m8-script-engine` 的结论就是「大部分 mode8 代码弹幕 api 返回不完整」，自研面在没有真实脚本可跑的情况下无法自我验证。
    - **注入机制**：宿主用 `new Function("$", "Player", "$G", "Global", "Tween", "Utils", "ScriptManager", "timer", "interval", "clearTimer", "trace", "tracex", "stopExecution", "foreach", "clone", "getTimer", code)` 把 M8 全局名作为**参数**注入。用参数而不是 `window.xxx`：脚本对 `$` / `Tween` 的赋值只影响它自己那一次执行，不污染其它条目。整份宿主仍然只有这一处 `new Function`，「脚本只编译一次」的契约不变。
      > `Global` 是 `$G` 在 M8 文档里的另一个名字，真实脚本两种都在用（样本里 `Global` 出现 90 次），因此两个名字注入同一个对象。`$G._` 也是 `_get` 的别名（Galgame 示例在用）。**真实脚本还依赖 `new Function` 体的非严格模式隐式全局**（样本里 `Factory` 被裸用 38 次，由更早的脚本赋值产生）——宿主保留了这一行为（脚本体不在 `"use strict"` 下）。
    - **渲染引擎一行未改**：保留模式的全部约束继续成立——脚本每条只执行一次、保留元件树、声明式补间、脏元素重绘、脏矩形擦除、元素寿命 `min(脚本声明 lifeTime, 条目窗口剩余)`、暂停自停。改动只落在「脚本拿到的名字」这一层。
    - **`motion` 与 `Tween.*` 分两条路径**（这是本次唯一动到补间引擎的地方）。M8 的 `opts.motion` 是**声明式**动画（元件上的属性声明），必须按**条目进度 `elapsed` 插值**——否则 seek 回窗口内重建时元素会从 0 重新开始，破坏保留模式契约（行为用例 D6 钉住了这一点）。`Tween.tween/to/...` 返回的 ITween 是**命令式**句柄，有自己的播放头（`play()` 起算）。两者共用同一套「段（segment）列表」插值器：段 = `{offsetMs, durationMs, tracks:[{key,from,to,easing}]}`，取值规则是**每个属性各认一个段**（从最后一段往前找第一个「已开始且含该属性」的段）。这条规则是修出来的：最初写成「只取最后一个已开始的段」，并行补间（`parallel`，或多属性各自带不同 duration）里先声明的属性永远不会被写（D13）。
    - **itween 组合子**（`delay`/`scale`/`reverse`/`repeat`/`slice`/`serial`/`parallel`）只做**段列表的时间轴运算**，返回的句柄**不带元件**：元件集合沿用来源句柄，等 `play()` 时由 `installTweenHandle` 统一装到元件上，并把来源句柄停用（否则两条句柄会各写同一批属性）。派生句柄一律深拷贝段列表——共享段对象会让 `repeat` 重复插值、`reverse` 把源句柄的 `from`/`to` 一起换掉（实际踩到过）。
    - **时间必须是实时读的**：`Player.time` 是 getter，读宿主外推时钟（毫秒），不是激活时的快照。M8 脚本的典型写法是「先记 `var t0 = Player.time`，再在 `interval` 回调里用 `Player.time - t0` 判断过了多久」，快照会直接算错。`Player.state` / `width` / `height` / `commentList` 同理。
    - **定时器生命周期**：`timer` / `interval` 一律登记在**条目**上（`item.scheduledTimers`），条目回收 / `reset` / seek 越窗时由 `deactivateItem` / `clearAllItems` 统一清掉；`ScriptManager.clearTimer()` 只清当前条目。**定时器绝不会活过条目**——这正是 M8 用 `ScriptManager.clearTimer()` 解决的问题，宿主做成了兜底而不只依赖脚本自觉（D12）。定时器按「条目已播放时间」的帧增量推进，因此暂停时与画面一起停住；`hasPendingAnimation` 也把「还有定时器在跑」计入，否则暂停后自停判据会把定时器冻住。
    - **占位而不伪造**：当时依赖数据链或 Flash 运行时能力的 API 做成「名字在、能调用、不生效」并逐条标注——`Player.commentList`、`commentTrigger`/`keyTrigger`、`setMask`、`createSound`、`Player.play()`、`External.Storage.*`、`External.Bitmap.*`、`load()`。完整清单见 §3 的三类表格。
      > **本条的占位清单已被实施记录 14 大幅收窄**：`Player.play()` / `commentList` / `commentTrigger` / `keyTrigger` / `setMask` 都已真实现，仅 `createSound` 与 `External.*` / `load()` 仍是占位。
    - **未做**：字体排版（`fontData` / `advanceHori` / `kernings` —— 11 条真实样本里 7 条是字体数据，是占比最大的能力，需要完整 TrueType 轮廓排版）、`beginGradientFill` / `lineGradientStyle` / `drawGraphicsData` / `drawPath`（需要完整图形数据模型，遇到时**显式抛错**不静默画错）、`rotationX`/`rotationY`（3D，2D 画布只存储）、`el.mask`（只存储）、`$.createButton` 的 `onclick`（无输入通道）。
    - **验证**：宿主行为套件新增 D11（**M8 脚本原样执行**：`$` / `Player` / `$G` / `Global` / `ScriptManager` / `timer` / `interval` / `foreach` / `clone` / `Utils` / `trace` / `stopExecution` 都在脚本作用域里可用，`$G` 跨条目共享，`Utils.hue` 的映射与文档一致）、D12（定时器随条目回收 / reset / seek 越窗一律不再跑）、D13（ITween 句柄与 `delay`/`serial`/`reverse`/`repeat`/`parallel` 组合子、`stop()` 后停在原地）；契约测试新增 `Host_DoesNotInjectCtxIntoScripts` 等 9 条改写为 M8 面。另外用一次性脚本（不进仓库）把真实脚本的三种形态原样丢进宿主验证：Galgame 示例形态（`$.createCanvas` + `parent:` + `$G._()` + `interval` + `setStyle` + `$.createButton`）、Akari 形态（`Global._get/_set` 幂等守卫 + `stopExecution` + `ScriptManager.popEl`）、以及 `Utils`/`foreach`/`clone`/`remove`/`clearTrigger` 的混合形态，均无错误上报。

14. **补齐脚本 API 的占位项：播放动作 + 弹幕/输入数据链 + 舞台遮罩（已落地）**。实施记录 13 收尾时列的占位清单里，能真实现的这一轮都做了；剩下确实做不通的（`createSound`、`External.*`、`load`）保持占位并写明原因。
    - **`Player.play()`（桥协议新增 `play` 动作）**。此前是唯一被主动放弃的接口，理由是「C# 侧没有播放动作通道，不为它乱造桥消息」。这一轮按既有协议风格补齐：宿主 `post("action", {action:"play"})` → `ScriptDanmakuControl.HandleActionMessage` 的 `case "play"` → 新增 `ScriptDanmakuActionKind.Play` → `PlayerPage` 的 `case ScriptDanmakuActionKind.Play: mediaPlayer?.Play();`。**C# 改动是三个文件各加一处**，与既有 `Pause` 分支同形，没有引入任何新机制。
    - **弹幕数据链（桥协议新增 4 条 C#→宿主命令）**。`Player.commentList` 取的是**推入的快照**而不是拉取——脚本在正文里一次性读它（M8 的官方示例就是 `for (i=0;i<Player.commentList.length;i++)`），异步拉取会让 `length` 读到 0，所以必须在脚本执行前就位。链路：`PlayerPage.SetDanmakuPool`（弹幕池落定）→ 转成 `ScriptDanmakuComment`（M8 CommentData 形状）→ 控件 `PushDanmakuBatchAsync` → 宿主 `resetComments()` + `appendComments([...])`（按 24KB 字节预算分批）。三个务实处理：① **控件保留快照并在 `reset` 之后补投**，这样「先加载弹幕池、后加载脚本」的顺序下脚本仍读得到数据；② **分页加载会反复落定弹幕池**，按「同一列表实例 + 条数不变」跳过重复投递；③ **没有任何脚本时经懒初始化闸门直接返回**，不创建 WebView2、不发 RPC（零开销原则）。
      - `mode` 的映射不能想当然：`DanmakuModel` 只保留了 `DanmakuLocation` 枚举，它的**枚举序号不是 B 站的 mode 编号**，必须按 `BiliDanmakuService.TryToLocation` 的反向映射（Scroll→1 / Bottom→4 / Top→5 / ReverseScroll→6 / Position→7）。已知信息损失：解析时 mode 1/2/3 都归成 Scroll，池子里不再保留原始 mode，脚本看到的一律是 1。
    - **`commentTrigger` / `keyTrigger`（桥协议新增 2 条 C#→宿主命令）**。`pushComment` 由 `SendDanmakuDialog.DanmakuSended` 回调转发，`pushKey` 由 `PlayerPage_KeyDown` / 新增的 `PlayerPage_KeyUp` 转发。要点：① **键值整数值透传**——M8 允许监听的那组键（小键盘 0-9、方向键、Home/End/PgUp/PgDn、W/S/A/D）与 Windows `VirtualKey` / DOM/Flash `keyCode` **同值**，所以 C# 侧 `(int)args.VirtualKey` 直接过桥、宿主侧按 `M8_TRIGGER_KEY_CODES` 筛选，不需要任何映射表；② **`up` 参数真的可用**：为它补了 `KeyUp` 订阅，与既有 `KeyDown` 同址（`OnNavigatedTo` 挂、退出路径摘）成对；③ **触发器与定时器共用同一条生命周期**——登记在 `item.triggers`，`deactivateItem` / `clearAllItems` 统一清掉，条目回收 / `reset` / seek 越窗后一律不再触发（行为用例 D16）；④ **投递是消息驱动的**，不依赖帧循环，所以暂停时收到的事件也会立即送达回调，回调里照常可以建元件（宿主会把该条目设为 `activeItem` 并拉起一帧）。`timeout` 用**条目已播放时间**计量而不是 M8 的挂钟——本宿主的一切都挂在这条时钟上，混用两套时钟会让触发时机与画面错位（已在代码注释与 §3 写明这处偏离）。
    - **`Player.setMask` 真实现为合成期裁剪**。裁剪点只有一个：`composeElement`（`applyStageMask` + `clip()`），因此**元素自己的离屏缓存完全不受遮罩影响**，遮罩变化不会让任何元素重建位图。三条边界写清楚并各有断言：① **擦除（`flushEraseRects`）与整屏清空（`clearSurface`）不带裁剪**——它们要抹掉的正是上一帧的像素，带上裁剪会留下旧内容；② **换遮罩时整屏清空 + 全部重合成**（可见区域变了，已画像素全作废），这是 `clearSurface()` 的第 4 条合法路径，契约测试从 3 处放宽到 4 处并显式加入 `setStageMask`；③ **遮罩元件本身不参与渲染**（M8 的遮罩对象不在显示列表里）——只从渲染树摘除，仍留在条目元素表里，变换照旧可用、条目回收时一起释放。已知近似：文本 / 图片 / 复合元件当遮罩时退化成它的外接矩形（形状元件按真实图元描路径）。
    - **仍未实现（并写明原因）**：`Player.createSound` —— M8 的 `createSound(t)` 是按**名字**取它内置音效库里的音，这个库没有随客户端分发，宿主也没有可用音频资产（零外部依赖、单文件内联）；且 WebView2 的自动播放策略会拦截无手势播放。**刻意不用合成音冒充原音效**（那会是「听起来生效了、但完全不是原声」，比明确的 no-op 更误导）。`External.Storage.*` / `External.Bitmap.*` / `load()` 保持占位（不伪造数据、不发请求、不调回调）。
    - **行为套件新增 D14~D17**：D14（`Player.play/pause/seek/jump` 四个动作真的发出 `action` 消息、非法 av 号被拒、负数 seek 钳到 0）、D15（`commentList` 快照可读、字段形状与缺省补齐、`resetComments` 后清空）、D16（`commentTrigger`/`keyTrigger` 只在收到桥消息时触发、keyUp 只投给 `up=true`、M8 允许键之外的按键被忽略、条目回收与 `reset` 之后不再触发）、D17（`setMask` 把落笔裁到遮罩形状内、遮罩外元素不画但仍存活、取消遮罩后恢复完整）。契约测试新增 7 条（宿主命令名与 `action: "play"`、`Player` 的 `setMask`/`commentTrigger`/`keyTrigger` 接到实现上、M8 键值集合、裁剪只落在合成期、控件与 PlayerPage 的新挂钩）。

15. **补齐 Flash DisplayObject 能力，让 av2669196 的真实 M8 脚本真的出画面（已落地）**。这是「11 条真脚本全部灌进同一宿主实例、0 runtime error 且出画面」这一个硬指标一路逼出来的：每修掉一个报错就露出下一个，最后补了 4 类 Flash 能力 + 5 处兼容语义。
    - **起点与终点**：改造前 11 条里 8 条字体脚本能跑（只注册数据、不出画面），出画面的两条都挂在报错上（`entry_08` 断在 `sprite.transform.matrix3D=null`、`entry_10` 连带断在 `Akari.stop()`）。改造后：**11 条同实例 0 报错，主画布 79 次 `drawImage`**（t≈150s）。
    - **4 类 Flash DisplayObject 能力**（本轮的主题）：
      1. **`element.transform`**：`.matrix` 与元素已有的 `props.matrix` **是同一份对象**（脚本的 `mx = el.transform.matrix; mx.identity()` 是取出→原地改→写回，两套数据会互相打架）；`.matrix3D` / `.colorTransform` 可读可写只存储；`.getRelativeMatrix3D()` 返回累计 2D 变换铺成的 Matrix3D。配套把 `$.createMatrix3D` / `$.createVector3D` 从占位改成**真实现**（`append*` / `transformVector` / `transformVectors` 原地填充 / `invert`，rawData 用 Flash 的列主序）——Akari 用 `transformVectors` 做 3D 深度排序，算错画面顺序就错。
      2. **显示列表查询**：`numChildren`（必须是 own property，脚本用 `hasOwnProperty("numChildren")` 判断「是不是显示对象」）/ `getChildAt` / `getChildIndex` / `setChildIndex` / `getChildByName` / `contains` / `addChildAt` / `removeChildAt` / `swapChildren`。`getChildAt` 越界返回 `null` 而不是像 Flash 那样抛 RangeError（不让坏索引炸掉整条脚本）。
      3. **`blendMode`**：映射到 canvas 的 `globalCompositeOperation`（`add`→`lighter`、`multiply`/`screen`/`overlay`/`difference`/`lighten`/`darken`/`hard-light`/`color-dodge`… 同名直映；Flash 8 的 `layer`/`alpha` 近似成 `source-over`），**未知值一律退回 `normal` 不抛错**。脚本读回的 `blendMode` 是原值（它在图层之间互相拷贝）。
      4. **元素级 `mask`**：`被遮罩元素.mask = 遮罩元素`，**只裁被遮罩元素自己（含子树）**，不串到整块画布——这是它与 `Player.setMask` 的关键区别。实现上有个坑：**不能**用 `save → 施加遮罩元件的变换 → clip → restore`，因为 canvas 的裁剪区在状态栈里，`restore` 会把刚建立的裁剪一起去掉；改为把遮罩元件的变换**烘进路径坐标**（`createMaskPointMapper`）再用当前坐标系直接 `clip()`。遮罩元件自己不参与合成（连 `lastPaintedRect` 都不留）。
    - **另外 5 处是「不补就出不了画面」的兼容语义**，都不是可选装饰：
      - **元素属性必须不可枚举**（`hideElementInternals`）。Flash 里显示对象的属性挂在原型上，脚本 `foreach(obj, fn)` 遍历显示对象**一个属性都拿不到**；Akari 的 `Factory.clone` 正是靠 `countProperties === 0` 分叉走「新建 `$.createCanvas` 再逐个拷贝显示属性」那条路。本宿主的元素是普通对象、内部字段全在自身上且成环（`treeParent ↔ childList ↔ ownerItem ↔ motion.handle.elements`），可枚举就会让 clone 顺着环无限递归（实测 `Maximum call stack size exceeded`）。构造期之后新增的字段（`shapeItems`/`graphics`/`autoCached`/`createParent`/`transformValue`/`maskUseCount`）也要逐个藏。`hasOwnProperty` 不受影响。
      - **`foreach` 只遍历真对象**：原始值（string/number/boolean）不遍历。上面那条 clone 递归到字符串时，枚举 `"a"` 的字符下标会 `clone("a")` → `clone("a")` 无限递归。M8 文档把参数标成 `Object`、Flash 的 for-in 对原始值也不产生可枚举属性，所以「零次迭代」才是正确语义。
      - **`ScriptManager.popEl` 的语义是「从自动清理表里弹出」**，不是从显示列表摘除（`clearEl()` 会跳过被 popEl 过的元件）。早先实现成 `el.remove()`——Akari 把整幅作品挂在 `popEl` 过的常驻 root 下，于是整棵树脱离渲染，`topLevel=0`、26797 个元件一个像素都画不出来。
      - **`Event.ENTER_FRAME`**：`el.addEventListener("enterFrame", fn)` 每帧派发。Akari 的 `Composition.present()` 就是 `canvas.addEventListener("enterFrame", frameFn)`，整幅画面的每帧更新挂在这上面。监听表按条目存、随条目回收清掉，派发早于补间推进与脏元素重绘。
      - **AVM1 宽容语义**：读未声明变量得到 `undefined` 而不抛 `ReferenceError`。`entry_10` 的 `update:function(time){if(time < startTime)...}` 里 `startTime`/`duration` 是 AS→JS 翻译时丢掉的 `var`。实现：`runItemScriptWithAvm1Scope` 捕获 `ReferenceError` 认出名字 → **声明到脚本全局对象上**（值 `undefined`）→ 清理并重跑本条脚本。**为什么是全局对象而不是补脚本形参**：抛错的闭包可能不是本条脚本创建的——`entry_10` 抛错的那处 `update` 定义在 `entry_08` 的 Akari 库里，作用域链在 entry_08 执行时就定死了，补形参救不了；标识符解析对未绑定名是每次访问都回落全局对象查，只有补在全局对象上才能让**已建好的闭包**一并恢复。定时器 / 触发器 / enterFrame 回调里抛的同类错误同样处理（`startTime` 就是在 interval 回调里读的）。安全性：只声明「已被 `ReferenceError` 证明不存在」的名字，**绝不会遮蔽任何真实存在的名字**（脚本自己声明的、或像 `entry_10` 那样用 `Factory.extend(this, …)` 导出到全局对象上的，都照旧解析）；不做静态分析、不动作用域链（不用 `with`）。
    - **顺带补上的**：`drawPath`（真实脚本在用，此前是显式抛错——复用 `moveTo`/`lineTo`/`curveTo` 的路径模型，命令码 1/2/3/4/5 完整、6 取第一个控制点近似）、`beginGradientFill` / `lineGradientStyle`（按 `createGradientBox(w,h,rotation,tx,ty)` 的渐变框换算成 canvas 线性/径向渐变）、`DisplayObject.name`（`getChildByName` 要用）、`$.width`/`$.height`（`$` 作为 Display 命名空间的舞台尺寸，entry_08 用它算居中与缩放比）。`onclick` / `drawGraphicsData` 仍不支持。
    - **三轮对照实测（每条结论都是跑出来的，不是推的）**：
      | 加载 | 结果 |
      |---|---|
      | 全 11 条 | 0 报错；主画布 19→39→59→79 次 `drawImage`（t=90/110/130/150s） |
      | 去掉 `entry_10` | 落笔 **0**（`entry_10` 才是真正绘制的主作品） |
      | 去掉 `entry_08` | `entry_10` 直接报错（Akari 库来自 `entry_08`） |
      | 仓库夹具（`entry_08` + 一条字体脚本） | 0 报错、**0 落笔**（`entry_10` 的文字图层要从 `$G` 取 6 张字形表，少一条就报错） |
      > **对任务书里「entry_08 与 entry_10 必须出画面」的精确化**：实测 `entry_08` 是**库 + 骨架**，单独加载在任何时间点都不落笔；可见画面来自 `entry_10`（主作品）**渲染时用 entry_08 的 Akari 库**，且还需要 6 条字体脚本注册字形表。所以「出画面」的准确表述是「`entry_08` + `entry_10` + 字体脚本一起出画面」。
    - **测试**：新增 `tests/host/real-m8-scripts.test.js`（真实脚本集成测试，5 例：仓库夹具齐备 / 0 报错 / 字体脚本不出画面 / 字形表注册进 `$G` / 完整 11 条 0 报错且真落笔；外部夹具目录缺失时**跳过**而非失败）；`retained-mode.test.js` 新增 D18~D22（transform 与 Matrix3D、显示列表 + 不可枚举、blendMode 映射与未知值、元素级遮罩作用域、popEl + enterFrame）；契约测试新增 5 条。夹具取舍与体积见 `tests/host/fixtures/real/README.md`。

测试：`tests/BiliBili.Tests/` 下三个文件——`ScriptDanmakuParserTests.cs`（解析/校验契约）、`ScriptDanmakuHostContractTests.cs`（宿主↔控件字符串契约：命令名、消息类型、**M8 注入名单与「不再注入 ctx」**、`Player` 的实时 getter 面、`$` 元件工厂与创建参数、**定时器登记在条目上**、dpr 缩放、可见性、自停位置、单脚本失败隔离、脏矩形擦除、缓存失效白名单、寿命 min 规则与摘除顺序、seek 重建入口唯一、不引入 BAS 资产、不为每条弹幕建 DOM）、`ScriptDanmakuPlayerPageContractTests.cs`（PlayerPage 接入完整性：倍速重推、可见性重推、PositionChanged 两条分发路径、清理点对称、层叠顺序、菜单处理器、跳转白名单）。

**宿主行为测试（`tests/host/retained-mode.test.js`）**：源码契约测试只能证明「某段代码还在」，证明不了「行为对不对」。宿主的渲染正确性用这个纯 node、**零依赖**（不需要 `npm install`）的套件补：它用 `node:vm` 把宿主 HTML 里的内联 `<script>` 加载进沙箱，桩掉 `document` / `canvas.getContext("2d")` / `requestAnimationFrame` / `performance.now`，按帧驱动并检查真实的画布操作序列。覆盖 D1~D22：

| 用例 | 语义 | 修复前的表现 |
|---|---|---|
| D1 | 移动元素跑 60 帧后不残留旧位置像素（无拖影） | 残留起点像素，`union.x` 停在 0 附近 |
| D2 | 自定义缓动抛错后帧循环存活、其他条目继续渲染、命令仍有效 | 异常逃出 rAF 回调，帧链冻死 |
| D3 | 元素寿命 = min(声明的 lifeTime, 条目窗口剩余时间) | 一律 3000ms，声明只能延长不能缩短 |
| D4 | 同屏有动画元素时，静止复合元素不重复重建整层 | 每帧重烘一次（60 帧 60 次） |
| D5 | `fontsize` 补间后缓存尺寸随之变化，静止元素不被过度失效 | 缓存尺寸不变，字号补间不可见 |
| D6 | 向后 seek 回窗口内：元素被重建、位置是插值结果、脚本不逐帧重跑 | 摘除后残影不擦、重建位置不对 |
| D7 | 内置示例（单条）声明式 `motion` 与 `interval` 并存，能渲染出画面且到点自然收尾 | 示例写的是立即模式，脚本跑完没有动画 |
| D8 | reset 整批作废时必须清画布，换一批弹幕不留旧像素 | 旧像素永久残留（换成新一批后仍在） |
| D9 | 无界窗口按兜底上限兜住、`lifeTime: 0` 常驻、`Player.time` / `Player.state` 实时可读 | 缺省窗口按 3 秒截断，`lifeTime: 0` 元素瞬间消失 |
| D10 | 暂停且没有待推进的补间时帧循环自停，恢复播放后重新拉起 | 无界窗口下暂停后 60fps 空转 |
| D11 | **M8 脚本原样执行**：`$` / `Player` / `$G` / `Global` / `ScriptManager` / 全局函数都在脚本作用域里，`$G` 跨条目共享 | 只有自研 `ctx`，真实脚本跑不起来 |
| D12 | 定时器登记在条目上：条目回收 / reset / seek 越窗后一律不再跑 | 条目销毁后 `interval` 继续触发 |
| D13 | ITween 句柄与组合子（`delay`/`serial`/`reverse`/`repeat`/`parallel`）、`stop()` 后停在原地 | 组合子结果没装到元件上，属性从不推进 |
| D14 | `Player.play/pause/seek/jump` 四个动作真的发出 `action` 消息，非法 av 号被拒、负数 seek 钳到 0 | `Player.play()` 是 no-op，不发任何消息 |
| D15 | `Player.commentList` 是推入的快照，字段形状与 M8 的 CommentData 一致、缺省补齐 | getter 恒返回空数组 |
| D16 | `commentTrigger`/`keyTrigger` 只在收到桥消息时触发；条目回收 / reset 后不再触发 | 注册返回 0，永不触发 |
| D17 | `Player.setMask` 把画面裁到遮罩形状里（合成期裁剪，不动元素缓存） | `setMask` 是 no-op，遮罩外照样画 |
| D18 | 元素 `transform`：`matrix` 与 `props.matrix` 同一份、`matrix3D` 可读写、Matrix3D/Vector3D 真算 | `transform` 不存在，脚本一碰就 TypeError |
| D19 | 显示列表 API + **元素属性不可枚举**（`foreach` 拿不到、`hasOwnProperty` 照旧） | 属性可枚举 → 真脚本的 clone 无限递归 |
| D20 | `blendMode` 映射到 `globalCompositeOperation`，未知值退回 `normal` | `blendMode` 只是个普通字段，不影响合成 |
| D21 | 元素级 `mask` 只裁被遮罩元素子树、遮罩元件自己不合成 | `el.mask` 只存储、不生效 |
| D22 | `popEl` 不摘离渲染树；`Event.ENTER_FRAME` 每帧派发 | `popEl` 实现成 `remove()` → 整棵树脱离渲染 |

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
  3.6. **M8 API 面验收**：把一条**真实的旧 M8 脚本**（当年作品导出的 mode=8 文本）原样贴进 `.js` 加载，确认不抛错、能画出来；重点核 `Player.time` 在 `interval` 回调里是**实时值**（不是激活快照）、`$G` 跨条目共享、条目结束后 `interval` 不再触发（定时器随条目回收）。
  3.7. **数据链与输入链验收**（必须真机，单测只能证明消息通了）：① 加载一条读 `Player.commentList` 的脚本（按 M8 官方示例数「是/否」），确认读到的条数与实际弹幕池一致；② 发一条弹幕后确认 `commentTrigger` 回调被触发且内容/颜色/时间正确；③ 按方向键确认 `keyTrigger` 收到键值，按住不放/松开能区分 `keyDown` 与 `keyUp`（`up=true`）；④ 条目窗口结束后再发弹幕/按键，确认不再触发；⑤ `Player.setMask` 用一个矩形遮罩确认弹幕只在遮罩内出现，`setMask(null)` 后恢复；⑥ `Player.play()/pause()/seek()/jump()` 各验一次（**这条要在 Windows 上跑**，本环境编不了 UWP）。
  3.8. **性能回归点**：未加载任何脚本时，播放 / 切集 / 输入路径不得因本轮新增的推入而出现可感知开销（控件应在懒初始化闸门上直接返回，且分页加载不重复推同一份池子）。
  4. 四类拦截各验一次：点击被消费、播放操作被阻止、弹幕被拦下、发送被拦下。
  5. 三类交互各验一次：读到弹幕数据、屏蔽一条、发送一条。
  6. 弹幕总开关关闭 → 脚本弹幕隐藏；未加载时不初始化 WebView2。
  7. **未注册任何拦截器时，播放/弹幕/输入路径无可感知开销**（性能回归点）。
- **接口可用性前置**：阶段 4 开工前，先按 `Controls/SendDanmakuDialog.xaml.cs:57` 的参数与签名形态实测 `x/v2/dm/post`，确认可用后再决定抽取方式（见 §6③）。不要先按 `PlayerAPI.SendDanmu` 实现。
- **回归**：BAS 弹幕（mode9）行为不变。
- **测试**：`tests/BiliBili.Tests`（net8.0 + MSTest）。阶段 1 已补 `ScriptDanmakuParserTests`（18 例）、`ScriptDanmakuHostContractTests`（37 例，含保留模式改造后的断言）、`ScriptDanmakuPlayerPageContractTests`（8 例）；另有宿主行为测试 `tests/host/retained-mode.test.js`（纯 node、零依赖，22 例 D1~D22，见上）与真实脚本集成测试 `tests/host/real-m8-scripts.test.js`（5 例，夹具缺失时跳过）。**CI 已接入**：`.github/workflows/ci.yml` 的 `test` job 在 `dotnet test` 之前跑 `node tests/host/retained-mode.test.js`（运行器自带 node，无需 `setup-node`）。覆盖不到的部分——实际渲染、时间同步、性能——必须走页面级验证。
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
