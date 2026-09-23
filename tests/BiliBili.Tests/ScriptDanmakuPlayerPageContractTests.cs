using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    /// <summary>
    /// 脚本弹幕在 PlayerPage 的接入契约。这些挂钩一旦漏掉不会编译失败、
    /// 也不会被解析层测试覆盖，只会在页面里表现为「时间轴慢慢漂移」这类难查的症状。
    /// </summary>
    [TestClass]
    public class ScriptDanmakuPlayerPageContractTests
    {
        private const string PlayerPagePath = "BiliBili.UWP/Pages/PlayerPage.xaml.cs";
        private const string PlayerPageXamlPath = "BiliBili.UWP/Pages/PlayerPage.xaml";

        private static string PlayerPageSource()
        {
            return TestRepository.ReadFile(PlayerPagePath);
        }

        private static string Body(string methodSignature)
        {
            return TestRepository.MethodBody(PlayerPageSource(), methodSignature);
        }

        [TestMethod]
        public void RateChange_ResyncsBothDanmakuClocks()
        {
            // 宿主时钟按 rate 外推。倍速变了若不重推，脚本时间轴会永久漂移：
            // 漂移判据用「媒体新倍速」算期望值，所以自愈路径不存在。
            var body = Body("private void slider_Rate_ValueChanged(");

            StringAssert.Contains(body, "SyncBasDanmakuPlaybackState();");
            StringAssert.Contains(body, "SyncScriptDanmakuPlaybackState();");
        }

        [TestMethod]
        public void DanmakuToggle_ResyncsScriptVisibilityAndPlaybackState()
        {
            var body = Body("private void MTC_OpenDanmaku(");

            StringAssert.Contains(body, "scriptDanmakuControl?.SetVisibleAsync(e)");
            StringAssert.Contains(body, "SyncScriptDanmakuPlaybackState();");
        }

        [TestMethod]
        public void PositionChanged_SyncsScriptPositionOnBothDispatchPaths()
        {
            var body = Body("private void PlaybackSession_PositionChanged(");

            // 已有线程与 Dispatcher 回投两条路径都要挂，漏一条会在特定线程下静默失效。
            var occurrences = body.Split(new[] { "SyncScriptDanmakuPosition();" },
                StringSplitOptions.None).Length - 1;
            Assert.AreEqual(
                2,
                occurrences,
                "PositionChanged 的两条分发路径都应调用 SyncScriptDanmakuPosition()");
        }

        [TestMethod]
        public void PlaybackStateChanged_SyncsScriptPlaybackState()
        {
            var body = Body("private async void PlaybackSession_PlaybackStateChanged(");

            StringAssert.Contains(body, "SyncScriptDanmakuPlaybackState();");
        }

        [TestMethod]
        public void EveryBasCleanupPointAlsoClearsScriptDanmaku()
        {
            // 换集 / 换播放源 / 换分P 三处，BAS 清了脚本也必须清，否则上一个视频的脚本会残留。
            // 按「紧邻」而不是「总数」校验：总数比对会被菜单项等无关调用点凑巧满足。
            const int Proximity = 120;
            var source = PlayerPageSource();
            var index = source.IndexOf("ClearBasDanmaku();", StringComparison.Ordinal);
            var checkedCount = 0;

            while (index >= 0)
            {
                var window = source.Substring(
                    index,
                    Math.Min(Proximity, source.Length - index));
                var line = source.Substring(0, index).Split('\n').Length;

                Assert.IsTrue(
                    window.Contains("ClearScriptDanmaku();"),
                    "PlayerPage.xaml.cs:" + line
                        + " 的 ClearBasDanmaku() 未并列 ClearScriptDanmaku()");

                checkedCount++;
                index = source.IndexOf(
                    "ClearBasDanmaku();",
                    index + 1,
                    StringComparison.Ordinal);
            }

            Assert.AreEqual(3, checkedCount, "清理点数量变化，请确认契约仍然成立");
        }

        [TestMethod]
        public void ScriptControlSitsAboveBasAndBelowInteractive()
        {
            var xaml = TestRepository.ReadFile(PlayerPageXamlPath);
            var bas = xaml.IndexOf("x:Name=\"basDanmakuControl\"", StringComparison.Ordinal);
            var script = xaml.IndexOf("x:Name=\"scriptDanmakuControl\"", StringComparison.Ordinal);
            var interactive = xaml.IndexOf("x:Name=\"interactiveDanmakuControl\"", StringComparison.Ordinal);

            Assert.IsTrue(bas >= 0, "未找到 basDanmakuControl");
            Assert.IsTrue(script >= 0, "未找到 scriptDanmakuControl");
            Assert.IsTrue(interactive >= 0, "未找到 interactiveDanmakuControl");
            Assert.IsTrue(bas < script, "脚本弹幕应叠在 BAS 之上");
            Assert.IsTrue(script < interactive, "脚本弹幕应位于互动弹幕之下");
        }

        [TestMethod]
        public void ScriptMenuItems_PointAtExistingHandlers()
        {
            var xaml = TestRepository.ReadFile(PlayerPageXamlPath);
            var source = PlayerPageSource();

            foreach (var handler in new[]
            {
                "menuitem_LoadScriptDanmaku_Click",
                "menuitem_LoadDemoScriptDanmaku_Click",
                "menuitem_ClearScriptDanmaku_Click"
            })
            {
                StringAssert.Contains(xaml, handler, handler + " 未在 XAML 中注册");
                StringAssert.Contains(source, "void " + handler + "(", handler + " 缺少代码后置实现");
            }
        }

        [TestMethod]
        public void ScriptPlayAction_ResumesPlayback()
        {
            var body = Body("private async void ScriptDanmakuControl_ActionRequested(");

            StringAssert.Contains(body, "case ScriptDanmakuActionKind.Pause:");
            StringAssert.Contains(body, "case ScriptDanmakuActionKind.Play:");
            StringAssert.Contains(body, "mediaPlayer?.Play();");
        }

        [TestMethod]
        public void DanmakuPool_IsPushedToTheScriptHost()
        {
            // 弹幕池落定时推快照（脚本侧 Player.commentList）；分页追加也走同一条路。
            var body = Body("private void SetDanmakuPool(");
            StringAssert.Contains(body, "SyncScriptDanmakuComments();");

            var syncBody = Body("private void SyncScriptDanmakuComments()");
            StringAssert.Contains(syncBody, "scriptDanmakuControl.PushDanmakuBatchAsync(comments);");

            // CommentData.mode 用的是 B 站的 mode 编号，不能直接拿 DanmakuLocation 的枚举序号。
            var modeBody = Body("private static int ToDanmakuMode(");
            StringAssert.Contains(modeBody, "return 5;");
            StringAssert.Contains(modeBody, "return 4;");
            StringAssert.Contains(modeBody, "return 6;");
            StringAssert.Contains(modeBody, "return 7;");
        }

        [TestMethod]
        public void SentDanmaku_IsForwardedToCommentTrigger()
        {
            // 发送成功回调里把这条弹幕转发给脚本的 Player.commentTrigger。
            var body = Body("private async void MTC_SendDanmakued(");

            StringAssert.Contains(body, "scriptDanmakuControl.PushSentCommentAsync(");
            // mode 直接用 SendDanmakuModel.location（它本身就是 1/4/5 这套编号）。
            StringAssert.Contains(body, "mode = item.location,");
        }

        [TestMethod]
        public void KeyDownAndKeyUp_BothFeedKeyTrigger()
        {
            // keyTrigger 的 up 参数要真的可用：KeyDown 与 KeyUp 都要转发，
            // 且两者与既有 KeyDown 生命周期对称（导航进入时挂、退出时摘）。
            var source = PlayerPageSource();
            StringAssert.Contains(source, "private void PlayerPage_KeyUp(");
            // 键值用 VirtualKey 的整数值透传（M8 允许的那组键与 DOM/Flash keyCode 同值）。
            StringAssert.Contains(source, "PushKeyEventAsync((int)args.VirtualKey, true);");
            StringAssert.Contains(source, "PushKeyEventAsync((int)args.VirtualKey, false);");

            var body = Body("private void PlayerPage_KeyDown(");
            StringAssert.Contains(body, "args.Handled = true;");

            // KeyUp 的订阅 / 退订点必须与既有 KeyDown **逐个对应**（数量相等）：
            // 少一处会出现「某段流程里只收 keyUp、不收 keyDown」的半截状态
            // ——发送弹幕对话框前后成对摘挂就是一处具体场景。
            var keyDownAdds = source.Split(new[] { "KeyDown += PlayerPage_KeyDown;" }, StringSplitOptions.None).Length - 1;
            var keyUpAdds = source.Split(new[] { "KeyUp += PlayerPage_KeyUp;" }, StringSplitOptions.None).Length - 1;
            var keyDownRemoves = source.Split(new[] { "KeyDown -= PlayerPage_KeyDown;" }, StringSplitOptions.None).Length - 1;
            var keyUpRemoves = source.Split(new[] { "KeyUp -= PlayerPage_KeyUp;" }, StringSplitOptions.None).Length - 1;

            Assert.IsTrue(keyDownAdds >= 2, "KeyDown 订阅点数量异常：" + keyDownAdds);
            Assert.AreEqual(keyDownAdds, keyUpAdds, "KeyUp 订阅点必须与 KeyDown 一一对应");
            Assert.AreEqual(keyDownRemoves, keyUpRemoves, "KeyUp 退订点必须与 KeyDown 一一对应");
        }

        [TestMethod]
        public void ScriptViewport_MatchesVideoRenderingRect()
        {
            // 作品自带的 Akari 库按「弹幕画布尺寸」等比缩放整个舞台并居中，
            // 所以画布必须等于视频实际渲染矩形，而不是整个播放器区域：
            // 非 16:9 的视频四周有黑边，画布铺满播放器会让作品整体缩放错位。
            var source = PlayerPageSource();
            StringAssert.Contains(
                source,
                "private void UpdateScriptDanmakuViewport()",
                "缺少弹幕画布尺寸计算入口");

            // 每一处都可能改变视频渲染矩形：自然尺寸变化、媒体打开、区域尺寸变化、
            // 以及两个加载脚本的入口（加载后立刻要算一次）。漏一处就是静默错位。
            foreach (var signature in new[]
            {
                "private async void PlaybackSession_NaturalVideoSizeChanged(",
                "private async void MediaPlayer_MediaOpened(",
                "private void UserControl_SizeChanged(",
                "private async void mediaElement_MediaOpened(",
                "private async void menuitem_LoadScriptDanmaku_Click(",
                "private void menuitem_LoadDemoScriptDanmaku_Click("
            })
            {
                StringAssert.Contains(
                    Body(signature),
                    "UpdateScriptDanmakuViewport();",
                    signature + " 未重算脚本弹幕画布");
            }

            var body = Body("private void UpdateScriptDanmakuViewport()");
            StringAssert.Contains(body, "DanmakuViewport.TryFit(");
            // 等比内缩后必须居中，否则黑边只落在单侧。
            StringAssert.Contains(body, "scriptDanmakuControl.HorizontalAlignment = HorizontalAlignment.Center;");
            StringAssert.Contains(body, "scriptDanmakuControl.VerticalAlignment = VerticalAlignment.Center;");
            // 自然尺寸未知（TryFit 失败）时直接返回，保持控件现状而不是把画布清成 0。
            StringAssert.Contains(body, "return;");
        }

        [TestMethod]
        public void ScriptNavigate_IsRestrictedToBilibiliHttps()
        {
            // 脚本可请求跳转，只放行 https + bilibili.com，与 BAS 侧同款规则。
            var body = Body("private async Task NavigateFromScriptDanmakuAsync(");

            StringAssert.Contains(body, "Uri.UriSchemeHttps");
            StringAssert.Contains(body, "\"www.bilibili.com\"");
            StringAssert.Contains(body, "\"bilibili.com\"");
        }
    }
}
