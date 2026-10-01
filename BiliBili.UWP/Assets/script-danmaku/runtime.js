// 跨条目变量、Utils、定时器、ScriptManager 与脚本作用域。
import {
    ScriptBitmap
} from "./bitmap.js";
import {
    currentItem,
    hostState,
    post,
    reportRuntimeError,
    state,
    toFiniteNumber
} from "./core.js";
import {
    M8Display,
    clonePerspectiveProjection,
    defineHiddenValue
} from "./display.js";
import {
    TweenEasing
} from "./easing.js";
import {
    clearItemElements,
    clearItemTriggers,
    ensureRunning,
    recoverItemFromCallbackError
} from "./lifecycle.js";
import {
    Player
} from "./player.js";
import {
    Tween
} from "./tween.js";

// ---- $G / Global：跨条目共享变量 ----

// 全局变量的「当前代」对象。reset 时换成新对象而不是清空旧对象：
// 上一代脚本仍可能持有 $G 的引用并继续读写（元素还会活若干帧），
// 换成新对象后它们的写入不会污染新一代。
hostState.globalStore = {};

function createGlobalApi() {
    var api = {
        _set: function (key, value) {
            if (key === undefined || key === null) {
                return;
            }

            hostState.globalStore[String(key)] = value;
        },
        _get: function (key) {
            if (key === undefined || key === null) {
                return undefined;
            }

            return hostState.globalStore[String(key)];
        },
        _remove: function (key) {
            delete hostState.globalStore[String(key)];
        }
    };

    // 文档写明 $G 的别名是 `_`，Galgame 示例里两种写法都在用。
    api._ = api._get;
    return api;
}

var Global = createGlobalApi();

// ---- Utils ----

// M8 的 Utils.hue(h)：原版是三段线性插值（ScriptUtils.as:24-50），不是标准
// HSL 色相扇形。分段用的是 > 与 <，端点因此落在 0：hue(0)=0x0000ff、
// hue(120)=0xff0000、hue(240)=0x00ff00 成立，但中间值与本扇形不同
// （例如 hue(60)：原版 0xbf00bf，扇形映射 0xff00ff）。
function hueToColor(value) {
    var hue = toFiniteNumber(value, 0) | 0;
    hue = hue % 360;

    // 三段权重按原版的分段边界算（> 与 <，端点落 0）。
    var redWeight = 0;
    var greenWeight = 0;
    var blueWeight = 0;
    if (hue > 0 && hue < 240) {
        redWeight = 100 - 50 * Math.abs(hue - 120) / 120;
    }

    if (hue > 124 && hue < 360) {
        greenWeight = 100 - 50 * Math.abs(hue - 240) / 120;
    }

    if (hue > 240 && hue <= 360) {
        blueWeight = 100 - 50 * Math.abs(hue - 360) / 120;
    }
    else if (hue + 360 >= 360 && hue + 360 < 480) {
        blueWeight = 100 - 50 * Math.abs(hue + 360 - 360) / 120;
    }

    // int(x) 在 AS3 里朝零截断，所以位拼前用 | 0，不能用 Math.round。
    return ((redWeight * 255 / 100 | 0) << 16)
        | ((greenWeight * 255 / 100 | 0) << 8)
        | (blueWeight * 255 / 100 | 0);
}

// M8 的 Utils.formatTimes(t)：原版是 mm:ss，**分钟不足两位也要补零**，
// 负数原样带负号（ScriptUtils.as:57-66）——不是「分不补零 + 负数钳到 0」。
function formatPlaybackTime(seconds) {
    var value = toFiniteNumber(seconds, 0);
    if (value < 0) {
        return "-" + formatPlaybackTime(-value);
    }

    var rest = Math.floor(value % 60);
    var minutes = Math.floor(value / 60);
    return (minutes < 10 ? "0" : "") + minutes + ":" + ("0" + rest).substr(-2, 2);
}

var Utils = {
    hue: hueToColor,
    // 原版直接位拼、不做范围钳制（ScriptUtils.as:52-55），
    // 超出 0-255 的值按 32 位整数溢出，与 AS3 的 << 一致。
    rgb: function (r, g, b) {
        return (toFiniteNumber(r, 0) << 16)
            | (toFiniteNumber(g, 0) << 8)
            | toFiniteNumber(b, 0);
    },
    formatTimes: formatPlaybackTime,
    distance: function (x1, y1, x2, y2) {
        var dx = toFiniteNumber(x2, 0) - toFiniteNumber(x1, 0);
        var dy = toFiniteNumber(y2, 0) - toFiniteNumber(y1, 0);
        return Math.sqrt(dx * dx + dy * dy);
    },
    // 原版返回整数且不含上界（ScriptUtils.as:116-119）：先插值再 floor。
    rand: function (min, max) {
        var low = toFiniteNumber(min, 0);
        var high = toFiniteNumber(max, 0);
        return Math.floor(low + Math.random() * (high - low));
    },
    // Utils.delay / Utils.interval 是全局 timer / interval 的别名。
    // 注意两者的时钟不同源：delay 走 setTimeout（真实时间），
    // interval 走 Timer（登记在 ScriptManager 上，暂停时被 stop）。
    delay: function (closure, time) {
        return scheduleItemTimeout(closure, time);
    },
    interval: function (closure, time, times) {
        return scheduleItemTimer(closure, time, false, times);
    },
    // Utils.clone / Utils.foreach：原版 ScriptUtils 里就在（ScriptUtils.as:121-139），
    // 注入的 Utils 就是这个类的实例，所以脚本写 Utils.clone(o) 与
    // 裸写 clone(o) 都成立（后者是 CommentScriptFactory 的额外注入）。
    // 宿主此前只注入了裸名，Utils.clone 会掉进 AVM1 兜底变成 undefined。
    clone: function (object) {
        return clone(object);
    },
    foreach: function (loop, callback) {
        return foreach(loop, callback);
    }
};

// ---- timer / interval（登记在条目上，随条目回收统一清理）----
//
// 重点：定时器绝不能活过条目。登记表由 deactivateItem / reset /
// seek 越窗统一清空，脚本不必自己清（M8 用 ScriptManager.clearTimer
// 解决的正是这个问题）。
// 原版 timer()/Utils.delay 走 flash.utils.setTimeout——**真实时间**，
// 不受播放暂停影响（ScriptUtils.as:68-83）。只有间隔式的 Utils.interval
// 才走 Timer + ScriptManager，暂停时被 stateHandler 统一 stop()。
// 所以一次性定时器在这里用宿主时钟 + 浏览器调度，与画面时钟分开；
// 条目回收 / reset / clearTimer(id) 时统一取消。
function scheduleItemTimeout(closure, delayMs) {
    if (typeof closure !== "function") {
        return 0;
    }

    var item = currentItem();
    if (!item) {
        return 0;
    }

    var handle = {
        id: ++nextTimerId,
        ownerItem: item,
        timeoutId: 0,
        stopped: false
    };
    // 原版 delay 的下限是 1ms（ScriptUtils.as:73-76）。
    handle.timeoutId = window.setTimeout(function () {
        if (handle.stopped || item.removed) {
            return;
        }

        try {
            closure();
        } catch (error) {
            if (!recoverItemFromCallbackError(item, error)) {
                reportRuntimeError(error, item.model.id);
            }
        }
    }, Math.max(1, toFiniteNumber(delayMs, 1000)));
    item.realTimers.push(handle);
    return handle.id;
}

function stopRealTimeout(handle) {
    if (!handle || handle.stopped) {
        return;
    }

    handle.stopped = true;
    try {
        window.clearTimeout(handle.timeoutId);
    } catch (error) {
        // 取消失败不影响脚本。
    }
}

function scheduleItemTimer(closure, delayMs, oneShot, times) {
    if (typeof closure !== "function") {
        return 0;
    }

    var item = currentItem();
    if (!item) {
        return 0;
    }

    var period = Math.max(1, toFiniteNumber(delayMs, 1000));
    var timer = {
        id: ++nextTimerId,
        callback: closure,
        intervalMs: period,
        // times: 0 表示无限次；oneShot（timer()）固定为 1 次。
        remaining: oneShot ? 1 : normalizeTimerTimes(times),
        elapsedMs: 0,
        running: true,
        ownerItem: item,
        stop: function () {
            timer.running = false;
        }
    };

    // Flash Timer 的 reset / start（原版 interval 返回的确实是
    // flash.utils.Timer，见 ScriptUtils.as:85-109，脚本可以拿它当 Timer 用）。
    // 用赋值而不是写在字面量里：契约测试按「宿主命令对象里那个 reset 成员」
    // 的源码形态定位宿主命令，这里若出现同样的字面写法会让它找错位置。
    //
    // 注意 stop() 之后定时器会在下一帧被 runItemTimers 摘出登记表，
    // 所以重启必须把它重新挂回去（否则 start() 静默失效）。
    timer.reset = function () {
        restartItemTimer(timer, oneShot, times);
    };
    timer.start = function () {
        if (timer.running) {
            return;
        }

        restartItemTimer(timer, oneShot, times);
    };

    item.scheduledTimers.push(timer);
    hostState.dirty = true;
    ensureRunning();
    return timer;
}

function normalizeTimerTimes(times) {
    if (times === undefined || times === null) {
        return 1;
    }

    if (times <= 0) {
        return Infinity;
    }

    return Math.floor(toFiniteNumber(times, 1));
}

// Flash Timer 的 reset / start：清零计时与剩余次数，重新开始。
// 关键一步是**把定时器重新挂回条目登记表**——stop()（或跑满 times）
// 之后 runItemTimers 会在下一帧把它摘出去，只置 running = true
// 是救不回来的（登记表里已经没有它了）。
function restartItemTimer(timer, oneShot, times) {
    var item = timer.ownerItem;
    if (!item || item.removed) {
        return;
    }

    timer.elapsedMs = 0;
    timer.remaining = oneShot ? 1 : normalizeTimerTimes(times);
    timer.running = true;
    if (item.scheduledTimers.indexOf(timer) < 0) {
        item.scheduledTimers.push(timer);
    }

    hostState.dirty = true;
    ensureRunning();
}

var nextTimerId = 0;

function runItemTimers(item, deltaMs) {
    var timers = item.scheduledTimers;
    if (!timers || timers.length === 0) {
        return;
    }

    for (var index = timers.length - 1; index >= 0; index--) {
        var timer = timers[index];
        if (!timer.running || item.removed) {
            timers.splice(index, 1);
            continue;
        }

        timer.elapsedMs += deltaMs;
        if (timer.elapsedMs < timer.intervalMs) {
            continue;
        }

        timer.elapsedMs = 0;
        timer.remaining--;
        try {
            timer.callback();
        } catch (error) {
            // 回调抛错只停这一个定时器，不影响同条目其它定时器。
            timer.running = false;
            // 若是「读未声明变量」，先按 AVM1 语义补绑并整条重跑
            // （重跑会重建定时器登记表，受 MAX_AVM1_SCOPE_RETRIES 约束）。
            if (!recoverItemFromCallbackError(item, error)) {
                reportRuntimeError(error, item.model.id);
            }

            return;
        }

        if (timer.remaining <= 0) {
            timer.running = false;
            timers.splice(index, 1);
        }
    }
}

function clearItemScheduledTimers(item) {
    if (!item || !item.scheduledTimers) {
        return;
    }

    for (var index = 0; index < item.scheduledTimers.length; index++) {
        item.scheduledTimers[index].running = false;
    }

    item.scheduledTimers = [];
}

function clearItemRealTimers(item) {
    if (!item || !item.realTimers) {
        return;
    }

    for (var index = 0; index < item.realTimers.length; index++) {
        stopRealTimeout(item.realTimers[index]);
    }

    item.realTimers = [];
}

function clearTimerById(id) {
    var found = false;
    var itemList = allItems();
    for (var itemIndex = 0; itemIndex < itemList.length; itemIndex++) {
        var item = itemList[itemIndex];
        for (var realIndex = item.realTimers.length - 1; realIndex >= 0; realIndex--) {
            if (item.realTimers[realIndex].id === id) {
                stopRealTimeout(item.realTimers[realIndex]);
                item.realTimers.splice(realIndex, 1);
                found = true;
            }
        }

        for (var index = item.scheduledTimers.length - 1; index >= 0; index--) {
            if (item.scheduledTimers[index].id === id) {
                item.scheduledTimers[index].running = false;
                item.scheduledTimers.splice(index, 1);
                found = true;
            }
        }
    }

    return found;
}

// 脚本可调用 clearTimer(timer) / clearTimer(id)：两种形态都接受。
function clearTimer(target) {
    if (target && typeof target === "object" && typeof target.stop === "function") {
        target.stop();
        return;
    }

    if (target === undefined || target === null) {
        clearItemScheduledTimers(currentItem());
        return;
    }

    clearTimerById(toFiniteNumber(target, -1));
}

function allItems() {
    return hostState.items;
}

// ---- ScriptManager ----

var ScriptManager = {
    clearTimer: function () {
        clearItemScheduledTimers(currentItem());
    },
    clearEl: function () {
        var item = currentItem();
        if (item) {
            clearItemElements(item);
        }
    },
    clearTrigger: function () {
        // 清掉当前条目上所有 commentTrigger / keyTrigger 登记。
        var item = currentItem();
        if (item) {
            clearItemTriggers(item);
        }
    },
    // M8 的 popEl(el)：原版是「从管理表移除 + 停掉 motionManager + 从显示列表摘离」
    // —— ScriptManager.as 的 popEl 就是 delete / motionManager.stop() / m.remove()，
    // 而 CommentCanvas.remove() 正是 parent.removeChild(this)。
    // 宿主对应三件事：不再被 clearEl 清理、停掉元素上的补间句柄、从渲染树摘除。
    //
    // 这里踩过一次弯路，记下来免得再犯：早先把 popEl 实现成 el.remove() 时，
    // 真实作品（entry_08 的 Akari）整棵树被摘下、一个像素都画不出来
    // （实测 topLevel=0、26797 个元件全在树下），于是误判为「原版 popEl 不摘离」，
    // 把语义改成了「只豁免清理」。真正的根因不在 popEl，而在宿主**缺 $.root**：
    // Akari 的 Akari.root() 先判 `$.hasOwnProperty("root") && $.root`——反编译里
    // root 是 ScriptDisplay 的 getter（:119-122，返回注释层 _layer），AS3 里
    // getter 属于实例 trait、hasOwnProperty 为真，所以原版直接返回 $.root，
    // 根本不会 createCanvas + popEl；宿主原先没有这个属性（M8Display 的
    // hasOwnProperty 是严格 JS own-property 检查），Akari 才掉进 else 分支，
    // 把作品挂到那个会被 popEl 处理的容器上。
    // 补上 $.root 之后（见 M8Display 的 root getter），popEl 按原版摘离也已验证
    // 不影响真实作品（real-m8-scripts 5/5）。
    popEl: function (element) {
        if (!element) {
            return element;
        }

        defineHiddenValue(element, "exemptFromClear", true);
        if (typeof element.remove === "function") {
            element.remove();
        }

        return element;
    }
};

// ---- 全局函数：trace / foreach / clone / getTimer / stopExecution ----

// getTimer 的基准：脚本开始运行的那一刻（M8 里是「启动播放器到现在」）。
var scriptClockOrigin = performance.now();

// 脚本 trace 的历史缓冲：M8 的 clear() 清的就是它（原版 EventBus.clear()
// 派发 "clearLog" 清空 IDE 输出区，见 org/lala/event/EventBus.as:219-222）。
var scriptTraceBuffer = [];
var MAX_TRACE_BUFFER_ENTRIES = 500;

function writeTrace(values) {                var parts = [];
    for (var index = 0; index < values.length; index++) {
        parts.push(stringifyTraceValue(values[index]));
    }

    var text = parts.join(" ");
    scriptTraceBuffer.push(text);
    if (scriptTraceBuffer.length > MAX_TRACE_BUFFER_ENTRIES) {
        scriptTraceBuffer.shift();
    }

    try {
        if (window.console && typeof window.console.log === "function") {
            window.console.log("[script-danmaku] " + text);
        }
    } catch (error) {
        // 回显失败不影响脚本执行。
    }

    return text;
}

function stringifyTraceValue(value) {
    if (value === undefined) {
        return "undefined";
    }

    if (value === null) {
        return "null";
    }

    if (typeof value === "object") {
        try {
            return JSON.stringify(value);
        } catch (error) {
            return String(value);
        }
    }

    return String(value);
}

function trace() {
    return writeTrace(arguments);
}

// M8 的 tracex 只接受一个字符串参数，语义与 trace 相同。
// 注意：原版 CommentScriptFactory 只把 trace 注入全局，没有 tracex
// （globals 字面量见 CommentScriptFactory.as:96-118）；这里保留 tracex
// 是兼容早期样本里的调用，属超集。
function tracex(text) {
    return writeTrace([text]);
}

// M8 的 foreach(loop:Object, f:Function)：回调签名是 (key, value)。
//
// **只遍历真正的对象**：原始值（string / number / boolean）一律不遍历。
// 这不是可选的洁癖，是让真实脚本能终止的必要条件——entry_08 的
// Factory.clone 是 `foreach(object, function(key, object){ ...
// clone(object) ... })`，递归到字符串时若去枚举它的字符下标，
// clone("a") 会调 clone("a") 无限递归（实测：Maximum call stack size
// exceeded）。M8 文档把参数标成 Object，Flash 的 for-in 对原始值
// 也不产生可枚举属性，因此「原始值 → 零次迭代」才是正确语义。
function foreach(loop, callback) {
    if (typeof callback !== "function" || loop === undefined || loop === null) {
        return;
    }

    if (typeof loop !== "object" && typeof loop !== "function") {
        return;
    }

    if (Array.isArray(loop)) {
        for (var index = 0; index < loop.length; index++) {
            callback(index, loop[index]);
        }

        return;
    }

    for (var key in loop) {
        if (Object.prototype.hasOwnProperty.call(loop, key)) {
            callback(key, loop[key]);
        }
    }
}

// M8 的 clone 原版是 ByteArray 深拷贝（ScriptUtils.as:121-130）：嵌套对象
// 各自独立，函数不参与复制（AMF 不支持函数）。早期版本按文档做成了浅拷贝。
function clone(object) {
    if (object === null || typeof object !== "object") {
        return object;
    }

    var projectionCopy = clonePerspectiveProjection(object);
    if (projectionCopy) return projectionCopy;

    if (Array.isArray(object)) {
        var arrayCopy = [];
        for (var arrayIndex = 0; arrayIndex < object.length; arrayIndex++) {
            arrayCopy.push(clone(object[arrayIndex]));
        }

        return arrayCopy;
    }

    var result = {};
    for (var key in object) {
        if (Object.prototype.hasOwnProperty.call(object, key)
            && typeof object[key] !== "function") {
            result[key] = clone(object[key]);
        }
    }

    return result;
}

function getTimer() {
    return Math.max(0, performance.now() - scriptClockOrigin);
}

// ---- AS2/AS3 的全局转换函数 ----

// int()：AVM1 里是**朝零截断**到 32 位有符号整数
// （int(-1.5) === -1、int(NaN) === 0、int("12abc") === 12）。
// 宿主此前没有注入它，脚本一用就掉进 AVM1 兜底被声明成 undefined。
function asInt(value) {
    var number = Number(value);
    if (!isFinite(number)) {
        // NaN / ±Infinity 都按 AS3 的 Number.int 给 0。
        return 0;
    }

    return Math.trunc(number) | 0;
}

// uint()：同 int()，但按 32 位**无符号**取值（uint(-1) === 4294967295）。
function asUint(value) {
    return asInt(value) >>> 0;
}

function stopExecution() {
    // M8 用 stopExecution() 终止当前脚本：抛出内部信号，
    // 由 activateItem 捕获后按「脚本主动终止」处理（不计为错误）。
    // 注意：原版注入表里没有 stopExecution（CommentScriptFactory.as:96-118），
    // 但真实脚本 entry_08 的 Akari 用它做幂等守卫，所以必须保留。
    throw STOP_EXECUTION_SIGNAL;
}

var STOP_EXECUTION_SIGNAL = { stopExecution: true };

// M8 的 clear()：清空脚本输出区。原版 EventBus.clear() 派发 "clearLog"
// （org/lala/event/EventBus.as:219-222）；宿主没有 IDE 面板，因此清本地
// trace 缓冲，并把消息透给 C# 侧按需消费。
function clear() {
    scriptTraceBuffer.length = 0;
    post("clearLog", {});
}

// M8 的 load(library, callback)：按需加载外部脚本库
// （原版 CommentScriptFactory.as:142-156）。原版把
// static-s.bilibili.com/playerLibrary/<lib>_2.swf 拉进 VM 执行；
// WebView2 不能执行 SWF 扩展库，所以命中内建库/已加载表就按原版语义
// 直接回调，其余记一条 trace 后同样回调——不能装死，脚本靠这个回调继续。
var BUILTIN_SCRIPT_LIBRARIES = { libBitmap: true };
var loadedScriptLibraries = {};

function load(library, callback) {
    var name = String(library);
    if (BUILTIN_SCRIPT_LIBRARIES[name] === true || loadedScriptLibraries[name] === true) {
        if (typeof callback === "function") {
            callback();
        }

        return;
    }

    loadedScriptLibraries[name] = true;
    writeTrace(["load(" + name + ") 未实现：宿主不能执行 SWF 扩展库，按原版语义直接回调"]);
    if (typeof callback === "function") {
        callback();
    }
}

// ---- 脚本作用域 ----

// 命中 setState / resize 等后用 tick(now) 重绘一次也没有额外代价。
function createScriptScope(item) {
    // 把该条目设为「当前条目」：脚本正文里的 $ / interval / trace
    // 都靠它归属元素与定时器。
    item.activeNow = state.positionMs;
    item.createParent = null;
    item.lastAdvanceMs = -1;
    // 顺序必须与 compileItem 的形参逐字对应。
    // 注意 `Global` 与 `$G` 是同一个对象（M8 文档里的两个名字，
    // 真实脚本两种都在用：样本里 Global 出现 90 次、$G 若干次）；
    // `Display` 与 `$` 同理，都是 ScriptDisplay 的命名空间。
    return [
        M8Display,
        M8Display,
        Player,
        Global,
        Global,
        Tween,
        TweenEasing,
        Utils,
        ScriptManager,
        function timer(closure, delay) {
            // 原版 timer() 就是 Utils.delay，走 setTimeout（真实时间）。
            return scheduleItemTimeout(closure, delay);
        },
        function interval(closure, delay, times) {
            return scheduleItemTimer(closure, delay, false, times);
        },
        clearTimer,
        clearTimer,
        clear,
        load,
        trace,
        tracex,
        stopExecution,
        foreach,
        clone,
        getTimer,
        window.parseInt,
        window.parseFloat,
        window.Math,
        window.String,
        ScriptBitmap,
        // AS2/AS3 的全局转换函数。int/uint 必须自己实现：
        // JS 只有 Number/Boolean/isNaN 是现成的（直接透传 window 上的），
        // 而 int()/uint() 是 AVM1 特有的 32 位截断转换。
        asInt,
        asUint,
        window.Number,
        window.Boolean,
        window.isNaN
    ];
}

export {
    Global,
    STOP_EXECUTION_SIGNAL,
    clearItemRealTimers,
    clearItemScheduledTimers,
    createScriptScope,
    runItemTimers,
    writeTrace
};
