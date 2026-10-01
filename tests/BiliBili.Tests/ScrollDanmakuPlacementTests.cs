using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSDanmaku.Helper;

namespace BiliBili.Tests
{
    [TestClass]
    public class ScrollDanmakuPlacementTests
    {
        // 保留优化前的逐候选位置全池扫描，作为碰撞规则的对照。
        private static bool OriginalFind(IList<ScrollDanmakuSpace> pool, double width,
            double height, double viewport, double middle, double available, out double y,
            Action onRead = null)
        {
            bool IsAvailable(double candidate)
            {
                foreach (var occupied in pool)
                {
                    onRead?.Invoke();
                    if (occupied.Y > candidate + height || occupied.Y + occupied.Height < candidate)
                        continue;
                    if (!(occupied.X + occupied.Width < viewport || occupied.X > viewport + width))
                        return false;
                    if (occupied.EndTime > middle) return false;
                }
                return true;
            }

            y = 0;
            if (pool.Count == 0 || IsAvailable(0)) return true;
            foreach (var occupied in pool)
            {
                y = occupied.Y + occupied.Height + 1;
                if (y + height > available) break;
                if (IsAvailable(y)) return true;
            }
            y = 0;
            return false;
        }

        [TestMethod]
        public void SingleScanMatchesOriginalAcrossMixedSizesDirectionsAndCatchUpTimes()
        {
            var random = new Random(2786505);
            for (var trial = 0; trial < 10000; trial++)
            {
                var available = random.Next(100, 1500);
                var viewport = random.Next(300, 2500);
                var width = random.Next(1, 600);
                var height = random.Next(1, available);
                var middle = random.Next(0, 100);
                var pool = Enumerable.Range(0, random.Next(0, 100)).Select(_ => new ScrollDanmakuSpace
                {
                    Y = random.Next(0, available),
                    Height = random.Next(1, 100),
                    X = random.Next(-600, viewport + 600),
                    Width = random.Next(1, 600),
                    EndTime = random.Next(0, 100)
                }).OrderBy(item => item.Y + item.Height).ToArray();
                var expected = OriginalFind(pool, width, height, viewport, middle, available, out var expectedY);
                var actual = ScrollDanmakuPlacement.TryFindPosition(pool, item => item,
                    width, height, viewport, middle, available, out var actualY);
                Assert.AreEqual(expected, actual, "trial " + trial);
                if (expected) Assert.AreEqual(expectedY, actualY, "trial " + trial);
            }
        }

        [TestMethod]
        public void DensePoolReadsEachOccupiedItemOnlyOnce()
        {
            var pool = Enumerable.Range(0, 5000).Select(index => new ScrollDanmakuSpace
            {
                Y = index * 21,
                Height = 20,
                X = 1000,
                Width = 100,
                EndTime = 10
            }).ToArray();
            var reads = 0;
            var found = ScrollDanmakuPlacement.TryFindPosition(pool, item =>
            {
                reads++;
                return item;
            }, 100, 20, 1000, 5, 200000, out var y);
            Assert.IsTrue(found);
            Assert.AreEqual(5000 * 21d, y);
            Assert.AreEqual(pool.Length, reads);
            var originalReads = 0;
            Assert.IsTrue(OriginalFind(pool, 100, 20, 1000, 5, 200000,
                out var originalY, () => originalReads++));
            Assert.AreEqual(originalY, y);
            Assert.IsTrue(originalReads > 12500000, "旧算法应覆盖密集池中的重复扫描。");
        }

        [TestMethod]
        public void TouchingEdgesAndLaterCatchUpStillBlockTheTrack()
        {
            var pool = new[] { new ScrollDanmakuSpace { Y = 0, Height = 20, X = 900, Width = 100, EndTime = 5 } };
            Assert.IsTrue(ScrollDanmakuPlacement.TryFindPosition(pool, item => item,
                100, 20, 1000, 5, 50, out var y));
            Assert.AreEqual(21d, y); // right == viewport 仍碰撞。
            pool[0].X = 899;
            Assert.IsTrue(ScrollDanmakuPlacement.TryFindPosition(pool, item => item,
                100, 20, 1000, 5, 50, out y));
            Assert.AreEqual(0d, y);
            pool[0].EndTime = 6;
            Assert.IsFalse(ScrollDanmakuPlacement.TryFindPosition(pool, item => item,
                100, 20, 1000, 5, 40, out _));
        }

        [TestMethod]
        public void RowHeightCountsPreserveDuplicateMaximumUntilLastRemoval()
        {
            var index = new DanmakuRowHeightIndex();
            index.Add(20);
            index.Add(40);
            index.Add(40);
            index.Remove(40);
            Assert.AreEqual(40d, index.Maximum);
            index.Remove(40);
            Assert.AreEqual(20d, index.Maximum);
            index.Remove(99);
            Assert.AreEqual(20d, index.Maximum);
            index.Remove(20);
            Assert.AreEqual(0d, index.Maximum);
        }

        [TestMethod]
        public void RowHeightIndexMatchesLiveItemsThroughDenseAddRemoveCycles()
        {
            var random = new Random(4000);
            var items = new List<double>();
            var index = new DanmakuRowHeightIndex();
            for (var step = 0; step < 10000; step++)
            {
                if (items.Count == 0 || random.Next(3) > 0)
                {
                    var height = random.Next(1, 100);
                    items.Add(height);
                    index.Add(height);
                }
                else
                {
                    var position = random.Next(items.Count);
                    index.Remove(items[position]);
                    items.RemoveAt(position);
                }
                Assert.AreEqual(items.Count == 0 ? 0 : items.Max(), index.Maximum);
            }
        }
    }
}
