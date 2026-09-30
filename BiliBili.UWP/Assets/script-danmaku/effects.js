// 滤镜按数组顺序作用于整份元件位图（包括容器的子树），不只处理叶子图元。
import { toFiniteNumber } from "./core.js";

function filterPadding(filters) {
    var x = 0, y = 0;
    for (var i = 0; filters && i < filters.length; i++) {
        var f = filters[i];
        if (f.type !== "GlowFilter" && f.type !== "BlurFilter") continue;
        var quality = Math.max(1, Math.min(15, Math.floor(toFiniteNumber(f.quality, 1))));
        if (f.inner) continue;
        x += Math.ceil(Math.max(0, f.blurX) / 2) * quality + 1;
        y += Math.ceil(Math.max(0, f.blurY) / 2) * quality + 1;
    }
    return { x: x, y: y };
}

// 固定大小滑动窗口，透明边界按零处理。Flash 的 quality 表示重复模糊次数。
function blurPass(input, width, height, channels, radius, horizontal) {
    if (!radius) return input;
    var out = new Float32Array(input.length);
    var length = horizontal ? width : height, lines = horizontal ? height : width;
    var stride = horizontal ? channels : width * channels;
    var divisor = 2 * radius + 1;
    for (var line = 0; line < lines; line++) for (var channel = 0; channel < channels; channel++) {
        var base = (horizontal ? line * width * channels : line * channels) + channel, sum = 0;
        for (var j = 0; j <= Math.min(radius, length - 1); j++) sum += input[base + j * stride];
        for (var pos = 0; pos < length; pos++) {
            out[base + pos * stride] = sum / divisor;
            var remove = pos - radius, add = pos + radius + 1;
            if (remove >= 0) sum -= input[base + remove * stride];
            if (add < length) sum += input[base + add * stride];
        }
    }
    return out;
}

function blur(input, width, height, channels, filter, ratio) {
    var x = Math.ceil(Math.max(0, filter.blurX) * ratio / 2);
    var y = Math.ceil(Math.max(0, filter.blurY) * ratio / 2);
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
    for (var index = 0; index < filters.length; index++) {
        var filter = filters[index];
        if (filter.type !== "GlowFilter" && filter.type !== "BlurFilter") continue;
        var image = context.getImageData(0, 0, width, height), data = image.data;
        var channels = filter.type === "BlurFilter" ? 4 : 1;
        var input = new Float32Array(width * height * channels);
        for (var i = 0, pixel = 0; i < data.length; i += 4, pixel++) {
            var alpha = data[i + 3] / 255;
            if (channels === 4) {
                input[i] = data[i] * alpha; input[i + 1] = data[i + 1] * alpha;
                input[i + 2] = data[i + 2] * alpha; input[i + 3] = alpha;
            } else input[pixel] = filter.inner ? 1 - alpha : alpha;
        }
        var blurred = blur(input, width, height, channels, filter, ratio);
        var color = filter.color >>> 0, rgb = [color >> 16 & 255, color >> 8 & 255, color & 255];
        var strength = Math.max(0, toFiniteNumber(filter.strength, 2)) * Math.max(0, Math.min(1, filter.alpha));
        for (i = 0, pixel = 0; i < data.length; i += 4, pixel++) {
            if (channels === 4) {
                var a = blurred[i + 3];
                for (var c = 0; c < 3; c++) data[i + c] = a > 0 ? blurred[i + c] / a : 0;
                data[i + 3] = a * 255;
            } else {
                var original = data[i + 3] / 255;
                var glow = Math.min(1, blurred[pixel] * strength);
                var ga = filter.inner ? glow * original : glow;
                if (filter.knockout) {
                    if (!filter.inner) ga *= 1 - original;
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
    applyFilters(canvas, element.props.filters, ratio || window.devicePixelRatio || 1);
    applyColorTransform(canvas, element.props.colorTransform);
}

export { filterPadding, applyEffects };
