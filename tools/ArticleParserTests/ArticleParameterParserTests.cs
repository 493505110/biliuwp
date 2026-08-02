using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArticleParserTests
{
    [TestClass]
    public class ArticleParameterParserTests
    {
        [DataTestMethod]
        [DataRow("123", 123L)]
        [DataRow("cv123", 123L)]
        [DataRow("https://www.bilibili.com/read/cv123", 123L)]
        [DataRow("https://www.bilibili.com/read/app/123", 123L)]
        [DataRow("https://www.bilibili.com/read/mobile/123", 123L)]
        [DataRow("bilibili://article/123", 123L)]
        public void TryParse_AcceptsSupportedStringForms(string parameter, long expectedArticleId)
        {
            bool parsed = ArticleParameterParser.TryParse(parameter, out long articleId);

            Assert.IsTrue(parsed);
            Assert.AreEqual(expectedArticleId, articleId);
        }

        [DataTestMethod]
        [DataRow(" cv123 ", 123L)]
        [DataRow(" 123 ", 123L)]
        public void TryParse_TrimsSurroundingWhitespace(string parameter, long expectedArticleId)
        {
            bool parsed = ArticleParameterParser.TryParse(parameter, out long articleId);

            Assert.IsTrue(parsed);
            Assert.AreEqual(expectedArticleId, articleId);
        }

        [TestMethod]
        public void TryParse_UnwrapsFirstObjectArrayItem()
        {
            bool parsed = ArticleParameterParser.TryParse(new object[] { "cv456" }, out long articleId);

            Assert.IsTrue(parsed);
            Assert.AreEqual(456L, articleId);
        }

        [TestMethod]
        public void TryParse_AcceptsPositiveNumericParameters()
        {
            Assert.IsTrue(ArticleParameterParser.TryParse(123, out long intArticleId));
            Assert.AreEqual(123L, intArticleId);
            Assert.IsTrue(ArticleParameterParser.TryParse(456L, out long longArticleId));
            Assert.AreEqual(456L, longArticleId);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        [DataRow(0L)]
        [DataRow(-1L)]
        public void TryParse_RejectsNonPositiveNumericParameters(object parameter)
        {
            bool parsed = ArticleParameterParser.TryParse(parameter, out long articleId);

            Assert.IsFalse(parsed);
            Assert.AreEqual(0L, articleId);
        }

        [DataTestMethod]
        [DataRow("-1")]
        [DataRow("9223372036854775808")]
        public void TryParse_RejectsInvalidNumericStrings(string parameter)
        {
            bool parsed = ArticleParameterParser.TryParse(parameter, out long articleId);

            Assert.IsFalse(parsed);
            Assert.AreEqual(0L, articleId);
        }

        [TestMethod]
        public void TryParse_RejectsEmptyObjectArray()
        {
            bool parsed = ArticleParameterParser.TryParse(new object[0], out long articleId);

            Assert.IsFalse(parsed);
            Assert.AreEqual(0L, articleId);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("cv0")]
        [DataRow("https://example.com/read/cv123")]
        [DataRow("not an article")]
        public void TryParse_RejectsInvalidStrings(string parameter)
        {
            bool parsed = ArticleParameterParser.TryParse(parameter, out long articleId);

            Assert.IsFalse(parsed);
            Assert.AreEqual(0L, articleId);
        }

        [TestMethod]
        public void TryParse_RejectsNull()
        {
            bool parsed = ArticleParameterParser.TryParse(null, out long articleId);

            Assert.IsFalse(parsed);
            Assert.AreEqual(0L, articleId);
        }
    }
}
