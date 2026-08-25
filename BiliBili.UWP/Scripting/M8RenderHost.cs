using System;
using System.Collections.Generic;

namespace scripting
{
    /// <summary>Player state names used by the original JWPlayer API.</summary>
    public static class PlayerState
    {
        public const string IDLE = "IDLE";
        public const string BUFFERING = "BUFFERING";
        public const string PLAYING = "PLAYING";
        public const string PAUSED = "PAUSED";
        public const string STOPED = "STOPED";
        public const string STOPPED = "STOPPED";
    }

    /// <summary>
    /// Rendering and player operations required by the script-facing API.
    /// The UWP adapter can implement this interface without exposing UWP types
    /// to the scripting assembly.
    /// </summary>
    public interface IM8RenderHost
    {
        string State { get; set; }
        double Stime { get; set; }
        double Volume { get; set; }

        void Play();
        void Pause();
        void Seek(double seconds);
        void Jump(string av, int page, bool newWindow);
        Dictionary<string, object> CreateSound(string name, Dictionary<string, object> callbacks);

        double StageWidth { get; }
        double StageHeight { get; }
        object Root { get; }

        void AddElement(M8Element element, object parent);
        void RemoveElement(M8Element element);

        void InvokeCommentTrigger(object comment);
        void InvokeKeyTrigger(int keyCode, bool isUp);
    }

    /// <summary>Headless host used by the pure VM overload and unit tests.</summary>
    public sealed class M8NullRenderHost : IM8RenderHost
    {
        private readonly M8Element _root;

        public M8NullRenderHost()
        {
            this._root = new M8Element();
            this._root.Set("type", "root");
        }

        public string State { get; set; } = PlayerState.PAUSED;

        public double Stime { get; set; }

        public double Volume { get; set; } = 100d;

        public double StageWidth { get; set; }

        public double StageHeight { get; set; }

        public object Root
        {
            get { return this._root; }
        }

        public void Play()
        {
            this.State = PlayerState.PLAYING;
        }

        public void Pause()
        {
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
            return callbacks ?? new Dictionary<string, object>(StringComparer.Ordinal);
        }

        public void AddElement(M8Element element, object parent)
        {
            if (element == null) return;
            var container = parent as M8Element;
            if (container != null) container.AddChild(element);
            else element.Set("parent", parent);
        }

        public void RemoveElement(M8Element element)
        {
            if (element == null) return;
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
}
