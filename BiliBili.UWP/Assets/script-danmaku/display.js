// 保留元素、显示列表、图形工厂与 Flash 变换。
import {
    DEFAULT_TEXT_COLOR,
    DEFAULT_TEXT_FONT,
    DEFAULT_TEXT_FONTSIZE,
    LIFE_TIME_UNBOUNDED,
    currentCreateParent,
    currentItem,
    hostState,
    normalizeColor,
    reportRuntimeError,
    toFiniteNumber
} from "./core.js";
import {
    recoverItemFromCallbackError,
    registerItemElement
} from "./lifecycle.js";
import {
    createElementTransform,
    displayObjectBounds,
    enqueueElementErase,
    markElementMoved,
    retirePaintedAncestorRect,
    traceElementClipPath
} from "./renderer.js";
import {
    TRANSFORM_ONLY_KEYS,
    applyElementLifeTime,
    createTween,
    invalidateElementCache,
    markPropertyDirty,
    readDeclaredSeconds,
    resolveDeclaredLifeTimeMs
} from "./tween.js";

import { localMatrix, transformPoint } from "./geometry.js";

// ---- 保留元素 ----

var nextElementId = 1;

function propertyDescriptor(name) {
    return {
        configurable: true,
        enumerable: true,
        get: function () {
            return this.props[name];
        },
        set: function (value) {
            // 元素属性可写：直接赋值即标脏，下一帧只重绘该元素。
            // 影响位图内容的属性（见 markPropertyDirty）还会让缓存失效。
            setPropertyInternal(this, name, value, true);
            markPropertyDirty(this, name);
            hostState.dirty = true;
        }
    };
}

function createRetainedElement(kind) {
    var element = {
        id: nextElementId++,
        kind: kind,
        props: {
            x: 0,
            y: 0,
            z: 0,
            alpha: 1,
            scaleX: 1,
            scaleY: 1,
            rotation: 0,
            rotationX: 0,
            rotationY: 0,
            rotationZ: 0,
            visible: true,
            matrix: null,
            filters: null,
            // 以下四项是 Flash DisplayObject 的平移面属性：
            //  - scaleZ        ：3D 缩放
            //  - blendMode     ：混合模式，映射到 canvas 的 globalCompositeOperation
            //  - scrollRect    ：滚动矩形（需要裁剪语义，当前只存储）
            //  - mask          ：元素级遮罩（真实现，见 applyElementMaskClip）
            scaleZ: 1,
            // Flash 的 DisplayObject.name（getChildByName 要用）。
            name: "",
            blendMode: "normal",
            scrollRect: null,
            mask: null,
            // transform 命名空间上的三个可写量（matrix 复用 props.matrix）。
            matrix3D: null,
            colorTransform: null
        },
        // treeParent / childList 是元素树的真实字段；对脚本暴露的
        // element.parent / element.children 是它们的读写访问器
        // （见 defineTreeProperties）。不能直接用 parent/children 存树，
        // 否则 attachElement 里 element.parent = target 会触发访问器
        // 递归调用 addChildToParent。
        childList: [],
        treeParent: null,
        // Flash 的 EventDispatcher 登记表（addEventListener /
        // removeEventListener）。Akari 用 canvas 上的 "enterFrame"
        // 监听驱动整幅画面的每帧更新。注意它挂在**元素自身**上，
        // 不是 props（props 是变换/样式属性表）。
        eventListeners: {},
        ownerItem: null,
        expired: false,
        // 逐属性记脏：元素被整体标脏（例如 insertItem 时）后，
        // 静态元素也不会因此被反复重绘。
        propertyDirty: {},
        painted: false,
        needsCache: false,
        cacheCanvas: null,
        cacheCtx: null,
        cacheBounds: null,
        cacheOriginX: 0,
        cacheOriginY: 0,
        cacheDpr: 1,
        // 复合缓存：元素有子节点时，首帧把子树烘到离屏画布，
        // 之后整体作为一个元素做变换合成，子树不再逐帧绘制。
        composite: null,
        compositeDpr: 1,
        projectedComposite: false,
        projectedPlanar: false,
        projectedEffectCanvas: null,
        compositeDirty: false,
        // 复合层位图在元素本地坐标系里的原点与尺寸（子树内容决定）。
        compositeBounds: null,
        // 元素在主画布上「上一帧画到哪」的包围盒。脏矩形擦除、以及
        // 元素到期/隐藏时的残影清理都依赖它（没有它就只能整屏清屏）。
        lastPaintedRect: null,
        // 本帧内容缓存被重建过：即使位置没变也必须重新合成。
        rebuiltThisFrame: false,
        // 本帧是否是「脏元素」（擦除波及后的补画要跳过它，避免重复合成）。
        dirtyCandidate: false,
        // 元素级失败标记：补间推进抛错后停用该元素，不再逐帧重试。
        failed: false,
        hasTween: false,
        // 脚本声明的寿命（毫秒），只记录 tween 声明的最大值；
        // 不参与「条目窗口剩余时间」的合并，否则窗口值永远胜出、
        // 声明只能延长不能缩短（见 applyElementLifeTime）。
        declaredLifeTimeMs: LIFE_TIME_UNBOUNDED,
        // 寿命上限（毫秒）= min(declaredLifeTimeMs, 条目窗口剩余时间)。
        lifeTimeMs: LIFE_TIME_UNBOUNDED,
        motion: null
    };

    var names = ["x", "y", "z", "alpha", "scaleX", "scaleY", "scaleZ",
        "rotation", "rotationX", "rotationY", "rotationZ",
        "visible", "matrix", "filters", "blendMode", "scrollRect", "mask", "name"];
    for (var index = 0; index < names.length; index++) {
        Object.defineProperty(element, names[index], propertyDescriptor(names[index]));
    }

    // transform 命名空间：脚本里 22 次访问，必须挂在元素自身上。
    // `.matrix` 与已有 props.matrix 是同一个对象（不另起一套），
    // 3D 与颜色变换由渲染器应用于整份元件位图。
    Object.defineProperty(element, "transform", {
        configurable: true,
        enumerable: false,
        get: function () {
            if (!element.transformValue) {
                // 懒创建，所以在这里单独藏一次（否则它会成为可枚举字段，
                // 脚本的 foreach 就能看到它 → Factory.clone 又会走偏）。
                Object.defineProperty(element, "transformValue", {
                    configurable: true,
                    enumerable: false,
                    writable: true,
                    value: createElementTransform(element)
                });
            }

            return element.transformValue;
        }
    });

    // M8 元素 API：remove / setStyle 与 parent / children 访问器。
    attachElementApi(element);
    defineTreeProperties(element);
    attachDisplayListApi(element);
    ["width", "height"].forEach(function (name) {
        Object.defineProperty(element, name, {
            configurable: true, enumerable: false,
            get: function () { var bounds = displayObjectBounds(this, true); return bounds ? bounds[name] : 0; },
            set: function (value) {
                var current = this[name];
                if (current > 0) this[name === "width" ? "scaleX" : "scaleY"] *= Math.max(0, toFiniteNumber(value, 0)) / current;
            }
        });
    });
    hideElementInternals(element);
    return element;
}

function setPropertyInternal(element, name, value, markDirty) {
    var props = element.props;
    // matrix 归一成「带方法的矩阵对象」：脚本会走
    // `mx = el.transform.matrix; mx.identity();` 这种取出→原地改的写法，
    // 拿到的必须是同一份对象、且带 Flash Matrix 的方法。
    if (name === "matrix") {
        value = normalizeMatrixObject(value);
    }

    if (props[name] === value) {
        return false;
    }

    // 元素级遮罩要记引用计数：被当作遮罩的元件自己不再绘制
    // （Flash 里遮罩对象不参与渲染），计数为 0 时才恢复绘制。
    if (name === "mask") {
        releaseMaskReference(props.mask);
        retainMaskReference(value);
    }

    var previous = props[name];
    props[name] = value;
    if (name === "matrix" && value) {
        props.matrix3D = null;
        props.x = value.tx; props.y = value.ty;
        props.scaleX = Math.sqrt(value.a * value.a + value.b * value.b);
        props.scaleY = Math.sqrt(value.c * value.c + value.d * value.d);
        props.rotation = props.rotationZ = Math.atan2(value.b, value.a) * 180 / Math.PI;
        props.rotationX = props.rotationY = props.z = 0;
    } else if (name === "x" || name === "y" || name === "z") {
        if (name === "z" && value) props.matrix = null;
        if (props.matrix && name !== "z") props.matrix[name === "x" ? "tx" : "ty"] = value;
        if (props.matrix3D) props.matrix3D.rawData[name === "x" ? 12 : name === "y" ? 13 : 14] = value;
    } else if (name === "rotationX" || name === "rotationY" || name === "scaleZ" || (name === "z" && value)) {
        props.matrix = null; props.matrix3D = null;
    } else if (name === "scaleX" || name === "scaleY" || name === "rotation" || name === "rotationZ") {
        props.matrix3D = null;
        if (props.matrix) {
            var m = props.matrix;
            if (name === "scaleX" || name === "scaleY") {
                var factor = previous ? value / previous : 1;
                if (name === "scaleX") { m.a *= factor; m.b *= factor; }
                else { m.c *= factor; m.d *= factor; }
                if (!previous) props.matrix = null;
            } else {
                var angle = (value - previous) * Math.PI / 180, cos = Math.cos(angle), sin = Math.sin(angle);
                var a = m.a, b = m.b, c = m.c, d = m.d;
                m.a = a * cos - b * sin; m.b = a * sin + b * cos;
                m.c = c * cos - d * sin; m.d = c * sin + d * cos;
            }
        }
    }
    // rotationZ 是 rotation 的别名，与 M8 移植层一致。
    if (name === "rotation" || name === "rotationZ") {
        props.rotation = value;
        props.rotationZ = value;
    }

    if (markDirty) {
        element.propertyDirty[name] = true;
    }

    if (TRANSFORM_ONLY_KEYS[name]) {
        // 隐藏元素同样要擦掉它留在主画布上的旧像素，
        // 否则 visible=false 之后画面里永远留着它的最后一帧。
        if (name === "visible" && value === false) {
            markElementHidden(element);
        } else if (name === "alpha" && toFiniteNumber(value, 1) <= 0) {
            // alpha 归零等同隐藏：像素必须从主画布上撤掉。M8 作品的
            // 整幅背景层常用关键帧 alpha 淡出，不擦就会留下整块残影
            // （复现：Comp0 的「背景2」层 outPoint=3750 淡出后仍在画布上）。
            markElementHidden(element);
        } else if (value !== false) {
            // 变换类属性变了：元素在屏幕上的位置随之改变，
            // 必须把上一帧的包围盒入队擦除，否则留下拖影。
            markElementMoved(element);
        }
    }

    return true;
}

// 元素被脚本隐藏：它的旧像素要擦掉，且它不再参与合成。
function markElementHidden(element) {
    if (element.painted || element.lastPaintedRect) {
        var hadOwnRect = !!element.lastPaintedRect;
        enqueueElementErase(element);
        if (!hadOwnRect) {
            // 嵌套元件自己在主画布上没有矩形（像素画在祖先的复合层里），
            // 要向上找最近一个画到主画布上的祖先，擦掉它的旧矩形。
            // Akari 的图层切换就是给图层 canvas 置 visible=false——
            // 漏掉这一步就会「该消失的整层留在画布上」。
            retirePaintedAncestorRect(element);
        }

        element.lastPaintedRect = null;
        hostState.dirty = true;
    }
}

function markElementDirty(element) {
    if (!element) {
        return;
    }

    // 只标「当前属性」：x 变化不该让一个静态的图片元素重新解码。
    element.propertyDirty["*"] = true;
    hostState.dirty = true;
}

// 有东西变了，本帧需要走一次合成。元素级的脏标记决定谁被重画。
function markItemDirty() {
    hostState.dirty = true;
}

// 画布尺寸 / DPI 变化：所有元素的内容缓存都要按新比例重画一次。
function markAllDirty() {
    hostState.dirty = true;
    for (var index = 0; index < hostState.elements.length; index++) {
        hostState.elements[index].needsCache = true;
    }
}

// 元素在主画布上的可见性：隐藏元素同样要擦掉它在主画布上的旧像素，
// 否则 visible=false 之后画面里永远留着它的最后一帧。
function isElementVisible(element) {
    return !element.expired && element.props.visible !== false;
}

function isElementDirty(element) {
    for (var name in element.propertyDirty) {
        if (Object.prototype.hasOwnProperty.call(element.propertyDirty, name)) {
            return true;
        }
    }

    return false;
}

function applyInitialValues(element, patch) {
    if (!patch || typeof patch !== "object") {
        return;
    }

    for (var name in patch) {
        if (Object.prototype.hasOwnProperty.call(patch, name)) {
            setPropertyInternal(element, name, patch[name], false);
        }
    }
}

// ---- 元素工厂 ----

function normalizeStyle(style) {
    var result = {
        x: 0,
        y: 0,
        z: 0,
        scaleX: 1,
        scaleY: 1,
        rotation: 0,
        alpha: 1,
        visible: true
    };

    if (!style || typeof style !== "object") {
        return result;
    }

    var names = ["x", "y", "z", "scaleX", "scaleY", "scaleZ",
        "rotation", "rotationX", "rotationY", "rotationZ", "alpha"];
    for (var index = 0; index < names.length; index++) {
        var name = names[index];
        if (style[name] !== undefined && style[name] !== null) {
            result[name] = toFiniteNumber(style[name], result[name]);
        }
    }

    // 非数值的创建参数：Flash DisplayObject 的混合模式与裁剪面也要能从
    // 创建参数直接给（`$.createShape({blendMode: "add"})`）。
    if (typeof style.blendMode === "string" && style.blendMode) {
        result.blendMode = style.blendMode;
    }

    if (style.name !== undefined && style.name !== null) {
        result.name = String(style.name);
    }

    if (style.scrollRect !== undefined) {
        result.scrollRect = style.scrollRect;
    }

    if (style.matrix !== undefined && style.matrix !== null) {
        result.matrix = style.matrix;
    }

    if (style.filters !== undefined) {
        result.filters = style.filters;
    }

    if (style.mask !== undefined && style.mask !== null) {
        result.mask = style.mask;
    }

    if (style.scale !== undefined && style.scale !== null) {
        var scale = toFiniteNumber(style.scale, 1);
        result.scaleX = scale;
        result.scaleY = scale;
    }

    if (style.visible !== undefined && style.visible !== null) {
        result.visible = !!style.visible;
    }

    return result;
}

// ---- M8 元素 API：remove / setStyle / parent / children / length ----
//
// 这些是 M8 脚本正文里直接用的成员（`el.remove()`、`b.setStyle("fillColors",…)`、
// `oTxt.length`、`$.createCanvas({parent: box})`），必须挂在元素本身上，
// 而不是宿主内部的另一个包装对象上。
function attachElementApi(element) {
    // 从渲染中摘除（M8 的 remove）：等价于宿主内部的 detachElement，
    // 但元素仍在条目登记表里，条目结束时不会被重复释放。
    element.remove = function () {
        removeElementFromItem(element);
        return element;
    };

    // M8 的 setStyle：按名字设一个「样式」属性。文本的 color/fontsize/bold
    // 落在 style 上，其余（按钮的 fillColors 等）落在元素属性上。
    element.setStyle = function (name, value) {
        if (!name) {
            return element;
        }

        // align 走元素自己的访问器（原版是 setStyle → TextFormat.align），
        // 归一与标脏都在那边，这里不重复一份。
        if (element.style && name === "align") {
            element.align = value;
            return element;
        }

        if (element.style && (name === "color" || name === "fontsize"
            || name === "font" || name === "bold" || name === "border"
            || name === "borderColor")) {
            if (name === "color" || name === "borderColor") {
                element.style[name] = normalizeColor(value);
            } else if (name === "bold" || name === "border") {
                element.style[name] = !!value;
            } else if (name === "fontsize") {
                element.style.fontsize = toFiniteNumber(value, element.style.fontsize);
            } else {
                element.style[name] = String(value);
            }

            element.propertyDirty["*"] = true;
            invalidateElementCache(element);
            hostState.dirty = true;
            return element;
        }

        setPropertyInternal(element, name, value, true);
        markPropertyDirty(element, name);
        hostState.dirty = true;
        return element;
    };

    return element;
}

// 元素的内容/样式类可写属性：直接赋值即让位图缓存失效。
// M8 脚本大量使用 `el.text = ...`、`el.fontsize = ...`、`el.color = ...`，
// 这些属性此前只能通过 setStyle 改，脚本原样移植会静默不生效。
var CONTENT_PROPERTY_NAMES = ["text", "font", "fontsize", "color", "bold",
    "border", "borderColor", "url"];

function contentPropertyDescriptor(name) {
    return {
        configurable: true,
        enumerable: true,
        get: function () {
            if (name === "text" || name === "url") {
                return readElementMember(this, name);
            }

            return this.style ? this.style[name] : undefined;
        },
        set: function (value) {
            var target = this;
            if (name === "text" || name === "url") {
                setElementMember(target, name, value);
            } else if (target.style) {
                if (name === "color" || name === "borderColor") {
                    target.style[name] = normalizeColor(value);
                } else if (name === "bold" || name === "border") {
                    target.style[name] = !!value;
                } else if (name === "fontsize") {
                    target.style.fontsize = toFiniteNumber(value, target.style.fontsize);
                } else {
                    target.style[name] = String(value);
                }

                // 改的是位图内容，必须重建缓存。
                target.propertyDirty["*"] = true;
                invalidateElementCache(target);
                hostState.dirty = true;
                return;
            }

            if (name === "url") {
                // 换图：重新走一遍加载（旧位图在 onload 之前继续显示）。
                loadImageElement(target);
            }

            target.propertyDirty["*"] = true;
            invalidateElementCache(target);
            hostState.dirty = true;
        }
    };
}

// 文本/图片元素的内容成员存在元素自身上（text / url）。
function readElementMember(element, name) {
    return element[name + "Value"];
}

function setElementMember(element, name, value) {
    var normalized = name === "text"
        ? (value === undefined || value === null ? "" : String(value))
        : String(value || "");
    if (element[name + "Value"] === normalized) {
        return;
    }

    element[name + "Value"] = normalized;
}

function defineContentProperties(element) {
    var names = element.kind === "image"
        ? ["url"]
        : (element.kind === "text" ? CONTENT_PROPERTY_NAMES : []);
    for (var index = 0; index < names.length; index++) {
        Object.defineProperty(
            element,
            names[index],
            contentPropertyDescriptor(names[index]));
    }

    return element;
}

// 把元素的所有自有属性设为**不可枚举**。
//
// 这一条是让真实 M8 脚本能跑的关键（entry_08 踩出来的）：Flash 里
// 显示对象的属性与方法都挂在原型上，脚本用 `foreach(obj, fn)` 或
// `for (k in obj)` 遍历一个显示对象时**一个属性都拿不到**；
// Akari 的 Factory.clone 正是靠这个分叉——`countProperties === 0`
// 时才走「新建 $.createCanvas 再逐个拷贝显示属性」那条正确路径。
//
// 本宿主的元素是普通对象，内部字段（props / childList / treeParent /
// ownerItem / motion / cacheCanvas …）全在自身上且互相成环
// （treeParent ↔ childList ↔ ownerItem ↔ motion.handle.elements），
// 可枚举的话脚本的 foreach 会顺着环无限递归（实测：
// RangeError: Maximum call stack size exceeded）。
//
// 注意：不可枚举**不影响** hasOwnProperty —— 脚本用
// `object.hasOwnProperty("numChildren")` 判断「是不是显示对象」照旧成立。
// 给元素加一个「脚本不该看见」的字段（不可枚举）。懒创建的字段必须走它，
// 否则脚本的 foreach / for-in 能枚举到，Akari 的 Factory.clone 会走偏。
function defineHiddenValue(element, name, value) {
    Object.defineProperty(element, name, {
        configurable: true,
        enumerable: false,
        writable: true,
        value: value
    });
}

function hideElementInternals(element) {
    var names = Object.getOwnPropertyNames(element);
    for (var index = 0; index < names.length; index++) {
        var descriptor = Object.getOwnPropertyDescriptor(element, names[index]);
        if (!descriptor || descriptor.enumerable === false) {
            continue;
        }

        descriptor.enumerable = false;
        Object.defineProperty(element, names[index], descriptor);
    }

    return element;
}

// ---- Flash 的 Event.ENTER_FRAME ----
//
// Akari 的 Composition.present() 是 `this.canvas.addEventListener(
// "enterFrame", frameFunction)`，整幅画面的每帧更新就挂在这上面——
// 没有它 entry_10 只建树不渲染。
//
// 监听表按条目存（item.frameListeners），随条目回收一起清掉；
// 派发在 advanceItem 里，与补间推进同一条每帧路径。
function registerItemFrameListener(element, listener) {
    var item = element.ownerItem || currentItem();
    if (!item) {
        return;
    }

    if (!item.frameListeners) {
        item.frameListeners = [];
    }

    item.frameListeners.push({ element: element, listener: listener });
}

function unregisterItemFrameListener(element, listener) {
    var item = element.ownerItem || currentItem();
    if (!item || !item.frameListeners) {
        return;
    }

    for (var index = item.frameListeners.length - 1; index >= 0; index--) {
        var entry = item.frameListeners[index];
        if (entry.element === element && entry.listener === listener) {
            item.frameListeners.splice(index, 1);
        }
    }
}

// 每帧派发 enterFrame。单个监听器抛错只摘掉它自己，不影响其它监听器与整帧。
function dispatchItemEnterFrame(item) {
    var listeners = item.frameListeners;
    if (!listeners || listeners.length === 0) {
        return;
    }

    for (var index = listeners.length - 1; index >= 0; index--) {
        var entry = listeners[index];
        if (entry.element.expired) {
            listeners.splice(index, 1);
            continue;
        }

        var previous = hostState.activeItem;
        hostState.activeItem = item;
        try {
            entry.listener.call(entry.element, { type: "enterFrame" });
        } catch (error) {
            listeners.splice(index, 1);
            if (!recoverItemFromCallbackError(item, error)) {
                reportRuntimeError(error, item.model.id);
            }

            // 补救会整条重跑（监听表已重建），这一轮不要再往下走。
            return;
        } finally {
            hostState.activeItem = previous;
            item.createParent = null;
        }
    }

    hostState.dirty = true;
}

// ---- Flash DisplayObject 的显示列表 / 变换 / 混合模式 ----

// 遮罩引用计数：被当作遮罩的元件不参与绘制（Flash 语义）。
// 计数而不是布尔：同一个元件可能同时是多个元素/层级的遮罩。
function retainMaskReference(maskElement) {
    if (!maskElement || !maskElement.kind) {
        return;
    }

    defineHiddenValue(maskElement, "maskUseCount", (maskElement.maskUseCount || 0) + 1);
}

function releaseMaskReference(maskElement) {
    if (!maskElement || !maskElement.kind) {
        return;
    }

    defineHiddenValue(
        maskElement,
        "maskUseCount",
        Math.max(0, (maskElement.maskUseCount || 0) - 1));
}

function isUsedAsMask(element) {
    return (element.maskUseCount || 0) > 0;
}

// Flash 的 BlendMode → canvas globalCompositeOperation。
// 未知值一律退回 normal（不抛错）——脚本里的取值集合远大于 canvas 能表达的。
var BLEND_MODE_COMPOSITES = {
    normal: "source-over",
    layer: "source-over",
    alpha: "source-over",
    darken: "darken",
    multiply: "multiply",
    lighten: "lighten",
    screen: "screen",
    overlay: "overlay",
    difference: "difference",
    subtract: "difference",
    add: "lighter",
    hardlight: "hard-light",
    hardLight: "hard-light",
    colordodge: "color-dodge",
    colorDodge: "color-dodge",
    colorburn: "color-burn",
    colorBurn: "color-burn",
    exclusion: "exclusion",
    hue: "hue",
    saturation: "saturation",
    color: "color",
    luminosity: "luminosity"
};

function blendModeToComposite(value) {
    if (typeof value !== "string") {
        return "source-over";
    }

    return BLEND_MODE_COMPOSITES[value] || "source-over";
}

// 元素级遮罩：Flash 的 `被遮罩元素.mask = 遮罩元素`。
//
// 作用域是**被遮罩元素自己（含子树）**，不是整块画布——这是它与
// Player.setMask（播放器级、整块画布）的关键区别。
//
// 实现要点：这里不能用「save → 施加遮罩元件的变换 → clip → restore」，
// 因为 restore 会把刚建立的裁剪一起去掉（canvas 的裁剪区在状态栈里）。
// 而且 blitElement 的调用方可能已经压了变换。所以改为**把遮罩元件的
// 变换烘进路径坐标**：用 maskLocalToParent 把遮罩元件的每个点换算到
// 它所在的父子空间，再用当前（父）坐标系直接 clip。
function applyElementMaskClip(target, element) {
    var masker = element.props.mask;
    if (!masker || masker.expired || masker.props.visible === false) {
        return false;
    }

    var map = createMaskPointMapper(masker);
    if (!traceElementClipPath(target, masker, map)) {
        return false;
    }

    target.clip();
    return true;
}

// 遮罩元件的局部坐标 → 父坐标（与 applyElementTransform 同序：
// 缩放 → matrix → 旋转 → 平移）。
function createMaskPointMapper(masker) {
    var matrix = localMatrix(masker);
    return function (x, y) { return transformPoint(matrix, x, y, 0); };
}

// Flash 的 DisplayObjectContainer 显示列表查询。
// 注意 numChildren 必须是元素自己的 **own property**：
// entry_08 用 `object.hasOwnProperty("numChildren")` 判断「这是不是一个
// 显示对象」，定义在原型/外部对象上会被判成非显示对象、整条克隆路径走偏。
function attachDisplayListApi(element) {
    Object.defineProperty(element, "numChildren", {
        configurable: true,
        enumerable: true,
        get: function () {
            return element.childList.length;
        }
    });

    element.getChildAt = function (index) {
        var position = Math.floor(toFiniteNumber(index, -1));
        // Flash 越界会抛 RangeError；脚本里都是按 numChildren 正常遍历，
        // 这里返回 null 而不是抛错，坏索引不至于毁掉整条脚本。
        return position >= 0 && position < element.childList.length
            ? element.childList[position]
            : null;
    };

    element.getChildIndex = function (child) {
        return element.childList.indexOf(child);
    };

    element.setChildIndex = function (child, index) {
        var from = element.childList.indexOf(child);
        if (from < 0) {
            return;
        }

        var to = Math.floor(toFiniteNumber(index, from));
        if (to < 0 || to >= element.childList.length) {
            return;
        }

        element.childList.splice(from, 1);
        element.childList.splice(to, 0, child);
        element.compositeDirty = true;
        hostState.dirty = true;
    };

    element.getChildByName = function (name) {
        for (var index = 0; index < element.childList.length; index++) {
            if (element.childList[index].name === name) {
                return element.childList[index];
            }
        }

        return null;
    };

    element.contains = function (child) {
        var current = child;
        while (current) {
            if (current === element) {
                return true;
            }

            current = current.treeParent;
        }

        return false;
    };

    element.swapChildren = function (left, right) {
        var leftIndex = element.childList.indexOf(left);
        var rightIndex = element.childList.indexOf(right);
        if (leftIndex < 0 || rightIndex < 0) {
            return;
        }

        element.childList[leftIndex] = right;
        element.childList[rightIndex] = left;
        element.compositeDirty = true;
        hostState.dirty = true;
    };

    // addChild / addChildAt / removeChild / removeChildAt：显示列表的写操作。
    element.addChild = function (child) {
        return addChildToParent(child, element);
    };

    element.addChildAt = function (child, index) {
        addChildToParent(child, element);
        var position = Math.floor(toFiniteNumber(index, 0));
        var current = element.childList.indexOf(child);
        if (current >= 0 && position >= 0 && position < element.childList.length - 1) {
            element.childList.splice(current, 1);
            element.childList.splice(position, 0, child);
            element.compositeDirty = true;
            hostState.dirty = true;
        }

        return child;
    };

    element.removeChild = function (child) {
        return removeChildFromParent(child);
    };

    element.removeChildAt = function (index) {
        var child = element.getChildAt(index);
        return child ? removeChildFromParent(child) : null;
    };

    // Flash 的 EventDispatcher：DisplayObject 可以挂监听。
    // 本宿主只真正派发 "enterFrame"（见 dispatchItemEnterFrame），
    // 其余事件类型登记了也不会被派发（不抛错）。
    element.addEventListener = function (type, listener) {
        if (typeof listener !== "function" || !type) {
            return;
        }

        var bucket = element.eventListeners[type];
        if (!bucket) {
            bucket = [];
            element.eventListeners[type] = bucket;
        }

        if (bucket.indexOf(listener) < 0) {
            bucket.push(listener);
        }

        if (type === "enterFrame") {
            registerItemFrameListener(element, listener);
        }
    };

    element.removeEventListener = function (type, listener) {
        var bucket = element.eventListeners[type];
        if (!bucket) {
            return;
        }

        var index = bucket.indexOf(listener);
        if (index >= 0) {
            bucket.splice(index, 1);
        }

        if (type === "enterFrame") {
            unregisterItemFrameListener(element, listener);
        }
    };

    element.hasEventListener = function (type) {
        var bucket = element.eventListeners[type];
        return !!bucket && bucket.length > 0;
    };

    element.dispatchEvent = function (event) {
        var type = event && event.type ? event.type : String(event);
        var bucket = element.eventListeners[type];
        if (!bucket) {
            return true;
        }

        for (var index = 0; index < bucket.length; index++) {
            bucket[index].call(element, event);
        }

        return true;
    };

    element.removeAllChildren = function () {
        while (element.childList.length > 0) {
            removeChildFromParent(element.childList[element.childList.length - 1]);
        }
    };

    return element;
}

// 脚本读回来的 blendMode 必须是它自己写进去的那个值（entry_08 在
// 图层之间互相拷贝 blendMode），所以存储原样保留、只在绘制时映射。
function applyElementBlendMode(target, element) {
    target.globalCompositeOperation = blendModeToComposite(element.props.blendMode);
}

// M8 的 parent / children 是普通可写成员：赋值即换父节点。
function defineTreeProperties(element) {
    Object.defineProperty(element, "parent", {
        configurable: true,
        enumerable: true,
        get: function () {
            return element.treeParent;
        },
        set: function (value) {
            addChildToParent(element, value || null);
        }
    });

    Object.defineProperty(element, "children", {
        configurable: true,
        enumerable: true,
        get: function () {
            return element.childList;
        }
    });

    return element;
}

// 文本元素的 length（M8 里是文本字段的字符数）。
function defineTextProperties(element) {
    Object.defineProperty(element, "length", {
        configurable: true,
        enumerable: true,
        get: function () {
            var text = element.textValue;
            return text === undefined || text === null ? 0 : String(text).length;
        }
    });

    // 原版 CommentField.align 落到 TextFormat.align（CommentField.as:62-71），
    // 取值 "left" / "center" / "right"。这里只做「可读写、可回读」，
    // **不参与绘制**——原版 CommentField 用的是 autoSize = LEFT
    // （CommentField.as:45），文本框宽度恒等于文本宽度，align 因此
    // 在原版里没有视觉效果。宿主照此保持左对齐，不臆造额外的排版行为。
    Object.defineProperty(element, "align", {
        configurable: true,
        enumerable: true,
        get: function () {
            return element.style ? element.style.align : "left";
        },
        set: function (value) {
            if (!element.style) {
                return;
            }

            var align = String(value === undefined || value === null ? "left" : value);
            if (align !== "center" && align !== "right") {
                // 原版把取值直接交给 TextFormat，非法值等同默认左对齐。
                align = "left";
            }

            element.style.align = align;
            element.propertyDirty["*"] = true;
            invalidateElementCache(element);
            hostState.dirty = true;
        }
    });

    // 原版 CommentField 重写了 htmlText 的 get/set，两个方向都直接
    // 读写 text（CommentField.as:117-125）——它根本不解析 HTML 标签。
    // 因此这里也照原样直通，不做富文本。
    Object.defineProperty(element, "htmlText", {
        configurable: true,
        enumerable: true,
        get: function () {
            return element.textValue;
        },
        set: function (value) {
            element.text = value;
        }
    });

    return element;
}

function createTextStyle(style) {
    var source = style && typeof style === "object" ? style : {};
    return {
        // 默认值与 M8 的 createComment 一致：白字、黑体、25px。
        color: normalizeColor(source.color === undefined || source.color === null
            ? DEFAULT_TEXT_COLOR
            : source.color),
        font: typeof source.font === "string" && source.font
            ? source.font
            : DEFAULT_TEXT_FONT,
        fontsize: toFiniteNumber(source.fontsize, DEFAULT_TEXT_FONTSIZE),
        bold: !!source.bold,
        border: !!source.border,
        borderColor: normalizeColor(source.borderColor === undefined
            || source.borderColor === null
            ? 0
            : source.borderColor),
        // 原版 initStyle 没有 align，文本框是 TextFieldAutoSize.LEFT；
        // 这里留一个可回读的默认值（见 defineTextProperties 的 align）。
        align: "left"
    };
}

function measureTextElement(element) {
    var style = element.style;
    var metrics = ensureMeasureContext();
    metrics.font = textFontCss(style);
    var text = element.textValue === undefined || element.textValue === null
        ? ""
        : String(element.textValue);
    return {
        text: text,
        width: metrics.measureText(text).width,
        height: style.fontsize * 1.2
    };
}

var measureContext = null;

function ensureMeasureContext() {
    if (!measureContext) {
        var probe = document.createElement("canvas");
        measureContext = probe.getContext("2d");
    }

    return measureContext;
}

function textFontCss(style) {
    return (style.bold ? "bold " : "") + style.fontsize + "px " + style.font;
}

function drawTextElement(target, element, offsetX, offsetY) {
    var style = element.style;
    var metrics = measureTextElement(element);
    target.font = textFontCss(style);
    target.textBaseline = "middle";
    target.textAlign = "left";
    if (style.border) {
        target.lineWidth = Math.max(1, style.fontsize / 12);
        target.strokeStyle = rgbCss(style.borderColor);
        target.strokeText(metrics.text, offsetX, offsetY + metrics.height / 2);
    }

    target.fillStyle = rgbCss(style.color);
    target.fillText(metrics.text, offsetX, offsetY + metrics.height / 2);
}

function rgbCss(color) {
    return "rgb(" + ((color >> 16) & 0xFF) + ","
        + ((color >> 8) & 0xFF) + ","
        + (color & 0xFF) + ")";
}

function createGraphics(element) {
    var graphics = {
        clear: function () {
            element.shapeItems = [];
            element.painted = false;
            scheduleElementCache(element);
        },
        beginFill: function (color, alpha) {
            graphics.__fill = {
                color: normalizeColor(color),
                alpha: alpha === undefined || alpha === null ? 1 : toFiniteNumber(alpha, 1)
            };
        },
        endFill: function () {
            graphics.__fill = null;
        },
        // Flash 的 beginGradientFill(type, colors, alphas, ratios,
        // matrix, spreadMethod, interpolationMethod, focalPointRatio)。
        // type 为 "radial" 时按径向，其余按线性（Flash 只认 linear/radial）。
        // spreadMethod / interpolationMethod / focalPointRatio 本宿主
        // 未参与计算（canvas 的渐变不支持 spread，见文档 §3 的已知近似）。
        beginGradientFill: function (type, colors, alphas, ratios, matrix) {
            graphics.__fill = {
                color: 0,
                alpha: 1,
                gradient: {
                    type: type === "radial" ? "radial" : "linear",
                    colors: colors,
                    alphas: alphas,
                    ratios: ratios,
                    box: matrix && matrix.gradientBox ? matrix.gradientBox : null
                }
            };
        },
        // Flash 的 lineGradientStyle：与 lineStyle 配对使用（先 lineStyle
        // 启用笔触，再换成渐变）。参数与 beginGradientFill 一致。
        lineGradientStyle: function (type, colors, alphas, ratios, matrix) {
            graphics.__line = {
                width: graphics.__line ? graphics.__line.width : 1,
                color: 0,
                alpha: 1,
                gradient: {
                    type: type === "radial" ? "radial" : "linear",
                    colors: colors,
                    alphas: alphas,
                    ratios: ratios,
                    box: matrix && matrix.gradientBox ? matrix.gradientBox : null
                }
            };
        },
        lineStyle: function (thickness, color, alpha) {
            graphics.__line = {
                width: toFiniteNumber(thickness, 1),
                color: normalizeColor(color),
                alpha: alpha === undefined || alpha === null ? 1 : toFiniteNumber(alpha, 1)
            };
        },
        moveTo: function (x, y) {
            graphics.__path = {
                kind: "poly",
                points: [toFiniteNumber(x, 0), toFiniteNumber(y, 0)], segments: []
            };
            if (graphics.__line) {
                element.shapeItems.push({ kind: "line", path: graphics.__path, style: graphics.__line });
            }

            element.shapeItems.push({ kind: "fill", path: graphics.__path, style: graphics.__fill });
        },
        lineTo: function (x, y) {
            if (!graphics.__path || graphics.__path.kind !== "poly") {
                return;
            }

            graphics.__path.points.push(toFiniteNumber(x, 0), toFiniteNumber(y, 0));
            graphics.__path.segments.push([2, toFiniteNumber(x, 0), toFiniteNumber(y, 0)]);
        },
        curveTo: function (cx, cy, x, y) {
            if (!graphics.__path || graphics.__path.kind !== "poly") {
                return;
            }

            graphics.__path.curves = graphics.__path.curves || [];
            graphics.__path.curves.push([
                toFiniteNumber(cx, 0), toFiniteNumber(cy, 0),
                toFiniteNumber(x, 0), toFiniteNumber(y, 0)
            ]);
            graphics.__path.points.push(toFiniteNumber(x, 0), toFiniteNumber(y, 0));
            graphics.__path.segments.push([3, toFiniteNumber(cx, 0), toFiniteNumber(cy, 0), toFiniteNumber(x, 0), toFiniteNumber(y, 0)]);
        },
        drawRect: function (x, y, width, height) {
            element.shapeItems.push({
                kind: "rect",
                x: toFiniteNumber(x, 0),
                y: toFiniteNumber(y, 0),
                width: toFiniteNumber(width, 0),
                height: toFiniteNumber(height, 0),
                fill: graphics.__fill,
                line: graphics.__line
            });
        },
        drawRoundRect: function (x, y, width, height, rx, ry) {
            element.shapeItems.push({
                kind: "roundRect",
                x: toFiniteNumber(x, 0),
                y: toFiniteNumber(y, 0),
                width: toFiniteNumber(width, 0),
                height: toFiniteNumber(height, 0),
                rx: toFiniteNumber(rx, 0),
                ry: toFiniteNumber(ry, toFiniteNumber(rx, 0)),
                fill: graphics.__fill,
                line: graphics.__line
            });
        },
        drawCircle: function (x, y, radius) {
            element.shapeItems.push({
                kind: "circle",
                x: toFiniteNumber(x, 0),
                y: toFiniteNumber(y, 0),
                radius: toFiniteNumber(radius, 0),
                fill: graphics.__fill,
                line: graphics.__line
            });
        },
        drawEllipse: function (x, y, width, height) {
            element.shapeItems.push({
                kind: "ellipse",
                x: toFiniteNumber(x, 0),
                y: toFiniteNumber(y, 0),
                width: toFiniteNumber(width, 0),
                height: toFiniteNumber(height, 0),
                fill: graphics.__fill,
                line: graphics.__line
            });
        },
        drawWedge: function (x, y, radius, startAngle, arc) {
            element.shapeItems.push({
                kind: "wedge",
                x: toFiniteNumber(x, 0),
                y: toFiniteNumber(y, 0),
                radius: toFiniteNumber(radius, 0),
                startAngle: toFiniteNumber(startAngle, 0),
                arc: toFiniteNumber(arc, 0),
                fill: graphics.__fill,
                line: graphics.__line
            });
        },
        drawPolygon: function () {
            var points = [];
            for (var index = 0; index < arguments.length; index++) {
                points.push(toFiniteNumber(arguments[index], 0));
            }

            element.shapeItems.push({
                kind: "polygon",
                points: points,
                fill: graphics.__fill,
                line: graphics.__line
            });
        },
        // Flash 的 Graphics.drawPath(commands, data, winding)。
        // 同一 drawPath 的所有轮廓一起填充，保留曲线、字形空洞和 winding。
        drawPath: function (commands, data, winding) {
            if (!commands || !data) {
                return;
            }

            element.shapeItems.push({ kind: "path", commands: Array.from(commands), data: Array.from(data),
                winding: winding === "nonZero" ? "nonzero" : "evenodd", fill: graphics.__fill, line: graphics.__line });

            element.propertyDirty["*"] = true;
            scheduleElementCache(element);
        },
        // drawGraphicsData 需要完整的 IGraphicsData 对象模型（IGraphicsPath /
        // IGraphicsStroke / IGraphicsSolidFill …），两条真实脚本里 0 次使用，
        // 保持显式报错而不是静默画错。
        drawGraphicsData: function () {
            throw new Error("drawGraphicsData 尚未支持");
        }
    };
    return graphics;
}

function scheduleElementCache(element) {
    markElementDirty(element);
    invalidateElementCache(element);
}

function createShapeElement() {
    var element = createRetainedElement("shape");
    element.shapeItems = [];
    element.graphics = createGraphics(element);
    element.autoCached = true;
    // 构造期后加的字段也要不可枚举（脚本用 foreach/hasOwnProperty 探测显示对象）。
    hideElementInternals(element);
    return element;
}

function createTextElement(text, style) {
    var element = createRetainedElement("text");
    // 内容存在 textValue 上；对脚本暴露的 element.text 是访问器
    // （赋值即让位图缓存失效，见 defineContentProperties）。
    element.textValue = text === undefined || text === null ? "" : String(text);
    element.style = createTextStyle(style);
    defineTextProperties(element);
    defineContentProperties(element);
    applyInitialValues(element, normalizeStyle(style));
    element.autoCached = true;
    // 构造期后加的字段也要不可枚举（脚本用 foreach/hasOwnProperty 探测显示对象）。
    hideElementInternals(element);
    return element;
}

// M8 的 createButton：当前是「带底色的文本 + 可选 onclick」的近似实现。
// 按钮交互（命中测试 / hover 状态 / fillColors 渐变）待接入输入链后补齐。
function createButtonElement(options) {
    var source = options && typeof options === "object" ? options : {};
    var element = createRetainedElement("group");
    var width = toFiniteNumber(source.width, 0);
    var height = toFiniteNumber(source.height, toFiniteNumber(source.fontsize, 20) + 12);
    var background = createShapeElement();
    background.graphics.beginFill(
        source.fillColor === undefined || source.fillColor === null
            ? 0x336699
            : source.fillColor,
        1);
    background.graphics.drawRect(0, 0, width > 0 ? width : 1, height);
    background.graphics.endFill();
    addChildToParent(background, element);
    var label = createTextElement(source.text === undefined ? "" : source.text, source);
    label.x = 8;
    label.y = Math.round(height / 2 - toFiniteNumber(source.fontsize, DEFAULT_TEXT_FONTSIZE) / 2);
    addChildToParent(label, element);
    element.buttonWidth = width > 0 ? width : 1;
    element.buttonHeight = height;
    element.fillColors = null;
    element.onclick = typeof source.onclick === "function" ? source.onclick : null;
    return element;
}

function createImageElement(url) {
    var element = createRetainedElement("image");
    // 与文本一致：内容存在 urlValue / imageValue 上，脚本看到的
    // element.url 是访问器（改 URL 会重新加载）。
    element.urlValue = String(url || "");
    element.imageValue = null;
    element.loaded = false;
    element.failed = false;
    element.imageWidth = 0;
    element.imageHeight = 0;
    element.autoCached = true;
    defineContentProperties(element);

    loadImageElement(element);
    // 构造期后加的字段也要不可枚举（脚本用 foreach/hasOwnProperty 探测显示对象）。
    hideElementInternals(element);
    return element;
}

function loadImageElement(element) {
    if (!element.urlValue) {
        return;
    }

    var source = element.urlValue;
    var bitmap = new Image();
    bitmap.onload = function () {
        // 异步加载期间脚本可能已经改了 url：过期的结果直接丢弃。
        if (element.urlValue !== source) {
            return;
        }

        element.imageValue = bitmap;
        element.loaded = true;
        element.imageWidth = bitmap.naturalWidth || bitmap.width;
        element.imageHeight = bitmap.naturalHeight || bitmap.height;
        element.needsCache = true;
        element.painted = false;
        markElementDirty(element);
    };
    bitmap.onerror = function () {
        if (element.urlValue !== source) {
            return;
        }

        element.failed = true;
        element.needsCache = true;
        element.painted = false;
        markElementDirty(element);
    };
    bitmap.src = source;
}

// 脚本自绘的离屏 canvas 层（M8 里对应 Display.createGraphic 的用法）。
// 当前 M8 API 面没有对应的工厂入口，保留内部实现供后续接入
// （例如 $.createCanvas({draw: function(g){...}}) 形态）。
function createLayerElement(width, height) {
    var element = createRetainedElement("layer");
    element.layerWidth = Math.max(1, Math.round(toFiniteNumber(width, hostState.viewportWidth || 1)));
    element.layerHeight = Math.max(1, Math.round(toFiniteNumber(height, hostState.viewportHeight || 1)));
    element.layerCanvas = document.createElement("canvas");
    element.layerCanvas.width = element.layerWidth;
    element.layerCanvas.height = element.layerHeight;
    element.layer = element.layerCanvas.getContext("2d");
    element.autoCached = false;
    return element;
}

// ---- 元素树 ----

// 元素登记表：markAllDirty()（画布尺寸/DPI 变化）要遍历它把
// 所有元素的内容缓存标为待重建，所以每个进入元素树的元素都要登记。
function registerElement(element) {
    if (hostState.elements.indexOf(element) < 0) {
        hostState.elements.push(element);
    }
}

function unregisterElement(element) {
    var index = hostState.elements.indexOf(element);
    if (index >= 0) {
        hostState.elements.splice(index, 1);
    }
}

function attachElement(element, parent) {
    var target = parent || hostState.rootElement;
    element.treeParent = target;
    target.childList.push(element);
    registerElement(element);
    // 挂到复合元素下的新子节点要先烘进父元素的复合层。
    // 注意这里**不能**清 target.composite：复合层是父元件自己的
    // 位图缓存，清掉后它会重新分配一张视口大小的画布，
    // 且「静止复合元素不重复重建」的判据会失效（见 D4）。
    // 子树变化只标 compositeDirty，由 prepareElement 决定是否重烘。
    if (target !== hostState.rootElement) {
        target.compositeDirty = true;
    }

    hostState.dirty = true;
}

function addChildToParent(element, parent) {
    if (!element || !element.kind) {
        return element;
    }

    // 自引用或挂到自己的子树里会让绘制无限递归，直接拒绝。
    if (element === parent || isAncestorOf(element, parent)) {
        return element;
    }

    if (element.treeParent) {
        removeChildFromParent(element);
    }

    attachElement(element, parent);
    return element;
}

function isAncestorOf(element, candidate) {
    var current = candidate;
    while (current) {
        if (current === element) {
            return true;
        }

        current = current.treeParent;
    }

    return false;
}

// 与 removeChildFromParent 的区别：从条目登记表里也摘掉。
// 脚本调 el.remove() 后元素不再由宿主回收，所以必须自己退出登记表，
// 否则 markAllDirty() 还会去给已释放的缓存置位。
function removeElementFromItem(element) {
    removeChildFromParent(element);
    unregisterSubtree(element);
    return element;
}

function removeChildFromParent(element) {
    if (!element || !element.treeParent) {
        return element;
    }

    var parent = element.treeParent;
    // 从复合层里摘出来的子元素，旧像素还在父层的复合缓存里：
    // 擦除它的主画布矩形并让父层重烘。
    // 嵌套元件自己在主画布上没有矩形（像素画在祖先的复合层里），
    // 这时要向上找最近一个画到主画布上的祖先，把它的旧矩形入队擦除并
    // 让它本帧重烘重合成，否则父层重烘后透明处盖不住旧像素，会留下残影。
    // 只在这里（摘除）做，普通移动不能擦，见 markElementMoved。
    var hadOwnRect = !!element.lastPaintedRect;
    markElementMoved(element);
    if (!hadOwnRect) {
        retirePaintedAncestorRect(element);
    }

    parent.compositeDirty = true;
    var siblings = parent.childList;
    var index = siblings.indexOf(element);
    if (index >= 0) {
        siblings.splice(index, 1);
    }

    parent.compositeDirty = true;
    element.treeParent = null;
    unregisterElement(element);
    hostState.dirty = true;
    return element;
}

function releaseElementCaches(element) {
    element.painted = false;
    element.needsCache = false;
    element.cacheCanvas = null;
    element.cacheCtx = null;
    element.cacheBounds = null;
    element.composite = null;
    element.projectedComposite = false;
    element.projectedPlanar = false;
    element.projectedEffectCanvas = null;
    element.compositeBounds = null;
    element.propertyDirty = {};

    for (var index = 0; index < element.childList.length; index++) {
        releaseElementCaches(element.childList[index]);
    }
}

function detachElement(element) {
    // 摘链前先处理旧像素：嵌套元件要靠 treeParent 向上找祖先，
    // 摘了链就找不到它的复合层了。
    if (!element.lastPaintedRect) {
        retirePaintedAncestorRect(element);
    }

    if (element.treeParent) {
        var parent = element.treeParent;
        var siblings = parent.childList;
        var index = siblings.indexOf(element);
        if (index >= 0) {
            siblings.splice(index, 1);
        }

        parent.compositeDirty = true;
        element.treeParent = null;
    }

    // 摘除前先把主画布上的旧像素入队擦除，否则移动元素到期后
    // 它的最后一帧会永久留在画布上。
    markElementMoved(element);
    unregisterSubtree(element);
    releaseElementCaches(element);
    hostState.dirty = true;
}

// 摘除的元素连同其子树一起退出登记表，否则元素被回收后
// markAllDirty() 还会去给已释放的缓存置位。
function unregisterSubtree(element) {
    unregisterElement(element);
    for (var index = 0; index < element.childList.length; index++) {
        unregisterSubtree(element.childList[index]);
    }
}

// 元素工厂的公共参数：坐标 / 变换 / 可见性 / 寿命 / 父元件。
// M8 的创建参数直接写在 options 上，不再是 ctx 时代的第二个参数。
function applyCreateOptions(element, options) {
    var source = options && typeof options === "object" ? options : {};
    applyInitialValues(element, normalizeStyle(source));

    if (source.alpha !== undefined && source.alpha !== null) {
        setPropertyInternal(element, "alpha", toFiniteNumber(source.alpha, 1), false);
    }

    if (source.visible !== undefined && source.visible !== null) {
        setPropertyInternal(element, "visible", !!source.visible, false);
    }

    // 寿命：**未声明时不改**（元素活到条目窗口兜底上限，见
    // registerItemElement 的初值），声明时才按声明值收紧。
    // 声明值可能写在外层，也可能写在 motion 的某条属性上
    // （`motion: {x: {…, lifeTime: 4}}`），后者在 M8 脚本里更常见，
    // 取两者的最大值——与「同一元素多个 tween 取最大声明值」一致。
    // lifeTime: 0 → 常驻；负数 → 立刻到期（原版 ScriptDisplay.as:238-240
    // 夹成 0.001 秒，见 resolveDeclaredLifeTimeMs）。
    var declared = readDeclaredSeconds(source, "lifeTime");
    var motionConfig = source.motion ? normalizeMotionConfig(source.motion) : null;
    var motionDeclared = motionConfig ? readMotionDeclaredSeconds(motionConfig) : null;
    if (motionDeclared !== null && (declared === null || motionDeclared > declared)) {
        declared = motionDeclared;
    }

    var declaredLifeTimeMs = resolveDeclaredLifeTimeMs(declared);
    if (declaredLifeTimeMs !== null) {
        applyElementLifeTime(element, declaredLifeTimeMs);
    }

    // 父元件：M8 的 parent 是「创建时挂到谁下面」的普通参数。
    if (source.parent) {
        addChildToParent(element, source.parent);
    } else {
        element.createParent = currentCreateParent();
    }

    // 声明式 motion：M8 的 `motion: {x: {fromValue, toValue, lifeTime}}`。
    //
    // 必须走**声明式**补间（按条目进度 elapsed 插值），不能走 Tween.* 的
    // 句柄路径：句柄有自己的时间轴（play() 起算），seek 回窗口内重建时
    // 会从 0 重新开始，而保留模式的契约是「重建后的位置按新播放位置重算」。
    // 只有脚本显式调用 Tween.tween/to/... 时才是句柄语义（play() 起算）。
    if (motionConfig) {
        createTween(element, motionConfig, source);
    }

    // 创建参数可能给元素加了新字段（createParent 等），统一再隐藏一次。
    hideElementInternals(element);
    return element;
}

// motion 里声明的 lifeTime（秒）最大值；未声明返回 null。
function readMotionDeclaredSeconds(config) {
    var declared = null;
    for (var name in config) {
        if (!Object.prototype.hasOwnProperty.call(config, name)) {
            continue;
        }

        var seconds = readDeclaredSeconds(config[name], "lifeTime");
        if (seconds === null) {
            continue;
        }

        if (declared === null || seconds > declared) {
            declared = seconds;
        }
    }

    return declared;
}

// M8 的 motion 写法允许省略 fromValue / toValue 这类包装：
//   motion: { x: { fromValue: 0, toValue: 320, lifeTime: 4 } }   ← 与本宿主 tween 同形
//   motion: { x: { to: 320, start: 0, time: 4 } }                ← M8 简写
// 两种都归一成宿主内部的属性配置。
function normalizeMotionConfig(motion) {
    var config = {};
    for (var name in motion) {
        if (!Object.prototype.hasOwnProperty.call(motion, name)) {
            continue;
        }

        var value = motion[name];
        if (value && typeof value === "object") {
            config[name] = {
                fromValue: value.fromValue !== undefined ? value.fromValue : value.start,
                toValue: value.toValue !== undefined ? value.toValue : value.to,
                lifeTime: value.lifeTime !== undefined ? value.lifeTime : value.time,
                startDelay: value.startDelay,
                startDelayMs: value.startDelayMs,
                lifeTimeMs: value.lifeTimeMs,
                easing: value.easing,
                repeat: value.repeat
            };
        } else {
            config[name] = { toValue: value };
        }
    }

    return config;
}

// M8 的 `$`：元件工厂。
var M8Display = {
    createComment: function (text, options) {
        return createItemElement(function () {
            return createTextElement(
                text === undefined || text === null ? "" : text,
                options);
        }, options);
    },
    createText: function (text, options) {
        return M8Display.createComment(text, options);
    },
    createShape: function (options) {
        return createItemElement(createShapeElement, options);
    },
    // M8 的 createCanvas 与 createShape 同义（都是可承载子元件的保留元件）。
    createCanvas: function (options) {
        return M8Display.createShape(options);
    },
    createSprite: function (options) {
        return M8Display.createShape(options);
    },
    createButton: function (options) {
        return createItemElement(function () {
            return createButtonElement(options);
        }, options);
    },
    // 图片元件：M8 用 External.Bitmap.createBitmap，这里额外提供一个
    // 直连 URL 的 convenience 工厂（脚本可用 ES6 模板字符串拼 data: URI）。
    createImage: function (url, options) {
        return createItemElement(function () {
            return createImageElement(url);
        }, options);
    },
    // 脚本自绘层：宿主扩展（M8 无对应工厂），保留内部离屏 canvas 能力。
    createLayer: function (width, height, options) {
        return createItemElement(function () {
            return createLayerElement(width, height);
        }, options);
    },
    // M8 的 toIntVector / toNumberVector：把 JS 数组转成 Vector。
    // 本宿主里 Vector 就是普通数组（元素是数值），保留是为了让
    // 使用它们的脚本（字体数据等）原样可跑。
    toIntVector: function (list) {
        return toNumberArray(list, true);
    },
    toNumberVector: function (list) {
        return toNumberArray(list, false);
    },
    // M8 的 Global：`$.Global` 与 `$G` 指向同一个对象。
    Global: null,
    // Flash 几何对象与滤镜工厂。未绘制的滤镜类型仍保留可读参数。
    // Flash 的 Vector（数值数组）。entry_08 里还把它当可增长的动态数组用
    // （`var vLocal=$.toNumberVector([]); vLocal.push(...)`），
    // 普通 Array 因此正合适。
    createVector: function (list) {
        return toNumberArray(list, false);
    },
    // Flash 的 Vector3D / Matrix3D：真实现（见 createVector3D / createMatrix3D）。
    createVector3D: function (x, y, z) {
        return createVector3D(x, y, z);
    },
    createMatrix3D: function (rawData) {
        return createMatrix3D(rawData);
    },
    // Flash 的 Matrix：与元素 props.matrix 同一形状（a/b/c/d/tx/ty + 方法）。
    createMatrix: function (a, b, c, d, tx, ty) {
        return createPlaceholderMatrix(a, b, c, d, tx, ty);
    },
    // Flash 的 ColorTransform（ScriptDisplay.as:284-287）。
    // 脚本（entry_08 的 Akari）用它做色调映射：
    //   rgbToTransformTint(rgb) → createColorTransform(r,g,b,alphaRatio)
    //   rgbToTransformAdd(rgb)  → createColorTransform(1,1,1,1,r*255,g*255,b*255,alpha*255)
    // 因此 8 个参数必须真存下来，不能返回 null。
    createColorTransform: function (redMultiplier, greenMultiplier, blueMultiplier,
        alphaMultiplier, redOffset, greenOffset, blueOffset, alphaOffset) {
        return createColorTransform(
            redMultiplier, greenMultiplier, blueMultiplier, alphaMultiplier,
            redOffset, greenOffset, blueOffset, alphaOffset);
    },
    createGlowFilter: function () {
        return createPlaceholderFilter("GlowFilter", arguments);
    },
    createBlurFilter: function () {
        return createPlaceholderFilter("BlurFilter", arguments);
    },
    createDropShadowFilter: function () {
        return createPlaceholderFilter("DropShadowFilter", arguments);
    },
    createBevelFilter: function () {
        return createPlaceholderFilter("BevelFilter", arguments);
    },
    // 原版 createGradientBox(w,h,rotation,tx,ty) 返回一个已经
    // createGradientBox 过的 Matrix（ScriptDisplay.as:129-134）。
    // 宿主矩阵的实例方法已支持该语义（含 gradientBox 记录），直接转发。
    createGradientBox: function (width, height, rotation, tx, ty) {
        var matrix = createPlaceholderMatrix();
        return matrix.createGradientBox(width, height, rotation, tx, ty);
    },
    createBitmapData: function () {
        return null;
    },
    // 原版 createTextFormat 直接转发 flash.text.TextFormat 的 13 参构造
    // （ScriptDisplay.as）。宿主返回同名字段的数据对象。
    createTextFormat: function (font, size, color, bold, italic, underline, url,
        target, align, leftMargin, rightMargin, indent, leading) {
        return {
            font: font === undefined ? null : font,
            size: size === undefined ? null : size,
            color: color === undefined ? null : color,
            bold: bold === undefined ? null : bold,
            italic: italic === undefined ? null : italic,
            underline: underline === undefined ? null : underline,
            url: url === undefined ? null : url,
            target: target === undefined ? null : target,
            align: align === undefined ? null : align,
            leftMargin: leftMargin === undefined ? null : leftMargin,
            rightMargin: rightMargin === undefined ? null : rightMargin,
            indent: indent === undefined ? null : indent,
            leading: leading === undefined ? null : leading
        };
    },
    // 原版 createTextField() 返回一个空白 CommentField（受 motion 管理的文本框）。
    createTextField: function () {
        return M8Display.createText("");
    },
    // 原版 toUIntVector(list) = Vector.<uint>(list)；宿主里 Vector 就是数值数组。
    toUIntVector: function (list) {
        return toNumberArray(list, true);
    },
    // 原版另外五个滤镜工厂（ColorMatrix / Convolution / DisplacementMap /
    // GradientBevel / GradientGlow）。宿主按既有占位约定返回可读对象，
    // 元素 filters 目前绘制 GlowFilter / BlurFilter，其余类型只保留参数。
    createColorMatrixFilter: function () {
        return createPlaceholderFilter("ColorMatrixFilter", arguments);
    },
    createConvolutionFilter: function () {
        return createPlaceholderFilter("ConvolutionFilter", arguments);
    },
    createDisplacementMapFilter: function () {
        return createPlaceholderFilter("DisplacementMapFilter", arguments);
    },
    createGradientBevelFilter: function () {
        return createPlaceholderFilter("GradientBevelFilter", arguments);
    },
    createGradientGlowFilter: function () {
        return createPlaceholderFilter("GradientGlowFilter", arguments);
    },
    createRectangle: function (x, y, width, height) {
        return {
            x: toFiniteNumber(x, 0),
            y: toFiniteNumber(y, 0),
            width: toFiniteNumber(width, 0),
            height: toFiniteNumber(height, 0)
        };
    },
    createPoint: function (x, y) {
        return { x: toFiniteNumber(x, 0), y: toFiniteNumber(y, 0) };
    },
    createColor: function (value) {
        return toFiniteNumber(value, 0);
    },
    hasOwnProperty: function (name) {
        return Object.prototype.hasOwnProperty.call(M8Display, name);
    }
};

// `$` 自身也带舞台尺寸（M8 里 `$` 是 Display 命名空间，`$.width` / `$.height`
// 是舞台宽高）。entry_08 用它算居中与缩放比，缺了会得到 undefined → NaN 几何。
Object.defineProperty(M8Display, "width", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.viewportWidth;
    }
});

Object.defineProperty(M8Display, "height", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.viewportHeight;
    }
});

// 原版 fullScreenWidth / fullScreenHeight 取 stage 的全屏尺寸
// （ScriptDisplay.as：this._layer.stage.fullScreenWidth）。宿主里画布就是
// 视频渲染矩形，等价量是视口宽高。
// 原版 `$` 有 root 属性（ScriptDisplay.as:119-122 的 get root() 返回注释层容器
// _layer）。Akari 的 `Akari.root()` 先判 `$.hasOwnProperty("root") && $.root`：
// 在 AS3 里 getter 属于实例 trait，hasOwnProperty 为真 → 它直接返回 $.root，
// **不会**走 else 分支去 createCanvas + ScriptManager.popEl。
// 宿主原先没有这个属性（M8Display 的 hasOwnProperty 是严格 JS own-property 检查），
// Akari 因此掉进 else、作品被挂到那个会被 popEl 处理的容器上。
// 宿主元素的默认父容器就是 rootElement，这里返回它。
Object.defineProperty(M8Display, "root", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.rootElement;
    }
});

Object.defineProperty(M8Display, "fullScreenWidth", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.viewportWidth;
    }
});

Object.defineProperty(M8Display, "fullScreenHeight", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.viewportHeight;
    }
});

// 原版还有 screenWidth / screenHeight / stageWidth / stageHeight
// （ScriptDisplay.as:79-107；screen* 与 fullScreen* 在原版里返回的是
// 同一个量 —— 四个 getter 里有两个直接读 fullScreenWidth/Height，
// stage* 才是 stage.stageWidth/Height）。宿主里舞台就是画布，
// 四者同为视口宽高，这里补齐名字避免脚本读到 undefined。
Object.defineProperty(M8Display, "screenWidth", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.viewportWidth;
    }
});

Object.defineProperty(M8Display, "screenHeight", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.viewportHeight;
    }
});

Object.defineProperty(M8Display, "stageWidth", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.viewportWidth;
    }
});

Object.defineProperty(M8Display, "stageHeight", {
    configurable: true,
    enumerable: true,
    get: function () {
        return hostState.viewportHeight;
    }
});

// 原版 frameRate 读写 stage.frameRate（ScriptDisplay.as:339-350，
// 写时钳到 (0, 120)）。宿主是 rAF，改不了帧率，但**要能读能写**：
// 脚本写 `$.frameRate = 30` 时若抛错会整条停摆。存一个值、
// 按原版的区间判定接受，不产生实际效果（与 Player.refreshRate 同一处理）。
var displayFrameRate = 60;
Object.defineProperty(M8Display, "frameRate", {
    configurable: true,
    enumerable: true,
    get: function () {
        return displayFrameRate;
    },
    set: function (value) {
        var rate = toFiniteNumber(value, displayFrameRate);
        if (rate > 0 && rate < 120) {
            displayFrameRate = rate;
        }
    }
});

function toNumberArray(list, truncate) {
    var result = [];
    if (list === undefined || list === null) {
        return result;
    }

    var length = toFiniteNumber(list.length, 0);
    for (var index = 0; index < length; index++) {
        var value = toFiniteNumber(list[index], 0);
        result.push(truncate ? Math.trunc(value) : value);
    }

    return result;
}

// 矩阵：M8 的 $.createMatrix() / $.createGradientBox() 返回可参与变换的矩阵对象，
// 本宿主支持 a/b/c/d/tx/ty 六个分量（与 canvas transform 同序），
// createGradientBox 会按 Flash 的公式算出分量并把渐变框记在 gradientBox 上
// （渲染渐变时直接用它，比反解矩阵更准）。
// ---- 2D 变换矩阵（Flash 的 Matrix）----
//
// 与元素已有的 props.matrix 是**同一个对象**：applyElementTransform 读
// a/b/c/d/tx/ty 落到 canvas 变换，脚本通过 el.transform.matrix 拿到它。
// 不另起一套——否则脚本改了矩阵、渲染却按另一份数据走（真实脚本里
// `mx = glyph.transform.matrix; mx.identity();` 这种「取出→原地改→写回」
// 的写法必须是同一份对象才能生效）。
function ensureMatrixMethods(matrix) {
    if (!matrix || typeof matrix !== "object" || typeof matrix.identity === "function") {
        return matrix;
    }

    matrix.identity = function () {
        this.a = 1; this.b = 0; this.c = 0; this.d = 1; this.tx = 0; this.ty = 0;
        return this;
    };
    matrix.translate = function (x, y) {
        this.tx += toFiniteNumber(x, 0);
        this.ty += toFiniteNumber(y, 0);
        return this;
    };
    matrix.scale = function (x, y) {
        x = toFiniteNumber(x, 1); y = toFiniteNumber(y, 1);
        this.a *= x; this.c *= x; this.tx *= x;
        this.b *= y; this.d *= y; this.ty *= y;
        return this;
    };
    matrix.rotate = function (radians) {
        var cos = Math.cos(toFiniteNumber(radians, 0));
        var sin = Math.sin(toFiniteNumber(radians, 0));
        var a = this.a;
        var b = this.b, c = this.c, d = this.d, tx = this.tx, ty = this.ty;
        this.a = a * cos - b * sin; this.b = a * sin + b * cos;
        this.c = c * cos - d * sin; this.d = c * sin + d * cos;
        this.tx = tx * cos - ty * sin; this.ty = tx * sin + ty * cos;
        return this;
    };
    matrix.clone = function () {
        return createPlaceholderMatrix(this.a, this.b, this.c, this.d, this.tx, this.ty);
    };
    matrix.concat = function (other) {
        // Flash 的 Matrix.concat：this = this × other。
        var m = normalizeMatrixObject(other);
        var a = this.a * m.a + this.b * m.c;
        var b = this.a * m.b + this.b * m.d;
        var c = this.c * m.a + this.d * m.c;
        var d = this.c * m.b + this.d * m.d;
        var tx = this.tx * m.a + this.ty * m.c + m.tx;
        var ty = this.tx * m.b + this.ty * m.d + m.ty;
        this.a = a; this.b = b; this.c = c; this.d = d; this.tx = tx; this.ty = ty;
        return this;
    };
    matrix.invert = function () {
        var det = this.a * this.d - this.b * this.c;
        if (!det) {
            return this;
        }

        var a = this.d / det;
        var b = -this.b / det;
        var c = -this.c / det;
        var d = this.a / det;
        var tx = (this.c * this.ty - this.d * this.tx) / det;
        var ty = (this.b * this.tx - this.a * this.ty) / det;
        this.a = a; this.b = b; this.c = c; this.d = d; this.tx = tx; this.ty = ty;
        return this;
    };
    matrix.deltaTransformPoint = function (point) {
        return {
            x: point.x * this.a + point.y * this.c,
            y: point.x * this.b + point.y * this.d
        };
    };
    matrix.transformPoint = function (point) {
        return {
            x: point.x * this.a + point.y * this.c + this.tx,
            y: point.x * this.b + point.y * this.d + this.ty
        };
    };
    // Flash 的 Matrix.createGradientBox(width, height, rotation, tx, ty)：
    // 把渐变框定义成 w×h、绕 (tx,ty) 旋转 rotation。这里除了按 Flash
    // 的公式写出 a/b/c/d/tx/ty（脚本读得到合理值），还把框本身记在
    // gradientBox 上——渲染时直接用它算渐变轴，比反解矩阵更准。
    matrix.createBox = function (width, height, rotation, tx, ty) {
        return matrix.createGradientBox(width, height, rotation, tx, ty);
    };
    matrix.createGradientBox = function (width, height, rotation, tx, ty) {
        var w = toFiniteNumber(width, 0);
        var h = toFiniteNumber(height, 0);
        var angle = toFiniteNumber(rotation, 0);
        var cos = Math.cos(angle);
        var sin = Math.sin(angle);
        matrix.a = w * cos;
        matrix.b = w * sin;
        matrix.c = -h * sin;
        matrix.d = h * cos;
        matrix.tx = toFiniteNumber(tx, 0);
        matrix.ty = toFiniteNumber(ty, 0);
        matrix.gradientBox = {
            width: w,
            height: h,
            rotation: angle,
            tx: matrix.tx,
            ty: matrix.ty
        };
        return matrix;
    };
    return matrix;
}

function createPlaceholderMatrix(a, b, c, d, tx, ty) {
    return ensureMatrixMethods({
        a: a === undefined ? 1 : toFiniteNumber(a, 1),
        b: b === undefined ? 0 : toFiniteNumber(b, 0),
        c: c === undefined ? 0 : toFiniteNumber(c, 0),
        d: d === undefined ? 1 : toFiniteNumber(d, 1),
        tx: tx === undefined ? 0 : toFiniteNumber(tx, 0),
        ty: ty === undefined ? 0 : toFiniteNumber(ty, 0)
    });
}

// 把脚本给的东西归一成矩阵对象：null 保持 null（「不改变换」的语义），
// 已经是矩阵的补齐方法，其它对象按 a/b/c/d/tx/ty 取值。
function normalizeMatrixObject(value) {
    if (value === null || value === undefined) {
        return null;
    }

    if (typeof value !== "object") {
        return null;
    }

    if (typeof value.a === "undefined" && typeof value.d === "undefined") {
        return null;
    }

    return ensureMatrixMethods({
        a: toFiniteNumber(value.a, 1),
        b: toFiniteNumber(value.b, 0),
        c: toFiniteNumber(value.c, 0),
        d: toFiniteNumber(value.d, 1),
        tx: toFiniteNumber(value.tx, 0),
        ty: toFiniteNumber(value.ty, 0)
    });
}

// ---- 3D 向量与 4×4 矩阵（Flash 的 Vector3D / Matrix3D）----
//
// entry_08 的 Akari 库用它做 3D 排序：`mat.appendRotation(...)` 拼出
// 相机矩阵，再用 `transformVectors(vLocal, vWorld)` 把子元件的局部坐标
// 投到世界坐标、按 z 排序后决定绘制顺序。因此这几个方法是真算的，
// 不是占位——算错了排序就错，画面会明显不对。
//
// rawData 用 Flash 的列主序布局（与 OpenGL 同）：
//   [0] [4] [8]  [12]
//   [1] [5] [9]  [13]
//   [2] [6] [10] [14]
//   [3] [7] [11] [15]
var MATRIX3D_IDENTITY = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

function createVector3D(x, y, z) {
    return {
        x: toFiniteNumber(x, 0),
        y: toFiniteNumber(y, 0),
        z: toFiniteNumber(z, 0),
        w: 0,
        length: function () {
            return Math.sqrt(this.x * this.x + this.y * this.y + this.z * this.z);
        },
        clone: function () {
            return createVector3D(this.x, this.y, this.z);
        },
        add: function (other) {
            return createVector3D(
                this.x + toFiniteNumber(other && other.x, 0),
                this.y + toFiniteNumber(other && other.y, 0),
                this.z + toFiniteNumber(other && other.z, 0));
        },
        subtract: function (other) {
            return createVector3D(
                this.x - toFiniteNumber(other && other.x, 0),
                this.y - toFiniteNumber(other && other.y, 0),
                this.z - toFiniteNumber(other && other.z, 0));
        },
        normalize: function () {
            var length = this.length();
            if (length > 0) {
                this.x /= length;
                this.y /= length;
                this.z /= length;
            }

            return this;
        }
    };
}

function createMatrix3D(rawData) {
    var matrix = {
        // 元素顺序与 Flash 一致；脚本里出现过 `mat.position` 这类读法。
        rawData: MATRIX3D_IDENTITY.slice(),
        index: 0
    };

    if (rawData && rawData.length) {
        for (var index = 0; index < 16; index++) {
            matrix.rawData[index] = toFiniteNumber(rawData[index], MATRIX3D_IDENTITY[index]);
        }
    }

    Object.defineProperty(matrix, "position", {
        enumerable: true,
        configurable: true,
        get: function () {
            return createVector3D(
                matrix.rawData[12], matrix.rawData[13], matrix.rawData[14]);
        },
        set: function (value) {
            matrix.rawData[12] = toFiniteNumber(value && value.x, 0);
            matrix.rawData[13] = toFiniteNumber(value && value.y, 0);
            matrix.rawData[14] = toFiniteNumber(value && value.z, 0);
        }
    });

    matrix.identity = function () {
        matrix.rawData = MATRIX3D_IDENTITY.slice();
        return matrix;
    };

    // Flash append：this = other × this（列向量，左乘）。
    matrix.append = function (other) {
        if (!other || !other.rawData) {
            return matrix;
        }

        matrix.rawData = multiplyMatrix3D(other.rawData, matrix.rawData);
        return matrix;
    };

    matrix.prepend = function (other) {
        if (!other || !other.rawData) {
            return matrix;
        }

        matrix.rawData = multiplyMatrix3D(matrix.rawData, other.rawData);
        return matrix;
    };

    matrix.appendTranslation = function (x, y, z) {
        var translation = MATRIX3D_IDENTITY.slice();
        translation[12] = toFiniteNumber(x, 0);
        translation[13] = toFiniteNumber(y, 0);
        translation[14] = toFiniteNumber(z, 0);
        return matrix.append({ rawData: translation });
    };

    matrix.prependTranslation = function (x, y, z) {
        var translation = MATRIX3D_IDENTITY.slice();
        translation[12] = toFiniteNumber(x, 0);
        translation[13] = toFiniteNumber(y, 0);
        translation[14] = toFiniteNumber(z, 0);
        return matrix.prepend({ rawData: translation });
    };

    matrix.appendRotation = function (degrees, axis) {
        return matrix.append(createRotationMatrix3D(degrees, axis));
    };

    matrix.prependRotation = function (degrees, axis) {
        return matrix.prepend(createRotationMatrix3D(degrees, axis));
    };

    matrix.appendScale = function (x, y, z) {
        var scale = MATRIX3D_IDENTITY.slice();
        scale[0] = toFiniteNumber(x, 1);
        scale[5] = toFiniteNumber(y, 1);
        scale[10] = toFiniteNumber(z, 1);
        return matrix.append({ rawData: scale });
    };

    matrix.prependScale = function (x, y, z) {
        var scale = MATRIX3D_IDENTITY.slice();
        scale[0] = toFiniteNumber(x, 1);
        scale[5] = toFiniteNumber(y, 1);
        scale[10] = toFiniteNumber(z, 1);
        return matrix.prepend({ rawData: scale });
    };

    matrix.transformVector = function (vector) {
        var data = matrix.rawData;
        var x = toFiniteNumber(vector && vector.x, 0);
        var y = toFiniteNumber(vector && vector.y, 0);
        var z = toFiniteNumber(vector && vector.z, 0);
        var result = createVector3D(
            x * data[0] + y * data[4] + z * data[8] + data[12],
            x * data[1] + y * data[5] + z * data[9] + data[13],
            x * data[2] + y * data[6] + z * data[10] + data[14]);
        result.w = x * data[3] + y * data[7] + z * data[11] + data[15];
        return result;
    };

    matrix.deltaTransformVector = function (vector) {
        var data = matrix.rawData;
        var x = toFiniteNumber(vector && vector.x, 0);
        var y = toFiniteNumber(vector && vector.y, 0);
        var z = toFiniteNumber(vector && vector.z, 0);
        return createVector3D(
            x * data[0] + y * data[4] + z * data[8],
            x * data[1] + y * data[5] + z * data[9],
            x * data[2] + y * data[6] + z * data[10]);
    };

    // Flash 的 transformVectors(vector, resultVector)：vector 是 xyz 三元组
    // 的扁平淡数组，结果**写进第二个参数**（就地填充，不是返回新数组）。
    // Akari 的排序正是 `transform.transformVectors(vLocal, vWorld)` 之后
    // 读 `vWorld[b*3+2]` 取 z，所以这里必须原地填。
    matrix.transformVectors = function (source, target) {
        if (!source || !target) {
            return target;
        }

        // 单个 Vector3D 形态的入参：也支持（注意 Vector3D 自带 length 方法，
        // 不能用 source.length 判类型，要用 Array.isArray）。
        if (!Array.isArray(source)) {
            var single = matrix.transformVector(source);
            target.x = single.x;
            target.y = single.y;
            target.z = single.z;
            return target;
        }

        var count = Math.floor(source.length / 3);
        for (var index = 0; index < count; index++) {
            var transformed = matrix.transformVector(createVector3D(
                source[index * 3],
                source[index * 3 + 1],
                source[index * 3 + 2]));
            target[index * 3] = transformed.x;
            target[index * 3 + 1] = transformed.y;
            target[index * 3 + 2] = transformed.z;
        }

        return target;
    };

    matrix.clone = function () {
        return createMatrix3D(matrix.rawData);
    };

    matrix.invert = function () {
        matrix.rawData = invertMatrix3D(matrix.rawData);
        return matrix;
    };

    matrix.copyFrom = function (other) {
        if (other && other.rawData) {
            matrix.rawData = other.rawData.slice();
        }

        return matrix;
    };

    return matrix;
}

function multiplyMatrix3D(left, right) {
    var result = [];
    for (var column = 0; column < 4; column++) {
        for (var row = 0; row < 4; row++) {
            var sum = 0;
            for (var k = 0; k < 4; k++) {
                sum += left[k * 4 + row] * right[column * 4 + k];
            }

            result[column * 4 + row] = sum;
        }
    }

    return result;
}

// 绕任意轴旋转（度）。轴为零向量时退回单位矩阵，不抛错。
function createRotationMatrix3D(degrees, axis) {
    var ax = toFiniteNumber(axis && axis.x, 0);
    var ay = toFiniteNumber(axis && axis.y, 0);
    var az = toFiniteNumber(axis && axis.z, 0);
    var length = Math.sqrt(ax * ax + ay * ay + az * az);
    if (length === 0) {
        return { rawData: MATRIX3D_IDENTITY.slice() };
    }

    ax /= length;
    ay /= length;
    az /= length;
    var radians = toFiniteNumber(degrees, 0) * Math.PI / 180;
    var cos = Math.cos(radians);
    var sin = Math.sin(radians);
    var oneMinusCos = 1 - cos;
    var data = MATRIX3D_IDENTITY.slice();
    data[0] = cos + ax * ax * oneMinusCos;
    data[1] = ay * ax * oneMinusCos + az * sin;
    data[2] = az * ax * oneMinusCos - ay * sin;
    data[4] = ax * ay * oneMinusCos - az * sin;
    data[5] = cos + ay * ay * oneMinusCos;
    data[6] = az * ay * oneMinusCos + ax * sin;
    data[8] = ax * az * oneMinusCos + ay * sin;
    data[9] = ay * az * oneMinusCos - ax * sin;
    data[10] = cos + az * az * oneMinusCos;
    return { rawData: data };
}

function invertMatrix3D(data) {
    // 4×4 求逆用「伴随矩阵 / 行列式」的展开式过于冗长；这里用
    // 分块（左上 3×3 + 平移列）求逆——脚本里的矩阵都由平移/旋转/缩放
    // 组成，属于这一族，反解足够正确。
    var a = data[0], b = data[4], c = data[8];
    var d = data[1], e = data[5], f = data[9];
    var g = data[2], h = data[6], i = data[10];
    var det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
    if (!det) {
        return MATRIX3D_IDENTITY.slice();
    }

    var inverse = MATRIX3D_IDENTITY.slice();
    inverse[0] = (e * i - f * h) / det;
    inverse[1] = (c * h - b * i) / det;
    inverse[2] = (b * f - c * e) / det;
    inverse[4] = (f * g - d * i) / det;
    inverse[5] = (a * i - c * g) / det;
    inverse[6] = (c * d - a * f) / det;
    inverse[8] = (d * h - e * g) / det;
    inverse[9] = (b * g - a * h) / det;
    inverse[10] = (a * e - b * d) / det;
    var tx = data[12], ty = data[13], tz = data[14];
    inverse[12] = -(tx * inverse[0] + ty * inverse[4] + tz * inverse[8]);
    inverse[13] = -(tx * inverse[1] + ty * inverse[5] + tz * inverse[9]);
    inverse[14] = -(tx * inverse[2] + ty * inverse[6] + tz * inverse[10]);
    return inverse;
}

// Flash 的 PerspectiveProjection：纯数据对象（M8 的 clone() 明确不复制
// 函数，脚本也是 clone 出来当数据用），默认值与 Flash 一致。
function createPerspectiveProjection() {
    var fieldOfView = 55;
    var width = hostState.viewportWidth || 500;
    var projection = { projectionCenter: createVector3D(width / 2, (hostState.viewportHeight || 500) / 2, 0) };
    Object.defineProperty(projection, "fieldOfView", {
        enumerable: true, get: function () { return fieldOfView; },
        set: function (value) { fieldOfView = Math.max(0.00001, Math.min(179.99999, Number(value))); }
    });
    Object.defineProperty(projection, "focalLength", {
        enumerable: true, get: function () { return width / (2 * Math.tan(fieldOfView * Math.PI / 360)); },
        set: function (value) { fieldOfView = 360 * Math.atan(width / (2 * Number(value))) / Math.PI; }
    });
    return projection;
}

// 原版工厂的参数顺序及默认值（ScriptDisplay.as）。
function createPlaceholderFilter(kind, args) {
    var filter = { type: kind, kind: kind };
    var names = kind === "BlurFilter" ? ["blurX", "blurY", "quality"]
        : ["color", "alpha", "blurX", "blurY", "strength", "quality", "inner", "knockout"];
    var defaults = kind === "BlurFilter" ? [0, 0, 1] : [16711680, 1, 6, 6, 2, 1, false, false];
    for (var i = 0; i < names.length; i++) filter[names[i]] = args[i] === undefined ? defaults[i] : args[i];
    return filter;
}

// Flash 的 ColorTransform：8 个分量齐全、可读可写。
// 渲染器逐通道应用乘数与偏移；容器的颜色变换作用于整份合成结果。
function createColorTransform(redMultiplier, greenMultiplier, blueMultiplier,
    alphaMultiplier, redOffset, greenOffset, blueOffset, alphaOffset) {
    return {
        redMultiplier: toFiniteNumber(redMultiplier, 1),
        greenMultiplier: toFiniteNumber(greenMultiplier, 1),
        blueMultiplier: toFiniteNumber(blueMultiplier, 1),
        alphaMultiplier: toFiniteNumber(alphaMultiplier, 1),
        redOffset: toFiniteNumber(redOffset, 0),
        greenOffset: toFiniteNumber(greenOffset, 0),
        blueOffset: toFiniteNumber(blueOffset, 0),
        alphaOffset: toFiniteNumber(alphaOffset, 0)
    };
}

// 元素工厂的统一入口：**先**接进元素树、**再**套用创建参数。
//
// 顺序不能反（踩过的坑）：applyCreateOptions 会声明元素寿命
// （applyElementLifeTime）并建立声明式 motion（createDrivenHandle
// 要把句柄登记到 element.ownerItem.tweenHandles）。而
// registerItemElement 会把 declaredLifeTimeMs 重置成「未声明」，
// 并在更早的时刻把 ownerItem 从 null 设成当前条目。因此
//  「先套参数再注册」会让寿命声明被覆盖、句柄挂到 null 上永不推进。
function createItemElement(factory, options) {
    var item = currentItem();
    if (!item) {
        // 定时器回调与脚本正文都在 activeItem 下执行；
        // 真正落到这里说明宿主内部有调用点漏了 activeItem 赋值。
        throw new Error("脚本元件只能在脚本执行或定时器回调中创建");
    }

    var element = registerItemElement(item, factory(item));
    applyCreateOptions(element, options);
    return element;
}

export {
    M8Display,
    MATRIX3D_IDENTITY,
    applyElementBlendMode,
    applyElementMaskClip,
    attachElement,
    createMatrix3D,
    createPerspectiveProjection,
    createPlaceholderMatrix,
    createRetainedElement,
    defineHiddenValue,
    detachElement,
    dispatchItemEnterFrame,
    drawTextElement,
    isElementDirty,
    isElementVisible,
    isUsedAsMask,
    markAllDirty,
    markItemDirty,
    measureTextElement,
    readMotionDeclaredSeconds,
    releaseElementCaches,
    removeChildFromParent,
    setPropertyInternal
};
