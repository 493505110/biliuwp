using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace scripting
{
    /// <summary>
    /// Exception thrown by the sandbox's stopExecution() to abort the currently
    /// running script. The host is expected to catch and discard it.
    /// </summary>
    public sealed class M8StopException : Exception
    {
        public M8StopException()
            : base("stopExecution") { }
    }

    /// <summary>
    /// Sandbox host callbacks. The UWP player supplies real implementations
    /// (logging sink, UI dispatcher); tests supply simple ones.
    /// </summary>
    public sealed class M8Host
    {
        public Action<string> Log = delegate { };
        public Action Clear = delegate { };
        public Func<long> NowMs = () => Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;
        /// <summary>True when delay()/interval() may schedule real timers; false = run once synchronously.</summary>
        public bool EnableTimers = true;
    }

    /// <summary>
    /// Builds and injects the pure-logic sandbox API layer of the M8 code-danmaku
    /// engine (mirrors the original Flash player's CommentScriptFactory globals):
    /// Math / String / parseInt / parseFloat / trace / clear / getTimer /
    /// Utils (clone, foreach, rgb, rand, distance, hue, formatTimes, delay, interval) /
    /// Global ($G) key-value store / stopExecution.
    /// Rendering & player APIs (Display/$, Player, Bitmap, Tween, ScriptManager) are
    /// layered on top of this by the host in a later phase.
    /// </summary>
    public static class M8Sandbox
    {
        /// <summary>Key-value store backing Global._get/_set.</summary>
        public sealed class GlobalStore
        {
            private readonly Dictionary<string, object> _map = new Dictionary<string, object>();

            public object Get(string key) => this._map.TryGetValue(key, out var v) ? v : null;
            public object Set(string key, object value) { this._map[key] = value; return value; }
            public bool Has(string key) => this._map.ContainsKey(key);
            public Dictionary<string, object> Raw => this._map;

            // Global._[name] direct dictionary access
            public object this[string key] { get => this.Get(key); set => this.Set(key, value); }
        }

        private const double NaN = double.NaN;

        private static double Number(object v) => VirtualMachine.ToNumber(v);
        private static string Str(object v) => VirtualMachine.As3String(v);
        private static bool Truthy(object v) => VirtualMachine.Truthy(v);

        /// <summary>Deep-clones a script value (Dictionary/List/atomic). Mirrors AS3 serialize-copy.</summary>
        public static object Clone(object v)
        {
            if (v is Dictionary<string, object> d)
            {
                var copy = new Dictionary<string, object>();
                foreach (var kv in d) copy[kv.Key] = Clone(kv.Value);
                return copy;
            }
            if (v is List<object> l)
            {
                var copy = new List<object>(l.Count);
                for (int i = 0; i < l.Count; i++) copy.Add(Clone(l[i]));
                return copy;
            }
            if (v is VirtualMachine.FuncObject f)
            {
                // Functions are shared by reference (AS3 serialization keeps class identity).
                return f;
            }
            return v;
        }

        /// <summary>Injects all pure-logic globals into the VM's global object.</summary>
        public static GlobalStore Install(VirtualMachine vm, Dictionary<string, object> global, M8Host host)
        {
            var store = new GlobalStore();

            Func<object, object> trace = s => { host.Log(Str(s)); return null; };
            Func<object> clear = () => { host.Clear(); return null; };
            Func<object> getTimer = () => (double)host.NowMs();
            Func<object[], object> parseInt = args =>
            {
                if (args.Length < 1) return NaN;
                string t = Str(args[0]).Trim();
                int r = args.Length > 1 && args[1] != null ? (int)Number(args[1]) : 10;
                try { return (double)Convert.ToInt32(t, r <= 0 ? 10 : r); }
                catch { return NaN; }
            };
            Func<object, object> parseFloat = s =>
            {
                string t = Str(s).Trim();
                return double.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : NaN;
            };
            Func<object> stopExec = () => throw new M8StopException();

            // ---- Math object (JS Math) ----
            var math = new Dictionary<string, object>
            {
                ["PI"] = Math.PI, ["E"] = Math.E, ["LN2"] = Math.Log(2), ["LN10"] = Math.Log(10),
                ["SQRT2"] = Math.Sqrt(2), ["SQRT1_2"] = Math.Sqrt(0.5),
                ["abs"] = (Func<object, object>)(x => (double)Math.Abs(Number(x))),
                ["ceil"] = (Func<object, object>)(x => (double)Math.Ceiling(Number(x))),
                ["floor"] = (Func<object, object>)(x => (double)Math.Floor(Number(x))),
                ["round"] = (Func<object, object>)(x => (double)Math.Round(Number(x), MidpointRounding.AwayFromZero)),
                ["sqrt"] = (Func<object, object>)(x => (double)Math.Sqrt(Number(x))),
                ["pow"] = (Func<object, object, object>)((x, y) => (double)Math.Pow(Number(x), Number(y))),
                ["random"] = (Func<object>)(() => (double)new Random().NextDouble()),
                ["sin"] = (Func<object, object>)(x => (double)Math.Sin(Number(x))),
                ["cos"] = (Func<object, object>)(x => (double)Math.Cos(Number(x))),
                ["tan"] = (Func<object, object>)(x => (double)Math.Tan(Number(x))),
                ["asin"] = (Func<object, object>)(x => (double)Math.Asin(Number(x))),
                ["acos"] = (Func<object, object>)(x => (double)Math.Acos(Number(x))),
                ["atan"] = (Func<object, object>)(x => (double)Math.Atan(Number(x))),
                ["atan2"] = (Func<object, object, object>)((y, x) => (double)Math.Atan2(Number(y), Number(x))),
                ["log"] = (Func<object, object>)(x => (double)Math.Log(Number(x))),
                ["exp"] = (Func<object, object>)(x => (double)Math.Exp(Number(x))),
                ["max"] = (Func<object, object, object>)((a, b) => (double)Math.Max(Number(a), Number(b))),
                ["min"] = (Func<object, object, object>)((a, b) => (double)Math.Min(Number(a), Number(b))),
                ["absValue"] = (Func<object, object>)(x => (double)Math.Abs(Number(x))),
            };

            // ---- String object (static members) ----
            var strObj = new Dictionary<string, object>
            {
                ["fromCharCode"] = (Func<object, object>)(c => Char.ConvertFromUtf32((int)Number(c))),
            };

            // ---- Utils (ScriptUtils semantics) ----
            var utils = new Dictionary<string, object>
            {
                ["rgb"] = (Func<object, object, object, object>)((r, g, b) => (double)((int)Number(r) << 16 | (int)Number(g) << 8 | (int)Number(b))),
                ["hue"] = (Func<object, object>)(h =>
                {
                    int p = (int)Number(h) % 360;
                    double r = 0, g = 0, b = 0;
                    if (p > 0 && p < 240) r = 100 - 50 * Math.Abs(p - 120) / 120.0;
                    if (p > 240 && p < 360) g = 100 - 50 * Math.Abs(p - 240) / 120.0;
                    if (p > 240 && p <= 360) b = 100 - 50 * Math.Abs(p - 360) / 120.0;
                    else if (p + 360 >= 240 && p + 360 < 360) b = 100 - 50 * Math.Abs(p + 360 - 240) / 120.0;
                    return (double)((int)(r * 255 / 100) << 16 | (int)(g * 255 / 100) << 8 | (int)(b * 255 / 100));
                }),
                ["formatTimes"] = (Func<object, object>)(s =>
                {
                    double t = Number(s);
                    if (t < 0) return "-" + Str(FormatTimes(-t));
                    int sec = (int)Math.Floor(t % 60);
                    int min = (int)Math.Floor(t / 60);
                    return (min < 10 ? "0" : "") + min + ":" + ("0" + sec).Substring(Math.Max(0, ("0" + sec).Length - 2));
                }),
                ["distance"] = (Func<object, object, object, object, object>)((x1, y1, x2, y2) =>
                    (double)Math.Sqrt((Number(x2) - Number(x1)) * (Number(x2) - Number(x1)) + (Number(y2) - Number(y1)) * (Number(y2) - Number(y1)))),
                ["rand"] = (Func<object, object, object>)((a, b) => (double)Math.Floor(Number(a) + new Random().NextDouble() * (Number(b) - Number(a)))),
                ["clone"] = (Func<object, object>)(v => Clone(v)),
                ["foreach"] = (Func<object, object, object>)((o, cb) =>
                {
                    if (cb == null) return null;
                    if (o is Dictionary<string, object> d)
                    {
                        foreach (var kv in d)
                            vm.InvokeFunction(cb, null, new List<object> { kv.Key, kv.Value });
                    }
                    else if (o is List<object> l)
                    {
                        for (int i = 0; i < l.Count; i++)
                            vm.InvokeFunction(cb, null, new List<object> { (double)i, l[i] });
                    }
                    return null;
                }),
            };

            // ---- delay / interval scheduling ----
            utils["delay"] = (Func<object, object, object>)((fn, ms) =>
            {
                double t = ms != null ? Number(ms) : 1000;
                var func = fn;
                if (!host.EnableTimers || !(func is Delegate || func is VirtualMachine.FuncObject))
                {
                    // No real timers: invoke once synchronously (used by tests / heads-up).
                    vm.InvokeFunction(func, null, new List<object>());
                    return (double)0;
                }
#if !WINDOWS_UWP
                var timer = new System.Threading.Timer(_ =>
                {
                    try { vm.InvokeFunction(func, null, new List<object>()); }
                    catch { }
                }, null, (int)Math.Max(1, t), System.Threading.Timeout.Infinite);
                return (double)(timer.GetHashCode());
#else
                return 0d; // UWP host supplies its own dispatcher-based delay
#endif
            });
            utils["interval"] = (Func<object, object, object, object>)((fn, ms, times) =>
            {
                double dt = ms != null ? Number(ms) : 1000;
                int cnt = times != null ? (int)Number(times) : 0;
                var func = fn;
#if !WINDOWS_UWP
                var timer = new System.Threading.Timer(_ =>
                {
                    try { vm.InvokeFunction(func, null, new List<object>()); }
                    catch { }
                }, null, (int)Math.Max(1, dt), (int)Math.Max(1, dt));
                return new Dictionary<string, object> { ["stop"] = (Func<object>)(() => { timer.Dispose(); return null; }) };
#else
                return new Dictionary<string, object> { ["stop"] = (Func<object>)(() => null) };
#endif
            });

            // ---- Global / $G ----
            var g = new Dictionary<string, object>
            {
                ["_get"] = (Func<object, object>)(k => store.Get(Str(k))),
                ["_set"] = (Func<object, object, object>)((k, v) => store.Set(Str(k), v)),
                ["_"] = (Func<object, object>)(k => store.Get(Str(k))),
            };

            // ---- inject ----
            global["trace"] = trace;
            global["clear"] = clear;
            global["getTimer"] = getTimer;
            global["parseInt"] = parseInt;
            global["parseFloat"] = parseFloat;
            global["stopExecution"] = stopExec;
            global["Math"] = math;
            global["String"] = strObj;
            global["Utils"] = utils;
            global["Global"] = g;
            global["$G"] = g;

            return store;
        }

        private static string FormatTimes(double t)
        {
            int sec = (int)Math.Floor(t % 60);
            int min = (int)Math.Floor(t / 60);
            return (min < 10 ? "0" : "") + min + ":" + ("0" + sec).Substring(Math.Max(0, ("0" + sec).Length - 2));
        }
    }
}