using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using PixivWPF.Common;
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
        //private Pages.NavPage pagenav = null;

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

        public async void UpdateIllustTagsAsync()
        {
            if (ContentFrame.Content is Pages.TilesPage)
            {
                var tiles = ContentFrame.Content as Pages.TilesPage;
                if (tiles.IllustDetail.Content is Pages.IllustDetailPage)
                {
                    await new Action(() =>
                    {
                        var detail = tiles.IllustDetail.Content as Pages.IllustDetailPage;
                        detail.UpdateIllustTagsAsync();
                    }).InvokeAsync();
                }
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
            else if( title.Equals("My"))
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

        private string GetLastSelectedID()
        {
            string id = pagetiles is Pages.TilesPage ? pagetiles.lastSelectedId : string.Empty;
            if (pagetiles.ListImageTiles.Items.Count > 0)
            {
                if (pagetiles.ListImageTiles.SelectedIndex == 0 && string.IsNullOrEmpty(pagetiles.lastSelectedId))
                    pagetiles.lastSelectedId = (pagetiles.ListImageTiles.Items[0] as ImageItem).ID;
                id = pagetiles.ListImageTiles.SelectedItem is ImageItem ? (pagetiles.ListImageTiles.SelectedItem as ImageItem).ID : pagetiles.lastSelectedId;
            }
            return (id);
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
                ex.Message.ShowMessageDialog("ERROR!");
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
                    if (contents.Equals("cmd:min_r18", StringComparison.CurrentCultureIgnoreCase))
                        Application.Current.MinimizedWindows("r18");
                    else
                    {
                        var links = contents.ParseLinks();
                        foreach (var link in links)
                        {
                            await new Action(() =>
                            {
                                CommonHelper.Cmd_Search.Execute(link);
                            }).InvokeAsync();
                        }
                    }
                }

                if(ps.IsConnected) ps.Disconnect();
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageDialog("ERROR!");
            }
            finally
            {
                if(pipeServer is NamedPipeServerStream && !pipeOnClosing) CreateNamedPipeServer();
            }
        }
        #endregion

        public MainWindow()
        {
            InitializeComponent();

            setting = Application.Current.LoadSetting();

            this.FontFamily = setting.FontFamily;

            Title = $"{Title} [Version: {Application.Current.Version()}]";

            SearchBox.ItemsSource = AutoSuggestList;

            Application.Current.SetThemeSync();
            CommandToggleTheme.ItemsSource = Application.Current.GetAccents();
            CommandToggleTheme.SelectedIndex = CommandToggleTheme.Items.IndexOf(Application.Current.CurrentAccent());

            MainContent = ContentFrame;

            pagetiles = new Pages.TilesPage() { FontFamily = FontFamily, Tag = ContentFrame };
            //pagenav = new Pages.NavPage() { FontFamily = FontFamily, Tag = pagetiles, NavFlyout = NavFlyout };

            //NavFlyout.Content = pagenav;
            //NavFlyout.Theme = FlyoutTheme.Adapt;
            //NavFlyout.Theme = FlyoutTheme.Accent;
            //NavFlyout.Opacity = 0.95;

            ContentFrame.Content = pagetiles;
            //NavFrame.Content = pagenav;

            ContentFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
            //NavFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;

            NavPageTitle.Text = pagetiles.TargetPage.ToString();

            //PixivCatgoryMenu.Content = pagenav;

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

            foreach (Window win in Application.Current.Windows)
            {
                if (win == this) continue;
                win.Close();
            }

            if (setting is Setting) setting.Save();
            Application.Current.Shutdown();
        }
#else
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageBox.Show("Continue Exit?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.Yes)
            {
                pipeOnClosing = true;
                ReleaseNamedPipeServer();
                Application.Current.ReleaseAppWatcher();
                Application.Current.LoadSetting().LocalStorage.ReleaseDownloadedWatcher();

                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MainWindow) continue;
                    else win.Close();
                }

                if (setting is Setting) setting.Save();
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
                CommonHelper.Cmd_Search.Execute(link);
            }
        }

        private void MetroWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                CommandNavRefresh_Click(CommandNavRefresh, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.F3)
            {
                CommandNavNext_Click(CommandNavNext, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.F6)
            {
                CommandNavRefresh_Click(CommandNavRefreshThumb, e);
                e.Handled = true;
            }
            else if (e.Key == Key.F7 || e.Key == Key.F8 || e.SystemKey == Key.F7 || e.SystemKey == Key.F8)
            {
                if (pagetiles is Pages.TilesPage)
                {
                    pagetiles.KeyAction(e);
                }
                e.Handled = true;
            }
            else
            {
                var ret = sender.WindowKeyUp(e);
                e.Handled = ret.Handled;
            }
        }

        private void MetroWindow_StateChanged(object sender, EventArgs e)
        {
            LastWindowStates.Enqueue(this.WindowState);
            if (LastWindowStates.Count > 2) LastWindowStates.Dequeue();
        }

        private async void MetroWindow_StylusUp(object sender, StylusEventArgs e)
        {
            //
            await Task.Delay(1);
            this.DoEvents();
        }

        private void NavFlyout_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is PixivPage)
            {
                UpdateTitle(e.NewValue.ToString());
            }
        }

        private void CommandToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.ToggleTheme();
        }

        private void CommandToggleTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(CommandToggleTheme.SelectedIndex>=0 && CommandToggleTheme.SelectedIndex< CommandToggleTheme.Items.Count)
            {
                Application.Current.SetAccent(CommandToggleTheme.SelectedValue.ToString());
            }
        }

        private void CommandLogin_Click(object sender, RoutedEventArgs e)
        {
            var accesstoken = Application.Current.AccessToken();
            var dlgLogin = new PixivLoginDialog() { AccessToken = accesstoken };
            var ret = dlgLogin.ShowDialog();
            if (ret ?? false)
            {
                accesstoken = dlgLogin.AccessToken;
                Setting.Token(accesstoken);
            }
        }

        private void CommandNav_Click(object sender, RoutedEventArgs e)
        {
            //NavFlyout.IsOpen = !NavFlyout.IsOpen;
        }

        internal void CommandNavRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender == CommandNavRefresh)
                {
                    var title = pagetiles.TargetPage.ToString();
                    if (title.StartsWith("Ranking", StringComparison.CurrentCultureIgnoreCase))
                    {
                        NavPageTitle.Text = $"{title}[{CommonHelper.SelectedDate.ToString("yyyy-MM-dd")}]";
                        CommandNavDate.IsEnabled = true;
                    }
                    else
                    {
                        NavPageTitle.Text = title;
                        CommandNavDate.IsEnabled = false;
                    }
                    pagetiles.ShowImages(pagetiles.TargetPage, false, GetLastSelectedID());
                }
                else if (sender == CommandNavRefreshThumb)
                {
                    pagetiles.UpdateImageTiles();
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
                CommonHelper.Cmd_DatePicker.Execute(point);

                if (LastSelectedDate.Year != CommonHelper.SelectedDate.Year ||
                    LastSelectedDate.Month != CommonHelper.SelectedDate.Month ||
                    LastSelectedDate.Day != CommonHelper.SelectedDate.Day)
                {
                    LastSelectedDate = CommonHelper.SelectedDate;
                    NavPageTitle.Text = $"{title}[{CommonHelper.SelectedDate.ToString("yyyy-MM-dd")}]";
                    pagetiles.ShowImages(pagetiles.TargetPage, false, GetLastSelectedID());
                }
            }
        }

        private void CommandNavPrev_Click(object sender, RoutedEventArgs e)
        {
        }

        internal void CommandNavNext_Click(object sender, RoutedEventArgs e)
        {
            pagetiles.ShowImages(pagetiles.TargetPage, true, GetLastSelectedID());
        }

        private void CommandSearch_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_Search.Execute(SearchBox.Text);
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
                    CommonHelper.Cmd_Search.Execute(query);
                }
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                e.Handled = true;
                CommonHelper.Cmd_Search.Execute(SearchBox.Text);
            }
        }

        private void CommandDownloadManager_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.ShowDownloadManager(true);
        }

        private void CommandToggleDropbox_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_OpenDropBox.Execute(sender);
        }
    }

}
