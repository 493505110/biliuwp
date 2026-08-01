using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BiliBili.UWP.Api.Live
{
    public class LiveCenterAPI
    {
        public ApiModel SignInfo(int pn = 1, int ps = 20)
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.live.bilibili.com/xlive/web-ucenter/v1/sign/WebGetSignInfo",
                parameter = string.Empty,
                headers = LiveRoomAPI.GetWebHeaders()
            };
        }

        public ApiModel History(int pn=1,int ps=20)
        {
            ApiModel api = new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = $"https://app.bilibili.com/x/v2/history/liveList",
                parameter = ApiUtils.MustParameter(ApiUtils.AndroidKey, true)+ $"&actionKey=appkey&pn={pn}&ps={ps}"
            };
            api.parameter += ApiUtils.GetSign(api.parameter, ApiUtils.AndroidKey);
            return api;
        }

       


    }
}
