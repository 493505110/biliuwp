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

        public ApiModel History(long max = 0, long viewAt = 0, string business = "", int pageSize = 20)
        {
            var cursorBusiness = string.IsNullOrEmpty(business) ? string.Empty : $"&business={business}";
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/web-interface/history/cursor",
                parameter = $"max={max}&view_at={viewAt}&type=live&ps={pageSize}{cursorBusiness}",
                headers = LiveRoomAPI.GetWebHeaders()
            };
        }

       


    }
}
