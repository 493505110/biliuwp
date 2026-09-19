using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    /// <summary>
    /// 脚本弹幕宿主的源码契约。宿主是纯 JS 资产，无法在本测试项目中执行，
    /// 因此这里断言「跨进程双方共同依赖的字符串约定」，避免任一侧被单方面改动。
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

        [TestMethod]
        public void Host_ExposesDocumentedContextFields()
        {
            var source = HostSource();

            // 文档 §4 约定的脚本上下文。改动这里等于破坏脚本 API 契约。
            StringAssert.Contains(source, "g: context2d");
            foreach (var field in new[]
            {
                "width: viewportWidth",
                "height: viewportHeight",
                "dpr: devicePixelRatioValue",
                "t: elapsed",
                "duration: duration",
                "progress: elapsed / duration",
                "stime: item.model.stime",
                "id: item.model.id",
                "pause: requestPause",
                "seek: requestSeek",
                "navigate: requestNavigate"
            })
            {
                StringAssert.Contains(source, field, field);
            }
        }

        [TestMethod]
        public void Host_ContextProgressStaysWithinUnitRange()
        {
            // progress 越界会让脚本的插值算出画布外的坐标。
            StringAssert.Contains(
                HostSource(),
                "var elapsed = Math.max(0, Math.min(duration, now - item.startMs));");
        }

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
        public void Host_DrawFrameHonoursVisibility()
        {
            // setState / resize 也会进 drawFrame。若不拦隐藏状态，
            // 关闭弹幕总开关后紧跟的 setState 会把脚本重新画回屏幕上。
            var source = HostSource();
            StringAssert.Contains(source, "if (!visible) {");
            StringAssert.Contains(source, "// 隐藏状态下保持画布为空");
        }

        [TestMethod]
        public void Host_SelfStopsInsideTheFrameCallback()
        {
            // 自停若放在 drawFrame 内部、frame() 又无条件续帧，
            // 恢复播放时会与已排队的回调形成两条并行帧链（每帧画两次）。
            var source = HostSource();
            StringAssert.Contains(source, "var anyActive = drawFrame(now);");
            StringAssert.Contains(source, "running = false;");
            StringAssert.Contains(source, "frameHandle = 0;");
            Assert.IsFalse(
                source.Contains("if (!anyActive && !state.playing) {\n                    running = false;\n                }"),
                "自停不应留在 drawFrame 内部");
        }

        [TestMethod]
        public void Host_SavesAndRestoresCanvasStateAroundEachScript()
        {
            var source = HostSource();
            StringAssert.Contains(source, "context2d.save();");
            StringAssert.Contains(source, "context2d.restore();");
        }

        [TestMethod]
        public void Host_IsolatesScriptFailuresFromTheRenderLoop()
        {
            // 单个脚本抛错不能中断整帧。
            var source = HostSource();
            StringAssert.Contains(source, "reportRuntimeError(error, item.model.id);");
            StringAssert.Contains(source, "item.compileFailed = true;");
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
    }
}
