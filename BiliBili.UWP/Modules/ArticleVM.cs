using BiliBili.UWP.Api;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BiliBili.UWP.Modules
{
    public class ArticleVM : IModules
    {
        private readonly ArticleAPI articleAPI = new ArticleAPI();
        private readonly ArticleContentParser contentParser = new ArticleContentParser();
        private int loadVersion;
        private bool loading;
        private string errorMessage;
        private ArticleDataModel article;
        private IReadOnlyList<ArticleBlockModel> blocks = new List<ArticleBlockModel>();
        private long articleId;

        public bool Loading
        {
            get { return loading; }
            private set { SetProperty(ref loading, value, "Loading"); }
        }

        public string ErrorMessage
        {
            get { return errorMessage; }
            private set { SetProperty(ref errorMessage, value, "ErrorMessage"); }
        }

        public ArticleDataModel Article
        {
            get { return article; }
            private set { SetProperty(ref article, value, "Article"); }
        }

        public IReadOnlyList<ArticleBlockModel> Blocks
        {
            get { return blocks; }
            private set { SetProperty(ref blocks, value, "Blocks"); }
        }

        public long ArticleId
        {
            get { return articleId; }
            private set { SetProperty(ref articleId, value, "ArticleId"); }
        }

        public async Task LoadAsync(long newArticleId)
        {
            int version = Interlocked.Increment(ref loadVersion);
            ArticleId = newArticleId;
            Loading = true;
            ErrorMessage = null;
            Article = null;
            Blocks = new List<ArticleBlockModel>();

            try
            {
                HttpResults response = await articleAPI.View(newArticleId).Request();
                if (version != loadVersion)
                {
                    return;
                }
                if (!response.status)
                {
                    ErrorMessage = response.message;
                    return;
                }

                ApiDataModel<ArticleDataModel> envelope = await response.GetJson<ApiDataModel<ArticleDataModel>>();
                if (version != loadVersion)
                {
                    return;
                }
                if (envelope == null)
                {
                    ErrorMessage = "专栏数据解析失败";
                    return;
                }
                if (envelope.code != 0)
                {
                    ErrorMessage = string.IsNullOrWhiteSpace(envelope.message)
                        ? "专栏加载失败（" + envelope.code + "）"
                        : envelope.message;
                    return;
                }
                if (envelope.data == null)
                {
                    ErrorMessage = "专栏数据解析失败";
                    return;
                }

                IReadOnlyList<ArticleBlockModel> parsedBlocks;
                try
                {
                    parsedBlocks = contentParser.Parse(envelope.data);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("专栏内容解析失败", LogType.ERROR, ex);
                    ErrorMessage = "专栏内容解析失败";
                    return;
                }
                if (parsedBlocks.Count == 0)
                {
                    ErrorMessage = "专栏正文为空";
                    return;
                }

                int unknownCount = parsedBlocks.Count(item => item.Type == ArticleBlockType.Unknown);
                if (unknownCount > 0)
                {
                    LogHelper.WriteLog(
                        "专栏 " + newArticleId + " 包含 " + unknownCount + " 个未知内容块",
                        LogType.INFO);
                }
                Article = envelope.data;
                Blocks = parsedBlocks;
            }
            catch (Exception ex)
            {
                if (version == loadVersion)
                {
                    ErrorMessage = HandelError(ex).message;
                }
            }
            finally
            {
                if (version == loadVersion)
                {
                    Loading = false;
                }
            }
        }

        private void SetProperty<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }
            field = value;
            DoPropertyChanged(propertyName);
        }
    }
}
