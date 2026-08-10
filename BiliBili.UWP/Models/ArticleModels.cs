using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BiliBili.UWP.Models
{
    public class ArticleDataModel
    {
        public long id { get; set; }
        public int type { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        public ArticleAuthorModel author { get; set; }
        public ArticleCategoryModel category { get; set; }
        public long publish_time { get; set; }
        public ArticleStatsModel stats { get; set; }
        public JObject opus { get; set; }
    }

    public class ArticleAuthorModel
    {
        public long mid { get; set; }
        public string name { get; set; }
        public string face { get; set; }
    }

    public class ArticleCategoryModel
    {
        public long id { get; set; }
        public string name { get; set; }
    }

    public class ArticleStatsModel
    {
        public long view { get; set; }
        public long like { get; set; }
        public long favorite { get; set; }
    }

    public enum ArticleBlockType
    {
        Text,
        Image,
        Separator,
        Embed,
        Unknown
    }

    public enum ArticleTextKind
    {
        Paragraph,
        Heading,
        Quote,
        Bullet,
        Ordered
    }

    public enum ArticleEmbedType
    {
        Video,
        Article,
        Vote,
        Live
    }

    public abstract class ArticleBlockModel
    {
        protected ArticleBlockModel(ArticleBlockType type)
        {
            Type = type;
        }

        public ArticleBlockType Type { get; private set; }
    }

    public class ArticleInlineModel
    {
        public string Text { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public bool Strike { get; set; }
        public string Color { get; set; }
        public string Link { get; set; }
    }

    public class ArticleTextBlockModel : ArticleBlockModel
    {
        public ArticleTextBlockModel() : base(ArticleBlockType.Text)
        {
            Inlines = new List<ArticleInlineModel>();
        }

        public ArticleTextKind Kind { get; set; }
        public int HeadingLevel { get; set; }
        public int ListLevel { get; set; }
        public int ListOrder { get; set; }
        public string Alignment { get; set; }
        public List<ArticleInlineModel> Inlines { get; private set; }
    }

    public class ArticleImageBlockModel : ArticleBlockModel
    {
        public ArticleImageBlockModel() : base(ArticleBlockType.Image)
        {
        }

        public string Url { get; set; }
        public string Alt { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class ArticleSeparatorBlockModel : ArticleBlockModel
    {
        public ArticleSeparatorBlockModel() : base(ArticleBlockType.Separator)
        {
        }
    }

    public class ArticleEmbedBlockModel : ArticleBlockModel
    {
        public ArticleEmbedBlockModel() : base(ArticleBlockType.Embed)
        {
        }

        public ArticleEmbedType EmbedType { get; set; }
        public string Id { get; set; }
        public string CoverUrl { get; set; }
        public string Title { get; set; }
        public string Link { get; set; }

        public string TypeText
        {
            get
            {
                switch (EmbedType)
                {
                    case ArticleEmbedType.Video:
                        return "视频";
                    case ArticleEmbedType.Article:
                        return "专栏";
                    case ArticleEmbedType.Vote:
                        return "投票";
                    case ArticleEmbedType.Live:
                        return "直播";
                    default:
                        return "内容";
                }
            }
        }

        public string DisplayTitle
        {
            get { return string.IsNullOrWhiteSpace(Title) ? TypeText + "卡片" : Title; }
        }
    }

    public class ArticleUnknownBlockModel : ArticleBlockModel
    {
        public ArticleUnknownBlockModel() : base(ArticleBlockType.Unknown)
        {
        }

        public string Description { get; set; }
    }
}
