'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { loadHost } = require('./retained-mode.test');
const scripts = JSON.parse(fs.readFileSync(path.join(__dirname, 'fixtures/av2676643/scripts.json'), 'utf8'));
const mainProbe = `;window.__game={root:$.root,player:player,bullets:danmus,
    state:function(){return {alive:alive,invincible:indestructible,deaths:deadTime,count:numDanmu,step:$G._("step")};}};`;
const observedScripts = scripts.map(s => ({ ...s, code: s.code + (s.id === '1122634223' ? mainProbe
    : s.id === '1122641917' ? ';window.__introButton=btn;' : '') }));
const item = code => ({ id: 'probe', stime: 0, duration: 375, lang: 'js', code });
const comment = { txt: '弹幕障碍', fontSize: 25, color: 0xff6677, mode: 1 };
function step(h, count, dt = 34) {
    for (let i = 0; i < count; i++) { h.runFrames(1, dt); h.advanceRealTime(dt); }
}
function clean(h) { assert.deepEqual(h.errors(), []); assert.deepEqual(h.frameErrors, []); }
async function create(position = 0, list = observedScripts) {
    const h = await loadHost(path.resolve(__dirname, '../../BiliBili.UWP/Assets/script-danmaku-host.html'));
    h.api.reset(position, true, 1, true, 375);
    h.api.setOrdinaryDanmakuBridge(17);
    h.append(list); step(h, 1);
    return h;
}
function click(h, button) {
    return h.api.handleTap((button.x + button.width / 2) / 800, (button.y + button.height / 2) / 600);
}

async function legacyTests() {
    const h = await create(0, [item(`
        window.__result=[];
        for(var i=0;i<3;i++) {
            var callback=function(){window.__result.push('callback');break;window.__result.push('unreachable');};
            callback();
            if(i===1)break;
        }
        var j=0;while(j<2)if(++j===1)break;
        switch(1){case 1:window.__result.push('switch');break;}
        window.__result.push(j,'break;',/break;/.source);
        // break;
        /* break; */
        window.__result.push('multiline
string');
    `)]);
    assert.equal(JSON.stringify(h.sandbox.__result), JSON.stringify(['callback', 'callback', 'switch', 1, 'break;', 'break;', 'multiline\nstring']));
    clean(h);
    const broken = await create(0, [item('function broken(){break;} var x = ;')]);
    assert.equal(broken.errors('compile').length, 1, '其它语法错误仍必须报告');
    console.log('ok 非法回调 break / 合法循环和 switch / 字符串注释正则 / 多行字符串 / 错误隔离');
}

async function cloneTests() {
    const h = await create(0, [item(`
        window.__root=$.root;window.__copies=[];window.__calls=0;
        $.root.addEventListener('added',function(e){
            if(!e.target.selectable)return;
            window.__calls++;
            var copy=clone(e.target);copy.selectable=false;
            copy.setTextFormat(e.target.getTextFormat());
            copy.x=123;copy.y=45;
            $.root.removeChild(e.target);$.root.addChild(copy);
            window.__copies.push(copy);
        });
    `)]);
    assert.equal(h.api.filterOrdinaryDanmaku(comment), false);
    const copy = h.sandbox.__copies[0];
    assert.equal(copy.text, comment.txt); assert.equal(copy.fontsize, comment.fontSize);
    assert.equal(copy.color, comment.color); assert.equal(copy.selectable, false);
    assert.equal(copy.parent, h.sandbox.__root); assert.equal(copy.x, 123);
    assert.equal(h.sandbox.__calls, 1, '克隆不能在脚本修改 selectable 前自动入树而递归');
    assert.equal(copy.ownerItem.model.id, 'probe');
    assert.equal(h.api.filterOrdinaryDanmaku({ ...comment, txt: '另一条', color: 0x33aaff }), false);
    assert.equal(copy.text, comment.txt); assert.equal(copy.color, comment.color);
    h.seek(376, true, 1); assert.equal(copy.parent, null, '副本随条目回收');
    clean(h);
    console.log('ok 普通弹幕 TextField / 独立克隆 / 格式保留 / 无递归 / 条目回收');
}

async function gameplayTests() {
    assert.equal(scripts.length, 58);
    const h = await create();
    assert.equal(click(h, h.sandbox.__introButton), true);
    assert.equal(h.messages.filter(m => m.type === 'action' && m.action === 'seek').at(-1).seconds, 19);
    h.seek(19, true, 1, true); step(h, 270);
    const game = h.sandbox.__game;
    assert.ok(game); assert.equal(game.state().alive, true); assert.equal(game.state().invincible, false);
    for (const [key, axis, direction] of [[68, 'x', 1], [65, 'x', -1], [87, 'y', -1], [83, 'y', 1]]) {
        const before = game.player.shape[axis];
        h.api.pushKey(key, false); step(h, 2); h.api.pushKey(key, true);
        assert.ok((game.player.shape[axis] - before) * direction > 0, '原始 WASD 必须移动自机');
        const released = game.player.shape[axis]; step(h, 2);
        assert.equal(game.player.shape[axis], released, '松键后停止移动');
    }
    assert.equal(h.api.filterOrdinaryDanmaku(comment), false);
    assert.equal(h.sandbox.__game, game, '首条弹幕的未声明变量不能重置整局游戏');
    assert.equal(game.state().count, 1); assert.equal(game.bullets.length, 1);
    const bullet = game.bullets[0], x = bullet.shape.x;
    step(h, 5); assert.ok(bullet.shape.x < x, '障碍必须移动');
    assert.equal(game.root.children.filter(e => e.text === comment.txt).length, 1, '重试不得残留失败副本');
    bullet.shape.y = game.player.shape.y - bullet.shape.height / 2;
    for (let i = 0; i < 350 && game.state().deaths === 0; i++) step(h, 1);
    assert.equal(game.state().deaths, 1, '自然移动的弹幕碰撞必须触发死亡');
    step(h, 180); assert.equal(game.state().alive, true, '死亡动画后重生');
    assert.equal(h.sandbox.__game, game);
    clean(h);
    console.log('ok 原始开始按钮 / WASD 按下松开 / 首条障碍克隆移动 / 碰撞死亡与重生');
}

async function timelineTests() {
    const h = await create(24);
    const game = h.sandbox.__game;
    let seconds = 24.034, index = 0, fallingChecks = 0, milestone = 60;
    while (seconds < 362) {
        // 使用原生入屏接口持续注入普通弹幕，让全部关卡处理实际障碍。
        if (index % 75 === 0 && seconds < 359) h.api.filterOrdinaryDanmaku({ ...comment, txt: '弹幕' });
        for (const target of [138.9, 153.895]) {
            if (seconds < target && seconds + .034 >= target) {
                h.api.filterOrdinaryDanmaku({ ...comment, txt: '回归' });
                const retained = game.bullets.at(-1);
                step(h, 2); seconds += .068; index += 2;
                if (retained && game.bullets.includes(retained)) {
                    assert.equal(retained.type, 17, '原始非法 break 脚本必须改写现有障碍');
                    const y = retained.shape.y; step(h, 2); seconds += .068; index += 2;
                    assert.ok(retained.shape.y > y, '回调必须正常加速下落'); fallingChecks++;
                }
            }
        }
        step(h, 1); seconds += .034; index++;
        if (seconds >= milestone) { console.log('  原始时间轴已验证至 ' + milestone + ' 秒'); milestone += 60; }
    }
    assert.equal(fallingChecks, 2, '两段原始非法 break 回调均须实际运行');
    assert.equal(h.sandbox.__game, game, '全时间轴不能因未声明变量反复重启游戏');
    const button = game.root.children.find(e => e.isButton);
    assert.ok(button, '361 秒应出现结算后的继续按钮');
    assert.equal(click(h, button), true);
    assert.equal(h.messages.filter(m => m.type === 'action' && m.action === 'seek').at(-1).seconds, 0);
    clean(h);
    console.log('ok 全部 58 条原始脚本 / 两段非法 break 回调 / 362 秒游戏时间轴 / 结算继续按钮');
}

async function browserTests() {
    const { createBrowserHost } = require('./browser-host');
    const browser = await createBrowserHost();
    try {
        const { page, errors } = await browser.page();
        const result = await page.evaluate(({ scripts, comment }) => {
            const h = scriptDanmakuHost;
            h.reset(0, true, 1, true, 375); h.setOrdinaryDanmakuBridge(17); h.append(scripts); __step(34);
            const button = __introButton;
            const clicked = h.handleTap((button.x + button.width / 2) / 800, (button.y + button.height / 2) / 600);
            h.seek(19, true, 1, true);
            for (let i = 0; i < 190; i++) __step(34);
            const game = __game, start = game.player.shape.x;
            h.pushKey(68, false); for (let i = 0; i < 5; i++) __step(34); h.pushKey(68, true);
            const moved = game.player.shape.x > start;
            const allowed = h.filterOrdinaryDanmaku(comment);
            const bullet = game.bullets[0], before = bullet.shape.x;
            for (let i = 0; i < 20; i++) __step(34);
            const canvas = document.querySelector('#stage canvas');
            const data = canvas.getContext('2d').getImageData(0, 0, 800, 600).data;
            const sprite = canvas.getContext('2d').getImageData(Math.floor(game.player.shape.x - 30),
                Math.floor(game.player.shape.y - 30), 60, 60).data;
            let spritePixels = 0;
            for (let i = 0; i < sprite.length; i += 4) if (sprite[i] + sprite[i + 1] + sprite[i + 2] > 80 && sprite[i + 3] > 0) spritePixels++;
            let obstaclePixels = 0;
            for (let i = 0; i < data.length; i += 4) if (data[i] > 200 && data[i + 1] > 50 && data[i + 1] < 160
                && data[i + 2] > 70 && data[i + 2] < 180 && data[i + 3] > 0) obstaclePixels++;
            return { clicked, moved, allowed, same: game === __game, count: game.state().count,
                moving: bullet.shape.x < before, obstaclePixels, spritePixels, errors: __messages.filter(m => m.type === 'error') };
        }, { scripts: observedScripts, comment });
        assert.deepEqual(errors, []); assert.deepEqual(result.errors, []);
        assert.ok(result.clicked && result.moved && result.same && result.moving);
        assert.equal(result.allowed, false); assert.equal(result.count, 1);
        assert.ok(result.obstaclePixels > 30, '真实 Canvas 必须画出彩色文字障碍');
        assert.ok(result.spritePixels > 100, '原始位图自机必须实际可见');
        console.log('ok Edge 原始开始按钮 / 位图自机 WASD / 普通弹幕障碍像素', JSON.stringify(result));
    } finally { await browser.close(); }
}
(async () => {
    if (!process.argv.includes('--browser-only')) {
        await legacyTests(); await cloneTests(); await gameplayTests(); await timelineTests();
    }
    if (process.argv.includes('--browser') || process.argv.includes('--browser-only')) await browserTests();
})().catch(error => { console.error(error); process.exitCode = 1; });
