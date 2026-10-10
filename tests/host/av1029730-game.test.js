'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const zlib = require('node:zlib');
const { ensureModuleSupport, evaluateModule } = require('./host-loader');
const { createBrowserHost } = require('./browser-host');
ensureModuleSupport();

const assets = path.resolve(__dirname, '../../BiliBili.UWP/Assets/script-danmaku');
const fixtures = JSON.parse(fs.readFileSync(path.join(__dirname, 'fixtures/av1029730/scripts.json'), 'utf8'));

async function binaryTests() {
    const context = vm.createContext({});
    const { inflateRaw, ScriptByteArray } = (await evaluateModule(path.join(assets, 'bytearray.js'), context)).namespace;
    const payloads = [Buffer.alloc(0), Buffer.from('hello hello hello'), Buffer.alloc(70000, 37),
        Buffer.from(Array.from({ length: 32000 }, (_, i) => (Math.imul(i, 1103515245) >>> 16) & 255))];
    for (const input of payloads) {
        for (const options of [{ level: 0 }, { strategy: zlib.constants.Z_FIXED }, { level: 9 }]) {
            const compressed = zlib.deflateRawSync(input, options);
            assert.deepEqual(Buffer.from(inflateRaw(compressed)), input);
            const bytes = new ScriptByteArray(compressed);
            bytes.position = bytes.length;
            bytes.inflate();
            assert.equal(bytes.position, 0);
            assert.deepEqual(Buffer.from(bytes.data), input);
        }
    }
    for (const data of [[], [7], [1, 1, 0, 0, 0], [3, 2, 0]]) assert.throws(() => inflateRaw(Uint8Array.from(data)));
    const bytes = new ScriptByteArray();
    bytes.writeByte(-1); bytes.writeUnsignedInt(0x12345678); bytes.position = 0;
    assert.equal(bytes.readUnsignedByte(), 255); assert.equal(bytes.readUnsignedInt(), 0x12345678);
    assert.throws(() => bytes.readUnsignedByte()); bytes.clear();
    assert.equal(bytes.length, 0); assert.equal(bytes.position, 0);
    bytes.position = 3; bytes.writeByte(5);
    assert.deepEqual(Array.from(bytes.data), [0, 0, 0, 5]);
    bytes.endian = 'littleEndian'; bytes.position = 0; bytes.writeUnsignedInt(0x12345678); bytes.position = 0;
    assert.equal(bytes.readUnsignedInt(), 0x12345678);

    const { normalizeMultilineStrings } = (await evaluateModule(path.join(assets, 'legacy.js'), context)).namespace;
    const source = 'var regex=/["\']/g; /* " */ var a="a\nb"; // "\nvar b=4/2;';
    const normalized = normalizeMultilineStrings(source);
    assert.equal(vm.runInContext(normalized + 'a', context), 'a\nb');
    assert.equal(vm.runInContext('b', context), 2);
    assert.equal(vm.runInContext(`var a=[{time:2},{time:10},{time:1}];a.sortOn('time',18);a.map(x=>x.time).join(',')`, context), '10,2,1');
    assert.equal(vm.runInContext(`a.sortOn('time',24).join(',')`, context), '2,1,0');
    assert.equal(vm.runInContext(`a.map(x=>x.time).join(',')`, context), '10,2,1');
    assert.equal(vm.runInContext(`[{x:1},{x:1}].sortOn('x',20)`, context), 0);
    assert.equal(vm.runInContext(`[{a:'b',b:1},{a:'A',b:2},{a:'a',b:3}].sortOn(['a','b'],[1,18]).map(x=>x.b).join(',')`, context), '3,2,1');
    assert.equal(vm.runInContext(`Object.keys(Array.prototype).includes('sortOn')`, context), false);
    console.log('ok ByteArray / DEFLATE / 多行字符串 / sortOn');
}

async function pixelTests(probe) {
    await probe.script(`
        var src=Bitmap.createBitmapData(3,1,true,0);
        src.setPixel32(0,0,0xffff0000);src.setPixel32(1,0,0xff00ff00);src.setPixel32(2,0,0xff0000ff);
        var data=src.getPixels(src.rect);
        var dst=Bitmap.createBitmapData(3,1,true,0xffffffff);dst.setPixels(dst.rect,data);
        window.__pixels=[dst.getPixel32(0,0),dst.getPixel32(1,0),dst.getPixel32(2,0)];
        window.__position=data.position;
        dst.copyPixels(dst,Bitmap.createRectangle(0,0,2,1),$.createPoint(1,0));
        window.__overlap=[dst.getPixel32(0,0),dst.getPixel32(1,0),dst.getPixel32(2,0)];
        var clear=Bitmap.createBitmapData(1,1,true,0);dst.copyPixels(clear,clear.rect,$.createPoint(0,0));
        window.__cleared=dst.getPixel32(0,0);
        var display=Bitmap.createBitmap({bitmapData:src,x:10,y:10,scale:10,lifeTime:20});
        window.__src=src;
        var text=$.createComment('abc\n123',{x:100,y:100,lifeTime:20});
        text.setTextFormat($.createTextFormat('sans-serif',20,0xffff00,true,true));
        text.defaultTextFormat=$.createTextFormat('sans-serif',20,0xffff00,true,true);
        text.background=true;text.backgroundColor=0x0000ff;
        window.__text=text;window.__textHeight=text.height;
        var wrap=$.createComment('abcdef',{x:200,y:100,lifeTime:20});wrap.wordWrap=true;wrap.multiline=true;wrap.width=20;
        window.__wrap=[wrap.width,wrap.height,wrap.scaleX];
        var a=$.createShape({x:300,y:100,lifeTime:20});a.graphics.beginFill(0xff0000);a.graphics.drawRect(0,0,40,40);a.graphics.endFill();
        var b=$.createShape({x:350,y:100,lifeTime:20});b.graphics.copyFrom(a.graphics);a.graphics.clear();
        window.__copy=b;
        var group=$.createCanvas({x:100,y:200,scale:2,lifeTime:20});
        var box=$.createShape({parent:group,x:10,y:10,visible:false,lifeTime:20});box.graphics.beginFill(0);box.graphics.drawRect(0,0,10,10);box.graphics.endFill();
        var hit=$.createShape({x:125,y:225,lifeTime:20});hit.graphics.beginFill(0);hit.graphics.drawRect(0,0,5,5);hit.graphics.endFill();
        window.__hits=[box.hitTestObject(hit)];hit.x=140;window.__hits.push(box.hitTestObject(hit));
    `);
    assert.deepEqual(await probe.page.evaluate(() => __messages.filter(m => m.type === 'error')), []);
    assert.deepEqual(await probe.page.evaluate(() => __pixels), [0xffff0000, 0xff00ff00, 0xff0000ff]);
    assert.equal(await probe.page.evaluate(() => __position), 12);
    assert.deepEqual(await probe.page.evaluate(() => __overlap), [0xffff0000, 0xffff0000, 0xff00ff00]);
    assert.equal(await probe.page.evaluate(() => __cleared), 0);
    assert.deepEqual(await probe.pixel(12, 12), [255, 0, 0, 255]);
    await probe.update('__src.setPixel32(0,0,0xff00ff00);');
    assert.deepEqual(await probe.pixel(12, 12), [0, 255, 0, 255]);
    assert.equal(await probe.page.evaluate(() => __textHeight), 48);
    assert.deepEqual(await probe.pixel(101, 101), [0, 0, 255, 255]);
    assert.deepEqual(await probe.page.evaluate(() => __hits), [true, false]);
    assert.deepEqual(await probe.pixel(365, 115), [255, 0, 0, 255]);
    const wrap = await probe.page.evaluate(() => __wrap);
    assert.equal(wrap[0], 20); assert.ok(wrap[1] > 30); assert.equal(wrap[2], 1);
    console.log('ok 真像素：ARGB / 重叠复制 / 位图更新 / 文本格式 / graphics复制 / 隐藏碰撞框');
}

async function gameTests(probe) {
    // 不改夹具；仅在脚本末尾导出闭包状态供断言，运行的游戏逻辑保持原样。
    const items = fixtures.map(item => ({ ...item }));
    items[0].code += `\nwindow.__game={player:player,canvas:gameCanvas,scoreboard:scoreboard,
        get deaths(){return deadTime;},get count(){return numDanmu;},get alive(){return alive;},
        get shield(){return indestructible;},get bullets(){return displayingDanmus;},get comments(){return numComments;}};`;
    items[1].code += '\nwindow.__result={canvas:mainCanvas,scoreboard:scoreboard};';
    await probe.page.evaluate(items => {
        scriptDanmakuHost.reset(0, false, 1, true);
        scriptDanmakuHost.setOrdinaryDanmakuBridge(42);
        scriptDanmakuHost.resetComments();
        scriptDanmakuHost.appendComments([0,26,56,87,116,147,198,241,281,321].map(time => ({txt:'test bullet',time,color:0xffffff,mode:1,fontSize:25})));
        scriptDanmakuHost.append(items); __step(0); __step(50);
    }, items);
    assert.deepEqual(await probe.page.evaluate(() => __messages.filter(m => m.type === 'error')), []);
    assert.equal(await probe.page.evaluate(() => __game.player.shape.bitmapData.getPixels(__game.player.shape.bitmapData.rect).length), 50 * 19 * 4);
    const alpha = await probe.page.evaluate(() => {
        const bytes = __game.player.shape.bitmapData.getPixels(__game.player.shape.bitmapData.rect).data;
        return bytes.filter((_, i) => i % 4 === 0 && bytes[i] > 0).length;
    });
    assert.ok(alpha > 20, '飞机图片必须真正解压出非透明像素');
    const before = await probe.page.evaluate(() => __game.player.shape.x);
    await probe.page.evaluate(() => { scriptDanmakuHost.setState(0, true, 1);scriptDanmakuHost.pushKey(68,false);__step(50); });
    assert.ok(await probe.page.evaluate(() => __game.player.shape.x) > before, 'D 按下应向右移动');
    assert.equal(await probe.page.evaluate(() => __messages.filter(m => m.type === 'ordinaryDanmaku').at(-1).suppressed), true,
        '原始 removeScrollComment 必须接管 NSDanmaku 显示层');
    await probe.page.evaluate(() => { scriptDanmakuHost.pushKey(68,true); });
    const released = await probe.page.evaluate(() => __game.player.shape.x);
    await probe.page.evaluate(() => __step(50));
    assert.equal(await probe.page.evaluate(() => __game.player.shape.x), released, '松开 D 应停止');
    for (const [key, axis, direction] of [[87,'y',-1],[83,'y',1],[65,'x',-1]]) {
        const position = await probe.page.evaluate(axis => __game.player.shape[axis], axis);
        await probe.page.evaluate(key => { scriptDanmakuHost.pushKey(key,false);__step(50);scriptDanmakuHost.pushKey(key,true); }, key);
        assert.ok((await probe.page.evaluate(axis => __game.player.shape[axis], axis) - position) * direction > 0);
    }
    if (process.env.M8_GAME_SCREENSHOT_DIR) await probe.page.screenshot({path:path.join(process.env.M8_GAME_SCREENSHOT_DIR,'av1029730-game.png')});
    await probe.page.waitForTimeout(2100); // 原脚本复活护盾用真实时间 timer(2000)。
    assert.equal(await probe.page.evaluate(() => __game.shield), false);
    await probe.page.evaluate(() => {
        const bullet=__game.bullets[0];bullet.shape.x=__game.player.shape.x+20;bullet.shape.y=__game.player.shape.y+7;
        __step(50);
    });
    assert.equal(await probe.page.evaluate(() => __game.deaths), 1);
    assert.equal(await probe.page.evaluate(() => __game.alive), false);
    await probe.page.evaluate(() => { for(let i=0;i<16;i++) __step(50); });
    assert.equal(await probe.page.evaluate(() => __game.alive), true, '爆炸动画结束后复活');
    assert.equal(await probe.page.evaluate(() => __game.shield), true);
    await probe.page.evaluate(() => {
        scriptDanmakuHost.pushComment({txt:'new bullet',time:1,color:0x00ff00,mode:1,fontSize:25});
        for(let i=0;i<24;i++) __step(50);
    });
    assert.equal(await probe.page.evaluate(() => __game.comments), 11, '原脚本轮询 commentList 能发现刚发送的弹幕');
    await probe.page.evaluate(() => {
        for(let second=2;second<=330;second++) { scriptDanmakuHost.setState(second,true,1);__step(50); }
    });
    assert.equal(await probe.page.evaluate(() => !!window.__result), true);
    assert.equal(await probe.page.evaluate(() => __messages.filter(m => m.type === 'ordinaryDanmaku').at(-1).suppressed), false,
        '原始 end/clearEl 必须释放接管');
    assert.match(await probe.page.evaluate(() => __result.scoreboard.text), /死亡次数：1/);
    assert.ok(await probe.page.evaluate(() => __result.canvas.width) > 300);
    assert.ok(await probe.page.evaluate(() => {
        const pixels=document.querySelector('#stage canvas').getContext('2d').getImageData(0,0,800,600).data;
        let count=0;for(let i=3;i<pixels.length;i+=4) if(pixels[i]) count++;return count;
    }) > 1000, '结算必须画出像素');
    if (process.env.M8_GAME_SCREENSHOT_DIR) await probe.page.screenshot({path:path.join(process.env.M8_GAME_SCREENSHOT_DIR,'av1029730-result.png')});
    console.log('ok 原始游戏：飞机解压 / WASD按下松开 / 死亡碰撞 / 爆炸复活 / 全时间轴 / 329秒结算');
}

async function run() {
    await binaryTests();
    const host = await createBrowserHost();
    try {
        for (const test of [pixelTests, gameTests]) {
            const probe = await host.page();
            try {
                await test(probe);
                assert.deepEqual(probe.errors, []);
                assert.deepEqual(await probe.page.evaluate(() => __messages.filter(m => m.type === 'error')), []);
            } finally { await probe.page.close(); }
        }
    } finally { await host.close(); }
}
run().catch(error => { console.error(error); process.exitCode = 1; });
