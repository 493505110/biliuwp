using System;

namespace BiliBili.UWP.Modules.Playback
{
    public static class PlaybackUrl
    {
        public static bool TryNormalizeHttpUrl(string value, out string normalizedUrl)
        {
            normalizedUrl = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var candidate = value.StartsWith("//", StringComparison.Ordinal)
                ? "https:" + value
                : value;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            normalizedUrl = uri.AbsoluteUri;
            return true;
        }
    }
}
