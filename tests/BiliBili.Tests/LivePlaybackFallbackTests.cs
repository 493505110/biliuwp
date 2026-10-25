using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace BiliBili.Tests
{
    [TestClass]
    public class LivePlaybackFallbackTests
    {
        private sealed class Source : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() { Disposed = true; }
        }

        [TestMethod]
        public async Task FailedFlvLinesFallBackToHlsAndTransferOwnership()
        {
            var fallback = new LivePlaybackFallback(new[] { "flv1", "flv2", "hls" }, "flv1");
            var attempts = new List<string>();
            var source = new Source();
            var result = await fallback.OpenNextAsync(url => url == "hls" ? Task.FromResult(source)
                : Task.FromException<Source>(new Exception("HTTP 404")), () => true, attempts.Add);

            CollectionAssert.AreEqual(new[] { "flv1", "flv2", "hls" }, attempts);
            Assert.AreSame(source, result);
            Assert.IsFalse(source.Disposed);
        }

        [TestMethod]
        public async Task ManualSelectionComesFirstAndEachCandidateIsTriedOnlyOnce()
        {
            var fallback = new LivePlaybackFallback(new[] { "one", "two", "one", "", "three" }, "two");
            var attempts = new List<string>();
            var error = new COMException("HTTP 404", unchecked((int)0x80004005));

            Exception final = null;
            try
            {
                await fallback.OpenNextAsync(url => Task.FromException<Source>(error), () => true, attempts.Add);
            }
            catch (Exception ex) { final = ex; }

            CollectionAssert.AreEqual(new[] { "two", "one", "three" }, attempts);
            Assert.IsNotNull(final);
            Assert.AreEqual(error.HResult, final.HResult);
            StringAssert.Contains(final.Message, "HTTP 404");
            StringAssert.Contains(final.Message, "3");
        }

        [TestMethod]
        public async Task MediaPlayerFailureContinuesAfterPreviouslyOpenedSource()
        {
            var fallback = new LivePlaybackFallback(new[] { "one", "two" }, "one");
            var attempts = new List<string>();
            var first = await fallback.OpenNextAsync(url => Task.FromResult(new Source()), () => true, attempts.Add);
            first.Dispose();
            fallback.ReportFailure(new Exception("MediaPlayer 解码失败"));
            var second = await fallback.OpenNextAsync(url => Task.FromResult(new Source()), () => true, attempts.Add);

            CollectionAssert.AreEqual(new[] { "one", "two" }, attempts);
            Assert.IsFalse(second.Disposed);
        }

        [TestMethod]
        public async Task StaleSuccessfulOpenIsDisposedWithoutTryingAnotherLine()
        {
            var gate = new PlaybackRequestGate();
            int request = gate.Begin();
            var pending = new TaskCompletionSource<Source>();
            var fallback = new LivePlaybackFallback(new[] { "old", "fallback" }, "old");
            var attempts = new List<string>();
            var opening = fallback.OpenNextAsync(url => pending.Task, () => gate.IsCurrent(request), attempts.Add);

            gate.Begin();
            var source = new Source();
            pending.SetResult(source);

            Assert.IsNull(await opening);
            Assert.IsTrue(source.Disposed);
            CollectionAssert.AreEqual(new[] { "old" }, attempts);
        }

        [TestMethod]
        public async Task StaleFailureDoesNotStartAnotherOpenOrReportFinalError()
        {
            bool current = true;
            var pending = new TaskCompletionSource<Source>();
            var fallback = new LivePlaybackFallback(new[] { "old", "fallback" }, "old");
            var attempts = new List<string>();
            var opening = fallback.OpenNextAsync(url => pending.Task, () => current, attempts.Add);

            current = false;
            pending.SetException(new Exception("old failure"));

            Assert.IsNull(await opening);
            CollectionAssert.AreEqual(new[] { "old" }, attempts);
        }

        [TestMethod]
        public async Task SelectionChangeDuringNotificationCancelsBeforeNativeOpen()
        {
            bool current = true;
            bool opened = false;
            var fallback = new LivePlaybackFallback(new[] { "old" }, "old");
            var result = await fallback.OpenNextAsync(url => { opened = true; return Task.FromResult(new Source()); },
                () => current, url => current = false);

            Assert.IsNull(result);
            Assert.IsFalse(opened);
        }

        [TestMethod]
        public async Task NullSourceFallsBackInsteadOfEndingPlaybackSetup()
        {
            var fallback = new LivePlaybackFallback(new[] { "empty", "valid" }, "empty");
            var source = new Source();
            var result = await fallback.OpenNextAsync(url => Task.FromResult(url == "empty" ? null : source), () => true, url => { });
            Assert.AreSame(source, result);
            Assert.AreEqual(2, fallback.AttemptCount);
        }
    }
}
