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
    public sealed partial class BasDanmakuControl : UserControl
    {
        private const string HostName = "biliuwp.local";
        private const string HostPage = "https://biliuwp.local/bas-host.html";
        private const int MaxAppendPayloadLength = 48 * 1024;
        private const int MaxChunkPayloadLength = 24 * 1024;
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

        public BasDanmakuControl()
        {
            InitializeComponent();
            SizeChanged += BasDanmakuControl_SizeChanged;
        }

        public event EventHandler<BasDanmakuActionEventArgs> ActionRequested;

        public Task ReplaceAsync(
            IEnumerable<BasDanmakuModel> items,
            double positionSeconds,
            bool shouldPlay,
            bool visible,
            double playbackRate)
        {
            var list = (items ?? Enumerable.Empty<BasDanmakuModel>())
                .Where(item => item != null
                    && !string.IsNullOrWhiteSpace(item.text)
                    && item.stime >= 0)
                .ToList();
            var version = Interlocked.Increment(ref contentVersion);
            return ExecuteCommandAsync(
                version,
                async () =>
                {
                    var safePosition = Math.Max(0, positionSeconds);
                    var safeRate = NormalizeRate(playbackRate);
                    await ExecuteScriptAsync(
                        "window.basHost.reset("
                        + JsonConvert.SerializeObject(safePosition)
                        + ","
                        + JsonConvert.SerializeObject(false)
                        + ","
                        + JsonConvert.SerializeObject(safeRate)
                        + ","
                        + JsonConvert.SerializeObject(visible)
                        + ");");

                    await AppendItemsAsync(list, version);
                    if (version != Volatile.Read(ref contentVersion))
                    {
                        return;
                    }

                    await ExecuteScriptAsync(
                        "window.basHost.setState("
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
                new List<BasDanmakuModel>(),
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
                        "window.basHost.setState("
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
                        "window.basHost.seek("
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
                        "window.basHost.visible("
                        + JsonConvert.SerializeObject(visible)
                        + ");");
                });
        }

        public async Task<bool> TryHandleTapAsync(double normalizedX, double normalizedY)
        {
            if (!IsNormalizedCoordinate(normalizedX)
                || !IsNormalizedCoordinate(normalizedY))
            {
                return false;
            }

            await commandGate.WaitAsync();
            try
            {
                if (!isPageReady || webView.CoreWebView2 == null)
                {
                    return false;
                }

                var result = await webView.CoreWebView2.ExecuteScriptAsync(
                    "window.basHost.handleTap("
                    + JsonConvert.SerializeObject(normalizedX)
                    + ","
                    + JsonConvert.SerializeObject(normalizedY)
                    + ");");
                var value = JToken.Parse(result);
                return value.Type == JTokenType.Boolean && value.Value<bool>();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("处理 BAS 弹幕互动失败", LogType.ERROR, ex);
                return false;
            }
            finally
            {
                commandGate.Release();
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

                if (!await EnsureReadyAsync()
                    || version != Volatile.Read(ref contentVersion))
                {
                    return;
                }

                await command();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("BAS 弹幕渲染失败", LogType.ERROR, ex);
                ShowRendererFailureOnce("BAS 弹幕渲染失败");
            }
            finally
            {
                commandGate.Release();
            }
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
                    LogHelper.WriteLog("BAS 弹幕页面加载失败", LogType.ERROR);
                    ShowRendererFailureOnce("BAS 弹幕渲染器加载失败");
                    return false;
                }

                var readyTask = pageReadyCompletion.Task;
                if (await Task.WhenAny(readyTask, Task.Delay(PageReadyTimeout)) != readyTask)
                {
                    LogHelper.WriteLog("BAS 弹幕页面未在规定时间内就绪", LogType.ERROR);
                    ShowRendererFailureOnce("BAS 弹幕渲染器加载失败");
                    return false;
                }

                if (!await readyTask)
                {
                    LogHelper.WriteLog("BAS 弹幕页面初始化失败", LogType.ERROR);
                    ShowRendererFailureOnce("BAS 弹幕渲染器加载失败");
                    return false;
                }

                isPageReady = true;
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("初始化 BAS 弹幕 WebView2 失败", LogType.ERROR, ex);
                ShowRendererFailureOnce("BAS 弹幕渲染器加载失败");
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
                LogHelper.WriteLog("解析 BAS 弹幕 WebView2 消息失败：" + Truncate(raw), LogType.ERROR, ex);
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
                "BAS 弹幕开始渲染，当前窗口已解析 " + parsedItemCount + " 条",
                LogType.INFO);
        }

        private void HandleRendererError(JObject message)
        {
            var stage = message["stage"]?.ToString();
            var itemId = message["dmid"]?.ToString();
            var detail = message["message"]?.ToString();
            var logMessage = "BAS 弹幕渲染器错误"
                + (string.IsNullOrWhiteSpace(stage) ? string.Empty : "（" + stage + "）")
                + (string.IsNullOrWhiteSpace(itemId) ? string.Empty : "，弹幕 " + itemId)
                + (string.IsNullOrWhiteSpace(detail) ? string.Empty : "：" + Truncate(detail));
            LogHelper.WriteLog(logMessage, LogType.ERROR);
            ShowRendererFailureOnce("BAS 弹幕渲染失败");
        }

        private void HandleActionMessage(JObject message)
        {
            var action = message["action"]?.ToString();
            switch (action)
            {
                case "pause":
                    ActionRequested?.Invoke(
                        this,
                        new BasDanmakuActionEventArgs(BasDanmakuActionKind.Pause));
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
                        new BasDanmakuActionEventArgs(BasDanmakuActionKind.Seek, seconds));
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
                        new BasDanmakuActionEventArgs(BasDanmakuActionKind.Navigate, 0, url));
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
            IList<BasDanmakuModel> items,
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
            builder.Append("window.basHost.append([");
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

            await ExecuteScriptAsync("window.basHost.beginItem();");
            for (var offset = 0; offset < itemJson.Length; offset += MaxChunkPayloadLength)
            {
                if (version != Volatile.Read(ref contentVersion))
                {
                    return;
                }

                var count = Math.Min(MaxChunkPayloadLength, itemJson.Length - offset);
                var chunk = itemJson.Substring(offset, count);
                await ExecuteScriptAsync(
                    "window.basHost.appendItemChunk("
                    + JsonConvert.SerializeObject(chunk)
                    + ");");
            }

            if (version == Volatile.Read(ref contentVersion))
            {
                await ExecuteScriptAsync("window.basHost.endItem();");
            }
        }

        private async void BasDanmakuControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!isPageReady || e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
            {
                return;
            }

            var version = Volatile.Read(ref contentVersion);
            await ExecuteCommandAsync(
                version,
                async () => await ExecuteScriptAsync("window.basHost.resize();"));
        }

        private static double NormalizeRate(double rate)
        {
            return double.IsNaN(rate) || double.IsInfinity(rate) || rate <= 0
                ? 1
                : rate;
        }

        private static bool IsNormalizedCoordinate(double value)
        {
            return !double.IsNaN(value)
                && !double.IsInfinity(value)
                && value >= 0
                && value <= 1;
        }
    }

    public enum BasDanmakuActionKind
    {
        Pause,
        Seek,
        Navigate
    }

    public sealed class BasDanmakuActionEventArgs : EventArgs
    {
        public BasDanmakuActionEventArgs(
            BasDanmakuActionKind action,
            double positionSeconds = 0,
            string url = null)
        {
            Action = action;
            PositionSeconds = positionSeconds;
            Url = url;
        }

        public BasDanmakuActionKind Action { get; }

        public double PositionSeconds { get; }

        public string Url { get; }
    }
}
