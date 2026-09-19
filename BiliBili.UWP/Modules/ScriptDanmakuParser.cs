using System.Collections.Generic;
using BiliBili.UWP.Models;
using Newtonsoft.Json;

namespace BiliBili.UWP.Modules
{
    /// <summary>
    /// 脚本弹幕条目的纯逻辑校验与规范化。不依赖 UWP 运行时，
    /// 由 <c>ScriptDanmakuService</c>（UWP 侧）与单元测试共同使用。
    /// </summary>
    public static class ScriptDanmakuParser
    {
        /// <summary>脚本未声明 duration 时使用的默认时长（秒）。</summary>
        public const double DefaultDurationSeconds = 3;

        /// <summary>支持的最大时长（秒），避免脚本声明超大值导致长期占位。</summary>
        public const double MaxDurationSeconds = 600;

        /// <summary>脚本语言标识：JavaScript。</summary>
        public const string LangJs = "js";

        /// <summary>脚本语言标识：TypeScript。</summary>
        public const string LangTs = "ts";

        /// <summary>
        /// 解析并规范化脚本弹幕 JSON。JSON 非法时返回空列表；
        /// 单条非法只丢弃该条，不影响其余条目。
        /// </summary>
        public static IReadOnlyList<ScriptDanmakuModel> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<ScriptDanmakuModel>();
            }

            ScriptDanmakuDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<ScriptDanmakuDocument>(json);
            }
            catch (JsonException)
            {
                return new List<ScriptDanmakuModel>();
            }

            return Normalize(document?.items);
        }

        /// <summary>
        /// 过滤并补齐条目字段。可空集合返回空列表，保证调用方无需判空。
        /// </summary>
        public static IReadOnlyList<ScriptDanmakuModel> Normalize(
            IEnumerable<ScriptDanmakuModel> items)
        {
            var result = new List<ScriptDanmakuModel>();
            if (items == null)
            {
                return result;
            }

            var index = 0;
            foreach (var item in items)
            {
                index++;
                if (item == null
                    || string.IsNullOrWhiteSpace(item.code)
                    || double.IsNaN(item.stime)
                    || double.IsInfinity(item.stime)
                    || item.stime < 0)
                {
                    continue;
                }

                var duration = item.duration;
                if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0)
                {
                    duration = DefaultDurationSeconds;
                }
                else if (duration > MaxDurationSeconds)
                {
                    duration = MaxDurationSeconds;
                }

                result.Add(new ScriptDanmakuModel
                {
                    id = string.IsNullOrWhiteSpace(item.id)
                        ? "item-" + index
                        : item.id,
                    stime = item.stime,
                    duration = duration,
                    lang = NormalizeLang(item.lang),
                    code = item.code
                });
            }

            return result;
        }

        /// <summary>
        /// 语言标识归一化：除明确的 ts 外一律按 js 处理。
        /// 未知语言不做静默丢弃——交由宿主在运行时上报，避免文件整体失效。
        /// </summary>
        public static string NormalizeLang(string lang)
        {
            return string.Equals(lang, LangTs, System.StringComparison.OrdinalIgnoreCase)
                ? LangTs
                : LangJs;
        }
    }
}
