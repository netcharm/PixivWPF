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

        internal string lastSelectedId = string.Empty;
        internal List<long> ids = new List<long>();
        internal ObservableCollection<ImageItem> ImageList = new ObservableCollection<ImageItem>();

        private Setting setting = Application.Current.LoadSetting();
        public PixivPage TargetPage = PixivPage.None;
        private PixivPage LastPage = PixivPage.None;
        private string NextURL = null;

        public DateTime SelectedDate { get; set; } = DateTime.Now;

        internal Task lastTask = null;
        internal CancellationTokenSource cancelTokenSource;

        internal void UpdateTheme()
        {
            if (detail_page is IllustDetailPage)
                detail_page.UpdateTheme();
        }

        public async void UpdateIllustTagsAsync()
        {
            try
            {
                if (IllustDetail.Content is IllustDetailPage)
                {
                    await new Action(() =>
                    {
                        var detail = IllustDetail.Content as IllustDetailPage;
                        detail.UpdateIllustTags();
                    }).InvokeAsync();
                }
            }
            catch (Exception) { }
        }

        public void UpdateIllustTags()
        {
            UpdateIllustTagsAsync();
        }

        public async void UpdateIllustDescAsync()
        {
            try
            {
                if (IllustDetail.Content is IllustDetailPage)
                {
                    await new Action(() =>
                    {
                        var detail = IllustDetail.Content as IllustDetailPage;
                        detail.UpdateIllustDesc();
                    }).InvokeAsync();
                }
            }
            catch (Exception) { }
        }

        public void UpdateIllustDesc()
        {
            UpdateIllustDescAsync();
        }

        public async void UpdateWebContentAsync()
        {
            try
            {
                if (IllustDetail.Content is IllustDetailPage)
                {
                    await new Action(() =>
                    {
                        var detail = IllustDetail.Content as IllustDetailPage;
                        detail.UpdateWebContent();
                    }).InvokeAsync();
                }
            }
            catch (Exception) { }
        }

        public void UpdateWebContent()
        {
            UpdateWebContentAsync();
        }

        public void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            if (ImageList is ObservableCollection<ImageItem>)
            {
                ImageList.UpdateDownloadStateAsync();
            }
        }

        public async void UpdateDownloadStateAsync(int? illustid = null, bool? exists = null)
        {
            await Task.Run(() =>
            {
                UpdateDownloadState(illustid, exists);
            });
        }

        public async void UpdateLikeStateAsync(int illustid = -1, bool is_user = false)
        {
            await Task.Run(() =>
            {
                ImageList.UpdateLikeState(illustid, is_user);
                //UpdateLikeState(illustid);
            });
        }

        private void OnlyActiveItems(object sender, FilterEventArgs e)
        {
            e.Accepted = false;

            var item = e.Item as ImageItem;
            if (item.Source == null) return;

            e.Accepted = true;
        }

        internal string GetLastSelectedID()
        {
            string id = lastSelectedId;
            if (ListImageTiles.Items.Count > 0)
            {
                if (ListImageTiles.SelectedIndex == 0 && string.IsNullOrEmpty(lastSelectedId))
                    lastSelectedId = (ListImageTiles.Items[0] as ImageItem).ID;
                id = ListImageTiles.SelectedItem is ImageItem ? (ListImageTiles.SelectedItem as ImageItem).ID : lastSelectedId;
            }
            return (id);
        }

        protected internal async void UpdateTilesThumb()
        {
            this.DoEvents();
            lastTask = await ImageList.UpdateTilesThumb(lastTask, cancelTokenSource, 5);
        }

        public void UpdateTiles()
        {
            ShowImages(TargetPage, false, GetLastSelectedID());
        }

        private void KeepLastSelected(string id)
        {
            if (ListImageTiles.Items.Count > 0)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    foreach (var item in ListImageTiles.Items)
                    {
                        if (item is ImageItem)
                        {
                            var ID = (item as ImageItem).ID;
                            if (ID.Equals(id, StringComparison.CurrentCultureIgnoreCase))
                            {
                                ListImageTiles.SelectedItem = item;
                                ListImageTiles.ScrollIntoView(ListImageTiles.SelectedItem);
                                break;
                            }
                        }
                    }
                }
                if (ListImageTiles.SelectedIndex < 0)
                {
                    ListImageTiles.SelectedIndex = 0;
                    ListImageTiles.ScrollIntoView(ListImageTiles.SelectedItem);
                }
            }
        }
        
        public void PrevIllust()
        {
            if (this is TilesPage)
            {
                if (ListImageTiles.Items.IsCurrentBeforeFirst)
                    ListImageTiles.Items.MoveCurrentToLast();
                else
                    ListImageTiles.Items.MoveCurrentToPrevious();
                ListImageTiles.ScrollIntoView(ListImageTiles.SelectedItem);
            }
        }

        public void NextIllust()
        {
            if (this is TilesPage)
            {
                if (ListImageTiles.Items.IsCurrentAfterLast)
                    ListImageTiles.Items.MoveCurrentToFirst();
                else
                    ListImageTiles.Items.MoveCurrentToNext();
                ListImageTiles.ScrollIntoView(ListImageTiles.SelectedItem);
            }
        }

        public void PrevIllustPage()
        {
            if (detail_page is IllustDetailPage) detail_page.PrevIllustPage();
        }

        public void NextIllustPage()
        {
            if (detail_page is IllustDetailPage) detail_page.NextIllustPage();
        }

        internal void KeyAction(KeyEventArgs e)
        {
            ListImageTiles_KeyDown(this, e);
        }

        public TilesPage()
        {
            InitializeComponent();

            setting = Application.Current.LoadSetting();

            window = this.GetActiveWindow();

            cancelTokenSource = new CancellationTokenSource();

            IllustDetail.Content = detail_page;

            UpdateTheme();

            ids.Clear();
            ImageList.Clear();
            ListImageTiles.ItemsSource = ImageList;

            PixivCatgoryMenu.IsPaneOpen = false;
            PixivCatgoryMenu.SelectedIndex = 0;
            //ShowImages();
        }

        internal void ShowImages(PixivPage target = PixivPage.Recommanded, bool IsAppend = false, string id = "")
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
            GC.Collect();

            var win = Application.Current.GetMainWindow();
            if (win is MainWindow && target != PixivPage.None) win.UpdateTitle(target.ToString());

            LastPage = target;
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

                case PixivPage.MyFollowerUser:
                    ShowMyFollower(0, NextURL);
                    break;
                case PixivPage.MyFollowingUser:
                    ShowMyFollowing(0, NextURL, false);
                    break;
                case PixivPage.MyFollowingUserPrivate:
                    ShowMyFollowing(0, NextURL, true);
                    break;
                case PixivPage.MyPixivUser:
                    ShowMyPixiv(0, NextURL);
                    break;
                case PixivPage.MyBlacklistUser:
                    ShowMyBlacklist(0, NextURL);
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

            if (!string.IsNullOrEmpty(id)) lastSelectedId = id;
        }

        private async void ShowRecommanded(string nexturl = null)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

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

                if (root.illusts != null)
                {
                    foreach (var illust in root.illusts)
                    {
                        illust.Cache();
                        if (!ids.Contains(illust.Id.Value))
                        {
                            ids.Add(illust.Id.Value);
                            illust.AddTo(ImageList, nexturl);
                            this.DoEvents();
                        }
                    }
                    this.DoEvents();
                    UpdateTilesThumb();
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowLatest(string nexturl = null)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                var page_no = string.IsNullOrEmpty(nexturl) ? 1 : Convert.ToInt32(nexturl);
                var root = await tokens.GetLatestWorksAsync(page_no);
                nexturl = root.Pagination.Next.ToString() ?? string.Empty;
                NextURL = nexturl;

                if (root != null)
                {
                    foreach (var illust in root)
                    {
                        illust.Cache();
                        if (!ids.Contains(illust.Id.Value))
                        {
                            ids.Add(illust.Id.Value);
                            illust.AddTo(ImageList, nexturl);
                            this.DoEvents();
                        }
                    }
                    this.DoEvents();
                    UpdateTilesThumb();
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowTrendingTags(string nexturl = null)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                var page = string.IsNullOrEmpty(nexturl) ? 1 : Convert.ToInt32(nexturl);
                var root = await tokens.GetTrendingTagsIllustAsync();
                nexturl = string.Empty;
                NextURL = nexturl;

                if (root != null)
                {
                    foreach (var tag in root.tags)
                    {
                        tag.illust.Cache();
                        if (!ids.Contains(tag.illust.Id.Value))
                        {
                            ids.Add(tag.illust.Id.Value);
                            tag.illust.AddTo(ImageList, nexturl);
                            this.DoEvents();
                        }
                    }
                    this.DoEvents();
                    UpdateTilesThumb();
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowFeeds(long uid, string nexturl = null)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                var page = string.IsNullOrEmpty(nexturl) ? 1 : Convert.ToInt32(nexturl);
                var root = await tokens.GetMyFeedsAsync(uid);
                nexturl = string.Empty;
                NextURL = nexturl;

                if (root != null)
                {
                    foreach (var feed in root)
                    {
                        feed.User.Cache();
                        if (!ids.Contains(feed.User.Id.Value))
                        {
                            ids.Add(feed.User.Id.Value);
                            feed.User.AddTo(ImageList);
                            this.DoEvents();
                        }
                    }
                    this.DoEvents();
                    UpdateTilesThumb();
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowFeeds(string nexturl = null)
        {
            ImageTilesWait.Show();
            var force = setting.MyInfo is Pixeez.Objects.User ? false : true;
            var tokens = await CommonHelper.ShowLogin(force);
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                var page = string.IsNullOrEmpty(nexturl) ? 1 : Convert.ToInt32(nexturl);
                var uid = setting.MyInfo is Pixeez.Objects.User ? setting.MyInfo.Id.Value : 0;

                var root = await tokens.GetMyFeedsAsync(uid);
                nexturl = string.Empty;
                NextURL = nexturl;

                if (root != null)
                {
                    foreach (var feed in root)
                    {
                        feed.User.Cache();
                        if (!ids.Contains(feed.User.Id.Value))
                        {
                            ids.Add(feed.User.Id.Value);
                            feed.User.AddTo(ImageList);
                            this.DoEvents();
                        }
                    }
                    this.DoEvents();
                    UpdateTilesThumb();
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowFavorite(string nexturl = null, bool IsPrivate = false)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin(setting.MyInfo == null && IsPrivate);
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                long uid = setting.MyID;
                var condition = IsPrivate ? "private" : "public";

                if (uid > 0)
                {
                    var root = string.IsNullOrEmpty(nexturl) ? await tokens.GetUserFavoriteWorksAsync(uid, condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                    nexturl = root.next_url ?? string.Empty;
                    NextURL = nexturl;

                    if (root.illusts != null)
                    {
                        foreach (var illust in root.illusts)
                        {
                            illust.Cache();
                            if (!ids.Contains(illust.Id.Value))
                            {
                                ids.Add(illust.Id.Value);
                                illust.AddTo(ImageList, nexturl);
                                this.DoEvents();
                            }
                        }
                        this.DoEvents();
                        UpdateTilesThumb();
                    }
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowFollowing(string nexturl = null, bool IsPrivate = false)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                var condition = IsPrivate ? "private" : "public";
                var root = string.IsNullOrEmpty(nexturl) ? await tokens.GetMyFollowingWorksAsync(condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                nexturl = root.next_url ?? string.Empty;
                NextURL = nexturl;

                if (root.illusts != null)
                {
                    foreach (var illust in root.illusts)
                    {
                        illust.Cache();
                        if (!ids.Contains(illust.Id.Value))
                        {
                            ids.Add(illust.Id.Value);
                            illust.AddTo(ImageList, nexturl);
                            this.DoEvents();
                        }
                    }
                    this.DoEvents();
                    UpdateTilesThumb();
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowRankingAll(string nexturl = null, string condition = "daily")
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                var page = string.IsNullOrEmpty(nexturl) ? 1 : Convert.ToInt32(nexturl);
                var root = await tokens.GetRankingAllAsync(condition, page);
                nexturl = root.Pagination.Next.ToString() ?? string.Empty;
                NextURL = nexturl;

                if (root != null)
                {
                    foreach (var works in root)
                    {
                        try
                        {
                            foreach (var work in works.Works)
                            {
                                var illust = work.Work;
                                illust.Cache();
                                if (!ids.Contains(illust.Id.Value))
                                {
                                    ids.Add(illust.Id.Value);
                                    illust.AddTo(ImageList, nexturl);
                                    this.DoEvents();
                                }
                            }
                            this.DoEvents();
                        }
                        catch (Exception ex)
                        {
                            ex.Message.ShowMessageBox("ERROR");
                        }
                    }
                    this.DoEvents();
                    UpdateTilesThumb();
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowRanking(string nexturl = null, string condition = "day")
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

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

                if (root.illusts != null)
                {
                    foreach (var illust in root.illusts)
                    {
                        illust.Cache();
                        if (!ids.Contains(illust.Id.Value))
                        {
                            ids.Add(illust.Id.Value);
                            illust.AddTo(ImageList, nexturl);
                            this.DoEvents();
                        }
                    }
                    this.DoEvents();
                    UpdateTilesThumb();
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowUser(long uid, bool IsPrivate = false)
        {
            if ((IsPrivate || uid == 0) && setting.MyInfo is Pixeez.Objects.User)
            {
                Commands.Open.Execute(setting.MyInfo);
            }
            else
            {
                Pixeez.Objects.User user = null;
                if (uid == 0)
                {
                    uid = setting.MyID;
                    user = setting.MyInfo;
                }
                else
                {
                    user = (Pixeez.Objects.User)uid.FindUser();
                }

                if (Keyboard.Modifiers == ModifierKeys.Control || !(user is Pixeez.Objects.User))
                    user = await uid.RefreshUser();
                Commands.Open.Execute(user);
            }
        }

        private async void ShowMyFollower(long uid, string nexturl = null)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                if (uid == 0) uid = setting.MyID;
                Pixeez.Objects.UsersSearchResult root = null;
                root = string.IsNullOrEmpty(nexturl) ? await tokens.GetFollowerUsers(uid.ToString()) : await tokens.AccessNewApiAsync<Pixeez.Objects.UsersSearchResult>(nexturl);

                nexturl = root.next_url ?? string.Empty;
                NextURL = nexturl;

                if (root.Users != null)
                {
                    foreach (var up in root.Users)
                    {
                        var user = up.User;
                        user.Cache();
                        if (!ids.Contains(user.Id.Value))
                        {
                            ids.Add(user.Id.Value);
                            user.AddTo(ImageList, nexturl);
                            this.DoEvents();
                        }
                    }
                    this.DoEvents();
                    UpdateTilesThumb();
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowMyFollowing(long uid, string nexturl = null, bool IsPrivate = false)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                if (uid == 0) uid = setting.MyID;
                var condition = IsPrivate ? "private" : "public";
                Pixeez.Objects.UsersSearchResult root = null;
                root = string.IsNullOrEmpty(nexturl) ? await tokens.GetFollowingUsers(uid.ToString(), condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.UsersSearchResult>(nexturl);

                nexturl = root.next_url ?? string.Empty;
                NextURL = nexturl;

                if (root.Users != null)
                {
                    foreach (var up in root.Users)
                    {
                        var user = up.User;
                        user.Cache();
                        if (!ids.Contains(user.Id.Value))
                        {
                            ids.Add(user.Id.Value);
                            user.AddTo(ImageList, nexturl);
                            this.DoEvents();
                        }
                    }
                    this.DoEvents();
                    UpdateTilesThumb();
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowMyPixiv(long uid, string nexturl = null)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                if (uid == 0) uid = setting.MyID;
                Pixeez.Objects.UsersSearchResult root = null;
                root = string.IsNullOrEmpty(nexturl) ? await tokens.GetMyPixiv(uid.ToString()) : await tokens.AccessNewApiAsync<Pixeez.Objects.UsersSearchResult>(nexturl);

                nexturl = root.next_url ?? string.Empty;
                NextURL = nexturl;

                if (root.Users != null)
                {
                    foreach (var up in root.Users)
                    {
                        var user = up.User;
                        user.Cache();
                        if (!ids.Contains(user.Id.Value))
                        {
                            ids.Add(user.Id.Value);
                            user.AddTo(ImageList, nexturl);
                            this.DoEvents();
                        }
                    }
                    this.DoEvents();
                    UpdateTilesThumb();
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowMyBlacklist(long uid, string nexturl = null)
        {
            ImageTilesWait.Show();
            var tokens = await CommonHelper.ShowLogin();
            ImageTilesWait.Hide();
            if (tokens == null) return;

            try
            {
                ImageTilesWait.Show();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                if (uid == 0) uid = setting.MyID;
                Pixeez.Objects.UsersSearchResultAlt root = null;
                root = string.IsNullOrEmpty(nexturl) ? await tokens.GetBlackListUsers(uid.ToString()) : await tokens.AccessNewApiAsync<Pixeez.Objects.UsersSearchResultAlt>(nexturl);

                nexturl = root.next_url ?? string.Empty;
                NextURL = nexturl;

                if (root.Users != null)
                {
                    foreach (var up in root.Users)
                    {
                        var user = up.User;
                        user.Cache();
                        if (!ids.Contains(user.Id.Value))
                        {
                            ids.Add(user.Id.Value);
                            user.AddTo(ImageList, nexturl);
                            this.DoEvents();
                        }
                    }
                    this.DoEvents();
                    UpdateTilesThumb();
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
                KeepLastSelected(lastSelectedId);
            }
        }

        private void ImageTiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var idx = ListImageTiles.SelectedIndex;
                if (idx < 0) return;

                var item = ImageList[idx];

                if (item.IsUser())
                {
                    item.IsDownloaded = false;
                    item.IsFavorited = false;
                    item.IsFollowed = item.User.IsLiked();
                }
                else
                {
                    item.IsDownloaded = item.Illust.IsPartDownloadedAsync();
                    item.IsFavorited = item.IsLiked();
                    item.IsFollowed = item.User.IsLiked();
                }

                var ID_O = detail_page.Tag is ImageItem ? (detail_page.Tag as ImageItem).ID : string.Empty;
                var ID_N = item is ImageItem ? item.ID : string.Empty;

                if (string.IsNullOrEmpty(ID_O) || !ID_O.Equals(ID_N, StringComparison.CurrentCultureIgnoreCase))// || detail_page.Tag != item)
                {
                    detail_page.Tag = item;
                    detail_page.Contents = item;
                    detail_page.UpdateDetail(item);
                }
                if (ListImageTiles.SelectedItem is ImageItem) (ListImageTiles.SelectedItem as ImageItem).Focus();
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
        }

        private void ListImageTiles_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = false;
            if (e.Key == Key.F5 || e.SystemKey == Key.F5)
            {
                var main = this.GetMainWindow() as MainWindow;
                main.CommandNavRefresh_Click(main.CommandNavRefresh, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.F3 || e.SystemKey == Key.F3)
            {
                var main = this.GetMainWindow() as MainWindow;
                main.CommandNavNext_Click(main.CommandNavNext, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (e.Key == Key.F6 || e.SystemKey == Key.F6)
            {
                UpdateTilesThumb();
                e.Handled = true;
            }
            else if (e.Key == Key.F7 || e.Key == Key.F8 || e.SystemKey == Key.F7 || e.SystemKey == Key.F8)
            {
                if (detail_page is IllustDetailPage)
                {
                    detail_page.KeyAction(e);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Enter || e.SystemKey == Key.Enter)
            {
                if (ListImageTiles.SelectedItem != null)
                {
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Down || e.Key == Key.Next || e.Key == Key.PageDown ||
                     e.SystemKey == Key.Down || e.SystemKey == Key.Next || e.SystemKey == Key.PageDown)
            {
                if (ListImageTiles.SelectedIndex >= ListImageTiles.Items.Count - 1)
                {
                    ShowImages(TargetPage, true);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Home || e.SystemKey == Key.Home)
            {
                if (ListImageTiles.Items.Count > 0)
                {
                    ListImageTiles.Items.MoveCurrentToFirst();
                    ListImageTiles.ScrollIntoView(ListImageTiles.SelectedItem);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.End || e.SystemKey == Key.End)
            {
                if (ListImageTiles.Items.Count > 0)
                {
                    ListImageTiles.Items.MoveCurrentToLast();
                    ListImageTiles.ScrollIntoView(ListImageTiles.SelectedItem);
                    e.Handled = true;
                }
            }
            else if ((e.Key == Key.S || e.SystemKey == Key.S) && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (ListImageTiles.SelectedItem is ImageItem)
                {
                    var item = ListImageTiles.SelectedItem as ImageItem;
                    if (item.Illust is Pixeez.Objects.Work)
                    {
                        var illust = item.Illust;
                        var url = illust.GetOriginalUrl();
                        var dt = illust.GetDateTime();
                        var is_meta_single_page = illust.PageCount == 1 ? true : false;
                        if (!string.IsNullOrEmpty(url))
                        {
                            url.SaveImage(illust.GetThumbnailUrl(), dt, is_meta_single_page);
                        }
                    }
                }
                e.Handled = true;
            }
            else if ((e.Key == Key.O || e.SystemKey == Key.O) && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var item = ListImageTiles.SelectedItem as ImageItem;
                Commands.OpenDownloaded.Execute(item);
                e.Handled = true;
            }
            else if ((e.Key == Key.Left || e.SystemKey == Key.Left) && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                PrevIllustPage();
                e.Handled = true;
            }
            else if ((e.Key == Key.Right || e.SystemKey == Key.Right) && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                NextIllustPage();
                e.Handled = true;
            }
        }

        private void ListImageTiles_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var change_detail_page = Keyboard.Modifiers == ModifierKeys.Shift && e.OriginalSource != null;
            if (e.XButton1 == MouseButtonState.Pressed)
            {
                if (change_detail_page && detail_page is IllustDetailPage)
                {
                    if (detail_page is IllustDetailPage) detail_page.NextIllustPage();
                }
                else
                {
                    NextIllust();
                }
                e.Handled = true;
            }
            else if (e.XButton2 == MouseButtonState.Pressed)
            {
                if (change_detail_page && detail_page is IllustDetailPage)
                {
                    if (detail_page is IllustDetailPage) detail_page.PrevIllustPage();
                }
                else
                {
                    PrevIllust();
                }
                e.Handled = true;
            }
        }

        private void ListImageTiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            FrameworkElement originalSource = e.OriginalSource as FrameworkElement;
            FrameworkElement source = e.Source as FrameworkElement;

            if (originalSource.Name.Equals("Arrow", StringComparison.CurrentCultureIgnoreCase))
            {
                ShowImages(TargetPage, true);
            }
            else if (source == ListImageTiles)
            {
                //ShowImages(TargetPage, true);
            }
            e.Handled = true;
        }

        private void ListImageTiles_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ListImageTiles.Items != null && ListImageTiles.Items.Count > 0)
            {
                if (e.Delta < 0 && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    ShowImages(TargetPage, true);
                    e.Handled = true;
                }
            }
        }

        private void TileImage_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if (e.Property == null) return;
            if (sender is Image)
            {
                var image = sender as Image;
                if (e.Property.Name.Equals("Source", StringComparison.CurrentCultureIgnoreCase))
                {
                    var progressObj = image.FindName("PART_Progress");
                    if (progressObj is ProgressRing)
                    {
                        var progress = progressObj as ProgressRing;
                        if (image.Source == null)
                        {
                            progress.Show();
                        }
                        else
                        {
                            progress.Hide();
                        }
                    }
                }
            }
            else if (sender is PackIconModern)
            {
#if DEBUG
                var icon = sender as PackIconModern;
                if (e.Property.Name.Equals("Visibility", StringComparison.CurrentCultureIgnoreCase))
                {
                    var follow = icon.FindName("PART_Follow");
                    var fav = icon.FindName("PART_Favorite");
                    if (follow is PackIconModern && fav is PackIconModern)
                    {
                        var follow_mark = follow as PackIconModern;
                        var follow_effect = follow_mark.FindName("PART_Follow_Shadow");
                        var fav_mark = fav as PackIconModern;
                        if (fav_mark.Visibility == Visibility.Visible)
                        {
                            follow_mark.Height = 16;
                            follow_mark.Width = 16;
                            follow_mark.Margin = new Thickness(0, 0, 12, 12);
                            follow_mark.Foreground = Theme.WhiteBrush;
                            if (follow_effect is System.Windows.Media.Effects.DropShadowEffect)
                            {
                                var shadow = follow_effect as System.Windows.Media.Effects.DropShadowEffect;
                                shadow.Color = Theme.AccentColor;
                            }
                        }
                        else
                        {
                            follow_mark.Height = 24;
                            follow_mark.Width = 24;
                            follow_mark.Margin = new Thickness(0, 0, 8, 8);
                            follow_mark.Foreground = Theme.AccentBrush;
                            if (follow_effect is System.Windows.Media.Effects.DropShadowEffect)
                            {
                                var shadow = follow_effect as System.Windows.Media.Effects.DropShadowEffect;
                                shadow.Color = Theme.WhiteColor;
                            }
                        }
                    }
                }
#endif
            }
        }

        private void ImageTilesViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {

        }

        private void PixivCatgoryMenu_ItemInvoked(object sender, HamburgerMenuItemInvokedEventArgs args)
        {
            //return;
            var item = args.InvokedItem;
            var idx = PixivCatgoryMenu.SelectedIndex;
            if(PixivCatgoryMenu.IsPaneOpen) PixivCatgoryMenu.IsPaneOpen = false;

            if (item == miAbout)
            {
                args.Handled = true;
                PixivCatgoryMenu.SelectedIndex = idx;
            }
            #region Common
            else if (item == miPixivRecommanded)
            {
                ShowImages(PixivPage.Recommanded, false);
            }
            else if (item == miPixivLatest)
            {
                ShowImages(PixivPage.Latest, false);
            }
            else if (item == miPixivTrendingTags)
            {
                ShowImages(PixivPage.TrendingTags, false);
            }
            #endregion
            #region Following
            else if (item == miPixivFollowing)
            {
                ShowImages(PixivPage.Follow, false);
            }
            else if (item == miPixivFollowingPrivate)
            {
                ShowImages(PixivPage.FollowPrivate, false);
            }
            #endregion
            #region Favorite
            else if (item == miPixivFavorite)
            {
                ShowImages(PixivPage.Favorite, false);
            }
            else if (item == miPixivFavoritePrivate)
            {
                ShowImages(PixivPage.FavoritePrivate, false);
            }
            #endregion
            #region Ranking Day
            else if (item == miPixivRankingDay)
            {
                ShowImages(PixivPage.RankingDay, false);
            }
            else if (item == miPixivRankingDayR18)
            {
                ShowImages(PixivPage.RankingDayR18, false);
            }
            else if (item == miPixivRankingDayMale)
            {
                ShowImages(PixivPage.RankingDayMale, false);
            }
            else if (item == miPixivRankingDayMaleR18)
            {
                ShowImages(PixivPage.RankingDayMaleR18, false);
            }
            else if (item == miPixivRankingDayFemale)
            {
                ShowImages(PixivPage.RankingDayFemale, false);
            }
            else if (item == miPixivRankingDayFemaleR18)
            {
                ShowImages(PixivPage.RankingDayFemaleR18, false);
            }
            #endregion
            #region Ranking Day
            else if (item == miPixivRankingWeek)
            {
                ShowImages(PixivPage.RankingWeek, false);
            }
            else if (item == miPixivRankingWeekOriginal)
            {
                ShowImages(PixivPage.RankingWeekOriginal, false);
            }
            else if (item == miPixivRankingWeekRookie)
            {
                ShowImages(PixivPage.RankingWeekRookie, false);
            }
            else if (item == miPixivRankingWeekR18)
            {
                ShowImages(PixivPage.RankingWeekR18, false);
            }
            #endregion
            #region Ranking Month
            else if (item == miPixivRankingMonth)
            {
                ShowImages(PixivPage.RankingMonth, false);
            }
            #endregion
            #region Pixiv Mine
            else if (item == miPixivMine)
            {
                args.Handled = true;
                PixivCatgoryMenu.SelectedIndex = idx;
                ShowImages(PixivPage.My, false);
            }
            else if (item == miPixivMyFollower)
            {
                ShowImages(PixivPage.MyFollowerUser, false);
            }
            else if (item == miPixivMyFollowing)
            {
                ShowImages(PixivPage.MyFollowingUser, false);
            }
            else if (item == miPixivMyFollowingPrivate)
            {
                ShowImages(PixivPage.MyFollowingUserPrivate, false);
            }
            else if (item == miPixivMyUsers)
            {
                ShowImages(PixivPage.MyPixivUser, false);
            }
            else if (item == miPixivMyBlacklis)
            {
                ShowImages(PixivPage.MyBlacklistUser, false);
            }
            #endregion
        }

    }
}
