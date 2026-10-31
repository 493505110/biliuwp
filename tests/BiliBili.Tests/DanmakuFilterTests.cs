using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class DanmakuFilterTests
    {
        [TestMethod]
        public void KeywordsAreLiteralCaseInsensitiveAndEmptyRulesDoNotBlock()
        {
            var rules = new DanmakuFilterRules("|开始游戏|A.B||", null, null);
            Assert.AreEqual(2, rules.GetBlockType("开始游戏！", "u", "", 0, true));
            Assert.AreEqual(2, rules.GetBlockType("a.b", "u", "", 0, true));
            Assert.IsNull(rules.GetBlockType("aXb", "u", "", 0, true));
            Assert.IsNull(new DanmakuFilterRules("", "", "").GetBlockType("开始游戏", "", "", 0, true));
        }

        [TestMethod]
        public void UserRulesMatchSenderExactlyAndWorkWithoutKeywords()
        {
            var rules = new DanmakuFilterRules("", "|deadbeef||", "");
            Assert.AreEqual(1, rules.GetBlockType("hello", "deadbeef", "", 0, true));
            Assert.IsNull(rules.GetBlockType("deadbeef", "other", "", 0, true));
            Assert.IsNull(rules.GetBlockType("hello", "deadbeef2", "", 0, true));
        }

        [TestMethod]
        public void SpecialPoolsBypassWordsButStillRespectModeSwitches()
        {
            var rules = new DanmakuFilterRules("bad", "u", ".*");
            foreach (var pool in new[] { 1, 2 })
            {
                Assert.IsNull(rules.GetBlockType("bad", "u", "", pool, true));
                Assert.AreEqual(4, rules.GetBlockType("bad", "u", "", pool, false));
            }
        }

        [TestMethod]
        public void RegexKeepsSourceMatchingAndInvalidExpressionDoesNotBlock()
        {
            var rules = new DanmakuFilterRules(null, null, "sender=blocked");
            Assert.AreEqual(2, rules.GetBlockType("hello", "u", "sender=blocked", 0, true));
            Assert.IsNull(rules.GetBlockType("sender=blocked", "u", "other", 0, true));
            Assert.IsNull(new DanmakuFilterRules(null, null, "[").GetBlockType(null, null, null, 0, true));
        }

        [TestMethod]
        public void DensityUsesOriginalSpeedFormulaAndMinimumOne()
        {
            Assert.AreEqual(16, DanmakuDensityFilter.GetWindowLimit(80, 5)); // speede=1.2
            Assert.AreEqual(32, DanmakuDensityFilter.GetWindowLimit(80, 2.5)); // speede=2.4
            Assert.AreEqual(1, DanmakuDensityFilter.GetWindowLimit(1, 10));
        }

        [TestMethod]
        public void DensitySharesScrollDirectionsAndIgnoresFixedModesAndSpecialPools()
        {
            var filter = new DanmakuDensityFilter();
            Assert.IsTrue(filter.TryAccept(1, 0, 10, 5, 100));
            Assert.IsTrue(filter.TryAccept(6, 0, 10, 5, 101));
            Assert.IsFalse(filter.TryAccept(2, 0, 10, 5, 102));
            foreach (var mode in new[] { 4, 5, 7 }) Assert.IsTrue(filter.TryAccept(mode, 0, 10, 5, 103));
            Assert.IsTrue(filter.TryAccept(1, 1, 10, 5, 104));
            Assert.IsTrue(filter.TryAccept(1, 2, 10, 5, 104));
            Assert.IsFalse(filter.TryAccept(3, 0, 10, 5, 105));
        }

        [TestMethod]
        public void DensityWindowUsesElapsedRealTimeWithOriginalStrictBoundary()
        {
            var filter = new DanmakuDensityFilter();
            Assert.IsTrue(filter.TryAccept(1, 0, 5, 5, 250));
            // 即使媒体时间已经跨整秒、seek 或倍速，只要真实时间窗口没结束就不能补发。
            Assert.IsFalse(filter.TryAccept(1, 0, 5, 5, 300));
            Assert.IsFalse(filter.TryAccept(1, 0, 5, 5, 1250));
            Assert.IsTrue(filter.TryAccept(1, 0, 5, 5, 1251));
            Assert.IsFalse(filter.TryAccept(1, 0, 5, 5, 2250));
            Assert.IsTrue(filter.TryAccept(1, 0, 5, 5, 10251));
            Assert.IsFalse(filter.TryAccept(1, 0, 5, 5, 11250));
            Assert.IsTrue(filter.TryAccept(1, 0, 5, 5, 11251));
        }

        [TestMethod]
        public void DensitySettingAndSpeedChangesApplyWithoutResettingTheWindow()
        {
            var filter = new DanmakuDensityFilter();
            Assert.IsTrue(filter.TryAccept(1, 0, 5, 5, 100));
            Assert.IsFalse(filter.TryAccept(1, 0, 5, 5, 101));
            Assert.IsTrue(filter.TryAccept(1, 0, 5, 2, 102));
            Assert.IsFalse(filter.TryAccept(1, 0, 5, 2, 103));
            Assert.IsTrue(filter.TryAccept(1, 0, 0, 5, 104));
            Assert.IsFalse(filter.TryAccept(1, 0, 5, 5, 105));
        }
    }
}
