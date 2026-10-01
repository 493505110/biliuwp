using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace BiliBili.Tests
{
    [TestClass]
    public class ScriptDanmakuCommentBatchTests
    {
        private const string Prefix = "window.scriptDanmakuHost.appendComments(";

        private static IEnumerable<ScriptDanmakuComment> Decode(IEnumerable<string> commands)
        {
            foreach (var command in commands)
            {
                Assert.IsTrue(command.StartsWith(Prefix, StringComparison.Ordinal));
                Assert.IsTrue(command.EndsWith("); ", StringComparison.Ordinal));
                var json = command.Substring(Prefix.Length, command.Length - Prefix.Length - 3);
                foreach (var item in JArray.Parse(json).ToObject<ScriptDanmakuComment[]>()) yield return item;
            }
        }

        [TestMethod]
        public void LargePoolRoundTripsEveryCommentInOrderWithinPayloadBudget()
        {
            var comments = Enumerable.Range(0, 5000).Select(index => new ScriptDanmakuComment
            {
                txt = "弹幕\"\\\n😀" + index,
                time = index / 10d,
                color = index,
                pool = index % 2,
                mode = index % 7 + 1,
                fontSize = 25
            }).ToArray();
            var commands = ScriptDanmakuCommentBatch.Build(comments);
            Assert.IsTrue(commands.Count > 1);
            Assert.IsTrue(commands.All(command => command.Length <= ScriptDanmakuCommentBatch.MaxPayloadLength));
            var decoded = Decode(commands).ToArray();
            Assert.AreEqual(comments.Length, decoded.Length);
            for (var index = 0; index < comments.Length; index++)
            {
                Assert.AreEqual(comments[index].txt, decoded[index].txt);
                Assert.AreEqual(comments[index].time, decoded[index].time);
                Assert.AreEqual(comments[index].color, decoded[index].color);
                Assert.AreEqual(comments[index].pool, decoded[index].pool);
                Assert.AreEqual(comments[index].mode, decoded[index].mode);
                Assert.AreEqual(comments[index].fontSize, decoded[index].fontSize);
            }
        }

        [TestMethod]
        public void OversizedCommentIsKeptWholeInItsOwnBatch()
        {
            var texts = new[] { "before", new string('长', ScriptDanmakuCommentBatch.MaxPayloadLength), "after" };
            var commands = ScriptDanmakuCommentBatch.Build(texts.Select(txt => new ScriptDanmakuComment { txt = txt }));
            Assert.AreEqual(3, commands.Count);
            CollectionAssert.AreEqual(texts, Decode(commands).Select(item => item.txt).ToArray());
        }

        [TestMethod]
        public void NullCommentsAndEmptyPoolsProduceNoEntries()
        {
            Assert.AreEqual(0, ScriptDanmakuCommentBatch.Build(null).Count);
            Assert.AreEqual(0, ScriptDanmakuCommentBatch.Build(new ScriptDanmakuComment[] { null }).Count);
            var commands = ScriptDanmakuCommentBatch.Build(new[] { null, new ScriptDanmakuComment { txt = "only" }, null });
            CollectionAssert.AreEqual(new[] { "only" }, Decode(commands).Select(item => item.txt).ToArray());
        }

        [TestMethod]
        public void SupersededPreparationStopsBeforeEnumeratingTheRemainingPool()
        {
            using (var source = new CancellationTokenSource())
            {
                var enumerated = 0;
                IEnumerable<ScriptDanmakuComment> Comments()
                {
                    for (var index = 0; index < 5000; index++)
                    {
                        enumerated++;
                        if (index == 10) source.Cancel();
                        yield return new ScriptDanmakuComment { txt = index.ToString() };
                    }
                }
                Assert.ThrowsException<OperationCanceledException>(() => ScriptDanmakuCommentBatch.Build(Comments(), source.Token));
                Assert.AreEqual(11, enumerated);
            }
        }
    }
}
