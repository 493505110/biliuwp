using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Input;
using BiliBili.UWP.Api;
using Newtonsoft.Json.Linq;

namespace BiliBili.UWP.Modules.Live
{
    public class LiveWatchHistoryVM:IModules
    {
        private const int PageSize = 20;
        readonly Api.Live.LiveCenterAPI liveCenterAPI;
        private long _max;
        private long _viewAt;
        private string _business = string.Empty;
        private bool _firstPage = true;

        public LiveWatchHistoryVM()
        {
            liveCenterAPI = new Api.Live.LiveCenterAPI();
            Historys = new ObservableCollection<LiveWatchHistoryItemModel>();
            RefreshCommand = new RelayCommand(Refresh);
            LoadMoreCommand = new RelayCommand(LoadMore);
        }
        public ICommand RefreshCommand { get; private set; }
        public ICommand LoadMoreCommand { get; private set; }
        public ObservableCollection<LiveWatchHistoryItemModel> Historys { get; set; }

        private bool _loading = true;
        public bool Loading
        {
            get { return _loading; }
            set { _loading = value; DoPropertyChanged("Loading"); }
        }

        private bool _canLoadMore = true;
        public bool ShowLoadMore
        {
            get { return _canLoadMore; }
            set { _canLoadMore = value; DoPropertyChanged("ShowLoadMore"); }
        }

        public async Task GetHistorys()
        {
            try
            {
                Loading = true;
                ShowLoadMore = false;
                var result = await liveCenterAPI.History(_max, _viewAt, _business, PageSize).Request();
                if (result.status)
                {
                    var root = result.GetJObject();
                    if (root != null && root.Value<int?>("code") == 0)
                    {
                        var data = root["data"];
                        var list = data?["list"] as JArray;
                        var cursor = data?["cursor"];
                        var nextMax = cursor?.Value<long?>("max") ?? 0;
                        var nextViewAt = cursor?.Value<long?>("view_at") ?? 0;
                        var nextBusiness = cursor?.Value<string>("business") ?? string.Empty;
                        var cursorAdvanced = nextMax != _max || nextViewAt != _viewAt || nextBusiness != _business;

                        if (_firstPage)
                        {
                            Historys.Clear();
                        }

                        var itemCount = 0;
                        if (list != null)
                        {
                            foreach (var item in list)
                            {
                                var history = item["history"];
                                var roomId = history?.Value<long?>("oid") ?? 0;
                                var uri = item.Value<string>("uri");
                                if (string.IsNullOrWhiteSpace(uri) && roomId > 0)
                                {
                                    uri = "https://live.bilibili.com/" + roomId;
                                }

                                Historys.Add(new LiveWatchHistoryItemModel
                                {
                                    cover = item.Value<string>("cover") ?? string.Empty,
                                    title = item.Value<string>("title") ?? item.Value<string>("show_title") ?? string.Empty,
                                    tag_name = item.Value<string>("tag_name") ?? string.Empty,
                                    name = item.Value<string>("author_name") ?? item.Value<string>("name") ?? string.Empty,
                                    live_status = item.Value<int?>("live_status") ?? 0,
                                    view_at = item.Value<long?>("view_at") ?? 0,
                                    uri = uri ?? string.Empty
                                });
                                itemCount++;
                            }
                        }

                        _max = nextMax;
                        _viewAt = nextViewAt;
                        _business = nextBusiness;
                        _firstPage = false;
                        ShowLoadMore = itemCount == PageSize && cursorAdvanced;
                    }
                    else
                    {
                        Utils.ShowMessageToast(root?["message"]?.ToString() ?? "读取观看历史失败");
                    }
                }
                else
                {
                    Utils.ShowMessageToast(result.message);
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

        public async void Refresh()
        {
            if (Loading)
            {
                return;
            }
            _max = 0;
            _viewAt = 0;
            _business = string.Empty;
            _firstPage = true;
            await GetHistorys();
        }
        public async void LoadMore()
        {
            if (Loading)
            {
                return;
            }
            await GetHistorys();
        }
    }


    public class LiveWatchHistoryItemModel
    {
        public string cover { get; set; }
        public string title { get; set; }
        public string tag_name { get; set; }
        public string name { get; set; }
        public int live_status { get; set; }
        public long view_at { get; set; }
        public string uri { get; set; }
        public bool live_ing
        {
            get
            {
                return live_status == 1;
            }
        }
    }
}
