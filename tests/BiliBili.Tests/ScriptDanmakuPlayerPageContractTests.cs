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
