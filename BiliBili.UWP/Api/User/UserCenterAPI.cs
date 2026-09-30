using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BiliBili.UWP.Api.User
{
    public class UserCenterAPI
    {
        /// <summary>
        /// 个人中心
        /// </summary>
        /// <param name="mid"></param>
        /// <returns></returns>
        public ApiModel UserCenterDetail(string mid)
        {
            ApiModel api = new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://app.bilibili.com/x/v2/space",
                parameter = ApiUtils.MustParameter(ApiHelper.AndroidKey, true) + $"&vmid={mid}",
            };
            api.parameter += ApiUtils.GetSign(api.parameter, ApiHelper.AndroidKey);
            return api;
        }

        /// <summary>
        /// 个人中心（网页API）
        /// </summary>
        /// <param name="mid"></param>
        /// <returns></returns>
        public ApiModel UserProfileWeb(string mid)
        {
            ApiModel api = new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/space/acc/info",
                parameter = $"mid={mid}",
            };
            return api;
        }

        /// <summary>
        /// 用户名片（网页API）。只有带上 photo=true 才会返回 data.space 里的头图地址，
        /// x/space/acc/info 已固定被风控拦截（-401 crawler_main_space_acc_info），头图改从这里取。
        /// </summary>
        /// <param name="mid"></param>
        /// <returns></returns>
        public ApiModel UserCard(string mid)
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/web-interface/card",
                parameter = $"mid={mid}&photo=true",
                headers = new Dictionary<string, string>()
                {
                    { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36" },
                    { "Referer", $"https://space.bilibili.com/{mid}" }
                }
            };
        }

        /// <summary>
        /// 获取当前用户的关注列表（网页 API）
        /// </summary>
        public ApiModel GetFollowings(int page = 1, int pageSize = 20)
        {
            var headers = new Dictionary<string, string>()
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36" },
                { "Referer", "https://www.bilibili.com/" }
            };
            var cookies = ApiHelper.GetCookies();
            if (!string.IsNullOrEmpty(cookies))
            {
                headers["Cookie"] = cookies;
            }

            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/relation/followings",
                parameter = $"vmid={ApiHelper.GetUserId()}&ps={pageSize}&pn={page}&order=desc&order_type=attention&jsonp=json",
                headers = headers
            };
        }

        /// <summary>
        /// 用户投稿（网页API）
        /// </summary>
        /// <param name="mid"></param>
        /// <returns></returns>
        public async Task<ApiModel> UserSubmitVideosWeb(string mid,int page=1,int pagesize=30)
        {
            ApiModel api = new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/space/wbi/arc/search",
                parameter = $"mid={mid}&ps={pagesize}&pn={page}&keywords=&order=pubdate&platform=web&tid=0",
                useWbi = true
            };
            return api;
        }

        /// <summary>
        /// 查询当前用户与目标用户的关注关系
        /// </summary>
        /// <param name="mid">目标用户ID</param>
        /// <returns></returns>
        public ApiModel Relation(string mid)
        {
            ApiModel api = new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/relation",
                parameter = ApiUtils.MustParameter(ApiHelper.AndroidKey, true) + $"&fid={mid}"
            };
            api.parameter += ApiUtils.GetSign(api.parameter, ApiHelper.AndroidKey);
            return api;
        }

        /// <summary>
        /// 关注
        /// </summary>
        /// <param name="mid">用户ID</param>
        /// <param name="mode">1为关注，2为取消关注</param>
        /// <returns></returns>
        public ApiModel Attention(string mid, int mode)
        {
            ApiModel api = new ApiModel()
            {
                method =   HttpMethod.POST,
                baseUrl = $"https://api.bilibili.com/x/relation/modify",
                body = ApiUtils.MustParameter(ApiHelper.AndroidKey, true) + $"&act={mode}&fid={mid}&re_src=32"
            };
            api.body += ApiUtils.GetSign(api.body, ApiHelper.AndroidKey);
            return api;
        }


    }
}
