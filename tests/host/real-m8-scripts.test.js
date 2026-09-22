'use strict';

// **真实 M8 脚本**的集成测试：把当年 B 站「代码弹幕」（mode=8）的脚本文本原样
// 灌进宿主，断言它们既能跑完（0 runtime error）也能真的画出东西。
//
// 为什么单开一个文件而不并进 retained-mode.test.js：那边是行为单测（每条用例
// 手写一小段脚本、断言某个具体行为），这边是集成测试（几百 KB 的第三方脚本、
// 断言「不报错 + 有落笔」）。两者的失败排查路径完全不同，混在一起会互相拖慢。
//
// 零依赖，与 retained-mode.test.js 同一套 node:vm 桩（桩代码按同一份约定写）：
//   node tests/host/real-m8-scripts.test.js
// 追加额外的夹具目录（可选）：
//   M8_SCRIPT_FIXTURES=/path/to/scripts-fixtures node tests/host/real-m8-scripts.test.js
//
// 全 11 条夹具**都在仓库里**（tests/host/fixtures/real/，约 808 KB）：出画面的主作品
// entry_10 的文字图层要从 $G 取 6 张字形表，而那些表由另外 7 条字体脚本注册，
// 少一条就报错，所以「只放出画面的两条」在仓库里跑不通，只能全量入库。

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const HOST_PATH = process.env.SCRIPT_DANMAKU_HOST || path.join(
    __dirname, '..', '..', 'BiliBili.UWP', 'Assets', 'script-danmaku-host.html');

// 仓库自带的夹具目录。两条：entry_08（出画面）+ 一条字体数据脚本（代表其余 8 条）。
const FIXTURE_DIR = path.join(__dirname, 'fixtures', 'real');
// 外部完整夹具目录（可选）：设了就跑全 11 条。
const EXTRA_FIXTURE_DIR = process.env.M8_SCRIPT_FIXTURES || '';

// ---- canvas 桩（记录落笔；约定与 retained-mode.test.js 一致）----

function createCanvasStub() {
    const canvas = {
        style: {},
        __isCanvasStub: true,
        __marks: [],
        __counts: {},
        __w: 300,
        __h: 150,
        __ctx: null,
        get width() { return canvas.__w; },
        set width(value) { canvas.__w = value; },
        get height() { return canvas.__h; },
        set height(value) { canvas.__h = value; },
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
        globalCompositeOperation: 'source-over',
        filter: 'none',
        textBaseline: 'alphabetic',
        textAlign: 'start'
    };

    function count(name) {
        canvas.__counts[name] = (canvas.__counts[name] || 0) + 1;
    }

    // 只关心「有没有落笔」，不关心落笔几何（几何断言在 retained-mode.test.js 里）。
    function mark(name) {
        count(name);
        canvas.__marks.push(name);
    }

    ctx.save = function () { };
    ctx.restore = function () { };
    ctx.setTransform = function () { };
    ctx.transform = function () { };
    ctx.translate = function () { };
    ctx.scale = function () { };
    ctx.rotate = function () { };
    ctx.clip = function () { };
    ctx.beginPath = function () { };
    ctx.closePath = function () { };
    ctx.moveTo = function () { };
    ctx.lineTo = function () { };
    ctx.rect = function () { };
    ctx.arc = function () { };
    ctx.ellipse = function () { };
    ctx.quadraticCurveTo = function () { };
    ctx.bezierCurveTo = function () { };
    ctx.clearRect = function () { count('clearRect'); };
    ctx.fillRect = function () { mark('fillRect'); };
    ctx.strokeRect = function () { mark('strokeRect'); };
    ctx.fillText = function () { mark('fillText'); };
    ctx.strokeText = ctx.fillText;
    ctx.measureText = function (text) {
        return { width: String(text).length * 6 };
    };
    ctx.drawImage = function () { mark('drawImage'); };
    ctx.fill = function () { mark('fill'); };
    ctx.stroke = function () { mark('stroke'); };
    ctx.createLinearGradient = function () {
        return { addColorStop: function () { } };
    };
    ctx.createRadialGradient = function () {
        return { addColorStop: function () { } };
    };
    return ctx;
}

// ---- 宿主加载器 ----

function loadHost() {
    const html = fs.readFileSync(HOST_PATH, 'utf8');
    const match = /<script>([\s\S]*?)<\/script>/.exec(html);
    if (!match) throw new Error('宿主 HTML 里找不到内联 <script>：' + HOST_PATH);

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
    sandbox.console = { log() { }, warn() { }, error() { } };
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
    vm.runInContext(match[1], sandbox, { filename: HOST_PATH });

    return {
        sandbox: sandbox,
        container: container,
        messages: messages,
        frameErrors: frameErrors,
        api: sandbox.scriptDanmakuHost,
        now: () => clock,
        errors: () => messages.filter((message) => message.type === 'error'),
        mainCanvas() {
            const canvas = container.children.find((child) => child && child.__isCanvasStub);
            assert.ok(canvas, '宿主启动后应把主画布挂到 #stage');
            return canvas;
        },
        runFrames(count, stepMs) {
            const step = stepMs === undefined ? 1000 / 60 : stepMs;
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
        }
    };
}

function readFixtureDir(directory) {
    if (!directory || !fs.existsSync(directory)) {
        return [];
    }

    return fs.readdirSync(directory)
        .filter((name) => /\.(js|bin)$/.test(name))
        .sort()
        .map((name) => ({
            id: name.replace(/\.(js|bin)$/, ''),
            stime: 0,
            duration: 200,
            lang: 'js',
            code: fs.readFileSync(path.join(directory, name), 'utf8')
        }));
}

function isFontDataScript(code) {
    return /^\s*var\s+fontData\s*=/.test(code);
}

// ---- 极简用例运行器（与 retained-mode.test.js 同款）----

const cases = [];

function test(name, fn) {
    cases.push({ name: name, fn: fn });
}

function run() {
    let failed = 0;
    let skipped = 0;
    for (const item of cases) {
        try {
            const result = item.fn();
            if (result === 'skip') {
                skipped++;
                process.stdout.write('  skip ' + item.name + '\n');
                continue;
            }

            process.stdout.write('  ok   ' + item.name + '\n');
        } catch (error) {
            failed++;
            process.stdout.write('  FAIL ' + item.name + '\n');
            process.stdout.write('       ' + String(error && error.message).split('\n').join('\n       ') + '\n');
        }
    }

    process.stdout.write('\n' + (cases.length - failed - skipped) + '/' + cases.length + ' 通过'
        + (skipped > 0 ? '（跳过 ' + skipped + '）' : '')
        + '（夹具：' + FIXTURE_DIR + (EXTRA_FIXTURE_DIR ? ' + ' + EXTRA_FIXTURE_DIR : '') + '）\n');
    if (failed > 0) process.exitCode = 1;
}

// ---- 用例 ----

const REPO_FIXTURES = readFixtureDir(FIXTURE_DIR);
const ALL_FIXTURES = REPO_FIXTURES.concat(readFixtureDir(EXTRA_FIXTURE_DIR));

test('R1 仓库自带夹具齐备（全 11 条真实脚本）', () => {
    const ids = REPO_FIXTURES.map((item) => item.id);
    assert.ok(ids.length >= 11, '仓库应自带 11 条真实脚本夹具，实际 ' + ids.length + '：' + ids.join(','));
    assert.ok(
        ids.some((id) => id.indexOf('entry_08') === 0),
        '缺少 entry_08（Akari 库 + 骨架，出画面的两条之一）');
    assert.ok(
        ids.some((id) => id.indexOf('entry_10') === 0),
        '缺少 entry_10（主作品，全 11 条里真正落笔的那条）');
    assert.ok(
        REPO_FIXTURES.some((item) => isFontDataScript(item.code)),
        '缺少字体数据脚本（只注册字形表、不出画面的那 8 条）');
});

test('R2 仓库夹具在同一宿主实例里跑完：0 runtime error', () => {
    const host = loadHost();
    host.api.reset(0, true, 1, true);
    for (const item of REPO_FIXTURES) {
        host.api.append([item]);
    }

    // 时间轴要推到作品真正出画的位置：entry_10 的分层 inPoint 在 87s 之后
    // （`inPoint: 87000`），所以这里用大步长把播放头推到 150s。
    host.runFrames(150, 1000);

    assert.equal(host.frameErrors.length, 0, '帧回调不应抛错：' + host.frameErrors);
    const errors = host.errors();
    assert.deepEqual(
        errors.map((message) => message.itemId + ': ' + message.message),
        [],
        '真实脚本不应产生任何 runtime error');
});

test('R3 字体数据脚本不出画面（它们只注册数据，不是绘制脚本）', () => {
    const host = loadHost();
    host.api.reset(0, true, 1, true);
    const fontOnly = REPO_FIXTURES.filter((item) => isFontDataScript(item.code));
    for (const item of fontOnly) {
        host.api.append([item]);
    }

    host.runFrames(150, 1000);

    assert.equal(host.errors().length, 0, '不应报错：' + JSON.stringify(host.errors().slice(0, 3)));
    assert.equal(
        host.mainCanvas().__marks.length, 0,
        '字体数据脚本只注册字形表、不该落笔——真正绘制的是 entry_10，见 R5');
});

test('R4 字体数据脚本把字形表注册进 $G（不出画面但数据要真的落地）', () => {
    const fontFixtures = REPO_FIXTURES.filter((item) => isFontDataScript(item.code));
    if (fontFixtures.length === 0) {
        return 'skip';
    }

    const host = loadHost();
    host.api.reset(0, true, 1, true);
    for (const item of fontFixtures) {
        host.api.append([item]);
    }

    // 探针：字体脚本自己只写 $G，这里再挂一条脚本把 $G 读出来，验证数据真落地了。
    host.api.append([{
        id: 'probe-font',
        stime: 0,
        duration: 200,
        lang: 'js',
        code: 'var keys = [];'
            + 'foreach($G._get("font.arial.black") || {}, function (key) { keys.push(key); });'
            + 'window.__fontProbe = {'
            + '  size: ($G._get("font.arial.black") || {}).size,'
            + '  glyphCount: ($G._get("font.arial.black") || {}).glyphCount,'
            + '  ascender: ($G._get("font.arial.black") || {}).ascender,'
            + '  keys: keys.sort().join(",")'
            + '};'
    }]);

    host.runFrames(4);

    const probe = host.sandbox.__fontProbe;
    assert.ok(probe, '探针脚本应读到 $G 里的字形表（读到 undefined 说明注册链断了）');
    assert.equal(probe.size, 36, '字形表 size 应与夹具声明一致');
    assert.equal(probe.glyphCount, 3, '字形表 glyphCount 应与夹具声明一致');
    assert.equal(probe.ascender, 52, '字形表 ascender 应与夹具声明一致');
    assert.ok(probe.keys.indexOf('X') >= 0, '字形表应含字形 ' + probe.keys);
});

test('R5 全 11 条夹具一起跑：0 runtime error 且真的出画面', () => {
    const host = loadHost();
    host.api.reset(0, true, 1, true);
    for (const item of ALL_FIXTURES) {
        host.api.append([item]);
    }

    host.runFrames(150, 1000);

    assert.equal(host.frameErrors.length, 0, '帧回调不应抛错：' + host.frameErrors);
    const errors = host.errors();
    assert.deepEqual(
        errors.map((message) => message.itemId + ': ' + message.message),
        [],
        ALL_FIXTURES.length + ' 条真实脚本全部不应报错');

    // 「出画面」的判定必须用真实落笔数，不能只看「没报错」。
    //
    // 实测的分工（三条对照跑出来的结论）：
    //  - 全 11 条   ：主画布 79 次 drawImage，0 报错（t≈150s）
    //  - 去掉 entry_10：落笔 0（entry_10 才是真正绘制的主作品）
    //  - 去掉 entry_08：entry_10 直接报错（Akari 库来自 entry_08）
    // 所以「出画面」是 entry_08（Akari 库）+ entry_10（主作品）+ 6 条字体脚本
    // 共同的功劳：entry_10 的文字图层要从 $G 取字形表，entry_08 提供库。
    const canvas = host.mainCanvas();
    assert.ok(
        canvas.__marks.length > 0,
        '11 条真实脚本必须真的画出东西——「没报错」不等于「出画面」。'
        + '落笔数 ' + canvas.__marks.length);
    assert.ok(
        (canvas.__counts.drawImage || 0) > 0,
        'Akari 的分层合成应把图层 blit 到主画布：' + JSON.stringify(canvas.__counts));
});

run();
