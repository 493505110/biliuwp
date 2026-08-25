using System;
using System.Collections.Generic;
using System.Threading;

namespace scripting
{
    /// <summary>Script-facing player facade backed by an IM8RenderHost.</summary>
    public sealed class M8PlayerApi : IM8ScriptObject
    {
        private sealed class TriggerRegistration
        {
            public object Callback;
            public bool IsKey;
            public bool IsUp;
        }

        private readonly IM8RenderHost _renderHost;
        private readonly VirtualMachine _vm;
        private readonly List<TriggerRegistration> _commentTriggers = new List<TriggerRegistration>();
        private readonly List<TriggerRegistration> _keyTriggers = new List<TriggerRegistration>();
        private readonly Dictionary<uint, Timer> _triggerTimers = new Dictionary<uint, Timer>();
        private readonly object _triggerLock = new object();
        private M8ScriptManager _scriptManager;
        private string _state;
        private double _stime;
        private double _volume;
        private int _refreshRate;
        private uint _nextTriggerId;
        private object _mask;

        public M8PlayerApi()
            : this(new M8NullRenderHost())
        {
        }

        public M8PlayerApi(IM8RenderHost renderHost)
            : this(renderHost, null, null)
        {
        }

        public M8PlayerApi(IM8RenderHost renderHost, M8ScriptManager scriptManager)
            : this(renderHost, null, scriptManager)
        {
        }

        public M8PlayerApi(IM8RenderHost renderHost, VirtualMachine vm, M8ScriptManager scriptManager = null)
        {
            this._renderHost = renderHost ?? new M8NullRenderHost();
            this._vm = vm;
            this._scriptManager = scriptManager;
            this._state = ReadHostState(this._renderHost.State, PlayerState.PAUSED);
            this._stime = this._renderHost.Stime;
            this._volume = this._renderHost.Volume;
            if (double.IsNaN(this._volume)) this._volume = 100d;
            if (this._scriptManager != null) this._scriptManager.AttachPlayer(this);
        }

        public M8ScriptManager ScriptManager
        {
            get { return this._scriptManager; }
        }

        public string state
        {
            get { return ReadState(); }
            set { SetState(value); }
        }

        public double stime
        {
            get
            {
                var value = this._renderHost.Stime;
                if (double.IsNaN(value)) return this._stime;
                this._stime = value;
                return value;
            }
            set
            {
                this._stime = NormalizeNumber(value);
                this._renderHost.Stime = this._stime;
            }
        }

        /// <summary>AS3 ScriptPlayer.time is stime expressed in milliseconds.</summary>
        public double time
        {
            get { return this.stime * 1000d; }
            set { this.stime = NormalizeNumber(value) / 1000d; }
        }

        public double volume
        {
            get
            {
                var value = this._renderHost.Volume;
                if (double.IsNaN(value)) return this._volume;
                this._volume = value;
                return value;
            }
            set
            {
                this._volume = NormalizeNumber(value);
                this._renderHost.Volume = this._volume;
            }
        }

        public List<object> commentList { get; } = new List<object>();

        public int refreshRate
        {
            get { return this._refreshRate; }
            set { this._refreshRate = value; }
        }

        public double width
        {
            get { return this._renderHost.StageWidth; }
        }

        public double height
        {
            get { return this._renderHost.StageHeight; }
        }

        public double videoWidth
        {
            get { return this._renderHost.StageWidth; }
        }

        public double videoHeight
        {
            get { return this._renderHost.StageHeight; }
        }

        public bool isContinueMode
        {
            get { return false; }
        }

        public object mask
        {
            get { return this._mask; }
        }

        public void AttachScriptManager(M8ScriptManager scriptManager)
        {
            this._scriptManager = scriptManager;
            if (scriptManager != null) scriptManager.AttachPlayer(this);
        }

        public void play()
        {
            var oldState = this.ReadState();
            this._renderHost.Play();
            this.SetStateAfterHostCall(oldState, PlayerState.PLAYING);
        }

        public void pause()
        {
            var oldState = this.ReadState();
            this._renderHost.Pause();
            this.SetStateAfterHostCall(oldState, PlayerState.PAUSED);
        }

        /// <summary>Matches ScriptPlayer.seek: the script argument is milliseconds.</summary>
        public void seek(double milliseconds)
        {
            var seconds = NormalizeNumber(milliseconds) / 1000d;
            this._renderHost.Seek(seconds);
            this.stime = seconds;
        }

        public void jump(string av, int page = 1, bool newWindow = false)
        {
            this._renderHost.Jump(av, page, newWindow);
        }

        public uint commentTrigger(object callback, double timeout = 1000d)
        {
            if (callback == null) return 0;
            var registration = new TriggerRegistration { Callback = callback, IsKey = false };
            return this.RegisterTrigger(this._commentTriggers, registration, timeout);
        }

        public uint keyTrigger(object callback, double timeout = 1000d, bool isUp = false)
        {
            if (callback == null) return 0;
            var registration = new TriggerRegistration { Callback = callback, IsKey = true, IsUp = isUp };
            return this.RegisterTrigger(this._keyTriggers, registration, timeout);
        }

        public uint keyTriggerCapture(object callback, double timeout = 1000d, bool isUp = false)
        {
            return this.keyTrigger(callback, timeout, isUp);
        }

        public void setMask(object mask)
        {
            this._mask = mask;
            var element = mask as M8Element;
            if (element != null) element.Set("mask", mask);
        }

        public Dictionary<string, object> createSound(string name, object onLoad = null)
        {
            var callbacks = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["name"] = name,
                ["onLoad"] = onLoad
            };

            callbacks["Play"] = (Action)(() => this._renderHost.CreateSound(name, callbacks));
            callbacks["Stop"] = (Action)(() => { });
            callbacks["Remove"] = (Action)(() => { });
            callbacks["LoadPercent"] = (Func<object>)(() => 0d);
            callbacks["play"] = (Func<object[], object>)(args => { this._renderHost.CreateSound(name, callbacks); return null; });
            callbacks["stop"] = (Func<object>)(() => null);
            callbacks["remove"] = (Func<object>)(() => null);
            callbacks["loadPercent"] = (Func<object>)(() => 0d);

            var sound = this._renderHost.CreateSound(name, callbacks);
            return sound ?? callbacks;
        }

        public Dictionary<string, object> CreateSound(string name, object onLoad = null)
        {
            return this.createSound(name, onLoad);
        }

        public bool InvokeCommentTrigger(object comment)
        {
            TriggerRegistration[] registrations;
            lock (this._triggerLock) registrations = this._commentTriggers.ToArray();
            if (registrations.Length == 0) return false;
            foreach (var registration in registrations) this.InvokeCallback(registration.Callback, comment);
            return true;
        }

        public bool invokeCommentTrigger(object comment)
        {
            return this.InvokeCommentTrigger(comment);
        }

        public bool InvokeCommentTrigger()
        {
            return this.InvokeCommentTrigger(null);
        }

        public bool InvokeKeyTrigger(int keyCode, bool isUp = false)
        {
            TriggerRegistration[] registrations;
            lock (this._triggerLock) registrations = this._keyTriggers.ToArray();
            if (registrations.Length == 0) return false;
            foreach (var registration in registrations)
            {
                if (registration.IsUp == isUp) this.InvokeCallback(registration.Callback, (double)keyCode);
            }
            return true;
        }

        public bool invokeKeyTrigger(int keyCode, bool isUp = false)
        {
            return this.InvokeKeyTrigger(keyCode, isUp);
        }

        public bool InvokeKeyTrigger()
        {
            return this.InvokeKeyTrigger(0, false);
        }

        public void ClearTriggers()
        {
            Timer[] timers;
            lock (this._triggerLock)
            {
                this._commentTriggers.Clear();
                this._keyTriggers.Clear();
                timers = new List<Timer>(this._triggerTimers.Values).ToArray();
                this._triggerTimers.Clear();
            }
            foreach (var timer in timers) timer.Dispose();
        }

        public void clearTriggers()
        {
            this.ClearTriggers();
        }

        public void InvokeCallback(object callback, params object[] arguments)
        {
            this.InvokeCallbackInternal(callback, arguments == null ? new List<object>() : new List<object>(arguments));
        }

        public void SetState(string value)
        {
            var state = ReadHostState(value, PlayerState.PAUSED);
            var oldState = this.ReadState();
            this._state = state;
            this._renderHost.State = state;
            if (this._scriptManager != null) this._scriptManager.OnPlayerStateChanged(oldState, state);
        }

        public object Get(string key)
        {
            if (key == null) return null;
            switch (key)
            {
                case "state": return this.state;
                case "stime": return this.stime;
                case "time": return this.time;
                case "volume": return this.volume;
                case "commentList": return this.commentList;
                case "refreshRate": return (double)this.refreshRate;
                case "width": return this.width;
                case "height": return this.height;
                case "videoWidth": return this.videoWidth;
                case "videoHeight": return this.videoHeight;
                case "isContinueMode": return this.isContinueMode;
                case "mask": return this.mask;
                case "play": return (Func<object>)(() => { this.play(); return null; });
                case "pause": return (Func<object>)(() => { this.pause(); return null; });
                case "seek": return (Func<object, object>)(milliseconds => { this.seek(VirtualMachine.ToNumber(milliseconds)); return null; });
                case "jump": return (Func<object[], object>)(args =>
                {
                    var av = args.Length > 0 ? VirtualMachine.As3String(args[0]) : null;
                    var page = args.Length > 1 ? (int)VirtualMachine.ToNumber(args[1]) : 1;
                    var newWindow = args.Length > 2 && VirtualMachine.Truthy(args[2]);
                    this.jump(av, page, newWindow);
                    return null;
                });
                case "commentTrigger": return (Func<object[], object>)(args =>
                {
                    var callback = args.Length > 0 ? args[0] : null;
                    var timeout = args.Length > 1 ? VirtualMachine.ToNumber(args[1]) : 1000d;
                    return (double)this.commentTrigger(callback, timeout);
                });
                case "keyTrigger": return (Func<object[], object>)(args =>
                {
                    var callback = args.Length > 0 ? args[0] : null;
                    var timeout = args.Length > 1 ? VirtualMachine.ToNumber(args[1]) : 1000d;
                    var isUp = args.Length > 2 && VirtualMachine.Truthy(args[2]);
                    return (double)this.keyTrigger(callback, timeout, isUp);
                });
                case "keyTriggerCapture": return (Func<object[], object>)(args =>
                {
                    var callback = args.Length > 0 ? args[0] : null;
                    var timeout = args.Length > 1 ? VirtualMachine.ToNumber(args[1]) : 1000d;
                    var isUp = args.Length > 2 && VirtualMachine.Truthy(args[2]);
                    return (double)this.keyTriggerCapture(callback, timeout, isUp);
                });
                case "setMask": return (Func<object, object>)(mask => { this.setMask(mask); return null; });
                case "createSound": return (Func<object[], object>)(args =>
                {
                    var name = args.Length > 0 ? VirtualMachine.As3String(args[0]) : null;
                    var onLoad = args.Length > 1 ? args[1] : null;
                    return this.createSound(name, onLoad);
                });
                case "clearTriggers": return (Func<object>)(() => { this.ClearTriggers(); return null; });
                default: return null;
            }
        }

        public void Set(string key, object value)
        {
            if (key == null) return;
            switch (key)
            {
                case "state": this.SetState(VirtualMachine.As3String(value)); break;
                case "stime": this.stime = VirtualMachine.ToNumber(value); break;
                case "time": this.time = VirtualMachine.ToNumber(value); break;
                case "volume": this.volume = VirtualMachine.ToNumber(value); break;
                case "mask": this.setMask(value); break;
                case "refreshRate": this.refreshRate = (int)VirtualMachine.ToNumber(value); break;
            }
        }

        private uint RegisterTrigger(List<TriggerRegistration> registrations, TriggerRegistration registration, double timeout)
        {
            uint id;
            lock (this._triggerLock)
            {
                do
                {
                    this._nextTriggerId++;
                    if (this._nextTriggerId == 0) this._nextTriggerId++;
                    id = this._nextTriggerId;
                }
                while (this._triggerTimers.ContainsKey(id));
                registrations.Add(registration);
            }

            var delay = double.IsNaN(timeout) || timeout < 0 ? 0 : Math.Min(int.MaxValue, timeout);
            var timer = new Timer(_ => RemoveTrigger(id, registration), null, (int)delay, Timeout.Infinite);
            lock (this._triggerLock) this._triggerTimers[id] = timer;
            return id;
        }

        private void RemoveTrigger(uint id, TriggerRegistration registration)
        {
            Timer timer = null;
            lock (this._triggerLock)
            {
                if (registration.IsKey) this._keyTriggers.Remove(registration);
                else this._commentTriggers.Remove(registration);
                if (this._triggerTimers.TryGetValue(id, out timer)) this._triggerTimers.Remove(id);
            }
            if (timer != null) timer.Dispose();
        }

        private void InvokeCallback(object callback, object argument)
        {
            this.InvokeCallbackInternal(callback, new List<object> { argument });
        }

        private void InvokeCallbackInternal(object callback, List<object> arguments)
        {
            try
            {
                if (this._vm != null)
                {
                    this._vm.InvokeFunction(callback, null, arguments);
                }
                else if (callback is Delegate callbackDelegate)
                {
                    callbackDelegate.DynamicInvoke(arguments.ToArray());
                }
            }
            catch
            {
            }
        }

        private string ReadState()
        {
            this._state = ReadHostState(this._renderHost.State, this._state);
            return this._state;
        }

        private void SetStateAfterHostCall(string oldState, string state)
        {
            this._state = state;
            this._renderHost.State = state;
            if (this._scriptManager != null) this._scriptManager.OnPlayerStateChanged(oldState, state);
        }

        private static string ReadHostState(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static double NormalizeNumber(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? 0d : value;
        }
    }
}
