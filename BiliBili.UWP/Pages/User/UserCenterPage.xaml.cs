using BiliBili.UWP.Views;
using BiliBili.UWP.Models;
using BiliBili.UWP.Api;
using BiliBili.UWP.Pages.FindMore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace BiliBili.UWP.Pages.User
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class UserCenterPage : Page
    {
        private Modules.UserCenterVM userCenterVM;
        readonly Modules.Account account;
        string mid = "";
        public UserCenterPage()
        {
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Enabled;
            account = new Modules.Account();
        }
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode == NavigationMode.New|| e.NavigationMode == NavigationMode.Back)
            {
                if (e.Parameter == null)
                {
                    mid = ApiHelper.GetUserId();
                }
                else if (e.Parameter is object[])
                {
                    mid = (e.Parameter as object[])[0].ToString();
                }
                else
                {
                    mid = e.Parameter.ToString();
                }
                if (userCenterVM == null)
                {
                    userCenterVM = new Modules.UserCenterVM(mid);
                    await userCenterVM.GetUserDetail();
                }
                else if (userCenterVM.mid != mid)
                {
                    userCenterVM.mid = mid;
                    userCenterVM.is_self = mid == ApiHelper.GetUserId();
                    userCenterVM.UserCenterDetail = null;
                    userCenterVM.SubmitVideos.Clear();
                    //切换用户时同步重置动态状态
                    _dynItems.Clear();
                    _dynOffset = "";
                    _dynHasMore = true;
                    ls_new_dynamic.ItemsSource = null;
                    tb_dynEmpty.Visibility = Visibility.Collapsed;
                    await userCenterVM.GetUserDetail();
                }
            }
        }
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back)
            {
                NavigationCacheMode = NavigationCacheMode.Disabled;
            }
            base.OnNavigatingFrom(e);
        }
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }

        private void btn_verify_Click(object sender, RoutedEventArgs e)
        {
            Utils.ShowMessageToast(userCenterVM.UserCenterDetail.card.official_verify.desc);
        }

        private async void btnAddFollow_Click(object sender, RoutedEventArgs e)
        {
            var result = await account.Follow(mid);
            if (result.success)
            {
                userCenterVM.UserCenterDetail.relation = 1;
            }
            else
            {
                Utils.ShowMessageToast(result.message);
            }
        }

        private async void btnCancelFollow_Click(object sender, RoutedEventArgs e)
        {
            var result = await account.UnFollow(mid);
            if (result.success)
            {
                userCenterVM.UserCenterDetail.relation = -999;
            }
            else
            {
                Utils.ShowMessageToast(result.message);
            }

        }

        private void btnEditProfile_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(EditProfilePage));
        }

        private void SubmitVideo_ItemClick(object sender, ItemClickEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(VideoViewPage), (e.ClickedItem as Modules.BiliBili.UWP.Modules.UserSubmitVideoModels.SubmitVideoItemModel).aid);
        }

        private void btnOpenLiveRoom_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Play, typeof(Live.LiveRoomPC), userCenterVM.UserCenterDetail.live.roomid);
        }

        private async void btnRefreshSubmitVideos_Click(object sender, RoutedEventArgs e)
        {
            await userCenterVM.SubmitVideos.RefreshAsync();
        }

        private void pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pivot.SelectedIndex == 1)
            {
                if (_dynItems.Count == 0 && !_loadDynamic)
                {
                    GetDynamic();
                }
            }
        }

        #region 动态

        readonly Api.User.DynamicAPI _dynamicAPI = new Api.User.DynamicAPI();
        readonly ObservableCollection<SpaceDynItemVM> _dynItems = new ObservableCollection<SpaceDynItemVM>();
        bool _loadDynamic = false;
        string _dynOffset = "";
        bool _dynHasMore = true;

        private async void GetDynamic()
        {
            if (_loadDynamic) return;
            try
            {
                pr_Load.Visibility = Visibility.Visible;
                _loadDynamic = true;

                var result = await _dynamicAPI.SpaceDynamic(mid, _dynOffset).Request();
                if (!result.status)
                {
                    Utils.ShowMessageToast("读取动态失败：" + result.message);
                    return;
                }
                var resp = await result.GetData<SpaceDynamicResp>();
                if (resp == null || !resp.success)
                {
                    Utils.ShowMessageToast(resp?.message ?? "读取动态失败");
                    return;
                }

                if (ls_new_dynamic.ItemsSource == null)
                {
                    ls_new_dynamic.ItemsSource = _dynItems;
                }

                var items = resp.data?.items;
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (!item.visible) continue;
                        _dynItems.Add(SpaceDynItemVM.FromItem(item));
                    }
                }

                _dynHasMore = resp.data?.has_more ?? false;
                _dynOffset = resp.data?.offset ?? "";
                tb_dynEmpty.Visibility = _dynItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Helper.LogHelper.WriteLog("读取用户动态失败", Helper.LogType.ERROR, ex);
                Utils.ShowMessageToast("读取动态失败");
            }
            finally
            {
                _loadDynamic = false;
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }

        private void sv_dynamic_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var sv = sender as ScrollViewer;
            if (sv == null) return;
            if (sv.VerticalOffset >= sv.ScrollableHeight - 200 && _dynHasMore && !_loadDynamic)
            {
                GetDynamic();
            }
        }

        private void ls_new_dynamic_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as SpaceDynItemVM;
            if (item == null) return;
            switch (item.DynType)
            {
                case "DYNAMIC_TYPE_AV":
                case "DYNAMIC_TYPE_UGC_SEASON":
                    if (!string.IsNullOrEmpty(item.VideoAid))
                        MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(VideoViewPage), item.VideoAid);
                    break;
                case "DYNAMIC_TYPE_PGC":
                    if (item.PgcEpId > 0)
                        MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(BanInfoPage), item.PgcEpId);
                    break;
                case "DYNAMIC_TYPE_ARTICLE":
                    if (item.ArticleId > 0)
                        MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(ArticleContentPage),
                            "https://www.bilibili.com/read/app/" + item.ArticleId);
                    break;
                default:
                    break;
            }
        }

        private void ImgGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            //Tag绑定了父 SpaceDynItemVM，比依赖DataContext更可靠
            var vm = (sender as GridView)?.Tag as SpaceDynItemVM;
            if (vm?.ImagesRaw == null || vm.ImagesRaw.Count == 0) return;
            var clickedThumb = e.ClickedItem as string;
            var origUrl = clickedThumb?.Replace("@300w_200h_1e_1c.jpg", "") ?? "";
            int index = Math.Max(0, vm.ImagesRaw.IndexOf(origUrl));
            new Controls.ImagePreview(vm.ImagesRaw, index).Show();
        }

        #endregion
    }
}
