using System;
using System.Collections.Generic;

namespace BiliBili.UWP.Api.User
{
    public class MessageAPI
    {
        private static IDictionary<string, string> WebHeaders()
        {
            return new Dictionary<string, string>()
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36" },
                { "Referer", "https://message.bilibili.com/" }
            };
        }

        public ApiModel UnreadFeed()
        {
            return Get(
                "https://api.vc.bilibili.com/x/im/web/msgfeed/unread",
                "build=0&mobi_app=web");
        }

        public ApiModel ReplyFeed(long id = 0, long time = 0)
        {
            var parameter = "build=0&mobi_app=web&platform=web";
            if (id > 0 && time > 0)
            {
                parameter += $"&id={id}&reply_time={time}";
            }
            return Get("https://api.bilibili.com/x/msgfeed/reply", parameter);
        }

        public ApiModel AtFeed(long id = 0, long time = 0)
        {
            var parameter = "build=0&mobi_app=web&platform=web";
            if (id > 0 && time > 0)
            {
                parameter += $"&id={id}&at_time={time}";
            }
            return Get("https://api.bilibili.com/x/msgfeed/at", parameter);
        }

        public ApiModel LikeFeed(long id = 0, long time = 0)
        {
            var parameter = "build=0&mobi_app=web&platform=web";
            if (id > 0 && time > 0)
            {
                parameter += $"&id={id}&like_time={time}";
            }
            return Get("https://api.bilibili.com/x/msgfeed/like", parameter);
        }

        public ApiModel SystemNotices(long cursor = 0, int pageSize = 40)
        {
            var parameter = $"build=0&mobi_app=web&page_size={pageSize}";
            if (cursor > 0)
            {
                parameter += $"&cursor={cursor}";
            }
            return Get("https://message.bilibili.com/x/sys-msg/query_notify_list", parameter);
        }

        public ApiModel PrivateUnread()
        {
            return Get(
                "https://api.vc.bilibili.com/session_svr/v1/session_svr/single_unread",
                "build=0&mobi_app=web&show_unfollow_list=1&show_dustbin=1&unread_type=0");
        }

        public ApiModel GroupUnread()
        {
            return Get(
                "https://api.vc.bilibili.com/session_svr/v1/session_svr/my_group_unread",
                "build=0&mobi_app=web");
        }

        public ApiModel Sessions(long endTimestamp = 0, int size = 100)
        {
            var parameter = $"build=0&group_fold=0&mobi_app=web&session_type=4&size={size}&sort_rule=2&unfollow_fold=0";
            if (endTimestamp > 0)
            {
                parameter += $"&end_ts={endTimestamp}";
            }
            return Get("https://api.vc.bilibili.com/session_svr/v1/session_svr/get_sessions", parameter);
        }

        public ApiModel SessionMessages(long talkerId, int sessionType, int size = 100)
        {
            return Get(
                "https://api.vc.bilibili.com/svr_sync/v1/svr_sync/fetch_session_msgs",
                $"build=0&mobi_app=web&sender_device_id=1&session_type={sessionType}&size={size}&talker_id={talkerId}");
        }

        public ApiModel UserCard(long mid)
        {
            return Get(
                "https://api.bilibili.com/x/web-interface/card",
                $"mid={mid}&photo=true");
        }

        public ApiModel UserCards(IEnumerable<long> mids)
        {
            var value = string.Join(",", mids ?? new long[0]);
            return Get(
                "https://api.vc.bilibili.com/account/v1/user/cards",
                $"uids={Uri.EscapeDataString(value)}");
        }

        public ApiModel SendMessage(long senderId, long receiverId, int receiverType, string content, string deviceId, string csrf)
        {
            var escapedDeviceId = Uri.EscapeDataString(deviceId ?? string.Empty);
            var escapedCsrf = Uri.EscapeDataString(csrf ?? string.Empty);
            var messageJson = Newtonsoft.Json.JsonConvert.SerializeObject(new { content = content ?? string.Empty });
            return new ApiModel()
            {
                method = HttpMethod.POST,
                baseUrl = "https://api.vc.bilibili.com/web_im/v1/web_im/send_msg",
                parameter = $"w_dev_id={escapedDeviceId}&w_receiver_id={receiverId}&w_sender_uid={senderId}",
                body = $"msg%5Bsender_uid%5D={senderId}"
                    + $"&msg%5Breceiver_id%5D={receiverId}"
                    + $"&msg%5Breceiver_type%5D={receiverType}"
                    + "&msg%5Bmsg_type%5D=1&msg%5Bmsg_status%5D=0"
                    + $"&msg%5Bdev_id%5D={escapedDeviceId}"
                    + $"&msg%5Btimestamp%5D={ApiHelper.GetTimeSpan}"
                    + "&msg%5Bnew_face_version%5D=1"
                    + $"&msg%5Bcontent%5D={Uri.EscapeDataString(messageJson)}"
                    + $"&csrf={escapedCsrf}&csrf_token={escapedCsrf}"
                    + "&build=0&mobi_app=web",
                headers = WebHeaders(),
                useWbi = true
            };
        }

        public ApiModel MarkSessionRead(long talkerId, int sessionType, long sequenceNumber, string csrf)
        {
            var escapedCsrf = Uri.EscapeDataString(csrf ?? string.Empty);
            return new ApiModel()
            {
                method = HttpMethod.POST,
                baseUrl = "https://api.vc.bilibili.com/session_svr/v1/session_svr/update_ack",
                parameter = string.Empty,
                body = $"talker_id={talkerId}&session_type={sessionType}&ack_seqno={sequenceNumber}"
                    + $"&csrf={escapedCsrf}&csrf_token={escapedCsrf}&build=0&mobi_app=web",
                headers = WebHeaders()
            };
        }

        private static ApiModel Get(string baseUrl, string parameter)
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = baseUrl,
                parameter = parameter,
                headers = WebHeaders()
            };
        }
    }
}
