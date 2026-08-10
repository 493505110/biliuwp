using BiliBili.UWP.Api;
using BiliBili.UWP.Api.User;
using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BiliBili.UWP.Pages
{
    public sealed partial class ChatPage : Page
    {
        private readonly MessageAPI _messageAPI = new MessageAPI();
        private readonly string _deviceId = Guid.NewGuid().ToString();
        private DispatcherTimer _timer;
        private long _talkerId;
        private long _ownId;
        private int _sessionType = 1;
        private string _peerAvatar = string.Empty;
        private string _selfAvatar = string.Empty;
        private bool _loadingMessages;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<ChatModel> messages { get; private set; }

        public ChatPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            messages = new ObservableCollection<ChatModel>();
            list_view.ItemsSource = messages;
            _ownId = ParseId(ApiHelper.GetUserId());

            var parameters = e.Parameter as object[];
            if (parameters == null || parameters.Length == 0)
            {
                Utils.ShowMessageToast("无法读取私信会话", 2000);
                return;
            }

            var session = parameters[0] as MessageChatModel;
            if (session != null)
            {
                _talkerId = ParseId(session.rid);
                _sessionType = session.session_type;
                _peerAvatar = session.avatar_url ?? string.Empty;
                top_txt_Header.Text = string.IsNullOrEmpty(session.room_name) ? "私聊" : session.room_name;
            }
            else
            {
                _talkerId = ParseId(parameters[0]?.ToString());
                _sessionType = 1;
            }

            if (_talkerId <= 0)
            {
                Utils.ShowMessageToast("私信会话参数无效", 2000);
                return;
            }

            pr_Load.Visibility = Visibility.Visible;
            await LoadParticipantInfo();
            await GetRoomMessage();
            pr_Load.Visibility = Visibility.Collapsed;

            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(10);
                _timer.Tick += Timer_Tick;
            }
            _timer.Start();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _timer?.Stop();
        }

        private async void Timer_Tick(object sender, object e)
        {
            await GetRoomMessage();
        }

        private void btn_back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async Task LoadParticipantInfo()
        {
            var peerTask = GetUserCard(_talkerId);
            var selfTask = GetUserCard(_ownId);
            await Task.WhenAll(peerTask, selfTask);

            var peer = peerTask.Result;
            if (peer != null)
            {
                top_txt_Header.Text = string.IsNullOrEmpty(peer.name) ? top_txt_Header.Text : peer.name;
                _peerAvatar = NormalizeImageUrl(peer.face);
            }

            var self = selfTask.Result;
            if (self != null)
            {
                _selfAvatar = NormalizeImageUrl(self.face);
            }
        }

        private async Task<MessageUserCardModel> GetUserCard(long mid)
        {
            if (mid <= 0)
            {
                return null;
            }
            var response = await _messageAPI.UserCard(mid).Request();
            if (response == null || !response.status)
            {
                return null;
            }
            var result = await response.GetData<MessageUserCardDataModel>();
            return result != null && result.success ? result.data?.card : null;
        }

        private async Task GetRoomMessage()
        {
            if (_loadingMessages)
            {
                return;
            }

            _loadingMessages = true;
            try
            {
                var response = await _messageAPI.SessionMessages(_talkerId, _sessionType).Request();
                if (response == null || !response.status)
                {
                    Utils.ShowMessageToast(response?.message ?? "读取信息失败", 2000);
                    return;
                }

                var result = await response.GetData<MessageHistoryModel>();
                if (result == null || !result.success)
                {
                    Utils.ShowMessageToast("读取信息失败" + (result?.message ?? string.Empty), 2000);
                    return;
                }

                var history = result.data;
                var items = history?.messages ?? new List<MessageHistoryItemModel>();
                foreach (var item in items.OrderBy(message => message.timestamp))
                {
                    if (messages.Any(message => message.id == item.msg_key))
                    {
                        continue;
                    }

                    var isPeer = item.sender_uid != _ownId;
                    messages.Add(new ChatModel()
                    {
                        id = item.msg_key,
                        mid = item.sender_uid.ToString(),
                        avatar_url = isPeer ? _peerAvatar : _selfAvatar,
                        is_me = isPeer ? 2 : 1,
                        message = item.msg_status == 1 || item.sys_cancel
                            ? "[消息已撤回]"
                            : MyMessagePage.ParseMessagePreview(item.content, item.msg_type),
                        send_time = item.timestamp
                    });
                }

                if (history != null && history.max_seqno > 0)
                {
                    await MarkAsRead(history.max_seqno);
                }

                sc.ChangeView(null, sc.ExtentHeight, null);
            }
            finally
            {
                _loadingMessages = false;
            }
        }

        private async Task MarkAsRead(long sequenceNumber)
        {
            var csrf = Account.GetCookieValue("bili_jct");
            if (string.IsNullOrEmpty(csrf))
            {
                return;
            }
            await _messageAPI.MarkSessionRead(_talkerId, _sessionType, sequenceNumber, csrf).Request();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            txt_Content.Text += (sender as Button)?.Content?.ToString();
        }

        private async void btn_Send_Click(object sender, RoutedEventArgs e)
        {
            var content = txt_Content.Text?.Trim();
            if (string.IsNullOrEmpty(content))
            {
                Utils.ShowMessageToast("内容不能为空", 2000);
                return;
            }

            var csrf = Account.GetCookieValue("bili_jct");
            if (_ownId <= 0 || string.IsNullOrEmpty(csrf))
            {
                Utils.ShowMessageToast("登录 Cookie 无效，请重新登录", 2000);
                return;
            }

            btn_Send.IsEnabled = false;
            try
            {
                var response = await _messageAPI.SendMessage(
                    _ownId,
                    _talkerId,
                    _sessionType,
                    content,
                    _deviceId,
                    csrf).Request();
                if (response == null || !response.status)
                {
                    Utils.ShowMessageToast(response?.message ?? "发送失败", 2000);
                    return;
                }

                var result = await response.GetData<object>();
                if (result == null || !result.success)
                {
                    Utils.ShowMessageToast("发送失败," + (result?.message ?? string.Empty), 2000);
                    return;
                }

                txt_Content.Text = string.Empty;
                await GetRoomMessage();
            }
            finally
            {
                btn_Send.IsEnabled = true;
            }
        }

        private static long ParseId(string value)
        {
            long result;
            return long.TryParse(value, out result) ? result : 0;
        }

        private static string NormalizeImageUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }
            return url.StartsWith("//") ? "https:" + url : url.Replace("http://", "https://");
        }
    }

    public class ChatModel
    {
        public long id { get; set; }
        public string mid { get; set; }
        public string uname { get; set; }
        public string avatar_url { get; set; }
        public int is_me { get; set; }
        public string message { get; set; }
        public long send_time { get; set; }

        public string Send_time
        {
            get
            {
                if (send_time <= 0)
                {
                    return string.Empty;
                }
                return DateTimeOffset.FromUnixTimeSeconds(send_time).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
    }

    public class MessageItemDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate Chat1 { get; set; }
        public DataTemplate Chat2 { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return (item as ChatModel)?.is_me == 2 ? Chat1 : Chat2;
        }
    }
}
