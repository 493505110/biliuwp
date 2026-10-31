'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { loadHost } = require('./retained-mode.test');
const { evaluateModule } = require('./host-loader');
const cases = [];
const test = (name, run) => cases.push({ name, run });
const item = code => ({ id: 'input', stime: 0, duration: 30, lang: 'js', code });
function healthy(h) { assert.deepEqual(h.errors(), []); assert.deepEqual(h.frameErrors, []); }
function key(h, keyCode, positionMs, up = false, version = 17) {
    h.postHostMessage({ type: 'key', version, keyCode, up, positionMs });
}
async function create(code) {
    const h = await loadHost();
    h.reset(1, true, 1, true); h.api.setOrdinaryDanmakuBridge(17);
    h.append([item(code)]); h.runFrames(1);
    return h;
}

for (const part of ['easy', 'normal']) {
    test(part + ' 完整谱面：每拍 Perfect 时音符到线，首次回调不重跑游戏', async () => {
        const scripts=JSON.parse(fs.readFileSync(path.join(__dirname,'fixtures/av2785213',part,'scripts.json'),'utf8'));
        const main=scripts[3],h=await loadHost();
        h.reset(4,true,1,true);h.api.setOrdinaryDanmakuBridge(17);
        const startedAt=h.now();
        h.append([{...main,code:'window.__starts=(window.__starts||0)+1;'+main.code+
            ';window.__game={chart:notelist,boxes:NoteBoxList,read:function(){return {perfect:n4,miss:n0};}};'}]);
        h.runFrames(1);
        const game=h.sandbox.__game,indices=[0,0,0,0];
        const count=game.chart[0].indexOf(-1);
        assert.ok(count>100,'完整谱面必须包含百余拍');
        let largestDistance=0;
        for(let i=0;i<count;i++) {
            const target=game.chart[0][i]+5000,lane=game.chart[1][i]-1;
            while(h.now()-startedAt<target-4000){h.runFrames(1);h.advanceRealTime(1000/60);}
            assert.equal(h.sandbox.__starts,1,'延迟回调不得重跑整局');
            assert.equal(h.sandbox.__game,game,'谱面和分数对象必须保留');
            const box=game.boxes[lane][indices[lane]++];
            assert.ok(box && !box.expired,`第 ${i+1} 拍音符必须仍在屏上`);
            assert.ok(box.y<=330 && box.y+10>=328,
                `${part} 第 ${i+1} 拍 ${target}ms：音符 y=${box.y} 未到判定线`);
            largestDistance=Math.max(largestDistance,Math.abs(box.y-328.5));
            key(h,[65,83,68,36][lane],target);
            assert.equal(game.read().perfect,i+1,'每拍必须判为 Perfect');
            assert.equal(game.read().miss,0);
        }
        healthy(h);
        console.log(`${part} ${count} 拍到线，最大顶边偏差 ${largestDistance.toFixed(2)} px（60 Hz 模拟时钟）`);
    });

    test(part + ' 原始四轨：0/150/350/650 ms 消息延后仍按按下时刻判 Perfect', async () => {
        const scripts = JSON.parse(fs.readFileSync(path.join(__dirname, 'fixtures/av2785213', part, 'scripts.json'), 'utf8'));
        assert.equal(scripts.length, 4);
        const main = scripts[3];
        const probe = `;window.__game={notes:notelist,read:function(){return {score:score,perfect:n4};}};`;
        for (let lane = 1; lane <= 4; lane++) {
            for (const delay of [0, 150, 350, 650]) {
                const h = await loadHost();
                h.reset(4, true, 1, true); h.api.setOrdinaryDanmakuBridge(17);
                const startedAt = h.now();
                h.append([{ ...main, code: main.code + probe }]); h.runFrames(1);
                const notes = h.sandbox.__game.notes;
                const index = notes[1].indexOf(lane);
                assert.ok(index >= 0);
                const targetMs = notes[0][index] + 5000;
                // 让原始 timer 创建音符，再单独延后消息，不修改原作品的判定窗口。
                while (h.now() - startedAt < targetMs - 4000) {
                    h.runFrames(1); h.advanceRealTime(1000 / 60);
                }
                h.setState(targetMs / 1000, true, 1);
                h.advanceClock(delay);
                const draws = h.mainCanvas().__counts.drawImage || 0;
                key(h, [65, 83, 68, 36][lane - 1], targetMs);
                const result = h.sandbox.__game.read();
                assert.equal(result.score, 1000, `${part} lane ${lane}, delay ${delay}`);
                assert.equal(result.perfect, 1);
                assert.ok(h.mainCanvas().__counts.drawImage > draws, '原游戏反馈必须在下次 RAF 前提交到主画布');
                h.runFrames(1); healthy(h);
            }
        }
    });
}

test('缺失缓动变量预备只识别裸配置值，保留注释、字面量、已有全局和局部变量', async () => {
    const {namespace}=await evaluateModule(path.resolve(__dirname,'../../BiliBili.UWP/Assets/script-danmaku/legacy.js'),vm.createContext({}));
    const prepare=namespace.prepareLegacyEasingGlobals;
    const globals=Object.create({Inherited:42});
    let reads=0;
    Object.defineProperty(globals,'Existing',{get(){reads++;return 1;}});
    const source=`var a={easing:Bounce},b={easing:Custom},c={easing:Existing},d={easing:Inherited},e={easing:Player};`;
    prepare(source,globals,{Player:true});
    assert.deepEqual(Object.keys(globals),['Bounce','Custom']);
    assert.equal(globals.Bounce,undefined);assert.equal(reads,0,'不得求值已有 getter');
    for(const code of ['"{easing:Fake}"',"'{easing:Fake}'",'`{easing:Fake}`',
        '// {easing:Fake}', '/* {easing:Fake} */', '/{easing:Fake}/',
        'obj.easing:Fake', '{easing:Fake.easeOut}', '{easing:true}', '{easing:null}',
        '{easing:function(){return Fake;}}']) {
        const target={};prepare(code,target,{});assert.deepEqual(target,{},code);
    }
    const h=await create(`
        window.__starts=(window.__starts||0)+1;
        var Local=function(){};
        timer(function(){var config={easing:Local};window.__same=config.easing===Local;},1);
    `);
    h.advanceRealTime(1);
    assert.equal(h.sandbox.__same,true);assert.equal(h.sandbox.__starts,1);healthy(h);
});

test('缺少快照的旧消息仍暴露原始延迟，且非法快照回退实时播放头', async () => {
    const h = await create(`window.__times=[];Player.keyTrigger(function(){window.__times.push(Player.time);},30000);`);
    h.setState(1, true, 1); h.advanceClock(150);
    for (const position of [undefined, null, -1, '1000', NaN, Infinity]) key(h, 65, position);
    assert.deepEqual(Array.from(h.sandbox.__times), Array(6).fill(1150));
    h.api.pushKey(65, false);
    assert.equal(h.sandbox.__times.at(-1), 1150);
    healthy(h);
});

test('按键回调时钟隔离：松键、嵌套输入、异常与普通弹幕不泄漏快照', async () => {
    const h = await create(`
        window.__events=[]; window.__read=function(){return Player.time;};
        Player.keyTrigger(function(k){
            if(k===68){window.__events.push(Player.time);throw new TypeError('expected');}
            window.__events.push(Player.time);
            if(k===65){window.scriptDanmakuHost.pushKey(83,false,900);window.__events.push(Player.time);}
        },30000);
        Player.keyTrigger(function(){window.__events.push(Player.time);},30000,true);
        Player.commentTrigger(function(){window.__events.push(Player.time);},30000);
    `);
    h.setState(1.5, true, 1);
    key(h, 65, 1000); key(h, 65, 1100, true); key(h, 68, 1200);
    h.api.pushComment({txt:'test'});
    assert.deepEqual(Array.from(h.sandbox.__events), [1000, 900, 1000, 1100, 1200, 1500]);
    assert.equal(h.sandbox.__read(), 1500);
    assert.equal(h.errors('runtime').length, 1);
    assert.deepEqual(h.frameErrors, []);
});

test('高亮按收到消息时创建，不因判定快照较早而立即过期；过期版本输入不执行', async () => {
    const h = await create(`
        window.__count=0;window.__read=function(){return Player.time;};
        Player.keyTrigger(function(){window.__count++;
            window.__light=$.createShape({lifeTime:0.25});
            window.__light.graphics.beginFill(0xffffff);window.__light.graphics.drawRect(0,0,20,20);
        },30000);
    `);
    h.setState(1, true, 1); h.advanceClock(350);
    key(h, 65, 1000, false, 16); assert.equal(h.sandbox.__count, 0);
    key(h, 65, 1000); assert.equal(h.sandbox.__read(), 1350);
    h.runFrames(1); assert.equal(h.sandbox.__light.expired, false);
    h.runFrames(16); assert.equal(h.sandbox.__light.expired, true);
    h.api.visible(false); key(h, 65, 1000); assert.equal(h.sandbox.__count, 1);
    h.reset(0, true, 1, true); key(h, 65, 1000); assert.equal(h.sandbox.__count, 1);
    healthy(h);
});

test('输入立即绘制，但不额外推进 Timer、enterFrame 或已有 Tween', async () => {
    const h = await create(`
        window.__ticks=0;window.__frames=0;
        var moving=$.createShape({lifeTime:10,motion:{x:{fromValue:0,toValue:100}}});
        window.__moving=moving;
        moving.addEventListener('enterFrame',function(){window.__frames++;});
        interval(function(){window.__ticks++;},1,0);
        Player.keyTrigger(function(){
            var light=$.createShape({lifeTime:0.25,x:200,y:100});
            light.graphics.beginFill(0xffffff);light.graphics.drawRect(0,0,20,20);
        },30000);
    `);
    const before=[h.sandbox.__ticks,h.sandbox.__frames,h.sandbox.__moving.x];
    const pending=h.pendingFrames();
    const draws=h.mainCanvas().__counts.drawImage || 0;
    h.advanceClock(20); key(h,65,1000);
    assert.ok(h.mainCanvas().__counts.drawImage > draws);
    assert.deepEqual([h.sandbox.__ticks,h.sandbox.__frames,h.sandbox.__moving.x],before);
    assert.equal(h.pendingFrames(),pending,'不能建立第二条 RAF 链');
    healthy(h);
});

(async () => {
    let failed = 0;
    for (const {name, run} of cases) {
        try { await run(); console.log('ok ' + name); }
        catch (error) { failed++; console.error('FAIL ' + name + '\n' + error.stack); }
    }
    console.log(`${cases.length - failed}/${cases.length} av2785213 input tests passed`);
    if (failed) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });
