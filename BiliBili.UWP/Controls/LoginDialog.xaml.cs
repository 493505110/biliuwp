using BiliBili.UWP.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using Windows.UI.Xaml.Media.Imaging;
using BiliBili.UWP.Helper;
using System.Text.RegularExpressions;
using BiliBili.UWP.Modules.AccountModels;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Web.WebView2.Core;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“内容对话框”项模板

namespace BiliBili.UWP.Controls
{
    public sealed partial class LoginDialog : ContentDialog
    {
        Account account;
        /// <summary>
        /// webView当前用途，NavigationCompleted据此分流
        /// </summary>
        enum LoginMode
        {
            None,
            /// <summary>极验人机验证(本地页面)</summary>
            Geetest,
            /// <summary>网页登录</summary>
            Web,
            /// <summary>网页登录后的授权确认</summary>
            WebConfirm,
            /// <summary>登录后的安全验证</summary>
            Validate
        }
        LoginMode mode = LoginMode.None;
        /// <summary>极验完成的等待，由geetest.html经postMessage回调完成</summary>
        TaskCompletionSource<GeetestValidateModel> geetestWaiter;
        CaptchaInfoModel captchaInfo;
        bool webViewReady = false;

        public LoginDialog()
        {
            this.InitializeComponent();
            account = new Account();
        }

        protected async override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            await GetQRAuthInfo();
        }

        /// <summary>
        /// 按需初始化WebView2。扫码登录用不到它，故不在构造时初始化。
        /// 返回false表示WebView2运行时不可用
        /// </summary>
        private async Task<bool> EnsureWebView()
        {
            if (webViewReady)
            {
                return true;
            }
            try
            {
                await webView.EnsureCoreWebView2Async();
                //把Assets映射成https源，本地页才能正常加载极验的外部脚本
                var assetsPath = System.IO.Path.Combine(
                    Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets");
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "biliuwp.local", assetsPath, CoreWebView2HostResourceAccessKind.Allow);
                webView.NavigationStarting += webView_NavigationStarting;
                webView.NavigationCompleted += webView_NavigationCompleted;
                webView.WebMessageReceived += webView_WebMessageReceived;
                //注销时由 UserManage.Logout() 调用，清 WebView2 自己的 cookie 存储
                WebView2CookieHelper.Register(webView.CoreWebView2);
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

        /// <summary>
        /// geetest.html通过chrome.webview.postMessage回传结果
        /// </summary>
        private void webView_WebMessageReceived(Microsoft.UI.Xaml.Controls.WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            string raw = null;
            try
            {
                raw = args.TryGetWebMessageAsString();
            }
            catch (Exception)
            {
                //非字符串消息，忽略
                return;
            }
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }
            try
            {
                var obj = JObject.Parse(raw);
                var type = obj["type"]?.ToString();
                if (type == "geetest_result")
                {
                    var m = new GeetestValidateModel()
                    {
                        validate = obj["validate"]?.ToString(),
                        seccode = obj["seccode"]?.ToString()
                    };
                    if (geetestWaiter != null && !geetestWaiter.Task.IsCompleted)
                    {
                        geetestWaiter.TrySetResult(m);
                    }
                }
                else if (type == "geetest_error")
                {
                    var msg = obj["message"]?.ToString();
                    Utils.ShowMessageToast(string.IsNullOrEmpty(msg) ? "验证失败" : msg);
                    if (geetestWaiter != null && !geetestWaiter.Task.IsCompleted)
                    {
                        geetestWaiter.TrySetResult(null);
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("解析WebView2消息失败：" + raw, LogType.ERROR, ex);
                if (geetestWaiter != null && !geetestWaiter.Task.IsCompleted)
                {
                    geetestWaiter.TrySetResult(null);
                }
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            if (txt_Username.Text.Length == 0)
            {
                txt_Username.Focus(FocusState.Pointer);
                Utils.ShowMessageToast("请输入用户名");
                return;
            }
            if (txt_Password.Password.Length == 0)
            {
                txt_Password.Focus(FocusState.Pointer);
                Utils.ShowMessageToast("请输入密码");
                return;
            }
            if (chatcha.Visibility == Visibility.Visible && txt_captcha.Text.Length == 0)
            {
                txt_Password.Focus(FocusState.Pointer);
                Utils.ShowMessageToast("请输入验证码");
                return;
            }
            IsPrimaryButtonEnabled = false;

            //先做人机验证，拿到validate才能调登录接口
            var validate = await DoGeetest();
            if (validate == null)
            {
                IsPrimaryButtonEnabled = true;
                return;
            }

            Title = "登录中";
            var results = await account.WebPasswordLogin(txt_Username.Text, txt_Password.Password,
                captchaInfo.token, captchaInfo.geetest.challenge, validate.validate);
            switch (results.status)
            {
                case Modules.LoginStatus.Success:
                    this.Hide();
                    break;
                case Modules.LoginStatus.Fail:
                case Modules.LoginStatus.Error:
                case Modules.LoginStatus.NeedCaptcha:
                    Title = "登录";
                    IsPrimaryButtonEnabled = true;
                    break;
                case Modules.LoginStatus.NeedValidate:
                    if (string.IsNullOrEmpty(results.url))
                    {
                        Title = "登录";
                        IsPrimaryButtonEnabled = true;
                        Utils.ShowMessageToast("需要安全验证，请改用扫码登录");
                        break;
                    }
                    //安全验证(如异地登录验证手机号)交给网页完成。
                    //登录过程的cookie在WinRT一侧，先回写给WebView2
                    await WebView2CookieHelper.CopyToWebViewAsync(webView.CoreWebView2);
                    Title = "安全验证";
                    mode = LoginMode.Validate;
                    pwdLogin.Visibility = Visibility.Collapsed;
                    webView.Visibility = Visibility.Visible;
                    webView.Width = 480;
                    webView.Height = 600;
                    webView.Source = new Uri(results.url.Replace("&ticket=1", ""));
                    break;
                default:
                    break;
            }
            if (!string.IsNullOrEmpty(results.message))
            {
                Utils.ShowMessageToast(results.message);
            }
        }

        /// <summary>
        /// 申请captcha并在WebView里完成极验，返回null表示未通过
        /// </summary>
        private async Task<GeetestValidateModel> DoGeetest()
        {
            if (!await EnsureWebView())
            {
                return null;
            }
            Title = "安全验证";
            var info = await account.GetCaptchaInfo();
            if (!info.success)
            {
                Title = "登录";
                Utils.ShowMessageToast(info.message);
                return null;
            }
            captchaInfo = info.data;

            mode = LoginMode.Geetest;
            geetestWaiter = new TaskCompletionSource<GeetestValidateModel>();
            webView.Visibility = Visibility.Visible;
            //极验embed面板展开后较高，给足高度避免出现滚动条
            webView.Width = 420;
            webView.Height = 520;
            //经虚拟主机映射加载，参数在NavigationCompleted里注入
            webView.Source = new Uri("https://biliuwp.local/geetest.html");

            //90秒没完成就当放弃，避免永久挂住
            var finished = await Task.WhenAny(geetestWaiter.Task, Task.Delay(90000));
            webView.Visibility = Visibility.Collapsed;
            mode = LoginMode.None;
            if (finished != geetestWaiter.Task)
            {
                Title = "登录";
                Utils.ShowMessageToast("验证超时，请重试");
                return null;
            }
            var result = geetestWaiter.Task.Result;
            if (result == null || string.IsNullOrEmpty(result.validate))
            {
                Title = "登录";
                return null;
            }
            return result;
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void txt_Password_GotFocus(object sender, RoutedEventArgs e)
        {
            hide.Visibility = Visibility.Visible;
        }
        private void txt_Password_LostFocus(object sender, RoutedEventArgs e)
        {
            hide.Visibility = Visibility.Collapsed;
        }

        private void Image_Tapped(object sender, TappedRoutedEventArgs e)
        {
            GetCaptcha();
        }
        private async void GetCaptcha()
        {
            try
            {
                var m = await WebClientClass.GetBuffer(new Uri("https://passport.bilibili.com/captcha?ts=" + ApiHelper.GetTimeSpan));
                var steam = m.AsStream();
                var img = new BitmapImage();
                await img.SetSourceAsync(steam.AsRandomAccessStream());
                img_Captcha.Source = img;
            }
            catch (Exception)
            {
                Utils.ShowMessageToast("无法加载验证码");
            }


        }

        private async void webView_NavigationStarting(Microsoft.UI.Xaml.Controls.WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            //旧式第三方授权回跳会把access_key带在url上
            var uri = args.Uri ?? "";
            if (uri.Contains("access_key="))
            {
                args.Cancel = true;
                var access = Regex.Match(uri, "access_key=(.*?)&").Groups[1].Value;
                var mid = Regex.Match(uri, "mid=(.*?)&").Groups[1].Value;
                await account.SetLoginSuccess(access, mid);
                this.Hide();
            }
        }

        /// <summary>
        /// 开始轮询授权状态，扫码与网页登录手动确认都用它
        /// </summary>
        private void StartQRTimer()
        {
            StopQRTimer();
            timer = new Timer();
            timer.Interval = 3000;
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        /// <summary>
        /// 离开扫码界面时停掉轮询，避免后台无效请求
        /// </summary>
        private void StopQRTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Dispose();
                timer = null;
            }
        }

        private async void BtnWebLogin_Click(object sender, RoutedEventArgs e)
        {
            if (!await EnsureWebView())
            {
                return;
            }
            Title = "网页登录";
            StopQRTimer();
            mode = LoginMode.Web;
            pwdLogin.Visibility = Visibility.Collapsed;
            qrLogin.Visibility = Visibility.Collapsed;
            webView.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = false;
            webView.Width = 480;
            webView.Height = 600;
            //用B站自己的登录页，人机验证由页面自行处理
            webView.Source = new Uri("https://passport.bilibili.com/login");
        }

        private async void webView_NavigationCompleted(Microsoft.UI.Xaml.Controls.WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            //本地极验页加载完，把gt/challenge注入进去
            if (mode == LoginMode.Geetest)
            {
                if (!args.IsSuccess || captchaInfo == null || captchaInfo.geetest == null)
                {
                    return;
                }
                try
                {
                    //参数用JSON序列化以避免转义问题
                    var gt = Newtonsoft.Json.JsonConvert.SerializeObject(captchaInfo.geetest.gt);
                    var challenge = Newtonsoft.Json.JsonConvert.SerializeObject(captchaInfo.geetest.challenge);
                    await webView.CoreWebView2.ExecuteScriptAsync($"initFromHost({gt},{challenge})");
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("注入极验参数失败", LogType.ERROR, ex);
                    Utils.ShowMessageToast("加载验证码失败");
                    if (geetestWaiter != null && !geetestWaiter.Task.IsCompleted)
                    {
                        geetestWaiter.TrySetResult(null);
                    }
                }
                return;
            }

            if (mode == LoginMode.Web || mode == LoginMode.Validate)
            {
                if (!args.IsSuccess)
                {
                    return;
                }
                var host = "";
                var path = "";
                try
                {
                    var u = sender.Source;
                    if (u != null)
                    {
                        host = u.Host;
                        path = u.AbsolutePath;
                    }
                }
                catch (Exception)
                {
                    return;
                }
                //离开登录/验证页且cookie里已有DedeUserID，说明网页侧已登录成功
                bool stillOnLoginPage = host.Contains("passport.bilibili.com") &&
                    (path.StartsWith("/login") || path.Contains("/h5-app/passport"));
                if (!stillOnLoginPage && await WebView2CookieHelper.GetCookieAsync(webView.CoreWebView2, "DedeUserID") != "")
                {
                    await FinishWebLogin();
                }
                return;
            }
        }

        /// <summary>
        /// 网页已登录，把cookie换成App要的access_key
        /// </summary>
        private async Task FinishWebLogin()
        {
            if (mode == LoginMode.WebConfirm)
            {
                return;
            }
            mode = LoginMode.WebConfirm;
            Title = "正在完成登录";
            webView.Visibility = Visibility.Collapsed;
            //WebView2与HttpClient的cookie存储是分开的，先搬过去
            await WebView2CookieHelper.CopyToHttpClientAsync(webView.CoreWebView2);
            var result = await account.CookieToAccessKey();
            if (result.status == Modules.LoginStatus.Success)
            {
                Utils.ShowMessageToast("登录成功");
                this.Hide();
                return;
            }
            //自动确认失败时，退回让用户在页面里手动点确认
            var authResult = await account.GetQRAuthInfo();
            if (authResult.success)
            {
                authInfo = authResult.data;
                Title = "确认登录";
                webView.Visibility = Visibility.Visible;
                webView.Width = 480;
                webView.Height = 600;
                webView.Source = new Uri(authInfo.url);
                StartQRTimer();
                Utils.ShowMessageToast("请在页面中点击确认登录");
                return;
            }

            mode = LoginMode.None;
            Title = "登录";
            pwdLogin.Visibility = Visibility.Collapsed;
            qrLogin.Visibility = Visibility.Visible;
            webView.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = true;
            Utils.ShowMessageToast(string.IsNullOrEmpty(result.message) ? "登录失败，请重试" : result.message);
            await GetQRAuthInfo();
        }

        private async void btnQRLogin_Click(object sender, RoutedEventArgs e)
        {
            mode = LoginMode.None;
            Title = "登录";
            pwdLogin.Visibility = Visibility.Collapsed;
            qrLogin.Visibility = Visibility.Visible;
            webView.Visibility = Visibility.Collapsed;
            await GetQRAuthInfo();
        }
        bool qr_loading = false;
        QRAuthInfo authInfo;
        Timer timer;
        private async Task GetQRAuthInfo()
        {
            try
            {
                qr_loading = true;
                StopQRTimer();
                var result = await account.GetQRAuthInfo();
                if (result.success)
                {
                    authInfo = result.data;
                    ZXing.BarcodeWriter barcodeWriter = new ZXing.BarcodeWriter();
                    barcodeWriter.Format = ZXing.BarcodeFormat.QR_CODE;
                    barcodeWriter.Options = new ZXing.Common.EncodingOptions()
                    {
                        Margin = 1,
                        Height = 200,
                        Width = 200
                    };
                    var img = barcodeWriter.Write(authInfo.url);
                    imgQR.Source = img;
                    StartQRTimer();
                }
                else
                {
                    Utils.ShowMessageToast(result.message);
                }
                qr_loading = false;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("读取和加载登录二维码失败", LogType.ERROR, ex);
                Utils.ShowMessageToast("加载二维码失败");
            }

        }

        private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {

            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
               async () =>
                {
                    var result = await account.PollQRAuthInfo(authInfo.auth_code);
                    if (result.status == Modules.LoginStatus.Success)
                    {
                        StopQRTimer();
                        this.Hide();
                    }
                });

        }

        private void btnPasswordLogin_Click(object sender, RoutedEventArgs e)
        {
            StopQRTimer();
            mode = LoginMode.None;
            Title = "登录";
            pwdLogin.Visibility = Visibility.Visible;
            qrLogin.Visibility = Visibility.Collapsed;
            webView.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = true;
        }

        private async void btnRefreshQR_Click(object sender, RoutedEventArgs e)
        {
            if (qr_loading)
                return;
            await GetQRAuthInfo();
        }
    }
}
