using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using Microsoft.WindowsAPICodePack.Dialogs;
using MahApps.Metro.Controls;
using Newtonsoft.Json;
using Prism.Commands;
using PixivWPF.Pages;

namespace PixivWPF.Common
{
    #region ICommand Json Converter
    public class ICommandTypeConverter<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            //assume we can convert to anything for now
            return (objectType == typeof(ICommand));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            //explicitly specify the concrete type we want to create
            return serializer.Deserialize<T>(reader);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            //use the default serialization - it works fine
            serializer.Serialize(writer, nameof(value));
        }
    }
    #endregion

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

        private const int WIDTH_MIN = 720;
        private const int HEIGHT_MIN = 524;
        private const int HEIGHT_DEF = 900;
        private const int HEIGHT_MAX = 1024;
        private const int WIDTH_DEF = 1280;
        private const int WIDTH_PEDIA = 1024;
        private const int WIDTH_SEARCH = 710;

        private static Func<IEnumerable<PixivItem>, bool> ParallelExecutionConfirmFunc = items => { return(ParallelExecutionConfirm(items)); };
        public static bool ParallelExecutionConfirm<T>(this IEnumerable<T> items)
        {
            var result = true;
            try
            {
                if (setting.MultipleOpeningConfirm && items is IEnumerable<T>)
                {
                    var count = items.LongCount();
                    if (count > setting.MultipleOpeningThreshold)
                    {
                        var ret = MessageBox.Show($"More Than {setting.MultipleOpeningThreshold} Items, {count} Items Will Be Executed!", "Continue?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                        result &= ret == MessageBoxResult.Yes || ret == MessageBoxResult.OK;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("ParallelExecutionConfirm"); }
            return (result);
        }

        private static Func<DirectoryInfo, bool> HugeFolderOpeningConfirmFunc = folder => { return(HugeFolderOpeningConfirm(folder)); };
        public static bool HugeFolderOpeningConfirm(this DirectoryInfo folder)
        {
            var result = true;
            try
            {
                if (setting.HugeFolderOpeningConfirm && folder is DirectoryInfo && folder.Exists)
                {
                    var count = folder.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly).LongCount();
                    if (count > setting.HugeFolderOpeningThreshold)
                    {
                        var ret = MessageBox.Show($"\"{folder.FullName}\" Contains {count} Files, More Then {setting.HugeFolderOpeningThreshold} Files!", "Continue?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                        result &= ret == MessageBoxResult.Yes || ret == MessageBoxResult.OK;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("HugeFolderOpeningConfirm"); }
            return (result);
        }

        public static bool HugeFolderOpeningConfirm(this string folder)
        {
            var result = true;
            try
            {
                if (string.IsNullOrEmpty(folder)) result = false;
                else result = new DirectoryInfo(folder.IsFile() ? Path.GetDirectoryName(folder) : Path.GetFullPath(folder)).HugeFolderOpeningConfirm();
            }
            catch (Exception ex) { ex.ERROR("HugeFolderOpeningConfirm"); }
            return (result);
        }

        private static bool IsPagesGallary(ImageListGrid gallery)
        {
            bool result = false;
            try
            {
                if (gallery.Name.Equals("SubIllusts", StringComparison.CurrentCultureIgnoreCase))
                    result = true;
            }
            catch (Exception ex) { ex.ERROR("IsPagesGallary"); }
            return (result);
        }

        private static bool IsNormalGallary(ImageListGrid gallery)
        {
            bool result = false;
            try
            {
                if (gallery.Name.Equals("ResultItems", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("RelatedItems", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("FavoriteItems", StringComparison.CurrentCultureIgnoreCase) ||
                    gallery.Name.Equals("HistoryItems", StringComparison.CurrentCultureIgnoreCase))
                    result = true;
            }
            catch (Exception ex) { ex.ERROR("IsNormalGallary"); }
            return (result);
        }

        public static void Invoke(this ICommand cmd, dynamic param)
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate
            {
                cmd.Execute(param);
            }));
        }

        public static async void InvokeAsync(this ICommand cmd, dynamic param, bool realtime = false)
        {
            await new Action(() =>
            {
                cmd.Execute(param);
            }).InvokeAsync(realtime);
        }

        public static ICommand RestartApplication { get; } = new DelegateCommand<dynamic>(obj =>
        {
            try
            {
                if (obj is MetroWindow)
                {
                    setting = Application.Current.LoadSetting();
                    RestartApplication.Execute(setting.ConfirmRestart);
                }
                else
                {
                    setting = Application.Current.LoadSetting();
                    var confirm = obj is bool ? (bool)obj : setting.ConfirmRestart;
                    var process = Application.Current.GetCurrentProcess();
                    var ret = confirm ? MessageBox.Show("Restart Application?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.Yes : true;
                    if (ret)
                    {
                        "nocheckinstance".OpenFileWithShell(command: process.MainModule.FileName);
                        var mainwindow = Application.Current.GetMainWindow();
                        if (mainwindow is MainWindow) mainwindow.CloseApplication(false);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("RestartApplication"); }
            finally { }
        });

        public static ICommand UpgradeApplication { get; } = new DelegateCommand<dynamic>(obj =>
        {
            try
            {
                if (obj is MetroWindow)
                {
                    setting = Application.Current.LoadSetting();
                    UpgradeApplication.Execute(setting.ConfirmUpgrade);
                }
                else
                {
                    setting = Application.Current.LoadSetting();
                    if (string.IsNullOrEmpty(setting.UpgradeLaunch)) return;
                    if (!(setting.UpgradeFiles is List<string>) || setting.UpgradeFiles.Count <= 0) return;

                    var confirm = obj is bool ? (bool)obj : setting.ConfirmUpgrade;
                    var ret = confirm ? MessageBox.Show("Upgrade Application?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.Yes : true;
                    if (ret)
                    {
                        //var files = string.Join(" ", setting.UpgradeFiles.Select(o=> $"\"{o.Trim()}\""));
                        //if ($"upgrade {files}".OpenFileWithShell(command: setting.UpgradeLaunch))
                        if ($"upgrade".OpenFileWithShell(command: setting.UpgradeLaunch))
                        {
                            Task.Delay(500).GetAwaiter().GetResult();
                            Application.Current.DoEvents();
                            //Application.Current.Delay(250);
                            var mainwindow = Application.Current.GetMainWindow();
                            if (mainwindow is MainWindow) mainwindow.CloseApplication(false);
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("UpgradeApplication"); }
            finally { }
        });

        public static ICommand OpenConfig { get; } = new DelegateCommand<string>(async obj =>
        {
            await new Action(() =>
            {
                var cfg = Path.Combine(Application.Current.GetRoot(), "config.json");
                var viewer = string.IsNullOrEmpty(setting.ShellTextViewer) ? setting.ShellLogViewer : setting.ShellTextViewer;
                var param = string.IsNullOrEmpty(setting.ShellTextViewer) ? setting.ShellLogViewerParams : setting.ShellTextViewerParams;
                if (string.IsNullOrEmpty(viewer)) viewer = "notepad.exe";
                cfg.OpenFileWithShell(command: viewer, custom_params: param);
            }).InvokeAsync(true);
        });

        public static ICommand OpenFullListUsers { get; } = new DelegateCommand<string>(async obj =>
        {
            await new Action(() =>
            {
                setting = Application.Current.LoadSetting();
                var cfg = Path.Combine(Application.Current.GetRoot(), setting.FullListedUsersFile);
                var viewer = string.IsNullOrEmpty(setting.ShellTextViewer) ? setting.ShellLogViewer : setting.ShellTextViewer;
                var param = string.IsNullOrEmpty(setting.ShellTextViewer) ? setting.ShellLogViewerParams : setting.ShellTextViewerParams;
                if (string.IsNullOrEmpty(viewer)) viewer = "notepad.exe";
                cfg.OpenFileWithShell(command: viewer, custom_params: param);
            }).InvokeAsync(true);
        });

        public static ICommand MaintainCustomTag { get; } = new DelegateCommand<string>(async obj =>
        {
            await new Action(() =>
            {
                var tag_file = Application.Current.MaintainCustomTagFile(save: true);
                var viewer = string.IsNullOrEmpty(setting.ShellTextViewer) ? setting.ShellLogViewer : setting.ShellTextViewer;
                var param = string.IsNullOrEmpty(setting.ShellTextViewer) ? setting.ShellLogViewerParams : setting.ShellTextViewerParams;
                if (string.IsNullOrEmpty(viewer)) viewer = "notepad.exe";
                tag_file.OpenFileWithShell(command: viewer, custom_params: param);
            }).InvokeAsync(true);
        });

        public static ICommand MaintainNetwork { get; } = new DelegateCommand<string>(async obj =>
        {
            await new Action(() =>
            {
                Application.Current.ReleaseHttpClient();
            }).InvokeAsync(true);
        });

        public static ICommand MaintainMemoryUsage { get; } = new DelegateCommand<string>(async obj =>
        {
            await new Action(() =>
            {
                Application.Current.GC("MaintainMemoryUsage", wait: true, system_memory: true);
            }).InvokeAsync(true);
        });

        public static ICommand MaintainDetailPage { get; } = new DelegateCommand<string>(async obj =>
        {
            await new Action(() =>
            {
                Application.Current.ReCreateDetailPage();
            }).InvokeAsync(true);
        });

        public static ICommand MaintainHiddenWindows { get; } = new DelegateCommand<string>(async obj =>
        {
            await new Action(() =>
            {
                Application.Current.ClearHiddenWindows();
            }).InvokeAsync(true);
        });

        public static ICommand Login { get; } = new DelegateCommand(() =>
        {
            var setting = Application.Current.LoadSetting();
            var dlgLogin = new PixivLoginDialog() { Name = "LoginDialog", AccessToken = setting.AccessToken };
            var ret = dlgLogin.ShowDialog();
            if (ret ?? false) setting.AccessToken = dlgLogin.AccessToken;
        });

        public static ICommand Copy { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is string)
            {
                CopyText.Execute(obj);
            }
            else if (obj is HtmlTextData)
            {
                CopyHtml.Execute(obj);
            }
            else if (obj is TilesPage)
            {
                var page = obj as TilesPage;
                if (page.IllustDetail.Content is IllustDetailPage)
                {
                    (page.IllustDetail.Content as IllustDetailPage).Copy();
                }
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).Copy();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).CopyPreview();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).Copy();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).Copy();
            }
            else if (obj is DownloadManagerPage)
            {
                CopyDownloadInfo.Execute((obj as DownloadManagerPage).GetDownloadInfo());
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) Copy.Execute(win.Content);
            }
            else
            {
                if (obj != null) CopyText.Execute(obj.ToString());
            }
        });

        public static ICommand CopyText { get; } = new DelegateCommand<dynamic>(obj =>
        {
            try
            {
                if (obj is string)
                {
                    var text = obj as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        text = text.KatakanaHalfToFull();
                        ClipboardHelper.CopyToClipboard(text, text);
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
            }
            catch (Exception ex) { ex.ERROR("CopyText"); }
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
                        text = text.KatakanaHalfToFull();
                        ClipboardHelper.CopyToClipboard(text, text);
                    }
                }
                else if (obj is HtmlTextData)
                {
                    var ht = obj as HtmlTextData;
                    ClipboardHelper.CopyToClipboard(ht.Html.KatakanaHalfToFull(), ht.Text.KatakanaHalfToFull());
                }
                else
                {
                    CopyText.Execute(obj);
                }
            }
            catch (Exception ex) { ex.ERROR("CopyHtml"); }
        });

        public static ICommand CopyJson { get; } = new DelegateCommand<dynamic>(obj =>
        {
            try
            {
                if (obj is Pixeez.Objects.Work)
                {
                    var illust = obj as Pixeez.Objects.Work;
                    var json = illust.IllustToJSON();
                    var xmldoc = illust.IllustToXmlDocument();
                    var xml = illust.IllustToXml();

                    var dataObject = new DataObject();
                    if (xmldoc is System.Xml.XmlDocument)
                    {
                        //dataObject.SetData("XmlDocument", xmldoc);
                        dataObject.SetData("Xml", xml);
                    }
                    dataObject.SetData("PixivIllustJSON", json);
                    dataObject.SetData("PixivJSON", json);
                    dataObject.SetData("JSON", json);
                    dataObject.SetData(DataFormats.Text, json);
                    dataObject.SetData(DataFormats.UnicodeText, json);
                    Clipboard.SetDataObject(dataObject, true);
                }
                else if (obj is IEnumerable<Pixeez.Objects.Work>)
                {
                    var illusts = obj as IEnumerable<Pixeez.Objects.Work>;
                    var json = illusts.IllustToJSON();
                    var xmldoc = illusts.IllustToXmlDocument();
                    var xml = illusts.IllustToXml();

                    var dataObject = new DataObject();
                    if (xmldoc is System.Xml.XmlDocument)
                    {
                        //dataObject.SetData("XmlDocument", xmldoc);
                        dataObject.SetData("Xml", xml);
                    }
                    dataObject.SetData("PixivIllustJSON", json);
                    dataObject.SetData("PixivJSON", json);
                    dataObject.SetData("JSON", json);
                    dataObject.SetData(DataFormats.Text, json);
                    dataObject.SetData(DataFormats.UnicodeText, json);
                    Clipboard.SetDataObject(dataObject, true);
                }
                else if (obj is PixivItem)
                {
                    var illust = (obj as PixivItem).Illust;
                    CopyJson.Execute(illust);
                }
                else if (obj is IEnumerable<PixivItem>)
                {
                    var illusts = (obj as IEnumerable<PixivItem>).Select(i => i.Illust).ToList();
                    CopyJson.Execute(illusts);
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (IsNormalGallary(gallery))
                    {
                        var items = gallery.SelectedItems;
                        if (items.Count > 0) CopyJson.Execute(items);
                    }
                    else if (IsPagesGallary(gallery))
                    {
                        var page = gallery.TryFindParent<IllustDetailPage>();
                        if (page is IllustDetailPage)
                        {
                            if (page.Contents is PixivItem && page.Contents.IsWork())
                                CopyJson.Execute(page.Contents);
                        }
                    }
                }

            }
            catch (Exception ex) { ex.ERROR("CopyJson"); }
        });

        public static ICommand CopyArtworkIDs { get; } = new DelegateCommand<dynamic>(obj =>
        {
            var prefix = Keyboard.Modifiers == ModifierKeys.Control ? "id:" : string.Empty;
            if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (IsNormalGallary(gallery))
                {
                    var ids = new  List<string>();
                    foreach (var item in gallery.GetSelected())
                    {
                        if (item.IsWork())
                        {
                            var id = $"{prefix}{item.ID}";
                            if (!ids.Contains(id)) ids.Add(id);
                        }
                    }
                    ids.Add("");
                    CopyText.Execute(string.Join(Environment.NewLine, ids));
                }
                else if (IsPagesGallary(gallery))
                {
                    var page = gallery.TryFindParent<IllustDetailPage>();
                    if (page is IllustDetailPage)
                    {
                        if (page.Contents is PixivItem && page.Contents.IsWork())
                            CopyText.Execute($"{prefix}{page.Contents.ID}");
                    }
                }
            }
            else if (obj is PixivItem)
            {
                var item = obj as PixivItem;
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

        public static ICommand CopyArtistIDs { get; } = new DelegateCommand<dynamic>(obj =>
        {
            var prefix = Keyboard.Modifiers == ModifierKeys.Control ? "uid:" : string.Empty;
            if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (IsNormalGallary(gallery))
                {
                    var ids = new  List<string>();
                    foreach (var item in gallery.GetSelected())
                    {
                        var uid = $"{prefix}{item.UserID}";
                        if (!ids.Contains(uid)) ids.Add(uid);
                    }
                    ids.Add("");
                    CopyText.Execute(string.Join(Environment.NewLine, ids));
                }
                else if (IsPagesGallary(gallery))
                {
                    var page = gallery.TryFindParent<IllustDetailPage>();
                    if (page is IllustDetailPage)
                    {
                        if (page.Contents is PixivItem)
                            CopyText.Execute($"{prefix}{page.Contents.UserID}");
                    }
                }
            }
            else if (obj is PixivItem)
            {
                var item = obj as PixivItem;
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

        public static ICommand CopyArtworkWeblinks { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (IsNormalGallary(gallery))
                {
                    var ids = new  List<string>();
                    foreach (var item in gallery.GetSelected())
                    {
                        if (item.IsWork())
                        {
                            var id = $"{item.ID.ArtworkLink()}";
                            if (!ids.Contains(id)) ids.Add(id);
                        }
                    }
                    ids.Add("");
                    CopyText.Execute(string.Join(Environment.NewLine, ids));
                }
                else if (IsPagesGallary(gallery))
                {
                    var page = gallery.TryFindParent<IllustDetailPage>();
                    if (page is IllustDetailPage)
                    {
                        if (page.Contents is PixivItem && page.Contents.IsWork())
                            CopyText.Execute($"{page.Contents.ID.ArtworkLink()}");
                    }
                }
            }
            else if (obj is PixivItem)
            {
                var item = obj as PixivItem;
                if (item.IsWork())
                {
                    CopyText.Execute(item.ID.ArtworkLink());
                }
            }
            else if (obj is IEnumerable<string>)
            {
                var ids = new List<string>();
                foreach (var s in (obj as IEnumerable<string>))
                {
                    var id = s.ArtworkLink();
                    if (!ids.Contains(id)) ids.Add(id);
                }
                CopyText.Execute(string.Join(Environment.NewLine, ids));
            }
            else if (obj is string)
            {
                var id = (obj as string).ParseLink().ParseID();
                if (!string.IsNullOrEmpty(id)) CopyText.Execute($"{id.ArtworkLink()}");
            }
        });

        public static ICommand CopyArtistWeblinks { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (IsNormalGallary(gallery))
                {
                    var ids = new  List<string>();
                    foreach (var item in gallery.GetSelected())
                    {
                        var id = $"{item.UserID.ArtistLink()}";
                        if (!ids.Contains(id)) ids.Add(id);
                    }
                    ids.Add("");
                    CopyText.Execute(string.Join(Environment.NewLine, ids));
                }
                else if (IsPagesGallary(gallery))
                {
                    var page = gallery.TryFindParent<IllustDetailPage>();
                    if (page is IllustDetailPage)
                    {
                        if (page.Contents is PixivItem)
                            CopyText.Execute($"{page.Contents.UserID.ArtistLink()}");
                    }
                }
            }
            else if (obj is PixivItem)
            {
                var item = obj as PixivItem;
                CopyText.Execute(item.UserID.ArtistLink());
            }
            else if (obj is IEnumerable<string>)
            {
                var ids = new List<string>();
                foreach (var s in (obj as IEnumerable<string>))
                {
                    var id = s.ArtistLink();
                    if (!ids.Contains(id)) ids.Add(id);
                }
                CopyText.Execute(string.Join(Environment.NewLine, ids));
            }
            else if (obj is string)
            {
                var id = (obj as string).ParseLink().ParseID();
                if (!string.IsNullOrEmpty(id)) CopyText.Execute($"{id.ArtistLink()}");
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
                        var info = (item as DownloadInfo).GetDownloadInfo();
                        if (info.Count() > 0)
                        {
                            targets.AddRange(info);
                            targets.Add(sep);
                        }
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

        public static ICommand CopyPediaLink { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is string)
            {
                var content = obj as string;
                var link = $"https://dic.pixiv.net/a/{content}";
                CopyText.Execute(content);
            }
            else if (obj is IEnumerable<string>)
            {
                var contents = obj as IEnumerable<string>;
                var links = new List<string>();
                foreach (var content in contents)
                {
                    links.Add($"https://dic.pixiv.net/a/{content}");
                }
                CopyText.Execute(string.Join(Environment.NewLine, links));
            }
        });

        public static ICommand CopyImage { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is string)
            {
                await new Action(() =>
                {
                    (obj as string).CopyImage();
                }).InvokeAsync(true);
            }
            else if (obj is CustomImageSource)
            {
                var img = obj as CustomImageSource;
                if (!string.IsNullOrEmpty(img.SourcePath) && File.Exists(img.SourcePath)) img.SourcePath.CopyImage();
            }
            else if (obj is Image)
            {
                var img = obj as Image;
                img.Source.CopyImage();
            }
            else if (obj is System.Windows.Media.ImageSource)
            {
                var img = obj as System.Windows.Media.ImageSource;
                img.CopyImage();
            }
            else if (obj is PixivItem)
            {
                var item = obj as PixivItem;
                if (item.IsWork())
                {
                    string fp = item.Illust.GetOriginalUrl(item.Index).GetImageCacheFile();
                    if (!string.IsNullOrEmpty(fp))
                    {
                        await new Action(() =>
                        {
                            fp.CopyImage();
                        }).InvokeAsync(true);
                    }
                }
            }
            else if (obj is TilesPage)
            {
                (obj as TilesPage).CopyPreview();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).CopyPreview();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).CopyPreview();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) CopyImage.Execute(win.Content);
            }
        });

        public static ICommand CopyDownloadedPath { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    if (item.IsWork())
                    {
                        var files = new List<string>();
                        files.AddRange(item.DownloadedFilePaths);
                        CopyText.Execute(string.Join(Environment.NewLine, files));
                    }
                }
                else if (obj is ImageListGrid)
                {
                    await new Action(() =>
                    {
                        var gallery = obj as ImageListGrid;
                        var files = new List<string>();
                        foreach (var item in gallery.GetSelected())
                        {
                            files.AddRange(item.DownloadedFilePaths);
                        }
                        CopyText.Execute(string.Join(Environment.NewLine, files));
                    }).InvokeAsync();
                }
                else if (obj is IList<PixivItem>)
                {
                    await new Action(() =>
                    {
                        var gallery = obj as IList<PixivItem>;
                        var files = new List<string>();
                        foreach (var item in gallery)
                        {
                            files.AddRange(item.DownloadedFilePaths);
                        }
                        CopyText.Execute(string.Join(Environment.NewLine, files));
                    }).InvokeAsync();
                }
            }
            catch (Exception ex) { ex.ERROR("OpenDownloaded"); }
        });

        public static ICommand Compare { get; } = new DelegateCommand<dynamic>(async (obj) =>
        {
            if (obj is string)
            {
                Compare.Execute(new string[] { obj });
            }
            else if (obj is CompareItem && (obj as CompareItem).Item is PixivItem)
            {
                var item = (obj as CompareItem).Item;
                var type = (obj as CompareItem).Type;
                if (item.IsWork())
                {
                    if (type == CompareType.Auto) Compare.Invoke(item);
                    else
                    {
                        if (item.Count > 1)
                        {
                            var id_0 = item.Index;
                            var id_1 = item.Index < item.Count - 1 ? item.Index + 1 : (item.Index - 1);
                            switch (type)
                            {
                                case CompareType.Original:
                                    Compare.Execute(new string[] {
                                        item.Illust.GetOriginalUrl(id_0).GetImageCachePath(),
                                        item.Illust.GetOriginalUrl(id_1).GetImageCachePath()
                                    });
                                    break;
                                case CompareType.Large:
                                    Compare.Execute(new string[] {
                                        item.Illust.GetPreviewUrl(id_0, large: true).GetImageCachePath(),
                                        item.Illust.GetPreviewUrl(id_1, large: true).GetImageCachePath()
                                    });
                                    break;
                                case CompareType.Preview:
                                    Compare.Execute(new string[] {
                                        item.Illust.GetPreviewUrl(id_0).GetImageCachePath(),
                                        item.Illust.GetPreviewUrl(id_1).GetImageCachePath()
                                    });
                                    break;
                                case CompareType.Thumb:
                                    Compare.Execute(new string[] {
                                        item.Illust.GetThumbnailUrl(id_0).GetImageCachePath(),
                                        item.Illust.GetThumbnailUrl(id_1).GetImageCachePath()
                                    });
                                    break;
                                default:
                                    Compare.Invoke(item);
                                    break;
                            }
                        }
                        else
                        {
                            switch (type)
                            {
                                case CompareType.Original:
                                    Compare.Execute(new string[] { $"{item.Illust.GetOriginalUrl(item.Index).GetImageCachePath()}" });
                                    break;
                                case CompareType.Large:
                                    Compare.Execute(new string[] { $"{item.Illust.GetPreviewUrl(item.Index, large: true).GetImageCachePath()}" });
                                    break;
                                case CompareType.Preview:
                                    Compare.Execute(new string[] { $"{item.Illust.GetPreviewUrl(item.Index, large: false).GetImageCachePath()}" });
                                    break;
                                case CompareType.Thumb:
                                    Compare.Execute(new string[] { $"{item.Illust.GetThumbnailUrl(item.Index).GetImageCachePath()}" });
                                    break;
                                default:
                                    Compare.Invoke(item);
                                    break;
                            }
                        }
                    }
                }
            }
            else if (obj is IEnumerable<string>)
            {
                var content = (obj as IEnumerable<string>).ToArray();
                if (content.Count() > 0)
                {
                    await new Action(async () =>
                    {
                        CommonHelper.ShellImageCompare(content);
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
            }
            else if (obj is IEnumerable<DownloadInfo>)
            {
                var content = (obj as IEnumerable<DownloadInfo>).Where(di => !string.IsNullOrEmpty(di.FileName) && File.Exists(di.FileName)).Select(di => di.FileName).ToArray();
                if (content.Count() > 0)
                {
                    await new Action(async () =>
                    {
                        CommonHelper.ShellImageCompare(content);
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
            }
            else if (obj is DownloadManagerPage)
            {
                var _downManager = obj as DownloadManagerPage;
                var items =  _downManager.DownloadItems.SelectedItems.Cast<DownloadInfo>().Where(i => i.State == DownloadState.Finished).Select(i => i.FileName).Take(2).ToList();
                Compare.Execute(items);
            }
            else if (obj is Pixeez.Objects.Work)
            {
                var item = obj as Pixeez.Objects.Work;
                var id = $"{item.GetPreviewUrl().GetImageCachePath()}";
                Compare.Execute(new string[] { id });
            }
            else if (obj is PixivItem)
            {
                var item = obj as PixivItem;
                if (item.IsWork())
                {
                    if (item.Count > 1)
                    {
                        var id_0 = item.Index;
                        var id_1 = item.Index < item.Count - 1 ? item.Index + 1 : (item.Index - 1);
                        Compare.Execute(new string[] {
                            item.Illust.GetPreviewUrl(id_0).GetImageCachePath(),
                            item.Illust.GetPreviewUrl(id_1).GetImageCachePath()
                        });
                    }
                    else
                    {
                        var id = $"{item.Illust.GetPreviewUrl(item.Index).GetImageCachePath()}";
                        Compare.Execute(new string[] { id });
                    }
                }
            }
            else if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (IsNormalGallary(gallery))
                {
                    var ids = new  List<string>();
                    var selected = gallery.GetSelected();
                    if (selected.Count() < 2)
                    {
                        try
                        {
                            if (gallery.Items.Count >= 1)
                            {
                                if (selected.Count() == 0 || gallery.Items.Count <= 2)
                                {
                                    ids.AddRange(gallery.Items.Select(i => i.Illust.GetPreviewUrl().GetImageCachePath()));
                                }
                                else
                                {
                                    var item = selected.First();
                                    var idx = gallery.Items.IndexOf(item);
                                    if (idx < gallery.Count - 1)
                                    {
                                        ids.Add(selected.First().Illust.GetPreviewUrl().GetImageCachePath());
                                        ids.Add(gallery.Items[idx + 1].Illust.GetPreviewUrl().GetImageCachePath());
                                    }
                                    else
                                    {
                                        ids.Add(gallery.Items[idx - 1].Illust.GetPreviewUrl().GetImageCachePath());
                                        ids.Add(selected.First().Illust.GetPreviewUrl().GetImageCachePath());
                                    }
                                }
                            }
                        }
                        catch (Exception ex) { ex.ERROR("Compare"); }
                    }
                    else
                    {
                        foreach (var item in selected)
                        {
                            if (item.IsWork())
                            {
                                var id = $"{item.Illust.GetPreviewUrl().GetImageCachePath()}";
                                if (!ids.Contains(id)) ids.Add(id);
                            }
                        }
                    }
                    Compare.Execute(ids);
                }
                else if (IsPagesGallary(gallery))
                {
                    var page = gallery.TryFindParent<IllustDetailPage>();
                    if (page is IllustDetailPage)
                    {
                        var ids = new  List<string>();
                        var selected = gallery.GetSelected();
                        if (selected.Count() < 2)
                        {
                            try
                            {
                                if (gallery.Items.Count >= 1)
                                {
                                    if (selected.Count() == 0 || gallery.Items.Count <= 2)
                                    {
                                        ids.AddRange(gallery.Items.Select(i => i.Illust.GetPreviewUrl(i.Index).GetImageCachePath()));
                                    }
                                    else
                                    {
                                        var item = selected.First();
                                        if (item.Index < item.Count - 1)
                                        {
                                            ids.Add(item.Illust.GetPreviewUrl(item.Index).GetImageCachePath());
                                            ids.Add(item.Illust.GetPreviewUrl(item.Index + 1).GetImageCachePath());
                                        }
                                        else
                                        {
                                            ids.Add(item.Illust.GetPreviewUrl(item.Index - 1).GetImageCachePath());
                                            ids.Add(item.Illust.GetPreviewUrl(item.Index).GetImageCachePath());
                                        }
                                    }
                                }
                            }
                            catch (Exception ex) { ex.ERROR("Compare"); }
                        }
                        else
                        {
                            foreach (var item in gallery.GetSelected())
                            {
                                if (item.IsPage() || item.IsPages())
                                {
                                    var id = $"{item.Illust.GetPreviewUrl(item.Index).GetImageCachePath()}";
                                    if (!ids.Contains(id)) ids.Add(id);
                                }
                            }
                        }
                        Compare.Execute(ids);
                    }
                }
            }
        });

        public static ICommand OpenTouchFolder { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is string && !string.IsNullOrEmpty(obj as string))
                {
                    var folder = obj as string;

                    var title = $"Touching {folder}";
                    if (Application.Current.ContentWindowExists(title))
                    {
                        await title.ActiveByTitle();
                        return;
                    }

                    await new Action(async () =>
                    {
                        var page = new BatchProcessPage() { Name = $"TouchFolder", FontFamily = setting.FontFamily, Contents = folder, Mode = "touch" };
                        var viewer = new ContentWindow(title)
                        {
                            Title = title,
                            Width = WIDTH_MIN,
                            MinWidth = WIDTH_SEARCH,
                            MaxWidth = WIDTH_PEDIA,
                            FontFamily = setting.FontFamily,
                            SizeToContent = SizeToContent.Height,
                            Content = page
                        };
                        //Application.Current.UpdateContentWindows(viewer, title: title);
                        //await Task.Delay(1);
                        //Application.Current.DoEvents();
                        viewer.Show();
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
                else if (obj is string && string.IsNullOrEmpty(obj as string))
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

                    if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        var result = dlg.FileName;
                        OpenTouchFolder.Execute(result);
                    }
                }
                else
                {
                    await new Action(async () =>
                    {
                        var title = $"Touching";
                        var page = new BatchProcessPage() { Name = $"TouchFolder", FontFamily = setting.FontFamily, Contents = string.Empty };
                        var viewer = new ContentWindow(title)
                        {
                            Title = title,
                            Width = WIDTH_MIN,
                            FontFamily = setting.FontFamily,
                            SizeToContent = SizeToContent.Height,
                            Content = page
                        };
                        viewer.Show();
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
            }
            catch (Exception ex) { ex.ShowExceptionToast(tag: "OpenTouch"); }
        });

        public static ICommand OpenAttachMetaInfo { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is string && !string.IsNullOrEmpty(obj as string))
                {
                    var folder = obj as string;

                    var title = $"AttachMetaInfo {Path.GetFileName(folder)}";
                    if (Application.Current.ContentWindowExists(title))
                    {
                        await title.ActiveByTitle();
                        return;
                    }

                    await new Action(async () =>
                    {
                        var page = new BatchProcessPage() { Name = $"AttachMetaInfoFolder", FontFamily = setting.FontFamily, Contents = folder, Mode = "attach" };
                        var viewer = new ContentWindow(title)
                        {
                            Title = title,
                            Width = WIDTH_MIN,
                            FontFamily = setting.FontFamily,
                            SizeToContent = SizeToContent.Height,
                            Content = page
                        };
                        //Application.Current.UpdateContentWindows(viewer, title: title);
                        //await Task.Delay(1);
                        //Application.Current.DoEvents();
                        viewer.Show();
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
                else if (obj is string && string.IsNullOrEmpty(obj as string))
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

                    if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        var result = dlg.FileName;
                        OpenTouchFolder.Execute(result);
                    }
                }
                else
                {
                    await new Action(async () =>
                    {
                        var title = $"AttachMetaInfo";
                        var page = new BatchProcessPage() { Name = $"AttachMetaInfoFolder", FontFamily = setting.FontFamily, Contents = string.Empty, Mode = "attach" };
                        var viewer = new ContentWindow(title)
                        {
                            Title = title,
                            Width = WIDTH_MIN,
                            FontFamily = setting.FontFamily,
                            SizeToContent = SizeToContent.Height,
                            Content = page
                        };
                        viewer.Show();
                        await Task.Delay(1);
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
            }
            catch (Exception ex) { ex.ShowExceptionToast(tag: "OpenAttachMetaInfo"); }
        });

        public static ICommand OpenItem { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
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
            }
            catch (Exception ex) { ex.ERROR("OpenItem"); }
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
                    if (Application.Current.ContentWindowExists(title))
                    {
                        await title.ActiveByTitle();
                        return;
                    }

                    await new Action(async () =>
                    {
                        var item = illust.WorkItem();
                        if (item is PixivItem)
                        {
                            var page = new IllustDetailPage() { Name = $"IllustDetail_{item.ID}", FontFamily = setting.FontFamily, Contents = item, Title = title };
                            var viewer = new ContentWindow(title)
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
                else if (obj is IEnumerable<Pixeez.Objects.Work>)
                {
                    var works = obj as IEnumerable<Pixeez.Objects.Work>;
                    foreach (var work in works.Distinct()) OpenWork.Execute(work);
                }
                else if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    if (item.IsWork())
                        OpenWork.Execute(item.Illust);
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (gallery.Count > 0) OpenWork.Execute(gallery.GetSelected());
                }
                else if (obj is IList<PixivItem>)
                {
                    await new Action(async () =>
                    {
                        var gallery = obj as IList<PixivItem>;
                        if (gallery.Count() > 0 && ParallelExecutionConfirm(gallery))
                        {
                            foreach (var item in gallery.Distinct())
                            {
                                await new Action(async () =>
                                {
                                    OpenWork.Execute(item);
                                    await Task.Delay(1);
                                    Application.Current.DoEvents();
                                }).InvokeAsync();
                            }
                        }
                    }).InvokeAsync();
                }
                else if (obj is IllustDetailPage)
                {
                    (obj as IllustDetailPage).OpenInNewWindow();
                }
                else if (obj is SearchResultPage)
                {
                    (obj as SearchResultPage).OpenWork();
                }
                else if (obj is HistoryPage)
                {
                    (obj as HistoryPage).OpenWork();
                }
                else if (obj is Window)
                {
                    var win = obj as Window;
                    if (win.Content is Page) OpenWork.Execute(win.Content);
                }
                else if (obj is string)
                {
                    if (!string.IsNullOrEmpty(obj as string))
                    {
                        var illust = (obj as string).GetIllustId().FindIllust();
                        if (illust is Pixeez.Objects.Work)
                            Commands.OpenWork.Execute(illust);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("OpenWork"); }
        });

        public static ICommand OpenWorkPreview { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is PixivItem && (obj as PixivItem).IsWork())
                {
                    var item = obj as PixivItem;
                    if (item.IsPage() || item.IsPages())
                        item.IsDownloaded = item.Illust.IsDownloadedAsync(item.Index);
                    else
                    {
                        if (item.HasPages() && item.Index < 0) item.Index = 0;
                        item.IsDownloaded = item.HasPages() ? item.Illust.IsDownloadedAsync(item.Index) : item.Illust.IsPartDownloadedAsync();
                    }

                    //var suffix = item.Count > 1 ? $" - {item.Index}/{item.Count}" : string.Empty;
                    var suffix = item.Count > 1 ? $"_{item.Index}_{item.Count}".Replace("-1", "0") : string.Empty;
                    var title = $"Preview ID: {item.ID}, {item.Subject}";
                    if (Application.Current.ContentWindowExists(title))
                    {
                        if (!(Keyboard.Modifiers == ModifierKeys.Control))
                        {
                            await title.ActiveByTitle();
                            return;
                        }
                    }

                    await new Action(async () =>
                    {
                        var page = new IllustImageViewerPage() { Name = $"IllustPreview_{item.ID}{suffix}", FontFamily = setting.FontFamily, Contents = item, Title = title };
                        var viewer = new ContentWindow(title)
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
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (gallery.Count > 0) OpenWorkPreview.Execute(gallery.GetSelected());
                }
                else if (obj is IList<PixivItem>)
                {
                    await new Action(async () =>
                    {
                        var gallery = obj as IList<PixivItem>;
                        if (gallery.Count() > 0 && ParallelExecutionConfirm(gallery))
                        {
                            foreach (var item in gallery)
                            {
                                await new Action(() =>
                                {
                                    OpenWorkPreview.Execute(item);
                                }).InvokeAsync();
                            }
                        }
                    }).InvokeAsync();
                }
            }
            catch (Exception ex) { ex.ShowExceptionToast(tag: "OpenWorkPreview"); }
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
                    if (Application.Current.ContentWindowExists(title))
                    {
                        await title.ActiveByTitle();
                        return;
                    }

                    await new Action(async () =>
                    {
                        var page = new IllustDetailPage() { Name = $"UserDetail_{user.Id}", FontFamily = setting.FontFamily, Contents = user.UserItem(), Title = title };
                        var viewer = new ContentWindow(title)
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
                else if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    if (item.HasUser()) OpenUser.Execute(item.User);
                    else if (!string.IsNullOrEmpty(item.UserID)) OpenSearch.Execute($"UserID: {item.UserID}");
                    else if (item.User is Pixeez.Objects.UserBase && (item.User.Id ?? 0) > 0) OpenSearch.Execute($"UserID: {item.User.Id}");
                    else if (item.User is Pixeez.Objects.UserBase && !string.IsNullOrEmpty(item.User.Name)) OpenSearch.Execute($"User: {item.User.Name}");
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
                else if (obj is IllustDetailPage)
                {
                    (obj as IllustDetailPage).OpenUser();
                }
                else if (obj is SearchResultPage)
                {
                    (obj as SearchResultPage).OpenUser();
                }
                else if (obj is HistoryPage)
                {
                    (obj as HistoryPage).OpenUser();
                }
                else if (obj is Window)
                {
                    var win = obj as Window;
                    if (win.Content is Page) OpenUser.Execute(win.Content);
                }
            }
            catch (Exception ex) { ex.ShowExceptionToast(tag: "OpenUser"); }
        });

        public static ICommand OpenGallery { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (IsNormalGallary(gallery))
                {
                    OpenItem.Execute(gallery);
                }
                else if (IsPagesGallary(gallery))
                {
                    OpenWorkPreview.Execute(gallery);
                }
            }
        });

        public static ICommand OpenDownloaded { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    if (item.IsWork())
                    {
                        var illust = item.Illust;

                        string fp = string.Empty;

                        if (item.HasPages() && item.Count > 1)
                        {
                            if (item.Index >= 0 && item.Index < item.Count)
                            {
                                if (illust.IsDownloadedAsync(out fp, item.Index, touch: false)) fp.OpenFileWithShell();
                            }
                            else
                            {
                                for (var i = 0; i < item.Count; i++)
                                {
                                    if (illust.IsDownloadedAsync(out fp, i, touch: false)) fp.OpenFileWithShell();
                                }
                            }
                        }
                        else
                        {
                            if (item.IsPage() || item.IsPages())
                                illust.IsDownloadedAsync(out fp, item.Index, touch: false);
                            else if (item.NoPage())
                                illust.IsDownloadedAsync(out fp, true, item.Index, touch: false);
                            else
                                illust.IsPartDownloadedAsync(out fp, touch: false);

                            if (!string.IsNullOrEmpty(fp)) fp.OpenFileWithShell();
                        }
                    }
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (gallery.Count > 0) OpenDownloaded.Execute(gallery.GetSelected());
                }
                else if (obj is IList<PixivItem>)
                {
                    await new Action(async () =>
                    {
                        var gallery = obj as IList<PixivItem>;
                        if (gallery.Count() > 0 && ParallelExecutionConfirm(gallery))
                        {
                            foreach (var item in gallery)
                            {
                                await new Action(() =>
                                {
                                    OpenDownloaded.Execute(item);
                                }).InvokeAsync();
                            }
                        }
                    }).InvokeAsync();
                }
                else if (obj is TilesPage)
                {
                    (obj as TilesPage).OpenIllust();
                }
                else if (obj is IllustDetailPage)
                {
                    (obj as IllustDetailPage).OpenIllust();
                }
                else if (obj is IllustImageViewerPage)
                {
                    (obj as IllustImageViewerPage).OpenIllust();
                }
                else if (obj is SearchResultPage)
                {
                    (obj as SearchResultPage).OpenIllust();
                }
                else if (obj is HistoryPage)
                {
                    (obj as HistoryPage).OpenIllust();
                }
                else if (obj is Window)
                {
                    var win = obj as Window;
                    if (win.Content is Page) OpenDownloaded.Execute(win.Content);
                }
            }
            catch (Exception ex) { ex.ERROR("OpenDownloaded"); }
        });

        public static ICommand ShowMeta { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                var setting = Application.Current.LoadSetting();
                if (obj is string)
                {
                    var str = obj as string;
                    if (!string.IsNullOrEmpty(str))
                    {
                        if (File.Exists(str))
                            str.OpenFileWithShell(command: setting.ShellShowMetaCmd, custom_params: setting.ShellShowMetaParams);
                        else
                        {
                            var illust = $"{str}".FindIllust();
                            if (illust.IsWork()) ShowMeta.Execute(illust);
                        }
                    }
                }
                else if (obj is Pixeez.Objects.Work)
                {
                    var illust = obj as Pixeez.Objects.Work;
                    var item = illust.WorkItem();
                    ShowMeta.Execute(item);
                }
                else if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    if (item.IsWork())
                    {
                        var illust = item.Illust;

                        string fp = string.Empty;

                        if (item.HasPages() && item.Count > 1)
                        {
                            if (item.Index >= 0)
                            {
                                if (illust.IsDownloadedAsync(out fp, item.Index, touch: false))
                                    fp.OpenFileWithShell(command: setting.ShellShowMetaCmd, custom_params: setting.ShellShowMetaParams);
                            }
                            else
                            {
                                for (var i = 0; i < item.Count; i++)
                                {
                                    if (illust.IsDownloadedAsync(out fp, i, touch: false))
                                        fp.OpenFileWithShell(command: setting.ShellShowMetaCmd, custom_params: setting.ShellShowMetaParams);
                                }
                            }
                        }
                        else
                        {
                            if (item.IsPage() || item.IsPages())
                                illust.IsDownloadedAsync(out fp, item.Index, touch: false);
                            else if (item.NoPage())
                                illust.IsDownloadedAsync(out fp, true, item.Index, touch: false);
                            else
                                illust.IsPartDownloadedAsync(out fp, touch: false);

                            if (!string.IsNullOrEmpty(fp))
                                fp.OpenFileWithShell(command: setting.ShellShowMetaCmd, custom_params: setting.ShellShowMetaParams);
                        }
                    }
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (gallery.Count > 0) ShowMeta.Execute(gallery.GetSelected());
                }
                else if (obj is IList<PixivItem>)
                {
                    var gallery = obj as IList<PixivItem>;
                    if (gallery.Count() > 0 && ParallelExecutionConfirm(gallery))
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery)
                            {
                                await new Action(() =>
                                {
                                    ShowMeta.Execute(item);
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
                else if (obj is TilesPage)
                {
                    (obj as TilesPage).OpenIllust();
                }
                else if (obj is IllustDetailPage)
                {
                    (obj as IllustDetailPage).OpenIllust();
                }
                else if (obj is IllustImageViewerPage)
                {
                    (obj as IllustImageViewerPage).OpenIllust();
                }
                else if (obj is SearchResultPage)
                {
                    (obj as SearchResultPage).OpenIllust();
                }
                else if (obj is HistoryPage)
                {
                    (obj as HistoryPage).OpenIllust();
                }
                else if (obj is Window)
                {
                    var win = obj as Window;
                    if (win.Content is Page) ShowMeta.Execute(win.Content);
                }
            }
            catch (Exception ex) { ex.ERROR("OpenDownloaded"); }
        });

        public static ICommand TouchMeta { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                var use_shell = Keyboard.Modifiers == ModifierKeys.Shift ? true : false;
                var force = Keyboard.Modifiers == ModifierKeys.Control ? true : false;
                var setting = Application.Current.LoadSetting();
                if (obj is int || obj is int? || obj is long || obj is long?)
                {
                    var illust = $"{obj}".FindIllust();
                    if (illust.IsWork()) TouchMeta.Execute(illust);
                }
                else if (obj is string)
                {
                    var str = obj as string;
                    if (!string.IsNullOrEmpty(str))
                    {
                        if (File.Exists(str))
                        {
                            var id = str.GetIllustId();
                            var idx = str.GetIllustPageIndex();
                            var illust = id.FindIllust();
                            if (use_shell)
                                str.OpenFileWithShell(command: setting.ShellTouchMetaCmd, custom_params: setting.ShellTouchMetaParams);
                            else
                                await (new FileInfo(str).AttachMetaInfo(illust.GetDateTime(), id, true));
                        }
                        else
                        {
                            var illust = $"{str}".FindIllust();
                            if (illust.IsWork()) TouchMeta.Execute(illust);
                        }
                    }
                }
                else if (obj is Pixeez.Objects.Work)
                {
                    var illust = obj as Pixeez.Objects.Work;
                    var item = illust.WorkItem();
                    TouchMeta.Execute(item);
                }
                else if (obj is IEnumerable<Pixeez.Objects.Work>)
                {
                    foreach (var illust in (obj as IEnumerable<Pixeez.Objects.Work>))
                        TouchMeta.Execute(illust);
                }
                else if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    if (item.IsWork())
                    {
                        var illust = item.Illust;

                        string fp = string.Empty;

                        if (item.HasPages() && item.Count > 1)
                        {
                            if (item.Index >= 0)
                            {
                                if (illust.IsDownloadedAsync(out fp, item.Index, touch: false))
                                {
                                    if (use_shell)
                                        fp.OpenFileWithShell(command: setting.ShellTouchMetaCmd, custom_params: setting.ShellTouchMetaParams);
                                    else
                                        await (new FileInfo(fp).AttachMetaInfo(item.Illust.GetDateTime(), item.ID, true));
                                }
                            }
                            else
                            {
                                for (var i = 0; i < item.Count; i++)
                                {
                                    if (illust.IsDownloadedAsync(out fp, i, touch: false))
                                    {
                                        if (use_shell)
                                            fp.OpenFileWithShell(command: setting.ShellTouchMetaCmd, custom_params: setting.ShellTouchMetaParams);
                                        else
                                            await (new FileInfo(fp).AttachMetaInfo(item.Illust.GetDateTime(), item.ID, true));
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (item.IsPage() || item.IsPages())
                                illust.IsDownloadedAsync(out fp, item.Index, touch: false);
                            else if (item.NoPage())
                                illust.IsDownloadedAsync(out fp, true, item.Index, touch: false);
                            else
                                illust.IsPartDownloadedAsync(out fp, touch: false);

                            if (!string.IsNullOrEmpty(fp))
                            {
                                if (use_shell)
                                    fp.OpenFileWithShell(command: setting.ShellTouchMetaCmd, custom_params: setting.ShellTouchMetaParams);
                                else
                                    await (new FileInfo(fp).AttachMetaInfo(item.Illust.GetDateTime(), item.ID, true));
                            }
                        }
                    }
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (gallery.Count > 0) TouchMeta.Execute(gallery.GetSelected());
                }
                else if (obj is IList<PixivItem>)
                {
                    var gallery = obj as IList<PixivItem>;
                    if (gallery.Count() > 0 && (use_shell ? ParallelExecutionConfirm(gallery) : true))
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery)
                            {
                                await new Action(() =>
                                {
                                    TouchMeta.Execute(item);
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }                
                }
            }
            catch (Exception ex) { ex.ERROR("OpenDownloaded"); }
        });

        public static ICommand OpenCachedImage { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is CustomImageSource)
                {
                    var img = obj as CustomImageSource;
                    if (!string.IsNullOrEmpty(img.SourcePath) && File.Exists(img.SourcePath)) ShellOpenFile.Execute(img.SourcePath);
                }
                else if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    if (item.IsWork())
                    {
                        var illust = item.Illust;

                        if (item.Index >= 0)
                        {
                            string fp = string.Empty;
                            item.IsDownloaded = illust.IsDownloadedAsync(out fp, item.Index, touch: false);
                            string fp_d = item.IsDownloaded ? fp : string.Empty;
                            string fp_o = illust.GetOriginalUrl(item.Index).GetImageCacheFile();
                            string fp_p = illust.GetPreviewUrl(item.Index, large: setting.ShowLargePreview).GetImageCacheFile();

                            if (File.Exists(fp_d)) ShellOpenFile.Execute(fp_d);
                            else if (File.Exists(fp_o)) ShellOpenFile.Execute(fp_o);
                            else if (File.Exists(fp_p)) ShellOpenFile.Execute(fp_p);
                        }
                        else
                        {
                            string fp = string.Empty;
                            item.IsDownloaded = illust.IsPartDownloadedAsync(out fp, touch: false);
                            string fp_d = item.IsDownloaded ? fp : string.Empty;
                            string fp_o = illust.GetOriginalUrl().GetImageCacheFile();
                            string fp_p = illust.GetPreviewUrl(large: setting.ShowLargePreview).GetImageCacheFile();

                            if (File.Exists(fp_d)) ShellOpenFile.Execute(fp_d);
                            else if (File.Exists(fp_o)) ShellOpenFile.Execute(fp_o);
                            else if (File.Exists(fp_p)) ShellOpenFile.Execute(fp_p);
                        }
                    }
                }
                else if (obj is string)
                {
                    string s = obj as string;
                    Uri url = null;
                    if (!string.IsNullOrEmpty(s) && Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out url)) ShellOpenFile.Execute(url);
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
                                OpenCachedImage.Execute(item);
                            }).InvokeAsync();
                        }
                    }).InvokeAsync();
                }
                else if (obj is IList<PixivItem>)
                {
                    await new Action(async () =>
                    {
                        var gallery = obj as IList<PixivItem>;
                        foreach (var item in gallery)
                        {
                            await new Action(() =>
                            {
                                OpenCachedImage.Execute(item);
                            }).InvokeAsync();
                        }
                    }).InvokeAsync();
                }
                else if (obj is TilesPage)
                {
                    (obj as TilesPage).OpenCachedImage();
                }
                else if (obj is IllustDetailPage)
                {
                    (obj as IllustDetailPage).OpenCachedImage();
                }
                else if (obj is IllustImageViewerPage)
                {
                    (obj as IllustImageViewerPage).OpenCachedImage();
                }
                else if (obj is SearchResultPage)
                {
                    (obj as SearchResultPage).OpenCachedImage();
                }
                else if (obj is HistoryPage)
                {
                    (obj as HistoryPage).OpenCachedImage();
                }
                else if (obj is Window)
                {
                    var win = obj as Window;
                    if (win.Content is Page) OpenCachedImage.Execute(win.Content);
                }
            }
            catch (Exception ex) { ex.ERROR("OpenCachedImage"); }
        });

        public static ICommand OpenFileProperties { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is CustomImageSource)
                {
                    var img = obj as CustomImageSource;
                    if (!string.IsNullOrEmpty(img.SourcePath) && File.Exists(img.SourcePath)) OpenFileProperties.Execute(img.SourcePath);
                }
                else if (obj is IEnumerable<string>)
                {
                    var s = obj as IEnumerable<string>;
                    if (s.Count() > 0) ShellOpenFileProperty.Execute(s);
                }
                else if (obj is string)
                {
                    string s = obj as string;
                    Uri url = null;
                    if (!string.IsNullOrEmpty(s) && Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out url)) ShellOpenFileProperty.Execute(url);
                }
                else if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    if (item.IsWork())
                    {
                        var illust = item.Illust;

                        if (item.HasPages() || item.IsPage() || item.IsPages() || item.Count > 1)
                        {
                            string fp = string.Empty;
                            item.IsDownloaded = illust.IsDownloadedAsync(out fp, item.Index >= 0 ? item.Index : 0, touch: false);
                            string fp_d = item.IsDownloaded ? fp : string.Empty;
                            string fp_o = illust.GetOriginalUrl(item.Index).GetImageCacheFile();
                            string fp_p = illust.GetPreviewUrl(item.Index, large: setting.ShowLargePreview).GetImageCacheFile();

                            if (File.Exists(fp_d)) ShellOpenFileProperty.Execute(fp_d);
                            else if (File.Exists(fp_o)) ShellOpenFileProperty.Execute(fp_o);
                            else if (File.Exists(fp_p)) ShellOpenFileProperty.Execute(fp_p);
                        }
                        else
                        {
                            string fp = string.Empty;
                            item.IsDownloaded = illust.IsPartDownloadedAsync(out fp, touch: false);
                            string fp_d = item.IsDownloaded ? fp : string.Empty;
                            string fp_o = illust.GetOriginalUrl().GetImageCacheFile();
                            string fp_p = illust.GetPreviewUrl(large: setting.ShowLargePreview).GetImageCacheFile();

                            if (File.Exists(fp_d)) ShellOpenFileProperty.Execute(fp_d);
                            else if (File.Exists(fp_o)) ShellOpenFileProperty.Execute(fp_o);
                            else if (File.Exists(fp_p)) ShellOpenFileProperty.Execute(fp_p);
                        }
                    }
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (gallery.Count > 0) OpenFileProperties.Execute(gallery.GetSelected());
                }
                else if (obj is IList<PixivItem>)
                {
                    var gallery = obj as IList<PixivItem>;
                    if (gallery.Count() > 0 && ParallelExecutionConfirm(gallery))
                    {
                        foreach (var item in gallery)
                        {
                            await new Action(() =>
                            {
                                OpenFileProperties.Execute(item.GetDownloadedFiles());
                            }).InvokeAsync();
                        }
                    }
                }
                else if (obj is TilesPage)
                {
                    (obj as TilesPage).OpenImageProperties();
                }
                else if (obj is IllustDetailPage)
                {
                    (obj as IllustDetailPage).OpenImageProperties();
                }
                else if (obj is IllustImageViewerPage)
                {
                    (obj as IllustImageViewerPage).OpenImageProperties();
                }
                else if (obj is Window)
                {
                    var win = obj as Window;
                    if (win.Content is Page) OpenFileProperties.Execute(win.Content);
                }
            }
            catch (Exception ex) { ex.ERROR("OpenFileProperties"); }
        });

        public static ICommand OpenHistory { get; } = new DelegateCommand(async () =>
        {
            try
            {
                var title = Application.Current.HistoryTitle();
                if (Application.Current.ContentWindowExists(title))
                {
                    await title.ActiveByTitle();
                    return;
                }

                await new Action(async () =>
                {
                    var page = new HistoryPage() { Name = "HistoryList", FontFamily = setting.FontFamily, Title = title };
                    var viewer = new ContentWindow(title)
                    {
                        Title = title,
                        Width = WIDTH_SEARCH,
                        Height = HEIGHT_DEF,
                        MinWidth = WIDTH_SEARCH,
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
            catch (Exception ex) { ex.ERROR("OpenHistory"); }
        });

        public static ICommand AddToHistory { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is Pixeez.Objects.Work || obj is Pixeez.Objects.User || obj is Pixeez.Objects.UserBase)
            {
                try
                {
                    await new Action(() =>
                    {
                        var win = Application.Current.HistoryTitle().GetWindowByTitle();
                        if (win is ContentWindow && win.Content is HistoryPage)
                            (win.Content as HistoryPage).AddToHistory(obj);
                        else
                        {
                            if (obj is Pixeez.Objects.Work) Application.Current.HistoryAdd(obj as Pixeez.Objects.Work);
                            else if (obj is Pixeez.Objects.User) Application.Current.HistoryAdd(obj as Pixeez.Objects.User);
                            else if (obj is Pixeez.Objects.UserBase) Application.Current.HistoryAdd(obj as Pixeez.Objects.UserBase);
                        }
                        Application.Current.DoEvents();
                    }).InvokeAsync();
                }
                catch (Exception ex) { ex.ERROR("AddToHistory"); }
            }
        });

        public static ICommand Open { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is IEnumerable<PixivItem>)
            {
                foreach (var item in (obj as IEnumerable<PixivItem>))
                {
                    OpenItem.Execute(obj);
                }
            }
            else if (obj is ImageListGrid)
            {
                OpenGallery.Execute(obj);
            }
            else if (obj is PixivItem)
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
            else if (obj is TilesPage)
            {
                (obj as TilesPage).OpenIllust();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).OpenIllust();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).OpenIllust();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).OpenIllust();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).OpenIllust();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) Open.Execute(win.Content);
            }
            else if (obj is string)
            {
                OpenSearch.Execute(obj as string);
            }
        });

        public static ICommand AddDownloadItem { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            await new Action(() =>
            {
                OpenDownloadManager.Execute(false);
                var _downManager = Application.Current.GetDownloadManager();
                if (_downManager is DownloadManagerPage && obj is DownloadParams)
                {
                    var dp = obj as DownloadParams;
                    _downManager.Add(dp.Url, dp.ThumbUrl, dp.Timestamp, dp.IsSinglePage, dp.OverwriteExists, jpeg: dp.SaveAsJPEG, largepreview: dp.SaveLargePreview);
                }
            }).InvokeAsync();
        });

        public static ICommand RunDownloadItemAction { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is Action<DownloadInfo>)
            {
                var _downManager = Application.Current.GetDownloadManager();
                if (_downManager is DownloadManagerPage)
                {
                    var items = _downManager.GetSelectedItems();
                    if (items is IEnumerable<DownloadInfo> && items.Count() > 0 && ParallelExecutionConfirm(items))
                    {
                        foreach (var item in items)
                        {
                            if (item is DownloadInfo)
                            {
                                await new Action(() =>
                                {
                                    try
                                    {
                                        (obj as Action<DownloadInfo>).Invoke(item);
                                    }
                                    catch (Exception ex) { ex.ERROR(); }
                                }).InvokeAsync();
                            }
                        }
                    }
                }            
            }
        });

        private static SemaphoreSlim CanOpenDownloadManager= new SemaphoreSlim(1, 1);
        public static ICommand OpenDownloadManager { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (Mouse.RightButton == MouseButtonState.Pressed)
            {
                await new Action(() =>
                {
                    CopyDownloadInfo.Execute(Application.Current.GetDownloadManager().GetDownloadInfo());
                }).InvokeAsync(true);
            }
            else if (await CanOpenDownloadManager.WaitAsync(TimeSpan.FromSeconds(60)))
            {
                try
                {
                    if (obj is bool)
                    {
                        var active = (bool)obj;

                        var title = Application.Current.DownloadTitle();
                        if (active ? await title.ActiveByTitle() : await title.ShowByTitle()) return;

                        await new Action(() =>
                        {
                            setting = Application.Current.LoadSetting();
                            var _downManager = Application.Current.GetDownloadManager();
                            var viewer = new ContentWindow(title)
                            {
                                Title = title,
                                MinWidth = 860,
                                MinHeight = 536,
                                Width = setting.DownloadManagerPosition.Width <= WIDTH_MIN + 80 ? WIDTH_MIN + 80 : setting.DownloadManagerPosition.Width,
                                Height = setting.DownloadManagerPosition.Height <= HEIGHT_MIN ? HEIGHT_MIN : setting.DownloadManagerPosition.Height,
                                Left = setting.DownloadManagerPosition.Left >=0 ? setting.DownloadManagerPosition.Left : _downManager.Pos.X,
                                Top = setting.DownloadManagerPosition.Top >=0 ? setting.DownloadManagerPosition.Top : _downManager.Pos.Y,
                                FontFamily = setting.FontFamily,
                                Content = _downManager
                            };
                            _downManager.ParentWindow = viewer;
                            viewer.Show();
                        }).InvokeAsync(true);
                    }
                }
                catch (Exception ex) { ex.ERROR("OpenDownloadManager"); }
                finally
                {
                    if (CanOpenDownloadManager is SemaphoreSlim && CanOpenDownloadManager.CurrentCount <= 0) CanOpenDownloadManager.Release();
                }
            }
        });

        public static ICommand OpenSearch { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is string && !string.IsNullOrEmpty((string)obj))
            {
                try
                {
                    var content = CommonHelper.ParseLink((string)obj);
                    if (!string.IsNullOrEmpty(content))
                    {
                        if (content.StartsWith("Local:"))
                        {
                            //var words = Regex.Replace(content.Replace("Local:", "").Trim(), @"(AND|OR|NOT)", Environment.NewLine, RegexOptions.IgnoreCase);
                            var so = new SearchObject(content.Replace("Local:", "").Trim(), raw: true);
                            SearchInStorage.Execute(so);
                            return;
                        }
                        else if (content.StartsWith("IllustID:", StringComparison.CurrentCultureIgnoreCase))
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
                        if (Application.Current.ContentWindowExists(title))
                        {
                            await title.ActiveByTitle();
                            return;
                        }

                        await new Action(async () =>
                        {
                            var page = new SearchResultPage() { Name = "SearchResult", FontFamily = setting.FontFamily, Contents = content, Title = title };
                            var viewer = new ContentWindow(title)
                            {
                                Title = title,
                                Width = WIDTH_SEARCH,
                                Height = HEIGHT_DEF,
                                MinWidth = WIDTH_SEARCH,
                                MaxWidth = WIDTH_SEARCH,
                                MinHeight = HEIGHT_MIN,
                                MaxHeight = HEIGHT_MAX,
                                FontFamily = setting.FontFamily,
                                Content = page
                            };
                            viewer.Show();
                            await Task.Delay(1);
                            Application.Current.DoEvents();
                        }).InvokeAsync();
                    }
                }
                catch (Exception ex) { ex.ERROR("OpenSearch"); }
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
                        catch (Exception ex) { ex.ShowExceptionToast(tag: "OpenSearch"); }
                    }
                }).InvokeAsync();
            }
        });

        public static ICommand SearchInStorage { get; } = new DelegateCommand<dynamic>(obj =>
        {
            try
            {
                if (obj is string)
                {
                    var text = obj as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        text = text.KatakanaHalfToFull();
                        Application.Current.SearchInFolder(text);
                    }
                }
                else if (obj is SearchObject)
                {
                    var s = obj as SearchObject;
                    Application.Current.SearchInFolder(s);
                }
            }
            catch (Exception ex) { ex.ERROR("SearchInStorage"); }
        });

        public static ICommand SearchInWeb { get; } = new DelegateCommand<dynamic>(obj =>
        {
            try
            {
                if (obj is string)
                {
                    var text = obj as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        text = text.KatakanaHalfToFull();
                        Application.Current.SearchInWeb(text);
                    }
                }
                else if (obj is SearchObject)
                {
                    var s = obj as SearchObject;
                    Application.Current.SearchInWeb(s);
                }
            }
            catch (Exception ex) { ex.ERROR("SearchInStorage"); }
        });

        public static ICommand ConvertToJpeg { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                var keep_name = Keyboard.Modifiers == ModifierKeys.Shift ? true : false;
                var force = Keyboard.Modifiers == ModifierKeys.Control ? true : false;
                var setting = Application.Current.LoadSetting();
                if (obj is int || obj is int? || obj is long || obj is long?)
                {
                    var illust = $"{obj}".FindIllust();
                    if (illust.IsWork()) ConvertToJpeg.Execute(illust);
                }
                else if (obj is string)
                {
                    var str = obj as string;
                    if (!string.IsNullOrEmpty(str))
                    {
                        if (File.Exists(str))
                        {
                            var id = str.GetIllustId();
                            var idx = str.GetIllustPageIndex();
                            var illust = id.FindIllust();
                            await str.ConvertImageTo("jpg", keep_name: keep_name, quality: setting.DownloadConvertJpegQuality, force: force);
                        }
                        else
                        {
                            var illust = $"{str}".FindIllust();
                            if (illust.IsWork()) ConvertToJpeg.Execute(illust);
                        }
                    }
                }
                else if (obj is Pixeez.Objects.Work)
                {
                    var illust = obj as Pixeez.Objects.Work;
                    var item = illust.WorkItem();
                    ConvertToJpeg.Execute(item);
                }
                else if (obj is IEnumerable<Pixeez.Objects.Work>)
                {
                    foreach (var illust in (obj as IEnumerable<Pixeez.Objects.Work>))
                        ConvertToJpeg.Execute(illust);
                }
                else if (obj is PixivItem)
                {
                    var type = setting.ConvertKeepName || keep_name ? DownloadType.ConvertKeepName : DownloadType.None;
                    var item = new KeyValuePair<PixivItem, DownloadType>(obj, type);
                    ConvertToJpeg.Execute(item);
                }
                else if (obj is KeyValuePair<PixivItem, DownloadType>)
                {
                    var kv = (KeyValuePair<PixivItem, DownloadType>)obj;
                    var item = kv.Key;
                    var type = kv.Value;
                    keep_name = setting.ConvertKeepName || type.HasFlag(DownloadType.ConvertKeepName) ? true : false;
                    if (item.IsWork())
                    {
                        var illust = item.Illust;

                        string fp = string.Empty;

                        if (item.HasPages() && item.IsNotPage())
                        {
                            for (var i = 0; i < item.Count; i++)
                            {
                                if (illust.IsDownloadedAsync(out fp, i, touch: false))
                                {
                                    await fp.ConvertImageTo("jpg", keep_name: keep_name, quality: setting.DownloadConvertJpegQuality, force: force);
                                }
                            }
                        }
                        else
                        {
                            if (item.IsPage() || item.IsPages())
                                illust.IsDownloadedAsync(out fp, item.Index, touch: false);
                            else
                                illust.IsPartDownloadedAsync(out fp, touch: false);

                            await fp.ConvertImageTo("jpg", keep_name: keep_name, quality: setting.DownloadConvertJpegQuality, force: force);
                        }
                    }
                }
                else if (obj is KeyValuePair<ImageListGrid, DownloadType>)
                {
                    var kv = (KeyValuePair<ImageListGrid, DownloadType>)obj;
                    var gallery = kv.Key;
                    var type = kv.Value;

                    if (gallery.Count > 0)
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery.GetSelected())
                            {
                                await new Action(() =>
                                {
                                    ConvertToJpeg.Execute(new KeyValuePair<PixivItem, DownloadType>(item, type));
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (gallery.Count > 0)
                    {
                        var type = setting.ConvertKeepName || Keyboard.Modifiers == ModifierKeys.Shift ? DownloadType.ConvertKeepName : DownloadType.None;
                        ConvertToJpeg.Execute(new KeyValuePair<ImageListGrid, DownloadType>(gallery, type));
                    }
                }
                else if (obj is IList<PixivItem>)
                {
                    var gallery = obj as IList<PixivItem>;
                    if (gallery.Count > 0)
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery)
                            {
                                await new Action(() =>
                                {
                                    ConvertToJpeg.Execute(item);
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("OpenDownloaded"); }
        });

        public static ICommand ReduceJpeg { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                var keep_name = Keyboard.Modifiers == ModifierKeys.Shift ? true : false;
                var force = Keyboard.Modifiers == ModifierKeys.Control ? true : false;
                var setting = Application.Current.LoadSetting();
                if (obj is int || obj is int? || obj is long || obj is long?)
                {
                    var illust = $"{obj}".FindIllust();
                    if (illust.IsWork()) ReduceJpeg.Execute(illust);
                }
                else if (obj is string)
                {
                    var str = obj as string;
                    if (!string.IsNullOrEmpty(str))
                    {
                        if (File.Exists(str))
                        {
                            var id = str.GetIllustId();
                            var idx = str.GetIllustPageIndex();
                            var illust = id.FindIllust();
                            await Task.Run(async () =>
                            {
                                var ret = await str.ReduceImageFileSize("jpg", keep_name: keep_name, quality: setting.DownloadRecudeJpegQuality, force: force);
                                return (ret);
                            });
                            //await str.ReduceImageFileSize("jpg", keep_name: keep_name, quality: setting.DownloadRecudeJpegQuality, force: force);
                        }
                        else
                        {
                            var illust = $"{str}".FindIllust();
                            if (illust.IsWork()) ReduceJpeg.Execute(illust);
                        }
                    }
                }
                else if (obj is Tuple<string, int>)
                {
                    var target = obj as Tuple<string, int>;
                    var str = target.Item1;
                    var q = target.Item2;
                    if (!string.IsNullOrEmpty(str))
                    {
                        if (File.Exists(str))
                        {
                            var id = str.GetIllustId();
                            var idx = str.GetIllustPageIndex();
                            var illust = id.FindIllust();
                            await Task.Run(async () =>
                            {
                                var ret = await str.ReduceImageFileSize("jpg", keep_name: keep_name, quality: q, force: force);
                                return (ret);
                            });
                            //await str.ReduceImageFileSize("jpg", keep_name: keep_name, quality: q, force: force);
                        }
                        else
                        {
                            var illust = $"{str}".FindIllust();
                            if (illust.IsWork()) ReduceJpeg.Execute(new Tuple<Pixeez.Objects.Work, int>(illust, q));
                        }
                    }
                }
                else if (obj is Pixeez.Objects.Work)
                {
                    var illust = obj as Pixeez.Objects.Work;
                    var item = illust.WorkItem();
                    ReduceJpeg.Execute(item);
                }
                else if (obj is Tuple<Pixeez.Objects.Work, int>)
                {
                    var target = obj as Tuple<Pixeez.Objects.Work, int>;
                    var illust = target.Item1;
                    var q = target.Item2;
                    var item = illust.WorkItem();
                    ReduceJpeg.Execute(new Tuple<PixivItem, int>(item, q));
                }
                else if (obj is KeyValuePair<Pixeez.Objects.Work, int>)
                {
                    var target = (KeyValuePair<Pixeez.Objects.Work, int>)obj;
                    var illust = target.Key;
                    var q = target.Value;
                    var item = illust.WorkItem();
                    ReduceJpeg.Execute(new Tuple<PixivItem, int>(item, q));
                }
                else if (obj is IEnumerable<Pixeez.Objects.Work>)
                {
                    foreach (var illust in (obj as IEnumerable<Pixeez.Objects.Work>))
                        ReduceJpeg.Execute(illust);
                }
                else if (obj is PixivItem)
                {
                    var type = setting.ConvertKeepName || keep_name ? DownloadType.ConvertKeepName : DownloadType.None;
                    var item = new KeyValuePair<PixivItem, DownloadType>(obj, type);
                    ReduceJpeg.Execute(item);
                }
                else if (obj is Tuple<PixivItem, int>)
                {
                    var type = setting.ConvertKeepName || keep_name ? DownloadType.ConvertKeepName : DownloadType.None;
                    var target = obj as Tuple<PixivItem, int>;
                    var item = new Tuple<PixivItem, DownloadType, int>(target.Item1, type, target.Item2);
                    ReduceJpeg.Execute(item);
                }
                else if (obj is KeyValuePair<PixivItem, DownloadType>)
                {
                    var kv = (KeyValuePair<PixivItem, DownloadType>)obj;
                    var item = kv.Key;
                    var type = kv.Value;
                    keep_name = setting.ConvertKeepName || type.HasFlag(DownloadType.ConvertKeepName) ? true : false;
                    if (item.IsWork())
                    {
                        var illust = item.Illust;

                        string fp = string.Empty;

                        if (item.HasPages() && item.IsNotPage())
                        {
                            for (var i = 0; i < item.Count; i++)
                            {
                                if (illust.IsDownloadedAsync(out fp, i, touch: false))
                                {
                                    await Task.Run(async () =>
                                    {
                                        var ret = await fp.ReduceImageFileSize("jpg", keep_name: keep_name, quality: setting.DownloadRecudeJpegQuality, force: force);
                                        return (ret);
                                    });
                                    //await fp.ReduceImageFileSize("jpg", keep_name: keep_name, quality: setting.DownloadRecudeJpegQuality, force: force);
                                }
                            }
                        }
                        else
                        {
                            if (item.IsPage() || item.IsPages())
                                illust.IsDownloadedAsync(out fp, item.Index, touch: false);
                            else
                                illust.IsPartDownloadedAsync(out fp, touch: false);

                            await Task.Run(async () =>
                            {
                                var ret = await fp.ReduceImageFileSize("jpg", keep_name: keep_name, quality: setting.DownloadRecudeJpegQuality, force: force);
                                return (ret);
                            });
                            //await fp.ReduceImageFileSize("jpg", keep_name: keep_name, quality: setting.DownloadRecudeJpegQuality, force: force);
                        }
                    }
                }
                else if (obj is Tuple<PixivItem, DownloadType, int>)
                {
                    var kv = obj as Tuple<PixivItem, DownloadType, int>;
                    var item = kv.Item1;
                    var type = kv.Item2;
                    var q = kv.Item3;
                    keep_name = setting.ConvertKeepName || type.HasFlag(DownloadType.ConvertKeepName) ? true : false;
                    if (item.IsWork())
                    {
                        var illust = item.Illust;

                        string fp = string.Empty;

                        if (item.HasPages() && item.IsNotPage())
                        {
                            for (var i = 0; i < item.Count; i++)
                            {
                                if (illust.IsDownloadedAsync(out fp, i, touch: false))
                                {
                                    await Task.Run(async () =>
                                    {
                                        var ret = await fp.ReduceImageFileSize("jpg", keep_name: keep_name, quality: q, force: force);
                                        return (ret);
                                    });
                                    //await fp.ReduceImageFileSize("jpg", keep_name: keep_name, quality: q, force: force);
                                }
                            }
                        }
                        else
                        {
                            if (item.IsPage() || item.IsPages())
                                illust.IsDownloadedAsync(out fp, item.Index, touch: false);
                            else
                                illust.IsPartDownloadedAsync(out fp, touch: false);

                            await Task.Run(async () =>
                            {
                                var ret = await fp.ReduceImageFileSize("jpg", keep_name: keep_name, quality: q, force: force);
                                return (ret);
                            });
                            //await fp.ReduceImageFileSize("jpg", keep_name: keep_name, quality: q, force: force);
                        }
                    }
                }
                else if (obj is Tuple<KeyValuePair<PixivItem, DownloadType>, int>)
                {
                    var kv = obj as Tuple<KeyValuePair<PixivItem, DownloadType>, int>;
                    var item = kv.Item1.Key;
                    var type = kv.Item1.Value;
                    var q = kv.Item2;

                    await new Action(() =>
                    {
                        ReduceJpeg.Execute(new Tuple<PixivItem, DownloadType, int>(item, type, q));
                    }).InvokeAsync();
                }
                else if (obj is Tuple<ImageListGrid, DownloadType>)
                {
                    var kv = obj as Tuple<ImageListGrid, DownloadType>;
                    var gallery = kv.Item1;
                    var type = kv.Item2;

                    if (gallery.Count > 0)
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery.GetSelected())
                            {
                                await new Action(() =>
                                {
                                    ReduceJpeg.Execute(new KeyValuePair<PixivItem, DownloadType>(item, type));
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
                else if (obj is Tuple<ImageListGrid, DownloadType, int>)
                {
                    var kv = obj as Tuple<ImageListGrid, DownloadType, int>;
                    var gallery = kv.Item1;
                    var type = kv.Item2;
                    var q = kv.Item3;

                    if (gallery.Count > 0)
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery.GetSelected())
                            {
                                await new Action(() =>
                                {
                                    ReduceJpeg.Execute(new Tuple<PixivItem, DownloadType, int>(item, type, q));
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
                else if (obj is Tuple<KeyValuePair<ImageListGrid, DownloadType>, int>)
                {
                    var kv = obj as Tuple<KeyValuePair<ImageListGrid, DownloadType>, int>;
                    var gallery = kv.Item1.Key;
                    var type = kv.Item1.Value;
                    var q = kv.Item2;

                    if (gallery.Count > 0)
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery.GetSelected())
                            {
                                await new Action(() =>
                                {
                                    ReduceJpeg.Execute(new Tuple<PixivItem, DownloadType, int>(item, type, q));
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
                else if (obj is KeyValuePair<ImageListGrid, DownloadType>)
                {
                    var kv = (KeyValuePair<ImageListGrid, DownloadType>)obj;
                    var gallery = kv.Key;
                    var type = kv.Value;

                    if (gallery.Count > 0)
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery.GetSelected())
                            {
                                await new Action(() =>
                                {
                                    ReduceJpeg.Execute(new KeyValuePair<PixivItem, DownloadType>(item, type));
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (gallery.Count > 0)
                    {
                        var type = setting.ConvertKeepName || Keyboard.Modifiers == ModifierKeys.Shift ? DownloadType.ConvertKeepName : DownloadType.None;
                        ReduceJpeg.Execute(new KeyValuePair<ImageListGrid, DownloadType>(gallery, type));
                    }
                }
                else if (obj is IList<KeyValuePair<PixivItem, DownloadType>>)
                {
                    var gallery = obj as IList<KeyValuePair<PixivItem, DownloadType>>;
                    if (gallery.Count > 0)
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery)
                            {
                                await new Action(() =>
                                {
                                    ReduceJpeg.Execute(item);
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
                else if (obj is KeyValuePair<IList<PixivItem>, DownloadType>)
                {
                    var gallery = (KeyValuePair<IList<PixivItem>, DownloadType>)obj;
                    if (gallery.Key is IList<PixivItem> && gallery.Key.Count > 0)
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery.Key)
                            {
                                await new Action(() =>
                                {
                                    ReduceJpeg.Execute(new KeyValuePair<PixivItem, DownloadType>(item, gallery.Value));
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
                else if (obj is Tuple<IList<PixivItem>, DownloadType>)
                {
                    var gallery = obj as Tuple<IList<PixivItem>, DownloadType>;
                    if (gallery.Item1 is IList<PixivItem> && gallery.Item1.Count > 0)
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery.Item1)
                            {
                                await new Action(() =>
                                {
                                    ReduceJpeg.Execute(new KeyValuePair<PixivItem, DownloadType>(item, gallery.Item2));
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
                else if (obj is Tuple<IList<PixivItem>, DownloadType, int>)
                {
                    var gallery = obj as Tuple<IList<PixivItem>, DownloadType, int>;
                    if (gallery.Item1 is IList<PixivItem> && gallery.Item1.Count > 0)
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery.Item1)
                            {
                                await new Action(() =>
                                {
                                    ReduceJpeg.Execute(new Tuple<PixivItem, DownloadType, int>(item, gallery.Item2, gallery.Item3));
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
                else if (obj is IList<Tuple<PixivItem, DownloadType, int>>)
                {
                    var gallery = obj as IList<Tuple<PixivItem, DownloadType, int>>;
                    if (gallery.Count > 0)
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery)
                            {
                                await new Action(() =>
                                {
                                    ReduceJpeg.Execute(item);
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
                else if (obj is IList<PixivItem>)
                {
                    var gallery = obj as IList<PixivItem>;
                    if (gallery.Count > 0)
                    {
                        await new Action(async () =>
                        {
                            foreach (var item in gallery)
                            {
                                await new Action(() =>
                                {
                                    ReduceJpeg.Execute(item);
                                }).InvokeAsync();
                            }
                        }).InvokeAsync();
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("OpenDownloaded"); }
        });

        public static ICommand SaveIllust { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is KeyValuePair<PixivItem, DownloadType>)
            {
                var kv = (KeyValuePair<PixivItem, DownloadType>)obj;
                var item = kv.Key;
                var type = kv.Value;

                if (item.IsWork())
                {
                    await new Action(() =>
                    {
                        setting = Application.Current.LoadSetting();
                        if (setting.DownloadWithBookmarked && !item.IsFavorited) LikeIllust.Execute(item);
                        if (setting.DownloadWithAutoReduce && !type.HasFlag(DownloadType.Original)) type |= DownloadType.AsJPEG;

                        var dt = item.Illust.GetDateTime();
                        var is_meta_single_page = (item.Illust.PageCount ?? 0) <= 1 ? true : false;
                        if (item.IsPage() || item.IsPages())
                        {
                            var url = type.HasFlag(DownloadType.UseLargePreview) ? item.Illust.GetPreviewUrl(item.Index, large: true) : item.Illust.GetOriginalUrl(item.Index);
                            if (!string.IsNullOrEmpty(url))
                            {
                                url.SaveImage(
                                    item.Illust.GetThumbnailUrl(item.Index),
                                    dt, is_meta_single_page,
                                    jpeg: type.HasFlag(DownloadType.AsJPEG),
                                    largepreview: type.HasFlag(DownloadType.UseLargePreview)
                                );
                            }
                        }
                        else if (item.Illust is Pixeez.Objects.Work)
                        {
                            var url = type.HasFlag(DownloadType.UseLargePreview) ? item.Illust.GetPreviewUrl(item.Index, large: true) : item.Illust.GetOriginalUrl(item.Index);
                            if (!string.IsNullOrEmpty(url))
                            {
                                url.SaveImage(
                                    item.Illust.GetThumbnailUrl(item.Index),
                                    dt, is_meta_single_page,
                                    jpeg: type.HasFlag(DownloadType.AsJPEG),
                                    largepreview: type.HasFlag(DownloadType.UseLargePreview)
                                );
                            }
                        }
                    }).InvokeAsync();
                }
            }
            else if (obj is KeyValuePair<PixivItem, bool>)
            {
                var kv = (KeyValuePair<PixivItem, bool>)obj;
                var item = kv.Key;
                var jpeg = kv.Value;

                SaveIllust.Execute(new KeyValuePair<PixivItem, DownloadType>(item, jpeg ? DownloadType.AsJPEG : DownloadType.None));
            }
            else if (obj is PixivItem)
            {
                SaveIllust.Execute(new KeyValuePair<PixivItem, bool>(obj as PixivItem, false));
            }
            else if (obj is KeyValuePair<ImageListGrid, DownloadType>)
            {
                setting = Application.Current.LoadSetting();
                var kv = (KeyValuePair<ImageListGrid, DownloadType>)obj;
                var gallery = kv.Key as ImageListGrid;
                var type = kv.Value;
                await new Action(async () =>
                {
                    foreach (var item in gallery.GetSelected())
                    {
                        await new Action(() =>
                        {
                            SaveIllust.Execute(new KeyValuePair<PixivItem, DownloadType>(item as PixivItem, type));
                        }).InvokeAsync();
                    }
                }).InvokeAsync();
            }
            else if (obj is ImageListGrid)
            {
                setting = Application.Current.LoadSetting();
                var gallery = obj as ImageListGrid;
                var type = DownloadType.None;
                await new Action(async () =>
                {
                    foreach (var item in gallery.GetSelected())
                    {
                        await new Action(() =>
                        {
                            SaveIllust.Execute(new KeyValuePair<PixivItem, DownloadType>(item as PixivItem, type));
                        }).InvokeAsync();
                    }
                }).InvokeAsync();
            }
            else if (obj is string)
            {
                var link = obj as string;
                var patten = @"(https?://.*?\.pximg\.net/img-original/img/\d{4}/\d{2}/\d{2}/\d{2}/\d{2}/\d{2}/)?(\d+)(_p?(\d+))?\..*?$";
                var id = Regex.Replace(link, patten, "$2", RegexOptions.IgnoreCase);
                var index = Regex.Replace(link, patten, "$4", RegexOptions.IgnoreCase);
                if (!string.IsNullOrEmpty(id))
                {
                    var illust = id.FindIllust();
                    if (!(illust is Pixeez.Objects.Work)) illust = await id.RefreshIllust();
                    if (illust is Pixeez.Objects.Work)
                    {
                        var item = illust.WorkItem();
                        int idx = item.Index;
                        int.TryParse(index, out idx);
                        item.Index = idx;
                        SaveIllust.Execute(item);
                    }
                }
            }
            else if (obj is TilesPage)
            {
                (obj as TilesPage).SaveIllust();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).SaveIllust();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).SaveIllust();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).SaveIllust();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).SaveIllust();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) SaveIllust.Execute(win.Content);
            }
        });

        public static ICommand SaveIllustAll { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is KeyValuePair<PixivItem, DownloadType>)
            {
                var kv = (KeyValuePair<PixivItem, DownloadType>)obj;
                var item = kv.Key;
                var type = kv.Value;

                if (item.IsWork())
                {
                    setting = Application.Current.LoadSetting();
                    if (setting.DownloadWithBookmarked && !item.IsFavorited) LikeIllust.Execute(item);
                    if (setting.DownloadWithAutoReduce && !type.HasFlag(DownloadType.Original)) type |= DownloadType.AsJPEG;

                    var illust = item.Illust;
                    var dt = illust.GetDateTime();

                    if (item.HasPages())
                    {
                        await new Action(() =>
                        {
                            for (var idx = 0; idx < item.Count; idx++)
                            {
                                //var page = item.Illust.WorkItem(work_type: PixivItemType.Page);
                                //page.Index = idx;
                                //page.Count = item.Count;
                                //SaveIllust.Execute(new KeyValuePair<PixivItem, DownloadType>(page, type));
                                var url = type.HasFlag(DownloadType.UseLargePreview) ? item.Illust.GetPreviewUrl(idx, large: true) : item.Illust.GetOriginalUrl(idx);
                                if (!string.IsNullOrEmpty(url))
                                {
                                    url.SaveImage(
                                        item.Illust.GetThumbnailUrl(idx),
                                        dt, false,
                                        jpeg: type.HasFlag(DownloadType.AsJPEG),
                                        largepreview: type.HasFlag(DownloadType.UseLargePreview)
                                    );
                                }
                            }
                        }).InvokeAsync();
                    }
                    else SaveIllust.Execute(kv);
                }
            }
            else if (obj is KeyValuePair<PixivItem, bool>)
            {
                var kv = (KeyValuePair<PixivItem, bool>)obj;
                var item = kv.Key;
                var jpeg = kv.Value;

                SaveIllustAll.Execute(new KeyValuePair<PixivItem, DownloadType>(item, jpeg ? DownloadType.AsJPEG : DownloadType.None));
            }
            else if (obj is PixivItem)
            {
                SaveIllustAll.Execute(new KeyValuePair<PixivItem, bool>(obj as PixivItem, false));
            }
            else if (obj is KeyValuePair<ImageListGrid, DownloadType>)
            {
                setting = Application.Current.LoadSetting();
                var kv = (KeyValuePair<ImageListGrid, DownloadType>)obj;
                var gallery = kv.Key as ImageListGrid;
                var type = kv.Value;
                await new Action(async () =>
                {
                    foreach (var item in gallery.GetSelected())
                    {
                        await new Action(() =>
                        {
                            SaveIllustAll.Execute(new KeyValuePair<PixivItem, DownloadType>(item as PixivItem, type));
                        }).InvokeAsync();
                    }
                }).InvokeAsync();
            }
            else if (obj is ImageListGrid)
            {
                setting = Application.Current.LoadSetting();
                var gallery = obj as ImageListGrid;
                var type = DownloadType.None;
                await new Action(async () =>
                {
                    foreach (var item in gallery.GetSelected())
                    {
                        await new Action(() =>
                        {
                            SaveIllustAll.Execute(new KeyValuePair<PixivItem, DownloadType>(item as PixivItem, type));
                        }).InvokeAsync();
                    }
                }).InvokeAsync();
            }
            else if (obj is string)
            {
                var link = obj as string;
                var patten = @"(https?://.*?\.pximg\.net/img-original/img/\d{4}/\d{2}/\d{2}/\d{2}/\d{2}/\d{2}/)?(\d+)(_p?(\d+))?\..*?$";
                var id = Regex.Replace(link, patten, "$2", RegexOptions.IgnoreCase);
                if (!string.IsNullOrEmpty(id))
                {
                    var illust = id.FindIllust();
                    if (!(illust is Pixeez.Objects.Work)) illust = await id.RefreshIllust();
                    if (illust is Pixeez.Objects.Work)
                    {
                        var item = illust.WorkItem();
                        SaveIllustAll.Execute(item);
                    }
                }
            }
            else if (obj is TilesPage)
            {
                (obj as TilesPage).SaveIllustAll();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).SaveIllustAll();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).SaveIllustAll();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).SaveIllustAll();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).SaveIllustAll();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) SaveIllustAll.Execute(win.Content);
            }
        });

        public static ICommand SavePreviewUgoira { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is PixivItem)
            {
                await new Action(async () =>
                {
                    var item = obj as PixivItem;
                    if (item.IsUgoira())
                    {
                        setting = Application.Current.LoadSetting();
                        if (setting.DownloadWithBookmarked && !item.IsFavorited) LikeIllust.Execute(item);

                        var dt = item.Illust.GetDateTime();
                        var is_meta_single_page = (item.Illust.PageCount ?? 0) <= 1 ? true : false;
                        var info = item.Ugoira != null ? item.Ugoira : await item.Illust.GetUgoiraMeta(ajax: true);
                        if (info != null)
                        {
                            item.Ugoira = info;
                            var url =  info.GetUgoiraUrl(preview: true);
                            if (!string.IsNullOrEmpty(url))
                            {
                                url.SaveImage(item.Illust.GetThumbnailUrl(), dt, is_meta_single_page);
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
                    foreach (var item in gallery.GetSelected().Where(i => i.IsUgoira()))
                    {
                        await new Action(() =>
                        {
                            SavePreviewUgoira.Execute(item);
                        }).InvokeAsync();
                    }
                }).InvokeAsync();
            }
            else if (obj is string)
            {
                var link = obj as string;
                var patten = @"(https?://.*?\.pximg\.net/img-zip-ugoira/img/\d{4}/\d{2}/\d{2}/\d{2}/\d{2}/\d{2}/)?(\d+)(_ugoira(\d+)x(\d+)).zip$";
                var id = Regex.Replace(link, patten, "$2", RegexOptions.IgnoreCase);
                if (!string.IsNullOrEmpty(id))
                {
                    var illust = id.FindIllust();
                    if (!(illust is Pixeez.Objects.Work)) illust = await id.RefreshIllust();
                    if (illust is Pixeez.Objects.Work)
                    {
                        var item = illust.WorkItem();
                        SavePreviewUgoira.Execute(item);
                    }
                }
            }
            else if (obj is TilesPage)
            {
                (obj as TilesPage).SaveUgoira();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).SaveUgoira();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).SaveUgoira();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).SaveUgoira();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).SaveUgoira();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) SavePreviewUgoira.Execute(win.Content);
            }
        });

        public static ICommand SaveOriginalUgoira { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is PixivItem)
            {
                await new Action(async () =>
                {
                    var item = obj as PixivItem;
                    if (item.IsUgoira())
                    {
                        setting = Application.Current.LoadSetting();
                        if (setting.DownloadWithBookmarked && !item.IsFavorited) LikeIllust.Execute(item);

                        var dt = item.Illust.GetDateTime();
                        var is_meta_single_page = (item.Illust.PageCount ?? 0) <= 1 ? true : false;
                        var info = item.Ugoira != null ? item.Ugoira : await item.Illust.GetUgoiraMeta(ajax: true);
                        if (info != null)
                        {
                            item.Ugoira = info;
                            var url =  info.GetUgoiraUrl();
                            if (!string.IsNullOrEmpty(url))
                            {
                                url.SaveImage(item.Illust.GetThumbnailUrl(), dt, is_meta_single_page);
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
                    foreach (var item in gallery.GetSelected().Where(i => i.IsUgoira()))
                    {
                        await new Action(() =>
                        {
                            SavePreviewUgoira.Execute(item);
                        }).InvokeAsync();
                    }
                }).InvokeAsync();
            }
            else if (obj is string)
            {
                var link = obj as string;
                var patten = @"(https?://.*?\.pximg\.net/img-zip-ugoira/img/\d{4}/\d{2}/\d{2}/\d{2}/\d{2}/\d{2}/)?(\d+)(_ugoira(\d+)x(\d+)).zip$";
                var id = Regex.Replace(link, patten, "$2", RegexOptions.IgnoreCase);
                if (!string.IsNullOrEmpty(id))
                {
                    var illust = id.FindIllust();
                    if (!(illust is Pixeez.Objects.Work)) illust = await id.RefreshIllust();
                    if (illust is Pixeez.Objects.Work)
                    {
                        var item = illust.WorkItem();
                        SavePreviewUgoira.Execute(item);
                    }
                }
            }
            else if (obj is TilesPage)
            {
                (obj as TilesPage).SaveUgoira();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).SaveUgoira();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).SaveUgoira();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).SaveUgoira();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).SaveUgoira();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) SavePreviewUgoira.Execute(win.Content);
            }
        });

        public static ICommand ShellOpenUgoira { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is string)
            {
                var content = obj as string;
                if (!string.IsNullOrEmpty(content))
                {
                    await new Action(() =>
                    {
                        content.OpenFileWithShell();
                    }).InvokeAsync(true);
                }
            }
            else if (obj is Uri)
            {
                try
                {
                    var url = obj as Uri;
                    if ((url.IsFile || url.IsUnc) && File.Exists(url.LocalPath)) url.LocalPath.OpenFileWithShell();
                    else if (url.IsAbsoluteUri)
                    {
                        string fp_d = Uri.UnescapeDataString(url.AbsoluteUri).GetImageCacheFile();
                        if (File.Exists(fp_d)) fp_d.OpenFileWithShell();
                    }
                }
                catch (Exception ex) { ex.ERROR("ShellOpenFile"); }
            }
            else if (obj is PixivItem)
            {
                var item = obj as PixivItem;
                string fp = await item.GetUgoiraFile(preview: false) ?? await item.GetUgoiraFile(preview: true) ?? string.Empty;
                if (!string.IsNullOrEmpty(fp))
                {
                    await new Action(() =>
                    {
                        item.Ugoira.MakeUgoiraConcatFile(fp);
                        fp.OpenFileWithShell();
                    }).InvokeAsync(true);
                }
            }
        });

        public static ICommand OpenDropBox { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is System.Windows.Controls.Primitives.ToggleButton)
            {
                var sender = obj as System.Windows.Controls.Primitives.ToggleButton;
                await new Action(() =>
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control || Mouse.RightButton == MouseButtonState.Pressed)
                    {
                        IList<string> titles = Application.Current.OpenedWindowTitles();
                        if (titles.Count > 0) CopyText.Execute($"{string.Join(Environment.NewLine, titles)}{Environment.NewLine}");
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        SaveOpenedWindows.Execute(null);
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                    {
                        LoadLastOpenedWindows.Execute(null);
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.None)
                        Application.Current.ToggleDropBox();
                }).InvokeAsync(true);
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
            else if (obj is PixivItem)
            {
                var item = obj as PixivItem;
                if (item.IsWork()) SendToOtherInstance.Execute(item.Illust);
                else if (item.IsUser()) SendToOtherInstance.Execute(item.User);
            }
            else if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (IsNormalGallary(gallery))
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
                else if (IsPagesGallary(gallery))
                {
                    var page = gallery.TryFindParent<IllustDetailPage>();
                    if (page is IllustDetailPage)
                    {
                        if (page.Contents is PixivItem && page.Contents.IsWork())
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
            else if (obj is PixivItem)
            {
                var item = obj as PixivItem;
                if (item.IsUser()) ShellSendToOtherInstance.Execute(item.User);
                else if (item.IsWork()) ShellSendToOtherInstance.Execute(item.Illust);
            }
            else if (obj is ImageListGrid)
            {
                var gallery = obj as ImageListGrid;
                if (IsNormalGallary(gallery))
                {
                    var ids = new  List<string>();
                    foreach (var item in gallery.GetSelected())
                    {
                        if (item.IsUser()) ids.Add($"uid:{item.ID}");
                        else if (item.IsWork()) ids.Add($"id:{item.ID}");
                    }
                    ShellSendToOtherInstance.Execute(ids);
                }
                else if (IsPagesGallary(gallery))
                {
                    var page = gallery.TryFindParent<IllustDetailPage>();
                    if (page is IllustDetailPage)
                    {
                        if (page.Contents is PixivItem && page.Contents.IsWork())
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
                        content.OpenPixivPediaWithShell();
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
                    }).InvokeAsync(true);
                }
            }
            else if (obj is Uri)
            {
                try
                {
                    var url = obj as Uri;
                    if ((url.IsFile || url.IsUnc) && File.Exists(url.LocalPath)) url.LocalPath.OpenFileWithShell();
                    else if (url.IsAbsoluteUri)
                    {
                        string fp_d = Uri.UnescapeDataString(url.AbsoluteUri).GetImageCacheFile();
                        if (File.Exists(fp_d)) fp_d.OpenFileWithShell();
                    }
                }
                catch (Exception ex) { ex.ERROR("ShellOpenFile"); }
            }
            else if (obj is PixivItem)
            {
                var item = obj as PixivItem;
                string fp = item.Illust.GetOriginalUrl(item.Index).GetImageCacheFile();
                if (!string.IsNullOrEmpty(fp))
                {
                    await new Action(() =>
                    {
                        fp.OpenFileWithShell();
                    }).InvokeAsync(true);
                }
            }
        });

        public static ICommand ShellOpenFileProperty { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            if (obj is IEnumerable<string>)
            {
                var content = obj as IEnumerable<string>;
                await new Action(() =>
                {
                    content.OpenShellProperties();
                }).InvokeAsync(true);
            }
            else if (obj is string)
            {
                var content = obj as string;
                if (!string.IsNullOrEmpty(content))
                {
                    await new Action(() =>
                    {
                        content.OpenShellProperties();
                    }).InvokeAsync(true);
                }
            }
            else if (obj is Uri)
            {
                try
                {
                    var url = obj as Uri;
                    if ((url.IsFile || url.IsUnc) && File.Exists(url.LocalPath)) url.LocalPath.OpenShellProperties();
                    else if (url.IsAbsoluteUri)
                    {
                        string fp_d = Uri.UnescapeDataString(url.AbsoluteUri).GetImageCacheFile();
                        if (File.Exists(fp_d)) fp_d.OpenShellProperties();
                    }
                }
                catch (Exception ex) { ex.ERROR("ShellOpenFile"); }
            }
            else if (obj is PixivItem)
            {
                var item = obj as PixivItem;
                string fp = item.Illust.GetOriginalUrl(item.Index).GetImageCacheFile();
                if (!string.IsNullOrEmpty(fp))
                {
                    await new Action(() =>
                    {
                        fp.OpenShellProperties();
                    }).InvokeAsync(true);
                }
            }
        });

        public static ICommand SaveTags { get; } = new DelegateCommand(() =>
        {
            Application.Current.SaveTags();
        });

        public static ICommand OpenTags { get; } = new DelegateCommand<string>(async obj =>
        {
            setting = Application.Current.LoadSetting();
            var root = Application.Current.GetRoot();
            var tags = new List<string>()
            {
                Path.Combine(root, setting.CustomTagsFile),
                Path.Combine(root, setting.CustomWildcardTagsFile),
                Path.Combine(root, setting.TagsFile)
            };
            var content = obj is string && !string.IsNullOrEmpty(obj as string) ? obj as string : setting.CustomTagsFile;
            if (content.Equals("folder", StringComparison.CurrentCultureIgnoreCase))
            {
                if (tags.Count > 0)
                {
                    var file = tags.FirstOrDefault();
                    file.OpenFileWithShell(ShowFolder: true);
                }
                else
                {
                    root.OpenFileWithShell(ShowFolder: true);
                }
            }
            else
            {
                foreach (var tag in tags)
                {
                    if (tag.StartsWith(content, StringComparison.CurrentCultureIgnoreCase))
                    {
                        await new Action(() =>
                        {
                            var viewer = string.IsNullOrEmpty(setting.ShellTextViewer) ? setting.ShellLogViewer : setting.ShellTextViewer;
                            var param = string.IsNullOrEmpty(setting.ShellTextViewer) ? setting.ShellLogViewerParams : setting.ShellTextViewerParams;
                            if (string.IsNullOrEmpty(viewer)) viewer = "notepad.exe";
                            tag.OpenFileWithShell(command: viewer, custom_params: param);
                        }).InvokeAsync(true);
                        break;
                    }
                }
            }
        });

        private static DateTime lastSaveOpenedWindows = setting.LastOpenedFile.GetFileTime();
        public static ICommand SaveOpenedWindows { get; } = new DelegateCommand<bool?>(async obj =>
        {
            var force = obj is bool ? (bool)obj : false;
            await new Action(() =>
            {
                try
                {
                    setting = Application.Current.LoadSetting();
                    var now = DateTime.Now;
                    //if (setting.LastOpenedFile.GetFileTime().DeltaSeconds(now) > setting.LastOpenedFileAutoSaveFrequency)
                    if (force || now.DeltaSeconds(lastSaveOpenedWindows) > setting.LastOpenedFileAutoSaveFrequency)
                    {
                        IList<string> titles = Application.Current.OpenedWindowTitles();
                        if (titles.Count > 0)
                        {
                            var links = JsonConvert.SerializeObject(titles, Formatting.Indented);
                            File.WriteAllText(setting.LastOpenedFile, links, new UTF8Encoding(true));
                            lastSaveOpenedWindows = now;
                        }
                    }
                }
                catch (Exception ex) { ex.ERROR("SaveOpenedWindows"); }
            }).InvokeAsync();
        });

        public static ICommand LoadLastOpenedWindows { get; } = new DelegateCommand(async () =>
        {
            await new Action(() =>
            {
                try
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
                catch (Exception ex) { ex.ERROR("LoadLastOpenedWindows"); }
            }).InvokeAsync();
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

        public static ICommand WriteLogs { get; } = new DelegateCommand<string>(obj =>
        {
            if (obj is string)
            {
                var content = obj as string;
                if (!string.IsNullOrEmpty(content))
                {
                    content.INFO();
                }
            }
        });

        public static ICommand OpenLogs { get; } = new DelegateCommand<string>(async obj =>
        {
            var logs = Application.Current.GetLogs();

            var content = obj is string && !string.IsNullOrEmpty(obj as string) ? obj as string : "INFO";
            if (content.ToLower().Contains("folder"))
            {
                if (logs.Count > 0)
                {
                    var file = logs.FirstOrDefault();
                    file.OpenFileWithShell(ShowFolder: true);
                }
                else
                {
                    var folder = Application.Current.GetLogsFolder();
                    folder.OpenFileWithShell(ShowFolder: true);
                }
            }
            else
            {
                setting = Application.Current.LoadSetting();
                foreach (var log in logs)
                {
                    if (log.ToLower().Contains(content.ToLower()))
                    {
                        await new Action(() =>
                        {
                            var viewer = string.IsNullOrEmpty(setting.ShellLogViewer) ? setting.ShellTextViewer : setting.ShellLogViewer;
                            var param = string.IsNullOrEmpty(setting.ShellLogViewer) ? setting.ShellTextViewerParams : setting.ShellLogViewerParams;
                            if (string.IsNullOrEmpty(viewer)) viewer = "notepad.exe";
                            log.OpenFileWithShell(command: viewer, custom_params: param);
                        }).InvokeAsync(true);
                        break;
                    }
                }
            }
        });

        public static ICommand CleanLogs { get; } = new DelegateCommand(() =>
        {
            Application.Current.CleanLogs();
        });

        #region tiles navigation
        public static ICommand PrevCategory { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).PrevCategory();
            }
            else if (obj is Window)
            {
                var win = Application.Current.GetMainWindow();
                if (win is MainWindow && win.Content is TilesPage) PrevCategory.Execute(win.Content);
            }
        });

        public static ICommand NextCategory { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).NextCategory();
            }
            else if (obj is Window)
            {
                var win = Application.Current.GetMainWindow();
                if (win is MainWindow && win.Content is TilesPage) NextCategory.Execute(win.Content);
            }
        });

        public static ICommand FirstCategory { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).FirstCategory();
            }
            else if (obj is Window)
            {
                var win = Application.Current.GetMainWindow();
                if (win is MainWindow && win.Content is TilesPage) FirstCategory.Execute(win.Content);
            }
        });

        public static ICommand LastCategory { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).LastCategory();
            }
            else if (obj is Window)
            {
                var win = Application.Current.GetMainWindow();
                if (win is MainWindow && win.Content is TilesPage) LastCategory.Execute(win.Content);
            }
        });

        public static ICommand RefreshPage { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).UpdateTiles();
            }
            else if (obj is IllustDetailPage)
            {
                var page = obj as IllustDetailPage;
                if (page.Contents is PixivItem)
                {
                    var item = page.Contents;
                    if (item.IsWork())
                    {
                        var illust = page.Contents.ID.FindIllust();
                        if (illust.IsWork()) item = illust.WorkItem();
                    }
                    else if(item.IsUser())
                    {
                        var user = page.Contents.UserID.FindUser();
                        if (user is Pixeez.Objects.UserBase) item = user.UserItem();
                    }
                    page.UpdateDetail(item);
                }
            }
            else if (obj is IllustImageViewerPage)
            {
                var page = obj as IllustImageViewerPage;
                if (page.Contents is PixivItem) page.UpdateDetail(page.Contents);
            }
            else if (obj is SearchResultPage)
            {
                var page = obj as SearchResultPage;
                if (page.Contents is string) page.UpdateDetail(page.Contents);
            }
            else if (obj is HistoryPage)
            {
                var page = obj as HistoryPage;
                page.UpdateDetail();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) RefreshPage.Execute(win.Content);
            }
        });

        public static ICommand RefreshPageThumb { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).UpdateTilesThumb();
            }
            else if (obj is IllustDetailPage)
            {
                var page = obj as IllustDetailPage;
                page.UpdateThumb(true);
            }
            else if (obj is IllustImageViewerPage)
            {
                var overwrite = Keyboard.Modifiers == ModifierKeys.Alt ? true : false;
                var page = obj as IllustImageViewerPage;
                if (page.Contents is PixivItem) page.UpdateDetail(page.Contents, overwrite);
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
            else if (obj is BrowerPage)
            {
                var page = obj as BrowerPage;
                page.UpdateDetail(page.Contents);
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) RefreshPageThumb.Execute(win.Content);
            }
        });

        public static ICommand RefreshCancel { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).StopPrefetching();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).StopPrefetching();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).StopPrefetching();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).StopPrefetching();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).StopPrefetching();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) RefreshCancel.Execute(win.Content);
            }
        });

        public static ICommand AppendTiles { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).AppendTiles();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) AppendTiles.Execute(win.Content);
            }
        });

        public static ICommand ScrollPageFirst { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).ScrollPageFirst();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).ScrollPageFirst();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).ScrollPageFirst();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) ScrollPageFirst.Execute(win.Content);
            }
        });

        public static ICommand ScrollPageLast { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).ScrollPageLast();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).ScrollPageLast();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).ScrollPageLast();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) ScrollPageLast.Execute(win.Content);
            }
        });

        public static ICommand ScrollPageUp { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).ScrollPageUp();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).ScrollPageUp();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).ScrollPageUp();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) ScrollPageUp.Execute(win.Content);
            }
        });

        public static ICommand ScrollPageDown { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).ScrollPageDown();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).ScrollPageDown();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).ScrollPageDown();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) ScrollPageDown.Execute(win.Content);
            }
        });

        public static ICommand PrevIllust { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).PrevIllust();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).PrevIllust();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).PrevIllust();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).PrevIllust();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).PrevIllust();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) PrevIllust.Execute(win.Content);
            }
        });

        public static ICommand NextIllust { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).NextIllust();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).NextIllust();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).NextIllust();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).NextIllust();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).NextIllust();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) NextIllust.Execute(win.Content);
            }
        });

        public static ICommand FirstIllust { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).FirstIllust();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).FirstIllust();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).FirstIllust();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).FirstIllust();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).FirstIllust();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) FirstIllust.Execute(win.Content);
            }
        });

        public static ICommand LastIllust { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).LastIllust();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).LastIllust();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).LastIllust();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).LastIllust();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).LastIllust();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) LastIllust.Execute(win.Content);
            }
        });

        public static ICommand PrevIllustPage { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).PrevIllustPage();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).PrevIllustPage();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).PrevIllustPage();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).PrevIllust();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).PrevIllust();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) PrevIllustPage.Execute(win.Content);
            }
        });

        public static ICommand NextIllustPage { get; } = new DelegateCommand<dynamic>(obj =>
        {
            if (obj is TilesPage)
            {
                (obj as TilesPage).NextIllustPage();
            }
            else if (obj is IllustDetailPage)
            {
                (obj as IllustDetailPage).NextIllustPage();
            }
            else if (obj is IllustImageViewerPage)
            {
                (obj as IllustImageViewerPage).NextIllustPage();
            }
            else if (obj is SearchResultPage)
            {
                (obj as SearchResultPage).NextIllust();
            }
            else if (obj is HistoryPage)
            {
                (obj as HistoryPage).NextIllust();
            }
            else if (obj is Window)
            {
                var win = obj as Window;
                if (win.Content is Page) NextIllustPage.Execute(win.Content);
            }
        });
        #endregion

        #region Like/Unlike Work/User Related
        public static ICommand LikeIllust { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                setting = Application.Current.LoadSetting();
                var pub = setting.PrivateBookmarkPrefer ? false : true;

                if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    var ret = item.IsLiked() ? true : await item.LikeIllust(pub);
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    gallery.GetSelected().LikeIllust(pub);
                }
                else if (obj is IList<PixivItem>)
                {
                    var gallery = obj as IList<PixivItem>;
                    gallery.LikeIllust(pub);
                }
            }
            catch (Exception ex) { ex.ERROR("LikeIllust"); }
        });

        public static ICommand UnLikeIllust { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    var ret = await item.UnLikeIllust();
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    gallery.GetSelected().UnLikeIllust();
                }
                else if (obj is IList<PixivItem>)
                {
                    var gallery = obj as IList<PixivItem>;
                    gallery.UnLikeIllust();
                }
            }
            catch (Exception ex) { ex.ERROR("UnLikeIllust"); }
        });

        public static ICommand ChangeIllustLikeState { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                setting = Application.Current.LoadSetting();
                var pub = setting.PrivateFavPrefer ? false : true;
                var toggle = setting.ToggleFavBookmarkState;

                if (obj is PixivItem)
                {
                    var ret = false;
                    var item = obj as PixivItem;
                    if (toggle)
                    {
                        ret = await item.ToggleLikeIllust(pub);
                    }
                    else
                    {
                        if (Keyboard.Modifiers == ModifierKeys.None)
                            ret = await item.LikeIllust(pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Shift)
                            ret = await item.LikeIllust(!pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Alt)
                            ret = await item.UnLikeIllust();
                    }
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (toggle)
                    {
                        gallery.GetSelected().ToggleLikeIllust(pub);
                    }
                    else
                    {
                        if (Keyboard.Modifiers == ModifierKeys.None)
                            gallery.GetSelected().LikeIllust(pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Shift)
                            gallery.GetSelected().LikeIllust(!pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Alt)
                            gallery.GetSelected().UnLikeIllust();
                    }
                }
                else if (obj is IList<PixivItem>)
                {
                    var gallery = obj as IList<PixivItem>;
                    if (toggle)
                    {
                        gallery.ToggleLikeIllust(pub);
                    }
                    else
                    {
                        if (Keyboard.Modifiers == ModifierKeys.None)
                            gallery.LikeIllust(pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Shift)
                            gallery.LikeIllust(!pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Alt)
                            gallery.UnLikeIllust();
                    }
                }
                else if (obj is TilesPage)
                {
                    ChangeIllustLikeState.Execute((obj as TilesPage).CurrentItem);
                }
                else if (obj is IllustDetailPage)
                {
                    (obj as IllustDetailPage).ChangeIllustLikeState();
                }
                else if (obj is IllustImageViewerPage)
                {
                    (obj as IllustImageViewerPage).ChangeIllustLikeState();
                }
                else if (obj is HistoryPage)
                {
                    (obj as HistoryPage).ChangeIllustLikeState();
                }
                else if (obj is SearchResultPage)
                {
                    (obj as SearchResultPage).ChangeIllustLikeState();
                }
                else if (obj is Window)
                {
                    var win = obj as Window;
                    if (win.Content is Page) ChangeIllustLikeState.Execute(win.Content);
                }
            }
            catch (Exception ex) { ex.ERROR("ChangeIllustLikeState"); }
        });

        public static ICommand LikeUser { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                setting = Application.Current.LoadSetting();
                var pub = setting.PrivateFavPrefer ? false : true;

                if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    await item.LikeUser(pub);
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    gallery.GetSelected().LikeUser(pub);
                }
                else if (obj is IList<PixivItem>)
                {
                    var gallery = obj as IList<PixivItem>;
                    gallery.LikeUser(pub);
                }
            }
            catch (Exception ex) { ex.ERROR("LikeUser"); }
        });

        public static ICommand UnLikeUser { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    await item.UnLikeUser();
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    gallery.GetSelected().UnLikeUser();
                }
                else if (obj is IList<PixivItem>)
                {
                    var gallery = obj as IList<PixivItem>;
                    gallery.UnLikeUser();
                }
            }
            catch (Exception ex) { ex.ERROR("UnLikeUser"); }
        });

        public static ICommand ChangeUserLikeState { get; } = new DelegateCommand<dynamic>(async obj =>
        {
            try
            {
                setting = Application.Current.LoadSetting();
                var pub = setting.PrivateFavPrefer ? false : true;
                var toggle = setting.ToggleFavBookmarkState;

                if (obj is PixivItem)
                {
                    var item = obj as PixivItem;
                    if (toggle)
                    {
                        await item.ToggleLikeUser(pub);
                    }
                    else
                    {
                        if (Keyboard.Modifiers == ModifierKeys.None)
                            await item.LikeUser(pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Shift)
                            await item.LikeUser(!pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Alt)
                            await item.UnLikeUser();
                    }
                }
                else if (obj is ImageListGrid)
                {
                    var gallery = obj as ImageListGrid;
                    if (toggle)
                    {
                        gallery.GetSelected().ToggleLikeUser(pub);
                    }
                    else
                    {
                        if (Keyboard.Modifiers == ModifierKeys.None)
                            gallery.GetSelected().LikeUser(pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Shift)
                            gallery.GetSelected().LikeUser(!pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Alt)
                            gallery.GetSelected().UnLikeUser();
                    }
                }
                else if (obj is IList<PixivItem>)
                {
                    var gallery = obj as IList<PixivItem>;
                    if (toggle)
                    {
                        gallery.ToggleLikeUser(pub);
                    }
                    else
                    {
                        if (Keyboard.Modifiers == ModifierKeys.None)
                            gallery.LikeUser(pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Shift)
                            gallery.LikeUser(!pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Alt)
                            gallery.UnLikeUser();
                    }
                }
                else if (obj is TilesPage)
                {
                    ChangeUserLikeState.Execute((obj as TilesPage).CurrentItem);
                }
                else if (obj is IllustDetailPage)
                {
                    (obj as IllustDetailPage).ChangeUserLikeState();
                }
                else if (obj is IllustImageViewerPage)
                {
                    (obj as IllustImageViewerPage).ChangeUserLikeState();
                }
                else if (obj is HistoryPage)
                {
                    (obj as HistoryPage).ChangeUserLikeState();
                }
                else if (obj is SearchResultPage)
                {
                    (obj as SearchResultPage).ChangeUserLikeState();
                }
                else if (obj is Window)
                {
                    var win = obj as Window;
                    if (win.Content is Page) ChangeUserLikeState.Execute(win.Content);
                }
            }
            catch (Exception ex) { ex.ERROR("ChangeUserLikeState"); }
        });
        #endregion

        #region PixivPedia Related
        private static async void OpenPediaWindow(string contents)
        {
            if (!string.IsNullOrEmpty(contents))
            {
                if (contents.ToLower().Contains("://dic.pixiv.net/a/"))
                    contents = Uri.UnescapeDataString(contents.Substring(contents.IndexOf("/a/") + 3));
                var title = $"PixivPedia: {contents} ...";
                if (await title.ActiveByTitle()) return;

                var page = new BrowerPage () { Name = Application.Current.PediaTitle(), Contents = contents, Title = title };
                var viewer = new ContentWindow(title)
                {
                    Title = title,
                    Width = WIDTH_PEDIA,
                    MinWidth = WIDTH_PEDIA,
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
