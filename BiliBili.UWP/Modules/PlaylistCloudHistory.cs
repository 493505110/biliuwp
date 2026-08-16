using BiliBili.UWP.Api;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BiliBili.UWP.Modules
{
    public class PlaylistCloudHistory
    {
        private const int PageSize = 30;
        private const int MaxPageCount = 100;
        private readonly HistoryAPI historyAPI = new HistoryAPI();

        public async Task<int> GetLatestIndex(IList<PlayerModel> playerList)
        {
            if (playerList == null || playerList.Count == 0)
            {
                return 0;
            }

            var candidates = new List<PlaylistHistoryCandidateModel>();
            foreach (var item in playerList)
            {
                candidates.Add(new PlaylistHistoryCandidateModel()
                {
                    aid = ParseId(item?.Aid),
                    cid = ParseId(item?.Mid)
                });
            }

            try
            {
                for (int page = 0; page < MaxPageCount; page++)
                {
                    var history = await historyAPI.GetHistory(page + 1, PageSize);
                    if (history == null || history.Count == 0)
                    {
                        return 0;
                    }

                    int index = PlaylistCloudHistorySelector.FindLatestIndex(candidates, history);
                    if (index >= 0)
                    {
                        return index;
                    }

                    if (history.Count < PageSize)
                    {
                        return 0;
                    }
                }
            }
            catch
            {
                return 0;
            }

            return 0;
        }

        private static long ParseId(string value)
        {
            return long.TryParse(value, out long id) ? id : 0;
        }
    }
}
