using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace BiliBili.Tests
{
    [TestClass]
    public class PlaybackCdnPolicyTests
    {
        [DataTestMethod]
        [DataRow("https://xy1.mcdn.bilivideo.cn:8082/v1/resource/a")]
        [DataRow("https://mcdn.bilivideo.com/v1/resource/a")]
        [DataRow("https://xy1.mcdn.bilivideo.net/a")]
        [DataRow("https://XY1.MCDN.BILIVIDEO.CN.:8082/a")]
        [DataRow("https://node.szbdyd.com/a")]
        [DataRow("https://edge.mountaintoys.cn/a")]
        [DataRow("https://node.nexusedgeio.com/a")]
        [DataRow("https://node.ahdohpiechei.com/a")]
        [DataRow("https://upos-sz-302ppio.bilivideo.com/a")]
        [DataRow("https://upos-sz-302kodo.bilivideo.com/a")]
        [DataRow("https://upos-sz-mirror14b.bilivideo.com/a")]
        [DataRow("https://upos-sz-mirrorali.bilivideo.com/a?token=x&os=mcdn&expires=1")]
        [DataRow("https://upos-sz-mirrorali.bilivideo.com/a?%6Fs=%6Dcdn")]
        [DataRow("http://192.0.2.1:8082/v1/resource/a")]
        [DataRow("http://[2001:db8::1]:8082/v2/resource/a")]
        public void RecognizesKnownPcdnAddresses(string url)
        {
            Assert.IsTrue(PlaybackCdnPolicy.IsPcdnUrl(url));
        }

        [DataTestMethod]
        [DataRow("https://upos-sz-mirrorali.bilivideo.com/upgcxcode/a?os=upos")]
        [DataRow("https://upos-sz-mirrorcos.bilivideo.com/a")]
        [DataRow("https://cn-sdjn-cm-02-01.bilivideo.com/a")]
        [DataRow("https://upos-hz-mirrorakam.akamaized.net/a")]
        [DataRow("https://live.bilivideo.com/live.flv")]
        [DataRow("https://192.0.2.1/a")]
        [DataRow("https://cdn.example/a/mcdn.bilivideo.cn?token=szbdyd.com")]
        [DataRow("https://cdn.example/a?myos=mcdn&os=upos&token=os%3Dmcdn")]
        [DataRow("https://mcdn.bilivideo.cn.example/a")]
        [DataRow("https://notszbdyd.com/a")]
        [DataRow("https://szbdyd.com@cdn.example/a")]
        [DataRow("https://cdn.example/a#os=mcdn")]
        [DataRow("not-a-url")]
        [DataRow(null)]
        public void DoesNotTreatRegularCdnOrUrlTextAsPcdn(string url)
        {
            Assert.IsFalse(PlaybackCdnPolicy.IsPcdnUrl(url));
        }

        [TestMethod]
        public void SelectsFirstAllowedBackupWithoutChangingSignature()
        {
            const string signed = "https://upos-sz-mirrorcos.bilivideo.com/a?token=a%2fb%2Bc&x=1&x=2";
            var backups = new[] { "invalid", "https://node.szbdyd.com/a", signed, "https://other.example/a" };
            Assert.AreEqual(signed, DashStreamSelector.ResolvePlayableUrl("https://node.mcdn.bilivideo.cn/a", backups, true));
            Assert.AreEqual(signed, PlaybackCdnPolicy.RequireUrl("https://node.mcdn.bilivideo.cn/a", backups, true));
        }

        [TestMethod]
        public void BlockingNeverReturnsTheLastPcdnAsFallback()
        {
            const string primary = "https://node.mcdn.bilivideo.cn/a";
            var backups = new[] { "https://node.szbdyd.com/a" };
            Assert.IsNull(DashStreamSelector.ResolvePlayableUrl(primary, backups, true));
            var error = Assert.ThrowsException<PcdnBlockedException>(() => PlaybackCdnPolicy.RequireUrl(primary, backups, true));
            Assert.AreEqual(PlaybackCdnPolicy.BlockedMessage, error.Message);
        }

        [TestMethod]
        public void DisabledPolicyPreservesExistingPrimaryAndBackupBehavior()
        {
            const string primary = "https://node.mcdn.bilivideo.cn/a";
            Assert.AreEqual(primary, DashStreamSelector.ResolvePlayableUrl(primary, new[] { "https://cdn.example/a" }));
            Assert.AreEqual(primary, PlaybackCdnPolicy.RequireUrl(primary, null, false));
            Assert.AreEqual(primary, DashStreamSelector.ResolvePlayableUrl("invalid", new[] { primary }));
        }

        [TestMethod]
        public void InvalidAddressesDoNotMasqueradeAsPolicyRejection()
        {
            Assert.IsNull(PlaybackCdnPolicy.SelectUrl("invalid", new[] { "file:///a", "ftp://cdn.example/a" }, true));
            var error = Assert.ThrowsException<InvalidOperationException>(() => PlaybackCdnPolicy.RequireUrl("invalid", null, true));
            Assert.IsFalse(error is PcdnBlockedException);
        }
    }
}
