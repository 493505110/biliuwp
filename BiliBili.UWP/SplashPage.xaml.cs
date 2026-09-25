using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.UI;
using Windows.UI.ViewManagement;
using BiliBili.UWP.Helper;
using Windows.UI.Composition;
using Windows.UI.Xaml.Hosting;
using Microsoft.Graphics.Canvas.Effects;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using System.Text.RegularExpressions;
using BiliBili.UWP.Modules;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace BiliBili.UWP
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class SplashPage : Page
    {
        public SplashPage()
        {
            this.InitializeComponent();
            //夜间黑主题下启动页要一起变深色，否则从启动到主界面之间会先闪一下浅色
            bool isDark = string.Equals(SettingHelper.Get_EffectiveTheme(), "Dark", StringComparison.Ordinal);
            //取值与 App.xaml 的 Dark 字典一致：Bili-Background #FF1F1F1F、Bili-ForeColor #FF323232
            var bg = isDark ? Color.FromArgb(255, 31, 31, 31) : new Color() { R = 233, G = 233, B = 233 };
            var fg = isDark ? Colors.White : Colors.Black;
            if (isDark)
            {
                //本页背景用的是 ThemeResource Bili-Background，指定 Dark 后才会解析成深色
                RequestedTheme = ElementTheme.Dark;
            }

            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                // StatusBar.GetForCurrentView().HideAsync();
                StatusBar statusBar = StatusBar.GetForCurrentView();
                statusBar.ForegroundColor = fg;
                statusBar.BackgroundColor = bg;
                statusBar.BackgroundOpacity = 100;
            }

            var titleBar = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TitleBar;
            titleBar.BackgroundColor = bg;
            titleBar.ForegroundColor = fg;//Colors.White纯白用不了。。。
            titleBar.ButtonHoverBackgroundColor = isDark ? Color.FromArgb(255, 50, 50, 50) : Colors.White;
            titleBar.ButtonBackgroundColor = bg;
            titleBar.ButtonForegroundColor = Color.FromArgb(255, 254, 254, 254);
            titleBar.InactiveBackgroundColor = bg;
            titleBar.ButtonInactiveBackgroundColor = bg;
        }
        
        StartModel m;

        //拉图宽限（毫秒）：1 秒等待结束后再给一点，避免「刚好晚到」的图被丢掉
        private const int SplashFetchGraceMs = 1500;

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            //注册后台任务和刷新磁贴都不能阻塞启动，也不能被下面那个 catch 吞掉异常
            _ = RegisterBackgroundTaskAsync();
            _ = RefreshAttentionTileAsync();
            #region
            try
            {
                //读取已下载的文件
                DownloadHelper2.LoadDowned();
                //加载分区
                ApiHelper.SetRegions();
                //加载直播头衔
                LiveRoom.GetTitleItems();
                //ApiHelper.SetEmojis();
            }
            catch (Exception)
            {
            }
            #endregion

            m = e.Parameter as StartModel;

            //只有正常启动（没有指定跳转目标）才准备开屏图；
            //从通知/协议/文件进入时 StartType 不是 None，一律不准备，避免打断跳转
            var splashEnabled = SettingHelper.Get_LoadSplash();
            if (splashEnabled && (m == null || m.StartType == StartTypes.None))
            {
                //拉图与下面那 1 秒等待并发；拿到后交给 MainPage 展示
                var fetching = FetchSplashAsync();
                await Task.Delay(1000);

                //1 秒内没拿到就再给一小段宽限，之后放弃
                var finished = await Task.WhenAny(fetching, Task.Delay(SplashFetchGraceMs));
                MainPage.PendingSplash = finished == fetching ? await fetching : null;
                if (MainPage.PendingSplash == null)
                {
                    LogHelper.WriteLog("启动开屏图：等待窗口内未取到图，跳过展示", LogType.INFO);
                }
            }
            else
            {
                LogHelper.WriteLog($"启动开屏图：未准备（开关={splashEnabled}，StartType={(m == null ? "null" : m.StartType.ToString())}）", LogType.INFO);
                await Task.Delay(1000);
            }

            //Frame 自 Windows 10 1803 起默认使用 NavigationThemeTransition 播放导航动画
            //（Page Refresh = 新页上滑 + 淡入，见 MSDN「a Frame uses NavigationThemeTransition to
            //animate navigation between Pages by default」）。MainPage 的首帧就包含开屏图，
            //若不抑制，整块开屏图会跟着「从下往上滑入」——这正是「开屏图像导航一样移动」的来源。
            this.Frame.Navigate(typeof(MainPage), m, new SuppressNavigationTransitionInfo());
           

        }

        #region 开屏图拉取

        /// <summary>请求开屏品牌图接口，取出本次要展示的图并解码。任何失败都返回 null。</summary>
        private async Task<MainPage.SplashImageItem> FetchSplashAsync()
        {
            try
            {
                var url = ApiHelper.GetSignWithUrl(
                    $"https://app.bilibili.com/x/v2/splash/brand/list?appkey={ApiHelper.AndroidKey.Appkey}&ts={ApiHelper.GetTimeSpan}",
                    ApiHelper.AndroidKey);

                var results = await WebClientClass.GetResultsUTF8Encode(new Uri(url));
                var root = JObject.Parse(results);
                if ((int?)root["code"] != 0)
                {
                    LogHelper.WriteLog($"请求启动开屏图失败：{root["message"]}", LogType.ERROR);
                    return null;
                }

                var data = root["data"];
                var show = data?["show"] as JArray;
                if (show == null || show.Count == 0)
                {
                    //当前没有开屏图投放，属正常情况，直接跳过
                    return null;
                }

                //选取规则抽在 SplashImageSelector 里（纯函数，有单元测试覆盖）
                var id = SplashImageSelector.SelectShowId(show);
                var thumb = SplashImageSelector.SelectThumb(show, data?["list"] as JArray);
                if (string.IsNullOrEmpty(thumb))
                {
                    LogHelper.WriteLog($"启动开屏图：列表中找不到 id={id} 对应的图片", LogType.INFO);
                    return null;
                }

                var image = await LoadSplashBitmapAsync(thumb);
                if (image == null)
                {
                    return null;
                }

                var durationMs = SplashImageSelector.SelectDurationMs(show);
                return new MainPage.SplashImageItem
                {
                    Image = image,
                    DurationMs = durationMs
                };
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("请求启动开屏图异常", LogType.ERROR, ex);
                return null;
            }
        }

        /// <summary>下载并解码开屏图。webp 交给 CDN 转 jpeg，UWP 对 webp 的解码支持不保证。</summary>
        private static async Task<BitmapImage> LoadSplashBitmapAsync(string thumb)
        {
            //webp 交给 CDN 转 jpeg 的规则同样收在 SplashImageSelector 里
            var buffer = await WebClientClass.GetBuffer(new Uri(SplashImageSelector.EnsureJpegUrl(thumb)));
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(buffer.AsStream().AsRandomAccessStream());
            return bitmap;
        }

        #endregion


        #region 后台任务注册

        private async Task RegisterBackgroundTaskAsync()
        {
            var task = await BackgroundTaskRegistrar.SyncAsync();
            if (task != null)
            {
                task.Progress += TaskOnProgress;
                task.Completed += TaskOnCompleted;
            }
        }

        /// <summary>后台任务最快也要 15 分钟才触发一次，启动时先在前台刷一遍磁贴。</summary>
        private async Task RefreshAttentionTileAsync()
        {
            await BackgroundTaskRegistrar.RefreshTileAsync();
        }

        private void TaskOnProgress(BackgroundTaskRegistration sender, BackgroundTaskProgressEventArgs args)
        {
            Debug.WriteLine($"Background {sender.Name} TaskOnProgress.");
        }

        private void TaskOnCompleted(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            Debug.WriteLine($"Background {sender.Name} TaskOnCompleted.");
        }

        #endregion


    }
}
