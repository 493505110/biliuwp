'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { loadHost } = require('./retained-mode.test');
const scripts = JSON.parse(fs.readFileSync(path.join(__dirname, 'fixtures/av734037/scripts.json'), 'utf8'));
const probe = `;window.__art={scene:function(){return lastview;},root:$.root,
    texts:function(){return txtlist;},shapes:function(){return conlist;}};`;
const observed = scripts.map(s => ({ ...s, code: s.code + probe }));
function step(h, count, dt = 50) {
    for (let i = 0; i < count; i++) { h.runFrames(1, dt); h.advanceRealTime(dt); }
}
function clean(h) { assert.deepEqual(h.errors(), []); assert.deepEqual(h.frameErrors, []); }
function near(actual, expected) { assert.ok(Math.abs(actual - expected) < 1e-7, `${actual} != ${expected}`); }
async function create(position = 0, list = observed) {
    const h = await loadHost(path.resolve(__dirname, '../../BiliBili.UWP/Assets/script-danmaku-host.html'));
    h.api.reset(position, true, 1, true, 94); h.append(list);
    return h;
}
const item = code => [{ id: 'bezier', stime: 0, duration: 94, lang: 'js', code }];

async function curveTests() {
    const h = await create(0, item(`
        var a={x:0,y:10},b=$.createShape({x:0,lifeTime:0}),c={x:0},d={x:10},e={x:0},f={x:0};
        window.__objects=[a,b,c,d,e,f];
        var ta=Tween.bezier(a,{x:100,y:110},{x:0,y:10},{x:100},1);
        var tb=Tween.bezier(b,{x:0},{x:0},{x:[100,-100]},1);
        var tc=Tween.bezierTo(c,{x:100},{x:100},1,TweenEasing.Quad.easeIn);
        var td=Tween.bezierFrom(d,{x:0},{x:20});
        var te=Tween.bezier(e,{x:100},{x:0},{x:[80,200,-40]},1);
        var tf=Tween.bezier(f,null,null,{x:100},1);
        window.__curves=[ta,tb,tc,td,te,tf];
        window.__reverse=Tween.reverse(tb);
        window.__slice=Tween.slice(tb,0.125,0.375);
        window.__repeat=Tween.repeat(tb,2);
        window.__delay=Tween.delay(Tween.scale(tb,2),0.5);
        ta.play();tb.play();tc.play();td.play();te.play();tf.play();
    `));
    step(h, 1); clean(h);
    const objects = h.sandbox.__objects, curves = h.sandbox.__curves;
    const at = (tween, time) => { tween.gotoAndStop(time); step(h, 1); };
    at(curves[0], .25); near(objects[0].x, 43.75); near(objects[0].y, 35);
    at(curves[1], .25); near(objects[1].x, 50);
    at(curves[1], .5); near(objects[1].x, 0);
    at(curves[1], .75); near(objects[1].x, -50);
    at(curves[2], .5); near(objects[2].x, 43.75);
    at(curves[3], .5); near(objects[3].x, 12.5);
    near(curves[3].durationMs, 1000);
    at(curves[4], 1 / 3); near(objects[4].x, 140);
    at(curves[4], 2 / 3); near(objects[4].x, 80);
    at(curves[4], 1); near(objects[4].x, 100);
    at(curves[5], .5); near(objects[5].x, 50);
    at(h.sandbox.__reverse, .25); near(objects[1].x, -50);
    at(h.sandbox.__slice, .125); near(objects[1].x, 50);
    at(h.sandbox.__slice, 0); near(objects[1].x, 37.5);
    at(h.sandbox.__slice, .25); near(objects[1].x, 37.5);
    at(h.sandbox.__repeat, 1.25); near(objects[1].x, 50);
    at(h.sandbox.__delay, 1); near(objects[1].x, 50);
    curves[0].play(); step(h, 5); h.api.setState(.5, false, 1);
    const paused = objects[0].x; step(h, 10); near(objects[0].x, paused);
    h.api.setState(.5, true, 1); step(h, 30); near(objects[0].x, 100);
    curves[0].play(); near(objects[0].x, 0); step(h, 30); near(objects[0].x, 100);
    assert.equal(Object.keys(objects[0]).join(','), 'x,y'); clean(h);
    console.log('ok 二次/多控制点贝塞尔 / 缓动 / 默认端点 / reverse/slice/repeat/delay/scale / 暂停重播');
}

async function displayTests() {
    const h = await create(0, item(`
        var text=$.createComment('初始',{lifeTime:0,color:0x123456,fontsize:30});
        window.__text=text;window.__root=$.root;
        window.__append=function(){text.appendText('歌词');};
        var shape=$.createShape({lifeTime:0});window.__shape=shape;
        shape.graphics.beginFill(0xff0000);shape.graphics.drawRect(0,0,20,20);shape.endFill();
        shape.graphics.lineStyle(2,0xffffff);
        shape.graphics.drawPath([1,2,3],[30,30,60,60,80]);
    `));
    step(h, 2);
    const text = h.sandbox.__text, before = text.width;
    h.sandbox.__append(); step(h, 2);
    assert.equal(text.text, '初始歌词'); assert.equal(text.length, 4);
    assert.ok(text.width > before); assert.equal(text.color, 0x123456); assert.equal(text.fontsize, 30);
    assert.ok(text.cacheCanvas.__counts.fillText > 0);
    assert.equal(h.sandbox.__shape.graphics.__fill, null);
    assert.equal(Object.keys(text).length, 0); clean(h);
    h.api.reset(0, true, 1, true, 94); step(h, 2);
    assert.ok(!h.sandbox.__root.children.includes(text));
    assert.equal(text.cacheCanvas, null); assert.equal(h.mainCanvas().__marks.length, 0);
    console.log('ok appendText 内容与缓存 / 旧式 endFill / drawPath 不完整尾段 / reset 回收');
}

async function timelineTests() {
    assert.equal(scripts.length, 1); assert.equal(scripts[0].id, '295719347');
    const h = await create(); const seen = new Set(); let lyrics = false, drawn = false;
    for (let i = 0; i < 1900; i++) {
        step(h, 1);
        const art = h.sandbox.__art;
        if (art) {
            seen.add(art.scene());
            lyrics ||= art.texts().some(text => text.text.length > 1);
            drawn ||= art.shapes().some(shape => shape.painted);
        }
    }
    assert.deepEqual([...seen].sort(), [0,1,2,3,4,5,6,7,8]);
    assert.ok(lyrics, '歌词必须由原始 appendText 回调逐字写入');
    assert.ok(drawn, '原始图形必须实际落笔'); clean(h);
    for (const [position, scene] of [[25,2],[32,3],[44,4],[60,5],[74,6],[80,7],[92,8],[1,1]]) {
        h.seek(position, true, 1); step(h, 60);
        assert.equal(h.sandbox.__art.scene(), scene, '跳转后重建当前场景：' + position); clean(h);
    }
    h.container.offsetWidth = 1280; h.container.offsetHeight = 720;
    h.resize(); step(h, 10); clean(h);
    h.api.reset(0, true, 1, true, 94); step(h, 2);
    assert.equal(h.mainCanvas().__marks.length, 0);
    console.log('ok 原始 94 秒时间轴 / 全部九个场景 / 歌词与图形 / 前后跳转 / 缩放 / 清理');
}

async function browserTests() {
    const { createBrowserHost } = require('./browser-host');
    const browser = await createBrowserHost();
    try {
        const { page, errors } = await browser.page(1280, 720);
        const result = await page.evaluate(scripts => {
            // timer()/Utils.delay 使用真实时间，interval 使用舞台时间。
            // 与 VM 夹具一样推进两种时钟，不能只推进 rAF 而漏掉全部延迟歌词。
            let realClock=0, sequence=0; const timers=new Map();
            window.setTimeout=(callback,delay)=>{const id=++sequence;timers.set(id,{callback,due:realClock+Math.max(0,Number(delay)||0)});return id;};
            window.clearTimeout=id=>timers.delete(id);
            window.__frame=()=>{
                __step(50); realClock+=50;
                for(let guard=0;guard<100;guard++) {
                    const due=[...timers].filter(([,timer])=>timer.due<=realClock);
                    if(!due.length)break;
                    for(const [id,timer] of due){timers.delete(id);timer.callback();}
                }
            };
            const h = scriptDanmakuHost;
            h.reset(0, true, 1, true, 94); h.append(scripts);
            window.__seen = new Set(); window.__pixels = []; window.__lyrics = false;
            window.__sample = () => {
                const canvas = document.querySelector('#stage canvas');
                const data = canvas.getContext('2d').getImageData(0,0,canvas.width,canvas.height).data;
                let colored = 0;
                for (let i = 0; i < data.length; i += 4)
                    if (data[i+3] && Math.max(data[i],data[i+1],data[i+2]) - Math.min(data[i],data[i+1],data[i+2]) > 20) colored++;
                return colored;
            };
            for (let i = 0; i < 1900; i++) {
                __frame();
                __seen.add(__art.scene()); __lyrics ||= __art.texts().some(text => text.text.length > 1);
                if ([39,399,599,699,999,1299,1499,1699].includes(i)) __pixels.push({seconds:(i+1)*.05,colored:__sample(),texts:__art.texts().map(t=>t.text.slice(0,40))});
            }
            return {scenes:[...__seen].sort(),pixels:__pixels,lyrics:__lyrics,errors:__messages.filter(m=>m.type==='error')};
        }, observed);
        assert.deepEqual(result.errors, []); assert.deepEqual(errors, []);
        assert.deepEqual(result.scenes, [0,1,2,3,4,5,6,7,8]); assert.ok(result.lyrics);
        assert.ok(result.pixels.some(p=>p.colored>100), '真实 Canvas 必须绘出蓝白图形');
        // 同一宿主中反复向前/向后定位，避免只测初始化路径。
        const jumps = await page.evaluate(() => {
            const results=[];
            for (const [seconds,scene] of [[32,3],[44,4],[74,6],[80,7],[1,1]]) {
                scriptDanmakuHost.seek(seconds,true,1);
                for(let i=0;i<60;i++) __frame();
                results.push({seconds,expected:scene,actual:__art.scene()});
            }
            return results;
        });
        for (const jump of jumps) assert.equal(jump.actual,jump.expected);
        await page.setViewportSize({width:960,height:540});
        await page.evaluate(()=>{scriptDanmakuHost.resize();for(let i=0;i<20;i++)__frame();});
        assert.deepEqual(await page.evaluate(()=>__messages.filter(m=>m.type==='error')),[]);
        assert.deepEqual(errors,[]);
        console.log('ok Edge 原始完整时间轴 / 真实像素 / 跳转 / 缩放',JSON.stringify(result.pixels));
    } finally { await browser.close(); }
}

(async()=>{
    await curveTests(); await displayTests(); await timelineTests();
    if(process.argv.includes('--browser')) await browserTests();
})().catch(error=>{console.error(error);process.exitCode=1;});
