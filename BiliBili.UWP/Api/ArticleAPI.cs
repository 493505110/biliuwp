namespace BiliBili.UWP.Api
{
    public class ArticleAPI
    {
        public ApiModel View(long articleId)
        {
            return new ApiModel
            {
                method = HttpMethod.GET,
                baseUrl = "https://api.bilibili.com/x/article/view",
                parameter = "id=" + articleId,
                headers = ApiUtils.GetDefaultHeaders()
            };
        }
    }
}
