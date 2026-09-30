using BiliBili.UWP.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace BiliBili.Tests
{
    /// <summary>
    /// 开屏图选取逻辑的契约测试。用例形态取自 app.bilibili.com/x/v2/splash/brand/list 的真实响应
    /// （2026-09-25 实测：list 22 张候选图，show[0].id=144 对应「2026中秋」，duration=1000）。
    /// </summary>
    [TestClass]
    public class SplashImageSelectorTests
    {
        private const string ListJson = @"[
            {""id"":35,""thumb"":""http://i0.hdslb.com/bfs/feed-admin/aaa.jpg"",""thumb_name"":""妇女节""},
            {""id"":144,""thumb"":""https://i0.hdslb.com/bfs/splash/bbb.webp"",""thumb_name"":""2026中秋""},
            {""id"":31,""thumb"":""http://i0.hdslb.com/bfs/feed-admin/ccc.jpg"",""thumb_name"":""万圣节""}
        ]";

        private static JArray CandidateList
        {
            get { return JArray.Parse(ListJson); }
        }

        [TestMethod]
        public void SelectThumb_按当前投放id取出对应图片()
        {
            var show = JArray.Parse(@"[{""id"":144,""duration"":1000}]");
            Assert.AreEqual("https://i0.hdslb.com/bfs/splash/bbb.webp",
                SplashImageSelector.SelectThumb(show, CandidateList));
        }

        [TestMethod]
        public void SelectThumb_无投放时返回null()
        {
            Assert.IsNull(SplashImageSelector.SelectThumb(new JArray(), CandidateList));
            Assert.IsNull(SplashImageSelector.SelectThumb(null, CandidateList));
        }

        [TestMethod]
        public void SelectThumb_列表里没有该id时返回null()
        {
            var show = JArray.Parse(@"[{""id"":999}]");
            Assert.IsNull(SplashImageSelector.SelectThumb(show, CandidateList));
        }

        [TestMethod]
        public void SelectThumb_候选列表为null时返回null()
        {
            var show = JArray.Parse(@"[{""id"":144}]");
            Assert.IsNull(SplashImageSelector.SelectThumb(show, null));
        }

        [TestMethod]
        public void SelectThumb_只认show的第一项()
        {
            var show = JArray.Parse(@"[{""id"":144},{""id"":35}]");
            Assert.AreEqual("https://i0.hdslb.com/bfs/splash/bbb.webp",
                SplashImageSelector.SelectThumb(show, CandidateList));
        }

        [TestMethod]
        public void SelectShowId_空或缺失返回负一()
        {
            Assert.AreEqual(-1L, SplashImageSelector.SelectShowId(new JArray()));
            Assert.AreEqual(-1L, SplashImageSelector.SelectShowId(null));
            Assert.AreEqual(-1L, SplashImageSelector.SelectShowId(JArray.Parse(@"[{}]")));
        }

        [TestMethod]
        public void SelectDurationMs_取首项时长_缺失返回零()
        {
            Assert.AreEqual(1000,
                SplashImageSelector.SelectDurationMs(JArray.Parse(@"[{""id"":144,""duration"":1000}]")));
            Assert.AreEqual(0, SplashImageSelector.SelectDurationMs(JArray.Parse(@"[{""id"":144}]")));
            Assert.AreEqual(0, SplashImageSelector.SelectDurationMs(new JArray()));
        }

        [TestMethod]
        public void EnsureJpegUrl_webp交给CDN转jpeg()
        {
            Assert.AreEqual("https://i0.hdslb.com/bfs/splash/bbb.webp@1080w.jpg",
                SplashImageSelector.EnsureJpegUrl("https://i0.hdslb.com/bfs/splash/bbb.webp"));
        }

        [TestMethod]
        public void EnsureJpegUrl_大写后缀同样识别()
        {
            Assert.AreEqual("a.WEBP@1080w.jpg", SplashImageSelector.EnsureJpegUrl("a.WEBP"));
        }

        [TestMethod]
        public void EnsureJpegUrl_非webp原样返回()
        {
            const string jpg = "http://i0.hdslb.com/bfs/feed-admin/aaa.jpg";
            Assert.AreEqual(jpg, SplashImageSelector.EnsureJpegUrl(jpg));
            Assert.AreEqual("", SplashImageSelector.EnsureJpegUrl(""));
            Assert.IsNull(SplashImageSelector.EnsureJpegUrl(null));
        }

        [TestMethod]
        public void EnsureJpegUrl_已带CDN后缀的地址不再追加()
        {
            // 该地址以 .jpg 结尾，不满足 .webp 结尾的判定，应原样返回
            Assert.AreEqual("a.webp@1080w.jpg", SplashImageSelector.EnsureJpegUrl("a.webp@1080w.jpg"));
        }

        [TestMethod]
        public void NormalizeDurationMs_服务端给1000时抬到下限()
        {
            // 实测服务端 duration=1000，直接照搬会一闪而过
            Assert.AreEqual(3000, SplashImageSelector.NormalizeDurationMs(1000, 3000, 5000));
        }

        [TestMethod]
        public void NormalizeDurationMs_超上限时截断()
        {
            Assert.AreEqual(5000, SplashImageSelector.NormalizeDurationMs(99999, 3000, 5000));
        }

        [TestMethod]
        public void NormalizeDurationMs_区间内原样返回()
        {
            Assert.AreEqual(4000, SplashImageSelector.NormalizeDurationMs(4000, 3000, 5000));
            Assert.AreEqual(3000, SplashImageSelector.NormalizeDurationMs(3000, 3000, 5000));
            Assert.AreEqual(5000, SplashImageSelector.NormalizeDurationMs(5000, 3000, 5000));
        }

        [TestMethod]
        public void NormalizeDurationMs_上下限颠倒时返回下限()
        {
            Assert.AreEqual(3000, SplashImageSelector.NormalizeDurationMs(1000, 3000, 1000));
        }
    }
}
