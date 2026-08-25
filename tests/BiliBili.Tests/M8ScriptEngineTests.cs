using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using scripting;

namespace BiliBili.Tests
{
    [TestClass]
    public class M8ScriptEngineTests
    {
        [TestMethod]
        public void Scanner_ParsesLanguageFeaturesAndGeneratesByteCode()
        {
            const string script = @"
var total = 0;
function increment(value) {
    return value + 1;
}
var makeAdder = function (seed) {
    return function (value) {
        return seed + value;
    };
};
var record = { value: 2 };
for (var index = 0; index < 2; index++) {
    total += increment(record.value);
}
switch (total) {
    case 6:
        total = 7;
        break;
    default:
        total = 0;
}
";

            var parsed = ParseScript(script);

            Assert.IsNotNull(parsed.ByteCode);
            Assert.IsTrue(parsed.ByteCode.Count > 1);
        }

        [TestMethod]
        public void Execute_StoresArithmeticResultInGlobalStore()
        {
            var result = ExecuteScript(@"
Global._set(""arithmetic"", 1 + 2 * 3);
");

            Assert.AreEqual(7d, (double)result.Store.Get("arithmetic"));
        }

        [TestMethod]
        public void FunctionExpression_CapturesClosureAndReturnsValue()
        {
            var result = ExecuteScript(@"
var makeAdder = function (seed) {
    return function (value) {
        return seed + value;
    };
};
var addFive = makeAdder(5);
Global._set(""closureResult"", addFive(7));
");

            Assert.AreEqual(12d, (double)result.Store.Get("closureResult"));
        }

        [TestMethod]
        public void ArraysObjectsAndUtilsForeach_WorkTogether()
        {
            var result = ExecuteScript(@"
var values = [1, 2, 3];
values[1] = 4;
var record = { selected: 0, total: 0 };
record.selected = values[1];
Utils.foreach(values, function (index, value) {
    record.total += value;
});
Global._set(""arrayValue"", values[1]);
Global._set(""objectValue"", record.selected);
Global._set(""foreachTotal"", record.total);
");

            Assert.AreEqual(4d, (double)result.Store.Get("arrayValue"));
            Assert.AreEqual(4d, (double)result.Store.Get("objectValue"));
            Assert.AreEqual(8d, (double)result.Store.Get("foreachTotal"));
        }

        [TestMethod]
        public void SandboxApis_ReturnExpectedValues()
        {
            var result = ExecuteScript(@"
Global._set(""source"", 42);
Global._set(""roundtrip"", Global._get(""source""));
Global._set(""rgb"", Utils.rgb(255, 0, 0));
Global._set(""formatTimes"", Utils.formatTimes(65));
Global._set(""distance"", Utils.distance(0, 0, 3, 4));
Global._set(""rand"", Utils.rand(5, 5));
Global._set(""floor"", Math.floor(3.7));
Global._set(""max"", Math.max(3, 9));
Global._set(""pow"", Math.pow(2, 10));
Global._set(""parseInt"", parseInt(""42""));
Global._set(""parseHex"", parseInt(""ff"", 16));
Global._set(""parseFloat"", parseFloat(""3.5""));
");

            Assert.AreEqual(42d, (double)result.Store.Get("roundtrip"));
            Assert.AreEqual(16711680d, (double)result.Store.Get("rgb"));
            Assert.AreEqual("01:05", result.Store.Get("formatTimes"));
            Assert.AreEqual(5d, (double)result.Store.Get("distance"));
            Assert.AreEqual(5d, (double)result.Store.Get("rand"));
            Assert.AreEqual(3d, (double)result.Store.Get("floor"));
            Assert.AreEqual(9d, (double)result.Store.Get("max"));
            Assert.AreEqual(1024d, (double)result.Store.Get("pow"));
            Assert.AreEqual(42d, (double)result.Store.Get("parseInt"));
            Assert.AreEqual(255d, (double)result.Store.Get("parseHex"));
            Assert.AreEqual(3.5d, (double)result.Store.Get("parseFloat"));
        }

        [TestMethod]
        public void StopExecution_ThrowsM8StopException()
        {
            var parsed = ParseScript("stopExecution();");

            Assert.ThrowsException<M8StopException>(() => parsed.Vm.execute());
        }

        [TestMethod]
        public void CoroutineSyntax_ParsesYieldSuspendAndLoop()
        {
            const string script = @"
coroutine ticker(value) {
    yield;
    suspend;
    loop {
        break;
    }
}
";

            var parsed = ParseScript(script);

            Assert.IsNotNull(parsed.ByteCode);
            Assert.IsTrue(parsed.ByteCode.Count > 1);
        }

        [TestMethod]
        public void Display_CreateShape_ExposesMutableStyleAndDefaults()
        {
            var host = new FakeRenderHost { StageWidth = 640d, StageHeight = 360d };
            var manager = new M8ScriptManager(host);
            var player = new M8PlayerApi(host, manager);
            var display = new M8DisplayApi(host, player, manager);
            var shape = display.createShape(new Dictionary<string, object>
            {
                ["x"] = 12d,
                ["alpha"] = 0.5d
            });

            Assert.IsNotNull(shape);
            Assert.AreEqual(12d, (double)shape.Get("x"));
            Assert.AreEqual(0.5d, (double)shape.Get("alpha"));
            Assert.AreEqual(0d, (double)shape.Get("y"));
            Assert.AreEqual(1d, (double)shape.Get("scale"));
            shape.Set("x", 42d);
            shape.Set("alpha", 0.25d);
            Assert.AreEqual(42d, (double)shape.Get("x"));
            Assert.AreEqual(0.25d, (double)shape.Get("alpha"));
        }

        [TestMethod]
        public void Display_MotionCompletion_RemovesElementFromHostAndManager()
        {
            var host = new FakeRenderHost();
            var manager = new M8ScriptManager(host);
            var player = new M8PlayerApi(host, manager);
            var display = new M8DisplayApi(host, player, manager);
            var shape = display.createShape(new Dictionary<string, object>
            {
                ["lifeTime"] = 0.1d,
                ["motion"] = new Dictionary<string, object>
                {
                    ["x"] = new Dictionary<string, object>
                    {
                        ["fromValue"] = 0d,
                        ["toValue"] = 100d
                    }
                }
            });

            Assert.AreEqual(1, manager.Elements.Count);
            player.play();
            manager.Step(100d);

            Assert.AreEqual(100d, (double)shape.Get("x"));
            Assert.AreEqual(0, manager.Elements.Count);
            Assert.AreEqual(1, host.Removed.Count);
        }

        [TestMethod]
        public void Display_ButtonClick_InvokesOnclickCallback()
        {
            var host = new FakeRenderHost();
            var manager = new M8ScriptManager(host);
            var player = new M8PlayerApi(host, manager);
            var display = new M8DisplayApi(host, player, manager);
            var invoked = 0;
            var button = display.createButton(new Dictionary<string, object>
            {
                ["onclick"] = (Action)(() => invoked++)
            });

            display.InvokeButtonClick(button);

            Assert.AreEqual(1, invoked);
            Assert.AreEqual("Button", button.Get("text"));
            Assert.AreEqual(60d, (double)button.Get("width"));
        }

        [TestMethod]
        public void Player_PlayPauseAndStime_UseHostState()
        {
            var host = new FakeRenderHost();
            var player = new M8PlayerApi(host);

            player.play();
            Assert.AreEqual(PlayerState.PLAYING, player.state);
            Assert.AreEqual(1, host.PlayCalls);
            player.pause();
            Assert.AreEqual(PlayerState.PAUSED, player.state);
            Assert.AreEqual(1, host.PauseCalls);
            player.stime = 12.5d;
            Assert.AreEqual(12.5d, player.stime);
            Assert.AreEqual(12.5d, host.Stime);
            Assert.AreEqual(12500d, player.time);
        }

        [TestMethod]
        public void Display_VectorConversions_ReturnScriptReadableNumbers()
        {
            var display = new M8DisplayApi(new FakeRenderHost());
            var source = new List<object> { 1d, 2.9d, -3d };

            CollectionAssert.AreEqual(
                new List<object> { 1d, 2d, -3d },
                display.toIntVector(source));
            CollectionAssert.AreEqual(
                new List<object> { 1d, 2.9d, -3d },
                display.toNumberVector(source));
        }

        [TestMethod]
        public void Sandbox_RenderApis_ExecuteShapeMotionAndCommentTrigger()
        {
            var host = new FakeRenderHost();
            var result = ExecuteScript(@"
var shape = Display.createShape({
    x: 1,
    lifeTime: 0.01,
    motion: { x: { fromValue: 0, toValue: 10 } }
});
Global._set(""shape"", shape);
Player.commentTrigger(function (item) {
    Global._set(""triggered"", item);
}, 10000);
", host);
            var global = (Dictionary<string, object>)result.Vm.getGlobalObject();
            var player = (M8PlayerApi)global["Player"];
            var manager = (M8ScriptManager)global["ScriptManager"];

            Assert.IsInstanceOfType(result.Store.Get("shape"), typeof(M8Element));
            Assert.AreEqual(1, manager.Elements.Count);
            Assert.IsTrue(player.InvokeCommentTrigger("comment-data"));
            Assert.AreEqual("comment-data", result.Store.Get("triggered"));
            player.play();
            manager.Step(10d);
            Assert.AreEqual(0, manager.Elements.Count);
            manager.ClearTrigger();
        }

        private static ScriptResult ParseScript(string script)
        {
            var vm = new VirtualMachine();
            var global = (Dictionary<string, object>)vm.getGlobalObject();
            var store = M8Sandbox.Install(vm, global, new M8Host { EnableTimers = false });
            var byteCode = new Parser(new Scanner(script)).parse(vm);
            vm.setByteCode(byteCode);
            return new ScriptResult(vm, store, byteCode);
        }

        private static ScriptResult ParseScript(string script, IM8RenderHost renderHost)
        {
            var vm = new VirtualMachine();
            var global = (Dictionary<string, object>)vm.getGlobalObject();
            var store = M8Sandbox.Install(vm, global, new M8Host { EnableTimers = false }, renderHost);
            var byteCode = new Parser(new Scanner(script)).parse(vm);
            vm.setByteCode(byteCode);
            return new ScriptResult(vm, store, byteCode);
        }

        private static ScriptResult ExecuteScript(string script)
        {
            var result = ParseScript(script);
            result.Vm.execute();
            return result;
        }

        private static ScriptResult ExecuteScript(string script, IM8RenderHost renderHost)
        {
            var result = ParseScript(script, renderHost);
            result.Vm.execute();
            return result;
        }

        private sealed class FakeRenderHost : IM8RenderHost
        {
            public FakeRenderHost()
            {
                this.Root = new M8Element();
                this.State = PlayerState.PAUSED;
                this.Volume = 100d;
                this.Added = new List<M8Element>();
                this.Removed = new List<M8Element>();
            }

            public string State { get; set; }
            public double Stime { get; set; }
            public double Volume { get; set; }
            public double StageWidth { get; set; }
            public double StageHeight { get; set; }
            public object Root { get; private set; }
            public int PlayCalls { get; private set; }
            public int PauseCalls { get; private set; }
            public List<M8Element> Added { get; private set; }
            public List<M8Element> Removed { get; private set; }

            public void Play()
            {
                this.PlayCalls++;
                this.State = PlayerState.PLAYING;
            }

            public void Pause()
            {
                this.PauseCalls++;
                this.State = PlayerState.PAUSED;
            }

            public void Seek(double seconds)
            {
                this.Stime = seconds;
            }

            public void Jump(string av, int page, bool newWindow)
            {
            }

            public Dictionary<string, object> CreateSound(string name, Dictionary<string, object> callbacks)
            {
                return callbacks;
            }

            public void AddElement(M8Element element, object parent)
            {
                this.Added.Add(element);
                var container = parent as M8Element;
                if (container != null) container.AddChild(element);
                else element.Set("parent", parent);
            }

            public void RemoveElement(M8Element element)
            {
                this.Removed.Add(element);
                var container = element.Get("parent") as M8Element;
                if (container != null) container.RemoveChild(element);
                else element.Set("parent", null);
            }

            public void InvokeCommentTrigger(object comment)
            {
            }

            public void InvokeKeyTrigger(int keyCode, bool isUp)
            {
            }
        }

        private sealed class ScriptResult
        {
            public ScriptResult(VirtualMachine vm, M8Sandbox.GlobalStore store, List<object> byteCode)
            {
                Vm = vm;
                Store = store;
                ByteCode = byteCode;
            }

            public VirtualMachine Vm { get; }
            public M8Sandbox.GlobalStore Store { get; }
            public List<object> ByteCode { get; }
        }
    }
}
