using Newtonsoft.Json;
using BiliBili.UWP.Api;
using BiliBili.UWP.Api.Live;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
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
    public sealed partial class LiveMyMedalPage : Page
    {
        public LiveMyMedalPage()
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
            if (e.NavigationMode == NavigationMode.New)
            {
                LoadData();
            }
        }
        private async void LoadData()
        {
            try
            {
                pr_Load.Visibility = Visibility.Visible;
                var medals = new List<LiveMedalModel>();
                var page = 1;
                var totalPages = 1;
                while (page <= totalPages)
                {
                    var response = await LiveRoomAPI.GetMyMedals(page, 10).Request();
                    var root = response.GetJObject();
                    if (!response.status || root == null || root.Value<int?>("code") != 0)
                    {
                        Utils.ShowMessageToast(root?["message"]?.ToString() ?? response.message, 3000);
                        return;
                    }
                    var items = root["data"]?["items"]?.ToObject<List<LiveMedalModel>>() ?? new List<LiveMedalModel>();
                    foreach (var medal in items)
                    {
                        medal.color = medal.medal_color_start.ToString();
                        medals.Add(medal);
                    }
                    totalPages = root["data"]?["page_info"]?.Value<int?>("total_page") ?? 1;
                    page++;
                }
                NoDT.Visibility = medals.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                list.ItemsSource = medals;
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

       

        private void btn_Edit_Click(object sender, RoutedEventArgs e)
        {

        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var medal = (sender as Button).DataContext as LiveMedalModel;
            if (medal.status == 1)
            {
                Cancel();
            }
            else
            {
                Add(medal.medal_id);
            }
        }
        private async void Cancel()
        {
            await new Windows.UI.Popups.MessageDialog("当前接口不支持卸下粉丝勋章").ShowAsync();
        }
        private async void Add(int id)
        {
            try
            {
                pr_Load.Visibility = Visibility.Visible;

                var response = await LiveRoomAPI.WearMedal(id).Request();
                var root = response.GetJObject();
                if (response.status && root != null && root.Value<int?>("code") == 0)
                {
                    Utils.ShowMessageToast("操作成功", 3000);
                    LoadData();
                }
                else
                {
                    Utils.ShowMessageToast(root?["message"]?.ToString() ?? response.message, 3000);
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

    }

    public class LiveMedalModel
    {
        public int code { get; set; }
        public string message { get; set; }
        public List<LiveMedalModel> data{ get; set; }

        public int medal_id { get; set; }
        public int medal_color_start { get; set; }
        public string medal_name { get; set; }
        public string level { get; set; }
        public string uname { get; set; }
        public string intimacy { get; set; }
        public string next_intimacy { get; set; }
        public int status { get; set; }
        public string Status
        {
            get
            {
                if (status==1)
                {
                    return "已佩戴";
                }
                else
                {
                    return "佩戴";
                }
            }
        }
        public string color { get; set; }
        public SolidColorBrush _Color
        {
            get
            {
                try
                {
                    color = Convert.ToInt32(color).ToString("X2");
                    if (color.StartsWith("#"))
                        color = color.Replace("#", string.Empty);
                    int v = int.Parse(color, System.Globalization.NumberStyles.HexNumber);
                    SolidColorBrush solid = new SolidColorBrush(new Color()
                    {
                        A = Convert.ToByte(255),
                        R = Convert.ToByte((v >> 16) & 255),
                        G = Convert.ToByte((v >> 8) & 255),
                        B = Convert.ToByte((v >> 0) & 255)
                    });
                    // color = solid;
                    return solid;
                }
                catch (Exception)
                {
                    SolidColorBrush solid = new SolidColorBrush(new Color()
                    {
                        A = 255,
                        R = 255,
                        G = 255,
                        B = 255
                    });
                    // color = solid;
                    return solid;
                }
            }
        }
        public int guard_type { get; set; }
        public string buff_msg { get; set; }
    }


}
