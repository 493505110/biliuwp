using BiliBili.UWP.Api;
using BiliBili.UWP.Api.Season;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Modules.Season;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace BiliBili.UWP.Pages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class JpBangumiPage : Page
    {
        public JpBangumiPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (SettingHelper.Get_RefreshButton() && SettingHelper.IsPc())
            {
                b_btn_Refresh.Visibility = Visibility.Visible;
            }
            else
            {
                b_btn_Refresh.Visibility = Visibility.Collapsed;
            }
            if (e.NavigationMode == NavigationMode.New && list_ban_jp.ItemsSource == null)
            {
                LoadHome();
            }
        }

        private bool _loading;
        private async void LoadHome()
        {
            if (_loading)
            {
                return;
            }

            try
            {
                _loading = true;
                pr_Load.Visibility = Visibility.Visible;
                var seasonApi = new SeasonIndexAPI();
                var serializingTask = LoadSeasonList(seasonApi, 0);
                var finishedTask = LoadSeasonList(seasonApi, 1);

                await Task.WhenAll(serializingTask, finishedTask);
                list_ban_jp.ItemsSource = await serializingTask;
                list_ban_new.ItemsSource = await finishedTask;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("读取番剧页面信息失败", LogType.ERROR, ex);
                if (ex.HResult == -2147012867 || ex.HResult == -2147012889)
                {
                    Utils.ShowMessageToast("无法连接服务器，请检查你的网络连接", 3000);
                }
                else
                {
                    Utils.ShowMessageToast("读取番剧信息失败", 3000);
                }
            }
            finally
            {
                _loading = false;
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<List<SeasonIndexResultItemModel>> LoadSeasonList(SeasonIndexAPI seasonApi, int isFinish)
        {
            var response = await seasonApi.Result(
                1,
                (int)IndexSeasonType.Anime,
                $"&order=3&sort=0&is_finish={isFinish}",
                9).Request();
            if (!response.status)
            {
                throw new InvalidOperationException(response.message);
            }

            var data = response.GetJObject();
            if (data == null)
            {
                throw new InvalidOperationException("番剧信息解析失败");
            }
            if (data["code"].ToInt32() != 0)
            {
                throw new InvalidOperationException(data["message"]?.ToString() ?? "番剧信息加载失败");
            }

            var list = data["data"]?["list"];
            if (list == null)
            {
                throw new InvalidOperationException("番剧信息为空");
            }
            return list.ToObject<List<SeasonIndexResultItemModel>>() ?? new List<SeasonIndexResultItemModel>();
        }

        private void list_ban_jp_ItemClick(object sender, ItemClickEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(BanInfoPage), (e.ClickedItem as SeasonIndexResultItemModel).season_id.ToString());
        }

        private void list_ban_new_ItemClick(object sender, ItemClickEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(BanInfoPage), (e.ClickedItem as SeasonIndexResultItemModel).season_id.ToString());
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ViewBox2_num.Width = ActualWidth / 3 - 21;
        }

        private void btn_NewBan_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(TimelinePage), 2);
        }

        private void btn_10Ban_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(Season.SeasonIndexPage), new Modules.Season.SeasonIndexParameter()
            {
                type = Modules.Season.IndexSeasonType.Anime
            });
        }

        private void b_btn_Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadHome();
        }

        private void PullToRefreshBox_RefreshInvoked(DependencyObject sender, object args)
        {
            LoadHome();
        }
    }
}
