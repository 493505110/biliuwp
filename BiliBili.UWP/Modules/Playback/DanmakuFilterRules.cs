using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace BiliBili.UWP.Modules.Playback
{
    /// <summary>不可变的屏蔽规则快照，供原生显示和 M8 列表共同使用。</summary>
    public sealed class DanmakuFilterRules
    {
        // play_20181010: org/lala/filter/BlockType.as。
        public const int UserBlock = 1;
        public const int KeywordBlock = 2;
        public const int ModePoolBlock = 4;

        private readonly string[] keywords;
        private readonly string[] users;
        private readonly Regex sourceRegex;

        public DanmakuFilterRules(string keywords, string users, string expression)
        {
            this.keywords = Split(keywords);
            this.users = Split(users);
            if (!string.IsNullOrEmpty(expression))
            {
                try { sourceRegex = new Regex(expression, RegexOptions.None, TimeSpan.FromMilliseconds(100)); }
                catch (ArgumentException) { /* 无效规则不应阻断弹幕。 */ }
            }
        }

        private static string[] Split(string value)
        {
            return (value ?? string.Empty).Split('|').Where(item => item.Length != 0).ToArray();
        }

        /// <returns>null 表示通过；其余值对应原版的 blockType。</returns>
        public int? GetBlockType(string text, string userId, string source, int pool, bool modeEnabled)
        {
            if (!modeEnabled) return ModePoolBlock;
            // 原版字幕池、特殊池在位置类型检查之后跳过普通屏蔽规则。
            if (pool == 1 || pool == 2) return null;
            if (users.Contains(userId ?? string.Empty)) return UserBlock;
            text = text ?? string.Empty;
            if (keywords.Any(word => text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
                return KeywordBlock;
            try
            {
                // 保留 UWP 的正则规则语义：匹配原始弹幕数据。
                if (sourceRegex != null && sourceRegex.IsMatch(source ?? string.Empty)) return KeywordBlock;
            }
            catch (RegexMatchTimeoutException) { }
            return null;
        }
    }
}
