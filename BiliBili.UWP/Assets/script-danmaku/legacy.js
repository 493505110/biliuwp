// 旧 M8 的 ActionScript 语法与内建数组方法。
function normalizeMultilineStrings(code) {
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
            let word = c;
            while (i + 1 < code.length && /[\w$]/.test(code[i + 1])) word += code[++i];
            result += word; expression = /^(return|throw|case|delete|void|typeof|new|in|instanceof)$/.test(word);
        } else {
            result += c;
            if (!/\s/.test(c)) expression = /[=(:,;!&|?{}\[+*%<>~-]/.test(c);
        }
    }
    return result;
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

export { normalizeMultilineStrings, sortOn };
