using Newtonsoft.Json.Linq;
using System;

namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// 启动开屏图的选取逻辑。抽成纯函数（只依赖 Newtonsoft.Json，不碰 UWP 运行时），
    /// 以便被 tests/BiliBili.Tests 直接编译并做单元测试。
    ///
    /// 接口 app.bilibili.com/x/v2/splash/brand/list 的响应结构里：
    /// show 是「当前投放项」（含 id / duration），list 是「候选图列表」（含 id / thumb），
    /// 需要按 show[0].id 去 list 里找出对应的图片地址。
    /// </summary>
    public static class SplashImageSelector
    {
        /// <summary>按 show 的当前投放 id 在 list 中取图片地址；无投放或找不到时返回 null。</summary>
        public static string SelectThumb(JArray show, JArray list)
        {
            if (list == null)
            {
                return null;
            }

            var id = SelectShowId(show);
            if (id < 0)
            {
                return null;
            }

            foreach (var item in list)
            {
                if ((long?)item?["id"] == id)
                {
                    return (string)item["thumb"];
                }
            }

            return null;
        }

        /// <summary>取当前投放项的 id；无投放时返回 -1。</summary>
        public static long SelectShowId(JArray show)
        {
            if (show == null || show.Count == 0)
            {
                return -1;
            }

            return (long?)show[0]?["id"] ?? -1;
        }

        /// <summary>取当前投放项的建议展示时长（毫秒）；无投放时返回 0。</summary>
        public static int SelectDurationMs(JArray show)
        {
            if (show == null || show.Count == 0)
            {
                return 0;
            }

            return (int?)show[0]?["duration"] ?? 0;
        }

        /// <summary>
        /// webp 交给 CDN 转 jpeg（UWP 对 webp 的解码支持不保证），只给 .webp 结尾的地址追加后缀。
        /// 判断不区分大小写；空值原样返回。
        /// </summary>
        public static string EnsureJpegUrl(string thumb)
        {
            if (string.IsNullOrEmpty(thumb))
            {
                return thumb;
            }

            return thumb.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
                ? thumb + "@1080w.jpg"
                : thumb;
        }

        /// <summary>
        /// 把展示时长夹到 [minShowMs, maxShowMs]。
        /// 服务端 duration 实测常为 1000ms，直接照搬会一闪而过，故有下限。
        /// maxShowMs 小于 minShowMs 时（配置错误）返回下限，避免出现负值区间。
        /// </summary>
        public static int NormalizeDurationMs(int durationMs, int minShowMs, int maxShowMs)
        {
            if (maxShowMs < minShowMs)
            {
                return minShowMs;
            }

            return Math.Min(Math.Max(durationMs, minShowMs), maxShowMs);
        }
    }
}
