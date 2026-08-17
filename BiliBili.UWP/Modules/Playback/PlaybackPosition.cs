using System;

namespace BiliBili.UWP.Modules.Playback
{
    public static class PlaybackPosition
    {
        public static TimeSpan Clamp(TimeSpan position, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            if (position <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            return position >= duration ? duration : position;
        }
    }
}
