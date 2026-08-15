using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace BiliBili.UWP.Helper
{
    public class WbiEncodeHelper
    {
        private static readonly int[] MixinKeyEncTab =
        {
            46, 47, 18, 2, 53, 8, 23, 32, 15, 50, 10, 31, 58, 3, 45, 35, 27, 43, 5, 49, 33, 9, 42, 19, 29, 28, 14, 39,
            12, 38, 41, 13, 37, 48, 7, 16, 24, 55, 40, 61, 26, 17, 0, 1, 60, 51, 30, 4, 22, 25, 54, 21, 56, 59, 6, 63,
            57, 62, 11, 36, 20, 34, 44, 52
        };

        // 对 imgKey 与 subKey 按打乱表重排，取前 32 位作为 mixin key
        private static string GetMixinKey(string orig)
        {
            return MixinKeyEncTab.Aggregate("", (s, i) => s + orig[i]).Substring(0, 32);
        }

        // 对参数做 form-urlencoded 序列化（与 FormUrlEncodedContent 行为一致，纯同步避免 .Result 阻塞）
        private static string BuildFormQuery(IEnumerable<KeyValuePair<string, string>> parameters)
        {
            return string.Join("&", parameters.Select(kvp =>
                Uri.EscapeDataString(kvp.Key).Replace("%20", "+") + "=" + Uri.EscapeDataString(kvp.Value).Replace("%20", "+")));
        }

        /// <summary>
        /// 对参数做 Wbi 签名，返回带 wts 与 w_rid 的参数表。
        /// timestamp 可注入固定时间戳（用于单元测试），默认用当前 Unix 时间。
        /// </summary>
        public static Dictionary<string, string> EncWbi(Dictionary<string, string> parameters, string imgKey,
            string subKey, string timestamp = null)
        {
            string mixinKey = GetMixinKey(imgKey + subKey);
            string currTime = timestamp ?? DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
            // 添加 wts 字段
            parameters["wts"] = currTime;
            // 按 key 重排参数
            parameters = parameters.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value);
            // 过滤 value 中的 "!'()*" 字符
            parameters = parameters.ToDictionary(
                kvp => kvp.Key,
                kvp => new string(kvp.Value.Where(chr => !"!'()*".Contains(chr)).ToArray())
            );
            // 序列化参数（同步，避免 FormUrlEncodedContent.ReadAsStringAsync().Result 阻塞 UI 线程）
            string query = BuildFormQuery(parameters);
            // 计算 w_rid
            MD5 md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(query + mixinKey));
            string wbiSign = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            parameters["w_rid"] = wbiSign;

            return parameters;
        }
    }
}