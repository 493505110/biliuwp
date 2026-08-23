using BiliBili.UWP.Api;
using BiliBili.UWP.Modules;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BiliBili.UWP.Helper
{
    public sealed class BiliJumpAiCacheResponse
    {
        public bool Available { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string CacheKey { get; set; }
        public string LeaseToken { get; set; }
        public BiliJumpAiResult Result { get; set; }

        public bool IsHit
        {
            get { return Available && string.Equals(Status, "hit", StringComparison.OrdinalIgnoreCase) && Result != null; }
        }

        public bool IsLeader
        {
            get { return Available && string.Equals(Status, "leader", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsPending
        {
            get { return Available && string.Equals(Status, "pending", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsSaved
        {
            get { return Available && string.Equals(Status, "saved", StringComparison.OrdinalIgnoreCase); }
        }

        public static BiliJumpAiCacheResponse Unavailable(string message = null)
        {
            return new BiliJumpAiCacheResponse
            {
                Available = false,
                Status = "unavailable",
                Message = message ?? "远程缓存不可用"
            };
        }
    }

    public static class BiliJumpAiCacheService
    {
        public const string PromptVersion = "bili-jump-v1";
        private const string Endpoint = "https://api.zhou2008.cn/biliuwp/video_ad_jump";
        private const int RequestTimeoutMilliseconds = 3000;

        public static bool IsConfigured()
        {
            return string.Equals(
                       SettingHelper.Get_BiliJumpAiProvider(),
                       BiliJumpAiProviders.Zhou2008,
                       StringComparison.OrdinalIgnoreCase)
                || SettingHelper.Get_BiliJumpAiCacheEnabled();
        }

        public static async Task<BiliJumpAiCacheResponse> QueryAsync(
            string aid,
            string cid,
            string provider,
            string apiUrl,
            string model)
        {
            return await PostAsync("cache/query", new
            {
                aid = aid,
                cid = cid,
                provider = provider,
                api_url = apiUrl,
                model = model,
                prompt_version = PromptVersion
            });
        }

        public static async Task<BiliJumpAiCacheResponse> ClaimAsync(
            string aid,
            string cid,
            string provider,
            string apiUrl,
            string model,
            string title,
            double duration)
        {
            return await PostAsync("cache/claim", new
            {
                aid = aid,
                cid = cid,
                provider = provider,
                api_url = apiUrl,
                model = model,
                prompt_version = PromptVersion,
                title = title ?? string.Empty,
                duration = duration > 0 ? duration : 0
            });
        }

        public static async Task<BiliJumpAiCacheResponse> WaitForHitAsync(
            string aid,
            string cid,
            string provider,
            string apiUrl,
            string model,
            int attempts = 8)
        {
            var last = BiliJumpAiCacheResponse.Unavailable();
            for (var i = 0; i < attempts; i++)
            {
                await Task.Delay(1000);
                last = await QueryAsync(aid, cid, provider, apiUrl, model);
                if (!last.Available || last.IsHit || !last.IsPending)
                {
                    return last;
                }
            }

            return last;
        }

        public static async Task<BiliJumpAiCacheResponse> SaveAsync(
            string cacheKey,
            string leaseToken,
            string subtitleHash,
            BiliJumpAiResult result)
        {
            return await PostAsync("cache/save", new
            {
                cache_key = cacheKey,
                lease_token = leaseToken,
                subtitle_hash = subtitleHash,
                result = result
            });
        }

        public static async Task ReleaseAsync(string cacheKey, string leaseToken)
        {
            if (!IsConfigured() || string.IsNullOrWhiteSpace(cacheKey) || string.IsNullOrWhiteSpace(leaseToken))
            {
                return;
            }

            await PostAsync("cache/release", new
            {
                cache_key = cacheKey,
                lease_token = leaseToken
            });
        }

        private static async Task<BiliJumpAiCacheResponse> PostAsync(string path, object body)
        {
            if (!TryGetEndpoint(out var endpoint))
            {
                return BiliJumpAiCacheResponse.Unavailable("远程缓存未配置");
            }

            try
            {
                var request = ApiRequest.Post(
                    endpoint + "/v1/" + path,
                    JsonConvert.SerializeObject(body),
                    new Dictionary<string, string>(),
                    "application/json");
                var completed = await Task.WhenAny(request, Task.Delay(RequestTimeoutMilliseconds));
                if (completed != request)
                {
                    return BiliJumpAiCacheResponse.Unavailable("远程缓存请求超时");
                }

                var response = await request;
                if (response == null || !response.status || string.IsNullOrWhiteSpace(response.results))
                {
                    return BiliJumpAiCacheResponse.Unavailable(response?.message);
                }

                var json = response.GetJObject();
                if (json == null)
                {
                    return BiliJumpAiCacheResponse.Unavailable("远程缓存返回内容无效");
                }

                return FromJson(json);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("远程 AI 缓存请求失败", LogType.ERROR, ex);
                return BiliJumpAiCacheResponse.Unavailable(ex.Message);
            }
        }

        private static BiliJumpAiCacheResponse FromJson(JObject json)
        {
            var resultToken = json["result"] ?? json["data"];
            BiliJumpAiResult result = null;
            if (resultToken != null && resultToken.Type != JTokenType.Null)
            {
                try
                {
                    result = resultToken.ToObject<BiliJumpAiResult>();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("远程 AI 缓存结果解析失败", LogType.ERROR, ex);
                }
            }

            return new BiliJumpAiCacheResponse
            {
                Available = true,
                Status = json["status"]?.ToString() ?? string.Empty,
                Message = json["message"]?.ToString(),
                CacheKey = json["cache_key"]?.ToString(),
                LeaseToken = json["lease_token"]?.ToString(),
                Result = result
            };
        }

        private static bool TryGetEndpoint(out string endpoint)
        {
            endpoint = Endpoint;
            return true;
        }
    }
}
