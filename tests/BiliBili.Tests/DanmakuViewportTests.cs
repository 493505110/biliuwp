using BiliBili.UWP.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace BiliBili.Tests
{
    /// <summary>
    /// 弹幕画布的等比内容矩形计算。画布尺寸决定脚本弹幕作品的整体缩放与位置
    /// （作品的 Akari 舞台按画布尺寸 maximizeInContainer），所以这里的取值必须与
    /// 播放器 Stretch=Uniform 的渲染矩形一致，否则非 16:9 视频会整体错位。
    /// </summary>
    [TestClass]
    public class DanmakuViewportTests
    {
        private const double Tolerance = 0.0001;

        [TestMethod]
        public void WideVideoInsideTallerAreaIsLimitedByWidth()
        {
            // 16:9 放进 4:3：宽度先受限，上下留黑边。
            double width;
            double height;

            var fitted = DanmakuViewport.TryFit(1920, 1080, 800, 600, out width, out height);

            Assert.IsTrue(fitted);
            Assert.AreEqual(800d, width, Tolerance);
            Assert.AreEqual(450d, height, Tolerance);
        }

        [TestMethod]
        public void TallVideoInsideWiderAreaIsLimitedByHeight()
        {
            // 4:3 放进 16:9：高度先受限，左右留黑边（老视频的常见情形）。
            double width;
            double height;

            var fitted = DanmakuViewport.TryFit(640, 480, 1920, 1080, out width, out height);

            Assert.IsTrue(fitted);
            Assert.AreEqual(1440d, width, Tolerance);
            Assert.AreEqual(1080d, height, Tolerance);
        }

        [TestMethod]
        public void SquareVideoInsideWideAreaKeepsSquareShape()
        {
            double width;
            double height;

            var fitted = DanmakuViewport.TryFit(500, 500, 1000, 500, out width, out height);

            Assert.IsTrue(fitted);
            Assert.AreEqual(500d, width, Tolerance);
            Assert.AreEqual(500d, height, Tolerance);
        }

        [TestMethod]
        public void MatchingAspectRatioKeepsFullArea()
        {
            // av2669196 的探针画布：原舞台就是 640×360 的视频画面区，缩放 1.0 时 1:1 对齐。
            double width;
            double height;

            var fitted = DanmakuViewport.TryFit(640, 360, 640, 360, out width, out height);

            Assert.IsTrue(fitted);
            Assert.AreEqual(640d, width, Tolerance);
            Assert.AreEqual(360d, height, Tolerance);
        }

        [TestMethod]
        public void InvalidSizeIsRejectedAndOutputsZero()
        {
            // 媒体未打开时自然尺寸为 0，布局未完成时区域尺寸也可能为 0；
            // NaN/Infinity 可能来自异常的媒体元数据。这些情况都必须返回 false，
            // 由调用方保留控件现状，而不是把画布缩成 0 或负尺寸。
            var invalid = new[]
            {
                new[] { 0d, 360d, 640d, 360d },
                new[] { 640d, 0d, 640d, 360d },
                new[] { 640d, 360d, 0d, 360d },
                new[] { 640d, 360d, 640d, 0d },
                new[] { -640d, 360d, 640d, 360d },
                new[] { 640d, -360d, 640d, 360d },
                new[] { 640d, 360d, -640d, 360d },
                new[] { 640d, 360d, 640d, -360d },
                new[] { double.NaN, 360d, 640d, 360d },
                new[] { 640d, double.NaN, 640d, 360d },
                new[] { 640d, 360d, double.NaN, 360d },
                new[] { 640d, 360d, 640d, double.NaN },
                new[] { double.PositiveInfinity, 360d, 640d, 360d },
                new[] { 640d, 360d, 640d, double.PositiveInfinity }
            };

            foreach (var values in invalid)
            {
                double width = 123;
                double height = 456;

                var fitted = DanmakuViewport.TryFit(
                    values[0], values[1], values[2], values[3], out width, out height);

                Assert.IsFalse(
                    fitted,
                    string.Format(
                        "TryFit({0}, {1}, {2}, {3}) 应返回 false",
                        values[0], values[1], values[2], values[3]));
                Assert.AreEqual(0d, width, Tolerance, "失败时输出宽度应为 0");
                Assert.AreEqual(0d, height, Tolerance, "失败时输出高度应为 0");
            }
        }

        [TestMethod]
        public void FittedSizeNeverExceedsArea()
        {
            // 等比且完整可见：结果不会超出区域，也不会出现负值。
            var sizes = new[]
            {
                new[] { 1920d, 1080d },
                new[] { 640d, 480d },
                new[] { 1d, 1000d },
                new[] { 1000d, 1d }
            };
            var areas = new[]
            {
                new[] { 640d, 360d },
                new[] { 1280d, 720d },
                new[] { 300d, 900d }
            };

            foreach (var size in sizes)
            {
                foreach (var area in areas)
                {
                    double width;
                    double height;

                    Assert.IsTrue(
                        DanmakuViewport.TryFit(
                            size[0], size[1], area[0], area[1], out width, out height));

                    Assert.IsTrue(width > 0 && width <= area[0] + Tolerance, "宽度越界：" + width);
                    Assert.IsTrue(height > 0 && height <= area[1] + Tolerance, "高度越界：" + height);
                    Assert.AreEqual(
                        size[0] / size[1],
                        width / height,
                        0.001,
                        "宽高比必须与自然尺寸一致");
                }
            }
        }
    }
}
