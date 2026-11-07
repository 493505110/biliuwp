using System;
using Windows.Security.Credentials;

namespace BiliBili.UWP.Helper
{
    internal static class LoginCredentialVault
    {
        public static LoginCredential Read()
        {
            try
            {
                var password = new PasswordVault().Retrieve(SettingKeys.AccessKeyResource, SettingKeys.AccessKeyUserName).Password;
                return LoginCredential.Parse(password);
            }
            catch (Exception ex) when (ex.HResult == unchecked((int)0x80070490))
            {
                return null;
            }
        }

        public static void Save(LoginCredential credential)
        {
            var vault = new PasswordVault();
            if (string.IsNullOrEmpty(credential.AccessToken))
            {
                try
                {
                    vault.Remove(vault.Retrieve(SettingKeys.AccessKeyResource, SettingKeys.AccessKeyUserName));
                }
                catch (Exception ex) when (ex.HResult == unchecked((int)0x80070490))
                {
                    // 已注销
                }
                return;
            }

            vault.Add(new PasswordCredential(SettingKeys.AccessKeyResource, SettingKeys.AccessKeyUserName, credential.Serialize()));
        }
    }
}
