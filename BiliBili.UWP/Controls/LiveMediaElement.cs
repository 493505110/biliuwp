using BiliBili.UWP.Helper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
#if ARM64
using Windows.Media.Playback;
using Windows.UI.Core;
#endif

namespace BiliBili.UWP.Controls
{
    public sealed class LiveMediaFailedEventArgs : EventArgs
    {
        public string ErrorMessage { get; }
        public LiveMediaFailedEventArgs(string message) { ErrorMessage = message; }
    }

    // 保留直播页的操作接口，ARM64 使用有原生实现的 FFmpegInteropX。
    public sealed class LiveMediaElement : UserControl
    {
        public event RoutedEventHandler CurrentStateChanged;
        public event RoutedEventHandler MediaEnded;
        public event EventHandler<LiveMediaFailedEventArgs> MediaFailed;
#if ARM64
        public IDictionary<string, object> Options { get; } = new Dictionary<string, object>();
#else
        public IDictionary<string, object> Options => player.Options;
#endif
        private string source;
#if ARM64
        private readonly MediaPlayerElement element = new MediaPlayerElement { AreTransportControlsEnabled = false };
        private MediaPlayer player;
        private FFmpegDashSource ffmpegSource;
        private int sourceVersion;
        private bool unloaded;
        private bool opening;
        private bool playRequested;
        private bool isMuted;
        private MediaElementState currentState = MediaElementState.Closed;
#else
        private readonly VLC.MediaElement player = new VLC.MediaElement();
#endif

        public LiveMediaElement()
        {
#if ARM64
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
#else
            Content = player;
            player.CurrentStateChanged += (s, e) => CurrentStateChanged?.Invoke(this, e);
            player.MediaEnded += (s, e) => MediaEnded?.Invoke(this, e);
            player.MediaFailed += (s, e) => MediaFailed?.Invoke(this, new LiveMediaFailedEventArgs(e.ErrorMessage));
#endif
        }

        public string Source
        {
            get => source;
            set
            {
                source = value;
#if ARM64
                playRequested = AutoPlay;
                StartSource();
#else
                player.Source = value;
                if (value == null) player.MediaSource = null;
#endif
            }
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
#if ARM64
            if (control.player != null) control.player.Volume = Math.Max(0, Math.Min(1, control.Volume / 100));
#else
            control.player.Volume = (int)control.Volume;
#endif
        }

#if ARM64
        public bool AutoPlay { get; set; } = true;
        public bool HardwareAcceleration { get; set; } = true;
#else
        public bool AutoPlay
        {
            get => player.AutoPlay;
            set => player.AutoPlay = value;
        }

        public bool HardwareAcceleration
        {
            get => player.HardwareAcceleration;
            set => player.HardwareAcceleration = value;
        }
#endif

        public bool IsMuted
        {
#if ARM64
            get => isMuted;
            set { isMuted = value; if (player != null) player.IsMuted = value; }
#else
            get => player.IsMuted;
            set => player.IsMuted = value;
#endif
        }

        public MediaElementState CurrentState
        {
#if ARM64
            get => currentState;
#else
            get => player.CurrentState;
#endif
        }

        public uint VideoWidth
        {
            get
            {
#if ARM64
                return player?.PlaybackSession.NaturalVideoWidth ?? 0;
#else
                uint width, height;
                player.MediaPlayer.size(0, out width, out height);
                return width;
#endif
            }
        }

        public uint VideoHeight
        {
            get
            {
#if ARM64
                return player?.PlaybackSession.NaturalVideoHeight ?? 0;
#else
                uint width, height;
                player.MediaPlayer.size(0, out width, out height);
                return height;
#endif
            }
        }

        public void Play()
        {
#if ARM64
            playRequested = true;
            if (player?.Source != null) player.Play();
            else if (!opening) StartSource();
#else
            player.Play();
#endif
        }

        public void Pause()
        {
#if ARM64
            playRequested = false;
            player?.Pause();
#else
            player.Pause();
#endif
        }

        public void Stop()
        {
#if ARM64
            ++sourceVersion;
            opening = false;
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
            SetState(MediaElementState.Stopped);
#else
            player.Stop();
#endif
        }

#if ARM64
        private void StartSource()
        {
            Stop();
            if (!unloaded && !string.IsNullOrEmpty(source))
            {
                opening = true;
                _ = LoadSourceAsync(source, sourceVersion);
            }
        }

        private async Task LoadSourceAsync(string url, int version)
        {
            FFmpegDashSource createdSource = null;
            try
            {
                SetState(MediaElementState.Opening);
                Options.TryGetValue("http-referrer", out var referer);
                Options.TryGetValue("http-user-agent", out var userAgent);
                createdSource = await FFmpegDashSource.CreateAsync(
                    url, null, !HardwareAcceleration, referer?.ToString(), userAgent?.ToString());
                if (version != sourceVersion || unloaded) return;
                if (createdSource == null) throw new InvalidOperationException("无法创建直播播放源");
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
                        if (player == currentPlayer && player.Source != null)
                            MediaFailed?.Invoke(this, new LiveMediaFailedEventArgs(e.ErrorMessage));
                    });
                    element.SetMediaPlayer(player);
                }
                player.Volume = Math.Max(0, Math.Min(1, Volume / 100));
                player.IsMuted = isMuted;
                ffmpegSource = createdSource;
                createdSource = null;
                player.Source = ffmpegSource.CreateVideoPlaybackItem();
                if (playRequested) player.Play();
            }
            catch (Exception ex)
            {
                if (version == sourceVersion && !unloaded)
                {
                    Stop();
                    MediaFailed?.Invoke(this, new LiveMediaFailedEventArgs(ex.Message));
                }
            }
            finally
            {
                createdSource?.Dispose();
                if (version == sourceVersion) opening = false;
            }
        }

        private void SetState(MediaElementState state)
        {
            if (currentState == state) return;
            currentState = state;
            CurrentStateChanged?.Invoke(this, new RoutedEventArgs());
        }
#endif
    }
}
