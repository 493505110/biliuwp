using NSDanmaku.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using BiliBili.UWP;
using Windows.UI;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// Loads the web segmented danmaku stream used by the Bilibili web player.
    /// </summary>
    public static class BiliDanmakuService
    {
        private const string ViewUrl = "https://api.bilibili.com/x/v2/dm/web/view";
        private const string SegmentUrl = "https://api.bilibili.com/x/v2/dm/wbi/web/seg.so";
        private const int MaxSegmentCount = 10000;
        private const int MaxUnknownDurationSegmentCount = 100;

        public static async Task<List<DanmakuModel>> LoadAsync(
            long aid,
            long cid,
            double durationSeconds = 0)
        {
            var initial = await LoadInitialAsync(aid, cid, durationSeconds);
            if (initial == null || initial.Items == null)
            {
                return new List<DanmakuModel>();
            }

            if (!initial.NeedsLegacySupplement)
            {
                return initial.Items;
            }

            try
            {
                return await LoadLegacySupplementAsync(cid, initial.Items);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("补齐旧版弹幕失败，保留新版结果", LogType.ERROR, ex);
                return initial.Items;
            }
        }

        /// <summary>
        /// Loads the fast initial pool. The player can start with this result and
        /// fetch the legacy supplement in the background when the web metadata
        /// indicates that the segmented response is incomplete.
        /// </summary>
        public static async Task<BiliDanmakuLoadResult> LoadInitialAsync(
            long aid,
            long cid,
            double durationSeconds = 0)
        {
            if (cid <= 0)
            {
                return new BiliDanmakuLoadResult(
                    new List<DanmakuModel>(),
                    false,
                    false);
            }

            if (!SettingHelper.Get_UseNewDanmakuInterface())
            {
                try
                {
                    return new BiliDanmakuLoadResult(
                        await LoadLegacyAsync(cid),
                        false,
                        false);
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("加载旧版弹幕失败", LogType.ERROR, ex);
                    return new BiliDanmakuLoadResult(
                        new List<DanmakuModel>(),
                        false,
                        false);
                }
            }

            try
            {
                var webResult = await LoadWebAsync(aid, cid, durationSeconds);
                var items = webResult.Items ?? new List<DanmakuModel>();
                var needsSupplement = webResult.TotalCount < 0
                    ? items.Count != 0
                    : webResult.TotalCount != items.Count;
                return new BiliDanmakuLoadResult(items, needsSupplement, true);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("加载新版弹幕失败，回退旧接口", LogType.ERROR, ex);
                try
                {
                    return new BiliDanmakuLoadResult(
                        await LoadLegacyAsync(cid),
                        false,
                        false);
                }
                catch (Exception fallbackException)
                {
                    LogHelper.WriteLog("回退旧弹幕接口失败", LogType.ERROR, fallbackException);
                    return new BiliDanmakuLoadResult(
                        new List<DanmakuModel>(),
                        false,
                        false);
                }
            }
        }

        /// <summary>
        /// Loads the legacy pool and merges it without dropping repeated comments
        /// that have different danmaku ids.
        /// </summary>
        public static async Task<List<DanmakuModel>> LoadLegacySupplementAsync(
            long cid,
            IEnumerable<DanmakuModel> initial)
        {
            if (cid <= 0)
            {
                return initial == null
                    ? new List<DanmakuModel>()
                    : new List<DanmakuModel>(initial);
            }

            var legacy = await LoadLegacyAsync(cid);
            return MergeDanmaku(initial, legacy);
        }

        public static List<DanmakuModel> MergeDanmaku(
            IEnumerable<DanmakuModel> initial,
            IEnumerable<DanmakuModel> supplement)
        {
            var result = new List<DanmakuModel>();
            var identities = new HashSet<string>(StringComparer.Ordinal);

            AddUniqueDanmaku(result, identities, supplement);
            AddUniqueDanmaku(result, identities, initial);
            return result;
        }

        private static void AddUniqueDanmaku(
            List<DanmakuModel> result,
            HashSet<string> identities,
            IEnumerable<DanmakuModel> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var item in source)
            {
                if (item == null)
                {
                    continue;
                }

                var identity = GetDanmakuIdentity(item);
                if (identity == null || identities.Add(identity))
                {
                    result.Add(item);
                }
            }
        }

        private static string GetDanmakuIdentity(DanmakuModel item)
        {
            if (!string.IsNullOrWhiteSpace(item.rowID)
                && !string.Equals(item.rowID, "0", StringComparison.Ordinal))
            {
                return "id|" + item.rowID;
            }

            var color = (item.color.R << 16) | (item.color.G << 8) | item.color.B;
            return string.Join("|", new[]
            {
                "value",
                item.time.ToString("R", CultureInfo.InvariantCulture),
                ((int)item.location).ToString(CultureInfo.InvariantCulture),
                item.size.ToString(CultureInfo.InvariantCulture),
                color.ToString(CultureInfo.InvariantCulture),
                item.sendTime ?? string.Empty,
                item.sendID ?? string.Empty,
                item.text ?? string.Empty
            });
        }

        private static async Task<List<DanmakuModel>> LoadLegacyAsync(long cid)
        {
            var result = await new NSDanmaku.Helper.DanmakuParse().ParseBiliBili(cid);
            return result ?? new List<DanmakuModel>();
        }

        public static async Task<string> GetXmlAsync(
            long aid,
            long cid,
            double durationSeconds = 0)
        {
            var danmakus = await LoadAsync(aid, cid, durationSeconds);
            var builder = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false
            };

            using (var writer = XmlWriter.Create(builder, settings))
            {
                writer.WriteStartElement("i");
                writer.WriteElementString("chatserver", "chat.bilibili.com");
                writer.WriteElementString("chatid", cid.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("mission", "0");
                writer.WriteElementString("maxlimit", danmakus.Count.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("state", "0");
                writer.WriteElementString("real_name", "0");
                writer.WriteElementString("source", "k-v");

                foreach (var item in danmakus)
                {
                    writer.WriteStartElement("d");
                    writer.WriteAttributeString("p", BuildXmlParameter(item));
                    writer.WriteString(item.text ?? string.Empty);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            return builder.ToString();
        }

        private static async Task<WebDanmakuResult> LoadWebAsync(
            long aid,
            long cid,
            double durationSeconds)
        {
            var viewUrl = ViewUrl
                + "?type=1&oid=" + cid.ToString(CultureInfo.InvariantCulture);
            if (aid > 0)
            {
                viewUrl += "&pid=" + aid.ToString(CultureInfo.InvariantCulture);
            }

            var viewResponse = await GetBytesAsync(new Uri(viewUrl));
            if (viewResponse.IsNotModified || viewResponse.Bytes == null || viewResponse.Bytes.Length == 0)
            {
                throw new InvalidDataException("新版弹幕分段信息为空");
            }

            var resolvedDuration = durationSeconds;
            if (resolvedDuration <= 0 || double.IsNaN(resolvedDuration) || double.IsInfinity(resolvedDuration))
            {
                resolvedDuration = await TryGetDurationSecondsAsync(aid, cid);
            }

            if (!TryGetSegmentCount(viewResponse.Bytes, resolvedDuration, out var segmentCount))
            {
                throw new InvalidDataException("新版弹幕分段配置无效");
            }

            var result = new List<DanmakuModel>();
            for (var segmentIndex = 1; segmentIndex <= segmentCount; segmentIndex++)
            {
                var segmentBytes = await GetSegmentBytesAsync(aid, cid, segmentIndex);
                if (segmentBytes != null && segmentBytes.Length != 0)
                {
                    ParseSegment(segmentBytes, result);
                }
            }

            return new WebDanmakuResult(result, GetTotalDanmakuCount(viewResponse.Bytes));
        }

        private static long GetTotalDanmakuCount(byte[] bytes)
        {
            foreach (var field in ReadFields(bytes))
            {
                if (field.Number == 8 && field.WireType == 0)
                {
                    return ToLong(field.Varint);
                }
            }

            return -1;
        }

        private static async Task<double> TryGetDurationSecondsAsync(long aid, long cid)
        {
            if (aid <= 0)
            {
                return 0;
            }

            try
            {
                var response = await GetBytesAsync(new Uri(
                    "https://api.bilibili.com/x/web-interface/view?aid="
                    + aid.ToString(CultureInfo.InvariantCulture)));
                if (response.IsNotModified || response.Bytes == null || response.Bytes.Length == 0)
                {
                    return 0;
                }

                var root = JObject.Parse(Encoding.UTF8.GetString(response.Bytes, 0, response.Bytes.Length));
                if ((root["code"]?.Value<int>() ?? -1) != 0)
                {
                    return 0;
                }

                var data = root["data"] as JObject;
                var pages = data?["pages"] as JArray;
                if (pages != null)
                {
                    foreach (var page in pages)
                    {
                        if ((page["cid"]?.Value<long>() ?? 0) != cid)
                        {
                            continue;
                        }

                        var pageDuration = page["duration"]?.Value<double>() ?? 0;
                        if (pageDuration > 0)
                        {
                            return pageDuration;
                        }
                    }
                }

                return data?["duration"]?.Value<double>() ?? 0;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("读取视频时长失败，使用弹幕接口探测范围", LogType.INFO, ex);
                return 0;
            }
        }

        private static bool TryGetSegmentCount(
            byte[] bytes,
            double durationSeconds,
            out int segmentCount)
        {
            segmentCount = 0;
            long pageSize = 0;
            foreach (var field in ReadFields(bytes))
            {
                if (field.Number != 4 || field.WireType != 2 || field.Bytes == null)
                {
                    continue;
                }

                foreach (var segmentField in ReadFields(field.Bytes))
                {
                    if (segmentField.WireType != 0)
                    {
                        continue;
                    }

                    if (segmentField.Number == 1)
                    {
                        pageSize = ToLong(segmentField.Varint);
                    }
                }
            }

            if (pageSize <= 0)
            {
                return false;
            }

            if (durationSeconds > 0
                && !double.IsNaN(durationSeconds)
                && !double.IsInfinity(durationSeconds))
            {
                var calculatedCount = Math.Ceiling(durationSeconds * 1000d / pageSize);
                if (calculatedCount < 1 || calculatedCount > MaxSegmentCount)
                {
                    return false;
                }

                segmentCount = (int)calculatedCount;
                return true;
            }

            // The duration is normally supplied by the player or resolved from the aid.
            // When both are unavailable, use a bounded probe instead of treating the
            // server's total field as the number of segments to request.
            segmentCount = MaxUnknownDurationSegmentCount;
            return true;
        }

        private static void ParseSegment(byte[] bytes, List<DanmakuModel> result)
        {
            foreach (var field in ReadFields(bytes))
            {
                if (field.Number != 1 || field.WireType != 2 || field.Bytes == null)
                {
                    continue;
                }

                var item = ParseDanmaku(field.Bytes);
                if (item != null)
                {
                    result.Add(item);
                }
            }
        }

        private static DanmakuModel ParseDanmaku(byte[] bytes)
        {
            long id = 0;
            long progress = 0;
            long mode = 1;
            long size = 25;
            long color = 0xFFFFFF;
            long ctime = 0;
            long pool = 0;
            string midHash = string.Empty;
            string text = string.Empty;
            string idString = string.Empty;

            foreach (var field in ReadFields(bytes))
            {
                switch (field.Number)
                {
                    case 1:
                        id = ToLong(field.Varint);
                        break;
                    case 2:
                        progress = ToLong(field.Varint);
                        break;
                    case 3:
                        mode = ToLong(field.Varint);
                        break;
                    case 4:
                        size = ToLong(field.Varint);
                        break;
                    case 5:
                        color = ToLong(field.Varint);
                        break;
                    case 6:
                        midHash = GetString(field);
                        break;
                    case 7:
                        text = GetString(field);
                        break;
                    case 8:
                        ctime = ToLong(field.Varint);
                        break;
                    case 11:
                        pool = ToLong(field.Varint);
                        break;
                    case 12:
                        idString = GetString(field);
                        break;
                }
            }

            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            var time = progress / 1000d;
            var rowId = string.IsNullOrEmpty(idString)
                ? id.ToString(CultureInfo.InvariantCulture)
                : idString;
            var modeValue = mode > int.MaxValue ? 1 : (int)mode;
            var sizeValue = size > 0 ? size : 25;
            var colorValue = unchecked((uint)color);
            return new DanmakuModel
            {
                text = text,
                size = sizeValue,
                color = Color.FromArgb(
                    255,
                    (byte)((colorValue >> 16) & 0xFF),
                    (byte)((colorValue >> 8) & 0xFF),
                    (byte)(colorValue & 0xFF)),
                time = time,
                time_s = Convert.ToInt32(time),
                sendTime = ctime.ToString(CultureInfo.InvariantCulture),
                pool = pool.ToString(CultureInfo.InvariantCulture),
                sendID = midHash,
                rowID = rowId,
                location = ToLocation(modeValue),
                fromSite = DanmakuSite.Bilibili,
                source = BuildSource(progress, modeValue, sizeValue, colorValue, ctime, pool, midHash, rowId)
            };
        }

        private static DanmakuLocation ToLocation(int mode)
        {
            switch (mode)
            {
                case 4:
                    return DanmakuLocation.Bottom;
                case 5:
                    return DanmakuLocation.Top;
                case 7:
                    return DanmakuLocation.Position;
                default:
                    return DanmakuLocation.Roll;
            }
        }

        private static string BuildSource(
            long progress,
            int mode,
            long size,
            uint color,
            long ctime,
            long pool,
            string midHash,
            string rowId)
        {
            return string.Join(",", new[]
            {
                (progress / 1000d).ToString(CultureInfo.InvariantCulture),
                mode.ToString(CultureInfo.InvariantCulture),
                size.ToString(CultureInfo.InvariantCulture),
                color.ToString(CultureInfo.InvariantCulture),
                ctime.ToString(CultureInfo.InvariantCulture),
                pool.ToString(CultureInfo.InvariantCulture),
                midHash ?? string.Empty,
                rowId ?? string.Empty
            });
        }

        private static string BuildXmlParameter(DanmakuModel item)
        {
            var mode = item.location == DanmakuLocation.Bottom
                ? 4
                : item.location == DanmakuLocation.Top
                    ? 5
                    : item.location == DanmakuLocation.Position ? 7 : 1;
            var color = (item.color.R << 16) | (item.color.G << 8) | item.color.B;
            return string.Join(",", new[]
            {
                item.time.ToString(CultureInfo.InvariantCulture),
                mode.ToString(CultureInfo.InvariantCulture),
                item.size.ToString(CultureInfo.InvariantCulture),
                color.ToString(CultureInfo.InvariantCulture),
                item.sendTime ?? "0",
                item.pool ?? "0",
                item.sendID ?? string.Empty,
                item.rowID ?? string.Empty
            });
        }

        private static async Task<HttpBytesResult> GetBytesAsync(Uri url)
        {
            using (var filter = new HttpBaseProtocolFilter())
            using (var client = new HttpClient(filter))
            {
                filter.IgnorableServerCertificateErrors.Add(
                    Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Append("User-Agent", "Mozilla/5.0");
                request.Headers.Append("Referer", "https://www.bilibili.com/");
                var cookies = ApiHelper.GetCookies();
                if (!string.IsNullOrEmpty(cookies))
                {
                    request.Headers.Append("Cookie", cookies);
                }

                var response = await client.SendRequestAsync(request);
                var statusCode = (int)response.StatusCode;
                if (statusCode == 304)
                {
                    return new HttpBytesResult(statusCode, null);
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException("弹幕接口 HTTP 状态码: " + statusCode);
                }

                var buffer = await response.Content.ReadAsBufferAsync();
                return new HttpBytesResult(statusCode, buffer?.ToArray());
            }
        }

        private static async Task<byte[]> GetSegmentBytesAsync(
            long aid,
            long cid,
            int segmentIndex)
        {
            var originalParameter = "type=1"
                + "&oid=" + cid.ToString(CultureInfo.InvariantCulture)
                + (aid > 0
                    ? "&pid=" + aid.ToString(CultureInfo.InvariantCulture)
                    : string.Empty)
                + "&segment_index=" + segmentIndex.ToString(CultureInfo.InvariantCulture)
                + "&web_location=1315873";

            Exception lastException = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    var signedParameter = await ApiHelper.GetWbiSign(originalParameter);
                    if (string.IsNullOrEmpty(signedParameter))
                    {
                        throw new InvalidDataException("Wbi 签名失败");
                    }

                    var response = await GetBytesAsync(new Uri(SegmentUrl + "?" + signedParameter));
                    if (response.IsNotModified
                        || response.Bytes == null
                        || response.Bytes.Length == 0)
                    {
                        return null;
                    }
                    if (!IsJsonResponse(response.Bytes))
                    {
                        return response.Bytes;
                    }

                    lastException = new InvalidDataException("新版弹幕分段接口返回 JSON 错误");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }

                if (attempt == 0)
                {
                    ApiHelper.ClearWbiKey();
                }
            }

            throw lastException ?? new InvalidDataException("新版弹幕分段接口返回错误");
        }

        private static bool IsJsonResponse(byte[] bytes)
        {
            if (bytes == null)
            {
                return false;
            }

            foreach (var value in bytes)
            {
                if (value == 0x20 || value == 0x09 || value == 0x0A || value == 0x0D)
                {
                    continue;
                }

                return value == (byte)'{' || value == (byte)'[';
            }

            return false;
        }

        private static string GetString(ProtoField field)
        {
            return field == null || field.Bytes == null
                ? string.Empty
                : Encoding.UTF8.GetString(field.Bytes, 0, field.Bytes.Length);
        }

        private static long ToLong(ulong value)
        {
            return unchecked((long)value);
        }

        private static List<ProtoField> ReadFields(byte[] bytes)
        {
            var fields = new List<ProtoField>();
            if (bytes == null)
            {
                return fields;
            }

            var offset = 0;
            while (offset < bytes.Length)
            {
                var tag = ReadVarint(bytes, ref offset);
                var number = (int)(tag >> 3);
                var wireType = (int)(tag & 7);
                if (number <= 0)
                {
                    break;
                }

                switch (wireType)
                {
                    case 0:
                        fields.Add(new ProtoField(number, wireType, ReadVarint(bytes, ref offset), null));
                        break;
                    case 1:
                        EnsureAvailable(bytes, offset, 8);
                        offset += 8;
                        break;
                    case 2:
                        var length = ReadVarint(bytes, ref offset);
                        if (length > int.MaxValue)
                        {
                            throw new InvalidDataException("Protobuf 字段长度无效");
                        }

                        var count = (int)length;
                        EnsureAvailable(bytes, offset, count);
                        var fieldBytes = new byte[count];
                        Buffer.BlockCopy(bytes, offset, fieldBytes, 0, count);
                        offset += count;
                        fields.Add(new ProtoField(number, wireType, 0, fieldBytes));
                        break;
                    case 3:
                        SkipGroup(bytes, ref offset);
                        break;
                    case 4:
                        return fields;
                    case 5:
                        EnsureAvailable(bytes, offset, 4);
                        offset += 4;
                        break;
                    default:
                        throw new InvalidDataException("Protobuf 字段类型无效");
                }
            }

            return fields;
        }

        private static void SkipGroup(byte[] bytes, ref int offset)
        {
            var depth = 1;
            while (offset < bytes.Length && depth > 0)
            {
                var tag = ReadVarint(bytes, ref offset);
                var wireType = (int)(tag & 7);
                switch (wireType)
                {
                    case 0:
                        ReadVarint(bytes, ref offset);
                        break;
                    case 1:
                        EnsureAvailable(bytes, offset, 8);
                        offset += 8;
                        break;
                    case 2:
                        var length = ReadVarint(bytes, ref offset);
                        if (length > int.MaxValue)
                        {
                            throw new InvalidDataException("Protobuf 分组长度无效");
                        }

                        EnsureAvailable(bytes, offset, (int)length);
                        offset += (int)length;
                        break;
                    case 3:
                        depth++;
                        break;
                    case 4:
                        depth--;
                        break;
                    case 5:
                        EnsureAvailable(bytes, offset, 4);
                        offset += 4;
                        break;
                    default:
                        throw new InvalidDataException("Protobuf 分组类型无效");
                }
            }
        }

        private static ulong ReadVarint(byte[] bytes, ref int offset)
        {
            ulong value = 0;
            var shift = 0;
            while (offset < bytes.Length && shift < 64)
            {
                var current = bytes[offset++];
                value |= (ulong)(current & 0x7F) << shift;
                if ((current & 0x80) == 0)
                {
                    return value;
                }

                shift += 7;
            }

            throw new InvalidDataException("Protobuf 变长整数无效");
        }

        private static void EnsureAvailable(byte[] bytes, int offset, int count)
        {
            if (count < 0 || offset < 0 || offset > bytes.Length - count)
            {
                throw new InvalidDataException("Protobuf 数据长度无效");
            }
        }

        private sealed class HttpBytesResult
        {
            public HttpBytesResult(int statusCode, byte[] bytes)
            {
                StatusCode = statusCode;
                Bytes = bytes;
            }

            public int StatusCode { get; }
            public byte[] Bytes { get; }
            public bool IsNotModified => StatusCode == 304;
        }

        private sealed class WebDanmakuResult
        {
            public WebDanmakuResult(List<DanmakuModel> items, long totalCount)
            {
                Items = items;
                TotalCount = totalCount;
            }

            public List<DanmakuModel> Items { get; }
            public long TotalCount { get; }
        }

        private sealed class ProtoField
        {
            public ProtoField(int number, int wireType, ulong varint, byte[] bytes)
            {
                Number = number;
                WireType = wireType;
                Varint = varint;
                Bytes = bytes;
            }

            public int Number { get; }
            public int WireType { get; }
            public ulong Varint { get; }
            public byte[] Bytes { get; }
        }
    }

    public sealed class BiliDanmakuLoadResult
    {
        public BiliDanmakuLoadResult(
            List<DanmakuModel> items,
            bool needsLegacySupplement,
            bool usedNewInterface)
        {
            Items = items ?? new List<DanmakuModel>();
            NeedsLegacySupplement = needsLegacySupplement;
            UsedNewInterface = usedNewInterface;
        }

        public List<DanmakuModel> Items { get; }
        public bool NeedsLegacySupplement { get; }
        public bool UsedNewInterface { get; }
    }
}
