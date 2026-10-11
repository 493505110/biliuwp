'use strict';

// 真 Chromium 几何与消息桥回归；不使用 Canvas 桩的包围盒近似。
const assert = require('node:assert/strict');
const { createBrowserHost } = require('./browser-host');
const circle = `window.__hits=0;var circle=$.createShape({x:100,y:100,lifeTime:20});
    circle.graphics.beginFill(0xffffff,1);circle.graphics.drawCircle(40,40,40);circle.graphics.endFill();
    circle.addEventListener('click',function(){window.__hits++;});window.__circle=circle;`;
const tap = (page,x,y) => page.evaluate(([x,y])=>scriptDanmakuHost.handleTap(x/800,y/600),[x,y]);

async function run() {
    const browser=await createBrowserHost();
    let passed=0;
    try {
        {
            const p=await browser.page();
            try {
                await p.script(circle);
                assert.equal(await tap(p.page,105,105),false,'圆的外接矩形角落不可点击');
                assert.equal(await tap(p.page,140,140),true);
                await p.update(`window.__circle.scaleX=2;window.__circle.rotation=45;`);
                assert.equal(await tap(p.page,128.28427,184.85281),true,'旋转和非等比缩放后的圆心');
                assert.equal(await tap(p.page,200,110),false);
                assert.equal(await p.page.evaluate(()=>window.__hits),2);
                assert.equal(p.errors.length,0);
                console.log('ok Chromium 圆形路径 / 旋转 / 非等比缩放');passed++;
            } finally { await p.page.close(); }
        }
        {
            const p=await browser.page();
            try {
                await p.script(`window.__hits=0;
                    var button=$.createButton({x:300,y:200,z:100,rotationY:45,width:100,height:40,lifeTime:20,
                        onclick:function(e){window.__hits++;window.__local=[e.localX,e.localY];}});window.__button=button;`);
                const point=await p.page.evaluate(()=>{
                    const focal=300/(2*Math.tan(55*Math.PI/360));
                    const x=300+50*Math.cos(Math.PI/4), y=220, z=100-50*Math.sin(Math.PI/4);
                    return [400+(x-400)*focal/(focal+z),300+(y-300)*focal/(focal+z)];
                });
                assert.equal(await tap(p.page,...point),true);
                const local=await p.page.evaluate(()=>window.__local);
                assert.ok(Math.abs(local[0]-50)<1e-6&&Math.abs(local[1]-20)<1e-6,'投影逆变换必须恢复本地坐标');
                await p.update('window.__button.z=-1000;');
                assert.equal(await tap(p.page,...point),false,'相机背后的元件不可点击');
                assert.equal(p.errors.length,0);
                console.log('ok Chromium 3D 投影 / 本地坐标 / 近裁剪');passed++;
            } finally { await p.page.close(); }
        }
        {
            const p=await browser.page();
            try {
                await p.script(circle+`
                    var button=$.createButton({x:100,y:100,width:80,height:80,lifeTime:20,onclick:function(){window.__hits++;}});
                    button.mask=circle;window.__button=button;`);
                assert.equal(await tap(p.page,105,105),false);
                assert.equal(await tap(p.page,140,140),true);
                assert.equal(p.errors.length,0);
            } finally { await p.page.close(); }
            const q=await browser.page();
            try {
                await q.script(circle+`Player.setMask(circle);
                    $.createButton({x:0,y:0,width:800,height:600,lifeTime:20,onclick:function(){window.__hits++;}});`);
                assert.equal(await tap(q.page,105,105),false);
                assert.equal(await tap(q.page,140,140),true);
                assert.equal(q.errors.length,0);
                console.log('ok Chromium 元素遮罩 / 舞台遮罩');passed++;
            } finally { await q.page.close(); }
        }
        {
            const p=await browser.page();
            try {
                await p.script(`window.__events=[];var b=$.createButton({x:100,y:100,width:100,height:40,lifeTime:20,
                    onclick:function(){window.__events.push('click');}});
                    ['mouseOver','mouseDown','mouseUp','mouseOut'].forEach(function(t){b.addEventListener(t,function(){window.__events.push(t);});});`);
                await p.page.evaluate(()=>{
                    scriptDanmakuHost.setOrdinaryDanmakuBridge(7);
                    function input(action,down,version=7){__postHostMessage({type:'pointer',version,action,x:0.15,y:0.2,pointerId:1,down});}
                    input('down',true,6);input('move',false);input('down',true);input('up',false);
                    scriptDanmakuHost.handleTap(0.15,0.2);input('leave',false);
                });
                assert.deepEqual(await p.page.evaluate(()=>window.__events),['mouseOver','mouseDown','mouseUp','click','mouseOut']);
                assert.equal(p.errors.length,0);
                console.log('ok Chromium WebMessage 版本隔离 / 鼠标事件 / 单次点击');passed++;
            } finally { await p.page.close(); }
        }
    } finally { await browser.close(); }
    console.log(`${passed}/4 Chromium mouse input tests passed`);
}
run().catch(error=>{console.error(error);process.exitCode=1;});
