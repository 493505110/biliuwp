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
        private static CoreWebView2CookieManager cookieManager;

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
            Register(webView);
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
            Register(webView);
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

        public static Task CopyToWebViewAsync(CoreWebView2 webView)
        {
            Register(webView);
            try
            {
                var filter = new HttpBaseProtocolFilter();
                var copied = new HashSet<string>();
                foreach (var origin in BilibiliOrigins)
                {
                    var originUri = new Uri(origin);
                    var cookies = filter.CookieManager.GetCookies(originUri);
                    foreach (var item in cookies)
                    {
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
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("回写cookie到WebView2失败", LogType.ERROR, ex);
            }
            return Task.CompletedTask;
        }

        public static void Register(CoreWebView2 webView)
        {
            cookieManager = webView.CookieManager;
            UserManage.ClearWebViewCookies = ClearCookies;
        }

        private static void ClearCookies()
        {
            try
            {
                cookieManager?.DeleteAllCookies();
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("清除WebView2 cookie失败", LogType.ERROR, ex);
            }
        }
    }
}
