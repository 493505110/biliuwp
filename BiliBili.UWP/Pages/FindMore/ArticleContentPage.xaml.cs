using BiliBili.UWP.Helper;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace BiliBili.UWP.Pages.FindMore
{
    public sealed partial class ArticleContentPage : Page
    {
        private bool webViewReady;
        private string bypassNavigationUri;

        public ArticleContentPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter == null)
            {
                return;
            }

            var parameter = e.Parameter is object[] values && values.Length > 0
                ? values[0]?.ToString()
                : e.Parameter.ToString();
            var url = parameter;
            if (!string.IsNullOrEmpty(url) && !url.Contains("bilibili.com"))
            {
                url = "https://www.bilibili.com/read/cv" + url;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                Utils.ShowMessageToast("无法打开无效的专栏地址");
                return;
            }

            if (await EnsureWebViewAsync())
            {
                web.Source = uri;
            }
        }

        private async System.Threading.Tasks.Task<bool> EnsureWebViewAsync()
        {
            if (webViewReady)
            {
                return true;
            }

            try
            {
                await web.EnsureCoreWebView2Async();
                web.NavigationStarting += Web_NavigationStarting;
                web.NavigationCompleted += Web_NavigationCompleted;
                web.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                await WebView2CookieHelper.CopyToWebViewAsync(web.CoreWebView2);
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

        private async void Web_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            txt_Header.Text = "专栏";
            pr_Load.Visibility = Visibility.Visible;
            if (args.Uri == bypassNavigationUri)
            {
                bypassNavigationUri = null;
                return;
            }

            if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri))
            {
                args.Cancel = true;
                pr_Load.Visibility = Visibility.Collapsed;
                return;
            }

            var isWebUri = uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
            if (isWebUri && args.Uri.Contains("read/"))
            {
                return;
            }
            if (isWebUri && args.NavigationKind != CoreWebView2NavigationKind.NewDocument)
            {
                return;
            }

            args.Cancel = true;
            if (await MessageCenter.HandelUrl(args.Uri))
            {
                pr_Load.Visibility = Visibility.Collapsed;
                return;
            }

            if (isWebUri)
            {
                bypassNavigationUri = args.Uri;
                sender.CoreWebView2.Navigate(args.Uri);
                return;
            }

            pr_Load.Visibility = Visibility.Collapsed;
            Utils.ShowMessageToast("不支持打开的链接" + args.Uri);
        }

        private async void Web_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            pr_Load.Visibility = Visibility.Collapsed;
            if (!args.IsSuccess || sender.Source == null)
            {
                return;
            }

            txt_Header.Text = sender.CoreWebView2.DocumentTitle;
            await WebView2CookieHelper.CopyToHttpClientAsync(sender.CoreWebView2);
            if (!sender.Source.AbsoluteUri.Contains("read/app") &&
                !sender.Source.AbsoluteUri.Contains("read/mobile"))
            {
                return;
            }

            try
            {
                const string script = @"
['h5-download-bar', 'bili-nav-bar', 'top-holder'].forEach(name => {
    const element = document.getElementsByClassName(name)[0];
    if (element) element.style.display = 'none';
});";
                await sender.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("专栏页面脚本执行失败", LogType.ERROR, ex);
            }
        }

        private async void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = true;
            var deferral = args.GetDeferral();
            try
            {
                if (await MessageCenter.HandelUrl(args.Uri))
                {
                    return;
                }
                if (!Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri))
                {
                    return;
                }

                var dialog = new MessageDialog("是否调用外部浏览器打开此链接？");
                dialog.Commands.Add(new UICommand("确定", async command =>
                    await Windows.System.Launcher.LaunchUriAsync(uri)));
                dialog.Commands.Add(new UICommand("取消"));
                await dialog.ShowAsync();
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void btn_Back_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void btn_Share_Click(object sender, RoutedEventArgs e)
        {
            if (web.Source == null)
            {
                return;
            }
            var package = new DataPackage();
            package.SetText(web.Source.AbsoluteUri);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            Utils.ShowMessageToast("已将地址复制到剪切板", 3000);
        }
    }
}
