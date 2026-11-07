using BiliBili.UWP.Modules.Playback;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BiliBili.UWP.Helper
{
    internal static class PlaybackCdnHelper
    {
        public static string RequireUrl(string primary, IEnumerable<string> backups = null)
        {
            return PlaybackCdnPolicy.RequireUrl(primary, backups, SettingHelper.Get_BlockPcdn());
        }

        public static string ResolveDashUrl(DashItem item)
        {
            if (item == null) return null;
            return DashStreamSelector.ResolvePlayableUrl(Primary(item), Backups(item), SettingHelper.Get_BlockPcdn());
        }

        public static string RequireDashUrl(DashItem item)
        {
            return RequireUrl(Primary(item), Backups(item));
        }

        public static bool IsDashBlocked(DashItem item)
        {
            return item != null && SettingHelper.Get_BlockPcdn() && ResolveDashUrl(item) == null
                && new[] { Primary(item) }.Concat(Backups(item)).Any(PlaybackCdnPolicy.IsPcdnUrl);
        }

        public static List<DashItem> FilterDashStreams(List<DashItem> items, Func<DashItem, bool> isRequested)
        {
            if (!SettingHelper.Get_BlockPcdn() || items == null) return items;
            var candidates = items.Where(item => item != null && isRequested(item)).ToList();
            var allowed = candidates.Where(item => ResolveDashUrl(item) != null).ToList();
            if (allowed.Count == 0 && candidates.Any(IsDashBlocked)) throw new PcdnBlockedException();
            return allowed;
        }

        private static string Primary(DashItem item)
        {
            return string.IsNullOrWhiteSpace(item?.baseUrl) ? item?.base_url : item.baseUrl;
        }

        private static IEnumerable<string> Backups(DashItem item)
        {
            return (item?.backupUrl ?? Enumerable.Empty<string>())
                .Concat(item?.backup_url ?? Enumerable.Empty<string>());
        }
    }
}
