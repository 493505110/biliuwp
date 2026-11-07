using System;
using Windows.Security.Credentials;

namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// 字幕识别服务的 API Key 凭证保险箱
    /// </summary>
    public static class CredentialVault
    {
        private const string BiliJumpResourceName = "BiliBili.UWP.BiliJumpAi";
        private const string BiliJumpUserName = "bili_jump_ai_api_key";

        public static string GetBiliJumpApiKey()
        {
            try
            {
                var credential = new PasswordVault().Retrieve(BiliJumpResourceName, BiliJumpUserName);
                return credential?.Password;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void SetBiliJumpApiKey(string value)
        {
            var vault = new PasswordVault();
            try
            {
                var credential = vault.Retrieve(BiliJumpResourceName, BiliJumpUserName);
                if (credential != null)
                {
                    vault.Remove(credential);
                }
            }
            catch (Exception)
            {
                // 凭证不存在，直接写入。
            }

            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            try
            {
                vault.Add(new PasswordCredential(BiliJumpResourceName, BiliJumpUserName, value));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("BiliJump AI 凭据写入失败", LogType.ERROR, ex);
            }
        }

    }
}
