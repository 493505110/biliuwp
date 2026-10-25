using BiliBili.UWP.Helper;
using BiliBili.UWP.Modules.Playback;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace BiliBili.UWP.Controls
{
    public sealed class LiveMediaFailedEventArgs : EventArgs
    {
        public string ErrorMessage { get; }
        public LiveMediaFailedEventArgs(string message) { ErrorMessage = message; }
    }

    // 保留直播页的操作接口，各平台统一使用 MediaPlayer 和 FFmpegInteropX。
    public sealed class LiveMediaElement : UserControl
    {
        public event RoutedEventHandler CurrentStateChanged;
        public event RoutedEventHandler MediaEnded;
        public event EventHandler<LiveMediaFailedEventArgs> MediaFailed;
        public event RoutedEventHandler SourceChanged;
        public IDictionary<string, object> Options { get; } = new Dictionary<string, object>();
        private string source;
        private List<string> sources = new List<string>();
        private LivePlaybackFallback fallback;
        private readonly MediaPlayerElement element = new MediaPlayerElement { AreTransportControlsEnabled = false };
        private MediaPlayer player;
        private FFmpegDashSource ffmpegSource;
        private int sourceVersion;
        private bool unloaded;
        private bool opening;
        private bool playRequested;
        private bool isMuted;
        private MediaElementState currentState = MediaElementState.Closed;

        public LiveMediaElement()
        {
            Content = element;
            Loaded += (s, e) =>
            {
                unloaded = false;
                if (player == null && !string.IsNullOrEmpty(source)) StartSource();
            };
            Unloaded += (s, e) =>
            {
                unloaded = true;
                Stop();
            };
        }

        public string Source
        {
            get => source;
            set
            {
                SetSources(string.IsNullOrEmpty(value) ? new string[0] : new[] { value }, value);
            }
        }

        public void SetSources(IEnumerable<string> urls, string selectedSource)
        {
            sources = new List<string>(urls);
            source = selectedSource;
            playRequested = AutoPlay;
            StartSource();
        }

        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
            nameof(Volume), typeof(double), typeof(LiveMediaElement), new PropertyMetadata(100d, OnVolumeChanged));

        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, Math.Max(0, Math.Min(100, value)));
        }

        private static void OnVolumeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var control = (LiveMediaElement)sender;
            if (control.player != null) control.player.Volume = Math.Max(0, Math.Min(1, control.Volume / 100));
        }

        public bool AutoPlay { get; set; } = true;
        public bool HardwareAcceleration { get; set; } = true;

        public bool IsMuted
        {
            get => isMuted;
            set { isMuted = value; if (player != null) player.IsMuted = value; }
        }

        public MediaElementState CurrentState
        {
            get => currentState;
        }

        public uint VideoWidth
        {
            get
            {
                return player?.PlaybackSession.NaturalVideoWidth ?? 0;
            }
        }

        public uint VideoHeight
        {
            get
            {
                return player?.PlaybackSession.NaturalVideoHeight ?? 0;
            }
        }

        public void Play()
        {
            playRequested = true;
            if (player?.Source != null) player.Play();
            else if (!opening) StartSource();
        }

        public void Pause()
        {
            playRequested = false;
            player?.Pause();
        }

        public void Stop()
        {
            ++sourceVersion;
            opening = false;
            fallback = null;
            ReleasePlayer();
            SetState(MediaElementState.Stopped);
        }

        private void ReleasePlayer()
        {
            if (player != null)
            {
                var oldPlayer = player;
                player = null;
                oldPlayer.Pause();
                oldPlayer.Source = null;
                element.SetMediaPlayer(null);
                oldPlayer.Dispose();
            }
            ffmpegSource?.Dispose();
            ffmpegSource = null;
        }

        private void StartSource()
        {
            Stop();
            if (!unloaded && !string.IsNullOrEmpty(source))
            {
                fallback = new LivePlaybackFallback(sources, source);
                _ = LoadSourceAsync(fallback, sourceVersion);
            }
        }

        private bool IsCurrent(LivePlaybackFallback request, int version)
        {
            return version == sourceVersion && !unloaded && fallback == request;
        }

        private async Task LoadSourceAsync(LivePlaybackFallback request, int version)
        {
            FFmpegDashSource createdSource = null;
            try
            {
                if (!IsCurrent(request, version)) return;
                opening = true;
                SetState(MediaElementState.Opening);
#if !FFMPEG_INTEROP_SUPPORTED
                throw new PlatformNotSupportedException("当前架构不支持 FFmpegInteropX 直播播放，请使用 x86、x64 或 ARM64 版本");
#else
                Options.TryGetValue("http-referrer", out var referer);
                Options.TryGetValue("http-user-agent", out var userAgent);
                var softwareDecode = !HardwareAcceleration;
                createdSource = await request.OpenNextAsync(
                    url => FFmpegDashSource.CreateAsync(url, null, softwareDecode,
                        referer?.ToString(), userAgent?.ToString(), isLive: true),
                    () => IsCurrent(request, version),
                    url =>
                    {
                        source = url;
                        SourceChanged?.Invoke(this, new RoutedEventArgs());
                    });
                if (!IsCurrent(request, version) || createdSource == null) return;
                if (player == null)
                {
                    player = new MediaPlayer { AutoPlay = false };
                    var currentPlayer = player;
                    player.CommandManager.IsEnabled = false;
                    player.PlaybackSession.PlaybackStateChanged += async (s, e) =>
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            if (player != currentPlayer || player.Source == null) return;
                            switch (player.PlaybackSession.PlaybackState)
                            {
                                case MediaPlaybackState.Opening: SetState(MediaElementState.Opening); break;
                                case MediaPlaybackState.Buffering: SetState(MediaElementState.Buffering); break;
                                case MediaPlaybackState.Playing: SetState(MediaElementState.Playing); break;
                                case MediaPlaybackState.Paused: SetState(MediaElementState.Paused); break;
                            }
                        });
                    };
                    player.MediaEnded += async (s, e) => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        if (player == currentPlayer) MediaEnded?.Invoke(this, new RoutedEventArgs());
                    });
                    player.MediaFailed += async (s, e) => await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        if (player != currentPlayer || player.Source == null || !IsCurrent(request, version)) return;
                        var error = new LivePlaybackException("播放直播源失败（" + e.Error + "，HRESULT=0x"
                            + (e.ExtendedErrorCode?.HResult ?? unchecked((int)0x80004005)).ToString("X8") + "）\r\n"
                            + FFmpegDashSource.RedactQueryString(e.ErrorMessage ?? "未知错误"), e.ExtendedErrorCode);
                        request.ReportFailure(error);
                        LogHelper.WriteLog(error.Message, LogType.ERROR);
                        ReleasePlayer();
                        _ = LoadSourceAsync(request, version);
                    });
                    element.SetMediaPlayer(player);
                }
                player.Volume = Math.Max(0, Math.Min(1, Volume / 100));
                player.IsMuted = isMuted;
                ffmpegSource = createdSource;
                createdSource = null;
                player.Source = ffmpegSource.CreateVideoPlaybackItem();
                if (playRequested) player.Play();
#endif
            }
            catch (Exception ex)
            {
                if (IsCurrent(request, version))
                {
                    Stop();
                    var message = ex.Message + "\r\nHRESULT=0x" + ex.HResult.ToString("X8");
                    LogHelper.WriteLog(message, LogType.ERROR);
                    MediaFailed?.Invoke(this, new LiveMediaFailedEventArgs(message));
                }
            }
            finally
            {
                createdSource?.Dispose();
                if (IsCurrent(request, version)) opening = false;
            }
        }

        private sealed class LivePlaybackException : Exception
        {
            public LivePlaybackException(string message, Exception inner) : base(message, inner)
            {
                HResult = inner?.HResult ?? unchecked((int)0x80004005);
            }
        }

        private void SetState(MediaElementState state)
        {
            if (currentState == state) return;
            currentState = state;
            CurrentStateChanged?.Invoke(this, new RoutedEventArgs());
        }
    }
}
