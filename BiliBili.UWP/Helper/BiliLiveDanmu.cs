using BiliBili.UWP.Api;
using BiliBili.UWP.Api.Live;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// 直播间实时信息流客户端。
    /// </summary>
    public class BiliLiveDanmu : IDisposable
    {
        public enum LiveDanmuTypes
        {
            Viewer,
            Danmu,
            Gift,
            Welcome,
            SystemMsg
        }

        public delegate void HasDanmuHandel(LiveDanmuModel value);
        public event HasDanmuHandel HasDanmu;

        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private StreamSocket _clientSocket;
        private DispatcherTimer _timer;
        private int _sequence = 1;
        private bool _startState;

        public int delay = 100;

        /// <summary>
        /// 使用 getDanmuInfo 返回的节点和 token 连接直播信息流。
        /// </summary>
        public async void Start(int roomid, long userId)
        {
            try
            {
                var connection = await GetDanmuConnectionInfo(roomid);
                if (connection == null || connection.host_list == null || connection.host_list.Count == 0)
                {
                    throw new InvalidOperationException("无法获取直播弹幕服务器");
                }

                foreach (var host in connection.host_list)
                {
                    StreamSocket socket = null;
                    try
                    {
                        socket = new StreamSocket();
                        await socket.ConnectAsync(new HostName(host.host), host.port.ToString());
                        _clientSocket = socket;
                        break;
                    }
                    catch (Exception ex)
                    {
                        socket?.Dispose();
                        LogHelper.WriteLog("连接直播弹幕节点失败: " + host.host, LogType.ERROR, ex);
                    }
                }

                if (_clientSocket == null)
                {
                    throw new InvalidOperationException("无法连接直播弹幕服务器");
                }

                _startState = true;
                // 只有 getDanmuInfo 请求携带了该用户的 SESSDATA 时才能在认证包中传 uid。
                // 仅有旧 access_key 的登录状态应按游客连接，否则服务器会拒绝认证。
                var authUserId = string.IsNullOrEmpty(LiveRoomAPI.GetCookieValue("SESSDATA")) ? 0 : userId;
                await SendJoinChannel(roomid, authUserId, connection.token);
                await SendHeartbeatAsync();

                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
                _timer.Tick += Timer_Tick;
                _timer.Start();

                await Task.Run(() => Listen());
            }
            catch (Exception ex)
            {
                _startState = false;
                LogHelper.WriteLog("启动直播弹幕失败", LogType.ERROR, ex);
                Raise(new LiveDanmuModel { type = LiveDanmuTypes.SystemMsg, value = "直播弹幕连接失败" });
            }
            finally
            {
                if (!_startState)
                {
                    CloseConnection();
                }
            }
        }

        private async Task<DanmuConnectionInfo> GetDanmuConnectionInfo(int roomid)
        {
            var response = await LiveRoomAPI.GetDanmuInfo(roomid).Request();
            if (!response.status)
            {
                return null;
            }

            var root = response.GetJObject();
            if (root == null || root.Value<int?>("code") != 0)
            {
                LogHelper.WriteLog("获取直播弹幕服务器失败: " + (root?["message"]?.ToString() ?? response.message), LogType.ERROR);
                return null;
            }
            return root["data"]?.ToObject<DanmuConnectionInfo>();
        }

        private void Listen()
        {
            try
            {
                using (var stream = _clientSocket.InputStream.AsStreamForRead())
                {
                    var header = new byte[16];
                    while (_startState)
                    {
                        ReadFully(stream, header, 0, header.Length);
                        var packetLength = ReadInt32(header, 0);
                        var headerLength = ReadInt16(header, 4);
                        var version = ReadInt16(header, 6);
                        var operation = ReadInt32(header, 8);

                        if (headerLength < 16 || packetLength < headerLength || packetLength > 16 * 1024 * 1024)
                        {
                            throw new InvalidDataException("直播弹幕数据包长度无效: " + packetLength);
                        }

                        var payload = new byte[packetLength - headerLength];
                        ReadFully(stream, payload, 0, payload.Length);
                        ProcessPacket(version, operation, payload);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (EndOfStreamException)
            {
            }
            catch (Exception ex)
            {
                if (_startState)
                {
                    LogHelper.WriteLog("接收直播弹幕失败", LogType.ERROR, ex);
                    Raise(new LiveDanmuModel { type = LiveDanmuTypes.SystemMsg, value = "直播弹幕连接已断开" });
                }
            }
            finally
            {
                _startState = false;
            }
        }

        private void ProcessPacket(int version, int operation, byte[] payload)
        {
            if (version == 2)
            {
                var data = DecompressZlib(payload);
                if (data != null)
                {
                    ProcessFramedPackets(data);
                }
                return;
            }

            if (version == 3)
            {
                // 客户端认证时请求 protover=2，正常不会收到 Brotli 数据。
                LogHelper.WriteLog("收到不支持的 Brotli 直播弹幕包", LogType.INFO);
                return;
            }

            switch (operation)
            {
                case 3:
                    if (payload.Length >= 4)
                    {
                        var viewer = ReadInt32(payload, 0);
                        // 新版弹幕服务器可能先返回占位值 1，不能用它覆盖房间接口的人气。
                        if (viewer > 1)
                        {
                            Raise(new LiveDanmuModel { type = LiveDanmuTypes.Viewer, viewer = viewer });
                        }
                    }
                    break;
                case 5:
                    ProcessCommand(payload);
                    break;
                case 8:
                    var auth = ParseObject(payload);
                    if (auth != null && auth.Value<int?>("code") != 0)
                    {
                        Raise(new LiveDanmuModel { type = LiveDanmuTypes.SystemMsg, value = "直播弹幕认证失败" });
                    }
                    break;
            }
        }

        private void ProcessFramedPackets(byte[] data)
        {
            var offset = 0;
            while (offset + 16 <= data.Length)
            {
                var packetLength = ReadInt32(data, offset);
                var headerLength = ReadInt16(data, offset + 4);
                if (packetLength < headerLength || headerLength < 16 || offset + packetLength > data.Length)
                {
                    return;
                }

                var version = ReadInt16(data, offset + 6);
                var operation = ReadInt32(data, offset + 8);
                var payload = new byte[packetLength - headerLength];
                System.Buffer.BlockCopy(data, offset + headerLength, payload, 0, payload.Length);
                ProcessPacket(version, operation, payload);
                offset += packetLength;
            }
        }

        private void ProcessCommand(byte[] payload)
        {
            var obj = ParseObject(payload);
            var cmd = obj?["cmd"]?.ToString() ?? string.Empty;
            if (cmd.StartsWith("DANMU_MSG", StringComparison.Ordinal))
            {
                ProcessDanmu(obj);
                return;
            }

            switch (cmd)
            {
                case "SEND_GIFT":
                    var gift = obj["data"];
                    if (gift != null)
                    {
                        Raise(new LiveDanmuModel
                        {
                            type = LiveDanmuTypes.Gift,
                            value = new GiftMsgModel
                            {
                                uname = gift.Value<string>("uname") ?? string.Empty,
                                action = gift.Value<string>("action") ?? "赠送",
                                giftId = gift.Value<int?>("giftId") ?? 0,
                                giftName = gift.Value<string>("giftName") ?? string.Empty,
                                num = gift["num"]?.ToString() ?? "0",
                                uid = gift["uid"]?.ToString() ?? "0"
                            }
                        });
                    }
                    break;
                case "WELCOME":
                case "WELCOME_GUARD":
                case "INTERACT_WORD":
                    ProcessWelcome(obj["data"]);
                    break;
                case "NOTICE_MSG":
                case "SYS_MSG":
                case "WARNING":
                    var message = obj.Value<string>("msg_common") ?? obj.Value<string>("msg") ?? obj["data"]?["msg"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        Raise(new LiveDanmuModel { type = LiveDanmuTypes.SystemMsg, value = message });
                    }
                    break;
            }

            if (delay > 0)
            {
                Thread.Sleep(delay);
            }
        }

        private void ProcessDanmu(JObject obj)
        {
            var info = obj["info"] as JArray;
            if (info == null || info.Count < 3)
            {
                return;
            }

            var user = info[2] as JArray;
            var medal = info.Count > 3 ? info[3] as JArray : null;
            var level = info.Count > 4 ? info[4] as JArray : null;
            var title = info.Count > 5 ? info[5] as JArray : null;
            var model = new DanmuMsgModel
            {
                text = info[1]?.ToString() ?? string.Empty,
                username = ArrayString(user, 1) + ":",
                ul = level != null && level.Count > 0 ? "UL" + ArrayString(level, 0) : string.Empty,
                ulColor = ArrayString(level, 2),
                medal_lv = ArrayString(medal, 0),
                medal_name = ArrayString(medal, 1),
                medalColor = ArrayString(medal, 4),
                user_title = ArrayString(title, 0)
            };

            if (user != null)
            {
                model.isAdmin = ArrayInt(user, 2) == 1 ? Visibility.Visible : Visibility.Collapsed;
                model.isVip = ArrayInt(user, 3) == 1 ? Visibility.Visible : Visibility.Collapsed;
                model.isBigVip = ArrayInt(user, 4) == 1 ? Visibility.Visible : Visibility.Collapsed;
            }
            model.hasMedal = medal != null && medal.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            model.hasTitle = !string.IsNullOrEmpty(model.user_title) ? Visibility.Visible : Visibility.Collapsed;

            Raise(new LiveDanmuModel { type = LiveDanmuTypes.Danmu, value = model });
        }

        private void ProcessWelcome(JToken data)
        {
            if (data == null)
            {
                return;
            }
            Raise(new LiveDanmuModel
            {
                type = LiveDanmuTypes.Welcome,
                value = new WelcomeMsgModel
                {
                    uname = data.Value<string>("uname") ?? string.Empty,
                    uid = data["uid"]?.ToString() ?? "0",
                    svip = (data.Value<int?>("svip") ?? data.Value<int?>("vip") ?? 0) == 1
                }
            });
        }

        private async void Timer_Tick(object sender, object e)
        {
            await SendHeartbeatAsync();
        }

        private Task SendHeartbeatAsync()
        {
            return SendSocketData(2);
        }

        private Task SendJoinChannel(int channelId, long userId, string token)
        {
            var body = JsonConvert.SerializeObject(new
            {
                uid = Math.Max(0, userId),
                roomid = channelId,
                protover = 2,
                buvid = LiveRoomAPI.GetBuvid3(),
                platform = "web",
                type = 2,
                key = token ?? string.Empty
            });
            return SendSocketData(7, body);
        }

        private async Task SendSocketData(int operation, string body = "")
        {
            if (_clientSocket == null)
            {
                return;
            }

            await _sendLock.WaitAsync();
            try
            {
                var payload = Encoding.UTF8.GetBytes(body ?? string.Empty);
                var buffer = new byte[payload.Length + 16];
                WriteInt32(buffer, 0, buffer.Length);
                WriteInt16(buffer, 4, 16);
                WriteInt16(buffer, 6, 1);
                WriteInt32(buffer, 8, operation);
                WriteInt32(buffer, 12, Interlocked.Increment(ref _sequence));
                System.Buffer.BlockCopy(payload, 0, buffer, 16, payload.Length);

                using (var writer = new DataWriter(_clientSocket.OutputStream))
                {
                    writer.WriteBytes(buffer);
                    await writer.StoreAsync();
                    writer.DetachStream();
                }
            }
            catch (Exception ex)
            {
                if (_startState)
                {
                    LogHelper.WriteLog("发送直播弹幕协议包失败", LogType.ERROR, ex);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private static byte[] DecompressZlib(byte[] payload)
        {
            if (payload == null || payload.Length <= 6)
            {
                return null;
            }
            try
            {
                using (var input = new MemoryStream(payload, 2, payload.Length - 6))
                using (var inflater = new DeflateStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream())
                {
                    inflater.CopyTo(output);
                    return output.ToArray();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("解压直播弹幕失败", LogType.ERROR, ex);
                return null;
            }
        }

        private static JObject ParseObject(byte[] payload)
        {
            try
            {
                return JObject.Parse(Encoding.UTF8.GetString(payload, 0, payload.Length).TrimEnd('\0'));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void ReadFully(Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                var read = stream.Read(buffer, offset, count);
                if (read <= 0)
                {
                    throw new EndOfStreamException();
                }
                offset += read;
                count -= read;
            }
        }

        private static int ReadInt32(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3];
        }

        private static int ReadInt16(byte[] buffer, int offset)
        {
            return (buffer[offset] << 8) | buffer[offset + 1];
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static void WriteInt16(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;
        }

        private static string ArrayString(JArray array, int index)
        {
            return array != null && index >= 0 && index < array.Count ? array[index]?.ToString() ?? string.Empty : string.Empty;
        }

        private static int ArrayInt(JArray array, int index)
        {
            int value;
            return int.TryParse(ArrayString(array, index), out value) ? value : 0;
        }

        private void Raise(LiveDanmuModel model)
        {
            HasDanmu?.Invoke(model);
        }

        public void Dispose()
        {
            _startState = false;
            CloseConnection();
        }

        private void CloseConnection()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
                _timer = null;
            }
            if (_clientSocket != null)
            {
                _clientSocket.Dispose();
                _clientSocket = null;
            }
        }

        private class DanmuConnectionInfo
        {
            public string token { get; set; }
            public System.Collections.Generic.List<DanmuHostInfo> host_list { get; set; }
        }

        private class DanmuHostInfo
        {
            public string host { get; set; }
            public int port { get; set; }
        }

        public class LiveDanmuModel
        {
            public LiveDanmuTypes type { get; set; }
            public int viewer { get; set; }
            public object value { get; set; }
        }

        public class DanmuMsgModel
        {
            public string text { get; set; }
            public string username { get; set; }
            public string ul { get; set; }
            public string ulColor { get; set; }
            public SolidColorBrush ul_color { get; set; }
            public string user_title { get; set; }
            public string vip { get; set; }
            public string medal_name { get; set; }
            public string medal_lv { get; set; }
            public string medalColor { get; set; }
            public SolidColorBrush medal_color { get; set; }
            public Visibility isAdmin { get; set; } = Visibility.Collapsed;
            public Visibility isVip { get; set; } = Visibility.Collapsed;
            public Visibility isBigVip { get; set; } = Visibility.Collapsed;
            public Visibility hasMedal { get; set; } = Visibility.Collapsed;
            public Visibility hasTitle { get; set; } = Visibility.Collapsed;
            public Visibility hasUL { get; set; } = Visibility.Visible;
            public string titleImg
            {
                get
                {
                    return Modules.LiveRoom.titleItems?.Find(x => x.identification == user_title)?.web_pic_url;
                }
            }
            public SolidColorBrush uname_color { get; set; }
            public SolidColorBrush content_color { get; set; }
        }

        public class GiftMsgModel
        {
            public string uname { get; set; }
            public string giftName { get; set; }
            public string action { get; set; }
            public string num { get; set; }
            public int giftId { get; set; }
            public string uid { get; set; }
        }

        public class WelcomeMsgModel
        {
            public string uname { get; set; }
            public string isadmin { get; set; }
            public string uid { get; set; }
            public bool svip { get; set; }
        }
    }
}
