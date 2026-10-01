// 分离的亚像素盒式模糊。中间结果保持在 GPU，避免每帧读取整份 Canvas 像素。
// CPU 后备路径使用相同核宽；不依赖 CSS blur 的高斯近似。
var renderer;
var MAX_FILTER_PIXELS = 8 * 1024 * 1024;
var vertexSource = "attribute vec2 position;varying vec2 uv;void main(){gl_Position=vec4(position,0.,1.);uv=position*.5+.5;}";
var fragmentSource = [
    "precision highp float;varying vec2 uv;uniform sampler2D image;uniform sampler2D original;",
    "uniform vec2 extent;uniform vec2 size;uniform vec2 direction;uniform float kernel;uniform float mode;",
    "uniform vec4 glowColor;uniform float strength;uniform float inner;uniform float knockout;",
    "uniform vec4 multiplier;uniform vec4 offset;",
    "vec4 sampleAt(vec2 p){vec2 edge=clamp(min(p+vec2(.5),size+vec2(.5)-p),0.,1.);return texture2D(image,clamp(p,vec2(.5),size-vec2(.5))/size*extent)*edge.x*edge.y;}",
    "void main(){vec2 p=uv*size;vec4 result;",
    "if(mode<.5){float radius=max(0.,(kernel-1.)*.5);float m=max(0.,ceil(radius)-1.);float fraction=radius-m;",
    "result=sampleAt(p-direction*(m+1.))*fraction;for(int i=0;i<MAX_PAIRS;i++){float n=float(i)*2.+.5;if(n>=m*2.)break;result+=sampleAt(p+direction*(n-m))*2.;}",
    "result+=sampleAt(p+direction*(m+fraction/(fraction+1.)))*(fraction+1.);result/=kernel;",
    // 与 Flash 的逐通道固定精度运算对齐，各次模糊后向下取整。
    "result=floor(result*255.+.0001)/255.;}",
    "else if(mode<1.5){vec4 base=texture2D(original,uv);float a=base.a;float g=min(1.,(inner>.5?1.-sampleAt(p).a:sampleAt(p).a)*strength)*glowColor.a;",
    "if(knockout>.5){float ga=inner>.5?g*a:g*(1.-a);result=vec4(glowColor.rgb*ga,ga);}",
    "else if(inner>.5){result=vec4(mix(base.rgb,glowColor.rgb*a,g),a);}",
    "else{float ga=g*(1.-a);result=vec4(base.rgb+glowColor.rgb*ga,a+ga);}}",
    "else result=sampleAt(p);",
    "if(result.a>0.){vec4 straight=vec4(result.rgb/result.a,result.a);straight=clamp(straight*multiplier+offset,0.,1.);result=vec4(straight.rgb*straight.a,straight.a);}",
    "gl_FragColor=result;}"
].join("\n");

function getRenderer() {
    if (renderer !== undefined) return renderer;
    renderer = null;
    var canvas = document.createElement("canvas");
    var gl = canvas.getContext("webgl", { alpha: true, premultipliedAlpha: true, antialias: false });
    if (!gl || typeof gl.createShader !== "function") return null;
    function shader(type, source) {
        var value = gl.createShader(type); gl.shaderSource(value, source); gl.compileShader(value);
        if (!gl.getShaderParameter(value, gl.COMPILE_STATUS)) { gl.deleteShader(value); return null; }
        return value;
    }
    function programFor(limit) {
        var vertex = shader(gl.VERTEX_SHADER, vertexSource), fragment = shader(gl.FRAGMENT_SHADER, fragmentSource.replace("MAX_PAIRS", String(limit)));
        if (!vertex || !fragment) {
            if (vertex) gl.deleteShader(vertex);
            if (fragment) gl.deleteShader(fragment);
            return null;
        }
        var program = gl.createProgram(); gl.attachShader(program, vertex); gl.attachShader(program, fragment);
        gl.bindAttribLocation(program, 0, "position"); gl.linkProgram(program);
        gl.deleteShader(vertex); gl.deleteShader(fragment);
        if (!gl.getProgramParameter(program, gl.LINK_STATUS)) { gl.deleteProgram(program); return null; }
        var uniforms = {};
        ["image", "original", "extent", "size", "direction", "kernel", "mode", "glowColor", "strength", "inner", "knockout", "multiplier", "offset"].forEach(function (name) {
            uniforms[name] = gl.getUniformLocation(program, name);
        });
        return { program: program, uniforms: uniforms };
    }
    var initial = programFor(0);
    if (!initial) return null;
    var buffer = gl.createBuffer(); gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1,-1,1,-1,-1,1,-1,1,1,-1,1,1]), gl.STATIC_DRAW);
    function texture() {
        var value = gl.createTexture(); gl.bindTexture(gl.TEXTURE_2D, value);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        return value;
    }
    renderer = { canvas: canvas, gl: gl, buffer: buffer, programs: new Map([[0, initial]]), programFor: programFor,
        source: texture(), targets: [texture(), texture()],
        framebuffers: [gl.createFramebuffer(), gl.createFramebuffer()], width: 0, height: 0 };
    return renderer;
}

function applyEffectsGPU(canvas, filters, transform, ratio) {
    filters = filters || [];
    if (!filters.length) return false;
    if (filters.some(function (f) { return (f.kind || f.type) !== "BlurFilter" && (f.kind || f.type) !== "GlowFilter"; })) return false;
    var r = getRenderer();
    if (!r || r.gl.isContextLost()) return false;
    function programForKernel(kernel) {
        var pairs = Math.max(0, Math.ceil((kernel - 1) / 2) - 1);
        var limit = [0,4,8,16,32,64,127].find(function (n) { return n >= pairs; });
        if (!r.programs.has(limit)) r.programs.set(limit, r.programFor(limit));
        return r.programs.get(limit);
    }
    // 在修改输入位图前编译全部所需变体，驱动不支持时完整退回 CPU。
    for (var filterIndex = 0; filterIndex < filters.length; filterIndex++) {
        for (var filterAxis = 0; filterAxis < 2; filterAxis++) {
            var filterKernel = Math.max(1, Math.min(255, Number(filterAxis ? filters[filterIndex].blurY : filters[filterIndex].blurX) * ratio || 1));
            if (filterKernel > 1 && !programForKernel(filterKernel)) return false;
        }
    }
    var width = canvas.width, height = canvas.height;
    var capacityWidth = Math.pow(2, Math.ceil(Math.log2(Math.max(width, r.width, 1))));
    var capacityHeight = Math.pow(2, Math.ceil(Math.log2(Math.max(height, r.height, 1))));
    // 横长/竖长缓存交替时不能把两个历史最大边长拼成一张巨型纹理。
    if (capacityWidth * capacityHeight > MAX_FILTER_PIXELS) {
        capacityWidth = Math.pow(2, Math.ceil(Math.log2(Math.max(width, 1))));
        capacityHeight = Math.pow(2, Math.ceil(Math.log2(Math.max(height, 1))));
    }
    // 大字形可能是4096×1405；NPOT纹理无需为二次幂扩容浪费面积。
    if (capacityWidth * capacityHeight > MAX_FILTER_PIXELS && width * height <= MAX_FILTER_PIXELS) {
        capacityWidth = width; capacityHeight = height;
    }
    // 固定上限：两份中间纹理、输入纹理与绘制缓冲均可复用，不随元素数增长。
    if (capacityWidth * capacityHeight > MAX_FILTER_PIXELS || capacityWidth > 4096 || capacityHeight > 4096) return false;
    var gl = r.gl;
    if (capacityWidth !== r.width || capacityHeight !== r.height) {
        r.width = r.canvas.width = capacityWidth; r.height = r.canvas.height = capacityHeight;
        for (var i = 0; i < 2; i++) {
            gl.bindTexture(gl.TEXTURE_2D, r.targets[i]);
            gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, r.width, r.height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
            gl.bindFramebuffer(gl.FRAMEBUFFER, r.framebuffers[i]);
            gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, r.targets[i], 0);
            if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) {
                // 禁用失败载体；不能让下一帧因尺寸未变而误用未完成的FBO。
                renderer = null;
                return false;
            }
        }
    }
    gl.bindBuffer(gl.ARRAY_BUFFER, r.buffer);
    gl.enableVertexAttribArray(0); gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    gl.disable(gl.BLEND); gl.disable(gl.SCISSOR_TEST);
    var context = canvas.getContext("2d");
    function draw(texture, extentX, extentY, target, mode, axis, kernel, filter, colorTransform) {
        var program = mode === 0 ? programForKernel(kernel) : r.programs.get(0);
        gl.useProgram(program.program);
        var u = program.uniforms;
        gl.uniform1i(u.image, 0); gl.uniform1i(u.original, 1); gl.uniform2f(u.size, width, height);
        gl.uniform2f(u.direction, mode === 0 && axis === 0 ? 1 : 0, mode === 0 && axis === 1 ? 1 : 0);
        gl.uniform1f(u.kernel, kernel || 1);
        gl.uniform4f(u.multiplier, 1, 1, 1, 1); gl.uniform4f(u.offset, 0, 0, 0, 0);
        if (filter) {
            var color = filter.color >>> 0;
            gl.uniform4f(u.glowColor, (color >> 16 & 255) / 255, (color >> 8 & 255) / 255, (color & 255) / 255, Math.max(0, Math.min(1, Number(filter.alpha))));
            gl.uniform1f(u.strength, Math.max(0, Number(filter.strength) || 0));
            gl.uniform1f(u.inner, filter.inner ? 1 : 0); gl.uniform1f(u.knockout, filter.knockout ? 1 : 0);
        }
        if (colorTransform) {
            function value(name, fallback) { var v = Number(colorTransform[name]); return isFinite(v) ? v : fallback; }
            gl.uniform4f(u.multiplier, value("redMultiplier", 1), value("greenMultiplier", 1), value("blueMultiplier", 1), value("alphaMultiplier", 1));
            gl.uniform4f(u.offset, value("redOffset", 0) / 255, value("greenOffset", 0) / 255, value("blueOffset", 0) / 255, value("alphaOffset", 0) / 255);
        }
        gl.activeTexture(gl.TEXTURE0); gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.uniform2f(u.extent, extentX, extentY); gl.uniform1f(u.mode, mode);
        gl.bindFramebuffer(gl.FRAMEBUFFER, target === null ? null : r.framebuffers[target]);
        gl.viewport(0, target === null ? r.height - height : 0, width, height);
        gl.drawArrays(gl.TRIANGLES, 0, 6);
    }
    function copyBack() {
        context.save(); context.setTransform(1, 0, 0, 1, 0, 0);
        context.globalAlpha = 1; context.globalCompositeOperation = "copy";
        context.drawImage(r.canvas, 0, 0, width, height, 0, 0, width, height); context.restore();
    }
    for (var index = 0; index < filters.length; index++) {
        var filter = filters[index], kind = filter.kind || filter.type;
        gl.activeTexture(gl.TEXTURE1); gl.bindTexture(gl.TEXTURE_2D, r.source);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true); gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, true);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, canvas);
        var texture = r.source, extentX = 1, extentY = 1, target = 0;
        var quality = Math.max(1, Math.min(15, Math.floor(Number(filter.quality) || 1)));
        for (var pass = 0; pass < quality; pass++) for (var axis = 0; axis < 2; axis++) {
            var kernel = Math.max(1, Math.min(255, Number(axis ? filter.blurY : filter.blurX) * ratio || 1));
            if (kernel <= 1) continue;
            draw(texture, extentX, extentY, target, 0, axis, kernel);
            texture = r.targets[target]; target = 1 - target;
            extentX = width / r.width; extentY = height / r.height;
        }
        draw(texture, extentX, extentY, null, kind === "GlowFilter" ? 1 : 2, 0, 1, kind === "GlowFilter" ? filter : null, index === filters.length - 1 ? transform : null);
        copyBack();
    }
    return true;
}

export { applyEffectsGPU };
