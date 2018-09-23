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
    public partial class ImageListGrid : UserControl
    {
        //public static readonly DependencyProperty ColumnsProperty =
        //    DependencyProperty.Register("Columns", typeof(int), typeof(ImageListGrid),
        //    new FrameworkPropertyMetadata(5, new PropertyChangedCallback(ColumnsPropertyChangedCallback)));
        //private static PropertyChangedCallback ColumnsPropertyChangedCallback;
        [Description("Get or Set Columns for display Image Tile Grid")]
        [Category("Common Properties")]
        [DefaultValue(5)]
        public int Columns { get; set; }

        [Description("Get or Set Tile Width")]
        [Category("Common Properties")]
        [DefaultValue(128)]
        public int TileWidth { get; set; }

        [Description("Get or Set Tile Height")]
        [Category("Common Properties")]
        [DefaultValue(128)]
        public int TileHeight { get; set; }

        [Description("Get or Set Image Tiles Select Item Index")]
        [Category("Common Properties")]
        public int SelectedIndex
        {
            get { return PART_ImageTiles.SelectedIndex; }
            set { PART_ImageTiles.SelectedIndex = value; }
        }

        [Description("Get or Set Image Tiles Select Item")]
        [Category("Common Properties")]
        public ImageItem SelectedItem
        {
            get { return PART_ImageTiles.SelectedItem is ImageItem ? PART_ImageTiles.SelectedItem as ImageItem : null; }
        }

        [Description("Get or Set Image Tiles Select Items")]
        [Category("Common Properties")]
        public IList<ImageItem> SelectedItems
        {
            get
            {
                if (PART_ImageTiles.SelectedItems == null)
                    return null;
                else
                {
                    System.Collections.IList items = (System.Collections.IList)PART_ImageTiles.SelectedItems;
                    var collection = items.Cast<ImageItem>();
                    //IList<ImageItem> collection = (IList<ImageItem>)PART_ImageTiles.SelectedItems;
                    return (collection.ToList());
                }
            }
        }

        [Description("Get or Set Image Tiles Selection Mode")]
        [Category("Common Properties")]
        public SelectionMode SelectionMode
        {
            get { return PART_ImageTiles.SelectionMode; }
            set { PART_ImageTiles.SelectionMode = value; }
        }

        //public static readonly DependencyProperty ItemProperty =
        //    DependencyProperty.Register("TileItems", typeof(IEnumerable<ImageItem>), typeof(ListView), new UIPropertyMetadata(null));

        private ObservableCollection<ImageItem> ImageList = new ObservableCollection<ImageItem>();
        [Description("Get or Set Image Tiles List")]
        [Category("Common Properties")]
        public ObservableCollection<ImageItem> Items
        {
            get { return ImageList; }
            //set { ImageList = value; OnPropertyChanged(); }
        }

        private Visibility badgevisibility = Visibility.Visible;
        public Visibility BadgeVisibility
        {
            get { return badgevisibility; }
            set { badgevisibility = value; }
        }
        public bool DisplayBadge
        {
            get
            {
                if (badgevisibility == Visibility.Visible) return true;
                else return false;
            }
            set
            {
                if (value) badgevisibility = Visibility.Visible;
                else badgevisibility = Visibility.Collapsed;
            }
        }

        private Visibility titlevisibility = Visibility.Visible;
        public Visibility TitleVisibility
        {
            get { return titlevisibility; }
            set { titlevisibility = value; }
        }
        public bool DisplayTitle
        {
            get
            {
                if (titlevisibility == Visibility.Visible) return true;
                else return false;
            }
            set
            {
                if (value) titlevisibility = Visibility.Visible;
                else titlevisibility = Visibility.Collapsed;
            }
        }

        public ImageListGrid()
        {
            InitializeComponent();
            PART_ImageTiles.ItemsSource = ImageList;
            //Columns = 5;
            //TileWidth = 128;
            //TileHeight = 128;
        }

        private bool UPDATING = false;
        public void UpdateImageTile(Pixeez.Tokens tokens, int parallel=15)
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
                                //item.Source = await item.Thumb.ToImageSource(tokens);
                                item.Source = await item.Thumb.LoadImage(tokens);
                                PART_ImageTiles.Items.Refresh();
                            }
                        }
                        catch (Exception ex)
                        {
                            MetroWindow window = Application.Current.MainWindow as MetroWindow;
                            await window.ShowMessageAsync("ERROR", $"Download Image Failed:\n{ex.Message}");
                        }
                    }));
                });
                UPDATING = false;
            }).Start();
        }

        public void Refresh()
        {
            PART_ImageTiles.Items.Refresh();
        }


        // Create custom routed event by first registering a RoutedEventID
        // This event uses the bubbling routing strategy
        public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
            "SelectionChanged", RoutingStrategy.Bubble, typeof(SelectionChangedEventHandler), typeof(ImageListGrid));
        public event SelectionChangedEventHandler SelectionChanged
        {
            add { AddHandler(SelectionChangedEvent, value); }
            remove { RemoveHandler(SelectionChangedEvent, value); }
        }
        // This method raises the Tap event
        void RaiseSelectionChangedEvent()
        {
            RoutedEventArgs newEventArgs = new RoutedEventArgs(SelectionChangedEvent);
            RaiseEvent(newEventArgs);
        }
        // For demonstration purposes we raise the event when the MyButtonSimple is clicked
        protected void OnSelectionChangedEvent()
        {
            RaiseSelectionChangedEvent();
        }

        // Create custom routed event by first registering a RoutedEventID
        // This event uses the bubbling routing strategy
        public static readonly RoutedEvent ScrollChangedEvent = EventManager.RegisterRoutedEvent(
            "ScrollChanged", RoutingStrategy.Bubble, typeof(ScrollChangedEventHandler), typeof(ImageListGrid));
        public event SelectionChangedEventHandler ScrollnChanged
        {
            add { AddHandler(SelectionChangedEvent, value); }
            remove { RemoveHandler(SelectionChangedEvent, value); }
        }
        // This method raises the Tap event
        void RaiseScrollChangedEvent()
        {
            RoutedEventArgs newEventArgs = new RoutedEventArgs(ScrollChangedEvent);
            RaiseEvent(newEventArgs);
        }
        // For demonstration purposes we raise the event when the MyButtonSimple is clicked
        protected void OnScrollChangedEvent()
        {
            RaiseScrollChangedEvent();
        }

        //public ImageListGrid()
        //{
        //    ImageTiles.SelectionChanged += SelectionChanged;
        //    ImageTiles.ScrollChanged += ScrollChanged;
        //}
    }

}
