using System;
using System.Collections.Generic;

namespace BiliBili.UWP.Models
{
    public enum InteractiveDanmakuType
    {
        Vote,
        Grade
    }

    public sealed class InteractiveDanmakuModel
    {
        public InteractiveDanmakuType Type { get; set; }
        public long Id { get; set; }
        public long VoteId { get; set; }
        public long GradeId { get; set; }
        public int Progress { get; set; }
        public int Duration { get; set; }
        public string Command { get; set; }
        public string Title { get; set; }
        public int VoteType { get; set; }
        public bool VoteSubmitted { get; set; }
        public int SelectedVoteOption { get; set; }
        public bool GradeSubmitted { get; set; }
        public int SelectedGradeScore { get; set; }
        public int Count { get; set; }
        public double AverageScore { get; set; }
        public List<InteractiveDanmakuOption> Options { get; } = new List<InteractiveDanmakuOption>();

        public string Key
        {
            get
            {
                return Id != 0
                    ? Id.ToString()
                    : Command + ":" + Progress.ToString();
            }
        }

        public TimeSpan EndTime
        {
            get
            {
                return TimeSpan.FromMilliseconds(Math.Max(1, Progress + Duration));
            }
        }
    }

    public sealed class InteractiveDanmakuOption
    {
        public int Index { get; set; }
        public string Text { get; set; }
        public int Count { get; set; }
        public bool HasSelfDefinition { get; set; }
    }

    public sealed class InteractiveDanmakuSubmitResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
