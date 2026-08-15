using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// B 站客户端请求签名算法：参数排序 + Secret 拼接 + MD5。
    /// 纯逻辑、无 UWP 依赖，可被单元测试覆盖。
    /// </summary>
    public static class SignHelper
    {
        /// <summary>对已拼好的 query（形如 "a=1&amp;b=2"）做 MD5 签名，返回小写 hex，不含 sign 前缀。</summary>
        public static string SignQuery(string query, string secret)
        {
            var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(query + secret));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        /// <summary>从完整 URL 中提取参数串、按字符排序后签名，返回 "&amp;sign=..."。</summary>
        public static string SignUrl(string url, string appKeySecret)
        {
            return "&sign=" + SignUrlValue(url, appKeySecret);
        }

        /// <summary>从完整 URL 中提取参数串、按字符排序后签名，返回纯 sign 值（不含前缀）。</summary>
        public static string SignUrlValue(string url, string appKeySecret)
        {
            var str = url.Substring(url.IndexOf("?", 4) + 1);
            var list = str.Split('&').ToList();
            list.Sort();
            var sb = new StringBuilder();
            foreach (var item in list)
            {
                sb.Append(sb.Length > 0 ? "&" : "");
                sb.Append(item);
            }
            return SignQuery(sb.ToString(), appKeySecret);
        }

        /// <summary>对参数字典按 key 升序拼装后签名，返回 "&amp;sign=..."。</summary>
        public static string SignParameters(IDictionary<string, string> pars, string appKeySecret)
        {
            var sb = new StringBuilder();
            foreach (var kv in pars.OrderBy(x => x.Key))
            {
                sb.Append(kv.Key);
                sb.Append("=");
                sb.Append(kv.Value);
                sb.Append("&");
            }
            var results = sb.ToString().TrimEnd('&');
            return "&sign=" + SignQuery(results, appKeySecret);
        }
    }
}