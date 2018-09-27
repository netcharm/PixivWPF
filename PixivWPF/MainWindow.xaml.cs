using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using PixivWPF.Common;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PixivWPF
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public Frame MainContent = null;

        private Pages.PageTiles pagetiles = null;
        private Pages.PageNav pagenav = null;

        private ICommand searchBoxCmd;
        public ICommand SearchBoxCmd
        {
            get
            {
                return this.searchBoxCmd ?? (this.searchBoxCmd = new SimpleCommand
                {
                    CanExecuteDelegate = x => true, ExecuteDelegate = x =>
                    {
                        var content = (string)x;

                        var viewer = new ContentWindow();
                        var page = new Pages.IllustWithTagPage();
                        page.UpdateDetail(content);
                        viewer.Title = $"Search Keyword \"{content}\" Results";
                        viewer.Width = 720;
                        viewer.Height = 800;
                        viewer.Content = page;
                        viewer.Show();
                    }
                });
            }
        }

        private ObservableCollection<string> auto_suggest_list = new ObservableCollection<string>() {"a", "b" };
        public ObservableCollection<string> AutoSuggestList
        {
            get { return (auto_suggest_list); }
        }


        public void UpdateTheme()
        {
            if (pagenav is Pages.PageNav) pagenav.CheckPage();
            if (pagetiles is Pages.PageTiles) pagetiles.UpdateTheme();
        }

        public MainWindow()
        {
            InitializeComponent();

            SearchBox.ItemsSource = AutoSuggestList;

            MainContent = ContentFrame;

            //ContentFrame.Content = new Pages.PageLogin() { Tag = ContentFrame };
            pagetiles = new Pages.PageTiles() { Tag = ContentFrame };
            pagenav = new Pages.PageNav() { Tag = pagetiles, NavFlyout = NavFlyout };

            ContentFrame.Content = pagetiles;
            NavFrame.Content = pagenav;

            NavFlyout.Content = pagenav;
            NavFlyout.Theme = FlyoutTheme.Adapt;
            NavFlyout.Theme = FlyoutTheme.Accent;
            NavFlyout.Opacity = 0.95;

            ContentFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
            NavFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;

            NavPageTitle.Text = pagetiles.TargetPage.ToString();

            CommandToggleTheme.ItemsSource = Common.Theme.Accents;
            CommandToggleTheme.SelectedIndex = Common.Theme.Accents.IndexOf(Common.Theme.CurrentAccent);
        }

#if !DEBUG
        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;

            var opt = new MetroDialogSettings();
            opt.AffirmativeButtonText = "Yes";
            opt.NegativeButtonText = "No";
            opt.DefaultButtonFocus = MessageDialogResult.Affirmative;
            opt.DialogMessageFontSize = 24;
            opt.DialogResultOnCancel = MessageDialogResult.Canceled;

            var ret = await this.ShowMessageAsync("Confirm", "Continue Exit?", MessageDialogStyle.AffirmativeAndNegative, opt);
            if(ret == MessageDialogResult.Affirmative)
            {
                Application.Current.Shutdown();
            }
        }
#else
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win == this) continue;
                win.Close();
            }
        }
#endif
        private void CommandToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            Common.Theme.Toggle();
            this.UpdateTheme();
        }

        private void CommandToggleTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(CommandToggleTheme.SelectedIndex>=0 && CommandToggleTheme.SelectedIndex< CommandToggleTheme.Items.Count)
            {
                Common.Theme.CurrentAccent = Common.Theme.Accents[CommandToggleTheme.SelectedIndex];
                if (pagenav is Pages.PageNav) pagenav.CheckPage();
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
            NavPageTitle.Text = pagetiles.TargetPage.ToString();
            pagetiles.ShowImages(pagetiles.TargetPage, false);
        }

        private void CommandNavPrev_Click(object sender, RoutedEventArgs e)
        {
        }

        private void CommandNavNext_Click(object sender, RoutedEventArgs e)
        {
            NavPageTitle.Text = pagetiles.TargetPage.ToString();
            pagetiles.ShowImages(pagetiles.TargetPage, true);
        }

        private void NavFlyout_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if(e.NewValue is PixivPage)
            {
                NavPageTitle.Text = e.NewValue.ToString();
            }
        }

        private void SearchBox_TextInput(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0)
            {
                auto_suggest_list.Clear();
                if (Regex.IsMatch(e.Text, @"\d+", RegexOptions.IgnoreCase))
                {
                    auto_suggest_list.Add($"UserID: {e.Text}");
                    auto_suggest_list.Add($"IllustID: {e.Text}");
                }
                auto_suggest_list.Add($"Tag: {e.Text}");
                auto_suggest_list.Add($"Caption: {e.Text}");
                SearchBox.Items.Refresh();
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key== Key.Return)
            {
                SearchBoxCmd.Execute(SearchBox.Text);
            }
        }

        private void CommandSeqrch_Click(object sender, RoutedEventArgs e)
        {
            SearchBoxCmd.Execute(SearchBox.Text);
        }

        private void SearchBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text.Length > 0)
            {
                var content = SearchBox.Text;
                auto_suggest_list.Clear();
                if (Regex.IsMatch(content, @"^\d+$", RegexOptions.IgnoreCase))
                {
                    auto_suggest_list.Add($"UserID: {content}");
                    auto_suggest_list.Add($"IllustID: {content}");
                }
                auto_suggest_list.Add($"Illust: {content}");
                auto_suggest_list.Add($"Tag: {content}");
                auto_suggest_list.Add($"Caption: {content}");
                SearchBox.Items.Refresh();
                SearchBox.IsDropDownOpen = true;
                e.Handled = true;
            }
        }

        private void SearchBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var items = e.AddedItems;
            if (items.Count > 0)
            {
                var item = items[0];
                if (item is string)
                {
                    var content = (string)item;
                    SearchBoxCmd.Execute(content);
                }
            }
        }
    }


}
