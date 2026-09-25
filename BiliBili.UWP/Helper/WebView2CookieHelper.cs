using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace BiliBili.UWP.Helper
{
    public static class WebView2CookieHelper
    {
        /// <summary>
        /// 由常驻页面（MainPage）注入：借出一个可用于清理的 CoreWebView2。
        /// 注销时用它清 Chromium 侧登录状态。载体用完即弃，不留在可视树上。
        /// </summary>
        public static Func<Task<CoreWebView2>> CleanupHostProvider;

        /// <summary>
        /// 由常驻页面（MainPage）注入：归还并销毁 CleanupHostProvider 借出的载体。
        /// 必须在清理结束后调用，否则 Chromium 渲染进程会一直驻留。
        /// </summary>
        public static Action CleanupHostReleaser;

        private static readonly string[] BilibiliOrigins =
        {
            "https://www.bilibili.com/",
            "https://passport.bilibili.com/"
        };

        private static readonly HashSet<string> SharedLoginCookieNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "SESSDATA",
            "DedeUserID",
            "DedeUserID__ckMd5",
            "bili_jct",
            "sid"
        };

        public static async Task<string> GetCookieAsync(CoreWebView2 webView, string name)
        {
            try
            {
                var cookies = await webView.CookieManager.GetCookiesAsync("https://www.bilibili.com");
                foreach (var cookie in cookies)
                {
                    if (cookie.Name == name && !string.IsNullOrEmpty(cookie.Value))
                    {
                        return cookie.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("读取WebView2 cookie失败", LogType.ERROR, ex);
            }
            return string.Empty;
        }

        public static async Task CopyToHttpClientAsync(CoreWebView2 webView)
        {
            try
            {
                var filter = new HttpBaseProtocolFilter();
                foreach (var origin in BilibiliOrigins)
                {
                    var cookies = await webView.CookieManager.GetCookiesAsync(origin);
                    foreach (var item in cookies)
                    {
                        try
                        {
                            var domain = string.IsNullOrEmpty(item.Domain)
                                ? "bilibili.com"
                                : item.Domain.TrimStart('.');
                            var path = string.IsNullOrEmpty(item.Path) ? "/" : item.Path;
                            var cookie = new HttpCookie(item.Name, domain, path)
                            {
                                Value = item.Value,
                                HttpOnly = item.IsHttpOnly,
                                Secure = item.IsSecure
                            };
                            if (!item.IsSession && item.Expires > 0)
                            {
                                cookie.Expires = DateTimeOffset.FromUnixTimeSeconds((long)item.Expires);
                            }
                            filter.CookieManager.SetCookie(cookie);
                        }
                        catch (Exception)
                        {
                            // 个别 Cookie 的域名或属性不被 WinRT 接受时跳过。
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("拷贝WebView2 cookie失败", LogType.ERROR, ex);
            }
        }

        public static async Task CopyToWebViewAsync(CoreWebView2 webView)
        {
            try
            {
                var filter = new HttpBaseProtocolFilter();
                var copied = new HashSet<string>();
                //WinRT 侧登录凭证的实际名字集合，用来判定 WebView2 里的同组 cookie 是否已过期
                var loginCookiesInApp = new HashSet<string>(StringComparer.Ordinal);
                foreach (var origin in BilibiliOrigins)
                {
                    var originUri = new Uri(origin);
                    var cookies = filter.CookieManager.GetCookies(originUri);
                    foreach (var item in cookies)
                    {
                        if (SharedLoginCookieNames.Contains(item.Name))
                        {
                            loginCookiesInApp.Add(item.Name);
                        }
                        var isSharedLoginCookie = SharedLoginCookieNames.Contains(item.Name);
                        var domain = isSharedLoginCookie
                            ? ".bilibili.com"
                            : (string.IsNullOrEmpty(item.Domain) ? originUri.Host : item.Domain);
                        var path = isSharedLoginCookie || string.IsNullOrEmpty(item.Path) ? "/" : item.Path;
                        if (!copied.Add(item.Name + "\n" + domain + "\n" + path))
                        {
                            continue;
                        }

                        try
                        {
                            var cookie = webView.CookieManager.CreateCookie(item.Name, item.Value, domain, path);
                            cookie.IsHttpOnly = item.HttpOnly;
                            cookie.IsSecure = item.Secure;
                            if (item.Expires.HasValue)
                            {
                                cookie.Expires = item.Expires.Value.ToUnixTimeSeconds();
                            }
                            webView.CookieManager.AddOrUpdateCookie(cookie);
                        }
                        catch (Exception)
                        {
                            // 个别 Cookie 的域名或属性不被 Chromium 接受时跳过。
                        }
                    }
                }
                //只写不删会让 WebView2 里留着 App 已不认的登录凭证（换号、注销残留），
                //导致网页仍是旧账号的登录态，所以登录凭证组要和 WinRT 严格对齐。
                await RemoveStaleLoginCookiesAsync(webView, loginCookiesInApp);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("回写cookie到WebView2失败", LogType.ERROR, ex);
            }
        }

        /// <summary>
        /// 删除 WebView2 中属于登录凭证组、但 App 侧已经不存在的 cookie
        /// </summary>
        private static async Task RemoveStaleLoginCookiesAsync(CoreWebView2 webView, HashSet<string> loginCookiesInApp)
        {
            foreach (var origin in BilibiliOrigins)
            {
                var cookies = await webView.CookieManager.GetCookiesAsync(origin);
                foreach (var item in cookies)
                {
                    if (SharedLoginCookieNames.Contains(item.Name) && !loginCookiesInApp.Contains(item.Name))
                    {
                        try
                        {
                            webView.CookieManager.DeleteCookie(item);
                        }
                        catch (Exception)
                        {
                            // 单个 cookie 删除失败不影响其余清理。
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 注销时清 Chromium 侧的登录状态。
        /// 只删 cookie 不够：B站网页的登录态还会落到 localStorage / IndexedDB，
        /// 且 WebView2 与 WinRT HttpClient 不共用存储，必须单独清。
        /// 载体用完立即释放——WebView2 每实例都会拉起一组渲染进程，常驻不划算。
        /// </summary>
        public static async Task ClearAllAsync()
        {
            var provider = CleanupHostProvider;
            if (provider == null)
            {
                LogHelper.WriteLog("未注册WebView2清理载体，跳过清除", LogType.INFO);
                return;
            }
            try
            {
                var webView = await provider();
                if (webView == null)
                {
                    return;
                }
                await webView.Profile.ClearBrowsingDataAsync(
                    CoreWebView2BrowsingDataKinds.Cookies |
                    CoreWebView2BrowsingDataKinds.AllDomStorage |
                    CoreWebView2BrowsingDataKinds.ServiceWorkers |
                    CoreWebView2BrowsingDataKinds.CacheStorage);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("清除WebView2数据失败", LogType.ERROR, ex);
            }
            finally
            {
                //无论清理成功与否都要归还载体，否则会漏一个常驻渲染进程
                try
                {
                    CleanupHostReleaser?.Invoke();
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("释放WebView2清理载体失败", LogType.ERROR, ex);
                }
            }
        }
    }
}
