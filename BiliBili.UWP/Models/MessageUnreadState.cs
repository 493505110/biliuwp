using System;

namespace BiliBili.UWP.Models
{
    /// <summary>
    /// 消息未读状态共享缓存：主页消息红点与消息中心共用同一份数据，
    /// 避免两处各自拉取 API 导致状态不一致（如读完消息后主页红点不消失）。
    /// 本地清零后短时间内忽略 API 旧值，规避 B站未读接口的缓存延迟。
    /// </summary>
    public static class MessageUnreadState
    {
        public static MessageFeedUnreadModel Feed { get; private set; } = new MessageFeedUnreadModel();
        public static MessagePrivateUnreadModel Private { get; private set; } = new MessagePrivateUnreadModel();
        public static MessageGroupUnreadModel Group { get; private set; } = new MessageGroupUnreadModel();

        private static DateTimeOffset _replyClearedAt = DateTimeOffset.MinValue;
        private static DateTimeOffset _atClearedAt = DateTimeOffset.MinValue;
        private static DateTimeOffset _likeClearedAt = DateTimeOffset.MinValue;
        private static DateTimeOffset _noticeClearedAt = DateTimeOffset.MinValue;
        private static DateTimeOffset _privateClearedAt = DateTimeOffset.MinValue;
        private static DateTimeOffset _groupClearedAt = DateTimeOffset.MinValue;

        // B站未读接口缓存延迟窗口；窗口内 API 返回的旧未读数会被忽略
        private const double ClearWindowSeconds = 60;

        public static bool HasUnread()
        {
            return Feed.recv_reply > 0
                || Feed.at > 0
                || Feed.recv_like > 0
                || Feed.sys_msg > 0
                || Private.Total > 0
                || Group.unread_count > 0;
        }

        public static void ClearReply()
        {
            Feed.recv_reply = 0;
            _replyClearedAt = DateTimeOffset.Now;
        }

        public static void ClearAt()
        {
            Feed.at = 0;
            _atClearedAt = DateTimeOffset.Now;
        }

        public static void ClearLike()
        {
            Feed.recv_like = 0;
            _likeClearedAt = DateTimeOffset.Now;
        }

        public static void ClearNotice()
        {
            Feed.sys_msg = 0;
            _noticeClearedAt = DateTimeOffset.Now;
        }

        public static void ClearPrivate()
        {
            Private = new MessagePrivateUnreadModel();
            Group = new MessageGroupUnreadModel();
            _privateClearedAt = DateTimeOffset.Now;
            _groupClearedAt = DateTimeOffset.Now;
        }

        public static void ClearAll()
        {
            Feed = new MessageFeedUnreadModel();
            ClearPrivate();
            _replyClearedAt = DateTimeOffset.Now;
            _atClearedAt = DateTimeOffset.Now;
            _likeClearedAt = DateTimeOffset.Now;
            _noticeClearedAt = DateTimeOffset.Now;
        }

        /// <summary>
        /// 未登录等场景直接清空数据，不动清零标记（避免影响后续 API 合并）。
        /// </summary>
        public static void Reset()
        {
            Feed = new MessageFeedUnreadModel();
            Private = new MessagePrivateUnreadModel();
            Group = new MessageGroupUnreadModel();
        }

        /// <summary>
        /// 合并 API 拉取的未读数据；传入 null 表示该类别不更新。
        /// 本地清零窗口内的类别，API 返回的旧值会被忽略（视为缓存延迟）。
        /// </summary>
        public static void MergeFromApi(MessageFeedUnreadModel feed, MessagePrivateUnreadModel privateUnread, MessageGroupUnreadModel groupUnread)
        {
            var now = DateTimeOffset.Now;
            if (feed != null)
            {
                if ((now - _replyClearedAt).TotalSeconds < ClearWindowSeconds) feed.recv_reply = 0;
                if ((now - _atClearedAt).TotalSeconds < ClearWindowSeconds) feed.at = 0;
                if ((now - _likeClearedAt).TotalSeconds < ClearWindowSeconds) feed.recv_like = 0;
                if ((now - _noticeClearedAt).TotalSeconds < ClearWindowSeconds) feed.sys_msg = 0;
                Feed = feed;
            }
            if (privateUnread != null)
            {
                if ((now - _privateClearedAt).TotalSeconds < ClearWindowSeconds)
                {
                    privateUnread = new MessagePrivateUnreadModel();
                }
                Private = privateUnread;
            }
            if (groupUnread != null)
            {
                if ((now - _groupClearedAt).TotalSeconds < ClearWindowSeconds)
                {
                    groupUnread = new MessageGroupUnreadModel();
                }
                Group = groupUnread;
            }
        }
    }
}
