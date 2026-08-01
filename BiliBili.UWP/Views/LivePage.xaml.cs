using BiliBili.UWP.Models;
using BiliBili.UWP.Pages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
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

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace BiliBili.UWP.Views
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class LivePage : Page
    {
        public LivePage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        private void btn_Banner_Click(object sender, RoutedEventArgs e)
        {
            string ban = Regex.Match(((sender as HyperlinkButton).DataContext as HomeLiveModel).link, @"^bilibili://live/(.*?)").Groups[1].Value;
            if (ban.Length != 0)
            {
                MessageCenter.SendNavigateTo(NavigateMode.Play, typeof(LiveRoomPage), ban);

                return;
            }
            //string ban2 = Regex.Match(((sender as HyperlinkButton).DataContext as HomeLiveModel).link+"/", @"id=(.*?)/").Groups[1].Value;
            //if (ban2.Length != 0)
            //{
            //    MessageCenter.SendNavigateTo(NavigateMode.Play, typeof(LiveRoomPage), ban2.Replace("/",""));

            //    return;
            //}
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(WebPage), ((sender as HyperlinkButton).DataContext as HomeLiveModel).link);
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
            if (e.NavigationMode == NavigationMode.New && home_flipView.ItemsSource == null)
            {
                await Task.Delay(200);

                GetLiveInfo();
            }
        }
        public bool isLoaded = false;
        public void GetLiveInfo()
        {
            isLoaded = false;
            pr_Load.Visibility = Visibility.Collapsed;
            MessageCenter.SendNavigateTo(NavigateMode.Home, typeof(LiveV2Page));
        }

        private void gridview_Hot_ItemClick(object sender, ItemClickEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Play, typeof(LiveRoomPage), (e.ClickedItem as HomeLiveModel).room_id);
            //PlayEvent((e.ClickedItem as HomeLiveModel).room_id);
        }

        private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
        {

            var info = (sender as HyperlinkButton).DataContext as HomeLiveModel;
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(LivePartInfoPage), info.partition.id);
            //OpenEvent(7);
        }



        private void hot_LoadMore_Click(object sender, RoutedEventArgs e)
        {
            // OpenEvent(0);
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ViewBox2_num.Width = ActualWidth / 2 - 20;
            //double d = ((ViewBox2_num.Width + 12) / 1.15) * 2;
            //gridview_Hot.Height = d;
            //gridview_DJ.Height = d;
            //gridview_FY.Height = d;
            //gridview_HH.Height = d;
            //gridview_JJ.Height = d;
            //gridview_MZ.Height = d;
            ////gridview_SH.Height = d;
            //gridview_WL.Height = d;
            //gridview_YZ.Height = d;
            //gridview_SJ.Height = d;
            //gridview_CW.Height = d;
        }

        private void btn_LivePart_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(LivePartPage));
        }

        private async void btn_myfeed_Click(object sender, RoutedEventArgs e)
        {
            if (!ApiHelper.IsLogin())
            {
                if (!await Utils.ShowLoginDialog())
                {
                    Utils.ShowMessageToast("请先登录");
                    return;
                }
            }

            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(LiveFeedPage));

        }

        private async void btn_liveCenter_Click(object sender, RoutedEventArgs e)
        {
            if (!ApiHelper.IsLogin())
            {
                if (!await Utils.ShowLoginDialog())
                {
                    Utils.ShowMessageToast("请先登录");
                    return;
                }
            }
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(LiveCenterPage));

        }

        private void btn_search_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(LiveSearchPage));
        }

        private void btn_miniVideo_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(LiveVideoPage));
        }

        private void b_btn_Refresh_Click(object sender, RoutedEventArgs e)
        {
            GetLiveInfo();
        }

        private void PullToRefreshBox_RefreshInvoked(DependencyObject sender, object args)
        {
            GetLiveInfo();
        }
    }

}
