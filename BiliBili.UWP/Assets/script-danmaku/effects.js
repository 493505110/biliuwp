// 滤镜按数组顺序作用于整份元件位图（包括容器的子树），不只处理叶子图元。
import { toFiniteNumber } from "./core.js";
import { applyEffectsGPU } from "./effects-gpu.js";

function filterPadding(filters) {
    var x = 0, y = 0;
    for (var i = 0; filters && i < filters.length; i++) {
        var f = filters[i];
        var kind = f.kind || f.type;
        var gradient = kind === "GradientGlowFilter" || kind === "GradientBevelFilter";
        if (gradient && (!gradientStops(f).length || toFiniteNumber(f.strength, 1) <= 0)) continue;
        if (!["GlowFilter", "BlurFilter", "DropShadowFilter", "BevelFilter", "GradientGlowFilter", "GradientBevelFilter"].includes(kind)) continue;
        var quality = Math.max(1, Math.min(15, Math.floor(toFiniteNumber(f.quality, 1))));
        if (f.inner || ((kind === "BevelFilter" || gradient) && f.type === "inner")) continue;
        var angle = toFiniteNumber(f.angle, 0) * Math.PI / 180, distance = toFiniteNumber(f.distance, 0);
        x += Math.ceil(Math.max(0, toFiniteNumber(f.blurX, 0)) / 2) * quality + Math.ceil(Math.abs(distance * Math.cos(angle))) + 1;
        y += Math.ceil(Math.max(0, toFiniteNumber(f.blurY, 0)) / 2) * quality + Math.ceil(Math.abs(distance * Math.sin(angle))) + 1;
    }
    return { x: x, y: y };
}

// 固定大小滑动窗口，透明边界按零处理。Flash 的 quality 表示重复模糊次数。
function blurPass(input, width, height, channels, kernel, horizontal) {
    if (kernel <= 1) return input;
    var radius = (kernel - 1) / 2, whole = Math.floor(radius), fraction = radius - whole;
    var out = new Float32Array(input.length);
    var length = horizontal ? width : height, lines = horizontal ? height : width;
    var stride = horizontal ? channels : width * channels;
    var divisor = kernel;
    for (var line = 0; line < lines; line++) for (var channel = 0; channel < channels; channel++) {
        var base = (horizontal ? line * width * channels : line * channels) + channel, sum = 0;
        for (var j = 0; j <= Math.min(whole, length - 1); j++) sum += input[base + j * stride];
        for (var pos = 0; pos < length; pos++) {
            var low = pos - whole - 1, high = pos + whole + 1;
            var edges = (low >= 0 ? input[base + low * stride] : 0) + (high < length ? input[base + high * stride] : 0);
            // channels=4 的 alpha 用0–1表示，其余通道用0–255表示。
            var precision = channels === 1 || channel === 3 ? 255 : 1;
            out[base + pos * stride] = Math.floor((sum + fraction * edges) / divisor * precision + 0.0001) / precision;
            var remove = pos - whole, add = pos + whole + 1;
            if (remove >= 0) sum -= input[base + remove * stride];
            if (add < length) sum += input[base + add * stride];
        }
    }
    return out;
}

function blur(input, width, height, channels, filter, ratio) {
    var x = Math.max(1, Math.min(255, Math.max(0, filter.blurX) * ratio));
    var y = Math.max(1, Math.min(255, Math.max(0, filter.blurY) * ratio));
    var quality = Math.max(1, Math.min(15, Math.floor(toFiniteNumber(filter.quality, 1))));
    for (var i = 0; i < quality; i++) {
        input = blurPass(input, width, height, channels, x, true);
        input = blurPass(input, width, height, channels, y, false);
    }
    return input;
}

function applyFilters(canvas, filters, ratio) {
    if (!filters || !filters.length) return;
    var context = canvas.getContext("2d"), width = canvas.width, height = canvas.height;
    // 位移的边界是输入位图，不包含后续滤镜预留的透明扩边。
    var content = canvas.__m8FilterContent || { x: 0, y: 0, width: width / ratio, height: height / ratio };
    var region = { left: Math.max(0, Math.floor(content.x * ratio)), top: Math.max(0, Math.floor(content.y * ratio)),
        right: Math.min(width, Math.ceil((content.x + content.width) * ratio)), bottom: Math.min(height, Math.ceil((content.y + content.height) * ratio)) };
    for (var index = 0; index < filters.length; index++) {
        var filter = filters[index];
        var kind = filter.kind || filter.type;
        if (kind === "DisplacementMapFilter") {
            applyDisplacement(context, width, height, filter, ratio, region);
            continue;
        }
        var padding = filterPadding([filter]);
        region = { left: Math.max(0, region.left - Math.ceil(padding.x * ratio)), top: Math.max(0, region.top - Math.ceil(padding.y * ratio)),
            right: Math.min(width, region.right + Math.ceil(padding.x * ratio)), bottom: Math.min(height, region.bottom + Math.ceil(padding.y * ratio)) };
        if (kind === "GradientGlowFilter" || kind === "GradientBevelFilter") {
            applyGradient(context, width, height, filter, ratio, kind);
            continue;
        }
        if (kind === "ColorMatrixFilter" || kind === "ConvolutionFilter") {
            applyPixelFilter(context, width, height, filter, kind);
            continue;
        }
        if (kind === "BevelFilter") {
            applyBevel(context, width, height, filter, ratio);
            continue;
        }
        if (kind !== "GlowFilter" && kind !== "BlurFilter" && kind !== "DropShadowFilter") continue;
        var image = context.getImageData(0, 0, width, height), data = image.data;
        // 完全透明的输入仍然透明，不分配模糊缓冲或逐像素合成。
        var covered = false;
        for (var alphaIndex = 3; alphaIndex < data.length; alphaIndex += 4) {
            if (data[alphaIndex]) { covered = true; break; }
        }
        if (!covered) continue;
        var channels = kind === "BlurFilter" ? 4 : 1;
        var input = new Float32Array(width * height * channels);
        for (var i = 0, pixel = 0; i < data.length; i += 4, pixel++) {
            var alpha = data[i + 3] / 255;
            if (channels === 4) {
                input[i] = data[i] * alpha; input[i + 1] = data[i + 1] * alpha;
                input[i + 2] = data[i + 2] * alpha; input[i + 3] = alpha;
            } else input[pixel] = alpha;
        }
        var blurred = blur(input, width, height, channels, filter, ratio);
        var angle = toFiniteNumber(filter.angle, 0) * Math.PI / 180;
        var dx = kind === "DropShadowFilter" ? toFiniteNumber(filter.distance, 4) * Math.cos(angle) * ratio : 0;
        var dy = kind === "DropShadowFilter" ? toFiniteNumber(filter.distance, 4) * Math.sin(angle) * ratio : 0;
        var color = filter.color >>> 0, rgb = [color >> 16 & 255, color >> 8 & 255, color & 255];
        var strength = Math.max(0, toFiniteNumber(filter.strength, 2));
        for (i = 0, pixel = 0; i < data.length; i += 4, pixel++) {
            if (channels === 4) {
                var a = blurred[i + 3];
                for (var c = 0; c < 3; c++) data[i + c] = a > 0 ? blurred[i + c] / a : 0;
                data[i + 3] = a * 255;
            } else {
                var original = data[i + 3] / 255;
                var mask = sampleAlpha(blurred, width, height, pixel % width - dx, Math.floor(pixel / width) - dy, 0);
                // 内发光/阴影在模糊后取补集，缓存外的透明区域也属于补集。
                var glow = Math.min(1, (filter.inner ? 1 - mask : mask) * strength) * Math.max(0, Math.min(1, filter.alpha));
                var ga = filter.inner ? glow * original : glow;
                if (filter.knockout || filter.hideObject) {
                    if (!filter.inner && filter.knockout) ga *= 1 - original;
                    for (c = 0; c < 3; c++) data[i + c] = rgb[c];
                    data[i + 3] = ga * 255;
                } else {
                    var outAlpha = filter.inner ? original : original + ga * (1 - original);
                    for (c = 0; c < 3; c++) {
                        var value = filter.inner ? (data[i + c] * (1 - glow) + rgb[c] * glow) * original
                            : data[i + c] * original + rgb[c] * ga * (1 - original);
                        data[i + c] = outAlpha > 0 ? value / outAlpha : 0;
                    }
                    data[i + 3] = outAlpha * 255;
                }
            }
        }
        context.putImageData(image, 0, 0);
    }
}

function sampleAlpha(data, width, height, x, y, outside) {
    var left = Math.floor(x), top = Math.floor(y), tx = x - left, ty = y - top;
    function get(px, py) { return px < 0 || py < 0 || px >= width || py >= height ? outside : data[py * width + px]; }
    return (get(left, top) * (1 - tx) + get(left + 1, top) * tx) * (1 - ty)
        + (get(left, top + 1) * (1 - tx) + get(left + 1, top + 1) * tx) * ty;
}

function gradientStops(filter) {
    var colors = filter.colors || [], alphas = filter.alphas || [], ratios = filter.ratios || [];
    var stops = [];
    for (var i = 0; i < Math.min(colors.length, alphas.length, ratios.length); i++) {
        var color = colors[i] >>> 0;
        stops.push({ ratio: Math.max(0, Math.min(255, toFiniteNumber(ratios[i], 0))),
            rgba: [color >> 16 & 255, color >> 8 & 255, color & 255, Math.max(0, Math.min(1, toFiniteNumber(alphas[i], 0)))] });
    }
    return stops.sort(function (a, b) { return a.ratio - b.ratio; });
}

function gradientTable(stops) {
    var table = new Float32Array(256 * 4), index = 0;
    for (var value = 0; value < 256; value++) {
        // 相同 ratio 使用最后一个色标，不除以零；两端沿用端点颜色。
        while (index + 1 < stops.length && stops[index + 1].ratio <= value) index++;
        var first = stops[index], next = stops[Math.min(index + 1, stops.length - 1)];
        var fraction = value <= first.ratio || next.ratio === first.ratio ? 0 : Math.min(1, (value - first.ratio) / (next.ratio - first.ratio));
        for (var c = 0; c < 4; c++) table[value * 4 + c] = first.rgba[c] + (next.rgba[c] - first.rgba[c]) * fraction;
    }
    return table;
}

function applyGradient(context, width, height, filter, ratio, kind) {
    var stops = gradientStops(filter), strength = Math.max(0, toFiniteNumber(filter.strength, 1));
    if (!stops.length || !strength) return;
    var table = gradientTable(stops), image = context.getImageData(0, 0, width, height), data = image.data;
    var input = new Float32Array(width * height);
    for (var i = 0; i < input.length; i++) input[i] = data[i * 4 + 3] / 255;
    var blurred = blur(input, width, height, 1, filter, ratio);
    var angle = toFiniteNumber(filter.angle, 45) * Math.PI / 180;
    var dx = toFiniteNumber(filter.distance, 4) * Math.cos(angle) * ratio, dy = toFiniteNumber(filter.distance, 4) * Math.sin(angle) * ratio;
    for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) {
        var pixel = y * width + x, offset = pixel * 4, original = input[pixel];
        var behind = sampleAlpha(blurred, width, height, x - dx, y - dy, 0);
        // Glow 以模糊 alpha 查 0..255 色标；Bevel 以高光/阴影差查表，128 是基色。
        var value = kind === "GradientGlowFilter" ? behind * strength * 255
            : 128 + (behind - sampleAlpha(blurred, width, height, x + dx, y + dy, 0)) * strength * 128;
        var entry = Math.max(0, Math.min(255, Math.round(value))) * 4;
        var effectAlpha = table[entry + 3], baseAlpha = filter.knockout ? 0 : original;
        if (filter.type === "inner") effectAlpha *= original;
        else if (filter.type === "outer") effectAlpha *= 1 - original;
        var outAlpha = effectAlpha + baseAlpha * (1 - effectAlpha);
        // inner 必须保留输入的覆盖率，不能把半透明边缘二次叠加变实。
        if (filter.type === "inner" && !filter.knockout) {
            outAlpha = original;
            baseAlpha = original * (1 - table[entry + 3]);
        } else if (filter.type === "outer" && !filter.knockout) {
            outAlpha = original + effectAlpha;
        } else baseAlpha *= 1 - effectAlpha;
        for (var c = 0; c < 3; c++) data[offset + c] = outAlpha ? (table[entry + c] * effectAlpha + data[offset + c] * baseAlpha) / outAlpha : 0;
        data[offset + 3] = outAlpha * 255;
    }
    context.putImageData(image, 0, 0);
}

function applyDisplacement(context, width, height, filter, ratio, region) {
    var bitmap = filter.mapBitmap, map = bitmap && !bitmap.disposed && bitmap.canvas;
    if (!map || !map.width || !map.height || region.right <= region.left || region.bottom <= region.top) return;
    var mapData = map.getContext("2d").getImageData(0, 0, map.width, map.height).data;
    var image = context.getImageData(0, 0, width, height), data = image.data, input = data.slice();
    var point = filter.mapPoint || { x: 0, y: 0 };
    var pointX = toFiniteNumber(point.x, 0), pointY = toFiniteNumber(point.y, 0);
    var channels = { 1: 0, 2: 1, 4: 2, 8: 3 }, channelX = channels[filter.componentX], channelY = channels[filter.componentY];
    var scaleX = toFiniteNumber(filter.scaleX, 0) * ratio / 256, scaleY = toFiniteNumber(filter.scaleY, 0) * ratio / 256;
    var mode = filter.mode || "wrap", spanX = region.right - region.left, spanY = region.bottom - region.top;
    var color = filter.color >>> 0, edge = [color >> 16 & 255, color >> 8 & 255, color & 255, Math.max(0, Math.min(1, toFiniteNumber(filter.alpha, 0))) * 255];
    function wrapped(value, start, span) { return start + ((value - start) % span + span) % span; }
    function sample(px, py, c) {
        if (mode === "wrap") { px = wrapped(px, region.left, spanX); py = wrapped(py, region.top, spanY); }
        else { px = Math.max(region.left, Math.min(region.right - 1, px)); py = Math.max(region.top, Math.min(region.bottom - 1, py)); }
        var offset = (py * width + px) * 4;
        return c === 3 ? input[offset + 3] : input[offset + c] * input[offset + 3] / 255;
    }
    for (var y = region.top; y < region.bottom; y++) for (var x = region.left; x < region.right; x++) {
        var mx = Math.floor((x - region.left) / ratio - pointX), my = Math.floor((y - region.top) / ratio - pointY);
        // 映射图外或未选通道不产生位移。
        if (mx < 0 || my < 0 || mx >= map.width || my >= map.height) continue;
        var mapOffset = (my * map.width + mx) * 4;
        var sx = x + (channelX === undefined ? 0 : mapData[mapOffset + channelX] - 128) * scaleX;
        var sy = y + (channelY === undefined ? 0 : mapData[mapOffset + channelY] - 128) * scaleY;
        var outside = sx < region.left || sx >= region.right || sy < region.top || sy >= region.bottom;
        var offset = (y * width + x) * 4;
        if (outside && mode === "ignore") continue;
        if (outside && mode === "color") { for (var c = 0; c < 4; c++) data[offset + c] = edge[c]; continue; }
        var left = Math.floor(sx), top = Math.floor(sy), tx = sx - left, ty = sy - top, rgba = [0, 0, 0, 0];
        for (c = 0; c < 4; c++) rgba[c] = (sample(left, top, c) * (1 - tx) + sample(left + 1, top, c) * tx) * (1 - ty)
            + (sample(left, top + 1, c) * (1 - tx) + sample(left + 1, top + 1, c) * tx) * ty;
        for (c = 0; c < 3; c++) data[offset + c] = rgba[3] ? rgba[c] * 255 / rgba[3] : 0;
        data[offset + 3] = rgba[3];
    }
    context.putImageData(image, 0, 0);
}

function applyPixelFilter(context, width, height, filter, kind) {
    var image = context.getImageData(0, 0, width, height), data = image.data, input = data.slice();
    var matrix = filter.matrix || [];
    if (kind === "ColorMatrixFilter") {
        if (matrix.length !== 20) return;
        for (var i = 0; i < data.length; i += 4) for (var c = 0; c < 4; c++) {
            var row = c * 5;
            data[i + c] = matrix[row] * input[i] + matrix[row + 1] * input[i + 1]
                + matrix[row + 2] * input[i + 2] + matrix[row + 3] * input[i + 3] + matrix[row + 4];
        }
    } else {
        var columns = Math.max(0, Math.floor(toFiniteNumber(filter.matrixX, 0)));
        var rows = Math.max(0, Math.floor(toFiniteNumber(filter.matrixY, 0)));
        if (!columns || !rows || matrix.length < columns * rows) return;
        var divisor = toFiniteNumber(filter.divisor, 1) || 1, bias = toFiniteNumber(filter.bias, 0);
        var edge = [filter.color >> 16 & 255, filter.color >> 8 & 255, filter.color & 255, Math.max(0, Math.min(1, filter.alpha)) * 255];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) for (c = 0; c < 4; c++) {
            if (c === 3 && filter.preserveAlpha) continue;
            var sum = 0;
            for (var r = 0; r < rows; r++) for (var col = 0; col < columns; col++) {
                var px = x + col - Math.floor(columns / 2), py = y + r - Math.floor(rows / 2);
                if (filter.clamp) { px = Math.max(0, Math.min(width - 1, px)); py = Math.max(0, Math.min(height - 1, py)); }
                var value = px < 0 || py < 0 || px >= width || py >= height ? edge[c] : input[(py * width + px) * 4 + c];
                sum += value * matrix[r * columns + col];
            }
            data[(y * width + x) * 4 + c] = sum / divisor + bias;
        }
    }
    context.putImageData(image, 0, 0);
}

function applyBevel(context, width, height, filter, ratio) {
    var image = context.getImageData(0, 0, width, height), data = image.data;
    var input = new Float32Array(width * height);
    for (var i = 0; i < input.length; i++) input[i] = data[i * 4 + 3] / 255;
    var blurred = blur(input, width, height, 1, filter, ratio);
    var angle = toFiniteNumber(filter.angle, 45) * Math.PI / 180;
    var dx = toFiniteNumber(filter.distance, 4) * Math.cos(angle) * ratio, dy = toFiniteNumber(filter.distance, 4) * Math.sin(angle) * ratio;
    var colors = [filter.highlightColor >>> 0, filter.shadowColor >>> 0];
    for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) {
        var pixel = y * width + x, offset = pixel * 4, original = input[pixel];
        var relief = sampleAlpha(blurred, width, height, x + dx, y + dy, 0) - sampleAlpha(blurred, width, height, x - dx, y - dy, 0);
        var highlight = relief >= 0, color = colors[highlight ? 0 : 1];
        var alpha = Math.min(1, Math.abs(relief) * Math.max(0, toFiniteNumber(filter.strength, 1)))
            * Math.max(0, Math.min(1, highlight ? filter.highlightAlpha : filter.shadowAlpha));
        if (filter.type === "inner") alpha *= original;
        else if (filter.type === "outer") alpha *= 1 - original;
        var baseAlpha = filter.knockout ? 0 : original;
        var outAlpha = alpha + baseAlpha * (1 - alpha);
        for (var c = 0; c < 3; c++) data[offset + c] = outAlpha ? (((color >> (16 - c * 8)) & 255) * alpha + data[offset + c] * baseAlpha * (1 - alpha)) / outAlpha : 0;
        data[offset + 3] = outAlpha * 255;
    }
    context.putImageData(image, 0, 0);
}

function applyColorTransform(canvas, transform) {
    if (!transform) return;
    var multipliers = [transform.redMultiplier, transform.greenMultiplier, transform.blueMultiplier, transform.alphaMultiplier]
        .map(function (value) { return toFiniteNumber(value, 1); });
    var offsets = [transform.redOffset, transform.greenOffset, transform.blueOffset, transform.alphaOffset]
        .map(function (value) { return toFiniteNumber(value, 0); });
    if (multipliers.every(function (x) { return x === 1; }) && offsets.every(function (x) { return x === 0; })) return;
    var context = canvas.getContext("2d"), image = context.getImageData(0, 0, canvas.width, canvas.height);
    var data = image.data;
    for (var i = 0; i < data.length; i += 4) {
        // 缓存扩边不是元件内容，不得被 alphaOffset 变成不透明矩形。
        if (!data[i + 3]) continue;
        for (var c = 0; c < 4; c++) data[i + c] = data[i + c] * multipliers[c] + offsets[c];
    }
    context.putImageData(image, 0, 0);
}

function applyEffects(canvas, element, ratio) {
    if (applyEffectsGPU(canvas, element.props.filters, element.props.colorTransform, ratio || window.devicePixelRatio || 1)) return;
    applyFilters(canvas, element.props.filters, ratio || window.devicePixelRatio || 1);
    applyColorTransform(canvas, element.props.colorTransform);
}

export { filterPadding, applyEffects };
