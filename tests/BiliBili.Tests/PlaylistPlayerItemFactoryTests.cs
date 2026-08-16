using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaylistPlayerItemFactoryTests
    {
        [TestMethod]
        public void Create_UsesFirstPlayablePageAndSkipsInvalidResources()
        {
            var resources = new List<PlaylistResourceItemModel>()
            {
                new PlaylistResourceItemModel()
                {
                    id = 117099414492917,
                    index = 3,
                    title = "视频标题",
                    cover = "http://i0.hdslb.com/cover.jpg",
                    pages = new List<PlaylistResourcePageModel>()
                    {
                        new PlaylistResourcePageModel() { id = 40938308824, title = "P1 标题" }
                    }
                },
                new PlaylistResourceItemModel() { id = 100, pages = new List<PlaylistResourcePageModel>() }
            };

            var items = PlaylistPlayerItemFactory.Create(resources);

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual(117099414492917L, items[0].aid);
            Assert.AreEqual(40938308824L, items[0].cid);
            Assert.AreEqual("视频标题", items[0].title);
            Assert.AreEqual("http://i0.hdslb.com/cover.jpg", items[0].cover);
            Assert.AreEqual(3, items[0].index);
        }
    }
}
