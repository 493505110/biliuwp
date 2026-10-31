'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createBrowserHost } = require('./browser-host');

(async () => {
    const browser = await createBrowserHost(process.env.M8_BASE_REVISION);
    try {
        for (const part of ['easy', 'normal']) {
            const p = await browser.page(800, 600, 1, false, true);
            try {
                const main = JSON.parse(fs.readFileSync(path.join(__dirname,
                    'fixtures/av2785213', part, 'scripts.json'), 'utf8'))[3];
                const result = await p.page.evaluate(async ({main,part}) => {
                    let sequence = 0;
                    const timers = new Map();
                    // 与 VM 回归同样手动推进原始 setTimeout，保留原游戏所有回调。
                    window.setTimeout = (fn, delay) => {
                        timers.set(++sequence, {fn, at:performance.now()+delay}); return sequence;
                    };
                    window.clearTimeout = id => timers.delete(id);
                    function step(ms) {
                        __step(ms);
                        for (let guard=0;guard<100;guard++) {
                            const due=[...timers].filter(([,t])=>t.at<=performance.now());
                            if(!due.length)break;
                            for(const [id,t] of due){timers.delete(id);t.fn();}
                        }
                    }
                    const realNow=()=>Performance.prototype.now.call(performance);
                    const gl=document.createElement('canvas').getContext('webgl');
                    const extension=gl && gl.getExtension('WEBGL_debug_renderer_info');
                    const gpu=extension?gl.getParameter(extension.UNMASKED_RENDERER_WEBGL):'none';
                    scriptDanmakuHost.reset(4,true,1,true);
                    scriptDanmakuHost.setOrdinaryDanmakuBridge(17);
                    scriptDanmakuHost.append([{...main,code:'window.__starts=(window.__starts||0)+1;'+main.code+
                        ';window.__game={notes:NoteBoxList,read:function(){return {score:score,n4:n4};}};'}]);
                    step(0);
                    const targetMs=part==='easy'?6331:6216;
                    const lane=part==='easy'?1:3, keyCode=part==='easy'?83:36;
                    for(let elapsed=0;elapsed<targetMs-4000;elapsed+=1000/60)step(1000/60);
                    step(0);
                    scriptDanmakuHost.setState(targetMs/1000,true,1);
                    const ctx=document.querySelector('#stage canvas').getContext('2d');
                    const note=__game.notes[lane][0];
                    const point=[Math.round(note.x+25),Math.round(note.y+5)];
                    const pixel=p=>Array.from(ctx.getImageData(p[0],p[1],1,1).data);
                    const before=pixel(point),lightBefore=pixel([226+60*lane+5,333]);
                    const started=realNow();
                    __postHostMessage({type:'key',version:17,keyCode,up:false,positionMs:targetMs});
                    const inputMs=realNow()-started;
                    // 不调用 step/RAF，直接读取最终主画布像素。
                    const after=pixel(point),lightAfter=pixel([226+60*lane+5,333]);
                    const noteY=note.y,remainingShapes=note.shapeItems.length;
                    // 到线后的音符与高亮重叠，不能再把该位置的非透明像素当成音符残留。
                    // 高亮结束后检查音符本应继续下落的位置，仍应保持透明。
                    for(let frame=0;frame<21;frame++)step(1000/60);
                    const afterFeedback=pixel([Math.round(note.x+25),Math.round(note.y+5)]);
                    return {before,after,lightBefore,lightAfter,inputMs,gpu,state:__game.read(),
                        noteY,lineY:328,starts:window.__starts,remainingShapes,afterFeedback,
                        errors:__messages.filter(m=>m.type==='error')};
                }, {main,part});
                console.log(part+' '+JSON.stringify(result));
                assert.equal(result.state.score,1000);
                assert.equal(result.state.n4,1);
                assert.equal(result.starts,1,'首次音符回调不能重跑整局游戏');
                assert.ok(result.noteY<=330 && result.noteY+10>=328,
                    'Perfect 时音符必须与 y=328..330 的白色判定线相交：'+result.noteY);
                assert.ok(result.before[3]>0,'击打前音符必须已在主画布上');
                assert.equal(result.remainingShapes,0,'下次 RAF 前必须清除原音符图形');
                assert.equal(result.afterFeedback[3],0,'高亮结束后音符不得继续下落或留下残影');
                assert.notDeepEqual(result.lightAfter,result.lightBefore,'下次 RAF 前必须出现击打高亮');
                assert.deepEqual(result.lightAfter,[255,255,255,255]);
                assert.deepEqual(result.errors,[]); assert.deepEqual(p.errors,[]);
                console.log('ok '+part+' 原脚本按键后立即消除和高亮（真实 Chromium 像素）');
            } finally { await p.page.close(); }
        }
    } finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
