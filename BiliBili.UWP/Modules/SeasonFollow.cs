using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using BiliBili.UWP.Models;
using BiliBili.UWP.Api;
using BiliBili.UWP.Api.User;

namespace BiliBili.UWP.Modules
{
    public enum SeasonType
    {
        bangumi,
        cinema
    }
    public class SeasonFollow : IModules
    {
        /// <summary>
        /// status 1=想看,2=在看,3=看过
        /// </summary>
        public int Status { get; set; }

        private bool _HasNext = true;
        public bool HasNext
        {
            get { return _HasNext; }
            set { _HasNext = value; DoPropertyChanged("HasNext"); }
        }

        private bool _Loading = false;
        public bool Loading
        {
            get { return _Loading; }
            set { _Loading = value; DoPropertyChanged("Loading"); }
        }

        public int Page { get; set; } = 0;
        private int _total = 0;
        public int Total
        {
            get { return _total; }
            set { _total = value; DoPropertyChanged("Total"); }
        }
        public SeasonType SeasonType { get; set; }

        private ObservableCollection<FollowSeasonInfo> _FollowList = new ObservableCollection<FollowSeasonInfo>();
        public ObservableCollection<FollowSeasonInfo> FollowList
        {
            get { return _FollowList; }
            set { _FollowList = value; DoPropertyChanged("FollowList"); }
        }
        public SeasonFollow(SeasonType seasonType, int status)
        {
            SeasonType = seasonType;
            this.Status = status;
            //LoadData();
        }
        /// <summary>
        /// 加载数据
        /// </summary>
        public async void LoadData()
        {
            if (Loading || !HasNext)
            {
                return;
            }
            Loading = true;
            try
            {
                var added = 0;
                do
                {
                    var nextPage = Page + 1;
                    var api = SeasonType == SeasonType.bangumi
                        ? new FollowAPI().MyFollowBangumi(nextPage, Status, 30)
                        : new FollowAPI().MyFollowCinema(nextPage, Status, 30);
                    var response = await api.Request();
                    if (!response.status)
                    {
                        Utils.ShowMessageToast(response.message);
                        return;
                    }

                    var obj = response.GetJObject();
                    if (obj == null)
                    {
                        Utils.ShowMessageToast("追番信息解析失败");
                        return;
                    }
                    if (obj["code"].ToInt32() != 0)
                    {
                        Utils.ShowMessageToast(obj["message"]?.ToString() ?? "读取追番失败");
                        return;
                    }

                    var data = obj["data"]?.ToObject<FollowResult>();
                    if (data == null)
                    {
                        Utils.ShowMessageToast("追番信息为空");
                        return;
                    }

                    Page = nextPage;
                    Total = data.total;
                    HasNext = data.ps > 0 && Page * data.ps < data.total;
                    var items = data.list?.Where(x => x.follow_status == Status).ToList() ?? new List<FollowSeasonInfo>();
                    foreach (var item in items)
                    {
                        item._status = Status;
                        FollowList.Add(item);
                    }
                    added = items.Count;
                } while (added == 0 && HasNext);
            }
            catch (Exception ex)
            {
                Utils.ShowMessageToast(HandelError(ex).message);
            }
            finally
            {
                Loading = false;
            }
        }
        /// <summary>
        /// 取消收藏
        /// </summary>
        /// <param name="seasonId"></param>
        /// <param name="seasonType"></param>
        /// <returns></returns>
        public async Task<ReturnModel> CancelFollow(int seasonId,int seasonType)
        {
            try
            {
                var url = string.Format($"https://bangumi.bilibili.com/follow/api/season/unfollow?access_key={ ApiHelper.access_key}&appkey={ApiHelper.AndroidKey.Appkey}&build={ApiHelper.build}&platform=android&season_id={seasonId}&season_type={seasonType}&ts={ApiHelper.GetTimeSpan}");
                url += "&sign=" + ApiHelper.GetSign(url);
                string results = await WebClientClass.GetResults(new Uri(url));
                JObject json = JObject.Parse(results);
                if ((int)json["code"] == 0)
                {
                    return new ReturnModel()
                    {
                        success = true
                    };
                }
                else
                {
                    return new ReturnModel()
                    {
                        success = false,
                        message = json["message"].ToString()
                    };
                }
            }
            catch (Exception ex)
            {

                return HandelError(ex);
            }
            
        }

        /// <summary>
        /// 移动
        /// </summary>
        /// <param name="seasonId"></param>
        /// <param name="seasonType"></param>
        /// <returns></returns>
        public async Task<ReturnModel> MoveStatus(int seasonId, int status)
        {
            try
            {
                var url = "https://api.bilibili.com/pgc/app/follow/status/update";
                var body = $"access_key={ApiHelper.access_key}&appkey={ApiHelper.AndroidKey.Appkey}&build={ApiHelper.build}&platform=android&season_id={seasonId}&status={status}&ts={ApiHelper.GetTimeSpan}";
                body += "&sign=" + ApiHelper.GetSign(body);
                string results = await WebClientClass.PostResults(new Uri(url), body);
                JObject json = JObject.Parse(results);
                if ((int)json["code"] == 0)
                {
                    return new ReturnModel()
                    {
                        success = true
                    };
                }
                else
                {
                    return new ReturnModel()
                    {
                        success = false,
                        message = json["message"].ToString()
                    };
                }
            }
            catch (Exception ex)
            {

                return HandelError(ex);
            }

        }


    }
    public class FollowResult
    {
        public int pn { get; set; }
        public int ps { get; set; }
        public int total { get; set; }
        public List<FollowSeasonInfo> list { get; set; }
    }
    public class FollowSeasonInfo
    {
        public int _status { get; set; }
        public string badge { get; set; }

        public bool show_badge
        {
            get { return badge != ""; }
        }
        public int badge_type { get; set; }
        private string _cover;

        public string cover
        {
            get { return _cover + "@300w.jpg"; }
            set { _cover = value; }
        }

        public int is_finish { get; set; }
        public string title { get; set; }
        public int season_id { get; set; }
        public int season_type { get; set; }
        public int follow_status { get; set; }
        public string season_type_name { get; set; }
        public string url { get; set; }
        public FollowSeasonNewEp new_ep { get; set; }
        public JToken progress { get; set; }
        public string progress_text
        {
            get
            {
                if (progress == null || progress.Type == JTokenType.Null)
                {
                    return "尚未观看";
                }
                if (progress.Type == JTokenType.Object)
                {
                    return progress["index_show"]?.ToString() ?? "尚未观看";
                }
                return string.IsNullOrEmpty(progress.ToString()) ? "尚未观看" : progress.ToString();
            }
        }
        public List<FollowSeasonAreas> areas { get; set; }
        public bool show_areas
        {
            get { return areas != null && areas.Count != 0; }
        }
        public string areas_str
        {
            get
            {
                var str = season_type_name;
                if (areas != null && areas.Count != 0)
                {
                    str += " | ";
                    foreach (var item in areas)
                    {
                        str += item.name + "、";
                    }
                    str = str.TrimEnd('、');
                }
                return str;
            }
        }

    }

    public class FollowSeasonNewEp
    {
        public string cover { get; set; }
        public string index_show { get; set; }

        public int duration { get; set; }
        public int id { get; set; }
        public int is_new { get; set; }
    }
    public class FollowSeasonProgress
    {
        public string index_show { get; set; }
        public int last_ep_id { get; set; }
        public int last_time { get; set; }
    }
    public class FollowSeasonAreas
    {
        public int id { get; set; }
        public string name { get; set; }
    }
}
