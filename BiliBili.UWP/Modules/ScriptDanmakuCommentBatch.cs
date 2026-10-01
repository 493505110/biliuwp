using System.Collections.Generic;
using System.Text;
using System.Threading;
using BiliBili.UWP.Models;
using Newtonsoft.Json;

namespace BiliBili.UWP.Modules
{
    /// <summary>纯数据处理：在后台把弹幕快照转换成有大小上限的宿主命令。</summary>
    public static class ScriptDanmakuCommentBatch
    {
        public const int MaxPayloadLength = 24 * 1024;
        private const string Prefix = "window.scriptDanmakuHost.appendComments([";
        private const string Suffix = "]); ";

        public static IReadOnlyList<string> Build(
            IEnumerable<ScriptDanmakuComment> comments,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var commands = new List<string>();
            var builder = new StringBuilder(MaxPayloadLength + 64);
            builder.Append(Prefix);
            var count = 0;
            foreach (var comment in comments ?? new ScriptDanmakuComment[0])
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (comment == null)
                {
                    continue;
                }

                var json = JsonConvert.SerializeObject(comment);
                var separatorLength = count == 0 ? 0 : 1;
                // 单条超长弹幕单独成包，保留完整内容；通常条目连同命令外壳受预算约束。
                if (count > 0 && builder.Length + separatorLength + json.Length + Suffix.Length > MaxPayloadLength)
                {
                    builder.Append(Suffix);
                    commands.Add(builder.ToString());
                    builder.Clear();
                    builder.Append(Prefix);
                    count = 0;
                }

                if (count > 0) builder.Append(',');
                builder.Append(json);
                count++;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (count > 0)
            {
                builder.Append(Suffix);
                commands.Add(builder.ToString());
            }
            return commands;
        }
    }
}
