using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using MahApps.Metro.Controls;
using PixivWPF.Pages;

namespace PixivWPF.Common
{
    /// <summary>
    /// ImageViewerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ContentWindow : MetroWindow
    {
        private Queue<WindowState> LastWindowStates { get; set; } = new Queue<WindowState>();
        public void RestoreWindowState()
        {
            if (LastWindowStates is Queue<WindowState> && LastWindowStates.Count > 0)
                WindowState = LastWindowStates.Dequeue();
        }

        public void SetDropBoxState(bool state)
        {
            CommandDropbox.IsChecked = state;
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

        public bool InSearching
        {
            get { return (SearchBox.IsKeyboardFocusWithin); }
            set
            {
                if (SearchBox.IsKeyboardFocusWithin && !value)
                {
                    SearchBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    Keyboard.ClearFocus();
                    if (Content is Page) (Content as Page).Focus();
                }
                else if (!SearchBox.IsKeyboardFocusWithin && value)
                {
                    SearchBox.Focus();
                    Keyboard.Focus(SearchBox);
                }
            }
        }

        private Storyboard PreftchingStateRing = null;
        public void SetPrefetchingProgress(double progress, string tooltip = "", TaskStatus state = TaskStatus.Created)
        {
            new Action(() =>
            {
                if (PreftchingProgress.IsHidden()) PreftchingProgress.Show();
                if (string.IsNullOrEmpty(tooltip)) PreftchingProgress.ToolTip = null;
                else PreftchingProgress.ToolTip = tooltip;

                PreftchingProgressInfo.Text = $"{Math.Floor(progress):F0}%";

                if (PreftchingStateRing == null) PreftchingStateRing = (Storyboard)PreftchingProgressState.FindResource("PreftchingStateRing");
                if (state == TaskStatus.Created)
                {
                    PreftchingProgressInfo.Hide();
                    PreftchingProgressState.Hide();
                }
                else if (state == TaskStatus.WaitingToRun)
                {
                    PreftchingProgressInfo.Show();
                    PreftchingProgressState.Show();
                    if (PreftchingStateRing != null) PreftchingStateRing.Begin();
                }
                else if (state == TaskStatus.Running)
                {
                    // do something
                }
                else
                {
                    if (PreftchingStateRing != null) PreftchingStateRing.Stop();
                    PreftchingProgressState.Hide();
                }
                if (progress < 0) PreftchingProgressInfo.Hide();
            }).Invoke(async: false);
        }

        private Storyboard RefreshRing = null;
        private CancellationTokenSource RefreshRingCancelSource = null;
        public void SetRefreshRing(bool rotate, CancellationTokenSource canceltoken = null)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (RefreshRing == null) RefreshRing = (Storyboard)RefreshIcon.FindResource("RefreshRing");
                    if (RefreshRing is Storyboard)
                    {
                        RefreshRingCancelSource = canceltoken;
                        if (rotate) RefreshRing.Begin();
                        else RefreshRing.Stop();
                    }
                }
                catch (Exception ex) { ex.ERROR("SetRefreshRing"); }
            });
        }

        public void SetToolTip(string element, string tooltip, bool append = false, string append_seprate = default(string))
        {
            this.BeginInvoke(() =>
            {
                try
                {
                    var control = this.FindByName<Control>(element);
                    if (control is Control)
                    {
                        control.ToolTip = append ? $"{control.ToolTip}{append_seprate}{tooltip}" : $"{tooltip}";
                    }
                    else
                    {
                        var child = this.FindVisualChild<Control>(control);
                        if (child is Control)
                        {
                            child.ToolTip = append ? $"{child.ToolTip}{append_seprate}{tooltip}" : $"{tooltip}";
                        }
                    }
                }
                catch (Exception ex) { ex.ERROR("ContentWindowSetToolTip"); }
            });
        }

        public void JumpTo(string id)
        {
            try
            {
                if (!string.IsNullOrEmpty(id))
                {
                    var win = this.GetMainWindow();
                    if (win is MainWindow && (win as MainWindow).Contents is TilesPage)
                    {
                        (win as MainWindow).Contents.JumpTo(id);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("RecentJumpTo"); }
        }

        public ContentWindow()
        {
            InitializeComponent();
            //this.GlowBrush = null;

            Title = $"{GetType().Name}_{GetHashCode()}";
            Application.Current.UpdateContentWindows(this);

            SearchBox.ItemsSource = AutoSuggestList;

            //Topmost = true;
            ShowActivated = true;
            //Activate();

            LastWindowStates.Enqueue(WindowState.Normal);
            //UpdateTheme(this);
        }

        public ContentWindow(string title)
        {
            InitializeComponent();
            //this.GlowBrush = null;

            Title = string.IsNullOrEmpty(title) ? $"{GetType().Name}_{GetHashCode()}" : title;
            Application.Current.UpdateContentWindows(this, Title);

            SearchBox.ItemsSource = AutoSuggestList;

            //Topmost = true;
            ShowActivated = true;
            //Activate();

            LastWindowStates.Enqueue(WindowState.Normal);
            UpdateTheme(this);
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //Application.Current.UpdateContentWindows(this);
            $"{Title} Loading...".INFO();

            CommandRefresh.Hide();
            CommandFilter.Hide();

            if (Content is BrowerPage)
            {
                CommandPageRead.Show();
                CommandRefreshThumb.ToolTip = "Refresh";
                PreftchingProgress.Hide();
            }
            else
                CommandPageRead.Hide();

            if (Content is IllustDetailPage ||
                Content is IllustImageViewerPage ||
                Content is SearchResultPage ||
                Content is HistoryPage)
            {
                CommandRefresh.Show();
                if (!(Content is IllustImageViewerPage))
                {
                    CommandRefreshThumb.Show();
                    PreftchingProgress.Show();
                    CommandFilter.Show();
                }

                RoutedCommand cmd_Escape = new RoutedCommand();
                cmd_Escape.InputGestures.Add(new KeyGesture(Key.Escape, ModifierKeys.None, Key.Escape.ToString()));
                CommandBindings.Add(new CommandBinding(cmd_Escape, (obj, evt) =>
                {
                    evt.Handled = true;
                    if (Content is IllustDetailPage) (Content as IllustDetailPage).StopPrefetching();
                    else if (Content is IllustImageViewerPage) (Content as IllustImageViewerPage).StopPrefetching();
                    else if (Content is SearchResultPage) (Content as SearchResultPage).StopPrefetching();
                    else if (Content is HistoryPage) (Content as HistoryPage).StopPrefetching();
                }));
            }

            if (Content is BatchProcessPage)
            {
                LeftWindowCommands.Hide();
                RightWindowCommands.Hide();
                ShowMinButton = true;
                ShowMaxRestoreButton = false;
                ResizeMode = ResizeMode.CanMinimize;
            }

            if (Application.Current.DropBoxExists() == null)
                CommandDropbox.IsChecked = false;
            else
                CommandDropbox.IsChecked = true;

            this.AdjustWindowPos();

            Commands.SaveOpenedWindows.Execute(null);
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (Application.Current.GetLoginWindow() != null) { e.Cancel = true; return; }

                this.WindowState = WindowState.Minimized;
                //this.Hide();

                //foreach (var c in this.GetChildren<Control>())
                //{
                //    try
                //    {
                //        if ((c as Control).ToolTip != null)
                //        {
                //           new Action(() => {
                //                ToolTipService.SetIsEnabled(c as Control, false);
                //                (c as Control).ToolTip = null;
                //            }).Invoke();
                //        }
                //    }
                //    catch (Exception ex) { ex.ERROR("MetroWindow_Closing_RemoveToolTip"); }
                //}

                if (Content is DownloadManagerPage)
                    (Content as DownloadManagerPage).Pos = new Point(this.Left, this.Top);
                else if (Content is HistoryPage)
                    (Content as HistoryPage).Pos = new Point(this.Left, this.Top);

                if (Content is IllustDetailPage)
                {
                    if ((Content as IllustDetailPage).Contents.HasUser()) (Content as IllustDetailPage).Contents.AddToHistory();
                    (Content as IllustDetailPage).Dispose();
                }
                else if (Content is IllustImageViewerPage)
                {
                    if ((Content as IllustImageViewerPage).Contents.HasUser()) (Content as IllustImageViewerPage).Contents.AddToHistory();
                    (Content as IllustImageViewerPage).Dispose();
                }
                else if (Content is HistoryPage)
                    (Content as HistoryPage).Dispose();
                else if (Content is SearchResultPage)
                    (Content as SearchResultPage).Dispose();
                else if (Title.Equals(Application.Current.DropboxTitle(), StringComparison.CurrentCultureIgnoreCase))
                {
                    if (Content is Image) (Content as Image).Dispose();
                    false.SetDropBoxState();
                }

                if (Content is Page)
                {
                    (Content as Page).DataContext = null;
                }
            }
            catch (Exception ex) { ex.ERROR("CLOSEWIN"); }
            finally
            {
                if (!e.Cancel)
                {
                    var name = Content is Page ? (Content as Page).Name ?? (Content as Page).GetType().Name : Title;
                    Content = null;
                    Application.Current.GC(name: name, wait: Application.Current.LoadSetting().WaitGC);
                }
                Application.Current.RemoveContentWindows(this);
            }
        }

        private void MetroWindow_StateChanged(object sender, EventArgs e)
        {
            LastWindowStates.Enqueue(WindowState);
            if (LastWindowStates.Count > 2) LastWindowStates.Dequeue();
        }

        private void MetroWindow_Activated(object sender, EventArgs e)
        {
            //Application.Current.ReleaseKeyboardModifiers(force: false, use_keybd_event: true);
            Application.Current.ReleaseKeyboardModifiers(force: false, use_sendkey: true);
            this.DoEvents();
        }

        private void MetroWindow_Deactivated(object sender, EventArgs e)
        {
            //Application.Current.ReleaseKeyboardModifiers(updown: true);
            this.DoEvents();
        }

        private void MetroWindow_DragOver(object sender, DragEventArgs e)
        {
            var fmts = new List<string>(e.Data.GetFormats(true));
            if (fmts.Contains("Text") || fmts.Contains("FileDrop") || fmts.Contains("PixivItems"))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else e.Effects = DragDropEffects.None;
        }

        private void MetroWindow_Drop(object sender, DragEventArgs e)
        {
            var fmts = new List<string>(e.Data.GetFormats(true));
            if (fmts.Contains("PixivItems"))
            {
                var items = Clipboard.GetData("PixivItems") as IEnumerable<PixivItem>;
                Commands.OpenItem.Execute(items);
            }
            else
            {
                var links = e.ParseDragContent();
                if (Commands.MultipleOpeningConfirm(links))
                {
                    foreach (var link in links)
                    {
                        Commands.OpenSearch.Execute(link);
                    }
                }
            }
        }

        private void MetroWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
            if (e.ChangedButton == MouseButton.Middle)
            {
                e.Handled = true;
                if (Title.Equals(Application.Current.DownloadTitle(), StringComparison.CurrentCultureIgnoreCase))
                {
                    Hide();
                }
                else
                {
                    Close();
                }
            }
        }

        private void cmiProxyAction_Opened(object sender, RoutedEventArgs e)
        {
            try
            {
                var setting = Application.Current.LoadSetting();
                cmiUseProxy.IsChecked = setting.UsingProxy;
                cmiUseProxyDown.IsChecked = setting.DownloadUsingProxy;

                cmiUseHttp10.IsChecked = false;
                cmiUseHttp11.IsChecked = false;
                cmiUseHttp20.IsChecked = false;

                var http_ver = setting.HttpVersion;
                if (http_ver.Major == 1 && http_ver.Minor == 0) cmiUseHttp10.IsChecked = true;
                else if (http_ver.Major == 1 && http_ver.Minor == 1) cmiUseHttp11.IsChecked = true;
                else if (http_ver.Major == 2 && http_ver.Minor == 0) cmiUseHttp20.IsChecked = true;
            }
            catch (Exception ex) { ex.ERROR("cmiProxyAction_Opened"); }
        }

        private void cmiProxyAction_Closed(object sender, RoutedEventArgs e)
        {
            try
            {
                var setting = Application.Current.LoadSetting();
                setting.UsingProxy = cmiUseProxy.IsChecked;
                setting.DownloadUsingProxy = cmiUseProxyDown.IsChecked;

                var http_ver = new Version(1, 1);
                if (cmiUseHttp10.IsChecked ?? false) http_ver = new Version(1, 0);
                else if (cmiUseHttp11.IsChecked ?? false) http_ver = new Version(1, 1);
                else if (cmiUseHttp20.IsChecked ?? false) http_ver = new Version(2, 0);
                setting.HttpVersion = http_ver;
            }
            catch (Exception ex) { ex.ERROR("cmiProxyAction_Opened"); }
        }

        private void PreftchingProgress_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            //if (e.ButtonState == Mouse.RightButton)
            {
                e.Handled = true;
                if (Content is IllustDetailPage)
                    (Content as IllustDetailPage).StopPrefetching();
                else if (Content is IllustImageViewerPage)
                    (Content as IllustImageViewerPage).StopPrefetching();
                else if (Content is SearchResultPage)
                    (Content as SearchResultPage).StopPrefetching();
                else if (Content is HistoryPage)
                    (Content as HistoryPage).StopPrefetching();
            }
        }

        private void CommandRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (Mouse.RightButton == MouseButtonState.Pressed) return;
            if (sender == CommandRefresh)
            {
                e.Handled = true;
                Commands.RefreshPage.Execute(Content);
            }
            else if (sender == CommandRefreshThumb)
            {
                e.Handled = true;
                Commands.RefreshPageThumb.Execute(Content);
            }
        }

        private void CommandRefresh_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (RefreshRingCancelSource is CancellationTokenSource)
            {
                "Request Canceled".INFO(tag: "RefreshToken");
                RefreshRingCancelSource.Cancel();
                e.Handled = true;
            }
            else { "Request Canceling Failed".WARN(tag: "RefreshToken"); }
        }

        private void CommandRecents_Click(object sender, RoutedEventArgs e)
        {
            var setting = Application.Current.LoadSetting();
            var recents = Application.Current.HistoryRecentIllusts(setting.MostRecents);
            RecentsList.Items.Clear();
            //var contents = recents.Select(item => $"ID: {item.ID}, {item.Illust.Title}").ToList();
            foreach (var item in recents)
            {
                RecentsList.Items.Add($"ID: {item.ID}, {new string(item.Illust.Title.Take(32).ToArray())} ");
            }
            RecentsPopup.IsOpen = true;
        }

        private void CommandRecentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var item = e.AddedItems[0];
                if (item is string)
                {
                    var contents = item as string;
                    var id = Regex.Replace(contents, @"ID:\s*?(\d+),.*?$", "$1", RegexOptions.IgnoreCase);
                    JumpTo(id);
                }
                RecentsPopup.IsOpen = false;
            }
        }

        private void CommandPageRead_Click(object sender, RoutedEventArgs e)
        {
            if (Content is BrowerPage)
            {
                (Content as BrowerPage).ReadText();
            }
        }

        private void CommandLogin_Click(object sender, RoutedEventArgs e)
        {
            Commands.Login.Execute(sender);
        }

        private void CommandLog_Click(object sender, RoutedEventArgs e)
        {
            var log_type = string.Empty;
            if (sender == CommandLog_Info) log_type = "INFO";
            else if (sender == CommandLog_Warn) log_type = "WARN";
            else if (sender == CommandLog_Debug) log_type = "DEBUG";
            else if (sender == CommandLog_Error) log_type = "ERROR";
            else if (sender == CommandLog_Folder) log_type = "FOLDER";
            Commands.OpenLogs.Execute(log_type);
        }

        private void CommandLog_DropDownOpened(object sender, EventArgs e)
        {
            CommandLog.ContextMenu.IsOpen = true;
        }

        private void CommandDownloadManager_Click(object sender, RoutedEventArgs e)
        {
            Commands.OpenDownloadManager.Execute(true);
        }

        private void CommandDownloadManager_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Commands.OpenDownloadManager.Execute(false);
        }

        private void CommandDropbox_Click(object sender, RoutedEventArgs e)
        {
            Commands.OpenDropBox.Execute(sender);
        }

        private void CommandDropbox_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
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

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SearchBox.IsDropDownOpen = false;
        }

        private void SearchBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text.Length > 0)
            {
                auto_suggest_list.Clear();

                var content = SearchBox.Text.ParseLink().ParseID();
                if (!string.IsNullOrEmpty(content))
                {
                    content.GetSuggestList(SearchBox.Text).ToList().ForEach(t => auto_suggest_list.Add(t));
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

        private void LiveFilter_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (Content is IllustDetailPage)
                CommandFilter.ToolTip = $"{(Content as IllustDetailPage).GetTilesCount()}";
            else if (Content is SearchResultPage)
                CommandFilter.ToolTip = $"{(Content as SearchResultPage).GetTilesCount()}";
            else if (Content is HistoryPage)
                CommandFilter.ToolTip = $"{(Content as HistoryPage).GetTilesCount()}";
            else CommandFilter.ToolTip = $"Live Filter";
        }

        private void LiveFilter_Click(object sender, RoutedEventArgs e)
        {
            if (LiveFilterSanity_OptIncludeUnder.IsChecked)
            {
                LiveFilterSanity_NoR18.IsChecked = LiveFilterSanity_R18.IsChecked = false;
                LiveFilterSanity_NoR18.IsEnabled = LiveFilterSanity_R18.IsEnabled = false;
            }
            else
            {
                LiveFilterSanity_NoR18.IsEnabled = LiveFilterSanity_R18.IsEnabled = true;
            }
        }

        private void LiveFilterItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem)) return;
            if (sender == LiveFilterFavoritedRange) return;

            #region pre-define filter menus list
            var menus_type = new List<MenuItem>() {
                LiveFilterUser, LiveFilterWork
            };
            var menus_fast = new List<MenuItem>() {
                LiveFilterFast_None,
                LiveFilterFast_Portrait, LiveFilterFast_Landscape, LiveFilterFast_Square,
                LiveFilterFast_Size1K, LiveFilterFast_Size2K, LiveFilterFast_Size4K, LiveFilterFast_Size8K,
                LiveFilterFast_SinglePage, LiveFilterFast_NotSinglePage,
                LiveFilterFast_InHistory, LiveFilterFast_NotInHistory,
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
            var menus_ai = new List<MenuItem>() {
                LiveFilterAIGC, LiveFilterAIAD, LiveFilterNoAI,
            };
            var menus_sanity = new List<MenuItem>() {
                LiveFilterSanity_Any,
                LiveFilterSanity_All, LiveFilterSanity_NoAll,
                LiveFilterSanity_R12, LiveFilterSanity_NoR12,
                LiveFilterSanity_R15, LiveFilterSanity_NoR15,
                LiveFilterSanity_R17, LiveFilterSanity_NoR17,
                LiveFilterSanity_R18, LiveFilterSanity_NoR18,
            };

            var menus = new List<IEnumerable<MenuItem>>() { menus_type, menus_fav_no, menus_fast, menus_fav, menus_follow, menus_down, menus_ai, menus_sanity };
            #endregion

            var idx = "LiveFilter".Length;

            string filter_type = string.Empty;
            string filter_fav_no = string.Empty;
            string filter_fast = string.Empty;
            string filter_fav = string.Empty;
            string filter_follow = string.Empty;
            string filter_down = string.Empty;
            string filter_ai = string.Empty;
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
                if (LiveFilterFavorited_00000.IsChecked) LiveFilterFavoritedRange.IsChecked = false;
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
                if (LiveFilterFast_None.IsChecked) LiveFilterFast.IsChecked = false;
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
                #region filter by ai state
                foreach (var fmenu in menus_ai)
                {
                    if (menus_ai.Contains(menu))
                    {
                        if (fmenu == menu) fmenu.IsChecked = !fmenu.IsChecked;
                        else fmenu.IsChecked = false;
                    }
                    if (fmenu.IsChecked) filter_ai = fmenu.Name.Substring(idx);
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
                if (LiveFilterSanity_OptIncludeUnder.IsChecked)
                {
                    LiveFilterSanity_NoR18.IsChecked = LiveFilterSanity_R18.IsChecked = false;
                    LiveFilterSanity_NoR18.IsEnabled = LiveFilterSanity_R18.IsEnabled = false;
                }
                else
                {
                    LiveFilterSanity_NoR18.IsEnabled = LiveFilterSanity_R18.IsEnabled = true;
                }
                if (LiveFilterSanity_Any.IsChecked) LiveFilterSanity.IsChecked = false;
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
                AI = filter_ai,
                Sanity = filter_sanity,
                SanityOption_IncludeUnder = LiveFilterSanity_OptIncludeUnder.IsChecked
            };

            if (Content is IllustDetailPage)
                (Content as IllustDetailPage).SetFilter(filter);
            else if (Content is SearchResultPage)
                (Content as SearchResultPage).SetFilter(filter);
            else if (Content is HistoryPage)
                (Content as HistoryPage).SetFilter(filter);
        }

    }
}
