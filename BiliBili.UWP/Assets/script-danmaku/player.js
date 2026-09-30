// Player API、音效、弹幕快照与输入触发器。
import {
    AV_NUMBER_PATTERN,
    DEFAULT_TEXT_FONTSIZE,
    DEFAULT_TRIGGER_TIMEOUT_MS,
    currentItem,
    currentPositionMs,
    hostState,
    normalizeColor,
    reportRuntimeError,
    requestNavigate,
    requestPause,
    requestPlay,
    requestSeek,
    state,
    toFiniteNumber
} from "./core.js";
import {
    ensureRunning,
    isInWindow,
    recoverItemFromCallbackError
} from "./lifecycle.js";
import {
    setStageMask
} from "./renderer.js";

// ---- commentTrigger / keyTrigger ----
//
// 触发器登记在条目上，与定时器共用同一条生命周期：条目回收 / reset /
// seek 越窗时由 clearItemTriggers 统一清掉，触发器绝不会活过条目。
//
// timeout 用**真实时间**计量（performance.now）：原版 ScriptPlayer 的
// commentTrigger / keyTrigger 是裸 setTimeout（ScriptPlayer.as:101-136），
// 暂停期间照常倒计时、到点自动摘除——与画面时钟不是同一条线。
// （按反编译代码纠正：此前按「与画面同一时钟」实现，暂停会冻结倒计时。）
//
// 投递是**消息驱动**的（pushComment / pushKey 到达时同步调用），
// 不依赖帧循环，所以暂停时收到的事件也会立即送达回调。
function itemElapsedMs(item) {
    return Math.max(0, currentPositionMs() - item.startMs);
}

// M8 的 keyTrigger 只监听这一组键（文档明确列出），其余按键不投递。
// 键值与 DOM/Flash 的 keyCode 同源，C# 侧直接透传 VirtualKey 的整数值。
// 27（Escape）不能漏：原版的判定是
// `code == 27 || code >= 96 && code <= 105 || code >= 34 && code <= 40
//  || W || S || A || D`（ScriptEventManager.as:33、:57），
// 注意 96-105 是**小键盘 0-9**、34-40 是 Home/方向键/End/PgDn——
// 33（PageUp）不在原版范围里，此前宿主收错了。
var M8_TRIGGER_KEY_CODES = {
    27: true, 34: true, 35: true, 36: true, 37: true, 38: true, 39: true, 40: true,
    65: true, 68: true, 83: true, 87: true,
    96: true, 97: true, 98: true, 99: true, 100: true,
    101: true, 102: true, 103: true, 104: true, 105: true
};

function normalizeTriggerTimeout(timeoutMs) {
    var value = toFiniteNumber(timeoutMs, DEFAULT_TRIGGER_TIMEOUT_MS);
    return value > 0 ? value : DEFAULT_TRIGGER_TIMEOUT_MS;
}

function registerItemTrigger(kind, callback, timeoutMs, up) {
    if (typeof callback !== "function") {
        return 0;
    }

    var item = currentItem();
    if (!item) {
        return 0;
    }

    var trigger = {
        id: ++hostState.nextTriggerId,
        kind: kind,
        callback: callback,
        up: !!up,
        ownerItem: item,
        // 真实时间基准（原版是裸 setTimeout，播放暂停也照走）。
        registeredAtMs: performance.now(),
        timeoutMs: normalizeTriggerTimeout(timeoutMs)
    };
    item.triggers.push(trigger);
    return trigger.id;
}

// 取出该条目上该类型「未过期」的触发器，顺手回收已过期的。
function liveTriggers(item, kind, keyUp) {
    var now = performance.now();
    var live = [];
    for (var index = item.triggers.length - 1; index >= 0; index--) {
        var trigger = item.triggers[index];
        if (trigger.kind !== kind) {
            continue;
        }

        if (kind === "key" && trigger.up !== keyUp) {
            continue;
        }

        if (now - trigger.registeredAtMs >= trigger.timeoutMs) {
            item.triggers.splice(index, 1);
            continue;
        }

        live.push(trigger);
    }

    return live;
}

function removeTrigger(item, trigger) {
    var index = item.triggers.indexOf(trigger);
    if (index >= 0) {
        item.triggers.splice(index, 1);
    }
}

// 触发器回调在「该条目」的上下文里执行：回调里可以照常建元件、开定时器。
function invokeTrigger(trigger, argument) {
    var item = trigger.ownerItem;
    var previous = hostState.activeItem;
    hostState.activeItem = item;
    item.activeNow = currentPositionMs();
    try {
        trigger.callback(argument);
    } catch (error) {
        // 回调抛错只摘掉这一个触发器，不影响其它触发器与整帧。
        removeTrigger(item, trigger);
        if (!recoverItemFromCallbackError(item, error)) {
            reportRuntimeError(error, item.model.id);
        }
    } finally {
        hostState.activeItem = previous;
        item.createParent = null;
    }

    hostState.dirty = true;
    ensureRunning();
}

function forEachTriggerItem(callback) {
    var now = currentPositionMs();
    for (var index = 0; index < hostState.items.length; index++) {
        var item = hostState.items[index];
        // 只有「已激活、在窗口内、没失败」的条目才收事件：窗口外的条目
        // 已经被 deactivateItem 清过触发器，这里再挡一道防边界竞态。
        if (!item.activated || item.failed || !isInWindow(item, now)) {
            continue;
        }

        callback(item);
    }
}

function deliverCommentTrigger(comment) {
    forEachTriggerItem(function (item) {
        var triggers = liveTriggers(item, "comment", false);
        for (var index = 0; index < triggers.length; index++) {
            invokeTrigger(triggers[index], comment);
        }
    });
}

function deliverKeyTrigger(keyCode, keyUp) {
    if (!M8_TRIGGER_KEY_CODES[keyCode]) {
        return;
    }

    forEachTriggerItem(function (item) {
        var triggers = liveTriggers(item, "key", keyUp);
        for (var index = 0; index < triggers.length; index++) {
            invokeTrigger(triggers[index], keyCode);
        }
    });
}

// ---- 弹幕快照（M8 的 Player.commentList）----
//
// 由 C# 侧推入：resetComments() 清空、appendComments([...]) 追加。
// 字段与 M8 的 CommentData 同名同义（txt / time / color / pool / mode /
// fontSize），宿主只做缺省填充，不做重命名，脚本拿到的就是 M8 的形状。
function normalizeComment(raw) {
    var source = raw && typeof raw === "object" ? raw : {};
    return {
        txt: source.txt === undefined || source.txt === null ? "" : String(source.txt),
        // M8 的 CommentData.time 是**秒**（Player.time 才是毫秒）。
        time: toFiniteNumber(source.time, 0),
        color: normalizeColor(source.color),
        pool: Math.trunc(toFiniteNumber(source.pool, 0)),
        mode: Math.trunc(toFiniteNumber(source.mode, 1)),
        fontSize: toFiniteNumber(source.fontSize, DEFAULT_TEXT_FONTSIZE)
    };
}

// ---- Player：实时读宿主的外推时钟与播放状态 ----

function currentPlayerState() {
    if (!hostState.visible) {
        // 宿主没有独立于可见性的停止态：弹幕层隐藏时给 stop。
        return "stop";
    }

    // 播放结束：原版 ScriptPlayer.completeHandler 把 _state 置 "stop"，
    // 此后只有在收到新的播放器状态事件时才离开这个态。
    if (state.stopped) {
        return "stop";
    }

    return state.playing ? "playing" : "pause";
}

var Player = {
    play: function () {
        // 恢复播放：走 action 通道交给 PlayerPage（与 pause / seek / jump 同一条路）。
        requestPlay();
    },
    pause: function () {
        requestPause();
    },
    seek: function (offset) {
        var offsetMs = toFiniteNumber(offset, 0);
        return requestSeek(Math.max(0, offsetMs) / 1000);
    },
    jump: function (av, page, newwindow) {
        var match = AV_NUMBER_PATTERN.exec(String(av === undefined || av === null ? "" : av));
        if (!match) {
            return false;
        }

        var pageNumber = Math.max(1, Math.floor(toFiniteNumber(page, 1)));
        return requestNavigate(
            "https://www.bilibili.com/video/av" + match[1] + "/?p=" + pageNumber);
    },
    // 原版音效不在客户端里，是按名字在线拉的 mp3（ScriptSound.as:12-30）：
    //   <协议>://i2.hdslb.com/soundlib/<name>.mp3
    // onLoad 挂在 Event.OPEN 上——开始加载就回调，不等加载完成。
    createSound: function (name, onLoad) {
        return createScriptSound(name, onLoad);
    },
    // 设置播放器遮罩：整块脚本弹幕画布裁剪到该元件的形状里。
    setMask: function (obj) {
        setStageMask(obj);
    },
    // 监听「用户发送弹幕」：回调收到一条 CommentData 形状的对象。
    // PlayerPage 在发送成功后经 pushComment 推入，本函数只登记。
    commentTrigger: function (f, timeout) {
        return registerItemTrigger("comment", f, timeout, false);
    },
    // 监听键盘输入。up = true 时监听 keyUp。只投递 M8 文档列出的那组键。
    keyTrigger: function (f, timeout, up) {
        return registerItemTrigger("key", f, timeout, up);
    }
};

// ---- Player.createSound：原版 ScriptSound ----
//
// 原版音效是**在线拉取**的 mp3，不是随客户端分发的内置音效库
// （ScriptSound.as:16-22：url = HTTP_PROTOCOL + "://i2.hdslb.com/soundlib/"
// + name + ".mp3"）。这里用 <audio> 元素等价实现：
//  - onLoad 绑 loadstart（对应原版的 Event.OPEN：开始加载即回调），
//    并补一个 error 兜底，避免加载失败时脚本永远等不到回调；
//  - loadPercent() 对应 AS3 的 bytesLoaded / bytesTotal，这里取 buffered 比例；
//  - play(startTime, loops)：startTime 是秒偏移，loops 是**额外**重复次数；
//  - stop() 原版就是空实现（ScriptSound.as:42-44），这里同样不做事；
//  - remove() 对应 AS3 的 Sound.close()。
// 注意 WebView2 的自动播放策略可能拦下没有用户手势的播放：这种情况下
// audio.play() 会 reject，这里按静默失败处理（不打扰脚本）。
var SOUND_LIBRARY_BASE = "https://i2.hdslb.com/soundlib/";

function createScriptSound(name, onLoad) {
    // 桩环境（node:vm）没有 Audio：给一个等价的空实现，脚本不会崩。
    if (typeof window.Audio !== "function") {
        return {
            loadPercent: function () {
                return 0;
            },
            play: function () {
            },
            stop: function () {
            },
            remove: function () {
            }
        };
    }

    var soundName = String(name);
    var audio = new window.Audio();
    audio.preload = "auto";
    audio.src = SOUND_LIBRARY_BASE + encodeURIComponent(soundName) + ".mp3";
    if (typeof onLoad === "function") {
        var notifyLoaded = function () {
            onLoad();
        };
        audio.addEventListener("loadstart", notifyLoaded, { once: true });
        audio.addEventListener("error", notifyLoaded, { once: true });
    }

    return {
        loadPercent: function () {
            try {
                var buffered = audio.buffered;
                if (buffered && buffered.length > 0 && audio.duration && isFinite(audio.duration)) {
                    return Math.floor(100 * buffered.end(buffered.length - 1) / audio.duration);
                }
            } catch (error) {
                // 取不到缓冲信息时按 0 处理。
            }

            return 0;
        },
        play: function (startTime, loops) {
            var start = Math.max(0, toFiniteNumber(startTime, 0));
            // AS3 的 loops 是「额外重复次数」：0 = 播一次，1 = 播两次。
            var extra = Math.max(0, Math.floor(toFiniteNumber(loops, 0)));
            try {
                audio.currentTime = start;
                audio.loop = extra > 0;
                var promise = audio.play();
                if (promise && typeof promise.catch === "function") {
                    promise.catch(function () {
                        // 自动播放被拦：静默失败，不影响脚本。
                    });
                }
            } catch (error) {
                // 同上。
            }
        },
        stop: function () {
            // 原版 stop() 是空实现（ScriptSound.as:42-44）。
        },
        remove: function () {
            try {
                audio.pause();
                audio.removeAttribute("src");
                audio.load();
            } catch (error) {
                // 释放失败不影响脚本。
            }
        }
    };
}

export {
    Player,
    currentPlayerState,
    deliverCommentTrigger,
    deliverKeyTrigger,
    normalizeComment
};
