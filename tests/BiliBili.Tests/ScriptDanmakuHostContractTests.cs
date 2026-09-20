using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    /// <summary>
    /// 脚本弹幕宿主的源码契约。宿主是纯 JS 资产，无法在本测试项目中执行，
    /// 因此这里断言「跨进程双方共同依赖的字符串约定」，避免任一侧被单方面改动。
    /// 阶段 1 的宿主已从立即模式改为保留模式（见设计文档 §3.1）：
    /// 脚本每条只执行一次，逐帧只推进 tween 与重绘脏元素。
    /// </summary>
    [TestClass]
    public class ScriptDanmakuHostContractTests
    {
        private const string HostPath =
            "BiliBili.UWP/Assets/script-danmaku-host.html";

        private const string ControlPath =
            "BiliBili.UWP/Controls/ScriptDanmakuControl.xaml.cs";

        private static string HostSource()
        {
            return TestRepository.ReadFile(HostPath);
        }

        /// <summary>取 createContext 的函数体，用于断言脚本上下文的能力边界。</summary>
        private static string ContextBody()
        {
            return TestRepository.MethodBody(HostSource(), "function createContext(item, now) {");
        }

        /// <summary>取 tick 的函数体，用于断言每帧流程与可见性拦截。</summary>
        private static string TickBody()
        {
            return TestRepository.MethodBody(HostSource(), "function tick(now) {");
        }

        /// <summary>
        /// 取「带脏矩形擦除」的那版 paintDirtyElements 函数体。
        /// 文件里另有一处同名函数（无擦除的早期截面），只靠函数签名会取到前者，
        /// 因此把锚点前移到它独有的 composeElement 定义之后。
        /// </summary>
        private static string PaintBody()
        {
            return TestRepository.MethodBody(
                HostSource(),
                "blitElement(context2d, element);\n                recordElementRect(element);\n            }\n\n            function paintDirtyElements() {");
        }

        [TestMethod]
        public void Host_ExposesScriptDanmakuHostEntryPoint()
        {
            StringAssert.Contains(
                HostSource(),
                "window.scriptDanmakuHost = {");
        }

        [TestMethod]
        public void Host_ExposesEveryCommandTheControlInvokes()
        {
            // 控件通过 ExecuteScriptAsync 调用这些方法，名称必须逐字一致。
            // setState 直接引用函数而非内联定义，因此单独断言。
            var source = HostSource();
            foreach (var command in new[]
            {
                "reset: function",
                "append: function",
                "beginItem: function",
                "appendItemChunk: function",
                "endItem: function",
                "seek: function",
                "visible: function",
                "resize: function"
            })
            {
                StringAssert.Contains(source, command, command);
            }

            StringAssert.Contains(source, "setState: setState,");
        }

        [TestMethod]
        public void Host_ReportsEveryMessageTypeTheControlHandles()
        {
            var source = HostSource();
            foreach (var messageType in new[]
            {
                "post(\"ready\")",
                "post(\"parsed\"",
                "post(\"rendered\"",
                "post(\"error\""
            })
            {
                StringAssert.Contains(source, messageType, messageType);
            }
        }

        [TestMethod]
        public void Host_ReportsActionMessagesForSeekNavigateAndPause()
        {
            var source = HostSource();
            StringAssert.Contains(source, "action: \"seek\"");
            StringAssert.Contains(source, "action: \"navigate\"");
            StringAssert.Contains(source, "action: \"pause\"");
        }

        // ---- 保留模式：脚本只执行一次 ----

        [TestMethod]
        public void Host_CompilesEachScriptExactlyOnceAtRegistration()
        {
            // 保留模式的根契约：整份宿主里只允许一处 new Function，
            // 且它在 compileItem 里被调用，编译结果缓存到 item.run。
            var source = HostSource();
            var occurrences = 0;
            var index = source.IndexOf("new Function(\"ctx\", code)", System.StringComparison.Ordinal);
            while (index >= 0)
            {
                occurrences++;
                index = source.IndexOf(
                    "new Function(\"ctx\", code)",
                    index + 1,
                    System.StringComparison.Ordinal);
            }

            Assert.AreEqual(
                1,
                occurrences,
                "宿主只应在 compileItem 里编译脚本，逐帧路径不得再出现 new Function");
            StringAssert.Contains(source, "item.run = compileItem(model);");
        }

        [TestMethod]
        public void Host_RunsScriptBodiesOnlyFromActivateItem()
        {
            // 脚本体的唯一执行点是 activateItem；它又只被 updateItems 里
            // 「首次进入时间窗」的分支调用，因此同一播放过程里不会重跑。
            var source = HostSource();
            StringAssert.Contains(source, "item.run(createContext(item, now));");

            // 执行一次就记一次数：这是「脚本只跑一次」的可观测落点。
            var activateBody = TestRepository.MethodBody(source, "function activateItem(item, now) {");
            StringAssert.Contains(activateBody, "item.runCount++;");
            StringAssert.Contains(activateBody, "totalRunCount++;");

            var updateBody = TestRepository.MethodBody(source, "function updateItems(now) {");
            StringAssert.Contains(updateBody, "activateItem(item, now);");
            StringAssert.Contains(updateBody, "advanceItem(item, now);");

            var advanceBody = TestRepository.MethodBody(source, "function advanceItem(item, now) {");
            Assert.IsFalse(
                advanceBody.Contains("item.run("),
                "逐帧路径 advanceItem 不得重跑脚本");
        }

        [TestMethod]
        public void Host_ContextHasNoPerFrameFields()
        {
            // 保留模式下脚本执行时没有任何时间推进，t/progress 恒定是 0：
            // 它们不得再被用来驱动逐帧机制。
            var body = ContextBody();
            StringAssert.Contains(body, "t: 0,");
            StringAssert.Contains(body, "progress: 0,");
        }

        [TestMethod]
        public void Host_ContextDoesNotExposeImmediateModeCanvas()
        {
            // 立即模式的 ctx.g 已取消：脚本只能建保留元素，不能直接拿到画布逐帧画。
            var source = HostSource();
            Assert.IsFalse(
                ContextBody().Contains("g: context2d"),
                "保留模式的 ctx 不应再暴露 ctx.g");
            Assert.IsFalse(
                source.Contains("ctx.g."),
                "宿主与内置示例都不应再使用 ctx.g");
        }

        // ---- 保留模式的元素与 tween API ----

        [TestMethod]
        public void Host_ExposesDocumentedContextFields()
        {
            var body = ContextBody();

            // 设计文档 §3 约定的只读上下文。改动这里等于破坏脚本 API 契约。
            foreach (var field in new[]
            {
                "width: viewportWidth",
                "height: viewportHeight",
                "dpr: devicePixelRatioValue",
                "duration: durationSeconds",
                "stime: item.model.stime",
                "id: item.model.id",
                "pause: requestPause",
                "seek: requestSeek",
                "navigate: requestNavigate"
            })
            {
                StringAssert.Contains(body, field, field);
            }
        }

        [TestMethod]
        public void Host_ExposesRetainedModeElementFactories()
        {
            var body = ContextBody();
            foreach (var factory in new[]
            {
                "createText: function",
                "createShape: function",
                "createImage: function",
                "createLayer: function",
                "addChild: function",
                "removeChild: function",
                "tween: function",
                "onFrame: function"
            })
            {
                StringAssert.Contains(body, factory, factory);
            }

            // 元素创建后必须自动归属当前条目，否则到期无人回收。
            StringAssert.Contains(body, "registerItemElement(item, createTextElement(text, style))");
            StringAssert.Contains(body, "registerItemElement(item, createShapeElement())");
            StringAssert.Contains(body, "registerItemElement(item, createImageElement(url))");
            StringAssert.Contains(body, "registerItemElement(item, createLayerElement(width, height))");

            // tween 走 M8 语义的补间引擎。
            StringAssert.Contains(body, "return createTween(element, config, options);");
        }

        [TestMethod]
        public void Host_OnFrameIsDocumentedAsALastResort()
        {
            // onFrame 是逃生舱，注册后该条目退回逐帧调用。
            // 缺了这条约束，后来者会把它当成首选写法，单条脚本的每帧开销
            // 就不再只与「脏元素数」相关。
            var body = ContextBody();
            StringAssert.Contains(body, "onFrame: function (fn) {");
            StringAssert.Contains(body, "item.usesOnFrame = typeof fn === \"function\";");
            StringAssert.Contains(body, "item.frameFn = item.usesOnFrame ? fn : null;");

            // 逃生舱只应在 advanceItem 里被调用，且失败后要自行摘掉，
            // 否则坏脚本会每帧抛错刷屏。
            var advanceBody = TestRepository.MethodBody(
                HostSource(),
                "function advanceItem(item, now) {");
            StringAssert.Contains(advanceBody, "if (item.usesOnFrame && typeof item.frameFn === \"function\") {");
            StringAssert.Contains(advanceBody, "item.usesOnFrame = false;");
            StringAssert.Contains(advanceBody, "item.frameFn = null;");
        }

        [TestMethod]
        public void Host_TextStyleDefaultsMatchM8()
        {
            // 默认值与 M8 的 createComment 一致：白字、黑体、25px。
            var source = HostSource();
            StringAssert.Contains(source, "var DEFAULT_TEXT_COLOR = 16777215;");
            StringAssert.Contains(source, "var DEFAULT_TEXT_FONT = \"黑体\";");
            StringAssert.Contains(source, "var DEFAULT_TEXT_FONTSIZE = 25;");
        }

        // ---- 保留模式的逐帧流程：脏标记 + 离屏缓存 + 呈现 ----

        [TestMethod]
        public void Host_IsolatesScriptFailuresFromTheRenderLoop()
        {
            // 单个脚本抛错不能中断整帧，也不能影响其它条目。
            var source = HostSource();
            StringAssert.Contains(source, "reportRuntimeError(error, item.model.id);");
            StringAssert.Contains(source, "item.compileFailed = true;");

            var tickBody = TickBody();
            Assert.IsFalse(
                tickBody.Contains("reportRuntimeError"),
                "错误隔离应落在条目级路径里，而不是把异常抛到整帧流程");
        }

        [TestMethod]
        public void Host_TickOnlyRepaintsWhenDirty()
        {
            // 每帧不再是全屏清空 + 重跑脚本，而是「有脏标记才合成」。
            // 注意：这里**不能**断言 tick 里没有 clearRect。整屏清空是隐藏 /
            // 停止两条路径的正当行为，而「元素级矩形擦除」才是移动元素不留
            // 拖影的落点；两者都在 tick 的调用链上（见 D1）。
            var body = TickBody();
            StringAssert.Contains(body, "if (dirty) {");
            StringAssert.Contains(body, "dirty = false;");
            StringAssert.Contains(body, "paintDirtyElements();");

            // 合成必须走「脏元素才重画」的路径，而不是整帧重画。
            var paintBody = PaintBody();
            StringAssert.Contains(paintBody, "if (prepareElement(element)) {");
            StringAssert.Contains(paintBody, "composeElement(candidate);");
        }

        [TestMethod]
        public void Host_SkipsCompositingWhenHiddenAndKeepsCanvasBlank()
        {
            // setState / resize 也会进 tick。若不拦隐藏状态，
            // 关闭弹幕总开关后紧跟的 setState 会把脏元素重新合成回屏幕。
            var body = TickBody();
            StringAssert.Contains(body, "if (!visible) {");
            StringAssert.Contains(body, "clearSurface();");
            StringAssert.Contains(body, "return false;");

            // 隐藏分支必须在合成之前直接返回。
            var hideIndex = body.IndexOf("if (!visible) {", System.StringComparison.Ordinal);
            var paintIndex = body.IndexOf("paintDirtyElements();", System.StringComparison.Ordinal);
            Assert.IsTrue(
                hideIndex >= 0 && paintIndex > hideIndex,
                "可见性判断必须早于合成步骤");

            var hideBranch = body.Substring(hideIndex, body.IndexOf("}", body.IndexOf("return false;", hideIndex, System.StringComparison.Ordinal), System.StringComparison.Ordinal) - hideIndex);
            Assert.IsFalse(
                hideBranch.Contains("paintDirtyElements("),
                "隐藏分支内不得出现合成调用");
            StringAssert.Contains(hideBranch, "dirty = false;");
        }

        [TestMethod]
        public void Host_SelfStopsInsideTheFrameCallback()
        {
            // 自停若放在 tick 内部、frame() 又无条件续帧，
            // 恢复播放时会与已排队的回调形成两条并行帧链（每帧画两次）。
            var source = HostSource();
            StringAssert.Contains(source, "var anyActive = tick(now);");
            StringAssert.Contains(source, "running = false;");
            StringAssert.Contains(source, "frameHandle = 0;");

            var frameBody = TestRepository.MethodBody(source, "function frame() {");
            Assert.IsTrue(
                frameBody.Contains("if (!anyActive && !state.playing) {"),
                "自停条件必须写在 frame() 内部");

            var stopIndex = frameBody.IndexOf("if (!anyActive && !state.playing) {", System.StringComparison.Ordinal);
            var scheduleIndex = frameBody.IndexOf(
                "frameHandle = window.requestAnimationFrame(frame);",
                System.StringComparison.Ordinal);
            Assert.IsTrue(
                stopIndex >= 0 && scheduleIndex > stopIndex,
                "自停判断必须早于续帧调度");

            // 续帧在 finally 里，且以 running 为条件——异常不能把帧链剪断。
            Assert.IsTrue(
                frameBody.Contains("} finally {"),
                "帧调度必须放在 finally 里，避免脚本异常冻结帧循环");
            Assert.IsTrue(
                frameBody.Contains("if (running) {"),
                "续帧必须以 running 为条件");

            Assert.IsFalse(
                TickBody().Contains("running = false;"),
                "自停不应留在 tick 内部");
        }

        [TestMethod]
        public void Host_MarksOnlyDirtyElementsForRepaint()
        {
            // 静态元素的离屏缓存是「静态元素首帧之后不再重绘」的落点。
            var source = HostSource();
            StringAssert.Contains(source, "function rebuildElementCache(element) {");
            StringAssert.Contains(source, "element.cacheCanvas = document.createElement(\"canvas\");");
            StringAssert.Contains(source, "if (!element.painted || element.needsCache) {");

            // 只有脏元素才走合成。
            var paintBody = TestRepository.MethodBody(
                source,
                "function paintDirtyElements() {");
            StringAssert.Contains(paintBody, "if (prepareElement(element)) {");
            StringAssert.Contains(paintBody, "blitElement(context2d, element);");

            // 元素属性可写：赋值即标脏（属性描述符的 setter 里做）。
            var descriptorBody = TestRepository.MethodBody(
                source,
                "function propertyDescriptor(name) {");
            StringAssert.Contains(descriptorBody, "setPropertyInternal(this, name, value, true);");
            StringAssert.Contains(descriptorBody, "markPropertyDirty(this, name);");

            // tween 逐帧写入的值同样要标脏，否则元素动了但不会被重绘。
            var motionBody = TestRepository.MethodBody(
                source,
                "function applyMotion(motion, element, elapsedMs) {");
            StringAssert.Contains(motionBody, "markTweenKeyDirty(element, track.key);");
        }

        [TestMethod]
        public void Host_RegistersElementsSoDpiChangesRebuildEveryCache()
        {
            // markAllDirty() 遍历的是 elements 登记表；元素不登记则
            // resize / 改 DPI 后旧缓存不会重建，画面糊在旧比例上。
            var source = HostSource();
            StringAssert.Contains(source, "function registerElement(element) {");
            StringAssert.Contains(source, "elements.push(element);");
            StringAssert.Contains(source, "function unregisterSubtree(element) {");

            var attachBody = TestRepository.MethodBody(
                source,
                "function attachElement(element, parent) {");
            StringAssert.Contains(attachBody, "registerElement(element);");

            var detachBody = TestRepository.MethodBody(
                source,
                "function detachElement(element) {");
            StringAssert.Contains(detachBody, "unregisterSubtree(element);");

            var markAllBody = TestRepository.MethodBody(source, "function markAllDirty() {");
            StringAssert.Contains(markAllBody, "elements[index].needsCache = true;");
        }

        [TestMethod]
        public void Host_ReleasesElementCachesOnLifetimeExpiry()
        {
            // lifeTime 到期由宿主摘除元素并释放离屏缓存，脚本不负责清理。
            // 元素寿命 = min(脚本声明的 lifeTime, 条目窗口剩余时间)：
            // 未声明 tween 的元素寿命就等于窗口剩余时间，不存在固定 3 秒的缺省。
            var source = HostSource();
            StringAssert.Contains(source, "var LIFE_TIME_UNBOUNDED = Infinity;");
            StringAssert.Contains(source, "function resolveElementLifeTimeMs(item, declaredLifeTimeMs) {");

            var resolveBody = TestRepository.MethodBody(
                source,
                "function resolveElementLifeTimeMs(item, declaredLifeTimeMs) {");
            StringAssert.Contains(resolveBody, "var remaining = item.endMs - item.startMs;");
            StringAssert.Contains(resolveBody, "var lifeTimeMs = Math.min(declaredLifeTimeMs, remaining);");

            // 声明值必须与窗口值分开存：否则 min 会退化成「窗口永远胜出」，
            // 脚本声明的寿命只能延长、永远无法缩短。
            var applyBody = TestRepository.MethodBody(
                source,
                "function applyElementLifeTime(element, declaredLifeTimeMs) {");
            StringAssert.Contains(applyBody, "element.declaredLifeTimeMs = declared;");
            StringAssert.Contains(
                applyBody,
                "element.lifeTimeMs = item ? resolveElementLifeTimeMs(item, declared) : declared;");

            var registerBody = TestRepository.MethodBody(
                source,
                "function registerItemElement(item, element) {");
            StringAssert.Contains(
                registerBody,
                "element.lifeTimeMs = resolveElementLifeTimeMs(item, LIFE_TIME_UNBOUNDED);");

            var advanceBody = TestRepository.MethodBody(source, "function advanceItem(item, now) {");
            StringAssert.Contains(advanceBody, "if (elapsed >= element.lifeTimeMs) {");
            StringAssert.Contains(advanceBody, "releaseItemElement(item, index);");

            var releaseBody = TestRepository.MethodBody(
                source,
                "function releaseItemElement(item, index) {");
            StringAssert.Contains(releaseBody, "detachElement(element);");
            StringAssert.Contains(releaseBody, "item.elements.splice(index, 1);");
            // 必须先摘除再标 expired：markElementMoved 见到 expired 会直接返回，
            // 顺序反了元素最后一帧的像素就入不了擦除队列（见 D6）。
            Assert.IsTrue(
                releaseBody.IndexOf("detachElement(element);", System.StringComparison.Ordinal)
                    < releaseBody.IndexOf("element.expired = true;", System.StringComparison.Ordinal),
                "releaseItemElement 必须先 detachElement 再标 expired，否则残影擦不掉");

            var detachBody = TestRepository.MethodBody(
                source,
                "function detachElement(element) {");
            StringAssert.Contains(detachBody, "markElementMoved(element);");
            StringAssert.Contains(detachBody, "releaseElementCaches(element);");
        }

        [TestMethod]
        public void Host_ReinterpolatesTweensOnSeekWithoutRerunningScripts()
        {
            // seek 后按新位置重算 tween 插值，而不是重跑脚本。
            var source = HostSource();
            StringAssert.Contains(source, "function resetItemsForSeek(now) {");
            var seekBody = TestRepository.MethodBody(
                source,
                "function resetItemsForSeek(now) {");
            StringAssert.Contains(seekBody, "element.motion.lastElapsedMs = -1;");
            Assert.IsFalse(
                seekBody.Contains("item.run("),
                "seek 不得重跑脚本");

            var seekToBody = TestRepository.MethodBody(
                source,
                "function seekTo(positionSeconds, playing, rate) {");
            StringAssert.Contains(seekToBody, "resetItemsForSeek(now);");

            // 向后 seek 越过窗口后元素已被释放，再拖回来只能重建：
            // 重建会再执行一次脚本（这是唯一能知道脚本建了哪些元素的做法），
            // 但绝不能逐帧重跑——重建入口只在 resetItemsForSeek 里。
            var rebuildBody = TestRepository.MethodBody(
                source,
                "function rebuildItemElementsForSeek(item, now) {");
            StringAssert.Contains(rebuildBody, "deactivateItem(item);");
            StringAssert.Contains(rebuildBody, "activateItem(item, now);");

            var occurrences = 0;
            var index = source.IndexOf("rebuildItemElementsForSeek(item, now);", System.StringComparison.Ordinal);
            while (index >= 0)
            {
                occurrences++;
                index = source.IndexOf(
                    "rebuildItemElementsForSeek(item, now);",
                    index + 1,
                    System.StringComparison.Ordinal);
            }

            Assert.AreEqual(
                1,
                occurrences,
                "元素重建只应发生在 resetItemsForSeek 这一处，不得进入逐帧路径");
        }

        // ---- 保留模式的正确性细则（与 tests/host/retained-mode.test.js 对应）----

        [TestMethod]
        public void Host_ClearsMovedElementPixelsWithDirtyRects()
        {
            // D1：移动元素不留拖影靠的是「按元素包围盒擦除」，
            // 而不是每帧整屏 clearRect（那正是立即模式的做法）。
            var source = HostSource();
            StringAssert.Contains(source, "function retireElementRect(element) {");
            StringAssert.Contains(source, "function flushEraseRects(rects) {");
            StringAssert.Contains(source, "function computeElementCanvasRect(element) {");
            StringAssert.Contains(source, "var DIRTY_RECT_PADDING = 2;");

            // 擦除必须用单位变换：矩形是主画布坐标，跟着元素矩阵走会擦错位置。
            var flushBody = TestRepository.MethodBody(
                source,
                "function flushEraseRects(rects) {");
            StringAssert.Contains(flushBody, "context2d.setTransform(1, 0, 0, 1, 0, 0);");

            // 顺序：先擦上一帧的包围盒，再合成本帧的脏元素。反了会把刚画好的擦掉。
            var paintBody = PaintBody();
            var flushIndex = paintBody.IndexOf("flushEraseRects(erasedRects);", System.StringComparison.Ordinal);
            var composeIndex = paintBody.IndexOf("composeElement(candidate);", System.StringComparison.Ordinal);
            Assert.IsTrue(
                flushIndex >= 0 && composeIndex > flushIndex,
                "擦除必须早于本帧合成");

            // 整屏 clearSurface 只允许出现在「隐藏 / 停止」两条整幅作废的路径上
            // （tick 的隐藏分支 + stopRunning），逐帧路径不得整屏清空。
            var callSites = new System.Collections.Generic.List<int>();
            var index = source.IndexOf("clearSurface();", System.StringComparison.Ordinal);
            while (index >= 0)
            {
                callSites.Add(index);
                index = source.IndexOf("clearSurface();", index + 1, System.StringComparison.Ordinal);
            }

            Assert.AreEqual(
                2,
                callSites.Count,
                "clearSurface() 只应有 tick 隐藏分支与 stopRunning 两处调用点");

            var tickBody = TickBody();
            var tickStart = source.IndexOf("function tick(now) {", System.StringComparison.Ordinal);
            var stopBody = TestRepository.MethodBody(source, "function stopRunning() {");
            var stopStart = source.IndexOf("function stopRunning() {", System.StringComparison.Ordinal);
            foreach (var callSite in callSites)
            {
                Assert.IsTrue(
                    (callSite > tickStart && callSite < tickStart + tickBody.Length)
                        || (callSite > stopStart && callSite < stopStart + stopBody.Length),
                    "clearSurface() 不得出现在隐藏 / 停止之外的路径上");
            }
        }

        [TestMethod]
        public void Host_KeepsFrameChainAliveWhenScriptThrows()
        {
            // D2：缓动是脚本传入的函数，抛错时若让异常从 rAF 回调逃逸，
            // running 会停在 true 而实际已无排队回调，帧循环永久冻结。
            var frameBody = TestRepository.MethodBody(HostSource(), "function frame() {");
            StringAssert.Contains(frameBody, "} catch (error) {");
            StringAssert.Contains(frameBody, "reportCompositeError(error, \"\");");
            StringAssert.Contains(frameBody, "} finally {");

            var finallyIndex = frameBody.IndexOf("} finally {", System.StringComparison.Ordinal);
            var scheduleIndex = frameBody.IndexOf(
                "frameHandle = window.requestAnimationFrame(frame);",
                System.StringComparison.Ordinal);
            Assert.IsTrue(
                scheduleIndex > finallyIndex,
                "续帧调度必须在 finally 里，异常不得剪断帧链");

            // 元素级失败也不能拖垮同帧其它元素：只标该元素失败并跳过。
            var advanceBody = TestRepository.MethodBody(
                HostSource(),
                "function advanceItem(item, now) {");
            StringAssert.Contains(advanceBody, "element.failed = true;");
            StringAssert.Contains(advanceBody, "failItem(item, error);");
        }

        [TestMethod]
        public void Host_RebuildsCompositeLayersOnlyWhenStructureChanges()
        {
            // D4：复合层只在结构真变时重烘，否则每帧白烘一整张视口大小的层。
            var source = HostSource();
            var prepareBody = TestRepository.MethodBody(
                source,
                "function prepareElement(element) {");
            StringAssert.Contains(prepareBody, "var structural = !element.painted || element.needsCache;");
            StringAssert.Contains(
                prepareBody,
                "if (structural || element.compositeDirty || childrenChanged) {");
            StringAssert.Contains(prepareBody, "element.needsCache = false;");

            // 子元素重建要让父层重烘（childrenChanged），
            // 但父层自己的 needsCache 必须在同一帧清掉，否则每帧都是结构脏。
            StringAssert.Contains(source, "function rebuildComposite(element) {");
            StringAssert.Contains(source, "element.compositeDirty = false;");
        }

        [TestMethod]
        public void Host_InvalidatesElementCacheOnlyForContentProperties()
        {
            // D5：移动 / 缩放 / 旋转只改变换矩阵，缓存位图要复用；
            // 只有影响位图内容的属性（字号、颜色、文本…）才让缓存失效。
            var source = HostSource();
            StringAssert.Contains(source, "var TRANSFORM_ONLY_KEYS = {");
            StringAssert.Contains(source, "function invalidateElementCache(element) {");

            var markBody = TestRepository.MethodBody(
                source,
                "function markPropertyDirty(element, name) {");
            StringAssert.Contains(markBody, "if (!TRANSFORM_ONLY_KEYS[name]) {");
            StringAssert.Contains(markBody, "invalidateElementCache(element);");

            var tweenBody = TestRepository.MethodBody(
                source,
                "function markTweenKeyDirty(element, key) {");
            StringAssert.Contains(tweenBody, "if (!TRANSFORM_ONLY_KEYS[key]) {");
            StringAssert.Contains(tweenBody, "invalidateElementCache(element);");

            // fontsize 属于内容类属性，必须不在复用白名单里。
            var keysBody = TestRepository.MethodBody(source, "var TRANSFORM_ONLY_KEYS = {");
            Assert.IsFalse(
                keysBody.Contains("fontsize"),
                "fontsize 改变位图内容，不得列入只改变换的复用白名单");
            Assert.IsFalse(
                keysBody.Contains("text:"),
                "文本内容改变位图内容，不得列入只改变换的复用白名单");
        }

        // ---- 既有契约（立即模式时已确立，保留模式下必须继续成立）----

        [TestMethod]
        public void Host_ContextActionHelpersEmitMatchingActions()
        {
            var source = HostSource();
            StringAssert.Contains(source, "post(\"action\", { action: \"pause\" });");
            StringAssert.Contains(source, "post(\"action\", { action: \"seek\", seconds: value });");
            StringAssert.Contains(source, "post(\"action\", { action: \"navigate\", url: value });");
        }

        [TestMethod]
        public void Host_ContextRejectsInvalidSeekAndNavigateArguments()
        {
            var source = HostSource();
            StringAssert.Contains(source, "if (!isFinite(value) || value < 0) {");
            StringAssert.Contains(
                source,
                "if (value.indexOf(\"https://\") !== 0 && value.indexOf(\"http://\") !== 0) {");
        }

        [TestMethod]
        public void Host_RejectsTypeScriptWithExplicitMessageUntilTranspilerLands()
        {
            // 阶段 1 未接入 tsc。TS 必须显式报错，不能静默降级成 JS 执行。
            var source = HostSource();
            StringAssert.Contains(source, "暂不支持的语言");
            StringAssert.Contains(source, "TS 转译尚未接入");
        }

        [TestMethod]
        public void Host_DoesNotDependOnBasAssets()
        {
            // 两条链路必须独立：宿主不得引用 BAS 的 shim / 渲染器。
            var source = HostSource();
            Assert.IsFalse(
                source.Contains("bas-jquery-shim.js"),
                "脚本弹幕宿主不应引用 BAS 的 jquery shim");
            Assert.IsFalse(
                source.Contains("window.BasDanmaku"),
                "脚本弹幕宿主不应引用 BAS 渲染器");
        }

        [TestMethod]
        public void Host_DoesNotCreateDomElementPerItem()
        {
            // 硬约束：渲染必须走统一画布，禁止每条弹幕一个 DOM 元素。
            var source = HostSource();
            Assert.IsFalse(
                source.Contains("createElement(\"div\")"),
                "脚本弹幕不得为每条弹幕创建 DOM 元素");
            Assert.IsFalse(
                source.Contains("appendChild(item"),
                "脚本弹幕不得把条目挂到 DOM 上");

            // 元素树是 JS 对象树，唯一挂到 DOM 的是主画布。
            StringAssert.Contains(source, "container.appendChild(canvas);");
        }

        [TestMethod]
        public void Host_ScalesCanvasByDevicePixelRatio()
        {
            // 不按 dpr 放大画布会在高 DPI 屏上糊。
            var source = HostSource();
            StringAssert.Contains(source, "var ratio = window.devicePixelRatio || 1;");
            StringAssert.Contains(
                source,
                "canvas.width = Math.max(1, Math.round(width * ratio));");
            StringAssert.Contains(
                source,
                "context2d.setTransform(ratio, 0, 0, ratio, 0, 0);");
        }

        [TestMethod]
        public void Host_CapturesGenerationOncePerBatch()
        {
            // generation 必须在批次入口取一次再下传；
            // 若在 addItem 内部取，「reset 后丢弃剩余条目」的检查恒为假，形同虚设。
            var source = HostSource();
            StringAssert.Contains(source, "var itemGeneration = generation;");
            StringAssert.Contains(source, "addItem(list[index], itemGeneration);");
            StringAssert.Contains(source, "function addItem(model, itemGeneration) {");

            // 分块路径（beginItem → appendItemChunk* → endItem）同样受保护。
            var endItemBody = TestRepository.MethodBody(source, "endItem: function () {");
            StringAssert.Contains(endItemBody, "addItem(model, generation);");
        }

        [TestMethod]
        public void Host_ReassemblesChunkedItemPayload()
        {
            // 大脚本走分块传输，宿主必须逐块拼接后整体解析。
            var source = HostSource();
            var beginBody = TestRepository.MethodBody(source, "beginItem: function () {");
            StringAssert.Contains(beginBody, "pendingItemJson = \"\";");

            var chunkBody = TestRepository.MethodBody(source, "appendItemChunk: function (chunk) {");
            StringAssert.Contains(chunkBody, "pendingItemJson += chunk");

            var endBody = TestRepository.MethodBody(source, "endItem: function () {");
            StringAssert.Contains(endBody, "JSON.parse(json)");
            StringAssert.Contains(endBody, "reportCompileError(error, \"\");");
        }

        [TestMethod]
        public void Host_BootstrapsWithReadyHandshakeAndResizeBinding()
        {
            // 控件靠 ready 判定宿主可用，靠 resize 事件跟随画布尺寸。
            var source = HostSource();
            StringAssert.Contains(
                source,
                "window.addEventListener(\"resize\", window.scriptDanmakuHost.resize);");
            StringAssert.Contains(source, "ensureCanvas();");
            StringAssert.Contains(source, "post(\"ready\");");
        }

        [TestMethod]
        public void Control_KeepsWebViewLazyUntilContentExists()
        {
            var source = TestRepository.ReadFile(ControlPath);
            StringAssert.Contains(source, "private bool HasContentOrVisibility()");
            StringAssert.Contains(source, "|| Volatile.Read(ref pendingItemCount) > 0;");
        }

        [TestMethod]
        public void Control_RequiresNoHitTestSoPlayerGesturesKeepWorking()
        {
            // 阶段 1 不拦截输入，控件不得抢占命中测试。
            StringAssert.Contains(
                TestRepository.ReadFile("BiliBili.UWP/Controls/ScriptDanmakuControl.xaml"),
                "IsHitTestVisible=\"False\"");
        }

        [TestMethod]
        public void Control_UsesItsOwnVirtualHostPage()
        {
            var source = TestRepository.ReadFile(ControlPath);
            StringAssert.Contains(
                source,
                "https://biliuwp.local/script-danmaku-host.html");
        }

        [TestMethod]
        public void BuiltInDemosAreWrittenInRetainedMode()
        {
            // 内置示例是「保留模式怎么写」的样板，不能退回每帧重算坐标。
            var source = TestRepository.ReadFile("BiliBili.UWP/Helper/ScriptDanmakuService.cs");
            StringAssert.Contains(source, "ctx.createText(");
            StringAssert.Contains(source, "ctx.createShape()");
            StringAssert.Contains(source, "ctx.tween(");
            Assert.IsFalse(
                source.Contains("ctx.progress"),
                "保留模式示例不得再用 ctx.progress 逐帧重算坐标");
            Assert.IsFalse(
                source.Contains("ctx.g."),
                "保留模式示例不得再直接操作画布上下文");
        }
    }
}
