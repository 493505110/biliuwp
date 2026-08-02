using BiliBili.UWP.Helper;
using System;
using System.Collections.Generic;

namespace BiliBili.UWP.Api.Live
{
    /// <summary>
    /// Web 端直播接口。直播弹幕接口已经不再接受旧的 AppRoom/mobile 参数，
    /// 因此这些请求统一使用 Cookie、Wbi 和 web 参数。
    /// </summary>
    public static class LiveRoomAPI
    {
        private static readonly string AnonymousBuvid3 = Guid.NewGuid().ToString().ToUpperInvariant() + "infoc";

        public const string DanmuInfoUrl = "https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo";
        public const string HistoryUrl = "https://api.live.bilibili.com/xlive/web-room/v1/dM/gethistory";
        public const string SendDanmuUrl = "https://api.live.bilibili.com/msg/send";
        public const string AnchorInfoUrl = "https://api.live.bilibili.com/live_user/v1/Master/info";
        public const string FollowingUrl = "https://api.live.bilibili.com/xlive/web-ucenter/user/following";
        public const string FollowingLiveUrl = "https://api.live.bilibili.com/xlive/web-ucenter/v1/xfetter/GetWebList";
        public const string OnlineGoldRankUrl = "https://api.live.bilibili.com/xlive/general-interface/v1/rank/getOnlineGoldRank";
        public const string FansRankUrl = "https://api.live.bilibili.com/xlive/general-interface/v1/rank/getFansMembersRank";
        public const string GuardRankUrl = "https://api.live.bilibili.com/xlive/app-room/v2/guardTab/topListNew";
        public const string RoomPlayInfoUrl = "https://api.live.bilibili.com/xlive/web-room/v2/index/getRoomPlayInfo";
        public const string RoomInfoUrl = "https://api.live.bilibili.com/room/v1/Room/get_info";
        public const string RoomInfoByRoomUrl = "https://api.live.bilibili.com/xlive/web-room/v1/index/getInfoByRoom";
        public const string LiveUserInfoUrl = "https://api.live.bilibili.com/xlive/web-ucenter/user/get_user_info";
        public const string GiftBagUrl = "https://api.live.bilibili.com/xlive/web-room/v1/gift/bag_list";
        public const string SendBagGiftUrl = "https://api.live.bilibili.com/xlive/revenue/v1/gift/sendBag";
        public const string SendGoldGiftUrl = "https://api.live.bilibili.com/xlive/revenue/v1/gift/sendGold";
        public const string MyMedalsUrl = "https://api.live.bilibili.com/xlive/app-ucenter/v1/user/GetMyMedals";
        public const string WearMedalUrl = "https://api.live.bilibili.com/xlive/web-room/v1/fansMedal/wear";
        public const string AreaListUrl = "https://api.live.bilibili.com/room/v1/Area/getList";
        public const string RecommendRoomsUrl = "https://api.live.bilibili.com/xlive/web-interface/v1/webMain/getMoreRecList";
        public const string AreaRoomsUrl = "https://api.live.bilibili.com/room/v3/Area/getRoomList";
        public const string AllRoomsUrl = "https://api.live.bilibili.com/xlive/web-interface/v1/second/getListByArea";

        public static ApiModel GetAreaList()
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = AreaListUrl,
                parameter = "parent_id=0&platform=web",
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetRecommendRooms()
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = RecommendRoomsUrl,
                parameter = "platform=web&web_location=333.1007",
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetAreaRooms(int parentAreaId, int areaId, int page, string sortType = "")
        {
            var api = new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = AreaRoomsUrl,
                parameter = "actionKey=appkey&appkey=" + ApiHelper.AndroidKey.Appkey
                    + "&area_id=" + areaId
                    + "&build=" + ApiHelper.build
                    + "&cate_id=0&mobi_app=android"
                    + "&page=" + page
                    + "&page_size=30&parent_area_id=" + parentAreaId
                    + "&platform=android&qn=0&tag_version=1"
                    + "&sort_type=" + Escape(string.IsNullOrEmpty(sortType) ? "online" : sortType)
                    + "&ts=" + ApiHelper.GetTimeSpan,
                headers = GetAppHeaders()
            };
            api.parameter += ApiUtils.GetSign(api.parameter, ApiHelper.AndroidKey);
            return api;
        }

        public static ApiModel GetAllRooms(int page, bool latest)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = AllRoomsUrl,
                parameter = "sort=" + (latest ? "livetime" : "online")
                    + "&page=" + page
                    + "&page_size=30&platform=web&web_location=444.253",
                headers = GetWebHeaders(),
                useWbi = true
            };
        }

        public static ApiModel GetDanmuInfo(int roomId)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = DanmuInfoUrl,
                parameter = "id=" + roomId + "&type=0&web_location=444.8",
                headers = GetWebHeaders(),
                useWbi = true
            };
        }

        public static ApiModel GetHistory(int roomId)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = HistoryUrl,
                parameter = "roomid=" + roomId,
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetAnchorInfo(long uid)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = AnchorInfoUrl,
                parameter = "uid=" + uid,
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetFollowing(int page, int pageSize)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = FollowingUrl,
                parameter = "page=" + page + "&page_size=" + pageSize + "&ignoreRecord=1&hit_ab=true",
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetFollowingLive()
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = FollowingLiveUrl,
                parameter = "hit_ab=false",
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetOnlineGoldRank(int roomId, long anchorUid)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = OnlineGoldRankUrl,
                parameter = "roomId=" + roomId + "&ruid=" + anchorUid + "&page=1&pageSize=10",
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetFansRank(long anchorUid)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = FansRankUrl,
                parameter = "ruid=" + anchorUid + "&page=1&page_size=10&rank_type=1",
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetGuardRank(int roomId, long anchorUid)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = GuardRankUrl,
                parameter = "roomid=" + roomId + "&ruid=" + anchorUid + "&page=1&page_size=30",
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetRoomPlayInfo(int roomId, int quality)
        {
            var requestedQuality = quality > 0 ? quality : 10000;
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = RoomPlayInfoUrl,
                parameter = "room_id=" + roomId + "&protocol=0,1&format=0,1,2&codec=0&qn=" + requestedQuality + "&platform=web&ptype=8",
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetRoomInfo(int roomId)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = RoomInfoUrl,
                parameter = "room_id=" + roomId,
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetRoomInfoByRoom(int roomId)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = RoomInfoByRoomUrl,
                parameter = "room_id=" + roomId,
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetLiveUserInfo()
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = LiveUserInfoUrl,
                parameter = string.Empty,
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetGiftBag(int roomId)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = GiftBagUrl,
                parameter = "room_id=" + roomId + "&t=" + ApiHelper.GetTimeSpan_2,
                headers = GetWebHeaders()
            };
        }

        public static ApiModel SendBagGift(string uid, string anchorUid, int giftId, int giftNum, int bagId, int roomId)
        {
            var csrf = GetCookieValue("bili_jct");
            return new ApiModel
            {
                method = HttpMethod.POST,
                baseUrl = SendBagGiftUrl,
                parameter = string.Empty,
                body = "uid=" + Escape(uid)
                    + "&ruid=" + Escape(anchorUid)
                    + "&gift_id=" + giftId
                    + "&gift_num=" + giftNum
                    + "&bag_id=" + bagId
                    + "&biz_code=live&biz_id=" + roomId
                    + "&platform=pc&rnd=" + ApiHelper.GetTimeSpan
                    + "&csrf=" + Escape(csrf)
                    + "&csrf_token=" + Escape(csrf),
                headers = GetWebHeaders()
            };
        }

        public static ApiModel SendGoldGift(string uid, string anchorUid, int giftId, int giftNum, int roomId, int price)
        {
            var csrf = GetCookieValue("bili_jct");
            return new ApiModel
            {
                method = HttpMethod.POST,
                baseUrl = SendGoldGiftUrl,
                parameter = string.Empty,
                body = "uid=" + Escape(uid)
                    + "&ruid=" + Escape(anchorUid)
                    + "&gift_id=" + giftId
                    + "&gift_num=" + giftNum
                    + "&bag_id=0&coin_type=gold"
                    + "&biz_code=live&biz_id=" + roomId
                    + "&platform=pc&price=" + price
                    + "&rnd=" + ApiHelper.GetTimeSpan
                    + "&csrf=" + Escape(csrf)
                    + "&csrf_token=" + Escape(csrf),
                headers = GetWebHeaders()
            };
        }

        public static ApiModel GetMyMedals(int page, int pageSize)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = MyMedalsUrl,
                parameter = "page=" + page + "&page_size=" + pageSize,
                headers = GetWebHeaders()
            };
        }

        public static ApiModel WearMedal(int medalId)
        {
            var csrf = GetCookieValue("bili_jct");
            return new ApiModel
            {
                method = HttpMethod.POST,
                baseUrl = WearMedalUrl,
                parameter = string.Empty,
                body = "medal_id=" + medalId
                    + "&csrf=" + Escape(csrf)
                    + "&csrf_token=" + Escape(csrf),
                headers = GetWebHeaders()
            };
        }

        public static IDictionary<string, string> GetWebHeaders()
        {
            return new Dictionary<string, string>
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36" },
                { "Referer", "https://live.bilibili.com/" },
                { "Cookie", GetCookieHeader() }
            };
        }

        private static IDictionary<string, string> GetAppHeaders()
        {
            return new Dictionary<string, string>
            {
                { "User-Agent", "Mozilla/5.0 BiliDroid/5.44.2 (bbcallen@gmail.com)" },
                { "Referer", "https://www.bilibili.com/" }
            };
        }

        public static string GetCookieHeader()
        {
            var cookies = ApiHelper.GetCookies() ?? string.Empty;
            // getDanmuInfo requires buvid3 even for an anonymous visitor. The
            // server accepts a newly generated browser identity for this use.
            if (cookies.IndexOf("buvid3=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                cookies += "buvid3=" + GetBuvid3() + ";";
            }
            return cookies;
        }

        public static string GetBuvid3()
        {
            var buvid = GetCookieValue("buvid3");
            return string.IsNullOrEmpty(buvid) ? AnonymousBuvid3 : buvid;
        }

        public static string GetCookieValue(string name)
        {
            var cookies = ApiHelper.GetCookies() ?? string.Empty;
            foreach (var part in cookies.Split(';'))
            {
                var pair = part.Trim().Split(new[] { '=' }, 2);
                if (pair.Length == 2 && string.Equals(pair[0], name, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair[1]);
                }
            }
            return string.Empty;
        }

        private static string Escape(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }
    }
}
