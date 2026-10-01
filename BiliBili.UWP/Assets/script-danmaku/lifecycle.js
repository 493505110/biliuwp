// 条目编译、生命周期、播放时钟同步与帧调度。
import {
    advanceParticleElements
} from "./bitmap.js";
import {
    INJECTED_SCRIPT_NAMES,
    LIFE_TIME_UNBOUNDED,
    MAX_AVM1_SCOPE_RETRIES,
    MAX_FRAME_DELTA_MS,
    MAX_ITEM_WINDOW_MS,
    RESERVED_WORDS,
    SCRIPT_GLOBAL_NAMES,
    consumeStageFrame,
    currentPositionMs,
    hostState,
    post,
    reportCompileError,
    reportCompositeError,
    reportRuntimeError,
    resetStageClock,
    state
} from "./core.js";
import {
    attachElement,
    detachElement,
    dispatchItemEnterFrame,
    releaseElementCaches
} from "./display.js";
import {
    clearSurface,
    paintDirtyElements
} from "./renderer.js";
import {
    STOP_EXECUTION_SIGNAL,
    clearItemRealTimers,
    clearItemScheduledTimers,
    createScriptScope,
    runItemTimers
} from "./runtime.js";
import {
    advanceElementMotion,
    advanceItemHandles,
    isMotionHandleDriven,
    resolveElementLifeTimeMs
} from "./tween.js";

// ---- 条目生命周期 ----

function isInWindow(item, now) {
    return now >= item.startMs && now <= item.endMs;
}

function registerItemElement(item, element) {
    element.ownerItem = item;
    // 寿命初值 = 条目窗口剩余时间（未声明 tween 时就是这个值）。
    // 声明了 tween 的元素再由 tween 的 lifeTime 收紧，
    // 取 min(声明值, 窗口剩余)，见 applyElementLifeTime。
    element.declaredLifeTimeMs = LIFE_TIME_UNBOUNDED;
    element.lifeTimeMs = resolveElementLifeTimeMs(item, LIFE_TIME_UNBOUNDED);

    item.elements.push(element);
    // parent 由创建参数决定；未指定时挂到条目 Canvas（M8 的默认父元件）。
    if (element.treeParent === null) {
        attachElement(element, element.createParent || null);
    }

    element.createParent = null;
    return element;
}

// 条目级内容重置：ScriptManager.clearEl / clearTimer / clearTrigger 用。
// clearEl 只摘元素（元素的 tween 与定时器随之失效），不重置整个条目。
function clearItemElements(item) {
    for (var index = item.elements.length - 1; index >= 0; index--) {
        // 被 ScriptManager.popEl() 标记过的元件不清（M8 语义：
        // popEl 就是「从自动清理表里弹出」）。
        if (item.elements[index].exemptFromClear) {
            continue;
        }

        releaseItemElement(item, index);
    }

    hostState.dirty = true;
}

function clearItemTimers(item) {
    clearItemScheduledTimers(item);
    clearItemRealTimers(item);
    item.tweenHandles = [];
}

function clearItemTriggers(item) {
    item.triggers = [];
}

function activateItem(item, now) {
    item.activated = true;
    item.removed = false;
    item.failed = false;
    // 重跑脚本前先把上一次的定时器 / 句柄清掉，避免重建后两份并存。
    clearItemTimers(item);
    clearItemTriggers(item);
    // 脚本在这里、也只在这里执行一次。之后每帧只推进 tween。
    // 计数按「一次激活」算一次，重试用的是同一次激活里的重跑（见下）。
    item.runCount++;
    hostState.totalRunCount++;
    runItemScriptWithAvm1Scope(item);
}

// 执行脚本正文；遇到「读未声明变量」抛 ReferenceError 时按 AVM1
// （Flash 8 / AS2）的宽容语义补救：**把那个标识符声明到脚本全局对象上
// 并置为 undefined**，然后重跑本条脚本。
//
// 为什么要这一层：M8 脚本是 AS2 时代写给 AVM1 的，那里读未声明变量得到
// undefined、不抛错。真实样本里 `update:function(time){if(time <
// startTime)...}` 的 startTime / duration 就是**翻译时丢掉的 var**——
// AVM1 下它们读成 undefined（比较恒为 false，正文照跑），JS 下直接
// ReferenceError，整条脚本停摆。
//
// 为什么修在全局对象上而不是补脚本形参：这些闭包可能**不是本条脚本创建的**。
// entry_10 抛错的那处 `update` 定义在 entry_08 的 Akari 库里，
// 它的作用域链早在 entry_08 执行时就定死了——给 entry_10 的编译结果
// 加形参救不了它。标识符解析对「未绑定名」是每次访问都回落到全局对象上查的，
// 所以只要在全局对象上把名字补出来，**已经建好的闭包也会立刻恢复正常**。
//
// 安全性：只声明「已被 ReferenceError 证明不存在」的名字，因此绝不会遮蔽
// 任何真实存在的名字——脚本自己声明的、或像 entry_10 那样用
// `Factory.extend(this, …)` 导出到全局对象上的，都照旧解析。
// 不做任何静态分析，也不动作用域链（不用 with）。
function runItemScriptWithAvm1Scope(item) {
    for (var attempt = 0; attempt <= MAX_AVM1_SCOPE_RETRIES; attempt++) {
        try {
            var scriptArgs = createScriptScope(item);
            hostState.activeItem = item;
            item.run.apply(null, scriptArgs);
            return;
        } catch (error) {
            if (error === STOP_EXECUTION_SIGNAL) {
                // 脚本自己调了 stopExecution()：正常终止，不计为错误。
                item.removed = false;
                return;
            }

            if (!declareAvm1GlobalIfUndeclared(error)) {
                // 不是「未声明变量」这类错误，或已经修不动了：按脚本自身
                // 的失败处理（停用该条目，不影响同帧其它条目）。
                item.failed = true;
                reportRuntimeError(error, item.model.id);
                return;
            }

            // 上一次跑出来的元件、定时器、触发器全部作废：脚本会被完整
            // 重跑一遍，留着会变成两份（与 seek 重建同一条清理路径）。
            releaseItemForRerun(item);
        } finally {
            hostState.activeItem = null;
            // clearEl / clearTimer 之类的清理要发生在脚本执行之后、
            // 下一帧推进之前，否则被清掉的元素还会被推进一帧。
            item.createParent = null;
        }
    }
}

// 已按 AVM1 语义补出来的全局名（跨条目共享：同一个名字只需补一次，
// 补完对所有条目的闭包都生效）。
var avm1DeclaredGlobals = {};

// 从错误里认出「读未声明变量」，把该名字声明到全局对象上。
// 返回 true 表示已经补救（调用方应清理并重跑）。
function declareAvm1GlobalIfUndeclared(error) {
    var missingName = readUndeclaredIdentifier(error);
    if (missingName === null || INJECTED_SCRIPT_NAMES[missingName]) {
        return false;
    }

    if (avm1DeclaredGlobals[missingName]
        || Object.prototype.hasOwnProperty.call(window, missingName)) {
        // 已经补过了／其实存在：说明不是「未声明」这一个原因，别再兜圈子。
        return false;
    }

    avm1DeclaredGlobals[missingName] = true;
    // 声明在脚本全局对象上，值与 AVM1 一致：undefined。
    window[missingName] = undefined;
    return true;
}

// 定时器 / 触发器回调里抛出的错误：与脚本正文走同一套 AVM1 补救。
//
// 为什么回调也要管：entry_10 的 `startTime` 不是正文里读的，而是正文建立的
// interval 回调里读的（`composition.update(Player.time)`）。只补正文的话
// 正文跑过了、回调一触发还是 ReferenceError。
//
// 补救方式只能是「补绑 → 重编译 → 整条脚本重跑」：闭包是在重跑时新建的，
// 新闭包才会带上补绑的形参。返回 true 表示已经补救（调用方应停止处理该错误）。
function recoverItemFromCallbackError(item, error) {
    if (!declareAvm1GlobalIfUndeclared(error)) {
        return false;
    }

    // 补完全局还不够：抛错的那个回调所属的定时器已经停了、
    // 半截状态也要清掉，所以把本条脚本完整重跑一遍。
    releaseItemForRerun(item);
    runItemScriptWithAvm1Scope(item);
    return true;
}

// 从 ReferenceError 里取出未声明的标识符名（V8 / JSC / SpiderMonkey 的文案）。
function readUndeclaredIdentifier(error) {
    if (!error || !(error instanceof ReferenceError)) {
        return null;
    }

    var message = String(error.message || "");
    var match = /^(?:can't find variable: |)?([A-Za-z_$][A-Za-z0-9_$]*) is not defined/.exec(message)
        || /^([A-Za-z_$][A-Za-z0-9_$]*) is not defined/.exec(message)
        || /^ReferenceError: ([A-Za-z_$][A-Za-z0-9_$]*) is not defined/.exec(message);
    if (!match) {
        return null;
    }

    // 保留字不能当形参，直接放弃（脚本用保留字当变量名本身就是语法错误）。
    return RESERVED_WORDS[match[1]] ? null : match[1];
}

// 重跑前的清理：与 deactivateItem 同一条释放路径，但不改 activated 状态。
function releaseItemForRerun(item) {
    clearItemTimers(item);
    clearItemTriggers(item);
    item.frameListeners = [];
    for (var index = item.elements.length - 1; index >= 0; index--) {
        releaseItemElement(item, index);
    }

    item.lastAdvanceMs = -1;
}

// 条目级失败：同一处只上报一次，之后逐帧跳过，避免每帧刷屏。
function failItem(item, error) {
    if (item.failed) {
        return;
    }

    item.failed = true;
    reportCompositeError(error, item.model.id);
}

function releaseItemElement(item, index) {
    var element = item.elements[index];
    // 到期摘除并释放离屏缓存，脚本不负责清理。
    // detachElement 会把它的主画布矩形入队，本帧擦掉残影。
    // 顺序不能反：markElementMoved 见到 element.expired 为真会直接
    // 返回，先标 expired 就入不了队，元素最后一帧的像素会永久
    // 留在画布上（条目窗口结束时尤其明显）。
    detachElement(element);
    element.expired = true;
    item.elements.splice(index, 1);
}

function deactivateItem(item) {
    item.activated = false;
    item.removed = true;
    item.frameListeners = [];
    // 定时器必须随条目一起清掉：M8 用 ScriptManager.clearTimer 解决的
    // 正是「条目销毁后 interval 还在跑」——这里由宿主兜底，脚本不必自己清。
    clearItemTimers(item);
    clearItemTriggers(item);
    for (var index = item.elements.length - 1; index >= 0; index--) {
        releaseItemElement(item, index);
    }
}

function advanceItem(item, now, logicalFrame) {
    var elapsed = now - item.startMs;
    // 粒子元件（Bitmap.createParticle）按帧重绘自己那块离屏画布。
    if (logicalFrame) {
        advanceParticleElements(item);
    }

    // 本帧推进了多少「播放时间」：句柄自己的时间轴与元素寿命都按它走，
    // 这样暂停时两者都停（与 M8 的「暂停不推进效果」一致）。
    var delta = elapsed - item.lastAdvanceMs;
    if (delta < 0 || delta > MAX_FRAME_DELTA_MS) {
        delta = 0;
    }

    item.lastAdvanceMs = elapsed;

    // 脚本执行期间可能又注册定时器 / 创建元素，所以先把这一帧的
    // 定时器到期项跑掉，再推进补间。
    hostState.activeItem = item;
    item.activeNow = now;
    try {
        if (logicalFrame) {
            runItemTimers(item);
        }
    } catch (error) {
        failItem(item, error);
    } finally {
        hostState.activeItem = null;
    }

    // Flash 的 enterFrame 派发：Akari 的整幅画面更新挂在这里，
    // 必须早于下面「推进补间 / 重绘脏元素」，否则本帧画的是旧状态。
    if (logicalFrame) {
        dispatchItemEnterFrame(item);
    }

    try {
        advanceItemHandles(item, delta);
    } catch (error) {
        failItem(item, error);
    }

    for (var index = item.elements.length - 1; index >= 0; index--) {
        var element = item.elements[index];
        if (element.expired) {
            continue;
        }

        // 被 Tween.* 句柄驱动的元素由句柄写回，不再按条目进度插值：
        // 否则两套时间轴会互相覆盖（先句柄后条目，句柄结果当场被冲掉）。
        // 单个元素出错不拖垮整条条目，更不拖垮整帧：
        // 只把该元素标成已失败，后续帧不再推进它。
        if (element.hasTween && !isMotionHandleDriven(element)) {
            try {
                advanceElementMotion(element, elapsed);
            } catch (error) {
                element.failed = true;
                failItem(item, error);
            }
        }

        if (elapsed >= element.lifeTimeMs) {
            releaseItemElement(item, index);
        }
    }

}

function updateItems(now, logicalFrame) {
    var anyActive = false;
    for (var index = 0; index < hostState.items.length; index++) {
        var item = hostState.items[index];
        if (isInWindow(item, now)) {
            anyActive = true;
            if (item.compileFailed) {
                continue;
            }

            if (!item.activated) {
                activateItem(item, now);
            } else {
                advanceItem(item, now, logicalFrame);
            }
        } else if (item.activated) {
            deactivateItem(item);
        }
    }

    // 从「还有条目在窗口内」到「一条都不剩」的那一帧，整幅清空一次。
    // 元素级擦除只覆盖还登记在条目名下的元素；脚本自己摘出去
    // （el.remove()）或重新挂到别处的像素不在名下，靠元素擦除
    // 永远清不掉，会在作品结束后永久留在画布上。原版 M8 也是
    // 条目结束即整层消失，所以这里按「画面作废」处理。
    if (!anyActive && hostState.hadActiveItem) {
        clearSurface();
        hostState.dirty = true;
    }

    hostState.hadActiveItem = anyActive;

    return anyActive;
}

// seek 后按新位置重算插值：已激活且在窗口内的条目只让 tween 重新插值，
// 不重跑脚本；元素已经到期被摘除的（例如向后拖过再拖回来），
// 按条目进度重算补间后重建元素——脚本仍然不重跑，也不重复执行。
function resetItemsForSeek(now) {
    for (var index = 0; index < hostState.items.length; index++) {
        var item = hostState.items[index];
        if (!isInWindow(item, now)) {
            if (item.activated) {
                deactivateItem(item);
            }

            continue;
        }

        if (!item.activated) {
            continue;
        }

        // 条目中途失败过（脚本或缓动抛错）就不再重建，
        // 否则 seek 会把已经停用的坏条目重新拉活。
        if (item.failed) {
            continue;
        }

        rebuildItemElementsForSeek(item, now);

        // 重建后句柄时间轴要跟着新位置走：补间是按「播放进度」
        // 重算的，句柄留在旧时间上会让两套时间轴立刻对不上。
        item.lastAdvanceMs = -1;
        for (var handleIndex = 0; handleIndex < item.tweenHandles.length; handleIndex++) {
            var handle = item.tweenHandles[handleIndex];
            if (handle.state !== "playing") {
                continue;
            }

            handle.timeMs = handle.durationMs > 0
                ? Math.min(handle.durationMs, Math.max(0, now - item.startMs))
                : 0;
        }

        for (var elementIndex = 0; elementIndex < item.elements.length; elementIndex++) {
            var element = item.elements[elementIndex];
            if (element.motion) {
                element.motion.lastElapsedMs = -1;
            }
        }
    }

    hostState.dirty = true;
}

// 按新位置重建该条目的元素：清掉旧元素（含它们的残影），
// 再用当前播放位置重跑一次脚本——脚本只会被"重建"这一次，
// 之后仍走 tween 插值。不重跑脚本就无法知道脚本建了哪些元素、
// 建在哪个坐标上，这是元素已被释放后唯一正确的做法。
function rebuildItemElementsForSeek(item, now) {
    var elapsed = now - item.startMs;
    if (elapsed < 0) {
        elapsed = 0;
    }

    var hasLiveElement = false;
    for (var index = 0; index < item.elements.length; index++) {
        var element = item.elements[index];
        if (!element.expired && elapsed < element.lifeTimeMs) {
            hasLiveElement = true;
            break;
        }
    }

    if (hasLiveElement) {
        return;
    }

    deactivateItem(item);
    activateItem(item, now);
}

// 向后 seek 越过条目窗口时，窗口内的元素会被整批释放；
// 再拖回来必须重建。判据与 resetItemsForSeek 一致。
function isItemWindowReentered(item, now) {
    return isInWindow(item, now) && item.activated && !item.failed;
}

function clearAllItems() {
    for (var index = 0; index < hostState.items.length; index++) {
        var item = hostState.items[index];
        item.activated = false;
        item.removed = true;
        // 整批作废同样要清定时器与句柄：条目被丢弃后它们不能再跑。
        clearItemTimers(item);
        clearItemTriggers(item);
        item.frameListeners = [];
        for (var elementIndex = 0; elementIndex < item.elements.length; elementIndex++) {
            releaseElementCaches(item.elements[elementIndex]);
        }

        item.elements = [];
    }

    hostState.items = [];
    // reset 时清空 $G：M8 的全局变量只在一次播放/一批脚本内共享。
    hostState.globalStore = {};
    // 遮罩元件随整批条目一起作废，遮罩本身也要摘掉，否则下一批
    // 弹幕会被上一批的遮罩裁掉。
    hostState.stageMaskElement = null;
    hostState.rootElement.childList = [];
    hostState.rootElement.composite = null;
    hostState.elements = [];
    resetStageClock(false);
    hostState.stagePlayingTimeMs = 0;
}

// 返回「是否存在处于时间窗内的条目」。窗口内没有条目且未播放时自停。
function tick(now, logicalFrame) {
    if (!hostState.context2d) {
        return false;
    }

    var anyActive = updateItems(now, logicalFrame);

    // 隐藏状态下合成步骤必须直接跳过并保持画布空白，
    // 否则关闭弹幕总开关后紧跟的 setState / resize 会把脏元素合成回屏幕。
    if (!hostState.visible) {
        clearSurface();
        hostState.dirty = false;
        return false;
    }

    if (hostState.dirty) {
        hostState.dirty = false;
        paintDirtyElements();
    }

    return anyActive;
}

// 「还有东西要动吗」：窗口内的条目里有登记在册的定时器
// （M8 的 interval —— 逐帧回调只有这一条路径）、有未跑完的
// Tween.* 句柄，或有元素补间尚未跑完。
// 播放中不看这个——播放本身就要持续出帧。
// duration 缺省（无界窗口）下条目会长时间停在窗口内，只看
// 「窗口内有没有条目」会让暂停后的帧循环一直空转。
function hasPendingAnimation(now) {
    for (var index = 0; index < hostState.items.length; index++) {
        var item = hostState.items[index];
        if (!isInWindow(item, now)) {
            continue;
        }

        // 还有在跑的定时器（interval(…, 0) 这类无界回调）就必须继续出帧，
        // 否则暂停后定时器会被自停判据「冻住」。
        if (item.scheduledTimers.length > 0) {
            return true;
        }
        if (item.frameListeners.some(function (entry) { return !entry.element.expired; })) {
            return true;
        }

        var handleIndex;
        for (handleIndex = 0; handleIndex < item.tweenHandles.length; handleIndex++) {
            if (item.tweenHandles[handleIndex].state === "playing") {
                return true;
            }
        }

        var elapsed = now - item.startMs;
        for (var elementIndex = 0; elementIndex < item.elements.length; elementIndex++) {
            var element = item.elements[elementIndex];
            if (element.expired || !element.hasTween || !element.motion) {
                continue;
            }

            // 被句柄驱动的补间由句柄判定（上面已覆盖），这里只看声明式补间。
            if (isMotionHandleDriven(element)) {
                continue;
            }

            if (elapsed < element.motion.totalMs) {
                return true;
            }
        }
    }

    return false;
}

function frame() {
    if (!hostState.running) {
        return;
    }

    // 调度链必须与「本帧是否出错」解耦：缓动是脚本传入的函数，
    // 它抛错时若让异常从 rAF 回调逃逸，running 会停在 true 而
    // 实际已无排队回调，帧循环永久冻结且 ensureRunning 救不回来。
    try {
        var now = currentPositionMs();
        tick(now, consumeStageFrame());

        // 自停判据不能只看「窗口内有没有条目」：无界窗口（duration
        // 缺省）下条目会长时间停在窗口内，暂停后帧循环会一直空转。
        // 改成「不在播放且没有待推进的补间/逐帧回调」才停。
        // 自停必须在这里决定并直接返回：若在 tick 内置 running=false
        // 却仍无条件续帧，恢复播放时会与已排队的回调形成两条并行帧链。
        if (!state.playing && !hasPendingAnimation(now) && !hostState.dirty) {
            hostState.running = false;
            hostState.frameHandle = 0;
            return;
        }
    } catch (error) {
        reportCompositeError(error, "");
    } finally {
        if (hostState.running) {
            hostState.frameHandle = window.requestAnimationFrame(frame);
        }
    }
}

function ensureRunning() {
    if (hostState.running || !hostState.visible) {
        return;
    }

    hostState.running = true;
    resetStageClock(false);
    hostState.frameHandle = window.requestAnimationFrame(frame);
}

function stopRunning() {
    resetStageClock(true);
    hostState.running = false;
    if (hostState.frameHandle) {
        window.cancelAnimationFrame(hostState.frameHandle);
        hostState.frameHandle = 0;
    }

    clearSurface();
    hostState.dirty = true;
}

function setState(positionSeconds, playing, rate) {
    if (!!playing !== state.playing) {
        resetStageClock(true);
    }
    state.positionMs = Math.max(0, Number(positionSeconds) || 0) * 1000;
    state.playing = !!playing;
    state.stopped = false;
    state.rate = Number(rate) > 0 ? Number(rate) : 1;
    state.updatedAt = performance.now();

    if (state.playing) {
        ensureRunning();
    } else {
        // 暂停时按当前播放位置推进一次插值并重绘，让画面停在脚本进度上
        // （不重跑脚本，也不引入任何基于逐帧回调的机制）。
        tick(currentPositionMs());
    }
}

// 播放结束：只置停止态并出最后一帧，**不动元素可见性**。
//
// 原版 ScriptManager.playerCompleteHandler 会把所有元件置 visible=false，
// 但那个「所有元件」是它自己的全局登记表；宿主的元素登记表是**按条目**
// 分代的，全局失效会跨条目误伤（host 的 reset 早已把上一代作废）。
// 因此这里只保证 Player.state 读到 "stop"（对脚本可观测的那一面），
// 画面收尾交给各条目自己的 lifeTime / 窗口。这是刻意记录的偏离。
function setStopped(positionSeconds, rate) {
    resetStageClock(true);
    state.positionMs = Math.max(0, Number(positionSeconds) || 0) * 1000;
    state.playing = false;
    state.stopped = true;
    state.rate = Number(rate) > 0 ? Number(rate) : 1;
    state.updatedAt = performance.now();
    tick(currentPositionMs());
}

function seekTo(positionSeconds, playing, rate) {
    resetStageClock(true);
    state.positionMs = Math.max(0, Number(positionSeconds) || 0) * 1000;
    state.playing = !!playing;
    state.stopped = false;
    state.rate = Number(rate) > 0 ? Number(rate) : 1;
    state.updatedAt = performance.now();

    var now = currentPositionMs();
    resetItemsForSeek(now);
    if (state.playing) {
        ensureRunning();
    } else {
        tick(now);
    }
}

function compileItem(model) {
    var code = model && typeof model.code === "string" ? model.code : "";
    if (!code) {
        throw new Error("脚本内容为空");
    }

    // TypeScript 支持见设计文档 §2：此处只接受已转译的 JS。
    // lang == "ts" 时需先经宿主内嵌 tsc 转译，当前阶段尚未接入。
    var lang = (model.lang || "js").toLowerCase();
    if (lang !== "js") {
        throw new Error("暂不支持的语言：" + lang + "（TS 转译尚未接入）");
    }

    // 整个宿主里只有这一处动态编译，且每条脚本只编译一次。
    // 脚本环境直接提供 M8 的全局名（见 SCRIPT_GLOBAL_NAMES），不再注入自研的 ctx。
    // 用参数注入而不是给 window 挂属性：脚本里对 $ / Tween 的赋值
    // 只影响它自己那一次执行，不会污染其它条目。
    //
    // 形参名与 createScriptScope() 的实参表必须逐字同序。这两张表曾经各自硬编码：
    // 只往实参表加名字而漏了这里，整表就会错位——foreach 收到 trace、
    // getTimer 收到 stopExecution，真实脚本静默失败或中途被终止。
    // 现在形参一律从 SCRIPT_GLOBAL_NAMES 展开生成，全宿主只有这一处真源。
    return new Function(...SCRIPT_GLOBAL_NAMES, code);
}

function addItem(model, itemGeneration) {
    var startMs = Math.max(0, Number(model.stime) || 0) * 1000;
    var durationSeconds = Number(model.duration);
    if (!isFinite(durationSeconds) || durationSeconds <= 0) {
        // 不设时间窗：元素寿命交给脚本的 lifeTime，这里只留防呆上限。
        durationSeconds = MAX_ITEM_WINDOW_MS / 1000;
    }

    var item = {
        model: model,
        startMs: startMs,
        endMs: startMs + durationSeconds * 1000,
        durationMs: durationSeconds * 1000,
        run: null,
        compileFailed: false,
        activated: false,
        removed: false,
        // 条目级失败：脚本或补间抛错后置位，后续帧跳过它的推进。
        failed: false,
        hasTween: false,
        elements: [],
        // Tween.* 返回的 ITween 句柄（组合子形态的补间）。
        tweenHandles: [],
        // 上一次推进用的条目内时间（毫秒）：帧增量 = 本帧 - 上一次。
        // 句柄时间轴与元素寿命都按它走，暂停时自然停住。
        lastAdvanceMs: -1,
        // Flash 的 enterFrame 监听表（随条目回收一起清掉）。
        frameListeners: [],
        // timer / interval 登记表（条目回收 / reset / seek 越窗时统一清掉）。
        scheduledTimers: [],
        // 真实时间的一次性定时器（原版 timer()/Utils.delay 的 setTimeout，
        // 与条目时钟无关：播放暂停时照常倒计时）。
        realTimers: [],
        // commentTrigger / keyTrigger 登记表（阶段 2-4 的数据链占位）。
        triggers: [],
        // 当前正在执行的脚本：全局函数（$.create* / interval / foreach…）
        // 要知道自己属于哪条条目——脚本执行期间由宿主指向它。
        activeItem: null,
        activeNow: 0,
        // 脚本执行期间处于最前端（$.create* 的 parent 缺省值）。
        createParent: null,
        runCount: 0
    };

    try {
        item.run = compileItem(model);
    } catch (error) {
        item.compileFailed = true;
        reportCompileError(error, model && model.id);
    }

    hostState.items.push(item);
    post("parsed", { count: hostState.items.length, itemId: model && model.id });

    if (itemGeneration !== hostState.generation) {
        return;
    }

    if (isInWindow(item, currentPositionMs())) {
        hostState.dirty = true;
        ensureRunning();
        post("rendered", { count: hostState.items.length });
    }
}

function appendItems(list) {
    if (!Array.isArray(list) || list.length === 0) {
        return;
    }

    // 取一次 generation：若这批条目到达期间发生了 reset，
    // 后续条目不再触发 rendered 上报。
    var itemGeneration = hostState.generation;
    for (var index = 0; index < list.length; index++) {
        addItem(list[index], itemGeneration);
    }
}

export {
    addItem,
    appendItems,
    clearAllItems,
    clearItemElements,
    clearItemTriggers,
    ensureRunning,
    isInWindow,
    recoverItemFromCallbackError,
    registerItemElement,
    seekTo,
    setState,
    setStopped,
    stopRunning
};
