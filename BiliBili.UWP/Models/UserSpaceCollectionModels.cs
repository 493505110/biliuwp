using System.Collections.Generic;

namespace BiliBili.UWP.Models
{
    public enum UserSpaceCollectionKind
    {
        Season,
        Series
    }

    public sealed class UserSpaceCollection
    {
        public long Id { get; set; }
        public UserSpaceCollectionKind Kind { get; set; }
        public string Title { get; set; }
        public string Cover { get; set; }
        public string Description { get; set; }
        public int Count { get; set; }
        public string KindName => Kind == UserSpaceCollectionKind.Season ? "合集" : "系列";
        public string CountText => Count + " 个视频";
        public string Heading => KindName + "·" + Title;
        public List<UserSpaceVideo> PreviewVideos { get; } = new List<UserSpaceVideo>();
        public bool HasPreviewVideos => PreviewVideos.Count > 0;
    }

    public sealed class UserSpaceVideo
    {
        public long Aid { get; set; }
        public string Title { get; set; }
        public string Cover { get; set; }
        public string Duration { get; set; }
        public long Views { get; set; }
        public long? DanmakuCount { get; set; }
        public string DanmakuText => DanmakuCount?.ToString() ?? "--";
        public long? PublishedAt { get; set; }
        public bool HasPublicationTime => PublishedAt.HasValue;
        public bool CanOpen { get; set; } = true;
    }

    public sealed class UserSpacePage<T>
    {
        public List<T> Items { get; } = new List<T>();
        public bool HasMore { get; set; }
    }
}
