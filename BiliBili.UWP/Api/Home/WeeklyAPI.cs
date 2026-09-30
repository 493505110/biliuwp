using System.Collections.Generic;
using BiliBili.UWP.Api.Live;

namespace BiliBili.UWP.Api.Home
{
    /// <summary>
    /// 每周必看。App 给的入口是 h5 页面，桌面 WebView2 里渲染不出内容，改走 web 原生接口。
    /// </summary>
    public class WeeklyAPI
    {
        /// <summary>
        /// 期数列表，按期号倒序
        /// </summary>
        public ApiModel SeriesList()
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/web-interface/popular/series/list",
                parameter = "",
                headers = GetHeaders()
            };
        }

        /// <summary>
        /// 某一期的视频列表
        /// </summary>
        /// <param name="number">期号</param>
        public ApiModel SeriesOne(int number)
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/web-interface/popular/series/one",
                parameter = "number=" + number,
                headers = GetHeaders()
            };
        }

        /// <summary>
        /// 这两个接口不带 buvid3 一律返回 -352，未登录时也要补一个匿名 buvid3。
        /// </summary>
        private static IDictionary<string, string> GetHeaders()
        {
            var headers = ApiUtils.GetDefaultHeaders();
            headers["Cookie"] = LiveRoomAPI.GetCookieHeader();
            return headers;
        }
    }
}
