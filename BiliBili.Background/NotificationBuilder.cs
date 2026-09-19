using System.Text;

namespace BiliBili.Background
{
    /// <summary>
    /// 磁贴与 Toast 的 XML 构造。
    /// 所有插值文本必须走 EscapeXml：标题里的 &amp; 、&lt; 会让 XmlDocument.LoadXml 直接抛异常，
    /// 整条通知随之丢失，这是旧实现磁贴长期空白的原因之一。
    /// </summary>
    internal static class NotificationBuilder
    {
        //磁贴通知的图片超过 200KB 会被系统静默丢弃，B 站封面原图常年超标，必须走缩略参数
        private const string CoverThumbSuffix = "@336w_190h_1c.jpg";

        public static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                switch (c)
                {
                    case '&':
                        sb.Append("&amp;");
                        break;
                    case '<':
                        sb.Append("&lt;");
                        break;
                    case '>':
                        sb.Append("&gt;");
                        break;
                    case '"':
                        sb.Append("&quot;");
                        break;
                    case '\'':
                        sb.Append("&apos;");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        /// <summary>封面统一转 https 并补上缩略参数；已经带 @ 参数的保持原样。</summary>
        public static string NormalizeCover(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return "";
            }
            var result = url.Trim();
            if (result.StartsWith("//"))
            {
                result = "https:" + result;
            }
            else if (result.StartsWith("http://"))
            {
                result = "https://" + result.Substring("http://".Length);
            }
            return result.Contains("@") ? result : result + CoverThumbSuffix;
        }

        public static string BuildTileXml(FeedItem item)
        {
            var cover = EscapeXml(NormalizeCover(item.Cover));
            var title = EscapeXml(item.Title);
            var subTitle = EscapeXml(item.SubTitle);

            var sb = new StringBuilder();
            sb.Append("<tile><visual branding='name'>");
            foreach (var template in new[] { "TileMedium", "TileWide", "TileLarge" })
            {
                sb.Append("<binding template='").Append(template).Append("'>");
                if (cover.Length != 0)
                {
                    sb.Append("<image src='").Append(cover).Append("' placement='peek'/>");
                }
                sb.Append("<text hint-wrap='true'>").Append(title).Append("</text>");
                sb.Append("<text hint-style='captionSubtle' hint-wrap='true'>").Append(subTitle).Append("</text>");
                sb.Append("</binding>");
            }
            sb.Append("</visual></tile>");
            return sb.ToString();
        }

        public static string BuildToastXml(FeedItem item)
        {
            var cover = EscapeXml(NormalizeCover(item.Cover));
            var launch = EscapeXml(item.LaunchArgument);

            //番剧："您关注的《XXX》更新了第 3 话"；视频："某某 上传了《XXX》"
            var headline = item.IsBangumi
                ? "您关注的《" + item.Title + "》"
                : item.SubTitle;
            var body = item.IsBangumi
                ? item.SubTitle
                : "《" + item.Title + "》";

            var sb = new StringBuilder();
            sb.Append("<toast launch='").Append(launch).Append("'>");
            sb.Append("<visual><binding template='ToastGeneric'>");
            sb.Append("<text>").Append(EscapeXml(headline)).Append("</text>");
            sb.Append("<text>").Append(EscapeXml(body)).Append("</text>");
            if (cover.Length != 0)
            {
                sb.Append("<image placement='appLogoOverride' src='").Append(cover).Append("'/>");
            }
            sb.Append("</binding></visual></toast>");
            return sb.ToString();
        }
    }
}
