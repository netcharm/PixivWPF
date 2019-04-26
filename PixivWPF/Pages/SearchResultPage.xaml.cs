using PixivWPF.Common;
using Prism.Commands;
using System;
using System.Collections.Generic;
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

namespace PixivWPF.Pages
{
    /// <summary>
    /// IllustWithTagPage.xaml 的交互逻辑
    /// </summary>
    public partial class SearchResultPage : Page
    {
        private Window window = null;
        public Window CurrentWindow
        {
            get { return (window); }
            set { window = value; }
        }

        internal object DataType = null;

        internal string result_filter = string.Empty;

        internal MenuItem ActionResultFilter = null;
        internal ContextMenu ContextMenuResultFilter = null;
        private Dictionary<string, Tuple<MenuItem, MenuItem>> filter_items = new Dictionary<string, Tuple<MenuItem, MenuItem>>();

        public SearchResultPage()
        {
            InitializeComponent();

            var cmr = Resources["MenuSearchResult"] as ContextMenu;
            if (cmr is ContextMenu)
            {
                foreach (MenuItem item in cmr.Items)
                {
                    if (item.Name.Equals("ActionResultFilter", StringComparison.CurrentCultureIgnoreCase))
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
                foreach (var item in cmf.Items)
                { 
                    if(item is MenuItem)
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
        }

        #region Search Result Panel related routines
        internal async void ShowResultInline(Pixeez.Tokens tokens, string content, string filter="", string next_url = "")
        {
            try
            {
                PreviewWait.Visibility = Visibility.Visible;

                ResultIllusts.Items.Clear();
                if (content.StartsWith("UserID:", StringComparison.CurrentCultureIgnoreCase))
                {
                    SearchFilter.Visibility = Visibility.Collapsed;

                    var query = Regex.Replace(content, @"^UserId: *?(\d+).*?$", "$1", RegexOptions.IgnoreCase).Trim();
                    var relatives = await tokens.GetUsersAsync(Convert.ToInt64(query));

                    if (relatives is List<Pixeez.Objects.User>)
                    {
                        foreach (var user in relatives)
                        {
                            user.AddTo(ResultIllusts.Items);
                        }
                    }
                }
                else if (content.StartsWith("IllustID:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^IllustID: *?(\d+).*?$", "$1", RegexOptions.IgnoreCase).Trim();
                    var relatives = await tokens.GetWorksAsync(Convert.ToInt64(query));
                    next_url = string.Empty;

                    if (relatives is List<Pixeez.Objects.NormalWork>)
                    {
                        foreach(var illust in relatives)
                        {
                            illust.AddTo(ResultIllusts.Items, next_url);
                        }
                    }
                }
                else if (content.StartsWith("Fuzzy:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^Fuzzy:(.*?)$", "$1", RegexOptions.IgnoreCase).Trim();
                    query = string.IsNullOrEmpty(filter) ? query : $"{query} {filter}";
                    var relatives = await tokens.SearchWorksAsync(query);

                    if (relatives is Pixeez.Objects.Paginated<Pixeez.Objects.NormalWork>)
                    {
                        foreach (var illust in relatives)
                        {
                            illust.AddTo(ResultIllusts.Items, next_url);
                        }
                    }
                }
                else if (content.StartsWith("Tag:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^Tag:(.*?)$", "$1", RegexOptions.IgnoreCase).Trim();
                    query = string.IsNullOrEmpty(filter) ? query : $"{query} {filter}";
                    var relatives = string.IsNullOrEmpty(next_url) ? await tokens.SearchIllustWorksAsync(query, "exact_match_for_tags") : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                    next_url = relatives.next_url ?? string.Empty;

                    if (relatives is Pixeez.Objects.Illusts && relatives.illusts is Array)
                    {
                        ResultExpander.Tag = next_url;
                        foreach (var illust in relatives.illusts)
                        {
                            illust.AddTo(ResultIllusts.Items, relatives.next_url);
                        }
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
                            illust.AddTo(ResultIllusts.Items, relatives.next_url);
                        }
                    }
                }
                else if (content.StartsWith("Caption:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^Caption:(.*?)$", "$1", RegexOptions.IgnoreCase).Trim();
                    query = string.IsNullOrEmpty(filter) ? query : $"{query} {filter}";
                    var relatives = string.IsNullOrEmpty(next_url) ? await tokens.SearchIllustWorksAsync(query, "title_and_caption") : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                    next_url = relatives.next_url ?? string.Empty;

                    if (relatives is Pixeez.Objects.Illusts &&  relatives.illusts is Array)
                    {
                        ResultExpander.Tag = next_url;
                        foreach (var illust in relatives.illusts)
                        {
                            illust.AddTo(ResultIllusts.Items, relatives.next_url);
                        }
                    }
                }
                ResultIllusts.UpdateImageTiles(tokens);
                var no_filter = string.IsNullOrEmpty(filter);
                var filter_string = no_filter ? string.Empty : $" ({filter.Replace("users入り", "+ Favs")})";
                ResultExpander.Header = $"Search Results{filter_string}";
                if (ResultIllusts.Items.Count() == 1 && no_filter)
                {
                    ResultIllusts.SelectedIndex = 0;
                    CommonHelper.Cmd_OpenIllust.Execute(ResultIllusts);
                    if (window != null)
                        window.Close();
                }
            }
            catch (Exception ex)
            {
                if(ex is NullReferenceException)
                {
                    "No Result".ShowMessageBox("INFO");
                }
                else
                {
                    ex.Message.ShowMessageBox("ERROR");
                }
            }
            finally
            {
                PreviewWait.Visibility = Visibility.Collapsed;
                if (window is ContentWindow)
                {
                    window.Topmost = true;
                    if (!window.IsActive) window.Activate();
                    window.Topmost = false;
                }
            }
        }

        private async void ResultExpander_Expanded(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (DataType is string)
            {
                var tag = (string)DataType;
                ShowResultInline(tokens, tag, result_filter);
            }
            if(ResultNextPage is Button)
                ResultNextPage.Visibility = Visibility.Visible;
            //ResultExpander.IsHitTestVisible = false;
        }

        private void ActionOpenResult_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_OpenIllust.Execute(ResultIllusts);
        }

        private void ActionSaveResult_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ActionSaveAllResult_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ResultIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void ResultIllusts_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void ResultIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            CommonHelper.Cmd_OpenIllust.Execute(ResultIllusts);
        }

        private void ResultIllusts_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommonHelper.Cmd_OpenIllust.Execute(ResultIllusts);
            }
        }

        private void SearchResultPrevPage_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void ResultNextPage_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (DataType is string)
            {
                var item = (string)DataType;
                var next_url = string.Empty;
                if (ResultExpander.Tag is string)
                    next_url = ResultExpander.Tag as string;

                ShowResultInline(tokens, item, result_filter, next_url);
            }
            ResultNextPage.Visibility = Visibility.Visible;
        }

        internal void UpdateDetail(string content)
        {
            DataType = content;
            ResultExpander.Visibility = Visibility.Visible;
            ResultExpander.IsExpanded = false;
            ResultExpander.IsExpanded = true;

            if (CurrentWindow != null)
                CurrentWindow.SizeToContent = SizeToContent.WidthAndHeight;
        }
        #endregion

        private void SearchFilter_Click(object sender, RoutedEventArgs e)
        {
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

                    ResultExpander_Expanded(ResultExpander, new RoutedEventArgs());
                }
            }
        }

        private void ActionCopyResultIllustID_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_CopyIllustIDs.Execute(ResultIllusts);
        }
    }
}
