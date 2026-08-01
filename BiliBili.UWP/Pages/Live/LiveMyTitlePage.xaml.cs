using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
    public sealed partial class LiveMyTitlePage : Page
    {
        public LiveMyTitlePage()
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
            pr_Load.Visibility = Visibility.Collapsed;
            list.ItemsSource = null;
            NoDT.Visibility = Visibility.Visible;
            await new Windows.UI.Popups.MessageDialog("直播头衔功能已下线").ShowAsync();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button).Content.ToString() == "卸下")
            {
                Cancel();
            }
            else
            {
                Add(((sender as Button).DataContext as LiveTitleModel).title);
            }
        }
        private async void Cancel()
        {
            await new Windows.UI.Popups.MessageDialog("直播头衔功能已下线").ShowAsync();
        }
        private async void Add(string title)
        {
            await new Windows.UI.Popups.MessageDialog("直播头衔功能已下线").ShowAsync();
        }

    }
    public class LiveTitleModel
    {
        public int code { get; set; }
        public string message { get; set; }
        public LiveTitleModel data { get; set; }
         
        public List<LiveTitleModel> list { get; set; }

        public string uid { get; set; }
        public bool had { get; set; }
        public string title { get; set; }
        public string activity { get; set; }


        public int status { get; set; }
        public string Status
        {
            get
            {
                if (status == 1)
                {
                    return "卸下";
                }
                else
                {
                    return "佩戴";
                }
            }
        }

        public LiveTitleModel title_pic { get; set; }
        public string id { get; set; }
        public string img { get; set; }
    }


}
