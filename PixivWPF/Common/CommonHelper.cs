using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Management;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Dfust.Hotkeys;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using WPFNotification.Core.Configuration;
using WPFNotification.Model;
using WPFNotification.Services;
using PixivWPF.Pages;

namespace PixivWPF.Common
{
    #region Page enmu type
    public enum PixivPage
    {
        None,
        TrendingTags,
        WorkSet,
        Recommanded,
        Latest,
        My,
        MyFollowerUser,
        MyFollowingUser,
        MyFollowingUserPrivate,
        MyPixivUser,
        MyBlacklistUser,
        MyWork,
        User,
        UserWork,
        Feeds,
        Favorite,
        FavoritePrivate,
        Follow,
        FollowPrivate,
        Bookmark,
        MyBookmark,
        RankingDay,
        RankingDayMale,
        RankingDayFemale,
        RankingDayR18,
        RankingDayMaleR18,
        RankingDayFemaleR18,
        RankingDayManga,
        RankingWeek,
        RankingWeekOriginal,
        RankingWeekRookie,
        RankingWeekR18,
        RankingWeekR18G,
        RankingMonth,
        RankingYear,
        About
    }
    #endregion

    public enum ToastType { DOWNLOAD = 0, OK, OKCANCEL, YES, NO, YESNO };

    public enum AutoExpandMode { OFF = 0, ON, AUTO, SINGLEPAGE };

    public class WebBrowserEx : System.Windows.Forms.WebBrowser
    {
        internal new void Dispose(bool disposing)
        {
            // call WebBrower.Dispose(bool)
            base.Dispose(disposing);
        }

        private bool ignore_all_error = false;
        public bool IgnoreAllError
        {
            get { return (ignore_all_error); }
            set
            {
                ignore_all_error = value;
                if (value) SuppressedAllError();
            }
        }

        /// <summary>
        /// code from -> https://stackoverflow.com/a/13788814/1842521
        /// </summary>
        private void SuppressedAllError()
        {
            ScriptErrorsSuppressed = true;

            try
            {
                FieldInfo field = typeof(WebBrowserEx).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    object axIWebBrowser2 = field.GetValue(this);
                    axIWebBrowser2.GetType().InvokeMember("Silent", BindingFlags.SetProperty, null, axIWebBrowser2, new object[] { true });
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }
    }

    public class HtmlTextData
    {
        public string Html { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    #region DPI Helper
    public class DPI
    {
        public double ScaleX { get; } = 1.0;
        public double ScaleY { get; } = 1.0;

        public double X { get; } = 96.0;
        public double Y { get; } = 96.0;

        public double X15 { get; } = 144.0;
        public double Y15 { get; } = 144.0;

        public double X20 { get; } = 192.0;
        public double Y20 { get; } = 192.0;

        private static DPI dpi = new DPI();
        public static DPI Default
        {
            get { return (dpi); }
            set { dpi = value; }
        }

        public DPI()
        {
            var dpi = BySystemParameters();
            ScaleX = dpi.ScaleX;
            ScaleY = dpi.ScaleY;
            X = dpi.X;
            Y = dpi.Y;
            X15 = X * 1.5;
            Y15 = Y * 1.5;
            X20 = X * 2.0;
            Y20 = Y * 2.0;
        }

        public DPI(double x, double y, double scale_x = 1.0, double scale_y = 1.0)
        {
            ScaleX = scale_x;
            ScaleY = scale_y;
            X = x;
            Y = y;
            X15 = X * 1.5;
            Y15 = Y * 1.5;
            X20 = X * 2.0;
            Y20 = Y * 2.0;
        }

        public DPI(Visual visual)
        {
            try
            {
                dpi = GetDefault(visual);
                ScaleX = dpi.ScaleX;
                ScaleY = dpi.ScaleY;
                X = dpi.X;
                Y = dpi.Y;
                X15 = X * 1.5;
                Y15 = Y * 1.5;
                X20 = X * 2.0;
                Y20 = Y * 2.0;
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static DPI GetDefault(Visual visual)
        {
            var result = new DPI();
            try
            {
                var ds = VisualTreeHelper.GetDpi(visual);
                var x = ds.PixelsPerInchX;
                var y = ds.PixelsPerInchY;
                var sx = ds.DpiScaleX;
                var sy = ds.DpiScaleY;
                dpi = new DPI(x, y, sx, sy);
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static DPI FromVisual(Visual visual)
        {
            var source = PresentationSource.FromVisual(visual);
            var dpiX = 96.0;
            var dpiY = 96.0;
            var scaleX = 1.0;
            var scaleY = 1.0;
            try
            {
                if (source?.CompositionTarget != null)
                {
                    scaleX = source.CompositionTarget.TransformToDevice.M11;
                    scaleY = source.CompositionTarget.TransformToDevice.M22;
                    dpiX = 96.0 * scaleX;
                    dpiY = 96.0 * scaleY;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return new DPI(dpiX, dpiY, scaleX, scaleY);
        }

        public static DPI BySystemParameters()
        {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            var dpiXProperty = typeof(SystemParameters).GetProperty("DpiX", flags);
            //var dpiYProperty = typeof(SystemParameters).GetProperty("DpiY", flags);
            var dpiYProperty = typeof(SystemParameters).GetProperty("Dpi", flags);
            var dpiX = 96.0;
            var dpiY = 96.0;
            var scaleX = 1.0;
            var scaleY = 1.0;
            try
            {
                if (dpiXProperty != null) { dpiX = (int)dpiXProperty.GetValue(null, null); }
                if (dpiYProperty != null) { dpiY = (int)dpiYProperty.GetValue(null, null); }
            }
            catch (Exception ex) { ex.ERROR(); }
            return new DPI(dpiX, dpiY, scaleX, scaleY);
        }
    }
    #endregion

    #region Custom storage type
    public class StorageType
    {
        [JsonProperty("Folder")]
        public string Folder { get; set; } = string.Empty;
        [JsonProperty("Cached")]
        public bool Cached { get; set; } = true;
        [JsonProperty("IncludeSubFolder")]
        public bool IncludeSubFolder { get; set; } = false;

        [JsonIgnore]
        public int Count { get; set; } = -1;

        public override string ToString()
        {
            return Folder;
        }

        public StorageType(string path, bool cached = false)
        {
            Folder = path;
            Cached = cached;
            Count = -1;
        }
    }
    #endregion

    #region CustomImage
    public class CustomImageSource : IDisposable
    {
        public ImageSource Source { get; set; } = null;
        public long Size { get; set; } = 0;
        public long ColorDepth { get; set; } = 0;
        public string SourcePath { get; set; } = string.Empty;

        public CustomImageSource()
        {
        }

        public CustomImageSource(ImageSource source, string path)
        {
            Source = source;
            SourcePath = path;
        }

        ~CustomImageSource()
        {
            Dispose(false);
        }

        public void Close()
        {
            Dispose();
        }

        private bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                SourcePath = null;
                Source = null;
            }
            disposed = true;
        }
    }
    #endregion

    public static class CommonHelper
    {
        private static Setting setting = Application.Current.LoadSetting();
        private static CacheImage cache = new CacheImage();
        private static ConcurrentDictionary<long?, Pixeez.Objects.Work> IllustCache = new ConcurrentDictionary<long?, Pixeez.Objects.Work>();
        private static ConcurrentDictionary<long?, Pixeez.Objects.UserBase> UserCache = new ConcurrentDictionary<long?, Pixeez.Objects.UserBase>();
        private static ConcurrentDictionary<long?, Pixeez.Objects.UserInfo> UserInfoCache = new ConcurrentDictionary<long?, Pixeez.Objects.UserInfo>();

        private static ConcurrentDictionary<string, string> _TagsCache = null;
        public static ConcurrentDictionary<string, string> TagsCache
        {
            get { if (_TagsCache == null) _TagsCache = new ConcurrentDictionary<string, string>(); return (_TagsCache); }
        }
        private static ConcurrentDictionary<string, string> _TagsT2S = null;
        public static ConcurrentDictionary<string, string> TagsT2S
        {
            get { if (_TagsT2S == null) _TagsT2S = new ConcurrentDictionary<string, string>(); return (_TagsT2S); }
        }
        private static ConcurrentDictionary<string, string> _TagsWildecardT2S = null;
        public static ConcurrentDictionary<string, string> TagsWildecardT2S
        {
            get { if (_TagsWildecardT2S == null) _TagsWildecardT2S = new ConcurrentDictionary<string, string>(); return (_TagsWildecardT2S); }
        }

        private static List<string> ext_imgs = new List<string>() { ".png", ".jpg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".jpeg" };
        private static char[] trim_char = new char[] { ' ', ',', '.', '/', '\\', '\r', '\n', ':', ';' };
        private static string[] trim_str = new string[] { Environment.NewLine };
        private static string regex_img_ext = @"\.(png|jpg|jpeg|gif|bmp|zip|webp)";
        private static string regex_symbol = @"([\u0020-\u002F\u003A-\u0040\u005B-\u005E\u007B-\u007E])";

        #region Pixiv Token Helper
        private static SemaphoreSlim CanRefreshToken = new SemaphoreSlim(1, 1);
        private static async Task<Pixeez.Tokens> RefreshToken()
        {
            Pixeez.Tokens result = null;
            if (await CanRefreshToken.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                try
                {
                    setting = Application.Current.LoadSetting();
                    var authResult = await Pixeez.Auth.AuthorizeAsync(setting.User, setting.Pass, setting.RefreshToken, setting.Proxy, setting.ProxyBypass, setting.UsingProxy);
                    setting.AccessToken = authResult.Authorize.AccessToken;
                    setting.RefreshToken = authResult.Authorize.RefreshToken;
                    setting.ExpTime = authResult.Key.KeyExpTime.ToLocalTime();
                    setting.ExpiresIn = authResult.Authorize.ExpiresIn.Value;
                    setting.Update = DateTime.Now.ToFileTime().FileTimeToSecond();
                    setting.MyInfo = authResult.Authorize.User;
                    setting.Save();
                    result = authResult.Tokens;
                }
                catch (Exception ex)
                {
                    if (!string.IsNullOrEmpty(setting.User) && !string.IsNullOrEmpty(setting.Pass))
                    {
                        try
                        {
                            setting = Application.Current.LoadSetting();
                            var authResult = await Pixeez.Auth.AuthorizeAsync(setting.User, setting.Pass, setting.Proxy, setting.ProxyBypass.ToArray(), setting.UsingProxy);
                            setting.AccessToken = authResult.Authorize.AccessToken;
                            setting.RefreshToken = authResult.Authorize.RefreshToken;
                            setting.ExpTime = authResult.Key.KeyExpTime.ToLocalTime();
                            setting.ExpiresIn = authResult.Authorize.ExpiresIn.Value;
                            setting.Update = DateTime.Now.ToFileTime().FileTimeToSecond();
                            setting.MyInfo = authResult.Authorize.User;
                            setting.Save();
                            result = authResult.Tokens;
                        }
                        catch (Exception exx)
                        {
                            var ret = exx.Message;
                            var tokens = await ShowLogin();
                        }
                    }
                    var rt = ex.Message;
                }
                finally
                {
                    if (CanRefreshToken is SemaphoreSlim && CanRefreshToken.CurrentCount <= 0) CanRefreshToken.Release();
                }
            }
            return (result);
        }

        private static SemaphoreSlim CanShowLogin = new SemaphoreSlim(1, 1);
        public static async Task<Pixeez.Tokens> ShowLogin(bool force = false)
        {
            Pixeez.Tokens result = null;
            if (await CanShowLogin.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                try
                {
                    if (GetWindow<PixivLoginDialog>() is MetroWindow) return (result);
                    Application.Current.DoEvents();
                    await Task.Delay(1);

                    setting = Application.Current.LoadSetting();
                    if (!force && setting.ExpTime > DateTime.Now && !string.IsNullOrEmpty(setting.AccessToken))
                    {
                        result = Pixeez.Auth.AuthorizeWithAccessToken(setting.AccessToken, setting.RefreshToken, setting.Proxy, setting.ProxyBypass, setting.UsingProxy);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(setting.User) && !string.IsNullOrEmpty(setting.Pass) && !string.IsNullOrEmpty(setting.RefreshToken))
                        {
                            try
                            {
                                result = await RefreshToken();
                            }
                            catch (Exception ex)
                            {
                                ex.ERROR("SHOWLOGIN");
                                result = Pixeez.Auth.AuthorizeWithAccessToken(setting.AccessToken, setting.RefreshToken, setting.Proxy, setting.ProxyBypass, setting.UsingProxy);
                            }
                        }
                        else
                        {
                            "Show Login Dialog...".INFO();
                            Application.Current.DoEvents();
                            var dlgLogin = new PixivLoginDialog() { AccessToken=setting.AccessToken, RefreshToken=setting.RefreshToken };
                            var ret = dlgLogin.ShowDialog();
                            result = dlgLogin.Tokens;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ex.Message.ShowMessageBox("ERROR");
                }
                finally
                {
                    if (result == null) "Request Token Error!".ShowToast("ERROR");
                    if (CanShowLogin is SemaphoreSlim && CanShowLogin.CurrentCount <= 0) CanShowLogin.Release();
                }
            }
            return (result);
        }

        public static string AccessToken(this Application app)
        {
            var result = string.Empty;
            try
            {
                setting = app.LoadSetting();
                result = setting.AccessToken;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static string RefreshToken(this Application app)
        {
            var result = string.Empty;
            try
            {
                setting = app.LoadSetting();
                result = setting.RefreshToken;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static bool DownloadUsingToken(this Application app)
        {
            var setting = Application.Current.LoadSetting();
            return (setting.DownloadByAPI && !string.IsNullOrEmpty(setting.AccessToken) && setting.ExpTime <= DateTime.Now);
        }
        #endregion

        #region WebBrowser helper
        public static string GetText(this System.Windows.Forms.WebBrowser browser, bool html = false, bool all_without_selection = true)
        {
            string result = string.Empty;
            try
            {
                if (browser is System.Windows.Forms.WebBrowser &&
                    browser.Document is System.Windows.Forms.HtmlDocument &&
                    browser.Document.DomDocument is MSHTML.IHTMLDocument2)
                {
                    StringBuilder sb = new StringBuilder();
                    MSHTML.IHTMLDocument2 document = browser.Document.DomDocument as MSHTML.IHTMLDocument2;
                    MSHTML.IHTMLSelectionObject currentSelection = document.selection;
                    if (currentSelection != null && currentSelection.type.Equals("Text", StringComparison.CurrentCultureIgnoreCase))
                    {
                        MSHTML.IHTMLTxtRange range = currentSelection.createRange() as MSHTML.IHTMLTxtRange;
                        if (range != null)
                            sb.AppendLine(html ? range.htmlText : range.text);
                    }
                    else if (all_without_selection)
                    {
                        var bodies = browser.Document.GetElementsByTagName("body");
                        foreach (System.Windows.Forms.HtmlElement body in bodies)
                        {
                            sb.AppendLine(html ? body.InnerHtml : body.InnerText);
                        }
                    }
                    result = sb.Length > 0 ? sb.ToString().Trim().KatakanaHalfToFull() : string.Empty;
                }
            }
            catch (Exception ex) { ex.ERROR("GetBrowserText"); }
            return (result);
        }
        #endregion

        #region Link parsing/genaration helper
        public static bool IsFile(this string text)
        {
            var result = false;
            Uri unc = null;
            var invalid = new List<char> { '<', ':', '>' };
            try
            {
                if (!string.IsNullOrEmpty(text) && !invalid.Contains(text.FirstOrDefault()) && Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out unc))
                {
                    result = unc.IsAbsoluteUri ? unc.IsFile : false;
                }
            }
            catch (Exception ex) { ex.ERROR("IsFile"); }
            return (result);
        }

        public static IList<string> ParseDragContent(this DragEventArgs e)
        {
            List<string> links = new List<string>();

            var fmts = new List<string>(e.Data.GetFormats(true));

            var str = fmts.Contains("System.String") ? (string)e.Data.GetData("System.String") : string.Empty;
            var text = fmts.Contains("Text") ? (string)e.Data.GetData("Text") : string.Empty;
            var unicode = fmts.Contains("UnicodeText") ? (string)e.Data.GetData("UnicodeText") : string.Empty;

            if (fmts.Contains("text/html"))
            {
                using (var ms = (MemoryStream)e.Data.GetData("text/html"))
                {
                    var bytes = ms.ToArray();
                    var IsUnicode = bytes.Length>=4 && bytes[1] == 0x00 && bytes[3] == 0x00;
                    if (IsUnicode)
                    {
                        var html = Encoding.Unicode.GetString(bytes).Trim().Trim('\0');
                        links = html.ParseLinks(true).ToList();
                    }
                    else
                    {
                        var html = Encoding.Unicode.GetString(bytes).Trim().Trim('\0');
                        if (!string.IsNullOrEmpty(text) && html.Contains(text))
                            links = html.ParseLinks(true).ToList();
                        else
                        {
                            html = Encoding.UTF8.GetString(ms.ToArray()).Trim().Trim('\0');
                            links = html.ParseLinks(true).ToList();
                        }
                    }
                }
            }
            else if (fmts.Contains("System.String"))
            {
                var html = ((string)e.Data.GetData("System.String")).Trim().Trim('\0');
                links = html.ParseLinks(false).ToList();
            }
            else if (fmts.Contains("UnicodeText"))
            {
                var html = ((string)e.Data.GetData("UnicodeText")).Trim().Trim('\0');
                links = html.ParseLinks(false).ToList();
            }
            else if (fmts.Contains("Text"))
            {
                var html = ((string)e.Data.GetData("Text")).Trim().Trim('\0');
                links = html.ParseLinks(false).ToList();
            }
            else if (fmts.Contains("FileDrop"))
            {
                var files = (string[])(e.Data.GetData("FileDrop"));
                links = string.Join(Environment.NewLine, files).ParseLinks(false).ToList();
            }
            return (links);
        }

        public static string ParseID(this string searchContent)
        {
            var patten =  @"((UserID)|(IllustID)|(User)|(Tag)|(Caption)|(Fuzzy)|(Fuzzy Tag)|(Downloading)):\s*(.*?)$";
            string result = searchContent;
            if (!string.IsNullOrEmpty(result))
            {
                result = Regex.Replace(result, patten, "$10", RegexOptions.IgnoreCase).Trim().Trim(trim_char);
            }
            return (result);
        }

        public static string ParseLink(this string link)
        {
            string result = link;

            if (!string.IsNullOrEmpty(link))
            {
                if (Regex.IsMatch(result, @"((UserID)|(IllustID)):( )*(\d+)", RegexOptions.IgnoreCase))
                    result = result.Trim();

                else if (Regex.IsMatch(result, @"(.*?/artworks/)(\d+)(.*)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?/artworks/)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?illust_id=)(\d+)(.*)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?illust_id=)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?/pixiv\.navirank\.com/id/)(\d+)(.*)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?/id/)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase);

                else if (Regex.IsMatch(result, @"^(.*?\.pixiv.net/users/)(\d+)(.*)$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?\.pixiv.net/users/)(\d+)(.*)$", "UserID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"^(.*?\.pixiv.net/fanbox/creator/)(\d+)(.*)$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?\.pixiv.net/fanbox/creator/)(\d+)(.*)$", "UserID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"^(.*?\?id=)(\d+)(.*)$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?\?id=)(\d+)(.*)$", "UserID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?/pixiv\.navirank\.com/user/)(\d+)(.*)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?/user/)(\d+)(.*)", "UserID: $2", RegexOptions.IgnoreCase);

                else if (Regex.IsMatch(result, @"^(.*?tag_full&word=)(.*)$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?tag_full&word=)(.*)$", "Tag: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?\.pixiv\.net/tags/)(.*?){1}(/.*?)*$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?/tags/)(.*?){1}(/.*?)*", "Tag: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?/pixiv\.navirank\.com/tag/)(.*?)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?/tag/)(.*?)", "Tag: $2", RegexOptions.IgnoreCase);

                else if (Regex.IsMatch(result, @"^(.*?/img-.*?/)(\d+)(_p\d+.*?" + regex_img_ext + ")$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?/img-.*?/)(\d+)(_p\d+.*?" + regex_img_ext + ")$", "IllustID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"^(.*?)/\d{4}/\d{2}/\d{2}/\d{2}/\d{2}/\d{2}/(\d+).*?" + regex_img_ext + "$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?)/\d{4}/\d{2}/\d{2}/\d{2}/\d{2}/\d{2}/(\d+).*?" + regex_img_ext + "$", "IllustID: $2", RegexOptions.IgnoreCase);


                else if (Regex.IsMatch(Path.GetFileNameWithoutExtension(result), @"^((\d+)(_((p)|(ugoira))*\d+)*)"))
                    result = Regex.Replace(Path.GetFileNameWithoutExtension(result), @"(.*?(\d+)(_((p)|(ugoira))*\d+)*.*)", "$2", RegexOptions.IgnoreCase);

                else if (!Regex.IsMatch(result, @"((UserID)|(User)|(IllustID)|(Tag)|(Caption)|(Fuzzy)|(Fuzzy Tag)):", RegexOptions.IgnoreCase))
                    result = $"Fuzzy: {result}";
            }

            return (result.Trim().Trim(trim_char).HtmlDecode());
        }

        public static IList<string> ParseLinks(this string html, bool is_src = false)
        {
            List<string> links = new List<string>();
            var href_prefix_0 = is_src ? @"href=""" : string.Empty;
            var href_prefix_1 = is_src ? @"src=""" : string.Empty;
            var href_suffix = is_src ? @"""" : @"";
            var cmd_sep = new char[] { ':', ' ', '=' };

            var opt = RegexOptions.IgnoreCase;// | RegexOptions.Multiline;

            var mr = new List<MatchCollection>();
            foreach (var text in html.Split(new string[] { Environment.NewLine, "\n", "\r", "\t", "<br/>", "<br>", "<br />", "><", "</a>" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var content = text.StartsWith("\"") && text.EndsWith("\"") ? text.Trim('"').Trim() : text.Trim();
                if (string.IsNullOrEmpty(content)) continue;
                else if (content.Equals("<a", StringComparison.CurrentCultureIgnoreCase)) continue;
                else if (content.Equals("<img", StringComparison.CurrentCultureIgnoreCase)) continue;
                else if (content.Equals(">", StringComparison.CurrentCultureIgnoreCase)) continue;

                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/(en/)?artworks/\d+)" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/(en/)?users/\d+)" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/member.*?\.php\?.*?illust_id=\d+).*?" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/member.*?\.php\?id=\d+).*?" + href_suffix, opt));

                mr.Add(Regex.Matches(content, href_prefix_0 + @"(.*?\.pximg\.net/img-.*?/\d+_p\d+" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(.*?\.pximg\.net/img-.*?/(\d+)_p\d+.*?" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(.*?\.pximg\.net/.*?/img(-(master|original))?/.*?/\d+_p\d+(_.*?)?" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://.*?\.pximg\.net/.*?/img/\d{4}/\d{2}/\d{2}/\d{2}/\d{2}/\d{2}/(\d+)_p\d+.*?" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_1 + @"(.*?\.pximg\.net/img-.*?/\d+_p\d+" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_1 + @"(.*?\.pximg\.net/img-.*?/(\d+)_p\d+.*?" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_1 + @"(.*?\.pximg\.net/.*?/img/.*?/\d+_p\d+" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_1 + @"(https?://.*?\.pximg\.net/.*?/img/\d{4}/\d{2}/\d{2}/\d{2}/\d{2}/\d{2}/(\d+)_p\d+.*?" + regex_img_ext + ")" + href_suffix, opt));

                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/fanbox/creator/\d+).*?" + href_suffix, opt));

                mr.Add(Regex.Matches(content, href_prefix_0 + @"https?://.*?\.pixiv\.net/(tags/(.*?){1})(/.*?)*$" + href_suffix, opt));

                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://pixiv\.navirank\.com/id/\d+).*?" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://pixiv\.navirank\.com/user/\d+).*?" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://pixiv\.navirank\.com/tag/.*?/)" + href_suffix, opt));

                mr.Add(Regex.Matches(content, @"[\\|/](background|workspace|user-profile)[\\|/].*?[\\|/]((\d+)(_.{10,}" + regex_img_ext + "))", opt));

                mr.Add(Regex.Matches(content, @"^(\d+)([_]*.*?)" + regex_img_ext + "$", opt));

                mr.Add(Regex.Matches(content, @"^((illust|illusts|artworks)/(\d+))", opt));
                mr.Add(Regex.Matches(content, @"^((users?)/(\d+))", opt));

                mr.Add(Regex.Matches(content, @"^((u?id):[ ]*(\d+)+)", opt));
                mr.Add(Regex.Matches(content, @"^((user|fuzzy|tag|title):[ ]*(.+)+)", opt));

                mr.Add(Regex.Matches(content, @"(Searching\s)(.*?)$", opt));

                mr.Add(Regex.Matches(content, @"(Preview\sID:\s)(\d+),(.*?)$", opt));

                mr.Add(Regex.Matches(content, @"((down(all)?|Downloading):\s?.*?)$", opt));

                if (!Regex.IsMatch(content, @"^((https?)|(<a)|(href=)|(src=)|(id:)|(uid:)|(tag:)|(user:)|(title:)|(fuzzy:)|(down(all|load(ing)?)?:)|(illust/)|(illusts/)|(artworks/)|(user/)|(users/)).*?", opt))
                {
                    try
                    {
                        if (content.IsFile())
                        {
                            var ap = Path.GetFullPath(content).Replace('\\', '/');
                            var root = Path.GetPathRoot(ap);
                            var IsFile = root.Length == 3 && string.IsNullOrEmpty(Path.GetExtension(ap)) ? false : true;
                            if (IsFile)
                            {
                                if (Regex.IsMatch(ap, @"[\\|/]((background)(workspace)|(user-profile))[\\|/].*?[\\|/]((\d+)(_.{10,}" + regex_img_ext + "))", opt))
                                    mr.Add(Regex.Matches(ap, @"[\\|/]((workspace)|(user-profile))[\\|/].*?[\\|/]((\d+)(_.{10,}" + regex_img_ext + "))", opt));
                                else
                                    mr.Add(Regex.Matches(Path.Combine(root, Path.GetFileName(content)), @"((\d+)((_((p)|(ugoira))*\d+)*(_((master)|(square))+\d+)*)*(\..+)*)", opt));
                            }
                            else
                                mr.Add(Regex.Matches(content, @"((\d+)((_((p)|(ugoira))*\d+)*(_((master)|(square))+\d+)*)*(\..+)*)", opt));
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.ERROR();
                        mr.Add(Regex.Matches(content, @"((\d+)((_((p)|(ugoira))*\d+)*(_((master)|(square)))*\d+)*(" + regex_img_ext + "))", opt));
                    }
                }
            }

            var download_links = new List<string>();
            foreach (var mi in mr)
            {
                if (mi.Count <= 0) continue;
                else if (mi.Count > 50)
                {
                    ShowMessageBox("There are too many links, which may cause the program to crash and cancel the operation.", "WARNING");
                    continue;
                }
                var linkexists = false;

                foreach (Match m in mi)
                {
                    var link = m.Groups[1].Value.Trim().Trim(trim_char);
                    var downloads = Application.Current.OpenedWindowTitles();
                    downloads = downloads.Concat(download_links).ToList();
                    foreach (var di in downloads)
                    {
                        if (di.Contains(link))
                        {
                            linkexists = true;
                            break;
                        }
                    }
                    if (linkexists) continue;

                    if (link.Equals("user-profile", StringComparison.CurrentCultureIgnoreCase)) break;
                    else if (link.Equals("background", StringComparison.CurrentCultureIgnoreCase) || link.Equals("workspace", StringComparison.CurrentCultureIgnoreCase))
                        link = $"uid:{m.Groups[5].Value.Trim().Trim(trim_char)}";

                    if (link.StartsWith("searching", StringComparison.CurrentCultureIgnoreCase))
                    {
                        link = m.Groups[2].Value.Trim();
                        if (!links.Contains(link)) links.Add(link);
                    }
                    else if (link.StartsWith("preview", StringComparison.CurrentCultureIgnoreCase))
                    {
                        link = m.Groups[2].Value.Trim().ArtworkLink();
                        if (!string.IsNullOrEmpty(link) && !links.Contains(link)) links.Add(link);
                    }
                    else if (link.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                    {
                        //link = Uri.UnescapeDataString(WebUtility.HtmlDecode(link));
                        if (Regex.IsMatch(link, @"(\d+)(_.*?)?" + regex_img_ext + "$", RegexOptions.IgnoreCase))
                        {
                            var id = Regex.Replace(link, @"^.*?/\d{2}/(\d+)(_.*?)?"+regex_img_ext+"$", "$1", RegexOptions.IgnoreCase);
                            link = id.ArtworkLink();
                        }
                        if (!links.Contains(link)) links.Add(link);
                    }
                    else if (link.StartsWith("id:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var id = link.Substring(3).Trim();
                        if (Regex.IsMatch(id, @"(\d+),\s(.+?)", opt))
                            id = Regex.Replace(id, @"(\d+),\s(.+?)", "$1", opt);
                        var a_link = id.ArtworkLink();
                        var a_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={id}";
                        if (!string.IsNullOrEmpty(a_link) && !links.Contains(a_link) && !links.Contains(a_link_o)) links.Add(a_link);
                    }
                    else if (link.StartsWith("illust/", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("illusts/", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("artworks/", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var id = Regex.Replace(link, @"(((illust)|(illusts)|(artworks))/(\d+))", "$6", opt).Trim();
                        var a_link = id.ArtworkLink();
                        var a_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={id}";
                        if (!string.IsNullOrEmpty(a_link) && !links.Contains(a_link) && !links.Contains(a_link_o)) links.Add(a_link);
                    }
                    else if (link.StartsWith("uid:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var id = link.Substring(4).Trim();
                        var u_link = id.ArtistLink();
                        var u_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&id={id}";
                        if (!string.IsNullOrEmpty(u_link) && !links.Contains(u_link) && !links.Contains(u_link_o)) links.Add(u_link);
                    }
                    else if (link.StartsWith("user/", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("users/", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var id = Regex.Replace(link, @"(((user)|(users))/(\d+))", "$5", opt).Trim();
                        var u_link = id.ArtistLink();
                        var u_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&id={id}";
                        if (!string.IsNullOrEmpty(u_link) && !links.Contains(u_link) && !links.Contains(u_link_o)) links.Add(u_link);
                    }
                    //(UserID)|(User)|(IllustID)|(Tag)|(Caption)|(Fuzzy)|(Fuzzy Tag)
                    else if (link.StartsWith("tag:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var tag = link.Substring(4).Trim();
                        var t_link = $"Tag:{tag}";
                        if (!links.Contains(t_link)) links.Add(t_link);
                    }
                    else if (link.StartsWith("tags/", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var tag = link.Substring(5).Trim();
                        var t_link = $"Tag:{Uri.UnescapeDataString(tag)}";
                        if (!links.Contains(t_link)) links.Add(t_link);
                    }
                    else if (link.StartsWith("user:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var user = link.Substring(5).Trim();
                        if (Regex.IsMatch(user, @"(.+)\s/\s(\d+)\s/\s(.+)", opt))
                        {
                            var uid = Regex.Replace(user, @"(.+)\s/\s(\d+)\s/\s(.+)", "$2", opt);
                            var u_link = uid.ArtistLink();
                            var u_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&id={uid}";
                            if (!string.IsNullOrEmpty(u_link) && !links.Contains(u_link) && !links.Contains(u_link_o)) links.Add(u_link);
                        }
                        else
                        {
                            var u_link = $"User:{user}";
                            if (!links.Contains(u_link)) links.Add(u_link);
                        }
                    }
                    else if (link.StartsWith("fuzzy:", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("title:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var fuzzy = link.Substring(6).Trim();
                        var f_link = $"Fuzzy:{fuzzy}";
                        if (!links.Contains(f_link)) links.Add(f_link);
                    }
                    else if (link.StartsWith("searching ", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("searching:", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("search ", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("search:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var search = Regex.Replace(link, @"search.*?[:\s]+(.*?)", "$1", RegexOptions.IgnoreCase).Trim().TrimEnd('.').Trim();
                        var s_link = $"{search}";
                        if (!links.Contains(s_link)) links.Add(s_link);
                    }
                    else if (link.StartsWith("down:", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("download:", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("downloading:", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("downloading ", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var down = Regex.Replace(link, @"down.*?[:\s]+(.*?)", "$1", RegexOptions.IgnoreCase).Trim().TrimEnd('.').Trim();
                        var exists = download_links.Where(l=>l.Contains(down)).Count();
                        if (exists <= 0) download_links.Add($"down:{down}");
                    }
                    else if (link.StartsWith("downall:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var down = link.Substring(8).Trim().TrimEnd('.').Trim();
                        var exists = download_links.Where(l=>l.Contains(down)).Count();
                        if (exists <= 0) download_links.Add($"downall:{down}");
                    }
                    else if (link.Equals("downloading", StringComparison.CurrentCultureIgnoreCase)) continue;
                    else
                    {
                        var fn = m.Value.Trim().Trim(trim_char);
                        try
                        {
                            var sid = Regex.Replace(Path.GetFileNameWithoutExtension(fn), @"(.*?(\d+)(_((p)|(ugoira))*\d+)*.*)", "$2", RegexOptions.IgnoreCase);
                            var IsFile = string.IsNullOrEmpty(Path.GetExtension(fn)) ? false : true;
                            long id;
                            if (long.TryParse(sid, out id) && id > 100)
                            {
                                var a_link = id.ArtworkLink();
                                var a_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={id}";
                                if (!links.Contains(a_link) && !links.Contains(a_link_o)) links.Add(a_link);

                                if (!IsFile)
                                {
                                    var u_link = id.ArtistLink();
                                    var u_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&id={id}";
                                    if (!links.Contains(u_link) && !links.Contains(u_link_o)) links.Add(u_link);
                                }
                            }
                        }
                        catch (Exception ex) { ex.ERROR("ParseLinks"); }
                    }
                }
                if (linkexists) continue;
            }
            if (links.Count <= 0)
            {
                if (html.Split(Path.GetInvalidPathChars()).Length <= 1 && download_links.Count <= 0) links.Add($"Fuzzy:{html}");
                foreach (var dl in download_links) links.Add(dl);
            }
            return (links);
        }

        public static string ArtworkLink(this string id)
        {
            long iid = -1;
            return (string.IsNullOrEmpty(id) || !long.TryParse(id, out iid) || iid < 0 ? string.Empty : $"https://www.pixiv.net/artworks/{id}");
        }

        public static string ArtworkLink(this long id)
        {
            return (id < 0 ? string.Empty : $"https://www.pixiv.net/artworks/{id}");
        }

        public static string ArtistLink(this string id)
        {
            long uid = -1;
            return (string.IsNullOrEmpty(id) || !long.TryParse(id, out uid) || uid < 0 ? string.Empty : $"https://www.pixiv.net/users/{id}");
        }

        public static string ArtistLink(this long id)
        {
            return (id < 0 ? string.Empty : $"https://www.pixiv.net/users/{id}");
        }

        public static string TagLink(this string tag)
        {
            return (string.IsNullOrEmpty(tag) ? string.Empty : Uri.EscapeUriString($"https://www.pixiv.net/tags/{tag}"));
        }
        #endregion

        #region Text process routines
        #region Kana Half To Full Lookup Map
        private static Dictionary<string, string> KanaToFullMap = new Dictionary<string, string>()
        {
            {"ｸﾞ", "グ"}, {"ﾎﾟ", "ポ"}, {"ｹﾞ", "ゲ"}, {"ｶﾞ", "ガ"}, {"ｷﾞ", "ギ"},
            {"ｺﾞ", "ゴ"}, {"ｻﾞ", "ザ"}, {"ｼﾞ", "ジ"}, {"ｽﾞ", "ズ"}, {"ｾﾞ", "ゼ"},
            {"ﾀﾞ", "ダ"}, {"ﾂﾞ", "ヅ"}, {"ﾁﾞ", "ヂ"}, {"ｿﾞ", "ゾ"}, {"ﾃﾞ", "デ"},
            {"ﾄﾞ", "ド"}, {"ﾊﾞ", "バ"}, {"ﾊﾟ", "パ"}, {"ﾋﾞ", "ビ"}, {"ﾋﾟ", "ピ"},
            {"ﾍﾞ", "ベ"}, {"ﾌﾟ", "プ"}, {"ﾍﾟ", "ペ"}, {"ﾎﾞ", "ボ"}, {"ﾌﾞ", "ブ"},

            {"ｧ", "ァ"}, {"ｱ", "ア"}, {"ｨ", "ィ"}, {"ｲ", "イ"}, {"ｩ", "ゥ"},
            {"ｳ", "ウ"}, {"ｪ", "ェ"}, {"ｴ", "エ"}, {"ｫ", "ォ"}, {"ｵ", "オ"},
            {"ｶ", "カ"}, {"ｷ", "キ"}, {"ｸ", "ク"}, {"ｹ", "ケ"}, {"ｺ", "コ"},
            {"ｻ", "サ"}, {"ｼ", "シ"}, {"ｽ", "ス"}, {"ｾ", "セ"}, {"ｿ", "ソ"},
            {"ﾀ", "タ"}, {"ﾁ", "チ"}, {"ｯ", "ッ"}, {"ﾂ", "ツ"}, {"ﾃ", "テ"},
            {"ﾄ", "ト"}, {"ﾅ", "ナ"}, {"ﾆ", "ニ"}, {"ﾇ", "ヌ"}, {"ﾈ", "ネ"},
            {"ﾉ", "ノ"}, {"ﾊ", "ハ"}, {"ﾋ", "ヒ"}, {"ﾌ", "フ"}, {"ﾍ", "ヘ"},
            {"ﾎ", "ホ"}, {"ﾏ", "マ"}, {"ﾐ", "ミ"}, {"ﾑ", "ム"}, {"ﾒ", "メ"},
            {"ﾓ", "モ"}, {"ｬ", "ャ"}, {"ﾔ", "ヤ"}, {"ｭ", "ュ"}, {"ﾕ", "ユ"},
            {"ｮ", "ョ"}, {"ﾖ", "ヨ"}, {"ﾗ", "ラ"}, {"ﾘ", "リ"}, {"ﾙ", "ル"},
            {"ﾚ", "レ"}, {"ﾛ", "ロ"}, /*{"ﾜ", "ヮ"},*/ {"ﾜ", "ワ"}, {"ｦ", "ヲ"},
            {"ﾝ", "ン"}, {"ｰ", "ー"},

            //{"A", "Ａ"}, {"B", "Ｂ"}, {"C", "Ｃ"}, {"D", "Ｄ"}, {"E", "Ｅ"},
            //{"F", "Ｆ"}, {"G", "Ｇ"}, {"H", "Ｈ"}, {"I", "Ｉ"}, {"J", "Ｊ"},
            //{"K", "Ｋ"}, {"L", "Ｌ"}, {"M", "Ｍ"}, {"N", "Ｎ"}, {"O", "Ｏ"},
            //{"P", "Ｐ"}, {"Q", "Ｑ"}, {"R", "Ｒ"}, {"S", "Ｓ"}, {"T", "Ｔ"},
            //{"U", "Ｕ"}, {"V", "Ｖ"}, {"W", "Ｗ"}, {"X", "Ｘ"}, {"Y", "Ｙ"},
            //{"Z", "Ｚ"}, {"a", "ａ"}, {"b", "ｂ"}, {"c", "ｃ"}, {"d", "ｄ"},
            //{"e", "ｅ"}, {"f", "ｆ"}, {"g", "ｇ"}, {"h", "ｈ"}, {"i", "ｉ"},
            //{"j", "ｊ"}, {"k", "ｋ"}, {"l", "ｌ"}, {"m", "ｍ"}, {"n", "ｎ"},
            //{"o", "ｏ"}, {"p", "ｐ"}, {"q", "ｑ"}, {"r", "ｒ"}, {"s", "ｓ"},
            //{"t", "ｔ"}, {"u", "ｕ"}, {"v", "ｖ"}, {"w", "ｗ"}, {"x", "ｘ"},
            //{"y", "ｙ"}, {"z", "ｚ"}, {",", "、"},
        };
        #endregion

        public static string KatakanaHalfToFull(this string text, bool lookup = true)
        {
            if (string.IsNullOrEmpty(text)) return (string.Empty);

            var result = text;
            if (lookup)
            {
                foreach (var kana in KanaToFullMap)
                {
                    var k = kana.Key;
                    var v = kana.Value;
                    result = result.Replace(k, v);
                }
            }
            else
            {
                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] == 32)
                    {
                        result += (char)12288;
                    }
                    if (text[i] < 127)
                    {
                        result += (char)(text[i] + 65248);
                    }
                }
                if (string.IsNullOrEmpty(result)) result = text;
            }
            return result;
        }

        public static string TranslatedText(this string src, string translated = default(string))
        {
            var result = src;
            try
            {
                src = string.IsNullOrEmpty(src) ? string.Empty : src.Trim();
                translated = string.IsNullOrEmpty(translated) ? string.Empty : translated.Trim();
                if (string.IsNullOrEmpty(src)) return (string.Empty);

                result = src;
                if (TagsCache is ConcurrentDictionary<string, string>)
                {
                    if (string.IsNullOrEmpty(translated) || src.Equals(translated, StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (TagsCache.ContainsKey(src))
                        {
                            var tag_t = TagsCache[src];
                            if (!string.IsNullOrEmpty(tag_t)) result = tag_t;
                        }
                    }
                    else
                    {
                        TagsCache[src] = translated;
                        result = translated;
                    }
                }

                if (TagsT2S is ConcurrentDictionary<string, string>)
                {
                    if (TagsT2S.ContainsKey(src)) result = TagsT2S[src];
                    else if (TagsT2S.ContainsKey(result)) result = TagsT2S[result];

                    var pattern = $@"/{src}/";
                    if (TagsT2S.ContainsKey(pattern))
                        result = Regex.Replace(result, src, TagsT2S[pattern], RegexOptions.IgnoreCase);
                }

                if (TagsWildecardT2S is ConcurrentDictionary<string, string>)
                {
                    var alpha = Regex.IsMatch(result, @"^[\u0020-\u007E]*$", RegexOptions.IgnoreCase);
                    var text = alpha ? src : result;
                    foreach (var kv in TagsWildecardT2S)
                    {
                        var k = kv.Key.Replace(" ", "\\s");
                        var v = kv.Value;
                        if (text.IndexOf(v, 0, StringComparison.OrdinalIgnoreCase) < 0)
                            text = Regex.Replace(text, $@"{k.Trim('/')}", v, RegexOptions.IgnoreCase);

                        if (k.StartsWith("/") && k.EndsWith("/"))
                        {
                            text = Regex.Replace(text, $@"{k.Trim('/')}", v, RegexOptions.IgnoreCase);
                            result = Regex.Replace(result, $@"{k.Trim('/')}", v, RegexOptions.IgnoreCase);
                        }
                    }
                    var result_o = Regex.Replace(result, regex_symbol, "\\$1", RegexOptions.IgnoreCase);
                    result = alpha && !Regex.IsMatch(text, result_o, RegexOptions.IgnoreCase) ? $"{text}/{result}" : text;
                }
            }
            catch (Exception ex) { ex.ERROR("TRANSLATE"); }
            return (result);
        }

        public static string InsertLineBreak(this string text, int lineLength)
        {
            if (string.IsNullOrEmpty(text)) return (string.Empty);
            //return Regex.Replace(text, @"(.{" + lineLength + @"})", "$1" + Environment.NewLine);
            var t = text.HtmlFormatBreakLine(false);// Regex.Replace(text, @"[\n\r]", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            //t = Regex.Replace(t, @"<[^>]*>", "$1", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"(<br *?/>)", Environment.NewLine, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"(<a .*?>(.*?)</a>)|(<strong>(.*?)</strong>)", "$2", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"<.*?>(.*?)</.*?>", "$1", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            //return Regex.Replace(t, @"(.{" + lineLength + @"})", "$1" + Environment.NewLine, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var ts = t.Split(new string[]{Environment.NewLine}, StringSplitOptions.None);
            for (var i = 0; i < ts.Length; i++)
            {
                if (ts[i].Length > lineLength) ts[i] = Regex.Replace(ts[i], @"(.{" + lineLength + @"})", "$1" + Environment.NewLine, RegexOptions.IgnoreCase);
            }
            return (string.Join(Environment.NewLine, ts));
        }

        public static string HtmlEncode(this string text)
        {
            if (string.IsNullOrEmpty(text)) return (string.Empty);
            else return (WebUtility.HtmlEncode(text));
        }

        public static string HtmlDecode(this string text, bool br = true)
        {
            string result = text;
            if (!string.IsNullOrEmpty(result))
            {
                var patten = new Regex(@"&(amp;)?#(([0-9]{1,6})|(x([a-fA-F0-9]{1,5})));", RegexOptions.IgnoreCase);
                //result = WebUtility.UrlDecode(WebUtility.HtmlDecode(result));
                result = Uri.UnescapeDataString(WebUtility.HtmlDecode(result));
                foreach (Match match in patten.Matches(result))
                {
                    var v = Convert.ToInt32(match.Groups[2].Value);
                    if (v > 0xFFFF)
                        result = result.Replace(match.Value, char.ConvertFromUtf32(v));
                }
                result = result.HtmlFormatBreakLine(br);
            }
            return (result);
        }

        public static string HtmlFormatBreakLine(this string text, bool br = true)
        {
            var result = text.Replace("\r\n", "<br/>").Replace("\n\r", "<br/>").Replace("\r", "<br/>").Replace("\n", "<br/>");
            if (br) result = result.Replace("<br/>", $"<br/>{Environment.NewLine}");
            else result = result.Replace("<br/>", Environment.NewLine);
            return (result);
        }

        /// <summary>
        /// How To Convert HTML To Formatted Plain Text
        /// source: http://www.beansoftware.com/ASP.NET-Tutorials/Convert-HTML-To-Plain-Text.aspx
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static string HtmlToText(this string html)
        {
            if (string.IsNullOrEmpty(html)) return (html);
            string result = string.Copy(html);
            try
            {
                // Remove new lines since they are not visible in HTML
                result = result.HtmlFormatBreakLine(false);//.Replace("\n", " ");

                // Remove tab spaces
                result = result.Replace("\t", " ");

                // Remove multiple white spaces from HTML
                result = Regex.Replace(result, " +", " ");

                // Remove HEAD tag
                result = Regex.Replace(result, "<head.*?</head>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                // Remove any JavaScript
                result = Regex.Replace(result, "<script.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                result = Regex.Replace(result, @"&#x([0-9a-fA-F]+);", delegate (Match m) { return (char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16))); }, RegexOptions.IgnoreCase);
                result = Regex.Replace(result, @"&#(\d+);", delegate (Match m) { return (char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value))); }, RegexOptions.IgnoreCase);

                // Replace special characters like &, <, >, " etc.
                StringBuilder sb = new StringBuilder(result);
                // Note: There are many more special characters, these are just
                // most common. You can add new characters in this arrays if needed
                string[] OldWords = { "&nbsp;", "&amp;", "&quot;", "&lt;", "&gt;", "&reg;", "&copy;", "&bull;", "&trade;" };
                string[] NewWords = { " ", "&", "\"", "<", ">", "®", "©", "•", "™" };
                for (int i = 0; i < OldWords.Length; i++)
                {
                    sb.Replace(OldWords[i], NewWords[i]);
                }

                // Check if there are line breaks (<br>) or paragraph (<p>)
                sb.Replace("<br>", "\n<br>");
                sb.Replace("<br ", "\n<br ");
                sb.Replace("<p ", "\n<p ");
                result = Regex.Replace(sb.ToString(), "<[^>]*>", "");
            }
            catch (Exception ex) { ex.ERROR("HtmlToText"); result = html.HtmlDecode(false); }
            return result;
        }

        public static string GetDefaultTemplate()
        {
            var result = string.Empty;
            if (setting is Setting)
            {
                var template = string.IsNullOrEmpty(setting.ContentsTemplete) ? string.Empty : setting.ContentsTemplete;
                if (string.IsNullOrEmpty(template))
                {
                    if (string.IsNullOrEmpty(setting.CustomContentsTemplete))
                    {
                        StringBuilder html = new StringBuilder();
                        html.AppendLine("<!DOCTYPE html>");
                        html.AppendLine("<HTML>");
                        html.AppendLine("  <HEAD>");
                        html.AppendLine("    <TITLE>{% title %}</TITLE>");
                        html.AppendLine("    <META http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />");
                        html.AppendLine("    <META http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />");
                        html.AppendLine("    <STYLE>");
                        html.AppendLine("      :root { --accent: {% accentcolor_rgb %}; --text: {% textcolor_rgb %} }");
                        html.AppendLine("      *{font-family:\"等距更纱黑体 SC\", FontAwesome, \"Segoe UI Emoji\", \"Segoe MDL2 Assets\", \"Segoe UI\", Iosevka, \"Sarasa Mono J\", \"Sarasa Term J\", \"Sarasa Gothic J\", \"更纱黑体 SC\", 思源黑体, 思源宋体, 微软雅黑, 宋体, 黑体, 楷体, Consolas, \"Courier New\", Tahoma, Arial, Helvetica, sans-serif !important;}");
                        html.AppendLine("      body{background-color: {% backcolor %} !important;}");
                        html.AppendLine("      a:link{color:{% accentcolor %} !important;text-decoration:none !important;}");
                        html.AppendLine("      a:hover{color:{% accentcolor %} !important;text-decoration:none !important;}");
                        html.AppendLine("      a:active{color:{% accentcolor %} !important;text-decoration:none !important;}");
                        html.AppendLine("      a:visited{color:{% accentcolor %} !important;text-decoration:none !important;}");
                        html.AppendLine("      img{width:auto!important;height:auto!important;max-width:100%!important;max-height:100% !important;}");
                        html.AppendLine("      .tag{color:{% accentcolor %} !important;background-color:rgba(var(--accent), 10%);line-height:1.6em;padding:0 2px 0 1px;text-decoration:none;border:1px solid {% accentcolor %};border-left-width:5px;overflow-wrap:break-word;}");
                        html.AppendLine("      .tag.::before{ content: '#'; }");
                        html.AppendLine("      .desc{color:{% textcolor %} !important;text-decoration:none !important;width: 99% !important;word-wrap: break-word !important;overflow-wrap: break-word !important;white-space:normal !important;}");
                        html.AppendLine("      .twitter::before{font-family:FontAwesome; content:''; margin-left:3px; padding-right:4px; color: #1da1f2;}");
                        html.AppendLine("      .web::before{content:'🌐'; padding-right:3px; margin-left:-0px;}");
                        html.AppendLine("      .mail::before{content:'🖃'; padding-right:4px; margin-left:2px;}");
                        html.AppendLine("      .E404{display:block; min-height:calc(95vh); background-image:url('{% site %}/404.jpg'); background-position: center; background-attachment: fixed; background-repeat: no-repeat;}");
                        html.AppendLine("      .E404T{font-size:calc(2.5vw); color:gray; position:fixed; margin-left:calc(50vw); margin-top:calc(50vh);}");
                        html.AppendLine();
                        html.AppendLine("      @media screen and(-ms-high-contrast: active), (-ms-high-contrast: none) {");
                        html.AppendLine("      .tag{color:{% accentcolor %} !important;background-color:rgba({% accentcolor_rgb %}, 0.1);line-height:1.6em;padding:0 2px 0 1px;text-decoration:none;border:1px solid {% accentcolor %};border-left-width:5px;overflow-wrap:break-word;}");
                        html.AppendLine("      }");
                        html.AppendLine("    </STYLE>");
                        html.AppendLine("    <SCRIPT>");
                        html.AppendLine("      window.alert = function () { }");
                        html.AppendLine("    </SCRIPT>");
                        html.AppendLine("  </HEAD>");
                        html.AppendLine("<BODY>");
                        html.AppendLine("{% contents %}");
                        html.AppendLine("</BODY>");
                        html.AppendLine("</HTML>");

                        result = html.ToString();

                        File.WriteAllText(setting.ContentsTemplateFile, result);
                    }
                    else
                    {
                        result = setting.CustomContentsTemplete;
                    }

                    if (!setting.ContentsTemplete.Equals(result))
                    {
                        setting.ContentsTemplete = result;
                        setting.Save();
                    }
                }
                else
                {
                    result = template;
                }
            }
            return (result);
        }

        public static string GetHtmlFromTemplate(this string contents, string title = "")
        {
            var backcolor = Theme.WhiteColor.ToHtml();
            if (backcolor.StartsWith("#FF") && backcolor.Length > 6) backcolor = backcolor.Replace("#FF", "#");
            else if (backcolor.StartsWith("#00") && backcolor.Length > 6) backcolor = backcolor.Replace("#00", "#");
            var accentcolor = Theme.AccentBaseColor.ToHtml(false);
            var accentcolor_rgb = Theme.AccentBaseColor.ToRGB(false, false);
            var textcolor = Theme.TextColor.ToHtml(false);
            var textcolor_rgb = Theme.TextColor.ToRGB(false, false);

            contents = string.IsNullOrEmpty(contents) ? string.Empty : contents.Trim();
            title = string.IsNullOrEmpty(title) ? string.Empty : title.Trim();

            var template = GetDefaultTemplate();
            template = Regex.Replace(template, @"{%\s*?site\s*?%}", new Uri(Application.Current.GetRoot()).AbsoluteUri, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?title\s*?%}", title, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?backcolor\s*?%}", backcolor, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?accentcolor\s*?%}", accentcolor, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?accentcolor_rgb\s*?%}", accentcolor_rgb, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?textcolor\s*?%}", textcolor, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?textcolor_rgb\s*?%}", textcolor_rgb, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?contents\s*?%}", contents, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            return (template.ToString());
        }

        public static async void UpdateIllustTagsAsync()
        {
            await new Action(async () =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MainWindow)
                    {
                        var mw = win as MainWindow;
                        mw.UpdateIllustTagsAsync();
                        await Task.Delay(1);
                        mw.DoEvents();
                    }
                    else if (win is ContentWindow)
                    {
                        var w = win as ContentWindow;
                        if (w.Content is IllustDetailPage)
                        {
                            (w.Content as IllustDetailPage).UpdateIllustTags();
                            await Task.Delay(1);
                            w.DoEvents();
                        }
                    }
                    else continue;
                }
            }).InvokeAsync();
        }

        public static void UpdateIllustTags(this ConcurrentDictionary<string, string> tags)
        {
            UpdateIllustTagsAsync();
        }

        public static void UpdateIllustTags(this Application app)
        {
            UpdateIllustTagsAsync();
        }

        public static async void UpdateIllustDescAsync()
        {
            await new Action(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MainWindow)
                    {
                        var mw = win as MainWindow;
                        mw.UpdateIllustDescAsync();
                    }
                    else if (win is ContentWindow)
                    {
                        var w = win as ContentWindow;
                        if (w.Content is IllustDetailPage)
                        {
                            (w.Content as IllustDetailPage).UpdateIllustDesc();
                        }
                    }
                    else continue;
                }
            }).InvokeAsync();
        }

        public static void UpdateIllustDesc(this string content)
        {
            UpdateIllustDescAsync();
        }

        public static void UpdateIllustDesc(this Application app)
        {
            UpdateIllustDescAsync();
        }

        public static async void UpdateWebContentAsync()
        {
            await new Action(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MainWindow)
                    {
                        var mw = win as MainWindow;
                        mw.UpdateWebContentAsync();
                    }
                    else if (win is ContentWindow)
                    {
                        var w = win as ContentWindow;
                        if (w.Content is IllustDetailPage)
                        {
                            (w.Content as IllustDetailPage).UpdateWebContent();
                        }
                    }
                    else continue;
                }
            }).InvokeAsync();
        }

        public static void UpdateWebContent(this Pixeez.Objects.Work illust)
        {
            UpdateWebContentAsync();
        }

        public static void UpdateWebContent(this Application app)
        {
            UpdateWebContentAsync();
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

        // MakePackUri is a utility method for computing a pack uri
        // for the given resource. 
        public static Uri MakePackUri(this string relativeFile)
        {
            Assembly a = typeof(ThresholdEffect).Assembly;

            // Extract the short name.
            string assemblyShortName = a.ToString().Split(',')[0];
            string uriString = $"pack://application:,,,/{assemblyShortName};component/{relativeFile}";

            return new Uri(uriString);
        }

        public static string[] Where(this string cmd)
        {
            var result = new List<string>();

            if (Path.IsPathRooted(cmd) && File.Exists(cmd)) result.Add(cmd);
            else
            {
                var cmd_name = Path.IsPathRooted(cmd) ? Path.GetFileName(cmd) : cmd;
                var search_list = Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator).ToList();
                search_list.Insert(0, Application.Current.GetRoot());
                foreach (var p in search_list)
                {
                    var c = Path.Combine(p, cmd);
                    if (File.Exists(c)) result.Add(c);
                }
            }
            return (result.ToArray());
        }

        public static string GetIllustId(this string url)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url))
            {
                var m = Regex.Match(Path.GetFileName(url), @"(\d+)(_((p)(ugoira))\d+.*?)", RegexOptions.IgnoreCase);
                if (m.Groups.Count > 0)
                {
                    result = m.Groups[1].Value;
                }

                if (string.IsNullOrEmpty(result))
                {
                    m = Regex.Match(Path.GetFileName(url), @"(\d+)", RegexOptions.IgnoreCase);
                    if (m.Groups.Count > 0)
                    {
                        result = m.Groups[1].Value;
                    }
                }
            }
            return (result);
        }

        public static string GetImageId(this string url)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url))
            {
                result = Path.GetFileNameWithoutExtension(url);
            }
            return (result);
        }

        public static string SanityAge(this string sanity)
        {
            string age = "all";

            int san = 2;
            if (int.TryParse(sanity, out san))
            {
                switch (sanity)
                {
                    case "3":
                        age = "12+";
                        break;
                    case "4":
                        age = "15+";
                        break;
                    case "5":
                        age = "17+";
                        break;
                    case "6":
                        age = "18+";
                        break;
                    default:
                        age = "all";
                        break;
                }
            }
            else
            {
                if (sanity.StartsWith("all")) age = "all";
                else if (sanity.StartsWith("12")) age = "12+";
                else if (sanity.StartsWith("15")) age = "15+";
                else if (sanity.StartsWith("17")) age = "17+";
                else if (sanity.StartsWith("18")) age = "18+";
                else age = "all";
            }
            return (age);
        }

        public static string FolderMacroReplace(this string text)
        {
            var result = text;
            result = MacroReplace(result, @"%id%", text.GetIllustId());
            return (result);
        }

        public static string FolderMacroReplace(this string text, string target)
        {
            var result = text;
            result = MacroReplace(result, @"%id%", target);
            return (result);
        }

        public static string MacroReplace(this string text, string macro, string target)
        {
            return (Regex.Replace(text, macro, target, RegexOptions.IgnoreCase));
        }

        public static void SendToOtherInstance(this IEnumerable<string> contents)
        {
            if (contents is IEnumerable<string> && contents.Count() > 0)
            {
                var sendData = string.Join(Environment.NewLine, contents.ToArray());
                SendToOtherInstance(sendData);
            }
        }

        public static void SendToOtherInstance(this string contents)
        {
            try
            {
                var sendData = contents.Trim();
                if (string.IsNullOrEmpty(sendData)) return;

                var pipes = Directory.GetFiles("\\\\.\\pipe\\", "PixivWPF*");
#if DEBUG
                if (pipes.Length > 0)
                {
                    $"Found {pipes.Length} PixivWPF-Search Bridge(s):".DEBUG();
                    foreach (var pipe in pipes)
                    {
                        $"  {pipe}".DEBUG();
                    }
                }
                else return;
#endif

                var current = Application.Current.PipeServerName();
                foreach (var pipe in pipes)
                {
                    try
                    {
                        var pipeName = pipe.Substring(9);
                        if (pipeName.Equals(current, StringComparison.CurrentCultureIgnoreCase) &&
                            Keyboard.Modifiers != ModifierKeys.Control) continue;

                        using (var pipeClient = new NamedPipeClientStream(".", pipeName,
                            PipeDirection.Out, PipeOptions.Asynchronous,
                            System.Security.Principal.TokenImpersonationLevel.Impersonation))
                        {
                            pipeClient.Connect(1000);
                            using (StreamWriter sw = new StreamWriter(pipeClient))
                            {
#if DEBUG
                                $"Sending [{sendData}] to {pipeName}".DEBUG();
#endif
                                sw.WriteLine(sendData);
                                sw.Flush();
                            }
                        }
                    }
#if DEBUG
                    catch (Exception ex)
                    {
                        ex.ToString().ShowMessageBox("ERROR", MessageBoxImage.Error);
                    }
#else
                    catch (Exception ex) { ex.ERROR(); }
#endif
                }
            }
            catch (Exception ex)
            {
                ex.ToString().ShowMessageBox("ERROR", MessageBoxImage.Error);
            }
        }

        public static void ShellSendToOtherInstance(this IEnumerable<string> contents)
        {
            if (contents is IEnumerable<string> && contents.Count() > 0)
            {
                var sendData = string.Join("\" \"", contents.ToArray());
                ShellSendToOtherInstance($"\"{sendData}\"");
            }
        }

        public static void ShellSendToOtherInstance(this string contents)
        {
            var shell = Path.Combine(Application.Current.GetRoot(), setting.ShellSearchBridgeApplication);
            if (File.Exists(shell))
            {
                Process.Start(shell, contents);
            }
        }

        public static void ShellOpenPixivPedia(this string contents)
        {
            if (string.IsNullOrEmpty(contents)) return;

            var currentUri = contents.StartsWith("http", StringComparison.CurrentCultureIgnoreCase) ? Uri.EscapeUriString(contents.Replace("http://", "https://")) : Uri.EscapeUriString($"https://dic.pixiv.net/a/{contents}/");

            var all = setting.ShellPixivPediaApplication.Where();
            var shell = all.Length > 0 ? all.First() : string.Empty;

            if (File.Exists(shell) && shell.EndsWith("\\nw.exe", StringComparison.CurrentCultureIgnoreCase))
            {
                var args = new List<string>() {
                    setting.ShellPixivPediaApplicationArgs,
                    $"--app=\"PixivPedia-{contents}\"",
                    $"--app-id=\"PixivPedia-{contents}\"",
                    $"--user-data-dir=\"{Path.Combine(Application.Current.GetRoot(), ".web")}\"",
                    $"--url=\"{currentUri}\""
                };
                Process.Start(shell, string.Join(" ", args));
            }
            else
            {
                Process.Start(currentUri);
            }
        }

        public static bool OpenUrlWithShell(this string url)
        {
            bool result = false;

            try
            {
                Process.Start(url);
                result = true;
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }

            return (result);
        }

        private static bool Run(string FileName, string args = "")
        {
            bool result = false;
            try
            {
                Task.Run(() =>
                {
                    var process = new Process();
                    process.StartInfo.FileName = FileName;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.ErrorDialog = process.StartInfo.UseShellExecute ? true : false;
                    process.StartInfo.WorkingDirectory = Path.GetDirectoryName(FileName);
                    //process.StartInfo.ErrorDialogParentHandle = Application.Current.GetMainWindow();
                    process.Start();
                }).Start();
            }
            catch (Exception ex) { ex.Message.DEBUG("SHELL"); }
            return (result);
        }

        public static bool OpenFileWithShell(this string FileName, bool ShowFolder = false, string command = "", string custom_params = "")
        {
            bool result = false;
            try
            {
                var file = string.IsNullOrEmpty(command) ? Path.GetFullPath(FileName) : FileName;
                var WinDir = Environment.GetEnvironmentVariable("WinDir");
                if (ShowFolder)
                {
                    if (!string.IsNullOrEmpty(file))
                    {
                        Application.Current.ReleaseKeyboardModifiers();
                        Application.Current.DoEvents();

                        var shell = string.IsNullOrEmpty(WinDir) ? "explorer.exe" : Path.Combine(WinDir, "explorer.exe");
                        if (File.Exists(file))
                        {
                            Process.Start(shell, $"/select,\"{file}\"");
                            result = true;
                        }
                        else if (Directory.Exists(file))
                        {
                            Process.Start(shell, $"\"{file}\"");
                            result = true;
                        }
                        else
                        {
                            var folder = Path.GetDirectoryName(file);
                            if (Directory.Exists(folder))
                            {
                                Process.Start(shell, $"\"{folder}\"");
                                result = true;
                            }
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(file) && File.Exists(file))
                    {
                        var UsingOpenWith = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? true : false;
                        var ShowProperties = Keyboard.Modifiers == ModifierKeys.Control ? true : false;
                        var SysDir = Path.Combine(WinDir, Environment.Is64BitOperatingSystem ? "SysWOW64" : "System32", "OpenWith.exe");
                        var OpenWith = string.IsNullOrEmpty(WinDir) ? string.Empty : SysDir;
                        var openwith_exists = File.Exists(OpenWith) ?  true : false;

                        Application.Current.ReleaseKeyboardModifiers();
                        Application.Current.DoEvents();

                        if (UsingOpenWith && openwith_exists)
                        {
                            Process.Start(OpenWith, file);
                            result = true;
                        }
                        else if (ShowProperties)
                        {
                            file.OpenShellFileProperty();
                        }
                        else
                        {
                            setting = Application.Current.LoadSetting();
                            var alt_viewer = (int)(Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Control)) == 3 ? !setting.ShellImageViewerEnabled : setting.ShellImageViewerEnabled;
                            var IsImage = ext_imgs.Contains(Path.GetExtension(file).ToLower()) ? true : false;
                            if (alt_viewer && IsImage)
                            {
                                if (string.IsNullOrEmpty(setting.ShellImageViewerCmd) ||
                                    !setting.ShellImageViewerCmd.ToLower().Contains(setting.ShellImageViewer.ToLower()))
                                    setting.ShellImageViewerCmd = setting.ShellImageViewer;
                                if (!File.Exists(setting.ShellImageViewerCmd))
                                {
                                    var cmd_found = setting.ShellImageViewerCmd.Where();
                                    if (cmd_found.Length > 0) setting.ShellImageViewerCmd = cmd_found.First();
                                }
                                var args = $"{setting.ShellImageViewerParams} \"{file}\"";
                                if (string.IsNullOrEmpty(setting.ShellImageViewerCmd))
                                    Process.Start(file);
                                else
                                    Process.Start(setting.ShellImageViewerCmd, args.Trim());
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(command))
                                    Process.Start(file);
                                else
                                    Process.Start(command, $"{custom_params} \"{file}\"".Trim());
                            }
                        }
                        result = true;
                    }
                    else if (File.Exists(command))
                    {
                        Process.Start(command, $"{file}");
                        result = true;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("SHELLRUN"); }
            finally
            {
                Application.Current.DoEvents();
            }
            return (result);
        }

        public static bool OpenShellFileProperty(this string FileName)
        {
            bool result = false;
            try
            {
                //result = ShowFileProperties(FileName);
                result = ShellProperties.Show(FileName) == 0 ? true : false;
            }
            catch (Exception ex) { ex.ERROR("OpenShellFileProperty"); }
            return (result);
        }

        public static IEnumerable<string> GetDownloadInfo(this DownloadInfo item)
        {
            List<string> result = new List<string>();
            if (item is DownloadInfo)
            {
                var di = item as DownloadInfo;
                var fail = string.IsNullOrEmpty(di.FailReason) ? string.Empty : $", Reason: {di.FailReason.Replace(Environment.NewLine, $"\t{Environment.NewLine}")}".Trim();
                var delta = di.EndTime - di.StartTime;
                var rate = delta.TotalSeconds <= 0 ? 0 : di.Received / 1024.0 / delta.TotalSeconds;
                result.Add($"URL    : {di.Url}");
                result.Add($"File   : {di.FileName}, {di.FileTime.ToString("yyyy-MM-dd HH:mm:sszzz")}");
                result.Add($"State  : {di.State}{fail}");
                result.Add($"Elapsed: {di.StartTime.ToString("yyyy-MM-dd HH:mm:sszzz")} -> {di.EndTime.ToString("yyyy-MM-dd HH:mm:sszzz")}, {delta.Days * 24 + delta.Hours}:{delta.Minutes}:{delta.Seconds} s");
                result.Add($"Status : {di.Received / 1024.0:0.} KB / {di.Length / 1024.0:0.} KB ({di.Received} Bytes / {di.Length} Bytes), Rate ≈ {rate:0.00} KB/s");
            }
            return (result);
        }
        #endregion

        #region Get Illust Work DateTime
        private static TimeZoneInfo TokoyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        private static TimeZoneInfo LocalTimeZone = TimeZoneInfo.Local;

        private static DateTime ParseDateTime(this string url)
        {
            var result = DateTime.FromFileTime(0);
            //https://i.pximg.net/img-original/img/2010/11/16/22/34/05/14611687_p0.png
            var ds = Regex.Replace(url, @"https?://i\.pximg\.net/.*?/(\d{4})/(\d{2})/(\d{2})/(\d{2})/(\d{2})/(\d{2})/\d+.*?" + regex_img_ext, "$1-$2-$3T$4:$5:$6+09:00", RegexOptions.IgnoreCase);
            DateTime.TryParse(ds, out result);
            return (result);
        }

        public static DateTime GetDateTime(this Pixeez.Objects.Work Illust, bool local = false)
        {
            var dt = DateTime.Now;
            if (Illust is Pixeez.Objects.IllustWork)
            {
                var illustset = Illust as Pixeez.Objects.IllustWork;
                dt = illustset.GetOriginalUrl().ParseDateTime();
                if (dt.Year <= 1601) dt = illustset.CreatedTime;
            }
            else if (Illust is Pixeez.Objects.NormalWork)
            {
                var illustset = Illust as Pixeez.Objects.NormalWork;
                dt = illustset.GetOriginalUrl().ParseDateTime();
                if (dt.Year <= 1601) dt = illustset.CreatedTime.LocalDateTime;
            }
            else if (!string.IsNullOrEmpty(Illust.ReuploadedTime))
            {
                DateTime.TryParse($"{Illust.ReuploadedTime}+09:00", out dt);
            }
            dt = new DateTime(dt.Ticks, DateTimeKind.Unspecified);
            if (local) return (TimeZoneInfo.ConvertTimeBySystemTimeZoneId(dt, TokoyTimeZone.Id, LocalTimeZone.Id));
            else return (dt);
        }

        private static ConcurrentDictionary<string, bool> _MetaTouching_ = new ConcurrentDictionary<string, bool>();
        public static void AttachMetaInfo(this FileInfo fileinfo, DateTime dt = default(DateTime), string id = "")
        {
            try
            {
                if (!Microsoft.WindowsAPICodePack.Shell.ShellObject.IsPlatformSupported) return;
                if (_MetaTouching_.TryAdd(fileinfo.FullName, true))
                {
                    System.Diagnostics.Debug.WriteLine($"=> Touching {fileinfo.Name}");
                    if (string.IsNullOrEmpty(id)) id = GetIllustId(fileinfo.Name);
                    var illust = id.FindIllust();
                    if (illust is Pixeez.Objects.Work)
                    {
                        var uid = $"{illust.User.Id}";
                        bool is_png = fileinfo.Extension.Equals(".png", StringComparison.CurrentCultureIgnoreCase);
                        string name = Path.GetFileNameWithoutExtension(fileinfo.Name);

                        using (var sh = Microsoft.WindowsAPICodePack.Shell.ShellFile.FromFilePath(fileinfo.FullName))
                        {
                            if (sh.Properties.System.Photo.DateTaken.Value == null) sh.Properties.System.Photo.DateTaken.Value = dt;
                            if (!is_png)
                            {
                                if (sh.Properties.System.DateAcquired.Value == null || sh.Properties.System.DateAcquired.Value.Value.Ticks != dt.Ticks)
                                    sh.Properties.System.DateAcquired.Value = dt;

                                if (sh.Properties.System.Subject.Value == null || !sh.Properties.System.Subject.Value.Equals(id.ArtworkLink()))
                                    sh.Properties.System.Subject.Value = id.ArtworkLink();
                                if (sh.Properties.System.Title.Value == null || !sh.Properties.System.Title.Value.Equals(illust.Title))
                                    sh.Properties.System.Title.Value = illust.Title;
                                if (sh.Properties.System.Author.Value == null)
                                    sh.Properties.System.Author.Value = new string[] { illust.User.Name, $"uid:{illust.User.Id ?? -1}" };
                                if (sh.Properties.System.Keywords.Value == null || sh.Properties.System.Keywords.Value.Length != illust.Tags.Count)
                                    sh.Properties.System.Keywords.Value = illust.Tags.ToArray();
                                if (sh.Properties.System.Copyright.Value == null)
                                    sh.Properties.System.Copyright.Value = string.Join("; ", sh.Properties.System.Author.Value);

                                if (sh.Properties.System.Comment.Value == null || !sh.Properties.System.Comment.Value.Equals(illust.Caption.HtmlToText()))
                                    sh.Properties.System.Comment.Value = illust.Caption.HtmlToText();

                                //if (sh.Properties.System.Contact.Webpage.Value == null) sh.Properties.System.Contact.Webpage.Value = id.ArtworkLink();

                                //if (sh.Properties.System.Media.AuthorUrl.Value == null) sh.Properties.System.Media.AuthorUrl.Value = uid.ArtistLink();
                                //if (sh.Properties.System.Media.PromotionUrl.Value == null) sh.Properties.System.Media.PromotionUrl.Value = id.ArtworkLink();

                                if (illust.IsLiked())
                                {
                                    if (sh.Properties.System.SimpleRating.Value != 4)
                                        sh.Properties.System.SimpleRating.Value = 4;
                                }
                                else
                                {
                                    if (sh.Properties.System.SimpleRating.Value != null)
                                        sh.Properties.System.SimpleRating.ClearValue();
                                }
                            }
                        }
                        //sh.Update();
                    }
                }
                bool fn = false;
                _MetaTouching_.TryRemove(fileinfo.FullName, out fn);
            }
            catch (Exception ex) { ex.ERROR($"AttachMetaInfo_{fileinfo.Name}"); }
        }

        public static void Touch(this FileInfo fileinfo, string url, bool local = false, bool meta = false)
        {
            try
            {
                if (url.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                {
                    new Action(() =>
                    {
                        var fdt = url.ParseDateTime();
                        if (fdt.Year <= 1601) return;
                        fileinfo.WaitFileUnlock();
                        //if (meta && setting.DownloadAttachMetaInfo) fileinfo.AttachMetaInfo(dt: fdt);
                        if (setting.DownloadAttachMetaInfo) fileinfo.AttachMetaInfo(dt: fdt);
                        if (fileinfo.CreationTime.Ticks != fdt.Ticks) fileinfo.CreationTime = fdt;
                        if (fileinfo.LastWriteTime.Ticks != fdt.Ticks) fileinfo.LastWriteTime = fdt;
                        if (fileinfo.LastAccessTime.Ticks != fdt.Ticks) fileinfo.LastAccessTime = fdt;
                    }).Invoke(async: true);
                }
            }
            catch (Exception ex) { var id = fileinfo is FileInfo ? fileinfo.Name : url.GetIllustId(); ex.ERROR($"Touch_{id}"); }
        }

        public static void Touch(this string file, string url, bool local = false, bool meta = false)
        {
            try
            {
                if (File.Exists(file))
                {
                    file.WaitFileUnlock();
                    FileInfo fi = new FileInfo(file);
                    fi.Touch(url, local, meta);
                }
            }
            catch (Exception ex) { var id = Path.GetFileName(file); ex.ERROR($"Touch_{id}"); }
        }

        public static void Touch(this string file, Pixeez.Objects.Work Illust, bool local = false, bool meta = false)
        {
            file.Touch(Illust.GetOriginalUrl(), local, meta);
        }
        #endregion

        #region Downloaded Cache routines
        private static ConcurrentDictionary<string, bool> _cachedDownloadedList = new ConcurrentDictionary<string, bool>();
        internal static void UpdateDownloadedListCache(this string folder, bool cached = true)
        {
            if (Directory.Exists(folder) && cached)
            {
                try
                {
                    if (!_cachedDownloadedList.ContainsKey(folder))
                    {
                        _cachedDownloadedList[folder] = cached;
                        var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories);
                        foreach (var f in files)
                        {
                            if (ext_imgs.Contains(Path.GetExtension(f).ToLower()))
                                _cachedDownloadedList[f] = cached;
                        }
                    }
                }
                catch (Exception ex) { ex.ERROR(); }
            }
        }

        internal static async void UpdateDownloadedListCacheAsync(this string folder, bool cached = true)
        {
            await Task.Run(() =>
            {
                UpdateDownloadedListCache(folder, cached);
            });
        }

        internal static void UpdateDownloadedListCache(this StorageType storage)
        {
            if (storage is StorageType)
            {
                storage.Folder.UpdateDownloadedListCacheAsync(storage.Cached);
            }
        }

        internal static async void UpdateDownloadedListCacheAsync(this StorageType storage)
        {
            await Task.Run(() =>
            {
                UpdateDownloadedListCache(storage);
            });
        }

        internal static bool DownoadedCacheExists(this string file)
        {
            return (_cachedDownloadedList.ContainsKey(file));
        }

        private static Func<string, bool> DownloadedCacheExistsFunc = x => DownoadedCacheExists(x);
        internal static bool DownloadedCacheExistsAsync(this string file)
        {
            return (DownloadedCacheExistsFunc(file));
        }

        internal static void DownloadedCacheAdd(this string file, bool cached = true)
        {
            try
            {
                _cachedDownloadedList[file] = cached;
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        internal static void DownloadedCacheRemove(this string file)
        {
            try
            {
                bool cached = false;
                if (_cachedDownloadedList.ContainsKey(file))
                    _cachedDownloadedList.TryRemove(file, out cached);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        internal static void DownloadedCacheUpdate(this string old_file, string new_file, bool cached = true)
        {
            try
            {
                if (_cachedDownloadedList.ContainsKey(old_file))
                {
                    old_file.DownloadedCacheRemove();
                }
                new_file.DownloadedCacheAdd(cached);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        // Define the event handlers.
        private static ConcurrentDictionary<string, FileSystemWatcher> _watchers = new ConcurrentDictionary<string, FileSystemWatcher>();
        private static DateTime lastDownloadEventTick = DateTime.Now;
        private static string lastDownloadEventFile = string.Empty;
        private static WatcherChangeTypes lastDownloadEventType = WatcherChangeTypes.All;

        private static void OnDownloadChanged(object source, FileSystemEventArgs e)
        {
#if DEBUG
            // Specify what is done when a file is changed, created, or deleted.
            $"File: {e.FullPath} {e.ChangeType}".DEBUG("DOWNLOADWATCHER");
#endif
            try
            {
                //if (e.ChangeType == lastDownloadEventType &&
                //    e.FullPath.Equals(lastDownloadEventFile, StringComparison.CurrentCultureIgnoreCase) &&
                //    lastDownloadEventTick.Ticks.DeltaNowMillisecond() < 10) throw new Exception("Same download event!");
                if (e.ChangeType == WatcherChangeTypes.Created)
                {
                    if (File.Exists(e.FullPath))
                    {
                        if (ext_imgs.Contains(Path.GetExtension(e.Name).ToLower()))
                        {
                            e.FullPath.DownloadedCacheAdd();
                            UpdateDownloadStateAsync(GetIllustId(e.Name), true);
                            lastDownloadEventTick = DateTime.Now;
                        }
                    }
                }
                else if (e.ChangeType == WatcherChangeTypes.Changed)
                {
                    //if (File.Exists(e.FullPath))
                    //{
                    //    e.FullPath.DownloadedCacheAdd();
                    //    UpdateDownloadStateAsync(GetIllustId(e.FullPath));
                    //    lastDownloadEventTick = DateTime.Now;
                    //}
                }
                else if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    if (ext_imgs.Contains(Path.GetExtension(e.Name).ToLower()))
                    {
                        e.FullPath.DownloadedCacheRemove();
                        UpdateDownloadStateAsync(GetIllustId(e.Name), false);
                        lastDownloadEventTick = DateTime.Now;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("DOWNLOADWATCHER"); }
            finally
            {
                //lastDownloadEventTick = DateTime.Now;
                lastDownloadEventFile = e.FullPath;
                lastDownloadEventType = e.ChangeType;
            }
        }

        private static void OnDownloadRenamed(object source, RenamedEventArgs e)
        {
#if DEBUG
            // Specify what is done when a file is renamed.
            $"File: {e.OldFullPath} renamed to {e.FullPath}".DEBUG("DOWNLOADWATCHER");
#endif
            try
            {
                //if (e.ChangeType == lastDownloadEventType &&
                //    e.FullPath.Equals(lastDownloadEventFile, StringComparison.CurrentCultureIgnoreCase) &&
                //    lastDownloadEventTick.Ticks.DeltaNowMillisecond() < 10) throw new Exception("Same download event!");
                if (e.ChangeType == WatcherChangeTypes.Renamed)
                {
                    e.OldFullPath.DownloadedCacheUpdate(e.FullPath);
                    if (ext_imgs.Contains(Path.GetExtension(e.Name).ToLower()))
                    {
                        UpdateDownloadStateAsync(GetIllustId(e.Name));
                        lastDownloadEventTick = DateTime.Now;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("DOWNLOADWATCHER"); }
            finally
            {
                //lastDownloadEventTick = DateTime.Now;
                lastDownloadEventFile = e.FullPath;
                lastDownloadEventType = e.ChangeType;
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void InitDownloadedWatcher(this IEnumerable<StorageType> storages)
        {
            ConcurrentDictionary<string, StorageType> items = new ConcurrentDictionary<string, StorageType>();
            foreach (var ls in storages)
            {
                var folder = Path.GetFullPath(ls.Folder.MacroReplace("%ID%", "")).TrimEnd('\\');
                var parent = storages.Where(o => folder.StartsWith(o.Folder, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                if (items.ContainsKey(folder))
                {
                    if (parent is StorageType && parent.IncludeSubFolder) ls.Cached = true;
                    continue;
                }
                items.TryAdd(ls.Folder.TrimEnd('\\'), ls);
            }

            storages.ReleaseDownloadedWatcher();
            if (_watchers == null) _watchers = new ConcurrentDictionary<string, FileSystemWatcher>();

            foreach (var i in items)
            {
                var folder = i.Key;
                var storage = i.Value;

                if (Directory.Exists(folder))
                {
                    folder.UpdateDownloadedListCacheAsync();
                    var watcher = new FileSystemWatcher(folder, "*.*")
                    {
                        NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                        IncludeSubdirectories = storage is StorageType ? storage.IncludeSubFolder : false
                    };
                    watcher.Changed += new FileSystemEventHandler(OnDownloadChanged);
                    watcher.Created += new FileSystemEventHandler(OnDownloadChanged);
                    watcher.Deleted += new FileSystemEventHandler(OnDownloadChanged);
                    watcher.Renamed += new RenamedEventHandler(OnDownloadRenamed);
                    // Begin watching.
                    watcher.EnableRaisingEvents = true;

                    _watchers.AddOrUpdate(folder, watcher, (k, v) => watcher);
                }
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void AddDownloadedWatcher(this string folder, bool IncludeSubFolder = false)
        {
            if (Directory.Exists(folder) && !_watchers.ContainsKey(folder))
            {
                folder.UpdateDownloadedListCacheAsync();
                var watcher = new FileSystemWatcher(folder, "*.*")
                {
                    NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    IncludeSubdirectories = IncludeSubFolder
                };
                watcher.Changed += new FileSystemEventHandler(OnDownloadChanged);
                watcher.Created += new FileSystemEventHandler(OnDownloadChanged);
                watcher.Deleted += new FileSystemEventHandler(OnDownloadChanged);
                watcher.Renamed += new RenamedEventHandler(OnDownloadRenamed);
                // Begin watching.
                watcher.EnableRaisingEvents = true;

                _watchers[folder] = watcher;
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void ReleaseDownloadedWatcher(this IEnumerable<StorageType> storages)
        {
            if (_watchers is ConcurrentDictionary<string, FileSystemWatcher>)
            {
                foreach (var w in _watchers)
                {
                    try
                    {
                        if (w.Value is FileSystemWatcher)
                        {
                            w.Value.Dispose();
                        }
                    }
                    catch (Exception ex) { ex.ERROR("ReleaseDownloadedWatcher"); }
                }
                _watchers.Clear();
            }
        }

        public static void UpdateDownloadStateAsync(string illustid = default(string), bool? exists = null)
        {
            int id = -1;
            int.TryParse(illustid, out id);
            UpdateDownloadStateAsync(id, exists);
        }

        public static async void UpdateDownloadStateAsync(int? illustid = null, bool? exists = null)
        {
            await new Action(() =>
            {
                foreach (var win in Application.Current.Windows)
                {
                    if (win is MainWindow)
                    {
                        var mw = win as MainWindow;
                        mw.UpdateDownloadState(illustid, exists);
                    }
                    else if (win is ContentWindow)
                    {
                        var w = win as ContentWindow;
                        if (w.Content is IllustDetailPage)
                            (w.Content as IllustDetailPage).UpdateDownloadStateAsync(illustid, exists);
                        else if (w.Content is IllustImageViewerPage)
                            (w.Content as IllustImageViewerPage).UpdateDownloadStateAsync(illustid, exists);
                        else if (w.Content is SearchResultPage)
                            (w.Content as SearchResultPage).UpdateDownloadStateAsync(illustid, exists);
                        else if (w.Content is HistoryPage)
                            (w.Content as HistoryPage).UpdateDownloadStateAsync(illustid, exists);
                        else if (w.Content is DownloadManagerPage)
                            (w.Content as DownloadManagerPage).UpdateDownloadStateAsync(illustid, exists);
                    }
                }
            }).InvokeAsync();
        }

        public static async void UpdateDownloadStateAsync(this ImageListGrid list, int? illustid = null, bool? exists = null)
        {
            await new Action(() =>
            {
                UpdateDownloadState(list, illustid, exists);
            }).InvokeAsync();
        }

        public static void UpdateDownloadState(this ImageListGrid list, int? illustid = null, bool? exists = null)
        {
            try
            {
                list.Items.UpdateDownloadState(illustid, exists);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void UpdateDownloadState(this ItemCollection items, int? illustid = null, bool? exists = null)
        {
            try
            {
                items.UpdateDownloadState(illustid, exists);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static async void UpdateDownloadStateAsync(this ObservableCollection<PixivItem> collection, int? illustid = null, bool? exists = null)
        {
            await new Action(() =>
            {
                UpdateDownloadState(collection, illustid, exists);
            }).InvokeAsync();
        }

        public static void UpdateDownloadState(this ObservableCollection<PixivItem> collection, int? illustid = null, bool? exists = null)
        {
            try
            {
                var id = illustid ?? -1;
                foreach (var item in collection)
                {
                    if (item.IsPage() || item.IsPages())
                    {
                        item.IsDownloaded = item.Illust.GetOriginalUrl(item.Index).IsDownloadedAsync();
                    }
                    else if (item.IsWork())
                    {
                        var part_down = item.Illust.IsPartDownloadedAsync();
                        if (id == -1)
                            item.IsDownloaded = part_down;
                        else if (id == (int)(item.Illust.Id))
                        {
                            if (item.Count > 1)
                                item.IsDownloaded = part_down;
                            else
                                item.IsDownloaded = exists ?? part_down;
                        }
                        item.IsPartDownloaded = part_down;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("UpdateDownloadState"); }
        }
        #endregion

        #region Check Download State routines
        #region IsDownloaded
        private class DownloadState
        {
            public string Path { get; set; } = string.Empty;
            public bool Exists { get; set; } = false;
        }

        internal static bool IsDownloadedAsync(this Pixeez.Objects.Work illust, bool is_meta_single_page = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsDownloadedAsync(is_meta_single_page));
            else
                return (false);
        }

        internal static bool IsDownloaded(this Pixeez.Objects.Work illust, bool is_meta_single_page = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsDownloaded(is_meta_single_page));
            else
                return (false);
        }

        internal static bool IsDownloadedAsync(this Pixeez.Objects.Work illust, out string filepath, bool is_meta_single_page = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsDownloadedAsync(out filepath, is_meta_single_page));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsDownloaded(this Pixeez.Objects.Work illust, out string filepath, bool is_meta_single_page = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsDownloaded(out filepath, is_meta_single_page));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsDownloadedAsync(this Pixeez.Objects.Work illust, int index = -1)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloadedAsync());
            else
                return (false);
        }

        internal static bool IsDownloaded(this Pixeez.Objects.Work illust, int index = -1)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloaded());
            else
                return (false);
        }

        internal static bool IsDownloadedAsync(this Pixeez.Objects.Work illust, out string filepath, int index = -1, bool is_meta_single_page = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloadedAsync(out filepath, is_meta_single_page));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsDownloaded(this Pixeez.Objects.Work illust, out string filepath, int index = -1, bool is_meta_single_page = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloaded(out filepath, is_meta_single_page));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        private static Func<string, bool, bool> IsDownloadedFunc = (url, meta) => IsDownloaded(url, meta);
        internal static bool IsDownloadedAsync(this string url, bool is_meta_single_page = false)
        {
            return (IsDownloadedFunc(url, is_meta_single_page));
        }

        private static Func<string, string, bool, DownloadState> IsDownloadedFileFunc = (url, file, meta) =>
        {
            var state = new DownloadState();
            file = string.Empty;
            state.Exists = IsDownloaded(url, out file, meta);
            state.Path = file;
            return(state);
        };

        internal static bool IsDownloadedAsync(this string url, out string filepath, bool is_meta_single_page = false)
        {
            filepath = string.Empty;
            var result = IsDownloadedFileFunc(url, filepath, is_meta_single_page);
            filepath = result.Path;
            return (result.Exists); ;
        }

        internal static bool IsDownloaded(this string url, bool is_meta_single_page = false)
        {
            string fp = string.Empty;
            return (IsDownloaded(url, out fp, is_meta_single_page));
        }

        internal static bool IsDownloaded(this string url, out string filepath, bool is_meta_single_page = false)
        {
            bool result = false;
            filepath = string.Empty;

            var file = url.GetImageName(is_meta_single_page);
            foreach (var local in setting.LocalStorage)
            {
                if (string.IsNullOrEmpty(local.Folder)) continue;

                var folder = local.Folder.FolderMacroReplace(url.GetIllustId());
                if (Directory.Exists(folder))
                {
                    folder.UpdateDownloadedListCacheAsync(local.Cached);

                    var f = Path.Combine(folder, file);
                    if (local.Cached)
                    {
                        if (f.DownloadedCacheExistsAsync())
                        {
                            filepath = f;
                            result = true;
                            break;
                        }
                    }
                    else
                    {
                        if (File.Exists(f))
                        {
                            filepath = f;
                            result = true;
                            break;
                        }
                    }
                }
            }
            if(!string.IsNullOrEmpty(filepath)) filepath.Touch(url, meta: true);
            return (result);
        }
        #endregion

        #region IsPartDownloaded
        internal static bool IsPartDownloadedAsync(this PixivItem item)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloadedAsync());
            else
                return (false);
        }

        internal static bool IsPartDownloaded(this PixivItem item)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloaded());
            else
                return (false);
        }

        internal static bool IsPartDownloadedAsync(this PixivItem item, out string filepath)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloadedAsync(out filepath));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsPartDownloaded(this PixivItem item, out string filepath)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloaded(out filepath));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsPartDownloadedAsync(this Pixeez.Objects.Work illust)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsPartDownloadedAsync());
            else
                return (false);
        }

        internal static bool IsPartDownloaded(this Pixeez.Objects.Work illust)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsPartDownloaded());
            else
                return (false);
        }

        internal static bool IsPartDownloadedAsync(this Pixeez.Objects.Work illust, out string filepath)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsPartDownloadedAsync(out filepath));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsPartDownloaded(this Pixeez.Objects.Work illust, out string filepath)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsPartDownloaded(out filepath));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        private static Func<string, bool> IsPartDownloadedFunc = (url) => IsPartDownloaded(url);
        internal static bool IsPartDownloadedAsync(this string url)
        {
            return (IsPartDownloaded(url));
        }

        private static Func<string, string, DownloadState> IsPartDownloadedFileFunc = (url, file) =>
        {
            var state = new DownloadState();
            file = string.Empty;
            state.Exists = IsPartDownloaded(url, out file);
            state.Path = file;
            return(state);
        };

        internal static bool IsPartDownloadedAsync(this string url, out string filepath)
        {
            filepath = string.Empty;
            var result = IsPartDownloadedFileFunc(url, filepath);
            filepath = result.Path;
            return (result.Exists);
        }

        internal static bool IsPartDownloaded(this string url, out string filepath)
        {
            bool result = false;
            var file = url.GetImageName(true);

            filepath = string.Empty;
            foreach (var local in setting.LocalStorage)
            {
                if (string.IsNullOrEmpty(local.Folder)) continue;

                var folder = local.Folder.FolderMacroReplace(url.GetIllustId());
                if (Directory.Exists(folder))
                {
                    folder.UpdateDownloadedListCacheAsync(local.Cached);

                    var f = Path.Combine(folder, file);
                    if (local.Cached)
                    {
                        if (f.DownloadedCacheExistsAsync())
                        {
                            filepath = f;
                            result = true;
                            break;
                        }
                    }
                    else
                    {
                        if (File.Exists(f))
                        {
                            filepath = f;
                            result = true;
                            break;
                        }
                    }

                    var fn = Path.GetFileNameWithoutExtension(file);
                    var files = Directory.GetFiles(folder, $"{fn}_*.*").OrderBy(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(50, '0')));
                    if (files.Count() > 0)
                    {
                        foreach (var fc in files.Skip(1)) fc.Touch(url, meta: false);
                        filepath = files.First();
                        result = true;
                    }
                }
                if (result) break;
            }
            if (!string.IsNullOrEmpty(filepath)) filepath.Touch(url, meta: true);

            return (result);
        }

        internal static bool IsPartDownloaded(this string url)
        {
            string fp = string.Empty;
            return (IsPartDownloaded(url, out fp));
        }
        #endregion
        #endregion

        #region Download/Convert/Resize Image routines
        private static Dictionary<string, string[]> exts = new Dictionary<string, string[]>()
        {
            { ".png", new string[] { ".png", "image/png", "PNG" } },
            { ".bmp", new string[] { ".bmp", "image/bmp", "image/bitmap" } },
            { ".gif", new string[] { ".gif", "image/gif", "image/gif89" } },
            { ".tif", new string[] { ".tif", "image/tiff", "image/tif", ".tiff" } },
            { ".tiff", new string[] { ".tif", "image/tiff", "image/tif", ".tiff" } },
            { ".jpg", new string[] { ".jpg", "image/jpg", "image/jpeg", ".jpeg" } },
            { ".jpeg", new string[] { ".jpg", "image/jpg", "image/jpeg", ".jpeg" } },
        };

        public static string GetPixivLinkPattern(this string url)
        {
            return (@"http(s)*://.*?\.((pixiv\..*?)|(pximg\..*?))/");
        }

        public static bool IsPixivImage(this string url)
        {
            var pattern = @"http(s)*://.*?\.(pximg\.net/.*?)/";
            if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
                return (true);
            else
                return (false);
        }

        public static bool IsPixivLink(this string url)
        {
            var pattern = url.GetPixivLinkPattern();
            if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
                return (true);
            else
                return (false);
        }

        private static bool IsFileReady(this string filename)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                if (!File.Exists(filename)) return true;

                using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                    return inputStream.Length > 0;
            }
            catch (Exception ex) { ex.ERROR(); return false; }
        }

        private static void WaitForFile(this string filename)
        {
            //This will lock the execution until the file is ready
            //TODO: Add some logic to make it async and cancelable
            while (!IsFileReady(filename)) { }
        }

        public static bool WaitFileUnlock(this FileInfo file, int interval = 50, int times = 20)
        {
            int wait_count = times;
            while (file.IsLocked() && wait_count > 0) { wait_count--; Task.Delay(interval).GetAwaiter().GetResult(); }
            return (true);
        }

        public static bool WaitFileUnlock(this string filename, int interval = 50, int times = 20)
        {
            int wait_count = times;
            while (filename.IsLocked() && wait_count > 0) { wait_count--; Task.Delay(interval).GetAwaiter().GetResult(); }
            return (true);
        }

        public static async Task<bool> WaitFileUnlockAsync(this FileInfo file, int interval = 50, int times = 20)
        {
            int wait_count = times;
            while (file.IsLocked() && wait_count > 0) { wait_count--; await Task.Delay(interval); }
            return (true);
        }

        public static async Task<bool> WaitFileUnlockAsync(this string filename, int interval = 50, int times = 20)
        {
            int wait_count = times;
            while (filename.IsLocked() && wait_count > 0) { wait_count--; await Task.Delay(interval); }
            return (true);
        }

        public static bool IsLocked(this string file)
        {
            bool result = false;
            FileStream stream = null;
            try
            {
                if (File.Exists(file))
                {
                    using (stream = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        stream.Close();
                        stream.Dispose();
                    }
                }
            }
            catch (Exception)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                result = true;
                try { if (stream is FileStream) { stream.Close(); stream.Dispose(); } }
                catch (Exception) { }
            }
            //file is not locked
            return (result);
        }

        public static bool IsLocked(this FileInfo file)
        {
            bool result = false;
            FileStream stream = null;
            try
            {
                if (file.Exists)
                {
                    using (stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        stream.Close();
                        stream.Dispose();
                    }
                }
            }
            catch (Exception)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                result = true;
                try { if (stream is FileStream) { stream.Close(); stream.Dispose(); } }
                catch (Exception) { }
            }
            //file is not locked
            return (result);
        }

        public static bool IsCached(this string url)
        {
            bool result = false;
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = cache.IsCached(url);
            }
            return (result);
        }

        public static long GetFileLength(this string filename)
        {
            long result = -1;
            if (File.Exists(filename))
            {
                result = new FileInfo(filename).Length;
            }
            return (result);
        }

        public static FileInfo GetFileInfo(this string filename)
        {
            FileInfo result = null;
            if (File.Exists(filename))
            {
                result = new FileInfo(filename);
            }
            return (result);
        }

        public static DateTime GetFileTime(this string filename, string mode = "m")
        {
            DateTime result = default(DateTime);
            if (File.Exists(filename))
            {
                mode = mode.ToLower();
                if (mode.Equals("c"))
                    result = new FileInfo(filename).CreationTime;
                else if (mode.Equals("m"))
                    result = new FileInfo(filename).LastWriteTime;
                else if (mode.Equals("a"))
                    result = new FileInfo(filename).LastAccessTime;
            }
            return (result);
        }

        public static string GetImageName(this string url, bool is_meta_single_page)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url))
            {
                result = Path.GetFileName(url).Replace("_p", "_");
                if (is_meta_single_page) result = result.Replace("_0.", ".");
            }
            return (result);
        }

        public static string GetImageCachePath(this string url)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = cache.GetCacheFile(url);
            }
            return (result);
        }

        public static string GetImageCacheFile(this string url)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = cache.GetImagePath(url);
            }
            return (result);
        }

        public static async Task<ImageSource> ToImageSource(this string url, Size size = default(Size))
        {
            ImageSource result = null;

            var ContentType = string.Empty;
            var ext = Path.GetExtension(url).ToLower();
            switch (ext)
            {
                case ".jpeg":
                case ".jpg":
                    ContentType = "image/jpeg";
                    break;
                case ".png":
                    ContentType = "image/png";
                    break;
                case ".bmp":
                    ContentType = "image/bmp";
                    break;
                case ".gif":
                    ContentType = "image/gif";
                    break;
                case ".webp":
                    ContentType = "image/webp";
                    break;
                case ".tiff":
                case ".tif":
                    ContentType = "image/tiff";
                    break;
                default:
                    ContentType = "application/octet-stream";
                    break;
            }
            try
            {
                var dpi = DPI.Default;
                HttpClient client = Application.Current.GetHttpClient(is_download: true);
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Content-Type", ContentType);
                HttpResponseMessage response = await client.SendAsync(request);
                byte[] content = await response.Content.ReadAsByteArrayAsync();
                result = await content.ToBitmapSource(size);
            }
            catch (Exception ex) { ex.ERROR("ToImageSource"); }

            return (result);
        }

        public static async Task<ImageSource> ToImageSource(this string url, Pixeez.Tokens tokens)
        {
            ImageSource result = null;
            //url = Regex.Replace(url, @"//.*?\.pixiv.net/", "//i.pximg.net/", RegexOptions.IgnoreCase);
            using (var response = await tokens.SendRequestAsync(Pixeez.MethodType.GET, url))
            {
                if (response.Source.StatusCode == HttpStatusCode.OK)
                    result = await response.ToImageSource();
                else
                    result = null;
            }
            return (result);
        }

        public static async Task<ImageSource> ToImageSource(this Pixeez.AsyncResponse response, Size size = default(Size))
        {
            ImageSource result = null;
            using (var stream = await response.GetResponseStreamAsync())
            {
                result = stream.ToImageSource(size);
                stream.Close();
                stream.Dispose();
            }
            return (result);
        }

        public static async Task<ImageSource> ToImageSource(this ImageSource source, double width, double height)
        {
            ImageSource result = source;
            try
            {
                if (source is BitmapSource && width > 0 && height > 0)
                {
                    await new Action(() =>
                    {
                        var scale = new ScaleTransform(width / source.Width, height / source.Height);
                        result = new TransformedBitmap(source as BitmapSource, scale);
                        result.Freeze();
                    }).InvokeAsync(true);
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static async Task<BitmapSource> ToBitmapSource(this byte[] buffer, Size size = default(Size))
        {
            BitmapSource result = null;
            try
            {
                var ms = new MemoryStream(buffer);
                ms.Seek(0, SeekOrigin.Begin);
                result = BitmapFrame.Create(ms);
                await ms.FlushAsync();
                if (size.Width > 0 && size.Height > 0 && (size.Width != result.PixelWidth || size.Height != result.PixelHeight))
                {
                    result = result.ToBitmapSource(size);
                }
            }
            catch (Exception ex) { ex.ERROR("ToBitmapSource"); }
            return (result);
        }

        public static async Task<MemoryStream> ToMemoryStream(this BitmapSource bitmap, string fmt = "")
        {
            MemoryStream result = new MemoryStream();
            try
            {
                if (string.IsNullOrEmpty(fmt)) fmt = ".png";
                dynamic encoder = null;
                switch (fmt)
                {
                    case "image/bmp":
                    case "image/bitmap":
                    case "CF_BITMAP":
                    case "CF_DIB":
                    case ".bmp":
                        encoder = new BmpBitmapEncoder();
                        break;
                    case "image/gif":
                    case "gif":
                    case ".gif":
                        encoder = new GifBitmapEncoder();
                        break;
                    case "image/png":
                    case "png":
                    case ".png":
                        encoder = new PngBitmapEncoder();
                        break;
                    case "image/jpg":
                    case ".jpg":
                        encoder = new JpegBitmapEncoder();
                        break;
                    case "image/jpeg":
                    case ".jpeg":
                        encoder = new JpegBitmapEncoder();
                        break;
                    case "image/tif":
                    case ".tif":
                        encoder = new TiffBitmapEncoder();
                        break;
                    case "image/tiff":
                    case ".tiff":
                        encoder = new TiffBitmapEncoder();
                        break;
                    default:
                        encoder = new PngBitmapEncoder();
                        break;
                }
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(result);
                await result.FlushAsync();
            }
            catch (Exception ex) { ex.ERROR("ENCODER"); }
            return (result);
        }

        public static async Task<MemoryStream> ToMemoryStream(this Pixeez.AsyncResponse response)
        {
            MemoryStream result = null;
            using (var stream = await response.GetResponseStreamAsync())
            {
                result = new MemoryStream();
                await stream.CopyToAsync(result);
            }
            return (result);
        }

        public static ImageSource ToImageSource(this Stream stream, Size size = default(Size))
        {
            setting = Application.Current.LoadSetting();
            var dpi = DPI.Default;

            BitmapSource result = null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CreateOptions = BitmapCreateOptions.None;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                if (!size.Equals(default(Size)) && size.Width >= 0 && size.Height >= 0)
                {
                    bmp.DecodePixelWidth = (int)Math.Ceiling(size.Width * dpi.ScaleX);
                    bmp.DecodePixelHeight = (int)Math.Ceiling(size.Height * dpi.ScaleY);
                }
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();

                result = bmp;
                result.Freeze();
                bmp = null;
            }
            catch (Exception ex)
            {
                // maybe loading webp.
                var ret = ex.Message;
                try
                {
                    //result = stream.ToWriteableBitmap(size);
                    var bmp0 = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                    var bmp = bmp0.ResizeImage(size) as BitmapSource;

                    result = null;
                    result = bmp == null ? bmp0 : bmp;
                    result.Freeze();
                    bmp = null;
                    bmp0 = null;
                }
                catch (Exception exx) { exx.ERROR("ToImageSource"); }
            }
            finally
            {
                if (setting.AutoConvertDPI && result is ImageSource)
                {
                    try
                    {
                        if (result.DpiX != dpi.X || result.DpiY != dpi.Y)
                        //if (result.DpiX > dpi.X15 || result.DpiY > dpi.Y15)
                        {
                            var bmp = ConvertBitmapDPI(result, dpi.X, dpi.Y);
                            result = null;
                            result = bmp;
                            result.Freeze();
                            bmp = null;
                        }
                    }
                    catch (Exception ex) { ex.ERROR("ConvertDPI"); }
                }
            }
            return (result);
        }

        public static BitmapSource ToBitmapSource(this ImageSource source, Size size = default(Size))
        {
            BitmapSource result = source is BitmapSource ? source as BitmapSource : null;
            try
            {
                if (source is ImageSource && source.Width > 0 && source.Height > 0 && size.Width > 0 && size.Height > 0 && (source.Width != size.Width || source.Height != size.Height))
                {
                    var dpi = DPI.Default;
                    RenderTargetBitmap target = null;
                    if (size != default(Size) && size.Width > 0 && size.Height > 0)
                        target = new RenderTargetBitmap((int)(size.Width), (int)(size.Height), dpi.X, dpi.Y, PixelFormats.Pbgra32);
                    else
                        target = new RenderTargetBitmap((int)(source.Width), (int)(source.Height), dpi.X, dpi.Y, PixelFormats.Pbgra32);

                    DrawingVisual drawingVisual = new DrawingVisual();
                    using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                    {
                        drawingContext.DrawImage(source, new Rect(0, 0, target.Width, target.Height));
                    }
                    target.Render(drawingVisual);

                    int width = target.PixelWidth;
                    int height = target.PixelHeight;
                    var palette = target.Palette;
                    int stride = width * ((target.Format.BitsPerPixel + 31) / 32 * 4);
                    byte[] pixelData = new byte[stride * height];
                    target.CopyPixels(pixelData, stride, 0);

                    result = BitmapSource.Create(width, height,
                                                target.DpiX, target.DpiY,
                                                target.Format, target.Palette,
                                                pixelData, stride);
                    pixelData = null;
                    target = null;
                }
            }
            catch (Exception ex) { ex.ERROR("ToBitmapSource"); }
            return (result);
        }

        public static WriteableBitmap ToWriteableBitmap(this Stream stream, Size size = default(Size))
        {
            WriteableBitmap result = default(WriteableBitmap);
            try
            {
                setting = Application.Current.LoadSetting();
                var dpi = DPI.Default;

                var bmp = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.None);
                result = bmp.ToWriteableBitmap(size);
                result.Freeze();
                bmp = null;
            }
            catch (Exception ex) { ex.ERROR("ToWriteableBitmap"); }
            return (result);
        }

        public static WriteableBitmap ToWriteableBitmap(this BitmapSource bitmap, Size size = default(Size))
        {
            WriteableBitmap result = default(WriteableBitmap);
            try
            {
                setting = Application.Current.LoadSetting();
                var dpi = DPI.Default;

                int width = bitmap.PixelWidth;
                int height = bitmap.PixelHeight;
                int stride = width * ((bitmap.Format.BitsPerPixel + 31) / 32 * 4);
                byte[] pixelData = new byte[stride * height];
                bitmap.CopyPixels(pixelData, stride, 0);
                var format = bitmap.Format.ToString().Equals(PixelFormats.Default.ToString()) ? PixelFormats.Pbgra32 : bitmap.Format;

                var src = BitmapSource.Create(width, height, dpi.X, dpi.Y, format, bitmap.Palette, pixelData, stride);
                var wbs = new WriteableBitmap(src);

                if (false && !size.Equals(default(Size)) && size.Width >= 0 && size.Height >= 0)
                {
                    int width_new =(int)(size.Width * dpi.ScaleX);
                    int height_new =(int)(size.Height * dpi.ScaleY);
                    result = new WriteableBitmap(width_new, height_new, dpi.X, dpi.Y, wbs.Format, wbs.Palette);
                    lock (result)
                    {
                        using (result.GetBitmapContext())
                        {
                            result.Blit(new Rect(0, 0, width_new, height_new), wbs, new Rect(0, 0, width, height));
                        }
                    }
                }
                else
                {
                    result = wbs;
                }
                result.Freeze();
                wbs = null;
                bitmap = null;
            }
            catch (Exception ex) { ex.ERROR("ToWriteableBitmap"); }
            return (result);
        }

        public static byte[] ToBytes(this string file)
        {
            if (string.IsNullOrEmpty(file)) return (null);

            byte[] result = null;

            if (File.Exists(file))
            {
                result = File.ReadAllBytes(file);
            }

            return (result);
        }

        public static async Task<byte[]> ToBytes(this BitmapSource bitmap, string fmt = "")
        {
            if (string.IsNullOrEmpty(fmt)) fmt = ".png";
            return ((await bitmap.ToMemoryStream(fmt)).ToArray());
        }

        public static async Task<byte[]> ToBytes(this byte[] buffer, string fmt = "")
        {
            if (string.IsNullOrEmpty(fmt)) fmt = ".png";
            var bitmap = await buffer.ToBitmapSource();
            return (await bitmap.ToBytes(fmt));
        }

        public static BitmapSource ConvertBitmapDPI(this BitmapSource source, double dpiX = 96, double dpiY = 96)
        {
            if (dpiX == source.DpiX || dpiY == source.DpiY) return (source);

            int width = source.PixelWidth;
            int height = source.PixelHeight;

            var palette = source.Palette;
            int stride = width * ((source.Format.BitsPerPixel + 31) / 32 * 4);
            byte[] pixelData = new byte[stride * height];
            source.CopyPixels(pixelData, stride, 0);

            BitmapSource result = source;
            try
            {
                var bmp = BitmapSource.Create(width, height, dpiX, dpiY, source.Format, palette, pixelData, stride);
                result = null;
                result = bmp;
                result.Freeze();
                bmp = null;
            }
            catch (Exception ex)
            {
                ex.ERROR("CONVERT");
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        var bmp = BitmapSource.Create(width, height, dpiX, dpiY, source.Format, palette, pixelData, stride);
                        PngBitmapEncoder pngEnc = new PngBitmapEncoder();
                        var fbmp = BitmapFrame.Create(bmp);
                        pngEnc.Frames.Add(fbmp);
                        pngEnc.Save(ms);
                        var pngDec = new PngBitmapDecoder(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                        result = pngDec.Frames[0];
                        result.Freeze();

                        pngEnc.Frames.Clear();
                        pngEnc = null;
                        pngDec = null;
                        fbmp = null;
                        bmp = null;
                        ms.Close();
                        ms.Dispose();
                    }
                }
                catch (Exception exx)
                {
                    exx.Message.ShowMessageBox("ERROR");
                }
            }
            finally
            {
                Array.Clear(pixelData, 0, pixelData.Length);
                Array.Resize(ref pixelData, 0);
                pixelData = null;
            }
            return result;
        }

        public static ImageSource ResizeImage(this ImageSource source, Size size)
        {
            ImageSource result = source;
            try
            {
                var width = size.Width;
                var height = size.Height;
                if (width > 0 && height > 0)
                {
                    if (source is BitmapSource)
                    {
                        var bitmap = source as BitmapSource;
                        var dpi = DPI.Default;
                        var factorX = dpi.X / bitmap.DpiX;
                        var factorY = dpi.Y / bitmap.DpiY;
                        var scale = new ScaleTransform(width * factorX / source.Width, height * factorY / source.Height);
                        result = new TransformedBitmap(bitmap, scale);
                        result.Freeze();
                    }
                    else
                    {
                        result = source.ToBitmapSource(size);
                        result.Freeze();
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("ResizeImage"); }
            return (result);
        }

        public static ImageSource ResizeImage(this ImageSource source, double width, double height)
        {
            if (width <= 0 || height <= 0) return (source);
            else return (source.ResizeImage(new Size(width, height)));
        }

        private static byte[] ClipboardBuffer = null;

        public static async void CopyImage(this ImageSource source)
        {
            try
            {
                var bs = source.ToBitmapSource();

                DataObject dataPackage = new DataObject();
                MemoryStream ms = null;

                #region Copy Standard Bitmap date to Clipboard
                dataPackage.SetImage(bs);
                #endregion
                #region Copy other MIME format data to Clipboard
                string[] fmts = new string[] { "PNG", "image/png", "image/bmp", "image/jpg", "image/jpeg" };
                //string[] fmts = new string[] { };
                foreach (var fmt in fmts)
                {
                    if (fmt.Equals("CF_DIBV5", StringComparison.CurrentCultureIgnoreCase))
                    {
                        byte[] arr = await bs.ToBytes(fmt);
                        byte[] dib = arr.Skip(14).ToArray();
                        ms = new MemoryStream(dib);
                        dataPackage.SetData(fmt, ms);
                        await ms.FlushAsync();
                    }
                    else
                    {
                        byte[] arr = await bs.ToBytes(fmt);
                        ms = new MemoryStream(arr);
                        dataPackage.SetData(fmt, ms);
                        await ms.FlushAsync();
                    }
                }
                #endregion
                Clipboard.SetDataObject(dataPackage, true);
            }
            catch (Exception ex) { ex.ERROR("CopyImage"); }
        }

        public static async void CopyImage(this string file)
        {
            try
            {
                if (File.Exists(file))
                {
                    var ext = Path.GetExtension(file).ToLower();
                    ClipboardBuffer = file.ToBytes();
                    var bs = await ClipboardBuffer.ToBitmapSource();

                    DataObject dataPackage = new DataObject();
                    MemoryStream ms = null;

                    #region Copy Standard Bitmap date to Clipboard
                    dataPackage.SetImage(bs);
                    #endregion
                    #region Copy other MIME format data to Clipboard
                    string[] fmts = new string[] { "PNG", "image/png", "image/bmp", "image/jpg", "image/jpeg" };
                    //string[] fmts = new string[] { };
                    foreach (var fmt in fmts)
                    {
                        if (exts.ContainsKey(ext) && exts[ext].Contains(fmt))
                        {
                            ms = new MemoryStream(ClipboardBuffer);
                            dataPackage.SetData(fmt, ms);
                            await ms.FlushAsync();
                        }
                        else
                        {
                            if (fmt.Equals("CF_DIBV5", StringComparison.CurrentCultureIgnoreCase))
                            {
                                byte[] arr = await bs.ToBytes(fmt);
                                byte[] dib = arr.Skip(14).ToArray();
                                ms = new MemoryStream(dib);
                                dataPackage.SetData(fmt, ms);
                                await ms.FlushAsync();
                            }
                            else
                            {
                                byte[] arr = await bs.ToBytes(fmt);
                                ms = new MemoryStream(arr);
                                dataPackage.SetData(fmt, ms);
                                await ms.FlushAsync();
                            }
                        }
                    }
                    #endregion
                    Clipboard.SetDataObject(dataPackage, true);
                }
            }
            catch (Exception ex) { ex.ERROR("CopyImage"); }
        }

        public static async Task<bool> WriteToFile(this Stream source, string file, int bufferSize = 4096, FileMode mode = FileMode.OpenOrCreate, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.ReadWrite)
        {
            var result = false;
            using (var ms = new MemoryStream())
            {
                if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                await source.CopyToAsync(ms, bufferSize);
                if (ms.Length > 0)
                {
                    var folder = Path.GetDirectoryName(file);
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    await file.WaitFileUnlockAsync(1000, 10);
                    using (var fs = new FileStream(file, mode, access, share, bufferSize, true))
                    {
                        await fs.WriteAsync(ms.ToArray(), 0, (int)ms.Length);
                        await fs.FlushAsync();
                        fs.Close();
                        fs.Dispose();
                        result = true;
                    }
                }
                ms.Close();
                ms.Dispose();
            }
            return (result);
        }

        public static async Task<bool> WriteToFile(this Stream source, string file, Action<double, double> progressAction, ContentRangeHeaderValue range, int bufferSize = 4096, FileMode mode = FileMode.OpenOrCreate, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.ReadWrite)
        {
            var result = false;
            using (var ms = new MemoryStream())
            {
                if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                var length = range.HasLength ? range.Length ?? 0 : 0;
                int received = 0;
                if (length <= 0)
                {
                    await source.CopyToAsync(ms, bufferSize);
                    length = received = (int)ms.Length;
                }
                else
                {
                    setting = Application.Current.LoadSetting();
                    bufferSize = setting.DownloadHttpStreamBlockSize;
                    byte[] bytes = new byte[bufferSize];
                    int bytesread = 0;
                    do
                    {
                        var cancelReadStreamSource = new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout));
                        using (cancelReadStreamSource.Token.Register(() => source.Close()))
                        {
                            bytesread = await source.ReadAsync(bytes, 0, bufferSize, cancelReadStreamSource.Token).ConfigureAwait(false);
                        }

                        if (bytesread > 0 && bytesread <= bufferSize && received < length)
                        {
                            received += bytesread;
                            await ms.WriteAsync(bytes, 0, bytesread);
                            if (progressAction is Action<double, double>) progressAction.Invoke(received, length);
                        }
                    } while (bytesread > 0 && received < length);
                }

                if (received == length && ms.Length > 0)
                {
                    var folder = Path.GetDirectoryName(file);
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                    await file.WaitFileUnlockAsync(1000, 10);
                    using (var fs = new FileStream(file, mode, access, share, bufferSize, true))
                    {
                        await fs.WriteAsync(ms.ToArray(), 0, (int)ms.Length);
                        await fs.FlushAsync();
                        fs.Close();
                        fs.Dispose();
                        result = true;
                    }
                    if (progressAction is Action<double, double>) progressAction.Invoke(received, length);
                }

                ms.Close();
                ms.Dispose();
            }
            return (result);
        }
        #endregion

        #region Load/Save Image routines
        public static async Task<CustomImageSource> LoadImageFromFile(this string file, Size size = default(Size))
        {
            CustomImageSource result = new CustomImageSource();
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                try
                {
                    await file.WaitFileUnlockAsync(500, 10);
                    using (Stream stream = new MemoryStream(File.ReadAllBytes(file)))
                    {
                        result.Source = stream.ToImageSource(size);
                        result.SourcePath = file;
                        result.Size = stream.Length;
                        result.ColorDepth = result.Source is BitmapSource ? (result.Source as BitmapSource).Format.BitsPerPixel : 32;
                        stream.Close();
                        stream.Dispose();
                    }
                }
                catch (Exception ex) { ex.ERROR("LoadImageFromFile"); }
            }
            return (result);
        }

        public static async Task<CustomImageSource> LoadImageFromUrl(this string url, bool overwrite = false, bool login = false, Size size = default(Size), Action<double, double> progressAction = null)
        {
            CustomImageSource result = new CustomImageSource();
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = await cache.GetImage(url, overwrite, login, size, progressAction);
            }
            return (result);
        }

        public static async Task<CustomImageSource> LoadImageFromUri(this Uri uri, bool overwrite = false, Pixeez.Tokens tokens = null, Size size = default(Size), Action<double, double> progressAction = null)
        {
            CustomImageSource result = new CustomImageSource();
            if (uri.IsUnc || uri.IsFile)
                result = await LoadImageFromFile(uri.LocalPath, size);
            else if (!(uri.IsLoopback || uri.IsAbsoluteUri))
                result = await LoadImageFromUrl(uri.OriginalString, overwrite, false, size, progressAction);
            return (result);
        }

        public static async Task<string> DownloadCacheFile(this string url, bool overwrite = false)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = await cache.DownloadImage(url, overwrite);
            }
            return (result);
        }

        public static async Task<string> DownloadImage(this string url, string file, bool overwrite = true, Action<double, double> progressAction = null)
        {
            var result = string.Empty;
            if (!File.Exists(file) || overwrite || new FileInfo(file).Length <= 0)
            {
                setting = Application.Current.LoadSetting();
                HttpClient client = null;
                HttpResponseMessage response = null;
                try
                {
                    client = Application.Current.GetHttpClient(is_download: true);
                    using (response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                    {
                        //response.EnsureSuccessStatusCode();
                        if (response != null && (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.PartialContent))
                        {
                            var length = response.Content.Headers.ContentLength ?? 0;
                            var range = response.Content.Headers.ContentRange ?? new ContentRangeHeaderValue(0, 0, length);
                            var pos = range.From ?? 0;
                            var Length = range.Length ?? 0;

                            string vl = response.Content.Headers.ContentEncoding.FirstOrDefault();
                            using (var sr = vl != null && vl == "gzip" ? new System.IO.Compression.GZipStream(await response.Content.ReadAsStreamAsync(), System.IO.Compression.CompressionMode.Decompress) : await response.Content.ReadAsStreamAsync())
                            {
                                var ret = progressAction is Action<double, double> ? await sr.WriteToFile(file, progressAction, range) : await sr.WriteToFile(file);
                                if (ret) result = file;
                                sr.Close();
                                sr.Dispose();
                            }
                        }
                        response.Dispose();
                    }
                }
                catch (Exception ex) { ex.ERROR($"DownloadImage_{Path.GetFileName(file)}"); }
                finally
                {
                    if (response is HttpResponseMessage) response.Dispose();
                }
            }
            return (result);
        }

        public static async Task<string> DownloadImage(this string url, string file, Pixeez.Tokens tokens, bool overwrite = true)
        {
            var result = string.Empty;
            if (!File.Exists(file) || overwrite || new FileInfo(file).Length <= 0)
            {
                if (tokens == null) tokens = await ShowLogin();
                try
                {
                    using (var response = await tokens.SendRequestAsync(Pixeez.MethodType.GET, url))
                    {
                        //response.Source.EnsureSuccessStatusCode();
                        if (response != null && response.Source.StatusCode == HttpStatusCode.OK)
                        {
                            using (var sr = await response.GetResponseStreamAsync())
                            {
                                if (await sr.WriteToFile(file)) result = file;
                                sr.Close();
                                sr.Dispose();
                            }
                        }
                        response.Dispose();
                    }
                }
                catch (Exception ex) { ex.ERROR($"DownloadImage_{Path.GetFileName(file)}"); }
            }
            return (result);
        }

        public static async Task<bool> SaveImage(this string url, string file, bool overwrite = true, Action<double, double> progressAction = null)
        {
            bool result = false;
            if (url.IndexOf("https://") > 1 || url.IndexOf("http://") > 1) return (result);

            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    var unc = file.IndexOf("file:\\\\\\");
                    if (unc > 0) file = file.Substring(0, unc - 1);
                    else if (unc == 0) file = file.Substring(8);

                    result = !string.IsNullOrEmpty(await url.DownloadImage(file, overwrite, progressAction));
                }
                catch (Exception ex)
                {
                    if (ex is IOException)
                    {

                    }
                    else
                    {
                        ex.ERROR("SaveImage");
                    }
                }
            }
            return (result);
        }

        public static async Task<bool> SaveImage(this string url, Pixeez.Tokens tokens, string file, bool overwrite = true)
        {
            bool result = false;
            if (url.IndexOf("https://") > 1 || url.IndexOf("http://") > 1) return (result);

            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    var unc = file.IndexOf("file:\\\\\\");
                    if (unc > 0) file = file.Substring(0, unc - 1);
                    else if (unc == 0) file = file.Substring(8);

                    //if (string.IsNullOrEmpty(await url.DownloadImage(file, overwrite)))
                    result = !string.IsNullOrEmpty(await url.DownloadImage(file, tokens, overwrite));
                }
                catch (Exception ex)
                {
                    if (ex is IOException)
                    {

                    }
                    else
                    {
                        ex.ERROR("SaveImage");
                    }
                }
            }
            return (result);
        }

        public static async Task<string> SaveImage(this string url, Pixeez.Tokens tokens, bool is_meta_single_page = false, bool overwrite = true)
        {
            string result = string.Empty;

            var file = Application.Current.SaveTarget(url.GetImageName(is_meta_single_page));

            try
            {
                if (!string.IsNullOrEmpty(file))
                {
                    result = await url.DownloadImage(file, tokens, overwrite);
                }
            }
            catch (Exception ex)
            {
                if (ex is IOException)
                {

                }
                else
                {
                    ex.ERROR("SaveImage");
                }
            }
            return (result);
        }

        public static async Task<string> SaveImage(this string url, Pixeez.Tokens tokens, DateTime dt, bool is_meta_single_page = false, bool overwrite = true)
        {
            var file = await url.SaveImage(tokens, is_meta_single_page, overwrite);
            var id = url.GetIllustId();

            if (!string.IsNullOrEmpty(file))
            {
                File.SetCreationTime(file, dt);
                File.SetLastWriteTime(file, dt);
                File.SetLastAccessTime(file, dt);
                var state = "Succeed";
                $"{Path.GetFileName(file)} is saved!".ShowDownloadToast(state, file, state);

                if (Regex.IsMatch(file, @"_ugoira\d+\.", RegexOptions.IgnoreCase))
                {
                    var ugoira_url = url.Replace("img-original", "img-zip-ugoira");
                    //ugoira_url = Regex.Replace(ugoira_url, @"(_ugoira)(\d+)(\..*?)", "_ugoira1920x1080.zip", RegexOptions.IgnoreCase);
                    ugoira_url = Regex.Replace(ugoira_url, @"_ugoira\d+\..*?$", "_ugoira1920x1080.zip", RegexOptions.IgnoreCase);
                    var ugoira_file = await ugoira_url.SaveImage(tokens, dt, true, overwrite);
                    if (!string.IsNullOrEmpty(ugoira_file))
                    {
                        File.SetCreationTime(ugoira_file, dt);
                        File.SetLastWriteTime(ugoira_file, dt);
                        File.SetLastAccessTime(ugoira_file, dt);
                        state = "Succeed";
                        $"{Path.GetFileName(ugoira_file)} is saved!".ShowDownloadToast(state, ugoira_file, state);
                    }
                    else
                    {
                        state = "Failed";
                        $"Save {Path.GetFileName(ugoira_url)} failed!".ShowDownloadToast(state, "", state);
                    }
                }
            }
            else
            {
                var state = "Failed";
                $"Save {Path.GetFileName(url)} failed!".ShowDownloadToast(state, "", state);
            }
            return (file);
        }

        public static async Task<List<string>> SaveImage(Dictionary<string, DateTime> files, Pixeez.Tokens tokens, bool is_meta_single_page = false)
        {
            List<string> result = new List<string>();

            foreach (var file in files)
            {
                var f = await file.Key.SaveImage(tokens, file.Value, is_meta_single_page);
                result.Add(f);
            }
            SystemSounds.Beep.Play();

            return (result);
        }

        public static void SaveImage(this string url, string thumb, DateTime dt, bool is_meta_single_page = false, bool overwrite = true)
        {
            Commands.AddDownloadItem.Execute(new DownloadParams()
            {
                Url = url,
                ThumbUrl = thumb,
                Timestamp = dt,
                IsSinglePage = is_meta_single_page,
                OverwriteExists = overwrite
            });
        }

        public static void SaveImages(Dictionary<Tuple<string, bool>, Tuple<string, DateTime>> files, bool overwrite = true)
        {
            foreach (var file in files)
            {
                var url = file.Key.Item1;
                var is_meta_single_page =  file.Key.Item2;
                var thumb = file.Value.Item1;
                var dt = file.Value.Item2;
                url.SaveImage(thumb, dt, is_meta_single_page, overwrite);
            }
            SystemSounds.Beep.Play();
        }
        #endregion

        #region Illust routines
        #region SameIllust
        public static bool IsSameIllust(this string id, int hash)
        {
            return (cache.IsSameIllust(hash, id));
        }

        public static bool IsSameIllust(this long id, int hash)
        {
            return (cache.IsSameIllust(hash, $"{id}"));
        }

        public static bool IsSameIllust(this long? id, int hash)
        {
            return (cache.IsSameIllust(hash, $"{id ?? -1}"));
        }

        public static bool IsSameIllust(this PixivItem item, int hash)
        {
            bool result = false;

            if (item.IsWork())
            {
                result = item.Illust.GetPreviewUrl(item.Index).GetImageId().IsSameIllust(hash) || item.Illust.GetOriginalUrl(item.Index).GetImageId().IsSameIllust(hash);
            }

            return (result);
        }

        public static bool IsSameIllust(this PixivItem item, long id)
        {
            bool result = false;

            try
            {
                result = long.Parse(item.ID) == id;
            }
            catch (Exception ex) { ex.ERROR("IsSameIllust"); }

            return (result);
        }

        public static bool IsSameIllust(this PixivItem item, long? id)
        {
            bool result = false;

            try
            {
                result = long.Parse(item.ID) == (id ?? -1);
            }
            catch (Exception ex) { ex.ERROR("IsSameIllust"); }

            return (result);
        }

        public static bool IsSameIllust(this PixivItem item, PixivItem item_now)
        {
            bool result = false;

            try
            {
                result = long.Parse(item.ID) == long.Parse(item_now.ID) && item.Index == item_now.Index;
            }
            catch (Exception ex) { ex.ERROR(); }

            return (result);
        }

        public static IList<PixivItem> GetSelected(this ImageListGrid gallery, bool WithSelectionOrder, bool NonForAll = false)
        {
            var result = new List<PixivItem>();
            try
            {
                if (Keyboard.Modifiers == ModifierKeys.Control) WithSelectionOrder = !WithSelectionOrder;
                var items = gallery.SelectedItems.Count <= 0 && NonForAll ? gallery.Items : gallery.SelectedItems;
                if (WithSelectionOrder)
                {
                    result = items.ToList();
                }
                else
                {
                    foreach (var item in gallery.Items)
                    {
                        if (items.Contains(item)) result.Add(item);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static IList<PixivItem> GetSelected(this ImageListGrid gallery, bool NonForAll)
        {
            setting = Application.Current.LoadSetting();
            return (GetSelected(gallery, setting.OpenWithSelectionOrder, NonForAll));
        }

        public static IList<PixivItem> GetSelected(this ImageListGrid gallery)
        {
            setting = Application.Current.LoadSetting();
            return (GetSelected(gallery, setting.OpenWithSelectionOrder, setting.AllForSelectionNone));
        }

        public static IList<PixivItem> GetSelected(this ImageListGrid gallery, PixivItemType type)
        {
            setting = Application.Current.LoadSetting();
            var selected = GetSelected(gallery, setting.OpenWithSelectionOrder, setting.AllForSelectionNone);
            return (selected.Where(i => i.ItemType == type).ToList());
        }

        public static IList<PixivItem> GetSelectedIllusts(this ImageListGrid gallery)
        {
            setting = Application.Current.LoadSetting();
            var selected = GetSelected(gallery, setting.OpenWithSelectionOrder, setting.AllForSelectionNone);
            return (selected.Where(i => i.IsWork()).ToList());
        }

        public static IList<PixivItem> GetSelectedUsers(this ImageListGrid gallery)
        {
            setting = Application.Current.LoadSetting();
            var selected = GetSelected(gallery, setting.OpenWithSelectionOrder, setting.AllForSelectionNone);
            return (selected.Where(i => i.IsUser()).ToList());
        }
        #endregion

        #region History routines
        public static void AddToHistory(this PixivItem item)
        {
            //Commands.AddToHistory.Execute(illust);
            var win = "History".GetWindowByTitle();
            if (win is ContentWindow && win.Content is HistoryPage)
                (win.Content as HistoryPage).AddToHistory(item);
            else
                Application.Current.HistoryAdd(item);
        }

        public static void AddToHistory(this Pixeez.Objects.Work illust)
        {
            //Commands.AddToHistory.Execute(illust);
            var win = "History".GetWindowByTitle();
            if (win is ContentWindow && win.Content is HistoryPage)
                (win.Content as HistoryPage).AddToHistory(illust);
            else
                Application.Current.HistoryAdd(illust);
        }

        public static void AddToHistory(this Pixeez.Objects.User user)
        {
            //Commands.AddToHistory.Execute(user);
            var win = "History".GetWindowByTitle();
            if (win is ContentWindow && win.Content is HistoryPage)
                (win.Content as HistoryPage).AddToHistory(user);
            else
                Application.Current.HistoryAdd(user);
        }

        public static void AddToHistory(this Pixeez.Objects.UserBase user)
        {
            //Commands.AddToHistory.Execute(user);
            var win = "History".GetWindowByTitle();
            if (win is ContentWindow && win.Content is HistoryPage)
                (win.Content as HistoryPage).AddToHistory(user);
            else
                Application.Current.HistoryAdd(user);
        }

        public static void ShowHistory(this Application app)
        {
            Commands.OpenHistory.Execute(null);
        }
        #endregion

        #region Refresh Illust/User Info
        public static async Task<Pixeez.Objects.Work> RefreshIllust(this Pixeez.Objects.Work Illust, Pixeez.Tokens tokens = null)
        {
            var result = Illust.Id != null ? await RefreshIllust(Illust.Id.Value, tokens) : Illust;
            if (result == null)
            {
                "404 (Not Found) or 503 (Service Unavailable)".ShowToast("INFO");
                return (result);
            }
            try
            {
                if (Illust is Pixeez.Objects.IllustWork)
                {
                    var i = Illust as Pixeez.Objects.IllustWork;
                    if (result is Pixeez.Objects.IllustWork)
                    {
                        var r = result as Pixeez.Objects.IllustWork;
                        i.is_bookmarked = r.is_bookmarked;
                        i.is_muted = r.is_muted;
                        i.IsLiked = r.IsLiked;
                        i.IsManga = r.IsManga;
                    }
                    else if (result is Pixeez.Objects.NormalWork)
                    {
                        var r = result as Pixeez.Objects.NormalWork;
                        i.IsLiked = r.IsLiked;
                        i.IsManga = r.IsManga;
                        i.is_bookmarked = r.BookMarked;
                    }
                }
                else if (Illust is Pixeez.Objects.NormalWork)
                {
                    var i = Illust as Pixeez.Objects.NormalWork;
                    if (result is Pixeez.Objects.IllustWork)
                    {
                        var r = result as Pixeez.Objects.IllustWork;
                        i.IsLiked = r.IsLiked;
                        i.IsManga = r.IsManga;
                    }
                    else if (result is Pixeez.Objects.NormalWork)
                    {
                        var r = result as Pixeez.Objects.NormalWork;
                        i.IsLiked = r.IsLiked;
                        i.IsManga = r.IsManga;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("REFRESHILLUST"); }
            return (result);
        }

        public static async Task<Pixeez.Objects.Work> RefreshIllust(this string IllustID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.Work result = null;
            try
            {
                if (!string.IsNullOrEmpty(IllustID))
                    result = await RefreshIllust(Convert.ToInt32(IllustID), tokens);
            }
            catch (Exception ex) { ex.ERROR("REFRESHILLUST"); }
            return (result);
        }

        public static async Task<Pixeez.Objects.Work> RefreshIllust(this long IllustID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.Work result = null;
            if (IllustID < 0) return result;
            if (tokens == null) tokens = await ShowLogin();
            if (tokens == null) return result;
            try
            {
                var illusts = await tokens.GetWorksAsync(IllustID);
                if (illusts is List<Pixeez.Objects.NormalWork>)
                {
                    foreach (var illust in illusts)
                    {
                        illust.Cache();
                        result = illust;
                        break;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("REFRESHILLUST"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserBase> RefreshUser(this Pixeez.Objects.Work Illust, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.UserBase result = Illust.User;
            try
            {
                var user = await Illust.User.RefreshUser(tokens);
                if (user is Pixeez.Objects.UserBase && user.Id.Value == Illust.User.Id.Value)
                {
                    //Illust.User.is_followed = user.is_followed;
                    result = user;
                }
            }
            catch (Exception ex) { ex.ERROR("REFRESHUSER"); }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserBase> RefreshUser(this Pixeez.Objects.UserBase User, Pixeez.Tokens tokens = null)
        {
            var user = await RefreshUser(User.Id.Value);
            try
            {
                if (user is Pixeez.Objects.UserBase)
                {
                    User.is_followed = user.is_followed;
                    if (User is Pixeez.Objects.User)
                    {
                        var u = User as Pixeez.Objects.User;
                        u.IsFollowed = user.IsFollowed;
                        u.IsFollower = user.IsFollower;
                        u.IsFollowing = user.IsFollowing;
                        u.IsFriend = user.IsFriend;
                        u.IsPremium = user.IsFriend;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("REFRESHUSER"); }
            return (user);
        }

        public static async Task<Pixeez.Objects.User> RefreshUser(this string UserID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.User result = null;
            if (!string.IsNullOrEmpty(UserID))
            {
                try
                {
                    result = await RefreshUser(Convert.ToInt32(UserID), tokens);
                }
                catch (Exception ex) { ex.ERROR("REFRESHUSER"); }
            }
            return (result);
        }

        public static async Task<Pixeez.Objects.User> RefreshUser(this long UserID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.User result = null;
            if (UserID < 0) return (result);
            setting = Application.Current.LoadSetting();
            var force = UserID == 0 && !(setting.MyInfo is Pixeez.Objects.User) ? true : false;
            if (tokens == null) tokens = await ShowLogin(force);
            if (tokens == null) return (result);
            try
            {
                var users = await tokens.GetUsersAsync(UserID);
                foreach (var user in users)
                {
                    user.Cache();
                    if (user.Id.Value == UserID) result = user;
                }
            }
            catch (Exception ex) { ex.ERROR("REFRESHUSER"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserInfo> RefreshUserInfo(this string UserID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.UserInfo result = null;
            if (!string.IsNullOrEmpty(UserID))
            {
                try
                {
                    long id = -1;
                    if (long.TryParse(UserID, out id)) result = await RefreshUserInfo(id, tokens);
                }
                catch (Exception ex) { ex.ERROR("REFRESHUSERINFO"); }
            }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserInfo> RefreshUserInfo(this long UserID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.UserInfo result = null;
            if (UserID < 0) return (result);
            setting = Application.Current.LoadSetting();
            var force = UserID == 0 && !(setting.MyInfo is Pixeez.Objects.User) ? true : false;
            if (tokens == null) tokens = await ShowLogin(force);
            if (tokens == null) return (result);
            try
            {
                var userinfo = await tokens.GetUserInfoAsync($"{UserID}");
                if (userinfo is Pixeez.Objects.UserInfo)
                {
                    userinfo.Cache();
                    if (userinfo.user.Id.Value == UserID) result = userinfo;
                }
            }
            catch (Exception ex) { ex.ERROR("REFRESHUSERINFO"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserInfo> RefreshUserInfo(this long? UserID, Pixeez.Tokens tokens = null)
        {
            return (await RefreshUserInfo(UserID.Value));
        }

        public static async Task<Pixeez.Objects.UserInfo> RefreshUserInfo(this Pixeez.Objects.UserBase User, Pixeez.Tokens tokens = null)
        {
            return (await RefreshUserInfo(User.Id));
        }
        #endregion

        #region Like helper routines
        public static bool IsLiked(this Pixeez.Objects.Work illust)
        {
            bool result = false;
            if (illust is Pixeez.Objects.Work && illust.User is Pixeez.Objects.UserBase)
            {
                if (!IllustCache.ContainsKey(illust.Id)) illust.Cache();
                result = IllustCache[illust.Id].IsBookMarked();
            }
            return (result);
        }

        public static bool IsLiked(this Pixeez.Objects.UserBase user)
        {
            bool result = false;
            if (user is Pixeez.Objects.UserBase)
            {
                if (!UserCache.ContainsKey(user.Id)) user.Cache();
                var u = UserCache[user.Id];
                if (u is Pixeez.Objects.User)
                {
                    var old_user = u as Pixeez.Objects.User;
                    result = old_user.is_followed ?? old_user.IsFollowing ?? old_user.IsFollowed ?? false;
                }
                else if (u is Pixeez.Objects.NewUser)
                {
                    var old_user = u as Pixeez.Objects.NewUser;
                    result = old_user.is_followed ?? false;
                }
            }
            return (result);
        }

        public static bool IsLiked(this PixivItem item)
        {
            var result = false;
            if (item.IsUser()) result = item.User.IsLiked();
            else if (item.IsWork()) result = item.Illust.IsLiked();
            return (result);
        }

        public static async Task<bool> Like(this PixivItem item, bool pub = true)
        {
            if (item.IsWork())
            {
                var result = item.Illust.IsLiked() ? true : await item.LikeIllust(pub);
                UpdateLikeStateAsync((int)(item.Illust.Id));
                return (result);
            }
            else if (item.IsUser())
            {
                var result = item.User.IsLiked() ? true : await item.LikeUser(pub);
                UpdateLikeStateAsync((int)(item.User.Id), true);
                return (result);
            }
            else return false;
        }

        public static async Task<bool> UnLike(this PixivItem item, bool pub = true)
        {
            if (item.IsWork())
            {
                var result = item.Illust.IsLiked() ? await item.UnLikeIllust(pub) : false;
                UpdateLikeStateAsync((int)(item.Illust.Id));
                return (result);
            }
            else if (item.IsUser())
            {
                var result = item.User.IsLiked() ? await item.UnLikeUser(pub) : false;
                item.IsFavorited = result;
                UpdateLikeStateAsync((int)(item.User.Id), true);
                return (result);
            }
            else return false;
        }
        #endregion

        #region Like/Unlike Illust helper routines
        public class BookmarkState
        {
            public bool State { get; set; } = false;
            public bool IsBookmarked { get; set; } = false;
            public string Restrict { get; set; } = string.Empty;
        }
        public static async Task<BookmarkState> RefreshIllustBookmarkState(this Pixeez.Objects.Work illust)
        {
            BookmarkState result = new BookmarkState();

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            var bookmarkstate = await tokens.GetBookMarkedDetailAsync(illust.Id??-1);
            if (bookmarkstate is Pixeez.Objects.BookmarkDetailRootobject && bookmarkstate.bookmark_detail is Pixeez.Objects.BookmarkDetail)
            {
                var is_bookmarked = bookmarkstate.bookmark_detail.is_bookmarked;
                var restrict = bookmarkstate.bookmark_detail.restrict;
                if (illust is Pixeez.Objects.IllustWork)
                {
                    var i = illust as Pixeez.Objects.IllustWork;
                    i.is_bookmarked = is_bookmarked;
                }
                else if (illust is Pixeez.Objects.NormalWork)
                {
                    var i = illust as Pixeez.Objects.NormalWork;
                    i.IsLiked = is_bookmarked;
                }
                result.State = true;
                result.Restrict = restrict;
                result.IsBookmarked = is_bookmarked;
            }

            return (result);
        }

        /// <summary>
        /// Like Illust Work
        /// </summary>
        /// <param name="illust"></param>
        /// <param name="pub"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, Pixeez.Objects.Work>> LikeIllust(this Pixeez.Objects.Work illust, bool pub = true)
        {
            Tuple<bool, Pixeez.Objects.Work> result = new Tuple<bool, Pixeez.Objects.Work>(illust.IsLiked(), illust);

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            try
            {
                var mode = pub ? "public" : "private";
                var ret = await tokens.AddMyFavoriteWorksAsync((long)illust.Id, illust.Tags, mode);
                if (!ret) return (result);
            }
            catch (Exception ex) { ex.ERROR("AddMyFavoriteWorksAsync"); }
            finally
            {
                try
                {
                    illust = await illust.RefreshIllust();
                    if (illust != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.Work>(illust.IsLiked(), illust);
                        var info = "Liked";
                        var title = result.Item1 ? "Succeed" : "Failed";
                        var fail = result.Item1 ? "is" : "isn't";
                        var pub_like = pub ? "Public" : "Private";
                        $"Illust \"{illust.Title}\" {fail} {pub_like} {info}!".ShowToast($"{title}", illust.GetThumbnailUrl(), title, pub_like);
                    }
                }
                catch (Exception ex) { ex.ERROR("RefreshIllust"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            }

            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.Work>> Like(this Pixeez.Objects.Work illust, bool pub = true)
        {
            var result = await illust.LikeIllust(pub);
            UpdateLikeStateAsync((int)(illust.Id.Value), false);
            return (result);
        }

        public static async Task<bool> LikeIllust(this PixivItem item, bool pub = true)
        {
            bool result = false;

            if (item.IsWork())
            {
                var ret = await item.Illust.Like(pub);
                result = ret.Item1;
                item.Illust = ret.Item2;
                item.IsFavorited = result;
                if (item.Source == null)
                {
                    using (var thumb = await item.Thumb.LoadImageFromUrl(size: Application.Current.GetDefaultThumbSize()))
                    {
                        item.Source = thumb.Source;
                        item.State = TaskStatus.RanToCompletion;
                    }
                }
            }

            return (result);
        }

        public static void LikeIllust(this ObservableCollection<PixivItem> collection, bool pub = true)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            //var items = collection.Distinct();
            var items = collection.GroupBy(i => i.ID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item.IsWork())
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.LikeIllust(pub);
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }).InvokeAsync();
                }
            });
        }

        public static void LikeIllust(this IList<PixivItem> collection, bool pub = true)
        {
            LikeIllust(new ObservableCollection<PixivItem>(collection), pub);
        }

        /// <summary>
        /// Unlike Illust Work
        /// </summary>
        /// <param name="illust"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, Pixeez.Objects.Work>> UnLikeIllust(this Pixeez.Objects.Work illust)
        {
            Tuple<bool, Pixeez.Objects.Work> result = new Tuple<bool, Pixeez.Objects.Work>(false, illust);

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            try
            {
                var works = await tokens.DeleteMyFavoriteWorksAsync((long)illust.Id);
                if (works is Pixeez.Objects.Paginated<Pixeez.Objects.UsersFavoriteWork>)
                {
                    foreach (var ufw in works)
                    {
                        var id = ufw.Id;
                        if (id.Value == illust.Id.Value)
                        {
                            var work = ufw.Work;

                            break;
                        }
                    }
                }
                //ret = await tokens.DeleteMyFavoriteWorksAsync((long)illust.Id, "private");
            }
            catch (Exception ex) { ex.ERROR("DeleteMyFavoriteWorksAsync"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            finally
            {
                try
                {
                    illust = await illust.RefreshIllust();
                    if (illust != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.Work>(illust.IsLiked(), illust);
                        var info = "Unliked";
                        var title = result.Item1 ? "Failed" : "Succeed";
                        var fail = result.Item1 ?  "isn't" : "is";
                        $"Illust \"{illust.Title}\" {fail} {info}!".ShowToast(title, illust.GetThumbnailUrl(), title);
                    }
                }
                catch (Exception ex) { ex.ERROR("RefreshIllust"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            }

            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.Work>> UnLike(this Pixeez.Objects.Work illust)
        {
            var result = await illust.UnLikeIllust();
            UpdateLikeStateAsync((int)(illust.Id.Value), false);
            return (result);
        }

        public static async Task<bool> UnLikeIllust(this PixivItem item, bool pub = true)
        {
            bool result = false;
            if (item.IsWork())
            {
                var ret = await item.Illust.UnLike();
                result = ret.Item1;
                item.Illust = ret.Item2;
                item.IsFavorited = result;
                if (item.Source == null)
                {
                    using (var thumb = await item.Thumb.LoadImageFromUrl(size: Application.Current.GetDefaultThumbSize()))
                    {
                        item.Source = thumb.Source;
                        item.State = TaskStatus.RanToCompletion;
                    }
                }
            }
            return (result);
        }

        public static void UnLikeIllust(this ObservableCollection<PixivItem> collection)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            var items = collection.GroupBy(i => i.ID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item.IsWork())
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.UnLikeIllust();
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }).InvokeAsync();
                }
            });
        }

        public static void UnLikeIllust(this IList<PixivItem> collection)
        {
            UnLikeIllust(new ObservableCollection<PixivItem>(collection));
        }

        /// <summary>
        /// Toggle Illust Work Like State
        /// </summary>
        /// <param name="user"></param>
        /// <param name="pub"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, Pixeez.Objects.Work>> ToggleLikeIllust(this Pixeez.Objects.Work illust, bool pub = true)
        {
            var result = illust.IsLiked() ? await illust.UnLikeIllust() : await illust.LikeIllust(pub);
            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.Work>> ToggleLike(this Pixeez.Objects.Work illust, bool pub = true)
        {
            var result = await illust.ToggleLikeIllust(pub);
            UpdateLikeStateAsync((int)(illust.Id.Value), false);
            return (result);
        }

        public static async Task<bool> ToggleLikeIllust(this PixivItem item, bool pub = true)
        {
            bool result = false;
            if (item.IsWork())
            {
                var ret = await item.Illust.ToggleLike(pub);
                result = ret.Item1;
                item.Illust = ret.Item2;
                item.IsFavorited = result;
                if (item.Source == null) item.State = TaskStatus.RanToCompletion;
            }
            return (result);
        }

        public static void ToggleLikeIllust(this ObservableCollection<PixivItem> collection, bool pub = true)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            //var items = collection.Distinct();
            var items = collection.GroupBy(i => i.ID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item.IsWork())
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.ToggleLikeIllust(pub);
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }).InvokeAsync();
                }
            });
        }

        public static void ToggleLikeIllust(this IList<PixivItem> collection, bool pub = true)
        {
            ToggleLikeIllust(new ObservableCollection<PixivItem>(collection), pub);
        }
        #endregion

        #region Like/Unlike User helper routines
        /// <summary>
        /// Like user
        /// </summary>
        /// <param name="user"></param>
        /// <param name="pub"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> LikeUser(this Pixeez.Objects.UserBase user, bool pub = true)
        {
            Tuple<bool, Pixeez.Objects.UserBase> result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            try
            {
                var mode = pub ? "public" : "private";
                var ret = await tokens.AddFavouriteUser((long)user.Id, mode);
                if (!ret) return (result);
            }
            catch (Exception ex) { ex.ERROR("AddFavouriteUser"); }
            finally
            {
                try
                {
                    user = await user.RefreshUser();
                    if (user != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);
                        var info = "Liked";
                        var title = result.Item1 ? "Succeed" : "Failed";
                        var fail = result.Item1 ?  "is" : "isn't";
                        var pub_like = pub ? "Public" : "Private";
                        $"User \"{user.Name ?? string.Empty}\" {fail} {pub_like} {info}!".ShowToast(title, user.GetAvatarUrl(), title, pub_like);
                    }
                }
                catch (Exception ex) { ex.ERROR("RefreshUser"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            }
            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> Like(this Pixeez.Objects.UserBase user, bool pub = true)
        {
            var result = await user.LikeUser(pub);
            UpdateLikeStateAsync((int)(user.Id.Value), true);
            return (result);
        }

        public static async Task<bool> LikeUser(this PixivItem item, bool pub = true)
        {
            bool result = false;

            if (item.HasUser())
            {
                try
                {
                    var user = item.User;
                    var ret = await user.Like(pub);
                    result = ret.Item1;
                    item.User = ret.Item2;
                    if (item.IsUser())
                    {
                        item.IsFavorited = result;
                    }
                    if (item.Source == null)
                    {
                        using (var thumb = await item.Thumb.LoadImageFromUrl(size: Application.Current.GetDefaultThumbSize()))
                        {
                            item.Source = thumb.Source;
                            item.State = TaskStatus.RanToCompletion;
                        }
                    }
                }
                catch (Exception ex) { ex.ERROR("LIKEUSER"); }
            }

            return (result);
        }

        public static void LikeUser(this ObservableCollection<PixivItem> collection, bool pub = true)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            //var items = collection.Distinct();
            var items = collection.GroupBy(i => i.UserID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item.HasUser())
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.LikeUser(pub);
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }).InvokeAsync();
                }
            });
        }

        public static void LikeUser(this IList<PixivItem> collection, bool pub = true)
        {
            LikeUser(new ObservableCollection<PixivItem>(collection), pub);
        }

        /// <summary>
        /// Unlike user 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="pub"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> UnLikeUser(this Pixeez.Objects.UserBase user)
        {
            Tuple<bool, Pixeez.Objects.UserBase> result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            try
            {
                var ret_public = await tokens.DeleteFavouriteUser(user.Id.ToString());
                if (!ret_public) return (result);
                //var ret_private = await tokens.DeleteFavouriteUser(user.Id.ToString(), "private");
            }
            catch (Exception ex) { ex.ERROR("DeleteFavouriteUser"); }
            finally
            {
                try
                {
                    user = await user.RefreshUser();
                    if (user != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);
                        var info = "Unliked";
                        var title = result.Item1 ? "Failed" : "Succeed";
                        var fail = result.Item1 ?  "isn't" : "is";
                        $"User \"{user.Name ?? string.Empty}\" {fail} {info}!".ShowToast(title, user.GetAvatarUrl(), title);
                    }
                }
                catch (Exception ex) { ex.ERROR("RefreshUser"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            }
            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> UnLike(this Pixeez.Objects.UserBase user)
        {
            var result = await user.UnLikeUser();
            UpdateLikeStateAsync((int)(user.Id.Value), true);
            return (result);
        }

        public static async Task<bool> UnLikeUser(this PixivItem item, bool pub = true)
        {
            bool result = false;

            if (item.HasUser())
            {
                try
                {
                    var user = item.User;
                    var ret = await user.UnLike();
                    result = ret.Item1;
                    item.User = ret.Item2;
                    if (item.IsUser())
                    {
                        item.IsFavorited = result;
                    }
                    if (item.Source == null)
                    {
                        using (var thumb = await item.Thumb.LoadImageFromUrl(size: Application.Current.GetDefaultThumbSize()))
                        {
                            item.Source = thumb.Source;
                            item.State = TaskStatus.RanToCompletion;
                        }
                    }
                }
                catch (Exception ex) { ex.ERROR("UNLIKEUSER"); }
            }

            return (result);
        }

        public static void UnLikeUser(this ObservableCollection<PixivItem> collection)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            var items = collection.GroupBy(i => i.UserID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item.HasUser())
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.UnLikeUser();
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }).InvokeAsync();
                }
            });
        }

        public static void UnLikeUser(this IList<PixivItem> collection)
        {
            UnLikeUser(new ObservableCollection<PixivItem>(collection));
        }

        /// <summary>
        /// Toggle User Like State
        /// </summary>
        /// <param name="user"></param>
        /// <param name="pub"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> ToggleLikeUser(this Pixeez.Objects.UserBase user, bool pub = true)
        {
            var result = user.IsLiked() ? await user.UnLikeUser() : await user.LikeUser(pub);
            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> ToggleLike(this Pixeez.Objects.UserBase user, bool pub = true)
        {
            var result = await user.ToggleLikeUser(pub);
            UpdateLikeStateAsync((int)(user.Id.Value), true);
            return (result);
        }

        public static async Task<bool> ToggleLikeUser(this PixivItem item, bool pub = true)
        {
            bool result = false;

            if (item.HasUser())
            {
                try
                {
                    var user = item.User;
                    var ret =  await user.ToggleLike(pub);
                    result = ret.Item1;
                    item.User = ret.Item2;
                    if (item.IsUser())
                    {
                        item.IsFavorited = result;
                    }
                    if (item.Source == null) item.State = TaskStatus.RanToCompletion;
                }
                catch (Exception ex) { ex.ERROR(); }
            }

            return (result);
        }

        public static void ToggleLikeUser(this ObservableCollection<PixivItem> collection, bool pub = true)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            //var items = collection.Distinct();
            var items = collection.GroupBy(i => i.UserID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item.HasUser())
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.ToggleLikeUser(pub);
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }).InvokeAsync();
                }
            });
        }

        public static void ToggleLikeUser(this IList<PixivItem> collection, bool pub = true)
        {
            ToggleLikeUser(new ObservableCollection<PixivItem>(collection), pub);
        }
        #endregion

        #region Update/Find Illust/User info cache
        public static void Cache(this Pixeez.Objects.UserBase user)
        {
            if (user is Pixeez.Objects.UserBase)
                UserCache[user.Id] = user;
        }

        public static void Cache(this Pixeez.Objects.Work illust)
        {
            if (illust is Pixeez.Objects.Work)
            {
                if (IllustCache.ContainsKey(illust.Id))
                {
                    var illust_old = IllustCache[illust.Id];
                    if (illust.ImageUrls != null && illust_old.ImageUrls != null)
                    {
                        if (illust.ImageUrls.Px128x128 == null) illust.ImageUrls.Px128x128 = illust_old.ImageUrls.Px128x128;
                        if (illust.ImageUrls.Small == null) illust.ImageUrls.Small = illust_old.ImageUrls.Small;
                        if (illust.ImageUrls.Medium == null) illust.ImageUrls.Medium = illust_old.ImageUrls.Medium;
                        if (illust.ImageUrls.Large == null) illust.ImageUrls.Large = illust_old.ImageUrls.Large;
                        if (illust.ImageUrls.Px480mw == null) illust.ImageUrls.Px480mw = illust_old.ImageUrls.Px480mw;
                        if (illust.ImageUrls.SquareMedium == null) illust.ImageUrls.SquareMedium = illust_old.ImageUrls.SquareMedium;
                        if (illust.ImageUrls.Original == null)
                        {
                            illust.ImageUrls.Original = string.IsNullOrEmpty(illust.ImageUrls.Large) ? illust_old.ImageUrls.Original : illust.ImageUrls.Large;
                            if (illust.ImageUrls.Original.Equals(illust.ImageUrls.Large) && !string.IsNullOrEmpty(illust_old.ImageUrls.Large))
                                illust.ImageUrls.Large = illust_old.ImageUrls.Large;
                        }
                    }
                }
                IllustCache[illust.Id] = illust;
            }
        }

        public static void Cache(this Pixeez.Objects.UserInfo userinfo)
        {
            if (userinfo is Pixeez.Objects.UserInfo)
                UserInfoCache[userinfo.user.Id] = userinfo;
        }

        public static Pixeez.Objects.Work FindIllust(this long id)
        {
            if (IllustCache.ContainsKey(id)) return (IllustCache[id]);
            else return (null);
        }

        public static Pixeez.Objects.Work FindIllust(this long? id)
        {
            if (id != null && IllustCache.ContainsKey(id)) return (IllustCache[id]);
            else return (null);
        }

        public static Pixeez.Objects.Work FindIllust(this string id)
        {
            long idv = 0;
            if (long.TryParse(id, out idv)) return (FindIllust(idv));
            else return (null);
        }

        public static Pixeez.Objects.Work FindIllust(this Pixeez.Objects.Work work)
        {
            return (FindIllust(work));
        }

        public static Pixeez.Objects.UserBase FindUser(this long id)
        {
            if (UserCache.ContainsKey(id)) return (UserCache[id]);
            else return (null);
        }

        public static Pixeez.Objects.UserBase FindUser(this long? id)
        {
            if (id != null && UserCache.ContainsKey(id)) return (UserCache[id]);
            else return (null);
        }

        public static Pixeez.Objects.UserBase FindUser(this string id)
        {
            long idv = 0;
            if (long.TryParse(id, out idv)) return (FindUser(idv));
            else return (null);
        }

        public static Pixeez.Objects.UserBase FindUser(this Pixeez.Objects.UserBase user)
        {
            return (FindUser(user.Id));
        }

        public static Pixeez.Objects.UserInfo FindUserInfo(this long id)
        {
            if (UserInfoCache.ContainsKey(id)) return (UserInfoCache[id]);
            else return (null);
        }

        public static Pixeez.Objects.UserInfo FindUserInfo(this long? id)
        {
            if (id != null && UserInfoCache.ContainsKey(id)) return (UserInfoCache[id]);
            else return (null);
        }

        public static Pixeez.Objects.UserInfo FindUserInfo(this string id)
        {
            long idv = 0;
            if (long.TryParse(id, out idv)) return (FindUserInfo(idv));
            else return (null);
        }

        public static Pixeez.Objects.UserInfo FindUserInfo(this Pixeez.Objects.UserBase user)
        {
            return (FindUserInfo(user.Id));
        }
        #endregion

        #region Sync Illust/User Like State
        public static void UpdateLikeStateAsync(string illustid = default(string), bool is_user = false)
        {
            int id = -1;
            int.TryParse(illustid, out id);
            UpdateLikeStateAsync(id);
        }

        public static async void UpdateLikeStateAsync(int illustid = -1, bool is_user = false)
        {
            await new Action(() =>
            {
                foreach (var win in Application.Current.Windows)
                {
                    if (win is MainWindow)
                    {
                        var mw = win as MainWindow;
                        mw.UpdateLikeState(illustid, is_user);
                    }
                    else if (win is ContentWindow)
                    {
                        var w = win as ContentWindow;
                        if (w.Content is IllustDetailPage)
                            (w.Content as IllustDetailPage).UpdateLikeStateAsync(illustid, is_user);
                        else if (w.Content is IllustImageViewerPage)
                            (w.Content as IllustImageViewerPage).UpdateLikeStateAsync(illustid, is_user);
                        else if (w.Content is SearchResultPage)
                            (w.Content as SearchResultPage).UpdateLikeStateAsync(illustid, is_user);
                        else if (w.Content is DownloadManagerPage)
                            (w.Content as DownloadManagerPage).UpdateLikeStateAsync(illustid, is_user);
                        else if (w.Content is HistoryPage)
                            (w.Content as HistoryPage).UpdateLikeStateAsync(illustid, is_user);
                    }
                }
            }).InvokeAsync();
        }

        public static void UpdateLikeState(this ImageListGrid list, int illustid = -1, bool is_user = false)
        {
            list.Items.UpdateLikeState(illustid, is_user);
        }

        public static void UpdateLikeState(this ObservableCollection<PixivItem> collection, int illustid = -1, bool is_user = false)
        {
            foreach (PixivItem item in collection)
            {
                int item_id = -1;
                int.TryParse(item.ID, out item_id);
                int user_id = -1;
                int.TryParse(item.UserID, out user_id);

                try
                {
                    if (is_user) item_id = user_id;
                    if (illustid == -1 || illustid == item_id)
                    {
                        if (item.IsUser())
                        {
                            item.IsFavorited = false;
                            item.IsFollowed = item.User.IsLiked();
                        }
                        else if (item.IsPage() || item.IsPages())
                        {
                            item.IsFavorited = false;
                            item.IsFollowed = false;
                        }
                        else if (item.IsWork())
                        {
                            item.IsFavorited = item.Illust.IsLiked();
                            item.IsFollowed = item.User.IsLiked();
                        }
                        else
                        {
                            item.IsFavorited = item.IsFollowed = false;
                        }
                    }
                    if (item.Source == null)
                    {
                        new Action(async () =>
                        {
                            using (var thumb = await item.Thumb.LoadImageFromUrl(size: Application.Current.GetDefaultThumbSize()))
                            {
                                if (thumb.Source != null)
                                {
                                    item.Source = thumb.Source;
                                    item.State = TaskStatus.RanToCompletion;
                                }
                            }
                        }).Invoke(async: true);
                    }
                }
                catch (Exception ex) { ex.ERROR("UpdateLikeState"); }
            }
        }
        #endregion

        #endregion

        #region UI Element Relative
        public static string GetUid(this object obj)
        {
            string result = string.Empty;

            if (obj is UIElement)
            {
                result = (obj as UIElement).Uid;
            }

            return (result);
        }

        public static ImageSource CreateThemedImage(this Uri uri)
        {
            ImageSource result = new BitmapImage(uri);
            try
            {
                new Action(() =>
                {
                    var dpi = new DPI();

                    var src = Application.Current.GetDefalutIcon();
                    src.Opacity = 0.8;
                    src.Effect = new ThresholdEffect() { Threshold = 0.67, BlankColor = Theme.WindowTitleColor };
                    //img.Effect = new TranspranceEffect() { TransColor = Theme.WindowTitleColor };
                    //img.Effect = new TransparenceEffect() { TransColor = Color.FromRgb(0x00, 0x96, 0xfa) };
                    //img.Effect = new ReplaceColorEffect() { Threshold = 0.5, SourceColor = Color.FromArgb(0xff, 0x00, 0x96, 0xfa), TargetColor = Theme.MahApps.Colors.Accent };
                    //img.Effect = new ReplaceColorEffect() { Threshold = 0.5, SourceColor = Color.FromRgb(0x00, 0x96, 0xfa), TargetColor = Colors.Transparent };
                    //img.Effect = new ReplaceColorEffect() { Threshold = 0.5, SourceColor = Color.FromRgb(0x00, 0x96, 0xfa), TargetColor = Theme.WindowTitleColor };
                    //img.Effect = new ExcludeReplaceColorEffect() { Threshold = 0.05, ExcludeColor = Colors.White, TargetColor = Theme.WindowTitleColor };
                    int width = (int)src.Source.Width;
                    int height = (int)src.Source.Height;

                    Grid root = new Grid();
                    root.Background = Theme.WindowTitleBrush;
                    Arrange(root, width, height);
                    root.Children.Add(src);
                    Arrange(src, width, height);

                    RenderTargetBitmap bmp = new RenderTargetBitmap(width, height, dpi.X, dpi.Y, PixelFormats.Pbgra32);
                    DrawingVisual drawingVisual = new DrawingVisual();
                    using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                    {
                        VisualBrush visualBrush = new VisualBrush(root);
                        drawingContext.DrawRectangle(visualBrush, null, new Rect(new Point(), new Size(width, height)));
                    }
                    bmp.Render(drawingVisual);
                    result = bmp;

                    root.Children.Clear();
                    root.UpdateLayout();
                    root = null;
                }).Invoke(async: false);
            }
            catch (Exception ex) { ex.ERROR("CreateThemedImage"); }
            return (result);
        }

        public static void UpdateTheme(this Window win, ImageSource icon = null)
        {
            try
            {
                new Action(() =>
                {
                    win.Icon = icon == null ? Application.Current.GetIcon().Source : icon;

                    if (win is MainWindow)
                    {
                        (win as MainWindow).UpdateTheme();
                    }
                    else if (win is ContentWindow)
                    {
                        if (win.Content is IllustDetailPage)
                        {
                            var page = win.Content as IllustDetailPage;
                            page.UpdateTheme();
                        }
                        else if (win.Content is IllustImageViewerPage)
                        {
                            var page = win.Content as IllustImageViewerPage;
                            page.UpdateTheme();
                        }
                        else if (win.Content is DownloadManagerPage)
                        {
                            var page = win.Content as DownloadManagerPage;
                            page.UpdateTheme();
                        }
                        else if (win.Title.Equals("DropBox", StringComparison.CurrentCultureIgnoreCase))
                        {
                            win.Background = Theme.AccentBrush;
                            win.Content = icon;
                        }
                    }
                }).Invoke(async: false);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void UpdateTheme()
        {
            try
            {
                new Action(() =>
                {
                    var img = Application.Current.GetIcon();
                    foreach (Window win in Application.Current.Windows)
                    {
                        if (win is MetroWindow) win.UpdateTheme(img.Source);
                    }
                }).Invoke(async: false);
            }
            catch (Exception ex) { ex.ERROR("UpdateTheme"); }
        }

        private static void Arrange(UIElement element, int width, int height)
        {
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();
        }

        public static bool IsShown(this UIElement element)
        {
            return (element.Visibility == Visibility.Visible ? true : false);
        }

        public static bool IsHidden(this UIElement element)
        {
            return (element.Visibility != Visibility.Visible ? true : false);
        }

        public static void Show(this ProgressRing progress, bool show, bool active = true)
        {
            if (progress is ProgressRing)
            {
                if (show)
                {
                    progress.Visibility = Visibility.Visible;
                    progress.IsEnabled = true;
                    progress.IsActive = active;
                }
                else
                {
                    progress.Visibility = Visibility.Collapsed;
                    progress.IsEnabled = false;
                    progress.IsActive = false;
                }
            }
        }

        public static void Pause(this ProgressRing progress)
        {
            progress.IsActive = false;
        }

        public static void Resume(this ProgressRing progress)
        {
            progress.IsEnabled = true;
            progress.IsActive = true;
        }

        public static void Disable(this ProgressRing progress)
        {
            progress.IsEnabled = false;
            progress.IsActive = false;
        }

        public static void Show(this ProgressRing progress, bool active = true)
        {
            progress.Show(true, active);
        }

        public static void Hide(this ProgressRing progress)
        {
            progress.Show(false, false);
        }

        public static void Show(this UIElement element, bool show, bool parent = false)
        {
            if (element is UIElement)
            {
                if (show)
                    element.Visibility = Visibility.Visible;
                else
                    element.Visibility = Visibility.Collapsed;

                if (parent && element.GetParentObject() is UIElement)
                    (element.GetParentObject() as UIElement).Visibility = element.Visibility;
            }
        }

        public static void Show(this UIElement element, bool parent = false)
        {
            if (element is UIElement) (element as UIElement).Show(true, parent);
        }

        public static void Show(this object element, bool parent = false)
        {
            if (element is UIElement) element.Show(parent);
        }

        public static void Hide(this UIElement element, bool parent = false)
        {
            if (element is UIElement) element.Show(false, parent);
        }

        public static void Hide(this object element, bool parent = false)
        {
            if (element is UIElement) (element as UIElement).Hide(parent);
        }

        public static void Enable(this Control element, bool state, bool show = true)
        {
            if (element is Control)
            {
                element.IsEnabled = state;
                element.Foreground = state ? Theme.AccentBrush : Theme.GrayBrush;
                if (show)
                    element.Visibility = Visibility.Visible;
                else
                    element.Visibility = Visibility.Collapsed;
            }
        }

        public static void Enable(this Control element)
        {
            if (element is Control)
            {
                element.IsEnabled = true;
                element.Foreground = Theme.AccentBrush;
                element.Visibility = Visibility.Visible;
            }
        }

        public static void Disable(this Control element, bool state, bool show = true)
        {
            if (element is Control)
            {
                element.IsEnabled = !state;
                element.Foreground = state ? Theme.GrayBrush : Theme.AccentBrush;
                if (show)
                    element.Visibility = Visibility.Visible;
                else
                    element.Visibility = Visibility.Collapsed;
            }
        }

        public static void Disable(this Control element)
        {
            if (element is Control)
            {
                element.IsEnabled = false;
                element.Foreground = Theme.GrayBrush;
                element.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region Button MouseOver Action
        public static void MouseOverAction(this ButtonBase button)
        {
            if (button is ButtonBase)
            {
                try
                {
                    //button.IsMouseOver
                    button.BorderBrush = Theme.AccentBrush;
                    button.MouseEnter += ToolButton_MouseEnter;
                    button.MouseLeave += ToolButton_MouseLeave;

                    if (button is ToggleButton) MouseLeave(button);
                }
                catch (Exception ex) { ex.ERROR(); }
            }
        }

        public static void MouseEnter(this ButtonBase button)
        {
            try
            {
                if ((button.Parent is StackPanel) && (button.Parent as StackPanel).Name.Equals("ActionBar") && button.ActualWidth >= 32)
                    button.Foreground = Theme.IdealForegroundBrush;

                if ((button.Parent is Grid) && (button.Parent as Grid).Name.Equals("PopupContainer") && button.ActualWidth >= 24)
                    button.Foreground = Theme.IdealForegroundBrush;

                if (!(button is ToggleButton) || (button is ToggleButton && !(button as ToggleButton).IsChecked.Value))
                    button.Background = Theme.SemiTransparentBrush;
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void MouseLeave(this ButtonBase button)
        {
            try
            {
                if ((button.Parent is StackPanel) && (button.Parent as StackPanel).Name.Equals("ActionBar") && button.ActualWidth >= 32 && button.IsEnabled)
                    button.Foreground = Theme.AccentBrush;

                if ((button.Parent is Grid) && (button.Parent as Grid).Name.Equals("PopupContainer") && button.ActualWidth >= 24)
                    button.Foreground = Theme.AccentBrush;

                if (!(button is ToggleButton) || (button is ToggleButton && !(button as ToggleButton).IsChecked.Value))
                    button.Background = Theme.TransparentBrush;
                else if (button is ToggleButton && (button as ToggleButton).IsChecked.Value)
                {
                    var bg = new SolidColorBrush(Theme.SemiTransparentColor);
                    bg.Opacity = 0.4;
                    button.Background = bg;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void ToolButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is ButtonBase) MouseEnter(sender as ButtonBase);
        }

        public static void ToolButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is ButtonBase) MouseLeave(sender as ButtonBase);
        }
        #endregion

        #region SearchBox common routines
        private static ObservableCollection<string> auto_suggest_list = new ObservableCollection<string>() {};
        public static ObservableCollection<string> AutoSuggestList
        {
            get { return (auto_suggest_list); }
        }

        public static IEnumerable<string> GetSuggestList(this string text)
        {
            List<string> result = new List<string>();

            if (!string.IsNullOrEmpty(text))
            {
                if (Regex.IsMatch(text, @"^\d+$", RegexOptions.IgnoreCase))
                {
                    result.Add($"IllustID: {text}");
                    result.Add($"UserID: {text}");
                }
                result.Add($"User: {text}");
                result.Add($"Fuzzy: {text}");
                result.Add($"Tag: {text}");
                result.Add($"Fuzzy Tag: {text}");
                //result.Add($"Caption: {text}");
            }

            return (result);
        }

        public static void SearchBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox)
            {
                var SearchBox = sender as ComboBox;
                if (SearchBox.Text.Length > 0)
                {
                    auto_suggest_list.Clear();

                    var content = SearchBox.Text.ParseLink().ParseID();
                    if (!string.IsNullOrEmpty(content))
                    {
                        content.GetSuggestList().ToList().ForEach(t => auto_suggest_list.Add(t));
                        SearchBox.Items.Refresh();
                        SearchBox.IsDropDownOpen = true;
                    }

                    e.Handled = true;
                }
            }
        }

        public static void SearchBox_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is ComboBox)
            {
                var SearchBox = sender as ComboBox;

                var textBox = Keyboard.FocusedElement as TextBox;
                if (textBox != null && textBox.Text.Length == 1 && textBox.SelectionLength == 1)
                {
                    textBox.SelectionLength = 0;
                    textBox.SelectionStart = 1;
                }
            }
        }

        public static void SearchBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox)
            {
                var SearchBox = sender as ComboBox;

                e.Handled = true;
                var items = e.AddedItems;
                if (items.Count > 0)
                {
                    var item = items[0];
                    if (item is string)
                    {
                        var query = (string)item;
                        Commands.OpenSearch.Execute(query);
                    }
                }
            }
        }

        public static void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is ComboBox)
            {
                var SearchBox = sender as ComboBox;

                if (e.Key == Key.Return)
                {
                    e.Handled = true;
                    Commands.OpenSearch.Execute(SearchBox.Text);
                }
            }
        }
        #endregion

        #region Window routines
        public static MetroWindow GetMainWindow()
        {
            return (Application.Current.MainWindow as MetroWindow);
        }

        public static MetroWindow GetMainWindow(this MetroWindow win)
        {
            return (Application.Current.MainWindow as MetroWindow);
        }

        public static MainWindow GetMainWindow(this Page page)
        {
            MainWindow result = null;
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    result = Application.Current.MainWindow as MainWindow;
                });
            }
            catch (Exception ex) { ex.ERROR("GETMAINWINDOW"); }
            return (result);
        }

        public static MetroWindow GetActiveWindow()
        {
            MetroWindow window = Application.Current.Windows.OfType<MetroWindow>().SingleOrDefault(x => x.IsActive || x.IsFocused);
            if (window == null) window = Application.Current.MainWindow as MetroWindow;
            return (window);
        }

        public static MetroWindow GetPrevWindow(this MetroWindow window)
        {
            return (window.GetWindow(-1));
        }

        public static MetroWindow GetNextWindow(this MetroWindow window)
        {
            return (window.GetWindow(1));
        }

        public static IList<MetroWindow> GetWindows<T>()
        {
            List<MetroWindow> result = new List<MetroWindow>();
            new Action(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is T && win is MetroWindow) result.Add(win as MetroWindow);
                }
            }).Invoke(async: false);
            return (result);
        }

        public static MetroWindow GetWindow<T>()
        {
            return (GetWindows<T>().FirstOrDefault());
        }

        public static MetroWindow GetWindow(this MetroWindow window, int index = 0, bool relative = true)
        {
            var wins = Application.Current.Windows.OfType<MetroWindow>().Where(w => !w.Title.Equals("DropBox", StringComparison.CurrentCultureIgnoreCase)).ToList();
            var active = window is MetroWindow ? window : wins.SingleOrDefault(x => x.IsActive);
            if (active == null) active = Application.Current.MainWindow as MetroWindow;

            var result = active;
            var current_index = wins.IndexOf(active);

            var next = relative ? current_index + index : index;
            if (next > 0)
            {
                if (next >= wins.Count) next = next % wins.Count;
                result = wins.ElementAtOrDefault(next);
            }
            else if (next < 0)
            {
                if (next < 0) next = wins.Count - (Math.Abs(next) % wins.Count);
                result = wins.ElementAtOrDefault(next);
            }
            else
            {
            }

            return (result);
        }

        public static IList<MetroWindow> GetWindows(this Page page)
        {
            IList<MetroWindow> result = new List<MetroWindow>();
            foreach (var win in Application.Current.Windows)
            {
                if (win is MetroWindow)
                {
                    if ((win as MetroWindow).Content == page)
                    {
                        result.Add(win as MetroWindow);
                    }
                }
            }
            return (result);
        }

        public static MetroWindow GetWindow(this Page page)
        {
            return (GetWindows(page).FirstOrDefault());
        }

        public static IList<MetroWindow> GetWindows<T>(this Page page)
        {
            IList<MetroWindow> result = new List<MetroWindow>();
            if (!(page.Parent is MetroWindow))
            {
                //Window.GetWindow(page);
                var win = page.TryFindParent<MetroWindow>();
                if (win is MetroWindow) result.Add(win);
            }
            else
            {
                foreach (var win in Application.Current.Windows)
                {
                    if (win is T && win is MetroWindow)
                    {
                        if ((win as MetroWindow).Content == page)
                        {
                            result.Add(win as MetroWindow);
                        }
                    }
                }
            }
            return (result);
        }

        public static MetroWindow GetWindow<T>(this Page page)
        {
            return (GetWindows<T>(page).FirstOrDefault());
        }

        public static MetroWindow GetWindowByTitle(this string title)
        {
            return (GetWindowsByTitle(title).FirstOrDefault());
        }

        public static IList<MetroWindow> GetWindowsByTitle(this string title)
        {
            List<MetroWindow> result = new List<MetroWindow>();
            new Action(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MetroWindow)
                    {
                        var win_title = (win as MetroWindow).Title;
                        if (win_title.Equals(title, StringComparison.CurrentCultureIgnoreCase))
                        {
                            result.Add(win as MetroWindow);
                        }
                    }
                }
            }).Invoke(async: false);
            return (result);
        }

        public static void AdjustWindowPos(this MetroWindow window)
        {
            if (window is ContentWindow)
            {
                //var dw = System.Windows.SystemParameters.MaximizedPrimaryScreenWidth;
                //var dh = System.Windows.SystemParameters.MaximizedPrimaryScreenHeight;
                //var dw = System.Windows.SystemParameters.WorkArea.Width;
                //var dh = System.Windows.SystemParameters.WorkArea.Height;

                var rect = System.Windows.Forms.Screen.GetWorkingArea(new System.Drawing.Point((int)window.Top, (int)window.Left));
                var dw = rect.Width;
                var dh = rect.Height;

                //window.MaxWidth = Math.Min(window.MaxWidth, dw + 16);
                //window.MaxHeight = Math.Min(window.MaxHeight, dh + 16);

                if (window.Left + window.Width > dw) window.Left = window.Left + window.Width - dw;
                if (window.Top + window.Height > dh) window.Top = window.Top + window.Height - dh;
            }
        }

        public static void AdjustWindowPos(this Window window)
        {
            if (window is ContentWindow)
            {
                AdjustWindowPos(window as ContentWindow);
            }
        }

        public static void Active(this MetroWindow window)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                try
                {
                    if (window is MainWindow)
                        (window as MainWindow).RestoreWindowState();
                    else if (window is ContentWindow)
                        (window as ContentWindow).RestoreWindowState();
                }
                catch (Exception ex)
                {
                    ex.ERROR();
                    window.WindowState = WindowState.Normal;
                }
            }
            window.Show();
            window.Activate();
        }

        public static async Task<bool> ActiveByTitle(this string title)
        {
            bool result = false;
            await new Action(() =>
            {
                var win  = GetWindowByTitle(title);
                if (win is MetroWindow) { result = true; win.Active(); }
                else if (win is Window) { result = true; win.Activate(); }
            }).InvokeAsync();
            return (result);
        }

        public static async Task<bool> ShowByTitle(this string title)
        {
            bool result = false;
            await new Action(() =>
            {
                var win  = GetWindowByTitle(title);
                if (win is Window) { result = true; win.Show(); }
            }).InvokeAsync();
            return (result);
        }

        public static Window GetActiveWindow(this Page page)
        {
            var window = Window.GetWindow(page);
            if (window == null) window = GetActiveWindow();
            return (window);
        }

        public static T GetActiveWindow<T>(this Page page) where T : Window
        {
            var window = Window.GetWindow(page);
            if (window == null) window = GetActiveWindow();
            if (window is T)
                return (window as T);
            else
                return (default(T));
        }

        public static dynamic GetWindowContent(this MetroWindow window)
        {
            dynamic result = null;
            try
            {
                if (window is MainWindow)
                {
                    if (window.Content is TilesPage)
                        result = window.Content;
                }
                else if (window is ContentWindow)
                {
                    if (window.Content is Page)
                        result = window.Content;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static bool WindowExists(string title)
        {
            bool result = false;

            //HWND hDlgExists = FindWindow(0, "MyDialogTitle"); // hDlgExists will be NULL if dlg is not exist.

            return (result);
        }
        #endregion

        #region Dialog/MessageBox routines
        public static string ChangeSaveTarget(this string file)
        {
            return (ChangeSaveFolder(file));
        }

        public static string ChangeSaveFolder(string file = "")
        {
            var result = string.Empty;
            setting = Application.Current.LoadSetting();
            if (string.IsNullOrEmpty(file))
            {
                CommonOpenFileDialog dlg = new CommonOpenFileDialog()
                {
                    Title = "Select Folder",
                    IsFolderPicker = true,
                    InitialDirectory = setting.LastFolder,

                    AddToMostRecentlyUsedList = false,
                    AllowNonFileSystemItems = false,
                    DefaultDirectory = setting.LastFolder,
                    EnsureFileExists = true,
                    EnsurePathExists = true,
                    EnsureReadOnly = false,
                    EnsureValidNames = true,
                    Multiselect = false,
                    ShowPlacesList = true
                };

                Window dm = GetWindowByTitle("Download Manager");
                if (!(dm is ContentWindow)) dm = Application.Current.MainWindow;
                if (dlg.ShowDialog(dm) == CommonFileDialogResult.Ok)
                {
                    result = dlg.FileName;
                    setting.LastFolder = dlg.FileName;
                    // Do something with selected folder string
                    if (!string.IsNullOrEmpty(setting.LastFolder)) setting.LastFolder.INFO("ChangeSaveFolder");
                }
            }
            else
            {
                if (string.IsNullOrEmpty(setting.LastFolder))
                {
                    SaveFileDialog dlgSave = new SaveFileDialog();
                    dlgSave.FileName = file;
                    if (dlgSave.ShowDialog() == true)
                    {
                        file = dlgSave.FileName;
                        setting.LastFolder = Path.GetDirectoryName(file);
                        if (!string.IsNullOrEmpty(setting.LastFolder)) setting.LastFolder.INFO("ChangeSaveFolder");
                    }
                }
                result = Path.Combine(setting.LastFolder, Path.GetFileName(file));
            }
            return (result);
        }

        private static ConcurrentDictionary<string, string> _MessageDialogList = new ConcurrentDictionary<string, string>();
        public static bool IsMessagePopup(this string title, string content = "")
        {
            var result = _MessageDialogList.ContainsKey(title) && _MessageDialogList[title].Equals(content);
            return (result);
        }

        private static TaskDialog MakeTaskDialog(string title, string content, MessageBoxImage image, TaskDialogStandardButtons buttons)
        {
            var dlg_icon = TaskDialogStandardIcon.Information;
            switch (image)
            {
                case MessageBoxImage.Error:
                    dlg_icon = TaskDialogStandardIcon.Error;
                    break;
                case MessageBoxImage.Information:
                    dlg_icon = TaskDialogStandardIcon.Information;
                    break;
                case MessageBoxImage.Warning:
                    dlg_icon = TaskDialogStandardIcon.Warning;
                    break;
                case MessageBoxImage.Question:
                    dlg_icon = TaskDialogStandardIcon.Shield;
                    break;
                default:
                    dlg_icon = TaskDialogStandardIcon.None;
                    break;
            }
            var dlg_btns = TaskDialogStandardButtons.Ok;
            var dlg = new TaskDialog()
            {
                FooterIcon = dlg_icon,
                Icon = dlg_icon,
                Cancelable = true,
                StandardButtons = dlg_btns,
                Text = content,
                DetailsExpandedText = content,
                InstructionText = title
            };
            return (dlg);
        }

        public static async void ShowExceptionMessageBox(this Exception ex, string title)
        {
            ex.LOG(title);
            await Task.Delay(1);
            _MessageDialogList[title] = ex.Message;
            var dialog = new TaskDialog()
            {
                Cancelable = true,
                StandardButtons = TaskDialogStandardButtons.Ok,
                Icon = TaskDialogStandardIcon.Error,
                FooterIcon = TaskDialogStandardIcon.Error,
                ExpansionMode = TaskDialogExpandedDetailsLocation.ExpandFooter,
                DetailsExpanded = false,
                DetailsExpandedText = ex.StackTrace,
                Text = ex.Message,
                FooterText = ex.Message,
            };
            var ret = dialog.Show();
            var value = string.Empty;
            _MessageDialogList.TryRemove(title, out value);
        }

        public static async Task<bool> ShowExceptionDialogBox(this Exception ex, string title)
        {
            var result = false;
            ex.LOG(title);
            await Task.Delay(1);
            _MessageDialogList[title] = ex.Message;
            var dialog = new TaskDialog()
            {
                Cancelable = true,
                StandardButtons = TaskDialogStandardButtons.Cancel | TaskDialogStandardButtons.Ok,
                Icon = TaskDialogStandardIcon.Error,
                FooterIcon = TaskDialogStandardIcon.Error,
                ExpansionMode = TaskDialogExpandedDetailsLocation.ExpandFooter,
                DetailsExpanded = false,
                DetailsExpandedText = ex.StackTrace,
                Text = ex.Message,
                FooterText = ex.Message,
            };
            var ret = dialog.Show();
            if (ret == TaskDialogResult.Ok || ret == TaskDialogResult.Yes || ret == TaskDialogResult.Close) result = true;
            var value = string.Empty;
            _MessageDialogList.TryRemove(title, out value);
            return (result);
        }

        public static async void ShowMessageBox(this string content, string title, MessageBoxImage image = MessageBoxImage.Information)
        {
            content.LOG(title);

            await Task.Delay(1);
            _MessageDialogList[title] = content;
            MessageBox.Show(content, title, MessageBoxButton.OK, image);
            var value = string.Empty;
            _MessageDialogList.TryRemove(title, out value);
        }

        public static async Task<bool> ShowMessageDialog(this string content, string title, MessageBoxImage image = MessageBoxImage.Information)
        {
            content.LOG(title);

            await Task.Delay(1);
            _MessageDialogList[title] = content;
            var ret = MessageBox.Show(content, title, MessageBoxButton.OKCancel, image);
            var value = string.Empty;
            _MessageDialogList.TryRemove(title, out value);
            return (ret == MessageBoxResult.OK || ret == MessageBoxResult.Yes ? true : false);
        }

        public static async Task ShowMessageBoxAsync(this string content, string title, MessageBoxImage image = MessageBoxImage.Information)
        {
            await ShowMessageDialogAsync(content, title, image);
        }

        public static async Task ShowMessageDialogAsync(this string content, string title, MessageBoxImage image = MessageBoxImage.Information)
        {
            MetroWindow window = GetActiveWindow();
            await window.ShowMessageAsync(content, title);
        }

        public static async void ShowProgressDialog(object sender, RoutedEventArgs e)
        {
            var mySettings = new MetroDialogSettings()
            {
                NegativeButtonText = "Close now",
                AnimateShow = false,
                AnimateHide = false
            };

            MetroWindow window = GetActiveWindow();

            var controller = await window.ShowProgressAsync("Please wait...", "We are baking some cupcakes!", settings: mySettings);
            controller.SetIndeterminate();

            //await Task.Delay(5000);

            controller.SetCancelable(true);

            double i = 0.0;
            while (i < 6.0)
            {
                double val = (i / 100.0) * 20.0;
                controller.SetProgress(val);
                controller.SetMessage("Baking cupcake: " + i + "...");

                if (controller.IsCanceled)
                    break; //canceled progressdialog auto closes.

                i += 1.0;

                //await Task.Delay(2000);
            }

            await controller.CloseAsync();

            if (controller.IsCanceled)
            {

                await window.ShowMessageAsync("No cupcakes!", "You stopped baking!");
            }
            else
            {
                await window.ShowMessageAsync("Cupcakes!", "Your cupcakes are finished! Enjoy!");
            }
        }
        #endregion

        #region Toast routines
        private static string lastToastTitle = string.Empty;
        private static string lastToastContent = string.Empty;
        public async static void ShowDownloadToast(this string content, string title = "Pixiv", string imgsrc = "", string file = "", string state = "", string state_description = "", object tag = null)
        {
            try
            {
                if (title.Equals(lastToastTitle) && content.Equals(lastToastContent)) return;

                lastToastTitle = title;
                lastToastContent = content;

                content.LOG(title);

                setting = Application.Current.LoadSetting();

                await new Action(() =>
                {
                    INotificationDialogService _dialogService = new NotificationDialogService();
                    NotificationConfiguration cfgDefault = NotificationConfiguration.DefaultConfiguration;
                    NotificationConfiguration cfg = new NotificationConfiguration(
                    //new TimeSpan(0, 0, 30), 
                    TimeSpan.FromSeconds(setting.ToastShowTimes),
                    cfgDefault.Width+32, cfgDefault.Height,
                    "ToastTemplate",
                    //cfgDefault.TemplateName, 
                    cfgDefault.NotificationFlowDirection);

                    var newNotification = new CustomToast()
                    {
                        Type = ToastType.DOWNLOAD,
                        Title = title,
                        ImgURL = imgsrc,
                        Message = content,
                        Extra = string.IsNullOrEmpty(file) ? string.Empty : file,
                        State = state,
                        StateDescription = state_description,
                        Tag = tag
                    };

                    _dialogService.ClearNotifications();
                    _dialogService.ShowNotificationWindow(newNotification, cfg);
                }).InvokeAsync(true);
            }
            catch (Exception ex) { ex.ERROR("ShowDownloadToast"); }
        }

        public async static void ShowToast(this string content, string title, string imgsrc, string state = "", string state_description = "", string tag = "")
        {
            try
            {
                Regex.Replace(content, @"(\r\n|\n\r|\r|\n|\s)+", " ", RegexOptions.IgnoreCase).LOG(title, tag);

                setting = Application.Current.LoadSetting();
                var main = Application.Current.GetMainWindow();
                if (main is MainWindow && main.IsShown())
                {
                    await new Action(() =>
                    {
                        INotificationDialogService _dialogService = new NotificationDialogService();
                        NotificationConfiguration cfgDefault = NotificationConfiguration.DefaultConfiguration;
                        NotificationConfiguration cfg = new NotificationConfiguration(
                            //new TimeSpan(0, 0, 30), 
                            TimeSpan.FromSeconds(setting.ToastShowTimes),
                            cfgDefault.Width + 32, cfgDefault.Height,
                            "ToastTemplate",
                            //cfgDefault.TemplateName, 
                            cfgDefault.NotificationFlowDirection
                        );

                        var newNotification = new CustomToast()
                        {
                            Type = ToastType.OK,
                            Title = title,
                            ImgURL = imgsrc,
                            Message = content,
                            State = state,
                            StateDescription = state_description,
                            Tag = null
                        };

                        _dialogService.ClearNotifications();
                        _dialogService.ShowNotificationWindow(newNotification, cfg);
                    }).InvokeAsync(true);
                }
            }
            catch (Exception ex) { ex.ERROR("ShowToast"); }
        }

        public async static void ShowToast(this string content, string title, bool messagebox = false, string tag = "")
        {
            try
            {
                if (messagebox) { content.ShowMessageBox(title); return; }

                Regex.Replace(content, @"(\r\n|\n\r|\r|\n|\s)+", " ", RegexOptions.IgnoreCase).LOG(title, tag);

                var main = Application.Current.GetMainWindow();
                if (main is MainWindow && main.IsShown())
                {
                    setting = Application.Current.LoadSetting();

                    await new Action(() =>
                    {
                        INotificationDialogService _dialogService = new NotificationDialogService();
                        NotificationConfiguration cfgDefault = NotificationConfiguration.DefaultConfiguration;
                        NotificationConfiguration cfg = new NotificationConfiguration(
                            TimeSpan.FromSeconds(setting.ToastShowTimes),
                            cfgDefault.Width + 32, cfgDefault.Height,
                            "ToastTemplate",
                            //cfgDefault.TemplateName, 
                            cfgDefault.NotificationFlowDirection
                        );

                        var newNotification = new CustomToast()
                        {
                            Title = title,
                            Message = content
                        };

                        _dialogService.ClearNotifications();
                        _dialogService.ShowNotificationWindow(newNotification, cfg);
                    }).InvokeAsync(true);
                }
            }
            catch (Exception ex) { ex.ERROR("ShowToast"); }
        }

        public static void ShowExceptionToast(this Exception ex, bool messagebox = false, string tag = "")
        {
            ex.ERROR(tag);
            ex.Message.ShowToast($"ERROR[{tag}]", messagebox, tag);
        }
        #endregion

        #region Drop Window routines
        private static void DropBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (sender is ContentWindow)
                {
                    var window = sender as ContentWindow;
                    window.DragMove();

                    var desktop = SystemParameters.WorkArea;
                    if (window.Left < desktop.Left) window.Left = desktop.Left;
                    if (window.Top < desktop.Top) window.Top = desktop.Top;
                    if (window.Left + window.Width > desktop.Left + desktop.Width) window.Left = desktop.Left + desktop.Width - window.Width;
                    if (window.Top + window.Height > desktop.Top + desktop.Height) window.Top = desktop.Top + desktop.Height - window.Height;
                    setting.DropBoxPosition = new Point(window.Left, window.Top);
                    //setting.Save();
                }
            }
            else if (e.ChangedButton == MouseButton.XButton1)
            {
                if (sender is ContentWindow)
                {
                    //var window = sender as ContentWindow;
                    //window.Hide();
                    e.Handled = true;
                }
            }
        }

        private static void DropBox_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (sender is ContentWindow && e.ClickCount == 1)
                {
                    setting = Application.Current.LoadSetting();

                    var window = sender as ContentWindow;

                    var desktop = SystemParameters.WorkArea;
                    if (window.Left < desktop.Left) window.Left = desktop.Left;
                    if (window.Top < desktop.Top) window.Top = desktop.Top;
                    if (window.Left + window.Width > desktop.Left + desktop.Width) window.Left = desktop.Left + desktop.Width - window.Width;
                    if (window.Top + window.Height > desktop.Top + desktop.Height) window.Top = desktop.Top + desktop.Height - window.Height;
                    setting.DropBoxPosition = new Point(window.Left, window.Top);
                    setting.Save();
                }
            }
        }

        private static void DropBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (sender is ContentWindow)
                {
                    var window = sender as ContentWindow;
                    // In maximum window state case, window will return normal state and continue moving follow cursor
                    if (window.WindowState == WindowState.Maximized)
                    {
                        window.WindowState = WindowState.Normal;
                        // 3 or any where you want to set window location affter return from maximum state
                        //Application.Current.MainWindow.Top = 3;
                    }
                    window.DragMove();
                }
            }
        }

        private static void DropBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 3)
            {
                if (sender is ContentWindow)
                {
                    var window = sender as ContentWindow;
                    window.Hide();
                    window.Close();
                    window = null;
                }
            }
        }

        private static void DropBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ContentWindow)
            {
                var window = sender as ContentWindow;
                window.Hide();
                window.Close();
                window = null;
            }
        }

        public static Window DropBoxExists(this Window window)
        {
            Window result = null;

            var win  = GetWindowByTitle("Dropbox");
            if (win is ContentWindow) result = win as ContentWindow;

            return (result);
        }

        public static void SetDropBoxState(this bool state)
        {
            new Action(() =>
            {
                var win = GetWindowByTitle("Dropbox");
                if (win is ContentWindow)
                    (win as ContentWindow).SetDropBoxState(state);
                else if (win is MainWindow)
                    (win as MainWindow).SetDropBoxState(state);
            }).Invoke(async: false);
        }

        public static bool ShowDropBox(this bool show)
        {
            var win = DropBoxExists(null);
            ContentWindow box = win == null ? null : (ContentWindow)win;

            if (box is ContentWindow)
            {
                box.Hide();
                box.Close();
                box = null;
            }
            else
            {
                box = new ContentWindow();
                box.MouseDown += DropBox_MouseDown;
                box.MouseUp += DropBox_MouseUp;
                ///box.MouseMove += DropBox_MouseMove;
                //box.MouseDoubleClick += DropBox_MouseDoubleClick;
                box.MouseLeftButtonDown += DropBox_MouseLeftButtonDown;
                box.Width = 48;
                box.Height = 48;
                box.MinWidth = 48;
                box.MinHeight = 48;
                box.MaxWidth = 48;
                box.MaxHeight = 48;

                box.Background = Theme.WindowTitleBrush;
                box.OverlayBrush = Theme.WindowTitleBrush;
                //box.OverlayOpacity = 0.8;

                box.Opacity = 0.85;
                box.AllowsTransparency = true;
                //box.SaveWindowPosition = true;
                //box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                box.AllowDrop = true;
                box.Topmost = true;
                box.ResizeMode = ResizeMode.NoResize;
                box.ShowInTaskbar = false;
                box.ShowIconOnTitleBar = false;
                box.ShowCloseButton = false;
                box.ShowMinButton = false;
                box.ShowMaxRestoreButton = false;
                box.ShowSystemMenuOnRightClick = false;
                box.ShowTitleBar = false;
                //box.WindowStyle = WindowStyle.None;
                box.Title = "DropBox";

                var icon = Application.Current.GetIcon();
                box.Content = icon;
                box.Icon = icon.Source;

                if (setting.DropBoxPosition != null)
                {
                    double x= setting.DropBoxPosition.X;
                    double y =setting.DropBoxPosition.Y;
                    if (x == 0 && y == 0)
                        box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    else
                    {
                        box.Left = x;
                        box.Top = y;
                    }
                }

                box.Show();
                box.Activate();
            }

            var result = box is ContentWindow ? box.IsVisible : false;
            SetDropBoxState(result);
            return (result);
        }
        #endregion
    }

    #region Custom Toast 
    public class CustomToast : Notification
    {
        [Description("Get or Set Toast Type")]
        [Category("Common Properties")]
        public ToastType Type { get; set; } = ToastType.OK;

        [Description("Get or Set Extra Contents")]
        [Category("Common Properties")]
        public string Extra { get; set; } = string.Empty;

        [Description("Get or Set State")]
        [Category("Common Properties")]
        public string State { get; set; } = string.Empty;

        [Description("Get or Set State Description")]
        [Category("Common Properties")]
        public string StateDescription { get; set; } = string.Empty;

        //public string ImgURL { get; set; }
        //public string Message { get; set; }
        //public string Title { get; set; }
        public object Tag { get; set; }
    }
    #endregion

    public static class TaskWaitingExtensions
    {
        /// <summary>
        /// Async Task Wait
        /// </summary>
        /// <typeparam name="TResult">result</typeparam>
        /// <param name="task">task instance</param>
        /// <param name="timeout">milliseconds timeout</param>
        /// <returns></returns>
        public static async Task<TResult> WaitAsync<TResult>(this Task<TResult> task, int timeout)
        {
            return (await WaitAsync(task, TimeSpan.FromMilliseconds(timeout)));
        }

        public static async Task<TResult> WaitAsync<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                var delayTask = Task.Delay(timeout, timeoutCancellationTokenSource.Token);
                if (await Task.WhenAny(task, delayTask) == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;
                }
                throw new TimeoutException("The operation has timed out.");
            }
        }
    }

    public static class ExtensionMethods
    {
        #region Time Calc Helper
        public static long MillisecondToTicks(this int millisecond)
        {
            long result = 0;
            try
            {
                result = TimeSpan.TicksPerMillisecond * millisecond;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static long TicksToMillisecond(this long ticks)
        {
            long result = 0;
            try
            {
                result = ticks / TimeSpan.TicksPerMillisecond;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static long TicksToSecond(this long ticks)
        {
            return (ticks / TimeSpan.TicksPerSecond);
        }

        public static long SecondToTicks(this long second)
        {
            return (second * TimeSpan.TicksPerSecond);
        }

        public static long FileTimeToSecond(this long filetime)
        {
            return (TicksToSecond(filetime));
        }

        public static long SecondToFileTime(this long second)
        {
            return (SecondToTicks(second));
        }

        public static long DeltaTicks(this long ticks1, long ticks2, bool abs = true)
        {
            long result = 0;
            try
            {
                result = ticks2 - ticks1;
                if (abs) result = Math.Abs(result);
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static long DeltaMillisecond(this long ticks1, long ticks2, bool abs = true)
        {
            long result = 0;
            try
            {
                result = DeltaTicks(ticks1, ticks2, abs).TicksToMillisecond();
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static long DeltaMillisecond(this DateTime dt1, DateTime dt2, bool abs = true)
        {
            long result = 0;
            try
            {
                result = DeltaMillisecond(dt1.Ticks, dt2.Ticks, abs);
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static double DeltaMilliseconds(this DateTime dt1, DateTime dt2, bool abs = true)
        {
            var delta = (dt2 - dt1).TotalMilliseconds;
            if (abs) delta = Math.Abs(delta);
            return (delta);
        }

        public static double DeltaSeconds(this DateTime dt1, DateTime dt2, bool abs = true)
        {
            var delta = (dt2 - dt1).TotalSeconds;
            if (abs) delta = Math.Abs(delta);
            return (delta);
        }

        public static double DeltaMinutes(this DateTime dt1, DateTime dt2, bool abs = true)
        {
            var delta = (dt2 - dt1).TotalMinutes;
            if (abs) delta = Math.Abs(delta);
            return (delta);
        }

        public static double DeltaHours(this DateTime dt1, DateTime dt2, bool abs = true)
        {
            var delta = (dt2 - dt1).TotalHours;
            if (abs) delta = Math.Abs(delta);
            return (delta);
        }

        public static double DeltaDays(this DateTime dt1, DateTime dt2, bool abs = true)
        {
            var delta = (dt2 - dt1).TotalDays;
            if (abs) delta = Math.Abs(delta);
            return (delta);
        }

        public static TimeSpan Delta(this DateTime dt1, DateTime dt2)
        {
            TimeSpan result = TimeSpan.FromTicks(0);
            try
            {
                result = dt2 - dt1;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static long DeltaNowMillisecond(this long ticks, bool abs = true)
        {
            long result = 0;
            try
            {
                result = DeltaMillisecond(ticks, DateTime.Now.Ticks, abs);
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static long DeltaNowMillisecond(this DateTime dt, bool abs = true)
        {
            long result = 0;
            try
            {
                result = DeltaNowMillisecond(dt.Ticks, abs);
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static bool DeltaNowMillisecond(this long ticks, int millisecond, bool abs = true)
        {
            bool result = true;
            try
            {
                result = DeltaNowMillisecond(ticks, abs) > millisecond;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static bool DeltaNowMillisecond(this DateTime dt, int millisecond, bool abs = true)
        {
            bool result = true;
            try
            {
                result = DeltaNowMillisecond(dt, abs) > millisecond;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }
        #endregion

        #region Media Play
        public static async void Sound(this object obj, string mode = "")
        {
            try
            {
                await new Action(() =>
                {
                    Sound(mode);
                }).InvokeAsync();
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void Sound(string mode)
        {
            try
            {
                if (string.IsNullOrEmpty(mode))
                {
                    SystemSounds.Beep.Play();
                }
                else
                {
                    switch (mode.ToLower())
                    {
                        case "*":
                            SystemSounds.Asterisk.Play();
                            break;
                        case "!":
                            SystemSounds.Exclamation.Play();
                            break;
                        case "?":
                            SystemSounds.Question.Play();
                            break;
                        case "d":
                            SystemSounds.Hand.Play();
                            break;
                        case "h":
                            SystemSounds.Hand.Play();
                            break;
                        case "b":
                            SystemSounds.Beep.Play();
                            break;
                        default:
                            SystemSounds.Beep.Play();
                            break;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }
        #endregion

        #region Misc Helper
        public static void Dispose(this Image image)
        {
            try
            {
                if (image is Image)
                {
                    image.Source = null;
                    image.UpdateLayout();
                }
            }
            catch (Exception ex) { ex.ERROR("DisposeImage"); }
        }

        public static void Dispose<T>(this T[] array)
        {
            array.Clear();
            array = null;
        }

        public static void Dispose<T>(this T[] array, ref T[] target)
        {
            target.Clear(ref target);
            target = null;
        }

        public static void Clear<T>(this T[] array)
        {
            try
            {
                if (array is Array)
                {
                    Array.Clear(array, 0, array.Length);
                    Array.Resize<T>(ref array, 0);
                }
            }
            catch (Exception ex) { ex.ERROR("ClearArray"); }
        }

        public static void Clear<T>(this T[] array, ref T[] target)
        {
            try
            {
                if (array is Array)
                {
                    Array.Clear(array, 0, array.Length);
                    Array.Resize<T>(ref array, 0);
                }
            }
            catch (Exception ex) { ex.ERROR("ClearArray"); }
        }
        #endregion

        #region WPF UI Helper
        public static T FindByName<T>(this FrameworkElement element, string name) where T : FrameworkElement
        {
            T result = default(T);
            try
            {
                var ret = element.FindName(name);
                if (ret is T) result = (T)ret;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static DependencyObject GetVisualChildFromTreePath(this DependencyObject dpo, int[] path)
        {
            if (path.Length == 0) return dpo;
            if (VisualTreeHelper.GetChildrenCount(dpo) == 0) return (dpo);
            List<int> newPath = new List<int>(path);
            newPath.RemoveAt(0);
            return VisualTreeHelper.GetChild(dpo, path[0]).GetVisualChildFromTreePath(newPath.ToArray());
        }

        public static childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                    return (childItem)child;
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        public static childItem FindVisualChild<childItem>(this DependencyObject parent, DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is childItem && child == obj)
                    return (childItem)child;
                else
                {
                    childItem childOfChild = child.FindVisualChild<childItem>(obj);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        public static List<T> GetVisualChildren<T>(this DependencyObject obj) where T : DependencyObject
        {
            List<T> childList = new List<T>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    childList.Add(child as T);

                var childOfChilds = child.GetVisualChildren<T>();
                if (childOfChilds != null)
                {
                    childList.AddRange(childOfChilds);
                }
            }

            if (childList.Count > 0)
                return childList;

            return null;
        }

        public static T GetVisualChild<T>(this Visual referenceVisual) where T : Visual
        {
            Visual child = null;
            for (Int32 i = 0; i < VisualTreeHelper.GetChildrenCount(referenceVisual); i++)
            {
                child = VisualTreeHelper.GetChild(referenceVisual, i) as Visual;
                if (child != null && child is T)
                {
                    break;
                }
                else if (child != null)
                {
                    child = GetVisualChild<T>(child);
                    if (child != null && child is T)
                    {
                        break;
                    }
                }
            }
            return child as T;
        }

        public static List<T> GetVisualChildren<T>(this Visual obj) where T : Visual
        {
            List<T> childList = new List<T>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is T)
                    childList.Add(child as T);

                var childOfChilds = child.GetVisualChildren<T>();
                if (childOfChilds.Count > 0)
                {
                    childList.AddRange(childOfChilds);
                }
            }
            return childList;
        }

        public static List<T> GetChildren<T>(this Visual obj) where T : Visual
        {
            List<T> childList = new List<T>();
            if (obj is Visual)
            {
                var children = obj.GetChildObjects();
                foreach (var child in children)
                {
                    if (child is T) childList.Add(child as T);
                    if (child is Visual)
                    {
                        var childOfChilds = (child as Visual).GetChildren<T>();
                        if (childOfChilds.Count > 0)
                        {
                            childList.AddRange(childOfChilds);
                        }
                    }
                }
            }
            return childList;
        }

        //private static int current_deeper = 0;
        //public static bool IsVisiualChild(this DependencyObject obj, DependencyObject parent, int deeper = 0)
        //{
        //    return (IsVisiualChild(obj, parent, 0, deeper));
        //}

        public static bool IsVisiualChild(this DependencyObject obj, DependencyObject parent, int max_deeper = 0, int current_deeper = 0)
        {
            var result = false;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child == obj)
                {
                    result = true;
                    break;
                }

                if (current_deeper < max_deeper)
                {
                    current_deeper++;
                    result = obj.IsVisiualChild(child, max_deeper, current_deeper);
                }

                if (result) break;
            }

            current_deeper = 0;
            return (result);
        }
        #endregion

        #region Graphic Helper
        public static Tuple<double, double> AspectRatio(this ImageSource image)
        {
            double bestDelta = double.MaxValue;
            double i = 1;
            int j = 1;
            double bestI = 0;
            int bestJ = 0;

            var ratio = image.Width / image.Height;

            for (int iterations = 0; iterations < 100; iterations++)
            {
                double delta = i / j - ratio;

                // Optionally, quit here if delta is "close enough" to zero
                if (delta < 0) i += 0.1;
                else if (delta == 0)
                {
                    i = 1;
                    j = 1;
                }
                else j++;

                double newDelta = Math.Abs( i / j - ratio);
                if (newDelta < bestDelta)
                {
                    bestDelta = newDelta;
                    bestI = i;
                    bestJ = j;
                }
            }
            return (new Tuple<double, double>(bestI, bestJ));
        }
        #endregion
    }

}
