using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaybackUrlTests
    {
        [DataTestMethod]
        [DataRow("https://example.com/subtitle.json", "https://example.com/subtitle.json")]
        [DataRow("http://example.com/subtitle.json", "http://example.com/subtitle.json")]
        [DataRow("//example.com/subtitle.json", "https://example.com/subtitle.json")]
        public void TryNormalizeHttpUrlHandlesSupportedSubtitleUrls(string input, string expected)
        {
            Assert.IsTrue(PlaybackUrl.TryNormalizeHttpUrl(input, out var result));
            Assert.AreEqual(expected, result);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("subtitle.json")]
        [DataRow("ftp://example.com/subtitle.json")]
        public void TryNormalizeHttpUrlRejectsUnsupportedUrls(string input)
        {
            Assert.IsFalse(PlaybackUrl.TryNormalizeHttpUrl(input, out var result));
            Assert.IsNull(result);
        }
    }
}
