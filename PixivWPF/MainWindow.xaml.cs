using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

        private bool IsShutdowning = false;
        public bool CloseApplication(bool confirm = true)
        {
            bool result = false;
            var ret = confirm ? MessageBox.Show("Continue Exit?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.Yes : true;
            if (ret)
            {
                IsShutdowning = true;
                pipeOnClosing = true;

                ReleaseNamedPipeServer();
                Application.Current.LoadSetting().LocalStorage.ReleaseDownloadedWatcher();
                Application.Current.ReleaseAppWatcher();
                Application.Current.ReleaseHttpClient();
                Application.Current.ReleaseHotkeys();

                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MainWindow) continue;
                    else if (win is MetroWindow) win.Close();
                }
                Application.Current.Shutdown();
            }
            else result = true;
            return (result);
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

        public void SetMemoryUsage()
        {
            this.BeginInvoke(() =>
            {
                try
                {
                    var pb = Application.Current.MemoryUsage(is_private:true).SmartFileSize(trimzero: false, padleft: 7);
                    var ws = Application.Current.MemoryUsage(is_private:false).SmartFileSize(trimzero: false, padleft: 7);
                    NavPageTitle.ToolTip = $"Memory Usage:{Environment.NewLine}Private Bytes = {pb}{Environment.NewLine}Working Set   = {ws}";
                }
                catch (Exception ex) { ex.ERROR("SetMemoryUsage Error!"); }
            });
        }

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

            CommandRestart.ContextMenu.UpdateDefaultStyle();
            //ConvertBackColorPicker.UpdateDefaultStyle();
        }

        public void UpdateTitle(string title)
        {
            if (title.StartsWith("Ranking", StringComparison.CurrentCultureIgnoreCase))
            {
                NavPageTitle.Text = $"{title}[{Contents.SelectedDate.ToString("yyyy-MM-dd")}]";
                CommandDatePicker.IsEnabled = true;
            }
            else if (title.Equals("My"))
            {

            }
            else
            {
                NavPageTitle.Text = title;
                CommandDatePicker.IsEnabled = false;
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

        public void Prefetching()
        {
            if (Contents is Pages.TilesPage) Contents.Prefetching();
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
            catch (Exception ex) { ex.ERROR("RecentJumpTo"); }
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
                catch (Exception ex) { ex.ERROR("ReleaseNamedPipeServer"); }
                try
                {
                    pipeServer.Close();
                }
                catch (Exception ex) { ex.ERROR("ReleaseNamedPipeServer"); }
                try
                {
                    pipeServer.Dispose();
                }
                catch (Exception ex) { ex.ERROR("ReleaseNamedPipeServer"); }
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
                    var contents = sw.ReadToEnd().Trim('"').Trim();
                    $"RECEIVED => {contents}".DEBUG("NamedPipe");

                    if (Regex.IsMatch(contents, @"^cmd[.,;:=%_+\-].*?", RegexOptions.IgnoreCase))
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
            catch (Exception ex) { ex.ERROR("PIPE"); }
            finally
            {
                if (pipeServer is NamedPipeServerStream && !pipeOnClosing) CreateNamedPipeServer();
            }
        }
        #endregion

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

#if DEBUG
            CommandAttachMetaFolder.Show();
            CommandMaintainDetailPage.Show();
#else
            CommandAttachMetaFolder.Hide();
            CommandMaintainDetailPage.Hide();
#endif                        
            Contents = new Pages.TilesPage() { Name = "CategoryTiles", FontFamily = FontFamily };
            Content = Contents;

            NavPageTitle.Text = Contents.TargetPage.ToString();

            LastWindowStates.Enqueue(WindowState.Normal);

            ConvertBackColorPicker.SelectedColor = setting.ConvertBackColor;
            ConvertBackColorPicker.CustomColorPalette01Header = "Recommended Colors";
            ConvertBackColorPicker.CustomColorPalette01ItemsSource = new List<Color>()
            {
                setting.DownloadReduceBackGroundColor,
                Colors.White, Colors.Black,
                Colors.Gray, Colors.Silver, Colors.DarkGray
            };
            ConvertBackColorPicker.CustomColorPalette01Style = ConvertBackColorPicker.StandardColorPaletteStyle;
            ConvertBackColorPicker.IsCustomColorPalette01Visible = true;
#if DEBUG
            ConvertBackColorPicker.Height += 56;
            ConvertBackColorPicker.MaxHeight += 56;
#endif

            RoutedCommand cmd_Escape = new RoutedCommand();
            cmd_Escape.InputGestures.Add(new KeyGesture(Key.Escape, ModifierKeys.None, Key.Escape.ToString()));
            CommandBindings.Add(new CommandBinding(cmd_Escape, (obj, evt) =>
            {
                evt.Handled = true;
                if (Content is Pages.TilesPage) (Content as Pages.TilesPage).StopPrefetching();
            }));

            CreateNamedPipeServer();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
#if DEBUG0
            e.Cancel = CloseApplication(false);
#else
            setting = Application.Current.LoadSetting();
            e.Cancel = IsShutdowning ? false : CloseApplication(setting.ConfirmExit);
#endif
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
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

        private void MainWindow_DragOver(object sender, DragEventArgs e)
        {
            var fmts = new List<string>(e.Data.GetFormats(true));
            if (fmts.Contains("Text") || fmts.Contains("FileDrop") || fmts.Contains("PixivItems"))
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        private void MainWindow_Drop(object sender, DragEventArgs e)
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
                if (Commands.ParallelExecutionConfirm(links))
                {
                    foreach (var link in links)
                    {
                        Commands.OpenSearch.Execute(link);
                    }
                }
            }
        }

        private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
            if (e.ChangedButton == MouseButton.Middle)
            {
                e.Handled = true;
                WindowState = WindowState.Minimized;
            }
        }

        private void PreftchingProgress_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Content is Pages.TilesPage)
                (Content as Pages.TilesPage).StopPrefetching();
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

        private void NavPageTitle_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            SetMemoryUsage();
        }

        private void cmiProxyAction_Opened(object sender, RoutedEventArgs e)
        {
            try
            {
                setting = Application.Current.LoadSetting();
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
                setting = Application.Current.LoadSetting();
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

        private void ConvertBackColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            var setting = Application.Current.LoadSetting();
            setting.ConvertBackColor = e.NewValue ?? setting.ConvertBackColor;
            if (Keyboard.Modifiers == ModifierKeys.Control)
                setting.DownloadReduceBackGroundColor = e.NewValue ?? setting.DownloadReduceBackGroundColor;
            e.Handled = true;
        }

        private void CommandToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            Application.Current.ToggleTheme();
        }

        private void CommandToggleTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CommandToggleTheme.SelectedIndex >= 0 && CommandToggleTheme.SelectedIndex < CommandToggleTheme.Items.Count)
            {
                e.Handled = true;
                var item = CommandToggleTheme.SelectedItem;
                if (item is SimpleAccent)
                    Application.Current.SetAccent((item as SimpleAccent).AccentName);
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

        private void CommandApplication_Click(object sender, RoutedEventArgs e)
        {
            if (sender == CommandRestart)
                Commands.RestartApplication.Execute(null);
            else if (sender == CommandUpgrade)
                Commands.UpgradeApplication.Execute(null);
            else if (sender == CommandOpenConfig)
                Commands.OpenConfig.Execute(null);
            else if (sender == CommandOpenFullListUsers)
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    setting.LoadFullListedUserState(force: true);
                else
                    Commands.OpenFullListUsers.Execute(null);
            }
            else if (sender == CommandMaintainCustomTag)
                Commands.MaintainCustomTag.Execute(null);
            else if (sender == CommandMaintainNetwork)
                Commands.MaintainNetwork.Execute(null);
            else if (sender == CommandMaintainMemoryUsage)
                Commands.MaintainMemoryUsage.Execute(null);
            else if (sender == CommandMaintainDetailPage)
                Commands.MaintainDetailPage.Execute(null);
            else if (sender == CommandMaintainHiddenWindow)
                Commands.MaintainDetailPage.Execute(null);
            else if (sender == CommandSearchInFile)
            {
                //Commands.SearchInStorage.Execute(null);
            }
            else if (sender == CommandConvertBackColor)
            {
                // Commands.PickColor.Execute(null);

            }
        }

        private void CommandRestart_DropDownOpened(object sender, EventArgs e)
        {
            CommandRestart.ContextMenu.IsOpen = true;
        }

        private void CommandTouch_Click(object sender, RoutedEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
                Commands.OpenAttachMetaInfo.Execute(null);
            else
                Commands.OpenTouchFolder.Execute(null);
        }

        private void CommandAttachMeta_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Commands.OpenAttachMetaInfo.Execute(null);
#endif
        }

        private void CommandRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender == CommandRefresh)
                {
                    RefreshPage();
                }
                else if (sender == CommandRefreshThumb)
                {
                    RefreshThumbnail();
                }
            }
            catch (Exception ex) { ex.ERROR("CommandRefresh"); }
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

        private void CommandDatePicker_Click(object sender, RoutedEventArgs e)
        {
            var title = Contents.TargetPage.ToString();
            if (title.StartsWith("Ranking", StringComparison.CurrentCultureIgnoreCase))
            {
                DatePicker.DisplayDateEnd = DateTime.Now;
                DatePickerPopup.IsOpen = true;
            }
        }

        private void CommandNext_Click(object sender, RoutedEventArgs e)
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

            if (Contents is Pages.TilesPage) Contents.SetFilter(filter);
        }

    }
}