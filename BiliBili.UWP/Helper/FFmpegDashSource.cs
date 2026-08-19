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
                    if (messages.Count < 20)
                    {
                        messages.Add(RedactQueryString(message));
                    }
                }
            }
        }

        private FFmpegDashSource(FFmpegMediaSource videoSource, FFmpegMediaSource audioSource)
        {
            this.videoSource = videoSource;
            this.audioSource = audioSource;
        }
#endif

        public static async Task<FFmpegDashSource> CreateAsync(string videoUrl, string audioUrl)
        {
#if FFMPEG_INTEROP_SUPPORTED
            if (!IsHttpUrl(videoUrl))
            {
                throw new ArgumentException("DASH video URL must be an absolute HTTP(S) URL.", nameof(videoUrl));
            }
            if (!IsHttpUrl(audioUrl))
            {
                throw new ArgumentException("DASH audio URL must be an absolute HTTP(S) URL.", nameof(audioUrl));
            }

            var nativeLogs = new List<string>();
            var logProvider = new FFmpegLogProvider(nativeLogs);
            bool logProviderSet = false;
            FFmpegMediaSource createdVideoSource = null;
            FFmpegMediaSource createdAudioSource = null;
            // FFmpegInteropX 日志设置为进程级全局状态,并发创建会互相摘掉对方的日志提供器,
            // 用锁串行化"日志设置+源创建",确保原生日志可追溯
            await createLock.WaitAsync();
            try
            {
                var config = new MediaSourceConfig();
                config.Video.VideoDecoderMode = VideoDecoderMode.ForceFFmpegSoftwareDecoder;
                config.FFmpegOptions["referer"] = "https://www.bilibili.com";
                config.FFmpegOptions["user_agent"] = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 Chrome/69.0.3497.100 Safari/537.36";
                config.FFmpegOptions["reconnect"] = 1;
                config.FFmpegOptions["reconnect_streamed"] = 1;
                config.FFmpegOptions["reconnect_on_network_error"] = 1;

                FFmpegInteropLogging.SetLogLevel(LogLevel.Error);
                FFmpegInteropLogging.SetLogProvider(logProvider);
                logProviderSet = true;
                createdVideoSource = await FFmpegMediaSource.CreateFromUriAsync(videoUrl, config);
                if (createdVideoSource == null)
                {
                    return null;
                }
                createdAudioSource = await FFmpegMediaSource.CreateFromUriAsync(audioUrl, config);
                if (createdAudioSource == null)
                {
                    createdVideoSource.Dispose();
                    return null;
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
                    "FFmpeg DASH 原生日志（HRESULT=0x" + ex.HResult.ToString("X8") + "）: " +
                    (string.IsNullOrWhiteSpace(nativeLog) ? "<empty>" : nativeLog),
                    LogType.ERROR);
                createdAudioSource?.Dispose();
                createdVideoSource?.Dispose();
                throw;
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

        private static string RedactQueryString(string message)
        {
            var queryIndex = message.IndexOf('?');
            return queryIndex < 0
                ? message.Trim()
                : message.Substring(0, queryIndex + 1).Trim() + "<redacted>";
        }
#endif

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
