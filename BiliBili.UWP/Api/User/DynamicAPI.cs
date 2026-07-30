using BiliBili.UWP.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BiliBili.UWP.Api.User
{
    public class DynamicAPI
    {
        /// <summary>
        /// 发表图片动态
        /// </summary>
        /// <param name="mid">用户ID</param>
        /// <param name="mode">1为关注，2为取消关注</param>
        /// <returns></returns>
        public ApiModel CreateDynamicPhoto(string imgs, string content,string at_uids, string at_control)
        {
           
            ApiModel api = new ApiModel()
            {
                method = HttpMethod.POST,
                baseUrl = $"https://api.vc.bilibili.com/dynamic_svr/v1/dynamic_svr/create_draw",
                parameter = ApiUtils.MustParameter(ApiHelper.AndroidKey, true),
                body = $"uid={ApiHelper.GetUserId()}&category=3&pictures={Uri.EscapeDataString(imgs)}&description={Uri.EscapeDataString(content)}&content={Uri.EscapeDataString(content)}&setting=%7B%22copy_forbidden%22%3A0%7D&at_uids={Uri.EscapeDataString(at_uids)}&at_control={Uri.EscapeDataString(at_control)}&jumpfrom=110&extension=%7B%22emoji_type%22%3A1%7D"
            };
            api.parameter += ApiUtils.GetSign(api.parameter, ApiHelper.AndroidKey);
            return api;
        }

        /// <summary>
        /// 发表文本动态
        /// </summary>
        /// <param name="mid">用户ID</param>
        /// <param name="mode">1为关注，2为取消关注</param>
        /// <returns></returns>
        public ApiModel CreateDynamicText(string content, string at_uids, string at_control)
        {
            ApiModel api = new ApiModel()
            {
                method = HttpMethod.POST,
                baseUrl = $"https://api.vc.bilibili.com/dynamic_svr/v1/dynamic_svr/create",
                parameter = ApiUtils.MustParameter(ApiHelper.AndroidKey, true),
                body = $"uid={ApiHelper.GetUserId()}&dynamic_id=0&type=4&content={Uri.EscapeDataString(content)}&setting=%7B%22copy_forbidden%22%3A0%7D&at_uids={Uri.EscapeDataString(at_uids)}&at_control={Uri.EscapeDataString(at_control)}&jumpfrom=110&extension=%7B%22emoji_type%22%3A1%7D"
            };
            api.parameter += ApiUtils.GetSign(api.parameter, ApiHelper.AndroidKey);
            return api;
        }

        /// <summary>
        /// 获取用户空间动态（新版polymer接口，需要Wbi签名）
        /// </summary>
        /// <param name="hostMid">目标用户UID</param>
        /// <param name="offset">分页偏移量，第一页留空</param>
        public ApiModel SpaceDynamic(string hostMid, string offset = "")
        {
            var par = $"host_mid={hostMid}&platform=web&features=itemOpusStyle";
            if (!string.IsNullOrEmpty(offset))
            {
                par += $"&offset={Uri.EscapeDataString(offset)}";
            }
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/polymer/web-dynamic/v1/feed/space",
                parameter = par,
                useWbi = true
            };
        }

        /// <summary>
        /// 获取动态详情（新版polymer接口，需要Wbi签名）
        /// </summary>
        public ApiModel GetDetail(string id)
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/polymer/web-dynamic/v1/detail",
                parameter = $"id={id}&timezone_offset=-480&platform=web&features=itemOpusStyle",
                useWbi = true
            };
        }

        /// <summary>
        /// 获取转发列表（新版polymer接口，需要桌面UA）
        /// </summary>
        public ApiModel GetForwardList(string id, string offset = "")
        {
            var par = $"id={id}";
            if (!string.IsNullOrEmpty(offset))
                par += $"&offset={Uri.EscapeDataString(offset)}";
            var api = new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/polymer/web-dynamic/v1/detail/forward",
                parameter = par
            };
            // 该接口需要桌面 UA，否则返回 -352 风控错误
            api.headers = new System.Collections.Generic.Dictionary<string, string>()
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36" }
            };
            return api;
        }

    }
}
