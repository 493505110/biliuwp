'use strict';

// 使用仓库已有的原始 Akari 库验证 TrackMatte；断言真实像素，不以无报错代替遮罩正确。
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createBrowserHost } = require('./browser-host');
const akari = fs.readFileSync(path.join(__dirname, 'fixtures/real/entry_08_1147140491.js'), 'utf8');
const scene = akari + `
var A=Global._get('__akari'), D=A.Display;
var source=D.Solid({width:200,height:100,color:0xff0000});
var mask=D.Solid({width:100,height:100,color:0xffffff});
var layer=D.Effects.TrackMatte({
    layer:D.Layer({source:source,inPoint:0,outPoint:60000}),
    mask:D.Layer({source:mask,inPoint:0,outPoint:60000})
});
A.execute(D.MainComposition({width:800,height:600,duration:60000,layers:[layer]}));
window.__source=source;window.__matte=mask;window.__blendLayer=mask.parent;
`;

async function healthy(p) {
    assert.deepEqual(p.errors, []);
    assert.deepEqual(await p.page.evaluate(() => __messages.filter(m => m.type === 'error')), []);
}

async function alphaAndUpdates(p) {
    await p.script(scene);
    assert.deepEqual(await p.pixel(50,50), [255,0,0,255], '遮罩只使用 alpha，不显示白色');
    assert.deepEqual(await p.pixel(150,50), [0,0,0,0], '遮罩范围外透明');
    await p.update('__matte.alpha=0.5;');
    assert.deepEqual(await p.pixel(50,50), [255,0,0,128]);
    await p.update('__source.alpha=0.5;');
    assert.deepEqual(await p.pixel(50,50), [255,0,0,64], '内容透明度与遮罩透明度相乘');
    await p.update('__source.alpha=1;');
    await p.update('__matte.alpha=0;');
    assert.deepEqual(await p.pixel(50,50), [0,0,0,0], '全透明遮罩应清空整层');
    await p.update('__matte.alpha=1;__matte.x=100;');
    assert.deepEqual(await p.pixel(50,50), [0,0,0,0], '移动遮罩后清掉旧范围');
    assert.deepEqual(await p.pixel(150,50), [255,0,0,255]);
    await p.update('__matte.visible=false;');
    assert.deepEqual(await p.pixel(50,50), [255,0,0,255], '隐藏遮罩后恢复内容');
    await p.update('__matte.visible=true;__matte.graphics.clear();');
    assert.deepEqual(await p.pixel(150,50), [0,0,0,0], '空遮罩不能留下上一帧内容');
    await p.update('__matte.x=0;__matte.graphics.beginFill(0x000000);__matte.graphics.drawPath([1,2,2,2,2],[0,0,100,0,100,100,0,100,0,0]);__matte.graphics.endFill();');
    assert.deepEqual(await p.pixel(50,50), [255,0,0,255], '遮罩颜色不影响内容颜色');
    await p.update('__blendLayer.x=80;__blendLayer.y=40;__blendLayer.scaleX=1.5;');
    assert.deepEqual(await p.pixel(155,90), [255,0,0,255]);
    assert.deepEqual(await p.pixel(305,90), [0,0,0,0], '父级变换后仍按本地遮罩裁剪');
    await p.update('__blendLayer.x=__blendLayer.y=0;__blendLayer.scaleX=1;__matte.graphics.clear();'
        + '__matte.graphics.beginFill(0xffffff);__matte.graphics.drawPath([1,2,2,2,2,1,2,2,2,2],'
        + '[0,0,100,0,100,100,0,100,0,0,25,25,75,25,75,75,25,75,25,25]);__matte.graphics.endFill();');
    assert.deepEqual(await p.pixel(10,10), [255,0,0,255]);
    assert.deepEqual(await p.pixel(50,50), [0,0,0,0], '遮罩内部空洞按每像素 alpha 裁剪');
    await healthy(p);
}

async function nestedLayers(p) {
    await p.script(scene + `
var outer=D.Sprite();outer.blendMode="layer";
var second=D.Solid({width:50,height:100,color:0xffffff});second.x=50;second.blendMode="alpha";
outer.addChild(__blendLayer);outer.addChild(second);A.root.addChild(outer);
window.__outer=outer;window.__second=second;
`);
    assert.deepEqual(await p.pixel(25,50), [0,0,0,0]);
    assert.deepEqual(await p.pixel(75,50), [255,0,0,255]);
    assert.deepEqual(await p.pixel(125,50), [0,0,0,0]);
    await p.update('__second.alpha=0;');
    assert.deepEqual(await p.pixel(75,50), [0,0,0,0]);
    await p.update('__second.alpha=0.5;__outer.alpha=0.5;');
    assert.deepEqual(await p.pixel(75,50), [255,0,0,64], '嵌套 layer 的隔离表面和透明度独立');
    await healthy(p);
}

async function isolationAndModeChanges(p) {
    await p.script(scene + `
var background=D.Solid({width:300,height:180,color:0x0000ff});
A.root.addChildAt(background,0);
var later=D.Solid({width:20,height:100,color:0x00ff00});later.x=160;
__blendLayer.addChild(later);
`);
    assert.deepEqual(await p.pixel(50,50), [255,0,0,255]);
    assert.deepEqual(await p.pixel(150,50), [0,0,255,255], '不能擦除 layer 以外的背景');
    assert.deepEqual(await p.pixel(170,50), [0,255,0,255], '遮罩之后的兄弟仍按显示列表绘制');
    await p.update('__blendLayer.blendMode="normal";');
    assert.deepEqual(await p.pixel(50,50), [255,255,255,255], '离开 layer 时保留既有普通叠加行为');
    assert.deepEqual(await p.pixel(150,50), [255,0,0,255]);
    await p.update('__blendLayer.blendMode="layer";');
    assert.deepEqual(await p.pixel(50,50), [255,0,0,255], '切回 layer 必须重建合成内容');
    assert.deepEqual(await p.pixel(150,50), [0,0,255,255]);
    await p.update('__matte.blendMode="normal";');
    assert.deepEqual(await p.pixel(50,50), [255,255,255,255]);
    await p.update('__matte.blendMode="alpha";');
    assert.deepEqual(await p.pixel(50,50), [255,0,0,255]);
    await healthy(p);
}

async function projected(p) {
    await p.script(scene + `
__source.x=__matte.x=300;__source.y=__matte.y=200;__source.z=__matte.z=100;
`);
    assert.deepEqual(await p.pixel(350,250), [255,0,0,255], '3D 内容的颜色保留');
    assert.deepEqual(await p.pixel(450,250), [0,0,0,0], '屏幕空间合成也必须裁去遮罩外像素');
    assert.equal(await p.page.evaluate(async () => {
        const {hostState}=await import('/script-danmaku/core.js');
        return hostState.canvas.style.visibility === 'hidden';
    }), false, '需要 layer 隔离时不得直接呈现未遮罩的 WebGL 批次');
    await p.update('__matte.alpha=0.5;');
    assert.deepEqual(await p.pixel(350,250), [255,0,0,128]);
    await p.update('__matte.alpha=0;');
    assert.deepEqual(await p.pixel(350,250), [0,0,0,0]);
    await p.update('__matte.alpha=1;__matte.x=400;');
    assert.deepEqual(await p.pixel(350,250), [0,0,0,0]);
    assert.deepEqual(await p.pixel(450,250), [255,0,0,255]);
    await healthy(p);
}

(async () => {
    const host = await createBrowserHost();
    try {
        for (const disableWebgl of [false,true]) {
            for (const run of [alphaAndUpdates,isolationAndModeChanges,nestedLayers,projected]) {
                const p = await host.page(800,600,1,disableWebgl,true);
                try {
                    await run(p);
                    console.log('ok Akari TrackMatte ' + run.name + (disableWebgl ? ' Canvas' : ' WebGL'));
                } finally { await p.page.close(); }
            }
        }
    } finally { await host.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
