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

        public SearchResultPage()
        {
            InitializeComponent();
        }

        #region Relative Panel related routines
        internal async void ShowRelativeInline(Pixeez.Tokens tokens, string content, string next_url = "")
        {
            try
            {
                PreviewWait.Visibility = Visibility.Visible;

                RelativeIllusts.Items.Clear();
                if (content.StartsWith("UserID:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^UserId: *?(\d+).*?$", "$1", RegexOptions.IgnoreCase).Trim();
                    var relatives = await tokens.GetUsersAsync(Convert.ToInt64(query));

                    if (relatives is List<Pixeez.Objects.User>)
                    {
                        foreach (var user in relatives)
                        {
                            user.AddTo(RelativeIllusts.Items);
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
                            illust.AddTo(RelativeIllusts.Items, next_url);
                        }
                    }
                }
                else if (content.StartsWith("Fuzzy:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^Fuzzy:(.*?)$", "$1", RegexOptions.IgnoreCase).Trim();
                    var relatives = await tokens.SearchWorksAsync(query);

                    if (relatives is Pixeez.Objects.Paginated<Pixeez.Objects.NormalWork>)
                    {
                        foreach (var illust in relatives)
                        {
                            illust.AddTo(RelativeIllusts.Items, next_url);
                        }
                    }
                }
                else if (content.StartsWith("Tag:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^Tag:(.*?)$", "$1", RegexOptions.IgnoreCase).Trim();
                    var relatives = string.IsNullOrEmpty(next_url) ? await tokens.SearchIllustWorksAsync(query, "exact_match_for_tags") : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                    next_url = relatives.next_url ?? string.Empty;

                    if (relatives is Pixeez.Objects.Illusts && relatives.illusts is Array)
                    {
                        RelativeIllustsExpander.Tag = next_url;
                        foreach (var illust in relatives.illusts)
                        {
                            illust.AddTo(RelativeIllusts.Items, relatives.next_url);
                        }
                    }
                }
                else if (content.StartsWith("Fuzzy Tag:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^Fuzzy Tag:(.*?)$", "$1", RegexOptions.IgnoreCase).Trim();
                    var relatives = string.IsNullOrEmpty(next_url) ? await tokens.SearchIllustWorksAsync(query, "partial_match_for_tags") : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                    next_url = relatives.next_url ?? string.Empty;

                    if (relatives is Pixeez.Objects.Illusts && relatives.illusts is Array)
                    {
                        RelativeIllustsExpander.Tag = next_url;
                        foreach (var illust in relatives.illusts)
                        {
                            illust.AddTo(RelativeIllusts.Items, relatives.next_url);
                        }
                    }
                }
                else if (content.StartsWith("Caption:", StringComparison.CurrentCultureIgnoreCase))
                {
                    var query = Regex.Replace(content, @"^Caption:(.*?)$", "$1", RegexOptions.IgnoreCase).Trim();
                    var relatives = string.IsNullOrEmpty(next_url) ? await tokens.SearchIllustWorksAsync(query, "title_and_caption") : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                    next_url = relatives.next_url ?? string.Empty;

                    if (relatives is Pixeez.Objects.Illusts &&  relatives.illusts is Array)
                    {
                        RelativeIllustsExpander.Tag = next_url;
                        foreach (var illust in relatives.illusts)
                        {
                            illust.AddTo(RelativeIllusts.Items, relatives.next_url);
                        }
                    }
                }
                RelativeIllusts.UpdateImageTiles(tokens);
                if (RelativeIllusts.Items.Count() == 1)
                {
                    RelativeIllusts.SelectedIndex = 0;
                    CommonHelper.Cmd_OpenIllust.Execute(RelativeIllusts);
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

        private async void RelativeIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (DataType is string)
            {
                var tag = (string)DataType;
                ShowRelativeInline(tokens, tag);
            }
            RelativeNextPage.Visibility = Visibility.Visible;
        }

        private void ActionOpenRelative_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_OpenIllust.Execute(RelativeIllusts);
        }

        private void ActionSaveRelative_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ActionSaveAllRelative_Click(object sender, RoutedEventArgs e)
        {

        }

        private void RelativeIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void RelativeIllusts_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void RelativeIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            CommonHelper.Cmd_OpenIllust.Execute(RelativeIllusts);
        }

        private void RelativeIllusts_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommonHelper.Cmd_OpenIllust.Execute(RelativeIllusts);
            }
        }

        private void RelativePrevPage_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void RelativeNextPage_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (DataType is string)
            {
                var item = (string)DataType;
                var next_url = string.Empty;
                if (RelativeIllustsExpander.Tag is string)
                    next_url = RelativeIllustsExpander.Tag as string;
                ShowRelativeInline(tokens, item, next_url);
            }
            RelativeNextPage.Visibility = Visibility.Visible;
        }

        internal void UpdateDetail(string content)
        {
            DataType = content;
            RelativeIllustsExpander.Visibility = Visibility.Visible;
            RelativeIllustsExpander.IsExpanded = false;
            RelativeIllustsExpander.IsExpanded = true;

            if (CurrentWindow != null)
                CurrentWindow.SizeToContent = SizeToContent.WidthAndHeight;
        }
        #endregion

    }
}
