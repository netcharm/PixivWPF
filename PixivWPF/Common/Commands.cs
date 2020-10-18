using MahApps.Metro.Controls;
using Newtonsoft.Json;
using PixivWPF.Pages;
using Prism.Commands;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PixivWPF.Common
{
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

    public static class Commands
    {
        private static Setting setting = Application.Current.LoadSetting();

        private static DownloadManagerPage _downManager = new DownloadManagerPage();


        private const int WIDTH_MIN = 720;
        private const int HEIGHT_MIN = 524;
        private const int HEIGHT_DEF = 900;
        private const int HEIGHT_MAX = 1024;
        private const int WIDTH_DEF = 1280;

        public static ICommand Login { get; } = new DelegateCommand(() =>
        {
            var setting = Application.Current.LoadSetting();
            var dlgLogin = new PixivLoginDialog() { AccessToken = setting.AccessToken };
            var ret = dlgLogin.ShowDialog();
            if (ret ?? false) setting.AccessToken = dlgLogin.AccessToken;
        });

        public static ICommand DatePicker { get; } = new DelegateCommand<Point?>(obj =>
        {
            if (obj.HasValue)
            {
                var page = new DateTimePicker() { FontFamily = setting.FontFamily };
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

        public static ICommand CopyText { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is string)
            {
                var text = obj as string;
                if (!string.IsNullOrEmpty(text))
                {
                    var data = new DataObject();
                    data.SetData(DataFormats.Text, text);
                    data.SetData(DataFormats.UnicodeText, text);
                    Clipboard.SetDataObject(data, true);
                }
            }
            else if (obj is HtmlTextData)
            {
                CopyHtml.Execute(obj);
            }
            else
            {
                if (obj != null) CopyText.Execute(obj.ToString());
            }
        });

        public static ICommand CopyHtml { get; } = new DelegateCommand<dynamic>(obj =>
        {
            try
            {
                if (obj is string)
                {
                    var text = obj as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        ClipboardHelper.CopyToClipboard(text, text);
                    }
                }
                else if (obj is HtmlTextData)
                {
                    var ht = obj as HtmlTextData;
                    ClipboardHelper.CopyToClipboard(ht.Html, ht.Text);
                }
                else
                {
                    CopyText.Execute(obj);
                }
            }
            catch (Exception) { }
        });

        public static ICommand CopyIllustIDs { get; } = new DelegateCommand<dynamic>(obj =>
        {
            var prefix = Keyboard.Modifiers == ModifierKeys.Control ? "id:" : string.Empty;
            if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (gallery.Name.Equals("RelativeIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("ResultIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("FavoriteIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("HistoryItems", StringComparison.CurrentCultureIgnoreCase))
                {
                    var ids = new  List<string>();
                    foreach (var item in gallery.GetSelected())
                    {
                        if (item.IsWork())
                        {
                            var id = $"{prefix}{item.ID}";
                            if (!ids.Contains(id)) ids.Add(id);
                        }
                        //else if (item.IsUser())
                        //{
                        //    var id = $"u{prefix}{item.ID}";
                        //    if (!ids.Contains(id)) ids.Add(id);
                        //}
                    }
                    CopyText.Execute(string.Join(Environment.NewLine, ids));
                }
                else if (gallery.Name.Equals("SubIllusts", StringComparison.CurrentCultureIgnoreCase))
                {
                    var page = gallery.TryFindParent<IllustDetailPage>();
                    if (page is IllustDetailPage)
                    {
                        if (page.Contents is ImageItem && page.Contents.IsWork())
                            CopyText.Execute($"{prefix}{page.Contents.ID}");
                    }
                }
            }
            else if (obj is ImageItem)
            {
                var item = obj as ImageItem;
                if (item.IsWork())
                {
                    CopyText.Execute($"{prefix}{item.ID}");
                }
            }
            else if (obj is IEnumerable<string>)
            {
                var ids = new List<string>();
                foreach (var s in (obj as IEnumerable<string>))
                {
                    var id = $"{prefix}{s}";
                    if (!ids.Contains(id)) ids.Add(id);
                }
                CopyText.Execute(string.Join(Environment.NewLine, ids));
            }
            else if (obj is string)
            {
                var id = (obj as string).ParseLink().ParseID();
                if (!string.IsNullOrEmpty(id)) CopyText.Execute($"{prefix}{id}");
            }
        });

        public static ICommand CopyUserIDs { get; } = new DelegateCommand<dynamic>(obj =>
        {
            var prefix = Keyboard.Modifiers == ModifierKeys.Control ? "uid:" : string.Empty;
            if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (gallery.Name.Equals("RelativeIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("ResultIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("FavoriteIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("HistoryItems", StringComparison.CurrentCultureIgnoreCase))
                {
                    var ids = new  List<string>();
                    foreach (var item in gallery.GetSelected())
                    {
                        var uid = $"{prefix}{item.UserID}";
                        if (!ids.Contains(uid)) ids.Add(uid);
                    }
                    CopyText.Execute(string.Join(Environment.NewLine, ids));
                }
                else if (gallery.Name.Equals("SubIllusts", StringComparison.CurrentCultureIgnoreCase))
                {
                    var page = gallery.TryFindParent<IllustDetailPage>();
                    if (page is IllustDetailPage)
                    {
                        if (page.Contents is ImageItem)
                            CopyText.Execute($"{prefix}{page.Contents.UserID}");
                    }
                }
            }
            else if (obj is ImageItem)
            {
                var item = obj as ImageItem;
                if (item.IsWork())
                {
                    CopyText.Execute($"{prefix}{item.UserID}");
                }
            }
            else if (obj is IEnumerable<string>)
            {
                var ids = new List<string>();
                foreach (var s in (obj as IEnumerable<string>))
                {
                    var uid = $"{prefix}{s}";
                    if (!ids.Contains(uid)) ids.Add(uid);
                }
                CopyText.Execute(string.Join(Environment.NewLine, ids));
            }
            else if (obj is string)
            {
                var id = (obj as string).ParseLink().ParseID();
                if (!string.IsNullOrEmpty(id)) CopyText.Execute($"{prefix}{id}");
            }
        });

        public static ICommand CopyDownloadInfo { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is IEnumerable)
            {
                var items = obj as IList;
                if (items.Count <= 0) return;
                var sep = @"--------------------------------------------------------------------------------------------";
                var targets = new List<string>();
                targets.Add(sep);
                foreach (var item in items)
                {
                    if (item is DownloadInfo)
                    {
                        var di = item as DownloadInfo;
                        var fail = string.IsNullOrEmpty(di.FailReason) ? string.Empty : $", Reason:{di.FailReason}";
                        var delta = di.EndTime - di.StartTime;
                        var rate = delta.TotalSeconds <= 0 ? 0 : di.Received / 1024.0 / delta.TotalSeconds;
                        targets.Add($"URL    : {di.Url}");
                        targets.Add($"File   : {di.FileName}, {di.FileTime.ToString("yyyy-MM-dd HH:mm:sszzz")}");
                        targets.Add($"State  : {di.State}{fail}");
                        targets.Add($"Elapsed: {di.StartTime.ToString("yyyy-MM-dd HH:mm:sszzz")} -> {di.EndTime.ToString("yyyy-MM-dd HH:mm:sszzz")}, {delta.Days * 24 + delta.Hours}:{delta.Minutes}:{delta.Seconds} s");
                        targets.Add($"Status : {di.Received / 1024.0:0.} KB / {di.Length / 1024.0:0.} KB ({di.Received} Bytes / {di.Length} Bytes), Rate ≈ {rate:0.00} KB/s");
                        targets.Add(sep);
                    }
                }
                targets.Add("");
                CopyText.Execute(string.Join(Environment.NewLine, targets));
            }
            else if (obj is DownloadInfo)
            {
                CopyDownloadInfo.Execute(new List<DownloadInfo>() { obj as DownloadInfo });
            }
            else if (obj is ItemCollection)
            {
                CopyDownloadInfo.Execute(obj as IList);
            }
        });

        public static ICommand CopyImage { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is string)
            {
                await new Action(() =>
                {
                    (obj as string).CopyImage();
                }).InvokeAsync();
            }
            else if (obj is ImageItem)
            {
                var item = obj as ImageItem;
                if (item.IsWork())
                {
                    string fp = item.Illust.GetOriginalUrl(item.Index).GetImageCachePath();
                    if (!string.IsNullOrEmpty(fp))
                    {
                        await new Action(() =>
                        {
                            fp.CopyImage();
                        }).InvokeAsync();
                    }
                }
            }
        });

        public static ICommand OpenItem { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is ImageItem)
            {
                try
                {
                    var item = obj as ImageItem;
                    if (item.IsWork())
                    {
                        if (item.IsPage() || item.IsPages())
                            item.IsDownloaded = item.Illust.IsDownloadedAsync(item.Index);
                        else
                            item.IsDownloaded = item.Illust.IsPartDownloadedAsync();

                        OpenWork.Execute(item.Illust);
                    }
                    else if (item.IsUser())
                    {
                        OpenUser.Execute(item.User);
                    }
                }
                catch (Exception) { }
            }
            else if (obj is ImageListGrid)
            {
                var list = obj as ImageListGrid;
                foreach (var item in list.GetSelected())
                {
                    await new Action(() =>
                    {
                        OpenItem.Execute(item);
                    }).InvokeAsync();
                }
            }
        });

        public static ICommand OpenWork { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is Pixeez.Objects.Work)
                {
                    var illust = obj as Pixeez.Objects.Work;
                    var i = illust.Id.FindIllust();
                    if (i is Pixeez.Objects.Work) illust = i;

                    var title = $"ID: {illust.Id}, {illust.Title}";
                    if (await title.ActiveByTitle()) return;

                    await new Action(async () =>
                    {
                        var item = illust.IllustItem();
                        if (item is ImageItem)
                        {
                            var page = new IllustDetailPage() { FontFamily = setting.FontFamily, Tag = item, Contents = item };
                            var viewer = new ContentWindow()
                            {
                                Title = title,
                                Width = WIDTH_MIN,
                                Height = HEIGHT_DEF,
                                MinWidth = WIDTH_MIN,
                                MinHeight = HEIGHT_MIN,
                                FontFamily = setting.FontFamily,
                                Content = page
                            };
                            viewer.Show();
                            await Task.Delay(1);
                            Application.Current.DoEvents();
                        }
                    }).InvokeAsync();
                }
                else if(obj is ImageItem)
                {
                    var item = obj as ImageItem;
                    if (item.IsWork())
                        OpenUser.Execute(item.Illust);
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    foreach (var item in gallery.GetSelected())
                    {
                        await new Action(() =>
                        {
                            OpenWork.Execute(item);
                        }).InvokeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[ILLUST]");
            }
        });

        public static ICommand OpenWorkPreview { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is ImageItem && (obj as ImageItem).IsWork())
                {
                    var item = obj as ImageItem;
                    if(item.IsPage()||item.IsPages())
                        item.IsDownloaded = item.Illust.IsDownloadedAsync(item.Index);
                    else
                        item.IsDownloaded = item.Illust.IsPartDownloadedAsync();

                    var suffix = item.Count > 1 ? $" - {item.Index}/{item.Count}" : string.Empty;
                    var title = $"Preview ID: {item.ID}, {item.Subject}";
                    if (await title.ActiveByTitle()) return;

                    await new Action(async () =>
                    {
                        var page = new IllustImageViewerPage() { FontFamily = setting.FontFamily, Tag = item, Contents = item };
                        var viewer = new ContentWindow()
                        {
                            Title = $"{title}",
                            Width = WIDTH_MIN,
                            Height = HEIGHT_DEF,
                            MinWidth = WIDTH_MIN,
                            MinHeight = HEIGHT_MIN,
                            FontFamily = setting.FontFamily,
                            Content = page
                        };
                        viewer.Show();
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    foreach (var item in gallery.GetSelected())
                    {
                        await new Action(() =>
                        {
                            OpenWorkPreview.Execute(item);
                        }).InvokeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[PREVIEW]");
            }
        });

        public static ICommand OpenUser { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is Pixeez.Objects.UserBase)
                {
                    var user = obj as Pixeez.Objects.UserBase;
                    var u = user.Id.FindUser();
                    if (u is Pixeez.Objects.UserBase) user = u;

                    var title = $"User: {user.Name} / {user.Id} / {user.Account}";
                    if (await title.ActiveByTitle()) return;

                    await new Action(async () =>
                    {
                        var page = new IllustDetailPage() { FontFamily = setting.FontFamily, Contents = user.UserItem(), Tag = obj };
                        var viewer = new ContentWindow()
                        {
                            Title = title,
                            Width = WIDTH_MIN,
                            Height = HEIGHT_DEF,
                            MinWidth = WIDTH_MIN,
                            MinHeight = HEIGHT_MIN,
                            FontFamily = setting.FontFamily,
                            Content = page
                        };
                        viewer.Show();
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
                else if (obj is ImageItem)
                {
                    var item = obj as ImageItem;
                    if(item.IsWork() || item.IsUser())
                        OpenUser.Execute(item.User);                    
                }
                else if (obj is ImageListGrid)
                {
                    await new Action(async () =>
                    {
                        var gallery = obj as ImageListGrid;
                        foreach (var item in gallery.GetSelected())
                        {
                            await new Action(() =>
                            {
                                OpenUser.Execute(item);
                            }).InvokeAsync();
                        }
                    }).InvokeAsync();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[USER]");
            }
        });

        public static ICommand OpenGallery { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (gallery.Name.Equals("ResultIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("RelativeIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("FavoriteIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("HistoryItems", StringComparison.CurrentCultureIgnoreCase))
                {
                    OpenItem.Execute(gallery);
                }
                else if (gallery.Name.Equals("SubIllusts", StringComparison.CurrentCultureIgnoreCase))
                {
                    OpenWorkPreview.Execute(gallery);
                }
            }
        });

        public static ICommand OpenDownloaded { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is ImageItem)
                {
                    var item = obj as ImageItem;
                    if (item.IsWork())
                    {
                        var illust = item.Illust;

                        if (item.Count > 1)
                        {
                            string fp = string.Empty;
                            if (item.IsPage() || item.IsPages())
                                illust.IsDownloadedAsync(out fp, item.Index);
                            else if (item.IsWork())
                                illust.IsPartDownloadedAsync(out fp);
                            fp.OpenFileWithShell();
                        }
                        else
                        {
                            string fp = string.Empty;
                            illust.IsPartDownloadedAsync(out fp);
                            fp.OpenFileWithShell();
                        }
                    }
                }
                else if (obj is ImageListGrid)
                {
                    await new Action(async () =>
                    {
                        var gallery = obj as ImageListGrid;
                        foreach (var item in gallery.GetSelected())
                        {
                            await new Action(() =>
                            {
                                OpenDownloaded.Execute(item);
                            }).InvokeAsync();
                        }
                    }).InvokeAsync();
                }
                else if (obj is IList<ImageItem>)
                {
                    await new Action(async () =>
                    {
                        var gallery = obj as IList<ImageItem>;
                        foreach (var item in gallery)
                        {
                            await new Action(() =>
                            {
                                OpenDownloaded.Execute(item);
                            }).InvokeAsync();
                        }
                    }).InvokeAsync();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[DOWNLOADED]");
            }
        });

        public static ICommand OpenCachedImage { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is ImageItem)
                {
                    var item = obj as ImageItem;
                    var illust = item.Illust;

                    if (item.Index >= 0)
                    {
                        string fp = string.Empty;
                        item.IsDownloaded = illust.IsDownloadedAsync(out fp, item.Index);
                        string fp_d = item.IsDownloaded ? fp : string.Empty;
                        string fp_o = illust.GetOriginalUrl(item.Index).GetImageCachePath();
                        string fp_p = illust.GetPreviewUrl(item.Index).GetImageCachePath();

                        if (File.Exists(fp_d)) ShellOpenFile.Execute(fp_d);
                        else if (File.Exists(fp_o)) ShellOpenFile.Execute(fp_o);
                        else if (File.Exists(fp_p)) ShellOpenFile.Execute(fp_p);
                    }
                    else
                    {
                        string fp = string.Empty;
                        item.IsDownloaded = illust.IsPartDownloadedAsync(out fp);
                        string fp_d = item.IsDownloaded ? fp : string.Empty;
                        string fp_o = illust.GetOriginalUrl().GetImageCachePath();
                        string fp_p = illust.GetPreviewUrl().GetImageCachePath();

                        if (File.Exists(fp_d)) ShellOpenFile.Execute(fp_d);
                        else if (File.Exists(fp_o)) ShellOpenFile.Execute(fp_o);
                        else if (File.Exists(fp_p)) ShellOpenFile.Execute(fp_p);
                    }
                }
                else if (obj is string)
                {
                    try
                    {
                        Uri url = null;
                        if (Uri.TryCreate(obj as string, UriKind.RelativeOrAbsolute, out url) && url is Uri) ShellOpenFile.Execute(url);
                    }
                    catch (Exception) { }
                }
                else if (obj is ImageListGrid)
                {
                    await new Action(async () =>
                    {
                        var gallery = obj as ImageListGrid;
                        foreach (var item in gallery.GetSelected())
                        {
                            await new Action(() =>
                            {
                                OpenDownloaded.Execute(item);
                            }).InvokeAsync();
                        }
                    }).InvokeAsync();
                }
                else if (obj is IList<ImageItem>)
                {
                    await new Action(async () =>
                    {
                        var gallery = obj as IList<ImageItem>;
                        foreach (var item in gallery)
                        {
                            await new Action(() =>
                            {
                                OpenDownloaded.Execute(item);
                            }).InvokeAsync();
                        }
                    }).InvokeAsync();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[DOWNLOADED]");
            }
        });

        public static ICommand OpenHistory { get; } = new DelegateCommand(async () =>
        {
            try
            {
                var title = $"History";
                if (await title.ActiveByTitle()) return;

                await new Action(async () =>
                {
                    var page = new HistoryPage() { FontFamily = setting.FontFamily };
                    var viewer = new ContentWindow()
                    {
                        Title = title,
                        Width = WIDTH_MIN,
                        Height = HEIGHT_DEF,
                        MinWidth = WIDTH_MIN,
                        MinHeight = HEIGHT_MIN,
                        MaxWidth = WIDTH_MIN + 16,
                        FontFamily = setting.FontFamily,
                        Content = page
                    };
                    viewer.Show();
                    await Task.Delay(1);
                    Application.Current.DoEvents();
                }).InvokeAsync();
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[HISTORY]");
            }
        });

        public static ICommand Open { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is ImageListGrid)
            {
                OpenGallery.Execute(obj);
            }
            else if (obj is ImageItem)
            {
                OpenItem.Execute(obj);
            }
            else if (obj is Pixeez.Objects.Work)
            {
                OpenWork.Execute(obj);
            }
            else if (obj is Pixeez.Objects.UserBase)
            {
                OpenUser.Execute(obj);
            }
            else if (obj is string)
            {
                OpenSearch.Execute(obj as string);
            }
        });

        public static ICommand AddDownloadItem { get; } = new DelegateCommand<dynamic>(async obj => {
            await new Action(() => {
                OpenDownloadManager.Execute(true);
                if (_downManager is DownloadManagerPage && obj is DownloadParams)
                {
                    var dp = obj as DownloadParams;
                    _downManager.Add(dp.Url, dp.ThumbUrl, dp.Timestamp, dp.IsSinglePage, dp.OverwriteExists);
                }
            }).InvokeAsync();
        });

        public static ICommand OpenDownloadManager { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is bool)
            {
                var active = (bool)obj;
                await new Action(() =>
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
                        //if (_dm.WindowState == WindowState.Minimized) _dm.WindowState = WindowState.Normal;
                        //if (active) _dm.Activate();
                    }
                    else
                    {
                        setting = Application.Current.LoadSetting();
                        var viewer = new ContentWindow()
                        {
                            Title = $"Download Manager",
                            MinWidth = WIDTH_MIN + 80,
                            MinHeight = HEIGHT_MIN,
                            Width = setting.DownloadManagerPosition.Width <= WIDTH_MIN + 80 ? WIDTH_MIN + 80 : setting.DownloadManagerPosition.Width,
                            Height = setting.DownloadManagerPosition.Height <= HEIGHT_MIN ? HEIGHT_MIN : setting.DownloadManagerPosition.Height,
                            Left = setting.DownloadManagerPosition.Left >=0 ? setting.DownloadManagerPosition.Left : _downManager.Pos.X,
                            Top = setting.DownloadManagerPosition.Top >=0 ? setting.DownloadManagerPosition.Top : _downManager.Pos.Y,
                            Tag = _downManager,
                            FontFamily = setting.FontFamily,
                            Content = _downManager
                        };
                        viewer.Show();
                    }
                }).InvokeAsync();
            }
        });

        public static ICommand OpenSearch { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is string && !string.IsNullOrEmpty((string)obj))
            {
                var content = CommonHelper.ParseLink((string)obj);
                if (!string.IsNullOrEmpty(content))
                {
                    if (content.StartsWith("IllustID:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var illust = content.ParseID().FindIllust();
                        if (illust is Pixeez.Objects.Work)
                        {
                            Open.Execute(illust);
                            return;
                        }
                    }
                    else if (content.StartsWith("UserID:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var user = content.ParseID().FindUser();
                        if (user is Pixeez.Objects.UserBase)
                        {
                            OpenUser.Execute(user);
                            return;
                        }
                    }

                    var title = $"Searching {content} ...";
                    if (await title.ActiveByTitle()) return;

                    await new Action(async () =>
                    {
                        var page = new SearchResultPage() { FontFamily = setting.FontFamily, Tag = content, Contents = content };
                        var viewer = new ContentWindow()
                        {
                            Title = title,
                            Width = WIDTH_MIN,
                            Height = HEIGHT_DEF,
                            MinWidth = WIDTH_MIN,
                            MinHeight = HEIGHT_MIN,
                            MaxHeight = HEIGHT_MAX,
                            MaxWidth = WIDTH_MIN + 16,
                            FontFamily = setting.FontFamily,
                            Content = page
                        };
                        viewer.Show();
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
            }
            else if (obj is IEnumerable<string>)
            {
                await new Action(async () =>
                {
                    foreach (var link in obj as IEnumerable<string>)
                    {
                        try
                        {
                            await new Action(() =>
                            {
                                OpenSearch.Execute(link);
                            }).InvokeAsync();
                        }
                        catch (Exception) { }
                    }
                }).InvokeAsync();
            }
        });

        public static ICommand SaveIllust { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is ImageItem)
            {
                await new Action(async () =>
                {
                    var item = obj as ImageItem;
                    if (item.IsWork())
                    {
                        if (string.IsNullOrEmpty(setting.LastFolder))
                        {
                            "".ChangeSaveTarget();
                            await Task.Delay(10);
                            Application.Current.DoEvents();
                        }

                        var dt = item.Illust.GetDateTime();
                        var is_meta_single_page = item.Illust.PageCount == 1 ? true : false;
                        if (item.IsPage() || item.IsPages())
                        {
                            var url = item.Illust.GetOriginalUrl(item.Index);
                            if (!string.IsNullOrEmpty(url))
                            {
                                url.SaveImage(item.Illust.GetThumbnailUrl(item.Index), dt, is_meta_single_page);
                            }
                        }
                        else if (item.Illust is Pixeez.Objects.Work)
                        {
                            var url = item.Illust.GetOriginalUrl(item.Index);
                            if (!string.IsNullOrEmpty(url))
                            {
                                url.SaveImage(item.Illust.GetThumbnailUrl(item.Index), dt, is_meta_single_page);
                            }
                        }
                    }
                }).InvokeAsync();
            }
            else if (obj is ImageListGrid)
            {
                setting = Application.Current.LoadSetting();
                await new Action(async () =>
                {
                    var gallery = obj as ImageListGrid;
                    foreach (var item in gallery.GetSelected())
                    {
                        if (string.IsNullOrEmpty(setting.LastFolder))
                        {
                            "".ChangeSaveTarget();
                            await Task.Delay(10);
                            Application.Current.DoEvents();
                        }
                        await new Action(() => {
                            SaveIllust.Execute(item);
                        }).InvokeAsync();
                    }
                }).InvokeAsync();
            }
        });

        public static ICommand SaveIllustAll { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is ImageItem)
            {
                await new Action(async () =>
                {
                    var item = obj as ImageItem;
                    if (item.IsWork())
                    {
                        if (string.IsNullOrEmpty(setting.LastFolder))
                        {
                            "".ChangeSaveTarget();
                            await Task.Delay(10);
                            Application.Current.DoEvents();
                        }

                        var illust = item.Illust;
                        var dt = illust.GetDateTime();
                        var is_meta_single_page = illust.PageCount==1 ? true : false;

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
                }).InvokeAsync();
            }
            else if (obj is ImageListGrid)
            {
                setting = Application.Current.LoadSetting();
                await new Action(async () =>
                {
                    var gallery = obj as ImageListGrid;
                    foreach (var item in gallery.GetSelected())
                    {
                        if (string.IsNullOrEmpty(setting.LastFolder))
                        {
                            "".ChangeSaveTarget();
                            await Task.Delay(10);
                            Application.Current.DoEvents();
                        }
                        await new Action(() => {
                            SaveIllustAll.Execute(item);
                        }).InvokeAsync();
                    }
                }).InvokeAsync();
            }
        });

        public static ICommand OpenDropBox { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is System.Windows.Controls.Primitives.ToggleButton)
            {
                var sender = obj as System.Windows.Controls.Primitives.ToggleButton;
                await new Action(() =>
                {
                    if (sender is System.Windows.Controls.Primitives.ToggleButton)
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Control)
                        {
                            IList<string> titles = Application.Current.OpenedWindowTitles();
                            if (titles.Count > 0) CopyText.Execute($"{string.Join(Environment.NewLine, titles)}{Environment.NewLine}");
                        }
                        else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            setting = Application.Current.LoadSetting();
                            IList<string> titles = Application.Current.OpenedWindowTitles();
                            if (titles.Count > 0)
                            {
                                var links = JsonConvert.SerializeObject(titles, Formatting.Indented);
                                File.WriteAllText(setting.LastOpenedFile, links, new UTF8Encoding(true));
                            }
                        }
                        else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        {
                            setting = Application.Current.LoadSetting();
                            var opened = File.ReadAllText(setting.LastOpenedFile);
                            IList<string> titles = JsonConvert.DeserializeObject<IList<string>>(opened);
                            if (titles.Count > 0)
                            {
                                var links = string.Join(Environment.NewLine, titles).ParseLinks();
                                OpenSearch.Execute(links);
                            }
                        }
                        else if (Keyboard.Modifiers == ModifierKeys.None)
                            CommonHelper.SetDropBoxState(true.ShowDropBox());
                    }
                }).InvokeAsync();
            }
        });

        public static ICommand OpenDragDrop { get; } = new DelegateCommand<IEnumerable<string>>(obj =>
        {
            if (obj is IEnumerable<string>)
            {
                OpenSearch.Execute(obj);
            }
        });

        public static ICommand SendToOtherInstance { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is string)
            {
                var content = obj as string;
                if (!string.IsNullOrEmpty(content))
                {
                    await new Action(async () =>
                    {
                        CommonHelper.SendToOtherInstance(content);
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
            }
            else if (obj is IEnumerable<string>)
            {
                var content = (obj as IEnumerable<string>).ToArray();
                if (content.Count() > 0)
                {
                    await new Action(async () =>
                    {
                        CommonHelper.SendToOtherInstance(content);
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
            }
            else if (obj is Pixeez.Objects.Work)
            {
                SendToOtherInstance.Execute($"id:{(obj as Pixeez.Objects.Work).Id}");
            }
            else if (obj is Pixeez.Objects.UserBase)
            {
                SendToOtherInstance.Execute($"uid:{(obj as Pixeez.Objects.UserBase).Id}");
            }
            else if (obj is ImageItem)
            {
                var item = obj as ImageItem;
                if (item.IsWork()) SendToOtherInstance.Execute(item.Illust);
                else if (item.IsUser()) SendToOtherInstance.Execute(item.User);
            }
            else if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (gallery.Name.Equals("RelativeIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("ResultIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("FavoriteIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("HistoryItems", StringComparison.CurrentCultureIgnoreCase))
                {
                    var ids = new  List<string>();
                    foreach (var item in gallery.GetSelected())
                    {
                        if (item.IsUser())
                        {
                            var uid = $"uid:{item.ID}";
                            if (!ids.Contains(uid)) ids.Add(uid);
                        }
                        else if (item.IsWork())
                        {
                            var id = $"id:{item.ID}";
                            if (!ids.Contains(id)) ids.Add(id);
                        }
                    }
                    SendToOtherInstance.Execute(ids);
                }
                else if (gallery.Name.Equals("SubIllusts", StringComparison.CurrentCultureIgnoreCase))
                {
                    var page = gallery.TryFindParent<IllustDetailPage>();
                    if (page is IllustDetailPage)
                    {
                        if (page.Contents is ImageItem && page.Contents.IsWork())
                            SendToOtherInstance.Execute(page.Contents);
                    }
                }
            }
        });

        public static ICommand ShellSendToOtherInstance { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is string)
            {
                var content = obj as string;
                if (!string.IsNullOrEmpty(content))
                {
                    await new Action(async () =>
                    {
                        CommonHelper.ShellSendToOtherInstance(content);
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
            }
            else if (obj is IEnumerable<string>)
            {
                var content = (obj as IEnumerable<string>).ToArray();
                if (content.Count() > 0)
                {
                    await new Action(async () =>
                    {
                        CommonHelper.ShellSendToOtherInstance(content);
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
            }
            else if (obj is Pixeez.Objects.Work)
            {
                ShellSendToOtherInstance.Execute($"id:{(obj as Pixeez.Objects.Work).Id}");
            }
            else if (obj is Pixeez.Objects.UserBase)
            {
                ShellSendToOtherInstance.Execute($"uid:{(obj as Pixeez.Objects.UserBase).Id}");
            }
            else if (obj is ImageItem)
            {
                var item = obj as ImageItem;
                if (item.IsUser()) ShellSendToOtherInstance.Execute(item.User);
                else if (item.IsWork()) ShellSendToOtherInstance.Execute(item.Illust);
            }
            else if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (gallery.Name.Equals("RelativeIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("ResultIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("FavoriteIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("HistoryItems", StringComparison.CurrentCultureIgnoreCase))
                {
                    var ids = new  List<string>();
                    foreach (var item in gallery.GetSelected())
                    {
                        if (item.IsUser()) ids.Add($"uid:{item.ID}");
                        else if (item.IsWork()) ids.Add($"id:{item.ID}");
                    }
                    ShellSendToOtherInstance.Execute(ids);
                }
                else if (gallery.Name.Equals("SubIllusts", StringComparison.CurrentCultureIgnoreCase))
                {
                    var page = gallery.TryFindParent<IllustDetailPage>();
                    if (page is IllustDetailPage)
                    {
                        if (page.Contents is ImageItem && page.Contents.IsWork())
                            ShellSendToOtherInstance.Execute(page.Contents);
                    }
                }
            }
        });

        public static ICommand OpenPedia { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is string)
            {
                var content = obj as string;
                await new Action(() =>
                {
                    OpenPediaWindow(content);
                }).InvokeAsync();
            }
            else if (obj is IEnumerable<string>)
            {
                await new Action(async () =>
                {
                    var texts = obj as IEnumerable<string>;
                    foreach (var text in texts)
                    {
                        await new Action(() =>
                        {
                            OpenPedia.Execute(text);
                        }).InvokeAsync();
                    }
                }).InvokeAsync();
            }
        });

        public static ICommand ShellOpenPixivPedia { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is string)
            {
                var content = obj as string;
                if (!string.IsNullOrEmpty(content))
                {
                    await new Action(() =>
                    {
                        content.ShellOpenPixivPedia();
                    }).InvokeAsync();
                }
            }
            else if (obj is IEnumerable<string>)
            {
                await new Action(async () =>
                {
                    var texts = obj as IEnumerable<string>;
                    foreach (var text in texts)
                    {
                        await new Action(() =>
                        {
                            ShellOpenPixivPedia.Execute(text);
                        }).InvokeAsync();
                    }
                }).InvokeAsync();
            }
        });

        public static ICommand ShellOpenFile { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is string)
            {
                var content = obj as string;
                if (!string.IsNullOrEmpty(content))
                {
                    await new Action(() =>
                    {
                        content.OpenFileWithShell();
                    }).InvokeAsync();
                }
            }
            else if (obj is Uri)
            {
                try
                {
                    var url = obj as Uri;
                    if ((url.IsFile || url.IsUnc) && File.Exists(url.AbsolutePath)) url.AbsolutePath.OpenFileWithShell();
                    else if (url.IsAbsoluteUri)
                    {
                        string fp_d = url.AbsoluteUri.GetImageCachePath();
                        if (File.Exists(fp_d)) fp_d.OpenFileWithShell();
                    }
                }
                catch (Exception) { }
            }
            else if (obj is ImageItem)
            {
                var item = obj as ImageItem;
                string fp = item.Illust.GetOriginalUrl(item.Index).GetImageCachePath();
                if (!string.IsNullOrEmpty(fp))
                {
                    await new Action(() =>
                    {
                        fp.OpenFileWithShell();
                    }).InvokeAsync();
                }
            }
        });

        public static ICommand SaveTags { get; } = new DelegateCommand(() =>
        {
            Application.Current.SaveTags();
        });

        public static ICommand Speech { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is string)
            {
                var content = obj as string;
                if (!string.IsNullOrEmpty(content))
                {
                    content.Play();
                }
            }
        });

        public static ICommand RefreshPage { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is IllustDetailPage)
            {
                var page = obj as IllustDetailPage;
                if (page.Contents is ImageItem)
                    page.UpdateDetail(page.Contents);
            }
            else if (obj is IllustImageViewerPage)
            {
                var page = obj as IllustImageViewerPage;
                if (page.Contents is ImageItem)
                    page.UpdateDetail(page.Contents);
            }
            else if (obj is SearchResultPage)
            {
                var page = obj as SearchResultPage;
                if (page.Contents is string)
                    page.UpdateDetail(page.Contents);
            }
            else if (obj is HistoryPage)
            {
                var page = obj as HistoryPage;
                page.UpdateDetail();
            }
            else if (obj is TilesPage)
            {
                var win = Application.Current.MainWindow is MainWindow ? Application.Current.MainWindow as MainWindow : null;
                if (win is MainWindow) win.CommandNavRefresh_Click(win.CommandNavRefresh, new RoutedEventArgs());
            }
            else if (obj is MainWindow)
            {
                var win = obj as MainWindow;
                win.CommandNavRefresh_Click(win.CommandNavRefresh, new RoutedEventArgs());
            }
        });

        public static ICommand RefreshPageThumb { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is IllustDetailPage)
            {
                var page = obj as IllustDetailPage;
                page.UpdateThumb(true);
            }
            else if (obj is IllustImageViewerPage)
            {
                var page = obj as IllustImageViewerPage;
                if (page.Contents is ImageItem)
                    page.UpdateDetail(page.Contents);
            }
            else if (obj is SearchResultPage)
            {
                var page = obj as SearchResultPage;
                page.UpdateThumb();
            }
            else if (obj is HistoryPage)
            {
                var page = obj as HistoryPage;
                page.UpdateThumb();
            }
            else if (obj is TilesPage)
            {
                var win = Application.Current.MainWindow is MainWindow ? Application.Current.MainWindow as MainWindow : null;
                if (win is MainWindow) win.CommandNavRefresh_Click(win.CommandNavRefreshThumb, new RoutedEventArgs());
            }
            else if (obj is MainWindow)
            {
                var win = obj as MainWindow;
                win.CommandNavRefresh_Click(win.CommandNavRefreshThumb, new RoutedEventArgs());
            }
        });

        public static ICommand AppendTiles { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                var win = Application.Current.MainWindow is MainWindow ? Application.Current.MainWindow as MainWindow : null;
                if (win is MainWindow) win.CommandNavNext_Click(win.CommandNavNext, new RoutedEventArgs());
            }
            else if (obj is MainWindow)
            {
                var win = obj as MainWindow;
                win.CommandNavNext_Click(win.CommandNavNext, new RoutedEventArgs());
            }
        });

        public static ICommand ScrollUpTiles { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                var tiles = obj as TilesPage;
                tiles.ScrollPageUp();
            }
            else if (obj is MainWindow)
            {
                var content = (obj as MainWindow).GetWindowContent();
                if (content is TilesPage) ScrollUpTiles.Execute(content);
            }
        });

        public static ICommand ScrollDownTiles { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                var tiles = obj as TilesPage;
                tiles.ScrollPageDown();
            }
            else if (obj is MainWindow)
            {
                var content = (obj as MainWindow).GetWindowContent();
                if(content is TilesPage) ScrollDownTiles.Execute(content);
            }
        });

        public static ICommand PrevIllust { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).PrevIllust();
            }
            else if (obj is MainWindow)
            {
                var win = obj as MainWindow;
                win.PrevIllust();
            }
        });

        public static ICommand NextIllust { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).NextIllust();
            }
            else if (obj is MainWindow)
            {
                var win = obj as MainWindow;
                win.NextIllust();
            }
        });

        public static ICommand PrevIllustPage { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).PrevIllustPage();
            }
            else if (obj is MainWindow)
            {
                var win = obj as MainWindow;
                win.PrevIllustPage();
            }
            else if (obj is ContentWindow)
            {
                var win = obj as ContentWindow;
                if (win.Content is IllustDetailPage) (win.Content as IllustDetailPage).PrevIllustPage();
            }
        });

        public static ICommand NextIllustPage { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).NextIllustPage();
            }
            else if (obj is MainWindow)
            {
                var win = obj as MainWindow;
                win.NextIllustPage();
            }
            else if (obj is ContentWindow)
            {
                var win = obj as ContentWindow;
                if (win.Content is IllustDetailPage) (win.Content as IllustDetailPage).NextIllustPage();
            }
        });

        private static long lastKeyUp = Environment.TickCount;
        public static ICommand KeyProcessor { get; } = new DelegateCommand<dynamic>(obj =>
        {
            try
            {
                if (obj is KeyValuePair<dynamic, KeyEventArgs>)
                {
                    var sender = obj.Key;
                    KeyEventArgs e =  obj.Value;
                    if (e.Timestamp - lastKeyUp > 50 && !e.IsRepeat)
                    {
                        if(!Application.Current.IsModified(e.Key)) lastKeyUp = e.Timestamp;

                        if (sender is ImageListGrid || sender is ImageItem)
                        {
                            if (e.IsKey(Key.Enter))
                            {
                                Open.Execute(sender);
                                e.Handled = true;
                            }
                            else if (e.IsKey(Key.F6))
                            {
                                RefreshPageThumb.Execute(sender);
                                e.Handled = true;
                            }
                            else if (e.IsKey(Key.F7))
                            {
                                ChangeIllustLikeState.Execute(sender);
                                e.Handled = true;
                            }
                            else if (e.IsKey(Key.F8))
                            {
                                ChangeUserLikeState.Execute(sender);
                                e.Handled = true;
                            }
                            else if (e.IsKey(Key.O, ModifierKeys.Control))
                            {
                                OpenDownloaded.Execute(sender);
                                e.Handled = true;
                            }
                            else if (e.IsKey(Key.H, ModifierKeys.Control))
                            {
                                OpenHistory.Execute(sender);
                                e.Handled = true;
                            }
                            else if (e.IsKey(Key.S, ModifierKeys.Control))
                            {
                                SaveIllust.Execute(sender);
                                e.Handled = true;
                            }
                            else if (e.IsKey(Key.S, ModifierKeys.Shift))
                            {
                                SaveIllustAll.Execute(sender);
                                e.Handled = true;
                            }
                        }
                        else if (sender is MainWindow)
                        {
#if DEBUG
                            $"KeyUp: {e.Key}, {e.SystemKey}, Modifiers: {Keyboard.Modifiers.ToString()}".DEBUG();
#endif
                            var win = sender as MainWindow;
                            if (e.IsKey(Key.F5))
                            {
                                RefreshPage.Execute(sender);
                                e.Handled = true;
                            }
                            else if (e.IsKey(Key.F3))
                            {
                                AppendTiles.Execute(sender);
                                e.Handled = true;
                            }
                            else if (e.IsKey(Key.F6))
                            {
                                RefreshPageThumb.Execute(sender);
                                e.Handled = true;
                            }
                            else if (e.IsKey(Key.Up, ModifierKeys.Alt))
                            {
                                ScrollUpTiles.Execute(sender);
                                e.Handled = true;
                            }
                            else if (e.IsKey(Key.Down, ModifierKeys.Alt))
                            {
                                ScrollDownTiles.Execute(sender);
                                e.Handled = true;
                            }
                            else
                            {
                                var content = win.GetWindowContent();
                                if (content is TilesPage)
                                {
                                    KeyProcessor.Execute(new KeyValuePair<dynamic, KeyEventArgs>(content, e));
                                    e.Handled = true;
                                }
                            }
                        }
                        else if (sender is ContentWindow)
                        {
                            var win = sender as ContentWindow;
                            if (win.Content is Page)
                            {
                                KeyProcessor.Execute(new KeyValuePair<dynamic, KeyEventArgs>(win.Content, e));
                                e.Handled = true;
                            }
                        }
                        else if (sender is TilesPage)
                        {
                            (sender as TilesPage).KeyAction(e);
                            e.Handled = true;
                        }
                        else if (sender is IllustDetailPage)
                        {
                            (sender as IllustDetailPage).KeyAction(e);
                            e.Handled = true;
                        }
                        else if (sender is IllustImageViewerPage)
                        {
                            (sender as IllustImageViewerPage).KeyAction(e);
                            e.Handled = true;
                        }
                        else if (sender is HistoryPage)
                        {
                            (sender as HistoryPage).KeyAction(e);
                            e.Handled = true;
                        }
                        else if (sender is SearchResultPage)
                        {
                            (sender as SearchResultPage).KeyAction(e);
                            e.Handled = true;
                        }
                    }
                }
            }
            catch (Exception) { }
        });

        #region Like/Unlile Work/User relative
        public static ICommand LikeIllust { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                setting = Application.Current.LoadSetting();
                var pub = setting.PrivateBookmarkPrefer ? false : true;

                if (obj is ImageItem)
                {
                    var item = obj as ImageItem;
                    await item.LikeIllust(pub);
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    gallery.GetSelected().LikeIllust(pub);
                }
                else if (obj is IList<ImageItem>)
                {
                    var gallery = obj as IList<ImageItem>;
                    gallery.LikeIllust(pub);
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[DOWNLOADED]");
            }
        });

        public static ICommand UnLikeIllust { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is ImageItem)
                {
                    var item = obj as ImageItem;
                    await item.UnLikeIllust();
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    gallery.GetSelected().UnLikeIllust();
                }
                else if (obj is IList<ImageItem>)
                {
                    var gallery = obj as IList<ImageItem>;
                    gallery.UnLikeIllust();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[DOWNLOADED]");
            }
        });

        public static ICommand ChangeIllustLikeState { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                setting = Application.Current.LoadSetting();
                var pub = setting.PrivateFavPrefer ? false : true;

                if (obj is ImageItem)
                {
                    var item = obj as ImageItem;
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        await item.LikeIllust(pub);
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        await item.LikeIllust(!pub);
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        await item.UnLikeIllust();
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        gallery.GetSelected().LikeIllust(pub);
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        gallery.GetSelected().LikeIllust(!pub);
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        gallery.GetSelected().UnLikeIllust();
                }
                else if (obj is IList<ImageItem>)
                {
                    var gallery = obj as IList<ImageItem>;
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        gallery.LikeIllust(pub);
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        gallery.LikeIllust(!pub);
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        gallery.UnLikeIllust();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[DOWNLOADED]");
            }
        });

        public static ICommand LikeUser { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                setting = Application.Current.LoadSetting();
                var pub = setting.PrivateFavPrefer ? false : true;

                if (obj is ImageItem)
                {
                    var item = obj as ImageItem;
                    await item.LikeUser(pub);
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    gallery.GetSelected().LikeUser(pub);
                }
                else if (obj is IList<ImageItem>)
                {
                    var gallery = obj as IList<ImageItem>;
                    gallery.LikeUser(pub);
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[DOWNLOADED]");
            }
        });

        public static ICommand UnLikeUser { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is ImageItem)
                {
                    var item = obj as ImageItem;
                    await item.UnLikeUser();
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    gallery.GetSelected().UnLikeUser();
                }
                else if (obj is IList<ImageItem>)
                {
                    var gallery = obj as IList<ImageItem>;
                    gallery.UnLikeUser();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[DOWNLOADED]");
            }
        });

        public static ICommand ChangeUserLikeState { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                setting = Application.Current.LoadSetting();
                var pub = setting.PrivateFavPrefer ? false : true;

                if (obj is ImageItem)
                {
                    var item = obj as ImageItem;
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        await item.LikeUser(pub);
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        await item.LikeUser(!pub);
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        await item.UnLikeUser();
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        gallery.GetSelected().LikeUser(pub);
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        gallery.GetSelected().LikeUser(!pub);
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        gallery.GetSelected().UnLikeUser();
                }
                else if (obj is IList<ImageItem>)
                {
                    var gallery = obj as IList<ImageItem>;
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        gallery.LikeUser(pub);
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        gallery.LikeUser(!pub);
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        gallery.UnLikeUser();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[DOWNLOADED]");
            }
        });
        #endregion

        #region PixivPedia relative
        private static async void OpenPediaWindow(string contents)
        {
            if (!string.IsNullOrEmpty(contents))
            {
                if (contents.ToLower().Contains("://dic.pixiv.net/a/"))
                    contents = Uri.UnescapeDataString(contents.Substring(contents.IndexOf("/a/") + 3));
                var title = $"PixivPedia: {contents} ...";
                if (await title.ActiveByTitle()) return;

                var page = new BrowerPage () { Contents = contents };
                var viewer = new ContentWindow()
                {
                    Title = title,
                    Width = WIDTH_DEF,
                    Height = HEIGHT_DEF,
                    FontFamily = setting.FontFamily,
                    Content = page
                };
                viewer.Show();
                await Task.Delay(1);
                Application.Current.DoEvents();
            }
        }
        #endregion
    }
}
