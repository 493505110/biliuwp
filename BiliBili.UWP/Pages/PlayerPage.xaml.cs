using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.Media.Core;
using Newtonsoft.Json.Linq;
using Windows.Media.Editing;
using BiliBili.UWP.Helper;
using NSDanmaku.Helper;
using BiliBili.UWP.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using SYEngine;
using System.Diagnostics;
using BiliBili.UWP.Modules;
using BiliBili.UWP.Modules.Detail;
using BiliBili.UWP.Modules.Playback;
using BiliBili.UWP.Pages.User;
using BiliBili.UWP.Api;
using Windows.Media.Streaming.Adaptive;
using Windows.Media.MediaProperties;
using System.Numerics;
using BiliBili.UWP.Models;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace BiliBili.UWP.Pages
{
    public enum PlayMode
    {
        Bangumi,
        Movie,
        VipBangumi,
        Video,
        QQ,
        Sohu,
        Local,
        FormLocal
    }
    enum HeartBeatType
    {
        Start,
        Play,
        End
    }
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class PlayerPage : Page
    {
        MediaPlayer mediaPlayer;
        MediaPlayer mediaPlayer_audio;
        FFmpegDashSource ffmpegDashSource;
        PlayerAPI playerAPI;
        readonly PlaybackRequestGate playbackRequestGate = new PlaybackRequestGate();
        CancellationTokenSource danmakuLoadCancellation;
        int pendingPlaybackRequest;
        PlaybackRestoreState pendingPlaybackRestoreState;
        int subtitleLoadVersion;
        int biliJumpLoadVersion;
        int interactiveDanmakuLoadVersion;
        List<BiliJumpAdSegment> biliJumpAds = new List<BiliJumpAdSegment>();
        BiliJumpAdSegment biliJumpCurrentAd;
        string biliJumpLastNotifiedKey;
        string biliJumpLastHandledKey;
        private const double BiliJumpMinimumDurationSeconds = 150;
        bool _isExiting = false;//退出页面标志,防止3秒延迟后仍播放下一集
        public PlayerPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Disabled;
            InitMediaPlayer();
            danmakuParse = new DanmakuParse();
            playerAPI = new PlayerAPI();
            MTC.DanmuLoaded += MTC_DanmuLoaded;
        }

        private void InitMediaPlayer()
        {
            mediaPlayer = new MediaPlayer();
            mediaPlayer.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
            mediaPlayer.PlaybackSession.NaturalVideoSizeChanged += PlaybackSession_NaturalVideoSizeChanged;
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            mediaPlayer.VolumeChanged += MediaPlayer_VolumeChanged;
            mediaPlayer.PlaybackSession.BufferingProgressChanged += PlaybackSession_BufferingProgressChanged;
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
            mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            mediaElement.SetMediaPlayer(mediaPlayer);
        }

        private void DetachMediaPlayerEvents(MediaPlayer player)
        {
            if (player == null)
            {
                return;
            }

            player.PlaybackSession.PositionChanged -= PlaybackSession_PositionChanged;
            player.PlaybackSession.NaturalVideoSizeChanged -= PlaybackSession_NaturalVideoSizeChanged;
            player.MediaOpened -= MediaPlayer_MediaOpened;
            player.VolumeChanged -= MediaPlayer_VolumeChanged;
            player.PlaybackSession.BufferingProgressChanged -= PlaybackSession_BufferingProgressChanged;
            player.MediaEnded -= MediaPlayer_MediaEnded;
            player.MediaFailed -= MediaPlayer_MediaFailed;
            player.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
        }

        private void DisposeAuxiliaryMediaPlayer()
        {
            var player = mediaPlayer_audio;
            mediaPlayer_audio = null;
            if (player == null)
            {
                return;
            }

            try
            {
                player.Pause();
                player.Source = null;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("释放辅助音频播放器失败", LogType.ERROR, ex);
            }
            finally
            {
                try
                {
                    player.Dispose();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("销毁辅助音频播放器失败", LogType.ERROR, ex);
                }
            }
        }

        private void DisposeMediaPlayer(bool releaseSystemMediaControls)
        {
            var player = mediaPlayer;
            mediaPlayer = null;
            DisposeAuxiliaryMediaPlayer();
            if (player != null)
            {
                try
                {
                    DetachMediaPlayerEvents(player);
                    player.Pause();
                    if (releaseSystemMediaControls)
                    {
                        ReleaseSystemMediaTransportControls(player);
                    }
                    player.Source = null;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("释放视频播放器失败", LogType.ERROR, ex);
                }
                finally
                {
                    ReleaseFFmpegDashSource();
                    try
                    {
                        mediaElement.SetMediaPlayer(null);
                        player.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLog("销毁视频播放器失败", LogType.ERROR, ex);
                    }
                }
            }
            else
            {
                ReleaseFFmpegDashSource();
            }
        }

        /// <summary>重置 MediaPlayer:MediaFailed 后同一实例无法通过重设 Source 恢复,必须重建实例。</summary>
        private void ResetMediaPlayer()
        {
            try
            {
                var volume = mediaPlayer?.Volume ?? SettingHelper.Get_Volume();
                DisposeMediaPlayer(true);
                InitMediaPlayer();
                SetVolume(volume);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("重建视频播放器失败", LogType.ERROR, ex);
            }
        }

        private void MediaPlayer_VolumeChanged(MediaPlayer sender, object args)
        {
            if (!ReferenceEquals(sender, mediaPlayer))
            {
                return;
            }

            var audioPlayer = mediaPlayer_audio;
            if (audioPlayer == null)
            {
                return;
            }

            try
            {
                audioPlayer.Volume = sender.Volume;
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(audioPlayer, mediaPlayer_audio))
                {
                    LogHelper.WriteLog("同步辅助音频音量失败", LogType.ERROR, ex);
                }
            }
        }

        private void PlaybackSession_PositionChanged(MediaPlaybackSession sender, object args)
        {
            if (mediaPlayer == null || !ReferenceEquals(sender, mediaPlayer.PlaybackSession))
            {
                return;
            }

            try
            {
                if (Dispatcher.HasThreadAccess)
                {
                    HandleBiliJumpPosition();
                    HandleInteractiveDanmakuPosition();
                }
                else
                {
                    _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        try
                        {
                            if (mediaPlayer != null && ReferenceEquals(sender, mediaPlayer.PlaybackSession))
                            {
                                HandleBiliJumpPosition();
                                HandleInteractiveDanmakuPosition();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.WriteLog("处理 BiliJump AI 播放位置失败", LogType.ERROR, ex);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("处理 BiliJump AI 播放位置失败", LogType.ERROR, ex);
            }

            var audioPlayer = mediaPlayer_audio;
            if (audioPlayer == null)
            {
                return;
            }

            try
            {
                var audioSession = audioPlayer.PlaybackSession;
                var position = sender.Position;
                if (Math.Abs(position.TotalSeconds - audioSession.Position.TotalSeconds) > 1)
                {
                    audioSession.Position = position;
                }
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(audioPlayer, mediaPlayer_audio))
                {
                    LogHelper.WriteLog("同步辅助音频位置失败", LogType.ERROR, ex);
                }
            }
        }

        private async void PlaybackSession_NaturalVideoSizeChanged(MediaPlaybackSession sender, object args)
        {
            if (mediaPlayer == null || !ReferenceEquals(sender, mediaPlayer.PlaybackSession))
            {
                return;
            }

            var callbackRequest = playbackRequestGate.Current;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (mediaPlayer == null
                    || !ReferenceEquals(sender, mediaPlayer.PlaybackSession)
                    || !playbackRequestGate.IsCurrent(callbackRequest))
                {
                    return;
                }

                txt_VideoWidth.Text = sender.NaturalVideoWidth.ToString();
                txt_VideoHeight.Text = sender.NaturalVideoHeight.ToString();
            });
        }




        #region MediaPlayer事件
        private async void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
        {
            if (!ReferenceEquals(sender, mediaPlayer))
            {
                return;
            }

            var callbackRequest = playbackRequestGate.Current;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {

                try
                {
                    if (!ReferenceEquals(sender, mediaPlayer) || !playbackRequestGate.IsCurrent(callbackRequest))
                    {
                        return;
                    }

                    SetSystemMediaTransportControl();
                    MTC_Video360Changed(this, MTC.Video360);
                    if (pendingPlaybackRestoreState != null && playbackRequestGate.IsCurrent(pendingPlaybackRequest))
                    {
                        var restoreState = pendingPlaybackRestoreState;
                        pendingPlaybackRestoreState = null;
                        sender.PlaybackSession.Position = PlaybackPosition.Clamp(restoreState.Position, sender.PlaybackSession.NaturalDuration);
                        if (restoreState.ShouldPlay)
                        {
                            sender.Play();
                        }
                        else
                        {
                            sender.Pause();
                        }
                        return;
                    }
                    var record = SqlHelper.GetVideoWatchRecord(string.IsNullOrEmpty(playNow.episode_id) ? playNow.Mid : "ep" + playNow.episode_id);
                    if (record != null && record.Post != 0)
                    {
                        if (SettingHelper.Get_SkipToHistory())
                        {
                            var session = sender.PlaybackSession;
                            session.Position = PlaybackPosition.Clamp(TimeSpan.FromSeconds(record.Post), session.NaturalDuration);
                        }
                        else
                        {
                            TimeSpan ts = new TimeSpan(0, 0, record.Post);
                            LastPost = record.Post;
                            btn_ViewPost.Content = "上次播放到" + ts.Hours.ToString("00") + ":" + ts.Minutes.ToString("00") + ":" + ts.Seconds.ToString("00");
                            btn_ViewPost.Visibility = Visibility.Visible;
                            await Task.Delay(5000);
                            if (!ReferenceEquals(sender, mediaPlayer)
                                || !playbackRequestGate.IsCurrent(callbackRequest))
                            {
                                return;
                            }
                            btn_ViewPost.Visibility = Visibility.Collapsed;
                        }
                    }


                }
                catch (Exception)
                {

                }
            });
        }
        private async void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            if (mediaPlayer == null || !ReferenceEquals(sender, mediaPlayer.PlaybackSession))
            {
                return;
            }

            var callbackRequest = playbackRequestGate.Current;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (mediaPlayer == null
                    || !ReferenceEquals(sender, mediaPlayer.PlaybackSession)
                    || !playbackRequestGate.IsCurrent(callbackRequest))
                {
                    return;
                }

                buffering = false;
                switch (sender.PlaybackState)
                {
                    //case  MediaPlaybackState.Closed:
                    //    if (_systemMediaTransportControls != null)
                    //    {
                    //        _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Closed;
                    //    }


                    //    break;
                    case MediaPlaybackState.Opening:

                        progress.Visibility = Visibility.Visible;

                        break;
                    case MediaPlaybackState.Buffering:
                        buffering = true;
                        progress.Visibility = Visibility.Visible;
                        mediaPlayer_audio?.Pause();
                        danmu?.PauseDanmaku();
                        break;
                    case MediaPlaybackState.Playing:
                        mediaPlayer.PlaybackSession.PlaybackRate = slider_Rate.Value;
                        if (mediaPlayer_audio != null)
                        {
                            mediaPlayer_audio.PlaybackSession.PlaybackRate = mediaPlayer.PlaybackSession.PlaybackRate;
                            mediaPlayer_audio.Play();
                        }
                        progress.Visibility = Visibility.Collapsed;
                        danmu?.ResumeDanmaku();

                        if (timer != null)
                        {
                            timer.Start();
                        }

                        break;
                    case MediaPlaybackState.Paused:
                        progress.Visibility = Visibility.Collapsed;
                        danmu?.PauseDanmaku();
                        if (timer != null)
                        {
                            timer.Stop();
                        }
                        mediaPlayer_audio?.Pause();
                        break;
                    //case MediaPlaybackState.Stopped:
                    //    if (_systemMediaTransportControls != null)
                    //    {
                    //        _systemMediaTransportControls.PlaybackStatus = MediaPlaybackStatus.Stopped;
                    //    }

                    //    progress.Visibility = Visibility.Collapsed;
                    //    danmu.ClearAll();
                    //    if (timer != null)
                    //    {
                    //        timer.Stop();
                    //    }

                    //    break;
                    default:
                        break;
                }
            });

        }
        private async void PlaybackSession_BufferingProgressChanged(MediaPlaybackSession sender, object args)
        {
            if (mediaPlayer == null || !ReferenceEquals(sender, mediaPlayer.PlaybackSession))
            {
                return;
            }

            var callbackRequest = playbackRequestGate.Current;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (mediaPlayer == null
                    || !ReferenceEquals(sender, mediaPlayer.PlaybackSession)
                    || !playbackRequestGate.IsCurrent(callbackRequest))
                {
                    return;
                }

                var progressText = sender.BufferingProgress.ToString("P");
                pr.Text = progressText;
                txt_BufferingProgress.Text = progressText;
            });
        }
        private async void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            if (!ReferenceEquals(sender, mediaPlayer))
            {
                return;
            }

            var callbackRequest = playbackRequestGate.Current;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (!ReferenceEquals(sender, mediaPlayer) || !playbackRequestGate.IsCurrent(callbackRequest))
                {
                    return;
                }

                await new MessageDialog($"无法播放此视频 ＞﹏＜ \r\n{args.Error.ToString()}: {args.ExtendedErrorCode?.Message ?? "未知错误"}\r\n请尝试更换清晰度或者在播放设置中打开/关闭DASH").ShowAsync();
                // 失败后重建 MediaPlayer,避免同一实例的失败状态污染后续所有播放
                if (ReferenceEquals(sender, mediaPlayer) && playbackRequestGate.IsCurrent(callbackRequest))
                {
                    ResetMediaPlayer();
                }
            });
        }
        private async void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            if (!ReferenceEquals(sender, mediaPlayer))
            {
                return;
            }

            var callbackRequest = playbackRequestGate.Current;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    if (!ReferenceEquals(sender, mediaPlayer) || !playbackRequestGate.IsCurrent(callbackRequest))
                    {
                        return;
                    }

                    if (cb_setting_1.IsChecked.Value)
                    {
                        var audioPlayer = mediaPlayer_audio;
                        if (audioPlayer != null)
                        {
                            // 重置辅助音频播放器,避免 FFmpeg 双播放器模式下第二轮循环无声或音频停在结束状态
                            audioPlayer.Pause();
                            audioPlayer.PlaybackSession.Position = TimeSpan.Zero;
                            audioPlayer.Play();
                        }
                        mediaElement.MediaPlayer.Play();
                        danmu?.ClearAll();
                        return;
                    }
                    if (gv_play.SelectedIndex == gv_play.Items.Count - 1)
                    {
                        if (playNow.isInteraction)
                        {
                            if (nodeInfo.edges != null)
                            {
                                if (nodeInfo.edges.choices.Count == 1)
                                {
                                    ChangeNode(nodeInfo.edges.choices[0].node_id, nodeInfo.edges.choices[0].cid.ToString());
                                }
                                else
                                {
                                    gridview_node.Visibility = Visibility.Visible;
                                }
                            }
                            else
                            {
                                Utils.ShowMessageToast("互动视频已结束，可点击右下角选择节点重新开始", 3000);
                            }

                        }
                        else
                        {
                            if (cb_setting_2.IsChecked.Value)
                            {
                                gv_play.SelectedIndex = 0;
                            }
                            else
                            {
                                Utils.ShowMessageToast("全部看完了", 3000);
                                mediaPlayer_audio?.Pause();
                            }
                        }
                    }
                    else
                    {
                        //mediaElement.MediaPlayer.PlaybackSession.();
                        Utils.ShowMessageToast("3秒后播放下一集", 3000);
                        await Task.Delay(3000);
                        //等待期间用户可能已退出播放页,不再继续播放下一集
                        if (_isExiting
                            || !ReferenceEquals(sender, mediaPlayer)
                            || !playbackRequestGate.IsCurrent(callbackRequest))
                        {
                            return;
                        }
                        gv_play.SelectedIndex += 1;
                    }
                }
                catch (Exception)
                {
                }
            });

        }

        #endregion
        private void MTC_DanmuLoaded(object sender, NSDanmaku.Controls.Danmaku e)
        {
            if (e != null)
            {
                danmu = e;
            }
        }

        private void PlayerPage_KeyDown(CoreWindow sender, KeyEventArgs args)
        {

            args.Handled = true;
            if (sp_View.IsPaneOpen)
            {
                return;
            }
            switch (args.VirtualKey)
            {
                case Windows.System.VirtualKey.A:
                    if (MTC.Video360)
                        mediaPlayer.PlaybackSession.SphericalVideoProjection.ViewOrientation *= Quaternion.CreateFromYawPitchRoll(.05f, 0, 0);
                    break;
                case Windows.System.VirtualKey.S:
                    if (MTC.Video360)
                        mediaPlayer.PlaybackSession.SphericalVideoProjection.ViewOrientation *= Quaternion.CreateFromYawPitchRoll(0, 0, .05f);
                    break;
                case Windows.System.VirtualKey.W:
                    if (MTC.Video360)
                        mediaPlayer.PlaybackSession.SphericalVideoProjection.ViewOrientation *= Quaternion.CreateFromYawPitchRoll(0, 0, -.05f);
                    break;
                case Windows.System.VirtualKey.D:
                    if (MTC.Video360)
                        mediaPlayer.PlaybackSession.SphericalVideoProjection.ViewOrientation *= Quaternion.CreateFromYawPitchRoll(-.05f, 0, 0);
                    break;

                case Windows.System.VirtualKey.Space:
                    if (mediaElement.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                    {
                        mediaElement.MediaPlayer.Pause();
                    }
                    else
                    {
                        mediaElement.MediaPlayer.Play();
                    }
                    break;
                case Windows.System.VirtualKey.Left:
                    var backwardSession = mediaElement.MediaPlayer.PlaybackSession;
                    backwardSession.Position = PlaybackPosition.Clamp(
                        backwardSession.Position.Subtract(TimeSpan.FromSeconds(3)),
                        backwardSession.NaturalDuration);
                    Utils.ShowMessageToast(mediaElement.MediaPlayer.PlaybackSession.Position.Hours.ToString("00") + ":" + mediaElement.MediaPlayer.PlaybackSession.Position.Minutes.ToString("00") + ":" + mediaElement.MediaPlayer.PlaybackSession.Position.Seconds.ToString("00"), 3000);
                    break;
                case Windows.System.VirtualKey.Up:
                    {
                        var volume = mediaElement.MediaPlayer.Volume + 0.1;
                        SetVolume(volume);
                        Utils.ShowMessageToast("音量:" + mediaElement.MediaPlayer.Volume.ToString("P"), 3000);
                    }
                    break;
                case Windows.System.VirtualKey.Right:
                    var forwardSession = mediaElement.MediaPlayer.PlaybackSession;
                    forwardSession.Position = PlaybackPosition.Clamp(
                        forwardSession.Position.Add(TimeSpan.FromSeconds(3)),
                        forwardSession.NaturalDuration);
                    Utils.ShowMessageToast(mediaElement.MediaPlayer.PlaybackSession.Position.Hours.ToString("00") + ":" + mediaElement.MediaPlayer.PlaybackSession.Position.Minutes.ToString("00") + ":" + mediaElement.MediaPlayer.PlaybackSession.Position.Seconds.ToString("00"), 3000);
                    break;
                case Windows.System.VirtualKey.Down:
                    {
                        var volume = mediaElement.MediaPlayer.Volume - 0.1;
                        SetVolume(volume);
                        Utils.ShowMessageToast("音量:" + mediaElement.MediaPlayer.Volume.ToString("P"), 3000);
                    }
                    break;
                case Windows.System.VirtualKey.Escape:
                    if (MTC.IsFullWindow)
                    {
                        ApplicationView.GetForCurrentView().ExitFullScreenMode();
                    }
                    break;

                case Windows.System.VirtualKey.F11:
                case Windows.System.VirtualKey.Enter:
                    if (!MTC.IsFullWindow)
                    {
                        MTC.ToFull();
                        //ApplicationView.GetForCurrentView().TryEnterFullScreenMode();

                    }
                    else
                    {
                        MTC.ExitFull();
                        //ApplicationView.GetForCurrentView().ExitFullScreenMode();

                    }
                    break;
                //跳过OP 90秒
                case Windows.System.VirtualKey.O:
                case Windows.System.VirtualKey.P:
                    {
                        var session = mediaElement.MediaPlayer.PlaybackSession;
                        session.Position = PlaybackPosition.Clamp(
                            session.Position.Add(TimeSpan.FromSeconds(90)),
                            session.NaturalDuration);
                        Utils.ShowMessageToast(session.Position.Hours.ToString("00") + ":" + session.Position.Minutes.ToString("00") + ":" + session.Position.Seconds.ToString("00"), 3000);
                    }
                    break;
                //打开关闭弹幕
                case Windows.System.VirtualKey.I:
                case Windows.System.VirtualKey.F9:
                    {
                        MTC.OpenOrCloseDanmaku();
                    }
                    break;
                //上一话
                case (Windows.System.VirtualKey)188:
                case Windows.System.VirtualKey.N:
                    if (gv_play.SelectedIndex == 0)
                    {
                        Utils.ShowMessageToast("前面没有了");
                        return;
                    }
                    gv_play.SelectedIndex -= 1;
                    break;
                //下一话
                case (Windows.System.VirtualKey)190:
                case Windows.System.VirtualKey.M:
                    if (gv_play.SelectedIndex == gv_play.Items.Count - 1)
                    {
                        Utils.ShowMessageToast("后面没有了");
                        return;
                    }
                    gv_play.SelectedIndex += 1;
                    break;
                case Windows.System.VirtualKey.F8:
                case Windows.System.VirtualKey.F10:
                    CaptureVideo();
                    break;
                default:
                    break;
            }
        }



        private void ReleaseSystemMediaTransportControls()
        {
            ReleaseSystemMediaTransportControls(mediaPlayer);
        }

        private void ReleaseSystemMediaTransportControls(MediaPlayer player)
        {
            if (player != null)
            {
                player.CommandManager.IsEnabled = false;
                var controls = player.SystemMediaTransportControls;
                controls.PlaybackStatus = MediaPlaybackStatus.Closed;
                controls.DisplayUpdater.ClearAll();
                controls.IsEnabled = false;
            }
        }

        private DisplayRequest dispRequest = null;//保持屏幕常亮
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            CoreWindow.GetForCurrentThread().KeyDown += PlayerPage_KeyDown;
            this.Frame.Visibility = Visibility.Visible;
            int flag = 1;
            while (true)
            {
                if (_isExiting)
                {
                    return;
                }
                if (danmu != null)
                {
                    break;
                }
                if (flag >= 100)
                {
                    MessageDialog messageDialog = new MessageDialog("播放组件似乎加载失败了,是否报告开发者？");
                    messageDialog.Commands.Add(new UICommand("确定", (sender) => { LogHelper.WriteLog("无法初始化播放器", LogType.ERROR); }));
                    messageDialog.Commands.Add(new UICommand("取消"));
                    await messageDialog.ShowAsync();
                    flag = 1;
                }
                await Task.Delay(100);
                if (_isExiting)
                {
                    return;
                }
                flag++;
            }

            // CheckNetwork();
            //await Task.sp_View(200);
            if (e.NavigationMode == NavigationMode.New)
            {
                object[] obj = e.Parameter as object[];
                var ls = obj[0] as List<PlayerModel>;
                var index = (int)obj[1];

                LoadPlayer(ls, index);
                sp_View.Focus(FocusState.Pointer);
            }

        }
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            try
            {
                _isExiting = true;
                playbackRequestGate.Invalidate();
                CancelDanmakuLoading();
                pendingPlaybackRestoreState = null;
                mediaPlayer?.Pause();
                _ = ClosePlayerAsync();
                //Debug.WriteLine("开始返回");
                CoreWindow.GetForCurrentThread().KeyDown -= PlayerPage_KeyDown;
                this.Frame.Visibility = Visibility.Collapsed;
                MusicHelper.ActivatePausedMusic();

                //mediaElement.Stop();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("关闭播放页失败", LogType.ERROR, ex);
            }
        }

        private async Task CheckNetwork()
        {
            if (SystemHelper.GetNetWorkType() == NetworkType.Other && SettingHelper.Get_Use4GPlay())
            {
                var md = new MessageDialog("当前使用的是数据网络，是否继续播放？\r\n可到设置中关闭此提醒", "播放询问");
                md.Commands.Add(new UICommand("确定", new UICommandInvokedHandler((e) => { })));
                md.Commands.Add(new UICommand("取消", new UICommandInvokedHandler((e) => { this.Frame.GoBack(); return; })));
                await md.ShowAsync();
            }
        }


        private NSDanmaku.Controls.Danmaku danmu;
        DanmakuParse danmakuParse;
        DispatcherTimer timer;
        DispatcherTimer timer_Date;
        List<PlayerModel> playList;
        List<NSDanmaku.Model.DanmakuModel> DanMuPool = null;
        List<InteractiveDanmakuModel> interactiveDanmakuPool = new List<InteractiveDanmakuModel>();
        InteractiveDanmakuModel currentInteractiveDanmaku;
        PlaybackEventTimeline<NSDanmaku.Model.DanmakuModel> danmakuTimeline;
        int danmakuLimitSecond = -1;
        int danmakuLimitCount;
        int mergeDanmakuSecond = -1;
        PlayerModel playNow;
        InteractionVideo interactionVideo;
        NodeInfo nodeInfo;
        //int _index = 0;

        bool LoadDanmu = true;
        int LastPost = 0;
        bool settingFlag = true;
        BrightnessOverride bo;
        double _brightness;
        double Brightness
        {
            get => _brightness;
            set
            {
                _brightness = value;
                //if (bo != null && bo.IsSupported)
                //{
                //    // 0-dark => 1-light
                //    bo.SetBrightnessLevel(1 - value, DisplayBrightnessOverrideOptions.None);
                //}
                //else
                //{
                // 0-light => 1-dark
                MTC.Brightness = value;
                //}
            }
        }

        public async void LoadPlayer(List<PlayerModel> par, int index)
        {


            await Task.Delay(200);
            if (_isExiting)
            {
                return;
            }
            danmu = MTC.myDanmaku;

            UpdateSetting();
            //if (timer == null)
            //{
            //    timer = new DispatcherTimer();
            //    timer.Interval = new TimeSpan(0, 0, 1);
            //    timer.Tick += Timer_Tick;
            //    timer.Start();
            //}
            if (timer_Date == null)
            {
                timer_Date = new DispatcherTimer();
                timer_Date.Interval = TimeSpan.FromMilliseconds(100);
                timer_Date.Tick += Timer_Date_Tick; ;
                timer_Date.Start();
            }
            if (dispRequest == null)
            {
                // 用户观看视频，需要保持屏幕的点亮状态
                dispRequest = new DisplayRequest();
                dispRequest.RequestActive(); // 激活显示请求
            }
            if (SettingHelper.Get_QZHP())
            {
                DisplayInformation.AutoRotationPreferences = (DisplayOrientations)5;
            }

            if (SettingHelper.Get_AutoFull())
            {
                MTC.ToFull();
                //mediaElement.IsFullWindow = true;
            }

            playList = par;
            playNow = playList[index];
            if (playNow.isInteraction)
            {
                interactionVideo = new InteractionVideo(playNow.Aid, playNow.graph_version);
                nodeInfo = await interactionVideo.GetNodes(playNow.node_id);
                if (_isExiting)
                {
                    return;
                }
                gridview_node.ItemsSource = nodeInfo?.edges?.choices;
                gv_story_list.ItemsSource = nodeInfo?.story_list;
                settingStorylist = true;
                gv_story_list.SelectedItem = nodeInfo.story_list.FirstOrDefault(x => x.node_id == nodeInfo.node_id);
                settingStorylist = false;
            }

            //  btn_HideInfo.Visibility = Visibility.Collapsed;
            //   btn_ShowInfo.Visibility = Visibility.Collapsed;
            mediaElement.AutoPlay = true;

            gv_play.ItemsSource = playList;
            gv_play.SelectedIndex = index;


            //DisplayInformation.AutoRotationPreferences = (DisplayOrientations)5;

        }

        private async Task ClosePlayerAsync()
        {
            var currentItem = playNow;
            var progressValue = mediaPlayer == null
                ? 0
                : Convert.ToInt32(mediaPlayer.PlaybackSession.Position.TotalSeconds);
            try
            {
                if (dispRequest != null)
                {
                    dispRequest.RequestRelease();
                    dispRequest = null;
                }
                if (mediaPlayer != null)
                {
                    SettingHelper.Set_Volume(mediaPlayer.Volume);
                }
                SettingHelper.Set_Light(Brightness);
                ApplicationView.GetForCurrentView().ExitFullScreenMode();
                DisplayInformation.AutoRotationPreferences = DisplayOrientations.None;
                if (timer != null)
                {
                    timer.Stop();
                    timer = null;
                }
                if (timer_Date != null)
                {
                    timer_Date.Stop();
                    timer_Date.Tick -= Timer_Date_Tick;
                    timer_Date = null;
                }
                if (bo != null)
                {
                    bo.IsSupportedChanged -= Bo_IsSupportedChanged;
                    if (bo.IsOverrideActive)
                        bo.StopOverride();
                    bo = null;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("释放播放页资源失败", LogType.ERROR, ex);
            }
            finally
            {
                DisposeMediaPlayer(true);
                try
                {
                    ClearBiliJumpAds();
                    ClearSubTitle();
                    ClearInteractiveDanmaku();
                    MTC.timer2.Stop();
                    MTC.DanmuLoaded -= MTC_DanmuLoaded;
                    Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
                    DisplayInformation.AutoRotationPreferences = DisplayOrientations.None;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("释放播放页界面资源失败", LogType.ERROR, ex);
                }
            }

            if (currentItem != null)
            {
                await ReportHistory(currentItem, progressValue);
            }
        }

        /// <summary>
        /// 上传播放记录
        /// </summary>
        /// <param name="progress"></param>
        /// <returns></returns>
        private async Task ReportHistory(int progress)
        {
            await ReportHistory(playNow, progress);
        }

        private async Task ReportHistory(PlayerModel item, int progress)
        {
            try
            {
                if (item == null)
                {
                    return;
                }

                UpdateLocalHistory(item, progress);
                var api = playerAPI.SeasonHistoryReport(item.Aid, item.Mid, progress, item.banId, item.episode_id, item.playMode == PlayMode.Video ? 3 : 4);
                await api.Request();
                Debug.WriteLine(progress);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("上传播放记录失败", LogType.ERROR, ex);
            }
        }

        private void UpdateLocalHistory(int progress)
        {
            UpdateLocalHistory(playNow, progress);
        }

        private void UpdateLocalHistory(PlayerModel item, int progress)
        {
            if (item == null)
            {
                return;
            }

            var storedProgress = PlaybackHistory.GetStoredProgress(item.isInteraction, progress);
            var id = item.Mid;
            if (!string.IsNullOrEmpty(item.episode_id))
            {
                //加EP是防止EPID与CID重复
                id = "ep" + item.episode_id;
            }
            var record = SqlHelper.GetVideoWatchRecord(id);
            if (record != null)
            {
                if (storedProgress != 0)
                {
                    record.Post = storedProgress;
                }
                record.viewTime = DateTime.Now;
                SqlHelper.UpdateVideoWatchRecord(record);
            }
            else
            {
                SqlHelper.AddVideoWatchRecord(new ViewPostHelperClass()
                {
                    epId = id,
                    Post = storedProgress,
                    viewTime = DateTime.Now
                });
            }
        }


        public void UpdateSetting()
        {
            //if (!SettingHelper.IsPc())
            //{
            //    btn_ShowInfo.Visibility = Visibility.Collapsed;
            //    btn_HideInfo.Visibility = Visibility.Collapsed;
            //    // danmu.fontSize = 16;
            //}
            settingFlag = true;

            SYEngine.Core.ForceSoftwareDecode = SettingHelper.Get_ForceVideo();

            DanDis_Get();
            DMZZBDS = SettingHelper.Get_DMZZ();
            slider_DanmuSize.Value = SettingHelper.Get_NewDMSize();
            slider_Num.Value = SettingHelper.Get_DMNumber();
            slider_DanmuTran.Value = SettingHelper.Get_NewDMTran();
            slider_DanmuSpeed.Value = SettingHelper.Get_DMSpeed();
            cb_Style.SelectedIndex = SettingHelper.Get_DMStyle();

            sw_DanmuBorder.IsOn = SettingHelper.Get_DMBorder();
            sw_MergeDanmu.IsOn = SettingHelper.Get_MergeDanmu();
            mergeDanmu = sw_MergeDanmu.IsOn;

            sw_DanmuNotSubtitle.IsOn = SettingHelper.Get_DanmuNotSubtitle();
            //danmu.notHideSubtitle = sw_DanmuNotSubtitle.IsOn;

            sw_InteractiveDanmaku.IsOn = SettingHelper.Get_InteractiveDanmakuStatus();
            sw_UseNewDanmakuInterface.IsOn = SettingHelper.Get_UseNewDanmakuInterface();

            sw_BoldDanmu.IsOn = SettingHelper.Get_BoldDanmu();

            sw_UseDASH.IsOn = SettingHelper.Get_UseDASH();
            SetDASHVideoCodecSelection(SettingHelper.Get_DASHVideoCodecPreference());
            sw_DASHForceVideoCodec.IsOn = SettingHelper.Get_DASHForceVideoCodec();
            sw_ForceVideo.IsOn = SettingHelper.Get_ForceVideo();

            List<string> fonts = SystemHelper.GetSystemFontFamilies();
            cb_Font.ItemsSource = fonts;
            cb_SubtitleFont.ItemsSource = fonts;
            if (SettingHelper.Get_DanmuFont() != "")
            {
                cb_Font.SelectedIndex = fonts.IndexOf(SettingHelper.Get_DanmuFont());
            }
            else
            {
                cb_Font.SelectedIndex = fonts.IndexOf(cb_Font.FontFamily.Source);
            }
            if (SettingHelper.Get_SubtitleFontFamily() != "")
            {
                cb_SubtitleFont.SelectedIndex = fonts.IndexOf(SettingHelper.Get_SubtitleFontFamily());
            }
            else
            {
                cb_Font.SelectedIndex = fonts.IndexOf(cb_Font.FontFamily.Source);
            }

            var subColor = SettingHelper.Get_SubtitleColor();
            foreach (ComboBoxItem item in cb_SubtitleColor.Items)
            {
                if (item.Tag.ToString() == subColor)
                {
                    cb_SubtitleColor.SelectedItem = item;
                    break;
                }
            }
            slider_SubtitleTran.Value = SettingHelper.Get_SubtitleBgTran();
            slider_SubtitleSize.Value = SettingHelper.Get_SubtitleSize();

            //mediaElement.MediaPlayer.Volume = SettingHelper.Get_Volume();
            SetVolume(SettingHelper.Get_Volume());
            bo = BrightnessOverride.GetForCurrentView();
            if (bo.IsSupported)
            {
                bo.StartOverride();
            }
            bo.IsSupportedChanged += Bo_IsSupportedChanged;
            Brightness = SettingHelper.Get_Light();

            DanmuNum = SettingHelper.Get_DMNumber();
            rb_defu.IsChecked = true;
            btn_ViewPost.Visibility = Visibility.Collapsed;

            //danmu.borderStyle = (NSDanmaku.Model.DanmakuBorderStyle)SettingHelper.Get_DMStyle();
            menu_setting_buttom.IsChecked = !SettingHelper.Get_DMVisBottom();
            menu_setting_top.IsChecked = !SettingHelper.Get_DMVisTop();
            menu_setting_gd.IsChecked = !SettingHelper.Get_DMVisRoll();

            var danmuStatus = SettingHelper.Get_DMStatus();
            if (danmuStatus)
            {
                danmu.Visibility = Visibility.Visible;
                LoadDanmu = true;
            }
            else
            {
                danmu.Visibility = Visibility.Collapsed;
                LoadDanmu = false;
            }
            settingFlag = false;
        }

        private void Bo_IsSupportedChanged(BrightnessOverride sender, object args)
        {
            //if (bo.IsSupported)
            //{
            //    MTC.Brightness = 0;
            //    bo.SetBrightnessLevel(1 - Brightness, DisplayBrightnessOverrideOptions.None);
            //}
            //else
            //{
            MTC.Brightness = Brightness;
            //}
        }

        string DMZZBDS = "";
        bool hidePointerFlag = false;
        int DanmuNum = 0;
        bool mergeDanmu = false;
        List<string> sended = new List<string>();

        private void SetDanmakuPool(
            List<NSDanmaku.Model.DanmakuModel> pool,
            bool includeCurrentPosition = true)
        {
            DanMuPool = pool ?? new List<NSDanmaku.Model.DanmakuModel>();
            danmakuTimeline = new PlaybackEventTimeline<NSDanmaku.Model.DanmakuModel>(
                DanMuPool,
                item => item.time);
            ResetDanmakuTimeline(includeCurrentPosition);
        }

        private void AppendDanmakuPool(IEnumerable<NSDanmaku.Model.DanmakuModel> additions)
        {
            var pool = DanMuPool ?? new List<NSDanmaku.Model.DanmakuModel>();
            if (additions != null)
            {
                pool.AddRange(additions.Where(item => item != null));
            }

            SetDanmakuPool(pool);
        }

        private void ResetDanmakuTimeline(bool includeCurrentPosition = true)
        {
            if (danmakuTimeline == null)
            {
                return;
            }

            var position = mediaPlayer?.PlaybackSession.Position.TotalSeconds ?? 0;
            danmakuTimeline.Reset(Math.Max(0, position), includeCurrentPosition);
            danmakuLimitSecond = -1;
            danmakuLimitCount = 0;
            mergeDanmakuSecond = -1;
            sended.Clear();
        }

        private void Timer_Date_Tick(object sender, object e)
        {
            if (_PointerHideTime >= 50 && !hidePointerFlag)
            {
                Window.Current.CoreWindow.PointerCursor = null;
            }
            _PointerHideTime++;

            try
            {
                if (mediaPlayer?.PlaybackSession.PlaybackState == MediaPlaybackState.Playing && LoadDanmu)
                {
                    var batch = danmakuTimeline?.Advance(
                        mediaPlayer.PlaybackSession.Position.TotalSeconds);
                    if (batch != null)
                    {
                        foreach (var item in batch.Items)
                        {
                            ShowDanmaku(item);
                        }
                    }
                }

                HandleBiliJumpPosition();
                HandleInteractiveDanmakuPosition();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("显示弹幕失败", LogType.ERROR, ex);
            }
        }

        private void ShowDanmaku(NSDanmaku.Model.DanmakuModel item)
        {
            if (item == null || DanDis_Dis(item.text))
            {
                return;
            }

            var itemSecond = item.time < 0 ? 0 : (int)Math.Floor(item.time);
            if (itemSecond != danmakuLimitSecond)
            {
                danmakuLimitSecond = itemSecond;
                danmakuLimitCount = 0;
            }

            if (itemSecond != mergeDanmakuSecond)
            {
                mergeDanmakuSecond = itemSecond;
                sended.Clear();
            }

            if (DanmuNum != 0 && danmakuLimitCount >= DanmuNum)
            {
                return;
            }

            try
            {
                if (DMZZBDS.Length != 0 && Regex.IsMatch(item.source, DMZZBDS))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("匹配弹幕屏蔽规则失败", LogType.ERROR, ex);
            }

            if (mergeDanmu)
            {
                if (sended.Contains(item.text + item.location))
                {
                    return;
                }
                sended.Add(item.text + item.location);
            }

            switch (item.location)
            {
                case NSDanmaku.Model.DanmakuLocation.Top:
                    danmu.AddTopDanmu(item, false);
                    break;
                case NSDanmaku.Model.DanmakuLocation.Bottom:
                    danmu.AddBottomDanmu(item, false);
                    break;
                case NSDanmaku.Model.DanmakuLocation.Position:
                    danmu.AddPositionDanmu(item);
                    break;
                default:
                    danmu.AddRollDanmu(item, false);
                    break;
            }

            danmakuLimitCount++;
        }

        private void ClearInteractiveDanmaku()
        {
            interactiveDanmakuLoadVersion++;
            interactiveDanmakuPool = new List<InteractiveDanmakuModel>();
            currentInteractiveDanmaku = null;
            if (interactiveDanmakuControl != null)
            {
                interactiveDanmakuControl.HideItem();
            }
        }

        private void HandleInteractiveDanmakuPosition()
        {
            if (!SettingHelper.Get_InteractiveDanmakuStatus()
                || !LoadDanmu
                || mediaPlayer == null
                || interactiveDanmakuPool == null
                || interactiveDanmakuPool.Count == 0)
            {
                currentInteractiveDanmaku = null;
                interactiveDanmakuControl?.HideItem();
                return;
            }

            var position = mediaPlayer.PlaybackSession.Position;
            var current = interactiveDanmakuPool.FirstOrDefault(item =>
                position.TotalMilliseconds >= item.Progress
                && position.TotalMilliseconds < item.Progress + item.Duration);
            if (current == null)
            {
                currentInteractiveDanmaku = null;
                interactiveDanmakuControl.HideItem();
                return;
            }

            if (currentInteractiveDanmaku == null
                || currentInteractiveDanmaku.Key != current.Key)
            {
                currentInteractiveDanmaku = current;
                interactiveDanmakuControl.ShowItem(current);
            }
        }

        //private async void Timer_Tick(object sender, object e)
        //{
        //    await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
        //    {
        //        if (SqlHelper.GetPostIsViewPost(playNow.Mid))
        //        {
        //            SqlHelper.UpdateViewPost(new ViewPostHelperClass() { epId = playNow.Mid, Post = Convert.ToInt32(mediaElement.MediaPlayer.PlaybackSession.Position.TotalSeconds) });
        //        }

        //        //sql.UpdateValue(Cid, Convert.ToInt32(mediaElement.MediaPlayer.PlaybackSession.Position.TotalSeconds));
        //    });
        //}

        #region 弹幕设置
        /// <summary>
        /// 弹幕屏蔽
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btn_Dis_Remove_Click(object sender, RoutedEventArgs e)
        {
            foreach (NSDanmaku.Model.DanmakuModel item in list_DisDanmu.SelectedItems)
            {
                DanDis_Add(item.sendID, true);
                danmu.Remove(item);
                list_DisDanmu.Items.Remove(item);
            }
        }
        List<string> Guanjianzi = new List<string>();
        List<string> Yonghu = new List<string>();
        private void DanDis_Get()
        {


            string a = SettingHelper.Get_Guanjianzi();
            string b = SettingHelper.Get_Yonghu();
            if (a.Length != 0)
            {

                Guanjianzi = a.Split('|').ToList();
                Yonghu = b.Split('|').ToList();
                Guanjianzi.Remove(string.Empty);
                Yonghu.Remove(string.Empty);
            }


        }
        private bool DanDis_Dis(string text)
        {
            var a = (from sb in Guanjianzi where text.Contains(sb) select sb).ToList();
            var b = (from sb in Yonghu where text.Contains(sb) select sb).ToList();
            if (b.Count != 0 || a.Count != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private void DanDis_Add(string text, bool IsYonghu)
        {
            if (IsYonghu)
            {
                SettingHelper.Set_Yonghu(SettingHelper.Get_Yonghu() + "|" + text);
                Yonghu.Add(text);
            }
            else
            {
                SettingHelper.Set_Guanjianzi(SettingHelper.Get_Guanjianzi() + "|" + text);

                Guanjianzi.Add(text);
            }

        }
        #endregion


        //private async void GetPlayUrl(string cid)
        //{
        //    string url = "https://interface.bilibili.com/playurl?_device=uwp&cid=" + cid + "&otype=xml&quality=" + 2 + "&appkey=" + ApiHelper.AndroidKey.Appkey + "&access_key=" + ApiHelper.access_key + "&type=mp4&mid=" + "" + "&_buvid=" + ApiHelper._buvid + "&_hwid=" + ApiHelper._hwid + "&platform=uwp_desktop" + "&ts=" + ApiHelper.GetTimeSpan;
        //    url += "&sign=" + ApiHelper.GetSign(url);
        //    string re = await WebClientClass.GetResults_Phone(new Uri(url));
        //    re = await WebClientClass.GetResults_Phone(new Uri(url));
        //    string playUrl = Regex.Match(re, "<url>(.*?)</url>").Groups[1].Value;
        //    playUrl = playUrl.Replace("<![CDATA[", "");
        //    playUrl = playUrl.Replace("]]>", "");
        //    mediaElement.Source = new Uri(playUrl);
        //}

        bool QuityLoading = false;
        private async void OpenVideo()
        {
            await OpenVideoAsync(playNow);
        }

        private void ClearPlaybackSource()
        {
            try
            {
                if (mediaPlayer != null)
                {
                    mediaPlayer.Source = null;
                }
            }
            finally
            {
                DisposeAuxiliaryMediaPlayer();
                ReleaseFFmpegDashSource();
            }
        }

        private void ReleaseFFmpegDashSource()
        {
            var source = ffmpegDashSource;
            ffmpegDashSource = null;
            try
            {
                source?.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("释放 FFmpeg DASH 源失败", LogType.ERROR, ex);
            }
        }

        private bool IsPlaybackRequestCurrent(int requestId, PlayerModel item)
        {
            return !_isExiting
                && playbackRequestGate.IsCurrent(requestId)
                && ReferenceEquals(playNow, item);
        }

        private CancellationToken BeginDanmakuLoading()
        {
            CancelDanmakuLoading();
            danmakuLoadCancellation = new CancellationTokenSource();
            return danmakuLoadCancellation.Token;
        }

        private void CancelDanmakuLoading()
        {
            var source = danmakuLoadCancellation;
            danmakuLoadCancellation = null;
            if (source == null)
            {
                return;
            }

            try
            {
                source.Cancel();
            }
            finally
            {
                source.Dispose();
            }
        }

        private async Task<BiliDanmakuLoadResult> LoadDanmakuOrEmptyAsync(
            long aid,
            long cid,
            double durationSeconds = 0,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                return await BiliDanmakuService.LoadInitialAsync(
                    aid,
                    cid,
                    durationSeconds,
                    cancellationToken)
                    ?? new BiliDanmakuLoadResult(
                        new List<NSDanmaku.Model.DanmakuModel>(),
                        false,
                        false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("加载弹幕失败，继续播放", LogType.ERROR, ex);
                return new BiliDanmakuLoadResult(
                    new List<NSDanmaku.Model.DanmakuModel>(),
                    false,
                    false);
            }
        }

        private async Task<List<NSDanmaku.Model.DanmakuModel>> LoadCompleteDanmakuOrEmptyAsync(
            long aid,
            long cid,
            double durationSeconds = 0,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                return await BiliDanmakuService.LoadAsync(
                    aid,
                    cid,
                    durationSeconds,
                    cancellationToken)
                    ?? new List<NSDanmaku.Model.DanmakuModel>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("加载弹幕失败，继续播放", LogType.ERROR, ex);
                return new List<NSDanmaku.Model.DanmakuModel>();
            }
        }

        private void ApplyInitialDanmaku(
            BiliDanmakuLoadResult load,
            int requestId,
            PlayerModel item,
            CancellationToken cancellationToken)
        {
            var initial = load?.Items ?? new List<NSDanmaku.Model.DanmakuModel>();
            SetDanmakuPool(initial);
            if (load?.IsDanmakuClosed == true)
            {
                AddLog("当前视频已关闭弹幕");
                return;
            }

            if (load?.UnsupportedDanmakuCount > 0)
            {
                AddLog("跳过当前渲染器不支持的弹幕: " + load.UnsupportedDanmakuCount + " 条");
            }

            if (load?.WebLoadPlan != null)
            {
                _ = ApplyDanmakuSupplementWhenReadyAsync(
                    load,
                    requestId,
                    item,
                    cancellationToken);
            }
        }

        private async Task ApplyDanmakuSupplementWhenReadyAsync(
            BiliDanmakuLoadResult initial,
            int requestId,
            PlayerModel item,
            CancellationToken cancellationToken)
        {
            if (item == null || initial == null)
            {
                return;
            }

            try
            {
                var completed = await BiliDanmakuService.LoadSupplementAsync(initial, cancellationToken);
                if (!cancellationToken.IsCancellationRequested
                    && IsPlaybackRequestCurrent(requestId, item))
                {
                    SetDanmakuPool(completed.Items, false);
                    AddLog("后台补齐弹幕完成，共 " + completed.Items.Count + " 条");
                    if (completed.UnsupportedDanmakuCount > 0)
                    {
                        AddLog("跳过当前渲染器不支持的弹幕: "
                            + completed.UnsupportedDanmakuCount + " 条");
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("后台补齐弹幕失败", LogType.ERROR, ex);
            }
        }

        private async Task LoadInteractiveDanmakuAsync(PlayerModel item, int requestId = 0)
        {
            ClearInteractiveDanmaku();
            var loadVersion = interactiveDanmakuLoadVersion;
            if (!SettingHelper.Get_InteractiveDanmakuStatus()
                || item == null
                || item.Mode == PlayMode.Local
                || item.Mode == PlayMode.FormLocal
                || item.Mode == PlayMode.QQ
                || !long.TryParse(item.Aid, out var aid)
                || !long.TryParse(item.Mid, out var cid))
            {
                return;
            }

            try
            {
                AddLog("读取互动弹幕...");
                var result = await InteractiveDanmakuService.LoadAsync(aid, cid);
                if (requestId != 0 && !IsPlaybackRequestCurrent(requestId, item))
                {
                    return;
                }
                if (loadVersion != interactiveDanmakuLoadVersion
                    || !SettingHelper.Get_InteractiveDanmakuStatus()
                    || !ReferenceEquals(playNow, item))
                {
                    return;
                }

                interactiveDanmakuPool = result ?? new List<InteractiveDanmakuModel>();
                AddLog("互动弹幕数量: " + interactiveDanmakuPool.Count);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("读取互动弹幕失败", LogType.ERROR, ex);
            }
        }

        private string GetPlaybackRequestStrategy()
        {
            if (!SettingHelper.Get_UseDASH())
            {
                return "传统流 + SYEngine";
            }

            return SettingHelper.Get_ForceVideo()
                ? "DASH + FFmpeg 软解"
                : "DASH + 系统决定";
        }

        private void UpdateSoftwareDecodeInfo(ReturnPlayModel result)
        {
            string status;
            switch (result?.usePlayMode)
            {
                case UsePlayMode.FFmpegDash:
                    status = "软解 (FFmpeg)";
                    break;
                case UsePlayMode.SYEngine:
                    status = SYEngine.Core.ForceSoftwareDecode
                        ? "请求软解 (SYEngine)"
                        : "系统决定 (SYEngine)";
                    break;
                case UsePlayMode.System:
                case UsePlayMode.Dash:
                    status = "系统决定";
                    break;
                default:
                    status = "未知";
                    break;
            }

            txt_fvideo.Text = status;
            AddLog("软解状态：" + status);
        }

        private async Task<bool> ApplyPlaybackSourceAsync(ReturnPlayModel result, int requestId, PlayerModel item)
        {
            if (result == null || !IsPlaybackRequestCurrent(requestId, item) || mediaPlayer == null)
            {
                result?.ffmpegDashSource?.Dispose();
                return false;
            }

            bool ffmpegOwnershipTransferred = false;
            try
            {
                IMediaPlaybackSource source = null;
                IMediaPlaybackSource audioSource = null;
                if (result.usePlayMode == UsePlayMode.System && Uri.TryCreate(result.url, UriKind.Absolute, out Uri uri))
                {
                    source = MediaSource.CreateFromUri(uri);
                }
                else if (result.usePlayMode == UsePlayMode.Dash && result.mediaSource is AdaptiveMediaSource adaptiveSource)
                {
                    source = MediaSource.CreateFromAdaptiveMediaSource(adaptiveSource);
                }
                else if (result.usePlayMode == UsePlayMode.FFmpegDash && result.ffmpegDashSource != null)
                {
                    var videoPlaybackItem = result.ffmpegDashSource.CreateVideoPlaybackItem();
                    var audioPlaybackItem = result.ffmpegDashSource.CreateAudioPlaybackItem();
                    if (videoPlaybackItem != null && audioPlaybackItem != null)
                    {
                        source = videoPlaybackItem;
                        audioSource = audioPlaybackItem;
                    }
                }
                else if (result.playlist != null)
                {
                    var playlistUri = await result.playlist.SaveAndGetFileUriAsync();
                    if (!IsPlaybackRequestCurrent(requestId, item))
                    {
                        result.ffmpegDashSource?.Dispose();
                        return false;
                    }
                    source = MediaSource.CreateFromUri(playlistUri);
                }

                if (source == null || !IsPlaybackRequestCurrent(requestId, item) || mediaPlayer == null)
                {
                    result.ffmpegDashSource?.Dispose();
                    return false;
                }

                txt_site.Text = result.from;
                txt_VideoCodec.Text = string.IsNullOrWhiteSpace(result.videoCodec) ? "未知" : result.videoCodec;
                txt_VideoWidth.Text = result.videoWidth ?? string.Empty;
                txt_VideoHeight.Text = result.videoHeight ?? string.Empty;
                if (result.ffmpegDashSource != null)
                {
                    DisposeAuxiliaryMediaPlayer();
                    ReleaseFFmpegDashSource();
                    ffmpegDashSource = result.ffmpegDashSource;
                    ffmpegOwnershipTransferred = true;
                    mediaPlayer_audio = new MediaPlayer();
                    mediaPlayer_audio.CommandManager.IsEnabled = false;
                    mediaPlayer_audio.Volume = mediaPlayer.Volume;
                    mediaPlayer_audio.PlaybackSession.PlaybackRate = mediaPlayer.PlaybackSession.PlaybackRate;
                    mediaPlayer_audio.Source = audioSource;
                }
                mediaPlayer.Source = source;
                UpdateSoftwareDecodeInfo(result);
                return true;
            }
            catch
            {
                if (ffmpegOwnershipTransferred)
                {
                    try
                    {
                        if (mediaPlayer != null)
                        {
                            mediaPlayer.Source = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLog("清理失败的 FFmpeg DASH 播放源失败", LogType.ERROR, ex);
                    }
                    DisposeAuxiliaryMediaPlayer();
                    ReleaseFFmpegDashSource();
                }
                else
                {
                    result.ffmpegDashSource?.Dispose();
                }
                throw;
            }
        }

        private async Task OpenVideoAsync(PlayerModel item)
        {
            if (item == null)
            {
                return;
            }

            var requestId = playbackRequestGate.Begin();
            var danmakuCancellationToken = BeginDanmakuLoading();
            pendingPlaybackRestoreState = null;
            DisposeAuxiliaryMediaPlayer();
            mediaPlayer?.Pause();
            ClearPlaybackSource();
            txt_VideoCodec.Text = "未知";
            string playbackErrorMessage = null;
            try
            {
                if (gv_play.Items.Count == 0 || gv_play.Items.Count == 1)
                {
                    MTC.ShowPlayListBtn = item.isInteraction;
                    MTC.ShowNextButton = false;
                    MTC.ShowPreviousButton = false;
                }
                else
                {
                    MTC.ShowPlayListBtn = true;
                    MTC.ShowPreviousButton = true;
                    MTC.ShowNextButton = true;
                    if (gv_play.SelectedIndex == 0)
                    {
                        MTC.ShowPreviousButton = false;
                    }
                    if (gv_play.SelectedIndex == gv_play.Items.Count - 1)
                    {
                        MTC.ShowNextButton = false;
                    }
                }

                LastPost = 0;
                ClearBiliJumpAds();
                ClearSubTitle();
                ClearInteractiveDanmaku();
                MTC.ClearLog();
                MTC.VideoTitle = item.Title + " - " + item.VideoTitle;
                MTC.ShowSendDanmuBtn = true;
                MTC.ShowDanmakuBtn = Visibility.Visible;
                cb_Quity.Visibility = Visibility.Visible;
                pr.Text = "正在初始化播放器...";
                AddLog("正在初始化播放器...");
                if (!await LoadQualities(item, requestId))
                {
                    return;
                }
                AddLog("请求策略：" + GetPlaybackRequestStrategy());
                cb_Quity.IsEnabled = true;
                var quality = (cb_Quity.SelectedItem as QualityModel)?.qn ?? 64;
                switch (item.Mode)
                {
                    case PlayMode.Bangumi:
                    case PlayMode.Movie:
                    case PlayMode.VipBangumi:
                        pr.Text = "填充弹幕中...";
                        AddLog("开始填充弹幕...");
                        var bangumiDanmakuTask = LoadDanmakuOrEmptyAsync(
                            Convert.ToInt64(item.Aid),
                            Convert.ToInt64(item.Mid),
                            item.Duration,
                            danmakuCancellationToken);
                        var bangumiSourceTask = PlayurlHelper.GetBangumiUrl(item, quality);
                        await Task.WhenAll(bangumiDanmakuTask, bangumiSourceTask);
                        var ban = bangumiSourceTask.Result;
                        if (!IsPlaybackRequestCurrent(requestId, item))
                        {
                            ban?.ffmpegDashSource?.Dispose();
                            return;
                        }
                        ApplyInitialDanmaku(
                            bangumiDanmakuTask.Result,
                            requestId,
                            item,
                            danmakuCancellationToken);
                        if (!await ApplyPlaybackSourceAsync(ban, requestId, item))
                        {
                            playbackErrorMessage = ban?.errorMessage;
                            throw new InvalidOperationException("番剧播放源无效");
                        }
                        if (ban.from == "server")
                        {
                            Utils.ShowMessageToast("当前视频可能非哔哩哔哩提供，请勿轻信视频内广告", 5000);
                        }
                        AddLog("播放器类型:" + ban.usePlayMode.ToString());
                        break;
                    case PlayMode.Video:
                        pr.Text = "填充弹幕中...";
                        AddLog("开始填充弹幕...");
                        var videoDanmakuTask = LoadDanmakuOrEmptyAsync(
                            Convert.ToInt64(item.Aid),
                            Convert.ToInt64(item.Mid),
                            item.Duration,
                            danmakuCancellationToken);
                        var videoSourceTask = PlayurlHelper.GetVideoUrl(item.Aid, item.Mid, quality);
                        await Task.WhenAll(videoDanmakuTask, videoSourceTask);
                        var videoSource = videoSourceTask.Result;
                        if (!IsPlaybackRequestCurrent(requestId, item))
                        {
                            videoSource?.ffmpegDashSource?.Dispose();
                            return;
                        }
                        ApplyInitialDanmaku(
                            videoDanmakuTask.Result,
                            requestId,
                            item,
                            danmakuCancellationToken);
                        if (!await ApplyPlaybackSourceAsync(videoSource, requestId, item))
                        {
                            playbackErrorMessage = videoSource?.errorMessage;
                            throw new InvalidOperationException("视频播放源无效");
                        }
                        break;
                    case PlayMode.QQ:
                        AddLog("不支持播放的源:腾讯");
                        break;
                    case PlayMode.Sohu:
                        pr.Text = "填充弹幕中...";
                        AddLog("开始填充弹幕...");
                        var sohuDanmakuTask = LoadDanmakuOrEmptyAsync(
                            Convert.ToInt64(item.Aid),
                            Convert.ToInt64(item.Mid),
                            item.Duration,
                            danmakuCancellationToken);
                        var sohuSourceTask = PlayurlHelper.GetSoHuPlayInfo(item.rich_vid, cb_Quity.SelectedIndex + 1);
                        await Task.WhenAll(sohuDanmakuTask, sohuSourceTask);
                        if (!IsPlaybackRequestCurrent(requestId, item)) return;
                        ApplyInitialDanmaku(
                            sohuDanmakuTask.Result,
                            requestId,
                            item,
                            danmakuCancellationToken);
                        mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(sohuSourceTask.Result));
                        txt_site.Text = "sohu";
                        UpdateSoftwareDecodeInfo(new ReturnPlayModel { usePlayMode = UsePlayMode.System });
                        break;
                    case PlayMode.Local:
                        pr.Text = "加载视频中...";
                        MTC.ShowShareBtn = Visibility.Collapsed;
                        MTC.ShowCoinsBtn = Visibility.Collapsed;
                        cb_Quity.Visibility = Visibility.Collapsed;
                        await PlayLocal(item, requestId);
                        if (!IsPlaybackRequestCurrent(requestId, item)) return;
                        txt_site.Text = "本地";
                        UpdateSoftwareDecodeInfo(new ReturnPlayModel { usePlayMode = UsePlayMode.System });
                        break;
                    case PlayMode.FormLocal:
                        pr.Text = "加载视频中...";
                        AddLog("读取本地视频...");
                        MTC.ShowSendDanmuBtn = false;

                        MTC.ShowDanmakuBtn = Visibility.Collapsed;
                        cb_Quity.Visibility = Visibility.Collapsed;
                        txt_site.Text = "本地";
                        await PlayFromLocal(item, requestId);
                        if (!IsPlaybackRequestCurrent(requestId, item)) return;
                        UpdateSoftwareDecodeInfo(new ReturnPlayModel { usePlayMode = UsePlayMode.System });
                        break;
                    default:
                        break;
                }

                await LoadInteractiveDanmakuAsync(item, requestId);
                if (!IsPlaybackRequestCurrent(requestId, item))
                {
                    return;
                }

                if (item.Mode != PlayMode.Local && item.Mode != PlayMode.FormLocal)
                {
                    AddLog("读取是否包含字幕");
                    var hasSub = await PlayurlHelper.GetHasSubTitle(item.Aid, item.Mid);
                    if (!IsPlaybackRequestCurrent(requestId, item)) return;
                    LaodSubTitleMenu(hasSub);
                    if (IsBiliJumpVideo(item))
                    {
                        _ = LoadBiliJumpAdsAsync(item, requestId, hasSub);
                    }
                }

                AddLog("准备开始播放...");
                MTC.HideLog();
                MTC.timer2.Start();
                await ReportHistory(item, 0);
            }
            catch (Exception ex)
            {
                if (!IsPlaybackRequestCurrent(requestId, item))
                {
                    return;
                }
                AddLog("视频播放失败了" + ex.HResult);
                LogHelper.WriteLog("读取播放地址失败", LogType.ERROR, ex);
                var message = string.IsNullOrWhiteSpace(playbackErrorMessage)
                    ? "无法读取到播放地址 ＞﹏＜ \r\n请尝试登录、更换清晰度、开通大会员后再试"
                    : playbackErrorMessage;
                await new MessageDialog(message).ShowAsync();
            }
        }

        private void LaodSubTitleMenu(HasSubtitleModel hasSub)
        {
            if (hasSub.subtitles != null && hasSub.subtitles.Count != 0)
            {
                AddLog($"该视频包含了{hasSub.subtitles.Count}个字幕文件");
                var menu = new MenuFlyout();


                foreach (var item in hasSub.subtitles)
                {
                    ToggleMenuFlyoutItem menuitem = new ToggleMenuFlyoutItem() { Text = item.lan_doc, Tag = item.subtitle_url };
                    menuitem.Click += Menuitem_Click;
                    menu.Items.Add(menuitem);
                }
                ToggleMenuFlyoutItem noneItem = new ToggleMenuFlyoutItem() { Text = "无" };
                noneItem.Click += Menuitem_Click;
                menu.Items.Add(noneItem);
                (menu.Items.LastOrDefault() as ToggleMenuFlyoutItem).IsChecked = true;
                //SetSubTitle((menu.Items[0] as ToggleMenuFlyoutItem).Tag.ToString());
                MTC.CCSelectFlyout = menu;
            }
            else
            {
                AddLog("该视频没有字幕文件");
                var menu = new MenuFlyout();
                menu.Items.Add(new ToggleMenuFlyoutItem() { Text = "无", IsChecked = true });
                MTC.CCSelectFlyout = menu;
            }


        }
        /// <summary>
        /// 字幕文件
        /// </summary>
        SubtitleModel subtitles;
        /// <summary>
        /// 字幕Timer
        /// </summary>
        DispatcherTimer subtitleTimer;
        PlaybackTimelineIndex<SubtitleItemModel> subtitleTimeline;
        /// <summary>
        /// 选择字幕
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Menuitem_Click(object sender, RoutedEventArgs e)
        {

            foreach (ToggleMenuFlyoutItem item in (MTC.CCSelectFlyout as MenuFlyout).Items)
            {
                item.IsChecked = false;
            }
            var menuitem = (sender as ToggleMenuFlyoutItem);
            if (menuitem.Text == "无")
            {
                ClearSubTitle();
            }
            else
            {
                SetSubTitle(menuitem.Tag.ToString());
            }
            menuitem.IsChecked = true;
        }
        /// <summary>
        /// 设置字幕文件
        /// </summary>
        /// <param name="url"></param>
        private async void SetSubTitle(string url)
        {
            var loadVersion = ++subtitleLoadVersion;
            StopSubtitleTimer();
            subtitles = null;
            try
            {
                var loadedSubtitles = await PlayurlHelper.GetSubtitle(url);
                if (loadVersion != subtitleLoadVersion)
                {
                    return;
                }

                if (loadedSubtitles?.body != null)
                {
                    loadedSubtitles.body = loadedSubtitles.body.OrderBy(x => x.from).ToList();
                    subtitles = loadedSubtitles;
                    subtitleTimeline = new PlaybackTimelineIndex<SubtitleItemModel>(loadedSubtitles.body, x => x.from, x => x.to);
                    subtitleTimer = new DispatcherTimer();
                    subtitleTimer.Interval = TimeSpan.FromMilliseconds(100);
                    subtitleTimer.Tick += SubtitleTimer_Tick;
                    subtitleTimer.Start();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("加载字幕失败", LogType.ERROR, ex);
                Utils.ShowMessageToast("加载字幕失败了");
            }
        }

        private void SubtitleTimer_Tick(object sender, object e)
        {
            if (mediaPlayer?.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                var body = subtitles?.body;
                if (body == null || body.Count == 0) return;
                var time = mediaPlayer.PlaybackSession.Position.TotalSeconds;
                var current = subtitleTimeline?.Find(time);
                if (current != null)
                {
                    if (current.content != MTC.GetSubtitle())
                    {
                        MTC.ShowSubtitle();
                        MTC.SetSubtitle(current.content);
                    }
                }
                else
                {
                    MTC.HideSubtitle();
                }
            }
        }

        private void ClearSubTitle()
        {
            subtitleLoadVersion++;
            StopSubtitleTimer();
            subtitleTimeline = null;
            MTC.HideSubtitle();
            subtitles = null;
        }

        private void StopSubtitleTimer()
        {
            if (subtitleTimer != null)
            {
                subtitleTimer.Stop();
                subtitleTimer.Tick -= SubtitleTimer_Tick;
                subtitleTimer = null;
            }
        }

        private static bool IsBiliJumpVideo(PlayerModel item)
        {
            return item != null
                && item.Mode == PlayMode.Video
                && item.banInfo == null
                && string.IsNullOrWhiteSpace(item.banId);
        }

        private double GetBiliJumpVideoDuration(PlayerModel item)
        {
            var duration = item?.Duration ?? 0;
            if (duration <= 0)
            {
                duration = mediaPlayer?.PlaybackSession.NaturalDuration.TotalSeconds ?? 0;
            }

            return double.IsNaN(duration) || double.IsInfinity(duration)
                ? 0
                : Math.Max(0, duration);
        }

        private bool IsBiliJumpDurationEligible(PlayerModel item)
        {
            return GetBiliJumpVideoDuration(item) > BiliJumpMinimumDurationSeconds;
        }

        private async Task<int?> LoadBiliJumpOwnerFansAsync(string aid)
        {
            if (string.IsNullOrWhiteSpace(aid))
            {
                return null;
            }

            try
            {
                AddLog("读取UP主粉丝数...");
                var response = await new VideoAPI().Detail(aid, false).Request();
                if (response == null || !response.status)
                {
                    return null;
                }

                var detail = await response.GetJson<ApiDataModel<VideoDetailModel>>();
                return detail?.success == true ? detail.data?.owner_ext?.fans : null;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("读取UP主粉丝数失败", LogType.ERROR, ex);
                return null;
            }
        }

        private async Task LoadBiliJumpAdsAsync(PlayerModel item, int requestId, HasSubtitleModel hasSub)
        {
            if (!IsBiliJumpVideo(item)
                || !SettingHelper.Get_BiliJumpAiEnabled())
            {
                return;
            }

            var duration = GetBiliJumpVideoDuration(item);
            if (duration <= BiliJumpMinimumDurationSeconds)
            {
                ClearBiliJumpAds();
                UpdateBiliJumpInfo(
                    duration > 0
                        ? "未识别：视频时长不超过 2 分 30 秒"
                        : "未识别：无法获取视频时长",
                    null);
                return;
            }

            var loadVersion = ++biliJumpLoadVersion;
            try
            {
                var minFans = SettingHelper.Get_BiliJumpAiMinFans();
                if (minFans > 0)
                {
                    var ownerFans = item.OwnerFans;
                    if (!ownerFans.HasValue)
                    {
                        ownerFans = await LoadBiliJumpOwnerFansAsync(item.Aid);
                        if (!IsPlaybackRequestCurrent(requestId, item) || loadVersion != biliJumpLoadVersion)
                        {
                            return;
                        }

                        if (ownerFans.HasValue)
                        {
                            item.OwnerFans = ownerFans;
                        }
                    }

                    if (!ownerFans.HasValue)
                    {
                        UpdateBiliJumpInfo("未识别：无法获取UP主粉丝数", null);
                        return;
                    }

                    var minFansCount = (long)minFans * 10000L;
                    if (ownerFans.Value < minFansCount)
                    {
                        UpdateBiliJumpInfo($"未识别：UP主粉丝数低于 {minFans} 万阈值", null);
                        return;
                    }
                }

                UpdateBiliJumpInfo("识别中...", null);
                var result = await BiliJumpAiService.RecognizeAsync(
                    item.Aid,
                    item.Mid,
                    item.VideoTitle,
                    duration,
                    hasSub);

                if (!IsPlaybackRequestCurrent(requestId, item) || loadVersion != biliJumpLoadVersion)
                {
                    return;
                }

                if (!result.success || result.data == null)
                {
                    UpdateBiliJumpInfo("识别失败：" + (result.message ?? "未知错误"), null);
                    return;
                }

                biliJumpAds = BiliJumpAiParser.NormalizeSegments(result.data.ads, duration);
                biliJumpCurrentAd = null;
                biliJumpLastNotifiedKey = null;
                biliJumpLastHandledKey = null;
                menuitem_BiliJumpSkipCurrent.IsEnabled = false;
                var source = (result.message ?? string.Empty).IndexOf("缓存", StringComparison.Ordinal) >= 0
                    ? "公共缓存"
                    : "AI 实时识别";
                UpdateBiliJumpInfo(
                    biliJumpAds.Count == 0
                        ? $"AI识别来源：{source}，未识别到植入广告"
                        : $"AI识别来源：{source}，识别到 {biliJumpAds.Count} 个植入广告",
                    biliJumpAds);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("加载 BiliJump AI 结果失败", LogType.ERROR, ex);
                if (IsPlaybackRequestCurrent(requestId, item) && loadVersion == biliJumpLoadVersion)
                {
                    UpdateBiliJumpInfo("识别失败", null);
                }
            }
        }

        private void UpdateBiliJumpInfo(string status, IEnumerable<BiliJumpAdSegment> segments)
        {
            if (txt_BiliJumpStatus != null)
            {
                txt_BiliJumpStatus.Text = status ?? string.Empty;
            }

            if (list_BiliJumpAds != null)
            {
                list_BiliJumpAds.ItemsSource = segments == null
                    ? null
                    : segments.Select(FormatBiliJumpAd).ToList();
            }
        }

        private static string FormatBiliJumpAd(BiliJumpAdSegment segment)
        {
            var start = FormatBiliJumpTime(segment.start_time);
            var end = FormatBiliJumpTime(segment.end_time);
            var text = $"{start} - {end}";
            if (!string.IsNullOrWhiteSpace(segment.product_name))
            {
                text += " " + segment.product_name;
            }
            if (!string.IsNullOrWhiteSpace(segment.ad_content))
            {
                text += ": " + segment.ad_content;
            }
            return text;
        }

        private static string FormatBiliJumpTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return time.TotalHours >= 1
                ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}"
                : $"{time.Minutes:00}:{time.Seconds:00}";
        }

        private void HandleBiliJumpPosition()
        {
            if (!IsBiliJumpVideo(playNow)
                || !SettingHelper.Get_BiliJumpAiEnabled()
                || !IsBiliJumpDurationEligible(playNow)
                || mediaPlayer?.PlaybackSession.PlaybackState != MediaPlaybackState.Playing
                || biliJumpAds == null
                || biliJumpAds.Count == 0)
            {
                menuitem_BiliJumpSkipCurrent.IsEnabled = false;
                biliJumpCurrentAd = null;
                return;
            }

            var position = mediaPlayer.PlaybackSession.Position.TotalSeconds;
            var current = biliJumpAds.FirstOrDefault(x => position >= x.start_time && position < x.end_time);
            biliJumpCurrentAd = current;
            menuitem_BiliJumpSkipCurrent.IsEnabled = current != null;
            if (current == null)
            {
                biliJumpLastHandledKey = null;
                return;
            }

            var key = GetBiliJumpAdKey(current);
            if (SettingHelper.Get_BiliJumpAiAutoJump())
            {
                if (biliJumpLastHandledKey == key)
                {
                    return;
                }

                biliJumpLastHandledKey = key;
                mediaPlayer.PlaybackSession.Position = TimeSpan.FromSeconds(current.end_time);
                menuitem_BiliJumpSkipCurrent.IsEnabled = false;
                Utils.ShowMessageToast("已自动跳过广告", 3000);
                return;
            }

            if (biliJumpLastNotifiedKey != key)
            {
                biliJumpLastNotifiedKey = key;
                Utils.ShowMessageToast("识别到植入广告，可在播放器更多菜单中跳过", 3000);
            }
        }

        private void SkipCurrentBiliJumpAd()
        {
            if (!IsBiliJumpVideo(playNow)
                || !IsBiliJumpDurationEligible(playNow)
                || biliJumpCurrentAd == null
                || mediaPlayer == null)
            {
                return;
            }

            biliJumpLastHandledKey = GetBiliJumpAdKey(biliJumpCurrentAd);
            mediaPlayer.PlaybackSession.Position = TimeSpan.FromSeconds(biliJumpCurrentAd.end_time);
            menuitem_BiliJumpSkipCurrent.IsEnabled = false;
            Utils.ShowMessageToast("已跳过植入广告", 2000);
        }

        private static string GetBiliJumpAdKey(BiliJumpAdSegment segment)
        {
            return segment.start_time.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + "-"
                + segment.end_time.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void ClearBiliJumpAds()
        {
            biliJumpLoadVersion++;
            biliJumpAds.Clear();
            biliJumpCurrentAd = null;
            biliJumpLastNotifiedKey = null;
            biliJumpLastHandledKey = null;
            UpdateBiliJumpInfo("未识别", null);
            if (menuitem_BiliJumpSkipCurrent != null)
            {
                menuitem_BiliJumpSkipCurrent.IsEnabled = false;
            }
        }

        private void menuitem_BiliJumpSkipCurrent_Click(object sender, RoutedEventArgs e)
        {
            SkipCurrentBiliJumpAd();
        }


        private async Task ChangeQualityAsync(PlayerModel item, int quality, PlaybackRestoreState restoreState)
        {
            if (item == null)
            {
                return;
            }

            var requestId = playbackRequestGate.Begin();
            pendingPlaybackRequest = requestId;
            pendingPlaybackRestoreState = restoreState;
            ReturnPlayModel result = null;
            try
            {
                mediaPlayer?.Pause();
                ClearPlaybackSource();
                txt_VideoCodec.Text = "未知";

                switch (item.Mode)
                {
                    case PlayMode.Bangumi:
                    case PlayMode.Movie:
                    case PlayMode.VipBangumi:
                        result = await PlayurlHelper.GetBangumiUrl(item, quality);
                        break;
                    case PlayMode.Video:
                        result = await PlayurlHelper.GetVideoUrl(item.Aid, item.Mid, quality);
                        break;
                    case PlayMode.Sohu:
                        var sohuUrl = await PlayurlHelper.GetSoHuPlayInfo(item.rich_vid, cb_Quity.SelectedIndex + 1);
                        if (IsPlaybackRequestCurrent(requestId, item) && Uri.TryCreate(sohuUrl, UriKind.Absolute, out Uri uri))
                        {
                            mediaPlayer.Source = MediaSource.CreateFromUri(uri);
                            txt_site.Text = "sohu";
                            UpdateSoftwareDecodeInfo(new ReturnPlayModel { usePlayMode = UsePlayMode.System });
                            return;
                        }
                        break;
                    default:
                        pendingPlaybackRestoreState = null;
                        return;
                }

                if (!await ApplyPlaybackSourceAsync(result, requestId, item))
                {
                    throw new InvalidOperationException("清晰度对应的播放源无效");
                }
            }
            catch (Exception ex)
            {
                if (IsPlaybackRequestCurrent(requestId, item))
                {
                    pendingPlaybackRestoreState = null;
                    LogHelper.WriteLog("更换清晰度失败", LogType.ERROR, ex);
                    Utils.ShowMessageToast(string.IsNullOrWhiteSpace(result?.errorMessage)
                        ? "更换清晰度失败，无法读取播放地址"
                        : result.errorMessage);
                }
            }
            MTC.HideLog();
        }

        /// <summary>
        /// 读取清晰度
        /// </summary>
        private async Task<bool> LoadQualities(PlayerModel item, int requestId)
        {
            AddLog("正在获取视频清晰度");
            QuityLoading = true;
            List<QualityModel> qualities;
            switch (item.Mode)
            {
                case PlayMode.Bangumi:
                case PlayMode.Movie:
                case PlayMode.VipBangumi:
                    qualities = await PlayurlHelper.GetAnimeQualities(item);
                    break;
                case PlayMode.Video:
                    qualities = await PlayurlHelper.GetVideoQualities(item);
                    break;
                case PlayMode.QQ:
                    qualities = new List<QualityModel>() { new QualityModel() { description = "默认", qn = 64 } };
                    AddLog("不支持的播放源:腾讯");
                    break;
                case PlayMode.Sohu:
                    qualities = PlayurlHelper.GetDefaultQualities();
                    break;
                default:
                    qualities = new List<QualityModel>() { new QualityModel() { description = "默认", qn = 64 } };
                    break;
            }

            if (!IsPlaybackRequestCurrent(requestId, item))
            {
                QuityLoading = false;
                return false;
            }

            if (qualities == null || qualities.Count == 0)
            {
                qualities = PlayurlHelper.GetDefaultQualities();
            }
            cb_Quity.ItemsSource = qualities;
            var settingq = SettingHelper.Get_NewQuality();
            var selected = qualities.FirstOrDefault(x => x.qn == settingq);
            if (selected != null)
            {
                cb_Quity.SelectedItem = selected;
            }
            else
            {
                cb_Quity.SelectedIndex = cb_Quity.Items.Count - 1;
            }
            QuityLoading = false;
            return true;
        }

        private void AddLog(string msg)
        {
            MTC.AddLog(msg);
            //txt_log.Text += string.Format("[{0}]{1}\r\n",DateTime.Now.ToString("HH:mm:ss"),msg);
        }


        private async Task PlayFromLocal(PlayerModel item, int requestId)
        {
            var file = item.Parameter as StorageFile;
            if (file == null)
            {
                throw new FileNotFoundException("未找到本地视频文件");
            }

            IRandomAccessStream readStream = await file.OpenAsync(FileAccessMode.Read);
            if (IsPlaybackRequestCurrent(requestId, item) && mediaPlayer != null)
            {
                mediaPlayer.Source = MediaSource.CreateFromStream(readStream, file.ContentType);
            }
        }
        private async Task PlayLocal(PlayerModel item, int requestId)
        {
            AddLog("开始读取本地视频...");
            StorageFolder f = await StorageFolder.GetFolderFromPathAsync(item.Path);
            var ls = await f.GetFilesAsync();
            if (!IsPlaybackRequestCurrent(requestId, item)) return;
            var danmakuFile = ls.FirstOrDefault(x => x.FileType == ".xml");
            if (danmakuFile != null)
            {
                pr.Text = "填充弹幕中...";
                AddLog("填充弹幕中...");
                var pool = await danmakuParse.ParseBiliBili(danmakuFile);
                if (!IsPlaybackRequestCurrent(requestId, item)) return;
                SetDanmakuPool(pool);
            }
            var video = ls.FirstOrDefault(x => x.Name == "video.m4s");
            if (video != null)
            {
                var audio = ls.FirstOrDefault(x => x.Name == "audio.m4s");
                if (audio != null && mediaPlayer_audio == null)
                {
                    mediaPlayer_audio = new MediaPlayer();
                    mediaPlayer_audio.CommandManager.IsEnabled = false;
                    mediaPlayer_audio.Volume = mediaPlayer.Volume;
                    mediaPlayer_audio.Source = MediaSource.CreateFromStream(await audio.OpenReadAsync(), audio.ContentType);
                }
                var videoStream = await video.OpenReadAsync();
                if (!IsPlaybackRequestCurrent(requestId, item) || mediaPlayer == null) return;
                mediaPlayer.Source = MediaSource.CreateFromStream(videoStream, video.ContentType);
            }
            else
            {
                var paths = ls.Where(x => x.FileType == ".mp4" || x.FileType == ".flv").Select(x => x.Path).ToList();
                if (paths.Count == 0)
                {
                    throw new FileNotFoundException("下载目录中没有可播放的视频文件");
                }
                if (paths.Count == 1)
                {
                    var file = await StorageFile.GetFileFromPathAsync(paths[0]);
                    var stream = await file.OpenReadAsync();
                    if (!IsPlaybackRequestCurrent(requestId, item) || mediaPlayer == null) return;
                    mediaPlayer.Source = MediaSource.CreateFromStream(stream, file.ContentType);
                }
                else
                {
                    var s = await PlayLocalVideo(paths);
                    if (!IsPlaybackRequestCurrent(requestId, item) || mediaPlayer == null) return;
                    mediaPlayer.Source = MediaSource.CreateFromMediaStreamSource(s);
                }
            }
            MTC.HideLog();
            //playNow.Path
        }

        private async Task<MediaStreamSource> PlayLocalVideo(List<string> paths)
        {
            var playList = new SYEngine.Playlist(SYEngine.PlaylistTypes.LocalFile);

            MediaComposition composition = new MediaComposition();
            foreach (var item in paths)
            {
                playList.Append(item, 0, 0);
                var file = await StorageFile.GetFileFromPathAsync(item);
                var clip = await MediaClip.CreateFromFileAsync(file);
                composition.Clips.Add(clip);
            }
            return composition.GenerateMediaStreamSource();
        }




        private void btn_Back_Click(object sender, RoutedEventArgs e)
        {
            //if (this.Frame.CanGoBack)
            //{
            //    mediaElement.Stop();
            //    this.Frame.GoBack();
            //}
        }

        private void btn_Play_Click(object sender, RoutedEventArgs e)
        {
            mediaElement.MediaPlayer.Play();

        }

        private void btn_Pause_Click(object sender, RoutedEventArgs e)
        {
            if (mediaElement.MediaPlayer.PlaybackSession.CanPause)
            {
                mediaElement.MediaPlayer.Pause();
            }
        }




        #region 手势操作
        double ssValue = 0;
        bool ManipulatingBrightness = false;

        private void Grid_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            e.Handled = true;
            //progress.Visibility = Visibility.Visible;
            if (e.Delta.Translation.Y == 0)
            {
                if (MTC.Video360)
                {
                    mediaPlayer.PlaybackSession.SphericalVideoProjection.ViewOrientation *= Quaternion.CreateFromYawPitchRoll(e.Delta.Translation.X > 0 ? -.01f : .01f, 0, 0);
                }
                else
                {
                    HandleSlideProgressDelta(e.Delta.Translation.X);
                }

            }
            else
            {
                if (MTC.Video360)
                {
                    mediaPlayer.PlaybackSession.SphericalVideoProjection.ViewOrientation *= Quaternion.CreateFromYawPitchRoll(0, 0, e.Delta.Translation.Y > 0 ? -.01f : .01f);
                }
                else
                {
                    if (ManipulatingBrightness)
                        HandleSlideBrightnessDelta(e.Delta.Translation.Y);
                    else
                        HandleSlideVolumeDelta(e.Delta.Translation.Y);
                }
            }
        }

        private void HandleSlideProgressDelta(double delta)
        {
            if (mediaElement.MediaPlayer.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
                return;

            if (delta > 0)
            {
                double dd = delta / this.ActualWidth;
                double d = dd * 90;
                ssValue += d;
                //slider.Value += d;
            }
            else
            {
                double dd = Math.Abs(delta) / this.ActualWidth;
                double d = dd * 90;
                ssValue -= d;
                //slider.Value -= d;
            }
            TimeSpan ts = mediaElement.MediaPlayer.PlaybackSession.Position;
            ts = ts.Add(TimeSpan.FromSeconds(ssValue));

            if (ts < TimeSpan.Zero)
                ts = TimeSpan.Zero;
            else if (ts > mediaElement.MediaPlayer.PlaybackSession.NaturalDuration)
                ts = mediaElement.MediaPlayer.PlaybackSession.NaturalDuration;
            //txt_Post.Text = ts.Hours.ToString("00") + ":" + ts.Minutes.ToString("00") + ":" + ts.Seconds.ToString("00") + "/" + mediaElement.MediaPlayer.PlaybackSession.NaturalDuration.TimeSpan.Hours.ToString("00") + ":" + mediaElement.MediaPlayer.PlaybackSession.NaturalDuration.TimeSpan.Minutes.ToString("00") + ":" + mediaElement.MediaPlayer.PlaybackSession.NaturalDuration.TimeSpan.Seconds.ToString("00");

            txt_SSPosition.Text = ts.Hours.ToString("00") + ":" + ts.Minutes.ToString("00") + ":" + ts.Seconds.ToString("00");
            //Utils.ShowMessageToast(ts.Hours.ToString("00") + ":" + ts.Minutes.ToString("00") + ":" + ts.Seconds.ToString("00"), 3000);
        }

        private void HandleSlideVolumeDelta(double delta)
        {
            if (delta > 0)
            {
                double dd = delta / (this.ActualHeight * 0.8);

                //slider_V.Value -= d;
                var volume = mediaElement.MediaPlayer.Volume - dd;
                SetVolume(volume);

            }
            else
            {
                double dd = Math.Abs(delta) / (this.ActualHeight * 0.8);
                var volume = mediaElement.MediaPlayer.Volume + dd;
                SetVolume(volume);
                //slider_V.Value += d;
            }
            txt_SSPosition.Text = "音量:" + mediaElement.MediaPlayer.Volume.ToString("P");
            //Utils.ShowMessageToast("音量:" +  mediaElement.MediaPlayer.Volume.ToString("P"), 3000);
        }

        private void HandleSlideBrightnessDelta(double delta)
        {
            double dd = Math.Abs(delta) / (this.ActualHeight * 0.8);
            if (delta > 0)
            {
                Brightness = Math.Min(Brightness + dd, 1);
            }
            else
            {
                Brightness = Math.Max(Brightness - dd, 0);
            }
            txt_SSPosition.Text = "亮度:" + Math.Abs(Brightness - 1).ToString("P");
        }

        private void Grid_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            e.Handled = true;

            if (ssValue != 0)
            {
                var session = mediaElement.MediaPlayer.PlaybackSession;
                session.Position = PlaybackPosition.Clamp(
                    session.Position.Add(TimeSpan.FromSeconds(ssValue)),
                    session.NaturalDuration);
            }
            ssPositionShadow.Visibility = Visibility.Collapsed;
        }

        private void MTC_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            e.Handled = true;
            ssValue = 0;
            txt_SSPosition.Text = "";
            ssPositionShadow.Visibility = Visibility.Visible;

            if (e.Position.X < this.ActualWidth / 2)
                ManipulatingBrightness = true;
            else
                ManipulatingBrightness = false;
        }
        #endregion

        private void gv_play_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gv_play.SelectedIndex != -1)
            {
                var selectedItem = gv_play.SelectedItem as PlayerModel;
                var previousItem = playNow;
                if (previousItem != null && !ReferenceEquals(previousItem, selectedItem))
                {
                    var progressValue = mediaPlayer == null
                        ? 0
                        : Convert.ToInt32(mediaPlayer.PlaybackSession.Position.TotalSeconds);
                    _ = ReportHistory(previousItem, progressValue);
                }

                playNow = selectedItem;

                cb_Quity.ItemsSource = null;

                // PlayerEvent(gv_play.SelectedIndex);
                OpenVideo();

            }
        }

        private void btn_Select_Click(object sender, RoutedEventArgs e)
        {

            gv_play.Visibility = Visibility.Visible;
            gv_story_list.Visibility = Visibility.Collapsed;
            grid_Setting.Visibility = Visibility.Collapsed;
            grid_DM.Visibility = Visibility.Collapsed;
            grid_Info.Visibility = Visibility.Collapsed;
            grid_PB.Visibility = Visibility.Collapsed;
            grid_Subtitle.Visibility = Visibility.Collapsed;
            sp_View.IsPaneOpen = true;
        }



        private void cb_Quity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (!QuityLoading && cb_Quity.SelectedItem != null)
            {
                var item = playNow;
                var position = mediaPlayer?.PlaybackSession.Position ?? TimeSpan.Zero;
                var shouldPlay = mediaPlayer?.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
                var quality = (cb_Quity.SelectedItem as QualityModel).qn;
                UpdateLocalHistory(item, Convert.ToInt32(position.TotalSeconds));
                SettingHelper.Set_NewQuality(quality);
                _ = ChangeQualityAsync(item, quality, PlaybackRestoreState.ForQualityChange(position, shouldPlay));
            }
            //if (gv_play.ItemsSource == null)
            //{
            //    return;
            //}
            //mediaElement.Stop();
            //SettingHelper.Set_PlayQualit(cb_Quity.SelectedIndex + 1);
            //OpenVideo();
        }
        private void _lastpost_out_Completed(object sender, object e)
        {
            btn_ViewPost.Visibility = Visibility.Collapsed;
        }

        private void btn_ViewPost_Click(object sender, RoutedEventArgs e)
        {
            if (LastPost != 0)
            {
                var session = mediaElement.MediaPlayer.PlaybackSession;
                session.Position = PlaybackPosition.Clamp(TimeSpan.FromSeconds(LastPost), session.NaturalDuration);
                btn_ViewPost.Visibility = Visibility.Collapsed;

            }
        }

        private void btn_VideoInfo_Click(object sender, RoutedEventArgs e)
        {

            sp_View.IsPaneOpen = true;
            grid_Setting.Visibility = Visibility.Visible;
            gv_play.Visibility = Visibility.Collapsed;
            gv_story_list.Visibility = Visibility.Collapsed;
            grid_DM.Visibility = Visibility.Collapsed;
            grid_Info.Visibility = Visibility.Collapsed;
            grid_PB.Visibility = Visibility.Collapsed;
            grid_Subtitle.Visibility = Visibility.Collapsed;
            //string info = string.Format("视频高度：{0}\r\n视频宽度：{1}\r\n视频长度：{2}\r\n缓冲进度:{3}", mediaElement.NaturalVideoHeight, mediaElement.NaturalVideoWidth, mediaElement.MediaPlayer.PlaybackSession.NaturalDuration.TimeSpan.Hours.ToString("00") + ":" + mediaElement.MediaPlayer.PlaybackSession.NaturalDuration.TimeSpan.Minutes.ToString("00") + ":" + mediaElement.MediaPlayer.PlaybackSession.NaturalDuration.TimeSpan.Seconds.ToString("00"), mediaElement.DownloadProgress.ToString("P"));
            //await new MessageDialog(info, "视频信息").ShowAsync();
        }

        private void cb_setting_defu_Checked(object sender, RoutedEventArgs e)
        {
            if (mediaElement == null)
            {
                return;
            }
            mediaElement.Width = this.ActualWidth;
            mediaElement.Height = this.ActualHeight;
            mediaElement.Stretch = Stretch.Uniform;
        }

        private void cb_setting_43_Checked(object sender, RoutedEventArgs e)
        {
            if (mediaElement == null)
            {
                return;
            }
            mediaElement.Stretch = Stretch.Fill;
            mediaElement.Height = this.ActualHeight;
            mediaElement.Width = this.ActualHeight * 4 / 3;
        }

        private void cb_setting_169_Checked(object sender, RoutedEventArgs e)
        {
            if (mediaElement == null)
            {
                return;
            }
            mediaElement.Stretch = Stretch.Fill;
            mediaElement.Height = this.ActualHeight;
            mediaElement.Width = this.ActualHeight * 16 / 9;


        }

        //private void btn_HideInfo_Click(object sender, RoutedEventArgs e)
        //{
        //    //ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
        //    //MaxWIndowsEvent(true);
        //    btn_HideInfo.Visibility = Visibility.Collapsed;
        //    btn_ShowInfo.Visibility = Visibility.Visible;
        //    danmu.SetJJ();
        //}

        //private void btn_ShowInfo_Click(object sender, RoutedEventArgs e)
        //{
        //    //MaxWIndowsEvent(false);
        //    //ApplicationView.GetForCurrentView().ExitFullScreenMode();
        //    btn_HideInfo.Visibility = Visibility.Visible;
        //    btn_ShowInfo.Visibility = Visibility.Collapsed;
        //    danmu.SetJJ();
        //}





        /// <summary>
        /// 设置系统播放控制器
        /// </summary>
        private void SetSystemMediaTransportControl()
        {
            try
            {
                var controls = mediaPlayer.SystemMediaTransportControls;
                SystemMediaTransportControlsDisplayUpdater updater = controls.DisplayUpdater;
                updater.Type = MediaPlaybackType.Video;
                updater.VideoProperties.Subtitle = playNow.VideoTitle;
                updater.VideoProperties.Title = playNow.Title;

                var imageSource = playNow.ImageSrc;
                if (!string.IsNullOrWhiteSpace(imageSource) && imageSource.StartsWith("//"))
                {
                    imageSource = "https:" + imageSource;
                }
                if (!Uri.TryCreate(imageSource, UriKind.Absolute, out Uri thumbnailUri))
                {
                    thumbnailUri = new Uri("ms-appx:///Assets/Logo.png");
                }
                updater.Thumbnail = RandomAccessStreamReference.CreateFromUri(thumbnailUri);

                updater.Update();
                var timelineProperties = new SystemMediaTransportControlsTimelineProperties();

                timelineProperties.StartTime = TimeSpan.FromSeconds(0);
                timelineProperties.MinSeekTime = TimeSpan.FromSeconds(0);
                timelineProperties.Position = mediaElement.MediaPlayer.PlaybackSession.Position;
                timelineProperties.MaxSeekTime = mediaElement.MediaPlayer.PlaybackSession.NaturalDuration;
                timelineProperties.EndTime = mediaElement.MediaPlayer.PlaybackSession.NaturalDuration;

                controls.UpdateTimelineProperties(timelineProperties);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("设置系统播放控件失败", LogType.ERROR, ex);
            }
        }

        private void menuitem_DM_Click(object sender, RoutedEventArgs e)
        {

            sp_View.IsPaneOpen = true;
            grid_Setting.Visibility = Visibility.Collapsed;
            gv_play.Visibility = Visibility.Collapsed;
            gv_story_list.Visibility = Visibility.Collapsed;
            grid_DM.Visibility = Visibility.Visible;
            grid_Info.Visibility = Visibility.Collapsed;
            grid_Subtitle.Visibility = Visibility.Collapsed;
            grid_PB.Visibility = Visibility.Collapsed;

        }

        private void menuitem_PB_Click(object sender, RoutedEventArgs e)
        {

            mediaElement.MediaPlayer.Pause();
            sp_View.IsPaneOpen = true;
            grid_Setting.Visibility = Visibility.Collapsed;
            gv_play.Visibility = Visibility.Collapsed;
            grid_DM.Visibility = Visibility.Collapsed;
            gv_story_list.Visibility = Visibility.Collapsed;
            grid_Info.Visibility = Visibility.Collapsed;
            grid_Subtitle.Visibility = Visibility.Collapsed;
            grid_PB.Visibility = Visibility.Visible;
            list_DisDanmu.Items.Clear();
            foreach (var item in danmu.GetDanmakus())
            {
                list_DisDanmu.Items.Add(item);
            }
        }

        private void menuitem_Info_Click(object sender, RoutedEventArgs e)
        {

            sp_View.IsPaneOpen = true;
            if (playNow != null && DanMuPool != null)
            {
                txt_Num.Text = DanMuPool.Count.ToString();
                txt_sId.Text = playNow.Aid;
                txt_eId.Text = playNow.Mid;
            }
            grid_Setting.Visibility = Visibility.Collapsed;
            gv_play.Visibility = Visibility.Collapsed;
            gv_story_list.Visibility = Visibility.Collapsed;
            grid_DM.Visibility = Visibility.Collapsed;
            grid_Info.Visibility = Visibility.Visible;
            grid_PB.Visibility = Visibility.Collapsed;
        }

        #region 设置
        private void sw_DanmuBorder_Toggled(object sender, RoutedEventArgs e)
        {
            //danmu.D_Border = sw_DanmuBorder.IsOn;
            SettingHelper.Set_DMBorder(sw_DanmuBorder.IsOn);
        }

        private void slider_DanmuSize_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (danmu == null)
            {
                return;
            }
            danmu.sizeZoom = slider_DanmuSize.Value;

            SettingHelper.Set_NewDMSize(slider_DanmuSize.Value);
        }

        private void cb_Font_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //switch (cb_Font.SelectedIndex)
            //{
            //    case 0:
            //        danmu.fontFamily = "默认";
            //        break;
            //    case 1:
            //        danmu.fontFamily = "微软雅黑";
            //        break;
            //    case 2:
            //        danmu.fontFamily = "黑体";
            //        break;
            //    case 3:
            //        danmu.fontFamily = "楷体";
            //        break;
            //    case 4:
            //        danmu.fontFamily = "宋体";
            //        break;
            //    case 5:
            //        danmu.fontFamily = "等线";
            //        break;
            //    default:
            //        break;
            //}
            if (cb_Font.SelectedItem == null)
            {
                return;
            }
            SettingHelper.Set_DanmuFont(cb_Font.SelectedItem.ToString());
            danmu.font = cb_Font.SelectedItem.ToString();
        }

        private void slider_DanmuSpeed_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (danmu == null)
            {
                return;
            }
            danmu.speed = Convert.ToInt32(slider_DanmuSpeed.Value);
            if (slider_DanmuSpeed.Value == 0 || slider_DanmuSpeed.Value == -1)
            {
                return;
            }
            SettingHelper.Set_DMSpeed(slider_DanmuSpeed.Value);
        }

        private void slider_DanmuTran_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (danmu == null)
            {
                return;
            }
            if (slider_DanmuTran.Value == 0 || slider_DanmuTran.Value == -1)
            {
                return;
            }
            danmu.Opacity = slider_DanmuTran.Value;
            SettingHelper.Set_NewDMTran(slider_DanmuTran.Value);
        }
        private void slider_Num_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (danmu == null)
            {
                return;
            }
            DanmuNum = Convert.ToInt32(slider_Num.Value);
            SettingHelper.Set_DMNumber(Convert.ToInt32(slider_Num.Value));
        }




        private void menu_setting_top_Click(object sender, RoutedEventArgs e)
        {

            danmu.HideDanmaku(NSDanmaku.Model.DanmakuLocation.Top);
            SettingHelper.Set_DMVisTop(false);


        }

        private void menu_setting_buttom_Click(object sender, RoutedEventArgs e)
        {
            danmu.HideDanmaku(NSDanmaku.Model.DanmakuLocation.Bottom);
            SettingHelper.Set_DMVisBottom(false);
        }

        private void menu_setting_gd_Checked(object sender, RoutedEventArgs e)
        {
            danmu.HideDanmaku(NSDanmaku.Model.DanmakuLocation.Roll);
            SettingHelper.Set_DMVisRoll(false);
        }

        private void menu_setting_gd_Unchecked(object sender, RoutedEventArgs e)
        {
            danmu.ShowDanmaku(NSDanmaku.Model.DanmakuLocation.Roll);
            SettingHelper.Set_DMVisRoll(true);
        }

        private void menu_setting_top_Unchecked(object sender, RoutedEventArgs e)
        {
            danmu.ShowDanmaku(NSDanmaku.Model.DanmakuLocation.Top);
            SettingHelper.Set_DMVisTop(true);
        }

        private void menu_setting_buttom_Unchecked(object sender, RoutedEventArgs e)
        {
            // danmu.SetDanmuVisibility(true, MyDanmaku.DanmuMode.Buttom);
            danmu.ShowDanmaku(NSDanmaku.Model.DanmakuLocation.Bottom);
            SettingHelper.Set_DMVisBottom(true);
        }


        private void btn_OK_Click(object sender, RoutedEventArgs e)
        {

            DanDis_Add(txt_Dis.Text, false);
            txt_Dis.Text = "";
            var s = danmu.GetDanmakus();
            foreach (var item in s)
            {
                if (DanDis_Dis(item.text))
                {
                    danmu.Remove(item);
                }
            }
        }






        #endregion

        protected override Size MeasureOverride(Size availableSize)
        {

            return base.MeasureOverride(availableSize);
        }
        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {

        }





        private void btn_Dis_Report_Click(object sender, RoutedEventArgs e)
        {
            if (list_DisDanmu.SelectedItems.Count == 0)
            {
                return;
            }
            foreach (NSDanmaku.Model.DanmakuModel item in list_DisDanmu.SelectedItems)
            {
                ReportDM(item.rowID);
            }
        }

        private async void ReportDM(string dmid)
        {
            try
            {
                string results = await WebClientClass.PostResults(new Uri("https://interface.bilibili.com/dmreport"), string.Format("reportToAdmin=0&reason=&dm_inid={0}&dmid={1}", playNow.Mid, dmid), "https://www.bilibili.com");
                if (results == "0")
                {
                    Utils.ShowMessageToast("举报成功", 3000);
                }
                else
                {
                    Utils.ShowMessageToast("举报失败", 3000);
                }
            }
            catch (Exception)
            {
                Utils.ShowMessageToast("举报错误", 3000);
            }
        }

        private async void menuitem_UpdateDanmu_Click(object sender, RoutedEventArgs e)
        {
            var item = playNow;
            if (item == null)
            {
                return;
            }

            var requestId = playbackRequestGate.Current;
            var cancellationToken = BeginDanmakuLoading();
            try
            {
                var pool = await LoadCompleteDanmakuOrEmptyAsync(
                    Convert.ToInt64(item.Aid),
                    Convert.ToInt64(item.Mid),
                    item.Duration,
                    cancellationToken);
                if (cancellationToken.IsCancellationRequested
                    || !IsPlaybackRequestCurrent(requestId, item))
                {
                    return;
                }

                SetDanmakuPool(pool);
                await LoadInteractiveDanmakuAsync(item, requestId);
                if (!cancellationToken.IsCancellationRequested
                    && IsPlaybackRequestCurrent(requestId, item))
                {
                    Utils.ShowMessageToast("已经更新弹幕池", 3000);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("更新弹幕池失败", LogType.ERROR, ex);
            }

        }


        private void sw_MergeDanmu_Toggled(object sender, RoutedEventArgs e)
        {
            SettingHelper.Set_MergeDanmu(sw_MergeDanmu.IsOn);
            mergeDanmu = sw_MergeDanmu.IsOn;
        }

        private async void sw_InteractiveDanmaku_Toggled(object sender, RoutedEventArgs e)
        {
            if (settingFlag)
            {
                return;
            }

            var enabled = sw_InteractiveDanmaku.IsOn;
            SettingHelper.Set_InteractiveDanmakuStatus(enabled);
            ClearInteractiveDanmaku();
            if (enabled && playNow != null)
            {
                await LoadInteractiveDanmakuAsync(playNow);
                HandleInteractiveDanmakuPosition();
            }
        }

        private void sw_UseNewDanmakuInterface_Toggled(object sender, RoutedEventArgs e)
        {
            if (settingFlag)
            {
                return;
            }

            SettingHelper.Set_UseNewDanmakuInterface(sw_UseNewDanmakuInterface.IsOn);
        }

        private void MTC_OpenDanmaku(object sender, bool e)
        {
            LoadDanmu = e;
            if (!e)
            {
                currentInteractiveDanmaku = null;
                interactiveDanmakuControl.HideItem();
            }
        }

        private async void InteractiveDanmakuControl_ActionRequested(
            object sender,
            InteractiveDanmakuActionEventArgs e)
        {
            var item = e?.Item;
            var playbackItem = playNow;
            if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
            {
                return;
            }

            switch (e.Action)
            {
                case InteractiveDanmakuActionKind.OpenUser:
                {
                    if (item.SenderMid <= 0)
                    {
                        interactiveDanmakuControl.ShowStatus("该互动弹幕没有可打开的 UP 主");
                        return;
                    }

                    MessageCenter.SendNavigateTo(
                        NavigateMode.Info,
                        typeof(UserCenterPage),
                        item.SenderMid.ToString());
                    return;
                }
                case InteractiveDanmakuActionKind.OpenVideo:
                {
                    var targetVideo = item.Type == InteractiveDanmakuType.Link
                        ? (!string.IsNullOrWhiteSpace(item.RelatedBvid)
                            ? item.RelatedBvid
                            : item.RelatedAid > 0 ? item.RelatedAid.ToString() : string.Empty)
                        : item.Type == InteractiveDanmakuType.Attention
                            && item.AttentionType != 0
                            && playbackItem != null
                            ? playbackItem.Aid
                            : string.Empty;
                    if (string.IsNullOrWhiteSpace(targetVideo))
                    {
                        interactiveDanmakuControl.ShowStatus("该互动弹幕没有关联视频");
                        return;
                    }

                    MessageCenter.SendNavigateTo(
                        NavigateMode.Info,
                        typeof(VideoViewPage),
                        targetVideo);
                    return;
                }
                case InteractiveDanmakuActionKind.Follow:
                {
                    if (item.Type != InteractiveDanmakuType.Attention || item.SenderMid <= 0)
                    {
                        interactiveDanmakuControl.ShowStatus("该互动弹幕没有可关注的 UP 主");
                        return;
                    }

                    if (!ApiHelper.IsLogin())
                    {
                        var loggedIn = await Utils.ShowLoginDialog();
                        if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
                        {
                            return;
                        }

                        if (!loggedIn)
                        {
                            interactiveDanmakuControl.ShowStatus("请先登录");
                            return;
                        }
                    }

                    if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
                    {
                        return;
                    }

                    interactiveDanmakuControl.SetActionEnabled(false);
                    try
                    {
                        var followResult = await new Account().Follow(item.SenderMid.ToString());
                        if (followResult != null && followResult.success)
                        {
                            item.AttentionSubmitted = true;
                        }

                        if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
                        {
                            return;
                        }

                        if (followResult == null || !followResult.success)
                        {
                            interactiveDanmakuControl.ShowStatus(
                                string.IsNullOrWhiteSpace(followResult?.message)
                                    ? "关注失败"
                                    : followResult.message);
                            interactiveDanmakuControl.SetActionEnabled(true);
                            return;
                        }

                        interactiveDanmakuControl.ShowStatus("关注成功");
                        interactiveDanmakuControl.ShowAttentionResult();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLog("关注互动弹幕 UP 主失败", LogType.ERROR, ex);
                        if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
                        {
                            return;
                        }

                        interactiveDanmakuControl.ShowStatus("关注失败");
                        interactiveDanmakuControl.SetActionEnabled(true);
                    }
                    return;
                }
                case InteractiveDanmakuActionKind.Triple:
                {
                    if (item.Type != InteractiveDanmakuType.Attention || item.AttentionType == 0)
                    {
                        interactiveDanmakuControl.ShowStatus("该互动弹幕不支持一键三连");
                        return;
                    }

                    var videoAid = playbackItem?.Aid;
                    if (string.IsNullOrWhiteSpace(videoAid))
                    {
                        interactiveDanmakuControl.ShowStatus("当前视频不支持一键三连");
                        return;
                    }

                    if (!ApiHelper.IsLogin())
                    {
                        var loggedIn = await Utils.ShowLoginDialog();
                        if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
                        {
                            return;
                        }

                        if (!loggedIn)
                        {
                            interactiveDanmakuControl.ShowStatus("请先登录");
                            return;
                        }
                    }

                    if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
                    {
                        return;
                    }

                    interactiveDanmakuControl.SetActionEnabled(false);
                    try
                    {
                        var tripleResult = await new VideoAPI().Triple(videoAid).Request();
                        var tripleData = tripleResult != null && tripleResult.status
                            ? await tripleResult.GetJson<ApiDataModel<JObject>>()
                            : null;
                        var isSuccess = tripleData != null && tripleData.success;
                        if (isSuccess)
                        {
                            item.TripleSubmitted = true;
                        }

                        if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
                        {
                            return;
                        }

                        if (!isSuccess)
                        {
                            var message = !string.IsNullOrWhiteSpace(tripleData?.message)
                                ? tripleData.message
                                : !string.IsNullOrWhiteSpace(tripleResult?.message)
                                    ? tripleResult.message
                                    : "三连失败";
                            interactiveDanmakuControl.ShowStatus(message);
                            interactiveDanmakuControl.SetActionEnabled(true);
                            return;
                        }

                        interactiveDanmakuControl.ShowStatus("三连完成");
                        interactiveDanmakuControl.ShowTripleResult();
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLog("互动弹幕一键三连失败", LogType.ERROR, ex);
                        if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
                        {
                            return;
                        }

                        interactiveDanmakuControl.ShowStatus("三连失败");
                        interactiveDanmakuControl.SetActionEnabled(true);
                    }
                    return;
                }
                case InteractiveDanmakuActionKind.Submit:
                {
                    break;
                }
                default:
                {
                    return;
                }
            }

            if (item.Type != InteractiveDanmakuType.Vote
                    && item.Type != InteractiveDanmakuType.Grade)
            {
                interactiveDanmakuControl.ShowStatus("当前互动弹幕不支持提交");
                return;
            }

            if (!ApiHelper.IsLogin())
            {
                var loggedIn = await Utils.ShowLoginDialog();
                if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
                {
                    return;
                }

                if (!loggedIn)
                {
                    interactiveDanmakuControl.ShowStatus("请先登录");
                    return;
                }
            }

            if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
            {
                return;
            }

            if (!long.TryParse(playbackItem.Aid, out var aid)
                || !long.TryParse(playbackItem.Mid, out var cid))
            {
                interactiveDanmakuControl.ShowStatus("当前视频不支持提交");
                return;
            }

            var position = mediaPlayer?.PlaybackSession.Position.TotalMilliseconds ?? item.Progress;
            var progress = position > int.MaxValue
                ? int.MaxValue
                : Math.Max(0, Convert.ToInt32(position));
            var selectedValue = e.Value;
            interactiveDanmakuControl.SetActionEnabled(false);

            try
            {
                InteractiveDanmakuSubmitResult result;
                if (item.Type == InteractiveDanmakuType.Vote)
                {
                    result = await InteractiveDanmakuService.SubmitVoteAsync(
                        aid,
                        cid,
                        progress,
                        item.VoteId,
                        selectedValue);
                }
                else
                {
                    result = await InteractiveDanmakuService.SubmitGradeAsync(
                        aid,
                        cid,
                        progress,
                        item.GradeId,
                        selectedValue * 2);
                }

                if (result != null && result.Success)
                {
                    MarkInteractiveDanmakuSubmitted(item, selectedValue);
                }

                if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
                {
                    return;
                }

                interactiveDanmakuControl.ShowStatus(result?.Message);
                if (result == null || !result.Success)
                {
                    interactiveDanmakuControl.SetActionEnabled(true);
                }
                else if (item.Type == InteractiveDanmakuType.Vote)
                {
                    interactiveDanmakuControl.ShowVoteResult(selectedValue);
                }
                else
                {
                    interactiveDanmakuControl.ShowGradeResult(selectedValue);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("提交互动弹幕失败", LogType.ERROR, ex);
                if (!IsCurrentInteractiveDanmakuItem(item, playbackItem))
                {
                    return;
                }

                interactiveDanmakuControl.ShowStatus("提交互动弹幕失败");
                interactiveDanmakuControl.SetActionEnabled(true);
            }
        }

        private bool IsCurrentInteractiveDanmakuItem(
            InteractiveDanmakuModel item,
            PlayerModel playbackItem)
        {
            return item != null
                && ReferenceEquals(playNow, playbackItem)
                && interactiveDanmakuControl != null
                && interactiveDanmakuControl.IsShowingItem(item);
        }

        private static void MarkInteractiveDanmakuSubmitted(
            InteractiveDanmakuModel item,
            int selectedValue)
        {
            if (item == null)
            {
                return;
            }

            if (item.Type == InteractiveDanmakuType.Vote)
            {
                if (item.VoteSubmitted)
                {
                    return;
                }

                var selectedOption = item.Options.FirstOrDefault(
                    option => option.Index == selectedValue);
                if (selectedOption != null)
                {
                    selectedOption.Count = Math.Max(0, selectedOption.Count) + 1;
                }

                item.SelectedVoteOption = selectedValue;
                item.VoteSubmitted = true;
                return;
            }

            if (item.Type != InteractiveDanmakuType.Grade || item.GradeSubmitted)
            {
                return;
            }

            var selectedScore = Math.Max(1, Math.Min(5, selectedValue));
            var previousCount = Math.Max(0, item.Count);
            var previousAverageScore = Math.Max(0, item.AverageScore);
            var submittedScore = selectedScore * 2;
            item.Count = previousCount + 1;
            item.AverageScore = previousCount <= 0
                ? submittedScore
                : (previousAverageScore * previousCount + submittedScore) / item.Count;
            item.SelectedGradeScore = selectedScore;
            item.GradeSubmitted = true;
        }

        private void MTC_ExitPlayer(object sender, EventArgs e)
        {
            this.Frame.GoBack();
        }

        private async void MTC_OnMiniWindows(object sender, EventArgs e)
        {
            if (ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay))
            {
                await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.CompactOverlay);
                danmu.ClearAll();
                danmu.SetSpeed(5);
                danmu.sizeZoom = 0.5;
            }
        }

        private async void MTC_OnExitMiniWindows(object sender, EventArgs e)
        {
            await ApplicationView.GetForCurrentView().TryEnterViewModeAsync(ApplicationViewMode.Default);
            danmu.ClearAll();
            danmu.speed = SettingHelper.Get_DMSpeed().ToInt32();
            danmu.sizeZoom = SettingHelper.Get_NewDMSize();
        }

        private void MTC_DanmakuSetting(object sender, EventArgs e)
        {
            sp_View.IsPaneOpen = true;
            grid_Setting.Visibility = Visibility.Collapsed;
            gv_play.Visibility = Visibility.Collapsed;
            gv_story_list.Visibility = Visibility.Collapsed;
            grid_DM.Visibility = Visibility.Visible;
            grid_Info.Visibility = Visibility.Collapsed;
            grid_Subtitle.Visibility = Visibility.Collapsed;
            grid_PB.Visibility = Visibility.Collapsed;
        }

        private void MTC_SelectList(object sender, EventArgs e)
        {
            if (playNow.isInteraction)
            {
                gv_story_list.Visibility = Visibility.Visible;
                gv_play.Visibility = Visibility.Collapsed;

            }
            else
            {
                gv_play.Visibility = Visibility.Visible;
                gv_story_list.Visibility = Visibility.Collapsed;
            }

            grid_Setting.Visibility = Visibility.Collapsed;
            grid_DM.Visibility = Visibility.Collapsed;
            grid_Info.Visibility = Visibility.Collapsed;
            grid_Subtitle.Visibility = Visibility.Collapsed;
            grid_PB.Visibility = Visibility.Collapsed;

            sp_View.IsPaneOpen = true;
        }

        private void MTC_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (!SettingHelper.IsPc() || SettingHelper.IsTabletMode())
            {
                if (mediaElement.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                {
                    mediaElement.MediaPlayer.Pause();
                }
                else
                {
                    mediaElement.MediaPlayer.Play();
                }
            }
            else
            {
                if (MTC.IsFullWindow)
                {
                    MTC.ExitFull();
                    //mediaElement.IsFullWindow = false;
                }
                else
                {
                    MTC.ToFull();
                    //mediaElement.IsFullWindow = true;
                }
            }
        }

        private void MTC_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (SettingHelper.IsPc() && e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if (mediaElement.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                {
                    mediaElement.MediaPlayer.Pause();
                }
                else
                {
                    mediaElement.MediaPlayer.Play();
                }
            }
        }

        private void MTC_Next(object sender, EventArgs e)
        {
            gv_play.SelectedIndex += 1;
        }

        private void MTC_Previous(object sender, EventArgs e)
        {
            gv_play.SelectedIndex -= 1;
        }

        private async void MTC_SendDanmakued(object sender, EventArgs e)
        {
            if (!ApiHelper.IsLogin() && !await Utils.ShowLoginDialog())
            {
                Utils.ShowMessageToast("请先登录!", 3000);
            }
            CoreWindow.GetForCurrentThread().KeyDown -= PlayerPage_KeyDown;
            hidePointerFlag = true;
            mediaElement.MediaPlayer.Pause();
            SendDanmakuDialog dialog = new SendDanmakuDialog(playNow.Aid, playNow.Mid, mediaElement.MediaPlayer.PlaybackSession.Position.TotalSeconds);
            dialog.DanmakuSended += new EventHandler<SendDanmakuModel>((obj, item) =>
            {

                if (item.location == 1)
                {
                    danmu.AddRollDanmu(new NSDanmaku.Model.DanmakuModel { text = item.text, color = item.color.ToColor(), size = 25 }, true);
                }
                if (item.location == 4)
                {
                    danmu.AddBottomDanmu(new NSDanmaku.Model.DanmakuModel { text = item.text, color = item.color.ToColor(), size = 25 }, true);
                }
                if (item.location == 5)
                {
                    danmu.AddTopDanmu(new NSDanmaku.Model.DanmakuModel { text = item.text, color = item.color.ToColor(), size = 25 }, true);
                }
                mediaElement.MediaPlayer.Play();
            });
            await dialog.ShowAsync();
            CoreWindow.GetForCurrentThread().KeyDown += PlayerPage_KeyDown;
            hidePointerFlag = false;
            mediaElement.MediaPlayer.Play();
        }

        private void MTC_ShareEvent(object sender, EventArgs e)
        {
            Utils.SetClipboard(string.Format("https://www.bilibili.com/video/av{0}", playNow.Aid));
            Utils.ShowMessageToast("已将内容复制到剪切板", 3000);
        }

        private async void MTC_CoinsEvent(object sender, EventArgs e)
        {
            if (SettingHelper.IsPc())
            {
                MessageDialog messageDialog = new MessageDialog("确定要投币吗?", "投币");
                messageDialog.Commands.Add(new UICommand("投币X1", (com) => { TouBi(1); }, "1"));
                messageDialog.Commands.Add(new UICommand("投币X2", (com) => { TouBi(2); }, "2"));
                messageDialog.Commands.Add(new UICommand("取消", (com) => { }, "0"));
                await messageDialog.ShowAsync();
            }
            else
            {
                MenuFlyout menuFlyout = new MenuFlyout();
                var menu1 = new MenuFlyoutItem() { Text = "投币X1" };
                menu1.Click += new RoutedEventHandler((x, y) => { TouBi(1); });
                var menu2 = new MenuFlyoutItem() { Text = "投币X2" };
                menu2.Click += new RoutedEventHandler((x, y) => { TouBi(2); });
                menuFlyout.Items.Add(menu1);
                menuFlyout.Items.Add(menu2);

                menuFlyout.ShowAt(sender as AppBarButton);
            }

        }
        public async void TouBi(int num)
        {
            if (!ApiHelper.IsLogin() && !await Utils.ShowLoginDialog())
            {
                Utils.ShowMessageToast("请先登录!", 3000);
            }

            try
            {
                WebClientClass wc = new WebClientClass();
                Uri ReUri = new Uri("https://app.bilibili.com/x/v2/view/coin/add");
                string QuStr = string.Format("access_key={0}&aid={1}&appkey={2}&build=540000&from=7&mid={3}&platform=android&&multiply={4}&ts={5}", ApiHelper.access_key, playNow.Aid, ApiHelper.AndroidKey.Appkey, ApiHelper.GetUserId(), num, ApiHelper.GetTimeSpan);
                QuStr += "&sign=" + ApiHelper.GetSign(QuStr);
                string result = await WebClientClass.PostResults(ReUri, QuStr);
                JObject jObject = JObject.Parse(result);
                if (Convert.ToInt32(jObject["code"].ToString()) == 0)
                {
                    Utils.ShowMessageToast("投币成功！", 3000);
                }
                else
                {
                    Utils.ShowMessageToast(jObject["message"].ToString(), 3000);
                }
            }
            catch (Exception ex)
            {
                Utils.ShowMessageToast("投币时发生错误\r\n" + ex.Message, 3000);
            }

        }

        private void cb_Style_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cb_Style.SelectedIndex == -1 || danmu == null)
            {
                return;
            }
            danmu.borderStyle = (NSDanmaku.Model.DanmakuBorderStyle)cb_Style.SelectedIndex;
            SettingHelper.Set_DMStyle(cb_Style.SelectedIndex);

        }

        private void sw_DanmuNotSubtitle_Toggled(object sender, RoutedEventArgs e)
        {
            if (danmu == null)
            {
                return;
            }
            danmu.notHideSubtitle = sw_DanmuNotSubtitle.IsOn;
            SettingHelper.Set_DanmuNotSubtitle(sw_DanmuNotSubtitle.IsOn);

        }

        private void slider_Rate_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (mediaElement == null)
            {
                return;
            }
            mediaElement.MediaPlayer.PlaybackSession.PlaybackRate = slider_Rate.Value;
            if (mediaPlayer_audio != null)
            {
                mediaPlayer_audio.PlaybackSession.PlaybackRate = mediaPlayer.PlaybackSession.PlaybackRate;
            }
        }

        private void MTC_FullWindows(object sender, EventArgs e)
        {
            ApplicationView.GetForCurrentView().TryEnterFullScreenMode();
        }

        private void MTC_ExitFullWindows(object sender, EventArgs e)
        {
            ApplicationView.GetForCurrentView().ExitFullScreenMode();
        }

        private void menuitem_Capture_Click(object sender, RoutedEventArgs e)
        {
            CaptureVideo();
        }
        private async void CaptureVideo()
        {
            try
            {
                MTC.Visibility = Visibility.Collapsed;
                string fileName = DateTime.Now.ToString("yyyyMMddHHmmss") + ".jpg";
                StorageFolder applicationFolder = KnownFolders.PicturesLibrary;
                StorageFolder folder = await applicationFolder.CreateFolderAsync("bilibili UWP", CreationCollisionOption.OpenIfExists);
                StorageFile saveFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
                RenderTargetBitmap bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(mediaElement);
                var pixelBuffer = await bitmap.GetPixelsAsync();
                using (var fileStream = await saveFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Ignore,
                         (uint)bitmap.PixelWidth,
                         (uint)bitmap.PixelHeight,
                         DisplayInformation.GetForCurrentView().LogicalDpi,
                         DisplayInformation.GetForCurrentView().LogicalDpi,
                         pixelBuffer.ToArray());
                    await encoder.FlushAsync();
                }
                Utils.ShowMessageToast("截图已经保存至图片库");
                MTC.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                Utils.ShowMessageToast("截图失败");
            }

        }

        private void MTC_Captured(object sender, EventArgs e)
        {
            CaptureVideo();
        }

        private void MTC_Tapped_1(object sender, TappedRoutedEventArgs e)
        {
            MTC.HideOrShowMTC();
        }

        private void MTC_FastForward(object sender, double e)
        {
            if (mediaPlayer == null)
            {
                return;
            }

            var session = mediaPlayer.PlaybackSession;
            session.Position = PlaybackPosition.Clamp(session.Position.Add(TimeSpan.FromSeconds(e)), session.NaturalDuration);
        }




        private void sw_BoldDanmu_Toggled(object sender, RoutedEventArgs e)
        {
            if (danmu == null)
            {
                return;
            }
            danmu.bold = sw_BoldDanmu.IsOn;
            SettingHelper.Set_BoldDanmu(sw_BoldDanmu.IsOn);
        }

        private async void menuitem_LocalDanmu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Windows.Storage.Pickers.FileOpenPicker fileOpenPicker = new Windows.Storage.Pickers.FileOpenPicker();
                fileOpenPicker.FileTypeFilter.Add(".xml");
                var file = await fileOpenPicker.PickSingleFileAsync();
                if (file != null)
                {
                    var ls = await danmakuParse.ParseBiliBili(file);
                    AppendDanmakuPool(ls);
                }
            }
            catch (Exception)
            {
                Utils.ShowMessageToast("加载失败");
            }


        }

        private async void menuitem_tantan_Click(object sender, RoutedEventArgs e)
        {
            NSDanmaku.Controls.TantanDialog tantanDialog = new NSDanmaku.Controls.TantanDialog();
            tantanDialog.ReturnDanmakus += TantanDialog_ReturnDanmakus;
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            hidePointerFlag = true;
            await tantanDialog.ShowAsync();
            hidePointerFlag = false;
        }

        private void TantanDialog_ReturnDanmakus(object sender, List<NSDanmaku.Model.DanmakuModel> e)
        {
            AppendDanmakuPool(e);
        }

        private void Gridview_node_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ((ItemsWrapGrid)gridview_node.ItemsPanelRoot).ItemWidth = (e.NewSize.Width) / 2;
        }

        private void Gridview_node_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickItem = e.ClickedItem as Choices;
            ChangeNode(clickItem.node_id, clickItem.cid.ToString());
        }
        public async void ChangeNode(int node_id, string cid)
        {
            ClearBiliJumpAds();
            var data = await interactionVideo.GetNodes(node_id);
            if (data == null)
            {
                Utils.ShowMessageToast("加载分支失败，请重试");
                return;
            }
            nodeInfo = data;
            gridview_node.ItemsSource = nodeInfo.edges?.choices;
            gv_story_list.ItemsSource = nodeInfo.story_list;
            settingStorylist = true;
            gv_story_list.SelectedItem = nodeInfo.story_list.FirstOrDefault(x => x.node_id == data.node_id);
            settingStorylist = false;
            playNow.Mid = cid;
            playNow.node_id = node_id;
            playNow.VideoTitle = data.title;
            gridview_node.Visibility = Visibility.Collapsed;
            SetDanmakuPool(await LoadCompleteDanmakuOrEmptyAsync(
                Convert.ToInt64(playNow.Aid),
                Convert.ToInt64(playNow.Mid),
                playNow.Duration));
            danmu.ClearAll();
            var item = playNow;
            var quality = (cb_Quity.SelectedItem as QualityModel)?.qn ?? 64;
            await ChangeQualityAsync(item, quality, PlaybackRestoreState.ForContentChange());
            await LoadInteractiveDanmakuAsync(item);
        }



        bool settingStorylist = false;
        private void Gv_story_list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gv_story_list.SelectedItem == null || settingStorylist)
            {
                return;
            }

            var clickItem = gv_story_list.SelectedItem as StoryList;
            ChangeNode(clickItem.node_id, clickItem.cid.ToString());

        }

        private void DASHVideoCodec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (settingFlag)
            {
                return;
            }
            if (!(sender is ComboBox comboBox)
                || !(comboBox.SelectedItem is ComboBoxItem item)
                || !int.TryParse(item.Tag?.ToString(), out var codecId))
            {
                return;
            }

            SettingHelper.Set_DASHVideoCodecPreference(codecId);
            Utils.ShowMessageToast("更改清晰度或重新加载生效");
        }

        private void SetDASHVideoCodecSelection(int codecId)
        {
            cb_DASHVideoCodec.SelectedIndex = codecId == 12 ? 1 : codecId == 13 ? 2 : 0;
        }

        private void DASHForceVideoCodec_Toggled(object sender, RoutedEventArgs e)
        {
            if (settingFlag)
            {
                return;
            }

            SettingHelper.Set_DASHForceVideoCodec(sw_DASHForceVideoCodec.IsOn);
            Utils.ShowMessageToast("更改清晰度或重新加载生效");
        }

        private void sw_ForceVideo_Toggled(object sender, RoutedEventArgs e)
        {
            if (settingFlag)
            {
                return;
            }

            SettingHelper.Set_ForceVideo(sw_ForceVideo.IsOn);
            SYEngine.Core.ForceSoftwareDecode = sw_ForceVideo.IsOn;
            Utils.ShowMessageToast("更改清晰度或重新加载生效");
        }

        private void Sw_UseDASH_Toggled(object sender, RoutedEventArgs e)
        {
            if (settingFlag)
            {
                return;
            }
            if (sw_UseDASH.IsOn && SystemHelper.GetSystemBuild() < 17763)
            {
                Utils.ShowMessageToast("系统版本1809以上才可以开启");
                sw_UseDASH.IsOn = false;
                return;
            }
            SettingHelper.Set_UseDASH(sw_UseDASH.IsOn);
            Utils.ShowMessageToast("更改清晰度或重新加载生效");
        }


        private void Sp_View_PaneClosed(SplitView sender, object args)
        {

        }

        private void Cb_SubtitleFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cb_SubtitleFont.SelectedItem == null)
            {
                return;
            }
            SettingHelper.Set_SubtitleFontFamily(cb_SubtitleFont.SelectedItem.ToString());
            MTC.SubTitleFontFamily = new FontFamily(cb_SubtitleFont.SelectedItem.ToString());
        }

        private void Cb_SubtitleColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cb_SubtitleColor.SelectedItem == null)
            {
                return;
            }
            MTC.SubTitleColor = new SolidColorBrush(Utils.ToColor2((cb_SubtitleColor.SelectedItem as ComboBoxItem).Tag.ToString()));
            SettingHelper.Set_SubtitleColor((cb_SubtitleColor.SelectedItem as ComboBoxItem).Tag.ToString());
        }

        private void Slider_SubtitleSize_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (MTC == null)
            {
                return;
            }
            MTC.SubTitleFontSize = e.NewValue;
            SettingHelper.Set_SubtitleSize(e.NewValue);
        }

        private void Menuitem_SubtitleSetting_Click(object sender, RoutedEventArgs e)
        {
            sp_View.IsPaneOpen = true;
            grid_Setting.Visibility = Visibility.Collapsed;
            gv_play.Visibility = Visibility.Collapsed;
            gv_story_list.Visibility = Visibility.Collapsed;
            grid_DM.Visibility = Visibility.Collapsed;
            grid_Info.Visibility = Visibility.Collapsed;
            grid_Subtitle.Visibility = Visibility.Visible;
            grid_PB.Visibility = Visibility.Collapsed;
        }

        private void Slider_SubtitleTran_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (MTC == null)
            {
                return;
            }
            MTC.SubTitleBackground = new SolidColorBrush(Color.FromArgb(Convert.ToByte(e.NewValue * 255), 0, 0, 0));
            SettingHelper.Set_SubtitleBgTran(e.NewValue);
        }

        #region 播放器事件
        private async void mediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                SetSystemMediaTransportControl();

                var record = SqlHelper.GetVideoWatchRecord(string.IsNullOrEmpty(playNow.episode_id) ? playNow.Mid : "ep" + playNow.episode_id);
                if (record != null && record.Post != 0)
                {
                    if (SettingHelper.Get_SkipToHistory())
                    {
                        var session = mediaElement.MediaPlayer.PlaybackSession;
                        session.Position = PlaybackPosition.Clamp(TimeSpan.FromSeconds(record.Post), session.NaturalDuration);
                    }
                    else
                    {
                        TimeSpan ts = new TimeSpan(0, 0, record.Post);
                        LastPost = record.Post;
                        btn_ViewPost.Content = "上次播放到" + ts.Hours.ToString("00") + ":" + ts.Minutes.ToString("00") + ":" + ts.Seconds.ToString("00");
                        btn_ViewPost.Visibility = Visibility.Visible;
                        await Task.Delay(5000);
                        btn_ViewPost.Visibility = Visibility.Collapsed;
                    }
                }


            }
            catch (Exception)
            {

            }

        }
        private async void mediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cb_setting_1.IsChecked.Value)
                {
                    mediaElement.MediaPlayer.Play();
                    danmu.ClearAll();
                    return;
                }
                if (gv_play.SelectedIndex == gv_play.Items.Count - 1)
                {
                    if (playNow.isInteraction)
                    {
                        if (nodeInfo.edges != null)
                        {
                            if (nodeInfo.edges.choices.Count == 1)
                            {
                                ChangeNode(nodeInfo.edges.choices[0].node_id, nodeInfo.edges.choices[0].cid.ToString());
                            }
                            else
                            {
                                gridview_node.Visibility = Visibility.Visible;
                            }
                        }
                        else
                        {
                            Utils.ShowMessageToast("互动视频已结束，可点击右下角选择节点重新开始", 3000);
                        }

                    }
                    else
                    {
                        if (cb_setting_2.IsChecked.Value)
                        {
                            gv_play.SelectedIndex = 0;
                        }
                        else
                        {
                            Utils.ShowMessageToast("全部看完了", 3000);
                        }
                    }
                }
                else
                {
                    //mediaElement.MediaPlayer.PlaybackSession.();
                    Utils.ShowMessageToast("3秒后播放下一集", 3000);
                    await Task.Delay(3000);
                    //等待期间用户可能已退出播放页,不再继续播放下一集
                    if (_isExiting)
                    {
                        return;
                    }
                    gv_play.SelectedIndex += 1;
                }
            }
            catch (Exception)
            {
            }

        }
        private async void mediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            //if (e.ErrorMessage.Contains("SRC_NOT_SUPPORT"))
            //{
            //    await new MessageDialog("暂时无法播放此视频，请稍后再试").ShowAsync();
            //}
            //else
            //{
            //    await new MessageDialog("无法播放此视频" + e.ErrorMessage).ShowAsync();

            //}
            await new MessageDialog("无法播放此视频 ＞﹏＜ \r\n请尝试更换清晰度或者在播放设置中打开/关闭DASH").ShowAsync();
        }

        bool buffering = false;
        private void mediaElement_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            _PointerHideTime = 1;
        }
        private void mediaElement_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            _PointerHideTime = 1;
        }
        int _PointerHideTime = 1;
        private void Grid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
            _PointerHideTime = 1;
        }

        #endregion

        private void MTC_Video360Changed(object sender, bool e)
        {
            if (mediaPlayer.PlaybackSession.SphericalVideoProjection.FrameFormat == SphericalVideoFrameFormat.None)
            {
                mediaPlayer.PlaybackSession.SphericalVideoProjection.FrameFormat = SphericalVideoFrameFormat.Equirectangular;
            }
            mediaPlayer.PlaybackSession.SphericalVideoProjection.IsEnabled = e;
            if (e)
            {
                mediaPlayer.PlaybackSession.SphericalVideoProjection.ProjectionMode = SphericalVideoProjectionMode.Spherical;
                mediaPlayer.PlaybackSession.SphericalVideoProjection.HorizontalFieldOfViewInDegrees = 90;
            }

        }

        private void SetVolume(double volume)
        {
            if (volume >= 1)
                volume = 1;
            if (volume <= 0)
                volume = 0;
            mediaElement.MediaPlayer.Volume = volume;
            if (mediaPlayer_audio != null)
            {
                mediaPlayer_audio.Volume = volume;
            }
        }

        private async void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ZXing.BarcodeWriter barcodeWriter = new ZXing.BarcodeWriter();
                barcodeWriter.Format = ZXing.BarcodeFormat.QR_CODE;
                barcodeWriter.Options = new ZXing.Common.EncodingOptions()
                {
                    Margin = 1,
                    Height = 200,
                    Width = 200
                };
                var data = barcodeWriter.Write("http://b23.tv/av" + playNow.Aid);
                Image imgQR = new Image()
                {
                    Width = 200
                };
                imgQR.Source = data;
                ContentDialog contentDialog = new ContentDialog()
                {
                    Title="手机续看",
                    Content = imgQR,
                    CloseButtonText = "关闭"
                };

                await ReportHistory(Convert.ToInt32(mediaElement.MediaPlayer.PlaybackSession.Position.TotalSeconds));
                await contentDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("创建二维码失败", LogType.ERROR, ex);
                Utils.ShowMessageToast("创建二维码失败");
            }
        }
    }
}
