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
    assert.ok((await p.pixel(185,160))[3]>240);
    await p.update('window.__box.graphics.clear();');
    assert.equal((await p.pixel(185,160))[3],0);
});
test('P19 3D自绘位图更新后不冻结在首帧', async p => {
    await p.script('var bitmap=$.createLayer(40,40,{lifeTime:20,x:100,y:100,z:100});bitmap.layer.fillStyle="#ff0000";bitmap.layer.fillRect(0,0,40,40);window.__bitmap=bitmap;');
    assert.deepEqual(await p.pixel(185,160),[255,0,0,255]);
    await p.update('window.__bitmap.layer.fillStyle="#00ff00";window.__bitmap.layer.fillRect(0,0,40,40);window.__bitmap.x+=0.01;');
    assert.deepEqual(await p.pixel(185,160),[0,255,0,255]);
});

test('P20 GPU缓冲复用保留不同大小图元的颜色、透明度和移动擦除', async p => {
    // 图元横向锚点取投影中心，避开固定短焦距下近乎侧对摄像机的细窄平面；
    // 此例检查缓冲复用后的颜色与透明度，不取抗锯齿边缘。
    await p.script('var red=$.createShape({lifeTime:20,x:400,y:300,rotationY:60});red.graphics.beginFill(0xff0000,0.5);red.graphics.drawRect(0,0,40,40);red.graphics.endFill();var green=$.createShape({lifeTime:20,x:400,y:100,rotationY:60});green.graphics.beginFill(0x00ff00,0.5);green.graphics.drawRect(0,0,20,20);green.graphics.endFill();window.__red=red;');
    const red = await p.pixel(410,320), green = await p.pixel(405,105);
    assert.ok(red[0]>240 && red[1]===0 && Math.abs(red[3]-128)<=1,JSON.stringify(red));
    assert.ok(green[1]>240 && green[0]===0 && Math.abs(green[3]-128)<=1,JSON.stringify(green));
    await p.update('window.__red.x=450;');
    assert.equal((await p.pixel(410,320))[3],0);
    assert.deepEqual(await p.pixel(405,105),green);
    assert.ok((await p.pixel(461,320))[0]>240);
});

test('P21 3D子树批量合成保持半透明层序和元素遮罩边界', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:400,y:300,z:100});var red=$.createShape({lifeTime:20,parent:group,rotationY:60});red.graphics.beginFill(0xff0000,0.5);red.graphics.drawRect(0,0,40,40);red.graphics.endFill();var green=$.createShape({lifeTime:20,parent:group,rotationY:-60});green.graphics.beginFill(0x00ff00,0.5);green.graphics.drawRect(0,0,40,40);green.graphics.endFill();var mask=$.createShape({lifeTime:20,parent:group});mask.graphics.beginFill(0xffffff);mask.graphics.drawRect(0,0,15,40);mask.graphics.endFill();green.mask=mask;');
    const overlap = await p.pixel(405,315), outside = await p.pixel(415,315);
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

test('P24 scrollRect 按本地坐标滚动并裁剪，更新后擦掉旧位置', async p => {
    await p.script(shape + 'box.scrollRect=$.createRectangle(10,5,20,20);');
    assert.deepEqual(await p.pixel(105,105), [255,255,255,255]);
    assert.equal((await p.pixel(125,105))[3],0);
    assert.equal((await p.pixel(105,125))[3],0);
    assert.deepEqual(await p.page.evaluate(() => [window.__box.x,window.__box.transform.matrix.tx]), [100,100]);
    await p.update('window.__box.scrollRect={x:35,y:5,width:20,height:20};');
    assert.equal((await p.pixel(110,105))[3],0);
    assert.ok((await p.pixel(102,105))[3]>240);
    await p.update('window.__box.scrollRect=null;');
    assert.ok((await p.pixel(130,130))[3]>240);
});
test('P25 容器的 scrollRect 同时裁剪自身graphics与嵌套子树', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:200,y:100,scaleX:2});group.scrollRect=$.createRectangle(10,0,20,20);group.graphics.beginFill(0xff0000);group.graphics.drawRect(0,0,40,40);group.graphics.endFill();var child=$.createShape({lifeTime:20,parent:group,x:20});child.graphics.beginFill(0x00ff00);child.graphics.drawRect(0,0,40,40);child.graphics.endFill();');
    assert.deepEqual(await p.pixel(205,105),[255,0,0,255]);
    assert.deepEqual(await p.pixel(225,105),[0,255,0,255]);
    assert.equal((await p.pixel(245,105))[3],0);
    assert.equal((await p.pixel(225,125))[3],0);
});
test('P26 3D非共面子树的 scrollRect 裁剪投影且保留透明层序', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:400,y:300,z:100});group.scrollRect=$.createRectangle(10,0,15,30);var box=$.createShape({lifeTime:20,parent:group,rotationY:45});box.graphics.beginFill(0xff0000);box.graphics.drawRect(0,0,100,100);box.graphics.endFill();window.__group=group;');
    assert.ok((await p.pixel(405,310))[0]>240);
    assert.equal((await p.pixel(430,310))[3],0);
    await p.update('window.__group.scrollRect={x:10,y:0,width:0,height:30};');
    assert.equal((await p.pixel(405,310))[3],0);
});
test('P27 DropShadow 的距离、角度、颜色和hideObject进入绘制', async p => {
    await p.script(shape + 'box.filters=[$.createDropShadowFilter(50,0,0xff0000,1,0,0,1,1,false,false,true)];');
    assert.equal((await p.pixel(120,120))[3],0);
    assert.deepEqual(await p.pixel(170,120),[255,0,0,255]);
    assert.equal((await p.pixel(145,120))[3],0);
    await p.update('window.__box.filters=[{kind:"DropShadowFilter",distance:50,angle:90,color:0x00ff00,alpha:1,blurX:0,blurY:0,strength:1,quality:1,inner:false,knockout:false,hideObject:false}];');
    assert.deepEqual(await p.pixel(120,170),[0,255,0,255]);
    assert.deepEqual(await p.pixel(120,120),[255,255,255,255]);
    assert.equal((await p.pixel(170,120))[3],0);
});
test('P28 内阴影限制在原填充中，knockout去掉填充', async p => {
    await p.script(shape + 'box.filters=[$.createDropShadowFilter(10,0,0xff0000,1,0,0,1,1,true,true)];');
    assert.deepEqual(await p.pixel(105,120),[255,0,0,255]);
    assert.equal((await p.pixel(120,120))[3],0);
    assert.equal((await p.pixel(95,120))[3],0);
});
test('P29 ColorMatrix 的4×5矩阵变换RGBA，数组按顺序合成', async p => {
    await p.script(shape + 'box.filters=[$.createColorMatrixFilter([0,0,0,0,255,0,0,0,0,0,0,0,0,0,0,0,0,0,0.5,0]),$.createBlurFilter(0,0,1)];');
    const red=await p.pixel(120,120);
    assert.ok(red[0]===255 && red[1]===0 && red[2]===0 && Math.abs(red[3]-128)<=1,JSON.stringify(red));
    await p.update('window.__box.filters=[];');
    assert.deepEqual(await p.pixel(120,120),[255,255,255,255]);
});
test('P30 Convolution 使用核、divisor与bias，并保留原alpha', async p => {
    await p.script(shape + 'box.filters=[$.createConvolutionFilter(1,1,[1],2,10,true)];');
    const pixel=await p.pixel(120,120);
    assert.ok(Math.abs(pixel[0]-138)<=1 && pixel[3]===255,JSON.stringify(pixel));
});
test('P31 Bevel 的方向、两种颜色、inner与knockout进入绘制', async p => {
    await p.script(shape + 'box.filters=[$.createBevelFilter(5,0,0xff0000,1,0x0000ff,1,0,0,1,1,"inner",true)];');
    assert.deepEqual(await p.pixel(102,120),[255,0,0,255]);
    assert.deepEqual(await p.pixel(137,120),[0,0,255,255]);
    assert.equal((await p.pixel(120,120))[3],0);
    assert.equal((await p.pixel(97,120))[3],0);
});
test('P32 混合仿射和透视图元时批次边界不改变半透明层序', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:400,y:300});var red=$.createShape({lifeTime:20,parent:group,rotationY:45});red.graphics.beginFill(0xff0000);red.graphics.drawRect(0,0,40,40);red.graphics.endFill();var green=$.createShape({lifeTime:20,parent:group,alpha:0.5,z:1});green.graphics.beginFill(0x00ff00);green.graphics.drawRect(0,0,40,40);green.graphics.endFill();var blue=$.createShape({lifeTime:20,parent:group,alpha:0.5,rotationY:45});blue.graphics.beginFill(0x0000ff);blue.graphics.drawRect(0,0,40,40);blue.graphics.endFill();');
    const pixel=await p.pixel(410,310);
    assert.ok(Math.abs(pixel[0]-64)<3 && Math.abs(pixel[1]-64)<3 && Math.abs(pixel[2]-128)<3 && pixel[3]===255,JSON.stringify(pixel));
});
test('P33 绝对帧时钟不会落在110000ms场景边界之前', async p => {
    await p.page.evaluate(() => {
        scriptDanmakuHost.reset(0,true,1,true);
        scriptDanmakuHost.append([{id:"boundary",stime:0,duration:200,lang:"js",code:'window.__switch=false;var box=$.createShape({lifeTime:200});box.addEventListener("enterFrame",function(){if(Player.time>=110000)window.__switch=true;});'}]);
        window.__step(0);
    });
    await p.page.evaluate(() => { for(let frame=1;frame<=6600;frame++)window.__stepTo(frame*1000/60); });
    assert.equal(await p.page.evaluate(() => window.__switch),true);
});

test('P34 Flash模糊使用完整核宽，1px不模糊，3px为三点平均', async p => {
    await p.script('var box=$.createShape({lifeTime:20,x:100,y:100});box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,1,20);box.graphics.endFill();box.filters=[$.createBlurFilter(1,0,1)];window.__box=box;');
    assert.deepEqual(await p.pixel(100,110),[255,255,255,255]);
    assert.equal((await p.pixel(99,110))[3],0);
    await p.update('window.__box.filters=[{kind:"BlurFilter",blurX:3,blurY:0,quality:1}];');
    for(const x of [99,100,101]) assert.ok(Math.abs((await p.pixel(x,110))[3]-85)<=1);
    assert.equal((await p.pixel(98,110))[3],0);
});
test('P35 GPU与CPU滤镜在分数核宽、quality、inner/knockout和颜色变换下吻合', async p => {
    const host=await createBrowserHost();
    try {
        const fallback=await host.page(800,600,1,true);
        try {
            const codes=[
                shape+'box.filters=[$.createBlurFilter(5.5,3.25,2)];',
                shape+'box.filters=[$.createGlowFilter(0xff0000,0.5,8,4,2,2,false,true)];',
                shape+'box.filters=[$.createGlowFilter(0x00ff00,1,8,4,2,1,true,false)];',
                shape+'box.filters=[$.createBlurFilter(7,3,1)];box.transform.colorTransform=$.createColorTransform(1,0,0,0.5);',
                shape+'box.filters=[$.createBlurFilter(65.5,128,1)];',
                shape+'box.filters=[$.createBlurFilter(255,2,1)];'
            ];
            for(const code of codes){
                await p.script(code);await fallback.script(code);
                for(const point of [[96,120],[99,120],[100,120],[103,120],[120,120],[120,99],[140,120]]){
                    const actual=await p.pixel(...point), expected=await fallback.pixel(...point);
                    assert.ok(actual.every((v,i)=>Math.abs(v-expected[i])<=3),JSON.stringify({point,actual,expected}));
                }
            }
            assert.deepEqual(fallback.errors,[]);
            assert.deepEqual(await fallback.page.evaluate(()=>window.__messages.filter(m=>m.type==='error')),[]);
        } finally { await fallback.page.close(); }
    } finally { await host.close(); }
});
test('P36 自绘位图的颜色矩阵生效，逐帧内容更新不会冻结滤镜缓存', async p => {
    await p.script('var box=$.createLayer(40,40,{lifeTime:20,x:100,y:100});box.layer.fillStyle="#ffffff";box.layer.fillRect(0,0,40,40);box.filters=[$.createColorMatrixFilter([0,0,0,0,255,0,0,0,0,0,0,0,0,0,0,0,0,0,1,0])];window.__box=box;');
    assert.deepEqual(await p.pixel(120,120),[255,0,0,255]);
    await p.update('window.__box.layer.clearRect(0,0,40,40);window.__box.layer.fillRect(0,0,10,10);');
    await p.page.evaluate(() => window.__step(1000/60));
    assert.equal((await p.pixel(120,120))[3],0);
    assert.deepEqual(await p.pixel(105,105),[255,0,0,255]);
});
test('P37 横长与竖长滤镜缓存交替时仍走GPU，历史边长不触发CPU回读', async p => {
    await p.page.evaluate(() => {
        window.__readbacks=0;
        const read=CanvasRenderingContext2D.prototype.getImageData;
        CanvasRenderingContext2D.prototype.getImageData=function(...args){window.__readbacks++;return read.apply(this,args);};
    });
    await p.script('var box=$.createShape({lifeTime:20,x:100,y:100});box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,2500,20);box.graphics.endFill();box.filters=[$.createBlurFilter(8,4,1)];');
    await p.script('var box=$.createShape({lifeTime:20,x:100,y:100});box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,20,1500);box.graphics.endFill();box.filters=[$.createBlurFilter(8,4,1)];');
    assert.equal(await p.page.evaluate(()=>window.__readbacks),0);
    assert.deepEqual(await p.pixel(110,120),[255,255,255,255]);
});
test('P38 自绘位图的滤镜扩边进入损坏范围，清空后阴影全部擦除', async p => {
    await p.script('var box=$.createLayer(40,40,{lifeTime:20,x:100,y:100});box.layer.fillStyle="#ffffff";box.layer.fillRect(0,0,40,40);box.filters=[$.createDropShadowFilter(50,0,0xff0000,1,0,0,1,1,false,false,true)];window.__box=box;');
    assert.deepEqual(await p.pixel(170,120),[255,0,0,255]);
    await p.update('window.__box.layer.clearRect(0,0,40,40);');
    assert.equal((await p.pixel(170,120))[3],0);
});

test('P39 驱动拒绝模糊着色器变体时，完整退回CPU且保留滤镜结果', async p => {
    await p.page.evaluate(() => {
        const sources=new WeakMap(), source=WebGLRenderingContext.prototype.shaderSource, status=WebGLRenderingContext.prototype.getShaderParameter;
        window.__rejectedShader=0;
        WebGLRenderingContext.prototype.shaderSource=function(shader,text){sources.set(shader,text);return source.call(this,shader,text);};
        WebGLRenderingContext.prototype.getShaderParameter=function(shader,key){
            if(key===this.COMPILE_STATUS && /i<4;/.test(sources.get(shader)||'')){window.__rejectedShader++;return false;}
            return status.call(this,shader,key);
        };
    });
    await p.script('var box=$.createShape({lifeTime:20,x:100,y:100});box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,1,20);box.graphics.endFill();box.filters=[$.createBlurFilter(5,0,1)];');
    for(const x of [98,99,100,101,102]) assert.ok(Math.abs((await p.pixel(x,110))[3]-51)<=1);
    assert.equal(await p.page.evaluate(()=>window.__rejectedShader),1);
});

test('P40 原作4096宽大字形和NPOT缓存不退回CPU像素循环', async p => {
    await p.page.evaluate(() => {
        window.__readbacks=0;
        const read=CanvasRenderingContext2D.prototype.getImageData;
        CanvasRenderingContext2D.prototype.getImageData=function(...args){window.__readbacks++;return read.apply(this,args);};
    });
    for(const [width,height] of [[4090,1390],[2800,2200]]){
        await p.script('var box=$.createShape({lifeTime:20,x:100,y:100});box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,'+width+','+height+');box.graphics.endFill();box.filters=[$.createBlurFilter(16,16,1)];');
    }
    assert.equal(await p.page.evaluate(()=>window.__readbacks),0);
    assert.deepEqual(await p.pixel(120,120),[255,255,255,255]);
});

test('P41 滤镜FBO创建失败后每次更新都保持CPU后备，不留下空白缓存', async p => {
    await p.page.evaluate(() => {
        const status=WebGLRenderingContext.prototype.checkFramebufferStatus;
        window.__failedFramebuffer=false;
        WebGLRenderingContext.prototype.checkFramebufferStatus=function(target){
            if(!window.__failedFramebuffer){window.__failedFramebuffer=true;return this.FRAMEBUFFER_UNSUPPORTED;}
            return status.call(this,target);
        };
    });
    await p.script('var box=$.createShape({lifeTime:20,x:100,y:100});box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,1,20);box.graphics.endFill();box.filters=[$.createBlurFilter(5,0,1)];window.__box=box;');
    assert.equal(await p.page.evaluate(()=>window.__failedFramebuffer),true);
    for(let update=0;update<2;update++){
        await p.update('window.__box.filters=[{kind:"BlurFilter",blurX:5,blurY:0,quality:1}];');
        for(const x of [98,99,100,101,102]) assert.ok(Math.abs((await p.pixel(x,110))[3]-51)<=1);
    }
});

const filterGlobals = 'window.__display=$;window.__bitmap=Bitmap;';
test('P42 GradientGlow 以模糊覆盖率查渐变，端点、色标与透明度进入绘制', async p => {
    await p.script(filterGlobals + shape + 'box.filters=[$.createGradientGlowFilter(0,0,[0xff0000,0x0000ff],[0,1],[0,255],0,0,1,1,"full",true)];');
    assert.deepEqual(await p.pixel(120,120), [0,0,255,255]);
    assert.equal((await p.pixel(98,120))[3], 0);
    await p.update('window.__box.filters=[window.__display.createGradientGlowFilter(0,0,[0,0xff0000,0x00ff00,0x0000ff],[0,1,1,1],[0,85,170,255],9,0,1,1,"outer",true)];');
    assert.deepEqual(await p.pixel(98,120), [255,0,0,255]);
    const mixed = await p.pixel(99,120);
    assert.ok(mixed[0] > 160 && mixed[0] < 180 && mixed[1] > 75 && mixed[1] < 95 && mixed[3] === 255, JSON.stringify(mixed));
    assert.equal((await p.pixel(120,120))[3], 0);
});

test('P43 GradientGlow 的 inner/outer/full 对半透明输入采用不同合成', async p => {
    await p.script(filterGlobals + 'var box=$.createShape({lifeTime:20,x:100,y:100});box.graphics.beginFill(0xffffff,0.5);box.graphics.drawRect(0,0,40,40);box.graphics.endFill();window.__box=box;');
    for (const [type, knockout, expected] of [
        ['inner',false,[255,128,128,128]], ['outer',false,[255,170,170,192]],
        ['full',false,[255,85,85,192]], ['inner',true,[255,0,0,64]],
        ['outer',true,[255,0,0,64]], ['full',true,[255,0,0,128]]
    ]) {
        await p.update('window.__box.filters=[window.__display.createGradientGlowFilter(0,0,[0xff0000],[0.5],[128],0,0,1,1,"'+type+'",'+knockout+')];');
        const actual = await p.pixel(120,120);
        for(let c=0;c<4;c++) assert.ok(Math.abs(actual[c]-expected[c])<=2, type+' '+knockout+' '+JSON.stringify(actual));
    }
});

test('P44 GradientBevel 的两侧使用不同色标，128 是基色，方向与knockout生效', async p => {
    await p.script(filterGlobals + shape + 'box.filters=[$.createGradientBevelFilter(5,0,[0xff0000,0,0x0000ff],[1,0,1],[0,128,255],0,0,1,1,"inner",true)];');
    assert.deepEqual(await p.pixel(101,120), [255,0,0,255]);
    assert.deepEqual(await p.pixel(138,120), [0,0,255,255]);
    assert.equal((await p.pixel(120,120))[3], 0);
    assert.equal((await p.pixel(97,120))[3], 0);
    await p.update('window.__box.filters=[window.__display.createGradientBevelFilter(5,90,[0xff0000,0x00ff00,0x0000ff],[1,1,1],[0,128,255],0,0,1,1,"full",true)];');
    assert.deepEqual(await p.pixel(120,101), [255,0,0,255]);
    assert.deepEqual(await p.pixel(120,120), [0,255,0,255]);
    assert.deepEqual(await p.pixel(120,138), [0,0,255,255]);
    assert.deepEqual(await p.pixel(120,97), [255,0,0,255]);
});

test('P45 渐变滤镜容器扩边不截断，更新、移动及移除不留旧像素', async p => {
    await p.script(shape + 'var group=$.createCanvas({lifeTime:20});group.addChild(box);group.filters=[$.createGradientGlowFilter(50,0,[0,0xff0000],[0,1],[0,255],0,0,1,1,"outer",true)];window.__group=group;');
    assert.deepEqual(await p.pixel(170,120), [255,0,0,255]);
    await p.update('window.__group.x=100;');
    assert.equal((await p.pixel(170,120))[3], 0);
    assert.deepEqual(await p.pixel(270,120), [255,0,0,255]);
    await p.update('window.__group.filters=[];');
    assert.equal((await p.pixel(270,120))[3], 0);
    assert.deepEqual(await p.pixel(220,120), [255,255,255,255]);
});

test('P46 GradientBevel outer 扩边、quality/strength，以及重复色标正常绘制', async p => {
    await p.script(filterGlobals + shape + 'box.filters=[$.createGradientBevelFilter(5,0,[0xff0000,0,0x0000ff],[1,0,1],[0,128,255],0,0,1,1,"outer",true)];');
    assert.deepEqual(await p.pixel(97,120), [255,0,0,255]);
    assert.deepEqual(await p.pixel(142,120), [0,0,255,255]);
    assert.equal((await p.pixel(120,120))[3], 0);
    await p.update('window.__box.filters=[window.__display.createGradientGlowFilter(0,0,[0,0xff0000,0x00ff00],[0,1,1],[0,255,255],5.5,3.25,2,2,"full",true)];');
    assert.deepEqual(await p.pixel(120,120), [0,255,0,255]);
    assert.ok((await p.pixel(99,120))[3]>0);
    await p.update('window.__box.filters=[window.__display.createGradientGlowFilter(0,0,null,null,null),window.__display.createGradientBevelFilter(0,0,[0xff0000],[1],[0],0,0,0,1,"full",true)];');
    assert.deepEqual(await p.pixel(120,120), [255,255,255,255]);
});

const displacementStripes = filterGlobals + 'var box=$.createLayer(4,2,{lifeTime:20,x:100,y:100});var colors=["#ff0000","#00ff00","#0000ff","#ffff00"];for(var i=0;i<4;i++){box.layer.fillStyle=colors[i];box.layer.fillRect(i,0,1,2);}var map=Bitmap.createBitmapData(4,2,true,0xff008080);window.__box=box;window.__map=map;';
test('P47 DisplacementMap 按通道/256位移，四种越界模式区别于透明扩边', async p => {
    for (const [mode, expected] of [ ['wrap',[255,0,0,255]], ['clamp',[255,255,0,255]], ['ignore',[0,0,255,255]], ['color',[255,0,255,128]] ]) {
        await p.script(displacementStripes + 'box.filters=[$.createDisplacementMapFilter(map,null,1,0,-4,0,"'+mode+'",0xff00ff,0.5),$.createGlowFilter(0,0,12,12)];');
        assert.deepEqual(await p.pixel(100,100), [0,0,255,255],mode);
        assert.deepEqual(await p.pixel(102,100), expected,mode);
        assert.equal((await p.pixel(99,100))[3], 0,mode);
    }
});

test('P48 DisplacementMap mapPoint与映射图外保持原像素，未选择通道保持中性', async p => {
    await p.script(displacementStripes + 'box.filters=[$.createDisplacementMapFilter(map,{x:1,y:1},1,0,-4,0)];');
    assert.deepEqual(await p.pixel(100,100), [255,0,0,255]);
    assert.deepEqual(await p.pixel(101,101), [255,255,0,255]);
    assert.deepEqual(await p.pixel(100,101), [255,0,0,255]);
    await p.update('window.__box.filters=[window.__display.createDisplacementMapFilter(window.__map,null,0,0,500,500)];');
    assert.deepEqual(await p.pixel(100,100), [255,0,0,255]);
});

test('P49 DisplacementMap 绿/蓝/alpha通道控制纵向位移', async p => {
    await p.script(filterGlobals + 'var box=$.createLayer(2,4,{lifeTime:20,x:100,y:100});var colors=["#ff0000","#00ff00","#0000ff","#ffff00"];for(var i=0;i<4;i++){box.layer.fillStyle=colors[i];box.layer.fillRect(0,i,2,1);}window.__box=box;');
    for (const [component, color] of [[2,0xff800080],[4,0xff808000],[8,0x40808080]]) {
        await p.update('var map=window.__bitmap.createBitmapData(2,4,true,'+color+');window.__box.filters=[window.__display.createDisplacementMapFilter(map,null,0,'+component+',0,'+(component===8?-8:-4)+')];');
        assert.deepEqual(await p.pixel(100,100), [0,0,255,255]);
        assert.deepEqual(await p.pixel(100,102), [255,0,0,255]);
    }
});

test('P50 DisplacementMap 跟随栅格倍率，容器负坐标内容仍以位图左上角对齐', async p => {
    await p.script(displacementStripes + 'box.scaleX=2;box.scaleY=2;box.filters=[$.createDisplacementMapFilter(map,null,0,0,0,0)];');
    const original=[await p.pixel(100,100),await p.pixel(104,100)];
    await p.update('window.__box.filters=[window.__display.createDisplacementMapFilter(window.__map,null,1,0,-4,0)];');
    assert.deepEqual(await p.pixel(100,100), original[1]);
    assert.deepEqual(await p.pixel(104,100), original[0]);
    await p.script('var group=$.createCanvas({lifeTime:20,x:100,y:100});var box=$.createLayer(4,2,{lifeTime:20,parent:group,x:-4,y:-2});box.layer.fillStyle="#ff0000";box.layer.fillRect(0,0,2,2);box.layer.fillStyle="#0000ff";box.layer.fillRect(2,0,2,2);var map=Bitmap.createBitmapData(4,2,true,0xff008080);group.filters=[$.createDisplacementMapFilter(map,null,1,0,-4,0),$.createGlowFilter(0,0,12,12)];');
    assert.deepEqual(await p.pixel(96,98), [0,0,255,255]);
    assert.deepEqual(await p.pixel(98,98), [255,0,0,255]);
});

test('P51 BitmapData 映射图的fillRect/setPixel32/draw/dispose更新嵌套缓存', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20});' + displacementStripes + 'group.addChild(box);box.filters=[$.createDisplacementMapFilter(map,null,1,0,-4,0)];');
    assert.deepEqual(await p.pixel(100,100), [0,0,255,255]);
    await p.update('window.__map.fillRect(window.__bitmap.createRectangle(0,0,4,2),0xff808080);');
    assert.deepEqual(await p.pixel(100,100), [255,0,0,255]);
    await p.update('window.__map.setPixel32(0,0,0xff008080);');
    assert.deepEqual(await p.pixel(100,100), [0,0,255,255]);
    await p.update('var source=window.__bitmap.createBitmapData(4,2,true,0xff808080);window.__map.draw(source);');
    assert.deepEqual(await p.pixel(100,100), [255,0,0,255]);
    await p.update('window.__map.fillRect(window.__bitmap.createRectangle(0,0,4,2),0xff008080);window.__map.dispose();');
    assert.deepEqual(await p.pixel(100,100), [255,0,0,255]);
});

test('P52 位移的分数采样在预乘alpha中插值，不产生暗边', async p => {
    await p.script('var box=$.createLayer(2,1,{lifeTime:20,x:100,y:100});box.layer.fillStyle="#ff0000";box.layer.fillRect(0,0,1,1);var map=Bitmap.createBitmapData(2,1,true,0xff008080);box.filters=[$.createDisplacementMapFilter(map,null,1,0,-1,0,"clamp")];');
    assert.deepEqual(await p.pixel(100,100), [255,0,0,128]);
});

test('P53 BitmapData默认白色及alpha替换，alpha通道位移不会沿用旧像素', async p => {
    await p.script(displacementStripes + 'window.__default=Bitmap.createBitmapData(1,1);window.__opaque=Bitmap.createBitmapData(1,1,false,0);map.fillRect(Bitmap.createRectangle(0,0,4,2),0x40808080);box.filters=[$.createDisplacementMapFilter(map,null,0,8,0,-8)];');
    assert.deepEqual(await p.page.evaluate(()=>[window.__default.getPixel32(0,0),window.__opaque.getPixel32(0,0),window.__map.getPixel32(0,0)]),[0xffffffff,0xff000000,0x40808080]);
    await p.update('window.__map.setPixel32(0,0,0x80808080);');
    assert.equal(await p.page.evaluate(()=>window.__map.getPixel32(0,0)),0x80808080);
});

test('P54 新滤镜与ColorMatrix按数组顺序执行，有无WebGL均产生相同输出', async p => {
    const host=await createBrowserHost();
    const fallback=await host.page(800,600,1,true);
    try {
        const code=shape+'box.filters=[$.createGradientGlowFilter(0,0,[0,0xff0000],[0,1],[0,255],5.5,3.25,2,2,"full",true),$.createColorMatrixFilter([0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0,0,0,1,0])];';
        await p.script(code);await fallback.script(code);
        assert.deepEqual(await p.pixel(120,120),[0,255,0,255]);
        for(const x of [96,98,99,100,101,120,138,142]) assert.deepEqual(await p.pixel(x,120),await fallback.pixel(x,120));
        assert.deepEqual(fallback.errors,[]);
        assert.deepEqual(await fallback.page.evaluate(()=>window.__messages.filter(m=>m.type==='error')),[]);
    } finally {await fallback.page.close();await host.close();}
});

test('P55 shape的抗锯齿缓存边缘不移动mapPoint，容器内字形映射同样对齐', async p => {
    for (const container of [false,true]) {
        await p.script('var group=$.createCanvas({lifeTime:20,x:100,y:100});var box=$.createShape({lifeTime:20,parent:group});box.graphics.beginFill(0xff0000);box.graphics.drawRect(-4,-2,2,2);box.graphics.endFill();box.graphics.beginFill(0x0000ff);box.graphics.drawRect(-2,-2,2,2);box.graphics.endFill();var map=Bitmap.createBitmapData(4,2,true,0xff008080);'+(container?'group':'box')+'.filters=[$.createDisplacementMapFilter(map,null,1,0,-4,0)];');
        assert.deepEqual(await p.pixel(96,98),[0,0,255,255],String(container));
        assert.deepEqual(await p.pixel(98,98),[255,0,0,255],String(container));
    }
});

const defaultFocal = 500 / (2 * Math.tan(55 * Math.PI / 360));
const stageFocal = 300 / (2 * Math.tan(55 * Math.PI / 360));

test('P56 独立投影以500计算焦距，绑定根投影使用原版300宽度基准', async p => {
    const result = await p.page.evaluate(async () => {
        const { hostState } = await import('/script-danmaku/core.js');
        const { createPerspectiveProjection } = await import('/script-danmaku/display.js');
        const standalone = createPerspectiveProjection(), root = hostState.rootElement.transform.perspectiveProjection;
        return { standalone: { focal: standalone.focalLength, center: standalone.projectionCenter },
            root: { focal: root.focalLength, fov: root.fieldOfView, center: root.projectionCenter } };
    });
    assert.ok(Math.abs(result.standalone.focal - defaultFocal) < 1e-8);
    assert.deepEqual(result.standalone.center, { x:250, y:250 });
    assert.deepEqual(result.root, { focal:stageFocal, fov:55, center:{ x:400, y:300 } });
});

test('P57 clone及嵌套clone保留投影类型，视角与焦距双向联动且中心独立', async p => {
    await p.script('var original=$.root.transform.perspectiveProjection;var copied=clone({projection:original}).projection;copied.fieldOfView=90;var afterFov=copied.focalLength;copied.focalLength=500;copied.projectionCenter={x:10,y:20};var again=clone(copied);again.fieldOfView=60;again.projectionCenter={x:30,y:40};window.__projectionProbe={afterFov:afterFov,afterFocal:copied.fieldOfView,originalFocal:original.focalLength,originalCenter:original.projectionCenter,copiedCenter:copied.projectionCenter,againFocal:again.focalLength,keys:Object.keys(copied)};');
    const result = await p.page.evaluate(() => window.__projectionProbe);
    assert.ok(Math.abs(result.afterFov - 250) < 1e-8);
    assert.ok(Math.abs(result.afterFocal - 360*Math.atan(0.5)/Math.PI) < 1e-8);
    assert.ok(Math.abs(result.againFocal - 250/Math.tan(Math.PI/6)) < 1e-8);
    assert.equal(result.originalFocal, stageFocal);
    assert.deepEqual(result.originalCenter, { x:400, y:300 });
    assert.deepEqual(result.copiedCenter, { x:10, y:20 });
    assert.deepEqual(result.keys.sort(), ['fieldOfView','focalLength','projectionCenter']);
});

test('P58 已绘制元件改变投影视角后重新投影并擦除旧像素', async p => {
    await p.script(shape+'box.z=300/(2*Math.tan(55*Math.PI/360));var projection=clone($.root.transform.perspectiveProjection);projection.fieldOfView=55;box.transform.perspectiveProjection=projection;window.__projection=box.transform.perspectiveProjection;');
    assert.ok((await p.pixel(260,210))[3] > 240);
    await p.update('window.__projection.fieldOfView=90;');
    assert.ok((await p.pixel(304,238))[3] > 240);
    assert.equal((await p.pixel(260,210))[3], 0);
    await p.update('window.__projection.focalLength=300/(2*Math.tan(55*Math.PI/360));');
    assert.ok((await p.pixel(260,210))[3] > 240);
    assert.equal((await p.pixel(304,238))[3], 0);
});

test('P59 resize重建根投影中心并清除旧画面，重复同尺寸命令保留脚本设置', async p => {
    await p.script(shape+'box.z=300/(2*Math.tan(55*Math.PI/360));window.__root=$.root;');
    assert.ok((await p.pixel(260,210))[3] > 240);
    await p.page.setViewportSize({width:640,height:480});
    await p.page.waitForFunction(() => document.querySelector('#stage canvas').width === 640, null, {polling:50});
    await p.update('scriptDanmakuHost.resize();');
    const result = await p.page.evaluate(() => ({focal:window.__root.transform.perspectiveProjection.focalLength,center:window.__root.transform.perspectiveProjection.projectionCenter}));
    assert.deepEqual(result, {focal:stageFocal,center:{x:320,y:240}});
    assert.ok((await p.pixel(220,180))[3] > 240);
    assert.equal((await p.pixel(260,210))[3], 0);
    await p.update('window.__root.transform.perspectiveProjection.focalLength=700;scriptDanmakuHost.resize();');
    assert.ok(Math.abs(await p.page.evaluate(() => window.__root.transform.perspectiveProjection.focalLength) - 700) < 1e-8);
});

test('P60 根resize不覆盖子树显式投影，投影替换及移除重新绘制', async p => {
    await p.script(shape+'box.z=300/(2*Math.tan(55*Math.PI/360));var projection=clone($.root.transform.perspectiveProjection);projection.fieldOfView=55;box.transform.perspectiveProjection=projection;window.__projection=box.transform.perspectiveProjection;window.__root=$.root;');
    await p.page.setViewportSize({width:640,height:480});
    await p.page.waitForFunction(() => document.querySelector('#stage canvas').width === 640, null, {polling:50});
    await p.update('scriptDanmakuHost.resize();');
    assert.ok((await p.pixel(260,210))[3] > 240);
    await p.update('window.__box.transform.perspectiveProjection=null;');
    assert.ok((await p.pixel(220,180))[3] > 240);
    assert.equal((await p.pixel(260,210))[3], 0);
    assert.deepEqual(await p.page.evaluate(() => window.__projection.projectionCenter), {x:400,y:300});
    await p.update('window.__root.transform.perspectiveProjection.projectionCenter={x:100,y:100};');
    assert.ok((await p.pixel(109,109))[3] > 240);
    assert.equal((await p.pixel(220,180))[3], 0);
});

test('P61 原样Akari摄像机的fov进入焦距计算，GPU与Canvas后备保持投影一致', async p => {
    const fs = require('node:fs'), path = require('node:path');
    const akari = fs.readFileSync(path.join(__dirname,'fixtures/real/entry_08_1147140491.js'),'utf8');
    const script = akari + '\nvar camera=Akari.Display.Three.Camera({fov:90});camera.update(0);window.__camera=camera;'+shape+'box.z=300/(2*Math.tan(55*Math.PI/360));box.transform.perspectiveProjection=camera.projection;';
    await p.script(script);
    const result = await p.page.evaluate(() => ({focal:window.__camera.projection.focalLength, fov:window.__camera.projection.fieldOfView}));
    assert.ok(Math.abs(result.focal - 250) < 1e-8);
    assert.equal(result.fov, 90);
    assert.ok(Math.abs(await p.page.evaluate(() => window.__box.transform.perspectiveProjection.focalLength) - 150) < 1e-8);
    assert.ok((await p.pixel(304,238))[3] > 240);
    const host = await createBrowserHost();
    try {
        const fallback = await host.page(800,600,1,true);
        try {
            await fallback.script(script);
            assert.deepEqual(await fallback.pixel(304,238), await p.pixel(304,238));
            assert.deepEqual(await fallback.pixel(260,210), await p.pixel(260,210));
            assert.deepEqual(fallback.errors, []);
            assert.deepEqual(await fallback.page.evaluate(() => window.__messages.filter(m=>m.type==='error')), []);
        } finally { await fallback.page.close(); }
    } finally { await host.close(); }
});

test('P62 transform赋值复制投影参数，独立输入与绑定投影不共享更新', async p => {
    await p.script(shape+'var input=clone($.root.transform.perspectiveProjection);input.fieldOfView=55;box.transform.perspectiveProjection=input;input.fieldOfView=90;window.__input=input;window.__bound=box.transform.perspectiveProjection;');
    const result = await p.page.evaluate(() => ({input:window.__input.focalLength,bound:window.__bound.focalLength,fov:window.__bound.fieldOfView}));
    assert.deepEqual(result, {input:250/Math.tan(Math.PI/4),bound:stageFocal,fov:55});
    await p.update('window.__bound.fieldOfView=90;var center=window.__bound.projectionCenter;center.x=0;');
    assert.ok(Math.abs(await p.page.evaluate(() => window.__bound.focalLength) - 150) < 1e-8);
    assert.deepEqual(await p.page.evaluate(() => window.__bound.projectionCenter), {x:400,y:300});
});

test('P63 逐帧赋回相同投影参数复用静态3D子树，参数改变才重建', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:400,y:300,z:100});var box=$.createShape({lifeTime:20,parent:group,rotationY:45});box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,40,40);box.graphics.endFill();var projection=clone($.root.transform.perspectiveProjection);projection.fieldOfView=55;group.transform.perspectiveProjection=projection;window.__group=group;window.__input=projection;');
    const rebuilt = await p.page.evaluate(async () => {
        const {hostState} = await import('/script-danmaku/core.js');
        window.__group.transform.perspectiveProjection = window.__input;
        hostState.dirty = true;
        window.__step(0);
        return window.__group.rebuiltThisFrame;
    });
    assert.equal(rebuilt, false);
    await p.update('window.__input.fieldOfView=90;window.__group.transform.perspectiveProjection=window.__input;');
    assert.equal(await p.page.evaluate(() => window.__group.rebuiltThisFrame), true);
});

test('P64 原版窗口到全屏的实测焦距保持固定，中心及深度像素随尺寸更新', async p => {
    const host = await createBrowserHost();
    try {
        const fallback = await host.page(680,502,1,true);
        try {
            // 原版输出：窗口 player=680×502、center=(340,251)，全屏
            // player=1360×768、center=(680,384)，两者 focal=288.147308。
            // z 取实测焦距，白色矩形应缩为一半；不依赖生产投影函数算期望像素。
            for (const probe of [p, fallback]) {
                await probe.page.setViewportSize({width:680,height:502});
                await probe.page.waitForFunction(() => document.querySelector('#stage canvas').width === 680, null, {polling:50});
                await probe.script(shape+'box.z=288.1473083496094;window.__root=$.root;');
                const snapshot = () => probe.page.evaluate(() => {
                    const projection = window.__root.transform.perspectiveProjection;
                    return {focal:projection.focalLength,fov:projection.fieldOfView,center:projection.projectionCenter};
                });
                const windowed = await snapshot();
                assert.ok(Math.abs(windowed.focal - 288.1473083496094) < 0.00002);
                assert.equal(windowed.fov,55);
                assert.deepEqual(windowed.center,{x:340,y:251});
                assert.deepEqual(await probe.pixel(230,185),[255,255,255,255]);
                assert.equal((await probe.pixel(245,185))[3],0);
                await probe.page.setViewportSize({width:1360,height:768});
                await probe.page.waitForFunction(() => document.querySelector('#stage canvas').width === 1360, null, {polling:50});
                await probe.update('scriptDanmakuHost.resize();');
                const fullscreen = await snapshot();
                assert.equal(fullscreen.focal,windowed.focal);
                assert.equal(fullscreen.fov,55);
                assert.deepEqual(fullscreen.center,{x:680,y:384});
                assert.deepEqual(await probe.pixel(400,250),[255,255,255,255]);
                assert.equal((await probe.pixel(415,250))[3],0);
                assert.equal((await probe.pixel(230,185))[3],0);
            }
            assert.deepEqual(fallback.errors,[]);
            assert.deepEqual(await fallback.page.evaluate(() => window.__messages.filter(m=>m.type==='error')),[]);
        } finally { await fallback.page.close(); }
    } finally { await host.close(); }
});

test('P65 移除根显式投影后，默认投影保持原版焦距和深度缩放', async p => {
    await p.script(shape+'box.z=288.1473083496094;$.root.transform.perspectiveProjection=null;');
    assert.deepEqual(await p.pixel(260,210),[255,255,255,255]);
    assert.equal((await p.pixel(275,210))[3],0);
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
