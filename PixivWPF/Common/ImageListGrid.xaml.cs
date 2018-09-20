using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PixivWPF.Common
{
    /// <summary>
    /// ImageListGrid.xaml 的交互逻辑
    /// </summary>
    public partial class ImageListGrid : ListView
    {
        //public static readonly DependencyProperty ColumnsProperty =
        //    DependencyProperty.Register("Columns", typeof(int), typeof(ImageListGrid),
        //    new FrameworkPropertyMetadata(5, new PropertyChangedCallback(ColumnsPropertyChangedCallback)));
        //private static PropertyChangedCallback ColumnsPropertyChangedCallback;
        [Description("Get or Set Columns for display Image Tile Grid")]
        [Category("Common Properties")]
        [DefaultValue(5)]
        public int Columns { get; set; }


        private ObservableCollection<ImageItem> ImageList = new ObservableCollection<ImageItem>();
        [Description("Get or Set Image Tiles List")]
        [Category("Common Properties")]
        public new ObservableCollection<ImageItem> Items
        {
            get { return ImageList; }
        }

        public ImageListGrid()
        {
            InitializeComponent();
        }

        private bool UPDATING = false;
        public void UpdateImageTile(Pixeez.Tokens tokens, int parallel=10)
        {
            if (UPDATING) return;

            var needUpdate = ImageList.Where(item => item.Source == null);

            new Thread(delegate ()
            {
                var opt = new ParallelOptions();

                if (parallel <= 0) parallel = 1;
                else if (parallel >= needUpdate.Count()) parallel = needUpdate.Count();

                opt.MaxDegreeOfParallelism = parallel;
                Parallel.ForEach(needUpdate, opt, (item, loopstate, elementIndex) =>
                {
                    item.Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            if (item.Source == null)
                            {
                                item.Source = await item.Thumb.ToImageSource(tokens);
                                ImageTiles.Items.Refresh();
                            }
                        }
                        catch (Exception ex)
                        {
                            MetroWindow window = Application.Current.MainWindow as MetroWindow;
                            await window.ShowMessageAsync("ERROR", ex.Message);
                        }
                    }));
                });
                UPDATING = false;
            }).Start();
        }
    }

    public class ImageItem : FrameworkElement
    {
        public ImageSource Source { get; set; }
        public string Thumb { get; set; }
        public string Subject { get; set; }
        public string Caption { get; set; }
        public int Count { get; set; }
        //public Visibility Badge { get; set; }
        public string UserID { get; set; }
        public string ID { get; set; }
        //public Pixeez.Objects.IllustWork Illust { get; set; }
        public Pixeez.Objects.Work Illust { get; set; }
        public string AccessToken { get; set; }
        public string NextURL { get; set; }

        private Visibility Badge = Visibility.Visible;
        public bool DisplayBadge
        {
            get
            {
                if (Badge == Visibility.Visible) return true;
                else return false;
            }
            set
            {
                if (value) Badge = Visibility.Visible;
                else Badge = Visibility.Collapsed;
            }
        }

        private Visibility TitleVisibility = Visibility.Visible;
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

        public static async void AddTo(this Pixeez.Objects.Work illust, IList<ImageItem> Colloection, string nexturl = "")
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
                            Illust = illust
                        };
                        Colloection.Add(i);
                    }
                }
            }
            catch (Exception ex)
            {
                MetroWindow window = Application.Current.MainWindow as MetroWindow;
                await window.ShowMessageAsync("ERROR", ex.Message);
            }
        }
    }
}
