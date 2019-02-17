using MahApps.Metro.Controls;
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
        public void UpdateTheme()
        {
            foreach (Window win in Application.Current.Windows)
            {
                if (win.Content is Pages.IllustDetailPage)
                {
                    var page = win.Content as Pages.IllustDetailPage;
                    page.UpdateTheme();
                }
            }

        }

        private ObservableCollection<string> auto_suggest_list = new ObservableCollection<string>() {"a", "b" };
        public ObservableCollection<string> AutoSuggestList
        {
            get { return (auto_suggest_list); }
        }

        //public object Content
        //{
        //    get { return ViewerContent.Content; }
        //    set { ViewerContent.Content = value; }
        //}

        public ContentWindow()
        {
            InitializeComponent();

            SearchBox.ItemsSource = AutoSuggestList;

            CommandToggleTheme.ItemsSource = Common.Theme.Accents;
            CommandToggleTheme.SelectedIndex = Common.Theme.Accents.IndexOf(Common.Theme.CurrentAccent);

            //Topmost = true;
            ShowActivated = true;
            //Activate();
        }

        private void CommandToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            Common.Theme.Toggle();
            this.UpdateTheme();
        }

        private void CommandToggleTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CommandToggleTheme.SelectedIndex >= 0 && CommandToggleTheme.SelectedIndex < CommandToggleTheme.Items.Count)
            {
                Common.Theme.CurrentAccent = Common.Theme.Accents[CommandToggleTheme.SelectedIndex];
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

        private void CommandSearch_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_Search.Execute(SearchBox.Text);
        }

        private void SearchBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text.Length > 0)
            {
                var content = SearchBox.Text;
                auto_suggest_list.Clear();

                if (Regex.IsMatch(content, @"(.*?illust_id=)(\d+)(.*)", RegexOptions.IgnoreCase))
                    content = Regex.Replace(content, @"(.*?illust_id=)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase).Trim();
                else if (Regex.IsMatch(content, @"^(.*?\?id=)(\d+)(.*)$", RegexOptions.IgnoreCase))
                    content = Regex.Replace(content, @"^(.*?\?id=)(\d+)(.*)$", "UserID: $2", RegexOptions.IgnoreCase).Trim();
                else if (Regex.IsMatch(content, @"^(.*?tag_full&word=)(.*)$", RegexOptions.IgnoreCase))
                {
                    content = Regex.Replace(content, @"^(.*?tag_full&word=)(.*)$", "Tag: $2", RegexOptions.IgnoreCase).Trim();
                    content = Uri.UnescapeDataString(content);
                }
                content = Regex.Replace(content, @"((UserID)|(IllustID)|(Tag)|(Caption)|(Fuzzy)|(Fuzzy Tag)):", "", RegexOptions.IgnoreCase).Trim();

                if (Regex.IsMatch(content, @"^\d+$", RegexOptions.IgnoreCase))
                {
                    auto_suggest_list.Add($"UserID: {content}");
                    auto_suggest_list.Add($"IllustID: {content}");
                }
                auto_suggest_list.Add($"Fuzzy: {content}");
                auto_suggest_list.Add($"Tag: {content}");
                auto_suggest_list.Add($"Fuzzy Tag: {content}");
                auto_suggest_list.Add($"Caption: {content}");
                SearchBox.Items.Refresh();
                SearchBox.IsDropDownOpen = true;
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
            if (sender is System.Windows.Controls.Primitives.ToggleButton)
            {
                ContentWindow box = null;
                foreach (Window win in Application.Current.Windows)
                {
                    if (win.Title.Equals("Dropbox", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (win is ContentWindow)
                        {
                            box = win as ContentWindow;
                            break;
                        }
                    }
                }

                var btn = sender as System.Windows.Controls.Primitives.ToggleButton;
                if (box == null && !btn.IsChecked.Value)
                {
                    btn.IsChecked = true;
                }
                btn.IsChecked = CommonHelper.ShowDropBox(btn.IsChecked.Value);
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (this.Content is Pages.DownloadManagerPage) return;
            if (this.Tag is Pages.DownloadManagerPage) return;

            e.Handled = true;
            if (e.Key == Key.Escape) Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            var fmts = e.Data.GetFormats(true);
            if (new List<string>(fmts).Contains("Text"))
            {
                e.Effects = DragDropEffects.Link;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            var fmts = new List<string>(e.Data.GetFormats(true));

            if (fmts.Contains("text/html"))
            {
                using (var ms = (System.IO.MemoryStream)e.Data.GetData("text/html"))
                {
                    List<string> links = new List<string>();

                    var html = Encoding.Unicode.GetString(ms.ToArray());
                    if(Regex.IsMatch(html, @"href=.*?illust_id=\d+"))
                    {
                        foreach (Match m in Regex.Matches(html, @"href=""(https:\/\/www.pixiv.net\/member_illust\.php\?mode=.*?illust_id=\d+.*?)"""))
                        {
                            var link = m.Groups[1].Value;
                            if (!string.IsNullOrEmpty(link))
                            {
                                if (!links.Contains(link))
                                {
                                    links.Add(link);
                                    CommonHelper.Cmd_Search.Execute(link);
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (Match m in Regex.Matches(html, @"href=""(https:\/\/www.pixiv.net\/member\.php\?id=\d+)"""))
                        {
                            var link = m.Groups[1].Value;
                            if (!string.IsNullOrEmpty(link))
                            {
                                if (!links.Contains(link))
                                {
                                    links.Add(link);
                                    CommonHelper.Cmd_Search.Execute(link);
                                }
                            }
                        }
                    }
                }
            }
            else if (fmts.Contains("Text"))
            {
                var link = (string)e.Data.GetData("Text");
                if(new Uri(link) != null)
                {
                    CommonHelper.Cmd_Search.Execute(link);
                }
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e.ChangedButton == MouseButton.XButton1)
            {
                if (Title.Equals("DropBox", StringComparison.CurrentCultureIgnoreCase))
                {
                    //Hide();
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
    }
}
