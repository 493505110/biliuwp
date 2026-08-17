using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaybackRestoreStateTests
    {
        [TestMethod]
        public void QualityChangePreservesPositionAndPlaybackState()
        {
            var position = TimeSpan.FromSeconds(42);

            var playing = PlaybackRestoreState.ForQualityChange(position, true);
            var paused = PlaybackRestoreState.ForQualityChange(position, false);

            Assert.AreEqual(position, playing.Position);
            Assert.IsTrue(playing.ShouldPlay);
            Assert.AreEqual(position, paused.Position);
            Assert.IsFalse(paused.ShouldPlay);
        }

        [TestMethod]
        public void ContentChangeRestartsAndPlays()
        {
            var state = PlaybackRestoreState.ForContentChange();

            Assert.AreEqual(TimeSpan.Zero, state.Position);
            Assert.IsTrue(state.ShouldPlay);
        }
    }
}
