// 宿主初始化与 C# 命令入口。
import {
    hostState,
    post,
    reportCompileError,
    state,
    toFiniteNumber
} from "./core.js";
import {
    M8Display,
    createRetainedElement
} from "./display.js";
import {
    addItem,
    appendItems,
    clearAllItems,
    ensureRunning,
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

// 所有模块完成初始化后再创建舞台根，避免循环依赖读取尚未初始化的状态。
hostState.rootElement = createRetainedElement("group");
// `$.Global` 与 `$G` 必须是同一个对象；在全部 API 就绪后绑定别名。
M8Display.Global = Global;

// ---- 宿主命令对象（控件通过 ExecuteScriptAsync 逐字调用）----

window.scriptDanmakuHost = {
    reset: function (positionSeconds, playing, rate, shouldShow) {
        hostState.generation++;
        clearAllItems();
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
    seek: function (positionSeconds, playing, rate) {
        seekTo(positionSeconds, playing, rate);
    },
    visible: function (shouldShow) {
        hostState.visible = shouldShow !== false;
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
        deliverCommentTrigger(normalizeComment(comment));
    },
    // 键盘事件：投递给在窗口内条目的 keyTrigger（up=true 的只收 keyUp）。
    pushKey: function (keyCode, up) {
        deliverKeyTrigger(Math.trunc(toFiniteNumber(keyCode, -1)), !!up);
    }
};

window.addEventListener("resize", window.scriptDanmakuHost.resize);
ensureCanvas();
post("ready");
