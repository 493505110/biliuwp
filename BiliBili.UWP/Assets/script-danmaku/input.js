// 播放器转发的鼠标输入：不让透明 WebView2 抢占 UWP 控制栏的命中测试。
import { hostState, state, reportRuntimeError, syncOrdinaryDanmakuSuppression } from "./core.js";
import { isUsedAsMask } from "./display.js";
import { displayObjectBounds, traceElementClipPath } from "./renderer.js";
import { screenToLocal } from "./geometry.js";
import { ensureRunning, prepareItemsForComment, recoverItemFromCallbackError } from "./lifecycle.js";

var mouseTypes = ["click", "mouseDown", "mouseUp", "mouseMove", "mouseOver", "mouseOut", "rollOver", "rollOut"];
var hovered = null, pressed = null, pointerId = null, buttonDown = false;
var hitContext = null;

function resetPointer(notify) {
    var released = pressed, exited = hovered, position = hostState.pointerPosition;
    hovered = pressed = pointerId = null;
    buttonDown = false;
    // 丢失捕获/隐藏时也松开脚本保存的按住状态，但绝不能合成 click。
    if (notify) {
        dispatch(released, "mouseUp", position.x, position.y);
        dispatch(exited, "mouseOut", position.x, position.y);
    }
    hostState.pointerPosition = { x: 0, y: 0 };
}

function releasePointerElement(element) {
    function inside(target) {
        for (var node = target; node; node = node.treeParent) if (node === element) return true;
        return false;
    }
    if (inside(hovered)) hovered = null;
    if (inside(pressed)) { pressed = pointerId = null; buttonDown = false; }
}

function active(element) {
    if (!element || element.expired || element.failed || element.props.visible === false
        || element.props.alpha <= 0 || element.enabled === false) return false;
    var item = element.ownerItem;
    return !item || (item.activated && !item.failed && !item.removed);
}

function attached(element) {
    for (var node = element; node; node = node.treeParent) {
        if (!active(node)) return false;
        if (node === hostState.rootElement) return true;
    }
    return false;
}

function interactive(element) {
    return element.mouseEnabled !== false && (element.isButton || typeof element.onclick === "function"
        || mouseTypes.some(function (type) { return element.hasEventListener(type); }));
}

function inRect(point, rect) {
    return point && rect && rect.width > 0 && rect.height > 0 && point.x >= rect.x && point.y >= rect.y
        && point.x <= rect.x + rect.width && point.y <= rect.y + rect.height;
}

function contains(element, x, y, includeChildren) {
    var point = screenToLocal(element, x, y);
    if (!inRect(point, displayObjectBounds(element, false, includeChildren === true))) return false;
    if (element.kind !== "shape" || !element.shapeItems.length) return true;
    // 与绘制共用形状路径；滤镜光晕不扩大可点击区域。
    if (!hitContext) hitContext = document.createElement("canvas").getContext("2d");
    if (typeof hitContext.isPointInPath !== "function") return true;
    hitContext.setTransform(1, 0, 0, 1, 0, 0);
    return traceElementClipPath(hitContext, element) && hitContext.isPointInPath(point.x, point.y);
}

function hitTest(x, y) {
    if (!hostState.visible || state.stopped || x < 0 || y < 0
        || x > hostState.viewportWidth || y > hostState.viewportHeight) return null;
    var mask = hostState.stageMaskElement;
    if (mask && !mask.expired && mask.props.visible !== false && !contains(mask, x, y, true)) return null;
    // 装饰作品可能有数千元件。先跳过没有鼠标监听/按钮的分支，避免逐个求逆矩阵。
    var candidates = new WeakMap();
    function hasInteractive(element) {
        if (candidates.has(element)) return candidates.get(element);
        var result = active(element) && !isUsedAsMask(element)
            && (interactive(element) || element.childList.some(hasInteractive));
        candidates.set(element, result);
        return result;
    }
    function visit(element, interested) {
        if (!active(element) || isUsedAsMask(element)) return null;
        if (!interested && !hasInteractive(element)) return null;
        var local = screenToLocal(element, x, y), scroll = element.props.scrollRect;
        if (!local || (scroll && !inRect(local, scroll))) return null;
        var mask = element.props.mask;
        if (mask && !mask.expired && mask.props.visible !== false && !contains(mask, x, y, true)) return null;
        interested = interested || interactive(element);
        for (var i = element.childList.length - 1; i >= 0; i--) {
            var child = visit(element.childList[i], interested);
            if (child) {
                if (element.mouseChildren === false) return element.mouseEnabled === false ? null : element;
                return child;
            }
        }
        if (element.mouseEnabled !== false && interested && contains(element, x, y)) return element;
        return null;
    }
    var root = hostState.rootElement;
    var result = visit(root, false);
    // 舞台监听允许接收空白区域的鼠标输入。
    return result || (active(root) && interactive(root) ? root : null);
}

function invokeClick(element, event) {
    if (typeof element.onclick !== "function") return;
    var item = element.ownerItem, previous = hostState.activeItem;
    hostState.activeItem = item || previous;
    try { element.onclick.call(element, event); }
    catch (error) {
        element.onclick = null;
        if (!item || !recoverItemFromCallbackError(item, error)) reportRuntimeError(error, item ? item.model.id : "");
    } finally {
        hostState.activeItem = previous;
        if (item) item.createParent = null;
    }
}

function dispatch(target, type, x, y, related, bubbles) {
    if (!target || !attached(target)) return;
    var event = {
        type: type, target: target, currentTarget: target, relatedObject: related || null,
        stageX: x, stageY: y, localX: 0, localY: 0, buttonDown: buttonDown,
        bubbles: bubbles !== false, propagationStopped: false, immediatePropagationStopped: false,
        stopPropagation: function () { this.propagationStopped = true; },
        stopImmediatePropagation: function () { this.immediatePropagationStopped = this.propagationStopped = true; }
    };
    // 回调可以换父节点或删除元件；冒泡路径在投递前固定。
    var path = [];
    for (var node = target; node; node = node.treeParent) path.push(node);
    for (var element of path) {
        if (!attached(element)) continue;
        var point = screenToLocal(element, x, y);
        event.currentTarget = element;
        event.localX = point ? point.x : 0;
        event.localY = point ? point.y : 0;
        element.dispatchEvent(event);
        if (type === "click" && !event.immediatePropagationStopped && attached(element)) invokeClick(element, event);
        if (!event.bubbles || event.propagationStopped || !attached(target)) break;
    }
    syncOrdinaryDanmakuSuppression();
    if (hostState.visible) ensureRunning();
}

function updateHover(target, x, y) {
    if (hovered === target) return;
    var previous = hovered;
    hovered = target;
    dispatch(previous, "mouseOut", x, y, target);
    dispatch(target, "mouseOver", x, y, previous);
    // rollOver / rollOut 不冒泡，只通知真正进入/离开的祖先。
    var oldPath = [], newPath = [];
    for (var node = previous; node; node = node.treeParent) oldPath.push(node);
    for (var node = target; node; node = node.treeParent) newPath.push(node);
    oldPath.filter(function (node) { return newPath.indexOf(node) < 0; })
        .forEach(function (node) { dispatch(node, "rollOut", x, y, target, false); });
    newPath.filter(function (node) { return oldPath.indexOf(node) < 0; }).reverse()
        .forEach(function (node) { dispatch(node, "rollOver", x, y, previous, false); });
}

function pushPointer(action, normalizedX, normalizedY, id, down) {
    if (action === "cancel" || action === "leave") {
        if (pointerId !== null && id !== 0 && id !== pointerId) return;
        var released = pressed;
        pressed = pointerId = null;
        buttonDown = false;
        var position = hostState.pointerPosition;
        dispatch(released, "mouseUp", position.x, position.y);
        updateHover(null, position.x, position.y);
        resetPointer();
        return;
    }
    if (!hostState.visible || state.stopped || !["move", "down", "up"].includes(action)
        || !Number.isFinite(normalizedX) || !Number.isFinite(normalizedY)) return;
    if (pointerId !== null && pointerId !== id) return;
    prepareItemsForComment();
    var x = normalizedX * hostState.viewportWidth, y = normalizedY * hostState.viewportHeight;
    hostState.pointerPosition = { x: x, y: y };
    buttonDown = !!down;
    var target = hitTest(x, y);
    updateHover(target, x, y);
    if (action === "down") {
        pointerId = id;
        pressed = target;
        dispatch(target, "mouseDown", x, y);
    } else if (action === "up") {
        dispatch(target || pressed, "mouseUp", x, y);
        pressed = pointerId = null;
    } else dispatch(target, "mouseMove", x, y);
}

// 点击只由 UWP Tapped 投递一次，鼠标松开不额外合成 click。
// 返回命中结果供播放器决定是否继续处理 BAS / 控制栏单击。
function handleTap(normalizedX, normalizedY, dispatchClick) {
    if (!hostState.visible || state.stopped || !Number.isFinite(normalizedX) || !Number.isFinite(normalizedY)) return false;
    prepareItemsForComment();
    var x = normalizedX * hostState.viewportWidth, y = normalizedY * hostState.viewportHeight;
    var target = hitTest(x, y);
    if (!target) return false;
    if (dispatchClick !== false) {
        hostState.pointerPosition = { x: x, y: y };
        dispatch(target, "click", x, y);
    }
    return true;
}

export { pushPointer, handleTap, resetPointer, releasePointerElement };
