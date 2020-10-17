using MahApps.Metro.Controls;
using PixivWPF.Common;
using Prism.Commands;
using System;
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
        private Dictionary<string, Tuple<MenuItem, MenuItem>> filter_items = new Dictionary<string, Tuple<MenuItem, MenuItem>>();

        public string Contents { get; set; } = string.Empty;

        private void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            ResultIllusts.UpdateDownloadStateAsync(illustid, exists);
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
            await new Action(() => {
                UpdateLikeState(illustid, is_user);
            }).InvokeAsync();
        }

        public void UpdateLikeState(int illustid = -1, bool is_user = false)
        {
            if (ResultExpander.IsExpanded)
            {
                ResultIllusts.UpdateLikeState(illustid, is_user);
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
                        if (tokens == null) return;

                        ShowResultInline(tokens, Contents, result_filter);
                    }
                    if (ResultNextPage is Button) ResultNextPage.Show();
                }
                catch (Exception) { }
                finally
                {
                    CanUpdateing.Release();
                    if (window != null)
                        window.SizeToContent = SizeToContent.WidthAndHeight;
                }
            }
        }

        public void UpdateThumb()
        {
            ResultIllusts.UpdateTilesImage();
        }

        public void SetFilter(string filter)
        {
            try
            {
                ResultIllusts.Filter = filter.GetFilter();
            }
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
        }

        public void SetFilter(string filter_type, string filter_fav, string filter_follow, string filter_down, string filter_sanity)
        {
            try
            {
                var filter = new FilterParam()
                {
                    Type = filter_type,
                    Favorited = filter_fav,
                    Followed = filter_follow,
                    Downloaded = filter_down,
                    Sanity = filter_sanity
                };
                SetFilter(filter);
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
                    ResultIllusts.Filter = filter.GetFilter();
                }
                else
                {
                    ResultIllusts.Filter = null;
                }
            }
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
        }

        internal void KeyAction(KeyEventArgs e)
        {
            ResultIllusts_KeyUp(this, e);
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
            SearchRefreshThumb.MouseOverAction();
            #endregion

            window = Window.GetWindow(this);
            if (!string.IsNullOrEmpty(Contents)) UpdateDetail(Contents);
        }

        #region Search Result Panel related routines
        private async void ShowResultInline(Pixeez.Tokens tokens, string content, string filter = "", string next_url = "")
        {
            try
            {
                if (string.IsNullOrEmpty(content)) return;

                PreviewWait.Show();

                ResultIllusts.Items.Clear();

                List<long?> id_user = new List<long?>();
                List<long?> id_illust = new List<long?>();

                var no_filter = string.IsNullOrEmpty(filter);
                var filter_string = no_filter ? string.Empty : $" ({filter.Replace("users入り", "+ Favs")})";
                ResultExpander.Header = $"Search Results{filter_string}";

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
                            user.Cache();
                            user.AddTo(ResultIllusts.Items, next_url);
                            id_user.Add(user.Id);
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
                            illust.Cache();
                            illust.AddTo(ResultIllusts.Items, next_url);
                            id_illust.Add(illust.Id);
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
                            user.User.AddTo(ResultIllusts.Items, next_url);
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
                            illust.AddTo(ResultIllusts.Items, next_url);
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
                            illust.AddTo(ResultIllusts.Items, relatives.next_url);
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
                            illust.AddTo(ResultIllusts.Items, relatives.next_url);
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
                            illust.AddTo(ResultIllusts.Items, relatives.next_url);
                            id_illust.Add(illust.Id);
                            this.DoEvents();
                        }
                        this.DoEvents();
                    }
                }
                ResultIllusts.UpdateTilesImage();

                if (ResultIllusts.Items.Count() == 1 && no_filter)
                {
                    ResultIllusts.SelectedIndex = 0;
                    Commands.Open.Execute(ResultIllusts);
                }
                if (ResultIllusts.Items.Count() <= 1 && no_filter)
                {
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
                if (ex is NullReferenceException)
                {
                    //"No Result".ShowMessageBox("WARNING");
                    "No Result".ShowToast("WARNING");
                }
                else
                {
                    ex.Message.ShowMessageBox("ERROR");
                }
            }
            finally
            {
                if (window is ContentWindow)
                {
                    PreviewWait.Hide();
                    (window as MetroWindow).AdjustWindowPos();
                }
            }
        }

        private void ActionCopyResultIllustID_Click(object sender, RoutedEventArgs e)
        {
            Commands.CopyIllustIDs.Execute(ResultIllusts);
        }

        private void ActionOpenResult_Click(object sender, RoutedEventArgs e)
        {
            Commands.Open.Execute(ResultIllusts);
        }

        private void ActionSendToOtherInstance_Click(object sender, RoutedEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.None)
                Commands.SendToOtherInstance.Execute(ResultIllusts);
            else
                Commands.ShellSendToOtherInstance.Execute(ResultIllusts);
        }

        private void ActionRefreshResult_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                var m = sender as MenuItem;
                var host = (m.Parent as ContextMenu).PlacementTarget;
                if (m.Uid.Equals("ActionRefresh", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (host == ResultExpander || host == ResultIllusts)
                    {
                        UpdateDetail(Contents);
                    }
                }
                else if (m.Uid.Equals("ActionRefreshThumb", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (host == ResultExpander || host == ResultIllusts)
                    {
                        ResultIllusts.UpdateTilesImage();
                    }
                }
            }
            else if(sender == SearchRefreshThumb)
            {
                ResultIllusts.UpdateTilesImage();
            }
        }

        private void ActionSaveResult_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                foreach (ImageItem item in ResultIllusts.SelectedItems)
                {
                    Commands.SaveIllust.Execute(item);
                }
            }
        }

        private void ActionSaveAllResult_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                foreach (ImageItem item in ResultIllusts.SelectedItems)
                {
                    Commands.SaveIllustAll.Execute(item);
                }
            }
        }

        private void ActionOpenDownloaded_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                foreach (ImageItem item in ResultIllusts.SelectedItems)
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
                    if (host == ResultExpander || host == ResultIllusts)
                    {
                        foreach (ImageItem item in ResultIllusts.SelectedItems)
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
                IList<ImageItem> items = new List<ImageItem>();
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == ResultIllusts || host == ResultExpander) items = ResultIllusts.GetSelectedIllusts();
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
                IList<ImageItem> items = new List<ImageItem>();
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == ResultIllusts || host == ResultExpander) items = ResultIllusts.GetSelected();
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
            if(Contents is string && !string.IsNullOrEmpty(Contents)) UpdateDetail(Contents);
        }

        private void ResultExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            PreviewWait.Hide();
            if (ResultNextPage is Button) ResultNextPage.Hide();
        }

        private void ResultIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = false;
            ResultIllusts.UpdateTilesState();
            e.Handled = true;
        }

        private void ResultIllusts_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void ResultIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Commands.Open.Execute(ResultIllusts);
        }

        private void ResultIllusts_KeyUp(object sender, KeyEventArgs e)
        {
            Commands.KeyProcessor.Execute(new KeyValuePair<dynamic, KeyEventArgs>(ResultIllusts, e));
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

                ShowResultInline(tokens, Contents, result_filter, next_url);
            }
            ResultNextPage.Show();
        }

        #endregion
    }
}
