using System;
using System.Collections.Generic;
using System.Linq;

namespace BiliBili.UWP.Modules.Playback
{
    public sealed class DashStreamInfo
    {
        public DashStreamInfo(int qualityId, int codecId, long bandwidth, string mimeType, string baseUrl, IReadOnlyList<string> backupUrls = null)
        {
            QualityId = qualityId;
            CodecId = codecId;
            Bandwidth = bandwidth;
            MimeType = mimeType;
            BaseUrl = baseUrl;
            BackupUrls = backupUrls ?? Array.Empty<string>();
        }

        public int QualityId { get; }
        public int CodecId { get; }
        public long Bandwidth { get; }
        public string MimeType { get; }
        public string BaseUrl { get; }
        public IReadOnlyList<string> BackupUrls { get; }
    }

    public static class DashStreamSelector
    {
        public static DashStreamInfo SelectVideo(IEnumerable<DashStreamInfo> streams, int qualityId, int codecId)
        {
            var candidates = streams?.Where(IsVideo).Where(x => x.CodecId == codecId).ToList();
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            return candidates
                .Where(x => x.QualityId == qualityId)
                .OrderByDescending(x => x.Bandwidth)
                .FirstOrDefault()
                ?? candidates
                    .Where(x => x.QualityId <= qualityId)
                    .OrderByDescending(x => x.QualityId)
                    .ThenByDescending(x => x.Bandwidth)
                    .FirstOrDefault()
                ?? candidates
                    .OrderByDescending(x => x.QualityId)
                    .ThenByDescending(x => x.Bandwidth)
                    .FirstOrDefault();
        }

        public static DashStreamInfo SelectAudio(IEnumerable<DashStreamInfo> streams)
        {
            return streams?.Where(x => IsPlayable(x) && string.Equals(x.MimeType, "audio/mp4", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Bandwidth)
                .FirstOrDefault();
        }

        public static bool IsPlayable(DashStreamInfo stream)
        {
            return stream != null
                && ResolvePlayableUrl(stream.BaseUrl, stream.BackupUrls) != null;
        }

        public static string ResolvePlayableUrl(string baseUrl, IEnumerable<string> backupUrls)
        {
            return new[] { baseUrl }
                .Concat(backupUrls ?? Enumerable.Empty<string>())
                .FirstOrDefault(IsHttpUrl);
        }

        public static IEnumerable<int> GetCodecPreference(int preferredCodecId, bool forceCodec = false)
        {
            yield return preferredCodecId;
            if (forceCodec)
            {
                yield break;
            }
            if (preferredCodecId == 13)
            {
                yield return 12;
                yield return 7;
            }
            else if (preferredCodecId == 12)
            {
                yield return 7;
            }
        }

        private static bool IsVideo(DashStreamInfo stream)
        {
            return IsPlayable(stream)
                && !string.IsNullOrWhiteSpace(stream.MimeType)
                && stream.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHttpUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
