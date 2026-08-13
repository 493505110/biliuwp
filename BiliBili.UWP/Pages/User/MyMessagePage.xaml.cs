using BiliBili.UWP.Api;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Api.User;
using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using BiliBili.UWP.Pages.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BiliBili.UWP.Pages
{
    enum ChatType
    {
        New,
        Old
    }

    public sealed partial class MyMessagePage : Page
    {
        private readonly MessageAPI _messageAPI = new MessageAPI();
        private readonly Dictionary<long, MessageUserCardModel> _userCards = new Dictionary<long, MessageUserCardModel>();
        private DispatcherTimer _timer;
        private int _activeLoads;
        private bool _loadingUnread;

        public MyMessagePage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            MessageCenter.HasMessaged += MessageCenter_HasMessaged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromMinutes(2);
                _timer.Tick += Timer_Tick;
            }
            _timer.Start();
            GetMessage();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            MessageCenter.SendMessage("private:all");
            _timer?.Stop();
        }

        private void Timer_Tick(object sender, object e)
        {
            GetMessage();
        }

        private async void btn_MarkAllRead_Click(object sender, RoutedEventArgs e)
        {
            var csrf = Account.GetCookieValue("bili_jct");
            if (string.IsNullOrEmpty(csrf))
            {
                Utils.ShowMessageToast("无法获取登录凭证", 2000);
                return;
            }
            var sessions = list_ChatMe.ItemsSource as IEnumerable<MessageChatModel>;
            if (sessions == null || !sessions.Any())
            {
                Utils.ShowMessageToast("没有可标记的会话", 2000);
                return;
            }
            btn_MarkAllRead.IsEnabled = false;
            var count = 0;
            foreach (var session in sessions)
            {
                if (session.msg_count > 0 && long.TryParse(session.rid, out var talkerId))
                {
                    await _messageAPI.MarkSessionRead(talkerId, session.session_type, session.max_seqno, csrf).Request();
                    session.msg_count = 0;
                    count++;
                }
            }
            // 乐观更新：本地清零后立即重建列表，不依赖API缓存
            list_ChatMe.ItemsSource = sessions.ToList();
            SetUnreadVisibility(bor_SX, 0);
            btn_MarkAllRead.IsEnabled = true;
            if (count > 0)
            {
                Utils.ShowMessageToast($"已标记{count}个会话为已读", 3000);
                GetMessage();
                GetSessions();
                MessageCenter.SendMessage("private:all");
            }
            else
            {
                Utils.ShowMessageToast("没有未读的会话", 2000);
            }
        }


        private void MessageCenter_HasMessaged(object sender, object e)
        {
            var tag = e as string;
            if (tag != null && tag.StartsWith("private:"))
            {
                var rid = tag.Substring("private:".Length);
                var sessions = list_ChatMe.ItemsSource as IEnumerable<MessageChatModel>;
                if (sessions != null)
                {
                    foreach (var session in sessions)
                    {
                        if (session.rid == rid)
                        {
                            session.msg_count = 0;
                        }
                    }
                    list_ChatMe.ItemsSource = sessions.ToList();
                }
            }
        }

        private void btn_Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void btn_HF_Click(object sender, RoutedEventArgs e)
        {
            pivot.SelectedIndex = Convert.ToInt32((sender as Button)?.Tag);
        }

        private void pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            GetMessage();
            switch (pivot.SelectedIndex)
            {
                case 0:
                    GetReply();
                    break;
                case 1:
                    GetAtMe();
                    break;
                case 2:
                    GetLikes();
                    break;
                case 3:
                    GetNotices();
                    break;
                case 4:
                    GetSessions();
                    break;
            }
        }

        private async void GetReply()
        {
            BeginLoad();
            try
            {
                var data = await RequestData<MessageFeedReplyDataModel>(_messageAPI.ReplyFeed(), "读取回复失败");
                var items = data?.items ?? new List<MessageFeedReplyItemModel>();
                list_Reply.ItemsSource = items.Select(item => ToDisplayItem(
                    item.user,
                    item.item,
                    item.reply_time,
                    FirstText(item.item?.source_content, item.item?.root_reply_content, item.item?.target_reply_content)))
                    .ToList();
            }
            finally
            {
                EndLoad();
            }
        }

        private async void GetAtMe()
        {
            BeginLoad();
            try
            {
                var data = await RequestData<MessageFeedAtDataModel>(_messageAPI.AtFeed(), "读取@消息失败");
                var items = data?.items ?? new List<MessageFeedAtItemModel>();
                list_AtMe.ItemsSource = items.Select(item => ToDisplayItem(
                    item.user,
                    item.item,
                    item.at_time,
                    item.item?.source_content))
                    .ToList();
            }
            finally
            {
                EndLoad();
            }
        }

        private async void GetLikes()
        {
            BeginLoad();
            try
            {
                var data = await RequestData<MessageFeedLikeDataModel>(_messageAPI.LikeFeed(), "读取点赞消息失败");
                var bucket = data?.total ?? data?.latest;
                var items = bucket?.items ?? new List<MessageFeedLikeItemModel>();
                list_Zan.ItemsSource = items.Select(item =>
                {
                    var user = item.users?.FirstOrDefault() ?? new MessageFeedUserModel();
                    var display = ToDisplayItem(user, item.item, item.like_time, "赞了你的内容");
                    if (item.counts > 1)
                    {
                        display.name = $"{display.name} 等{item.counts}人";
                    }
                    return display;
                }).ToList();
            }
            finally
            {
                EndLoad();
            }
        }

        private async void GetNotices()
        {
            BeginLoad();
            try
            {
                var data = await RequestData<List<MessageSystemNoticeModel>>(_messageAPI.SystemNotices(), "读取通知失败");
                list_Notify.ItemsSource = (data ?? new List<MessageSystemNoticeModel>()).Select(item => new MessageReplyModel()
                {
                    id = item.id.ToString(),
                    cursor = item.cursor.ToString(),
                    title = item.title,
                    content = ParseNoticeContent(item.content),
                    time_at = item.time_at
                }).ToList();
            }
            finally
            {
                EndLoad();
            }
        }

        private async void GetSessions()
        {
            BeginLoad();
            try
            {
                var data = await RequestData<MessageSessionListModel>(_messageAPI.Sessions(), "读取私信失败");
                var sessions = data?.session_list ?? new List<MessageSessionModel>();
                await LoadUserCards(sessions);
                list_ChatMe.ItemsSource = sessions.Select(ToChatDisplayItem).ToList();
            }
            finally
            {
                EndLoad();
            }
        }

        private async void GetMessage()
        {
            if (_loadingUnread)
            {
                return;
            }

            if (!ApiHelper.IsLogin() || string.IsNullOrEmpty(Account.GetCookieValue("SESSDATA")))
            {
                SetUnreadVisibility(bor_HF, 0);
                SetUnreadVisibility(bor_At, 0);
                SetUnreadVisibility(bor_Zan, 0);
                SetUnreadVisibility(bor_TZ, 0);
                SetUnreadVisibility(bor_SX, 0);
                return;
            }

            _loadingUnread = true;
            try
            {
                var feedRequest = _messageAPI.UnreadFeed().Request();
                var privateRequest = _messageAPI.PrivateUnread().Request();
                var groupRequest = _messageAPI.GroupUnread().Request();
                await Task.WhenAll(feedRequest, privateRequest, groupRequest);

                var feed = await ReadData<MessageFeedUnreadModel>(feedRequest.Result, "读取通知失败");
                var privateUnread = await ReadData<MessagePrivateUnreadModel>(privateRequest.Result, "读取私信未读数失败");
                var groupUnread = await ReadData<MessageGroupUnreadModel>(groupRequest.Result, "读取粉丝团未读数失败");
                if (feed != null)
                {
                    SetUnreadVisibility(bor_HF, feed.recv_reply);
                    SetUnreadVisibility(bor_At, feed.at);
                    SetUnreadVisibility(bor_Zan, feed.recv_like);
                    SetUnreadVisibility(bor_TZ, feed.sys_msg);
                }

                if (privateUnread != null || groupUnread != null)
                {
                    SetUnreadVisibility(bor_SX, (privateUnread?.Total ?? 0) + (groupUnread?.unread_count ?? 0));
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("读取消息未读数失败", LogType.ERROR, ex);
            }
            finally
            {
                _loadingUnread = false;
            }
        }

        private async Task<T> RequestData<T>(ApiModel api, string failureMessage) where T : class
        {
            return await ReadData<T>(await api.Request(), failureMessage);
        }

        private static async Task<T> ReadData<T>(HttpResults response, string failureMessage) where T : class
        {
            if (response == null || !response.status)
            {
                Utils.ShowMessageToast(response?.message ?? failureMessage, 3000);
                return null;
            }

            var result = await response.GetData<T>();
            if (result == null || !result.success)
            {
                Utils.ShowMessageToast(failureMessage + (result?.message ?? string.Empty), 3000);
                return null;
            }
            return result.data;
        }

        private MessageChatModel ToChatDisplayItem(MessageSessionModel session)
        {
            MessageUserCardModel card;
            _userCards.TryGetValue(session.talker_id, out card);
            var display = new MessageChatModel()
            {
                rid = session.talker_id.ToString(),
                mid = session.session_type == 1 && session.system_msg_type == 0
                    ? session.talker_id.ToString()
                    : null,
                session_type = session.session_type,
                max_seqno = session.max_seqno,
                msg_count = session.unread_count,
                room_name = FirstText(session.group_name, session.account_info?.name, card?.name, $"用户{session.talker_id}"),
                avatar_url = NormalizeImageUrl(FirstText(session.group_cover, session.account_info?.Avatar, card?.face)),
                last_msg = ParseMessagePreview(session.last_msg?.content, session.last_msg?.msg_type ?? 0),
                last_time = session.last_msg?.timestamp ?? NormalizeSessionTimestamp(session.session_ts)
            };
            return display;
        }

        private async Task LoadUserCards(IList<MessageSessionModel> sessions)
        {
            var mids = sessions
                .Where(session => session.session_type == 1
                    && session.system_msg_type == 0
                    && session.talker_id > 0
                    && (string.IsNullOrEmpty(session.account_info?.name)
                        || string.IsNullOrEmpty(session.account_info?.Avatar))
                    && !_userCards.ContainsKey(session.talker_id))
                .Select(session => session.talker_id)
                .Distinct()
                .ToList();

            for (var index = 0; index < mids.Count; index += 50)
            {
                var batch = mids.Skip(index).Take(50).ToList();
                var cards = await RequestData<List<MessageUserCardModel>>(
                    _messageAPI.UserCards(batch),
                    "读取私信用户信息失败");
                foreach (var card in cards ?? new List<MessageUserCardModel>())
                {
                    long mid;
                    if (card != null && long.TryParse(card.mid, out mid) && mid > 0)
                    {
                        _userCards[mid] = card;
                    }
                }
            }
        }

        private static MessageReplyModel ToDisplayItem(MessageFeedUserModel user, MessageFeedContentModel item, long timestamp, string content)
        {
            return new MessageReplyModel()
            {
                id = item?.source_id.ToString(),
                mid = user?.mid.ToString(),
                name = user?.nickname ?? string.Empty,
                face = NormalizeImageUrl(user?.avatar),
                time_at = FormatTimestamp(timestamp),
                content = FirstText(content, item?.business),
                title = item?.title ?? string.Empty,
                link = FirstText(item?.native_uri, item?.uri)
            };
        }

        private static string ParseNoticeContent(string content)
        {
            if (string.IsNullOrEmpty(content) || !content.TrimStart().StartsWith("{"))
            {
                return content ?? string.Empty;
            }
            try
            {
                var json = JObject.Parse(content);
                return FirstText(json["web"]?.ToString(), json["text"]?.ToString(), content);
            }
            catch
            {
                return content;
            }
        }

        internal static string ParseMessagePreview(string content, int messageType)
        {
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }
            if (messageType == 2)
            {
                return "[图片]";
            }
            if (messageType == 5)
            {
                return "[消息已撤回]";
            }
            if (messageType == 6)
            {
                return "[表情]";
            }

            try
            {
                var json = JObject.Parse(content);
                return FirstText(
                    json["content"]?.ToString(),
                    json["text"]?.ToString(),
                    json["title"]?.ToString(),
                    json["desc"]?.ToString(),
                    messageType == 2 ? "[图片]" : "[消息]");
            }
            catch
            {
                return content;
            }
        }

        private async void list_Reply_ItemClick(object sender, ItemClickEventArgs e)
        {
            await NavigateMessageLink((e.ClickedItem as MessageReplyModel)?.link);
        }

        private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as HyperlinkButton;
            long mid;
            if (button == null || !long.TryParse(button.Tag?.ToString(), out mid) || mid <= 0)
            {
                return;
            }
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(UserCenterPage), mid.ToString());
        }

        private async void list_Notify_ItemClick(object sender, ItemClickEventArgs e)
        {
            await NavigateMessageLink((e.ClickedItem as MessageReplyModel)?.link);
        }

        private void list_ChatMe_ItemClick(object sender, ItemClickEventArgs e)
        {
            var session = e.ClickedItem as MessageChatModel;
            if (session != null)
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(ChatPage), new object[] { session, ChatType.Old });
            }
        }

        private static async Task NavigateMessageLink(string link)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return;
            }
            if (!await MessageCenter.HandelUrl(link))
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(WebPage), link);
            }
        }

        private void BeginLoad()
        {
            _activeLoads++;
            pr_Load.Visibility = Visibility.Visible;
        }

        private void EndLoad()
        {
            _activeLoads = Math.Max(0, _activeLoads - 1);
            if (_activeLoads == 0)
            {
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }

        private static void SetUnreadVisibility(UIElement element, int count)
        {
            element.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string FormatTimestamp(long timestamp)
        {
            if (timestamp <= 0)
            {
                return string.Empty;
            }
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static long NormalizeSessionTimestamp(long timestamp)
        {
            return timestamp > 1000000000000 ? timestamp / 1000000 : timestamp;
        }

        private static string NormalizeImageUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }
            return url.StartsWith("//") ? "https:" + url : url.Replace("http://", "https://");
        }

        private static string FirstText(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }
}
