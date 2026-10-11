'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { loadHost } = require('./retained-mode.test');
const scripts = JSON.parse(fs.readFileSync(path.join(__dirname, 'fixtures/av2612400/scripts.json'), 'utf8'));
const item = code => ({ id: 'probe', stime: 0, duration: 30, lang: 'js', code });
const observed = scripts.map(s => s.id !== '1042796465' ? s : { ...s, code: s.code + `
    ;window.__game={shape:shape,start:startarray,meteors:meteorarray,keys:keyPressed,
        colors:colors,small:smallmeteors,state:function(){return {mode:GameState,time:gametime,score:score,over:gameover,banned:bancmt};}};
` });
function step(h, count) {
    for (let i = 0; i < count; i++) { h.runFrames(1, 20); h.advanceRealTime(20); }
}
function clean(h) { assert.deepEqual(h.errors(), []); assert.deepEqual(h.frameErrors, []); }

async function timeoutTests() {
    const h = await loadHost(); h.api.reset(0, true, 1, true);
    h.append([item(`
        window.__events=[];window.__ticks=0;
        var t=timer(function(){window.__events.push('timer');},200);
        var repeat=interval(function(){window.__ticks++;},20,0);
        var kd=Player.keyTrigger(function(){window.__events.push('down');},100);
        var ku=Player.keyTrigger(function(){window.__events.push('up');},100,true);
        var c=Player.commentTrigger(function(){window.__events.push('comment');},100);
        Player.keyTrigger(function(){window.__events.push('expired');},100);
        window.__ids=[t,repeat,kd,ku,c];
        clearTimeout(kd);clearTimeout(ku);clearTimeout(c);
        clearTimeout(t);
    `)]);
    step(h, 20);
    assert.equal(new Set(h.sandbox.__ids).size, 5, '计时器与监听移除计时不得共用句柄');
    assert.ok(h.sandbox.__ticks > 0, '取消监听过期计时不得误停 interval');
    h.api.pushKey(65, false); h.api.pushKey(65, true); h.api.pushComment({ txt: 'hello' });
    assert.deepEqual(Array.from(h.sandbox.__events), ['down', 'up', 'comment']);
    h.api.setState(0.4, false, 1); h.advanceClock(5000);
    h.api.pushKey(65, false);
    assert.equal(h.sandbox.__events.at(-1), 'down', '暂停期间取消过期的监听仍有效');
    h.append([{ ...item('ScriptManager.clearTrigger();'), id: 'clear' }]);
    step(h, 1);
    h.api.pushKey(65, false); h.api.pushComment({ txt: 'after clear' });
    assert.equal(h.sandbox.__events.length, 4, 'clearTrigger 仍回收常驻监听');
    clean(h);

    const expired = await loadHost(); expired.api.reset(0, true, 1, true);
    expired.append([item(`window.__calls=0;var key=Player.keyTrigger(function(){window.__calls++;},20);
        interval(function(){clearTimeout(key);},100,1);`)]);
    step(expired, 10); expired.api.pushKey(65, false);
    assert.equal(expired.sandbox.__calls, 0, '过期后取消不能复活监听');
    clean(expired);

    const lifecycle = await loadHost(); lifecycle.api.reset(0, true, 1, true);
    lifecycle.append([item(`window.__calls=0;clearTimeout(Player.keyTrigger(function(){window.__calls++;}));`)]);
    step(lifecycle, 100); lifecycle.api.pushKey(65, false);
    assert.equal(lifecycle.sandbox.__calls, 1);
    lifecycle.seek(31, true, 1); lifecycle.api.pushKey(65, false);
    assert.equal(lifecycle.sandbox.__calls, 1, '监听不能活过条目窗口');
    lifecycle.api.reset(0, true, 1, true); lifecycle.api.pushKey(65, false);
    assert.equal(lifecycle.sandbox.__calls, 1, 'reset 后不残留监听');
    clean(lifecycle);
    console.log('ok 取消触发器移除计时 / 唯一句柄 / 默认到期 / 暂停 / clearTrigger / 越窗与重置');
}

async function gameplayTests() {
    assert.equal(scripts.length, 4);
    const h = await loadHost(); h.api.reset(0, true, 1, true, 700); h.append(observed); step(h, 100);
    const game = h.sandbox.__game, button = game.start.at(-1);
    assert.ok(h.api.handleTap((button.x + 60) / 800, (button.y + 30) / 600));
    assert.equal(h.messages.filter(m => m.type === 'action' && m.action === 'seek').at(-1).seconds, 26);
    h.seek(26, true, 1, true); step(h, 400);
    assert.equal(h.sandbox.__game, game, '游戏内 seek 保留正在运行的场景');
    assert.equal(game.state().mode, 1); assert.ok(game.meteors.length > 0);
    for (const [key, direction] of [[68, 1], [65, -1]]) {
        const x = game.shape.x; h.api.pushKey(key, false); step(h, 20); h.api.pushKey(key, true);
        assert.ok((game.shape.x - x) * direction > 0, '超过1秒后 A/D 仍能移动人物');
        assert.equal(game.keys[key === 65 ? 'A' : 'D'], false, '松键仍能送达');
    }
    const meteor = game.meteors[0], y = meteor.y;
    step(h, 5); assert.ok(meteor.y > y, '流星自然下落');
    clean(h);
    console.log('ok 原始四条脚本 / 开始按钮 / 26秒跳转 / 持续A和D / 松键 / 自然下落');
}

async function particleLifetimeTests() {
    const h = await loadHost(); h.api.reset(26, true, 1, true, 700);
    h.append([{ ...item(`timer(function(){
        window.__particle=$.createShape({lifeTime:1,motion:{x:{fromValue:0,toValue:100,lifeTime:1}}});
        __particle.graphics.lineStyle(1,0xffffff);__particle.graphics.lineTo(1,1);
    },200);`), duration: 700 }]);
    step(h, 10);
    const particle = h.sandbox.__particle;
    assert.ok(particle && !particle.expired, '26秒后的新粒子不能按条目0秒起算而立即过期');
    assert.ok(particle.x < 5, '回调新建的声明式动画从初始值开始');
    step(h, 10);
    assert.ok(particle.x > 10 && particle.x < 30, '声明式动画按创建后的时间插值');
    h.api.setState(26.4, false, 1); const x = particle.x; step(h, 100);
    assert.equal(particle.x, x); assert.equal(particle.expired, false, '暂停不消耗粒子寿命');
    h.seek(50, true, 1, true); step(h, 5);
    assert.equal(h.sandbox.__particle, particle);
    assert.ok(!particle.expired && particle.x < 40, '游戏内seek切换BGM不耗尽粒子寿命');
    step(h, 50); assert.equal(particle.expired, true, '粒子按声明的1秒寿命回收');
    clean(h);
    console.log('ok 中途回调新建粒子 / 创建时刻起算 / 声明式动画 / 暂停 / 脚本seek / 到期回收');
}

async function homeKeyTests() {
    const h = await loadHost(); h.api.reset(0, true, 1, true, 700);
    h.api.setOrdinaryDanmakuBridge(7); h.append(observed); step(h, 100);
    const comment = mode => ({ txt: 'Home测试', fontSize: 25, color: 0xffffff, mode });
    h.api.appendComments([comment(1), comment(4), comment(5)]);
    assert.equal(h.api.filterOrdinaryDanmaku(comment(1)), true);
    for (const banned of [true, false, true]) {
        h.runFrames(1, 34);
        h.postHostMessage({ type: 'key', version: 7, keyCode: 36, up: false });
        assert.equal(h.sandbox.__game.state().banned, !banned, 'Home按下不触发松键监听');
        h.postHostMessage({ type: 'key', version: 7, keyCode: 36, up: true });
        assert.equal(h.sandbox.__game.state().banned, banned);
        for (const mode of [1, 6]) assert.equal(h.api.filterOrdinaryDanmaku(comment(mode)), !banned,
            '原始脚本按ScrollComment类型屏蔽滚动弹幕');
        for (const mode of [4, 5]) assert.equal(h.api.filterOrdinaryDanmaku(comment(mode)), true,
            '顶底弹幕不应被Home误屏蔽');
    }
    assert.equal(h.sandbox.__game.shape.expired, false);
    assert.equal(h.messages.filter(m => m.type === 'ordinaryDanmaku').at(-1).suppressed, false,
        '按条目筛选不能变成全部普通弹幕层接管');
    clean(h);
    console.log('ok 原始Home松键 / 反复关闭与恢复 / 正反向滚动筛选 / 顶底及游戏图元保留');
}

async function browserTests() {
    const { createBrowserHost } = require('./browser-host');
    const browser = await createBrowserHost(process.env.M8_BASE_REVISION);
    let failed = 0;
    async function check(name, fn) {
        try { await fn(); console.log('ok Edge ' + name); }
        catch (error) { failed++; console.error('FAIL Edge ' + name + ': ' + error.message); }
    }
    try {
        await check('原始Home关闭与恢复滚动弹幕，顶底保留', async () => {
            const p = await browser.page();
            const result = await p.page.evaluate(scripts => {
                const h = scriptDanmakuHost;
                h.reset(0, true, 1, true, 700); h.setOrdinaryDanmakuBridge(7); h.append(scripts);
                for (let i = 0; i < 100; i++) __step(20);
                const results = [];
                for (let toggle = 0; toggle < 2; toggle++) {
                    __step(34);
                    __postHostMessage({ type: 'key', version: 7, keyCode: 36, up: true });
                    results.push({ banned: __game.state().banned, allowed: [1, 6, 4, 5].map(mode =>
                        h.filterOrdinaryDanmaku({ txt: 'Home测试', fontSize: 25, color: 0xffffff, mode })) });
                }
                return { results, errors: __messages.filter(m => m.type === 'error') };
            }, observed);
            assert.deepEqual(result.results, [
                { banned: true, allowed: [false, false, true, true] },
                { banned: false, allowed: [true, true, true, true] }
            ]);
            assert.deepEqual(result.errors, []); assert.deepEqual(p.errors, []);
        });
        await check('笔触切换 / 透明段 / 默认原点 / 曲线与动态缓存 / clear重置', async () => {
            const p = await browser.page();
            await p.script(`
                var s=$.createShape({lifeTime:20,x:100,y:100});window.__shape=s;
                var g=s.graphics;g.moveTo(0,0);g.lineStyle(4,0xff0000);g.lineTo(20,0);
                g.lineStyle(4,0x00ff00);g.lineTo(40,0);g.lineStyle(4,0,0);g.lineTo(60,0);
                var origin=$.createShape({lifeTime:20,x:200,y:100});
                origin.graphics.lineStyle(4,0x0000ff);origin.graphics.lineTo(20,0);
            `);
            assert.deepEqual(await p.pixel(110, 100), [255, 0, 0, 255]);
            assert.deepEqual(await p.pixel(130, 100), [0, 255, 0, 255]);
            assert.equal((await p.pixel(150, 100))[3], 0);
            assert.deepEqual(await p.pixel(210, 100), [0, 0, 255, 255]);
            await p.update(`var g=__shape.graphics;g.lineStyle(4,0x0000ff);g.curveTo(80,100,100,0);`);
            assert.ok((await p.pixel(180, 150))[2] > 200, '曲线控制点参与缓存包围盒');
            await p.update(`var g=__shape.graphics;g.clear();g.lineStyle(4,0xffffff);g.lineTo(20,0);`);
            assert.deepEqual(await p.pixel(110, 100), [255, 255, 255, 255]);
            assert.equal((await p.pixel(130, 100))[3], 0);
            assert.deepEqual(p.errors, []);
        });
        await check('填充轮廓不随描边样式拆开', async () => {
            const p = await browser.page();
            await p.script(`var s=$.createShape({lifeTime:20,x:100,y:100}),g=s.graphics;
                g.beginFill(0xffffff);g.moveTo(0,0);g.lineStyle(4,0xff0000);g.lineTo(40,0);
                g.lineStyle(4,0x00ff00);g.lineTo(40,40);g.lineStyle();g.lineTo(0,40);g.lineTo(0,0);g.endFill();`);
            assert.deepEqual(await p.pixel(120, 120), [255, 255, 255, 255]);
            assert.deepEqual(await p.pixel(120, 100), [255, 0, 0, 255]);
            assert.deepEqual(await p.pixel(140, 120), [0, 255, 0, 255]);
            assert.deepEqual(p.errors, []);
        });
        for (const [width, height] of [[800, 600], [1280, 720]]) {
            await check(`原始人物和背景流星像素 / 持续操作 ${width}x${height}`, async () => {
                const p = await browser.page(width, height);
                const result = await p.page.evaluate(({ scripts, width, height }) => {
                    const h = scriptDanmakuHost;
                    h.reset(0, true, 1, true, 700); h.append(scripts);
                    for (let i = 0; i < 100; i++) __step(20);
                    const game = __game, b = game.start.at(-1);
                    h.handleTap((b.x + 60) / width, (b.y + 30) / height);
                    h.seek(26, true, 1, true);
                    let backgroundPixels = 0;
                    for (let i = 0; i < 400; i++) {
                        __step(20);
                        if (i % 10) continue;
                        const c = document.querySelector('#stage canvas');
                        for (const sm of game.small) {
                            if (sm.x < 2 || sm.x > width - 3 || sm.y < 2 || sm.y > height - 3) continue;
                            const d = c.getContext('2d').getImageData(Math.floor(sm.x - 2), Math.floor(sm.y - 2), 5, 5).data;
                            for (let j = 3; j < d.length; j += 4) if (d[j]) backgroundPixels++;
                        }
                    }
                    const c = document.querySelector('#stage canvas');
                    const d = c.getContext('2d').getImageData(Math.floor(game.shape.x), height - 25, 25, 25).data;
                    let spritePixels = 0;
                    for (let i = 3; i < d.length; i += 4) if (d[i]) spritePixels++;
                    const x = game.shape.x; h.pushKey(68, false);
                    for (let i = 0; i < 20; i++) __step(20);
                    h.pushKey(68, true);
                    return { spritePixels, backgroundPixels, moved: game.shape.x > x, released: game.keys.D === false,
                        same: game === __game, errors: __messages.filter(m => m.type === 'error') };
                }, { scripts: observed, width, height });
                console.log(JSON.stringify({ width, height, ...result }));
                if (process.env.M8_ARTIFACT_DIR) {
                    fs.mkdirSync(process.env.M8_ARTIFACT_DIR, { recursive: true });
                    // 独立宿主没有底层视频；截图用黑底呈现白色文字与流星。
                    await p.page.evaluate(() => { document.body.style.background = '#000'; });
                    await p.page.screenshot({ path: path.join(process.env.M8_ARTIFACT_DIR, `av2612400-${width}x${height}.png`) });
                }
                assert.deepEqual(result.errors, []); assert.deepEqual(p.errors, []);
                assert.ok(result.spritePixels > 100, '原始25x25人物必须实际可见');
                assert.ok(result.backgroundPixels > 0, '原始背景流星必须实际可见');
                assert.ok(result.moved && result.released && result.same, '超过1秒后人物持续响应按下和松开');
            });
        }
    } finally { await browser.close(); }
    assert.equal(failed, 0, `${failed} 个浏览器回归失败`);
}

(async () => {
    if (!process.argv.includes('--browser-only')) {
        await timeoutTests(); await particleLifetimeTests(); await gameplayTests(); await homeKeyTests();
    }
    if (process.argv.includes('--browser') || process.argv.includes('--browser-only')) await browserTests();
})().catch(error => { console.error(error); process.exitCode = 1; });
