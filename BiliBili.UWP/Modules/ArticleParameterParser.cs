using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BiliBili.UWP.Modules
{
    public static class ArticleParameterParser
    {
        private static readonly Regex ArticleParameterRegex = new Regex(
            @"^(?:cv(?<id>[0-9]+)|https?://(?:www\.)?bilibili\.com/read/(?:cv|app/|mobile/)(?<id>[0-9]+)|bilibili://article/(?<id>[0-9]+)|(?<id>[0-9]+))/?(?:[?#].*)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static bool TryParse(object parameter, out long articleId)
        {
            articleId = 0;

            object[] parameters = parameter as object[];
            if (parameters != null)
            {
                if (parameters.Length == 0)
                {
                    return false;
                }

                parameter = parameters[0];
            }

            if (parameter is long)
            {
                long value = (long)parameter;
                if (value > 0)
                {
                    articleId = value;
                    return true;
                }

                return false;
            }

            if (parameter is int)
            {
                int value = (int)parameter;
                if (value > 0)
                {
                    articleId = value;
                    return true;
                }

                return false;
            }

            string text = parameter as string;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            text = text.Trim();
            Match match = ArticleParameterRegex.Match(text);
            long parsedArticleId;
            if (!match.Success ||
                !long.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out parsedArticleId) ||
                parsedArticleId <= 0)
            {
                return false;
            }

            articleId = parsedArticleId;
            return true;
        }
    }
}
