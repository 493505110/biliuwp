using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Models;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace BiliBili.UWP.Controls
{
    /// <summary>
    /// 脚本弹幕渲染宿主。承载 WebView2 + <c>Assets/script-danmaku-host.html</c>，
    /// 把 <see cref="ScriptDanmakuModel"/> 注入宿主运行时，由宿主按播放时间调度绘制。
    /// 与 BAS（mode9）控件并列独立，互不影响。
    /// </summary>
    public sealed partial class ScriptDanmakuControl : UserControl
    {
        private const string HostName = "biliuwp.local";
        private const string HostPage = "https://biliuwp.local/script-danmaku-host.html";
        private const int MaxAppendPayloadLength = 48 * 1024;
        private const int MaxChunkPayloadLength = 24 * 1024;
        // appendComments 单次调用的外壳（不含具体条目），用于按字节预算切分。
        private const string CommentBatchPrefix = "window.scriptDanmakuHost.appendComments([";
        private const string CommentBatchSuffix = "]); ";
        private static readonly TimeSpan PageReadyTimeout = TimeSpan.FromSeconds(10);

        private readonly SemaphoreSlim commandGate = new SemaphoreSlim(1, 1);
        private Task<bool> initializationTask;
        private TaskCompletionSource<bool> navigationCompletion;
        private TaskCompletionSource<bool> pageReadyCompletion;
        private int contentVersion;
        private bool isPageReady;
        private bool rendererFailureNotified;
        private int parsedItemCount;
        private bool hasRenderedItem;
        private int pendingItemCount;
        // 最近一次推入的弹幕快照。保留它是为了在 reset（换视频 / 重新推脚本）之后
        // 重新投递：脚本可能是后于弹幕池加载的，那时第一次推送已被懒初始化闸门丢掉。
        private readonly List<ScriptDanmakuComment> danmakuSnapshot =
            new List<ScriptDanmakuComment>();
        // 上一次投递的列表实例与条数，用于跳过「池子没变」的重复投递。
        private IList<ScriptDanmakuComment> lastPushedComments;
        private int lastPushedCommentCount;

        public ScriptDanmakuControl()
        {
            InitializeComponent();
            SizeChanged += ScriptDanmakuControl_SizeChanged;
        }

        /// <summary>宿主侧脚本请求播放器执行动作（暂停 / 播放 / 定位 / 导航）。</summary>
        public event EventHandler<ScriptDanmakuActionEventArgs> ActionRequested;

        public Task ReplaceAsync(
            IEnumerable<ScriptDanmakuModel> items,
            double positionSeconds,
            bool shouldPlay,
            bool visible,
            double playbackRate)
        {
            var list = ScriptDanmakuService.Normalize(items).ToList();
            var version = Interlocked.Increment(ref contentVersion);
            // 脚本集合本身是懒初始化的依据：清空后不再需要 WebView2。
            Volatile.Write(ref pendingItemCount, list.Count);
            return ExecuteCommandAsync(
                version,
                async () =>
                {
                    var safePosition = Math.Max(0, positionSeconds);
                    var safeRate = NormalizeRate(playbackRate);
                    await ExecuteScriptAsync(
                        "window.scriptDanmakuHost.reset("
                        + JsonConvert.SerializeObject(safePosition)
                        + ","
                        + JsonConvert.SerializeObject(false)
                        + ","
                        + JsonConvert.SerializeObject(safeRate)
                        + ","
                        + JsonConvert.SerializeObject(visible)
                        + ");");

                    // reset 会清空宿主侧的 commentList，这里把保留的快照补回去，
                    // 保证「先加载弹幕池、后加载脚本」的顺序下脚本仍读得到数据。
                    await PushDanmakuSnapshotAsync(version);

                    await AppendItemsAsync(list, version);
                    if (version != Volatile.Read(ref contentVersion))
                    {
                        return;
                    }

                    await ExecuteScriptAsync(
                        "window.scriptDanmakuHost.setState("
                        + JsonConvert.SerializeObject(safePosition)
                        + ","
                        + JsonConvert.SerializeObject(shouldPlay)
                        + ","
                        + JsonConvert.SerializeObject(safeRate)
                        + ");");
                });
        }

        public Task ClearAsync()
        {
            return ReplaceAsync(
                new List<ScriptDanmakuModel>(),
                0,
                false,
                false,
                1);
        }

        public Task SetPlaybackStateAsync(
            double positionSeconds,
            bool shouldPlay,
            double playbackRate)
        {
            var version = Volatile.Read(ref contentVersion);
            return ExecuteCommandAsync(
                version,
                async () =>
                {
                    await ExecuteScriptAsync(
                        "window.scriptDanmakuHost.setState("
                        + JsonConvert.SerializeObject(Math.Max(0, positionSeconds))
                        + ","
                        + JsonConvert.SerializeObject(shouldPlay)
                        + ","
                        + JsonConvert.SerializeObject(NormalizeRate(playbackRate))
                        + ");");
                });
        }

        public Task SeekAsync(
            double positionSeconds,
            bool shouldPlay,
            double playbackRate)
        {
            var version = Volatile.Read(ref contentVersion);
            return ExecuteCommandAsync(
                version,
                async () =>
                {
                    await ExecuteScriptAsync(
                        "window.scriptDanmakuHost.seek("
                        + JsonConvert.SerializeObject(Math.Max(0, positionSeconds))
                        + ","
                        + JsonConvert.SerializeObject(shouldPlay)
                        + ","
                        + JsonConvert.SerializeObject(NormalizeRate(playbackRate))
                        + ");");
                });
        }

        public Task SetVisibleAsync(bool visible)
        {
            var version = Volatile.Read(ref contentVersion);
            return ExecuteCommandAsync(
                version,
                async () =>
                {
                    await ExecuteScriptAsync(
                        "window.scriptDanmakuHost.visible("
                        + JsonConvert.SerializeObject(visible)
                        + ");");
                });
        }

        /// <summary>
        /// 推入弹幕快照（脚本侧的 <c>Player.commentList</c>）。会整体替换上一次的快照，
        /// 并在后续 <see cref="ReplaceAsync"/> 的 reset 之后自动重投。
        /// 没有任何脚本时经懒初始化闸门直接返回，不创建 WebView2（零开销）。
        /// </summary>
        public Task PushDanmakuBatchAsync(IEnumerable<ScriptDanmakuComment> comments)
        {
            var list = comments as IList<ScriptDanmakuComment> ?? comments?.ToList();
            // 分页加载弹幕会反复走 SetDanmakuPool，池子没变时不必再把整批推一遍。
            // 用「同一列表实例 + 条数不变」判定：AppendDanmakuPool 是就地追加，
            // 条数会变；换集 / 换视频则是新实例。
            if (list != null && ReferenceEquals(list, lastPushedComments) && list.Count == lastPushedCommentCount)
            {
                return Task.CompletedTask;
            }

            lastPushedComments = list;
            lastPushedCommentCount = list?.Count ?? 0;

            danmakuSnapshot.Clear();
            if (list != null)
            {
                danmakuSnapshot.AddRange(list.Where(item => item != null));
            }

            var version = Volatile.Read(ref contentVersion);
            return ExecuteCommandAsync(
                version,
                async () => await PushDanmakuSnapshotAsync(version));
        }

        /// <summary>
        /// 通知宿主「用户刚发送了一条弹幕」，投递给脚本的 <c>Player.commentTrigger</c> 回调。
        /// 只负责转发，不改变脚本弹幕自身的渲染。
        /// </summary>
        public Task PushSentCommentAsync(ScriptDanmakuComment comment)
        {
            if (comment == null)
            {
                return Task.CompletedTask;
            }

            var version = Volatile.Read(ref contentVersion);
            return ExecuteCommandAsync(
                version,
                async () => await ExecuteScriptAsync(
                    "window.scriptDanmakuHost.pushComment("
                    + JsonConvert.SerializeObject(comment)
                    + ");"));
        }

        /// <summary>
        /// 把按键事件转发给脚本的 <c>Player.keyTrigger</c>。
        /// <paramref name="keyCode"/> 用 Windows.System.VirtualKey 的整数值——
        /// M8 允许监听的那组键（方向键 / Home / End / PgUp / PgDn / W A S D /
        /// 小键盘 0-9）与 DOM/Flash 的 keyCode 同值，因此可以直接透传，宿主侧筛选。
        /// </summary>
        public Task PushKeyEventAsync(int keyCode, bool isKeyUp)
        {
            var version = Volatile.Read(ref contentVersion);
            return ExecuteCommandAsync(
                version,
                async () => await ExecuteScriptAsync(
                    "window.scriptDanmakuHost.pushKey("
                    + keyCode.ToString(CultureInfo.InvariantCulture)
                    + ","
                    + (isKeyUp ? "true" : "false")
                    + ");"));
        }

        /// <summary>
        /// 把快照推给宿主：先 resetComments 清空，再按 <see cref="MaxChunkPayloadLength"/>
        /// 的字节预算分批 appendComments（与 append / beginItem 的分块上限同一套口径）。
        /// 按预算而不是按固定条数切分：单条弹幕的长度可以差一个量级，
        /// 固定条数在长弹幕上会突破上限、在短弹幕上又切得过碎。
        /// </summary>
        private async Task PushDanmakuSnapshotAsync(int version)
        {
            if (version != Volatile.Read(ref contentVersion))
            {
                return;
            }

            await ExecuteScriptAsync("window.scriptDanmakuHost.resetComments();");

            var builder = new StringBuilder(MaxChunkPayloadLength + 64);
            builder.Append(CommentBatchPrefix);
            var batched = 0;
            foreach (var comment in danmakuSnapshot)
            {
                var json = JsonConvert.SerializeObject(comment);
                // 单条就超过预算时也照发：宁可一次大载荷，也不能丢弹幕。
                if (batched > 0
                    && builder.Length + json.Length + CommentBatchSuffix.Length > MaxChunkPayloadLength)
                {
                    if (version != Volatile.Read(ref contentVersion))
                    {
                        return;
                    }

                    builder.Append(CommentBatchSuffix);
                    await ExecuteScriptAsync(builder.ToString());
                    builder.Clear();
                    builder.Append(CommentBatchPrefix);
                    batched = 0;
                }

                if (batched != 0)
                {
                    builder.Append(',');
                }

                builder.Append(json);
                batched++;
            }

            if (batched > 0)
            {
                builder.Append(CommentBatchSuffix);
                await ExecuteScriptAsync(builder.ToString());
            }
        }

        private async Task ExecuteCommandAsync(int version, Func<Task> command)
        {
            await commandGate.WaitAsync();
            try
            {
                if (version != Volatile.Read(ref contentVersion))
                {
                    return;
                }

                if (!HasContentOrVisibility())
                {
                    return;
                }

                if (!await EnsureReadyAsync()
                    || version != Volatile.Read(ref contentVersion))
                {
                    return;
                }

                await command();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("脚本弹幕渲染失败", LogType.ERROR, ex);
                ShowRendererFailureOnce("脚本弹幕渲染失败");
            }
            finally
            {
                commandGate.Release();
            }
        }

        /// <summary>
        /// 懒初始化闸门：没有任何脚本时不创建 WebView2。
        /// 已经初始化过的实例不再走这条捷径，避免 <c>SetVisibleAsync</c> 被吞掉。
        /// </summary>
        private bool HasContentOrVisibility()
        {
            return isPageReady
                || initializationTask != null
                || Volatile.Read(ref pendingItemCount) > 0;
        }

        private async Task<bool> EnsureReadyAsync()
        {
            if (initializationTask == null)
            {
                initializationTask = InitializeAsync();
            }

            return await initializationTask;
        }

        private async Task<bool> InitializeAsync()
        {
            try
            {
                await webView.EnsureCoreWebView2Async();
                var assetsPath = Path.Combine(
                    Package.Current.InstalledLocation.Path,
                    "Assets");
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    HostName,
                    assetsPath,
                    CoreWebView2HostResourceAccessKind.Allow);

                isPageReady = false;
                navigationCompletion = new TaskCompletionSource<bool>();
                pageReadyCompletion = new TaskCompletionSource<bool>();
                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.WebMessageReceived += WebView_WebMessageReceived;
                webView.CoreWebView2.Navigate(HostPage);
                if (!await navigationCompletion.Task)
                {
                    LogHelper.WriteLog("脚本弹幕页面加载失败", LogType.ERROR);
                    ShowRendererFailureOnce("脚本弹幕渲染器加载失败");
                    return false;
                }

                var readyTask = pageReadyCompletion.Task;
                if (await Task.WhenAny(readyTask, Task.Delay(PageReadyTimeout)) != readyTask)
                {
                    LogHelper.WriteLog("脚本弹幕页面未在规定时间内就绪", LogType.ERROR);
                    ShowRendererFailureOnce("脚本弹幕渲染器加载失败");
                    return false;
                }

                if (!await readyTask)
                {
                    LogHelper.WriteLog("脚本弹幕页面初始化失败", LogType.ERROR);
                    ShowRendererFailureOnce("脚本弹幕渲染器加载失败");
                    return false;
                }

                isPageReady = true;
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("初始化脚本弹幕 WebView2 失败", LogType.ERROR, ex);
                ShowRendererFailureOnce("脚本弹幕渲染器加载失败");
                return false;
            }
        }

        private void WebView_NavigationCompleted(
            Microsoft.UI.Xaml.Controls.WebView2 sender,
            CoreWebView2NavigationCompletedEventArgs args)
        {
            if (navigationCompletion == null)
            {
                return;
            }

            if (args.IsSuccess)
            {
                navigationCompletion.TrySetResult(true);
            }
            else
            {
                navigationCompletion.TrySetResult(false);
                pageReadyCompletion?.TrySetResult(false);
            }
        }

        private void WebView_WebMessageReceived(
            Microsoft.UI.Xaml.Controls.WebView2 sender,
            CoreWebView2WebMessageReceivedEventArgs args)
        {
            string raw;
            try
            {
                raw = args.TryGetWebMessageAsString();
            }
            catch (Exception)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            try
            {
                var message = JObject.Parse(raw);
                var type = message["type"]?.ToString();
                switch (type)
                {
                    case "ready":
                        pageReadyCompletion?.TrySetResult(true);
                        break;
                    case "parsed":
                        HandleParsedMessage(message);
                        break;
                    case "rendered":
                        HandleRenderedMessage();
                        break;
                    case "action":
                        HandleActionMessage(message);
                        break;
                    case "error":
                        HandleRendererError(message);
                        if (pageReadyCompletion != null
                            && !pageReadyCompletion.Task.IsCompleted)
                        {
                            pageReadyCompletion.TrySetResult(false);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog(
                    "解析脚本弹幕 WebView2 消息失败：" + Truncate(raw),
                    LogType.ERROR,
                    ex);
            }
        }

        private void HandleParsedMessage(JObject message)
        {
            int count;
            if (!int.TryParse(message["count"]?.ToString(), out count))
            {
                count = 0;
            }

            if (count <= 0)
            {
                parsedItemCount = 0;
                hasRenderedItem = false;
                return;
            }

            parsedItemCount = Math.Max(parsedItemCount, count);
        }

        private void HandleRenderedMessage()
        {
            if (hasRenderedItem)
            {
                return;
            }

            hasRenderedItem = true;
            LogHelper.WriteLog(
                "脚本弹幕开始渲染，当前窗口已解析 " + parsedItemCount + " 条",
                LogType.INFO);
        }

        private void HandleRendererError(JObject message)
        {
            var stage = message["stage"]?.ToString();
            var itemId = message["itemId"]?.ToString();
            var detail = message["message"]?.ToString();
            var logMessage = "脚本弹幕渲染器错误"
                + (string.IsNullOrWhiteSpace(stage) ? string.Empty : "（" + stage + "）")
                + (string.IsNullOrWhiteSpace(itemId) ? string.Empty : "，脚本 " + itemId)
                + (string.IsNullOrWhiteSpace(detail) ? string.Empty : "：" + Truncate(detail));
            LogHelper.WriteLog(logMessage, LogType.ERROR);
            ShowRendererFailureOnce("脚本弹幕渲染失败");
        }

        private void HandleActionMessage(JObject message)
        {
            var action = message["action"]?.ToString();
            switch (action)
            {
                case "pause":
                    ActionRequested?.Invoke(
                        this,
                        new ScriptDanmakuActionEventArgs(ScriptDanmakuActionKind.Pause));
                    break;
                case "play":
                    ActionRequested?.Invoke(
                        this,
                        new ScriptDanmakuActionEventArgs(ScriptDanmakuActionKind.Play));
                    break;
                case "seek":
                {
                    double seconds;
                    if (!double.TryParse(
                        message["seconds"]?.ToString(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out seconds)
                        || double.IsNaN(seconds)
                        || double.IsInfinity(seconds)
                        || seconds < 0)
                    {
                        return;
                    }

                    ActionRequested?.Invoke(
                        this,
                        new ScriptDanmakuActionEventArgs(
                            ScriptDanmakuActionKind.Seek,
                            seconds));
                    break;
                }
                case "navigate":
                {
                    var url = message["url"]?.ToString();
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        return;
                    }

                    ActionRequested?.Invoke(
                        this,
                        new ScriptDanmakuActionEventArgs(
                            ScriptDanmakuActionKind.Navigate,
                            0,
                            url));
                    break;
                }
            }
        }

        private void ShowRendererFailureOnce(string message)
        {
            if (rendererFailureNotified)
            {
                return;
            }

            rendererFailureNotified = true;
            Utils.ShowMessageToast(message, 3000);
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 500)
            {
                return value;
            }

            return value.Substring(0, 500) + "...";
        }

        private async Task ExecuteScriptAsync(string script)
        {
            if (!isPageReady || webView.CoreWebView2 == null)
            {
                return;
            }

            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }

        private async Task AppendItemsAsync(
            IList<ScriptDanmakuModel> items,
            int version)
        {
            var batch = new List<string>();
            var batchLength = 2;

            foreach (var item in items)
            {
                if (version != Volatile.Read(ref contentVersion))
                {
                    return;
                }

                var itemJson = JsonConvert.SerializeObject(item);
                if (itemJson.Length > MaxAppendPayloadLength)
                {
                    await FlushBatchAsync(batch, version);
                    batch.Clear();
                    batchLength = 2;
                    await AppendLargeItemAsync(itemJson, version);
                    continue;
                }

                var separatorLength = batch.Count == 0 ? 0 : 1;
                if (batch.Count != 0
                    && batchLength + separatorLength + itemJson.Length > MaxAppendPayloadLength)
                {
                    await FlushBatchAsync(batch, version);
                    batch.Clear();
                    batchLength = 2;
                    separatorLength = 0;
                }

                batch.Add(itemJson);
                batchLength += separatorLength + itemJson.Length;
            }

            await FlushBatchAsync(batch, version);
        }

        private async Task FlushBatchAsync(
            IList<string> batch,
            int version)
        {
            if (batch == null || batch.Count == 0
                || version != Volatile.Read(ref contentVersion))
            {
                return;
            }

            var builder = new StringBuilder(MaxAppendPayloadLength + 64);
            builder.Append("window.scriptDanmakuHost.append([");
            for (var index = 0; index < batch.Count; index++)
            {
                if (index != 0)
                {
                    builder.Append(',');
                }

                builder.Append(batch[index]);
            }

            builder.Append("]); ");
            await ExecuteScriptAsync(builder.ToString());
        }

        private async Task AppendLargeItemAsync(
            string itemJson,
            int version)
        {
            if (string.IsNullOrEmpty(itemJson)
                || version != Volatile.Read(ref contentVersion))
            {
                return;
            }

            await ExecuteScriptAsync("window.scriptDanmakuHost.beginItem();");
            for (var offset = 0; offset < itemJson.Length; offset += MaxChunkPayloadLength)
            {
                if (version != Volatile.Read(ref contentVersion))
                {
                    return;
                }

                var count = Math.Min(MaxChunkPayloadLength, itemJson.Length - offset);
                var chunk = itemJson.Substring(offset, count);
                await ExecuteScriptAsync(
                    "window.scriptDanmakuHost.appendItemChunk("
                    + JsonConvert.SerializeObject(chunk)
                    + ");");
            }

            if (version == Volatile.Read(ref contentVersion))
            {
                await ExecuteScriptAsync("window.scriptDanmakuHost.endItem();");
            }
        }

        private async void ScriptDanmakuControl_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            if (!isPageReady || e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
            {
                return;
            }

            var version = Volatile.Read(ref contentVersion);
            await ExecuteCommandAsync(
                version,
                async () => await ExecuteScriptAsync("window.scriptDanmakuHost.resize();"));
        }

        private static double NormalizeRate(double rate)
        {
            return double.IsNaN(rate) || double.IsInfinity(rate) || rate <= 0
                ? 1
                : rate;
        }
    }

    public enum ScriptDanmakuActionKind
    {
        Pause,
        Play,
        Seek,
        Navigate
    }

    public sealed class ScriptDanmakuActionEventArgs : EventArgs
    {
        public ScriptDanmakuActionEventArgs(
            ScriptDanmakuActionKind action,
            double positionSeconds = 0,
            string url = null)
        {
            Action = action;
            PositionSeconds = positionSeconds;
            Url = url;
        }

        public ScriptDanmakuActionKind Action { get; }

        public double PositionSeconds { get; }

        public string Url { get; }
    }
}
