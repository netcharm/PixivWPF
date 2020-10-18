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
        private static System.Diagnostics.Process CurrentProcess = Application.Current.Process();

        private Setting setting = Application.Current.LoadSetting();
        public Queue<WindowState> LastWindowStates { get; set; } = new Queue<WindowState>();

        public Frame MainContent = null;

        private Pages.TilesPage pagetiles = null;

        private ObservableCollection<string> auto_suggest_list = new ObservableCollection<string>() {"a", "b" };
        public ObservableCollection<string> AutoSuggestList
        {
            get { return (auto_suggest_list); }
        }

        private DateTime LastSelectedDate = DateTime.Now;

        public void SetDropBoxState(bool state)
        {
            CommandToggleDropbox.IsChecked = state;
        }

        public void UpdateIllustTagsAsync()
        {
            if (ContentFrame.Content is Pages.TilesPage)
            {
                var tiles = ContentFrame.Content as Pages.TilesPage;
                tiles.UpdateIllustTags();
            }
        }

        public void UpdateIllustDescAsync()
        {
            if (ContentFrame.Content is Pages.TilesPage)
            {
                var tiles = ContentFrame.Content as Pages.TilesPage;
                tiles.UpdateIllustDesc();
            }
        }

        public void UpdateWebContentAsync()
        {
            if (ContentFrame.Content is Pages.TilesPage)
            {
                var tiles = ContentFrame.Content as Pages.TilesPage;
                tiles.UpdateWebContent();
            }
        }

        public void UpdateTheme()
        {
            //if (pagenav is Pages.NavPage) pagenav.CheckPage();
            if (pagetiles is Pages.TilesPage) pagetiles.UpdateTheme();
        }

        public void UpdateTitle(string title)
        {
            if (title.StartsWith("Ranking", StringComparison.CurrentCultureIgnoreCase))
            {
                NavPageTitle.Text = $"{title}[{CommonHelper.SelectedDate.ToString("yyyy-MM-dd")}]";
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
            if (ContentFrame.Content is Pages.TilesPage)
            {
                var tiles = ContentFrame.Content as Pages.TilesPage;
                tiles.UpdateDownloadStateAsync(illustid, exists);
                if (tiles.IllustDetail.Content is Pages.IllustDetailPage)
                {
                    var detail = tiles.IllustDetail.Content as Pages.IllustDetailPage;
                    detail.UpdateDownloadStateAsync(illustid, exists);
                }
            }
        }

        public void UpdateLikeState(int illustid = -1, bool is_user = false)
        {
            if (ContentFrame.Content is Pages.TilesPage)
            {
                var tiles = ContentFrame.Content as Pages.TilesPage;
                tiles.UpdateLikeStateAsync(illustid, is_user);
                if (tiles.IllustDetail.Content is Pages.IllustDetailPage)
                {
                    var detail = tiles.IllustDetail.Content as Pages.IllustDetailPage;
                    detail.UpdateLikeStateAsync(illustid, is_user);
                }
            }
        }

        public void PrevIllust()
        {
            if(pagetiles is Pages.TilesPage)
            {
                pagetiles.PrevIllust();
            }
        }

        public void NextIllust()
        {
            if (pagetiles is Pages.TilesPage)
            {
                pagetiles.NextIllust();
            }
        }

        public void PrevIllustPage()
        {
            if (pagetiles is Pages.TilesPage)
            {
                pagetiles.PrevIllustPage();
            }
        }

        public void NextIllustPage()
        {
            if (pagetiles is Pages.TilesPage)
            {
                pagetiles.NextIllustPage();
            }
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
                    //sw.ReadToEnd().ShowMessageDialog("RECEIVED!");
                    var contents = sw.ReadToEnd().Trim();
                    if (contents.StartsWith("cmd:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var kv = contents.Substring(4).Split(new char[] { '-', '_', ':', '+', '=' });
                        var action = kv[0];
                        var param = kv.Length == 2 ? kv[1] : "r18";
                        if (action.StartsWith("min", StringComparison.CurrentCultureIgnoreCase))
                        {
                            Application.Current.MinimizedWindows(param);
                        }
                    }
                    else
                    {
                        var links = contents.ParseLinks();
                        foreach (var link in links)
                        {
                            await new Action(() =>
                            {
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

        public MainWindow()
        {
            InitializeComponent();

            setting = Application.Current.LoadSetting();

            FontFamily = setting.FontFamily;

            Title = $"{Title} [Version: {Application.Current.Version()}]";

            SearchBox.ItemsSource = AutoSuggestList;
#if DEBUG
            //Application.Current.SetThemeSync();
#endif
            CommandToggleTheme.ItemsSource = Application.Current.GetAccentColorList();
            CommandToggleTheme.SelectedIndex = Application.Current.GetAccentIndex();

            MainContent = ContentFrame;

            pagetiles = new Pages.TilesPage() { FontFamily = FontFamily, Tag = ContentFrame };

            ContentFrame.Content = pagetiles;

            ContentFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;

            NavPageTitle.Text = pagetiles.TargetPage.ToString();

            LastWindowStates.Enqueue(WindowState.Normal);

            Application.Current.InitAppWatcher(Application.Current.GetRoot());

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
            var ret = setting.NoConfirmExit;
            if (!ret) ret = MessageBox.Show("Continue Exit?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.Yes;
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

        private long lastKeyUp = Environment.TickCount;
        private void MetroWindow_KeyUp(object sender, KeyEventArgs e)
        {
            Commands.KeyProcessor.Execute(new KeyValuePair<dynamic, KeyEventArgs>(sender, e));
        }

        private void MetroWindow_StateChanged(object sender, EventArgs e)
        {
            LastWindowStates.Enqueue(this.WindowState);
            if (LastWindowStates.Count > 2) LastWindowStates.Dequeue();
        }

#if DEBUG
        private async void MetroWindow_StylusUp(object sender, StylusEventArgs e)
        {
            //
            await Task.Delay(1);
            this.DoEvents();
        }
#else
        private void MetroWindow_StylusUp(object sender, StylusEventArgs e)
        {
            this.DoEvents();
        }
#endif

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
                    UpdateTitle(pagetiles.TargetPage.ToString());
                    pagetiles.ShowImages(pagetiles.TargetPage, false, pagetiles.GetLastSelectedID());
                    CommandFilter.ToolTip = $"Tiles Count: {pagetiles.GetTilesCount()}";
                }
                else if (sender == CommandNavRefreshThumb)
                {
                    pagetiles.UpdateTilesThumb();
                }
            }
            catch { }
        }

        private void CommandNavDate_Click(object sender, RoutedEventArgs e)
        {
            var title = pagetiles.TargetPage.ToString();
            if (title.StartsWith("Ranking", StringComparison.CurrentCultureIgnoreCase))
            {
                //var point = CommandNavDate.PointToScreen(Mouse.GetPosition(CommandNavDate));
                var point = CommandNavDate.PointToScreen(new Point(0, CommandNavDate.ActualHeight));
                //var point = CommandNavDate.TransformToAncestor(Application.Current.MainWindow).Transform(new Point(0,0));
                Commands.DatePicker.Execute(point);

                if (LastSelectedDate.Year != CommonHelper.SelectedDate.Year ||
                    LastSelectedDate.Month != CommonHelper.SelectedDate.Month ||
                    LastSelectedDate.Day != CommonHelper.SelectedDate.Day)
                {
                    LastSelectedDate = CommonHelper.SelectedDate;
                    NavPageTitle.Text = $"{title}[{CommonHelper.SelectedDate.ToString("yyyy-MM-dd")}]";
                    pagetiles.ShowImages(pagetiles.TargetPage, false, pagetiles.GetLastSelectedID());
                }
            }
        }

        internal void CommandNavNext_Click(object sender, RoutedEventArgs e)
        {
            pagetiles.ShowImages(pagetiles.TargetPage, true, pagetiles.GetLastSelectedID());
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

        private void LiveFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem)) return;
            if (sender == LiveFilterFavoritedRange) return;

            var menus_type = new List<MenuItem>() {
                LiveFilterUser, LiveFilterWork
            };
            var menus_fav = new List<MenuItem>() {
                LiveFilterFavorited, LiveFilterNotFavorited, //LiveFilterFavoritedRange,
                LiveFilterFavorited_00000,
                LiveFilterFavorited_00100, LiveFilterFavorited_00200, LiveFilterFavorited_00500,
                LiveFilterFavorited_01000, LiveFilterFavorited_02000, LiveFilterFavorited_05000,
                LiveFilterFavorited_10000, LiveFilterFavorited_20000, LiveFilterFavorited_50000,
            };
            var menus_follow = new List<MenuItem>() {
                LiveFilterFollowed, LiveFilterNotFollowed,
            };
            var menus_down = new List<MenuItem>() {
                LiveFilterDownloaded, LiveFilterNotDownloaded,
            };
            var menus_sanity = new List<MenuItem>() {
                LiveFilterAllAge, LiveFilterNoAllAge,
                LiveFilterR12, LiveFilterNoR12,
                LiveFilterR15, LiveFilterNoR15,
                LiveFilterR17, LiveFilterNoR17,
                LiveFilterR18, LiveFilterNoR18,
            };

            var menus = new List<IEnumerable<MenuItem>>() { menus_type, menus_fav, menus_follow, menus_down, menus_sanity };

            var idx = "LiveFilter".Length;

            string filter_type = string.Empty;
            string filter_fav = string.Empty;
            string filter_follow = string.Empty;
            string filter_down = string.Empty;
            string filter_sanity = string.Empty;

            var menu = sender as MenuItem;

            if (menu == LiveFilterNone)
            {
                LiveFilterNone.IsChecked = true;
                LiveFilterFavoritedRange.IsChecked = false;
                foreach (var fmenus in menus)
                {
                    foreach (var fmenu in fmenus)
                    {
                        fmenu.IsChecked = false;
                        fmenu.IsEnabled = true;
                    }
                }
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
                #region filter by favorited state
                LiveFilterFavoritedRange.IsChecked = false;
                foreach (var fmenu in menus_fav)
                {
                    if (menus_fav.Contains(menu))
                    {
                        if (fmenu == menu) fmenu.IsChecked = !fmenu.IsChecked;
                        else fmenu.IsChecked = false;
                    }
                    if (fmenu.IsChecked)
                    {
                        filter_fav = fmenu.Name.Substring(idx);
                        if (fmenu.Name.StartsWith("LiveFilterFavorited_"))
                            LiveFilterFavoritedRange.IsChecked = true;
                    }
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
                foreach (var fmenu in menus_sanity)
                {
                    if (menus_sanity.Contains(menu))
                    {
                        if (fmenu == menu) fmenu.IsChecked = !fmenu.IsChecked;
                        else fmenu.IsChecked = false;
                    }
                    if(fmenu.IsChecked) filter_sanity = fmenu.Name.Substring(idx);
                }
                #endregion
            }
            if (pagetiles is Pages.TilesPage)
                pagetiles.SetFilter(filter_type, filter_fav, filter_follow, filter_down, filter_sanity);
        }

        private void CommandFilter_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (pagetiles is Pages.TilesPage)
                CommandFilter.ToolTip = $"Tiles Count: {pagetiles.GetTilesCount()}";
            else CommandFilter.ToolTip = $"Live Filter";
        }

    }

}
