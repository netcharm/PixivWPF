using MahApps.Metro.IconPacks;
using PixivWPF.Common;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
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

namespace PixivWPF.Pages
{
    /// <summary>
    /// IllustDetailPage.xaml 的交互逻辑
    /// </summary>
    public partial class IllustDetailPage : Page
    {
        internal object DataObject = null;
        internal Task lastTask = null;
        internal CancellationTokenSource cancelTokenSource;
        internal CancellationToken cancelToken;

        public ICommand MouseDoubleClickCommand { get; } = new DelegateCommand<object>(obj => {
            MessageBox.Show("");
        });

        //private ICommand Cmd_CopyIllustIDs { get; } = new DelegateCommand<object>(obj =>
        //{
        //    if (obj is ImageListGrid)
        //    {
        //        var list = obj as ImageListGrid;
        //        var ids = new  List<string>();
        //        foreach (var item in list.SelectedItems)
        //        {
        //            if (list.Name.Equals("RelativeIllusts", StringComparison.CurrentCultureIgnoreCase) ||
        //                list.Name.Equals("ResultIllusts", StringComparison.CurrentCultureIgnoreCase) ||
        //                list.Name.Equals("FavoriteIllusts", StringComparison.CurrentCultureIgnoreCase))
        //            {
        //                ids.Add($"{item.ID}");
        //            }
        //        }
        //        Clipboard.SetText(string.Join("\n", ids));
        //    }
        //});

        public void UpdateTheme()
        {
            //var fonts = string.Join(",", IllustTitle.FontFamily.FamilyNames.Values);

            var style = new StringBuilder();
            //style.AppendLine($"*{{font-family:FontAwesome, \"Segoe UI Emoji\", \"Segoe MDL2 Assets\", Monaco, Consolas, \"Courier New\", \"Segoe UI\", monospace, 思源黑体, 思源宋体, 微软雅黑, 宋体, 黑体, 楷体, \"WenQuanYi Microhei\", \"WenQuanYi Microhei Mono\", \"Microsoft YaHei\", Tahoma, Arial, Helvetica, sans-serif;}}");
            style.AppendLine($"*{{font-family:FontAwesome, \"Segoe UI Emoji\", \"Segoe MDL2 Assets\", Consolas, \"Courier New\", \"Segoe UI\", 思源黑体, 思源宋体, 微软雅黑, 宋体, 黑体, 楷体, Tahoma, Arial, Helvetica, sans-serif |important;}}");
            style.AppendLine($"body{{font-family:FontAwesome, \"Segoe UI Emoji\", \"Segoe MDL2 Assets\", Consolas, \"Courier New\", \"Segoe UI\", 思源黑体, 思源宋体, 微软雅黑, 宋体, 黑体, 楷体, Tahoma, Arial, Helvetica, sans-serif |important;}}");
            style.AppendLine($".tag{{background-color:{Common.Theme.AccentColor.ToHtml(false)}|important;color:{Common.Theme.TextColor.ToHtml(false)}|important;margin:4px;text-decoration:none;}}");
            style.AppendLine($".desc{{color:{Common.Theme.TextColor.ToHtml(false)} !important;text-decoration:none !important;}}");
            style.AppendLine($"a{{color:{Common.Theme.TextColor.ToHtml(false)}|important;text-decoration:none !important;}}");
            style.AppendLine($"img{{width:auto!important;;height:auto!important;;max-width:100%!important;;max-height:100% !important;}}");

            var BaseStyleSheet = string.Join("\n", style);
            IllustTags.BaseStylesheet = BaseStyleSheet;
            IllustDesc.BaseStylesheet = BaseStyleSheet;
            //IllustDesc.FontFamily = IllustTitle.FontFamily;

            var tags = IllustTags.Text;
            var desc = IllustDesc.Text;

            IllustTags.Text = string.Empty;
            IllustDesc.Text = string.Empty;

            IllustTags.Text = tags;
            IllustDesc.Text = desc;

            IllustDesc.AvoidImagesLateLoading = true;
        }

        public async void UpdateDetailTask(ImageItem item)
        {
            try
            {
                IllustDetailWait.Visibility = Visibility.Visible;

                var tokens = await CommonHelper.ShowLogin();
                DataObject = item;

                var dpi = new DPI();
                Preview.Source = new WriteableBitmap(300, 300, dpi.X, dpi.Y, PixelFormats.Bgra32, BitmapPalettes.WebPalette);
                PreviewWait.Visibility = Visibility.Visible;

                string stat_viewed = "????";
                string stat_favorited = "????";
                var stat_tip = new List<string>();
                if (item.Illust is Pixeez.Objects.IllustWork)
                {
                    var illust = item.Illust as Pixeez.Objects.IllustWork;
                    stat_viewed = $"{illust.total_view}";
                    stat_favorited = $"{illust.total_bookmarks}";
                    stat_tip.Add($"Viewed    : {illust.total_view}");
                    stat_tip.Add($"Favorited : {illust.total_bookmarks}");
                }
                if (item.Illust.Stats != null)
                {
                    stat_viewed = $"{item.Illust.Stats.ViewsCount}";
                    stat_favorited = $"{item.Illust.Stats.FavoritedCount.Public} / {item.Illust.Stats.FavoritedCount.Private}";
                    stat_tip.Add($"Scores    : {item.Illust.Stats.Score}");
                    stat_tip.Add($"Viewed    : {item.Illust.Stats.ViewsCount}");
                    stat_tip.Add($"Scored    : {item.Illust.Stats.ScoredCount}");
                    stat_tip.Add($"Comments  : {item.Illust.Stats.CommentedCount}");
                    stat_tip.Add($"Favorited : {item.Illust.Stats.FavoritedCount.Public} / {item.Illust.Stats.FavoritedCount.Private}");
                }
                stat_tip.Add($"Size      : {item.Illust.Width}x{item.Illust.Height}");

                IllustSize.Text = $"{item.Illust.Width}x{item.Illust.Height}";
                IllustViewed.Text = stat_viewed;
                IllustFavorited.Text = stat_favorited;

                IllustStatInfo.Visibility = Visibility.Visible;
                IllustStatInfo.ToolTip = string.Join("\r", stat_tip).Trim();

                IllustAuthor.Text = item.Illust.User.Name;
                IllustAuthorAvator.Source = new WriteableBitmap(64, 64, dpi.X, dpi.Y, PixelFormats.Bgra32, BitmapPalettes.WebPalette);
                IllustAuthorAvatorWait.Visibility = Visibility.Visible;

                IllustTitle.Text = $"{item.Illust.Title}";
                ActionCopyIllustDate.Header = item.Illust.GetDateTime().ToString("yyyy-MM-dd HH:mm:sszzz");

                FollowAuthor.Visibility = Visibility.Visible;
                if (item.Illust.User.is_followed == true ||
                    (item.Illust.User is Pixeez.Objects.User && (item.Illust.User as Pixeez.Objects.User).IsFollowing == true))
                {
                    FollowAuthor.Tag = PackIconModernKind.Check;// "Check";
                    ActionFollowAuthorRemove.IsEnabled = true;
                }
                else
                {
                    FollowAuthor.Tag = PackIconModernKind.Add;// "Add";
                    ActionFollowAuthorRemove.IsEnabled = false;
                }

                BookmarkIllust.Visibility = Visibility.Visible;
                if (item.Illust.IsBookMarked())
                {
                    BookmarkIllust.Tag = PackIconModernKind.Heart;// "Heart";
                    ActionBookmarkIllustRemove.IsEnabled = true;
                }
                else
                {
                    BookmarkIllust.Tag = PackIconModernKind.HeartOutline;// "HeartOutline";
                    ActionBookmarkIllustRemove.IsEnabled = false;
                }

                IllustActions.Visibility = Visibility.Visible;

                if (item.Illust.Tags.Count > 0)
                {
                    var html = new StringBuilder();
                    foreach (var tag in item.Illust.Tags)
                    {
                        //html.AppendLine($"<a href=\"https://www.pixiv.net/search.php?s_mode=s_tag_full&word={Uri.EscapeDataString(tag)}\" class=\"tag\" data-tag=\"{tag}\">{tag}</a>");
                        html.AppendLine($"<a href=\"https://www.pixiv.net/search.php?s_mode=s_tag&word={Uri.EscapeDataString(tag)}\" class=\"tag\" data-tag=\"{tag}\">{tag}</a>");
                        //html.AppendLine($"<button class=\"tag\" data-tag=\"{tag}\">{tag}</button>");
                    }
                    IllustTags.Foreground = Common.Theme.TextBrush;
                    IllustTags.Text = string.Join(";", html);
                    IllustTagExpander.Header = "Tags";
                    IllustTagExpander.Visibility = Visibility.Visible;
                }
                else IllustTagExpander.Visibility = Visibility.Collapsed;

                if (!string.IsNullOrEmpty(item.Illust.Caption) && item.Illust.Caption.Length > 0)
                {
                    IllustDesc.Text = $"<div class=\"desc\">{item.Illust.Caption}</div>".Replace("\r\n", "<br/>");
                    IllustDescExpander.Visibility = Visibility.Visible;
                }
                else
                {
                    IllustDescExpander.Visibility = Visibility.Collapsed;
                }

                SubIllusts.Items.Clear();
                //SubIllusts.Refresh();
                SubIllustsExpander.IsExpanded = false;
                PreviewBadge.Badge = item.Illust.PageCount;
                if (item.Illust is Pixeez.Objects.IllustWork && item.Illust.PageCount > 1)
                {
                    PreviewBadge.Visibility = Visibility.Visible;
                    SubIllustsExpander.Visibility = Visibility.Visible;
                    SubIllustsNavPanel.Visibility = Visibility.Visible;
                    //System.Threading.Thread.Sleep(250);
                    SubIllustsExpander.IsExpanded = true;
                }
                //else if (item.Illust is Pixeez.Objects.NormalWork && item.Illust.Metadata != null && item.Illust.PageCount > 1)
                else if (item.Illust is Pixeez.Objects.NormalWork && item.Illust.PageCount > 1)
                {
                    PreviewBadge.Visibility = Visibility.Visible;
                    SubIllustsExpander.Visibility = Visibility.Visible;
                    SubIllustsNavPanel.Visibility = Visibility.Visible;
                    //System.Threading.Thread.Sleep(250);
                    SubIllustsExpander.IsExpanded = true;
                }
                else
                {
                    SubIllustsExpander.Visibility = Visibility.Collapsed;
                    SubIllustsNavPanel.Visibility = Visibility.Collapsed;
                    PreviewBadge.Visibility = Visibility.Collapsed;
                }

                RelativeIllustsExpander.Header = "Related Illusts";
                RelativeIllustsExpander.Visibility = Visibility.Visible;
                RelativeNextPage.Visibility = Visibility.Collapsed;
                RelativeIllustsExpander.IsExpanded = false;

                FavoriteIllustsExpander.Header = "Author Favorite";
                FavoriteIllustsExpander.Visibility = Visibility.Visible;
                FavoriteNextPage.Visibility = Visibility.Collapsed;
                FavoriteIllustsExpander.IsExpanded = false;

                if (cancelToken.IsCancellationRequested)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    return;
                }
                IllustAuthorAvator.Source = await item.Illust.User.GetAvatarUrl().LoadImage(tokens);
                IllustAuthorAvatorWait.Visibility = Visibility.Collapsed;
                var img = await item.Illust.GetPreviewUrl().LoadImage(tokens);
                if (cancelToken.IsCancellationRequested)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    return;
                }
                Preview.Source = img;
                //if (Preview.Source == null || Preview.Source.Width < 450)
                if (Preview.Source == null || Preview.Source.Width < 350)
                {
                    if (cancelToken.IsCancellationRequested)
                    {
                        cancelToken.ThrowIfCancellationRequested();
                        return;
                    }
                    var large = await item.Illust.GetOriginalUrl().LoadImage(tokens);
                    if (cancelToken.IsCancellationRequested)
                    {
                        cancelToken.ThrowIfCancellationRequested();
                        return;
                    }
                    if (large != null) Preview.Source = large;
                }
                if (Preview.Source != null) Preview.Visibility = Visibility.Visible;
                PreviewWait.Visibility = Visibility.Collapsed;

                IllustDetailWait.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                IllustDetailWait.Visibility = Visibility.Hidden;
            }
        }

        public async void UpdateDetail(ImageItem item)
        {
            try
            {
                if (lastTask is Task)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    lastTask.Wait();
                }

                if (lastTask == null || (lastTask is Task && (lastTask.IsCanceled || lastTask.IsCompleted || lastTask.IsFaulted)))
                {
                    lastTask = new Task(() =>
                    {
                        UpdateDetailTask(item);
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
                IllustDetailWait.Visibility = Visibility.Hidden;
            }
        }

        public async void UpdateDetail(Pixeez.Objects.UserBase user)
        {
            try
            {
                IllustDetailWait.Visibility = Visibility.Visible;

                var tokens = await CommonHelper.ShowLogin();

                var UserInfo = await tokens.GetUserInfoAsync(user.Id.Value.ToString());
                var nuser = UserInfo.user;
                var nprof = UserInfo.profile;
                var nworks = UserInfo.workspace;

                DataObject = user;

                Preview.Visibility = Visibility.Collapsed;
                Preview.Source = null;
                //if (nprof.background_image_url is string)
                //    Preview.Source = await ((string)nprof.background_image_url).LoadImage(tokens);
                //else
                //    Preview.Source = await nuser.GetPreviewUrl().LoadImage(tokens);

                IllustSizeIcon.Kind = PackIconModernKind.Image;
                IllustSize.Text = $"{nprof.total_illusts}";
                IllustViewedIcon.Kind = PackIconModernKind.Check;
                IllustViewed.Text = $"{nprof.total_follow_users}";
                IllustFavorited.Text = $"{nprof.total_illust_bookmarks_public}";

                IllustStatInfo.Visibility = Visibility.Visible;

                IllustAuthor.Text = nuser.Name;
                IllustAuthorAvator.Source = await nuser.GetAvatarUrl().LoadImage(tokens);
                IllustTitle.Text = string.Empty;

                FollowAuthor.Visibility = Visibility.Visible;
                if (nuser.is_followed.Value)
                {
                    FollowAuthor.Tag = PackIconModernKind.Check;// "Check";
                    ActionFollowAuthorRemove.IsEnabled = true;
                }
                else
                {
                    FollowAuthor.Tag = PackIconModernKind.Add;// "Add";
                    ActionFollowAuthorRemove.IsEnabled = false;
                }

                BookmarkIllust.Visibility = Visibility.Collapsed;
                IllustActions.Visibility = Visibility.Collapsed;

                if (nuser != null && nprof != null && nworks != null)
                {
                    StringBuilder desc = new StringBuilder();
                    desc.AppendLine($"Account:<br/> {nuser.Account} / [{nuser.Id}] / {nuser.Name} / {nuser.Email}");
                    desc.AppendLine($"<br/>Stat:<br/> {nprof.total_illust_bookmarks_public} Bookmarked / {nprof.total_follower} Following / {nprof.total_follow_users} Follower /<br/> {nprof.total_illusts} Illust / {nprof.total_manga} Manga / {nprof.total_novels} Novels /<br/> {nprof.total_mypixiv_users} MyPixiv User");
                    desc.AppendLine($"<hr/>");

                    desc.AppendLine($"<br/>Profile:<br/> {nprof.gender} / {nprof.birth} / {nprof.region} / {nprof.job}");
                    desc.AppendLine($"<br/>Contacts:<br/>twitter: <a href=\"{nprof.twitter_url}\">@{nprof.twitter_account}</a> / web: {nprof.webpage}");
                    desc.AppendLine($"<hr/>");

                    desc.AppendLine($"<br/>Workspace Device_:<br/> {nworks.pc} / {nworks.monitor} / {nworks.tablet} / {nworks.mouse} / {nworks.printer} / {nworks.scanner} / {nworks.tool}");
                    desc.AppendLine($"<br/>Workspace Environment:<br/> {nworks.desk} / {nworks.chair} / {nworks.desktop} / {nworks.music} / {nworks.comment}");

                    if (!string.IsNullOrEmpty(nworks.workspace_image_url))
                    {
                        desc.AppendLine($"<hr/>");
                        desc.AppendLine($"<br/>Workspace Images:<br/> <img src=\"{nworks.workspace_image_url}\"/>");
                    }

                    IllustTags.Foreground = Common.Theme.TextBrush;
                    IllustTags.Text = string.Join(";", desc);
                    IllustTagExpander.Header = "User Infomation";
                    IllustTagExpander.Visibility = Visibility.Visible;
                    IllustTagExpander.IsExpanded = false;
                }
                else IllustTagExpander.Visibility = Visibility.Collapsed;

                if (!string.IsNullOrEmpty(nuser.comment) && nuser.comment.Length > 0)
                {
                    StringBuilder desc = new StringBuilder();
                    desc.AppendLine($"{nuser.comment}");
                    IllustDesc.Text = $"<div class=\"desc\">{string.Join("<br></br>\n", desc)}</div>";
                    IllustDescExpander.Visibility = Visibility.Visible;
                }
                else
                {
                    IllustDescExpander.Visibility = Visibility.Collapsed;
                }

                SubIllusts.Items.Clear();
                //SubIllusts.Refresh();
                SubIllustsExpander.IsExpanded = false;
                SubIllustsExpander.Visibility = Visibility.Collapsed;
                SubIllustsNavPanel.Visibility = Visibility.Collapsed;
                PreviewBadge.Visibility = Visibility.Collapsed;

                RelativeIllustsExpander.Header = "Illusts";
                RelativeIllustsExpander.Visibility = Visibility.Visible;
                RelativeNextPage.Visibility = Visibility.Collapsed;
                RelativeIllustsExpander.IsExpanded = false;

                FavoriteIllustsExpander.Header = "Favorite";
                FavoriteIllustsExpander.Visibility = Visibility.Visible;
                FavoriteNextPage.Visibility = Visibility.Collapsed;
                FavoriteIllustsExpander.IsExpanded = false;

                IllustDetailWait.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                IllustDetailWait.Visibility = Visibility.Hidden;
            }
        }

        internal async void ShowIllustPages(Pixeez.Tokens tokens, ImageItem item, int start = 0, int count = 30)
        {
            try
            {
                IllustDetailWait.Visibility = Visibility.Visible;

                //SubIllustsPanel.Visibility = Visibility.Collapsed;
                SubIllusts.Items.Clear();
                if (item.Illust is Pixeez.Objects.IllustWork)
                {
                    //System.Threading.Thread.Sleep(250);
                    var subset = item.Illust as Pixeez.Objects.IllustWork;
                    if (subset.meta_pages.Count() > 1)
                    {
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
                        SubIllusts.UpdateImageTiles(tokens);
                        var nullimages = SubIllusts.Items.Where(img => img.Source == null);
                        if (nullimages.Count() > 0)
                        {
                            //System.Threading.Thread.Sleep(250);
                            SubIllusts.UpdateImageTiles(tokens);
                        }
                    }
                }
                else if (item.Illust is Pixeez.Objects.NormalWork)
                {
                    //System.Threading.Thread.Sleep(250);
                    var subset = item.Illust as Pixeez.Objects.NormalWork;
                    if(subset.Metadata == null)
                    {
                        var illusts = await tokens.GetWorksAsync(item.Illust.Id.Value);
                        foreach (var illust in illusts)
                        {
                            item.Illust = illust;
                            subset = illust;
                            break;
                        }
                    }

                    if (subset.Metadata != null && subset.Metadata.Pages != null && subset.Metadata.Pages.Count() > 1)
                    {
                        btnSubIllustPrevPages.Tag = start - count;
                        var total = subset.Metadata.Pages.Count();
                        for (var i = start; i < total; i++)
                        {
                            if (i < 0) continue;

                            var pages = subset.Metadata.Pages[i];
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
                        SubIllusts.UpdateImageTiles(tokens);
                        var nullimages = SubIllusts.Items.Where(img => img.Source == null);
                        if (nullimages.Count() > 0)
                        {
                            //System.Threading.Thread.Sleep(250);
                            SubIllusts.UpdateImageTiles(tokens);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                IllustDetailWait.Visibility = Visibility.Collapsed;
            }

        }

        internal async void ShowRelativeInline(Pixeez.Tokens tokens, ImageItem item, string next_url="")
        {
            try
            {
                IllustDetailWait.Visibility = Visibility.Visible;
                RelativeIllusts.Items.Clear();

                var lastUrl = next_url;
                var relatives = string.IsNullOrEmpty(next_url) ? await tokens.GetRelatedWorks(item.Illust.Id.Value) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = relatives.next_url ?? string.Empty;

                if (relatives.illusts is Array)
                {
                    //if (relatives.illusts.Length < 30) RelativeNextPage.Visibility = Visibility.Collapsed;
                    //else 
                    if (next_url.Equals(lastUrl, StringComparison.CurrentCultureIgnoreCase))
                        RelativeNextPage.Visibility = Visibility.Collapsed;
                    else RelativeNextPage.Visibility = Visibility.Visible;

                    RelativeIllustsExpander.Tag = next_url;
                    foreach (var illust in relatives.illusts)
                    {
                        illust.AddTo(RelativeIllusts.Items, relatives.next_url);
                    }
                    RelativeIllusts.UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                IllustDetailWait.Visibility = Visibility.Collapsed;
            }
        }

        internal async void ShowUserWorksInline(Pixeez.Tokens tokens, Pixeez.Objects.UserBase user, string next_url = "")
        {
            try
            {
                IllustDetailWait.Visibility = Visibility.Visible;
                RelativeIllusts.Items.Clear();

                var lastUrl = next_url;
                var relatives = string.IsNullOrEmpty(next_url) ? await tokens.GetUserWorksAsync(user.Id.Value) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = relatives.next_url ?? string.Empty;

                if (relatives.illusts is Array)
                {
                    //if (relatives.illusts.Length < 30) RelativeNextPage.Visibility = Visibility.Collapsed;
                    //else 
                    if (next_url.Equals(lastUrl, StringComparison.CurrentCultureIgnoreCase))
                        RelativeNextPage.Visibility = Visibility.Collapsed;
                    else RelativeNextPage.Visibility = Visibility.Visible;

                    RelativeIllustsExpander.Tag = next_url;
                    foreach (var illust in relatives.illusts)
                    {
                        illust.AddTo(RelativeIllusts.Items, relatives.next_url);
                    }
                    RelativeIllusts.UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                IllustDetailWait.Visibility = Visibility.Collapsed;
            }
        }

        internal async void ShowFavoriteInline(Pixeez.Tokens tokens, Pixeez.Objects.UserBase user, string next_url = "")
        {
            try
            {
                IllustDetailWait.Visibility = Visibility.Visible;
                FavoriteIllusts.Items.Clear();

                var lastUrl = next_url;
                var relatives = string.IsNullOrEmpty(next_url) ? await tokens.GetUserFavoriteWorksAsync(user.Id.Value) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = relatives.next_url ?? string.Empty;

                if (relatives.illusts is Array)
                {
                    //if (relatives.illusts.Length < 30) FavoriteNextPage.Visibility = Visibility.Collapsed;
                    //else 
                    if (next_url.Equals(lastUrl, StringComparison.CurrentCultureIgnoreCase))
                        FavoriteNextPage.Visibility = Visibility.Collapsed;
                    else FavoriteNextPage.Visibility = Visibility.Visible;

                    FavoriteIllustsExpander.Tag = next_url;
                    foreach (var illust in relatives.illusts)
                    {
                        illust.AddTo(FavoriteIllusts.Items, relatives.next_url);
                    }
                    FavoriteIllusts.UpdateImageTiles(tokens);
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                IllustDetailWait.Visibility = Visibility.Collapsed;
            }
        }

        public IllustDetailPage()
        {
            InitializeComponent();

            RelativeIllusts.Columns = 5;

            IllustDetailWait.Visibility = Visibility.Collapsed;

            cancelTokenSource = new CancellationTokenSource();
            cancelToken = cancelTokenSource.Token;

            UpdateTheme();
        }

        private async void IllustTags_LinkClicked(object sender, TheArtOfDev.HtmlRenderer.WPF.RoutedEvenArgs<TheArtOfDev.HtmlRenderer.Core.Entities.HtmlLinkClickedEventArgs> args)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (args.Data is TheArtOfDev.HtmlRenderer.Core.Entities.HtmlLinkClickedEventArgs)
            {
                var link = args.Data as TheArtOfDev.HtmlRenderer.Core.Entities.HtmlLinkClickedEventArgs;

                if (link.Attributes.ContainsKey("data-tag"))
                {
                    args.Handled = true;
                    link.Handled = true;

                    var tag  = link.Attributes["data-tag"];
                    if(Keyboard.Modifiers == ModifierKeys.Control)
                        CommonHelper.Cmd_Search.Execute($"Tag:{tag}");
                    else
                        CommonHelper.Cmd_Search.Execute($"Fuzzy Tag:{tag}");

                    //if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                    //    CommonHelper.Cmd_Search.Execute($"Tag:{tag}");
                    //else
                    //    CommonHelper.Cmd_Search.Execute($"Fuzzy Tag:{tag}");
                }
            }
        }

        private async void IllustTags_ImageLoad(object sender, TheArtOfDev.HtmlRenderer.WPF.RoutedEvenArgs<TheArtOfDev.HtmlRenderer.Core.Entities.HtmlImageLoadEventArgs> args)
        {
            if (args.Data is TheArtOfDev.HtmlRenderer.Core.Entities.HtmlImageLoadEventArgs)
            {
                var img = args.Data as TheArtOfDev.HtmlRenderer.Core.Entities.HtmlImageLoadEventArgs;

                if (string.IsNullOrEmpty(img.Src)) return;

                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                var src = await img.Src.LoadImagePath(tokens);
                img.Callback(src);
                img.Handled = true;
                args.Handled = true;
            }
        }

        private async void IllustDesc_LinkClicked(object sender, TheArtOfDev.HtmlRenderer.WPF.RoutedEvenArgs<TheArtOfDev.HtmlRenderer.Core.Entities.HtmlLinkClickedEventArgs> args)
        {
            if (args.Data is TheArtOfDev.HtmlRenderer.Core.Entities.HtmlLinkClickedEventArgs)
            {
                var link = args.Data as TheArtOfDev.HtmlRenderer.Core.Entities.HtmlLinkClickedEventArgs;

                if (link.Attributes.ContainsKey("href"))
                {
                    args.Handled = true;
                    link.Handled = true;

                    var href = link.Attributes["href"];
                    if (href.StartsWith("pixiv://illusts/"))
                    {
                        var illust_id = Regex.Replace(href, @"pixiv://illusts/(\d+)", "$1", RegexOptions.IgnoreCase);
                        if (!string.IsNullOrEmpty(illust_id))
                        {
                            var tokens = await CommonHelper.ShowLogin();
                            if (tokens == null) return;

                            var illusts = await tokens.GetWorksAsync(Convert.ToInt32(illust_id));
                            foreach(var illust in illusts)
                            {
                                if (illust is Pixeez.Objects.Work)
                                {
                                    CommonHelper.Cmd_OpenIllust.Execute(illust);
                                }
                            }
                        }
                    }
                    else if (href.StartsWith("pixiv://users/"))
                    {
                        var user_id = Regex.Replace(href, @"pixiv://users/(\d+)", "$1", RegexOptions.IgnoreCase);
                        if (!string.IsNullOrEmpty(user_id))
                        {
                            var tokens = await CommonHelper.ShowLogin();
                            if (tokens == null) return;

                            var users = await tokens.GetUsersAsync(Convert.ToInt32(user_id));
                            foreach (var user in users)
                            {
                                if (user is Pixeez.Objects.User)
                                {
                                    CommonHelper.Cmd_OpenIllust.Execute(user);
                                }
                            }
                        }
                    }
                    else
                    {
                        args.Handled = false;
                        link.Handled = false;
                    }
                }
            }
        }

        private async void IllustDesc_ImageLoad(object sender, TheArtOfDev.HtmlRenderer.WPF.RoutedEvenArgs<TheArtOfDev.HtmlRenderer.Core.Entities.HtmlImageLoadEventArgs> args)
        {
            if (args.Data is TheArtOfDev.HtmlRenderer.Core.Entities.HtmlImageLoadEventArgs)
            {
                var img = args.Data as TheArtOfDev.HtmlRenderer.Core.Entities.HtmlImageLoadEventArgs;

                if (string.IsNullOrEmpty(img.Src)) return;

                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                var src = await img.Src.LoadImagePath(tokens);
                img.Callback(src);
                img.Handled = true;
                args.Handled = true;
            }
        }

        private void Preview_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                if (SubIllusts.Items.Count() <= 0)
                {
                    if (DataObject is ImageItem)
                    {
                        var item = DataObject as ImageItem;
                        item.ItemType = ImageItemType.Pages;
                        CommonHelper.Cmd_OpenIllust.Execute(item);
                    }
                }
                else
                {
                    if (SubIllusts.SelectedItems == null || SubIllusts.SelectedItems.Count <= 0)
                        SubIllusts.SelectedIndex = 0;
                    CommonHelper.Cmd_OpenIllust.Execute(SubIllusts);
                }
                e.Handled = true;
            }
        }

        #region Illust Actions
        private void ActionIllustInfo_Click(object sender, RoutedEventArgs e)
        {
            if (sender == ActionCopyIllustTitle)
            {
                Clipboard.SetText(IllustTitle.Text);
            }
            else if (sender == ActionCopyIllustAuthor)
            {
                Clipboard.SetText(IllustAuthor.Text);
            }
            else if (sender == ActionCopyAuthorID)
            {
                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    Clipboard.SetText(item.UserID);
                }
            }
            else if (sender == ActionCopyIllustID || sender == PreviewCopyIllustID)
            {
                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    Clipboard.SetText(item.ID);
                }
            }
            else if (sender == ActionCopyIllustDate)
            {
                Clipboard.SetText(ActionCopyIllustDate.Header.ToString());
            }
            else if (sender == ActionIllustWebPage)
            {
                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    if (item.Illust is Pixeez.Objects.Work)
                    {
                        var href = $"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={item.ID}";
                        Clipboard.SetText(href);
                        try
                        {
                            System.Diagnostics.Process.Start(href);
                        }
                        catch (Exception ex)
                        {
                            ex.Message.ShowMessageBox("ERROR");
                        }
                    }
                }
            }
        }

        private async void ActionIllustAuthourInfo_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if(sender == ActionIllustAuthorInfo || sender == btnAuthorInto)
            {
                if (DataObject is ImageItem)
                {
                    var viewer = new ContentWindow();
                    var page = new IllustDetailPage();

                    var item = DataObject as ImageItem;
                    var user = item.Illust.User;
                    page.UpdateDetail(user);
                    viewer.Title = $"User: {user.Name} / {user.Id} / {user.Account}";

                    viewer.Width = 720;
                    viewer.Height = 800;
                    viewer.Content = page;
                    viewer.Show();
                }
            }
            else if(sender == ActionIllustAuthorFollowing)
            {
                if (DataObject is Pixeez.Objects.UserBase)
                {
                }
            }
            else if (sender == ActionIllustAuthorFollowed)
            {

            }
            else if (sender == ActionIllustAuthorFavorite)
            {

            }

        }

        private void ActionShowIllustPages_Click(object sender, RoutedEventArgs e)
        {
            SubIllustsExpander.IsExpanded = !SubIllustsExpander.IsExpanded;
        }

        private void ActionShowRelative_Click(object sender, RoutedEventArgs e)
        {
            if (!RelativeIllustsExpander.IsExpanded) RelativeIllustsExpander.IsExpanded = true;
        }

        private void ActionShowFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (!FavoriteIllustsExpander.IsExpanded) FavoriteIllustsExpander.IsExpanded = true;
        }
        #endregion

        #region Following User / Bookmark Illust routines
        private void ActionIllust_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender == BookmarkIllust)
                BookmarkIllust.ContextMenu.IsOpen = true;
            else if (sender == FollowAuthor)
                FollowAuthor.ContextMenu.IsOpen = true;
            else if (sender == IllustActions)
                IllustActions.ContextMenu.IsOpen = true;
        }

        private async void ActionBookmarkIllust_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (DataObject is ImageItem)
            {
                var item = DataObject as ImageItem;
                var illust = item.Illust;
                var lastID = illust.Id;
                try
                {
                    if (sender == ActionBookmarkIllustPublic)
                    {
                        await tokens.AddMyFavoriteWorksAsync((long)illust.Id, illust.Tags);
                    }
                    else if (sender == ActionBookmarkIllustPrivate)
                    {
                        await tokens.AddMyFavoriteWorksAsync((long)illust.Id, illust.Tags, "private");
                    }
                    else if (sender == ActionBookmarkIllustRemove)
                    {
                        await tokens.DeleteMyFavoriteWorksAsync((long)illust.Id);
                        await tokens.DeleteMyFavoriteWorksAsync((long)illust.Id, "private");
                    }
                    //await item.RefreshIllust(tokens);
                }
                catch (Exception) { }
                finally
                {
                    try
                    {
                        var currentItem = DataObject as ImageItem;
                        var currentIllust = currentItem.Illust;
                        if (lastID == currentIllust.Id)
                        {
                            tokens = await CommonHelper.ShowLogin();
                            await item.RefreshIllustAsync(tokens);
                            currentItem = DataObject as ImageItem;
                            currentIllust = currentItem.Illust;
                            if (lastID == currentIllust.Id)
                            {
                                if (item.Illust.IsBookMarked())
                                {
                                    BookmarkIllust.Tag = PackIconModernKind.Heart;
                                    ActionBookmarkIllustRemove.IsEnabled = true;
                                }
                                else
                                {
                                    BookmarkIllust.Tag = PackIconModernKind.HeartOutline;
                                    ActionBookmarkIllustRemove.IsEnabled = false;
                                }
                            }
                        }
                    }
                    catch (Exception) { }
                }
            }
        }

        private async void ActionFollowAuthor_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (DataObject is ImageItem)
            {
                var item = DataObject as ImageItem;
                var illust = item.Illust;
                var lastID = illust.Id;
                try
                {
                    if (sender == ActionFollowAuthorPublic)
                    {
                        await tokens.AddFavouriteUser((long)illust.User.Id);
                    }
                    else if (sender == ActionFollowAuthorPrivate)
                    {
                        await tokens.AddFavouriteUser((long)illust.User.Id, "private");
                    }
                    else if (sender == ActionFollowAuthorRemove)
                    {
                        await tokens.DeleteFavouriteUser(illust.User.Id.ToString());
                        await tokens.DeleteFavouriteUser(illust.User.Id.ToString(), "private");
                    }
                    //await item.RefreshUserInfo(tokens);
                }
                catch (Exception) { }
                finally
                {
                    try
                    {
                        var currentItem = DataObject as ImageItem;
                        var currentIllust = currentItem.Illust;
                        if (lastID == currentIllust.Id)
                        {
                            tokens = await CommonHelper.ShowLogin();
                            await item.RefreshUserInfoAsync(tokens);
                            currentItem = DataObject as ImageItem;
                            currentIllust = currentItem.Illust;
                            if (lastID == currentIllust.Id)
                            {
                                if (item.Illust.User != null && item.Illust.User.is_followed != null && item.Illust.User.is_followed.Value)
                                {
                                    FollowAuthor.Tag = PackIconModernKind.Check;
                                    ActionFollowAuthorRemove.IsEnabled = true;
                                }
                                else
                                {
                                    FollowAuthor.Tag = PackIconModernKind.Add;
                                    ActionFollowAuthorRemove.IsEnabled = false;
                                }
                            }
                        }
                    }
                    catch (Exception) { }
                }
            }
            else if (DataObject is Pixeez.Objects.UserBase)
            {
                var user = DataObject as Pixeez.Objects.UserBase;
                try
                {
                    if (sender == ActionFollowAuthorPublic)
                        await tokens.AddFavouriteUser((long)user.Id);
                    else if (sender == ActionFollowAuthorPrivate)
                        await tokens.AddFavouriteUser((long)user.Id, "private");
                    else if (sender == ActionFollowAuthorRemove)
                    {
                        await tokens.DeleteFavouriteUser(user.Id.ToString());
                        await tokens.DeleteFavouriteUser(user.Id.ToString(), "private");
                    }
                }
                catch (Exception) { }
                finally
                {
                    try
                    {
                        //Thread.Sleep(250);
                        tokens = await CommonHelper.ShowLogin();
                        var users = await tokens.GetUsersAsync(user.Id.Value);
                        foreach (var u in users)
                        {                            
                            user.is_followed = u.IsFollowing;
                            DataObject = u;
                            break;
                        }
                        if (user.is_followed.Value)
                        {
                            FollowAuthor.Tag = PackIconModernKind.Check;
                            ActionFollowAuthorRemove.IsEnabled = true;
                        }
                        else
                        {
                            FollowAuthor.Tag = PackIconModernKind.Add;
                            ActionFollowAuthorRemove.IsEnabled = false;
                        }
                    }
                    catch (Exception) { }
                }
            }
        }
        #endregion

        #region Illust Multi-Pages related routines
        private async void SubIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (SubIllusts.Items.Count() <= 1)
            {
                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    ShowIllustPages(tokens, item);
                }
            }
        }

        private void ActionOpenIllust_Click(object sender, RoutedEventArgs e)
        {
            if (SubIllusts.Items.Count() <= 0)
            {
                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    item.ItemType = ImageItemType.Pages;
                    CommonHelper.Cmd_OpenIllust.Execute(item);
                }
            }
            else
            {
                if (SubIllusts.SelectedItems == null || SubIllusts.SelectedItems.Count <= 0)
                    SubIllusts.SelectedIndex = 0;
                CommonHelper.Cmd_OpenIllust.Execute(SubIllusts);
            }
        }

        private async void ActionSaveIllust_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (sender == PreviewSave)
            {
                var item = DataObject as ImageItem;
                CommonHelper.Cmd_SaveIllust.Execute(item);
            }
            else if (SubIllusts.SelectedItems != null && SubIllusts.SelectedItems.Count > 0)
            {
                foreach (var item in SubIllusts.SelectedItems)
                {
                    CommonHelper.Cmd_SaveIllust.Execute(item);
                }
            }
            else if (SubIllusts.SelectedItem is ImageItem)
            {
                var item = SubIllusts.SelectedItem;
                CommonHelper.Cmd_SaveIllust.Execute(item);
            }
            else if (DataObject is ImageItem)
            {
                var item = DataObject as ImageItem;
                CommonHelper.Cmd_SaveIllust.Execute(item);
            }

        }

        private async void ActionSaveAllIllust_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            IllustsSaveProgress.Visibility = Visibility.Visible;
            IllustsSaveProgress.Value = 0;
            IProgress<int> progress = new Progress<int>(i => { IllustsSaveProgress.Value = i; });

            Pixeez.Objects.Work illust = null;
            if (DataObject is Pixeez.Objects.Work)
                illust = DataObject as Pixeez.Objects.Work;
            else if (DataObject is ImageItem)
                illust = (DataObject as ImageItem).Illust;

            if (illust != null)
            {
                var dt = illust.GetDateTime();

                if (illust is Pixeez.Objects.IllustWork)
                {
                    var illustset = illust as Pixeez.Objects.IllustWork;
                    var is_meta_single_page = illust.PageCount==1 ? true : false;
                    var idx=1;
                    var total = illustset.meta_pages.Count();
                    foreach (var pages in illustset.meta_pages)
                    {
                        var url = pages.GetOriginalUrl();
                        tokens = await CommonHelper.ShowLogin();
                        //await url.ToImageFile(tokens, dt, is_meta_single_page);
                        url.ToImageFile(pages.GetThumbnailUrl(), dt, is_meta_single_page);

                        idx++;
                        progress.Report((int)((double)idx / total * 100));
                    }
                }
                else if (illust is Pixeez.Objects.NormalWork)
                {
                    var url = illust.GetOriginalUrl();
                    var illustset = illust as Pixeez.Objects.NormalWork;
                    var is_meta_single_page = illust.PageCount==1 ? true : false;
                    tokens = await CommonHelper.ShowLogin();
                    //await url.ToImageFile(tokens, dt, is_meta_single_page);
                    if (is_meta_single_page)
                    {
                        url.ToImageFile(illust.GetThumbnailUrl(), dt, is_meta_single_page);
                    }
                    else
                    {
                        tokens = await CommonHelper.ShowLogin();
                        var illusts = await tokens.GetWorksAsync(illust.Id.Value);
                        foreach(var w in illusts)
                        {
                            if(w.Metadata != null && w.Metadata.Pages != null)
                            {
                                foreach (var p in w.Metadata.Pages)
                                {
                                    var u = p.GetOriginalUrl();
                                    u.ToImageFile(p.GetThumbnailUrl(), dt, is_meta_single_page);
                                }
                            }
                            
                        }
                    }
                    
                }
                IllustsSaveProgress.Value = 100;
                IllustsSaveProgress.Visibility = Visibility.Collapsed;
                SystemSounds.Beep.Play();
            }
        }

        private void SubIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void SubIllusts_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void SubIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            CommonHelper.Cmd_OpenIllust.Execute(SubIllusts);
        }

        private void SubIllusts_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommonHelper.Cmd_OpenIllust.Execute(SubIllusts);
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

                    if (DataObject is ImageItem)
                    {
                        var item = DataObject as ImageItem;
                        ShowIllustPages(tokens, item, start);
                    }
                }
            }
        }
        #endregion

        #region Relative Panel related routines
        private async void RelativeIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (DataObject is ImageItem)
            {
                var item = DataObject as ImageItem;
                ShowRelativeInline(tokens, item);
            }
            else if(DataObject is Pixeez.Objects.UserBase)
            {
                var user = DataObject as Pixeez.Objects.UserBase;
                ShowUserWorksInline(tokens, user);
            }
            RelativeNextPage.Visibility = Visibility.Visible;
        }

        private void ActionOpenRelative_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_OpenIllust.Execute(RelativeIllusts);
        }

        private void ActionCopyRelativeIllustID_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_CopyIllustIDs.Execute(RelativeIllusts);
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

            if (DataObject is ImageItem)
            {
                var item = DataObject as ImageItem;
                var next_url = string.Empty;
                if (RelativeIllustsExpander.Tag is string)
                    next_url = RelativeIllustsExpander.Tag as string;
                ShowRelativeInline(tokens, item, next_url);
            }
            else if (DataObject is Pixeez.Objects.UserBase)
            {
                var user = DataObject as Pixeez.Objects.UserBase;
                var next_url = string.Empty;
                if (RelativeIllustsExpander.Tag is string)
                    next_url = RelativeIllustsExpander.Tag as string;
                ShowUserWorksInline(tokens, user, next_url);
            }
            RelativeNextPage.Visibility = Visibility.Visible;
        }
        #endregion

        #region Autoor Favorite routines
        private async void FavoriteIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (DataObject is ImageItem)
            {
                var item = DataObject as ImageItem;
                var user = item.Illust.User;
                ShowFavoriteInline(tokens, user);
            }
            else if (DataObject is Pixeez.Objects.UserBase)
            {
                var user = DataObject as Pixeez.Objects.UserBase;
                ShowFavoriteInline(tokens, user);
            }
            FavoriteNextPage.Visibility = Visibility.Visible;
        }

        private void ActionOpenFavorite_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_OpenIllust.Execute(FavoriteIllusts);
        }

        private void ActionCopyFavoriteIllustID_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_CopyIllustIDs.Execute(FavoriteIllusts);
        }

        private void FavriteIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void FavriteIllusts_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void FavriteIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            CommonHelper.Cmd_OpenIllust.Execute(FavoriteIllusts);
        }

        private void FavriteIllusts_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommonHelper.Cmd_OpenIllust.Execute(FavoriteIllusts);
            }
        }

        private void FavoritePrevPage_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void FavoriteNextPage_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (DataObject is ImageItem)
            {
                var item = DataObject as ImageItem;
                var user = item.Illust.User;
                var next_url = string.Empty;
                if (FavoriteIllustsExpander.Tag is string)
                    next_url = FavoriteIllustsExpander.Tag as string;
                ShowFavoriteInline(tokens, user, next_url);
            }
            else if (DataObject is Pixeez.Objects.UserBase)
            {
                var user = DataObject as Pixeez.Objects.UserBase;
                var next_url = string.Empty;
                if (FavoriteIllustsExpander.Tag is string)
                    next_url = FavoriteIllustsExpander.Tag as string;
                ShowFavoriteInline(tokens, user, next_url);
            }
            FavoriteNextPage.Visibility = Visibility.Visible;
        }


        #endregion

    }

}
