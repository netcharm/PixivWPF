using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.IconPacks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
    public partial class ImageListGrid : UserControl, INotifyPropertyChanged
    {
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
            set { PART_ImageTiles.SelectedItem = value; }
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

        private ObservableCollection<ImageItem> ImageList = new ObservableCollection<ImageItem>();
        [Description("Get or Set Image Tiles List")]
        [Category("Common Properties")]
        public ObservableCollection<ImageItem> Items
        {
            get { return (ImageList); }
            set
            {
                ImageList = value;
                PART_ImageTiles.ItemsSource = value;
                NotifyPropertyChanged("ItemsChanged");
            }
        }
        //public ItemCollection Items
        //{
        //    get { return (PART_ImageTiles.Items); }
        //}
        public IEnumerable ItemsSource
        {
            get { return (PART_ImageTiles.ItemsSource); }
            set { PART_ImageTiles.ItemsSource = value; }
        }

        [Description("Get or Set Image Tiles LiveFilter")]
        [Category("Common Properties")]
        public Predicate<object> Filter
        {
            get { return (PART_ImageTiles.Items is ItemCollection ? PART_ImageTiles.Items.Filter : null); }
            set { if (PART_ImageTiles.Items is ItemCollection) PART_ImageTiles.Items.Filter = value; }
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

        [Description("Get or Set Image Tiles Count after filtered/current be displayed")]
        [Category("Common Properties")]
        public int ItemsCount { get { return (PART_ImageTiles.Items != null ? PART_ImageTiles.Items.Count : 0); } }

        private Visibility titlevisibility = Visibility.Visible;
        public Visibility TitleVisibility
        {
            get { return titlevisibility; }
            set { titlevisibility = value; }
        }
        [Description("Get or Set Item Title Visibility")]
        [Category("Common Properties")]
        [DefaultValue(true)]
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

        public event SelectionChangedEventHandler SelectionChanged;
        public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);
        private void PART_ImageTiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Handled) return;
            SelectionChanged?.Invoke(sender, e);
        }

        public new event KeyUpEventHandler KeyUp;
        public delegate void KeyUpEventHandler(object sender, KeyEventArgs e);
        private void PART_ImageTiles_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Handled) return;
            KeyUp?.Invoke(sender, e);
        }

        public new event MouseWheelEventHandler MouseWheel;
        public delegate void MouseWheelEventHandler(object sender, MouseWheelEventArgs e);
        private void PART_ImageTiles_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            MouseWheel?.Invoke(sender, e);
        }

        public new event PreviewMouseWheelEventHandler PreviewMouseWheel;
        public delegate void PreviewMouseWheelEventHandler(object sender, MouseWheelEventArgs e);
        private void PART_ImageTiles_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;
            PreviewMouseWheel?.Invoke(sender, e);
        }

        public new event MouseDownEventHandler MouseDown;
        public delegate void MouseDownEventHandler(object sender, MouseButtonEventArgs e);
        private void PART_ImageTiles_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled) return;
            MouseDown?.Invoke(sender, e);
            //if (MouseDown is MouseDownEventHandler)
            //{
            //    MouseDown?.Invoke(sender, e);
            //}
            //else
            //{
            //    if (PART_ImageTiles.SelectedIndex < 0)
            //    {
            //        PART_ImageTiles.SelectedIndex = 0;
            //        e.Handled = true;
            //    }
            //    else if (e.XButton1 == MouseButtonState.Pressed)
            //    {
            //        PART_ImageTiles.Items.MoveCurrentToNext();
            //        PART_ImageTiles.ScrollIntoView(PART_ImageTiles.SelectedItem);
            //        e.Handled = true;
            //    }
            //    else if (e.XButton2 == MouseButtonState.Pressed)
            //    {
            //        PART_ImageTiles.Items.MoveCurrentToPrevious();
            //        PART_ImageTiles.ScrollIntoView(PART_ImageTiles.SelectedItem);
            //        e.Handled = true;
            //    }
            //}
        }

        internal Task lastTask = null;
        internal CancellationTokenSource cancelTokenSource;

        public ImageListGrid()
        {
            InitializeComponent();

            cancelTokenSource = new CancellationTokenSource();

            PART_ImageTiles.ItemsSource = ImageList;
            //Columns = 5;
            //TileWidth = 128;
            //TileHeight = 128;
        }

        public void Cancel()
        {
            if (cancelTokenSource is CancellationTokenSource)
            {
                cancelTokenSource.Cancel(true);
            }
        }

        public async void UpdateTilesImage(int parallel = 5, SemaphoreSlim updating_semaphore = null)
        {
            Application.Current.DoEvents();
            var needUpdate = Items.Where(item => item.Source == null);
            if (needUpdate.Count() > 0)
            {
                //PART_ImageTilesWait.Show();
                lastTask = await Items.UpdateTilesThumb(lastTask, cancelTokenSource, parallel, updating_semaphore);
                //if (lastTask.IsCompleted) PART_ImageTilesWait.Hide();
            }
        }

        public void Refresh()
        {
            PART_ImageTiles.Items.Refresh();
            //CollectionViewSource.GetDefaultView(this).Refresh();
        }
        
        public void Filtering(string filter)
        {
            if (PART_ImageTiles.Items.CanFilter)
            {
                if (string.IsNullOrEmpty(filter))
                    PART_ImageTiles.Items.Filter = null;
                else
                    PART_ImageTiles.Items.Filter = filter.GetFilter();
            }
        }

        private void TileBadge_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if (sender is Badged && e.Property != null)
            {
                var badge = sender as Badged;
                if (e.Property.Name.Equals("Tag", StringComparison.CurrentCultureIgnoreCase) ||
                    e.Property.Name.Equals("Visibility", StringComparison.CurrentCultureIgnoreCase))
                {
                    var badged = true;
                    if (badge.Tag is bool) badged = (bool)badge.Tag;
                    badge.Visibility = badged ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void TileImage_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if (e.Property == null) return;
            if (sender is Image)
            {
                var image = sender as Image;
                if (e.Property.Name.Equals("Source", StringComparison.CurrentCultureIgnoreCase))
                {
                    //var mask = image.FindName("PART_Mask") as Border;
                    var progressObj = image.FindName("PART_Progress");
                    if (progressObj is ProgressRing)
                    {
                        var progress = progressObj as ProgressRing;
                        if (image.Source == null)
                            progress.Show();
                        else
                            progress.Hide();
                    }
                }
            }
            else if(sender is Grid)
            {
                var tile = sender as Grid;
                if (e.Property.Name.Equals("Tag", StringComparison.CurrentCultureIgnoreCase))
                {
                    var progressObj = tile.FindName("PART_Progress");
                    if (progressObj is ProgressRing)
                    {
                        var progress = progressObj as ProgressRing;
                        if (tile.Tag is TaskStatus)
                        {
                            var state = (TaskStatus)tile.Tag;
                            if (state == TaskStatus.RanToCompletion)
                                progress.Hide();
                            else if (state == TaskStatus.Created)
                                progress.Show();
                            else
                                progress.Pause();
                        }
                    }
                }
            }
            else if (sender is PackIconModern)
            {
#if DEBUG
                var icon = sender as PackIconModern;
                if (e.Property.Name.Equals("Visibility", StringComparison.CurrentCultureIgnoreCase))
                {
                    var follow = icon.FindName("PART_Follow");
                    var fav = icon.FindName("PART_Favorite");
                    if (follow is PackIconModern && fav is PackIconModern)
                    {
                        var follow_mark = follow as PackIconModern;
                        var follow_effect = follow_mark.FindName("PART_Follow_Shadow");
                        var fav_mark = fav as PackIconModern;
                        if (fav_mark.Visibility == Visibility.Visible)
                        {
                            follow_mark.Height = 16;
                            follow_mark.Width = 16;
                            follow_mark.Margin = new Thickness(0, 0, 12, 12);
                            follow_mark.Foreground = Common.Theme.WhiteBrush;
                            if (follow_effect is System.Windows.Media.Effects.DropShadowEffect)
                            {
                                var shadow = follow_effect as System.Windows.Media.Effects.DropShadowEffect;
                                shadow.Color = Common.Theme.AccentColor;
                            }
                        }
                        else
                        {
                            follow_mark.Height = 24;
                            follow_mark.Width = 24;
                            follow_mark.Margin = new Thickness(0, 0, 8, 8);
                            follow_mark.Foreground = Common.Theme.AccentBrush;
                            if (follow_effect is System.Windows.Media.Effects.DropShadowEffect)
                            {
                                var shadow = follow_effect as System.Windows.Media.Effects.DropShadowEffect;
                                shadow.Color = Common.Theme.WhiteColor;
                            }
                        }
                    }
                }
#endif
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void RaisePropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
