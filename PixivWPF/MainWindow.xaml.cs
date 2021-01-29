using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;

using MahApps.Metro.Controls;
using PixivWPF.Common;

namespace PixivWPF
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private Setting setting = Application.Current.LoadSetting();
        private Queue<WindowState> LastWindowStates { get; set; } = new Queue<WindowState>();
        public void RestoreWindowState()
        {
            if (LastWindowStates is Queue<WindowState> && LastWindowStates.Count > 0)
                WindowState = LastWindowStates.Dequeue();
        }

        public Pages.TilesPage Contents { get; set; } = null;

        public DateTime RankingDate { get; set; } = DateTime.Now;

        private ObservableCollection<string> auto_suggest_list = new ObservableCollection<string>() {"a", "b" };
        public ObservableCollection<string> AutoSuggestList
        {
            get { return (auto_suggest_list); }
        }

        private DateTime LastSelectedDate = DateTime.Now;

        public void SetDropBoxState(bool state)
        {
            CommandDropbox.IsChecked = state;
        }

        public void UpdateIllustTagsAsync()
        {
            if (Contents is Pages.TilesPage) Contents.UpdateIllustTags();
        }

        public void UpdateIllustDescAsync()
        {
            if (Contents is Pages.TilesPage) Contents.UpdateIllustDesc();
        }

        public void UpdateWebContentAsync()
        {
            if (Contents is Pages.TilesPage) Contents.UpdateWebContent();
        }

        public void UpdateTheme()
        {
            if (Contents is Pages.TilesPage) Contents.UpdateTheme();
        }

        public void UpdateTitle(string title)
        {
            if (title.StartsWith("Ranking", StringComparison.CurrentCultureIgnoreCase))
            {
                NavPageTitle.Text = $"{title}[{Contents.SelectedDate.ToString("yyyy-MM-dd")}]";
                CommandNavDate.IsEnabled = true;
            }
            else if (title.Equals("My"))
            {

            }
            else
            {
                NavPageTitle.Text = title;
                CommandNavDate.IsEnabled = false;
            }
        }

        public void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            if (Contents is Pages.TilesPage)
            {
                Contents.UpdateDownloadStateAsync(illustid, exists);
                if (Contents.IllustDetail.Content is Pages.IllustDetailPage)
                {
                    var detail = Contents.IllustDetail.Content as Pages.IllustDetailPage;
                    detail.UpdateDownloadStateAsync(illustid, exists);
                }
            }
        }

        public void UpdateLikeState(int illustid = -1, bool is_user = false)
        {
            if (Contents is Pages.TilesPage)
            {
                Contents.UpdateLikeStateAsync(illustid, is_user);
                if (Contents.IllustDetail.Content is Pages.IllustDetailPage)
                {
                    var detail = Contents.IllustDetail.Content as Pages.IllustDetailPage;
                    detail.UpdateLikeStateAsync(illustid, is_user);
                }
            }
        }

        public void RefreshPage()
        {
            if (Contents is Pages.TilesPage)
            {
                UpdateTitle(Contents.TargetPage.ToString());
                Contents.RefreshPage();
            }
        }

        public void RefreshThumbnail()
        {
            if (Contents is Pages.TilesPage) Contents.RefreshThumbnail();
        }

        public void AppendTiles()
        {
            if (Contents is Pages.TilesPage) Contents.AppendTiles();
        }

        public void OpenIllust()
        {
            if (Contents is Pages.TilesPage) Contents.OpenIllust();
        }

        public void OpenWork()
        {
            if (Contents is Pages.TilesPage) Contents.OpenWork();
        }

        public void OpenUser()
        {
            if (Contents is Pages.TilesPage) Contents.OpenUser();
        }

        public void SaveIllust()
        {
            if (Contents is Pages.TilesPage) Contents.SaveIllust();
        }

        public void SaveIllustAll()
        {
            if (Contents is Pages.TilesPage) Contents.SaveIllustAll();
        }

        public void CopyPreview()
        {
            if (Contents is Pages.TilesPage) Contents.CopyPreview();
        }

        public void JumpTo(string id)
        {
            try
            {
                if (!string.IsNullOrEmpty(id))
                {
                    if (Contents is Pages.TilesPage)
                    {
                        Contents.JumpTo(id);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public void FirstIllust()
        {
            if (InSearching) return;
            if (Contents is Pages.TilesPage) Contents.FirstIllust();
        }

        public void LastIllust()
        {
            if (InSearching) return;
            if (Contents is Pages.TilesPage) Contents.LastIllust();
        }

        public void PrevIllust()
        {
            if (InSearching) return;
            if (Contents is Pages.TilesPage) Contents.PrevIllust();
        }

        public void NextIllust()
        {
            if (InSearching) return;
            if (Contents is Pages.TilesPage) Contents.NextIllust();
        }

        public void PrevIllustPage()
        {
            if (InSearching) return;
            if (Contents is Pages.TilesPage) Contents.PrevIllustPage();
        }

        public void NextIllustPage()
        {
            if (InSearching) return;
            if (Contents is Pages.TilesPage) Contents.NextIllustPage();
        }

        #region Named Pipe Heler
        private NamedPipeServerStream pipeServer;
        private string pipeName = Application.Current.PipeServerName();
        private bool pipeOnClosing = false;
        private bool CreateNamedPipeServer()
        {
            try
            {
                ReleaseNamedPipeServer();
                pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                //pipeServer.WaitForConnectionAsync();
                pipeServer.BeginWaitForConnection(PipeReceiveData, pipeServer);
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR!");
            }

            return (true);
        }

        private bool ReleaseNamedPipeServer()
        {
            if (pipeServer is NamedPipeServerStream)
            {
                try
                {
                    if (pipeServer.IsConnected) pipeServer.Disconnect();
                }
                catch { }
                try
                {
                    pipeServer.Close();
                }
                catch { }
                try
                {
                    pipeServer.Dispose();
                }
                catch { }
                pipeServer = null;
            }
            return (true);
        }

        private async void PipeReceiveData(IAsyncResult result)
        {
            try
            {
                NamedPipeServerStream ps = (NamedPipeServerStream)result.AsyncState;
                if (pipeOnClosing) return;
                ps.EndWaitForConnection(result);

                using (StreamReader sw = new StreamReader(ps))
                {
                    var contents = sw.ReadToEnd().Trim();
                    $"RECEIVED => {contents}".INFO();
                    if (contents.StartsWith("cmd:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Application.Current.ProcessCommand(contents);
                    }
                    else
                    {
                        var links = contents.ParseLinks();
                        foreach (var link in links)
                        {
                            await new Action(() =>
                            {
                                if (link.StartsWith("down:"))
                                    Commands.SaveIllust.Execute(link.Substring(5));
                                else if (link.StartsWith("downall:"))
                                    Commands.SaveIllustAll.Execute(link.Substring(8));
                                else
                                    Commands.OpenSearch.Execute(link);
                            }).InvokeAsync();
                        }
                    }
                }

                if (ps.IsConnected) ps.Disconnect();
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[PIPE]!");
            }
            finally
            {
                if (pipeServer is NamedPipeServerStream && !pipeOnClosing) CreateNamedPipeServer();
            }
        }
        #endregion

        public bool InSearching { get { return (SearchBox.IsKeyboardFocusWithin); } }

        public MainWindow()
        {
            InitializeComponent();

            DPI.GetDefault(this);

            setting = Application.Current.LoadSetting();

            FontFamily = setting.FontFamily;

            Title = $"{Title} [Version: {Application.Current.Version()}]";

            SearchBox.ItemsSource = AutoSuggestList;

            #region Themem Init.
            CommandToggleTheme.ItemsSource = Application.Current.GetAccentColorList();
            CommandToggleTheme.SelectedIndex = Application.Current.GetAccentIndex();
            #endregion

            #region DatePicker Init.
            DatePicker.DisplayMode = CalendarMode.Month;
            DatePicker.FirstDayOfWeek = DayOfWeek.Monday;
            DatePicker.IsTodayHighlighted = true;
            DatePicker.SelectedDate = DateTime.Now;
            DatePicker.DisplayDate = DateTime.Now;
            DatePicker.DisplayDateStart = new DateTime(2007, 09, 11);
            DatePicker.DisplayDateEnd = DateTime.Now;
            DatePicker.Language = System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentCulture.IetfLanguageTag);
            #endregion

            Contents = new Pages.TilesPage() { FontFamily = FontFamily };
            Content = Contents;

            NavPageTitle.Text = Contents.TargetPage.ToString();

            LastWindowStates.Enqueue(WindowState.Normal);

            CreateNamedPipeServer();
        }

#if DEBUG
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            pipeOnClosing = true;
            ReleaseNamedPipeServer();
            Application.Current.ReleaseAppWatcher();
            Application.Current.LoadSetting().LocalStorage.ReleaseDownloadedWatcher();

            if (setting is Setting) setting.Save(true);

            foreach (Window win in Application.Current.Windows)
            {
                if (win == this) continue;
                win.Close();
            }
            Application.Current.Shutdown();
        }
#else
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            setting = Application.Current.LoadSetting();
            var ret = setting.NoConfirmExit ? true : MessageBox.Show("Continue Exit?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.Yes;
            if (ret)
            {
                pipeOnClosing = true;
                ReleaseNamedPipeServer();
                Application.Current.ReleaseAppWatcher();
                Application.Current.LoadSetting().LocalStorage.ReleaseDownloadedWatcher();

                if (setting is Setting) setting.Save(true);

                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MainWindow) continue;
                    else win.Close();
                }
                Application.Current.Shutdown();
            }
            else e.Cancel = true;
        }
#endif

        private void MainWindow_DragOver(object sender, DragEventArgs e)
        {
            var fmts = e.Data.GetFormats(true);
            if (new List<string>(fmts).Contains("Text"))
            {
                e.Effects = DragDropEffects.Link;
            }
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
        {
            var links = e.ParseDragContent();
            foreach (var link in links)
            {
                Commands.OpenSearch.Execute(link);
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            LastWindowStates.Enqueue(WindowState);
            if (LastWindowStates.Count > 2) LastWindowStates.Dequeue();
            if (Keyboard.Modifiers == ModifierKeys.Shift && WindowState == WindowState.Minimized)
                Application.Current.RebindHotKeys();
            Application.Current.ReleaseModifiers(all:true, updown:true);
        }

        private void RecentsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

        private void DatePicker_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            //if (Contents is Pages.TilesPage && DatePicker.SelectedDate.HasValue && DatePicker.SelectedDate.Value <= DateTime.Now)
            //    Contents.SelectedDate = DatePicker.SelectedDate.Value;
        }

        private void DatePicker_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) DatePicker.DisplayDate -= TimeSpan.FromDays(30);
            else if (e.Delta < 0) DatePicker.DisplayDate += TimeSpan.FromDays(30);
        }

        private void DatePicker_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (DatePicker.SelectedDate.HasValue && DatePicker.SelectedDate.Value <= DateTime.Now)
                {
                    Contents.SelectedDate = DatePicker.SelectedDate.Value;
                    DatePickerPopup.IsOpen = false;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        private void DatePickerPopup_Closed(object sender, EventArgs e)
        {
            var title = Contents.TargetPage.ToString();
            if (title.StartsWith("Ranking", StringComparison.CurrentCultureIgnoreCase))
            {
                if (LastSelectedDate.Year != Contents.SelectedDate.Year ||
                LastSelectedDate.Month != Contents.SelectedDate.Month ||
                LastSelectedDate.Day != Contents.SelectedDate.Day)
                {
                    LastSelectedDate = Contents.SelectedDate;
                    NavPageTitle.Text = $"{title}[{Contents.SelectedDate.ToString("yyyy-MM-dd")}]";
                    Contents.ShowImages(Contents.TargetPage, false, Contents.GetLastSelectedID());
                }
            }
        }

        private void CommandToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.ToggleTheme();
        }

        private void CommandToggleTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CommandToggleTheme.SelectedIndex >= 0 && CommandToggleTheme.SelectedIndex < CommandToggleTheme.Items.Count)
            {
                var item = CommandToggleTheme.SelectedItem;
                if (item is SimpleAccent)
                    Application.Current.SetAccent((item as SimpleAccent).AccentName);
            }
        }

        private void CommandLogin_Click(object sender, RoutedEventArgs e)
        {
            Commands.Login.Execute(sender);
        }

        internal void CommandNavRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender == CommandNavRefresh)
                {
                    RefreshPage();
                }
                else if (sender == CommandNavRefreshThumb)
                {
                    RefreshThumbnail();
                }
            }
            catch { }
        }

        private void CommandNavRecents_Click(object sender, RoutedEventArgs e)
        {
            setting = Application.Current.LoadSetting();
            var recents = Application.Current.HistoryRecentIllusts(setting.MostRecents);
            RecentsList.Items.Clear();
            //var contents = recents.Select(item => $"ID: {item.ID}, {item.Illust.Title}").ToList();
            foreach (var item in recents)
            {
                RecentsList.Items.Add($"ID: {item.ID}, {new string(item.Illust.Title.Take(32).ToArray())} ");
            }
            RecentsPopup.IsOpen = true;
        }

        private void CommandNavDate_Click(object sender, RoutedEventArgs e)
        {
            var title = Contents.TargetPage.ToString();
            if (title.StartsWith("Ranking", StringComparison.CurrentCultureIgnoreCase))
            {
                DatePickerPopup.IsOpen = true;
                ////var point = CommandNavDate.PointToScreen(Mouse.GetPosition(CommandNavDate));
                //var point = CommandNavDate.PointToScreen(new Point(0, CommandNavDate.ActualHeight));
                ////var point = CommandNavDate.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0,0));
                //Commands.DatePicker.Execute(point);
            }
        }

        internal void CommandNavNext_Click(object sender, RoutedEventArgs e)
        {
            AppendTiles();
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

        private void LiveFilter_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (Contents is Pages.TilesPage)
                CommandFilter.ToolTip = $"{Contents.GetTilesCount()}";
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
            var menus_sanity = new List<MenuItem>() {
                LiveFilterSanity_Any,
                LiveFilterSanity_All, LiveFilterSanity_NoAll,
                LiveFilterSanity_R12, LiveFilterSanity_NoR12,
                LiveFilterSanity_R15, LiveFilterSanity_NoR15,
                LiveFilterSanity_R17, LiveFilterSanity_NoR17,
                LiveFilterSanity_R18, LiveFilterSanity_NoR18,
            };

            var menus = new List<IEnumerable<MenuItem>>() { menus_type, menus_fav_no, menus_fast, menus_fav, menus_follow, menus_down, menus_sanity };
            #endregion

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
                if (LiveFilterSanity_OptIncludeUnder.IsChecked)
                {
                    LiveFilterSanity_NoR18.IsChecked = LiveFilterSanity_R18.IsChecked = false;
                    LiveFilterSanity_NoR18.IsEnabled = LiveFilterSanity_R18.IsEnabled = false;
                }
                else
                {
                    LiveFilterSanity_NoR18.IsEnabled = LiveFilterSanity_R18.IsEnabled = true;
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
                Sanity = filter_sanity,
                SanityOption_IncludeUnder = LiveFilterSanity_OptIncludeUnder.IsChecked
            };

            if (Contents is Pages.TilesPage) Contents.SetFilter(filter);
        }
    }
}