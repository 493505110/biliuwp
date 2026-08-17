namespace BiliBili.UWP.Modules.Playback
{
    public static class PlaybackHistory
    {
        public static int GetStoredProgress(bool isInteraction, int progress)
        {
            return isInteraction ? 0 : progress;
        }
    }
}
