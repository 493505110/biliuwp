// 声明式 motion、Tween 句柄与组合子。
import {
    DEFAULT_LIFE_TIME_SECONDS,
    DEFAULT_TEXT_FONTSIZE,
    LIFE_TIME_UNBOUNDED,
    MAX_REPEAT_EXPANSION,
    hostState,
    toFiniteNumber
} from "./core.js";
import {
    markItemDirty,
    readMotionDeclaredSeconds,
    setPropertyInternal
} from "./display.js";
import {
    resolveEasing
} from "./easing.js";
import {
    ensureRunning
} from "./lifecycle.js";

// ---- tween 补间 ----
//
// 补间统一成「段（segment）列表」模型：每段有自己的起始偏移与时长，
// 段内按属性记一条轨道 { key, from, to, easing }。M8 的 ITween 组合子
// （serial / parallel / delay / scale / reverse / repeat / slice）都是
// 在这个列表上做变换，最终仍由同一条逐帧插值路径推进。
//
// 为什么不用「每条属性一条轨道」的模型：serial 需要同一属性先后跑两段，
// 老模型在第二段尚未开始时会把它自己的起始值写回去（同帧内后者覆盖前者）。
// 现在按「取最后一个已开始的段」取值，串行/并行都自然成立。
//
// 声明式 motion（M8 的 `motion: {x: {fromValue, toValue, lifeTime, ...}}`）
// 会被展开成等价的段列表：repeat 展开成连续多段，startDelay 变成段的偏移。
// 外层 lifeTime 是补间时长（秒）的缺省值，同时也是元素的寿命；
// 属性级 lifeTime 覆盖外层；startDelay / startDelayMs 是延迟。
// 属性级 fromValue 缺省时从元素当前值出发。
// 注意：原版认的可补间属性只有 x / y / alpha / rotationX / rotationY /
// rotationZ / fontsize（MotionManager.as:19），这里是超集（多出 z / rotation /
// scaleX / scaleY）。原版脚本在宿主上跑不会因此出错，但反向不成立——
// 不要拿"宿主支持补间 scaleX"去推断原版支持。
var TWEEN_KEYS = ["x", "y", "z", "alpha", "rotation", "rotationX", "rotationY",
    "rotationZ", "scaleX", "scaleY", "fontsize"];

function readNumberMember(config, name) {
    if (!config || typeof config !== "object") {
        return NaN;
    }

    var value = config[name];
    if (value === undefined || value === null || value === "") {
        return NaN;
    }

    var number = Number(value);
    return isFinite(number) ? number : NaN;
}

function readSeconds(config, name, fallbackSeconds) {
    var seconds = readNumberMember(config, name);
    return isNaN(seconds) ? fallbackSeconds : Math.max(0, seconds);
}

// lifeTime 的「声明值」：null = 未声明（调用方用默认 3 秒），
// 0 或负数原样返回（调用方按「常驻」处理，见 createTween）。
function readDeclaredSeconds(config, name) {
    var seconds = readNumberMember(config, name);
    return isNaN(seconds) ? null : seconds;
}

function readTrackDurationMs(config, fallbackSeconds) {
    var milliseconds = readNumberMember(config, "lifeTimeMs");
    if (!isNaN(milliseconds)) {
        return Math.max(0, milliseconds);
    }

    return Math.max(0, readSeconds(config, "lifeTime", fallbackSeconds) * 1000);
}

function readTrackDelayMs(config) {
    var milliseconds = readNumberMember(config, "startDelayMs");
    if (!isNaN(milliseconds)) {
        return Math.max(0, milliseconds);
    }

    return Math.max(0, readSeconds(config, "startDelay", 0) * 1000);
}

// repeat 的次数：缺省 1；0 或负数按「无限重复」处理（由元素寿命收口，
// 与 M8 的 ITween 组合子语义一致），实际展开时用 MAX_REPEAT_EXPANSION 兜住。
function readRepeatCount(config) {
    var repeat = readNumberMember(config, "repeat");
    if (isNaN(repeat)) {
        return 1;
    }

    if (repeat <= 0) {
        return Infinity;
    }

    return Math.floor(repeat);
}

// ---- 位图缓存的失效规则 ----
//
// 缓存里存的是「元素内容」的像素，所以只有影响内容的属性变化才需要重建：
//  - 内容类（fontsize 等）：缓存尺寸与绘制结果都会变 → 必须重建。
//  - 变换类（x / y / scale / rotation / alpha / matrix / z / visible）：
//    只改变换矩阵，缓存原样复用 → 不重建（这是保留模式的性能来源）。
// 未知属性按内容类处理：宁可多重建一次，也不能让脚本改了内容却看不到。
var TRANSFORM_ONLY_KEYS = {
    x: true, y: true, z: true,
    alpha: true, scaleX: true, scaleY: true, scaleZ: true,
    rotation: true, rotationX: true, rotationY: true, rotationZ: true,
    matrix: true, visible: true, filters: true,
    // 合成/裁剪类：改变的是「怎么画上去」，不是元素自己的位图内容，
    // 因此复用位图缓存，只把该元素标脏重合成（Flash 的 blendMode 与 mask）。
    blendMode: true, mask: true, scrollRect: true
};

// 脚本直接给元素属性赋值（例如先建元素再设 text 之外的内容属性）走这里。
function markPropertyDirty(element, name) {
    element.propertyDirty[name] = true;
    if (!TRANSFORM_ONLY_KEYS[name]) {
        invalidateElementCache(element);
    }
}

// 内容变了：自己重建缓存，父元素重建复合层。
function invalidateElementCache(element) {
    element.needsCache = true;
    element.painted = false;
    var parent = element.treeParent;
    if (parent && parent !== hostState.rootElement) {
        parent.needsCache = true;
    }
}

// 每帧推进 tween 时值一定在变，逐帧置 needsCache 会让叶子缓存形同虚设，
// 因此这里只在「内容类属性」变化时才失效缓存。
function markTweenKeyDirty(element, key) {
    element.propertyDirty[key] = true;
    if (!TRANSFORM_ONLY_KEYS[key]) {
        invalidateElementCache(element);
    }
}

function applyTweenValue(element, key, value) {
    if (key === "fontsize") {
        if (element.style) {
            element.style.fontsize = value;
        }

        return;
    }

    setPropertyInternal(element, key, value, false);
}

// fromValue 缺省时按元素的当前值推导（原版 M8 的常见写法
// { toValue: Player.width } 没有 fromValue）。fontsize 取文本样式上的值，
// 其余属性取元素自己的属性；元素上本来就没有该属性时视为 0。
function readTweenTargetValue(element, key) {
    if (key === "fontsize") {
        return element.style ? element.style.fontsize : DEFAULT_TEXT_FONTSIZE;
    }

    var value = element.props[key];
    return value === undefined || value === null ? 0 : value;
}

// ---- 段（segment）模型 ----

// 元素寿命 = min(脚本声明的寿命, 条目窗口剩余时间)。
// 没有任何 tween 声明 lifeTime 时，寿命就等于窗口剩余时间——
// 旧实现把缺省硬编码成 3 秒并丢弃脚本声明，于是 lifeTime: 4 的
// 滚动文字在屏幕中间就被摘掉了（见设计文档 §3.1）。
// 注意：传入的 declaredLifeTimeMs 必须是**未经窗口约束**的声明值，
// 否则 min 会退化成「窗口永远胜出」（见 applyElementLifeTime）。
function resolveElementLifeTimeMs(item, declaredLifeTimeMs) {
    var remaining = item.endMs - item.startMs;
    if (!isFinite(remaining) || remaining < 0) {
        remaining = 0;
    }

    if (declaredLifeTimeMs === undefined || declaredLifeTimeMs === null) {
        declaredLifeTimeMs = LIFE_TIME_UNBOUNDED;
    }

    var lifeTimeMs = Math.min(declaredLifeTimeMs, remaining);
    return lifeTimeMs > 0 ? lifeTimeMs : remaining;
}

// 同一元素可能声明多个 tween：寿命取声明值的最大值。
// 声明值单独存在 declaredLifeTimeMs 上，不能与已经并入窗口约束的
// lifeTimeMs 做 max —— 那样窗口值会永远胜出，脚本声明的寿命
// 只能延长、永远无法缩短（lifeTime: 2 的元素会活到窗口结束）。
function applyElementLifeTime(element, declaredLifeTimeMs) {
    var declared = isFinite(element.declaredLifeTimeMs)
        ? Math.max(element.declaredLifeTimeMs, declaredLifeTimeMs)
        : declaredLifeTimeMs;
    element.declaredLifeTimeMs = declared;
    var item = element.ownerItem;
    element.lifeTimeMs = item ? resolveElementLifeTimeMs(item, declared) : declared;
}

// 段：{ offsetMs, durationMs, repeat, tracks: [{key, from, to, easing}] }。
// 元素当前值 = 「已开始的段里最后一个」在本地时间上的插值结果。
// 串行补间因此只要把后一段的 offset 排到前一段之后即可。
function createSegment(offsetMs, durationMs, tracks) {
    var offset = Math.max(0, toFiniteNumber(offsetMs, 0));
    var duration = Math.max(1, toFiniteNumber(durationMs, 1));
    return {
        offsetMs: offset,
        durationMs: duration,
        repeat: 1,
        tracks: tracks,
        totalMs: offset + duration
    };
}

// 深拷贝段列表：句柄之间会互相派生（repeat / reverse / delay / serial…），
// 共享同一批段对象会让派生操作改到源句柄（repeat 会重复插值、
// reverse 会把源句柄的 from/to 一起换掉）。
function cloneSegments(segments) {
    var cloned = [];
    for (var index = 0; index < segments.length; index++) {
        var segment = segments[index];
        var tracks = [];
        for (var trackIndex = 0; trackIndex < segment.tracks.length; trackIndex++) {
            var track = segment.tracks[trackIndex];
            tracks.push({
                key: track.key,
                from: track.from,
                to: track.to,
                easing: track.easing
            });
        }

        var copy = createSegment(segment.offsetMs, segment.durationMs, tracks);
        copy.repeat = segment.repeat;
        cloned.push(copy);
    }

    return cloned;
}

// 原版 MotionManager 的相对坐标语义（判定见 MotionManager.as:317-338、
// 换算见 :181-200）：x / y 的 fromValue 或 toValue 落在开区间 (0,1) 时，
// 按「父容器宽度的百分比」解释。注意 y 乘的也是 parent.width——这是原版的
// 原样行为，不要"修正"。没有父容器时取视口宽度。
function resolveRelativeMotionValue(element, key, value) {
    if ((key !== "x" && key !== "y") || !(value > 0 && value < 1)) {
        return value;
    }

    var containerWidth = hostState.viewportWidth;
    var parentElement = element && element.treeParent;
    if (parentElement) {
        containerWidth = parentElement.layerWidth
            || (parentElement.props && parentElement.props.width)
            || containerWidth;
    }

    return containerWidth * value;
}

function createTweenTrack(config, propertyConfig, key, lifeTimeSeconds, element) {
    if (!propertyConfig || typeof propertyConfig !== "object") {
        return null;
    }

    var fromValue = propertyConfig.fromValue;
    if (fromValue === undefined || fromValue === null) {
        // 缺省即从元素当前值出发，不再报错禁用该条。
        // 相对坐标只在配置里**显式**给出数值时成立（原版判定的也是配置值），
        // 由当前值推导出来的 fromValue 不参与换算。
        fromValue = readTweenTargetValue(element, key);
    } else {
        fromValue = resolveRelativeMotionValue(element, key, Number(fromValue));
    }

    var toValue = propertyConfig.toValue;
    if (toValue === undefined || toValue === null) {
        toValue = fromValue;
    } else {
        toValue = resolveRelativeMotionValue(element, key, Number(toValue));
    }

    var from = Number(fromValue);
    var to = Number(toValue);
    if (!isFinite(from) || !isFinite(to)) {
        throw new Error("tween " + key + " 的 fromValue/toValue 不是有效数字");
    }

    return {
        key: key,
        from: from,
        to: to,
        easing: resolveEasing(propertyConfig.easing)
    };
}

// 声明式补间的时长/寿命来自同一处声明，但**不能**混为一谈：
// 时长必须有限（常驻元素也得先把这次补间跑完），寿命才是元素的存续期。
//
// 返回 null = 未声明（调用方保留元素已有寿命）；
// Infinity = 常驻（lifeTime: 0）；负数按原版夹成 0.001 秒（见下）。
function resolveDeclaredLifeTimeMs(declaredSeconds) {
    if (declaredSeconds === null) {
        return null;
    }

    if (declaredSeconds === 0) {
        return LIFE_TIME_UNBOUNDED;
    }

    // 原版 setupMotionElement（ScriptDisplay.as:238-240）：
    //   if(motionConfig.lifeTime < 0) motionConfig.lifeTime = 0.001;
    // 负数是「立刻到期」，不是「常驻」——早先把 <= 0 一起并进常驻，
    // 语义正好反了。
    if (declaredSeconds < 0) {
        return 1;
    }

    return declaredSeconds * 1000;
}

// 把声明式 motion 展开成段列表。
//  - 每条属性一条轨道；同一属性的 repeat 展开成连续多段
//    （M8 的 `repeat` 是「整条补间重复」，因此每段都从 fromValue 重新开始）。
//  - startDelay 只作用于第一段。
//  - 一条属性都没有时返回空列表：调用方（createTween）据此判定配置无效。
function buildMotionSegments(config, options, element) {
    var declaredLifeTimeSeconds = readDeclaredSeconds(config, "lifeTime");
    if (declaredLifeTimeSeconds === null && options && typeof options === "object") {
        declaredLifeTimeSeconds = readDeclaredSeconds(options, "lifeTime");
    }

    var unboundedLifeTime = declaredLifeTimeSeconds !== null
        && declaredLifeTimeSeconds <= 0;
    // 轨道补间时长仍要有有限值：常驻元素也得先把这次补间跑完。
    var lifeTimeSeconds = declaredLifeTimeSeconds === null || unboundedLifeTime
        ? DEFAULT_LIFE_TIME_SECONDS
        : declaredLifeTimeSeconds;

    var segments = [];
    var index;
    for (index = 0; index < TWEEN_KEYS.length; index++) {
        var key = TWEEN_KEYS[index];
        var propertyConfig = config[key];
        if (propertyConfig === undefined || propertyConfig === null) {
            continue;
        }

        var track = createTweenTrack(config, propertyConfig, key, lifeTimeSeconds, element);
        if (!track) {
            continue;
        }

        var durationMs = readTrackDurationMs(propertyConfig, lifeTimeSeconds);
        var delayMs = readTrackDelayMs(propertyConfig);
        var repeat = readRepeatCount(propertyConfig);
        // 无限重复展开成有限段：元素寿命是真正的收口（MAX_REPEAT_EXPANSION 兜底）。
        var count = isFinite(repeat) ? repeat : MAX_REPEAT_EXPANSION;
        if (count > MAX_REPEAT_EXPANSION) {
            count = MAX_REPEAT_EXPANSION;
        }

        for (var repeatIndex = 0; repeatIndex < count; repeatIndex++) {
            segments.push(createSegment(
                repeatIndex === 0 ? delayMs : 0,
                durationMs,
                [track]));
        }
    }

    return {
        // 调用方（createTween / createDrivenHandle）只需要段列表，
        // 寿命声明由它们自己从 config / options 上读——
        // 这里不再回传一份，免得两处判定分叉。
        segments: segments
    };
}

// 声明式 motion 的时间轴长度（元素寿命要用它）。
function motionTotalMs(segments) {
    var total = 0;
    for (var index = 0; index < segments.length; index++) {
        if (segments[index].totalMs > total) {
            total = segments[index].totalMs;
        }
    }

    return total;
}

// ---- tween 对象（M8 的 ITween 控制句柄）----
//
// 控制句柄只做「时间轴换算」：所有补间仍由 advanceElementMotion 逐帧
// 插值，句柄不引入第二条驱动路径。速度/延迟/反向/截取都是把源时间轴
// 映射成一条新的段列表；串行/并行则是段的拼接。
function createTweenHandle(segments, durationMs, elements) {
    var handle = {
        segments: segments,
        durationMs: Math.max(0, toFiniteNumber(durationMs, 0)),
        // 这条补间作用在哪些元件上：createDrivenHandle 给单个元件，
        // 组合子沿用来源句柄的元件集合（serial(t1, reverse(t1)) 是同一元件）。
        elements: elements || [],
        state: "idle",
        // 句柄自己的时间轴（毫秒），由宿主逐帧推进。play() 起算。
        timeMs: 0,
        // M8 的 stopOnComplete：true = 跑完即停（不循环）。
        stopOnComplete: true,
        // 是否已经把自己的段列表装到 element.motion 上（见 installTweenHandle）。
        installed: false,
        // 推进到 durationMs 时是否需要把「段末值」精确落定一次。
        needsFinalApply: false,
        // ownerItem 由 installTweenHandle 按元件归属补上。
        ownerItem: null,
        play: function () {
            // 组合子的结果可能还没装到元件上（组合子是纯时间轴运算），
            // 这里在开始播放时统一安装：接管来源句柄的元件。
            installTweenHandle(handle);
            handle.state = "playing";
            handle.needsFinalApply = false;
            handle.ownerItem = firstHandleItem(handle) || handle.ownerItem;
            hostState.dirty = true;
            if (handle.ownerItem) {
                ensureRunning();
            }

            return handle;
        },
        stop: function () {
            handle.state = "stopped";
            return handle;
        },
        togglePause: function () {
            if (handle.state === "playing") {
                handle.state = "paused";
            } else if (handle.state === "paused") {
                handle.state = "playing";
            }

            return handle;
        },
        gotoAndPlay: function (timeSeconds) {
            installTweenHandle(handle);
            handle.timeMs = normalizeHandleTime(handle, timeSeconds);
            handle.state = "playing";
            handle.needsFinalApply = true;
            hostState.dirty = true;
            if (handle.ownerItem) {
                ensureRunning();
            }

            return handle;
        },
        gotoAndStop: function (timeSeconds) {
            installTweenHandle(handle);
            handle.timeMs = normalizeHandleTime(handle, timeSeconds);
            handle.state = "stopped";
            handle.needsFinalApply = true;
            hostState.dirty = true;
            if (handle.ownerItem) {
                ensureRunning();
            }

            return handle;
        }
    };

    return handle;
}

function firstHandleItem(handle) {
    for (var index = 0; index < handle.elements.length; index++) {
        if (handle.elements[index].ownerItem) {
            return handle.elements[index].ownerItem;
        }
    }

    return null;
}

// 把句柄的段列表装到它作用的元件上。
//
// 组合子（serial / parallel / delay / …）只做时间轴运算，结果一开始
// 没有元件；等 play() / gotoAndPlay() 时才在这里接管：
//  - 先把这些元件上**原有的句柄驱动**停掉（来源句柄的段已被合并进
//    新的段列表），否则两条句柄会各自写同一批属性；
//  - 再把合并后的段列表挂成该元件的 motion，让逐帧路径统一插值。
function installTweenHandle(handle) {
    if (handle.installed || handle.elements.length === 0) {
        return handle;
    }

    handle.installed = true;
    for (var index = 0; index < handle.elements.length; index++) {
        var element = handle.elements[index];
        if (!element || !element.kind) {
            continue;
        }

        supersedeElementHandle(element, handle);
        element.motion = {
            segments: handle.segments,
            // 句柄补间的时长由句柄自己收口，不改元素寿命
            // （元素寿命仍由创建参数的 lifeTime / 条目窗口决定）。
            lifeTimeMs: null,
            totalMs: handle.durationMs,
            lastElapsedMs: -1,
            appliedOnce: false,
            persistent: false,
            handle: handle
        };
        element.hasTween = true;
        if (element.ownerItem) {
            element.ownerItem.hasTween = true;
            if (element.ownerItem.tweenHandles.indexOf(handle) < 0) {
                element.ownerItem.tweenHandles.push(handle);
            }

            handle.ownerItem = element.ownerItem;
        }
    }

    return handle;
}

// 元件被新句柄接管：旧句柄停在原地并退出逐帧推进表，
// 否则它会继续按自己的时间轴写同一批属性。
function supersedeElementHandle(element, nextHandle) {
    var motion = element.motion;
    if (!motion || !motion.handle || motion.handle === nextHandle) {
        return;
    }

    motion.handle.state = "stopped";
    var item = element.ownerItem;
    if (!item) {
        return;
    }

    var index = item.tweenHandles.indexOf(motion.handle);
    if (index >= 0) {
        item.tweenHandles.splice(index, 1);
    }
}

// gotoAndPlay / gotoAndStop 的入参是「秒」，超出范围按 M8 的钳制行为处理。
function normalizeHandleTime(handle, timeSeconds) {
    var milliseconds = toFiniteNumber(timeSeconds, 0) * 1000;
    if (milliseconds < 0) {
        return 0;
    }

    if (milliseconds > handle.durationMs) {
        return handle.durationMs;
    }

    return milliseconds;
}

function registerHandle(ownerItem, handle) {
    if (ownerItem && ownerItem.tweenHandles.indexOf(handle) < 0) {
        ownerItem.tweenHandles.push(handle);
    }

    return handle;
}

// 组合子沿用来源句柄的元件集合：serial(t1, reverse(t1)) 是同一元件，
// parallel(t1, t2) 可能是两个元件，取其并集。
function unionHandleElements() {
    var result = [];
    for (var index = 0; index < arguments.length; index++) {
        var handle = arguments[index];
        if (!handle || !handle.elements) {
            continue;
        }

        for (var elementIndex = 0; elementIndex < handle.elements.length; elementIndex++) {
            var element = handle.elements[elementIndex];
            if (result.indexOf(element) < 0) {
                result.push(element);
            }
        }
    }

    return result;
}

// 逐帧推进句柄：只走时间轴，真正改元素属性仍在 applyMotion 里。
function advanceTweenHandle(handle, deltaMs) {
    if (handle.state !== "playing") {
        return false;
    }

    if (handle.timeMs >= handle.durationMs) {
        if (handle.stopOnComplete) {
            handle.state = "stopped";
        } else {
            handle.timeMs = 0;
        }

        return false;
    }

    handle.timeMs += deltaMs;
    if (handle.timeMs >= handle.durationMs) {
        handle.timeMs = handle.durationMs;
        handle.needsFinalApply = true;
        if (handle.stopOnComplete) {
            handle.state = "stopped";
        }
    }

    return true;
}

// 句柄的时间轴映射到段列表：offset 统一按 scale 缩放，时长按 scale 拉伸。
// 先深拷贝：派生句柄绝不能改到源句柄的段对象。
function mapSegments(segments, scale, offsetMs) {
    var mapped = cloneSegments(segments);
    var offset = Math.max(0, toFiniteNumber(offsetMs, 0));
    var factor = toFiniteNumber(scale, 1);
    if (factor === 0) {
        factor = 1;
    }

    for (var index = 0; index < mapped.length; index++) {
        var segment = mapped[index];
        segment.offsetMs = segment.offsetMs * factor + offset;
        segment.durationMs = segment.durationMs * factor;
        segment.totalMs = segment.offsetMs + segment.durationMs;
    }

    return mapped;
}

// 反向：段序倒转、时间轴镜像，且段内 from/to 互换。
// M8 的 reverse 把整条效果倒着放，所以时间轴要一起镜像。
function reverseSegments(segments, durationMs) {
    var reversed = [];
    for (var index = segments.length - 1; index >= 0; index--) {
        var segment = segments[index];
        var tracks = [];
        for (var trackIndex = 0; trackIndex < segment.tracks.length; trackIndex++) {
            var track = segment.tracks[trackIndex];
            tracks.push({
                key: track.key,
                from: track.to,
                to: track.from,
                easing: track.easing
            });
        }

        var offset = durationMs - (segment.offsetMs + segment.durationMs);
        var copy = createSegment(offset, segment.durationMs, tracks);
        copy.repeat = segment.repeat;
        reversed.push(copy);
    }

    return reversed;
}

function createTween(element, config, options) {
    if (!element) {
        throw new Error("tween 需要一个元素");
    }

    if (!config || typeof config !== "object") {
        throw new Error("tween 需要补间配置");
    }

    var built = buildMotionSegments(config, options, element);
    if (built.segments.length === 0) {
        throw new Error("tween 配置里没有任何可补间的属性");
    }

    // 寿命声明值的来源，按优先级：外层（create 参数 / options）→
    // motion 各属性上声明值的最大值。
    // 两处都没有声明时**不动元素寿命**：元素活到条目窗口兜底上限
    // （由 applyCreateOptions 或 registerItemElement 决定），补间时长
    // 照旧用缺省 3 秒。这里绝不能把「补间时长的缺省 3 秒」当成
    // 寿命声明写进去——applyElementLifeTime 取声明的最大值，
    // 会把脚本真正声明的 lifeTime: 2 顶成 3000ms（D3 的第二例）。
    var declaredSeconds = readDeclaredSeconds(config, "lifeTime");
    if (declaredSeconds === null && options && typeof options === "object") {
        declaredSeconds = readDeclaredSeconds(options, "lifeTime");
    }

    if (declaredSeconds === null) {
        declaredSeconds = readMotionDeclaredSeconds(config);
    }

    // lifeTime: 0 = 常驻，负数 = 立刻到期（原版 ScriptDisplay.as:238-240
    // 把负数夹成 0.001 秒）。两者都算「已声明」，见 resolveDeclaredLifeTimeMs。
    var lifeTimeMs = resolveDeclaredLifeTimeMs(declaredSeconds);

    var motion = {
        segments: built.segments,
        // 脚本声明的寿命（毫秒）。它只作声明记录，元素的真实寿命
        // 还要受条目窗口剩余时间约束，见 resolveElementLifeTimeMs。
        // null = 本次补间没声明寿命，保留元素已有的值。
        lifeTimeMs: lifeTimeMs,
        totalMs: motionTotalMs(built.segments),
        lastElapsedMs: -1,
        appliedOnce: false,
        // 「常驻」声明（M8 的 lifeTime: 0）：元素活到条目窗口兜底上限，
        // 而不是补间跑完就到期。
        persistent: lifeTimeMs === LIFE_TIME_UNBOUNDED
    };

    element.motion = motion;
    element.hasTween = true;
    element.ownerItem.hasTween = true;

    // lifeTimeMs 为 null = 本次补间没有声明寿命，保留元素已有的寿命
    // （创建参数声明的值、或条目窗口剩余时间）。
    if (motion.lifeTimeMs !== null) {
        applyElementLifeTime(element, motion.lifeTimeMs);
    }

    // 立即套用 t=0 的插值：脚本执行完那一刻元素就应在起始位置，
    // 且此时属性尚未被脚本赋值覆盖（先建元素再 tween 的写法两者一致）。
    applyMotion(motion, element, 0);
    return motion;
}

// 取「最早开始」的段：所有段都还没开始时（延迟期间）用它落位起始值。
function findEarliestSegment(segments) {
    var earliest = null;
    for (var index = 0; index < segments.length; index++) {
        if (!earliest || segments[index].offsetMs < earliest.offsetMs) {
            earliest = segments[index];
        }
    }

    return earliest;
}

// 把 tween 在 elapsedMs 时刻的值写到元素上。
//
// 取值规则：**每个属性各认一个段**——从最后一段往前找，第一个
// 「已开始且含该属性」的段胜出（串行补间里后一段的起点值才是当前值），
// 同一属性只写一次。
//
// 不能只取「最后一个已开始的段」：并行补间（parallel，或声明式 motion
// 里多条属性各自带 duration）会让多个段共享同一个 offset，
// 只取最后一个的话，先声明的属性永远不会被写（D13 的 parallel 用例）。
function applyMotion(motion, element, elapsedMs) {
    var segments = motion.segments;
    var appliedKeys = null;
    var applied = false;
    var index;
    var trackIndex;

    for (index = segments.length - 1; index >= 0; index--) {
        var segment = segments[index];
        if (elapsedMs < segment.offsetMs) {
            continue;
        }

        var local = elapsedMs - segment.offsetMs;
        if (local > segment.durationMs) {
            local = segment.durationMs;
        }

        for (trackIndex = 0; trackIndex < segment.tracks.length; trackIndex++) {
            var track = segment.tracks[trackIndex];
            if (appliedKeys && appliedKeys[track.key]) {
                continue;
            }

            if (!appliedKeys) {
                appliedKeys = {};
            }

            appliedKeys[track.key] = true;
            applyTweenValue(element, track.key, track.easing(
                local,
                track.from,
                track.to - track.from,
                segment.durationMs));
            markTweenKeyDirty(element, track.key);
            applied = true;
        }
    }

    if (!applied) {
        // 全部段都还没开始（延迟期间）：按最早那段的起始值落位，
        // 否则元素会停在上一次写入的值上。
        var first = findEarliestSegment(segments);
        if (!first) {
            return;
        }

        for (trackIndex = 0; trackIndex < first.tracks.length; trackIndex++) {
            applyTweenValue(element, first.tracks[trackIndex].key, first.tracks[trackIndex].from);
            markTweenKeyDirty(element, first.tracks[trackIndex].key);
        }
    }

    markItemDirty(element.ownerItem);
}

// 只推进 tween，不做任何绘制——这是每帧唯一会跑脚本「声明」的地方。
function advanceMotion(motion, element, elapsedMs) {
    if (motion.lastElapsedMs === elapsedMs) {
        return false;
    }

    motion.lastElapsedMs = elapsedMs;
    applyMotion(motion, element, elapsedMs);
    return true;
}

function advanceElementMotion(element, elapsedMs) {
    if (!element.hasTween || !element.motion) {
        return false;
    }

    return advanceMotion(element.motion, element, elapsedMs);
}

// ---- Tween.* 句柄的逐帧驱动 ----

// 宿主逐帧推进本条目所有句柄；被句柄驱动的 motion 由句柄写回元素。
function advanceItemHandles(item, deltaMs) {
    var handles = item.tweenHandles;
    if (!handles || handles.length === 0) {
        return;
    }

    for (var index = 0; index < handles.length; index++) {
        var handle = handles[index];
        var before = handle.timeMs;
        advanceTweenHandle(handle, deltaMs);
        // 首次 play() 之前不写元素属性（M8 语义）。
        if (handle.state === "idle") {
            continue;
        }

        if (handle.timeMs === before && !handle.needsFinalApply) {
            continue;
        }

        // 一条句柄可能作用在多个元件上（parallel 复用同一元件时不会，
        // 但组合子可能把不同元件的段并到一起）。
        for (var elementIndex = 0; elementIndex < handle.elements.length; elementIndex++) {
            var element = handle.elements[elementIndex];
            if (!element || !element.motion || element.motion.handle !== handle) {
                continue;
            }

            element.motion.lastElapsedMs = -1;
            advanceMotion(element.motion, element, handle.timeMs);
        }

        handle.needsFinalApply = false;
    }
}

// 被句柄驱动的元素不再按条目进度插值，避免两套时间轴互相打架。
function isMotionHandleDriven(element) {
    return !!(element.motion && element.motion.handle);
}

// ---- Tween.* 组合子 ----

function handleSegments(value) {
    if (!value || !value.segments) {
        throw new Error("Tween 组合子需要一个补间对象");
    }

    return value.segments;
}

function handleDuration(value) {
    return value && isFinite(value.durationMs) ? value.durationMs : 0;
}

// 把段列表截到 [fromMs, toMs] 区间内（slice 用）。
// 段被从中间切开时，端点值要按原缓动在该时刻的取值重算，
// 否则截出来的效果会从错误的起点开始。
function clipSegments(segments, fromMs, toMs) {
    var clipped = [];
    for (var index = 0; index < segments.length; index++) {
        var segment = segments[index];
        var start = segment.offsetMs;
        var end = segment.offsetMs + segment.durationMs;
        if (end <= fromMs || start >= toMs) {
            continue;
        }

        var localFrom = Math.max(start, fromMs) - start;
        var localTo = Math.min(end, toMs) - start;
        if (localTo <= localFrom) {
            continue;
        }

        var tracks = [];
        for (var trackIndex = 0; trackIndex < segment.tracks.length; trackIndex++) {
            var track = segment.tracks[trackIndex];
            tracks.push({
                key: track.key,
                from: track.easing(localFrom, track.from, track.to - track.from, segment.durationMs),
                to: track.easing(localTo, track.from, track.to - track.from, segment.durationMs),
                easing: track.easing
            });
        }

        var copy = createSegment(
            Math.max(start, fromMs) - fromMs,
            localTo - localFrom,
            tracks);
        copy.repeat = segment.repeat;
        clipped.push(copy);
    }

    return clipped;
}

// BetweenAS3 的静态工厂里，宿主此前只实现了 M8 脚本最常用的那一批
// （tween / to / 组合子）。原版 org.libspark.betweenas3.BetweenAS3
// （:58-222）还有下面这些公开静态方法，脚本在用 `Tween` 这个名字
// （宿主注入的就是 BetweenAS3 本体）时都可能撞上：全是时间轴运算，
// 直接复用同一套段列表模型即可。
var Tween = {
    // 与 M8 文档同签名；src 为空时等价于 to()。
    tween: function (object, dest, src, duration, easing) {
        return createDrivenHandle(object, buildTweenConfig(dest, src, easing), duration);
    },
    to: function (object, dest, duration, easing) {
        var config = buildTweenConfig(dest, null, easing);
        return createDrivenHandle(object, config, duration);
    },
    // from(object, src, duration, easing)：从 src 出发、补到元素**当前值**。
    // 与原版的差别记在这里：原版是播放时才读当前值，宿主在建句柄时就
    // 把当前值固化成 toValue——句柄的 play() 起算语义不变，
    // 只是「目标值」在创建时刻确定（M8 脚本的写法都是建完立刻 play）。
    from: function (object, src, duration, easing) {
        return createDrivenHandle(
            object,
            buildFromTweenConfig(object, src, easing),
            duration);
    },
    // apply(object, dest)：不建句柄，立即把 dest 落到对象上
    // （原版 BetweenAS3.apply 是「duration 为 1 但立刻生效」的便利方法）。
    // 只支持元素：宿主的一切可补间量都是元素属性。
    apply: function (object, dest) {
        if (!object || !dest || typeof dest !== "object") {
            return;
        }

        for (var name in dest) {
            if (Object.prototype.hasOwnProperty.call(dest, name)) {
                applyTweenValue(object, name, dest[name]);
            }
        }
    },
    // ---- 组合子 ----
    //
    // 组合子只做「段列表的时间轴运算」，返回的句柄**不带元件**：
    // 元件集合沿用来源句柄（见 unionHandleElements），等 play() 时
    // 由 installTweenHandle 统一装到元件上。这样 serial(t1, reverse(t1))
    // 这类写法不会让两条句柄同时写同一批属性。
    delay: function (src, delaySeconds) {
        var delayMs = Math.max(0, toFiniteNumber(delaySeconds, 0) * 1000);
        return createTweenHandle(
            mapSegments(handleSegments(src), 1, delayMs),
            handleDuration(src) + delayMs,
            unionHandleElements(src));
    },
    scale: function (src, scale) {
        var factor = toFiniteNumber(scale, 1);
        if (factor <= 0) {
            factor = 1;
        }

        return createTweenHandle(
            mapSegments(handleSegments(src), factor, 0),
            handleDuration(src) * factor,
            unionHandleElements(src));
    },
    reverse: function (src) {
        // 时间轴镜像 + 段内取反：整条效果倒着放（深拷贝，不动源句柄）。
        var duration = handleDuration(src);
        return createTweenHandle(
            reverseSegments(cloneSegments(handleSegments(src)), duration),
            duration,
            unionHandleElements(src));
    },
    repeat: function (src, times) {
        var count = toFiniteNumber(times, 1);
        if (count <= 0) {
            count = 1;
        }

        // 深拷贝源段列表：句柄之间会互相派生，共享段对象会让
        // repeat / reverse 之类的操作改到源句柄（详见 cloneSegments）。
        var source = cloneSegments(handleSegments(src));
        var duration = handleDuration(src);
        var segments = [];
        for (var index = 0; index < count; index++) {
            segments = segments.concat(mapSegments(source, 1, duration * index));
        }

        return createTweenHandle(segments, duration * count, unionHandleElements(src));
    },
    slice: function (src, from, to) {
        var duration = handleDuration(src);
        var fromMs = Math.max(0, toFiniteNumber(from, 0) * 1000);
        var toMs = Math.max(fromMs, toFiniteNumber(to, 0) * 1000);
        if (toMs > duration) {
            toMs = duration;
        }

        return createTweenHandle(
            clipSegments(cloneSegments(handleSegments(src)), fromMs, toMs),
            toMs - fromMs,
            unionHandleElements(src));
    },
    serial: function () {
        var segments = [];
        var cursor = 0;
        for (var index = 0; index < arguments.length; index++) {
            // 深拷贝：串行句柄不能与源句柄共享段对象（见 cloneSegments）。
            segments = segments.concat(
                mapSegments(cloneSegments(handleSegments(arguments[index])), 1, cursor));
            cursor += handleDuration(arguments[index]);
        }

        return createTweenHandle(segments, cursor, unionHandleElements.apply(null, arguments));
    },
    parallel: function () {
        var segments = [];
        var longest = 0;
        for (var index = 0; index < arguments.length; index++) {
            // 深拷贝：并行句柄不能与源句柄共享段对象（见 cloneSegments）。
            segments = segments.concat(cloneSegments(handleSegments(arguments[index])));
            var duration = handleDuration(arguments[index]);
            if (duration > longest) {
                longest = duration;
            }
        }

        return createTweenHandle(segments, longest, unionHandleElements.apply(null, arguments));
    },
    // 原版 BetweenAS3 的 serial / parallel 是 (…rest:ITween)，返回 ITweenGroup；
    // 另有 serialTweens / parallelTweens 接受**数组**（BetweenAS3.as:78、:222）。
    // 宿主内部就是同一件事，数组形态直接展开转发。
    serialTweens: function (list) {
        return Tween.serial.apply(null, list || []);
    },
    parallelTweens: function (list) {
        return Tween.parallel.apply(null, list || []);
    }
};

// 把 M8 的 (dest, src, easing) 形式转成宿主内部的 motion 配置。
function buildTweenConfig(dest, src, easing) {
    var config = {};
    var name;
    if (dest && typeof dest === "object") {
        for (name in dest) {
            if (Object.prototype.hasOwnProperty.call(dest, name)) {
                config[name] = { fromValue: undefined, toValue: dest[name] };
            }
        }
    }

    if (src && typeof src === "object") {
        for (name in src) {
            if (Object.prototype.hasOwnProperty.call(src, name)) {
                if (!config[name]) {
                    config[name] = { toValue: undefined };
                }

                config[name].fromValue = src[name];
            }
        }
    }

    if (easing !== undefined && easing !== null) {
        for (name in config) {
            if (Object.prototype.hasOwnProperty.call(config, name)) {
                config[name].easing = easing;
            }
        }
    }

    return config;
}

// Tween.from(object, src, duration, easing)：与 buildTweenConfig(dest, src) 反向。
// src 是**起点**，终点是元素**当前值**——必须在这里显式取出来写进 toValue，
// 因为 createTweenTrack 见到 toValue 缺失时是「等 fromValue」，
// 不是「补到当前值」（那个缺省只适用于 fromValue 缺失的方向）。
function buildFromTweenConfig(object, src, easing) {
    var config = {};
    var name;
    if (src && typeof src === "object") {
        for (name in src) {
            if (Object.prototype.hasOwnProperty.call(src, name)) {
                config[name] = {
                    fromValue: src[name],
                    toValue: readTweenTargetValue(object, name)
                };
            }
        }
    }

    if (easing !== undefined && easing !== null) {
        for (name in config) {
            if (Object.prototype.hasOwnProperty.call(config, name)) {
                config[name].easing = easing;
            }
        }
    }

    return config;
}

// Tween.from / Tween.to：用声明式配置建段，再交给句柄驱动。
function createDrivenHandle(object, config, duration) {
    var built = buildMotionSegments(config, null, object);
    if (built.segments.length === 0) {
        throw new Error("Tween 需要至少一个可补间的属性");
    }

    var durationMs = motionTotalMs(built.segments);
    // M8 的 duration 单位是秒；未给或非正数时用补间自身推导出的时长。
    var declaredMs = toFiniteNumber(duration, 0) * 1000;
    if (declaredMs > 0 && Math.abs(declaredMs - durationMs) > 1) {
        built.segments = mapSegments(built.segments, declaredMs / durationMs, 0);
        durationMs = declaredMs;
    }

    var handle = createTweenHandle(built.segments, durationMs, object ? [object] : []);
    // 句柄在创建时装到元件上（但 state 仍是 idle，逐帧路径不写属性），
    // 这样 ownerItem / 元素寿命的归属在创建那一刻就确定了；
    // play() 负责把 timeMs 的时间轴跑起来。
    installTweenHandle(handle);
    return handle;
}

export {
    TRANSFORM_ONLY_KEYS,
    Tween,
    advanceElementMotion,
    advanceItemHandles,
    applyElementLifeTime,
    createTween,
    invalidateElementCache,
    isMotionHandleDriven,
    markPropertyDirty,
    readDeclaredSeconds,
    resolveDeclaredLifeTimeMs,
    resolveElementLifeTimeMs
};
