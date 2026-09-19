namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// 主应用与后台任务共享的 LocalSettings key 常量。
    /// 两端编译期内联 const，防止 key 字符串漂移；Background 通过 csproj 链接本文件。
    /// </summary>
    internal static class SettingKeys
    {
        public const string DTCT = "DTCT";
        public const string DT = "DT";
        public const string FJ = "FJ";
        public const string TsDt = "TsDt";
        //关注动态通知的去重记录，存 dynamic_id 逗号串。与旧的 TsDt(存 aid)格式不兼容，故另起 key
        public const string DynamicNotifiedIds = "DynamicNotifiedIds";
        //access_key 在 Credential Locker 中的资源标识，主应用写、后台任务读
        public const string AccessKeyResource = "BiliBili.UWP.AccessKey";
        public const string AccessKeyUserName = "bili_access_key";
        public const string UserID = "UserID";
        public const string BiliJumpAiEnabled = "BiliJumpAiEnabled";
        public const string BiliJumpAiAutoJump = "BiliJumpAiAutoJump";
        public const string BiliJumpAiProvider = "BiliJumpAiProvider";
        public const string BiliJumpAiApiUrl = "BiliJumpAiApiUrl";
        public const string BiliJumpAiModel = "BiliJumpAiModel";
        public const string BiliJumpAiCacheEnabled = "BiliJumpAiCacheEnabled";
        public const string BiliJumpAiMinFans = "BiliJumpAiMinFans";
    }
}
