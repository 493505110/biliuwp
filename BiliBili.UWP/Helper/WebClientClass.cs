using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using Windows.Security.ExchangeActiveSyncProvisioning;


namespace BiliBili.UWP
{
    class WebClientClass
    {
        public static async Task<string> GetResults(Uri url)
        {
            HttpBaseProtocolFilter fiter = new HttpBaseProtocolFilter();
            fiter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);
            using (HttpClient hc = new HttpClient(fiter))
            {
                //hc.DefaultRequestHeaders.Add("user-agent", $"Mozilla/5.0 BiliDroid/6.1.0 (bbcallen@gmail.com)");
                hc.DefaultRequestHeaders.Add("user-agent", $"Bilibili UWP Client/3.4.10.0 (atelier39@outlook.com)");
                //hc.DefaultRequestHeaders.Referer = new Uri("http://www.bilibili.com/");
                HttpResponseMessage hr = await hc.GetAsync(url);
                hr.EnsureSuccessStatusCode();
                var encodeResults = await hr.Content.ReadAsBufferAsync();
                var bytes = encodeResults.ToArray();
                string results = Encoding.UTF8.GetString(bytes, 0, bytes.Length);

                //string result = await response.Content.ReadAsStringAsync();
                return results;
            }
        }
        public static async Task<string> GetResults(Uri url,Dictionary<string,string> header)
        {
            HttpBaseProtocolFilter fiter = new HttpBaseProtocolFilter();
            fiter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);
            using (HttpClient hc = new HttpClient(fiter))
            {
                foreach (var item in header)
                {
                    hc.DefaultRequestHeaders.Add(item.Key, item.Value);
                }
                HttpResponseMessage hr = await hc.GetAsync(url);
                hr.EnsureSuccessStatusCode();
                var encodeResults = await hr.Content.ReadAsBufferAsync();
                var bytes = encodeResults.ToArray();
                string results = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
                return results;
            }
        }


        public static async Task<IBuffer> GetBuffer(Uri url)
        {
            HttpBaseProtocolFilter fiter = new HttpBaseProtocolFilter();
            //sid = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 8).ToLower();
            //fiter.CookieManager.SetCookie(new HttpCookie("sid", "bilibili.com", "/") { Value = sid });
            using (HttpClient hc = new HttpClient(fiter))
            {
                HttpResponseMessage hr = await hc.GetAsync(url);

                hr.EnsureSuccessStatusCode();
                IBuffer results = await hr.Content.ReadAsBufferAsync();
                return results;
            }
        }


        static string sid = "";
        public static async Task<string> PostResults(Uri url, string PostContent)
        {
            try
            {
                HttpBaseProtocolFilter fiter = new HttpBaseProtocolFilter();
                fiter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);

                if (url.AbsoluteUri.Contains("oauth2/login")&& sid!="")
                {
                    fiter.CookieManager.SetCookie(new HttpCookie("sid", "bilibili.com", "/") { Value = sid });
                }
                using (HttpClient hc = new HttpClient(fiter))
                {
                    hc.DefaultRequestHeaders.Referer = new Uri("http://www.bilibili.com/");
                    var response = await hc.PostAsync(url, new HttpStringContent(PostContent, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/x-www-form-urlencoded"));
                    response.EnsureSuccessStatusCode();
                 
                    var encodeResults = await response.Content.ReadAsBufferAsync();
                    var bytes = encodeResults.ToArray();
                    string result = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
                    return result;
                }
            }
            catch (Exception)
            {
                return "";
            }
        }
        public static async Task<string> PostResultsJson(Uri url, string PostContent)
        {
            try
            {
                HttpBaseProtocolFilter fiter = new HttpBaseProtocolFilter();
                fiter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);
                using (HttpClient hc = new HttpClient(fiter))
                {
                  
                    var response = await hc.PostAsync(url, new HttpStringContent(PostContent, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json"));
                    response.EnsureSuccessStatusCode();
                    string result = await response.Content.ReadAsStringAsync();
                    return result;
                }
            }
            catch (Exception)
            {
                return "";
            }
        }
        public static async Task<string> PostResultsUtf8(Uri url, string PostContent)
        {
            try
            {
                HttpBaseProtocolFilter fiter = new HttpBaseProtocolFilter();
                fiter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);
                using (HttpClient hc = new HttpClient(fiter))
                {
                    hc.DefaultRequestHeaders.Referer = new Uri("http://www.bilibili.com/");
                    var response = await hc.PostAsync(url, new HttpStringContent(PostContent, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/x-www-form-urlencoded"));
                    response.EnsureSuccessStatusCode();

                    var encodeResults = await response.Content.ReadAsBufferAsync();
                    var bytes = encodeResults.ToArray();
                    string results = Encoding.UTF8.GetString(bytes, 0, bytes.Length);

                    //string result = await response.Content.ReadAsStringAsync();
                    return results;
                }
            }
            catch (Exception)
            {
                return "";
            }
        }
        public static async Task<string> PostResults(Uri url, string PostContent, string Referer)
        {
            try
            {
                using (HttpClient hc = new HttpClient())
                {
                    hc.DefaultRequestHeaders.Referer = new Uri(Referer);
                    var response = await hc.PostAsync(url, new HttpStringContent(PostContent, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/x-www-form-urlencoded"));
                    response.EnsureSuccessStatusCode();
                    string result = await response.Content.ReadAsStringAsync();
                    return result;
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static async Task<string> PostResults(Uri url, string PostContent, string Referer, string Home)
        {
            try
            {
                using (HttpClient hc = new HttpClient())
                {
                    hc.DefaultRequestHeaders.Referer = new Uri(Referer);
                    hc.DefaultRequestHeaders.Host = new Windows.Networking.HostName(Home);
                    var response = await hc.PostAsync(url, new HttpStringContent(PostContent, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/x-www-form-urlencoded"));
                    response.EnsureSuccessStatusCode();
                    string result = await response.Content.ReadAsStringAsync();
                    return result;
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static async Task<string> PostResults(Uri url, StorageFile PostContent, string Referer, string Home)
        {
            try
            {
                using (HttpClient hc = new HttpClient())
                {
                    hc.DefaultRequestHeaders.Referer = new Uri(Referer);
                    hc.DefaultRequestHeaders.Host = new Windows.Networking.HostName(Home);
                    IBuffer buffer = await FileIO.ReadBufferAsync(PostContent);

                    var response = await hc.PostAsync(url, new HttpBufferContent(buffer));
                    response.EnsureSuccessStatusCode();
                    string result = await response.Content.ReadAsStringAsync();
                    return result;
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static async Task<string> PostResults(Uri url, StorageFile PostContent)
        {
            try
            {
                using (HttpClient hc = new HttpClient())
                {

                    IBuffer buffer = await FileIO.ReadBufferAsync(PostContent);

                    var response = await hc.PostAsync(url, new HttpBufferContent(buffer));
                    response.EnsureSuccessStatusCode();
                    string result = await response.Content.ReadAsStringAsync();
                    return result;
                }
            }
            catch (Exception)
            {
                return "";
            }
        }


        public static async Task<string> PostResults(Uri url, IInputStream PostContent, string Referer)
        {
            try
            {
                using (HttpClient hc = new HttpClient())
                {
                    //hc.DefaultRequestHeaders.Add("Content-Disposition", @"form-data; name=""img_file""");
                    //hc.DefaultRequestHeaders.Add("Content-Type", " application/octet-stream");
                    //hc.DefaultRequestHeaders.Add("Content-Transfer-Encoding", " binary");
                    //hc.DefaultRequestHeaders.Host = new Windows.Networking.HostName(Home);
                    hc.DefaultRequestHeaders.Referer = new Uri(Referer);
                    var response = await hc.PostAsync(url, new HttpStreamContent(PostContent));
                    response.EnsureSuccessStatusCode();
                    string result = await response.Content.ReadAsStringAsync();
                    return result;
                }
            }
            catch (Exception)
            {
                return "";
            }
        }


        public static async Task<string> PostResults(Uri url, Stream PostContent)
        {
            try
            {
                using (HttpClient hc = new HttpClient())
                {
                    //hc.DefaultRequestHeaders.Add("Content-Disposition", @"form-data; name=""img_file""");
                    //hc.DefaultRequestHeaders.Add("Content-Type", " application/octet-stream");
                    //hc.DefaultRequestHeaders.Add("Content-Type", "multipart/form-data;");
                    //hc.DefaultRequestHeaders.Add("Content-Length", PostContent.Length.ToString());
                    //hc.DefaultRequestHeaders.Host = new Windows.Networking.HostName(Home);
                    HttpMultipartFormDataContent httpMultipartFormDataContent = new HttpMultipartFormDataContent();
                    httpMultipartFormDataContent.Add(new HttpStreamContent(PostContent.AsInputStream()), "data");

                    //HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
                    //request.Content = httpMultipartFormDataContent;

                    HttpResponseMessage response = await hc.PostAsync(url, httpMultipartFormDataContent);
                    response.EnsureSuccessStatusCode();
                    string result = await response.Content.ReadAsStringAsync();
                    return result;
                }
            }
            catch (Exception)
            {
                return "";
            }
        }


        public static async Task<string> GetResultsUTF8Encode(Uri url, IDictionary<string, string> header=null)
        {
            HttpBaseProtocolFilter fiter = new HttpBaseProtocolFilter();
            fiter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);

            using (HttpClient hc = new HttpClient(fiter))
            {
                if (header != null)
                {
                    foreach (var item in header)
                    {
                        hc.DefaultRequestHeaders.Add(item.Key, item.Value);
                    }
                }
                HttpResponseMessage hr = await hc.GetAsync(url);
                hr.EnsureSuccessStatusCode();
                var encodeResults = await hr.Content.ReadAsBufferAsync();
                var bytes = encodeResults.ToArray();
                string results = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
                return results;
            }

        }


    }


}
