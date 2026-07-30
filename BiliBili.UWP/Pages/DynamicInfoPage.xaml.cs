using BiliBili.UWP.Controls;
using BiliBili.UWP.Models;
using BiliBili.UWP.Api;
using BiliBili.UWP.Api.User;
using BiliBili.UWP.Pages.FindMore;
using BiliBili.UWP.Pages.User;
using BiliBili.UWP.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BiliBili.UWP.Pages
{
    public sealed partial class DynamicInfoPage : Page
    {
        public DynamicInfoPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        readonly DynamicAPI _dynamicAPI = new DynamicAPI();

        // 当前动态 ViewModel 和 id
        SpaceDynItemVM _vm;
        string _dynIdStr = "";

        // 转发列表分页状态
        string _forwardOffset = "";
        bool _forwardHasMore = false;
        bool _forwardLoading = false;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var par = e.Parameter as object[];
            if (par == null || par.Length == 0) return;

            var id = par[0]?.ToString() ?? "";
            if (string.IsNullOrEmpty(id)) return;

            // 重置状态（支持缓存导航重新打开不同动态）
            if (id != _dynIdStr)
            {
                _dynIdStr = "";
                _vm = null;
                dynList.ItemsSource = null;
                ls_repost.ItemsSource = null;
                _forwardOffset = "";
                _forwardHasMore = false;
                btn_LoadMoreRepost.Visibility = Visibility.Collapsed;
                noRepost.Visibility = Visibility.Collapsed;
                comment.ClearComment();
                LoadDynamic(id);
            }
        }

        private async void LoadDynamic(string id)
        {
            try
            {
                pr_Load.Visibility = Visibility.Visible;

                var result = await _dynamicAPI.GetDetail(id).Request();
                if (!result.status)
                {
                    Utils.ShowMessageToast("读取动态失败：" + result.message);
                    return;
                }
                var resp = await result.GetData<SpaceDynDetailData>();
                if (resp == null || !resp.success || resp.data?.item == null)
                {
                    Utils.ShowMessageToast(resp?.message ?? "读取动态失败");
                    return;
                }

                var item = resp.data.item;
                _dynIdStr = item.id_str ?? id;
                _vm = SpaceDynItemVM.FromItem(item);

                // 展示动态正文
                dynList.ItemsSource = new List<SpaceDynItemVM> { _vm };

                // 删除按钮：仅自己的动态可见
                btn_Delete.Visibility =
                    item.modules?.module_author?.mid.ToString() == ApiHelper.GetUserId()
                    ? Visibility.Visible : Visibility.Collapsed;

                // 初始化评论区
                var basic = item.basic;
                if (basic != null && !string.IsNullOrEmpty(basic.comment_id_str))
                    InitializeComment(basic.comment_id_str, basic.comment_type);

                // 加载转发列表
                LoadForward();
            }
            catch (Exception ex)
            {
                Helper.LogHelper.WriteLog("读取动态详情失败", Helper.LogType.ERROR, ex);
                Utils.ShowMessageToast("读取动态详情失败");
            }
            finally
            {
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }

        // comment_type 对应关系（来自Bilibili评论区类型）
        // 1=视频  11=相册/图文  17=动态/文字/转发  12=专栏
        private void InitializeComment(string commentIdStr, int commentType)
        {
            CommentMode mode;
            switch (commentType)
            {
                case 1:  mode = CommentMode.Video;   break;
                case 11: mode = CommentMode.Photo;   break;
                default: mode = CommentMode.Dynamic; break;
            }
            comment.InitializeComment(new LoadCommentInfo()
            {
                commentMode = mode,
                conmmentSortMode = ConmmentSortMode.Hot,
                oid = commentIdStr
            });
        }

        #region 转发列表

        private async void LoadForward()
        {
            if (_forwardLoading) return;
            try
            {
                _forwardLoading = true;
                pr_Load.Visibility = Visibility.Visible;
                noRepost.Visibility = Visibility.Collapsed;
                btn_LoadMoreRepost.Visibility = Visibility.Collapsed;

                var result = await _dynamicAPI.GetForwardList(_dynIdStr, _forwardOffset).Request();
                if (!result.status)
                {
                    Utils.ShowMessageToast("读取转发列表失败：" + result.message);
                    return;
                }
                var resp = await result.GetData<SpaceDynForwardResp>();
                if (resp == null || !resp.success)
                {
                    Utils.ShowMessageToast(resp?.message ?? "读取转发列表失败");
                    return;
                }

                var data = resp.data;
                if (data?.items == null || data.items.Count == 0)
                {
                    if (ls_repost.ItemsSource == null || (ls_repost.ItemsSource as ObservableCollection<SpaceDynForwardItemVM>).Count == 0)
                        noRepost.Visibility = Visibility.Visible;
                    return;
                }

                var col = ls_repost.ItemsSource as ObservableCollection<SpaceDynForwardItemVM>;
                if (col == null)
                {
                    col = new ObservableCollection<SpaceDynForwardItemVM>();
                    ls_repost.ItemsSource = col;
                }
                foreach (var item in data.items)
                    col.Add(SpaceDynForwardItemVM.From(item));

                _forwardHasMore = data.has_more;
                _forwardOffset = data.offset ?? "";

                // 更新转发标签上的数字
                if (data.total > 0)
                    txt_ForwardHeader.Text = $"转发 {data.total}";

                btn_LoadMoreRepost.Visibility = _forwardHasMore ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Helper.LogHelper.WriteLog("读取转发列表失败", Helper.LogType.ERROR, ex);
                Utils.ShowMessageToast("读取转发列表失败");
            }
            finally
            {
                _forwardLoading = false;
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }

        private void btn_LoadMoreRepost_Click(object sender, RoutedEventArgs e)
        {
            if (!_forwardLoading) LoadForward();
        }

        #endregion

        #region 操作按钮

        private void btn_Back_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }

        private void btn_Share_Click(object sender, RoutedEventArgs e)
        {
            Utils.SetClipboard("https://t.bilibili.com/" + _dynIdStr);
            Utils.ShowMessageToast("已将地址复制到剪切板", 3000);
        }

        private async void btn_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (!ApiHelper.IsLogin() || string.IsNullOrEmpty(_dynIdStr)) return;
            try
            {
                // 仍使用旧接口（新接口需要Cookie CSRF，App走access_key体系）
                string url = string.Format(
                    "https://api.vc.bilibili.com/dynamic_svr/v1/dynamic_svr/rm_dynamic?access_key={0}&appkey={1}&build=5250000&platform=android&ts={2}",
                    ApiHelper.access_key, ApiHelper.AndroidKey.Appkey, ApiHelper.GetTimeSpan_2);
                url += "&sign=" + ApiHelper.GetSign(url);

                string body = $"dynamic_id={_dynIdStr}&uid={ApiHelper.GetUserId()}&csrf=&csrf_token=";
                var re = await Helper.WebClientClass.PostResultsUtf8(new Uri(url), body);
                var obj = Newtonsoft.Json.Linq.JObject.Parse(re);
                if (obj["code"].ToObject<int>() == 0)
                {
                    Utils.ShowMessageToast("已删除");
                    this.Frame.GoBack();
                }
                else
                {
                    Utils.ShowMessageToast("删除失败：" + obj["message"]);
                }
            }
            catch (Exception ex)
            {
                Helper.LogHelper.WriteLog("删除动态失败", Helper.LogType.ERROR, ex);
                Utils.ShowMessageToast("删除动态发生错误");
            }
        }

        #endregion

        #region 事件处理

        private void pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pivot.SelectedIndex == 1 && comment.CommentCount == 0)
                comment.LoadComment();
        }

        private void ImgGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            var vm = (sender as GridView)?.Tag as SpaceDynItemVM;
            if (vm?.ImagesRaw == null || vm.ImagesRaw.Count == 0) return;
            var clickedThumb = e.ClickedItem as string;
            var origUrl = clickedThumb?.Replace("@300w_200h_1e_1c.jpg", "") ?? "";
            int index = Math.Max(0, vm.ImagesRaw.IndexOf(origUrl));
            new Controls.ImagePreview(vm.ImagesRaw, index).Show();
        }

        private void btn_ForwardUser_Click(object sender, RoutedEventArgs e)
        {
            // Tag 绑定了 Mid（long）
            if ((sender as HyperlinkButton)?.Tag is long mid && mid > 0)
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(UserCenterPage), mid);
        }

        private void dynamic_OpenComment(object sender, EventArgs e)
        {
            comment.ShowCommentBox();
        }

        #endregion
    }
}
