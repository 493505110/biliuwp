using System;

namespace BiliBili.UWP.Api
{
    public class PlaylistAPI
    {
        public ApiModel ResourceList(long playlistId, string bvid = "", long oid = 0, int pageSize = 20)
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/v2/medialist/resource/list",
                parameter = "type=1"
                    + "&biz_id=" + playlistId
                    + "&ps=" + pageSize
                    + "&desc=0"
                    + "&sort_field=1"
                    + "&tid=0"
                    + "&bvid=" + Uri.EscapeDataString(bvid ?? "")
                    + "&oid=" + oid
                    + "&otype=2"
                    + "&with_current=0"
                    + "&direction=0"
                    + "&preview=0"
                    + "&use_pn=0"
                    + "&pn=1"
                    + "&mobi_app=web"
            };
        }
    }
}
