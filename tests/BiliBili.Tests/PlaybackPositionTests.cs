using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaybackPositionTests
    {
        [TestMethod]
        public void ClampKeepsPositionWithinDuration()
        {
            var duration = TimeSpan.FromSeconds(100);

            Assert.AreEqual(TimeSpan.Zero, PlaybackPosition.Clamp(TimeSpan.FromSeconds(-2), duration));
            Assert.AreEqual(duration, PlaybackPosition.Clamp(TimeSpan.FromSeconds(120), duration));
            Assert.AreEqual(TimeSpan.FromSeconds(42), PlaybackPosition.Clamp(TimeSpan.FromSeconds(42), duration));
        }

        [TestMethod]
        public void ClampReturnsZeroForEmptyDuration()
        {
            Assert.AreEqual(TimeSpan.Zero, PlaybackPosition.Clamp(TimeSpan.FromSeconds(20), TimeSpan.Zero));
        }
    }
}
