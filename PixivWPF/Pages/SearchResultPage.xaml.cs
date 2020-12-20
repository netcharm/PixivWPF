using MahApps.Metro.Controls;
using PixivWPF.Common;
using Prism.Commands;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PixivWPF.Pages
{
    /// <summary>
    /// IllustWithTagPage.xaml 的交互逻辑
    /// </summary>
    public partial class SearchResultPage : Page
    {
        private Window window = null;

        private string result_filter = string.Empty;

        private MenuItem ActionResultFilter = null;
        private ContextMenu ContextMenuResultFilter = null;
        private ConcurrentDictionary<string, Tuple<MenuItem, MenuItem>> filter_items = new ConcurrentDictionary<string, Tuple<MenuItem, MenuItem>>();

        public string Contents { get; set; } = string.Empty;

        private void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            ResultItems.UpdateDownloadStateAsync(illustid, exists);
        }

        public async void UpdateDownloadStateAsync(int? illustid = null, bool? exists = false)
        {
            await Task.Run(() =>
            {
                UpdateDownloadState(illustid, exists);
            });
        }

        public async void UpdateLikeStateAsync(int illustid = -1, bool is_user = false)
        {
            await new Action(() =>
            {
                UpdateLikeState(illustid, is_user);
            }).InvokeAsync();
        }

        public void UpdateLikeState(int illustid = -1, bool is_user = false)
        {
            if (ResultExpander.IsExpanded)
            {
                ResultItems.UpdateLikeState(illustid, is_user);
            }
        }

        private SemaphoreSlim CanUpdateing = new SemaphoreSlim(1, 1);

        public async void UpdateDetail(string content)
        {
            if (CanUpdateing.Wait(0))
            {
                try
                {
                    ResultExpander.Show();
                    if (Contents is string && !string.IsNullOrEmpty(Contents))
                    {
                        if (!ResultExpander.IsExpanded) ResultExpander.IsExpanded = true;

                        var tokens = await CommonHelper.ShowLogin();
                        if (tokens == null) throw (new Exception("No Token"));

                        ShowResultInline(tokens, Contents, result_filter);
                    }
                    if (ResultNextPage is Button) ResultNextPage.Show();
                }
                catch (Exception ex)
                {
                    ResultExpander.Header = $"Search Results, ERROR: {ex.Message}";
                }
                finally
                {
                    if (window != null) window.SizeToContent = SizeToContent.WidthAndHeight;
                    CanUpdateing.Release();
                }
            }
        }

        public void UpdateThumb()
        {
            ResultItems.UpdateTilesImage(Keyboard.Modifiers == ModifierKeys.Alt);
        }

        public void SetFilter(string filter)
        {
            try
            {
                ResultItems.Filter = filter.GetFilter();
            }
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
        }

        public void SetFilter(FilterParam filter)
        {
            try
            {
                if (filter is FilterParam)
                {
                    ResultItems.Filter = filter.GetFilter();
                }
                else
                {
                    ResultItems.Filter = null;
                }
            }
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
        }

        public dynamic GetTilesCount()
        {
            return ($"{ResultItems.ItemsCount}({ResultItems.Items.Count})");
        }

        public void PrevIllust()
        {
            if (this is SearchResultPage && ResultItems.ItemsCount > 1)
            {
                if (ResultItems.IsCurrentBeforeFirst)
                    ResultItems.MoveCurrentToLast();
                else
                    ResultItems.ItemsCollection.MoveCurrentToPrevious();
                ResultItems.ScrollIntoView(ResultItems.SelectedItem);
            }
        }

        public void NextIllust()
        {
            if (this is SearchResultPage && ResultItems.ItemsCount > 1)
            {
                if (ResultItems.IsCurrentAfterLast)
                    ResultItems.MoveCurrentToFirst();
                else
                    ResultItems.MoveCurrentToNext();
                ResultItems.ScrollIntoView(ResultItems.SelectedItem);
            }
        }

        internal void KeyAction(KeyEventArgs e)
        {
            Page_PreviewKeyUp(this, e);
        }

        public SearchResultPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            #region Update ContextMenu
            var cmr = Resources["MenuSearchResult"] as ContextMenu;
            if (cmr is ContextMenu)
            {
                foreach (dynamic item in cmr.Items)
                {
                    if (item is MenuItem && item.Name.Equals("ActionResultFilter", StringComparison.CurrentCultureIgnoreCase))
                    {
                        ActionResultFilter = item;
                        if ((item as MenuItem).Items.Count > 0)
                            ((item as MenuItem).Items[0] as MenuItem).IsChecked = true;
                        break;
                    }
                }
            }

            var cmf = Resources["MenuSearchFilter"] as ContextMenu;
            if (cmf is ContextMenu)
            {
                ContextMenuResultFilter = cmf;
                foreach (dynamic item in cmf.Items)
                {
                    if (item is MenuItem)
                    {
                        var mi = item as MenuItem;
                        if (mi.Name.Equals("SearchFilter_00000users", StringComparison.CurrentCultureIgnoreCase))
                        {
                            mi.IsChecked = true;
                            break;
                        }
                    }
                }
            }
            #endregion

            #region ToolButton MouseOver action
            SearchFilter.MouseOverAction();
            ResultPrevPage.MouseOverAction();
            ResultNextPage.MouseOverAction();
            ResultNextAppend.MouseOverAction();
            SearchRefreshThumb.MouseOverAction();
            #endregion

            window = Window.GetWindow(this);
            if (!string.IsNullOrEmpty(Contents)) UpdateDetail(Contents);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ResultItems.Items.Clear();
            }
            catch (Exception) { }
        }

        private void Page_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            Commands.KeyProcessor.Execute(new KeyValuePair<dynamic, KeyEventArgs>(ResultItems, e));
        }

        private void Page_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.XButton1 == MouseButtonState.Pressed)
            {
                NextIllust();
                e.Handled = true;
            }
            else if (e.XButton2 == MouseButtonState.Pressed)
            {
                PrevIllust();
                e.Handled = true;
            }
        }

        #region Search Result Panel related routines
        List<long?> id_user = new List<long?>();
        List<long?> id_illust = new List<long?>();
        private async void ShowResultInline(Pixeez.Tokens tokens, string content, string filter = "", string next_url = "", bool append = false)
        {
            try
            {
                if (string.IsNullOrEmpty(content)) return;

                ResultItems.Wait();

                if (!append)
                {
                    ResultItems.Items.Clear();
                    id_user.Clear();
                    id_illust.Clear();
                }

                var no_filter = string.IsNullOrEmpty(filter);
                var filter_string = no_filter ? string.Empty : $" ({filter.Replace("users入り", "+ Favs")})";
                ResultExpander.Header = $"Search Results{filter_string}, {content}";

                if (content.StartsWith("UserID:", StringComparison.CurrentCultureIgnoreCase))
                {
                    SearchFilter.Visibility = Visibility.Collapsed;

                    var query = Regex.Replace(content, @"^UserId: *?(\d+).*?$", "$1", RegexOptions.IgnoreCase).Trim();
                    var relatives = await tokens.GetUsersAsync(Convert.ToInt64(query));

                    if (relatives is List<Pixeez.Objects.User>)
                    {
                        foreach (var user in relatives)
                        {
                            if (id_user.Contains(user.Id)) continue;
                            id_user.Add(user.Id);
                            user.Cache();
                            user.AddTo(ResultItems.Items, next_url);
                            this.DoEvents();
                        }
                        this.DoEvents();
                    }
                }
                else if (content.StartsWith("IllustID:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^IllustID: *?(\d+).*?$", "$1", RegexOptions.IgnoreCase).Trim();
                    var relatives = await tokens.GetWorksAsync(Convert.ToInt64(query));
                    next_url = string.Empty;

                    if (relatives is List<Pixeez.Objects.NormalWork>)
                    {
                        foreach (var illust in relatives)
                        {
                            if (id_illust.Contains(illust.Id)) continue;
                            id_illust.Add(illust.Id);
                            illust.Cache();
                            illust.AddTo(ResultItems.Items, next_url);
                            this.DoEvents();
                        }
                        this.DoEvents();
                    }
                }
                else if (content.StartsWith("User:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^User:(.*?)$", "$1", RegexOptions.IgnoreCase).Trim();
                    query = string.IsNullOrEmpty(filter) ? query : $"{query} {filter}";
                    var relatives = string.IsNullOrEmpty(next_url) ? await tokens.SearchUserAsync(query) :
                        await tokens.AccessNewApiAsync<Pixeez.Objects.UsersSearchResult>(next_url);
                    next_url = relatives.next_url ?? string.Empty;

                    if (relatives is Pixeez.Objects.UsersSearchResult)
                    {
                        ResultExpander.Tag = next_url;
                        foreach (var user in relatives.Users)
                        {
                            if (id_user.Contains(user.User.Id)) continue;
                            user.User.Cache();
                            user.User.AddTo(ResultItems.Items, next_url);
                            id_user.Add(user.User.Id);
                            this.DoEvents();
                        }
                        this.DoEvents();
                    }
                }
                else if (content.StartsWith("Fuzzy:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^Fuzzy:(.*?)$", "$1", RegexOptions.IgnoreCase).Trim();
                    query = string.IsNullOrEmpty(filter) ? query : $"{query} {filter}";
                    var relatives = string.IsNullOrEmpty(next_url) ? await tokens.SearchIllustWorksAsync(query, "title_and_caption") :
                        await tokens.AccessNewApiAsync<Pixeez.Objects.Illusts>(next_url);
                    if (relatives is Pixeez.Objects.Illusts)
                    {
                        ResultExpander.Tag = next_url;
                        foreach (var illust in relatives.illusts)
                        {
                            if (id_illust.Contains(illust.Id)) continue;
                            illust.Cache();
                            illust.AddTo(ResultItems.Items, next_url);
                            id_illust.Add(illust.Id);
                            this.DoEvents();
                        }
                        this.DoEvents();
                    }
                }
                else if (content.StartsWith("Tag:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^Tag:(.*?)$", "$1", RegexOptions.IgnoreCase).Trim();
                    query = string.IsNullOrEmpty(filter) ? query : $"{query} {filter}";
                    var relatives = string.IsNullOrEmpty(next_url) ? await tokens.SearchIllustWorksAsync(query, "exact_match_for_tags") :
                        await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                    next_url = relatives.next_url ?? string.Empty;

                    if (relatives is Pixeez.Objects.Illusts && relatives.illusts is Array)
                    {
                        ResultExpander.Tag = next_url;
                        foreach (var illust in relatives.illusts)
                        {
                            if (id_illust.Contains(illust.Id)) continue;
                            illust.Cache();
                            illust.AddTo(ResultItems.Items, relatives.next_url);
                            id_illust.Add(illust.Id);
                            this.DoEvents();
                        }
                        this.DoEvents();
                    }
                }
                else if (content.StartsWith("Fuzzy Tag:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^Fuzzy Tag:(.*?)$", "$1", RegexOptions.IgnoreCase).Trim();
                    query = string.IsNullOrEmpty(filter) ? query : $"{query} {filter}";
                    var relatives = string.IsNullOrEmpty(next_url) ? await tokens.SearchIllustWorksAsync(query, "partial_match_for_tags") : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                    next_url = relatives.next_url ?? string.Empty;

                    if (relatives is Pixeez.Objects.Illusts && relatives.illusts is Array)
                    {
                        ResultExpander.Tag = next_url;
                        foreach (var illust in relatives.illusts)
                        {
                            if (id_illust.Contains(illust.Id)) continue;
                            illust.Cache();
                            illust.AddTo(ResultItems.Items, relatives.next_url);
                            id_illust.Add(illust.Id);
                            this.DoEvents();
                        }
                        this.DoEvents();
                    }
                }
                else if (content.StartsWith("Caption:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^Caption:(.*?)$", "$1", RegexOptions.IgnoreCase).Trim();
                    query = string.IsNullOrEmpty(filter) ? query : $"{query} {filter}";
                    var relatives = string.IsNullOrEmpty(next_url) ? await tokens.SearchIllustWorksAsync(query, "title_and_caption") : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                    next_url = relatives.next_url ?? string.Empty;

                    if (relatives is Pixeez.Objects.Illusts && relatives.illusts is Array)
                    {
                        ResultExpander.Tag = next_url;
                        foreach (var illust in relatives.illusts)
                        {
                            if (id_illust.Contains(illust.Id)) continue;
                            illust.Cache();
                            illust.AddTo(ResultItems.Items, relatives.next_url);
                            id_illust.Add(illust.Id);
                            this.DoEvents();
                        }
                        this.DoEvents();
                    }
                }
                ResultItems.UpdateTilesImage();

                if (ResultItems.Items.Count() == 1 && no_filter)
                {
                    ResultItems.SelectedIndex = 0;
                    Commands.Open.Execute(ResultItems);
                }
                if (ResultItems.Items.Count() <= 1 && no_filter)
                {
                    if (ResultItems.Items.Count() <= 0) "No Result".ShowToast("INFO");

                    if (window != null)
                    {
                        Application.Current.DoEvents();
                        await Task.Delay(1);
                        window.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                ResultItems.Fail();
                if (ex is NullReferenceException)
                {
                    //"No Result".ShowMessageBox("WARNING");
                    "No Result".ShowToast("WARNING");
                }
                else
                {
                    ex.Message.ShowToast("ERROR");
                }
            }
            finally
            {
                if (window is ContentWindow)
                {
                    ResultItems.Ready();
                    (window as MetroWindow).AdjustWindowPos();
                }
            }
        }

        private void ActionCopyResultIllustID_Click(object sender, RoutedEventArgs e)
        {
            Commands.CopyArtworkIDs.Execute(ResultItems);
        }

        private void ActionCopyWeblink_Click(object sender, RoutedEventArgs e)
        {
            UpdateLikeState();

            if (sender.GetUid().Equals("ActionIllustWebLink", StringComparison.CurrentCultureIgnoreCase))
            {
                Commands.CopyArtworkWeblinks.Execute(ResultItems);
            }
            else if (sender.GetUid().Equals("ActionAuthorWebLink", StringComparison.CurrentCultureIgnoreCase))
            {
                Commands.CopyArtistWeblinks.Execute(ResultItems);
            }

            e.Handled = true;
        }

        private void ActionOpenResult_Click(object sender, RoutedEventArgs e)
        {
            Commands.Open.Execute(ResultItems);
        }

        private void ActionSendToOtherInstance_Click(object sender, RoutedEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.None)
                Commands.SendToOtherInstance.Execute(ResultItems);
            else
                Commands.ShellSendToOtherInstance.Execute(ResultItems);
        }

        private void ActionRefreshResult_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                var m = sender as MenuItem;
                var host = (m.Parent as ContextMenu).PlacementTarget;
                if (m.Uid.Equals("ActionRefresh", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (host == ResultExpander || host == ResultItems)
                    {
                        UpdateDetail(Contents);
                    }
                }
                else if (m.Uid.Equals("ActionRefreshThumb", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (host == ResultExpander || host == ResultItems)
                    {
                        ResultItems.UpdateTilesImage(Keyboard.Modifiers == ModifierKeys.Alt);
                    }
                }
            }
            else if (sender == SearchRefreshThumb)
            {
                ResultItems.UpdateTilesImage();
            }
        }

        private void ActionSaveResult_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                foreach (PixivItem item in ResultItems.SelectedItems)
                {
                    Commands.SaveIllust.Execute(item);
                }
            }
        }

        private void ActionSaveAllResult_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                foreach (PixivItem item in ResultItems.SelectedItems)
                {
                    Commands.SaveIllustAll.Execute(item);
                }
            }
        }

        private void ActionOpenDownloaded_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                foreach (PixivItem item in ResultItems.SelectedItems)
                {
                    Commands.OpenDownloaded.Execute(item);
                }
            }
        }

        private void ActionSpeech_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Empty;
            CultureInfo culture = null;
            if (sender is MenuItem)
            {
                var mi = sender as MenuItem;
                if (mi.Parent is ContextMenu)
                {
                    var host = (mi.Parent as ContextMenu).PlacementTarget;
                    if (host == ResultExpander || host == ResultItems)
                    {
                        foreach (PixivItem item in ResultItems.SelectedItems)
                        {
                            text += $"{item.Subject},\r\n";
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(text)) text.Play(culture);
        }

        private void ActionBookmarkIllust_Click(object sender, RoutedEventArgs e)
        {
            string uid = (sender as dynamic).Uid;

            if (uid.Equals("ActionLikeIllust", StringComparison.CurrentCultureIgnoreCase) ||
                uid.Equals("ActionLikeIllustPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                uid.Equals("ActionUnLikeIllust", StringComparison.CurrentCultureIgnoreCase))
            {
                IList<PixivItem> items = new List<PixivItem>();
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == ResultItems || host == ResultExpander) items = ResultItems.GetSelectedIllusts();
                try
                {
                    if (uid.Equals("ActionLikeIllust", StringComparison.CurrentCultureIgnoreCase))
                    {
                        items.LikeIllust();
                    }
                    else if (uid.Equals("ActionLikeIllustPrivate", StringComparison.CurrentCultureIgnoreCase))
                    {
                        items.LikeIllust(false);
                    }
                    else if (uid.Equals("ActionUnLikeIllust", StringComparison.CurrentCultureIgnoreCase))
                    {
                        items.UnLikeIllust();
                    }
                }
                catch (Exception) { }
            }
        }

        private void ActionFollowAuthor_Click(object sender, RoutedEventArgs e)
        {
            string uid = (sender as dynamic).Uid;

            if (uid.Equals("ActionLikeUser", StringComparison.CurrentCultureIgnoreCase) ||
                uid.Equals("ActionLikeUserPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                uid.Equals("ActionUnLikeUser", StringComparison.CurrentCultureIgnoreCase))
            {
                IList<PixivItem> items = new List<PixivItem>();
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == ResultItems || host == ResultExpander) items = ResultItems.GetSelected();
                try
                {
                    if (uid.Equals("ActionLikeUser", StringComparison.CurrentCultureIgnoreCase))
                    {
                        items.LikeUser();
                    }
                    else if (uid.Equals("ActionLikeUserPrivate", StringComparison.CurrentCultureIgnoreCase))
                    {
                        items.LikeUser(false);
                    }
                    else if (uid.Equals("ActionUnLikeUser", StringComparison.CurrentCultureIgnoreCase))
                    {
                        items.UnLikeUser();
                    }
                }
                catch (Exception) { }
            }
        }

        private void ResultExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Contents is string && !string.IsNullOrEmpty(Contents)) UpdateDetail(Contents);
        }

        private void ResultExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            ResultItems.Ready();
            if (ResultNextPage is Button) ResultNextPage.Hide();
        }

        private void ResultItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = false;
            ResultItems.UpdateTilesState();
            e.Handled = true;
        }

        private void ResultItems_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void ResultItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed) Commands.Open.Execute(ResultItems);
            }
            catch (Exception) { }
        }

        private void SearchFilter_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender == SearchFilter && SearchFilter.ContextMenu is ContextMenu)
            {
                SearchFilter.ContextMenu.IsOpen = true;
            }
            else
            {
                if (sender is MenuItem)
                {
                    var mi = sender as MenuItem;
                    if (mi.Name.StartsWith("ActionFilter_"))
                    {
                        foreach (MenuItem item in ActionResultFilter.Items)
                        {
                            if (item == sender)
                            {
                                item.IsChecked = true;
                                var filter = Regex.Replace(item.Uid, @"SearchFilter_0*", "", RegexOptions.IgnoreCase);
                                result_filter = filter.Equals("users", StringComparison.CurrentCultureIgnoreCase) ? string.Empty : $"{filter}入り";
                            }
                            else item.IsChecked = false;

                            var cmi = ContextMenuResultFilter.Items.Cast<MenuItem>().Where(o=>string.Equals(o.Uid, item.Uid, StringComparison.CurrentCultureIgnoreCase));
                            if (cmi.Count() > 0)
                            {
                                if (cmi.First() is MenuItem) cmi.First().IsChecked = item.IsChecked;
                            }
                        }
                    }
                    else if (mi.Name.StartsWith("SearchFilter_"))
                    {
                        foreach (MenuItem item in ContextMenuResultFilter.Items)
                        {
                            if (item == sender)
                            {
                                item.IsChecked = true;
                                var filter = Regex.Replace(item.Uid, @"SearchFilter_0*", "", RegexOptions.IgnoreCase);
                                result_filter = filter.Equals("users", StringComparison.CurrentCultureIgnoreCase) ? string.Empty : $"{filter}入り";
                            }
                            else item.IsChecked = false;

                            var cmi = ActionResultFilter.Items.Cast<MenuItem>().Where(o=>string.Equals(o.Uid, item.Uid, StringComparison.CurrentCultureIgnoreCase));
                            if (cmi.Count() > 0)
                            {
                                if (cmi.First() is MenuItem) cmi.First().IsChecked = item.IsChecked;
                            }
                        }
                    }
                    UpdateDetail(Contents);
                }
            }
        }

        private void SearchResultPrevPage_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void ResultNextPage_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (Contents is string)
            {
                var next_url = string.Empty;
                if (ResultExpander.Tag is string)
                    next_url = ResultExpander.Tag as string;

                var append = sender == ResultNextAppend || sender.GetUid().Equals("ActionNextResultAppend", StringComparison.CurrentCultureIgnoreCase) ? true : false;
                ShowResultInline(tokens, Contents, result_filter, next_url, append);
            }
            ResultNextPage.Show();
        }

        #endregion
    }
}
