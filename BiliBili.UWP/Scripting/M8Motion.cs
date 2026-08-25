using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace scripting
{
    /// <summary>AS3 MotionManager port for a pure M8 element.</summary>
    public sealed class M8Motion
    {
        private sealed class MotionTrack
        {
            public string Key;
            public object From;
            public object To;
            public bool HasFrom;
            public bool HasTo;
            public bool FromRelative;
            public bool ToRelative;
            public bool FromParentRelative;
            public bool ToParentRelative;
            public double DurationMs;
            public double DelayMs;
            public int Repeat;
            public M8Easing.EasingFunction Easing;
            public bool Resolved;
            public bool Numeric;
            public double FromNumber;
            public double ToNumber;
        }

        private sealed class MotionSegment
        {
            public readonly List<MotionTrack> Tracks = new List<MotionTrack>();
            public double LifeTimeMs;
            public double DurationMs;

            public void CalculateDuration()
            {
                this.DurationMs = this.LifeTimeMs;
                if (this.DurationMs > 0) return;
                foreach (var track in this.Tracks)
                {
                    var trackDuration = track.DelayMs + track.DurationMs * Math.Max(1, track.Repeat);
                    if (trackDuration > this.DurationMs) this.DurationMs = trackDuration;
                }
                if (this.DurationMs < 0) this.DurationMs = 0;
            }
        }

        private static readonly string[] _acceptValue =
        {
            "x", "y", "alpha", "rotationZ", "rotationY", "rotationX", "fontsize"
        };

        private readonly IM8ScriptObject _target;
        private readonly List<MotionSegment> _segments = new List<MotionSegment>();
        private double _motionPlayTime;
        private double _elapsed;
        private double _totalDuration;
        private bool _running;
        private bool _motionComplete;
        private Action _complete;
        private Action<object> _completeObject;
        private Action<M8Motion> _completeMotion;
        private object _motionConfig;

        public M8Motion(M8Element target)
            : this((IM8ScriptObject)target)
        {
        }

        public M8Motion(IM8ScriptObject target)
        {
            this._target = target;
        }

        public static string[] AcceptValue
        {
            get { return _acceptValue; }
        }

        public static string[] acceptValue
        {
            get { return _acceptValue; }
        }

        public IM8ScriptObject Target
        {
            get { return this._target; }
        }

        public bool Running
        {
            get { return this._running; }
        }

        public bool running
        {
            get { return this._running; }
        }

        public double Position
        {
            get { return this._elapsed; }
        }

        public double position
        {
            get { return this._elapsed; }
        }

        public double Duration
        {
            get { return this._totalDuration; }
        }

        public double duration
        {
            get { return this._totalDuration; }
        }

        public double PlayTime
        {
            get { return this._motionPlayTime; }
        }

        public object MotionConfig
        {
            get { return this._motionConfig; }
        }

        public Action<M8Motion> OnCompleteWithMotion
        {
            get { return this._completeMotion; }
            set { this._completeMotion = value; }
        }

        public void SetCompleteListener(Action listener)
        {
            this._complete = listener;
        }

        public void setCompleteListener(Action listener)
        {
            this.SetCompleteListener(listener);
        }

        public void SetCompleteListener(Action<object> listener)
        {
            this._completeObject = listener;
        }

        public void setCompleteListener(Action<object> listener)
        {
            this.SetCompleteListener(listener);
        }

        public void SetPlayTime(double milliseconds)
        {
            this._motionPlayTime = milliseconds;
        }

        public void setPlayTime(double milliseconds)
        {
            this.SetPlayTime(milliseconds);
        }

        public void Play()
        {
            if (this._running) return;
            this._running = true;
            if (this._motionComplete)
            {
                this._elapsed = 0;
                this._motionComplete = false;
                this.ResetTrackState();
            }
            this.ApplyAt(this._elapsed);
            if (this._totalDuration <= 0) this.CompleteMotion();
        }

        public void play()
        {
            this.Play();
        }

        public void Stop()
        {
            this._running = false;
        }

        public void stop()
        {
            this.Stop();
        }

        public void Reset()
        {
            this.Stop();
            this._elapsed = 0;
            this._motionComplete = false;
            this.ResetTrackState();
            this.ApplyAt(0);
        }

        public void reset()
        {
            this.Reset();
        }

        public bool Forcasting(double milliseconds)
        {
            if (this._totalDuration <= 0 && milliseconds > this._motionPlayTime) return true;
            if (this._totalDuration <= 0) return false;
            return milliseconds > this._motionPlayTime &&
                milliseconds < this._motionPlayTime + this._totalDuration;
        }

        public bool forcasting(double milliseconds)
        {
            return this.Forcasting(milliseconds);
        }

        /// <summary>Advances this manager by a host-provided delta in milliseconds.</summary>
        public void Step(double milliseconds)
        {
            if (!this._running) return;
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0) return;
            this._elapsed += milliseconds;
            if (this._elapsed >= this._totalDuration)
            {
                this._elapsed = this._totalDuration;
                this.ApplyAt(this._elapsed);
                this.CompleteMotion();
                return;
            }
            this.ApplyAt(this._elapsed);
        }

        public string InitTween(object motionConfig, bool motionGroup = false)
        {
            if (!motionGroup) this._segments.Clear();
            var segment = this.CreateSegment(motionConfig, double.NaN);
            if (segment.Error != null) return segment.Error;
            this._segments.Add(segment.Value);
            this._motionConfig = motionConfig;
            this.RecalculateDuration();
            this._elapsed = 0;
            this._motionComplete = false;
            this.ResetTrackState();
            if (!this.HasRelativeMotion()) this.ApplyAt(0);
            return string.Empty;
        }

        public string initTween(object motionConfig, bool motionGroup = false)
        {
            return this.InitTween(motionConfig, motionGroup);
        }

        public void InitTweenGroup(object group, double lifeTime = double.NaN)
        {
            this._segments.Clear();
            this._motionConfig = group;
            if (group is IEnumerable enumerable && !(group is string))
            {
                foreach (var item in enumerable)
                {
                    var result = this.CreateSegment(item, lifeTime);
                    if (result.Error != null) throw new InvalidOperationException(result.Error);
                    this._segments.Add(result.Value);
                }
            }
            this.RecalculateDuration();
            this._elapsed = 0;
            this._motionComplete = false;
            this.ResetTrackState();
            if (!this.HasRelativeMotion()) this.ApplyAt(0);
        }

        public void initTweenGroup(object group, double lifeTime = double.NaN)
        {
            this.InitTweenGroup(group, lifeTime);
        }

        private void RecalculateDuration()
        {
            this._totalDuration = 0;
            foreach (var segment in this._segments)
            {
                segment.CalculateDuration();
                this._totalDuration += segment.DurationMs;
            }
        }

        private void CompleteMotion()
        {
            if (this._motionComplete) return;
            this._running = false;
            this._motionComplete = true;
            if (this._complete != null) this._complete();
            if (this._completeObject != null) this._completeObject(null);
            if (this._completeMotion != null) this._completeMotion(this);
        }

        private void ApplyAt(double milliseconds)
        {
            if (this._segments.Count == 0) return;
            var start = 0d;
            foreach (var segment in this._segments)
            {
                if (milliseconds < start) break;
                var local = milliseconds - start;
                if (local > segment.DurationMs) local = segment.DurationMs;
                this.ApplySegment(segment, local);
                start += segment.DurationMs;
            }
        }

        private void ApplySegment(MotionSegment segment, double milliseconds)
        {
            foreach (var track in segment.Tracks)
            {
                if (!track.Resolved) ResolveTrack(track);
                var local = milliseconds - track.DelayMs;
                var duration = track.DurationMs;
                var repeat = Math.Max(1, track.Repeat);
                var total = duration * repeat;
                var progress = 0d;
                if (local >= total)
                {
                    progress = 1;
                }
                else if (local > 0 && duration > 0)
                {
                    var cyclePosition = local < total ? local - duration * Math.Floor(local / duration) : duration;
                    progress = cyclePosition >= duration ? 1 :
                        cyclePosition <= 0 ? 0 : track.Easing(cyclePosition, 0, 1, duration);
                }

                if (track.Numeric)
                {
                    this._target.Set(track.Key, track.FromNumber * (1 - progress) + track.ToNumber * progress);
                }
                else if (local <= 0)
                {
                    if (track.HasFrom) this._target.Set(track.Key, track.From);
                }
                else if (local >= total && track.HasTo)
                {
                    this._target.Set(track.Key, track.To);
                }
            }
        }

        private void ResolveTrack(MotionTrack track)
        {
            var current = this._target.Get(track.Key);
            var currentNumber = VirtualMachine.ToNumber(current);
            track.Numeric = IsNumber(track.HasFrom ? track.From : current) ||
                IsNumber(track.HasTo ? track.To : current);
            if (track.Numeric)
            {
                track.FromNumber = track.HasFrom ? VirtualMachine.ToNumber(track.From) : currentNumber;
                track.ToNumber = track.HasTo ? VirtualMachine.ToNumber(track.To) : currentNumber;
                if (track.FromRelative) track.FromNumber += currentNumber;
                if (track.ToRelative) track.ToNumber += currentNumber;
                if (track.FromParentRelative) track.FromNumber = ResolveParentRelativeNumber(track.Key, track.FromNumber);
                if (track.ToParentRelative) track.ToNumber = ResolveParentRelativeNumber(track.Key, track.ToNumber);
            }
            track.Resolved = true;
        }

        private void ResetTrackState()
        {
            foreach (var segment in this._segments)
            {
                foreach (var track in segment.Tracks) track.Resolved = false;
            }
        }

        private bool HasRelativeMotion()
        {
            foreach (var segment in this._segments)
            {
                foreach (var track in segment.Tracks)
                {
                    if (track.FromRelative || track.ToRelative || track.FromParentRelative || track.ToParentRelative) return true;
                }
            }
            return false;
        }

        private struct SegmentResult
        {
            public MotionSegment Value;
            public string Error;
        }

        private SegmentResult CreateSegment(object config, double overrideLifeTime)
        {
            var segment = new MotionSegment();
            var outerLifeTime = GetNumber(config, "lifeTime");
            if (double.IsNaN(outerLifeTime)) outerLifeTime = 3;
            if (!double.IsNaN(overrideLifeTime)) outerLifeTime = overrideLifeTime;
            segment.LifeTimeMs = ConfigMilliseconds(config, "lifeTimeMs", outerLifeTime);

            var motion = GetMember(config, "motion");
            var from = motion == null ? GetMember(config, "from") : GetMember(motion, "from");
            var to = motion == null ? GetMember(config, "to") : GetMember(motion, "to");
            var hasShape = from != null || to != null;
            if (hasShape)
            {
                foreach (var key in _acceptValue)
                {
                    var hasFrom = TryGetMember(from, key, out var fromValue);
                    var hasTo = TryGetMember(to, key, out var toValue);
                    if (!hasFrom && !hasTo) continue;
                    segment.Tracks.Add(CreateShapeTrack(config, key, fromValue, hasFrom, toValue, hasTo, outerLifeTime));
                }
            }
            else
            {
                foreach (var key in _acceptValue)
                {
                    if (!TryGetMember(config, key, out var propertyConfig) || propertyConfig == null) continue;
                    var hasFrom = TryGetMember(propertyConfig, "fromValue", out var fromValue);
                    var hasTo = TryGetMember(propertyConfig, "toValue", out var toValue);
                    if (!hasFrom)
                    {
                        return new SegmentResult
                        {
                            Error = "Motion " + key + " error: no transform"
                        };
                    }
                    if (!hasTo)
                    {
                        toValue = fromValue;
                        hasTo = true;
                    }
                    var propertyLifeTime = GetNumber(propertyConfig, "lifeTime");
                    if (double.IsNaN(propertyLifeTime) || propertyLifeTime == 0) propertyLifeTime = outerLifeTime;
                    segment.Tracks.Add(CreatePropertyTrack(config, propertyConfig, key, fromValue, toValue, propertyLifeTime));
                }
            }
            segment.CalculateDuration();
            return new SegmentResult { Value = segment };
        }

        private MotionTrack CreateShapeTrack(object config, string key, object from, bool hasFrom, object to, bool hasTo, double lifeTime)
        {
            var fromValue = ResolveRelativeValue(key, from, out var fromRelative, out var fromParentRelative);
            var toValue = ResolveRelativeValue(key, to, out var toRelative, out var toParentRelative);
            return new MotionTrack
            {
                Key = key,
                From = fromValue,
                To = toValue,
                HasFrom = hasFrom,
                HasTo = hasTo,
                FromRelative = hasFrom && fromRelative,
                ToRelative = hasTo && toRelative,
                FromParentRelative = hasFrom && fromParentRelative,
                ToParentRelative = hasTo && toParentRelative,
                DurationMs = ConfigMilliseconds(config, "lifeTimeMs", lifeTime),
                DelayMs = ConfigDelayMilliseconds(config),
                Repeat = ReadRepeat(config),
                Easing = ReadEasing(config)
            };
        }

        private MotionTrack CreatePropertyTrack(object outerConfig, object config, string key, object from, object to, double lifeTime)
        {
            var fromValue = ResolveRelativeValue(key, from, out var fromRelative, out var fromParentRelative);
            var toValue = ResolveRelativeValue(key, to, out var toRelative, out var toParentRelative);
            return new MotionTrack
            {
                Key = key,
                From = fromValue,
                To = toValue,
                HasFrom = true,
                HasTo = true,
                FromRelative = fromRelative,
                ToRelative = toRelative,
                FromParentRelative = fromParentRelative,
                ToParentRelative = toParentRelative,
                DurationMs = ConfigMilliseconds(config, "lifeTimeMs", lifeTime),
                DelayMs = ConfigDelayMilliseconds(config),
                Repeat = ReadRepeat(config),
                Easing = ReadEasing(config)
            };
        }

        private object ResolveRelativeValue(string key, object value, out bool relative, out bool parentRelative)
        {
            relative = false;
            parentRelative = false;
            var number = VirtualMachine.ToNumber(value);
            if ((key == "x" || key == "y") && !double.IsNaN(number) && number > 0 && number < 1)
            {
                parentRelative = true;
            }
            return value;
        }

        private double ResolveParentRelativeNumber(string key, double value)
        {
            var parent = this._target.Get("parent");
            var width = ReadMember(parent, "width");
            var parentWidth = VirtualMachine.ToNumber(width);
            return double.IsNaN(parentWidth) ? value : value * parentWidth;
        }

        private static int ReadRepeat(object config)
        {
            var value = GetNumber(config, "repeat");
            if (double.IsNaN(value) || value <= 0) return 1;
            return (int)value;
        }

        private static M8Easing.EasingFunction ReadEasing(object config)
        {
            var value = GetMember(config, "easing");
            if (value == null) return M8Easing.EaseNone;
            if (value is M8Easing.EasingFunction function) return function;
            if (value is Func<double, double, double, double, double> func)
            {
                return (time, begin, change, duration) => func(time, begin, change, duration);
            }
            return M8Easing.Get(value as string);
        }

        private static double ConfigDelayMilliseconds(object config)
        {
            var milliseconds = GetNumber(config, "startDelayMs");
            if (!double.IsNaN(milliseconds)) return Math.Max(0, milliseconds);
            var seconds = GetNumber(config, "startDelay");
            if (double.IsNaN(seconds) || seconds <= 0) return 0;
            return seconds * 1000;
        }

        private static double ConfigMilliseconds(object config, string millisecondsKey, double seconds)
        {
            var milliseconds = GetNumber(config, millisecondsKey);
            if (!double.IsNaN(milliseconds)) return Math.Max(0, milliseconds);
            return Math.Max(0, seconds * 1000);
        }

        private static bool IsNumber(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong ||
                value is float || value is double || value is decimal;
        }

        private static double GetNumber(object value, string key)
        {
            if (!TryGetMember(value, key, out var member) || member == null) return double.NaN;
            return VirtualMachine.ToNumber(member);
        }

        private static object ReadMember(object value, string key)
        {
            return TryGetMember(value, key, out var result) ? result : null;
        }

        private static object GetMember(object value, string key)
        {
            return ReadMember(value, key);
        }

        private static bool TryGetMember(object value, string key, out object result)
        {
            result = null;
            if (value == null) return false;
            if (value is IM8ScriptObject scriptObject)
            {
                result = scriptObject.Get(key);
                return result != null;
            }
            if (value is IDictionary<string, object> generic)
            {
                return generic.TryGetValue(key, out result);
            }
            if (value is IDictionary dictionary)
            {
                if (!dictionary.Contains(key)) return false;
                result = dictionary[key];
                return true;
            }
            return false;
        }
    }
}
