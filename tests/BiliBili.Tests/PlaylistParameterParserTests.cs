using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaylistParameterParserTests
    {
        [DataTestMethod]
        [DataRow("https://www.bilibili.com/list/94742590", 94742590L)]
        [DataRow("https://www.bilibili.com/list/ml94742590?oid=117099414492917", 94742590L)]
        [DataRow("https://www.bilibili.com/list/94742590/", 94742590L)]
        public void TryParse_AcceptsPublicPlaylistUrls(string url, long expectedPlaylistId)
        {
            bool parsed = PlaylistParameterParser.TryParse(url, out long playlistId);

            Assert.IsTrue(parsed);
            Assert.AreEqual(expectedPlaylistId, playlistId);
        }

        [DataTestMethod]
        [DataRow("https://example.com/list/94742590")]
        [DataRow("https://www.bilibili.com/video/BV1xx")]
        [DataRow("https://www.bilibili.com/list/0")]
        [DataRow("https://www.bilibili.com/list/+94742590")]
        [DataRow("https://www.bilibili.com/list/not-a-number")]
        [DataRow("")]
        public void TryParse_RejectsInvalidPlaylistUrls(string url)
        {
            bool parsed = PlaylistParameterParser.TryParse(url, out long playlistId);

            Assert.IsFalse(parsed);
            Assert.AreEqual(0L, playlistId);
        }
    }
}
