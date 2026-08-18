using BiliBili.UWP.Models;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Modules.User;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
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
    public sealed partial class MyCollectPage : Page
    {
        readonly MyFollowVideoVM myFollowVideoVM;
        private bool _editingFavorite;

        public MyCollectPage()
        {
            this.InitializeComponent();
            myFollowVideoVM = new MyFollowVideoVM();
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
            UpdateFavoriteCommandState();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.New || myFollowVideoVM == null)
            {
                await myFollowVideoVM.LoadFavorite();
            }
            UpdateFavoriteCommandState();
        }
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back)
            {
                this.NavigationCacheMode = NavigationCacheMode.Disabled;
            }
            base.OnNavigatingFrom(e);
        }

        private void btn_Back_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }

        private async void btn_CreateFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (myFollowVideoVM.Loading)
            {
                return;
            }
            if (!ApiHelper.IsLogin() && !await Utils.ShowLoginDialog())
            {
                Utils.ShowMessageToast("请先登录");
                return;
            }

            _editingFavorite = false;
            cd_FavoriteEditor.Title = "新建收藏夹";
            txt_FavoriteTitle.Text = string.Empty;
            cb_FavoritePublic.IsChecked = true;
            await cd_FavoriteEditor.ShowAsync();
        }

        private async void btn_EditFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (myFollowVideoVM.CurrentFavorite == null || myFollowVideoVM.Loading)
            {
                return;
            }
            if (!ApiHelper.IsLogin() && !await Utils.ShowLoginDialog())
            {
                Utils.ShowMessageToast("请先登录");
                return;
            }

            _editingFavorite = true;
            cd_FavoriteEditor.Title = "编辑收藏夹";
            txt_FavoriteTitle.Text = myFollowVideoVM.CurrentFavorite.title;
            cb_FavoritePublic.IsChecked = !myFollowVideoVM.CurrentFavorite.privacy;
            await cd_FavoriteEditor.ShowAsync();
        }

        private async void FavoriteEditor_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            var title = txt_FavoriteTitle.Text?.Trim();
            if (string.IsNullOrEmpty(title) || title.Length > 20)
            {
                Utils.ShowMessageToast("收藏夹名称不能为空且不能超过20个字符");
                return;
            }

            var deferral = args.GetDeferral();
            try
            {
                var privacy = cb_FavoritePublic.IsChecked != true;
                var success = _editingFavorite
                    ? await myFollowVideoVM.EditFavoriteFolder(title, privacy)
                    : await myFollowVideoVM.CreateFavoriteFolder(title, privacy);
                if (success)
                {
                    UpdateFavoriteCommandState();
                    cd_FavoriteEditor.Hide();
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async void btn_DeleteFavoriteFolder_Click(object sender, RoutedEventArgs e)
        {
            if (myFollowVideoVM.CurrentFavorite == null || myFollowVideoVM.Loading)
            {
                return;
            }
            if (!ApiHelper.IsLogin() && !await Utils.ShowLoginDialog())
            {
                Utils.ShowMessageToast("请先登录");
                return;
            }

            var dialog = new MessageDialog($"确定要删除收藏夹“{myFollowVideoVM.CurrentFavorite.title}”吗？");
            dialog.Commands.Add(new UICommand("确认"));
            dialog.Commands.Add(new UICommand("取消"));
            var command = await dialog.ShowAsync();
            if (command.Label != "确认")
            {
                return;
            }

            await myFollowVideoVM.DeleteCurrentFavoriteFolder();
            UpdateFavoriteCommandState();
        }

        private void UpdateFavoriteCommandState()
        {
            var hasCurrentFavorite = myFollowVideoVM.CurrentFavorite != null;
            btn_EditFavorite.IsEnabled = hasCurrentFavorite;
            btn_DeleteFavoriteFolder.IsEnabled = hasCurrentFavorite;
        }

        private void cb_favbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFavoriteCommandState();
            if (cb_favbox.SelectedItem != null)
            {
                //pageNum = 1;
                //MaxPage = 0;
                //fid = (cb_favbox.SelectedItem as GetUserFovBox).fav_box;
                //top_txt_Header.Text = (cb_favbox.SelectedItem as GetUserFovBox).name;
                //User_ListView_FavouriteVideo.Items.Clear();
                //GetFavouriteBoxVideo();

                myFollowVideoVM.Refresh();
            }
        }



        private void sw_Select_Checked(object sender, RoutedEventArgs e)
        {
            User_ListView_FavouriteVideo.IsItemClickEnabled = false;
            User_ListView_FavouriteVideo.SelectionMode = ListViewSelectionMode.Multiple;
        }

        private void sw_Select_Unchecked(object sender, RoutedEventArgs e)
        {
            User_ListView_FavouriteVideo.IsItemClickEnabled = true;
            User_ListView_FavouriteVideo.SelectionMode = ListViewSelectionMode.None;
        }


        private void Video_ItemClick(object sender, ItemClickEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(VideoViewPage), (e.ClickedItem as FavoriteInfoVideoItemModel).id);
        }

        private async void btn_Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = User_ListView_FavouriteVideo.SelectedItems
                .Cast<FavoriteInfoVideoItemModel>()
                .ToList();
            foreach (var item in selectedItems)
            {
               await myFollowVideoVM.RemoveFavoriteVideo(item);
            }
        }

        private async void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            var item= (sender as MenuFlyoutItem).DataContext as FavoriteInfoVideoItemModel;
            await myFollowVideoVM.RemoveFavoriteVideo(item);
        }
    }
}
