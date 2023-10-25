﻿//PixivUniversal
//Copyright(C) 2017 Pixeez Plus Project

//This program is free software; you can redistribute it and/or
//modify it under the terms of the GNU General Public License
//as published by the Free Software Foundation; version 2
//of the License.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//GNU General Public License for more details.

//You should have received a copy of the GNU General Public License
//along with this program; if not, write to the Free Software
//Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using Newtonsoft.Json.Linq;
using Pixeez.Objects;

namespace Pixeez
{
    public enum MethodType
    {
        GET = 0,
        POST = 1,
        DELETE = 2,
    }

    public class AsyncResponse : IDisposable
    {
        public AsyncResponse(HttpResponseMessage source)
        {
            this.Source = source;
        }

        public HttpResponseMessage Source { get; }

        public virtual Task<Stream> GetResponseStreamAsync()
        {
            return this.Source.Content.ReadAsStreamAsync();
        }

        public virtual Task<string> GetResponseStringAsync()
        {
            return this.Source.Content.ReadAsStringAsync();
        }

        public virtual Task<byte[]> GetResponseByteArrayAsync()
        {
            return this.Source.Content.ReadAsByteArrayAsync();
        }

        public virtual void Dispose()
        {
            this.Source?.Dispose();
        }
    }

    public class StreamAsyncResponse : AsyncResponse
    {
        public Stream Stream { get; set; }
        public StreamAsyncResponse(HttpResponseMessage source, Stream stream) : base(source)
        {
            Stream = stream;
        }

        public override Task<Stream> GetResponseStreamAsync()
        {
            return Task.FromResult(Stream);
        }

        public async override Task<string> GetResponseStringAsync()
        {
            return await new StreamReader(Stream).ReadToEndAsync();
        }

        public override Task<byte[]> GetResponseByteArrayAsync()
        {
            throw new NotImplementedException();
        }

        public override void Dispose()
        {
            Source?.Dispose();
            this.Stream?.Dispose();
        }
    }

    public class AuthKey
    {
        public string Password;
        public string Username;

        public DateTime KeyExpTime;
    }
    public class AuthResult
    {
        public Tokens Tokens;
        public Authorize Authorize;
        public AuthKey Key;
    }

    public static class PIXIV
    {
        public static string ClientID = "MOBrBDS8blbauoSck0ZfDbtuzpyT";
        public static string ClientSecret = "lsACyCD94FhDUtGTXi3QzcFE2uU1hqtDaKeqrdwj";
        public static string HashSecret = "28c1fdd170a5204386cb1313c7077b34f83e4aaf4aa829ce78c231e05b0bae2c";
        public static int? TimeOut = null;

        public static Dictionary<string, string> TargetIPs { get; set; } = new Dictionary<string, string>()
        {
            {"oauth.secure.pixiv.net","210.140.131.224" },
            {"i.pximg.net","210.140.92.142" },
            {"www.pixiv.net","210.140.131.224" },
            {"app-api.pixiv.net","210.140.131.224" }
        };

        public static Dictionary<string, string> TargetSubjects { get; set; } = new Dictionary<string, string>()
        {
            {"210.140.131.224","CN=*.pixiv.net, O=pixiv Inc., OU=Development department, L=Shibuya-ku, S=Tokyo, C=JP" },
            {"210.140.92.142","CN=*.pximg.net, OU=Domain Control Validated" }
        };

        public static Dictionary<string, string> TargetSNs { get; set; } = new Dictionary<string, string>()
        {
            {"210.140.131.224","281941D074A6D4B07B72D729" },
            {"210.140.92.142","2387DB20E84EFCF82492545C" }
        };

        public static Dictionary<string, string> TargetTPs { get; set; } = new Dictionary<string, string>()
        {
            {"210.140.131.224","352FCC13B920E12CD15F3875E52AEDB95B62972B" },
            {"210.140.92.142","F4A431620F42E4D10EB42621C6948E3CD5014FB0" }
        };

        public static string MD5Hash(this string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text.Trim()));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                    builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }

        public static HttpClient Client(string proxy, string[] proxybypass, bool useproxy, int timeout = 30, bool ssl_version = true)
        {
            var Proxy = proxy;
            var UsingProxy = !string.IsNullOrEmpty(proxy) && useproxy;
            //ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            string time = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss+00:00");

            HttpClientHandler handler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 15,
                AutomaticDecompression = DecompressionMethods.None | DecompressionMethods.Deflate | DecompressionMethods.GZip,
                //SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
                Proxy = string.IsNullOrEmpty(proxy) ? null : new WebProxy(proxy, true, proxybypass),
                UseProxy = string.IsNullOrEmpty(proxy) || !UsingProxy ? false : true
            };
            if (ssl_version) handler.SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
            var httpClient = new HttpClient(handler, true){ Timeout=TimeSpan.FromSeconds(timeout) };
            httpClient.DefaultRequestHeaders.Add("App-OS", "ios");
            httpClient.DefaultRequestHeaders.Add("App-OS-Version", "14.6");
            //httpClient.DefaultRequestHeaders.Add("App-Version", "7.6.2");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "PixivIOSApp/7.13.3 (iOS 14.6; iPhone13,2)");

            httpClient.DefaultRequestHeaders.Add("X-Client-Time", time);
            httpClient.DefaultRequestHeaders.Add("X-Client-Hash", $"{time}{HashSecret}".MD5Hash());

            httpClient.DefaultRequestHeaders.AcceptEncoding.TryParseAdd("gzip");
            httpClient.DefaultRequestHeaders.AcceptEncoding.TryParseAdd("deflate");
            httpClient.DefaultRequestHeaders.AcceptEncoding.TryParseAdd("br");

            httpClient.DefaultRequestHeaders.AcceptLanguage.TryParseAdd("zh_CN");
            httpClient.DefaultRequestHeaders.AcceptLanguage.TryParseAdd("ja_JP");
            httpClient.DefaultRequestHeaders.AcceptLanguage.TryParseAdd("en");

            //httpClient.DefaultRequestHeaders.Add("Connection", "close");
            //httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            //httpClient.DefaultRequestHeaders.Add("Keep-Alive", "300");
            httpClient.DefaultRequestHeaders.ConnectionClose = true;

            //httpClient.Timeout = TimeSpan.FromSeconds(60);

            return (httpClient);
        }
    }

    public class Auth
    {
        protected internal static string Proxy = string.Empty;
        protected internal static string[] ProxyBypass = new string[] { };

        protected internal static bool UsingProxy = false;
        public static int TimeOut = 30;

        /// <summary>
        /// support proxy, modified by netcharm
        /// <para>Available parameters:</para>
        /// <para>- <c>string</c> username (required)</para>
        /// <para>- <c>string</c> password (required)</para>
        /// </summary>
        /// <returns>Tokens.</returns>
        public static async Task<AuthResult> AuthorizeAsync(string username, string password, string refreshtoken, string devicetoken, string proxy, string[] proxybypass, bool useproxy = false, CancellationTokenSource cancelToken = null, bool ssl_version = true)
        {
            //return await (AuthorizeAsync(username, password, refreshtoken, proxy, useproxy));

            var httpClient = PIXIV.Client(proxy, proxybypass, useproxy, TimeOut, ssl_version);

            FormUrlEncodedContent param;
            if (string.IsNullOrEmpty(refreshtoken))
            {
                param = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "get_secure_url", "1" },
                    { "client_id", PIXIV.ClientID },
                    { "client_secret", PIXIV.ClientSecret },
                    { "grant_type", "password" },
                    { "username", username },
                    { "password", password },
                });
            }
            else
            {
                param = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "get_secure_url", "1" },
                    { "client_id", PIXIV.ClientID },
                    { "client_secret", PIXIV.ClientSecret },
                    { "grant_type", "refresh_token" },
                    { "refresh_token", refreshtoken },
                });
            }

            if (cancelToken == null) cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(TimeOut));
            var response = await httpClient.PostAsync("https://oauth.secure.pixiv.net/auth/token", param, cancelToken.Token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var authorize = JToken.Parse(json).SelectToken("response").ToObject<Authorize>();

            var result = new Pixeez.AuthResult();
            result.Authorize = authorize;
            result.Key = new AuthKey()
            {
                Password = password,
                Username = username,
                KeyExpTime = authorize.ExpiresIn.HasValue ? DateTime.UtcNow.AddSeconds(authorize.ExpiresIn.Value) : DateTime.UtcNow.AddSeconds(3600 * 365)
            };
            result.Tokens = new Tokens(authorize.AccessToken) { RefreshToken = authorize.RefreshToken };
            return result;
        }

        public static async Task<AuthResult> AuthorizeAsync(string username, string password, string refreshtoken, string devicetoken, string proxy, IList<string> proxybypass, bool useproxy = false, CancellationTokenSource cancelToken = null, bool ssl_version = true)
        {
            return (await AuthorizeAsync(username, password, refreshtoken, devicetoken, proxy, proxybypass.ToArray(), useproxy, cancelToken));
        }

        /// <summary>
        /// support proxy, modified by netcharm
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="refreshtoken"></param>
        /// <param name="proxy"></param>
        /// <param name="useproxy"></param>
        /// <returns></returns>
        public static async Task<AuthResult> AuthorizeAsync(string username, string password, string refreshtoken, string proxy, string[] proxybypass, bool useproxy = false, CancellationTokenSource cancelToken = null, bool ssl_version = true)
        {
            var httpClient = PIXIV.Client(proxy, proxybypass, useproxy, TimeOut, ssl_version);

            FormUrlEncodedContent param;
            if (string.IsNullOrEmpty(refreshtoken))
            {
                param = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "get_secure_url", "1" },
                    { "client_id", PIXIV.ClientID },
                    { "client_secret", PIXIV.ClientSecret },
                    { "grant_type", "password" },
                    { "username", username },
                    { "password", password },
                });
            }
            else
            {
                param = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "get_secure_url", "1" },
                    { "client_id", PIXIV.ClientID },
                    { "client_secret", PIXIV.ClientSecret },
                    { "grant_type", "refresh_token" },
                    { "refresh_token", refreshtoken },
                });
            }

            if (cancelToken == null) cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(TimeOut));
            var response = await httpClient.PostAsync("https://oauth.secure.pixiv.net/auth/token", param, cancelToken.Token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException();

            var json = await response.Content.ReadAsStringAsync();
            var authorize = JToken.Parse(json).SelectToken("response").ToObject<Authorize>();

            var tokens = new Tokens(authorize.AccessToken);

            var result = new AuthResult();
            result.Authorize = authorize;
            result.Key = new AuthKey()
            {
                Password = password,
                Username = username,
                KeyExpTime = authorize.ExpiresIn.HasValue ? DateTime.UtcNow.AddSeconds(authorize.ExpiresIn.Value) : DateTime.UtcNow.AddSeconds(3600 * 365)
            };
            result.Tokens = new Tokens(authorize.AccessToken) { RefreshToken = authorize.RefreshToken };
            return result;
        }

        public static async Task<AuthResult> AuthorizeAsync(string username, string password, string refreshtoken, string proxy, IList<string> proxybypass, bool useproxy = false, CancellationTokenSource cancelToken = null, bool ssl_version = true)
        {
            return (await (AuthorizeAsync(username, password, refreshtoken, proxy, proxybypass.ToArray(), useproxy, cancelToken)));
        }

        /// <summary>
        /// support proxy, modified by netcharm
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="proxy"></param>
        /// <param name="useproxy"></param>
        /// <returns></returns>
        public static async Task<AuthResult> AuthorizeAsync(string username, string password, string proxy, string[] proxybypass, bool useproxy = false, CancellationTokenSource cancelToken = null, bool ssl_version = true)
        {
            var httpClient = PIXIV.Client(proxy, proxybypass, useproxy, TimeOut, ssl_version);

            FormUrlEncodedContent param;
            param = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "get_secure_url", "1" },
                { "client_id", PIXIV.ClientID },
                { "client_secret", PIXIV.ClientSecret },
                { "grant_type", "password" },
                { "username", username },
                { "password", password },
            });

            if (cancelToken == null) cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(TimeOut));
            var response = await httpClient.PostAsync("https://oauth.secure.pixiv.net/auth/token", param, cancelToken.Token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException();

            var json = await response.Content.ReadAsStringAsync();
            var authorize = JToken.Parse(json).SelectToken("response").ToObject<Authorize>();

            var tokens = new Tokens(authorize.AccessToken);

            var result = new Pixeez.AuthResult();
            result.Authorize = authorize;
            result.Key = new AuthKey()
            {
                Password = password,
                Username = username,
                KeyExpTime = authorize.ExpiresIn.HasValue ? DateTime.UtcNow.AddSeconds(authorize.ExpiresIn.Value) : DateTime.UtcNow.AddSeconds(3600 * 365)
            };
            result.Tokens = new Tokens(authorize.AccessToken) { RefreshToken = authorize.RefreshToken };
            return result;
        }

        public static async Task<AuthResult> AuthorizeAsync(string username, string password, string proxy, IList<string> proxybypass, bool useproxy = false, CancellationTokenSource cancelToken = null, bool ssl_version = true)
        {
            return (await AuthorizeAsync(username, password, proxy, proxybypass.ToArray(), useproxy, cancelToken, ssl_version));
        }

        /// <summary>
        /// support proxy, modified by netcharm
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="proxy"></param>
        /// <param name="useproxy"></param>
        /// <returns></returns>
        public static Tokens AuthorizeWithAccessToken(string accessToken, string proxy, string[] proxybypass, bool useproxy = false)
        {
            Proxy = proxy;
            ProxyBypass = proxybypass;
            UsingProxy = !string.IsNullOrEmpty(proxy) && useproxy;
            return new Tokens(accessToken);
        }

        public static Tokens AuthorizeWithAccessToken(string accessToken, string proxy, IList<string> proxybypass, bool useproxy = false)
        {
            Proxy = proxy;
            ProxyBypass = proxybypass.ToArray();
            UsingProxy = !string.IsNullOrEmpty(proxy) && useproxy;
            return new Tokens(accessToken);
        }

        public static Tokens AuthorizeWithAccessToken(string accessToken, string refreshToken, string proxy, string[] proxybypass, bool useproxy = false)
        {
            Proxy = proxy;
            ProxyBypass = proxybypass;
            UsingProxy = !string.IsNullOrEmpty(proxy) && useproxy;
            return new Tokens(accessToken, refreshToken);
        }

        public static Tokens AuthorizeWithAccessToken(string accessToken, string refreshToken, string proxy, IList<string> proxybypass, bool useproxy = false)
        {
            return (AuthorizeWithAccessToken(accessToken, refreshToken, proxy, proxybypass.ToArray(), useproxy));
        }
    }

    public class Tokens
    {
        private string Proxy = string.Empty;
        private bool UsingProxy = false;

        static public CancellationTokenSource RequestCancelSource { get; set; } = null;

        [Newtonsoft.Json.JsonProperty]
        public string RefreshToken { get; set; }
        [Newtonsoft.Json.JsonProperty, Newtonsoft.Json.JsonRequired]
        public string AccessToken { get; private set; }
        [Newtonsoft.Json.JsonConstructor]
        private Tokens() { }
        internal Tokens(string accessToken)
        {
            Proxy = Auth.Proxy;
            UsingProxy = Auth.UsingProxy;
            this.AccessToken = accessToken;
        }

        internal Tokens(string accessToken, string refreshToken)
        {
            Proxy = Auth.Proxy;
            UsingProxy = Auth.UsingProxy;
            this.AccessToken = accessToken;
            this.RefreshToken = refreshToken;
        }

        #region Request API related
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="url"></param>
        /// <param name="param"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public async Task<AsyncResponse> SendRequestWithAuthAsync(MethodType type, string url, IDictionary<string, string> param = null, IDictionary<string, string> headers = null, bool ssl_version = true)
        {
            AsyncResponse result = null;
            using (var httpClient = PIXIV.Client(Auth.Proxy, Auth.ProxyBypass, Auth.UsingProxy, Auth.TimeOut, ssl_version))
            {
                //httpClient.DefaultRequestHeaders.Add("Referer", "https://spapi.pixiv.net/");
                httpClient.DefaultRequestHeaders.Add("Referer", "https://app-api.pixiv.net/");
                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + this.AccessToken);
                result = await SendRequestWithoutHeaderAsync(type, url, param, headers, httpClient, cancelToken: RequestCancelSource);
            }
            return (result);
        }

        /// <summary>
        /// Fetch Image, Added by NetCharm
        /// </summary>
        /// <param name="type"></param>
        /// <param name="url"></param>
        /// <param name="param"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public async Task<AsyncResponse> SendRequestToGetImageAsync(MethodType type, string url, IDictionary<string, string> param = null, IDictionary<string, string> headers = null, bool ssl_version = true)
        {
            AsyncResponse result = null;
            using (var httpClient = PIXIV.Client(Auth.Proxy, Auth.ProxyBypass, Auth.UsingProxy, Auth.TimeOut, ssl_version))
            {
                httpClient.DefaultRequestHeaders.Add("Referer", "https://app-api.pixiv.net/");
                //httpClient.DefaultRequestHeaders.Add("Referer", "https://public-api.secure.pixiv.net/");
                result = await SendRequestWithoutHeaderAsync(type, url, param, headers, httpClient);
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="url"></param>
        /// <param name="needauth"></param>
        /// <param name="param"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public async Task<AsyncResponse> SendRequestWithoutAuthAsync(MethodType type, string url, bool needauth = false, IDictionary<string, string> param = null, IDictionary<string, string> headers = null, bool ssl_version = true)
        {
            AsyncResponse result = null;
            using (var httpClient = PIXIV.Client(Auth.Proxy, Auth.ProxyBypass, Auth.UsingProxy, Auth.TimeOut, ssl_version))
            {
                if (needauth) httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + this.AccessToken);
                result = await SendRequestWithoutHeaderAsync(type, url, param, headers, httpClient);
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="url"></param>
        /// <param name="param"></param>
        /// <param name="headers"></param>
        /// <param name="httpClient"></param>
        /// <returns></returns>
        private static async Task<AsyncResponse> SendRequestWithoutHeaderAsync(MethodType type, string url, IDictionary<string, string> param, IDictionary<string, string> headers, HttpClient httpClient, CancellationTokenSource cancelToken = null)
        {
            if (headers != null)
            {
                foreach (var header in headers)
                    httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            AsyncResponse asyncResponse = null;

            if (type == MethodType.POST)
            {
                var reqParam = new FormUrlEncodedContent(param);
                //reqParam.Headers.ContentType=""
                try
                {
                    using (var response = await httpClient.PostAsync(url, reqParam))
                    {
                        asyncResponse = new AsyncResponse(response);
                    }
                }
                catch (Exception ex)
                {
                    var r = ex.Message;
                }
            }
            else if (type == MethodType.DELETE)
            {
                var uri = url;

                if (param != null)
                {
                    var query_string = "";
                    foreach (KeyValuePair<string, string> kvp in param)
                    {
                        if (query_string == "")
                            query_string += "?";
                        else
                            query_string += "&";

                        query_string += WebUtility.UrlEncode(kvp.Key) + "=" + WebUtility.UrlEncode(kvp.Value);
                    }
                    uri += query_string;
                }
                try
                {
                    using (var response = await httpClient.DeleteAsync(uri))
                    {
                        asyncResponse = new AsyncResponse(response);
                    }
                }
                catch (Exception ex)
                {
                    var r = ex.Message;
                }
            }
            else
            {
                var uri = url;

                if (param != null)
                {
                    var query_string = "";
                    foreach (KeyValuePair<string, string> kvp in param)
                    {
                        if (query_string == "")
                            query_string += "?";
                        else
                            query_string += "&";

                        query_string += kvp.Key + "=" + WebUtility.UrlEncode(kvp.Value);
                    }
                    uri += query_string;
                }

                try
                {
                    if (!(RequestCancelSource is CancellationTokenSource)) RequestCancelSource = new CancellationTokenSource();
                    if (!(cancelToken is CancellationTokenSource)) cancelToken = RequestCancelSource; 
                    var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancelToken.Token);
                    response.EnsureSuccessStatusCode();
                    string vl = response.Content.Headers.ContentEncoding.FirstOrDefault();
                    if (vl != null && vl == "gzip")
                    {
                        asyncResponse = new StreamAsyncResponse(response, new System.IO.Compression.GZipStream(await response.Content.ReadAsStreamAsync(), System.IO.Compression.CompressionMode.Decompress));
                    }
                    else
                    {
                        asyncResponse = new AsyncResponse(response);
                    }
                }
                catch (Exception ex)
                {
                    var r = ex.Message;
                }
            }

            return asyncResponse;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="url"></param>
        /// <param name="param"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        public async Task<AsyncResponse> SendRequestAsync(MethodType type, string url, IDictionary<string, string> param = null, IDictionary<string, string> headers = null)
        {
            return await SendRequestWithAuthAsync(type, url, param, headers);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="type"></param>
        /// <param name="url"></param>
        /// <param name="param"></param>
        /// <param name="headers"></param>
        /// <returns></returns>
        private async Task<T> AccessApiAsync<T>(MethodType type, string url, IDictionary<string, string> param, IDictionary<string, string> headers = null) where T : class
        {
            //var dic = new Dictionary<string, string>(param);
            //var ret = await AccessNewApiAsync<T>(url, true, dic, type);
            //return (ret);
            var response = await this.SendRequestAsync(type, url, param, headers);
            return (response == null ? default(T) : await AccessApiAsync<T>(response));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="res"></param>
        /// <returns></returns>
        private async Task<T> AccessApiAsync<T>(AsyncResponse res) where T : class
        {
            T result = default(T);
            using (var response = res)
            {
                if (response != null && response.Source.IsSuccessStatusCode)
                {
                    var len = response.Source.Content.Headers.ContentLength ?? -1;
                    if (len > 2 || len < 0)
                    {
                        var json = await response.GetResponseStringAsync();
                        var obj = JToken.Parse(json).SelectToken("response").ToObject<T>();

                        if (obj is IPagenated)
                            ((IPagenated)obj).Pagination = JToken.Parse(json).SelectToken("pagination").ToObject<Pagination>();

                        result = obj;
                    }
                }
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="req_auth"></param>
        /// <param name="dic"></param>
        /// <param name="methodtype"></param>
        /// <returns></returns>
        public async Task<T> AccessNewApiAsync<T>(string url, bool req_auth = true, Dictionary<string, string> dic = null, MethodType methodtype = MethodType.GET)
        {
            if (req_auth)
            {
                //using (var res = await SendRequestAsync(methodtype, url, dic))
                using (var res = await SendRequestWithoutAuthAsync(methodtype, url, req_auth, dic))
                {
                    if (res != null && res.Source.IsSuccessStatusCode)
                    {
                        var str = await res.GetResponseStringAsync();
                        return JToken.Parse(str).ToObject<T>();
                    }
                    else return default(T);
                }
            }
            else
            {
                using (var res = await SendRequestWithoutAuthAsync(methodtype, url, req_auth, dic))
                {
                    if (res != null && res.Source.IsSuccessStatusCode)
                    {
                        var str = await res.GetResponseStringAsync();
                        return JToken.Parse(str).ToObject<T>();
                    }
                    else return default(T);
                }
            }
        }
        #endregion

        public string format_bool(bool value)
        {
            if (value)
            {
                return "true";
            }
            return "false";
        }

        #region User related
        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> authorId (required)</para>
        /// </summary>
        /// <returns>Users.</returns>
        public async Task<List<User>> GetUsersAsync(long authorId)
        {
            var url = "https://public-api.secure.pixiv.net/v1/users/" + authorId.ToString() + ".json";

            var param = new Dictionary<string, string>
            {
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
                //{ "image_sizes", "px_128x128,small,medium,large,px_480mw,square_medium,original" } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" } ,
                { "include_stats", "1" } ,
                { "include_profile", "1" } ,
                { "include_workspace", "1" } ,
                { "include_contacts", "1" } ,
            };

            return await AccessApiAsync<List<User>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user_id"></param>
        /// <param name="filter"></param>
        /// <param name="req_auth"></param>
        /// <returns></returns>
        public async Task<UserInfo> GetUserInfoAsync(string user_id, string filter = "for_ios", bool req_auth = true)
        {
            var url = "https://app-api.pixiv.net/v1/user/detail";

            var param = new Dictionary<string, string>
            {
                {"user_id",user_id },
                {"filter",filter }
            };
            return await AccessNewApiAsync<UserInfo>(url, req_auth, param);
        }

        /// <summary>
        /// 获取我的订阅
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> maxId (optional)</para>
        /// <para>- <c>bool</c> showR18 (optional)</para>
        /// </summary>
        /// <returns>Feeds.</returns>
        public async Task<List<Feed>> GetMyFeedsAsync(long maxId = 0, bool showR18 = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/me/feeds.json";
            var param = new Dictionary<string, string>
            {
                { "relation", "all" } ,
                { "type", "touch_nottext" } ,
                { "show_r18", Convert.ToInt32(showR18).ToString() } ,
            };

            if (maxId != 0)
                param.Add("max_id", maxId.ToString());

            return await this.AccessApiAsync<List<Feed>>(MethodType.GET, url, param);
        }

        public async Task<List<Feed>> GetMyFeedsAsync(long user_id = 0)
        {
            var url = "https://public-api.secure.pixiv.net/v1/user/related";
            var param = new Dictionary<string, string>
            {
                { "seed_user_id", user_id.ToString() } ,
                { "filter", "for_ios" } ,
            };
            return await AccessNewApiAsync<List<Feed>>(url, true, param);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user_id"></param>
        /// <param name="publicity"></param>
        /// <returns></returns>
        public async Task<bool> AddFavouriteUser(long user_id, string publicity = "public")
        {
            bool result = false;
            var url = "https://public-api.secure.pixiv.net/v1/me/favorite-users.json";
            var param = new Dictionary<string, string>
            {
                { "target_user_id", user_id.ToString() },
                { "publicity", publicity }
            };
            using (var res = await SendRequestAsync(MethodType.POST, url, param))
            {
                if (res is AsyncResponse)
                {
                    try
                    {
                        result = res.Source.IsSuccessStatusCode;
                        var code = res.Source.EnsureSuccessStatusCode();
                        var len = res.Source.Content.Headers.ContentLength ?? 0;
                        if (len > 2)
                        {
                            var response = await res.GetResponseStringAsync();
                        }
                    }
                    catch (Exception ex) { var r = ex.Message; }
                }
            }
            return (result);
        }

        public async Task<bool> AddFollowUser(long user_id, string restrict = "public")
        {
            bool result = false;
            var url = "https://app-api.pixiv.net/v1/user/follow/add";
            var param = new Dictionary<string, string>
            {
                { "user_id", user_id.ToString() },
                { "restrict", restrict }
            };
            using (var res = await SendRequestAsync(MethodType.POST, url, param))
            {
                try
                {
                    result = res.Source.IsSuccessStatusCode;
                    var code = res.Source.EnsureSuccessStatusCode();
                    var len = res.Source.Content.Headers.ContentLength ?? 0;
                    if (len > 2)
                    {
                        var response = await res.GetResponseStringAsync();
                    }
                }
                catch (Exception ex) { var r = ex.Message; }
            }
            return (result);
        }

        public async Task<bool> AddFollowUser(string user_id, string restrict = "public")
        {
            long uid = 0;
            long.TryParse(user_id, out uid);
            return (await AddFollowUser(uid, restrict));
        }

        /// <summary>
        /// 可批量解除，逗号分隔
        /// </summary>
        /// <param name="user_id"></param>
        /// <param name="publicity"></param>
        /// <returns></returns>
        public async Task<bool> DeleteFavouriteUser(string user_id, string publicity = "public")
        {
            bool result = false;
            var url = "https://public-api.secure.pixiv.net/v1/me/favorite-users.json";
            var param = new Dictionary<string, string> {
                { "delete_ids", user_id.ToString() },
                //{ "publicity", publicity }
            };
            using (var res = await SendRequestAsync(MethodType.DELETE, url, param))
            {
                try
                {
                    result = res.Source.IsSuccessStatusCode;
                    var code = res.Source.EnsureSuccessStatusCode();
                    var len = res.Source.Content.Headers.ContentLength ?? 0;
                    if (len > 2)
                    {
                        var response = await res.GetResponseStringAsync();
                    }
                }
                catch (Exception ex) { var r = ex.Message; }
            }
            return (result);
        }

        public async Task<bool> DeleteFollowUser(long user_id)
        {
            bool result = false;
            var url = "https://app-api.pixiv.net/v1/user/follow/delete";
            var param = new Dictionary<string, string>
            {
                { "user_id", user_id.ToString() }
            };
            using (var res = await SendRequestAsync(MethodType.POST, url, param))
            {
                try
                {
                    result = res.Source.IsSuccessStatusCode;
                    var code = res.Source.EnsureSuccessStatusCode();
                    var len = res.Source.Content.Headers.ContentLength ?? 0;
                    if (len > 2)
                    {
                        var response = await res.GetResponseStringAsync();
                    }
                }
                catch (Exception ex) { var r = ex.Message; }
            }
            return (result);
        }

        public async Task<bool> DeleteFollowUser(string user_id)
        {
            long uid = 0;
            long.TryParse(user_id, out uid);
            return(await DeleteFollowUser(uid));
        }

        /// <summary>
        /// Following用户列表
        /// </summary>
        /// <param name="user_id"></param>
        /// <param name="publicity"></param>
        /// <param name="offset"></param>
        /// <param name="req_auth"></param>
        /// <returns></returns>
        public async Task<UsersSearchResult> GetFollowingUsers(string user_id, string publicity = "public", int offset = 0, bool req_auth = true)
        {
            var url = "https://app-api.pixiv.net/v1/user/following";
            var param = new Dictionary<string, string>
            {
                { "user_id", user_id.ToString() },
                { "restrict", publicity }
            };
            return await AccessNewApiAsync<UsersSearchResult>(url, true, param);
        }

        /// <summary>
        /// Followers用户列表
        /// </summary>
        /// <param name="user_id"></param>
        /// <param name="filter"></param>
        /// <param name="offset"></param>
        /// <param name="req_auth"></param>
        /// <returns></returns>
        public async Task<UsersSearchResult> GetFollowerUsers(string user_id, string filter = "for_ios", int offset = 0, bool req_auth = true)
        {
            var url = "https://app-api.pixiv.net/v1/user/follower";
            var param = new Dictionary<string, string>
            {
                { "user_id", user_id.ToString() },
                { "filter", filter }
            };
            if (offset >= 0) param["offset"] = offset.ToString();
            return await AccessNewApiAsync<UsersSearchResult>(url, true, param);
        }

        /// <summary>
        /// 好P友
        /// </summary>
        /// <param name="user_id"></param>
        /// <param name="offset"></param>
        /// <param name="req_auth"></param>
        /// <returns></returns>
        public async Task<UsersSearchResult> GetMyPixiv(string user_id, int offset = 0, bool req_auth = true)
        {
            var url = "https://app-api.pixiv.net/v1/user/mypixiv";
            var param = new Dictionary<string, string>
            {
                { "user_id", user_id.ToString() },
            };
            if (offset >= 0) param["offset"] = offset.ToString();
            return await AccessNewApiAsync<UsersSearchResult>(url, true, param);
        }

        public async Task<UsersSearchResultAlt> GetBlackListUsers(string user_id, string filter = "for_ios", int offset = 0, bool req_auth = true)
        {
            var url = "https://app-api.pixiv.net/v2/user/list";
            var param = new Dictionary<string, string>
            {
                { "user_id", user_id.ToString() },
                { "filter", filter }
            };
            if (offset >= 0) param["offset"] = offset.ToString();
            return await AccessNewApiAsync<UsersSearchResultAlt>(url, true, param);
        }

        #endregion

        #region Bookmark / Favorite related
        /// <summary>
        /// 
        /// </summary>
        /// <param name="illust_id"></param>
        /// <param name="restrict"></param>
        /// <returns></returns>
        public async Task<BookmarkDetailRootobject> GetBookMarkedDetailAsync(long illust_id, string restrict = "public")
        {
            string url = "https://app-api.pixiv.net/v2/illust/bookmark/detail";
            var dic = new Dictionary<string, string>()
            {
                { "illust_id", illust_id.ToString() },
                { "restrict", restrict.ToString() }
            };
            return await AccessNewApiAsync<BookmarkDetailRootobject>(url, true, dic);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="restrict"></param>
        /// <returns></returns>
        public async Task<BookmarkDetailRootobject> GetBookMarkedTagsAsync(string restrict = "public")
        {
            string url = "https://app-api.pixiv.net/v1/user/bookmark-tags/illust";
            var dic = new Dictionary<string, string>()
            {
                { "restrict", restrict.ToString() },
            };
            return await AccessNewApiAsync<BookmarkDetailRootobject>(url, true, dic);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>UsersFavoriteWorks. (Pagenated)</returns>
        public async Task<Illusts> GetUserFavoriteWorksAsync(long user_id, string restrict = "public", string filter = "for_ios", int? max_bookmark_id = null, string tag = null, bool req_auth = true)
        {
            var url = "https://app-api.pixiv.net/v1/user/bookmarks/illust";

            var param = new Dictionary<string, string>
            {
                { "user_id", user_id.ToString() } ,
                { "restrict", restrict.ToString() } ,
                { "filter", filter } ,
            };
            if (max_bookmark_id.HasValue)
            {
                param.Add("max_bookmark_id", max_bookmark_id.Value.ToString());
            }
            if (tag != null) param.Add("tag", tag);
            return await AccessNewApiAsync<Illusts>(url, dic: param, req_auth: req_auth);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> workID (required)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// </summary>
        /// <returns>UsersWorks. (Pagenated)</returns>
        public async Task<bool> AddMyFavoriteWorksAsync(long workId, IEnumerable<string> tags = null, string publicity = "public")
        {
            bool result = false;
            var url = "https://app-api.pixiv.net/v2/illust/bookmark/add";

            var param = new Dictionary<string, string>
            {
                { "illust_id", workId.ToString() } ,
                { "restrict", publicity } ,
            };

            if (tags != null)
                param.Add("tags[]", string.Join(",", tags));

            using (var res = await SendRequestWithoutAuthAsync(MethodType.POST, url, param: param, needauth: true))
            {
                if (res is AsyncResponse)
                {
                    try
                    {
                        var code = res.Source.EnsureSuccessStatusCode();
                        var len = res.Source.Content.Headers.ContentLength ?? 0;
                        if (len > 2)
                        {
                            var response = await res.GetResponseStringAsync();
                        }
                        result = true;
                    }
                    catch (Exception ex) { var r = ex.Message; }
                }
            }
            return (result);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>IEnumerable</c> workIds (required)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// </summary>
        /// <returns>UsersWorks. (Pagenated)</returns>
        public async Task<List<UsersFavoriteWork>> DeleteMyFavoriteWorksAsync(IEnumerable<long> workIds, string publicity = "public")
        {
            var url = "https://app-api.pixiv.net/v1/illust/bookmark/delete";

            var param = new Dictionary<string, string>
            {
                { "illust_id", string.Join(",", workIds.Select(x => x.ToString())) } ,
                //{ "publicity", publicity } ,
            };

            return await AccessApiAsync<List<UsersFavoriteWork>>(MethodType.POST, url, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> workId (required)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// </summary>
        /// <returns>UsersWorks. (Pagenated)</returns>
        public async Task<Paginated<UsersFavoriteWork>> DeleteMyFavoriteWorksAsync(long workId, string publicity = "public")
        {
            var url = "https://app-api.pixiv.net/v1/illust/bookmark/delete";

            var param = new Dictionary<string, string>
            {
                { "illust_id", workId.ToString() } ,
                //{ "publicity", publicity } ,
            };

            return await AccessApiAsync<Paginated<UsersFavoriteWork>>(MethodType.POST, url, param);
        }
        #endregion

        #region Following related
        //public async Task GetMyFollowingWorksAsync(string restrict = "public",string offset=null)
        //{
        //    var dic = new Dictionary<string, string> { { "restrict", restrict } };
        //    if (offset != null)
        //        dic["offset"] = offset;
        //    await SendRequestWithoutAuthAsync(MethodType.GET, "https://app-api.pixiv.net/v2/illust/follow", param: dic, needauth: true);
        //}

        //public async Task GetMyFollowingUsers(string authorid, int page = 1, int per_page = 30)
        //{
        //    string url = "https://public-api.secure.pixiv.net/v1/users/"+ authorid +"/following.json";
        //    await SendRequestWithAuthAsync(MethodType.GET, url, new Dictionary<string, string> { { "page", page.ToString() }, { "per_page", per_page.ToString() } });
        //}

        public async Task<BookmarkDetailRootobject> GetFollowedDetailAsync(long user_id, string restrict = "public")
        {
            string url = "https://app-api.pixiv.net/v2/user/following/detail";
            var dic = new Dictionary<string, string>()
            {
                { "user_id", user_id.ToString() },
                { "restrict", restrict.ToString() }
            };
            return await AccessNewApiAsync<BookmarkDetailRootobject>(url, true, dic);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> authorId (required)</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>UsersWorks. (Pagenated)</returns>
        /// 
        public async Task<RecommendedRootobject> GetMyFollowingWorksAsync(string restrict = "public", int? offset = null)
        {
            var url = "https://app-api.pixiv.net/v2/illust/follow";

            var param = new Dictionary<string, string>
            {
                { "restrict", restrict } ,
            };
            if (offset.HasValue)
                param.Add("offset", offset.Value.ToString());

            return await AccessNewApiAsync<RecommendedRootobject>(url, dic: param);
        }

        public async Task<List<User>> GetMyFollowingUsers(long authorid, string restrict = "public", int? offset = null)
        {
            //var url = "https://app-api.pixiv.net/v2/illust/follow";
            var url = "https://public-api.secure.pixiv.net/v1/users/"+ authorid +"/following.json";

            var param = new Dictionary<string, string>
            {
                { "restrict", restrict } ,
            };
            if (offset.HasValue)
                param.Add("offset", offset.Value.ToString());

            return await AccessNewApiAsync<List<User>>(url, dic: param);
        }
        #endregion

        #region User works related
        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> authorId (required)</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>UsersWorks. (Pagenated)</returns>
        [Obsolete]
        public async Task<Paginated<UsersWork>> GetUsersWorksAsync(long authorId, int page = 1, int perPage = 30, string publicity = "public", bool includeSanityLevel = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/users/" + authorId.ToString() + "/works.json";

            var param = new Dictionary<string, string>
            {
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,
                { "publicity", publicity } ,
                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw,square_medium,original" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            return await AccessApiAsync<Paginated<UsersWork>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> authorId (required)</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>string</c> publicity (optional) [ public, private ]</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>UsersFavoriteWorks. (Pagenated)</returns>
        [Obsolete]
        public async Task<Paginated<UsersFavoriteWork>> GetUsersFavoriteWorksAsync(long authorId, int page = 1, int perPage = 30, string publicity = "public", bool includeSanityLevel = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/users/" + authorId.ToString() + "/favorite_works.json";

            var param = new Dictionary<string, string>
            {
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,
                { "publicity", publicity } ,
                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw,square_medium,original" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            return await AccessApiAsync<Paginated<UsersFavoriteWork>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> maxId (optional)</para>
        /// <para>- <c>bool</c> showR18 (optional)</para>
        /// </summary>
        /// <returns>Feed.</returns>
        public async Task<List<Feed>> GetUsersFeedsAsync(long authorId, long maxId = 0, bool showR18 = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/users/" + authorId.ToString() + "/feeds.json";

            var param = new Dictionary<string, string>
            {
                { "relation", "all" } ,
                { "type", "touch_nottext" } ,
                { "show_r18", Convert.ToInt32(showR18).ToString() } ,
            };

            if (maxId != 0)
                param.Add("max_id", maxId.ToString());

            return await AccessApiAsync<List<Feed>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// 获取用户作品列表 (无需登录)
        /// </summary>
        /// <param name="user_id"></param>
        /// <param name="type">allowed: illust, manga, or none</param>
        /// <param name="filter"></param>
        /// <param name="offset"></param>
        /// <param name="req_auth"></param>
        /// <returns></returns>
        public async Task<Illusts> GetUserWorksAsync(long user_id, string type = "", string filter = "for_ios", int? offset = null, bool req_auth = true)
        {
            var url = "https://app-api.pixiv.net/v1/user/illusts";

            var param = new Dictionary<string, string>
            {
                { "user_id",user_id.ToString() },
                { "type", string.IsNullOrEmpty(type) ? "illust,manga,ugoira" : type },
                { "filter", filter }
            };
            if (string.IsNullOrEmpty(type)) param.Remove("type");
            if (offset != null)
            {
                param["offset"] = offset.Value.ToString();
            }
            return await AccessNewApiAsync<Illusts>(url, req_auth, param);
        }
        #endregion

        #region Search related
        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>string</c> q (required)</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>string</c> mode (optional) [ text, tag, exact_tag, caption ]</para>
        /// <para>- <c>string</c> period (optional) [ all, day, week, month ]</para>
        /// <para>- <c>string</c> order (optional) [ desc, asc ]</para>
        /// <para>- <c>string</c> sort (optional) [ date, popular ]</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>Works. (Pagenated)</returns>
        public async Task<Paginated<NormalWork>> SearchWorksAsync(string query, int page = 1, int perPage = 30, string mode = "text", string period = "all", string order = "desc", string sort = "date", bool includeSanityLevel = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/search/works.json";

            var param = new Dictionary<string, string>
            {
                { "q", query } ,
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,
                { "period", period } ,
                { "order", order } ,
                { "sort", sort } ,
                { "mode", mode } ,
                { "types", "illustration,manga,ugoira" },
                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                //{ "image_sizes", "px_128x128,small,medium,large,px_480mw,square_medium,original" } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            return await AccessApiAsync<Paginated<NormalWork>>(MethodType.GET, url, param);
        }

        //# 搜索 (Search) (无需登录)
        //# search_target - 搜索类型
        //# partial_match_for_tags  - 标签部分一致
        //# exact_match_for_tags    - 标签完全一致
        //# title_and_caption       - 标题说明文
        //# sort: [date_desc, date_asc]
        //# duration: [within_last_day, within_last_week, within_last_month]
        public async Task<Illusts> SearchIllustWorksAsync(string query, string search_target = "partial_match_for_tags", string sort = "date_desc", string filter = "for_ios", string offset = null, bool req_auth = true, string duration = null)
        {
            var url = "https://app-api.pixiv.net/v1/search/illust";

            var param = new Dictionary<string, string>
            {
                {"word",query },
                {"search_target",search_target },
                {"sort",sort },
                {"filter",filter }
            };
            if (duration != null)
                param["duration"] = duration;
            if (offset != null)
                param["offset"] = offset;
            return await AccessNewApiAsync<Illusts>(url, req_auth, param);
        }

        public async Task<UsersSearchResult> SearchUserAsync(string query, string filter = "for_ios", bool req_auth = true)
        {
            var url = "https://app-api.pixiv.net/v1/search/user";

            var param = new Dictionary<string, string>
            {
                {"word",query },
                {"filter",filter }
            };
            return await AccessNewApiAsync<UsersSearchResult>(url, req_auth, param);
        }

        /// <summary>
        /// Search Trending Tags Illust, added by netcharm
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="req_auth"></param>
        /// <returns></returns>
        public async Task<TrendingTags> GetTrendingTagsIllustAsync(string filter = "for_ios", bool req_auth = true)
        {
            var url = "https://app-api.pixiv.net/v1/trending-tags/illust";

            var param = new Dictionary<string, string>
            {
                {"filter",filter }
            };
            return await AccessNewApiAsync<TrendingTags>(url, req_auth, param);
        }
        #endregion

        #region Illusts related
        /// <summary>
        /// 
        /// </summary>
        /// <param name="content_type"></param>
        /// <param name="include_ranking_label"></param>
        /// <param name="filter"></param>
        /// <param name="max_bookmark_id_for_recommend"></param>
        /// <param name="min_bookmark_id_for_recent_illust"></param>
        /// <param name="offset"></param>
        /// <param name="include_ranking_illusts"></param>
        /// <param name="bookmark_illust_ids"></param>
        /// <param name="req_auth"></param>
        /// <returns></returns>
        public async Task<RecommendedRootobject> GetRecommendedWorks(string content_type = "illust", bool include_ranking_label = true, string filter = "for_ios",
            string max_bookmark_id_for_recommend = null, string min_bookmark_id_for_recent_illust = null,
            string offset = null, bool? include_ranking_illusts = null, string bookmark_illust_ids = null, bool include_privacy_policy = true, bool req_auth = true)
        {
            string url;
            if (req_auth)
                url = "https://app-api.pixiv.net/v1/illust/recommended";
            else
                url = "https://app-api.pixiv.net/v1/illust/recommended-nologin";
            var dic = new Dictionary<string, string>() {
                { "content_type", content_type},
                //{ "include_ranking", format_bool(true)},
                { "include_ranking_label", format_bool(include_ranking_label)},
                { "filter", filter }
            };
            if (!string.IsNullOrEmpty(max_bookmark_id_for_recommend))
                dic["max_bookmark_id_for_recommend"] = max_bookmark_id_for_recommend;
            //else
            //    dic["max_bookmark_id_for_recommend"] = "2000";
            if (!string.IsNullOrEmpty(min_bookmark_id_for_recent_illust))
                dic["min_bookmark_id_for_recent_illust"] = min_bookmark_id_for_recent_illust;
            //else
            //    dic["min_bookmark_id_for_recent_illust"] = "20";
            if (!string.IsNullOrEmpty(offset))
                dic["offset"] = offset;
            if (include_ranking_illusts.HasValue)
                dic["include_ranking_illusts"] = format_bool(include_ranking_illusts.Value);
            if (!req_auth && !string.IsNullOrEmpty(bookmark_illust_ids))
                dic["bookmark_illust_ids"] = bookmark_illust_ids;
            if (include_privacy_policy)
                dic["include_privacy_policy"] = format_bool(include_privacy_policy);

            return await AccessNewApiAsync<RecommendedRootobject>(url, req_auth, dic);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>MethodType</c> type (required) [ GET, POST ]</para>
        /// <para>- <c>string</c> url (required)</para>
        /// <para>- <c>IDictionary</c> param (required)</para>
        /// <para>- <c>IDictionary</c> header (optional)</para>
        /// </summary>
        /// <returns>AsyncResponse.</returns>
        public async Task<Illusts> GetRelatedWorks(long illust_id, string filter = "for_ios", string seed_illust_ids = null, bool req_auth = true)
        {
            string url = "https://app-api.pixiv.net/v2/illust/related";
            var dic = new Dictionary<string, string>()
            {
                { "illust_id", illust_id.ToString() },
                { "filter", filter }
            };
            if (!string.IsNullOrEmpty(seed_illust_ids))
                dic.Add("seed_illust_ids[]", $"[{seed_illust_ids}]");
            return await AccessNewApiAsync<Illusts>(url, req_auth, dic);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>string</c> mode (optional) [ daily, weekly, monthly, male, female, rookie, daily_r18, weekly_r18, male_r18, female_r18, r18g ]</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>string</c> date (optional) [ 2015-04-01 ]</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>RankingAll. (Pagenated)</returns>
        public async Task<Paginated<Rank>> GetRankingAllAsync(string mode = "daily", int page = 1, int perPage = 30, string date = "", bool includeSanityLevel = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/ranking/all";
            var param = new Dictionary<string, string>
            {
                { "mode", mode } ,
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,
                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw,square_medium,original" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            if (!string.IsNullOrWhiteSpace(date))
                param.Add("date", date);

            return await AccessApiAsync<Paginated<Rank>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// New ranking illusts, added by netcharm
        /// mode: [day, week, month, day_male, day_female, week_original, week_rookie, day_r18, day_male_r18, day_female_r18, week_r18, week_r18g, day_manga]
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="page"></param>
        /// <param name="perPage"></param>
        /// <param name="date"></param>
        /// <param name="req_auth"></param>
        /// <param name="includeSanityLevel"></param>
        /// <returns></returns>
        public async Task<RecommendedRootobject> GetRankingAsync(string mode = "day", int page = 1, int perPage = 30, string date = "", bool req_auth = true, bool includeSanityLevel = true)
        {
            var url = "https://app-api.pixiv.net/v1/illust/ranking";
            var param = new Dictionary<string, string>
            {
                { "mode", mode } ,
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,
                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw,square_medium,original" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            if (!string.IsNullOrWhiteSpace(date))
                param.Add("date", date);

            return await AccessNewApiAsync<RecommendedRootobject>(url, true, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>int</c> page (optional)</para>
        /// <para>- <c>int</c> perPage (optional)</para>
        /// <para>- <c>bool</c> includeSanityLevel (optional)</para>
        /// </summary>
        /// <returns>Works. (Pagenated)</returns>
        public async Task<Paginated<NormalWork>> GetLatestWorksAsync(int page = 1, int perPage = 30, bool includeSanityLevel = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/works.json";

            var param = new Dictionary<string, string>
            {
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,

                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                //{ "image_sizes", "px_128x128,small,medium,large,px_480mw,square_medium,original" } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            return await AccessApiAsync<Paginated<NormalWork>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// try using new API, but... added by netcharm
        /// </summary>
        /// <param name="page"></param>
        /// <param name="perPage"></param>
        /// <param name="includeSanityLevel"></param>
        /// <param name="req_auth"></param>
        /// <returns></returns>
        public async Task<Paginated<NormalWork>> GetLatestWorksNewAsync(int page = 1, int perPage = 30, bool includeSanityLevel = true, bool req_auth = true)
        {
            var url = "https://public-api.secure.pixiv.net/v1/works.json";

            var param = new Dictionary<string, string>
            {
                { "page", page.ToString() } ,
                { "per_page", perPage.ToString() } ,

                { "include_stats", "1" } ,
                { "include_sanity_level", Convert.ToInt32(includeSanityLevel).ToString() } ,
                //{ "image_sizes", "px_128x128,small,medium,large,px_480mw,square_medium,original" } ,
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" } ,
                { "profile_image_sizes", "px_170x170,px_50x50" } ,
            };

            return await AccessNewApiAsync<Paginated<NormalWork>>(url, true, param);
            //return await AccessApiAsync<Paginated<IllustWork>>(MethodType.GET, url, param);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="illust_id"></param>
        /// <param name="offset"></param>
        /// <param name="include_total_comments"></param>
        /// <returns></returns>
        public async Task<IllustCommentObject> GetIllustComments(string illust_id, string offset = null,
            bool? include_total_comments = null)
        {
            string url = "https://app-api.pixiv.net/v1/illust/comments";
            var dic = new Dictionary<string, string>()
            {
                {"illust_id",illust_id }
            };
            if (offset != null)
                dic["offset"] = offset;
            if (include_total_comments != null)
                dic["include_total_comments"] = format_bool(include_total_comments.Value);

            return await AccessNewApiAsync<IllustCommentObject>(url, true, dic);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="illust_id"></param>
        /// <returns></returns>
        public async Task<UgoiraMetadata> GetUgoiraMetadata(long illust_id)
        {
            return (await GetUgoiraMetadata(illust_id.ToString()));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="illust_id"></param>
        /// <returns></returns>
        public async Task<UgoiraMetadata> GetUgoiraMetadata(string illust_id)
        {
            var url = "https://app-api.pixiv.net/v1/ugoira/metadata";

            var param = new Dictionary<string, string>
            {
                { "illust_id", illust_id.ToString() }
            };
            return await AccessNewApiAsync<UgoiraMetadata>(url, true, param);
        }

        /// <summary>
        /// <para>Available parameters:</para>
        /// <para>- <c>long</c> illustId (required)</para>
        /// </summary>
        /// <returns>Works.</returns>
        public async Task<List<NormalWork>> GetWorksAsync(long illustId)
        {
            var result = default(List<NormalWork>);
            var url = "https://public-api.secure.pixiv.net/v1/works/" + illustId.ToString() + ".json";

            var param = new Dictionary<string, string>
            {
                { "image_sizes", "px_128x128,small,medium,large,px_480mw" } ,
                { "include_stats", "true" },
                { "include_sanity_level", "true" },
            };

            try
            {
                result = await AccessApiAsync<List<NormalWork>>(MethodType.GET, url, param);
            }
            catch (Exception ex) { var r = ex.Message; }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="illustId"></param>
        /// <returns></returns>
        public async Task<List<NormalWork>> GetIllustDetailAsync(long illustId)
        {
            var result = default(List<NormalWork>);
            //var url = "https://public-api.secure.pixiv.net/v1/works/" + illustId.ToString() + ".json";
            var url = "https://app-api.pixiv.net/v1/illust/detail";
            //var url = "https://public-api.secure.pixiv.net/v1/illust/detail";

            var param = new Dictionary<string, string>
            {
                { "illust_id", illustId.ToString() },
                //{ "filter", "for_android" }
            };

            try
            {
                result = await AccessApiAsync<List<NormalWork>>(MethodType.GET, url, param);
            }
            catch (Exception ex) { var r = ex.Message; }
            return (result);
        }

        #endregion
    }
}
