using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BiliBili.UWP.Api.User
{
    public class LoginAPI
    {
        /// <summary>
        /// 二维码登录获取二维码及AuthCode
        /// </summary>
        /// <param name="mid"></param>
        /// <returns></returns>
        public ApiModel QRLoginAuthCode(string local_id)
        {
            ApiModel api = new ApiModel()
            {
                method = HttpMethod.POST,
                baseUrl = "https://passport.bilibili.com/x/passport-tv-login/qrcode/auth_code",
                body = ApiUtils.MustParameter(ApiUtils.AndroidTVKey, false)+ $"&local_id={local_id}",
            };
            api.body += ApiUtils.GetSign(api.body, ApiUtils.AndroidTVKey);
            return api;
        }

        /// <summary>
        /// 二维码登录轮询
        /// </summary>
        /// <param name="auth_code"></param>
        /// <returns></returns>
        public ApiModel QRLoginPoll(string auth_code, string local_id)
        {
            ApiModel api = new ApiModel()
            {
                method = HttpMethod.POST,
                baseUrl = "https://passport.bilibili.com/x/passport-tv-login/qrcode/poll",
                body = ApiUtils.MustParameter(ApiUtils.AndroidTVKey, false)+ $"&auth_code={auth_code}&guid={Guid.NewGuid().ToString()}&local_id={local_id}",
            };
            api.body += ApiUtils.GetSign(api.body, ApiUtils.AndroidTVKey);
            return api;
        }

        /// <summary>
        /// Web二维码登录获取二维码及qrcode_key
        /// </summary>
        public ApiModel WebQRLoginGenerate()
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://passport.bilibili.com/x/passport-login/web/qrcode/generate",
                parameter = "source=main-fe-header",
                headers = WebHeaders()
            };
        }

        /// <summary>
        /// Web二维码登录轮询。成功响应会向应用Cookie存储写入Web登录Cookie
        /// </summary>
        public ApiModel WebQRLoginPoll(string qrcodeKey)
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://passport.bilibili.com/x/passport-login/web/qrcode/poll",
                parameter = $"qrcode_key={Uri.EscapeDataString(qrcodeKey)}&source=main-fe-header",
                headers = WebHeaders()
            };
        }

        /// <summary>
        /// Web端请求头，passport接口对UA与Referer敏感
        /// </summary>
        private static IDictionary<string, string> WebHeaders()
        {
            return new Dictionary<string, string>()
            {
                { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36 Edg/147.0.0.0" },
                //不加Origin：WinRT对部分请求头有限制，且实测服务端不需要
                { "Referer", "https://passport.bilibili.com/login" }
            };
        }

        /// <summary>
        /// 申请captcha验证码，得到极验gt/challenge与登录token
        /// </summary>
        public ApiModel Captcha()
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://passport.bilibili.com/x/passport-login/captcha",
                parameter = "source=main_web",
                headers = WebHeaders()
            };
        }

        /// <summary>
        /// 获取密码加密用的salt(hash)与RSA公钥，hash有效期约20秒
        /// </summary>
        public ApiModel WebKey()
        {
            return new ApiModel()
            {
                method = HttpMethod.GET,
                baseUrl = "https://passport.bilibili.com/x/passport-login/web/key",
                parameter = "",
                headers = WebHeaders()
            };
        }

        /// <summary>
        /// Web端账密登录，成功后cookie写入应用cookie jar
        /// </summary>
        /// <param name="username">手机号或邮箱</param>
        /// <param name="encryptedPassword">RSA加密并base64后的(hash+密码)</param>
        /// <param name="token">captcha接口返回的token</param>
        /// <param name="challenge">极验challenge</param>
        /// <param name="validate">极验验证结果</param>
        public ApiModel WebPasswordLogin(string username, string encryptedPassword, string token, string challenge, string validate)
        {
            var body = $"username={Uri.EscapeDataString(username)}" +
                $"&password={Uri.EscapeDataString(encryptedPassword)}" +
                $"&keep=0" +
                $"&token={Uri.EscapeDataString(token)}" +
                $"&challenge={Uri.EscapeDataString(challenge)}" +
                $"&validate={Uri.EscapeDataString(validate)}" +
                //seccode固定为validate加上"|jordan"
                $"&seccode={Uri.EscapeDataString(validate + "|jordan")}" +
                $"&go_url={Uri.EscapeDataString("https://www.bilibili.com")}" +
                $"&source=main_web";
            return new ApiModel()
            {
                method = HttpMethod.POST,
                baseUrl = "https://passport.bilibili.com/x/passport-login/web/login",
                parameter = "",
                body = body,
                headers = WebHeaders()
            };
        }

        /// <summary>
        /// 用web cookie确认TV二维码，是把cookie换成access_key的关键一步
        /// （原先的/login/app/third接口已下线，返回code 20000）
        /// </summary>
        /// <param name="auth_code">TV二维码auth_code</param>
        /// <param name="csrf">cookie中的bili_jct</param>
        public ApiModel QRLoginConfirm(string auth_code, string csrf)
        {
            return new ApiModel()
            {
                method = HttpMethod.POST,
                baseUrl = "https://passport.bilibili.com/x/passport-tv-login/h5/qrcode/confirm",
                parameter = "",
                body = $"auth_code={Uri.EscapeDataString(auth_code)}&csrf={Uri.EscapeDataString(csrf ?? "")}&scanning_type=1",
                headers = WebHeaders()
            };
        }
    }
}
