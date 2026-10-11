// M8 位图和字体资源使用同步的 ByteArray.inflate / uncompress。
// 浏览器的 DecompressionStream 是异步的，不能在脚本的 setPixels 调用前完成。
const MAX_BYTES = 64 * 1024 * 1024;

function inflateRaw(input) {
    let bit = 0, size = 0, output = new Uint8Array(1024);
    function bits(count) {
        if (bit + count > input.length * 8) throw new Error("DEFLATE 数据不完整");
        let value = 0;
        for (let i = 0; i < count; i++, bit++) value |= (input[bit >> 3] >> (bit & 7) & 1) << i;
        return value;
    }
    function reserve(count) {
        if (size + count > MAX_BYTES) throw new RangeError("ByteArray 解压数据过大");
        if (size + count <= output.length) return;
        const next = new Uint8Array(Math.min(MAX_BYTES, Math.max(output.length * 2, size + count)));
        next.set(output); output = next;
    }
    function tree(lengths) {
        const counts = new Array(16).fill(0), next = [], tables = [];
        for (const length of lengths) {
            if (length < 0 || length > 15) throw new Error("无效的 Huffman 长度");
            if (length) counts[length]++;
        }
        let code = 0;
        for (let length = 1; length <= 15; length++) {
            code = (code + (counts[length - 1] || 0)) << 1;
            if (code + counts[length] > 1 << length) throw new Error("无效的 Huffman 树");
            next[length] = code; tables[length] = new Map();
        }
        lengths.forEach((length, symbol) => { if (length) tables[length].set(next[length]++, symbol); });
        return tables;
    }
    function symbol(tables) {
        let code = 0;
        for (let length = 1; length <= 15; length++) {
            code = code << 1 | bits(1);
            if (tables[length].has(code)) return tables[length].get(code);
        }
        throw new Error("无效的 Huffman 编码");
    }
    const lengthBase = [3,4,5,6,7,8,9,10,11,13,15,17,19,23,27,31,35,43,51,59,67,83,99,115,131,163,195,227,258];
    const lengthExtra = [0,0,0,0,0,0,0,0,1,1,1,1,2,2,2,2,3,3,3,3,4,4,4,4,5,5,5,5,0];
    const distanceBase = [1,2,3,4,5,7,9,13,17,25,33,49,65,97,129,193,257,385,513,769,1025,1537,2049,3073,4097,6145,8193,12289,16385,24577];
    const distanceExtra = [0,0,0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9,10,10,11,11,12,12,13,13];
    let final;
    do {
        final = bits(1);
        const type = bits(2);
        if (type === 0) {
            bit = Math.ceil(bit / 8) * 8;
            const count = bits(16);
            if ((count ^ bits(16)) !== 65535) throw new Error("无效的 DEFLATE 存储块");
            reserve(count);
            for (let i = 0; i < count; i++) output[size++] = bits(8);
            continue;
        }
        if (type === 3) throw new Error("无效的 DEFLATE 块类型");
        let literals, distances;
        if (type === 1) {
            literals = tree(Array.from({ length: 288 }, (_, i) => i < 144 ? 8 : i < 256 ? 9 : i < 280 ? 7 : 8));
            distances = tree(new Array(32).fill(5));
        } else {
            const literalCount = bits(5) + 257, distanceCount = bits(5) + 1, codeCount = bits(4) + 4;
            if (literalCount > 286) throw new Error("无效的 DEFLATE 字面量表");
            const order = [16,17,18,0,8,7,9,6,10,5,11,4,12,3,13,2,14,1,15];
            const codeLengths = new Array(19).fill(0);
            for (let i = 0; i < codeCount; i++) codeLengths[order[i]] = bits(3);
            const codes = tree(codeLengths), lengths = [];
            while (lengths.length < literalCount + distanceCount) {
                const value = symbol(codes);
                if (value < 16) lengths.push(value);
                else {
                    if (value === 16 && !lengths.length) throw new Error("无效的 DEFLATE 重复长度");
                    const count = value === 16 ? bits(2) + 3 : value === 17 ? bits(3) + 3 : bits(7) + 11;
                    const repeated = value === 16 ? lengths[lengths.length - 1] : 0;
                    if (lengths.length + count > literalCount + distanceCount) throw new Error("DEFLATE 长度表越界");
                    for (let i = 0; i < count; i++) lengths.push(repeated);
                }
            }
            if (!lengths[256]) throw new Error("DEFLATE 缺少结束符");
            literals = tree(lengths.slice(0, literalCount)); distances = tree(lengths.slice(literalCount));
        }
        for (;;) {
            const value = symbol(literals);
            if (value === 256) break;
            if (value < 256) { reserve(1); output[size++] = value; continue; }
            const index = value - 257;
            if (index >= lengthBase.length) throw new Error("无效的 DEFLATE 长度");
            const count = lengthBase[index] + bits(lengthExtra[index]), distanceCode = symbol(distances);
            if (distanceCode >= distanceBase.length) throw new Error("无效的 DEFLATE 距离");
            const distance = distanceBase[distanceCode] + bits(distanceExtra[distanceCode]);
            if (distance > size) throw new Error("DEFLATE 距离越界");
            reserve(count);
            for (let i = 0; i < count; i++) { output[size] = output[size - distance]; size++; }
        }
    } while (!final);
    return output.slice(0, size);
}

class ScriptByteArray {
    constructor(bytes) {
        this.data = Array.from(bytes || []);
        if (this.data.length > MAX_BYTES) throw new RangeError("ByteArray 数据过大");
        this.position = 0;
        this.endian = "bigEndian";
        // Flash ByteArray 的数字下标直接访问字节，不推进 position。
        return new Proxy(this, {
            get(target, key, receiver) {
                return isByteIndex(key) ? target.data[Number(key)] : Reflect.get(target, key, receiver);
            },
            set(target, key, value, receiver) {
                if (!isByteIndex(key)) return Reflect.set(target, key, value, receiver);
                const index = Number(key);
                if (index >= MAX_BYTES) throw new RangeError("ByteArray 数据过大");
                if (index >= target.length) {
                    const position = target.position;
                    target.length = index + 1;
                    target.position = position;
                }
                target.data[index] = Number(value) & 255;
                return true;
            }
        });
    }
    get length() { return this.data.length; }
    set length(value) {
        const length = Number(value) >>> 0;
        if (length > MAX_BYTES) throw new RangeError("ByteArray 数据过大");
        const previous = this.data.length;
        this.data.length = length;
        if (length > previous) this.data.fill(0, previous);
        this.position = Math.min(this.position, length);
    }
    get bytesAvailable() { return Math.max(0, this.length - this.position); }
    clear() { this.data = []; this.position = 0; }
    writeByte(value) {
        const position = Number(this.position) >>> 0;
        if (position >= MAX_BYTES) throw new RangeError("ByteArray 数据过大");
        if (position > this.length) this.length = position;
        this.data[position] = value & 255; this.position = position + 1;
    }
    writeUTFBytes(value) {
        // 不写长度前缀；不成对的 UTF-16 代理项按 UTF-8 替换字符编码。
        for (const character of String(value)) {
            let code = character.codePointAt(0);
            if (code >= 0xD800 && code <= 0xDFFF) code = 0xFFFD;
            if (code < 0x80) this.writeByte(code);
            else if (code < 0x800) {
                this.writeByte(0xC0 | code >> 6); this.writeByte(0x80 | code & 63);
            } else if (code < 0x10000) {
                this.writeByte(0xE0 | code >> 12); this.writeByte(0x80 | code >> 6 & 63);
                this.writeByte(0x80 | code & 63);
            } else {
                this.writeByte(0xF0 | code >> 18); this.writeByte(0x80 | code >> 12 & 63);
                this.writeByte(0x80 | code >> 6 & 63); this.writeByte(0x80 | code & 63);
            }
        }
    }
    readUnsignedByte() {
        if (this.bytesAvailable < 1) throw new RangeError("ByteArray 已到末尾");
        return this.data[this.position++];
    }
    readBoolean() { return this.readUnsignedByte() !== 0; }
    readByte() { const value = this.readUnsignedByte(); return value > 127 ? value - 256 : value; }
    readUnsignedShort() { return readNumber(this, 2, "getUint16"); }
    readShort() { return readNumber(this, 2, "getInt16"); }
    readFloat() { return readNumber(this, 4, "getFloat32"); }
    readUnsignedInt() {
        if (this.bytesAvailable < 4) throw new RangeError("ByteArray 已到末尾");
        let value = 0;
        for (let i = 0; i < 4; i++) {
            const byte = this.readUnsignedByte();
            value = this.endian === "littleEndian" ? value | byte << (i * 8) : value << 8 | byte;
        }
        return value >>> 0;
    }
    writeUnsignedInt(value) {
        for (let i = 0; i < 4; i++) this.writeByte(value >>> ((this.endian === "littleEndian" ? i : 3 - i) * 8));
    }
    inflate() {
        const result = inflateRaw(Uint8Array.from(this.data));
        this.data = Array.from(result); this.position = 0;
    }
    uncompress(algorithm = "zlib") {
        if (algorithm === "deflate") { this.inflate(); return; }
        if (algorithm !== "zlib") throw new Error("不支持的 ByteArray 压缩格式：" + algorithm);
        const input = Uint8Array.from(this.data);
        // zlib = CMF/FLG + DEFLATE + 大端 Adler-32；不能直接交给 inflateRaw。
        if (input.length < 6 || (input[0] & 15) !== 8 || input[0] >> 4 > 7
            || ((input[0] << 8) | input[1]) % 31 !== 0 || (input[1] & 32)) {
            throw new Error("无效的 zlib 头或不支持的预设字典");
        }
        const output = inflateRaw(input.subarray(2, input.length - 4));
        let a = 1, b = 0;
        for (const value of output) { a = (a + value) % 65521; b = (b + a) % 65521; }
        const checksum = new DataView(input.buffer, input.byteOffset, input.byteLength).getUint32(input.length - 4);
        if (((b << 16 | a) >>> 0) !== checksum) throw new Error("zlib 校验失败");
        this.data = Array.from(output); this.position = 0;
    }
}

function isByteIndex(key) {
    return typeof key === "string" && /^(0|[1-9][0-9]*)$/.test(key);
}

function readNumber(bytes, size, method) {
    if (bytes.bytesAvailable < size) throw new RangeError("ByteArray 已到末尾");
    const data = Uint8Array.from(bytes.data.slice(bytes.position, bytes.position + size));
    const value = new DataView(data.buffer)[method](0, bytes.endian === "littleEndian");
    bytes.position += size;
    return value;
}

export { ScriptByteArray, inflateRaw };
