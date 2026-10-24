using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace BiliBili.UWP.Helper
{
    // SYEngine 没有 ARM64 原生库，启动及传统流播放必须在编译期隔离。
    public static class LegacyPlayback
    {
#if ARM64
        private static bool forceSoftwareDecode;
#endif
        public static string BackendName
        {
            get
            {
#if ARM64
                return "FFmpeg";
#else
                return "SYEngine";
#endif
            }
        }

        public static bool ForceSoftwareDecode
        {
            get
            {
#if ARM64
                return forceSoftwareDecode;
#else
                return SYEngine.Core.ForceSoftwareDecode;
#endif
            }
            set
            {
#if ARM64
                forceSoftwareDecode = value;
#else
                SYEngine.Core.ForceSoftwareDecode = value;
#endif
            }
        }

        public static void Initialize()
        {
#if !ARM64
            SYEngine.Core.Initialize();
#endif
        }
    }

    public sealed class LegacyPlaybackPlaylist
    {
#if ARM64
        private readonly List<string> urls = new List<string>();
        private string referer;
#else
        private readonly SYEngine.Playlist playlist = new SYEngine.Playlist(SYEngine.PlaylistTypes.NetworkHttp);
#endif
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/69.0.3497.100 Safari/537.36";

        public void Append(string url, int size, float duration)
        {
#if ARM64
            urls.Add(url);
#else
            playlist.Append(url, size, duration);
#endif
        }

        public void SetNetworkHeaders(string referer)
        {
#if ARM64
            this.referer = referer;
#else
            playlist.NetworkConfigs = new SYEngine.PlaylistNetworkConfigs
            {
                DownloadRetryOnFail = true,
                HttpCookie = string.Empty,
                UniqueId = string.Empty,
                HttpUserAgent = UserAgent,
                HttpReferer = referer
            };
#endif
        }

        public async Task<IMediaPlaybackSource> CreateMediaSourceAsync(ReturnPlayModel result)
        {
#if ARM64
            // 多项 MediaPlaybackList 的进度会在每段归零，不能直接替代连续的 SYEngine 时间轴。
            if (urls.Count != 1)
            {
                result.errorMessage = "ARM64 暂不支持传统多段流，请在设置中开启 DASH 后重试。";
                return null;
            }
            result.ffmpegDashSource = await FFmpegDashSource.CreateAsync(
                urls[0], null, LegacyPlayback.ForceSoftwareDecode, referer, UserAgent);
            result.usePlayMode = UsePlayMode.FFmpeg;
            return result.ffmpegDashSource?.CreateVideoPlaybackItem();
#else
            return MediaSource.CreateFromUri(await playlist.SaveAndGetFileUriAsync());
#endif
        }
    }
}
