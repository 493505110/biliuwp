using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSDanmaku.Helper;
using System;
using System.Globalization;
using System.Linq;

namespace BiliBili.Tests
{
    [TestClass]
    public class PositionDanmakuTests
    {
        private static PositionDanmaku Parse(string json)
        {
            Assert.IsTrue(PositionDanmaku.TryParse(json, out var result), json);
            return result;
        }

        [TestMethod]
        public void StaticCommentPreservesRawJsonAndSupportsSingleAlphaAndNewlines()
        {
            const string json = "[341,219,\"0.5\",4,\"a/n中文\\nb\"]";
            var item = Parse(json);
            var state = item.Evaluate(2, 682, 438);
            Assert.AreEqual("a\n中文\nb", item.Text);
            Assert.AreEqual(341, state.X, 1e-9);
            Assert.AreEqual(219, state.Y, 1e-9);
            Assert.AreEqual(0.5, state.Opacity, 1e-9);
            Assert.AreEqual(item.Text, Parse(json).Text);
            Assert.IsTrue(item.HasOutline);
        }

        [TestMethod]
        public void TranslationHasIndependentDelayDurationAndLifetime()
        {
            var item = Parse("[0,100,\"1-0\",5,\"移动\",0,0,400,200,2000,1000]");
            var waiting = item.Evaluate(0.5, 682, 438);
            Assert.AreEqual(0, waiting.X, 1e-9);
            Assert.AreEqual(100, waiting.Y, 1e-9);
            var middle = item.Evaluate(2, 682, 438);
            Assert.AreEqual(200, middle.X, 1e-9);
            Assert.AreEqual(150, middle.Y, 1e-9);
            Assert.AreEqual(0.6, middle.Opacity, 1e-9);
            Assert.AreEqual(400, item.Evaluate(4, 682, 438).X, 1e-9);
            Assert.IsTrue(item.Evaluate(4.99, 682, 438).Visible);
            Assert.IsFalse(item.Evaluate(5, 682, 438).Visible);
            Assert.IsFalse(item.Evaluate(-1, 682, 438).Visible);
        }

        [TestMethod]
        public void ZeroDurationMovesInstantlyAfterDelay()
        {
            var item = Parse("[10,20,\"1-1\",4,\"瞬移\",0,0,50,60,0,1000]");
            Assert.AreEqual(10, item.Evaluate(0.99, 682, 438).X, 1e-9);
            Assert.AreEqual(50, item.Evaluate(1, 682, 438).X, 1e-9);
        }

        [TestMethod]
        public void PartialOptionalFieldsAndLegacyFullWidthZeroAreAccepted()
        {
            var item = Parse("[0,0,\"1-1\",4,\"兼容\",\"\",null,100,200,\"\",\"０\"]");
            Assert.AreEqual(50, item.Evaluate(2, 682, 438).X, 1e-9);
            Assert.AreEqual(100, item.Evaluate(2, 682, 438).Y, 1e-9);
            Assert.AreEqual(0, Parse("[0,0,\"1\",4,\"兼容\",0]").RotateY);
        }

        [TestMethod]
        public void CoordinateTypeAndEachAxisAreHandledIndependently()
        {
            var item = Parse("[0.5,219,\"1\",4,\"坐标\",0,0,1.0,\"0.5\",2000,0]");
            var first = item.Evaluate(0, 1364, 876);
            Assert.AreEqual(682, first.X, 1e-9);
            Assert.AreEqual(438, first.Y, 1e-9);
            Assert.AreEqual(2, first.Scale, 1e-9);
            Assert.AreEqual(1364, item.Evaluate(2, 1364, 876).X, 1e-9);
            Assert.AreEqual(1, Parse("[1,1,\"1\",4,\"整数\"]").Evaluate(0, 682, 438).X, 1e-9);
        }

        [TestMethod]
        public void FontStyleAndRotationsAreParsedWithoutMutatingInput()
        {
            var item = Parse("[0,0,\"1\",4,\"字体\",30,-45,0,0,0,0,true,\"幼圆\",1]");
            Assert.AreEqual("幼圆", item.FontFamily);
            Assert.IsFalse(item.HasOutline);
            Assert.AreEqual(30, item.RotateZ);
            Assert.AreEqual(-45, item.RotateY);
            Assert.IsTrue(Parse("[0,0,\"1\",4,\"字体\",0,0,0,0,0,0,false]").HasOutline);
        }

        [DataTestMethod]
        [DataRow("0", 75.0)]
        [DataRow("1", 50.0)]
        [DataRow("\"0\"", 75.0)]
        public void InterpolationMatchesBilibiliLinearAndQuadraticEaseOut(string easing, double expected)
        {
            var item = Parse("[0,0,\"1\",4,\"补间\",0,0,100,0,2000,0,false,\"\"," + easing + "]");
            Assert.AreEqual(expected, item.Evaluate(1, 682, 438).X, 1e-9);
        }

        [TestMethod]
        public void PathTimingFollowsLengthAndDelayAndOverridesEndpoints()
        {
            var item = Parse("[500,500,\"1-0\",6,\"路径\",0,0,600,600,4000,1000,false,\"\",0,\"M0,0L100,0L100,300\"]");
            Assert.AreEqual(0, item.Evaluate(0.5, 682, 438).X, 1e-9);
            Assert.AreEqual(100, item.Evaluate(2, 682, 438).X, 1e-9);
            Assert.AreEqual(0, item.Evaluate(2, 682, 438).Y, 1e-9);
            Assert.AreEqual(100, item.Evaluate(3, 682, 438).Y, 1e-9);
            Assert.AreEqual(300, item.Evaluate(5, 682, 438).Y, 1e-9);
            Assert.AreEqual(200, item.Evaluate(3, 1364, 876).Y, 1e-9);
        }

        [DataTestMethod]
        [DataRow("M-10.5,2 L-10.5,2 L89.5,2", 39.5, 2.0)]
        [DataRow("m-10.5,2 l100,0", 39.5, 2.0)]
        [DataRow("M0,0 L0,0 L0,0", 0.0, 0.0)]
        [DataRow("M0,0 L1e2,0", 50.0, 0.0)]
        public void PathsSupportDecimalsNegativeCoordinatesAndZeroLengthSegments(string path, double x, double y)
        {
            var array = JArray.Parse("[0,0,\"1\",4,\"路径\",0,0,0,0,2000,0,false,\"\",0]");
            array.Add(path);
            var state = Parse(array.ToString()).Evaluate(1, 682, 438);
            Assert.AreEqual(x, state.X, 1e-9);
            Assert.AreEqual(y, state.Y, 1e-9);
        }

        [DataTestMethod]
        [DataRow("not json")]
        [DataRow("[]")]
        [DataRow("[0,0,\"1\",0,\"零寿命\"]")]
        [DataRow("[0,0,\"1\",-1,\"负寿命\"]")]
        [DataRow("[\"NaN\",0,\"1\",4,\"非有限\"]")]
        [DataRow("[0,0,\"1\",\"Infinity\",\"非有限\"]")]
        [DataRow("[null,0,\"1\",4,\"空坐标\"]")]
        [DataRow("[0,0,\"oops\",4,\"透明度\"]")]
        [DataRow("[0,0,\"1\",4,\"负时长\",0,0,0,0,-1,0]")]
        [DataRow("[0,0,\"1\",4,\"负延迟\",0,0,0,0,1,-1]")]
        [DataRow("[0,0,\"1\",4,\"非法路径\",0,0,0,0,1,0,false,\"\",0,\"M0,0C1,2,3,4\"]")]
        [DataRow("[0,0,\"1\",4,\"非法路径\",0,0,0,0,1,0,false,\"\",0,\"M0,0L1\"]")]
        [DataRow("[0,0,\"1\",4,\"非法路径\",0,0,0,0,1,0,false,\"\",0,\"M0,0L1,1garbage\"]")]
        public void InvalidDataIsRejectedWithoutThrowing(string json)
        {
            Assert.IsFalse(PositionDanmaku.TryParse(json, out _));
        }

        [TestMethod]
        public void ParsingIsIndependentOfSystemCulture()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                var state = Parse("[0.5,0.5,\"0.1-0.9\",2.5,\"小数\"]").Evaluate(1.25, 682, 438);
                Assert.AreEqual(341, state.X, 1e-9);
                Assert.AreEqual(0.5, state.Opacity, 1e-9);
                Assert.AreEqual(0.5, Parse("[0,0,0.5,4,\"数字透明度\"]").Evaluate(1, 682, 438).Opacity, 1e-9);
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }

        [TestMethod]
        public void SeekRestoresAllOverlappingCommentsAndReplayKeepsOrder()
        {
            var items = new[] { new[] { 10.0, 10 }, new[] { 12.0, 1 }, new[] { 5.0, 20 }, new[] { 15.0, 3 } };
            var index = new PositionDanmakuIndex<double[]>(items, item => item[0], item => item[1]);
            CollectionAssert.AreEqual(new[] { 5.0, 10.0, 15.0 }, index.ActiveAt(15).Select(item => item[0]).ToArray());
            CollectionAssert.AreEqual(new[] { 5.0 }, index.ActiveAt(20).Select(item => item[0]).ToArray());
            Assert.AreEqual(0, index.ActiveAt(30).Count);
            CollectionAssert.AreEqual(new[] { 5.0, 10.0, 12.0 }, index.ActiveAt(12).Select(item => item[0]).ToArray());
            Assert.AreEqual(0, index.ActiveAt(double.NaN).Count);
        }

        [TestMethod]
        public void MidpointRecoveryAndResizeAreDeterministic()
        {
            var item = Parse("[0,0,\"0-1\",10,\"恢复\",0,0,682,438,10000,0]");
            var middle = item.Evaluate(5, 682, 438);
            var resized = item.Evaluate(5, 1364, 876);
            Assert.AreEqual(341, middle.X, 1e-9);
            Assert.AreEqual(219, middle.Y, 1e-9);
            Assert.AreEqual(0.5, middle.Opacity, 1e-9);
            Assert.AreEqual(middle.X * 2, resized.X, 1e-9);
            Assert.AreEqual(68.2, item.Evaluate(1, 682, 438).X, 1e-9);
            Assert.IsFalse(item.Evaluate(1, 0, 438).Visible);
        }

        [TestMethod]
        public void FiniteLargeLifetimeAndCoordinatesAvoidIntermediateOverflow()
        {
            var item = Parse("[\"1e305\",0,\"1\",1e308,\"边界\"]");
            Assert.AreEqual(1e308, item.MoveDuration);
            var state = item.Evaluate(1, 682, 438);
            Assert.IsTrue(state.Visible);
            Assert.IsFalse(double.IsInfinity(state.X));
            Assert.IsFalse(item.Evaluate(1, double.MaxValue, 438).Visible);
        }

        [TestMethod]
        public void PlaybackClockTracksRatePauseBufferingAndBackwardSeek()
        {
            var clock = new PositionDanmakuClock();
            clock.Synchronize(10, true, 2, 100);
            Assert.AreEqual(12, clock.Read(101), 1e-9);
            clock.Pause(101);
            Assert.AreEqual(12, clock.Read(200), 1e-9);
            clock.Resume(200);
            Assert.AreEqual(14, clock.Read(201), 1e-9);
            clock.Synchronize(5, false, 0.5, 202);
            Assert.AreEqual(5, clock.Read(300), 1e-9);
            clock.Resume(300);
            Assert.AreEqual(6, clock.Read(302), 1e-9);
            clock.Synchronize(20, true, 1, 303);
            Assert.AreEqual(21, clock.Read(304), 1e-9);
        }

        [DataTestMethod]
        [DataRow(0.0)]
        [DataRow(-1.0)]
        [DataRow(double.NaN)]
        [DataRow(double.PositiveInfinity)]
        public void InvalidRateFallsBackToNormalPlayback(double rate)
        {
            var clock = new PositionDanmakuClock();
            clock.Synchronize(10, true, rate, 0);
            Assert.AreEqual(11, clock.Read(1), 1e-9);
        }

        [TestMethod]
        public void IntervalIndexMatchesBruteForceAcrossDenseOverlapsAndSeeks()
        {
            var random = new Random(7);
            var items = Enumerable.Range(0, 1000).Select(_ => new[] { random.NextDouble() * 200, random.NextDouble() * 50 + 0.1 }).ToArray();
            var index = new PositionDanmakuIndex<double[]>(items, item => item[0], item => item[1]);
            foreach (var time in Enumerable.Range(0, 100).Select(_ => random.NextDouble() * 260))
            {
                var expected = items.Where(item => item[0] <= time && item[0] + item[1] > time).OrderBy(item => item[0]).ToArray();
                CollectionAssert.AreEqual(expected, index.ActiveAt(time).ToArray());
            }
        }
    }
}
