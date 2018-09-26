using PixivWPF.Common;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public partial class IllustWithTagPage : Page
    {
        public ICommand Cmd_OpenIllust { get; } = new DelegateCommand<object>(obj => {
            //MessageBox.Show($"{obj}");
            if (obj is ImageListGrid)
            {
                var list = obj as ImageListGrid;
                foreach (var illust in list.SelectedItems)
                {
                    var viewer = new ViewerWindow();
                    if (list.Name.Equals("RelativeIllusts", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var page = new IllustDetailPage();
                        viewer.Content = page;
                        page.UpdateDetail(illust);
                    }
                    else
                    {
                        var page = new IllustImageViewerPage();
                        viewer.Content = page;
                        page.UpdateDetail(illust);
                    }
                    viewer.Title = $"ID: {illust.ID}, {illust.Subject}";
                    viewer.Width = 720;
                    viewer.Height = 800;
                    viewer.Show();
                }
            }
            else if (obj is ImageItem)
            {
                var viewer = new ViewerWindow();
                var page = new IllustImageViewerPage();
                var illust = obj as ImageItem;
                page.UpdateDetail(illust);

                viewer.Title = illust.Subject;
                viewer.Width = 720;
                viewer.Height = 800;
                viewer.Content = page;
                viewer.Show();
            }
        });

        internal object DataType = null;

        public IllustWithTagPage()
        {
            InitializeComponent();
        }

        #region Relative Panel related routines
        internal async void ShowRelativeInline(Pixeez.Tokens tokens, string tag, string next_url = "")
        {
            try
            {
                PreviewWait.Visibility = Visibility.Visible;

                var relatives = string.IsNullOrEmpty(next_url) ? await tokens.SearchIllustWorksAsync(tag) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = relatives.next_url ?? string.Empty;

                RelativeIllusts.Items.Clear();
                if (relatives.illusts is Array)
                {
                    RelativeIllustsExpander.Tag = next_url;
                    foreach (var illust in relatives.illusts)
                    {
                        illust.AddTo(RelativeIllusts.Items, relatives.next_url);
                    }
                    RelativeIllusts.UpdateImageTile(tokens);
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                PreviewWait.Visibility = Visibility.Collapsed;
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
            Cmd_OpenIllust.Execute(RelativeIllusts);
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
            Cmd_OpenIllust.Execute(RelativeIllusts);
        }

        private void RelativeIllusts_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Cmd_OpenIllust.Execute(RelativeIllusts);
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

        internal void UpdateDetail(string tag)
        {
            DataType = tag;
            RelativeIllustsExpander.Visibility = Visibility.Visible;
            RelativeIllustsExpander.IsExpanded = false;
            RelativeIllustsExpander.IsExpanded = true;
        }
        #endregion

    }
}
