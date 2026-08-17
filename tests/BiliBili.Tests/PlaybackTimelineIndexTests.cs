using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaybackTimelineIndexTests
    {
        private sealed class TimedItem
        {
            public double From { get; set; }
            public double To { get; set; }
            public string Name { get; set; }
        }

        [TestMethod]
        public void FindSupportsForwardPlaybackAndBackwardSeek()
        {
            var index = new PlaybackTimelineIndex<TimedItem>(
                new List<TimedItem>
                {
                    new TimedItem { From = 0, To = 1, Name = "first" },
                    new TimedItem { From = 3, To = 4, Name = "second" }
                },
                x => x.From,
                x => x.To);

            Assert.AreEqual("first", index.Find(0.5).Name);
            Assert.AreEqual("second", index.Find(3.5).Name);
            Assert.AreEqual("first", index.Find(0.5).Name);
            Assert.IsNull(index.Find(2));
        }

        [TestMethod]
        public void FindReturnsNullForEmptyTimeline()
        {
            var index = new PlaybackTimelineIndex<TimedItem>(
                new List<TimedItem>(),
                x => x.From,
                x => x.To);

            Assert.IsNull(index.Find(1));
        }
    }
}
