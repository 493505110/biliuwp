using NSDanmaku.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using BiliBili.UWP;
using BiliBili.UWP.Models;
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
        private const int MaxConcurrentSegmentRequests = 4;
        private const long DanmakuClosedState = 1;

        public static async Task<List<DanmakuModel>> LoadAsync(
            long aid,
            long cid,
            double durationSeconds = 0,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var completed = await LoadCompleteAsync(aid, cid, durationSeconds, cancellationToken);
            return completed?.Items ?? new List<DanmakuModel>();
        }

        public static async Task<BiliDanmakuLoadResult> LoadCompleteAsync(
            long aid,
            long cid,
            double durationSeconds = 0,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var initial = await LoadInitialAsync(aid, cid, durationSeconds, cancellationToken);
            if (initial == null)
            {
                return new BiliDanmakuLoadResult(
                    new List<DanmakuModel>(),
                    false,
                    false);
            }

            return await LoadSupplementAsync(initial, cancellationToken);
        }

        /// <summary>
        /// Loads the first segmented packet so the player can start without
        /// waiting for the complete danmaku timeline.
        /// </summary>
        public static async Task<BiliDanmakuLoadResult> LoadInitialAsync(
            long aid,
            long cid,
            double durationSeconds = 0,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                    cancellationToken.ThrowIfCancellationRequested();
                    var legacy = await LoadLegacyAsync(cid);
                    cancellationToken.ThrowIfCancellationRequested();
                    return new BiliDanmakuLoadResult(
                        legacy,
                        false,
                        false);
                }
                catch (OperationCanceledException)
                {
                    throw;
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
                return await LoadWebInitialAsync(aid, cid, durationSeconds, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("加载新版弹幕失败，回退旧接口", LogType.ERROR, ex);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var legacy = await LoadLegacyAsync(cid);
                    cancellationToken.ThrowIfCancellationRequested();
                    return new BiliDanmakuLoadResult(
                        legacy,
                        false,
                        false);
                }
                catch (OperationCanceledException)
                {
                    throw;
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
        /// Completes the remaining segmented requests. Successful packets are
        /// retained when individual packets fail; the legacy XML endpoint is
        /// only used to recover those failed packets.
        /// </summary>
        public static async Task<BiliDanmakuLoadResult> LoadSupplementAsync(
            BiliDanmakuLoadResult initial,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (initial == null || initial.WebLoadPlan == null || initial.IsDanmakuClosed)
            {
                return initial ?? new BiliDanmakuLoadResult(
                    new List<DanmakuModel>(),
                    false,
                    false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var plan = initial.WebLoadPlan;
            var items = new List<DanmakuModel>(initial.Items ?? new List<DanmakuModel>());
            var basItems = new List<BasDanmakuModel>(initial.BasItems ?? new List<BasDanmakuModel>());
            var failedRegularSegmentCount = 0;
            var failedSpecialPackageCount = 0;
            var unsupportedDanmakuCount = initial.UnsupportedDanmakuCount;
            var unsupportedDanmakuModes = MergeUnsupportedDanmakuModes(
                initial.UnsupportedDanmakuModes,
                null);

            var segmentResults = await LoadRemainingSegmentsAsync(plan, cancellationToken);
            foreach (var segmentResult in segmentResults)
            {
                cancellationToken.ThrowIfCancellationRequested();
                unsupportedDanmakuCount += segmentResult.UnsupportedDanmakuCount;
                unsupportedDanmakuModes = MergeUnsupportedDanmakuModes(
                    unsupportedDanmakuModes,
                    segmentResult.UnsupportedDanmakuModes);
                if (segmentResult.Error != null)
                {
                    if (segmentResult.IsSpecialPackage)
                    {
                        failedSpecialPackageCount++;
                    }
                    else
                    {
                        failedRegularSegmentCount++;
                    }

                    LogHelper.WriteLog(
                        segmentResult.IsSpecialPackage
                            ? "加载 BAS/代码弹幕专包失败: " + segmentResult.Source
                            : "加载新版弹幕分段失败: " + segmentResult.SegmentIndex,
                        LogType.ERROR,
                        segmentResult.Error);
                    continue;
                }

                if (segmentResult.Items != null && segmentResult.Items.Count != 0)
                {
                    items.AddRange(segmentResult.Items);
                }
                if (segmentResult.BasItems != null && segmentResult.BasItems.Count != 0)
                {
                    basItems.AddRange(segmentResult.BasItems);
                }
            }

            if (failedRegularSegmentCount != 0)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var legacy = await LoadLegacyAsync(plan.Cid);
                    cancellationToken.ThrowIfCancellationRequested();
                    items = MergeDanmaku(items, legacy);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("补齐失败分段的旧版弹幕失败，保留新版结果", LogType.ERROR, ex);
                }
            }

            return new BiliDanmakuLoadResult(
                items,
                MergeBasDanmaku(null, basItems),
                failedRegularSegmentCount != 0 || failedSpecialPackageCount != 0,
                true,
                false,
                initial.SpecialDanmakuPackageCount,
                unsupportedDanmakuCount,
                unsupportedDanmakuModes,
                null);
        }

        /// <summary>
        /// Loads the legacy pool and merges it without dropping repeated comments
        /// that have different danmaku ids.
        /// </summary>
        public static async Task<List<DanmakuModel>> LoadLegacySupplementAsync(
            long cid,
            IEnumerable<DanmakuModel> initial,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (cid <= 0)
            {
                return initial == null
                    ? new List<DanmakuModel>()
                    : new List<DanmakuModel>(initial);
            }

            var legacy = await LoadLegacyAsync(cid);
            cancellationToken.ThrowIfCancellationRequested();
            return MergeDanmaku(initial, legacy);
        }

        public static List<DanmakuModel> MergeDanmaku(
            IEnumerable<DanmakuModel> initial,
            IEnumerable<DanmakuModel> supplement)
        {
            var result = new List<DanmakuModel>();
            var identities = new HashSet<string>(StringComparer.Ordinal);

            AddUniqueDanmaku(result, identities, initial);
            AddUniqueDanmaku(result, identities, supplement);
            return result;
        }

        public static List<BasDanmakuModel> MergeBasDanmaku(
            IEnumerable<BasDanmakuModel> initial,
            IEnumerable<BasDanmakuModel> supplement)
        {
            var result = new List<BasDanmakuModel>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            AddUniqueBasDanmaku(result, identities, initial);
            AddUniqueBasDanmaku(result, identities, supplement);
            return result;
        }

        private static void AddUniqueBasDanmaku(
            List<BasDanmakuModel> result,
            HashSet<string> identities,
            IEnumerable<BasDanmakuModel> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var item in source)
            {
                if (item == null || string.IsNullOrEmpty(item.text))
                {
                    continue;
                }

                var identity = !string.IsNullOrWhiteSpace(item.dmid)
                    && !string.Equals(item.dmid, "0", StringComparison.Ordinal)
                    ? "id|" + item.dmid
                    : string.Join("|", new[]
                    {
                        "value",
                        item.stime.ToString("R", CultureInfo.InvariantCulture),
                        item.text
                    });
                if (identities.Add(identity))
                {
                    result.Add(item);
                }
            }
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

        private static async Task<BiliDanmakuLoadResult> LoadWebInitialAsync(
            long aid,
            long cid,
            double durationSeconds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

            var state = GetDanmakuState(viewResponse.Bytes);
            var specialDanmakuUrls = GetSpecialDanmakuUrls(viewResponse.Bytes);
            if (state == DanmakuClosedState)
            {
                return new BiliDanmakuLoadResult(
                    new List<DanmakuModel>(),
                    false,
                    true,
                    true,
                    specialDanmakuUrls.Count,
                    0,
                    null);
            }

            var resolvedDuration = durationSeconds;
            if (resolvedDuration <= 0 || double.IsNaN(resolvedDuration) || double.IsInfinity(resolvedDuration))
            {
                resolvedDuration = await TryGetDurationSecondsAsync(aid, cid);
            }
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetSegmentCount(viewResponse.Bytes, resolvedDuration, out var segmentCount))
            {
                throw new InvalidDataException("新版弹幕分段配置无效");
            }

            var plan = new BiliDanmakuLoadPlan(aid, cid, segmentCount, specialDanmakuUrls);
            var firstSegment = await LoadSegmentAsync(plan, 1, cancellationToken);
            plan.RetryFirstSegment = firstSegment.Error != null;
            if (firstSegment.Error != null)
            {
                LogHelper.WriteLog("加载新版弹幕首段失败，继续后台补齐", LogType.ERROR, firstSegment.Error);
            }

            return new BiliDanmakuLoadResult(
                firstSegment.Items,
                firstSegment.BasItems,
                plan.RetryFirstSegment,
                true,
                false,
                specialDanmakuUrls.Count,
                firstSegment.UnsupportedDanmakuCount,
                firstSegment.UnsupportedDanmakuModes,
                plan);
        }

        private static async Task<List<SegmentLoadResult>> LoadRemainingSegmentsAsync(
            BiliDanmakuLoadPlan plan,
            CancellationToken cancellationToken)
        {
            var results = new List<SegmentLoadResult>();
            if (plan == null || !plan.HasPendingSegments)
            {
                return results;
            }

            using (var limiter = new SemaphoreSlim(MaxConcurrentSegmentRequests))
            {
                var tasks = new List<Task<SegmentLoadResult>>();
                for (var segmentIndex = 1; segmentIndex <= plan.SegmentCount; segmentIndex++)
                {
                    if (segmentIndex == 1 && !plan.RetryFirstSegment)
                    {
                        continue;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    tasks.Add(LoadSegmentWithLimitAsync(plan, segmentIndex, limiter, cancellationToken));
                }

                foreach (var specialUrl in plan.SpecialDanmakuUrls)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    tasks.Add(LoadSpecialPackageWithLimitAsync(
                        specialUrl,
                        limiter,
                        cancellationToken));
                }

                if (tasks.Count != 0)
                {
                    results.AddRange(await Task.WhenAll(tasks));
                }
            }

            return results;
        }

        private static async Task<SegmentLoadResult> LoadSegmentWithLimitAsync(
            BiliDanmakuLoadPlan plan,
            int segmentIndex,
            SemaphoreSlim limiter,
            CancellationToken cancellationToken)
        {
            await limiter.WaitAsync(cancellationToken);
            try
            {
                return await LoadSegmentAsync(plan, segmentIndex, cancellationToken);
            }
            finally
            {
                limiter.Release();
            }
        }

        private static async Task<SegmentLoadResult> LoadSegmentAsync(
            BiliDanmakuLoadPlan plan,
            int segmentIndex,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var segmentBytes = await GetSegmentBytesAsync(
                    plan.Aid,
                    plan.Cid,
                    segmentIndex,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (segmentBytes == null || segmentBytes.Length == 0)
                {
                    return new SegmentLoadResult(
                        segmentIndex,
                        false,
                        segmentIndex.ToString(CultureInfo.InvariantCulture),
                        new List<DanmakuModel>(),
                        new List<BasDanmakuModel>(),
                        0,
                        null);
                }

                var unsupportedDanmakuCount = 0;
                var unsupportedDanmakuModes = new Dictionary<int, int>();
                var basItems = new List<BasDanmakuModel>();
                var items = ParseSegment(
                    segmentBytes,
                    ref unsupportedDanmakuCount,
                    unsupportedDanmakuModes,
                    basItems);
                return new SegmentLoadResult(
                    segmentIndex,
                    false,
                    segmentIndex.ToString(CultureInfo.InvariantCulture),
                    items,
                    basItems,
                    unsupportedDanmakuCount,
                    unsupportedDanmakuModes,
                    null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new SegmentLoadResult(
                    segmentIndex,
                    false,
                    segmentIndex.ToString(CultureInfo.InvariantCulture),
                    new List<DanmakuModel>(),
                    new List<BasDanmakuModel>(),
                    0,
                    ex);
            }
        }

        private static async Task<SegmentLoadResult> LoadSpecialPackageWithLimitAsync(
            string url,
            SemaphoreSlim limiter,
            CancellationToken cancellationToken)
        {
            await limiter.WaitAsync(cancellationToken);
            try
            {
                return await LoadSpecialPackageAsync(url, cancellationToken);
            }
            finally
            {
                limiter.Release();
            }
        }

        private static async Task<SegmentLoadResult> LoadSpecialPackageAsync(
            string url,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(url))
                {
                    throw new InvalidDataException("特殊弹幕包地址为空");
                }

                var response = await GetBytesAsync(new Uri(url));
                cancellationToken.ThrowIfCancellationRequested();
                if (response.IsNotModified || response.Bytes == null || response.Bytes.Length == 0)
                {
                    return new SegmentLoadResult(
                        0,
                        true,
                        url,
                        new List<DanmakuModel>(),
                        new List<BasDanmakuModel>(),
                        0,
                        null);
                }

                var unsupportedDanmakuCount = 0;
                var unsupportedDanmakuModes = new Dictionary<int, int>();
                var basItems = new List<BasDanmakuModel>();
                var items = ParseSegment(
                    response.Bytes,
                    ref unsupportedDanmakuCount,
                    unsupportedDanmakuModes,
                    basItems);
                return new SegmentLoadResult(
                    0,
                    true,
                    url,
                    items,
                    basItems,
                    unsupportedDanmakuCount,
                    unsupportedDanmakuModes,
                    null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new SegmentLoadResult(
                    0,
                    true,
                    url,
                    new List<DanmakuModel>(),
                    new List<BasDanmakuModel>(),
                    0,
                    ex);
            }
        }

        private static long GetDanmakuState(byte[] bytes)
        {
            foreach (var field in ReadFields(bytes))
            {
                if (field.Number == 1 && field.WireType == 0)
                {
                    return ToLong(field.Varint);
                }
            }

            return 0;
        }

        private static List<string> GetSpecialDanmakuUrls(byte[] bytes)
        {
            var urls = new List<string>();
            foreach (var field in ReadFields(bytes))
            {
                if (field.Number == 6 && field.WireType == 2 && field.Bytes != null)
                {
                    var url = GetString(field);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        urls.Add(NormalizeSpecialDanmakuUrl(url));
                    }
                }
            }

            return urls;
        }

        private static string NormalizeSpecialDanmakuUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return url;
            }

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            var builder = new UriBuilder(uri)
            {
                Scheme = Uri.UriSchemeHttps,
                Port = -1
            };
            return builder.Uri.ToString();
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

        private static List<DanmakuModel> ParseSegment(
            byte[] bytes,
            ref int unsupportedDanmakuCount,
            Dictionary<int, int> unsupportedDanmakuModes,
            List<BasDanmakuModel> basItems)
        {
            var result = new List<DanmakuModel>();
            foreach (var field in ReadFields(bytes))
            {
                if (field.Number != 1 || field.WireType != 2 || field.Bytes == null)
                {
                    continue;
                }

                var item = ParseDanmaku(
                    field.Bytes,
                    ref unsupportedDanmakuCount,
                    unsupportedDanmakuModes,
                    basItems);
                if (item != null)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private static DanmakuModel ParseDanmaku(
            byte[] bytes,
            ref int unsupportedDanmakuCount,
            Dictionary<int, int> unsupportedDanmakuModes,
            List<BasDanmakuModel> basItems)
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
            if (modeValue == 9)
            {
                if (basItems != null)
                {
                    basItems.Add(new BasDanmakuModel
                    {
                        dmid = rowId,
                        stime = Math.Max(0, time),
                        text = text
                    });
                }

                return null;
            }

            DanmakuLocation location;
            if (!TryToLocation(modeValue, out location))
            {
                unsupportedDanmakuCount++;
                AddUnsupportedDanmakuMode(unsupportedDanmakuModes, modeValue);
                return null;
            }

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
                location = location,
                fromSite = DanmakuSite.Bilibili,
                source = BuildSource(progress, modeValue, sizeValue, colorValue, ctime, pool, midHash, rowId)
            };
        }

        private static bool TryToLocation(int mode, out DanmakuLocation location)
        {
            switch (mode)
            {
                case 1:
                case 2:
                case 3:
                    location = DanmakuLocation.Roll;
                    return true;
                case 4:
                    location = DanmakuLocation.Bottom;
                    return true;
                case 5:
                    location = DanmakuLocation.Top;
                    return true;
                case 7:
                    location = DanmakuLocation.Position;
                    return true;
                default:
                    location = DanmakuLocation.Roll;
                    return false;
            }
        }

        private static void AddUnsupportedDanmakuMode(
            Dictionary<int, int> modes,
            int mode)
        {
            if (modes == null)
            {
                return;
            }

            if (modes.ContainsKey(mode))
            {
                modes[mode]++;
            }
            else
            {
                modes[mode] = 1;
            }
        }

        private static Dictionary<int, int> MergeUnsupportedDanmakuModes(
            IDictionary<int, int> first,
            IDictionary<int, int> second)
        {
            var result = new Dictionary<int, int>();
            AddUnsupportedDanmakuModes(result, first);
            AddUnsupportedDanmakuModes(result, second);
            return result;
        }

        private static void AddUnsupportedDanmakuModes(
            Dictionary<int, int> target,
            IDictionary<int, int> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            foreach (var item in source)
            {
                if (target.ContainsKey(item.Key))
                {
                    target[item.Key] += item.Value;
                }
                else
                {
                    target[item.Key] = item.Value;
                }
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
            int segmentIndex,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                    cancellationToken.ThrowIfCancellationRequested();
                    var signedParameter = await ApiHelper.GetWbiSign(originalParameter);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(signedParameter))
                    {
                        throw new InvalidDataException("Wbi 签名失败");
                    }

                    var response = await GetBytesAsync(new Uri(SegmentUrl + "?" + signedParameter));
                    cancellationToken.ThrowIfCancellationRequested();
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
                catch (OperationCanceledException)
                {
                    throw;
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

        private sealed class SegmentLoadResult
        {
            public SegmentLoadResult(
                int segmentIndex,
                bool isSpecialPackage,
                string source,
                List<DanmakuModel> items,
                List<BasDanmakuModel> basItems,
                int unsupportedDanmakuCount,
                Exception error)
                : this(
                    segmentIndex,
                    isSpecialPackage,
                    source,
                    items,
                    basItems,
                    unsupportedDanmakuCount,
                    new Dictionary<int, int>(),
                    error)
            {
            }

            public SegmentLoadResult(
                int segmentIndex,
                bool isSpecialPackage,
                string source,
                List<DanmakuModel> items,
                List<BasDanmakuModel> basItems,
                int unsupportedDanmakuCount,
                IDictionary<int, int> unsupportedDanmakuModes,
                Exception error)
            {
                SegmentIndex = segmentIndex;
                IsSpecialPackage = isSpecialPackage;
                Source = source;
                Items = items ?? new List<DanmakuModel>();
                BasItems = basItems ?? new List<BasDanmakuModel>();
                UnsupportedDanmakuCount = unsupportedDanmakuCount;
                UnsupportedDanmakuModes = unsupportedDanmakuModes == null
                    ? new Dictionary<int, int>()
                    : new Dictionary<int, int>(unsupportedDanmakuModes);
                Error = error;
            }

            public int SegmentIndex { get; }
            public bool IsSpecialPackage { get; }
            public string Source { get; }
            public List<DanmakuModel> Items { get; }
            public List<BasDanmakuModel> BasItems { get; }
            public int UnsupportedDanmakuCount { get; }
            public Dictionary<int, int> UnsupportedDanmakuModes { get; }
            public Exception Error { get; }
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

    internal sealed class BiliDanmakuLoadPlan
    {
        public BiliDanmakuLoadPlan(
            long aid,
            long cid,
            int segmentCount,
            IEnumerable<string> specialDanmakuUrls)
        {
            Aid = aid;
            Cid = cid;
            SegmentCount = Math.Max(1, segmentCount);
            SpecialDanmakuUrls = specialDanmakuUrls == null
                ? new List<string>()
                : new List<string>(specialDanmakuUrls);
        }

        public long Aid { get; }
        public long Cid { get; }
        public int SegmentCount { get; }
        public List<string> SpecialDanmakuUrls { get; }
        public bool RetryFirstSegment { get; set; }

        public bool HasPendingSegments
        {
            get
            {
                return RetryFirstSegment
                    || SegmentCount > 1
                    || SpecialDanmakuUrls.Count != 0;
            }
        }
    }

    public sealed class BiliDanmakuLoadResult
    {
        public BiliDanmakuLoadResult(
            List<DanmakuModel> items,
            bool needsLegacySupplement,
            bool usedNewInterface)
            : this(
                items,
                needsLegacySupplement,
                usedNewInterface,
                false,
                0,
                0,
                null)
        {
        }

        internal BiliDanmakuLoadResult(
            List<DanmakuModel> items,
            bool needsLegacySupplement,
            bool usedNewInterface,
            bool isDanmakuClosed,
            int specialDanmakuPackageCount,
            int unsupportedDanmakuCount,
            BiliDanmakuLoadPlan webLoadPlan)
            : this(
                items,
                new List<BasDanmakuModel>(),
                needsLegacySupplement,
                usedNewInterface,
                isDanmakuClosed,
                specialDanmakuPackageCount,
                unsupportedDanmakuCount,
                new Dictionary<int, int>(),
                webLoadPlan)
        {
        }

        internal BiliDanmakuLoadResult(
            List<DanmakuModel> items,
            List<BasDanmakuModel> basItems,
            bool needsLegacySupplement,
            bool usedNewInterface,
            bool isDanmakuClosed,
            int specialDanmakuPackageCount,
            int unsupportedDanmakuCount,
            IDictionary<int, int> unsupportedDanmakuModes,
            BiliDanmakuLoadPlan webLoadPlan)
        {
            Items = items ?? new List<DanmakuModel>();
            BasItems = basItems ?? new List<BasDanmakuModel>();
            NeedsLegacySupplement = needsLegacySupplement;
            UsedNewInterface = usedNewInterface;
            IsDanmakuClosed = isDanmakuClosed;
            SpecialDanmakuPackageCount = Math.Max(0, specialDanmakuPackageCount);
            UnsupportedDanmakuCount = Math.Max(0, unsupportedDanmakuCount);
            UnsupportedDanmakuModes = unsupportedDanmakuModes == null
                ? new Dictionary<int, int>()
                : new Dictionary<int, int>(unsupportedDanmakuModes);
            WebLoadPlan = webLoadPlan;
        }

        public List<DanmakuModel> Items { get; }
        public List<BasDanmakuModel> BasItems { get; }
        public bool NeedsLegacySupplement { get; }
        public bool UsedNewInterface { get; }
        public bool IsDanmakuClosed { get; }
        public int SpecialDanmakuPackageCount { get; }
        public int UnsupportedDanmakuCount { get; }
        public Dictionary<int, int> UnsupportedDanmakuModes { get; }
        internal BiliDanmakuLoadPlan WebLoadPlan { get; }
    }
}
