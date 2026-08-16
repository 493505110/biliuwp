using System;

namespace BiliBili.UWP.Modules
{
    public static class PlaylistParameterParser
    {
        public static bool TryParse(string parameter, out long playlistId)
        {
            playlistId = 0;
            if (string.IsNullOrWhiteSpace(parameter))
            {
                return false;
            }

            if (!Uri.TryCreate(parameter.Trim(), UriKind.Absolute, out Uri uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !IsBilibiliHost(uri.Host))
            {
                return false;
            }

            string[] segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 2 || !segments[0].Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string id = segments[1];
            if (id.StartsWith("ml", StringComparison.OrdinalIgnoreCase))
            {
                id = id.Substring(2);
            }

            if (id.Length == 0)
            {
                return false;
            }
            for (int i = 0; i < id.Length; i++)
            {
                if (id[i] < '0' || id[i] > '9')
                {
                    return false;
                }
            }

            return long.TryParse(id, out playlistId) && playlistId > 0;
        }

        private static bool IsBilibiliHost(string host)
        {
            return host.Equals("bilibili.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".bilibili.com", StringComparison.OrdinalIgnoreCase);
        }
    }
}
