'use strict';
// 真浏览器验证缓存复用和上传尺寸，同时检查像素，避免性能优化冻结画面。
const assert = require('node:assert/strict');
const { createBrowserHost } = require('./browser-host');
const cases = [];
const shape = 'var box=$.createShape({lifeTime:20,x:100,y:100});box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,40,40);box.graphics.endFill();window.__box=box;';
function test(name, run) { cases.push({ name, run }); }
async function version(p, field = 'cacheCanvas') {
    return p.page.evaluate(field => window.__box[field].__m8RasterVersion, field);
}
async function uploads(p) {
    return p.page.evaluate(() => window.__uploads.splice(0));
}
async function observeUploads(p) {
    await p.page.evaluate(() => {
        window.__uploads = [];
        const original = WebGLRenderingContext.prototype.texImage2D;
        WebGLRenderingContext.prototype.texImage2D = function (...args) {
            if (args.length === 6) window.__uploads.push([args[5].width, args[5].height]);
            return original.apply(this, args);
        };
    });
}

test('同值标量和新建的相同 Blur/Glow 不重绘，滤镜修改仍刷新', async p => {
    await p.script(shape + 'box.filters=[$.createBlurFilter(0,0,1),$.createGlowFilter(0xff0000,1,8,8,2,1)];window.__display=$;');
    const before = await version(p);
    const pixel = await p.pixel(120,120);
    await p.update('__box.x=100;__box.y=100;__box.visible=true;__box.alpha=1;__box.filters=[{quality:1,blurY:0,blurX:0,kind:"BlurFilter",type:"BlurFilter"},__display.createGlowFilter(0xff0000,1,8,8,2,1)];');
    assert.equal(await version(p), before);
    assert.deepEqual(await p.pixel(120,120), pixel);
    await p.update('__box.filters[0].blurX=24;__box.filters=__box.filters;');
    assert.notEqual(await version(p), before);
    assert.ok((await p.pixel(92,120))[3] > 0);
    const changed = await version(p);
    await p.update('__box.filters[1].color=0x00ff00;__box.filters=__box.filters;');
    assert.notEqual(await version(p), changed);
    const glow = await p.pixel(120,97);
    assert.ok(glow[1] > glow[0] && glow[1] > glow[2]);
});

test('原地修改复杂滤镜矩阵并重新赋值仍刷新位图', async p => {
    await p.script(shape + 'box.filters=[$.createColorMatrixFilter()];');
    await p.update('__box.filters[0].matrix[0]=0;__box.filters[0].matrix[12]=0;__box.filters=__box.filters;');
    assert.deepEqual(await p.pixel(120,120),[0,255,0,255]);
});

test('3D 平面容器移动复用内容，子节点移动则重建合成', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:400,y:300,z:100,rotationY:20});' + shape + 'group.addChild(box);box.transform.matrix3D=null;box.x=0;box.y=0;window.__child=box;window.__box=group;');
    const before = await version(p,'composite');
    const pixel = await p.pixel(405,310);
    assert.ok(pixel[3] > 240);
    await p.update('__box.x+=30;');
    assert.equal(await version(p,'composite'),before);
    assert.equal((await p.pixel(405,310))[3],0);
    assert.ok((await p.pixel(430,310))[3] > 240);
    await p.update('__child.x+=10;');
    assert.notEqual(await version(p,'composite'),before);
});

const large = 'var box=$.createShape({lifeTime:20,x:-1500,y:-1500,rotationY:1});box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,4096,4096);box.graphics.endFill();box.graphics.beginFill(0xff0000);box.graphics.drawRect(1700,1700,200,100);box.graphics.endFill();window.__box=box;';
test('4096 方形纹理按可见区域上传，移动复用裁剪且内容修改不冻结', async p => {
    await observeUploads(p);
    await p.script(large);
    const first = await uploads(p);
    assert.ok(first.length > 0);
    assert.ok(first.every(([w,h]) => w*h < 4096*4096/4),JSON.stringify(first));
    assert.deepEqual(await p.pixel(288,244),[255,0,0,255]);
    assert.deepEqual(await p.pixel(600,500),[255,255,255,255]);
    await p.update('__box.x+=1;');
    assert.deepEqual(await uploads(p),[]);
    assert.deepEqual(await p.pixel(289,244),[255,0,0,255]);
    await p.update('__box.graphics.clear();__box.graphics.beginFill(0x00ff00);__box.graphics.drawRect(0,0,4096,4096);__box.graphics.endFill();');
    assert.ok((await uploads(p)).length > 0);
    assert.deepEqual(await p.pixel(289,244),[0,255,0,255]);
    await p.update('__box.x=-15000;');
    assert.deepEqual(await uploads(p),[]);
    assert.equal((await p.pixel(289,244))[3],0);
});

test('穿过近裁剪面的巨型平面仍保留可见像素，视口变化重新裁剪', async p => {
    await observeUploads(p);
    await p.script('var box=$.createShape({lifeTime:20,x:400,rotationY:80});box.graphics.beginFill(0x00ff00);box.graphics.drawRect(0,0,4096,4096);box.graphics.endFill();window.__box=box;');
    assert.deepEqual(await p.pixel(404,300),[0,255,0,255]);
    assert.ok((await uploads(p)).every(([w,h]) => w*h < 4096*4096/4));
    await p.page.setViewportSize({width:1200,height:800});
    await p.page.waitForFunction(() => document.querySelector('#stage canvas').width === 1200,null,{polling:50});
    await p.update('scriptDanmakuHost.resize();');
    assert.deepEqual(await p.pixel(389,293),[0,255,0,255]);
});

test('裁剪的大位图与相同图案的小位图保留半透明和条纹像素', async p => {
    await p.script('var large=$.createShape({lifeTime:20,x:-1500,y:-1500,rotationY:1});var small=$.createShape({lifeTime:20,x:-1500,y:-1500,rotationY:1,visible:false});large.graphics.beginFill(0,0);large.graphics.drawRect(0,0,4092,4092);large.graphics.endFill();[large,small].forEach(function(box){for(var i=0;i<30;i++){box.graphics.beginFill(i%2?0x00ff00:0xff0000,0.5);box.graphics.drawRect(1700+i*6,1700,3,100);box.graphics.endFill();}});window.__large=large;window.__small=small;');
    await p.page.evaluate(() => {
        const c=document.querySelector('#stage canvas');
        window.__largePixels=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
    });
    await p.update('__large.visible=false;__small.visible=true;');
    const mismatch=await p.page.evaluate(() => {
        const c=document.querySelector('#stage canvas'), data=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
        let alphaMax=0,interiorMax=0,total=0;
        for(let i=0;i<data.length;i+=4) {
            alphaMax=Math.max(alphaMax,Math.abs(data[i+3]-__largePixels[i+3]));
            for(let channel=0;channel<3;channel++) {
                const delta=Math.abs(data[i+channel]-__largePixels[i+channel]);total+=delta;
                if(Math.min(data[i+3],__largePixels[i+3])>=64)interiorMax=Math.max(interiorMax,delta);
            }
        }
        return {alphaMax,interiorMax,mean:total/(c.width*c.height*3)};
    });
    // 离屏表面的边界不同，低 alpha 抗锯齿经反预乘后 RGB 会放大舍入误差。
    // 检查覆盖率、条纹主体和整幅误差，而不是要求这些边缘 RGB 完全相等。
    // alpha 为 64 时，1 级预乘舍入可对应最多 255/64≈4 级 RGB 差异。
    assert.ok(mismatch.alphaMax<=1 && mismatch.interiorMax<=4 && mismatch.mean<0.001,JSON.stringify(mismatch));
});

test('两张总计超过 64 MiB 的静态纹理在后续帧复用，颜色不串图', async p => {
    await observeUploads(p);
    await p.page.evaluate(async () => {
        const geometry=await import('/script-danmaku/geometry.js');
        const textures=[0xff0000,0x0000ff].map(color => {
            const c=document.createElement('canvas');c.width=4096;c.height=2200;
            const context=c.getContext('2d');context.fillStyle='#'+color.toString(16).padStart(6,'0');context.fillRect(0,0,c.width,c.height);
            c.__m8RasterVersion=1;return c;
        });
        const matrix=geometry.IDENTITY.slice(), angle=10*Math.PI/180;
        matrix[0]=matrix[10]=Math.cos(angle);matrix[2]=-Math.sin(angle);matrix[8]=Math.sin(angle);
        const projection={focalLength:288.1473083496094,projectionCenter:{x:400,y:300}};
        const context=document.querySelector('#stage canvas').getContext('2d');
        window.__drawTextures=() => {
            context.clearRect(0,0,800,600);
            geometry.beginProjectedBatch(context);
            textures.forEach((source,i) => geometry.drawProjected(context,source,{x:360+i*120,y:260,width:80,height:80},matrix,projection));
            geometry.endProjectedBatch(context);
        };
        window.__drawTextures();
    });
    assert.equal((await uploads(p)).length,2);
    assert.deepEqual(await p.pixel(400,300),[255,0,0,255]);
    assert.deepEqual(await p.pixel(540,300),[0,0,255,255]);
    await p.page.evaluate(() => window.__drawTextures());
    assert.deepEqual(await uploads(p),[]);
    assert.deepEqual(await p.pixel(400,300),[255,0,0,255]);
    assert.deepEqual(await p.pixel(540,300),[0,0,255,255]);
});

test('持续更新的大位图不额外复制裁剪块，稳定后恢复裁剪', async p => {
    await observeUploads(p);
    await p.page.evaluate(() => {
        window.__cropCopies=0;
        const original=CanvasRenderingContext2D.prototype.drawImage;
        CanvasRenderingContext2D.prototype.drawImage=function(...args) {
            if(window.__box && args[0]===__box.cacheCanvas && args.length===9)window.__cropCopies++;
            return original.apply(this,args);
        };
    });
    await p.script(large);
    await uploads(p);
    await p.page.evaluate(() => window.__cropCopies=0);
    for(let i=0;i<3;i++) await p.update('__box.graphics.clear();__box.graphics.beginFill(0x00ff00);__box.graphics.drawRect(0,0,4096,4096);__box.graphics.endFill();');
    assert.equal(await p.page.evaluate(() => window.__cropCopies),0);
    assert.equal((await uploads(p)).filter(([w,h]) => w===4096 && h===4096).length,3);
    assert.deepEqual(await p.pixel(288,244),[0,255,0,255]);
    await p.page.evaluate(async () => {
        const g=await import('/script-danmaku/geometry.js');
        const source=__box.cacheCanvas,context=document.querySelector('#stage canvas').getContext('2d');
        source.__m8RasterVersion++;
        const draw=() => g.drawProjected(context,source,__box.cacheBounds,g.worldMatrix(__box),g.projectionFor(__box));
        draw();window.__step(8);draw();
    });
    assert.equal(await p.page.evaluate(() => window.__cropCopies),0);
    await uploads(p);
    await p.update('__box.x+=1;window.__step(16);');
    assert.equal(await p.page.evaluate(() => window.__cropCopies),1);
    assert.ok((await uploads(p)).every(([w,h]) => w*h<4096*4096/4));
    assert.deepEqual(await p.pixel(289,244),[0,255,0,255]);
});

async function run() {
    const host = await createBrowserHost();
    try {
        for (const item of cases) {
            const p = await host.page();
            try {
                await item.run(p);
                assert.deepEqual(p.errors,[]);
                assert.deepEqual(await p.page.evaluate(() => __messages.filter(m => m.type === 'error')),[]);
                console.log('ok ' + item.name);
            } catch (error) { console.error('FAIL ' + item.name + ': ' + error.stack); process.exitCode = 1; }
            finally { await p.page.close(); }
        }
    } finally { await host.close(); }
}
run().catch(error => { console.error(error); process.exitCode = 1; });
