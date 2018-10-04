using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PixivWPF.Common
{
    public class ImageItem : FrameworkElement, INotifyPropertyChanged
    {
        //public static readonly DependencyProperty SourceProperty =
        //    DependencyProperty.Register("Source", typeof(ImageSource), typeof(ImageItem), new UIPropertyMetadata(null));

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
        //public Visibility BadgeVisibility { get; set; }
        public string UserID { get; set; }
        public string ID { get; set; }
        //public Pixeez.Objects.IllustWork Illust { get; set; }
        public Pixeez.Objects.Work Illust { get; set; }
        public string AccessToken { get; set; }
        public string NextURL { get; set; }

        public string BadgeValue { get; set; }
        public Visibility BadgeVisibility { get; set; }
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
                NotifyPropertyChanged();
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
                NotifyPropertyChanged();
            }
        }

        public async Task RefreshIllustAsync(Pixeez.Tokens tokens)
        {
            var illusts = await tokens.GetWorksAsync(Illust.Id.Value);
            foreach(var illust in illusts)
            {
                if (Illust.Id == illust.Id)
                {
                    Illust = illust;
                    break;
                }
            }
        }

        public async Task RefreshUserInfoAsync(Pixeez.Tokens tokens)
        {
            var users = await tokens.GetUsersAsync(Illust.User.Id.Value);
            foreach (var user in users)
            {
                if (user.Id == Illust.User.Id)
                {
                    if (user is Pixeez.Objects.User)
                    {
                        var u = user as Pixeez.Objects.User;
                        Illust.User.is_followed = u.IsFollowing;
                    }
                    else if (user is Pixeez.Objects.UserBase)
                    {
                        var u = user as Pixeez.Objects.UserBase;
                        Illust.User.is_followed = u.is_followed;
                    }
                    break;
                }
            }
        }

        public async void RefreshIllust(Pixeez.Tokens tokens)
        {
            var illusts = await tokens.GetWorksAsync(Illust.Id.Value);
            foreach (var illust in illusts)
            {
                Illust = illust;
                break;
            }
        }

        public async void RefreshUserInfo(Pixeez.Tokens tokens)
        {
            var users = await tokens.GetUsersAsync(Illust.User.Id.Value);
            foreach (var user in users)
            {
                Illust.User.is_followed = user.is_followed;
                break;
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public static class ImageTileHelper
    {
        #region Image Tile Add Helper
        public static void AddTo(this IList<Pixeez.Objects.Work> works, IList<ImageItem> Colloection, string nexturl = "")
        {
            foreach (var illust in works)
            {
                illust.AddTo(Colloection, nexturl);
            }
        }

        public static void AddTo(this Pixeez.Objects.Work illust, IList<ImageItem> Colloection, string nexturl = "")
        {
            try
            {
                if (illust is Pixeez.Objects.Work && Colloection is IList<ImageItem>)
                {
                    var url = illust.GetThumbnailUrl();

                    if (!string.IsNullOrEmpty(url))
                    {
                        var tooltip = string.IsNullOrEmpty(illust.Caption) ? string.Empty : string.Join("", illust.Caption.InsertLineBreak(48).Take(256));
                        var i = new ImageItem()
                        {
                            NextURL = nexturl,
                            Thumb = url,
                            Index = -1,
                            Count = (int)illust.PageCount,
                            BadgeValue = illust.PageCount.Value.ToString(),
                            BadgeVisibility = illust.PageCount > 1 ? Visibility.Visible : Visibility.Collapsed,
                            DisplayBadge = illust.PageCount > 1 ? true : false,
                            ID = illust.Id.ToString(),
                            UserID = illust.User.Id.ToString(),
                            Subject = illust.Title,
                            DisplayTitle = true,
                            Caption = illust.Caption,
                            ToolTip = tooltip,
                            Illust = illust,
                            Tag = illust
                        };
                        Colloection.Add(i);
                    }
                }
            }
            catch (Exception ex)
            {
                CommonHelper.ShowMessageDialog("ERROR", ex.Message);
            }
        }

        public static void AddTo(this Pixeez.Objects.MetaPages pages, IList<ImageItem> Colloection, Pixeez.Objects.Work illust, int index, string nexturl = "")
        {
            try
            {
                if (pages is Pixeez.Objects.MetaPages && Colloection is IList<ImageItem>)
                {
                    var url = pages.GetThumbnailUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var i = new ImageItem()
                        {
                            NextURL = nexturl,
                            Thumb = url,
                            Index = index,
                            Count = illust.PageCount.Value,
                            BadgeValue = (index+1).ToString(),
                            ID = illust.Id.ToString(),
                            UserID = illust.User.Id.ToString(),
                            Subject = $"{illust.Title} - {index+1}/{illust.PageCount}",
                            DisplayTitle = false,
                            Illust = illust,
                            Tag = pages
                        };
                        Colloection.Add(i);
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
        }

        public static void AddTo(this Pixeez.Objects.Page page, IList<ImageItem> Colloection, Pixeez.Objects.Work illust, int index, string nexturl = "")
        {
            try
            {
                if (page is Pixeez.Objects.Page && Colloection is IList<ImageItem>)
                {
                    var url = page.GetThumbnailUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var i = new ImageItem()
                        {
                            NextURL = nexturl,
                            Thumb = url,
                            Index = index,
                            Count = illust.PageCount.Value,
                            BadgeValue = (index+1).ToString(),
                            ID = illust.Id.ToString(),
                            UserID = illust.User.Id.ToString(),
                            Subject = $"{illust.Title} - {index+1}/{illust.PageCount}",
                            DisplayTitle = false,
                            Illust = illust,
                            Tag = page
                        };
                        Colloection.Add(i);
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
        }

        public static void AddTo(this Pixeez.Objects.User user, IList<ImageItem> Colloection)
        {
            try
            {
                if (user is Pixeez.Objects.User && Colloection is IList<ImageItem>)
                {
                    var url = user.GetAvatarUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var i = new ImageItem()
                        {
                            Thumb = url,
                            BadgeValue = user.Stats.Works.Value.ToString(),
                            ID = user.Id.ToString(),
                            UserID = user.Id.ToString(),
                            Subject = $"{user.Name} - {user.Profile.Contacts.Twitter}",
                            DisplayTitle = true,
                            Illust = null,
                            Tag = user
                        };
                        Colloection.Add(i);
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
        public static string GetThumbnailUrl(this Pixeez.Objects.Page page)
        {
            var url = string.Empty;
            if (page is Pixeez.Objects.Page)
            {
                var images = page.ImageUrls;
                url = images.Px128x128;
                if (string.IsNullOrEmpty(url))
                {
                    if (!string.IsNullOrEmpty(images.SquareMedium))
                        url = images.SquareMedium;
                    else if (!string.IsNullOrEmpty(images.Px480mw))
                        url = images.Px480mw;
                    else if (!string.IsNullOrEmpty(images.Small))
                        url = images.Px128x128;
                    else if (!string.IsNullOrEmpty(images.Medium))
                        url = images.Medium;
                    else if (!string.IsNullOrEmpty(images.Large))
                        url = images.Large;
                    else if (!string.IsNullOrEmpty(images.Original))
                        url = images.Original;
                }
            }
            return (url);
        }

        public static string GetPreviewUrl(this Pixeez.Objects.Page page)
        {
            var url = string.Empty;
            if (page is Pixeez.Objects.Page)
            {
                var images = page.ImageUrls;
                url = images.Large;
                if (string.IsNullOrEmpty(url))
                {
                    if (!string.IsNullOrEmpty(images.Original))
                        url = images.Original;
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

        public static string GetOriginalUrl(this Pixeez.Objects.Page page)
        {
            var url = string.Empty;
            if (page is Pixeez.Objects.Page)
            {
                var images = page.ImageUrls;
                url = images.Original;
                if (string.IsNullOrEmpty(url))
                {
                    if (!string.IsNullOrEmpty(images.Large))
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
            }
            return (url);
        }
        #endregion

        #region MetaPage Helper
        public static string GetThumbnailUrl(this Pixeez.Objects.MetaPages pages)
        {
            var url = string.Empty;
            if (pages is Pixeez.Objects.MetaPages)
            {
                var images = pages.ImageUrls;
                url = images.Px128x128;
                if (string.IsNullOrEmpty(url))
                {
                    if (!string.IsNullOrEmpty(images.SquareMedium))
                        url = images.SquareMedium;
                    else if (!string.IsNullOrEmpty(images.Px480mw))
                        url = images.Px480mw;
                    else if (!string.IsNullOrEmpty(images.Small))
                        url = images.Px128x128;
                    else if (!string.IsNullOrEmpty(images.Medium))
                        url = images.Medium;
                    else if (!string.IsNullOrEmpty(images.Large))
                        url = images.Large;
                    else if (!string.IsNullOrEmpty(images.Original))
                        url = images.Original;
                }
            }
            return (url);
        }

        public static string GetPreviewUrl(this Pixeez.Objects.MetaPages pages)
        {
            var url = string.Empty;
            if (pages is Pixeez.Objects.MetaPages)
            {
                var images = pages.ImageUrls;
                url = images.Large;
                if (string.IsNullOrEmpty(url))
                {
                    if (!string.IsNullOrEmpty(images.Original))
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

        public static string GetPreviewUrl(this Pixeez.Objects.IllustWork Illust, int idx)
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
                    url = pages.GetPreviewUrl();
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
        public static string GetThumbnailUrl(this Pixeez.Objects.NormalWork Illust, int idx)
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
                    url = pages.GetThumbnailUrl();
                }
            }
            return (url);
        }

        public static string GetPreviewUrl(this Pixeez.Objects.NormalWork Illust, int idx)
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
                    url = pages.GetPreviewUrl();
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
        public static string GetThumbnailUrl(this Pixeez.Objects.Work Illust, int index = -1)
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
                if (!string.IsNullOrEmpty(Illust.ImageUrls.Px128x128))
                    url = Illust.ImageUrls.Px128x128;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.SquareMedium))
                    url = Illust.ImageUrls.SquareMedium;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Px480mw))
                    url = Illust.ImageUrls.Px480mw;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Medium))
                    url = Illust.ImageUrls.Medium;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Small))
                    url = Illust.ImageUrls.Small;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Large))
                    url = Illust.ImageUrls.Large;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Original))
                    url = Illust.ImageUrls.Original;
            }
            return (url);
        }

        public static string GetPreviewUrl(this Pixeez.Objects.Work Illust, int index = -1)
        {
            var url = string.Empty;

            if (Illust is Pixeez.Objects.IllustWork)
            {
                var illust = Illust as Pixeez.Objects.IllustWork;
                url = illust.GetPreviewUrl(index);
            }
            else if (Illust is Pixeez.Objects.NormalWork)
            {
                var illust = Illust as Pixeez.Objects.NormalWork;
                url = illust.GetPreviewUrl(index);
            }

            if (string.IsNullOrEmpty(url))
            {
                if (!string.IsNullOrEmpty(Illust.ImageUrls.Large))
                    url = Illust.ImageUrls.Large;
                else if (!string.IsNullOrEmpty(Illust.ImageUrls.Original))
                    url = Illust.ImageUrls.Original;
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
        public static string GetThumbnailUrl(this Pixeez.Objects.NewUser user)
        {
            var url = user.profile_image_urls.Px128x128;
            if (string.IsNullOrEmpty(url))
            {
                if (!string.IsNullOrEmpty(user.profile_image_urls.SquareMedium))
                    url = user.profile_image_urls.SquareMedium;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Px480mw))
                    url = user.profile_image_urls.Px480mw;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Medium))
                    url = user.profile_image_urls.Medium;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Small))
                    url = user.profile_image_urls.Small;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Large))
                    url = user.profile_image_urls.Large;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Original))
                    url = user.profile_image_urls.Original;
            }
            return (url);
        }

        public static string GetPreviewUrl(this Pixeez.Objects.NewUser user)
        {
            var url = user.profile_image_urls.Large;
            if (string.IsNullOrEmpty(url))
            {
                if (!string.IsNullOrEmpty(user.profile_image_urls.Original))
                    url = user.profile_image_urls.Original;
                else if (!string.IsNullOrEmpty(user.profile_image_urls.Medium))
                    url = user.profile_image_urls.Medium;
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

        #region Get Illust Work DateTime
        public static DateTime GetDateTime(this Pixeez.Objects.Work Illust)
        {
            var dt = DateTime.Now;
            if (Illust is Pixeez.Objects.IllustWork)
            {
                var illustset = Illust as Pixeez.Objects.IllustWork;
                dt = illustset.CreatedTime;
            }
            else if (Illust is Pixeez.Objects.NormalWork)
            {
                var illustset = Illust as Pixeez.Objects.NormalWork;
                dt = illustset.CreatedTime.UtcDateTime;
            }
            else if (!string.IsNullOrEmpty(Illust.ReuploadedTime))
            {
                dt = DateTime.Parse(Illust.ReuploadedTime);
            }
            return (dt);
        }
        #endregion

    }


}
