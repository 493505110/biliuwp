using BiliBili.UWP.Controls;
using BiliBili.UWP.Api;
using BiliBili.UWP.Api.User;
using BiliBili.UWP.Controls;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using BiliBili.UWP.Pages;
using BiliBili.UWP.Pages.FindMore;
using BiliBili.UWP.Pages.Home;
using BiliBili.UWP.Pages.Music;
using BiliBili.UWP.Pages.User;
using BiliBili.UWP.Views;
using Microsoft.Graphics.Canvas.Effects;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Text;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using static BiliBili.UWP.Helper.MusicHelper;

//“空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 上有介绍

namespace BiliBili.UWP
{
    public enum StartTypes
    {
        None,
        Video,
        Live,
        Bangumi,
        MiniVideo,
        Web,
        File,
        Article,
        Music,
        Album,
        User,
        HandleUri
    }
    public class StartModel
    {
        public StartTypes StartType { get; set; }
        public string Par1 { get; set; }
        public string Par2 { get; set; }
        public object Par3 { get; set; }
    }


    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>SplashPage 拉到的开屏图；本页在 OnNavigatedTo 里消费一次后立即清空，
        /// 避免后续再导航到本页时重复播放。</summary>
        public static SplashImageItem PendingSplash;

        //开屏图展示时长的下限/上限（毫秒）：服务端 duration 常为 1000，直接照搬会一闪而过
        private const int SplashMinShowMs = 2500;
        private const int SplashMaxShowMs = 5000;
        //淡入与上滑离场的时长（毫秒）
        private const int SplashFadeInMs = 500;
        private const int SplashSlideOutMs = 420;

        //点击跳过用：开屏图展示期间被点击则提前结束停留
        private TaskCompletionSource<bool> _splashSkip;

        //开屏图播放过程（含滑出动画）。更新日志等首帧弹层要等它结束再弹，
        //否则 ContentDialog 会盖在还没滑走的开屏图上，看起来像「动画没结束就弹出来了」
        private Task _splashTask;


        public MainPage()
        {
            this.InitializeComponent();
            MusicHelper.InitializeMusicPlay();
            music.Visibility = Visibility.Collapsed;
            MusicHelper.MediaChanged += MusicHelper_MediaChanged;
            MusicHelper.DisplayEvent += MusicHelper_DisplayEvent;
            MusicHelper.UpdateList += MusicHelper_UpdateList1;
            //ls_music.ItemsSource = MusicHelper.playList;
            musicplayer.SetMediaPlayer(MusicHelper._mediaPlayer);
            MessageCenter.NetworkError += MessageCenter_NetworkError;
            MessageCenter.ShowError += MessageCenter_ShowError;
            SystemNavigationManager.GetForCurrentView().BackRequested += MainPage_BackRequested;
            DisplayInformation.GetForCurrentView().OrientationChanged += MainPage_OrientationChanged;
            Window.Current.Content.PointerPressed += MainPage_PointerEntered;
            //注销时要清 WebView2 存储，但登录弹窗里的 WebView2 活不到那时，
            //故由主页面临时借出一个用完即弃的载体（WebView2 每实例都会拉起一组渲染进程，不留常驻）
            WebView2CookieHelper.CleanupHostProvider = AcquireCleanupWebViewAsync;
            WebView2CookieHelper.CleanupHostReleaser = ReleaseCleanupWebView;


        }

        private Microsoft.UI.Xaml.Controls.WebView2 cleanupWebView;

        /// <summary>
        /// 借出一个未显示的 WebView2 作为清理载体。首次调用会拉起 Chromium 渲染进程，
        /// 因此每次注销只在需要时创建，用完立刻由 <see cref="ReleaseCleanupWebView"/> 释放。
        /// </summary>
        private async Task<Microsoft.Web.WebView2.Core.CoreWebView2> AcquireCleanupWebViewAsync()
        {
            if (cleanupWebView == null)
            {
                cleanupWebView = new Microsoft.UI.Xaml.Controls.WebView2();
                //放进可视树才能初始化；Visible 且尺寸为 0，不占布局也不接收输入
                cleanupWebView.Width = 0;
                cleanupWebView.Height = 0;
                cleanupWebView.IsHitTestVisible = false;
                cleanupWebView.HorizontalAlignment = HorizontalAlignment.Left;
                cleanupWebView.VerticalAlignment = VerticalAlignment.Top;
                RootPanel.Children.Add(cleanupWebView);
            }
            await cleanupWebView.EnsureCoreWebView2Async();
            return cleanupWebView.CoreWebView2;
        }

        /// <summary>
        /// 归还清理载体：关闭 CoreWebView2 并移出可视树，避免渲染进程常驻。
        /// </summary>
        private void ReleaseCleanupWebView()
        {
            if (cleanupWebView == null)
            {
                return;
            }
            try
            {
                cleanupWebView.Close();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("关闭WebView2清理载体失败", LogType.ERROR, ex);
            }
            RootPanel.Children.Remove(cleanupWebView);
            cleanupWebView = null;
        }
      

        private void MessageCenter_ShowError(object sender, Exception e)
        {
            //弹窗体验太差
            //try
            //{
            //    //ErrorDialog errorDialog = new ErrorDialog(e);
            //    //await errorDialog.ShowAsync();
            //}
            //catch (Exception)
            //{
            //}
            
        }

        private void MessageCenter_NetworkError(object sender, string e)
        {
            network_error.Visibility = Visibility.Visible;
        }

        private async void MusicHelper_UpdateList1(object sender, List<MusicPlayModel> e)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //btn_MediaList.Flyout.ShowAt(btn_MediaList);
                isSetMusic = true;

                //ls_music.ItemsSource =e; 设置ItemsSource会出现bug
                ls_music.Items.Clear();
                foreach (var item in e)
                {
                    ls_music.Items.Add(item);
                }
                ls_music.SelectedIndex = MusicHelper._mediaPlaybackList.CurrentItemIndex.ToInt32();
                ls_music.UpdateLayout();
                isSetMusic = false;
                //btn_MediaList.Flyout.Hide();
            });
        }


        private async void MusicHelper_DisplayEvent(object sender, Visibility e)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                music.Visibility = e;
            });
        }

        private async void MusicHelper_MediaChanged(object sender, MusicPlayModel e)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                isSetMusic = true;
                btn_showMusicInfo.Tag = e.songid;
                music_img.Source = new BitmapImage(new Uri(e.pic));
                txt_musicInfo.Text = e.title + " - " + e.artist;
                ls_music.SelectedIndex = MusicHelper._mediaPlaybackList.CurrentItemIndex.ToInt32();
                isSetMusic = false;
            });

        }

        private async void MainPage_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var par = e.GetCurrentPoint(sender as Frame).Properties.PointerUpdateKind;
            if (par == Windows.UI.Input.PointerUpdateKind.XButton1Pressed || par == Windows.UI.Input.PointerUpdateKind.MiddleButtonPressed)
            {
                if (!SettingHelper.Get_MouseBack())
                {
                    return;
                }
                if (play_frame.CanGoBack)
                {
                    e.Handled = true;
                    play_frame.GoBack();
                }
                else
                {
                    if (frame.CanGoBack)
                    {
                        e.Handled = true;
                        frame.GoBack();
                    }
                    else
                    {
                        if (_InBangumi)
                        {
                            e.Handled = true;
                            main_frame.GoBack();
                        }
                        else
                        {
                            if (e.Handled == false)
                            {
                                if (IsClicks)
                                {
                                    Application.Current.Exit();
                                }
                                else
                                {
                                    IsClicks = true;
                                    e.Handled = true;
                                    Utils.ShowMessageToast("再按一次退出应用", 1500);
                                    await Task.Delay(1500);
                                    IsClicks = false;
                                }
                            }
                        }

                    }
                }




            }

        }


        private async void MainPage_OrientationChanged(DisplayInformation sender, object args)
        {

            if (sender.CurrentOrientation == DisplayOrientations.Landscape || sender.CurrentOrientation == DisplayOrientations.LandscapeFlipped || sender.CurrentOrientation == (DisplayOrientations)5)
            {
                if (SettingHelper.Get_HideStatus())
                {
                    if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent(typeof(StatusBar).ToString()))
                    {
                        StatusBar statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                        await statusBar.HideAsync();
                    }
                }

            }
            else
            {
                if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent(typeof(StatusBar).ToString()))
                {
                    StatusBar statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
                    await statusBar.ShowAsync();
                }
            }
        }
        Account account;
        bool IsClicks = false;
        private async void MainPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (play_frame.CanGoBack)
            {
                e.Handled = true;
                play_frame.GoBack();
            }
            else
            {
                if (frame.CanGoBack)
                {
                    e.Handled = true;
                    frame.GoBack();
                }
                else
                {
                    if (_InBangumi)
                    {
                        e.Handled = true;
                        main_frame.GoBack();
                    }
                    else
                    {
                        if (e.Handled == false)
                        {
                            if (IsClicks)
                            {
                                Application.Current.Exit();
                            }
                            else
                            {
                                IsClicks = true;
                                e.Handled = true;
                                Utils.ShowMessageToast("再按一次退出应用", 1500);
                                await Task.Delay(1500);
                                IsClicks = false;
                            }
                        }
                    }

                }
            }
        }
       
        DispatcherTimer timer;
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            //开屏图要尽早铺上：在首帧渲染前就显示，才不会先闪出主界面
            ConsumePendingSplash();

            if (SettingHelper.IsPc())
            {
                sp_View.DisplayMode = SplitViewDisplayMode.CompactOverlay;
            }
            else
            {
                sp_View.DisplayMode = SplitViewDisplayMode.Overlay;
            }
            ChangeTheme();
            timer = new DispatcherTimer();
            timer.Interval = new TimeSpan(0, 0, 5);
            timer.Start();
            timer.Tick += Timer_Tick;
            MessageCenter.ChanageThemeEvent += MessageCenter_ChanageThemeEvent;
            RegisterSystemThemeWatcher();
            MessageCenter.HasMessaged += MessageCenter_HasMessaged;
            MessageCenter.MianNavigateToEvent += MessageCenter_MianNavigateToEvent;
            MessageCenter.InfoNavigateToEvent += MessageCenter_InfoNavigateToEvent;
            MessageCenter.PlayNavigateToEvent += MessageCenter_PlayNavigateToEvent;
            MessageCenter.HomeNavigateToEvent += MessageCenter_HomeNavigateToEvent;
            MessageCenter.BgNavigateToEvent += MessageCenter_BgNavigateToEvent; ;
            MessageCenter.Logined += MessageCenter_Logined;
           
            MessageCenter.ChangeBg += MessageCenter_ChangeBg;
            //main_frame.Navigate(typeof(ChannelPage));
            MessageCenter_ChangeBg();
            main_frame.Visibility = Visibility.Visible;
            menu_List.SelectedIndex = 0;
            Can_Nav = false;
            Can_Nav = true;
            frame.Visibility = Visibility.Visible;
            frame.Navigate(typeof(BlankPage));

            play_frame.Visibility = Visibility.Visible;
            play_frame.Navigate(typeof(BlankPage));

            //LoadPlayApiInfo();

            if (e.Parameter != null)
            {
                var m = e.Parameter as StartModel;
                switch (m.StartType)
                {
                    case StartTypes.None:
                        break;
                    case StartTypes.Video:
                        MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(VideoViewPage), m.Par1);
                        break;
                    case StartTypes.Live:
                        MessageCenter.SendNavigateTo(NavigateMode.Play, typeof(LiveRoomPage), m.Par1);
                        break;
                    case StartTypes.Bangumi:
                        MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(BanInfoPage), m.Par1);
                        break;
                    case StartTypes.MiniVideo:
                        //MessageCenter.ShowMiniVideo(m.Par1);
                        //MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(WebPage), "http://vc.bilibili.com/mobile/detail?vc=" + m.Par1);
                        break;
                    case StartTypes.Web:
                        MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(WebPage), m.Par1);
                        break;
                    case StartTypes.Album:
                        MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(DynamicInfoPage), m.Par1);
                        break;
                    case StartTypes.Article:
                        MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(ArticleContentPage), m.Par1);
                        break;
                    case StartTypes.Music:
                        MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(MusicInfoPage), m.Par1);
                        break;
                    case StartTypes.User:
                        MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(UserCenterPage), m.Par1);
                        break;
                    case StartTypes.File:
                        var files = m.Par3 as IReadOnlyList<IStorageItem>;
                        List<PlayerModel> ls = new List<PlayerModel>();
                        int i = 1;
                        foreach (StorageFile file in files)
                        {

                            ls.Add(new PlayerModel() { Mode = PlayMode.FormLocal, No = i.ToString(), VideoTitle = "", Title = file.DisplayName, Parameter = file, Aid = file.DisplayName, Mid = file.Path });
                            i++;
                        }
                        play_frame.Navigate(typeof(PlayerPage), new object[] { ls, 0 });
                        // MessageCenter.SendNavigateTo(NavigateMode.Play, typeof(PlayerPage), new object[] { ls, 0 });
                        break;
                    case StartTypes.HandleUri:
                        if (!await MessageCenter.HandleUrl(m.Par1))
                        {
                            ContentDialog contentDialog = new ContentDialog()
                            {
                                PrimaryButtonText = "确定",
                                Title = "不支持跳转的地址"
                            };
                            TextBlock textBlock = new TextBlock()
                            {
                                Text = m.Par1,
                                IsTextSelectionEnabled = true
                            };
                            contentDialog.Content = textBlock;
                            contentDialog.ShowAsync();
                        }
                        break;
                    default:
                        break;
                }

            }


            if (SettingHelper.Get_First())
            {
                //更新日志弹层要等开屏图滑走，否则会盖在开屏图上
                await WaitSplashFinishedAsync();
                await AppHelper.LoadChangelogAsync();
                var ver = AppHelper.Changelog.FirstOrDefault();
                if (ver != null)
                {
                    StackPanel sp = new StackPanel();
                    StackPanel titleRow = new StackPanel() { Orientation = Orientation.Horizontal };
                    titleRow.Children.Add(new TextBlock()
                    {
                        Text = ver.Version,
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Foreground = (Brush)Application.Current.Resources["SystemControlForegroundAccentBrush"]
                    });
                    titleRow.Children.Add(new TextBlock()
                    {
                        Text = ver.Date,
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Colors.Gray),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(10, 0, 0, 1)
                    });
                    sp.Children.Add(titleRow);
                    foreach (var item in ver.Items)
                    {
                        // 用 Grid 替代横向 StackPanel,给条目 TextBlock 限定宽度,长条目才能换行显示
                        Grid row = new Grid() { Margin = new Thickness(0, 3, 0, 0) };
                        row.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
                        row.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                        var bullet = new TextBlock()
                        {
                            Text = "•",
                            Foreground = new SolidColorBrush(Colors.Gray),
                            Margin = new Thickness(0, 0, 6, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        Grid.SetColumn(bullet, 0);
                        var itemText = new TextBlock() { Text = item, TextWrapping = TextWrapping.Wrap };
                        Grid.SetColumn(itemText, 1);
                        row.Children.Add(bullet);
                        row.Children.Add(itemText);
                        sp.Children.Add(row);
                    }
                    await new ContentDialog() { Content = sp, PrimaryButtonText = "知道了" }.ShowAsync();
                }
                else
                {
                    await new ContentDialog() { Content = AppHelper.GetLastVersionStr(), PrimaryButtonText = "知道了" }.ShowAsync();
                }
                SettingHelper.Set_First(false);
            }


            new AppHelper().GetDeveloperMessage();

            account = new Account();
            //检查登录状态
            if (SettingHelper.Get_Access_key() != "")
            {
                if ((await account.CheckLoginState(ApiHelper.access_key)).success)
                {
                    MessageCenter_Logined();
                    await account.SSO(ApiHelper.access_key);
                }
                else
                {
                    var data = await account.RefreshToken(SettingHelper.Get_Access_key(), SettingHelper.Get_Refresh_Token());
                    if (!data.success)
                    {
                        await UserManage.LogoutAsync();
                        Utils.ShowMessageToast("登录过期，请重新登录");
                        await Utils.ShowLoginDialog();
                    }
                }
            }


        }
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            MessageCenter.ChanageThemeEvent -= MessageCenter_ChanageThemeEvent;
            UnregisterSystemThemeWatcher();
            MessageCenter.MianNavigateToEvent -= MessageCenter_MianNavigateToEvent;
            MessageCenter.InfoNavigateToEvent -= MessageCenter_InfoNavigateToEvent;
            MessageCenter.PlayNavigateToEvent -= MessageCenter_PlayNavigateToEvent;
            MessageCenter.HomeNavigateToEvent -= MessageCenter_HomeNavigateToEvent;
            MessageCenter.BgNavigateToEvent -= MessageCenter_BgNavigateToEvent; ;
            MessageCenter.Logined -= MessageCenter_Logined;
          
            MessageCenter.ChangeBg -= MessageCenter_ChangeBg;
        }
        private async void MessageCenter_ChangeBg()
        {
            if (SettingHelper.Get_CustomBG() && SettingHelper.Get_BGPath().Length != 0)
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(SettingHelper.Get_BGPath());
                if (file != null)
                {
                    img_bg.Stretch = (Stretch)SettingHelper.Get_BGStretch();
                    img_bg.HorizontalAlignment = (HorizontalAlignment)SettingHelper.Get__BGHor();
                    img_bg.VerticalAlignment = (VerticalAlignment)SettingHelper.Get_BGVer();
                    img_bg.Opacity = Convert.ToDouble(SettingHelper.Get_BGOpacity()) / 10;

                    if (SettingHelper.Get_BGMaxWidth() != 0)
                    {
                        img_bg.MaxWidth = SettingHelper.Get_BGMaxWidth();
                    }
                    else
                    {

                        img_bg.MaxWidth = double.PositiveInfinity;
                    }
                    if (SettingHelper.Get_BGMaxHeight() != 0)
                    {
                        img_bg.MaxHeight = SettingHelper.Get_BGMaxHeight();
                    }
                    else
                    {
                        img_bg.MaxHeight = double.PositiveInfinity;
                    }


                    var st = await file.OpenReadAsync();
                    BitmapImage bit = new BitmapImage();
                    await bit.SetSourceAsync(st);
                    img_bg.Source = bit;
                    if (SettingHelper.Get_FrostedGlass() != 0)
                    {
                        GlassHost.Visibility = Visibility.Visible;
                        InitializedFrostedGlass(GlassHost, SettingHelper.Get_FrostedGlass());
                    }
                    else
                    {
                        GlassHost.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {

                }
            }
            else
            {
                img_bg.Source = null;
            }
        }


        #region 开屏图

        /// <summary>取走待显示的开屏图并开始播放。标志消费一次即清空。</summary>
        private void ConsumePendingSplash()
        {
            var pending = PendingSplash;
            PendingSplash = null;
            if (pending != null)
            {
                _splashTask = ShowSplashAsync(pending);
            }
        }

        /// <summary>等开屏图完全滑走再放行首个弹层；本次没展示开屏图时立即返回。</summary>
        private async Task WaitSplashFinishedAsync()
        {
            var task = _splashTask;
            if (task == null)
            {
                return;
            }

            try
            {
                await task;
            }
            catch (Exception ex)
            {
                //ShowSplashAsync 内部已兜底，这里只是防止异常冒到调用方
                LogHelper.WriteLog("等待开屏图结束异常", LogType.ERROR, ex);
            }
        }

        /// <summary>把开屏图铺在最上层，停留后向上滑走 —— 滑走过程露出的就是下面已经加载好的主界面。</summary>
        private async Task ShowSplashAsync(SplashImageItem splash)
        {
            try
            {
                splash_bg.Source = splash.Image;
                splash_img.Source = splash.Image;
                splash_layer.Visibility = Visibility.Visible;

                //淡入。此前几轮淡入「无效」的真正原因是：Frame 的导航动画（Page Refresh）正在对整个
                //页面播「上滑 + 淡入」，会覆盖掉页面内部元素的透明度变化。现已在 SplashPage 侧用
                //SuppressNavigationTransitionInfo 抑制了那次导航动画，这里的淡入才可能真正生效。
                //注意淡入的是内层 content：外层已不透明，先遮住主界面，避免淡入时透出主界面内容。
                splash_content.Opacity = 0;
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromMilliseconds(SplashFadeInMs)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    EnableDependentAnimation = true
                };
                Storyboard.SetTarget(fadeIn, splash_content);
                Storyboard.SetTargetProperty(fadeIn, "(UIElement.Opacity)");
                var fadeStoryboard = new Storyboard { FillBehavior = FillBehavior.HoldEnd };
                fadeStoryboard.Children.Add(fadeIn);
                fadeStoryboard.Begin();

                try
                {
                    //模糊强度沿用自定义背景图的写法（d * 5）
                    InitializedFrostedGlass(splash_glass, 2);
                }
                catch (Exception ex)
                {
                    //毛玻璃失败只影响背景观感，不应让整张开屏图显示失败
                    LogHelper.WriteLog("开屏图毛玻璃初始化失败", LogType.ERROR, ex);
                }

                _splashSkip = new TaskCompletionSource<bool>();
                try
                {
                    //时长夹取规则同样有单元测试覆盖
                    var displayMs = SplashImageSelector.NormalizeDurationMs(splash.DurationMs, SplashMinShowMs, SplashMaxShowMs);
                    //倒计时到点或用户点击跳过，先到者生效
                    await Task.WhenAny(Task.Delay(displayMs), _splashSkip.Task);
                }
                finally
                {
                    _splashSkip = null;
                }

                //向上滑出，露出已在下方渲染好的主界面。
                //AnimateDoublePropertyAsync 是 CarouselHelper 的扩展方法，类外必须用扩展调用语法
                await splash_layer.GetCompositeTransform().AnimateDoublePropertyAsync(
                    "TranslateY",
                    0,
                    -splash_layer.ActualHeight,
                    SplashSlideOutMs,
                    new CubicEase { EasingMode = EasingMode.EaseIn });
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("展示启动开屏图异常", LogType.ERROR, ex);
            }
            finally
            {
                //无论如何都要把覆盖层收掉，绝不能挡住主界面
                splash_layer.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>点击开屏图立即跳过，不必等倒计时。</summary>
        private void splash_layer_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _splashSkip?.TrySetResult(true);
        }

        /// <summary>交给 MainPage 展示的开屏图。</summary>
        public class SplashImageItem
        {
            public BitmapImage Image { get; set; }
            public int DurationMs { get; set; }
        }

        #endregion


        private void InitializedFrostedGlass(UIElement glassHost, int d)
        {
            Visual hostVisual = ElementCompositionPreview.GetElementVisual(glassHost);
            Compositor compositor = hostVisual.Compositor;

            // Create a glass effect, requires Win2D NuGet package
            var glassEffect = new GaussianBlurEffect
            {
                BlurAmount = d * 5.0f,
                BorderMode = EffectBorderMode.Hard,
                Source = new ArithmeticCompositeEffect
                {
                    MultiplyAmount = 0,
                    Source1Amount = 0.5f,
                    Source2Amount = 0.5f,
                    Source1 = new CompositionEffectSourceParameter("backdropBrush"),
                    Source2 = new ColorSourceEffect
                    {
                        Color = Color.FromArgb(255, 245, 245, 245)
                    }
                }
            };

            //  Create an instance of the effect and set its source to a CompositionBackdropBrush
            var effectFactory = compositor.CreateEffectFactory(glassEffect);
            var backdropBrush = compositor.CreateBackdropBrush();
            var effectBrush = effectFactory.CreateBrush();

            effectBrush.SetSourceParameter("backdropBrush", backdropBrush);

            // Create a Visual to contain the frosted glass effect
            var glassVisual = compositor.CreateSpriteVisual();
            glassVisual.Brush = effectBrush;

            // Add the blur as a child of the host in the visual tree
            ElementCompositionPreview.SetElementChildVisual(glassHost, glassVisual);

            // Make sure size of glass host and glass visual always stay in sync
            var bindSizeAnimation = compositor.CreateExpressionAnimation("hostVisual.Size");
            bindSizeAnimation.SetReferenceParameter("hostVisual", hostVisual);

            glassVisual.StartAnimation("Size", bindSizeAnimation);
        }


        private void MessageCenter_BgNavigateToEvent(Type page, params object[] par)
        {
            bg_Frame.Navigate(page, par);
        }

        private void Storyboard_Completed(object sender, object e)
        {
            row_bottom.Height = new GridLength(0);
        }

        private async void MessageCenter_Logined()
        {
            btn_Login.Visibility = Visibility.Collapsed;
            btn_UserInfo.Visibility = Visibility.Visible;
            gv_User.Visibility = Visibility.Visible;
            try
            {
                var data = await account.GetMyInfo();
                if (data.success)
                {
                    var m = data.data;
                    gv_user.DataContext = data.data;
                    if (m.rank == 0 || m.rank == 5000)
                    {
                        dtzz.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        dtzz.Visibility = Visibility.Collapsed;
                    }
                    if (m.vip != null && m.vip.type != 0)
                    {
                        img_VIP.Visibility = Visibility.Visible;
                        SettingHelper.Set_UserIsVip(true);
                    }
                    else
                    {
                        img_VIP.Visibility = Visibility.Collapsed;
                        SettingHelper.Set_UserIsVip(false);
                    }
                }
                else
                {
                    Utils.ShowMessageToast(data.message);
                }


            }
            catch (Exception)
            {
            }

        }

        private void MessageCenter_HomeNavigateToEvent(Type page, params object[] par)
        {
            main_frame.Navigate(page, par);
        }

        private void MessageCenter_PlayNavigateToEvent(Type page, params object[] par)
        {
            if (SettingHelper.Get_NewWindow())
            {
                MessageCenter.OpenNewWindow(page, par);
            }
            else
            {
                play_frame.Navigate(page, par);
            }

        }

        private void MessageCenter_InfoNavigateToEvent(Type page, params object[] par)
        {
            frame.Navigate(page, par);
        }

        private void MessageCenter_MianNavigateToEvent(Type page, params object[] par)
        {

            this.Frame.Navigate(page, par);
        }



        private void MessageCenter_ChanageThemeEvent(object par, params object[] par1)
        {
            //只换肤，不重设右侧背景页。ChangeTheme() 里的 switch 读的是 Get_Rigth()，
            //与主题无关，却会在 UI 线程同步重建页面，导致切换主题时卡顿。
            ApplyTheme();
        }

        private void ChangeTheme()
        {

            switch (SettingHelper.Get_Rigth())
            {
                case 1:
                    bg_Frame.Navigate(typeof(FastNavigatePage));
                    break;
                case 2:
                    bg_Frame.Navigate(typeof(ChannelPage));
                    break;
                case 3:
                    bg_Frame.Navigate(typeof(RankPage));
                    break;
                case 4:
                    bg_Frame.Navigate(typeof(TimelinePage));
                    break;
                case 5:
                    bg_Frame.Navigate(typeof(LiveAllPage));
                    break;
                default:
                    bg_Frame.Navigate(typeof(BlankPage));
                    break;
            }

            //tuic.To = this.ActualWidth;
            //storyboardPopOut.Begin();
            ApplyTheme();
        }

        /// <summary>
        /// 只重新解析主题资源，不动右侧背景页。
        /// 系统深色模式变化时走这里，避免跟随系统导致背景页被反复重载。
        /// </summary>
        private void ApplyTheme()
        {
            string ThemeName = SettingHelper.Get_EffectiveTheme();
            if (ThemeName== "Dark")
            {
                RequestedTheme = ElementTheme.Dark;
            }
            else
            {
                ResourceDictionary newDictionary = new ResourceDictionary();
                newDictionary.Source = new Uri($"ms-appx:///Theme/{ThemeName}Theme.xaml", UriKind.RelativeOrAbsolute);
                Application.Current.Resources.ThemeDictionaries["Light"] = newDictionary;
                RequestedTheme = ElementTheme.Dark;
                RequestedTheme = ElementTheme.Light;
            }
            ChangeTitbarColor();
        }

        private UISettings uiSettings;

        private void RegisterSystemThemeWatcher()
        {
            if (uiSettings != null)
            {
                return;
            }
            uiSettings = new UISettings();
            uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
        }

        private void UnregisterSystemThemeWatcher()
        {
            if (uiSettings == null)
            {
                return;
            }
            uiSettings.ColorValuesChanged -= UiSettings_ColorValuesChanged;
            uiSettings = null;
        }

        //ColorValuesChanged 在后台线程触发，必须切回 UI 线程才能改 RequestedTheme
        private async void UiSettings_ColorValuesChanged(UISettings sender, object args)
        {
            if (!SettingHelper.Get_FollowSystemTheme())
            {
                return;
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, ApplyTheme);
        }
        private void ChangeTitbarColor()
        {
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                var applicationView = ApplicationView.GetForCurrentView();
                applicationView.SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);
                //StatusBar.GetForCurrentView().HideAsync();
                StatusBar statusBar = StatusBar.GetForCurrentView();

                statusBar.ForegroundColor = Color.FromArgb(255, 254, 254, 254);
                statusBar.BackgroundColor = ((SolidColorBrush)grid_Top.Background).Color;
                statusBar.BackgroundOpacity = 100;
            }

            var titleBar = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().TitleBar;
            titleBar.BackgroundColor = ((SolidColorBrush)grid_Top.Background).Color;
            titleBar.ForegroundColor = Color.FromArgb(255, 254, 254, 254);//Colors.White纯白用不了。。。
            titleBar.ButtonHoverBackgroundColor = ((SolidColorBrush)sp_View.PaneBackground).Color;
            titleBar.ButtonBackgroundColor = ((SolidColorBrush)grid_Top.Background).Color;
            titleBar.ButtonForegroundColor = Color.FromArgb(255, 254, 254, 254);
            titleBar.InactiveBackgroundColor = ((SolidColorBrush)grid_Top.Background).Color;
            titleBar.ButtonInactiveBackgroundColor = ((SolidColorBrush)grid_Top.Background).Color;
        }
        /// <summary>
        /// 重新是否有新的信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MessageCenter_HasMessaged(object sender, object e)
        {
            var tag = e as string;
            if (tag != null && tag.StartsWith("private:"))
            {
                // 私信已读，本地清零避免API缓存延迟导致红点残留
                MessageUnreadState.ClearPrivate();
                UpdateMessageDot();
            }
            else if (tag != null && tag.StartsWith("feed:"))
            {
                // 消息中心查看列表后按类别本地清零，主页红点立即刷新
                switch (tag.Substring("feed:".Length))
                {
                    case "reply":
                        MessageUnreadState.ClearReply();
                        break;
                    case "at":
                        MessageUnreadState.ClearAt();
                        break;
                    case "like":
                        MessageUnreadState.ClearLike();
                        break;
                    case "notice":
                        MessageUnreadState.ClearNotice();
                        break;
                    case "all":
                        MessageUnreadState.ClearAll();
                        break;
                }
                UpdateMessageDot();
            }
            else
            {
                Timer_Tick(null, null);
            }
        }
        private void UpdateMessageDot()
        {
            var has = MessageUnreadState.HasUnread();
            bor_TZ.Visibility = has
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void Timer_Tick(object sender, object e)
        {
            if (checkingMessages)
            {
                return;
            }

            checkingMessages = true;
            try
            {
                //if (ApiHelper.IsLogin())
                //{
                if (await HasMessage())
                {
                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        //menu_bor_HasMessage

                        //隐藏主页消息红点时,菜单内和顶部按钮的红点同时隐藏
                        bor_TZ.Visibility = SettingHelper.Get_HideMainPageMessageDot()
                            ? Visibility.Collapsed
                            : Visibility.Visible;
                    });
                }
                else
                {

                    await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        bor_TZ.Visibility = Visibility.Collapsed;
                    });
                }
            }
            finally
            {
                checkingMessages = false;
            }
        }

        readonly MessageAPI messageAPI = new MessageAPI();
        DateTimeOffset lastPrivateUnreadCheck = DateTimeOffset.MinValue;
        bool checkingMessages;
        private async Task<bool> HasMessage()
        {
            try
            {
                if (!ApiHelper.IsLogin() || string.IsNullOrEmpty(Account.GetCookieValue("SESSDATA")))
                {
                    MessageUnreadState.Reset();
                    lastPrivateUnreadCheck = DateTimeOffset.MinValue;
                    return false;
                }

                var feedResponse = await messageAPI.UnreadFeed().Request();
                if (feedResponse != null && feedResponse.status)
                {
                    var feedResult = await feedResponse.GetData<MessageFeedUnreadModel>();
                    if (feedResult != null && feedResult.success && feedResult.data != null)
                    {
                        MessageUnreadState.MergeFromApi(feedResult.data, null, null);
                    }
                }

                if (DateTimeOffset.Now - lastPrivateUnreadCheck >= TimeSpan.FromMinutes(2))
                {
                    lastPrivateUnreadCheck = DateTimeOffset.Now;
                    var privateRequest = messageAPI.PrivateUnread().Request();
                    var groupRequest = messageAPI.GroupUnread().Request();
                    var privateResponse = await privateRequest;
                    if (privateResponse != null && privateResponse.status)
                    {
                        var privateResult = await privateResponse.GetData<MessagePrivateUnreadModel>();
                        if (privateResult != null && privateResult.success && privateResult.data != null)
                        {
                            MessageUnreadState.MergeFromApi(null, privateResult.data, null);
                        }
                    }

                    var groupResponse = await groupRequest;
                    if (groupResponse != null && groupResponse.status)
                    {
                        var groupResult = await groupResponse.GetData<MessageGroupUnreadModel>();
                        if (groupResult != null && groupResult.success && groupResult.data != null)
                        {
                            MessageUnreadState.MergeFromApi(null, null, groupResult.data);
                        }
                    }
                }

                return MessageUnreadState.HasUnread();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void play_frame_Navigated(object sender, NavigationEventArgs e)
        {

            if (play_frame.CanGoBack)
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            }
            else
            {
                if (!frame.CanGoBack)
                {
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
                }

            }
            if ((main_frame.Content as Page).Tag == null)
            {
                return;
            }
            switch ((main_frame.Content as Page).Tag.ToString())
            {

                case "Cn":
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    _InBangumi = true;
                    txt_Header.Text = "国漫";
                    break;
                case "Jp":
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    _InBangumi = true;
                    txt_Header.Text = "番剧";
                    break;
                case "Music":
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    _InBangumi = true;
                    txt_Header.Text = "音频";
                    break;
                default:
                    break;
            }


        }
        private void frame_Navigated(object sender, NavigationEventArgs e)
        {
            if (frame.CanGoBack)
            {
                SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
            }
            else
            {
                if (!play_frame.CanGoBack)
                {
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
                }

            }

            //if ((frame.Content as Page).Tag == null)
            //{
            //    frame.Background = App.Current.Resources["Bili-Background"] as SolidColorBrush;
            //    return;
            //}

            //if ((frame.Content as Page).Tag.ToString()!= "blank")
            //{
            //    frame.Background = App.Current.Resources["Bili-Background"] as SolidColorBrush;
            //}
            //else
            //{
            //    frame.Background =null;
            //}
            if ((main_frame.Content as Page).Tag == null)
            {
                return;
            }
            switch ((main_frame.Content as Page).Tag.ToString())
            {

                case "Cn":
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    _InBangumi = true;
                    txt_Header.Text = "国漫";
                    break;
                case "Jp":
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    _InBangumi = true;
                    txt_Header.Text = "番剧";
                    break;
                case "Music":
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    _InBangumi = true;
                    txt_Header.Text = "音频";
                    break;
                case "Article":
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    _InBangumi = true;
                    txt_Header.Text = "专栏";
                    break;
                default:
                    break;
            }



            //switch ((frame.Content as Page).Tag.ToString())
            //{
            //    case "blank":c
            //        //Background="{ThemeResource Bili-Background}"

            //        break;
            //}
        }
        bool _InBangumi = false;
        private void main_frame_Navigated(object sender, NavigationEventArgs e)
        {
            if ((main_frame.Content as Page).Tag == null)
            {
                return;
            }
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;
            Can_Nav = false;
            _InBangumi = false;
            switch ((main_frame.Content as Page).Tag.ToString())
            {
                case "首页":
                    menu_List.SelectedIndex = 0;
                    txt_Header.Text = "首页";
                    break;
                case "频道":
                    menu_List.SelectedIndex = 1;
                    txt_Header.Text = "频道";
                    break;
                case "直播":
                    menu_List.SelectedIndex = 2;
                    txt_Header.Text = "直播";
                    break;
                case "番剧":
                    menu_List.SelectedIndex = 3;

                    txt_Header.Text = "番剧";
                    break;
                case "动态":
                    menu_List.SelectedIndex = 4;

                    //menu_List.SelectedIndex = 3;
                    //bottom.SelectedIndex = 3;
                    txt_Header.Text = "动态";
                    break;
                case "发现":
                    menu_List.SelectedIndex = 5;

                    //menu_List.SelectedIndex = 4;
                    //bottom.SelectedIndex = 4;
                    txt_Header.Text = "发现";
                    break;
                case "设置":
                    menu_List.SelectedIndex = 6;
                    txt_Header.Text = "设置";
                    break;
                case "Cn":
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    _InBangumi = true;
                    txt_Header.Text = "国漫";
                    break;
                case "Jp":
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    _InBangumi = true;
                    txt_Header.Text = "番剧";
                    break;
                case "Music":
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    _InBangumi = true;
                    txt_Header.Text = "音频";
                    break;
                case "Article":
                    SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
                    _InBangumi = true;
                    txt_Header.Text = "专栏";
                    break;
                default:
                    break;
            }
            Can_Nav = true;
            // }
        }
        bool Can_Nav = true;
        private void menu_List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!Can_Nav)
            {
                return;
            }

            switch (menu_List.SelectedIndex)
            {
                case 0:
                    //if (SettingHelper.Get_NewFeed())
                    //{
                    //   
                    //}
                    //else
                    //{
                    main_frame.Navigate(typeof(HomePage));
                    //}
                    //if (!Reg_OpenVideo)
                    //{
                    //    (main_frame.Content as ChannelPage).OpenVideo += MainPage_OpenVideo; 
                    //    Reg_OpenVideo = true;
                    //}
                    txt_Header.Text = "首页";
                    break;
                case 1:
                    main_frame.Navigate(typeof(ChannelPage));

                    txt_Header.Text = "频道";
                    break;
                case 2:
                    main_frame.Navigate(typeof(LiveV2Page));

                    txt_Header.Text = "直播";
                    break;
                case 3:
                    main_frame.Navigate(typeof(BangumiPage));

                    txt_Header.Text = "番剧";
                    break;

                case 4:
                    main_frame.Navigate(typeof(AttentionPage));

                    txt_Header.Text = "动态";
                    break;
                case 5:
                    main_frame.Navigate(typeof(FindPage));

                    txt_Header.Text = "发现";
                    break;
                //case 4:
                //    main_frame.Navigate(typeof(SettingPage));

                //    txt_Header.Text = "设置";
                //    break;
                default:
                    break;
            }
            sp_View.IsPaneOpen = false;

        }

        private void btn_Search_Click(object sender, RoutedEventArgs e)
        {
            frame.Navigate(typeof(SearchV2Page));
        }

        private async void btn_Login_Click(object sender, RoutedEventArgs e)
        {
            //frame.Navigate(typeof(LoginPage));

            LoginDialog loginDialog = new LoginDialog();
            await loginDialog.ShowAsync();
            fy.Hide();
        }

        private async void btn_LogOut_Click(object sender, RoutedEventArgs e)
        {
            //清理 WebView2 存储是异步的，先收起浮层再等清理完成，避免清完还带着旧账号
            btn_Login.Visibility = Visibility.Visible;
            btn_UserInfo.Visibility = Visibility.Collapsed;
            gv_User.Visibility = Visibility.Collapsed;
            fy.Hide();
            await UserManage.LogoutAsync();
        }

     

        private void btn_user_myvip_Click(object sender, RoutedEventArgs e)
        {
            //http://big.bilibili.com/site/big.html
            frame.Navigate(typeof(WebPage), new object[] { "https://big.bilibili.com/mobile/home" });
            fy.Hide();
        }

        private void btn_user_mycollect_Click(object sender, RoutedEventArgs e)
        {
            frame.Navigate(typeof(MyCollectPage));
            fy.Hide();
        }

        private void btn_user_mychistory_Click(object sender, RoutedEventArgs e)
        {
            frame.Navigate(typeof(MyHistroryPage));
            fy.Hide();
        }

        private void btn_user_mywallet_Click(object sender, RoutedEventArgs e)
        {
            frame.Navigate(typeof(MyWalletPage));
            fy.Hide();
        }

        private void dtzz_Click(object sender, RoutedEventArgs e)
        {
            frame.Navigate(typeof(WebPage), "https://account.bilibili.com/answer/base");
            fy.Hide();
        }

        private void btn_UserInfo_Click(object sender, RoutedEventArgs e)
        {
            frame.Navigate(typeof(UserCenterPage),ApiHelper.GetUserId());
            fy.Hide();
        }

        private void btn_user_myGuanzhu_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(WebPage), "https://space.bilibili.com/h5/follow");
            //frame.Navigate(typeof(UserInfoPage), new object[] { null, 2 });
            fy.Hide();
        }

        private void btn_user_mymessage_Click(object sender, RoutedEventArgs e)
        {

            frame.Navigate(typeof(MyMessagePage));
            fy.Hide();
        }

        private void btn_user_Qr_Click(object sender, RoutedEventArgs e)
        {

            //var info = gv_user.DataContext as UserInfoModel;

            frame.Navigate(typeof(MyQrPage), new object[] { new MyqrModel() {
                  name=Account.myInfo.name,
                  photo=Account.myInfo.face,
                  qr="https://space.bilibili.com/"+ApiHelper.GetUserId(),
                  sex=Account.myInfo.Sex
            } });
            fy.Hide();
        }

        private void btn_Qr_Click(object sender, RoutedEventArgs e)
        {
            play_frame.Navigate(typeof(QRPage));
        }

        private void btn_Down_Click(object sender, RoutedEventArgs e)
        {

            frame.Navigate(typeof(Download2Page));
        }

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = (e.ClickedItem as StackPanel).Tag.ToString();
            if (item == "ToView")
            {
                if (!ApiHelper.IsLogin() && !await Utils.ShowLoginDialog())
                {
                    Utils.ShowMessageToast("请先登录");
                    return;
                }
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(ToViewPage));
            }
            else if (item == "Test")
            {
               
            }
            else
            {
                MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(SettingPage));
            }

        }


        //private async void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    //piv.SelectedIndex += 1;


        //    if (piv.SelectedIndex == 0)
        //    {

        //        if (menu_List.SelectedIndex == 0)
        //        {
        //            menu_List.SelectedIndex = 4;
        //        }
        //        else
        //        {
        //            menu_List.SelectedIndex = menu_List.SelectedIndex - 1;
        //        }
        //        await Task.Delay(200);
        //        piv.SelectedIndex = 1;
        //        return;

        //    }
        //    if (piv.SelectedIndex == 2)
        //    {

        //        if (menu_List.SelectedIndex == 4)
        //        {
        //            menu_List.SelectedIndex = 0;
        //        }
        //        else
        //        {
        //            menu_List.SelectedIndex = menu_List.SelectedIndex + 1;
        //        }
        //        await Task.Delay(200);
        //        piv.SelectedIndex = 1;
        //        return;
        //    }

        //}

        private void piv_PointerMoved(object sender, PointerRoutedEventArgs e)
        {

        }

        private void btn_user_moviecollect_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(FollowSeasonPage), Modules.SeasonType.cinema);
            fy.Hide();

        }

        private void btn_ClearMedia_Click(object sender, RoutedEventArgs e)
        {
            MusicHelper.ClearMediaList();
        }

        bool isSetMusic = false;
        private void ls_music_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isSetMusic)
            {
                MusicHelper._mediaPlaybackList.MoveTo(Convert.ToUInt32(ls_music.SelectedIndex));
            }
        }

        private void btn_showMusicInfo_Click(object sender, RoutedEventArgs e)
        {

            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(MusicInfoPage), (sender as AppBarButton).Tag.ToString());
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MusicHelper._mediaPlayer.SystemMediaTransportControls.DisplayUpdater.Update();
        }

        private void btn_MiniPlayer_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Play, typeof(MusicMiniPlayerPage));
        }

        private void btn_music_Shuffle_Click(object sender, RoutedEventArgs e)
        {
            MusicHelper._mediaPlaybackList.ShuffleEnabled = true;
            btn_music_Shuffle.Visibility = Visibility.Collapsed;
            btn_music_List.Visibility = Visibility.Visible;
        }

        private void btn_music_List_Click(object sender, RoutedEventArgs e)
        {
            MusicHelper._mediaPlaybackList.ShuffleEnabled = false;
            btn_music_Shuffle.Visibility = Visibility.Visible;
            btn_music_List.Visibility = Visibility.Collapsed;
        }

        private void btn_CG_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
        }

        private void btn_user_toView_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(ToViewPage));
        }

        private void btn_IKonwn_Click(object sender, RoutedEventArgs e)
        {
            network_error.Visibility = Visibility.Collapsed;
        }

        private void btn_Test_Click(object sender, RoutedEventArgs e)
        {

        }
        protected override Size MeasureOverride(Size availableSize)
        {
            if (availableSize.Width>=800)
            {
                SetWideUI();
            }
            else
            {
                SetNarrowUI();
            }
            return base.MeasureOverride(availableSize);
        }
        //设置宅布局
        private void SetNarrowUI()
        {
            grid_o.BorderThickness = new Thickness(0, 0, 1, 0);
           
            bg.Visibility = Visibility.Collapsed;
            sp_View.DisplayMode = SplitViewDisplayMode.CompactOverlay;
            Grid.SetColumn(frame, 0);
            btn_OpenMenu.Visibility = Visibility.Visible;
            SetOneColumn();
        }
        //设置宽布局
        private void SetWideUI()
        {
            grid_o.BorderThickness = new Thickness(0, 0, 1, 0);
         
            sp_View.DisplayMode = SplitViewDisplayMode.CompactOverlay;
            Grid.SetColumn(frame,1);
            bg.Visibility = Visibility.Visible;
            btn_OpenMenu.Visibility = Visibility.Visible;
            if (frame.CurrentSourcePageType == typeof(BlankPage)&&main_frame.CurrentSourcePageType==typeof(HomePage))
            {
                if (SettingHelper.Get_ColunmHome())
                {
                    SetTwoColumn();
                }
                else
                {
                    SetOneColumn();
                }
            }
            else
            {
                SetTwoColumn();
            }
        }
        //显示双列
        private void SetTwoColumn()
        {
            column_left.MaxWidth = 500;
            column_left.Width = new GridLength(1, GridUnitType.Star);
            column_right.Width = new GridLength(1, GridUnitType.Star);
        }
        //显示单列
        private void SetOneColumn()
        {
            column_left.MaxWidth = double.MaxValue;
            column_right.Width = new GridLength(0);
        }
        private void frame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (this.ActualWidth >= 800)
            {
                if (e.SourcePageType != typeof(BlankPage))
                {
                    SetTwoColumn();
                }
                else if (main_frame.SourcePageType == typeof(HomePage))
                {
                    if (SettingHelper.Get_ColunmHome())
                    {
                        SetTwoColumn();
                    }
                    else
                    {
                        SetOneColumn();
                    }
                }
                else
                {
                    SetTwoColumn();
                }
            }
        }
        private void main_frame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (this.ActualWidth >= 800)
            {
                if (e.SourcePageType != typeof(HomePage))
                {
                    SetTwoColumn();
                }
                else if(frame.SourcePageType != typeof(BlankPage))
                {
                    SetTwoColumn();
                }
                else
                {
                    if (SettingHelper.Get_ColunmHome())
                    {
                        SetTwoColumn();
                    }
                    else
                    {
                        SetOneColumn();
                    }
                }
            }
        }

        private void btn_user_seasoncollect_Click(object sender, RoutedEventArgs e)
        {
            MessageCenter.SendNavigateTo(NavigateMode.Info, typeof(FollowSeasonPage), Modules.SeasonType.bangumi);
            fy.Hide();
        }
    }
}
