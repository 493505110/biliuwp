using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BiliBili.UWP.Pages
{
    public sealed partial class LiveAllPage : Page
    {
        private readonly LiveArea _liveArea = new LiveArea();
        private int _TJPage = 1;
        private int _NewPage = 1;
        private bool _TJLoading;
        private bool _NewLoading;

        public LiveAllPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (Frame.Name == "bg_Frame")
            {
                g.Background = null;
            }
            b_btn_Refresh.Visibility = SettingHelper.Get_RefreshButton() && SettingHelper.IsPc()
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (e.NavigationMode == NavigationMode.New)
            {
                _TJPage = 1;
                _NewPage = 1;
                _TJLoading = false;
                _NewLoading = false;
                pivot.SelectedIndex = 0;
                await GetTJ();
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

        private async Task GetTJ()
        {
            _TJLoading = true;
            pr_Load.Visibility = Visibility.Visible;
            try
            {
                var result = await _liveArea.GetAllRoomList(_TJPage, false);
                if (!result.success)
                {
                    Utils.ShowMessageToast(result.message, 3000);
                    return;
                }
                if (result.data.list.Count == 0)
                {
                    Utils.ShowMessageToast("加载完了...", 3000);
                    return;
                }

                SetItems(gv_TJ, result.data.list, _TJPage == 1);
                _TJPage++;
            }
            finally
            {
                _TJLoading = false;
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }

        private async Task GetNew()
        {
            _NewLoading = true;
            pr_Load.Visibility = Visibility.Visible;
            try
            {
                var result = await _liveArea.GetAllRoomList(_NewPage, true);
                if (!result.success)
                {
                    Utils.ShowMessageToast(result.message, 3000);
                    return;
                }
                if (result.data.list.Count == 0)
                {
                    Utils.ShowMessageToast("加载完了...", 3000);
                    return;
                }

                SetItems(gv_New, result.data.list, _NewPage == 1);
                _NewPage++;
            }
            finally
            {
                _NewLoading = false;
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }

        private static void SetItems(GridView grid, ObservableCollection<RoomListItem> items, bool replace)
        {
            if (replace || grid.ItemsSource == null)
            {
                grid.ItemsSource = items;
                return;
            }

            var target = grid.ItemsSource as ObservableCollection<RoomListItem>;
            if (target == null)
            {
                grid.ItemsSource = items;
                return;
            }
            foreach (var item in items)
            {
                target.Add(item);
            }
        }

        private void btn_Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void sv_TJ_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sv_TJ.VerticalOffset == sv_TJ.ScrollableHeight && !_TJLoading)
            {
                await GetTJ();
            }
        }

        private void gv_TJ_ItemClick(object sender, ItemClickEventArgs e)
        {
            MessageCenter.SendNavigateTo(
                NavigateMode.Play,
                typeof(LiveRoomPage),
                (e.ClickedItem as RoomListItem).roomid);
        }

        private async void btn_LoadMore_TJ_Click(object sender, RoutedEventArgs e)
        {
            if (!_TJLoading)
            {
                await GetTJ();
            }
        }

        private async void pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pivot.SelectedIndex == 1 && gv_New.ItemsSource == null && !_NewLoading)
            {
                _NewPage = 1;
                await GetNew();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (availableSize.Width <= 500)
            {
                bor_Width2.Width = availableSize.Width / 2 - 20;
            }
            else
            {
                var count = Math.Max(1, Convert.ToInt32(availableSize.Width / 200));
                bor_Width2.Width = availableSize.Width / count - 15;
            }
            return base.MeasureOverride(availableSize);
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        private async void sv_New_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sv_New.VerticalOffset == sv_New.ScrollableHeight && !_NewLoading)
            {
                await GetNew();
            }
        }

        private async void btn_LoadMore_New_Click(object sender, RoutedEventArgs e)
        {
            if (!_NewLoading)
            {
                await GetNew();
            }
        }

        private async void b_btn_Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (pivot.SelectedIndex == 0)
            {
                _TJPage = 1;
                await GetTJ();
            }
            else if (pivot.SelectedIndex == 1)
            {
                _NewPage = 1;
                await GetNew();
            }
        }
    }
}
