using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaylistPaginationTests
    {
        [TestMethod]
        public void TryGetNextCursor_UsesLastResourceCursor()
        {
            var resources = new List<PlaylistResourceItemModel>()
            {
                new PlaylistResourceItemModel() { id = 100, bv_id = "BVfirst" },
                new PlaylistResourceItemModel() { id = 200, bv_id = "BVlast" }
            };

            bool hasCursor = PlaylistPagination.TryGetNextCursor(resources, "", 0, out string bvid, out long oid);

            Assert.IsTrue(hasCursor);
            Assert.AreEqual("BVlast", bvid);
            Assert.AreEqual(200L, oid);
        }

        [TestMethod]
        public void TryGetNextCursor_RejectsMissingOrRepeatedCursor()
        {
            bool missingCursor = PlaylistPagination.TryGetNextCursor(
                new List<PlaylistResourceItemModel>(), "", 0, out _, out _);
            bool repeatedCursor = PlaylistPagination.TryGetNextCursor(
                new List<PlaylistResourceItemModel>()
                {
                    new PlaylistResourceItemModel() { id = 200, bv_id = "BVlast" }
                }, "BVlast", 200, out _, out _);

            Assert.IsFalse(missingCursor);
            Assert.IsFalse(repeatedCursor);
        }
    }
}
