using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

namespace PixivWPF.Pages
{
    /// <summary>
    /// HistoryPage.xaml 的交互逻辑
    /// </summary>
    public partial class HistoryPage : Page
    {
        private Window window = null;
        public Point Pos { get; set; } = new Point(0, 0);

        private string result_filter = string.Empty;

        private Dictionary<string, Tuple<MenuItem, MenuItem>> filter_items = new Dictionary<string, Tuple<MenuItem, MenuItem>>();

        public string Contents { get; set; } = string.Empty;

        private void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            HistoryItems.UpdateDownloadStateAsync(illustid, exists);
        }

        public async void UpdateDownloadStateAsync(int? illustid = null, bool? exists = false)
        {
            await Task.Run(() =>
            {
                UpdateDownloadState(illustid, exists);
            });
        }

        public async void UpdateLikeStateAsync(int illustid = -1, bool is_user = false)
        {
            await new Action(() => {
                UpdateLikeState(illustid, is_user);
            }).InvokeAsync();
        }

        public void UpdateLikeState(int illustid = -1, bool is_user = false)
        {
                HistoryItems.UpdateLikeState(illustid, is_user);
        }

        public void AddToHistory(Pixeez.Objects.Work illust)
        {
            if(HistoryItems.Items is ObservableCollection<ImageItem>)
            {
                //Application.Current.HistoryAdd(illust);
                Application.Current.HistoryAdd(illust, HistoryItems.Items);
                UpdateDetail();
            }            
        }

        public void AddToHistory(Pixeez.Objects.User user)
        {
            if (HistoryItems.Items is ObservableCollection<ImageItem>)
            {
                Application.Current.HistoryAdd(user, HistoryItems.Items);
                UpdateDetail();
            }
        }

        public void AddToHistory(Pixeez.Objects.UserBase user)
        {
            if (HistoryItems.Items is ObservableCollection<ImageItem>)
            {
                Application.Current.HistoryAdd(user, HistoryItems.Items);
                UpdateDetail();
            }
        }

        private SemaphoreSlim CanUpdating = new SemaphoreSlim(1, 1);
        private void ShowHistory()
        {
            try
            {
                HistoryWait.Show();
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    HistoryItems.Items.Clear();
                    foreach (var item in Application.Current.HistorySource())
                    {
                        HistoryItems.Items.Add(item);
                    }
                    Application.Current.DoEvents();
                }
                else
                {
                    UpdateLikeState();
                    Application.Current.DoEvents();
                    UpdateDownloadState();
                    Application.Current.DoEvents();
                }
                HistoryItems.UpdateTilesImage(5, CanUpdating);
            }
            catch (Exception ex)
            {
                if (ex is NullReferenceException)
                {
                    //"No Result".ShowMessageBox("WARNING");
                    "No Result".ShowToast("WARNING[HISTORY]");
                }
                else
                {
                    ex.Message.ShowMessageBox("ERROR[HISTORY]");
                }
            }
            finally
            {
                HistoryWait.Hide();
                Application.Current.DoEvents();
            }
        }

        internal void UpdateThumb()
        {
            try
            {
                HistoryItems.UpdateTilesImage(5, CanUpdating);
                Application.Current.DoEvents();
            }
            catch (Exception) { }
        }

        internal void UpdateDetail()
        {
            //if (CanUpdating is SemaphoreSlim) CanUpdating.Release();
            ShowHistory();
            if (window != null)
            {
                window.SizeToContent = SizeToContent.WidthAndHeight;
                if (window is ContentWindow) (window as ContentWindow).AdjustWindowPos();
            }
        }

        public void SetFilter(string filter)
        {
            try
            {
                HistoryItems.Filter = filter.GetFilter();
            }
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
        }

        public void SetFilter(FilterParam filter)
        {
            try
            {
                if (filter is FilterParam)
                {
                    HistoryItems.Filter = filter.GetFilter();
                }
                else
                {
                    HistoryItems.Filter = null;
                }
            }
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
        }

        public dynamic GetTilesCount()
        {
            return (HistoryItems.ItemsCount);
        }

        internal void KeyAction(KeyEventArgs e)
        {
            HistoryIllusts_PreviewKeyUp(this, e);
        }

        public HistoryPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            window = Window.GetWindow(this);

            if (window != null)
            {
                var wa = System.Windows.Forms.Screen.GetWorkingArea(new System.Drawing.Point((int)window.Left, (int)window.Top));
                window.MaxHeight = Math.Min(960, wa.Height);
            }

            try
            {
                if (CanUpdating is SemaphoreSlim) CanUpdating.Release();
                HistoryItems.Items.Clear();
                foreach (var item in Application.Current.HistorySource())
                {
                    HistoryItems.Items.Add(item);
                }
                Application.Current.DoEvents();
                UpdateDetail();
            }
            catch (Exception) { }
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (window is ContentWindow) (window as ContentWindow).AdjustWindowPos();
        }

        #region History Result related routines
        private void ActionCopyResultIllustID_Click(object sender, RoutedEventArgs e)
        {
            Commands.CopyIllustIDs.Execute(HistoryItems);
        }

        private void ActionOpenResult_Click(object sender, RoutedEventArgs e)
        {
            Commands.Open.Execute(HistoryItems);
        }

        private void ActionSendToOtherInstance_Click(object sender, RoutedEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.None)
                Commands.SendToOtherInstance.Execute(HistoryItems);
            else
                Commands.ShellSendToOtherInstance.Execute(HistoryItems);
        }

        private void ActionRefreshResult_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                var m = sender as MenuItem;
                var host = (m.Parent as ContextMenu).PlacementTarget;
                if (m.Uid.Equals("ActionRefresh", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (host == HistoryItems)
                    {
                        ShowHistory();
                    }
                }
                else if (m.Uid.Equals("ActionRefreshThumb", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (host == HistoryItems)
                    {
                        HistoryItems.UpdateTilesImage();
                    }
                }
            }
        }

        private void ActionSaveResult_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                foreach (ImageItem item in HistoryItems.SelectedItems)
                {
                    Commands.SaveIllust.Execute(item);
                }
            }
        }

        private void ActionSaveAllResult_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                foreach (ImageItem item in HistoryItems.SelectedItems)
                {
                    Commands.SaveIllustAll.Execute(item);
                }
            }
        }

        private void ActionOpenDownloaded_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                foreach (ImageItem item in HistoryItems.SelectedItems)
                {
                    Commands.OpenDownloaded.Execute(item);
                }
            }
        }

        private void ActionSpeech_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Empty;
            CultureInfo culture = null;
            if (sender is MenuItem)
            {
                var mi = sender as MenuItem;
                if (mi.Parent is ContextMenu)
                {
                    var host = (mi.Parent as ContextMenu).PlacementTarget;
                    if (host == HistoryItems)
                    {
                        foreach (ImageItem item in HistoryItems.SelectedItems)
                        {
                            text += $"{item.Subject},\r\n";
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(text)) text.Play(culture);
        }

        private void ActionBookmarkIllust_Click(object sender, RoutedEventArgs e)
        {
            string uid = (sender as dynamic).Uid;
            try
            {
                if (uid.Equals("ActionLikeIllust", StringComparison.CurrentCultureIgnoreCase) ||
                    uid.Equals("ActionLikeIllustPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                    uid.Equals("ActionUnLikeIllust", StringComparison.CurrentCultureIgnoreCase))
                {
                    IList<ImageItem> items = new List<ImageItem>();
                    var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                    if (host == HistoryItems) items = HistoryItems.GetSelectedIllusts();
                    try
                    {
                        if (uid.Equals("ActionLikeIllust", StringComparison.CurrentCultureIgnoreCase))
                        {
                            items.LikeIllust();
                        }
                        else if (uid.Equals("ActionLikeIllustPrivate", StringComparison.CurrentCultureIgnoreCase))
                        {
                            items.LikeIllust(false);
                        }
                        else if (uid.Equals("ActionUnLikeIllust", StringComparison.CurrentCultureIgnoreCase))
                        {
                            items.UnLikeIllust();
                        }
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
        }

        private void ActionFollowAuthor_Click(object sender, RoutedEventArgs e)
        {
            string uid = (sender as dynamic).Uid;
            try
            {
                if (uid.Equals("ActionLikeUser", StringComparison.CurrentCultureIgnoreCase) ||
                    uid.Equals("ActionLikeUserPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                    uid.Equals("ActionUnLikeUser", StringComparison.CurrentCultureIgnoreCase))
                {
                    IList<ImageItem> items = new List<ImageItem>();
                    var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                    if (host == HistoryItems) items = HistoryItems.GetSelected();
                    try
                    {
                        if (uid.Equals("ActionLikeUser", StringComparison.CurrentCultureIgnoreCase))
                        {
                            items.LikeUser();
                        }
                        else if (uid.Equals("ActionLikeUserPrivate", StringComparison.CurrentCultureIgnoreCase))
                        {
                            items.LikeUser(false);
                        }
                        else if (uid.Equals("ActionUnLikeUser", StringComparison.CurrentCultureIgnoreCase))
                        {
                            items.UnLikeUser();
                        }
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception) { }
        }

        private void HistoryIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {                
                Commands.Open.Execute(HistoryItems);
            }
            catch (Exception) { }
        }

        private void HistoryIllusts_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            Commands.KeyProcessor.Execute(new KeyValuePair<dynamic, KeyEventArgs>(HistoryItems, e));
        }
        #endregion
    }
}
