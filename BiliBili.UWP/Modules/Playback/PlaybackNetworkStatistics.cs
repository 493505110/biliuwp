using System;
using System.Collections.Generic;

namespace BiliBili.UWP.Modules.Playback
{
    // 下载回调可能来自后台线程；每个播放源持有独立实例，切源后不复用。
    public sealed class PlaybackNetworkStatistics
    {
        public const int HistoryLength = 60;
        private readonly object sync = new object();
        private readonly Queue<double?> videoBitrateHistory = new Queue<double?>();
        private readonly Queue<double?> audioBitrateHistory = new Queue<double?>();
        private readonly Queue<double> networkActivityHistory = new Queue<double>();
        private string videoHost;
        private string audioHost;
        private double networkBytes;

        public PlaybackNetworkStatistics(string videoUrl, string audioUrl)
        {
            videoHost = GetHost(videoUrl);
            audioHost = GetHost(audioUrl);
        }

        public static string GetHost(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return null;
            // Authority 可能带有 userinfo，只显示 Host 和非默认端口。
            var host = uri.Host;
            if (uri.HostNameType == UriHostNameType.IPv6 && !host.StartsWith("[")) host = "[" + host + "]";
            return uri.IsDefaultPort ? host : host + ":" + uri.Port;
        }

        public void RecordDownload(bool isAudio, string url, ulong bytes)
        {
            lock (sync)
            {
                var host = GetHost(url);
                if (isAudio)
                {
                    if (host != null) audioHost = host;
                }
                else
                {
                    if (host != null) videoHost = host;
                }
                networkBytes += bytes;
            }
        }

        public void Sample(double? videoBitsPerSecond, double? audioBitsPerSecond)
        {
            lock (sync)
            {
                AddSample(videoBitrateHistory, ToKilobitsPerSecond(videoBitsPerSecond));
                AddSample(audioBitrateHistory, ToKilobitsPerSecond(audioBitsPerSecond));
                // 活动量表示本采样周期内收到的总字节，不除以采样时长。
                AddSample(networkActivityHistory, networkBytes / 1024);
                networkBytes = 0;
            }
        }

        private static double? ToKilobitsPerSecond(double? bitrate)
        {
            return bitrate.HasValue && bitrate.Value > 0
                && !double.IsNaN(bitrate.Value) && !double.IsInfinity(bitrate.Value)
                ? (double?)(bitrate.Value / 1000) : null;
        }

        private static void AddSample<T>(Queue<T> history, T value)
        {
            if (history.Count == HistoryLength) history.Dequeue();
            history.Enqueue(value);
        }

        public PlaybackNetworkSnapshot GetSnapshot()
        {
            lock (sync)
            {
                return new PlaybackNetworkSnapshot(videoHost, audioHost,
                    videoBitrateHistory.ToArray(), audioBitrateHistory.ToArray(), networkActivityHistory.ToArray());
            }
        }
    }

    public sealed class PlaybackNetworkSnapshot
    {
        public string VideoHost { get; }
        public string AudioHost { get; }
        public double?[] VideoBitrateSamples { get; }
        public double?[] AudioBitrateSamples { get; }
        public double[] NetworkActivitySamples { get; }

        public PlaybackNetworkSnapshot(string videoHost, string audioHost,
            double?[] videoBitrateSamples, double?[] audioBitrateSamples, double[] networkActivitySamples)
        {
            VideoHost = videoHost;
            AudioHost = audioHost;
            VideoBitrateSamples = videoBitrateSamples;
            AudioBitrateSamples = audioBitrateSamples;
            NetworkActivitySamples = networkActivitySamples;
        }
    }
}
