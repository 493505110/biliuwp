using System.Linq;
using System.Xml.Linq;
using BiliBili.Background;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class DynamicFeedParserTests
    {
        private static FeedParseResult Parse()
        {
            return DynamicFeedParser.ParseFeed(TestRepository.ReadFixture("dynamic_new.json"));
        }

        [TestMethod]
        public void ParseFeed_FiltersUnsupportedCardTypes()
        {
            var result = Parse();
            Assert.AreEqual(0, result.Code);
            // Articles (type 64) and malformed cards should be skipped
            CollectionAssert.AreEqual(
                new[] { "111111", "333333", "444444", "777777" },
                result.Items.Select(x => x.DynamicId).ToArray());
        }

        [TestMethod]
        public void ParseFeed_VideoCard_UsesAidAsLaunchArgument()
        {
            var item = Parse().Items.Single(x => x.DynamicId == "111111");
            Assert.IsFalse(item.IsBangumi);
            Assert.AreEqual("222222", item.LaunchArgument);
            Assert.AreEqual("某某UP主 投稿了新视频", item.SubTitle);
        }

        [TestMethod]
        public void ParseFeed_VideoCard_FallsBackToUserProfile_WhenOwnerMissing()
        {
            var item = Parse().Items.Single(x => x.DynamicId == "777777");
            Assert.AreEqual("回退UP 投稿了新视频", item.SubTitle);
            Assert.AreEqual("888888", item.LaunchArgument);
        }

        [TestMethod]
        public void ParseFeed_BangumiCard_PrefixesSeasonIdWithBangumi()
        {
            var item = Parse().Items.Single(x => x.DynamicId == "333333");
            Assert.IsTrue(item.IsBangumi);
            // App.OnLaunched splits by comma; first segment must be "bangumi" for BanInfoPage routing
            Assert.AreEqual("bangumi,12345", item.LaunchArgument);
            Assert.AreEqual("测试番剧", item.Title);
            Assert.AreEqual("更新至第3话", item.SubTitle);
        }

        [TestMethod]
        public void ParseFeed_BangumiCard_PreservesNonNumericIndex()
        {
            var item = Parse().Items.Single(x => x.DynamicId == "444444");
            Assert.AreEqual("更新至特别篇", item.SubTitle);
        }

        [TestMethod]
        public void ParseFeed_ReturnsEmptyItems_OnApiErrorCode()
        {
            var result = DynamicFeedParser.ParseFeed("{\"code\":-101,\"message\":\"账号未登录\"}");
            Assert.AreEqual(-101, result.Code);
            Assert.AreEqual("账号未登录", result.Message);
            Assert.AreEqual(0, result.Items.Count);
        }

        [TestMethod]
        public void ParseFeed_HandlesInvalidInput_WithoutThrowing()
        {
            foreach (var json in new[] { null, "", "   ", "not json", "[]" })
            {
                var result = DynamicFeedParser.ParseFeed(json);
                Assert.AreNotEqual(0, result.Code);
                Assert.AreEqual(0, result.Items.Count);
            }
        }
    }

    [TestClass]
    public class NotificationBuilderTests
    {
        [TestMethod]
        public void EscapeXml_EscapesAllSpecialCharacters()
        {
            Assert.AreEqual("&amp;&lt;&gt;&quot;&apos;", NotificationBuilder.EscapeXml("&<>\"'"));
            Assert.AreEqual("", NotificationBuilder.EscapeXml(null));
        }

        [TestMethod]
        public void NormalizeCover_ConvertsToHttpsAndAppendsThumbnailSuffix()
        {
            // Tile images over 200KB are silently dropped by Windows; must use thumbnail params
            Assert.AreEqual("https://i0.hdslb.com/a.jpg@336w_190h_1c.jpg",
                NotificationBuilder.NormalizeCover("http://i0.hdslb.com/a.jpg"));
            Assert.AreEqual("https://i0.hdslb.com/a.jpg@336w_190h_1c.jpg",
                NotificationBuilder.NormalizeCover("//i0.hdslb.com/a.jpg"));
            // Preserves existing @ parameter
            Assert.AreEqual("https://i0.hdslb.com/a.jpg@100w.jpg",
                NotificationBuilder.NormalizeCover("https://i0.hdslb.com/a.jpg@100w.jpg"));
            Assert.AreEqual("", NotificationBuilder.NormalizeCover(null));
        }

        [TestMethod]
        public void BuildTileXml_ProducesValidXml_WithSpecialCharactersInTitle()
        {
            var item = new FeedItem()
            {
                Title = "标题里有 & 号和 <尖括号>",
                SubTitle = "某某UP主 投稿了新视频",
                Cover = "http://i0.hdslb.com/a.jpg"
            };
            // Old implementation used string.Format without escaping, causing LoadXml to throw
            var doc = XDocument.Parse(NotificationBuilder.BuildTileXml(item));
            var bindings = doc.Root.Element("visual").Elements("binding").ToArray();
            CollectionAssert.AreEqual(
                new[] { "TileMedium", "TileWide", "TileLarge" },
                bindings.Select(x => x.Attribute("template").Value).ToArray());
            Assert.AreEqual("标题里有 & 号和 <尖括号>", bindings[0].Elements("text").First().Value);
        }

        [TestMethod]
        public void BuildToastXml_BangumiCard_UsesCorrectTextAndLaunchArgument()
        {
            var item = new FeedItem()
            {
                IsBangumi = true,
                Title = "测试番剧",
                SubTitle = "更新至第3话",
                Cover = "https://i0.hdslb.com/b.jpg",
                LaunchArgument = "bangumi,12345"
            };
            var doc = XDocument.Parse(NotificationBuilder.BuildToastXml(item));
            Assert.AreEqual("bangumi,12345", doc.Root.Attribute("launch").Value);
            var texts = doc.Descendants("text").Select(x => x.Value).ToArray();
            CollectionAssert.AreEqual(new[] { "您关注的《测试番剧》", "更新至第3话" }, texts);
        }

        [TestMethod]
        public void BuildToastXml_VideoCard_UsesCorrectTextAndLaunchArgument()
        {
            var item = new FeedItem()
            {
                IsBangumi = false,
                Title = "某个视频",
                SubTitle = "某某UP主 投稿了新视频",
                Cover = "https://i0.hdslb.com/c.jpg",
                LaunchArgument = "222222"
            };
            var doc = XDocument.Parse(NotificationBuilder.BuildToastXml(item));
            Assert.AreEqual("222222", doc.Root.Attribute("launch").Value);
            var texts = doc.Descendants("text").Select(x => x.Value).ToArray();
            CollectionAssert.AreEqual(new[] { "某某UP主 投稿了新视频", "《某个视频》" }, texts);
        }

        [TestMethod]
        public void BuildTileXml_OmitsImageNode_WhenCoverIsEmpty()
        {
            var item = new FeedItem() { Title = "无封面", SubTitle = "副标题", Cover = "" };
            var doc = XDocument.Parse(NotificationBuilder.BuildTileXml(item));
            Assert.AreEqual(0, doc.Descendants("image").Count());
        }
    }
}
