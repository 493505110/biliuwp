using BiliBili.UWP.Api;
using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BiliBili.UWP.Helper
{
    public static class BiliJumpAiProviders
    {
        public const string Zhou2008 = "zhou2008";
        public const string DeepSeek = "deepseek";
        public const string Custom = "custom";
        public const string Zhou2008BuiltInApiKey = "sk-TWKHcsjER5CuMK8nMC3HNpTFd5Cq2PaAXdv9OkaJ9t0YPKTp";

        public static bool IsCustom(string provider)
        {
            return string.Equals(provider, Custom, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetDefaultApiUrl(string provider)
        {
            switch (provider?.Trim().ToLowerInvariant())
            {
                case DeepSeek:
                    return "https://api.deepseek.com/v1/chat/completions";
                case Custom:
                    return "https://api.openai.com/v1/chat/completions";
                case Zhou2008:
                default:
                    return "https://newapi.zhou2008.cn/v1/chat/completions";
            }
        }

        public static string GetDefaultModel(string provider)
        {
            switch (provider?.Trim().ToLowerInvariant())
            {
                case DeepSeek:
                    return "deepseek-v4-flash";
                case Custom:
                    return "gpt-5.6-luna";
                case Zhou2008:
                default:
                    return "deepseek-v4-flash";
            }
        }
    }

    public static class BiliJumpAiService
    {
        private const string SystemPrompt =
            "你是一个严谨的视频植入广告识别器。根据带时间轴的字幕，识别视频正文中的商品、服务、平台或品牌植入广告。" +
            "不要把视频主题本身、普通口播、片头片尾、节目赞助鸣谢或无推广性质的内容误判为广告。" +
            "请只返回 JSON，不要返回 Markdown 或解释文字。格式必须是：" +
            "{\"ads\":[{\"start_time\":0,\"end_time\":0,\"product_name\":\"\",\"ad_content\":\"\"}],\"msg\":\"\"}。" +
            "时间单位为秒，start_time 必须小于 end_time；没有广告时 ads 返回空数组。";

        public static async Task<ReturnModel<BiliJumpAiResult>> RecognizeAsync(
            string aid,
            string cid,
            string title,
            double duration,
            HasSubtitleModel subtitleInfo = null,
            bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(aid) || string.IsNullOrWhiteSpace(cid))
            {
                return Failure("视频标识不完整");
            }

            var provider = SettingHelper.Get_BiliJumpAiProvider();
            var isZhou2008 = string.Equals(provider, BiliJumpAiProviders.Zhou2008, StringComparison.OrdinalIgnoreCase);
            var apiKey = isZhou2008
                ? BiliJumpAiProviders.Zhou2008BuiltInApiKey
                : SettingHelper.Get_BiliJumpAiApiKey();
            var apiUrl = SettingHelper.Get_BiliJumpAiApiUrl();
            var model = SettingHelper.Get_BiliJumpAiModel();
            var cacheKey = aid + ":" + cid;
            if (!forceRefresh)
            {
                var cached = SqlHelper.GetBiliJumpCache(cacheKey);
                if (cached != null
                    && !string.IsNullOrWhiteSpace(cached.adsJson)
                    && (string.IsNullOrWhiteSpace(cached.model)
                        || string.Equals(cached.model, model, StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        var cachedResult = JsonConvert.DeserializeObject<BiliJumpAiResult>(cached.adsJson);
                        if (cachedResult != null)
                        {
                            cachedResult.ads = BiliJumpAiParser.NormalizeSegments(cachedResult.ads, duration);
                            return Success(cachedResult, "已使用本地 AI 识别缓存");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLog("读取 BiliJump AI 缓存失败", LogType.ERROR, ex);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Failure("请先在设置中填写 AI API 密钥");
            }
            if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var endpoint)
                || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
            {
                return Failure("AI API 地址必须是 HTTP 或 HTTPS 地址");
            }
            if (string.IsNullOrWhiteSpace(model))
            {
                return Failure("请先在设置中填写 AI 模型名称");
            }

            try
            {
                subtitleInfo = subtitleInfo ?? await PlayurlHelper.GetHasSubTitle(aid, cid);
                var subtitle = subtitleInfo?.subtitles?.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.subtitle_url));
                if (subtitle == null)
                {
                    return Failure("该视频没有可用字幕，暂不支持音频识别");
                }

                var subtitleModel = await PlayurlHelper.GetSubtitle(subtitle.subtitle_url);
                if (subtitleModel?.body == null || subtitleModel.body.Count == 0)
                {
                    return Failure("字幕内容为空");
                }

                var lines = subtitleModel.body.Select(x => new BiliJumpSubtitleLine
                {
                    From = x.from,
                    To = x.to,
                    Content = x.content
                });
                var subtitleText = BiliJumpAiParser.BuildSubtitleText(title, lines);
                if (string.IsNullOrWhiteSpace(subtitleText))
                {
                    return Failure("字幕内容为空");
                }

                var requestBody = new
                {
                    model = model,
                    temperature = 0.1,
                    max_tokens = 2048,
                    messages = new[]
                    {
                        new { role = "system", content = SystemPrompt },
                        new { role = "user", content = subtitleText }
                    }
                };
                var headers = new Dictionary<string, string>
                {
                    ["Authorization"] = "Bearer " + apiKey
                };
                var response = await ApiRequest.Post(endpoint.AbsoluteUri, JsonConvert.SerializeObject(requestBody), headers, "application/json");
                if (response == null || !response.status || string.IsNullOrWhiteSpace(response.results))
                {
                    return Failure("AI 请求失败：" + (response?.message ?? "没有返回内容"));
                }

                var content = ExtractMessageContent(response.results);
                if (!BiliJumpAiParser.TryParse(content, duration, out var result, out var parseError))
                {
                    return Failure(parseError);
                }

                SqlHelper.SaveBiliJumpCache(new BiliJumpCacheClass
                {
                    cacheKey = cacheKey,
                    aid = aid,
                    cid = cid,
                    title = title ?? string.Empty,
                    adsJson = JsonConvert.SerializeObject(result),
                    model = model,
                    updatedAt = DateTime.Now
                });
                return Success(result, "AI 识别完成");
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("BiliJump AI 识别失败", LogType.ERROR, ex);
                return Failure("AI 识别失败：" + ex.Message);
            }
        }

        private static string ExtractMessageContent(string response)
        {
            var root = JObject.Parse(response);
            var content = root["choices"]?.FirstOrDefault()?["message"]?["content"];
            if (content == null)
            {
                return response;
            }
            if (content.Type == JTokenType.String)
            {
                return content.ToString();
            }
            if (content is JArray parts)
            {
                return string.Join("\n", parts.Select(x => x["text"]?.ToString() ?? x.ToString()));
            }
            return content.ToString();
        }

        private static ReturnModel<BiliJumpAiResult> Success(BiliJumpAiResult result, string message)
        {
            return new ReturnModel<BiliJumpAiResult>
            {
                success = true,
                message = message,
                data = result
            };
        }

        private static ReturnModel<BiliJumpAiResult> Failure(string message)
        {
            return new ReturnModel<BiliJumpAiResult>
            {
                success = false,
                message = message,
                data = null
            };
        }
    }
}
