using BiliBili.UWP.Models;
using System.Collections.Generic;
using System.Linq;

namespace BiliBili.UWP.Modules
{
    public static class PlaylistPlayerItemFactory
    {
        public static List<PlaylistPlaybackItemModel> Create(IList<PlaylistResourceItemModel> resources)
        {
            List<PlaylistPlaybackItemModel> items = new List<PlaylistPlaybackItemModel>();
            if (resources == null)
            {
                return items;
            }

            foreach (var resource in resources)
            {
                if (resource == null || resource.id <= 0)
                {
                    continue;
                }

                var page = resource.pages?.FirstOrDefault(item => item != null && item.id > 0);
                if (page == null)
                {
                    continue;
                }

                items.Add(new PlaylistPlaybackItemModel()
                {
                    aid = resource.id,
                    cid = page.id,
                    index = resource.index,
                    cover = resource.cover,
                    title = string.IsNullOrWhiteSpace(resource.title) ? page.title : resource.title
                });
            }

            return items;
        }
    }
}
