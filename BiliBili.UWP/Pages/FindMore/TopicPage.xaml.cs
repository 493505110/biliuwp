using BiliBili.UWP.Api;
using BiliBili.UWP.Helper;
using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BiliBili.UWP.Pages
{
    public sealed partial class TopicPage : Page
    {
        bool IsLoading;
        int offset;
        bool hasMore = true;

        public TopicPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        private void btn_Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            b_btn_Refresh.Visibility = SettingHelper.Get_RefreshButton() && SettingHelper.IsPc()
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (e.NavigationMode == NavigationMode.New)
            {
                GetTopic(true);
            }
        }

        private async void GetTopic(bool refresh = false)
        {
            if (IsLoading || (!refresh && !hasMore))
            {
                return;
            }

            try
            {
                IsLoading = true;
                btn_More_Video.Visibility = Visibility.Collapsed;
                pr_Load.Visibility = Visibility.Visible;

                int requestOffset = refresh ? 0 : offset;
                ApiModel api = new ApiModel
                {
                    method = HttpMethod.GET,
                    baseUrl = "https://api.bilibili.com/x/topic/pub/search",
                    parameter = $"keywords=&page_size=20&page_num=1&offset={requestOffset}&web_location=333.1365"
                };
                HttpResults results = await api.Request();
                if (!results.status)
                {
                    Utils.ShowMessageToast(results.message, 2000);
                    return;
                }

                ApiDataModel<TopicDataModel> response = await results.GetJson<ApiDataModel<TopicDataModel>>();
                if (response == null || !response.success)
                {
                    Utils.ShowMessageToast(response?.message ?? "读取失败了", 2000);
                    return;
                }

                List<TopicModel> topics = response.data?.topic_items;
                if (topics == null)
                {
                    Utils.ShowMessageToast("读取失败了", 2000);
                    return;
                }

                if (refresh)
                {
                    grid_View.Items.Clear();
                }
                foreach (TopicModel topic in topics)
                {
                    grid_View.Items.Add(topic);
                }

                offset = response.data.page_info?.offset ?? requestOffset + topics.Count;
                hasMore = response.data.page_info?.has_more == true && topics.Count > 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("话题中心读取失败", LogType.ERROR, ex);
                Utils.ShowMessageToast("读取失败了", 2000);
            }
            finally
            {
                IsLoading = false;
                btn_More_Video.Visibility = hasMore ? Visibility.Visible : Visibility.Collapsed;
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }

        private void btn_More_Video_Click(object sender, RoutedEventArgs e)
        {
            GetTopic();
        }

        private void list_Topic_ItemClick(object sender, ItemClickEventArgs e)
        {
            TopicModel topic = e.ClickedItem as TopicModel;
            if (topic != null)
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(WebPage), topic.link);
            }
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            int columns = Math.Max(1, Convert.ToInt32(ActualWidth / 400));
            bor_Width.Width = ActualWidth / columns - 12;
        }

        private void sv_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sv.VerticalOffset >= sv.ScrollableHeight - 1)
            {
                GetTopic();
            }
        }

        private void b_btn_Refresh_Click(object sender, RoutedEventArgs e)
        {
            GetTopic(true);
        }
    }

    public class TopicDataModel
    {
        public List<TopicModel> topic_items { get; set; }
        public TopicPageInfoModel page_info { get; set; }
    }

    public class TopicPageInfoModel
    {
        public int offset { get; set; }
        public bool has_more { get; set; }
    }

    public class TopicModel
    {
        public long id { get; set; }
        public string name { get; set; }
        public long view { get; set; }
        public long discuss { get; set; }
        public string stat_desc { get; set; }
        public string description { get; set; }
        public string link => $"https://m.bilibili.com/topic-detail?topic_id={id}";
    }
}
