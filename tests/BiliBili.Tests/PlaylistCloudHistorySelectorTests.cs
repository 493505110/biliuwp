using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaylistCloudHistorySelectorTests
    {
        [TestMethod]
        public void FindLatestIndex_UsesNewestCloudViewTimeAmongPlaylistItems()
        {
            var items = new List<PlaylistHistoryCandidateModel>()
            {
                new PlaylistHistoryCandidateModel() { aid = 100, cid = 1001 },
                new PlaylistHistoryCandidateModel() { aid = 200, cid = 2001 },
                new PlaylistHistoryCandidateModel() { aid = 300, cid = 3001 }
            };
            var history = new List<GetHistoryModel>()
            {
                new GetHistoryModel() { aid = "100", view_at = 1700000000 },
                new GetHistoryModel() { aid = "300", view_at = 1800000000 },
                new GetHistoryModel() { aid = "200", view_at = 1750000000 }
            };

            int index = PlaylistCloudHistorySelector.FindLatestIndex(items, history);

            Assert.AreEqual(2, index);
        }

        [TestMethod]
        public void FindLatestIndex_ReturnsMinusOneWhenCloudHistoryDoesNotMatch()
        {
            var items = new List<PlaylistHistoryCandidateModel>()
            {
                new PlaylistHistoryCandidateModel() { aid = 100, cid = 1001 }
            };
            var history = new List<GetHistoryModel>()
            {
                new GetHistoryModel() { aid = "999", view_at = 1800000000 }
            };

            int index = PlaylistCloudHistorySelector.FindLatestIndex(items, history);

            Assert.AreEqual(-1, index);
        }

        [TestMethod]
        public void ExistingHistoryModel_DeserializesVideoIdentityAndViewTime()
        {
            var model = JsonConvert.DeserializeObject<GetHistoryModel>(
                "{\"code\":0,\"data\":[{\"aid\":\"123\",\"view_at\":1800000000}]}" );
            var history = JsonConvert.DeserializeObject<List<GetHistoryModel>>(model.data.ToString());

            Assert.AreEqual(1, history.Count);
            Assert.AreEqual("123", history[0].aid);
            Assert.AreEqual(1800000000L, history[0].view_at);
        }
    }
}
