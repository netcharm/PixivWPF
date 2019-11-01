using MahApps.Metro.Controls;
using MahApps.Metro.IconPacks;
using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PixivWPF.Pages
{
    /// <summary>
    /// PageTiles.xaml 的交互逻辑
    /// </summary>
    public partial class TilesPage : Page
    {
        private Window window = null;
        private IllustDetailPage detail_page = new IllustDetailPage();

        internal List<long> ids = new List<long>();
        internal ObservableCollection<ImageItem> ImageList = new ObservableCollection<ImageItem>();

        private Setting setting = Setting.Load();
        public PixivPage TargetPage = PixivPage.Recommanded;
        private string NextURL = null;
        private bool UPDATING = false;
        private Task<bool> UpdateTask = null;

        public DateTime SelectedDate { get; set; } = DateTime.Now;

        internal Task lastTask = null;
        internal CancellationTokenSource cancelTokenSource;
        internal CancellationToken cancelToken;

        public void UpdateTheme()
        {
            detail_page.UpdateTheme();
        }

        private void OnlyActiveItems(object sender, FilterEventArgs e)
        {
            e.Accepted = false;
           
            var item = e.Item as ImageItem;
            if (item.Source == null) return;

            e.Accepted = true;
        }

        internal void UpdateImageTilesTask(Pixeez.Tokens tokens)
        {
            if (UPDATING) return;

            var needUpdate = ImageList.Where(item => item.Source == null);
            if (needUpdate.Count() > 0)
            {
                //using (ListImageTiles.Items.DeferRefresh())
                {
                    UpdateTask = new Task<bool>(() =>
                    {
                        bool result = true;
                        UPDATING = result;
                        try
                        {
                            var opt = new ParallelOptions();
                            opt.MaxDegreeOfParallelism = 15;
                            opt.TaskScheduler = TaskScheduler.Current;
                            opt.CancellationToken = cancelToken;
                            var ret = Parallel.ForEach(needUpdate, opt, (item, loopstate, elementIndex) =>
                            {
                                using (cancelToken.Register(Thread.CurrentThread.Abort))
                                {
                                    if (cancelToken.IsCancellationRequested)
                                    {
                                        //cancelTokenSource.Cancel(true);
                                        //cancelToken.ThrowIfCancellationRequested();
                                        opt.CancellationToken.ThrowIfCancellationRequested();
                                        return;
                                    }

                                    //item.Dispatcher.BeginInvoke((Action)async delegate() 
                                    //{
                                    //    try
                                    //    {
                                    //        if (item.Source == null)
                                    //        {
                                    //            if(item.Illust.PageCount<=1) item.BadgeValue = null;
                                    //            item.Source = await item.Thumb.LoadImage(tokens);
                                    //        }
                                    //    }
                                    //    catch (Exception ex)
                                    //    {
                                    //        $"Download Image Failed:\n{ex.Message}".ShowMessageBox("ERROR");
                                    //    }
                                    //});

                                    item.Dispatcher.BeginInvoke(new Action(async () =>
                                    {
                                        try
                                        {
                                            if (item.Source == null)
                                            {
                                                if(item.Illust.PageCount<=1) item.BadgeValue = null;
                                                item.Source = await item.Thumb.LoadImage(tokens);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            $"Download Image Failed:\n{ex.Message}".ShowMessageBox("ERROR");
                                        }
                                    }));
                                }
                            });
                            if (ret.IsCompleted)
                            {
                                result = !ret.IsCompleted;
                            }
                        }
                        finally
                        {
                            result = false;
                        }
                        UPDATING = result;
                        return (result);
                    });
                    UpdateTask.Start();
                }
            }
        }

        internal async void UpdateImageTiles(Pixeez.Tokens tokens)
        {
            try
            {
                if (lastTask is Task && lastTask.Status == TaskStatus.Running)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    lastTask.Wait();
                    //cancelTokenSource.Cancel(true);
                    //lastTask.Wait(500, cancelToken);
                    //cancelTokenSource = new CancellationTokenSource();
                    //cancelTokenSource.CancelAfter(30000);
                    //cancelToken = cancelTokenSource.Token;
                }

                if (lastTask == null || (lastTask is Task && (lastTask.IsCanceled || lastTask.IsCompleted || lastTask.IsFaulted)))
                {
                    lastTask = new Task(() =>
                    {
                        UpdateImageTilesTask(tokens);
                    }, cancelTokenSource.Token, TaskCreationOptions.None);
                    lastTask.RunSynchronously();
                    //lastTask.Start();
                    await lastTask;
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
            }
        }

        public TilesPage()
        {
            InitializeComponent();

            window = this.GetActiveWindow();

            cancelTokenSource = new CancellationTokenSource();
            //cancelTokenSource.CancelAfter(30000);
            cancelToken = cancelTokenSource.Token;

            IllustDetail.Content = detail_page;

            UpdateTheme();

            ids.Clear();
            ImageList.Clear();
            ListImageTiles.ItemsSource = ImageList;

            ShowImages();
        }

        public void ShowImages(PixivPage target = PixivPage.Recommanded, bool IsAppend = false)
        {
            if (window == null) window = this.GetActiveWindow();

            if (target != PixivPage.My && TargetPage != target)
            {
                NextURL = null;
                TargetPage = target;
                ids.Clear();
                ImageList.Clear();
            }
            if (target != PixivPage.My && !IsAppend)
            {
                NextURL = null;
                ids.Clear();
                ImageList.Clear();
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
                case PixivPage.TrendingTags:
                    ShowTrendingTags(NextURL);
                    break;
                case PixivPage.Feeds:
#if DEBUG
                    ShowFeeds(NextURL);
#endif
                    break;
                case PixivPage.Favorite:
                    ShowFavorite(NextURL, false);
                    break;
                case PixivPage.FavoritePrivate:
                    ShowFavorite(NextURL, true);
                    break;
                case PixivPage.Follow:
                    ShowFollowing(NextURL);
                    break;
                case PixivPage.FollowPrivate:
                    ShowFollowing(NextURL, true);
                    break;
                case PixivPage.My:
                    ShowUser(0, true);
                    break;
                case PixivPage.MyWork:
                    //ShowFavorite(NextURL, true);
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
                case PixivPage.RankingDayManga:
                    ShowRanking(NextURL, "day_manga");
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
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();
                Pixeez.Objects.RecommendedRootobject root = null;
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRecommendedWorks("illust", true, "for_ios", "20", "1", "0", true) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                }
                else if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRecommendedWorks("illust", true, "for_ios", "200", "200", "0", true) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                }
                else if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRecommendedWorks("illust", true, "for_ios", "2000", "1000", "0", true) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                }
                else if (Keyboard.Modifiers == ModifierKeys.Windows)
                {
                    root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRecommendedWorks("illust", true, "for_ios", "2000", "2000", "0", true) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                }
                else
                {
                    root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRecommendedWorks() : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                }
                nexturl = root.next_url ?? string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Hide();

                if (root.illusts != null)
                {
                    ImageTilesWait.Show();
                    foreach (var illust in root.illusts)
                    {
                        if (!ids.Contains(illust.Id.Value))
                        {
                            ids.Add(illust.Id.Value);
                            illust.AddTo(ImageList, nexturl);
                        }
                    }
                    ImageTilesWait.Hide();
                    if (root.illusts.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                if (ex is NullReferenceException)
                {
                    "No Result".ShowMessageBox("INFO");
                }
                else
                {
                    ex.Message.ShowMessageBox("ERROR");
                }
                //setting.AccessToken = string.Empty;
                //setting.Update = 0;
            }
            finally
            {
                ImageTilesWait.Hide();
            }
        }

        public async void ShowLatest(string nexturl = null)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                var page_no = string.IsNullOrEmpty(nexturl) ? 1 : Convert.ToInt32(nexturl);

                ImageTilesWait.Show();
                var root = await tokens.GetLatestWorksAsync(page_no);
                //var root = await tokens.GetLatestWorksNewAsync(page_no);
                nexturl = root.Pagination.Next.ToString() ?? string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Hide();

                if (root != null)
                {
                    ImageTilesWait.Show();
                    foreach (var illust in root)
                    {
                        if (!ids.Contains(illust.Id.Value))
                        {
                            ids.Add(illust.Id.Value);
                            illust.AddTo(ImageList, nexturl);
                        }
                    }
                    ImageTilesWait.Hide();
                    if (root.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTiles(tokens);
                }
            }
            catch(Exception ex)
            {
                if (ex is NullReferenceException)
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
                ImageTilesWait.Hide();
            }
        }

        public async void ShowTrendingTags(string nexturl = null)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                var page = string.IsNullOrEmpty(nexturl) ? 1 : Convert.ToInt32(nexturl);

                ImageTilesWait.Show();
                var root = await tokens.GetTrendingTagsIllustAsync();
                nexturl = string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Hide();

                if (root != null)
                {
                    ImageTilesWait.Show();
                    foreach (var tag in root.tags)
                    {
                        if (!ids.Contains(tag.illust.Id.Value))
                        {
                            ids.Add(tag.illust.Id.Value);
                            tag.illust.AddTo(ImageList, nexturl);
                        }                        
                    }
                    ImageTilesWait.Hide();
                    if (root.tags.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                if (ex is NullReferenceException)
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
                ImageTilesWait.Hide();
            }
        }

        public async void ShowFeeds(long uid, string nexturl = null)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                var page = string.IsNullOrEmpty(nexturl) ? 1 : Convert.ToInt32(nexturl);

                ImageTilesWait.Show();
                var root = await tokens.GetMyFeedsAsync(uid);
                nexturl = string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Hide();

                if (root != null)
                {
                    ImageTilesWait.Show();
                    foreach (var feed in root)
                    {
                        if (!ids.Contains(feed.User.Id.Value))
                        {
                            ids.Add(feed.User.Id.Value);
                            feed.User.AddTo(ImageList);
                        }
                    }
                    ImageTilesWait.Hide();
                    if (root.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                if (ex is NullReferenceException)
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
                ImageTilesWait.Hide();
            }
        }

        public async void ShowFeeds(string nexturl = null)
        {
            ImageTilesWait.Show();
            var force = setting.MyInfo is Pixeez.Objects.User ? false : true;
            var tokens = await CommonHelper.ShowLogin(force);
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                var page = string.IsNullOrEmpty(nexturl) ? 1 : Convert.ToInt32(nexturl);
                var uid = setting.MyInfo is Pixeez.Objects.User ? setting.MyInfo.Id.Value : 0;

                ImageTilesWait.Show();
                var root = await tokens.GetMyFeedsAsync(uid);
                nexturl = string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Hide();

                if (root != null)
                {
                    ImageTilesWait.Show();
                    foreach (var feed in root)
                    {
                        if (!ids.Contains(feed.User.Id.Value))
                        {
                            ids.Add(feed.User.Id.Value);
                            feed.User.AddTo(ImageList);
                        }
                    }
                    ImageTilesWait.Hide();
                    if (root.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                if (ex is NullReferenceException)
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
                ImageTilesWait.Hide();
            }
        }

        public async void ShowFavorite(string nexturl = null, bool IsPrivate = false)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin(setting.MyInfo == null && IsPrivate);
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                long uid = 0;

                ImageTilesWait.Show();
                var condition = IsPrivate ? "private" : "public";

                if (setting.MyInfo != null && uid == 0) uid = setting.MyInfo.Id.Value;
                else if (uid <= 0) return;

                var root = string.IsNullOrEmpty(nexturl) ? await tokens.GetUserFavoriteWorksAsync(uid, condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                nexturl = root.next_url ?? string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Hide();

                if (root.illusts != null)
                {
                    ImageTilesWait.Show();
                    foreach (var illust in root.illusts)
                    {
                        if (!ids.Contains(illust.Id.Value))
                        {
                            ids.Add(illust.Id.Value);
                            illust.AddTo(ImageList, nexturl);
                        }
                    }
                    ImageTilesWait.Hide();
                    if (root.illusts.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                if (ex is NullReferenceException)
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
                ImageTilesWait.Hide();
            }
        }

        public async void ShowFollowing(string nexturl = null, bool IsPrivate = false)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();
                var condition = IsPrivate ? "private" : "public";
                var root = string.IsNullOrEmpty(nexturl) ? await tokens.GetMyFollowingWorksAsync(condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                nexturl = root.next_url ?? string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Hide();

                if (root.illusts != null)
                {
                    ImageTilesWait.Show();
                    foreach (var illust in root.illusts)
                    {
                        if (!ids.Contains(illust.Id.Value))
                        {
                            ids.Add(illust.Id.Value);
                            illust.AddTo(ImageList, nexturl);
                        }
                    }
                    ImageTilesWait.Hide();
                    if (root.illusts.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                if (ex is NullReferenceException)
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
                ImageTilesWait.Hide();
            }
        }

        public async void ShowRankingAll(string nexturl = null, string condition = "daily")
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();
                var page = string.IsNullOrEmpty(nexturl) ? 1 : Convert.ToInt32(nexturl);
                var root = await tokens.GetRankingAllAsync(condition, page);
                nexturl = root.Pagination.Next.ToString() ?? string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Hide();

                if (root != null)
                {
                    ImageTilesWait.Show();
                    foreach (var works in root)
                    {
                        try
                        {
                            foreach (var work in works.Works)
                            {
                                var illust = work.Work;
                                if (!ids.Contains(illust.Id.Value))
                                {
                                    ids.Add(illust.Id.Value);
                                    illust.AddTo(ImageList, nexturl);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ex.Message.ShowMessageBox("ERROR");
                        }
                    }
                    ImageTilesWait.Hide();
                    if (root.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                if (ex is NullReferenceException)
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
                ImageTilesWait.Hide();
            }
        }

        public async void ShowRanking(string nexturl = null, string condition = "day")
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();
                var date = CommonHelper.SelectedDate.Date == DateTime.Now.Date ? string.Empty : (CommonHelper.SelectedDate - TimeSpan.FromDays(1)).ToString("yyyy-MM-dd");
                var root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRankingAsync(condition, 1, 30, date) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                int count = 2;
                while (count <= 7 && (root.illusts == null || root.illusts.Length <= 0))
                {
                    date = (CommonHelper.SelectedDate - TimeSpan.FromDays(count)).ToString("yyyy-MM-dd");
                    root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRankingAsync(condition, 1, 30, date) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                    count++;
                }
                nexturl = root.next_url ?? string.Empty;
                NextURL = nexturl;
                ImageTilesWait.Hide();

                if (root.illusts != null)
                {
                    ImageTilesWait.Show();
                    foreach (var illust in root.illusts)
                    {
                        if (!ids.Contains(illust.Id.Value))
                        {
                            ids.Add(illust.Id.Value);
                            illust.AddTo(ImageList, nexturl);
                        }
                    }
                    ImageTilesWait.Hide();
                    if (root.illusts.Count() > 0 && ListImageTiles.SelectedIndex < 0) ListImageTiles.SelectedIndex = 0;
                    UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                if (ex is NullReferenceException)
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
                ImageTilesWait.Hide();
            }
        }

        public async void ShowUser(long uid, bool IsPrivate=false)
        {
            var force = uid == 0 && setting.MyInfo is Pixeez.Objects.User ? false : true;
            var tokens = await CommonHelper.ShowLogin(force);
            if (tokens == null) return;

            if ((IsPrivate || uid == 0) && setting.MyInfo is Pixeez.Objects.User)
            {
                CommonHelper.Cmd_OpenIllust.Execute(setting.MyInfo);
            }
            else
            {
                var users = await tokens.GetUsersAsync(uid);
                Pixeez.Objects.User user = null;
                if (users is List<Pixeez.Objects.User>)
                {
                    foreach (var u in users)
                    {
                        user = u;
                        break;
                    }
                }
                if (user is Pixeez.Objects.User && uid == user.Id.Value)
                {
                    CommonHelper.Cmd_OpenIllust.Execute(user);
                }
            }
        }

        private void ImageTiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var idx = ListImageTiles.SelectedIndex;
                if (idx < 0) return;

                var item = ImageList[idx];

                bool download = item.Illust.GetOriginalUrl().IsPartDownloaded();
                if (item.IsDownloaded != download)
                {
                    item.IsDownloaded = download;
                    ImageList.UpdateTiles(item);
                    ListImageTiles.SelectedIndex = idx;
                }

                detail_page.UpdateDetail(item);

                //UpdateDetail(item);
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
        }

        private void ListImageTiles_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ListImageTiles.SelectedItem != null)
                {

                }
            }
            else if (e.Key == Key.Down || e.Key == Key.Right || e.Key == Key.PageDown)
            {
                if (ListImageTiles.SelectedIndex >= ListImageTiles.Items.Count - 1)
                {
                    ShowImages(TargetPage, true);
                }
                else if (ListImageTiles.Items.CurrentPosition >= ListImageTiles.Items.Count - 1)
                {
                    ShowImages(TargetPage, true);
                }
            }
            else if (e.Key == Key.Home)
            {
                if(ListImageTiles.Items.Count > 0)
                {
                    ListImageTiles.SelectedIndex = 0;
                }
            }
            else if (e.Key == Key.End)
            {
                if (ListImageTiles.Items.Count > 0)
                {
                    ListImageTiles.SelectedIndex = ListImageTiles.Items.Count - 1;
                }
            }
            else if (e.Key == Key.S && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                if(ListImageTiles.SelectedItem is ImageItem)
                {
                    var item = ListImageTiles.SelectedItem as ImageItem;
                    if (item.Illust is Pixeez.Objects.Work)
                    {
                        var illust = item.Illust;
                        var url = illust.GetOriginalUrl();
                        var dt = illust.GetDateTime();
                        var is_meta_single_page = illust.PageCount==1 ? true : false;
                        if (!string.IsNullOrEmpty(url))
                        {
                            url.SaveImage(illust.GetThumbnailUrl(), dt, is_meta_single_page);
                        }
                    }
                }
            }
        }

        private void ListImageTiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //var originalSource = (DependencyObject)e.OriginalSource;
            //while ((originalSource != null) && !(originalSource is ListViewItem)) originalSource = VisualTreeHelper.GetParent(originalSource);
            //if (originalSource == null) return;

            FrameworkElement originalSource = e.OriginalSource as FrameworkElement;
            FrameworkElement source = e.Source as FrameworkElement;

            if(originalSource.Name.Equals("Arrow", StringComparison.CurrentCultureIgnoreCase))
            {
                ShowImages(TargetPage, true);
            }
            else if(source == ListImageTiles)
            {
                ShowImages(TargetPage, true);
            }
            //if (originalSource.DataContext != source.DataContext)
            //{
            //    ShowImages(TargetPage, true);
            //}
        }

        private void ListImageTiles_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ListImageTiles.Items != null && ListImageTiles.Items.Count > 0)
            {
                if (e.Delta < 0 && (Keyboard.IsKeyDown(Key.LeftCtrl)|| Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    ShowImages(TargetPage, true);
                    e.Handled = true;
                }
                return;

                //var last = ListImageTiles.Items[ListImageTiles.Items.Count-1];
                //if(last is ImageItem)
                //{
                //    var item = last as ImageItem;
                //    var vcs = ListImageTiles.GetVisualChildren<Image>();
                //
                //    if (vcs != null)
                //    {
                //        foreach(var vc in vcs)
                //        {
                //            if(vc.Tag is Pixeez.Objects.Work)
                //            {
                //                var illust = vc.Tag as Pixeez.Objects.Work;
                //                if(illust.Id == item.Illust.Id)
                //                {
                //                    if (e.Delta < 0)
                //                    {
                //                        ShowImages(TargetPage, true);
                //                    }
                //                    break;
                //                }
                //            }
                //        }
                //    }
                //}
            }
        }

        private void TileImage_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if (sender is Image && e.Property != null)
            {
                var image = sender as Image;
                if (e.Property.Name.Equals("Source", StringComparison.CurrentCultureIgnoreCase))
                {
                    //var mask = image.FindName("PART_Mask") as Border;
                    var progressObj = image.FindName("PART_Progress");
                    if (progressObj is ProgressRing)
                    {
                        var progress = progressObj as ProgressRing;
                        if (image.Source == null)
                        {
                            progress.Show();
                            //if (mask != null) mask.Opacity = 0.67;
                        }
                        else
                        {
                            //if (mask != null) mask.Opacity = 0.13;
                            progress.Hide();
                        }
                    }
                }
            }
            else if (sender is PackIconModern && e.Property != null)
            {
                var image = sender as PackIconModern;
                if (e.Property.Name.Equals("IsDownloaded", StringComparison.CurrentCultureIgnoreCase))
                {
                    var download = image.FindName("PART_IllustDownloaded");
                    if (download is PackIconModern)
                    {

                    }
                }
            }
        }

        private void ImageTilesViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {

        }

    }
}
