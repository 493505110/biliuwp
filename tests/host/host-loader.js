'use strict';

const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const { spawnSync } = require('node:child_process');

// 保持 node tests/host/*.test.js 的入口不变；VM 原生模块需要显式启用。
function ensureModuleSupport() {
    if (typeof vm.SourceTextModule === 'function') return;
    const result = spawnSync(process.execPath,
        [...process.execArgv, '--experimental-vm-modules', ...process.argv.slice(1)],
        { stdio: 'inherit', env: { ...process.env, NODE_NO_WARNINGS: '1' } });
    if (result.error) throw result.error;
    process.exit(result.status === null ? 1 : result.status);
}

function hostScripts(hostPath) {
    const html = fs.readFileSync(hostPath, 'utf8');
    const scripts = [];
    for (const match of html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)) {
        const source = /\bsrc\s*=\s*["']([^"']+)["']/i.exec(match[1]);
        const module = /\btype\s*=\s*["']module["']/i.test(match[1]);
        scripts.push({
            path: source ? path.resolve(path.dirname(hostPath), source[1]) : hostPath,
            code: source ? null : match[2],
            module: module
        });
    }
    if (scripts.length === 0) throw new Error('宿主 HTML 里找不到 <script>：' + hostPath);
    return scripts;
}

// 使用原生模块链接与求值，覆盖真实的 import/export、循环依赖与初始化顺序。
async function evaluateModule(modulePath, context) {
    const cache = new Map();
    function getModule(filePath) {
        const absolutePath = path.resolve(filePath);
        if (!cache.has(absolutePath)) {
            cache.set(absolutePath, new vm.SourceTextModule(
                fs.readFileSync(absolutePath, 'utf8'),
                { context: context, identifier: absolutePath }));
        }
        return cache.get(absolutePath);
    }
    const entry = getModule(modulePath);
    await entry.link((specifier, referencingModule) => {
        if (!specifier.startsWith('./') && !specifier.startsWith('../')) {
            throw new Error('宿主模块只支持相对依赖：' + specifier);
        }
        return getModule(path.resolve(path.dirname(referencingModule.identifier), specifier));
    });
    await entry.evaluate();
    return entry;
}

async function evaluateHost(hostPath, context) {
    for (const script of hostScripts(hostPath)) {
        if (script.module) {
            await evaluateModule(script.path, context);
        } else {
            // 仍可指定拆分前的 HTML，方便对照旧版行为。
            const code = script.code === null
                ? fs.readFileSync(script.path, 'utf8') : script.code;
            vm.runInContext(code, context, { filename: script.path });
        }
    }
}

// 源码契约按实际入口的依赖图读取，不能用目录扫描把未加载的文件算进去。
function readHostSources(hostPath) {
    const sources = new Map();
    function readModule(filePath) {
        const absolutePath = path.resolve(filePath);
        if (sources.has(absolutePath)) return;
        const code = fs.readFileSync(absolutePath, 'utf8');
        sources.set(absolutePath, code);
        for (const match of code.matchAll(/\bfrom\s*["'](\.[^"']+)["']/g)) {
            readModule(path.resolve(path.dirname(absolutePath), match[1]));
        }
    }
    for (const script of hostScripts(hostPath)) {
        if (script.module) readModule(script.path);
        else sources.set(script.path, script.code === null
            ? fs.readFileSync(script.path, 'utf8') : script.code);
    }
    return sources;
}

module.exports = { ensureModuleSupport, evaluateModule, evaluateHost, readHostSources };
