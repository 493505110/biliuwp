using BiliBili.UWP.Modules;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class ScriptDanmakuDiagnosticsTests
    {
        [TestMethod]
        public void LogsKeepTheLatest500EntriesAcrossBatchesWithoutDeduplication()
        {
            var diagnostics = new ScriptDanmakuDiagnostics();
            diagnostics.RecordLogs(new ScriptDanmakuLogEventArgs(Enumerable.Range(0, 400)
                .Select(index => new ScriptDanmakuLogEntry(index, "123", "line" + index)).ToArray()));
            diagnostics.RecordLogs(new ScriptDanmakuLogEventArgs(Enumerable.Range(400, 200)
                .Select(index => new ScriptDanmakuLogEntry(index, "123", "line" + index)).ToArray()));

            Assert.AreEqual(500, diagnostics.LogCount);
            Assert.IsTrue(diagnostics.LogText.StartsWith("[00:01:40.000] [脚本 123] line100"));
            Assert.IsTrue(diagnostics.LogText.EndsWith("line599"));
            var repeated = new ScriptDanmakuLogEntry(600, "123", "repeat");
            diagnostics.RecordLogs(new ScriptDanmakuLogEventArgs(new[] { repeated, repeated }));
            Assert.AreEqual(500, diagnostics.LogCount);
            Assert.AreEqual(2, diagnostics.LogText.Split('\n').Count(line => line.EndsWith("repeat")));
        }

        [TestMethod]
        public void ScriptClearRemovesLogsAndPreservesErrorSummary()
        {
            var diagnostics = new ScriptDanmakuDiagnostics();
            diagnostics.Record(new ScriptDanmakuErrorEventArgs("runtime", "123", "error"));
            diagnostics.RecordLogs(new ScriptDanmakuLogEventArgs(new[] { new ScriptDanmakuLogEntry(0, "123", "old") }));
            diagnostics.RecordLogs(new ScriptDanmakuLogEventArgs(null, isClear: true));
            Assert.AreEqual(0, diagnostics.LogCount);
            Assert.AreEqual(string.Empty, diagnostics.LogText);
            Assert.AreEqual(1, diagnostics.Count);

            diagnostics.RecordLogs(new ScriptDanmakuLogEventArgs(new[] { new ScriptDanmakuLogEntry(1, "123", "new") }));
            Assert.AreEqual(1, diagnostics.LogCount);
            diagnostics.Clear();
            Assert.AreEqual(0, diagnostics.LogCount);
            Assert.AreEqual(0, diagnostics.Count);
        }

        [TestMethod]
        public void LogFormattingHandlesPlaybackHoursAndInvalidPositions()
        {
            Assert.AreEqual("[01:01:01.250] [脚本 123] hello",
                new ScriptDanmakuLogEntry(3661.25, "123", "hello").Description);
            foreach (var position in new[] { double.NaN, double.PositiveInfinity, -1d })
                Assert.AreEqual("[00:00:00.000] hello", new ScriptDanmakuLogEntry(position, null, "hello").Description);
            Assert.IsFalse(string.IsNullOrEmpty(new ScriptDanmakuLogEntry(double.MaxValue, null, null).Description));
        }

        [TestMethod]
        public void NullLogBatchesAndEntriesAreIgnored()
        {
            var diagnostics = new ScriptDanmakuDiagnostics();
            diagnostics.RecordLogs(null);
            diagnostics.RecordLogs(new ScriptDanmakuLogEventArgs(null));
            diagnostics.RecordLogs(new ScriptDanmakuLogEventArgs(new ScriptDanmakuLogEntry[] { null }));
            Assert.AreEqual(0, diagnostics.LogCount);
        }

        [TestMethod]
        public void RepeatedScriptErrorIsCountedOnce()
        {
            var diagnostics = new ScriptDanmakuDiagnostics();
            diagnostics.Record(new ScriptDanmakuErrorEventArgs("runtime", "123", "undefined value"));
            diagnostics.Record(new ScriptDanmakuErrorEventArgs(" Runtime ", "123", "undefined value"));

            Assert.AreEqual(1, diagnostics.Count);
        }

        [TestMethod]
        public void DifferentStageScriptOrMessageProducesDistinctErrors()
        {
            var diagnostics = new ScriptDanmakuDiagnostics();
            diagnostics.Record(new ScriptDanmakuErrorEventArgs("compile", "123", "invalid token"));
            diagnostics.Record(new ScriptDanmakuErrorEventArgs("runtime", "123", "invalid token"));
            diagnostics.Record(new ScriptDanmakuErrorEventArgs("compile", "456", "invalid token"));
            diagnostics.Record(new ScriptDanmakuErrorEventArgs("compile", "123", "unexpected end"));

            Assert.AreEqual(4, diagnostics.Count);
        }

        [TestMethod]
        public void RepeatedErrorStillBecomesTheLatestError()
        {
            var diagnostics = new ScriptDanmakuDiagnostics();
            diagnostics.Record(new ScriptDanmakuErrorEventArgs("compile", "123", "unexpected end"));
            diagnostics.Record(new ScriptDanmakuErrorEventArgs("runtime", "456", "undefined value"));
            diagnostics.Record(new ScriptDanmakuErrorEventArgs("compile", "123", "unexpected end"));

            Assert.AreEqual(2, diagnostics.Count);
            Assert.AreEqual("123", diagnostics.LatestError.ItemId);
            StringAssert.Contains(diagnostics.LatestError.Description, "阶段：编译");
            StringAssert.Contains(diagnostics.LatestError.Description, "脚本 ID：123");
            StringAssert.Contains(diagnostics.LatestError.Description, "原因：unexpected end");
        }

        [TestMethod]
        public void ClearResetsCountLatestErrorAndDeduplication()
        {
            var diagnostics = new ScriptDanmakuDiagnostics();
            var error = new ScriptDanmakuErrorEventArgs("composite", "123", "bad tween");
            diagnostics.Record(error);
            diagnostics.Clear();

            Assert.AreEqual(0, diagnostics.Count);
            Assert.IsNull(diagnostics.LatestError);
            diagnostics.Record(error);
            Assert.AreEqual(1, diagnostics.Count);
            StringAssert.Contains(diagnostics.LatestError.Description, "阶段：合成");
        }

        [TestMethod]
        public void HostAndIncompleteErrorsHaveReadableDetails()
        {
            var diagnostics = new ScriptDanmakuDiagnostics();
            diagnostics.Record(null);
            Assert.AreEqual(0, diagnostics.Count);

            var hostError = new ScriptDanmakuErrorEventArgs("host", null, "页面加载失败");
            Assert.AreEqual("阶段：宿主\n原因：页面加载失败", hostError.Description);
            var unknownError = new ScriptDanmakuErrorEventArgs(null, null, null);
            Assert.AreEqual("阶段：未知\n原因：", unknownError.Description);
        }
    }
}
