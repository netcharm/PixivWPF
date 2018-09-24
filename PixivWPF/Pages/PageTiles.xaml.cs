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

            public AutoNextStatusChecker(int count)
            {
                invokeCount = 0;
                maxCount = count;
                target = null;
            }

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
                if (target is ScrollViewer && target.VerticalOffset == target.ScrollableHeight)
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

        //public static readonly DependencyProperty ItemsProperty =
        //    DependencyProperty.Register("Items", typeof(ObservableCollection<ImageItem>), typeof(ListView), new UIPropertyMetadata(null));

        internal ObservableCollection<ImageItem> ImageList = new ObservableCollection<ImageItem>();
        private Setting setting = Setting.Load();
        public PixivPage TargetPage = PixivPage.Recommanded;
        private string NextURL = null;
        private bool UPDATING = false;
        private Thread UpdateThread = null;

        /// <summary>
        /// Create Auto Check Scroll Viewer for automatic load next page
        /// </summary>
        //private AutoResetEvent AutoNextEvent = null;
        //AutoNextStatusChecker  AutoNextChecker = null;
        //TimerCallback TimerCheck = null;
        //private Timer AutoNextTimer = null;
        //private int invokeCount;
        //private int  maxCount=200;

        //Action onCompleted = () =>
        //{
        //    //On complete action
        //    //ListImageTiles.Items.Refresh();
        //};

        //public void CheckStatus(object stateInfo)
        //{
        //    try
        //    {
        //        AutoResetEvent autoEvent = (AutoResetEvent)stateInfo;
        //        //if (ImageTilesViewer.VerticalOffset == ImageTilesViewer.ScrollableHeight)
        //        //if(UpdateThread is Thread && UpdateThread.IsAlive)
        //        {
        //            invokeCount++;
        //            //if (invokeCount >= maxCount && !string.IsNullOrEmpty(setting.AccessToken))
        //            if (invokeCount >= maxCount)
        //            {                   
        //                // Reset the counter and signal Main.
        //                //ShowImages(TargetPage, NextURL);
        //                //ListImageTiles.Items.Refresh();
        //                //ListImageTiles.ItemsSource = null;
        //                //ListImageTiles.ItemsSource = ImageList;
        //                invokeCount = 0;
        //                autoEvent.Set();
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        var ret = ex.Message;
        //    }                
        //}
        //////////////////////////////////////////////////////////////////

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
                //var tokens = Pixeez.Auth.AuthorizeWithAccessToken(item.AccessToken, setting.Proxy, setting.UsingProxy);
                //Preview.Source = await url.ToImageSource(tokens);
                Preview.Source = await url.LoadImage(tokens);

                IllustAuthor.Text = item.Illust.User.Name;
                //IllustAuthorIcon.Source = await item.Illust.User.GetAvatarUrl().ToImageSource(tokens);
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

        private void OnlyActiveItems(object sender, FilterEventArgs e)
        {
            e.Accepted = false;
           
            var item = e.Item as ImageItem;
            if (item.Source == null) return;

            e.Accepted = true;
        }

        internal void UpdateImageTiles(Pixeez.Tokens tokens)
        {
            if (UPDATING) return;

            var needUpdate = ImageList.Where(item => item.Source == null);

            UpdateThread = new Thread(() =>
            {
                try
                {
                    var opt = new ParallelOptions();
                    opt.MaxDegreeOfParallelism = 15;
                    opt.TaskScheduler = TaskScheduler.Current;
                    var ret = Parallel.ForEach(needUpdate, opt, (item, loopstate, elementIndex) =>
                    {
                        item.Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            try
                            {
                                if (item.Source == null)
                                {
                                    //item.Source = await item.Thumb.ToImageSource(tokens);
                                    item.Source = await item.Thumb.LoadImage(tokens);

                                    //ListImageTiles.Items.DeferRefresh();
                                    ListImageTiles.Items.Refresh();
                                    //ListImageTiles.ItemsSource = null;
                                    //ListImageTiles.ItemsSource = ImageList;

                                    //ListImageTiles.InvalidateProperty(dp);
                                }
                            }
                            catch (Exception ex)
                            {
                                CommonHelper.ShowMessageDialog("ERROR", $"Download Image Failed:\n{ex.Message}");
                            }
                        }));
                    });
                    if (ret.IsCompleted)
                    {
                        UPDATING = !ret.IsCompleted;
                    }
                }
                finally
                {
                    UPDATING = false;
                }
            });
            UpdateThread.Start();

            //new Thread(delegate ()
            //{
            //    var opt = new ParallelOptions();
            //    opt.MaxDegreeOfParallelism = 10;
            //    Parallel.ForEach(needUpdate, opt, (item, loopstate, elementIndex) =>
            //    {
            //        item.Dispatcher.BeginInvoke(new Action(async () =>
            //        {
            //            try
            //            {
            //                if (item.Source == null)
            //                {
            //                    item.Source = await item.Thumb.ToImageSource(tokens);
            //                    //ListImageTiles.Items.Refresh();
            //                }
            //            }
            //            catch (Exception ex)
            //            {
            //                CommonHelper.ShowMessageDialog("ERROR", $"Download Image Failed:\n{ex.Message}");
            //            }
            //        }));
            //    });
            //    UPDATING = false;
            //}).Start();
        }

        internal void ShowIllustPages(Pixeez.Tokens tokens, ImageItem item, int start=0, int count=30)
        {
            try
            {
                PreviewWait.Visibility = Visibility.Visible;

                SubIllustsPanel.Visibility = Visibility.Collapsed;
                SubIllusts.Items.Clear();
                if (item.Illust is Pixeez.Objects.IllustWork)
                {
                    var subset = item.Illust as Pixeez.Objects.IllustWork;
                    if (subset.meta_pages.Count() > 1)
                    {
                        SubIllustsPanel.Visibility = Visibility.Visible;

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

                        SubIllustsPanel.InvalidateVisual();
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

        internal async void ShowRelativeInline(Pixeez.Tokens tokens, ImageItem item)
        {
            try
            {
                PreviewWait.Visibility = Visibility.Visible;

                var next_url = string.Empty;
                var relatives = string.IsNullOrEmpty(next_url) ? await tokens.GetRelatedWorks(item.Illust.Id.Value) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = relatives.next_url ?? string.Empty;

                RelativeIllusts.Items.Clear();
                if (relatives.illusts is Array)
                {
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

        public PageTiles()
        {
            InitializeComponent();

            UpdateTheme();

            ImageList.Clear();
            ListImageTiles.ItemsSource = ImageList;

            RelativeIllusts.Columns = 5;

            //AutoNextEvent = new AutoResetEvent(false);
            //AutoNextChecker = new AutoNextStatusChecker(maxCount);
            //TimerCheck = CheckStatus;
            //AutoNextTimer = new Timer(TimerCheck, AutoNextEvent, 2000, 100);

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
                case PixivPage.RankingDay:
                    ShowRanking(NextURL, "day");
                    break;
                case PixivPage.RankingDayMale:
                    ShowRanking(NextURL, "day_male");
                    break;
                case PixivPage.RankingDayFemale:
                    ShowRanking(NextURL, "day_female");
                    break;
                case PixivPage.RankingDayR18:
                    ShowRanking(NextURL, "day_r18");
                    break;
                case PixivPage.RankingDayMaleR18:
                    ShowRanking(NextURL, "day_male_r18");
                    break;
                case PixivPage.RankingDayFemaleR18:
                    ShowRanking(NextURL, "day_female_r18");
                    break;
                case PixivPage.RankingWeek:
                    ShowRanking(NextURL, "week");
                    break;
                case PixivPage.RankingWeekOriginal:
                    ShowRanking(NextURL, "week_original");
                    break;
                case PixivPage.RankingWeekRookie:
                    ShowRanking(NextURL, "week_rookie");
                    break;
                case PixivPage.RankingWeekR18:
                    ShowRanking(NextURL, "week_r18");
                    break;
                case PixivPage.RankingWeekR18G:
                    ShowRanking(NextURL, "week_r18g");
                    break;
                case PixivPage.RankingMonth:
                    ShowRanking(NextURL, "month");
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
                ListImageTiles.SelectedIndex = -1;
                ImageList.Clear();
                PreviewWait.Visibility = Visibility.Collapsed;
            }
            else if(ImageList.Count >= 90)
            {
                ListImageTiles.SelectedIndex = -1;
                var items = new ObservableCollection<ImageItem>();
                foreach (var item in ImageList.Skip(30))
                {
                    items.Add(item);
                }
                ImageList = items;
                ListImageTiles.ItemsSource = items;
                ListImageTiles.Items.Refresh();
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
                case PixivPage.RankingDay:
                    ShowRanking(NextURL, "day");
                    break;
                case PixivPage.RankingDayMale:
                    ShowRanking(NextURL, "day_male");
                    break;
                case PixivPage.RankingDayFemale:
                    ShowRanking(NextURL, "day_female");
                    break;
                case PixivPage.RankingDayR18:
                    ShowRanking(NextURL, "day_r18");
                    break;
                case PixivPage.RankingDayMaleR18:
                    ShowRanking(NextURL, "day_male_r18");
                    break;
                case PixivPage.RankingDayFemaleR18:
                    ShowRanking(NextURL, "day_female_r18");
                    break;
                case PixivPage.RankingWeek:
                    ShowRanking(NextURL, "week");
                    break;
                case PixivPage.RankingWeekOriginal:
                    ShowRanking(NextURL, "week_original");
                    break;
                case PixivPage.RankingWeekRookie:
                    ShowRanking(NextURL, "week_rookie");
                    break;
                case PixivPage.RankingWeekR18:
                    ShowRanking(NextURL, "week_r18");
                    break;
                case PixivPage.RankingWeekR18G:
                    ShowRanking(NextURL, "week_r18g");
                    break;
                case PixivPage.RankingMonth:
                    ShowRanking(NextURL, "month");
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
                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
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
                    UpdateImageTiles(tokens);
                }
            }
            catch(Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
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

                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
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
                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                ImageTilesWait.Visibility = Visibility.Hidden;
            }
        }

        public async void ShowRankingAll(string nexturl = null, string condition = "daily")
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
                            ex.Message.ShowMessageBox("ERROR");
                        }
                    }
                    ImageTilesWait.Visibility = Visibility.Hidden;
                    if (root.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                ImageTilesWait.Visibility = Visibility.Hidden;
            }
        }

        public async void ShowRanking(string nexturl = null, string condition = "day")
        {
            ImageTilesWait.Visibility = Visibility.Visible;
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Visibility = Visibility.Hidden;
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Visibility = Visibility.Visible;
                //var works = await tokens.GetMyFollowingWorksAsync("private");
                var root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRankingAsync(condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                if (root == null || root.ranking_illusts == null)
                {
                    tokens = await CommonHelper.ShowLogin();
                    root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRankingAsync(condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
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
                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                ImageTilesWait.Visibility = Visibility.Hidden;
            }
        }

        private void ImageTiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var idx = ListImageTiles.SelectedIndex;
                if (idx < 0) return;

                var item = ImageList[idx];
                UpdateDetail(item);
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
        }

        private void RelativeIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void SubIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

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

        private async void ActionIllustPages_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            //var idx = ListImageTiles.SelectedIndex;
            //if (idx < 0) return;
            //var item = ImageList[idx];
            //ShowIllustPages(tokens, item);

            if (Preview.Tag is ImageItem)
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
            //ShowRelativeInline(tokens, item);

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

                            var is_meta_single_page = illust.PageCount==1 ? true : false;
                            await pages.ImageUrls.Original.ToImageFile(tokens, dt, is_meta_single_page);
                        }
                    }
                    SystemSounds.Beep.Play();
                }
            }
            else if(SubIllusts.SelectedItem is ImageItem)
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
                if (Preview.Tag is ImageItem)
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

            if (Preview.Tag is ImageItem)
            {
                var item = Preview.Tag as ImageItem;
                if (item.Illust is Pixeez.Objects.Work)
                {
                    IllustsSaveProgress.Visibility = Visibility.Visible;
                    IllustsSaveProgress.Value = 0;
                    IProgress<int> progress = new Progress<int>(i => { IllustsSaveProgress.Value = i; });

                    var illust = item.Illust;

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
        }

        private async void SubIllustPagesNav_Click(object sender, RoutedEventArgs e)
        {
            if(sender == btnSubIllustPrevPages || sender==btnSubIllustNextPages)
            {
                var btn = sender as Button;
                if(btn.Tag is int)
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
            //var idx = RelativeIllusts.SelectedIndex;
            //if (idx < 0) return;
            //var item = ImageList[idx];

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
            }
        }

        private void SubIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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

        private void RelativeIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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

    }



}
