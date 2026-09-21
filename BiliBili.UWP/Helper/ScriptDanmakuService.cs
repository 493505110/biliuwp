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
        /// 示例按保留模式编写：脚本只执行一次，期间创建保留元素并声明 tween，
        /// 之后逐帧由宿主插值（不要写成每帧重算坐标）。
        /// tween 的 lifeTime 声明的是补间时长；元素寿命取「声明值」与
        /// 「条目窗口剩余时间」的较小值，未声明 lifeTime 时等于窗口剩余时间。
        /// demo-scroll-text 复刻原版 M8 示例的观感：文字从右侧屏幕外滑入、
        /// 向左移出；demo-particles 把 24 个点画进同一个 shape 的本地坐标，
        /// 用 rotation + scale + alpha 三条补间同时做出旋转、扩散与淡出。
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
                    code = "var label = ctx.createText('脚本弹幕已生效', {"
                        + " font: 'sans-serif', fontsize: 32, color: 0x66CCFF });"
                        + "label.y = Math.round(ctx.height / 2 - 19);"
                        + "ctx.tween(label, {"
                        + "  x: { fromValue: ctx.width + 120, toValue: -120,"
                        + "       easing: 'Linear' }"
                        + "}, { lifeTime: 4 });"
                },
                new ScriptDanmakuModel
                {
                    id = "demo-particles",
                    stime = 3,
                    duration = 5,
                    lang = LangJs,
                    code = "var ring = ctx.createShape();"
                        + "for (var i = 0; i < 24; i++) {"
                        + "  var angle = i / 24 * Math.PI * 2;"
                        + "  ring.graphics.beginFill(0xFF66CC, 1);"
                        + "  ring.graphics.drawCircle(Math.cos(angle) * 40,"
                        + "      Math.sin(angle) * 40, 6);"
                        + "  ring.graphics.endFill();"
                        + "}"
                        + "ring.x = ctx.width / 2;"
                        + "ring.y = ctx.height / 2;"
                        + "ctx.tween(ring, {"
                        + "  rotation: { fromValue: 0, toValue: 360,"
                        + "      easing: 'Linear' },"
                        + "  scaleX: { fromValue: 1, toValue: 5, easing: 'Linear' },"
                        + "  scaleY: { fromValue: 1, toValue: 5, easing: 'Linear' },"
                        + "  alpha: { fromValue: 1, toValue: 0, easing: 'Linear' }"
                        + "}, { lifeTime: 3 });"
                }
            });
        }
    }
}
