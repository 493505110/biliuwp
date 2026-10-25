using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BiliBili.UWP.Modules.Playback
{
    public sealed class LivePlaybackSources
    {
        public int Quality { get; private set; }
        public IReadOnlyList<string> Urls { get; private set; }
        public bool RequiresWebPlayback { get; private set; }
        public const string DrmMessage = "该直播使用 DRM 加密播放源，客户端暂不支持。可在哔哩哔哩官方网页观看。";

        public static LivePlaybackSources FromPlayInfo(JToken play, int requestedQuality)
        {
            var preferred = new List<JToken>();
            var fallback = new List<JToken>();
            bool hasActiveDrmSource = false;
            foreach (var stream in (play?["stream"] as JArray) ?? new JArray())
            {
                foreach (var format in (stream["format"] as JArray) ?? new JArray())
                {
                    var isFlv = string.Equals(stream.Value<string>("protocol_name"), "http_stream", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(format.Value<string>("format_name"), "flv", StringComparison.OrdinalIgnoreCase);
                    foreach (var codec in (format["codec"] as JArray) ?? new JArray())
                    {
                        // 兼容未返回推流状态的旧响应；明确未推流的地址不能作为播放候选。
                        if (codec.Value<bool?>("is_pushing") == false) continue;
                        if (codec.Value<bool?>("drm") == true)
                        {
                            hasActiveDrmSource = true;
                            continue;
                        }
                        if (string.Equals(codec.Value<string>("codec_name"), "avc", StringComparison.OrdinalIgnoreCase))
                            (isFlv ? preferred : fallback).Add(codec);
                    }
                }
            }

            var urls = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int quality = requestedQuality > 0 ? requestedQuality : 10000;
            foreach (var codec in preferred.Concat(fallback))
            {
                int codecQuality = codec.Value<int?>("current_qn") ?? quality;
                // 回退只更换线路或封装，不静默更换用户选中的清晰度。
                if (urls.Count > 0 && codecQuality != quality) continue;
                var baseUrl = codec.Value<string>("base_url") ?? string.Empty;
                foreach (var info in (codec["url_info"] as JArray) ?? new JArray())
                {
                    var url = BuildUrl(info, baseUrl);
                    if (url == null || !seen.Add(url)) continue;
                    if (urls.Count == 0) quality = codecQuality;
                    urls.Add(url);
                }
            }
            return new LivePlaybackSources
            {
                Quality = quality, Urls = urls,
                RequiresWebPlayback = urls.Count == 0 && hasActiveDrmSource
            };
        }

        private static string BuildUrl(JToken info, string baseUrl)
        {
            var host = info.Value<string>("host");
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(baseUrl)) return null;
            var extra = (info.Value<string>("extra") ?? string.Empty).TrimStart('?', '&');
            var separator = string.IsNullOrEmpty(extra) || baseUrl.EndsWith("?") || baseUrl.EndsWith("&")
                ? string.Empty : baseUrl.Contains("?") ? "&" : "?";
            var url = host + baseUrl + separator + extra;
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? url : null;
        }
    }
}
