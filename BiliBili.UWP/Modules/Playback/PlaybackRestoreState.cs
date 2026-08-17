using System;

namespace BiliBili.UWP.Modules.Playback
{
    public sealed class PlaybackRestoreState
    {
        private PlaybackRestoreState(TimeSpan position, bool shouldPlay)
        {
            Position = position;
            ShouldPlay = shouldPlay;
        }

        public TimeSpan Position { get; }
        public bool ShouldPlay { get; }

        public static PlaybackRestoreState ForQualityChange(TimeSpan position, bool shouldPlay)
        {
            return new PlaybackRestoreState(position, shouldPlay);
        }

        public static PlaybackRestoreState ForContentChange()
        {
            return new PlaybackRestoreState(TimeSpan.Zero, true);
        }
    }
}
