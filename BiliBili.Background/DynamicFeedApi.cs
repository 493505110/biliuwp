using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using BiliBili.UWP.Helper;
using Windows.Security.Credentials;
using Windows.Web.Http;

namespace BiliBili.Background
{
    /// <summary>
    /// 关注动态的取数层，走与主应用 AttentionPage 完全相同的移动端接口。
    /// 响应解析在 DynamicFeedParser，那部分是纯逻辑、单独受单元测试覆盖。
    /// </summary>
    internal static class DynamicFeedApi
    {
        //与主应用 ApiHelper.AndroidKey / ApiHelper.build 保持一致。
        //后台任务是独立的 winmdobj，引用不到主应用程序集，只能各自声明一份。
        private const string AndroidAppKey = "4409e2ce8ffd12b8";
        private const string AndroidAppSecret = "59b43e04ad6965f34319062b478f83dd";
        private const string Build = "5520400";

        /// <summary>
        /// 从 Credential Locker 读 access_key。
        /// 旧实现读的是 LocalFolder\us.bili，但主应用从来不写那个文件，读出来恒为空。
        /// </summary>
        public static string GetAccessKey()
        {
            try
            {
                var credential = new PasswordVault()
                    .Retrieve(SettingKeys.AccessKeyResource, SettingKeys.AccessKeyUserName);
                return credential == null ? "" : (credential.Password ?? "");
            }
            catch (Exception)
            {
                //凭证不存在时 Retrieve 会抛异常，按未登录处理
                return "";
            }
        }

        public static async Task<List<FeedItem>> GetAttentionUpdateAsync()
        {
            var accessKey = GetAccessKey();
            if (accessKey.Length == 0)
            {
                await BgLog.WriteAsync("跳过：未登录，Credential Locker 中没有 access_key");
                return null;
            }

            //时间戳沿用主应用 ApiHelper.GetTimeSpan_2 的算法(毫秒 + 8 小时偏移)。
            //AttentionPage 用的就是它，而 ts 参与签名，必须逐字保持一致。
            var ts = Convert.ToInt64((DateTime.Now - new DateTime(1970, 1, 1, 8, 0, 0, 0)).TotalMilliseconds);
            var url = "https://api.vc.bilibili.com/dynamic_svr/v1/dynamic_svr/dynamic_new"
                + "?access_key=" + accessKey
                + "&appkey=" + AndroidAppKey
                + "&build=" + Build
                + "&mobi_app=android&platform=android&qn=32&rsp_type=2&src=bili"
                + "&ts=" + ts
                + "&type_list=8%2C512%2C4099"
                + "&uid=" + SettingHelper.Get_UserID()
                + "&update_num_dy_id=0";
            url += "&sign=" + SignHelper.SignUrlValue(url, AndroidAppSecret);

            string results;
            using (var hc = new HttpClient())
            {
                var hr = await hc.GetAsync(new Uri(url));
                hr.EnsureSuccessStatusCode();
                //接口固定返回 UTF-8，按 buffer 手动解码，与主应用 WebClientClass 的做法一致
                var buffer = await hr.Content.ReadAsBufferAsync();
                var bytes = buffer.ToArray();
                results = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            }

            var parsed = DynamicFeedParser.ParseFeed(results);
            if (parsed.Code != 0)
            {
                await BgLog.WriteAsync("接口返回失败 code=" + parsed.Code + " message=" + parsed.Message);
                return null;
            }
            return parsed.Items;
        }
    }
}
