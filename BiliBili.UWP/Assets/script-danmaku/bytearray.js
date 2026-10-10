// M8 位图资源使用同步的 ByteArray.inflate（RFC 1951 裸 DEFLATE）。
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
        this.position = 0;
        this.endian = "bigEndian";
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
    readUnsignedByte() {
        if (this.bytesAvailable < 1) throw new RangeError("ByteArray 已到末尾");
        return this.data[this.position++];
    }
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
}

export { ScriptByteArray, inflateRaw };
