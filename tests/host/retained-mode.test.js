'use strict';

// 脚本弹幕宿主的**行为型**测试（保留模式）。
//
// 为什么需要它：tests/BiliBili.Tests 里的 ScriptDanmakuHostContractTests 只能断言
// 源码字符串，改注释、改写法都能让它误判，而真正的渲染行为（拖影、寿命、缓存失效、
// seek 重建）完全没被覆盖。这里用 node:vm 把宿主 HTML 里的内联 <script> 加载进一个
// 无头桩里跑起来，逐帧驱动 requestAnimationFrame，断言可观察的渲染行为。
//
// 零依赖：只用 node 内置模块，不 npm install。运行：
//   node tests/host/retained-mode.test.js
// 指定其它宿主文件（例如对照修复前的版本）：
//   node tests/host/retained-mode.test.js /tmp/prefix-host.html
//   SCRIPT_DANMAKU_HOST=/tmp/prefix-host.html node tests/host/retained-mode.test.js
//
// 桩的硬性要求（踩过的坑）：
//  1. 必须实现 canvas 变换（translate/scale/setTransform/transform + save/restore 栈），
//     并在 drawImage/fillText 里把当前矩阵作用到目标坐标。否则被 translate 驱动的
//     移动元素在桩里永远画在原地，D1（拖影）会假通过。
//  2. 每个用例重新加载一份宿主（新建 vm context），不在用例之间手动清空 rAF 队列：
//     宿主用 running 标志判断循环是否在跑，清空队列但 running 仍为 true 会让后续
//     断言全部变成假结论。
//  3. postMessage 收到的是 JSON 字符串，桩里必须 JSON.parse。
//  4. 元素的内部字段以 Object.keys(element) 为准（cacheCanvas / cacheCtx / props /
//     ownerItem / lifeTimeMs / declaredLifeTimeMs / motion / expired / needsCache /
//     composite ...），不要凭猜测取字段名。

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const DEFAULT_HOST_PATH = path.join(
    __dirname, '..', '..', 'BiliBili.UWP', 'Assets', 'script-danmaku-host.html');
const HOST_PATH = process.argv[2] || process.env.SCRIPT_DANMAKU_HOST || DEFAULT_HOST_PATH;

// ---- 2D 矩阵工具（列主序 a,b,c,d,e,f，与 canvas 一致）----

function multiply(m, n) {
    return [
        m[0] * n[0] + m[2] * n[1],
        m[1] * n[0] + m[3] * n[1],
        m[0] * n[2] + m[2] * n[3],
        m[1] * n[2] + m[3] * n[3],
        m[0] * n[4] + m[2] * n[5] + m[4],
        m[1] * n[4] + m[3] * n[5] + m[5]
    ];
}

function applyMatrix(m, x, y) {
    return { x: m[0] * x + m[2] * y + m[4], y: m[1] * x + m[3] * y + m[5] };
}

function transformedAABB(matrix, x, y, width, height) {
    const corners = [
        applyMatrix(matrix, x, y),
        applyMatrix(matrix, x + width, y),
        applyMatrix(matrix, x + width, y + height),
        applyMatrix(matrix, x, y + height)
    ];
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const point of corners) {
        if (point.x < minX) minX = point.x;
        if (point.y < minY) minY = point.y;
        if (point.x > maxX) maxX = point.x;
        if (point.y > maxY) maxY = point.y;
    }
    return { x: minX, y: minY, width: maxX - minX, height: maxY - minY };
}

function rectsOverlap(a, b) {
    return !(a.x + a.width <= b.x || b.x + b.width <= a.x
        || a.y + a.height <= b.y || b.y + b.height <= a.y);
}

function unionRect(rects) {
    if (rects.length === 0) return null;
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const rect of rects) {
        minX = Math.min(minX, rect.x);
        minY = Math.min(minY, rect.y);
        maxX = Math.max(maxX, rect.x + rect.width);
        maxY = Math.max(maxY, rect.y + rect.height);
    }
    return { x: minX, y: minY, width: maxX - minX, height: maxY - minY };
}

function intersectRect(a, b) {
    const x = Math.max(a.x, b.x);
    const y = Math.max(a.y, b.y);
    const right = Math.min(a.x + a.width, b.x + b.width);
    const bottom = Math.min(a.y + a.height, b.y + b.height);
    if (right <= x || bottom <= y) return null;
    return { x: x, y: y, width: right - x, height: bottom - y };
}

function parseFontSize(font) {
    const match = /([0-9.]+)px/.exec(String(font || ''));
    return match ? Number(match[1]) : 10;
}

// ---- canvas 桩：记录「画了什么」与「擦了哪里」，并让擦除真的生效 ----

function createCanvasStub() {
    const canvas = {
        style: {},
        __isCanvasStub: true,
        __marks: [],
        __ops: [],
        __counts: {},
        __resizes: 0,
        __width: 300,
        __height: 150,
        __ctx: null,
        get width() { return canvas.__width; },
        set width(value) { canvas.__width = value; canvas.__resizes++; canvas.__marks = []; },
        get height() { return canvas.__height; },
        set height(value) { canvas.__height = value; canvas.__resizes++; canvas.__marks = []; },
        getContext(kind) {
            if (kind !== '2d') return null;
            if (!canvas.__ctx) canvas.__ctx = createContextStub(canvas);
            return canvas.__ctx;
        }
    };
    return canvas;
}

function createContextStub(canvas) {
    const ctx = {
        canvas: canvas,
        font: '10px sans-serif',
        fillStyle: '#000000',
        strokeStyle: '#000000',
        lineWidth: 1,
        globalAlpha: 1,
        textBaseline: 'alphabetic',
        textAlign: 'start',
        globalCompositeOperation: 'source-over',
        filter: 'none',
        __matrix: [1, 0, 0, 1, 0, 0],
        __stack: [],
        __path: null
    };

    function count(name) {
        canvas.__counts[name] = (canvas.__counts[name] || 0) + 1;
    }

    function record(kind, x, y, width, height) {
        let rect = transformedAABB(ctx.__matrix, x, y, width, height);
        if (ctx.__clip) {
            rect = intersectRect(rect, ctx.__clip);
            // 完全落在裁剪区外的落笔不算「画出来了」。
            if (!rect || rect.width <= 0 || rect.height <= 0) return null;
        }

        canvas.__marks.push(rect);
        // 记下混合模式：blendMode 的断言要看「落笔时用的是哪个 composite」。
        canvas.__ops.push({
            type: kind,
            rect: rect,
            font: ctx.font,
            composite: ctx.globalCompositeOperation
        });
        count(kind);
        return rect;
    }

    function recordPath(kind) {
        const bounds = pathBounds(ctx.__path);
        if (!bounds) return;
        record(kind, bounds.x, bounds.y, bounds.width, bounds.height);
    }

    // 裁剪区（宿主 Player.setMask 会用到）。save/restore 一并保存恢复，
    // record() 把落笔矩形与裁剪区求交：完全落在裁剪外的落笔不记录，
    // 这样「遮罩外的元素没画出来」可以被断言。
    ctx.__clip = null;

    ctx.save = function () { ctx.__stack.push({ matrix: ctx.__matrix.slice(), clip: ctx.__clip }); };
    ctx.restore = function () {
        if (ctx.__stack.length > 0) {
            const state = ctx.__stack.pop();
            ctx.__matrix = state.matrix;
            ctx.__clip = state.clip;
        }
    };

    ctx.clip = function () {
        const bounds = pathBounds(ctx.__path);
        if (!bounds) return;
        const rect = transformedAABB(ctx.__matrix, bounds.x, bounds.y, bounds.width, bounds.height);
        ctx.__clip = ctx.__clip ? intersectRect(ctx.__clip, rect) : rect;
    };
    ctx.setTransform = function (a, b, c, d, e, f) { ctx.__matrix = [a, b, c, d, e, f]; };
    ctx.transform = function (a, b, c, d, e, f) { ctx.__matrix = multiply(ctx.__matrix, [a, b, c, d, e, f]); };
    ctx.translate = function (x, y) { ctx.transform(1, 0, 0, 1, x, y); };
    ctx.scale = function (x, y) { ctx.transform(x, 0, 0, y, 0, 0); };
    ctx.rotate = function (radians) {
        const cos = Math.cos(radians), sin = Math.sin(radians);
        ctx.transform(cos, sin, -sin, cos, 0, 0);
    };

    ctx.clearRect = function (x, y, width, height) {
        const rect = transformedAABB(ctx.__matrix, x, y, width, height);
        canvas.__ops.push({ type: 'clearRect', rect: rect });
        count('clearRect');
        canvas.__marks = canvas.__marks.filter((mark) => !rectsOverlap(mark, rect));
    };

    ctx.fillRect = function (x, y, width, height) { record('fillRect', x, y, width, height); };
    ctx.strokeRect = function (x, y, width, height) { record('strokeRect', x, y, width, height); };

    ctx.fillText = function (text, x, y) {
        const size = parseFontSize(ctx.font);
        const width = String(text).length * size * 0.6;
        const height = size * 1.2;
        // 宿主用 textBaseline="middle" + textAlign="left" 画文本。
        record('fillText', x, y - height / 2, width, height);
    };
    ctx.strokeText = ctx.fillText;

    ctx.measureText = function (text) {
        return { width: String(text).length * parseFontSize(ctx.font) * 0.6 };
    };

    ctx.drawImage = function (source) {
        const args = Array.prototype.slice.call(arguments, 1);
        let dx = 0, dy = 0, dw = 0, dh = 0;
        if (args.length === 2) {
            dx = args[0]; dy = args[1];
            dw = source && source.width ? source.width : 0;
            dh = source && source.height ? source.height : 0;
        } else if (args.length === 4) {
            dx = args[0]; dy = args[1]; dw = args[2]; dh = args[3];
        } else if (args.length === 8) {
            dx = args[4]; dy = args[5]; dw = args[6]; dh = args[7];
        }
        record('drawImage', dx, dy, dw, dh);
    };

    // 渐变对象（宿主 beginGradientFill 会用到）。记录色标数量，便于断言。
    ctx.__gradients = [];
    ctx.createLinearGradient = function (x0, y0, x1, y1) {
        const gradient = { kind: 'linear', x0, y0, x1, y1, stops: [], addColorStop(o, c) { gradient.stops.push([o, c]); } };
        ctx.__gradients.push(gradient);
        return gradient;
    };
    ctx.createRadialGradient = function (x0, y0, r0, x1, y1, r1) {
        const gradient = { kind: 'radial', x0, y0, r0, x1, y1, r1, stops: [], addColorStop(o, c) { gradient.stops.push([o, c]); } };
        ctx.__gradients.push(gradient);
        return gradient;
    };

    ctx.beginPath = function () { ctx.__path = { points: [] }; };
    ctx.moveTo = function (x, y) { pathAdd(ctx.__path, x, y); };
    ctx.lineTo = function (x, y) { pathAdd(ctx.__path, x, y); };
    ctx.rect = function (x, y, width, height) {
        pathAdd(ctx.__path, x, y);
        pathAdd(ctx.__path, x + width, y + height);
    };
    ctx.arc = function (x, y, radius) {
        pathAdd(ctx.__path, x - Math.abs(radius), y - Math.abs(radius));
        pathAdd(ctx.__path, x + Math.abs(radius), y + Math.abs(radius));
    };
    ctx.ellipse = function (x, y, rx, ry) {
        pathAdd(ctx.__path, x - Math.abs(rx), y - Math.abs(ry));
        pathAdd(ctx.__path, x + Math.abs(rx), y + Math.abs(ry));
    };
    ctx.quadraticCurveTo = function (cx, cy, x, y) {
        pathAdd(ctx.__path, cx, cy);
        pathAdd(ctx.__path, x, y);
    };
    ctx.closePath = function () { };
    ctx.fill = function () { recordPath('fill'); };
    ctx.stroke = function () { recordPath('stroke'); };

    return ctx;
}

function pathAdd(path, x, y) {
    if (!path) return;
    if (typeof x === 'number' && isFinite(x) && typeof y === 'number' && isFinite(y)) {
        path.points.push([x, y]);
    }
}

function pathBounds(path) {
    if (!path || path.points.length === 0) return null;
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const point of path.points) {
        minX = Math.min(minX, point[0]);
        minY = Math.min(minY, point[1]);
        maxX = Math.max(maxX, point[0]);
        maxY = Math.max(maxY, point[1]);
    }
    return { x: minX, y: minY, width: Math.max(0.01, maxX - minX), height: Math.max(0.01, maxY - minY) };
}

// ---- 宿主加载器 ----

function loadHost(hostPath) {
    const html = fs.readFileSync(hostPath || HOST_PATH, 'utf8');
    const match = /<script>([\s\S]*?)<\/script>/.exec(html);
    if (!match) throw new Error('宿主 HTML 里找不到内联 <script>：' + hostPath);

    const messages = [];
    const frameErrors = [];
    const rafCallbacks = new Map();
    let rafId = 0;
    let clock = 1000;

    const container = {
        offsetWidth: 800,
        offsetHeight: 600,
        children: [],
        appendChild(child) { container.children.push(child); return child; }
    };

    const sandbox = {};
    sandbox.window = sandbox;
    sandbox.devicePixelRatio = 1;
    sandbox.performance = { now: () => clock };
    sandbox.addEventListener = function () { };
    sandbox.chrome = { webview: { postMessage: (text) => messages.push(JSON.parse(text)) } };
    sandbox.console = { log() { }, warn() { }, error() { }, count() { } };
    sandbox.Image = function ImageStub() { this.width = 0; this.height = 0; };
    sandbox.document = {
        getElementById: (id) => (id === 'stage' ? container : null),
        createElement: () => createCanvasStub()
    };
    sandbox.requestAnimationFrame = function (callback) {
        const id = ++rafId;
        rafCallbacks.set(id, callback);
        return id;
    };
    sandbox.cancelAnimationFrame = function (id) { rafCallbacks.delete(id); };

    vm.createContext(sandbox);
    vm.runInContext(match[1], sandbox, { filename: hostPath || HOST_PATH });

    const host = {
        sandbox: sandbox,
        container: container,
        messages: messages,
        frameErrors: frameErrors,
        api: sandbox.scriptDanmakuHost,
        now: () => clock,
        advanceClock(ms) { clock += ms; },
        pendingFrames: () => rafCallbacks.size,
        errors: (stage) => messages.filter(
            (message) => message.type === 'error' && (stage === undefined || message.stage === stage)),
        // 主画布 = 第一个被 appendChild 到 #stage 的 canvas。
        mainCanvas() {
            const canvas = container.children.find((child) => child && child.__isCanvasStub);
            assert.ok(canvas, '宿主启动后应把主画布挂到 #stage');
            return canvas;
        },
        // 跑 n 帧，每帧推进 dtMs 毫秒。帧回调抛出的异常被记录下来而不是让套件崩掉，
        // 这样「修复前的宿主」能跑完全部用例并如实报出失败。
        runFrames(count, dtMs) {
            const step = dtMs === undefined ? 1000 / 60 : dtMs;
            for (let index = 0; index < count; index++) {
                clock += step;
                const callbacks = Array.from(rafCallbacks.values());
                rafCallbacks.clear();
                for (const callback of callbacks) {
                    try {
                        callback(clock);
                    } catch (error) {
                        frameErrors.push(error);
                    }
                }
            }
        },
        // 便捷封装：宿主命令。
        reset(positionSeconds, playing, rate, visible) {
            host.api.reset(positionSeconds, playing, rate, visible);
        },
        append(items) {
            host.api.append(items);
        },
        setState(positionSeconds, playing, rate) {
            host.api.setState(positionSeconds, playing, rate);
        },
        seek(positionSeconds, playing, rate) {
            host.api.seek(positionSeconds, playing, rate);
        },
        resize() {
            host.api.resize();
        },
        // 读元素内部字段前先确认字段名确实存在（防止宿主改字段名后测试静默取到 undefined）。
        // 用 getOwnPropertyNames 而不是 Object.keys：宿主把元素的字段都定义成
        // 不可枚举（Flash 的显示对象属性在原型上、foreach 拿不到，M8 的脚本
        // 依赖这个分叉），Object.keys 会全都看不到。校验强度不变。
        elementField(element, name) {
            assert.ok(element && typeof element === 'object', '元素探针未设置：脚本里没有 window.__probe = ...');
            assert.ok(
                Object.getOwnPropertyNames(element).indexOf(name) >= 0,
                '元素上不存在字段 ' + name + '，实际字段：' + Object.getOwnPropertyNames(element).join(', '));
            return element[name];
        }
    };

    return host;
}

function linear(from, to, elapsedMs, durationMs) {
    const t = Math.max(0, Math.min(durationMs, elapsedMs));
    return from + (to - from) * t / durationMs;
}

// ---- 极简用例运行器（零依赖，node 直接执行）----

const cases = [];

function test(name, fn) {
    cases.push({ name: name, fn: fn });
}

function run() {
    let failed = 0;
    for (const item of cases) {
        try {
            item.fn();
            process.stdout.write('  ok   ' + item.name + '\n');
        } catch (error) {
            failed++;
            process.stdout.write('  FAIL ' + item.name + '\n');
            process.stdout.write('       ' + String(error && error.message).split('\n').join('\n       ') + '\n');
            if (error && error.stack) {
                const frame = error.stack.split('\n').find((line) => line.indexOf('retained-mode.test.js') >= 0);
                if (frame) process.stdout.write('       at' + frame.split('at')[1] + '\n');
            }
        }
    }

    process.stdout.write('\n' + (cases.length - failed) + '/' + cases.length + ' 通过'
        + '（宿主：' + HOST_PATH + '）\n');
    if (failed > 0) process.exitCode = 1;
}

// ---- 用例 ----

function scriptItem(id, stime, duration, code) {
    return { id: id, stime: stime, duration: duration, lang: 'js', code: code };
}

// 每个用例都用 loadHost() 新建一份宿主（新 vm context）。
// 不在用例之间手动清空 rAF 队列：宿主用 running 标志判断循环是否在跑，
// 清空队列但 running 仍为 true 会让后续断言全部变成假结论。

test('D1 移动元素跑 60 帧后不残留旧位置像素（无拖影）', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([scriptItem('d1', 0, 10,
        'var box = $.createShape({ lifeTime: 10,'
        + ' motion: { x: { fromValue: 0, toValue: 400, easing: "Linear", lifeTime: 10 } } });'
        + 'box.graphics.beginFill(0xFFFFFF, 1);'
        + 'box.graphics.drawRect(0, 0, 40, 20);'
        + 'box.graphics.endFill();'
        + 'window.__box = box;')]);

    host.runFrames(60);
    assert.equal(host.frameErrors.length, 0, '帧回调不应抛错：' + host.frameErrors);

    const box = host.sandbox.__box;
    assert.ok(box, '脚本应把元素挂到 window.__box 供探针读取');
    // 60 帧 × 16.667ms = 1000ms，10s 线性补间 → x ≈ 40。
    assert.ok(Math.abs(box.x - 40) < 3, '第 60 帧 x 应约为 40，实际 ' + box.x);

    const marks = host.mainCanvas().__marks;
    assert.ok(marks.length > 0, '移动元素每帧都应重新合成，画布上不应为空');
    const union = unionRect(marks);
    // 元素本地宽 40（含包围盒 padding 后约 44）。若旧位置没被擦掉，
    // union 会横跨 0→40 的整条路径（宽约 92px）。
    assert.ok(
        union.x >= box.x - 8,
        '画布上仍残留起点附近的像素（拖影）：union.x=' + union.x + '，当前 x=' + box.x);
    assert.ok(
        union.width <= 56,
        '画布残留像素横跨 ' + union.width + 'px，超出元素宽度（40px）→ 存在拖影');
});

test('D2 自定义缓动抛错后帧循环存活、其他条目继续渲染、命令仍有效', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        // 缓动只在 t>0 时抛错：t=0 那一次（创建时立即套用的插值）不抛，
        // 这样错误落在「逐帧推进」路径上，正是 D2 要覆盖的位置。
        scriptItem('d2-bad', 0, 10,
            'var bad = $.createShape({ lifeTime: 10 });'
            + 'bad.graphics.beginFill(0xFF0000, 1);'
            + 'bad.graphics.drawRect(0, 0, 20, 20);'
            + 'bad.graphics.endFill();'
            + 'Tween.tween(bad, { x: 100 }, { x: 0 }, 10, function (time, begin) {'
            + '  if (time > 0) { throw new Error("坏缓动"); }'
            + '  return begin;'
            + '}).play();'),
        scriptItem('d2-good', 0, 10,
            'var good = $.createShape({ lifeTime: 10, y: 200 });'
            + 'good.graphics.beginFill(0x00FF00, 1);'
            + 'good.graphics.drawRect(0, 0, 20, 20);'
            + 'good.graphics.endFill();'
            + 'window.__good = good;'
            + 'Tween.tween(good, { x: 300 }, { x: 0 }, 10).play();')
    ]);

    host.runFrames(5);
    assert.equal(
        host.frameErrors.length, 0,
        '缓动抛出的异常不得从 rAF 回调逃逸（否则帧循环永久冻结）：' + host.frameErrors);
    assert.equal(host.pendingFrames(), 1, '出错后 rAF 队列应保持 1（帧循环存活）');
    assert.equal(
        host.errors('composite').length, 1,
        '同一个坏缓动只应上报 1 条 composite 错误，实际 ' + host.errors('composite').length);

    const good = host.sandbox.__good;
    const xAfterError = good.x;
    host.runFrames(30);
    assert.ok(good.x > xAfterError + 1, '同帧其他条目应继续被推进（x 应继续增长）');
    assert.equal(host.pendingFrames(), 1, '持续运行期间 rAF 队列应恒为 1');

    // setState / resize / reset 仍必须有效。
    host.setState(4, true, 1);
    host.runFrames(1);
    assert.equal(host.pendingFrames(), 1, 'setState 后帧循环仍应存活');

    const canvas = host.mainCanvas();
    const resizesBefore = canvas.__resizes;
    host.resize();
    assert.ok(canvas.__resizes > resizesBefore, 'resize 应重建画布尺寸');
    host.runFrames(2);
    assert.equal(host.pendingFrames(), 1, 'resize 后帧循环仍应存活');

    host.reset(0, true, 1, true);
    host.append([scriptItem('d2-after', 0, 10,
        'window.__after = $.createShape();')]);
    host.runFrames(2);
    assert.ok(host.sandbox.__after, 'reset 之后仍应能接收并执行新条目');
    assert.equal(host.pendingFrames(), 1, 'reset 后帧循环仍应存活');
});

test('D3 元素寿命 = min(声明的 lifeTime, 条目窗口剩余时间)', () => {
    function lifeTimeOf(stime, duration, code) {
        const host = loadHost();
        host.reset(0, true, 1, true);
        host.append([scriptItem('d3', stime, duration, code)]);
        // 条目不在窗口内时 append 不会起循环，用 setState 推进到窗口内。
        host.setState(stime + 0.1, true, 1);
        host.runFrames(2);
        return host;
    }

    // 声明式 motion 的 lifeTime 同时是补间时长与元素寿命（与 M8 的
    // `motion: {x: {..., lifeTime: n}}` 一致）。
    function motionShape(lifeTime) {
        return 'var box = $.createShape({ motion: { x: { fromValue: 0, toValue: 10,'
            + ' easing: "Linear", lifeTime: ' + lifeTime + ' } } });'
            + 'box.graphics.beginFill(0xFFFFFF, 1);'
            + 'box.graphics.drawRect(0, 0, 20, 20);'
            + 'box.graphics.endFill();'
            + 'window.__box = box;';
    }

    const shape = 'var box = $.createShape();'
        + 'box.graphics.beginFill(0xFFFFFF, 1);'
        + 'box.graphics.drawRect(0, 0, 20, 20);'
        + 'box.graphics.endFill();'
        + 'window.__box = box;';

    // 声明 4s、窗口 10s：寿命 4000ms（声明生效，不被窗口吞掉）。
    let host = lifeTimeOf(0, 10, motionShape(4));
    assert.equal(
        host.elementField(host.sandbox.__box, 'lifeTimeMs'), 4000,
        '声明 lifeTime: 4、窗口 10s 时寿命应为 4000ms');
    host.setState(3.9, true, 1);
    host.runFrames(1);
    assert.equal(host.sandbox.__box.expired, false, '3.9s 时元素应仍存活');
    host.setState(4.05, true, 1);
    host.runFrames(1);
    assert.equal(host.sandbox.__box.expired, true, '约 4s 后元素应被摘除');

    // 声明 2s、窗口 10s：寿命 2000ms（声明必须能**缩短**寿命，不能只延长）。
    host = lifeTimeOf(0, 10, motionShape(2));
    assert.equal(
        host.elementField(host.sandbox.__box, 'lifeTimeMs'), 2000,
        '声明 lifeTime: 2、窗口 10s 时寿命应为 2000ms，而不是活到窗口结束');
    host.setState(1.9, true, 1);
    host.runFrames(1);
    assert.equal(host.sandbox.__box.expired, false, '1.9s 时元素应仍存活');
    host.setState(2.05, true, 1);
    host.runFrames(1);
    assert.equal(host.sandbox.__box.expired, true, '约 2s 后元素应被摘除');

    // 声明 8s、窗口 10s：寿命 8000ms（窗口不能反过来吞掉声明）。
    host = lifeTimeOf(0, 10, motionShape(8));
    assert.equal(
        host.elementField(host.sandbox.__box, 'lifeTimeMs'), 8000,
        '声明 lifeTime: 8、窗口 10s 时寿命应为 8000ms');

    // 声明 8s、窗口 4s：寿命被窗口钳到 4000ms。
    host = lifeTimeOf(0, 4, motionShape(8));
    assert.equal(
        host.elementField(host.sandbox.__box, 'lifeTimeMs'), 4000,
        '声明 lifeTime: 8、窗口 4s 时寿命应被窗口钳到 4000ms');

    // 完全没有 tween 的元素：寿命 = 条目窗口剩余时间。
    host = lifeTimeOf(2, 6, shape);
    assert.equal(
        host.elementField(host.sandbox.__box, 'lifeTimeMs'), 6000,
        '未声明 tween 的元素寿命应等于条目窗口剩余时间');
});

test('D4 同屏有动画元素时，静止复合元素不重复重建整层', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('d4-composite', 0, 10,
            'var group = $.createShape();'
            + 'var dot = $.createShape({ parent: group, x: 100, y: 100 });'
            + 'dot.graphics.beginFill(0xFFFFFF, 1);'
            + 'dot.graphics.drawCircle(0, 0, 5);'
            + 'dot.graphics.endFill();'
            + 'window.__group = group;'),
        scriptItem('d4-anim', 0, 10,
            'var mover = $.createShape({ lifeTime: 10, y: 300,'
            + ' motion: { x: { fromValue: 0, toValue: 300, easing: "Linear", lifeTime: 10 } } });'
            + 'mover.graphics.beginFill(0x00FF00, 1);'
            + 'mover.graphics.drawRect(0, 0, 20, 20);'
            + 'mover.graphics.endFill();')
    ]);

    host.runFrames(60);
    const group = host.sandbox.__group;
    const composite = host.elementField(group, 'composite');
    assert.ok(composite, '复合元素应有复合层画布');
    assert.equal(
        composite.__counts.clearRect, 1,
        '静止复合元素 60 帧内只应烘焙一次，实际 ' + composite.__counts.clearRect + ' 次');

    // 画布尺寸 / DPI 变化会让所有元素 needsCache = true；
    // 重建后必须把 needsCache 清掉，否则之后每帧都会白烘一整张视口层。
    host.resize();
    host.runFrames(60);
    assert.equal(
        composite.__counts.clearRect, 2,
        'resize 后静止复合元素只应再烘焙一次，实际 ' + composite.__counts.clearRect + ' 次');
    assert.equal(
        host.elementField(group, 'needsCache'), false,
        '重建后 needsCache 应被清掉，否则每帧都会被判为结构脏');
});

test('D5 fontsize 补间后缓存尺寸随之变化，静止元素不被过度失效', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('d5-anim', 0, 10,
            'var label = $.createComment("AAAA", { fontsize: 12, lifeTime: 10,'
            + ' motion: { fontsize: { fromValue: 12, toValue: 40, easing: "Linear", lifeTime: 10 } } });'
            + 'window.__label = label;'),
        scriptItem('d5-static', 0, 10,
            'window.__still = $.createComment("BBBB", { fontsize: 12, x: 400, y: 400 });')
    ]);

    host.runFrames(1);
    const label = host.sandbox.__label;
    const still = host.sandbox.__still;
    const initialHeight = host.elementField(label, 'cacheCanvas').height;

    host.runFrames(29);
    assert.equal(host.frameErrors.length, 0, '帧回调不应抛错：' + host.frameErrors);

    // 30 帧 × 16.667ms = 500ms，10s 线性补间 → fontsize ≈ 12 + 28 × 0.05 = 13.4。
    const expectedFontSize = 12 + 28 * 0.05;
    const cacheCanvas = host.elementField(label, 'cacheCanvas');
    const expectedHeight = Math.ceil(expectedFontSize * 1.2 + 4);
    assert.ok(
        Math.abs(cacheCanvas.height - expectedHeight) <= 2,
        'fontsize 补间后缓存高度应随字号变化：期望 ≈' + expectedHeight + '，实际 ' + cacheCanvas.height
        + '（首帧 ' + initialHeight + '）');
    assert.ok(
        cacheCanvas.height > initialHeight,
        '缓存尺寸未随 fontsize 增长（缓存被当成变换类属性复用了）：'
        + initialHeight + ' → ' + cacheCanvas.height);

    const labelOps = host.elementField(label, 'cacheCtx').canvas.__ops
        .filter((op) => op.type === 'fillText');
    assert.ok(labelOps.length >= 20, '文本缓存应随字号逐帧重建，实际重绘 ' + labelOps.length + ' 次');
    const lastFontSize = parseFontSize(labelOps[labelOps.length - 1].font);
    assert.ok(
        Math.abs(lastFontSize - expectedFontSize) <= 2,
        '文本应按新字号重绘：期望 ≈' + expectedFontSize + 'px，实际 ' + lastFontSize + 'px');

    // 静止元素不得被过度失效：它的缓存只应烘焙一次。
    const stillCanvas = host.elementField(still, 'cacheCanvas');
    assert.equal(
        stillCanvas.__counts.fillText, 1,
        '静止文本元素的缓存只应烘焙一次，实际 ' + stillCanvas.__counts.fillText + ' 次');
    assert.equal(host.elementField(still, 'needsCache'), false);
});

test('D6 向后 seek 回窗口内：元素被重建、位置是插值结果、脚本不逐帧重跑', () => {
    // D6a：条目窗口 [1s,5s]，播到 7.0s（窗口已结束）后向后 seek 回 2s。
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([scriptItem('d6a', 1, 4,
        'window.__runs = (window.__runs || 0) + 1;'
        + 'var box = $.createShape({ lifeTime: 4,'
        + ' motion: { x: { fromValue: 0, toValue: 400, easing: "Linear", lifeTime: 4 } } });'
        + 'box.graphics.beginFill(0xFFFFFF, 1);'
        + 'box.graphics.drawRect(0, 0, 40, 20);'
        + 'box.graphics.endFill();'
        + 'window.__box = box;')]);
    // 条目窗口从 1s 开始：append 时还不在窗口内，靠 setState 把播放位置推进到
    // 窗口之前并起帧，之后由帧循环自然把条目激活。
    host.setState(0.5, true, 1);

    host.runFrames(390);   // 播放位置 0.5s → 7.0s，条目窗口 [1s,5s] 已越过
    const firstBox = host.sandbox.__box;
    assert.equal(firstBox.expired, true, '7.0s 时元素应已随条目窗口结束被摘除');
    assert.equal(host.sandbox.__runs, 1, '首轮播放脚本只应执行 1 次');
    assert.equal(
        unionRect(host.mainCanvas().__marks), null,
        '元素摘除后它的旧像素应已被擦掉，画布上不应残留上一轮的像素');

    host.seek(2, true, 1);
    host.runFrames(2);
    const box = host.sandbox.__box;
    assert.notEqual(box, firstBox, '向后 seek 回窗口内后元素应被重新创建');
    assert.equal(box.expired, false, '重建后的元素应是存活的');

    // 位置必须是「按新播放位置重算插值」的结果，而不是 0 或旧位置。
    const elapsedMs = 2000 + 2 * (1000 / 60) - 1000;
    const expectedX = 400 * elapsedMs / 4000;
    assert.ok(
        Math.abs(box.x - expectedX) < 3,
        '重建后的位置应是插值结果：期望 ≈' + expectedX.toFixed(1) + '，实际 ' + box.x.toFixed(1));

    const union = unionRect(host.mainCanvas().__marks);
    assert.ok(union, '向后 seek 回窗口内后应重新画出元素');
    assert.ok(
        union.x >= expectedX - 10 && union.x + union.width <= expectedX + 55,
        '画布上残留了上一轮播放的像素：union=' + JSON.stringify(union)
        + '，当前 x≈' + expectedX.toFixed(1));

    // 重建会再执行一次脚本（元素已被释放，这是唯一正确的做法），
    // 但绝不能逐帧重跑：之后 60 帧执行次数必须保持不变。
    const runsAfterRebuild = host.sandbox.__runs;
    assert.equal(runsAfterRebuild, 2, '重建应恰好再执行 1 次脚本，实际 ' + runsAfterRebuild + ' 次');
    host.runFrames(60);
    assert.equal(
        host.sandbox.__runs, runsAfterRebuild,
        'seek 之后不得逐帧重跑脚本，实际执行 ' + host.sandbox.__runs + ' 次');

    // D6b：条目窗口 [1s,10s]，元素声明 lifeTime: 2 → 3s 就被摘除但条目仍在窗口内；
    // 此时向后 seek 回 2s 必须走「按进度重建元素」这条路径。
    const late = loadHost();
    late.reset(0, true, 1, true);
    late.append([scriptItem('d6b', 1, 9,
        'window.__runs = (window.__runs || 0) + 1;'
        + 'var box = $.createShape({ lifeTime: 2,'
        + ' motion: { x: { fromValue: 0, toValue: 400, easing: "Linear", lifeTime: 2 } } });'
        + 'box.graphics.beginFill(0xFFFFFF, 1);'
        + 'box.graphics.drawRect(0, 0, 40, 20);'
        + 'box.graphics.endFill();'
        + 'window.__box = box;')]);
    late.setState(0.5, true, 1);

    late.runFrames(390);   // 播放位置 0.5s → 7.0s：元素在 3s 已被摘除，条目仍在窗口内
    const earlyBox = late.sandbox.__box;
    assert.equal(earlyBox.expired, true, '声明 lifeTime: 2 的元素应在约 3s 被摘除');

    late.seek(2, true, 1);
    late.runFrames(2);
    const rebuilt = late.sandbox.__box;
    assert.notEqual(rebuilt, earlyBox, '条目仍在窗口内时，向后 seek 也必须重建已摘除的元素');
    assert.equal(rebuilt.expired, false, '重建后的元素应是存活的');

    // D6b 的 tween 没有单独的 duration，补间时长就取自 lifeTime: 2，
    // 所以重建后的位置按 2000ms 的进度算，而不是 D6a 的 4000ms。
    const lateElapsedMs = 2000 + 2 * (1000 / 60) - 1000;
    const lateExpectedX = 400 * lateElapsedMs / 2000;
    assert.ok(
        Math.abs(rebuilt.x - lateExpectedX) < 3,
        '重建后的位置应是插值结果：期望 ≈' + lateExpectedX.toFixed(1)
        + '，实际 ' + rebuilt.x.toFixed(1));
});

test('D7 内置示例（单条）声明式 motion 与 interval 并存，能渲染出画面且到点自然收尾', () => {
    // 示例代码必须与 ScriptDanmakuService.GetBuiltInDemo() 逐字一致。
    // 它在 C# 里是字符串拼接，这里改写成等价字面量；两边的"事实来源"
    // 仍是那份 C#，本用例只保证示例确实能跑、且不是每帧重算坐标的写法。
    const demos = [
        {
            id: 'demo-m8-sample', stime: 1, duration: 7, lang: 'js',
            code: "var label = $.createComment('脚本弹幕已生效', {"
                + " font: 'sans-serif', fontsize: 32, color: 0x66CCFF,"
                + " x: Player.width + 120, y: Math.round(Player.height / 2 - 19),"
                + " lifeTime: 4,"
                + " motion: { x: { fromValue: Player.width + 120,"
                + "                 toValue: -120, easing: 'Linear', lifeTime: 4 } } });"
                + 'var count = 24;'
                + 'var cx = Player.width / 2;'
                + 'var cy = Player.height / 2;'
                + 'var dots = [];'
                + 'for (var i = 0; i < count; i++) {'
                + '  var dot = $.createShape({ visible: false });'
                + '  dot.graphics.beginFill(0xFF66CC, 1);'
                + '  dot.graphics.drawCircle(0, 0, 6);'
                + '  dot.graphics.endFill();'
                + '  dots.push(dot);'
                + '}'
                + 'var startAt = Player.time;'
                + 'interval(function () {'
                + '  var local = Player.time - startAt - 2000;'
                + '  if (local < 0) { return; }'
                + '  var p = Math.min(1, local / 3000);'
                + '  var radius = 40 + p * 160;'
                + '  for (var i = 0; i < count; i++) {'
                + '    var angle = i / count * Math.PI * 2 + p * Math.PI * 2;'
                + '    dots[i].x = cx + Math.cos(angle) * radius;'
                + '    dots[i].y = cy + Math.sin(angle) * radius;'
                + '    dots[i].alpha = 1 - p;'
                + '    dots[i].visible = p < 1;'
                + '  }'
                + '}, 16, 0);'
        }
    ];

    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append(demos);
    host.setState(0.5, true, 1);

    // 示例必须写在原版 M8 的 API 面上，不得退回自研 ctx 或立即模式。
    for (const demo of demos) {
        assert.equal(demo.code.indexOf('ctx.'), -1, demo.id + ' 不得再用自研的 ctx API');
        assert.equal(demo.code.indexOf('progress'), -1, demo.id + ' 不得用 progress 逐帧重算');
    }

    // 单条脚本里两种写法并存：文字走声明式 motion；粒子要恒定 6px 点半径，
    // tween 表达不了（scale 会把点一起放大），因此走 M8 惯用的 interval 逐帧只改位置。
    assert.ok(demos[0].code.indexOf('$.createComment(') >= 0, '示例必须用 $.createComment 建文本元件');
    assert.ok(demos[0].code.indexOf('motion:') >= 0, '示例必须用声明式 motion 描述文字动画');
    assert.ok(demos[0].code.indexOf('interval(') >= 0, '示例必须用 M8 的 interval 驱动粒子逐帧路径');
    assert.equal(demos[0].code.indexOf('scaleX'), -1, '示例不得用 scale 扩散：那会把点一起放大');
    assert.ok(
        demos[0].code.indexOf('Player.time - startAt - 2000') >= 0,
        '粒子晚于文字 2 秒出现，interval 的 delay 是首次触发的间隔，'
        + '须按 Player.time 与条目起始时刻比对自行扣掉偏移');

    // 跑到两种效果同时在屏的中段：文字 1~5s、粒子 3~6s，3.8s 都在。
    host.runFrames(200);
    assert.equal(host.errors().length, 0, '示例不应产生任何错误上报：' + JSON.stringify(host.errors()));
    assert.equal(host.frameErrors.length, 0, '示例不应让帧回调抛错：' + host.frameErrors);
    assert.ok(host.mainCanvas().__marks.length > 0, '3.8s 时文字与粒子都应已在画布上画出内容');

    // 跑过示例窗口末端（1+7=8s）后必须自然收尾：元素全部到期摘除、残影擦净。
    host.runFrames(400);
    assert.equal(
        unionRect(host.mainCanvas().__marks), null,
        '示例播放结束后画布上不应残留像素');
    assert.equal(host.errors().length, 0, '收尾阶段也不应产生错误');
});

test('D8 reset 整批作废时必须清画布，换一批弹幕不留旧像素', () => {
    // 对应 ReplaceAsync 在弹幕可见时换脚本 / 倍速重推：宿主收到 reset(…, visible=true)
    // 时整批元素被丢弃，它们的像素不会再有擦除队列，必须靠 reset 自己清屏。
    const host = loadHost();
    const painted = () => unionRect(host.mainCanvas().__marks);

    host.reset(0, true, 1, true);
    host.append([{
        id: 'gen1', stime: 0, duration: 8, lang: 'js',
        code: "var t = $.createComment('AAAA', { font: 'sans-serif', fontsize: 48, color: 0xFF0000, x: 100, y: 100 });"
    }]);
    host.setState(0.5, true, 1);
    host.runFrames(3);
    assert.ok(painted(), '第一批弹幕应已画到画布上');

    host.reset(0, true, 1, true);
    host.append([{
        id: 'gen2', stime: 0, duration: 8, lang: 'js',
        code: "var t = $.createComment('BBBB', { font: 'sans-serif', fontsize: 48, color: 0x0000FF, x: 400, y: 400 });"
    }]);
    assert.equal(
        painted(), null,
        'reset 之后画布必须被清空，否则上一批弹幕的像素会永久残留');

    host.setState(0.5, true, 1);
    host.runFrames(3);
    assert.ok(painted(), '第二批弹幕应能正常画出来');
});

test('D9 无界窗口按兜底上限兜住、lifeTime: 0 常驻、Player.time / Player.state 实时可读', () => {
    // duration 缺省（0）在 Parser 与宿主两侧都表示「不设时间窗」：
    // 原版 M8 没有条目窗口，元素寿命由脚本的 lifeTime 决定，宿主只留防呆上限。
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('unbounded', 1, 0,
            "var label = $.createComment('U', { font: 'sans-serif', fontsize: 32, color: 0xFFFFFF,"
            + ' x: 10, y: 10, lifeTime: 0,'
            + " motion: { x: { fromValue: 10, toValue: 300, easing: 'Linear', lifeTime: 0 } } });"
            + 'window.__probe = { startTime: Player.time, startState: Player.state, frames: 0,'
            + ' lastTime: 0, lastState: "" };'
            + 'window.__probeTweened = label;'
            + 'var dot = $.createShape({ x: 400, y: 300 });'
            + 'dot.graphics.beginFill(0xFFFFFF, 1);'
            + 'dot.graphics.drawCircle(0, 0, 4);'
            + 'dot.graphics.endFill();'
            + 'window.__probePlain = dot;'
            + 'interval(function () {'
            + '  window.__probe.frames++;'
            + '  window.__probe.lastTime = Player.time;'
            + '  window.__probe.lastState = Player.state;'
            + '}, 16, 0);')
    ]);
    host.setState(0.5, true, 1);
    host.runFrames(120);   // 跑到约 2.5s

    const probe = host.sandbox.__probe;
    assert.ok(probe, '脚本应写入探针');
    assert.ok(
        Math.abs(probe.startTime - 1000) < 50,
        'Player.time 应是激活那一刻的播放头位置（毫秒）：' + probe.startTime);
    assert.equal(probe.startState, 'playing', 'Player.state 在播放时应是 playing');
    assert.ok(probe.frames > 0, 'interval 回调应被逐帧调用');
    assert.ok(
        Math.abs(probe.lastTime - 2500) < 100,
        'interval 回调里的 Player.time 应实时跟着播放头走（毫秒）：' + probe.lastTime);
    assert.equal(probe.lastState, 'playing');

    // 寿命语义：无界窗口下未声明寿命的元素吃满兜底上限（不再是 3 秒窗口），
    // 声明 lifeTime: 0 的元素是常驻（声明值 Infinity，实际仍受兜底上限约束）。
    const plain = host.sandbox.__probePlain;
    const tweened = host.sandbox.__probeTweened;
    assert.equal(plain.lifeTimeMs, 600000, '未声明寿命的元素应活到窗口兜底上限');
    assert.equal(tweened.declaredLifeTimeMs, Infinity, 'lifeTime: 0 应记成常驻声明');
    assert.equal(tweened.lifeTimeMs, 600000, '常驻元素的实际寿命受兜底上限约束');

    // 越过原来 3 秒默认窗口的位置，元素仍在（旧的兜底会让它在这个位置消失）。
    host.runFrames(120);   // 到约 4.5s
    assert.ok(!plain.expired && !tweened.expired, '无界窗口下元素不应被窗口提前摘除');
    assert.ok(host.mainCanvas().__marks.length > 0, '4.5s 时画面应仍有内容');
    assert.equal(host.errors().length, 0, '不应产生错误上报');
});

test('D10 暂停且没有待推进的补间时帧循环自停，恢复播放后重新拉起', () => {
    // 旧判据是「窗口内有没有条目」，无界窗口下条目会长时间停在窗口内，
    // 暂停后帧循环就会一直空转；现在按「有没有待推进的补间/逐帧回调」自停。
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('unbounded-static', 1, 0,
            "var label = $.createComment('S', { font: 'sans-serif', fontsize: 32, color: 0xFFFFFF,"
            + ' x: 10, y: 10, lifeTime: 1,'
            + " motion: { x: { fromValue: 10, toValue: 200, easing: 'Linear', lifeTime: 1 } } });")
    ]);
    host.setState(0.5, true, 1);
    host.runFrames(120);   // 到约 2.5s，1 秒的补间已跑完
    assert.ok(host.pendingFrames() > 0, '播放中帧循环应在跑');

    host.setState(2.5, false, 1);
    host.runFrames(1);
    assert.equal(host.pendingFrames(), 0, '暂停且无待推进动画时应自停，不能空转');

    host.setState(2.5, true, 1);
    assert.ok(host.pendingFrames() > 0, '恢复播放后帧循环应重新拉起');
});

test('D11 M8 脚本原样执行：$ / Player / $G / ScriptManager / 全局函数都在脚本作用域里', () => {
    // 这条用例的正文刻意用**原版 M8 的写法**写成（不出现 ctx、不出现宿主扩展），
    // 断言的也是 M8 文档里写明的行为：$ 是元件工厂、Player.time 实时读、
    // $G 跨条目共享、ScriptManager.clearTimer 停当前条目的定时器、
    // timer/interval/clearTimer/foreach/clone/Utils/trace 都在脚本作用域里可用。
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('d11-a', 0, 10,
            // 先写一条，往 $G 里放一个值，并建一个「跨条目要复用」的计时器。
            "window.__log = [];"
            // Global 与 $G 是同一个对象（M8 文档里的两个名字，真实脚本两种都在用）。
            + "Global._set('greeting', 'hi');"
            + "$G._set('greeting2', Global._get('greeting') + '!');"
            + "window.__greeting2 = $G._get('greeting2');"
            + "var ticker = interval(function () { window.__log.push(Player.time); }, 200, 0);"
            + "$G._set('ticker', ticker);"
            + "function stopTicker() { ScriptManager.clearTimer(); }"
            + "$G._set('stopTicker', stopTicker);"),
        scriptItem('d11-b', 2, 8,
            // 再写一条，读上一条放进 $G 的值，并用 M8 的全局名做一圈操作。
            "var label = $.createComment($G._get('greeting') + ' M8',"
            + " { x: 0, y: 0, lifeTime: 2, color: Utils.rgb(102, 204, 255), fontsize: 32 });"
            + "window.__label = label;"
            + "window.__labelLength = label.length;"
            + "window.__state = Player.state;"
            + "window.__utils = [Utils.hue(0), Utils.hue(120), Utils.hue(240),"
            + " Utils.formatTimes(75), Math.round(Utils.distance(0, 0, 3, 4)),"
            + " (Utils.rand(5, 10) >= 5 && Utils.rand(5, 10) < 10)];"
            + "window.__foreach = [];"
            + "foreach({ a: 1, b: 2 }, function (key, value) { window.__foreach.push(key + value); });"
            + "var src = { test: 2 };"
            + "var copy = clone(src);"
            + "src.test = 1;"
            + "window.__clone = copy.test;"
            + "window.__timerType = typeof timer;"
            + "window.__getTimerPositive = getTimer() >= 0;"
            + "trace('m8 script ran');"
            + "tracex('m8 script ran');"
            + "timer(function () { window.__timerFired = true; }, 50);"),
        scriptItem('d11-c', 2.5, 8,
            // stopExecution() 必须终止当前脚本且不计为错误（原版脚本用它做幂等守卫）。
            "window.__beforeStop = true;"
            + "if (Player.time >= 0) { stopExecution(); }"
            + "window.__afterStop = true;")
    ]);
    host.setState(0.5, true, 1);
    host.runFrames(30);   // 到约 1.0s：第二条还没进窗口

    assert.equal(host.errors().length, 0, 'M8 脚本不应产生任何错误上报：' + JSON.stringify(host.errors()));
    assert.equal(host.frameErrors.length, 0, 'M8 脚本不应让帧回调抛错：' + host.frameErrors);
    assert.ok(host.sandbox.__log.length > 0, 'interval 回调应被触发（Player.time 实时可读）');
    assert.ok(
        host.sandbox.__log.every((value) => typeof value === 'number' && value >= 0),
        'Player.time 应是毫秒数：' + JSON.stringify(host.sandbox.__log.slice(0, 3)));

    host.runFrames(120);   // 到约 3.0s：第二条已激活
    assert.equal(host.errors().length, 0, '第二条 M8 脚本也不应报错：' + JSON.stringify(host.errors()));

    const label = host.sandbox.__label;
    assert.ok(label, '$.createComment 应建出元件并挂到 window.__label');
    assert.equal(label.text, 'hi M8', '$G._get 应能读到另一条条目写入的值（跨条目共享）');
    assert.equal(
        host.sandbox.__greeting2, 'hi!',
        'Global 与 $G 必须是同一个对象（Global._set 写的值 $G._get 要读到）');
    assert.equal(host.sandbox.__labelLength, 5, '文本元件的 length 应是字符数');
    assert.equal(host.sandbox.__state, 'playing', 'Player.state 在播放时应是 playing');
    // 注意：沙箱里的数组是 vm realm 的 Array，deepEqual 会因原型不同而误判，
    // 因此统一用 JSON 归一后再比较。
    const asJson = (value) => JSON.parse(JSON.stringify(value));
    assert.deepEqual(
        asJson(host.sandbox.__utils).slice(0, 3), [0x0000FF, 0xFF0000, 0x00FF00],
        'Utils.hue 的映射必须与 M8 文档一致（0→蓝、120→红、240→绿）');
    assert.equal(host.sandbox.__utils[3], '1:15', 'Utils.formatTimes(75) 应是 1:15');
    assert.equal(host.sandbox.__utils[4], 5, 'Utils.distance(0,0,3,4) 应是 5');
    assert.equal(host.sandbox.__utils[5], true, 'Utils.rand(min,max) 应落在 [min,max)');
    assert.deepEqual(
        asJson(host.sandbox.__foreach).sort(), ['a1', 'b2'],
        'foreach 回调签名应是 (key, value)');
    assert.equal(host.sandbox.__clone, 2, 'clone 应是值拷贝（改源对象不影响副本）');
    assert.equal(host.sandbox.__timerType, 'function', 'timer 应在脚本作用域里可用');
    assert.equal(host.sandbox.__getTimerPositive, true, 'getTimer 应返回非负毫秒数');
    assert.equal(host.sandbox.__timerFired, true, 'timer(fn, 50) 应在到点后触发一次');
    assert.equal(host.sandbox.__beforeStop, true, 'stopExecution 之前的语句应已执行');
    assert.equal(
        host.sandbox.__afterStop, undefined,
        'stopExecution() 必须立刻终止脚本（其后的语句不得执行）');
    assert.equal(
        host.errors().filter((message) => message.itemId === 'd11-c').length, 0,
        'stopExecution 是主动终止，不得被上报成脚本错误');

    // ScriptManager.clearTimer() 只清当前条目的定时器：第一条的 ticker 停了，
    // 但第二条（$G 里存着 handle）不受影响。这里直接调第一条留下的函数验证。
    const logLengthBefore = host.sandbox.__log.length;
    host.setState(3.2, true, 1);
    host.runFrames(30);
    assert.ok(
        host.sandbox.__log.length > logLengthBefore,
        'interval(…, 0) 是无限次，条目未回收前应继续触发');
});

test('D12 定时器登记在条目上：条目回收 / reset / seek 越窗后一律不再跑', () => {
    // M8 用 ScriptManager.clearTimer() 解决的问题：脚本忘了清定时器时，
    // 定时器不能在条目销毁后继续跑。宿主把它做成兜底——登记表随条目一起清。
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('d12', 1, 2,
            "window.__ticks = (window.__ticks || 0);"
            + "interval(function () { window.__ticks++; }, 100, 0);")
    ]);
    host.setState(0.5, true, 1);
    host.runFrames(60);   // 到约 1.5s：条目在窗口内，定时器已触发若干次
    const ticksInWindow = host.sandbox.__ticks;
    assert.ok(ticksInWindow > 0, '条目在窗口内时 interval 应触发');

    host.runFrames(120);  // 到约 3.5s：条目窗口 [1s,3s] 刚结束
    const ticksAfterWindow = host.sandbox.__ticks;
    assert.ok(
        ticksAfterWindow > ticksInWindow,
        '窗口内 interval 应继续触发：' + ticksInWindow + ' → ' + ticksAfterWindow);

    host.runFrames(120);  // 到约 5.5s
    assert.equal(
        host.sandbox.__ticks, ticksAfterWindow,
        '条目窗口结束后 interval 不得继续触发（定时器随条目一起清掉）');

    // seek 越出窗口后再拖回来：条目重建，定时器重新登记（新的一轮）。
    host.seek(2, true, 1);
    host.runFrames(60);
    assert.ok(
        host.sandbox.__ticks > ticksAfterWindow,
        '条目重建后定时器应重新登记并触发');

    // reset 整批作废：定时器必须停。
    const ticksBeforeReset = host.sandbox.__ticks;
    host.reset(0, true, 1, true);
    host.runFrames(60);
    assert.equal(
        host.sandbox.__ticks, ticksBeforeReset,
        'reset 之后旧条目的 interval 不得继续触发');
    assert.equal(host.errors().length, 0, '定时器生命周期不应产生错误上报');
});

test('D13 Tween.* 句柄与组合子：play/stop/stopOnComplete、delay/serial/reverse/repeat', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('d13-to', 0, 10,
            // tween + to + play：句柄时间轴由 play() 起算（M8 语义）。
            "var a = $.createShape({ x: 0, y: 0, lifeTime: 10 });"
            + "a.graphics.beginFill(0xFFFFFF, 1);"
            + "a.graphics.drawRect(0, 0, 10, 10);"
            + "a.graphics.endFill();"
            + "window.__a = a;"
            + "Tween.tween(a, { x: 200 }, { x: 0 }, 1).play();"),
        scriptItem('d13-serial', 0, 10,
            // serial(t1, reverse(t1))：先去再回，段列表按时间轴拼接。
            "var b = $.createShape({ x: 0, y: 100, lifeTime: 10 });"
            + "b.graphics.beginFill(0xFFFFFF, 1);"
            + "b.graphics.drawRect(0, 0, 10, 10);"
            + "b.graphics.endFill();"
            + "window.__b = b;"
            + "var t1 = Tween.tween(b, { x: 100 }, { x: 0 }, 1);"
            + "Tween.serial(t1, Tween.reverse(Tween.tween(b, { x: 100 }, { x: 0 }, 1))).play();"),
        scriptItem('d13-delay', 0, 10,
            // delay(t, 1)：前 1 秒不动，1~2 秒跑完。
            "var c = $.createShape({ x: 0, y: 200, lifeTime: 10 });"
            + "c.graphics.beginFill(0xFFFFFF, 1);"
            + "c.graphics.drawRect(0, 0, 10, 10);"
            + "c.graphics.endFill();"
            + "window.__c = c;"
            + "Tween.delay(Tween.tween(c, { x: 100 }, { x: 0 }, 1), 1).play();"),
        scriptItem('d13-repeat', 0, 10,
            // repeat(t, 3)：整条补间跑 3 轮（每轮都从 fromValue 重新开始）。
            "var d = $.createShape({ x: 0, y: 300, lifeTime: 10 });"
            + "d.graphics.beginFill(0xFFFFFF, 1);"
            + "d.graphics.drawRect(0, 0, 10, 10);"
            + "d.graphics.endFill();"
            + "window.__d = d;"
            + "window.__dRepeatEnd = 0;"
            + "var r = Tween.repeat(Tween.tween(d, { x: 60 }, { x: 0 }, 1), 3);"
            + "window.__dRepeatEnd = r.durationMs;"
            + "r.play();"),
        scriptItem('d13-stop', 0, 10,
            // stop()：停在当前位置，之后不再推进。
            "var e = $.createShape({ x: 0, y: 400, lifeTime: 10 });"
            + "e.graphics.beginFill(0xFFFFFF, 1);"
            + "e.graphics.drawRect(0, 0, 10, 10);"
            + "e.graphics.endFill();"
            + "window.__e = e;"
            + "var t = Tween.tween(e, { x: 900 }, { x: 0 }, 10);"
            + "t.play();"
            + "timer(function () { t.stop(); }, 200);"),
        scriptItem('d13-parallel', 0, 10,
            // parallel(t1, t2)：两条属性同时跑。
            "var f = $.createShape({ x: 0, y: 500, alpha: 1, lifeTime: 10 });"
            + "f.graphics.beginFill(0xFFFFFF, 1);"
            + "f.graphics.drawRect(0, 0, 10, 10);"
            + "f.graphics.endFill();"
            + "window.__f = f;"
            + "Tween.parallel("
            + "  Tween.tween(f, { x: 120 }, { x: 0 }, 1),"
            + "  Tween.tween(f, { alpha: 0 }, { alpha: 1 }, 1)"
            + ").play();")
    ]);
    host.setState(0.1, true, 1);

    // 0.5s：三条线性补间都在中段。
    host.runFrames(30);
    assert.equal(host.frameErrors.length, 0, 'Tween 用例不应让帧回调抛错：' + host.frameErrors);
    assert.equal(host.errors().length, 0, 'Tween 用例不应产生错误上报：' + JSON.stringify(host.errors()));

    const a = host.sandbox.__a;
    assert.ok(a.x > 60 && a.x < 140, 'Tween.tween(...).play() 应推进 x：实际 ' + a.x);
    assert.ok(host.sandbox.__b.x > 20 && host.sandbox.__b.x < 90,
        'serial 的第一段应正在推进：实际 ' + host.sandbox.__b.x);
    assert.ok(host.sandbox.__c.x < 5, 'delay 期间元素不应移动：实际 ' + host.sandbox.__c.x);
    assert.ok(host.sandbox.__d.x > 5 && host.sandbox.__d.x < 55,
        'repeat 第一轮应正在推进：实际 ' + host.sandbox.__d.x);
    assert.equal(host.sandbox.__dRepeatEnd, 3000, 'repeat(t, 3) 的总时长应是 3 倍（毫秒）');
    assert.ok(host.sandbox.__e.x < 100, 'stop 之前的推进量应有限：实际 ' + host.sandbox.__e.x);
    assert.ok(host.sandbox.__f.x > 30, 'parallel 的第一个补间应推进：实际 ' + host.sandbox.__f.x);
    assert.ok(host.sandbox.__f.alpha < 0.9, 'parallel 的第二个补间应推进：实际 ' + host.sandbox.__f.alpha);

    // 1.0s（30 帧 × 16.667ms ≈ 0.5s 之后又 0.5s）：serial 应进入反向段，
    // delay 应开始移动，stop 的元素必须停在 200ms 处不动了。
    const stoppedX = host.sandbox.__e.x;
    host.runFrames(60);
    assert.ok(host.sandbox.__c.x > 20, 'delay 结束后元素应开始移动：实际 ' + host.sandbox.__c.x);
    assert.equal(
        host.sandbox.__e.x, stoppedX,
        'stop() 之后元素必须停在当前位置，不再推进：' + stoppedX + ' → ' + host.sandbox.__e.x);
    assert.ok(
        Math.abs(host.sandbox.__a.x - 200) < 40,
        'tween 在 1 秒后应接近终点 200：实际 ' + host.sandbox.__a.x);
});

test('D14 Player 的动作请求走 action 通道：play / pause / seek / jump', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('d14', 0, 10,
            "Player.pause();"
            + "Player.play();"
            + "Player.seek(1500);"
            + "Player.jump('av120040', 2);"
            + "Player.jump('120040');"
            // 非法 av 号必须被拒绝，不能把坏消息发到 C# 侧。
            + "window.__jumpBad = [Player.jump('not-an-av'), Player.jump('')];"
            // 负数 seek 不是「拒绝」，而是钳到 0（与 PlayerPage.SeekFromScriptDanmaku 的
            // Math.Max(0, …) 一致）：画面回到开头比静默丢弃更符合预期。
            + "window.__seekNegative = Player.seek(-5);")
    ]);
    host.setState(0.1, true, 1);
    host.runFrames(2);

    const actions = host.messages.filter((message) => message.type === 'action');
    assert.deepEqual(
        actions.map((message) => message.action),
        ['pause', 'play', 'seek', 'navigate', 'navigate', 'seek'],
        '每个动作应按调用顺序各发一条 action 消息：' + JSON.stringify(actions));
    assert.equal(actions[2].seconds, 1.5, 'Player.seek 的入参是毫秒，进 action 时换成秒');
    assert.equal(
        actions[3].url, 'https://www.bilibili.com/video/av120040/?p=2',
        'Player.jump(av, page) 应拼成 bilibili 视频页 URL：' + actions[3].url);
    assert.equal(
        actions[4].url, 'https://www.bilibili.com/video/av120040/?p=1',
        'Player.jump 省略 page 时默认第 1 页：' + actions[4].url);
    assert.equal(host.sandbox.__jumpBad[0], false, '非 av 号应被拒绝（不发消息）');
    assert.equal(host.sandbox.__jumpBad[1], false, '空字符串应被拒绝（不发消息）');
    assert.equal(host.sandbox.__seekNegative, true, '负数 seek 应被接受（钳到 0）');
    assert.equal(actions[5].seconds, 0, 'Player.seek(-5) 应钳到 0 秒');
    assert.equal(host.errors().length, 0, '动作请求不应产生错误上报');
});

test('D15 Player.commentList 是推入的快照，字段形状与 M8 的 CommentData 一致', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    // C# 侧推快照：resetComments 清空 + appendComments 追加（真实链路是分批推的）。
    host.api.resetComments();
    host.api.appendComments([
        { txt: '是', time: 1.5, color: 0xFFFFFF, pool: 0, mode: 1, fontSize: 25 },
        { txt: '否', time: 2.5, color: 0xFF0000, pool: 1, mode: 4, fontSize: 25 }
    ]);
    // 第三条刻意只给 txt：其余字段应被补成 M8 的缺省值（不是 undefined）。
    host.api.appendComments([{ txt: '是' }]);

    host.append([
        scriptItem('d15', 0, 10,
            // 完全按 M8 文档里的 Player.commentList 示例写法。
            'var l = Player.commentList.length;'
            + 'var yes = 0; var no = 0;'
            + 'for (var i = 0; i < l; i++) {'
            + "  var cmt = Player.commentList[i];"
            + "  if (cmt.txt == '是') { yes++; } else if (cmt.txt == '否') { no++; }"
            + '}'
            + 'window.__vote = { length: l, yes: yes, no: no,'
            + ' second: Player.commentList[1],'
            + ' defaults: Player.commentList[2],'
            + ' defaultsTime: Player.commentList[2].time };')
    ]);
    host.setState(0.1, true, 1);
    host.runFrames(2);

    const vote = host.sandbox.__vote;
    assert.ok(vote, '脚本应写入探针');
    assert.equal(vote.length, 3, 'commentList.length 应是推入的条数');
    assert.equal(vote.yes, 2, '按 txt 统计「是」的条数');
    assert.equal(vote.no, 1, '按 txt 统计「否」的条数');
    assert.equal(vote.second.time, 2.5, 'CommentData.time 是秒');
    assert.equal(vote.second.color, 0xFF0000, 'CommentData.color 原样保留');
    assert.equal(vote.second.pool, 1, 'CommentData.pool 原样保留');
    assert.equal(vote.second.mode, 4, 'CommentData.mode 原样保留');
    assert.equal(vote.second.fontSize, 25, 'CommentData.fontSize 原样保留');
    // 缺省字段要被补齐，脚本读到的形状始终完整（不是 undefined）。
    assert.equal(vote.defaults.color, 0xFFFFFF, '未给的 color 应补成默认字色');
    assert.equal(vote.defaults.pool, 0, '未给的 pool 应补成 0');
    assert.equal(vote.defaults.mode, 1, '未给的 mode 应补成 1（滚动）');
    assert.equal(vote.defaults.fontSize, 25, '未给的 fontSize 应补成 M8 默认字号');
    assert.equal(vote.defaultsTime, 0, '未给的 time 应补成 0');

    // resetComments 清空快照（换一集 / 换视频时 C# 会重新推）：
    // 用一条新条目读，确认脚本看到的是空表而不是上一批。
    host.api.resetComments();
    host.append([scriptItem('d15b', 2, 4,
        'window.__afterReset = Player.commentList.length;')]);
    host.setState(2.1, true, 1);
    host.runFrames(30);
    assert.equal(host.sandbox.__afterReset, 0, 'resetComments 之后 commentList 应为空');
    assert.equal(host.errors().length, 0, '数据链不应产生错误上报');
});

test('D16 commentTrigger / keyTrigger：只在收到桥消息时触发，条目回收后不再触发', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('d16', 1, 3,
            'window.__got = [];'
            // 监听发送弹幕（timeout 给足，避免用例中途过期）
            + 'Player.commentTrigger(function (data) {'
            + "  window.__got.push('c:' + data.txt + '@' + data.time);"
            + '}, 30000);'
            // 监听 keyDown 与 keyUp（M8 的 up 参数）
            + 'Player.keyTrigger(function (key) { window.__got.push("kd:" + key); }, 30000);'
            + 'Player.keyTrigger(function (key) { window.__got.push("ku:" + key); }, 30000, true);'
            + 'window.__triggerIds = ['
            + '  Player.commentTrigger(function () { }, 5000),'
            + '  Player.keyTrigger(function () { }, 5000)'
            + '];')
    ]);
    host.setState(0.5, true, 1);
    host.runFrames(40);   // 到约 1.2s，条目已激活

    // 条目还没进窗口时收到的事件不该投递（这里条目已在窗口内，验证投递生效）。
    host.api.pushComment({ txt: '你好', time: 1.6, color: 0x66CCFF, mode: 1, fontSize: 25 });
    host.api.pushKey(37, false);   // Left
    host.api.pushKey(37, true);    // Left up
    host.api.pushKey(65, false);   // A
    host.api.pushKey(13, false);   // Enter —— 不在 M8 允许的键里，应被忽略

    assert.deepEqual(
        JSON.parse(JSON.stringify(host.sandbox.__got)),
        ['c:你好@1.6', 'kd:37', 'ku:37', 'kd:65'],
        'M8 允许的键与发送弹幕都应投递，且 keyUp 只投给 up=true 的触发器');
    assert.deepEqual(
        JSON.parse(JSON.stringify(host.sandbox.__triggerIds)).length, 2,
        'commentTrigger / keyTrigger 应按 M8 返回 id');

    // 条目窗口 [1s,4s] 结束后：定时器与触发器一起被清，再推事件不得触发。
    host.runFrames(240);   // 到约 5.2s
    const afterWindow = host.sandbox.__got.length;
    host.api.pushComment({ txt: '窗口外', time: 5.2 });
    host.api.pushKey(38, false);
    assert.equal(
        host.sandbox.__got.length, afterWindow,
        '条目回收后 commentTrigger / keyTrigger 不得再触发');

    // reset 整批作废后同理。
    host.reset(0, true, 1, true);
    host.runFrames(2);
    host.api.pushComment({ txt: 'reset 后', time: 0.1 });
    host.api.pushKey(38, false);
    assert.equal(
        host.sandbox.__got.length, afterWindow,
        'reset 之后旧条目的触发器不得再触发');
    assert.equal(host.errors().length, 0, '触发器不应产生错误上报');
});

test('D17 Player.setMask 把画面裁到遮罩形状里（合成期裁剪，不动元素缓存）', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('d17', 0, 10,
            // 遮罩：左上角 100×100 的矩形。
            'var mask = $.createShape({ x: 0, y: 0 });'
            + 'mask.graphics.beginFill(0xFF0000, 1);'
            + 'mask.graphics.drawRect(0, 0, 100, 100);'
            + 'mask.graphics.endFill();'
            // 两个方块：一个在遮罩内，一个在遮罩外（右侧 400px 处）。
            + 'var inside = $.createShape({ x: 10, y: 10, lifeTime: 10 });'
            + 'inside.graphics.beginFill(0x00FF00, 1);'
            + 'inside.graphics.drawRect(0, 0, 20, 20);'
            + 'inside.graphics.endFill();'
            + 'window.__inside = inside;'
            + 'var outside = $.createShape({ x: 400, y: 10, lifeTime: 10 });'
            + 'outside.graphics.beginFill(0x0000FF, 1);'
            + 'outside.graphics.drawRect(0, 0, 20, 20);'
            + 'outside.graphics.endFill();'
            + 'window.__outside = outside;'
            + 'window.__mask = mask;'
            + 'Player.setMask(mask);')
    ]);
    host.setState(0.1, true, 1);
    host.runFrames(4);

    assert.equal(host.frameErrors.length, 0, 'setMask 不应让帧回调抛错：' + host.frameErrors);
    assert.equal(host.errors().length, 0, 'setMask 不应产生错误上报：' + JSON.stringify(host.errors()));

    const marks = host.mainCanvas().__marks;
    assert.ok(marks.length > 0, '遮罩内应有内容被画出来');
    // 所有落笔都必须落在 100×100 的遮罩内（桩把落笔与裁剪区求交）。
    const union = unionRect(marks);
    assert.ok(
        union.x + union.width <= 100.5 && union.y + union.height <= 100.5,
        '所有落笔都应被裁到遮罩内，实际 union=' + JSON.stringify(union));

    // 遮罩元件本身不参与渲染（M8 的遮罩对象不在显示列表里）。
    const mask = host.sandbox.__mask;
    assert.equal(mask.treeParent, null, '遮罩元件应从渲染树摘除');
    assert.equal(mask.expired, false, '遮罩元件应仍然存活（条目回收时才释放）');

    // 裁剪是合成期的：元素自己的位图缓存不受影响，元素也没被标成结构脏。
    const outside = host.sandbox.__outside;
    assert.ok(host.elementField(outside, 'cacheCanvas'), '遮罩外的元素仍应有自己的位图缓存');
    assert.equal(host.elementField(outside, 'expired'), false, '遮罩外元素不应被摘除');

    // 换掉遮罩（设为 null）后画面恢复完整：遮罩外的方块重新可见。
    host.append([scriptItem('d17b', 1, 8, 'Player.setMask(null);')]);
    host.runFrames(90);
    const afterUnion = unionRect(host.mainCanvas().__marks);
    assert.ok(
        afterUnion && afterUnion.x + afterUnion.width > 400,
        '取消遮罩后遮罩外的元素应重新可见，实际 union=' + JSON.stringify(afterUnion));
    assert.equal(host.errors().length, 0, '取消遮罩不应产生错误上报');
});

test('D18 元素 transform：matrix 与 props.matrix 同一份、matrix3D 可读写、距离矩阵可用', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('d18', 0, 10,
            'var box = $.createShape({ x: 0, y: 0, lifeTime: 10 });'
            + 'box.graphics.beginFill(0xFFFFFF, 1);'
            + 'box.graphics.drawRect(0, 0, 20, 20);'
            + 'box.graphics.endFill();'
            + 'window.__box = box;'
            // transform 命名空间必须挂在元素自身上
            + 'window.__hasTransform = typeof box.transform === "object";'
            // matrix 取出→原地改→写回（Akari 的写法）必须作用于同一份对象
            + 'var mx = box.transform.matrix;'
            + 'window.__matrixIsProps = (mx === box.props.matrix);'
            + 'mx.identity();'
            + 'window.__afterIdentity = [mx.a, mx.b, mx.c, mx.d, mx.tx, mx.ty].join(",");'
            + 'mx.translate(5, 6);'
            + 'mx.scale(2, 3);'
            + 'box.transform.matrix = mx;'
            + 'window.__afterOps = [box.transform.matrix.a, box.transform.matrix.d,'
            + ' box.transform.matrix.tx, box.transform.matrix.ty].join(",");'
            // matrix3D：可赋值（含 null）、可读回，读回值要能被 clone/append
            + 'box.transform.matrix3D = null;'
            + 'window.__m3dNull = box.transform.matrix3D;'
            + 'var m3 = $.createMatrix3D([1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]);'
            + 'm3.appendTranslation(10, 20, 30);'
            + 'm3.appendRotation(90, $.createVector3D(1, 0, 0));'
            + 'var v = m3.transformVector($.createVector3D(1, 0, 0));'
            + 'window.__m3dVector = [v.x, v.y, v.z].map(function (n) { return Math.round(n * 100) / 100; }).join(",");'
            + 'box.transform.matrix3D = m3;'
            + 'window.__m3dSame = (box.transform.matrix3D === m3);'
            // 相对矩阵：返回带 transformVectors 的 Matrix3D（Akari 的 3D 排序要用）
            + 'var rel = box.transform.getRelativeMatrix3D(null);'
            + 'var vLocal = $.toNumberVector([0, 0, 0]);'
            + 'var vWorld = $.toNumberVector([]);'
            + 'rel.transformVectors(vLocal, vWorld);'
            + 'window.__worldLength = vWorld.length;'
            + 'window.__perspective = typeof box.transform.perspectiveProjection.fieldOfView;')
    ]);
    host.setState(0.1, true, 1);
    host.runFrames(2);

    assert.equal(host.frameErrors.length, 0, '不应抛错：' + host.frameErrors);
    assert.equal(host.errors().length, 0, 'transform 不应产生错误：' + JSON.stringify(host.errors()));
    assert.equal(host.sandbox.__hasTransform, true, 'element.transform 应是对象');
    assert.equal(host.sandbox.__matrixIsProps, true, 'transform.matrix 必须与 props.matrix 是同一份对象');
    assert.equal(host.sandbox.__afterIdentity, '1,0,0,1,0,0', 'identity() 应重置为单位矩阵');
    assert.equal(host.sandbox.__afterOps, '2,3,5,6', 'translate/scale 应就地生效');
    assert.equal(host.sandbox.__m3dNull, null, 'matrix3D 赋 null 后应读回 null（不报错）');
    assert.equal(host.sandbox.__m3dSame, true, 'matrix3D 应原样存回');
    // 绕 X 轴转 90°：(1,0,0) → (1,0,0)（X 轴不变），平移分量是 (10,20,30)
    assert.equal(host.sandbox.__m3dVector, '11,20,30', 'Matrix3D 的平移与旋转应真算');
    assert.equal(host.sandbox.__worldLength, 3, 'transformVectors 必须原地填充目标数组');
    assert.equal(host.sandbox.__perspective, 'number', 'perspectiveProjection 应是可读的默认值对象');
});

test('D19 显示列表：numChildren/getChildAt 等按 Flash 语义，且元素属性不可枚举', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('d19', 0, 10,
            'var box = $.createShape({ lifeTime: 10 });'
            + 'window.__box = box;'
            + 'var a = $.createShape({ parent: box, name: "a" });'
            + 'var b = $.createShape({ parent: box });'
            + 'var c = $.createShape({ parent: box });'
            + 'window.__kids = [a, b, c];'
            + 'window.__flash = {'
            + '  num: box.numChildren,'
            + '  first: box.getChildAt(0) === a,'
            + '  last: box.getChildAt(2) === c,'
            + '  outOfRange: box.getChildAt(9),'
            + '  index: box.getChildIndex(b),'
            + '  byName: box.getChildByName("a") === a,'
            + '  contains: box.contains(a),'
            + '  ownNumChildren: box.hasOwnProperty("numChildren"),'
            + '  ownGraphics: box.hasOwnProperty("graphics"),'
            // Flash 里显示对象的属性在原型上：foreach / for-in 拿不到任何一项。
            // Akari 的 Factory.clone 正是靠这个分叉（countProperties === 0）。
            + '  foreachCount: (function () { var n = 0; foreach(box, function () { n++; }); return n; })()'
            + '};'
            + 'box.setChildIndex(c, 0);'
            + 'window.__afterSwap = box.getChildAt(0) === c;'
            + 'box.removeChildAt(0);'
            + 'window.__afterRemove = box.numChildren;'
            + 'box.addChildAt(c, 0);'
            + 'window.__afterAdd = [box.numChildren, box.getChildAt(0) === c].join(",");')
    ]);
    host.setState(0.1, true, 1);
    host.runFrames(2);

    assert.equal(host.frameErrors.length, 0, '不应抛错：' + host.frameErrors);
    assert.equal(host.errors().length, 0, '显示列表 API 不应报错：' + JSON.stringify(host.errors()));
    const flash = host.sandbox.__flash;
    assert.equal(flash.num, 3, 'numChildren 应是子元件数');
    assert.equal(flash.first, true, 'getChildAt(0) 应是第一个子元件');
    assert.equal(flash.last, true, 'getChildAt(2) 应是第三个子元件');
    assert.equal(flash.outOfRange, null, '越界的 getChildAt 应返回 null 而不是抛错');
    assert.equal(flash.index, 1, 'getChildIndex 应返回下标');
    assert.equal(flash.byName, true, 'getChildByName 应按 name 找到子元件');
    assert.equal(flash.contains, true, 'contains 应沿父链判断');
    assert.equal(flash.ownNumChildren, true, 'numChildren 必须是 own property（脚本用它判断显示对象）');
    assert.equal(flash.ownGraphics, true, 'graphics 必须是 own property');
    assert.equal(
        flash.foreachCount, 0,
        'foreach 遍历显示对象应一个属性都拿不到（Flash 的属性在原型上）——'
        + 'Akari 的 clone 靠这个分叉，可枚举会让它顺着对象图无限递归');
    assert.equal(host.sandbox.__afterSwap, true, 'setChildIndex 应改变顺序');
    assert.equal(host.sandbox.__afterRemove, 2, 'removeChildAt 应移除子元件');
    assert.equal(host.sandbox.__afterAdd, '3,true', 'addChildAt 应插回指定位置');
});

test('D20 blendMode 映射到 globalCompositeOperation，未知值退回 normal', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('d20', 0, 10,
            'var add = $.createShape({ x: 0, y: 0, lifeTime: 10, blendMode: "add" });'
            + 'add.graphics.beginFill(0xFF0000, 1);'
            + 'add.graphics.drawRect(0, 0, 10, 10);'
            + 'add.graphics.endFill();'
            + 'window.__add = add;'
            + 'var unk = $.createShape({ x: 100, y: 0, lifeTime: 10, blendMode: "layer" });'
            + 'unk.graphics.beginFill(0x00FF00, 1);'
            + 'unk.graphics.drawRect(0, 0, 10, 10);'
            + 'unk.graphics.endFill();'
            + 'window.__unk = unk;'
            + 'var mul = $.createShape({ x: 200, y: 0, lifeTime: 10, blendMode: "multiply" });'
            + 'mul.graphics.beginFill(0x0000FF, 1);'
            + 'mul.graphics.drawRect(0, 0, 10, 10);'
            + 'mul.graphics.endFill();'
            + 'window.__mul = mul;'
            + 'window.__readBack = add.blendMode;')
    ]);
    host.setState(0.1, true, 1);
    host.runFrames(4);

    assert.equal(host.frameErrors.length, 0, '不应抛错：' + host.frameErrors);
    assert.equal(
        host.errors().length, 0,
        '未知 blendMode（"layer"）不得抛错：' + JSON.stringify(host.errors()));
    assert.equal(host.sandbox.__readBack, 'add', '脚本读回的 blendMode 应是它写进去的原值');

    // 落笔时的 composite 必须按元素各自的 blendMode 走。
    const canvas = host.mainCanvas();
    const byX = {};
    for (const op of canvas.__ops) {
        if (op.type !== 'drawImage') continue;
        const x = Math.round(op.rect.x);
        if (x < 20) byX.add = op.composite;
        else if (x > 180) byX.multiply = op.composite;
        else if (x > 80 && x < 130) byX.unknown = op.composite;
    }

    assert.equal(byX.add, 'lighter', 'blendMode "add" 应映射到 lighter');
    assert.equal(byX.multiply, 'multiply', 'blendMode "multiply" 应映射到 multiply');
    assert.equal(byX.unknown, 'source-over', '未知 blendMode 应退回 normal（source-over）');
});

test('D21 元素级 mask 只裁被遮罩元素，且遮罩元件自己不绘制', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    // 用数组 join 拼脚本，避免长串拼接里少一个 + 或引号而看不出问题。
    const code = [
        'var maskEl = $.createShape({ x: 0, y: 0, lifeTime: 10 });',
        'maskEl.graphics.beginFill(0xFFFFFF, 1);',
        'maskEl.graphics.drawRect(0, 0, 40, 40);',
        'maskEl.graphics.endFill();',
        // 被遮罩的容器：子元件铺满 200x200，实际只有左上 40x40 可见
        'var box = $.createShape({ x: 0, y: 0, lifeTime: 10 });',
        'var inner = $.createShape({ parent: box });',
        'inner.graphics.beginFill(0xFF0000, 1);',
        'inner.graphics.drawRect(0, 0, 200, 200);',
        'inner.graphics.endFill();',
        'box.mask = maskEl;',
        'window.__mask = maskEl;',
        'window.__box = box;',
        // 同一画布上的另一个元素不受影响（mask 作用域是子树，不是整块画布）
        'var other = $.createShape({ x: 400, y: 0, lifeTime: 10 });',
        'other.graphics.beginFill(0x00FF00, 1);',
        'other.graphics.drawRect(0, 0, 60, 60);',
        'other.graphics.endFill();',
        'window.__other = other;'
    ].join('\n');

    host.append([scriptItem('d21', 0, 10, code)]);
    host.setState(0.1, true, 1);
    host.runFrames(4);

    assert.equal(host.frameErrors.length, 0, '不应抛错：' + host.frameErrors);
    assert.equal(host.errors().length, 0, '元素遮罩不应报错：' + JSON.stringify(host.errors()));

    const marks = host.mainCanvas().__marks;
    assert.ok(marks.length > 0, '应有落笔');

    // 被遮罩元素（x < 300）的落笔必须落在 40x40 内。
    const masked = marks.filter((rect) => rect.x < 300);
    assert.ok(masked.length > 0, '被遮罩元素应有落笔：' + JSON.stringify(marks));
    for (const rect of masked) {
        assert.ok(
            rect.x >= -1 && rect.y >= -1 && rect.x + rect.width <= 41 && rect.y + rect.height <= 41,
            '被遮罩元素的落笔应被裁到 40x40 内，实际 ' + JSON.stringify(rect));
    }

    // 遮罩元件自己不应参与合成（Flash 的遮罩对象不参与渲染）：
    // lastPaintedRect 是「上一帧在主画布上画到哪」的记录，没参与合成就应该一直是 null。
    assert.equal(
        host.elementField(host.sandbox.__mask, 'lastPaintedRect'), null,
        '遮罩元件不应参与合成（它只是裁剪形状）');

    // 未被遮罩的邻居照常画在自己位置：证明遮罩没串成整块画布。
    const neighbor = marks.filter((rect) => rect.x >= 390);
    assert.ok(neighbor.length > 0, '未被遮罩的元素应正常绘制：' + JSON.stringify(marks));
});

test('D22 popEl 不摘离渲染树；Event.ENTER_FRAME 每帧派发', () => {
    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append([
        scriptItem('d22', 0, 10,
            'var sprite = $.createShape({ x: 10, y: 10, lifeTime: 10 });'
            + 'sprite.graphics.beginFill(0xFFFFFF, 1);'
            + 'sprite.graphics.drawRect(0, 0, 30, 30);'
            + 'sprite.graphics.endFill();'
            // M8 的 popEl 语义：从「自动清理表」里弹出，**不是**从显示列表摘掉。
            // Akari 把整幅作品挂在 popEl 过的常驻 root 下，摘掉就一个像素都没有。
            + 'ScriptManager.popEl(sprite);'
            + 'window.__sprite = sprite;'
            + 'window.__frames = 0;'
            + 'function onFrame(e) { window.__frames++; window.__lastType = e.type; }'
            + 'sprite.addEventListener("enterFrame", onFrame);'
            + 'window.__eventNames = [typeof sprite.removeEventListener, typeof sprite.dispatchEvent,'
            + ' sprite.hasEventListener("enterFrame")].join(",");')
    ]);
    host.setState(0.1, true, 1);
    host.runFrames(10);

    assert.equal(host.frameErrors.length, 0, '不应抛错：' + host.frameErrors);
    assert.equal(host.errors().length, 0, '不应报错：' + JSON.stringify(host.errors()));

    const sprite = host.sandbox.__sprite;
    assert.equal(
        sprite.treeParent, host.sandbox.__sprite.treeParent && sprite.treeParent,
        'popEl 后元件仍应在渲染树里（treeParent 不为 null）');
    assert.notEqual(sprite.treeParent, null, 'popEl 不得把元件摘离渲染树');
    assert.equal(host.mainCanvas().__marks.length > 0, true, 'popEl 过的元件照旧要画出来');

    assert.equal(host.sandbox.__eventNames, 'function,function,true', 'EventDispatcher API 应齐备');
    assert.ok(host.sandbox.__frames > 0, 'enterFrame 监听应被逐帧派发');
    assert.equal(host.sandbox.__lastType, 'enterFrame', '派发的事件对象应带 type');
});

run();
