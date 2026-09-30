// 共享状态、基础工具与 WebView2 消息桥。
var hostState = {};

// 渲染模型：保留模式（设计文档 §3.1）。
// 每条脚本只执行一次，执行期间用 M8 的 $ / Player / Tween 建出保留元素树并声明补间；
// 之后每帧只推进 tween 插值、更新属性、重绘脏元素。
// 禁止每帧重跑脚本绘制：真实 M8 作品脚本里没有逐帧重绘代码，
// 立即模式只会让画面停在第一帧（理由见设计文档 §3.1）。
//
// 清屏策略（设计文档 §3.1，本版修正）：逐帧**不做整屏 clearRect**，
// 而是「本帧移动过 / 已释放 / 已隐藏」的元素按上一帧的包围盒擦除，
// 再合成本帧的脏元素。静态元素因此既不会被重画、也不会被误擦。
// 整屏 clearSurface() 只出现在「整幅画面作废」的三条路径上：tick 的隐藏分支、
// stopRunning、以及 reset 整批替换条目时（被丢弃的元素不会再进擦除队列）。

// 条目未声明 duration 时的兜底上限（毫秒）。duration 缺省或非正数
// 表示「不设时间窗」——原版 M8 没有条目窗口，元素寿命由脚本的
// lifeTime 决定，这里只留一个防呆上限，避免脚本永久占位。
var MAX_ITEM_WINDOW_MS = 600000;
var DEFAULT_LIFE_TIME_SECONDS = 3;
var MAX_COMPILE_ERROR_REPORTS = 8;
var MAX_RUNTIME_ERROR_REPORTS = 8;
var DEFAULT_TEXT_COLOR = 16777215;
var DEFAULT_TEXT_FONT = "黑体";
var DEFAULT_TEXT_FONTSIZE = 25;
var GLOW_PADDING = 12;
var DEFAULT_BOUNDS_PADDING = 2;
// 脏矩形擦除的外扩量（CSS 像素）：吸收缩放/旋转后的取整误差，
// 否则被擦掉的一圈上会留下上一帧的细边。
var DIRTY_RECT_PADDING = 2;
// 元素寿命的「不设上限」取值：条目窗口剩余时间才是真正的上限。
var LIFE_TIME_UNBOUNDED = Infinity;
// 声明式 motion 里 repeat: 0 / 负数按无限重复处理，展开时用它兜住；
// 真正的收口是元素寿命（元素到期时补间随之停止）。
var MAX_REPEAT_EXPANSION = 1000;
// 单帧最多推进多少「播放时间」。暂停 / seek / 长卡顿后帧增量可能极大，
// 不钳制会让 Tween.* 句柄的时间轴一步跳完（与 M8 的逐帧推进语义不符）。
var MAX_FRAME_DELTA_MS = 250;
// Player.jump 的 av 号解析：只接受 "av123" / "123" 两种写法。
var AV_NUMBER_PATTERN = /^(?:av)?(\d+)$/i;
// commentTrigger / keyTrigger 的默认监听时长（M8 文档：1000ms）。
// （Player.refreshRate 不再是常量：原版是空实现，见那边的 getter 注释。）
var DEFAULT_TRIGGER_TIMEOUT_MS = 1000;
// 注入脚本作用域的 M8 全局名。compileItem 按这张表生成形参、
// createScriptScope 按同一顺序生成实参，两边必须逐字对应。
// Display 与 $ 指向同一个对象：原版的 globals 里两个名字并存
// （CommentScriptFactory.as:112 「"Display": this._display」与 :113 的
// 「"$": this._display」），ScriptBitmap.as:50 还从 scope["Display"]
// 反查 _defaultConfig / extend / setupMotionElement。宿主此前只注入 $，
// 脚本写 Display.createCanvas(...) 会掉进 AVM1 兜底被声明成 undefined，
// 随后的属性访问抛 TypeError。
var SCRIPT_GLOBAL_NAMES = [
    "$", "Display", "Player", "$G", "Global", "Tween", "TweenEasing", "Utils",
    "ScriptManager",
    "timer", "interval", "clearTimer", "clearTimeout", "clear", "load",
    "trace", "tracex", "stopExecution", "foreach", "clone", "getTimer",
    "parseInt", "parseFloat", "Math", "String", "Bitmap",
    // AS2/AS3 的全局转换函数。真实脚本（av2669196 的 11 条）里
    // String 用了 1 次、其余为 0，但它们在 M8 文档的示例与当年作品里
    // 很常见，而 AVM1 兜底救不了「函数被当作对象属性访问」之外的情况。
    "int", "uint", "Number", "Boolean", "isNaN"];
// 上面这张表的反查表：判断某个未声明标识符是不是已经注入过的 M8 全局名
// （是的话说明不是「未声明」，补绑也救不了，直接按脚本失败处理）。
var INJECTED_SCRIPT_NAMES = {};
for (var injectedIndex = 0; injectedIndex < SCRIPT_GLOBAL_NAMES.length; injectedIndex++) {
    INJECTED_SCRIPT_NAMES[SCRIPT_GLOBAL_NAMES[injectedIndex]] = true;
}

// 「未声明标识符按 undefined 处理」的重试上限（见 runItemScriptWithAvm1Scope）。
var MAX_AVM1_SCOPE_RETRIES = 8;
// 保留字不能当形参。
var RESERVED_WORDS = {
    break: true, case: true, catch: true, class: true, const: true, continue: true,
    debugger: true, default: true, delete: true, do: true, else: true, enum: true,
    export: true, extends: true, finally: true, for: true, function: true, if: true,
    import: true, in: true, instanceof: true, new: true, return: true, super: true,
    switch: true, this: true, throw: true, try: true, typeof: true, var: true,
    void: true, while: true, with: true, yield: true, let: true, static: true,
    null: true, true: true, false: true, arguments: true, eval: true
};

// 播放器状态。`stopped` 是**播放结束**这一个瞬时状态的标记，
// 只在收到宿主明示的 setStopped 时置位；setState / seek / reset
// 都会把它清掉（原版 ScriptPlayer：MEDIA_COMPLETE 置 "stop"，
// 之后任何一次 PLAYER_STATE 事件都会把它改回 playing / pause）。
var state = {
    positionMs: 0,
    playing: false,
    stopped: false,
    rate: 1,
    updatedAt: performance.now()
};

var container = document.getElementById("stage");
hostState.canvas = null;
hostState.context2d = null;
hostState.items = [];
// 上一帧是否还有条目在时间窗内。用来捕捉「最后一个条目离开窗口」
// 这一瞬间，好把画布整幅清空（见 updateItems）。
hostState.hadActiveItem = false;
hostState.elements = [];
hostState.rootElement = null;
hostState.generation = 0;
// 当前正在执行脚本 / 定时器回调 / 触发器回调的条目：$、interval、
// commentTrigger 等全局函数靠它归属元素、定时器与触发器。
hostState.activeItem = null;
// 弹幕快照（Player.commentList）。由 C# 侧推入，resetComments/appendComments 维护。
hostState.commentSnapshot = [];
hostState.nextTriggerId = 0;
// 舞台遮罩（M8 的 Player.setMask）：非空时整块画布裁剪到该元件的形状里。
hostState.stageMaskElement = null;
hostState.visible = true;
hostState.running = false;
hostState.frameHandle = 0;
var compileErrorCount = 0;
var runtimeErrorCount = 0;
// 合成阶段的错误（补间缓动是脚本传入的函数，可能在任意一帧抛错）
// 与脚本执行错误分开计数：两者都不该刷屏，但也不该互相挤掉配额。
var compositeErrorCount = 0;
// 分块传输大脚本时的拼接缓冲（beginItem → appendItemChunk* → endItem）。
hostState.pendingItemJson = "";
// 脚本执行次数计数：保留模式的核心契约是「每条脚本只执行一次」，
// 这个计数器让它成为可断言的量。
hostState.totalRunCount = 0;
hostState.viewportWidth = 0;
hostState.viewportHeight = 0;
hostState.devicePixelRatioValue = 1;
// 脏标记：tick 消费，tween 推进 / seek / setState 之类的变化置位。
hostState.dirty = false;
// 绘制计数器：脏元素才会增加它，用于把「静态元素不重绘」做成可断言的契约。
hostState.paintCount = 0;
// 待擦除的主画布矩形（像素坐标）。元素移动 / 释放 / 隐藏时入队，
// 下一帧合成之前统一擦掉——这就是保留模式下的「清屏」。
hostState.pendingEraseRects = [];

function post(type, values) {
    try {
        if (!window.chrome
            || !window.chrome.webview
            || typeof window.chrome.webview.postMessage !== "function") {
            return;
        }

        var message = { type: type };
        if (values) {
            for (var key in values) {
                if (Object.prototype.hasOwnProperty.call(values, key)) {
                    message[key] = values[key];
                }
            }
        }

        window.chrome.webview.postMessage(JSON.stringify(message));
    } catch (error) {
        // 消息桥不可用时仍让渲染器继续工作。
    }
}

function getErrorMessage(error) {
    if (!error) {
        return "未知错误";
    }

    if (typeof error === "string") {
        return error;
    }

    return error.message || String(error);
}

function reportError(stage, error, itemId) {
    post("error", {
        stage: stage || "unknown",
        itemId: itemId || "",
        message: getErrorMessage(error)
    });
}

function reportCompileError(error, itemId) {
    if (compileErrorCount >= MAX_COMPILE_ERROR_REPORTS) {
        return;
    }

    compileErrorCount++;
    reportError("compile", error, itemId);
}

function reportRuntimeError(error, itemId) {
    if (runtimeErrorCount >= MAX_RUNTIME_ERROR_REPORTS) {
        return;
    }

    runtimeErrorCount++;
    reportError("runtime", error, itemId);
}

// 补间推进 / 合成阶段的上报通道。单独配额，避免一个每帧抛错的
// 坏缓动把编译期错误的配额一起吃掉。
function reportCompositeError(error, itemId) {
    if (compositeErrorCount >= MAX_RUNTIME_ERROR_REPORTS) {
        return;
    }

    compositeErrorCount++;
    reportError("composite", error, itemId);
}

function currentPositionMs() {
    if (!state.playing) {
        return state.positionMs;
    }

    return state.positionMs
        + (performance.now() - state.updatedAt) * state.rate;
}

function requestPause() {
    post("action", { action: "pause" });
    return true;
}

// 播放动作：与 pause / seek / navigate 走同一条宿主→C# 的 action 通道。
// 由 PlayerPage 决定是恢复播放还是被拦截链拦下。
function requestPlay() {
    post("action", { action: "play" });
    return true;
}

function requestSeek(seconds) {
    var value = Number(seconds);
    if (!isFinite(value) || value < 0) {
        return false;
    }

    post("action", { action: "seek", seconds: value });
    return true;
}

function requestNavigate(url) {
    var value = String(url || "");
    if (!value) {
        return false;
    }

    // 目标白名单由 PlayerPage 侧判定，这里只做非空与协议粗筛。
    if (value.indexOf("https://") !== 0 && value.indexOf("http://") !== 0) {
        return false;
    }

    post("action", { action: "navigate", url: value });
    return true;
}

function toFiniteNumber(value, fallback) {
    var number = Number(value);
    return isFinite(number) ? number : fallback;
}

function normalizeColor(value) {
    var color = Math.floor(toFiniteNumber(value, DEFAULT_TEXT_COLOR));
    if (color < 0) {
        color = 0;
    }

    return color & 0xFFFFFF;
}

// ---- M8 脚本运行时（原版 API 面）----
//
// 这里不再有自研的 ctx：脚本拿到的就是原版 M8 的全局名
// （$ / Player / $G / Tween / Utils / ScriptManager / timer /
// interval / clearTimer / trace / tracex / stopExecution /
// foreach / clone / getTimer，见 compileItem）。
//
// 冲突处理：M8 的 `$.createCanvas({width, height})` 与
// `$.createShape({...})` 语义相同（都返回可承载子元件的保留元件），
// 本宿主统一映射到「容器」元素；`$.createText` 映射到 M8 的文本元件。
// 没有对应实现的 createVector / createMatrix / createColorTransform /
// createGlowFilter 等工厂见文件末尾的占位说明。

// 脚本执行期间宿主指向当前条目：全局函数靠它归属元素与定时器。
function currentItem() {
    return hostState.activeItem;
}

// 脚本执行期间处于最前端（$.create* 的 parent 缺省值）。
// 目前恒为 null（= 条目根），保留它是为了后续 $.createCanvas 的
// 「新元件默认挂在当前元件下」语义（阶段 2-4）。
function currentCreateParent() {
    return hostState.activeItem ? hostState.activeItem.createParent : null;
}

export {
    AV_NUMBER_PATTERN,
    DEFAULT_BOUNDS_PADDING,
    DEFAULT_LIFE_TIME_SECONDS,
    DEFAULT_TEXT_COLOR,
    DEFAULT_TEXT_FONT,
    DEFAULT_TEXT_FONTSIZE,
    DEFAULT_TRIGGER_TIMEOUT_MS,
    DIRTY_RECT_PADDING,
    GLOW_PADDING,
    INJECTED_SCRIPT_NAMES,
    LIFE_TIME_UNBOUNDED,
    MAX_AVM1_SCOPE_RETRIES,
    MAX_FRAME_DELTA_MS,
    MAX_ITEM_WINDOW_MS,
    MAX_REPEAT_EXPANSION,
    RESERVED_WORDS,
    SCRIPT_GLOBAL_NAMES,
    container,
    currentCreateParent,
    currentItem,
    currentPositionMs,
    hostState,
    normalizeColor,
    post,
    reportCompileError,
    reportCompositeError,
    reportRuntimeError,
    requestNavigate,
    requestPause,
    requestPlay,
    requestSeek,
    state,
    toFiniteNumber
};
