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
using System.Media;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;

using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WPFNotification.Core.Configuration;
using WPFNotification.Model;
using WPFNotification.Services;
using CompactExifLib;
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
                    if (axIWebBrowser2 != null) axIWebBrowser2.GetType().InvokeMember("Silent", BindingFlags.SetProperty, null, axIWebBrowser2, new object[] { true });
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

    public enum CompareType { Auto = 0, Thumb = 1, Preview = 2, Large = 3, Original = 4 };
    public class CompareItem
    {
        public PixivItem Item { get; set; } = null;
        public CompareType Type { get; set; } = CompareType.Auto;
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
    [Flags]
    public enum SearchDateScope { Year, Season, Month, Week, Day, Hour, Minute };

    [Flags]
    public enum StorageSearchScope
    {
        None = 0,
        Title = 1, Subject = 2, Author = 4, Description = 8, Tag = 16, Keyword = 16, Copyright = 32,
        Bookmarked = 64, Followed = 128, Downloaded = 256,
        Width = 512, Height = 1024,
        Date = 2048,
        Comments = 4096,
        All = 65536
    };

    [Flags]
    public enum StorageSearchMode
    {
        None = 0,
        Not = 1,
        And = 2,
        Or = 4,
        Xor = 8,
        All = 65536
    };

    public class SearchObject
    {
        public string Query { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public StorageSearchMode Mode { get; set; } = StorageSearchMode.And;
        public StorageSearchScope Scope { get; set; } = StorageSearchScope.None;

        public SearchObject(string query, string folder = "", StorageSearchScope scope = StorageSearchScope.None, StorageSearchMode mode = StorageSearchMode.And)
        {
            Query = query;
            Folder = folder;
            Mode = mode;
            Scope = scope;
        }
    }

    public class StorageType
    {
        [JsonProperty("Folder")]
        public string Folder { get; set; } = string.Empty;
        [JsonProperty("Cached")]
        public bool Cached { get; set; } = true;
        [JsonProperty("IncludeSubFolder")]
        public bool IncludeSubFolder { get; set; } = false;
        [JsonProperty("Searchable", Required = Required.AllowNull)]
        public bool? Searchable { get; set; } = false;

        [JsonIgnore]
        public int Count { get; set; } = -1;

        public StorageType(string path, bool cached = false)
        {
            Folder = path;
            Cached = cached;
            Count = -1;
        }

        public override string ToString()
        {
            return Folder;
        }

        public void Search(string query, StorageSearchScope flags = StorageSearchScope.None, StorageSearchMode mode = StorageSearchMode.And)
        {
            if (Searchable ?? false && Directory.Exists(Folder) && !string.IsNullOrEmpty(query))
            {
                var cmd = "explorer.exe";
                var cmd_param = $"/root,\"search-ms:crumb=location:{Folder}&query={query}&\"";
                Process.Start(cmd, cmd_param);
            }
        }

        public static void Search(string query, IEnumerable<string> folders, StorageSearchScope flags = StorageSearchScope.None, StorageSearchMode mode = StorageSearchMode.And)
        {
            if (!string.IsNullOrEmpty(query) && folders is IEnumerable<string>)
            {
                var targets = folders.Where(d => Directory.Exists(d));
                var location = string.Join("&", targets.Select(d => $"crumb=location:{d}"));
                var cmd = "explorer.exe";
                var cmd_param = $"/root,\"search-ms:{location}&query={query}&\"";
                Process.Start(cmd, cmd_param);
            }
        }

        public static void Search(string query, IEnumerable<StorageType> folders, StorageSearchScope flags = StorageSearchScope.None, StorageSearchMode mode = StorageSearchMode.And)
        {
            if (!string.IsNullOrEmpty(query) && folders is IEnumerable<StorageType>)
            {
                var targets = folders.Where(d => Directory.Exists(d.Folder)).Select(d => d.Folder);
                Search(query, targets, flags);
            }
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

    #region Metadata Infomation
    public class MetaInfo
    {
        public bool TouchProfiles { get; set; } = true;

        public DateTime? DateCreated { get; set; } = null;
        public DateTime? DateModified { get; set; } = null;
        public DateTime? DateAccesed { get; set; } = null;

        public DateTime? DateAcquired { get; set; } = null;
        public DateTime? DateTaken { get; set; } = null;

        public string Title { get; set; } = null;
        public string Subject { get; set; } = null;
        public string Keywords { get; set; } = null;
        public string Comment { get; set; } = null;
        public string Authors { get; set; } = null;
        public string Copyrights { get; set; } = null;

        public int? Rating { get; set; } = null;
        public int? Ranking { get; set; } = null;

        public string Software { get; set; } = null;

        public Dictionary<string, string> Attributes { get; set; } = null;
        //public Dictionary<string, IImageProfile> Profiles { get; set; } = null;
    }
    #endregion

    public class BatchProgressInfo
    {
        public string FolderName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public long Current { get; set; } = 0;
        public long Total { get; set; } = 0;
        public double Percentage { get { return (Total > 0 && Current >= 0 ? (double)Current / Total : 0); } }
        public TaskStatus State { get; set; } = TaskStatus.Created;
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime CurrentTime { get; set; } = DateTime.Now;
        public DateTime LastestTime { get; set; } = DateTime.Now;
        public TimeSpan ElapsedTime { get { return (CurrentTime - StartTime); } }
        public TimeSpan EstimateTime { get { return (Total > 0 && Current >= 0 ? TimeSpan.FromTicks((CurrentTime - LastestTime).Ticks * (Total - Current)) : TimeSpan.FromTicks(0)); } }
    }

    public static class CommonHelper
    {
        private static Setting setting = Application.Current.LoadSetting();
        private static CacheImage cache = new CacheImage();
        private static ConcurrentDictionary<long?, Pixeez.Objects.Work> IllustCache = new ConcurrentDictionary<long?, Pixeez.Objects.Work>();
        private static ConcurrentDictionary<long?, Pixeez.Objects.UserBase> UserCache = new ConcurrentDictionary<long?, Pixeez.Objects.UserBase>();
        private static ConcurrentDictionary<long?, Pixeez.Objects.UserInfo> UserInfoCache = new ConcurrentDictionary<long?, Pixeez.Objects.UserInfo>();
        private static ConcurrentDictionary<string, byte[]> DownloadTaskCache = new ConcurrentDictionary<string, byte[]>();

        private static ConcurrentDictionary<string, int> DownloadedImageQualityInfoCache = new ConcurrentDictionary<string, int>();
        public static int GetImageQualityInfo(this string file)
        {
            var result = -1;
            try
            {
                if (DownloadedImageQualityInfoCache.ContainsKey(file))
                {
                    var ret = DownloadedImageQualityInfoCache.TryGetValue(file, out result);
                    if (!ret) result = -1;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static bool SetImageQualityInfo(this string file, int quality)
        {
            var result = false;
            try
            {
                if (DownloadedImageQualityInfoCache.ContainsKey(file))
                {
                    int old;
                    var ret = DownloadedImageQualityInfoCache.TryGetValue(file, out old);
                    result = ret && DownloadedImageQualityInfoCache.TryUpdate(file, quality, old);
                }
                else
                {
                    result = DownloadedImageQualityInfoCache.TryAdd(file, quality);
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        private static ConcurrentDictionary<string, string> _TagsCache = null;
        public static ConcurrentDictionary<string, string> TagsCache
        {
            get { if (_TagsCache == null) _TagsCache = new ConcurrentDictionary<string, string>(); return (_TagsCache); }
        }
        private static ConcurrentDictionary<string, string>  _TagsT2S = null;
        public static ConcurrentDictionary<string, string> TagsT2S
        {
            get { if (_TagsT2S == null) _TagsT2S = new ConcurrentDictionary<string, string>(StringComparer.CurrentCultureIgnoreCase); return (_TagsT2S); }
        }
        private static OrderedDictionary _TagsWildecardT2S = null;
        public static OrderedDictionary TagsWildecardT2S
        {
            get { if (_TagsWildecardT2S == null) _TagsWildecardT2S = new OrderedDictionary(); return (_TagsWildecardT2S); }
        }
        private static List<string> _BadTags_ = new List<string>();

        private class TagsWildecardCacheItem
        {
            public List<string> Keys { get; set; } = new List<string>();
            public string Translated { get; set; } = string.Empty;
            public List<string> LastKeys { get; set; } = new List<string>();
            public string LastTranslated { get; set; } = string.Empty;
        }
        private static ConcurrentDictionary<string, TagsWildecardCacheItem> _TagsWildecardT2SCache = new ConcurrentDictionary<string, TagsWildecardCacheItem>(StringComparer.CurrentCultureIgnoreCase);

        private static List<string> ext_imgs = new List<string>() { ".png", ".jpg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".jpeg" };
        private static List<string> ext_movs = new List<string>() { ".webm", ".mp4", ".mov", ".ogv", ".ogg",".gif", ".zip" };
        private static char[] trim_char = new char[] { ' ', ',', '.', '/', '\\', '\r', '\n', ':', ';' };
        private static string[] trim_str = new string[] { Environment.NewLine };
        private static string regex_img_ext = @"\.(png|jpg|jpeg|gif|bmp|zip|webp)";
        private static string regex_symbol = @"([\u0020-\u002F\u003A-\u0040\u005B-\u005E\u007B-\u007E])";
        private static string regex_invalid_char  = @"[\u2000-\u200B]";

        private static double VALUE_GB = 1024 * 1024 * 1024;
        private static double VALUE_MB = 1024 * 1024;
        private static double VALUE_KB = 1024;

        #region Shell Object Properties
        //Get a List of the properties from a type
        public static PropertyInfo[] ListOfPropertiesFromInstance(Type AType)
        {
            if (AType == null) return null;
            return AType.GetProperties(BindingFlags.Public);
        }

        //Get a List of the properties from a instance of a class
        public static PropertyInfo[] ListOfPropertiesFromInstance(object InstanceOfAType)
        {
            if (InstanceOfAType == null) return null;
            Type TheType = InstanceOfAType.GetType();
            return TheType.GetProperties(BindingFlags.Public);
        }

        //purrfect for usage example and Get a Map of the properties from a instance of a class
        public static Dictionary<string, PropertyInfo> DictionaryOfPropertiesFromInstance(object InstanceOfAType, BindingFlags? flag = null)
        {
            if (InstanceOfAType == null) return null;
            Type TheType = InstanceOfAType.GetType();
            PropertyInfo[] Properties = flag == null ? TheType.GetProperties() : TheType.GetProperties(flag.Value);
            Dictionary<string, PropertyInfo> PropertiesMap = new Dictionary<string, PropertyInfo>();
            foreach (PropertyInfo Prop in Properties)
            {
                PropertiesMap.Add(Prop.Name, Prop);
            }
            return PropertiesMap;
        }
        #endregion

        #region File type identification
        public static bool IsImage(this string file)
        {
            if (file is string && !string.IsNullOrEmpty(file))
            {

                var ext = Path.GetExtension(file).ToLower();
                return (ext_imgs.Contains(ext));
            }
            else return (false);
        }

        public static bool IsPng(this string file)
        {
            if (file is string && !string.IsNullOrEmpty(file))
            {

                var ext = Path.GetExtension(file).ToLower();
                return (ext.Equals(".png"));
            }
            else return (false);
        }

        public static bool IsMovie(this string file)
        {
            if (file is string && !string.IsNullOrEmpty(file))
            {
                var ext = Path.GetExtension(file).ToLower();
                return (ext_movs.Contains(ext));
            }
            else return (false);
        }

        public static bool IsZip(this string file)
        {
            if (file is string && !string.IsNullOrEmpty(file))
            {

                var ext = Path.GetExtension(file).ToLower();
                return (ext.Equals(".zip"));
            }
            else return (false);
        }

        public static bool IsImage(this FileInfo file)
        {
            if (file is FileInfo)
            {
                var ext = file.Extension.ToLower();
                return (ext_imgs.Contains(ext));
            }
            else return (false);
        }

        public static bool IsPng(this FileInfo file)
        {
            if (file is FileInfo)
            {
                var ext = file.Extension.ToLower();
                return (ext.Equals(".png"));
            }
            else return (false);
        }

        public static bool IsMovie(this FileInfo file)
        {
            if (file is FileInfo)
            {
                var ext = file.Extension.ToLower();
                return (ext_movs.Contains(ext));
            }
            else return (false);
        }

        public static bool IsZip(this FileInfo file)
        {
            if (file is FileInfo)
            {

                var ext = file.Extension.ToLower();
                return (ext.Equals(".zip"));
            }
            else return (false);
        }
        #endregion

        #region Pixiv Token Helper
        private static SemaphoreSlim CanRefreshToken = new SemaphoreSlim(1, 1);
        private static CancellationTokenSource CancelRefreshSource = new CancellationTokenSource();
        private static async Task<Pixeez.Tokens> RefreshToken(CancellationTokenSource cancelToken = null)
        {
            Pixeez.Tokens result = null;
            setting = Application.Current.LoadSetting();
            CancelRefreshSource = cancelToken == null ? new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout)) : cancelToken;
            if (await CanRefreshToken.WaitAsync(TimeSpan.FromSeconds(setting.DownloadHttpTimeout), CancelRefreshSource.Token))
            {
                try
                {
                    Pixeez.Auth.TimeOut = setting.DownloadHttpTimeout;
                    var authResult = await Pixeez.Auth.AuthorizeAsync(setting.User, setting.Pass, setting.RefreshToken, setting.Proxy, setting.ProxyBypass, setting.UsingProxy, CancelRefreshSource);
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
                    ex.ERROR("RefreshToken", no_stack: true);
                    if (ex.IsNetworkError())
                    {
                        if (CancelRefreshSource is CancellationTokenSource) CancelRefreshSource.Cancel();
                    }
                    else if (!string.IsNullOrEmpty(setting.User) && !string.IsNullOrEmpty(setting.Pass))
                    {
                        try
                        {
                            Pixeez.Auth.TimeOut = setting.DownloadHttpTimeout;
                            var authResult = await Pixeez.Auth.AuthorizeAsync(setting.User, setting.Pass, setting.Proxy, setting.ProxyBypass.ToArray(), setting.UsingProxy, CancelRefreshSource);
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
                            exx.ERROR("RequestToken", no_stack: true);
                            var ret = exx.Message;
                            if (CancelRefreshSource is CancellationTokenSource) CancelRefreshSource.Cancel();
                            //var tokens = await ShowLogin(canceltoken: CancelRefreshSource);
                            var tokens = await ShowLogin();
                        }
                    }
                    var rt = ex.Message;
                }
                finally
                {
                    if (CanRefreshToken is SemaphoreSlim && CanRefreshToken.CurrentCount <= 0) CanRefreshToken.Release();
                    if (CancelRefreshSource is CancellationTokenSource) CancelRefreshSource.Cancel();
                }
            }
            return (result);
        }

        private static SemaphoreSlim CanShowLogin = new SemaphoreSlim(1, 1);
        private static CancellationTokenSource CancelShowLoginSource = new CancellationTokenSource();
        public static async Task<Pixeez.Tokens> ShowLogin(bool force = false, CancellationTokenSource canceltoken = null)
        {
            Pixeez.Tokens result = null;
            CancelShowLoginSource = canceltoken is CancellationTokenSource ? canceltoken : new CancellationTokenSource();
            try
            {
                setting = Application.Current.LoadSetting();
                if (await CanShowLogin.WaitAsync(TimeSpan.FromSeconds(setting.DownloadHttpTimeout), CancelShowLoginSource.Token))
                {
                    try
                    {
                        var win = GetMainWindow();
                        if (win is MainWindow)
                        {
                            var mw = win as MainWindow;
                            mw.SetRefreshRing(rotate: true, canceltoken: CancelShowLoginSource);
                        }

                        if (GetWindow<PixivLoginDialog>() is MetroWindow) return (result);
                        Application.Current.DoEvents();
                        await Task.Delay(1);

                        if (!force && setting.ExpTime > DateTime.Now &&
                            !string.IsNullOrEmpty(setting.AccessToken) &&
                            !string.IsNullOrEmpty(setting.RefreshToken))
                        {
                            result = Pixeez.Auth.AuthorizeWithAccessToken(
                                setting.AccessToken,
                                setting.RefreshToken,
                                setting.Proxy,
                                setting.ProxyBypass,
                                setting.UsingProxy
                            );
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(setting.User) && !string.IsNullOrEmpty(setting.Pass) && !string.IsNullOrEmpty(setting.RefreshToken))
                            {
                                try
                                {
                                    result = await RefreshToken(CancelShowLoginSource);
                                }
                                catch (Exception ex)
                                {
                                    ex.ERROR("ShowLogin_RefreshToken");
                                    result = Pixeez.Auth.AuthorizeWithAccessToken(
                                        setting.AccessToken,
                                        setting.RefreshToken,
                                        setting.Proxy,
                                        setting.ProxyBypass,
                                        setting.UsingProxy
                                    );
                                    if (CancelShowLoginSource is CancellationTokenSource) CancelShowLoginSource.Cancel();
                                }
                            }
                            else
                            {
                                "Show Login Dialog...".INFO();
                                Application.Current.DoEvents();
                                var dlgLogin = new PixivLoginDialog()
                                {
                                    AccessToken = setting.AccessToken,
                                    RefreshToken = setting.RefreshToken
                                };
                                var ret = dlgLogin.ShowDialog();
                                if (ret ?? false) result = dlgLogin.Tokens;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.Message.ShowMessageBox("ERROR");
                    }
                    finally
                    {
                        if (result == null) "Request Token Error!".ShowToast("ERROR", tag: "ShowLogin");
                        if (CanShowLogin is SemaphoreSlim && CanShowLogin.CurrentCount <= 0) CanShowLogin.Release();
                        if (CancelShowLoginSource is CancellationTokenSource) CancelShowLoginSource.Cancel();
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("ShowLogin_WaitFailed"); }
            finally
            {
                var win = GetMainWindow();
                if (win is MainWindow)
                {
                    var mw = win as MainWindow;
                    mw.SetRefreshRing(rotate: false, canceltoken: CancelShowLoginSource);
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
                    browser.Document.DomDocument is mshtml.IHTMLDocument2)
                {
                    StringBuilder sb = new StringBuilder();
                    mshtml.IHTMLDocument2 document = browser.Document.DomDocument as mshtml.IHTMLDocument2;
                    mshtml.IHTMLSelectionObject currentSelection = document.selection;
                    if (currentSelection != null && currentSelection.type.Equals("Text", StringComparison.CurrentCultureIgnoreCase))
                    {
                        mshtml.IHTMLTxtRange range = currentSelection.createRange() as mshtml.IHTMLTxtRange;
                        if (range != null)
                        {
                            mshtml.IHTMLElement root = range.parentElement();
                            if (root.tagName.Equals("html", StringComparison.CurrentCultureIgnoreCase))
                            {
                                var bodies = browser.Document.GetElementsByTagName("body");
                                foreach (System.Windows.Forms.HtmlElement body in bodies)
                                {
                                    sb.AppendLine(html ? body.InnerHtml : body.InnerText);
                                }
                            }
                            else
                                sb.AppendLine(html ? range.htmlText : range.text);
                        }
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

            if (fmts.Contains("FileDrop"))
            {
                var files = (string[])(e.Data.GetData("FileDrop"));
                links = string.Join(Environment.NewLine, files).ParseLinks(false).ToList();
            }
            else if (fmts.Contains("text/html"))
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
            return (links);
        }

        public static string ParseID(this string searchContent)
        {
            var patten =  @"((UserID|PID)|(IllustID)|(User)|(Tag)|(Caption)|(Fuzzy)|(Fuzzy Tag)|(Downloading))[：:]\s*(.*?)$";
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
                try
                {
                    if (Regex.IsMatch(result, @"(UserID|PID)|(IllustID)[：:]( )*(\d+)", RegexOptions.IgnoreCase))
                        result = result.Trim();

                    else if (Regex.IsMatch(result, @"(.*?/artworks?/)(\d+)(.*)", RegexOptions.IgnoreCase))
                        result = Regex.Replace(result, @"(.*?/artworks?/)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase);
                    else if (Regex.IsMatch(result, @"(.*?illust_id=)(\d+)(.*)", RegexOptions.IgnoreCase))
                        result = Regex.Replace(result, @"(.*?illust_id=)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase);
                    else if (Regex.IsMatch(result, @"(.*?/pixiv\.navirank\.com/id/)(\d+)(.*)", RegexOptions.IgnoreCase))
                        result = Regex.Replace(result, @"(.*?/id/)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase);

                    else if (Regex.IsMatch(result, @"^(.*?\.pixiv.net/users?/)(\d+)(.*)$", RegexOptions.IgnoreCase))
                        result = Regex.Replace(result, @"^(.*?\.pixiv.net/users?/)(\d+)(.*)$", "UserID: $2", RegexOptions.IgnoreCase);
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

                    else if (Regex.IsMatch(result, @"(User|Tag|Caption|Fuzzy|Fuzzy Tag):(\s?.+)", RegexOptions.IgnoreCase))
                        result = Regex.Replace(result, @"(User|Tag|Caption|Fuzzy|Fuzzy Tag):(\s?.+)", "$1:$2", RegexOptions.IgnoreCase);
                    else if (Regex.IsMatch(Path.GetFileNameWithoutExtension(result), @"^((\d+)(_((p)|(ugoira))*\d+(x\d+)?)*)"))
                        result = Regex.Replace(Path.GetFileNameWithoutExtension(result), @"(.*?(\d+)(_((p)|(ugoira))*\d+(x\d+)?)*.*)", "$2", RegexOptions.IgnoreCase);

                    else if (!Regex.IsMatch(result, @"((UserID|PID)|(User)|(IllustID)|(Tag)|(Caption)|(Fuzzy)|(Fuzzy Tag)):", RegexOptions.IgnoreCase))
                        result = $"Fuzzy: {result}";
                }
                catch (Exception ex) { ex.ERROR("ParseLink"); link.ERROR("ParseLink"); }
            }

            return (result.Trim().Trim(trim_char).HtmlDecode());
        }

        private static string[] html_split = new string[] { Environment.NewLine, "\n", "\r", "\t", "url", "src", "href", "<p>", "</p>", "<br/>", "<br>", "<br />", "><", "</a>", ">", "&nbsp;" };//, " " };
        public static IList<string> ParseLinks(this string html, bool is_src = false)
        {
            List<string> links = new List<string>();
            var href_prefix_0 = is_src ? @"(href="")?" : string.Empty;
            var href_prefix_1 = is_src ? @"(src="")?" : string.Empty;
            var href_suffix = is_src ? @"""?" : @"";
            var group_index = is_src ? 2 : 1;
            var cmd_sep = new char[] { ':', ' ', '=' };

            var opt = RegexOptions.IgnoreCase;// | RegexOptions.Multiline;

            Func<Match, string> mf = m =>
            {
                var result = string.Empty;
                if(m.Success)
                {
                    var results = new List<string>();
                    results.Add("");
                    if (m.Groups.Count > 1)
                        //foreach(Match g in m.Groups) results.AppendLine(g.Value);
                        for(int i=1; i< m.Groups.Count;i++)
                        {
                            var mv = m.Groups[i].Value.Trim(new char[] { ' ', '"', '=' });
                            if (!html_split.Contains(mv) && !results.Contains(mv))
                                results.Add(mv);
                            else results.Add(m.Groups[0].Value);
                        }
                    results.Add("");
                    result = string.Join(Environment.NewLine, results);
                }
                return (result);
            };

            html = Regex.Replace(html, @"<div .*?>(.*?)</div>", m => mf(m), opt);
            html = Regex.Replace(html, @"<h\d+ .*?>(.*?)</h\d+>", m => mf(m), opt);
            html = Regex.Replace(html, @"<p .*?>(.*?)</p>", m => mf(m), opt);
            html = Regex.Replace(html, @"<br *?/?>", "", opt);
            html = Regex.Replace(html, @"<a .*?href=("".*?"").*?>(.*?)</a>", m => mf(m), opt);
            html = Regex.Replace(html, @"<img .*?src=("".*?"").*?>(.*?)</img>", m => mf(m), opt);
            html = Regex.Replace(html, @"<img .*?src=("".*?"").*?/?>", m => mf(m), opt);
            //html = Regex.Replace(html, @"(?<=(url|href|src)=)""(.+?)""", m => mf(m), opt);
            html = Regex.Replace(html, @"(?<=(href|src)=)""(.+?)""", m => mf(m), opt);
            html = Regex.Replace(html, @"(<.+(.*?)/?>)", "", opt);

            var mr = new List<MatchCollection>();
            foreach (var text in html.Split(html_split, StringSplitOptions.RemoveEmptyEntries))
            {
                //var content = text.StartsWith("\"") && text.EndsWith("\"") ? text.Trim(new char[] { '"', ' ' } ) : text.Trim();
                var content = Regex.Replace(text, @"Loading|(\.){2,}", "", RegexOptions.IgnoreCase).Trim(new char[] { ' ', '"', ',' } );
                if (string.IsNullOrEmpty(content)) continue;
                else if (content.Equals("=")) continue;
                else if (content.Equals("<a", StringComparison.CurrentCultureIgnoreCase)) continue;
                else if (content.Equals("<img", StringComparison.CurrentCultureIgnoreCase)) continue;
                else if (content.Equals(">", StringComparison.CurrentCultureIgnoreCase)) continue;

                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/(en/)?artworks?/\d+)" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/(en/)?users?/\d+)" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/member.*?\.php\?.*?illust_id=\d+).*?" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/member.*?\.php\?id=\d+).*?" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://pixiv\.net/[iu]/\d+).*?" + href_suffix, opt));

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

                mr.Add(Regex.Matches(content, @"^(([pu]?id)[：:][ ]*(\d+)+)", opt));
                mr.Add(Regex.Matches(content, @"^((user|fuzzy|tag|title):[ ]*(.+)+)", opt));

                mr.Add(Regex.Matches(content, @"(Searching\s)(.*?)$", opt));

                mr.Add(Regex.Matches(content, @"(Preview\sID:\s)(\d+),(.*?)$", opt));

                mr.Add(Regex.Matches(content, @"(ID[：:]\s)(\d+),(.*?)$", opt));

                mr.Add(Regex.Matches(content, @"(User:\s)(.*?)\s/\s(\d+)\s/\s(.*?)$", opt));

                mr.Add(Regex.Matches(content, @"((down(all)?|Downloading):\s?.*?)$", opt));

                if (!Regex.IsMatch(content, @"^((https?)|(<a)|(href=)|(src=)|(id:)|([pu]id[：:])|(tag:)|(user:)|(title:)|(fuzzy:)|(down(all|load(ing)?)?:)|(illust/)|(illusts/)|(artworks/)|(user/)|(users/)).*?", opt))
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
                                    mr.Add(Regex.Matches(Path.Combine(root, Path.GetFileName(content)), @"((\d+)((_((p)|(ugoira))*\d+(x\d+)?)*(_((master)|(square))+\d+)*)*(\..+)*)", opt));
                            }
                            else
                                mr.Add(Regex.Matches(content, @"((\d+)((_((p)|(ugoira\d+(x\d+)?))*\d+)*(_((master)|(square))+\d+)*)*(\..+)*)", opt));
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.ERROR();
                        mr.Add(Regex.Matches(content, @"((\d+)((_((p)|(ugoira))*\d+(x\d+)?)*(_((master)|(square)))*\d+)*(" + regex_img_ext + "))", opt));
                    }
                }
            }

            var download_links = new List<string>();
            foreach (var mi in mr.Where(m => m.Count > 0))
            {
                if (mi.Count > 50)
                {
                    "There are too many links, which may cause the program to crash and cancel the operation.".DEBUG("ParseLinks");
                    continue;
                }
                var linkexists = false;

                foreach (Match m in mi)
                {
                    if (m.Groups.Count < 1) continue;
                    var link = m.Groups[group_index].Value.Trim().Trim(trim_char);
                    if (string.IsNullOrEmpty(link)) continue;

                    var downloads = Application.Current.OpenedWindowTitles();
                    foreach (var win in downloads)
                    {
                        if (link.Equals(win)) Application.Current.ActiveWindowByTitle(win);
                    }
                    //downloads = downloads.Concat(download_links).ToList();
                    //foreach (var di in downloads)
                    //{
                    //    if (di.Contains(link))
                    //    {
                    //        linkexists = true;
                    //        break;
                    //    }
                    //}
                    //if (linkexists) continue;

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
                        else if (Regex.IsMatch(link, @"i/(\d+)$", RegexOptions.IgnoreCase))
                        {
                            var id = Regex.Replace(link, @".*?i/(\d+)$", "$1", RegexOptions.IgnoreCase);
                            link = id.ArtworkLink();
                        }
                        else if (Regex.IsMatch(link, @"u/(\d+)$", RegexOptions.IgnoreCase))
                        {
                            var id = Regex.Replace(link, @".*?u/(\d+)$", "$1", RegexOptions.IgnoreCase);
                            link = id.ArtistLink();
                        }
                        if (!links.Contains(link)) links.Add(link.Contains(".pixiv.") ? link.Replace("http://", "https://") : link);
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
                    else if (Regex.IsMatch(link, @"^[pu]id[：:] *?\d+", RegexOptions.IgnoreCase))
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
                            var sid = Regex.Replace(Path.GetFileNameWithoutExtension(fn), @"^(\d+)((_(p|ugoira)d+)?.*?)$", "$1", RegexOptions.IgnoreCase);
                            var IsFile = string.IsNullOrEmpty(Path.GetExtension(fn)) && !fn.Contains('_') ? false : true;
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
                if (html.IsFile())
                {
                    var fn = Path.GetFileNameWithoutExtension(html);
                    if (Regex.IsMatch(fn, @"^\d+.*?\d+$", RegexOptions.IgnoreCase)) links.Add(fn.GetIllustId().ArtworkLink());
                    else links.Add($"Fuzzy:{fn}");
                }
                else if (html.Split(Path.GetInvalidPathChars()).Length <= 1 && download_links.Count <= 0) links.Add($"Fuzzy:{html}");
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
        public static bool IsAlpha(this string text)
        {
            return (Regex.IsMatch(text, @"^[\u0020-\u007E]+$", RegexOptions.IgnoreCase));
        }

        //
        // https://stackoverflow.com/a/6944095/1842521
        //
        public static int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
            {
                if (string.IsNullOrEmpty(t))
                    return 0;
                return t.Length;
            }

            if (string.IsNullOrEmpty(t))
            {
                return s.Length;
            }

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // initialize the top and right of the table to 0, 1, 2, ...
            for (int i = 0; i <= n; d[i, 0] = i++) ;
            for (int j = 1; j <= m; d[0, j] = j++) ;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    int min1 = d[i - 1, j] + 1;
                    int min2 = d[i, j - 1] + 1;
                    int min3 = d[i - 1, j - 1] + cost;
                    d[i, j] = Math.Min(Math.Min(min1, min2), min3);
                }
            }
            return d[n, m];
        }

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

        #region Convert Chinese to Japanese Kanji
        static private Encoding GB2312 = Encoding.GetEncoding("GB2312");
        static private Encoding JIS = Encoding.GetEncoding("SHIFT_JIS");
        static private List<char> GB2312_List { get; set; } = new List<char>();
        static private List<char> JIS_List { get; set; } = new List<char>();
        static private void InitGBJISTable()
        {
            if (JIS_List.Count == 0 || GB2312_List.Count == 0)
            {
                JIS_List.Clear();
                GB2312_List.Clear();

                var jis_data =  Properties.Resources.GB2JIS;
                var jis_count = jis_data.Length / 2;
                JIS_List = JIS.GetString(jis_data).ToList();
                GB2312_List = GB2312.GetString(jis_data).ToList();

                var jis = new byte[2];
                var gb2312 = new byte[2];
                for (var i = 0; i < 94; i++)
                {
                    gb2312[0] = (byte)(i + 0xA1);
                    for (var j = 0; j < 94; j++)
                    {
                        gb2312[1] = (byte)(j + 0xA1);
                        var offset = i * 94 + j;
                        GB2312_List[offset] = GB2312.GetString(gb2312).First();

                        jis[0] = jis_data[2 * offset];
                        jis[1] = jis_data[2 * offset + 1];
                        JIS_List[i * 94 + j] = JIS.GetString(jis).First();
                    }
                }
            }
        }

        static public char ConvertChinese2Japanese(this char character)
        {
            var result = character;

            InitGBJISTable();
            var idx = GB2312_List.IndexOf(result);
            if (idx >= 0) result = JIS_List[idx];

            return (result);
        }

        static public string ConvertChinese2Japanese(this string line)
        {
            var result = line;

            result = string.Join("", line.ToCharArray().Select(c => ConvertChinese2Japanese(c)));
            //result = new string(line.ToCharArray().Select(c => ConvertChinese2Japanese(c)).ToArray());

            return (result);
        }

        static public IList<string> ConvertChinese2Japanese(this IEnumerable<string> lines)
        {
            var result = new List<string>();

            result.AddRange(lines.Select(l => ConvertChinese2Japanese(l)).ToList());

            return (result);
        }

        static public char ConvertJapanese2Chinese(this char character)
        {
            var result = character;

            InitGBJISTable();
            var idx = JIS_List.IndexOf(result);
            if (idx >= 0) result = GB2312_List[idx];

            return (result);
        }

        static public string ConvertJapanese2Chinese(this string line)
        {
            var result = line;

            result = string.Join("", line.ToCharArray().Select(c => ConvertJapanese2Chinese(c)));
            //result = new string(line.ToCharArray().Select(c => ConvertJapanese2Chinese(c)).ToArray());

            return (result);
        }

        static public IList<string> ConvertJapanese2Chinese(this IEnumerable<string> lines)
        {
            var result = new List<string>();

            result.AddRange(lines.Select(l => ConvertJapanese2Chinese(l)).ToList());

            return (result);
        }
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

        private static ConcurrentDictionary<string, string> _symbol_letters_ = new ConcurrentDictionary<string, string>();
        private static ConcurrentDictionary<string, string> SymbolLetterTable
        {
            get
            {
                if (_symbol_letters_.Count == 0)
                {
                    int au = 0x41, al = 0x61;
                    int[] su = { 0x1D400, 0x1D434, 0x1D468, 0x1D49C, 0x1D4D0, 0x1D504, 0x1D538, 0x1D56C, 0x1D5A0, 0x1D5D4, 0x1D608, 0x1D63C, 0x1D670 };
                    int[] sl = { 0x1D41A, 0x1D44E, 0x1D482, 0x1D4B6, 0x1D4EA, 0x1D51E, 0x1D552, 0x1D586, 0x1D5BA, 0x1D5EE, 0x1D622, 0x1D656, 0x1D68A };

                    foreach (var i in Enumerable.Range(0, 26))
                    {
                        foreach (var u in su)
                            _symbol_letters_.TryAdd(char.ConvertFromUtf32(u + i), char.ConvertFromUtf32(au + i));
                        foreach (var l in sl)
                            _symbol_letters_.TryAdd(char.ConvertFromUtf32(l + i), char.ConvertFromUtf32(al + i));
                    }
                }
                return (_symbol_letters_);
            }
        }

        public static string SymbolToLetter(this string text)
        {
            var result = new List<string>();
            try
            {
                for (int i = 0; i < text.Length; i++)
                {
                    var t = char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i+1]) ? $"{char.ConvertFromUtf32(char.ConvertToUtf32(text[i], text[++i]))}" : $"{text[i]}";
                    result.Add(SymbolLetterTable.ContainsKey(t) ? SymbolLetterTable[t] : t);
                }
            }
            catch (Exception ex) { ex.ERROR("LetterNormalizing"); }
            return (result.Count <= 0 ? text : string.Join(string.Empty, result));
        }

        public static string Normalizing(this string text)
        {
            return (SymbolToLetter(KatakanaHalfToFull(text)));
        }

        public static string MaintainCustomTagFile(this Application app, bool save = true)
        {
            var setting = Application.Current.LoadSetting();
            var tag_file = Path.Combine(Application.Current.GetRoot(), setting.CustomTagsFile);
            if (!string.IsNullOrEmpty(tag_file))
            {
                try
                {
                    var keys = _TagsT2S.Keys.Distinct().ToList();
                    foreach (var k in keys)
                    {
                        _TagsT2S[k.Trim()] = _TagsT2S[k].Trim();
                    }
                    var sd = new SortedDictionary<string, string>(_TagsT2S, StringComparer.CurrentCultureIgnoreCase);
                    //Sort(tags);
                    var tags_o = JsonConvert.SerializeObject(sd, Newtonsoft.Json.Formatting.Indented);
                    if (save) File.WriteAllText(tag_file, tags_o, new UTF8Encoding(true));
                }
                catch (Exception ex) { ex.ERROR($"MaintainCustomTagFile_{Path.GetFileName(tag_file)}"); }
            }
            return (tag_file);
        }

        public static void TagWildcardCacheClear(this IEnumerable<string> keys)
        {
            try { foreach (var key in keys) TagWildcardCacheClear(key); }
            catch (Exception ex) { ex.ERROR("TagWildcardCacheClear"); }
        }

        public static void TagWildcardCacheClear(this string key)
        {
            try
            {
                if (_TagsWildecardT2SCache.ContainsKey(key))
                {
                    _BadTags_.RemoveAll(s => _TagsWildecardT2SCache[key].Keys.Contains(s));

                    _TagsWildecardT2SCache[key].Keys.Clear();
                    _TagsWildecardT2SCache[key].Translated = string.Empty;
                    TagsWildecardCacheItem v = null;
                    _TagsWildecardT2SCache.TryRemove(key, out v);
                }
            }
            catch (Exception ex) { ex.ERROR("TagWildcardCacheClear"); }
        }

        public static void TagWildcardCacheClear(Application app)
        {
            try { _BadTags_.Clear(); _TagsWildecardT2SCache.Clear(); }
            catch (Exception ex) { ex.ERROR("TagWildcardCacheClear"); }
        }

        public static void TagWildcardCacheUpdate(this string key, string value = "")
        {
            try
            {
                if (!(_TagsWildecardT2SCache is ConcurrentDictionary<string, TagsWildecardCacheItem>))
                    _TagsWildecardT2SCache = new ConcurrentDictionary<string, TagsWildecardCacheItem>(StringComparer.CurrentCultureIgnoreCase);

                _BadTags_.RemoveAll(s => _TagsWildecardT2SCache[key].Keys.Contains(s));

                var k = key.Trim('/').Replace(" ", @"\s");
                var v = value.Trim();
                foreach (var cache in _TagsWildecardT2SCache)
                {
                    if (cache.Value.Translated.Equals(value, StringComparison.CurrentCultureIgnoreCase) &&
                        cache.Key.Equals(key, StringComparison.CurrentCultureIgnoreCase)) continue;

                    if (cache.Value.Keys.Contains(k) && Regex.IsMatch(cache.Value.Translated, value, RegexOptions.IgnoreCase)) continue;

                    if (cache.Value.LastKeys == null) { cache.Value.LastKeys = new List<string>(); cache.Value.LastKeys.AddRange(cache.Value.Keys); }

                    if (cache.Value.Keys.Contains(k) || cache.Value.LastKeys.Contains(k) || Regex.IsMatch(cache.Key, k, RegexOptions.IgnoreCase) ||
                        (!string.IsNullOrEmpty(cache.Value.Translated) && Regex.IsMatch(cache.Value.Translated, k, RegexOptions.IgnoreCase)) ||
                        (!string.IsNullOrEmpty(cache.Value.LastTranslated) && Regex.IsMatch(cache.Value.LastTranslated, k, RegexOptions.IgnoreCase)))
                    {
                        cache.Value.LastKeys.Clear();
                        cache.Value.LastKeys.AddRange(cache.Value.Keys);
                        cache.Value.LastTranslated = cache.Value.Translated;

                        cache.Value.Translated = string.Empty;
                        cache.Value.Keys.Clear();
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("TagWildcardChanged"); }
        }

        public static void TagWildcardCacheUpdate(this DictionaryEntry entry)
        {
            try
            {
                var k = (entry.Key as string).Trim();
                var v = (entry.Value as string).Trim();
                TagWildcardCacheUpdate(k, v);
            }
            catch (Exception ex) { ex.ERROR("TagWildcardChanged"); }
        }

        public static string TranslatedText(this string src, out string matched, string translated = default(string))
        {
            var result = src;
            matched = string.Empty;
            try
            {
                List<string> matches = new List<string>();

                src = string.IsNullOrEmpty(src) ? string.Empty : src.Normalizing().Trim();
                translated = string.IsNullOrEmpty(translated) ? string.Empty : translated.Normalizing().Trim();
                if (string.IsNullOrEmpty(src)) return (string.Empty);

                result = src;
                #region Pixiv Tag Translated
                bool TagsMatched = false;
                if (TagsCache is ConcurrentDictionary<string, string>)
                {
                    if (string.IsNullOrEmpty(translated) || src.Equals(translated, StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (TagsCache.ContainsKey(src))
                        {
                            var tag_t = TagsCache[src];
                            if (!string.IsNullOrEmpty(tag_t))
                            {
                                result = tag_t;
                                matches.Add($"Tags => {src}");
                                TagsMatched = true;
                            }
                        }
                    }
                    else
                    {
                        TagsCache[src] = translated;
                        result = translated;
                        matches.Add($"Tags => {src}");
                        TagsMatched = true;
                    }
                }
                #endregion

                #region My Custom Tag Translated
                bool CustomTagsMatched = false;
                if (TagsT2S is ConcurrentDictionary<string, string>)
                {
                    if (TagsT2S.ContainsKey(src))
                    {
                        result = TagsT2S[src];
                        matches.Add($"CustomTags => {src}");
                        src.TagWildcardCacheUpdate(result);
                        CustomTagsMatched = true;
                    }
                    else if (TagsT2S.ContainsKey(result))
                    {
                        result = TagsT2S[result];
                        matches.Add($"CustomTags => {result}");
                        result.TagWildcardCacheUpdate(result);
                        CustomTagsMatched = true;
                    }
                }
                #endregion

                #region My Custom Wildcard Tag Translated
                if (!(_TagsWildecardT2SCache is ConcurrentDictionary<string, TagsWildecardCacheItem>))
                    _TagsWildecardT2SCache = new ConcurrentDictionary<string, TagsWildecardCacheItem>(StringComparer.CurrentCultureIgnoreCase);

                if (_TagsWildecardT2SCache.ContainsKey(src) &&
                    _TagsWildecardT2SCache[src].Keys.Count > 0 &&
                    !string.IsNullOrEmpty(_TagsWildecardT2SCache[src].Translated))
                {
                    result = _TagsWildecardT2SCache[src].Translated;
                    matches.Add($"CustomWildcardTagsCache => {src}:{string.Join(";", _TagsWildecardT2SCache[src].Keys)}");
                }
                else if (TagsWildecardT2S is OrderedDictionary)
                {
                    var alpha = result.IsAlpha();
                    var text = alpha || !(TagsMatched || CustomTagsMatched) ? src : result;
                    var keys = new List<string>();
                    foreach (DictionaryEntry entry in TagsWildecardT2S)
                    {
                        try
                        {
                            var k = (entry.Key as string).Trim('/');//.Replace(" ", @"\s");
                            if (_BadTags_.Contains(k)) continue;
                            var v = (entry.Value as string).Replace("\n", "\\n");
                            var vt = text;
                            text = Regex.Replace(text, k, m =>
                            {
                                if (string.IsNullOrEmpty(v)) return (v);
                                var vs = Regex.Replace(v, @"\$(\d+)(%.*?%)", idx =>
                            {
                                var i = int.Parse(idx.Groups[1].Value);
                                var t = idx.Groups[2].Value.Trim('%');
                                if (t.StartsWith("!"))
                                    return (m.Groups[i].Success ? string.Empty : $"{t.Substring(1)}");
                                else
                                    return (m.Groups[i].Success ? $"{t}" : string.Empty);
                            });
                                for (int i = 0; i < m.Groups.Count; i++)
                                {
                                    vs = vs.Replace($"${i}", m.Groups[i].Value);
                                }
                                if (!keys.Contains(k) && !string.IsNullOrEmpty(m.Value)) { keys.Add(k); vt = vt.Replace(m.Value, vs); }
                                else { if (vt.Contains(vs) && vs.Length > 2) vs = string.Empty; }
                                return (vs);
                            }, RegexOptions.IgnoreCase);
                        }
                        catch (Exception exw) { _BadTags_.Add((entry.Key as string).Trim('/')); exw.ERROR("TRANSLATE"); }
                    }

                    if (keys.Count > 0 && !text.Equals(src) && (!TagsMatched || LevenshteinDistance(text, src) > 1))
                    {
                        var result_sym = Regex.Replace(result, regex_symbol, @"\$1", RegexOptions.IgnoreCase);
                        result = alpha && !Regex.IsMatch(text, result_sym, RegexOptions.IgnoreCase) ? $"{text}{Environment.NewLine}💬{result}" : text;
                        if (!result.Equals(src))
                        {
                            _TagsWildecardT2SCache[src] = new TagsWildecardCacheItem()
                            {
                                Keys = keys.Distinct().ToList(),
                                Translated = result
                            };
                            matches.Add($"CustomWildcardTags => '{string.Join(";", keys)}'");
                        }
                    }
                }
                #endregion

                #region TTS Slicing result
                if (result.StartsWith("`") || result.StartsWith("^")) result = result.Substring(1);
                else if (setting.TextSlicingUsingTTS)
                {
                    var ptags = TagsT2S is ConcurrentDictionary<string, string>;
                    var ctags = TagsCache is ConcurrentDictionary<string, string>;
                    if (ptags && !TagsT2S.ContainsKey(src))
                    {
                        var ipos = result.IndexOf("💬");
                        var trans = ipos > 0 ? result.Substring(ipos) : string.Empty;
                        var text = ipos > 0 ? result.Replace(trans, string.Empty).Trim() : result;
                        var culture = src.DetectCulture();
                        if (culture == null || (culture is CultureInfo && culture.IetfLanguageTag.Equals(CultureInfo.CurrentUICulture.IetfLanguageTag)))
                            culture = CultureInfo.GetCultureInfo("ja-jp");
                        var slice_words = Speech.Slice(src, culture);
                        if (slice_words is IList<string>)
                        {
                            foreach (var word in slice_words)
                            {
                                if (ctags && TagsT2S.ContainsKey(word))
                                {
                                    text = text.Replace(word, TagsT2S[word]);
                                    matches.Add($"CustomTags => {word}");
                                    CustomTagsMatched = true;
                                }
                                else if (ptags && TagsCache.ContainsKey(word))
                                {
                                    var alpha = TagsCache[word].IsAlpha();
                                    if (!TagsCache[word].IsAlpha())
                                    {
                                        text = text.Replace(word, TagsCache[word]);
                                        matches.Add($"Tags => {word}");
                                        TagsMatched = true;
                                    }
                                }
                            }
                            result = string.IsNullOrEmpty(trans) ? text : $"{text}{Environment.NewLine}{trans}";
                        }
                    }
                }
                #endregion

                //var contains = CultureInfo.CurrentCulture.CompareInfo.IndexOf(result, translated, CompareOptions.IgnoreCase) >= 0;
                var contains = result.IndexOf(translated, StringComparison.CurrentCultureIgnoreCase) >= 0;
                //if (!string.IsNullOrEmpty(translated) && translated.IsAlpha() && !contains) result = $"{result}{Environment.NewLine}💭{translated}";
                if (!string.IsNullOrEmpty(translated) && !contains) result = $"{result}{Environment.NewLine}💭{translated}";
                matched = string.Join(", ", matches);
            }
            catch (Exception ex) { ex.ERROR("TRANSLATE"); }
            return (result.Trim().Replace("\\n", "\n").TrimEnd(new char[] { ',', '，' }));
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
                //if (ts[i].Length > lineLength) ts[i] = Regex.Replace(ts[i], @"(.{" + lineLength + @"})", "$1" + Environment.NewLine, RegexOptions.IgnoreCase);
                var count = 0;
                ts[i] = string.Join("", ts[i].Select(c =>
                {
                    count = (int)c > 255 && !char.IsSurrogate(c) ? count + 2 : count + 1;
                    var can_break = !char.IsSurrogate(c) && // here have a bug when character in UNICODE CJK Extentions area
                                    char.GetUnicodeCategory(c) != UnicodeCategory.Format &&
                                    char.GetUnicodeCategory(c) != UnicodeCategory.ConnectorPunctuation &&
                                    char.GetUnicodeCategory(c) != UnicodeCategory.DashPunctuation &&
                                    char.GetUnicodeCategory(c) != UnicodeCategory.InitialQuotePunctuation &&
                                    char.GetUnicodeCategory(c) != UnicodeCategory.OpenPunctuation &&
                                    char.GetUnicodeCategory(c) != UnicodeCategory.MathSymbol &&
                                    char.GetUnicodeCategory(c) != UnicodeCategory.ModifierSymbol &&
                                    char.GetUnicodeCategory(c) != UnicodeCategory.OtherSymbol &&
                                    char.GetUnicodeCategory(c) != UnicodeCategory.OtherNotAssigned &&
                                    char.GetUnicodeCategory(c) != UnicodeCategory.ModifierSymbol;
                    var crlf = count >= lineLength && can_break ? Environment.NewLine : string.Empty;
                    if (!string.IsNullOrEmpty(crlf)) count = 0;
                    if (c == '\n' || c == '\r' ||
                        char.GetUnicodeCategory(c) == UnicodeCategory.LineSeparator ||
                        char.GetUnicodeCategory(c) == UnicodeCategory.ParagraphSeparator) { count = 0; crlf = string.Empty; }
#if DEBUG
                    Debug.WriteLine($"{c}, {(int)c}[{char.GetUnicodeCategory(c).ToString()}] => {count}");
#endif
                    return ($"{c}{crlf}");
                }));
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

                result = Regex.Replace(result, @"&#x([0-9a-fA-F]{1,6});", m => char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value, 16)));
                result = Regex.Replace(result, @"&#(\d+){1,6};", m => char.ConvertFromUtf32(Convert.ToInt32(m.Groups[1].Value)));

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
                result = Regex.Replace(sb.ToString().FilterInvalidChar(), @"<[^>]*>", "").TrimEnd();
            }
            catch (Exception ex) { ex.ERROR("HtmlToText"); result = html.HtmlDecode(false); }
            return result;
        }

        public static string HtmlToText(this string html, bool decode = false, bool br = false, bool limit = false, int limitcount = 512, bool breakline = false, int breakcount = 72)
        {
            if (string.IsNullOrEmpty(html)) return (html);
            var result = html.TrimEnd().HtmlToText();
            if (decode) result = result.HtmlDecode(br);
            if (limit) result = string.Join("", result.Take(limitcount));
            if (breakline) result = result.InsertLineBreak(breakcount);
            return (result);
        }

        public static string FilterInvalidChar(this string text)
        {
            if (string.IsNullOrEmpty(text)) return (text);
            else return (Regex.Replace(text, regex_invalid_char, " "));
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
                        html.AppendLine("      :root {--accent:{% accentcolor_rgb %}; --text:{% textcolor_rgb %} }");
                        html.AppendLine("      *{font-family:\"等距更纱黑体 SC\", FontAwesome, \"Segoe UI Emoji\", \"Segoe MDL2 Assets\", \"Segoe UI\", Iosevka, \"Sarasa Mono J\", \"Sarasa Term J\", \"Sarasa Gothic J\", \"更纱黑体 SC\", 思源黑体, 思源宋体, 微软雅黑, 宋体, 黑体, 楷体, Consolas, \"Courier New\", Tahoma, Arial, Helvetica, sans-serif !important;}");
                        html.AppendLine("      body{background-color:{% backcolor %} !important;}");
                        html.AppendLine("      a:link{color:{% accentcolor %} !important; text-decoration:none !important;}");
                        html.AppendLine("      a:hover{color:{% accentcolor %} !important; text-decoration:none !important;}");
                        html.AppendLine("      a:active{color:{% accentcolor %} !important; text-decoration:none !important;}");
                        html.AppendLine("      a:visited{color:{% accentcolor %} !important; text-decoration:none !important;}");
                        html.AppendLine("      // a[title]:hover::after{content:attr(title); position:absolute; top:100%; left:0;}");
                        html.AppendLine("      img{width:auto !important; height:auto !important; max-width:100%! important; max-height:100% !important;}");
                        html.AppendLine("      .tag{color:{% accentcolor %} !important; background-color:rgba(var(--accent), 10%); line-height:1.6em; padding:0 2px 0 1px; text-decoration:none; border:1px solid {% accentcolor %}; border-left-width:5px; overflow-wrap:break-word; position:relative; display:inline-block; margin-bottom:0.5em;}");
                        html.AppendLine("      .tag.::before{content:'#';}");
                        html.AppendLine("      .section{color:{% accentcolor %} !important; background-color:rgba(var(--accent), 10%); line-height:1.6em; padding:0 2px 0 1px; text-decoration:none; border:1px solid {% accentcolor %}; border-left-width:5px; overflow-wrap:break-word; position:relative; display:block; margin-bottom:0.5em;}");
                        html.AppendLine("      .desc{color:{% textcolor %} !important; text-decoration:none !important; width:99% !important; word-wrap:break-word !important; overflow-wrap:break-word !important; white-space:normal !important; padding-bottom:0.5em !important;}");
                        html.AppendLine("      .twitter::before{font-family:FontAwesome; content:''; margin-left:3px; padding-right:4px; color:#1da1f2;}");
                        html.AppendLine("      .web::before{content:'🌐'; padding-right:3px; margin-left:-0px;}");
                        html.AppendLine("      .mail::before{content:'🖃'; padding-right:4px; margin-left:2px;}");
                        html.AppendLine("      .E404{display:block; min-height:calc(95vh); background-image:url('{% site %}/404.jpg'); background-position:center; background-attachment:fixed; background-repeat:no-repeat;}");
                        html.AppendLine("      .E404T{font-size:calc(2.5vw); color:gray; position:fixed; margin-left:calc(50vw); margin-top:calc(50vh);}");
                        html.AppendLine();
                        html.AppendLine("      @media screen and(-ms-high-contrast:active), (-ms-high-contrast:none) {");
                        html.AppendLine("      .tag{color:{% accentcolor %} !important; background-color:rgba({% accentcolor_rgb %}, 0.1); line-height:1.6em; padding:0 2px 0 1px; text-decoration:none; border:1px solid {% accentcolor %}; border-left-width:5px; overflow-wrap:break-word; position:relative; display:inline-block; margin-bottom:0.5em;}");
                        html.AppendLine("      .section{color:{% accentcolor %} !important; background-color:rgba({% accentcolor_rgb %}, 0.1); line-height:1.6em; padding:0 2px 0 1px; text-decoration:none; border:1px solid {% accentcolor %}; border-left-width:5px; overflow-wrap:break-word; position:relative; display:block; margin-bottom:0.5em;}");
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

            template = Regex.Replace(template, @"<br\s*/?>(\r\n|\n\r|\n|\r)+", $@"<br />{Environment.NewLine}", RegexOptions.IgnoreCase);
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

        public static int GetIllustPageIndex(this string url)
        {
            int idx = -1;
            if (!string.IsNullOrEmpty(url))
            {
                //var idx_s = Regex.Replace(Path.GetFileName(url), @"\d+_.*?p?(\d+)\.\w+", "$1", RegexOptions.IgnoreCase);
                var idx_s = Regex.Replace(Path.GetFileName(url), @"\d+_p?(\d+)(_.*?)?\.\w+", "$1", RegexOptions.IgnoreCase);
                int.TryParse(idx_s, out idx);
            }
            return (idx);
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

        public static string GetIllustId(this string url, out int index)
        {
            index = GetIllustPageIndex(url);
            return (GetIllustId(url));
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

        public static void ShellImageCompare(this string file_s, string file_t = "")
        {
            if (string.IsNullOrEmpty(setting.ShellImageCompareCmd)) return;
            var shell = Path.IsPathRooted(setting.ShellImageCompareCmd) ? setting.ShellImageCompareCmd : Path.Combine(Application.Current.GetRoot(), setting.ShellImageCompareCmd);
            if (File.Exists(shell))
            {
                try
                {
                    Process.Start(shell, $"\"{file_s}\" \"{file_t}\"");
                }
                catch (Exception ex) { ex.ERROR("ShellImageCompare"); }
            }
        }

        public static void ShellImageCompare(this IEnumerable<string> files)
        {
            if (string.IsNullOrEmpty(setting.ShellImageCompareCmd)) return;
            var shell = Path.IsPathRooted(setting.ShellImageCompareCmd) ? setting.ShellImageCompareCmd : Path.Combine(Application.Current.GetRoot(), setting.ShellImageCompareCmd);
            if (File.Exists(shell) && files is IEnumerable<string> && files.Count() >= 1)
            {
                try
                {
                    Process.Start(shell, string.Join(" ", files.TakeWhile(f => !string.IsNullOrEmpty(f.Trim())).Select(f => $"\"{f.Trim()}\"")));
                }
                catch (Exception ex) { ex.ERROR("ShellImageCompare"); }
            }
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
            if (string.IsNullOrEmpty(setting.ShellSearchBridgeApplication)) return;
            var shell = Path.IsPathRooted(setting.ShellSearchBridgeApplication) ? setting.ShellSearchBridgeApplication : Path.Combine(Application.Current.GetRoot(), setting.ShellSearchBridgeApplication);
            if (File.Exists(shell))
            {
                Process.Start(shell, contents);
            }
        }

        public static void OpenPixivPediaWithShell(this string contents)
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
            catch (Exception ex) { ex.ERROR("OpenUrlWithShell"); }

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
                        Application.Current.ReleaseKeyboardModifiers(use_keybd_event: true);
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
                        setting = Application.Current.LoadSetting();

                        var AltViewer = (int)(Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Control)) == 3 ? !setting.ShellImageViewerEnabled : setting.ShellImageViewerEnabled;
                        var ShowProperties = Keyboard.Modifiers == ModifierKeys.Alt ? true : false;
                        var UsingOpenWith = !ShowProperties && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? true : false;

                        var SysDir = Path.Combine(WinDir, Environment.Is64BitOperatingSystem ? "SysWOW64" : "System32", "OpenWith.exe");
                        var OpenWith = string.IsNullOrEmpty(WinDir) ? string.Empty : SysDir;
                        var openwith_exists = File.Exists(OpenWith) ?  true : false;
                        var command_full = Path.IsPathRooted(command) ? command : command.Where().FirstOrDefault();

                        Application.Current.ReleaseKeyboardModifiers(use_keybd_event: true);
                        Application.Current.DoEvents();

                        if (ShowProperties)
                        {
                            file.OpenShellProperties();
                        }
                        else if (string.IsNullOrEmpty(command_full) && UsingOpenWith && openwith_exists)
                        {
                            Process.Start(OpenWith, file);
                            result = true;
                        }
                        else
                        {
                            var IsImage = ext_imgs.Contains(Path.GetExtension(file).ToLower()) ? true : false;
                            var ext = string.Join("|", ext_movs.Select(e => e.Substring(1)));
                            var IsUgoira = Regex.IsMatch(Path.GetFileName(file), $@"\d+_ugoira\d+x\d+\.({ext})", RegexOptions.IgnoreCase);
                            if (AltViewer && IsImage && string.IsNullOrEmpty(command_full))
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
                            else if (IsUgoira && !string.IsNullOrEmpty(setting.ShellUgoiraViewer))
                            {
                                if (!File.Exists(setting.ShellUgoiraViewer))
                                {
                                    var cmd_found = setting.ShellUgoiraViewer.Where();
                                    if (cmd_found.Length > 0) setting.ShellUgoiraViewer = cmd_found.First();
                                }
                                var args = $"\"{file}\"";
                                if (string.IsNullOrEmpty(setting.ShellUgoiraViewer))
                                    Process.Start(file);
                                else
                                    Process.Start(setting.ShellUgoiraViewer, args.Trim());
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(command_full))
                                    Process.Start(file);
                                else
                                    Process.Start(command_full, $"{custom_params} \"{file}\"".Trim());
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

        public static bool OpenShellProperties(this string FileName)
        {
            bool result = false;
            if (!string.IsNullOrEmpty(FileName) && File.Exists(FileName))
            {
                try
                {
                    //result = ShowFileProperties(FileName);
                    result = ShellProperties.Show(FileName) == 0 ? true : false;
                }
                catch (Exception ex) { ex.ERROR("OpenShellProperties"); }
            }
            return (result);
        }

        public static bool OpenShellProperties(this IEnumerable<string> FileName)
        {
            bool result = false;
            if (FileName is IEnumerable<string> && FileName.Count() > 0)
            {
                try
                {
                    //result = ShowFileProperties(FileName);
                    result = ShellProperties.Show(FileName) == 0 ? true : false;
                }
                catch (Exception ex) { ex.ERROR("OpenShellProperties"); }
            }
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
                var rate = delta.TotalSeconds <= 0 ? 0 : di.Received / delta.TotalSeconds;
                var size = di.State == Common.DownloadState.Finished && File.Exists(di.FileName) ? (new FileInfo(di.FileName)).Length.SmartFileSize() : "????";
                //var fmt = di.SaveAsJPEG ? $"JPG_Q<={di.JPEGQuality}" : $"{Path.GetExtension(di.FileName).Trim('.').ToUpper()}";
                var quality = di.FileName.GetImageQualityInfo();
                var fmt = di.SaveAsJPEG && quality != -1 ? $"JPG_Q≈{quality}" : $"{Path.GetExtension(di.FileName).Trim('.').ToUpper()}";
                fmt = string.IsNullOrEmpty(di.ConvertReason) ? fmt : di.ConvertReason.Trim();
                result.Add($"URL    : {di.Url}");
                result.Add($"File   : {di.FileName}, {di.FileTime.ToString("yyyy-MM-dd HH:mm:sszzz")}");
                result.Add($"State  : {di.State}{fail} Disk Usage : {size}, {fmt}");
                result.Add($"Elapsed: {di.StartTime.ToString("yyyy-MM-dd HH:mm:sszzz")} -> {di.EndTime.ToString("yyyy-MM-dd HH:mm:sszzz")}, {delta.SmartElapsed()} s");
                result.Add($"Status : {di.Received.SmartFileSize()} / {di.Length.SmartFileSize()} ({di.Received} Bytes / {di.Length} Bytes), Rate ≈ {rate.SmartSpeedRate()}");
            }
            return (result);
        }

        public static Func<double, string> SmartSpeedRateFunc = (v) => { return(SmartSpeedRate(v)); };

        public static string SmartSpeedRate(this long v, double factor = 1, bool unit = true, bool trimzero = true, int padleft = 0) { return (SmartSpeedRate((double)v, factor, unit, trimzero: trimzero, padleft: padleft)); }

        public static string SmartSpeedRate(this double v, double factor = 1, bool unit = true, bool trimzero = false, int padleft = 0)
        {
            string v_str = string.Empty;
            string u_str = string.Empty;
            if (double.IsNaN(v) || double.IsInfinity(v) || double.IsNegativeInfinity(v) || double.IsPositiveInfinity(v)) { v = 0; v_str = "0.00"; u_str = "B/s"; }
            else if (v >= VALUE_MB) { v_str = $"{v / factor / VALUE_MB:F2}"; u_str = "MB/s"; }
            else if (v >= VALUE_KB) { v_str = $"{v / factor / VALUE_KB:F2}"; u_str = "KB/s"; }
            else { v_str = $"{v / factor:F2}"; u_str = "B/s"; }
            var vs = trimzero && v != 0 && !u_str.Equals("B/s") ? v_str.TrimEnd('0').TrimEnd('.') : v_str;
            return ((unit ? $"{vs} {u_str}" : vs).PadLeft(padleft));
        }

        public static Func<double, string> SmartFileSizeFunc = (v) => { return(SmartFileSize(v)); };

        public static string SmartFileSize(this long v, double factor = 1, bool unit = true, bool trimzero = true, int padleft = 0) { return (SmartFileSize((double)v, factor, unit, trimzero: trimzero, padleft: padleft)); }

        public static string SmartFileSize(this double v, double factor = 1, bool unit = true, bool trimzero = true, int padleft = 0)
        {
            string v_str = string.Empty;
            string u_str = string.Empty;
            if (double.IsNaN(v) || double.IsInfinity(v) || double.IsNegativeInfinity(v) || double.IsPositiveInfinity(v)) { v = 0; v_str = "0"; u_str = "B"; }
            else if (v >= VALUE_GB) { v_str = $"{v / factor / VALUE_GB:F2}"; u_str = "GB"; }
            else if (v >= VALUE_MB) { v_str = $"{v / factor / VALUE_MB:F2}"; u_str = "MB"; }
            else if (v >= VALUE_KB) { v_str = $"{v / factor / VALUE_KB:F2}"; u_str = "KB"; }
            else { v_str = $"{v / factor:F0}"; u_str = "B"; }
            var vs = trimzero && v != 0 && !u_str.Equals("B") ? v_str.TrimEnd('0').TrimEnd('.') : v_str;
            return ((unit ? $"{vs} {u_str}" : vs).PadLeft(padleft));
        }

        public static string SmartElapsed(this TimeSpan delta, bool msec = true, bool unit = false, bool trimzero = true, int padleft = 0)
        {
            var elapsed = "0";
            if (delta.TotalDays >= 1) elapsed = $"{delta.TotalHours:F0}:{delta.Minutes:00}:{delta.Seconds:00}";
            else if (delta.TotalHours >= 1) elapsed = $"{delta.Hours:00}:{delta.Minutes:00}:{delta.Seconds:00}";
            else if (delta.TotalMinutes >= 1) elapsed = $"{delta.Minutes:00}:{delta.Seconds:00}";
            else if (delta.TotalSeconds >= 1) elapsed = $"{delta.Seconds}";
            var ms_str = msec ? $".{delta.Milliseconds:000}" : string.Empty;
            elapsed = trimzero && msec ? $"{elapsed}{ms_str}".TrimEnd('0').TrimEnd('.') : $"{elapsed}{ms_str}";
            return ((unit ? $"{elapsed} s" : elapsed).PadLeft(padleft));
        }

        public static void DragOut(this DependencyObject sender, PixivItem item, bool original = false)
        {
            try
            {
                if (item.IsWork())
                {
                    var downloaded = new List<string>();
                    string fp = string.Empty;
                    if (item.HasPages())
                    {
                        for (var i = 0; i < item.Count; i++)
                        {
                            if (item.Illust.IsDownloaded(out fp, i, is_meta_single_page: false)) downloaded.Add(fp);
                        }
                    }
                    else if (item.Illust.IsDownloaded(out fp, is_meta_single_page: true)) downloaded.Add(fp);

                    var file = original ? item.Illust.GetOriginalUrl(item.Index).GetImageCacheFile() : item.Illust.GetPreviewUrl(item.Index).GetImageCacheFile();
                    if (File.Exists(file))
                    {
                        var dp = new DataObject();
                        dp.SetFileDropList(new StringCollection() { file });
                        dp.SetData("Text", file);
                        if (downloaded.Count > 0)
                        {
                            var dc = new StringCollection();
                            dc.AddRange(downloaded.ToArray());
                            dp.SetData("Downloaded", dc);
                        }
                        DragDrop.DoDragDrop(sender, dp, DragDropEffects.Copy);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("DragOut"); }
        }

        public static void DragOut(this DependencyObject sender, ImageListGrid gallery, bool original = false)
        {
            try
            {
                DragOut(sender, gallery.GetSelected(), original);
            }
            catch (Exception ex) { ex.ERROR("DragOut"); }
        }

        public static void DragOut(this DependencyObject sender, IEnumerable<PixivItem> items, bool original = false)
        {
            try
            {
                var files = new List<string>();
                var downloaded = new List<string>();
                foreach (var item in items)
                {
                    if (item.IsWork())
                    {
                        var file = original ? item.Illust.GetOriginalUrl(item.Index).GetImageCacheFile() : item.Illust.GetPreviewUrl(item.Index).GetImageCacheFile();
                        if (!string.IsNullOrEmpty(file) && File.Exists(file)) files.Add(file);
                        string fp = string.Empty;
                        if (item.HasPages())
                        {
                            for (var i = 0; i < item.Count; i++)
                            {
                                if (item.Illust.IsDownloaded(out fp, i, is_meta_single_page: false)) downloaded.Add(fp);
                            }
                        }
                        else if (item.Illust.IsDownloaded(out fp, is_meta_single_page: true)) downloaded.Add(fp);
                    }
                }
                if (files.Count > 0)
                {
                    var dp = new DataObject();
                    var sc = new StringCollection();
                    sc.AddRange(files.ToArray());
                    dp.SetFileDropList(sc);
                    dp.SetData("Text", string.Join(Environment.NewLine, files));
                    if (downloaded.Count > 0)
                    {
                        var dc = new StringCollection();
                        dc.AddRange(downloaded.ToArray());
                        dp.SetData("Downloaded", dc);
                    }
                    DragDrop.DoDragDrop(sender, dp, DragDropEffects.Copy);
                }
            }
            catch (Exception ex) { ex.ERROR("DragOut"); }
        }
        #endregion

        #region ImageListGrid page calculating
        internal const int ImagesPerPage = 30;

        public static int CalcPageOffset(this string url)
        {
            int result = 0;
            var offset = Regex.IsMatch(url, @".*?offset=(\d+).*?", RegexOptions.IgnoreCase | RegexOptions.Singleline) ? Regex.Replace(url, @".*?offset=(\d+).*?", "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline) : string.Empty;
            if (!string.IsNullOrEmpty(offset)) int.TryParse(offset, out result);
            return (result);
        }

        public static int CalcPageNum(this int offset)
        {
            return (offset / ImagesPerPage + 1);
        }

        public static int CalcPageNum(this string url)
        {
            var offset = CalcPageOffset(url);
            return (offset / ImagesPerPage + 1);
        }

        public static int CalcTotalPages(this int totals)
        {
            return ((int)Math.Ceiling((double)totals / ImagesPerPage));
        }

        public static int CalcTotalPages(this string totals)
        {
            int total = 0;
            int.TryParse(totals, out total);
            var pages = total <= 0 ? 0 : CalcTotalPages(total);
            return (pages);
        }

        public static string CalcUrlPageHint(this string url, string totals = null)
        {
            string result = "Unknown";
            try
            {
                var offset = CalcPageOffset(url);
                int pages = CalcTotalPages(totals);
                int page = CalcPageNum(offset);
                if (page == 0) page = pages;
                if (pages <= 0) result = $"Page: {page}";
                else result = $"Page: {page} / {pages}";
            }
            catch (Exception ex) { ex.ERROR("CalcUrlPages"); }
            return (result);
        }

        public static string CalcUrlPageHint(this string url, int totals = 0, string current = null)
        {
            string result = "Unknown";
            try
            {
                int offset = CalcPageOffset(url);
                int pages = CalcTotalPages(totals);
                int page = CalcPageNum(offset);
                if (page == 0) page = pages;
                if (pages <= 0) result = $"Page: {page}";
                else result = $"Page: {page} / {pages}";
            }
            catch (Exception ex) { ex.ERROR("CalcUrlPages"); }
            return (result);
        }

        public static int CalcPrevPage(this string url, string totals = null)
        {
            int result = 0;
            try
            {
                int offset = CalcPageOffset(url);
                int pages = CalcTotalPages(totals);
                int page = CalcPageNum(offset);
                result = page <= 2 ? 1 : page - 1;
            }
            catch (Exception ex) { ex.ERROR("CalcPrevPage"); }
            return (result);
        }

        public static int CalcNextPage(this string url, string totals = null, string current = null)
        {
            int result = 0;
            try
            {
                int offset = CalcPageOffset(url);
                int pages = CalcTotalPages(totals);
                int page = CalcPageNum(offset);
                result = page <= 2 ? 1 : page + 1;
            }
            catch (Exception ex) { ex.ERROR("CalcNextPage"); }
            return (result);
        }

        public static string CalcPrevUrl(this string url, string totals, bool is_next_url = false)
        {
            string result = string.Empty;
            try
            {
                int pages = CalcTotalPages(totals);
                result = CalcPrevUrl(url, pages, is_next_url);
            }
            catch (Exception ex) { ex.ERROR("CalcPrevUrl"); }
            return (result);
        }

        public static string CalcNextUrl(this string url, string totals, bool is_next_url = false)
        {
            string result = string.Empty;
            try
            {
                int pages = CalcTotalPages(totals);
                result = CalcNextUrl(url, pages, is_next_url);
            }
            catch (Exception ex) { ex.ERROR("CalcNextUrl"); }
            return (result);
        }

        public static string CalcPrevUrl(this string url, int pages = -1, bool is_next_url = false)
        {
            string result = string.Empty;
            try
            {
                var offset = CalcPageOffset(url);
                var page = CalcPageNum(offset);
                if (pages > 1)
                {
                    var bias = is_next_url ? 2 : 1;
                    var prev_offset = page > bias ? (page - bias - 1) * ImagesPerPage : (pages - 1) * ImagesPerPage;
                    if (prev_offset >= ImagesPerPage) result = Regex.Replace(url, @"offset=(\d+)", m => { return ($"offset={prev_offset}"); }, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                }
            }
            catch (Exception ex) { ex.ERROR("CalcPrevUrl"); }
            return (result);
        }

        public static string CalcNextUrl(this string url, int pages = -1, bool is_next_url = false)
        {
            string result = string.Empty;
            try
            {
                var offset = CalcPageOffset(url);
                var page = CalcPageNum(offset);
                var next_offset = (page + 1) * ImagesPerPage;
                if (next_offset > 0) result = Regex.Replace(url, @"offset=(\d+)", m => { return ($"offset={next_offset}"); }, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            }
            catch (Exception ex) { ex.ERROR("CalcNextUrl"); }
            return (result);
        }

        public static string MakeRelWorkNextUrl(this PixivItem item, int offset = 0)
        {
            var result = string.Empty;
            if (item.HasUser())
            {
                result = $"https://app-api.pixiv.net/v1/user/illusts?user_id={item.UserID}&filter=for_ios&offset={offset}";
            }
            return (result);
        }

        public static string MakeUserWorkNextUrl(this PixivItem item, int offset = 0)
        {
            var result = string.Empty;
            if (item.HasUser())
            {
                result = $"https://app-api.pixiv.net/v1/user/illusts?user_id={item.UserID}&filter=for_ios&offset={offset}";
            }
            return (result);
        }

        public static string MakeUserFavNextUrl(this PixivItem item, int offset = 0)
        {
            var result = string.Empty;
            if (item.HasUser())
            {
                result = $"https://app-api.pixiv.net/v1/user/illusts?user_id={item.UserID}&filter=for_ios&offset={offset}";
            }
            return (result);
        }
        #endregion

        #region Illust Work DateTime routines
        private static TimeZoneInfo TokoyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        public static TimeZoneInfo GetTokyoTimeZone(this Application app)
        {
            return (TokoyTimeZone);
        }
        private static TimeZoneInfo LocalTimeZone = TimeZoneInfo.Local;
        public static TimeZoneInfo GetLocalTimeZone(this Application app)
        {
            return (LocalTimeZone);
        }

        public static DateTime ParseDateTime(this string url)
        {
            var result = default(DateTime);
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

        private static bool IsShellSupported = Microsoft.WindowsAPICodePack.Shell.ShellObject.IsPlatformSupported;
        private static ConcurrentDictionary<string, long> _Touching_ = new ConcurrentDictionary<string, long>();
        private static ConcurrentDictionary<string, long> _Attaching_ = new ConcurrentDictionary<string, long>();
        private static SemaphoreSlim _CanAttaching_ = new SemaphoreSlim(5, 5);

        private static bool IsMetaAttaching(this string file)
        {
            return (_Attaching_ is ConcurrentDictionary<string, long> && _Attaching_.ContainsKey(file));
        }

        private static bool IsMetaAttaching(this FileInfo fileinfo)
        {
            return (_Attaching_ is ConcurrentDictionary<string, long> && _Attaching_.ContainsKey(fileinfo.FullName));
        }

        private static bool IsTouching(this string file)
        {
            return (_Touching_ is ConcurrentDictionary<string, long> && _Touching_.ContainsKey(file));
        }

        private static bool IsTouching(this FileInfo fileinfo)
        {
            return (_Touching_ is ConcurrentDictionary<string, long> && _Touching_.ContainsKey(fileinfo.FullName));
        }
        #endregion

        #region XMP XML Formating Helper
        private static List<string> xmp_ns = new List<string> { "rdf", "xmp", "dc", "exif", "tiff", "iptc", "MicrosoftPhoto" };
        private static Dictionary<string, string> xmp_ns_lookup = new Dictionary<string, string>()
        {
            {"rdf", "http://www.w3.org/1999/02/22-rdf-syntax-ns#" },
            {"xmp", "http://ns.adobe.com/xap/1.0/" },
            {"dc", "http://purl.org/dc/elements/1.1/" },
            //{"iptc", "http://ns.adobe.com/iptc/1.0/" },
            {"exif", "http://ns.adobe.com/exif/1.0/" },
            {"tiff", "http://ns.adobe.com/tiff/1.0/" },
            {"photoshop", "http://ns.adobe.com/photoshop/1.0/" },
            {"MicrosoftPhoto", "http://ns.microsoft.com/photo/1.0" },
            //{"MicrosoftPhoto", "http://ns.microsoft.com/photo/1.0/" },
            //{"MicrosoftPhoto", "http://ns.microsoft.com/photo/1.2/" },
        };

        #region below tags will be touching
        private static string[] tag_date = new string[] {
            "exif:DateTimeDigitized",
            "exif:DateTimeOriginal",
            "exif:DateTime",
            "MicrosoftPhoto:DateAcquired",
            "MicrosoftPhoto:DateTaken",
            //"png:tIME",
            "xmp:CreateDate",
            "xmp:ModifyDate",
            "xmp:DateTimeDigitized",
            "xmp:DateTimeOriginal",
            "Creation Time",
            "create-date",
            "modify-date",
            "tiff:DateTime",
            //"date:modify",
            //"date:create",
        };
        private static string[] tag_author = new string[] {
            "exif:Artist",
            "exif:WinXP-Author",
            "tiff:artist",
        };
        private static string[] tag_copyright = new string[] {
            "exif:Copyright",
            "tiff:copyright",
            //"iptc:CopyrightNotice",
        };
        private static string[] tag_title = new string[] {
            "exif:ImageDescription",
            "exif:WinXP-Title",
        };
        private static string[] tag_subject = new string[] {
            "exif:WinXP-Subject",
        };
        private static string[] tag_comments = new string[] {
            "exif:WinXP-Comments",
            "exif:UserComment"
        };
        private static string[] tag_keywords = new string[] {
            "exif:WinXP-Keywords",
            //"iptc:Keywords",
            "dc:Subject",
        };
        private static string[] tag_rating = new string[] {
            "Rating",
            "RatingPercent",
            "MicrosoftPhoto:Rating",
            "xmp:Rating",
        };
        private static string[] tag_software = new string[] {
            "Software"
        };
        #endregion

        private static string FormatXML(string xml)
        {
            var result = xml;
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xml);
                result = FormatXML(doc);
            }
            catch (Exception ex) { ex.Message.ERROR("FormatXML"); }
            return (result);
        }

        private static string FormatXML(XmlDocument xml)
        {
            var result = xml.OuterXml;
            using (var ms = new MemoryStream())
            {
                var writer = new XmlTextWriter(ms, Encoding.UTF8);
                writer.Formatting = System.Xml.Formatting.Indented;
                xml.WriteContentTo(writer);
                writer.Flush();
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);
                using (var sr = new StreamReader(ms)) { result = sr.ReadToEnd(); }
                result = result.Replace("\"", "'");
                foreach (var ns in xmp_ns) { result = result.Replace($" xmlns:{ns}='{ns}'", ""); }
            }
            return (result);
        }

        private static string FormatXML(XmlNode xml)
        {
            var result = xml.OuterXml;
            using (var ms = new MemoryStream())
            {
                var writer = new XmlTextWriter(ms, Encoding.UTF8);
                writer.Formatting = System.Xml.Formatting.Indented;
                xml.WriteTo(writer);
                writer.Flush();
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);
                using (var sr = new StreamReader(ms)) { result = sr.ReadToEnd(); }
            }
            return (result);
        }

        private static string FormatXML(XmlElement xml)
        {
            var result = xml.OuterXml;
            using (var ms = new MemoryStream())
            {
                var writer = new XmlTextWriter(ms, Encoding.UTF8);
                writer.Formatting = System.Xml.Formatting.Indented;
                xml.WriteTo(writer);
                writer.Flush();
                ms.Flush();
                ms.Seek(0, SeekOrigin.Begin);
                using (var sr = new StreamReader(ms)) { result = sr.ReadToEnd(); }
            }
            return (result);
        }

        private static string FormatXML(XmlDocument xml, bool merge_nodes)
        {
            var result = FormatXML(xml);
            if (merge_nodes && xml is XmlDocument)
            {
                foreach (XmlElement root in xml.DocumentElement.ChildNodes)
                {
                    var elements_list = new Dictionary<string, XmlElement>();
                    Func<XmlElement, IList<XmlElement>> ChildList = (elements)=>
                    {
                        var list = new List<XmlElement>();
                        foreach(XmlElement node in elements.ChildNodes) list.Add(node);
                        return(list);
                    };
                    foreach (XmlElement node in ChildList.Invoke(root))
                    {
                        foreach (XmlAttribute attr in node.Attributes)
                        {
                            if (attr.Name.StartsWith("xmlns:"))
                            {
                                var key = attr.Name.Substring(6);
                                if (xmp_ns_lookup.ContainsKey(key))
                                {
                                    if (!elements_list.ContainsKey(key) || elements_list[key] == null)
                                    {
                                        elements_list[key] = xml.CreateElement("rdf:Description", "rdf");
                                        elements_list[key].SetAttribute($"xmlns:{key}", xmp_ns_lookup[key]);
                                    }
                                }
                                else
                                {
                                    if (!elements_list.ContainsKey(key) || elements_list[key] == null)
                                    {
                                        elements_list[key] = xml.CreateElement("rdf:Description", "rdf");
                                        elements_list[key].SetAttribute($"xmlns:{key}", attr.Value);
                                    }
                                }
                                if (!xmp_ns.Contains(key)) xmp_ns.Append(key);
                            }
                            else
                            {
                                var keys = attr.Name.Split(':');
                                var key = keys[0];
                                var xmlns = $"xmlns:{key}";
                                if (node.HasAttribute(xmlns))
                                {
                                    var value = node.GetAttribute(xmlns);
                                    if (!elements_list.ContainsKey(key) || elements_list[key] == null)
                                    {
                                        elements_list[key] = xml.CreateElement("rdf:Description", "rdf");
                                        elements_list[key].SetAttribute($"xmlns:{key}", value);
                                    }
                                    if (!xmp_ns.Contains(key)) xmp_ns.Append(key);
                                    var child = xml.CreateElement(attr.Name, key);
                                    child.InnerText = attr.Value;
                                    elements_list[key].AppendChild(child);
                                }
                            }
                        }

                        try
                        {
                            foreach (XmlElement item in ChildList.Invoke(node))
                            {
                                var xmlns = $"xmlns:{item.Prefix}";
                                var ns = item.NamespaceURI;
                                if (item.HasAttribute(xmlns))
                                {
                                    if (!xmp_ns_lookup.ContainsKey(item.Prefix)) { xmp_ns_lookup.Add(item.Prefix, item.GetAttribute(xmlns)); }
                                    if (!elements_list.ContainsKey(item.Prefix) || elements_list[item.Prefix] == null)
                                    {
                                        elements_list[item.Prefix] = xml.CreateElement("rdf:Description", "rdf");
                                        elements_list[item.Prefix].SetAttribute($"xmlns:{item.Prefix}", xmp_ns_lookup[item.Prefix]);
                                    }
                                    item.RemoveAttribute(xmlns);
                                }
                                else
                                {
                                    if (!xmp_ns_lookup.ContainsKey(item.Prefix)) { xmp_ns_lookup.Add(item.Prefix, ns); }
                                    if (!elements_list.ContainsKey(item.Prefix) || elements_list[item.Prefix] == null)
                                    {
                                        elements_list[item.Prefix] = xml.CreateElement("rdf:Description", "rdf");
                                        elements_list[item.Prefix].SetAttribute($"xmlns:{item.Prefix}", xmp_ns_lookup[item.Prefix]);
                                    }
                                }
                                elements_list[item.Prefix].AppendChild(item);
                            }
                            root.RemoveChild(node);
                        }
                        catch (Exception ex) { ex.Message.ERROR("FormatXML"); }
                    }
                    foreach (var kv in elements_list) { if (kv.Value is XmlElement && kv.Value.HasChildNodes) root.AppendChild(kv.Value); }
                }
                result = FormatXML(xml);
            }
            return (result);
        }

        private static string FormatXML(string xml, bool merge_nodes)
        {
            var result = xml;
            if (!string.IsNullOrEmpty(xml))
            {
                XmlDocument xml_doc = new XmlDocument();
                xml_doc.LoadXml(xml);
                result = FormatXML(xml_doc, merge_nodes);
            }
            return (result);
        }

        private static string TouchXMP(FileInfo fi, string xml, MetaInfo meta)
        {
            if (meta is MetaInfo)
            {
                var title = meta is MetaInfo ? meta.Title ?? Path.GetFileNameWithoutExtension(fi.Name) : Path.GetFileNameWithoutExtension(fi.Name);
                var subject = meta is MetaInfo ? meta.Subject : title;
                var authors = meta is MetaInfo ? meta.Authors : string.Empty;
                var copyright = meta is MetaInfo ? meta.Copyrights : authors;
                var keywords = meta is MetaInfo ? meta.Keywords : string.Empty;
                var comment = meta is MetaInfo ? meta.Comment : string.Empty;
                var rating = meta is MetaInfo ? meta.Rating : null;
                if (!string.IsNullOrEmpty(title)) title.Replace("\0", string.Empty).TrimEnd('\0');
                if (!string.IsNullOrEmpty(subject)) subject.Replace("\0", string.Empty).TrimEnd('\0');
                if (!string.IsNullOrEmpty(authors)) authors.Replace("\0", string.Empty).TrimEnd('\0');
                if (!string.IsNullOrEmpty(copyright)) copyright.Replace("\0", string.Empty).TrimEnd('\0');
                if (!string.IsNullOrEmpty(keywords)) keywords.Replace("\0", string.Empty).TrimEnd('\0');
                if (!string.IsNullOrEmpty(comment)) comment.Replace("\0", string.Empty).TrimEnd('\0');

                var dc = (meta is MetaInfo ? meta.DateCreated ?? meta.DateAcquired ?? meta.DateTaken : null) ?? fi.CreationTime;
                var dm = (meta is MetaInfo ? meta.DateModified ?? meta.DateAcquired ?? meta.DateTaken : null) ?? fi.LastWriteTime;
                var da = (meta is MetaInfo ? meta.DateAccesed ?? meta.DateAcquired ?? meta.DateTaken : null) ?? fi.LastAccessTime;

                // 2021:09:13 11:00:16
                var dc_exif = dc.ToString("yyyy:MM:dd HH:mm:ss");
                var dm_exif = dm.ToString("yyyy:MM:dd HH:mm:ss");
                var da_exif = da.ToString("yyyy:MM:dd HH:mm:ss");
                // 2021:09:13T11:00:16
                var dc_xmp = dc.ToString("yyyy:MM:dd HH:mm:ss");
                var dm_xmp = dm.ToString("yyyy:MM:dd HH:mm:ss");
                var da_xmp = da.ToString("yyyy:MM:dd HH:mm:ss");
                // 2021-09-13T06:38:49+00:00
                var dc_date = dc.ToString("yyyy-MM-ddTHH:mm:sszzz");
                var dm_date = dm.ToString("yyyy-MM-ddTHH:mm:sszzz");
                var da_date = da.ToString("yyyy-MM-ddTHH:mm:sszzz");
                // 2021-08-26T12:23:49
                var dc_ms = dc.ToString("yyyy-MM-ddTHH:mm:sszzz");
                var dm_ms = dm.ToString("yyyy-MM-ddTHH:mm:sszzz");
                var da_ms = da.ToString("yyyy-MM-ddTHH:mm:sszzz");
                // 2021-08-26T12:23:49.002
                var dc_msxmp = dc.ToString("yyyy-MM-ddTHH:mm:ss.fff");
                var dm_msxmp = dm.ToString("yyyy-MM-ddTHH:mm:ss.fff");
                var da_msxmp = da.ToString("yyyy-MM-ddTHH:mm:ss.fff");
                // 2021-09-13T08:38:13Z
                var dc_png = dc.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var dm_png = dm.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var da_png = da.ToString("yyyy-MM-ddTHH:mm:ssZ");
                // 2021:09:13 11:00:16+08:00
                var dc_misc = dc.ToString("yyyy:MM:dd HH:mm:sszzz");
                var dm_misc = dm.ToString("yyyy:MM:dd HH:mm:sszzz");
                var da_misc = da.ToString("yyyy:MM:dd HH:mm:sszzz");

                var keyword_list = string.IsNullOrEmpty(keywords) ? new List<string>() : keywords.Split(new char[] { ';', '#' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).Where(k => !string.IsNullOrEmpty(k)).Distinct();
                keywords = string.Join("; ", keyword_list);

                #region Init a XMP contents
                if (string.IsNullOrEmpty(xml))
                {
                    //var xml = $"<?xpacket begin='?' id='W5M0MpCehiHzreSzNTczkc9d'?>{Environment.NewLine}<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"uuid:faf5bdd5-ba3d-11da-ad31-d33d75182f1b\" xmlns:MicrosoftPhoto=\"http://ns.microsoft.com/photo/1.0/\"><MicrosoftPhoto:DateAcquired>{dm_msxmp}</MicrosoftPhoto:DateAcquired></rdf:Description></rdf:RDF></x:xmpmeta>{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}                            <?xpacket end='w'?>";
                    //var xml = $"<?xpacket begin='?' id='W5M0MpCehiHzreSzNTczkc9d'?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"uuid:faf5bdd5-ba3d-11da-ad31-d33d75182f1b\" xmlns:MicrosoftPhoto=\"http://ns.microsoft.com/photo/1.0/\"><MicrosoftPhoto:DateAcquired>{dm_msxmp}</MicrosoftPhoto:DateAcquired><MicrosoftPhoto:DateTaken>{dm_msxmp}</MicrosoftPhoto:DateTaken></rdf:Description><rdf:Description about='' xmlns:exif='http://ns.adobe.com/exif/1.0/'><exif:DateTimeDigitized>{dm_ms}</exif:DateTimeDigitized><exif:DateTimeOriginal>{dm_ms}</exif:DateTimeOriginal></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end='w'?>";

                    xml = $"<?xpacket begin='?' id='W5M0MpCehiHzreSzNTczkc9d'?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"></rdf:RDF></x:xmpmeta><?xpacket end='w'?>";
                }
                #endregion
                try
                {
                    var xml_doc = new XmlDocument();
                    xml_doc.LoadXml(xml);
                    var root_nodes = xml_doc.GetElementsByTagName("rdf:RDF");
                    if (root_nodes.Count >= 1)
                    {
                        var root_node = root_nodes.Item(0);
                        #region Title node
                        if (xml_doc.GetElementsByTagName("dc:title").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("xmlns:dc", xmp_ns_lookup["dc"]);
                            desc.AppendChild(xml_doc.CreateElement("dc:title", "dc"));
                            root_node.AppendChild(desc);
                        }
                        #endregion
                        #region Comment node
                        if (xml_doc.GetElementsByTagName("dc:description").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("xmlns:dc", xmp_ns_lookup["dc"]);
                            desc.AppendChild(xml_doc.CreateElement("dc:description", "dc"));
                            root_node.AppendChild(desc);
                        }
                        #endregion
                        #region Author node
                        if (xml_doc.GetElementsByTagName("dc:creator").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("xmlns:dc", xmp_ns_lookup["dc"]);
                            desc.AppendChild(xml_doc.CreateElement("dc:creator", "dc"));
                            root_node.AppendChild(desc);
                        }
                        if (xml_doc.GetElementsByTagName("xmp:creator").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("xmlns:xmp", xmp_ns_lookup["xmp"]);
                            desc.AppendChild(xml_doc.CreateElement("xmp:creator", "xmp"));
                            root_node.AppendChild(desc);
                        }
                        #endregion
                        #region Keywords node
                        if (xml_doc.GetElementsByTagName("dc:subject").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("xmlns:dc", xmp_ns_lookup["dc"]);
                            desc.AppendChild(xml_doc.CreateElement("dc:subject", "dc"));
                            root_node.AppendChild(desc);
                        }
                        if (xml_doc.GetElementsByTagName("MicrosoftPhoto:LastKeywordXMP").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("xmlns:MicrosoftPhoto", xmp_ns_lookup["MicrosoftPhoto"]);
                            desc.AppendChild(xml_doc.CreateElement("MicrosoftPhoto:LastKeywordXMP", "MicrosoftPhoto"));
                            root_node.AppendChild(desc);
                        }
                        if (xml_doc.GetElementsByTagName("MicrosoftPhoto:LastKeywordIPTC").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("xmlns:MicrosoftPhoto", xmp_ns_lookup["MicrosoftPhoto"]);
                            desc.AppendChild(xml_doc.CreateElement("MicrosoftPhoto:LastKeywordIPTC", "MicrosoftPhoto"));
                            root_node.AppendChild(desc);
                        }
                        if (xml_doc.GetElementsByTagName("MicrosoftPhoto:LastKeywordIPTC_TIFF_IRB").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("xmlns:MicrosoftPhoto", xmp_ns_lookup["MicrosoftPhoto"]);
                            desc.AppendChild(xml_doc.CreateElement("MicrosoftPhoto:LastKeywordIPTC_TIFF_IRB", "MicrosoftPhoto"));
                            root_node.AppendChild(desc);
                        }
                        #endregion
                        #region Copyright node
                        if (xml_doc.GetElementsByTagName("dc:rights").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("xmlns:dc", xmp_ns_lookup["dc"]);
                            desc.AppendChild(xml_doc.CreateElement("dc:rights", "dc"));
                            root_node.AppendChild(desc);
                        }
                        #endregion
                        #region CreateTime node
                        if (xml_doc.GetElementsByTagName("xmp:CreateDate").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("rdf:about", "uuid:faf5bdd5-ba3d-11da-ad31-d33d75182f1b");
                            desc.SetAttribute("xmlns:xmp", xmp_ns_lookup["xmp"]);
                            desc.AppendChild(xml_doc.CreateElement("xmp:CreateDate", "xmp"));
                            root_node.AppendChild(desc);
                        }
                        #endregion
                        #region ModifyDate node
                        if (xml_doc.GetElementsByTagName("xmp:ModifyDate").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("rdf:about", "uuid:faf5bdd5-ba3d-11da-ad31-d33d75182f1b");
                            desc.SetAttribute("xmlns:xmp", xmp_ns_lookup["xmp"]);
                            desc.AppendChild(xml_doc.CreateElement("xmp:ModifyDate", "xmp"));
                            root_node.AppendChild(desc);
                        }
                        #endregion
                        #region DateTimeOriginal node
                        if (xml_doc.GetElementsByTagName("xmp:DateTimeOriginal").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("rdf:about", "uuid:faf5bdd5-ba3d-11da-ad31-d33d75182f1b");
                            desc.SetAttribute("xmlns:xmp", xmp_ns_lookup["xmp"]);
                            desc.AppendChild(xml_doc.CreateElement("xmp:DateTimeOriginal", "xmp"));
                            root_node.AppendChild(desc);
                        }
                        #endregion
                        #region DateTimeDigitized node
                        if (xml_doc.GetElementsByTagName("xmp:DateTimeDigitized").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("rdf:about", "uuid:faf5bdd5-ba3d-11da-ad31-d33d75182f1b");
                            desc.SetAttribute("xmlns:xmp", xmp_ns_lookup["xmp"]);
                            desc.AppendChild(xml_doc.CreateElement("xmp:DateTimeDigitized", "xmp"));
                            root_node.AppendChild(desc);
                        }
                        #endregion
                        #region Ranking/Rating node
                        if (xml_doc.GetElementsByTagName("xmp:Rating").Count <= 0 && rating > 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("rdf:about", "uuid:faf5bdd5-ba3d-11da-ad31-d33d75182f1b");
                            desc.SetAttribute("xmlns:xmp", xmp_ns_lookup["xmp"]);
                            desc.AppendChild(xml_doc.CreateElement("xmp:Rating", "xmp"));
                            root_node.AppendChild(desc);
                        }
                        if (xml_doc.GetElementsByTagName("MicrosoftPhoto:Rating").Count <= 0 && rating > 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("rdf:about", "uuid:faf5bdd5-ba3d-11da-ad31-d33d75182f1b");
                            desc.SetAttribute("xmlns:MicrosoftPhoto", xmp_ns_lookup["MicrosoftPhoto"]);
                            desc.AppendChild(xml_doc.CreateElement("MicrosoftPhoto:Rating", "MicrosoftPhoto"));
                            root_node.AppendChild(desc);
                        }
                        #endregion
                        #region EXIF DateTime node
                        if (xml_doc.GetElementsByTagName("exif:DateTimeDigitized").Count <= 0)
                        {
                            if (xml_doc.GetElementsByTagName("exif:DateTimeOriginal").Count > 0)
                            {
                                var node_msdt = xml_doc.GetElementsByTagName("exif:DateTimeOriginal").Item(0);
                                node_msdt.ParentNode.AppendChild(xml_doc.CreateElement("exif:DateTimeDigitized", "exif"));
                            }
                            else
                            {
                                var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                                desc.SetAttribute("rdf:about", "");
                                desc.SetAttribute("xmlns:exif", xmp_ns_lookup["exif"]);
                                desc.AppendChild(xml_doc.CreateElement("exif:DateTimeDigitized", "exif"));
                                root_node.AppendChild(desc);
                            }
                        }
                        if (xml_doc.GetElementsByTagName("exif:DateTimeOriginal").Count <= 0)
                        {
                            if (xml_doc.GetElementsByTagName("exif:DateTimeDigitized").Count > 0)
                            {
                                var node_msdt = xml_doc.GetElementsByTagName("exif:DateTimeDigitized").Item(0);
                                node_msdt.ParentNode.AppendChild(xml_doc.CreateElement("exif:DateTimeOriginal", "exif"));
                            }
                            else
                            {
                                var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                                desc.SetAttribute("rdf:about", "");
                                desc.SetAttribute("xmlns:exif", xmp_ns_lookup["exif"]);
                                desc.AppendChild(xml_doc.CreateElement("exif:DateTimeOriginal", "exif"));
                                root_node.AppendChild(desc);
                            }
                        }
                        #endregion
                        #region TIFF DateTime node
                        if (xml_doc.GetElementsByTagName("tiff:DateTime").Count <= 0)
                        {
                            var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                            desc.SetAttribute("rdf:about", "");
                            desc.SetAttribute("xmlns:tiff", xmp_ns_lookup["tiff"]);
                            desc.AppendChild(xml_doc.CreateElement("tiff:DateTime", "tiff"));
                            root_node.AppendChild(desc);
                        }
                        #endregion
                        #region MicrosoftPhoto DateTime node
                        if (xml_doc.GetElementsByTagName("MicrosoftPhoto:DateAcquired").Count <= 0)
                        {
                            if (xml_doc.GetElementsByTagName("MicrosoftPhoto:DateTaken").Count > 0)
                            {
                                var node_msdt = xml_doc.GetElementsByTagName("MicrosoftPhoto:DateTaken").Item(0);
                                node_msdt.ParentNode.AppendChild(xml_doc.CreateElement("MicrosoftPhoto:DateAcquired", "MicrosoftPhoto"));
                            }
                            else
                            {
                                var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                                desc.SetAttribute("rdf:about", "uuid:faf5bdd5-ba3d-11da-ad31-d33d75182f1b");
                                desc.SetAttribute("xmlns:MicrosoftPhoto", xmp_ns_lookup["MicrosoftPhoto"]);
                                desc.AppendChild(xml_doc.CreateElement("MicrosoftPhoto:DateAcquired", "MicrosoftPhoto"));
                                root_node.AppendChild(desc);
                            }
                        }
                        if (xml_doc.GetElementsByTagName("MicrosoftPhoto:DateTaken").Count <= 0)
                        {
                            if (xml_doc.GetElementsByTagName("MicrosoftPhoto:DateAcquired").Count > 0)
                            {
                                var node_msdt = xml_doc.GetElementsByTagName("MicrosoftPhoto:DateAcquired").Item(0);
                                node_msdt.ParentNode.AppendChild(xml_doc.CreateElement("MicrosoftPhoto:DateTaken", "MicrosoftPhoto"));
                            }
                            else
                            {
                                var desc = xml_doc.CreateElement("rdf:Description", "rdf");
                                desc.SetAttribute("rdf:about", "uuid:faf5bdd5-ba3d-11da-ad31-d33d75182f1b");
                                desc.SetAttribute("xmlns:MicrosoftPhoto", xmp_ns_lookup["MicrosoftPhoto"]);
                                desc.AppendChild(xml_doc.CreateElement("MicrosoftPhoto:DateTaken", "MicrosoftPhoto"));
                                root_node.AppendChild(desc);
                            }
                        }
                        #endregion

                        #region Remove duplicate node
                        var all_elements = new List<string>()
                        {
                            "dc:title",
                            "dc:description",
                            "dc:creator", "xmp:creator",
                            "dc:subject", "MicrosoftPhoto:LastKeywordXMP", "MicrosoftPhoto:LastKeywordIPTC", "MicrosoftPhoto:LastKeywordIPTC_TIFF_IRB",
                            "dc:rights",
                            "xmp:CreateDate", "xmp:ModifyDate", "xmp:DateTimeOriginal", "xmp:DateTimeDigitized", "xmp:MetadataDate",
                            "xmp:Rating", "MicrosoftPhoto:Rating",
                            "exif:DateTimeDigitized", "exif:DateTimeOriginal",
                            "tiff:DateTime",
                            "MicrosoftPhoto:DateAcquired", "MicrosoftPhoto:DateTaken",
                            "xmp:CreatorTool",
                        };
                        all_elements.AddRange(tag_author);
                        all_elements.AddRange(tag_comments);
                        all_elements.AddRange(tag_copyright);
                        all_elements.AddRange(tag_date);
                        all_elements.AddRange(tag_keywords);
                        all_elements.AddRange(tag_rating);
                        all_elements.AddRange(tag_subject);
                        all_elements.AddRange(tag_title);
                        all_elements.AddRange(tag_software);
                        foreach (var element in all_elements.Distinct())
                        {
                            var nodes = xml_doc.GetElementsByTagName(element);
                            if (nodes.Count > 1)
                            {
                                for (var i = 1; i < nodes.Count; i++)
                                {
                                    nodes[i].ParentNode.RemoveChild(nodes[i]);
                                }
                            }
                        }
                        #endregion

                        #region xml nodes updating
                        var rdf_attr = "xmlns:rdf";
                        var rdf_value = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
                        Action<XmlElement, dynamic> add_rdf_li = new Action<XmlElement, dynamic>((element, text)=>
                        {
                            if (text is string && !string.IsNullOrEmpty(text as string))
                            {
                                var items = (text as string).Split(new string[] { ";", "#" }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).Where(k => !string.IsNullOrEmpty(k)).Distinct();
                                foreach (var item in items)
                                {
                                    var node_author_li = xml_doc.CreateElement("rdf:li", "rdf");
                                    node_author_li.InnerText = item;
                                    element.AppendChild(node_author_li);
                                }
                            }
                            else if(text is IEnumerable<string> && (text as IEnumerable<string>).Count() > 0)
                            {
                                foreach (var item in (text as IEnumerable<string>))
                                {
                                    var node_author_li = xml_doc.CreateElement("rdf:li", "rdf");
                                    node_author_li.InnerText = item;
                                    element.AppendChild(node_author_li);
                                }
                            }
                        });
                        foreach (XmlNode node in xml_doc.GetElementsByTagName("rdf:Description"))
                        {
                            var nodes = new List<XmlNode>();
                            foreach (XmlNode child in node.ChildNodes)
                            {
                                if (child.Name.Equals("dc:title", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    child.RemoveAll();
                                    var node_title = xml_doc.CreateElement("rdf:Alt", "rdf");
                                    var node_title_li = xml_doc.CreateElement("rdf:li", "rdf");
                                    node_title_li.SetAttribute("xml:lang", "x-default");
                                    node_title_li.InnerText = title;
                                    node_title.AppendChild(node_title_li);
                                    child.AppendChild(node_title);
                                }
                                else if (child.Name.Equals("dc:description", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    child.RemoveAll();
                                    var node_comment = xml_doc.CreateElement("rdf:Alt", "rdf");
                                    var node_comment_li = xml_doc.CreateElement("rdf:li", "rdf");
                                    node_comment_li.SetAttribute("xml:lang", "x-default");
                                    node_comment_li.InnerText = comment;
                                    node_comment.AppendChild(node_comment_li);
                                    child.AppendChild(node_comment);
                                }
                                else if (child.Name.Equals("xmp:creator", StringComparison.CurrentCultureIgnoreCase) ||
                                    child.Name.Equals("dc:creator", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    child.RemoveAll();
                                    var node_author = xml_doc.CreateElement("rdf:Seq", "rdf");
                                    node_author.SetAttribute(rdf_attr, rdf_value);
                                    add_rdf_li.Invoke(node_author, authors);
                                    child.AppendChild(node_author);
                                }
                                else if (child.Name.Equals("dc:rights", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    child.RemoveAll();
                                    var node_rights = xml_doc.CreateElement("rdf:Bag", "rdf");
                                    node_rights.SetAttribute(rdf_attr, rdf_value);
                                    add_rdf_li.Invoke(node_rights, copyright);
                                    child.AppendChild(node_rights);
                                }
                                else if (child.Name.Equals("dc:subject", StringComparison.CurrentCultureIgnoreCase) ||
                                    child.Name.StartsWith("MicrosoftPhoto:LastKeyword", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    child.RemoveAll();
                                    var node_subject = xml_doc.CreateElement("rdf:Bag", "rdf");
                                    node_subject.SetAttribute(rdf_attr, rdf_value);
                                    add_rdf_li.Invoke(node_subject, keyword_list);
                                    child.AppendChild(node_subject);
                                }
                                else if (child.Name.Equals("MicrosoftPhoto:Rating", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    child.InnerText = $"{rating}";
                                    //if (rating > 0) child.InnerText = $"{rating}";
                                    //else child.ParentNode.RemoveChild(child);
                                }
                                else if (child.Name.Equals("xmp:Rating", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    var rating_level = 0;
                                    if (rating >= 99) rating_level = 5;
                                    else if (rating >= 75) rating_level = 4;
                                    else if (rating >= 50) rating_level = 3;
                                    else if (rating >= 25) rating_level = 2;
                                    else if (rating >= 01) rating_level = 1;
                                    child.InnerText = $"{rating_level}";
                                    //if (rating_level > 0) child.InnerText = $"{rating_level}";
                                    //else child.ParentNode.RemoveChild(child);
                                }
                                else if (child.Name.Equals("xmp:CreateDate", StringComparison.CurrentCultureIgnoreCase))
                                    child.InnerText = dc_xmp;
                                else if (child.Name.Equals("xmp:ModifyDate", StringComparison.CurrentCultureIgnoreCase))
                                    child.InnerText = dm_xmp;
                                else if (child.Name.Equals("xmp:DateTimeOriginal", StringComparison.CurrentCultureIgnoreCase))
                                    child.InnerText = dm_date;
                                else if (child.Name.Equals("xmp:DateTimeDigitized", StringComparison.CurrentCultureIgnoreCase))
                                    child.InnerText = dm_date;
                                else if (child.Name.Equals("MicrosoftPhoto:DateAcquired", StringComparison.CurrentCultureIgnoreCase))
                                    child.InnerText = dm_msxmp;
                                else if (child.Name.Equals("MicrosoftPhoto:DateTaken", StringComparison.CurrentCultureIgnoreCase))
                                    child.InnerText = dm_msxmp;
                                else if (child.Name.Equals("exif:DateTimeDigitized", StringComparison.CurrentCultureIgnoreCase))
                                    child.InnerText = dm_ms;
                                else if (child.Name.Equals("exif:DateTimeOriginal", StringComparison.CurrentCultureIgnoreCase))
                                    child.InnerText = dm_ms;
                                else if (child.Name.Equals("tiff:DateTime", StringComparison.CurrentCultureIgnoreCase))
                                    child.InnerText = dm_ms;

                                if (tag_date.Contains(node.Name, StringComparer.CurrentCultureIgnoreCase))
                                {
                                    if (nodes.Count(n => n.Name.Equals(child.Name, StringComparison.CurrentCultureIgnoreCase)) > 0) node.RemoveChild(child);
                                    else nodes.Add(child);
                                }
                            }
                            nodes.Clear();
                        }
                        #endregion
                        #region pretty xml
                        xml = FormatXML(xml_doc, true);
                        #endregion
                    }
                }
                catch
                {
                    #region Title
                    var pattern_title = @"(<dc:title>.*?<rdf:li.*?xml:lang='.*?')(>).*?(</rdf:li></rdf:Alt></dc:title>)";
                    if (Regex.IsMatch(xml, pattern_title, RegexOptions.IgnoreCase | RegexOptions.Multiline))
                    {
                        xml = Regex.Replace(xml, pattern_title, $"$1$2{title}$3", RegexOptions.IgnoreCase | RegexOptions.Multiline);
                        xml = xml.Replace("$2", ">");
                    }
                    else
                    {
                        var title_xml = $"<rdf:Description rdf:about='' xmlns:dc='http://purl.org/dc/elements/1.1/'><dc:title><rdf:Alt><rdf:li xml:lang='x-default'>{title}</rdf:li></rdf:Alt></dc:title></rdf:Description>";
                        xml = Regex.Replace(xml, @"(</rdf:RDF>.*?</x:xmpmeta>)", $"{title_xml}$1", RegexOptions.IgnoreCase);
                    }
                    #endregion
                    #region MS Photo DateAcquired
                    var pattern_ms_da = @"(<MicrosoftPhoto:DateAcquired>).*?(</MicrosoftPhoto:DateAcquired>)";
                    if (Regex.IsMatch(xml, pattern_ms_da, RegexOptions.IgnoreCase))
                    {
                        xml = Regex.Replace(xml, pattern_ms_da, $"$1{dm_msxmp}$2", RegexOptions.IgnoreCase);
                        xml = xml.Replace("$1", "<MicrosoftPhoto:DateAcquired>");
                    }
                    else
                    {
                        var msda_xml = $"<rdf:Description rdf:about=\"uuid:faf5bdd5-ba3d-11da-ad31-d33d75182f1b\" xmlns:MicrosoftPhoto=\"http://ns.microsoft.com/photo/1.0/\"><MicrosoftPhoto:DateAcquired>{dm_msxmp}</MicrosoftPhoto:DateAcquired></rdf:Description>";
                        xml = Regex.Replace(xml, @"(</rdf:RDF></x:xmpmeta>)", $"{msda_xml}$1", RegexOptions.IgnoreCase);
                    }
                    #endregion
                    #region MS Photo DateTaken
                    var pattern_ms_dt = @"(<MicrosoftPhoto:DateTaken>).*?(</MicrosoftPhoto:DateTaken>)";
                    if (Regex.IsMatch(xml, pattern_ms_dt, RegexOptions.IgnoreCase))
                    {
                        xml = Regex.Replace(xml, pattern_ms_dt, $"$1{dm_msxmp}$2", RegexOptions.IgnoreCase);
                        xml = xml.Replace("$1", "<MicrosoftPhoto:DateTaken>");
                    }
                    else
                    {
                        var msdt_xml = $"<rdf:Description rdf:about=\"uuid:faf5bdd5-ba3d-11da-ad31-d33d75182f1b\" xmlns:MicrosoftPhoto=\"http://ns.microsoft.com/photo/1.0/\"><MicrosoftPhoto:DateTaken>{dm_msxmp}</MicrosoftPhoto:DateTaken></rdf:Description>";
                        xml = Regex.Replace(xml, @"(</rdf:RDF></x:xmpmeta>)", $"{msdt_xml}$1", RegexOptions.IgnoreCase);
                    }
                    #endregion
                    #region tiff:DateTime
                    var pattern_tiff_dt = @"(<tiff:DateTime>).*?(</tiff:DateTime>)";
                    if (Regex.IsMatch(xml, pattern_tiff_dt, RegexOptions.IgnoreCase))
                    {
                        xml = Regex.Replace(xml, pattern_tiff_dt, $"$1{dm_ms}$2", RegexOptions.IgnoreCase);
                        xml = xml.Replace("$1", "<tiff:DateTime>");
                    }
                    else
                    {
                        var tiffdt_xml = $"<rdf:Description rdf:about='' xmlns:tiff='http://ns.adobe.com/tiff/1.0/'><tiff:DateTime>{dm_ms}</tiff:DateTime></rdf:Description>";
                        xml = Regex.Replace(xml, @"(</rdf:RDF></x:xmpmeta>)", $"{tiffdt_xml}$1", RegexOptions.IgnoreCase);
                    }
                    #endregion
                    #region exif:DateTimeDigitized
                    var pattern_exif_dd = @"(<exif:DateTimeDigitized>).*?(</exif:DateTimeDigitized>)";
                    if (Regex.IsMatch(xml, pattern_exif_dd, RegexOptions.IgnoreCase))
                    {
                        xml = Regex.Replace(xml, pattern_exif_dd, $"$1{dm_ms}$2", RegexOptions.IgnoreCase);
                        xml = xml.Replace("$1", "<exif:DateTimeDigitized>");
                    }
                    else
                    {
                        var exifdo_xml = $"<rdf:Description rdf:about='' xmlns:exif='http://ns.adobe.com/exif/1.0/'><exif:DateTimeDigitized>{dm_ms}</exif:DateTimeDigitized></rdf:Description>";
                        xml = Regex.Replace(xml, @"(</rdf:RDF></x:xmpmeta>)", $"{exifdo_xml}$1", RegexOptions.IgnoreCase);
                    }
                    #endregion
                    #region exif:DateTimeOriginal
                    var pattern_exif_do = @"(<exif:DateTimeOriginal>).*?(</exif:DateTimeOriginal>)";
                    if (Regex.IsMatch(xml, pattern_exif_do, RegexOptions.IgnoreCase))
                    {
                        xml = Regex.Replace(xml, pattern_exif_do, $"$1{dm_ms}$2", RegexOptions.IgnoreCase);
                        xml = xml.Replace("$1", "<exif:DateTimeOriginal>");
                    }
                    else
                    {
                        var exifdo_xml = $"<rdf:Description rdf:about='' xmlns:exif='http://ns.adobe.com/exif/1.0/'><exif:DateTimeOriginal>{dm_ms}</exif:DateTimeOriginal></rdf:Description>";
                        xml = Regex.Replace(xml, @"(</rdf:RDF></x:xmpmeta>)", $"{exifdo_xml}$1", RegexOptions.IgnoreCase);
                    }
                    #endregion
                }
            }
            return (xml);
        }
        #endregion

        #region PngCs Routines for Update PNG Image Metadata
        //private static int GZIP_MAGIC = 35615;
        private static byte[] GZIP_MAGIC_HEADER = new byte[] { 0x1F, 0x8B, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        private static string GzipBytesToText(byte[] bytes, Encoding encoding = default(Encoding), int skip = 2)
        {
            var result = string.Empty;
            try
            {
                if (encoding == default(Encoding)) encoding = Encoding.UTF8;
#if DEBUG
                ///
                /// below line is need in VS2015 (C# 6.0) must add a gzip header struct & skip two bytes of zlib header, 
                /// VX2017 (c# 7.0+) work fina willout this line
                ///
                if (bytes[0] != GZIP_MAGIC_HEADER[0] && bytes[1] != GZIP_MAGIC_HEADER[1]) bytes = GZIP_MAGIC_HEADER.Concat(bytes.Skip(2)).ToArray();
#endif
                using (var msi = new MemoryStream(bytes))
                {
                    using (var mso = new MemoryStream())
                    {
                        using (var ds = new System.IO.Compression.GZipStream(msi, System.IO.Compression.CompressionMode.Decompress))
                        {
                            ds.CopyTo(mso);
                            ds.Close();
                        }
                        var ret = mso.ToArray();
                        try
                        {
                            var text = string.Join("", encoding.GetString(ret).Split().Skip(2));
                            var buff = new byte[text.Length/2];
                            for (var i = 0; i < text.Length / 2; i++)
                            {
                                buff[i] = Convert.ToByte($"0x{text[2 * i]}{text[2 * i + 1]}", 16);
                            }
                            result = encoding.GetString(buff.Skip(skip).ToArray());
                        }
                        catch (Exception ex) { ex.ERROR("GzipBytesToText_GetString"); };
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("GzipBytesToText"); }
            return (result);
        }

        public static Dictionary<string, string> GetPngMetaInfo(FileInfo fileinfo, Encoding encoding = default(Encoding), bool full_field = true)
        {
            var result = new Dictionary<string, string>();
            try
            {
                string[] png_meta_chunk_text = new string[]{ "iTXt", "tEXt", "zTXt" };
                if (fileinfo.Exists && fileinfo.Length > 0)
                {
                    if (encoding == default(Encoding)) encoding = Encoding.UTF8;
                    using (var msi = new MemoryStream(File.ReadAllBytes(fileinfo.FullName)))
                    {
                        result = GetPngMetaInfo(msi, encoding, full_field);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("GetPngMetaInfo"); }
            return (result);
        }

        public static Dictionary<string, string> GetPngMetaInfo(Stream src, Encoding encoding = default(Encoding), bool full_field = true)
        {
            var result = new Dictionary<string, string>();
            try
            {
                if (src is Stream && src.CanRead && src.Length > 0)
                {
                    string[] png_meta_chunk_text = new string[]{ "iTXt", "tEXt", "zTXt" };

                    if (encoding == default(Encoding)) encoding = Encoding.UTF8;
                    src.Seek(0, SeekOrigin.Begin);
                    using (var ms = new MemoryStream())
                    {
                        src.CopyTo(ms);
                        ms.Seek(0, SeekOrigin.Begin);
                        var png_r  = new Hjg.Pngcs.PngReader(ms);
                        if (png_r is Hjg.Pngcs.PngReader)
                        {
                            png_r.ChunkLoadBehaviour = Hjg.Pngcs.Chunks.ChunkLoadBehaviour.LOAD_CHUNK_ALWAYS;
                            var png_chunks = png_r.GetChunksList();
                            foreach (var chunk in png_chunks.GetChunks())
                            {
                                if (png_meta_chunk_text.Contains(chunk.Id))
                                {
                                    var raw = chunk.CreateRawChunk();
                                    chunk.ParseFromRaw(raw);

                                    var data = encoding.GetString(raw.Data).Split('\0');
                                    var key = data.FirstOrDefault();
                                    var value = string.Empty;
                                    if (chunk.Id.Equals("zTXt"))
                                    {
                                        value = GzipBytesToText(raw.Data.Skip(key.Length + 2).ToArray());
                                        if ((raw.Data.Length > key.Length + 2) && string.IsNullOrEmpty(value)) value = "(Decodeing Error)";
                                    }
                                    else if (chunk.Id.Equals("iTXt"))
                                    {
                                        var vs = raw.Data.Skip(key.Length+1).ToArray();
                                        var compress_flag = vs[0];
                                        var compress_method = vs[1];
                                        var language_tag = string.Empty;
                                        var translate_tag = string.Empty;
                                        var text = string.Empty;

                                        if (vs[2] == 0 && vs[3] == 0)
                                            text = compress_flag == 1 ? GzipBytesToText(vs.Skip(4).ToArray()) : encoding.GetString(vs.Skip(4).ToArray());
                                        else if (vs[2] == 0 && vs[3] != 0)
                                        {
                                            var trans = vs.Skip(3).TakeWhile(c => c != 0);
                                            translate_tag = encoding.GetString(trans.ToArray());

                                            var txt = vs.Skip(3).Skip(trans.Count()).SkipWhile(c => c==0);
                                            text = compress_flag == 1 ? GzipBytesToText(txt.ToArray()) : encoding.GetString(txt.ToArray());
                                        }

                                        value = full_field ? $"{(int)compress_flag}, {(int)compress_method}, {language_tag}, {translate_tag}, {text.Trim().Trim('\0')}" : text.Trim().Trim('\0');
                                    }
                                    else
                                        value = full_field ? string.Join(", ", data.Skip(1)) : data.Last().Trim().Trim('\0');

                                    result[key] = value;
                                }
                            }
                            png_r.End();
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("GetPngMetaInfo"); }
            return (result);
        }

        private static bool PngUpdateTextMetadata(string fileName, Dictionary<string, string> metainfo, bool keeptime = false)
        {
            var result = false;
            if (File.Exists(fileName) && metainfo is Dictionary<string, string>)
            {
                var fileinfo = new FileInfo(fileName);
                var dtc = fileinfo.CreationTime;
                var dtm = fileinfo.LastWriteTime;
                var dta = fileinfo.LastAccessTime;
                using (var mso = new MemoryStream())
                {
                    using (var msi = new MemoryStream(File.ReadAllBytes(fileName)))
                    {
                        result = PngUpdateTextMetadata(msi, mso, metainfo);
                    }
                    File.WriteAllBytes(fileName, mso.ToArray());
                }
                if (keeptime)
                {
                    fileinfo.CreationTime = dtc;
                    fileinfo.LastWriteTime = dtm;
                    fileinfo.LastAccessTime = dta;
                }
                result = true;
            }
            return (result);
        }

        public static bool UpdatePngMetaInfo(this FileInfo fileinfo, DateTime? dt = null, MetaInfo meta = null, Encoding encoding = default(Encoding))
        {
            var result = false;
            try
            {
                if (fileinfo.Exists && fileinfo.Length > 0 && meta is MetaInfo)
                {
                    var metainfo = new Dictionary<string, string>();

                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Creation_Time] = (meta.DateTaken ?? dt ?? DateTime.Now).ToString("yyyy:MM:dd HH:mm:sszzz");
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Title] = string.IsNullOrEmpty(meta.Title) ? "" : meta.Title;
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Source] = string.IsNullOrEmpty(meta.Subject) ? "" : meta.Subject;
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Comment] = string.IsNullOrEmpty(meta.Keywords) ? "" : meta.Keywords;
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Description] = string.IsNullOrEmpty(meta.Comment) ? "" : meta.Comment;
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Author] = string.IsNullOrEmpty(meta.Authors) ? "" : meta.Authors;
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Copyright] = string.IsNullOrEmpty(meta.Authors) ? "" : meta.Authors;
                    //Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Software
                    //Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Disclaimer
                    //Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Warning

                    //result  = Hjg.Pngcs.FileHelper.PngUpdateTextMetadata(fileinfo.FullName, metainfo, keeptime: true);
                    result = PngUpdateTextMetadata(fileinfo.FullName, metainfo, keeptime: true);
                }
            }
            catch (Exception ex) { ex.ERROR("UpdatePngMetaInfo"); }
            return (result);
        }

        public static bool UpdatePngMetaInfo(this FileInfo fileinfo, DateTime dt = default(DateTime), string id = "", MetaInfo meta = null, Encoding encoding = default(Encoding))
        {
            var result = false;
            try
            {
                if (encoding == default(Encoding)) encoding = Encoding.UTF8;

                if (fileinfo.Exists && fileinfo.Length > 0)
                {
                    if (meta == null) meta = MakeMetaInfo(fileinfo, dt, id);
                    if (meta is MetaInfo)
                    {
                        result = UpdatePngMetaInfo(fileinfo, dt, meta);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("GetMetaInfoPng"); }
            return (result);
        }

        private static bool PngUpdateTextMetadata(Stream src, Stream dst, Dictionary<string, string> metainfo)
        {
            var result = false;
            if (src is Stream && src.CanRead && dst is Stream && dst.CanWrite && src.Length > 0)
            {
                src.Seek(0, SeekOrigin.Begin);
                dst.Seek(0, SeekOrigin.Begin);
                var png_r = new Hjg.Pngcs.PngReader(src);
                if (png_r is Hjg.Pngcs.PngReader)
                {
                    png_r.SetCrcCheckDisabled();
                    png_r.ChunkLoadBehaviour = Hjg.Pngcs.Chunks.ChunkLoadBehaviour.LOAD_CHUNK_ALWAYS;
                    var png_w = new Hjg.Pngcs.PngWriter(dst, png_r.ImgInfo);
                    if (png_w is Hjg.Pngcs.PngWriter)
                    {
                        png_w.ShouldCloseStream = false;
                        png_w.CopyChunksFirst(png_r, Hjg.Pngcs.Chunks.ChunkCopyBehaviour.COPY_ALL);

                        var meta = png_w.GetMetadata();
                        foreach (var kv in metainfo)
                        {
                            if (kv.Key.Equals(Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Creation_Time))
                            {
                                var chunk_ct = new Hjg.Pngcs.Chunks.PngChunkTEXT(png_r.ImgInfo);
                                chunk_ct.SetKeyVal(kv.Key, kv.Value);
                                chunk_ct.Priority = true;
                                meta.QueueChunk(chunk_ct);
                            }
                            else if (kv.Key.Equals(Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Software))
                            {
                                if (!string.IsNullOrEmpty(kv.Value))
                                {
                                    var chunk = meta.SetText(kv.Key, kv.Value);
                                    chunk.Priority = true;
                                }
                            }
                            else
                            {
                                var chunk = meta.SetText(kv.Key, kv.Value);
                                chunk.Priority = true;
                            }
                        }

                        for (int row = 0; row < png_r.ImgInfo.Rows; row++)
                        {
                            Hjg.Pngcs.ImageLine il = png_r.ReadRow(row);
                            png_w.WriteRow(il, row);
                        }

                        png_w.End();
                    }
                    png_r.End();
                    result = true;
                }
            }
            return (result);
        }

        public static bool UpdatePngMetaInfo(this Stream src, Stream dst, DateTime? dt = null, MetaInfo meta = null, Encoding encoding = default(Encoding))
        {
            var result = false;
            try
            {
                if (meta is MetaInfo)
                {
                    var metainfo = new Dictionary<string, string>();

                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Creation_Time] = (meta.DateTaken ?? dt ?? DateTime.Now).ToString("yyyy:MM:dd HH:mm:sszzz");
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Title] = string.IsNullOrEmpty(meta.Title) ? "" : meta.Title;
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Source] = string.IsNullOrEmpty(meta.Subject) ? "" : meta.Subject;
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Comment] = string.IsNullOrEmpty(meta.Keywords) ? "" : meta.Keywords;
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Description] = string.IsNullOrEmpty(meta.Comment) ? "" : meta.Comment;
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Author] = string.IsNullOrEmpty(meta.Authors) ? "" : meta.Authors;
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Copyright] = string.IsNullOrEmpty(meta.Authors) ? "" : meta.Authors;
                    metainfo[Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Software] = string.IsNullOrEmpty(meta.Software) ? "" : meta.Software;

                    //Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Software
                    //Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Disclaimer
                    //Hjg.Pngcs.Chunks.PngChunkTextVar.KEY_Warning

                    //result  = Hjg.Pngcs.FileHelper.PngUpdateTextMetadata(fileinfo.FullName, metainfo, keeptime: true);
                    result = PngUpdateTextMetadata(src, dst, metainfo);
                }
            }
            catch (Exception ex) { ex.ERROR("UpdatePngMetaInfo"); }
            return (result);
        }

        public static bool UpdatePngMetaInfo(this Stream src, Stream dst, FileInfo fileinfo, DateTime dt = default(DateTime), string id = "", MetaInfo meta = null, Encoding encoding = default(Encoding))
        {
            var result = false;
            try
            {
                if (encoding == default(Encoding)) encoding = Encoding.UTF8;

                if (fileinfo.Exists && fileinfo.Length > 0)
                {
                    if (meta == null) meta = MakeMetaInfo(fileinfo, dt, id);
                    if (meta is MetaInfo)
                    {
                        result = UpdatePngMetaInfo(src, dst, dt, meta);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("GetMetaInfoPng"); }
            return (result);
        }
        #endregion

        #region Attach Metadata Helper
        public static bool HasShellProperty(this Microsoft.WindowsAPICodePack.Shell.ShellObject obj, Microsoft.WindowsAPICodePack.Shell.PropertySystem.PropertyKey property, bool writeable = true)
        {
            var result = false;
            if (obj is Microsoft.WindowsAPICodePack.Shell.ShellObject)
            {
                var props = obj.Properties.DefaultPropertyCollection;
                var plist = props.Select(p => p.CanonicalName).ToList();
                result = props.Contains(property) && obj.Properties.GetProperty(property) is Microsoft.WindowsAPICodePack.Shell.PropertySystem.IShellProperty;
                //if(writeable) result &= obj.Properties.GetProperty(property).
            }
            return (result);
        }

        public static MetaInfo MakeMetaInfo(this FileInfo fileinfo, DateTime dt = default(DateTime), string id = "")
        {
            MetaInfo meta = null;
            #region make meta struct
            try
            {
                if (string.IsNullOrEmpty(id)) id = GetIllustId(fileinfo.Name);
                var illust = id.FindIllust();
                if (illust is Pixeez.Objects.Work && (dt != null || dt.Ticks > 0))
                {
                    var uid = $"{illust.User.Id}";
                    meta = new MetaInfo()
                    {
                        DateCreated = dt,
                        DateModified = dt,
                        DateAccesed = dt,
                        DateAcquired = dt,
                        DateTaken = dt,

                        Title = illust.Title.KatakanaHalfToFull().FilterInvalidChar().TrimEnd(),
                        Subject = id.ArtworkLink(),
                        Authors = $"{illust.User.Name ?? string.Empty}; uid:{illust.User.Id ?? -1}",
                        Copyrights = $"{illust.User.Name ?? string.Empty}; uid:{illust.User.Id ?? -1}",
                        Keywords = string.Join("; ", illust.Tags.Select(t => t.Replace(";", "；⸵")).Distinct(StringComparer.CurrentCultureIgnoreCase)),
                        Comment = illust.Caption.HtmlToText(),

                        Rating = illust.IsLiked() ? 75 : 0,
                        Ranking = illust.IsLiked() ? 4 : 0,
                    };
                }
            }
            catch (Exception ex) { ex.ERROR("MakeMeteInfo"); }
            #endregion
            return (meta);
        }

        public static string GetMetaInfo(this FileInfo fileinfo)
        {
            var result = string.Empty;

            try
            {
                if (fileinfo.Exists && fileinfo.Length > 0)
                {
                    var exif = new ExifData(fileinfo.FullName);
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"{"ImageType".PadRight(20)} : {exif.ImageType}");
                    foreach (var TagName in Enum.GetNames(typeof(ExifTag)))
                    {
                        bool MSB = exif.ByteOrder == ExifByteOrder.BigEndian ? true : false;
                        ExifTag tag;
                        if (Enum.TryParse(TagName, out tag))
                        {
                            if (exif.TagExists(tag))
                            {
                                ExifTagType tag_type;
                                if (exif.GetTagType(tag, out tag_type))
                                {
                                    dynamic tag_value = null;
                                    switch (tag_type)
                                    {
                                        case ExifTagType.Ascii:
                                            var value_a = string.Empty;
                                            if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.Double:
                                            //var value_d = double;
                                            //if (exif.GetTagValue(tag, out value_d)) tag_value = value_a;
                                            break;
                                        case ExifTagType.Float:
                                            var value = string.Empty;
                                            if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.SByte:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.Byte:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.SShort:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.UShort:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.SLong:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.ULong:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.SRational:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.URational:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.Undefined:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        default:
                                            break;
                                    }
                                    sb.AppendLine($"{"TagName".PadRight(20)} : {""}");
                                }
                            }
                        }
                    }
                    //foreach(var attr in exif.enu))
                }
            }
            catch (Exception ex) { ex.ERROR("GetMetaInfo"); }

            return (result);
        }

        public static string GetMetaInfo(this Stream src)
        {
            var result = string.Empty;

            try
            {
                if (src is Stream && src.CanRead && src.Length > 0)
                {
                    var exif = new ExifData(src);
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"{"ImageType".PadRight(20)} : {exif.ImageType}");
                    foreach (var TagName in Enum.GetNames(typeof(ExifTag)))
                    {
                        bool MSB = exif.ByteOrder == ExifByteOrder.BigEndian ? true : false;
                        ExifTag tag;
                        if (Enum.TryParse(TagName, out tag))
                        {
                            if (exif.TagExists(tag))
                            {
                                ExifTagType tag_type;
                                if (exif.GetTagType(tag, out tag_type))
                                {
                                    dynamic tag_value = null;
                                    switch (tag_type)
                                    {
                                        case ExifTagType.Ascii:
                                            var value_a = string.Empty;
                                            if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.Double:
                                            //var value_d = double;
                                            //if (exif.GetTagValue(tag, out value_d)) tag_value = value_a;
                                            break;
                                        case ExifTagType.Float:
                                            var value = string.Empty;
                                            if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.SByte:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.Byte:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.SShort:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.UShort:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.SLong:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.ULong:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.SRational:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.URational:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        case ExifTagType.Undefined:
                                            //var value = string.Empty;
                                            //if (exif.GetTagValue(tag, out value_a, StrCoding.UsAscii)) tag_value = value_a;
                                            break;
                                        default:
                                            break;
                                    }
                                    sb.AppendLine($"{"TagName".PadRight(20)} : {""}");
                                }
                            }
                        }
                    }
                    //foreach(var attr in exif.enu))
                }
            }
            catch (Exception ex) { ex.ERROR("GetMetaInfo"); }
            return (result);
        }

        public static ExifData GetExifData(this FileInfo fi)
        {
            ExifData result = null;
            try
            {
                if (fi.Exists)
                {
                    result = new ExifData(fi.FullName);
                    if (result is ExifData && result.ImageType == ImageType.Png)
                    {
                        var dc = fi.CreationTime;
                        var dm = fi.LastWriteTime;
                        var da = fi.LastAccessTime;

                        DateTime dt = dm;

                        var meta = GetPngMetaInfo(fi, full_field: false);
                        if (meta.ContainsKey("Creation Time") && DateTime.TryParse(Regex.Replace(meta["Creation Time"], @"^(\d{4}):(\d{2})\:(\d{2})(.*?)$", "$1/$2/$3T$4", RegexOptions.IgnoreCase), out dt))
                        {
                            if (!result.TagExists(ExifTag.DateTime))
                                result.SetTagValue(ExifTag.DateTime, dt, ExifDateFormat.DateAndTime);
                            if (!result.TagExists(ExifTag.DateTimeDigitized))
                                result.SetTagValue(ExifTag.DateTimeDigitized, dt, ExifDateFormat.DateAndTime);
                            if (!result.TagExists(ExifTag.DateTimeOriginal))
                                result.SetTagValue(ExifTag.DateTimeOriginal, dt, ExifDateFormat.DateAndTime);
                        }
                        if (!result.TagExists(ExifTag.XpTitle) && meta.ContainsKey("Title"))
                            result.SetTagValue(ExifTag.XpTitle, meta["Title"], StrCoding.Utf16Le_Byte);
                        if (!result.TagExists(ExifTag.XpTitle) && meta.ContainsKey("Subject"))
                            result.SetTagValue(ExifTag.XpTitle, meta["Subject"], StrCoding.Utf16Le_Byte);
                        //exif.SetTagRawData(ExifTag.XpSubject, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta["Subject"]), Encoding.Unicode.GetBytes(meta["Subject"]));
                        if (!result.TagExists(ExifTag.XpAuthor) && meta.ContainsKey("Author"))
                        {
                            result.SetTagValue(ExifTag.Artist, meta["Author"], StrCoding.Utf8);
                            result.SetTagValue(ExifTag.XpAuthor, meta["Author"], StrCoding.Utf16Le_Byte);
                            //exif.SetTagRawData(ExifTag.XpAuthor, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta["Author"]), Encoding.Unicode.GetBytes((meta["Author"]));
                        }
                        if (!result.TagExists(ExifTag.Copyright) && meta.ContainsKey("Copyright"))
                            result.SetTagValue(ExifTag.Copyright, meta["Copyright"], StrCoding.Utf8);
                        if (!result.TagExists(ExifTag.XpComment) && meta.ContainsKey("Description"))
                            result.SetTagValue(ExifTag.XpComment, meta["Description"], StrCoding.Utf16Le_Byte);
                        if (!result.TagExists(ExifTag.UserComment) && meta.ContainsKey("Description"))
                            result.SetTagValue(ExifTag.UserComment, meta["Description"], StrCoding.Utf8);

                        if (!result.TagExists(ExifTag.XpKeywords) && meta.ContainsKey("Comment"))
                            result.SetTagValue(ExifTag.XpKeywords, meta["Comment"], StrCoding.Utf16Le_Byte);

                        if (!result.TagExists(ExifTag.FileSource) && meta.ContainsKey("Source"))
                            result.SetTagValue(ExifTag.FileSource, meta["Source"], StrCoding.Utf8);

                        if (!result.TagExists(ExifTag.Software) && meta.ContainsKey("Software"))
                            result.SetTagValue(ExifTag.Software, meta["Software"], StrCoding.Utf8);

                        if (!result.TagExists(ExifTag.XmpMetadata) && meta.ContainsKey("XML:com.adobe.xmp"))
                        {
                            var value = meta["XML:com.adobe.xmp"].Split(new char[]{ '\0' }).Last();
                            result.SetTagRawData(ExifTag.XmpMetadata, ExifTagType.Byte, Encoding.UTF8.GetByteCount(value), Encoding.UTF8.GetBytes(value));
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR($"GetExifData_{fi.Name}"); }
            return (result);
        }

        public static ExifData GetExifData(this Stream src, DateTime dt = default(DateTime))
        {
            ExifData result = null;
            try
            {
                if (src is Stream && src.CanRead && src.Length > 0)
                {
                    if (src.CanSeek) src.Seek(0, SeekOrigin.Begin);
                    result = new ExifData(src);
                    if (result is ExifData && result.ImageType == ImageType.Png)
                    {
                        var meta = GetPngMetaInfo(src, full_field: false);
                        if (meta.ContainsKey("Creation Time") && DateTime.TryParse(Regex.Replace(meta["Creation Time"], @"^(\d{4}):(\d{2})\:(\d{2})(.*?)$", "$1/$2/$3T$4", RegexOptions.IgnoreCase), out dt))
                        {
                            if (!result.TagExists(ExifTag.DateTime))
                                result.SetTagValue(ExifTag.DateTime, dt, ExifDateFormat.DateAndTime);
                            if (!result.TagExists(ExifTag.DateTimeDigitized))
                                result.SetTagValue(ExifTag.DateTimeDigitized, dt, ExifDateFormat.DateAndTime);
                            if (!result.TagExists(ExifTag.DateTimeOriginal))
                                result.SetTagValue(ExifTag.DateTimeOriginal, dt, ExifDateFormat.DateAndTime);
                        }
                        if (!result.TagExists(ExifTag.XpTitle) && meta.ContainsKey("Title"))
                            result.SetTagValue(ExifTag.XpTitle, meta["Title"], StrCoding.Utf16Le_Byte);
                        if (!result.TagExists(ExifTag.XpTitle) && meta.ContainsKey("Subject"))
                            result.SetTagValue(ExifTag.XpTitle, meta["Subject"], StrCoding.Utf16Le_Byte);
                        //exif.SetTagRawData(ExifTag.XpSubject, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta["Subject"]), Encoding.Unicode.GetBytes(meta["Subject"]));
                        if (!result.TagExists(ExifTag.XpAuthor) && meta.ContainsKey("Author"))
                        {
                            result.SetTagValue(ExifTag.Artist, meta["Author"], StrCoding.Utf8);
                            result.SetTagValue(ExifTag.XpAuthor, meta["Author"], StrCoding.Utf16Le_Byte);
                            //exif.SetTagRawData(ExifTag.XpAuthor, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta["Author"]), Encoding.Unicode.GetBytes((meta["Author"]));
                        }
                        if (!result.TagExists(ExifTag.Copyright) && meta.ContainsKey("Copyright"))
                            result.SetTagValue(ExifTag.Copyright, meta["Copyright"], StrCoding.Utf8);
                        if (!result.TagExists(ExifTag.XpComment) && meta.ContainsKey("Description"))
                            result.SetTagValue(ExifTag.XpComment, meta["Description"], StrCoding.Utf16Le_Byte);
                        if (!result.TagExists(ExifTag.UserComment) && meta.ContainsKey("Description"))
                            result.SetTagValue(ExifTag.UserComment, meta["Description"], StrCoding.Utf8);

                        if (!result.TagExists(ExifTag.XpKeywords) && meta.ContainsKey("Comment"))
                            result.SetTagValue(ExifTag.XpKeywords, meta["Comment"], StrCoding.Utf16Le_Byte);

                        if (!result.TagExists(ExifTag.FileSource) && meta.ContainsKey("Source"))
                            result.SetTagValue(ExifTag.FileSource, meta["Source"], StrCoding.Utf8);

                        if (!result.TagExists(ExifTag.Software) && meta.ContainsKey("Software"))
                            result.SetTagValue(ExifTag.Software, meta["Software"], StrCoding.Utf8);

                        if (!result.TagExists(ExifTag.XmpMetadata) && meta.ContainsKey("XML:com.adobe.xmp"))
                        {
                            var value = meta["XML:com.adobe.xmp"].Split(new char[]{ '\0' }).Last();
                            result.SetTagRawData(ExifTag.XmpMetadata, ExifTagType.Byte, Encoding.UTF8.GetByteCount(value), Encoding.UTF8.GetBytes(value));
                        }
                        else if (!result.TagExists(ExifTag.XmpMetadata) && meta.ContainsKey("Raw profile type xmp"))
                        {
                            var value = string.Join("", meta["Raw profile type xmp"].Split(new char[]{ '\0' }).Last().ToArray().SkipWhile(c => c != '<'));
                            result.SetTagRawData(ExifTag.XmpMetadata, ExifTagType.Byte, Encoding.UTF8.GetByteCount(value), Encoding.UTF8.GetBytes(value));
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("GetExifData"); }
            return (result);
        }

        private static bool UpdateExifDate(this ExifData exif, FileInfo fileinfo, DateTime dt = default(DateTime), MetaInfo meta = null)
        {
            var result = true;
            try
            {
                if (exif is ExifData && meta is MetaInfo)
                {
                    exif.SetDateTaken(meta.DateTaken ?? dt);
                    exif.SetDateDigitized(meta.DateAcquired ?? dt);
                    exif.SetDateChanged(meta.DateModified ?? dt);

                    if (string.IsNullOrEmpty(meta.Title))
                    {
                        exif.RemoveTag(ExifTag.XpTitle);
                        exif.RemoveTag(ExifTag.ImageDescription);
                    }
                    else
                    {
                        exif.SetTagRawData(ExifTag.XpTitle, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta.Title), Encoding.Unicode.GetBytes(meta.Title));
                        exif.SetTagValue(ExifTag.ImageDescription, meta.Title, StrCoding.Utf8);
                    }

                    if (string.IsNullOrEmpty(meta.Subject))
                    {
                        exif.RemoveTag(ExifTag.XpSubject);
                    }
                    else
                    {
                        exif.SetTagRawData(ExifTag.XpSubject, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta.Subject), Encoding.Unicode.GetBytes(meta.Subject));
                    }

                    if (string.IsNullOrEmpty(meta.Keywords))
                    {
                        exif.RemoveTag(ExifTag.XpKeywords);
                    }
                    else
                    {
                        exif.SetTagRawData(ExifTag.XpKeywords, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta.Keywords), Encoding.Unicode.GetBytes(meta.Keywords));
                    }

                    if (string.IsNullOrEmpty(meta.Authors))
                    {
                        exif.RemoveTag(ExifTag.XpAuthor);
                        exif.RemoveTag(ExifTag.Artist);
                    }
                    else
                    {
                        exif.SetTagRawData(ExifTag.XpAuthor, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta.Authors), Encoding.Unicode.GetBytes(meta.Authors));
                        exif.SetTagValue(ExifTag.Artist, meta.Authors, StrCoding.Utf8);
                    }

                    if (string.IsNullOrEmpty(meta.Copyrights))
                    {
                        exif.RemoveTag(ExifTag.Copyright);
                    }
                    else
                    {
                        exif.SetTagValue(ExifTag.Copyright, meta.Copyrights, StrCoding.Utf8);
                    }

                    if (string.IsNullOrEmpty(meta.Comment))
                    {
                        exif.RemoveTag(ExifTag.XpComment);
                        exif.RemoveTag(ExifTag.UserComment);
                    }
                    else
                    {
                        exif.SetTagRawData(ExifTag.XpComment, ExifTagType.Byte, Encoding.Unicode.GetByteCount(meta.Comment), Encoding.Unicode.GetBytes(meta.Comment));
                        exif.SetTagValue(ExifTag.UserComment, meta.Comment, StrCoding.IdCode_Utf16);
                    }

                    if (!string.IsNullOrEmpty(meta.Software))
                    {
                        exif.SetTagValue(ExifTag.Software, meta.Software, StrCoding.Utf8);
                    }
                    else if (exif.TagExists(ExifTag.MakerNote))
                    {
                        string note = string.Empty;
                        ExifTagType type;
                        int count;
                        byte[] value;
                        if (string.IsNullOrEmpty(meta.Software))                            
                        {
                            //if (exif.GetTagValue(ExifTag.MakerNote, out note, StrCoding.Utf16Le_Byte))
                            //{

                            //}
                            //else if (exif.GetTagValue(ExifTag.MakerNote, out note, StrCoding.Utf8))
                            //{

                            //}
                            //else if (exif.GetTagRawData(ExifTag.MakerNote, out type, out count, out value))
                            if (exif.GetTagRawData(ExifTag.MakerNote, out type, out count, out value) && value is byte[] && value.Length > 0)
                            {
                                if (type == ExifTagType.Ascii || type == ExifTagType.Byte || type == ExifTagType.SByte)
                                    note = Encoding.UTF8.GetString(value);
                                else if (type == ExifTagType.Undefined)
                                {
                                    try
                                    {
                                        if (value.Length >= 2 && value[1] == 0x00)
                                            note = Encoding.Unicode.GetString(value);
                                        else
                                            note = Encoding.UTF8.GetString(value);
                                    }
                                    catch { note = Encoding.UTF8.GetString(value.Where(c => c != 0x00).ToArray()); }
                                }
                                if (!string.IsNullOrEmpty(note)) note = note.Replace("\0", "").Substring(0, Math.Min(note.Length, 128));
                            }

                            if (!string.IsNullOrEmpty(note))
                            {
                                meta.Software = note;
                                exif.SetTagValue(ExifTag.Software, note, StrCoding.Utf8);
                            }
                        }
                    }

                    exif.SetTagValue(ExifTag.Rating, meta.Ranking ?? 0, TagType: ExifTagType.UShort);
                    exif.SetTagValue(ExifTag.RatingPercent, meta.Rating ?? 0, TagType: ExifTagType.UShort);

                    var xmp = string.Empty;
                    exif.GetTagValue(ExifTag.XmpMetadata, out xmp, StrCoding.Utf8);
                    xmp = TouchXMP(fileinfo, xmp, meta);
                    //exif.SetTagValue(CompactExifLib.ExifTag.XmpMetadata, xmp, StrCoding.Utf8);
                    exif.SetTagRawData(ExifTag.XmpMetadata, ExifTagType.Byte, Encoding.UTF8.GetByteCount(xmp), Encoding.UTF8.GetBytes(xmp));
                    result = true;
                }
            }
            catch (Exception ex) { ex.ERROR("UpdateExifData"); }
            return (result);
        }

        public static bool AttachMetaInfoInternal(this Stream src, Stream dst, FileInfo fileinfo, out bool is_jpg, out int quality, DateTime dt = default(DateTime), string id = "", bool force = false)
        {
            var result = false;
            is_jpg = false;
            quality = -1;
            if (src is Stream && src.CanRead && dst is Stream && dst.CanWrite)
            {
                var setting = Application.Current.LoadSetting();
                src.Seek(0, SeekOrigin.Begin);
                dst.Seek(0, SeekOrigin.Begin);
                var exif = GetExifData(src);
                if (exif is ExifData)
                {
                    is_jpg = exif.ImageType == ImageType.Jpeg;
                    quality = is_jpg ? exif.JpegQuality : 100;
                    var meta = MakeMetaInfo(fileinfo, dt, id);
                    if (meta is MetaInfo)
                    {
                        if (exif.ImageType == ImageType.Png)
                        {
                            if (setting.DownloadAttachPngMetaInfoUsingPngCs)
                            {
                                using (var msp = new MemoryStream())
                                {
                                    if (UpdatePngMetaInfo(src, msp, dt, meta))
                                    {
                                        msp.Seek(0, SeekOrigin.Begin);
                                        exif = GetExifData(msp);
                                    }
                                }
                            }
                        }

                        if (UpdateExifDate(exif, fileinfo, dt, meta))
                        {
                            src.Seek(0, SeekOrigin.Begin);
                            dst.Seek(0, SeekOrigin.Begin);
                            exif.Save(src, dst);
                            result = true;
                        }
                    }
                }
            }
            return (result);
        }

        public static bool AttachMetaInfoInternal(this FileInfo fileinfo, out bool is_jpg, DateTime dt = default(DateTime), string id = "", bool force = false)
        {
            var result = false;
            is_jpg = false;
            try
            {
                if (fileinfo.Exists && fileinfo.Length > 0)
                {
                    var setting = Application.Current.LoadSetting();

                    if (setting.DownloadAttachMetaInfoUsingMemory)
                    {
                        #region Update EXIF using MemoryStream
                        using (var msi = new MemoryStream(File.ReadAllBytes(fileinfo.FullName)))
                        {
                            using (var mso = new MemoryStream())
                            {
                                int quality = -1;                                
                                if (AttachMetaInfoInternal(msi, mso, fileinfo, out is_jpg, out quality, dt, id, force))
                                {
                                    var cs = new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout));
                                    WaitForFile(fileinfo.FullName, cs.Token);
                                    File.WriteAllBytes(fileinfo.FullName, mso.ToArray());
                                    fileinfo.FullName.SetImageQualityInfo(quality);
                                    result = true;
                                }
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        #region Update EXIF using file
                        var exif = new ExifData(fileinfo.FullName);
                        if (exif is ExifData)
                        {
                            is_jpg = exif.ImageType == ImageType.Jpeg;
                            var meta = MakeMetaInfo(fileinfo, dt, id);
                            if (meta is MetaInfo)
                            {
                                if (exif.ImageType == ImageType.Png && setting.DownloadAttachPngMetaInfoUsingPngCs)
                                    UpdatePngMetaInfo(fileinfo, dt, meta);

                                if (UpdateExifDate(exif, fileinfo, dt, meta))
                                {
                                    exif.Save(fileinfo.FullName);
                                    result = true;
                                }
                            }
                            fileinfo.FullName.SetImageQualityInfo(exif.JpegQuality);
                        }
                        #endregion
                    }
                }
            }
            catch (Exception ex)
            {
                // Error occurred while reading image file
                ex.ERROR("AttachMetaInfoInternal");
            }
            return (result);
        }

        public static async Task<bool> AttachMetaInfoInternalAsync(this FileInfo fileinfo, DateTime dt = default(DateTime), string id = "", bool force = false)
        {
            return (await Task.Run<bool>(async () =>
            {
                var is_jpg = false;
                var result = AttachMetaInfoInternal(fileinfo, out is_jpg, dt, id);
                await Task.Delay(250);
                return (result);
            }));
        }

        private static ConcurrentDictionary<string, long> LastAttachMetaInfo = new ConcurrentDictionary<string, long>();
        public static async Task<bool> AttachMetaInfo(this FileInfo fileinfo, DateTime dt = default(DateTime), string id = "", bool force = false)
        {
            var result = false;
            if (fileinfo.Length == 0) { $"{fileinfo.FullName} Zero Length!".ERROR("AttachMetaInfo"); return (result); }

            var now = DateTime.Now.Ticks;
            var setting = Application.Current.LoadSetting();
            var interval = setting.DownloadTouchInterval;
            var capacities = setting.DownloadTouchCapatices;
            var using_shell = setting.DownloadAttachMetaInfoUsingShell;
            if (IsShellSupported && (force || !_Attaching_.ContainsKey(fileinfo.FullName) || now - _Attaching_[fileinfo.FullName] > TimeSpan.FromMilliseconds(interval).Ticks) && await _CanAttaching_.WaitAsync(TimeSpan.FromSeconds(60)))
            {
                try
                {
                    _Attaching_.AddOrUpdate(fileinfo.FullName, now, (k, v) => now);
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"=> Touching Meta : {fileinfo.Name} Started ...");
#endif
                    if (force || !LastAttachMetaInfo.ContainsKey(fileinfo.FullName) || now - LastAttachMetaInfo[fileinfo.FullName] > TimeSpan.FromMilliseconds(interval).Ticks)
                    {
#if DEBUG
                        LastAttachMetaInfo.AddOrUpdate(fileinfo.FullName, now, (k, v) => now);
                        long tick = now;
                        if (LastAttachMetaInfo.Count > capacities)
                            LastAttachMetaInfo.TryRemove(LastAttachMetaInfo.First().Key, out tick);
#endif
                        if (string.IsNullOrEmpty(id)) id = GetIllustId(fileinfo.Name);
                        var illust = id.FindIllust();
                        if (illust is Pixeez.Objects.Work && (dt != null || dt.Ticks > 0) && fileinfo.Length > 0)
                        {
                            if (!illust.HasUser())
                            {
                                throw new Exception("Illust No User Info");
                            }
                            var uid = $"{illust.User.Id}";

                            bool is_png = fileinfo.IsPng();
                            bool is_img = fileinfo.IsImage();
                            bool is_mov = fileinfo.IsMovie();
                            bool is_zip = fileinfo.IsZip();

                            var authors = new string[] { illust.User.Name, $"uid:{illust.User.Id ?? -1}" };

                            string name = Path.GetFileNameWithoutExtension(fileinfo.Name);

                            using (var sh = Microsoft.WindowsAPICodePack.Shell.ShellFile.FromFilePath(fileinfo.FullName))
                            {
                                if (is_img)
                                {
                                    //if (sh.Properties.System.Photo.DateTaken.Value == null || sh.Properties.System.Photo.DateTaken.Value.Value.Ticks != dt.Ticks)
                                    if (sh.HasShellProperty(sh.Properties.System.Photo.DateTaken.PropertyKey) &&
                                       (sh.Properties.System.Photo.DateTaken.Value == null ||
                                        sh.Properties.System.Photo.DateTaken.Value.Value.Ticks != dt.Ticks))
                                        sh.Properties.System.Photo.DateTaken.Value = dt;

                                    if (using_shell && !is_png)
                                    {
                                        #region jpg
                                        if (sh.Properties.System.DateAcquired.Value == null || sh.Properties.System.DateAcquired.Value.Value.Ticks != dt.Ticks)
                                            sh.Properties.System.DateAcquired.Value = dt;

                                        sh.Properties.System.Subject.AllowSetTruncatedValue = true;
                                        if (sh.Properties.System.Subject.Value == null || !sh.Properties.System.Subject.Value.Equals(id.ArtworkLink()))
                                            sh.Properties.System.Subject.Value = id.ArtworkLink();

                                        var title = illust.Title.KatakanaHalfToFull().FilterInvalidChar().TrimEnd();
                                        sh.Properties.System.Title.AllowSetTruncatedValue = true;
                                        if (sh.Properties.System.Title.Value == null || !sh.Properties.System.Title.Value.Equals(title))
                                            sh.Properties.System.Title.Value = title;

                                        sh.Properties.System.Author.AllowSetTruncatedValue = true;
                                        if (sh.Properties.System.Author.Value == null || !sh.Properties.System.Author.Value.Contains(authors[0]) || !sh.Properties.System.Author.Value.Contains(authors[1]))
                                            sh.Properties.System.Author.Value = authors;

                                        sh.Properties.System.Copyright.AllowSetTruncatedValue = true;
                                        if (sh.Properties.System.Copyright.Value == null || !sh.Properties.System.Author.Value.Contains(authors[0]) || !sh.Properties.System.Author.Value.Contains(authors[1]))
                                            sh.Properties.System.Copyright.Value = string.Join("; ", authors);

                                        var tags = illust.Tags.Select(t => t.Replace(";", "；⸵")).Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
                                        sh.Properties.System.Keywords.AllowSetTruncatedValue = true;
                                        if (sh.Properties.System.Keywords.Value == null || sh.Properties.System.Keywords.Value.Length != tags.Length)
                                            sh.Properties.System.Keywords.Value = tags;

                                        var comment = illust.Caption.HtmlToText();
                                        sh.Properties.System.Comment.AllowSetTruncatedValue = true;
                                        if (sh.Properties.System.Comment.Value == null || !sh.Properties.System.Comment.Value.Equals(comment))
                                        {
                                            if (string.IsNullOrEmpty(comment)) sh.Properties.System.Comment.ClearValue();
                                            else sh.Properties.System.Comment.Value = comment;
                                        }

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
                                        result = true;
                                        #endregion
                                    }
                                    else if (!using_shell || is_png)
                                    {
                                        bool is_jpg = false;
                                        result = AttachMetaInfoInternal(fileinfo, out is_jpg, dt, id);
                                        if (is_jpg)
                                        {
#if DEBUG
                                            var sdt = string.Empty;
                                            var fmt = Microsoft.WindowsAPICodePack.Shell.PropertySystem.PropertyDescriptionFormatOptions.SmartDateTime;
#endif
                                            try
                                            {
                                                //if (sh.HasShellProperty(sh.Properties.System.Photo.DateTaken.PropertyKey) &&
                                                //    sh.Properties.System.Photo.DateTaken.TryFormatForDisplay(fmt, out sdt) &&
                                                //   (sh.Properties.System.Photo.DateTaken.Value == null ||
                                                //    sh.Properties.System.Photo.DateTaken.Value.Value.Ticks != dt.Ticks))
                                                if (sh.Properties.System.Photo.DateTaken.Value == null ||
                                                    sh.Properties.System.Photo.DateTaken.Value.Value.Ticks != dt.Ticks)
                                                    sh.Properties.System.Photo.DateTaken.Value = dt;
                                            }
                                            catch (Exception exx) { exx.ERROR($"ShellPropertiesSet_DateTaken_{fileinfo.Name}"); }
                                            try
                                            {
                                                //if (!is_png)
                                                {
                                                    //if (sh.HasShellProperty(sh.Properties.System.DateAcquired.PropertyKey) && 
                                                    //    sh.Properties.System.DateAcquired.TryFormatForDisplay(fmt, out sdt) &&
                                                    //   (sh.Properties.System.DateAcquired.Value == null ||
                                                    //    sh.Properties.System.DateAcquired.Value.Value.Ticks != dt.Ticks))
                                                    if (sh.Properties.System.DateAcquired.Value == null ||
                                                        sh.Properties.System.DateAcquired.Value.Value.Ticks != dt.Ticks)
                                                        sh.Properties.System.DateAcquired.Value = dt;
                                                }
                                            }
                                            catch (Exception exx) { exx.ERROR($"ShellPropertiesSet_DateAcquired_{fileinfo.Name}"); }
#if DEBUG
                                            try
                                            {
                                                if (sh.HasShellProperty(sh.Properties.System.DateImported.PropertyKey) && 
                                                    sh.Properties.System.DateImported.TryFormatForDisplay(fmt, out sdt) &&
                                                   (sh.Properties.System.DateImported.Value == null ||
                                                    sh.Properties.System.DateImported.Value.Value.Ticks != dt.Ticks))
                                                    sh.Properties.System.DateImported.Value = dt;
                                            }
                                            catch (Exception exx) { exx.ERROR($"ShellPropertiesSet_DateImported_{fileinfo.Name}"); }
#endif
                                        }
                                    }
                                }
                                else if (is_mov && !is_zip)
                                {
                                    #region mov
                                    if (sh.Properties.System.DateAcquired.Value == null || sh.Properties.System.DateAcquired.Value.Value.Ticks != dt.Ticks)
                                        sh.Properties.System.DateAcquired.Value = dt;

                                    sh.Properties.System.Subject.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Subject.Value == null || !sh.Properties.System.Subject.Value.Equals(id.ArtworkLink()))
                                        sh.Properties.System.Subject.Value = id.ArtworkLink();

                                    var title = illust.Title.FilterInvalidChar().TrimEnd();
                                    sh.Properties.System.Title.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Title.Value == null || !sh.Properties.System.Title.Value.Equals(title))
                                        sh.Properties.System.Title.Value = title;

                                    sh.Properties.System.Author.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Author.Value == null)
                                        sh.Properties.System.Author.Value = new string[] { illust.User.Name, $"uid:{illust.User.Id ?? -1}" };

                                    var tags = illust.Tags.Select(t => t.Replace(";", "；⸵")).Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
                                    sh.Properties.System.Keywords.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Keywords.Value == null || sh.Properties.System.Keywords.Value.Length != tags.Length)
                                        sh.Properties.System.Keywords.Value = tags;

                                    //sh.Properties.System.Copyright.AllowSetTruncatedValue = true;
                                    //if (sh.Properties.System.Copyright.Value == null)
                                    //    sh.Properties.System.Copyright.Value = $"{illust.User.Name ?? string.Empty}; uid:{illust.User.Id ?? -1}".Trim(';');

                                    var comment = illust.Caption.HtmlToText();
                                    sh.Properties.System.Comment.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Comment.Value == null || !sh.Properties.System.Comment.Value.Equals(comment))
                                    {
                                        if (string.IsNullOrEmpty(comment)) sh.Properties.System.Comment.ClearValue();
                                        else sh.Properties.System.Comment.Value = comment;
                                    }

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

                                    if (sh.Properties.System.Media.DateEncoded.Value == null || sh.Properties.System.Media.DateEncoded.Value.Value.Ticks != dt.Ticks)
                                        sh.Properties.System.Media.DateEncoded.Value = dt;
                                    if (sh.Properties.System.Media.DateReleased.Value == null)
                                        sh.Properties.System.Media.DateReleased.Value = dt.ToString("yyyy/MM/ddTHH:mm:sszzz");
                                    if (sh.Properties.System.Media.Year.Value == null)
                                        sh.Properties.System.Media.Year.Value = (uint)dt.Year;

                                    sh.Properties.System.Media.Subtitle.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Media.Subtitle.Value == null || !sh.Properties.System.Media.Subtitle.Value.Equals(id.ArtworkLink()))
                                        sh.Properties.System.Media.Subtitle.Value = id.ArtworkLink();

                                    sh.Properties.System.Media.ContentDistributor.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Media.ContentDistributor.Value == null)
                                        sh.Properties.System.Media.ContentDistributor.Value = $"{illust.User.Name ?? string.Empty}; uid:{illust.User.Id ?? -1}".Trim(';');

                                    if (sh.Properties.System.Media.AuthorUrl.Value == null)
                                        sh.Properties.System.Media.AuthorUrl.Value = uid.ArtistLink();
                                    if (sh.Properties.System.Media.UserWebUrl.Value == null)
                                        sh.Properties.System.Media.UserWebUrl.Value = uid.ArtistLink();
                                    if (sh.Properties.System.Media.PromotionUrl.Value == null)
                                        sh.Properties.System.Media.PromotionUrl.Value = id.ArtworkLink();

                                    sh.Properties.System.Media.Producer.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Media.Producer.Value == null)
                                        sh.Properties.System.Media.Producer.Value = new string[] { illust.User.Name, $"uid:{illust.User.Id ?? -1}" };
                                    sh.Properties.System.Media.Writer.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Media.Writer.Value == null)
                                        sh.Properties.System.Media.Writer.Value = new string[] { illust.User.Name, $"uid:{illust.User.Id ?? -1}" };
                                    sh.Properties.System.Media.EncodedBy.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Media.EncodedBy.Value == null)
                                        sh.Properties.System.Media.EncodedBy.Value = illust.User.Name;
                                    sh.Properties.System.Media.Publisher.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Media.Publisher.Value == null)
                                        sh.Properties.System.Media.Publisher.Value = illust.User.Name;

                                    sh.Properties.System.Media.MetadataContentProvider.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Media.MetadataContentProvider.Value == null)
                                        sh.Properties.System.Media.MetadataContentProvider.Value = illust.User.Name;

                                    sh.Properties.System.Video.Director.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Video.Director.Value == null)
                                        sh.Properties.System.Video.Director.Value = new string[] { illust.User.Name, $"uid:{illust.User.Id ?? -1}" };

                                    sh.Properties.System.Music.Artist.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Music.Artist.Value == null)
                                        sh.Properties.System.Music.Artist.Value = new string[] { illust.User.Name, $"uid:{illust.User.Id ?? -1}" };
                                    sh.Properties.System.Music.AlbumArtist.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Music.AlbumArtist.Value == null)
                                        sh.Properties.System.Music.AlbumArtist.Value = illust.User.Name;
                                    sh.Properties.System.Music.AlbumTitle.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Music.AlbumTitle.Value == null)
                                        sh.Properties.System.Music.AlbumTitle.Value = title;
                                    sh.Properties.System.Music.Composer.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Music.Composer.Value == null)
                                        sh.Properties.System.Music.Composer.Value = new string[] { illust.User.Name, $"uid:{illust.User.Id ?? -1}" };
                                    sh.Properties.System.Music.Conductor.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Music.Conductor.Value == null)
                                        sh.Properties.System.Music.Conductor.Value = new string[] { illust.User.Name, $"uid:{illust.User.Id ?? -1}" };
                                    sh.Properties.System.Music.Genre.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Music.Genre.Value == null)
                                        sh.Properties.System.Music.Genre.Value = new string[] { "Pixiv", "Comic", "Anime", "Game", "CG", "Japan" };
                                    sh.Properties.System.Music.Mood.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Music.Mood.Value == null)
                                        sh.Properties.System.Music.Mood.Value = "Happy";
                                    sh.Properties.System.Music.Period.AllowSetTruncatedValue = true;
                                    if (sh.Properties.System.Music.Period.Value == null)
                                        sh.Properties.System.Music.Period.Value = dt.ToString("yyyy");

                                    #endregion
                                    result = true;
                                }
                                //sh.Update(null);
                                var ret_info = result ? "Succeeded" : "Failed";
                                $"{fileinfo.FullName} Metadata Update {ret_info}!".INFO("AttachMetaInfo");
                            }
                        }
                        fileinfo.Refresh();
                        //await Task.Delay(250);
                        WaitForFile(fileinfo.FullName, new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout)).Token);
                        if (fileinfo.CreationTime.Ticks != dt.Ticks) fileinfo.CreationTime = dt;
                        if (fileinfo.LastWriteTime.Ticks != dt.Ticks) fileinfo.LastWriteTime = dt;
                        if (fileinfo.LastAccessTime.Ticks != dt.Ticks) fileinfo.LastAccessTime = dt;
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"=> Touching Meta : {fileinfo.Name} Finished!");
#endif
                    }
                }
                catch (Exception ex) { ex.ERROR($"AttachMetaInfo_{fileinfo.Name}"); }
                finally
                {
                    if (_CanAttaching_ is SemaphoreSlim && _CanAttaching_.CurrentCount < 5) _CanAttaching_.Release();
                    result = true;
                    long tick;
                    _Attaching_.TryRemove(fileinfo.FullName, out tick);
#if DEBUG
                    //try { } catch (Exception ex) { ex.StackTrace.DEBUG("AttachMetaCallStacl"); }
                    var trace = new System.Diagnostics.StackTrace(true);
                    trace.ToString().DEBUG("AttachMetaCallStack");
#endif
                }
            }
            return (result);
        }

        public static async void AttachMetaInfo(this string folder, Action progressAction = null, bool force = false)
        {
            if (!string.IsNullOrEmpty(folder))
            {
                try
                {
                    if (Directory.Exists(folder))
                    {
                        AttachMetaInfo(new DirectoryInfo(folder));
                    }
                    else if (File.Exists(folder))
                    {
                        var illust = await folder.GetIllust();
                        if (illust is Pixeez.Objects.Work)
                        {
                            var url = illust.GetOriginalUrl();
                            var dt = url.ParseDateTime();
                            if (dt.Ticks > 0)
                            {
                                var fi = new FileInfo(folder);
                                var meta = await AttachMetaInfo(fi, dt);
                                if (meta) Touch(fi, url, meta: false);
                            }
                        }
                    }
                }
                catch (Exception ex) { ex.ERROR("AttachMetaInfo"); }
            }
        }

        public static void AttachMetaInfo(this DirectoryInfo folderinfo, bool recursion = false, CancellationTokenSource cancelSource = null, Action<BatchProgressInfo> reportAction = null, bool test = false, bool force = false)
        {
            if (Directory.Exists(folderinfo.FullName))
            {
                var setting = Application.Current.LoadSetting();
                var search_opt =  recursion ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = folderinfo.GetFiles("*.*", search_opt);
                var flist = files.Where(f => ext_imgs.Contains(f.Extension)).Distinct().NaturalSort().ToList();
                var parallel = setting.PrefetchingDownloadParallel;
                var rnd = new Random();
                cancelSource = new CancellationTokenSource();
                SemaphoreSlim tasks = new SemaphoreSlim(parallel, parallel);
                for (int i = 0; i < flist.Count; i++)
                {
                    if (cancelSource.IsCancellationRequested) { break; }
                    if (tasks.Wait(-1, cancelSource.Token))
                    {
                        if (cancelSource.IsCancellationRequested) { break; }
                        new Action(async () =>
                        {
                            var f = flist[i];
                            try
                            {
                                if (cancelSource.IsCancellationRequested) { return; }

                                var current_info = new BatchProgressInfo()
                                {
                                    FolderName = folderinfo.FullName,
                                    FileName = f.Name,
                                    DisplayText = f.Name,
                                    Current = i + 1,
                                    Total = flist.Count(),
                                    State = TaskStatus.Running
                                };

                                int idx = -1;
                                var illust = await f.FullName.GetIllustId(out idx).GetIllust();
                                if (illust is Pixeez.Objects.Work)
                                {
                                    var url = idx >= 0 ? illust.GetOriginalUrl(idx) : illust.GetOriginalUrl();
                                    var dt = url.ParseDateTime();
                                    if (dt.Ticks > 0)
                                    {
                                        if (!test)
                                        {
                                            var meta = await AttachMetaInfo(f, dt);
                                            if (meta) Touch(f, url, meta: false);
                                        }
                                        current_info.Result = $"{f.Name} processing successed";
                                    }
                                    else
                                    {
                                        current_info.Result = $"{f.Name} paesing date failed";
                                        $"{f.Name} => {url}".DEBUG("ParseDateTime");
                                    }
                                }
                                else
                                {
                                    current_info.Result = $"{f.Name} get work failed";
                                    f.Name.DEBUG("GetIllust");
                                }
                                current_info.LastestTime = current_info.CurrentTime;
                                current_info.CurrentTime = DateTime.Now;
                                if (i == flist.Count - 1) current_info.State = TaskStatus.RanToCompletion;
                                if (reportAction is Action<BatchProgressInfo>) reportAction.Invoke(current_info);
                                Application.Current.DoEvents();
                            }
                            catch (Exception ex) { ex.ERROR($"BatchProcessing_{f.Name}"); }
                            finally { if (tasks is SemaphoreSlim && tasks.CurrentCount <= parallel) tasks.Release(); Application.Current.DoEvents(); await Task.Delay(1); }
                        }).Invoke(async: false);
                    }
                }
            }
        }
        #endregion

        #region Touch Helper
        public static void Touch(this DirectoryInfo folderinfo, bool recursion = false, CancellationTokenSource cancelSource = null, Action<BatchProgressInfo> reportAction = null, bool test = false, bool force = false)
        {
            if (Directory.Exists(folderinfo.FullName))
            {
                var setting = Application.Current.LoadSetting();
                var search_opt =  recursion ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = folderinfo.GetFiles("*.*", search_opt);
                var flist = files.Where(f => ext_imgs.Contains(f.Extension)).Distinct().NaturalSort().ToList();
                var parallel = setting.PrefetchingDownloadParallel;
                var rnd = new Random();
                cancelSource = new CancellationTokenSource();
                SemaphoreSlim tasks = new SemaphoreSlim(parallel, parallel);
                for (int i = 0; i < flist.Count; i++)
                {
                    if (cancelSource.IsCancellationRequested) { break; }
                    if (tasks.Wait(-1, cancelSource.Token))
                    {
                        if (cancelSource.IsCancellationRequested) { break; }
                        new Action(async () =>
                        {
                            var f = flist[i];
                            try
                            {
                                if (cancelSource.IsCancellationRequested) { return; }

                                var current_info = new BatchProgressInfo()
                                {
                                    FolderName = folderinfo.FullName,
                                    FileName = f.Name,
                                    DisplayText = f.Name,
                                    Current = i + 1,
                                    Total = flist.Count(),
                                    State = TaskStatus.Running
                                };

                                int idx = -1;
                                var illust = await f.FullName.GetIllustId(out idx).GetIllust();
                                if (illust is Pixeez.Objects.Work)
                                {
                                    var url = idx >= 0 ? illust.GetOriginalUrl(idx) : illust.GetOriginalUrl();
                                    var dt = url.ParseDateTime();
                                    if (dt.Ticks > 0)
                                    {
                                        if (!test) Touch(f, url);
                                        current_info.Result = $"{f.Name} processing successed";
                                    }
                                    else
                                    {
                                        current_info.Result = $"{f.Name} paesing date failed";
                                        $"{f.Name} => {url}".DEBUG("ParseDateTime");
                                    }
                                }
                                else
                                {
                                    current_info.Result = $"{f.Name} get work failed";
                                    f.Name.DEBUG("GetIllust");
                                }
                                current_info.LastestTime = current_info.CurrentTime;
                                current_info.CurrentTime = DateTime.Now;
                                if (i == flist.Count - 1) current_info.State = TaskStatus.RanToCompletion;
                                if (reportAction is Action<BatchProgressInfo>) reportAction.Invoke(current_info);
                                Application.Current.DoEvents();
                            }
                            catch (Exception ex) { ex.ERROR($"BatchProcessing_{f.Name}"); }
                            finally { if (tasks is SemaphoreSlim && tasks.CurrentCount <= parallel) tasks.Release(); Application.Current.DoEvents(); await Task.Delay(1); }
                        }).Invoke(async: false);
                    }
                }
            }
        }

        private static ConcurrentDictionary<string, long> LastTouch = new ConcurrentDictionary<string, long>();
        public static void Touch(this FileInfo fileinfo, string url, bool local = false, bool meta = false, bool force = false)
        {
            try
            {
                var now = DateTime.Now.Ticks;
                var interval = Application.Current.LoadSetting().DownloadTouchInterval;
                var capacities = Application.Current.LoadSetting().DownloadTouchCapatices;
                if (force || !_Touching_.ContainsKey(fileinfo.FullName) || now - _Touching_[fileinfo.FullName] > TimeSpan.FromMilliseconds(interval).Ticks)
                {
                    _Touching_.AddOrUpdate(fileinfo.FullName, now, (k, v) => now);

                    if (force || (!LastTouch.ContainsKey(fileinfo.FullName) || (now - LastTouch[fileinfo.FullName] > TimeSpan.FromMilliseconds(interval).Ticks)))
                    {
                        LastTouch.AddOrUpdate(fileinfo.FullName, _Touching_[fileinfo.FullName], (k, v) => _Touching_[fileinfo.FullName]);
                        long tick = now;
                        if (LastTouch.Count > capacities)
                            LastTouch.TryRemove(LastTouch.First().Key, out tick);

                        if (url.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                        {
                            new Action(async () =>
                            {
                                try
                                {
                                    var fdt = url.ParseDateTime();
                                    if (fdt.Year <= 1601) return;
                                    var meta_ret = true;
                                    setting = Application.Current.LoadSetting();
                                    //if (setting.DownloadAttachMetaInfo && meta && await fileinfo.WaitFileUnlockAsync())
                                    if (setting.DownloadAttachMetaInfo && meta && fileinfo.WaitFileUnlock())
                                    {
                                        meta_ret = await fileinfo.AttachMetaInfo(dt: fdt, force: force);
                                        fileinfo = new FileInfo(fileinfo.FullName);
                                    }
                                    //if (await fileinfo.WaitFileUnlockAsync())
                                    if (meta_ret && fileinfo.WaitFileUnlock())
                                    {
#if DEBUG
                                        Debug.WriteLine($"=> Touching Time : {fileinfo.Name}");
#endif
                                        if (fileinfo.CreationTime.Ticks != fdt.Ticks) fileinfo.CreationTime = fdt;
                                        if (fileinfo.LastWriteTime.Ticks != fdt.Ticks) fileinfo.LastWriteTime = fdt;
                                        if (fileinfo.LastAccessTime.Ticks != fdt.Ticks) fileinfo.LastAccessTime = fdt;

                                        if (fileinfo.Extension.Equals(".png", StringComparison.CurrentCultureIgnoreCase) ||
                                            fileinfo.Extension.Equals(".jpg", StringComparison.CurrentCultureIgnoreCase) ||
                                            fileinfo.Extension.Equals(".webp", StringComparison.CurrentCultureIgnoreCase) ||
                                            fileinfo.Extension.Equals(".gif", StringComparison.CurrentCultureIgnoreCase))
                                        {
                                            int idx = -1;
                                            var illust = await fileinfo.FullName.GetIllustId(out idx).GetIllust();
                                            var fname = illust.GetOriginalUrl(idx).GetImageName((illust.PageCount ?? 0) <= 1);
                                            if (!fileinfo.Name.Equals(fname)) fileinfo.MoveTo(Path.Combine(fileinfo.DirectoryName, fname));
                                        }
                                    }
                                }
                                catch (Exception ex) { var id = fileinfo is FileInfo ? fileinfo.Name : url.GetIllustId(); ex.ERROR($"Touch_{id}"); }
                                finally { long ticks; _Touching_.TryRemove(fileinfo.FullName, out ticks); }
                            }).Invoke(async: true);
                        }
                    }
                }
            }
            catch (Exception ex) { var id = fileinfo is FileInfo ? fileinfo.Name : url.GetIllustId(); ex.ERROR($"Touch_{id}"); }
        }

        public static void Touch(this string file, string url, bool local = false, bool meta = false, bool force = false)
        {
            try
            {
                if (File.Exists(file) && file.WaitFileUnlock())
                {
                    FileInfo fi = new FileInfo(file);
                    fi.Touch(url, local, meta, force);
                }
            }
            catch (Exception ex) { var id = Path.GetFileName(file); ex.ERROR($"Touch_{id}"); }
        }

        public static void Touch(this string file, Pixeez.Objects.Work Illust, bool local = false, bool meta = false, bool force = false)
        {
            file.Touch(Illust.GetOriginalUrl(), local, meta, force);
        }

        public static void Touch(this PixivItem item, bool local = false, bool meta = false, bool force = false)
        {
            if (item.IsPage())
            {
                string file = string.Empty;
                item.IsDownloaded = item.Illust.IsDownloaded(out file, item.Index, item.Count <= 1, touch: force);
                item.DownloadedFilePath = file;
                item.DownloadedTooltip = file;
            }
            else if (item.IsWork())
            {
                string file = string.Empty;
                item.IsDownloaded = item.Illust.IsPartDownloaded(out file, touch: force);
                item.DownloadedFilePath = file;
                item.DownloadedTooltip = file;
            }
        }

        public static async void TouchAsync(this string file, string url, bool local = false, bool meta = false, bool force = false)
        {
            await new Action(() => { Touch(file, url, local, meta, force); }).InvokeAsync();
        }

        public static async void TouchAsync(this PixivItem item, bool local = false, bool meta = true, bool force = false)
        {
            await new Action(() => { Touch(item, local, meta, force); }).InvokeAsync();
        }

        public static async void TouchAsync(this IEnumerable<string> files, string url, bool local = false, bool meta = false, bool force = false)
        {
            foreach (var file in files)
            {
                await new Action(() => { Touch(file, url, local, meta, force); }).InvokeAsync();
            }
        }

        public static async void TouchAsync(this IEnumerable<PixivItem> items, bool local = false, bool meta = true, bool force = false)
        {
            foreach (var item in items)
            {
                await new Action(() => { Touch(item, local, meta, force); }).InvokeAsync();
            }
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
                catch (Exception ex) { ex.ERROR("UpdateDownloadedListCache"); }
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
                        var ext = Path.GetExtension(e.Name).ToLower();
                        if (ext_imgs.Contains(ext) || ext_movs.Contains(ext))
                        {
                            if (!_Attaching_.ContainsKey(e.FullPath))
                            {
                                e.FullPath.DownloadedCacheAdd();
                                UpdateDownloadStateAsync(GetIllustId(e.Name), true);
                                lastDownloadEventTick = DateTime.Now;
                            }
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
                    var ext = Path.GetExtension(e.Name).ToLower();
                    if (ext_imgs.Contains(ext) || ext_movs.Contains(ext))
                    {
                        if (!_Attaching_.ContainsKey(e.FullPath))
                        {
                            e.FullPath.DownloadedCacheRemove();
                            UpdateDownloadStateAsync(GetIllustId(e.Name), false);
                            lastDownloadEventTick = DateTime.Now;
                        }
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
                    var ext = Path.GetExtension(e.Name).ToLower();
                    if (ext_imgs.Contains(ext) || ext_movs.Contains(ext))
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
                //if ((exists ?? false) && ((illustid ?? 0) > 0)) Commands.TouchMeta.Execute(illustid ?? 0);
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
                        item.IsDownloaded = item.Illust.IsDownloadedAsync(item.Index);
                    }
                    else if (item.IsWork())
                    {
                        var part_down = item.Illust.IsPartDownloadedAsync();
                        item.IsPartDownloaded = part_down;
                        if (id == -1)
                            item.IsDownloaded = part_down;
                        else if (id == (int)(item.Illust.Id))
                        {
                            if (item.Count > 1)
                                item.IsDownloaded = part_down;
                            else
                                item.IsDownloaded = exists ?? part_down;
                        }
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

        internal static bool IsDownloadedAsync(this Pixeez.Objects.Work illust, bool is_meta_single_page = false, int index = -1, bool touch = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloadedAsync(is_meta_single_page, touch));
            else
                return (false);
        }

        internal static bool IsDownloadedAsync(this Pixeez.Objects.Work illust, out string filepath, bool is_meta_single_page = false, int index = -1, bool touch = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloadedAsync(out filepath, is_meta_single_page, touch));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsDownloadedAsync(this Pixeez.Objects.Work illust, int index = -1, bool touch = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloadedAsync(touch: touch));
            else
                return (false);
        }

        internal static bool IsDownloadedAsync(this Pixeez.Objects.Work illust, out string filepath, int index = -1, bool is_meta_single_page = false, bool touch = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloadedAsync(out filepath, is_meta_single_page, touch));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        private static Func<string, bool, bool, bool> IsDownloadedFunc = (url, meta, touch) => IsDownloaded(url, meta, touch);
        internal static bool IsDownloadedAsync(this string url, bool is_meta_single_page = false, bool touch = false)
        {
            return (IsDownloadedFunc(url, is_meta_single_page, touch));
        }

        private static Func<string, string, bool, bool, DownloadState> IsDownloadedFileFunc = (url, file, meta, touch) =>
        {
            var state = new DownloadState();
            file = string.Empty;
            state.Exists = IsDownloaded(url, out file, meta, touch);
            state.Path = file;
            return(state);
        };

        internal static bool IsDownloadedAsync(this string url, out string filepath, bool is_meta_single_page = false, bool touch = false)
        {
            filepath = string.Empty;
            var result = IsDownloadedFileFunc(url, filepath, is_meta_single_page, touch);
            filepath = result.Path;
            return (result.Exists); ;
        }

        internal static bool IsDownloaded(this Pixeez.Objects.Work illust, bool is_meta_single_page = false, int index = -1, bool touch = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloaded(is_meta_single_page, touch));
            else
                return (false);
        }

        internal static bool IsDownloaded(this Pixeez.Objects.Work illust, int index = -1, bool touch = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloaded(is_meta_single_page: index == -1 || illust.PageCount <= 1, touch: touch));
            else
                return (false);
        }

        internal static bool IsDownloaded(this Pixeez.Objects.Work illust, out string filepath, int index = -1, bool is_meta_single_page = false, bool touch = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloaded(out filepath, is_meta_single_page, touch));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsDownloaded(this string url, bool is_meta_single_page = false, bool touch = false)
        {
            string fp = string.Empty;
            return (IsDownloaded(url, out fp, is_meta_single_page, touch));
        }

        internal static bool IsDownloaded(this string url, out string filepath, bool is_meta_single_page = false, bool touch = false)
        {
            bool result = false;
            filepath = string.Empty;
            try
            {
                var file = url.GetImageName(is_meta_single_page);
                foreach (var local in setting.LocalStorage)
                {
                    if (string.IsNullOrEmpty(local.Folder)) continue;

                    var folder = local.Folder.FolderMacroReplace(url.GetIllustId());
                    if (Directory.Exists(folder))
                    {
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
                if (touch && !string.IsNullOrEmpty(filepath)) filepath.TouchAsync(url, meta: touch);
            }
            catch (Exception ex) { ex.ERROR("IsDownloaded"); }
            return (result);
        }
        #endregion

        #region IsPartDownloaded
        internal static bool IsPartDownloadedAsync(this PixivItem item, bool touch = false)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloadedAsync(touch));
            else
                return (false);
        }

        internal static bool IsPartDownloaded(this PixivItem item, bool touch = false)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloaded(touch));
            else
                return (false);
        }

        internal static bool IsPartDownloadedAsync(this PixivItem item, out string filepath, bool touch = false)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloadedAsync(out filepath, touch));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsPartDownloaded(this PixivItem item, out string filepath, bool touch = false)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloaded(out filepath, touch));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsPartDownloadedAsync(this Pixeez.Objects.Work illust, bool touch = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsPartDownloadedAsync(touch: touch));
            else
                return (false);
        }

        internal static bool IsPartDownloaded(this Pixeez.Objects.Work illust, bool touch = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsPartDownloaded(touch: touch));
            else
                return (false);
        }

        internal static bool IsPartDownloadedAsync(this Pixeez.Objects.Work illust, out string filepath, bool touch = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsPartDownloadedAsync(out filepath, touch: touch));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsPartDownloaded(this Pixeez.Objects.Work illust, out string filepath, bool touch = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsPartDownloaded(out filepath, touch: touch));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        private static Func<string, bool, bool> IsPartDownloadedFunc = (url, touch) => IsPartDownloaded(url, touch);
        internal static bool IsPartDownloadedAsync(this string url, bool touch = false)
        {
            return (IsPartDownloaded(url, touch));
        }

        private static Func<string, string, bool, DownloadState> IsPartDownloadedFileFunc = (url, file, touch) =>
        {
            var state = new DownloadState();
            file = string.Empty;
            state.Exists = IsPartDownloaded(url, out file, touch);
            state.Path = file;
            return(state);
        };

        internal static bool IsPartDownloadedAsync(this string url, out string filepath, bool touch = false)
        {
            filepath = string.Empty;
            var result = IsPartDownloadedFileFunc(url, filepath, touch);
            filepath = result.Path;
            return (result.Exists);
        }

        internal static bool IsPartDownloaded(this string url, bool touch = false)
        {
            string fp = string.Empty;
            return (IsPartDownloaded(url, out fp, touch));
        }

        internal static bool IsPartDownloaded(this string url, out string filepath, bool touch = false)
        {
            bool result = false;
            filepath = string.Empty;
            try
            {
                var file_s = url.GetImageName(true);
                foreach (var local in setting.LocalStorage)
                {
                    if (string.IsNullOrEmpty(local.Folder)) continue;

                    var folder = local.Folder.FolderMacroReplace(url.GetIllustId());
                    if (Directory.Exists(folder))
                    {
                        var f_s = Path.Combine(folder, file_s);
                        if (local.Cached && f_s.DownloadedCacheExistsAsync())
                        {
                            filepath = f_s;
                            result = true;
                        }
                        else if (File.Exists(f_s))
                        {
                            filepath = f_s;
                            result = true;
                        }
                        if (result) break;

                        var fn = Path.GetFileNameWithoutExtension(file_s);
                        var files = Directory.EnumerateFiles(folder, $"{fn}_*.*").NaturalSort();
                        if (files.Count() > 0)
                        {
                            if (touch) { files.Skip(1).TouchAsync(url, meta: touch); }
                            filepath = files.First();
                            result = true;
                        }
                    }
                    if (result) break;
                }
                if (touch && !string.IsNullOrEmpty(filepath)) filepath.TouchAsync(url, meta: touch);
            }
            catch (Exception ex) { ex.ERROR("IsPartDownloaded"); }
            return (result);
        }
        #endregion

        #region Ugoira file download checking
        internal static bool IsUgoiraDownloaded(this string url, out string filepath, bool touch = true)
        {
            bool result = false;
            filepath = string.Empty;
            try
            {
                var file = Path.GetFileNameWithoutExtension(url);
                foreach (var local in setting.LocalStorage)
                {
                    if (string.IsNullOrEmpty(local.Folder)) continue;

                    var folder = local.Folder.FolderMacroReplace(url.GetIllustId());
                    if (Directory.Exists(folder))
                    {
                        foreach (var f in ext_movs.OrderBy(e => e).Select(e => Path.Combine(folder, $"{file}{e}")))
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
                if (touch && !string.IsNullOrEmpty(filepath)) filepath.TouchAsync(url, meta: touch);
            }
            catch (Exception ex) { ex.ERROR("IsUgoiraDownloaded"); }
            return (result);
        }
        #endregion

        public static IEnumerable<string> GetDownloadedFiles(this PixivItem item)
        {
            List<string> result = new List<string>();
            try
            {
                if (item.IsWork())
                {
                    var id = item.ID;
                    var is_page = item.IsPage();
                    var has_page = item.HasPages();
                    foreach (var local in setting.LocalStorage)
                    {
                        if (string.IsNullOrEmpty(local.Folder)) continue;

                        var folder = local.Folder.FolderMacroReplace(id);
                        if (Directory.Exists(folder))
                        {
                            if (is_page)
                            {
                                var file = item.Illust.GetOriginalUrl(item.Index).GetImageName(false);
                                var f = Path.Combine(folder, file);
                                if ((local.Cached && f.DownloadedCacheExistsAsync()) || File.Exists(f))
                                {
                                    result.Add(f);
                                    break;
                                }
                            }
                            else
                            {
                                var sep = item.HasPages() ? "_*" : "";
                                var files = Directory.EnumerateFiles(folder, $"{id}{sep}.*").NaturalSort();
                                if (files.Count() > 0)
                                {
                                    result.AddRange(files);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("GetDownloaded"); }
            return (result.Distinct().ToList());
        }
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
#if DEBUG
            catch (Exception ex) { ex.ERROR(); return false; }
#else
            catch { return false; }
#endif
        }

        private static void WaitForFile(this string filename, CancellationToken cancel = default(CancellationToken))
        {
            //This will lock the execution until the file is ready
            //TODO: Add some logic to make it async and cancelable
            var start =  new TimeSpan(DateTime.Now.Ticks);
            while (!IsFileReady(filename) && !cancel.IsCancellationRequested)
            {
                if (start.Subtract(new TimeSpan(DateTime.Now.Ticks)).Duration().TotalSeconds > setting.DownloadWaitingTime) break;
            }
        }

        public static bool WaitFileUnlock(this FileInfo file, int interval = 50, int times = 20, dynamic timeout = null)
        {
            if (timeout is int && (int)timeout > 0) times = (int)Math.Ceiling(((int)timeout) * 1000.0 / interval);
            else if (timeout is TimeSpan && ((TimeSpan)timeout).Ticks > 0) times = (int)Math.Ceiling(((TimeSpan)timeout).TotalMilliseconds / interval);

            int wait_count = times;
            while (file.IsLocked() && wait_count > 0) { wait_count--; Task.Delay(interval).GetAwaiter().GetResult(); }
            return (true);
        }

        public static bool WaitFileUnlock(this string filename, int interval = 50, int times = 20, dynamic timeout = null)
        {
            if (timeout is int && (int)timeout > 0) times = (int)Math.Ceiling(((int)timeout) * 1000.0 / interval);
            else if (timeout is TimeSpan && ((TimeSpan)timeout).Ticks > 0) times = (int)Math.Ceiling(((TimeSpan)timeout).TotalMilliseconds / interval);

            int wait_count = times;
            while (filename.IsLocked() && wait_count > 0) { wait_count--; Task.Delay(interval).GetAwaiter().GetResult(); }
            return (true);
        }

        public static async Task<bool> WaitFileUnlockAsync(this FileInfo file, int interval = 50, int times = 20, dynamic timeout = null)
        {
            if (timeout is int && (int)timeout > 0) times = (int)Math.Ceiling(((int)timeout) * 1000.0 / interval);
            else if (timeout is TimeSpan && ((TimeSpan)timeout).Ticks > 0) times = (int)Math.Ceiling(((TimeSpan)timeout).TotalMilliseconds / interval);

            int wait_count = times;
            while (file.IsLocked() && wait_count > 0)
            {
                wait_count--;
                var t = Task.Delay(interval);
                await t.ConfigureAwait(false);
                t.Dispose();
            }
            return (true);
        }

        public static async Task<bool> WaitFileUnlockAsync(this string filename, int interval = 50, int times = 20, dynamic timeout = null)
        {
            if (timeout is int && (int)timeout > 0) times = (int)Math.Ceiling(((int)timeout) * 1000.0 / interval);
            else if (timeout is TimeSpan && ((TimeSpan)timeout).Ticks > 0) times = (int)Math.Ceiling(((TimeSpan)timeout).TotalMilliseconds / interval);

            int wait_count = times;
            while (filename.IsLocked() && wait_count > 0)
            {
                wait_count--;
                var t = Task.Delay(interval);
                await t.ConfigureAwait(false);
                t.Dispose();
            }
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
                bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
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
                ex.ERROR("ToImageSource_BitmapImage");
                // maybe loading webp.
                var ret = ex.Message;
                try
                {
                    //result = stream.ToWriteableBitmap(size);
                    var src = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    var bmp = src.ResizeImage(size) as BitmapSource;

                    result = null;
                    result = bmp == null ? src : bmp;
                    result.Freeze();
                    bmp = null;
                    src = null;
                }
                catch (Exception exx) { exx.ERROR("ToImageSource_BitmapFrame"); }
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

        private static System.Drawing.Imaging.ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            // Get image codecs for all image formats 
            var codecs = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders();

            // Find the correct image codec 
            for (int i = 0; i < codecs.Length; i++)
            {
                if (codecs[i].MimeType == mimeType) return codecs[i];
            }

            return null;
        }

        private static int GetJpegQuality(this ExifData exif)
        {
            var result = 0;
            if (exif is ExifData && exif.ImageType == ImageType.Jpeg) result = exif.JpegQuality;
            return (result);
        }

        private static System.Drawing.Color[] GetMatrix(System.Drawing.Bitmap bmp, int x, int y, int w, int h)
        {
            var ret = new List<System.Drawing.Color>();
            if (bmp is System.Drawing.Bitmap)
            {
                //var data = bmp.LockBits(new Rectangle(x, y, w, h), ImageLockMode.ReadOnly, bmp.PixelFormat);
                for (var i = x; i < x + w; i++)
                {
                    for (var j = y; j < y + h; j++)
                    {
                        if (i < bmp.Width && j < bmp.Height)
                            ret.Add(bmp.GetPixel(i, j));
                    }
                }
                //bmp.UnlockBits(data);
            }
            return (ret.ToArray());
        }

        private static bool GuessAlpha(this Stream source, int window = 3, int threshold = 255)
        {
            var result = false;
            try
            {
                if (source is Stream && source.CanRead)
                {
                    var status = false;
                    if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                    using (System.Drawing.Image image = System.Drawing.Image.FromStream(source))
                    {
                        if (image is System.Drawing.Image && (
                            image.RawFormat.Guid.Equals(System.Drawing.Imaging.ImageFormat.Png.Guid) ||
                            image.RawFormat.Guid.Equals(System.Drawing.Imaging.ImageFormat.Bmp.Guid) ||
                            image.RawFormat.Guid.Equals(System.Drawing.Imaging.ImageFormat.Tiff.Guid)))
                        {
                            if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb) { status = true; }
                            else if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppPArgb) { status = true; }
                            else if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format16bppArgb1555) { status = true; }
                            else if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format64bppArgb) { status = true; }
                            else if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format64bppPArgb) { status = true; }
                            else if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.PAlpha) { status = true; }
                            else if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Alpha) { status = true; }
                            else if (System.Drawing.Image.IsAlphaPixelFormat(image.PixelFormat)) { status = true; }

                            if (status)
                            {
                                var bmp = new System.Drawing.Bitmap(image);
                                var w = bmp.Width;
                                var h = bmp.Height;
                                var m = window;
                                var mt = Math.Ceiling(m * m / 2.0);
                                var lt = GetMatrix(bmp, 0, 0, m, m).Count(c => c.A < threshold);
                                var rt = GetMatrix(bmp, w - m, 0, m, m).Count(c => c.A < threshold);
                                var lb = GetMatrix(bmp, 0, h - m, m, m).Count(c => c.A < threshold);
                                var rb = GetMatrix(bmp, w - m, h - m, m, m).Count(c => c.A < threshold);
                                var ct = GetMatrix(bmp, (int)(w / 2.0 - m / 2.0) , (int)(h / 2.0 - m / 2.0), m, m).Count(c => c.A < threshold);
                                status = (lt > mt || rt > mt || lb > mt || rb > mt || ct > mt) ? true : false;
                            }
                        }
                    }
                    result = status;
                }
            }
            catch (Exception ex) { ex.ERROR("GuessAlpha"); }
            return (result);
        }

        private static bool GuessAlpha(this byte[] buffer, int window = 3, int threshold = 255)
        {
            var result = false;
            if (buffer is byte[] && buffer.Length > 0)
            {
                using (var ms = new MemoryStream(buffer))
                {
                    result = GuessAlpha(ms, window, threshold);
                }
            }
            return (result);
        }

        private static bool GuessAlpha(this string file, int window = 3, int threshold = 255)
        {
            var result = false;

            if (File.Exists(file))
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(file)))
                {
                    result = GuessAlpha(ms, window, threshold);
                }
            }
            return (result);
        }

        public static byte[] ConvertImageTo(this byte[] buffer, string fmt, out string failreason, int quality = 85, bool force = false)
        {
            byte[] result = buffer;
            failreason = string.Empty;
            try
            {
                if (buffer is byte[] && buffer.Length > 0)
                {
                    System.Drawing.Imaging.ImageFormat pFmt = System.Drawing.Imaging.ImageFormat.MemoryBmp;

                    fmt = fmt.ToLower();
                    if (fmt.Equals("png")) pFmt = System.Drawing.Imaging.ImageFormat.Png;
                    else if (fmt.Equals("jpg")) pFmt = System.Drawing.Imaging.ImageFormat.Jpeg;
                    else return (buffer);

                    setting = Application.Current.LoadSetting();
                    var hasAlpha = !force && setting.DownloadConvertCheckAlpha ? buffer.GuessAlpha(threshold: setting.DownloadConvertCheckAlphaThreshold) : false;
                    if (!hasAlpha)
                    {
                        using (var mi = new MemoryStream(buffer))
                        {
                            ExifData exif_in = null;
                            using (var exif_ms = new MemoryStream(buffer)) { exif_in = GetExifData(exif_ms); }
                            if (mi.CanSeek) mi.Seek(0, SeekOrigin.Begin);
                            if (mi.CanRead && exif_in is ExifData)
                            {
                                var jq = exif_in.JpegQuality == 0 ? 75 : exif_in.JpegQuality;
                                if (exif_in.ImageType != ImageType.Jpeg || jq > quality)
                                {
                                    using (var mo = new MemoryStream())
                                    {
                                        var bmp = new System.Drawing.Bitmap(mi);
                                        if (bmp is System.Drawing.Bitmap)
                                        {
                                            var codec_info = GetEncoderInfo("image/jpeg");
                                            var qualityParam = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                                            var encoderParams = new System.Drawing.Imaging.EncoderParameters(1);
                                            encoderParams.Param[0] = qualityParam;

                                            if (pFmt == System.Drawing.Imaging.ImageFormat.Jpeg)
                                            {
                                                var img = new System.Drawing.Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                                                img.SetResolution(bmp.HorizontalResolution, bmp.VerticalResolution);
                                                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(img))
                                                {
                                                    var bg = setting.DownloadReduceBackGroundColor;
                                                    var r = new System.Drawing.Rectangle(new System.Drawing.Point(0, 0), bmp.Size);
                                                    g.Clear(System.Drawing.Color.FromArgb(bg.A, bg.R, bg.G, bg.B));
                                                    g.DrawImage(bmp, 0, 0, r, System.Drawing.GraphicsUnit.Pixel);
                                                }
                                                img.Save(mo, codec_info, encoderParams);
                                                img.Dispose();
                                            }
                                            else
                                                bmp.Save(mo, pFmt);

                                            result = mo.ToArray();
                                            bmp.Dispose();
                                        }
                                    }
                                }
                                else result = mi.ToArray();
                            }
                            else result = mi.ToArray();
                        }
                    }
                    else
                    {
                        failreason = $"Image Has Alpha!";
                        result = buffer;
                        throw new WarningException(failreason);
                    }
                }
            }
            catch (WarningException ex) { ex.WARN("ConvertImageTo"); }
            catch (Exception ex) { ex.ERROR("ConvertImageTo", no_stack: ex is WarningException); }
            return (result);
        }

        public static async Task<string> ConvertImageTo(this string file, string fmt, bool keep_name = false, int quality = 85, bool reduce = false, bool force = false)
        {
            string result = string.Empty;
            var feature = reduce ? "Reduce" : "Convert";
            var InfoTitle = $"{feature}Image_{Path.GetFileName(file)}_To_{fmt.ToUpper()}_Q={quality}";
            try
            {
                if (!string.IsNullOrEmpty(file) && File.Exists(file))
                {
                    var setting = Application.Current.LoadSetting();

                    var fi = new FileInfo(file);
                    var dc = fi.CreationTime;
                    var dm = fi.LastWriteTime;
                    var da = fi.LastAccessTime;

                    var fout = keep_name ? file : Path.ChangeExtension(file, $".{fmt}");

                    if (setting.DownloadConvertUsingMemory)
                    {
                        using (var mso = new MemoryStream())
                        {
                            using (var msi = new MemoryStream(File.ReadAllBytes(file)))
                            {
                                ExifData exif_in = null;
                                using (var exif_ms = new MemoryStream()) { await msi.CopyToAsync(exif_ms); exif_in = GetExifData(exif_ms); }
                                if (msi.CanSeek) msi.Seek(0, SeekOrigin.Begin);
                                if (exif_in is ExifData)
                                {
                                    var jq = exif_in.JpegQuality == 0 ? 75 : exif_in.JpegQuality;
                                    file.SetImageQualityInfo(exif_in.JpegQuality);
                                    if (exif_in.ImageType != ImageType.Jpeg || jq > quality)
                                    {
                                        var reason = string.Empty;
                                        using (var msp = new MemoryStream(msi.ToArray().ConvertImageTo(fmt, out reason, quality: quality, force: force)))
                                        {
                                            if (!string.IsNullOrEmpty(reason))
                                            {
                                                //$"{feature}ed File {fi.Name} Failed: {reason}".WARN("ConvertImageTo");
                                                throw new WarningException($"{feature}ed File {fi.Name} Failed: {reason}");
                                            }

                                            msp.Seek(0, SeekOrigin.Begin);
                                            var exif_out = new ExifData(msp);
                                            exif_out.ReplaceAllTagsBy(exif_in);

                                            msp.Seek(0, SeekOrigin.Begin);
                                            mso.Seek(0, SeekOrigin.Begin);
                                            exif_out.Save(msp, mso);
                                            if (exif_out.ImageType == exif_in.ImageType && mso.Length >= fi.Length)
                                            {
                                                $"{feature} {file} To {fmt.ToUpper()} Failed! {reason}".Trim().INFO(InfoTitle);
                                                throw new WarningException($"{feature}ed File Size : {mso.Length} >= Original File Size : {fi.Length}!");
                                            }
                                            File.WriteAllBytes(fout, mso.ToArray());
                                            file.SetImageQualityInfo(exif_out.JpegQuality);
                                        }
                                    }
                                    else throw new WarningException($"{feature}ed File Size : Original Image JPEG Quality <= {quality}!");
                                }
                                else throw new WarningException($"{feature}ed File Size : Original Image has Error!");
                            }
                        }
                    }
                    else
                    {
                        var exif_in = fi.GetExifData();
                        if (exif_in is ExifData && (exif_in.ImageType != ImageType.Jpeg || exif_in.JpegQuality > quality))
                        {
                            file.SetImageQualityInfo(exif_in.JpegQuality);
                            var reason = string.Empty;
                            var bytes = File.ReadAllBytes(file).ConvertImageTo(fmt, out reason, quality: quality, force: force);
                            if (!string.IsNullOrEmpty(reason))
                            {
                                //$"{feature}ed File {fi.Name} Failed: {reason}".WARN("ConvertImageTo");
                                throw new WarningException($"{feature}ed File {fi.Name} Failed: {reason}");
                            }
                            if (((fmt.Equals("jpg", StringComparison.CurrentCultureIgnoreCase) && exif_in.ImageType == ImageType.Jpeg) ||
                                 (fmt.Equals("jpeg", StringComparison.CurrentCultureIgnoreCase) && exif_in.ImageType == ImageType.Jpeg) ||
                                 (fmt.Equals("png", StringComparison.CurrentCultureIgnoreCase) && exif_in.ImageType == ImageType.Png)) &&
                                bytes.Length >= fi.Length)
                            {
                                $"{feature} {file} To {fmt.ToUpper()} Failed! {reason}".Trim().INFO(InfoTitle);
                                throw new WarningException($"{feature}ed File Size : {bytes.Length} >= Original File Size : {fi.Length}!");
                            }
                            File.WriteAllBytes(fout, bytes);

                            var exif_out = new ExifData(fout);
                            exif_out.ReplaceAllTagsBy(exif_in);
                            exif_out.Save(fout);
                            file.SetImageQualityInfo(exif_out.JpegQuality);
                        }
                        else throw new WarningException($"{feature}ed File Size : Original Image is Error or JPEG Quality <= {quality}!");
                    }

                    var fo = new FileInfo(fout);
                    fo.CreationTime = dc;
                    fo.LastWriteTime = dm;
                    fo.LastAccessTime = da;

                    var id = fout.GetIllustId();
                    var idx = fout.GetIllustPageIndex();
                    var illust = id.FindIllust();
                    await new FileInfo(fout).AttachMetaInfo(illust.GetDateTime(), id, true);

                    result = fout;
                    if (string.IsNullOrEmpty(fout))
                        $"{feature} {file} To {fmt.ToUpper()} Failed!".INFO(InfoTitle);
                    else
                        $"{feature} {file} To {fmt.ToUpper()} Succeed!".INFO(InfoTitle);                    
                }
            }
            catch (WarningException ex) { ex.WARN(InfoTitle); }
            catch (Exception ex) { ex.ERROR(InfoTitle, no_stack: ex is WarningException); }
            return (result);
        }

        public static async Task<string> ReduceImageFileSize(this string file, string fmt, bool keep_name = false, int quality = 85, bool force = false)
        {
            return (await ConvertImageTo(file, fmt, keep_name: true, quality: quality, reduce: true, force: force));
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
            await new Action(async () =>
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
            }).InvokeAsync(realtime: false);
        }

        public static async void CopyImage(this string file)
        {
            await new Action(async () =>
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
            }).InvokeAsync(realtime: false);
        }

        public static void CleenLastDownloaded(this string file)
        {
            byte[] lastdown = null;
            if (DownloadTaskCache.TryRemove(file, out lastdown))
            {
                if (lastdown is byte[] && lastdown.Length >= 0) lastdown.Dispose();
            }
        }

        public static async Task<bool> WriteToFile(this Stream source, string file, ContentRangeHeaderValue range = null, Action<double, double> progressAction = null, CancellationTokenSource cancelToken = null, int bufferSize = 4096, FileMode mode = FileMode.OpenOrCreate, FileAccess access = FileAccess.ReadWrite, FileShare share = FileShare.ReadWrite, byte[] lastdownloaded = null)
        {
            var result = false;
            using (var ms = new MemoryStream())
            {
                var fn = string.IsNullOrEmpty(file) ? string.Empty : $"_{Path.GetFileName(file)}";
                try
                {
                    if (source.CanRead)
                    {
                        if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                        var length = range is ContentRangeHeaderValue && range.HasLength ? range.Length ?? 0 : 0;
                        int received = 0;
                        if (length <= 0 && source.CanRead)
                        {
                            try
                            {
                                await source.CopyToAsync(ms, bufferSize);
                                length = received = (int)ms.Length;
                                if (progressAction is Action<double, double>) progressAction.Invoke(received, length);
                            }
                            catch { }
                        }
                        else
                        {
                            setting = Application.Current.LoadSetting();
                            if (lastdownloaded is byte[] && (range.From ?? 0) <= lastdownloaded.Length)
                            {
                                await ms.WriteAsync(lastdownloaded, 0, Math.Min((int)(range.From ?? 0), lastdownloaded.Length));
                                received = (int)ms.Position;
                            }

                            bufferSize = setting.DownloadHttpStreamBlockSize;
                            byte[] bytes = new byte[bufferSize];
                            int bytesread = 0;
                            if (!(cancelToken is CancellationTokenSource)) cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout));
                            do
                            {
                                using (cancelToken.Token.Register(() => source.Close()))
                                {
                                    if (source.CanRead)
                                    {
                                        //try
                                        //{
                                            bytesread = await source.ReadAsync(bytes, 0, bufferSize, cancelToken.Token).ConfigureAwait(false);
                                        //}
                                        //catch { }
                                        //catch (Exception exx) { exx.ERROR($"WriteToFile_StreamClosed{fn}", no_stack: exx.IsNetworkError()); }
                                        //catch { throw new WarningException($"StreamClosed{fn}"); }
                                    }
                                    else throw new WarningException($"StreamClosed{fn}");
                                }

                                if (bytesread > 0 && bytesread <= bufferSize && received < length)
                                {
                                    received += bytesread;
                                    if (ms is MemoryStream && ms.CanWrite)
                                    {
                                        await ms.WriteAsync(bytes, 0, bytesread);
                                        if (progressAction is Action<double, double>) progressAction.Invoke(received, length);
                                    }
                                    else throw new WarningException($"Write Bytes To Stream Failed");
                                }
                            } while (!cancelToken.IsCancellationRequested && bytesread > 0 && received < length);
                        }

                        if (!cancelToken.IsCancellationRequested && received == length && ms.Length == length)
                        {
                            var folder = Path.GetDirectoryName(file);
                            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                            if (!File.Exists(file) || await file.WaitFileUnlockAsync(1000, 10))
                            {
                                using (var fs = new FileStream(file, mode, access, share, bufferSize, true))
                                {
                                    await fs.WriteAsync(ms.ToArray(), 0, (int)ms.Length);
                                    await fs.FlushAsync();
                                    fs.Close();
                                    fs.Dispose();

                                    DownloadTaskCache.TryRemove(file, out lastdownloaded);
                                    if (lastdownloaded is byte[] && lastdownloaded.Length >= 0) lastdownloaded.Dispose();
                                }
                            }                            
                        }

                        ms.Close();
                        ms.Dispose();

                        if (progressAction is Action<double, double>) progressAction.Invoke(received, length);

                        result = File.Exists(file);
                        //var illust = file.GetIllustId().FindIllust();
                    }
                }
                catch (Exception ex)
                {
                    ex.ERROR($"WriteToFile{fn}", no_stack: ex is WarningException || ex.IsNetworkError() || ex.IsCanceled() || !source.CanRead);
                    if (ms is MemoryStream && ms.Length < (range.Length ?? 0))
                    {
                        if (lastdownloaded is byte[]) lastdownloaded.Dispose();
                        lastdownloaded = ms.ToArray();
                        if (DownloadTaskCache.ContainsKey(file)) DownloadTaskCache.TryUpdate(file, lastdownloaded, DownloadTaskCache[file]);
                        else DownloadTaskCache.TryAdd(file, lastdownloaded);
                        //DownloadTaskCache.AddOrUpdate(file, lastdownloaded, (k, v) => lastdownloaded);

                        if (progressAction is Action<double, double>) progressAction.Invoke(ms.Length, range.Length ?? 0);
                    }
                }
            }
            return (result);
        }
        #endregion

        #region Load/Save Image routines
        private static ConcurrentDictionary<string, bool> _Downloading_ = new ConcurrentDictionary<string, bool>();

        public static async Task<CustomImageSource> LoadImageFromFile(this string file, Size size = default(Size))
        {
            CustomImageSource result = new CustomImageSource();
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                try
                {
                    if (await file.WaitFileUnlockAsync(275, 20))
                    {
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
                }
                catch (Exception ex) { ex.ERROR("LoadImageFromFile", no_stack:ex is IOException); }
            }
            return (result);
        }

        public static async Task<CustomImageSource> LoadImageFromUrl(this string url, bool overwrite = false, bool login = false, Size size = default(Size), Action<double, double> progressAction = null, CancellationTokenSource cancelToken = null)
        {
            CustomImageSource result = new CustomImageSource();
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = await cache.GetImage(url, overwrite, login, size, progressAction, cancelToken);
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

        public static async Task<string> DownloadCacheFile(this string url, bool overwrite = false, Action<double, double> progressAction = null, CancellationTokenSource cancelToken = null)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = await cache.DownloadImage(url, overwrite, overwrite, progressAction, cancelToken);
            }
            return (result);
        }

        public static bool IsDownloading(this string file)
        {
            return (_Downloading_ is ConcurrentDictionary<string, bool> && _Downloading_.ContainsKey(file));
        }

        public static long QueryDownloadingState(this string file)
        {
            long result = 0;// File.Exists(file) ? new FileInfo(file).Length : 0;
            if (DownloadTaskCache is ConcurrentDictionary<string, byte[]> && DownloadTaskCache.ContainsKey(file))
            {
                byte[] d = null;
                if (DownloadTaskCache.TryGetValue(file, out d))
                {
                    if (d is byte[]) result = d.Length;
                }
            }
            return (result);
        }

        public static void ClearDownloading(this string file)
        {
            if (_Downloading_ is ConcurrentDictionary<string, bool> && _Downloading_.ContainsKey(file))
            {
                bool f = false;
                _Downloading_.TryRemove(file, out f);
            }
        }

        private static ConcurrentDictionary<string, long?> _ImageFileSizeCache_ = new ConcurrentDictionary<string, long?>();

        public static IList<string> TrimImageFileSizeData(this ConcurrentDictionary<string, long?> data, int keep = 10000)
        {
            List<string> result = new List<string>();
            if (data is ConcurrentDictionary<string, long?>)
            {
                try
                {
                    var datelist = data.Select(o => new KeyValuePair<string, DateTime>(o.Key, o.Key.ParseDateTime())).OrderByDescending(o => o.Value);
                    var dates = datelist.Take(keep).ToDictionary(o => o.Key, o => o.Value);
                    result = dates.Keys.ToList();
                }
                catch (Exception ex) { ex.ERROR("TrimImageFileSizeData"); }
            }
            return (result);
        }

        public static void SaveImageFileSizeData(this string file)
        {
            try
            {
                var json = JsonConvert.SerializeObject(_ImageFileSizeCache_, Newtonsoft.Json.Formatting.Indented);
                file.WaitFileUnlock();
                File.WriteAllText(file, json, new UTF8Encoding(true));
            }
            catch (Exception ex) { ex.ERROR("SaveImageFileSizeData"); }
        }

        public static void LoadImageFileSizeData(this string file)
        {
            try
            {
                if (File.Exists(file) && file.WaitFileUnlock())
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var data = JsonConvert.DeserializeObject<ConcurrentDictionary<string, long?>>(json);
                        var keys = TrimImageFileSizeData(data);
                        if (keys.Count <= 0) keys = data.Keys.ToList();
                        _ImageFileSizeCache_.Clear();
                        foreach (var k in keys)
                        {
                            if (data[k] > 0) _ImageFileSizeCache_.TryAdd(k.Trim(), data[k]);
                        }
                    }
                    catch (Exception ex) { ex.ERROR("LoadImageFileSizeData"); }
                }
            }
            catch (Exception ex) { ex.ERROR("SaveImageFileSizeData"); }
        }

        public static async Task<long?> QueryImageFileSize(this string url, CancellationTokenSource cancelToken = null)
        {
            long? result = null;

            setting = Application.Current.LoadSetting();
            if (_ImageFileSizeCache_.ContainsKey(url)) _ImageFileSizeCache_.TryGetValue(url, out result);
            if (setting.QueryOriginalImageSize && (result ?? -1) <= 0)
            {
                if (!(cancelToken is CancellationTokenSource)) cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout));
                HttpResponseMessage response = null;
                try
                {
                    setting = Application.Current.LoadSetting();
                    HttpClient client = Application.Current.GetHttpClient(is_download: setting.QueryImageSizeAsDownload);
                    using (var request = Application.Current.GetHttpRequest(url))
                    {
                        using (response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelToken.Token))
                        {
                            //response.EnsureSuccessStatusCode();
                            if (response != null && (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.PartialContent))
                            {
                                var length = response.Content.Headers.ContentLength ?? 0;
                                var range = response.Content.Headers.ContentRange ?? new ContentRangeHeaderValue(0, 0, length);
                                result = range.Length ?? -1;
                                _ImageFileSizeCache_.AddOrUpdate(url, result, (k, v) => result);
                            }
                            response.Dispose();
                        }
                        request.Dispose();
                    }
                }
                catch (Exception ex) { ex.Message.ERROR($"QueryImageFileSize_{Path.GetFileName(url)}"); }
            }
            return (result);
        }

        public static Func<string, int, TimeSpan, Task<bool>> WaitDownloadingFunc = async(file, interval, timeout) =>
        {
            bool exists = false;
            int wait_count = timeout.Ticks > 0 ? (int)Math.Ceiling(timeout.TotalMilliseconds / interval) : 100;
            while (file.IsDownloading() && wait_count > 0)
            {
                wait_count--;
                var t = Task.Delay(interval);
                await t.ConfigureAwait(false);
                t.Dispose();
            }
            exists = File.Exists(file);
            return(exists);
        };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file">file to downloading</param>
        /// <param name="interval">unit: miliseconds, default: 100ms</param>
        /// <param name="times">unit: none, default: 100 times</param>
        /// <param name="timeout">unit: miliseconds or TimeSpan, default: null</param>
        /// <returns></returns>
        public static async Task<bool> WaitDownloading(this string file, int interval = 100, int times = 100, dynamic timeout = null)
        {
            bool exists = false;
            if (!string.IsNullOrEmpty(file))
            {
                if (timeout is int && (int)timeout > 0) times = (int)Math.Ceiling(((int)timeout) * 1000.0 / interval);
                else if (timeout is TimeSpan && ((TimeSpan)timeout).Ticks > 0) times = (int)Math.Ceiling(((TimeSpan)timeout).TotalMilliseconds / interval);

                int wait_count = times;
                while (file.IsDownloading() && wait_count > 0)
                {
                    wait_count--;
                    var t = Task.Delay(interval);
                    await t;
                    t.Dispose();
                }
                exists = File.Exists(file);
            }
            return (exists);
        }

        public static async Task<string> DownloadImage(this string url, string file, bool overwrite = true, Action<double, double> progressAction = null, CancellationTokenSource cancelToken = null)
        {
            var result = string.Empty;
            if (!File.Exists(file) || overwrite || new FileInfo(file).Length <= 0)
            {
                setting = Application.Current.LoadSetting();
                if (!(cancelToken is CancellationTokenSource)) cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout));
                if (_Downloading_.TryAdd(file, true))
                {
                    HttpResponseMessage response = null;
                    try
                    {
                        int count = setting.DownloadFailAutoRetryCount;
                        do
                        {
                            byte[] lastdownloaded = null;
                            int start = 0;
                            if (DownloadTaskCache.TryGetValue(file, out lastdownloaded)) start = lastdownloaded.Length;
                            HttpClient client = Application.Current.GetHttpClient(is_download: true);
                            using (var request = Application.Current.GetHttpRequest(url, range_start: start))
                            {
                                using (response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancelToken.Token))
                                {
                                    //response.EnsureSuccessStatusCode();
                                    if (response != null && (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.PartialContent))
                                    {
                                        var length = response.Content.Headers.ContentLength ?? 0;
                                        var range = response.Content.Headers.ContentRange ?? new ContentRangeHeaderValue(0, 0, length);
                                        var pos = range.From ?? 0;
                                        var Length = range.Length ?? 0;
                                        if (progressAction is Action<double, double>) progressAction.Invoke(pos, Length);
                                        if (length > 0) _ImageFileSizeCache_.AddOrUpdate(url, Length, (k, v) => Length);

                                        string vl = response.Content.Headers.ContentEncoding.FirstOrDefault();
                                        using (var sr = vl != null && vl == "gzip" ? new System.IO.Compression.GZipStream(await response.Content.ReadAsStreamAsync(), System.IO.Compression.CompressionMode.Decompress) : await response.Content.ReadAsStreamAsync())
                                        {
                                            var ret = await sr.WriteToFile(file, range, progressAction, cancelToken, lastdownloaded: lastdownloaded);
                                            if (ret) result = file;
                                            sr.Close();
                                            sr.Dispose();
                                        }
                                    }
                                    response.Dispose();
                                }
                                request.Dispose();
                            }
                            count--;
                            if (string.IsNullOrEmpty(result) && count > 0) await Task.Delay(Application.Current.DownloadRetryDelay());
                        }
                        while (string.IsNullOrEmpty(result) && count > 0);
                    }
                    catch (Exception ex) { ex.ERROR($"DownloadImage_{Path.GetFileName(file)}"); }
                    finally
                    {
                        bool f = false;
                        _Downloading_.TryRemove(file, out f);
                        if (response is HttpResponseMessage) response.Dispose();
                    }
                }
            }
            return (result);
        }

        public static async Task<string> DownloadImage(this string url, string file, Pixeez.Tokens tokens, bool overwrite = true, CancellationTokenSource cancelToken = null)
        {
            var result = string.Empty;
            if (!File.Exists(file) || overwrite || new FileInfo(file).Length <= 0)
            {
                if (tokens == null) tokens = await ShowLogin();
                if (_Downloading_.TryAdd(file, true))
                {
                    try
                    {
                        int count = setting.DownloadFailAutoRetryCount;
                        do
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
                            count--;
                            if (string.IsNullOrEmpty(result) && count > 0) await Task.Delay(Application.Current.DownloadRetryDelay());
                        }
                        while (string.IsNullOrEmpty(result) && count > 0);
                    }
                    catch (Exception ex) { ex.ERROR($"DownloadImage_{Path.GetFileName(file)}"); }
                    finally
                    {
                        bool f = false;
                        _Downloading_.TryRemove(file, out f);
                    }
                }
            }
            return (result);
        }

        public static async Task<bool> SaveImage(this string url, string file, bool overwrite = true, Action<double, double> progressAction = null, CancellationTokenSource cancelToken = null)
        {
            bool result = false;
            if (url.IndexOf("https://") > 1 || url.IndexOf("http://") > 1) return (result);

            setting = Application.Current.LoadSetting();
            var cancelTokenSource = new CancellationTokenSource();
            if (!(cancelToken is CancellationTokenSource)) cancelToken = new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout));
            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    var unc = file.IndexOf("file:\\\\\\");
                    if (unc > 0) file = file.Substring(0, unc - 1);
                    else if (unc == 0) file = file.Substring(8);

                    result = !string.IsNullOrEmpty(await url.DownloadImage(file, overwrite, progressAction, cancelToken));
                }
                catch (Exception ex)
                {
                    if (ex is IOException) { }
                    else { ex.ERROR("SaveImage"); }
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

        public static async Task<string> SaveImage(this string url, Pixeez.Tokens tokens, DateTime dt, bool is_meta_single_page = false, bool overwrite = true, bool jpeg = false, bool largepreview = false)
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

        public static void SaveImage(this string url, string thumb, DateTime dt, bool is_meta_single_page = false, bool overwrite = true, bool jpeg = false, bool largepreview = false)
        {
            Commands.AddDownloadItem.Execute(new DownloadParams()
            {
                Url = url,
                ThumbUrl = thumb,
                SaveAsJPEG = jpeg,
                SaveLargePreview = largepreview,
                Timestamp = dt,
                IsSinglePage = is_meta_single_page,
                OverwriteExists = overwrite
            });
        }

        public static void SaveImages(Dictionary<Tuple<string, bool>, Tuple<string, DateTime>> files, bool overwrite = true, bool jpeg = false, bool largepreview = false)
        {
            foreach (var file in files)
            {
                var url = file.Key.Item1;
                var is_meta_single_page =  file.Key.Item2;
                var thumb = file.Value.Item1;
                var dt = file.Value.Item2;
                url.SaveImage(thumb, dt, is_meta_single_page, overwrite, jpeg: jpeg, largepreview: largepreview);
            }
            SystemSounds.Beep.Play();
        }

        public static void MakeUgoiraConcatFile(this Pixeez.Objects.UgoiraInfo ugoira_info, string file)
        {
            if (!string.IsNullOrEmpty(file) && ugoira_info != null)
            {
                var fn = Path.ChangeExtension(file, ".txt");
                if (!File.Exists(fn))
                {
                    List<string> lines = new List<string>();
                    foreach (var frame in ugoira_info.Frames)
                    {
                        lines.Add($"file '{frame.File}'");
                        lines.Add($"duration {Math.Max(0.04, frame.Delay / 1000.0):F2}");
                    }
                    File.WriteAllLines(fn, lines);
                    fn.DEBUG("MakeUgoiraConcatFile");
                }
            }
        }
        #endregion

        #region Illust routines
        public static bool IsWork(this Pixeez.Objects.Work work)
        {
            return (work is Pixeez.Objects.Work);
        }

        public static bool IsUgoira(this Pixeez.Objects.Work work)
        {
            return (work is Pixeez.Objects.Work && (work.IsUgoira || work.Type.Equals("ugoira", StringComparison.CurrentCultureIgnoreCase)));
        }

        public static bool IsNormalWork(this Pixeez.Objects.Work work)
        {
            return (work is Pixeez.Objects.NormalWork);
        }

        public static bool IsIllustWork(this Pixeez.Objects.Work work)
        {
            return (work is Pixeez.Objects.IllustWork);
        }

        public static bool HasMetadata(this Pixeez.Objects.Work work)
        {
            var result = false;
            if (work is Pixeez.Objects.Work)
            {
                result = work.PageCount > 1 &&
                         work.Metadata is Pixeez.Objects.Metadata &&
                         work.Metadata.Pages is IList<Pixeez.Objects.Page> &&
                         work.Metadata.Pages.Count == work.PageCount &&
                         work.Metadata.Pages.Count(p => string.IsNullOrEmpty(p.GetThumbnailUrl())) > 0;
            }
            return (result);
        }

        public static bool HasUser(this Pixeez.Objects.Work work)
        {
            return (work is Pixeez.Objects.Work && work.User is Pixeez.Objects.UserBase);
        }

        public static bool HasNewUser(this Pixeez.Objects.Work work)
        {
            return (work is Pixeez.Objects.Work && work.User is Pixeez.Objects.NewUser);
        }

        public static JObject IllustToJObject(this Pixeez.Objects.Work work)
        {
            var result = new JObject();
            try
            {
                if (work.IsWork() && work.HasUser())
                {
                    var json = JObject.FromObject(work);
                    result.Add("id", JToken.FromObject(work.Id));
                    result.Add("date", JToken.FromObject(work.GetDateTime()));
                    result.Add("title", JToken.FromObject(work.Title.KatakanaHalfToFull().FilterInvalidChar()));
                    result.Add("description", JToken.FromObject(work.Caption.HtmlToText()));
                    result.Add("tags", JToken.FromObject(string.Join(" ", work.Tags.Select(t => $"#{t}"))));
                    result.Add("favorited", JToken.FromObject(work.IsBookMarked()));
                    result.Add("downloaded", JToken.FromObject(work.IsDownloaded(0, touch: false)));
                    result.Add("weblink", JToken.FromObject($"{work.Id}".ArtworkLink()));
                    result.Add("user", JToken.FromObject(work.User.Name));
                    result.Add("userid", JToken.FromObject(work.User.Id));
                    result.Add("userlink", JToken.FromObject($"{work.User.Id}".ArtistLink()));
                }
            }
            catch (Exception ex) { ex.ERROR("IllustToJObject"); }
            return (result);
        }

        public static JArray IllustToJObject(this IEnumerable<Pixeez.Objects.Work> works)
        {
            var result = new JArray();
            try
            {
                foreach (var work in works)
                {
                    if (work.IsWork() && work.HasUser())
                    {
                        var json = new JObject();
                        json.Add("id", JToken.FromObject(work.Id));
                        json.Add("date", JToken.FromObject(work.GetDateTime()));
                        json.Add("title", JToken.FromObject(work.Title.KatakanaHalfToFull().FilterInvalidChar()));
                        json.Add("description", JToken.FromObject(work.Caption.HtmlToText()));
                        json.Add("tags", JToken.FromObject(string.Join(" ", work.Tags.Select(t => $"#{t}"))));
                        json.Add("favorited", JToken.FromObject(work.IsBookMarked()));
                        json.Add("downloaded", JToken.FromObject(work.IsDownloaded(0, touch: false)));
                        json.Add("weblink", JToken.FromObject($"{work.Id}".ArtworkLink()));
                        json.Add("user", JToken.FromObject(work.User.Name));
                        json.Add("userid", JToken.FromObject(work.User.Id));
                        json.Add("userlink", JToken.FromObject($"{work.User.Id}".ArtistLink()));
                        result.Add(json);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("IllustToJObject"); }
            return (result);
        }

        public static string IllustToJSON(this Pixeez.Objects.Work work)
        {
            return (JsonConvert.SerializeObject(IllustToJObject(work), Newtonsoft.Json.Formatting.Indented));
        }

        public static string IllustToJSON(this IEnumerable<Pixeez.Objects.Work> works)
        {
            return (JsonConvert.SerializeObject(IllustToJObject(works), Newtonsoft.Json.Formatting.Indented));
        }

        public static XmlDocument IllustToXmlDocument(this Pixeez.Objects.Work work)
        {
            XmlDocument result = null;
            try
            {
                var json = IllustToJSON(work);
                var lines = json.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();
                lines.Insert(0, "{");
                lines.Insert(1, "'?xml': { '@version': '1.0', '@standalone': 'no' },");
                lines.Insert(2, "'root': {");
                lines.Insert(3, "'illust': ");
                lines.Add("}");
                lines.Add("}");
                var xml_json = string.Join(Environment.NewLine, lines);
                result = JsonConvert.DeserializeXmlNode(xml_json);

                //result = new XmlDocument();
                //var id = result.CreateElement("id");
                //id.Value = $"{work.Id}";
                ////result.CreateAttribute("id");
                //result.AppendChild(id);

                //var date = result.CreateElement("date");
                //date.Value = $"{work.GetDateTime()}";
                ////result.CreateAttribute("id");
                //result.AppendChild(date);

                //var id = result.CreateElement("id");
                //id.Value = $"{work.Id}";
                ////result.CreateAttribute("id");
                //result.AppendChild(id);

                //var id = result.CreateElement("id");
                //id.Value = $"{work.Id}";
                ////result.CreateAttribute("id");
                //result.AppendChild(id);

                //var id = result.CreateElement("id");
                //id.Value = $"{work.Id}";
                ////result.CreateAttribute("id");
                //result.AppendChild(id);

                //var id = result.CreateElement("id");
                //id.Value = $"{work.Id}";
                ////result.CreateAttribute("id");
                //result.AppendChild(id);

                //var id = result.CreateElement("id");
                //id.Value = $"{work.Id}";
                ////result.CreateAttribute("id");
                //result.AppendChild(id);
            }
            catch (Exception ex) { ex.ERROR("IllustToXmlDocument"); }
            return (result);
        }

        public static XmlDocument IllustToXmlDocument(this IEnumerable<Pixeez.Objects.Work> works)
        {
            XmlDocument result = null;
            try
            {
                var json = IllustToJSON(works);
                var lines = json.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();
                lines.Insert(0, "{");
                lines.Insert(1, "'?xml': { '@version': '1.0', '@standalone': 'no' },");
                lines.Insert(2, "'root': {");
                lines.Insert(3, "'illust': ");
                lines.Add("}");
                lines.Add("}");
                var xml_json = string.Join(Environment.NewLine, lines);
                result = JsonConvert.DeserializeXmlNode(xml_json);
            }
            catch (Exception ex) { ex.ERROR("IllustToXmlDocument"); }
            return (result);
        }

        public static string IllustToXml(this Pixeez.Objects.Work work)
        {
            var xml = IllustToXmlDocument(work);
            var xml_out = FormatXML(xml);
            return (xml is XmlDocument ? xml_out : string.Empty);
        }

        public static string IllustToXml(this IEnumerable<Pixeez.Objects.Work> works)
        {
            var xml = IllustToXmlDocument(works);
            var xml_out = FormatXML(xml);
            return (xml is XmlDocument ? xml_out : string.Empty);
        }
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
                result = item.Illust.GetPreviewUrl(item.Index, large: setting.ShowLargePreview).GetImageId().IsSameIllust(hash) || item.Illust.GetOriginalUrl(item.Index).GetImageId().IsSameIllust(hash);
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
        public static bool InHistory(this PixivItem item)
        {
            return (Application.Current.InHistory(item));
        }

        public static bool InHistory(this Pixeez.Objects.Work illust)
        {
            return (Application.Current.InHistory(illust));
        }

        public static bool InHistory(this Pixeez.Objects.User user)
        {
            return (Application.Current.InHistory(user));
        }

        public static bool InHistory(this Pixeez.Objects.UserBase user)
        {
            return (Application.Current.InHistory(user));
        }

        public static void AddToHistory(this PixivItem item)
        {
            //Commands.AddToHistory.Execute(illust);
            var win = Application.Current.HistoryTitle().GetWindowByTitle();
            if (win is ContentWindow && win.Content is HistoryPage)
                (win.Content as HistoryPage).AddToHistory(item);
            else
                Application.Current.HistoryAdd(item);
        }

        public static void AddToHistory(this Pixeez.Objects.Work illust)
        {
            //Commands.AddToHistory.Execute(illust);
            var win = Application.Current.HistoryTitle().GetWindowByTitle();
            if (win is ContentWindow && win.Content is HistoryPage)
                (win.Content as HistoryPage).AddToHistory(illust);
            else
                Application.Current.HistoryAdd(illust);
        }

        public static void AddToHistory(this Pixeez.Objects.User user)
        {
            //Commands.AddToHistory.Execute(user);
            var win = Application.Current.HistoryTitle().GetWindowByTitle();
            if (win is ContentWindow && win.Content is HistoryPage)
                (win.Content as HistoryPage).AddToHistory(user);
            else
                Application.Current.HistoryAdd(user);
        }

        public static void AddToHistory(this Pixeez.Objects.UserBase user)
        {
            //Commands.AddToHistory.Execute(user);
            var win = Application.Current.HistoryTitle().GetWindowByTitle();
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
        public static async Task<Pixeez.Objects.Work> RefreshIllust(this Pixeez.Objects.Work Illust, Pixeez.Tokens tokens = null, bool restrict = true)
        {
            var result = Illust.Id != null ? await RefreshIllust(Illust.Id.Value, tokens, restrict: restrict) : Illust;
            if (result == null)
            {
                "404 (Not Found) or 503 (Service Unavailable)".ShowToast("INFO", tag: "RefreshIllust");
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
            catch (Exception ex) { ex.ERROR("RefreshIllust"); }
            return (result);
        }

        public static async Task<Pixeez.Objects.Work> RefreshIllust(this string IllustID, Pixeez.Tokens tokens = null, bool restrict = true)
        {
            Pixeez.Objects.Work result = null;
            try
            {
                long id = 0;
                if (!string.IsNullOrEmpty(IllustID) && long.TryParse(IllustID, out id))
                    result = await RefreshIllust(id, tokens, restrict: restrict);
            }
            catch (Exception ex) { ex.ERROR("RefreshIllust"); }
            return (result);
        }

        public static async Task<Pixeez.Objects.Work> RefreshIllust(this long IllustID, Pixeez.Tokens tokens = null, bool restrict = true)
        {
            Pixeez.Objects.Work result = null;
            if (IllustID < 0) return result;
            if (tokens == null) tokens = await ShowLogin();
            if (tokens == null) return result;
            try
            {
                dynamic illusts = setting.UsingAjaxAPI ? null : await tokens.GetWorksAsync(IllustID) ?? await tokens.GetIllustDetailAsync(IllustID);
                if (illusts == null) illusts = await IllustID.SearchIllustById(tokens);
                if (illusts is List<Pixeez.Objects.Work>)
                {
                    foreach (Pixeez.Objects.Work illust in illusts)
                    {
                        illust.Cache();
                        if (illust.Id == IllustID)
                        {
                            result = illust;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO", tag: "RefreshIllust");
                else ex.ERROR("RefreshIllust");
            }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserBase> RefreshUser(this Pixeez.Objects.Work Illust, Pixeez.Tokens tokens = null, bool restrict = true)
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
            catch (Exception ex) { ex.ERROR("RefreshUser"); }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserBase> RefreshUser(this Pixeez.Objects.UserBase User, Pixeez.Tokens tokens = null, bool restrict = true)
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
                        u.IsFollowed = user is Pixeez.Objects.NewUser ? (user as Pixeez.Objects.NewUser).is_followed : (user as Pixeez.Objects.User).IsFollowed;
                        u.IsFollower = user is Pixeez.Objects.NewUser ? u.IsFollower : (user as Pixeez.Objects.User).IsFollower;
                        u.IsFollowing = user is Pixeez.Objects.NewUser ? u.IsFollower : (user as Pixeez.Objects.User).IsFollowing;
                        u.IsFriend = user is Pixeez.Objects.NewUser ? u.IsFriend : (user as Pixeez.Objects.User).IsFriend;
                        u.IsPremium = user is Pixeez.Objects.NewUser ? u.IsPremium : (user as Pixeez.Objects.User).IsPremium;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("RefreshUser"); }
            return (user);
        }

        public static async Task<Pixeez.Objects.UserBase> RefreshUser(this string UserID, Pixeez.Tokens tokens = null, bool restrict = true)
        {
            Pixeez.Objects.UserBase result = null;
            if (!string.IsNullOrEmpty(UserID))
            {
                try
                {
                    result = await RefreshUser(Convert.ToInt32(UserID), tokens);
                }
                catch (Exception ex) { ex.ERROR("RefreshUser"); }
            }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserBase> RefreshUser(this long UserID, Pixeez.Tokens tokens = null, bool restrict = true)
        {
            Pixeez.Objects.UserBase result = null;
            if (UserID < 0) return (result);
            setting = Application.Current.LoadSetting();
            var force = UserID == 0 && !(setting.MyInfo is Pixeez.Objects.User) ? true : false;
            if (tokens == null) tokens = await ShowLogin(force);
            if (tokens == null) return (result);
            try
            {
                dynamic users = setting.UsingAjaxAPI ? null : await tokens.GetUsersAsync(UserID);
                if (users == null) users = await UserID.SearchUserById(tokens);
                foreach (var user in users)
                {
                    var ub = user as Pixeez.Objects.UserBase;
                    ub.Cache();
                    if (ub.Id.Value == UserID) result = ub;
                    //{
                    //    if (user is Pixeez.Objects.User)
                    //        result = user;
                    //    else if (user is Pixeez.Objects.NewUser)
                    //    {
                    //        var nu = user as Pixeez.Objects.NewUser;
                    //        result = new Pixeez.Objects.User()
                    //        {
                    //            Id = nu.Id,
                    //            Name = nu.Name,
                    //            IsFollowed = nu.is_followed,
                    //            ProfileImageUrls = new Pixeez.Objects.ProfileImageUrls()
                    //            {
                    //                Px16x16 = nu.profile_image_urls.Small,
                    //                Px50x50 = nu.profile_image_urls.Small,
                    //                medium = nu.profile_image_urls.Px128x128,
                    //                Px170x170 = nu.profile_image_urls.Medium
                    //            },
                    //            Account = nu.Account,
                    //            Email = nu.Email,
                    //        };
                    //    }
                    //}
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO", tag: "RefreshUser");
                else ex.ERROR("RefreshUser");
            }
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
                catch (Exception ex) { ex.ERROR("RefreshUserInfo"); }
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
            catch (Exception ex)
            {
                if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO", tag: "RefreshUserInfo");
                else ex.ERROR("RefreshUserInfo");
            }
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

            var bookmarkstate = await tokens.GetBookMarkedDetailAsync(illust.Id ?? -1);
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
                illust.Cache();
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

            setting = Application.Current.LoadSetting();

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            bool ret = false;
            try
            {
                var mode = pub ? "public" : "private";
                ret = await tokens.AddMyFavoriteWorksAsync((long)illust.Id, setting.FavBookmarkWithTags ? illust.Tags : null, mode);
                if (!ret) return (result);
            }
            catch (Exception ex) { ex.ERROR("AddMyFavoriteWorksAsync"); }
            finally
            {
                try
                {
                    if (ret) await RefreshIllustBookmarkState(illust);
                    if (illust != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.Work>(illust.IsLiked() || illust.IsBookMarked(), illust);
                        var info = "Liked";
                        var title = ret && result.Item1 ? "Succeed" : "Failed";
                        var fail = ret && result.Item1 ? "is" : "isn't";
                        var pub_like = pub ? "Public" : "Private";
                        $"Illust \"{illust.Title}\" {fail} {pub_like} {info}!".ShowToast($"{title}", illust.GetThumbnailUrl(), title, pub_like);
                        $"Illust \"{illust.Title}\" {fail} {pub_like} {info}!".INFO("LikeIllust");
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO", tag: "LikeIllust");
                    else ex.ERROR("LikeIllust");
                }
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
                if (result && item.IsDownloaded) Commands.TouchMeta.Execute(item.Illust);
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

            bool ret = false;
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
                            ret = true;
                            break;
                        }
                    }
                }
                else if (works == null) ret = true;
                //ret = await tokens.DeleteMyFavoriteWorksAsync((long)illust.Id, "private") != null;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO", tag: "UnLikeIllust");
                else ex.ERROR("DeleteMyFavoriteWorksAsync");
            }
            finally
            {
                try
                {
                    if (ret) await illust.RefreshIllustBookmarkState();
                    if (illust != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.Work>(illust.IsLiked(), illust);
                        var info = "Unliked";
                        var title = ret && result.Item1 ? "Failed" : "Succeed";
                        var fail = ret && result.Item1 ?  "isn't" : "is";
                        $"Illust \"{illust.Title}\" {fail} {info}!".ShowToast(title, illust.GetThumbnailUrl(), title);
                        $"Illust \"{illust.Title}\" {fail} {info}!".INFO("UnLikeIllust");
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO", tag: "UnLikeIllust");
                    else ex.ERROR("RefreshIllust");
                }
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
                if (result && item.IsDownloaded) Commands.TouchMeta.Execute(item.Illust);
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
                if (result && item.IsDownloaded) Commands.TouchMeta.Execute(item.Illust);
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

            bool ret = false;
            try
            {
                ret = await tokens.AddFollowUser(user.Id ?? -1, pub ? "public" : "private");
                if (!ret) return (result);
            }
            catch (Exception ex) { ex.ERROR("LikeUser"); }
            finally
            {
                try
                {
                    if (ret) user = await user.RefreshUser();
                    if (user != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);
                        var info = "Liked";
                        var title = ret && result.Item1 ? "Succeed" : "Failed";
                        var fail = ret && result.Item1 ?  "is" : "isn't";
                        var pub_like = pub ? "Public" : "Private";
                        $"User \"{user.Name ?? string.Empty}\" {fail} {pub_like} {info}!".ShowToast(title, user.GetAvatarUrl(), title, pub_like);
                        $"User \"{user.Name ?? string.Empty}\" {fail} {pub_like} {info}!".INFO("LikeUser");
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO", tag: "LikeUser");
                    else ex.ERROR("LikeUser");
                }
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

            bool ret = false;
            try
            {
                ret = await tokens.DeleteFollowUser(user.Id ?? -1);
                if (!ret) return (result);
            }
            catch (Exception ex) { ex.ERROR("UnLikeUser"); }
            finally
            {
                try
                {
                    if (ret) user = await user.RefreshUser();
                    if (user != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);
                        var info = "Unliked";
                        var title = ret && result.Item1 ? "Failed" : "Succeed";
                        var fail = ret && result.Item1 ?  "isn't" : "is";
                        $"User \"{user.Name ?? string.Empty}\" {fail} {info}!".ShowToast(title, user.GetAvatarUrl(), title);
                        $"User \"{user.Name ?? string.Empty}\" {fail} {info}!".INFO("UnLikeUser");
                    }
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO", tag: "UnLikeUser");
                    else ex.ERROR("UnLikeUser");
                }
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

        //public static void Cache(this Pixeez.Objects.User user)
        //{
        //    if (user is Pixeez.Objects.UserBase)
        //        (user as Pixeez.Objects.UserBase).Cache();
        //}

        //public static void Cache(this Pixeez.Objects.NewUser user)
        //{
        //    if (user is Pixeez.Objects.UserBase)
        //        (user as Pixeez.Objects.UserBase).Cache();
        //}

        public static void Cache(this Pixeez.Objects.Work illust)
        {
            if (illust is Pixeez.Objects.Work && (illust.Id ?? -1) > 0)
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
                    if ((illust_old.Metadata != null && illust_old.Metadata.Pages != null) &&
                        (illust.Metadata == null || illust.Metadata.Pages == null))
                        illust.Metadata = illust_old.Metadata;
                }
                IllustCache[illust.Id] = illust;
            }
        }

        //public static void Cache(this Pixeez.Objects.IllustWork illust)
        //{
        //    if (illust is Pixeez.Objects.Work)
        //        (illust as Pixeez.Objects.Work).Cache();
        //}

        //public static void Cache(this Pixeez.Objects.NormalWork illust)
        //{
        //    if (illust is Pixeez.Objects.Work)
        //        (illust as Pixeez.Objects.Work).Cache();
        //}

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

        #region Get Illust/User/UserInfo
        public static async Task<Pixeez.Objects.Work> GetIllust(this long id, Pixeez.Tokens tokens = null)
        {
            var illust = id.FindIllust();
            if (!(illust is Pixeez.Objects.Work)) illust = await RefreshIllust(id, tokens);
            if (IllustCache.ContainsKey(id)) return (illust);
            else return (null);
        }

        public static async Task<Pixeez.Objects.Work> GetIllust(this long? id, Pixeez.Tokens tokens = null)
        {
            var illust = id.FindIllust();
            if (!(illust is Pixeez.Objects.Work)) illust = await RefreshIllust(id.Value, tokens);
            if (id != null && IllustCache.ContainsKey(id)) return (illust);
            else return (null);
        }

        public static async Task<Pixeez.Objects.Work> GetIllust(this string id, Pixeez.Tokens tokens = null)
        {
            long idv = 0;
            if (long.TryParse(id, out idv)) return (await GetIllust(idv, tokens));
            else return (null);
        }

        public static async Task<Pixeez.Objects.Work> GetIllust(this Pixeez.Objects.Work work, Pixeez.Tokens tokens = null)
        {
            return (await GetIllust(work, tokens));
        }

        public static async Task<Pixeez.Objects.UserBase> GetUser(this long id, Pixeez.Tokens tokens = null)
        {
            var user = id.FindUser();
            if (!(user is Pixeez.Objects.UserBase)) user = await RefreshUser(id, tokens);
            if (UserCache.ContainsKey(id)) return (user);
            else return (null);
        }

        public static async Task<Pixeez.Objects.UserBase> GetUser(this long? id, Pixeez.Tokens tokens = null)
        {
            var user = id.FindUser();
            if (!(user is Pixeez.Objects.UserBase)) user = await RefreshUser(id.Value, tokens);
            if (id != null && UserCache.ContainsKey(id)) return (user);
            else return (null);
        }

        public static async Task<Pixeez.Objects.UserBase> GetUser(this string id, Pixeez.Tokens tokens = null)
        {
            long idv = 0;
            if (long.TryParse(id, out idv)) return (await GetUser(idv, tokens));
            else return (null);
        }

        public static async Task<Pixeez.Objects.UserBase> GetUser(this Pixeez.Objects.UserBase user, Pixeez.Tokens tokens = null)
        {
            return (await GetUser(user.Id, tokens));
        }

        public static async Task<Pixeez.Objects.UserInfo> GetUserInfo(this long id, Pixeez.Tokens tokens = null)
        {
            var userinfo = id.FindUserInfo();
            if (!(userinfo is Pixeez.Objects.UserInfo)) userinfo = await RefreshUserInfo(id, tokens);
            if (UserInfoCache.ContainsKey(id)) return (userinfo);
            else return (null);
        }

        public static async Task<Pixeez.Objects.UserInfo> GetUserInfo(this long? id, Pixeez.Tokens tokens = null)
        {
            var userinfo = id.FindUserInfo();
            if (!(userinfo is Pixeez.Objects.UserInfo)) userinfo = await RefreshUserInfo(id, tokens);
            if (id != null && UserInfoCache.ContainsKey(id)) return (userinfo);
            else return (null);
        }

        public static async Task<Pixeez.Objects.UserInfo> GetUserInfo(this string id, Pixeez.Tokens tokens = null)
        {
            long idv = 0;
            if (long.TryParse(id, out idv)) return (await GetUserInfo(idv, tokens));
            else return (null);
        }

        public static async Task<Pixeez.Objects.UserInfo> GetUserInfo(this Pixeez.Objects.UserBase user, Pixeez.Tokens tokens = null)
        {
            return (await GetUserInfo(user.Id, tokens));
        }

        public static Func<Pixeez.Objects.UserBase, int> GetTotalIllust = (user) =>
        {
            var result = -1;
            if (user is Pixeez.Objects.UserBase && user.Id != null && user.Id.HasValue)
            {
                var prof = user.FindUserInfo();
                if(prof is Pixeez.Objects.UserInfo)
                {
                    result = prof.profile.total_illusts + prof.profile.total_manga;
                }
            }
            return (result);
        };
        #endregion

        #region Sync Illust/User Like State
        public static void UpdateLikeStateAsync(string illustid = default(string), bool is_user = false)
        {
            int id = -1;
            int.TryParse(illustid, out id);
            UpdateLikeStateAsync(id);
        }

        public static void UpdateLikeStateAsync(this bool is_user, int illustid = -1)
        {
            UpdateLikeStateAsync(illustid, is_user);
        }

        public static async void UpdateLikeStateAsync(int illustid = -1, bool is_user = false)
        {
            await new Action(() =>
            {
                foreach (var win in Application.Current.Windows)
                {
                    //if (!is_user && illustid > 0)
                    //{
                    //    var illust = FindIllust(illustid);
                    //    if (illust.IsWork()) Commands.TouchMeta.Execute(illust);
                    //}

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

        #region UI Element Related
        public static string GetUid(this object obj)
        {
            string result = string.Empty;

            try { if (obj is UIElement) result = (obj as UIElement).Uid; }
            catch (Exception ex) { ex.ERROR("GetUid"); }

            return (result);
        }

        public static UIElement GetContextMenuHost(this UIElement item)
        {
            UIElement result = null;
            if (item is MenuItem)
            {
                var parent = (item as MenuItem).TryFindParent<ContextMenu>();
                if (parent is ContextMenu)
                {
                    result = parent.PlacementTarget;
                }
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

        public static void UpdateTheme(this Window win, Image icon = null)
        {
            try
            {
                new Action(() =>
                {
                    win.Icon = icon == null ? Application.Current.GetIcon().Source : icon.Source;

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
                        else if (win.Title.Equals(Application.Current.DropboxTitle(), StringComparison.CurrentCultureIgnoreCase))
                        {
                            win.Background = Theme.AccentBrush;
                            if (icon is Image)
                            {
                                win.Content = icon;
                                win.Icon = icon.Source;
                            }
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
                        if (win is MetroWindow) win.UpdateTheme(img);
                    }
                }).Invoke(async: false);
            }
            catch (Exception ex) { ex.ERROR("UpdateTheme"); }
        }

        public static bool IsVisible(this Window win)
        {
            var result = false;
            if (win is MetroWindow)
            {
                result = win.Dispatcher.Invoke(() =>
                {
                    return (win.IsActive || (win.IsShown() && win.WindowState != WindowState.Minimized));
                });
            }
            return (result);
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
            if (element is UIElement) (element as UIElement).Show(parent);
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
                if ((button.Parent is StackPanel) && (button.Parent as StackPanel).Name.StartsWith("ActionBar") && button.ActualWidth >= 32)
                    button.Foreground = Theme.IdealForegroundBrush;

                if ((button.Parent is StackPanel) && (button.Parent as StackPanel).Name.StartsWith("ZoomBar") && button.ActualWidth >= 32)
                    button.Foreground = Theme.IdealForegroundBrush;

                if ((button.Parent is StackPanel) && (button.Parent as StackPanel).Name.StartsWith("TransformBar") && button.ActualWidth >= 32)
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
                if ((button.Parent is StackPanel) && (button.Parent as StackPanel).Name.StartsWith("ActionBar") && button.ActualWidth >= 32 && button.IsEnabled)
                    button.Foreground = Theme.AccentBrush;

                if ((button.Parent is StackPanel) && (button.Parent as StackPanel).Name.StartsWith("ZoomBar") && button.ActualWidth >= 32)
                    button.Foreground = Theme.AccentBrush;

                if ((button.Parent is StackPanel) && (button.Parent as StackPanel).Name.StartsWith("TransformBar") && button.ActualWidth >= 32)
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

        public static IEnumerable<string> GetSuggestList(this string text, string original = "")
        {
            List<string> result = new List<string>();

            if (!string.IsNullOrEmpty(text))
            {
                var patten = @"^(User|Fuzzy|Tag|Fuzzy Tag): ?";
                text = Regex.Replace(text, patten, "", RegexOptions.IgnoreCase).Trim();

                if (Regex.IsMatch(text, @"^\d+$", RegexOptions.IgnoreCase))
                {
                    result.Add($"IllustID: {text}");
                    result.Add($"UserID: {text}");
                }
                if (string.IsNullOrEmpty(original))
                {
                    result.Add($"User: {text}");
                    result.Add($"Fuzzy: {text}");
                    result.Add($"Tag: {text}");
                    result.Add($"Fuzzy Tag: {text}");
                }
                else
                {
                    original = Regex.Replace(original, patten, "", RegexOptions.IgnoreCase).Trim();
                    text = original.Trim();
                    result.Add($"User: {text}");
                    result.Add($"Fuzzy: {text}");
                    result.Add($"Tag: {text}");
                    result.Add($"Fuzzy Tag: {text}");
                }
                result = result.Distinct().ToList();
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
                        content.GetSuggestList(SearchBox.Text).ToList().ForEach(t => auto_suggest_list.Add(t));
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
            var wins = Application.Current.Windows.OfType<MetroWindow>().Where(w => !w.Title.Equals(Application.Current.DropboxTitle(), StringComparison.CurrentCultureIgnoreCase)).ToList();
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
            if (window is MetroWindow)
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
                        ex.ERROR("ActiveWindow");
                        window.WindowState = WindowState.Normal;
                    }
                }

                if (window.WindowState != WindowState.Minimized)
                {
                    window.Show();
                    window.Activate();
                }
            }
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

                Window dm = GetWindowByTitle(Application.Current.DownloadTitle());
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

                await new Action(async () =>
                {
                    INotificationDialogService _dialogService = new NotificationDialogService();
                    NotificationConfiguration cfgDefault = NotificationConfiguration.DefaultConfiguration;
                    NotificationConfiguration cfg = new NotificationConfiguration(
                    //new TimeSpan(0, 0, 30), 
                    TimeSpan.FromSeconds(setting.ToastTimeout),
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
                    _dialogService.DoEvents();
                    await Task.Delay(1);
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
                if (main is MainWindow && main.IsVisible())
                {
                    await new Action(async () =>
                    {
                        INotificationDialogService _dialogService = new NotificationDialogService();
                        NotificationConfiguration cfgDefault = NotificationConfiguration.DefaultConfiguration;
                        NotificationConfiguration cfg = new NotificationConfiguration(
                            //new TimeSpan(0, 0, 30), 
                            TimeSpan.FromSeconds(setting.ToastTimeout),
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
                        _dialogService.DoEvents();
                        await Task.Delay(1);
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
                if (main is MainWindow && main.IsVisible())
                {
                    setting = Application.Current.LoadSetting();

                    await new Action(async () =>
                    {
                        INotificationDialogService _dialogService = new NotificationDialogService();
                        NotificationConfiguration cfgDefault = NotificationConfiguration.DefaultConfiguration;
                        NotificationConfiguration cfg = new NotificationConfiguration(
                            TimeSpan.FromSeconds(setting.ToastTimeout),
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
                        _dialogService.DoEvents();
                        await Task.Delay(1);
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

    public static class TaskExtensions
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

        public static bool IsCanceled(this Exception ex, bool manual = false)
        {
            return ((!manual && ex is TaskCanceledException) || ex is OperationCanceledException || ex is HttpRequestException || ex is WebException);
        }

        public static bool IsNetworkError(this Exception ex)
        {
            return (ex is ArgumentNullException || ex is ArgumentOutOfRangeException || ex is NotSupportedException || ex is ObjectDisposedException || ex is HttpRequestException || ex is WebException || ex is IOException);
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
        private static string NormalizationFileName(string file, int padding = 16)
        {
            var f = Path.GetFileName(file);
            f = Regex.IsMatch(f, @"_(master|ugoira|p)?\d+\.(jpg|gif|png)", RegexOptions.IgnoreCase) ? file : Path.ChangeExtension(file, $"_0.{Path.GetExtension(file)}");
            return (Regex.Replace(f, @"\d+", m => m.Value.PadLeft(padding, '0')));
        }

        public static IList<string> NaturalSort(this IList<string> list, int padding = 16)
        {
            try
            {
                return (list is IList<string> ? list.OrderBy(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(padding, '0'))).ToList() : list);
            }
            catch (Exception ex) { ex.ERROR("NaturalSort"); return (list); }
        }

        public static IList<FileInfo> NaturalSort(this IList<FileInfo> list, int padding = 16)
        {
            try
            {
                return (list is IList<FileInfo> ? list.OrderBy(x => NormalizationFileName(x.FullName, padding)).ToList() : list);
            }
            catch (Exception ex) { ex.ERROR("NaturalSort"); return (list); }
        }

        public static IEnumerable<string> NaturalSort(this IEnumerable<string> list, int padding = 16)
        {
            try
            {
                return (list is IEnumerable<string> ? list.OrderBy(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(padding, '0'))) : list);
            }
            catch (Exception ex) { ex.ERROR("NaturalSort"); return (list); }
        }

        public static IEnumerable<FileInfo> NaturalSort(this IEnumerable<FileInfo> list, int padding = 16)
        {
            try
            {
                return (list is IEnumerable<FileInfo> ? list.OrderBy(x => NormalizationFileName(x.FullName, padding)) : list);
            }
            catch (Exception ex) { ex.ERROR("NaturalSort"); return (list); }
        }

        public static bool CanRelease(this SemaphoreSlim ss, int? max = null)
        {
            max = max ?? -1;
            if (max <= 0)
                return (ss is SemaphoreSlim && ss.CurrentCount >= 0);
            else
                return (ss is SemaphoreSlim && ss.CurrentCount >= 0 && ss.CurrentCount < max);
        }

        public static void Release(this SemaphoreSlim ss, bool all = false, int? max = null)
        {
            try
            {
                max = max ?? -1;
                if (CanRelease(ss, max))
                {
                    if (all)
                    {
                        if (max > 0) ss.Release(max.Value - ss.CurrentCount);
                        else { while (ss.Release() == -1) ; }
                    }
                    else { ss.Release(); }
                }
            }
            catch (SemaphoreFullException) { }
            catch (Exception ex) { ex.ERROR("SemaphoreSlimRelease"); }
        }

        public static void Dispose(this Image image)
        {
            try
            {
                if (image is Image)
                {
                    //image.AppDispatcher().Invoke(() =>
                    image.Dispatcher.Invoke(() =>
                    {
                        image.Source = null;
                        image.UpdateLayout();
                    });
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

        public static sbyte Between(this sbyte value, sbyte range_l, sbyte range_h)
        {
            if (range_l < range_h)
                return (Math.Max(range_l, Math.Min(range_h, value)));
            else if (range_l > range_h)
                return (Math.Max(range_h, Math.Min(range_l, value)));
            else
                return (range_l);
        }

        public static byte Between(this byte value, byte range_l, byte range_h)
        {
            if (range_l < range_h)
                return (Math.Max(range_l, Math.Min(range_h, value)));
            else if (range_l > range_h)
                return (Math.Max(range_h, Math.Min(range_l, value)));
            else
                return (range_l);
        }

        public static short Between(this short value, short range_l, short range_h)
        {
            if (range_l < range_h)
                return (Math.Max(range_l, Math.Min(range_h, value)));
            else if (range_l > range_h)
                return (Math.Max(range_h, Math.Min(range_l, value)));
            else
                return (range_l);
        }

        public static ushort Between(this ushort value, ushort range_l, ushort range_h)
        {
            if (range_l < range_h)
                return (Math.Max(range_l, Math.Min(range_h, value)));
            else if (range_l > range_h)
                return (Math.Max(range_h, Math.Min(range_l, value)));
            else
                return (range_l);
        }

        public static int Between(this int value, int range_l, int range_h)
        {
            if (range_l < range_h)
                return (Math.Max(range_l, Math.Min(range_h, value)));
            else if (range_l > range_h)
                return (Math.Max(range_h, Math.Min(range_l, value)));
            else
                return (range_l);
        }

        public static uint Between(this uint value, uint range_l, uint range_h)
        {
            if (range_l < range_h)
                return (Math.Max(range_l, Math.Min(range_h, value)));
            else if (range_l > range_h)
                return (Math.Max(range_h, Math.Min(range_l, value)));
            else
                return (range_l);
        }

        public static long Between(this long value, long range_l, long range_h)
        {
            if (range_l < range_h)
                return (Math.Max(range_l, Math.Min(range_h, value)));
            else if (range_l > range_h)
                return (Math.Max(range_h, Math.Min(range_l, value)));
            else
                return (range_l);
        }

        public static ulong Between(this ulong value, ulong range_l, ulong range_h)
        {
            if (range_l < range_h)
                return (Math.Max(range_l, Math.Min(range_h, value)));
            else if (range_l > range_h)
                return (Math.Max(range_h, Math.Min(range_l, value)));
            else
                return (range_l);
        }

        public static decimal Between(this decimal value, decimal range_l, decimal range_h)
        {
            if (range_l < range_h)
                return (Math.Max(range_l, Math.Min(range_h, value)));
            else if (range_l > range_h)
                return (Math.Max(range_h, Math.Min(range_l, value)));
            else
                return (range_l);
        }

        public static float Between(this float value, float range_l, float range_h)
        {
            if (range_l < range_h)
                return (Math.Max(range_l, Math.Min(range_h, value)));
            else if (range_l > range_h)
                return (Math.Max(range_h, Math.Min(range_l, value)));
            else
                return (range_l);
        }

        public static double Between(this double value, double range_l, double range_h)
        {
            if (range_l < range_h)
                return (Math.Max(range_l, Math.Min(range_h, value)));
            else if (range_l > range_h)
                return (Math.Max(range_h, Math.Min(range_l, value)));
            else
                return (range_l);
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

        public static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        public static T FindVisualChild<T>(this DependencyObject parent, DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T && child == obj)
                    return (T)child;
                else
                {
                    T childOfChild = child.FindVisualChild<T>(obj);
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
        
        //public static ModifierKeys GetModifiderKeys(this Application app)
        //{
        //    var result = Application.Current.Dispatcher.BeginInvoke(new Func<ModifierKeys>(delegate
        //    {
        //        return (Keyboard.Modifiers);
        //    }));
        //   return (result.Dispatcher.re);
        //}
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

        public static double Distance(this Point src, Point dst)
        {
            return (Math.Sqrt(Math.Pow(src.X - dst.X, 2) + Math.Pow(src.Y - dst.Y, 2)));
        }
        #endregion
    }

}
