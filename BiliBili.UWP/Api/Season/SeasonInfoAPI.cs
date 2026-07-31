using System;

namespace BiliBili.UWP.Api.Season
{
    public class SeasonInfoAPI
    {
        /// <summary>
        /// 获取番剧或影视的基本信息。
        /// </summary>
        /// <param name="seasonId">番剧 season_id</param>
        public ApiModel Detail(string seasonId)
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/pgc/view/web/season",
                parameter = "season_id=" + Uri.EscapeDataString(seasonId),
                headers = ApiUtils.GetDefaultHeaders()
            };
        }

        /// <summary>
        /// 使用 APP 鉴权获取用户追番和观看进度。
        /// </summary>
        public ApiModel AppDetail(string seasonId)
        {
            var api = new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/pgc/view/app/season",
                parameter = ApiUtils.MustParameter(ApiHelper.AndroidKey, true)
                    + "&season_id=" + Uri.EscapeDataString(seasonId),
                headers = ApiUtils.GetDefaultHeaders()
            };
            api.parameter += ApiUtils.GetSign(api.parameter, ApiHelper.AndroidKey);
            return api;
        }

        /// <summary>
        /// 获取官方接口不可用时的地区限定剧集信息。
        /// </summary>
        public ApiModel FallbackDetail(string seasonId)
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://www.biliplus.com/api/bangumi",
                parameter = "season=" + Uri.EscapeDataString(seasonId),
                headers = ApiUtils.GetDefaultHeaders()
            };
        }
    }
}
