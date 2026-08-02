using BiliBili.UWP.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json;
using BiliBili.UWP.Pages;
using System.Text.RegularExpressions;
using BiliBili.UWP.Pages.Season;
using BiliBili.UWP.Api.User;
using BiliBili.UWP.Api;
using BiliBili.UWP.Api.Season;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Modules.Season;
using Newtonsoft.Json.Linq;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace BiliBili.UWP.Views
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class BangumiPage : Page
    {
        public BangumiPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
        }
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (SettingHelper.Get_RefreshButton() && SettingHelper.IsPc())
            {
                b_btn_Refresh.Visibility = Visibility.Visible;
            }
            else
            {
                b_btn_Refresh.Visibility = Visibility.Collapsed;

            }
            if (e.NavigationMode == NavigationMode.New )
            {
                await Task.Delay(200);
                
                if (ApiHelper.IsLogin())
                {
                    
                    myban.Visibility = Visibility.Visible;
                    LoadMy();
                }
                else
                {
                    myban.Visibility = Visibility.Collapsed;
                    b_btn_Refresh.Visibility = Visibility.Collapsed;
                }
                if (list_ban_jp.ItemsSource == null)
                {
                    LoadHome();
                }
                
            }
            // await Task.Delay(200);
          

        }
        private async void LoadMy()
        {
            try
            {
                pr_Load.Visibility = Visibility.Visible;
                var result =await new FollowAPI().MyFollowBangumi().Request();

                if (result.status)
                {
                    var data = result.GetJObject();
                    if (data != null && data["code"].ToInt32() == 0)
                    {
                        var list = data["data"]?["list"]?.ToObject<List<FollowSeasonModel>>() ?? new List<FollowSeasonModel>();
                        list_ban_mine.ItemsSource = list.Take(9).ToList();
                    }
                    else
                    {
                        Utils.ShowMessageToast(data?["message"]?.ToString() ?? "读取追番失败了");
                    }
                }
                else
                {
                    Utils.ShowMessageToast(result.message);
                }
                
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147012867 || ex.HResult == -2147012889)
                {
                    Utils.ShowMessageToast("无法连接服务器，请检查你的网络连接", 3000);
                }
                else
                {

                    Utils.ShowMessageToast("读取追番失败了", 3000);
                }
            }
            finally
            {
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }
        private bool _loadingHome;
        private async void LoadHome()
        {
            if (_loadingHome)
            {
                return;
            }

            try
            {
                _loadingHome = true;
                pr_Load.Visibility = Visibility.Visible;
                var seasonApi = new SeasonIndexAPI();
                var animeTask = LoadRecommend(seasonApi, IndexSeasonType.Anime);
                var guochuangTask = LoadRecommend(seasonApi, IndexSeasonType.Guochuang);

                await Task.WhenAll(animeTask, guochuangTask);
                list_ban_jp.ItemsSource = await animeTask;
                list_ban_cn.ItemsSource = await guochuangTask;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("读取番剧首页推荐信息失败", LogType.ERROR, ex);
                if (ex.HResult == -2147012867 || ex.HResult == -2147012889)
                {
                    Utils.ShowMessageToast("无法连接服务器，请检查你的网络连接", 3000);
                }
                else
                {
                    Utils.ShowMessageToast("读取推荐信息失败", 3000);
                }
            }
            finally
            {
                _loadingHome = false;
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<List<SeasonIndexResultItemModel>> LoadRecommend(SeasonIndexAPI seasonApi, IndexSeasonType seasonType)
        {
            var response = await seasonApi.Result(1, (int)seasonType, "&order=3&sort=0", 9).Request();
            if (!response.status)
            {
                throw new InvalidOperationException(response.message);
            }

            var data = response.GetJObject();
            if (data == null)
            {
                throw new InvalidOperationException("推荐信息解析失败");
            }
            if (data["code"].ToInt32() != 0)
            {
                throw new InvalidOperationException(data["message"]?.ToString() ?? "推荐信息加载失败");
            }

            var list = data["data"]?["list"];
            if (list == null)
            {
                throw new InvalidOperationException("推荐信息为空");
            }
            return list.ToObject<List<SeasonIndexResultItemModel>>() ?? new List<SeasonIndexResultItemModel>();
        }
        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ViewBox2_num.Width = ActualWidth / 3 - 21;
        }

        private void btn_MyBan_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(FollowSeasonPage), Modules.SeasonType.bangumi);
        }

        private void list_ban_mine_ItemClick(object sender, ItemClickEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(BanInfoPage), (e.ClickedItem as FollowSeasonModel).season_id.ToString());
        }

        private void list_ban_jp_ItemClick(object sender, ItemClickEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(BanInfoPage), (e.ClickedItem as SeasonIndexResultItemModel).season_id.ToString());
        }

        private async void list_ban_cn_foot_ItemClick(object sender, ItemClickEventArgs e)
        {
            //妈蛋，B站就一定要返回个链接么,就不能返回个类型加参数吗
            var link = (e.ClickedItem as BangumiHomeModel).link;
            if(!await MessageCenter.HandelUrl(link))
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(WebPage), link);
            }
        }

        private void btn_Timeline_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(TimelinePage));
        }

        private void btn_tag_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(SeasonIndexPage),new Modules.Season.SeasonIndexParameter() { 
                type= Modules.Season.IndexSeasonType.Anime
            });
        }

        private void btn_jp_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(JpBangumiPage));
        }

        private void btn_cn_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(CnBangumiPage));
        }

        private void b_btn_Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (ApiHelper.IsLogin())
            {
                myban.Visibility = Visibility.Visible;
                LoadMy();
            }
            else
            {
                myban.Visibility = Visibility.Collapsed;
            }
            LoadHome();
        }

        private void PullToRefreshBox_RefreshInvoked(DependencyObject sender, object args)
        {
            if (ApiHelper.IsLogin())
            {
                myban.Visibility = Visibility.Visible;
                LoadMy();
            }
            else
            {
                myban.Visibility = Visibility.Collapsed;
            }
            LoadHome();
        }
    }
}
