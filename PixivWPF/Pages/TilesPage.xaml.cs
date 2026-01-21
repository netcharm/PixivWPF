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
    public partial class TilesPage : Page, IDisposable
    {
        public MainWindow ParentWindow { get; private set; }
        private IllustDetailPage _detail_page_ = new IllustDetailPage() { Name = "IllustDetail", ShowsNavigationUI = false };
        public IllustDetailPage DetailPage
        {
            get { return (_detail_page_); }
            set
            {
                _detail_page_ = value;
                IllustDetail.Content = _detail_page_;
                //IllustDetail.SandboxExternalContent = false;
                //IllustDetail.DataContext = _detail_page_;
                IllustDetail.Refresh();
                //IllustDetail.NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Automatic;
            }
        }

        internal string lastSelectedId = string.Empty;
        internal List<long> ids = new List<long>();
        private List<long> ids_last = new List<long>();

        private Setting setting = Application.Current.LoadSetting();
        public PixivPage TargetPage = PixivPage.None;
        private PixivPage LastPage = PixivPage.None;
        private string NextURL = null;

        public DateTime SelectedDate { get; set; } = DateTime.Now;

        #region Update UI helper
        internal void UpdateTheme()
        {
            if (DetailPage is IllustDetailPage)
                DetailPage.UpdateTheme();
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
                ImageTiles.UpdateTilesState(id: illustid ?? -1);
            }
        }

        public async void UpdateDownloadStateAsync(int? illustid = null, bool? exists = null)
        {
            await Task.Run(() =>
            {
                UpdateDownloadState(illustid, exists);
            });
        }

        public void UpdateLikeStateAsync(int illustid = -1, bool is_user = false)
        {
            if (ImageTiles.Items is ObservableCollection<PixivItem>)
            {
                ImageTiles.UpdateTilesState(id: illustid, is_user: is_user);
            }
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
            if (DetailPage is IllustDetailPage) DetailPage.UpdateThumb(true, overwrite);
        }

        public void UpdateTiles()
        {
            ShowImages(TargetPage, false, GetLastSelectedID());
        }

        internal string GetLastSelectedID()
        {
            string id = lastSelectedId;
            if (DetailPage is IllustDetailPage && DetailPage.Contents.IsWork())
            {
                id = DetailPage.Contents.ID;
            }
            else if (ImageTiles.Items.Count > 0)
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

        private void KeepLastSelected(string id = null, bool prefetching = true)
        {
            if (string.IsNullOrEmpty(id)) id = lastSelectedId;
            new Action(() =>
            {
                if (ImageTiles.ItemsCount > 0)
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
                    if (prefetching) Prefetching();
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
            try
            {
                for (int i = 0; i < CategoryMenu.Items?.Count; i++)
                {
                    if (GetPageTypeByCategory(CategoryMenu.Items[i]) == target)
                    {
                        result = i;
                        break;
                    }
                }
            }
            catch (Exception ex) { ex.LOG("GetCategoryIndex"); }
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
            if (PrefetchingImagesTask is PrefetchingTask) PrefetchingImagesTask.Start(reverse: true);
        }

        public void AppendTiles()
        {
            ShowImages(TargetPage, true, GetLastSelectedID());
        }

        public void OpenIllust()
        {
            if (DetailPage is IllustDetailPage) DetailPage.OpenIllust();
        }

        public void OpenCachedImage()
        {
            if (DetailPage is IllustDetailPage) DetailPage.OpenCachedImage();
        }

        public void OpenImageProperties()
        {
            if (DetailPage is IllustDetailPage) DetailPage.OpenImageProperties();
        }

        public void OpenWork()
        {
            if (DetailPage is IllustDetailPage) DetailPage.OpenInNewWindow();
        }

        public void OpenUser()
        {
            if (DetailPage is IllustDetailPage) DetailPage.OpenUser();
        }

        public void SaveIllust()
        {
            if (DetailPage is IllustDetailPage) DetailPage.SaveIllust();
        }

        public void SaveIllustAll()
        {
            if (DetailPage is IllustDetailPage) DetailPage.SaveIllustAll();
        }

        public void SaveUgoira()
        {
            //throw new NotImplementedException();
        }

        public void CopyPreview()
        {
            if (DetailPage is IllustDetailPage) DetailPage.CopyPreview();
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
            if (DetailPage is IllustDetailPage) DetailPage.PrevIllustPage();
        }

        public void NextIllustPage()
        {
            if (DetailPage is IllustDetailPage) DetailPage.NextIllustPage();
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
            var id = GetLastSelectedID();
            ImageTiles.SetFilter(filter);
            KeepLastSelected(id, prefetching: false);
        }

        public void SetFilter(FilterParam filter)
        {
            var id = GetLastSelectedID();
            ImageTiles.SetFilter(filter);
            KeepLastSelected(id, prefetching: false);
        }

        public dynamic GetTilesCount()
        {
            List<string> tips = new List<string>();
            tips.Add($"Illust  : {ImageTiles.ItemsCount} of {ImageTiles.Items.Count}");
            tips.Add($"Page    : {ImageTiles.CurrentPage} of {ImageTiles.TotalPages}");
            if (DetailPage is IllustDetailPage)
                tips.Add(DetailPage.GetTilesCount());
            return (string.Join(Environment.NewLine, tips));
        }
        #endregion

        #region Prefetching background task
        private PrefetchingTask PrefetchingImagesTask = null;

        private void InitPrefetchingTask()
        {
            if (PrefetchingImagesTask == null)
            {
                PrefetchingImagesTask = new PrefetchingTask()
                {
                    Name = "TilesPrefetching",
                    Items = ImageTiles.Items,
                    ReportProgressSlim = () =>
                    {
                        var percent = PrefetchingImagesTask.Percentage;
                        var tooltip = PrefetchingImagesTask.Comments;
                        var state = PrefetchingImagesTask.State;
                        if (ParentWindow is MainWindow) ParentWindow.SetPrefetchingProgress(percent, tooltip, state);
                        if (state == TaskStatus.RanToCompletion ||
                            state == TaskStatus.Faulted ||
                            state == TaskStatus.Canceled ||
                            (state != TaskStatus.WaitingForChildrenToComplete && percent >= 100))
                            ImageTiles.UpdateTilesImage();
                    },
                    ReportProgress = (percent, tooltip, state) =>
                    {
                        if (ParentWindow is MainWindow) ParentWindow.SetPrefetchingProgress(percent, tooltip, state);
                        if (state == TaskStatus.RanToCompletion ||
                            state == TaskStatus.Faulted ||
                            state == TaskStatus.Canceled ||
                            (state != TaskStatus.WaitingForChildrenToComplete && percent >= 100))
                            ImageTiles.UpdateTilesImage();
                    }
                };
            }
        }

        public void StopPrefetching()
        {
            if (PrefetchingImagesTask is PrefetchingTask) PrefetchingImagesTask.Stop();
            ImageTiles.Cancel();
        }
        #endregion

        public void Dispose()
        {
            if (PrefetchingImagesTask is PrefetchingTask) PrefetchingImagesTask.Dispose();
        }

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

            InitPrefetchingTask();

            ids.Clear();
            ImageTiles.AutoGC = true;
            ImageTiles.WaitGC = true;
            ImageTiles.CalcSystemMemoryUsage = setting.CalcSystemMemoryUsage;
            this.DoEvents();

            await new Action(async () =>
            {
                await Task.Delay(1);
                IllustDetail.Content = DetailPage;
                this.DoEvents();
                CategoryMenu.IsPaneOpen = false;
                this.DoEvents();
                CategoryMenu.SelectedIndex = GetCategoryIndex(setting.DefaultPage);
                this.DoEvents();
            }).InvokeAsync(true);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private void Page_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var change_detail_page = setting.SmartMouseResponse && e.Source == IllustDetail;

            if (ParentWindow is MainWindow) (ParentWindow as MainWindow).InSearching = false;

            if (Keyboard.Modifiers == ModifierKeys.Shift) change_detail_page = !change_detail_page;

            setting = Application.Current.LoadSetting();

            if (change_detail_page)
            {
                if (e.XButton1 == MouseButtonState.Pressed && e.LeftButton == MouseButtonState.Released)
                {
                    e.Handled = true;
                    if (DetailPage is IllustDetailPage)
                    {
                        if (setting.ReverseMouseXButton) DetailPage.NextIllustPage();
                        else DetailPage.PrevIllustPage();
                    }
                }
                else if (e.XButton2 == MouseButtonState.Pressed && e.LeftButton == MouseButtonState.Released)
                {
                    e.Handled = true;
                    if (DetailPage is IllustDetailPage)
                    {
                        if (setting.ReverseMouseXButton) DetailPage.PrevIllustPage();
                        else DetailPage.NextIllustPage();
                    }
                }
            }
            else
            {
                if (e.XButton1 == MouseButtonState.Pressed && e.LeftButton == MouseButtonState.Released)
                {
                    e.Handled = true;
                    if (setting.ReverseMouseXButton) NextIllust();
                    else PrevIllust();
                }
                else if (e.XButton2 == MouseButtonState.Pressed && e.LeftButton == MouseButtonState.Released)
                {
                    e.Handled = true;
                    if (setting.ReverseMouseXButton) PrevIllust();
                    else NextIllust();
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

        #region Hamburger Category Menu
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
                if (Keyboard.Modifiers == ModifierKeys.Control) SelectedDate = DateTime.Now;
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
        #endregion

        private async void ImageTiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var idx = ImageTiles.SelectedIndex;
                if (idx < 0) return;

                if (ParentWindow is MainWindow && ParentWindow.InSearching) ParentWindow.InSearching = false;

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
                        item.IsDownloaded = item.Illust.IsPartDownloadedAsync(touch: false);
                        item.IsFavorited = item.IsLiked();
                        item.IsFollowed = item.User.IsLiked();
                    }

                    var ID_O = DetailPage.Contents is PixivItem ? DetailPage.Contents.ID : string.Empty;
                    var ID_N = item is PixivItem ? item.ID : string.Empty;

                    if (string.IsNullOrEmpty(ID_O) || !ID_N.Equals(ID_O, StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (item.IsWork())
                            $"ID: {item.ID}, {item.Illust.Title} Loading...".INFO();
                        else if (item.IsUser())
                            $"UserID: {item.ID}, {item.User.Name} Loading...".INFO();
                        DetailPage.UpdateDetail(item);
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

        internal void ShowImages(PixivPage target = PixivPage.Recommanded, bool IsAppend = false, string id = "")
        {
            if (ParentWindow == null) ParentWindow = this.GetMainWindow();
            if (target == PixivPage.My) { ShowUser(0, true); return; }
            if (ParentWindow is MainWindow) ParentWindow.UpdateTitle(target.ToString());
            this.DoEvents();

            InitPrefetchingTask();
            if (PrefetchingImagesTask is PrefetchingTask) PrefetchingImagesTask.Stop();

            //if (ids.Count > 0)
            //{
            //    ids_last.Clear();
            //    ids_last.AddRange(ids);
            //}

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

            setting = Application.Current.LoadSetting();

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
            var tokens = await CommonHelper.ShowLogin();
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
                    "No Result".ShowToast("INFO", tag: "ShowRecommanded");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowRecommanded");
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
            var tokens = await CommonHelper.ShowLogin();
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
                    "No Result".ShowToast("INFO", tag: "ShowLatest");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowLatest");
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
            var tokens = await CommonHelper.ShowLogin();
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
                    "No Result".ShowToast("INFO", tag: "ShowTrendingTags");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowTrendingTags");
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
            var tokens = await CommonHelper.ShowLogin();
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
                    "No Result".ShowToast("INFO", tag: "ShowFeeds");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowFeeds");
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
            var force = setting.MyInfo is Pixeez.Objects.User ? false : true;
            var tokens = await CommonHelper.ShowLogin(force);
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
                    "No Result".ShowToast("INFO", tag: "ShowFeeds");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowFeeds");
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
            var tokens = await CommonHelper.ShowLogin(setting.MyInfo == null && IsPrivate);
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
                    "No Result".ShowToast("INFO", tag: "ShowFavorite");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowFavorite");
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
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                var condition = IsPrivate ? "private" : "public";
                var root = string.IsNullOrEmpty(nexturl) ? await tokens.GetMyFollowingWorksAsync(condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                if (root != null)
                {
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
                    "No Result".ShowToast("INFO", tag: "ShowFollowing");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowFollowing");
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
            var tokens = await CommonHelper.ShowLogin();
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
                    "No Result".ShowToast("INFO", tag: "ShowRankingAll");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowRankingAll");
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
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            try
            {
                ImageTiles.Wait();

                if (string.IsNullOrEmpty(nexturl)) ids.Clear();

                var rank_date_fmt = "yyyy-MM-dd";
                var date = SelectedDate.Date == DateTime.Now.Date ? DateTime.Now : (SelectedDate - TimeSpan.FromDays(setting.RankingDateOffset));
                date = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(date, Application.Current.GetTokyoTimeZone().Id);
                var root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRankingAsync(condition, 1, 30, date.ToString(rank_date_fmt)) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                int count = 1;
                while (count <= 7 && (root.illusts == null || root.illusts.Length <= 0))
                {
                    date = SelectedDate - TimeSpan.FromDays(setting.RankingDateOffset + count);
                    root = string.IsNullOrEmpty(nexturl) ? await tokens.GetRankingAsync(condition, 1, 30, date.ToString(rank_date_fmt)) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
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
                    "No Result".ShowToast("INFO", tag: "ShowRanking");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowRanking");
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
                Pixeez.Objects.UserBase user = null;
                if (uid == 0)
                {
                    uid = setting.MyID;
                    user = setting.MyInfo;
                }
                else
                {
                    user = uid.FindUser();
                }

                if (Keyboard.Modifiers == ModifierKeys.Control || !(user is Pixeez.Objects.User))
                    user = await uid.RefreshUser();
                Commands.Open.Execute(user);
            }
        }

        private async void ShowMyFollower(long uid, string nexturl = null)
        {            
            var tokens = await CommonHelper.ShowLogin();
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
                    "No Result".ShowToast("INFO", tag: "ShowMyFollower");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowMyFollower");
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
            var tokens = await CommonHelper.ShowLogin();
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
                    "No Result".ShowToast("INFO", tag: "ShowMyFollowing");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowMyFollowing");
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
            var tokens = await CommonHelper.ShowLogin();
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
                    "No Result".ShowToast("INFO", tag: "ShowMyPixiv");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowMyPixiv");
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
            var tokens = await CommonHelper.ShowLogin();
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
                    "No Result".ShowToast("INFO", tag: "ShowMyBlacklist");
                }
                else
                {
                    ex.Message.ShowToast("ERROR", tag: "ShowMyBlacklist");
                }
            }
            finally
            {
                ImageTiles.Ready();
                KeepLastSelected(lastSelectedId);
            }
        }
        #endregion
    }
}
