using System.Collections.Generic;

namespace BiliBili.UWP.Models
{
    /// <summary>
    /// 单条脚本弹幕。<see cref="code"/> 是可执行脚本源码，由 WebView2 宿主运行时编译执行。
    /// </summary>
    public sealed class ScriptDanmakuModel
    {
        public string id { get; set; }

        /// <summary>出现时间（秒）。</summary>
        public double stime { get; set; }

        /// <summary>持续时长（秒）。</summary>
        public double duration { get; set; }

        /// <summary>脚本语言："js" 或 "ts"。缺省按 js 处理。</summary>
        public string lang { get; set; }

        public string code { get; set; }
    }

    /// <summary>
    /// 脚本弹幕文件（.json）的顶层结构。
    /// </summary>
    public sealed class ScriptDanmakuDocument
    {
        public int version { get; set; }
        public string title { get; set; }
        public List<ScriptDanmakuModel> items { get; set; }
    }
}
