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
        /// <summary>duration 缺省或非正数时的取值：不设时间窗（秒）。</summary>
        public const double UnboundedDurationSeconds = ScriptDanmakuParser.UnboundedDurationSeconds;

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
        /// 示例按**原版 M8 的 API 面**编写（不是自研的 ctx）：脚本只执行一次，
        /// 期间用 $.createComment / $.createShape 建出保留元件，用声明式 tween
        /// 或 interval 声明动画，之后逐帧由宿主插值（不要写成每帧重算坐标）。
        /// lifeTime 声明的是补间时长，同时也是元素的寿命；元素寿命取
        /// 「声明值」与「条目窗口剩余时间」的较小值，未声明时等于窗口剩余时间。
        /// 单条脚本同时演示两种写法：文字走声明式 tween（复刻原版 M8 示例的
        /// 观感，从右侧屏幕外滑入、向左移出）；粒子环复刻原版的 24 点旋转扩散
        /// ——点的半径要恒定 6px，用 scale 会把点一起放大，所以走 M8 惯用的
        /// interval(fn, 16, 0) 逐帧只改位置（脚本仍只执行一次，元素树与缓存不重建）。
        /// 粒子比文字晚 2 秒出现：interval 的 delay 是「首次触发的间隔」，
        /// 用 Player.time 与条目起始时刻比对（M8 脚本就是这么判时间的），
        /// 到点前直接返回。定时器登记在条目上，条目回收时由宿主统一清掉，
        /// 不需要脚本自己 clearTimer。
        /// </summary>
        public static IReadOnlyList<ScriptDanmakuModel> GetBuiltInDemo()
        {
            return ScriptDanmakuParser.Normalize(new[]
            {
                new ScriptDanmakuModel
                {
                    // 单条脚本：文字（tween）+ 粒子环（interval 逐帧）写在一条里，
                    // 与原版 M8 示例「一条脚本画完整个效果」的形态一致。
                    id = "demo-m8-sample",
                    stime = 1,
                    duration = 7,
                    lang = LangJs,
                    code = "var label = $.createComment('脚本弹幕已生效', {"
                        + " font: 'sans-serif', fontsize: 32, color: 0x66CCFF,"
                        + " x: Player.width + 120, y: Math.round(Player.height / 2 - 19),"
                        + " lifeTime: 4,"
                        + " motion: { x: { fromValue: Player.width + 120,"
                        + "                 toValue: -120, easing: 'Linear', lifeTime: 4 } } });"
                        + "var count = 24;"
                        + "var cx = Player.width / 2;"
                        + "var cy = Player.height / 2;"
                        + "var dots = [];"
                        + "for (var i = 0; i < count; i++) {"
                        + "  var dot = $.createShape({ visible: false });"
                        + "  dot.graphics.beginFill(0xFF66CC, 1);"
                        + "  dot.graphics.drawCircle(0, 0, 6);"
                        + "  dot.graphics.endFill();"
                        + "  dots.push(dot);"
                        + "}"
                        + "var startAt = Player.time;"
                        + "interval(function () {"
                        + "  var local = Player.time - startAt - 2000;"
                        + "  if (local < 0) { return; }"
                        + "  var p = Math.min(1, local / 3000);"
                        + "  var radius = 40 + p * 160;"
                        + "  for (var i = 0; i < count; i++) {"
                        + "    var angle = i / count * Math.PI * 2 + p * Math.PI * 2;"
                        + "    dots[i].x = cx + Math.cos(angle) * radius;"
                        + "    dots[i].y = cy + Math.sin(angle) * radius;"
                        + "    dots[i].alpha = 1 - p;"
                        + "    dots[i].visible = p < 1;"
                        + "  }"
                        + "}, 16, 0);"
                }
            });
        }
    }
}
