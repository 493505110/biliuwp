using BiliBili.UWP.Modules;
using BiliBili.UWP.Modules.Home;
using BiliBili.UWP.Modules.Home.HotModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace BiliBili.UWP.Pages.Home
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class HotPage : Page
    {
        readonly HotVM hotVM;
        readonly ToView toView;
        public HotPage()
        {
            this.InitializeComponent();
            this.Loaded += HotPage_Loaded;
            hotVM = new HotVM();
            toView = new ToView();
        }

        private async void HotPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!hotVM.Loading && hotVM.Items == null)
            {
                await hotVM.GetPopular();
            }
        }

        private void List_hot_ItemClick(object sender, ItemClickEventArgs e)
        {
            var data = e.ClickedItem as HotDataItemModel;
            if (data.card_goto == "av")
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(VideoViewPage), data.param);
                return;
            }
            else
            {
                Utils.ShowMessageToast("不支持跳转的类型");
            }
        }

        private async void ls_Part_ItemClick(object sender, ItemClickEventArgs e)
        {
            var data = e.ClickedItem as HotTopItemModel;
            if (data.module_id == "rank")
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(RankPage));
                return;
            }
            //每周必看与热门子频道（美食等）原本指向 App 专用 h5，桌面 WebView2 里是白屏，这里都走原生
            if (data.module_id == "weekly-selected")
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(WeeklyPage));
                return;
            }
            if (data.module_id == "hot-channel" && data.entrance_id != 0)
            {
                var target = hotVM.EntranceId == data.entrance_id ? 0 : data.entrance_id;
                await hotVM.SwitchChannel(target);
                Utils.ShowMessageToast(target == 0 ? "已切回全部热门" : "已切换到" + data.title);
                return;
            }
            if (data.uri.Contains("https://") || data.uri.Contains("http://"))
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(WebPage), data.uri);
                return;
            }
            else
            {
                Utils.ShowMessageToast("不支持跳转的类型");
            }
        }
    }
}
