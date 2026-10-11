// 宿主初始化与 C# 命令入口。
import {
    hostState,
    post,
    reportCompileError,
    state,
    syncOrdinaryDanmakuSuppression,
    toFiniteNumber
} from "./core.js";
import {
    M8Display,
    createRetainedElement,
    filterOrdinaryDanmaku,
    prepareOrdinaryDanmakuLayer
} from "./display.js";
import {
    addItem,
    appendItems,
    clearAllItems,
    ensureRunning,
    prepareItemsForComment,
    seekTo,
    setState,
    setStopped,
    stopRunning
} from "./lifecycle.js";
import {
    deliverCommentTrigger,
    deliverKeyTrigger,
    normalizeComment
} from "./player.js";
import {
    clearSurface,
    ensureCanvas,
    resizeCanvas
} from "./renderer.js";
import { Global } from "./runtime.js";
import { pushPointer, handleTap, resetPointer } from "./input.js";
import { prepareEffectsGPU } from "./effects-gpu.js";

// 所有模块完成初始化后再创建舞台根，避免循环依赖读取尚未初始化的状态。
hostState.rootElement = createRetainedElement("group");
// `$.Global` 与 `$G` 必须是同一个对象；在全部 API 就绪后绑定别名。
M8Display.Global = Global;

// ---- 宿主命令对象（控件通过 ExecuteScriptAsync 逐字调用）----

window.scriptDanmakuHost = {
    reset: function (positionSeconds, playing, rate, shouldShow, videoDurationSeconds) {
        hostState.generation++;
        hostState.scriptSeekVersion = 0;
        clearAllItems();
        hostState.pendingScriptSeekMs = null;
        hostState.videoDurationMs = Math.max(0, Number(videoDurationSeconds) || 0) * 1000;
        state.positionMs = Math.max(0, Number(positionSeconds) || 0) * 1000;
        state.playing = !!playing;
        state.stopped = false;
        state.rate = Number(rate) > 0 ? Number(rate) : 1;
        state.updatedAt = performance.now();
        hostState.visible = shouldShow !== false;
        hostState.pendingItemJson = "";
        if (hostState.visible) {
            ensureCanvas();
            // 整批条目已作废：被丢弃的元素不会再进擦除队列，光靠
            // 「脏元素重绘」会把上一批弹幕的像素永久留在画布上。
            clearSurface();
            hostState.dirty = false;
        } else {
            stopRunning();
        }
    },
    append: function (list) {
        appendItems(list);
    },
    // 在 C# 的一次内容替换内配置；版本随消息返回，旧视频不能控制新视频。
    setOrdinaryDanmakuBridge: function (version) {
        hostState.ordinaryDanmakuBridgeVersion = version;
        hostState.ordinaryDanmakuLayer = createRetainedElement("group");
        hostState.ordinaryDanmakuLayer.name = "__biliuwpOrdinaryDanmaku";
        hostState.ordinaryDanmakuOverlay = createRetainedElement("group");
        hostState.ordinaryDanmakuOverlay.name = "__biliuwpOverlay";
        prepareOrdinaryDanmakuLayer();
        syncOrdinaryDanmakuSuppression(true);
    },
    beginItem: function () {
        // 大 payload 分块：先清缓冲，后续 appendItemChunk 逐块追加。
        hostState.pendingItemJson = "";
    },
    appendItemChunk: function (chunk) {
        hostState.pendingItemJson += chunk === undefined || chunk === null ? "" : String(chunk);
    },
    endItem: function () {
        var json = hostState.pendingItemJson;
        hostState.pendingItemJson = "";
        if (!json) {
            return;
        }

        var model = null;
        try {
            model = JSON.parse(json);
        } catch (error) {
            reportCompileError(error, "");
            return;
        }

        if (!model) {
            return;
        }

        // 与 append 同一条路径，同样受 generation 保护。
        addItem(model, hostState.generation);
    },
    setState: setState,
    // 播放结束（原版 ScriptPlayer 的 "stop" 态）。
    setStopped: function (positionSeconds, rate) {
        setStopped(positionSeconds, rate);
    },
    seek: function (positionSeconds, playing, rate, fromScript, seekVersion) {
        seekTo(positionSeconds, playing, rate, fromScript, seekVersion);
    },
    visible: function (shouldShow) {
        hostState.visible = shouldShow !== false;
        if (!hostState.visible) resetPointer(true);
        syncOrdinaryDanmakuSuppression();
        if (hostState.visible) {
            hostState.dirty = true;
            ensureRunning();
        } else {
            // 隐藏时立刻停帧并清空画布；合成步骤也会拦住脏元素。
            stopRunning();
        }
    },
    resize: function () {
        ensureCanvas();
        resizeCanvas();
        hostState.dirty = true;
        if (hostState.visible) {
            ensureRunning();
        }
    },
    // ---- 弹幕数据链 / 输入链（C# → 宿主）----
    // 弹幕快照分两段推：resetComments() 清空 + appendComments(list)
    // 按 24KB 一包追加以外的批量（与 append/beginItem 的分块风格一致）。
    resetComments: function () {
        hostState.commentSnapshot = [];
    },
    filterOrdinaryDanmaku: function (comment) {
        if (!hostState.visible || state.stopped) return true;
        prepareItemsForComment();
        return filterOrdinaryDanmaku(normalizeComment(comment)) && !hostState.ordinaryDanmakuSuppressed;
    },
    appendComments: function (list) {
        if (!Array.isArray(list)) {
            return;
        }

        for (var index = 0; index < list.length; index++) {
            hostState.commentSnapshot.push(normalizeComment(list[index]));
        }
    },
    // 用户发送了一条弹幕：投递给在窗口内条目的 commentTrigger。
    // M8 的 CommentData.time 是秒，C# 侧传的就是播放位置（秒）。
    pushComment: function (comment) {
        var normalized = normalizeComment(comment);
        // 早期游戏轮询 commentList.length 捕获新弹幕，同时保留 commentTrigger 通知。
        hostState.commentSnapshot.push(normalized);
        deliverCommentTrigger(normalized);
    },
    // 键盘事件：投递给在窗口内条目的 keyTrigger（up=true 的只收 keyUp）。
    pushKey: function (keyCode, up) {
        deliverKeyTrigger(Math.trunc(toFiniteNumber(keyCode, -1)), !!up);
    },
    pushPointer: pushPointer,
    handleTap: handleTap,
    inputVersion: function () { return hostState.ordinaryDanmakuBridgeVersion; }
};

// 键盘/鼠标走独立消息通道，不等待 C# 普通弹幕命令队列。内容版本防止
// 旧视频输入在 reset 后误投递给新脚本；隐藏或停止时只接收松键与取消鼠标。
if (window.chrome && window.chrome.webview && window.chrome.webview.addEventListener) {
    window.chrome.webview.addEventListener("message", function (event) {
        var message = event.data;
        if (!message
            || hostState.ordinaryDanmakuBridgeVersion === null
            || message.version !== hostState.ordinaryDanmakuBridgeVersion) return;
        if (message.type === "key") {
            if ((!hostState.visible || state.stopped) && !message.up) return;
            window.scriptDanmakuHost.pushKey(message.keyCode, message.up);
        } else if (message.type === "pointer") {
            pushPointer(message.action, message.x, message.y, message.pointerId, message.down);
        }
    });
}

window.addEventListener("resize", window.scriptDanmakuHost.resize);
ensureCanvas();
prepareEffectsGPU();
post("ready");
