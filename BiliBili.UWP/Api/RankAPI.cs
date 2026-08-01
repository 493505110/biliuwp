using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BiliBili.UWP.Api
{
    public class RankAPI
    {
        /// <summary>
        /// 排行榜
        /// </summary>
        /// <param name="rid">分区ID</param>
        /// <param name="type">1=全站，2原创</param>
        /// <returns></returns>
        public ApiModel Rank(int rid, int type)
        {
            string rankType = type == 2 ? "origin" : "all";
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/web-interface/ranking/v2",
                parameter = $"rid={rid}&type={rankType}",
                headers = new Dictionary<string, string>
                {
                    { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36 Edg/147.0.0.0" },
                    { "Referer", "https://www.bilibili.com/v/popular/rank/all" }
                }
            };
        }
    }
}
