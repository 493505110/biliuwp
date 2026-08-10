using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using BiliBili.UWP.Models;

namespace ArticleParserTests
{
    [TestClass]
    public class ModelsSerializationTests
    {
        [TestMethod]
        public void LoginModel_反序列化()
        {
            var json = "{\"message\":\"ok\",\"access_token\":\"abc123\",\"refresh_token\":\"ref456\",\"mid\":\"12345\",\"code\":0}";
            var m = JsonConvert.DeserializeObject<LoginModel>(json);

            Assert.AreEqual("ok", m.message);
            Assert.AreEqual("abc123", m.access_token);
            Assert.AreEqual("ref456", m.refresh_token);
            Assert.AreEqual("12345", m.mid);
            Assert.AreEqual(0, m.code);
        }

        [TestMethod]
        public void LoginModel_code非零_可为负数()
        {
            var json = "{\"code\":-352,\"message\":\"风控\",\"access_token\":null}";
            var m = JsonConvert.DeserializeObject<LoginModel>(json);

            Assert.AreEqual(-352, m.code);
            Assert.IsNull(m.access_token);
        }

        [TestMethod]
        public void HomeRefreshModel_反序列化()
        {
            var json = "{\"code\":0,\"message\":\"0\",\"data\":[{\"title\":\"测试1\",\"param\":\"123\"},{\"title\":\"测试2\",\"param\":\"456\"}]}";
            var m = JsonConvert.DeserializeObject<HomeRefreshModel>(json);

            Assert.AreEqual(0, m.code);
            Assert.AreEqual(2, m.data.Count);
            Assert.AreEqual("测试1", m.data[0].title);
            Assert.AreEqual("456", m.data[1].param);
        }

        [TestMethod]
        public void BannerModel_反序列化()
        {
            var json = "{\"top\":[{\"id\":1,\"title\":\"横幅\",\"image\":\"a.jpg\",\"hash\":\"h1\",\"uri\":\"bilibili://x\",\"is_ad\":false}]}";
            var m = JsonConvert.DeserializeObject<BannerModel>(json);

            Assert.AreEqual(1, m.top.Count);
            Assert.AreEqual("横幅", m.top[0].title);
            Assert.AreEqual("a.jpg", m.top[0].image);
            Assert.IsFalse(m.top[0].is_ad);
        }

        [TestMethod]
        public void MessageFeedUnreadModel_反序列化()
        {
            var json = "{\"at\":5,\"recv_like\":10,\"recv_reply\":2,\"sys_msg\":1}";
            var m = JsonConvert.DeserializeObject<MessageFeedUnreadModel>(json);

            Assert.AreEqual(5, m.at);
            Assert.AreEqual(10, m.recv_like);
            Assert.AreEqual(2, m.recv_reply);
            Assert.AreEqual(1, m.sys_msg);
        }

        [TestMethod]
        public void bodyModel_cover_读取时拼接后缀()
        {
            // 反序列化只设置 _cover，getter 在读取时拼接 @300w.jpg
            var json = "{\"title\":\"视频\",\"cover\":\"http://x/cover.jpg\",\"param\":\"42\"}";
            var m = JsonConvert.DeserializeObject<bodyModel>(json);

            Assert.AreEqual("http://x/cover.jpg@300w.jpg", m.cover);
            Assert.AreEqual("42", m.param);
        }
    }
}