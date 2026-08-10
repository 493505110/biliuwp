using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using BiliBili.UWP.Helper;

namespace BiliBili.Tests
{
    [TestClass]
    public class WbiEncodeHelperTests
    {
        private const string ImgKey = "7cd084941338484aae1ad9425b84077c";
        private const string SubKey = "4932caff0ff746eab6f01bf08b70ac45";
        private const string Timestamp = "1594364400";

        [TestMethod]
        public void EncWbi_已知向量_生成正确w_rid()
        {
            var p = new Dictionary<string, string> { { "foo", "114" }, { "bar", "514" }, { "zab", "1919810" } };
            var result = WbiEncodeHelper.EncWbi(p, ImgKey, SubKey, Timestamp);

            Assert.AreEqual(Timestamp, result["wts"]);
            // 与 B 站官方 Wbi 算法手工计算的基准一致
            Assert.AreEqual("67e90c7e527110b7d7462ed88d8a7e77", result["w_rid"]);
        }

        [TestMethod]
        public void EncWbi_过滤特殊字符()
        {
            var p = new Dictionary<string, string> { { "foo", "114" }, { "key", "va'l*ue(x)" } };
            var result = WbiEncodeHelper.EncWbi(p, ImgKey, SubKey, Timestamp);

            Assert.AreEqual("valuex", result["key"]);
        }

        [TestMethod]
        public void EncWbi_按key排序()
        {
            var p = new Dictionary<string, string> { { "zab", "1" }, { "aaa", "2" }, { "mid", "3" } };
            var result = WbiEncodeHelper.EncWbi(p, ImgKey, SubKey, Timestamp);

            CollectionAssert.AreEqual(
                new[] { "aaa", "mid", "wts", "zab", "w_rid" },
                new List<string>(result.Keys));
        }

        [TestMethod]
        public void EncWbi_w_rid_为32位小写hex()
        {
            var result = WbiEncodeHelper.EncWbi(new Dictionary<string, string> { { "a", "1" } }, ImgKey, SubKey, Timestamp);

            Assert.AreEqual(32, result["w_rid"].Length);
            foreach (char c in result["w_rid"])
            {
                Assert.IsTrue((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'), $"w_rid 含非法字符 {c}");
            }
        }

        [TestMethod]
        public void EncWbi_默认时间戳_为10位Unix秒()
        {
            var result = WbiEncodeHelper.EncWbi(new Dictionary<string, string> { { "a", "1" } }, ImgKey, SubKey);

            Assert.AreEqual(10, result["wts"].Length);
            long ts;
            Assert.IsTrue(long.TryParse(result["wts"], out ts));
        }
    }
}