using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace scripting
{
    /// <summary>Externally driven ticker used by all M8 tweens.</summary>
    public sealed class M8Ticker
    {
        private static readonly M8Ticker _instance = new M8Ticker();
        private readonly List<M8Tween> _tweens = new List<M8Tween>();
        private double _time;

        private M8Ticker()
        {
        }

        public static M8Ticker Instance
        {
            get { return _instance; }
        }

        public static M8Ticker instance
        {
            get { return _instance; }
        }

        public double Time
        {
            get { return this._time; }
        }

        public double time
        {
            get { return this._time; }
        }

        public static M8Ticker Current
        {
            get { return _instance; }
        }

        public static M8Ticker GetInstance()
        {
            return _instance;
        }

        public void Step(double milliseconds)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0) return;
            this._time += milliseconds;
            var active = this._tweens.ToArray();
            foreach (var tween in active)
            {
                if (this._tweens.Contains(tween)) tween.AdvanceTo(this._time);
            }
        }

        public void Reset()
        {
            var active = this._tweens.ToArray();
            foreach (var tween in active) tween.Stop();
            this._tweens.Clear();
            this._time = 0;
        }

        public void Clear()
        {
            this.Reset();
        }

        internal void Add(M8Tween tween)
        {
            if (!this._tweens.Contains(tween)) this._tweens.Add(tween);
        }

        internal void Remove(M8Tween tween)
        {
            this._tweens.Remove(tween);
        }
    }

    /// <summary>BetweenAS3 object tween with an externally driven clock.</summary>
    public sealed class M8Tween
    {
        private sealed class PropertyTween
        {
            public string Key;
            public object Source;
            public object Destination;
            public bool HasSource;
            public bool HasDestination;
            public bool SourceRelative;
            public bool DestinationRelative;
            public bool Numeric;
            public double SourceNumber;
            public double DestinationNumber;
        }

        private readonly object _target;
        private readonly Dictionary<string, PropertyTween> _properties = new Dictionary<string, PropertyTween>(StringComparer.Ordinal);
        private readonly object _fromProperties;
        private readonly object _toProperties;
        private readonly double _duration;
        private readonly double _delay;
        private readonly object _easingValue;
        private M8Easing.EasingFunction _easing;
        private bool _resolved;
        private bool _isPlaying;
        private bool _isCompleted;
        private double _startTime;
        private double _position;
        private Action _complete;
        private Action<object> _completeObject;
        private Action<M8Tween> _completeTween;

        private M8Tween(object target, object fromProperties, object toProperties, double duration, double delay, object easing)
        {
            this._target = target;
            this._fromProperties = fromProperties;
            this._toProperties = toProperties;
            this._duration = Math.Max(0, duration);
            this._delay = Math.Max(0, delay);
            this._easingValue = easing;
        }

        public static M8Tween To(object target, object properties, double duration, double delay = 0, object easing = null)
        {
            return new M8Tween(target, null, properties, duration, delay, easing);
        }

        public static M8Tween FromTo(object target, object fromProperties, object toProperties, double duration, double delay = 0, object easing = null)
        {
            return new M8Tween(target, fromProperties, toProperties, duration, delay, easing);
        }

        public static M8Tween From(object target, object fromProperties, double duration, double delay = 0, object easing = null)
        {
            return new M8Tween(target, fromProperties, null, duration, delay, easing);
        }

        public static M8Tween Tween(object target, object toProperties, object fromProperties, double duration, object easing = null)
        {
            return new M8Tween(target, fromProperties, toProperties, duration, 0, easing);
        }

        public static M8Tween to(object target, object properties, double duration, double delay = 0, object easing = null)
        {
            return To(target, properties, duration, delay, easing);
        }

        public static M8Tween fromTo(object target, object fromProperties, object toProperties, double duration, double delay = 0, object easing = null)
        {
            return FromTo(target, fromProperties, toProperties, duration, delay, easing);
        }

        public static M8Tween from(object target, object fromProperties, double duration, double delay = 0, object easing = null)
        {
            return From(target, fromProperties, duration, delay, easing);
        }

        public static M8Tween tween(object target, object toProperties, object fromProperties, double duration, object easing = null)
        {
            return Tween(target, toProperties, fromProperties, duration, easing);
        }

        public static M8Tween TweenTo(object target, object properties, double duration, double delay = 0, object easing = null)
        {
            return To(target, properties, duration, delay, easing);
        }

        public static M8Tween TweenFromTo(object target, object fromProperties, object toProperties, double duration, double delay = 0, object easing = null)
        {
            return FromTo(target, fromProperties, toProperties, duration, delay, easing);
        }

        public static M8Tween TweenFrom(object target, object fromProperties, double duration, double delay = 0, object easing = null)
        {
            return From(target, fromProperties, duration, delay, easing);
        }

        public static M8Tween tweenTo(object target, object properties, double duration, double delay = 0, object easing = null)
        {
            return To(target, properties, duration, delay, easing);
        }

        public static M8Tween tweenFromTo(object target, object fromProperties, object toProperties, double duration, double delay = 0, object easing = null)
        {
            return FromTo(target, fromProperties, toProperties, duration, delay, easing);
        }

        public static M8Tween tweenFrom(object target, object fromProperties, double duration, double delay = 0, object easing = null)
        {
            return From(target, fromProperties, duration, delay, easing);
        }

        public object Target
        {
            get { return this._target; }
        }

        public object target
        {
            get { return this._target; }
        }

        public double Duration
        {
            get { return this._duration; }
        }

        public double duration
        {
            get { return this._duration; }
        }

        public double Delay
        {
            get { return this._delay; }
        }

        public double delay
        {
            get { return this._delay; }
        }

        public double Position
        {
            get { return this._position; }
        }

        public double position
        {
            get { return this._position; }
        }

        public bool IsPlaying
        {
            get { return this._isPlaying; }
        }

        public bool isPlaying
        {
            get { return this._isPlaying; }
        }

        public bool IsCompleted
        {
            get { return this._isCompleted; }
        }

        public Action OnComplete
        {
            get { return this._complete; }
            set { this._complete = value; }
        }

        public Action onComplete
        {
            get { return this._complete; }
            set { this._complete = value; }
        }

        public Action<M8Tween> OnCompleteWithTween
        {
            get { return this._completeTween; }
            set { this._completeTween = value; }
        }

        public Action<M8Tween> onCompleteWithTween
        {
            get { return this._completeTween; }
            set { this._completeTween = value; }
        }

        public event Action<M8Tween> Complete;
        public event Action<M8Tween> Completed;

        public void SetCompleteListener(Action listener)
        {
            this._complete = listener;
        }

        public void SetCompleteListener(Action<object> listener)
        {
            this._completeObject = listener;
        }

        public void setCompleteListener(Action listener)
        {
            this.SetCompleteListener(listener);
        }

        public void setCompleteListener(Action<object> listener)
        {
            this.SetCompleteListener(listener);
        }

        public void AddCompleteListener(Action listener)
        {
            this._complete += listener;
        }

        public void addCompleteListener(Action listener)
        {
            this.AddCompleteListener(listener);
        }

        public void Play()
        {
            if (this._isPlaying) return;
            if (this._isCompleted || this._position >= this._delay + this._duration)
            {
                this._position = 0;
                this._isCompleted = false;
            }
            if (!this._resolved) this.ResolveValues();
            this._startTime = M8Ticker.Instance.Time - this._position;
            this._isPlaying = true;
            M8Ticker.Instance.Add(this);
            this.Apply(this._position);
            if (this._delay + this._duration <= 0)
            {
                this.CompleteTween();
            }
        }

        public void play()
        {
            this.Play();
        }

        public void Stop()
        {
            if (!this._isPlaying) return;
            this._isPlaying = false;
            M8Ticker.Instance.Remove(this);
        }

        public void stop()
        {
            this.Stop();
        }

        public void Reset()
        {
            this.Stop();
            this._position = 0;
            this._isCompleted = false;
        }

        public void reset()
        {
            this.Reset();
        }

        public void TogglePause()
        {
            if (this._isPlaying) this.Stop();
            else this.Play();
        }

        public void togglePause()
        {
            this.TogglePause();
        }

        public void GotoAndStop(double milliseconds)
        {
            this.UpdatePosition(milliseconds);
            this.Stop();
        }

        public void gotoAndStop(double milliseconds)
        {
            this.GotoAndStop(milliseconds);
        }

        public void GotoAndPlay(double milliseconds)
        {
            this.UpdatePosition(milliseconds);
            this._isCompleted = false;
            this._startTime = M8Ticker.Instance.Time - this._position;
            this._isPlaying = true;
            M8Ticker.Instance.Add(this);
            this.Apply(this._position);
        }

        public void gotoAndPlay(double milliseconds)
        {
            this.GotoAndPlay(milliseconds);
        }

        /// <summary>Sets the tween position without changing its playing state.</summary>
        public void Update(double milliseconds)
        {
            this.UpdatePosition(milliseconds);
            this.Apply(this._position);
        }

        internal void AdvanceTo(double absoluteTime)
        {
            if (!this._isPlaying) return;
            var position = absoluteTime - this._startTime;
            if (position < 0) position = 0;
            this._position = position;
            this.Apply(position);
            if (position >= this._delay + this._duration)
            {
                this._position = this._delay + this._duration;
                this.Apply(this._position);
                this.CompleteTween();
            }
        }

        private void UpdatePosition(double milliseconds)
        {
            if (double.IsNaN(milliseconds)) milliseconds = 0;
            if (milliseconds < 0) milliseconds = 0;
            var total = this._delay + this._duration;
            this._position = milliseconds > total ? total : milliseconds;
            if (!this._resolved) this.ResolveValues();
        }

        private void CompleteTween()
        {
            if (this._isCompleted) return;
            this._isPlaying = false;
            this._isCompleted = true;
            M8Ticker.Instance.Remove(this);
            if (this._complete != null) this._complete();
            if (this._completeObject != null) this._completeObject(null);
            if (this._completeTween != null) this._completeTween(this);
            if (this.Complete != null) this.Complete(this);
            if (this.Completed != null) this.Completed(this);
        }

        private void ResolveValues()
        {
            this._properties.Clear();
            foreach (var item in EnumerateProperties(this._fromProperties))
            {
                var key = item.Key ?? string.Empty;
                var normalized = NormalizeKey(key, out var relative);
                if (!this._properties.TryGetValue(normalized, out var property))
                {
                    property = new PropertyTween { Key = normalized };
                    this._properties[normalized] = property;
                }
                property.Source = item.Value;
                property.HasSource = true;
                property.SourceRelative = relative;
            }
            foreach (var item in EnumerateProperties(this._toProperties))
            {
                var key = item.Key ?? string.Empty;
                var normalized = NormalizeKey(key, out var relative);
                if (!this._properties.TryGetValue(normalized, out var property))
                {
                    property = new PropertyTween { Key = normalized };
                    this._properties[normalized] = property;
                }
                property.Destination = item.Value;
                property.HasDestination = true;
                property.DestinationRelative = relative;
            }

            foreach (var property in this._properties.Values)
            {
                var current = ReadProperty(this._target, property.Key);
                var currentNumber = ToNumber(current);
                property.Numeric = IsNumeric(property.HasSource ? property.Source : current) ||
                    IsNumeric(property.HasDestination ? property.Destination : current);
                if (property.Numeric)
                {
                    property.SourceNumber = property.HasSource ? ToNumber(property.Source) : currentNumber;
                    property.DestinationNumber = property.HasDestination ? ToNumber(property.Destination) : currentNumber;
                    if (property.SourceRelative) property.SourceNumber += currentNumber;
                    if (property.DestinationRelative) property.DestinationNumber += currentNumber;
                }
            }
            this._easing = ResolveEasing(this._easingValue);
            this._resolved = true;
        }

        private void Apply(double position)
        {
            if (!this._resolved) this.ResolveValues();
            var local = position - this._delay;
            var progress = 0d;
            if (local >= this._duration)
            {
                progress = this._duration <= 0 ? 1 : 1;
            }
            else if (local > 0 && this._duration > 0)
            {
                progress = this._easing(local, 0, 1, this._duration);
            }

            foreach (var property in this._properties.Values)
            {
                if (property.Numeric)
                {
                    var value = property.SourceNumber * (1 - progress) + property.DestinationNumber * progress;
                    WriteProperty(this._target, property.Key, value);
                }
                else if (local <= 0)
                {
                    if (property.HasSource) WriteProperty(this._target, property.Key, property.Source);
                }
                else if (local >= this._duration && property.HasDestination)
                {
                    WriteProperty(this._target, property.Key, property.Destination);
                }
            }
        }

        private static M8Easing.EasingFunction ResolveEasing(object easing)
        {
            if (easing == null) return M8Easing.EaseNone;
            if (easing is M8Easing.EasingFunction function) return function;
            if (easing is Func<double, double, double, double, double> func)
            {
                return (time, begin, change, duration) => func(time, begin, change, duration);
            }
            if (easing is string name) return M8Easing.Get(name);
            if (easing is Delegate delegateValue)
            {
                return (time, begin, change, duration) =>
                    Convert.ToDouble(delegateValue.DynamicInvoke(time, begin, change, duration), CultureInfo.InvariantCulture);
            }
            return M8Easing.EaseNone;
        }

        private static IEnumerable<KeyValuePair<string, object>> EnumerateProperties(object properties)
        {
            if (properties == null) yield break;
            if (properties is M8Element element)
            {
                foreach (var item in element.Properties) yield return item;
                yield break;
            }
            if (properties is IDictionary<string, object> generic)
            {
                foreach (var item in generic) yield return item;
                yield break;
            }
            if (properties is IDictionary dictionary)
            {
                foreach (DictionaryEntry item in dictionary)
                {
                    yield return new KeyValuePair<string, object>(Convert.ToString(item.Key, CultureInfo.InvariantCulture), item.Value);
                }
                yield break;
            }
            var type = properties.GetType();
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                yield return new KeyValuePair<string, object>(property.Name, property.GetValue(properties, null));
            }
        }

        private static string NormalizeKey(string key, out bool relative)
        {
            relative = key.Length > 0 && key[0] == '$';
            return relative ? key.Substring(1) : key;
        }

        private static bool IsNumeric(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong ||
                value is float || value is double || value is decimal;
        }

        private static double ToNumber(object value)
        {
            return VirtualMachine.ToNumber(value);
        }

        private static object ReadProperty(object target, string key)
        {
            if (target == null) return null;
            if (target is IM8ScriptObject scriptObject) return scriptObject.Get(key);
            if (target is IDictionary<string, object> generic && generic.TryGetValue(key, out var value)) return value;
            if (target is IDictionary dictionary) return dictionary.Contains(key) ? dictionary[key] : null;
            var type = target.GetType();
            var property = type.GetProperty(key, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanRead) return property.GetValue(target, null);
            var field = type.GetField(key, BindingFlags.Instance | BindingFlags.Public);
            return field != null ? field.GetValue(target) : null;
        }

        private static void WriteProperty(object target, string key, object value)
        {
            if (target == null) return;
            if (target is IM8ScriptObject scriptObject)
            {
                scriptObject.Set(key, value);
                return;
            }
            if (target is IDictionary<string, object> generic)
            {
                generic[key] = value;
                return;
            }
            if (target is IDictionary dictionary)
            {
                dictionary[key] = value;
                return;
            }
            var type = target.GetType();
            var property = type.GetProperty(key, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, ConvertToType(value, property.PropertyType), null);
                return;
            }
            var field = type.GetField(key, BindingFlags.Instance | BindingFlags.Public);
            if (field != null) field.SetValue(target, ConvertToType(value, field.FieldType));
        }

        private static object ConvertToType(object value, Type type)
        {
            if (value == null) return null;
            if (type.IsInstanceOfType(value)) return value;
            if (type == typeof(double)) return ToNumber(value);
            if (type == typeof(float)) return (float)ToNumber(value);
            if (type == typeof(int)) return (int)ToNumber(value);
            if (type == typeof(long)) return (long)ToNumber(value);
            if (type == typeof(bool)) return VirtualMachine.Truthy(value);
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Short global-style facade matching BetweenAS3's Tween object.</summary>
    public static class Tween
    {
        public static M8Tween to(object target, object properties, double duration, double delay = 0, object easing = null)
        {
            return M8Tween.To(target, properties, duration, delay, easing);
        }

        public static M8Tween fromTo(object target, object fromProperties, object toProperties, double duration, double delay = 0, object easing = null)
        {
            return M8Tween.FromTo(target, fromProperties, toProperties, duration, delay, easing);
        }

        public static M8Tween from(object target, object properties, double duration, double delay = 0, object easing = null)
        {
            return M8Tween.From(target, properties, duration, delay, easing);
        }

        public static M8Tween To(object target, object properties, double duration, double delay = 0, object easing = null)
        {
            return M8Tween.To(target, properties, duration, delay, easing);
        }

        public static M8Tween FromTo(object target, object fromProperties, object toProperties, double duration, double delay = 0, object easing = null)
        {
            return M8Tween.FromTo(target, fromProperties, toProperties, duration, delay, easing);
        }

        public static M8Tween From(object target, object properties, double duration, double delay = 0, object easing = null)
        {
            return M8Tween.From(target, properties, duration, delay, easing);
        }

        public static M8Tween tweenTo(object target, object properties, double duration, double delay = 0, object easing = null)
        {
            return M8Tween.To(target, properties, duration, delay, easing);
        }

        public static M8Tween tweenFromTo(object target, object fromProperties, object toProperties, double duration, double delay = 0, object easing = null)
        {
            return M8Tween.FromTo(target, fromProperties, toProperties, duration, delay, easing);
        }

        public static M8Tween tweenFrom(object target, object properties, double duration, double delay = 0, object easing = null)
        {
            return M8Tween.From(target, properties, duration, delay, easing);
        }
    }

    /// <summary>BetweenAS3 easing formulas.</summary>
    public static class M8Easing
    {
        public delegate double EasingFunction(double time, double begin, double change, double duration);

        private static readonly Dictionary<string, EasingFunction> _table = CreateTable();

        public static IDictionary<string, EasingFunction> Table
        {
            get { return _table; }
        }

        public static IDictionary<string, EasingFunction> Functions
        {
            get { return _table; }
        }

        public static IDictionary<string, EasingFunction> EasingTable
        {
            get { return _table; }
        }

        public static EasingFunction Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return EaseNone;
            if (_table.TryGetValue(name, out var easing)) return easing;
            var lastDot = name.LastIndexOf('.');
            if (lastDot >= 0 && _table.TryGetValue(name.Substring(lastDot + 1), out easing)) return easing;
            return EaseNone;
        }

        public static double EaseNone(double time, double begin, double change, double duration)
        {
            return change * time / duration + begin;
        }

        public static double Linear(double time, double begin, double change, double duration)
        {
            return EaseNone(time, begin, change, duration);
        }

        public static double LinearEaseIn(double time, double begin, double change, double duration)
        {
            return EaseNone(time, begin, change, duration);
        }

        public static double LinearEaseOut(double time, double begin, double change, double duration)
        {
            return EaseNone(time, begin, change, duration);
        }

        public static double LinearEaseInOut(double time, double begin, double change, double duration)
        {
            return EaseNone(time, begin, change, duration);
        }

        public static double LinearEaseOutIn(double time, double begin, double change, double duration)
        {
            return EaseNone(time, begin, change, duration);
        }

        public static EasingFunction CustomFunctionEasing(EasingFunction function)
        {
            return function ?? EaseNone;
        }

        public static EasingFunction CustomFunctionEasing(Func<double, double, double, double, double> function)
        {
            if (function == null) return EaseNone;
            return (time, begin, change, duration) => function(time, begin, change, duration);
        }

        public static EasingFunction Custom(Func<double, double, double, double, double> function)
        {
            return CustomFunctionEasing(function);
        }

        public static double SineEaseIn(double time, double begin, double change, double duration)
        {
            return -change * Math.Cos(time / duration * (Math.PI / 2)) + change + begin;
        }

        public static double SineEaseOut(double time, double begin, double change, double duration)
        {
            return change * Math.Sin(time / duration * (Math.PI / 2)) + begin;
        }

        public static double SineEaseInOut(double time, double begin, double change, double duration)
        {
            return -change / 2 * (Math.Cos(Math.PI * time / duration) - 1) + begin;
        }

        public static double SineEaseOutIn(double time, double begin, double change, double duration)
        {
            if (time < duration / 2)
            {
                return change / 2 * Math.Sin(time * 2 / duration * (Math.PI / 2)) + begin;
            }
            return -(change / 2) * Math.Cos((time * 2 - duration) / duration * (Math.PI / 2)) + change / 2 + (begin + change / 2);
        }

        public static double QuadraticEaseIn(double time, double begin, double change, double duration)
        {
            time = time / duration;
            return change * time * time + begin;
        }

        public static double QuadraticEaseOut(double time, double begin, double change, double duration)
        {
            time = time / duration;
            return -change * time * (time - 2) + begin;
        }

        public static double QuadraticEaseInOut(double time, double begin, double change, double duration)
        {
            time = time / (duration / 2);
            if (time < 1) return change / 2 * time * time + begin;
            time--;
            return -change / 2 * (time * (time - 2) - 1) + begin;
        }

        public static double QuadraticEaseOutIn(double time, double begin, double change, double duration)
        {
            if (time < duration / 2)
            {
                time = time * 2 / duration;
                return -(change / 2) * time * (time - 2) + begin;
            }
            time = (time * 2 - duration) / duration;
            return change / 2 * time * time + (begin + change / 2);
        }

        public static double CubicEaseIn(double time, double begin, double change, double duration)
        {
            time = time / duration;
            return change * time * time * time + begin;
        }

        public static double CubicEaseOut(double time, double begin, double change, double duration)
        {
            time = time / duration - 1;
            return change * (time * time * time + 1) + begin;
        }

        public static double CubicEaseInOut(double time, double begin, double change, double duration)
        {
            time = time / (duration / 2);
            return time < 1
                ? change / 2 * time * time * time + begin
                : change / 2 * ((time -= 2) * time * time + 2) + begin;
        }

        public static double CubicEaseOutIn(double time, double begin, double change, double duration)
        {
            return time < duration / 2
                ? change / 2 * ((time = time * 2 / duration - 1) * time * time + 1) + begin
                : change / 2 * ((time = (time * 2 - duration) / duration) * time * time) + begin + change / 2;
        }

        public static double QuarticEaseIn(double time, double begin, double change, double duration)
        {
            time = time / duration;
            return change * time * time * time * time + begin;
        }

        public static double QuarticEaseOut(double time, double begin, double change, double duration)
        {
            time = time / duration - 1;
            return -change * (time * time * time * time - 1) + begin;
        }

        public static double QuarticEaseInOut(double time, double begin, double change, double duration)
        {
            time = time / (duration / 2);
            if (time < 1) return change / 2 * time * time * time * time + begin;
            time -= 2;
            return -change / 2 * (time * time * time * time - 2) + begin;
        }

        public static double QuarticEaseOutIn(double time, double begin, double change, double duration)
        {
            if (time < duration / 2)
            {
                time = time * 2 / duration - 1;
                return -(change / 2) * (time * time * time * time - 1) + begin;
            }
            time = (time * 2 - duration) / duration;
            return change / 2 * time * time * time * time + (begin + change / 2);
        }

        public static double QuinticEaseIn(double time, double begin, double change, double duration)
        {
            time = time / duration;
            return change * time * time * time * time * time + begin;
        }

        public static double QuinticEaseOut(double time, double begin, double change, double duration)
        {
            time = time / duration - 1;
            return change * (time * time * time * time * time + 1) + begin;
        }

        public static double QuinticEaseInOut(double time, double begin, double change, double duration)
        {
            time = time / (duration / 2);
            if (time < 1) return change / 2 * time * time * time * time * time + begin;
            time -= 2;
            return change / 2 * (time * time * time * time * time + 2) + begin;
        }

        public static double QuinticEaseOutIn(double time, double begin, double change, double duration)
        {
            if (time < duration / 2)
            {
                time = time * 2 / duration - 1;
                return change / 2 * (time * time * time * time * time + 1) + begin;
            }
            time = (time * 2 - duration) / duration;
            return change / 2 * time * time * time * time * time + (begin + change / 2);
        }

        public static double ExponentialEaseIn(double time, double begin, double change, double duration)
        {
            return time == 0 ? begin : change * Math.Pow(2, 10 * (time / duration - 1)) + begin;
        }

        public static double ExponentialEaseOut(double time, double begin, double change, double duration)
        {
            return time == duration ? begin + change : change * (1 - Math.Pow(2, -10 * time / duration)) + begin;
        }

        public static double ExponentialEaseInOut(double time, double begin, double change, double duration)
        {
            if (time == 0) return begin;
            if (time == duration) return begin + change;
            time = time / (duration / 2);
            if (time < 1) return change / 2 * Math.Pow(2, 10 * (time - 1)) + begin;
            time--;
            return change / 2 * (2 - Math.Pow(2, -10 * time)) + begin;
        }

        public static double ExponentialEaseOutIn(double time, double begin, double change, double duration)
        {
            if (time < duration / 2)
            {
                return time * 2 == duration
                    ? begin + change / 2
                    : change / 2 * (1 - Math.Pow(2, -10 * time * 2 / duration)) + begin;
            }
            return time * 2 - duration == 0
                ? begin + change / 2
                : change / 2 * Math.Pow(2, 10 * ((time * 2 - duration) / duration - 1)) + begin + change / 2;
        }

        public static double CircularEaseIn(double time, double begin, double change, double duration)
        {
            time = time / duration;
            return -change * (Math.Sqrt(1 - time * time) - 1) + begin;
        }

        public static double CircularEaseOut(double time, double begin, double change, double duration)
        {
            time = time / duration - 1;
            return change * Math.Sqrt(1 - time * time) + begin;
        }

        public static double CircularEaseInOut(double time, double begin, double change, double duration)
        {
            time = time / (duration / 2);
            if (time < 1) return -change / 2 * (Math.Sqrt(1 - time * time) - 1) + begin;
            time -= 2;
            return change / 2 * (Math.Sqrt(1 - time * time) + 1) + begin;
        }

        public static double CircularEaseOutIn(double time, double begin, double change, double duration)
        {
            if (time < duration / 2)
            {
                time = time * 2 / duration - 1;
                return change / 2 * Math.Sqrt(1 - time * time) + begin;
            }
            time = (time * 2 - duration) / duration;
            return -(change / 2) * (Math.Sqrt(1 - time * time) - 1) + (begin + change / 2);
        }

        public static double BackEaseIn(double time, double begin, double change, double duration)
        {
            return BackEaseIn(time, begin, change, duration, 1.70158);
        }

        public static double BackEaseIn(double time, double begin, double change, double duration, double overshoot)
        {
            time = time / duration;
            return change * time * time * ((overshoot + 1) * time - overshoot) + begin;
        }

        public static double BackEaseOut(double time, double begin, double change, double duration)
        {
            return BackEaseOut(time, begin, change, duration, 1.70158);
        }

        public static double BackEaseOut(double time, double begin, double change, double duration, double overshoot)
        {
            time = time / duration - 1;
            return change * (time * time * ((overshoot + 1) * time + overshoot) + 1) + begin;
        }

        public static double BackEaseInOut(double time, double begin, double change, double duration)
        {
            return BackEaseInOut(time, begin, change, duration, 1.70158);
        }

        public static double BackEaseInOut(double time, double begin, double change, double duration, double overshoot)
        {
            time = time / (duration / 2);
            if (time < 1) return change / 2 * (time * time * ((overshoot * 1.525 + 1) * time - overshoot * 1.525)) + begin;
            time -= 2;
            return change / 2 * (time * time * ((overshoot * 1.525 + 1) * time + overshoot * 1.525) + 2) + begin;
        }

        public static double BackEaseOutIn(double time, double begin, double change, double duration)
        {
            return BackEaseOutIn(time, begin, change, duration, 1.70158);
        }

        public static double BackEaseOutIn(double time, double begin, double change, double duration, double overshoot)
        {
            if (time < duration / 2)
            {
                time = time * 2 / duration - 1;
                return change / 2 * (time * time * ((overshoot + 1) * time + overshoot) + 1) + begin;
            }
            time = (time * 2 - duration) / duration;
            return change / 2 * time * time * ((overshoot + 1) * time - overshoot) + (begin + change / 2);
        }

        public static double BounceEaseOut(double time, double begin, double change, double duration)
        {
            return begin + change * BounceOut(time / duration);
        }

        public static double BounceEaseIn(double time, double begin, double change, double duration)
        {
            return begin + change * (1 - BounceOut((duration - time) / duration));
        }

        public static double BounceEaseInOut(double time, double begin, double change, double duration)
        {
            if (time < duration / 2) return begin + change / 2 * (1 - BounceOut((duration - time * 2) / duration));
            return begin + change / 2 + change / 2 * BounceOut((time * 2 - duration) / duration);
        }

        public static double BounceEaseOutIn(double time, double begin, double change, double duration)
        {
            if (time < duration / 2) return begin + change / 2 * BounceOut(time * 2 / duration);
            return begin + change / 2 + change / 2 * (1 - BounceOut((duration - (time * 2 - duration)) / duration));
        }

        public static double ElasticEaseIn(double time, double begin, double change, double duration)
        {
            if (time == 0) return begin;
            time = time / duration;
            if (time == 1) return begin + change;
            var period = duration * 0.3;
            var amplitude = change;
            var phase = period / 4;
            return -(amplitude * Math.Pow(2, 10 * (time - 1)) * Math.Sin((time * duration - phase) * (2 * Math.PI) / period)) + begin;
        }

        public static double ElasticEaseOut(double time, double begin, double change, double duration)
        {
            if (time == 0) return begin;
            time = time / duration;
            if (time == 1) return begin + change;
            var period = duration * 0.3;
            var amplitude = change;
            var phase = period / 4;
            return amplitude * Math.Pow(2, -10 * time) * Math.Sin((time * duration - phase) * (2 * Math.PI) / period) + change + begin;
        }

        public static double ElasticEaseInOut(double time, double begin, double change, double duration)
        {
            if (time == 0) return begin;
            time = time / (duration / 2);
            if (time == 2) return begin + change;
            var period = duration * (0.3 * 1.5);
            var amplitude = change;
            var phase = period / 4;
            if (time < 1)
            {
                time--;
                return -0.5 * (amplitude * Math.Pow(2, 10 * time) * Math.Sin((time * duration - phase) * (2 * Math.PI) / period)) + begin;
            }
            time--;
            return amplitude * Math.Pow(2, -10 * time) * Math.Sin((time * duration - phase) * (2 * Math.PI) / period) * 0.5 + change + begin;
        }

        public static double ElasticEaseOutIn(double time, double begin, double change, double duration)
        {
            change /= 2;
            if (time < duration / 2)
            {
                time *= 2;
                if (time == 0) return begin;
                time /= duration;
                if (time == 1) return begin + change;
                var period = duration * 0.3;
                var amplitude = change;
                var phase = period / 4;
                return amplitude * Math.Pow(2, -10 * time) * Math.Sin((time * duration - phase) * (2 * Math.PI) / period) + change + begin;
            }
            time = time * 2 - duration;
            if (time == 0) return begin + change;
            time /= duration;
            if (time == 1) return begin + change + change;
            var period2 = duration * 0.3;
            var amplitude2 = change;
            var phase2 = period2 / 4;
            time--;
            return -(amplitude2 * Math.Pow(2, 10 * time) * Math.Sin((time * duration - phase2) * (2 * Math.PI) / period2)) + (begin + change);
        }

        public static double PhysicalUniform(double time, double begin, double change, double velocity = 10, double fps = 30)
        {
            return begin + (change < 0 ? -velocity : velocity) * (time / (1 / fps));
        }

        public static double PhysicalUniformDuration(double distance, double velocity = 10, double fps = 30)
        {
            return distance / (distance < 0 ? -velocity : velocity) * (1 / fps);
        }

        public static double PhysicalExponential(double time, double begin, double change, double friction = 0.2, double threshold = 0.0001, double fps = 30)
        {
            return -change * Math.Pow(1 - friction, time / (1 / fps) - 1) + (begin + change);
        }

        public static double PhysicalExponentialDuration(double distance, double threshold = 0.0001, double friction = 0.2, double fps = 30)
        {
            return (Math.Log(threshold / distance) / Math.Log(1 - friction) + 1) * (1 / fps);
        }

        public static double PhysicalAccelerate(double time, double begin, double change, double acceleration = 1, double initialVelocity = 0, double fps = 30)
        {
            var sign = change < 0 ? -1 : 1;
            var frames = time / (1 / fps);
            return begin + sign * initialVelocity * frames + sign * acceleration * frames * frames / 2;
        }

        public static double PhysicalAccelerateDuration(double distance, double acceleration = 1, double initialVelocity = 0, double fps = 30)
        {
            var velocity = distance < 0 ? -initialVelocity : initialVelocity;
            var force = distance < 0 ? -acceleration : acceleration;
            return (-velocity + Math.Sqrt(velocity * velocity - 4 * (force / 2) * -distance)) /
                (2 * (force / 2)) * (1 / fps);
        }

        private static double BounceOut(double time)
        {
            if (time < 1 / 2.75) return 7.5625 * time * time;
            if (time < 2 / 2.75)
            {
                time -= 1.5 / 2.75;
                return 7.5625 * time * time + 0.75;
            }
            if (time < 2.5 / 2.75)
            {
                time -= 2.25 / 2.75;
                return 7.5625 * time * time + 0.9375;
            }
            time -= 2.625 / 2.75;
            return 7.5625 * time * time + 0.984375;
        }

        private static Dictionary<string, EasingFunction> CreateTable()
        {
            var table = new Dictionary<string, EasingFunction>(StringComparer.Ordinal)
            {
                ["EaseNone"] = EaseNone,
                ["Linear"] = EaseNone,
                ["LinearEaseNone"] = EaseNone,
                ["LinearEaseIn"] = EaseNone,
                ["LinearEaseOut"] = EaseNone,
                ["LinearEaseInOut"] = EaseNone,
                ["LinearEaseOutIn"] = EaseNone,
                ["SineEaseIn"] = SineEaseIn,
                ["SineEaseOut"] = SineEaseOut,
                ["SineEaseInOut"] = SineEaseInOut,
                ["SineEaseOutIn"] = SineEaseOutIn,
                ["QuadraticEaseIn"] = QuadraticEaseIn,
                ["QuadraticEaseOut"] = QuadraticEaseOut,
                ["QuadraticEaseInOut"] = QuadraticEaseInOut,
                ["QuadraticEaseOutIn"] = QuadraticEaseOutIn,
                ["CubicEaseIn"] = CubicEaseIn,
                ["CubicEaseOut"] = CubicEaseOut,
                ["CubicEaseInOut"] = CubicEaseInOut,
                ["CubicEaseOutIn"] = CubicEaseOutIn,
                ["QuarticEaseIn"] = QuarticEaseIn,
                ["QuarticEaseOut"] = QuarticEaseOut,
                ["QuarticEaseInOut"] = QuarticEaseInOut,
                ["QuarticEaseOutIn"] = QuarticEaseOutIn,
                ["QuinticEaseIn"] = QuinticEaseIn,
                ["QuinticEaseOut"] = QuinticEaseOut,
                ["QuinticEaseInOut"] = QuinticEaseInOut,
                ["QuinticEaseOutIn"] = QuinticEaseOutIn,
                ["ExponentialEaseIn"] = ExponentialEaseIn,
                ["ExponentialEaseOut"] = ExponentialEaseOut,
                ["ExponentialEaseInOut"] = ExponentialEaseInOut,
                ["ExponentialEaseOutIn"] = ExponentialEaseOutIn,
                ["CircularEaseIn"] = CircularEaseIn,
                ["CircularEaseOut"] = CircularEaseOut,
                ["CircularEaseInOut"] = CircularEaseInOut,
                ["CircularEaseOutIn"] = CircularEaseOutIn,
                ["BackEaseIn"] = BackEaseIn,
                ["BackEaseOut"] = BackEaseOut,
                ["BackEaseInOut"] = BackEaseInOut,
                ["BackEaseOutIn"] = BackEaseOutIn,
                ["BounceEaseIn"] = BounceEaseIn,
                ["BounceEaseOut"] = BounceEaseOut,
                ["BounceEaseInOut"] = BounceEaseInOut,
                ["BounceEaseOutIn"] = BounceEaseOutIn,
                ["ElasticEaseIn"] = ElasticEaseIn,
                ["ElasticEaseOut"] = ElasticEaseOut,
                ["ElasticEaseInOut"] = ElasticEaseInOut,
                ["ElasticEaseOutIn"] = ElasticEaseOutIn,
                ["CustomFunctionEasing"] = EaseNone,
                ["PhysicalUniform"] = (time, begin, change, duration) => PhysicalUniform(time, begin, change),
                ["PhysicalExponential"] = (time, begin, change, duration) => PhysicalExponential(time, begin, change),
                ["PhysicalAccelerate"] = (time, begin, change, duration) => PhysicalAccelerate(time, begin, change)
            };

            table["None"] = EaseNone;
            table["Sine"] = SineEaseInOut;
            table["Quadratic"] = QuadraticEaseInOut;
            table["Cubic"] = CubicEaseInOut;
            table["Quartic"] = QuarticEaseInOut;
            table["Quintic"] = QuinticEaseInOut;
            table["Exponential"] = ExponentialEaseInOut;
            table["Circular"] = CircularEaseInOut;
            table["Back"] = BackEaseInOut;
            table["Bounce"] = BounceEaseInOut;
            table["Elastic"] = ElasticEaseInOut;
            return table;
        }
    }
}
