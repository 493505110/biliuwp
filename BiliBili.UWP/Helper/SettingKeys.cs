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
        public const string BiliJumpAiEnabled = "BiliJumpAiEnabled";
        public const string BiliJumpAiAutoJump = "BiliJumpAiAutoJump";
        public const string BiliJumpAiProvider = "BiliJumpAiProvider";
        public const string BiliJumpAiApiUrl = "BiliJumpAiApiUrl";
        public const string BiliJumpAiModel = "BiliJumpAiModel";
    }
}
