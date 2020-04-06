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

            SearchBox.ItemsSource = AutoSuggestList;

            CommandToggleTheme.ItemsSource = Theme.Accents;
            CommandToggleTheme.SelectedIndex = Theme.Accents.IndexOf(Theme.CurrentAccent);

            //Topmost = true;
            ShowActivated = true;
            //Activate();

            LastWindowStates.Enqueue(WindowState.Normal);
            UpdateTheme();
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Content is IllustDetailPage || Content is IllustImageViewerPage) CommandPageRefresh.Visibility = Visibility.Visible;
            else CommandPageRefresh.Visibility = Visibility.Collapsed;

            if (this.DropBoxExists() == null)
                CommandToggleDropbox.IsChecked = false;
            else
                CommandToggleDropbox.IsChecked = true;
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (this.Content is DownloadManagerPage)
            {
                (this.Content as DownloadManagerPage).Pos = new Point(this.Left, this.Top);
            }
            else if(this.Content is IllustDetailPage)
            {
                (this.Content as IllustDetailPage).DeleteHtmlRender();
            }
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
                CommonHelper.Cmd_Search.Execute(link);
            }
        }

        private void MetroWindow_KeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                sender.WindowKeyUp(e);
            }
            catch (Exception) { }
        }

        private void MetroWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
            if (e.ChangedButton == MouseButton.XButton1)
            {
                if (Title.Equals("DropBox", StringComparison.CurrentCultureIgnoreCase))
                {
                    //Hide();
                }
                else if (Title.Equals("Download Manager", StringComparison.CurrentCultureIgnoreCase))
                {
                    Hide();
                }
                else if(e.ClickCount >= 2)
                {
                    Close();
                }
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.XButton2)
            {
                if (Title.Equals("Download Manager", StringComparison.CurrentCultureIgnoreCase))
                {
                    Hide();
                }
                //else if(Title.StartsWith("Preview", StringComparison.CurrentCultureIgnoreCase) && e.ClickCount >= 2)
                //{
                //    Close();
                //}
                else if (e.ClickCount >= 2)
                {
                    Close();
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
            if (Content is Pages.IllustDetailPage)
            {
                var page = Content as Pages.IllustDetailPage;
                if(page.Tag is ImageItem)
                    page.UpdateDetail(page.Tag as ImageItem);
                else if(page.Tag is Pixeez.Objects.UserBase)
                    page.UpdateDetail(page.Tag as Pixeez.Objects.UserBase);
            }
            else if (Content is Pages.IllustImageViewerPage)
            {
                var page = Content as Pages.IllustImageViewerPage;
                if (page.Tag is ImageItem)
                    page.UpdateDetail(page.Tag as ImageItem);
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

        private void CommandDownloadManager_Click(object sender, RoutedEventArgs e)
        {
            true.ShowDownloadManager();
        }

        private void CommandToggleDropbox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton)
            {
                SetDropBoxState(true.ShowDropBox());
            }
        }

        private void CommandToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            Theme.Toggle();
            UpdateTheme();
        }

        private void CommandToggleTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CommandToggleTheme.SelectedIndex >= 0 && CommandToggleTheme.SelectedIndex < CommandToggleTheme.Items.Count)
            {
                Theme.CurrentAccent = Theme.Accents[CommandToggleTheme.SelectedIndex];
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

    }
}
