using System.Linq;
using BiliBili.UWP.Models;
using BiliBili.UWP.Modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace BiliBili.Tests
{
    [TestClass]
    public class ScriptDanmakuParserTests
    {
        private const string ValidScript =
            "ctx.g.fillStyle = '#66ccff'; ctx.g.fillRect(0, 0, 10, 10);";

        [TestMethod]
        public void Parse_FullDocument_ReadsAllFields()
        {
            var json = JsonConvert.SerializeObject(new
            {
                version = 1,
                title = "示例",
                items = new[]
                {
                    new
                    {
                        id = "a",
                        stime = 2.5,
                        duration = 4.5,
                        lang = "ts",
                        code = ValidScript
                    }
                }
            });

            var items = ScriptDanmakuParser.Parse(json);

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("a", items[0].id);
            Assert.AreEqual(2.5, items[0].stime);
            Assert.AreEqual(4.5, items[0].duration);
            Assert.AreEqual(ScriptDanmakuParser.LangTs, items[0].lang);
            Assert.AreEqual(ValidScript, items[0].code);
        }

        [TestMethod]
        public void Parse_EmptyOrBlankInput_ReturnsEmpty()
        {
            Assert.AreEqual(0, ScriptDanmakuParser.Parse(null).Count);
            Assert.AreEqual(0, ScriptDanmakuParser.Parse("").Count);
            Assert.AreEqual(0, ScriptDanmakuParser.Parse("   ").Count);
        }

        [TestMethod]
        public void Parse_MalformedJson_ReturnsEmptyInsteadOfThrowing()
        {
            var items = ScriptDanmakuParser.Parse("{ this is not json");

            Assert.AreEqual(0, items.Count);
        }

        [TestMethod]
        public void Parse_DocumentWithoutItems_ReturnsEmpty()
        {
            Assert.AreEqual(0, ScriptDanmakuParser.Parse("{\"version\":1}").Count);
            Assert.AreEqual(0, ScriptDanmakuParser.Parse("{\"items\":null}").Count);
        }

        [TestMethod]
        public void Parse_OneBadItem_DoesNotDropTheRest()
        {
            var json = JsonConvert.SerializeObject(new
            {
                items = new object[]
                {
                    new { id = "ok-1", stime = 0.0, duration = 1.0, code = ValidScript },
                    new { id = "no-code", stime = 1.0, duration = 1.0, code = "" },
                    new { id = "ok-2", stime = 2.0, duration = 1.0, code = ValidScript }
                }
            });

            var items = ScriptDanmakuParser.Parse(json);

            CollectionAssert.AreEqual(
                new[] { "ok-1", "ok-2" },
                items.Select(item => item.id).ToArray());
        }

        [TestMethod]
        public void Normalize_NegativeStartTime_IsDropped()
        {
            var items = ScriptDanmakuParser.Normalize(new[]
            {
                new ScriptDanmakuModel { id = "neg", stime = -1, code = ValidScript },
                new ScriptDanmakuModel { id = "zero", stime = 0, code = ValidScript }
            });

            CollectionAssert.AreEqual(
                new[] { "zero" },
                items.Select(item => item.id).ToArray());
        }

        [TestMethod]
        public void Normalize_NaNOrInfiniteStartTime_IsDropped()
        {
            var items = ScriptDanmakuParser.Normalize(new[]
            {
                new ScriptDanmakuModel { id = "nan", stime = double.NaN, code = ValidScript },
                new ScriptDanmakuModel { id = "inf", stime = double.PositiveInfinity, code = ValidScript },
                new ScriptDanmakuModel { id = "ok", stime = 1, code = ValidScript }
            });

            CollectionAssert.AreEqual(
                new[] { "ok" },
                items.Select(item => item.id).ToArray());
        }

        [TestMethod]
        public void Normalize_NullOrBlankCode_IsDropped()
        {
            var items = ScriptDanmakuParser.Normalize(new[]
            {
                new ScriptDanmakuModel { id = "null-code", stime = 0, code = null },
                new ScriptDanmakuModel { id = "blank", stime = 0, code = "   " },
                new ScriptDanmakuModel { id = "ok", stime = 0, code = ValidScript }
            });

            CollectionAssert.AreEqual(
                new[] { "ok" },
                items.Select(item => item.id).ToArray());
        }

        [TestMethod]
        public void Normalize_NullEntry_IsDropped()
        {
            var items = ScriptDanmakuParser.Normalize(new[]
            {
                null,
                new ScriptDanmakuModel { id = "ok", stime = 0, code = ValidScript }
            });

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("ok", items[0].id);
        }

        [TestMethod]
        public void Normalize_NullCollection_ReturnsEmpty()
        {
            Assert.AreEqual(0, ScriptDanmakuParser.Normalize(null).Count);
        }

        [TestMethod]
        public void Normalize_ZeroOrMissingDuration_FallsBackToDefault()
        {
            var items = ScriptDanmakuParser.Normalize(new[]
            {
                new ScriptDanmakuModel { id = "zero", stime = 0, duration = 0, code = ValidScript },
                new ScriptDanmakuModel { id = "missing", stime = 0, code = ValidScript },
                new ScriptDanmakuModel { id = "nan", stime = 0, duration = double.NaN, code = ValidScript }
            });

            Assert.AreEqual(3, items.Count);
            foreach (var item in items)
            {
                Assert.AreEqual(
                    ScriptDanmakuParser.DefaultDurationSeconds,
                    item.duration,
                    item.id);
            }
        }

        [TestMethod]
        public void Normalize_OverlongDuration_IsClamped()
        {
            var items = ScriptDanmakuParser.Normalize(new[]
            {
                new ScriptDanmakuModel
                {
                    id = "long",
                    stime = 0,
                    duration = ScriptDanmakuParser.MaxDurationSeconds * 10,
                    code = ValidScript
                }
            });

            Assert.AreEqual(ScriptDanmakuParser.MaxDurationSeconds, items[0].duration);
        }

        [TestMethod]
        public void Normalize_MissingId_GetsSequentialFallback()
        {
            var items = ScriptDanmakuParser.Normalize(new[]
            {
                new ScriptDanmakuModel { stime = 0, code = ValidScript },
                new ScriptDanmakuModel { stime = 1, code = ValidScript }
            });

            CollectionAssert.AreEqual(
                new[] { "item-1", "item-2" },
                items.Select(item => item.id).ToArray());
        }

        [TestMethod]
        public void Normalize_FallbackIdUsesSourcePosition_NotOutputPosition()
        {
            // 第 2 条非法，第 3 条缺 id：补出的 id 应反映它在原始集合中的位置，
            // 否则「第几条」的诊断信息会错位。
            var items = ScriptDanmakuParser.Normalize(new[]
            {
                new ScriptDanmakuModel { id = "keep", stime = 0, code = ValidScript },
                new ScriptDanmakuModel { id = "dropped", stime = -1, code = ValidScript },
                new ScriptDanmakuModel { stime = 2, code = ValidScript }
            });

            CollectionAssert.AreEqual(
                new[] { "keep", "item-3" },
                items.Select(item => item.id).ToArray());
        }

        [TestMethod]
        public void Normalize_NonDefaultDuration_IsPreserved()
        {
            var items = ScriptDanmakuParser.Normalize(new[]
            {
                new ScriptDanmakuModel { id = "a", stime = 0, duration = 0.5, code = ValidScript }
            });

            Assert.AreEqual(0.5, items[0].duration);
        }

        [TestMethod]
        public void NormalizeLang_OnlyExplicitTsIsTypeScript()
        {
            Assert.AreEqual(ScriptDanmakuParser.LangTs, ScriptDanmakuParser.NormalizeLang("ts"));
            Assert.AreEqual(ScriptDanmakuParser.LangTs, ScriptDanmakuParser.NormalizeLang("TS"));
            Assert.AreEqual(ScriptDanmakuParser.LangTs, ScriptDanmakuParser.NormalizeLang("Ts"));

            Assert.AreEqual(ScriptDanmakuParser.LangJs, ScriptDanmakuParser.NormalizeLang("js"));
            Assert.AreEqual(ScriptDanmakuParser.LangJs, ScriptDanmakuParser.NormalizeLang("javascript"));
            Assert.AreEqual(ScriptDanmakuParser.LangJs, ScriptDanmakuParser.NormalizeLang(null));
            Assert.AreEqual(ScriptDanmakuParser.LangJs, ScriptDanmakuParser.NormalizeLang(""));
            Assert.AreEqual(ScriptDanmakuParser.LangJs, ScriptDanmakuParser.NormalizeLang("unknown"));
        }

        [TestMethod]
        public void Normalize_DoesNotMutateInputModels()
        {
            var source = new ScriptDanmakuModel
            {
                id = "",
                stime = 0,
                duration = 0,
                lang = "ts",
                code = ValidScript
            };

            var items = ScriptDanmakuParser.Normalize(new[] { source });

            Assert.AreEqual("", source.id);
            Assert.AreEqual(0, source.duration);
            Assert.AreEqual("ts", source.lang);
            Assert.AreEqual("item-1", items[0].id);
            Assert.AreEqual(ScriptDanmakuParser.DefaultDurationSeconds, items[0].duration);
        }

        [TestMethod]
        public void Normalize_PreservesSourceOrder()
        {
            var items = ScriptDanmakuParser.Normalize(new[]
            {
                new ScriptDanmakuModel { id = "late", stime = 90, code = ValidScript },
                new ScriptDanmakuModel { id = "early", stime = 1, code = ValidScript }
            });

            // 排序由调用方（PlayerPage）负责，解析层只保序。
            CollectionAssert.AreEqual(
                new[] { "late", "early" },
                items.Select(item => item.id).ToArray());
        }
    }
}
