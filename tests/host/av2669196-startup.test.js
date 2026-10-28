'use strict';

// 原样执行全部11条脚本，逐帧经过终端打字段与报幕切换。
// NODE_PATH 指向 Playwright/pngjs；M8_BASE_REVISION=HEAD 对照修改前的实际截图。
// M8_BASELINE_DIR 可复用已保存的 baseline-startup.json 与截图，避免重复跑基线。
// M8_ARTIFACT_DIR 可保存截图和计数；耗时必须连同实际 WebGL 设备一起解读。
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { PNG } = require('pngjs');
const { createBrowserHost } = require('./browser-host');
const directory = path.join(__dirname, 'fixtures/real');
const fixtures = fs.readdirSync(directory).filter(name => name.endsWith('.js')).sort().map(name => ({
    id:name,stime:0,duration:333,lang:'js',code:fs.readFileSync(path.join(directory,name),'utf8')
}));
const seconds = [0.5,1,2,3,3.5,3.75,4];

async function sample(revision, label) {
    const host = await createBrowserHost(revision);
    try {
        const p = await host.page(1280,720,1,false,true);
        const device = await p.page.evaluate(() => {
            const gl = document.createElement('canvas').getContext('webgl');
            if (!gl) return 'Canvas fallback';
            const info = gl.getExtension('WEBGL_debug_renderer_info');
            return gl.getParameter(info ? info.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
        });
        console.log(label + ' device: ' + device);
        await p.page.evaluate(() => {
            window.__startup = {sourceUploads:0,sourceUploadBytes:0,canvasCopies:0};
            const upload=WebGLRenderingContext.prototype.texImage2D;
            WebGLRenderingContext.prototype.texImage2D=function(...args) {
                if(args.length===6) {
                    __startup.sourceUploads++;
                    __startup.sourceUploadBytes+=args[5].width*args[5].height*4;
                }
                return upload.apply(this,args);
            };
            const copy=CanvasRenderingContext2D.prototype.drawImage;
            CanvasRenderingContext2D.prototype.drawImage=function(...args) {
                __startup.canvasCopies++; return copy.apply(this,args);
            };
        });
        const initialAt=Date.now();
        await p.page.evaluate(fixtures => {
            scriptDanmakuHost.reset(0,true,1,true);
            scriptDanmakuHost.append(fixtures); __step(0);
        },fixtures);
        const result = {device,initialMs:Date.now()-initialAt,rows:[],images:[]};
        let frame=0;
        for(const second of seconds) {
            const target=Math.round(second*60);
            const timings=await p.page.evaluate(({frame,target}) => {
                const times=[];
                for(let i=frame+1;i<=target;i++) {
                    const t=Date.now();__stepTo(i*1000/60);times.push(Date.now()-t);
                }
                return times;
            },{frame,target});
            frame=target;
            const counts=await p.page.evaluate(async () => {
                const {hostState}=await import('/script-danmaku/core.js');
                const shapes=hostState.elements.filter(e=>e.kind==='shape'&&e.cacheCanvas
                    &&e.shapeItems.length&&e.shapeItems.every(item=>item.kind==='path'));
                return {...__startup,paints:hostState.paintCount,shapeCount:shapes.length,
                    shapeCanvases:new Set(shapes.map(e=>e.cacheCanvas)).size};
            });
            const png=await p.page.screenshot();
            const decoded=PNG.sync.read(png);
            // 原作开头持续有背景和终端内容，不能靠漏画得到更低计数。
            let covered=0;
            for(let i=3;i<decoded.data.length;i+=4) if(decoded.data[i]) covered++;
            assert.ok(covered>1280*720/2,'开头不得变为空白：'+second);
            const row={second,stepMs:timings.reduce((a,b)=>a+b,0),maxStepMs:Math.max(...timings),...counts};
            result.rows.push(row);result.images.push(decoded.data);
            console.log(label+' '+JSON.stringify(row));
            if(process.env.M8_ARTIFACT_DIR) {
                fs.mkdirSync(process.env.M8_ARTIFACT_DIR,{recursive:true});
                fs.writeFileSync(path.join(process.env.M8_ARTIFACT_DIR,label+'-startup-'+second+'.png'),png);
            }
        }
        assert.deepEqual(p.errors,[]);
        assert.deepEqual(await p.page.evaluate(()=>__messages.filter(m=>m.type==='error')),[]);
        if(!revision) {
            const first=result.rows[0];
            assert.ok(first.shapeCanvases<first.shapeCount/2,'重复字形应复用栅格：'+JSON.stringify(first));
        }
        if(process.env.M8_ARTIFACT_DIR) {
            fs.writeFileSync(path.join(process.env.M8_ARTIFACT_DIR,label+'-startup.json'),JSON.stringify({
                device:result.device,initialMs:result.initialMs,rows:result.rows
            },null,2));
        }
        return result;
    } finally { await host.close(); }
}

(async () => {
    const baselineDirectory=process.env.M8_BASELINE_DIR;
    const baseline=baselineDirectory ? {
        ...JSON.parse(fs.readFileSync(path.join(baselineDirectory,'baseline-startup.json'),'utf8')),
        images:seconds.map(second=>PNG.sync.read(fs.readFileSync(path.join(baselineDirectory,'baseline-startup-'+second+'.png'))).data)
    } : process.env.M8_BASE_REVISION ? await sample(process.env.M8_BASE_REVISION,'baseline') : null;
    const current=await sample(null,'current');
    if(baseline) {
        assert.equal(current.device,baseline.device,'基线与当前必须使用同一渲染设备');
        for(let i=0;i<seconds.length;i++) {
            assert.deepEqual(current.images[i],baseline.images[i],'开头截图必须逐像素一致：'+seconds[i]);
        }
        assert.ok(current.rows.at(-1).paints<baseline.rows.at(-1).paints/2,'重复字形的实际栅格重建次数应减少');
        assert.ok(current.rows.at(-1).canvasCopies<baseline.rows.at(-1).canvasCopies/2,'未变化的文字行不应重复逐字合成');
        console.log('baseline/current: all '+seconds.length+' startup screenshots are pixel-identical');
    }
    console.log('av2669196 startup passed');
})().catch(error=>{console.error(error);process.exitCode=1;});
