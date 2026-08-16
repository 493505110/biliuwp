using System.Collections.Generic;

namespace BiliBili.UWP.Models
{
    public class PlaylistResourceListModel
    {
        public bool has_more { get; set; }
        public int total_count { get; set; }
        public List<PlaylistResourceItemModel> media_list { get; set; }
    }

    public class PlaylistResourceItemModel
    {
        public long id { get; set; }
        public int index { get; set; }
        public string bv_id { get; set; }
        public string title { get; set; }
        public string cover { get; set; }
        public List<PlaylistResourcePageModel> pages { get; set; }
    }

    public class PlaylistResourcePageModel
    {
        public long id { get; set; }
        public string title { get; set; }
        public int page { get; set; }
    }

    public class PlaylistPlaybackItemModel
    {
        public long aid { get; set; }
        public long cid { get; set; }
        public int index { get; set; }
        public string cover { get; set; }
        public string title { get; set; }
    }

    public class PlaylistHistoryCandidateModel
    {
        public long aid { get; set; }
        public long cid { get; set; }
    }
}
