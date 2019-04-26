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

        public SearchResultPage()
        {
            InitializeComponent();
            SearchFilter_00000users.IsChecked = true;
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
                        ResultIllustsExpander.Tag = next_url;
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
                        ResultIllustsExpander.Tag = next_url;
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
                        ResultIllustsExpander.Tag = next_url;
                        foreach (var illust in relatives.illusts)
                        {
                            illust.AddTo(ResultIllusts.Items, relatives.next_url);
                        }
                    }
                }
                ResultIllusts.UpdateImageTiles(tokens);
                if (ResultIllusts.Items.Count() == 1)
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
            ResultNextPage.Visibility = Visibility.Visible;
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
                if (ResultIllustsExpander.Tag is string)
                    next_url = ResultIllustsExpander.Tag as string;

                ShowResultInline(tokens, item, result_filter, next_url);
            }
            ResultNextPage.Visibility = Visibility.Visible;
        }

        internal void UpdateDetail(string content)
        {
            DataType = content;
            ResultIllustsExpander.Visibility = Visibility.Visible;
            ResultIllustsExpander.IsExpanded = false;
            ResultIllustsExpander.IsExpanded = true;

            if (CurrentWindow != null)
                CurrentWindow.SizeToContent = SizeToContent.WidthAndHeight;
        }
        #endregion

        private void SearchFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender == SearchFilter && SearchFilter.ContextMenu is ContextMenu)
            {
                SearchFilter.ContextMenu.IsOpen = true;
                return;
            }
            else
            {
                MenuItem[] items = new MenuItem[]
                {
                    SearchFilter_00000users,
                    SearchFilter_00100users, SearchFilter_00500users,
                    SearchFilter_01000users, SearchFilter_03000users, SearchFilter_05000users,
                    SearchFilter_10000users, SearchFilter_20000users, SearchFilter_30000users, SearchFilter_50000users
                };
                foreach (var item in items)
                {
                    if (item == sender)
                    {
                        item.IsChecked = true;
                        var filter = Regex.Replace(item.Name, @"SearchFilter_0*", "", RegexOptions.IgnoreCase);
                        result_filter = filter.Equals("users", StringComparison.CurrentCultureIgnoreCase) ? string.Empty : $"{filter}入り";
                    }
                    else item.IsChecked = false;
                }
                ResultExpander_Expanded(ResultIllustsExpander, new RoutedEventArgs());
            }
        }
    }
}
