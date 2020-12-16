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
using System.Windows.Input;
using System.Windows.Media;

namespace PixivWPF.Common
{
    public enum ImageItemType { None, User, Works, Work, Pages, Page, Manga, Novel }

    public class FilterParam
    {
        public string Type { get; set; } = string.Empty;
        public string Fast { get; set; } = string.Empty;
        public string FavoitedRange { get; set; } = string.Empty;
        public string Favorited { get; set; } = string.Empty;
        public string Followed { get; set; } = string.Empty;
        public string Downloaded { get; set; } = string.Empty;
        public string Sanity { get; set; } = string.Empty;
        public bool SanityOption_IncludeUnder { get; set; } = true;
    }

    public class PixivItem : FrameworkElement, INotifyPropertyChanged
    {
        ~PixivItem()
        {
            if (source is ImageSource) source = null;
        }

        public ImageItemType ItemType { get; set; } = ImageItemType.None;

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
        private TaskStatus state = TaskStatus.Created;
        public TaskStatus State
        {
            get { return (state); }
            internal set { state = value; NotifyPropertyChanged("State"); }
        }

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

        public Visibility UserMarkVisibility { get { return (this.IsUser() ? Visibility.Visible : Visibility.Collapsed); } } 

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

        public Visibility IsPartDownloadedVisibilityAlt { get; set; } = Visibility.Collapsed;
        public Visibility IsPartDownloadedVisibility { get; set; } = Visibility.Collapsed;
        [Description("Get or Set Illust IsPartDownloaded State Mark")]
        [Category("Common Properties")]
        [DefaultValue(false)]
        public bool IsPartDownloaded
        {
            get { return (IsPartDownloadedVisibility == Visibility.Visible ? true : false); }
            set
            {
                if (value) IsPartDownloadedVisibility = Visibility.Visible;
                else IsPartDownloadedVisibility = Visibility.Collapsed;
                NotifyPropertyChanged("IsDownloadedVisibility");

                if (DisplayTitle) IsPartDownloadedVisibilityAlt = Visibility.Collapsed;
                else IsPartDownloadedVisibilityAlt = IsPartDownloadedVisibility;
                NotifyPropertyChanged("IsDownloadedVisibilityAlt");

                NotifyPropertyChanged("IsDownloaded");
            }
        }
        public Visibility IsDownloadedVisibilityAlt { get; set; } = Visibility.Collapsed;
        public Visibility IsDownloadedVisibility { get; set; } = Visibility.Collapsed;
        [Description("Get or Set Illust IsDownloaded State Mark")]
        [Category("Common Properties")]
        [DefaultValue(false)]
        public bool IsDownloaded
        {
            get
            {
                if(UsePartDownloaded)
                    return (IsPartDownloadedVisibility == Visibility.Visible ? true : false);
                else
                    return (IsDownloadedVisibility == Visibility.Visible ? true : false);
            }
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
        public string DownloadedTooltip { get; set; } = string.Empty;
        public string DownloadedFilePath { get; set; } = string.Empty;

        public bool UsePartDownloaded { get; set; } = false;

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

        #region Tiles Filter Helper
        public static Predicate<object> GetFilter(this string filter)
        {
            Predicate<object> result_filter = null;
            Func<object, bool> filter_action = null;

            if (!string.IsNullOrEmpty(filter))
            {
                filter = filter.ToLower();
                #region item type
                if (filter.Equals("user"))
                {
                    filter_action = new Func<object, bool>(obj =>
                    {
                        var result = true;
                        if (obj is PixivItem)
                        {
                            result = (obj as PixivItem).IsUser() ? true : false;
                        }
                        return (result);
                    });
                }
                else if (filter.Equals("work"))
                {
                    filter_action = new Func<object, bool>(obj =>
                    {
                        var result = true;
                        if (obj is PixivItem)
                        {
                            result = (obj as PixivItem).IsWork() ? true : false;
                        }
                        return (result);
                    });
                }
                #endregion
                #region item states
                else if (filter.Equals("favorited"))
                {
                    filter_action = new Func<object, bool>(obj =>
                    {
                        var result = true;
                        if (obj is PixivItem)
                        {
                            result = (obj as PixivItem).IsFavorited ? true : false;
                        }
                        return (result);
                    });
                }
                else if (filter.StartsWith("favorited_"))
                {
                    int range = 0;
                    if (int.TryParse(filter.Substring(10), out range))
                    {
                        filter_action = new Func<object, bool>(obj =>
                        {
                            var result = true;
                            if (obj is PixivItem)
                            {
                                var item = (obj as PixivItem);
                                if (item.IsWork()) {
                                    var illust = item.Illust;
                                    if(illust is Pixeez.Objects.IllustWork)
                                    {
                                        var fav_count = (illust as Pixeez.Objects.IllustWork).total_bookmarks;
                                        result = fav_count > range ? true : false;
                                    }
                                    else if (illust is Pixeez.Objects.NormalWork && illust.Stats is Pixeez.Objects.WorkStats)
                                    {
                                        var fav_count = illust.Stats.FavoritedCount.Public + illust.Stats.FavoritedCount.Private;
                                        result = fav_count > range ? true : false;
                                    }
                                }
                            }
                            return (result);
                        });
                    }
                }
                else if (filter.Equals("notfavorited"))
                {
                    filter_action = new Func<object, bool>(obj =>
                    {
                        var result = true;
                        if (obj is PixivItem)
                        {
                            result = (obj as PixivItem).IsFavorited ? false : true;
                        }
                        return (result);
                    });
                }
                else if (filter.Equals("followed"))
                {
                    filter_action = new Func<object, bool>(obj =>
                    {
                        var result = true;
                        if (obj is PixivItem)
                        {
                            result = (obj as PixivItem).IsFollowed ? true : false;
                        }
                        return (result);
                    });
                }
                else if (filter.Equals("notfollowed"))
                {
                    filter_action = new Func<object, bool>(obj =>
                    {
                        var result = true;
                        if (obj is PixivItem)
                        {
                            result = (obj as PixivItem).IsFollowed ? false : true;
                        }
                        return (result);
                    });
                }
                else if (filter.Equals("downloaded"))
                {
                    filter_action = new Func<object, bool>(obj =>
                    {
                        var result = true;
                        if (obj is PixivItem)
                        {
                            result = (obj as PixivItem).IsDownloaded ? true : false;
                        }
                        return (result);
                    });
                }
                else if (filter.Equals("notdownloaded"))
                {
                    filter_action = new Func<object, bool>(obj =>
                    {
                        var result = true;
                        if (obj is PixivItem)
                        {
                            result = (obj as PixivItem).IsDownloaded ? false : true;
                        }
                        return (result);
                    });
                }
                #endregion
                #region Sanity
                else if (filter.Equals("allage") || filter.Equals("fullage"))
                {
                    filter_action = new Func<object, bool>(obj =>
                    {
                        var result = true;
                        if (obj is PixivItem)
                        {
                            var item = obj as PixivItem;
                            if (item.Sanity.Equals("all", StringComparison.CurrentCultureIgnoreCase)) result = true;
                            else result = false;
                        }
                        return (result);
                    });
                }
                else if (filter.Equals("r18") || filter.Equals("18+"))
                {
                    filter_action = new Func<object, bool>(obj =>
                    {
                        var result = true;
                        if (obj is PixivItem)
                        {
                            var item = obj as PixivItem;
                            if (item.Sanity.Equals("18+", StringComparison.CurrentCultureIgnoreCase)) result = true;
                            else result = false;
                        }
                        return (result);
                    });
                }
                else if (filter.Equals("r15") || filter.Equals("15+"))
                {
                    filter_action = new Func<object, bool>(obj =>
                    {
                        var result = true;
                        if (obj is PixivItem)
                        {
                            var item = obj as PixivItem;
                            if (item.Sanity.Equals("15+", StringComparison.CurrentCultureIgnoreCase)) result = true;
                            else result = false;
                        }
                        return (result);
                    });
                }
                #endregion
            }
            if (filter_action != null)
                result_filter = new Predicate<object>(filter_action);
            else
                result_filter = null;
            return (result_filter);
        }
      
        public static Predicate<object> GetFilter(this FilterParam filter)
        {
            Predicate<object> result_filter = null;

            if (filter is FilterParam)
            {
                Func<object, bool> filter_action = null;

                #region pre-process filter key/value
                var filter_type = string.IsNullOrEmpty(filter.Type) ? string.Empty : filter.Type.ToLower();
                var filter_fav_no = string.IsNullOrEmpty(filter.FavoitedRange) ? string.Empty : filter.FavoitedRange.ToLower();
                var filter_fast = string.IsNullOrEmpty(filter.Fast) ? string.Empty : filter.Fast.ToLower();
                var filter_fav = string.IsNullOrEmpty(filter.Favorited) ? string.Empty : filter.Favorited.ToLower();
                var filter_follow = string.IsNullOrEmpty(filter.Followed) ? string.Empty : filter.Followed.ToLower();
                var filter_down = string.IsNullOrEmpty(filter.Downloaded) ? string.Empty : filter.Downloaded.ToLower();
                var filter_sanity = string.IsNullOrEmpty(filter.Sanity) ? string.Empty : filter.Sanity.ToLower();

                if (filter_fav_no.Length > 10) filter_fav_no = filter_fav_no.Substring(10);
                if (filter_fast.Length > 5) filter_fast = filter_fast.Substring(5);
                if (filter_sanity.Length > 7) filter_sanity = filter_sanity.Substring(7);
                #endregion

                #region item type
                bool user = filter_type.Equals("user") ? true : false;
                bool work = filter_type.Equals("work") ? true : false;
                #endregion

                #region item states
                bool favorited = filter_fav.Equals("favorited") ? true : false;
                bool notfavorited = filter_fav.Equals("notfavorited") ? true : false;

                bool followed = filter_follow.Equals("followed") ? true : false;
                bool notfollowed = filter_follow.Equals("notfollowed") ? true : false;

                bool downloaded = filter_down.Equals("downloaded") ? true : false;
                bool notdownloaded = filter_down.Equals("notdownloaded") ? true : false;
                #endregion

                #region sanity / not_sanity
                string[] sanity_age_18 = new string[] { "all", "12+", "15+", "17+", "18+" };
                string[] sanity_age_17 = new string[] { "all", "12+", "15+", "17+" };
                string[] sanity_age_15 = new string[] { "all", "12+", "15+" };
                string[] sanity_age_12 = new string[] { "all", "12+" };
                string[] sanity_age_all = new string[] { "all" };
                bool sanity_include_under = filter.SanityOption_IncludeUnder;
                int sanity_age = -1;
                int not_sanity_age = -1;
                if (filter_sanity.Equals("allage") || filter_sanity.Equals("fullage") || filter_sanity.Equals("all")) sanity_age = 0;
                else if (filter_sanity.Equals("noallage") || filter_sanity.Equals("nofullage+") || filter_sanity.Equals("noall")) not_sanity_age = 0;
                else if (filter_sanity.Equals("r12") || filter_sanity.Equals("r12+") || filter_sanity.Equals("12+")) sanity_age = 12;
                else if (filter_sanity.Equals("nor12") || filter_sanity.Equals("nor12+") || filter_sanity.Equals("no12+")) not_sanity_age = 12;
                else if (filter_sanity.Equals("r15") || filter_sanity.Equals("r15+") || filter_sanity.Equals("15+")) sanity_age = 15;
                else if (filter_sanity.Equals("nor15") || filter_sanity.Equals("nor15+") || filter_sanity.Equals("no15+")) not_sanity_age = 15;
                else if (filter_sanity.Equals("r17") || filter_sanity.Equals("r17+") || filter_sanity.Equals("17+")) sanity_age = 17;
                else if (filter_sanity.Equals("nor17") || filter_sanity.Equals("nor17+") || filter_sanity.Equals("no17+")) not_sanity_age = 17;
                else if (filter_sanity.Equals("r18") || filter_sanity.Equals("r18+") || filter_sanity.Equals("18+")) sanity_age = 18;
                else if (filter_sanity.Equals("nor18") || filter_sanity.Equals("nor18+") || filter_sanity.Equals("no18+")) not_sanity_age = 18;
                #endregion

                #region item favorite range
                int fav_range = 0;
                int.TryParse(filter_fav_no, out fav_range);
                #endregion

                #region fast
                bool portrait = filter_fast.Equals("portrait") ? true : false;
                bool landscape = filter_fast.Equals("landscape") ? true : false;
                bool square = filter_fast.Equals("square") ? true : false;

                bool singlepage = filter_fast.Equals("singlepage") ? true : false;
                bool multipages = filter_fast.Equals("notsinglepage") ? true : false;

                bool size_s = filter_fast.Equals("size1k") ? true : false;
                bool size_m = filter_fast.Equals("size2k") ? true : false;
                bool size_l = filter_fast.Equals("size4k") ? true : false;
                bool size_h = filter_fast.Equals("size8k") ? true : false;

                bool in_history = filter_fast.Equals("inhistory") ? true : false;
                bool not_in_history = filter_fast.Equals("notinhistory") ? true : false;
                #endregion

                var hist = Application.Current.HistoryList();
                var hist_ids = hist.Select(h => h.ID).ToList();
                var hist_idl = hist.Select(h => long.Parse(h.ID)).ToList();

                filter_action = new Func<object, bool>(obj =>
                {
                    var result = true;
                    if (obj is PixivItem)
                    {
                        var item = obj as PixivItem;
                        #region filter by type
                        if (user || work)
                        {
                            if (user)
                                result = item.IsUser() ? true : false;
                            else if (work)
                                result = item.IsWork() ? true : false;
                        }
                        #endregion
                        #region filter by fast simple condition
                        if (!string.IsNullOrEmpty(filter_fast))
                        {
                            #region fast author
                            if (filter_fast.Equals("currentauthor"))
                            {
                                if (item.IsWork())
                                {
                                    //var illust = item.Illust;
                                    //result = result && illust.User.Id ?? -1 == 
                                }
                            }
                            #endregion
                            #region work aspect
                            else if (item.IsWork() && (portrait || landscape || square))
                            {
                                var width = item.Illust.Width ?? 0;
                                var height = item.Illust.Height ?? 0;
                                if (width > 0 && height > 0)
                                {
                                    double aspect = (double)width / (double)height;
                                    if (landscape)
                                        result = result && aspect >= 1.05 ? true : false;
                                    else if (portrait)
                                        result = result && aspect <= 0.95 ? true : false;
                                    else if (square)
                                        result = result && 0.95 < aspect && aspect < 1.05 ? true : false;
                                }
                            }
                            #endregion
                            #region size
                            else if (item.IsWork() && (size_s || size_m || size_l || size_h))
                            {
                                var width = item.Illust.Width ?? 0;
                                var height = item.Illust.Height ?? 0;
                                if (size_s)
                                    result = result && width <= 1024 || height <= 1024 ? true : false;
                                else if (size_m)
                                    result = result && (width > 1024 && width <= 2048) || (height > 1024 && height <= 2048) ? true : false;
                                else if (size_l)
                                    result = result && (width > 2048 && width <= 4096) || (height > 2048 && height <= 4096) ? true : false;
                                else if (size_h)
                                    result = result && width >= 4096 || height >= 4096 ? true : false;
                            }
                            #endregion
                            #region pages
                            else if (item.IsWork() && (singlepage || multipages))
                            {
                                if (singlepage)
                                    result = result && item.Count <= 1 ? true : false;
                                else if (multipages)
                                    result = result && item.Count <= 1 ? false : true;
                            }
                            #endregion
                            #region history
                            else if (in_history || not_in_history)
                            {
                                if (in_history)
                                    result = result && hist_ids.Contains(item.ID) ? true : false;
                                else if (not_in_history)
                                    result = result && hist_ids.Contains(item.ID) ? false : true;
                            }
                            #endregion
                        }
                        #endregion
                        #region filter by favorited number
                        if (fav_range > 0)
                        {
                            if (item.IsWork())
                            {
                                var illust = item.Illust;
                                if (illust is Pixeez.Objects.IllustWork)
                                {
                                    var fav_count = (illust as Pixeez.Objects.IllustWork).total_bookmarks;
                                    result = result && (fav_count > fav_range ? true : false);
                                }
                                else if (illust is Pixeez.Objects.NormalWork && illust.Stats is Pixeez.Objects.WorkStats)
                                {
                                    var fav_count = illust.Stats.FavoritedCount.Public + illust.Stats.FavoritedCount.Private;
                                    result = result && (fav_count > fav_range ? true : false);
                                }
                            }
                        }
                        #endregion
                        #region filter by favorited state
                        if (favorited || notfavorited)
                        {
                            if (favorited)
                                result = result && (item.IsFavorited ? true : false);
                            else if (notfavorited)
                                result = result && (item.IsFavorited ? false : true);
                        }
                        #endregion
                        #region filter by followed state
                        if (followed || notfollowed)
                        {
                            if (followed)
                                result = result && (item.IsFollowed ? true : false);
                            else if (notfollowed)
                                result = result && (item.IsFollowed ? false : true);
                        }
                        #endregion
                        #region filter by downloaded state
                        if (downloaded || notdownloaded)
                        {
                            if (downloaded)
                                result = result && (item.IsDownloaded ? true : false);
                            else if (notdownloaded)
                                result = result && (item.IsDownloaded ? false : true);
                        }
                        #endregion
                        #region filter by sanity state
                        if (sanity_age >= 0 || not_sanity_age >= 0)
                        {
                            var sanity = item.Sanity.ToLower();
                            if (sanity_age == 0)
                                result = result && (sanity.Equals("all") ? true : false);
                            else if (not_sanity_age == 0)
                                result = result && (sanity.Equals("all") ? false : true);

                            else if (sanity_include_under)
                            {
                                if (sanity_age == 12)
                                    result = result && (sanity_age_12.Contains(sanity) ? true : false);
                                else if (not_sanity_age == 0)
                                    result = result && (sanity_age_12.Contains(sanity) ? false : true);

                                else if (sanity_age == 15)
                                    result = result && (sanity_age_15.Contains(sanity) ? true : false);
                                else if (not_sanity_age == 15)
                                    result = result && (sanity_age_15.Contains(sanity) ? false : true);

                                else if (sanity_age == 17)
                                    result = result && (sanity_age_17.Contains(sanity) ? true : false);
                                else if (not_sanity_age == 17)
                                    result = result && (sanity_age_17.Contains(sanity) ? false : true);

                                else if (sanity_age == 18)
                                    result = result && (sanity_age_18.Contains(sanity) ? true : false);
                                else if (not_sanity_age == 18)
                                    result = result && (sanity_age_18.Contains(sanity) ? false : true);
                            }
                            else
                            { 
                                if (sanity_age == 12)
                                    result = result && (sanity.Equals("12+") ? true : false);
                                else if (not_sanity_age == 0)
                                    result = result && (sanity.Equals("12+") ? false : true);

                                else if (sanity_age == 15)
                                    result = result && (sanity.Equals("15+") ? true : false);
                                else if (not_sanity_age == 15)
                                    result = result && (sanity.Equals("15+") ? false : true);

                                else if (sanity_age == 17)
                                    result = result && (sanity.Equals("17+") ? true : false);
                                else if (not_sanity_age == 17)
                                    result = result && (sanity.Equals("17+") ? false : true);

                                else if (sanity_age == 18)
                                    result = result && (sanity.Equals("18+") ? true : false);
                                else if (not_sanity_age == 18)
                                    result = result && (sanity.Equals("18+") ? false : true);
                            }
                        }
                        #endregion
                    }
                    return (result);
                });

                if (filter_action != null)
                    result_filter = new Predicate<object>(filter_action);
                else
                    result_filter = null;
            }                

            return (result_filter);
        }
        #endregion

        #region Tile Update Helper
        public static void UpdateTiles(this ObservableCollection<PixivItem> collection, PixivItem item = null)
        {
            if (collection is ObservableCollection<PixivItem>)
            {
                if (item is PixivItem)
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

        public static void UpdateTiles(this ObservableCollection<PixivItem> collection, IEnumerable<PixivItem> items)
        {
            if (collection is ObservableCollection<PixivItem>)
            {
                if (items is IEnumerable<PixivItem>)
                {
                    var count = collection.Count();
                    foreach (PixivItem sub in items)
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

                    bool part_down = item.Illust.IsPartDownloadedAsync();
                    if (item.IsPartDownloaded != part_down)
                    {
                        item.IsPartDownloaded = part_down;
                        result |= part_down;
                    }

                    bool download = fuzzy ? part_down : item.Illust.GetOriginalUrl(item.Index).IsDownloadedAsync();
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

        public static async void UpdateTilesImageTask(this IEnumerable<PixivItem> items, bool overwrite = false, CancellationToken cancelToken = default(CancellationToken), int parallel = 5, SemaphoreSlim update_semaphore = null)
        {
            try
            {
                if (update_semaphore == null || await update_semaphore.WaitAsync(10))
                {
                    var needUpdate = items.Where(item => item.Source == null || overwrite);
                    if (Application.Current != null && needUpdate.Count() > 0)
                    {
                        if (parallel <= 0) parallel = 1;
                        else if (parallel >= needUpdate.Count()) parallel = needUpdate.Count();

                        Random rnd = new Random();
                        var opt = new ParallelOptions();
                        //opt.TaskScheduler = TaskScheduler.Current;
                        opt.MaxDegreeOfParallelism = parallel;
                        opt.CancellationToken = cancelToken;

                        using (cancelToken.Register(Thread.CurrentThread.Abort))
                        {
                            var ret = Parallel.ForEach(needUpdate, opt, async (item, loopstate, elementIndex) =>
                            {
                                if (cancelToken.IsCancellationRequested)
                                    opt.CancellationToken.ThrowIfCancellationRequested();
                                await Task.Delay(rnd.Next(1, 50));
                                await new Action(async () =>
                                {
                                    try
                                    {
                                        if (item.Count <= 1) item.BadgeValue = string.Empty;
                                        if (item.Source == null || overwrite)
                                        {
                                            item.State = TaskStatus.Running;
                                            var img = await item.Thumb.LoadImageFromUrl(overwrite);
                                            if (item.Source == null) item.Source = img.Source;
                                            if(item.Source is ImageSource)
                                                item.State = TaskStatus.RanToCompletion;
                                            else
                                                item.State = TaskStatus.Faulted;
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
                                        if (item.Source == null && item.Thumb.IsCached())
                                            item.Source = (await item.Thumb.GetImageCachePath().LoadImageFromFile()).Source;
                                        Application.Current.DoEvents();
                                    }
                                }).InvokeAsync();
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var ert = ex.Message;
            }
            finally
            {
                if (update_semaphore is SemaphoreSlim)
                {
                    update_semaphore.Release();
                    await Task.Delay(1);
                }
                Application.Current.DoEvents();
            }
        }

        public static async Task<Task> UpdateTilesThumb(this IEnumerable<PixivItem> items, Task task, bool overwrite = false, CancellationTokenSource cancelSource = default(CancellationTokenSource), int parallel = 5, SemaphoreSlim update_semaphore = null)
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
                        result = Task.Factory.StartNew(delegate
                        {
                            items.UpdateTilesImageTask(overwrite, cancelSource.Token, parallel, update_semaphore);
                        }, cancelSource.Token, TaskCreationOptions.PreferFairness);
                        //result.Start();
                        //await result.ContinueWith((t) =>
                        //{
                        //    //SignalCompletion(sw);
                        //}).InvokeAsync();
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

        public static PixivItem WorkItem(this Pixeez.Objects.Work illust, string url = "", string nexturl = "")
        {
            PixivItem result = null;
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
                        result = new PixivItem()
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
                ex.Message.ShowMessageBox("ERROR");
            }
            return (result);
        }

        public static PixivItem UserItem(this Pixeez.Objects.UserBase user, string nexturl = "")
        {
            PixivItem result = null;
            try
            {
                if (user is Pixeez.Objects.User)
                {
                    var nu = user as Pixeez.Objects.User;
                    var contact = nu.Profile is Pixeez.Objects.Profile ? nu.Profile.Contacts : null;
                    var twitter = contact is Pixeez.Objects.Contacts ? $"📧{contact.Twitter}" : string.Empty;
                    var web = nu.Profile is Pixeez.Objects.Profile ? $"🌐{nu.Profile.Homepage}" : string.Empty;
                    var mail = string.IsNullOrEmpty(nu.Email) ? string.Empty : $"🖃{nu.Email}";

                    var info = new List<string>() { twitter, web, mail };
                    var tooltip = string.Join("\r\n", info).Trim();
                    if (string.IsNullOrEmpty(tooltip))
                    {
                        var cu = nu.Id.FindUser();
                        if (cu is Pixeez.Objects.User)
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

                    var url = nu.GetAvatarUrl();

                    result = new PixivItem()
                    {
                        ItemType = ImageItemType.User,
                        NextURL = nexturl,
                        Thumb = url,
                        BadgeValue = nu.Stats == null ? null : nu.Stats.Works.Value.ToString(),
                        IsFavorited = false,
                        IsFollowed = nu.IsLiked(),
                        Illust = null,
                        ID = nu.Id.ToString(),
                        User = nu,
                        UserID = nu.Id.ToString(),
                        Subject = contact == null ? $"{nu.Name}" : $"{nu.Name} - {contact.Twitter}",
                        DisplayTitle = true,
                        ToolTip = tooltip,
                        Tag = nu
                    };
                }
                else if(user is Pixeez.Objects.NewUser)
                {
                    var nu = user as Pixeez.Objects.NewUser;
                    dynamic contact = null;
                    var twitter = string.Empty;
                    var web = string.Empty;
                    var mail = string.IsNullOrEmpty(nu.Email) ? string.Empty : $"🖃{nu.Email}";

                    var info = new List<string>() { twitter, web, mail };
                    var tooltip = string.Join("\r\n", info).Trim();
                    if (string.IsNullOrEmpty(tooltip))
                    {
                        var cu = nu.Id.FindUser();
                        if (cu is Pixeez.Objects.User)
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

                    var url = nu.GetAvatarUrl();

                    result = new PixivItem()
                    {
                        ItemType = ImageItemType.User,
                        NextURL = nexturl,
                        Thumb = url,
                        BadgeValue = null,
                        IsFavorited = false,
                        IsFollowed = nu.IsLiked(),
                        Illust = null,
                        ID = nu.Id.ToString(),
                        User = nu,
                        UserID = nu.Id.ToString(),
                        Subject = contact == null ? $"{nu.Name}" : $"{nu.Name} - {contact.Twitter}",
                        DisplayTitle = true,
                        ToolTip = tooltip,
                        Tag = nu
                    };
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            return (result);
        }

        #region Image Tile Add Helper
        public static async void AddTo(this IList<Pixeez.Objects.Work> works, IList<PixivItem> Collection, string nexturl = "")
        {
            foreach (var illust in works)
            {
                illust.AddTo(Collection, nexturl);
                await Task.Delay(1);
                Application.Current.DoEvents();
            }
        }

        public static async void AddTo(this Pixeez.Objects.Work illust, IList<PixivItem> Collection, string nexturl = "")
        {
            try
            {
                if (illust is Pixeez.Objects.Work && Collection is IList<PixivItem>)
                {
                    var url = illust.GetThumbnailUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var i = illust.WorkItem(url, nexturl);
                        if (i is PixivItem)
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
                ex.Message.ShowMessageBox("ERROR");
            }
        }

        public static async void AddTo(this Pixeez.Objects.MetaPages pages, IList<PixivItem> Collection, Pixeez.Objects.Work illust, int index, string nexturl = "")
        {
            try
            {
                if (pages is Pixeez.Objects.MetaPages && Collection is IList<PixivItem>)
                {
                    var url = pages.GetThumbnailUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var i = illust.WorkItem(url, nexturl);
                        if (i is PixivItem)
                        {
                            //i.Thumb = url;
                            i.ItemType = ImageItemType.Pages;
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

        public static async void AddTo(this Pixeez.Objects.Page page, IList<PixivItem> Collection, Pixeez.Objects.Work illust, int index, string nexturl = "")
        {
            try
            {
                if (page is Pixeez.Objects.Page && Collection is IList<PixivItem>)
                {
                    var url = page.GetThumbnailUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var i = illust.WorkItem(url, nexturl);
                        if (i is PixivItem)
                        {
                            //i.Thumb = url;
                            i.ItemType = ImageItemType.Page;
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

        public static async void AddTo(this Pixeez.Objects.User user, IList<PixivItem> Collection, string nexturl = "")
        {
            try
            {
                if (user is Pixeez.Objects.User && Collection is IList<PixivItem>)
                {
                    var u = user.UserItem(nexturl);
                    Collection.Add(u);
                    await Task.Delay(1);
                    u.DoEvents();
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

        #region Misc Helper
        public static bool IsUser(this PixivItem item)
        {
            bool result = false;
            try
            {
                if (item is PixivItem)
                {
                    if (item.ItemType == ImageItemType.User) result = item.User is Pixeez.Objects.UserBase ? true : false;
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static bool IsWork(this PixivItem item)
        {
            bool result = false;
            try
            {
                if (item is PixivItem)
                {
                    if (item.ItemType == ImageItemType.Manga ||
                        item.ItemType == ImageItemType.Work ||
                        item.ItemType == ImageItemType.Works ||
                        item.ItemType == ImageItemType.Page ||
                        item.ItemType == ImageItemType.Pages)
                        result = item.Illust is Pixeez.Objects.Work ? true : false;
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static bool IsPage(this PixivItem item)
        {
            bool result = false;
            try
            {
                if (item is PixivItem)
                {
                    if (item.ItemType == ImageItemType.Page)
                        result = item.Illust is Pixeez.Objects.Work ? true : false;
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static bool IsPages(this PixivItem item)
        {
            bool result = false;
            try
            {
                if (item is PixivItem)
                {
                    if (item.ItemType == ImageItemType.Pages)
                        result = item.Illust is Pixeez.Objects.Work ? true : false;
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static bool HasUser(this PixivItem item)
        {
            bool result = false;
            try
            {
                if (item is PixivItem)
                {
                    if (item.ItemType == ImageItemType.Manga ||
                        item.ItemType == ImageItemType.Work ||
                        item.ItemType == ImageItemType.Works ||
                        item.ItemType == ImageItemType.Page ||
                        item.ItemType == ImageItemType.Pages ||
                        item.ItemType == ImageItemType.User)
                        result = item.User is Pixeez.Objects.UserBase ? true : false;
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static bool HasIllust(this PixivItem item)
        {
            bool result = false;
            try
            {
                if (item is PixivItem)
                {
                    if (item.ItemType == ImageItemType.Manga ||
                        item.ItemType == ImageItemType.Work ||
                        item.ItemType == ImageItemType.Works ||
                        item.ItemType == ImageItemType.Page ||
                        item.ItemType == ImageItemType.Pages)
                        result = item.Illust is Pixeez.Objects.Work ? true : false;
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static bool HasPages(this PixivItem item)
        {
            bool result = false;
            try
            {
                if (item is PixivItem)
                {
                    if (item.ItemType == ImageItemType.Manga ||
                        item.ItemType == ImageItemType.Work ||
                        item.ItemType == ImageItemType.Works ||
                        item.ItemType == ImageItemType.Page ||
                        item.ItemType == ImageItemType.Pages)
                        result = item.Count > 1 ? true : false;
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static bool IsManga(this PixivItem item)
        {
            bool result = false;
            try
            {
                if (item is PixivItem)
                {
                    if (item.ItemType == ImageItemType.Manga) result = true;
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static bool IsNovel(this PixivItem item)
        {
            bool result = false;
            try
            {
                if (item is PixivItem)
                {
                    if (item.ItemType == ImageItemType.Novel) result = true;
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static bool IsBook(this PixivItem item)
        {
            bool result = false;
            try
            {
                if (item is PixivItem)
                {
                    if (item.ItemType == ImageItemType.Manga || item.ItemType == ImageItemType.Novel) result = true;
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static bool IsNone(this PixivItem item)
        {
            bool result = false;
            try
            {
                if (item is PixivItem)
                {
                    if (item.ItemType == ImageItemType.None) result = true;
                }
            }
            catch (Exception) { }
            return (result);
        }
        #endregion
    }


}
