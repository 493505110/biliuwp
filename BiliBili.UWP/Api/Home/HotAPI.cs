using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BiliBili.UWP.Api.Home
{
    public class HotAPI
    {
        /// <summary>
        /// 热门列表
        /// </summary>
        /// <param name="idx">上一条的 idx，用于翻页</param>
        /// <param name="last_param">上一条的 param，用于翻页</param>
        /// <param name="entranceId">热门子频道（top_items 里的 entrance_id），0 为全部热门</param>
        public ApiModel Popular(string idx = "0", string last_param = "", int entranceId = 0)
        {
            ApiModel api = new ApiModel()
            {
                method =  HttpMethod.GET,
                baseUrl = $"https://app.bilibili.com/x/v2/show/popular/index",
                parameter = ApiUtils.MustParameter(ApiHelper.AndroidKey, true) + $"&idx={idx}&last_param={last_param}"
            };
            if (entranceId != 0)
            {
                api.parameter += $"&entrance_id={entranceId}";
            }
            api.parameter += ApiUtils.GetSign(api.parameter, ApiHelper.AndroidKey);
            return api;
        }
    }
}
