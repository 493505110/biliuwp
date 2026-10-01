// BitmapData、位图与粒子 API。
import {
    currentPositionMs,
    hostState,
    toFiniteNumber
} from "./core.js";
import {
    M8Display
} from "./display.js";
import {
    Player,
    currentPlayerState
} from "./player.js";
import {
    writeTrace
} from "./runtime.js";
import { invalidateElementCache } from "./tween.js";
import { ensureRunning } from "./lifecycle.js";

// BitmapData 是位移滤镜的实时输入；通过公开 API 修改映射图后，引用它的缓存必须失效。
function invalidateBitmapFilters(bitmap) {
    var changed = false;
    for (var i = 0; i < hostState.elements.length; i++) {
        var element = hostState.elements[i], filters = element.props.filters || [];
        if (!element.expired && filters.some(function (filter) { return filter.mapBitmap === bitmap; })) {
            invalidateElementCache(element);
            changed = true;
        }
    }
    if (changed) { hostState.dirty = true; ensureRunning(); }
}

// ---- Bitmap：原版 ScriptBitmap（位图与粒子工厂）----
//
// 原版 ScriptBitmap（根目录 ScriptBitmap.as）四个方法：
//  createBitmapData(w,h,transparent=true,fillColor=0xFFFFFFFF) → flash BitmapData
//  createRectangle(x,y,width,height)                            → flash Rectangle
//  createBitmap({bitmapData,...})                               → CommentBitmap（受 motion 管理的显示对象）
//  createParticle({obj, radius=200})                            → 像素粒子爆炸（Simple2D 物理）
// 宿主用元素体系的「自绘层」（layer 元素：一块离屏 canvas，合成时整块 blit）
// 承载位图与粒子，于是寿命 / 擦除 / 补间与其它元件走同一套。
function argbToCss(color, useAlpha) {
    var value = toFiniteNumber(color, 0) >>> 0;
    var alpha = useAlpha === false ? 1 : ((value >>> 24) & 0xFF) / 255;
    return "rgba(" + ((value >>> 16) & 0xFF) + "," + ((value >>> 8) & 0xFF)
        + "," + (value & 0xFF) + "," + alpha + ")";
}

function bitmapDataSourceCanvas(source) {
    if (!source) {
        return null;
    }

    if (source.__scriptBitmapData) {
        return source.canvas;
    }

    if (source.layerCanvas) {
        return source.layerCanvas;
    }

    return source.cacheCanvas || null;
}

function createScriptBitmapData(width, height, transparent, fillColor) {
    var pixelWidth = Math.max(1, Math.floor(toFiniteNumber(width, 0)));
    var pixelHeight = Math.max(1, Math.floor(toFiniteNumber(height, 0)));
    var canvas = document.createElement("canvas");
    canvas.width = pixelWidth;
    canvas.height = pixelHeight;
    var context = canvas.getContext("2d");
    // ScriptBitmap.as:29：BitmapData 的默认填充是 ARGB 白色。
    if (fillColor === undefined) fillColor = 0xffffffff;
    if (fillColor !== undefined && fillColor !== null) {
        context.fillStyle = argbToCss(fillColor, transparent !== false);
        context.fillRect(0, 0, pixelWidth, pixelHeight);
    }

    return {
        __scriptBitmapData: true,
        width: pixelWidth,
        height: pixelHeight,
        canvas: canvas,
        fillRect: function (rect, color) {
            var area = rect || {};
            var x = toFiniteNumber(area.x, 0), y = toFiniteNumber(area.y, 0);
            var width = toFiniteNumber(area.width, pixelWidth), height = toFiniteNumber(area.height, pixelHeight);
            // 写像素是替换，不是 source-over；否则 alpha 通道无法从不透明改为透明。
            context.clearRect(x, y, width, height);
            context.fillStyle = argbToCss(color, transparent !== false);
            context.fillRect(x, y, width, height);
            invalidateBitmapFilters(this);
        },
        draw: function (source) {
            var sourceCanvas = bitmapDataSourceCanvas(source);
            if (sourceCanvas) {
                context.drawImage(sourceCanvas, 0, 0);
                invalidateBitmapFilters(this);
            }
        },
        getPixel32: function (x, y) {
            var pixel = context.getImageData(
                Math.floor(toFiniteNumber(x, 0)), Math.floor(toFiniteNumber(y, 0)), 1, 1).data;
            return ((pixel[3] << 24) | (pixel[0] << 16) | (pixel[1] << 8) | pixel[2]) >>> 0;
        },
        setPixel32: function (x, y, color) {
            var px = Math.floor(toFiniteNumber(x, 0)), py = Math.floor(toFiniteNumber(y, 0));
            var pixel = context.createImageData(1, 1), value = color >>> 0;
            pixel.data.set([value >> 16 & 255, value >> 8 & 255, value & 255, transparent === false ? 255 : value >>> 24]);
            context.putImageData(pixel, px, py);
            invalidateBitmapFilters(this);
        },
        dispose: function () {
            this.disposed = true;
            canvas.width = 1;
            canvas.height = 1;
            invalidateBitmapFilters(this);
        }
    };
}

function createScriptBitmapElement(config) {
    var options = config || {};
    var bitmapData = options.bitmapData;
    var width = bitmapData ? bitmapData.width : hostState.viewportWidth;
    var height = bitmapData ? bitmapData.height : hostState.viewportHeight;
    var element = M8Display.createLayer(width, height, options);
    if (element && bitmapData) {
        element.layer.drawImage(bitmapData.canvas, 0, 0);
    }

    return element;
}

function createScriptParticleElement(config) {
    var options = config || {};
    var sourceCanvas = bitmapDataSourceCanvas(options.obj);
    if (!sourceCanvas || !sourceCanvas.width || !sourceCanvas.height) {
        writeTrace(["Bitmap.createParticle: 源对象没有可采样的画布，返回 null"]);
        return null;
    }

    var radius = Math.max(0, Math.floor(toFiniteNumber(options.radius, 200)));
    var sourceWidth = sourceCanvas.width;
    var sourceHeight = sourceCanvas.height;
    var width = sourceWidth + radius * 2;
    var height = sourceHeight + radius * 2;
    var element = M8Display.createLayer(width, height, options);
    var sourceData = sourceCanvas.getContext("2d")
        .getImageData(0, 0, sourceWidth, sourceHeight).data;
    var particles = [];
    // 原版 ScriptBitmap.as:129-144：逐像素采样，非透明像素各成一个粒子，
    // 速度是 (random - random) * 5，质量 0.5 + random * 5。
    for (var y = 0; y < sourceHeight; y++) {
        for (var x = 0; x < sourceWidth; x++) {
            var offset = (y * sourceWidth + x) * 4;
            if (sourceData[offset + 3] === 0) {
                continue;
            }

            particles.push({
                x: x + radius,
                y: y + radius,
                vx: (Math.random() - Math.random()) * 5,
                vy: (Math.random() - Math.random()) * 5,
                mass: 0.5 + Math.random() * 5,
                alpha: sourceData[offset + 3],
                red: sourceData[offset],
                green: sourceData[offset + 1],
                blue: sourceData[offset + 2],
                isOut: false
            });
        }
    }

    element.__particleState = {
        particles: particles,
        outCount: 0,
        width: width,
        height: height,
        imageData: element.layer.createImageData(width, height)
    };
    return element;
}

// 原版 Simple2D.update（Simple2D.as:74-86）+ ScriptBitmap 的逐帧回填：
// 位置按速度推进（每帧固定步进，与播放时间无关）、alpha 每帧减 mass*3
// （下界 0）、越界即出局；出局比例超过 0.8 时整块销毁（ScriptBitmap.as:101-104）。
function advanceParticleElements(item) {
    if (!item || !item.elements) {
        return;
    }

    for (var index = item.elements.length - 1; index >= 0; index--) {
        var element = item.elements[index];
        var state = element && element.__particleState;
        if (!state || !element.layer) {
            continue;
        }

        var data = state.imageData.data;
        data.fill(0);
        var alive = 0;
        for (var p = 0; p < state.particles.length; p++) {
            var particle = state.particles[p];
            if (particle.isOut) {
                continue;
            }

            particle.x += particle.vx;
            particle.y += particle.vy;
            var nextAlpha = particle.alpha - particle.mass * 3;
            particle.alpha = nextAlpha <= 0 ? 0 : Math.floor(nextAlpha);
            if (particle.x <= 0 || particle.x >= state.width
                || particle.y <= 0 || particle.y >= state.height
                || particle.alpha <= 0) {
                particle.isOut = true;
                state.outCount++;
                continue;
            }

            var target = (Math.floor(particle.y) * state.width + Math.floor(particle.x)) * 4;
            data[target] = particle.red;
            data[target + 1] = particle.green;
            data[target + 2] = particle.blue;
            data[target + 3] = particle.alpha;
            alive++;
        }

        element.layer.putImageData(state.imageData, 0, 0);
        if (state.particles.length > 0
            && state.outCount / state.particles.length > 0.8) {
            element.remove();
        }
    }
}

// 注入给脚本的 Bitmap（原版是 ScriptBitmap 实例，globals.Bitmap）。
var ScriptBitmap = {
    createBitmapData: function (width, height, transparent, fillColor) {
        return createScriptBitmapData(width, height, transparent, fillColor);
    },
    createRectangle: function (x, y, width, height) {
        return M8Display.createRectangle(x, y, width, height);
    },
    createBitmap: function (config) {
        return createScriptBitmapElement(config);
    },
    createParticle: function (config) {
        return createScriptParticleElement(config);
    }
};

// 只读 / 实时量用 getter：Player.time 必须在 interval 回调里
// 实时读到当前播放头，激活时的快照会算错（脚本就是这么用的）。
Object.defineProperty(Player, "time", {
    configurable: true,
    enumerable: true,
    get: function () {
        return currentPositionMs();
    }
});

Object.defineProperty(Player, "state", {
    configurable: true,
    enumerable: true,
    get: function () {
        return currentPlayerState();
    }
});

Object.defineProperty(Player, "width", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.viewportWidth;
    }
});

Object.defineProperty(Player, "height", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.viewportHeight;
    }
});

Object.defineProperty(Player, "videoWidth", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.viewportWidth;
    }
});

Object.defineProperty(Player, "videoHeight", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.viewportHeight;
    }
});

// refreshRate：原版是空实现（ScriptPlayer.as:166-173）——get 恒返回 0、
// set 无操作。此前按 M8 文档做成了真值（10-500、默认 170），与反编译代码
// 不符，这里按原版改回：脚本读写 refreshRate 不产生任何效果。
Object.defineProperty(Player, "refreshRate", {
    configurable: true,
    enumerable: true,
    get: function () {
        return 0;
    },
    set: function (value) {
        // 原版 set 是空体：故意忽略脚本的设置。
    }
});

// 原版运行时存在、但接口未声明的量（ScriptPlayer.as:185-198）：
// videoWidth/videoHeight 宿主已提供，isContinueMode 恒 false。
Object.defineProperty(Player, "isContinueMode", {
    configurable: true,
    enumerable: true,
    get: function () {
        return false;
    }
});

Object.defineProperty(Player, "commentList", {
    configurable: true,
    enumerable: true,
    get: function () {
        // 当前弹幕列表快照，每条为 M8 的 CommentData 形状
        // （txt / time / color / pool / mode / fontSize）。
        // 由 C# 侧推入（见宿主命令 resetComments / appendComments）。
        return hostState.commentSnapshot;
    }
});

export {
    ScriptBitmap,
    advanceParticleElements
};
