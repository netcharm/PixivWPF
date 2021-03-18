using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

using MahApps.Metro.Controls;
using MahApps.Metro.IconPacks;
using PixivWPF.Common;
using System.ComponentModel;
using System.IO;

namespace PixivWPF.Pages
{
    /// <summary>
    /// PageTiles.xaml 的交互逻辑
    /// </summary>
    public partial class TilesPage : Page
    {
        public MainWindow ParentWindow { get; private set; }
        private IllustDetailPage detail_page = new IllustDetailPage() { Name="IllustDetail" };

        internal string lastSelectedId = string.Empty;
        internal List<long> ids = new List<long>();

        private Setting setting = Application.Current.LoadSetting();
        public PixivPage TargetPage = PixivPage.None;
        private PixivPage LastPage = PixivPage.None;
        private string NextURL = null;

        public DateTime SelectedDate { get; set; } = DateTime.Now;

        #region Update UI helper
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
            catch (Exception ex) { ex.ERROR(); }
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
            catch (Exception ex) { ex.ERROR(); }
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
            catch (Exception ex) { ex.ERROR(); }
        }

        public void UpdateWebContent()
        {
            UpdateWebContentAsync();
        }

        public void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            if (ImageTiles.Items is ObservableCollection<PixivItem>)
            {
                ImageTiles.Items.UpdateDownloadStateAsync();
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
                ImageTiles.Items.UpdateLikeState(illustid, is_user);
                //UpdateLikeState(illustid);
            });
        }

        private void OnlyActiveItems(object sender, FilterEventArgs e)
        {
            e.Accepted = false;

            var item = e.Item as PixivItem;
            if (item.Source == null) return;

            e.Accepted = true;
        }

        protected internal void UpdateTilesThumb(bool overwrite = false)
        {
            ImageTiles.UpdateTilesImage(overwrite);
            if (detail_page is IllustDetailPage) detail_page.UpdateThumb(true, overwrite);
        }

        public void UpdateTiles()
        {
            ShowImages(TargetPage, false, GetLastSelectedID());
        }

        internal string GetLastSelectedID()
        {
            string id = lastSelectedId;
            if (ImageTiles.Items.Count > 0)
            {
                //var recents = Application.Current.HistoryRecentIllusts(setting.MostRecents);
                //lastSelectedId = recents.Count() > 0 ? recents.First().ID : string.Empty;
                //id = lastSelectedId;
                if (ImageTiles.SelectedIndex == 0 && string.IsNullOrEmpty(lastSelectedId))
                    lastSelectedId = (ImageTiles.Items[0] as PixivItem).ID;
                id = ImageTiles.SelectedItem is PixivItem ? (ImageTiles.SelectedItem as PixivItem).ID : lastSelectedId;
            }
            return (id);
        }

        private void KeepLastSelected(string id)
        {
            new Action(() =>
            {
                if (ImageTiles.Items.Count > 0)
                {
                    if (!string.IsNullOrEmpty(id))
                    {
                        foreach (var item in ImageTiles.Items)
                        {
                            if (item is PixivItem)
                            {
                                var ID = (item as PixivItem).ID;
                                if (ID.Equals(id, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    ImageTiles.SelectedItem = item;
                                    ImageTiles.ScrollIntoView(ImageTiles.SelectedItem);
                                    this.DoEvents();
                                    break;
                                }
                            }
                            this.DoEvents();
                        }
                    }
                    if (ImageTiles.SelectedIndex < 0 && string.IsNullOrEmpty(lastSelectedId))
                    {
                        ImageTiles.SelectedIndex = 0;
                        ImageTiles.ScrollIntoView(ImageTiles.SelectedItem);
                        this.DoEvents();
                    }
                    Prefetching();
                    this.DoEvents();
                }
            }).Invoke(async: false);
        }
        #endregion

        #region Navigation helper
        public PixivItem CurrentItem
        {
            get
            {
                return (ImageTiles.SelectedItem is PixivItem ? ImageTiles.SelectedItem : null);
            }
        }

        internal PixivPage GetPageTypeByCategory(object item)
        {
            PixivPage result = PixivPage.None;

            if (item == miAbout)
                result = PixivPage.About;
            #region Common
            else if (item == miPixivRecommanded)
                result = PixivPage.Recommanded;
            else if (item == miPixivLatest)
                result = PixivPage.Latest;
            else if (item == miPixivTrendingTags)
                result = PixivPage.TrendingTags;
            #endregion
            #region Following
            else if (item == miPixivFollowing)
                result = PixivPage.Follow;
            else if (item == miPixivFollowingPrivate)
                result = PixivPage.FollowPrivate;
            #endregion
            #region Favorite
            else if (item == miPixivFavorite)
                result = PixivPage.Favorite;
            else if (item == miPixivFavoritePrivate)
                result = PixivPage.FavoritePrivate;
            #endregion
            #region Ranking Day
            else if (item == miPixivRankingDay)
                result = PixivPage.RankingDay;
            else if (item == miPixivRankingDayR18)
                result = PixivPage.RankingDayR18;
            else if (item == miPixivRankingDayMale)
                result = PixivPage.RankingDayMale;
            else if (item == miPixivRankingDayMaleR18)
                result = PixivPage.RankingDayMaleR18;
            else if (item == miPixivRankingDayFemale)
                result = PixivPage.RankingDayFemale;
            else if (item == miPixivRankingDayFemaleR18)
                result = PixivPage.RankingDayFemaleR18;
            #endregion
            #region Ranking Day
            else if (item == miPixivRankingWeek)
                result = PixivPage.RankingWeek;
            else if (item == miPixivRankingWeekOriginal)
                result = PixivPage.RankingWeekOriginal;
            else if (item == miPixivRankingWeekRookie)
                result = PixivPage.RankingWeekRookie;
            else if (item == miPixivRankingWeekR18)
                result = PixivPage.RankingWeekR18;
            #endregion
            #region Ranking Month
            else if (item == miPixivRankingMonth)
                result = PixivPage.RankingMonth;
            #endregion
            #region Pixiv Mine
            else if (item == miPixivMine)
                result = PixivPage.My;
            else if (item == miPixivMyFollower)
                result = PixivPage.MyFollowerUser;
            else if (item == miPixivMyFollowing)
                result = PixivPage.MyFollowingUser;
            else if (item == miPixivMyFollowingPrivate)
                result = PixivPage.MyFollowingUserPrivate;
            else if (item == miPixivMyUsers)
                result = PixivPage.MyPixivUser;
            else if (item == miPixivMyBlacklis)
                result = PixivPage.MyBlacklistUser;
            #endregion

            return (result);
        }

        internal int GetCategoryIndex(PixivPage target)
        {
            int result = (int)PixivPage.Recommanded;
            for (int i = 0; i < CategoryMenu.Items.Count; i++)
            {
                if (GetPageTypeByCategory(CategoryMenu.Items[i]) == target)
                {
                    result = i;
                    break;
                }
            }
            return (result);
        }

        public void FirstCategory()
        {
            CategoryMenu.Items.MoveCurrentToFirst();
        }

        public void LastCategory()
        {
            CategoryMenu.Items.MoveCurrentToLast();
        }

        public void PrevCategory()
        {
            if (CategoryMenu.Items.CurrentPosition == 0)
                CategoryMenu.Items.MoveCurrentToLast();
            else
                CategoryMenu.Items.MoveCurrentToPrevious();
        }

        public void NextCategory()
        {
            if (CategoryMenu.Items.CurrentPosition == CategoryMenu.Items.Count - 1)
                CategoryMenu.Items.MoveCurrentToLast();
            else
                CategoryMenu.Items.MoveCurrentToNext();
        }

        public void RefreshPage()
        {
            ShowImages(TargetPage, false, GetLastSelectedID());
        }

        public void RefreshThumbnail()
        {
            var overwrite = Keyboard.Modifiers == ModifierKeys.Alt ? true : false;
            UpdateTilesThumb(overwrite);
            Prefetching();
        }

        public void Prefetching()
        {
            if (PrefetchingItems is PrefetchingTask) PrefetchingItems.Start(reverse: true);
        }

        public void AppendTiles()
        {
            ShowImages(TargetPage, true, GetLastSelectedID());
        }

        public void OpenIllust()
        {
            if (detail_page is IllustDetailPage) detail_page.OpenIllust();
        }

        public void OpenCachedImage()
        {
            if (detail_page is IllustDetailPage) detail_page.OpenCachedImage();
        }

        public void OpenWork()
        {
            if (detail_page is IllustDetailPage) detail_page.OpenInNewWindow();
        }

        public void OpenUser()
        {
            if (detail_page is IllustDetailPage) detail_page.OpenUser();
        }

        public void SaveIllust()
        {
            if (detail_page is IllustDetailPage) detail_page.SaveIllust();
        }

        public void SaveIllustAll()
        {
            if (detail_page is IllustDetailPage) detail_page.SaveIllustAll();
        }

        public void CopyPreview()
        {
            if (detail_page is IllustDetailPage) detail_page.CopyPreview();
        }

        public void JumpTo(string id)
        {
            var results = ImageTiles.Items.Where(item => item.ID.Equals(id));
            if (results.Count() > 0)
            {
                var target = results.FirstOrDefault();
                ImageTiles.ScrollIntoView(target);
                ImageTiles.SelectedItem = target;
            }
            else
            {
                var illust = id.FindIllust();
                if (illust is Pixeez.Objects.Work)
                    Commands.OpenWork.Execute(illust);
            }
        }

        public void FirstIllust()
        {
            if (this is TilesPage)
            {
                ImageTiles.MoveCurrentToFirst();
                ImageTiles.ScrollIntoView(ImageTiles.SelectedItem);
            }
        }

        public void LastIllust()
        {
            if (this is TilesPage)
            {
                ImageTiles.MoveCurrentToLast();
                ImageTiles.ScrollIntoView(ImageTiles.SelectedItem);
            }
        }

        public void PrevIllust()
        {
            if (this is TilesPage)
            {
                if (ImageTiles.IsCurrentFirst)
                    ImageTiles.MoveCurrentToLast();
                else
                    ImageTiles.MoveCurrentToPrevious();
                ImageTiles.ScrollIntoView(ImageTiles.SelectedItem);
            }
        }

        public void NextIllust()
        {
            if (this is TilesPage)
            {
                if (ImageTiles.IsCurrentLast)
                    ImageTiles.MoveCurrentToFirst();
                else
                    ImageTiles.MoveCurrentToNext();
                ImageTiles.ScrollIntoView(ImageTiles.SelectedItem);
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

        private int FirstInView(out int count)
        {
            int result = -1;

            UniformGrid vspanel = ImageTiles.GetVisualChild<UniformGrid>();
            List<ListViewItem> children = vspanel.GetVisualChildren<ListViewItem>();
            count = children.Count;
            if (children[1].IsVisiualChild(vspanel))
            {

            }
            return (result);
        }

        public void ScrollPageUp()
        {
            if (ImageTiles is ImageListGrid)
            {
                ImageTiles.PageUp();
            }
        }

        public void ScrollPageDown()
        {
            if (ImageTiles is ImageListGrid)
            {
                ImageTiles.PageDown();
            }
        }

        public void ScrollPageFirst()
        {
            if (ImageTiles is ImageListGrid)
            {
                ImageTiles.PageFirst();
            }
        }

        public void ScrollPageLast()
        {
            if (ImageTiles is ImageListGrid)
            {
                ImageTiles.PageLast();
            }
        }
        #endregion

        #region Live Filter helper
        public void SetFilter(string filter)
        {
            try
            {
                ImageTiles.Filter = filter.GetFilter();
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
                    ImageTiles.Filter = filter.GetFilter();
                else
                    ImageTiles.Filter = null;

                if (detail_page is IllustDetailPage)
                    detail_page.SetFilter(filter);
            }
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
        }
        public dynamic GetTilesCount()
        {
            List<string> tips = new List<string>();
            tips.Add($"Illust: {ImageTiles.ItemsCount} of {ImageTiles.Items.Count}");
            tips.Add($"Page: {ImageTiles.CurrentPage} of {ImageTiles.TotalPages}");
            if (detail_page is IllustDetailPage)
                tips.Add(detail_page.GetTilesCount());
            return (string.Join(Environment.NewLine, tips));
        }
        #endregion

        #region Prefetching background task
        private PrefetchingTask PrefetchingItems = null;
        #endregion

        public TilesPage()
        {
            InitializeComponent();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            $"{WindowTitle} Loading...".INFO();

            setting = Application.Current.LoadSetting();

            ParentWindow = Window.GetWindow(this) as MainWindow;
            UpdateTheme();
            this.DoEvents();

            if (PrefetchingItems == null) PrefetchingItems = new PrefetchingTask()
            {
                Name = "TilesPrefetching",
                Items = ImageTiles.Items,
                ReportProgressSlim = () =>
                {
                    var percent = PrefetchingItems.Percentage;
                    var tooltip = PrefetchingItems.Comments;
                    var state = PrefetchingItems.State;
                    if (ParentWindow is MainWindow) ParentWindow.SetPrefetchingProgress(percent, tooltip, state);
                },
                ReportProgress = (percent, tooltip, state) =>
                {
                    if (ParentWindow is MainWindow) ParentWindow.SetPrefetchingProgress(percent, tooltip, state);
                }
            };

            ids.Clear();
            ImageTiles.AutoGC = true;
            ImageTiles.WaitGC = true;
            ImageTiles.CalcSystemMemoryUsage = setting.CalcSystemMemoryUsage;
            this.DoEvents();

            await new Action(async () =>
            {
                await Task.Delay(1);
                IllustDetail.Content = detail_page;
                this.DoEvents();
                CategoryMenu.IsPaneOpen = false;
                this.DoEvents();
                CategoryMenu.SelectedIndex = GetCategoryIndex(setting.DefaultPage);
                this.DoEvents();
            }).InvokeAsync(true);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            if (PrefetchingItems is PrefetchingTask) PrefetchingItems.Dispose();
        }

        internal void ShowImages(PixivPage target = PixivPage.Recommanded, bool IsAppend = false, string id = "")
        {
            if (ParentWindow == null) ParentWindow = this.GetMainWindow();
            if (target == PixivPage.My) { ShowUser(0, true); return; }
            if (ParentWindow is MainWindow) ParentWindow.UpdateTitle(target.ToString());
            this.DoEvents();

            if (PrefetchingItems is PrefetchingTask) PrefetchingItems.Stop();

            if (TargetPage != target)
            {
                lastSelectedId = string.Empty;
                TargetPage = target;
                IsAppend = false;
            }
            if (!IsAppend)
            {
                $"Show illusts from category \"{target.ToString()}\" ...".INFO();
                NextURL = null;
                ids.Clear();
                ImageTiles.ClearAsync(batch: setting.BatchClearThumbnails, force: true);
                this.DoEvents();
            }
            else $"Append illusts from category \"{target.ToString()}\" ...".INFO();

            ImageTiles.SelectedIndex = -1;
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
            this.DoEvents();
            if (!string.IsNullOrEmpty(id)) lastSelectedId = id;
        }

        #region Show category
        private async void ShowRecommanded(string nexturl = null)
        {
            ImageTiles.Wait();
            var tokens = await CommonHelper.ShowLogin();
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

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
                            illust.AddTo(ImageTiles.Items, nexturl);
                            this.DoEvents();
                        }
                    }
                    if (!Application.Current.IsLogin()) await Task.Delay(250);
                    this.DoEvents();
                    ImageTiles.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    //var n = nameof(ShowRecommanded);
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowLatest(string nexturl = null)
        {
            ImageTiles.Wait();
            var tokens = await CommonHelper.ShowLogin();
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

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
                            illust.AddTo(ImageTiles.Items, nexturl);
                            this.DoEvents();
                        }
                    }
                    if (!Application.Current.IsLogin()) await Task.Delay(250);
                    this.DoEvents();
                    ImageTiles.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowTrendingTags(string nexturl = null)
        {
            ImageTiles.Wait();
            var tokens = await CommonHelper.ShowLogin();
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

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
                            tag.illust.AddTo(ImageTiles.Items, nexturl);
                            this.DoEvents();
                        }
                    }
                    if (!Application.Current.IsLogin()) await Task.Delay(250);
                    this.DoEvents();
                    ImageTiles.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowFeeds(long uid, string nexturl = null)
        {
            ImageTiles.Wait();
            var tokens = await CommonHelper.ShowLogin();
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

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
                            feed.User.AddTo(ImageTiles.Items);
                            this.DoEvents();
                        }
                    }
                    if (!Application.Current.IsLogin()) await Task.Delay(250);
                    this.DoEvents();
                    ImageTiles.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowFeeds(string nexturl = null)
        {
            ImageTiles.Wait();
            var force = setting.MyInfo is Pixeez.Objects.User ? false : true;
            var tokens = await CommonHelper.ShowLogin(force);
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

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
                            feed.User.AddTo(ImageTiles.Items);
                            this.DoEvents();
                        }
                    }
                    if (!Application.Current.IsLogin()) await Task.Delay(250);
                    this.DoEvents();
                    ImageTiles.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowFavorite(string nexturl = null, bool IsPrivate = false)
        {
            ImageTiles.Wait();
            var tokens = await CommonHelper.ShowLogin(setting.MyInfo == null && IsPrivate);
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

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
                                illust.AddTo(ImageTiles.Items, nexturl);
                                this.DoEvents();
                            }
                        }
                        if (!Application.Current.IsLogin()) await Task.Delay(250);
                        this.DoEvents();
                        ImageTiles.UpdateTilesImage();
                    }
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowFollowing(string nexturl = null, bool IsPrivate = false)
        {
            ImageTiles.Wait();
            var tokens = await CommonHelper.ShowLogin();
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

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
                            illust.AddTo(ImageTiles.Items, nexturl);
                            this.DoEvents();
                        }
                    }
                    if (!Application.Current.IsLogin()) await Task.Delay(250);
                    this.DoEvents();
                    ImageTiles.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowRankingAll(string nexturl = null, string condition = "daily")
        {
            ImageTiles.Wait();
            var tokens = await CommonHelper.ShowLogin();
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

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
                                    illust.AddTo(ImageTiles.Items, nexturl);
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
                    if (!Application.Current.IsLogin()) await Task.Delay(250);
                    this.DoEvents();
                    ImageTiles.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowRanking(string nexturl = null, string condition = "day")
        {
            ImageTiles.Wait();
            var tokens = await CommonHelper.ShowLogin();
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                var date = SelectedDate.Date == DateTime.Now.Date ? string.Empty : (SelectedDate - TimeSpan.FromDays(1)).ToString("yyyy-MM-dd");
                var root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRankingAsync(condition, 1, 30, date) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                int count = 2;
                while (count <= 7 && (root.illusts == null || root.illusts.Length <= 0))
                {
                    date = (SelectedDate - TimeSpan.FromDays(count)).ToString("yyyy-MM-dd");
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
                            illust.AddTo(ImageTiles.Items, nexturl);
                            this.DoEvents();
                        }
                    }
                    if (!Application.Current.IsLogin()) await Task.Delay(250);
                    this.DoEvents();
                    ImageTiles.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
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
            ImageTiles.Wait();
            var tokens = await CommonHelper.ShowLogin();
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

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
                            user.AddTo(ImageTiles.Items, nexturl);
                            this.DoEvents();
                        }
                    }
                    if (!Application.Current.IsLogin()) await Task.Delay(250);
                    this.DoEvents();
                    ImageTiles.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowMyFollowing(long uid, string nexturl = null, bool IsPrivate = false)
        {
            ImageTiles.Wait();
            var tokens = await CommonHelper.ShowLogin();
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

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
                            user.AddTo(ImageTiles.Items, nexturl);
                            this.DoEvents();
                        }
                    }
                    if (!Application.Current.IsLogin()) await Task.Delay(250);
                    this.DoEvents();
                    ImageTiles.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowMyPixiv(long uid, string nexturl = null)
        {
            ImageTiles.Wait();
            var tokens = await CommonHelper.ShowLogin();
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

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
                            user.AddTo(ImageTiles.Items, nexturl);
                            this.DoEvents();
                        }
                    }
                    if (!Application.Current.IsLogin()) await Task.Delay(250);
                    this.DoEvents();
                    ImageTiles.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }

        private async void ShowMyBlacklist(long uid, string nexturl = null)
        {
            ImageTiles.Wait();
            var tokens = await CommonHelper.ShowLogin();
            ImageTiles.Ready();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

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
                            user.AddTo(ImageTiles.Items, nexturl);
                            this.DoEvents();
                        }
                    }
                    if (!Application.Current.IsLogin()) await Task.Delay(250);
                    this.DoEvents();
                    ImageTiles.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ImageTiles.Fail();
                if (ex is NullReferenceException)
                {
                    "No Result".ShowToast("INFO", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: System.Reflection.MethodBase.GetCurrentMethod().Name);
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }
        #endregion

        private void Page_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var change_detail_page = setting.SmartMouseResponse && e.Source == IllustDetail;
            if (Keyboard.Modifiers == ModifierKeys.Shift) change_detail_page = !change_detail_page;
            if (change_detail_page)
            {
                if (e.XButton1 == MouseButtonState.Pressed)
                {
                    if (detail_page is IllustDetailPage) detail_page.NextIllustPage();
                    e.Handled = true;
                }
                else if (e.XButton2 == MouseButtonState.Pressed)
                {
                    if (detail_page is IllustDetailPage) detail_page.PrevIllustPage();
                    e.Handled = true;
                }
            }
            else
            {
                if (e.XButton1 == MouseButtonState.Pressed)
                {
                    NextIllust();
                    e.Handled = true;
                }
                else if (e.XButton2 == MouseButtonState.Pressed)
                {
                    PrevIllust();
                    e.Handled = true;
                }
            }
        }

        private void Page_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ImageTiles.Items != null && ImageTiles.Items.Count > 0)
            {
                if (e.Delta < 0 && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                {
                    ShowImages(TargetPage, true);
                    e.Handled = true;
                }
            }
        }

        private object lastInvokedItem = null;
        private void CategoryMenu_ItemInvoked(object sender, HamburgerMenuItemInvokedEventArgs args)
        {
            if (!CategoryMenu.IsLoaded) return;

            var item = args.InvokedItem;
            var idx = CategoryMenu.SelectedIndex;

            if (CategoryMenu.IsPaneOpen) CategoryMenu.IsPaneOpen = false;

            if (item == miAbout)
            {
                args.Handled = true;
                CategoryMenu.SelectedIndex = idx;
            }
            else if (CategoryMenu.SelectedItem == lastInvokedItem) return;
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
                CategoryMenu.SelectedIndex = idx;
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

            if (!args.Handled) lastInvokedItem = item;
        }

        private void CategoryMenu_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            setting.DefaultPage = GetPageTypeByCategory(CategoryMenu.SelectedItem);
            setting.Save();
        }

        private async void ImageTiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var idx = ImageTiles.SelectedIndex;
                if (idx < 0) return;

                if (ImageTiles.SelectedItem is PixivItem)
                {
                    var item = ImageTiles.SelectedItem as PixivItem;
                    if (item.Thumb.IsCached() && item.Source == null)
                    {
                        var thumb = await item.Thumb.LoadImageFromFile(size: Application.Current.GetDefaultThumbSize());
                        if (thumb != null && thumb != null)
                        {
                            item.Source = thumb.Source;
                            item.State = TaskStatus.RanToCompletion;
                            thumb.Source = null;
                            thumb = null;
                        }
                    }
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

                    var ID_O = detail_page.Contents is PixivItem ? detail_page.Contents.ID : string.Empty;
                    var ID_N = item is PixivItem ? item.ID : string.Empty;

                    if (string.IsNullOrEmpty(ID_O) || !ID_N.Equals(ID_O, StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (item.IsWork())
                            $"ID: {item.ID}, {item.Illust.Title} Loading...".INFO();
                        else if (item.IsUser())
                            $"UserID: {item.ID}, {item.User.Name} Loading...".INFO();
                        //detail_page.Contents = item;
                        detail_page.UpdateDetail(item);
                    }

                    item.Focus();
                    Keyboard.Focus(item);
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                //if (!bg_prefetch.IsBusy && !bg_prefetch.CancellationPending) bg_prefetch.RunWorkerAsync(); 
            }
        }
    }
}
