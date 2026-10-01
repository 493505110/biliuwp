'use strict';

// M8 舞台时钟与浏览器刷新、视频时间分别计量。复用可观察绘制行为的宿主夹具。
const assert = require('node:assert/strict');
const { loadHost } = require('./retained-mode.test');

async function probe(fps, driverHz, playbackRate = 1) {
    const host = await loadHost();
    host.reset(0, true, playbackRate, true);
    host.append([{ id: 'clock', stime: 0, duration: 600, lang: 'js', code:
        (fps === undefined ? '' : '$.frameRate=' + fps + ';')
        + 'window.__display=$;window.__ticks=0;window.__frames=0;window.__controlTicks=0;'
        + 'var canvas=$.createCanvas({lifeTime:600});'
        + 'canvas.addEventListener("enterFrame",function(){window.__frames++;});'
        + 'Utils.interval(function(){window.__ticks++;},42.5,0);'
        + 'Utils.interval(function(){window.__controlTicks++;},100,0);'
    }]);
    host.runFrames(1, 1000 / driverHz);
    return host;
}

const cases = [];
function test(name, run) { cases.push({ name, run }); }
function healthy(host) {
    assert.equal(host.errors().length, 0, JSON.stringify(host.errors()));
    assert.equal(host.frameErrors.length, 0);
}

test('默认舞台 30 fps，在 60/120/144 Hz 显示器上保持相同动画节奏', async () => {
    for (const driverHz of [60, 120, 144]) {
        const host = await probe(undefined, driverHz);
        assert.equal(host.sandbox.__display.frameRate, 30);
        host.runFrames(driverHz * 10, 1000 / driverHz);
        assert.ok(Math.abs(host.sandbox.__ticks - 150) <= 1, driverHz + ' Hz Timer: ' + host.sandbox.__ticks);
        assert.ok(Math.abs(host.sandbox.__frames - 300) <= 1, driverHz + ' Hz enterFrame: ' + host.sandbox.__frames);
        assert.ok(Math.abs(host.sandbox.__controlTicks - 100) <= 1, driverHz + ' Hz 100 ms Timer: ' + host.sandbox.__controlTicks);
        healthy(host);
    }
});

test('修改 $.frameRate 会同时改变已注册 Timer 和 enterFrame 的节奏', async () => {
    const host = await probe(30, 120);
    host.runFrames(1200, 1000 / 120);
    const ticks = host.sandbox.__ticks;
    const frames = host.sandbox.__frames;
    host.sandbox.__display.frameRate = 60;
    host.runFrames(1200, 1000 / 120);
    assert.ok(Math.abs(host.sandbox.__ticks - ticks - 200) <= 1);
    assert.ok(Math.abs(host.sandbox.__frames - frames - 600) <= 1);
    for (const value of [0, -1, 120, Infinity, NaN, 'bad']) {
        host.sandbox.__display.frameRate = value;
        assert.equal(host.sandbox.__display.frameRate, 60);
    }
    healthy(host);
});

test('Timer 使用墙钟；改变视频倍速只影响 Tween 与播放进度', async () => {
    for (const playbackRate of [0.5, 1, 2]) {
        const host = await probe(30, 60, playbackRate);
        host.append([{ id: 'motion', stime: 0, duration: 600, lang: 'js', code:
            'window.__moving=$.createCanvas({lifeTime:600});'
            + 'Tween.to(window.__moving,{x:100},4).play();'
        }]);
        host.runFrames(120);
        assert.ok(Math.abs(host.sandbox.__ticks - 30) <= 1, playbackRate + 'x Timer');
        const x = host.sandbox.__moving.x;
        assert.ok(Math.abs(x - Math.min(100, playbackRate * 50)) <= 2, playbackRate + 'x Tween: ' + x);
        healthy(host);
    }
});

test('暂停不推进 Timer，恢复和 seek 不补派暂停或跳转期间的回调', async () => {
    const host = await probe(30, 60);
    host.runFrames(120);
    const ticks = host.sandbox.__ticks;
    host.setState(2, false, 1);
    host.runFrames(600);
    assert.equal(host.sandbox.__ticks, ticks);
    const frames = host.sandbox.__frames;
    host.setState(2, true, 1);
    host.runFrames(60);
    assert.ok(Math.abs(host.sandbox.__ticks - ticks - 15) <= 1);
    assert.ok(Math.abs(host.sandbox.__frames - frames - 30) <= 1);
    const beforeSeek = host.sandbox.__ticks;
    host.seek(100, true, 1);
    host.runFrames(1);
    assert.equal(host.sandbox.__ticks, beforeSeek);
    host.runFrames(59);
    assert.ok(Math.abs(host.sandbox.__ticks - beforeSeek - 15) <= 1);
    healthy(host);
});

test('舞台帧率跨条目共享，视频状态同步不重置逻辑帧相位', async () => {
    const host = await probe(30, 120);
    host.append([{ id: 'set-rate', stime: 0, duration: 600, lang: 'js', code: '$.frameRate=60;' }]);
    host.runFrames(1, 1000 / 120);
    const ticks = host.sandbox.__ticks;
    for (let frame = 0; frame < 1200; frame++) {
        host.setState(host.now() / 1000 - 1, true, 1);
        host.runFrames(1, 1000 / 120);
    }
    assert.ok(Math.abs(host.sandbox.__ticks - ticks - 200) <= 1);
    host.reset(0, true, 1, true);
    host.runFrames(120);
    assert.ok(Math.abs(host.sandbox.__ticks - ticks - 200) <= 1, 'reset 后旧 Timer 必须停止');
    healthy(host);
});

test('长时间停顿不爆发补帧，帧中途注册的 Timer 不会提前到期', async () => {
    const host = await probe(30, 60);
    host.advanceClock(1000);
    host.runFrames(1);
    assert.equal(host.sandbox.__ticks, 0);
    host.runFrames(60);
    assert.ok(Math.abs(host.sandbox.__ticks - 15) <= 1);
    host.append([{ id: 'late', stime: 0, duration: 600, lang: 'js', code:
        'Utils.delay(function(){window.__registered=getTimer();'
        + 'Utils.interval(function(){window.__fired=getTimer();},42.5,1);},1);'
    }]);
    host.runFrames(1);
    host.advanceClock(10);
    host.advanceRealTime(1);
    host.runFrames(1);
    assert.equal(host.sandbox.__fired, undefined);
    host.runFrames(6);
    assert.ok(host.sandbox.__fired - host.sandbox.__registered >= 42.5);
    healthy(host);
});

(async () => {
    let failed = 0;
    for (const item of cases) {
        try { await item.run(); console.log('ok ' + item.name); }
        catch (error) { failed++; console.error('FAIL ' + item.name + '\n' + error.stack); }
    }
    console.log((cases.length - failed) + '/' + cases.length + ' frame-rate tests passed');
    if (failed) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });
