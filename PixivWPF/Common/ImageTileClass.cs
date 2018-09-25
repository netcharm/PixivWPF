using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PixivWPF.Common
{
    public class ImageItem : FrameworkElement
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
            }
        }
        public string Thumb { get; set; }
        public string Subject { get; set; }
        public string Caption { get; set; }
        public int Count { get; set; }
        //public Visibility BadgeVisibility { get; set; }
        public string UserID { get; set; }
        public string ID { get; set; }
        //public Pixeez.Objects.IllustWork Illust { get; set; }
        public Pixeez.Objects.Work Illust { get; set; }
        public string AccessToken { get; set; }
        public string NextURL { get; set; }

        public Visibility BadgeVisibility = Visibility.Visible;
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
            }
        }

        public Visibility TitleVisibility = Visibility.Visible;
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
            }
        }
    }

    public static class ImageTileHelper
    {
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

                    //var url = illust.ImageUrls.Px128x128;
                    //if (string.IsNullOrEmpty(url))
                    //{
                    //    if (!string.IsNullOrEmpty(illust.ImageUrls.SquareMedium))
                    //    {
                    //        url = illust.ImageUrls.SquareMedium;
                    //    }
                    //    else if (!string.IsNullOrEmpty(illust.ImageUrls.Px480mw))
                    //    {
                    //        url = illust.ImageUrls.Px480mw;
                    //    }
                    //    else  if (!string.IsNullOrEmpty(illust.ImageUrls.Small))
                    //    {
                    //        url = illust.ImageUrls.Small;
                    //    }
                    //    else if (!string.IsNullOrEmpty(illust.ImageUrls.Medium))
                    //    {
                    //        url = illust.ImageUrls.Medium;
                    //    }
                    //    else if (!string.IsNullOrEmpty(illust.ImageUrls.Large))
                    //    {
                    //        url = illust.ImageUrls.Large;
                    //    }
                    //    else if (!string.IsNullOrEmpty(illust.ImageUrls.Original))
                    //    {
                    //        url = illust.ImageUrls.Original;
                    //    }
                    //}

                    if (!string.IsNullOrEmpty(url))
                    {
                        var tooltip = string.IsNullOrEmpty(illust.Caption) ? string.Empty : string.Join("", illust.Caption.InsertLineBreak(48).Take(256));
                        var i = new ImageItem()
                        {
                            NextURL = nexturl,
                            Thumb = url,
                            Count = (int)illust.PageCount,
                            BadgeVisibility = illust.PageCount > 1 ? Visibility.Visible : Visibility.Collapsed,
                            //DisplayBadge = illust.PageCount > 1 ? true : false,
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
                    var all_pages = (illust as Pixeez.Objects.IllustWork).meta_pages;
                    var url = pages.GetThumbnailUrl();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var i = new ImageItem()
                        {
                            NextURL = nexturl,
                            Thumb = url,
                            Count = index+1,
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
                CommonHelper.ShowMessageDialog("ERROR", ex.Message);
            }
        }

        public static string GetThumbnailUrl(this Pixeez.Objects.Work Illust)
        {
            var url = Illust.ImageUrls.Px128x128;
            if (string.IsNullOrEmpty(url))
            {
                if (!string.IsNullOrEmpty(Illust.ImageUrls.SquareMedium))
                    url = Illust.ImageUrls.SquareMedium;
                else if (Illust is Pixeez.Objects.IllustWork)
                {
                    var illust = Illust as Pixeez.Objects.IllustWork;
                    if (illust.meta_single_page != null)
                        url = illust.meta_single_page.OriginalImageUrl;
                    else if (illust.meta_pages is Array)
                    {
                        if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.Px128x128))
                            url = illust.meta_pages[0].ImageUrls.Px128x128;
                        else if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.SquareMedium))
                            url = illust.meta_pages[0].ImageUrls.SquareMedium;
                        else if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.Px480mw))
                            url = illust.meta_pages[0].ImageUrls.Px480mw;
                        else if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.Medium))
                            url = illust.meta_pages[0].ImageUrls.Medium;
                        else if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.Small))
                            url = illust.meta_pages[0].ImageUrls.Small;
                        else if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.Large))
                            url = illust.meta_pages[0].ImageUrls.Large;
                        else if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.Original))
                            url = illust.meta_pages[0].ImageUrls.Original;
                    }
                }
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

        public static string GetThumbnailUrl(this Pixeez.Objects.MetaPages pages)
        {
            var url = pages.ImageUrls.Px128x128;
            if (string.IsNullOrEmpty(url))
            {
                if (!string.IsNullOrEmpty(pages.ImageUrls.Small))
                {
                    url = pages.ImageUrls.Small;
                }
                else if (!string.IsNullOrEmpty(pages.ImageUrls.SquareMedium))
                {
                    url = pages.ImageUrls.SquareMedium;
                }
                else if (!string.IsNullOrEmpty(pages.ImageUrls.Px480mw))
                {
                    url = pages.ImageUrls.Px480mw;
                }
                else if (!string.IsNullOrEmpty(pages.ImageUrls.Small))
                {
                    url = pages.ImageUrls.Px128x128;
                }
                else if (!string.IsNullOrEmpty(pages.ImageUrls.Medium))
                {
                    url = pages.ImageUrls.Medium;
                }
                else if (!string.IsNullOrEmpty(pages.ImageUrls.Large))
                {
                    url = pages.ImageUrls.Large;
                }
                else if (!string.IsNullOrEmpty(pages.ImageUrls.Original))
                {
                    url = pages.ImageUrls.Original;
                }
            }
            return (url);
        }

        public static string GetPreviewUrl(this Pixeez.Objects.Work Illust)
        {
            var url = Illust.ImageUrls.Large;
            if (string.IsNullOrEmpty(url))
            {
                if (!string.IsNullOrEmpty(Illust.ImageUrls.Original))
                    url = Illust.ImageUrls.Original;
                else if (Illust is Pixeez.Objects.IllustWork)
                {
                    var illust = Illust as Pixeez.Objects.IllustWork;
                    if (illust.meta_single_page != null)
                        url = illust.meta_single_page.OriginalImageUrl;
                    else if (illust.meta_pages is Array)
                    {
                        if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.Large))
                            url = illust.meta_pages[0].ImageUrls.Large;
                        else if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.Original))
                            url = illust.meta_pages[0].ImageUrls.Original;
                        else if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.Medium))
                            url = illust.meta_pages[0].ImageUrls.Medium;
                        else if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.Px480mw))
                            url = illust.meta_pages[0].ImageUrls.Px480mw;
                        else if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.SquareMedium))
                            url = illust.meta_pages[0].ImageUrls.SquareMedium;
                        else if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.Px128x128))
                            url = illust.meta_pages[0].ImageUrls.Px128x128;
                        else if (!string.IsNullOrEmpty(illust.meta_pages[0].ImageUrls.Small))
                            url = illust.meta_pages[0].ImageUrls.Small;
                    }
                }
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

    }


}
