using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace BiliBili.Tests
{
    [TestClass]
    public class LivePlaybackSourcesTests
    {
        private static JObject Stream(string protocol, string format, int quality, string path, params string[] hosts)
        {
            return new JObject
            {
                ["protocol_name"] = protocol,
                ["format"] = new JArray(new JObject
                {
                    ["format_name"] = format,
                    ["codec"] = new JArray(new JObject
                    {
                        ["codec_name"] = "avc", ["current_qn"] = quality, ["base_url"] = path,
                        ["url_info"] = new JArray(hosts.Select(host => new JObject
                        {
                            ["host"] = host, ["extra"] = "token=test%2Bvalue&expires=123"
                        }))
                    })
                })
            };
        }

        [TestMethod]
        public void KeepsHlsAfterAllFlvLinesEvenWhenApiReturnsHlsFirst()
        {
            var play = new JObject { ["stream"] = new JArray(
                Stream("http_hls", "ts", 10000, "/live.m3u8?", "https://hls.example"),
                Stream("http_stream", "flv", 10000, "/live.flv?", "https://flv1.example", "https://flv2.example")) };

            var sources = LivePlaybackSources.FromPlayInfo(play, 10000);

            CollectionAssert.AreEqual(new[] { "flv1.example", "flv2.example", "hls.example" },
                sources.Urls.Select(url => new System.Uri(url).Host).ToArray());
            Assert.AreEqual(10000, sources.Quality);
        }

        [TestMethod]
        public void RejectsInvalidFlvAndUsesHlsWhenNoFlvUrlIsUsable()
        {
            var play = new JObject { ["stream"] = new JArray(
                Stream("http_stream", "flv", 10000, "/live.flv?", "", "ftp://invalid.example"),
                Stream("http_hls", "ts", 400, "/live.m3u8?", "https://hls.example")) };

            var sources = LivePlaybackSources.FromPlayInfo(play, 10000);

            Assert.AreEqual(1, sources.Urls.Count);
            Assert.AreEqual(400, sources.Quality);
        }

        [TestMethod]
        public void DeduplicatesLinesAndDoesNotSilentlyLowerQuality()
        {
            var play = new JObject { ["stream"] = new JArray(
                Stream("http_stream", "flv", 400, "/live.flv?", "https://flv.example", "https://flv.example"),
                Stream("http_hls", "ts", 80, "/low.m3u8?", "https://low.example"),
                Stream("http_hls", "fmp4", 400, "/live.m3u8?", "https://hls.example")) };

            var sources = LivePlaybackSources.FromPlayInfo(play, 400);

            Assert.AreEqual(2, sources.Urls.Count);
            Assert.IsFalse(sources.Urls.Any(url => url.Contains("low.example")));
        }

        [DataTestMethod]
        [DataRow("/live.flv?", "token=test%2Bvalue&expires=123", "https://cdn.example/live.flv?token=test%2Bvalue&expires=123")]
        [DataRow("/live.flv", "?token=test%2Bvalue", "https://cdn.example/live.flv?token=test%2Bvalue")]
        [DataRow("/live.flv?mode=1", "&token=test%2Bvalue", "https://cdn.example/live.flv?mode=1&token=test%2Bvalue")]
        public void PreservesSignedQueryWithoutAddingDuplicateSeparators(string path, string extra, string expected)
        {
            var stream = Stream("http_stream", "flv", 400, path, "https://cdn.example");
            stream["format"][0]["codec"][0]["url_info"][0]["extra"] = extra;
            var sources = LivePlaybackSources.FromPlayInfo(new JObject { ["stream"] = new JArray(stream) }, 400);
            Assert.AreEqual(expected, sources.Urls.Single());
        }

        [TestMethod]
        public void IgnoresUnrequestedCodecAndHandlesMissingStreams()
        {
            var stream = Stream("http_hls", "ts", 400, "/live.m3u8?", "https://cdn.example");
            stream["format"][0]["codec"][0]["codec_name"] = "hevc";
            Assert.AreEqual(0, LivePlaybackSources.FromPlayInfo(new JObject { ["stream"] = new JArray(stream) }, 400).Urls.Count);
            Assert.AreEqual(0, LivePlaybackSources.FromPlayInfo(null, 0).Urls.Count);
        }

        private static JObject WithAvailability(JObject stream, bool drm, bool? isPushing)
        {
            var codec = stream["format"][0]["codec"][0];
            codec["drm"] = drm;
            if (isPushing.HasValue) codec["is_pushing"] = isPushing.Value;
            return stream;
        }

        [TestMethod]
        public void ActiveDrmWithInactiveFlvAndTsRequiresOfficialWebPlayback()
        {
            var play = new JObject { ["stream"] = new JArray(
                WithAvailability(Stream("http_stream", "flv", 10000, "/live.flv?", "https://flv.example"), false, false),
                WithAvailability(Stream("http_hls", "ts", 10000, "/live.m3u8?", "https://ts.example"), false, false),
                WithAvailability(Stream("http_hls", "fmp4", 10000, "/live.m3u8?", "https://encrypted.example"), true, true)) };

            var sources = LivePlaybackSources.FromPlayInfo(play, 10000);

            Assert.AreEqual(0, sources.Urls.Count);
            Assert.IsTrue(sources.RequiresWebPlayback);
        }

        [TestMethod]
        public void ActiveClearStreamIsUsedEvenWhenDrmIsAlsoAvailable()
        {
            var play = new JObject { ["stream"] = new JArray(
                WithAvailability(Stream("http_hls", "fmp4", 400, "/encrypted.m3u8?", "https://encrypted.example"), true, true),
                WithAvailability(Stream("http_stream", "flv", 400, "/clear.flv?", "https://clear.example"), false, true)) };

            var sources = LivePlaybackSources.FromPlayInfo(play, 400);

            Assert.AreEqual(1, sources.Urls.Count);
            StringAssert.Contains(sources.Urls[0], "clear.example");
            Assert.IsFalse(sources.RequiresWebPlayback);
        }

        [TestMethod]
        public void InactiveDrmDoesNotTriggerEncryptedLiveNotice()
        {
            var play = new JObject { ["stream"] = new JArray(
                WithAvailability(Stream("http_hls", "fmp4", 400, "/encrypted.m3u8?", "https://encrypted.example"), true, false),
                WithAvailability(Stream("http_stream", "flv", 400, "/clear.flv?", "https://clear.example"), false, false)) };

            var sources = LivePlaybackSources.FromPlayInfo(play, 400);

            Assert.AreEqual(0, sources.Urls.Count);
            Assert.IsFalse(sources.RequiresWebPlayback);
        }

        [TestMethod]
        public void ActiveClearFmp4ReplacesInactivePreferredFlv()
        {
            var play = new JObject { ["stream"] = new JArray(
                WithAvailability(Stream("http_stream", "flv", 10000, "/inactive.flv?", "https://inactive.example"), false, false),
                WithAvailability(Stream("http_hls", "fmp4", 10000, "/clear.m3u8?", "https://clear.example"), false, true)) };

            var sources = LivePlaybackSources.FromPlayInfo(play, 10000);

            Assert.AreEqual(1, sources.Urls.Count);
            StringAssert.Contains(sources.Urls[0], "clear.example");
            Assert.IsFalse(sources.RequiresWebPlayback);
        }

        [TestMethod]
        public void MissingPushingFlagStillExcludesExplicitlyEncryptedSource()
        {
            var play = new JObject { ["stream"] = new JArray(
                WithAvailability(Stream("http_hls", "fmp4", 400, "/encrypted.m3u8?", "https://encrypted.example"), true, null)) };
            var sources = LivePlaybackSources.FromPlayInfo(play, 400);
            Assert.AreEqual(0, sources.Urls.Count);
            Assert.IsTrue(sources.RequiresWebPlayback);
        }

        [TestMethod]
        public void MissingAvailabilityFieldsKeepOlderClearResponsesPlayable()
        {
            var play = new JObject { ["stream"] = new JArray(
                Stream("http_stream", "flv", 400, "/live.flv?", "https://clear.example")) };
            var sources = LivePlaybackSources.FromPlayInfo(play, 400);
            Assert.AreEqual(1, sources.Urls.Count);
            Assert.IsFalse(sources.RequiresWebPlayback);
        }

        [TestMethod]
        public void DrmWithNoDirectUrlsStillProducesOfficialWebNotice()
        {
            var stream = WithAvailability(Stream("http_hls", "fmp4", 400, "/encrypted.m3u8?"), true, true);
            stream["format"][0]["master_url"] = "https://encrypted.example/master.m3u8";
            var sources = LivePlaybackSources.FromPlayInfo(new JObject { ["stream"] = new JArray(stream) }, 400);
            Assert.AreEqual(0, sources.Urls.Count);
            Assert.IsTrue(sources.RequiresWebPlayback);
        }
    }
}
