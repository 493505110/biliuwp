using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BiliBili.Tests
{
    [TestClass]
    public class DashStreamSelectorTests
    {
        [TestMethod]
        public void SelectVideoPrefersExactQualityAndCodec()
        {
            var streams = new List<DashStreamInfo>
            {
                new DashStreamInfo(80, 7, 800, "video/mp4", "https://video/80"),
                new DashStreamInfo(64, 7, 640, "video/mp4", "https://video/64"),
                new DashStreamInfo(80, 12, 900, "video/mp4", "https://video/hevc")
            };

            var selected = DashStreamSelector.SelectVideo(streams, 80, 7);

            Assert.AreEqual("https://video/80", selected.BaseUrl);
        }

        [TestMethod]
        public void SelectVideoFallsBackToHighestLowerQuality()
        {
            var streams = new List<DashStreamInfo>
            {
                new DashStreamInfo(32, 7, 320, "video/mp4", "https://video/32"),
                new DashStreamInfo(64, 7, 640, "video/mp4", "https://video/64"),
                new DashStreamInfo(80, 12, 900, "video/mp4", "https://video/hevc")
            };

            var selected = DashStreamSelector.SelectVideo(streams, 80, 7);

            Assert.AreEqual("https://video/64", selected.BaseUrl);
        }

        [TestMethod]
        public void SelectAudioChoosesHighestMp4Bandwidth()
        {
            var streams = new List<DashStreamInfo>
            {
                new DashStreamInfo(0, 0, 128, "audio/mp4", "https://audio/low"),
                new DashStreamInfo(0, 0, 320, "audio/mp4", "https://audio/high"),
                new DashStreamInfo(0, 0, 999, "audio/webm", "https://audio/webm")
            };

            var selected = DashStreamSelector.SelectAudio(streams);

            Assert.AreEqual("https://audio/high", selected.BaseUrl);
        }

        [TestMethod]
        public void SelectorRejectsMissingOrEmptyStreams()
        {
            Assert.IsNull(DashStreamSelector.SelectVideo(null, 80, 7));
            Assert.IsNull(DashStreamSelector.SelectAudio(new List<DashStreamInfo>()));
            Assert.IsFalse(DashStreamSelector.IsPlayable(null));
            Assert.IsFalse(DashStreamSelector.IsPlayable(new DashStreamInfo(80, 7, 800, "video/mp4", "")));
        }

        [TestMethod]
        public void ResolvePlayableUrlUsesFirstValidBackupWhenPrimaryIsInvalid()
        {
            var result = DashStreamSelector.ResolvePlayableUrl(
                "not-a-url",
                new[] { "", "https://backup/stream", "https://backup/other" });

            Assert.AreEqual("https://backup/stream", result);
        }

        [TestMethod]
        public void GetCodecPreferenceFallsBackFromHevcToAvc()
        {
            CollectionAssert.AreEqual(
                new[] { 12, 7 },
                DashStreamSelector.GetCodecPreference(12).ToArray());
            CollectionAssert.AreEqual(
                new[] { 7 },
                DashStreamSelector.GetCodecPreference(7).ToArray());
        }
    }
}
