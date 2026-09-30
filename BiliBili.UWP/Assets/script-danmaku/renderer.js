// 画布绘制、位图缓存、遮罩与脏矩形合成。
import {
    DEFAULT_BOUNDS_PADDING,
    DIRTY_RECT_PADDING,
    GLOW_PADDING,
    container,
    hostState,
    normalizeColor,
    toFiniteNumber
} from "./core.js";
import {
    MATRIX3D_IDENTITY,
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
    var index = 2;
    while (index < points.length) {
        target.lineTo(points[index] + offsetX, points[index + 1] + offsetY);
        index += 2;
    }
}

function drawShapeItem(target, item, offsetX, offsetY) {
    var path;
    switch (item.kind) {
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

function computeShapeBounds(element) {
    var items = element.shapeItems || [];
    boundsReset();
    for (var index = 0; index < items.length; index++) {
        var item = items[index];
        var path;
        switch (item.kind) {
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

    return boundsResult(element.padding);
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
                width: Math.max(1, element.width),
                height: Math.max(1, element.height)
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
    var props = element.props;
    var matrix = props.matrix;
    target.translate(props.x, props.y);
    if (props.rotation) {
        target.rotate(props.rotation * Math.PI / 180);
    }

    if (matrix && typeof matrix === "object") {
        target.transform(
            toFiniteNumber(matrix.a, 1),
            toFiniteNumber(matrix.b, 0),
            toFiniteNumber(matrix.c, 0),
            toFiniteNumber(matrix.d, 1),
            toFiniteNumber(matrix.tx, 0),
            toFiniteNumber(matrix.ty, 0));
    }

    target.scale(props.scaleX, props.scaleY);
    target.globalAlpha = Math.max(0, Math.min(1, props.alpha));
}

function hasGlowFilter(element) {
    var filters = element.props.filters;
    if (!filters || !filters.length) {
        return false;
    }

    for (var index = 0; index < filters.length; index++) {
        var filter = filters[index];
        var name = filter && (filter.type || filter.kind);
        if (name === "GlowFilter" || name === "Glow") {
            return true;
        }
    }

    return false;
}

// 内容绘制：把元素自己的图元画到给定的 2D 上下文（本地坐标系）。
function paintElementContent(target, element) {
    if (element.kind === "shape") {
        drawShapeElement(target, element, 0, 0);
    } else if (element.kind === "text") {
        drawTextElement(target, element, 0, 0);
    } else if (element.kind === "image" && element.imageValue) {
        target.drawImage(element.imageValue, 0, 0);
    } else if (element.kind === "layer") {
        target.drawImage(element.layerCanvas, 0, 0);
    }

    // 发光滤镜在缓存阶段一次性施加：脚本只画一次，滤镜不必每帧重算。
    //
    // 这是**近似**，两处已知偏离（都不是移植疏漏，是保留模式的取舍）：
    //  ① 衰减形状：原版是取 alpha 通道做高斯模糊后按 color/alpha/strength
    //     着色，再与原图叠加；宿主用 blur + lighter 叠加近似。要更接近得把
    //     原图渲到离屏、转 alpha mask、多次 box-blur，成本远大于收益。
    //  ② 着色：原版 CommentConfig.getFilterByColor(color) 只在**黑/白**
    //     两档里选（color != 0 → 白 glow，否则黑 glow），脚本传给
    //     $.createGlowFilter 的颜色则完全被忽略。宿主取元素的显示色，
    //     等于把「白字白 glow、黑字黑 glow」这一档算对了，其余颜色会比
    //     原版亮一些——这是刻意的：按两档猜色反而会在彩色文字上更失真。
    if (hasGlowFilter(element) && typeof target.filter === "string") {
        target.filter = "blur(" + glowFilterRadius(element) + "px)";
        target.globalCompositeOperation = "lighter";
        paintElementContentRaw(target, element);
        target.globalCompositeOperation = "source-over";
        target.filter = "none";
    }
}

// 取第一个 GlowFilter 的模糊半径（第三参 blurX）。
// 原版文本那两档是 4（重墨）/ 3（描边），缺省按 4 走。
function glowFilterRadius(element) {
    var filters = element.props.filters;
    if (!filters || !filters.length) {
        return 4;
    }

    for (var index = 0; index < filters.length; index++) {
        var name = filters[index] && (filters[index].type || filters[index].kind);
        if (name === "GlowFilter" || name === "Glow") {
            return Math.max(1, toFiniteNumber(filters[index].blurX, 4));
        }
    }

    return 4;
}

function paintElementContentRaw(target, element) {
    if (element.kind === "shape") {
        drawShapeElement(target, element, 0, 0);
    } else if (element.kind === "text") {
        drawTextElement(target, element, 0, 0);
    } else if (element.kind === "image" && element.imageValue) {
        target.drawImage(element.imageValue, 0, 0);
    }
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

    var padding = hasGlowFilter(element) ? GLOW_PADDING : 0;
    var ratio = window.devicePixelRatio || 1;
    var width = Math.max(1, Math.ceil((bounds.width + padding * 2) * ratio));
    var height = Math.max(1, Math.ceil((bounds.height + padding * 2) * ratio));
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
    target.translate(padding - bounds.x, padding - bounds.y);
    target.globalAlpha = 1;
    paintElementContent(target, element);

    element.cacheBounds = {
        x: bounds.x - padding,
        y: bounds.y - padding,
        width: bounds.width + padding * 2,
        height: bounds.height + padding * 2
    };
    element.painted = true;
    hostState.paintCount++;
}

// 复合元素的子树烘焙。子元素走 blitElement（用自己的缓存），
// 所以重建复合层是 N 次位图拷贝，而不是 N 份绘制代码。
// 复合层的实际尺寸取决于子树内容（子元素坐标相对父元件注册点）。
// 返回 null 表示子树当前没有可绘制内容（空容器首帧不建复合层）。
function computeCompositeBounds(element) {
    boundsReset();
    for (var index = 0; index < element.childList.length; index++) {
        var child = element.childList[index];
        if (child.expired || child.props.visible === false) {
            continue;
        }

        if (child.childList.length > 0) {
            var childBounds = computeCompositeBounds(child);
            if (!childBounds) {
                continue;
            }

            var childTransform = compositeChildBounds(child, childBounds);
            boundsAdd(childTransform.x, childTransform.y);
            boundsAdd(
                childTransform.x + childTransform.width,
                childTransform.y + childTransform.height);
            continue;
        }

        var bounds = elementLocalBounds(child);
        if (!bounds) {
            continue;
        }

        var transformed = compositeChildBounds(child, bounds);
        boundsAdd(transformed.x, transformed.y);
        boundsAdd(
            transformed.x + transformed.width,
            transformed.y + transformed.height);
    }

    return boundsResult(0);
}

// 子元素在父元件坐标系里的外接矩形（缩放 → 旋转 → 平移，
// 与 applyElementTransform 同序）。
function compositeChildBounds(child, bounds) {
    var props = child.props;
    var scaleX = toFiniteNumber(props.scaleX, 1);
    var scaleY = toFiniteNumber(props.scaleY, 1);
    var rotation = toFiniteNumber(props.rotation, 0) * Math.PI / 180;
    var cos = Math.cos(rotation);
    var sin = Math.sin(rotation);
    var corners = [
        [bounds.x, bounds.y],
        [bounds.x + bounds.width, bounds.y],
        [bounds.x + bounds.width, bounds.y + bounds.height],
        [bounds.x, bounds.y + bounds.height]
    ];

    var minX = Infinity;
    var minY = Infinity;
    var maxX = -Infinity;
    var maxY = -Infinity;
    for (var index = 0; index < corners.length; index++) {
        var localX = corners[index][0] * scaleX;
        var localY = corners[index][1] * scaleY;
        var pageX = localX * cos - localY * sin + props.x;
        var pageY = localX * sin + localY * cos + props.y;
        if (pageX < minX) {
            minX = pageX;
        }

        if (pageY < minY) {
            minY = pageY;
        }

        if (pageX > maxX) {
            maxX = pageX;
        }

        if (pageY > maxY) {
            maxY = pageY;
        }
    }

    return { x: minX, y: minY, width: maxX - minX, height: maxY - minY };
}

function rebuildComposite(element) {
    var bounds = computeCompositeBounds(element);
    element.compositeDirty = false;
    if (!bounds) {
        // 子树当前没有内容：不建/不重建复合层。保留已有位图（若有），
        // 位置也不改——空容器在屏幕上本来就不占像素。
        return;
    }

    // 子元素坐标相对父元件注册点：烘焙时把 bounds 原点平移到
    // (padding, padding)，blit 时再按 bounds 的偏移取用，
    // 这样父元件自己移动/缩放/旋转时子树不必重绘（见 D4）。
    var ratio = window.devicePixelRatio || 1;
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
    for (var index = 0; index < element.childList.length; index++) {
        blitElement(target, element.childList[index]);
    }

    element.compositeBounds = bounds;
    element.painted = true;
    hostState.paintCount++;
}

function blitElement(target, element) {
    if (!element || element.expired || element.props.visible === false) {
        return;
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
    if (element.childList.length > 0) {
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

    var props = element.props;
    var scaleX = toFiniteNumber(props.scaleX, 1);
    var scaleY = toFiniteNumber(props.scaleY, 1);
    var rotation = toFiniteNumber(props.rotation, 0) * Math.PI / 180;
    var cos = Math.cos(rotation);
    var sin = Math.sin(rotation);
    var corners = [
        [bounds.x, bounds.y],
        [bounds.x + bounds.width, bounds.y],
        [bounds.x + bounds.width, bounds.y + bounds.height],
        [bounds.x, bounds.y + bounds.height]
    ];

    var minX = Infinity;
    var minY = Infinity;
    var maxX = -Infinity;
    var maxY = -Infinity;
    for (var index = 0; index < corners.length; index++) {
        var localX = corners[index][0] * scaleX;
        var localY = corners[index][1] * scaleY;
        var pageX = localX * cos - localY * sin + props.x;
        var pageY = localX * sin + localY * cos + props.y;
        if (pageX < minX) {
            minX = pageX;
        }

        if (pageY < minY) {
            minY = pageY;
        }

        if (pageX > maxX) {
            maxX = pageX;
        }

        if (pageY > maxY) {
            maxY = pageY;
        }
    }

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

function intersectsAnyEraseRect(rect) {
    for (var index = 0; index < hostState.pendingEraseRects.length; index++) {
        if (rectsOverlap(rect, hostState.pendingEraseRects[index])) {
            return true;
        }
    }

    return false;
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
// 普通移动不能走这里：祖先的主画布矩形那时仍然有效，擦了会在画布上
// 留下空洞——实测把一段真作品的画面打薄了一半。
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
        // perspectiveProjection 是纯数据对象；M8 的 clone() 明确不复制函数，
        // 脚本也是 clone 出来当数据读，所以给默认值的普通对象即可。
        perspectiveProjection: createPerspectiveProjection(),
        // 相对变换矩阵：脚本用它做 3D 深度排序。本宿主是 2D 合成，
        // 按「目标元件到自己」的累计 2D 变换（平移/缩放/旋转）拼出一个
        // Matrix3D——z 分量恒为 0，排序退化成稳定的层序（不报错、不崩），
        // 这正是 2D 画布下的正确近似（见文档 §3 的已知近似）。
        getRelativeMatrix3D: function (target) {
            return createMatrix3D(relativeMatrix3DData(element, target));
        }
    };

    Object.defineProperty(transform, "matrix", {
        configurable: true,
        enumerable: true,
        get: function () {
            if (!element.props.matrix) {
                element.props.matrix = createPlaceholderMatrix();
            }

            return element.props.matrix;
        },
        set: function (value) {
            setPropertyInternal(element, "matrix", value, true);
            markPropertyDirty(element, "matrix");
            hostState.dirty = true;
        }
    });

    // matrix3D / colorTransform：Flash 的 3D 变换与颜色变换。
    // 本宿主是 2D 画布，这两个只存储、不参与呈现——但**必须可读可写**，
    // entry_08 大量做 `x.transform.matrix3D = y.transform.matrix3D` 的拷贝，
    // 读回 undefined 会让后续 `mat.append(...)` 直接崩。
    Object.defineProperty(transform, "matrix3D", {
        configurable: true,
        enumerable: true,
        get: function () {
            return element.props.matrix3D;
        },
        set: function (value) {
            element.props.matrix3D = value;
            hostState.dirty = true;
        }
    });

    Object.defineProperty(transform, "colorTransform", {
        configurable: true,
        enumerable: true,
        get: function () {
            return element.props.colorTransform;
        },
        set: function (value) {
            element.props.colorTransform = value;
            hostState.dirty = true;
        }
    });

    return transform;
}

// 累计「从 target 到 element」的 2D 变换，铺成 16 元 Matrix3D rawData。
// target 为空或不在祖先链上时退化成元素自己的变换。
function relativeMatrix3DData(element, target) {
    var accum = createPlaceholderMatrix();
    var current = element;
    while (current && current !== target) {
        var props = current.props;
        accum.translate(toFiniteNumber(props.x, 0), toFiniteNumber(props.y, 0));
        if (props.rotation) {
            accum.rotate(toFiniteNumber(props.rotation, 0) * Math.PI / 180);
        }

        accum.scale(toFiniteNumber(props.scaleX, 1), toFiniteNumber(props.scaleY, 1));
        current = current.treeParent;
    }

    var data = MATRIX3D_IDENTITY.slice();
    data[0] = accum.a;
    data[1] = accum.b;
    data[4] = accum.c;
    data[5] = accum.d;
    data[12] = accum.tx;
    data[13] = accum.ty;
    return data;
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
    element.compositeDirty = true;
}

// 元件自身或任一祖先的不透明度是否已接近 0。淡出中的元件补画它是白画：
// 它下一帧就消失，补回来的像素又要再擦一次（实测尾部 135s 因此从基线的
// 0.005 抬到 0.058）。
function isFadingOut(element) {
    var current = element;
    while (current && current !== hostState.rootElement) {
        if (toFiniteNumber(current.props.alpha, 1) <= 0.05) {
            return true;
        }

        current = current.treeParent;
    }

    return false;
}

// 一组矩形的最小包围盒：把「多个擦除矩形命中同一元件」合并为一次重贴，
// 保证重贴次数与基线一致（逐块重贴会把调用次数放大到 hits.length 倍）。
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

function composeElement(element) {
    // 被当作遮罩的元件不参与合成——连「呈现记录」都不该留
    // （否则它的 lastPaintedRect 会被后续的擦除/邻居补画逻辑当成真画过）。
    if (isUsedAsMask(element)) {
        return;
    }

    // 遮罩在合成期施加：只影响「这一帧画到主画布上的可见范围」，
    // 元素自己的离屏缓存与包围盒都不变。
    var clipped = applyStageMask(hostState.context2d);
    blitElement(hostState.context2d, element);
    if (clipped) {
        hostState.context2d.restore();
    }

    recordElementRect(element);
}

function paintDirtyElements() {
    var topLevel = hostState.rootElement.childList;
    var candidates = [];
    var index;
    for (index = 0; index < topLevel.length; index++) {
        var element = topLevel[index];
        element.rebuiltThisFrame = false;
        element.dirtyCandidate = false;
        if (element.expired || element.props.visible === false) {
            continue;
        }

        if (prepareElement(element)) {
            element.dirtyCandidate = true;
            candidates.push(element);
        }
    }

    var erasedRects = hostState.pendingEraseRects;
    hostState.pendingEraseRects = [];
    flushEraseRects(erasedRects);

    // 擦除是元素级的无差别矩形，可能盖住了别的（静止的）元素：
    // 先补画「被擦到但不是本帧脏元素」的邻居，再合成脏元素，
    // 这样既不留洞，也不会把层叠顺序反过来。
    for (index = 0; index < topLevel.length; index++) {
        var neighbor = topLevel[index];
        if (neighbor.dirtyCandidate || !isElementVisible(neighbor)) {
            continue;
        }

        var neighborHitBox = unionRects(
            overlappingRects(erasedRects, neighbor.lastPaintedRect));
        if (neighborHitBox) {
            composeElementClipped(neighbor, neighborHitBox);
        }
    }

    var painted = 0;
    for (index = 0; index < candidates.length; index++) {
        var candidate = candidates[index];
        if (!isElementVisible(candidate)) {
            continue;
        }

        // 位置与上一帧完全一致、内容也没重建的元素不必再合成。
        // 这是「静态元素首帧之后不再重绘」的落点。
        // （本帧移动过的元素 lastPaintedRect 已被清空，一定会走到合成。）
        if (!candidate.rebuiltThisFrame && candidate.lastPaintedRect
            && rectsEqual(candidate.lastPaintedRect, computeElementCanvasRect(candidate))) {
            // 矩形没变不等于画布上还有它：本帧的擦除矩形可能正盖在它上面，
            // 那些像素已经被清掉。按命中矩形的并集裁剪重贴一次即可。
            var candidateHitBox = unionRects(
                overlappingRects(erasedRects, candidate.lastPaintedRect));
            if (candidateHitBox && !isFadingOut(candidate)) {
                composeElementClipped(candidate, candidateHitBox);
            }

            continue;
        }

        composeElement(candidate);
        painted++;
    }

    return painted;
}

function intersectsAny(rects, rect) {
    if (!rect) {
        return false;
    }

    for (var index = 0; index < rects.length; index++) {
        if (rects[index] && rectsOverlap(rect, rects[index])) {
            return true;
        }
    }

    return false;
}

function rectsEqual(a, b) {
    if (!a || !b) {
        return a === b;
    }

    return a.x === b.x && a.y === b.y
        && a.width === b.width && a.height === b.height;
}

// 返回 true 表示该元素这一帧需要重新合成到画布上。
function prepareElement(element) {
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
    enqueueElementErase,
    ensureCanvas,
    markElementMoved,
    paintDirtyElements,
    resizeCanvas,
    retirePaintedAncestorRect,
    setStageMask,
    traceElementClipPath
};
