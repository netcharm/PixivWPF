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

        private ObservableCollection<ImageItem> ImageList = new ObservableCollection<ImageItem>();
        [Description("Get or Set Image Tiles List")]
        [Category("Common Properties")]
        public ObservableCollection<ImageItem> Items
        {
            get
            {
                return ImageList;
            }
            //set
            //{
            //    ImageList = value;
            //    NotifyPropertyChanged("Items");
            //    NotifyPropertyChanged("Source");
            //}
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

        public event SelectionChangedEventHandler SelectionChanged;
        public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);
        private void PART_ImageTiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(sender, e);
        }

        public new event KeyUpEventHandler KeyUp;
        public delegate void KeyUpEventHandler(object sender, KeyEventArgs e);
        private void PART_ImageTiles_KeyUp(object sender, KeyEventArgs e)
        {
            KeyUp?.Invoke(sender, e);
        }

        public new event MouseWheelEventHandler MouseWheel;
        public delegate void MouseWheelEventHandler(object sender, MouseWheelEventArgs e);
        private void PART_ImageTiles_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            MouseWheel?.Invoke(sender, e);
        }

        public new event PreviewMouseWheelEventHandler PreviewMouseWheel;
        public delegate void PreviewMouseWheelEventHandler(object sender, MouseWheelEventArgs e);
        private void PART_ImageTiles_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            PreviewMouseWheel?.Invoke(sender, e);
        }

        public ImageListGrid()
        {
            InitializeComponent();

            cancelTokenSource = new CancellationTokenSource();
            cancelToken = cancelTokenSource.Token;

            PART_ImageTiles.ItemsSource = ImageList;
            //Columns = 5;
            //TileWidth = 128;
            //TileHeight = 128;
        }

        private bool UPDATING = false;
        private Task<bool> UpdateTask = null;

        internal Task lastTask = null;
        internal CancellationTokenSource cancelTokenSource;
        internal CancellationToken cancelToken;

        internal void UpdateImageTilesTask(Pixeez.Tokens tokens, int parallel=15)
        {
            if (UPDATING) return;

            var needUpdate = ImageList.Where(item => item.Source == null);
            if (needUpdate.Count() > 0)
            {
                UpdateTask = new Task<bool>(delegate ()
                {
                    bool result = true;
                    UPDATING = result;
                    try
                    {
                        var opt = new ParallelOptions();

                        if (parallel <= 0) parallel = 1;
                        else if (parallel >= needUpdate.Count()) parallel = needUpdate.Count();
                        opt.MaxDegreeOfParallelism = parallel;

                        var ret = Parallel.ForEach(needUpdate, opt, (item, loopstate, elementIndex) =>
                        {
                            if (cancelToken.IsCancellationRequested)
                            {
                                cancelToken.ThrowIfCancellationRequested();
                                return;
                            }

                            item.Dispatcher.BeginInvoke(new Action(async () =>
                            {
                                try
                                {
                                    if (item.Source == null)
                                    {
                                        if (item.Count <= 1) item.BadgeValue = string.Empty;
                                        item.Source = await item.Thumb.LoadImage(tokens);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    var ert = ex.Message;
                                    //$"Download Image Failed:\n{ex.Message}".ShowMessageBox("ERROR");
                                }
                            }));
                        });
                        if (ret.IsCompleted)
                        {
                            result = !ret.IsCompleted;
                        }
                    }
                    catch (Exception ex)
                    {
                        var ert = ex.Message;
                        result = false;
                    }
                    finally
                    {
                        result = false;
                    }
                    UPDATING = result;
                    return (result);
                });
                UpdateTask.Start();
            }
        }

        public async void UpdateImageTiles(Pixeez.Tokens tokens, int parallel = 15)
        {
            try
            {
                if (lastTask is Task)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    lastTask.Wait();
                }

                if (lastTask == null || (lastTask is Task && (lastTask.IsCanceled || lastTask.IsCompleted || lastTask.IsFaulted)))
                {
                    lastTask = new Task(() => {
                        UpdateImageTilesTask(tokens, parallel);
                    }, cancelTokenSource.Token, TaskCreationOptions.None);
                    //lastTask.RunSynchronously();
                    lastTask.Start();
                    await lastTask;
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
            }
        }

        public void Refresh()
        {
            PART_ImageTiles.Items.Refresh();
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
            if (sender is Image && e.Property != null)
            {
                var image = sender as Image;
                if (e.Property.Name.Equals("Source", StringComparison.CurrentCultureIgnoreCase))
                {
                    var progressObj = image.FindName("PART_Progress");
                    if(progressObj is ProgressRing)
                    {
                        var progress = progressObj as ProgressRing;
                        if (image.Source == null)
                            progress.Show();
                        else
                            progress.Hide();
                    }
                }
            }
        }
    }

}
