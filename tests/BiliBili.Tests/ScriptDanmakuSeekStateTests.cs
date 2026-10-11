using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class ScriptDanmakuSeekStateTests
    {
        [TestMethod]
        public void OldPositionDoesNotConsumePendingScriptSeek()
        {
            var state = new ScriptDanmakuSeekState();
            state.Begin(66, 413);
            Assert.AreEqual(ScriptSeekObservation.Pending, state.Observe(66));
            Assert.AreEqual(ScriptSeekObservation.Pending, state.Observe(66.2));
            Assert.AreEqual(413.0, state.TargetSeconds.Value, "缓冲状态同步仍应使用脚本目标");
            Assert.AreEqual(ScriptSeekObservation.Confirmed, state.Observe(413.1));
            Assert.IsNull(state.TargetSeconds);
            Assert.AreEqual(ScriptSeekObservation.None, state.Observe(413.1), "重复确认应回到普通进度同步");
        }

        [TestMethod]
        public void SupersededSeekCannotConfirmNewRequest()
        {
            var state = new ScriptDanmakuSeekState();
            state.Begin(66, 413);
            state.Begin(66, 1100);
            Assert.AreEqual(ScriptSeekObservation.Pending, state.Observe(66));
            Assert.AreEqual(ScriptSeekObservation.Pending, state.Observe(413));
            Assert.AreEqual(1100.0, state.TargetSeconds.Value);
            Assert.AreEqual(ScriptSeekObservation.Confirmed, state.Observe(1100));
        }

        [TestMethod]
        public void CrashBackwardSeekAndBgmReturnCanBothBeConfirmed()
        {
            var state = new ScriptDanmakuSeekState();
            state.Begin(1105, 366);
            Assert.AreEqual(ScriptSeekObservation.Pending, state.Observe(1105.2));
            Assert.AreEqual(ScriptSeekObservation.Confirmed, state.Observe(366));
            state.Begin(368.5, 1105);
            Assert.AreEqual(ScriptSeekObservation.Pending, state.Observe(368.5));
            Assert.AreEqual(ScriptSeekObservation.Confirmed, state.Observe(1105));
        }

        [TestMethod]
        public void UserSeekAndContentResetDoNotKeepPendingTarget()
        {
            var state = new ScriptDanmakuSeekState();
            state.Begin(66, 413);
            Assert.AreEqual(ScriptSeekObservation.None, state.Observe(10));
            Assert.IsNull(state.TargetSeconds);
            state.Begin(66, 413);
            state.Reset();
            Assert.AreEqual(ScriptSeekObservation.None, state.Observe(66));
            Assert.IsNull(state.TargetSeconds);
        }
    }
}
