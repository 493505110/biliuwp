using BiliBili.UWP.Pages;
using BiliBili.UWP.Pages.FindMore;
using BiliBili.UWP.Pages.Music;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BiliBili.UWP.Views
{
    public sealed partial class ChannelPage : Page
    {
        public ChannelPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            b_btn_Refresh.Visibility = SettingHelper.Get_RefreshButton() && SettingHelper.IsPc()
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (e.NavigationMode == NavigationMode.New)
            {
                await LoadRegions();
            }
        }

        private async Task LoadRegions(bool forceRefresh = false)
        {
            pr_Load.Visibility = Visibility.Visible;

            if (forceRefresh || ApiHelper.regions == null)
            {
                await ApiHelper.SetRegions();
            }

            ls_Part.ItemsSource = ApiHelper.regions;
            if (ApiHelper.regions == null)
            {
                Utils.ShowMessageToast("分区加载失败，请稍后重试");
            }

            pr_Load.Visibility = Visibility.Collapsed;
        }

        private void ls_Part_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as RegionModel;
            if (item == null)
            {
                return;
            }

            if (item.name == "放映厅")
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(Pages.Season.SeasonIndexPage), new Modules.Season.SeasonIndexParameter
                {
                    type = Modules.Season.IndexSeasonType.Movie
                });
                return;
            }
            if (item.name == "相簿")
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(AlbumPage));
                return;
            }
            if (item.name == "音频")
            {
                MessageCenter.SendNavigateTo(NavigateMode.Home, typeof(MusicHomePage));
                return;
            }
            if (item.name == "小视频")
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(LiveVideoPage));
                return;
            }
            if (item.name == "专栏")
            {
                MessageCenter.SendNavigateTo(NavigateMode.Home, typeof(ArticlePage));
                return;
            }
            if (item.name == "直播")
            {
                MessageCenter.SendNavigateTo(NavigateMode.Home, typeof(LiveV2Page));
                return;
            }
            if (item.name.Contains("排行榜"))
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(RankPage), item.name.Contains("原创") ? 2 : 1);
                return;
            }
            if (item.name == "专题中心")
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(TopicPage));
                return;
            }
            if (item.uri != null && item.uri.Contains("https://"))
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(WebPage), item.uri);
                return;
            }

            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(PartsPage), item);
        }

        private async void b_btn_Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadRegions(true);
        }
    }
}
