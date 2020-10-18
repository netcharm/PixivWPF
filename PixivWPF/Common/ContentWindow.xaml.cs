using MahApps.Metro.Controls;
using PixivWPF.Pages;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PixivWPF.Common
{
    /// <summary>
    /// ImageViewerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ContentWindow : MetroWindow
    {
        public Queue<WindowState> LastWindowStates { get; set; } = new Queue<WindowState>();

        public void SetDropBoxState(bool state)
        {
            CommandToggleDropbox.IsChecked = state;
        }

        public void UpdateTheme(MetroWindow win = null)
        {
            if (win != null)
                CommonHelper.UpdateTheme(win);
            else
                CommonHelper.UpdateTheme();
        }

        private ObservableCollection<string> auto_suggest_list = new ObservableCollection<string>();
        public ObservableCollection<string> AutoSuggestList
        {
            get { return (auto_suggest_list); }
        }

        public ContentWindow()
        {
            InitializeComponent();

            //this.GlowBrush = null;

            SearchBox.ItemsSource = AutoSuggestList;

            //Topmost = true;
            ShowActivated = true;
            //Activate();

            LastWindowStates.Enqueue(WindowState.Normal);
            UpdateTheme(this);
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CommandPageRefresh.Hide();
            CommandFilter.Hide();

            if (Content is IllustDetailPage ||
                Content is IllustImageViewerPage ||
                Content is SearchResultPage ||
                Content is HistoryPage)
            {
                CommandPageRefresh.Show();
                if (!(Content is IllustImageViewerPage))
                {
                    CommandPageRefreshThumb.Show();
                    CommandFilter.Show();
                }
            }

            if (this.DropBoxExists() == null)
                CommandToggleDropbox.IsChecked = false;
            else
                CommandToggleDropbox.IsChecked = true;

            this.AdjustWindowPos();
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (this.Content is DownloadManagerPage)
                {
                    (this.Content as DownloadManagerPage).Pos = new Point(this.Left, this.Top);
                }
                if (Application.Current.GetLoginWindow() != null) e.Cancel = true;
            }
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR[CLOSEWIN]"); }
        }

        private void MetroWindow_DragOver(object sender, DragEventArgs e)
        {
            var fmts = e.Data.GetFormats(true);
            if (new List<string>(fmts).Contains("Text"))
            {
                e.Effects = DragDropEffects.Link;
            }
        }

        private void MetroWindow_Drop(object sender, DragEventArgs e)
        {
            var links = e.ParseDragContent();
            foreach (var link in links)
            {
                Commands.OpenSearch.Execute(link);
            }
        }

        private void MetroWindow_KeyUp(object sender, KeyEventArgs e)
        {
            Commands.KeyProcessor.Execute(new KeyValuePair<dynamic, KeyEventArgs>(Content, e));
        }

        private void MetroWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
            if(e.ChangedButton == MouseButton.Middle)
            {
                if (Title.Equals("DropBox", StringComparison.CurrentCultureIgnoreCase))
                {
                    Hide();
                }
                else if (Title.Equals("Download Manager", StringComparison.CurrentCultureIgnoreCase))
                {
                    Hide();
                }
                else
                {
                    Close();
                }
                e.Handled = true;
            }
        }

        private void MetroWindow_StateChanged(object sender, EventArgs e)
        {
            LastWindowStates.Enqueue(this.WindowState);
            if (LastWindowStates.Count > 2) LastWindowStates.Dequeue();
        }

        private void CommandPageRefresh_Click(object sender, RoutedEventArgs e)
        {
            if(sender == CommandPageRefresh)
                Commands.RefreshPage.Execute(Content);
            else if(sender == CommandPageRefreshThumb)
                Commands.RefreshPageThumb.Execute(Content);
        }

        private void CommandLogin_Click(object sender, RoutedEventArgs e)
        {
            Commands.Login.Execute(sender);
        }

        private void CommandDownloadManager_Click(object sender, RoutedEventArgs e)
        {
            Commands.OpenDownloadManager.Execute(true);
        }

        private void CommandToggleDropbox_Click(object sender, RoutedEventArgs e)
        {
            Commands.OpenDropBox.Execute(sender);
        }

        private void CommandHistory_Click(object sender, RoutedEventArgs e)
        {
            Commands.OpenHistory.Execute(null);
        }

        private void CommandSearch_Click(object sender, RoutedEventArgs e)
        {
            Commands.OpenSearch.Execute(SearchBox.Text);
        }

        private void SearchBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text.Length > 0)
            {
                auto_suggest_list.Clear();

                var content = SearchBox.Text.ParseLink().ParseID();
                if (!string.IsNullOrEmpty(content))
                {
                    content.GetSuggestList().ToList().ForEach(t => auto_suggest_list.Add(t));
                    SearchBox.Items.Refresh();
                    SearchBox.IsDropDownOpen = true;
                }

                e.Handled = true;
            }
        }

        private void SearchBox_DropDownOpened(object sender, EventArgs e)
        {
            var textBox = Keyboard.FocusedElement as TextBox;
            if (textBox != null && textBox.Text.Length == 1 && textBox.SelectionLength == 1)
            {
                textBox.SelectionLength = 0;
                textBox.SelectionStart = 1;
            }
        }

        private void SearchBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = true;
            var items = e.AddedItems;
            if (items.Count > 0)
            {
                var item = items[0];
                if (item is string)
                {
                    var query = (string)item;
                    Commands.OpenSearch.Execute(query);
                }
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                e.Handled = true;
                Commands.OpenSearch.Execute(SearchBox.Text);
            }
        }

        private void LiveFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem)) return;
            if (sender == LiveFilterFavoritedRange) return;

            var menus_type = new List<MenuItem>() {
                LiveFilterUser, LiveFilterWork
            };
            var menus_fast = new List<MenuItem>() {
                LiveFilterFast_None,
                LiveFilterFast_Portrait, LiveFilterFast_Landscape, LiveFilterFast_Square,
                LiveFilterFast_CurrentAuthor
            };
            var menus_fav_no = new List<MenuItem>() {
                LiveFilterFavorited_00000,
                LiveFilterFavorited_00100, LiveFilterFavorited_00200, LiveFilterFavorited_00500,
                LiveFilterFavorited_01000, LiveFilterFavorited_02000, LiveFilterFavorited_05000,
                LiveFilterFavorited_10000, LiveFilterFavorited_20000, LiveFilterFavorited_50000,
            };
            var menus_fav = new List<MenuItem>() {
                LiveFilterFavorited, LiveFilterNotFavorited,
            };
            var menus_follow = new List<MenuItem>() {
                LiveFilterFollowed, LiveFilterNotFollowed,
            };
            var menus_down = new List<MenuItem>() {
                LiveFilterDownloaded, LiveFilterNotDownloaded,
            };
            var menus_sanity = new List<MenuItem>() {
                LiveFilterSanity_Any,
                LiveFilterSanity_All, LiveFilterSanity_NoAll,
                LiveFilterSanity_R12, LiveFilterSanity_NoR12,
                LiveFilterSanity_R15, LiveFilterSanity_NoR15,
                LiveFilterSanity_R17, LiveFilterSanity_NoR17,
                LiveFilterSanity_R18, LiveFilterSanity_NoR18,
            };

            var menus = new List<IEnumerable<MenuItem>>() { menus_type, menus_fav_no, menus_fast, menus_fav, menus_follow, menus_down, menus_sanity };

            var idx = "LiveFilter".Length;

            string filter_type = string.Empty;
            string filter_fav_no = string.Empty;
            string filter_fast = string.Empty;
            string filter_fav = string.Empty;
            string filter_follow = string.Empty;
            string filter_down = string.Empty;
            string filter_sanity = string.Empty;

            var menu = sender as MenuItem;

            LiveFilterFavoritedRange.IsChecked = false;
            LiveFilterFast.IsChecked = false;
            LiveFilterSanity.IsChecked = false;

            if (menu == LiveFilterNone)
            {
                LiveFilterNone.IsChecked = true;
                #region un-check all filter conditions
                foreach (var fmenus in menus)
                {
                    foreach (var fmenu in fmenus)
                    {
                        fmenu.IsChecked = false;
                        fmenu.IsEnabled = true;
                    }
                }
                #endregion
            }
            else
            {
                LiveFilterNone.IsChecked = false;
                #region filter by item type 
                foreach (var fmenu in menus_type)
                {
                    if (menus_type.Contains(menu))
                    {
                        if (fmenu == menu) fmenu.IsChecked = !fmenu.IsChecked;
                        else fmenu.IsChecked = false;
                    }
                    if (fmenu.IsChecked) filter_type = fmenu.Name.Substring(idx);
                }
                if (menu == LiveFilterUser && menu.IsChecked)
                {
                    foreach (var fmenu in menus_fav)
                        fmenu.IsEnabled = false;
                    foreach (var fmenu in menus_down)
                        fmenu.IsEnabled = false;
                    foreach (var fmenu in menus_sanity)
                        fmenu.IsEnabled = false;
                }
                else
                {
                    foreach (var fmenu in menus_fav)
                        fmenu.IsEnabled = true;
                    foreach (var fmenu in menus_down)
                        fmenu.IsEnabled = true;
                    foreach (var fmenu in menus_sanity)
                        fmenu.IsEnabled = true;
                }
                #endregion
                #region filter by favirited number
                LiveFilterFavoritedRange.IsChecked = false;
                foreach (var fmenu in menus_fav_no)
                {
                    if (menus_fav_no.Contains(menu))
                    {
                        if (fmenu == menu) fmenu.IsChecked = !fmenu.IsChecked;
                        else fmenu.IsChecked = false;
                    }
                    if (fmenu.IsChecked)
                    {
                        filter_fav_no = fmenu.Name.Substring(idx);
                        if (fmenu.Name.StartsWith("LiveFilterFavorited_"))
                            LiveFilterFavoritedRange.IsChecked = true;
                    }
                }
                #endregion
                #region filter by fast simple filter
                LiveFilterFast.IsChecked = false;
                foreach (var fmenu in menus_fast)
                {
                    if (menus_fast.Contains(menu))
                    {
                        if (fmenu == menu) fmenu.IsChecked = !fmenu.IsChecked;
                        else fmenu.IsChecked = false;
                    }
                    if (fmenu.IsChecked)
                    {
                        var param = string.Empty;
                        filter_fast = $"{fmenu.Name.Substring(idx)}_{param}".Trim().TrimEnd('_');
                        if (fmenu.Name.StartsWith("LiveFilterFast_"))
                            LiveFilterFast.IsChecked = true;
                    }
                }
                #endregion
                #region filter by favorited state
                foreach (var fmenu in menus_fav)
                {
                    if (menus_fav.Contains(menu))
                    {
                        if (fmenu == menu) fmenu.IsChecked = !fmenu.IsChecked;
                        else fmenu.IsChecked = false;
                    }
                    if (fmenu.IsChecked) filter_fav = fmenu.Name.Substring(idx);
                }
                #endregion
                #region filter by followed state
                foreach (var fmenu in menus_follow)
                {
                    if (menus_follow.Contains(menu))
                    {
                        if (fmenu == menu) fmenu.IsChecked = !fmenu.IsChecked;
                        else fmenu.IsChecked = false;
                    }
                    if (fmenu.IsChecked) filter_follow = fmenu.Name.Substring(idx);
                }
                #endregion
                #region filter by downloaded state
                foreach (var fmenu in menus_down)
                {
                    if (menus_down.Contains(menu))
                    {
                        if (fmenu == menu) fmenu.IsChecked = !fmenu.IsChecked;
                        else fmenu.IsChecked = false;
                    }
                    if (fmenu.IsChecked) filter_down = fmenu.Name.Substring(idx);
                }
                #endregion
                #region filter by sanity state
                LiveFilterSanity.IsChecked = false;
                foreach (var fmenu in menus_sanity)
                {
                    if (menus_sanity.Contains(menu))
                    {
                        if (fmenu == menu) fmenu.IsChecked = !fmenu.IsChecked;
                        else fmenu.IsChecked = false;
                    }
                    if (fmenu.IsChecked)
                    {
                        filter_sanity = fmenu.Name.Substring(idx);
                        if (fmenu.Name.StartsWith("LiveFilterSanity_"))
                            LiveFilterSanity.IsChecked = true;
                    }
                }
                #endregion
            }

            var filter = new FilterParam()
            {
                Type = filter_type,
                FavoitedRange = filter_fav_no,
                Fast = filter_fast,
                Favorited = filter_fav,
                Followed = filter_follow,
                Downloaded = filter_down,
                Sanity = filter_sanity
            };


            if (Content is IllustDetailPage)
                (Content as IllustDetailPage).SetFilter(filter);
            else if (Content is SearchResultPage)
                (Content as SearchResultPage).SetFilter(filter);
            else if (Content is HistoryPage)
                (Content as HistoryPage).SetFilter(filter);
        }

        private void CommandFilter_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (Content is IllustDetailPage)
                CommandFilter.ToolTip = $"Tiles Count: {(Content as IllustDetailPage).GetTilesCount()}";
            else if (Content is SearchResultPage)
                CommandFilter.ToolTip = $"Tiles Count: {(Content as SearchResultPage).GetTilesCount()}";
            else if (Content is HistoryPage)
                CommandFilter.ToolTip = $"Tiles Count: {(Content as HistoryPage).GetTilesCount()}";
            else CommandFilter.ToolTip = $"Live Filter";
        }
    }
}
