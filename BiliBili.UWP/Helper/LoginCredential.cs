using Newtonsoft.Json.Linq;
using System;

namespace BiliBili.UWP.Helper
{
    internal sealed class LoginCredential
    {
        public string AccessToken { get; }
        public string RefreshToken { get; }

        public LoginCredential(string accessToken, string refreshToken)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
        }

        public static LoginCredential Parse(string password)
        {
            if (!password.StartsWith("{", StringComparison.Ordinal))
                return new LoginCredential(password, null); // 旧版本仅保存 access_key

            var pair = JObject.Parse(password);
            return new LoginCredential((string)pair["access_token"], (string)pair["refresh_token"] ?? "");
        }

        public string Serialize()
        {
            var pair = new JObject { ["access_token"] = AccessToken, ["refresh_token"] = RefreshToken ?? "" };
            return pair.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
