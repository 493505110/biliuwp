using Newtonsoft.Json.Linq;
using BiliBili.UWP.Api;
using BiliBili.UWP.Helper;
using BiliBili.UWP.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Newtonsoft.Json;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace BiliBili.UWP.Pages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class DMHideManagePage : Page
    {
        private readonly PlayerAPI playerAPI = new PlayerAPI();

        public DMHideManagePage()
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
                LoadSetting();
            }
        }

        private void LoadSetting()
        {
            txt_DM.Text="<d p=\"65.460998535156,1,25,16777215,1486119598,0,313ee262,2946614352\">这是一条正常的弹幕</d>";
            txt_SM.Text = "弹幕格式说明：<d p=\"弹幕出现时间,弹幕模式（1-3 滚动弹幕 4底端弹幕 5顶端弹幕 6.逆向弹幕 7精准定位 8高级弹幕）,弹幕大小（12非常小,16特小,18小,25中,36大,45很大,64特别大）,弹幕颜色（十进制）,弹幕发送时间（时间戳）,弹幕池（0普通池 1字幕池 2特殊池 【目前特殊池为高级弹幕专用】）,弹幕发送人,弹幕ID\">弹幕文本</d>";

            string a = SettingHelper.Get_Guanjianzi();
            if (a.Length != 0)
            {
                list_Guanjianzi.Items.Clear();
                foreach (var item in a.Split('|').ToList())
                {
                    list_Guanjianzi.Items.Add(item);
                }
                list_Guanjianzi.Items.Remove(string.Empty);
            }

            string b = SettingHelper.Get_Yonghu();
            if (b.Length != 0)
            {
                list_Yonghu.Items.Clear();
                foreach (var item in b.Split('|').ToList())
                {
                    list_Yonghu.Items.Add(item);
                }
                list_Yonghu.Items.Remove(string.Empty);
            }
            txt_ZZ.Text = SettingHelper.Get_DMZZ();


        }


        private void btn_AddYonghu_Click(object sender, RoutedEventArgs e)
        {
            // string b = (string)settings.GetSettingValue("Yonghu") + "|" + txt_Yonghu.Text;
            //settings.SetSettingValue("Yonghu", b);
            if (txt_Yonghu.Text.Length == 0)
            {
                txt_Yonghu.Text = "用户不能为空";
                return;
            }
            SettingHelper.Set_Yonghu(SettingHelper.Get_Yonghu() + "|" + txt_Yonghu.Text);
            list_Yonghu.Items.Add(txt_Yonghu.Text);
            txt_Yonghu.Text = string.Empty;
        }

        private void btn_AddGuanjianzi_Click(object sender, RoutedEventArgs e)
        {
            if (txt_Guanjianzi.Text.Length==0)
            {
                txt_Guanjianzi.Text = "关键字不能为空";
                return;
            }
            SettingHelper.Set_Guanjianzi(SettingHelper.Get_Guanjianzi() + "|" + txt_Guanjianzi.Text);
            list_Guanjianzi.Items.Add(txt_Guanjianzi.Text);
            txt_Guanjianzi.Text = string.Empty;
        }


        private async void btn_DeleteGuanjianzi_Click(object sender, RoutedEventArgs e)
        {
            await DeleteSelectedAsync(list_Guanjianzi);
        }

        private async void btn_DeleteYonghu_Click(object sender, RoutedEventArgs e)
        {
            await DeleteSelectedAsync(list_Yonghu);
        }

        private async Task DeleteSelectedAsync(ListView list)
        {
            var selectedItems = list.SelectedItems.Cast<string>().ToList();
            if (selectedItems.Count == 0)
            {
                return;
            }

            if (!ApiHelper.IsLogin())
            {
                Utils.ShowMessageToast("请先登录", 3000);
                return;
            }

            var response = await playerAPI.GetDanmuFilterWords().Request();
            if (!response.status)
            {
                Utils.ShowMessageToast("删除失败，" + response.message, 3000);
                return;
            }

            var filter = JsonConvert.DeserializeObject<DMFilterModel>(response.results);
            if (filter == null || filter.code != 0)
            {
                Utils.ShowMessageToast("删除失败，" + (filter?.message ?? "服务器返回数据格式错误"), 3000);
                return;
            }

            var rules = filter?.data?.rule ?? new List<DMFilterModel>();
            var csrf = Account.GetCookieValue("bili_jct");
            if (string.IsNullOrEmpty(csrf))
            {
                Utils.ShowMessageToast("删除失败，登录 Cookie 缺少 bili_jct，请重新登录", 3000);
                return;
            }

            var failures = new List<string>();
            var pendingVerification = new List<Tuple<string, int>>();

            foreach (var item in selectedItems)
            {
                var rule = rules.FirstOrDefault(x => x.filter == item);
                if (rule == null)
                {
                    failures.Add(item + "：云端未找到对应规则");
                    continue;
                }

                var deleteResponse = await playerAPI.DeleteDanmuFilterWord(rule.id, csrf).Request();
                if (!deleteResponse.status)
                {
                    failures.Add(item + "：" + deleteResponse.message);
                    continue;
                }

                var result = deleteResponse.GetJObject();
                if (result?.Value<int?>("code") == 0)
                {
                    pendingVerification.Add(Tuple.Create(item, rule.type));
                }
                else
                {
                    failures.Add(item + "：" + (result?.Value<string>("message") ?? "接口返回错误"));
                }
            }

            var deletedItems = new List<string>();
            if (pendingVerification.Count > 0)
            {
                var verifyResponse = await playerAPI.GetDanmuFilterWords().Request();
                var verifyFilter = verifyResponse.status
                    ? JsonConvert.DeserializeObject<DMFilterModel>(verifyResponse.results)
                    : null;
                if (verifyFilter == null || verifyFilter.code != 0)
                {
                    var message = verifyFilter?.message ?? verifyResponse.message ?? "服务器返回数据格式错误";
                    failures.Add("删除后验证失败：" + message);
                }
                else
                {
                    var remainingRules = verifyFilter.data?.rule ?? new List<DMFilterModel>();
                    foreach (var pending in pendingVerification)
                    {
                        if (remainingRules.Any(x => x.type == pending.Item2 && x.filter == pending.Item1))
                        {
                            failures.Add(pending.Item1 + "：云端删除后仍存在");
                        }
                        else
                        {
                            deletedItems.Add(pending.Item1);
                        }
                    }
                }
            }

            if (deletedItems.Count > 0)
            {
                var setting = list == list_Guanjianzi
                    ? SettingHelper.Get_Guanjianzi()
                    : SettingHelper.Get_Yonghu();
                foreach (var item in deletedItems)
                {
                    list.Items.Remove(item);
                    setting = RemoveSettingItem(setting, item);
                }

                if (list == list_Guanjianzi)
                {
                    SettingHelper.Set_Guanjianzi(setting);
                }
                else
                {
                    SettingHelper.Set_Yonghu(setting);
                }
            }

            if (failures.Count > 0)
            {
                Utils.ShowMessageToast("部分删除失败：" + failures[0], 5000);
            }
            else
            {
                Utils.ShowMessageToast("删除成功", 3000);
            }
        }

        private static string RemoveSettingItem(string setting, string item)
        {
            return string.Join("|", (setting ?? string.Empty)
                .Split('|')
                .Where(x => !string.Equals(x, item, StringComparison.Ordinal)));
        }

        private void btn_SaveZZ_Click(object sender, RoutedEventArgs e)
        {
            SettingHelper.Set_DMZZ(txt_ZZ.Text);
        }

        private void btn_TestZZ_Click(object sender, RoutedEventArgs e)
        {
            if (txt_ZZ.Text.Length==0)
            {
                txt_Results.Text = "正则表达式不能为空";
                return;
            }
            if (txt_DM.Text.Length==0)
            {
                txt_Results.Text = "测试弹幕文本不能为空";
                return;
            }

            try
            {
                if (Regex.IsMatch(txt_DM.Text, txt_ZZ.Text))
                {
                    txt_Results.Text = "弹幕测试通过";
                }
                else
                {
                    txt_Results.Text = "弹幕测试不通过";
                }
            }
            catch (Exception ex)
            {
                txt_Results.Text = "测试错误\r\n\r\n" + ex.Message;
            }


        }

        private async void btn_GetGuanjianzi_Click(object sender, RoutedEventArgs e)
        {
            if (!ApiHelper.IsLogin())
            {
                Utils.ShowMessageToast("请先登录", 3000);
            }
            else
            {
                await GetFilter();
            }
        }

        private async Task GetFilter()
        {
            try
            {
                var response = await playerAPI.GetDanmuFilterWords().Request();
                if (!response.status)
                {
                    Utils.ShowMessageToast("同步失败，" + response.message, 3000);
                    return;
                }

                var localWords = SettingHelper.Get_Guanjianzi().Split('|').ToList();
                localWords.Remove(string.Empty);
                var localUsers = SettingHelper.Get_Yonghu().Split('|').ToList();
                localUsers.Remove(string.Empty);

                var filter = JsonConvert.DeserializeObject<DMFilterModel>(response.results);
                if (filter == null)
                {
                    Utils.ShowMessageToast("同步失败，服务器返回数据格式错误", 3000);
                    return;
                }

                if (filter.code != 0)
                {
                    Utils.ShowMessageToast("同步失败，" + filter.message, 3000);
                    return;
                }

                var rules = filter.data?.rule ?? new List<DMFilterModel>();
                foreach (var item in rules)
                {
                    if (item.type == 0 && !localWords.Contains(item.filter))
                    {
                        SettingHelper.Set_Guanjianzi(SettingHelper.Get_Guanjianzi() + "|" + item.filter);
                    }
                    if (item.type == 2 && !localUsers.Contains(item.filter))
                    {
                        SettingHelper.Set_Yonghu(SettingHelper.Get_Yonghu() + "|" + item.filter);
                    }
                }

                var cloudWords = rules.Where(x => x.type == 0).Select(x => x.filter).ToList();
                var cloudUsers = rules.Where(x => x.type == 2).Select(x => x.filter).ToList();
                var csrf = Account.GetCookieValue("bili_jct");
                var uploadErrors = new List<string>();
                var uploadCount = 0;

                foreach (var item in localWords)
                {
                    if (!cloudWords.Contains(item))
                    {
                        var error = await AddInfo(0, item, csrf);
                        if (error == null)
                        {
                            uploadCount++;
                        }
                        else
                        {
                            uploadErrors.Add(error);
                        }
                    }
                }
                foreach (var item in localUsers)
                {
                    if (!cloudUsers.Contains(item))
                    {
                        var error = await AddInfo(2, item, csrf);
                        if (error == null)
                        {
                            uploadCount++;
                        }
                        else
                        {
                            uploadErrors.Add(error);
                        }
                    }
                }

                LoadSetting();
                if (uploadErrors.Count > 0)
                {
                    Utils.ShowMessageToast($"同步完成，上传成功 {uploadCount} 项，失败 {uploadErrors.Count} 项：{uploadErrors[0]}", 5000);
                }
                else
                {
                    Utils.ShowMessageToast("同步完成", 3000);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("同步弹幕屏蔽失败", LogType.ERROR, ex);
                Utils.ShowMessageToast("同步失败，" + ex.Message, 3000);
            }
        }

        private async Task<string> AddInfo(int type, string data, string csrf)
        {
            if (string.IsNullOrEmpty(csrf))
            {
                return "登录 Cookie 缺少 bili_jct，请重新登录";
            }

            try
            {
                var response = await playerAPI.AddDanmuFilterWord(data, type, csrf).Request();
                if (!response.status)
                {
                    return response.message;
                }

                var result = response.GetJObject();
                if (result == null)
                {
                    return "服务器返回数据格式错误";
                }

                var code = result.Value<int?>("code");
                if (code == 0)
                {
                    return null;
                }

                return result.Value<string>("message") ?? $"接口错误：{code}";
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("上传弹幕屏蔽项失败", LogType.ERROR, ex);
                return ex.Message;
            }
        }


    }

    public class DMFilterModel
    {
        public int code { get; set; }
        public string message { get; set; }
        public DMFilterModel data { get; set; }

        public List<DMFilterModel> rule { get; set; }

        public int id { get; set; }
        public int mid { get; set; }
        public int type { get; set; }
        public string filter { get; set; }
        public string comment { get; set; }
    }


}
