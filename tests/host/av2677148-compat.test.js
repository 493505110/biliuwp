'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const zlib = require('node:zlib');
const { ensureModuleSupport, evaluateModule } = require('./host-loader');
ensureModuleSupport();
const { loadHost } = require('./retained-mode.test');
const assets = path.resolve(__dirname, '../../BiliBili.UWP/Assets/script-danmaku');
const scripts = JSON.parse(fs.readFileSync(path.join(__dirname, 'fixtures/av2677148/scripts.json'), 'utf8'));
const item = code => ({ id: 'probe', stime: 0, duration: 0, lang: 'js', code });
function step(h, count) {
    for (let i = 0; i < count; i++) { h.runFrames(1); h.advanceRealTime(1000 / 60); }
}
function clean(h) { assert.deepEqual(h.errors(), []); assert.deepEqual(h.frameErrors, []); }

async function binaryTests() {
    const { ScriptByteArray } = (await evaluateModule(path.join(assets, 'bytearray.js'), vm.createContext({}))).namespace;
    const bytes = new ScriptByteArray();
    bytes.writeUTFBytes('BPM 中文 😀 \ud800');
    assert.deepEqual(Buffer.from(bytes.data), Buffer.from('BPM 中文 😀 \ud800'));
    assert.equal(bytes.position, bytes.length);
    bytes.position = 0; bytes[0] = 256 + 65;
    assert.equal(bytes[0], 65); assert.equal(bytes.position, 0);
    bytes.clear(); bytes.position = 12; bytes[3] = -1;
    assert.deepEqual(Array.from(bytes.data), [0, 0, 0, 255]);
    assert.equal(bytes.position, 12); assert.equal(bytes[4], undefined);
    assert.throws(() => { bytes[67108864] = 1; });
    for (const endian of ['littleEndian', 'bigEndian']) {
        const b = Buffer.alloc(11);
        b[0] = 2; b[1] = 255;
        b[endian === 'littleEndian' ? 'writeUInt16LE' : 'writeUInt16BE'](60000, 2);
        b[endian === 'littleEndian' ? 'writeInt16LE' : 'writeInt16BE'](-1234, 4);
        b[endian === 'littleEndian' ? 'writeFloatLE' : 'writeFloatBE'](-12.5, 6);
        const data = new ScriptByteArray(b); data.endian = endian;
        assert.equal(data.readBoolean(), true); assert.equal(data.readByte(), -1);
        assert.equal(data.readUnsignedShort(), 60000); assert.equal(data.readShort(), -1234);
        assert.equal(data.readFloat(), -12.5); assert.equal(data.readBoolean(), false);
        assert.throws(() => data.readShort()); assert.equal(data.position, 11);
    }
    for (const source of [Buffer.alloc(0), Buffer.from('字体数据'.repeat(1000)), Buffer.alloc(70000, 37)]) {
        for (const options of [{ level: 0 }, { strategy: zlib.constants.Z_FIXED }, { level: 9 }]) {
            const compressed = zlib.deflateSync(source, options), data = new ScriptByteArray(compressed);
            data.position = data.length; data.uncompress();
            assert.equal(data.position, 0); assert.deepEqual(Buffer.from(data.data), source);
            const raw = new ScriptByteArray(zlib.deflateRawSync(source, options));
            raw.uncompress('deflate'); assert.deepEqual(Buffer.from(raw.data), source);
        }
    }
    const compressed = zlib.deflateSync(Buffer.from('checksum'));
    compressed[compressed.length - 1] ^= 1;
    const corrupt = new ScriptByteArray(compressed); corrupt.position = 3;
    assert.throws(() => corrupt.uncompress());
    assert.deepEqual(Buffer.from(corrupt.data), compressed); assert.equal(corrupt.position, 3);
    for (const input of [[], [0x78, 0xda], [0x78, 0, 0, 0, 0, 0], [0x78, 0x20, 0, 0, 0, 0]]) {
        assert.throws(() => new ScriptByteArray(input).uncompress());
    }
    assert.throws(() => new ScriptByteArray().uncompress('lzma'));
    console.log('ok ByteArray UTF-8 / 下标 / 大小端 / EOF / zlib校验 / 裸DEFLATE');
}

async function syntaxAndCloneTests() {
    const h = await loadHost(); h.api.reset(0, true, 1, true);
    h.append([item(`
        var bytes=Bitmap.createBitmapData(1,1).getPixels({x:0,y:0,width:1,height:1});
        bytes.length=0;bytes.writeUTFBytes('abc');bytes.position=1;bytes.endian='littleEndian';
        var copy=clone(bytes);copy[0]=90;window.__bytes=[bytes,copy];
        function ordinary(){return\n[123];}window.__ordinary=ordinary();
    `)]);
    step(h, 2); clean(h);
    const [source, copy] = h.sandbox.__bytes;
    assert.equal(copy.constructor, source.constructor); assert.equal(copy.length, 3);
    assert.equal(copy.position, 1); assert.equal(copy.endian, 'littleEndian');
    assert.equal(source[0], 97); assert.equal(copy[0], 90);
    assert.equal(copy.readUnsignedByte(), 98); assert.equal(source.position, 1);
    assert.equal(h.sandbox.__ordinary, undefined, '合法 JS 的 return 换行保持 ASI');
    h.append([{...item(`
        var text='If Return Else';var re=/If|Return|Else/;
        /* If Return Else */
        function params(){return\n[{provider:42}];}
        function choose(x){If(x) Return params();Else Return [];}
        function pattern(){Return /If|Return|Else/;}
        var obj={If:7,Return:8,Else:9};
        window.__legacy=[choose(true)[0].provider,choose(false).length,text,re.source,pattern().source,obj.If,obj.Return,obj.Else];
    `), id:'legacy'}]);
    step(h, 2); clean(h);
    assert.deepEqual(Array.from(h.sandbox.__legacy), [42, 0, 'If Return Else', 'If|Return|Else', 'If|Return|Else',7,8,9]);
    h.append([{...item(`var text='multi\nline';function ordinary(){return\n[123];}window.__multiline=ordinary();`),id:'multiline'}]);
    step(h, 2); clean(h); assert.equal(h.sandbox.__multiline, undefined);
    console.log('ok ByteArray 克隆独立性 / 旧关键字与跨行返回 / 字符串正则注释 / 合法JS语义');
}

const probe = item(`window.__bpm={font:$G._get('fontData'),ready:$G._get('Font'),akari:$G._get('__akari')};`);
async function scriptTests() {
    const h = await loadHost(); h.api.reset(0, true, 1, true, 247);
    h.append([...scripts, probe]); step(h, 2); clean(h);
    assert.equal(h.sandbox.__bpm.ready, true);
    assert.equal(h.sandbox.__bpm.font.size, 36); assert.equal(h.sandbox.__bpm.font.glyphCount, 235);
    assert.ok(h.sandbox.__bpm.font.b.commands.length > 0);
    assert.ok(h.sandbox.__bpm.font.b.paths.length > 0);
    step(h, 247 * 60); clean(h);
    assert.ok(h.mainCanvas().__marks.length > 0, '原始作品必须画出图形');
    for (const position of [10, 120, 240, 0]) {
        h.api.seek(position, true, 1); step(h, 2); clean(h);
        assert.ok(h.mainCanvas().__marks.length > 0, '跳转后仍应出画面');
    }
    h.api.reset(0, true, 1, true, 247); h.append([...scripts, probe]); step(h, 2); clean(h);
    assert.equal(h.sandbox.__bpm.ready, true, '重建后字体重新加载');
    console.log('ok BPM 三条原始M8 / 235字形 / 247秒时间轴 / 前后跳转 / 重建');
}

async function browserTests() {
    const { createBrowserHost } = require('./browser-host');
    const host = await createBrowserHost();
    try {
        const {page, errors} = await host.page(1280,720);
        await page.evaluate(items => {
            scriptDanmakuHost.reset(0,true,1,true,247);scriptDanmakuHost.append(items);__step(0);
        }, [...scripts, probe]);
        assert.equal(await page.evaluate(()=>__bpm.font.glyphCount),235);
        let frame=0;
        for (const second of [0,10,33,60,90,120,180,240,247]) {
            const target=Math.round(second*60);
            await page.evaluate(count=>{for(let i=0;i<count;i++)__step(1000/60);},target-frame);frame=target;
            assert.deepEqual(await page.evaluate(()=>__messages.filter(m=>m.type==='error')),[]);
            assert.ok(await page.evaluate(()=>{
                const c=document.querySelector('#stage canvas'),d=c.getContext('2d').getImageData(0,0,c.width,c.height).data;
                for(let i=3;i<d.length;i+=4)if(d[i])return true;return false;
            }), `${second}s 必须有像素`);
            if(process.env.M8_ARTIFACT_DIR && [10,60,240].includes(second)) {
                fs.mkdirSync(process.env.M8_ARTIFACT_DIR,{recursive:true});
                await page.screenshot({path:path.join(process.env.M8_ARTIFACT_DIR,'bpm-'+second+'.png')});
            }
        }
        assert.deepEqual(errors,[]);
        console.log('ok 真实Edge：原始字体解压 / 247秒逐帧 / 九个像素采样点');
    } finally { await host.close(); }
}

(async()=>{
    await binaryTests();await syntaxAndCloneTests();await scriptTests();
    if(process.env.M8_BROWSER_TESTS==='1')await browserTests();
})().catch(error=>{console.error(error);process.exitCode=1;});
