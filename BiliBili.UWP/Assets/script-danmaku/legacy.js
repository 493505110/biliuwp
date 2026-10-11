// 旧 M8 的 ActionScript 语法与内建数组方法。
function normalizeMultilineStrings(code, onWord) {
    let result = "", quote = "", comment = "", regex = false, characterClass = false, expression = true;
    for (let i = 0; i < code.length; i++) {
        const c = code[i], next = code[i + 1];
        if (comment) {
            result += c;
            if (comment === "line" && (c === "\n" || c === "\r")) comment = "";
            else if (comment === "block" && c === "*" && next === "/") { result += next; i++; comment = ""; }
        } else if (quote) {
            if (c === "\\") {
                result += c + (next || ""); i++;
                if (next === "\r" && code[i + 1] === "\n") result += code[++i];
            } else if (c === quote) { result += c; quote = ""; expression = false; }
            else if (quote !== "`" && (c === "\n" || c === "\r")) {
                result += "\\n";
                if (c === "\r" && next === "\n") i++;
            } else result += c;
        } else if (regex) {
            result += c;
            if (c === "\\") { result += next || ""; i++; }
            else if (c === "[") characterClass = true;
            else if (c === "]") characterClass = false;
            else if (c === "/" && !characterClass) { regex = false; expression = false; }
        } else if (c === "/" && (next === "/" || next === "*")) {
            result += c + next; i++; comment = next === "/" ? "line" : "block";
        } else if (c === '"' || c === "'" || c === "`") { quote = c; result += c; }
        else if (c === "/" && expression) { regex = true; result += c; }
        else if (/[a-zA-Z_$]/.test(c)) {
            const start = i;
            let word = c;
            while (i + 1 < code.length && /[\w$]/.test(code[i + 1])) word += code[++i];
            if (onWord) onWord(word, start, i + 1);
            result += word; expression = /^(return|throw|case|delete|void|typeof|new|in|instanceof)$/i.test(word);
        } else {
            result += c;
            if (!/\s/.test(c)) expression = /[=(:,;!&|?{}\[+*%<>~-]/.test(c);
        }
    }
    return result;
}

// 仅用于原始 JS 编译失败后的旧 M8 回退。BPM 使用 If/Return/Else，
// 同时把返回数组写在 return 的下一行；普通 JS 的 ASI 语义必须保留。
function normalizeLegacySyntax(code) {
    const words = [], replacements = [];
    normalizeMultilineStrings(code, (word, start, end) => words.push({ word, start, end }));
    for (const token of words) {
        if (!/^(If|Return|Else)$/.test(token.word)
            || /\.\s*$/.test(code.slice(0, token.start))
            || /^\s*:/.test(code.slice(token.end))
            || /\b(?:var|let|const|function|class)\s*$/.test(code.slice(0, token.start))) continue;
        replacements.push({ start: token.start, end: token.end, text: token.word.toLowerCase() });
    }
    // 只有明确出现旧关键字的脚本才兼容 return 换行，避免改变其它
    // 因多行字符串等原因进入回退的合法 JS 函数返回值。
    if (!replacements.length) return code;
    for (const token of words) {
        if (token.word !== "return" && token.word !== "Return") continue;
        if (/\.\s*$/.test(code.slice(0, token.start))) continue;
        const gap = /^[ \t\r\n]+(?=\[)/.exec(code.slice(token.end));
        if (gap && /[\r\n]/.test(gap[0])) {
            replacements.push({ start: token.end, end: token.end + gap[0].length, text: " " });
        }
    }
    replacements.sort((left, right) => left.start - right.start);
    let result = "", offset = 0;
    for (const entry of replacements) {
        result += code.slice(offset, entry.start) + entry.text;
        offset = entry.end;
    }
    return result + code.slice(offset);
}

// 部分旧作品在函数回调末尾写 break。只修复解析器确认不在循环或 switch
// 内的无标签 break；合法 break、字符串、注释及其它语法错误保持原样。
function normalizeOrphanBreakStatements(code) {
    const breaks = [];
    normalizeMultilineStrings(code, (word, start, end) => {
        if (word === "break" && !/\.\s*$/.test(code.slice(0, start))
            && /^\s*(?:;|}|$)/.test(code.slice(end))) breaks.push({ start, end });
    });
    if (!breaks.length || breaks.length > 256) return code;
    function replace(replacements) {
        let result = "", offset = 0;
        breaks.forEach((entry, index) => {
            result += code.slice(offset, entry.start) + replacements[index];
            offset = entry.end;
        });
        return result + code.slice(offset);
    }
    const masked = breaks.map(() => "void 0");
    try { new Function(replace(masked)); } catch (_) { return code; }
    const replacements = breaks.map((_, index) => {
        const probe = masked.slice();
        probe[index] = "break";
        try { new Function(replace(probe)); return "break"; }
        catch (error) { return error instanceof SyntaxError ? "return" : "break"; }
    });
    return replace(replacements);
}

// 旧弹幕游戏的闪烁、追踪和淡出写法假定屏上始终有普通弹幕。
// 过滤、加载或 seek 后列表可能为空：为这三种已知源码模式补空目标检查。
// 只改编译副本，不按视频/弹幕 ID 特判，也不屏蔽其它空引用的运行错误。
function normalizeLegacyEmptyDanmakuTargets(code) {
    if (!code.includes("danmus")) return code;
    const replacements = [];
    normalizeMultilineStrings(code, (word, start) => {
        if (word !== "random" && word !== "var") return;
        if (/\.\s*$/.test(code.slice(0, start))) return;
        const tail = code.slice(start);
        const flash = word === "random" && /^random\s*=\s*Utils\.rand\(\s*0\s*,\s*danmus\.length\s*\)\s*;\s*danmuShape\s*=\s*danmus\[random\]\.shape\s*;/.test(tail);
        const tracking = word === "var" && /^var\s+i\s*=\s*Utils\.rand\(\s*0\s*,\s*danmus\.length\s*-\s*1\s*\)\s*;\s*current\s*=\s*danmus\[i\]\s*;/.test(tail);
        if (flash || tracking) {
            // 从当前效果回调返回，外层节拍/定时器仍继续；下一次可正常选中目标。
            replacements.push({ start, end: start, text: "if (!danmus.length) return;\n" });
        } else if (word === "var") {
            const fade = /^var\s+alpha\s*=\s*danmus\[0\]\.shape\.alpha\s*;/.exec(tail);
            // 空场以完全不透明为起点，仍注册淡出回调，随后到来的障碍也能淡出。
            if (fade) replacements.push({ start, end: start + fade[0].length,
                text: "var alpha = danmus.length ? danmus[0].shape.alpha : 1;" });
        }
    });
    if (!replacements.length) return code;
    let result = "", offset = 0;
    for (const entry of replacements) {
        result += code.slice(offset, entry.start) + entry.text;
        offset = entry.end;
    }
    return result + code.slice(offset);
}

// 非枚举，保持 for-in / foreach 对数组的行为；只作用于独立的 M8 宿主页。
function sortOn(fields, options) {
    const names = Array.isArray(fields) ? fields : [fields];
    const flags = Array.isArray(options) ? options : names.map(() => Number(options) || 0);
    const mode = flags[0] || 0;
    function compare(left, right) {
        for (let i = 0; i < names.length; i++) {
            const flag = flags[i] || 0;
            let a = left.value[names[i]], b = right.value[names[i]];
            if (flag & 16) { a = Number(a); b = Number(b); }
            else {
                a = String(a); b = String(b);
                if (flag & 1) { a = a.toLowerCase(); b = b.toLowerCase(); }
            }
            const order = a < b ? -1 : a > b ? 1 : 0;
            if (order) return flag & 2 ? -order : order;
        }
        return 0;
    }
    const sorted = Array.from(this, (value, index) => ({ value, index })).sort(compare);
    if (mode & 4) {
        for (let i = 1; i < sorted.length; i++) if (!compare(sorted[i - 1], sorted[i])) return 0;
    }
    if (mode & 8) return sorted.map(entry => entry.index);
    for (let i = 0; i < sorted.length; i++) this[i] = sorted[i].value;
    return this;
}

if (!Array.prototype.sortOn) Object.defineProperty(Array.prototype, "sortOn", { configurable: true, writable: true, value: sortOn });

export { normalizeMultilineStrings, normalizeLegacySyntax, normalizeOrphanBreakStatements, normalizeLegacyEmptyDanmakuTargets, sortOn };
