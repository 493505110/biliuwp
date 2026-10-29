'use strict';

// 最终 DOM 呈现回归：二维缓存像素不能验证可见 WebGL Canvas、透明度和后端切换。
// NODE_PATH 指向已有 Playwright；node tests/host/gpu-presentation.test.js。
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { createBrowserHost } = require('./browser-host');
const setup = `var box=$.createShape({lifeTime:600,x:400,y:300,z:20,rotationY:20});
box.graphics.beginFill(0xff0000);box.graphics.drawRect(0,0,100,100);box.graphics.endFill();window.__box=box;
var overlay=$.createShape({lifeTime:600,x:410,y:310,visible:false});
overlay.graphics.beginFill(0x00ff00);overlay.graphics.drawRect(0,0,30,30);overlay.graphics.endFill();window.__overlay=overlay;
var mask=$.createShape({lifeTime:600,x:400,y:300,visible:false});
mask.graphics.beginFill(0xffffff);mask.graphics.drawRect(0,0,50,80);mask.graphics.endFill();window.__mask=mask;`;

// 与 C# ReplaceAsync 相同：先暂停 reset，再建立普通弹幕桥接，最后开始播放。
async function script(p, code) {
    await p.page.evaluate(code => {
        scriptDanmakuHost.reset(0, false, 1, true);
        scriptDanmakuHost.setOrdinaryDanmakuBridge(7);
        scriptDanmakuHost.append([{ id: 'presentation', stime: 0, duration: 600, lang: 'js', code }]);
        scriptDanmakuHost.setState(0, true, 1);
        __step(0);
    }, code);
}

async function state(p) {
    return p.page.evaluate(async () => {
        const { hostState } = await import('/script-danmaku/core.js');
        return { direct: hostState.canvas.style.visibility === 'hidden',
            gpuVisible: [...document.querySelectorAll('#stage canvas')].some(c => c !== hostState.canvas && c.style.display !== 'none') };
    });
}

async function screenshot(p) {
    return 'data:image/png;base64,' + (await p.page.screenshot({ omitBackground: true })).toString('base64');
}

async function compare(p, a, b) {
    return p.page.evaluate(async ({ a, b }) => {
        const load = src => new Promise((resolve, reject) => {
            const image = new Image(); image.onload = () => resolve(image); image.onerror = reject; image.src = src;
        });
        const [first, second] = await Promise.all([load(a), load(b)]);
        if (first.width !== second.width || first.height !== second.height) return { differentSize: true };
        const canvas = document.createElement('canvas'); canvas.width = first.width; canvas.height = first.height;
        const context = canvas.getContext('2d'); context.drawImage(first, 0, 0);
        const left = context.getImageData(0, 0, canvas.width, canvas.height).data;
        context.clearRect(0, 0, canvas.width, canvas.height); context.drawImage(second, 0, 0);
        const right = context.getImageData(0, 0, canvas.width, canvas.height).data;
        let alphaMax = 0, visibleMax = 0, referencePixels = 0;
        for (let i = 0; i < left.length; i += 4) {
            if (left[i + 3]) referencePixels++;
            alphaMax = Math.max(alphaMax, Math.abs(left[i + 3] - right[i + 3]));
            for (let channel = 0; channel < 3; channel++) {
                const black = left[i + channel] * left[i + 3] / 255 - right[i + channel] * right[i + 3] / 255;
                const white = black - left[i + 3] + right[i + 3];
                visibleMax = Math.max(visibleMax, Math.abs(black), Math.abs(white));
            }
        }
        return { alphaMax, visibleMax, referencePixels };
    }, { a, b });
}

async function samePicture(p, reference, label) {
    const diff = await compare(p, reference, await screenshot(p));
    assert.ok(!diff.differentSize && diff.alphaMax <= 1 && diff.visibleMax <= 1, label + ': ' + JSON.stringify(diff));
    return diff;
}

async function visiblePixel(p, x, y) {
    const png = await screenshot(p);
    return p.page.evaluate(async ({ png, x, y }) => {
        const image = new Image(); image.src = png; await image.decode();
        const canvas = document.createElement('canvas'); canvas.width = image.width; canvas.height = image.height;
        const context = canvas.getContext('2d'); context.drawImage(image, 0, 0);
        return [...context.getImageData(x, y, 1, 1).data];
    }, { png, x, y });
}

async function healthy(p) {
    assert.deepEqual(p.errors, []);
    assert.deepEqual(await p.page.evaluate(() => __messages.filter(m => m.type === 'error')), []);
}

async function boundaries(host) {
    const steps = [
        ['direct', '', true],
        ['affine', '__box.rotationY=0;', false],
        ['2d', '__box.z=0;', false],
        ['3d-again', '__box.z=20;__box.rotationY=20;', true],
        ['other-layer', '__overlay.visible=true;', false],
        ['single-layer', '__overlay.visible=false;', true],
        ['mask', '__mask.visible=true;__box.mask=__mask;', false],
        ['no-mask', '__box.mask=null;__mask.visible=false;', true],
        ['offscreen', '__box.x=100000;', false],
        ['onscreen', '__box.x=400;', true],
        ['alpha-blend-alias', '__box.blendMode="alpha";', true],
        ['unknown-blend-alias', '__box.blendMode="legacy-unknown";', true],
        ['multiply-fallback', '__box.blendMode="multiply";', false],
        ['normal-blend-again', '__box.blendMode="normal";', true],
        ['invalid-scale', '__box.scaleX=NaN;', false],
        ['scale-recovered', '__box.scaleX=1;', true],
        ['invalid-alpha', '__box.alpha=NaN;', false],
        ['alpha-recovered', '__box.alpha=1;', true],
        ['invalid-matrix', 'var m=__box.transform.matrix3D;m.rawData[12]=NaN;__box.transform.matrix3D=m;', false],
        ['matrix-recovered', 'var m=__box.transform.matrix3D;m.rawData[12]=400;__box.transform.matrix3D=m;', true],
        ['alpha', '__box.alpha=0.5;', true],
        ['hidden', 'scriptDanmakuHost.visible(false);', false],
        // 显式触发一次重绘，避免把旧路径静态画面恢复后的空白当作正确参考。
        ['shown', 'scriptDanmakuHost.visible(true);__box.x+=0.001;', true],
        ['resize', null, true],
        ['reset', 'scriptDanmakuHost.reset(0,false,1,true);', false]
    ];
    const references = new Map();
    for (const direct of [false, true]) {
        const p = await host.page(800, 600, 1, false, direct);
        try {
            await script(p, setup);
            for (const [name, code, expected] of steps) {
                if (code === null) {
                    await p.page.evaluate(() => {
                        window.__resizeSeen = false;
                        window.addEventListener('resize', () => { window.__resizeSeen = true; }, { once: true });
                    });
                    await p.page.setViewportSize({ width: 1000, height: 700 });
                    await p.page.waitForFunction(() => window.__resizeSeen, null, { polling: 10 });
                    await p.page.evaluate(() => { scriptDanmakuHost.resize(); __step(34); });
                } else if (code) await p.update(code);
                if (direct) {
                    assert.equal((await state(p)).direct, expected, name);
                    await samePicture(p, references.get(name), name);
                } else references.set(name, await screenshot(p));
            }
            await healthy(p);
        } finally { await p.page.close(); }
    }
    console.log('ok 直呈/二维/遮罩/多图层/透明度/隐藏/尺寸/重置切换保持最终画面');
}

async function contextLoss(host) {
    const p = await host.page(800, 600, 1, false, true);
    try {
        await script(p, setup);
        await p.page.evaluate(() => { scriptDanmakuHost.setState(0, false, 1); __step(34); });
        assert.equal((await state(p)).direct, true);
        await p.page.evaluate(async () => {
            const { hostState } = await import('/script-danmaku/core.js');
            const canvas = [...document.querySelectorAll('#stage canvas')].find(c => c !== hostState.canvas);
            canvas.getContext('webgl').getExtension('WEBGL_lose_context').loseContext();
        });
        await p.page.waitForFunction(async () => {
            const { hostState } = await import('/script-danmaku/core.js');
            return hostState.canvas.style.visibility !== 'hidden';
        }, null, { polling: 10 });
        // 不改脚本属性：上下文丢失事件本身必须唤醒已暂停的帧循环。
        await p.page.evaluate(() => __step(34));
        assert.deepEqual(await state(p), { direct: false, gpuVisible: false });
        assert.deepEqual(await visiblePixel(p, 430, 330), [255, 0, 0, 255]);
        await healthy(p);
    } finally { await p.page.close(); }
    console.log('ok 静态暂停画面在 WebGL 丢失后主动回退，无需修改脚本');
}

async function staticResume(host) {
    const p = await host.page(800, 600, 1, false, true);
    try {
        await script(p, setup);
        await p.update('scriptDanmakuHost.visible(false);');
        assert.deepEqual(await visiblePixel(p, 430, 330), [0, 0, 0, 0]);
        await p.update('scriptDanmakuHost.visible(true);');
        assert.equal((await state(p)).direct, true);
        assert.deepEqual(await visiblePixel(p, 430, 330), [255, 0, 0, 255]);
        for (const enabled of [false, true]) {
            await p.page.evaluate(async enabled => {
                const { setDirectPresentationEnabled } = await import('/script-danmaku/renderer.js');
                setDirectPresentationEnabled(enabled); __step(1);
            }, enabled);
            assert.equal((await state(p)).direct, enabled);
            assert.deepEqual(await visiblePixel(p, 430, 330), [255, 0, 0, 255]);
        }
        await healthy(p);
    } finally { await p.page.close(); }
    console.log('ok 静态画面隐藏后恢复及后端切换立即补画');
}

async function unavailableGPU(host) {
    const p = await host.page(800, 600, 1, true, true);
    try {
        await script(p, setup);
        assert.deepEqual(await state(p), { direct: false, gpuVisible: false });
        assert.deepEqual(await visiblePixel(p, 430, 330), [255, 0, 0, 255]);
        await healthy(p);
    } finally { await p.page.close(); }
    console.log('ok 无 WebGL 时保持二维投影后备');
}

async function dpi(host) {
    for (const [ratio, width, height, size, expected] of [[1.25, 801, 601, 100, false], [1.25, 800, 600, 1000, true], [2, 801, 601, 100, true]]) {
        let reference;
        for (const direct of [false, true]) {
            const p = await host.page(width, height, 1, false, direct, ratio);
            try {
                await script(p, `var box=$.createShape({lifeTime:600,x:400,y:300,z:20,rotationY:20,alpha:0.5});box.graphics.beginFill(0xff3300);box.graphics.drawRect(0,0,${size},${size});box.graphics.endFill();`);
                if (direct) {
                    assert.equal((await state(p)).direct, expected, 'DPI ' + ratio);
                    await samePicture(p, reference, 'DPI ' + ratio);
                } else reference = await screenshot(p);
                await healthy(p);
            } finally { await p.page.close(); }
        }
    }
    console.log('ok 125%/200% DPI 的像素对齐与回退');
}

async function invalidMask(host) {
    const p = await host.page(800, 600, 1, false, true);
    try {
        await script(p, setup);
        await p.update('__mask.graphics.clear();__mask.graphics.beginFill(0xffffff);__mask.graphics.drawRect(0,0,0,80);__mask.graphics.endFill();__mask.visible=true;__box.mask=__mask;');
        assert.equal((await state(p)).direct, false);
        assert.deepEqual(await visiblePixel(p, 430, 330), [0, 0, 0, 0]);
        await healthy(p);
    } finally { await p.page.close(); }
    console.log('ok 退化矩形遮罩不会被误判为覆盖整个视口');
}

async function emptyContainers(host) {
    const steps = [
        ['empty-3d-sibling', '', true],
        ['empty-before-owner', '__box.parent.setChildIndex(__empty,0);', true],
        ['nested-content', '__overlay.visible=true;', false],
        ['nested-hidden', '__overlay.visible=false;', true],
        ['bridge-empty', '__box.parent.getChildByName("__biliuwpOverlay").addChild(__overlay);', true],
        ['bridge-content', '__overlay.visible=true;', false],
        ['bridge-hidden', '__overlay.visible=false;', true],
        ['remove-empty', '__empty.parent.removeChild(__empty);', true]
    ];
    const references = new Map();
    for (const direct of [false, true]) {
        const p = await host.page(800, 600, 1, false, direct);
        try {
            await script(p, setup + 'window.__empty=$.createCanvas({lifeTime:600,z:20,rotationY:10});'
                + 'var inner=$.createCanvas({lifeTime:600});__empty.addChild(inner);inner.addChild(overlay);');
            for (const [name, code, expected] of steps) {
                if (code) await p.update(code);
                if (direct) {
                    assert.equal((await state(p)).direct, expected, name);
                    await samePicture(p, references.get(name), name);
                } else references.set(name, await screenshot(p));
            }
            await healthy(p);
        } finally { await p.page.close(); }
    }
    console.log('ok 空桥接和嵌套/3D容器不阻止直呈，添加可见内容后回退且不丢图');
}

async function linearCandidateScan(host) {
    const reads = [];
    for (const count of [128, 256]) {
        const p = await host.page(800, 600, 1, false, true);
        try {
            await script(p, `for(var i=0;i<${count};i++){
                var box=$.createShape({lifeTime:600,z:-20000});
                box.graphics.beginFill(0xffffff);box.graphics.drawRect(0,0,10,10);box.graphics.endFill();
            }`);
            reads.push(await p.page.evaluate(async () => {
                const { hostState } = await import('/script-danmaku/core.js');
                let alphaReads = 0;
                for (const element of hostState.rootElement.childList) {
                    Object.defineProperty(element.props, 'alpha', {
                        configurable: true, get() { alphaReads++; return 1; }
                    });
                }
                // 统计实际属性读取而非墙钟时间；顶层数量翻倍不能引发四倍扫描。
                for (let frame = 0; frame < 3; frame++) {
                    for (const element of hostState.rootElement.childList) element.x += 1;
                    __step(34);
                }
                return alphaReads;
            }));
            assert.equal((await state(p)).direct, false);
            assert.deepEqual(await visiblePixel(p, 430, 330), [0, 0, 0, 0]);
            await healthy(p);
        } finally { await p.page.close(); }
    }
    assert.ok(reads[0] > 0 && reads[1] <= reads[0] * 2.5, JSON.stringify(reads));
    console.log('ok 顶层3D节点翻倍保持线性资格检查，alpha读取 ' + reads.join(' → '));
}

async function realSceneViewport(host) {
    const directory = path.join(__dirname, 'fixtures/real');
    const fixtures = fs.readdirSync(directory).filter(n => n.endsWith('.js')).sort().map(n => ({
        id: n, stime: 0, duration: 333, lang: 'js', code: fs.readFileSync(path.join(directory, n), 'utf8')
    }));
    for (const ratio of [1, 1.25, 2]) {
        const references = new Map();
        for (const direct of [false, true]) {
            const p = await host.page(1280, 720, 1, false, direct, ratio);
            try {
                await p.page.evaluate(fixtures => {
                    scriptDanmakuHost.reset(5, false, 1, true);
                    scriptDanmakuHost.setOrdinaryDanmakuBridge(7);
                    scriptDanmakuHost.append(fixtures);
                    scriptDanmakuHost.setState(5, true, 1);
                    __step(0);
                }, fixtures);
                for (const [width, height] of [[1280,720], [1920,1048], [800,600]]) {
                    await p.page.setViewportSize({width, height});
                    await p.page.waitForFunction(({width, height, ratio}) => {
                        const canvas = document.querySelector('#stage canvas');
                        return canvas.width === Math.round(width * ratio) && canvas.height === Math.round(height * ratio);
                    }, {width, height, ratio}, {polling: 10});
                    await p.update('scriptDanmakuHost.resize();__step(34);');
                    const scale = Math.min(width / 1280, height / 720);
                    const x = (width - 1280 * scale) / 2 + 1280 * scale * 0.9;
                    const y = (height - 720 * scale) / 2 + 720 * scale * 0.9;
                    const pixel = await visiblePixel(p, Math.floor(x * ratio), Math.floor(y * ratio));
                    assert.ok(pixel[3] > 128, `5s 背景未覆盖适配后的右下区域 ${width}x${height} DPI ${ratio}: ${pixel}`);
                    const label = `${width}x${height}`;
                    if (direct) await samePicture(p, references.get(label), '5s resize ' + label + ' DPI ' + ratio);
                    else references.set(label, await screenshot(p));
                }
                await healthy(p);
            } finally { await p.page.close(); }
        }
    }
    console.log('ok DECORATOR 5秒子级遮罩覆盖缩放后的画面，窗口放大/缩小及100%/125%/200% DPI一致');
}

async function realScene(host, startPosition = 36, frames = [30, 31, 60, 90, 91, 120, 150, 151, 180]) {
    const directory = path.join(__dirname, 'fixtures/real');
    const fixtures = fs.readdirSync(directory).filter(n => n.endsWith('.js')).sort().map(n => ({
        id: n, stime: 0, duration: 333, lang: 'js', code: fs.readFileSync(path.join(directory, n), 'utf8')
    }));
    const references = new Map();
    const rows = [];
    let alphaIsolationFrames = 0;
    for (const direct of [false, true]) {
        const p = await host.page(1280, 720, 1, false, direct);
        try {
            await p.page.evaluate(({ fixtures, startPosition }) => {
                scriptDanmakuHost.reset(startPosition, false, 1, true);
                scriptDanmakuHost.setOrdinaryDanmakuBridge(7);
                scriptDanmakuHost.append(fixtures);
                scriptDanmakuHost.setState(startPosition, true, 1);
                __step(0);
            }, { fixtures, startPosition });
            await p.page.evaluate(() => {
                window.__copies = 0;
                const draw = CanvasRenderingContext2D.prototype.drawImage;
                CanvasRenderingContext2D.prototype.drawImage = function (...args) {
                    if (new Error().stack.includes('flushProjectedBatch')) __copies++;
                    return draw.apply(this, args);
                };
            });
            let previous = 0;
            // 检查非整秒帧及紧随的保留帧；真实 alpha 遮罩需要隔离时允许回退。
            for (const frame of frames) {
                await p.page.evaluate(({ previous, frame }) => { for (let f = previous + 1; f <= frame; f++) __stepTo(f * 1000 / 60); }, { previous, frame });
                previous = frame;
                if (direct) {
                    const alphaIsolation = await p.page.evaluate(async () => {
                        const {hostState}=await import('/script-danmaku/core.js');
                        return hostState.elements.some(element => {
                            if (element.projectedPlanar || element.blendMode !== 'layer'
                                || !element.childList.some(child => child.blendMode === 'alpha')) return false;
                            for (let current=element; current; current=current.treeParent) {
                                if (current.expired || current.visible === false || current.alpha <= 0) return false;
                            }
                            return true;
                        });
                    });
                    if (alphaIsolation) {
                        alphaIsolationFrames++;
                        assert.equal((await state(p)).direct, false, 'alpha 遮罩隔离 frame ' + frame);
                    } else if (startPosition === 36 && [30,31,90,91,150,151].includes(frame)) {
                        assert.equal((await state(p)).direct, true, 'frame ' + frame);
                    }
                    const diff = await samePicture(p, references.get(frame), 'DECORATOR ' + startPosition + 's frame ' + frame);
                    assert.ok(diff.referencePixels > 0, '真实场景不能用两张空白截图验证：' + startPosition + 's frame ' + frame);
                } else references.set(frame, await screenshot(p));
            }
            rows.push(await p.page.evaluate(() => __copies));
            await healthy(p);
        } finally { await p.page.close(); }
    }
    if (startPosition === 36 && !alphaIsolationFrames) {
        assert.equal(rows[0], 90);
        assert.ok(rows[1] <= 1, JSON.stringify(rows));
    } else assert.ok(rows[1] <= rows[0], JSON.stringify(rows));
    console.log('ok DECORATOR ' + startPosition + 's 原样脚本最终画面一致，alpha 隔离采样帧 '
        + alphaIsolationFrames + '，投影回贴 ' + rows[0] + ' → ' + rows[1]);
}

(async () => {
    const host = await createBrowserHost();
    try {
        await realSceneViewport(host);
        await boundaries(host);
        await contextLoss(host);
        await staticResume(host);
        await unavailableGPU(host);
        await dpi(host);
        await invalidMask(host);
        await emptyContainers(host);
        await linearCandidateScan(host);
        await realScene(host);
        await realScene(host, 90, [30, 31, 60]);
        await realScene(host, 110, [30, 31, 60]);
    } finally { await host.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
