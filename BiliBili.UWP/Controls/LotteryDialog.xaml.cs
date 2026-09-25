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

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“内容对话框”项模板

namespace BiliBili.UWP.Controls
{
    public sealed partial class LotteryDialog : ContentDialog
    {
        /// <summary>WebView2 是否已初始化完成，决定关闭时要不要 Close</summary>
        private bool webViewReady;
        /// <summary>弹窗是否已关闭。初始化是异步的，关闭可能发生在初始化途中</summary>
        private bool isClosed;

        public LotteryDialog(string id)
        {
            this.InitializeComponent();
            Opened += async (sender, args) =>
            {
                try
                {
                    await web.EnsureCoreWebView2Async();
                    //初始化期间弹窗可能已被关闭，此时 Closed 里的 CloseWebView 还拦不到
                    if (isClosed)
                    {
                        webViewReady = true;
                        CloseWebView();
                        return;
                    }
                    await Helper.WebView2CookieHelper.CopyToWebViewAsync(web.CoreWebView2);
                    web.Source = new Uri($"https://t.bilibili.com/lottery/h5/index/#/result?business_id={id}&business_type=1&isWeb=1");
                    webViewReady = true;
                }
                catch (Exception ex)
                {
                    Helper.LogHelper.WriteLog("WebView2初始化失败", Helper.LogType.ERROR, ex);
                    BiliBili.UWP.Utils.ShowMessageToast("浏览器组件不可用，请安装 WebView2 运行时后重试");
                }
            };
            Closed += (sender, args) =>
            {
                isClosed = true;
                //弹窗关闭后 WebView2 仍在，只 Hide 不 Close 会让 Chromium 进程一直留着
                CloseWebView();
            };
        }

        /// <summary>
        /// 释放抽奖页的 WebView2。ContentViewDialog 关闭不会自动释放它。
        /// </summary>
        private void CloseWebView()
        {
            if (!webViewReady)
            {
                return;
            }
            webViewReady = false;
            try
            {
                web.Close();
            }
            catch (Exception ex)
            {
                Helper.LogHelper.WriteLog("关闭抽奖页WebView2失败", Helper.LogType.ERROR, ex);
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
