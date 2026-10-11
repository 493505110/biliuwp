'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { loadHost } = require('./retained-mode.test');
const { ensureModuleSupport } = require('./host-loader');
ensureModuleSupport();
const scripts = JSON.parse(fs.readFileSync(path.join(__dirname, 'fixtures/av2561316/scripts.json'), 'utf8'));
const item = code => ({ id: 'probe', stime: 0, duration: 30, lang: 'js', code });
const encodeRank = (name, score) => Array.from('\x02' + name + '\x1e' + score,
    c => String.fromCharCode(c.charCodeAt(0) ^ 0x2806)).join('');
const comments = [1000, 900, 800].map((score, i) => ({ txt: encodeRank('player' + i, score), pool: 0 }));
const mainProbe = `
window.__game = {
    root: $.root, red:red_ball, blue:blue_ball,
    state:function(){return game_state;}, distance:function(){return distance.now;},
    blocks:function(){return Object.keys(rect_blocks).map(function(key){return rect_blocks[key];});},
    health:function(){return health_point;},
    crash:function(){crashed('red');},
    finish:function(){game_state='game over';game_over_handle();}
};
Player.keyTrigger(function(key){if(key===27 && window.__command){window.__command();window.__command=null;}},0x7fffffff);
`;
async function create(position = 0) {
    const h = await loadHost(path.resolve(__dirname, '../../BiliBili.UWP/Assets/script-danmaku-host.html'));
    h.api.reset(position, true, 1, true, 1918);
    h.api.setOrdinaryDanmakuBridge(17);
    return h;
}
function step(h, count, dt = 20) {
    for (let i = 0; i < count; i++) { h.runFrames(1, dt); h.advanceRealTime(dt); }
}
function clean(h) { assert.deepEqual(h.errors(), []); assert.deepEqual(h.frameErrors, []); }

async function collectionTests() {
    const h = await create();
    h.append([item(`
        var blocks=[];blocks[2]='numeric';blocks.block560='named';
        Object.setPrototypeOf(blocks,{inherited:'skip'});
        window.__keys=[];foreach(blocks,function(key,value){window.__keys.push(key+'='+value);});
        window.__utilsKeys=[];Utils.foreach(blocks,function(key,value){window.__utilsKeys.push(key+'='+value);});
        var point=$.createPoint(3,4);window.__lengths=[point.length];
        point.x=0;point.y=-12;window.__lengths.push(point.length);
    `)]);
    step(h,1);
    assert.equal(JSON.stringify(h.sandbox.__keys),JSON.stringify(['2=numeric','block560=named']),
        '数组既要遍历实际数字项，也要遍历命名键；跳过空洞和继承属性');
    assert.equal(JSON.stringify(h.sandbox.__utilsKeys),JSON.stringify(h.sandbox.__keys));
    assert.equal(JSON.stringify(h.sandbox.__lengths),JSON.stringify([5,12]),'Point.length 随坐标实时变化');
    clean(h);console.log('ok foreach 数组命名键 / 稀疏项 / Utils 别名 / Point 动态长度');
}

async function startupTests() {
    const main=scripts.find(s=>s.id==='1064644479');
    const h=await create(main.stime);
    h.append([{...main,code:main.code+';window.__startup=[title,start_game_text,warning_text1];'}]);
    step(h,1);
    for(const text of h.sandbox.__startup)assert.equal(text.alpha,0,'主界面首帧不能在淡入前闪出不透明文字');
    step(h,1);
    for(const text of h.sandbox.__startup)assert.ok(text.alpha>0 && text.alpha<0.2,'第二帧从透明状态渐入');
    step(h,30);
    for(const text of h.sandbox.__startup)assert.equal(text.alpha,1,'淡入完成后文字仍正常显示');
    clean(h);console.log('ok 原始主界面首帧透明 / 单调淡入 / 正常显示');
}

async function obstacleTests() {
    const main=scripts.find(s=>s.id==='1064644479');
    const h=await create(main.stime);
    h.api.appendComments(comments);
    h.append([{...main,code:'Math.random=function(){return 0;};\n'+main.code+mainProbe}]);
    step(h,2);h.api.pushKey(83,false);step(h,2);h.api.pushKey(83,true);
    step(h,310);
    const game=h.sandbox.__game;
    const blocks=game.blocks();
    assert.ok(blocks.length>0,'原始 timer(createLevel) 必须生成障碍');
    const first=blocks[0],before=first.position.y;
    step(h,25);
    assert.ok(first.position.y<before,'命名键数组中的方块必须下落');
    for(let i=0;i<200 && first.render.y<=0;i++)step(h,1);
    assert.ok(first.render.y>0 && first.render.y<600,'方块必须进入可见画面');
    const health=game.health();
    for(let i=0;i<400 && game.health()===health;i++)step(h,1);
    assert.ok(game.health()<health,'原始碰撞检测必须自然扣血，不能直接调用 crashed 掩盖问题');
    assert.equal(h.sandbox.__game,game,'碰撞回调不得触发 AVM1 兜底重跑游戏正文');
    assert.equal(game.state(),'crash');
    step(h,125);
    assert.equal(h.sandbox.__game,game);
    assert.equal(game.state(),'playing','破碎与关卡回退动画完成后继续游戏');
    assert.ok(game.health()<3,'恢复后生命保持扣除，不能重新初始化为三颗心');
    clean(h);console.log('ok 原始关卡自然生成 / 方块下落入屏 / 碰撞扣血并恢复 / 游戏不重跑');
}

async function crashRoundTripTests() {
    const h=await create();
    h.api.appendComments(comments);
    h.append(scripts.map(s=>({...s,code:s.id==='1064644479'
        ? 'Math.random=function(){return 0;};\n'+s.code+mainProbe : s.code})));
    let cursor=0,game=null,crashedAt=-1,returned=false,resumed=false;
    for(let i=0;i<2500;i++) {
        step(h,1);
        if(!game && h.sandbox.__game) {
            game=h.sandbox.__game;h.api.pushKey(83,false);
        } else if(game) h.api.pushKey(83,true);
        if(game) assert.equal(h.sandbox.__game,game,'全量脚本与 seek 回调中，游戏对象不得被重新创建');
        while(cursor<h.messages.length) {
            const m=h.messages[cursor++];
            if(m.type!=='action'||m.action!=='seek')continue;
            if(m.seconds===366 && crashedAt<0)crashedAt=i;
            else if(crashedAt>=0 && m.seconds>400)returned=true;
            h.seek(m.seconds,true,1);
        }
        if(crashedAt>=0 && game.state()==='playing')resumed=true;
        if(crashedAt>=0 && i-crashedAt>175)break;
    }
    assert.ok(crashedAt>=0,'必须在原始全量脚本里发生自然碰撞');
    assert.ok(resumed,'碰撞动画完成后应继续游戏');
    assert.ok(returned,'368.5 秒的原始定位脚本必须跳回碰撞前的 BGM');
    assert.ok(game.health()<3,'生命不能因跳转重置');
    clean(h);console.log('ok 全量原始脚本自然碰撞 / seek 到音效 / BGM 回跳 / 保留游戏与生命');
}

async function duplicateSeekConfirmationTests() {
    const h=await create();
    h.append(scripts.map(s=>({...s,code:s.id==='1064644479'
        ? 'Math.random=function(){return 0.6;};\n'+s.code+mainProbe : s.code})));
    let cursor=0,game=null,start=-1,confirmations=0;
    const seeks=[];
    for(let i=0;i<2000;i++) {
        step(h,1);
        if(!game && h.sandbox.__game){game=h.sandbox.__game;start=i;h.api.pushKey(83,false);}
        if(start>=0 && i===start+4)h.api.pushKey(83,true);
        let guard=0;
        while(cursor<h.messages.length) {
            const m=h.messages[cursor++];
            if(m.type!=='action'||m.action!=='seek')continue;
            assert.ok(guard++<20,'确认不能触发旧代码重放和反复定位');
            seeks.push(m.seconds);
            // 与原生桥一致，来源必须显式传入；确认/缓冲回调不再被当成手动定位。
            const stalePosition=m.seconds>=382?66:1.2;
            h.api.seek(stalePosition,true,1,false,m.seekVersion-1);
            h.api.setState(stalePosition,false,1,m.seekVersion-1);
            h.api.seek(m.seconds,true,1,true,m.seekVersion);
            h.api.setState(m.seconds,false,1,m.seekVersion);
            step(h,3);
            h.api.seek(m.seconds,false,1,true,m.seekVersion);
            h.api.setState(stalePosition,false,1,m.seekVersion-1);
            h.api.seek(stalePosition,true,1,false,m.seekVersion-1);
            h.api.setState(m.seconds,true,1,m.seekVersion);
            confirmations++;
        }
        if(game)assert.equal(h.sandbox.__game,game,'重复确认不能重新创建游戏');
        if(start>=0 && i-start>240)break;
    }
    assert.ok(confirmations>=3,'包含开场、S 开始及随机 BGM 跳转');
    assert.equal(game.state(),'playing');assert.ok(game.blocks().length>0);
    assert.ok(game.root.numChildren>0,'开始游戏后不能清空显示树');
    assert.deepEqual(seeks,[65,413,1100],'不能补执行 360 秒的返回标题与 368.5 秒的音效返回脚本');
    clean(h);console.log('ok 全量脚本 S 开始 / 重复定位确认 / 缓冲暂停恢复 / 显示树与关卡保留');
}

async function tweenTests() {
    const h = await create();
    h.append([item(`
        var a={x:0,angle:0},b={x:100,angle:180};window.__objects=[a,b];window.__complete=0;
        var t=Tween.parallel(Tween.to(a,{x:10,angle:90},1),Tween.to(b,{x:200,angle:360},1));
        t.onComplete=function(){window.__complete++;window.__label=$.createComment('done');};
        window.__t=t;t.play();
    `)]);
    step(h, 26);
    const [a, b] = h.sandbox.__objects;
    assert.ok(a.x > 0 && a.x < 10); assert.ok(b.x > 100 && b.x < 200);
    assert.equal(Object.keys(a).sort().join(','), 'angle,x', '普通对象不能被注入内部字段');
    h.setState(0.52, false, 1); step(h, 50); const paused = a.x;
    step(h, 10); assert.equal(a.x, paused);
    h.setState(0.52, true, 1); step(h, 60);
    assert.equal(a.x, 10); assert.equal(b.x, 200);
    assert.equal(a.angle, 90); assert.equal(b.angle, 360);
    assert.equal(h.sandbox.__complete, 1);
    assert.ok(h.sandbox.__label.ownerItem, '完成回调创建的元件必须归属原脚本');
    h.sandbox.__t.play(); step(h, 60); assert.equal(h.sandbox.__complete, 2);
    clean(h);
    const other = await create();
    other.append([item(`var a={y:0.5};window.__a=a;
        var zero=Tween.to(a,{y:0.25},0);zero.onComplete=function(){window.__zero=a.y;};zero.play();`)]);
    step(other, 5); assert.equal(other.sandbox.__zero, 0.25, '直接 Tween 的小数坐标不能变成相对坐标');
    clean(other);
    console.log('ok 普通对象 / 多目标组合 / 暂停与重播 / 完成回调归属 / 零时长');
}

async function bridgeTests() {
    const h = await create();
    h.append([item(`window.__seen=[];window.__root=$.root;
        $.root.addEventListener('added',function(e){if(!e.target.data)return;
            window.__seen.push(e.target.data.text);if(e.target.data.text==='hide')e.target.visible=false;});`)]);
    assert.equal(h.api.filterOrdinaryDanmaku({txt:'hide'}), false);
    assert.equal(h.api.filterOrdinaryDanmaku({txt:'show'}), true);
    assert.equal(h.sandbox.__seen.join(','), 'hide,show');
    const count = h.sandbox.__root.numChildren;
    for (let i = 0; i < 500; i++) h.api.filterOrdinaryDanmaku({txt:'show'});
    assert.equal(h.sandbox.__root.numChildren, count, '临时代理不能积累在显示树');
    h.seek(40, true, 1); step(h, 1);
    assert.equal(h.api.filterOrdinaryDanmaku({txt:'hide'}), true, '离开条目后移除 root 监听');
    h.seek(0, true, 1); step(h, 1);
    assert.equal(h.api.filterOrdinaryDanmaku({txt:'hide'}), false, '重新进入可登记监听');
    h.reset(0, true, 1, true); assert.equal(h.api.filterOrdinaryDanmaku({txt:'hide'}), true);
    clean(h);
    const bad = await create();
    bad.append([item(`$.root.addEventListener('added',function(){throw new TypeError('bad listener');});`)]);
    step(bad, 1); assert.equal(bad.api.filterOrdinaryDanmaku({txt:'show'}), true);
    assert.equal(bad.errors().length, 1);
    assert.equal(bad.api.filterOrdinaryDanmaku({txt:'show'}), true);
    assert.equal(bad.errors().length, 1, '坏监听只上报一次');
    console.log('ok added 冒泡 / 可见性筛选 / 代理回收 / seek 与 reset / 坏监听隔离');
}

async function inputMessageTests() {
    const h=await create();
    h.append([item(`window.__keys=[];
        Player.keyTrigger(function(k){window.__keys.push('down'+k);},10000);
        Player.keyTrigger(function(k){window.__keys.push('up'+k);},10000,true);`)]);
    step(h,1);
    const send=(keyCode,up,version=17)=>h.postHostMessage({type:'key',version,keyCode,up});
    send(65,false);send(65,true);send(68,false);send(68,true);
    assert.equal(h.sandbox.__keys.join(','),'down65,up65,down68,up68','消息必须按顺序立即派发，无需下一帧');
    const before=h.sandbox.__keys.length;
    send(65,false,16);h.postHostMessage({type:'other',version:17,keyCode:65});
    h.api.visible(false);send(65,false);h.api.visible(true);
    h.api.setStopped(0,1);send(65,false);h.api.setState(0,false,1);
    assert.equal(h.sandbox.__keys.length,before,'旧版本、无关消息、隐藏和停止时的输入必须丢弃');
    h.api.visible(false);send(65,true);h.api.visible(true);
    h.api.setStopped(0,1);send(68,true);h.api.setState(0,false,1);
    assert.equal(h.sandbox.__keys.slice(-2).join(','),'up65,up68','隐藏和停止期间也要放行松键，避免恢复后继续转动');
    send(65,true);
    assert.equal(h.sandbox.__keys.at(-1),'up65','暂停时仍应立即收到松键');
    h.api.reset(0,true,1,true,1918);
    h.append([item(`window.__newKeys=[];Player.keyTrigger(function(k){window.__newKeys.push(k);},10000);`)]);
    step(h,1);send(65,false);
    h.api.setOrdinaryDanmakuBridge(18);send(65,false);send(68,false,18);
    assert.equal(h.sandbox.__newKeys.join(','),'68','reset 及新内容版本不能接收旧视频的在途输入');
    clean(h);console.log('ok 按键消息即时有序派发 / 暂停松键 / 旧版本与隐藏停止隔离');

    const main=scripts.find(s=>s.id==='1064644479');
    const gameHost=await create(main.stime);
    gameHost.append([{...main,code:main.code+mainProbe}]);step(gameHost,2);
    const key=(keyCode,up)=>gameHost.postHostMessage({type:'key',version:17,keyCode,up});
    key(83,false);step(gameHost,4);key(83,true);step(gameHost,60);
    const game=gameHost.sandbox.__game,beforeAngle=game.red.angle;
    key(65,false);step(gameHost,8);
    assert.notEqual(game.red.angle,beforeAngle);
    for(let i=0;i<500;i++)gameHost.api.filterOrdinaryDanmaku({txt:'密集普通弹幕'+i,pool:0});
    key(65,true);const releasedAngle=game.red.angle;step(gameHost,4);
    assert.equal(game.red.angle,releasedAngle,'普通弹幕筛选后收到松键，下一帧不能继续转动');
    clean(gameHost);console.log('ok 原始游戏经消息按键转动 / 500 条普通弹幕后松键即停');
}

async function sameSidePairTests() {
    const main=scripts.find(s=>s.id==='1064644479');
    for(const random of [0.6,0.8]) {
        const h=await create(main.stime);
        h.append([{...main,code:`Math.random=function(){return ${random};};\n`+main.code+mainProbe}]);
        const key=(keyCode,up)=>h.postHostMessage({type:'key',version:17,keyCode,up});
        step(h,2);key(83,false);step(h,4);key(83,true);step(h,60);key(random===0.6?65:68,false);
        let pair=[];
        for(let i=0;i<1000;i++) {
            step(h,1);pair=h.sandbox.__game.blocks().slice(0,2);
            if(pair.length===2 && pair[1].position.y<-246)break;
        }
        assert.equal(pair.length,2);assert.equal(pair[0].position.x,pair[1].position.x);
        assert.ok(pair.every(b=>b.width===150 && b.height===30));
        assert.ok(pair[1].position.y<-246,'两个同侧宽方块都必须通过球的活动范围');
        assert.equal(h.sandbox.__game.health(),3,'按原脚本旋转速度应能无损通过同侧连续方块');
        assert.equal(h.sandbox.__game.state(),'playing');clean(h);
    }
    console.log('ok 原始左右同侧连续宽方块 / 消息输入 / 无损通过');
}

async function originalTests() {
    assert.equal(scripts.length, 16);
    const opening = await create(1.2);
    opening.append([scripts.find(s => s.id === '1061263489')]); step(opening, 650, 50);
    assert.ok(opening.messages.some(m => m.type === 'action' && m.action === 'seek' && m.seconds === 65));
    clean(opening);
    const main = scripts.find(s => s.id === '1064644479');
    const h = await create(main.stime);
    h.api.appendComments(comments);
    h.append([{...main, code: main.code + mainProbe}]); step(h, 2);
    // 按键与回退动画单独在首批障碍到达前验证；自然碰撞由 obstacleTests 覆盖。
    h.api.pushKey(83, false); step(h, 4); h.api.pushKey(83, true); step(h, 80);
    const game = h.sandbox.__game;
    assert.equal(game.state(), 'playing');
    const angle = game.red.angle;
    h.api.pushKey(65, false); step(h, 8); h.api.pushKey(65, true);
    assert.notEqual(game.red.angle, angle);
    h.api.pushKey(68, false); step(h, 8); h.api.pushKey(68, true);
    h.api.pushKey(87, false); step(h, 2); h.api.pushKey(87, true);
    const distance = game.distance();
    // 原脚本随机选择 4 秒或 6 秒的开局等待，关卡生成后才累计距离。
    for(let i=0;i<350 && game.distance()===distance;i++)step(h,1);
    assert.ok(game.distance() > distance);
    h.sandbox.__command = () => game.crash(); h.api.pushKey(27, false); step(h, 125);
    assert.equal(game.state(), 'playing', '碰撞回退动画完成后恢复游戏');
    const rank = comments[0];
    assert.equal(h.api.filterOrdinaryDanmaku(rank), false);
    assert.equal(h.api.filterOrdinaryDanmaku({...rank,pool:2}), true);
    assert.equal(h.api.filterOrdinaryDanmaku({txt:'普通聊天',pool:0}), true);
    assert.equal(h.messages.filter(m=>m.type==='ordinaryDanmaku').at(-1).suppressed, false);
    h.sandbox.__command = () => game.finish(); h.api.pushKey(27, false); step(h, 3);
    assert.equal(game.state(), 'game over');
    clean(h);
    // 原始 BGM 脚本都要能执行，回跳脚本使用真实的共享状态参数。
    for (const script of scripts.filter(s => s.code.length < 1000)) {
        const bgm = await create(script.stime);
        bgm.append([{id:'setup',stime:script.stime,duration:1,code:'$G._set("bgm_time",448000);'},script]);
        step(bgm, 1); clean(bgm);
    }
    console.log('ok 原始 16 条脚本 / 开场链 / S 开始 / A D W / 距离 / 碰撞恢复 / 排行榜筛选与结算');
}

async function seekTests() {
    const h = await create();
    h.append([
        {id:'game',stime:0,duration:0,code:`window.__persistent={count:0};interval(function(){window.__persistent.count++;},20,0);
            Player.keyTrigger(function(){Player.seek(1628000);},10000);`},
        {id:'skipped',stime:5,duration:0,code:'window.__old=true;'}
    ]);
    step(h, 2); const game = h.sandbox.__persistent;
    h.api.pushKey(65,false); h.seek(1628,true,1); step(h,2);
    assert.equal(h.sandbox.__old, undefined);
    assert.equal(h.sandbox.__persistent,game); assert.ok(game.count > 0);
    h.seek(0,true,1);step(h,2);h.seek(6,true,1);step(h,2);
    assert.equal(h.sandbox.__old,true,'手动定位仍按现有窗口规则激活脚本');
    clean(h);
    console.log('ok 脚本切换 BGM 保留状态 / 长视频窗口 / 不补执行跨过的脚本');
}

async function sceneTests() {
    const h = await create();
    h.append([
        {id:'old',stime:0,duration:20,code:`window.__old=$.createComment('old',{lifeTime:0});
            interval(function(){window.__timer=true;},200,0);timer(function(){window.__delay=true;},200);
            Player.keyTrigger(function(){window.__key=true;},10000);`},
        {id:'new',stime:0.1,duration:20,code:`ScriptManager.clearEl();ScriptManager.clearTimer();ScriptManager.clearTrigger();
            window.__new=$.createComment('new',{lifeTime:0});`}
    ]);
    step(h,30);h.api.pushKey(65,false);
    assert.equal(h.sandbox.__old.parent,null);assert.ok(h.sandbox.__new.parent);
    assert.equal(h.sandbox.__timer,undefined);assert.equal(h.sandbox.__delay,undefined);assert.equal(h.sandbox.__key,undefined);
    clean(h);console.log('ok ScriptManager 跨条目清理上一场景的元素 / Timer / 触发器');
}

async function browserTests() {
    const {createBrowserHost} = require('./browser-host');
    const browser = await createBrowserHost();
    try {
        const {page:startupPage,errors:startupErrors}=await browser.page();
        const startup=await startupPage.evaluate(main=>{
            scriptDanmakuHost.reset(main.stime,true,1,true,1918);
            scriptDanmakuHost.append([{...main,code:main.code+';window.__startup=[title,start_game_text,warning_text1];'}]);
            __step(20);
            const firstAlpha=__startup.map(t=>t.alpha);
            const data=document.querySelector('#stage canvas').getContext('2d').getImageData(0,420,800,140).data;
            const firstWhitePixels=Array.from(data).filter((v,i)=>i%4===3 && v>0
                && data[i-3]>240 && data[i-2]>240 && data[i-1]>240).length;
            __step(20);const nextAlpha=__startup.map(t=>t.alpha);
            for(let i=0;i<30;i++)__step(20);
            return {firstAlpha,nextAlpha,finalAlpha:__startup.map(t=>t.alpha),firstWhitePixels,
                errors:__messages.filter(m=>m.type==='error')};
        },scripts.find(s=>s.id==='1064644479'));
        assert.deepEqual(startupErrors,[]);assert.deepEqual(startup.errors,[]);
        assert.deepEqual(startup.firstAlpha,[0,0,0]);assert.equal(startup.firstWhitePixels,0);
        assert.ok(startup.nextAlpha.every(a=>a>0 && a<0.2));assert.deepEqual(startup.finalAlpha,[1,1,1]);
        await startupPage.close();
        console.log('ok Edge 主界面首帧无文字闪现，随后正常淡入',JSON.stringify(startup));
        for(const [width,height] of [[800,600],[1920,1008]])for(const random of [0.6,0.8]) {
            const {page:pairPage,errors:pairErrors}=await browser.page(width,height);
            const pairResult=await pairPage.evaluate(({scripts,mainProbe,random})=>{
                let realClock=0,timerId=0,cursor=0,start=-1;const timers=new Map();
                window.setTimeout=(fn,delay)=>{timers.set(++timerId,{fn,due:realClock+delay});return timerId;};
                window.clearTimeout=id=>timers.delete(id);
                scriptDanmakuHost.reset(0,true,1,true,1918);
                scriptDanmakuHost.setOrdinaryDanmakuBridge(17);
                scriptDanmakuHost.append(scripts.map(s=>({...s,code:s.id==='1064644479'
                    ? `Math.random=function(){return ${random};};\n`+s.code+mainProbe : s.code})));
                const key=(keyCode,up)=>__postHostMessage({type:'key',version:17,keyCode,up});
                let pair=[],passed=false;
                for(let i=0;i<2000;i++) {
                    // 每帧筛选多条普通弹幕，同时走宿主消息入口控制游戏。
                    for(let j=0;j<16;j++)scriptDanmakuHost.filterOrdinaryDanmaku({txt:'普通聊天'+j,pool:0});
                    __step(20);realClock+=20;
                    for(const [id,timer] of timers)if(timer.due<=realClock){timers.delete(id);timer.fn();}
                    while(cursor<__messages.length){const m=__messages[cursor++];
                        if(m.type==='action'&&m.action==='seek')scriptDanmakuHost.seek(m.seconds,true,1);}
                    if(window.__game && start<0){start=i;key(83,false);}
                    if(start>=0 && i===start+4)key(83,true);
                    if(start>=0 && i===start+64)key(random===0.6?65:68,false);
                    if(window.__game) {
                        pair=__game.blocks().slice(0,2);
                        if(pair.length===2 && pair[1].position.y<-246){passed=true;break;}
                        if(__game.health()<3)break;
                    }
                }
                key(random===0.6?65:68,true);
                const releasedAngle=__game.red.angle;for(let i=0;i<4;i++)__step(20);
                return {passed,health:__game.health(),state:__game.state(),releasedAngle,angle:__game.red.angle,
                    pair:pair.map(b=>({x:b.position.x,y:b.position.y,width:b.width,height:b.height})),
                    errors:__messages.filter(m=>m.type==='error')};
            },{scripts,mainProbe,random});
            assert.deepEqual(pairErrors,[]);assert.deepEqual(pairResult.errors,[]);
            assert.ok(pairResult.passed,JSON.stringify(pairResult));assert.equal(pairResult.health,3);
            assert.equal(pairResult.state,'playing');assert.equal(pairResult.angle,pairResult.releasedAngle);
            assert.ok(pairResult.pair.every(b=>b.width===150 && b.height===30));
            await pairPage.close();
            console.log(`ok Edge ${width}x${height} ${random===0.6?'左':'右'}侧双宽方块 / 密集普通弹幕 / 消息松键即停`,JSON.stringify(pairResult));
        }
        const {page,errors} = await browser.page();
        await page.evaluate(({scripts,comments,mainProbe})=>{
            document.body.style.background='black';
            let realClock=0,timerId=0;const timers=new Map();
            window.setTimeout=(fn,delay)=>{timers.set(++timerId,{fn,due:realClock+delay});return timerId;};
            window.clearTimeout=id=>timers.delete(id);
            scriptDanmakuHost.reset(0,true,1,true,1918);
            scriptDanmakuHost.setOrdinaryDanmakuBridge(17);
            scriptDanmakuHost.appendComments(comments);
            scriptDanmakuHost.append(scripts.map(s=>({...s,code:s.code+(s.id==='1064644479'?mainProbe:'')})));
            let cursor=0,started=false,firstBlock=null,firstY=0;
            window.__advanceGame=function() {
                __step(20);
                realClock+=20;
                for(const [id,timer] of timers)if(timer.due<=realClock){timers.delete(id);timer.fn();}
                while(cursor<__messages.length){const m=__messages[cursor++];if(m.type==='action'&&m.action==='seek') {
                    // 模拟在途旧命令和重复确认；真实桥接为每条进度命令携带定位序号。
                    const oldPosition=m.seconds>=382?66:1.2;
                    scriptDanmakuHost.setState(oldPosition,false,1,m.seekVersion-1);
                    scriptDanmakuHost.seek(oldPosition,true,1,false,m.seekVersion-1);
                    scriptDanmakuHost.seek(m.seconds,true,1,true,m.seekVersion);
                    scriptDanmakuHost.seek(m.seconds,true,1,true,m.seekVersion);
                    scriptDanmakuHost.setState(oldPosition,false,1,m.seekVersion-1);
                }}
            };
            for(let i=0;i<1800;i++) {
                __advanceGame();
                if(window.__game&&!started){scriptDanmakuHost.pushKey(83,false);started=true;}
                if(started&&i%10===0)scriptDanmakuHost.pushKey(83,true);
                if(window.__game) {
                    const blocks=__game.blocks();
                    if(!firstBlock && blocks.length){firstBlock=blocks[0];firstY=firstBlock.position.y;}
                    if(firstBlock && firstBlock.position.y<firstY) {
                        const x=Math.round(firstBlock.render.x),y=Math.round(firstBlock.render.y);
                        if(x>0 && x<800 && y>50 && y<400) {
                            const rgba=Array.from(document.querySelector('#stage canvas').getContext('2d').getImageData(x,y,1,1).data);
                            if(rgba[0]>240 && rgba[1]>240 && rgba[2]>240 && rgba[3]>0) {
                                window.__obstacle={count:blocks.length,from:firstY,to:firstBlock.position.y,x,y,rgba};
                                break;
                            }
                        }
                    }
                }
            }
        },{scripts,comments,mainProbe});
        const result = await page.evaluate(()=>({state:__game.state(),distance:__game.distance(),obstacle:window.__obstacle,errors:__messages.filter(m=>m.type==='error'),
            oldElements:__game.root.children.filter(e=>e.ownerItem&&e.ownerItem.model.id==='1061263489').length,
            pixels:Array.from(document.querySelector('#stage canvas').getContext('2d').getImageData(0,0,800,600).data).filter((v,i)=>i%4===3&&v>0).length}));
        assert.deepEqual(errors,[]);assert.deepEqual(result.errors,[]);
        assert.equal(result.state,'playing');assert.ok(result.distance>0);assert.ok(result.pixels>1000);
        assert.ok(result.obstacle && result.obstacle.count>0,'必须在真实画布看到自然下落的白色障碍方块');
        assert.equal(result.oldElements,0,'游戏场景必须清掉开场圆球和文字');
        if(process.env.M8_GAME_SCREENSHOT_DIR) await page.screenshot({path:path.join(process.env.M8_GAME_SCREENSHOT_DIR,'av2561316-game.png')});
        console.log('ok Edge 原始全量脚本 + 实际 BGM 定位回调 + 游戏像素',JSON.stringify(result));
        const recovery=await page.evaluate(()=>{
            const game=__game,start=__messages.length,health=game.health();
            let crashFrame=-1,returned=false;
            for(let i=0;i<1800;i++) {
                __advanceGame();
                if(crashFrame<0 && game.health()<health)crashFrame=i;
                if(crashFrame>=0 && i-crashFrame>175)break;
            }
            const seeks=__messages.slice(start).filter(m=>m.type==='action'&&m.action==='seek');
            const crashSeek=seeks.findIndex(m=>m.seconds===366);
            returned=crashSeek>=0 && seeks.slice(crashSeek+1).some(m=>m.seconds>400);
            return {collided:crashFrame>=0,sameGame:__game===game,state:__game.state(),health:__game.health(),returned,
                errors:__messages.filter(m=>m.type==='error')};
        });
        assert.deepEqual(errors,[]);assert.deepEqual(recovery.errors,[]);
        assert.ok(recovery.collided);assert.ok(recovery.sameGame,'碰撞后不能重跑成标题界面');
        assert.equal(recovery.state,'playing');assert.ok(recovery.health<3);assert.ok(recovery.returned);
        console.log('ok Edge 自然碰撞后继续游戏并返回 BGM',JSON.stringify(recovery));
    } finally {await browser.close();}
}
(async()=>{
    await collectionTests();await startupTests();await obstacleTests();await crashRoundTripTests();await duplicateSeekConfirmationTests();await tweenTests();await bridgeTests();await inputMessageTests();await sameSidePairTests();await originalTests();await seekTests();await sceneTests();
    if(process.argv.includes('--browser')) await browserTests();
})().catch(error=>{console.error(error);process.exitCode=1;});
