using BiliBili.UWP.Models;
using System.Collections.Generic;

namespace BiliBili.UWP.Modules
{
    public static class PlaylistCloudHistorySelector
    {
        public static int FindLatestIndex(
            IList<PlaylistHistoryCandidateModel> playlistItems,
            IList<GetHistoryModel> history)
        {
            if (playlistItems == null || history == null)
            {
                return -1;
            }

            long latestViewAt = long.MinValue;
            int latestIndex = -1;
            for (int historyIndex = 0; historyIndex < history.Count; historyIndex++)
            {
                var historyItem = history[historyIndex];
                if (historyItem == null || !long.TryParse(historyItem.aid, out long historyAid))
                {
                    continue;
                }

                for (int playlistIndex = 0; playlistIndex < playlistItems.Count; playlistIndex++)
                {
                    var playlistItem = playlistItems[playlistIndex];
                    if (playlistItem == null || playlistItem.aid != historyAid)
                    {
                        continue;
                    }

                    if (historyItem.view_at > latestViewAt)
                    {
                        latestViewAt = historyItem.view_at;
                        latestIndex = playlistIndex;
                    }
                    break;
                }
            }

            return latestIndex;
        }
    }
}
