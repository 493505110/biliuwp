using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BiliBili.UWP.Modules.Playback
{
    public static class PlaybackCdnPolicy
    {
        public const string BlockedMessage = "没有符合 CDN 策略的播放或下载地址，已屏蔽 PCDN/MCDN。可在播放设置中关闭屏蔽后重试。";

        private static readonly string[] PcdnDomains =
        {
            "mcdn.bilivideo.com", "mcdn.bilivideo.cn", "mcdn.bilivideo.net",
            "szbdyd.com", "mountaintoys.cn", "nexusedgeio.com", "ahdohpiechei.com"
        };

        public static bool IsPcdnUrl(string value)
        {
            if (!TryGetHttpUri(value, out var uri)) return false;
            var host = uri.IdnHost.TrimEnd('.');
            if (PcdnDomains.Any(domain => IsDomain(host, domain))) return true;

            // 已知 PCDN 主机特征；仅检查 host，不能误命中路径或签名中的文字。
            // 参考 https://github.com/xfangfang/wiliwili/pull/666
            if (IsDomain(host, "bilivideo.com") || IsDomain(host, "bilivideo.cn") || IsDomain(host, "bilivideo.net"))
            {
                var label = host.Split('.')[0];
                if (label.IndexOf("302ppio", StringComparison.OrdinalIgnoreCase) >= 0
                    || label.IndexOf("302kodo", StringComparison.OrdinalIgnoreCase) >= 0
                    || host.Equals("upos-sz-mirror14b.bilivideo.com", StringComparison.OrdinalIgnoreCase)) return true;
            }

            // IP 直连的 MCDN 资源接口；普通 IP 地址不因此被一概屏蔽。
            if ((uri.HostNameType == UriHostNameType.IPv4 || uri.HostNameType == UriHostNameType.IPv6)
                && Regex.IsMatch(uri.AbsolutePath, @"^/v\d+/resource(?:/|$)", RegexOptions.IgnoreCase)) return true;

            foreach (var part in uri.Query.TrimStart('?').Split('&'))
            {
                var pair = part.Split(new[] { '=' }, 2);
                if (pair.Length == 2
                    && string.Equals(Uri.UnescapeDataString(pair[0]), "os", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(Uri.UnescapeDataString(pair[1]), "mcdn", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static string SelectUrl(string primary, IEnumerable<string> backups, bool blockPcdn)
        {
            // 返回原字符串，完整保留服务端签名和转义，不拼接或替换主机。
            return new[] { primary }.Concat(backups ?? Enumerable.Empty<string>())
                .FirstOrDefault(url => TryGetHttpUri(url, out _) && (!blockPcdn || !IsPcdnUrl(url)));
        }

        public static string RequireUrl(string primary, IEnumerable<string> backups, bool blockPcdn)
        {
            if (!blockPcdn) return primary;
            var candidates = backups?.ToList();
            var selected = SelectUrl(primary, candidates, true);
            if (selected != null) return selected;
            if (new[] { primary }.Concat(candidates ?? Enumerable.Empty<string>()).Any(IsPcdnUrl))
                throw new PcdnBlockedException();
            throw new InvalidOperationException("未获取到有效的播放或下载地址");
        }

        private static bool IsDomain(string host, string domain)
        {
            return host.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetHttpUri(string value, out Uri uri)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }

    public sealed class PcdnBlockedException : InvalidOperationException
    {
        public PcdnBlockedException() : base(PlaybackCdnPolicy.BlockedMessage) { }
    }
}
