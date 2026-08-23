using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaybackEventTimelineTests
    {
        private sealed class TimedItem
        {
            public double Time { get; set; }
            public string Name { get; set; }
        }

        [TestMethod]
        public void AdvanceDeliversEveryEventCrossedWithinOneTick()
        {
            var timeline = CreateTimeline(
                new TimedItem { Time = 0.05, Name = "first" },
                new TimedItem { Time = 0.12, Name = "second" },
                new TimedItem { Time = 0.19, Name = "third" });

            AssertItems(timeline.Advance(0).Items);
            var batch = timeline.Advance(0.2);

            Assert.IsFalse(batch.WasDiscontinuity);
            AssertItems(batch.Items, "first", "second", "third");
        }

        [TestMethod]
        public void AdvanceDoesNotRepeatEventsAtTheSamePosition()
        {
            var timeline = CreateTimeline(new TimedItem { Time = 1, Name = "only" });

            AssertItems(timeline.Advance(1).Items, "only");
            AssertItems(timeline.Advance(1).Items);
            AssertItems(timeline.Advance(1.0000001).Items);
        }

        [TestMethod]
        public void LargeForwardAndBackwardSeeksOnlyEmitEventsAtTheTarget()
        {
            var timeline = CreateTimeline(
                new TimedItem { Time = 2, Name = "backward-target" },
                new TimedItem { Time = 8, Name = "skipped" },
                new TimedItem { Time = 12, Name = "forward-target" });

            AssertItems(timeline.Advance(0).Items);
            var forward = timeline.Advance(12);
            Assert.IsTrue(forward.WasDiscontinuity);
            AssertItems(forward.Items, "forward-target");

            var backward = timeline.Advance(2);
            Assert.IsTrue(backward.WasDiscontinuity);
            AssertItems(backward.Items, "backward-target");
        }

        private static PlaybackEventTimeline<TimedItem> CreateTimeline(params TimedItem[] items)
        {
            return new PlaybackEventTimeline<TimedItem>(items, item => item.Time);
        }

        private static void AssertItems(IReadOnlyList<TimedItem> items, params string[] expected)
        {
            CollectionAssert.AreEqual(expected, items.Select(item => item.Name).ToArray());
        }
    }
}
