using System.Collections.Generic;

namespace BiliBili.UWP.Models
{
    public sealed class UserSpaceFavoriteFolder
    {
        // id 是资源列表使用的 media_id，不能拿 fid 代替；两者都可能超过 Int32。
        public long Id { get; set; }
        public string Title { get; set; }
        public int Count { get; set; }
        public int Attr { get; set; }
        public string DisplayTitle => Title + " · " + Count;
        public string Summary => ((Attr & 2) != 0 ? "公开" : "私密") + " · " + Count + " 个视频";
    }

    public sealed class UserSpaceFavoritePage
    {
        public List<UserSpaceVideo> Videos { get; } = new List<UserSpaceVideo>();
        public bool HasMore { get; set; }
    }
}
