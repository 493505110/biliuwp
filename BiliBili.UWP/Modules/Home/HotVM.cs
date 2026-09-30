using BiliBili.UWP.Api;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Modules.Home.HotModels;
using Microsoft.Toolkit.Collections;
using Microsoft.Toolkit.Uwp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BiliBili.UWP.Modules.Home
{
    public class HotVM:IModules
    {
        readonly Api.Home.HotAPI hotAPI;
        public HotVM() : this(0)
        {
        }
        public HotVM(int entranceId)
        {
            hotAPI = new Api.Home.HotAPI();
            EntranceId = entranceId;
            RefreshCommand = new RelayCommand(Refresh);
            LoadMoreCommand = new RelayCommand(LoadMore);
        }

        /// <summary>
        /// 热门子频道 ID（如美食为 9），0 表示全部热门。
        /// </summary>
        public int EntranceId { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand LoadMoreCommand { get; private set; }

        private bool _loading = false;
        public bool Loading
        {
            get { return _loading; }
            set { _loading = value; DoPropertyChanged("Loading"); }
        }

        private List<HotTopItemModel> _topItems;

        public List<HotTopItemModel> TopItems
        {
            get { return _topItems; }
            set { _topItems = value; DoPropertyChanged("TopItems"); }

        }
        private IncrementalLoadingCollection<HotItemSource, HotDataItemModel> _items;
        public IncrementalLoadingCollection<HotItemSource, HotDataItemModel> Items
        {
            get { return _items; }
            set { _items = value; DoPropertyChanged("Items"); }
        }

        public async Task GetPopular()
        {
            try
            {
                Loading = true;
                var result = await hotAPI.Popular("0","", EntranceId).Request();
                if (result.status)
                {
                    var data = result.GetJObject();
                    if (data["code"].ToInt32() == 0)
                    {
                        if (TopItems == null)
                        {
                            TopItems = JsonConvert.DeserializeObject<List<HotTopItemModel>>(data["config"]["top_items"].ToString());
                        }

                        var items = JsonConvert.DeserializeObject<List<HotDataItemModel>>(data["data"].ToString());
                        for (int i = items.Count - 1; i >= 0; i--)
                        {
                            if (items[i].card_goto != "av")
                                items.Remove(items[i]);
                        }
                        Items = new IncrementalLoadingCollection<HotItemSource, HotDataItemModel>(new HotItemSource(items, EntranceId));
                    }
                    else
                    {
                        Utils.ShowMessageToast(data["message"].ToString());
                    }
                }
                else
                {
                    Utils.ShowMessageToast(result.message);
                }
            }
            catch (Exception ex)
            {

                var handle = HandleError(ex);
                Utils.ShowMessageToast(handle.message);
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
            TopItems = null;
            await GetPopular();
        }

        /// <summary>
        /// 切换热门子频道（如美食 entrance_id=9），传 0 回到全部热门。
        /// 顶部入口列表保持不变，方便来回切换。
        /// </summary>
        public async Task SwitchChannel(int entranceId)
        {
            if (Loading || EntranceId == entranceId)
            {
                return;
            }
            EntranceId = entranceId;
            Items = null;
            await GetPopular();
        }
        public async void LoadMore()
        {
            if (Loading)
            {
                return;
            }
            await Items.LoadMoreItemsAsync(20);
        }
    }

    public class HotItemSource : IIncrementalSource<HotDataItemModel>
    {
        readonly Api.Home.HotAPI hotAPI;
        readonly int entranceId;
        public HotItemSource(List<HotDataItemModel> items, int entranceId = 0)
        {
            hotAPI = new Api.Home.HotAPI();
            this.entranceId = entranceId;
            last_idx = items.LastOrDefault().idx ?? "0";
            last_param = items.LastOrDefault()?.param ?? "";
            this.hot_items = items;
        }
        string last_idx = "0";
        string last_param = "0";
        List<HotDataItemModel> hot_items;
        public async Task<List<HotDataItemModel>> GetHot()
        {
            try
            {

                var result = await hotAPI.Popular(last_idx, last_param, entranceId).Request();
                if (result.status)
                {
                    var data = result.GetJObject();
                    if (data["code"].ToInt32() == 0)
                    {
                        var items = JsonConvert.DeserializeObject<List<HotDataItemModel>>(data["data"].ToString());
                        for (int i = items.Count - 1; i >= 0; i--)
                        {
                            if (items[i].card_goto != "av")
                                items.Remove(items[i]);
                        }
                        last_idx = items.LastOrDefault()?.idx ?? "0";
                        last_param = items.LastOrDefault()?.param ?? "";
                        return items;
                    }
                    else
                    {
                        Utils.ShowMessageToast(data["message"].ToString());
                        return new List<HotDataItemModel>();
                    }
                }
                else
                {
                    Utils.ShowMessageToast(result.message);
                    return new List<HotDataItemModel>();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("加载首页推荐信息失败", LogType.ERROR, ex);
                Utils.ShowMessageToast("加载首页推荐信息失败");
                return new List<HotDataItemModel>();
            }

        }
      

        public async Task<IEnumerable<HotDataItemModel>> GetPagedItemsAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
        {
            if (pageIndex == 0)
            {
                return hot_items;
            }
            return await GetHot();
        }
    }



    namespace HotModels
    {
        public class HotTopItemModel
        {
            public int entrance_id { get; set; }
            public string icon { get; set; }
            public string module_id { get; set; }
            public string uri { get; set; }
            public string title { get; set; }
        }
        public class HotDataItemModel
        {
            public string card_type { get; set; }
            public string card_goto { get; set; }
            public string param { get; set; }
            public string cover { get; set; }
            public string title { get; set; }
            public string idx { get; set; }
            public string uri { get; set; }
            public string cover_right_text_1 { get; set; }
            public string right_desc_1 { get; set; }
            public string right_desc_2 { get; set; }
            public RecommendModels.RecommendRcmdReasonStyleModel rcmd_reason_style { get; set; }
        }
    }
}
