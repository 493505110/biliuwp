namespace BiliBili.UWP.Api
{
    public class WbiAPI
    {
        /// <summary>
        /// 获取wbi_key和wbi_img
        /// </summary>
        /// <returns></returns>
        public ApiModel GetWbiKey()
        {
            ApiModel api = new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = $"https://api.bilibili.com/x/web-interface/nav"
            };
            return api;
        }
    }
}