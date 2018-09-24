using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
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
    /// IllustDetailPage.xaml 的交互逻辑
    /// </summary>
    public partial class IllustDetailPage : Page
    {

        public void UpdateTheme()
        {
            var style = new StringBuilder();
            //style.AppendLine($"body{{color:{Common.Theme.TextColor.ToHtml(false)};background-color:#333;}}");
            //style.AppendLine($"a{{background-color:{Common.Theme.AccentColor.ToHtml(false)} |important;color:{Common.Theme.TextColor.ToHtml(false)} |important;margin:4px;text-decoration:none;}}");
            style.AppendLine($".tag{{background-color:{Common.Theme.AccentColor.ToHtml(false)} |important;color:{Common.Theme.TextColor.ToHtml(false)} |important;margin:4px;text-decoration:none;}}");
            style.AppendLine($".desc{{color:{Common.Theme.TextColor.ToHtml(false)} !important;text-decoration:none !important;}}");
            style.AppendLine($"a{{color:{Common.Theme.TextColor.ToHtml(false)} |important;text-decoration:none !important;}}");

            var BaseStyleSheet = string.Join("\n", style);
            IllustTags.BaseStylesheet = BaseStyleSheet;
            IllustDesc.BaseStylesheet = BaseStyleSheet;

            var tags = IllustTags.Text;
            var desc = IllustDesc.Text;

            IllustTags.Text = string.Empty;
            IllustDesc.Text = string.Empty;

            IllustTags.Text = tags;
            IllustDesc.Text = desc;
        }

        public async void UpdateDetail(ImageItem item)
        {
            try
            {
                PreviewWait.Visibility = Visibility.Visible;

                Preview.Tag = item;

                var url = item.Illust.ImageUrls.Large;
                if (string.IsNullOrEmpty(url))
                {
                    url = item.Illust.ImageUrls.Medium;
                }

                var tokens = await CommonHelper.ShowLogin();
                Preview.Source = await url.LoadImage(tokens);

                IllustAuthor.Text = item.Illust.User.Name;
                IllustAuthorIcon.Source = await item.Illust.User.GetAvatarUrl().LoadImage(tokens);
                IllustTitle.Text = item.Illust.Title;

                var html = new StringBuilder();
                foreach (var tag in item.Illust.Tags)
                {
                    html.AppendLine($"<a href=\"https://www.pixiv.net/search.php?s_mode=s_tag_full&word={Uri.EscapeDataString(tag)}\" class=\"tag\">{tag}</a>");
                }
                IllustTags.Foreground = Common.Theme.TextBrush;
                IllustTags.Text = string.Join(";", html);
                IllustTag.Visibility = Visibility.Visible;
                IllustActions.Visibility = Visibility.Visible;

                PreviewBadge.Badge = item.Illust.PageCount;
                if (item.Illust.PageCount > 1)
                {
                    PreviewBadge.Visibility = Visibility.Visible;
                    ActionOpenIllustSet.Visibility = Visibility.Visible;
                }
                else
                {
                    PreviewBadge.Visibility = Visibility.Hidden;
                    ActionOpenIllustSet.Visibility = Visibility.Collapsed;
                }
                IllustDesc.Text = $"<div class=\"desc\">{item.Illust.Caption}</div>";

                SubIllusts.Items.Clear();
                SubIllusts.Refresh();
                if (item.Illust.PageCount > 1)
                {
                    SubIllustsExpander.Visibility = Visibility.Visible;
                    SubIllustsExpander.IsExpanded = true;
                }
                else
                    SubIllustsExpander.Visibility = Visibility.Collapsed;

                RelativeIllustsExpander.Visibility = Visibility.Visible;
                RelativeNextPage.Visibility = Visibility.Collapsed;
                RelativeIllustsExpander.IsExpanded = false;
                PreviewWait.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                PreviewWait.Visibility = Visibility.Hidden;
            }
        }

        internal void ShowIllustPages(Pixeez.Tokens tokens, ImageItem item, int start = 0, int count = 30)
        {
            try
            {
                PreviewWait.Visibility = Visibility.Visible;

                //SubIllustsPanel.Visibility = Visibility.Collapsed;
                SubIllusts.Items.Clear();
                if (item.Illust is Pixeez.Objects.IllustWork)
                {
                    var subset = item.Illust as Pixeez.Objects.IllustWork;
                    if (subset.meta_pages.Count() > 1)
                    {
                        //SubIllustsPanel.Visibility = Visibility.Visible;

                        //var idx = 0;
                        //foreach (var pages in subset.meta_pages)
                        //{
                        //    pages.AddTo(SubIllusts.Items, item.Illust, idx++, item.NextURL);
                        //}

                        btnSubIllustPrevPages.Tag = start - count;
                        var total = subset.meta_pages.Count();
                        for (var i = start; i < total; i++)
                        {
                            if (i < 0) continue;

                            var pages = subset.meta_pages[i];
                            pages.AddTo(SubIllusts.Items, item.Illust, i, item.NextURL);

                            if (i - start >= count - 1) break;
                            btnSubIllustNextPages.Tag = i + 2;
                        }

                        if ((int)btnSubIllustPrevPages.Tag < 0)
                            btnSubIllustPrevPages.Visibility = Visibility.Collapsed;
                        else
                            btnSubIllustPrevPages.Visibility = Visibility.Visible;

                        if ((int)btnSubIllustNextPages.Tag >= total - 1)
                            btnSubIllustNextPages.Visibility = Visibility.Collapsed;
                        else
                            btnSubIllustNextPages.Visibility = Visibility.Visible;

                        //SubIllustsPanel.InvalidateVisual();
                        SubIllusts.UpdateImageTile(tokens);
                    }
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

        internal async void ShowRelativeInline(Pixeez.Tokens tokens, ImageItem item, string next_url="")
        {
            try
            {
                PreviewWait.Visibility = Visibility.Visible;

                var relatives = string.IsNullOrEmpty(next_url) ? await tokens.GetRelatedWorks(item.Illust.Id.Value) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
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

        public IllustDetailPage()
        {
            InitializeComponent();
        }

        private void RelativeIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void SubIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Actions_Click(object sender, RoutedEventArgs e)
        {
            IllustActions.ContextMenu.IsOpen = true;
        }

        private void ActionOpenIllustSet_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ActionIllustUserInfo_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void ActionIllustPages_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if(Preview.Tag is ImageItem)
            {
                var item = Preview.Tag as ImageItem;
                ShowIllustPages(tokens, item);
            }
        }

        private async void ActionIllustRelative_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            //var idx = ListImageTiles.SelectedIndex;
            //if (idx < 0) return;
            //var item = ImageList[idx];
            if (Preview.Tag is ImageItem)
            {
                var item = Preview.Tag as ImageItem;
                ShowRelativeInline(tokens, item);
            }
        }

        private void ActionOpenIllust_Click(object sender, RoutedEventArgs e)
        {
            foreach (var illust in SubIllusts.SelectedItems)
            {
                var viewer = new ViewerWindow();
                var page = new IllustImageViewerPage();
                page.UpdateDetail(illust);

                viewer.Title = illust.Subject;
                viewer.Width = 720;
                viewer.Height = 800;
                viewer.Content = page;
                viewer.Show();
            }
        }

        private async void ActionSaveIllust_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (SubIllusts.SelectedItems != null && SubIllusts.SelectedItems.Count > 0)
            {
                foreach (var item in SubIllusts.SelectedItems)
                {
                    if (item.Tag is Pixeez.Objects.MetaPages)
                    {
                        var pages = item.Tag as Pixeez.Objects.MetaPages;
                        if (!string.IsNullOrEmpty(pages.ImageUrls.Original))
                        {
                            //await pages.ImageUrls.Original.ToImageFile(tokens);

                            var illust = item.Illust;
                            var dt = DateTime.Now;
                            if (illust is Pixeez.Objects.IllustWork)
                            {
                                var illustset = illust as Pixeez.Objects.IllustWork;
                                dt = illustset.CreatedTime;
                            }
                            else if (illust is Pixeez.Objects.NormalWork)
                            {
                                var illustset = illust as Pixeez.Objects.NormalWork;
                                dt = illustset.CreatedTime.UtcDateTime;
                            }
                            else if (!string.IsNullOrEmpty(illust.ReuploadedTime))
                            {
                                dt = DateTime.Parse(illust.ReuploadedTime);
                            }

                            var is_meta_single_page = illust.PageCount==1 ? true : false;
                            await pages.ImageUrls.Original.ToImageFile(tokens, dt, is_meta_single_page);
                        }
                    }
                    SystemSounds.Beep.Play();
                }
            }
            else if (SubIllusts.SelectedItem is Common.ImageItem)
            {
                var item = SubIllusts.SelectedItem;
                if (item.Tag is Pixeez.Objects.MetaPages)
                {
                    var pages = item.Tag as Pixeez.Objects.MetaPages;
                    if (!string.IsNullOrEmpty(pages.ImageUrls.Original))
                    {
                        //await pages.ImageUrls.Original.ToImageFile(tokens);

                        var illust = item.Illust;
                        var dt = DateTime.Now;
                        if (illust is Pixeez.Objects.IllustWork)
                        {
                            var illustset = illust as Pixeez.Objects.IllustWork;
                            dt = illustset.CreatedTime;
                        }
                        else if (illust is Pixeez.Objects.NormalWork)
                        {
                            var illustset = illust as Pixeez.Objects.NormalWork;
                            dt = illustset.CreatedTime.UtcDateTime;
                        }
                        else if (!string.IsNullOrEmpty(illust.ReuploadedTime))
                        {
                            dt = DateTime.Parse(illust.ReuploadedTime);
                        }

                        var is_meta_single_page = illust.PageCount==1 ? true : false;
                        await pages.ImageUrls.Original.ToImageFile(tokens, dt, is_meta_single_page);
                        SystemSounds.Beep.Play();
                    }
                }
            }
            else
            {
                if(Preview.Tag is ImageItem)
                {
                    var item = Preview.Tag as ImageItem;
                    if (item.Illust is Pixeez.Objects.Work)
                    {

                        var illust = item.Illust;
                        var images = illust.ImageUrls;
                        var url = images.Original;

                        var dt = DateTime.Now;

                        if (illust is Pixeez.Objects.IllustWork)
                        {
                            var illustset = illust as Pixeez.Objects.IllustWork;
                            if (illustset.meta_pages.Count() > 0)
                                url = illustset.meta_pages[0].ImageUrls.Original;
                            else if (illustset.meta_single_page is Pixeez.Objects.MetaSinglePage)
                                url = illustset.meta_single_page.OriginalImageUrl;

                            dt = illustset.CreatedTime;
                        }
                        else if (illust is Pixeez.Objects.NormalWork)
                        {
                            var illustset = illust as Pixeez.Objects.NormalWork;
                            dt = illustset.CreatedTime.UtcDateTime;
                        }
                        else if (!string.IsNullOrEmpty(illust.ReuploadedTime))
                        {
                            dt = DateTime.Parse(illust.ReuploadedTime);
                        }

                        if (string.IsNullOrEmpty(url))
                        {
                            if (!string.IsNullOrEmpty(images.Large))
                                url = images.Medium;
                            else if (!string.IsNullOrEmpty(images.Medium))
                                url = images.Medium;
                            else if (!string.IsNullOrEmpty(images.Px480mw))
                                url = images.Px480mw;
                            else if (!string.IsNullOrEmpty(images.SquareMedium))
                                url = images.SquareMedium;
                            else if (!string.IsNullOrEmpty(images.Px128x128))
                                url = images.Px128x128;
                            else if (!string.IsNullOrEmpty(images.Small))
                                url = images.Small;
                        }

                        if (!string.IsNullOrEmpty(url))
                        {
                            //await url.ToImageFile(tokens);
                            var is_meta_single_page = illust.PageCount==1 ? true : false;
                            await url.ToImageFile(tokens, dt, is_meta_single_page);
                            SystemSounds.Beep.Play();
                        }
                    }
                }
            }
        }

        private async void ActionSaveAllIllust_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (Preview.Tag is Pixeez.Objects.Work)
            {
                IllustsSaveProgress.Visibility = Visibility.Visible;
                IllustsSaveProgress.Value = 0;
                IProgress<int> progress = new Progress<int>(i => { IllustsSaveProgress.Value = i; });

                var illust = Preview.Tag as Pixeez.Objects.Work;
                var images = illust.ImageUrls;
                var url = images.Original;
                var dt = DateTime.Now;

                if (string.IsNullOrEmpty(url))
                {
                    if (!string.IsNullOrEmpty(images.Large))
                        url = images.Medium;
                    else if (!string.IsNullOrEmpty(images.Medium))
                        url = images.Medium;
                    else if (!string.IsNullOrEmpty(images.Px480mw))
                        url = images.Px480mw;
                    else if (!string.IsNullOrEmpty(images.SquareMedium))
                        url = images.SquareMedium;
                    else if (!string.IsNullOrEmpty(images.Px128x128))
                        url = images.Px128x128;
                    else if (!string.IsNullOrEmpty(images.Small))
                        url = images.Small;
                }

                if (illust is Pixeez.Objects.IllustWork)
                {
                    var illustset = illust as Pixeez.Objects.IllustWork;
                    dt = illustset.CreatedTime;
                    var is_meta_single_page = illust.PageCount==1 ? true : false;
                    var idx=0;
                    var total = illustset.meta_pages.Count();
                    foreach (var pages in illustset.meta_pages)
                    {
                        if (string.IsNullOrEmpty(pages.ImageUrls.Original))
                            await url.ToImageFile(tokens, dt, is_meta_single_page);
                        else
                            await pages.ImageUrls.Original.ToImageFile(tokens, dt, is_meta_single_page);

                        idx++;
                        progress.Report((int)((double)idx / total * 100));
                    }
                }
                else if (illust is Pixeez.Objects.NormalWork)
                {
                    var illustset = illust as Pixeez.Objects.NormalWork;
                    dt = illustset.CreatedTime.UtcDateTime;

                    var is_meta_single_page = illust.PageCount==1 ? true : false;
                    await url.ToImageFile(tokens, dt, is_meta_single_page);
                }
                IllustsSaveProgress.Value = 100;
                IllustsSaveProgress.Visibility = Visibility.Collapsed;
                SystemSounds.Beep.Play();
            }
        }

        private async void SubIllustPagesNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender == btnSubIllustPrevPages || sender == btnSubIllustNextPages)
            {
                var btn = sender as Button;
                if (btn.Tag is int)
                {
                    var start = (int)btn.Tag;

                    var tokens = await CommonHelper.ShowLogin();
                    if (tokens == null) return;

                    //var idx = ListImageTiles.SelectedIndex;
                    //if (idx < 0) return;
                    //var item = ImageList[idx];
                    //ShowIllustPages(tokens, item, start);

                    if (Preview.Tag is ImageItem)
                    {
                        var item = Preview.Tag as ImageItem;
                        ShowIllustPages(tokens, item, start);
                    }
                }
            }
        }

        private void ActionOpenRelative_Click(object sender, RoutedEventArgs e)
        {
            foreach (var illust in RelativeIllusts.SelectedItems)
            {
                var viewer = new ViewerWindow();
                var page = new IllustDetailPage();
                page.UpdateDetail(illust);

                viewer.Title = illust.Subject;
                viewer.Width = 720;
                viewer.Height = 800;
                viewer.Content = page;
                viewer.Show();
            }
        }

        private void ActionSaveRelative_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ActionSaveAllRelative_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void SubIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (Preview.Tag is ImageItem)
            {
                var item = Preview.Tag as ImageItem;
                ShowIllustPages(tokens, item);
            }
        }

        private async void RelativeIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (Preview.Tag is ImageItem)
            {
                var item = Preview.Tag as ImageItem;
                ShowRelativeInline(tokens, item);
                RelativeNextPage.Visibility = Visibility.Visible;
            }
        }

        private void SubIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            foreach (var illust in SubIllusts.SelectedItems)
            {
                var viewer = new ViewerWindow();
                var page = new IllustImageViewerPage();
                page.UpdateDetail(illust);

                viewer.Title = $"ID: {illust.ID}, {illust.Subject}";
                viewer.Width = 720;
                viewer.Height = 800;
                viewer.Content = page;
                viewer.Show();
            }
        }

        private void RelativeIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            foreach (var illust in RelativeIllusts.SelectedItems)
            {
                var viewer = new ViewerWindow();
                var page = new IllustDetailPage();
                page.UpdateDetail(illust);

                viewer.Title = $"ID: {illust.ID}, {illust.Subject}";
                viewer.Width = 720;
                viewer.Height = 800;
                viewer.Content = page;
                viewer.Show();
            }
        }

        private void RelativePrevPage_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void RelativeNextPage_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (Preview.Tag is ImageItem)
            {
                var item = Preview.Tag as ImageItem;
                var next_url = string.Empty;
                if (RelativeIllustsExpander.Tag is string)
                    next_url = RelativeIllustsExpander.Tag as string;
                ShowRelativeInline(tokens, item, next_url);
            }
        }
    }

}
