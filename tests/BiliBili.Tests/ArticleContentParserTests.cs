using System;
using System.IO;
using System.Linq;
using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace BiliBili.Tests
{
    [TestClass]
    public class ArticleContentParserTests
    {
        [TestMethod]
        public void Parse_LegacyHtml_PreservesSupportedBlocksAndInlines()
        {
            string json = File.ReadAllText(Path.Combine("Fixtures", "type0.json"));
            ArticleDataModel article = JsonConvert.DeserializeObject<ArticleDataModel>(json);

            ArticleBlockModel[] blocks = new ArticleContentParser().Parse(article).ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    ArticleBlockType.Text,
                    ArticleBlockType.Text,
                    ArticleBlockType.Text,
                    ArticleBlockType.Text,
                    ArticleBlockType.Text,
                    ArticleBlockType.Text,
                    ArticleBlockType.Image,
                    ArticleBlockType.Separator,
                    ArticleBlockType.Text
                },
                blocks.Select(item => item.Type).ToArray());

            ArticleTextBlockModel heading = (ArticleTextBlockModel)blocks[0];
            Assert.AreEqual(ArticleTextKind.Heading, heading.Kind);
            Assert.AreEqual(2, heading.HeadingLevel);
            Assert.AreEqual("章节", JoinText(heading));

            ArticleTextBlockModel paragraph = (ArticleTextBlockModel)blocks[1];
            Assert.IsTrue(paragraph.Inlines.Any(item => item.Bold && item.Text == "加粗"));
            Assert.IsTrue(paragraph.Inlines.Any(item => item.Italic && item.Text == "斜体"));
            Assert.IsTrue(paragraph.Inlines.Any(item => item.Strike && item.Text == "删除"));
            Assert.IsTrue(paragraph.Inlines.Any(item => item.Color == "#fb7299" && item.Text == "颜色"));
            Assert.AreEqual(
                "https://www.bilibili.com/video/av1",
                paragraph.Inlines.Single(item => item.Text == "链接").Link);
            Assert.IsTrue(paragraph.Inlines.Any(item => item.Text.Contains("\n")));

            Assert.AreEqual(ArticleTextKind.Quote, ((ArticleTextBlockModel)blocks[2]).Kind);
            Assert.AreEqual(ArticleTextKind.Ordered, ((ArticleTextBlockModel)blocks[3]).Kind);
            Assert.AreEqual(1, ((ArticleTextBlockModel)blocks[3]).ListOrder);
            Assert.AreEqual(2, ((ArticleTextBlockModel)blocks[4]).ListOrder);
            Assert.AreEqual(ArticleTextKind.Bullet, ((ArticleTextBlockModel)blocks[5]).Kind);

            ArticleImageBlockModel image = (ArticleImageBlockModel)blocks[6];
            Assert.AreEqual("https://i0.hdslb.com/test.jpg", image.Url);
            Assert.AreEqual("示例图", image.Alt);
            Assert.AreEqual(640, image.Width);
            Assert.AreEqual(360, image.Height);
            Assert.AreEqual("尾段", JoinText((ArticleTextBlockModel)blocks[8]));
            Assert.IsFalse(blocks.OfType<ArticleTextBlockModel>().Any(item => JoinText(item).Contains("不能显示")));
        }

        [TestMethod]
        public void Parse_LegacyHtml_LeavesLinkNullOutsideAnchors()
        {
            ArticleDataModel article = new ArticleDataModel
            {
                type = 0,
                content = "<p>plain<a href=\"https://example.com/path\">linked</a></p>"
            };

            ArticleTextBlockModel block = (ArticleTextBlockModel)new ArticleContentParser().Parse(article).Single();

            Assert.AreEqual(2, block.Inlines.Count);
            Assert.AreEqual("plain", block.Inlines[0].Text);
            Assert.IsNull(block.Inlines[0].Link);
            Assert.AreEqual("linked", block.Inlines[1].Text);
            Assert.AreEqual("https://example.com/path", block.Inlines[1].Link);
        }

        [TestMethod]
        public void Parse_LegacyHtml_DecodesEntitiesAndMergesEquivalentInlines()
        {
            ArticleDataModel article = new ArticleDataModel
            {
                type = 0,
                content = "<p><strong>A&amp;</strong><b>B</b></p>"
            };

            ArticleTextBlockModel block = (ArticleTextBlockModel)new ArticleContentParser().Parse(article).Single();

            Assert.AreEqual(1, block.Inlines.Count);
            Assert.AreEqual("A&B", block.Inlines[0].Text);
            Assert.IsTrue(block.Inlines[0].Bold);
        }

        [TestMethod]
        public void Parse_LegacyHtml_DecodesAttributeEntities()
        {
            ArticleDataModel article = new ArticleDataModel
            {
                type = 0,
                content = "<p><a href=\"https://example.com/?a=1&amp;b=2\">linked</a></p>" +
                    "<img src=\"https://example.com/image?a=1&amp;b=2\" alt=\"A&amp;B\">"
            };

            ArticleBlockModel[] blocks = new ArticleContentParser().Parse(article).ToArray();
            ArticleTextBlockModel paragraph = (ArticleTextBlockModel)blocks[0];
            ArticleImageBlockModel image = (ArticleImageBlockModel)blocks[1];

            Assert.AreEqual("https://example.com/?a=1&b=2", paragraph.Inlines.Single().Link);
            Assert.AreEqual("https://example.com/image?a=1&b=2", image.Url);
            Assert.AreEqual("A&B", image.Alt);
        }

        [TestMethod]
        public void Parse_DeltaJson_PreservesFormattingAndEmbeds()
        {
            string json = File.ReadAllText(Path.Combine("Fixtures", "type3.json"));
            ArticleDataModel article = JsonConvert.DeserializeObject<ArticleDataModel>(json);

            ArticleBlockModel[] blocks = new ArticleContentParser().Parse(article).ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    ArticleBlockType.Text,
                    ArticleBlockType.Text,
                    ArticleBlockType.Image,
                    ArticleBlockType.Separator,
                    ArticleBlockType.Embed,
                    ArticleBlockType.Embed,
                    ArticleBlockType.Embed,
                    ArticleBlockType.Embed
                },
                blocks.Select(item => item.Type).ToArray());
            Assert.AreEqual(ArticleTextKind.Heading, ((ArticleTextBlockModel)blocks[0]).Kind);
            Assert.AreEqual(2, ((ArticleTextBlockModel)blocks[0]).HeadingLevel);
            ArticleTextBlockModel quote = (ArticleTextBlockModel)blocks[1];
            Assert.AreEqual(ArticleTextKind.Quote, quote.Kind);
            Assert.IsTrue(quote.Inlines.Single().Bold);
            Assert.AreEqual("https://www.bilibili.com/video/av1", quote.Inlines.Single().Link);
            CollectionAssert.AreEqual(
                new[] { ArticleEmbedType.Video, ArticleEmbedType.Article, ArticleEmbedType.Vote, ArticleEmbedType.Live },
                blocks.OfType<ArticleEmbedBlockModel>().Select(item => item.EmbedType).ToArray());
            Assert.AreEqual("https://www.bilibili.com/video/av1", ((ArticleEmbedBlockModel)blocks[4]).Link);
            Assert.AreEqual("https://www.bilibili.com/read/cv2", ((ArticleEmbedBlockModel)blocks[5]).Link);
            Assert.AreEqual("https://t.bilibili.com/vote/h5/index/#/result?vote_id=3", ((ArticleEmbedBlockModel)blocks[6]).Link);
            Assert.AreEqual("https://live.bilibili.com/4", ((ArticleEmbedBlockModel)blocks[7]).Link);
        }

        [TestMethod]
        public void Parse_InvalidDelta_UsesOpusParagraphFallback()
        {
            ArticleDataModel article = new ArticleDataModel
            {
                type = 3,
                content = "{",
                opus = Newtonsoft.Json.Linq.JObject.Parse(
                    "{\"content\":{\"paragraphs\":[" +
                    "{\"para_type\":1,\"text\":{\"nodes\":[{\"word\":{\"words\":\"Opus text\"}}]}}," +
                    "{\"para_type\":2,\"pic\":{\"pics\":[{\"url\":\"https://i0.hdslb.com/opus.png\",\"width\":320,\"height\":180,\"alt\":\"Opus image\"}]}}" +
                    "]}}")
            };

            ArticleBlockModel[] blocks = new ArticleContentParser().Parse(article).ToArray();

            CollectionAssert.AreEqual(
                new[] { ArticleBlockType.Text, ArticleBlockType.Image },
                blocks.Select(item => item.Type).ToArray());
            Assert.AreEqual("Opus text", JoinText((ArticleTextBlockModel)blocks[0]));
            Assert.AreEqual("https://i0.hdslb.com/opus.png", ((ArticleImageBlockModel)blocks[1]).Url);
        }

        [TestMethod]
        public void Parse_Type4Article_UsesOpusParagraphs()
        {
            ArticleDataModel article = new ArticleDataModel
            {
                type = 4,
                opus = Newtonsoft.Json.Linq.JObject.Parse(
                    "{\"content\":{\"paragraphs\":[" +
                    "{\"para_type\":1,\"text\":{\"nodes\":[{\"word\":{\"words\":\"Type 4 text\"}}]}}," +
                    "{\"para_type\":2,\"pic\":{\"pics\":[{\"url\":\"https://i0.hdslb.com/type4.png\",\"width\":640,\"height\":360}]}}" +
                    "]}}")
            };

            ArticleBlockModel[] blocks = new ArticleContentParser().Parse(article).ToArray();

            CollectionAssert.AreEqual(
                new[] { ArticleBlockType.Text, ArticleBlockType.Image },
                blocks.Select(item => item.Type).ToArray());
            Assert.AreEqual("Type 4 text", JoinText((ArticleTextBlockModel)blocks[0]));
            Assert.AreEqual("https://i0.hdslb.com/type4.png", ((ArticleImageBlockModel)blocks[1]).Url);
        }

        [TestMethod]
        public void Parse_UnknownInsert_ProducesUnknownBlock()
        {
            ArticleDataModel article = new ArticleDataModel
            {
                type = 3,
                content = "{\"ops\":[{\"insert\":{\"unsupported-card\":{\"id\":\"1\"}}}]}"
            };

            ArticleUnknownBlockModel block = (ArticleUnknownBlockModel)new ArticleContentParser().Parse(article).Single();

            StringAssert.Contains(block.Description, "unsupported-card");
        }

        [TestMethod]
        public void Parse_MalformedOpusParagraph_AddsUnknownAndContinues()
        {
            ArticleDataModel article = new ArticleDataModel
            {
                type = 3,
                content = "{",
                opus = Newtonsoft.Json.Linq.JObject.Parse(
                    "{\"content\":{\"paragraphs\":[" +
                    "{\"para_type\":1}," +
                    "{\"para_type\":1,\"text\":{\"nodes\":[{\"word\":{\"words\":\"after\"}}]}}" +
                    "]}}")
            };

            ArticleBlockModel[] blocks = new ArticleContentParser().Parse(article).ToArray();

            CollectionAssert.AreEqual(
                new[] { ArticleBlockType.Unknown, ArticleBlockType.Text },
                blocks.Select(item => item.Type).ToArray());
            Assert.AreEqual("after", JoinText((ArticleTextBlockModel)blocks[1]));
        }

        [TestMethod]
        public void Parse_RejectsUnsupportedArticleType()
        {
            ArticleDataModel article = new ArticleDataModel { type = 9, content = "x" };

            FormatException exception = Assert.ThrowsException<FormatException>(
                () => new ArticleContentParser().Parse(article));

            StringAssert.Contains(exception.Message, "9");
        }

        private static string JoinText(ArticleTextBlockModel block)
        {
            return string.Concat(block.Inlines.Select(item => item.Text));
        }
    }
}
