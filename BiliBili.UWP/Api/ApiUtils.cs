using BiliBili.UWP.Helper;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BiliBili.UWP.Api
{
    public static class ApiUtils
    {
        public static ApiKeyInfo AndroidKey = new ApiKeyInfo("1d8b6e7d45233436", "560c52ccd288fed045859ed18bffd973");
        public static ApiKeyInfo AndroidTVKey = new ApiKeyInfo("4409e2ce8ffd12b8", "59b43e04ad6965f34319062b478f83dd");
        public static ApiKeyInfo AndroidVideoKey = new ApiKeyInfo("iVGUTjsxvpLeuDCf", "aHRmhWMLkdeMuILqORnYZocwMBpMEOdt");
        public static ApiKeyInfo WebVideoKey = new ApiKeyInfo("84956560bc028eb7", "94aba54af9065f71de72f5508f1cd42e");
        private const string build = "5520400";
        private const string _mobi_app = "android";
        private const string _platform = "android";
        public static string GetSign(string url, ApiKeyInfo apiKeyInfo)
        {
            return SignHelper.SignUrl(url, apiKeyInfo.Secret);
        }
        public static string GetSign(IDictionary<string, string> pars, ApiKeyInfo apiKeyInfo)
        {
            return SignHelper.SignParameters(pars, apiKeyInfo.Secret);
        }

        /// <summary>
        /// 一些必要的参数
        /// </summary>
        /// <param name="needAccesskey">是否需要accesskey</param>
        /// <returns></returns>
        public static string MustParameter(ApiKeyInfo apikey, bool needAccesskey = false)
        {
            var url = "";
            var access_key = SettingHelper.Get_Access_key();
            if (needAccesskey && access_key != "")
            {
                url = $"access_key={access_key}&";
            }
            return url + $"appkey={apikey.Appkey}&build={build}&mobi_app={_mobi_app}&platform={_platform}&ts={Utils.GetTimestampS()}";
        }
        /// <summary>
        /// 默认一些请求头
        /// </summary>
        /// <returns></returns>
        public static IDictionary<string, string> GetDefaultHeaders()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("user-agent", "Mozilla/5.0 BiliDroid/5.44.2 (bbcallen@gmail.com)");
            headers.Add("Referer", "https://www.bilibili.com/");
            return headers;
        }

        /// <summary>
        /// 发送请求，扩展方法
        /// </summary>
        /// <param name="api"></param>
        /// <returns></returns>
        public async static Task<HttpResults> Request(this ApiModel api)
        {
            if (api.baseUrl.Contains("search"))
            {
                if (api.headers == null)
                {
                    api.headers = new Dictionary<string, string>();
                }
                api.headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36 Edg/147.0.0.0";
                api.headers["Referer"] = "https://www.bilibili.com/";
            }
            if (api.useWbi)
            {
                var originalParameter = api.parameter;
                var signedParameter = await ApiHelper.GetWbiSign(originalParameter);
                if (signedParameter == null)
                {
                    LogHelper.WriteLog("Wbi 签名失败，跳过请求", LogType.ERROR);
                    return new HttpResults() { status = false, message = "Wbi 签名获取失败" };
                }
                api.parameter = signedParameter;
                var response = await Send(api);
                if (response != null && response.status && IsWbiRiskCode(response))
                {
                    //-352 风控：wbi key 已失效，清缓存重拉后重试一次
                    ApiHelper.ClearWbiKey();
                    var retryParameter = await ApiHelper.GetWbiSign(originalParameter);
                    if (retryParameter != null)
                    {
                        api.parameter = retryParameter;
                        response = await Send(api);
                    }
                }
                return response;
            }
            return await Send(api);
        }

        private async static Task<HttpResults> Send(ApiModel api)
        {
            if (api.method == HttpMethod.GET)
            {
                return await ApiRequest.Get(api.url, api.headers);
            }
            else
            {
                return await ApiRequest.Post(api.url, api.body, api.headers);
            }
        }

        private static bool IsWbiRiskCode(HttpResults response)
        {
            var obj = response.GetJObject();
            if (obj == null)
            {
                return false;
            }
            var code = obj["code"]?.ToInt32() ?? 0;
            return code == -352;
        }

    }
    public class ApiModel
    {
        /// <summary>
        /// 请求方法
        /// </summary>
        public HttpMethod method { get; set; }
        /// <summary>
        /// API地址
        /// </summary>
        public string baseUrl { get; set; }
        /// <summary>
        /// Url参数
        /// </summary>
        public string parameter { get; set; }
        /// <summary>
        /// 发送内容体，用于POST方法
        /// </summary>
        public string body { get; set; }
        /// <summary>
        /// 请求头
        /// </summary>
        public IDictionary<string, string> headers { get; set; }
        /// <summary>
        /// 请求cookie
        /// </summary>
        public IDictionary<string, string> cookies { get; set; }
        /// <summary>
        /// 请求地址
        /// </summary>
        public string url
        {
            get
            {
                return baseUrl + "?" + parameter;
            }
        }
        /// <summary>
        /// 启用Wbi
        /// </summary>
        public bool useWbi { get; set; }
    }
}
