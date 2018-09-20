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
using MahApps.Metro;

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
                if (Convert.ToInt64(DateTime.Now.ToFileTime() / 10000000) - setting.Update < 3600)
                {
                    result = Pixeez.Auth.AuthorizeWithAccessToken(setting.AccessToken, setting.Proxy, setting.UsingProxy);
                }
                else
                {
                    if (!string.IsNullOrEmpty(setting.User) && !string.IsNullOrEmpty(setting.Pass) && !string.IsNullOrEmpty(setting.AccessToken))
                    {
                        try
                        {
                            var authResult = await Pixeez.Auth.AuthorizeAsync(setting.User, setting.Pass, setting.AccessToken, setting.Proxy, setting.UsingProxy);
                            //var authResult = await Pixeez.Auth.AuthorizeAsync(setting.User, setting.Pass, "", setting.Proxy, setting.UsingProxy);
                            setting.AccessToken = authResult.Authorize.AccessToken;
                            setting.Update = Convert.ToInt64(DateTime.Now.ToFileTime() / 10000000);
                            result = authResult.Tokens;
                        }
                        catch (Exception)
                        {
                            result = Pixeez.Auth.AuthorizeWithAccessToken(setting.AccessToken, setting.Proxy, setting.UsingProxy);
                        }
                    }
                    else
                    {
                        var dlgLogin = new PixivLoginDialog() { AccessToken=accesstoken };
                        var ret = dlgLogin.ShowDialog();
                        result = dlgLogin.Tokens;
                    }
                }
            }
            catch(Exception ex)
            {
                MetroWindow window = Application.Current.MainWindow as MetroWindow;
                await window.ShowMessageAsync("ERROR", ex.Message);
            }
            return (result);
        }

        public static string InsertLineBreak(this string text, int lineLength)
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

    public static class Theme
    {
        private static List<string> accents = new List<string>() {
                //"BaseDark","BaseLight",
                "Amber","Blue","Brown","Cobalt","Crimson","Cyan", "Emerald","Green",
                "Indigo","Lime","Magenta","Mauve","Olive","Orange", "Pink",
                "Purple","Red","Sienna","Steel","Taupe","Teal","Violet","Yellow"
        };
        private static Setting setting = Setting.Load();
        public static IList<string> Accents
        {
            get { return accents; }
        }

        public static void Toggle()
        {
            Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
            var appTheme = appStyle.Item1;
            var appAccent = appStyle.Item2;

            var target = ThemeManager.GetInverseAppTheme(appTheme);
            ThemeManager.ChangeAppStyle(Application.Current, appAccent, target);
            setting.Theme = target.Name;
            setting.Save();
        }

        public static string CurrentAccent
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return appAccent.Name;
            }
            set
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                ThemeManager.ChangeAppStyle(Application.Current, ThemeManager.GetAccent(value), appTheme);
                setting.Accent = value;
                setting.Save();
            }
        }

        public static string CurrentTheme
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return appTheme.Name;
            }
            set
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                ThemeManager.ChangeAppStyle(Application.Current, appAccent, ThemeManager.GetAppTheme(value));
                setting.Theme = value;
                setting.Save();
            }
        }

        public static Color WhiteColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["WhiteColor"] as Brush).ToColor();
            }
        }

        public static Brush WhiteBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["WhiteBrush"] as Brush);
            }
        }

        public static Color BlackColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["BlackColor"] as Brush).ToColor();
            }
        }

        public static Brush BlackBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["BlackBrush"] as Brush);
            }
        }

        public static Color AccentColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (Color)appAccent.Resources["AccentColor"];
            }
        }

        public static Brush AccentBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return appAccent.Resources["AccentColorBrush"] as Brush;
            }
        }

        public static Color TextColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["TextBrush"] as Brush).ToColor();
            }
        }

        public static Brush TextBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["TextBrush"] as Brush);
            }
        }

        public static Color LabelTextColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["LabelTextBrush"] as Brush).ToColor();
            }
        }

        public static Brush LabelTextBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["LabelTextBrush"] as Brush);
            }
        }

        public static Color IdealForegroundColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["IdealForegroundColorBrush"] as Brush).ToColor();
            }
        }

        public static Brush IdealForegroundBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["IdealForegroundColorBrush"] as Brush);
            }
        }

        public static Color ToColor(this Brush b, bool prefixsharp = true)
        {
            if(b is SolidColorBrush)
            {
                return (b as SolidColorBrush).Color;
            }
            else
            {
                var hc = b.ToString();//.Replace("#", "");
                var c = System.Drawing.ColorTranslator.FromHtml(hc);
                var rc = Color.FromArgb(c.A, c.R, c.G, c.B);
                return (rc);
            }
        }

        public static string ToHtml(this Brush b, bool prefixsharp = true)
        {
            if (prefixsharp)
                return (b.ToString());
            else
                return (b.ToString().Replace("#", ""));
        }

        public static string ToHtml(this Color c, bool alpha = true, bool prefixsharp=true)
        {
            string result = string.Empty;

            if (alpha)
                result = string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
            else
                result = string.Format("{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);

            if (prefixsharp)
                result = $"#{result}";

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

        //[JsonIgnore]
        private long update = 0;
        //[JsonIgnore]
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

        private string theme = string.Empty;
        public string Theme { get; set; }

        private string accent = string.Empty;
        public string Accent { get; set; }

        private string lastSaveFolder = string.Empty;
        public string SaveFolder { get; set; }


        public async Task<bool> Save(string configfile = "")
        {
            bool result = false;
            try
            {
                if (string.IsNullOrEmpty(configfile)) configfile = config;
                var text = JsonConvert.SerializeObject(Cache, Formatting.Indented);
                File.WriteAllText(configfile, text, new UTF8Encoding(true));
                result = true;
            }
            catch (Exception ex)
            {
                MetroWindow window = Application.Current.MainWindow as MetroWindow;
                await window.ShowMessageAsync("ERROR", ex.Message);
            }
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
                    if (string.IsNullOrEmpty(configfile)) configfile = config;
                    if (File.Exists(config))
                    {
                        var text = File.ReadAllText(configfile);
                        if (text.Length < 20)
                            Cache = new Setting();
                        else
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
