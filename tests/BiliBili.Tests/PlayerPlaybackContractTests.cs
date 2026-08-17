using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlayerPlaybackContractTests
    {
        [TestMethod]
        public void NewHistoryRecordUsesStoredProgressPolicy()
        {
            var method = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private void UpdateLocalHistory(PlayerModel item, int progress)",
                "public void UpdateSetting()");

            StringAssert.Contains(method, "PlaybackHistory.GetStoredProgress(item.isInteraction, progress)");
            StringAssert.Contains(method, "Post = storedProgress");
        }

        [TestMethod]
        public void PlaylistChangeReportsPreviousItemBeforeReplacingCurrentItem()
        {
            var method = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private void gv_play_SelectionChanged",
                "private void btn_Select_Click");

            var reportIndex = method.IndexOf("ReportHistory(previousItem", StringComparison.Ordinal);
            var replaceIndex = method.IndexOf("playNow = selectedItem", StringComparison.Ordinal);

            Assert.IsTrue(reportIndex >= 0, "切集前没有上报上一播放项");
            Assert.IsTrue(replaceIndex > reportIndex, "必须先保存上一播放项，再替换 playNow");
        }

        [TestMethod]
        public void SuccessfulOpenRegistersCurrentItemHistory()
        {
            var method = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private async Task OpenVideoAsync(PlayerModel item)",
                "private void LaodSubTitleMenu");

            StringAssert.Contains(method, "await ReportHistory(item, 0)");
        }

        [TestMethod]
        public void SourceChangesUseTheMatchingRestorePolicy()
        {
            var qualitySelection = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private void cb_Quity_SelectionChanged",
                "private void _lastpost_out_Completed");
            var nodeChange = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "public async void ChangeNode",
                "bool settingStorylist");

            StringAssert.Contains(qualitySelection, "PlaybackRestoreState.ForQualityChange(position, shouldPlay)");
            StringAssert.Contains(nodeChange, "PlaybackRestoreState.ForContentChange()");
        }

        [TestMethod]
        public void LoadedDoesNotStartControlAutoHideTimer()
        {
            var method = ReadMethod(
                "BiliBili.UWP/Controls/DanmakuMTC.cs",
                "private void DanmakuMTC_Loaded",
                "private void DanmakuMTC_Unloaded");

            Assert.IsFalse(method.Contains("timer2.Start()"), "Loaded 不应启动控制栏自动隐藏计时器");
        }

        private static string ReadMethod(string relativePath, string startMarker, string endMarker)
        {
            var root = FindRepositoryRoot();
            var source = File.ReadAllText(Path.Combine(root, relativePath));
            var start = source.IndexOf(startMarker, StringComparison.Ordinal);
            var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);

            Assert.IsTrue(start >= 0, $"找不到方法起点: {startMarker}");
            Assert.IsTrue(end > start, $"找不到方法终点: {endMarker}");
            return source.Substring(start, end - start);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BiliBili.sln")))
            {
                directory = directory.Parent;
            }

            Assert.IsNotNull(directory, "找不到 BiliBili.sln 所在目录");
            return directory.FullName;
        }
    }
}
