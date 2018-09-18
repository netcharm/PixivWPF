using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Management;
using Microsoft.Win32;
using System.Windows.Media;
using System.Net;
using System.Windows.Media.Imaging;
using System.Net.Http;
using System.Collections;
using System.Threading;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using System.Text.RegularExpressions;
using MahApps.Metro.Controls.Dialogs;

namespace PixivWPF.Common
{
    public enum PixivPage
    {
        None,
        WorkSet,
        Recommanded,
        Latest,
        My,
        MyWork,
        User,
        UserWork,
        Favorite,
        FavoritePrivate,
        Follow,
        FollowPrivate,
        Bookmark,
        MyBookmark,
        DailyTop,
        WeeklyTop,
        MonthlyTop
    }

    public static class CommonHelper
    {
        private static Setting setting = Setting.Load();

        public static async Task<Pixeez.Tokens> ShowLogin()
        {
            Pixeez.Tokens result = null;
            var accesstoken = setting.AccessToken;
            try
            {
                if (!string.IsNullOrEmpty(setting.User) && !string.IsNullOrEmpty(setting.Pass) && !string.IsNullOrEmpty(setting.AccessToken))
                {
                    if(DateTime.Now.ToFileTime() - setting.Update < 300000)
                    {
                        result = Pixeez.Auth.AuthorizeWithAccessToken(setting.AccessToken, setting.Proxy, setting.UsingProxy);
                    }
                    else
                    {
                        try
                        {
                            var authResult = await Pixeez.Auth.AuthorizeAsync(setting.User, setting.Pass, setting.AccessToken, setting.Proxy, setting.UsingProxy);
                            //var authResult = await Pixeez.Auth.AuthorizeAsync(setting.User, setting.Pass, "", setting.Proxy, setting.UsingProxy);
                            setting.AccessToken = authResult.Authorize.AccessToken;
                            result = authResult.Tokens;
                        }
                        catch (Exception)
                        {
                            result = Pixeez.Auth.AuthorizeWithAccessToken(setting.AccessToken, setting.Proxy, setting.UsingProxy);
                        }
                    }
                }
                else
                {
                    var dlgLogin = new PixivLoginDialog() { AccessToken=accesstoken };
                    var ret = dlgLogin.ShowDialog();
                    result = dlgLogin.Tokens;
                }
            }
            catch(Exception ex)
            {
                MetroWindow window = Application.Current.MainWindow as MetroWindow;
                await window.ShowMessageAsync("ERROR", ex.Message);
            }
            return (result);
        }

        public static string ToLineBreak(this string text, int lineLength)
        {
            if (string.IsNullOrEmpty(text)) return (string.Empty);
            //return Regex.Replace(text, @"(.{" + lineLength + @"})", "$1" + Environment.NewLine);
            var t = Regex.Replace(text, @"[\n\r]", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            //t = Regex.Replace(t, @"<[^>]*>", "$1", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"(<br *?/>)", Environment.NewLine, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"(<a .*?>(.*?)</a>)|(<strong>(.*?)</strong>)", "$2", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"<.*?>(.*?)</.*?>", "$1", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return Regex.Replace(t, @"(.{" + lineLength + @"})", "$1" + Environment.NewLine, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        }

        // To return an array of strings instead:
        public static string[] Slice(this string text, int lineLength)
        {
            if (string.IsNullOrEmpty(text)) return (new string[] { });
            //return Regex.Matches(text, @"(.{" + lineLength + @"})").Cast<Match>().Select(m => m.Value).ToArray();
            var t = Regex.Replace(text, @"[\n\r]", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"(<br *?/>)", Environment.NewLine, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"(<a .*?>(.*?)</a>)|(<strong>(.*?)</strong>)", "$2", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"<.*?>(.*?)</.*?>", "$1", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return Regex.Matches(t, @"(.{" + lineLength + @"})", RegexOptions.IgnoreCase | RegexOptions.Multiline).Cast<Match>().Select(m => m.Value).ToArray();
        }

        public static async Task<ImageSource> GetImageFromURL(this string url)
        {
            ImageSource result = null;

            var uri = new Uri(url);
            var webRequest = WebRequest.CreateDefault(uri);
            var ext = Path.GetExtension(url).ToLower();
            switch (ext)
            {
                case ".jpeg":
                case ".jpg":
                    webRequest.ContentType = "image/jpeg";
                    break;
                case ".png":
                    webRequest.ContentType = "image/png";
                    break;
                case ".bmp":
                    webRequest.ContentType = "image/bmp";
                    break;
                case ".gif":
                    webRequest.ContentType = "image/gif";
                    break;
                case ".tiff":
                case ".tif":
                    webRequest.ContentType = "image/tiff";
                    break;
                default:
                    webRequest.ContentType = "application/octet-stream";
                    break;
            }

            var proxy = Setting.ProxyServer();
            var useproxy = Setting.UseProxy();
            HttpClientHandler handler = new HttpClientHandler()
            {
                Proxy = string.IsNullOrEmpty(Setting.ProxyServer()) ? null : new WebProxy(proxy, true, new string[] { "127.0.0.1", "localhost", "192.168.1" }),
                UseProxy = string.IsNullOrEmpty(proxy) || !useproxy ? false : true
            };
            using (HttpClient client = new HttpClient(handler))
            {
                HttpResponseMessage response = await client.GetAsync(url);
                byte[] content = await response.Content.ReadAsByteArrayAsync();
                //return "data:image/png;base64," + Convert.ToBase64String(content);
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = new MemoryStream(content);
                image.EndInit();
                image.Freeze();
                result = image;
            }

            //await webRequest.BeginGetResponse((ar) =>
            //{
            //    var response = webRequest.EndGetResponse(ar);
            //    var stream = response.GetResponseStream();
            //    if (stream.CanRead)
            //    {
            //        byte[] buffer = new byte[response.ContentLength];
            //        stream.BeginRead(buffer, 0, buffer.Length, (aResult) =>
            //        {
            //            stream.EndRead(aResult);
            //            //File.WriteAllBytes("c:\\test.jpg", buffer);
            //            BitmapImage image = new BitmapImage();
            //            image.BeginInit();
            //            image.StreamSource = new MemoryStream(buffer);
            //            image.EndInit();
            //            image.Freeze();
            //            result = image;
            //        }, null);
            //    }
            //}, null);

            return (result);
        }

        public static async Task<ImageSource> ToImageSource(this string url, Pixeez.Tokens tokens)
        {
            BitmapImage result = null;
            url = Regex.Replace(url, @"//.*?\.pixiv.net/", "//i.pximg.net/", RegexOptions.IgnoreCase);
            using (var response = await tokens.SendRequestAsync(Pixeez.MethodType.GET, url))
            {
                if (response.Source.StatusCode == HttpStatusCode.OK)
                    result = (BitmapImage)await response.ToImageSource();
                else
                    result = null;
            }
            return (result);
        }

        public static async Task<ImageSource> ToImageSource(this Pixeez.AsyncResponse response)
        {
            BitmapImage result = null;
            using (var stream = await response.GetResponseStreamAsync())
            {
                result = (BitmapImage)stream.ToImageSource();
            }
            return (result);
        }

        public static ImageSource ToImageSource(this Stream stream)
        {
            //await imgStream.GetResponseStreamAsync();
            BitmapImage result = new BitmapImage();
            result.BeginInit();
            result.StreamSource = stream;
            result.EndInit();
            result.Freeze();
            return (result);
        }
    }

    [JsonObject(MemberSerialization.OptOut)]
    class Setting
    {
        private static string AppPath = Path.GetDirectoryName(Application.ResourceAssembly.CodeBase.ToString()).Replace("file:\\", "");
        private static string config = Path.Combine(AppPath, "config.json");
        private static Setting Cache = null;// Load(config);

        [JsonIgnore]
        private string username = string.Empty;
        [JsonIgnore]
        public string User
        {
            get
            {
                return (username);
            }
            set
            {
                username = value;
            }
        }

        [JsonIgnore]
        private string password = string.Empty;
        [JsonIgnore]
        public string Pass
        {
            get
            {
                return (password);
            }
            set
            {
                password = value;
            }
        }

        [JsonIgnore]
        private Pixeez.Objects.User myinfo = null;
        [JsonIgnore]
        public Pixeez.Objects.User MyInfo
        {
            get { return myinfo; }
            set { myinfo = value; }
        }

        [JsonIgnore]
        private long update = 0;
        [JsonIgnore]
        public long Update
        {
            get { return update; }
            set { update = value; }
        }

        private string accesstoken = string.Empty;
        public string AccessToken
        {
            get
            {
                return (accesstoken);
            }
            set
            {
                accesstoken = value;
            }
        }

        private string proxy = string.Empty;
        public string Proxy
        {
            get
            {
                return (proxy);
            }
            set
            {
                proxy = value;
            }
        }

        private bool useproxy = false;
        public bool UsingProxy
        {
            get
            {
                return (useproxy);
            }
            set
            {
                useproxy = value;
            }
        }

        public bool Save(string configfile = "")
        {
            bool result = false;
            try
            {
                if (!string.IsNullOrEmpty(configfile)) config = configfile;
                var text = JsonConvert.SerializeObject(Cache, Formatting.Indented);
                File.WriteAllText(config, text, new UTF8Encoding(true));
                result = true;
            }
            catch (Exception) { }
            return (result);
        }

        public static Setting Load(string configfile="")
        {
            Setting result = new Setting();
            try
            {
                if (Cache is Setting) result = Cache;
                else
                {
                    if (!string.IsNullOrEmpty(configfile)) config = configfile;
                    if (File.Exists(config))
                    {
                        var text = File.ReadAllText(config);
                        Cache = JsonConvert.DeserializeObject<Setting>(text);
                        result = Cache;
                    }
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static string Token()
        {
            string result = null;
            if (Cache is Setting) result = Cache.AccessToken;
            return (result);
        }

        public static string ProxyServer()
        {
            string result = null;
            if (Cache is Setting) result = Cache.Proxy;
            return (result);
        }

        public static bool UseProxy()
        {
            bool result = false;
            if (Cache is Setting) result = Cache.UsingProxy;
            return (result);
        }

        public static bool Token(string token)
        {
            bool result = false;
            if (Cache is Setting)
            {
                Cache.AccessToken = token;
                result = true;
            }
            return (result);
        }

        public static string GetDeviceId()
        {
            string location = @"SOFTWARE\Microsoft\Cryptography";
            string name = "MachineGuid";

            using (RegistryKey localMachineX64View = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey rk = localMachineX64View.OpenSubKey(location))
                {
                    if (rk == null)
                        throw new KeyNotFoundException(string.Format("Key Not Found: {0}", location));

                    object machineGuid = rk.GetValue(name);
                    if (machineGuid == null)
                        throw new IndexOutOfRangeException(string.Format("Index Not Found: {0}", name));

                    return machineGuid.ToString().Replace("-","");
                }
            }
        }
    }
}
