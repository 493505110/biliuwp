using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaybackHistoryTests
    {
        [TestMethod]
        public void NormalVideoStoresActualProgress()
        {
            Assert.AreEqual(75, PlaybackHistory.GetStoredProgress(false, 75));
        }

        [TestMethod]
        public void InteractionVideoStoresZeroProgress()
        {
            Assert.AreEqual(0, PlaybackHistory.GetStoredProgress(true, 75));
        }
    }
}
