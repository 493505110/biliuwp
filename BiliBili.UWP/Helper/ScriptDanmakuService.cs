using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BiliBili.UWP.Modules;
using BiliBili.UWP.Models;
using Windows.Storage;

namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// 脚本弹幕的数据层：负责从本地文件读取脚本弹幕集合。
    /// 校验与规范化逻辑在 <see cref="ScriptDanmakuParser"/>（纯逻辑，可单测）。
    /// 只做数据准备，不参与渲染（渲染由 <see cref="Controls.ScriptDanmakuControl"/> 承载）。
    /// </summary>
    public static class ScriptDanmakuService
    {
        /// <summary>脚本未声明 duration 时使用的默认时长（秒）。</summary>
        public const double DefaultDurationSeconds = ScriptDanmakuParser.DefaultDurationSeconds;

        /// <summary>支持的最大时长（秒）。</summary>
        public const double MaxDurationSeconds = ScriptDanmakuParser.MaxDurationSeconds;

        /// <summary>脚本语言标识：JavaScript。</summary>
        public const string LangJs = ScriptDanmakuParser.LangJs;

        /// <summary>脚本语言标识：TypeScript。</summary>
        public const string LangTs = ScriptDanmakuParser.LangTs;

        public static async Task<IReadOnlyList<ScriptDanmakuModel>> LoadFromFileAsync(
            StorageFile file)
        {
            var json = await FileIO.ReadTextAsync(file);
            var items = ScriptDanmakuParser.Parse(json);
            if (items.Count == 0 && !string.IsNullOrWhiteSpace(json))
            {
                LogHelper.WriteLog("脚本弹幕文件没有可用条目：" + file.Name, LogType.INFO);
            }

            return items;
        }

        public static IReadOnlyList<ScriptDanmakuModel> Parse(string json)
        {
            return ScriptDanmakuParser.Parse(json);
        }

        public static IReadOnlyList<ScriptDanmakuModel> Normalize(
            IEnumerable<ScriptDanmakuModel> items)
        {
            return ScriptDanmakuParser.Normalize(items);
        }

        /// <summary>
        /// 内置示例。用于首次验证渲染链路，不依赖任何外部文件。
        /// </summary>
        public static IReadOnlyList<ScriptDanmakuModel> GetBuiltInDemo()
        {
            return ScriptDanmakuParser.Normalize(new[]
            {
                new ScriptDanmakuModel
                {
                    id = "demo-scroll-text",
                    stime = 1,
                    duration = 4,
                    lang = LangJs,
                    code = "var x = (1 - ctx.progress) * (ctx.width + 240) - 120;"
                        + "ctx.g.font = '32px sans-serif';"
                        + "ctx.g.fillStyle = '#66ccff';"
                        + "ctx.g.textBaseline = 'middle';"
                        + "ctx.g.fillText('脚本弹幕已生效', x, ctx.height / 2);"
                },
                new ScriptDanmakuModel
                {
                    id = "demo-particles",
                    stime = 3,
                    duration = 5,
                    lang = LangJs,
                    code = "var n = 24;"
                        + "for (var i = 0; i < n; i++) {"
                        + "  var a = i / n * Math.PI * 2 + ctx.progress * Math.PI * 2;"
                        + "  var r = 40 + ctx.progress * 160;"
                        + "  var cx = ctx.width / 2 + Math.cos(a) * r;"
                        + "  var cy = ctx.height / 2 + Math.sin(a) * r;"
                        + "  ctx.g.beginPath();"
                        + "  ctx.g.arc(cx, cy, 6, 0, Math.PI * 2);"
                        + "  ctx.g.fillStyle = 'rgba(255,102,204,' + (1 - ctx.progress).toFixed(2) + ')';"
                        + "  ctx.g.fill();"
                        + "}"
                }
            });
        }
    }
}
