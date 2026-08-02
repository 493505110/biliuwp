using BiliBili.UWP.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BiliBili.UWP.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace BiliBili.UWP.Pages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class LiveSearchPage : Page
    {
        private readonly SearchAPI _searchAPI = new SearchAPI();

        public LiveSearchPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }


        private void btn_back_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            txt_hea_0.Text = "正在直播";
            txt_hea_1.Text = "直播";
            _keyword = "";
            _page_room = 1;
            _page_user = 1;
            search.Visibility = Visibility.Visible;
            list_Feed.Items.Clear();
            gv_Room.Items.Clear();
        }
        int _page_room = 1;
        bool _loadRoom = false;
        bool _loadUser = false;
        int _page_user = 1;
        string _keyword = "";
        private async void Search()
        {
            try
            {
                search.Visibility = Visibility.Collapsed;
                _loadRoom = true;
                _loadUser = true;
                pr_Load.Visibility = Visibility.Visible;

                var data = await RequestLiveSearch(_searchAPI.WebSearchLive(_keyword));
                if (data != null)
                {
                    var liveUserTotal = data["pageinfo"]?["live_user"]?["total"]?.Value<int>() ?? 0;
                    if (liveUserTotal != 0)
                    {
                        txt_hea_1.Text = "主播(" + liveUserTotal + ")";
                        DeserializeItems(data["result"]?["live_user"]).ForEach(x => list_Feed.Items.Add(x));
                        _page_user++;
                    }
                    var liveRoomTotal = data["pageinfo"]?["live_room"]?["total"]?.Value<int>() ?? 0;
                    if (liveRoomTotal != 0)
                    {
                        txt_hea_0.Text = "正在直播(" + liveRoomTotal + ")";
                        DeserializeItems(data["result"]?["live_room"]).ForEach(x => gv_Room.Items.Add(x));
                        _page_room++;
                    }
                }

            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147012867)
                {
                    Utils.ShowMessageToast("检查你的网络连接！", 3000);
                }
                else
                {
                    Utils.ShowMessageToast("发生错误\r\n" + ex.Message, 3000);
                }
            }
            finally
            {
                _loadRoom = false;
                _loadUser = false;
                pr_Load.Visibility = Visibility.Collapsed;

            }
        }
        private async void AddUser()
        {
            try
            {
                _loadUser = true;
                pr_Load.Visibility = Visibility.Visible;

                var data = await RequestLiveSearch(_searchAPI.WebSearchLiveUser(_keyword, _page_user));
                if (data != null)
                {
                    List<LiveSearchModel> ls = DeserializeItems(data["result"]);


                    if (ls.Count != 0)
                    {
                        ls.ForEach(x => list_Feed.Items.Add(x));
                        _page_user++;
                    }
                    else
                    {
                        Utils.ShowMessageToast("加载完了...", 3000);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147012867)
                {
                    Utils.ShowMessageToast("检查你的网络连接！", 3000);
                }
                else
                {
                    Utils.ShowMessageToast("发生错误\r\n" + ex.Message, 3000);
                }
            }
            finally
            {
                _loadUser = false;
                pr_Load.Visibility = Visibility.Collapsed;

            }
        }

        private async void AddRoom()
        {
            try
            {
                _loadRoom = true;
                pr_Load.Visibility = Visibility.Visible;

                var data = await RequestLiveSearch(_searchAPI.WebSearchLiveRoom(_keyword, _page_room));
                if (data != null)
                {
                    List<LiveSearchModel> ls = DeserializeItems(data["result"]);


                    if (ls.Count != 0)
                    {
                        ls.ForEach(x => gv_Room.Items.Add(x));
                        _page_room++;
                    }
                    else
                    {
                        Utils.ShowMessageToast("加载完了...", 3000);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147012867)
                {
                    Utils.ShowMessageToast("检查你的网络连接！", 3000);
                }
                else
                {
                    Utils.ShowMessageToast("发生错误\r\n" + ex.Message, 3000);
                }
            }
            finally
            {
                _loadRoom = false;
                pr_Load.Visibility = Visibility.Collapsed;

            }
        }

        private void sv_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sv.VerticalOffset == sv.ScrollableHeight)
            {
                if (!_loadUser)
                {
                    AddUser();
                }
            }
        }

        private void list_Feed_ItemClick(object sender, ItemClickEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Play, typeof(LiveRoomPage), (e.ClickedItem as LiveSearchModel).roomid);
        }

        private void btn_LoadMore_Click(object sender, RoutedEventArgs e)
        {
            if (list_Feed.Items.Count == 0)
            {

                return;
            }
            if (!_loadUser)
            {
                AddUser();
            }
        }

        private void autoSug_Box_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (autoSug_Box.Text.Length < 2)
            {
                Utils.ShowMessageToast("关键字至少需要2个", 3000);
                return;
            }
            txt_hea_0.Text = "正在直播";
            txt_hea_1.Text = "直播";
            _keyword = autoSug_Box.Text;
            _page_room = 1;
            _page_user = 1;
            list_Feed.Items.Clear();
            gv_Room.Items.Clear();
            Search(); 
            search.Visibility = Visibility.Collapsed;
            //AddRoom();
            //AddUser();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (this.ActualWidth <= 500)
            {
                bor_Width2.Width = ActualWidth / 2 - 20;
            }
            else
            {
                int i = Convert.ToInt32(ActualWidth / 200);
                bor_Width2.Width = ActualWidth / i - 15;
            }

            int d = Convert.ToInt32(this.ActualWidth / 400);
            if (d > 3)
            {
                d = 3;
            }
            bor_Width.Width = this.ActualWidth / d - 22;
        }

        private void sv_room_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sv_room.VerticalOffset == sv_room.ScrollableHeight)
            {
                if (!_loadRoom)
                {
                    AddRoom();
                }
            }
        }

        private void btn_LoadMore_Room_Click(object sender, RoutedEventArgs e)
        {
            if (gv_Room.Items.Count == 0)
            {

                return;
            }

            if (!_loadRoom)
            {
                AddRoom();
            }
        }

        private void gv_Room_ItemClick(object sender, ItemClickEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Play, typeof(LiveRoomPage), (e.ClickedItem as LiveSearchModel).roomid);
        }

        private async System.Threading.Tasks.Task<JObject> RequestLiveSearch(ApiModel api)
        {
            var results = await api.Request();
            if (!results.status)
            {
                Utils.ShowMessageToast("搜索请求失败，代码：" + results.code, 3000);
                return null;
            }

            var response = await results.GetJson<ApiDataModel<JObject>>();
            if (response == null)
            {
                Utils.ShowMessageToast("搜索响应解析失败", 3000);
                return null;
            }
            if (!response.success)
            {
                Utils.ShowMessageToast(response.message, 3000);
                return null;
            }
            return response.data;
        }

        private static List<LiveSearchModel> DeserializeItems(JToken token)
        {
            if (token == null)
            {
                return new List<LiveSearchModel>();
            }
            return JsonConvert.DeserializeObject<List<LiveSearchModel>>(token.ToString()) ?? new List<LiveSearchModel>();
        }
    }
}
