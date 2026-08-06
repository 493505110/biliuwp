using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml;

namespace BiliBili.UWP.Helper
{
    public static class MusicHelper
    {
        public enum MusicPlayMode
        {
            listLoop,//列表循环
            songLoop,//单曲循环
            random,//随机播放
            sequence//顺序
        }
        public static event EventHandler<Visibility> DisplayEvent;
        public static event EventHandler<MusicPlayModel> MediaChanged;
        public static event EventHandler<List<MusicPlayModel>> UpdateList;


        public static List<MusicPlayModel> playList;
        //public static MusicPlayMode musicPlayMode;
        public static MediaPlayer _mediaPlayer;
        public static MediaPlaybackList _mediaPlaybackList;
        private static readonly object SystemMediaTransportControlsLock = new object();
        private static bool _ownsSystemMediaTransportControls;
        private static bool _suppressPlaybackActivationUntilStopped;

        public static void InitializeMusicPlay()
        {
            playList = new List<MusicPlayModel>();
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.AutoPlay = true;
            _mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Media;
            _mediaPlayer.CommandManager.IsEnabled = false;
            _mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            

           
            _mediaPlaybackList = new MediaPlaybackList();
            _mediaPlaybackList.AutoRepeatEnabled = true;
            _mediaPlaybackList.CurrentItemChanged += _mediaPlaybackList_CurrentItemChanged;

            _mediaPlayer.Source = _mediaPlaybackList;
        }

        private static bool TryGetCurrentMusic(out MusicPlayModel music)
        {
            music = null;
            if (_mediaPlaybackList == null || playList == null || _mediaPlaybackList.Items.Count == 0)
            {
                return false;
            }

            var index = _mediaPlaybackList.CurrentItemIndex;
            if (index == uint.MaxValue || index >= (uint)playList.Count)
            {
                return false;
            }

            music = playList[(int)index];
            return true;
        }

        private static void UpdateSystemMediaTransportControlsMetadata(MusicPlayModel music)
        {
            try
            {
                var controls = _mediaPlayer.SystemMediaTransportControls;
                var updater = controls.DisplayUpdater;
                updater.Type = MediaPlaybackType.Music;
                updater.MusicProperties.Artist = music.artist;
                updater.MusicProperties.Title = music.title;
                if (!string.IsNullOrWhiteSpace(music.pic) &&
                    Uri.TryCreate(music.pic, UriKind.Absolute, out Uri picUri))
                {
                    updater.Thumbnail = Windows.Storage.Streams.RandomAccessStreamReference.CreateFromUri(picUri);
                }
                updater.Update();

                var timelineProperties = new SystemMediaTransportControlsTimelineProperties
                {
                    StartTime = TimeSpan.FromSeconds(0),
                    MinSeekTime = TimeSpan.FromSeconds(0),
                    Position = _mediaPlayer.PlaybackSession.Position,
                    MaxSeekTime = _mediaPlayer.PlaybackSession.NaturalDuration,
                    EndTime = _mediaPlayer.PlaybackSession.NaturalDuration
                };
                controls.UpdateTimelineProperties(timelineProperties);
            }
            catch (Exception)
            {
                // 元数据更新失败不应影响播放
            }
        }

        private static void ActivateSystemMediaTransportControls(bool takeOwnership)
        {
            lock (SystemMediaTransportControlsLock)
            {
                if (_mediaPlayer == null || !TryGetCurrentMusic(out var music))
                {
                    return;
                }

                if (takeOwnership)
                {
                    _ownsSystemMediaTransportControls = true;
                    _suppressPlaybackActivationUntilStopped = false;
                }

                if (!_ownsSystemMediaTransportControls)
                {
                    return;
                }

                _mediaPlayer.CommandManager.IsEnabled = true;
                _mediaPlayer.SystemMediaTransportControls.IsEnabled = true;
                UpdateSystemMediaTransportControlsMetadata(music);
            }
        }

        private static void ReleaseSystemMediaTransportControls()
        {
            lock (SystemMediaTransportControlsLock)
            {
                _ownsSystemMediaTransportControls = false;
                if (_mediaPlayer == null)
                {
                    return;
                }

                var controls = _mediaPlayer.SystemMediaTransportControls;
                controls.DisplayUpdater.ClearAll();
                controls.PlaybackStatus = MediaPlaybackStatus.Closed;
                controls.IsEnabled = false;
                _mediaPlayer.CommandManager.IsEnabled = false;
            }
        }

        private static void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            if (sender.PlaybackState != MediaPlaybackState.Playing)
            {
                if (sender.PlaybackState == MediaPlaybackState.Paused ||
                    sender.PlaybackState == MediaPlaybackState.None)
                {
                    lock (SystemMediaTransportControlsLock)
                    {
                        _suppressPlaybackActivationUntilStopped = false;
                    }
                }
                return;
            }

            lock (SystemMediaTransportControlsLock)
            {
                if (_suppressPlaybackActivationUntilStopped)
                {
                    return;
                }

                ActivateSystemMediaTransportControls(true);
            }
        }

        

        public static void AddToPlay(MusicPlayModel item)
        {
            playList.Add(item);
            _mediaPlaybackList.Items.Add(
                   new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(item.url))));
            if (UpdateList!=null)
            {
                UpdateList(null, playList);
            }

            ActivateSystemMediaTransportControls(true);
            _mediaPlayer.Play();

        }

        public static void SetPlayList(List<MusicPlayModel> list)
        {
           
            foreach (var item in list)
            {
                _mediaPlaybackList.Items.Add(
                    new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(item.url))));
            }

        }

        public static void ClearMediaList()
        {
            if (_mediaPlayer.PlaybackSession.CanPause)
            {
                _mediaPlayer.Pause();
            }
            _mediaPlaybackList.Items.Clear();
            playList.Clear();
            ReleaseSystemMediaTransportControls();

            if (DisplayEvent != null)
            {
                DisplayEvent(null, Visibility.Collapsed);
            }

        }


        private static void _mediaPlaybackList_CurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {

            //switch (musicPlayMode)
            //{
            //    case MusicPlayMode.listLoop:
            //        _mediaPlaybackList.ShuffleEnabled = false;
            //        _mediaPlaybackList.AutoRepeatEnabled = true;
            //        break;
            //    case MusicPlayMode.songLoop:

            //        _mediaPlaybackList.MoveTo(_mediaPlaybackList.CurrentItemIndex);
            //        break;
            //    case MusicPlayMode.random:
            //        _mediaPlaybackList.ShuffleEnabled = true;
            //        break;
            //    case MusicPlayMode.sequence:
            //        _mediaPlaybackList.ShuffleEnabled = false;
            //        _mediaPlaybackList.AutoRepeatEnabled = false;
            //        break;
            //    default:
            //        break;
            //}
            if (_mediaPlaybackList.Items.Count==0)
            {
                return;
            }
            ActivateSystemMediaTransportControls(false);
            if (MediaChanged!=null)
            {
                MediaChanged(sender,playList[Convert.ToInt32(_mediaPlaybackList.CurrentItemIndex)]);
            }
            if (DisplayEvent!=null)
            {
                DisplayEvent(null, Visibility.Visible);
            }
        }


        public static void Play()
        {
            if (_mediaPlaybackList == null || _mediaPlaybackList.Items.Count == 0)
            {
                return;
            }

            ActivateSystemMediaTransportControls(true);
            _mediaPlayer.Play();
        }

        public static void PauseAndReleaseSystemMediaTransportControls()
        {
            lock (SystemMediaTransportControlsLock)
            {
                _ownsSystemMediaTransportControls = false;
                _suppressPlaybackActivationUntilStopped =
                    _mediaPlayer != null && _mediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
            }
            Pause();
            ReleaseSystemMediaTransportControls();
        }

        public static void Pause()
        {
            try
            {
                if (_mediaPlaybackList.Items.Count!=0&&_mediaPlayer.PlaybackSession.CanPause)
                {
                    _mediaPlayer.Pause();
                    _mediaPlayer.SystemMediaTransportControls.DisplayUpdater.Update();
                }

            }
            catch (Exception)
            {
                //会出现各种莫名其妙的错误catch掉算了- -
                //throw;
            }

        }

        public static void ActivatePausedMusic()
        {
            ActivateSystemMediaTransportControls(true);
        }



        public async static Task<string> GetMusicUri(string id,int quality=1)
        {
            try
            {
                string url = string.Format("https://api.bilibili.com/audio/music-service-c/url?access_key={0}&appkey={1}&build=5370000&mid={2}&mobi_app=android&platform=android&privilege=2&quality={5}&songid={3}&ts={4}",
               ApiHelper.access_key, ApiHelper.AndroidKey.Appkey, ApiHelper.GetUserId(), id, ApiHelper.GetTimeSpan, quality);
                url += "&sign=" + ApiHelper.GetSign(url);

                string re = await WebClientClass.GetResults(new Uri(url));

                JObject obj = JObject.Parse(re);
                if (obj["code"].ToInt32() == 0)
                {

                    List<string> ls = JsonConvert.DeserializeObject<List<string>>(obj["data"]["cdns"].ToString());

                    return ls[0];
                    //player.SetMediaPlayer(MusicHelper._mediaPlayer);
                    //player.Source = new Uri(ls[0]);
                }
                else
                {
                  
                    return null;
                }


            }
            catch (Exception)
            {
               
                return null;
            }
        }


    }


    public class MusicPlayModel
    {
        public string songid { get; set; }
        public string title { get; set; }
        public string artist { get; set; }
        public string pic { get; set; }
        public string url { get; set; }
    }


}
