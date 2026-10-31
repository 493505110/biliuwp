using System;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using BiliBili.UWP.Modules.Playback;
using Windows.Media.Streaming.Adaptive;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace BiliBili.UWP.Helper
{
    public sealed class PlaybackNetworkMonitor : IDisposable
    {
        private readonly object sync = new object();
        private readonly AdaptiveMediaSource source;
        private bool disposed;
        private static readonly HttpClient indexClient = new HttpClient();
        private readonly CancellationTokenSource indexCancellation = new CancellationTokenSource();
        private readonly string videoUrl;
        private readonly string audioUrl;
        private readonly string videoIndexRange;
        private readonly string audioIndexRange;
        private Task indexLoadTask;
        private bool loadingIndexes;
        private DashSegmentBitrateIndex videoIndex;
        private DashSegmentBitrateIndex audioIndex;
        public PlaybackNetworkStatistics Statistics { get; }
        public bool SupportsNetworkActivity => source != null;
        public bool HasSeparateAudio { get; }
        public bool IsDash { get; }
        public bool IsLoadingSegmentIndexes { get { lock (sync) return loadingIndexes; } }

        public double? GetSegmentBitrate(bool isAudio, double positionSeconds)
        {
            lock (sync) return (isAudio ? audioIndex : videoIndex)?.FindBitrate(positionSeconds);
        }

        public Task EnsureSegmentIndexesAsync()
        {
            lock (sync)
            {
                if (disposed || !IsDash) return Task.CompletedTask;
                if (indexLoadTask == null)
                {
                    loadingIndexes = true;
                    indexLoadTask = LoadSegmentIndexesAsync(indexCancellation.Token);
                }
                return indexLoadTask;
            }
        }

        public PlaybackNetworkMonitor(ReturnPlayModel result)
        {
            IsDash = result.usePlayMode == UsePlayMode.Dash || result.usePlayMode == UsePlayMode.FFmpegDash;
            HasSeparateAudio = !string.IsNullOrWhiteSpace(result.audioUrl);
            videoUrl = result.videoUrl;
            audioUrl = result.audioUrl;
            videoIndexRange = result.videoIndexRange;
            audioIndexRange = result.audioIndexRange;
            Statistics = new PlaybackNetworkStatistics(
                result.videoUrl ?? result.url ?? (result.urls?.Count > 0 ? result.urls[0] : null), result.audioUrl);
            if (result.usePlayMode == UsePlayMode.Dash)
            {
                source = result.mediaSource as AdaptiveMediaSource;
                if (source != null)
                {
                    source.DownloadCompleted += DownloadCompleted;
                    source.DownloadFailed += DownloadFailed;
                }
            }
        }

        private async Task LoadSegmentIndexesAsync(CancellationToken cancellationToken)
        {
            var indexes = await Task.WhenAll(
                LoadIndexAsync(videoUrl, videoIndexRange, cancellationToken),
                LoadIndexAsync(audioUrl, audioIndexRange, cancellationToken));
            lock (sync)
            {
                loadingIndexes = false;
                if (disposed) return;
                videoIndex = indexes[0];
                audioIndex = indexes[1];
            }
        }

        private static async Task<DashSegmentBitrateIndex> LoadIndexAsync(
            string url, string range, CancellationToken cancellationToken)
        {
            if (PlaybackNetworkStatistics.GetHost(url) == null
                || !DashSegmentBitrateIndex.TryParseRange(range, out var offset, out var length)) return null;
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                using (var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url)))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    request.Headers.Referer = new Uri("https://www.bilibili.com/");
                    request.Headers.TryAppendWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/69.0.3497.100 Safari/537.36");
                    request.Headers.TryAppendWithoutValidation("Accept-Encoding", "identity");
                    request.Headers.TryAppendWithoutValidation("Range", "bytes="
                        + offset.ToString(CultureInfo.InvariantCulture) + "-"
                        + (offset + length - 1).ToString(CultureInfo.InvariantCulture));
                    using (var response = await indexClient.SendRequestAsync(request,
                        HttpCompletionOption.ResponseHeadersRead).AsTask(timeout.Token))
                    {
                        var contentRange = response.Content.Headers.ContentRange;
                        // CDN 忽略 Range 时拒绝读取正文，避免为统计下载整段视频。
                        if (response.StatusCode != HttpStatusCode.PartialContent
                            || contentRange?.FirstBytePosition != (ulong)offset
                            || contentRange.LastBytePosition != (ulong)(offset + length - 1)
                            || !string.Equals(contentRange.Unit, "bytes", StringComparison.OrdinalIgnoreCase)) return null;
                        using (var stream = await response.Content.ReadAsInputStreamAsync().AsTask(timeout.Token))
                        using (var reader = new DataReader(stream))
                        {
                            if (await reader.LoadAsync((uint)length).AsTask(timeout.Token) != (uint)length) return null;
                            var data = new byte[length];
                            reader.ReadBytes(data);
                            return DashSegmentBitrateIndex.TryParse(data, out var index) ? index : null;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 统计不可用不影响播放；不要把带签名的媒体 URL 写入日志。
                return null;
            }
        }

        private void DownloadCompleted(AdaptiveMediaSource sender, AdaptiveMediaSourceDownloadCompletedEventArgs args)
        {
            Record(args.ResourceContentType, args.ResourceUri, args.Statistics);
        }

        private void DownloadFailed(AdaptiveMediaSource sender, AdaptiveMediaSourceDownloadFailedEventArgs args)
        {
            // 失败请求已经收到的字节同样计入下载量。
            Record(args.ResourceContentType, args.ResourceUri, args.Statistics);
        }

        private void Record(string contentType, Uri uri, AdaptiveMediaSourceDownloadStatistics statistics)
        {
            bool isAudio = contentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true;
            bool isVideo = contentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true;
            if (!isAudio && !isVideo) return;
            lock (sync)
            {
                if (disposed) return;
                Statistics.RecordDownload(isAudio, uri?.AbsoluteUri, statistics?.ContentBytesReceivedCount ?? 0);
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
            }
            indexCancellation.Cancel();
            indexCancellation.Dispose();
            if (source != null)
            {
                source.DownloadCompleted -= DownloadCompleted;
                source.DownloadFailed -= DownloadFailed;
            }
        }
    }
}
