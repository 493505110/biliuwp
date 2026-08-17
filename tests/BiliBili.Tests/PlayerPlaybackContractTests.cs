using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

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

        [TestMethod]
        public void DashPlaybackRequestsAdvertiseAv1Streams()
        {
            var bangumi = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "public static async Task<ReturnPlayModel> GetBilibiliBangumiUrlDash",
                "public static async Task<ReturnPlayModel> GetBilibiliBangumiWebUrl");
            var biliPlus = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "public static async Task<ReturnPlayModel> GetBiliPlusDashUrl",
                "public static async Task<ReturnPlayModel> GetBiliPlusUrl2");
            var video = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "public static async Task<ReturnPlayModel> GetVideoUrlDASH",
                "public static async Task<ReturnPlayModel> GetVideoUrlV1");

            StringAssert.Contains(bangumi, "fnval=4048");
            StringAssert.Contains(biliPlus, "fnval=4048");
            StringAssert.Contains(video, "fnval=4048");
        }

        [TestMethod]
        public void DashResolutionMetadataFlowsToPlayerInfo()
        {
            var dashFactory = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "private static async Task<ReturnPlayModel> CreateDashPlayModel",
                "private static string GetVideoCodecDisplayName");
            var player = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private async Task<bool> ApplyPlaybackSourceAsync",
                "private async Task OpenVideoAsync");

            StringAssert.Contains(dashFactory, "videoWidth = video.width");
            StringAssert.Contains(dashFactory, "videoHeight = video.height");
            StringAssert.Contains(player, "txt_VideoWidth.Text = result.videoWidth ?? string.Empty");
            StringAssert.Contains(player, "txt_VideoHeight.Text = result.videoHeight ?? string.Empty");
        }

        [TestMethod]
        public void DashCodecPreferenceUsesNativeComboBoxes()
        {
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            var expectedTags = new[] { "7", "12", "13" };

            foreach (var relativePath in new[]
            {
                "BiliBili.UWP/Views/SettingPage.xaml",
                "BiliBili.UWP/Pages/PlayerPage.xaml"
            })
            {
                var root = FindRepositoryRoot();
                var document = XDocument.Load(Path.Combine(root, relativePath));
                var comboBox = document
                    .Descendants()
                    .SingleOrDefault(element =>
                        element.Name.LocalName == "ComboBox"
                        && (string)element.Attribute(x + "Name") == "cb_DASHVideoCodec");

                Assert.IsNotNull(comboBox, $"{relativePath} should use a native ComboBox for the DASH codec preference");
                Assert.AreEqual(
                    "DASHVideoCodec_SelectionChanged",
                    (string)comboBox.Attribute("SelectionChanged"));
                Assert.AreEqual("2 0", (string)comboBox.Attribute("Margin"));
                Assert.AreEqual("#00424959", (string)comboBox.Attribute("Background"));
                Assert.AreEqual("0", (string)comboBox.Attribute("BorderThickness"));
                Assert.IsNull(comboBox.Attribute("Width"));
                CollectionAssert.AreEqual(
                    expectedTags,
                    comboBox.Elements()
                        .Where(element => element.Name.LocalName == "ComboBoxItem")
                        .Select(element => (string)element.Attribute("Tag"))
                        .ToArray());
                CollectionAssert.AreEqual(
                    new[] { "AVC/H.264", "HEVC/H.265", "AV1" },
                    comboBox.Elements()
                        .Where(element => element.Name.LocalName == "ComboBoxItem")
                        .Select(element => (string)element.Attribute("Content"))
                        .ToArray());
                Assert.IsFalse(document.Descendants().Any(element =>
                    element.Name.LocalName == "DropDownButton"
                    && (string)element.Attribute(x + "Name") == "btn_DASHVideoCodec"));
            }
        }

        [TestMethod]
        public void DashCodecPreferenceIncludesForceCodecToggles()
        {
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

            foreach (var relativePath in new[]
            {
                "BiliBili.UWP/Views/SettingPage.xaml",
                "BiliBili.UWP/Pages/PlayerPage.xaml"
            })
            {
                var root = FindRepositoryRoot();
                var document = XDocument.Load(Path.Combine(root, relativePath));
                var toggle = document
                    .Descendants()
                    .SingleOrDefault(element =>
                        element.Name.LocalName == "ToggleSwitch"
                        && (string)element.Attribute(x + "Name") == "sw_DASHForceVideoCodec");

                Assert.IsNotNull(toggle, $"{relativePath} should expose the force-codec setting");
                Assert.AreEqual("DASHForceVideoCodec_Toggled", (string)toggle.Attribute("Toggled"));
                Assert.IsTrue(document.Descendants().Any(element =>
                    element.Name.LocalName == "TextBlock"
                    && element.Value == "强制指定编码"));
            }
        }

        [TestMethod]
        public void ForcedDashCodecFailureReachesThePlayer()
        {
            var bangumi = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "public static async Task<ReturnPlayModel> GetBangumiUrl",
                "public static async Task<ReturnPlayModel> GetBilibiliBangumiUrlDash");
            var video = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "public static async Task<ReturnPlayModel> GetVideoUrl(string aid",
                "public static async Task<ReturnPlayModel> GetVideoUrlDASH");
            var dashFactory = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "private static async Task<ReturnPlayModel> CreateDashPlayModel",
                "private static string GetVideoCodecDisplayName");
            var player = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private async Task<bool> ApplyPlaybackSourceAsync",
                "private void LaodSubTitleMenu");

            StringAssert.Contains(bangumi, "bilidash?.errorMessage");
            StringAssert.Contains(video, "bilidash?.errorMessage");
            StringAssert.Contains(dashFactory, "当前视频没有 ");
            StringAssert.Contains(dashFactory, "errorMessage");
            StringAssert.Contains(player, ".errorMessage");
        }

        [TestMethod]
        public void ForcedCodecModeDoesNotFallBackToLegacySources()
        {
            var bangumi = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "public static async Task<ReturnPlayModel> GetBangumiUrl",
                "public static async Task<ReturnPlayModel> GetBilibiliBangumiUrlDash");
            var video = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "public static async Task<ReturnPlayModel> GetVideoUrl(string aid",
                "public static async Task<ReturnPlayModel> GetVideoUrlDASH");
            var dashFactory = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "private static async Task<ReturnPlayModel> CreateDashPlayModel",
                "private static string GetVideoCodecDisplayName");

            StringAssert.Contains(bangumi, "SettingHelper.Get_DASHForceVideoCodec()");
            StringAssert.Contains(bangumi, "preventFallback");
            StringAssert.Contains(video, "SettingHelper.Get_DASHForceVideoCodec()");
            StringAssert.Contains(video, "preventFallback");
            StringAssert.Contains(dashFactory, "preventFallback = true");
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
