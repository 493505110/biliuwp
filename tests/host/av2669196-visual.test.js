'use strict';
// 确定性地逐帧推进 11 条原样脚本，不用倍速或 seek 跳过中间状态。
// NODE_PATH 指向已有 Playwright；M8_ARTIFACT_DIR 指定抓帧输出目录。
// M8_BASE_REVISION=HEAD 可对照修改前代码；M8_SAMPLE_SECONDS 可缩小采样范围。
// M8_MEASURE_SECOND 记录从指定秒开始的60帧WebGL上传量和Canvas拷贝数。
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { createBrowserHost } = require('./browser-host');
const directory = path.join(__dirname, 'fixtures/real');
const fixtures = fs.readdirSync(directory).filter(name => name.endsWith('.js')).sort().map(name => ({
    id: name, stime: 0, duration: 333, lang: 'js', code: fs.readFileSync(path.join(directory, name), 'utf8')
}));
const seconds = (process.env.M8_SAMPLE_SECONDS || '0,10,30,40,60,90,105,110,115,119,121,122,123,125,130,135,142,180,240,300,332,333').split(',').map(Number).sort((a, b) => a - b);
const startSecond = Number(process.env.M8_START_SECOND || 0);
const measureFrame = process.env.M8_MEASURE_SECOND === undefined ? NaN : Math.round(Number(process.env.M8_MEASURE_SECOND) * 60);

async function run() {
    const host = await createBrowserHost(process.env.M8_BASE_REVISION);
    process.on('SIGINT', async () => { await host.close(); process.exit(130); });
    const label = process.env.M8_BASE_REVISION ? 'baseline' : 'current';
    try {
        const { page, errors } = await host.page(Number(process.env.M8_VIEWPORT_WIDTH || 1280), Number(process.env.M8_VIEWPORT_HEIGHT || 720), Number(process.env.M8_SEED || 1));
        console.log(label + ': browser ready');
        if (process.env.M8_PROFILE) {
            const profiler = await page.context().newCDPSession(page);
            await profiler.send('Profiler.enable'); await profiler.send('Profiler.start');
            setTimeout(async () => {
                const result = await profiler.send('Profiler.stop');
                fs.writeFileSync(process.env.M8_PROFILE, JSON.stringify(result));
                console.log('CPU profile saved');
            }, 20000).unref();
        }
        await page.evaluate(({ fixtures, startSecond }) => {
            scriptDanmakuHost.reset(startSecond, true, 1, true);
            scriptDanmakuHost.append(fixtures);
            window.__step(0);
        }, { fixtures, startSecond });
        console.log(label + ': initial frame prepared');
        if (Number.isFinite(measureFrame)) {
            await page.evaluate(() => {
                window.__measurement=null;
                function wrap(proto,name,observe) {
                    const original=proto[name];
                    proto[name]=function(...args) { if(window.__measurement)observe(window.__measurement,args);return original.apply(this,args); };
                }
                wrap(WebGLRenderingContext.prototype,'texImage2D',(s,args) => {
                    s.uploads++;
                    if(args.length===6) {
                        const source=args[5],size=source.width+'x'+source.height;
                        s.uploadBytes+=source.width*source.height*4;
                        s.sourceSizes[size]=(s.sourceSizes[size]||0)+1;
                    }
                });
                wrap(WebGLRenderingContext.prototype,'drawArrays',s => s.gpuDraws++);
                wrap(CanvasRenderingContext2D.prototype,'drawImage',s => s.canvasCopies++);
            });
        }
        if (process.env.M8_DIAG) {
            console.log(await page.evaluate(async () => {
                const { hostState } = await import('/script-danmaku/core.js');
                const sizes = hostState.elements.map(e => ({ id:e.id,kind:e.kind,visible:e.visible,children:e.childList.length,
                    cache:e.cacheCanvas ? [e.cacheCanvas.width,e.cacheCanvas.height]:null,
                    composite:e.composite ? [e.composite.width,e.composite.height]:null,
                    filters:e.filters,projected:e.projectedComposite }));
                return JSON.stringify({ count:sizes.length,top:hostState.rootElement.childList.length,
                    largest:sizes.sort((a,b)=>((b.composite||b.cache||[0,0])[0]*(b.composite||b.cache||[0,0])[1])-((a.composite||a.cache||[0,0])[0]*(a.composite||a.cache||[0,0])[1])).slice(0,12) });
            }));
        }
        let frame = Math.round(startSecond * 60);
        const startFrame = frame;
        const startedAt = Date.now();
        const rows = [];
        for (const second of seconds) {
            const target = Math.round(second * 60);
            while (frame < target) {
                if(frame===measureFrame) await page.evaluate(async () => {
                    const {hostState}=await import('/script-danmaku/core.js');
                    window.__measurement={uploads:0,uploadBytes:0,gpuDraws:0,canvasCopies:0,sourceSizes:{},paintStart:hostState.paintCount};
                });
                let count = Math.min(60, target - frame);
                for(const boundary of [measureFrame,measureFrame+60]) if(boundary>frame)count=Math.min(count,boundary-frame);
                await page.evaluate(({ count, frame, startFrame }) => {
                    for (let i = 1; i <= count; i++) window.__stepTo((frame + i - startFrame) * 1000 / 60);
                }, { count, frame, startFrame });
                frame += count;
                if(frame===measureFrame+60) {
                    const measured=await page.evaluate(async () => {
                        const {hostState}=await import('/script-danmaku/core.js');
                        document.querySelector('#stage canvas').getContext('2d').getImageData(0,0,1,1);
                        const value=window.__measurement;
                        value.paintDelta=hostState.paintCount-value.paintStart;delete value.paintStart;
                        window.__measurement=null;return value;
                    });
                    const result={second:measureFrame/60,frames:60,...measured};
                    console.log('performance: '+JSON.stringify(result));
                    if(process.env.M8_ARTIFACT_DIR) {
                        fs.mkdirSync(process.env.M8_ARTIFACT_DIR,{recursive:true});
                        fs.writeFileSync(path.join(process.env.M8_ARTIFACT_DIR,label+'-performance.json'),JSON.stringify(result,null,2));
                    }
                }
                if (frame % 600 === 0) console.log(label + ' advanced ' + frame / 60 + 's');
            }
            const result = await page.evaluate(() => {
                const source = document.querySelector('#stage canvas');
                const sourcePixels = source.getContext('2d').getImageData(0, 0, source.width, source.height).data;
                let opaque = 0;
                for (let i = 3; i < sourcePixels.length; i += 4) if (sourcePixels[i]) opaque++;
                const output = document.createElement('canvas'); output.width = source.width; output.height = source.height;
                const context = output.getContext('2d'); context.fillStyle = '#000'; context.fillRect(0, 0, output.width, output.height); context.drawImage(source, 0, 0);
                const data = context.getImageData(0, 0, output.width, output.height).data;
                let bright = 0, total = 0;
                for (let i = 0; i < data.length; i += 4) {
                    const gray = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
                    total += gray; if (gray > 60) bright++;
                }
                return { brightRatio: bright / (data.length / 4), mean: total / (data.length / 4), opaquePixels: opaque, png: output.toDataURL() };
            });
            if (process.env.M8_ARTIFACT_DIR) {
                fs.mkdirSync(process.env.M8_ARTIFACT_DIR, { recursive: true });
                fs.writeFileSync(path.join(process.env.M8_ARTIFACT_DIR, label + '-' + second + '.png'), Buffer.from(result.png.split(',')[1], 'base64'));
            }
            delete result.png;
            if ([105, 110, 115, 119, 121].includes(second)) assert.ok(result.mean > 1, '原作中段关键帧不得变为空白：' + second);
            if (second === 333) assert.equal(result.opaquePixels, 0, '脚本结束后画布不得残留滤镜或投影像素');
            rows.push({ second, ...result }); console.log(JSON.stringify(rows.at(-1)));
        }
        const bridgeErrors = await page.evaluate(() => window.__messages.filter(m => m.type === 'error'));
        assert.deepEqual(errors, []); assert.deepEqual(bridgeErrors, []);
        if (process.env.M8_ARTIFACT_DIR) fs.writeFileSync(path.join(process.env.M8_ARTIFACT_DIR, label + '.json'), JSON.stringify(rows, null, 2));
        console.log(JSON.stringify({ startSecond, endSecond: frame / 60, frames: frame - startFrame, elapsedMs: Date.now() - startedAt }));
        console.log(label + ': all 11 scripts completed without errors');
    } finally { await host.close(); }
}
run().catch(error => { console.error(error); process.exitCode = 1; });
