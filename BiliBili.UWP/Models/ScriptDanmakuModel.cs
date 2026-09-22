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

        /// <summary>
        /// 持续时长（秒）。缺省或非正数表示不设时间窗：元素寿命由脚本的 lifeTime
        /// 决定，宿主只保留防呆上限（见宿主 MAX_ITEM_WINDOW_MS）。
        /// </summary>
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

    /// <summary>
    /// 推给脚本宿主的弹幕条目（M8 的 CommentData 形状）。
    /// 字段名与 M8 文档一致，宿主按同名同义直接交给脚本，不做重命名——
    /// 脚本读到的 <c>Player.commentList[i].txt</c> 等就是这里的属性。
    /// 用显式模型而不是匿名类型：UWP Release 走 .NET Native AOT，
    /// 反射序列化要避开动态形状（见设计文档 §风险）。
    /// </summary>
    public sealed class ScriptDanmakuComment
    {
        /// <summary>弹幕内容。</summary>
        public string txt { get; set; }

        /// <summary>出现时间（**秒**；M8 的 CommentData.time 是秒，Player.time 才是毫秒）。</summary>
        public double time { get; set; }

        /// <summary>颜色（0xRRGGBB）。</summary>
        public int color { get; set; }

        /// <summary>弹幕池号码。</summary>
        public int pool { get; set; }

        /// <summary>弹幕模式（1/4/5 等）。</summary>
        public int mode { get; set; }

        /// <summary>字体大小。</summary>
        public double fontSize { get; set; }
    }
}
