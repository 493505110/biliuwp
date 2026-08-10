using BiliBili.UWP.Api;
using BiliBili.UWP.Api.Live;
using BiliBili.UWP.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace BiliBili.UWP.Modules
{
    public class LiveArea : IModules
    {
        public async Task<ReturnModel<List<AreaList>>> GetAreaList()
        {
            try
            {
                var response = await LiveRoomAPI.GetAreaList().Request();
                var root = response.GetJObject();
                if (!response.status || root == null)
                {
                    return Failure<List<AreaList>>(response.message);
                }
                if (root.Value<int?>("code") != 0)
                {
                    return Failure<List<AreaList>>(GetMessage(root));
                }

                var areas = root["data"]?.ToObject<List<AreaList>>() ?? new List<AreaList>();
                foreach (var item in areas.SelectMany(x => x.list ?? new List<AreaListItem>()))
                {
                    item.pic = ToHttps(item.pic);
                }
                return new ReturnModel<List<AreaList>> { success = true, data = areas };
            }
            catch (Exception ex)
            {
                return HandelError<List<AreaList>>(ex);
            }
        }

        public async Task<ReturnModel<AreaRoomList>> GetRoomList(
            int areaId,
            int parentAreaId,
            int page,
            string sortType = "")
        {
            try
            {
                var response = await LiveRoomAPI.GetAreaRooms(parentAreaId, areaId, page, sortType).Request();
                return ParseRoomList(response);
            }
            catch (Exception ex)
            {
                return HandelError<AreaRoomList>(ex);
            }
        }

        public async Task<ReturnModel<AreaRoomList>> GetAllRoomList(int page, bool latest)
        {
            try
            {
                var response = await LiveRoomAPI.GetAllRooms(page, latest).Request();
                return ParseRoomList(response);
            }
            catch (Exception ex)
            {
                return HandelError<AreaRoomList>(ex);
            }
        }

        private static ReturnModel<AreaRoomList> ParseRoomList(HttpResults response)
        {
            var root = response.GetJObject();
            if (!response.status || root == null)
            {
                return Failure<AreaRoomList>(response.message);
            }
            if (root.Value<int?>("code") != 0)
            {
                return Failure<AreaRoomList>(GetMessage(root));
            }

            var data = root["data"] as JObject;
            var source = data?["list"] as JArray ?? new JArray();
            var list = new ObservableCollection<RoomListItem>(source.OfType<JObject>().Select(MapRoom));
            return new ReturnModel<AreaRoomList>
            {
                success = true,
                data = new AreaRoomList
                {
                    count = data?.Value<int?>("count") ?? list.Count,
                    has_more = data?.Value<int?>("has_more") ?? (list.Count > 0 ? 1 : 0),
                    list = list,
                    banner = data?["banner"]?.ToObject<List<AreaRoomListBannerItem>>()
                        ?? new List<AreaRoomListBannerItem>(),
                    new_tags = data?["new_tags"]?.ToObject<List<new_tags>>() ?? new List<new_tags>()
                }
            };
        }

        private static RoomListItem MapRoom(JObject item)
        {
            return new RoomListItem
            {
                roomid = item.Value<int?>("roomid") ?? 0,
                cover = ToHttps(item.Value<string>("cover")
                    ?? item.Value<string>("user_cover")
                    ?? item.Value<string>("system_cover")),
                face = ToHttps(item.Value<string>("face")),
                title = item.Value<string>("title") ?? string.Empty,
                uname = item.Value<string>("uname") ?? string.Empty,
                online = item.Value<string>("online") ?? "0",
                area_name = item.Value<string>("area_v2_name")
                    ?? item.Value<string>("areaName")
                    ?? item.Value<string>("area_name")
                    ?? string.Empty,
                area_id = item.Value<int?>("area_v2_id") ?? item.Value<int?>("area_id") ?? 0,
                parent_name = item.Value<string>("area_v2_parent_name")
                    ?? item.Value<string>("parent_name")
                    ?? string.Empty
            };
        }

        private static string GetMessage(JObject root)
        {
            return root.Value<string>("message") ?? root.Value<string>("msg") ?? "直播数据请求失败";
        }

        private static ReturnModel<T> Failure<T>(string message)
        {
            return new ReturnModel<T>
            {
                success = false,
                message = string.IsNullOrEmpty(message) ? "直播数据请求失败" : message
            };
        }

        private static string ToHttps(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                ? "https://" + url.Substring("http://".Length)
                : url;
        }
    }

    public class AreaList
    {
        public int id { get; set; }
        public string name { get; set; }
        public List<AreaListItem> list { get; set; }
    }

    public class AreaListItem
    {
        public int id { get; set; }
        public int parent_id { get; set; }
        public string parent_name { get; set; }
        public string name { get; set; }
        public string pic { get; set; }
        public int hot_status { get; set; }
        public int area_type { get; set; }
    }

    public class AreaRoomList
    {
        public int count { get; set; }
        public int has_more { get; set; }
        public ObservableCollection<RoomListItem> list { get; set; }
        public List<AreaRoomListBannerItem> banner { get; set; }
        public List<new_tags> new_tags { get; set; }
    }

    public class AreaRoomListBannerItem
    {
        public int id { get; set; }
        public string pic { get; set; }
        public string title { get; set; }
        public string link { get; set; }
    }

    public class new_tags
    {
        public int id { get; set; }
        public string name { get; set; }
        public string sort_type { get; set; }
        public string sort { get; set; }
    }
}
