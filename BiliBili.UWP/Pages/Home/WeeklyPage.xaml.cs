using BiliBili.UWP.Modules.Home;
using BiliBili.UWP.Modules.Home.WeeklyModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BiliBili.UWP.Pages.Home
{
    /// <summary>
    /// 每周必看。热门页顶部入口原本用 WebView2 打开 App 专用 h5 页面，桌面上是白屏，改成原生页。
    /// </summary>
    public sealed partial class WeeklyPage : Page
    {
        readonly WeeklyVM weeklyVM;
        bool suppressPeriodChanged;

        public WeeklyPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Enabled;
            weeklyVM = new WeeklyVM();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            //上次加载失败时 Items 为空，再次进入要重试，不能只认 NavigationMode.New
            if (weeklyVM.Items.Count != 0)
            {
                return;
            }
            suppressPeriodChanged = true;
            try
            {
                await weeklyVM.LoadAsync();
            }
            finally
            {
                suppressPeriodChanged = false;
            }
        }

        private async void cb_Period_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (suppressPeriodChanged)
            {
                return;
            }
            var period = (sender as ComboBox)?.SelectedItem as WeeklyPeriodModel;
            if (period == null || period.number == weeklyVM.SelectedPeriod?.number)
            {
                return;
            }
            weeklyVM.SelectedPeriod = period;
            await weeklyVM.LoadPeriod(period.number);
        }

        private void List_weekly_ItemClick(object sender, ItemClickEventArgs e)
        {
            var data = e.ClickedItem as WeeklyVideoModel;
            if (data == null)
            {
                return;
            }
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(VideoViewPage), data.aid.ToString());
        }

        private void btn_back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}
