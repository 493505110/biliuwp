using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSDanmaku.Helper;

namespace BiliBili.Tests
{
    [TestClass]
    public class DanmakuRasterCacheTests
    {
        [TestMethod]
        public void HitKeepsFrequentTextureWhileCountLimitEvictsOldest()
        {
            var cache = new WeightedLruCache<string, object>(2, 1024);
            var shared = new object();
            cache.Add("first", shared, 100);
            cache.Add("second", new object(), 100);
            Assert.IsTrue(cache.TryGet("first", out var actual));
            Assert.AreSame(shared, actual);
            cache.Add("third", new object(), 100);
            Assert.IsFalse(cache.TryGet("second", out _));
            Assert.IsTrue(cache.TryGet("first", out _));
        }

        [TestMethod]
        public void PixelBudgetEvictsMultipleEntriesAndOversizedTextureIsNotRetained()
        {
            var cache = new WeightedLruCache<int, string>(128, 1000);
            cache.Add(1, "one", 400);
            cache.Add(2, "two", 400);
            cache.Add(3, "three", 700);
            Assert.AreEqual(1, cache.Count);
            Assert.AreEqual(700L, cache.Weight);
            cache.Add(4, "oversized", 1001);
            Assert.IsFalse(cache.TryGet(4, out _));
            Assert.AreEqual(700L, cache.Weight);
        }

        [TestMethod]
        public void ReplaceAndClearDoNotLeaveStaleWeights()
        {
            var cache = new WeightedLruCache<int, string>(2, 1000);
            cache.Add(1, "old", 800);
            cache.Add(1, "new", 200);
            cache.Add(2, "other", 700);
            Assert.AreEqual(900L, cache.Weight);
            Assert.IsTrue(cache.TryGet(1, out var value));
            Assert.AreEqual("new", value);
            cache.Clear();
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0L, cache.Weight);
            Assert.IsFalse(cache.TryGet(1, out _));
        }
    }
}
