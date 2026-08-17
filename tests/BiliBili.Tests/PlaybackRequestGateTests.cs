using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaybackRequestGateTests
    {
        [TestMethod]
        public void NewGenerationInvalidatesOlderRequest()
        {
            var gate = new PlaybackRequestGate();

            var first = gate.Begin();
            var second = gate.Begin();

            Assert.IsFalse(gate.IsCurrent(first));
            Assert.IsTrue(gate.IsCurrent(second));
        }

        [TestMethod]
        public void InvalidateMakesCurrentRequestStale()
        {
            var gate = new PlaybackRequestGate();
            var request = gate.Begin();

            gate.Invalidate();

            Assert.IsFalse(gate.IsCurrent(request));
        }

        [TestMethod]
        public void CurrentReturnsLatestGeneration()
        {
            var gate = new PlaybackRequestGate();

            var first = gate.Begin();
            var second = gate.Begin();

            Assert.AreNotEqual(first, gate.Current);
            Assert.AreEqual(second, gate.Current);
        }
    }
}
