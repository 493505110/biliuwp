using BiliBili.UWP.Models;
using System;
using System.Collections.Generic;

namespace BiliBili.UWP.Modules
{
    public static class PlaylistPagination
    {
        public static bool TryGetNextCursor(
            IList<PlaylistResourceItemModel> resources,
            string currentBvid,
            long currentOid,
            out string bvid,
            out long oid)
        {
            bvid = "";
            oid = 0;
            if (resources == null || resources.Count == 0)
            {
                return false;
            }

            PlaylistResourceItemModel last = null;
            for (int i = resources.Count - 1; i >= 0; i--)
            {
                if (resources[i] != null)
                {
                    last = resources[i];
                    break;
                }
            }

            if (last == null || last.id <= 0 || string.IsNullOrWhiteSpace(last.bv_id)
                || (last.id == currentOid && string.Equals(last.bv_id, currentBvid, StringComparison.Ordinal)))
            {
                return false;
            }

            bvid = last.bv_id;
            oid = last.id;
            return true;
        }
    }
}
