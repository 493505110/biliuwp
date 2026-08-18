using BiliBili.UWP.Api;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BiliBili.UWP.Modules.User
{
    public class MyFollowVideoVM : IModules
    {
        readonly Api.User.FollowAPI followAPI;
        public MyFollowVideoVM()
        {
            followAPI = new Api.User.FollowAPI();
            RefreshCommand = new RelayCommand(Refresh);
            LoadMoreCommand = new RelayCommand(LoadMore);
        }
        private bool _loading = false;
        private int _loadingCount;
        public bool Loading
        {
            get { return _loading; }
            set
            {
                var oldLoading = _loading;
                if (value)
                {
                    _loadingCount++;
                }
                else if (_loadingCount > 0)
                {
                    _loadingCount--;
                }

                _loading = _loadingCount > 0;
                if (_loading != oldLoading)
                {
                    DoPropertyChanged("Loading");
                }
            }
        }
        private bool _Nothing = false;
        public bool Nothing
        {
            get { return _Nothing; }
            set { _Nothing = value; DoPropertyChanged("Nothing"); }
        }

        private bool _ShowLoadMore = false;
        public bool ShowLoadMore
        {
            get { return _ShowLoadMore; }
            set { _ShowLoadMore = value; DoPropertyChanged("ShowLoadMore"); }
        }

        public ICommand RefreshCommand { get; private set; }
        public ICommand LoadMoreCommand { get; private set; }

        private ObservableCollection<FavoriteItemModel> _myFavorite;
        public ObservableCollection<FavoriteItemModel> MyFavorite
        {
            get { return _myFavorite; }
            set { _myFavorite = value; DoPropertyChanged("MyFavorite"); }
        }

        private FavoriteItemModel _currentFavorite;
        public FavoriteItemModel CurrentFavorite
        {
            get { return _currentFavorite; }
            set
            {
                if (ReferenceEquals(_currentFavorite, value))
                {
                    return;
                }

                _currentFavorite = value;
                DoPropertyChanged("CurrentFavorite");
            }
        }


        private ObservableCollection<FavoriteItemModel> _collectFavorite;
        public ObservableCollection<FavoriteItemModel> CollectFavorite
        {
            get { return _collectFavorite; }
            set { _collectFavorite = value; DoPropertyChanged("CollectFavorite"); }
        }
        private FavoriteInfoModel _FavoriteInfo;
        public FavoriteInfoModel FavoriteInfo
        {
            get { return _FavoriteInfo; }
            set { _FavoriteInfo = value; DoPropertyChanged("FavoriteInfo"); }
        }
        private ObservableCollection<FavoriteInfoVideoItemModel> _videos;
        public ObservableCollection<FavoriteInfoVideoItemModel> Videos
        {
            get { return _videos; }
            set { _videos = value; DoPropertyChanged("Videos"); }
        }

        public async Task<bool> LoadFavorite(string preferredFid = null, string preferredTitle = null, int preferredIndex = 0)
        {
            try
            {
                Loading = true;

                var results = await followAPI.MyCreatedFavorite().Request();
                if (results == null)
                {
                    Utils.ShowMessageToast("获取收藏夹失败");
                    return false;
                }

                if (!results.status)
                {
                    Utils.ShowMessageToast(results.message);
                    return false;
                }

                var data = await results.GetJson<ApiDataModel<JObject>>();
                if (data == null)
                {
                    Utils.ShowMessageToast(results.message);
                    return false;
                }

                if (!data.success)
                {
                    Utils.ShowMessageToast(data.message);
                    return false;
                }

                if (data.data == null)
                {
                    Utils.ShowMessageToast("获取收藏夹失败");
                    return false;
                }

                var list = data.data["list"];
                if (list == null || list.Type == JTokenType.Null)
                {
                    Utils.ShowMessageToast("获取收藏夹失败");
                    return false;
                }

                var favoriteList = await list.ToString().DeserializeJson<ObservableCollection<FavoriteItemModel>>();
                if (favoriteList == null)
                {
                    Utils.ShowMessageToast("获取收藏夹失败");
                    return false;
                }
                MyFavorite = favoriteList;

                if (MyFavorite.Count == 0)
                {
                    FavoriteInfo = null;
                    Videos = null;
                    ShowLoadMore = false;
                    Nothing = true;
                    CurrentFavorite = null;
                    Page = 1;
                    return true;
                }

                FavoriteItemModel preferredItem = null;
                if (!string.IsNullOrEmpty(preferredFid))
                {
                    preferredItem = MyFavorite.FirstOrDefault(item => item.fid == preferredFid);
                }
                if (preferredItem == null && !string.IsNullOrEmpty(preferredTitle))
                {
                    preferredItem = MyFavorite.FirstOrDefault(item => item.title == preferredTitle);
                }

                var selectedIndex = preferredItem == null
                    ? Math.Max(0, Math.Min(preferredIndex, MyFavorite.Count - 1))
                    : MyFavorite.IndexOf(preferredItem);
                CurrentFavorite = MyFavorite[selectedIndex];
                Page = 1;
                FavoriteInfo = null;
                Videos = null;
                ShowLoadMore = false;
                Nothing = false;
                await LoadFavoriteVideos();
                return true;
            }
            catch (Exception ex)
            {
                var handel = HandelError(ex);
                Utils.ShowMessageToast(handel.message);
                return false;
            }
            finally
            {
                Loading = false;
            }
        }
        public async Task LoadFavoriteVideos()
        {
            if (CurrentFavorite == null)
            {
                Videos = null;
                FavoriteInfo = null;
                Nothing = true;
                ShowLoadMore = false;
                return;
            }

            try
            {
                ShowLoadMore = false;
                Loading = true;
                Nothing = false;
                var results = await followAPI.FavoriteInfo(CurrentFavorite.id, "", Page).Request();
                if (results == null)
                {
                    Utils.ShowMessageToast("获取收藏内容失败");
                    return;
                }

                if (results.status)
                {
                    var data = await results.GetJson<ApiDataModel<FavoriteDetailModel>>();
                    if (data == null)
                    {
                        Utils.ShowMessageToast(results.message);
                        return;
                    }

                    if (data.success)
                    {
                        if (data.data == null)
                        {
                            Utils.ShowMessageToast("获取收藏内容失败");
                            return;
                        }

                        var medias = data.data.medias ?? new ObservableCollection<FavoriteInfoVideoItemModel>();
                        ShowLoadMore = false;
                        Nothing = false;
                        if (Page == 1)
                        {
                            FavoriteInfo = data.data.info;
                            Videos = medias;
                        }
                        else
                        {
                            if (Videos == null)
                            {
                                Videos = new ObservableCollection<FavoriteInfoVideoItemModel>();
                            }
                            foreach (var item in medias)
                            {
                                Videos.Add(item);
                            }
                        }

                        if (Videos == null || Videos.Count == 0)
                        {
                            Nothing = true;
                            ShowLoadMore = false;
                            return;
                        }

                        if (FavoriteInfo != null && Videos.Count != FavoriteInfo.media_count)
                        {
                            ShowLoadMore = true;
                            Page++;
                        }
                    }
                    else
                    {
                        Utils.ShowMessageToast(data.message);
                    }
                }
                else
                {
                    Utils.ShowMessageToast(results.message);
                }
            }
            catch (Exception ex)
            {
                var handel = HandelError(ex);
                Utils.ShowMessageToast(handel.message);
            }
            finally
            {
                Loading = false;
            }
        }

        public async Task<bool> CreateFavoriteFolder(string title, bool privacy)
        {
            title = title?.Trim();
            if (string.IsNullOrEmpty(title) || title.Length > 20)
            {
                return false;
            }
            if (Loading)
            {
                return false;
            }

            try
            {
                Loading = true;
                var results = await followAPI.CreateFavorite(title, privacy).Request();
                if (results == null)
                {
                    Utils.ShowMessageToast("创建收藏夹失败");
                    return false;
                }
                if (!results.status)
                {
                    Utils.ShowMessageToast(results.message);
                    return false;
                }

                var data = await results.GetJson<ApiDataModel<JObject>>();
                if (data == null)
                {
                    Utils.ShowMessageToast(results.message);
                    return false;
                }
                if (data.success)
                {
                    var preferredFid = data.data?["fid"]?.ToString();
                    if (string.IsNullOrEmpty(preferredFid))
                    {
                        preferredFid = data.data?["id"]?.ToString();
                    }
                    await LoadFavorite(preferredFid, title);
                    return true;
                }

                Utils.ShowMessageToast(data.message);
                return false;
            }
            catch (Exception ex)
            {
                var handel = HandelError(ex);
                Utils.ShowMessageToast(handel.message);
                return false;
            }
            finally
            {
                Loading = false;
            }
        }

        public async Task<bool> EditFavoriteFolder(string title, bool privacy)
        {
            title = title?.Trim();
            if (string.IsNullOrEmpty(title) || title.Length > 20)
            {
                return false;
            }
            if (Loading)
            {
                return false;
            }
            if (CurrentFavorite == null)
            {
                return false;
            }

            try
            {
                Loading = true;
                var results = await followAPI.EditFavorite(CurrentFavorite.fid, title, privacy).Request();
                if (results == null)
                {
                    Utils.ShowMessageToast("编辑收藏夹失败");
                    return false;
                }
                if (!results.status)
                {
                    Utils.ShowMessageToast(results.message);
                    return false;
                }

                var data = await results.GetJson<ApiDataModel<JObject>>();
                if (data == null)
                {
                    Utils.ShowMessageToast(results.message);
                    return false;
                }
                if (data.success)
                {
                    await LoadFavorite(CurrentFavorite.fid, title);
                    return true;
                }

                Utils.ShowMessageToast(data.message);
                return false;
            }
            catch (Exception ex)
            {
                var handel = HandelError(ex);
                Utils.ShowMessageToast(handel.message);
                return false;
            }
            finally
            {
                Loading = false;
            }
        }

        public async Task<bool> DeleteCurrentFavoriteFolder()
        {
            if (Loading)
            {
                return false;
            }
            if (CurrentFavorite == null || MyFavorite == null || MyFavorite.Count == 0)
            {
                return false;
            }

            var current = CurrentFavorite;
            var preferredIndex = Math.Max(0, MyFavorite.IndexOf(current));
            try
            {
                Loading = true;
                var results = await followAPI.DeleteFavorite(current.id).Request();
                if (results == null)
                {
                    Utils.ShowMessageToast("删除收藏夹失败");
                    return false;
                }
                if (!results.status)
                {
                    Utils.ShowMessageToast(results.message);
                    return false;
                }

                var data = await results.GetJson<ApiDataModel<object>>();
                if (data == null)
                {
                    Utils.ShowMessageToast(results.message);
                    return false;
                }
                if (!data.success)
                {
                    Utils.ShowMessageToast(data.message);
                    return false;
                }
                if (data.success)
                {
                    var loaded = await LoadFavorite(null, null, preferredIndex);
                    if (!loaded)
                    {
                        MyFavorite.Remove(current);
                        if (MyFavorite.Count == 0)
                        {
                            FavoriteInfo = null;
                            Videos = null;
                            ShowLoadMore = false;
                            Nothing = true;
                            CurrentFavorite = null;
                        }
                        else
                        {
                            var fallbackIndex = Math.Max(0, Math.Min(preferredIndex, MyFavorite.Count - 1));
                            CurrentFavorite = MyFavorite[fallbackIndex];
                            Page = 1;
                            FavoriteInfo = null;
                            Videos = null;
                            await LoadFavoriteVideos();
                        }
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                var handel = HandelError(ex);
                Utils.ShowMessageToast(handel.message);
                return false;
            }
            finally
            {
                Loading = false;
            }
        }

        public async Task<bool> RemoveFavoriteVideo( FavoriteInfoVideoItemModel item)
        {
            try
            {

                var results = await followAPI.RemoveFavorite(CurrentFavorite.id, item.id).Request();
                if (results.status)
                {
                    var data = await results.GetJson<ApiDataModel<object>>();
                    if (data.success)
                    {
                        Videos.Remove(item);
                        return true;
                    }
                    else
                    {
                        Utils.ShowMessageToast(data.message);
                    }
                }
                else
                {
                    Utils.ShowMessageToast(results.message);
                }
            }
            catch (Exception ex)
            {
                var handel = HandelError(ex);
                Utils.ShowMessageToast(handel.message);
                
            }
            return false;
        }

        public int Page { get; set; } = 1;
        public async void Refresh()
        {
            if (Loading)
            {
                return;
            }
            Page = 1;
            FavoriteInfo = null;
            Videos = null;
            await LoadFavoriteVideos();
        }
        public async void LoadMore()
        {
            if (Loading)
            {
                return;
            }
            if (Videos == null || Videos.Count == 0)
            {
                return;
            }
            await LoadFavoriteVideos();
        }
    }

    public class FavoriteItemModel : INotifyPropertyChanged
    {
        public string cover { get; set; }
        public int attr { get; set; }
        public bool privacy
        {
            get
            {
                return attr == 2;
            }
        }

        public string fid { get; set; }
        public string id { get; set; }
        public int like_state { get; set; }

        public string mid { get; set; }
        public string title { get; set; }
        public int type { get; set; }


        private int _media_count;
        public int media_count
        {
            get { return _media_count; }
            set { _media_count = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("media_count")); }
        }
        public int fav_state { get; set; }
        public bool is_fav
        {
            get
            {
                return fav_state == 1;
            }
            set
            {
                if (value)
                {
                    fav_state = 1;
                }
                else
                {
                    fav_state = 0;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("is_fav"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("fav_state"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
    public class FavoriteInfoVideoItemModel
    {
        public string id { get; set; }
        public string cover { get; set; }
        public string title { get; set; }
        public FavoriteInfoVideoItemUpperModel upper { get; set; }
        public FavoriteInfoVideoItemStatModel cnt_info { get; set; }
    }
    public class FavoriteDetailModel
    {
        public FavoriteInfoModel info { get; set; }
        public ObservableCollection<FavoriteInfoVideoItemModel> medias { get; set; }
    }
    public class FavoriteInfoModel
    {
        public string cover { get; set; }
        public int attr { get; set; }
        public bool privacy
        {
            get
            {
                return attr == 2;
            }
        }
        public string fid { get; set; }
        public string id { get; set; }
        public int like_state { get; set; }
        public string mid { get; set; }
        public string title { get; set; }
        public int type { get; set; }
        public int media_count { get; set; }
        public FavoriteInfoVideoItemUpperModel upper { get; set; }
    }
    public class FavoriteInfoVideoItemUpperModel
    {
        public string face { get; set; }
        public string name { get; set; }
        public string mid { get; set; }
    }
    public class FavoriteInfoVideoItemStatModel
    {
        public int coin { get; set; }
        public int collect { get; set; }
        public int danmaku { get; set; }
        public int play { get; set; }
        public int reply { get; set; }
        public int share { get; set; }
    }
}
