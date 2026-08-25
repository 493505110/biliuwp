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

        private static ScriptResult ParseScript(string script)
        {
            var vm = new VirtualMachine();
            var global = (Dictionary<string, object>)vm.getGlobalObject();
            var store = M8Sandbox.Install(vm, global, new M8Host { EnableTimers = false });
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
