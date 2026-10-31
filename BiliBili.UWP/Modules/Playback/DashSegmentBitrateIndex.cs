using System;
using System.Globalization;

namespace BiliBili.UWP.Modules.Playback
{
    // SIDX 的大小包括媒体分段容器开销，因此结果是分段平均码率，不是逐帧编码码率。
    public sealed class DashSegmentBitrateIndex
    {
        public const int MaximumIndexBytes = 1024 * 1024;
        private readonly double[] starts;
        private readonly double[] ends;
        private readonly double[] bitrates;

        private DashSegmentBitrateIndex(double[] starts, double[] ends, double[] bitrates)
        {
            this.starts = starts;
            this.ends = ends;
            this.bitrates = bitrates;
        }

        public double? FindBitrate(double positionSeconds)
        {
            if (double.IsNaN(positionSeconds) || double.IsInfinity(positionSeconds)) return null;
            int low = 0, high = starts.Length - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                if (starts[middle] <= positionSeconds) low = middle + 1;
                else high = middle - 1;
            }
            return high >= 0 && positionSeconds < ends[high] ? (double?)bitrates[high] : null;
        }

        public static bool TryParseRange(string range, out long offset, out int length)
        {
            offset = 0;
            length = 0;
            var parts = range?.Split('-');
            if (parts?.Length != 2
                || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var start)
                || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var end)
                || start < 0 || end < start || end - start >= MaximumIndexBytes) return false;
            offset = start;
            length = (int)(end - start + 1);
            return true;
        }

        public static bool TryParse(byte[] data, out DashSegmentBitrateIndex index)
        {
            index = null;
            if (data == null || data.Length > MaximumIndexBytes) return false;
            int offset = 0;
            while (data.Length - offset >= 8)
            {
                ulong size = Read32(data, offset);
                int headerSize = 8;
                if (size == 1)
                {
                    if (data.Length - offset < 16) return false;
                    size = Read64(data, offset + 8);
                    headerSize = 16;
                }
                else if (size == 0) size = (ulong)(data.Length - offset);
                if (size < (ulong)headerSize || size > (ulong)(data.Length - offset)) return false;
                int end = offset + (int)size;
                if (Read32(data, offset + 4) == 0x73696478) // sidx
                    return TryParseSidx(data, offset + headerSize, end, out index);
                offset = end;
            }
            return false;
        }

        private static bool TryParseSidx(byte[] data, int offset, int end, out DashSegmentBitrateIndex index)
        {
            index = null;
            if (end - offset < 24) return false;
            int version = data[offset];
            if (version > 1) return false;
            uint timescale = Read32(data, offset + 8);
            if (timescale == 0) return false;
            int countOffset = offset + (version == 0 ? 20 : 28);
            if (end - countOffset < 4) return false;
            ulong earliest = version == 0 ? Read32(data, offset + 12) : Read64(data, offset + 12);
            int count = data[countOffset + 2] * 256 + data[countOffset + 3];
            int entry = countOffset + 4;
            if (count == 0 || count > (end - entry) / 12) return false;
            var starts = new double[count];
            var ends = new double[count];
            var bitrates = new double[count];
            double position = earliest / (double)timescale;
            for (int i = 0; i < count; i++, entry += 12)
            {
                uint reference = Read32(data, entry);
                uint duration = Read32(data, entry + 4);
                // 不将嵌套索引或空分段误当成媒体数据。
                if ((reference & 0x80000000) != 0 || reference == 0 || duration == 0) return false;
                double seconds = duration / (double)timescale;
                double next = position + seconds;
                if (next <= position || double.IsInfinity(next)) return false;
                starts[i] = position;
                ends[i] = next;
                bitrates[i] = reference * 8.0 / seconds;
                position = next;
            }
            index = new DashSegmentBitrateIndex(starts, ends, bitrates);
            return true;
        }

        private static uint Read32(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
                | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private static ulong Read64(byte[] data, int offset)
        {
            return ((ulong)Read32(data, offset) << 32) | Read32(data, offset + 4);
        }
    }
}
