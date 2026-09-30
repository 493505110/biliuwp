'use strict';

// 真 Chromium 像素回归，避免 Canvas 桩把“存住参数”误当成“画面正确”。
// npm install --no-save playwright；node tests/host/rendering-pixels.test.js
// 默认使用已安装的 Edge；BROWSER_EXECUTABLE 可指定 Chromium。
const assert = require('node:assert/strict');
const { createBrowserHost } = require('./browser-host');
const shape = 'var box=$.createShape({lifeTime:20,x:100,y:100});box.graphics.beginFill(0xffffff,1);box.graphics.drawRect(0,0,40,40);box.graphics.endFill();window.__box=box;';
const cases = [];
function test(name, run) { cases.push({ name, run }); }

test('P1 ColorTransform 真正将白色染为红色，更新后缓存失效', async p => {
    await p.script(shape + 'box.transform.colorTransform=$.createColorTransform(1,0,0,1);');
    assert.deepEqual(await p.pixel(120, 120), [255, 0, 0, 255]);
    await p.update('window.__box.transform.colorTransform={redMultiplier:0,greenMultiplier:1,blueMultiplier:0,alphaMultiplier:1,redOffset:0,greenOffset:0,blueOffset:0,alphaOffset:0};');
    assert.deepEqual(await p.pixel(120, 120), [0, 255, 0, 255]);
});
test('P2 BlurFilter 参数顺序正确，横向模糊不扩散到纵向', async p => {
    await p.script(shape + 'box.filters=[$.createBlurFilter(16,0,1)];');
    assert.ok((await p.pixel(96, 120))[3] > 0);
    assert.equal((await p.pixel(120, 96))[3], 0);
});
test('P3 容器发光有指定颜色，64px 模糊不被12px缓存边距截断', async p => {
    await p.script(shape + 'var group=$.createCanvas({lifeTime:20});group.addChild(box);group.filters=[$.createGlowFilter(0xff0000,1,64,64,2)];');
    const pixel = await p.pixel(80, 120);
    assert.ok(pixel[0] > 200 && pixel[1] === 0 && pixel[2] === 0 && pixel[3] > 0, JSON.stringify(pixel));
    assert.equal((await p.pixel(60, 120))[3], 0);
});
test('P4 inner 和 knockout 生效，滤镜数组依次应用', async p => {
    await p.script(shape + 'box.filters=[$.createGlowFilter(0xff0000,1,8,8,2,1,true,true)];');
    assert.ok((await p.pixel(101, 120))[3] > 0);
    assert.equal((await p.pixel(120, 120))[3], 0);
    assert.equal((await p.pixel(97, 120))[3], 0);
});
test('P5 3D矩阵平移参与绘制，相对矩阵保留深度', async p => {
    await p.script(shape + 'var m=$.createMatrix3D();m.appendTranslation(200,50,100);box.transform.matrix3D=m;window.__relative=box.transform.getRelativeMatrix3D($.root).rawData;');
    const data = await p.page.evaluate(() => window.__relative.slice(12, 15));
    assert.deepEqual(data, [200, 50, 100]);
    const point = await p.page.evaluate(async () => {
        const { projectPoint, projectionFor } = await import('/script-danmaku/geometry.js');
        return projectPoint({ x: 220, y: 70, z: 100 }, projectionFor(window.__box));
    });
    assert.ok((await p.pixel(Math.round(point.x), Math.round(point.y)))[3] > 240);
    assert.equal((await p.pixel(120, 120))[3], 0);
});
test('P6 3D子树在投影前保留父子深度，rotationY 改变画面宽度', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:400,y:300,z:100});' + shape + 'group.addChild(box);box.x=0;box.y=0;box.rotationY=60;');
    assert.ok((await p.pixel(405, 315))[3] > 200);
    assert.equal((await p.pixel(435, 315))[3], 0);
});
test('P7 matrix 是完整变换，绘制与包围盒一致，无双重平移或裁切', async p => {
    await p.script(shape + 'box.transform.matrix=$.createMatrix(1,0,1,1,200,50);');
    assert.ok((await p.pixel(250, 80))[3] > 240);
    assert.equal((await p.pixel(350, 180))[3], 0);
});
test('P8 嵌套包围盒不丢失先前兄弟，容器自身graphics保留', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20});group.graphics.beginFill(0xff0000);group.graphics.drawRect(0,0,20,20);group.graphics.endFill();' + shape + 'group.addChild(box);var inner=$.createCanvas({lifeTime:20,x:300});group.addChild(inner);var other=$.createShape({lifeTime:20,parent:inner});other.graphics.beginFill(0x00ff00);other.graphics.drawRect(0,100,20,20);other.graphics.endFill();');
    assert.deepEqual(await p.pixel(10, 10), [255, 0, 0, 255]);
    assert.deepEqual(await p.pixel(120, 120), [255, 255, 255, 255]);
    assert.deepEqual(await p.pixel(310, 110), [0, 255, 0, 255]);
});
test('P9 motion startDelay 单位为毫秒', async p => {
    await p.script(shape + 'box.remove();window.__box=$.createShape({lifeTime:20,motion:{x:{fromValue:0,toValue:100,lifeTime:1,startDelay:100}}});');
    await p.page.evaluate(() => window.__step(150));
    const x = await p.page.evaluate(() => window.__box.x);
    assert.ok(Math.abs(x - 5) < 0.001, String(x));
});
test('P10 容器最后一个孩子隐藏后，原像素完全消失', async p => {
    await p.script(shape + 'var group=$.createCanvas({lifeTime:20});group.addChild(box);');
    await p.update('window.__box.visible=false;');
    assert.equal((await p.pixel(120, 120))[3], 0);
});

test('P11 擦除后按显示列表重贴，不改变半透明图层的叠加顺序', async p => {
    await p.script('var bottom=$.createShape({lifeTime:20,x:100,y:100});bottom.graphics.beginFill(0x0000ff);bottom.graphics.drawRect(0,0,40,40);bottom.graphics.endFill();var top=$.createShape({lifeTime:20,x:100,y:100,alpha:0.5});top.graphics.beginFill(0xff0000);top.graphics.drawRect(0,0,40,40);top.graphics.endFill();window.__bottom=bottom;');
    const before = await p.pixel(125, 120);
    await p.update('window.__bottom.x=110;');
    assert.deepEqual(await p.pixel(125, 120), before);
});

test('P12 显示对象尺寸立即包含缩放后的子树，居中与width赋值可用', async p => {
    await p.script(shape + 'box.x=0;box.y=0;box.scaleX=2;var group=$.createCanvas({lifeTime:20,x:400,y:300});group.addChild(box);group.x-=group.width/2;window.__size=[box.width,box.height,group.width];');
    assert.deepEqual(await p.page.evaluate(() => window.__size), [80,40,80]);
    assert.ok((await p.pixel(361,310))[3]>240);
    await p.update('window.__box.width=40;');
    assert.equal(await p.page.evaluate(() => window.__box.width),40);
    assert.equal((await p.pixel(420,310))[3],0);
});
test('P13 drawPath 的复合轮廓保留字形空洞，二次曲线参与绘制和尺寸', async p => {
    await p.script('var glyph=$.createShape({lifeTime:20,x:100,y:100});glyph.graphics.beginFill(0xffffff);glyph.graphics.drawPath([1,2,2,2,2,1,2,2,2,2],[0,0,60,0,60,60,0,60,0,0,20,20,20,40,40,40,40,20,20,20],"nonZero");glyph.graphics.endFill();var curve=$.createShape({lifeTime:20,x:300,y:100});curve.graphics.beginFill(0xffffff);curve.graphics.drawPath([1,3,2],[0,0,100,100,200,0,0,0]);curve.graphics.endFill();window.__size=[curve.width,curve.height];');
    assert.equal((await p.pixel(130,130))[3],0);
    assert.ok((await p.pixel(110,130))[3]>240);
    assert.ok((await p.pixel(400,140))[3]>240);
    assert.equal((await p.pixel(400,170))[3],0);
    assert.deepEqual(await p.page.evaluate(() => window.__size),[200,50]);
});
test('P14 drawPath 三次曲线与WIDE命令正确消费坐标', async p => {
    await p.script('var curve=$.createShape({lifeTime:20,x:100,y:100});curve.graphics.beginFill(0xffffff);curve.graphics.drawPath([4,6,5],[999,999,0,0,0,100,200,100,200,0,999,999,0,0]);curve.graphics.endFill();window.__size=[curve.width,curve.height];');
    assert.deepEqual(await p.page.evaluate(() => window.__size),[200,75]);
    assert.ok((await p.pixel(200,170))[3]>240);
    assert.equal((await p.pixel(200,185))[3],0);
});

test('P15 放大的矢量子树按显示倍率重建缓存，移动不重烘', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:100,y:100});group.scaleX=group.scaleY=20;var glyph=$.createShape({lifeTime:20,parent:group});glyph.graphics.beginFill(0xffffff);glyph.graphics.drawPath([1,2,2,2],[0,0,2,0,0,2,0,0]);glyph.graphics.endFill();window.__glyph=glyph;window.__group=group;');
    assert.ok((await p.pixel(125,105))[3]>245);
    assert.equal((await p.pixel(135,115))[3],0);
    const before = await p.page.evaluate(async () => { const {hostState}=await import('/script-danmaku/core.js');return hostState.paintCount; });
    await p.update('window.__group.x=200;');
    assert.ok((await p.pixel(225,105))[3]>245);
    const after = await p.page.evaluate(async () => { const {hostState}=await import('/script-danmaku/core.js');return hostState.paintCount; });
    assert.equal(after,before);
});

test('P16 场外巨型图元限制缓存尺寸，缩小显示仍有画面', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:100,y:100});group.scaleX=group.scaleY=0.05;var large=$.createShape({lifeTime:20,parent:group});large.graphics.beginFill(0xffffff);large.graphics.drawRect(0,0,24000,200);large.graphics.endFill();window.__large=large;window.__group=group;');
    assert.ok((await p.pixel(400,105))[3]>240);
    const widths = await p.page.evaluate(() => [window.__large.cacheCanvas.width,window.__group.composite.width]);
    assert.ok(widths.every(width => width <= 4096),JSON.stringify(widths));
});

test('P17 缩小显示的字体轮廓按实际倍率缓存，避免上传原始巨型纹理', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:100,y:100});group.scaleX=group.scaleY=0.02;var glyph=$.createShape({lifeTime:20,parent:group});glyph.graphics.beginFill(0xffffff);glyph.graphics.drawRect(0,0,2000,2000);glyph.graphics.endFill();window.__glyph=glyph;');
    assert.ok((await p.pixel(120,120))[3]>240);
    assert.ok(await p.page.evaluate(() => window.__glyph.cacheCanvas.width <= 128));
});

test('P18 3D元件清空graphics后旧缓存不再显示', async p => {
    await p.script(shape + 'box.z=100;');
    assert.ok((await p.pixel(152,141))[3]>240);
    await p.update('window.__box.graphics.clear();');
    assert.equal((await p.pixel(152,141))[3],0);
});
test('P19 3D自绘位图更新后不冻结在首帧', async p => {
    await p.script('var bitmap=$.createLayer(40,40,{lifeTime:20,x:100,y:100,z:100});bitmap.layer.fillStyle="#ff0000";bitmap.layer.fillRect(0,0,40,40);window.__bitmap=bitmap;');
    assert.deepEqual(await p.pixel(152,141),[255,0,0,255]);
    await p.update('window.__bitmap.layer.fillStyle="#00ff00";window.__bitmap.layer.fillRect(0,0,40,40);window.__bitmap.x+=0.01;');
    assert.deepEqual(await p.pixel(152,141),[0,255,0,255]);
});

test('P20 GPU缓冲复用保留不同大小图元的颜色、透明度和移动擦除', async p => {
    await p.script('var red=$.createShape({lifeTime:20,x:400,y:300,rotationY:60});red.graphics.beginFill(0xff0000,0.5);red.graphics.drawRect(0,0,40,40);red.graphics.endFill();var green=$.createShape({lifeTime:20,x:100,y:100,rotationY:60});green.graphics.beginFill(0x00ff00,0.5);green.graphics.drawRect(0,0,20,20);green.graphics.endFill();window.__red=red;');
    const red = await p.pixel(410,320), green = await p.pixel(102,110);
    assert.ok(red[0]>240 && red[1]===0 && Math.abs(red[3]-128)<=1,JSON.stringify(red));
    assert.ok(green[1]>240 && green[0]===0 && Math.abs(green[3]-128)<=1,JSON.stringify(green));
    await p.update('window.__red.x=450;');
    assert.equal((await p.pixel(410,320))[3],0);
    assert.deepEqual(await p.pixel(102,110),green);
    assert.ok((await p.pixel(461,320))[0]>240);
});

test('P21 3D子树批量合成保持半透明层序和元素遮罩边界', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:400,y:300,z:100});var red=$.createShape({lifeTime:20,parent:group,rotationY:60});red.graphics.beginFill(0xff0000,0.5);red.graphics.drawRect(0,0,40,40);red.graphics.endFill();var green=$.createShape({lifeTime:20,parent:group,rotationY:-60});green.graphics.beginFill(0x00ff00,0.5);green.graphics.drawRect(0,0,40,40);green.graphics.endFill();var mask=$.createShape({lifeTime:20,parent:group});mask.graphics.beginFill(0xffffff);mask.graphics.drawRect(0,0,15,40);mask.graphics.endFill();green.mask=mask;');
    const overlap = await p.pixel(405,315), outside = await p.pixel(417,315);
    assert.ok(Math.abs(overlap[0]-85)<4 && Math.abs(overlap[1]-170)<4 && Math.abs(overlap[3]-192)<3,JSON.stringify(overlap));
    assert.ok(outside[0]>240 && outside[1]===0 && Math.abs(outside[3]-128)<3,JSON.stringify(outside));
});
test('P22 非共面容器滤镜切换批次时保持父级背景和颜色变换', async p => {
    await p.script('var outer=$.createCanvas({lifeTime:20});outer.graphics.beginFill(0x0000ff);outer.graphics.drawRect(0,0,800,600);outer.graphics.endFill();var group=$.createCanvas({lifeTime:20,parent:outer,x:400,y:300,z:100});group.filters=[$.createBlurFilter(16,0,1)];group.transform.colorTransform=$.createColorTransform(1,0,0,1);var box=$.createShape({lifeTime:20,parent:group,rotationY:60});box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,40,40);box.graphics.endFill();');
    const center = await p.pixel(406,315), halo = await p.pixel(395,315);
    assert.ok(center[0]>120 && center[1]===0 && center[3]===255,JSON.stringify(center));
    assert.ok(halo[0]>0 && halo[2]>0 && halo[3]===255,JSON.stringify(halo));
    assert.deepEqual(await p.pixel(300,340),[0,0,255,255]);
});

test('P23 复用GPU纹理时，颜色变换更新仍刷新同一份位图', async p => {
    await p.script('var box=$.createShape({lifeTime:20,x:400,y:300,rotationY:60});box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,40,40);box.graphics.endFill();box.transform.colorTransform=$.createColorTransform(1,0,0,1);window.__box=box;');
    assert.deepEqual(await p.pixel(410,320),[255,0,0,255]);
    await p.page.evaluate(() => { window.__oldCanvas=window.__box.cacheCanvas; });
    await p.update('window.__box.transform.colorTransform={redMultiplier:0,greenMultiplier:1,blueMultiplier:0,alphaMultiplier:1};');
    assert.deepEqual(await p.pixel(410,320),[0,255,0,255]);
    assert.ok(await p.page.evaluate(() => window.__oldCanvas===window.__box.cacheCanvas));
});

async function run() {
    const host = await createBrowserHost();
    try {
        for (const item of cases) {
            const probe = await host.page();
            try {
                await item.run(probe);
                assert.deepEqual(probe.errors, []);
                assert.deepEqual(await probe.page.evaluate(() => window.__messages.filter(m => m.type === 'error')), []);
                console.log('ok ' + item.name);
            } catch (error) {
                console.error('FAIL ' + item.name + ': ' + error.stack);
                process.exitCode = 1;
            }
            await probe.page.close();
        }
    } finally { await host.close(); }
}
run().catch(error => { console.error(error); process.exitCode = 1; });
