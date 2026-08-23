using System;
using System.Collections.Generic;
using System.Linq;

namespace BiliBili.UWP.Modules.Playback
{
    public sealed class PlaybackEventTimeline<T> where T : class
    {
        private const double PositionEpsilon = 0.000001;
        private const double SeekThresholdSeconds = 5;

        private readonly List<T> items;
        private readonly Func<T, double> getTime;
        private int nextIndex;
        private double lastPosition;
        private bool hasPosition;

        public PlaybackEventTimeline(IEnumerable<T> items, Func<T, double> getTime)
        {
            this.getTime = getTime ?? throw new ArgumentNullException(nameof(getTime));
            this.items = (items ?? Enumerable.Empty<T>())
                .Where(item => item != null)
                .OrderBy(getTime)
                .ToList();
        }

        public PlaybackEventBatch<T> Advance(double position)
        {
            if (double.IsNaN(position) || double.IsInfinity(position))
            {
                return new PlaybackEventBatch<T>(new List<T>(), false);
            }

            if (!hasPosition)
            {
                Reset(position, true);
                return new PlaybackEventBatch<T>(CollectThrough(position), true);
            }

            var delta = position - lastPosition;
            if (delta < -PositionEpsilon || delta > SeekThresholdSeconds)
            {
                Reset(position, true);
                return new PlaybackEventBatch<T>(CollectThrough(position), true);
            }

            if (delta <= PositionEpsilon)
            {
                lastPosition = position;
                return new PlaybackEventBatch<T>(new List<T>(), false);
            }

            var result = CollectThrough(position);
            lastPosition = position;
            return new PlaybackEventBatch<T>(result, false);
        }

        public void Reset(double position, bool includeCurrentPosition)
        {
            if (double.IsNaN(position) || double.IsInfinity(position))
            {
                hasPosition = false;
                nextIndex = 0;
                return;
            }

            nextIndex = LowerBound(position, includeCurrentPosition);
            lastPosition = position;
            hasPosition = true;
        }

        private List<T> CollectThrough(double position)
        {
            var result = new List<T>();
            while (nextIndex < items.Count
                && getTime(items[nextIndex]) <= position + PositionEpsilon)
            {
                result.Add(items[nextIndex]);
                nextIndex++;
            }

            return result;
        }

        private int LowerBound(double position, bool includeCurrentPosition)
        {
            var low = 0;
            var high = items.Count;
            while (low < high)
            {
                var middle = low + ((high - low) / 2);
                var itemTime = getTime(items[middle]);
                var isBefore = includeCurrentPosition
                    ? itemTime < position - PositionEpsilon
                    : itemTime <= position + PositionEpsilon;
                if (isBefore)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }
    }

    public sealed class PlaybackEventBatch<T> where T : class
    {
        public PlaybackEventBatch(IReadOnlyList<T> items, bool wasDiscontinuity)
        {
            Items = items ?? new List<T>();
            WasDiscontinuity = wasDiscontinuity;
        }

        public IReadOnlyList<T> Items { get; }
        public bool WasDiscontinuity { get; }
    }
}
