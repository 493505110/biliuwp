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

        /// <summary>
        /// 脚本注入的作用域名单（M8 全局名）。宿主用 new Function(name…, code)
        /// 把这些名字作为参数注入，脚本正文因此可以直接写 $ / Player / Tween…，
        /// 不需要（也不存在）自研的 ctx。
        /// </summary>
        private static readonly string[] ScriptGlobalNames =
        {
            "\"$\"",
            "\"Player\"",
            "\"$G\"",
            "\"Global\"",
            "\"Tween\"",
            "\"Utils\"",
            "\"ScriptManager\"",
            "\"timer\"",
            "\"interval\"",
            "\"clearTimer\"",
            "\"trace\"",
            "\"tracex\"",
            "\"stopExecution\"",
            "\"foreach\"",
            "\"clone\"",
            "\"getTimer\""
        };

        /// <summary>取脚本作用域构造函数（createScriptArgs）的函数体。</summary>
        private static string ScriptScopeBody()
        {
            return TestRepository.MethodBody(HostSource(), "function createScriptScope(item) {");
        }

        /// <summary>取 M8 元件工厂对象（$）的定义体。</summary>
        private static string DisplayFactoryBody()
        {
            return TestRepository.MethodBody(HostSource(), "var M8Display = {");
        }

        /// <summary>取 createItemElement 的函数体（元素归属与创建参数的落点）。</summary>
        private static string CreateItemElementBody()
        {
            return TestRepository.MethodBody(HostSource(), "function createItemElement(factory, options) {");
        }

        /// <summary>取 applyCreateOptions 的函数体（M8 创建参数的落点）。</summary>
        private static string CreateOptionsBody()
        {
            return TestRepository.MethodBody(HostSource(), "function applyCreateOptions(element, options) {");
        }

        /// <summary>取 tick 的函数体，用于断言每帧流程与可见性拦截。</summary>
        private static string TickBody()
        {
            return TestRepository.MethodBody(HostSource(), "function tick(now) {");
        }

        /// <summary>
        /// 取「带脏矩形擦除」的那版 paintDirtyElements 函数体。
        /// 文件里另有一处同名函数（无擦除的早期截面），只靠函数签名会取到前者，
        /// 因此把锚点前移到它独有的 composeElement 定义之后（composeElement 以
        /// recordElementRect 收尾，遮罩的裁剪包裹也在其中）。
        /// </summary>
        private static string PaintBody()
        {
            return TestRepository.MethodBody(
                HostSource(),
                "recordElementRect(element);\n            }\n\n            function paintDirtyElements() {");
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
                "resize: function",
                // C# → 宿主的弹幕数据链与输入链
                "resetComments: function",
                "appendComments: function",
                "pushComment: function",
                "pushKey: function"
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
            // play 是本轮补的：脚本可以恢复播放（此前是唯一被主动放弃的接口）。
            StringAssert.Contains(source, "action: \"play\"");
        }

        [TestMethod]
        public void Host_M8PlayerPlayPauseSeekJumpAllGoThroughTheActionBridge()
        {
            // 四个动作必须都走 action 通道，且 Player.* 的入参单位按 M8：
            // seek 收毫秒、jump 收 av 号 + 分P。
            var source = HostSource();
            var body = TestRepository.MethodBody(source, "var Player = {");

            StringAssert.Contains(body, "requestPlay();");
            StringAssert.Contains(body, "requestPause();");
            StringAssert.Contains(body, "return requestSeek(Math.max(0, offsetMs) / 1000);");
            StringAssert.Contains(body, "return requestNavigate(");

            StringAssert.Contains(source, "function requestPlay() {");
            StringAssert.Contains(source, "post(\"action\", { action: \"play\" });");

            // jump 的 av 号只接受 "av123" / "123"，且拼成 bilibili 视频页
            // （PlayerPage 侧的白名单只放行 bilibili.com 的 https）。
            StringAssert.Contains(source, "var AV_NUMBER_PATTERN = /^(?:av)?(\\d+)$/i;");
            StringAssert.Contains(
                source,
                "\"https://www.bilibili.com/video/av\" + match[1] + \"/?p=\" + pageNumber");
        }

        [TestMethod]
        public void Host_ExposesTheDanmakuDataChain()
        {
            var source = HostSource();

            // Player.commentList 是**推入的快照**，不是写死的空数组。
            StringAssert.Contains(source, "var commentSnapshot = [];");
            StringAssert.Contains(source, "resetComments: function () {");
            StringAssert.Contains(source, "commentSnapshot = [];");
            StringAssert.Contains(source, "commentSnapshot.push(normalizeComment(list[index]));");
            StringAssert.Contains(source, "return commentSnapshot;");

            // 快照字段与 M8 的 CommentData 同名同义，缺省要补齐（脚本读到的形状必须完整）。
            var normalizeBody = TestRepository.MethodBody(source, "function normalizeComment(raw) {");
            foreach (var field in new[]
            {
                "txt:",
                "time:",
                "color: normalizeColor(source.color),",
                "pool:",
                "mode:",
                "fontSize:"
            })
            {
                StringAssert.Contains(normalizeBody, field, field);
            }

            // 触发器：登记在条目上、按条目已播放时间计时、消息驱动投递。
            StringAssert.Contains(source, "function registerItemTrigger(kind, callback, timeoutMs, up) {");
            StringAssert.Contains(source, "item.triggers.push(trigger);");
            StringAssert.Contains(source, "function liveTriggers(item, kind, keyUp) {");
            StringAssert.Contains(source, "function deliverCommentTrigger(comment) {");
            StringAssert.Contains(source, "function deliverKeyTrigger(keyCode, keyUp) {");
            StringAssert.Contains(source, "var DEFAULT_TRIGGER_TIMEOUT_MS = 1000;");

            // Player 上的三个入口接到实现上，不再是占位。
            var playerBody = TestRepository.MethodBody(source, "var Player = {");
            StringAssert.Contains(playerBody, "return registerItemTrigger(\"comment\", f, timeout, false);");
            StringAssert.Contains(playerBody, "return registerItemTrigger(\"key\", f, timeout, up);");

            // 条目回收 / reset 时触发器必须一起清（与定时器同一条生命周期）。
            var deactivateBody = TestRepository.MethodBody(source, "function deactivateItem(item) {");
            StringAssert.Contains(deactivateBody, "clearItemTriggers(item);");
            var clearAllBody = TestRepository.MethodBody(source, "function clearAllItems() {");
            StringAssert.Contains(clearAllBody, "clearItemTriggers(item);");
        }

        [TestMethod]
        public void Host_KeepsKeyTriggerToTheM8KeySet()
        {
            // M8 文档明确只监听数字键盘 0-9、方向键、Home/End/PgUp/PgDn、W/S/A/D。
            // 这组键与 Windows VirtualKey / DOM keyCode 同值，因此 C# 侧可整数值透传。
            var source = HostSource();
            StringAssert.Contains(source, "var M8_TRIGGER_KEY_CODES = {");
            var body = TestRepository.MethodBody(source, "function deliverKeyTrigger(keyCode, keyUp) {");
            StringAssert.Contains(body, "if (!M8_TRIGGER_KEY_CODES[keyCode]) {");
            StringAssert.Contains(body, "return;");

            // 键值必须覆盖 M8 文档列出的那组（左/上/右/下、Home、End、PgUp、PgDn、W、A、S、D）。
            var keyBody = TestRepository.MethodBody(source, "var M8_TRIGGER_KEY_CODES = {");
            foreach (var code in new[] { "33", "34", "35", "36", "37", "38", "39", "40", "65", "68", "83", "87" })
            {
                StringAssert.Contains(keyBody, code + ": true", "缺少 M8 允许监听的键 " + code);
            }
        }

        [TestMethod]
        public void Host_ImplementsPlayerSetMaskAsCompositionClipping()
        {
            // setMask 走合成期裁剪：只影响画到主画布上的可见范围，
            // 元素自己的离屏缓存不受影响（不因遮罩而重建位图）。
            var source = HostSource();
            StringAssert.Contains(source, "function setStageMask(element) {");
            StringAssert.Contains(source, "function applyStageMask(target) {");
            StringAssert.Contains(source, "function traceElementClipPath(target, element, map) {");
            StringAssert.Contains(source, "var stageMaskElement = null;");

            var playerBody = TestRepository.MethodBody(source, "var Player = {");
            StringAssert.Contains(playerBody, "setMask: function (obj) {");
            StringAssert.Contains(playerBody, "setStageMask(obj);");

            // 裁剪必须在 composeElement（合成期）施加，且擦除 / 整屏清空不带裁剪。
            // 另外被当作遮罩的元件本身不参与合成（连呈现记录都不留）。
            var composeBody = TestRepository.MethodBody(source, "function composeElement(element) {");
            StringAssert.Contains(composeBody, "if (isUsedAsMask(element)) {");
            StringAssert.Contains(composeBody, "var clipped = applyStageMask(context2d);");
            StringAssert.Contains(composeBody, "if (clipped) {");
            StringAssert.Contains(composeBody, "context2d.restore();");
            var flushBody = TestRepository.MethodBody(source, "function flushEraseRects(rects) {");
            Assert.IsFalse(
                flushBody.Contains("applyStageMask"),
                "擦除不得带裁剪：要抹掉的正是上一帧的像素");
            var clearBody = TestRepository.MethodBody(source, "function clearSurface() {");
            Assert.IsFalse(
                clearBody.Contains("applyStageMask"),
                "整屏清空不得带裁剪，否则遮罩外的旧像素永远擦不掉");

            // reset 整批作废时遮罩也要摘掉，否则下一批弹幕会被上一批的遮罩裁掉。
            var clearAllBody = TestRepository.MethodBody(source, "function clearAllItems() {");
            StringAssert.Contains(clearAllBody, "stageMaskElement = null;");
        }

        // ---- 保留模式：脚本只执行一次 ----

        [TestMethod]
        public void Host_CompilesEachScriptExactlyOnceAtRegistration()
        {
            // 保留模式的根契约：整份宿主里只允许一处 new Function，
            // 且它在 compileItem 里被调用，编译结果缓存到 item.run。
            var source = HostSource();

            // 「只编译一次」的可观测落点：整份宿主只有一处构造脚本函数的地方，
            // 且它在 compileItem 里执行、结果缓存到 item.run。
            //
            // 整份宿主只有一处 `new Function(`，且它在 compileItem 里。
            var occurrences = 0;
            var index = source.IndexOf("new Function(", System.StringComparison.Ordinal);
            while (index >= 0)
            {
                occurrences++;
                index = source.IndexOf("new Function(", index + 1, System.StringComparison.Ordinal);
            }

            Assert.AreEqual(
                1,
                occurrences,
                "宿主只应在 compileItem 里编译脚本，逐帧路径不得再出现 new Function");
            StringAssert.Contains(source, "item.run = compileItem(model);");

            // 注入名清单只有一张表（SCRIPT_GLOBAL_NAMES），compileItem 的形参与
            // createScriptScope 的实参都以它为准，避免两边顺序漂移。
            StringAssert.Contains(source, "var SCRIPT_GLOBAL_NAMES = [");
            var scopeBody = TestRepository.MethodBody(
                source,
                "function createScriptScope(item) {");
            StringAssert.Contains(scopeBody, "// 顺序必须与 compileItem 的形参逐字对应。");

            // 注入的是 M8 的全局名，不是自研的 ctx。
            var compileBody = TestRepository.MethodBody(
                source,
                "function compileItem(model) {");
            foreach (var name in ScriptGlobalNames)
            {
                StringAssert.Contains(compileBody, name, "compileItem 必须注入 M8 全局名 " + name);
            }
        }

        [TestMethod]
        public void Host_ExposesFlashDisplayObjectSurface()
        {
            // 这一批是让 av2669196 的真实 M8 脚本「真的出画面」时逐个补出来的
            // Flash DisplayObject 能力，缺一个都会让那两条脚本停摆。
            var source = HostSource();

            // 1) element.transform：matrix 与已有的 props.matrix 是同一份对象
            //    （脚本会 `mx = el.transform.matrix; mx.identity();` 原地改）。
            StringAssert.Contains(source, "function createElementTransform(element) {");
            var transformBody = TestRepository.MethodBody(
                source,
                "function createElementTransform(element) {");
            StringAssert.Contains(transformBody, "if (!element.props.matrix) {");
            StringAssert.Contains(transformBody, "element.props.matrix = createPlaceholderMatrix();");
            StringAssert.Contains(transformBody, "getRelativeMatrix3D: function (target) {");
            StringAssert.Contains(transformBody, "matrix3D");
            StringAssert.Contains(transformBody, "colorTransform");

            // Matrix3D / Vector3D 是真算的（Akari 靠 transformVectors 做 3D 深度排序）。
            StringAssert.Contains(source, "function createMatrix3D(rawData) {");
            StringAssert.Contains(source, "function createVector3D(x, y, z) {");
            StringAssert.Contains(source, "matrix.transformVectors = function (source, target) {");
            StringAssert.Contains(source, "matrix.appendRotation = function (degrees, axis) {");
            StringAssert.Contains(source, "createVector3D: function (x, y, z) {");
            StringAssert.Contains(source, "createMatrix3D: function (rawData) {");

            // 2) 显示列表查询（Akari 的 clone 整棵克隆走 getChildAt/numChildren）。
            StringAssert.Contains(source, "function attachDisplayListApi(element) {");
            foreach (var member in new[]
            {
                "element.getChildAt = function (index) {",
                "element.getChildIndex = function (child) {",
                "element.setChildIndex = function (child, index) {",
                "element.getChildByName = function (name) {",
                "element.contains = function (child) {",
                "element.addChildAt = function (child, index) {",
                "element.removeChildAt = function (index) {"
            })
            {
                StringAssert.Contains(source, member, member);
            }

            StringAssert.Contains(source, "Object.defineProperty(element, \"numChildren\", {");

            // 3) blendMode：映射到 globalCompositeOperation，未知值退回 normal。
            StringAssert.Contains(source, "var BLEND_MODE_COMPOSITES = {");
            var blendBody = TestRepository.MethodBody(
                source,
                "function blendModeToComposite(value) {");
            StringAssert.Contains(blendBody, "return \"source-over\";");
            StringAssert.Contains(source, "add: \"lighter\",");
            StringAssert.Contains(source, "multiply: \"multiply\",");
            StringAssert.Contains(source, "function applyElementBlendMode(target, element) {");

            // 4) 元素级 mask：作用域是元素子树，不是整块画布（与 Player.setMask 区分开）。
            StringAssert.Contains(source, "function applyElementMaskClip(target, element) {");
            StringAssert.Contains(source, "function createMaskPointMapper(masker) {");
            StringAssert.Contains(source, "function retainMaskReference(maskElement) {");
            StringAssert.Contains(source, "element.maskUseCount");
            // 遮罩元件的变换烘进路径坐标：不能靠 canvas 的 save/restore 压变换
            // （restore 会把刚建立的裁剪一起去掉）。
            var maskBody = TestRepository.MethodBody(
                source,
                "function applyElementMaskClip(target, element) {");
            Assert.IsFalse(
                maskBody.Contains("target.save()"),
                "元素遮罩不能用 save/restore 压遮罩元件的变换（restore 会连带撤销裁剪）");
            StringAssert.Contains(maskBody, "traceElementClipPath(target, masker, map)");
        }

        [TestMethod]
        public void Host_HidesElementInternalsFromScriptEnumeration()
        {
            // 元素的内部字段必须不可枚举：Flash 的显示对象属性在原型上，
            // 脚本 `foreach(obj, fn)` / `for-in` 遍历显示对象时一个都拿不到。
            // Akari 的 Factory.clone 正是靠 `countProperties === 0` 分叉——
            // 可枚举的话它会顺着（成环的）对象图无限递归。
            var source = HostSource();
            StringAssert.Contains(source, "function hideElementInternals(element) {");
            StringAssert.Contains(source, "function defineHiddenValue(element, name, value) {");

            var hideBody = TestRepository.MethodBody(
                source,
                "function hideElementInternals(element) {");
            StringAssert.Contains(hideBody, "Object.getOwnPropertyNames(element)");
            StringAssert.Contains(hideBody, "descriptor.enumerable = false;");

            // 构造期之后新增的字段也要藏（工厂收尾与创建参数收尾各一次）。
            StringAssert.Contains(source, "hideElementInternals(element);\n                return element;");
            var optionsBody = TestRepository.MethodBody(
                source,
                "function applyCreateOptions(element, options) {");
            StringAssert.Contains(optionsBody, "hideElementInternals(element);");

            // hasOwnProperty 不受影响：脚本用它判断「是不是显示对象」——
            // 不可枚举只影响 for-in / Object.keys，hasOwnProperty 照旧为真。
            StringAssert.Contains(source, "Object.prototype.hasOwnProperty.call(loop, key)");
        }

        [TestMethod]
        public void Host_KeepsPopElAndElementEventsFaithful()
        {
            // ScriptManager.popEl 的 M8 语义是「从自动清理表里弹出」，不是从
            // 显示列表摘除——Akari 把整幅作品挂在 popEl 过的常驻 root 下，
            // 实现成 remove() 会让整棵树脱离渲染（实测一个像素都画不出来）。
            var source = HostSource();
            StringAssert.Contains(source, "defineHiddenValue(element, \"exemptFromClear\", true);");
            var clearBody = TestRepository.MethodBody(source, "function clearItemElements(item) {");
            StringAssert.Contains(clearBody, "if (item.elements[index].exemptFromClear) {");
            StringAssert.Contains(clearBody, "continue;");

            // Flash 的 Event.ENTER_FRAME：Akari 的整幅画面更新挂在这上面。
            StringAssert.Contains(source, "function registerItemFrameListener(element, listener) {");
            StringAssert.Contains(source, "function dispatchItemEnterFrame(item) {");
            StringAssert.Contains(source, "element.addEventListener = function (type, listener) {");
            StringAssert.Contains(source, "element.removeEventListener = function (type, listener) {");
            StringAssert.Contains(source, "if (type === \"enterFrame\") {");
            // 监听表随条目回收一起清掉。
            var deactivateBody = TestRepository.MethodBody(source, "function deactivateItem(item) {");
            StringAssert.Contains(deactivateBody, "item.frameListeners = [];");
            // 派发必须早于补间推进 / 脏元素重绘，否则本帧画的是旧状态。
            var advanceBody = TestRepository.MethodBody(source, "function advanceItem(item, now) {");
            var dispatchIndex = advanceBody.IndexOf("dispatchItemEnterFrame(item);", System.StringComparison.Ordinal);
            var handlesIndex = advanceBody.IndexOf("advanceItemHandles(item, delta);", System.StringComparison.Ordinal);
            Assert.IsTrue(
                dispatchIndex >= 0 && handlesIndex > dispatchIndex,
                "enterFrame 派发应早于补间推进");
        }

        [TestMethod]
        public void Host_ComputesGradientsAndDrawPathInsteadOfThrowing()
        {
            // entry_10 真的会调 beginGradientFill 与 drawPath（此前是显式抛错）。
            var source = HostSource();
            StringAssert.Contains(source, "beginGradientFill: function (type, colors, alphas, ratios, matrix) {");
            StringAssert.Contains(source, "lineGradientStyle: function (type, colors, alphas, ratios, matrix) {");
            StringAssert.Contains(source, "function createCanvasGradient(target, gradient) {");
            StringAssert.Contains(source, "target.createLinearGradient(");
            StringAssert.Contains(source, "target.createRadialGradient(");

            // drawPath 复用 moveTo/lineTo/curveTo 的路径模型，所以描边/填充/包围盒
            // 以及元素级遮罩的路径描摹都自动生效。
            var drawPathBody = TestRepository.MethodBody(source, "drawPath: function (commands, data) {");
            StringAssert.Contains(drawPathBody, "graphics.moveTo(");
            StringAssert.Contains(drawPathBody, "graphics.lineTo(");
            StringAssert.Contains(drawPathBody, "graphics.curveTo(");
            // 没有前置 MOVE_TO 时，Flash 把首个 LINE_TO 当起点。
            StringAssert.Contains(drawPathBody, "if (!graphics.__path || graphics.__path.kind !== \"poly\") {");

            // drawGraphicsData 仍然显式报错（两条真实脚本 0 次使用）。
            StringAssert.Contains(source, "drawGraphicsData 尚未支持");
        }

        [TestMethod]
        public void Host_BindsUndeclaredIdentifiersToUndefinedForAvm1Scripts()
        {
            // M8 脚本是 AS2 时代写给 AVM1 的：读未声明变量得到 undefined、不抛错。
            // 真实样本 entry_10 的 `update:function(time){if(time < startTime)...}`
            // 里 startTime / duration 就是翻译时丢掉的 var——JS 下是 ReferenceError，
            // 整条脚本停摆。宿主按需把这些**确实不存在**的标识符绑定成 undefined 重跑。
            var source = HostSource();
            StringAssert.Contains(source, "function runItemScriptWithAvm1Scope(item) {");
            StringAssert.Contains(source, "function readUndeclaredIdentifier(error) {");
            StringAssert.Contains(source, "function declareAvm1GlobalIfUndeclared(error) {");
            StringAssert.Contains(source, "var MAX_AVM1_SCOPE_RETRIES = 8;");
            StringAssert.Contains(source, "if (!error || !(error instanceof ReferenceError)) {");

            // 修在**全局对象**上而不是补脚本形参：抛错的闭包可能不是本条脚本创建的
            // （entry_10 抛错的那处 update 定义在 entry_08 的 Akari 库里，
            // 它的作用域链在 entry_08 执行时就定死了）。标识符解析对未绑定名是
            // 每次访问都回落全局对象查，所以补在全局对象上连已建好的闭包也一起救。
            StringAssert.Contains(source, "window[missingName] = undefined;");
            StringAssert.Contains(source, "Object.prototype.hasOwnProperty.call(window, missingName)");

            // 释放上一次跑出来的元件/定时器/触发器，避免重跑出现两份。
            StringAssert.Contains(source, "function releaseItemForRerun(item) {");
            var rerunBody = TestRepository.MethodBody(source, "function releaseItemForRerun(item) {");
            StringAssert.Contains(rerunBody, "clearItemTimers(item);");
            StringAssert.Contains(rerunBody, "releaseItemElement(item, index);");

            // 只声明「已证明不存在」的名字：已注入的 M8 名一律不放行
            // （它们抛 ReferenceError 说明是别的原因，补绑也救不了）。
            StringAssert.Contains(
                HostSource(),
                "if (missingName === null || INJECTED_SCRIPT_NAMES[missingName]) {");

            // 回调（定时器 / 触发器 / enterFrame）里抛的同类错误也要救：
            // entry_10 的 startTime 就是在 interval 回调里读的，只救正文没用。
            StringAssert.Contains(
                HostSource(),
                "if (!recoverItemFromCallbackError(item, error)) {");
            var recoverBody = TestRepository.MethodBody(
                HostSource(),
                "function recoverItemFromCallbackError(item, error) {");
            StringAssert.Contains(recoverBody, "releaseItemForRerun(item);");
            StringAssert.Contains(recoverBody, "runItemScriptWithAvm1Scope(item);");
        }

        [TestMethod]
        public void Host_DoesNotInjectCtxIntoScripts()
        {
            // 本版的决定：放弃自研的 ctx 脚本 API 面，脚本环境直接提供 M8 的
            // 全局名（$ / Player / $G / Global / Tween / Utils / ScriptManager /
            // timer / interval / clearTimer / trace / tracex / stopExecution /
            // foreach / clone / getTimer）。脚本正文里的 ctx.xxx 不再有任何意义。
            var source = HostSource();
            Assert.IsFalse(
                source.Contains("\"ctx\""),
                "宿主不得再把 ctx 作为注入名（脚本环境只有 M8 全局名）");
            Assert.IsFalse(
                source.Contains("ctx."),
                "宿主源码与内置示例都不应再出现 ctx. 调用");
            StringAssert.Contains(source, "new Function(");
            StringAssert.Contains(source, "\"$\", \"Player\", \"$G\", \"Global\", \"Tween\", \"Utils\", \"ScriptManager\",");
        }

        [TestMethod]
        public void Host_RunsScriptBodiesOnlyFromActivateItem()
        {
            // 脚本体的唯一执行点是 activateItem；它又只被 updateItems 里
            // 「首次进入时间窗」的分支调用，因此同一播放过程里不会重跑。
            var source = HostSource();
            // 脚本作用域按 M8 全局名的顺序建出来，再 apply 给编译好的函数。
            StringAssert.Contains(source, "item.run.apply(null, scriptArgs);");
            StringAssert.Contains(source, "var scriptArgs = createScriptScope(item);");

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
        public void Host_ScriptScopeHasNoTimeSnapshot()
        {
            // 保留模式下脚本执行期间没有任何时间推进：作用域里不得再塞入
            // 「激活时刻的时间快照」这类量。要读时间只有一个入口——
            // Player.time（getter，实时读宿主外推时钟）。
            var body = ScriptScopeBody();
            Assert.IsFalse(
                body.Contains("time:"),
                "脚本作用域不得再注入时间快照（时间只能通过 Player.time 实时读）");
            Assert.IsFalse(
                body.Contains("progress"),
                "progress 之类的逐帧量已废弃，不得出现在脚本作用域里");
        }

        [TestMethod]
        public void Host_DoesNotExposeImmediateModeCanvas()
        {
            // 立即模式的画布上下文入口已取消：脚本只能建保留元件，
            // 不能直接拿到主画布逐帧画（那会退回立即模式）。
            var source = HostSource();
            Assert.IsFalse(
                source.Contains("g: context2d"),
                "脚本作用域不得暴露 2D 上下文");
            StringAssert.Contains(
                source,
                "window.scriptDanmakuHost = {",
                "主画布只应通过宿主命令对象暴露给控件，不进脚本作用域");
        }

        // ---- 保留模式的元素与 tween API ----

        [TestMethod]
        public void Host_ExposesM8PlayerSurface()
        {
            var source = HostSource();
            var body = TestRepository.MethodBody(source, "var Player = {");

            // Player 的方法面（M8 文档 §Player）。
            foreach (var member in new[]
            {
                "play: function",
                "pause: function",
                "seek: function",
                "jump: function",
                "createSound: function",
                "setMask: function",
                "commentTrigger: function",
                "keyTrigger: function"
            })
            {
                StringAssert.Contains(body, member, member);
            }

            // 只读量必须是**实时 getter**：Player.time 要在 interval 回调里
            // 读到当前播放头，激活时的快照会算错（脚本就是这么用的）。
            foreach (var getter in new[]
            {
                "Object.defineProperty(Player, \"time\", {",
                "Object.defineProperty(Player, \"state\", {",
                "Object.defineProperty(Player, \"width\", {",
                "Object.defineProperty(Player, \"height\", {",
                "Object.defineProperty(Player, \"videoWidth\", {",
                "Object.defineProperty(Player, \"videoHeight\", {",
                "Object.defineProperty(Player, \"refreshRate\", {",
                "Object.defineProperty(Player, \"commentList\", {"
            })
            {
                StringAssert.Contains(source, getter, getter);
            }

            // time 取宿主的外推时钟（毫秒），state 取 playing / pause / stop。
            StringAssert.Contains(source, "return currentPositionMs();");
            StringAssert.Contains(source, "return currentPlayerState();");

            // refreshRate 的取值区间（M8 文档：10-500，默认 170）。
            StringAssert.Contains(source, "var DEFAULT_REFRESH_RATE = 170;");
            StringAssert.Contains(source, "var MIN_REFRESH_RATE = 10;");
            StringAssert.Contains(source, "var MAX_REFRESH_RATE = 500;");

            // Player.seek 的入参是毫秒（M8 文档），转发给宿主动作通道时换成秒。
            StringAssert.Contains(body, "return requestSeek(Math.max(0, offsetMs) / 1000);");
            StringAssert.Contains(body, "requestPause();");
        }

        [TestMethod]
        public void Host_ExposesM8ElementFactories()
        {
            var body = DisplayFactoryBody();
            foreach (var factory in new[]
            {
                "createComment: function",
                "createText: function",
                "createShape: function",
                "createCanvas: function",
                "createButton: function",
                "createImage: function",
                "toIntVector: function",
                "toNumberVector: function"
            })
            {
                StringAssert.Contains(body, factory, factory);
            }

            // 创建参数（M8 把坐标 / 变换 / 寿命 / 父元件 / motion 都写在
            // options 上）统一由 applyCreateOptions 处理。
            var optionsBody = CreateOptionsBody();
            foreach (var option in new[]
            {
                "normalizeStyle(source)",
                "readDeclaredSeconds(source, \"lifeTime\")",
                "applyElementLifeTime(",
                "addChildToParent(element, source.parent)",
                "normalizeMotionConfig(source.motion)"
            })
            {
                StringAssert.Contains(optionsBody, option, option);
            }

            // 声明式 motion 必须走 elapsed 驱动的声明式补间，不能走 Tween.* 句柄
            // （句柄有自己的时间轴，seek 回窗口内重建时会从 0 重新开始）。
            StringAssert.Contains(optionsBody, "createTween(element, motionConfig, source);");

            // 元素创建必须**先注册再套创建参数**：registerItemElement 会把
            // declaredLifeTimeMs 重置成「未声明」，反了会让寿命声明失效；
            // 且声明式 motion 需要 element.ownerItem 才能挂到条目上。
            var createBody = CreateItemElementBody();
            Assert.IsTrue(
                createBody.IndexOf("registerItemElement(item, factory(item))", System.StringComparison.Ordinal)
                    < createBody.IndexOf("applyCreateOptions(element, options);", System.StringComparison.Ordinal),
                "createItemElement 必须先 registerItemElement 再 applyCreateOptions");
        }

        [TestMethod]
        public void Host_KeepsTimersScopedToTheItem()
        {
            // timer / interval 必须登记在条目上，条目回收 / reset / seek 越窗时
            // 统一清掉。缺了这条，定时器会在条目销毁后继续跑——这正是 M8 用
            // ScriptManager.clearTimer() 解决的问题。
            var source = HostSource();
            StringAssert.Contains(source, "function scheduleItemTimer(closure, delayMs, oneShot, times) {");
            StringAssert.Contains(source, "item.scheduledTimers.push(timer);");
            StringAssert.Contains(source, "function clearItemScheduledTimers(item) {");
            StringAssert.Contains(source, "function runItemTimers(item, deltaMs) {");

            // 条目停用（窗口结束 / reset）必须清定时器。
            var deactivateBody = TestRepository.MethodBody(
                source,
                "function deactivateItem(item) {");
            StringAssert.Contains(deactivateBody, "clearItemTimers(item);");

            // 整批作废（reset）同样要清。
            var clearAllBody = TestRepository.MethodBody(source, "function clearAllItems() {");
            StringAssert.Contains(clearAllBody, "clearItemTimers(item);");

            // 还有定时器在跑时不得自停，否则暂停后定时器会被「冻住」。
            var pendingBody = TestRepository.MethodBody(source, "function hasPendingAnimation(now) {");
            StringAssert.Contains(pendingBody, "if (item.scheduledTimers.length > 0) {");

            // ScriptManager 的三个方法都要在（clearTimer 是 M8 脚本依赖的收尾动作）。
            var managerBody = TestRepository.MethodBody(source, "var ScriptManager = {");
            StringAssert.Contains(managerBody, "clearTimer: function () {");
            StringAssert.Contains(managerBody, "clearEl: function () {");
            StringAssert.Contains(managerBody, "clearTrigger: function () {");
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
            // 自停判据是「不在播放且没有待推进的补间/逐帧回调」：只看
            // 「窗口内有没有条目」的话，无界窗口（duration 缺省）下暂停后会一直空转。
            var source = HostSource();
            StringAssert.Contains(source, "function hasPendingAnimation(now) {");
            StringAssert.Contains(source, "running = false;");
            StringAssert.Contains(source, "frameHandle = 0;");

            var frameBody = TestRepository.MethodBody(source, "function frame() {");
            Assert.IsTrue(
                frameBody.Contains("if (!state.playing && !hasPendingAnimation(now) && !dirty) {"),
                "自停条件必须写在 frame() 内部");

            var stopIndex = frameBody.IndexOf("if (!state.playing && !hasPendingAnimation(now) && !dirty) {", System.StringComparison.Ordinal);
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

            // 整屏 clearSurface 只允许出现在「整幅画面作废」的四条路径上：
            // tick 的隐藏分支、stopRunning、reset 整批替换条目时（被丢弃的元素
            // 不会再进擦除队列，必须靠 reset 自己清屏），以及换遮罩时
            // （可见区域变了，已画像素全部作废）。
            // 逐帧合成路径不得整屏清空。
            var callSites = new System.Collections.Generic.List<int>();
            var index = source.IndexOf("clearSurface();", System.StringComparison.Ordinal);
            while (index >= 0)
            {
                callSites.Add(index);
                index = source.IndexOf("clearSurface();", index + 1, System.StringComparison.Ordinal);
            }

            // 第四条路径是本轮新增的：换遮罩（Player.setMask）改变了可见区域，
            // 主画布上已有像素全部作废，必须整屏清掉再重合成。
            // 第五条路径是本轮新增的：最后一个条目离开时间窗时整幅作废
            // （元素级擦除盖不住脚本自己摘出去 / 重新挂载的像素，会留下残影）。
            Assert.AreEqual(
                5,
                callSites.Count,
                "clearSurface() 应只有 tick 隐藏分支、stopRunning、reset、setStageMask、updateItems 收口五处调用点");

            var resetStart = source.IndexOf("reset: function (", System.StringComparison.Ordinal);
            Assert.IsTrue(resetStart >= 0, "未找到 reset 命令");
            var resetClear = source.IndexOf("clearSurface();", resetStart, System.StringComparison.Ordinal);
            Assert.IsTrue(
                resetClear >= 0 && resetClear - resetStart < 900,
                "reset 必须清屏：整批条目作废后，被丢弃元素的像素不会再有擦除队列");

            var tickBody = TickBody();
            var tickStart = source.IndexOf("function tick(now) {", System.StringComparison.Ordinal);
            var stopBody = TestRepository.MethodBody(source, "function stopRunning() {");
            var stopStart = source.IndexOf("function stopRunning() {", System.StringComparison.Ordinal);
            var maskBody = TestRepository.MethodBody(source, "function setStageMask(element) {");
            var maskStart = source.IndexOf("function setStageMask(element) {", System.StringComparison.Ordinal);
            var itemsBody = TestRepository.MethodBody(source, "function updateItems(now) {");
            var itemsStart = source.IndexOf("function updateItems(now) {", System.StringComparison.Ordinal);
            Assert.IsTrue(itemsStart >= 0, "未找到 updateItems");
            foreach (var callSite in callSites)
            {
                Assert.IsTrue(
                    (callSite > tickStart && callSite < tickStart + tickBody.Length)
                        || (callSite > stopStart && callSite < stopStart + stopBody.Length)
                        || (callSite > resetStart && callSite < resetStart + 900)
                        || (callSite > maskStart && callSite < maskStart + maskBody.Length)
                        || (callSite > itemsStart && callSite < itemsStart + itemsBody.Length),
                    "clearSurface() 只允许出现在隐藏 / 停止 / reset / 换遮罩 / 条目收口五条整幅作废的路径上");
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

            // 切块不得把 UTF-16 代理对切成两半：B 站下发的代码弹幕正文
            // 可能有几十万字符（真实样本 354KB），必然走到分块路径，
            // 从代理对中间切开会在 JS 侧拼出半个字符、脚本正文直接损坏。
            var control = TestRepository.ReadFile(ControlPath);
            StringAssert.Contains(control, "private static int SafeChunkLength(string value, int offset)");
            StringAssert.Contains(control, "if (char.IsLowSurrogate(value[offset + count]))");
            // 步长必须用实际取到的 count：跳过那个被让出的字符同样会切坏正文。
            StringAssert.Contains(control, "offset += count;");
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
        public void Control_ReportsGlobalFailureOnlyForCompositeErrors()
        {
            // compile / runtime 是**那条脚本**的问题：宿主只标它 failed，其余条目
            // 照常推进。整个功能没坏，报「渲染失败」是误报——mode=8 下发模式实测
            // 就会收到服务端截断的脚本副本（av2669196 的分段包里有主脚本的前 300
            // 字符，必然编译失败），用户没做错任何事。
            var source = TestRepository.ReadFile(ControlPath);
            var handler = TestRepository.MethodBody(source, "private void HandleRendererError(JObject message)");

            StringAssert.Contains(handler, "if (string.Equals(stage, \"composite\", StringComparison.OrdinalIgnoreCase))");
            StringAssert.Contains(handler, "ShowRendererFailureOnce(\"脚本弹幕渲染失败\");");
            // 日志仍然全量记录（含 stage 与脚本 id），只是不再弹全局提示。
            StringAssert.Contains(handler, "LogHelper.WriteLog(logMessage, LogType.ERROR);");

            // 初始化失败与命令通道异常属于宿主自身的问题，提示必须保留。
            var initialize = TestRepository.MethodBody(source, "private async Task<bool> InitializeAsync()");
            StringAssert.Contains(initialize, "ShowRendererFailureOnce(\"脚本弹幕渲染器加载失败\");");
            var execute = TestRepository.MethodBody(source, "private async Task ExecuteCommandAsync(int version, Func<Task> command)");
            StringAssert.Contains(execute, "ShowRendererFailureOnce(\"脚本弹幕渲染失败\");");
        }

        // ---- 桥协议：宿主 ↔ 控件的动作与数据链（本轮新增）----

        [TestMethod]
        public void Control_ForwardsPlayActionToTheHostEvent()
        {
            // 宿主发 action: "play"，控件必须转成事件抛给 PlayerPage。
            var source = TestRepository.ReadFile(ControlPath);
            StringAssert.Contains(source, "case \"play\":");
            StringAssert.Contains(source, "ScriptDanmakuActionKind.Play");
            StringAssert.Contains(source, "Play,\n        Seek,");

            // 事件参数注释也要跟上（动作集合变了）。
            StringAssert.Contains(source, "暂停 / 播放 / 定位 / 导航");
        }

        [TestMethod]
        public void Control_PushesDanmakuSnapshotWithTheDocumentedCommands()
        {
            // C# → 宿主的弹幕数据链命令名必须与宿主逐一对应。
            var source = TestRepository.ReadFile(ControlPath);
            StringAssert.Contains(source, "public Task PushDanmakuBatchAsync(");
            StringAssert.Contains(source, "public Task PushSentCommentAsync(");
            StringAssert.Contains(source, "public Task PushKeyEventAsync(int keyCode, bool isKeyUp)");

            StringAssert.Contains(source, "window.scriptDanmakuHost.resetComments();");
            StringAssert.Contains(source, "window.scriptDanmakuHost.appendComments([");
            StringAssert.Contains(source, "window.scriptDanmakuHost.pushComment(");
            StringAssert.Contains(source, "window.scriptDanmakuHost.pushKey(");

            // 分块上限：按字节预算切分，单次载荷不能无限大（与 append 分块同一口径）。
            // 不按固定条数切：单条弹幕长度可以差一个量级。
            StringAssert.Contains(source, "private const string CommentBatchPrefix = \"window.scriptDanmakuHost.appendComments([\";");
            StringAssert.Contains(source, "builder.Length + json.Length + CommentBatchSuffix.Length > MaxChunkPayloadLength");

            // 快照要保留并在 reset 之后补投：脚本可能是后于弹幕池加载的。
            StringAssert.Contains(source, "private readonly List<ScriptDanmakuComment> danmakuSnapshot =");
            StringAssert.Contains(source, "await PushDanmakuSnapshotAsync(version);");

            // 池子没变时跳过重复投递（分页加载会反复走 SetDanmakuPool）。
            StringAssert.Contains(source, "ReferenceEquals(list, lastPushedComments)");
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
        public void BuiltInDemosAreWrittenAgainstTheM8Api()
        {
            // 内置示例是「M8 脚本怎么写」的样板：脚本只执行一次、动画交给
            // 声明式 motion 或 M8 的 interval 驱动，不能退回每帧重算坐标。
            var source = TestRepository.ReadFile("BiliBili.UWP/Helper/ScriptDanmakuService.cs");

            // 必须写在 M8 的 API 面上。
            StringAssert.Contains(source, "$.createComment(");
            StringAssert.Contains(source, "$.createShape(");
            StringAssert.Contains(source, "motion: {");
            StringAssert.Contains(source, "interval(function () {");
            StringAssert.Contains(source, "Player.time");
            StringAssert.Contains(source, "Player.width");
            StringAssert.Contains(source, "Player.height");

            // 不得再用自研 ctx / 立即模式 / scale 扩散（那会把点一起放大）。
            Assert.IsFalse(source.Contains("ctx."), "内置示例不得再用自研的 ctx API");
            Assert.IsFalse(source.Contains("ctx.onFrame"), "onFrame 逃生舱已取消，逐帧走 M8 的 interval");
            Assert.IsFalse(source.Contains("scaleX"), "示例不得用 scale 扩散：那会把点一起放大");
            Assert.IsFalse(
                source.Contains("ctx.progress"),
                "保留模式示例不得再用逐帧 progress 重算坐标");
        }

        [TestMethod]
        public void Host_TreatsMissingDurationAsUnboundedWindowWithCap()
        {
            // duration 缺省 / 非正数 = 不设时间窗（原版 M8 没有条目窗口，
            // 元素寿命由脚本的 lifeTime 决定）：宿主只保留一个防呆上限。
            var source = HostSource();
            StringAssert.Contains(source, "var MAX_ITEM_WINDOW_MS = 600000;");

            var addItemBody = TestRepository.MethodBody(
                source,
                "function addItem(model, itemGeneration) {");
            StringAssert.Contains(addItemBody, "durationSeconds = MAX_ITEM_WINDOW_MS / 1000;");
            StringAssert.Contains(addItemBody, "endMs: startMs + durationSeconds * 1000,");
        }

        [TestMethod]
        public void Host_TreatsZeroLifeTimeAsUnbounded()
        {
            // lifeTime 未声明 = 不动元素寿命（活到条目窗口兜底上限）；
            // 声明 0 / 负数 = 常驻（对齐 M8 的 lifeTime: 0）。
            var source = HostSource();
            StringAssert.Contains(source, "function readDeclaredSeconds(config, name) {");

            var tweenBody = TestRepository.MethodBody(
                source,
                "function createTween(element, config, options) {");
            StringAssert.Contains(tweenBody, "var unboundedLifeTime = declaredSeconds !== null && declaredSeconds <= 0;");
            StringAssert.Contains(
                tweenBody,
                "lifeTimeMs: unboundedLifeTime\n                        ? LIFE_TIME_UNBOUNDED");

            // 「没有声明」时绝不能把补间时长的缺省 3 秒当成寿命声明写进去：
            // applyElementLifeTime 取声明最大值，会把脚本真正声明的 lifeTime: 2
            // 顶成 3000ms（见 tests/host/retained-mode.test.js 的 D3 第二例）。
            StringAssert.Contains(
                tweenBody,
                "if (motion.lifeTimeMs !== null) {",
                "未声明寿命时不得调用 applyElementLifeTime");
        }
    }
}
