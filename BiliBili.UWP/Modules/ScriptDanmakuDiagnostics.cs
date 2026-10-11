using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BiliBili.UWP.Modules
{
    /// <summary>当前视频的 M8 报错与运行日志，独立于控件重建和脚本池补齐。</summary>
    public sealed class ScriptDanmakuDiagnostics
    {
        private readonly HashSet<Tuple<string, string, string>> errors =
            new HashSet<Tuple<string, string, string>>();
        public const int MaxLogEntries = 500;
        private readonly Queue<ScriptDanmakuLogEntry> logs = new Queue<ScriptDanmakuLogEntry>();

        public int Count => errors.Count;
        public ScriptDanmakuErrorEventArgs LatestError { get; private set; }
        public int LogCount => logs.Count;
        public string LogText => string.Join("\n", logs.Select(entry => entry.Description));

        public void RecordLogs(ScriptDanmakuLogEventArgs batch)
        {
            if (batch == null) return;
            if (batch.IsClear) ClearLogs();
            foreach (var entry in batch.Entries)
            {
                if (entry == null) continue;
                logs.Enqueue(entry);
                while (logs.Count > MaxLogEntries) logs.Dequeue();
            }
        }

        public void ClearLogs()
        {
            logs.Clear();
        }

        public void Record(ScriptDanmakuErrorEventArgs error)
        {
            if (error == null) return;

            errors.Add(Tuple.Create(error.Stage, error.ItemId, error.Message));
            LatestError = error;
        }

        public void Clear()
        {
            errors.Clear();
            LatestError = null;
            ClearLogs();
        }
    }

    public sealed class ScriptDanmakuLogEventArgs : EventArgs
    {
        public ScriptDanmakuLogEventArgs(IReadOnlyList<ScriptDanmakuLogEntry> entries, bool isClear = false)
        {
            Entries = entries ?? new ScriptDanmakuLogEntry[0];
            IsClear = isClear;
        }

        public IReadOnlyList<ScriptDanmakuLogEntry> Entries { get; }
        public bool IsClear { get; }
    }

    public sealed class ScriptDanmakuLogEntry
    {
        public ScriptDanmakuLogEntry(double positionSeconds, string itemId, string message)
        {
            PositionSeconds = double.IsNaN(positionSeconds) || double.IsInfinity(positionSeconds)
                ? 0 : Math.Max(0, Math.Min(positionSeconds, int.MaxValue));
            ItemId = itemId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public double PositionSeconds { get; }
        public string ItemId { get; }
        public string Message { get; }

        public string Description
        {
            get
            {
                var time = TimeSpan.FromSeconds(PositionSeconds);
                return string.Format(CultureInfo.InvariantCulture, "[{0:00}:{1:00}:{2:00}.{3:000}]{4} {5}",
                    (int)time.TotalHours, time.Minutes, time.Seconds, time.Milliseconds,
                    string.IsNullOrWhiteSpace(ItemId) ? string.Empty : " [脚本 " + ItemId + "]", Message);
            }
        }
    }

    public sealed class ScriptDanmakuErrorEventArgs : EventArgs
    {
        public ScriptDanmakuErrorEventArgs(string stage, string itemId, string message)
        {
            Stage = string.IsNullOrWhiteSpace(stage) ? "unknown" : stage.Trim().ToLowerInvariant();
            ItemId = itemId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Stage { get; }
        public string ItemId { get; }
        public string Message { get; }

        public string Description
        {
            get
            {
                string stage;
                switch (Stage)
                {
                    case "compile": stage = "编译"; break;
                    case "runtime": stage = "运行"; break;
                    case "composite": stage = "合成"; break;
                    case "host": stage = "宿主"; break;
                    default: stage = Stage == "unknown" ? "未知" : Stage; break;
                }

                return "阶段：" + stage
                    + (string.IsNullOrWhiteSpace(ItemId) ? string.Empty : "\n脚本 ID：" + ItemId)
                    + "\n原因：" + Message;
            }
        }
    }
}
