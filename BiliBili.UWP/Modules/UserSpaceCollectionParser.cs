using BiliBili.UWP.Models;
using Newtonsoft.Json.Linq;
using System;

namespace BiliBili.UWP.Modules
{
    public static class UserSpaceCollectionParser
    {
        public static UserSpacePage<UserSpaceCollection> ParseCollections(JObject data, int page, int pageSize)
        {
            var lists = data?["items_lists"] as JObject;
            if (lists == null) throw new FormatException("合集与系列列表格式错误");
            var result = new UserSpacePage<UserSpaceCollection>();
            int received = 0;
            foreach (var kind in new[] { UserSpaceCollectionKind.Season, UserSpaceCollectionKind.Series })
            {
                var entries = lists[kind == UserSpaceCollectionKind.Season ? "seasons_list" : "series_list"] as JArray;
                if (entries == null) continue;
                received += entries.Count;
                foreach (var entry in entries)
                {
                    var meta = entry["meta"];
                    long id = meta?.Value<long?>(kind == UserSpaceCollectionKind.Season ? "season_id" : "series_id") ?? 0;
                    if (id <= 0) continue;
                    string cover = meta.Value<string>("cover");
                    if (string.IsNullOrWhiteSpace(cover)) cover = (entry["archives"] as JArray)?.First?.Value<string>("pic");
                    var collection = new UserSpaceCollection
                    {
                        Id = id,
                        Kind = kind,
                        Title = meta.Value<string>("title") ?? meta.Value<string>("name") ?? "未命名",
                        Cover = NormalizeCover(cover),
                        Description = meta.Value<string>("description") ?? "",
                        Count = meta.Value<int?>("total") ?? 0
                    };
                    // 列表接口已带每组的视频预览，不为各组再请求一次详情。
                    if (entry["archives"] is JArray previews)
                    {
                        foreach (var preview in previews)
                        {
                            var video = ParseVideo(preview);
                            if (video != null) collection.PreviewVideos.Add(video);
                            if (collection.PreviewVideos.Count == 6) break;
                        }
                    }
                    result.Items.Add(collection);
                }
            }
            result.HasMore = HasMore(lists["page"], page, pageSize, received);
            return result;
        }

        public static UserSpacePage<UserSpaceVideo> ParseVideos(JObject data, int page, int pageSize)
        {
            if (data == null) throw new FormatException("合集视频列表格式错误");
            var entries = data["archives"] as JArray;
            var result = new UserSpacePage<UserSpaceVideo>();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    var video = ParseVideo(entry);
                    if (video != null) result.Items.Add(video);
                }
            }
            result.HasMore = HasMore(data["page"], page, pageSize, entries?.Count ?? 0);
            return result;
        }

        private static UserSpaceVideo ParseVideo(JToken entry)
        {
            long aid = entry.Value<long?>("aid") ?? 0;
            if (aid <= 0) return null;
            int seconds = Math.Max(0, entry.Value<int?>("duration") ?? 0);
            return new UserSpaceVideo
            {
                Aid = aid,
                Title = entry.Value<string>("title") ?? "视频暂不可用",
                Cover = NormalizeCover(entry.Value<string>("pic")),
                Duration = seconds >= 3600
                    ? string.Format("{0}:{1:00}:{2:00}", seconds / 3600, seconds / 60 % 60, seconds % 60)
                    : string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60),
                Views = entry["stat"]?.Value<long?>("view") ?? 0,
                DanmakuCount = entry["stat"]?.Value<long?>("danmaku"),
                PublishedAt = entry.Value<long?>("pubdate") is long published && published > 0 ? published : (long?)null
            };
        }

        private static bool HasMore(JToken pagination, int page, int pageSize, int received)
        {
            if (page < 1 || pageSize < 1) throw new ArgumentOutOfRangeException(nameof(page));
            var total = pagination?.Value<long?>("total");
            if (!total.HasValue || total.Value < 0) throw new FormatException("列表分页信息缺失");
            // 两种视频接口分别使用 page_num/page_size 与 num/size，统一用实际请求的页码。
            return received > 0 && (long)page * pageSize < total.Value;
        }

        private static string NormalizeCover(string cover)
        {
            if (string.IsNullOrWhiteSpace(cover)) return "ms-appx:///Assets/Thumbnails/Placeholde.png";
            if (cover.StartsWith("//", StringComparison.Ordinal)) return "https:" + cover;
            if (cover.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return "https://" + cover.Substring(7);
            return cover;
        }
    }
}
