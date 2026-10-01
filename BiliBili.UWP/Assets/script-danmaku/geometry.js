// Flash 列主序变换；同一套矩阵用于绘制、包围盒与脚本的深度排序。
import { FLASH_PROJECTION_WIDTH, hostState, toFiniteNumber } from "./core.js";

var IDENTITY = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

function multiply(a, b) {
    var out = new Array(16);
    for (var c = 0; c < 4; c++) {
        for (var r = 0; r < 4; r++) {
            out[c * 4 + r] = 0;
            for (var k = 0; k < 4; k++) out[c * 4 + r] += a[k * 4 + r] * b[c * 4 + k];
        }
    }
    return out;
}

function scrollMatrix(matrix, element, scrolled) {
    var rect = scrolled !== false && element.props.scrollRect;
    if (rect) {
        var x = Math.floor(toFiniteNumber(rect.x, 0)), y = Math.floor(toFiniteNumber(rect.y, 0));
        matrix[12] -= matrix[0] * x + matrix[4] * y;
        matrix[13] -= matrix[1] * x + matrix[5] * y;
        matrix[14] -= matrix[2] * x + matrix[6] * y;
    }
    return matrix;
}

function localMatrix(element, scrolled) {
    var p = element.props;
    if (p.matrix3D) return scrollMatrix(p.matrix3D.rawData.slice(), element, scrolled);
    if (p.matrix) {
        var m = p.matrix;
        return scrollMatrix([m.a, m.b, 0, 0, m.c, m.d, 0, 0, 0, 0, 1, 0, m.tx, m.ty, 0, 1], element, scrolled);
    }
    var x = toFiniteNumber(p.rotationX, 0) * Math.PI / 180;
    var y = toFiniteNumber(p.rotationY, 0) * Math.PI / 180;
    var z = toFiniteNumber(p.rotation, 0) * Math.PI / 180;
    var sx = Math.sin(x), cx = Math.cos(x), sy = Math.sin(y), cy = Math.cos(y);
    var sz = Math.sin(z), cz = Math.cos(z);
    var a = toFiniteNumber(p.scaleX, 1), b = toFiniteNumber(p.scaleY, 1), c = toFiniteNumber(p.scaleZ, 1);
    return scrollMatrix([cz * cy * a, sz * cy * a, -sy * a, 0,
        (cz * sy * sx - sz * cx) * b, (sz * sy * sx + cz * cx) * b, cy * sx * b, 0,
        (cz * sy * cx + sz * sx) * c, (sz * sy * cx - cz * sx) * c, cy * cx * c, 0,
        toFiniteNumber(p.x, 0), toFiniteNumber(p.y, 0), toFiniteNumber(p.z, 0), 1], element, scrolled);
}

function worldMatrix(element) {
    var m = IDENTITY.slice();
    for (var node = element; node; node = node.treeParent) m = multiply(localMatrix(node), m);
    return m;
}

function inverse(m) {
    var rows = [];
    for (var r = 0; r < 4; r++) {
        rows[r] = [];
        for (var c = 0; c < 4; c++) rows[r][c] = m[c * 4 + r];
        for (c = 0; c < 4; c++) rows[r][c + 4] = r === c ? 1 : 0;
    }
    for (c = 0; c < 4; c++) {
        var pivot = c;
        for (r = c + 1; r < 4; r++) if (Math.abs(rows[r][c]) > Math.abs(rows[pivot][c])) pivot = r;
        if (Math.abs(rows[pivot][c]) < 1e-12) return null;
        var swap = rows[c]; rows[c] = rows[pivot]; rows[pivot] = swap;
        var divisor = rows[c][c];
        for (var j = 0; j < 8; j++) rows[c][j] /= divisor;
        for (r = 0; r < 4; r++) if (r !== c) {
            var factor = rows[r][c];
            for (j = 0; j < 8; j++) rows[r][j] -= factor * rows[c][j];
        }
    }
    var out = [];
    for (c = 0; c < 4; c++) for (r = 0; r < 4; r++) out.push(rows[r][c + 4]);
    return out;
}

function relativeMatrix(element, target) {
    var inv = target ? inverse(worldMatrix(target)) : IDENTITY;
    return inv ? multiply(inv, worldMatrix(element)) : null;
}

function transformPoint(m, x, y, z) {
    return { x: m[0] * x + m[4] * y + m[8] * z + m[12],
        y: m[1] * x + m[5] * y + m[9] * z + m[13],
        z: m[2] * x + m[6] * y + m[10] * z + m[14] };
}

function has3D(element) {
    var p = element.props;
    return !!(p.matrix3D || p.z || p.rotationX || p.rotationY);
}

function subtree3D(element) {
    if (element.expired || element.props.visible === false || element.props.alpha <= 0) return false;
    if (has3D(element)) return true;
    for (var i = 0; i < element.childList.length; i++) if (subtree3D(element.childList[i])) return true;
    return false;
}

function projectionFor(element) {
    for (var node = element; node; node = node.treeParent) {
        if (node.transformValue && node.transformValue.perspectiveProjection) {
            return node.transformValue.perspectiveProjection;
        }
    }
    var width = hostState.viewportWidth || 1;
    return { focalLength: FLASH_PROJECTION_WIDTH / (2 * Math.tan(55 * Math.PI / 360)),
        projectionCenter: { x: width / 2, y: hostState.viewportHeight / 2 } };
}

function projectPoint(point, projection) {
    var focal = toFiniteNumber(projection.focalLength, 1);
    var depth = focal + point.z;
    var center = projection.projectionCenter || { x: 0, y: 0 };
    return { x: center.x + (point.x - center.x) * focal / depth,
        y: center.y + (point.y - center.y) * focal / depth, depth: depth };
}

function transformedBounds(bounds, matrix) {
    var points = [[bounds.x, bounds.y], [bounds.x + bounds.width, bounds.y],
        [bounds.x + bounds.width, bounds.y + bounds.height], [bounds.x, bounds.y + bounds.height]];
    var left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
    for (var i = 0; i < points.length; i++) {
        var p = transformPoint(matrix, points[i][0], points[i][1], 0);
        left = Math.min(left, p.x); top = Math.min(top, p.y);
        right = Math.max(right, p.x); bottom = Math.max(bottom, p.y);
    }
    return { x: left, y: top, width: right - left, height: bottom - top };
}

// 优先用 GPU 投影；没有 WebGL 时分块为纹理三角形，近裁剪防止坐标爆炸。
function drawProjected(target, source, bounds, matrix, projection) {
    var perspective = Math.abs(matrix[2]) + Math.abs(matrix[6]) > 1e-8;
    var batching = projectionBatch && projectionBatch.target === target;
    if (batching && target.globalCompositeOperation !== "source-over") flushProjectedBatch(target);
    var divisions = perspective ? 8 : 1;
    function vertex(u, v) {
        var p = transformPoint(matrix, bounds.x + u * bounds.width, bounds.y + v * bounds.height, 0);
        p.u = u * source.width; p.v = v * source.height;
        return p;
    }
    function triangle(vertices) {
        var clipped = [], focal = projection.focalLength;
        for (var k = 0; k < vertices.length; k++) {
            var a = vertices[k], b = vertices[(k + 1) % vertices.length];
            var da = focal + a.z, db = focal + b.z;
            if (da >= 1) clipped.push(a);
            if ((da >= 1) !== (db >= 1)) {
                var t = (1 - da) / (db - da);
                clipped.push({ x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t,
                    z: a.z + (b.z - a.z) * t, u: a.u + (b.u - a.u) * t, v: a.v + (b.v - a.v) * t });
            }
        }
        for (k = 1; k + 1 < clipped.length; k++) paintTriangle(clipped[0], clipped[k], clipped[k + 1]);
    }
    function paintTriangle(a, b, c) {
        var pa = projectPoint(a, projection), pb = projectPoint(b, projection), pc = projectPoint(c, projection);
        var du1 = b.u - a.u, dv1 = b.v - a.v, du2 = c.u - a.u, dv2 = c.v - a.v;
        var det = du1 * dv2 - du2 * dv1;
        if (Math.abs(det) < 1e-10) return;
        var ax = ((pb.x - pa.x) * dv2 - (pc.x - pa.x) * dv1) / det;
        var bx = ((pc.x - pa.x) * du1 - (pb.x - pa.x) * du2) / det;
        var ay = ((pb.y - pa.y) * dv2 - (pc.y - pa.y) * dv1) / det;
        var by = ((pc.y - pa.y) * du1 - (pb.y - pa.y) * du2) / det;
        target.save(); target.beginPath();
        target.moveTo(pa.x, pa.y); target.lineTo(pb.x, pb.y); target.lineTo(pc.x, pc.y); target.closePath(); target.clip();
        target.transform(ax, ay, bx, by, pa.x - ax * a.u - bx * a.v, pa.y - ay * a.u - by * a.v);
        target.drawImage(source, 0, 0); target.restore();
    }
    // 仿射平面不拆三角形，避免对角线抗锯齿接缝，也减少绘制次数。
    if (!perspective) {
        var a = projectPoint(vertex(0, 0), projection), b = projectPoint(vertex(1, 0), projection), c = projectPoint(vertex(0, 1), projection);
        if (a.depth < 1) return;
        // 深度恒定的平面投影仍是仿射变换，Canvas 可直接绘制。
        // 先提交前面的透视图元，保证混合平面/透视子树的显示列表顺序。
        if (batching) flushProjectedBatch(target);
        target.save();
        target.transform((b.x - a.x) / source.width, (b.y - a.y) / source.width,
            (c.x - a.x) / source.height, (c.y - a.y) / source.height, a.x, a.y);
        target.drawImage(source, 0, 0); target.restore(); return;
    }
    if (drawProjectedGPU(target, source, bounds, matrix, projection)) return;
    for (var row = 0; row < divisions; row++) for (var col = 0; col < divisions; col++) {
        var a = vertex(col / divisions, row / divisions), b = vertex((col + 1) / divisions, row / divisions);
        var c = vertex((col + 1) / divisions, (row + 1) / divisions), d = vertex(col / divisions, (row + 1) / divisions);
        triangle([a, b, c]); triangle([a, c, d]);
    }
}

var gpuProjector;
var projectionBatch = null;
// 原作两张可见区裁剪后的纹理合计约 68 MiB；64 MiB 会逐帧互相淘汰。
// 仍保留有界预算，给这一工作集及较小图元留出空间。
var PROJECTED_TEXTURE_BUDGET = 128 * 1024 * 1024;
var projectedCrops = new WeakMap();
var projectedSourceActivity = new WeakMap();
var cropRasterVersion = 0;

// 在齐次空间裁剪，近裁剪面的交点仍保留正确的纹理坐标。
// 不能直接用投影后四角的包围盒：平面穿过相机时坐标会翻转。
function visibleTextureRegion(points, projection, left, top, right, bottom) {
    var focal = projection.focalLength, center = projection.projectionCenter;
    var polygon = points.map(function (p, index) {
        var w = focal + p.z;
        return { x: (p.x - center.x) * focal + center.x * w,
            y: (p.y - center.y) * focal + center.y * w, w: w,
            u: index === 1 || index === 2 ? 1 : 0, v: index >= 2 ? 1 : 0 };
    });
    var planes = [function (p) { return p.w - 1; },
        function (p) { return p.x - left * p.w; }, function (p) { return right * p.w - p.x; },
        function (p) { return p.y - top * p.w; }, function (p) { return bottom * p.w - p.y; }];
    for (var i = 0; i < planes.length && polygon.length; i++) {
        var clipped = [], distance = planes[i];
        for (var j = 0; j < polygon.length; j++) {
            var a = polygon[j], b = polygon[(j + 1) % polygon.length], da = distance(a), db = distance(b);
            if (da >= 0) clipped.push(a);
            if ((da >= 0) !== (db >= 0)) {
                var t = da / (da - db), p = {};
                ["x", "y", "w", "u", "v"].forEach(function (key) { p[key] = a[key] + (b[key] - a[key]) * t; });
                clipped.push(p);
            }
        }
        polygon = clipped;
    }
    if (!polygon.length) return null;
    return { left: Math.min.apply(Math, polygon.map(function (p) { return p.u; })),
        top: Math.min.apply(Math, polygon.map(function (p) { return p.v; })),
        right: Math.max.apply(Math, polygon.map(function (p) { return p.u; })),
        bottom: Math.max.apply(Math, polygon.map(function (p) { return p.v; })) };
}

function croppedProjectedSource(source, bounds, points, projection, left, top, right, bottom) {
    // 小纹理无需 UV 多边形裁剪；动态位图直接交给 Canvas→WebGL 上传，
    // 避免每帧多一次 Canvas 复制及跨上下文同步。
    if (source.width * source.height < 1024 * 1024 || source.__m8RasterVersion === undefined) return { source: source, bounds: bounds };
    var frame = hostState.rasterFrame, activity = projectedSourceActivity.get(source);
    if (!activity) {
        activity = { frame: frame, version: source.__m8RasterVersion, dynamic: false };
        projectedSourceActivity.set(source, activity);
    } else if (activity.frame !== frame || activity.version !== source.__m8RasterVersion) {
        activity.dynamic = activity.version !== source.__m8RasterVersion;
        activity.frame = frame; activity.version = source.__m8RasterVersion;
    }
    if (activity.dynamic) return { source: source, bounds: bounds };
    var region = visibleTextureRegion(points, projection, left, top, right, bottom);
    if (!region) return null;
    if (![region.left, region.top, region.right, region.bottom].every(Number.isFinite)) return { source: source, bounds: bounds };
    // 2 像素保留双线性采样边缘；64 像素分档避免小幅运动每帧复制裁剪位图。
    var x = Math.max(0, Math.floor((region.left * source.width - 2) / 64) * 64);
    var y = Math.max(0, Math.floor((region.top * source.height - 2) / 64) * 64);
    var width = Math.min(source.width, Math.ceil((region.right * source.width + 2) / 64) * 64) - x;
    var height = Math.min(source.height, Math.ceil((region.bottom * source.height + 2) / 64) * 64) - y;
    if (width <= 0 || height <= 0) return null;
    if (width * height >= source.width * source.height * 0.75) return { source: source, bounds: bounds };
    var entry = projectedCrops.get(source);
    if (!entry) { entry = { canvas: document.createElement("canvas") }; projectedCrops.set(source, entry); }
    if (entry.x !== x || entry.y !== y || entry.canvas.width !== width || entry.canvas.height !== height ||
        source.__m8RasterVersion === undefined || entry.version !== source.__m8RasterVersion) {
        var canvas = entry.canvas;
        if (canvas.width !== width || canvas.height !== height) { canvas.width = width; canvas.height = height; }
        var context = canvas.getContext("2d");
        context.clearRect(0, 0, width, height);
        context.drawImage(source, x, y, width, height, 0, 0, width, height);
        canvas.__m8RasterVersion = ++cropRasterVersion;
        entry.x = x; entry.y = y; entry.version = source.__m8RasterVersion;
    }
    return { source: entry.canvas, bounds: { x: bounds.x + bounds.width * x / source.width,
        y: bounds.y + bounds.height * y / source.height,
        width: bounds.width * width / source.width, height: bounds.height * height / source.height } };
}

function projectedTexture(renderer, source) {
    var gl = renderer.gl, entry = renderer.textures.get(source);
    if (!entry) {
        entry = { source: source, texture: gl.createTexture(), version: -1, width: 0, height: 0, bytes: 0 };
        renderer.textures.set(source, entry);
        gl.bindTexture(gl.TEXTURE_2D, entry.texture);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE); gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    } else {
        gl.bindTexture(gl.TEXTURE_2D, entry.texture);
        renderer.recentTextures.splice(renderer.recentTextures.indexOf(entry), 1);
    }
    renderer.recentTextures.push(entry);
    if (source.__m8RasterVersion === undefined || entry.version !== source.__m8RasterVersion || entry.width !== source.width || entry.height !== source.height) {
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, source);
        renderer.textureBytes -= entry.bytes;
        entry.width = source.width; entry.height = source.height; entry.bytes = source.width * source.height * 4;
        entry.version = source.__m8RasterVersion;
        renderer.textureBytes += entry.bytes;
    }
    // 显存和条目数均有界；旧视频/seek 的缓存不会永久保留。
    while (renderer.recentTextures.length > 1 && (renderer.textureBytes > PROJECTED_TEXTURE_BUDGET || renderer.recentTextures.length > 1024)) {
        var old = renderer.recentTextures.shift();
        renderer.textureBytes -= old.bytes; renderer.textures.delete(old.source); gl.deleteTexture(old.texture);
    }
}
// 一个 3D 合成层只在遮罩/滤镜/混合边界回贴，避免每个字形触发 GPU→Canvas 同步。
function beginProjectedBatch(target) {
    if (projectionBatch) flushProjectedBatch(projectionBatch.target);
    var parent = projectionBatch;
    projectionBatch = { target: target, count: 0, parent: parent };
}
function flushProjectedBatch(target) {
    if (!projectionBatch || projectionBatch.target !== target || !projectionBatch.count) return;
    target.save(); target.globalAlpha = 1; target.globalCompositeOperation = "source-over";
    target.drawImage(gpuProjector.canvas, -256, -256, hostState.viewportWidth + 512, hostState.viewportHeight + 512);
    target.restore(); projectionBatch.count = 0;
}
function endProjectedBatch(target) {
    flushProjectedBatch(target);
    if (projectionBatch && projectionBatch.target === target) projectionBatch = projectionBatch.parent;
}
// GPU 的齐次插值在整张四边形上连续，不产生 Canvas 三角形裁剪的抗锯齿接缝。
function drawProjectedGPU(target, source, bounds, matrix, projection) {
    if (gpuProjector === undefined) {
        var canvas = document.createElement("canvas");
        var gl = canvas.getContext("webgl", { alpha: true, premultipliedAlpha: true, antialias: true });
        gpuProjector = null;
        if (gl) {
            function shader(type, code) {
                var value = gl.createShader(type); gl.shaderSource(value, code); gl.compileShader(value);
                if (!gl.getShaderParameter(value, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(value));
                return value;
            }
            var program = gl.createProgram();
            gl.attachShader(program, shader(gl.VERTEX_SHADER, "attribute vec4 position;attribute vec2 uv;varying vec2 tex;void main(){gl_Position=position;tex=uv;}"));
            gl.attachShader(program, shader(gl.FRAGMENT_SHADER, "precision mediump float;uniform sampler2D image;uniform float opacity;varying vec2 tex;void main(){vec4 c=texture2D(image,tex);gl_FragColor=vec4(c.rgb*c.a*opacity,c.a*opacity);}"));
            gl.linkProgram(program);
            if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
            gpuProjector = { canvas: canvas, gl: gl, program: program, buffer: gl.createBuffer(),
                textures: new WeakMap(), recentTextures: [], textureBytes: 0,
                position: gl.getAttribLocation(program, "position"), uv: gl.getAttribLocation(program, "uv"), opacity: gl.getUniformLocation(program, "opacity") };
        }
    }
    if (!gpuProjector || gpuProjector.gl.isContextLost()) return false;
    var points = [[bounds.x, bounds.y], [bounds.x + bounds.width, bounds.y],
        [bounds.x + bounds.width, bounds.y + bounds.height], [bounds.x, bounds.y + bounds.height]]
        .map(function (p) { return transformPoint(matrix, p[0], p[1], 0); });
    var projected = points.map(function (p) { return projectPoint(p, projection); });
    if (projected.every(function (p) { return p.depth < 1; })) return true;
    var batch = projectionBatch && projectionBatch.target === target && target.globalCompositeOperation === "source-over" ? projectionBatch : null;
    var left = batch ? -256 : 0, top = batch ? -256 : 0;
    var right = hostState.viewportWidth + (batch ? 256 : 0), bottom = hostState.viewportHeight + (batch ? 256 : 0);
    var cropped = croppedProjectedSource(source, bounds, points, projection, left, top, right, bottom);
    if (!cropped) return true;
    if (cropped.source !== source) {
        source = cropped.source; bounds = cropped.bounds;
        points = [[bounds.x, bounds.y], [bounds.x + bounds.width, bounds.y],
            [bounds.x + bounds.width, bounds.y + bounds.height], [bounds.x, bounds.y + bounds.height]]
            .map(function (p) { return transformPoint(matrix, p[0], p[1], 0); });
        projected = points.map(function (p) { return projectPoint(p, projection); });
    }
    if (projected.every(function (p) { return p.depth >= 1; })) {
        left = Math.max(left, Math.floor(Math.min.apply(Math, projected.map(function (p) { return p.x; })) - 1));
        top = Math.max(top, Math.floor(Math.min.apply(Math, projected.map(function (p) { return p.y; })) - 1));
        right = Math.min(right, Math.ceil(Math.max.apply(Math, projected.map(function (p) { return p.x; })) + 1));
        bottom = Math.min(bottom, Math.ceil(Math.max.apply(Math, projected.map(function (p) { return p.y; })) + 1));
    }
    if (right <= left || bottom <= top) return true;
    if (batch) { left = -256; top = -256; right = hostState.viewportWidth + 256; bottom = hostState.viewportHeight + 256; }
    var width = right - left, height = bottom - top, ratio = window.devicePixelRatio || 1;
    var renderer = gpuProjector, gl = renderer.gl;
    var pixelWidth = Math.ceil(width * ratio), pixelHeight = Math.ceil(height * ratio);
    // 共享绘制缓冲只随视口改变大小。逐图元 resize 会反复分配 GPU 缓冲并强制同步。
    var surfaceWidth = Math.ceil((hostState.viewportWidth + 512) * ratio), surfaceHeight = Math.ceil((hostState.viewportHeight + 512) * ratio);
    if (renderer.canvas.width !== surfaceWidth || renderer.canvas.height !== surfaceHeight) {
        renderer.canvas.width = surfaceWidth; renderer.canvas.height = surfaceHeight;
    }
    gl.viewport(0, surfaceHeight - pixelHeight, pixelWidth, pixelHeight);
    gl.enable(gl.SCISSOR_TEST); gl.scissor(0, surfaceHeight - pixelHeight, pixelWidth, pixelHeight);
    if (!batch || !batch.count) { gl.clearColor(0, 0, 0, 0); gl.clear(gl.COLOR_BUFFER_BIT); }
    if (batch) { gl.enable(gl.BLEND); gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA); }
    else gl.disable(gl.BLEND);
    gl.useProgram(renderer.program); gl.uniform1f(renderer.opacity, batch ? target.globalAlpha : 1);
    var center = projection.projectionCenter, focal = projection.focalLength;
    var vertices = [], uv = [[0, 0], [1, 0], [1, 1], [0, 1]], indices = [0, 1, 2, 0, 2, 3];
    for (var i = 0; i < indices.length; i++) {
        var index = indices[i], p = points[index], w = focal + p.z;
        vertices.push(2 * ((p.x - center.x) * focal + (center.x - left) * w) / width - w,
            w - 2 * ((p.y - center.y) * focal + (center.y - top) * w) / height,
            w - 2, w, uv[index][0], uv[index][1]);
    }
    gl.bindBuffer(gl.ARRAY_BUFFER, renderer.buffer); gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(vertices), gl.STREAM_DRAW);
    gl.enableVertexAttribArray(renderer.position); gl.vertexAttribPointer(renderer.position, 4, gl.FLOAT, false, 24, 0);
    gl.enableVertexAttribArray(renderer.uv); gl.vertexAttribPointer(renderer.uv, 2, gl.FLOAT, false, 24, 16);
    projectedTexture(renderer, source);
    gl.drawArrays(gl.TRIANGLES, 0, 6);
    if (batch) batch.count++;
    else target.drawImage(renderer.canvas, 0, 0, pixelWidth, pixelHeight, left, top, width, height);
    return true;
}

export { IDENTITY, multiply, localMatrix, worldMatrix, relativeMatrix, transformPoint,
    transformedBounds, subtree3D, projectionFor, projectPoint, drawProjected,
    beginProjectedBatch, flushProjectedBatch, endProjectedBatch };
