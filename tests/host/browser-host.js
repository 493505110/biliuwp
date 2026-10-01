'use strict';
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const { execFileSync } = require('node:child_process');
const { chromium } = require('playwright');
const assets = path.resolve(__dirname, '../../BiliBili.UWP/Assets');
const root = path.resolve(__dirname, '../..');

async function createBrowserHost(revision) {
    const cache = new Map();
    const server = http.createServer((req, res) => {
        const file = path.resolve(assets, '.' + decodeURIComponent(req.url.split('?')[0]));
        if (!file.startsWith(assets + path.sep)) { res.writeHead(403).end(); return; }
        try {
            if (!cache.has(file)) cache.set(file, revision
                ? execFileSync('git', ['show', revision + ':' + path.relative(root, file).replaceAll('\\', '/')], { cwd: root, windowsHide: true })
                : fs.readFileSync(file));
            res.setHeader('Content-Type', file.endsWith('.js') ? 'text/javascript' : 'text/html');
            res.end(cache.get(file));
        } catch { res.writeHead(404).end(); }
    });
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    let browser;
    try {
        const gpuDevice = process.env.M8_GPU_DEVICE;
        const args = gpuDevice ? ['--force-high-performance-gpu'] : [];
        browser = await chromium.launch(process.env.BROWSER_EXECUTABLE
            ? { executablePath: process.env.BROWSER_EXECUTABLE, headless: true, args } : { channel: 'msedge', headless: true, args });
        if (gpuDevice) {
            const probe = await browser.newPage();
            try {
                const renderer = await probe.evaluate(() => {
                    const gl = document.createElement('canvas').getContext('webgl');
                    if (!gl) return '';
                    const info = gl.getExtension('WEBGL_debug_renderer_info');
                    return gl.getParameter(info ? info.UNMASKED_RENDERER_WEBGL : gl.RENDERER);
                });
                if (!renderer.toLowerCase().includes(gpuDevice.toLowerCase())) throw new Error('Requested GPU ' + gpuDevice + ', actual WebGL renderer: ' + renderer);
                console.log('WebGL GPU: ' + renderer);
            } finally { await probe.close(); }
        }
    } catch (error) { if (browser) await browser.close(); await new Promise(resolve => server.close(resolve)); throw error; }
    return {
        async page(width = 800, height = 600, seed = 1, disableWebgl = false) {
            const page = await browser.newPage({ viewport: { width, height } });
            const errors = [];
            page.on('pageerror', error => errors.push(String(error)));
            await page.addInitScript(({ seed, disableWebgl }) => {
                if (disableWebgl) {
                    const getContext = HTMLCanvasElement.prototype.getContext;
                    HTMLCanvasElement.prototype.getContext = function (kind, ...args) {
                        return kind === 'webgl' || kind === 'webgl2' ? null : getContext.call(this, kind, ...args);
                    };
                }
                let clock = 1000, sequence = 0;
                const frames = new Map();
                Math.random = () => { seed = (1664525 * seed + 1013904223) >>> 0; return seed / 4294967296; };
                Object.defineProperty(performance, 'now', { value: () => clock });
                window.requestAnimationFrame = fn => { frames.set(++sequence, fn); return sequence; };
                window.cancelAnimationFrame = id => frames.delete(id);
                window.__stepTo = ms => { clock = 1000 + ms; const callbacks = [...frames.values()]; frames.clear(); callbacks.forEach(fn => fn(clock)); };
                window.__step = ms => window.__stepTo(clock - 1000 + ms);
                window.__messages = [];
                window.chrome = { webview: { postMessage: text => window.__messages.push(JSON.parse(text)) } };
            }, { seed, disableWebgl });
            await page.goto(`http://127.0.0.1:${server.address().port}/script-danmaku-host.html`);
            await page.waitForFunction(() => !!window.scriptDanmakuHost);
            return { page, errors,
                script: code => page.evaluate(code => {
                    scriptDanmakuHost.reset(0, true, 1, true);
                    scriptDanmakuHost.append([{ id: 'pixel', stime: 0, duration: 20, lang: 'js', code }]);
                    window.__step(0);
                }, code),
                update: code => page.evaluate(code => { (0, eval)(code); window.__step(0); }, code),
                pixel: (x, y) => page.evaluate(([x, y]) => [...document.querySelector('#stage canvas').getContext('2d').getImageData(x, y, 1, 1).data], [x, y])
            };
        },
        async close() { await browser.close(); await new Promise(resolve => server.close(resolve)); }
    };
}
module.exports = { createBrowserHost };
