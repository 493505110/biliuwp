using BiliBili.UWP.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaylistModelsTests
    {
        [TestMethod]
        public void ResourceListModel_DeserializesVideoAndPageIdentifiers()
        {
            var model = JsonConvert.DeserializeObject<PlaylistResourceListModel>(
                "{\"has_more\":true,\"total_count\":354,\"media_list\":[{\"id\":117099414492917,\"bv_id\":\"BV1Baby6cEBY\",\"title\":\"蜘蛛网真的比钢铁还坚固吗？\",\"cover\":\"http://i0.hdslb.com/cover.jpg\",\"pages\":[{\"id\":40938308824,\"title\":\"蜘蛛网真的比钢铁还坚固吗？\",\"page\":1}]}]}");

            Assert.IsTrue(model.has_more);
            Assert.AreEqual(354, model.total_count);
            Assert.AreEqual(1, model.media_list.Count);
            Assert.AreEqual(117099414492917L, model.media_list[0].id);
            Assert.AreEqual("BV1Baby6cEBY", model.media_list[0].bv_id);
            Assert.AreEqual(40938308824L, model.media_list[0].pages[0].id);
        }
    }
}
