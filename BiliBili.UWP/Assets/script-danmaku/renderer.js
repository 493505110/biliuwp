// 画布绘制、位图缓存、遮罩与脏矩形合成。
import {
    DEFAULT_BOUNDS_PADDING,
    DIRTY_RECT_PADDING,
    container,
    hostState,
    normalizeColor,
    toFiniteNumber
} from "./core.js";
import {
    applyElementBlendMode,
    applyElementMaskClip,
    createMatrix3D,
    createPerspectiveProjection,
    createPlaceholderMatrix,
    drawTextElement,
    isElementDirty,
    isElementVisible,
    isUsedAsMask,
    markAllDirty,
    measureTextElement,
    removeChildFromParent,
    setPropertyInternal
} from "./display.js";
import {
    ensureRunning
} from "./lifecycle.js";
import {
    markPropertyDirty
} from "./tween.js";

import { localMatrix, relativeMatrix, worldMatrix, transformedBounds,
    transformPoint, subtree3D, projectionFor, projectPoint, drawProjected,
    beginProjectedBatch, flushProjectedBatch, endProjectedBatch } from "./geometry.js";
import { filterPadding, applyEffects } from "./effects.js";

function ensureCanvas() {
    if (hostState.canvas) {
        return;
    }

    hostState.canvas = document.createElement("canvas");
    hostState.canvas.style.pointerEvents = "none";
    container.appendChild(hostState.canvas);
    hostState.context2d = hostState.canvas.getContext("2d");
    resizeCanvas();
}

function resizeCanvas() {
    if (!hostState.canvas) {
        return;
    }

    var width = container.offsetWidth || 1;
    var height = container.offsetHeight || 1;
    var ratio = window.devicePixelRatio || 1;
    hostState.viewportWidth = width;
    hostState.viewportHeight = height;
    hostState.devicePixelRatioValue = ratio;
    hostState.canvas.width = Math.max(1, Math.round(width * ratio));
    hostState.canvas.height = Math.max(1, Math.round(height * ratio));
    hostState.context2d = hostState.canvas.getContext("2d");
    if (hostState.context2d) {
        hostState.context2d.setTransform(ratio, 0, 0, ratio, 0, 0);
    }

    markAllDirty();
}

// ---- 绘制与缓存 ----
//
// 元素分三类：
//  - 叶子元素（shape / text / image）：内容画一次进离屏位图（cacheCanvas），
//    之后只有「内容变了」才重画；单纯移动 / 缩放 / 旋转只改变换矩阵。
//  - 复合元素（有子节点）：子树先各自成位图，再烘到该元素自己的复合层，
//    父元素的 tween 只作用在这张已烘好的位图上（子树的绘制代码不再重跑）。
//  - layer 元素：脚本自绘的离屏 canvas，逐帧直接合成，不走缓存。

var currentBounds = null;
var rasterVersion = 0;

function boundsReset() {
    currentBounds = {
        minX: Infinity,
        minY: Infinity,
        maxX: -Infinity,
        maxY: -Infinity
    };
}

function boundsAdd(x, y) {
    if (!isFinite(x) || !isFinite(y)) {
        return;
    }

    if (x < currentBounds.minX) {
        currentBounds.minX = x;
    }

    if (y < currentBounds.minY) {
        currentBounds.minY = y;
    }

    if (x > currentBounds.maxX) {
        currentBounds.maxX = x;
    }

    if (y > currentBounds.maxY) {
        currentBounds.maxY = y;
    }
}

function boundsResult(padding) {
    if (currentBounds.maxX < currentBounds.minX) {
        return null;
    }

    var pad = padding === undefined ? DEFAULT_BOUNDS_PADDING : padding;
    return {
        x: currentBounds.minX - pad,
        y: currentBounds.minY - pad,
        width: Math.max(1, currentBounds.maxX - currentBounds.minX + pad * 2),
        height: Math.max(1, currentBounds.maxY - currentBounds.minY + pad * 2)
    };
}

function drawShapeElement(target, element, offsetX, offsetY) {
    var items = element.shapeItems || [];
    for (var index = 0; index < items.length; index++) {
        drawShapeItem(target, items[index], offsetX, offsetY);
    }
}

function applyStrokeStyle(target, line) {
    target.lineWidth = Math.max(0.1, line.width);
    if (line.gradient) {
        var gradient = createCanvasGradient(target, line.gradient);
        if (gradient) {
            target.strokeStyle = gradient;
            return;
        }
    }

    target.strokeStyle = rgbaCss(line.color, line.alpha);
}

function rgbaCss(color, alpha) {
    return "rgba(" + ((color >> 16) & 0xFF) + ","
        + ((color >> 8) & 0xFF) + ","
        + (color & 0xFF) + "," + alpha + ")";
}

// 渐变填充：Flash 的 beginGradientFill 用「渐变框矩阵」定义颜色分布，
// 这里换算成 canvas 的 createLinearGradient / createRadialGradient。
// 渐变框（createGradientBox(w, h, rotation, tx, ty)）左边缘到右边缘
// 是颜色轴，中心在 (tx + w/2, ty + h/2)。
function createCanvasGradient(target, gradient) {
    var box = gradient.box || { width: 0, height: 0, rotation: 0, tx: 0, ty: 0 };
    var width = toFiniteNumber(box.width, 0);
    var height = toFiniteNumber(box.height, 0);
    var rotation = toFiniteNumber(box.rotation, 0);
    var centerX = toFiniteNumber(box.tx, 0) + width / 2;
    var centerY = toFiniteNumber(box.ty, 0) + height / 2;
    var cos = Math.cos(rotation);
    var sin = Math.sin(rotation);
    var halfX = (width / 2) * cos;
    var halfY = (width / 2) * sin;
    var canvasGradient;
    if (gradient.type === "radial") {
        // Flash 的径向渐变半径取渐变框宽度的一半（与矩形一致）。
        canvasGradient = target.createRadialGradient(
            centerX, centerY, 0, centerX, centerY, Math.abs(width) / 2);
    } else {
        canvasGradient = target.createLinearGradient(
            centerX - halfX, centerY - halfY, centerX + halfX, centerY + halfY);
    }

    if (!canvasGradient) {
        return null;
    }

    var colors = gradient.colors || [];
    var alphas = gradient.alphas || [];
    var ratios = gradient.ratios || [];
    for (var index = 0; index < colors.length; index++) {
        var ratio = toFiniteNumber(ratios[index], 0) / 255;
        if (ratio < 0) {
            ratio = 0;
        }

        if (ratio > 1) {
            ratio = 1;
        }

        var alpha = alphas.length > index ? toFiniteNumber(alphas[index], 1) : 1;
        canvasGradient.addColorStop(
            ratio,
            rgbaCss(normalizeColor(colors[index]),
                Math.max(0, Math.min(1, alpha))));
    }

    return canvasGradient;
}

function applyFillStyle(target, fill) {
    // 渐变填充：fill.gradient 非空时优先（与 Flash 的 beginFill /
    // beginGradientFill 二选一语义一致）。
    if (fill.gradient) {
        var gradient = createCanvasGradient(target, fill.gradient);
        if (gradient) {
            target.fillStyle = gradient;
            return;
        }
    }

    target.fillStyle = rgbaCss(fill.color, fill.alpha);
}

function tracePolygon(target, path, offsetX, offsetY) {
    var points = path.points;
    target.beginPath();
    target.moveTo(points[0] + offsetX, points[1] + offsetY);
    if (path.segments) {
        for (var i = 0; i < path.segments.length; i++) {
            var segment = path.segments[i];
            if (segment[0] === 3) target.quadraticCurveTo(segment[1] + offsetX, segment[2] + offsetY, segment[3] + offsetX, segment[4] + offsetY);
            else target.lineTo(segment[1] + offsetX, segment[2] + offsetY);
        }
        return;
    }
    var index = 2;
    while (index < points.length) {
        target.lineTo(points[index] + offsetX, points[index + 1] + offsetY);
        index += 2;
    }
}

function visitGraphicsPath(item, visit) {
    var offset = 0;
    for (var i = 0; i < item.commands.length; i++) {
        var command = item.commands[i], count = command === 3 ? 4 : command === 6 ? 6 : command >= 1 && command <= 5 ? (command >= 4 ? 4 : 2) : 0;
        if (!count) continue;
        var points = item.data.slice(offset, offset + count); offset += count;
        if (command === 4 || command === 5) { points = points.slice(2); command -= 3; }
        visit(command, points);
    }
}

function traceGraphicsPath(target, item, map) {
    visitGraphicsPath(item, function (command, values) {
        var points = [];
        for (var i = 0; i < values.length; i += 2) points.push(map(values[i], values[i + 1]));
        if (command === 1) target.moveTo(points[0].x, points[0].y);
        else if (command === 2) target.lineTo(points[0].x, points[0].y);
        else if (command === 3) target.quadraticCurveTo(points[0].x, points[0].y, points[1].x, points[1].y);
        else if (command === 6) target.bezierCurveTo(points[0].x, points[0].y, points[1].x, points[1].y, points[2].x, points[2].y);
    });
}

function drawShapeItem(target, item, offsetX, offsetY) {
    var path;
    switch (item.kind) {
        case "path":
            target.beginPath();
            traceGraphicsPath(target, item, function (x, y) { return { x: x + offsetX, y: y + offsetY }; });
            if (item.fill) { applyFillStyle(target, item.fill); target.fill(item.winding); }
            if (item.line) { applyStrokeStyle(target, item.line); target.stroke(); }
            return;
        case "line":
        case "fill":
            path = item.path;
            if (!path || !path.points || path.points.length < 2) {
                return;
            }

            tracePolygon(target, path, offsetX, offsetY);
            if (item.kind === "fill") {
                if (item.style) {
                    applyFillStyle(target, item.style);
                    target.fill();
                }
            } else if (item.style) {
                applyStrokeStyle(target, item.style);
                target.stroke();
            }

            return;
        case "rect":
            target.beginPath();
            target.rect(item.x + offsetX, item.y + offsetY, item.width, item.height);
            break;
        case "roundRect":
            target.beginPath();
            roundedRectPath(
                target,
                item.x + offsetX,
                item.y + offsetY,
                item.width,
                item.height,
                item.rx,
                item.ry);
            break;
        case "circle":
            target.beginPath();
            target.arc(
                item.x + offsetX,
                item.y + offsetY,
                Math.abs(item.radius),
                0,
                Math.PI * 2);
            break;
        case "ellipse":
            target.beginPath();
            target.ellipse(
                item.x + offsetX,
                item.y + offsetY,
                Math.abs(item.width) / 2,
                Math.abs(item.height) / 2,
                0,
                0,
                Math.PI * 2);
            break;
        case "wedge":
            target.beginPath();
            target.moveTo(item.x + offsetX, item.y + offsetY);
            target.arc(
                item.x + offsetX,
                item.y + offsetY,
                Math.abs(item.radius),
                item.startAngle,
                item.startAngle + item.arc);
            target.closePath();
            break;
        case "polygon":
            if (!item.points || item.points.length < 4) {
                return;
            }

            target.beginPath();
            target.moveTo(item.points[0] + offsetX, item.points[1] + offsetY);
            var pointIndex = 2;
            while (pointIndex < item.points.length) {
                target.lineTo(
                    item.points[pointIndex] + offsetX,
                    item.points[pointIndex + 1] + offsetY);
                pointIndex += 2;
            }

            target.closePath();
            break;
        default:
            return;
    }

    if (item.fill) {
        applyFillStyle(target, item.fill);
        target.fill();
    }

    if (item.line) {
        applyStrokeStyle(target, item.line);
        target.stroke();
    }
}

function roundedRectPath(target, x, y, width, height, rx, ry) {
    var radiusX = Math.max(0, Math.min(Math.abs(rx), Math.abs(width) / 2));
    var radiusY = Math.max(0, Math.min(Math.abs(ry), Math.abs(height) / 2));
    target.moveTo(x + radiusX, y);
    target.lineTo(x + width - radiusX, y);
    target.quadraticCurveTo(x + width, y, x + width, y + radiusY);
    target.lineTo(x + width, y + height - radiusY);
    target.quadraticCurveTo(x + width, y + height, x + width - radiusX, y + height);
    target.lineTo(x + radiusX, y + height);
    target.quadraticCurveTo(x, y + height, x, y + height - radiusY);
    target.lineTo(x, y + radiusY);
    target.quadraticCurveTo(x, y, x + radiusX, y);
    target.closePath();
}

// 曲线极值参与显示对象尺寸；控制点只界定曲线，不属于绘制内容。
function pathBounds(item) {
    var x = 0, y = 0;
    visitGraphicsPath(item, function (command, values) {
        if (command === 1) { x = values[0]; y = values[1]; return; }
        var xs = [x], ys = [y];
        for (var i = 0; i < values.length; i += 2) { xs.push(values[i]); ys.push(values[i + 1]); }
        boundsAdd(x, y); boundsAdd(xs[xs.length - 1], ys[ys.length - 1]);
        function valueAt(points, t) {
            var values = points.slice();
            while (values.length > 1) { for (var j = 0; j + 1 < values.length; j++) values[j] = values[j] * (1 - t) + values[j + 1] * t; values.pop(); }
            return values[0];
        }
        [xs, ys].forEach(function (points) {
            var roots = [];
            if (points.length === 3) {
                var denominator = points[0] - 2 * points[1] + points[2];
                if (denominator) roots.push((points[0] - points[1]) / denominator);
            } else if (points.length === 4) {
                var a = -points[0] + 3 * points[1] - 3 * points[2] + points[3];
                var b = 2 * (points[0] - 2 * points[1] + points[2]), c = points[1] - points[0];
                if (Math.abs(a) < 1e-12) { if (b) roots.push(-c / b); }
                else { var discriminant = b * b - 4 * a * c; if (discriminant >= 0) roots.push((-b + Math.sqrt(discriminant)) / (2 * a), (-b - Math.sqrt(discriminant)) / (2 * a)); }
            }
            roots.forEach(function (t) { if (t > 0 && t < 1) boundsAdd(valueAt(xs, t), valueAt(ys, t)); });
        });
        x = xs[xs.length - 1]; y = ys[ys.length - 1];
    });
}

function computeShapeBounds(element, padding) {
    var items = element.shapeItems || [];
    boundsReset();
    for (var index = 0; index < items.length; index++) {
        var item = items[index];
        var path;
        switch (item.kind) {
            case "path":
                pathBounds(item);
                break;
            case "line":
            case "fill":
                path = item.path;
                if (path && path.points) {
                    var pointIndex = 0;
                    while (pointIndex < path.points.length) {
                        boundsAdd(path.points[pointIndex], path.points[pointIndex + 1]);
                        pointIndex += 2;
                    }

                    if (path.curves) {
                        for (var curveIndex = 0; curveIndex < path.curves.length; curveIndex++) {
                            var curve = path.curves[curveIndex];
                            boundsAdd(curve[0], curve[1]);
                        }
                    }
                }

                break;
            case "rect":
            case "roundRect":
                boundsAdd(item.x, item.y);
                boundsAdd(item.x + item.width, item.y + item.height);
                break;
            case "circle":
                boundsAdd(item.x - Math.abs(item.radius), item.y - Math.abs(item.radius));
                boundsAdd(item.x + Math.abs(item.radius), item.y + Math.abs(item.radius));
                break;
            case "ellipse":
                boundsAdd(item.x - Math.abs(item.width) / 2, item.y - Math.abs(item.height) / 2);
                boundsAdd(item.x + Math.abs(item.width) / 2, item.y + Math.abs(item.height) / 2);
                break;
            case "wedge":
                boundsAdd(item.x - Math.abs(item.radius), item.y - Math.abs(item.radius));
                boundsAdd(item.x + Math.abs(item.radius), item.y + Math.abs(item.radius));
                break;
            case "polygon":
                if (item.points) {
                    var polygonIndex = 0;
                    while (polygonIndex < item.points.length) {
                        boundsAdd(item.points[polygonIndex], item.points[polygonIndex + 1]);
                        polygonIndex += 2;
                    }
                }

                break;
        }
    }

    return boundsResult(padding === undefined ? element.padding : padding);
}

// 同步读取几何尺寸，不能等待下一帧缓存：Akari 在排字时立即读取 glyph.width。
// Flash 尺寸不含滤镜/缓存扩边，隐藏的孩子仍计入容器几何范围。
function displayObjectBounds(element, parentSpace) {
    var rectangles = [], own = null;
    if (element.kind === "shape") own = computeShapeBounds(element, 0);
    else if (element.kind === "text") { var metrics = measureTextElement(element); own = { x: 0, y: 0, width: metrics.width, height: metrics.height }; }
    else if (element.kind === "image" && element.loaded) own = { x: 0, y: 0, width: element.imageWidth, height: element.imageHeight };
    else if (element.kind === "layer") own = { x: 0, y: 0, width: element.layerWidth, height: element.layerHeight };
    if (own) rectangles.push(own);
    for (var i = 0; i < element.childList.length; i++) {
        var child = element.childList[i];
        if (child.expired) continue;
        var bounds = displayObjectBounds(child, true);
        if (bounds) rectangles.push(bounds);
    }
    var result = unionRects(rectangles);
    return result && parentSpace ? transformedBounds(result, localMatrix(element)) : result;
}

function elementLocalBounds(element) {
    var metrics;
    switch (element.kind) {
        case "shape":
            return computeShapeBounds(element);
        case "text":
            metrics = measureTextElement(element);
            return {
                x: 0,
                y: 0,
                width: Math.max(1, metrics.width) + DEFAULT_BOUNDS_PADDING * 2,
                height: metrics.height + DEFAULT_BOUNDS_PADDING * 2
            };
        case "image":
            if (!element.loaded) {
                return null;
            }

            return {
                x: 0,
                y: 0,
                width: Math.max(1, element.imageWidth),
                height: Math.max(1, element.imageHeight)
            };
        case "layer":
            return {
                x: 0,
                y: 0,
                width: element.layerWidth,
                height: element.layerHeight
            };
        default:
            return null;
    }
}

function applyElementTransform(target, element) {
    var m = localMatrix(element);
    target.transform(m[0], m[1], m[4], m[5], m[12], m[13]);
    target.globalAlpha = Math.max(0, Math.min(1, element.props.alpha));
}

function paintElementContent(target, element) {
    if (element.kind === "shape") drawShapeElement(target, element, 0, 0);
    else if (element.kind === "text") drawTextElement(target, element, 0, 0);
    else if (element.kind === "image" && element.imageValue) target.drawImage(element.imageValue, 0, 0);
    else if (element.kind === "layer") target.drawImage(element.layerCanvas, 0, 0);
}

// 缓存按最终显示倍率选取分辨率，防止放大的小字形变成模糊位图。
// 倍率分档，纯移动不重烘；尺寸上限防止场外巨型图元分配过大位图。
function rasterRatio(element, bounds) {
    var m = worldMatrix(element), scale = Math.max(Math.hypot(m[0], m[1]), Math.hypot(m[4], m[5]));
    if (subtree3D(element)) {
        var focal = projectionFor(element).focalLength;
        scale *= focal / Math.max(1, focal + m[14]);
    }
    // 字体轮廓常有几千个本地单位，实际显示仅几十像素；缩小时同样分档。
    var bucket = Math.pow(2, Math.ceil(Math.log2(Math.max(1 / 64, Math.min(8, scale)))));
    var ratio = (window.devicePixelRatio || 1) * bucket;
    if (bounds) ratio = Math.min(ratio, 4096 / Math.max(1, bounds.width), 4096 / Math.max(1, bounds.height));
    return ratio;
}

// 叶子元素的内容缓存。只有 needsCache 置位时才会走到这里——
// 这是「静态元素首帧之后不再重绘」的落点。
function rebuildElementCache(element) {
    var bounds = elementLocalBounds(element);
    element.needsCache = false;
    if (!bounds) {
        element.cacheCanvas = null;
        element.cacheCtx = null;
        element.cacheBounds = null;
        element.painted = true;
        return;
    }

    var padding = filterPadding(element.childList.length ? null : element.props.filters);
    var ratio = rasterRatio(element, { width: bounds.width + padding.x * 2, height: bounds.height + padding.y * 2 });
    element.cacheDpr = ratio;
    var width = Math.max(1, Math.ceil((bounds.width + padding.x * 2) * ratio));
    var height = Math.max(1, Math.ceil((bounds.height + padding.y * 2) * ratio));
    if (!element.cacheCanvas) {
        element.cacheCanvas = document.createElement("canvas");
    }

    if (element.cacheCanvas.width !== width || element.cacheCanvas.height !== height) {
        element.cacheCanvas.width = width;
        element.cacheCanvas.height = height;
    }

    element.cacheCtx = element.cacheCanvas.getContext("2d");
    var target = element.cacheCtx;
    target.setTransform(1, 0, 0, 1, 0, 0);
    target.clearRect(0, 0, width, height);
    target.setTransform(ratio, 0, 0, ratio, 0, 0);
    target.translate(padding.x - bounds.x, padding.y - bounds.y);
    target.globalAlpha = 1;
    paintElementContent(target, element);
    if (!element.childList.length) applyEffects(element.cacheCanvas, element, ratio);
    element.cacheCanvas.__m8RasterVersion = ++rasterVersion;

    element.cacheBounds = {
        x: bounds.x - padding.x,
        y: bounds.y - padding.y,
        width: bounds.width + padding.x * 2,
        height: bounds.height + padding.y * 2
    };
    element.painted = true;
    hostState.paintCount++;
}

// 复合元素的子树烘焙。子元素走 blitElement（用自己的缓存），
// 所以重建复合层是 N 次位图拷贝，而不是 N 份绘制代码。
// 复合层的实际尺寸取决于子树内容（子元素坐标相对父元件注册点）。
// 返回 null 表示子树当前没有可绘制内容（空容器首帧不建复合层）。
function computeCompositeBounds(element) {
    // 每层独立累计；递归调用不能重置父级的包围盒。
    var rectangles = [], own = elementLocalBounds(element);
    if (own) rectangles.push(own);
    for (var i = 0; i < element.childList.length; i++) {
        var child = element.childList[i];
        if (child.expired || child.props.visible === false || child.props.alpha <= 0 || isUsedAsMask(child)) continue;
        var bounds = child.childList.length ? child.compositeBounds : child.cacheBounds;
        if (!bounds) bounds = child.childList.length ? computeCompositeBounds(child) : elementLocalBounds(child);
        if (bounds) rectangles.push(compositeChildBounds(child, bounds));
    }
    return unionRects(rectangles);
}

function compositeChildBounds(child, bounds) {
    return transformedBounds(bounds, localMatrix(child));
}

function rebuildComposite(element) {
    var bounds = computeCompositeBounds(element);
    element.compositeDirty = false;
    if (!bounds) {
        // 最后一个孩子隐藏后，旧复合位图也必须作废。
        element.composite = null; element.compositeBounds = null;
        element.painted = true;
        return;
    }

    // 子元素坐标相对父元件注册点：烘焙时把 bounds 原点平移到
    // (padding, padding)，blit 时再按 bounds 的偏移取用，
    // 这样父元件自己移动/缩放/旋转时子树不必重绘（见 D4）。
    var padding = filterPadding(element.props.filters);
    bounds = { x: bounds.x - padding.x, y: bounds.y - padding.y,
        width: bounds.width + padding.x * 2, height: bounds.height + padding.y * 2 };
    var ratio = rasterRatio(element, bounds);
    element.compositeDpr = ratio;
    var width = Math.max(1, Math.ceil(bounds.width * ratio));
    var height = Math.max(1, Math.ceil(bounds.height * ratio));
    if (!element.composite) {
        element.composite = document.createElement("canvas");
    }

    if (element.composite.width !== width || element.composite.height !== height) {
        element.composite.width = width;
        element.composite.height = height;
    }

    var target = element.composite.getContext("2d");
    target.setTransform(1, 0, 0, 1, 0, 0);
    target.clearRect(0, 0, width, height);
    target.setTransform(ratio, 0, 0, ratio, 0, 0);
    target.translate(-bounds.x, -bounds.y);
    // Sprite 自己的 graphics 与子节点都参与合成。
    paintElementContent(target, element);
    for (var index = 0; index < element.childList.length; index++) {
        blitElement(target, element.childList[index]);
    }
    applyEffects(element.composite, element, ratio);
    element.composite.__m8RasterVersion = ++rasterVersion;

    element.compositeBounds = bounds;
    element.painted = true;
    hostState.paintCount++;
}

function blitElement(target, element) {
    if (!element || element.expired || element.props.visible === false) {
        return;
    }

    if (element.projectedComposite) {
        target.save();
        applyElementBlendMode(target, element);
        var projected = element.compositeBounds;
        if (projected && element.composite) target.drawImage(element.composite, projected.x, projected.y, projected.width, projected.height);
        target.restore(); return;
    }
    // 被当作遮罩的元件自己不参与渲染（Flash 语义：遮罩对象不绘制）。
    if (isUsedAsMask(element)) {
        return;
    }

    if (element.childList.length > 0) {
        if (!element.composite) {
            rebuildComposite(element);
        }

        if (!element.composite) {
            return;
        }

        target.save();
        applyElementMaskClip(target, element);
        applyElementTransform(target, element);
        applyElementBlendMode(target, element);
        var compositeBoundsValue = element.compositeBounds || {
            x: 0,
            y: 0,
            width: element.composite.width / (window.devicePixelRatio || 1),
            height: element.composite.height / (window.devicePixelRatio || 1)
        };
        target.drawImage(
            element.composite,
            compositeBoundsValue.x,
            compositeBoundsValue.y,
            compositeBoundsValue.width,
            compositeBoundsValue.height);
        target.restore();
        return;
    }

    if (element.kind === "layer") {
        target.save();
        applyElementMaskClip(target, element);
        applyElementTransform(target, element);
        applyElementBlendMode(target, element);
        target.drawImage(element.layerCanvas, 0, 0, element.layerWidth, element.layerHeight);
        target.restore();
        return;
    }

    if (!element.painted || element.needsCache) {
        rebuildElementCache(element);
    }

    if (!element.cacheCanvas || !element.cacheBounds) {
        return;
    }

    target.save();
    applyElementMaskClip(target, element);
    applyElementTransform(target, element);
    applyElementBlendMode(target, element);
    target.drawImage(
        element.cacheCanvas,
        element.cacheBounds.x,
        element.cacheBounds.y,
        element.cacheBounds.width,
        element.cacheBounds.height);
    target.restore();
}

function isSubtreeDirty(element) {
    if (!element.painted || element.needsCache || element.compositeDirty
        || isElementDirty(element)) {
        return true;
    }

    for (var index = 0; index < element.childList.length; index++) {
        if (isSubtreeDirty(element.childList[index])) {
            return true;
        }
    }

    return false;
}

function clearSubtreeDirty(element) {
    element.propertyDirty = {};
    element.compositeDirty = false;
    for (var index = 0; index < element.childList.length; index++) {
        clearSubtreeDirty(element.childList[index]);
    }
}

// ---- 每帧流程：推进 tween → 标脏 → 只重绘脏元素 → 呈现 ----

function clearSurface() {
    if (!hostState.context2d || !hostState.canvas) {
        return;
    }

    hostState.context2d.save();
    hostState.context2d.setTransform(1, 0, 0, 1, 0, 0);
    hostState.context2d.clearRect(0, 0, hostState.canvas.width, hostState.canvas.height);
    hostState.context2d.restore();
}

// 把元素这一帧在主画布上占据的区域换算成主画布的像素矩形。
// 复合元素与 layer 元素的实际尺寸取决于它们自己的离屏画布，
// 其它元素取决于本地包围盒；再把本地包围盒的四个角按元素的变换
// （缩放 → 旋转 → 平移，与 applyElementTransform 同序）投到页面坐标，
// 取外接矩形。旋转时外接矩形会变大，宁可多擦一点也不能留残影。
function computeElementCanvasRect(element) {
    if (!element.painted || element.props.visible === false || element.expired) {
        return null;
    }

    var bounds;
    if (element.projectedComposite) {
        if (!element.composite || !element.compositeBounds) return null;
        bounds = element.compositeBounds;
    } else if (element.childList.length > 0) {
        if (!element.composite || !element.compositeBounds) {
            return null;
        }

        // 复合层的本地原点就是它自己的 compositeBounds 原点。
        bounds = element.compositeBounds;
    } else if (element.kind === "layer") {
        bounds = { x: 0, y: 0, width: element.layerWidth, height: element.layerHeight };
    } else if (element.cacheBounds) {
        bounds = element.cacheBounds;
    } else {
        return null;
    }

    var rect = element.projectedComposite ? bounds : compositeChildBounds(element, bounds);
    var minX = rect.x, minY = rect.y, maxX = rect.x + rect.width, maxY = rect.y + rect.height;

    var ratio = window.devicePixelRatio || 1;
    return {
        x: Math.floor((minX - DIRTY_RECT_PADDING) * ratio),
        y: Math.floor((minY - DIRTY_RECT_PADDING) * ratio),
        width: Math.ceil((maxX - minX + DIRTY_RECT_PADDING * 2) * ratio),
        height: Math.ceil((maxY - minY + DIRTY_RECT_PADDING * 2) * ratio)
    };
}

// 元素这一帧在主画布上实际占据的矩形（供下一帧擦除用）。
function recordElementRect(element) {
    element.lastPaintedRect = computeElementCanvasRect(element);
}

// 元素被隐藏 / 释放 / 摘除后，它在主画布上的旧像素必须被擦掉，
// 否则移动过的元素会在整条路径上留下拖影。
function retireElementRect(element) {
    var rect = element.lastPaintedRect;
    if (rect) {
        hostState.pendingEraseRects.push(rect);
        element.lastPaintedRect = null;
    }

    for (var index = 0; index < element.childList.length; index++) {
        retireElementRect(element.childList[index]);
    }
}

function rectsOverlap(a, b) {
    return !(a.x + a.width < b.x || b.x + b.width < a.x
        || a.y + a.height < b.y || b.y + b.height < a.y);
}

// 先擦掉「本帧移动过 / 已释放 / 已隐藏」的元素上一帧的包围盒，
// 再合成本帧的脏元素。顺序不能反：反了会把刚画好的像素擦掉。
function flushEraseRects(rects) {
    if (!hostState.context2d || !rects || rects.length === 0) {
        return;
    }
    hostState.context2d.save();
    hostState.context2d.setTransform(1, 0, 0, 1, 0, 0);
    for (var index = 0; index < rects.length; index++) {
        var rect = rects[index];
        hostState.context2d.clearRect(rect.x, rect.y, rect.width, rect.height);
    }

    hostState.context2d.restore();
}

// 嵌套元件的像素落在祖先的复合层里，只有「画到主画布上」的祖先才有
// lastPaintedRect。从元件自己往上找最近这样一个祖先，把它的旧矩形入队
// 擦除并让它本帧重烘重合成；整条链都没矩形就什么都不用做。
// 只在「摘除」路径调用（removeChildFromParent / detachElement）。
// 普通移动由 paintDirtyElements 统一收集旧、新范围并恢复相交图层。
function retirePaintedAncestorRect(element) {
    var current = element;
    while (current && current !== hostState.rootElement) {
        if (current.lastPaintedRect) {
            enqueueElementErase(current);
            current.compositeDirty = true;
            return;
        }

        current = current.treeParent;
    }
}

// 元素在主画布上的变换后位置变了（或它刚被隐藏），
// 上一帧的像素就要在下一帧被擦掉。retireElementRect 会清掉
// lastPaintedRect，所以同一帧内重复调用不会重复入队。
function enqueueElementErase(element) {
    retireElementRect(element);
}

// 元素移动/缩放/旋转/隐藏/释放时调用：把上帧包围盒入队擦除，
// 并让元素在下一帧重新合成（它自己或它所在的复合层）。
function markElementMoved(element) {
    if (!element || element.expired) {
        return;
    }

    // 只处理「自己有主画布矩形」的元件。嵌套元件的主画布矩形属于它的祖先，
    // 这里不能擦：普通移动只让父复合层重烘，祖先在主画布上的矩形仍然有效，
    // 擦了会在画布上留下空洞（祖先本帧不一定会重烘）。摘除另走
    // retirePaintedAncestorRect 的调用点。
    if (element.painted || element.lastPaintedRect) {
        enqueueElementErase(element);
    }

    hostState.dirty = true;
    var parent = element.treeParent;
    if (parent && parent !== hostState.rootElement) {
        // 子元素动了，父复合层要重烘；这里只标 compositeDirty，
        // 不动 parent.needsCache（后者会连带重建父元件的叶子缓存）。
        parent.compositeDirty = true;
    }
}

// ---- 舞台遮罩（M8 的 Player.setMask）----
//
// 语义：把整块脚本弹幕画布裁剪到 mask 元件的形状里（M8 里 setMask 设置的是
// 「播放器遮罩」，即视频区域的可见形状，弹幕只在其中出现）。
//
// 落点只有一个：composeElement。裁剪是**合成期**行为，元素自己的离屏缓存
// 不受影响，所以遮罩不会让任何元素重建位图（缓存只在内容变化时失效）。
// 擦除（flushEraseRects）与整屏清空（clearSurface）都**不带裁剪**：
// 它们要抹掉的是上一帧的像素，带上裁剪反而会留下旧内容。
function setStageMask(element) {
    var next = element && element.kind ? element : null;
    if (hostState.stageMaskElement === next) {
        return;
    }

    hostState.stageMaskElement = next;
    // 遮罩元件本身不参与渲染（M8 的遮罩对象不在显示列表里）。
    // 只从渲染树摘除，仍留在条目元素表里：它的变换照旧可用，
    // 条目回收时也会被一起释放。
    if (next && next.treeParent) {
        removeChildFromParent(next);
    }

    if (!hostState.context2d || !hostState.canvas) {
        return;
    }

    // 可见区域变了，主画布上已有像素全部作废：整屏清掉，
    // 再把所有元素标脏重合成一次（内容缓存不动，只重合成）。
    hostState.pendingEraseRects = [];
    clearSurface();
    markAllComposited();
}

// 把已登记元素的「上一帧呈现记录」作废，强制重合成一次。
// 只标脏、不清 needsCache：元素自身的位图没变，重建纯属浪费。
function markAllComposited() {
    for (var index = 0; index < hostState.elements.length; index++) {
        var element = hostState.elements[index];
        element.propertyDirty["*"] = true;
        element.lastPaintedRect = null;
    }

    hostState.dirty = true;
    if (hostState.visible) {
        ensureRunning();
    }
}

// 按 mask 元件的形状给 target 加上裁剪区。返回 true 表示加过 save，
// 调用方必须 restore。
function applyStageMask(target) {
    if (!hostState.stageMaskElement
        || hostState.stageMaskElement.expired
        || hostState.stageMaskElement.props.visible === false) {
        return false;
    }

    target.save();
    applyElementTransform(target, hostState.stageMaskElement);
    if (!traceElementClipPath(target, hostState.stageMaskElement)) {
        target.restore();
        return false;
    }

    target.clip();
    return true;
}

// 把元件的形状描进当前路径（只建路径，不填充不描边），供 clip() 使用。
// 形状类元件按它自己的图元描；文本 / 图片 / 复合元件退化成它的外接矩形
// （M8 里能用文本当遮罩，这里按矩形近似，见文档 §3 的说明）。
//
// map 是可选的「局部坐标 → 目标坐标」换算（缺省即恒等，表示当前
// 上下文的变换已经就位）。元素级遮罩传 createMaskPointMapper 的结果：
// 那条路径不能靠 canvas 的 save/restore 压变换（restore 会把裁剪一起
// 撤掉），所以把变换烘进坐标里。
function traceElementClipPath(target, element, map) {
    var mapper = map || null;
    var items = element.kind === "shape" ? element.shapeItems : null;
    if (!items || items.length === 0) {
        var bounds = elementLocalBounds(element);
        if (!bounds) {
            return false;
        }

        target.beginPath();
        tracePolygonPoints(target, [
            mapper ? mapper(bounds.x, bounds.y) : { x: bounds.x, y: bounds.y },
            mapper
                ? mapper(bounds.x + bounds.width, bounds.y)
                : { x: bounds.x + bounds.width, y: bounds.y },
            mapper
                ? mapper(bounds.x + bounds.width, bounds.y + bounds.height)
                : { x: bounds.x + bounds.width, y: bounds.y + bounds.height },
            mapper
                ? mapper(bounds.x, bounds.y + bounds.height)
                : { x: bounds.x, y: bounds.y + bounds.height }
        ], true);
        return true;
    }

    target.beginPath();
    var traced = false;
    for (var index = 0; index < items.length; index++) {
        if (traceShapeItemClipPath(target, items[index], mapper)) {
            traced = true;
        }
    }

    return traced;
}

// 折线/多边形路径：points 已换算到目标坐标系。
function tracePolygonPoints(target, points, close) {
    if (!points || points.length === 0) {
        return;
    }

    target.moveTo(points[0].x, points[0].y);
    for (var index = 1; index < points.length; index++) {
        target.lineTo(points[index].x, points[index].y);
    }

    if (close) {
        target.closePath();
    }
}

// 采样一条参数曲线成折线（圆/椭圆/扇形在有变换时不能直接用 arc）。
function traceSampledCurve(target, map, sampleCount, sampleFn) {
    var points = [];
    for (var index = 0; index <= sampleCount; index++) {
        var point = sampleFn(index / sampleCount);
        points.push(map ? map(point.x, point.y) : point);
    }

    tracePolygonPoints(target, points, true);
}

// 与 drawShapeItem 的几何部分一一对应，只是不落笔。
// mapper 非空时所有坐标都要先换算（见 traceElementClipPath 的说明）。
function traceShapeItemClipPath(target, item, mapper) {
    var map = mapper || null;
    var point = function (x, y) {
        return map ? map(x, y) : { x: x, y: y };
    };

    switch (item.kind) {
        case "path":
            traceGraphicsPath(target, item, point);
            return true;
        case "line":
        case "fill":
            if (!item.path || !item.path.points || item.path.points.length < 2) {
                return false;
            }

            var points = [];
            var pathIndex = 0;
            while (pathIndex < item.path.points.length) {
                points.push(point(
                    item.path.points[pathIndex],
                    item.path.points[pathIndex + 1]));
                pathIndex += 2;
            }

            tracePolygonPoints(target, points, false);
            return true;
        case "rect":
            tracePolygonPoints(target, [
                point(item.x, item.y),
                point(item.x + item.width, item.y),
                point(item.x + item.width, item.y + item.height),
                point(item.x, item.y + item.height)
            ], true);
            return true;
        case "roundRect":
            // 圆角在含旋转/错切的变换下精确换算要拆成 4 段椭圆弧；
            // 遮罩用途下按外接矩形近似（四角差别见文档 §3 的已知近似）。
            tracePolygonPoints(target, [
                point(item.x, item.y),
                point(item.x + item.width, item.y),
                point(item.x + item.width, item.y + item.height),
                point(item.x, item.y + item.height)
            ], true);
            return true;
        case "circle":
            traceSampledCurve(target, map, 32, function (t) {
                var angle = t * Math.PI * 2;
                return {
                    x: item.x + Math.cos(angle) * Math.abs(item.radius),
                    y: item.y + Math.sin(angle) * Math.abs(item.radius)
                };
            });
            return true;
        case "ellipse":
            traceSampledCurve(target, map, 32, function (t) {
                var angle = t * Math.PI * 2;
                return {
                    x: item.x + Math.cos(angle) * Math.abs(item.width) / 2,
                    y: item.y + Math.sin(angle) * Math.abs(item.height) / 2
                };
            });
            return true;
        case "wedge":
            var start = toFiniteNumber(item.startAngle, 0);
            var arc = toFiniteNumber(item.arc, 0);
            var wedgePoints = [point(item.x, item.y)];
            for (var step = 0; step <= 24; step++) {
                var wedgeAngle = start + arc * (step / 24);
                wedgePoints.push(point(
                    item.x + Math.cos(wedgeAngle) * Math.abs(item.radius),
                    item.y + Math.sin(wedgeAngle) * Math.abs(item.radius)));
            }

            tracePolygonPoints(target, wedgePoints, true);
            return true;
        case "polygon":
            if (!item.points || item.points.length < 4) {
                return false;
            }

            var polygonPoints = [];
            var polygonIndex = 0;
            while (polygonIndex < item.points.length) {
                polygonPoints.push(point(
                    item.points[polygonIndex],
                    item.points[polygonIndex + 1]));
                polygonIndex += 2;
            }

            tracePolygonPoints(target, polygonPoints, true);
            return true;
        default:
            return false;
    }
}

// 元素的 transform 命名空间（Flash DisplayObject.transform）。
function createElementTransform(element) {
    var transform = {
        perspectiveProjection: element === hostState.rootElement ? createPerspectiveProjection() : null,
        getRelativeMatrix3D: function (target) {
            var data = relativeMatrix3DData(element, target);
            return data ? createMatrix3D(data) : null;
        }
    };
    Object.defineProperty(transform, "matrix", {
        configurable: true, enumerable: true,
        get: function () {
            if (!element.props.matrix) {
                var m = localMatrix(element);
                element.props.matrix = createPlaceholderMatrix();
                element.props.matrix.a = m[0]; element.props.matrix.b = m[1];
                element.props.matrix.c = m[4]; element.props.matrix.d = m[5];
                element.props.matrix.tx = m[12]; element.props.matrix.ty = m[13];
            }
            return element.props.matrix;
        },
        set: function (value) {
            setPropertyInternal(element, "matrix", value, true);
            markPropertyDirty(element, "matrix"); hostState.dirty = true;
        }
    });
    Object.defineProperty(transform, "matrix3D", {
        configurable: true, enumerable: true,
        get: function () {
            if (!element.props.matrix3D && (element.props.z || element.props.rotationX || element.props.rotationY)) {
                element.props.matrix3D = createMatrix3D(localMatrix(element));
            }
            return element.props.matrix3D;
        },
        set: function (value) {
            if (value && value.rawData) {
                var d = value.rawData, p = element.props;
                p.matrix = null;
                p.x = d[12]; p.y = d[13]; p.z = d[14];
                p.scaleX = Math.hypot(d[0], d[1], d[2]); p.scaleY = Math.hypot(d[4], d[5], d[6]); p.scaleZ = Math.hypot(d[8], d[9], d[10]);
                p.rotationY = Math.asin(Math.max(-1, Math.min(1, -d[2] / (p.scaleX || 1)))) * 180 / Math.PI;
                p.rotationX = Math.atan2(d[6] / (p.scaleY || 1), d[10] / (p.scaleZ || 1)) * 180 / Math.PI;
                p.rotation = p.rotationZ = Math.atan2(d[1], d[0]) * 180 / Math.PI;
            } else {
                element.props.z = element.props.rotationX = element.props.rotationY = 0;
            }
            setPropertyInternal(element, "matrix3D", value, true);
            markPropertyDirty(element, "matrix3D"); hostState.dirty = true;
        }
    });
    Object.defineProperty(transform, "colorTransform", {
        configurable: true, enumerable: true,
        get: function () { return element.props.colorTransform; },
        set: function (value) {
            setPropertyInternal(element, "colorTransform", value, true);
            markPropertyDirty(element, "colorTransform"); hostState.dirty = true;
        }
    });
    return transform;
}

function relativeMatrix3DData(element, target) {
    return relativeMatrix(element, target);
}

// 只把元件落在 rect 里的那部分重画回主画布。
//
// 为什么需要它：擦除是按矩形做的无差别清除，被擦掉的像素属于哪些元件
// 是未知的。整元件重画（composeElement）虽然正确，但 Akari 的图层是
// 整视口 1280x720 的离屏 canvas——一个几十像素的擦除矩形碰到图层，
// 就得把整层重新 drawImage 一次，几十层叠加下每帧成本失控
// （实测直接把 headless 打到 tab crashed）。
// 按擦除矩形裁剪后重贴，代价只跟「被擦的那块面积」有关。
//
// rect 是设备像素（与 lastPaintedRect 同单位），而 blitElement 期望的
// 上下文已经带了 DPR 变换，所以裁剪前要把矩形换算回 CSS 像素。
function composeElementClipped(element, rect) {
    if (isUsedAsMask(element) || !rect) {
        return;
    }

    var ratio = window.devicePixelRatio || 1;
    var left = Math.max(0, rect.x / ratio);
    var top = Math.max(0, rect.y / ratio);
    var right = Math.min(hostState.canvas.width / ratio, (rect.x + rect.width) / ratio);
    var bottom = Math.min(hostState.canvas.height / ratio, (rect.y + rect.height) / ratio);
    if (!(right > left) || !(bottom > top)) {
        return;
    }

    hostState.context2d.save();
    hostState.context2d.beginPath();
    hostState.context2d.rect(left, top, right - left, bottom - top);
    hostState.context2d.clip();
    var masked = applyStageMask(hostState.context2d);
    blitElement(hostState.context2d, element);
    if (masked) {
        hostState.context2d.restore();
    }

    hostState.context2d.restore();
    // 刻意不覆盖呈现记录：裁剪只补了一小块，若把整元件矩形记成「已画过」，
    // 后续帧会认为它在画布上完好，遗漏处永远补不回来。
}

// 一组矩形的最小包围盒，供缓存边界和相交的擦除区域合并使用。
function unionRects(rects) {
    var left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
    var found = false;
    for (var i = 0; rects && i < rects.length; i++) {
        var r = rects[i];
        if (!r || !isFinite(r.x) || !isFinite(r.y) || !isFinite(r.width)
            || !isFinite(r.height) || r.width <= 0 || r.height <= 0) {
            continue;
        }

        found = true;
        if (r.x < left) left = r.x;
        if (r.y < top) top = r.y;
        if (r.x + r.width > right) right = r.x + r.width;
        if (r.y + r.height > bottom) bottom = r.y + r.height;
    }

    return found ? { x: left, y: top, width: right - left, height: bottom - top } : null;
}

function overlappingRects(rects, rect) {
    var hits = [];
    if (!rect || !isFinite(rect.x) || !isFinite(rect.width) || !rects) {
        return hits;
    }

    for (var i = 0; i < rects.length; i++) {
        var r = rects[i];
        if (r && isFinite(r.x) && isFinite(r.y) && isFinite(r.width)
            && isFinite(r.height) && r.width > 0 && r.height > 0
            && rectsOverlap(rect, r)) {
            hits.push(r);
        }
    }

    return hits;
}

function paintDirtyElements() {
    var topLevel = hostState.rootElement.childList;
    var damage = hostState.pendingEraseRects;
    hostState.pendingEraseRects = [];
    var index;
    for (index = 0; index < topLevel.length; index++) {
        var element = topLevel[index];
        element.rebuiltThisFrame = false; element.dirtyCandidate = false;
        if (element.expired || element.props.visible === false) continue;
        if (prepareElement(element)) {
            element.dirtyCandidate = true;
            if (element.lastPaintedRect) damage.push(element.lastPaintedRect);
            var nextRect = computeElementCanvasRect(element);
            if (nextRect) damage.push(nextRect);
        }
    }
    // 先清除整个重贴范围，再按显示列表顺序重贴所有相交元件。
    // 邻居先画、脏元素后画会颠倒层序；重贴未清掉的区域会累积半透明像素。
    var regions = mergeDamageRects(damage);
    if (!regions.length) return 0;
    flushEraseRects(regions);
    var painted = 0;
    for (index = 0; index < topLevel.length; index++) {
        var element = topLevel[index];
        if (!isElementVisible(element) || isUsedAsMask(element)) continue;
        var currentRect = computeElementCanvasRect(element);
        if (!currentRect) continue;
        var hits = overlappingRects(regions, currentRect);
        for (var j = 0; j < hits.length; j++) composeElementClipped(element, hits[j]);
        if (hits.length) { recordElementRect(element); painted++; }
    }
    return painted;
}

// 相交区域先合并，互不相交的区域保持分离，避免两个远处的小变更擦掉整屏。
function mergeDamageRects(rects) {
    var result = [];
    for (var i = 0; i < rects.length; i++) {
        var rect = rects[i];
        if (!rect || rect.width <= 0 || rect.height <= 0) continue;
        for (var j = 0; j < result.length;) {
            if (rectsOverlap(rect, result[j])) { rect = unionRects([rect, result.splice(j, 1)[0]]); j = 0; }
            else j++;
        }
        result.push(rect);
    }
    return result;
}

// 3D 子树保留每个叶子的世界矩阵，统一投影后再合成，避免先压成 2D 丢失 z。
function prepareProjected(element) {
    if (element.props.visible === false || element.props.alpha <= 0) {
        var hiddenDirty = isElementDirty(element) || element.compositeDirty;
        element.propertyDirty = {}; element.compositeDirty = false;
        return hiddenDirty;
    }
    var dirty = isElementDirty(element) || !element.painted || element.needsCache || element.compositeDirty || element.kind === "layer";
    // 没有 3D 子节点的容器是一张平面：先缓存其 2D 子树，再一次性投影。
    // 避免把几百个静止字形逐个上传纹理，也保留容器滤镜的本地坐标语义。
    var planar = element.childList.length > 0 && !element.childList.some(subtree3D);
    if (planar) {
        if (element.compositeBounds && rasterRatio(element, element.compositeBounds) !== element.compositeDpr) dirty = true;
        for (var i = 0; i < element.childList.length; i++) if (prepareElement(element.childList[i])) dirty = true;
        if (dirty || !element.projectedPlanar) rebuildComposite(element);
        element.projectedPlanar = true;
        element.propertyDirty = {}; element.needsCache = false; element.compositeDirty = false;
        element.painted = true;
        return dirty;
    }
    element.projectedPlanar = false;
    for (var i = 0; i < element.childList.length; i++) if (prepareProjected(element.childList[i])) dirty = true;
    var cacheScaleChanged = element.cacheBounds && rasterRatio(element, element.cacheBounds) !== element.cacheDpr;
    if (!element.painted || element.needsCache || cacheScaleChanged || element.kind === "layer") { rebuildElementCache(element); dirty = true; }
    element.propertyDirty = {}; element.needsCache = false; element.compositeDirty = false;
    element.painted = true;
    return dirty;
}

function projectedSubtreeBounds(element) {
    var rectangles = [], bounds = element.projectedPlanar ? element.compositeBounds : element.cacheBounds;
    if (bounds) {
        var matrix = worldMatrix(element), projection = projectionFor(element);
        var corners = [[bounds.x, bounds.y], [bounds.x + bounds.width, bounds.y],
            [bounds.x + bounds.width, bounds.y + bounds.height], [bounds.x, bounds.y + bounds.height]];
        var points = corners.map(function (p) { return projectPoint(transformPoint(matrix, p[0], p[1], 0), projection); });
        if (points.some(function (p) { return p.depth < 1; })) {
            rectangles.push({ x: 0, y: 0, width: hostState.viewportWidth, height: hostState.viewportHeight });
        } else {
            var xs = points.map(function (p) { return p.x; }), ys = points.map(function (p) { return p.y; });
            rectangles.push({ x: Math.min.apply(Math, xs), y: Math.min.apply(Math, ys),
                width: Math.max.apply(Math, xs) - Math.min.apply(Math, xs), height: Math.max.apply(Math, ys) - Math.min.apply(Math, ys) });
        }
    }
    for (var i = 0; !element.projectedPlanar && i < element.childList.length; i++) {
        var child = element.childList[i];
        if (child.expired || child.props.visible === false || child.props.alpha <= 0 || isUsedAsMask(child)) continue;
        var rect = projectedSubtreeBounds(child);
        if (rect) rectangles.push(rect);
    }
    var result = unionRects(rectangles);
    if (result && element.childList.length && !element.projectedPlanar) {
        var pad = filterPadding(element.props.filters);
        result = { x: result.x - pad.x, y: result.y - pad.y, width: result.width + pad.x * 2, height: result.height + pad.y * 2 };
    }
    return result;
}

function screenSurface(element, bounds, field) {
    var ratio = window.devicePixelRatio || 1;
    var left = Math.max(-256, Math.floor(bounds.x)), top = Math.max(-256, Math.floor(bounds.y));
    var right = Math.min(hostState.viewportWidth + 256, Math.ceil(bounds.x + bounds.width));
    var bottom = Math.min(hostState.viewportHeight + 256, Math.ceil(bounds.y + bounds.height));
    if (right <= left || bottom <= top) return null;
    bounds = { x: left, y: top, width: right - left, height: bottom - top };
    var canvas = element[field] || (element[field] = document.createElement("canvas"));
    var width = Math.ceil(bounds.width * ratio), height = Math.ceil(bounds.height * ratio);
    if (canvas.width !== width || canvas.height !== height) { canvas.width = width; canvas.height = height; }
    var context = canvas.getContext("2d"); context.setTransform(1, 0, 0, 1, 0, 0); context.clearRect(0, 0, width, height);
    context.setTransform(ratio, 0, 0, ratio, -bounds.x * ratio, -bounds.y * ratio);
    return { canvas: canvas, context: context, bounds: bounds };
}

function drawProjectedNode(target, element, alpha, skipEffects) {
    if (element.expired || element.props.visible === false || element.props.alpha <= 0 || isUsedAsMask(element)) return;
    var groupEffects = !element.projectedPlanar && element.childList.length && (element.props.colorTransform || (element.props.filters && element.props.filters.length));
    if (groupEffects && !skipEffects) {
        var bounds = projectedSubtreeBounds(element);
        var surface = bounds && screenSurface(element, bounds, "projectedEffectCanvas");
        if (!surface) return;
        flushProjectedBatch(target);
        beginProjectedBatch(surface.context);
        try { drawProjectedNode(surface.context, element, 1, true); } finally { endProjectedBatch(surface.context); }
        applyEffects(surface.canvas, element);
        target.save(); target.globalAlpha = alpha;
        applyElementBlendMode(target, element);
        target.drawImage(surface.canvas, surface.bounds.x, surface.bounds.y, surface.bounds.width, surface.bounds.height);
        target.restore(); return;
    }
    target.save();
    var mask = element.props.mask;
    if (mask) {
        flushProjectedBatch(target);
        var maskMatrix = worldMatrix(mask), projection = projectionFor(mask);
        target.beginPath();
        if (traceElementClipPath(target, mask, function (x, y) {
            return projectPoint(transformPoint(maskMatrix, x, y, 0), projection);
        })) target.clip();
    }
    var opacity = alpha * Math.max(0, Math.min(1, element.props.alpha));
    target.globalAlpha = opacity;
    applyElementBlendMode(target, element);
    if (element.projectedPlanar) {
        if (element.composite && element.compositeBounds) drawProjected(target, element.composite, element.compositeBounds, worldMatrix(element), projectionFor(element));
    } else {
        if (element.cacheCanvas && element.cacheBounds) drawProjected(target, element.cacheCanvas, element.cacheBounds, worldMatrix(element), projectionFor(element));
        for (var i = 0; i < element.childList.length; i++) drawProjectedNode(target, element.childList[i], opacity, false);
    }
    if (mask) flushProjectedBatch(target);
    target.restore();
}

function rebuildProjectedComposite(element) {
    var bounds = projectedSubtreeBounds(element);
    var surface = bounds && screenSurface(element, bounds, "composite");
    element.projectedComposite = true;
    element.compositeBounds = surface ? surface.bounds : null;
    if (surface) {
        beginProjectedBatch(surface.context);
        try { drawProjectedNode(surface.context, element, 1, false); } finally { endProjectedBatch(surface.context); }
    }
    else element.composite = null;
    element.painted = true; element.rebuiltThisFrame = true;
    hostState.paintCount++;
}

// 返回 true 表示该元素这一帧需要重新合成到画布上。
function prepareElement(element) {
    if (element.props.visible === false || element.props.alpha <= 0) {
        var hiddenDirty = isElementDirty(element) || element.compositeDirty;
        element.propertyDirty = {}; element.compositeDirty = false;
        return hiddenDirty;
    }
    if (element.treeParent === hostState.rootElement && subtree3D(element)) {
        var projectedDirty = prepareProjected(element);
        if (projectedDirty || !element.projectedComposite) rebuildProjectedComposite(element);
        return projectedDirty;
    }
    if (element.projectedComposite) {
        element.projectedComposite = false; element.composite = null; element.needsCache = true;
    }
    if (element.expired) {
        return false;
    }

    var childrenChanged = false;
    for (var index = 0; index < element.childList.length; index++) {
        if (prepareElement(element.childList[index])) {
            childrenChanged = true;
        }
    }

    var ownDirty = isElementDirty(element);
    var structural = !element.painted || element.needsCache;
    var scaleBounds = element.childList.length ? element.compositeBounds : element.cacheBounds;
    if (scaleBounds && rasterRatio(element, scaleBounds) !== (element.childList.length ? element.compositeDpr : element.cacheDpr)) structural = true;
    element.propertyDirty = {};
    element.needsCache = false;

    if (element.childList.length > 0) {
        // 复合元素只在结构真变时重烘：重建后 needsCache 必须清掉，
        // 否则它下一帧又会被判为结构脏，每帧白烘一整张视口层。
        if (structural || element.compositeDirty || childrenChanged) {
            rebuildComposite(element);
            element.compositeDirty = false;
            element.rebuiltThisFrame = true;
            return true;
        }

        return ownDirty;
    }

    if (element.kind === "layer") {
        // 脚本自绘层每帧都要合成，没有可缓存的静态内容。
        return true;
    }

    if (structural) {
        rebuildElementCache(element);
        element.rebuiltThisFrame = true;
        return true;
    }

    return ownDirty;
}

export {
    clearSurface,
    createElementTransform,
    displayObjectBounds,
    enqueueElementErase,
    ensureCanvas,
    markElementMoved,
    paintDirtyElements,
    resizeCanvas,
    retirePaintedAncestorRect,
    setStageMask,
    traceElementClipPath
};
