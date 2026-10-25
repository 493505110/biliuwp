#if FFMPEG_INTEROP_SUPPORTED
using FFmpegInteropX;
#endif
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;

namespace BiliBili.UWP.Helper
{
    public sealed class FFmpegDashSource : IDisposable
    {
#if FFMPEG_INTEROP_SUPPORTED
        private static readonly SemaphoreSlim createLock = new SemaphoreSlim(1, 1);
        private FFmpegMediaSource videoSource;
        private FFmpegMediaSource audioSource;

        private sealed class FFmpegLogProvider : ILogProvider
        {
            private readonly List<string> messages;

            public FFmpegLogProvider(List<string> messages)
            {
                this.messages = messages;
            }

            public void Log(LogLevel level, string message)
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                lock (messages)
                {
                    if (messages.Count == 20) messages.RemoveAt(0);
                    messages.Add(RedactQueryString(message));
                }
            }
        }

        private FFmpegDashSource(FFmpegMediaSource videoSource, FFmpegMediaSource audioSource)
        {
            this.videoSource = videoSource;
            this.audioSource = audioSource;
        }
#endif

        public static async Task<FFmpegDashSource> CreateAsync(string videoUrl, string audioUrl,
            bool forceSoftwareDecode = true, string referer = "https://www.bilibili.com",
            string userAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 Chrome/69.0.3497.100 Safari/537.36",
            bool isLive = false)
        {
#if FFMPEG_INTEROP_SUPPORTED
            if (!IsHttpUrl(videoUrl))
            {
                throw new ArgumentException("DASH video URL must be an absolute HTTP(S) URL.", nameof(videoUrl));
            }
            //无音轨投稿不给音频地址，此时只建视频源
            bool withAudio = !string.IsNullOrWhiteSpace(audioUrl);
            if (withAudio && !IsHttpUrl(audioUrl))
            {
                throw new ArgumentException("DASH audio URL must be an absolute HTTP(S) URL.", nameof(audioUrl));
            }

            var nativeLogs = new List<string>();
            var logProvider = new FFmpegLogProvider(nativeLogs);
            bool logProviderSet = false;
            FFmpegMediaSource createdVideoSource = null;
            FFmpegMediaSource createdAudioSource = null;
            string stage = "初始化播放源";
            // FFmpegInteropX 日志设置为进程级全局状态,并发创建会互相摘掉对方的日志提供器,
            // 用锁串行化"日志设置+源创建",确保原生日志可追溯
            await createLock.WaitAsync();
            try
            {
                var config = new MediaSourceConfig();
                if (forceSoftwareDecode)
                {
                    config.Video.VideoDecoderMode = VideoDecoderMode.ForceFFmpegSoftwareDecoder;
                }
                config.FFmpegOptions["referer"] = referer ?? "https://www.bilibili.com";
                config.FFmpegOptions["user_agent"] = userAgent ?? "Mozilla/5.0";
                config.FFmpegOptions["reconnect"] = 1;
                config.FFmpegOptions["reconnect_streamed"] = 1;
                config.FFmpegOptions["reconnect_on_network_error"] = 1;
                if (isLive)
                {
                    // 单条线路的网络等待必须有界，才能尝试其他 CDN 或 HLS 源。
                    config.FFmpegOptions["rw_timeout"] = 10000000L;
                    config.FFmpegOptions["reconnect_max_retries"] = 1;
                    config.FFmpegOptions["reconnect_delay_max"] = 2;
                }

                FFmpegInteropLogging.SetLogLevel(LogLevel.Warning);
                FFmpegInteropLogging.SetLogProvider(logProvider);
                logProviderSet = true;
                stage = isLive ? "打开直播源" : "打开视频源";
                createdVideoSource = await FFmpegMediaSource.CreateFromUriAsync(videoUrl, config);
                if (createdVideoSource == null)
                {
                    throw new InvalidOperationException("无法创建视频播放源");
                }
                if (!withAudio)
                {
                    return new FFmpegDashSource(createdVideoSource, null);
                }
                stage = "打开音频源";
                createdAudioSource = await FFmpegMediaSource.CreateFromUriAsync(audioUrl, config);
                if (createdAudioSource == null)
                {
                    throw new InvalidOperationException("无法创建音频播放源");
                }
                return new FFmpegDashSource(createdVideoSource, createdAudioSource);
            }
            catch (Exception ex)
            {
                string nativeLog;
                lock (nativeLogs)
                {
                    nativeLog = string.Join(" | ", nativeLogs);
                }
                LogHelper.WriteLog(
                    "FFmpeg 播放源原生日志（" + stage + "，HRESULT=0x" + ex.HResult.ToString("X8") + "）: " +
                    (string.IsNullOrWhiteSpace(nativeLog) ? "<empty>" : nativeLog),
                    LogType.ERROR);
                createdAudioSource?.Dispose();
                createdVideoSource?.Dispose();
                throw new SourceException(stage + "失败（HRESULT=0x" + ex.HResult.ToString("X8") + "）\r\n"
                    + RedactQueryString(ex.Message) + (string.IsNullOrWhiteSpace(nativeLog) ? string.Empty : "\r\n" + nativeLog), ex);
            }
            finally
            {
                if (logProviderSet)
                {
                    FFmpegInteropLogging.SetDefaultLogProvider();
                }
                createLock.Release();
            }
#else
            return await Task.FromResult<FFmpegDashSource>(null);
#endif
        }

#if FFMPEG_INTEROP_SUPPORTED
        private static bool IsHttpUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private sealed class SourceException : Exception
        {
            public SourceException(string message, Exception inner) : base(message, inner)
            {
                HResult = inner.HResult;
            }
        }
#endif

        internal static string RedactQueryString(string message)
        {
            var queryIndex = message.IndexOf('?');
            return queryIndex < 0
                ? message.Trim()
                : message.Substring(0, queryIndex + 1).Trim() + "<redacted>";
        }

        public MediaPlaybackItem CreateVideoPlaybackItem()
        {
#if FFMPEG_INTEROP_SUPPORTED
            return videoSource?.CreateMediaPlaybackItem();
#else
            return null;
#endif
        }

        public MediaPlaybackItem CreateAudioPlaybackItem()
        {
#if FFMPEG_INTEROP_SUPPORTED
            return audioSource?.CreateMediaPlaybackItem();
#else
            return null;
#endif
        }

        public void Dispose()
        {
#if FFMPEG_INTEROP_SUPPORTED
            var oldVideoSource = videoSource;
            var oldAudioSource = audioSource;
            videoSource = null;
            audioSource = null;
            try
            {
                oldAudioSource?.Dispose();
            }
            finally
            {
                oldVideoSource?.Dispose();
            }
#endif
        }
    }
}
