using BiliBili.UWP.Helper;
using BiliBili.UWP.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using Windows.Web.Http.Filters;
using BiliBili.UWP.Modules.AccountModels;
using Newtonsoft.Json;
using Windows.Web.Http;
using BiliBili.UWP.Api.User;
using BiliBili.UWP.Api;

namespace BiliBili.UWP.Modules
{
    public class Account : IModules
    {
        readonly UserCenterAPI userCenterAPI;
        readonly LoginAPI loginAPI;
        string guid = "";
        public Account()
        {
            userCenterAPI = new UserCenterAPI();
            loginAPI = new LoginAPI();
            guid = Guid.NewGuid().ToString();
        }
        public static MyInfoModel myInfo;

        public async Task<ReturnModel<bool>> IsFollowing(string uid)
        {
            try
            {
                var result = await userCenterAPI.Relation(uid).Request();
                if (result.status)
                {
                    var data = await result.GetJson<ApiDataModel<JObject>>();
                    if (data?.success == true)
                    {
                        var attribute = data.data?.Value<int?>("attribute") ?? 0;
                        return new ReturnModel<bool>()
                        {
                            success = true,
                            message = "",
                            data = attribute == 2 || attribute == 6
                        };
                    }
                    return new ReturnModel<bool>()
                    {
                        success = false,
                        message = data?.message ?? "读取关注状态失败"
                    };
                }
                return new ReturnModel<bool>()
                {
                    success = false,
                    message = result.message
                };
            }
            catch (Exception ex)
            {
                return HandelError<bool>(ex);
            }
        }

        public async Task<ReturnModel> Follow(string uid)
        {
            try
            {
                var result = await userCenterAPI.Attention(uid, 1).Request();
                if (result.status)
                {
                    var data = await result.GetJson<ApiDataModel<JObject>>();

                    if (data.success)
                    {
                        return new ReturnModel()
                        {
                            success = true,
                            message = ""
                        };
                    }
                    else
                    {
                        return new ReturnModel()
                        {
                            success = false,
                            message = data.message
                        };
                    }
                }
                else
                {
                    return new ReturnModel()
                    {
                        success = false,
                        message = result.message
                    };
                }

            }
            catch (Exception ex)
            {
                return HandelError(ex);
            }
        }
        public async Task<ReturnModel> UnFollow(string uid)
        {
            try
            {
                var result = await userCenterAPI.Attention(uid,2).Request();
                if (result.status)
                {
                    var data = await result.GetJson<ApiDataModel<JObject>>();

                    if (data.success)
                    {
                        return new ReturnModel()
                        {
                            success = true,
                            message = ""
                        };
                    }
                    else
                    {
                        return new ReturnModel()
                        {
                            success = false,
                            message = data.message
                        };
                    }
                }
                else
                {
                    return new ReturnModel()
                    {
                        success = false,
                        message = result.message
                    };
                }

            }
            catch (Exception ex)
            {
                return HandelError(ex);
            }
        }

        private static async Task<string> EncryptedPassword(string passWord)
        {
            string base64String;
            try
            {
                HttpBaseProtocolFilter httpBaseProtocolFilter = new HttpBaseProtocolFilter();
                httpBaseProtocolFilter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);
                httpBaseProtocolFilter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.Untrusted);
                Windows.Web.Http.HttpClient httpClient = new Windows.Web.Http.HttpClient(httpBaseProtocolFilter);
                string url = "https://passport.bilibili.com/api/oauth2/getKey";
                string content = $"appkey={ApiHelper.AndroidKey.Appkey}&mobi_app=android&platform=android&ts={ApiHelper.GetTimeSpan}";
                content += "&sign=" + ApiHelper.GetSign(content);
                string stringAsync = await WebClientClass.PostResults(new Uri(url), content);
                JObject jObjects = JObject.Parse(stringAsync);
                string str = jObjects["data"]["hash"].ToString();
                string str1 = jObjects["data"]["key"].ToString();
                string str2 = string.Concat(str, passWord);
                string str3 = Regex.Match(str1, "BEGIN PUBLIC KEY-----(?<key>[\\s\\S]+)-----END PUBLIC KEY").Groups["key"].Value.Trim();
                byte[] numArray = Convert.FromBase64String(str3);
                AsymmetricKeyAlgorithmProvider asymmetricKeyAlgorithmProvider = AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaPkcs1);
                CryptographicKey cryptographicKey = asymmetricKeyAlgorithmProvider.ImportPublicKey(WindowsRuntimeBufferExtensions.AsBuffer(numArray), 0);
                IBuffer buffer = CryptographicEngine.Encrypt(cryptographicKey, WindowsRuntimeBufferExtensions.AsBuffer(Encoding.UTF8.GetBytes(str2)), null);
                base64String = Convert.ToBase64String(WindowsRuntimeBufferExtensions.ToArray(buffer));
            }
            catch (Exception)
            {
                throw; // 加密失败不降级为明文密码
            }
            return base64String;
        }
        /// <summary>
        /// 登录V3版本，由于Edge未能很好支持Webp格式图片，会出现无法显示拼图验证码问题
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="captcha">验证码</param>
        /// <returns></returns>
        public async Task<LoginCallbackModel> LoginV3(string username, string password)
        {
            try
            {
                string url = "https://passport.bilibili.com/api/v3/oauth2/login";
                var pwd = Uri.EscapeDataString(await EncryptedPassword(password));

                string data = $"username={Uri.EscapeDataString(username)}&password={pwd}&gee_type=10&appkey={ApiHelper.AndroidKey.Appkey}&mobi_app=android&platform=android&ts={ApiHelper.GetTimeSpan}";
                data += "&sign=" + ApiHelper.GetSign(data);
                var results = await WebClientClass.PostResults(new Uri(url), data);
                var m = JsonConvert.DeserializeObject<AccountLoginModel>(results);
                if (m.code == 0)
                {
                    if (m.data.status == 0)
                    {
                        SettingHelper.Set_Access_key(m.data.token_info.access_token);
                        SettingHelper.Set_Refresh_Token(m.data.token_info.refresh_token);
                        SettingHelper.Set_LoginExpires(DateTime.Now.AddSeconds(m.data.token_info.expires_in));
                        SettingHelper.Set_UserID(m.data.token_info.mid);
                        //foreach (var item in m.data.sso)
                        //{
                        await SSO(m.data.token_info.access_token);
                        //}
                        MessageCenter.SendLogined();
                        return new LoginCallbackModel()
                        {
                            status = LoginStatus.Success,
                            message = "登录成功"
                        };
                    }
                    if (m.data.status == 1)
                    {
                        return new LoginCallbackModel()
                        {
                            status = LoginStatus.NeedValidate,
                            message = "本次登录需要安全验证",
                            url = m.data.url
                        };
                    }

                    return new LoginCallbackModel()
                    {
                        status = LoginStatus.Fail,
                        message = m.message
                    };
                }
                else if (m.code == -105)
                {
                    return new LoginCallbackModel()
                    {
                        status = LoginStatus.NeedCaptcha,
                        url = m.data.url,
                        message = "登录需要验证码"
                    };
                }
                else
                {
                    return new LoginCallbackModel()
                    {
                        status = LoginStatus.Fail,
                        message = m.message
                    };
                }
            }
            catch (Exception ex)
            {
                return new LoginCallbackModel()
                {
                    status = LoginStatus.Error,
                    message = "登录出现小问题,请重试"
                };
            }

        }


        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="captcha">验证码</param>
        /// <returns></returns>
        public async Task<LoginCallbackModel> LoginV2(string username, string password, string captcha = null)
        {
            try
            {
                string url = "https://passport.bilibili.com/api/oauth2/login";
                string data = $"appkey={ApiHelper.AndroidKey.Appkey}&build={ApiHelper.build}&mobi_app=android&password={Uri.EscapeDataString(await EncryptedPassword(password))}&platform=android&ts={ApiHelper.GetTimeSpan}&username={Uri.EscapeDataString(username)}";
                if (!string.IsNullOrEmpty(captcha))
                {
                    data += "&captcha=" + captcha;
                }
                data += "&sign=" + ApiHelper.GetSign(data);
                var results = await WebClientClass.PostResults(new Uri(url), data);
                var m = JsonConvert.DeserializeObject<AccountLoginModel>(results);
                if (m.code == 0)
                {

                    SettingHelper.Set_Access_key(m.data.access_token);
                    SettingHelper.Set_Refresh_Token(m.data.refresh_token);
                    SettingHelper.Set_LoginExpires(DateTime.Now.AddSeconds(m.data.expires_in));
                    SettingHelper.Set_UserID(m.data.mid);
                    //foreach (var item in m.data.sso)
                    //{
                    await SSO(m.data.access_token);
                    //}
                    MessageCenter.SendLogined();
                    return new LoginCallbackModel()
                    {
                        status = LoginStatus.Success,
                        message = "登录成功"
                    };
                }
                else if (m.code == -2100)
                {
                    return new LoginCallbackModel()
                    {
                        status = LoginStatus.NeedValidate,
                        url = m.url,
                        message = "登录需要验证"
                    };
                }
                else if (m.code == -105)
                {
                    return new LoginCallbackModel()
                    {
                        status = LoginStatus.NeedCaptcha,
                        message = "登录需要验证码"
                    };
                }
                else
                {
                    return new LoginCallbackModel()
                    {
                        status = LoginStatus.Fail,
                        message = m.message
                    };
                }
            }
            catch (Exception ex)
            {
                return new LoginCallbackModel()
                {
                    status = LoginStatus.Error,
                    message = "登录出现小问题,请重试"
                };
            }

        }

        public async Task SetLoginSuccess(string access_token, string mid)
        {
            SettingHelper.Set_Access_key(access_token);
            // WebView2 授权回跳仅提供 access_key,无 refresh_token,不得用 access_token 冒充
            SettingHelper.Set_LoginExpires(DateTime.Now.AddSeconds(7200));
            SettingHelper.Set_UserID(long.Parse(mid));
            await SSO(access_token);
            MessageCenter.SendLogined();
        }

        /// <summary>
        /// SSO，将accesskey转为cookie
        /// </summary>
        /// <param name="domain">域</param>
        /// <param name="access_key">access token</param>
        /// <returns></returns>
        public async Task SSO(string access_key)
        {
            try
            {
                //var url = $"{domain}?access_key={access_key}&appkey={ApiHelper.AndroidKey.Appkey}&build={ApiHelper.build}&mobi_app=android&platform=android&ts={ApiHelper.GetTimeSpan}";
                //url += "&sign=" + ApiHelper.GetSign(url);

                var url = $"https://passport.bilibili.com/api/login/sso?access_key={access_key}&appkey={ApiHelper.AndroidKey.Appkey}&build={ApiHelper.build}&gourl=https%3A%2F%2Faccount.bilibili.com%2Faccount%2Fhome&mobi_app=android&platform=android&ts={ApiHelper.GetTimeSpan}";
                url += "&sign=" + ApiHelper.GetSign(url);

                var content = await WebClientClass.GetResults(new Uri(url));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 刷新
        /// </summary>
        /// <param name="access_key">access token</param>
        /// <param name="refresh_token">access token</param>
        /// <returns></returns>
        public async Task<ReturnModel> RefreshToken(string access_key, string refresh_token)
        {
            try
            {
                var url = "https://passport.bilibili.com/api/oauth2/refreshToken";
                var data = $"access_token={access_key}&refresh_token={refresh_token}&appkey={ApiHelper.AndroidKey.Appkey}&ts={ApiHelper.GetTimeSpan}";
                data += "&sign=" + ApiHelper.GetSign(data);
                var content = await WebClientClass.PostResults(new Uri(url), data);
                var obj = JObject.Parse(content);
                if (obj["code"].ToInt32() == 0)
                {
                    var m = JsonConvert.DeserializeObject<Token_info>(obj["data"].ToString());
                    SettingHelper.Set_Access_key(m.access_token);
                    SettingHelper.Set_Refresh_Token(m.refresh_token);
                    SettingHelper.Set_LoginExpires(DateTime.Now.AddSeconds(m.expires_in));
                    SettingHelper.Set_UserID(m.mid);
                    List<string> sso = new List<string>() {
                        "https://passport.bilibili.com/api/v2/sso",
                        "https://passport.biligame.com/api/v2/sso",
                        "https://passport.im9.com/api/v2/sso"
                    };

                    //foreach (var item in sso)
                    //{

                    //}
                    await SSO(m.access_token);
                    MessageCenter.SendLogined();
                    return new ReturnModel()
                    {
                        success = true,
                        message = "刷新成功"
                    };
                }
                else
                {
                    return new ReturnModel()
                    {
                        success = false,
                        message = "刷新Token失败,请重新登录"
                    };
                }
            }
            catch (Exception)
            {

                return new ReturnModel()
                {
                    success = false,
                    message = "刷新Token失败，请重新登录"
                };
            }
        }

        /// <summary>
        /// 检查登录状态
        /// </summary>
        /// <param name="access_key"></param>
        /// <returns></returns>
        public async Task<ReturnModel> CheckLoginState(string access_key)
        {
            try
            {
                var url = $"https://passport.bilibili.com/api/oauth2/info?access_token={access_key}&appkey={ApiHelper.AndroidKey.Appkey}&ts={ApiHelper.GetTimeSpan}";
                url += "&sign=" + ApiHelper.GetSign(url);
                var content = await WebClientClass.GetResults(new Uri(url));
                var obj = JObject.Parse(content);
                if (obj["code"].ToInt32() == 0)
                {
                    return new ReturnModel()
                    {
                        success = true,
                        message = "检查状态成功"
                    };
                }
                else
                {
                    return new ReturnModel()
                    {
                        success = false,
                        message = "检查状态失败"
                    };
                }
            }
            catch (Exception)
            {
                return new ReturnModel()
                {
                    success = true,
                    message = "检查状态失败"
                };
            }
        }

        /// <summary>
        /// 安全验证后保存状态
        /// </summary>
        /// <param name="access_key"></param>
        /// <param name="refresh_token"></param>
        /// <param name="expires"></param>
        /// <param name="userid"></param>
        /// <returns></returns>
        public async Task<ReturnModel> CheckAgainLogin(string access_key, string refresh_token, int expires, long userid)
        {
            try
            {
                SettingHelper.Set_Access_key(access_key);
                SettingHelper.Set_Refresh_Token(refresh_token);
                SettingHelper.Set_LoginExpires(DateTime.Now.AddSeconds(expires));
                SettingHelper.Set_UserID(userid);
                List<string> sso = new List<string>() {
                        "https://passport.bilibili.com/api/v2/sso",
                        "https://passport.biligame.com/api/v2/sso",
                        "https://passport.im9.com/api/v2/sso"
                    };

                //foreach (var item in sso)
                //{
                await SSO(access_key);
                //}
                MessageCenter.SendLogined();
                return new ReturnModel()
                {
                    success = true,
                    message = "登录成功"
                };

            }
            catch (Exception ex)
            {

                return new ReturnModel()
                {
                    success = false,
                    message = "登录失败"
                };
            }
        }
        /// <summary>
        /// 读取我的信息
        /// </summary>
        /// <returns></returns>
        public async Task<ReturnModel<MyInfoModel>> GetMyInfo()
        {
            try
            {
                var url = $"https://app.bilibili.com/x/v2/account/myinfo?access_key={ApiHelper.access_key}&appkey={ApiHelper.AndroidKey.Appkey}&build={ApiHelper.build}&mobi_app=android&platform=android&ts={ApiHelper.GetTimeSpan}";
                url += "&sign=" + ApiHelper.GetSign(url);
                var str = await WebClientClass.GetResults(new Uri(url));
                var m = str.ToDynamicJObject();
                if (m.code == 0)
                {
                    var data = JsonConvert.DeserializeObject<MyInfoModel>(m.json["data"].ToString());
                    myInfo = data;

                    return new ReturnModel<MyInfoModel>()
                    {
                        success = true,
                        data = data
                    };
                }
                else
                {
                    return new ReturnModel<MyInfoModel>()
                    {
                        success = false,
                        message = m.message
                    };
                }

            }
            catch (Exception ex)
            {

                return HandelError<MyInfoModel>(ex);
            }
        }

        /// <summary>
        /// 授权Biliplus
        /// </summary>
        /// <returns></returns>
        public static async Task<string> AuthBiliPlus()
        {
            try
            {
                if (!ApiHelper.IsLogin())
                {
                    return "";
                }
                var url = new Uri($"https://www.biliplus.com/login?act=savekey&mid={SettingHelper.Get_UserID()}&access_key={ApiHelper.access_key}&expire=");
                using (HttpClient httpClient = new HttpClient())
                {
                    var rq = await httpClient.GetAsync(url);
                    var setCookie = rq.Headers["set-cookie"];
                    StringBuilder stringBuilder = new StringBuilder();
                    var matches = Regex.Matches(setCookie, "(.*?)=(.*?); ", RegexOptions.Singleline);
                    foreach (Match match in matches)
                    {
                        var key = match.Groups[1].Value.Replace("HttpOnly, ", "");
                        var value = match.Groups[2].Value;
                        if (key != "expires" && key != "Max-Age" && key != "path" && key != "domain")
                        {
                            stringBuilder.Append(match.Groups[0].Value.Replace("HttpOnly, ", ""));
                        }
                    }
                    SettingHelper.Set_BiliplusCookie(stringBuilder.ToString());
                    return stringBuilder.ToString();
                }
            }
            catch (Exception)
            {

                return "";
            }

        }
        /// <summary>
        /// 申请captcha验证码，拿到极验gt/challenge与登录token
        /// </summary>
        public async Task<ReturnModel<CaptchaInfoModel>> GetCaptchaInfo()
        {
            try
            {
                var result = await loginAPI.Captcha().Request();
                if (!result.status)
                {
                    return new ReturnModel<CaptchaInfoModel>() { success = false, message = result.message };
                }
                var data = await result.GetData<CaptchaInfoModel>();
                if (data == null || !data.success || data.data == null || data.data.geetest == null)
                {
                    return new ReturnModel<CaptchaInfoModel>()
                    {
                        success = false,
                        message = data == null ? "读取验证码失败" : data.message
                    };
                }
                return new ReturnModel<CaptchaInfoModel>() { success = true, data = data.data };
            }
            catch (Exception ex)
            {
                return HandelError<CaptchaInfoModel>(ex);
            }
        }

        /// <summary>
        /// 用Web端公钥加密密码。与旧的EncryptedPassword不同，失败时抛异常而不是回落到明文
        /// </summary>
        private async Task<string> EncryptedPasswordWeb(string password)
        {
            var result = await loginAPI.WebKey().Request();
            if (!result.status)
            {
                throw new Exception("获取登录密钥失败：" + result.message);
            }
            var obj = result.GetJObject();
            if (obj == null || obj["code"].ToInt32() != 0 || obj["data"] == null)
            {
                throw new Exception("获取登录密钥失败");
            }
            //hash为salt，需拼在密码前面，有效期约20秒，所以取key后要立刻登录
            var hash = obj["data"]["hash"].ToString();
            var pem = obj["data"]["key"].ToString();
            var keyBody = Regex.Match(pem, "BEGIN PUBLIC KEY-----(?<key>[\\s\\S]+)-----END PUBLIC KEY").Groups["key"].Value.Trim();
            if (keyBody == "")
            {
                throw new Exception("登录密钥格式异常");
            }
            var keyBytes = Convert.FromBase64String(keyBody);
            var provider = AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaPkcs1);
            var cryptographicKey = provider.ImportPublicKey(keyBytes.AsBuffer(), 0);
            var buffer = CryptographicEngine.Encrypt(cryptographicKey, Encoding.UTF8.GetBytes(hash + password).AsBuffer(), null);
            return Convert.ToBase64String(buffer.ToArray());
        }

        /// <summary>
        /// Web端账密登录。需先完成极验，成功后把cookie换成access_key
        /// </summary>
        /// <param name="username">手机号或邮箱</param>
        /// <param name="password">明文密码</param>
        /// <param name="token">captcha接口的token</param>
        /// <param name="challenge">极验challenge</param>
        /// <param name="validate">极验validate</param>
        public async Task<LoginCallbackModel> WebPasswordLogin(string username, string password, string token, string challenge, string validate)
        {
            try
            {
                var pwd = await EncryptedPasswordWeb(password);
                var result = await loginAPI.WebPasswordLogin(username, pwd, token, challenge, validate).Request();
                if (!result.status)
                {
                    return new LoginCallbackModel() { status = LoginStatus.Fail, message = result.message };
                }
                var obj = result.GetJObject();
                if (obj == null)
                {
                    return new LoginCallbackModel() { status = LoginStatus.Fail, message = "登录返回内容异常" };
                }
                var code = obj["code"].ToInt32();
                if (code == 0)
                {
                    var status = obj["data"]?["status"] == null ? 0 : obj["data"]["status"].ToInt32();
                    if (status == 0)
                    {
                        //cookie已写入应用cookie jar，换成App用的access_key
                        return await CookieToAccessKey();
                    }
                    //status非0表示需要安全验证(如异地登录要验证手机号)，url里带tmp_token
                    return new LoginCallbackModel()
                    {
                        status = LoginStatus.NeedValidate,
                        message = "本次登录需要安全验证",
                        url = obj["data"]?["url"]?.ToString()
                    };
                }
                return new LoginCallbackModel()
                {
                    status = code == -105 ? LoginStatus.NeedCaptcha : LoginStatus.Fail,
                    message = WebLoginCodeToMessage(code, obj["message"]?.ToString())
                };
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("Web账密登录失败", LogType.ERROR, ex);
                return new LoginCallbackModel() { status = LoginStatus.Error, message = "登录失败：" + ex.Message };
            }
        }

        private static string WebLoginCodeToMessage(int code, string message)
        {
            switch (code)
            {
                case -105: return "验证码错误，请重新验证";
                case -629: return "账号或密码错误";
                case -653:
                case -2001: return "登录参数缺失，请重试";
                case -662: return "登录超时，请重试";
                case 2400: return "登录密钥错误，请重试";
                case 2406: return "极验服务出错，请重试";
                case 86000: return "密码加密失败，请重试";
                default: return string.IsNullOrEmpty(message) ? ("登录失败，代码：" + code) : message;
            }
        }

        /// <summary>
        /// 把已有的web cookie换成App用的access_key。
        /// 走TV二维码：申请auth_code → 用cookie确认 → 轮询取token。
        /// 原先用的/login/app/third接口已下线(code 20000)，故改用此路径
        /// </summary>
        public Task<LoginCallbackModel> CookieToAccessKey()
        {
            return CookieToAccessKey(CancellationToken.None);
        }

        private async Task<LoginCallbackModel> CookieToAccessKey(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var csrf = GetCookieValue("bili_jct");
            var authResult = await GetQRAuthInfo();
            cancellationToken.ThrowIfCancellationRequested();
            if (!authResult.success)
            {
                return new LoginCallbackModel() { status = LoginStatus.Fail, message = "获取授权码失败：" + authResult.message };
            }
            var confirm = await loginAPI.QRLoginConfirm(authResult.data.auth_code, csrf).Request();
            if (!confirm.status)
            {
                return new LoginCallbackModel() { status = LoginStatus.Fail, message = confirm.message };
            }
            var obj = confirm.GetJObject();
            cancellationToken.ThrowIfCancellationRequested();
            if (obj == null || obj["code"].ToInt32() != 0)
            {
                return new LoginCallbackModel()
                {
                    status = LoginStatus.Fail,
                    message = obj == null ? "确认授权失败" : ("确认授权失败：" + obj["message"]?.ToString())
                };
            }
            //确认后轮询取token。服务端状态可能有延迟，重试几次
            ReturnModel<Token_info> tokenResult = null;
            for (int i = 0; i < 5; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                tokenResult = await PollQRTokenInfo(authResult.data.auth_code);
                if (tokenResult.success)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SaveTokenInfo(tokenResult.data);
                    MessageCenter.SendLogined();
                    return new LoginCallbackModel() { status = LoginStatus.Success, message = "登录成功" };
                }
                await Task.Delay(800, cancellationToken);
            }
            return new LoginCallbackModel()
            {
                status = LoginStatus.Fail,
                message = tokenResult == null || string.IsNullOrEmpty(tokenResult.message)
                    ? "获取登录凭证失败"
                    : tokenResult.message
            };
        }

        /// <summary>
        /// 从应用cookie jar里读取指定cookie。WebView与HttpClient共用该jar
        /// </summary>
        public static string GetCookieValue(string name)
        {
            try
            {
                var filter = new HttpBaseProtocolFilter();
                var cookies = filter.CookieManager.GetCookies(new Uri("https://www.bilibili.com/"));
                foreach (var item in cookies)
                {
                    if (item.Name == name)
                    {
                        return item.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("读取cookie失败", LogType.ERROR, ex);
            }
            return "";
        }

        /// <summary>
        /// 获取二维码登录信息
        /// </summary>
        /// <returns></returns>
        public async Task<ReturnModel<QRAuthInfo>> GetQRAuthInfo()
        {
            try
            {
                var result =await loginAPI.QRLoginAuthCode(guid).Request();
                if (result.status)
                {
                    var data =await result.GetData<QRAuthInfo>();
                    if (data.success)
                    {
                        return new ReturnModel<QRAuthInfo>()
                        {
                            success=true,
                            data= data.data
                        };
                    }
                    else
                    {
                        return new ReturnModel<QRAuthInfo>()
                        {
                            success = false,
                            message = data.message
                        };

                    }
                }
                else
                {
                    return new ReturnModel<QRAuthInfo>()
                    {
                        success = false,
                        message = result.message
                    };
                }
            }
            catch (Exception ex)
            {
                return HandelError<QRAuthInfo>(ex);
            }
        }

        /// <summary>
        /// 获取Web二维码登录信息。此流程会在扫码成功后建立Web Cookie登录态
        /// </summary>
        public async Task<ReturnModel<QRAuthInfo>> GetWebQRAuthInfo()
        {
            try
            {
                var result = await loginAPI.WebQRLoginGenerate().Request();
                if (!result.status)
                {
                    return new ReturnModel<QRAuthInfo>() { success = false, message = result.message };
                }

                var data = await result.GetData<QRAuthInfo>();
                if (data == null || !data.success || data.data == null ||
                    string.IsNullOrEmpty(data.data.url) || string.IsNullOrEmpty(data.data.qrcode_key))
                {
                    return new ReturnModel<QRAuthInfo>()
                    {
                        success = false,
                        message = data == null ? "读取二维码失败" : data.message
                    };
                }
                return new ReturnModel<QRAuthInfo>() { success = true, data = data.data };
            }
            catch (Exception ex)
            {
                return HandelError<QRAuthInfo>(ex);
            }
        }

        /// <summary>
        /// 轮询Web二维码；Web Cookie与App token都建立后才返回成功
        /// </summary>
        public Task<LoginCallbackModel> PollWebQRAuthInfo(string qrcodeKey)
        {
            return PollWebQRAuthInfo(qrcodeKey, CancellationToken.None);
        }

        public async Task<LoginCallbackModel> PollWebQRAuthInfo(string qrcodeKey, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await loginAPI.WebQRLoginPoll(qrcodeKey).Request();
                cancellationToken.ThrowIfCancellationRequested();
                if (!result.status)
                {
                    return new LoginCallbackModel() { status = LoginStatus.Fail, message = result.message };
                }

                var obj = result.GetJObject();
                if (obj == null || obj["code"].ToInt32() != 0 || obj["data"] == null)
                {
                    return new LoginCallbackModel()
                    {
                        status = LoginStatus.Fail,
                        message = obj == null ? "二维码轮询返回异常" : obj["message"]?.ToString()
                    };
                }

                var data = obj["data"];
                var code = data["code"].ToInt32();
                if (code == 86101 || code == 86090)
                {
                    return new LoginCallbackModel() { status = LoginStatus.Fail, message = data["message"]?.ToString() };
                }
                if (code == 86038)
                {
                    return new LoginCallbackModel() { status = LoginStatus.Error, message = "二维码已失效，请刷新后重试" };
                }
                if (code != 0)
                {
                    return new LoginCallbackModel()
                    {
                        status = LoginStatus.Error,
                        message = string.IsNullOrEmpty(data["message"]?.ToString()) ? $"扫码登录失败，代码：{code}" : data["message"].ToString()
                    };
                }

                //始终处理本次扫码的成功回跳，避免沿用Cookie存储中的旧账号状态。
                if (Uri.TryCreate(data["url"]?.ToString(), UriKind.Absolute, out var crossDomainUri))
                {
                    await WebClientClass.GetResults(crossDomainUri);
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (!HasWebLoginCookies())
                {
                    return new LoginCallbackModel()
                    {
                        status = LoginStatus.Error,
                        message = "扫码成功，但同步网页登录状态失败，请刷新二维码重试"
                    };
                }

                var tokenResult = await CookieToAccessKey(cancellationToken);
                if (tokenResult.status != LoginStatus.Success)
                {
                    tokenResult.status = LoginStatus.Error;
                    tokenResult.message = "网页已登录，但同步客户端登录状态失败：" + tokenResult.message;
                }
                return tokenResult;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.WriteLog("Web二维码登录失败", LogType.ERROR, ex);
                return new LoginCallbackModel() { status = LoginStatus.Error, message = "扫码登录失败：" + ex.Message };
            }
        }

        private static bool HasWebLoginCookies()
        {
            return !string.IsNullOrEmpty(GetCookieValue("SESSDATA")) &&
                !string.IsNullOrEmpty(GetCookieValue("DedeUserID")) &&
                !string.IsNullOrEmpty(GetCookieValue("bili_jct"));
        }

        private static void SaveTokenInfo(Token_info token)
        {
            SettingHelper.Set_Access_key(token.access_token);
            SettingHelper.Set_Refresh_Token(token.refresh_token);
            SettingHelper.Set_LoginExpires(DateTime.Now.AddSeconds(token.expires_in));
            SettingHelper.Set_UserID(token.mid);
        }

        private async Task<ReturnModel<Token_info>> PollQRTokenInfo(string authCode)
        {
            try
            {
                var result = await loginAPI.QRLoginPoll(authCode, guid).Request();
                if (!result.status)
                {
                    return new ReturnModel<Token_info>() { success = false, message = result.message };
                }
                var data = await result.GetData<Token_info>();
                if (data == null || !data.success || data.data == null)
                {
                    return new ReturnModel<Token_info>()
                    {
                        success = false,
                        message = data == null ? "二维码轮询返回异常" : data.message
                    };
                }
                return new ReturnModel<Token_info>() { success = true, data = data.data };
            }
            catch (Exception ex)
            {
                return new ReturnModel<Token_info>() { success = false, message = ex.Message };
            }
        }

        /// <summary>
        /// 轮询二维码扫描信息
        /// </summary>
        /// <returns></returns>
        public Task<LoginCallbackModel> PollQRAuthInfo(string auth_code)
        {
            return PollQRAuthInfo(auth_code, CancellationToken.None);
        }

        public async Task<LoginCallbackModel> PollQRAuthInfo(string auth_code, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tokenResult = await PollQRTokenInfo(auth_code);
            cancellationToken.ThrowIfCancellationRequested();
            if (!tokenResult.success)
            {
                return new LoginCallbackModel() { status = LoginStatus.Fail, message = tokenResult.message };
            }
            await SSO(tokenResult.data.access_token);
            cancellationToken.ThrowIfCancellationRequested();
            SaveTokenInfo(tokenResult.data);
            MessageCenter.SendLogined();
            return new LoginCallbackModel() { status = LoginStatus.Success, message = "" };
        }
    }
    public enum LoginStatus
    {
        /// <summary>
        /// 登录成功
        /// </summary>
        Success,
        /// <summary>
        /// 登录失败
        /// </summary>
        Fail,
        /// <summary>
        /// 登录错误
        /// </summary>
        Error,
        /// <summary>
        /// 登录需要验证码
        /// </summary>
        NeedCaptcha,
        /// <summary>
        /// 需要安全认证
        /// </summary>
        NeedValidate
    }
    namespace AccountModels
    {
        public class Token_info
        {
            /// <summary>
            /// Mid
            /// </summary>
            public long mid { get; set; }
            /// <summary>
            /// ac4dd9f599aeccd54e25f01ef1b222cc
            /// </summary>
            public string access_token { get; set; }
            /// <summary>
            /// 9f6632f1d5e0e2cd2373b488546d71da
            /// </summary>
            public string refresh_token { get; set; }
            /// <summary>
            /// Expires_in
            /// </summary>
            public int expires_in { get; set; }
        }

        /// <summary>
        /// captcha接口返回的验证码信息
        /// </summary>
        public class CaptchaInfoModel
        {
            /// <summary>
            /// 验证方式，目前只有geetest
            /// </summary>
            public string type { get; set; }
            /// <summary>
            /// 登录token，与captcha无关，登录接口要用
            /// </summary>
            public string token { get; set; }
            /// <summary>
            /// 极验参数
            /// </summary>
            public GeetestDataModel geetest { get; set; }
        }

        public class GeetestDataModel
        {
            /// <summary>
            /// 极验id，一般固定
            /// </summary>
            public string gt { get; set; }
            /// <summary>
            /// 极验KEY，每次请求都不同
            /// </summary>
            public string challenge { get; set; }
        }

        /// <summary>
        /// 极验验证完成后的结果，由geetest.html回传
        /// </summary>
        public class GeetestValidateModel
        {
            public string validate { get; set; }
            public string seccode { get; set; }
        }

        public class Cookies
        {
            /// <summary>
            /// bili_jct
            /// </summary>
            public string name { get; set; }
            /// <summary>
            /// 94d8d5b4fa1223a32f236ccc2012ba17
            /// </summary>
            public string value { get; set; }
            /// <summary>
            /// Http_only
            /// </summary>
            public int http_only { get; set; }
            /// <summary>
            /// Expires
            /// </summary>
            public int expires { get; set; }
        }

        public class Cookie_info
        {
            /// <summary>
            /// Cookies
            /// </summary>
            public List<Cookies> cookies { get; set; }
            /// <summary>
            /// Domains
            /// </summary>
            public List<string> domains { get; set; }
        }

        public class LoginDataModel
        {
            /// <summary>
            /// Status
            /// </summary>
            public int status { get; set; }
            /// <summary>
            /// Token_info
            /// </summary>
            public Token_info token_info { get; set; }
            /// <summary>
            /// Cookie_info
            /// </summary>
            public Cookie_info cookie_info { get; set; }
            /// <summary>
            /// Sso
            /// </summary>
            public List<string> sso { get; set; }

            public string url { get; set; }

            /// <summary>
            /// Mid
            /// </summary>
            public long mid { get; set; }
            /// <summary>
            /// ac4dd9f599aeccd54e25f01ef1b222cc
            /// </summary>
            public string access_token { get; set; }
            /// <summary>
            /// 9f6632f1d5e0e2cd2373b488546d71da
            /// </summary>
            public string refresh_token { get; set; }
            /// <summary>
            /// Expires_in
            /// </summary>
            public int expires_in { get; set; }

        }

        public class AccountLoginModel
        {
            /// <summary>
            /// Ts
            /// </summary>
            public int ts { get; set; }
            /// <summary>
            /// Code
            /// </summary>
            public int code { get; set; }
            /// <summary>
            /// Data
            /// </summary>
            public LoginDataModel data { get; set; }

            public string url { get; set; }
            public string message { get; set; }
        }


        public class LoginCallbackModel
        {
            public LoginStatus status { get; set; }
            public string message { get; set; }
            public string url { get; set; }
        }



        public class Vip
        {
            /// <summary>
            /// Type
            /// </summary>
            public int type { get; set; }
            /// <summary>
            /// Status
            /// </summary>
            public int status { get; set; }
            /// <summary>
            /// Due_date
            /// </summary>
            public string due_date { get; set; }
        }

        public class Official
        {
            /// <summary>
            /// Role
            /// </summary>
            public int role { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string title { get; set; }
            /// <summary>
            /// 
            /// </summary>
            public string desc { get; set; }
        }

        public class MyInfoModel
        {
            /// <summary>
            /// Mid
            /// </summary>
            public int mid { get; set; }
            /// <summary>
            /// xiaoyaocz
            /// </summary>
            public string name { get; set; }
            /// <summary>
            /// 死宅，半个程序猿.....
            /// </summary>
            public string sign { get; set; }
            /// <summary>
            /// Coins
            /// </summary>
            public string coins { get; set; }
            /// <summary>
            /// 1997-09-21
            /// </summary>
            public DateTime birthday { get; set; }
            /// <summary>
            /// http://i1.hdslb.com/bfs/face/3e323499026ad0019be48dcd76f8e03199bd606c.jpg
            /// </summary>
            private string _face;

            public string face
            {
                get
                {
                    return _face + "@100w.jpg";
                }
                set { _face = value; }
            }

            /// <summary>
            /// Sex
            /// </summary>
            public int sex { get; set; }
            /// <summary>
            /// Level
            /// </summary>
            public int level { get; set; }
            /// <summary>
            /// Rank
            /// </summary>
            public int rank { get; set; }
            /// <summary>
            /// Silence
            /// </summary>
            public int silence { get; set; }
            /// <summary>
            /// Vip
            /// </summary>
            public Vip vip { get; set; }
            /// <summary>
            /// Email_status
            /// </summary>
            public int email_status { get; set; }
            /// <summary>
            /// Tel_status
            /// </summary>
            public int tel_status { get; set; }
            /// <summary>
            /// Official
            /// </summary>
            public Official official { get; set; }


            public string Sex
            {
                get
                {
                    switch (sex)
                    {
                        case 0:
                            return "保密";
                        case 1:
                            return "男";
                        case 2:
                            return "女";
                        default:
                            return "保密";
                    }
                }
            }
        }


        public class QRAuthInfo
        {
            public string url { get; set; }
            public string auth_code { get; set; }
            public string qrcode_key { get; set; }
        }
      
    }
}
