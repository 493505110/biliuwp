using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using BiliBili.UWP.Helper;

namespace ArticleParserTests
{
    [TestClass]
    public class SignHelperTests
    {
        [TestMethod]
        public void SignQuery_已知向量()
        {
            Assert.AreEqual("2f5345af3e24fcc92083b8cb295c09fa", SignHelper.SignQuery("foo=114&bar=514", "secret"));
            Assert.AreEqual("7b380597bdecaeaa1a71eb1b54c8affb", SignHelper.SignQuery("a=1&b=2", "myscrete123"));
        }

        [TestMethod]
        public void SignUrl_提取参数排序签名()
        {
            var url = "https://api.bilibili.com/x/v2?a=1&b=2&c=3";
            Assert.AreEqual("&sign=b330f36944e275a904d9640f47e0765d", SignHelper.SignUrl(url, "appsecret"));
        }

        [TestMethod]
        public void SignParameters_字典按key排序签名()
        {
            var pars = new Dictionary<string, string> { { "a", "1" }, { "c", "3" }, { "b", "2" } };
            // 排序后 a=1&b=2&c=3，与 SignUrl 相同输入应得相同签名
            Assert.AreEqual("&sign=b330f36944e275a904d9640f47e0765d", SignHelper.SignParameters(pars, "appsecret"));
        }

        [TestMethod]
        public void SignParameters_空字典不抛异常()
        {
            var result = SignHelper.SignParameters(new Dictionary<string, string>(), "s");
            Assert.IsTrue(result.StartsWith("&sign="));
        }

        [TestMethod]
        public void SignUrl_参数顺序无关_签名一致()
        {
            var url1 = "https://api.bilibili.com/x?a=1&b=2&c=3";
            var url2 = "https://api.bilibili.com/x?c=3&b=2&a=1";
            Assert.AreEqual(SignHelper.SignUrl(url1, "sec"), SignHelper.SignUrl(url2, "sec"));
        }
    }
}