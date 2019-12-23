using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using Newtonsoft.Json;
using PixivWPF.Pages;
using Prism.Commands;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WPFNotification.Core.Configuration;
using WPFNotification.Model;
using WPFNotification.Services;


namespace PixivWPF.Common
{
    public enum PixivPage
    {
        None,
        TrendingTags,
        WorkSet,
        Recommanded,
        Latest,
        My,
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
        RankingYear
    }

    public enum ToastType { DOWNLOAD=0, OK, OKCANCEL, YES, NO, YESNO };

    public class SimpleCommand : ICommand
    {
        public Predicate<object> CanExecuteDelegate { get; set; }
        public Action<object> ExecuteDelegate { get; set; }

        public bool CanExecute(object parameter)
        {
            if (CanExecuteDelegate != null)
                return CanExecuteDelegate(parameter);
            return true; // if there is no can execute default to true
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter)
        {
            ExecuteDelegate?.Invoke(parameter);
        }
    }

    public class DPI
    {
        public double X { get; } = 96.0;
        public double Y { get; } = 96.0;

        public DPI()
        {
            var dpi = BySystemParameters();
            X = dpi.X;
            Y = dpi.Y;
        }

        public DPI(double x, double y)
        {
            X = x;
            Y = y;
        }

        public DPI(Visual visual)
        {
            try
            {
                var dpi = FromVisual(visual);
                X = dpi.X;
                Y = dpi.Y;
            }
            catch (Exception) {}
        }

        public static DPI FromVisual(Visual visual)
        {
            var source = PresentationSource.FromVisual(visual);
            var dpiX = 96.0;
            var dpiY = 96.0;
            try
            {
                if (source?.CompositionTarget != null)
                {
                    dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                    dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
                }
            }
            catch (Exception) { }
            return new DPI(dpiX, dpiY);
        }

        public static DPI BySystemParameters()
        {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            var dpiXProperty = typeof(SystemParameters).GetProperty("DpiX", flags);
            //var dpiYProperty = typeof(SystemParameters).GetProperty("DpiY", flags);
            var dpiYProperty = typeof(SystemParameters).GetProperty("Dpi", flags);
            var dpiX = 96.0;
            var dpiY = 96.0;
            try
            {
                if (dpiXProperty != null) { dpiX = (int)dpiXProperty.GetValue(null, null); }
                if (dpiYProperty != null) { dpiY = (int)dpiYProperty.GetValue(null, null); }
            }
            catch (Exception) { }
            return new DPI(dpiX, dpiY);
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

    public static class CommonHelper
    {
        private const int WIDTH_MIN = 720;
        private const int HEIGHT_MIN = 520;
        private const int HEIGHT_DEF = 900;
        private const int HEIGHT_MAX = 1008;

        private static Setting setting = Setting.Instance == null ? Setting.Load() : Setting.Instance;
        private static CacheImage cache = new CacheImage();
        public static Dictionary<long?, Pixeez.Objects.Work> IllustCache = new Dictionary<long?, Pixeez.Objects.Work>();
        public static Dictionary<long?, Pixeez.Objects.UserBase> UserCache = new Dictionary<long?, Pixeez.Objects.UserBase>();

        public static DateTime SelectedDate { get; set; } = DateTime.Now;

        private static List<string> ext_imgs = new List<string>() { ".png", ".jpg" };
        internal static char[] trim_char = new char[] { ' ', ',', '.', '/', '\\', '\r', '\n', ':', ';' };
        internal static string[] trim_str = new string[] { Environment.NewLine };

        public static ICommand Cmd_DatePicker { get; } = new DelegateCommand<Point?>(obj =>
        {
            if (obj.HasValue)
            {
                var page = new DateTimePicker();
                var viewer = new MetroWindow();
                viewer.Icon = "Resources/pixiv-icon.ico".MakePackUri().GetThemedImage().Source;
                viewer.ShowMinButton = false;
                viewer.ShowMaxRestoreButton = false;
                viewer.ResizeMode = ResizeMode.NoResize;
                viewer.Width = 320;
                viewer.Height = 240;
                viewer.Top = obj.Value.Y + 4;
                viewer.Left = obj.Value.X - 64;
                viewer.Content = page;
                viewer.Title = $"Pick Date";
                viewer.KeyUp += page.Page_KeyUp;
                viewer.MouseDown += page.Page_MouseDown;
                viewer.ShowDialog();
            }
        });

        public static ICommand Cmd_CopyIllustIDs { get; } = new DelegateCommand<object>(obj =>
        {
            if (obj is ImageListGrid)
            {
                var list = obj as ImageListGrid;
                var ids = new  List<string>();
                foreach (var item in list.SelectedItems)
                {
                    if (list.Name.Equals("RelativeIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                        list.Name.Equals("ResultIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                        list.Name.Equals("FavoriteIllusts", StringComparison.CurrentCultureIgnoreCase))
                    {
                        ids.Add($"{item.ID}");
                    }
                }
                Clipboard.SetText(string.Join("\n", ids));
            }
            else if (obj is ImageItem)
            {
                var item = obj as ImageItem;
                if (item.Illust is Pixeez.Objects.Work)
                {
                    Clipboard.SetText(item.ID);
                }
            }
            else if (obj is string)
            {
                var id = (obj as string).ParseLink().ParseID();
                if (!string.IsNullOrEmpty(id)) Clipboard.SetText(id);
            }
        });

        public static ICommand Cmd_OpenIllust { get; } = new DelegateCommand<dynamic>(obj =>
        {
            DoEvents();
            if (obj is ImageListGrid)
            {
                Cmd_OpenItems.Execute(obj);
            }
            else if (obj is ImageItem)
            {
                Cmd_OpenItem.Execute(obj);
            }
            else if (obj is Pixeez.Objects.Work)
            {
                Cmd_OpenWork.Execute(obj);
            }
            else if (obj is Pixeez.Objects.UserBase)
            {
                Cmd_OpenUser.Execute(obj);
            }
            else if (obj is string)
            {
                Cmd_Search.Execute(obj as string);
            }
            DoEvents();
        });

        public static ICommand Cmd_OpenItems { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is ImageListGrid)
            {
                var list = obj as ImageListGrid;
                foreach (var item in list.SelectedItems)
                {
                    if (list.Name.Equals("RelativeIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                        list.Name.Equals("ResultIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                        list.Name.Equals("FavoriteIllusts", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Cmd_OpenItem.Execute(item);
                    }
                    else if (list.Name.Equals("SubIllusts", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Cmd_OpenWorkPreview.Execute(item);
                    }
                    CommonHelper.DoEvents();
                }
            }
        });

        public static ICommand Cmd_OpenItem { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is ImageItem)
            {
                var item = obj as ImageItem;
                switch (item.ItemType)
                {
                    case ImageItemType.Work:
                        item.IsDownloaded = item.Illust == null ? false : item.Illust.IsPartDownloadedAsync();
                        Cmd_OpenWork.Execute(item.Illust);
                        break;
                    case ImageItemType.User:
                        Cmd_OpenUser.Execute(item.User);
                        break;
                    default:
                        Cmd_OpenIllust.Execute(item.Illust);
                        break;
                }
            }
        });

        public static ICommand Cmd_OpenWork { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is Pixeez.Objects.Work)
            {
                var illust = obj as Pixeez.Objects.Work;
                var title = $"ID: {illust.Id}, {illust.Title}";
                if (title.ActiveByTitle()) return;

                var item = illust.IllustItem();
                if (item is ImageItem)
                {
                    var page = new IllustDetailPage() { Tag = item };
                    page.UpdateDetail(item);

                    var viewer = new ContentWindow()
                    {
                        Title = title,
                        Width = WIDTH_MIN,
                        Height = HEIGHT_DEF,
                        MinWidth = WIDTH_MIN,
                        MinHeight = HEIGHT_MIN,
                        Content = page
                    };
                    viewer.Show();
                    CommonHelper.DoEvents();
                }
            }
        });

        public static ICommand Cmd_OpenWorkPreview { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is ImageItem && (obj.ItemType == ImageItemType.Work || obj.ItemType == ImageItemType.Manga))
            {
                var item = obj as ImageItem;
                item.IsDownloaded = item.Illust == null ? false : item.Illust.IsPartDownloadedAsync();

                var title = $"ID: {item.ID}, {item.Subject} - ";
                if (title.ActiveByTitle()) return;

                var page = new IllustImageViewerPage() { Tag = item };
                page.UpdateDetail(item);

                var viewer = new ContentWindow()
                {
                    Title = $"{title}{item.BadgeValue}/{item.Count}",
                    Width = WIDTH_MIN,
                    Height = HEIGHT_DEF,
                    MinWidth = WIDTH_MIN,
                    MinHeight = HEIGHT_MIN,
                    Content = page
                };
                viewer.Show();
                CommonHelper.DoEvents();
            }
        });

        public static ICommand Cmd_OpenUser { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is Pixeez.Objects.UserBase)
            {
                var user = obj as Pixeez.Objects.UserBase;
                var title = $"User: {user.Name} / {user.Id} / {user.Account}";
                if (title.ActiveByTitle()) return;

                var page = new IllustDetailPage() { Tag = obj };
                page.UpdateDetail(user);

                var viewer = new ContentWindow()
                {
                    Title = title,
                    Width = WIDTH_MIN,
                    Height = HEIGHT_DEF,
                    MinWidth = WIDTH_MIN,
                    MinHeight = HEIGHT_MIN,
                    Content = page
                };
                viewer.Show();
                CommonHelper.DoEvents();
            }
        });

        public static ICommand Cmd_SaveIllust { get; } = new DelegateCommand<object>(obj =>
        {
            if (obj is ImageItem)
            {
                var item = obj as ImageItem;
                var illust = item.Illust;
                var dt = illust.GetDateTime();
                var is_meta_single_page = illust.PageCount==1 ? true : false;
                if (item.Tag is Pixeez.Objects.MetaPages)
                {
                    var pages = item.Tag as Pixeez.Objects.MetaPages;
                    var url = pages.GetOriginalUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        url.SaveImage(pages.GetThumbnailUrl(), dt, is_meta_single_page);
                    }
                }
                else if (item.Tag is Pixeez.Objects.Page)
                {
                    var pages = item.Tag as Pixeez.Objects.Page;
                    var url = pages.GetOriginalUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        url.SaveImage(pages.GetThumbnailUrl(), dt, is_meta_single_page);
                    }
                }
                else if (item.Illust is Pixeez.Objects.Work)
                {
                    var url = illust.GetOriginalUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        url.SaveImage(illust.GetThumbnailUrl(), dt, is_meta_single_page);
                    }
                }
            }
        });

        public static ICommand Cmd_SaveIllustAll { get; } = new DelegateCommand<object>(async obj =>
        {
            if (obj is ImageItem)
            {
                var item = obj as ImageItem;
                var illust = item.Illust;
                var dt = illust.GetDateTime();
                var is_meta_single_page = illust.PageCount==1 ? true : false;

                if (illust != null)
                {
                    if (illust is Pixeez.Objects.IllustWork)
                    {
                        var illustset = illust as Pixeez.Objects.IllustWork;
                        var total = illustset.meta_pages.Count();
                        if (is_meta_single_page)
                        {
                            var url = illust.GetOriginalUrl();
                            url.SaveImage(illust.GetThumbnailUrl(), dt, is_meta_single_page);
                        }
                        else
                        {
                            foreach (var pages in illustset.meta_pages)
                            {
                                var url = pages.GetOriginalUrl();
                                url.SaveImage(pages.GetThumbnailUrl(), dt, is_meta_single_page);
                            }
                        }
                    }
                    else if (illust is Pixeez.Objects.NormalWork)
                    {
                        if (is_meta_single_page)
                        {
                            var url = illust.GetOriginalUrl();
                            var illustset = illust as Pixeez.Objects.NormalWork;
                            url.SaveImage(illust.GetThumbnailUrl(), dt, is_meta_single_page);
                        }
                        else
                        {
                            illust = await illust.RefreshIllust();
                            if (illust.Metadata != null && illust.Metadata.Pages != null)
                            {
                                illust.Cache();
                                foreach (var p in illust.Metadata.Pages)
                                {
                                    var u = p.GetOriginalUrl();
                                    u.SaveImage(p.GetThumbnailUrl(), dt, is_meta_single_page);
                                }
                            }
                        }
                    }
                }
            }
        });

        public static ICommand Cmd_OpenDownloaded { get; } = new DelegateCommand<object>(obj =>
        {
            if (obj is ImageItem)
            {
                var item = obj as ImageItem;
                var illust = item.Illust;

                if (item.Index >= 0)
                {
                    string fp = string.Empty;
                    item.IsDownloaded = illust.IsDownloadedAsync(out fp, item.Index);
                    if (!string.IsNullOrEmpty(fp) && File.Exists(fp))
                    {
                        System.Diagnostics.Process.Start(fp);
                    }
                }
                else
                {
                    string fp = string.Empty;
                    item.IsDownloaded = illust.IsPartDownloadedAsync(out fp);
                    if (!string.IsNullOrEmpty(fp) && File.Exists(fp))
                    {
                        System.Diagnostics.Process.Start(fp);
                    }
                }
            }
        });

        public static ICommand Cmd_Search { get; } = new DelegateCommand<string>(obj =>
        {
            if (obj is string && !string.IsNullOrEmpty(obj))
            {
                var content = ParseLink((string)obj);
                var id = ParseID(content);

                if (!string.IsNullOrEmpty(content))
                {
                    foreach (Window win in Application.Current.Windows)
                    {
                        if (win.Title.Contains(content) || win.Title.Contains($": {id},") || win.Title.Contains($"/ {id} /"))
                        {
                            win.Activate();
                            return;
                        }
                    }

                    var page = new SearchResultPage() { Tag = content };
                    page.UpdateDetail(content);

                    var viewer = new ContentWindow()
                    {
                        Title = $"Searching {content} ...",
                        Width = WIDTH_MIN,
                        Height = HEIGHT_DEF,
                        MinWidth = WIDTH_MIN,
                        MinHeight = HEIGHT_MIN,
                        MaxHeight = HEIGHT_MAX,
                        Content = page
                    };
                    viewer.Show();
                    CommonHelper.DoEvents();
                }
            }
        });

        public static ICommand Cmd_Drop { get; } = new DelegateCommand<IEnumerable<string>>(async obj =>
        {
            if (obj is IEnumerable)
            {
                foreach (var link in (obj as List<string>))
                {
                    await Task.Run(new Action(() =>
                    {
                        Cmd_Search.Execute(link);
                    }));
                }
            }
        });

        #region Maybe reduce UI frozen
        private static object ExitFrame(object state)
        {
            ((DispatcherFrame)state).Continue = false;
            return null;
        }

        public static async void DoEvents()
        {
            try
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    await Dispatcher.Yield();
                    //await System.Windows.Threading.Dispatcher.Yield();
                }
            }
            catch (Exception)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    DispatcherFrame frame = new DispatcherFrame();
                    //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate { }));
                    //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate { }));

                    //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(ExitFrame), frame);
                    await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, new DispatcherOperationCallback(ExitFrame), frame);
                    //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrame), frame);
                    Dispatcher.PushFrame(frame);
                }
            }
        }

        public static async void DoEvents(this object obj)
        {
            try
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    //await Dispatcher.Yield();
                    DispatcherFrame frame = new DispatcherFrame();
                    await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(ExitFrame), frame);
                    Dispatcher.PushFrame(frame);
                }
            }
            catch (Exception)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    DispatcherFrame frame = new DispatcherFrame();
                    await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, new DispatcherOperationCallback(ExitFrame), frame);
                    Dispatcher.PushFrame(frame);
                }
            }
        }

        public static void Sleep(int ms)
        {
            for (int i = 0; i < ms; i += 10)
            {
                System.Threading.Thread.Sleep(5);
                DoEvents();
            }
        }
        #endregion

        private static async Task<Pixeez.Tokens> RefreshToken()
        {
            Pixeez.Tokens result = null;
            try
            {
                var authResult = await Pixeez.Auth.AuthorizeAsync(setting.User, setting.Pass, setting.RefreshToken, setting.Proxy, setting.UsingProxy);
                setting.AccessToken = authResult.Authorize.AccessToken;
                setting.RefreshToken = authResult.Authorize.RefreshToken;
                setting.ExpTime = authResult.Key.KeyExpTime.ToLocalTime();
                setting.ExpiresIn = authResult.Authorize.ExpiresIn.Value;
                setting.Update = Convert.ToInt64(DateTime.Now.ToFileTime() / 10000000);
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
                        var authResult = await Pixeez.Auth.AuthorizeAsync(setting.User, setting.Pass, setting.Proxy, setting.UsingProxy);
                        setting.AccessToken = authResult.Authorize.AccessToken;
                        setting.RefreshToken = authResult.Authorize.RefreshToken;
                        setting.ExpTime = authResult.Key.KeyExpTime.ToLocalTime();
                        setting.ExpiresIn = authResult.Authorize.ExpiresIn.Value;
                        setting.Update = Convert.ToInt64(DateTime.Now.ToFileTime() / 10000000);
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
            return (result);
        }

        public static async Task<Pixeez.Tokens> ShowLogin(bool force = false)
        {
            Pixeez.Tokens result = null;
            foreach (Window win in Application.Current.Windows)
            {
                if (win is PixivLoginDialog) return (result);
            }
            try
            {
                if (!force && setting.ExpTime > DateTime.Now && !string.IsNullOrEmpty(setting.AccessToken))
                {
                    result = Pixeez.Auth.AuthorizeWithAccessToken(setting.AccessToken, setting.Proxy, setting.UsingProxy);
                }
                else
                {
                    if (!string.IsNullOrEmpty(setting.User) && !string.IsNullOrEmpty(setting.Pass) && !string.IsNullOrEmpty(setting.RefreshToken))
                    {
                        try
                        {
                            result = await RefreshToken();
                        }
                        catch (Exception)
                        {
                            result = Pixeez.Auth.AuthorizeWithAccessToken(setting.AccessToken, setting.Proxy, setting.UsingProxy);
                        }
                    }
                    else
                    {
                        var dlgLogin = new PixivLoginDialog() { AccessToken=setting.AccessToken, RefreshToken=setting.RefreshToken };
                        var ret = dlgLogin.ShowDialog();
                        result = dlgLogin.Tokens;
                    }
                }
            }
            catch (Exception ex)
            {
                await ex.Message.ShowMessageBoxAsync("ERROR");
            }
            return (result);
        }

        #region Text process routines
        public static IEnumerable<string> ParseDragContent(this DragEventArgs e)
        {
            List<string> links = new List<string>();

            var fmts = new List<string>(e.Data.GetFormats(true));

            if (fmts.Contains("text/html"))
            {
                using (var ms = (MemoryStream)e.Data.GetData("text/html"))
                {

                    var html = System.Text.Encoding.Unicode.GetString(ms.ToArray()).Trim();

                    var mr = new List<MatchCollection>();
                    mr.Add(Regex.Matches(html, @"href=""(http(s{0,1}):\/\/www\.pixiv\.net\/member_illust\.php\?mode=.*?illust_id=\d+.*?)"""));
                    mr.Add(Regex.Matches(html, @"href=""(http(s{0,1}):\/\/www\.pixiv\.net\/(.*?\/){0,1}artworks\/\d+.*?)"""));
                    mr.Add(Regex.Matches(html, @"((src)|(href))=""(.*?\.pximg\.net\/img-.*?\/(\d+)_p\d+.*?\.((png)|(jpg)|(jpeg)|(gif)|(bmp)))"""));
                    mr.Add(Regex.Matches(html, @"href=""(http(s{0,1}):\/\/www\.pixiv\.net\/member.*?\.php\?id=\d+).*?"""));
                    mr.Add(Regex.Matches(html, @"(http(s{0,1}):\/\/www\.pixiv\.net\/member.*?\.php\?id=\d+).*?"));

                    mr.Add(Regex.Matches(html, @"href=""(http(s{0,1}):\/\/pixiv\.navirank\.com\/id\/\d+).*?"""));
                    mr.Add(Regex.Matches(html, @"href=""(http(s{0,1}):\/\/pixiv\.navirank\.com\/user\/\d+).*?"""));
                    mr.Add(Regex.Matches(html, @"href=""(http(s{0,1}):\/\/pixiv\.navirank\.com\/tag\/.*?)"""));

                    foreach (var mi in mr)
                    {
                        if (mi.Count > 50)
                        {
                            ShowMessageBox("There are too many links, which may cause the program to crash and cancel the operation.", "WARNING");
                            continue;
                        }
                        foreach (Match m in mi)
                        {
                            var link = m.Groups[1].Value.Trim().Trim(trim_char);
                            if (!string.IsNullOrEmpty(link) && !links.Contains(link)) links.Add(link);
                        }
                    }
                }
            }
            else if (fmts.Contains("Text"))
            {
                var html = ((string)e.Data.GetData("Text")).Trim();

                var mr = new List<MatchCollection>();
                mr.Add(Regex.Matches(html, @"(http(s{0,1}):\/\/www\.pixiv\.net\/member.*?\.php\?id=\d+).*?$"));
                mr.Add(Regex.Matches(html, @"(http(s{0,1}):\/\/www\.pixiv\.net\/member.*?\.php\?.*?illust_id=\d+).*?$"));
                mr.Add(Regex.Matches(html, @"(http(s{0,1}):\/\/www\.pixiv\.net\/(.*?\/){0,1}artworks\/\d+).*?$"));
                mr.Add(Regex.Matches(html, @"(.*?\.pximg\.net\/img-.*?\/\d+_p\d+\.((png)|(jpg)|(jpeg)|(gif)|(bmp)))$"));

                mr.Add(Regex.Matches(html, @"(http(s{0,1}):\/\/pixiv\.navirank\.com\/id\/\d+).*?$"));
                mr.Add(Regex.Matches(html, @"(http(s{0,1}):\/\/pixiv\.navirank\.com\/user\/\d+).*?$"));
                mr.Add(Regex.Matches(html, @"(http(s{0,1}):\/\/pixiv\.navirank\.com\/tag\/.*?\/)$"));

                mr.Add(Regex.Matches(html, @"((\d+)(_((p)|(ugoira))*\d+)*)"));

                foreach (var mi in mr)
                {
                    if (mi.Count > 50)
                    {
                        ShowMessageBox("There are too many links, which may cause the program to crash and cancel the operation.", "WARNING");
                        continue;
                    }
                    foreach (Match m in mi)
                    {
                        var link = m.Groups[1].Value.Trim().Trim(trim_char);
                        long id;
                        if (long.TryParse(link, out id))
                        {
                            links.Add($"https://www.pixiv.net/artworks/{link}");
                            links.Add($"https://www.pixiv.net/member.php?id={link}");
                        }
                        else if (!string.IsNullOrEmpty(link) && !links.Contains(link)) links.Add(link);
                    }
                }
            }
            return (links);
        }

        public static string ParseID(this string searchContent)
        {
            var patten =  @"((UserID)|(IllustID)|(User)|(Tag)|(Caption)|(Fuzzy)|(Fuzzy Tag)):(.*?)$";
            string result = searchContent;
            if (!string.IsNullOrEmpty(result))
            {
                result = Regex.Replace(result, patten, "$9", RegexOptions.IgnoreCase).Trim().Trim(trim_char);
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

                else if (Regex.IsMatch(result, @"(.*?illust_id=)(\d+)(.*)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?illust_id=)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?\/artworks\/)(\d+)(.*)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?\/artworks\/)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?\/pixiv\.navirank\.com\/id\/)(\d+)(.*)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?\/id\/)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase);

                else if (Regex.IsMatch(result, @"^(.*?\?id=)(\d+)(.*)$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?\?id=)(\d+)(.*)$", "UserID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?\/pixiv\.navirank\.com\/user\/)(\d+)(.*)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?\/user\/)(\d+)(.*)", "UserID: $2", RegexOptions.IgnoreCase);

                else if (Regex.IsMatch(result, @"^(.*?tag_full&word=)(.*)$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?tag_full&word=)(.*)$", "Tag: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?\/pixiv\.navirank\.com\/tag\/)(.*?)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?\/tag\/)(.*?)", "Tag: $2", RegexOptions.IgnoreCase);

                else if (Regex.IsMatch(result, @"^(.*?\/img-.*?\/)(\d+)(_p\d+.*?\.((png)|(jpg)|(jpeg)|(gif)|(bmp)))$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?\/img-.*?\/)(\d+)(_p\d+.*?\.((png)|(jpg)|(jpeg)|(gif)|(bmp)))$", "IllustID: $2", RegexOptions.IgnoreCase);

                else if (Regex.IsMatch(result, @"((\d+)(_((p)|(ugoira))*\d+)*)"))
                    result = Regex.Replace(result, @"(.*?(\d+)(_((p)|(ugoira))*\d+)*.*)", "$2", RegexOptions.IgnoreCase);

                else if (!Regex.IsMatch(result, @"((UserID)|(User)|(IllustID)|(Tag)|(Caption)|(Fuzzy)|(Fuzzy Tag)):", RegexOptions.IgnoreCase))
                    result = $"Caption: {result}";
            }

            return (result.Trim().Trim(trim_char).HtmlDecode());
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

        public static string HtmlEncode(this string text)
        {
            return (WebUtility.HtmlEncode(text));
        }

        public static string HtmlDecode(this string text)
        {
            string result = text;

            var patten = new Regex(@"&(amp;){0,1}#(([0-9]{1,6})|(x([a-fA-F0-9]{1,5})));", RegexOptions.IgnoreCase);
            //result = WebUtility.UrlDecode(WebUtility.HtmlDecode(result));
            result = Uri.UnescapeDataString(WebUtility.HtmlDecode(result));
            foreach (Match match in patten.Matches(result))
            {
                var v = Convert.ToInt32(match.Groups[2].Value);
                if (v > 0xFFFF)
                    result = result.Replace(match.Value, char.ConvertFromUtf32(v));
            }

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
            string result = string.Copy(html);
            try
            {
                // Remove new lines since they are not visible in HTML
                result = result.Replace("\n", " ");

                // Remove tab spaces
                result = result.Replace("\t", " ");

                // Remove multiple white spaces from HTML
                result = Regex.Replace(result, "\\s+", " ");

                // Remove HEAD tag
                result = Regex.Replace(result, "<head.*?</head>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                // Remove any JavaScript
                result = Regex.Replace(result, "<script.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                // Replace special characters like &, <, >, " etc.
                StringBuilder sb = new StringBuilder(result);
                // Note: There are many more special characters, these are just
                // most common. You can add new characters in this arrays if needed
                string[] OldWords = {"&nbsp;", "&amp;", "&quot;", "&lt;", "&gt;", "&reg;", "&copy;", "&bull;", "&trade;"};
                string[] NewWords = {" ", "&", "\"", "<", ">", "®", "©", "•", "™"};
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
            catch (Exception) { result = html.HtmlDecode(); }
            return result;
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
            string age = "all-age";

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
                age = sanity;
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
        #endregion

        #region Loading Image from InterNet/LocalCache routines
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

        public async static Task<BitmapSource> ConvertBitmapDPI(this BitmapSource source, double dpiX = 96, double dpiY = 96)
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
                using (var ms = new MemoryStream())
                {
                    var nbmp = BitmapSource.Create(width, height, dpiX, dpiY, source.Format, palette, pixelData, stride);
                    PngBitmapEncoder pngEnc = new PngBitmapEncoder();
                    pngEnc.Frames.Add(BitmapFrame.Create(nbmp));
                    pngEnc.Save(ms);
                    var pngDec = new PngBitmapDecoder(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    result = pngDec.Frames[0];
                }
            }
            catch (Exception ex)
            {
                await ex.Message.ShowMessageBoxAsync("ERROR");
            }
            return result;
        }

        public async static Task<ImageSource> ToImageSource(this Stream stream)
        {
            //await imgStream.GetResponseStreamAsync();
            BitmapSource result = null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();

                result = bmp;
                //result = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }
            catch (Exception ex)
            {
                var ret = ex.Message;
                try
                {
                    result = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                }
                catch (Exception exx)
                {
                    var retx = exx.Message;
                }
            }
            if (result is ImageSource)
            {
                var dpi = new DPI();
                if (result.DpiX != dpi.X || result.DpiY != dpi.Y)
                    result = await ConvertBitmapDPI(result, dpi.X, dpi.Y);
            }
            return (result);
        }
        #endregion

        #region Downloaded Cache routines
        private static Dictionary<string, bool> _cachedDownloadedList = new Dictionary<string, bool>();
        internal static void UpdateDownloadedListCache(this string folder, bool cached = true)
        {
            if (Directory.Exists(folder) && cached)
            {
                if (!_cachedDownloadedList.ContainsKey(folder))
                {
                    _cachedDownloadedList[folder] = cached;
                    var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        if(ext_imgs.Contains(Path.GetExtension(f)))
                            _cachedDownloadedList[f] = cached;
                    }
                }
            }
        }

        internal static async void UpdateDownloadedListCacheAsync(this string folder, bool cached = true)
        {
            await Task.Run(() => {
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
            await Task.Run(() => {
                UpdateDownloadedListCache(storage);
            });
        }

        internal static bool DownoadedCacheExists(this string file)
        {
            return (_cachedDownloadedList.ContainsKey(file));
        }

        private static Func<string, bool> DownoadedCacheExistsFunc = x => DownoadedCacheExists(x);
        internal static bool DownoadedCacheExistsAsync(this string file)
        {
            return (DownoadedCacheExistsFunc(file));
        }

        internal static void DownloadedCacheAdd(this string file, bool cached = true)
        {
            try
            {
                _cachedDownloadedList[file] = cached;
            }
            catch (Exception) { }
        }

        internal static void DownloadedCacheRemove(this string file)
        {
            try
            {
                if (_cachedDownloadedList.ContainsKey(file))
                    _cachedDownloadedList.Remove(file);
            }
            catch (Exception) { }
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
            catch (Exception) { }
        }

        // Define the event handlers.
        private static void OnChanged(object source, FileSystemEventArgs e)
        {
#if DEBUG
            // Specify what is done when a file is changed, created, or deleted.
            Console.WriteLine($"File: {e.FullPath} {e.ChangeType}");
#endif
            if (e.ChangeType == WatcherChangeTypes.Created)
            {
                if (File.Exists(e.FullPath))
                {
                    if (ext_imgs.Contains(Path.GetExtension(e.Name).ToLower()))
                    {
                        e.FullPath.DownloadedCacheAdd();
                        UpdateDownloadStateAsync(GetIllustId(e.Name), true);
                    }
                }
            }
            else if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                //if (File.Exists(e.FullPath))
                //{
                //    e.FullPath.DownloadedCacheAdd();
                //    UpdateDownloadStateAsync(GetIllustId(e.FullPath));
                //}
            }
            else if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                if (ext_imgs.Contains(Path.GetExtension(e.Name).ToLower()))
                {
                    e.FullPath.DownloadedCacheRemove();
                    UpdateDownloadStateAsync(GetIllustId(e.Name), false);
                }
            }
        }

        private static void OnRenamed(object source, RenamedEventArgs e)
        {
#if DEBUG
            // Specify what is done when a file is renamed.
            Console.WriteLine($"File: {e.OldFullPath} renamed to {e.FullPath}");
#endif
            if (e.ChangeType == WatcherChangeTypes.Renamed)
            {
                e.OldFullPath.DownloadedCacheUpdate(e.FullPath);
                if (ext_imgs.Contains(Path.GetExtension(e.Name).ToLower()))
                {
                    UpdateDownloadStateAsync(GetIllustId(e.Name));
                }
            }
        }

        private static Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>();

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void InitDownloadedWatcher(this IEnumerable<StorageType> storages)
        {
            var folders = new List<string>();
            foreach(var l in storages)
            {
                folders.Add(l.Folder);
            }
            folders = folders.Distinct().ToList();
            folders.Sort();

            _watchers.Clear();
            foreach (var f in folders)
            {
                var folder = Path.GetFullPath(f.MacroReplace("%ID%", "")).TrimEnd('\\');
                var c = _watchers.Where(o => folder.StartsWith(o.Key, StringComparison.CurrentCultureIgnoreCase)).Count();
                if (c > 0) {
                    var stores = storages.Where(o=>o.Folder.Equals(f, StringComparison.CurrentCultureIgnoreCase));
                    if (stores.Count() > 0) stores.First().Cached = true;
                    continue;
                } 

                if (Directory.Exists(folder))
                {
                    var locals = storages.Where(o => Path.GetFullPath(o.Folder).TrimEnd('\\').Equals(folder, StringComparison.CurrentCultureIgnoreCase));
                    var local = locals.Count() > 0 ? locals.First() : null;
                    if (!(local != null ? local.Cached : false)) continue;

                    f.UpdateDownloadedListCacheAsync();
                    var watcher = new FileSystemWatcher(folder, "*.*")
                    {
                        NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                        IncludeSubdirectories = local is StorageType ? local.IncludeSubFolder : false
                    };
                    watcher.Changed += OnChanged;
                    watcher.Created += OnChanged;
                    watcher.Deleted += OnChanged;
                    watcher.Renamed += OnRenamed;
                    // Begin watching.
                    watcher.EnableRaisingEvents = true;

                    _watchers[folder] = watcher;
                }
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
            await new Action(() => {
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
                        else if (w.Content is SearchResultPage)
                            (w.Content as SearchResultPage).UpdateDownloadStateAsync(illustid, exists);
                    }
                }
            }).InvokeAsync();
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
                folder.UpdateDownloadedListCacheAsync(local.Cached);

                var f = Path.Combine(folder, file);
                if (local.Cached)
                {
                    if (f.DownoadedCacheExistsAsync())
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

            return (result);
        }
        #endregion

        #region IsPartDownloaded
        internal static bool IsPartDownloadedAsync(this ImageItem item)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloadedAsync());
            else
                return (false);
        }

        internal static bool IsPartDownloaded(this ImageItem item)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloaded());
            else
                return (false);
        }

        internal static bool IsPartDownloadedAsync(this ImageItem item, out string filepath)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloadedAsync(out filepath));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsPartDownloaded(this ImageItem item, out string filepath)
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
            var result =IsPartDownloadedFileFunc(url, filepath);
            filepath = result.Path;
            return (result.Exists);
        }

        internal static bool IsPartDownloaded(this string url, out string filepath)
        {
            bool result = false;
            var file = url.GetImageName(true);
            int[] range = Enumerable.Range(0, 250).ToArray();

            filepath = string.Empty;
            foreach (var local in setting.LocalStorage)
            {
                if (string.IsNullOrEmpty(local.Folder)) continue;

                var folder = local.Folder.FolderMacroReplace(url.GetIllustId());
                folder.UpdateDownloadedListCacheAsync(local.Cached);

                var f = Path.Combine(folder, file);
                if (local.Cached)
                {
                    if (f.DownoadedCacheExistsAsync())
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
                var fe = Path.GetExtension(file);
                foreach (var fc in range)
                {
                    var fp = Path.Combine(folder, $"{fn}_{fc}{fe}");
                    if (local.Cached)
                    {
                        if (fp.DownoadedCacheExistsAsync())
                        {
                            filepath = fp;
                            result = true;
                            break;
                        }
                    }
                    else
                    {
                        if (File.Exists(fp))
                        {
                            filepath = fp;
                            result = true;
                            break;
                        }
                    }
                }
                if (result) break;
            }
            return (result);
        }

        internal static bool IsPartDownloaded(this string url)
        {
            string fp = string.Empty;
            return (IsPartDownloaded(url, out fp));
        }
        #endregion
        #endregion

        #region Load/Save Image routines
        internal static bool IsFileReady(this string filename)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                if (!File.Exists(filename)) return true;

                using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                    return inputStream.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void WaitForFile(this string filename)
        {
            //This will lock the execution until the file is ready
            //TODO: Add some logic to make it async and cancelable
            while (!IsFileReady(filename)) { }
        }

        public static string GetFilePath(this string url)
        {
            string result = url;
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = cache.GetCacheFile(url);
            }
            return (result);
        }

        public static async Task<ImageSource> LoadImageFromFile(this string file)
        {
            ImageSource result = null;
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await new Action(async () =>
                    {
                        result = await stream.ToImageSource();
                    }).InvokeAsync();
                }
            }
            return (result);
        }

        public static async Task<ImageSource> LoadImageFromUrl(this string url, Pixeez.Tokens tokens = null)
        {
            ImageSource result = null;
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                if (tokens == null) tokens = await ShowLogin();
                result = await cache.GetImage(url, tokens);
            }
            return (result);
        }

        public static async Task<ImageSource> LoadImageFromUri(this Uri uri, Pixeez.Tokens tokens = null)
        {
            ImageSource result = null;
            if (uri.IsUnc || uri.IsFile)
                result = await LoadImageFromFile(uri.LocalPath);
            else
                result = await LoadImageFromUrl(uri.OriginalString, tokens);
            return (result);
        }

        public static async Task<string> GetImagePath(this string url, Pixeez.Tokens tokens = null)
        {
            string result = null;
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                if (tokens == null) tokens = await ShowLogin();
                result = await cache.GetImagePath(url, tokens);
            }
            return (result);
        }

        public static async Task<bool> SaveImage(this string url, Pixeez.Tokens tokens, string file, bool overwrite = true)
        {
            bool result = false;
            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    if (!overwrite && File.Exists(file) && new FileInfo(file).Length > 0)
                    {
                        return (true);
                    }
                    using (var response = await tokens.SendRequestAsync(Pixeez.MethodType.GET, url))
                    {
                        if (response != null && response.Source.StatusCode == HttpStatusCode.OK)
                        {
                            using (var ms = await response.ToMemoryStream())
                            {
                                var dir = Path.GetDirectoryName(file);
                                if (!Directory.Exists(dir))
                                {
                                    Directory.CreateDirectory(dir);
                                }
                                //using (var sms = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Read))
                                //{
                                //    ms.Seek(0, SeekOrigin.Begin);
                                //    await sms.WriteAsync(ms.ToArray(), 0, (int)ms.Length);
                                //    result = true;
                                //}
                                //WaitForFile(file);
                                File.WriteAllBytes(file, ms.ToArray());
                                result = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is IOException)
                    {

                    }
                    else
                    {
                        ex.Message.ShowMessageBox("ERROR");
                    }
                }
            }
            return (result);
        }

        public static async Task<string> SaveImage(this string url, Pixeez.Tokens tokens, bool is_meta_single_page = false, bool overwrite = true)
        {
            string result = string.Empty;
            //url = Regex.Replace(url, @"//.*?\.pixiv.net/", "//i.pximg.net/", RegexOptions.IgnoreCase);
            var file = url.GetImageName(is_meta_single_page);
            if (string.IsNullOrEmpty(setting.LastFolder))
            {
                SaveFileDialog dlgSave = new SaveFileDialog();
                dlgSave.FileName = file;
                if (dlgSave.ShowDialog() == true)
                {
                    file = dlgSave.FileName;
                    setting.LastFolder = Path.GetDirectoryName(file);
                }
                else file = string.Empty;
            }

            try
            {
                if (!string.IsNullOrEmpty(file))
                {
                    if (!overwrite && File.Exists(file) && new FileInfo(file).Length > 0)
                    {
                        return (file);
                    }

                    using (var response = await tokens.SendRequestAsync(Pixeez.MethodType.GET, url))
                    {
                        if (response != null && response.Source.StatusCode == HttpStatusCode.OK)
                        {
                            using (var ms = await response.ToMemoryStream())
                            {
                                file = Path.Combine(setting.LastFolder, Path.GetFileName(file));
                                File.WriteAllBytes(file, ms.ToArray());
                                result = file;
                            }
                        }
                        else result = null;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is IOException)
                {

                }
                else
                {
                    ex.Message.ShowMessageBox("ERROR");
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
                $"{Path.GetFileName(file)} is saved!".ShowDownloadToast("Successed", file);

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
                        $"{Path.GetFileName(ugoira_file)} is saved!".ShowDownloadToast("Successed", ugoira_file);
                    }
                    else
                    {
                        $"Save {Path.GetFileName(ugoira_url)} failed!".ShowDownloadToast("Failed", "");
                    }
                }
            }
            else
            {
                $"Save {Path.GetFileName(url)} failed!".ShowDownloadToast("Failed", "");
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
            ShowDownloadManager(true);
            if (_downManager is DownloadManagerPage)
            {
                _downManager.Add(url, thumb, dt, is_meta_single_page, overwrite);
            }
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

        public static async Task<ImageSource> ToImageSource(this Pixeez.AsyncResponse response)
        {
            ImageSource result = null;
            using (var stream = await response.GetResponseStreamAsync())
            {
                result = await stream.ToImageSource();
            }
            return (result);
        }

        private static void Arrange(UIElement element, int width, int height)
        {
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();
        }

        public static Image GetThemedImage(this Uri uri)
        {
            Image result = new Image() { Source = new BitmapImage(uri) };
            try
            {
                var dpi = new DPI();

                var img = new BitmapImage(uri);
                var src = new Image() { Source = img, Width = img.Width, Height = img.Height, Opacity = 0.8 };
                src.Effect = new ThresholdEffect() { Threshold = 0.67, BlankColor = Theme.AccentColor };
                //img.Effect = new TranspranceEffect() { TransColor = Theme.AccentColor };
                //img.Effect = new TransparenceEffect() { TransColor = Color.FromRgb(0x00, 0x96, 0xfa) };
                //img.Effect = new ReplaceColorEffect() { Threshold = 0.5, SourceColor = Color.FromArgb(0xff, 0x00, 0x96, 0xfa), TargetColor = Theme.AccentColor };
                //img.Effect = new ReplaceColorEffect() { Threshold = 0.5, SourceColor = Color.FromRgb(0x00, 0x96, 0xfa), TargetColor = Colors.Transparent };
                //img.Effect = new ReplaceColorEffect() { Threshold = 0.5, SourceColor = Color.FromRgb(0x00, 0x96, 0xfa), TargetColor = Theme.AccentColor };
                //img.Effect = new ExcludeReplaceColorEffect() { Threshold = 0.05, ExcludeColor = Colors.White, TargetColor = Theme.AccentColor };

                Grid root = new Grid();
                root.Background = Theme.AccentBrush;
                Arrange(root, (int)src.Width, (int)src.Height);
                root.Children.Add(src);
                Arrange(src, (int)src.Width, (int)src.Height);

                RenderTargetBitmap bmp = new RenderTargetBitmap((int)(src.Width), (int)(src.Height), dpi.X, dpi.Y, PixelFormats.Pbgra32);
                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    VisualBrush visualBrush = new VisualBrush(root);
                    drawingContext.DrawRectangle(visualBrush, null, new Rect(new Point(), new Size(src.Width, src.Height)));
                }
                bmp.Render(drawingVisual);
                result.Source = bmp;
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception) { }
#endif
            return (result);
        }
        #endregion

        #region Illust Tile ListView routines
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

        public static bool IsSameIllust(this ImageItem item, int hash)
        {
            bool result = false;

            if (item.ItemType == ImageItemType.Work)
            {
                result = item.Illust.GetPreviewUrl(item.Index).GetImageId().IsSameIllust(hash) || item.Illust.GetOriginalUrl(item.Index).GetImageId().IsSameIllust(hash);
            }

            return (result);
        }

        public static bool IsSameIllust(this ImageItem item, long id)
        {
            bool result = false;

            try
            {
                if (long.Parse(item.ID) == id) result = true;
            }
            catch (Exception) { }

            return (result);
        }

        public static bool IsSameIllust(this ImageItem item, long? id)
        {
            bool result = false;

            try
            {
                if (long.Parse(item.ID) == (id ?? -1)) result = true;
            }
            catch (Exception) { }

            //long id_s = -1;
            //long.TryParse(item.ID, out id_s);
            //if (id_s == id.Value) result = true;
            //if(string.Equals(item.ID, id.ToString(), StringComparison.CurrentCultureIgnoreCase))
            //{
            //    result = true;
            //}

            return (result);
        }

        public static bool IsSameIllust(this ImageItem item, ImageItem item_now)
        {
            bool result = false;

            try
            {
                if (long.Parse(item.ID) == long.Parse(item_now.ID)) result = true;
            }
            catch (Exception) { }

            //long id_s = -1;
            //long.TryParse(item.ID, out id_s);
            //long id_t = -1;
            //long.TryParse(item_now.ID, out id_t);
            //if (id_s == id_t) result = true;

            return (result);
        }
        #endregion
        
        #region Refresh Illust/User Info
        public static async Task<Pixeez.Objects.Work> RefreshIllust(this Pixeez.Objects.Work Illust, Pixeez.Tokens tokens = null)
        {
            var result = Illust;
            if (tokens == null) tokens = await ShowLogin();
            if (tokens == null) return result;
            try
            {
                var illusts = await tokens.GetWorksAsync(Illust.Id.Value);
                foreach (var illust in illusts)
                {
                    if (string.IsNullOrEmpty(illust.ImageUrls.Px128x128)) illust.ImageUrls.Px128x128 = result.ImageUrls.Px128x128;
                    if (string.IsNullOrEmpty(illust.ImageUrls.Px480mw)) illust.ImageUrls.Px480mw = result.ImageUrls.Px480mw;
                    if (string.IsNullOrEmpty(illust.ImageUrls.SquareMedium)) illust.ImageUrls.SquareMedium = result.ImageUrls.SquareMedium;
                    if (string.IsNullOrEmpty(illust.ImageUrls.Small)) illust.ImageUrls.Small = result.ImageUrls.Small;
                    if (string.IsNullOrEmpty(illust.ImageUrls.Medium)) illust.ImageUrls.Medium = result.ImageUrls.Medium;
                    if (string.IsNullOrEmpty(illust.ImageUrls.Large)) illust.ImageUrls.Large = result.ImageUrls.Large;
                    if (string.IsNullOrEmpty(illust.ImageUrls.Original)) illust.ImageUrls.Original = result.ImageUrls.Original;

                    illust.Cache();
                    Illust = illust;
                    result = illust;
                    break;
                }
            }
            catch (Exception) { }
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
                foreach (var illust in illusts)
                {
                    illust.Cache();
                    result = illust;
                    break;
                }
            }
            catch (Exception) { }
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
            catch (Exception) { }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserBase> RefreshUser(this Pixeez.Objects.Work Illust, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.UserBase result = Illust.User;
            try
            {
                var user = await Illust.User.RefreshUser(tokens);
                if (user.Id.Value == Illust.User.Id.Value)
                {
                    user.Cache();
                    Illust.User.is_followed = user.is_followed;
                    result = user;
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserBase> RefreshUser(this Pixeez.Objects.UserBase User, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.UserBase result = User;
            if (tokens == null) tokens = await ShowLogin();
            if (tokens == null) return result;
            try
            {
                var users = await tokens.GetUsersAsync(User.Id.Value);
                foreach (var user in users)
                {
                    user.Cache();
                    if (user.Id.Value == User.Id.Value)
                    {
                        User.is_followed = user.is_followed;
                        result = user;
                        break;
                    }
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static async Task<Pixeez.Objects.User> RefreshUser(this long UserID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.User result = null;
            if (UserID < 0) return (result);
            if (tokens == null) tokens = await ShowLogin();
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
            catch (Exception) { }
            return (result);
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
                catch (Exception) { }
            }
            return (result);
        }
        #endregion

        #region Like Helper routines
        public static bool IsLiked(this Pixeez.Objects.Work illust)
        {
            bool result = false;
            illust = IllustCache.ContainsKey(illust.Id) ? IllustCache[illust.Id] : illust;
            if (illust.User != null)
            {
                result = illust.IsBookMarked();// || (illust.IsLiked ?? false);
            }
            return (result);
        }

        public static bool IsLiked(this Pixeez.Objects.UserBase user)
        {
            bool result = false;
            user = UserCache.ContainsKey(user.Id) ? UserCache[user.Id] : user;
            if (user != null)
            {
                result = user.is_followed ?? (user as Pixeez.Objects.User).IsFollowing ?? (user as Pixeez.Objects.User).IsFollowed ?? false;
            }
            return (result);
        }

        public static bool IsLiked(this ImageItem item)
        {
            return (item.ItemType == ImageItemType.User ? item.Illust.User.IsLiked() : item.Illust.IsLiked());
        }

        public static async Task<bool> Like(this ImageItem item, bool pub = true)
        {
            if (item.ItemType == ImageItemType.Work && item.Illust is Pixeez.Objects.Work)
            {
                var result = item.Illust.IsLiked() ? true : await item.LikeIllust(pub);
                UpdateLikeStateAsync((int)(item.Illust.Id));
                return (result);
            }
            else if (item.ItemType == ImageItemType.User && item.User is Pixeez.Objects.UserBase)
            {
                var result = item.User.IsLiked() ? true : await item.LikeUser(pub);
                UpdateLikeStateAsync((int)(item.User.Id), true);
                return (result);
            }
            else return false;
        }

        public static async Task<bool> UnLike(this ImageItem item, bool pub = true)
        {
            if (item.ItemType == ImageItemType.Work && item.Illust is Pixeez.Objects.Work)
            {
                var result = item.Illust.IsLiked() ? await item.UnLikeIllust(pub) : false;
                UpdateLikeStateAsync((int)(item.Illust.Id));
                return (result);
            }
            else if (item.ItemType == ImageItemType.User && item.User is Pixeez.Objects.UserBase)
            {
                var result = item.User.IsLiked() ? await item.UnLikeUser(pub) : false;
                item.IsFavorited = result;
                UpdateLikeStateAsync((int)(item.User.Id), true);
                return (result);
            }
            else return false;
        }

        #region Like/Unlike Illust helper routines
        public static async Task<bool> Like(this Pixeez.Objects.Work illust, bool pub = true)
        {
            var result = (await illust.LikeIllust(pub)).Item1;
            UpdateLikeStateAsync((int)(illust.Id.Value), false);
            return (result);
        }

        public static async Task<bool> UnLike(this Pixeez.Objects.Work illust, bool pub = true)
        {
            var result = (await illust.UnLikeIllust()).Item1;
            UpdateLikeStateAsync((int)(illust.Id.Value), false);
            return (result);
        }

        public static async Task<bool> LikeIllust(this ImageItem item, bool pub = true)
        {
            bool result = false;

            if (item.ItemType == ImageItemType.Work || item.ItemType == ImageItemType.Works || item.ItemType == ImageItemType.Manga)
            {
                var ret = await item.Illust.LikeIllust(pub);
                result = ret.Item1;
                item.Illust = ret.Item2;
                item.IsFavorited = result;
            }

            return (result);
        }

        public static async Task<bool> UnLikeIllust(this ImageItem item, bool pub = true)
        {
            bool result = false;

            if (item.ItemType == ImageItemType.Work || item.ItemType == ImageItemType.Works || item.ItemType == ImageItemType.Manga)
            {
                var ret = await item.Illust.UnLikeIllust();
                result = ret.Item1;
                item.Illust = ret.Item2;
                item.IsFavorited = result;
            }

            return (result);
        }

        public static void LikeIllust(this IList<ImageItem> collection, bool pub = true)
        {
            LikeIllust(new ObservableCollection<ImageItem>(collection), pub);
        }

        public static void UnLikeIllust(this IList<ImageItem> collection)
        {
            UnLikeIllust(new ObservableCollection<ImageItem>(collection));
        }

        public static void LikeIllust(this ObservableCollection<ImageItem> collection, bool pub = true)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            //var items = collection.Distinct();
            var items = collection.GroupBy(i => i.ID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item is ImageItem && item.Illust is Pixeez.Objects.Work)
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.Illust.Like(pub);
                        }
                        catch (Exception){}
                    }).InvokeAsync();
                }
            });
        }

        public static void UnLikeIllust(this ObservableCollection<ImageItem> collection)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            var items = collection.GroupBy(i => i.ID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item is ImageItem && item.Illust is Pixeez.Objects.Work)
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.Illust.UnLike();
                        }
                        catch (Exception){}
                    }).InvokeAsync();
                }
            });
        }

        public static async Task<Tuple<bool, Pixeez.Objects.Work>> LikeIllust(this Pixeez.Objects.Work illust, bool pub = true)
        {
            Tuple<bool, Pixeez.Objects.Work> result = new Tuple<bool, Pixeez.Objects.Work>(false, illust);

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            try
            {
                if (pub)
                {
                    await tokens.AddMyFavoriteWorksAsync((long)illust.Id, illust.Tags);
                }
                else
                {
                    await tokens.AddMyFavoriteWorksAsync((long)illust.Id, illust.Tags, "private");
                }
            }
            catch (Exception) { }
            finally
            {
                try
                {
                    illust = await illust.RefreshIllust();
                    if (illust != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.Work>(illust.IsLiked(), illust);
                        $"Illust \"{illust.Title}\" is Liked!".ShowToast("Succeed", illust.GetThumbnailUrl());
                    }
                }
                catch (Exception) { }
            }

            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.Work>> UnLikeIllust(this Pixeez.Objects.Work illust)
        {
            Tuple<bool, Pixeez.Objects.Work> result = new Tuple<bool, Pixeez.Objects.Work>(false, illust);

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            try
            {
                await tokens.DeleteMyFavoriteWorksAsync((long)illust.Id);
                await tokens.DeleteMyFavoriteWorksAsync((long)illust.Id, "private");
            }
            catch (Exception) { }
            finally
            {
                try
                {
                    illust = await illust.RefreshIllust();
                    if (illust != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.Work>(illust.IsLiked(), illust);
                        $"Illust \"{illust.Title}\" is Un-Liked!".ShowToast("Succeed", illust.GetThumbnailUrl());
                    }
                }
                catch (Exception) { }
            }

            return (result);
        }
        #endregion

        #region Like/Unlike User helper routines
        public static async Task<bool> Like(this Pixeez.Objects.UserBase user, bool pub = true)
        {
            var result = (await user.LikeUser(pub)).Item1;
            UpdateLikeStateAsync((int)(user.Id.Value), true);
            return (result);
        }

        public static async Task<bool> UnLike(this Pixeez.Objects.UserBase user, bool pub = true)
        {
            var result = (await user.UnLikeUser()).Item1;
            UpdateLikeStateAsync((int)(user.Id.Value), true);
            return (result);
        }

        public static async Task<bool> LikeUser(this ImageItem item, bool pub = true)
        {
            bool result = false;

            if ((item.ItemType == ImageItemType.User || item.ItemType == ImageItemType.Work || item.ItemType == ImageItemType.Works || item.ItemType == ImageItemType.Manga) && item.User is Pixeez.Objects.UserBase)
            {
                try
                {
                    var user = item.User;
                    var ret = await user.LikeUser(pub);
                    result = ret.Item1;
                    item.User = ret.Item2;
                    if (item.ItemType == ImageItemType.User)
                    {
                        item.IsFavorited = result;
                    }
                }
                catch (Exception) { }
            }

            return (result);
        }

        public static async Task<bool> UnLikeUser(this ImageItem item, bool pub = true)
        {
            bool result = false;

            if (item.ItemType == ImageItemType.User && item.User is Pixeez.Objects.UserBase)
            {
                try
                {
                    var user = item.User;
                    result = await user.UnLike();
                    if (item.ItemType == ImageItemType.User)
                    {
                        item.IsFavorited = result;
                    }
                }
                catch (Exception) { }
            }

            return (result);
        }

        public static void LikeUser(this IList<ImageItem> collection, bool pub = true)
        {
            LikeUser(new ObservableCollection<ImageItem>(collection), pub);
        }

        public static void UnLikeUser(this IList<ImageItem> collection)
        {
            UnLikeUser(new ObservableCollection<ImageItem>(collection));
        }

        public static void LikeUser(this ObservableCollection<ImageItem> collection, bool pub = true)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            //var items = collection.Distinct();
            var items = collection.GroupBy(i => i.UserID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item is ImageItem && item.User is Pixeez.Objects.UserBase)
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.User.Like(pub);
                        }
                        catch (Exception){}
                    }).InvokeAsync();
                }
            });
        }

        public static void UnLikeUser(this ObservableCollection<ImageItem> collection)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            var items = collection.GroupBy(i => i.UserID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item is ImageItem && item.User is Pixeez.Objects.UserBase)
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.User.UnLike();
                        }
                        catch (Exception){}
                    }).InvokeAsync();
                }
            });
        }

        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> LikeUser(this Pixeez.Objects.UserBase user, bool pub = true)
        {
            Tuple<bool, Pixeez.Objects.UserBase> result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            try
            {
                if (pub)
                {
                    await tokens.AddFavouriteUser((long)user.Id);
                }
                else
                {
                    await tokens.AddFavouriteUser((long)user.Id, "private");
                }
            }
            catch (Exception) { }
            finally
            {
                try
                {
                    user = await user.RefreshUser();
                    if (user != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);
                        $"User \"{user.Name ?? string.Empty}\" is Liked!".ShowToast("Succeed", user.GetAvatarUrl());
                    }
                }
                catch (Exception) { }
            }
            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> UnLikeUser(this Pixeez.Objects.UserBase user)
        {
            Tuple<bool, Pixeez.Objects.UserBase> result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            try
            {
                await tokens.DeleteFavouriteUser(user.Id.ToString());
                await tokens.DeleteFavouriteUser(user.Id.ToString(), "private");
            }
            catch (Exception) { }
            finally
            {
                try
                {
                    user = await user.RefreshUser();
                    if (user != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);
                        $"User \"{user.Name ?? string.Empty}\" is Un-Liked!".ShowToast("Succeed", user.GetAvatarUrl());
                    }
                }
                catch (Exception) { }
            }
            return (result);
        }
        #endregion
        #endregion

        #region Update Illust/User info cache
        public static void Cache(this Pixeez.Objects.UserBase user)
        {
            if (user is Pixeez.Objects.UserBase)
                UserCache[user.Id] = user;
        }

        public static void Cache(this Pixeez.Objects.Work illust)
        {
            if (illust is Pixeez.Objects.Work)
                IllustCache[illust.Id] = illust;
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
            await new Action(() => {
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
                        else if (w.Content is SearchResultPage)
                            (w.Content as SearchResultPage).UpdateLikeStateAsync(illustid, is_user);
                    }
                }
            }).InvokeAsync();
        }
        #endregion

        #endregion

        #region UI Element Show/Hide
        public static void UpdateTheme(this Window win, Image icon = null)
        {
            try
            {
                if (icon == null)
                    icon = "Resources/pixiv-icon.ico".MakePackUri().GetThemedImage();
                win.Icon = icon.Source;

                if (win.Content is IllustDetailPage)
                {
                    var page = win.Content as IllustDetailPage;
                    page.UpdateTheme();
                }
                else if (win.Title.Equals("DropBox", StringComparison.CurrentCultureIgnoreCase))
                {
                    win.Background = Theme.AccentBrush;
                    win.Content = icon;
                }
            }
            catch (Exception) { }
        }

        public static void UpdateTheme()
        {
            try
            {
                var img = "Resources/pixiv-icon.ico".MakePackUri().GetThemedImage();

                foreach (Window win in Application.Current.Windows)
                {
                    win.UpdateTheme(img);
                }
            }
            catch (Exception) { }
        }

        public static void Show(this ProgressRing progress, bool show)
        {
            if (progress is ProgressRing)
            {
                if (show)
                {
                    progress.Visibility = Visibility.Visible;
                    progress.IsEnabled = true;
                    progress.IsActive = true;
                }
                else
                {
                    progress.Visibility = Visibility.Hidden;
                    progress.IsEnabled = false;
                    progress.IsActive = false;
                }
            }
        }

        public static void Show(this ProgressRing progress)
        {
            progress.Show(true);
        }

        public static void Hide(this ProgressRing progress)
        {
            progress.Show(false);
        }

        public static void Show(this UIElement element, bool show, bool parent = false)
        {
            if (show)
                element.Visibility = Visibility.Visible;
            else
                element.Visibility = Visibility.Collapsed;

            if (parent && element.GetParentObject() is UIElement)
                (element.GetParentObject() as UIElement).Visibility = element.Visibility;
        }

        public static void Show(this UIElement element, bool parent = false)
        {
            element.Show(true, parent);
        }

        public static void Hide(this UIElement element, bool parent = false)
        {
            element.Show(false, parent);
        }
        #endregion

        #region Window/Dialog/MessageBox routines
        public static MetroWindow GetActiveWindow()
        {
            MetroWindow window = Application.Current.Windows.OfType<MetroWindow>().SingleOrDefault(x => x.IsActive);
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

        public static void Active(this MetroWindow window)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                try
                {
                    if (window is MainWindow)
                        window.WindowState = (window as MainWindow).LastWindowStates.Dequeue();
                    else if (window is ContentWindow)
                        window.WindowState = (window as ContentWindow).LastWindowStates.Dequeue();
                }
                catch (Exception)
                {
                    window.WindowState = WindowState.Normal;
                }
            }
            window.Show();
            window.Activate();
        }

        public static bool ActiveByTitle(this string title)
        {
            bool result = false;
            foreach (Window win in Application.Current.Windows)
            {
                if (win.Title.StartsWith(title))
                {
                    if (win is MetroWindow) (win as MetroWindow).Active();
                    else win.Activate();
                    result = true;
                    break;
                }
            }
            return (result);
        }

        public static void WindowKeyUp(this object sender, KeyEventArgs e)
        {
            if (sender is MetroWindow)
            {
                try
                {
                    var win = sender as MetroWindow;
                    if ((Keyboard.Modifiers & ModifierKeys.Control & ModifierKeys.Shift) > 0 && e.Key == Key.Tab)
                    {
                        win.GetPrevWindow().Active();
                    }
                    else if ((Keyboard.Modifiers & ModifierKeys.Control) > 0 && e.Key == Key.Tab)
                    {
                        win.GetNextWindow().Active();
                    }
                    else
                    {
                        if ((sender as MetroWindow).Content is DownloadManagerPage) return;
                        if ((sender as MetroWindow).Tag is DownloadManagerPage) return;

                        if (e.Key == Key.Escape) win.Close();
                    }
                    e.Handled = true;
                }
#if DEBUG
                catch (Exception ex)
                {
                    ex.Message.ShowMessageBox("ERROR");
                }
#else
                catch(Exception) { }
#endif
            }
        }

        public static Dispatcher Dispatcher = Application.Current is Application ? Application.Current.Dispatcher : Dispatcher.CurrentDispatcher;
        public static Dispatcher AppDispatcher(this object obj)
        {
            if (Application.Current is Application)
                return (Application.Current.Dispatcher);
            else
                return (Dispatcher.CurrentDispatcher);
        }

        public static async Task Invoke(this Action action)
        {
            Dispatcher dispatcher = action.AppDispatcher();

            await dispatcher.BeginInvoke(action);
        }

        public static async Task InvokeAsync(this Action action)
        {
            Dispatcher dispatcher = action.AppDispatcher();

            await dispatcher.InvokeAsync(action);
        }

        public static Window GetActiveWindow(this Page page)
        {
            var window = Window.GetWindow(page);
            if (window == null) window = GetActiveWindow();
            return (window);
        }

        internal static DownloadManagerPage _downManager = new DownloadManagerPage();

        public static void ShowDownloadManager(this bool active)
        {
            if (!(_downManager is DownloadManagerPage))
            {
                _downManager = new DownloadManagerPage();
                _downManager.AutoStart = false;
            }

            Window _dm = null;
            foreach (Window win in Application.Current.Windows)
            {
                if (win.Content is DownloadManagerPage)
                {
                    _dm = win;
                    break;
                }
            }

            if (_dm is Window)
            {
                _dm.Show();
                if (active) _dm.Activate();
            }
            else
            {
                var viewer = new ContentWindow()
                {
                    Title = $"Download Manager",
                    Width = WIDTH_MIN,
                    Height = HEIGHT_MIN,
                    MinWidth = WIDTH_MIN,
                    MinHeight = HEIGHT_MIN,
                    Left = _downManager.Pos.X,
                    Top = _downManager.Pos.Y,
                    Tag = _downManager,
                    Content = _downManager
                };
                viewer.Show();
            }
        }

        public static void ShowMessageBox(this string content, string title)
        {
            ShowMessageDialog(title, content);
        }

        public static async Task ShowMessageBoxAsync(this string content, string title)
        {
            await ShowMessageDialogAsync(title, content);
        }

        public static async void ShowMessageDialog(string title, string content)
        {
            MetroWindow window = GetActiveWindow();
            await window.ShowMessageAsync(title, content);
        }

        public static async Task ShowMessageDialogAsync(string title, string content)
        {
            MetroWindow window = GetActiveWindow();
            await window.ShowMessageAsync(title, content);
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

        public static void ShowDownloadToast(this string content, string title = "Pixiv", string imgsrc = "", object tag = null)
        {
            INotificationDialogService _dialogService = new NotificationDialogService();
            NotificationConfiguration cfgDefault = NotificationConfiguration.DefaultConfiguration;
            NotificationConfiguration cfg = new NotificationConfiguration(
                //new TimeSpan(0, 0, 30), 
                TimeSpan.FromSeconds(3),
                cfgDefault.Width+32, cfgDefault.Height,
                "ToastTemplate",
                //cfgDefault.TemplateName, 
                cfgDefault.NotificationFlowDirection
            );

            var newNotification = new DownloadToast()
            {
                Title = title,
                ImgURL = imgsrc,
                Message = content,
                Tag = tag
            };

            _dialogService.ClearNotifications();
            _dialogService.ShowNotificationWindow(newNotification, cfg);
        }

        public static void ShowToast(this string content, string title, string imgsrc)
        {
            INotificationDialogService _dialogService = new NotificationDialogService();
            NotificationConfiguration cfgDefault = NotificationConfiguration.DefaultConfiguration;
            NotificationConfiguration cfg = new NotificationConfiguration(
                //new TimeSpan(0, 0, 30), 
                TimeSpan.FromSeconds(3),
                cfgDefault.Width + 32, cfgDefault.Height,
                "ToastTemplate",
                //cfgDefault.TemplateName, 
                cfgDefault.NotificationFlowDirection
            );

            var newNotification = new InfoToast()
            {
                Title = title,
                ImgURL = imgsrc,
                Message = content,
                Tag = null
            };

            _dialogService.ClearNotifications();
            _dialogService.ShowNotificationWindow(newNotification, cfg);
        }

        public static void ShowToast(this string content, string title)
        {
            INotificationDialogService _dialogService = new NotificationDialogService();
            NotificationConfiguration cfgDefault = NotificationConfiguration.DefaultConfiguration;
            NotificationConfiguration cfg = new NotificationConfiguration(
                //new TimeSpan(0, 0, 30), 
                TimeSpan.FromSeconds(3),
                cfgDefault.Width + 32, cfgDefault.Height,
                "ToastTemplate",
                //cfgDefault.TemplateName, 
                cfgDefault.NotificationFlowDirection
            );

            var newNotification = new Notification()
            {
                Title = title,
                Message = content
            };

            _dialogService.ClearNotifications();
            _dialogService.ShowNotificationWindow(newNotification, cfg);
        }
        #endregion

        #region Search Window routines
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
                result.Add($"Fuzzy Tag: {text}");
                result.Add($"Fuzzy: {text}");
                result.Add($"Tag: {text}");
                result.Add($"Caption: {text}");
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
                        Cmd_Search.Execute(query);
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
                    Cmd_Search.Execute(SearchBox.Text);
                }
            }
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
                    setting.Save();
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
            foreach (Window win in Application.Current.Windows)
            {
                var title = win.Title;
                var tag = win.Tag is string ? win.Tag as string : string.Empty;

                if (title.Equals("Dropbox", StringComparison.CurrentCultureIgnoreCase) ||
                    tag.Equals("Dropbox", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (win is ContentWindow)
                    {
                        result = win as ContentWindow;
                        break;
                    }
                }
            }
            return (result);
        }

        public static void SetDropBoxState(this bool state)
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win is MetroWindow)
                {
                    var title = win.Title;
                    var tag = win.Tag is string ? win.Tag as string : string.Empty;

                    if (!title.Equals("Dropbox", StringComparison.CurrentCultureIgnoreCase) &&
                        !tag.Equals("Dropbox", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (win is ContentWindow)
                            (win as ContentWindow).SetDropBoxState(state);
                        else if (win is MainWindow)
                            (win as MainWindow).SetDropBoxState(state);
                    }
                }
            }
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
                ///box.MouseMove += DropBox_MouseMove;
                //box.MouseDoubleClick += DropBox_MouseDoubleClick;
                box.MouseLeftButtonDown += DropBox_MouseLeftButtonDown;
                box.Width = 48;
                box.Height = 48;
                box.MinWidth = 48;
                box.MinHeight = 48;
                box.MaxWidth = 48;
                box.MaxHeight = 48;

                box.Background = new SolidColorBrush(Theme.AccentColor);
                box.OverlayBrush = Theme.AccentBrush;
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
                box.Tag = "DropBox";

                box.Content = "Resources/pixiv-icon.ico".MakePackUri().GetThemedImage();
                box.Icon = (box.Content as Image).Source;
                //box.Content = img;

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

    public class DownloadToast : Notification
    {
        [Description("Get or Set Toast Type")]
        [Category("Common Properties")]
        [DefaultValue(ToastType.DOWNLOAD)]
        public ToastType Type { get; set; } = ToastType.DOWNLOAD;

        //public string ImgURL { get; set; }
        //public string Message { get; set; }
        //public string Title { get; set; }
        public object Tag { get; set; }
    }

    public class InfoToast : Notification
    {
        [Description("Get or Set Toast Type")]
        [Category("Common Properties")]
        [DefaultValue(ToastType.OK)]
        public ToastType Type { get; set; } = ToastType.OK;

        //public string ImgURL { get; set; }
        //public string Message { get; set; }
        //public string Title { get; set; }
        public object Tag { get; set; }
    }

    public static class ExtensionMethods
    {
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
    }

}
