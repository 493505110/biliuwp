using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using scripting;
using Windows.UI;

namespace BiliBili.UWP.Controls
{
    /// <summary>
    /// Win2D/XAML 渲染宿主：把 M8 代码弹幕元素树绘制到视频弹幕层上方的
    /// CanvasControl 画布，与 NSDanmaku 普通弹幕叠加显示。
    /// 元素动画由宿主按键帧驱动 M8Motion.Step，播放器状态变化由 PlayerPage 同步。
    /// </summary>
    public sealed class M8Win2DRenderHost : IM8RenderHost
    {
        private readonly CanvasControl _canvas;
        private readonly M8Element _root;
        private readonly List<M8Element> _elements = new List<M8Element>();
        private readonly object _sync = new object();
        private long _lastTimestamp;

        public M8Win2DRenderHost(CanvasControl canvas)
        {
            this._canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            this._root = new M8Element();
            this._root.Set("type", "root");
            this._canvas.Draw += this.OnDraw;
        }

        // ---- IM8RenderHost: 播放状态（由 PlayerPage 同步） ----

        public string State { get; set; } = PlayerState.PAUSED;

        public double Stime { get; set; }

        public double Volume { get; set; } = 100d;

        public double StageWidth
        {
            get { return this._canvas.ActualWidth; }
        }

        public double StageHeight
        {
            get { return this._canvas.ActualHeight; }
        }

        public object Root
        {
            get { return this._root; }
        }

        // ---- 播放器动作转发（PlayerPage 挂接） ----

        public event Action PlayRequested;

        public event Action PauseRequested;

        public event Action<double> SeekRequested;

        public event Action<string, int, bool> JumpRequested;

        public void Play()
        {
            var handler = this.PlayRequested;
            if (handler != null) handler();
        }

        public void Pause()
        {
            var handler = this.PauseRequested;
            if (handler != null) handler();
        }

        public void Seek(double seconds)
        {
            var handler = this.SeekRequested;
            if (handler != null) handler(seconds);
        }

        public void Jump(string av, int page, bool newWindow)
        {
            var handler = this.JumpRequested;
            if (handler != null) handler(av, page, newWindow);
        }

        public Dictionary<string, object> CreateSound(string name, Dictionary<string, object> callbacks)
        {
            // 声音播放由脚本侧 callbacks 驱动，宿主暂不提供音效后端。
            return callbacks ?? new Dictionary<string, object>(StringComparer.Ordinal);
        }

        // ---- 元素生命周期 ----

        public void AddElement(M8Element element, object parent)
        {
            if (element == null) return;
            lock (this._sync)
            {
                if (!this._elements.Contains(element)) this._elements.Add(element);
            }
            this._canvas.Invalidate();
        }

        public void RemoveElement(M8Element element)
        {
            if (element == null) return;
            lock (this._sync) this._elements.Remove(element);
        }

        public void InvokeCommentTrigger(object comment)
        {
        }

        public void InvokeKeyTrigger(int keyCode, bool isUp)
        {
        }

        /// <summary>解除画布事件订阅（页面退出时调用）。</summary>
        public void Release()
        {
            this._canvas.Draw -= this.OnDraw;
            lock (this._sync) this._elements.Clear();
        }

        // ---- 绘制 ----

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var drawingSession = args.DrawingSession;
            long timestamp = Stopwatch.GetTimestamp();
            double frameMs = this._lastTimestamp == 0
                ? 16d
                : (timestamp - this._lastTimestamp) * 1000d / Stopwatch.Frequency;
            this._lastTimestamp = timestamp;

            M8Element[] snapshot;
            lock (this._sync) snapshot = this._elements.ToArray();

            foreach (var element in snapshot)
            {
                if (element == null) continue;
                element.motionManager.Step(frameMs);
                this.DrawElement(drawingSession, element, Matrix3x2.Identity, 1d);
            }

            // 有活跃元素时持续重绘以驱动动画；全部结束后停止，节省资源。
            if (snapshot.Length > 0) sender.Invalidate();
            else this._lastTimestamp = 0;
        }

        private void DrawElement(CanvasDrawingSession session, M8Element element, Matrix3x2 parentTransform, double parentAlpha)
        {
            if (element == null || !element.visible) return;
            double alpha = parentAlpha * this.Clamp(element.alpha, 0d, 1d);
            if (alpha < 0.01d) return;

            var transform = parentTransform
                * Matrix3x2.CreateScale((float)element.scaleX, (float)element.scaleY, Vector2.Zero)
                * Matrix3x2.CreateRotation((float)(element.rotation * Math.PI / 180d), Vector2.Zero)
                * Matrix3x2.CreateTranslation((float)element.x, (float)element.y);
            session.Transform = transform;

            string type = element.type as string;
            if (type == "comment" || type == "textField")
            {
                this.DrawTextElement(session, element, alpha);
            }
            else if (type == "button")
            {
                this.DrawButtonElement(session, element, alpha);
            }
            else if (type == "shape")
            {
                this.DrawShapeElement(session, element, alpha);
            }

            foreach (var child in element.children)
            {
                var childElement = child as M8Element;
                if (childElement != null) this.DrawElement(session, childElement, transform, alpha);
            }

            session.Transform = parentTransform;
        }

        private void DrawTextElement(CanvasDrawingSession session, M8Element element, double alpha)
        {
            string text = element.text;
            if (string.IsNullOrEmpty(text)) return;
            using (var format = new CanvasTextFormat())
            {
                format.FontSize = (float)Math.Max(8d, element.fontsize);
                format.FontFamily = "Microsoft YaHei";
                session.DrawText(text, 0f, 0f, this.ToColor(element.color, alpha), format);
            }
        }

        private void DrawButtonElement(CanvasDrawingSession session, M8Element element, double alpha)
        {
            float width = (float)Math.Max(8d, element.width);
            float height = (float)Math.Max(8d, element.height);
            var fill = this.ToColor(element.color, alpha * 0.25d);
            session.FillRectangle(0f, 0f, width, height, fill);
            session.DrawRectangle(0f, 0f, width, height, this.ToColor(element.color, alpha));

            string text = element.text;
            if (string.IsNullOrEmpty(text)) return;
            using (var format = new CanvasTextFormat())
            {
                format.FontSize = (float)Math.Max(8d, element.fontsize);
                format.FontFamily = "Microsoft YaHei";
                format.VerticalAlignment = CanvasVerticalAlignment.Center;
                format.HorizontalAlignment = CanvasHorizontalAlignment.Center;
                session.DrawText(text, 0f, 0f, width, height, this.ToColor(element.color, alpha), format);
            }
        }

        private void DrawShapeElement(CanvasDrawingSession session, M8Element element, double alpha)
        {
            float width = (float)(element.width > 0d ? element.width : 60d);
            float height = (float)(element.height > 0d ? element.height : 60d);
            session.FillRectangle(0f, 0f, width, height, this.ToColor(element.color, alpha));
        }

        private Color ToColor(double colorValue, double alpha)
        {
            int value = (int)Math.Max(0d, Math.Min(16777215d, colorValue));
            byte r = (byte)((value >> 16) & 0xFF);
            byte g = (byte)((value >> 8) & 0xFF);
            byte b = (byte)(value & 0xFF);
            byte a = (byte)(Math.Max(0d, Math.Min(1d, alpha)) * 255d);
            return Color.FromArgb(a, r, g, b);
        }

        private double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}