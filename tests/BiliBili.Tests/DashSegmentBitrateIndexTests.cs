using System;
using System.IO;
using BiliBili.UWP.Modules.Playback;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BiliBili.Tests
{
    [TestClass]
    public class DashSegmentBitrateIndexTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BitrateFollowsPlaybackPositionIncludingBackwardSeek(bool versionOne)
        {
            var data = BuildIndex(versionOne, 1000, 0, new uint[] { 1000000, 3000000, 500000 }, new uint[] { 2000, 2000, 1000 });
            Assert.IsTrue(DashSegmentBitrateIndex.TryParse(data, out var index));
            Assert.AreEqual(4000000d, index.FindBitrate(0));
            Assert.AreEqual(4000000d, index.FindBitrate(1.99));
            Assert.AreEqual(12000000d, index.FindBitrate(2));
            Assert.AreEqual(4000000d, index.FindBitrate(4));
            Assert.AreEqual(4000000d, index.FindBitrate(0.5));
            Assert.IsNull(index.FindBitrate(-0.1));
            Assert.IsNull(index.FindBitrate(5));
            Assert.IsNull(index.FindBitrate(double.NaN));
            Assert.IsNull(index.FindBitrate(double.PositiveInfinity));
        }

        [TestMethod]
        public void VersionOneKeeps64BitPresentationTimeAndFirstOffset()
        {
            var data = BuildIndex(true, 1000, 5000000000, new uint[] { 1000 }, new uint[] { 2000 });
            Assert.IsTrue(DashSegmentBitrateIndex.TryParse(data, out var index));
            Assert.IsNull(index.FindBitrate(0));
            Assert.AreEqual(4000d, index.FindBitrate(5000000));
            Assert.IsNull(index.FindBitrate(5000002));
        }

        [TestMethod]
        public void SkipsOtherBoxesAndSupportsExtendedBoxSize()
        {
            var data = BuildIndex(false, 1000, 0, new uint[] { 1000 }, new uint[] { 2000 });
            using (var stream = new MemoryStream())
            {
                Write32(stream, 8);
                Write32(stream, 0x66726565); // free
                Write32(stream, 1);
                Write32(stream, 0x73696478);
                Write64(stream, (ulong)data.Length + 8);
                stream.Write(data, 8, data.Length - 8);
                Assert.IsTrue(DashSegmentBitrateIndex.TryParse(stream.ToArray(), out var index));
                Assert.AreEqual(4000d, index.FindBitrate(1));
            }
        }

        [TestMethod]
        public void RejectsTruncatedAndMalformedDataWithoutPartialIndex()
        {
            var data = BuildIndex(false, 1000, 0, new uint[] { 1000 }, new uint[] { 2000 });
            for (int length = 0; length < data.Length; length++)
            {
                var truncated = new byte[length];
                Array.Copy(data, truncated, length);
                Assert.IsFalse(DashSegmentBitrateIndex.TryParse(truncated, out var index));
                Assert.IsNull(index);
            }
            foreach (var invalid in new[]
            {
                BuildIndex(false, 0, 0, new uint[] { 1000 }, new uint[] { 2000 }),
                BuildIndex(false, 1000, 0, new uint[] { 1000 }, new uint[] { 0 }),
                BuildIndex(false, 1000, 0, new uint[] { 0 }, new uint[] { 2000 }),
                BuildIndex(false, 1000, 0, new uint[] { 0x800003e8 }, new uint[] { 2000 }),
                BuildIndex(false, 1000, 0, new uint[0], new uint[0]),
                new byte[DashSegmentBitrateIndex.MaximumIndexBytes + 1]
            })
            {
                Assert.IsFalse(DashSegmentBitrateIndex.TryParse(invalid, out var index));
                Assert.IsNull(index);
            }
            data[8] = 2;
            Assert.IsFalse(DashSegmentBitrateIndex.TryParse(data, out _));
        }

        [TestMethod]
        public void AudioAndVideoKeepIndependentMediaTimelines()
        {
            Assert.IsTrue(DashSegmentBitrateIndex.TryParse(BuildIndex(false, 1000, 0,
                new uint[] { 1000000, 2000000 }, new uint[] { 2000, 2000 }), out var video));
            Assert.IsTrue(DashSegmentBitrateIndex.TryParse(BuildIndex(false, 48000, 0,
                new uint[] { 16000, 24000 }, new uint[] { 48000, 144000 }), out var audio));
            Assert.AreEqual(4000000d, video.FindBitrate(1.5));
            Assert.AreEqual(64000d, audio.FindBitrate(1.5));
            Assert.AreEqual(8000000d, video.FindBitrate(2.5));
            Assert.AreEqual(64000d, audio.FindBitrate(2.5));
        }

        [DataTestMethod]
        [DataRow("100-199", true, 100L, 100)]
        [DataRow("0-1048575", true, 0L, 1048576)]
        [DataRow("9223372036854775807-9223372036854775807", true, long.MaxValue, 1)]
        [DataRow("0-1048576", false, 0L, 0)]
        [DataRow("200-100", false, 0L, 0)]
        [DataRow("-1-100", false, 0L, 0)]
        [DataRow("0-9223372036854775807", false, 0L, 0)]
        [DataRow("0-9223372036854775808", false, 0L, 0)]
        [DataRow("bytes=0-10", false, 0L, 0)]
        [DataRow("0-10-20", false, 0L, 0)]
        [DataRow(null, false, 0L, 0)]
        public void RangeParsingIsBounded(string range, bool valid, long offset, int length)
        {
            Assert.AreEqual(valid, DashSegmentBitrateIndex.TryParseRange(range, out var actualOffset, out var actualLength));
            Assert.AreEqual(offset, actualOffset);
            Assert.AreEqual(length, actualLength);
        }

        private static byte[] BuildIndex(bool versionOne, uint timescale, ulong earliest, uint[] sizes, uint[] durations)
        {
            using (var stream = new MemoryStream())
            {
                Write32(stream, (uint)((versionOne ? 40 : 32) + sizes.Length * 12));
                Write32(stream, 0x73696478);
                Write32(stream, versionOne ? 0x01000000U : 0);
                Write32(stream, 1); // reference_ID
                Write32(stream, timescale);
                if (versionOne)
                {
                    Write64(stream, earliest);
                    Write64(stream, 0x100000000); // first_offset 不参与码率计算
                }
                else
                {
                    Write32(stream, (uint)earliest);
                    Write32(stream, 0);
                }
                Write32(stream, (uint)sizes.Length); // reserved + reference_count
                for (int i = 0; i < sizes.Length; i++)
                {
                    Write32(stream, sizes[i]);
                    Write32(stream, durations[i]);
                    Write32(stream, 0x90000000); // SAP flags
                }
                return stream.ToArray();
            }
        }

        private static void Write32(Stream stream, uint value)
        {
            for (int shift = 24; shift >= 0; shift -= 8) stream.WriteByte((byte)(value >> shift));
        }

        private static void Write64(Stream stream, ulong value)
        {
            Write32(stream, (uint)(value >> 32));
            Write32(stream, (uint)value);
        }
    }
}
