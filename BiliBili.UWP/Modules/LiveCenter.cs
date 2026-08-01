using BiliBili.UWP.Helper;
using BiliBili.UWP.Api;
using BiliBili.UWP.Api.Live;
using BiliBili.UWP.Models;
using BiliBili.UWP.Modules.LiveCenterModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace BiliBili.UWP.Modules
{
    public class LiveCenter : IModules
    {
        /// <summary>
        /// 读取个人信息
        /// </summary>
        /// <returns></returns>
        public async Task<ReturnModel<LiveUserInfoModel>> GetUserInfo()
        {
            try
            {
                var response = await LiveRoomAPI.GetLiveUserInfo().Request();
                var root = response.GetJObject();
                if (response.status && root != null && root.Value<int?>("code") == 0)
                {
                    if (Account.myInfo == null)
                    {
                        await new Account().GetMyInfo();
                    }
                    var data = root["data"];
                    var m = data?.ToObject<LiveUserInfoModel>() ?? new LiveUserInfoModel();
                    m.wearTitle = data?["wear_title"]?.ToObject<UserInfoWearTitleModel>();
                    m.uname = Account.myInfo?.name ?? data?.Value<string>("uname") ?? string.Empty;
                    m.face = Account.myInfo?.face ?? data?.Value<string>("face") ?? string.Empty;
                    return new ReturnModel<LiveUserInfoModel>()
                    {
                        success = true,
                        data = m
                    };
                }
                else
                {
                    return new ReturnModel<LiveUserInfoModel>()
                    {
                        success = false,
                        message = root?["message"]?.ToString() ?? response.message
                    };
                }

            }
            catch (Exception ex)
            {

                return HandelError<LiveUserInfoModel>(ex);
            }
        }

        /// <summary>
        /// 读取关注的正在直播列表
        /// </summary>
        /// <returns></returns>
        public async Task<ReturnModel<ObservableCollection<LivingModel>>> GetLiveList()
        {
            try
            {
                var response = await LiveRoomAPI.GetFollowingLive().Request();
                var root = response.GetJObject();
                if (response.status && root != null && root.Value<int?>("code") == 0)
                {
                    var m = new ObservableCollection<LivingModel>();
                    var rooms = root["data"]?["rooms"] as Newtonsoft.Json.Linq.JArray;
                    if (rooms != null)
                    {
                        foreach (var item in rooms)
                        {
                            m.Add(new LivingModel
                            {
                                roomid = item.Value<int?>("room_id") ?? item.Value<int?>("roomid") ?? 0,
                                uid = item.Value<long?>("uid") ?? 0,
                                uname = item.Value<string>("uname") ?? item.Value<string>("nickname") ?? string.Empty,
                                face = item.Value<string>("face") ?? string.Empty,
                                title = item.Value<string>("title") ?? string.Empty,
                                live_tag_name = item.Value<string>("tag_name") ?? string.Empty,
                                online = item.Value<int?>("online") ?? 0,
                                area_name = item.Value<string>("area_name") ?? string.Empty,
                                area_v2_id = item.Value<int?>("area_v2_id") ?? 0,
                                area_v2_name = item.Value<string>("area_v2_name") ?? string.Empty,
                                area_v2_parent_id = item.Value<int?>("area_v2_parent_id") ?? 0,
                                area_v2_parent_name = item.Value<string>("area_v2_parent_name") ?? string.Empty,
                                broadcast_type = item.Value<int?>("broadcast_type") ?? 0,
                                link = item.Value<string>("link") ?? string.Empty,
                                cover = item.Value<string>("cover_from_user") ?? item.Value<string>("keyframe") ?? string.Empty
                            });
                        }
                    }
                    return new ReturnModel<ObservableCollection<LivingModel>>()
                    {
                        success = true,
                        message="",
                        data = m
                    };
                }
                else
                {
                    return new ReturnModel<ObservableCollection<LivingModel>>()
                    {
                        success = false,
                        message = root?["message"]?.ToString() ?? response.message
                    };
                }

            }
            catch (Exception ex)
            {

                return HandelError<ObservableCollection<LivingModel>>(ex);
            }
        }

        /// <summary>
        /// 读取关注的未在直播列表
        /// </summary>
        /// <returns></returns>
        public async Task<ReturnModel<ObservableCollection<NotLivingModel>>> GetUnLiveList(int page)
        {
            try
            {
                var response = await LiveRoomAPI.GetFollowing(page, 10).Request();
                var root = response.GetJObject();
                if (response.status && root != null && root.Value<int?>("code") == 0)
                {
                    var m = new ObservableCollection<NotLivingModel>();
                    var list = root["data"]?["list"] as Newtonsoft.Json.Linq.JArray;
                    if (list != null)
                    {
                        foreach (var item in list.Where(x => (x.Value<int?>("live_status") ?? 0) != 1))
                        {
                            m.Add(new NotLivingModel
                            {
                                live_desc = item.Value<string>("text_small") ?? "最近",
                                roomid = item.Value<int?>("roomid") ?? 0,
                                uid = item.Value<long?>("uid") ?? 0,
                                uname = item.Value<string>("uname") ?? string.Empty,
                                face = item.Value<string>("face") ?? string.Empty,
                                live_status = item.Value<int?>("live_status") ?? 0,
                                broadcast_type = item.Value<int?>("broadcast_type") ?? 0,
                                area_name = item.Value<string>("area_name") ?? string.Empty,
                                area_v2_id = item.Value<int?>("area_id") ?? 0,
                                area_v2_name = item.Value<string>("area_name_v2") ?? string.Empty,
                                area_v2_parent_id = item.Value<int?>("parent_area_id") ?? 0,
                                link = "https://live.bilibili.com/" + (item.Value<int?>("roomid") ?? 0)
                            });
                        }
                    }
                    return new ReturnModel<ObservableCollection<NotLivingModel>>()
                    {
                        success = true,
                        message = "",
                        data = m
                    };
                }
                else
                {
                    return new ReturnModel<ObservableCollection<NotLivingModel>>()
                    {
                        success = false,
                        message = root?["message"]?.ToString() ?? response.message
                    };
                }

            }
            catch (Exception ex)
            {

                return HandelError<ObservableCollection<NotLivingModel>>(ex);
            }
        }

    }
    namespace LiveCenterModels
    {
        public class UserInfoMedalModel
        {
            /// <summary>
            /// 暴漫
            /// </summary>
            public string medal_name { get; set; }
            /// <summary>
            /// Level
            /// </summary>
            public int level { get; set; }
            /// <summary>
            /// Color
            /// </summary>
            public string color { get; set; }
            /// <summary>
            /// Medal_color
            /// </summary>
            public string medal_color { get; set; }

            public SolidColorBrush m_color { get { return new SolidColorBrush(Utils.ToColor(color)); } }
        }

        public class UserInfoWearTitleModel
        {
            /// <summary>
            /// title-147-1
            /// </summary>
            public string title { get; set; }

            public string title_img
            {
                get
                {
                    return Modules.LiveRoom.titleItems?.FirstOrDefault(x => x.identification == title)?.web_pic_url;
                }
            }

            /// <summary>
            /// 英雄联盟LPL2018助威
            /// </summary>
            public string activity { get; set; }
        }

        public class LiveUserInfoModel
        {
            public string uname { get; set; }
            public string face { get; set; }
            /// <summary>
            /// Silver
            /// </summary>
            public int silver { get; set; }
            /// <summary>
            /// Gold
            /// </summary>
            public int gold { get; set; }
            /// <summary>
            /// Medal
            /// </summary>
            public UserInfoMedalModel medal { get; set; }
            /// <summary>
            /// Vip
            /// </summary>
            public int vip { get; set; }
            /// <summary>
            /// Svip
            /// </summary>
            public int svip { get; set; }

            public string svip_time { get; set; }
            public string vip_time { get; set; }
            /// <summary>
            /// WearTitle
            /// </summary>
            public UserInfoWearTitleModel wearTitle { get; set; }
            /// <summary>
            /// IsSign
            /// </summary>
            public int isSign { get; set; }
            /// <summary>
            /// User_level
            /// </summary>
            public int user_level { get; set; }
            /// <summary>
            /// User_level_color
            /// </summary>
            public string user_level_color { get; set; }
            /// <summary>
            /// User_level_color
            /// </summary>
            public SolidColorBrush level_color { get { return new SolidColorBrush(Utils.ToColor(user_level_color)); } }
            /// <summary>
            /// Room_id
            /// </summary>
            public int room_id { get; set; }
            /// <summary>
            /// Use_count
            /// </summary>
            public int use_count { get; set; }
            /// <summary>
            /// Vip_view_status
            /// </summary>
            public int vip_view_status { get; set; }


            public string ExTime
            {
                get
                {
                    if (svip != 0)
                    {
                        return "年费老爷 到期时间" + svip_time;
                    }
                    if (vip != 0)
                    {
                        return "月费老爷 到期时间" + vip_time;
                    }
                    return "";
                }
            }
        }


        public class LivingModel
        {
            /// <summary>
            /// Roomid
            /// </summary>
            public int roomid { get; set; }
            /// <summary>
            /// Uid
            /// </summary>
            public long uid { get; set; }
            /// <summary>
            /// bilibili英雄联盟赛事
            /// </summary>
            public string uname { get; set; }
            /// <summary>
            /// https://i0.hdslb.com/bfs/face/f07c74fe2a020b33ab1035fea6d3338b6a6e6749.jpg
            /// </summary>
            private string _face;
            public string face { get { return _face + "@200w.jpg"; } set { _face = value; } }
            /// <summary>
            /// LPL夏季赛 SNG vs SS / IG vs OMG
            /// </summary>
            public string title { get; set; }
            /// <summary>
            /// 英雄联盟
            /// </summary>
            public string live_tag_name { get; set; }
            /// <summary>
            /// Online
            /// </summary>
            public int online { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string playurl { get; set; }
            /// <summary>
            /// Accept_quality
            /// </summary>
            public List<string> accept_quality { get; set; }
            /// <summary>
            /// Current_quality
            /// </summary>
            public int current_quality { get; set; }
            /// <summary>
            /// Pk_id
            /// </summary>
            public int pk_id { get; set; }
            /// <summary>
            /// Special_attention
            /// </summary>
            public int special_attention { get; set; }
            /// <summary>
            /// Area
            /// </summary>
            public int area { get; set; }
            /// <summary>
            /// 电子竞技
            /// </summary>
            public string area_name { get; set; }
            /// <summary>
            /// Area_v2_id
            /// </summary>
            public int area_v2_id { get; set; }
            /// <summary>
            /// 英雄联盟
            /// </summary>
            public string area_v2_name { get; set; }
            /// <summary>
            /// Broadcast_type
            /// </summary>
            public int broadcast_type { get; set; }
            /// <summary>
            /// 游戏
            /// </summary>
            public string area_v2_parent_name { get; set; }
            /// <summary>
            /// Official_verify
            /// </summary>
            public int official_verify { get; set; }
            /// <summary>
            /// Area_v2_parent_id
            /// </summary>
            public int area_v2_parent_id { get; set; }
            /// <summary>
            /// https://live.bilibili.com/7734200?broadcast_type=0
            /// </summary>
            public string link { get; set; }
            /// <summary>
            /// https://i0.hdslb.com/bfs/live/ca7b6f478308095faf26edaad72288dacb0e30cf.jpg
            /// </summary>
            public string cover { get; set; }

            public string Online
            {
                get { return online.ToW(); }
            }
        }
        public class NotLivingModel
        {
            /// <summary>
            /// 2小时前
            /// </summary>
            public string live_desc { get; set; }
            public string desc
            {
                get
                {
                    return live_desc + "直播了" + area_v2_name;
                }
            }
            /// <summary>
            /// Roomid
            /// </summary>
            public int roomid { get; set; }
            /// <summary>
            /// Uid
            /// </summary>
            public long uid { get; set; }
            /// <summary>
            /// 宫本狗雨
            /// </summary>
            public string uname { get; set; }
            /// <summary>
            /// https://i0.hdslb.com/bfs/face/8c49a758216f9bd14b0046afe48a3514f44126f0.jpg
            /// </summary>
            private string _face;
            public string face { get { return _face + "@200w.jpg"; } set { _face = value; } }
          
            /// <summary>
            /// Special_attention
            /// </summary>
            public int special_attention { get; set; }
            /// <summary>
            /// Official_verify
            /// </summary>
            public int official_verify { get; set; }
            /// <summary>
            /// Live_status
            /// </summary>
            public int live_status { get; set; }
            /// <summary>
            /// Broadcast_type
            /// </summary>
            public int broadcast_type { get; set; }
            /// <summary>
            /// Pk_id
            /// </summary>
            public int pk_id { get; set; }
            /// <summary>
            /// Area
            /// </summary>
            public int area { get; set; }
            /// <summary>
            /// Attentions
            /// </summary>
            public int attentions { get; set; }
            /// <summary>
            /// 电子竞技
            /// </summary>
            public string area_name { get; set; }
            /// <summary>
            /// Area_v2_id
            /// </summary>
            public int area_v2_id { get; set; }
            /// <summary>
            /// 英雄联盟
            /// </summary>
            public string area_v2_name { get; set; }
            /// <summary>
            /// 游戏
            /// </summary>
            public string area_v2_parent_name { get; set; }
            /// <summary>
            /// Area_v2_parent_id
            /// </summary>
            public int area_v2_parent_id { get; set; }
            /// <summary>
            /// https://live.bilibili.com/5279?broadcast_type=0
            /// </summary>
            public string link { get; set; }
            public string FansNum
            {
                get { return attentions.ToW(); }
            }

            public Visibility rounding
            {
                get
                {
                    if (live_status==2)
                    {
                        return Visibility.Visible;
                    }
                    else
                    {
                        return Visibility.Collapsed;
                    }
                }
            }
           
        }

    }
}
