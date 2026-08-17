using System;
using System.Collections.Generic;
using System.Linq;

namespace BiliBili.UWP.Modules.Playback
{
    public sealed class PlaybackTimelineIndex<T> where T : class
    {
        private readonly List<T> items;
        private readonly Func<T, double> getStart;
        private readonly Func<T, double> getEnd;
        private int index;

        public PlaybackTimelineIndex(IEnumerable<T> items, Func<T, double> getStart, Func<T, double> getEnd)
        {
            this.getStart = getStart ?? throw new ArgumentNullException(nameof(getStart));
            this.getEnd = getEnd ?? throw new ArgumentNullException(nameof(getEnd));
            this.items = items?.OrderBy(getStart).ToList() ?? new List<T>();
        }

        public T Find(double time)
        {
            if (items.Count == 0)
            {
                return null;
            }

            while (index > 0 && getStart(items[index]) > time)
            {
                index--;
            }
            while (index < items.Count - 1 && getEnd(items[index]) < time)
            {
                index++;
            }

            var current = items[index];
            return getStart(current) <= time && getEnd(current) >= time ? current : null;
        }
    }
}
