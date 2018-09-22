using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Cache;
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
using System.Windows.Threading;
using TheArtOfDev.HtmlRenderer.WPF;

namespace PixivWPF.Pages
{
    /// <summary>
    /// PageTiles.xaml 的交互逻辑
    /// </summary>
    public partial class PageTiles : Page
    {
        class AutoNextStatusChecker
        {
            private int invokeCount;
            private int  maxCount;
            private ScrollViewer target = null;

            public AutoNextStatusChecker(int count, ScrollViewer scroll)
            {
                invokeCount = 0;
                maxCount = count;
                target = scroll;
            }

            // This method is called by the timer delegate.
            public void CheckStatus(object stateInfo)
            {
                AutoResetEvent autoEvent = (AutoResetEvent)stateInfo;
                invokeCount++;
                if (target.VerticalOffset == target.ScrollableHeight)
                {
                    if (invokeCount >= maxCount)
                    {
                        // Reset the counter and signal Main.
                        //ShowImage();
                        invokeCount = 0;
                        autoEvent.Set();
                    }
                }
            }
        }

        private MetroWindow window = Application.Current.MainWindow as MetroWindow;
        internal ObservableCollection<ImageItem> ImageList = new ObservableCollection<ImageItem>();
        private Setting setting = Setting.Load();
        public PixivPage TargetPage = PixivPage.Recommanded;
        private string NextURL = null;
        private bool UPDATING = false;

        /// <summary>
        /// Create Auto Check Scroll Viewer for automatic load next page
        /// </summary>
        private AutoResetEvent AutoNextEvent = null;
        AutoNextStatusChecker  AutoNextChecker = null;
        TimerCallback TimerCheck = null;
        private Timer AutoNextTimer = null;
        private int invokeCount;
        private int  maxCount=20;


        public void CheckStatus(object stateInfo)
        {
            try
            {
                AutoResetEvent autoEvent = (AutoResetEvent)stateInfo;
                if (ImageTilesViewer.VerticalOffset == ImageTilesViewer.ScrollableHeight)
                {
                    invokeCount++;
                    if (invokeCount >= maxCount && !string.IsNullOrEmpty(setting.AccessToken))
                    {
                        // Reset the counter and signal Main.
                        ShowImages(TargetPage, NextURL);
                        invokeCount = 0;
                        autoEvent.Set();
                    }
                }
            }
            catch (Exception ex)
            {
                var ret = ex.Message;
            }                
        }
        ////////////////////////////////////////////////////////////////

        internal void UpdateImageTile(Pixeez.Tokens tokens)
        {
            if (UPDATING) return;

            var needUpdate = ImageList.Where(item => item.Source == null);

            new Thread(delegate ()
            {
                var opt = new ParallelOptions();
                opt.MaxDegreeOfParallelism = 10;
                Parallel.ForEach(needUpdate, opt, (item, loopstate, elementIndex) =>
                {
                    item.Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            if (item.Source == null)
                            {
                                item.Source = await item.Thumb.ToImageSource(tokens);
                                ListImageTiles.Items.Refresh();
                            }
                        }
                        catch (Exception ex)
                        {
                            CommonHelper.ShowMessageDialog("ERROR", $"Download Image Failed:\n{ex.Message}");
                        }
                    }));
                });
                UPDATING = false;
            }).Start();
        }

        internal async void ShowRelativeInline(Pixeez.Tokens tokens, ImageItem item)
        {
            try
            {
                PreviewWait.Visibility = Visibility.Visible;

                var next_url = string.Empty;
                var relatives = string.IsNullOrEmpty(next_url) ? await tokens.GetRelatedWorks(item.Illust.Id.Value) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = relatives.next_url ?? string.Empty;

                lblRelativeIllusts.Visibility = Visibility.Collapsed;
                RelativeIllusts.Items.Clear();
                if (relatives.illusts is Array)
                {
                    foreach (var illust in relatives.illusts)
                    {
                        illust.AddTo(RelativeIllusts.Items, relatives.next_url);
                    }
                    RelativeIllusts.UpdateImageTile(tokens);
                    lblRelativeIllusts.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                CommonHelper.ShowMessageDialog("ERROR", ex.Message);
            }
            finally
            {
                PreviewWait.Visibility = Visibility.Collapsed;
            }            
        }

        public PageTiles()
        {
            InitializeComponent();

            ImageList.Clear();
            ListImageTiles.ItemsSource = ImageList;

            RelativeIllusts.Columns = 5;

            AutoNextEvent = new AutoResetEvent(false);
            AutoNextChecker = new AutoNextStatusChecker(maxCount, ImageTilesViewer);
            TimerCheck = CheckStatus;
            AutoNextTimer = new Timer(TimerCheck, AutoNextEvent, 2000, 100);

            ShowImages();
        }

        public void ShowImages(PixivPage target, string page)
        {
            if (TargetPage != target)
            {
                NextURL = null;
                ListImageTiles.Items.Refresh();
                TargetPage = target;
            }

            //if(ListImageTiles.SelectionMode == SelectionMode.Single)
            //    ListImageTiles.SelectedItem = null;
            //else
            //    ListImageTiles.SelectedItems.Clear();
            ListImageTiles.SelectedIndex = -1;
            ImageList.Clear();
            PreviewWait.Visibility = Visibility.Collapsed;

            switch (target)
            {
                case PixivPage.None:
                    break;
                case PixivPage.Recommanded:
                    ShowRecommanded(NextURL);
                    break;
                case PixivPage.Latest:
                    ShowLatest(NextURL);
                    break;
                case PixivPage.Favorite:
                    ShowFavorite(NextURL, 0);
                    break;
                case PixivPage.FavoritePrivate:
                    ShowFavorite(NextURL, 0, true);
                    break;
                case PixivPage.Follow:
                    ShowFollowing(NextURL);
                    break;
                case PixivPage.FollowPrivate:
                    ShowFollowing(NextURL, true);
                    break;
                case PixivPage.My:
                    break;
                case PixivPage.MyWork:
                    break;
                case PixivPage.User:
                    break;
                case PixivPage.UserWork:
                    break;
                case PixivPage.MyBookmark:
                    break;
                case PixivPage.RankingDaily:
                    ShowRanking(NextURL, "daily");
                    break;
                case PixivPage.RankingWeekly:
                    ShowRanking(NextURL, "weekly");
                    break;
                case PixivPage.RankingMonthly:
                    ShowRanking(NextURL, "monthly");
                    break;
            }
        }

        public void ShowImages(PixivPage target = PixivPage.Recommanded, bool IsAppend = false)
        {
            if (TargetPage != target)
            {
                NextURL = null;
                ListImageTiles.Items.Refresh();
                TargetPage = target;
            }
            if (!IsAppend)
            {
                if(ListImageTiles.SelectionMode == SelectionMode.Single)
                {
                    ListImageTiles.SelectedItem = null;
                }
                else
                {
                    ListImageTiles.SelectedItems.Clear();
                }
                ListImageTiles.SelectedIndex = -1;
                ImageList.Clear();
                PreviewWait.Visibility = Visibility.Collapsed;
            }

            switch (target)
            {
                case PixivPage.None:
                    break;
                case PixivPage.Recommanded:
                    ShowRecommanded(NextURL);
                    break;
                case PixivPage.Latest:
                    ShowLatest(NextURL);
                    break;
                case PixivPage.Favorite:
                    ShowFavorite(NextURL, 0);
                    break;
                case PixivPage.FavoritePrivate:
                    ShowFavorite(NextURL, 0, true);
                    break;
                case PixivPage.Follow:
                    ShowFollowing(NextURL);
                    break;
                case PixivPage.FollowPrivate:
                    ShowFollowing(NextURL, true);
                    break;
                case PixivPage.My:
                    break;
                case PixivPage.MyWork:
                    break;
                case PixivPage.User:
                    break;
                case PixivPage.UserWork:
                    break;
                case PixivPage.MyBookmark:
                    break;
                case PixivPage.RankingDaily:
                    ShowRanking(NextURL, "daily");
                    break;
                case PixivPage.RankingWeekly:
                    ShowRanking(NextURL, "weekly");
                    break;
                case PixivPage.RankingMonthly:
                    ShowRanking(NextURL, "monthly");
                    break;
            }
            //UpdateImageTile(tokens);
        }

        public async void ShowRecommanded(string nexturl = null)
        {
            ImageTilesWait.Visibility = Visibility.Visible;
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Visibility = Visibility.Hidden;
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Visibility = Visibility.Visible;
                //var works = await tokens.GetMyFollowingWorksAsync("private");
                var root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRecommendedWorks() : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                if (root == null || root.ranking_illusts == null)
                {
                    tokens = await CommonHelper.ShowLogin();
                    root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRecommendedWorks() : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                }
                nexturl = root.next_url ?? string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Visibility = Visibility.Hidden;

                if (root.illusts != null)
                {
                    ImageTilesWait.Visibility = Visibility.Visible;
                    foreach (var illust in root.illusts)
                    {
                        illust.AddTo(ImageList, nexturl);
                    }
                    ImageTilesWait.Visibility = Visibility.Hidden;
                    if (root.illusts.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTile(tokens);
                }
            }
            catch (Exception ex)
            {
                CommonHelper.ShowMessageDialog("ERROR", ex.Message);
            }
            finally
            {
                ImageTilesWait.Visibility = Visibility.Hidden;
            }
        }

        public async void ShowLatest(string nexturl = null)
        {
            ImageTilesWait.Visibility = Visibility.Visible;
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Visibility = Visibility.Hidden;
            if (tokens == null) return;

            try
            {
                var page = string.IsNullOrEmpty(nexturl) ? 1 : Convert.ToInt32(nexturl);

                ImageTilesWait.Visibility = Visibility.Visible;
                var root = await tokens.GetLatestWorksAsync(page);
                if (root == null)
                {
                    tokens = await CommonHelper.ShowLogin();
                    root = await tokens.GetLatestWorksAsync(page);
                }
                nexturl = root.Pagination.Next.ToString() ?? string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Visibility = Visibility.Hidden;

                if (root != null)
                {
                    ImageTilesWait.Visibility = Visibility.Visible;
                    foreach (var illust in root)
                    {
                        illust.AddTo(ImageList, nexturl);
                    }
                    ImageTilesWait.Visibility = Visibility.Hidden;
                    if (root.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTile(tokens);
                }
            }
            catch(Exception ex)
            {
                CommonHelper.ShowMessageDialog("ERROR", ex.Message);
            }
            finally
            {
                ImageTilesWait.Visibility = Visibility.Hidden;
            }
        }

        public async void ShowFavorite(string nexturl = null, long uid = 0, bool IsPrivate = false)
        {
            ImageTilesWait.Visibility = Visibility.Visible;
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Visibility = Visibility.Hidden;
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Visibility = Visibility.Visible;
                //var works = await tokens.GetMyFollowingWorksAsync("private");
                var condition = IsPrivate ? "private" : "public";
                if (setting.MyInfo != null && uid == 0) uid = (long)setting.MyInfo.Id;
                else if (uid <= 0) return;

                var root = string.IsNullOrEmpty(nexturl) ? await tokens.GetUserFavoriteWorksAsync(uid, condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                if (root == null)
                {
                    tokens = await CommonHelper.ShowLogin();
                    root = string.IsNullOrEmpty(nexturl) ? await tokens.GetUserFavoriteWorksAsync(uid, condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                }
                nexturl = root.next_url ?? string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Visibility = Visibility.Hidden;

                if (root.illusts != null)
                {
                    ImageTilesWait.Visibility = Visibility.Visible;
                    foreach (var illust in root.illusts)
                    {
                        illust.AddTo(ImageList, nexturl);
                    }
                    ImageTilesWait.Visibility = Visibility.Hidden;
                    if (root.illusts.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;

                    UpdateImageTile(tokens);
                }
            }
            catch (Exception ex)
            {
                CommonHelper.ShowMessageDialog("ERROR", ex.Message);
            }
            finally
            {
                ImageTilesWait.Visibility = Visibility.Hidden;
            }
        }

        public async void ShowFollowing(string nexturl = null, bool IsPrivate = false)
        {
            ImageTilesWait.Visibility = Visibility.Visible;
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Visibility = Visibility.Hidden;
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Visibility = Visibility.Visible;
                var condition = IsPrivate ? "private" : "public";
                var root = string.IsNullOrEmpty(nexturl) ? await tokens.GetMyFollowingWorksAsync(condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                if (root == null)
                {
                    tokens = await CommonHelper.ShowLogin();
                    root = string.IsNullOrEmpty(nexturl) ? await tokens.GetMyFollowingWorksAsync(condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                }
                nexturl = root.next_url ?? string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Visibility = Visibility.Hidden;

                if (root.illusts != null)
                {
                    ImageTilesWait.Visibility = Visibility.Visible;
                    foreach (var illust in root.illusts)
                    {
                        illust.AddTo(ImageList, nexturl);
                    }
                    ImageTilesWait.Visibility = Visibility.Hidden;
                    if (root.illusts.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTile(tokens);
                }
            }
            catch (Exception ex)
            {
                CommonHelper.ShowMessageDialog("ERROR", ex.Message);
            }
            finally
            {
                ImageTilesWait.Visibility = Visibility.Hidden;
            }
        }

        public async void ShowRanking(string nexturl = null, string condition = "daily")
        {
            ImageTilesWait.Visibility = Visibility.Visible;
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Visibility = Visibility.Hidden;
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Visibility = Visibility.Visible;
                var page = string.IsNullOrEmpty(nexturl) ? 1 : Convert.ToInt32(nexturl);
                var root = await tokens.GetRankingAllAsync(condition, page);
                if (root == null)
                {
                    tokens = await CommonHelper.ShowLogin();
                    root = await tokens.GetRankingAllAsync(condition, page);
                }
                nexturl = root.Pagination.Next.ToString() ?? string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Visibility = Visibility.Hidden;

                if (root != null)
                {
                    ImageTilesWait.Visibility = Visibility.Visible;
                    foreach (var works in root)
                    {
                        try
                        {
                            foreach (var work in works.Works)
                            {
                                var illust = work.Work;
                                illust.AddTo(ImageList, nexturl);
                            }
                        }
                        catch (Exception ex)
                        {
                            CommonHelper.ShowMessageDialog("ERROR", ex.Message);
                        }
                    }
                    ImageTilesWait.Visibility = Visibility.Hidden;
                    if (root.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTile(tokens);
                }
            }
            catch (Exception ex)
            {
                CommonHelper.ShowMessageDialog("ERROR", ex.Message);
            }
            finally
            {
                ImageTilesWait.Visibility = Visibility.Hidden;
            }
        }

        private async void ImageTiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var idx = ListImageTiles.SelectedIndex;
                if (idx < 0) return;

                var item = ImageList[idx];
                var url = item.Illust.ImageUrls.Large;
                if (string.IsNullOrEmpty(url))
                {
                    url = item.Illust.ImageUrls.Medium;
                }
                PreviewWait.Visibility = Visibility.Visible;

                var tokens = await CommonHelper.ShowLogin();
                //var tokens = Pixeez.Auth.AuthorizeWithAccessToken(item.AccessToken, setting.Proxy, setting.UsingProxy);
                Preview.Source = await url.ToImageSource(tokens);
                Preview.Tag = item.Illust;

                IllustAuthor.Text = item.Illust.User.Name;
                IllustAuthorIcon.Source = await item.Illust.User.GetAvatarUrl().ToImageSource(tokens);
                IllustTitle.Text = item.Illust.Title;

                var style = $"background-color:{Common.Theme.AccentColor.ToHtml()} |important;color:{Common.Theme.TextColor.ToHtml()} |important;margin:4px;text-decoration:none;";
                var html = new StringBuilder();
                foreach (var tag in item.Illust.Tags)
                {
                    html.AppendLine($"<a href=\"https://www.pixiv.net/search.php?s_mode=s_tag_full&word={Uri.EscapeDataString(tag)}\" style=\"{style}\">{tag}</a>");
                }
                IllustTags.BaseStylesheet = $"body{{color:{Common.Theme.TextColor.ToHtml()};background-color:#333;}}";
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

                //IllustDesc.BaseStylesheet = $".t{{color:{Common.Theme.TextColor.ToHtml(false)};background-color:#888 !important;text-decoration:none;}}";
                IllustDesc.BaseStylesheet = $".t{{color:{Common.Theme.TextColor.ToHtml(false)};text-decoration:none;}}a{{text-decoration:none !important;}}";
                //var global_style = $"background-color:#eeeeee !important;color:{Common.Theme.TextColor.ToHtml()} !important;text-decoration:none;";
                var global_style = $"color:{Common.Theme.TextColor.ToHtml(false)} !important;text-decoration:none;";
                //IllustDesc.Text = $"<div style=\"{global_style}\">{item.Illust.Caption}</div>";
                //IllustDesc.Text = $"<div color=\"{Common.Theme.TextColor.ToHtml(false)}\">{item.Illust.Caption}</div>";
                IllustDesc.Text = $"<div class=\"t\">{item.Illust.Caption}</div>";

                //var relatives = await tokens.GetRelatedWorks((long)item.Illust.Id.Value, "");

                lblSubIllusts.Visibility = Visibility.Collapsed;
                SubIllusts.Items.Clear();
                if (item.Illust is Pixeez.Objects.IllustWork)
                {
                    var subset = item.Illust as Pixeez.Objects.IllustWork;
                    if (subset.meta_pages.Count() > 1)
                    {
                        lblSubIllusts.Visibility = Visibility.Visible;
                        foreach (var pages in subset.meta_pages)
                        {
                            pages.AddTo(SubIllusts.Items, item.Illust, item.NextURL);
                        }
                        SubIllusts.UpdateImageTile(tokens);
                    }
                }

                // ShowRelativeInline(tokens, item);
                PreviewWait.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                CommonHelper.ShowMessageDialog("ERROR", ex.Message);
            }
        }

        private void ImageTilesViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            //ShowImages(TargetPage, NextURL);
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

        private async void ActionIllustRelative_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            var idx = ListImageTiles.SelectedIndex;
            if (idx < 0) return;
            var item = ImageList[idx];

            ShowRelativeInline(tokens, item);
        }

        private void ActionOpenIllust_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void ActionSaveIllust_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (SubIllusts.SelectedItems != null && SubIllusts.SelectedItems.Count > 0)
            {
                foreach(var item in SubIllusts.SelectedItems)
                {
                    if(item.Tag is Pixeez.Objects.MetaPages)
                    {
                        var pages = item.Tag as Pixeez.Objects.MetaPages;
                        if(!string.IsNullOrEmpty(pages.ImageUrls.Original))
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

                            await pages.ImageUrls.Original.ToImageFile(tokens, DateTime.Parse(item.Illust.ReuploadedTime));
                        }
                    }
                    SystemSounds.Beep.Play();
                }
            }
            else if(SubIllusts.SelectedItem is Common.ImageItem)
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

                        await pages.ImageUrls.Original.ToImageFile(tokens, dt);
                        SystemSounds.Beep.Play();
                    }
                }
            }
            else
            {
                if (Preview.Tag is Pixeez.Objects.Work)
                {
                    var illust = Preview.Tag as Pixeez.Objects.Work;
                    var images = illust.ImageUrls;
                    var url = images.Original;

                    var dt = DateTime.Now;

                    if (illust is Pixeez.Objects.IllustWork)
                    {
                        var illustset = illust as Pixeez.Objects.IllustWork;
                        if(illustset.meta_pages.Count() > 0)
                            url = illustset.meta_pages[0].ImageUrls.Original;
                        else if(illustset.meta_single_page is Pixeez.Objects.MetaSinglePage)
                            url = illustset.meta_single_page.OriginalImageUrl;

                        dt = illustset.CreatedTime;
                    }
                    else if(illust is Pixeez.Objects.NormalWork)
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

                        await url.ToImageFile(tokens, dt);
                        SystemSounds.Beep.Play();
                    }
                }
            }
        }

        private void RelativeIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void SubIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

    }



}
