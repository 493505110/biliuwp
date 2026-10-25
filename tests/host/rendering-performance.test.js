'use strict';
// 真浏览器验证缓存复用和上传尺寸，同时检查像素，避免性能优化冻结画面。
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
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
        window.__filterAllocations = 0;
        const original = WebGLRenderingContext.prototype.texImage2D;
        WebGLRenderingContext.prototype.texImage2D = function (...args) {
            if (args.length === 6) window.__uploads.push([args[5].width, args[5].height]);
            if (args.length === 9 && args[8] === null) window.__filterAllocations++;
            return original.apply(this, args);
        };
    });
}

async function paints(p) {
    return p.page.evaluate(async () => (await import('/script-danmaku/core.js')).hostState.paintCount);
}

async function observeShaderPreparation(p) {
    await p.page.evaluate(() => {
        window.__shaderPreparation = [];
        for (const method of ['compileShader','linkProgram','getShaderParameter','getProgramParameter']) {
            const original = WebGLRenderingContext.prototype[method];
            WebGLRenderingContext.prototype[method] = function (...args) {
                __shaderPreparation.push(method);
                return original.apply(this,args);
            };
        }
    });
}

test('模糊核跨越全部档位时不在绘制帧编译或同步查询着色器', async p => {
    await observeShaderPreparation(p);
    await p.script(shape + 'window.__display=$;');
    for (const radius of [3,7,15,31,63,127,255,31,0]) {
        await p.update('__box.filters=[__display.createBlurFilter('+radius+',0,1)];');
        assert.ok((await p.pixel(120,120))[3]>0,'滤镜结果应持续可见');
    }
    assert.deepEqual(await p.page.evaluate(()=>__shaderPreparation),[]);
});

test('DECORATOR原样脚本37秒文字切换不触发同步着色器准备', async p => {
    await p.page.setViewportSize({width:1280,height:720});
    const directory=path.join(__dirname,'fixtures/real');
    const fixtures=fs.readdirSync(directory).filter(name=>name.endsWith('.js')).sort().map(name=>({
        id:name,stime:0,duration:333,lang:'js',code:fs.readFileSync(path.join(directory,name),'utf8')
    }));
    assert.equal(fixtures.length,11);
    await p.page.evaluate(fixtures=>{
        scriptDanmakuHost.reset(36,true,1,true);scriptDanmakuHost.append(fixtures);__step(0);
    },fixtures);
    await observeShaderPreparation(p);
    await observeUploads(p);
    await p.page.evaluate(()=>{for(let i=1;i<=180;i++)__stepTo(i*1000/60);});
    assert.deepEqual(await p.page.evaluate(()=>__shaderPreparation),[]);
    const textures=await uploads(p);
    assert.ok(textures.every(([w,h])=>w*h<3*1024*1024),'切换不能再上传4096px巨型模糊缓存: '+JSON.stringify(textures));
    assert.equal(await p.page.evaluate(()=>__filterAllocations),0,'切换期间不能扩容滤镜工作缓冲');
    const bright=await p.page.evaluate(()=>{
        const c=document.querySelector('#stage canvas'),data=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
        let count=0;for(let i=0;i<data.length;i+=4)if(data[i]+data[i+1]+data[i+2]>180)count++;
        return count;
    });
    assert.ok(bright>10000,'切换后的文字与背景仍须实际绘制');
});

test('相机背后的平面容器不烘焙子树，内容修改后重新入场仍显示最新像素', async p => {
    await observeUploads(p);
    await p.script('var group=$.createCanvas({lifeTime:20,x:400,y:300,z:-1000,rotationY:15});'
        + 'var box=$.createShape({lifeTime:20,parent:group});box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,40,40);box.graphics.endFill();'
        + 'group.filters=[$.createBlurFilter(3,3,1)];window.__box=box;window.__group=group;');
    assert.equal(await p.page.evaluate(()=>__group.composite),null);
    assert.equal(await p.page.evaluate(()=>__box.cacheCanvas),null);
    assert.deepEqual(await uploads(p),[]);
    await p.update('__box.graphics.clear();__box.graphics.beginFill(0xff0000);__box.graphics.drawRect(0,0,80,40);__box.graphics.endFill();');
    assert.equal(await p.page.evaluate(()=>__box.cacheCanvas),null);
    await p.update('__group.z=0;');
    assert.deepEqual(await p.pixel(410,320),[255,0,0,255]);
    assert.ok((await uploads(p)).length>0);
    assert.ok(await p.page.evaluate(()=>!Object.keys(__group).some(k=>k==='compositeFullBounds'||k==='compositeCrop')));
});

test('巨型平面在滤镜前裁剪输入，相机移动补烘新区域且静止时复用缓存', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:400,y:300,rotationY:1});'
        + 'var box=$.createShape({lifeTime:20,parent:group});box.graphics.beginFill(0xff0000);box.graphics.drawRect(-2000,-500,4000,1000);box.graphics.endFill();'
        + 'box.graphics.beginFill(0x0000ff);box.graphics.drawRect(0,0,100,100);box.graphics.endFill();'
        + 'group.filters=[$.createBlurFilter(3,3,1)];window.__box=group;');
    assert.ok(await p.page.evaluate(()=>!!__box.compositeCrop));
    assert.ok(await p.page.evaluate(()=>__box.composite.width*__box.composite.height<1024*1024));
    assert.deepEqual(await p.pixel(420,320),[0,0,255,255]);
    const before=await version(p,'composite');
    await p.update('');
    assert.equal(await version(p,'composite'),before);
    await p.update('__box.x=700;');
    assert.notEqual(await version(p,'composite'),before);
    assert.deepEqual(await p.pixel(420,320),[255,0,0,255]);
    assert.deepEqual(await p.pixel(720,320),[0,0,255,255]);
    await p.update('__box.rotationY=0;');
    assert.equal(await p.page.evaluate(()=>__box.compositeCrop),null);
    assert.deepEqual(await p.pixel(720,320),[0,0,255,255]);
});

test('Akari 每帧重复赋相同变换不重建父层，原地修改仍更新像素', async p => {
    await p.script(shape + 'var group=$.createCanvas({lifeTime:20});group.addChild(box);'
        + 'box.transform.matrix=$.createMatrix(1,0,0,1,100,100);'
        + 'box.transform.colorTransform=$.createColorTransform(1,0,0,1);window.__display=$;');
    const before = await paints(p);
    for (let i=0;i<10;i++) await p.update('__box.transform.matrix=__display.createMatrix(1,0,0,1,100,100);'
        + '__box.transform.matrix3D=null;__box.transform.colorTransform=__display.createColorTransform(1,0,0,1);');
    assert.equal(await paints(p), before, '相同值不得重新烘焙整棵子树');
    assert.deepEqual(await p.pixel(120,120), [255,0,0,255]);
    await p.update('var m=__box.transform.matrix;m.tx=200;__box.transform.matrix=m;');
    assert.equal((await p.pixel(120,120))[3],0);
    assert.deepEqual(await p.pixel(220,120),[255,0,0,255]);
    await p.update('var c=__box.transform.colorTransform;c.redMultiplier=0;c.greenMultiplier=1;__box.transform.colorTransform=c;');
    assert.deepEqual(await p.pixel(220,120),[0,255,0,255]);
    const changed = await paints(p);
    await p.update('__box.transform.matrix=__box.transform.matrix;__box.transform.colorTransform=__box.transform.colorTransform;');
    assert.equal(await paints(p),changed);
});

test('相同3D矩阵复用投影，rawData修改和标量修改不会被旧快照吞掉', async p => {
    await p.script(shape + 'window.__display=$;window.__matrix=$.createMatrix3D();'
        + '__matrix.appendTranslation(100,100,0);box.transform.matrix3D=__matrix;');
    const before = await paints(p);
    await p.update('__box.transform.matrix3D=__matrix.clone();');
    assert.equal(await paints(p),before);
    await p.update('var m=__box.transform.matrix3D;m.rawData[12]=200;__box.transform.matrix3D=m;');
    assert.equal((await p.pixel(120,120))[3],0);
    assert.deepEqual(await p.pixel(220,120),[255,255,255,255]);
    await p.update('__box.x=300;__box.transform.matrix3D=__matrix;');
    assert.equal((await p.pixel(320,120))[3],0);
    assert.deepEqual(await p.pixel(120,120),[255,255,255,255]);
    await p.update('__box.z=100;__box.transform.matrix3D=null;');
    assert.deepEqual(await p.page.evaluate(() => [__box.z,__box.rotationX,__box.rotationY]),[0,0,0]);
    assert.deepEqual(await p.pixel(120,120),[255,255,255,255]);
});

test('相同2D矩阵覆盖3D状态，清空矩阵后不会持续重合成', async p => {
    await p.script(shape + 'window.__display=$;box.transform.matrix=$.createMatrix(1,0,0,1,100,100);');
    await p.update('__box.z=100;__box.transform.matrix=__display.createMatrix(1,0,0,1,100,100);');
    assert.equal(await p.page.evaluate(() => __box.transform.matrix3D),null);
    assert.deepEqual(await p.pixel(120,120),[255,255,255,255]);
    await p.update('__box.transform.matrix=null;');
    const before = await paints(p);
    await p.update('__box.transform.matrix=null;__box.transform.matrix3D=null;__box.transform.colorTransform=null;');
    assert.equal(await paints(p),before);
});

test('Akari 原地复位同一矩阵后，相同位置和缩放仍与矩阵同步', async p => {
    await p.script(shape + 'window.__display=$;box.transform.matrix=$.createMatrix();');
    for (let frame = 0; frame < 3; frame++) {
        await p.update('var m=__box.transform.matrix;m.identity();__box.transform.matrix=m;'
            + '__box.x=200;__box.y=150;__box.scaleX=0.5;__box.scaleY=0.5;');
        assert.deepEqual(await p.page.evaluate(() => [__box.x,__box.y,__box.transform.matrix.tx,
            __box.transform.matrix.ty,__box.transform.matrix.a,__box.transform.matrix.d]), [200,150,200,150,0.5,0.5]);
        assert.deepEqual(await p.pixel(205,155), [255,255,255,255]);
        assert.equal((await p.pixel(105,105))[3], 0);
    }
});

test('连续 Blur/Glow 只上传一次并回贴一次，新增中间载体后单滤镜仍复用', async p => {
    await observeUploads(p);
    await p.page.evaluate(() => {
        window.__filterCopies=0;
        const draw=CanvasRenderingContext2D.prototype.drawImage;
        CanvasRenderingContext2D.prototype.drawImage=function(...args) {
            if (new Error().stack.includes('copyBack')) __filterCopies++;
            return draw.apply(this,args);
        };
    });
    await p.script(shape + 'window.__display=$;box.filters=[$.createBlurFilter(5,3,1),'
        + '$.createGlowFilter(0xff0000,1,8,8,2,1,true,false),$.createGlowFilter(0x00ff00,1,12,12,2,1)];');
    assert.equal((await uploads(p)).length,1);
    assert.equal(await p.page.evaluate(() => __filterCopies),1);
    assert.ok((await p.pixel(120,120))[3]>0);
    await p.update('__filterCopies=0;__box.filters=[__display.createBlurFilter(3,3,1)];');
    assert.equal((await uploads(p)).length,1);
    assert.equal(await p.page.evaluate(() => __filterCopies),1);
});

test('白色粒子的白色内发光不做恒等模糊，改为彩色后恢复完整滤镜', async p => {
    await p.page.evaluate(()=>{
        window.__gpuDraws=0;
        const draw=WebGLRenderingContext.prototype.drawArrays;
        WebGLRenderingContext.prototype.drawArrays=function(...args){__gpuDraws++;return draw.apply(this,args);};
    });
    await p.script(shape + 'box.filters=[$.createGlowFilter(0xffffff,1,8,8,2,1,true,false),'
        + '$.createGlowFilter(0xffffff,1,8,8,2,1,false,false)];');
    assert.equal(await p.page.evaluate(()=>__gpuDraws),3);
    assert.deepEqual(await p.pixel(120,120),[255,255,255,255]);
    await p.update('__gpuDraws=0;__box.graphics.clear();__box.graphics.beginFill(0xff0000);'
        + '__box.graphics.drawRect(0,0,40,40);__box.graphics.endFill();');
    assert.equal(await p.page.evaluate(()=>__gpuDraws),6);
    const edge=await p.pixel(101,120);
    assert.ok(edge[1]>0&&edge[2]>0,'彩色边缘仍须显示白色内发光：'+edge);
});

test('av2669196 原样脚本55秒歌词正常展开，65秒双发光不重复上传输入', async p => {
    await p.page.setViewportSize({width:1280,height:720});
    const directory=path.join(__dirname,'fixtures/real');
    const fixtures=fs.readdirSync(directory).filter(n=>n.endsWith('.js')).sort().map(n=>({
        id:n,stime:0,duration:333,lang:'js',code:fs.readFileSync(path.join(directory,n),'utf8')
    }));
    assert.equal(fixtures.length,11);
    await p.page.evaluate(fixtures=>{
        scriptDanmakuHost.reset(54,true,1,true);scriptDanmakuHost.append(fixtures);__step(0);
        for(let i=1;i<=60;i++)__stepTo(i*1000/60);
    },fixtures);
    const columns=await p.page.evaluate(()=>{
        const c=document.querySelector('#stage canvas'),ctx=c.getContext('2d');
        const data=ctx.getImageData(470,340,350,40).data;
        let occupied=0;
        for(let x=0;x<350;x++) {
            for(let y=0;y<40;y++) {
                const i=(y*350+x)*4;
                if(data[i]+data[i+1]+data[i+2]>180&&data[i+3]>60) {occupied++;break;}
            }
        }
        return occupied;
    });
    assert.ok(columns>200,'55秒歌词须横向展开，不能坍缩成几簇重叠文字：'+columns);
    await p.page.evaluate(fixtures=>{
        scriptDanmakuHost.reset(65,true,1,true);scriptDanmakuHost.append(fixtures);__step(0);
    },fixtures);
    await observeUploads(p);
    await p.page.evaluate(()=>{for(let i=1;i<=60;i++)__stepTo(1000+i*1000/60);});
    const textures=await uploads(p);
    const bytes=textures.reduce((total,[w,h])=>total+w*h*4,0);
    assert.ok(bytes<180*1024*1024,'65秒60帧上传量超出预算：'+bytes);
    const particles=await p.page.evaluate(async()=>{
        const {hostState}=await import('/script-danmaku/core.js');
        return hostState.elements.some(e=>e.childList.length===20&&e.props.filters
            &&e.props.filters.length===2&&e.props.filters.every(f=>f.kind==='GlowFilter'&&f.blurX===128)
            &&e.childList.every(c=>c.shapeItems&&c.shapeItems.length>0));
    });
    assert.equal(particles,true,'不能通过恢复旧版漏画lineTo来削减滤镜开销');
    console.log('av2669196 65秒60帧上传字节：'+bytes);
});

test('绘制缓存不跨脚本读操作或帧保留旧的祖先矩阵', async p => {
    await p.script('var group=$.createCanvas({lifeTime:20,x:50});' + shape + 'group.addChild(box);window.__group=group;window.__root=$.root;');
    const values = await p.page.evaluate(() => {
        const read=()=>__box.transform.getRelativeMatrix3D(__root).rawData[12];
        const first=read();__group.x=100;const moved=read();
        const m=__group.transform.matrix;m.tx=150;__group.transform.matrix=m;
        const mutated=read();return [first,moved,mutated];
    });
    assert.deepEqual(values,[150,200,250]);
    await p.update('');
    assert.equal((await p.pixel(170,120))[3],0);
    assert.deepEqual(await p.pixel(270,120),[255,255,255,255]);
    await p.update('__group.x=200;');
    assert.equal((await p.pixel(270,120))[3],0);
    assert.deepEqual(await p.pixel(320,120),[255,255,255,255]);
});

test('DECORATOR背面带滤镜粒子不分配大缓存，重新入场显示最新内容', async p => {
    await observeUploads(p);
    await p.script('var group=$.createCanvas({lifeTime:20});' + shape
        + 'group.addChild(box);box.z=-20000;box.rotationY=10;box.filters=[$.createBlurFilter(30,30,2)];window.__display=$;');
    assert.equal(await p.page.evaluate(() => __box.cacheCanvas),null);
    assert.deepEqual(await uploads(p),[]);
    await p.update('__box.graphics.clear();__box.graphics.beginFill(0xff0000);__box.graphics.drawRect(0,0,40,40);__box.graphics.endFill();');
    assert.equal(await p.page.evaluate(() => __box.cacheCanvas),null);
    assert.deepEqual(await uploads(p),[]);
    await p.update('__box.z=0;__box.rotationY=0;');
    const revealed = await p.pixel(120,120);
    assert.deepEqual(revealed.slice(0,3),[255,0,0]);
    assert.ok(revealed[3]>128, '模糊后的粒子重新入场仍可见');
    assert.ok((await uploads(p)).length>0);
    const versionBefore = await version(p);
    await p.update('__box.z=-20000;__box.rotationY=-10;__box.filters=[__display.createBlurFilter(60,60,2)];');
    assert.equal((await p.pixel(120,120))[3],0);
    assert.equal(await version(p),versionBefore);
    assert.deepEqual(await uploads(p),[]);
    await p.update('__box.z=0;__box.rotationY=0;');
    assert.notEqual(await version(p),versionBefore);
    assert.ok((await p.pixel(95,120))[3]>0);
});

test('DECORATOR原样11条脚本的90秒场景不再上传背面巨型纹理', async p => {
    await p.page.setViewportSize({width:1280,height:720});
    const directory=path.join(__dirname,'fixtures/real');
    const fixtures=fs.readdirSync(directory).filter(name=>name.endsWith('.js')).sort().map(name=>({
        id:name,stime:0,duration:333,lang:'js',code:fs.readFileSync(path.join(directory,name),'utf8')
    }));
    assert.equal(fixtures.length,11);
    await p.page.evaluate(fixtures=>{
        scriptDanmakuHost.reset(90,true,1,true);scriptDanmakuHost.append(fixtures);__step(0);
        window.__uploadBytes=0;
        const original=WebGLRenderingContext.prototype.texImage2D;
        WebGLRenderingContext.prototype.texImage2D=function(...args){
            if(args.length===6)__uploadBytes+=args[5].width*args[5].height*4;
            return original.apply(this,args);
        };
    },fixtures);
    // 原实现这60帧上传6.3GB；预算允许可见图元正常动画，阻止背面滤镜缓存回归。
    await p.page.evaluate(()=>{for(let i=0;i<60;i++)__step(1000/60);});
    const bytes=await p.page.evaluate(()=>__uploadBytes);
    assert.ok(bytes>0 && bytes<512*1024*1024,'60帧上传字节数: '+bytes);
    const bright=await p.page.evaluate(()=>{
        const c=document.querySelector('#stage canvas'),data=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
        let count=0;for(let i=0;i<data.length;i+=4)if(data[i]+data[i+1]+data[i+2]>180)count++;
        return count;
    });
    assert.ok(bright>10000,'原作字幕与粒子仍须实际绘制');
});

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
