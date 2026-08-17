using System.Threading;

namespace BiliBili.UWP.Modules.Playback
{
    public sealed class PlaybackRequestGate
    {
        private int generation;

        public int Current => Volatile.Read(ref generation);

        public int Begin()
        {
            return Interlocked.Increment(ref generation);
        }

        public int Invalidate()
        {
            return Interlocked.Increment(ref generation);
        }

        public bool IsCurrent(int requestGeneration)
        {
            return Current == requestGeneration;
        }
    }
}
