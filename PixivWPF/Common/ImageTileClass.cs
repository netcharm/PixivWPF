using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PixivWPF.Common
{
    public enum ImageItemType { User, Works, Work, Pages, Page, Manga }

    public class ImageItem : FrameworkElement, INotifyPropertyChanged
    {
        ~ImageItem()
        {
            if (source is ImageSource) source = null;
        }

        public ImageItemType ItemType { get; set; }

        private ImageSource source = null;
        public ImageSource Source
        {
            get { return source; }
            set
            {
                source = value;
                NotifyPropertyChanged();
            }
        }
        public string Thumb { get; set; }
        public string Subject { get; set; }
        public string Caption { get; set; }
        public int Count { get; set; }
        public int Index { get; set; }
        public string UserID { get; set; }
        public Pixeez.Objects.UserBase User { get; set; }
        public string ID { get; set; }
        public Pixeez.Objects.Work Illust { get; set; }
        public string AccessToken { get; set; }
        public string NextURL { get; set; } = string.Empty;
        public TaskStatus State { get; internal set; } = TaskStatus.Created;

        public Visibility FavMarkVisibility { get; set; } = Visibility.Collapsed;
        [Description("Get or Set Illust IsFavorited State")]
        [Category("Common Properties")]
        [DefaultValue(false)]
        public bool IsFavorited
        {
            get { return (FavMarkVisibility == Visibility.Visible ? true : false); }
            set
            {
                if (value) FavMarkVisibility = Visibility.Visible;
                else       FavMarkVisibility = Visibility.Collapsed;
                NotifyPropertyChanged("FavMarkVisibility");
            }
        }

        public Visibility FollowMarkVisibility { get; set; } = Visibility.Collapsed;
        [Description("Get or Set User IsFollowed State")]
        [Category("Common Properties")]
        [DefaultValue(false)]
        public bool IsFollowed
        {
            get { return (FollowMarkVisibility == Visibility.Visible ? true : false); }
            set
            {
                if (value) FollowMarkVisibility = Visibility.Visible;
                else FollowMarkVisibility = Visibility.Collapsed;
                NotifyPropertyChanged("FollowMarkVisibility");
            }
        }

        [Description("Get or Set Display Illust Favorited State Mark")]
        [Category("Common Properties")]
        [DefaultValue(true)]
        public bool IsDisplayFavMark { get; set; } = true;

        public string BadgeValue { get; set; }
        public Visibility BadgeVisibility { get; set; } = Visibility.Collapsed;
        public bool DisplayBadge
        {
            get
            {
                if (BadgeVisibility == Visibility.Visible) return true;
                else return false;
            }
            set
            {
                if (value) BadgeVisibility = Visibility.Visible;
                else BadgeVisibility = Visibility.Collapsed;
                NotifyPropertyChanged("DisplayBadge");
                NotifyPropertyChanged("BadgeVisibility");
            }
        }

        public Visibility TitleVisibility { get; set; }
        public bool DisplayTitle
        {
            get
            {
                if (TitleVisibility == Visibility.Visible) return true;
                else return false;
            }
            set
            {
                if (value) TitleVisibility = Visibility.Visible;
                else TitleVisibility = Visibility.Collapsed;
                NotifyPropertyChanged("DisplayTitle");
            }
        }

        public Visibility IsDownloadedVisibilityAlt { get; set; } = Visibility.Collapsed;
        public Visibility IsDownloadedVisibility { get; set; } = Visibility.Collapsed;
        [Description("Get or Set Illust IsDownloaded State Mark")]
        [Category("Common Properties")]
        [DefaultValue(false)]
        public bool IsDownloaded
        {
            get { return (IsDownloadedVisibility == Visibility.Visible ? true : false); }
            set
            {
                if (value) IsDownloadedVisibility = Visibility.Visible;
                else IsDownloadedVisibility = Visibility.Collapsed;
                NotifyPropertyChanged("IsDownloadedVisibility");

                if (DisplayTitle) IsDownloadedVisibilityAlt = Visibility.Collapsed;
                else IsDownloadedVisibilityAlt = IsDownloadedVisibility;
                NotifyPropertyChanged("IsDownloadedVisibilityAlt");

                NotifyPropertyChanged("IsDownloaded");
            }
        }

        public string Sanity { get; set; } = "all";
        public bool IsR18 { get { return (Sanity.Equals("18+")); } }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public static class ImageTileHelper
    {
        private static void ImageTile_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //throw new NotImplementedException();
        }

        #region Tile Update Helper
        public static void UpdateTiles(this ObservableCollection<ImageItem> collection, ImageItem item = null)
        {
            if (collection is ObservableCollection<ImageItem>)
            {
                if (item is ImageItem)
                {
                    int idx = collection.IndexOf(item);
                    if (idx >= 0 && idx < collection.Count())
                    {
                        collection.Remove(item);
                        collection.Insert(idx, item);
                    }
                }
                else
                {
                    CollectionViewSource.GetDefaultView(collection).Refresh();
                }
            }
        }

        public static void UpdateTiles(this ObservableCollection<ImageItem> collection, IEnumerable<ImageItem> items)
        {
            if (collection is ObservableCollection<ImageItem>)
            {
                if (items is IEnumerable<ImageItem>)
                {
                    var count = collection.Count();
                    foreach (ImageItem sub in items)
                    {
                        int idx = collection.IndexOf(sub);
                        if (idx >= 0 && idx < count)
                        {
                            collection.Remove(sub);
                            collection.Insert(idx, sub);
                        }
                        Application.Current.DoEvents();
                    }
                }
                else
                {
                    CollectionViewSource.GetDefaultView(collection).Refresh();
                }
            }
        }

        public static bool UpdateTilesState(this ImageListGrid gallery, bool fuzzy = true)
        {
            bool result = false;
            if (gallery.SelectedItems.Count <= 0 || gallery.SelectedIndex < 0) return (result);
            try
            {
                foreach (var item in gallery.SelectedItems)
                {
                    if (item.Illust == null) continue;
                    bool download = fuzzy ? item.Illust.IsPartDownloadedAsync() : item.Illust.GetOriginalUrl(item.Index).IsDownloadedAsync();
                    if (item.IsDownloaded != download)
                    {
                        item.IsDownloaded = download;
                        result |= download;
                    }
                    item.IsFavorited = item.IsLiked() && item.IsDisplayFavMark;
                    Application.Current.DoEvents();
                }
            }
#if DEBUG
            catch (Exception e)
            {
                e.Message.ShowMessageBox("ERROR");
            }
#else
            catch (Exception) { }
#endif
            return (result);
        }

        public static void UpdateTilesImageTask(this IEnumerable<ImageItem> items, CancellationToken cancelToken = default(CancellationToken), int parallel = 5)
        {
            var needUpdate = items.Where(item => item.Source == null);
            if (Application.Current != null && needUpdate.Count() > 0)
            {
                try
                {
                    if (parallel <= 0) parallel = 1;
                    else if (parallel >= needUpdate.Count()) parallel = needUpdate.Count();

                    var opt = new ParallelOptions();
                    //opt.TaskScheduler = TaskScheduler.Current;
                    opt.MaxDegreeOfParallelism = parallel;
                    opt.CancellationToken = cancelToken;

                    using (cancelToken.Register(Thread.CurrentThread.Abort))
                    {
                        var ret = Parallel.ForEach(needUpdate, opt, async (item, loopstate, elementIndex) =>
                        {
                            await new Action(async () =>
                            {
                                if (cancelToken.IsCancellationRequested)
                                    opt.CancellationToken.ThrowIfCancellationRequested();
                                else
                                {
                                    try
                                    {
                                        if (item.Source == null)
                                        {
                                            Random rnd = new Random();
                                            await Task.Delay(rnd.Next(20, 200));
                                            Application.Current.DoEvents();

                                            if (item.Count <= 1) item.BadgeValue = string.Empty;
                                            item.Source = await item.Thumb.LoadImageFromUrl();
                                            if(item.Source is ImageSource)
                                                item.State = TaskStatus.RanToCompletion;
                                            else
                                                item.State = TaskStatus.Faulted;
                                            Application.Current.DoEvents();
                                        }
                                    }
#if DEBUG
                                    catch (Exception ex)
                                    {
                                        $"Download Thumbnail Failed:\n{ex.Message}".ShowMessageBox("ERROR");
                                        item.State = TaskStatus.Faulted;
                                    }
#else
                                    catch(Exception){ }
#endif
                                    finally
                                    {
                                        if (item.Thumb.IsCached()) item.Source = await item.Thumb.GetImageCachePath().LoadImageFromFile();
                                    }
                                }
                            }).InvokeAsync();
                        });
                    }
                }
                catch (Exception ex)
                {
                    var ert = ex.Message;
                }
                finally
                {
                    Application.Current.DoEvents();
                }
            }
        }

        public static async Task<Task> UpdateTilesImage(this IEnumerable<ImageItem> items, Task task, CancellationTokenSource cancelSource = default(CancellationTokenSource), int parallel = 5)
        {
            Task result = null;
            try
            {
                var idle = !(task is Task) || task.IsCompleted || task.IsCanceled || task.IsFaulted;
                if (!idle)
                {
                    cancelSource.Cancel();
                    if (task is Task && task.Wait(5000, cancelSource.Token))
                    {
                        Application.Current.DoEvents();
                    }
                }

                if (Application.Current is Application)
                {
                    await new Action(() =>
                    {
                        cancelSource = new CancellationTokenSource();
                        result = new Task(() =>
                        {
                            items.UpdateTilesImageTask(cancelSource.Token, parallel);
                        }, cancelSource.Token, TaskCreationOptions.PreferFairness);                        
                        result.Start();
                    }).InvokeAsync();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
            }
            return (result);
        }
        #endregion

        public static ImageItem IllustItem(this Pixeez.Objects.Work illust, string url = "", string nexturl = "")
        {
            ImageItem result = null;
            try
            {
                if (illust is Pixeez.Objects.Work)
                {
                    url = string.IsNullOrEmpty(url) ? illust.GetThumbnailUrl() : url;

                    if (!string.IsNullOrEmpty(url))
                    {
                        var tooltip = string.IsNullOrEmpty(illust.Caption) ? string.Empty : "\r\n"+string.Join("", illust.Caption.TrimEnd().HtmlToText().HtmlDecode(false).InsertLineBreak(64).Take(512));
                        var tags = illust.Tags.Count>0 ? $"\r\n🔖[#{string.Join(", #", illust.Tags.Take(5))} ...]" : string.Empty;
                        var sanity = string.Empty;
                        var age = string.Empty;
                        var userliked = illust.User.IsLiked() ? $"✔/" : string.Empty;
                        var state = string.Empty;
                        if (illust is Pixeez.Objects.IllustWork)
                        {
                            var work = illust as Pixeez.Objects.IllustWork;
                            var like = work.Stats != null ? $", 👍[{work.Stats.ScoredCount}]" : string.Empty;
                            sanity = string.IsNullOrEmpty(work.SanityLevel) ? string.Empty : work.SanityLevel.SanityAge();
                            age = string.IsNullOrEmpty(sanity) ? string.Empty : $"R[{sanity}]";
                            state = $", 🔞{age}, {userliked}♥[{work.total_bookmarks}]{like}, 🖼[{work.Width}x{work.Height}]";
                        }
                        else if (illust is Pixeez.Objects.NormalWork)
                        {
                            var work = illust as Pixeez.Objects.NormalWork;
                            var like = work.Stats != null ? $", 👍[{work.Stats.ScoredCount}]" : string.Empty;
                            var stats = work.Stats != null ? $"♥[{work.Stats.FavoritedCount.Public}/{work.Stats.FavoritedCount.Private}]" : string.Empty;
                            sanity = work.AgeLimit != null ? work.AgeLimit.SanityAge() : string.Empty;
                            age = string.IsNullOrEmpty(sanity) ? string.Empty : $"R[{sanity}]";
                            state = $", 🔞{age}, {userliked}{stats}{like}, 🖼[{work.Width}x{work.Height}]";
                        }
                        var uname = illust.User is Pixeez.Objects.UserBase ? $"\r\n🎨[{illust.User.Name}]" : string.Empty;
                        tooltip = string.IsNullOrEmpty(illust.Title) ? $"{uname}{state}{tags}{tooltip}" : $"{illust.Title}{uname}{state}{tags}{tooltip}";
                        //var title = string.Join(" ", Regex.Split(illust.Title, @"(?:\r\n|\n|\r)"));
                        var title = Regex.Replace(illust.Title, @"[\n\r]", "", RegexOptions.IgnoreCase);
                        result = new ImageItem()
                        {
                            ItemType = ImageItemType.Work,
                            NextURL = nexturl,
                            Thumb = url,
                            Index = -1,
                            Count = (int)(illust.PageCount),
                            BadgeValue = illust.PageCount.Value.ToString(),
                            BadgeVisibility = illust.PageCount > 1 ? Visibility.Visible : Visibility.Collapsed,
                            IsDisplayFavMark = true,
                            IsFavorited = illust.IsLiked(),
                            IsFollowed = illust.User.IsLiked(),
                            Sanity = sanity,
                            DisplayBadge = illust.PageCount > 1 ? true : false,
                            Illust = illust,
                            ID = illust.Id.ToString(),
                            User = illust.User,
                            UserID = illust.User.Id.ToString(),
                            Subject = title,
                            DisplayTitle = true,
                            Caption = illust.Caption,
                            ToolTip = $"📅[{illust.GetDateTime()}]{tooltip}",
                            IsDownloaded = illust == null ? false : illust.IsPartDownloadedAsync(),
                            Tag = illust
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageDialog("ERROR");
            }
            return (result);
        }

        #region Image Tile Add Helper
        public static async void AddTo(this IList<Pixeez.Objects.Work> works, IList<ImageItem> Collection, string nexturl = "")
        {
            foreach (var illust in works)
            {
                illust.AddTo(Collection, nexturl);
                await Task.Delay(1);
                Application.Current.DoEvents();
            }
        }

        public static async void AddTo(this Pixeez.Objects.Work illust, IList<ImageItem> Collection, string nexturl = "")
        {
            try
            {
                if (illust is Pixeez.Objects.Work && Collection is IList<ImageItem>)
                {
                    var url = illust.GetThumbnailUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var i = illust.IllustItem(url, nexturl);
                        if (i is ImageItem)
                        {
                            i.ToolTip = $"№[{Collection.Count + 1}], {i.ToolTip}";
                            Collection.Add(i);
                            await Task.Delay(1);
                            i.DoEvents();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageDialog("ERROR");
            }
        }

        public static async void AddTo(this Pixeez.Objects.MetaPages pages, IList<ImageItem> Collection, Pixeez.Objects.Work illust, int index, string nexturl = "")
        {
            try
            {
                if (pages is Pixeez.Objects.MetaPages && Collection is IList<ImageItem>)
                {
                    var url = pages.GetThumbnailUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var i = illust.IllustItem(url, nexturl);
                        if (i is ImageItem)
                        {
                            //i.Thumb = url;
                            i.DisplayTitle = false;
                            i.Index = index;
                            i.Count = illust.PageCount ?? 0;
                            i.IsFavorited = false;
                            i.IsFollowed = false;
                            i.IsDisplayFavMark = false;
                            i.BadgeValue = (index + 1).ToString();
                            i.Subject = $"{i.Subject} - {index + 1}/{illust.PageCount}";
                            i.IsDownloaded = illust == null ? false : pages.GetOriginalUrl().IsDownloadedAsync(false);
                            i.Tag = pages;
                            Collection.Add(i);
                            await Task.Delay(1);
                            i.DoEvents();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
        }

        public static async void AddTo(this Pixeez.Objects.Page page, IList<ImageItem> Collection, Pixeez.Objects.Work illust, int index, string nexturl = "")
        {
            try
            {
                if (page is Pixeez.Objects.Page && Collection is IList<ImageItem>)
                {
                    var url = page.GetThumbnailUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var i = illust.IllustItem(url, nexturl);
                        if (i is ImageItem)
                        {
                            //i.Thumb = url;
                            i.DisplayTitle = false;
                            i.Index = index;
                            i.Count = illust.PageCount ?? 0;
                            i.IsFavorited = false;
                            i.IsFollowed = false;
                            i.IsDisplayFavMark = false;
                            i.BadgeValue = (index + 1).ToString();
                            i.Subject = $"{i.Subject} - {index + 1}/{illust.PageCount}";
                            i.IsDownloaded = illust == null ? false : page.GetOriginalUrl().IsDownloadedAsync(false);
                            i.Tag = page;
                            Collection.Add(i);
                            await Task.Delay(1);
                            i.DoEvents();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
        }

        public static async void AddTo(this Pixeez.Objects.User user, IList<ImageItem> Collection, string nexturl = "")
        {
            try
            {
                if (user is Pixeez.Objects.User && Collection is IList<ImageItem>)
                {
                    var contact = user.Profile is Pixeez.Objects.Profile ? user.Profile.Contacts : null;
                    var twitter = contact is Pixeez.Objects.Contacts ? $"📧{contact.Twitter}" : string.Empty;
                    var web = user.Profile is Pixeez.Objects.Profile ? $"🌐{user.Profile.Homepage}" : string.Empty;
                    var mail = string.IsNullOrEmpty(user.Email) ? string.Empty : $"🖃{user.Email}";

                    var info = new List<string>() { twitter, web, mail };
                    var tooltip = string.Join("\r\n", info).Trim();
                    if (string.IsNullOrEmpty(tooltip))
                    {
                        var cu = user.Id.FindUser();
                        if(cu is Pixeez.Objects.User)
                        {
                            var u = cu as Pixeez.Objects.User;
                            contact = u.Profile is Pixeez.Objects.Profile ? u.Profile.Contacts : null;
                            twitter = contact is Pixeez.Objects.Contacts ? $"📧{contact.Twitter}" : string.Empty;
                            web = u.Profile is Pixeez.Objects.Profile ? $"🌐{u.Profile.Homepage}" : string.Empty;
                            mail = string.IsNullOrEmpty(u.Email) ? string.Empty : $"🖃{u.Email}";
                            info = new List<string>() { twitter, web, mail };
                            tooltip = string.Join("\r\n", info).Trim();
                        }                           
                    }

                    if (string.IsNullOrEmpty(tooltip)) tooltip = null;

                    var url = user.GetAvatarUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var i = new ImageItem()
                        {
                            ItemType = ImageItemType.User,
                            Thumb = url,
                            NextURL = nexturl,
                            BadgeValue = user.Stats == null ? null : user.Stats.Works.Value.ToString(),
                            IsFavorited = false,
                            IsFollowed = user.IsLiked(),
                            Illust = null,
                            ID = user.Id.ToString(),
                            User = user,
                            UserID = user.Id.ToString(),
                            Subject = user.Profile == null ? $"{user.Name}" : $"{user.Name} - {user.Profile.Contacts.Twitter}",
                            DisplayTitle = true,
                            ToolTip = tooltip,
                            Tag = user
                        };
                        Collection.Add(i);
                        await Task.Delay(1);
                        i.DoEvents();
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
        }
        #endregion

        #region MetaPage Helper
        public static string GetThumbnailUrl(this Pixeez.Objects.Page page, bool large = false)
        {
            var url = string.Empty;
            if (page is Pixeez.Objects.Page)
            {
                var images = page.ImageUrls;

                if (large && !string.IsNullOrEmpty(images.Px480mw))
                    url = images.Px480mw;
                else if (!string.IsNullOrEmpty(images.SquareMedium))
                    url = images.SquareMedium;
                else if (!string.IsNullOrEmpty(images.Px128x128))
                    url = images.Px128x128;
                else if (!string.IsNullOrEmpty(images.Small))
                    url = images.Small;
                else if (!string.IsNullOrEmpty(images.Px480mw))
                    url = images.Px480mw;
                else if (!string.IsNullOrEmpty(images.Medium))
                    url = images.Medium;
                else if (!string.IsNullOrEmpty(images.Large))
                    url = images.Large;
                else if (!string.IsNullOrEmpty(images.Original))
                    url = images.Original;
            }
            return (url);
        }

        public static string GetPreviewUrl(this Pixeez.Objects.Page page, bool large = false)
        {
            var url = string.Empty;
            if (page is Pixeez.Objects.Page)
            {
                var images = page.ImageUrls;

                if (large && !string.IsNullOrEmpty(images.Large))
                    url = images.Large;
                else if (!string.IsNullOrEmpty(images.Medium))
                    url = images.Medium;
                else if (!string.IsNullOrEmpty(images.Px480mw))
                    url = images.Px480mw;
                else if (!string.IsNullOrEmpty(images.Large))
                    url = images.Large;
                else if (!string.IsNullOrEmpty(images.Original))
                    url = images.Original;
                else if (!string.IsNullOrEmpty(images.SquareMedium))
                    url = images.SquareMedium;
                else if (!string.IsNullOrEmpty(images.Px128x128))
                    url = images.Px128x128;
                else if (!string.IsNullOrEmpty(images.Small))
                    url = images.Small;
            }
            return (url);
        }

        public static string GetOriginalUrl(this Pixeez.Objects.Page page)
        {
            var url = string.Empty;
            if (page is Pixeez.Objects.Page)
            {
                var images = page.ImageUrls;

                if (!string.IsNullOrEmpty(images.Original))
                    url = images.Original;
                else if (!string.IsNullOrEmpty(images.Large))
                    url = images.Large;
                else if (!string.IsNullOrEmpty(images.Medium))
                    url = images.Medium;
                else if (!string.IsNullOrEmpty(images.Px480mw))
                    url = images.Px480mw;
                else if (!string.IsNullOrEmpty(images.SquareMedium))
                    url = images.SquareMedium;
                else if (!string.IsNullOrEmpty(images.Px128x128))
                    url = images.Px128x128;
                else if (!string.IsNullOrEmpty(images.Small))
                    url = images.Small;
            }
            return (url);
        }
        #endregion

        #region MetaPage Helper
        public static string GetThumbnailUrl(this Pixeez.Objects.MetaPages pages, bool large = false)
        {
            var url = string.Empty;
            if (pages is Pixeez.Objects.MetaPages)
            {
                var images = pages.ImageUrls;
                if (large && !string.IsNullOrEmpty(images.Px480mw))
                    url = images.Px480mw;
                else if (!string.IsNullOrEmpty(images.SquareMedium))
                    url = images.SquareMedium;
                else if (!string.IsNullOrEmpty(images.Px128x128))
                    url = images.Px128x128;
                else if (!string.IsNullOrEmpty(images.Small))
                    url = images.Small;
                else if (!string.IsNullOrEmpty(images.Px480mw))
                    url = images.Px480mw;
                else if (!string.IsNullOrEmpty(images.Medium))
                    url = images.Medium;
                else if (!string.IsNullOrEmpty(images.Large))
                    url = images.Large;
                else if (!string.IsNullOrEmpty(images.Original))
                    url = images.Original;
            }
            return (url);
        }

        public static string GetPreviewUrl(this Pixeez.Objects.MetaPages pages, bool large = false)
        {
            var url = string.Empty;
            if (pages is Pixeez.Objects.MetaPages)
            {
                var images = pages.ImageUrls;

                if (large && !string.IsNullOrEmpty(images.Large))
                    url = images.Large;
                else if (!string.IsNullOrEmpty(images.Medium))
                    url = images.Medium;
                else if (!string.IsNullOrEmpty(images.Px480mw))
                    url = images.Px480mw;
                else if (!string.IsNullOrEmpty(images.Large))
                    url = images.Large;
                else if (!string.IsNullOrEmpty(images.Original))
                    url = images.Medium;
                else if (!string.IsNullOrEmpty(images.SquareMedium))
                    url = images.SquareMedium;
                else if (!string.IsNullOrEmpty(images.Px128x128))
                    url = images.Px128x128;
                else if (!string.IsNullOrEmpty(images.Small))
                    url = images.Small;
            }
            return (url);
        }

        public static string GetOriginalUrl(this Pixeez.Objects.MetaPages pages)
        {
            var url = string.Empty;
            if (pages is Pixeez.Objects.MetaPages)
            {
                var images = pages.ImageUrls;
                url = images.Original;
                if (string.IsNullOrEmpty(url))
                {
                    if (!string.IsNullOrEmpty(images.Large))
                        url = images.Medium;
                    else if (!string.IsNullOrEmpty(images.Medium))
                        url = images.Medium;
                    else if (!string.IsNullOrEmpty(images.Px480mw))
                        url = images.Px480mw;
                    else if (!string.IsNullOrEmpty(images.SquareMedium))
                        url = images.SquareMedium;
                    else if (!string.IsNullOrEmpty(images.Px128x128))
                        url = images.Px128x128;
                    else if (!string.IsNullOrEmpty(images.Small))
                        url = images.Small;
                }
            }
            return (url);
        }
        #endregion

        #region IllusWork Helper
        public static string GetThumbnailUrl(this Pixeez.Objects.IllustWork Illust, int idx)
        {
            var url = string.Empty;
            if (Illust is Pixeez.Objects.IllustWork)
            {
                var illust = Illust as Pixeez.Objects.IllustWork;
                if (illust.PageCount == 1 && illust.meta_single_page != null)
                {
                    url = string.Empty;
                }
                else if (illust.PageCount.Value > 1 && illust.meta_pages.Count() == illust.PageCount.Value)
                {
                    if (idx < 0) idx = 0;
                    if (idx > illust.PageCount) idx = illust.PageCount.Value - 1;
                    var pages = illust.meta_pages[idx];
                    url = pages.GetThumbnailUrl();
                }
            }
            return (url);
        }

        public static string GetPreviewUrl(this Pixeez.Objects.IllustWork Illust, int idx, bool large = false)
        {
            var url = string.Empty;
            if (Illust is Pixeez.Objects.IllustWork)
            {
                var illust = Illust as Pixeez.Objects.IllustWork;
                if (illust.PageCount == 1 && illust.meta_single_page != null)
                {
                    url = string.Empty;
                }
                else if (illust.PageCount.Value > 1 && illust.meta_pages.Count() == illust.PageCount.Value)
                {
                    if (idx < 0) idx = 0;
                    if (idx > illust.PageCount) idx = illust.PageCount.Value - 1;
                    var pages = illust.meta_pages[idx];
                    url = pages.GetPreviewUrl(large);
                }
            }
            return (url);
        }

        public static string GetOriginalUrl(this Pixeez.Objects.IllustWork Illust, int idx)
        {
            var url = string.Empty;
            if (Illust is Pixeez.Objects.IllustWork)
            {
                var illust = Illust as Pixeez.Objects.IllustWork;
                if (illust.PageCount == 1 && illust.meta_single_page != null)
                {
                    url = illust.meta_single_page.OriginalImageUrl;
                }
                else if (illust.PageCount.Value > 1 && illust.meta_pages.Count() == illust.PageCount.Value)
                {
                    if (idx < 0) idx = 0;
                    if (idx > illust.PageCount) idx = illust.PageCount.Value - 1;
                    var pages = illust.meta_pages[idx];
                    url = pages.GetOriginalUrl();
                }
            }
            return (url);
        }
        #endregion

        #region NormalWork Helper
        public static string GetThumbnailUrl(this Pixeez.Objects.NormalWork Illust, int idx, bool large = false)
        {
            var url = string.Empty;
            if (Illust is Pixeez.Objects.NormalWork)
            {
                var illust = Illust as Pixeez.Objects.NormalWork;
                if (illust.PageCount == 1)
                {
                    url = string.Empty;
                }
                else if (illust.PageCount.Value > 1 && illust.Metadata != null && illust.Metadata.Pages != null && illust.Metadata.Pages.Count() == illust.PageCount.Value)
                {
                    if (idx < 0) idx = 0;
                    if (idx > illust.PageCount) idx = illust.PageCount.Value - 1;
                    var pages = illust.Metadata.Pages[idx];
                    url = pages.GetThumbnailUrl(large);
                }
            }
            return (url);
        }

        public static string GetPreviewUrl(this Pixeez.Objects.NormalWork Illust, int idx, bool large = false)
        {
            var url = string.Empty;
            if (Illust is Pixeez.Objects.NormalWork)
            {
                var illust = Illust as Pixeez.Objects.NormalWork;
                if (illust.PageCount == 1)
                {
                    url = string.Empty;
                }
                else if (illust.PageCount.Value > 1 && illust.Metadata != null && illust.Metadata.Pages != null && illust.Metadata.Pages.Count() == illust.PageCount.Value)
                {
                    if (idx < 0) idx = 0;
                    if (idx > illust.PageCount) idx = illust.PageCount.Value - 1;
                    var page = illust.Metadata.Pages[idx];
                    url = page.GetPreviewUrl(large);
                }
            }
            return (url);
        }

        public static string GetOriginalUrl(this Pixeez.Objects.NormalWork Illust, int idx)
        {
            var url = string.Empty;
            if (Illust is Pixeez.Objects.NormalWork)
            {
                var illust = Illust as Pixeez.Objects.NormalWork;
                if (illust.PageCount == 1)
                {
                    url = string.Empty;
                }
                else if (illust.PageCount.Value > 1 && illust.Metadata != null && illust.Metadata.Pages != null && illust.Metadata.Pages.Count() == illust.PageCount.Value)
                {
                    if (idx < 0) idx = 0;
                    if (idx > illust.PageCount) idx = illust.PageCount.Value - 1;
                    var pages = illust.Metadata.Pages[idx];
                    url = pages.GetOriginalUrl();
                }
            }
            return (url);
        }
        #endregion

        #region Work Helper
        public static string GetThumbnailUrl(this Pixeez.Objects.Work Illust, int index = -1, bool large = false)
        {
            var url = string.Empty;

            if (Illust is Pixeez.Objects.IllustWork)
            {
                var illust = Illust as Pixeez.Objects.IllustWork;
                url = illust.GetThumbnailUrl(index);
            }
            else if (Illust is Pixeez.Objects.NormalWork)
            {
                var illust = Illust as Pixeez.Objects.NormalWork;
                url = illust.GetThumbnailUrl(index);
            }

            if (string.IsNullOrEmpty(url))
            {
                if (large && !string.IsNullOrEmpty(Illust.ImageUrls.Px480mw))
                    url = Illust.ImageUrls.Px480mw;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.SquareMedium))
                    url = Illust.ImageUrls.SquareMedium;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Px128x128))
                    url = Illust.ImageUrls.Px128x128;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Small))
                    url = Illust.ImageUrls.Small;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Px480mw))
                    url = Illust.ImageUrls.Px480mw;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Medium))
                    url = Illust.ImageUrls.Medium;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Large))
                    url = Illust.ImageUrls.Large;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Original))
                    url = Illust.ImageUrls.Original;
            }
            return (url);
        }

        public static string GetPreviewUrl(this Pixeez.Objects.Work Illust, int index = -1, bool large = false)
        {
            var url = string.Empty;

            if (Illust is Pixeez.Objects.IllustWork)
            {
                var illust = Illust as Pixeez.Objects.IllustWork;
                url = illust.GetPreviewUrl(index, large);
            }
            else if (Illust is Pixeez.Objects.NormalWork)
            {
                var illust = Illust as Pixeez.Objects.NormalWork;
                url = illust.GetPreviewUrl(index, large);
            }

            if (string.IsNullOrEmpty(url))
            {
                if (large && !string.IsNullOrEmpty(Illust.ImageUrls.Large))
                    url = Illust.ImageUrls.Large;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Medium))
                    url = Illust.ImageUrls.Medium;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Px480mw))
                    url = Illust.ImageUrls.Px480mw;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Large))
                    url = Illust.ImageUrls.Large;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Original))
                    url = Illust.ImageUrls.Original;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.SquareMedium))
                    url = Illust.ImageUrls.SquareMedium;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Px128x128))
                    url = Illust.ImageUrls.Px128x128;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Small))
                    url = Illust.ImageUrls.Small;
            }
            return (url);
        }

        public static string GetOriginalUrl(this Pixeez.Objects.Work Illust, int index = -1)
        {
            var url = Illust.ImageUrls.Original;

            if (Illust is Pixeez.Objects.IllustWork)
            {
                var illust = Illust as Pixeez.Objects.IllustWork;
                url = illust.GetOriginalUrl(index);
            }
            else if (Illust is Pixeez.Objects.NormalWork)
            {
                var illust = Illust as Pixeez.Objects.NormalWork;
                url = illust.GetOriginalUrl(index);
            }

            if (string.IsNullOrEmpty(url))
            {
                if (!string.IsNullOrEmpty(Illust.ImageUrls.Original))
                    url = Illust.ImageUrls.Original;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Large))
                    url = Illust.ImageUrls.Large;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Medium))
                    url = Illust.ImageUrls.Medium;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Px480mw))
                    url = Illust.ImageUrls.Px480mw;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.SquareMedium))
                    url = Illust.ImageUrls.SquareMedium;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Px128x128))
                    url = Illust.ImageUrls.Px128x128;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Small))
                    url = Illust.ImageUrls.Small;
            }
            return (url);
        }
        #endregion

        #region User Image Help
        public static string GetThumbnailUrl(this Pixeez.Objects.NewUser user, bool large = false)
        {
            var url = user.profile_image_urls.Px128x128;
            if (string.IsNullOrEmpty(url))
            {
                if (large && !string.IsNullOrEmpty(user.profile_image_urls.Px480mw))
                    url = user.profile_image_urls.Px480mw;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.SquareMedium))
                    url = user.profile_image_urls.SquareMedium;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Px128x128))
                    url = user.profile_image_urls.Px128x128;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Small))
                    url = user.profile_image_urls.Small;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Px480mw))
                    url = user.profile_image_urls.Px480mw;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Medium))
                    url = user.profile_image_urls.Medium;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Large))
                    url = user.profile_image_urls.Large;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Original))
                    url = user.profile_image_urls.Original;
            }
            return (url);
        }

        public static string GetPreviewUrl(this Pixeez.Objects.NewUser user, bool large = false)
        {
            var url = user.profile_image_urls.Large;
            if (string.IsNullOrEmpty(url))
            {
                if (large && !string.IsNullOrEmpty(user.profile_image_urls.Large))
                    url = user.profile_image_urls.Large;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Medium))
                    url = user.profile_image_urls.Medium;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Original))
                    url = user.profile_image_urls.Original;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Px480mw))
                    url = user.profile_image_urls.Px480mw;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.SquareMedium))
                    url = user.profile_image_urls.SquareMedium;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Px128x128))
                    url = user.profile_image_urls.Px128x128;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Small))
                    url = user.profile_image_urls.Small;
            }
            return (url);
        }
        #endregion
    }


}
