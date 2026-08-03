using Newtonsoft.Json;
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

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace BiliBili.UWP.Pages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MyWalletPage : Page
    {
        public MyWalletPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        private void btn_Back_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode== NavigationMode.New)
            {
                list_Orders.Items.Clear();
                list_Recharge.Items.Clear();
                list_Quan.Items.Clear();
                list_Coins.ItemsSource = null;
                txt_CoinsEmpty.Visibility = Visibility.Collapsed;
                pivot.SelectedIndex = 0;
                LoadWallet();
                LoadCoins();
            }
        }
        private async void LoadWallet()
        {
            try
            {
                pr_Load.Visibility = Visibility.Visible;
                string uri = "https://pay.bilibili.com/paywallet/wallet/getUserWallet";
                string results = await WebClientClass.PostResultsJson(new Uri(uri), "{\"platformType\":\"3\"}");
                WalletModel model = JsonConvert.DeserializeObject<WalletModel>(results);
                if (model != null && model.errno == 0 && model.data != null)
                {
                    this.DataContext = model.data;
                }
                else
                {
                    Utils.ShowMessageToast(model?.showMsg ?? model?.msg ?? model?.message ?? "读取B币余额失败", 3000);
                }
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147012867)
                {
                    Utils.ShowMessageToast("检查你的网络连接！", 3000);
                }
                else
                {
                    Utils.ShowMessageToast("发生错误\r\n" + ex.Message, 3000);
                }
            }
            finally
            {
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }
        private async void LoadCoins()
        {
            try
            {
                pr_Load.Visibility = Visibility.Visible;
                string uri = "https://account.bilibili.com/site/getCoin";
                string results = await WebClientClass.GetResults(new Uri(uri));

                _UserInfoModel model = JsonConvert.DeserializeObject<_UserInfoModel>(results);
            
                if (model.code == 0)
                {
                    grid_coions.DataContext = model.data;
                }
                else
                {
                    Utils.ShowMessageToast(model.message, 3000);
                }
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147012867)
                {
                    Utils.ShowMessageToast("检查你的网络连接！", 3000);
                }
                else
                {
                    Utils.ShowMessageToast("读取硬币发生错误\r\n" + ex.Message, 3000);
                }
            }
            finally
            {
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }
        private void btn_GoPay_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(WebPage),new object[] { "https://pay.bilibili.com/charge_alipay.html" });
        }

        private async void list_Orders_ItemClick(object sender, ItemClickEventArgs e)
        {
          var info=   e.ClickedItem as payOrdersModel;
            var x = new ContentDialog();
            StackPanel st = new StackPanel();
          
            st.Children.Add(new TextBlock()
            {
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                Text = string.Format("订单号:{0}\r\n\r\n订单名称:{1}\r\n\r\n创建时间:{2}\r\n\r\n消费渠道:{3}\r\n\r\n支付状态:{4}\r\n\r\n支付金额:--\r\n\r\n支付B币:{5}B币", info.orderNo,info.remark,info.ctime,info.payAppInfo.name,info.Status,info.bp)
            });
            x.Content = st;
            x.PrimaryButtonText = "知道了";
            x.IsPrimaryButtonEnabled = true;
           await x.ShowAsync();

        }

        private void pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (pivot.SelectedIndex == 4 && list_Coins.ItemsSource == null)
            {
                GetCoins();
            }
        }
        int page_Orders =1;
        bool load_Orders = false;
        private async void GetOrders()
        {
            try
            {
                load_Orders = true;
                pr_Load.Visibility = Visibility.Visible;
                string uri = string.Format("https://pay.bilibili.com/bp/payOrders?pageSize=20&pageNo={0}&beginTime=2015-01-1&endTime={1}",page_Orders,DateTime.Now.ToString("yyyy-MM-dd"));
                string results = await WebClientClass.GetResults(new Uri(uri));

                payOrdersModel model = JsonConvert.DeserializeObject<payOrdersModel>(results);

                if (model.code == 0)
                {
                    if (model.data.list.Count!=0)
                    {
                        model.data.list.ForEach(x => list_Orders.Items.Add(x));
                        page_Orders++;
                    }
                    else
                    {
                        Utils.ShowMessageToast("加载完了",3000);
                    }
                }
                else
                {
                    Utils.ShowMessageToast(model.message, 3000);
                }
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147012867)
                {
                    Utils.ShowMessageToast("检查你的网络连接！", 3000);
                }
                else
                {
                    Utils.ShowMessageToast("读取硬币发生错误\r\n" + ex.Message, 3000);
                }
            }
            finally
            {
                load_Orders = false;
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }

        private void sv_Orders_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sv_Orders.VerticalOffset == sv_Orders.ScrollableHeight)
            {
                if (!load_Orders)
                {
                    //User_load_more.IsEnabled = false;
                    //User_load_more.Content = "正在加载";
                    GetOrders();
                }
            }
        }

        int page_Recharge = 1;
        bool load_Recharge = false;
        private async void GetRechargeOrders()
        {
            try
            {
                load_Recharge = true;
                pr_Load.Visibility = Visibility.Visible;
                string uri = string.Format("https://pay.bilibili.com/bp/rechargeOrders?pageSize=20&pageNo={0}&beginTime=2015-01-1&endTime={1}", page_Recharge, DateTime.Now.ToString("yyyy-MM-dd"));
                string results = await WebClientClass.GetResults(new Uri(uri));

                payOrdersModel model = JsonConvert.DeserializeObject<payOrdersModel>(results);

                if (model.code == 0)
                {
                    if (model.data.list.Count != 0)
                    {
                        model.data.list.ForEach(x => list_Recharge.Items.Add(x));
                        page_Recharge++;
                    }
                    else
                    {
                        Utils.ShowMessageToast("加载完了", 3000);
                    }
                }
                else
                {
                    Utils.ShowMessageToast(model.message, 3000);
                }
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147012867)
                {
                    Utils.ShowMessageToast("检查你的网络连接！", 3000);
                }
                else
                {
                    Utils.ShowMessageToast("读取硬币发生错误\r\n" + ex.Message, 3000);
                }
            }
            finally
            {
                load_Recharge = false;
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }

        private async void list_Recharge_ItemClick(object sender, ItemClickEventArgs e)
        {
            var info = e.ClickedItem as payOrdersModel;
            var x = new ContentDialog();
            StackPanel st = new StackPanel();

            st.Children.Add(new TextBlock()
            {
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                Text = string.Format("订单号:{0}\r\n\r\n订单名称:{1}\r\n\r\n创建时间:{2}\r\n\r\n消费渠道:{3}\r\n\r\n支付金额:{4}元\r\n\r\n到账B币:{5}B币", info.orderNo, info.remark, info.ctime, info.channelName, info.money, info.bp)
            });
            x.Content = st;
            x.PrimaryButtonText = "知道了";
            x.IsPrimaryButtonEnabled = true;
            await x.ShowAsync();
        }

        private void sv_Recharge_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sv_Recharge.VerticalOffset == sv_Recharge.ScrollableHeight)
            {
                if (!load_Recharge)
                {
                    //User_load_more.IsEnabled = false;
                    //User_load_more.Content = "正在加载";
                    GetRechargeOrders();
                }
            }
        }
        int page_Quan = 1;
        bool load_Quan = false;
        private async void GetQuan()
        {
            try
            {
                load_Quan = true;
                pr_Load.Visibility = Visibility.Visible;
                string uri = string.Format("https://pay.bilibili.com/coupon/list.do?pageSize=20&pageNo={0}&beginTime=2015-01-1&endTime={1}", page_Quan, DateTime.Now.ToString("yyyy-MM-dd"));
                string results = await WebClientClass.GetResults(new Uri(uri));

                payQuanModel model = JsonConvert.DeserializeObject<payQuanModel>(results);

                if (model.code == 0)
                {
                    if (model.data.list.Count != 0)
                    {
                        model.data.list.ForEach(x => list_Quan.Items.Add(x));
                        page_Quan++;
                    }
                    else
                    {
                        Utils.ShowMessageToast("加载完了", 3000);
                    }
                }
                else
                {
                    Utils.ShowMessageToast(model.message, 3000);
                }
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147012867)
                {
                    Utils.ShowMessageToast("检查你的网络连接！", 3000);
                }
                else
                {
                    Utils.ShowMessageToast("读取硬币发生错误\r\n" + ex.Message, 3000);
                }
            }
            finally
            {
                load_Quan = false;
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }
        private void sv_Quan_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (sv_Quan.VerticalOffset == sv_Quan.ScrollableHeight)
            {
                if (!load_Quan)
                {
                    //User_load_more.IsEnabled = false;
                    //User_load_more.Content = "正在加载";
                    GetQuan();
                }
            }
        }


        private async void GetCoins()
        {
            try
            {
                pr_Load.Visibility = Visibility.Visible;
                txt_CoinsEmpty.Visibility = Visibility.Collapsed;
                string uri = "https://api.bilibili.com/x/member/web/coin/log";
                string results = await WebClientClass.GetResults(new Uri(uri));

                CoinsModel model = JsonConvert.DeserializeObject<CoinsModel>(results);

                if (model != null && model.code == 0 && model.data != null)
                {
                    var list = model.data.list ?? new List<CoinsModel>();
                    list_Coins.ItemsSource = list;
                    txt_CoinsEmpty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    Utils.ShowMessageToast(model?.message ?? "读取硬币记录失败", 3000);
                }
            }
            catch (Exception ex)
            {
                if (ex.HResult == -2147012867)
                {
                    Utils.ShowMessageToast("检查你的网络连接！", 3000);
                }
                else
                {
                    Utils.ShowMessageToast("读取硬币发生错误\r\n" + ex.Message, 3000);
                }
            }
            finally
            {
                
                pr_Load.Visibility = Visibility.Collapsed;
            }
        }
    }

    public class WalletModel
    {
        public int code { get; set; }
        public int errno { get; set; }
        public string message { get; set; }
        public string msg { get; set; }
        public string showMsg { get; set; }

        public WalletModel coupon_info { get; set; }
        public WalletModel data { get; set; }

        public WalletModel wallet { get; set; }
        public string mid { get; set; }
        public string defaultBp { get; set; }
        public string bcoin_balance { get { return (total_coupon_money / 100).ToString("0.00"); } }
        public double total_coupon_money { get; set; }

        public string coupon_desc { get; set; }
    }
    public class _UserInfoModel
    {
        public int code { get; set; }
        public string message { get; set; }
        public _UserInfoModel data { get; set; }
        [JsonProperty("money")]
        public string coins { get; set; }
       
    }

    public class payOrdersModel
    {
        public int code { get; set; }
        public string message { get; set; }
        public payOrdersModel data { get; set; }
        public string money { get; set; }
        public List<payOrdersModel> list { get; set; }
        public string orderNo { get; set; }
        public string bp { get; set; }
        public int status { get; set; }
        public string Status
        {
            get
            {
                if (status==2)
                {
                    return "支付成功";
                }
                else
                {
                    return "订单关闭";
                }
            }
        }
        public string remark { get; set; }
        public string ctime { get; set; }
        public string mtime { get; set; }
        public string rechargeMoney { get; set; }
        public int platform_type { get; set; }
        public payOrdersModel payAppInfo { get; set; }
        public string name { get; set; }
        public string channelName { get; set; }
    }
    public class payQuanModel
    {
        public int code { get; set; }
        public string message { get; set; }
        public payQuanModel data { get; set; }
        public List<payQuanModel> list { get; set; }

        public string mid { get; set; }
        public string activity_name { get; set; }
        public int coupon_status { get; set; }
        public string Status
        {
            get
            {
                switch (coupon_status)
                {
                    case 1:
                        return "使用中";
                    case 2:
                        return "已使用";
                    default:
                        return "未使用";
                }
              
                
            }
        }
        public string coupon_money { get; set; }
        public string ctime { get; set; }
        public string coupon_original { get; set; }
        public string coupon_token { get; set; }
        public string Ctime {
            get
            {
                DateTime dtStart = new DateTime(1970, 1, 1);
                long lTime = long.Parse(ctime + "0000");
                //long lTime = long.Parse(textBox1.Text);
                TimeSpan toNow = new TimeSpan(lTime);

                DateTime dt = dtStart.Add(toNow);
                return dt.ToString();
            }
        }
    }

    public class CoinsModel
    {
        public int code { get; set; }
        public string message { get; set; }
        public CoinsModel data { get; set; }
        public List<CoinsModel> list { get; set; }
        public List<CoinsModel> result { get; set; }

        public string time { get; set; }
        public string reason { get; set; }
        public double delta { get; set; }
        public string Delta
        {
            get
            {
                if (delta<0)
                {
                    return delta.ToString("0.00");
                }
                else
                {
                    return "+" + delta.ToString("0.00");
                }

            }
        }
       
       
    }

}
