using BiliBili.UWP.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace BiliBili.UWP.Helper
{
    public static class InteractiveDanmakuService
    {
        private const string ViewUrl = "https://api.bilibili.com/x/v2/dm/web/view";
        private const string VotePostUrl = "https://api.bilibili.com/x/vote/do_vote";
        private const string GradePostUrl = "https://api.bilibili.com/x/v2/dm/command/grade/post";
        private const string SubmissionStateContainerName = "InteractiveDanmakuSubmissionStates";
        private static readonly object submissionStateLock = new object();
        private static readonly Dictionary<string, int> submissionStates = new Dictionary<string, int>();
        private static ApplicationDataContainer submissionStateContainer;

        public static async Task<List<InteractiveDanmakuModel>> LoadAsync(long aid, long cid)
        {
            var result = new List<InteractiveDanmakuModel>();
            if (aid <= 0 || cid <= 0)
            {
                return result;
            }

            try
            {
                var url = new Uri(ViewUrl + "?type=1&oid=" + cid + "&pid=" + aid);
                var bytes = await GetBytesAsync(url);
                foreach (var field in ReadFields(bytes))
                {
                    if (field.Number != 9 || field.WireType != 2 || field.Bytes == null)
                    {
                        continue;
                    }

                    var command = ParseCommand(field.Bytes);
                    var item = CreateModel(command);
                    if (item != null)
                    {
                        if (item.Type == InteractiveDanmakuType.Vote
                            || item.Type == InteractiveDanmakuType.Grade)
                        {
                            RestoreSubmissionState(aid, cid, item);
                        }
                        result.Add(item);
                    }
                }

                result.Sort((left, right) => left.Progress.CompareTo(right.Progress));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("加载互动弹幕失败", LogType.ERROR, ex);
            }

            return result;
        }

        public static async Task<InteractiveDanmakuSubmitResult> SubmitVoteAsync(
            long aid,
            long cid,
            int progress,
            long voteId,
            int option)
        {
            long voterUid;
            long.TryParse(
                ApiHelper.GetUserId(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out voterUid);

            var payload = new JObject();
            payload["vote_id"] = voteId;
            payload["votes"] = new JArray(option);
            payload["voter_uid"] = voterUid;
            payload["status"] = 0;
            payload["op_bit"] = 0;
            payload["dynamic_id"] = 0;
            var result = await SubmitJsonAsync(VotePostUrl, payload);
            if (result.Success)
            {
                RememberSubmissionState(aid, cid, InteractiveDanmakuType.Vote, voteId, option);
            }

            return result;
        }

        public static async Task<InteractiveDanmakuSubmitResult> SubmitGradeAsync(
            long aid,
            long cid,
            int progress,
            long gradeId,
            int gradeScore)
        {
            var body = BuildForm(
                "aid", aid.ToString(CultureInfo.InvariantCulture),
                "cid", cid.ToString(CultureInfo.InvariantCulture),
                "progress", progress.ToString(CultureInfo.InvariantCulture),
                "grade_id", gradeId.ToString(CultureInfo.InvariantCulture),
                "grade_score", gradeScore.ToString(CultureInfo.InvariantCulture));
            var result = await SubmitAsync(GradePostUrl, body);
            if (result.Success)
            {
                RememberSubmissionState(
                    aid,
                    cid,
                    InteractiveDanmakuType.Grade,
                    gradeId,
                    Math.Max(1, Math.Min(5, gradeScore / 2)));
            }

            return result;
        }

        private static async Task<InteractiveDanmakuSubmitResult> SubmitAsync(string url, string body)
        {
            try
            {
                var csrf = GetCookieValue("bili_jct");
                if (string.IsNullOrEmpty(csrf))
                {
                    return new InteractiveDanmakuSubmitResult
                    {
                        Success = false,
                        Message = "登录状态已失效，请重新登录"
                    };
                }

                body += "&csrf=" + Uri.EscapeDataString(csrf)
                    + "&csrf_token=" + Uri.EscapeDataString(csrf);
                var json = await PostFormAsync(new Uri(url), body);
                if (json == null)
                {
                    return new InteractiveDanmakuSubmitResult
                    {
                        Success = false,
                        Message = "提交互动弹幕失败"
                    };
                }

                return CreateSubmitResult(json);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("提交互动弹幕失败", LogType.ERROR, ex);
                return new InteractiveDanmakuSubmitResult
                {
                    Success = false,
                    Message = "提交互动弹幕失败"
                };
            }
        }

        private static async Task<InteractiveDanmakuSubmitResult> SubmitJsonAsync(
            string url,
            JObject payload)
        {
            try
            {
                var csrf = GetCookieValue("bili_jct");
                if (string.IsNullOrEmpty(csrf))
                {
                    return new InteractiveDanmakuSubmitResult
                    {
                        Success = false,
                        Message = "登录状态已失效，请重新登录"
                    };
                }

                payload["csrf"] = csrf;
                payload["csrf_token"] = csrf;
                var requestUrl = url + "?csrf=" + Uri.EscapeDataString(csrf);
                var json = await PostJsonAsync(new Uri(requestUrl), payload.ToString());
                return CreateSubmitResult(json);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("提交投票弹幕失败", LogType.ERROR, ex);
                return new InteractiveDanmakuSubmitResult
                {
                    Success = false,
                    Message = "提交投票弹幕失败"
                };
            }
        }

        private static InteractiveDanmakuSubmitResult CreateSubmitResult(JObject json)
        {
            if (json == null)
            {
                return new InteractiveDanmakuSubmitResult
                {
                    Success = false,
                    Message = "提交互动弹幕失败"
                };
            }

            var code = GetInt(json["code"]);
            var message = GetString(json["message"]);
            if (string.IsNullOrWhiteSpace(message) || message == "0")
            {
                message = GetString(json["msg"]);
            }

            return new InteractiveDanmakuSubmitResult
            {
                Success = code == 0,
                Message = code == 0
                    ? "提交成功"
                    : (string.IsNullOrWhiteSpace(message)
                        ? "提交失败 (错误码: " + code + ")"
                        : message)
            };
        }

        private static async Task<byte[]> GetBytesAsync(Uri url)
        {
            using (var filter = new HttpBaseProtocolFilter())
            using (var client = new HttpClient(filter))
            {
                filter.IgnorableServerCertificateErrors.Add(
                    Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddHeaders(request);
                var response = await client.SendRequestAsync(request);
                response.EnsureSuccessStatusCode();
                var buffer = await response.Content.ReadAsBufferAsync();
                return buffer.ToArray();
            }
        }

        private static async Task<JObject> PostFormAsync(Uri url, string body)
        {
            using (var filter = new HttpBaseProtocolFilter())
            using (var client = new HttpClient(filter))
            {
                filter.IgnorableServerCertificateErrors.Add(
                    Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new HttpStringContent(
                        body,
                        Windows.Storage.Streams.UnicodeEncoding.Utf8,
                        "application/x-www-form-urlencoded")
                };
                AddHeaders(request);
                var response = await client.SendRequestAsync(request);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return JObject.Parse(content);
            }
        }

        private static async Task<JObject> PostJsonAsync(Uri url, string body)
        {
            using (var filter = new HttpBaseProtocolFilter())
            using (var client = new HttpClient(filter))
            {
                filter.IgnorableServerCertificateErrors.Add(
                    Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new HttpStringContent(
                        body,
                        Windows.Storage.Streams.UnicodeEncoding.Utf8,
                        "application/json;charset=utf-8")
                };
                AddHeaders(request);
                var response = await client.SendRequestAsync(request);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return JObject.Parse(content);
            }
        }

        private static void AddHeaders(HttpRequestMessage request)
        {
            request.Headers.Append("User-Agent", "Mozilla/5.0");
            request.Headers.Append("Referer", "https://www.bilibili.com/");
            var cookies = ApiHelper.GetCookies();
            if (!string.IsNullOrEmpty(cookies))
            {
                request.Headers.Append("Cookie", cookies);
            }
        }

        private static string BuildForm(params string[] values)
        {
            var builder = new StringBuilder();
            for (var index = 0; index + 1 < values.Length; index += 2)
            {
                if (builder.Length > 0)
                {
                    builder.Append('&');
                }

                builder.Append(Uri.EscapeDataString(values[index] ?? string.Empty));
                builder.Append('=');
                builder.Append(Uri.EscapeDataString(values[index + 1] ?? string.Empty));
            }

            return builder.ToString();
        }

        private static string GetCookieValue(string name)
        {
            var cookies = ApiHelper.GetCookies() ?? string.Empty;
            foreach (var part in cookies.Split(';'))
            {
                var pair = part.Trim().Split(new[] { '=' }, 2);
                if (pair.Length == 2 && string.Equals(pair[0], name, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair[1]);
                }
            }

            return string.Empty;
        }

        private static CommandData ParseCommand(byte[] bytes)
        {
            var command = new CommandData();
            foreach (var field in ReadFields(bytes))
            {
                switch (field.Number)
                {
                    case 1:
                        command.Id = ToLong(field.Varint);
                        break;
                    case 2:
                        command.Oid = ToLong(field.Varint);
                        break;
                    case 3:
                        command.Mid = ToLong(field.Varint);
                        break;
                    case 4:
                        command.Command = GetString(field);
                        break;
                    case 5:
                        command.Content = GetString(field); // "关注弹幕"字样的来源
                        break;
                    case 6:
                        command.Progress = (int)field.Varint;
                        break;
                    case 9:
                        command.Extra = GetString(field);
                        break;
                    case 10:
                        command.IdStr = GetString(field);
                        break;
                }
            }

            return command;
        }

        private static InteractiveDanmakuModel CreateModel(CommandData command)
        {
            var normalizedCommand = (command.Command ?? string.Empty).Trim().ToUpperInvariant();
            if (normalizedCommand != "#VOTE#"
                && normalizedCommand != "#GRADE#"
                && normalizedCommand != "#UP#"
                && normalizedCommand != "#LINK#"
                && normalizedCommand != "#ATTENTION#")
            {
                return null;
            }

            JObject extra;
            try
            {
                extra = string.IsNullOrWhiteSpace(command.Extra)
                    ? new JObject()
                    : JObject.Parse(command.Extra);
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("解析互动弹幕扩展数据失败", LogType.INFO, ex);
                return null;
            }

            var item = new InteractiveDanmakuModel
            {
                Id = command.Id,
                IdStr = command.IdStr,
                Oid = command.Oid,
                SenderMid = command.Mid,
                Command = normalizedCommand,
                Progress = Math.Max(0, command.Progress),
                Duration = GetDuration(extra),
                Title = GetString(extra["title"])
                    ?? GetString(extra["question"])
                    ?? GetString(extra["msg"])
                    ?? (command.Content == "关注弹幕" ? "给个三连吧~" : command.Content)
                    ?? string.Empty
            };

            if (normalizedCommand == "#UP#")
            {
                item.Type = InteractiveDanmakuType.Up;
                item.IconUrl = GetString(extra["icon"]);
                if (string.IsNullOrWhiteSpace(item.Title))
                {
                    item.Title = "UP 主互动弹幕";
                }

                return item.SenderMid > 0 ? item : null;
            }

            if (normalizedCommand == "#LINK#")
            {
                item.Type = InteractiveDanmakuType.Link;
                item.RelatedAid = GetLong(extra["aid"]);
                item.RelatedBvid = GetString(extra["bvid"])
                    ?? GetString(extra["bv_id"]);
                item.IconUrl = GetString(extra["icon"]);
                if (string.IsNullOrWhiteSpace(item.Title))
                {
                    item.Title = "关联视频";
                }

                return item.RelatedAid > 0 || !string.IsNullOrWhiteSpace(item.RelatedBvid)
                    ? item
                    : null;
            }

            if (normalizedCommand == "#ATTENTION#")
            {
                item.Type = InteractiveDanmakuType.Attention;
                var attentionType = GetInt(extra["type"]);
                item.AttentionType = attentionType >= 0 && attentionType <= 2
                    ? attentionType
                    : 0;
                item.PositionX = extra["posX"] == null
                    ? GetDouble(extra["pos_x"])
                    : GetDouble(extra["posX"]);
                item.PositionY = extra["posY"] == null
                    ? GetDouble(extra["pos_y"])
                    : GetDouble(extra["posY"]);
                item.IconUrl = GetString(extra["icon"]);
                if (string.IsNullOrWhiteSpace(item.Title))
                {
                    item.Title = "喜欢就关注吧";
                }

                return item.SenderMid > 0 ? item : null;
            }

            if (normalizedCommand == "#GRADE#")
            {
                item.Type = InteractiveDanmakuType.Grade;
                item.GradeId = GetLong(extra["grade_id"]);
                item.Count = GetInt(extra["count"]);
                item.AverageScore = GetDouble(extra["avg_score"]);
                return item.GradeId == 0 ? null : item;
            }

            item.Type = InteractiveDanmakuType.Vote;
            item.VoteId = GetLong(extra["vote_id"]);
            item.VoteType = GetInt(extra["vote_type"]);
            var options = extra["options"] as JArray;
            if (options != null)
            {
                foreach (var token in options)
                {
                    var option = token as JObject;
                    if (option == null)
                    {
                        continue;
                    }

                    var index = GetInt(option["idx"]);
                    var text = GetString(option["desc"])
                        ?? GetString(option["text"])
                        ?? GetString(option["label"]);
                    if (index <= 0 || string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    item.Options.Add(new InteractiveDanmakuOption
                    {
                        Index = index,
                        Text = text,
                        Count = GetInt(option["cnt"]),
                        HasSelfDefinition = GetBool(option["has_self_def"])
                    });
                }
            }

            return item.VoteId == 0 || item.Options.Count == 0 ? null : item;
        }

        private static void RestoreSubmissionState(long aid, long cid, InteractiveDanmakuModel item)
        {
            var submissionId = item.Type == InteractiveDanmakuType.Vote
                ? item.VoteId
                : item.GradeId;
            var key = BuildSubmissionStateKey(aid, cid, item.Type, submissionId);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            int selectedValue;
            lock (submissionStateLock)
            {
                if (!submissionStates.TryGetValue(key, out selectedValue))
                {
                    try
                    {
                        var storedValue = GetSubmissionStateContainer().Values[key];
                        if (storedValue == null)
                        {
                            return;
                        }

                        selectedValue = Convert.ToInt32(storedValue);
                        submissionStates[key] = selectedValue;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.WriteLog("恢复互动弹幕提交状态失败", LogType.INFO, ex);
                        return;
                    }
                }
            }

            if (item.Type == InteractiveDanmakuType.Vote)
            {
                item.SelectedVoteOption = selectedValue;
                item.VoteSubmitted = true;
            }
            else
            {
                item.SelectedGradeScore = Math.Max(1, Math.Min(5, selectedValue));
                item.GradeSubmitted = true;
            }
        }

        private static void RememberSubmissionState(
            long aid,
            long cid,
            InteractiveDanmakuType type,
            long submissionId,
            int selectedValue)
        {
            var key = BuildSubmissionStateKey(aid, cid, type, submissionId);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            lock (submissionStateLock)
            {
                submissionStates[key] = selectedValue;
                try
                {
                    GetSubmissionStateContainer().Values[key] = selectedValue;
                }
                catch (Exception ex)
                {
                    LogHelper.WriteLog("保存互动弹幕提交状态失败", LogType.INFO, ex);
                }
            }
        }

        private static string BuildSubmissionStateKey(
            long aid,
            long cid,
            InteractiveDanmakuType type,
            long submissionId)
        {
            if (aid <= 0 || cid <= 0 || submissionId <= 0)
            {
                return string.Empty;
            }

            return ApiHelper.GetUserId()
                + ":"
                + aid.ToString(CultureInfo.InvariantCulture)
                + ":"
                + cid.ToString(CultureInfo.InvariantCulture)
                + ":"
                + ((int)type).ToString(CultureInfo.InvariantCulture)
                + ":"
                + submissionId.ToString(CultureInfo.InvariantCulture);
        }

        private static ApplicationDataContainer GetSubmissionStateContainer()
        {
            if (submissionStateContainer == null)
            {
                submissionStateContainer = ApplicationData.Current.LocalSettings.CreateContainer(
                    SubmissionStateContainerName,
                    ApplicationDataCreateDisposition.Always);
            }

            return submissionStateContainer;
        }

        private static int GetDuration(JObject extra)
        {
            var duration = GetInt(extra["duration"]);
            if (duration <= 0)
            {
                duration = GetInt(extra["custom_duration"]);
            }
            if (duration <= 0)
            {
                duration = GetInt(extra["summary_duration"]);
            }

            return duration > 0 ? duration : 7000;
        }

        private static string GetString(JToken token)
        {
            return token == null || token.Type == JTokenType.Null
                ? null
                : token.ToString();
        }

        private static long GetLong(JToken token)
        {
            long value;
            return long.TryParse(GetString(token), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : 0;
        }

        private static int GetInt(JToken token)
        {
            int value;
            return int.TryParse(GetString(token), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : 0;
        }

        private static double GetDouble(JToken token)
        {
            double value;
            return double.TryParse(GetString(token), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : 0;
        }

        private static bool GetBool(JToken token)
        {
            bool value;
            return bool.TryParse(GetString(token), out value) && value;
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
                            throw new InvalidOperationException("Protobuf 字段长度无效");
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
                        throw new InvalidOperationException("Protobuf 字段类型无效");
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
                            throw new InvalidOperationException("Protobuf 分组长度无效");
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
                        throw new InvalidOperationException("Protobuf 分组类型无效");
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

            throw new InvalidOperationException("Protobuf 变长整数无效");
        }

        private static void EnsureAvailable(byte[] bytes, int offset, int count)
        {
            if (count < 0 || offset < 0 || offset > bytes.Length - count)
            {
                throw new InvalidOperationException("Protobuf 数据长度无效");
            }
        }

        private sealed class CommandData
        {
            public long Id { get; set; }
            public long Oid { get; set; }
            public long Mid { get; set; }
            public string Command { get; set; }
            public string Content { get; set; }
            public int Progress { get; set; }
            public string Extra { get; set; }
            public string IdStr { get; set; }
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
}
