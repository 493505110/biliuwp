using System;
using System.Collections.Generic;

namespace BiliBili.UWP.Modules.Playback
{
    public enum ScriptSeekObservation { None, Pending, Confirmed }

    /// <summary>区分脚本定位的异步确认与定位前、被后续请求取代的旧进度。</summary>
    public sealed class ScriptDanmakuSeekState
    {
        private const double PositionToleranceSeconds = 1.25;
        private readonly List<double> stalePositions = new List<double>();
        public double? TargetSeconds { get; private set; }

        public void Begin(double sourceSeconds, double targetSeconds)
        {
            stalePositions.Add(sourceSeconds);
            if (TargetSeconds.HasValue) stalePositions.Add(TargetSeconds.Value);
            TargetSeconds = targetSeconds;
        }

        public ScriptSeekObservation Observe(double positionSeconds)
        {
            if (!TargetSeconds.HasValue) return ScriptSeekObservation.None;
            if (Math.Abs(positionSeconds - TargetSeconds.Value) <= PositionToleranceSeconds)
            {
                Reset();
                return ScriptSeekObservation.Confirmed;
            }
            foreach (var stale in stalePositions)
            {
                if (Math.Abs(positionSeconds - stale) <= PositionToleranceSeconds)
                    return ScriptSeekObservation.Pending;
            }
            // 非旧进度、也非脚本目标的定位按用户手动定位处理。
            Reset();
            return ScriptSeekObservation.None;
        }

        public void Reset()
        {
            TargetSeconds = null;
            stalePositions.Clear();
        }
    }
}
