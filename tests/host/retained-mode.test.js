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
        const rect = transformedAABB(ctx.__matrix, x, y, width, height);
        canvas.__marks.push(rect);
        canvas.__ops.push({ type: kind, rect: rect, font: ctx.font });
        count(kind);
        return rect;
    }

    function recordPath(kind) {
        const bounds = pathBounds(ctx.__path);
        if (!bounds) return;
        record(kind, bounds.x, bounds.y, bounds.width, bounds.height);
    }

    ctx.save = function () { ctx.__stack.push(ctx.__matrix.slice()); };
    ctx.restore = function () {
        if (ctx.__stack.length > 0) ctx.__matrix = ctx.__stack.pop();
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
        elementField(element, name) {
            assert.ok(element && typeof element === 'object', '元素探针未设置：脚本里没有 window.__probe = ...');
            assert.ok(
                Object.keys(element).indexOf(name) >= 0,
                '元素上不存在字段 ' + name + '，实际字段：' + Object.keys(element).join(', '));
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
        'var box = ctx.createShape();'
        + 'box.graphics.beginFill(0xFFFFFF, 1);'
        + 'box.graphics.drawRect(0, 0, 40, 20);'
        + 'box.graphics.endFill();'
        + 'window.__box = box;'
        + 'ctx.tween(box, { x: { fromValue: 0, toValue: 400, easing: "Linear" } }, { lifeTime: 10 });')]);

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
            'var bad = ctx.createShape();'
            + 'bad.graphics.beginFill(0xFF0000, 1);'
            + 'bad.graphics.drawRect(0, 0, 20, 20);'
            + 'bad.graphics.endFill();'
            + 'ctx.tween(bad, { x: { fromValue: 0, toValue: 100, easing: function (time, begin) {'
            + '  if (time > 0) { throw new Error("坏缓动"); }'
            + '  return begin;'
            + '} } }, { lifeTime: 10 });'),
        scriptItem('d2-good', 0, 10,
            'var good = ctx.createShape();'
            + 'good.graphics.beginFill(0x00FF00, 1);'
            + 'good.graphics.drawRect(0, 0, 20, 20);'
            + 'good.graphics.endFill();'
            + 'good.y = 200;'
            + 'window.__good = good;'
            + 'ctx.tween(good, { x: { fromValue: 0, toValue: 300, easing: "Linear" } }, { lifeTime: 10 });')
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
        'window.__after = ctx.createShape();')]);
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

    const shape = 'var box = ctx.createShape();'
        + 'box.graphics.beginFill(0xFFFFFF, 1);'
        + 'box.graphics.drawRect(0, 0, 20, 20);'
        + 'box.graphics.endFill();'
        + 'window.__box = box;';

    // 声明 4s、窗口 10s：寿命 4000ms（声明生效，不被窗口吞掉）。
    let host = lifeTimeOf(0, 10, shape + 'ctx.tween(box, { x: { fromValue: 0, toValue: 10 } }, { lifeTime: 4 });');
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
    host = lifeTimeOf(0, 10, shape + 'ctx.tween(box, { x: { fromValue: 0, toValue: 10 } }, { lifeTime: 2 });');
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
    host = lifeTimeOf(0, 10, shape + 'ctx.tween(box, { x: { fromValue: 0, toValue: 10 } }, { lifeTime: 8 });');
    assert.equal(
        host.elementField(host.sandbox.__box, 'lifeTimeMs'), 8000,
        '声明 lifeTime: 8、窗口 10s 时寿命应为 8000ms');

    // 声明 8s、窗口 4s：寿命被窗口钳到 4000ms。
    host = lifeTimeOf(0, 4, shape + 'ctx.tween(box, { x: { fromValue: 0, toValue: 10 } }, { lifeTime: 8 });');
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
            'var group = ctx.createShape();'
            + 'var dot = ctx.createShape();'
            + 'dot.graphics.beginFill(0xFFFFFF, 1);'
            + 'dot.graphics.drawCircle(0, 0, 5);'
            + 'dot.graphics.endFill();'
            + 'dot.x = 100;'
            + 'dot.y = 100;'
            + 'ctx.addChild(dot, group);'
            + 'window.__group = group;'),
        scriptItem('d4-anim', 0, 10,
            'var mover = ctx.createShape();'
            + 'mover.graphics.beginFill(0x00FF00, 1);'
            + 'mover.graphics.drawRect(0, 0, 20, 20);'
            + 'mover.graphics.endFill();'
            + 'mover.y = 300;'
            + 'ctx.tween(mover, { x: { fromValue: 0, toValue: 300, easing: "Linear" } }, { lifeTime: 10 });')
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
            'var label = ctx.createText("AAAA", { fontsize: 12 });'
            + 'window.__label = label;'
            + 'ctx.tween(label, { fontsize: { fromValue: 12, toValue: 40, easing: "Linear" } }, { lifeTime: 10 });'),
        scriptItem('d5-static', 0, 10,
            'var still = ctx.createText("BBBB", { fontsize: 12 });'
            + 'still.x = 400;'
            + 'still.y = 400;'
            + 'window.__still = still;')
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
        + 'var box = ctx.createShape();'
        + 'box.graphics.beginFill(0xFFFFFF, 1);'
        + 'box.graphics.drawRect(0, 0, 40, 20);'
        + 'box.graphics.endFill();'
        + 'window.__box = box;'
        + 'ctx.tween(box, { x: { fromValue: 0, toValue: 400, easing: "Linear" } }, { lifeTime: 4 });')]);
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
        + 'var box = ctx.createShape();'
        + 'box.graphics.beginFill(0xFFFFFF, 1);'
        + 'box.graphics.drawRect(0, 0, 40, 20);'
        + 'box.graphics.endFill();'
        + 'window.__box = box;'
        + 'ctx.tween(box, { x: { fromValue: 0, toValue: 400, easing: "Linear" } }, { lifeTime: 2 });')]);
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

test('D7 内置示例（单条）声明式 tween 与逃生舱并存，能渲染出画面且到点自然收尾', () => {
    // 示例代码必须与 ScriptDanmakuService.GetBuiltInDemo() 逐字一致。
    // 它在 C# 里是字符串拼接，这里改写成等价字面量；两边的"事实来源"
    // 仍是那份 C#，本用例只保证示例确实能跑、且不是每帧重算坐标的写法。
    const demos = [
        {
            id: 'demo-m8-sample', stime: 1, duration: 7, lang: 'js',
            code: "var label = ctx.createText('脚本弹幕已生效', { font: 'sans-serif', fontsize: 32, color: 0x66CCFF });"
                + 'label.y = Math.round(ctx.height / 2 - 19);'
                + 'ctx.tween(label, {'
                + "  x: { fromValue: ctx.width + 120, toValue: -120, easing: 'Linear' }"
                + '}, { lifeTime: 4 });'
                + 'var count = 24;'
                + 'var cx = ctx.width / 2;'
                + 'var cy = ctx.height / 2;'
                + 'var dots = [];'
                + 'for (var i = 0; i < count; i++) {'
                + '  var dot = ctx.createShape();'
                + '  dot.graphics.beginFill(0xFF66CC, 1);'
                + '  dot.graphics.drawCircle(0, 0, 6);'
                + '  dot.graphics.endFill();'
                + '  dot.visible = false;'
                + '  dots.push(dot);'
                + '}'
                + 'ctx.onFrame(function (frameCtx, elapsedMs) {'
                + '  var local = elapsedMs - 2000;'
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
                + '});'
        }
    ];

    const host = loadHost();
    host.reset(0, true, 1, true);
    host.append(demos);
    host.setState(0.5, true, 1);

    // 两条示例都不得退回立即模式的写法。
    for (const demo of demos) {
        assert.equal(demo.code.indexOf('ctx.progress'), -1, demo.id + ' 不得再用 ctx.progress 逐帧重算');
        assert.equal(demo.code.indexOf('ctx.g.'), -1, demo.id + ' 不得直接操作画布上下文');
    }

    // 单条脚本里两种写法并存：文字走声明式 tween；粒子要恒定 6px 点半径，
    // tween 表达不了（scale 会把点一起放大），因此走 ctx.onFrame 逃生舱逐帧只改位置。
    assert.ok(demos[0].code.indexOf('ctx.tween(') >= 0, '示例必须用 ctx.tween 声明文字动画');
    assert.ok(demos[0].code.indexOf('ctx.onFrame(') >= 0, '示例必须用 ctx.onFrame 声明粒子逐帧路径');
    assert.equal(demos[0].code.indexOf('scaleX'), -1, '示例不得用 scale 扩散：那会把点一起放大');
    assert.ok(
        demos[0].code.indexOf('elapsedMs - 2000') >= 0,
        '粒子晚于文字 2 秒出现，onFrame 没有 delay，须自行扣掉起始偏移');

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
        code: "var t = ctx.createText('AAAA', { font: 'sans-serif', fontsize: 48, color: 0xFF0000 });"
            + 't.x = 100;t.y = 100;'
    }]);
    host.setState(0.5, true, 1);
    host.runFrames(3);
    assert.ok(painted(), '第一批弹幕应已画到画布上');

    host.reset(0, true, 1, true);
    host.append([{
        id: 'gen2', stime: 0, duration: 8, lang: 'js',
        code: "var t = ctx.createText('BBBB', { font: 'sans-serif', fontsize: 48, color: 0x0000FF });"
            + 't.x = 400;t.y = 400;'
    }]);
    assert.equal(
        painted(), null,
        'reset 之后画布必须被清空，否则上一批弹幕的像素会永久残留');

    host.setState(0.5, true, 1);
    host.runFrames(3);
    assert.ok(painted(), '第二批弹幕应能正常画出来');
});

run();
