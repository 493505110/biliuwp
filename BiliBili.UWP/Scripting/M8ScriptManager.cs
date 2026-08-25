using System;
using System.Collections.Generic;

namespace scripting
{
    /// <summary>Runtime ownership for M8 elements, timers, and input hooks.</summary>
    public sealed class M8ScriptManager : IM8ScriptObject
    {
        private readonly IM8RenderHost _renderHost;
        private M8PlayerApi _player;

        public M8ScriptManager(IM8RenderHost renderHost = null)
        {
            this._renderHost = renderHost;
            this.Elements = new List<M8Element>();
            this.Timers = new List<object>();
        }

        public List<M8Element> Elements { get; private set; }

        public List<object> Timers { get; private set; }

        public List<M8Element> elements
        {
            get { return this.Elements; }
        }

        public List<object> timers
        {
            get { return this.Timers; }
        }

        public M8PlayerApi Player
        {
            get { return this._player; }
        }

        public void AttachPlayer(M8PlayerApi player)
        {
            this._player = player;
        }

        public void PushTimer(object timer)
        {
            if (timer != null && !this.Timers.Contains(timer)) this.Timers.Add(timer);
        }

        public void pushTimer(object timer)
        {
            this.PushTimer(timer);
        }

        public void PopTimer(object timer)
        {
            if (timer != null) this.Timers.Remove(timer);
            StopTimer(timer);
        }

        public void popTimer(object timer)
        {
            this.PopTimer(timer);
        }

        public void ClearTimer()
        {
            var timers = this.Timers.ToArray();
            this.Timers.Clear();
            foreach (var timer in timers) StopTimer(timer);
        }

        public void clearTimer()
        {
            this.ClearTimer();
        }

        public void PushEl(M8Element element)
        {
            if (element != null && !this.Elements.Contains(element)) this.Elements.Add(element);
        }

        public void pushEl(M8Element element)
        {
            this.PushEl(element);
        }

        public void PushEl(object element)
        {
            this.PushEl(element as M8Element);
        }

        public void PopEl(M8Element element)
        {
            if (element == null) return;
            var wasOwned = this.Elements.Remove(element);
            element.motionManager?.Stop();
            if (wasOwned && this._renderHost != null) this._renderHost.RemoveElement(element);
        }

        public void popEl(M8Element element)
        {
            this.PopEl(element);
        }

        public void PopEl(object element)
        {
            this.PopEl(element as M8Element);
        }

        public void ClearEl()
        {
            var elements = this.Elements.ToArray();
            this.Elements.Clear();
            foreach (var element in elements)
            {
                if (element == null) continue;
                element.motionManager?.Stop();
                if (this._renderHost != null) this._renderHost.RemoveElement(element);
            }
        }

        public void clearEl()
        {
            this.ClearEl();
        }

        public void ClearTrigger()
        {
            if (this._player != null) this._player.ClearTriggers();
        }

        public void clearTrigger()
        {
            this.ClearTrigger();
        }

        /// <summary>Advances all active motion managers by milliseconds.</summary>
        public void Step(double milliseconds)
        {
            var elements = this.Elements.ToArray();
            foreach (var element in elements)
            {
                if (element != null && this.Elements.Contains(element)) element.motionManager?.Step(milliseconds);
            }
        }

        public void step(double milliseconds)
        {
            this.Step(milliseconds);
        }

        /// <summary>Applies the same play/pause propagation as ScriptManager.stateHandler.</summary>
        public void OnPlayerStateChanged(string oldState, string newState)
        {
            var elements = this.Elements.ToArray();
            if (newState == PlayerState.PLAYING)
            {
                foreach (var element in elements)
                {
                    if (element != null && this.Elements.Contains(element)) element.motionManager?.Play();
                }
            }
            else if (oldState == PlayerState.PLAYING)
            {
                foreach (var element in elements)
                {
                    if (element != null && this.Elements.Contains(element)) element.motionManager?.Stop();
                }
            }
        }

        public void onPlayerStateChanged(string oldState, string newState)
        {
            this.OnPlayerStateChanged(oldState, newState);
        }

        public object Get(string key)
        {
            if (key == null) return null;
            switch (key)
            {
                case "elements": return this.Elements;
                case "timers": return this.Timers;
                case "pushTimer": return (Func<object, object>)(timer => { this.PushTimer(timer); return null; });
                case "popTimer": return (Func<object, object>)(timer => { this.PopTimer(timer); return null; });
                case "clearTimer": return (Func<object>)(() => { this.ClearTimer(); return null; });
                case "pushEl": return (Func<object, object>)(element => { this.PushEl(element); return null; });
                case "popEl": return (Func<object, object>)(element => { this.PopEl(element); return null; });
                case "clearEl": return (Func<object>)(() => { this.ClearEl(); return null; });
                case "clearTrigger": return (Func<object>)(() => { this.ClearTrigger(); return null; });
                default: return null;
            }
        }

        public void Set(string key, object value)
        {
        }

        private static void StopTimer(object timer)
        {
            if (timer == null) return;
            var disposable = timer as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
                return;
            }

            var stop = VirtualMachine.GetMember(timer, "stop");
            var delegateValue = stop as Delegate;
            if (delegateValue != null)
            {
                try { delegateValue.DynamicInvoke(); }
                catch { }
            }
        }
    }
}
