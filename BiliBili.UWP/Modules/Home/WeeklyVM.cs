using BiliBili.UWP.Api;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Modules.Home.WeeklyModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace BiliBili.UWP.Modules.Home
{
    /// <summary>
    /// 每周必看。原 App 入口是 h5 页面，在桌面 WebView2 里是白屏，这里用 web 原生接口重做。
    /// </summary>
    public class WeeklyVM : IModules
    {
        readonly Api.Home.WeeklyAPI weeklyAPI;

        public WeeklyVM()
        {
            weeklyAPI = new Api.Home.WeeklyAPI();
            Items = new ObservableCollection<WeeklyVideoModel>();
        }

        private bool _loading;
        public bool Loading
        {
            get { return _loading; }
            set { _loading = value; DoPropertyChanged("Loading"); }
        }

        private List<WeeklyPeriodModel> _periods;
        public List<WeeklyPeriodModel> Periods
        {
            get { return _periods; }
            set { _periods = value; DoPropertyChanged("Periods"); }
        }

        private WeeklyPeriodModel _selectedPeriod;
        public WeeklyPeriodModel SelectedPeriod
        {
            get { return _selectedPeriod; }
            set { _selectedPeriod = value; DoPropertyChanged("SelectedPeriod"); }
        }

        private string _subject;
        public string Subject
        {
            get { return _subject; }
            set { _subject = value; DoPropertyChanged("Subject"); }
        }

        public ObservableCollection<WeeklyVideoModel> Items { get; private set; }

        /// <summary>
        /// 拉期数列表并载入最新一期。已经载入过就不重复拉。
        /// </summary>
        public async Task LoadAsync()
        {
            if (Loading)
            {
                return;
            }
            try
            {
                Loading = true;
                if (Periods == null || Periods.Count == 0)
                {
                    var result = await weeklyAPI.SeriesList().Request();
                    if (!result.status)
                    {
                        Utils.ShowMessageToast(result.message);
                        return;
                    }
                    var data = result.GetJObject();
                    if (data == null || data["code"].ToInt32() != 0)
                    {
                        Utils.ShowMessageToast(data?["message"]?.ToString() ?? "读取每周必看失败");
                        return;
                    }
                    Periods = JsonConvert.DeserializeObject<List<WeeklyPeriodModel>>(
                        data["data"]?["list"]?.ToString() ?? "[]");
                }

                var period = SelectedPeriod ?? Periods.FirstOrDefault();
                if (period == null)
                {
                    Utils.ShowMessageToast("没有读取到每周必看的期数");
                    return;
                }
                SelectedPeriod = period;
                await LoadPeriodCore(period.number);
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

        /// <summary>
        /// 切换到指定期
        /// </summary>
        public async Task LoadPeriod(int number)
        {
            if (Loading)
            {
                return;
            }
            try
            {
                Loading = true;
                await LoadPeriodCore(number);
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

        private async Task LoadPeriodCore(int number)
        {
            var result = await weeklyAPI.SeriesOne(number).Request();
            if (!result.status)
            {
                Utils.ShowMessageToast(result.message);
                return;
            }
            var data = result.GetJObject();
            if (data == null || data["code"].ToInt32() != 0)
            {
                Utils.ShowMessageToast(data?["message"]?.ToString() ?? "读取每周必看失败");
                return;
            }

            Subject = data["data"]?["config"]?["subject"]?.ToString() ?? string.Empty;
            var videos = JsonConvert.DeserializeObject<List<WeeklyVideoModel>>(
                data["data"]?["list"]?.ToString() ?? "[]");
            Items.Clear();
            foreach (var item in videos)
            {
                Items.Add(item);
            }
        }
    }

    namespace WeeklyModels
    {
        public class WeeklyPeriodModel
        {
            public int number { get; set; }
            public string subject { get; set; }
            public string name { get; set; }
            public int status { get; set; }
        }

        public class WeeklyVideoModel
        {
            //新投稿的 aid/cid 已超出 int 范围，这里必须用 long
            public long aid { get; set; }
            public string bvid { get; set; }
            public string title { get; set; }
            public string pic { get; set; }
            public int duration { get; set; }
            public string rcmd_reason { get; set; }
            public WeeklyOwnerModel owner { get; set; }
            public WeeklyStatModel stat { get; set; }

            public string cover
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(pic))
                    {
                        return null;
                    }
                    var value = pic.Trim();
                    if (value.StartsWith("//", StringComparison.Ordinal))
                    {
                        return "https:" + value;
                    }
                    if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        return "https://" + value.Substring("http://".Length);
                    }
                    return value;
                }
            }

            public string durationText
            {
                get
                {
                    var span = TimeSpan.FromSeconds(duration);
                    return span.TotalHours >= 1
                        ? string.Format("{0:D2}:{1:D2}:{2:D2}", (int)span.TotalHours, span.Minutes, span.Seconds)
                        : string.Format("{0:D2}:{1:D2}", (int)span.TotalMinutes, span.Seconds);
                }
            }

            public string upName
            {
                get { return owner == null ? string.Empty : owner.name; }
            }

            public int viewCount
            {
                get { return stat == null ? 0 : stat.view; }
            }

            public bool hasReason
            {
                get { return !string.IsNullOrWhiteSpace(rcmd_reason); }
            }
        }

        public class WeeklyOwnerModel
        {
            public long mid { get; set; }
            public string name { get; set; }
            public string face { get; set; }
        }

        public class WeeklyStatModel
        {
            public int view { get; set; }
            public int danmaku { get; set; }
        }
    }
}
