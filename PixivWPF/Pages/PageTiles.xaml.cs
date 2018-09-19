using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
        private MetroWindow window = Application.Current.MainWindow as MetroWindow;
        internal ObservableCollection<ImageItem> ImageList = new ObservableCollection<ImageItem>();
        private Setting setting = Setting.Load();
        private PixivPage TargetPage = PixivPage.Recommanded;
        private string NextURL = null;
        private bool UPDATING = false;

        public async void UpdateImageTile(Pixeez.Tokens tokens)
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
                                ImageTiles.Items.Refresh();
                            }
                        }
                        catch (Exception ex)
                        {
                            await window.ShowMessageAsync("ERROR", ex.Message);
                        }
                    }));
                });
                UPDATING = false;
            }).Start();
        }

        public PageTiles()
        {
            InitializeComponent();

            ImageList.Clear();
            ImageTiles.ItemsSource = ImageList;

            ShowImages();
        }

        private async void ImageTiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var idx = ImageTiles.SelectedIndex;
                if (idx < 0) return;

                var item = ImageList[idx];
                var url = item.Illust.ImageUrls.Large;
                if (string.IsNullOrEmpty(url))
                {
                    url = item.Illust.ImageUrls.Medium;
                }
                PreviewWait.Visibility = Visibility.Visible;

                var tokens = Pixeez.Auth.AuthorizeWithAccessToken(item.AccessToken, setting.Proxy, setting.UsingProxy);
                Preview.Source = await url.ToImageSource(tokens);

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
                IllustTags.Foreground = Common.Theme.TextColorBrush;
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

                PreviewWait.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                await window.ShowMessageAsync("ERROR", ex.Message);
            }
        }

        public async void ShowImages(PixivPage target, string page)
        {
            if (TargetPage != target)
            {
                NextURL = null;
                ImageTiles.Items.Refresh();
                TargetPage = target;
            }

            ImageTiles.SelectedItems.Clear();
            ImageTiles.SelectedIndex = -1;
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
                case PixivPage.DailyTop:
                    ShowRanking(NextURL, "daily");
                    break;
                case PixivPage.WeeklyTop:
                    ShowRanking(NextURL, "weekly");
                    break;
                case PixivPage.MonthlyTop:
                    ShowRanking(NextURL, "monthly");
                    break;
            }
            //UpdateImageTile(tokens);
        }

        public async void ShowImages(PixivPage target = PixivPage.Recommanded, bool IsAppend = false)
        {
            if (TargetPage != target)
            {
                NextURL = null;
                ImageTiles.Items.Refresh();
                TargetPage = target;
            }
            if (!IsAppend)
            {
                ImageTiles.SelectedItems.Clear();
                ImageTiles.SelectedIndex = -1;
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
                case PixivPage.DailyTop:
                    ShowRanking(NextURL, "daily");
                    break;
                case PixivPage.WeeklyTop:
                    ShowRanking(NextURL, "weekly");
                    break;
                case PixivPage.MonthlyTop:
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
                var root = nexturl == null ? await tokens.GetRecommendedWorks() : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
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
                    UpdateImageTile(tokens);
                }
            }
            catch (Exception ex)
            {
                MetroWindow window = Application.Current.MainWindow as MetroWindow;
                await window.ShowMessageAsync("ERROR", ex.Message);
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

            var page = string.IsNullOrEmpty(NextURL) ? 1 : Convert.ToInt32(NextURL);
            try
            {
                ImageTilesWait.Visibility = Visibility.Visible;
                var root = await tokens.GetLatestWorksAsync(page);
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
                    UpdateImageTile(tokens);
                }
            }
            catch(Exception ex)
            {
                MetroWindow window = Application.Current.MainWindow as MetroWindow;
                await window.ShowMessageAsync("ERROR", ex.Message);
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

            ImageTilesWait.Visibility = Visibility.Visible;
            //var works = await tokens.GetMyFollowingWorksAsync("private");
            var condition = IsPrivate ? "private" : "public";
            if (setting.MyInfo != null && uid == 0) uid = (long)setting.MyInfo.Id;
            else if (uid <= 0) return;

            try
            {
                var root = nexturl == null ? await tokens.GetUserFavoriteWorksAsync(uid, condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
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
                    UpdateImageTile(tokens);
                }
            }
            catch (Exception ex)
            {
                MetroWindow window = Application.Current.MainWindow as MetroWindow;
                await window.ShowMessageAsync("ERROR", ex.Message);
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
                var root = nexturl == null ? await tokens.GetMyFollowingWorksAsync(condition) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
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
                    UpdateImageTile(tokens);
                }
            }
            catch (Exception ex)
            {
                MetroWindow window = Application.Current.MainWindow as MetroWindow;
                await window.ShowMessageAsync("ERROR", ex.Message);
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

            ImageTilesWait.Visibility = Visibility.Visible;
            var page = string.IsNullOrEmpty(NextURL) ? 1 : Convert.ToInt32(NextURL);
            var root = await tokens.GetRankingAllAsync(condition, page);
            nexturl = root.Pagination.Next.ToString() ?? string.Empty;
            NextURL = nexturl;
            ImageTilesWait.Visibility = Visibility.Hidden;

            try
            {
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
                            await window.ShowMessageAsync("ERROR", ex.Message);
                        }
                    }
                    ImageTilesWait.Visibility = Visibility.Hidden;
                    UpdateImageTile(tokens);
                }
            }
            catch (Exception ex)
            {
                MetroWindow window = Application.Current.MainWindow as MetroWindow;
                await window.ShowMessageAsync("ERROR", ex.Message);
            }
            finally
            {
                ImageTilesWait.Visibility = Visibility.Hidden;
            }
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

        private void ActionIllustRelative_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ActionSaveIllust_Click(object sender, RoutedEventArgs e)
        {

        }

    }

    public class ImageItem : FrameworkElement
    {
        public ImageSource Source { get; set; }
        public string Thumb { get; set; }
        public string Subject { get; set; }
        public string Caption { get; set; }
        public int Count { get; set; }
        public Visibility Badge { get; set; }
        public string UserID { get; set; }
        public string ID { get; set; }
        //public Pixeez.Objects.IllustWork Illust { get; set; }
        public Pixeez.Objects.Work Illust { get; set; }
        public string AccessToken { get; set; }
        public string NextURL { get; set; }

    }

    public static class ImageTileHelper
    {
        public static void AddTo(this IList<Pixeez.Objects.Work> works, IList<ImageItem> Colloection, string nexturl = "")
        {
            foreach (var illust in works)
            {
                illust.AddTo(Colloection, nexturl);
            }
        }

        public static async void AddTo(this Pixeez.Objects.Work illust, IList<ImageItem> Colloection, string nexturl = "")
        {
            try
            {
                if(illust is Pixeez.Objects.Work && Colloection is IList<ImageItem>)
                {
                    var url = illust.ImageUrls.SquareMedium;
                    if (string.IsNullOrEmpty(url))
                    {
                        if (!string.IsNullOrEmpty(illust.ImageUrls.Small))
                        {
                            url = illust.ImageUrls.Small;
                        }
                        else if (!string.IsNullOrEmpty(illust.ImageUrls.Px128x128))
                        {
                            url = illust.ImageUrls.Px128x128;
                        }
                        else if (!string.IsNullOrEmpty(illust.ImageUrls.Px480mw))
                        {
                            url = illust.ImageUrls.Px480mw;
                        }
                        else if (!string.IsNullOrEmpty(illust.ImageUrls.Medium))
                        {
                            url = illust.ImageUrls.Medium;
                        }
                        else if (!string.IsNullOrEmpty(illust.ImageUrls.Large))
                        {
                            url = illust.ImageUrls.Large;
                        }
                        else if (!string.IsNullOrEmpty(illust.ImageUrls.Original))
                        {
                            url = illust.ImageUrls.Original;
                        }
                    }

                    if (!string.IsNullOrEmpty(url))
                    {
                        var tooltip = string.IsNullOrEmpty(illust.Caption) ? string.Empty : string.Join("", illust.Caption.Take(256)).ToLineBreak(48);
                        var i = new ImageItem()
                        {
                            NextURL = nexturl,
                            Thumb = url,
                            Count = (int)illust.PageCount,
                            Badge = illust.PageCount > 1 ? Visibility.Visible : Visibility.Collapsed,
                            ID = illust.Id.ToString(),
                            UserID = illust.User.Id.ToString(),
                            Subject = illust.Title,
                            Caption = illust.Caption,
                            ToolTip = tooltip,
                            Illust = illust
                        };
                        Colloection.Add(i);
                    }
                }
            }
            catch (Exception ex)
            {
                MetroWindow window = Application.Current.MainWindow as MetroWindow;
                await window.ShowMessageAsync("ERROR", ex.Message);
            }
        }
    }

}
