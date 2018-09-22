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
        public ImageSource Source { get; set; }
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
            //get
            //{
            //    if (BadgeVisibility == Visibility.Visible) return true;
            //    else return false;
            //}
            set
            {
                if (value) BadgeVisibility = Visibility.Visible;
                else BadgeVisibility = Visibility.Collapsed;
            }
        }

        public Visibility TitleVisibility = Visibility.Visible;
        public bool DisplayTitle
        {
            //get
            //{
            //    if (TitleVisibility == Visibility.Visible) return true;
            //    else return false;
            //}
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
                    var url = illust.ImageUrls.SquareMedium;
                    if (string.IsNullOrEmpty(url))
                    {
                        if (!string.IsNullOrEmpty(illust.ImageUrls.Small))
                        {
                            url = illust.ImageUrls.Small;
                        }
                        else if (!string.IsNullOrEmpty(illust.ImageUrls.Px128x128))
                        {
                            url = illust.ImageUrls.Px128x128;
                        }
                        else if (!string.IsNullOrEmpty(illust.ImageUrls.Px480mw))
                        {
                            url = illust.ImageUrls.Px480mw;
                        }
                        else if (!string.IsNullOrEmpty(illust.ImageUrls.Medium))
                        {
                            url = illust.ImageUrls.Medium;
                        }
                        else if (!string.IsNullOrEmpty(illust.ImageUrls.Large))
                        {
                            url = illust.ImageUrls.Large;
                        }
                        else if (!string.IsNullOrEmpty(illust.ImageUrls.Original))
                        {
                            url = illust.ImageUrls.Original;
                        }
                    }

                    if (!string.IsNullOrEmpty(url))
                    {
                        var tooltip = string.IsNullOrEmpty(illust.Caption) ? string.Empty : string.Join("", illust.Caption.InsertLineBreak(48).Take(256));
                        var i = new ImageItem()
                        {
                            NextURL = nexturl,
                            Thumb = url,
                            Count = (int)illust.PageCount,
                            //Badge = illust.PageCount > 1 ? Visibility.Visible : Visibility.Collapsed,
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

        public static void AddTo(this Pixeez.Objects.MetaPages pages, IList<ImageItem> Colloection, Pixeez.Objects.Work illust, string nexturl = "")
        {
            try
            {
                if (pages is Pixeez.Objects.MetaPages && Colloection is IList<ImageItem>)
                {
                    var url = pages.ImageUrls.SquareMedium;
                    if (string.IsNullOrEmpty(url))
                    {
                        if (!string.IsNullOrEmpty(pages.ImageUrls.Small))
                        {
                            url = pages.ImageUrls.Small;
                        }
                        else if (!string.IsNullOrEmpty(pages.ImageUrls.Px128x128))
                        {
                            url = pages.ImageUrls.Px128x128;
                        }
                        else if (!string.IsNullOrEmpty(pages.ImageUrls.Px480mw))
                        {
                            url = pages.ImageUrls.Px480mw;
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

                    if (!string.IsNullOrEmpty(url))
                    {
                        var i = new ImageItem()
                        {
                            NextURL = nexturl,
                            Thumb = url,
                            Count = 1,
                            DisplayBadge = false,
                            ID = illust.Id.ToString(),
                            UserID = illust.User.Id.ToString(),
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

    }


}
