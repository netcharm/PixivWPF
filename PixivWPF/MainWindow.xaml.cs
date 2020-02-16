using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public Queue<WindowState> LastWindowStates { get; set; } = new Queue<WindowState>();

        public Frame MainContent = null;

        private Pages.TilesPage pagetiles = null;
        private Pages.NavPage pagenav = null;

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

        public void UpdateTheme()
        {
            if (pagenav is Pages.NavPage) pagenav.CheckPage();
            if (pagetiles is Pages.TilesPage) pagetiles.UpdateTheme();
            CommonHelper.UpdateTheme();
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
                tiles.UpdateLikeStateAsync(illustid);
                if (tiles.IllustDetail.Content is Pages.IllustDetailPage)
                {
                    var detail = tiles.IllustDetail.Content as Pages.IllustDetailPage;
                    detail.UpdateLikeStateAsync(illustid);
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            this.FontFamily = Setting.Instance.FontFamily;

            SearchBox.ItemsSource = AutoSuggestList;

            CommandToggleTheme.ItemsSource = Common.Theme.Accents;
            CommandToggleTheme.SelectedIndex = Common.Theme.Accents.IndexOf(Common.Theme.CurrentAccent);

            MainContent = ContentFrame;

            pagetiles = new Pages.TilesPage() { FontFamily = FontFamily, Tag = ContentFrame };
            pagenav = new Pages.NavPage() { FontFamily = FontFamily, Tag = pagetiles, NavFlyout = NavFlyout };

            NavFlyout.Content = pagenav;
            NavFlyout.Theme = FlyoutTheme.Adapt;
            NavFlyout.Theme = FlyoutTheme.Accent;
            NavFlyout.Opacity = 0.95;

            ContentFrame.Content = pagetiles;
            NavFrame.Content = pagenav;

            ContentFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
            NavFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;

            NavPageTitle.Text = pagetiles.TargetPage.ToString();

            LastWindowStates.Enqueue(WindowState.Normal);
        }

#if DEBUG
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win == this) continue;
                win.Close();
            }
            if (Setting.Instance is Setting) Setting.Instance.Save();
        }
#else
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageBox.Show("Continue Exit?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Cancel) == MessageBoxResult.Yes)
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MainWindow) continue;
                    else win.Close();
                }
                if (Setting.Instance is Setting) Setting.Instance.Save();
                Application.Current.Shutdown();
            }
            else e.Cancel = true;
        }
#endif
        private void CommandToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            Common.Theme.Toggle();
            UpdateTheme();
        }

        private void CommandToggleTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(CommandToggleTheme.SelectedIndex>=0 && CommandToggleTheme.SelectedIndex< CommandToggleTheme.Items.Count)
            {
                Common.Theme.CurrentAccent = Common.Theme.Accents[CommandToggleTheme.SelectedIndex];
                UpdateTheme();
            }
        }

        private void CommandLogin_Click(object sender, RoutedEventArgs e)
        {
            var accesstoken = Setting.Token();
            var dlgLogin = new PixivLoginDialog() { AccessToken = accesstoken };
            var ret = dlgLogin.ShowDialog();
            accesstoken = dlgLogin.AccessToken;
            Setting.Token(accesstoken);
        }

        private void CommandNav_Click(object sender, RoutedEventArgs e)
        {
            NavFlyout.IsOpen = !NavFlyout.IsOpen;
        }

        private void CommandNavRefresh_Click(object sender, RoutedEventArgs e)
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
            //var id = pagetiles.ListImageTiles.SelectedItem is ImageItem ? (pagetiles.ListImageTiles.SelectedItem as ImageItem).ID : string.Empty;
            var id = pagetiles.ListImageTiles.SelectedIndex > 0 && pagetiles.ListImageTiles.SelectedItem is ImageItem ? (pagetiles.ListImageTiles.SelectedItem as ImageItem).ID : pagetiles.lastSelectedId;
            pagetiles.ShowImages(pagetiles.TargetPage, false, id);
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
                    var id = pagetiles.ListImageTiles.SelectedIndex > 0 && pagetiles.ListImageTiles.SelectedItem is ImageItem ? (pagetiles.ListImageTiles.SelectedItem as ImageItem).ID : pagetiles.lastSelectedId;
                    pagetiles.ShowImages(pagetiles.TargetPage, false, id);
                }
            }
        }

        private void CommandNavPrev_Click(object sender, RoutedEventArgs e)
        {
        }

        private void CommandNavNext_Click(object sender, RoutedEventArgs e)
        {
            pagetiles.ShowImages(pagetiles.TargetPage, true);
        }

        private void NavFlyout_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(e.NewValue is PixivPage)
            {
                var title = e.NewValue.ToString();
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
            }
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
            if(sender is System.Windows.Controls.Primitives.ToggleButton)
            {
                SetDropBoxState(true.ShowDropBox());
            }            
        }

        private void MainWindow_DragOver(object sender, DragEventArgs e)
        {
            var fmts = e.Data.GetFormats(true);
            if ( new List<string>(fmts).Contains("Text"))
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
            sender.WindowKeyUp(e);
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
    }


}
