using BiliBili.UWP.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace BiliBili.UWP.Modules
{
    public static class UserSpaceFavoriteParser
    {
        public static List<UserSpaceFavoriteFolder> ParseFoldersResponse(JObject response)
        {
            if (response == null || response["code"]?.Type != JTokenType.Integer)
                throw new FormatException("收藏夹响应格式错误");
            if (response.Value<int>("code") != 0)
                throw new InvalidOperationException(response.Value<string>("message") ?? response.Value<string>("msg") ?? "请求失败");

            var data = response["data"];
            if (data == null) throw new FormatException("收藏夹响应数据缺失");
            // 无可见收藏夹时接口会成功返回 data: null，不能与数据字段缺失混为一谈。
            if (data.Type == JTokenType.Null) return new List<UserSpaceFavoriteFolder>();
            return ParseFolders(data as JObject);
        }

        public static List<UserSpaceFavoriteFolder> ParseFolders(JObject data)
        {
            if (data == null) throw new FormatException("收藏夹列表格式错误");
            var entries = data["list"] as JArray;
            if (entries == null && data.Value<int?>("count") != 0) throw new FormatException("收藏夹列表缺失");
            var result = new List<UserSpaceFavoriteFolder>();
            foreach (var entry in entries ?? new JArray())
            {
                long id = entry.Value<long?>("id") ?? 0;
                if (id <= 0) continue;
                result.Add(new UserSpaceFavoriteFolder
                {
                    Id = id,
                    Title = entry.Value<string>("title") ?? "未命名收藏夹",
                    Count = entry.Value<int?>("media_count") ?? 0,
                    Attr = entry.Value<int?>("attr") ?? 0
                });
            }
            return result;
        }

        public static UserSpaceFavoritePage ParseVideos(JObject data, int page, int pageSize = 20)
        {
            if (data == null) throw new FormatException("收藏视频列表格式错误");
            var entries = data["medias"] as JArray;
            var result = new UserSpaceFavoritePage();
            foreach (var entry in entries ?? new JArray())
            {
                long aid = entry.Value<long?>("id") ?? 0;
                if (aid <= 0 || entry.Value<int?>("type") != 2) continue;
                int duration = Math.Max(0, entry.Value<int?>("duration") ?? 0);
                string cover = entry.Value<string>("cover");
                if (string.IsNullOrWhiteSpace(cover)) cover = "ms-appx:///Assets/Logo/PI160_100.png";
                else if (cover.StartsWith("//", StringComparison.Ordinal)) cover = "https:" + cover;
                else if (cover.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) cover = "https://" + cover.Substring(7);
                result.Videos.Add(new UserSpaceVideo
                {
                    Aid = aid,
                    Title = entry.Value<string>("title") ?? "视频暂不可用",
                    Cover = cover,
                    Duration = duration >= 3600
                        ? string.Format("{0}:{1:00}:{2:00}", duration / 3600, duration / 60 % 60, duration % 60)
                        : string.Format("{0:00}:{1:00}", duration / 60, duration % 60),
                    Views = entry["cnt_info"]?.Value<long?>("play") ?? 0,
                    DanmakuCount = entry["cnt_info"]?.Value<long?>("danmaku"),
                    PublishedAt = entry.Value<long?>("pubtime") is long published && published > 0 ? published : (long?)null,
                    CanOpen = ((entry.Value<int?>("attr") ?? 0) & 1) == 0
                });
            }
            var hasMore = data.Value<bool?>("has_more");
            long? total = data["info"]?.Value<long?>("media_count");
            if (!hasMore.HasValue && !total.HasValue) throw new FormatException("收藏视频分页信息缺失");
            // 分页以服务端原始媒体数为准，不能因音频等非视频项被过滤就提前结束。
            result.HasMore = entries != null && entries.Count > 0 && (hasMore ?? (long)page * pageSize < total.Value);
            return result;
        }
    }
}
