'use strict';

// TweenEasing 与注入 API 面的回归测试。
//
// 为什么需要它：宿主把「注入哪些 M8 全局名」和「缓动表」都写成源码字面量，
// 漏改一半、改错映射、或往实参表里加名字却忘了形参，都不会被 C# 侧的源码契约测试发现。
// 这里在 node:vm 中加载真实缓动模块，断言行为：
//  1. 18 个缓动类名齐全，且每类都有 easeIn / easeOut / easeInOut / easeOutIn；
//  2. 每个缓动的端点性质成立（f(0) = begin、f(duration) = begin + change）；
//  3. 关键数值与原版 AS3（org.libspark.betweenas3.core.easing.*）一致，
//     尤其 Quartic 与 Quintic 必须不同——宿主曾把 Quartic 错映射到 quintic；
//  4. 求值形态可用：easeX.calculate(t,b,c,d) 可直调、Custom.func、Physical 三式。
// 另外做两条源码断言：注入名表必须含本批新增的名字，且形参表不得再各自硬编码。
//
// 零依赖：只用 node 内置模块，不 npm install。运行：
//   node tests/host/tween-easing.test.js
// 指定其它宿主文件（例如对照修复前的版本）：
//   node tests/host/tween-easing.test.js /tmp/prefix-host.html

const path = require('path');
const vm = require('vm');
const assert = require('assert');
const { ensureModuleSupport, evaluateModule, readHostSources } = require('./host-loader');

ensureModuleSupport();

const hostPath = process.argv[2]
    || path.join(__dirname, '..', '..', 'BiliBili.UWP', 'Assets', 'script-danmaku-host.html');
const sources = readHostSources(hostPath);
const source = [...sources.values()].join('\n');

async function run() {

    const results = [];

    function check(name, fn) {
        try {
            fn();
            results.push({ name: name, ok: true });
        } catch (error) {
            results.push({ name: name, ok: false, message: error && error.message });
        }
    }

    // ---- 优先求值真实模块；仍兼容拆分前的 HTML，便于旧版对照 ----
    const sandbox = {};
    vm.createContext(sandbox);
    const easingPath = [...sources.keys()].find(file => path.basename(file) === 'easing.js');
    let TweenEasing;
    if (easingPath) {
        TweenEasing = (await evaluateModule(easingPath, sandbox)).namespace.TweenEasing;
    } else {
        const START = '// ---- 补间缓动';
        const END = 'function toFiniteNumber(value, fallback)';
        const startIndex = source.indexOf(START);
        const endIndex = source.indexOf(END);
        assert.ok(startIndex >= 0 && endIndex > startIndex, '旧版宿主里找不到缓动段');
        vm.runInContext(source.slice(startIndex, endIndex) + '\n;this.__TweenEasing = TweenEasing;', sandbox);
        TweenEasing = sandbox.__TweenEasing;
    }

    const EASING_NAMES = ['Back', 'Bounce', 'Circ', 'Circular', 'Cubic', 'Custom', 'Elastic', 'Expo',
        'Exponential', 'Linear', 'Physical', 'Quad', 'Quadratic', 'Quart', 'Quartic', 'Quint',
        'Quintic', 'Sine'];
    const VARIANTS = ['easeIn', 'easeOut', 'easeInOut', 'easeOutIn'];

    check('E1 TweenEasing 暴露全部 18 个原版类名', () => {
        const missing = EASING_NAMES.filter((name) => !(name in TweenEasing));
        assert.deepStrictEqual(missing, [], '缺少的类名：' + JSON.stringify(missing));
    });

    check('E2 每个缓动类都有 easeIn/easeOut/easeInOut/easeOutIn 四个函数', () => {
        EASING_NAMES.filter((name) => name !== 'Custom' && name !== 'Physical').forEach((name) => {
            VARIANTS.forEach((variant) => {
                assert.strictEqual(typeof TweenEasing[name][variant], 'function',
                    name + '.' + variant + ' 不是函数');
            });
        });
    });

    check('E3 所有缓动满足端点性质：f(0)=begin 且 f(duration)=begin+change', () => {
        const odd = [];
        EASING_NAMES.filter((name) => name !== 'Custom' && name !== 'Physical').forEach((name) => {
            VARIANTS.forEach((variant) => {
                const fn = TweenEasing[name][variant];
                const atStart = fn(0, 7, 100, 1000);
                const atEnd = fn(1000, 7, 100, 1000);
                // Elastic/Bounce 的中间值可以过冲，端点必须精确落在 begin / begin+change 上。
                if (Math.abs(atStart - 7) > 1e-9 || Math.abs(atEnd - 107) > 1e-9) {
                    odd.push(name + '.' + variant + ' start=' + atStart + ' end=' + atEnd);
                }
            });
        });
        assert.deepStrictEqual(odd, [], '端点不成立的缓动：' + JSON.stringify(odd));
    });

    check('E4 Quartic 与 Quintic 必须是两条不同的曲线（宿主曾把 Quartic 映射到 quintic）', () => {
        const quart = TweenEasing.Quartic.easeIn(500, 0, 1, 1000);
        const quint = TweenEasing.Quintic.easeIn(500, 0, 1, 1000);
        assert.ok(Math.abs(quart - 0.0625) < 1e-12, 'Quartic.easeIn(0.5) 应是 0.0625，实际 ' + quart);
        assert.ok(Math.abs(quint - 0.03125) < 1e-12, 'Quintic.easeIn(0.5) 应是 0.03125，实际 ' + quint);
        assert.ok(Math.abs(quart - quint) > 1e-6, 'Quartic 与 Quintic 不能是同一条曲线');
    });

    check('E5 标准缓动数值与原版 AS3 一致', () => {
        const cases = [
            ['Cubic', 'easeOut', 500, 0, 100, 1000, 87.5],
            ['Quadratic', 'easeIn', 500, 0, 100, 1000, 25],
            ['Quadratic', 'easeOut', 500, 0, 100, 1000, 75],
            ['Quintic', 'easeOut', 500, 0, 100, 1000, 96.875],
            ['Sine', 'easeInOut', 500, 0, 100, 1000, 50],
            ['Circular', 'easeIn', 250, 0, 100, 1000, 100 * (1 - Math.sqrt(1 - 0.0625))],
            ['Back', 'easeInOut', 500, 0, 1, 1000, 0.5]
        ];
        cases.forEach(([group, variant, time, begin, change, duration, expected]) => {
            const actual = TweenEasing[group][variant](time, begin, change, duration);
            assert.ok(Math.abs(actual - expected) < 1e-9,
                group + '.' + variant + '(' + time + ') 应为 ' + expected + '，实际 ' + actual);
        });
    });

    check('E6 缓动实例可直调 calculate（原版是 IEasing 实例）', () => {
        const easing = TweenEasing.Sine.easeOut;
        assert.strictEqual(typeof easing.calculate, 'function');
        assert.strictEqual(easing.calculate(500, 0, 100, 1000), easing(500, 0, 100, 1000));
    });

    check('E7 Custom.func 与 Physical 三式可用', () => {
        assert.strictEqual(typeof TweenEasing.Custom.func, 'function');
        const custom = TweenEasing.Custom.func((time, begin, change, duration) =>
            begin + change * time / duration);
        assert.strictEqual(custom(500, 0, 100, 1000), 50);

        ['uniform', 'accelerate', 'exponential'].forEach((kind) => {
            assert.strictEqual(typeof TweenEasing.Physical[kind], 'function',
                'Physical.' + kind + ' 缺失');
            const easing = TweenEasing.Physical[kind]();
            assert.strictEqual(typeof easing.calculate, 'function', 'Physical.' + kind + '.calculate 缺失');
            assert.strictEqual(typeof easing.getDuration, 'function', 'Physical.' + kind + '.getDuration 缺失');
        });

        // 原版 Physical.as 的 _defaultFrameRate = 30，且它的时间参数单位是**秒**、位移按帧累加：
        // calculate(1, 0, 300) = 匀速 10px/帧 × 30 帧 = 300；getDuration(300) 反解回 1 秒。
        const uniform = TweenEasing.Physical.uniform(10);
        assert.strictEqual(uniform.calculate(1, 0, 300), 300);
        assert.strictEqual(uniform.getDuration(300), 1);
    });

    check('E8 注入名表含本批新增的全局名', () => {
        const required = ['TweenEasing', 'clear', 'clearTimeout', 'load',
            'parseInt', 'parseFloat', 'Math', 'String'];
        const table = source.match(/var SCRIPT_GLOBAL_NAMES = \[([\s\S]*?)\];/);
        assert.ok(table, '找不到 SCRIPT_GLOBAL_NAMES');
        const names = table[1].split(',').map((item) => item.trim().replace(/^"|"$/g, ''))
            .filter((item) => item.length > 0);
        const missing = required.filter((name) => names.indexOf(name) < 0);
        assert.deepStrictEqual(missing, [], '注入名表缺：' + JSON.stringify(missing));
    });

    check('E9 形参表必须由 SCRIPT_GLOBAL_NAMES 生成，不得再各自硬编码', () => {
        assert.ok(/new Function\(\.\.\.SCRIPT_GLOBAL_NAMES,\s*code\)/.test(source),
            '动态编译未从 SCRIPT_GLOBAL_NAMES 展开生成形参：形参与实参两张表各自硬编码会整表错位');
        // 形参名里不得出现 ctx：脚本环境只有 M8 全局名。
        assert.ok(!/new Function\([^)]*"ctx"/.test(source), '形参表里出现了 ctx');
    });

    const failed = results.filter((item) => !item.ok);
    results.forEach((item) => {
        if (item.ok) {
            console.log('  ok   ' + item.name);
        } else {
            console.log('  FAIL ' + item.name + '\n       ' + item.message);
        }
    });
    console.log('\n' + (results.length - failed.length) + '/' + results.length
        + ' 通过（宿主：' + hostPath + '）');

    process.exitCode = failed.length === 0 ? 0 : 1;
    }

    run().catch(error => { console.error(error); process.exitCode = 1; });
