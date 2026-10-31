using System;

namespace BiliBili.UWP.Modules.Playback
{
    /// <summary>原版默认 densityControl=true 的真实时间限流，不修改弹幕池或 blocked。</summary>
    public sealed class DanmakuDensityFilter
    {
        private double? windowStart;
        private int count;

        public static int GetWindowLimit(int density, double referenceDuration)
        {
            // 原版 floor(density * (0.5 * speede) / 3)；UWP 的参考时长为 6 / speede。
            if (double.IsNaN(referenceDuration) || double.IsInfinity(referenceDuration) || referenceDuration <= 0)
                referenceDuration = 5;
            return Math.Max(1, (int)Math.Floor(Math.Max(0, density) / referenceDuration));
        }

        public bool TryAccept(int mode, int pool, int density, double referenceDuration, double nowMilliseconds)
        {
            // ScrollCommentManager（含逆向滚动）才限流，非普通池直接放行。
            if (density <= 0 || pool != 0 || (mode != 1 && mode != 2 && mode != 3 && mode != 6)) return true;
            if (!windowStart.HasValue) windowStart = nowMilliseconds;
            var elapsed = nowMilliseconds - windowStart.Value;
            // 与 DensityFilter.validateLive 一致：恰好 1000ms 时仍属于旧窗口。
            if (elapsed > 1000)
            {
                windowStart += (Math.Ceiling(elapsed / 1000) - 1) * 1000;
                count = 0;
            }
            if (count >= GetWindowLimit(density, referenceDuration)) return false;
            count++;
            return true;
        }
    }
}
