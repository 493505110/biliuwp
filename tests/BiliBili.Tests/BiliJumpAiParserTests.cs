using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace BiliBili.Tests
{
    [TestClass]
    public class BiliJumpAiParserTests
    {
        [TestMethod]
        public void TryParse_HandlesMarkdownAndMergesAdjacentSegments()
        {
            var json = "\x60\x60\x60json\n{\"ads\":[" +
                "{\"start_time\":\"10\",\"end_time\":20,\"product_name\":\"A\",\"ad_content\":\"one\"}," +
                "{\"start_time\":20.5,\"end_time\":30,\"product_name\":\"B\",\"ad_content\":\"two\"}]," +
                "\"msg\":\"识别到广告\"}\n\x60\x60\x60";

            var success = BiliJumpAiParser.TryParse(json, 60, out var result, out var error);

            Assert.IsTrue(success, error);
            Assert.AreEqual(1, result.ads.Count);
            Assert.AreEqual(10, result.ads[0].start_time, 0.001);
            Assert.AreEqual(30, result.ads[0].end_time, 0.001);
            StringAssert.Contains(result.ads[0].product_name, "A");
            StringAssert.Contains(result.ads[0].product_name, "B");
        }

        [TestMethod]
        public void NormalizeSegments_ClampsToDurationAndDropsInvalidItems()
        {
            var result = BiliJumpAiParser.NormalizeSegments(new List<BiliJumpAdSegment>
            {
                new BiliJumpAdSegment { start_time = -5, end_time = 10 },
                new BiliJumpAdSegment { start_time = 90, end_time = 120 },
                new BiliJumpAdSegment { start_time = 40, end_time = 40 }
            }, 100);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(0, result[0].start_time, 0.001);
            Assert.AreEqual(100, result[1].end_time, 0.001);
        }

        [TestMethod]
        public void BuildSubtitleText_UsesStableTimestampFormat()
        {
            var text = BiliJumpAiParser.BuildSubtitleText("测试视频", new[]
            {
                new BiliJumpSubtitleLine { From = 1.2, To = 3.4, Content = "你好" }
            });

            StringAssert.Contains(text, "标题: 测试视频");
            StringAssert.Contains(text, "1.20 --> 3.40");
            StringAssert.Contains(text, "你好");
        }
    }
}
