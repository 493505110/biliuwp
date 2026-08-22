using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BiliBili.UWP.Modules
{
    public sealed class BiliJumpAdSegment
    {
        public double start_time { get; set; }
        public double end_time { get; set; }
        public string product_name { get; set; }
        public string ad_content { get; set; }
    }

    public sealed class BiliJumpAiResult
    {
        public List<BiliJumpAdSegment> ads { get; set; } = new List<BiliJumpAdSegment>();
        public string msg { get; set; }
    }

    public sealed class BiliJumpSubtitleLine
    {
        public double From { get; set; }
        public double To { get; set; }
        public string Content { get; set; }
    }

    public static class BiliJumpAiParser
    {
        public static string BuildSubtitleText(string title, IEnumerable<BiliJumpSubtitleLine> lines)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(title))
            {
                builder.Append("标题: ").AppendLine(title.Trim());
                builder.AppendLine();
            }

            builder.AppendLine("字幕:");
            if (lines != null)
            {
                foreach (var line in lines.OrderBy(x => x.From))
                {
                    if (line == null || string.IsNullOrWhiteSpace(line.Content) || line.To <= line.From)
                    {
                        continue;
                    }

                    builder.Append(line.From.ToString("0.00", CultureInfo.InvariantCulture))
                        .Append(" --> ")
                        .Append(line.To.ToString("0.00", CultureInfo.InvariantCulture))
                        .AppendLine();
                    builder.AppendLine(line.Content.Trim());
                }
            }

            return builder.ToString().Trim();
        }

        public static bool TryParse(string responseContent, double duration, out BiliJumpAiResult result, out string error)
        {
            result = null;
            error = null;
            if (string.IsNullOrWhiteSpace(responseContent))
            {
                error = "AI 返回内容为空";
                return false;
            }

            try
            {
                var json = ExtractJson(responseContent);
                var root = JObject.Parse(json);
                var data = root["data"] as JObject ?? root;
                var adsToken = data["ads"] ?? data["segments"];
                var ads = new List<BiliJumpAdSegment>();

                if (adsToken is JArray adsArray)
                {
                    foreach (var item in adsArray.OfType<JObject>())
                    {
                        if (!TryGetNumber(item, "start_time", "start", out var start)
                            || !TryGetNumber(item, "end_time", "end", out var end))
                        {
                            continue;
                        }

                        ads.Add(new BiliJumpAdSegment
                        {
                            start_time = start,
                            end_time = end,
                            product_name = GetString(item, "product_name", "product", "name"),
                            ad_content = GetString(item, "ad_content", "content", "description")
                        });
                    }
                }

                result = new BiliJumpAiResult
                {
                    ads = NormalizeSegments(ads, duration),
                    msg = GetString(data, "msg", "message")
                };
                if (string.IsNullOrWhiteSpace(result.msg))
                {
                    result.msg = result.ads.Count == 0 ? "未识别到广告" : "识别到广告";
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "AI 返回内容不是有效 JSON: " + ex.Message;
                return false;
            }
        }

        public static List<BiliJumpAdSegment> NormalizeSegments(IEnumerable<BiliJumpAdSegment> segments, double duration)
        {
            var normalized = new List<BiliJumpAdSegment>();
            if (segments == null)
            {
                return normalized;
            }

            foreach (var segment in segments)
            {
                if (segment == null || double.IsNaN(segment.start_time) || double.IsNaN(segment.end_time)
                    || double.IsInfinity(segment.start_time) || double.IsInfinity(segment.end_time))
                {
                    continue;
                }

                var start = Math.Max(0, segment.start_time);
                var end = Math.Max(0, segment.end_time);
                if (duration > 0)
                {
                    start = Math.Min(start, duration);
                    end = Math.Min(end, duration);
                }

                if (end <= start)
                {
                    continue;
                }

                normalized.Add(new BiliJumpAdSegment
                {
                    start_time = start,
                    end_time = end,
                    product_name = (segment.product_name ?? string.Empty).Trim(),
                    ad_content = (segment.ad_content ?? string.Empty).Trim()
                });
            }

            normalized = normalized.OrderBy(x => x.start_time).ToList();
            var merged = new List<BiliJumpAdSegment>();
            foreach (var segment in normalized)
            {
                var current = merged.LastOrDefault();
                if (current == null || segment.start_time > current.end_time + 1)
                {
                    merged.Add(segment);
                    continue;
                }

                current.end_time = Math.Max(current.end_time, segment.end_time);
                current.product_name = JoinText(current.product_name, segment.product_name, " | ");
                current.ad_content = JoinText(current.ad_content, segment.ad_content, "\n---\n");
            }

            return merged;
        }

        private static string ExtractJson(string responseContent)
        {
            var content = responseContent.Trim();
            var fenced = Regex.Match(content, @"\x60\x60\x60(?:json)?\s*([\s\S]*?)\x60\x60\x60", RegexOptions.IgnoreCase);
            if (fenced.Success)
            {
                content = fenced.Groups[1].Value.Trim();
            }

            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return content.Substring(start, end - start + 1);
            }

            return content;
        }

        private static bool TryGetNumber(JObject obj, string firstName, string secondName, out double value)
        {
            value = 0;
            var token = obj[firstName] ?? obj[secondName];
            if (token == null)
            {
                return false;
            }

            return double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string GetString(JObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                var token = obj[name];
                if (token != null && token.Type != JTokenType.Null)
                {
                    return token.ToString().Trim();
                }
            }

            return string.Empty;
        }

        private static string JoinText(string first, string second, string separator)
        {
            if (string.IsNullOrWhiteSpace(first)) return second ?? string.Empty;
            if (string.IsNullOrWhiteSpace(second)) return first;
            return first + separator + second;
        }
    }
}
