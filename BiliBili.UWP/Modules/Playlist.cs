using BiliBili.UWP.Api;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Models;
using BiliBili.UWP.Pages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BiliBili.UWP.Modules
{
    public class Playlist : IModules
    {
        private const int PageSize = 20;
        private const int MaxPageCount = 100;
        private readonly PlaylistAPI playlistAPI = new PlaylistAPI();

        public async Task<ReturnModel<List<PlayerModel>>> GetPlayerList(long playlistId)
        {
            try
            {
                List<PlayerModel> playerList = new List<PlayerModel>();
                string bvid = "";
                long oid = 0;
                bool completed = false;

                for (int page = 0; page < MaxPageCount; page++)
                {
                    var results = await playlistAPI.ResourceList(playlistId, bvid, oid, PageSize).Request();
                    if (!results.status)
                    {
                        return new ReturnModel<List<PlayerModel>>()
                        {
                            success = false,
                            message = results.message
                        };
                    }

                    var data = await results.GetData<PlaylistResourceListModel>();
                    if (data == null || !data.success || data.data == null)
                    {
                        return new ReturnModel<List<PlayerModel>>()
                        {
                            success = false,
                            message = data?.message ?? "播放列表读取失败"
                        };
                    }

                    var resources = data.data.media_list ?? new List<PlaylistResourceItemModel>();
                    playerList.AddRange(ToPlayerModels(resources));
                    if (!data.data.has_more)
                    {
                        completed = true;
                        break;
                    }

                    if (!PlaylistPagination.TryGetNextCursor(resources, bvid, oid, out string nextBvid, out long nextOid))
                    {
                        return new ReturnModel<List<PlayerModel>>()
                        {
                            success = false,
                            message = "播放列表分页游标无效"
                        };
                    }

                    oid = nextOid;
                    bvid = nextBvid;
                }

                if (!completed)
                {
                    return new ReturnModel<List<PlayerModel>>()
                    {
                        success = false,
                        message = "播放列表未完整加载"
                    };
                }

                if (playerList.Count == 0)
                {
                    return new ReturnModel<List<PlayerModel>>()
                    {
                        success = false,
                        message = "播放列表为空"
                    };
                }

                return new ReturnModel<List<PlayerModel>>()
                {
                    success = true,
                    data = playerList
                };
            }
            catch (Exception ex)
            {
                return HandelError<List<PlayerModel>>(ex);
            }
        }

        private static List<PlayerModel> ToPlayerModels(List<PlaylistResourceItemModel> resources)
        {
            List<PlayerModel> playerList = new List<PlayerModel>();
            foreach (var item in PlaylistPlayerItemFactory.Create(resources))
            {
                playerList.Add(new PlayerModel()
                {
                    Aid = item.aid.ToString(),
                    Mid = item.cid.ToString(),
                    Mode = PlayMode.Video,
                    No = (item.index + 1).ToString(),
                    ImageSrc = item.cover,
                    Title = "播放列表",
                    VideoTitle = item.title
                });
            }

            return playerList;
        }
    }
}
