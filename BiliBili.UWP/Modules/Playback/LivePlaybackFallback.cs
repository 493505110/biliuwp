using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BiliBili.UWP.Modules.Playback
{
    public sealed class LivePlaybackFallback
    {
        private readonly List<string> sources = new List<string>();
        private Exception lastFailure;
        public int AttemptCount { get; private set; }

        public LivePlaybackFallback(IEnumerable<string> urls, string selectedSource)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(selectedSource) && seen.Add(selectedSource)) sources.Add(selectedSource);
            foreach (var url in urls)
                if (!string.IsNullOrWhiteSpace(url) && seen.Add(url)) sources.Add(url);
        }

        public void ReportFailure(Exception error)
        {
            lastFailure = error;
        }

        public async Task<T> OpenNextAsync<T>(Func<string, Task<T>> open, Func<bool> isCurrent,
            Action<string> sourceChanged) where T : class, IDisposable
        {
            while (isCurrent() && AttemptCount < sources.Count)
            {
                var url = sources[AttemptCount++];
                sourceChanged(url);
                // SourceChanged 可能触发手动选线或导航，不能继续打开已过期的源。
                if (!isCurrent()) return null;
                try
                {
                    var resource = await open(url);
                    if (!isCurrent())
                    {
                        resource?.Dispose();
                        return null;
                    }
                    if (resource == null) throw new InvalidOperationException("无法创建直播播放源");
                    return resource;
                }
                catch (Exception ex)
                {
                    if (!isCurrent()) return null;
                    lastFailure = ex;
                }
            }
            if (!isCurrent()) return null;
            throw new SourceException("已尝试全部 " + AttemptCount + " 条直播线路。\r\n"
                + (lastFailure?.Message ?? "未获取到可用的直播源"), lastFailure);
        }

        private sealed class SourceException : Exception
        {
            public SourceException(string message, Exception inner) : base(message, inner)
            {
                if (inner != null) HResult = inner.HResult;
            }
        }
    }
}
