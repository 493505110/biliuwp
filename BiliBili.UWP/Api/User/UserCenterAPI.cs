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
        /// 用户投稿（网页API）
        /// </summary>
        /// <param name="mid"></param>
        /// <returns></returns>
        public async Task<ApiModel> UserSubmitVideosWeb(string mid,int page=1,int pagesize=30)
        {
            var parameter = $"mid={mid}&ps={pagesize}&pn={page}&keywords=&order=pubdate&platform=web&tid=0";
            var newParameter = await ApiHelper.GetWbiSign(parameter);
            var headers = new Dictionary<string, string>();
            headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36 Edg/147.0.0.0");
            ApiModel api = new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/space/wbi/arc/search",
                parameter = newParameter,
                headers = headers
            };
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
