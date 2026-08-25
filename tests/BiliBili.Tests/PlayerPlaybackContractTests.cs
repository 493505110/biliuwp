using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using static BiliBili.Tests.TestRepository;

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
                var document = XDocument.Load(GetPath(relativePath));
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
                var document = XDocument.Load(GetPath(relativePath));
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
        public void VideoDanmakuFailureDoesNotAbortPlayback()
        {
            var source = ReadFile("BiliBili.UWP/Pages/PlayerPage.xaml.cs");
            var video = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "case PlayMode.Video:",
                "case PlayMode.QQ:");
            var danmakuLoader = MethodBody(
                source,
                "private async Task<BiliDanmakuLoadResult> LoadDanmakuOrEmptyAsync");

            StringAssert.Contains(video, "LoadDanmakuOrEmptyAsync(");
            StringAssert.Contains(video, "item.Duration,\n                            danmakuCancellationToken)");
            StringAssert.Contains(video, "await Task.WhenAll(videoDanmakuTask, videoSourceTask)");
            StringAssert.Contains(video, "var videoSource = videoSourceTask.Result;");
            StringAssert.Contains(video, "ApplyInitialDanmaku(");
            StringAssert.Contains(video, "videoDanmakuTask.Result,\n                            requestId,\n                            item,\n                            danmakuCancellationToken)");
            StringAssert.Contains(video, "await ApplyPlaybackSourceAsync(videoSource, requestId, item)");

            var loadIndex = video.IndexOf("await Task.WhenAll(videoDanmakuTask, videoSourceTask)", StringComparison.Ordinal);
            var danmakuIndex = video.IndexOf("ApplyInitialDanmaku(", StringComparison.Ordinal);
            var applyIndex = video.IndexOf("await ApplyPlaybackSourceAsync(videoSource, requestId, item)", StringComparison.Ordinal);
            Assert.IsTrue(loadIndex >= 0 && danmakuIndex > loadIndex, "弹幕与播放源应先完成加载");
            Assert.IsTrue(applyIndex > danmakuIndex, "弹幕应在播放源应用前设置");
            StringAssert.Contains(danmakuLoader, "catch (Exception ex)");
            StringAssert.Contains(danmakuLoader, "加载弹幕失败，继续播放");
            StringAssert.Contains(danmakuLoader, "new BiliDanmakuLoadResult");
            StringAssert.Contains(source, "CancellationToken cancellationToken = default(CancellationToken)");
            StringAssert.Contains(source, "LoadInitialAsync(");
            StringAssert.Contains(source, "LoadSupplementAsync(initial, cancellationToken)");
            StringAssert.Contains(source, "ApplyDanmakuSupplementWhenReadyAsync");
        }

        [TestMethod]
        public void WebDanmakuSegmentMetadataUsesPageSizeAndHandlesEmptyResponses()
        {
            var service = ReadFile("BiliBili.UWP/Helper/BiliDanmakuService.cs");
            var segmentCount = MethodBody(service, "private static bool TryGetSegmentCount");
            var segmentRequest = MethodBody(service, "private static async Task<byte[]> GetSegmentBytesAsync");
            var webInitial = MethodBody(service, "private static async Task<BiliDanmakuLoadResult> LoadWebInitialAsync");
            var supplement = MethodBody(service, "public static async Task<BiliDanmakuLoadResult> LoadSupplementAsync");

            StringAssert.Contains(segmentCount, "durationSeconds * 1000d / pageSize");
            StringAssert.Contains(segmentCount, "MaxUnknownDurationSegmentCount");
            Assert.IsFalse(segmentCount.Contains("segmentField.Number == 2"), "不能把 dmSge.total 当作分段循环次数");
            StringAssert.Contains(segmentRequest, "response.IsNotModified");
            StringAssert.Contains(segmentRequest, "response.Bytes == null");
            StringAssert.Contains(segmentRequest, "return null;");
            StringAssert.Contains(service, "long aid,");
            StringAssert.Contains(segmentRequest, "\"&pid=\" + aid.ToString(CultureInfo.InvariantCulture)");
            StringAssert.Contains(webInitial, "TryGetDurationSecondsAsync(aid, cid)");
            StringAssert.Contains(webInitial, "LoadSegmentAsync(plan, 1, cancellationToken)");
            StringAssert.Contains(supplement, "LoadRemainingSegmentsAsync(plan, cancellationToken)");
            StringAssert.Contains(supplement, "LoadLegacyAsync(plan.Cid)");
        }

        [TestMethod]
        public void NewDanmakuPoolCanBeSupplementedWithoutTextDeduplication()
        {
            var service = ReadFile("BiliBili.UWP/Helper/BiliDanmakuService.cs");
            StringAssert.Contains(service, "LoadLegacySupplementAsync");
            StringAssert.Contains(service, "MergeDanmaku");
            StringAssert.Contains(service, "NeedsLegacySupplement");
            StringAssert.Contains(service, "different danmaku ids");
            Assert.IsFalse(service.Contains("item.text, StringComparison.Ordinal"), "不能按文本去重重复弹幕");
        }

        [TestMethod]
        public void ResolutionAspectModeResizesViewToNaturalVideoSize()
        {
            var playerPage = ReadFile("BiliBili.UWP/Pages/PlayerPage.xaml.cs");
            var playerXaml = ReadFile("BiliBili.UWP/Pages/PlayerPage.xaml");

            StringAssert.Contains(playerXaml, "x:Name=\"rb_resolution\"");
            StringAssert.Contains(playerXaml, "严格按照视频分辨率");
            StringAssert.Contains(playerPage, "private void cb_setting_resolution_Checked");
            StringAssert.Contains(playerPage, "private void ResizeViewToVideoResolution");
            StringAssert.Contains(playerPage, "NaturalVideoWidth");
            StringAssert.Contains(playerPage, "NaturalVideoHeight");
            StringAssert.Contains(playerPage, "TryResizeView(new Size(naturalWidth, naturalHeight))");
        }

        [TestMethod]
        public void NewDanmakuInterfaceSettingDefaultsOnAndControlsAllLoads()
        {
            var settings = ReadFile("BiliBili.UWP/Helper/SettingHelper.cs");
            var service = ReadFile("BiliBili.UWP/Helper/BiliDanmakuService.cs");
            var settingPage = ReadFile("BiliBili.UWP/Views/SettingPage.xaml");
            var settingCode = ReadFile("BiliBili.UWP/Views/SettingPage.xaml.cs");
            var playerPage = ReadFile("BiliBili.UWP/Pages/PlayerPage.xaml.cs");
            var playerXaml = ReadFile("BiliBili.UWP/Pages/PlayerPage.xaml");

            StringAssert.Contains(settings, "Set_UseNewDanmakuInterface(true);");
            StringAssert.Contains(service, "SettingHelper.Get_UseNewDanmakuInterface()");
            StringAssert.Contains(service, "if (!SettingHelper.Get_UseNewDanmakuInterface())");
            StringAssert.Contains(service, "LoadInitialAsync(");
            StringAssert.Contains(service, "LoadSupplementAsync(initial, cancellationToken)");
            StringAssert.Contains(service, "if (failedRegularSegmentCount != 0)");
            StringAssert.Contains(settingPage, "x:Name=\"sw_UseNewDanmakuInterface\"");
            StringAssert.Contains(settingPage, "Toggled=\"sw_UseNewDanmakuInterface_Toggled\"");
            StringAssert.Contains(settingCode, "sw_UseNewDanmakuInterface.IsOn = SettingHelper.Get_UseNewDanmakuInterface();");
            StringAssert.Contains(settingCode, "SettingHelper.Set_UseNewDanmakuInterface(sw_UseNewDanmakuInterface.IsOn);");
            StringAssert.Contains(playerXaml, "x:Name=\"sw_UseNewDanmakuInterface\"");
            StringAssert.Contains(playerXaml, "Toggled=\"sw_UseNewDanmakuInterface_Toggled\"");
            StringAssert.Contains(playerPage, "sw_UseNewDanmakuInterface.IsOn = SettingHelper.Get_UseNewDanmakuInterface();");
            StringAssert.Contains(playerPage, "SettingHelper.Set_UseNewDanmakuInterface(sw_UseNewDanmakuInterface.IsOn);");
        }

        [TestMethod]
        public void PlaybackInitializationLogsRequestStrategy()
        {
            var source = ReadFile("BiliBili.UWP/Pages/PlayerPage.xaml.cs");
            var openVideo = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private async Task OpenVideoAsync(PlayerModel item)",
                "private void LaodSubTitleMenu");
            var strategy = MethodBody(
                source,
                "private string GetPlaybackRequestStrategy");

            StringAssert.Contains(openVideo, "AddLog(\"请求策略：\" + GetPlaybackRequestStrategy())");
            Assert.IsFalse(openVideo.Contains("UpdateSoftwareDecodeInfo(null)"));
            StringAssert.Contains(strategy, "SettingHelper.Get_UseDASH()");
            StringAssert.Contains(strategy, "SettingHelper.Get_ForceVideo()");
            StringAssert.Contains(strategy, "DASH + FFmpeg 软解");
            StringAssert.Contains(strategy, "DASH + 系统决定");
            StringAssert.Contains(strategy, "传统流 + SYEngine");
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

        [TestMethod]
        public void ForcedSoftwareDashUsesFfmpegSoftwareDecoder()
        {
            var project = XDocument.Load(GetPath("BiliBili.UWP/BiliBili.UWP.csproj"));

            var packageReference = project.Descendants().SingleOrDefault(element =>
                element.Name.LocalName == "PackageReference"
                && (string)element.Attribute("Include") == "FFmpegInteropX.UWP");
            Assert.IsNotNull(packageReference, "BiliBili.UWP.csproj should reference FFmpegInteropX.UWP");
            Assert.AreEqual(
                "2.1.0.81200",
                packageReference.Elements().Single(element => element.Name.LocalName == "Version").Value);
            Assert.AreEqual("'$(Platform)' != 'ARM'", (string)packageReference.Attribute("Condition"));

            foreach (var configuration in new[] { "Debug|x86", "Release|x86", "Debug|x64", "Release|x64" })
            {
                var propertyGroup = project.Descendants().Single(element =>
                    element.Name.LocalName == "PropertyGroup"
                    && ((string)element.Attribute("Condition"))?.Contains(configuration) == true);
                var defineConstants = propertyGroup.Elements().Single(element =>
                    element.Name.LocalName == "DefineConstants").Value;
                StringAssert.Contains(
                    defineConstants,
                    "FFMPEG_INTEROP_SUPPORTED",
                    configuration + " 必须启用真实 FFmpegInteropX 实现");
            }

            var ffmpegSourcePath = GetPath("BiliBili.UWP/Helper/FFmpegDashSource.cs");
            Assert.IsTrue(File.Exists(ffmpegSourcePath), "缺少 FFmpegDashSource.cs");
            var ffmpegFactory = ReadMethod(
                "BiliBili.UWP/Helper/FFmpegDashSource.cs",
                "public static async Task<FFmpegDashSource> CreateAsync",
                "public MediaPlaybackItem CreateVideoPlaybackItem");
            var decoderModeIndex = ffmpegFactory.IndexOf(
                "config.Video.VideoDecoderMode = VideoDecoderMode.ForceFFmpegSoftwareDecoder;",
                StringComparison.Ordinal);
            var videoSourceIndex = ffmpegFactory.IndexOf(
                "FFmpegMediaSource.CreateFromUriAsync(videoUrl, config)",
                StringComparison.Ordinal);
            var audioSourceIndex = ffmpegFactory.IndexOf(
                "FFmpegMediaSource.CreateFromUriAsync(audioUrl, config)",
                StringComparison.Ordinal);

            Assert.IsTrue(decoderModeIndex >= 0, "FFmpeg DASH must explicitly force software video decoding");
            Assert.IsTrue(videoSourceIndex > decoderModeIndex, "The selected video URL must be opened directly by FFmpeg");
            Assert.IsTrue(audioSourceIndex > videoSourceIndex, "The selected audio URL must be opened directly by FFmpeg");
            Assert.IsFalse(ffmpegFactory.Contains("CreateFromStreamAsync"),
                "An in-memory MPD cannot propagate CDN headers to DASH child requests");
            StringAssert.Contains(ffmpegFactory, "config.FFmpegOptions[\"referer\"]");
            StringAssert.Contains(ffmpegFactory, "config.FFmpegOptions[\"user_agent\"]");

            StringAssert.Contains(ffmpegFactory, "FFmpegInteropLogging.SetLogLevel(LogLevel.Error);");
            StringAssert.Contains(ffmpegFactory, "FFmpegInteropLogging.SetLogProvider(logProvider);");
            StringAssert.Contains(ffmpegFactory, "FFmpegInteropLogging.SetDefaultLogProvider();");
            StringAssert.Contains(ffmpegFactory, "FFmpeg DASH 原生日志");

            var dashFactory = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "private static async Task<ReturnPlayModel> CreateDashPlayModel",
                "private static string GetVideoCodecDisplayName");
            var forceVideoIndex = dashFactory.IndexOf("if (SettingHelper.Get_ForceVideo())", StringComparison.Ordinal);
            var ffmpegDashIndex = dashFactory.IndexOf("FFmpegDashSource.CreateAsync", StringComparison.Ordinal);
            var adaptiveDashIndex = dashFactory.IndexOf("CreateAdaptiveMediaSource(video, audio)", StringComparison.Ordinal);

            Assert.IsTrue(forceVideoIndex >= 0, "DASH 创建路径必须读取强制软解设置");
            Assert.IsTrue(ffmpegDashIndex > forceVideoIndex, "强制软解分支必须创建 FFmpeg DASH 源");
            Assert.IsTrue(adaptiveDashIndex > ffmpegDashIndex, "Windows AdaptiveMediaSource 必须保留在非强制软解分支");
            StringAssert.Contains(dashFactory, "FFmpegDashSource.CreateAsync(GetBaseUrl(video), GetBaseUrl(audio))");
            Assert.IsFalse(dashFactory.Contains("DashMpdBuilder"), "The software path must not build an in-memory MPD");

            var bangumiEntry = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "public static async Task<ReturnPlayModel> GetBangumiUrl",
                "public static async Task<ReturnPlayModel> GetBiliPlus");
            var biliPlusEntry = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "public static async Task<ReturnPlayModel> GetBiliPlus",
                "public static async Task<ReturnPlayModel> GetBilibiliBangumiUrl");
            var videoEntry = ReadMethod(
                "BiliBili.UWP/Helper/PlayurlHelper.cs",
                "public static async Task<ReturnPlayModel> GetVideoUrl(string aid",
                "public static async Task<ReturnPlayModel> GetVideoUrlDASH");

            StringAssert.Contains(bangumiEntry, "bilidash?.ffmpegDashSource != null");
            StringAssert.Contains(biliPlusEntry, "biliplusdash?.ffmpegDashSource != null");
            StringAssert.Contains(videoEntry, "bilidash?.ffmpegDashSource != null");

            var ffmpegSource = ReadFile("BiliBili.UWP/Helper/FFmpegDashSource.cs");
            StringAssert.Contains(ffmpegSource, "#if FFMPEG_INTEROP_SUPPORTED");
            StringAssert.Contains(ffmpegSource, "Task.FromResult<FFmpegDashSource>(null)");
            StringAssert.Contains(ffmpegSource, "private FFmpegMediaSource videoSource;");
            StringAssert.Contains(ffmpegSource, "private FFmpegMediaSource audioSource;");
            StringAssert.Contains(ffmpegSource, "using Windows.Media.Playback;");
            StringAssert.Contains(ffmpegSource, "CreateVideoPlaybackItem()");
            StringAssert.Contains(ffmpegSource, "CreateAudioPlaybackItem()");
            Assert.IsFalse(ffmpegSource.Contains("GetVideoMediaStreamSource"),
                "FFmpegInteropX 源必须通过 MediaPlaybackItem 暴露给 MediaPlayer");
            Assert.IsFalse(ffmpegSource.Contains("GetAudioMediaStreamSource"),
                "FFmpegInteropX 源必须通过 MediaPlaybackItem 暴露给 MediaPlayer");

            var playerSource = ReadFile("BiliBili.UWP/Pages/PlayerPage.xaml.cs");
            Assert.IsFalse(playerSource.Contains("GetVideoMediaStreamSource"),
                "PlayerPage 不得直接消费尚未创建 MediaPlaybackItem 的 MediaStreamSource");
            Assert.IsFalse(playerSource.Contains("GetAudioMediaStreamSource"),
                "PlayerPage 不得直接消费尚未创建 MediaPlaybackItem 的 MediaStreamSource");
            StringAssert.Contains(playerSource, "CreateVideoPlaybackItem()");
            StringAssert.Contains(playerSource, "CreateAudioPlaybackItem()");
        }

        [TestMethod]
        public void PlayerRetainsAndDisposesFfmpegDashSource()
        {
            var player = ReadFile("BiliBili.UWP/Pages/PlayerPage.xaml.cs");
            Assert.IsTrue(player.Contains("FFmpegDashSource ffmpegDashSource;"),
                "PlayerPage 必须持有当前 FFmpeg DASH 源");

            var applySource = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private async Task<bool> ApplyPlaybackSourceAsync",
                "private async Task OpenVideoAsync");
            StringAssert.Contains(applySource, "result?.ffmpegDashSource?.Dispose();");
            StringAssert.Contains(applySource, "result.ffmpegDashSource?.Dispose();");
            StringAssert.Contains(applySource, "ReleaseFFmpegDashSource();");
            StringAssert.Contains(applySource, "ffmpegDashSource = result.ffmpegDashSource;");
            StringAssert.Contains(applySource, "ffmpegOwnershipTransferred");
            StringAssert.Contains(applySource, "catch");
            StringAssert.Contains(applySource, "CreateVideoPlaybackItem()");
            StringAssert.Contains(applySource, "CreateAudioPlaybackItem()");
            StringAssert.Contains(applySource, "mediaPlayer_audio = new MediaPlayer();");
            StringAssert.Contains(applySource, "mediaPlayer_audio.CommandManager.IsEnabled = false;");
            StringAssert.Contains(applySource, "mediaPlayer_audio.Source = audioSource;");

            var transferIndex = applySource.IndexOf(
                "ffmpegDashSource = result.ffmpegDashSource;", StringComparison.Ordinal);
            var assignPlayerIndex = applySource.IndexOf("mediaPlayer.Source = source;", StringComparison.Ordinal);
            Assert.IsTrue(assignPlayerIndex > transferIndex, "必须先接管 FFmpeg 源，再交给 MediaPlayer");

            var cleanup = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private void ClearPlaybackSource()",
                "private bool IsPlaybackRequestCurrent");
            var clearPlayerIndex = cleanup.IndexOf("mediaPlayer.Source = null;", StringComparison.Ordinal);
            var disposeAudioIndex = cleanup.IndexOf("DisposeAuxiliaryMediaPlayer();", StringComparison.Ordinal);
            var releaseIndex = cleanup.IndexOf("ReleaseFFmpegDashSource();", StringComparison.Ordinal);
            var tryIndex = cleanup.IndexOf("try", StringComparison.Ordinal);
            var finallyIndex = cleanup.IndexOf("finally", StringComparison.Ordinal);
            var copyIndex = cleanup.IndexOf("var source = ffmpegDashSource;", StringComparison.Ordinal);
            var clearOwnerIndex = cleanup.IndexOf("ffmpegDashSource = null;", StringComparison.Ordinal);
            var disposeIndex = cleanup.IndexOf("source?.Dispose();", StringComparison.Ordinal);

            Assert.IsTrue(
                tryIndex >= 0 && clearPlayerIndex > tryIndex && finallyIndex > clearPlayerIndex && releaseIndex > finallyIndex,
                "即使清空 MediaPlayer.Source 失败，也必须在 finally 中释放 FFmpeg 源");
            Assert.IsTrue(releaseIndex > clearPlayerIndex, "必须先清空 MediaPlayer.Source，再释放 FFmpeg 源");
            Assert.IsTrue(disposeAudioIndex > clearPlayerIndex && releaseIndex > disposeAudioIndex,
                "The auxiliary audio player must be cleared before disposing its FFmpeg source");
            Assert.IsTrue(copyIndex >= 0 && clearOwnerIndex > copyIndex && disposeIndex > clearOwnerIndex,
                "释放 FFmpeg 源时必须先清空页面持有的引用");

            var disposePlayer = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private void DisposeMediaPlayer",
                "private void ResetMediaPlayer");
            var disposePlayerSourceIndex = disposePlayer.IndexOf("player.Source = null;", StringComparison.Ordinal);
            var disposeFfmpegIndex = disposePlayer.IndexOf("ReleaseFFmpegDashSource();", StringComparison.Ordinal);
            Assert.IsTrue(disposeFfmpegIndex > disposePlayerSourceIndex,
                "销毁播放器时必须在清空 MediaPlayer.Source 后释放 FFmpeg 源");

            var disposeAudioPlayer = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private void DisposeAuxiliaryMediaPlayer",
                "private void DisposeMediaPlayer");
            var disposeAudioFinallyIndex = disposeAudioPlayer.IndexOf("finally", StringComparison.Ordinal);
            var destroyAudioPlayerIndex = disposeAudioPlayer.IndexOf("player.Dispose();", StringComparison.Ordinal);
            Assert.IsTrue(
                disposeAudioFinallyIndex >= 0 && destroyAudioPlayerIndex > disposeAudioFinallyIndex,
                "即使辅助播放器暂停或清空 Source 失败，也必须在 finally 中销毁播放器");
        }

        [TestMethod]
        public void AuxiliaryFfmpegAudioTracksPlaybackRate()
        {
            var playbackStateChanged = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private async void PlaybackSession_PlaybackStateChanged",
                "private async void MediaPlayer_MediaFailed");
            var mainRateIndex = playbackStateChanged.IndexOf(
                "mediaPlayer.PlaybackSession.PlaybackRate = slider_Rate.Value;",
                StringComparison.Ordinal);
            var audioRateIndex = playbackStateChanged.IndexOf(
                "mediaPlayer_audio.PlaybackSession.PlaybackRate = mediaPlayer.PlaybackSession.PlaybackRate;",
                StringComparison.Ordinal);
            var audioPlayIndex = playbackStateChanged.IndexOf("mediaPlayer_audio.Play();", StringComparison.Ordinal);

            Assert.IsTrue(mainRateIndex >= 0, "进入 Playing 时必须先恢复主播放器目标倍速");
            Assert.IsTrue(audioRateIndex > mainRateIndex, "辅助音频必须复制恢复后的主播放器倍速");
            Assert.IsTrue(audioPlayIndex > audioRateIndex, "辅助音频必须在倍速同步后开始播放");
        }

        [TestMethod]
        public void AuxiliaryFfmpegAudioCallbacksHandleSourceDisposalRace()
        {
            var volumeChanged = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private void MediaPlayer_VolumeChanged",
                "private void PlaybackSession_PositionChanged");
            StringAssert.Contains(volumeChanged, "var audioPlayer = mediaPlayer_audio;");
            StringAssert.Contains(volumeChanged, "catch (Exception ex)");
            Assert.IsFalse(volumeChanged.Contains("mediaPlayer_audio.Volume ="),
                "音量回调不得在空值检查后再次读取可并发清空的页面字段");

            var positionChanged = ReadMethod(
                "BiliBili.UWP/Pages/PlayerPage.xaml.cs",
                "private void PlaybackSession_PositionChanged",
                "private async void PlaybackSession_NaturalVideoSizeChanged");
            StringAssert.Contains(positionChanged, "var audioPlayer = mediaPlayer_audio;");
            StringAssert.Contains(positionChanged, "var audioSession = audioPlayer.PlaybackSession;");
            StringAssert.Contains(positionChanged, "catch (Exception ex)");
            Assert.IsFalse(positionChanged.Contains("mediaPlayer_audio.PlaybackSession"),
                "位置回调必须只通过局部快照访问辅助播放器");
        }

    }
}
