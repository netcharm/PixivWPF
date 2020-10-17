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

        private string result_filter = string.Empty;

        private MenuItem ActionResultFilter = null;
        private ContextMenu ContextMenuResultFilter = null;
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

        private void ShowHistory(string filter = "")
        {
            try
            {
                HistoryWait.Show();

                if (string.IsNullOrEmpty(filter)) filter = result_filter;

                var no_filter = string.IsNullOrEmpty(filter);
                var filter_string = no_filter ? string.Empty : $" ({filter.Replace("users入り", "+ Favs")})";

                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    HistoryItems.Items.Clear();
                    foreach (var item in Application.Current.HistorySource())
                    {
                        HistoryItems.Items.Add(item);
                    }
                }
                else
                {
                    UpdateLikeState();
                    UpdateDownloadState();

                    //var source = Application.Current.HistorySource();
                    //foreach (var item in HistoryItems.Items)
                    //{
                    //    var hist = source.Where(i => i.ID == item.ID && i.UserID == item.UserID);
                    //    if (hist.Count() >= 1)
                    //    {
                    //        var new_item = hist.First();
                    //        item.Illust = new_item.Illust;
                    //        item.User = new_item.User;
                    //        item.IsFollowed = new_item.IsFollowed;
                    //        item.IsFavorited = new_item.IsFavorited;
                    //    }
                    //}
                }

                if (HistoryItems.Items.Count() == 0 && window != null && no_filter)
                    window.Close();
                else
                    HistoryItems.UpdateTilesImage();
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
                HistoryItems.UpdateTilesImage();
                Application.Current.DoEvents();
            }
            catch (Exception) { }
        }

        internal void UpdateDetail()
        {
            ShowHistory();
            if (window != null)
            {
                window.SizeToContent = SizeToContent.WidthAndHeight;
                if(window is ContentWindow) (window as ContentWindow).AdjustWindowPos();
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

        public void SetFilter(string filter_type, string filter_fav, string filter_follow, string filter_down, string filter_sanity)
        {
            try
            {
                var filter = new FilterParam()
                {
                    Type = filter_type,
                    Favorited = filter_fav,
                    Followed = filter_follow,
                    Downloaded = filter_down,
                    Sanity = filter_sanity
                };
                SetFilter(filter);
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
            #region Update ContextMenu
            var cmr = Resources["MenuHistoryResult"] as ContextMenu;
            if (cmr is ContextMenu)
            {
                foreach (dynamic item in cmr.Items)
                {
                    if (item is MenuItem && item.Name.Equals("ActionResultFilter", StringComparison.CurrentCultureIgnoreCase))
                    {
                        ActionResultFilter = item;
                        break;
                    }
                }
            }

            var cmf = Resources["MenuHistoryFilter"] as ContextMenu;
            if (cmf is ContextMenu)
            {
                ContextMenuResultFilter = cmf;
                foreach (dynamic item in cmf.Items)
                {
                    if (item is MenuItem)
                    {
                        var mi = item as MenuItem;
                        if (mi.Name.Equals("HistoryFilter_00000users", StringComparison.CurrentCultureIgnoreCase))
                        {
                            mi.IsChecked = true;
                            break;
                        }
                    }
                }
            }
            #endregion

            window = Window.GetWindow(this);

            if (window != null)
            {
                var wa = System.Windows.Forms.Screen.GetWorkingArea(new System.Drawing.Point((int)window.Left, (int)window.Top));
                window.MaxHeight = Math.Min(960, wa.Height);
            }

            try
            {
                foreach (var item in Application.Current.HistorySource())
                {
                    HistoryItems.Items.Add(item);
                }
            }
            catch (Exception) { }
            UpdateDetail();
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

        private void HistoryFilter_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is MenuItem)
            {
                var mi = sender as MenuItem;
                if (mi.Name.StartsWith("ActionFilter_"))
                {
                    foreach (MenuItem item in ActionResultFilter.Items)
                    {
                        if (item == sender)
                        {
                            item.IsChecked = true;
                            var filter = Regex.Replace(item.Uid, @"SearchFilter_0*", "", RegexOptions.IgnoreCase);
                            result_filter = filter.Equals("users", StringComparison.CurrentCultureIgnoreCase) ? string.Empty : $"{filter}入り";
                        }
                        else item.IsChecked = false;

                        var cmi = ContextMenuResultFilter.Items.Cast<MenuItem>().Where(o=>string.Equals(o.Uid, item.Uid, StringComparison.CurrentCultureIgnoreCase));
                        if (cmi.Count() > 0)
                        {
                            if (cmi.First() is MenuItem) cmi.First().IsChecked = item.IsChecked;
                        }
                    }
                }
                else if (mi.Name.StartsWith("SearchFilter_"))
                {
                    foreach (MenuItem item in ContextMenuResultFilter.Items)
                    {
                        if (item == sender)
                        {
                            item.IsChecked = true;
                            var filter = Regex.Replace(item.Uid, @"SearchFilter_0*", "", RegexOptions.IgnoreCase);
                            result_filter = filter.Equals("users", StringComparison.CurrentCultureIgnoreCase) ? string.Empty : $"{filter}入り";
                        }
                        else item.IsChecked = false;

                        var cmi = ActionResultFilter.Items.Cast<MenuItem>().Where(o=>string.Equals(o.Uid, item.Uid, StringComparison.CurrentCultureIgnoreCase));
                        if (cmi.Count() > 0)
                        {
                            if (cmi.First() is MenuItem) cmi.First().IsChecked = item.IsChecked;
                        }
                    }
                }
                ShowHistory();
            }
        }
        #endregion
    }
}
