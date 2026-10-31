using System;
using System.Threading.Tasks;
using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaybackNetworkStatisticsTests
    {
        [DataTestMethod]
        [DataRow("https://cdn.example.com/video?token=secret", "cdn.example.com")]
        [DataRow("http://audio.example.com:8082/audio", "audio.example.com:8082")]
        [DataRow("https://user:password@cdn.example.com:443/video", "cdn.example.com")]
        [DataRow("http://[::1]:8080/video", "[::1]:8080")]
        [DataRow("file:///C:/video.mp4", null)]
        [DataRow("not a url", null)]
        [DataRow(null, null)]
        public void HostContainsOnlyHostAndNonDefaultPort(string url, string expected)
        {
            Assert.AreEqual(expected, PlaybackNetworkStatistics.GetHost(url));
        }

        [TestMethod]
        public void MediaBitrateSamplingIsIndependentOfDownloadedBytes()
        {
            var stats = new PlaybackNetworkStatistics("https://video.example/v", "https://audio.example/a");
            stats.RecordDownload(false, "https://new.example:8082/v?token=secret", 2000000);
            stats.RecordDownload(false, null, 1000000);
            stats.RecordDownload(true, "https://audio.example/a", 250000);
            stats.Sample(4000000, 128000);
            var snapshot = stats.GetSnapshot();
            Assert.AreEqual(4000d, snapshot.VideoBitrateSamples[0]);
            Assert.AreEqual(128d, snapshot.AudioBitrateSamples[0]);
            Assert.AreEqual(3250000d / 1024, snapshot.NetworkActivitySamples[0]);
            Assert.AreEqual("new.example:8082", snapshot.VideoHost);
            Assert.AreEqual("audio.example", snapshot.AudioHost);

            stats.Sample(4000000, 128000);
            snapshot = stats.GetSnapshot();
            Assert.AreEqual(4000d, snapshot.VideoBitrateSamples[1]);
            Assert.AreEqual(128d, snapshot.AudioBitrateSamples[1]);
            Assert.AreEqual(0d, snapshot.NetworkActivitySamples[1]);
        }

        [TestMethod]
        public void HistoryIsBoundedAndSnapshotsDoNotChangeAfterSampling()
        {
            var stats = new PlaybackNetworkStatistics(null, null);
            stats.RecordDownload(false, null, 1000);
            stats.Sample(8000, 16000);
            var first = stats.GetSnapshot();
            for (int i = 0; i < 65; i++) stats.Sample(9000, 17000);
            var latest = stats.GetSnapshot();
            Assert.AreEqual(PlaybackNetworkStatistics.HistoryLength, latest.VideoBitrateSamples.Length);
            Assert.AreEqual(PlaybackNetworkStatistics.HistoryLength, latest.AudioBitrateSamples.Length);
            Assert.AreEqual(PlaybackNetworkStatistics.HistoryLength, latest.NetworkActivitySamples.Length);
            Assert.AreEqual(9d, latest.VideoBitrateSamples[0]);
            Assert.AreEqual(17d, latest.AudioBitrateSamples[0]);
            Assert.AreEqual(0d, latest.NetworkActivitySamples[0]);
            Assert.AreEqual(8d, first.VideoBitrateSamples[0]);
            Assert.AreEqual(1000d / 1024, first.NetworkActivitySamples[0]);
            var nextSource = new PlaybackNetworkStatistics(null, null).GetSnapshot();
            Assert.IsNull(nextSource.VideoHost);
            Assert.AreEqual(0, nextSource.VideoBitrateSamples.Length);
            Assert.AreEqual(0, nextSource.NetworkActivitySamples.Length);
        }

        [TestMethod]
        public void MissingAndInvalidBitratesStayUnknownWithoutLosingNetworkActivity()
        {
            var stats = new PlaybackNetworkStatistics(null, null);
            stats.RecordDownload(false, null, 1000);
            foreach (var bitrate in new double?[] { null, 0, -1, double.NaN, double.PositiveInfinity })
                stats.Sample(bitrate, bitrate);
            var snapshot = stats.GetSnapshot();
            foreach (var sample in snapshot.VideoBitrateSamples) Assert.IsNull(sample);
            foreach (var sample in snapshot.AudioBitrateSamples) Assert.IsNull(sample);
            Assert.AreEqual(1000d / 1024, snapshot.NetworkActivitySamples[0]);
            Assert.AreEqual(0d, snapshot.NetworkActivitySamples[1]);
        }

        [TestMethod]
        public void ConcurrentDownloadCallbacksDoNotLoseBytes()
        {
            var stats = new PlaybackNetworkStatistics(null, null);
            Parallel.For(0, 10000, i => stats.RecordDownload(i % 2 == 0, null, 1000));
            stats.Sample(4000000, 128000);
            var snapshot = stats.GetSnapshot();
            Assert.AreEqual(4000d, snapshot.VideoBitrateSamples[0]);
            Assert.AreEqual(128d, snapshot.AudioBitrateSamples[0]);
            Assert.AreEqual(10000000d / 1024, snapshot.NetworkActivitySamples[0]);
        }

        [TestMethod]
        public void NetworkActivityReportsCombinedKilobytesIndependentlyOfBitrate()
        {
            var stats = new PlaybackNetworkStatistics(null, null);
            stats.RecordDownload(false, null, 3 * 1024);
            stats.RecordDownload(true, null, 2 * 1024);
            stats.Sample(4000000, 128000);
            var snapshot = stats.GetSnapshot();
            Assert.AreEqual(5d, snapshot.NetworkActivitySamples[0]);

            // 同样的下载量对应不同媒体码率时，活动量不变。
            stats.RecordDownload(false, null, 5 * 1024);
            stats.Sample(12000000, 64000);
            snapshot = stats.GetSnapshot();
            Assert.AreEqual(5d, snapshot.NetworkActivitySamples[1]);
            Assert.AreEqual(12000d, snapshot.VideoBitrateSamples[1]);
            Assert.AreEqual(64d, snapshot.AudioBitrateSamples[1]);
        }

        [TestMethod]
        public void BitrateHistoryCanReturnToAnEarlierSegmentAfterSeek()
        {
            var stats = new PlaybackNetworkStatistics(null, null);
            stats.Sample(4000000, 128000);
            stats.Sample(12000000, 96000);
            stats.Sample(4000000, 128000);
            var snapshot = stats.GetSnapshot();
            CollectionAssert.AreEqual(new double?[] { 4000, 12000, 4000 }, snapshot.VideoBitrateSamples);
            CollectionAssert.AreEqual(new double?[] { 128, 96, 128 }, snapshot.AudioBitrateSamples);
        }
    }
}
