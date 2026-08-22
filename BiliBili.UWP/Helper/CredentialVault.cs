using System;
using Windows.Security.Credentials;

namespace BiliBili.UWP.Helper
{
    /// <summary>
    /// access_key 凭证保险箱（Credential Locker），替代 LocalSettings 明文存储
    /// </summary>
    public static class CredentialVault
    {
        private const string ResourceName = "BiliBili.UWP.AccessKey";
        private const string UserName = "bili_access_key";
        private const string BiliJumpResourceName = "BiliBili.UWP.BiliJumpAi";
        private const string BiliJumpUserName = "bili_jump_ai_api_key";

        public static string Get()
        {
            try
            {
                var credential = new PasswordVault().Retrieve(ResourceName, UserName);
                return credential?.Password;
            }
            catch (Exception)
            {
                //Retrieve 在凭证不存在时抛异常，按未登录处理
                return null;
            }
        }

        public static void Set(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Remove();
                return;
            }
            var vault = new PasswordVault();
            try
            {
                var credential = vault.Retrieve(ResourceName, UserName);
                if (credential != null)
                {
                    vault.Remove(credential);
                }
            }
            catch (Exception)
            {
                //凭证不存在，直接 Add 即可
            }
            try
            {
                vault.Add(new PasswordCredential(ResourceName, UserName, value));
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("CredentialVault 写入失败", LogType.ERROR, ex);
            }
        }

        public static void Remove()
        {
            var vault = new PasswordVault();
            try
            {
                var credential = vault.Retrieve(ResourceName, UserName);
                if (credential != null)
                {
                    vault.Remove(credential);
                }
            }
            catch (Exception)
            {
                //凭证不存在，无需处理
            }
        }

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
