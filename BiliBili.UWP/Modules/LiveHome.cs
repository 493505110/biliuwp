using BiliBili.UWP.Api;
using BiliBili.UWP.Api.Live;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace BiliBili.UWP.Modules
{
    public class LiveHome : LiveCommand, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _loading = true;
        public bool Loading
        {
            get { return _loading; }
            set { _loading = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Loading")); }
        }

        private live_area_entrance_v2 _areas;
        public live_area_entrance_v2 Areas
        {
            get { return _areas; }
            set { _areas = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Areas")); }
        }

        private List<room_list> _roomList;
        public List<room_list> RoomList
        {
            get { return _roomList; }
            set { _roomList = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("RoomList")); }
        }

        public async Task LoadHome()
        {
            try
            {
                Loading = true;
                var areaTask = LiveRoomAPI.GetAreaList().Request();
                var roomTask = LiveRoomAPI.GetRecommendRooms().Request();
                await Task.WhenAll(areaTask, roomTask);

                var areaRoot = areaTask.Result.GetJObject();
                var roomRoot = roomTask.Result.GetJObject();
                if (!IsSuccess(areaTask.Result, areaRoot))
                {
                    Utils.ShowMessageToast(GetMessage(areaRoot, areaTask.Result.message));
                }
                else
                {
                    Areas = BuildAreas(areaRoot["data"] as JArray);
                }

                if (!IsSuccess(roomTask.Result, roomRoot))
                {
                    Utils.ShowMessageToast(GetMessage(roomRoot, roomTask.Result.message));
                    RoomList = new List<room_list>();
                }
                else
                {
                    RoomList = BuildRoomList(roomRoot["data"]?["recommend_room_list"] as JArray);
                }
            }
            catch (Exception ex)
            {
                Utils.ShowMessageToast(ex.Message);
            }
            finally
            {
                Loading = false;
            }
        }

        private static live_area_entrance_v2 BuildAreas(JArray source)
        {
            var result = new ObservableCollection<live_area_entrance_v2_item>();
            foreach (var parent in (source ?? new JArray()).OfType<JObject>())
            {
                var children = parent["list"] as JArray;
                var child = children?.OfType<JObject>()
                    .FirstOrDefault(x => x.Value<int?>("hot_status") == 1 && !string.IsNullOrEmpty(x.Value<string>("pic")))
                    ?? children?.OfType<JObject>().FirstOrDefault();
                if (child == null)
                {
                    continue;
                }

                var parentId = parent.Value<int?>("id") ?? child.Value<int?>("parent_id") ?? 0;
                var areaId = child.Value<int?>("id") ?? 0;
                var parentName = parent.Value<string>("name") ?? child.Value<string>("parent_name") ?? string.Empty;
                var areaName = child.Value<string>("name") ?? string.Empty;
                result.Add(new live_area_entrance_v2_item
                {
                    id = areaId,
                    title = areaName,
                    pic = ToHttps(child.Value<string>("pic")),
                    area_v2_id = areaId,
                    area_v2_parent_id = parentId,
                    link = "https://live.bilibili.com/app/area?parent_area_id=" + parentId
                        + "&parent_area_name=" + Uri.EscapeDataString(parentName)
                        + "&area_id=" + areaId
                        + "&area_name=" + Uri.EscapeDataString(areaName)
                });
            }

            return new live_area_entrance_v2
            {
                module_info = new live_module_info { title = "直播分区" },
                list = result
            };
        }

        private static List<room_list> BuildRoomList(JArray source)
        {
            var rooms = new ObservableCollection<room_list_item>(
                (source ?? new JArray()).OfType<JObject>().Select(item => new room_list_item
                {
                    area_v2_parent_id = item.Value<int?>("area_v2_parent_id") ?? 0,
                    area_v2_id = item.Value<int?>("area_v2_id") ?? 0,
                    area_v2_name = item.Value<string>("area_v2_name") ?? string.Empty,
                    area_v2_parent_name = item.Value<string>("area_v2_parent_name") ?? string.Empty,
                    cover = ToHttps(item.Value<string>("cover")),
                    title = item.Value<string>("title") ?? string.Empty,
                    roomid = item.Value<int?>("roomid") ?? 0,
                    uname = item.Value<string>("uname") ?? string.Empty,
                    online = item.Value<long?>("online") ?? 0
                }));

            return new List<room_list>
            {
                new room_list
                {
                    module_info = new live_module_info
                    {
                        title = "推荐直播",
                        link = "https://live.bilibili.com/app/all-live"
                    },
                    list = rooms
                }
            };
        }

        private static bool IsSuccess(HttpResults response, JObject root)
        {
            return response.status && root != null && root.Value<int?>("code") == 0;
        }

        private static string GetMessage(JObject root, string fallback)
        {
            return root?.Value<string>("message")
                ?? root?.Value<string>("msg")
                ?? fallback
                ?? "直播数据请求失败";
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

    public class live_module_info
    {
        public int id { get; set; }
        public int type { get; set; }
        public int sort { get; set; }
        public string title { get; set; }
        public string link { get; set; }
        public string pic { get; set; }
        public int count { get; set; }
    }

    public class live_area_entrance_v2
    {
        public live_module_info module_info { get; set; }
        public ObservableCollection<live_area_entrance_v2_item> list { get; set; }
    }

    public class live_area_entrance_v2_item
    {
        public int id { get; set; }
        public string title { get; set; }
        public string link { get; set; }
        public string pic { get; set; }
        public int area_v2_id { get; set; }
        public int area_v2_parent_id { get; set; }
    }

    public class room_list : LiveCommand
    {
        public live_module_info module_info { get; set; }
        public ObservableCollection<room_list_item> list { get; set; }
    }

    public class room_list_item
    {
        public int area_v2_parent_id { get; set; }
        public int area_v2_id { get; set; }
        public string area_v2_name { get; set; }
        public string area_v2_parent_name { get; set; }
        public string cover { get; set; }
        public string title { get; set; }
        public int roomid { get; set; }
        public string uname { get; set; }
        public long online { get; set; }
        public string online_str
        {
            get
            {
                return online >= 10000
                    ? (online / 10000d).ToString("0.00") + "万"
                    : online.ToString();
            }
        }
    }
}
