using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft;
using Newtonsoft.Json;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace BiliBili.UWP.Helper
{
    public class AppHelper
    {
        public async void GetDeveloperMessage()
        {
            try
            {
                var results = await WebClientClass.GetResultsUTF8Encode(new Uri("http://pic.iliili.cn/bilimessageV3.json?rnd=" + ApiHelper.GetTimeSpan_2));
                DeveloperMessageModel messageModel = JsonConvert.DeserializeObject<DeveloperMessageModel>(results);
                if (!messageModel.showAD)
                {
                    MessageCenter.SendHideAd();
                }
                if (Get_FirstShowMessage(messageModel.messageId) && messageModel.startdate < DateTime.Now && messageModel.enddate > DateTime.Now)
                {
                    var cd = new ContentDialog();
                    StackPanel stackPanel = new StackPanel();
                    //TextBlock title = new TextBlock() {
                    //    Text= messageModel.title,
                    //    TextWrapping= Windows.UI.Xaml.TextWrapping.Wrap,
                    //    IsTextSelectionEnabled = true
                    //};
                    //stackPanel.Children.Add(title);
                    cd.Title = messageModel.title;
                    TextBlock content = new TextBlock()
                    {
                        Text = messageModel.message,
                        TextWrapping = Windows.UI.Xaml.TextWrapping.Wrap,
                        IsTextSelectionEnabled = true
                    };
                    stackPanel.Children.Add(content);
                    cd.Content = stackPanel;
                    cd.PrimaryButtonText = "不再显示";
                    cd.SecondaryButtonText = "知道了";

                    cd.PrimaryButtonClick += new Windows.Foundation.TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs>((sender, e) =>
                    {
                        Set_FirstShowMessage(messageModel.messageId, false);
                    });
                    await cd.ShowAsync();
                }

            }
            catch (Exception)
            {

            }

        }

        static ApplicationDataContainer container;
        public static bool Get_FirstShowMessage(string code)
        {
            container = ApplicationData.Current.LocalSettings;
            if (container.Values["FirstShowMessage" + code] != null)
            {
                return (bool)container.Values["FirstShowMessage" + code];
            }
            else
            {
                Set_FirstShowMessage(code, true);
                return true;
            }
        }

        public static void Set_FirstShowMessage(string code, bool value)
        {
            container = ApplicationData.Current.LocalSettings;
            container.Values["FirstShowMessage" + code] = value;
        }

        public static string GetLastVersionStr()
        {
            if (Changelog != null && Changelog.Count > 0)
            {
                var v = Changelog[0];
                var sb = new StringBuilder();
                sb.Append("Ver ").Append(v.Version).Append(" ").Append(v.Date);
                int i = 1;
                foreach (var item in v.Items)
                {
                    sb.Append("\n").Append(i.ToString("00")).Append("、").Append(item);
                    i++;
                }
                return sb.ToString();
            }
            return verStr.Split('/')[0];
        }

        public static string verStr { get; private set; } = "";
        public static List<VersionLog> Changelog { get; private set; } = new List<VersionLog>();

        /// <summary>
        /// 异步加载 CHANGELOG.md(打包进应用的内容文件)并解析为结构化更新日志
        /// </summary>
        public static async Task LoadChangelogAsync()
        {
            try
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///CHANGELOG.md"));
                var text = await FileIO.ReadTextAsync(file);
                Changelog = ParseChangelog(text);
                verStr = ChangelogToVerStr(text);
            }
            catch (Exception)
            {
                Changelog = new List<VersionLog>();
                verStr = "";
            }
        }

        /// <summary>
        /// 解析 CHANGELOG.md(Markdown: ## 版本 (日期) + - 条目)为结构化版本日志列表
        /// </summary>
        private static List<VersionLog> ParseChangelog(string md)
        {
            var list = new List<VersionLog>();
            VersionLog current = null;
            foreach (var line in md.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                if (line.StartsWith("## "))
                {
                    var title = line.Substring(3).Trim();
                    // ## 3.13.1 (2026-08-11) -> version=3.13.1, date=2026-08-11
                    // ## 3.9.8.0 (2018-11)  -> version=3.9.8.0, date=2018-11(老版本仅年月)
                    var m = System.Text.RegularExpressions.Regex.Match(title, @"^(.+?)\s*\(?(\d{4}-\d{1,2}(?:-\d{1,2})?)\)?$");
                    var version = m.Success ? m.Groups[1].Value.Trim() : title;
                    var date = m.Success ? m.Groups[2].Value : "";
                    current = new VersionLog { Version = version, Date = date, Items = new List<string>() };
                    list.Add(current);
                }
                else if (line.StartsWith("- ") && current != null)
                {
                    var item = line.Substring(2).Trim();
                    if (item.Length > 0) current.Items.Add(item);
                }
            }
            return list;
        }

        /// <summary>
        /// 将 CHANGELOG.md(Markdown: ## 版本 (日期) + - 条目)转换为应用内显示格式
        /// (/Ver 版本 日期\n编号、条目...)，与旧硬编码 verStr 格式一致
        /// </summary>
        private static string ChangelogToVerStr(string md)
        {
            var lines = md.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            int itemNo = 1;
            bool firstBlock = true;
            foreach (var line in lines)
            {
                if (line.StartsWith("## "))
                {
                    // ## 3.13.1 (2026-08-11)  ->  Ver 3.13.1 2026-08-11 (首块不带 /,后续块带 /,兼容 GetLastVersionStr 的 Split('/')[0])
                    var title = line.Substring(3).Trim();
                    // ## 3.13.1 (2026-08-11) -> 3.13.1 2026-08-11; ## 3.13.0 2026-08-04 原样保留
                    title = title.Replace(" (", " ").Replace("(", "").Replace(")", "");
                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine(); // 版本块之间留空行,与旧硬编码格式一致
                    }
                    if (!firstBlock) sb.Append("/");
                    sb.Append("Ver ").Append(title);
                    firstBlock = false;
                    itemNo = 1;
                }
                else if (line.StartsWith("- "))
                {
                    var item = line.Substring(2).Trim();
                    if (item.Length > 0)
                    {
                        sb.AppendLine();
                        sb.Append(itemNo.ToString("00")).Append("、").Append(item);
                        itemNo++;
                    }
                }
                // 其他行(# 标题/空行)忽略
            }
            return sb.ToString();
        }


    }

    public class DeveloperMessageModel
    {
        public string title { get; set; }
        public string messageId { get; set; }
        public string message { get; set; }
        public DateTime startdate { get; set; }
        public DateTime enddate { get; set; }
        public bool showAD { get; set; }
    }

    /// <summary>
    /// 单个版本的更新日志(结构化,用于应用内更新日志页渲染)
    /// </summary>
    public class VersionLog
    {
        public string Version { get; set; }
        public string Date { get; set; }
        public List<string> Items { get; set; }
    }

}
