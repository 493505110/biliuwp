using BiliBili.UWP.Helper;
using BiliBili.UWP.Modules;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BiliBili.UWP.Pages
{
    public sealed partial class WebPage : Page
    {
        private const string BiliAppBridgeScript = @"
(() => {
    const send = (action, data) => window.chrome.webview.postMessage(JSON.stringify({
        source: 'biliapp',
        action: action,
        data: data == null ? '' : String(data)
    }));
    const alert = message => send('Alert', message);
    const validateLogin = data => send('ValidateLogin', data);
    const closeBrowser = () => send('CloseBrowser', '');
    window.biliapp = {
        Alert: alert,
        alert: alert,
        ValidateLogin: validateLogin,
        validateLogin: validateLogin,
        CloseBrowser: closeBrowser,
        closeBrowser: closeBrowser
    };
})();";

        private bool webViewReady;
        private string bypassNavigationUri;

        public WebPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.NavigationMode != NavigationMode.New || e.Parameter == null)
            {
                return;
            }

            var parameter = e.Parameter is object[] values && values.Length > 0
                ? values[0]?.ToString()
                : e.Parameter.ToString();
            if (!Uri.TryCreate(parameter, UriKind.Absolute, out var uri))
            {
                Utils.ShowMessageToast("无法打开无效的网址");
                return;
            }

            if (await EnsureWebViewAsync())
            {
                //页面会被导航缓存复用，登录或换号后每次进入都要刷新Cookie。
                await WebView2CookieHelper.CopyToWebViewAsync(webView.CoreWebView2);
                webView.CoreWebView2.Navigate(uri.AbsoluteUri);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back)
            {
                if (webView.CoreWebView2 != null)
                {
                    bypassNavigationUri = "about:blank";
                    webView.CoreWebView2.Navigate("about:blank");
                }
                NavigationCacheMode = NavigationCacheMode.Disabled;
            }
            base.OnNavigatedFrom(e);
        }

        private async System.Threading.Tasks.Task<bool> EnsureWebViewAsync()
        {
            if (webViewReady)
            {
                return true;
            }

            try
            {
                await webView.EnsureCoreWebView2Async();
                webView.NavigationStarting += WebView_NavigationStarting;
                webView.NavigationCompleted += WebView_NavigationCompleted;
                webView.WebMessageReceived += WebView_WebMessageReceived;
                webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                webView.CoreWebView2.DOMContentLoaded += (sender, args) =>
                    webview_progressBar.Visibility = Visibility.Collapsed;
                webView.CoreWebView2.DocumentTitleChanged += (sender, args) =>
                    txt_Header.Text = webView.CoreWebView2.DocumentTitle;
                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BiliAppBridgeScript);
                webViewReady = true;
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("WebView2初始化失败", LogType.ERROR, ex);
                Utils.ShowMessageToast("浏览器组件不可用，请安装 WebView2 运行时后重试");
                return false;
            }
        }

        private async void WebView_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            webview_progressBar.Visibility = Visibility.Visible;
            if (args.Uri == bypassNavigationUri)
            {
                bypassNavigationUri = null;
                return;
            }

            if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri))
            {
                args.Cancel = true;
                webview_progressBar.Visibility = Visibility.Collapsed;
                return;
            }

            var isWebUri = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            if (isWebUri && args.NavigationKind != CoreWebView2NavigationKind.NewDocument)
            {
                return;
            }

            args.Cancel = true;
            if (await MessageCenter.HandelUrl(args.Uri))
            {
                webview_progressBar.Visibility = Visibility.Collapsed;
                return;
            }

            if (isWebUri)
            {
                bypassNavigationUri = args.Uri;
                sender.CoreWebView2.Navigate(args.Uri);
                return;
            }

            webview_progressBar.Visibility = Visibility.Collapsed;
            await PromptOpenExternalAsync(uri);
        }

        private async void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = true;
            var deferral = args.GetDeferral();
            try
            {
                if (!await MessageCenter.HandelUrl(args.Uri) &&
                    Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri))
                {
                    await PromptOpenExternalAsync(uri);
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async void WebView_WebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                var message = JObject.Parse(args.TryGetWebMessageAsString());
                if (message.Value<string>("source") != "biliapp")
                {
                    return;
                }

                var data = message.Value<string>("data") ?? string.Empty;
                switch (message.Value<string>("action"))
                {
                    case "Alert":
                        await new MessageDialog(data).ShowAsync();
                        break;
                    case "ValidateLogin":
                        await ValidateLoginAsync(data);
                        break;
                    case "CloseBrowser":
                        if (Frame.CanGoBack)
                        {
                            Frame.GoBack();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("解析WebView2消息失败", LogType.ERROR, ex);
            }
        }

        private async System.Threading.Tasks.Task ValidateLoginAsync(string data)
        {
            try
            {
                var result = JObject.Parse(data);
                if (result["access_token"] == null)
                {
                    Utils.ShowMessageToast("登录失败");
                    return;
                }

                var account = new Account();
                var loginResult = await account.CheckAgainLogin(
                    result.Value<string>("access_token"),
                    result.Value<string>("refresh_token"),
                    result.Value<int>("expires_in"),
                    result.Value<long>("mid"));
                Utils.ShowMessageToast(loginResult.success ? "登录成功" : "登录失败");
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("处理网页登录结果失败", LogType.ERROR, ex);
                Utils.ShowMessageToast("登录失败");
            }
        }

        private static async System.Threading.Tasks.Task PromptOpenExternalAsync(Uri uri)
        {
            var dialog = new MessageDialog("是否调用外部浏览器打开此链接？");
            dialog.Commands.Add(new UICommand("确定", async command =>
                await Windows.System.Launcher.LaunchUriAsync(uri)));
            dialog.Commands.Add(new UICommand("取消"));
            await dialog.ShowAsync();
        }

        private void btn_Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void menu_copy_Click(object sender, RoutedEventArgs e)
        {
            if (webView.Source == null)
            {
                return;
            }
            var package = new DataPackage();
            package.SetText(webView.Source.AbsoluteUri);
            Clipboard.SetContent(package);
            Clipboard.Flush();
        }

        private async void menu_open_Click(object sender, RoutedEventArgs e)
        {
            if (webView.Source != null)
            {
                await Windows.System.Launcher.LaunchUriAsync(webView.Source);
            }
        }

        private void btn_refresh_Click(object sender, RoutedEventArgs e)
        {
            webView.CoreWebView2?.Reload();
        }

        private void btn_WebBack_Click(object sender, RoutedEventArgs e)
        {
            if (webView.CoreWebView2?.CanGoBack == true)
            {
                webView.CoreWebView2.GoBack();
            }
        }

        private void btn_WebRefresh_Click(object sender, RoutedEventArgs e)
        {
            webView.CoreWebView2?.Reload();
        }

        private async void WebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            webview_progressBar.Visibility = Visibility.Collapsed;
            if (!args.IsSuccess || sender.Source == null)
            {
                return;
            }

            try
            {
                await WebView2CookieHelper.CopyToHttpClientAsync(sender.CoreWebView2);
                if (sender.Source.AbsoluteUri.Contains("bilibili.com"))
                {
                    await sender.CoreWebView2.ExecuteScriptAsync(
                        "document.getElementById('internationalHeader')?.remove();" +
                        "document.getElementsByClassName('international-footer')[0]?.remove();");
                }

                if (sender.Source.AbsoluteUri.Contains("23344273.aspx"))
                {
                    var appVersion = SettingHelper.GetVersion();
                    var systemVersion = Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily + " " +
                        SystemHelper.SystemVersion();
                    var script = $"document.getElementById('q2').value={JsonConvert.SerializeObject(appVersion)};" +
                        $"document.getElementById('q3').value={JsonConvert.SerializeObject(systemVersion)};";
                    await sender.CoreWebView2.ExecuteScriptAsync(script);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("WebView2脚本执行失败", LogType.ERROR, ex);
            }
        }
    }
}
