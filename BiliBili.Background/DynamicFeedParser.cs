using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BiliBili.Background
{
    /// <summary>归一化后的一条关注动态，磁贴和 Toast 都只认这个结构。</summary>
    internal sealed class FeedItem
    {
        public string DynamicId { get; set; }
        public bool IsBangumi { get; set; }
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string Cover { get; set; }

        /// <summary>
        /// Toast 的 launch 参数，由 App.OnLaunched 解析：
        /// 视频直接传 aid，番剧传 "bangumi,{season_id}"。
        /// </summary>
        public string LaunchArgument { get; set; }
    }

    internal sealed class FeedParseResult
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public List<FeedItem> Items { get; set; }
    }

    /// <summary>
    /// dynamic_new 响应的解析。纯逻辑、不碰 UWP API，由单元测试项目链接编译。
    /// </summary>
    internal static class DynamicFeedParser
    {
        //投稿视频 / 番剧 / 影视，与请求的 type_list 对应
        private const int TypeVideo = 8;
        private const int TypeBangumi = 512;
        private const int TypeCinema = 4099;

        public static FeedParseResult ParseFeed(string json)
        {
            var result = new FeedParseResult()
            {
                Code = -1,
                Message = "",
                Items = new List<FeedItem>()
            };
            if (string.IsNullOrWhiteSpace(json))
            {
                return result;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException)
            {
                return result;
            }

            result.Code = root["code"] == null ? -1 : root["code"].Value<int>();
            result.Message = root["message"] == null ? "" : root["message"].ToString();
            if (result.Code != 0)
            {
                return result;
            }

            var cards = root["data"] == null ? null : root["data"]["cards"] as JArray;
            if (cards == null)
            {
                return result;
            }
            foreach (var card in cards)
            {
                var item = ParseCard(card);
                if (item != null)
                {
                    result.Items.Add(item);
                }
            }
            return result;
        }

        private static FeedItem ParseCard(JToken card)
        {
            var desc = card["desc"];
            if (desc == null)
            {
                return null;
            }
            var dynamicId = desc["dynamic_id"] == null ? "" : desc["dynamic_id"].ToString();
            if (dynamicId.Length == 0)
            {
                return null;
            }
            var type = desc["type"] == null ? 0 : desc["type"].Value<int>();
            var inner = ParseInnerCard(card["card"]);
            if (inner == null)
            {
                return null;
            }

            if (type == TypeVideo)
            {
                var aid = GetString(inner, "aid");
                if (aid.Length == 0)
                {
                    return null;
                }
                var upName = GetString(inner["owner"], "name");
                if (upName.Length == 0 && desc["user_profile"] != null)
                {
                    upName = GetString(desc["user_profile"]["info"], "uname");
                }
                return new FeedItem()
                {
                    DynamicId = dynamicId,
                    IsBangumi = false,
                    Title = GetString(inner, "title"),
                    SubTitle = upName.Length == 0 ? "投稿了新视频" : upName + " 投稿了新视频",
                    Cover = GetString(inner, "pic"),
                    LaunchArgument = aid
                };
            }

            if (type == TypeBangumi || type == TypeCinema)
            {
                var seasonInfo = inner["apiSeasonInfo"];
                var seasonId = GetString(seasonInfo, "season_id");
                if (seasonId.Length == 0)
                {
                    return null;
                }
                return new FeedItem()
                {
                    DynamicId = dynamicId,
                    IsBangumi = true,
                    Title = GetString(seasonInfo, "title"),
                    SubTitle = FormatEpisode(GetString(inner, "index")),
                    Cover = GetString(inner, "cover"),
                    //番剧跳转走 BanInfoPage，它认的是 season_id
                    LaunchArgument = "bangumi," + seasonId
                };
            }
            return null;
        }

        /// <summary>card 字段是一段 JSON 字符串，需要二次解析。</summary>
        private static JObject ParseInnerCard(JToken cardField)
        {
            if (cardField == null)
            {
                return null;
            }
            if (cardField.Type == JTokenType.Object)
            {
                return (JObject)cardField;
            }
            var text = cardField.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            try
            {
                return JObject.Parse(text);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        //index 多数是纯数字，也可能已经是"特别篇"这类完整描述
        private static string FormatEpisode(string index)
        {
            if (string.IsNullOrWhiteSpace(index))
            {
                return "有更新";
            }
            var trimmed = index.Trim();
            long number;
            return long.TryParse(trimmed, out number)
                ? "更新至第" + number + "话"
                : "更新至" + trimmed;
        }

        private static string GetString(JToken token, string name)
        {
            if (token == null || token[name] == null)
            {
                return "";
            }
            return token[name].ToString();
        }
    }
}
