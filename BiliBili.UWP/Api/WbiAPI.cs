namespace BiliBili.UWP.Api
{
    public class WbiAPI
    {
        /// <summary>
        /// ªÒ»°wbi_key∫Õwbi_img
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