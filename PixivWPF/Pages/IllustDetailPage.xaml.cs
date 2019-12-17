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

using PixivWPF.Common;
using MahApps.Metro.IconPacks;
using TheArtOfDev.HtmlRenderer.Core.Entities;
using TheArtOfDev.HtmlRenderer.WPF;
using Prism.Commands;
using System.IO;

namespace PixivWPF.Pages
{
    /// <summary>
    /// IllustDetailPage.xaml 的交互逻辑
    /// </summary>
    public partial class IllustDetailPage : Page
    {
        private object DataObject = null;

        internal void UpdateTheme()
        {
            //var fonts = string.Join(",", IllustTitle.FontFamily.FamilyNames.Values);

            var style = new StringBuilder();
            //style.AppendLine($"*{{font-family:FontAwesome, \"Segoe UI Emoji\", \"Segoe MDL2 Assets\", Monaco, Consolas, \"Courier New\", \"Segoe UI\", monospace, 思源黑体, 思源宋体, 微软雅黑, 宋体, 黑体, 楷体, \"WenQuanYi Microhei\", \"WenQuanYi Microhei Mono\", \"Microsoft YaHei\", Tahoma, Arial, Helvetica, sans-serif;}}");
            style.AppendLine($"*{{font-family:FontAwesome, \"Segoe UI Emoji\", \"Segoe MDL2 Assets\", \"Segoe UI\", 思源黑体, 思源宋体, 微软雅黑, 宋体, 黑体, 楷体, Consolas, \"Courier New\", Tahoma, Arial, Helvetica, sans-serif |important;}}");
            style.AppendLine($"body{{font-family:FontAwesome, \"Segoe UI Emoji\", \"Segoe MDL2 Assets\", \"Segoe UI\", 思源黑体, 思源宋体, 微软雅黑, 宋体, 黑体, 楷体, Consolas, \"Courier New\", Tahoma, Arial, Helvetica, sans-serif |important;}}");
            style.AppendLine($"a:link{{color:{Theme.AccentColor.ToHtml(false)}|important;text-decoration:none !important;}}");
            style.AppendLine($"a:hover{{color:{Theme.AccentColor.ToHtml(false)}|important;text-decoration:none !important;}}");
            style.AppendLine($"a:active{{color:{Theme.AccentColor.ToHtml(false)}|important;text-decoration:none !important;}}");
            style.AppendLine($"a:visited{{color:{Theme.AccentColor.ToHtml(false)}|important;text-decoration:none !important;}}");
            //style.AppendLine($"a{{color:{Theme.TextColor.ToHtml(false)}|important;text-decoration:none !important;}}");
            style.AppendLine($"img{{width:auto!important;;height:auto!important;;max-width:100%!important;;max-height:100% !important;}}");
            style.AppendLine($".tag{{background-color:{Theme.AccentColor.ToHtml(false)}|important;color:{Theme.TextColor.ToHtml(false)}|important;margin:4px;text-decoration:none;}}");
            style.AppendLine($".desc{{color:{Theme.TextColor.ToHtml(false)} !important;text-decoration:none !important;}}");

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

        private void UpdateFavMark(Pixeez.Objects.Work illust)
        {
            if (illust.IsLiked())
            {
                BookmarkIllust.Tag = PackIconModernKind.Heart;// "Heart";
                ActionBookmarkIllustRemove.IsEnabled = true;
            }
            else
            {
                BookmarkIllust.Tag = PackIconModernKind.HeartOutline;// "HeartOutline";
                ActionBookmarkIllustRemove.IsEnabled = false;
            }
        }

        private void UpdateFollowMark(Pixeez.Objects.UserBase user)
        {
            if (user.IsLiked())
            {
                FollowAuthor.Tag = PackIconModernKind.Check;// "Check";
                ActionFollowAuthorRemove.IsEnabled = true;
            }
            else
            {
                FollowAuthor.Tag = PackIconModernKind.Add;// "Add";
                ActionFollowAuthorRemove.IsEnabled = false;
            }
        }

        private void UpdateDownloadedMark()
        {
            if (DataObject is ImageItem)
            {
                var item = DataObject as ImageItem;
                UpdateDownloadedMark(item);
            }
        }

        private void UpdateDownloadedMark(ImageItem item)
        {
            if (item is ImageItem)
            {
                string fp = string.Empty;

                var index = item.Index;
                if (index < 0)
                {
                    item.IsDownloaded = item.Illust.IsPartDownloaded(out fp);
                    if (item.IsDownloaded)
                    {
                        IllustDownloaded.Visibility = Visibility.Visible;
                        IllustDownloaded.Tag = fp;
                        ToolTipService.SetToolTip(IllustDownloaded, fp);
                    }
                    else
                    {
                        IllustDownloaded.Visibility = Visibility.Collapsed;
                        IllustDownloaded.Tag = null;
                        ToolTipService.SetToolTip(IllustDownloaded, null);
                    }
                }
                else
                {
                    if (item.Illust.GetOriginalUrl(item.Index).IsDownloaded(out fp))
                    {
                        IllustDownloaded.Visibility = Visibility.Visible;
                        IllustDownloaded.Tag = fp;
                        ToolTipService.SetToolTip(IllustDownloaded, fp);
                    }
                    else
                    {
                        IllustDownloaded.Visibility = Visibility.Collapsed;
                        IllustDownloaded.Tag = null;
                        ToolTipService.SetToolTip(IllustDownloaded, null);
                    }
                }
            }
        }

        private void UpdateMark(bool all = false)
        {
            if (DataObject is ImageItem)
            {
                UpdateFollowMark((DataObject as ImageItem).Illust.User);
                UpdateFavMark((DataObject as ImageItem).Illust);
            }
            else if (DataObject is Pixeez.Objects.UserBase)
            {
                UpdateFollowMark(DataObject as Pixeez.Objects.UserBase);
            }
            if (all && RelativeIllustsExpander.IsExpanded)
            {
                foreach (ImageItem item in RelativeIllusts.Items)
                {
                    item.IsFavorited = item.IsLiked();
                }
            }
            if (all && FavoriteIllustsExpander.IsExpanded)
            {
                foreach (ImageItem item in FavoriteIllusts.Items)
                {
                    item.IsFavorited = item.IsLiked();
                }
            }
        }

        private void OpenDownloaded()
        {
            if (DataObject is ImageItem)
            {
                //string fp = string.Empty;
                //(DataObject as ImageItem).IsDownloaded = (DataObject as ImageItem).Illust.IsPartDownloaded(out fp);
                if (IllustDownloaded.Tag is string)
                {
                    var fp = IllustDownloaded.Tag as string;
                    if (!string.IsNullOrEmpty(fp) && File.Exists(fp))
                    {
                        System.Diagnostics.Process.Start(fp);
                    }
                }
            }
        }

        internal async void UpdateDetail(ImageItem item)
        {
            try
            {
                if (item.ItemType == ImageItemType.Work || item.ItemType == ImageItemType.Manga)
                {
                    await new Action(() =>
                    {
                        UpdateDetailIllust(item);
                    }).InvokeAsync();
                }
                else if (item.ItemType == ImageItemType.User)
                {
                    await new Action(() =>
                    {
                        UpdateDetailUser(item.User);
                    }).InvokeAsync();
                }
                IllustDetailViewer.ScrollToTop();
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
                IllustDetailWait.Hide();
            }
            finally
            {
            }
        }

        internal async void UpdateDetail(Pixeez.Objects.UserBase user)
        {
            try
            {
                await new Action(() =>
                {
                    UpdateDetailUser(user);
                }).InvokeAsync();
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
                IllustDetailWait.Hide();
            }
            finally
            {
            }
        }

        private void UpdateDetailIllust(ImageItem item)
        {
            try
            {
                IllustDetailWait.Show();

                DataObject = item;

                PreviewViewer.Show(true);
                PreviewBox.Show();
                PreviewBox.ToolTip = item.ToolTip;

                var dpi = new DPI();
                Preview.Source = new WriteableBitmap(300, 300, dpi.X, dpi.Y, PixelFormats.Bgra32, BitmapPalettes.WebPalette);
                PreviewWait.Show();

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

                if (item.IsDownloaded)
                {
                    IllustDownloaded.Show();
                    string fp = string.Empty;
                    //item.Illust.GetOriginalUrl(item.Index).IsDownloaded(out fp, item.Illust.PageCount <= 1);
                    item.Illust.IsPartDownloaded(out fp);
                    IllustDownloaded.Tag = fp;
                    ToolTipService.SetToolTip(IllustDownloaded, fp);
                }
                else
                {
                    IllustDownloaded.Hide();
                    IllustDownloaded.Tag = null;
                    ToolTipService.SetToolTip(IllustDownloaded, null);
                }

                IllustSize.Text = $"{item.Illust.Width}x{item.Illust.Height}";
                IllustViewed.Text = stat_viewed;
                IllustFavorited.Text = stat_favorited;

                IllustStatInfo.Show();
                IllustStatInfo.ToolTip = string.Join("\r", stat_tip).Trim();

                IllustAuthor.Text = item.Illust.User.Name;
                IllustAuthorAvator.Source = new WriteableBitmap(64, 64, dpi.X, dpi.Y, PixelFormats.Bgra32, BitmapPalettes.WebPalette);
                IllustAuthorAvatorWait.Show();

                IllustTitle.Text = $"{item.Illust.Title}";

                IllustDate.Text = item.Illust.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss");
                IllustDateInfo.ToolTip = IllustDate.Text;
                IllustDateInfo.Show();

                ActionCopyIllustDate.Header = item.Illust.GetDateTime().ToString("yyyy-MM-dd HH:mm:sszzz");

                FollowAuthor.Show();
                UpdateFollowMark(item.Illust.User);

                BookmarkIllust.Show();
                UpdateFavMark(item.Illust);

                IllustActions.Show();

                if (item.Illust.Tags.Count > 0)
                {
                    var html = new StringBuilder();
                    foreach (var tag in item.Illust.Tags)
                    {
                        //html.AppendLine($"<a href=\"https://www.pixiv.net/search.php?s_mode=s_tag_full&word={Uri.EscapeDataString(tag)}\" class=\"tag\" data-tag=\"{tag}\">{tag}</a>");
                        html.AppendLine($"<a href=\"https://www.pixiv.net/search.php?s_mode=s_tag&word={Uri.EscapeDataString(tag)}\" class=\"tag\" data-tag=\"{tag}\">#{tag}</a>");
                        //html.AppendLine($"<button class=\"tag\" data-tag=\"{tag}\">{tag}</button>");
                    }
                    html.AppendLine("<br/>");
                    IllustTags.Foreground = Theme.TextBrush;
                    IllustTags.Text = string.Join(";", html);
                    IllustTags.ClearSelection();

                    IllustTagExpander.Header = "Tags";
                    IllustTagExpander.IsExpanded = true;
                    IllustTagExpander.Show();
                }
                else
                {
                    IllustTagExpander.Hide();
                }

                if (!string.IsNullOrEmpty(item.Illust.Caption) && item.Illust.Caption.Length > 0)
                {
                    IllustDesc.Text = $"<div class=\"desc\">{item.Illust.Caption.HtmlDecode()}</div>".Replace("\r\n", "<br/>");
                    IllustDesc.ClearSelection();

                    IllustDescExpander.IsExpanded = true;
                    IllustDescExpander.Show();
                }
                else
                {
                    IllustDescExpander.Hide();
                }

                SubIllusts.Items.Clear();
                SubIllustsExpander.IsExpanded = false;
                PreviewBadge.Badge = item.Illust.PageCount;
                if (item.Illust is Pixeez.Objects.IllustWork && item.Illust.PageCount > 1)
                {
                    PreviewBadge.Show();
                    SubIllustsExpander.Show();
                    SubIllustsNavPanel.Show();
                    SubIllustsExpander.IsExpanded = true;
                }
                else if (item.Illust is Pixeez.Objects.NormalWork && item.Illust.PageCount > 1)
                {
                    PreviewBadge.Show();
                    SubIllustsExpander.Show();
                    SubIllustsNavPanel.Show();
                    SubIllustsExpander.IsExpanded = true;
                }
                else
                {
                    PreviewBadge.Hide();
                    SubIllustsExpander.Hide();
                    SubIllustsNavPanel.Hide();
                }

                RelativeIllustsExpander.Header = "Related Illusts";
                RelativeIllustsExpander.IsExpanded = false;
                RelativeIllustsExpander.Show();
                RelativeNextPage.Hide();

                FavoriteIllustsExpander.Header = "Author Favorite";
                FavoriteIllustsExpander.IsExpanded = false;
                FavoriteIllustsExpander.Show();
                FavoriteNextPage.Hide();

                CommentsExpander.IsExpanded = false;
                CommentsExpander.Show();
                CommentsNavigator.Hide();

                ActionRefreshAvator(item);
                ActionRefreshPreview_Click(this, new RoutedEventArgs());
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                PreviewWait.Hide();
                IllustDetailWait.Hide();
            }
        }

        private async void UpdateDetailUser(Pixeez.Objects.UserBase user)
        {
            try
            {
                IllustDetailWait.Show();

                var tokens = await CommonHelper.ShowLogin();

                var UserInfo = await tokens.GetUserInfoAsync(user.Id.Value.ToString());
                var nuser = UserInfo.user;
                var nprof = UserInfo.profile;
                var nworks = UserInfo.workspace;

                DataObject = user;

                PreviewViewer.Hide(true);
                PreviewViewer.Height = 0;
                PreviewBox.Hide();
                PreviewBox.Height = 0;
                Preview.Source = null;
                //if (nprof.background_image_url is string)
                //    Preview.Source = await ((string)nprof.background_image_url).LoadImageFromURL();
                //else
                //    Preview.Source = await nuser.GetPreviewUrl().LoadImageFromURL();

                IllustSizeIcon.Kind = PackIconModernKind.Image;
                IllustSize.Text = $"{nprof.total_illusts + nprof.total_manga}";
                IllustViewedIcon.Kind = PackIconModernKind.Check;
                IllustViewed.Text = $"{nprof.total_follow_users}";
                IllustFavorited.Text = $"{nprof.total_illust_bookmarks_public}";

                IllustStatInfo.Show();

                IllustTitle.Text = string.Empty;
                IllustAuthor.Text = nuser.Name;
                IllustAuthorAvator.Source = await nuser.GetAvatarUrl().LoadImageFromUrl();
                if (IllustAuthorAvator.Source != null)
                {
                    IllustAuthorAvatorWait.Hide();
                }

                FollowAuthor.Show();
                UpdateFollowMark(nuser);

                BookmarkIllust.Hide();
                IllustActions.Hide();

                if (nuser != null && nprof != null && nworks != null)
                {
                    StringBuilder desc = new StringBuilder();
                    desc.AppendLine($"Account:<br/> {nuser.Account} / {nuser.Id} / {nuser.Name} / {nuser.Email}");
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

                    IllustTags.Foreground = Theme.TextBrush;
                    IllustTags.Text = string.Join(";", desc);
                    IllustTagExpander.Header = "User Infomation";
                    IllustTagExpander.IsExpanded = false;
                    IllustTagExpander.Show();
                }
                else
                {
                    IllustTagExpander.Hide();
                }

                CommentsExpander.Hide();
                CommentsNavigator.Hide();

                if (!string.IsNullOrEmpty(nuser.comment) && nuser.comment.Length > 0)
                {
                    var comment = nuser.comment;//.HtmlEncode();
                    IllustDesc.Text = $"<div class=\"desc\">{comment.HtmlDecode()}</div>".Replace("\r\n", "<br/>").Replace("\r", "<br/>").Replace("\n", "<br/>");
                    IllustDescExpander.Show();
                }
                else
                {
                    IllustDescExpander.Hide();
                }

                SubIllusts.Items.Clear();
                //SubIllusts.Refresh();
                SubIllustsExpander.IsExpanded = false;
                SubIllustsExpander.Hide();
                SubIllustsNavPanel.Hide();
                PreviewBadge.Hide();

                RelativeIllustsExpander.Header = "Illusts";
                RelativeIllustsExpander.Show();
                RelativeNextPage.Hide();
                RelativeIllustsExpander.IsExpanded = false;

                FavoriteIllustsExpander.Header = "Favorite";
                FavoriteIllustsExpander.Show();
                FavoriteNextPage.Hide();
                FavoriteIllustsExpander.IsExpanded = false;

                IllustDetailWait.Hide();
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                IllustDetailWait.Hide();
            }
        }

        private async void ShowIllustPages(ImageItem item, int start = 0, int count = 30)
        {
            try
            {
                IllustDetailWait.Show();

                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                SubIllusts.Tag = start;
                SubIllusts.Items.Clear();
                if (item.Illust is Pixeez.Objects.IllustWork)
                {
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
                            CommonHelper.DoEvents();

                            if (i - start >= count - 1) break;
                            btnSubIllustNextPages.Tag = i + 2;
                        }
                        CommonHelper.DoEvents();

                        if ((int)btnSubIllustPrevPages.Tag < 0)
                            btnSubIllustPrevPages.Visibility = Visibility.Collapsed;
                        else
                            btnSubIllustPrevPages.Visibility = Visibility.Visible;

                        if ((int)btnSubIllustNextPages.Tag > total - 1)
                            btnSubIllustNextPages.Visibility = Visibility.Collapsed;
                        else
                            btnSubIllustNextPages.Visibility = Visibility.Visible;

                        SubIllusts.UpdateTilesImage();
                    }
                }
                else if (item.Illust is Pixeez.Objects.NormalWork)
                {
                    var subset = item.Illust as Pixeez.Objects.NormalWork;
                    if (subset.Metadata == null)
                    {
                        var illust = (await item.Illust.RefreshIllust()) as Pixeez.Objects.NormalWork;
                        if (illust is Pixeez.Objects.Work)
                        {
                            item.Illust = illust;
                            subset = illust;
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

                        SubIllusts.UpdateTilesImage();
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                IllustDetailWait.Hide();
                CommonHelper.DoEvents();
            }
        }

        private async void ShowRelativeInline(ImageItem item, string next_url = "")
        {
            try
            {
                IllustDetailWait.Show();
                RelativeIllusts.Items.Clear();

                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                var lastUrl = next_url;
                var relatives = string.IsNullOrEmpty(next_url) ? await tokens.GetRelatedWorks(item.Illust.Id.Value) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = relatives.next_url ?? string.Empty;

                if (relatives.illusts is Array)
                {
                    if (next_url.Equals(lastUrl, StringComparison.CurrentCultureIgnoreCase))
                        RelativeNextPage.Visibility = Visibility.Collapsed;
                    else RelativeNextPage.Visibility = Visibility.Visible;

                    RelativeIllustsExpander.Tag = next_url;
                    foreach (var illust in relatives.illusts)
                    {
                        illust.Cache();
                        illust.AddTo(RelativeIllusts.Items, relatives.next_url);
                        CommonHelper.DoEvents();
                    }
                    CommonHelper.DoEvents();
                    RelativeIllusts.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                IllustDetailWait.Hide();
            }
        }

        private async void ShowUserWorksInline(Pixeez.Objects.UserBase user, string next_url = "")
        {
            try
            {
                IllustDetailWait.Show();
                RelativeIllusts.Items.Clear();

                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                var lastUrl = next_url;
                var relatives = string.IsNullOrEmpty(next_url) ? await tokens.GetUserWorksAsync(user.Id.Value) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = relatives.next_url ?? string.Empty;

                if (relatives.illusts is Array)
                {
                    if (next_url.Equals(lastUrl, StringComparison.CurrentCultureIgnoreCase))
                        RelativeNextPage.Visibility = Visibility.Collapsed;
                    else RelativeNextPage.Visibility = Visibility.Visible;

                    RelativeIllustsExpander.Tag = next_url;
                    foreach (var illust in relatives.illusts)
                    {
                        illust.Cache();
                        illust.AddTo(RelativeIllusts.Items, relatives.next_url);
                        CommonHelper.DoEvents();
                    }
                    CommonHelper.DoEvents();
                    RelativeIllusts.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                IllustDetailWait.Hide();
            }
        }

        private string last_restrict = string.Empty;
        private async void ShowFavoriteInline(Pixeez.Objects.UserBase user, string next_url = "")
        {
            try
            {
                IllustDetailWait.Show();
                FavoriteIllusts.Items.Clear();

                Setting setting = Setting.Load();

                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                var lastUrl = next_url;
                var restrict = Keyboard.Modifiers != ModifierKeys.None ? "private" : "public";
                if (!last_restrict.Equals(restrict, StringComparison.CurrentCultureIgnoreCase)) next_url = string.Empty;
                FavoriteIllustsExpander.Header = $"Favorite ({System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(restrict)})";

                var favorites = string.IsNullOrEmpty(next_url) ? await tokens.GetUserFavoriteWorksAsync(user.Id.Value, restrict) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = favorites.next_url ?? string.Empty;
                last_restrict = restrict;

                if (favorites.illusts is Array)
                {
                    if (next_url.Equals(lastUrl, StringComparison.CurrentCultureIgnoreCase))
                        FavoriteNextPage.Visibility = Visibility.Collapsed;
                    else FavoriteNextPage.Visibility = Visibility.Visible;

                    FavoriteIllustsExpander.Tag = next_url;
                    foreach (var illust in favorites.illusts)
                    {
                        illust.Cache();
                        illust.AddTo(FavoriteIllusts.Items, favorites.next_url);
                        CommonHelper.DoEvents();
                    }
                    CommonHelper.DoEvents();
                    FavoriteIllusts.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                IllustDetailWait.Hide();
            }
        }

        public IllustDetailPage()
        {
            InitializeComponent();

            RelativeIllusts.Columns = 5;

            IllustDetailWait.Visibility = Visibility.Collapsed;

            UpdateTheme();
        }

        private async void IllustTags_LinkClicked(object sender, RoutedEvenArgs<HtmlLinkClickedEventArgs> args)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (args.Data is HtmlLinkClickedEventArgs)
            {
                try
                {
                    var link = args.Data as HtmlLinkClickedEventArgs;

                    if (link.Attributes.ContainsKey("data-tag"))
                    {
                        args.Handled = true;
                        link.Handled = true;

                        var tag  = link.Attributes["data-tag"];
                        if (Keyboard.Modifiers == ModifierKeys.Control)
                            CommonHelper.Cmd_Search.Execute($"Tag:{tag}");
                        else
                            CommonHelper.Cmd_Search.Execute($"Fuzzy Tag:{tag}");

                        //if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                        //    CommonHelper.Cmd_Search.Execute($"Tag:{tag}");
                        //else
                        //    CommonHelper.Cmd_Search.Execute($"Fuzzy Tag:{tag}");
                    }
                }
                catch (Exception e)
                {
                    e.Message.ShowMessageBox("ERROR");
                }
            }
        }

        private async void IllustTags_ImageLoad(object sender, RoutedEvenArgs<HtmlImageLoadEventArgs> args)
        {
            if (args.Data is HtmlImageLoadEventArgs)
            {
                try
                {
                    var img = args.Data as HtmlImageLoadEventArgs;

                    if (string.IsNullOrEmpty(img.Src)) return;

                    var src = await img.Src.GetImagePath();
                    if (!string.IsNullOrEmpty(src)) img.Callback(src);
                    img.Handled = true;
                    args.Handled = true;
                }
                catch (Exception e)
                {
                    e.Message.ShowMessageBox("ERROR");
                }
            }
        }

        private void IllustTags_KeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
                {
                    if (IllustTags.SelectedText.Length == 0)
                        Clipboard.SetDataObject(IllustTags.Text.HtmlToText().HtmlDecode());
                    else
                        Clipboard.SetDataObject(IllustTags.SelectedText);
                }
                else if (Keyboard.Modifiers == ModifierKeys.Shift && e.Key == Key.C)
                {
                    if (IllustTags.SelectedHtml.Length == 0)
                        Clipboard.SetDataObject(IllustTags.GetHtml());
                    else
                        Clipboard.SetDataObject(IllustTags.SelectedHtml);
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception ) { }
#endif
        }

        private async void IllustDesc_LinkClicked(object sender, RoutedEvenArgs<HtmlLinkClickedEventArgs> args)
        {
            if (args.Data is HtmlLinkClickedEventArgs)
            {
                try
                {
                    var link = args.Data as HtmlLinkClickedEventArgs;

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
                                var illust = await illust_id.RefreshIllust();
                                if (illust is Pixeez.Objects.Work)
                                {
                                    CommonHelper.Cmd_OpenIllust.Execute(illust);
                                }
                            }
                        }
                        else if (href.StartsWith("pixiv://users/"))
                        {
                            var user_id = Regex.Replace(href, @"pixiv://users/(\d+)", "$1", RegexOptions.IgnoreCase);
                            var user = await user_id.RefreshUser();
                            if (user is Pixeez.Objects.User)
                            {
                                CommonHelper.Cmd_OpenIllust.Execute(user);
                            }
                        }
                        else
                        {
                            args.Handled = false;
                            link.Handled = false;
                        }
                    }
                }
                catch (Exception e)
                {
                    e.Message.ShowMessageBox("ERROR");
                }
            }
        }

        private async void IllustDesc_ImageLoad(object sender, RoutedEvenArgs<HtmlImageLoadEventArgs> args)
        {
            if (args.Data is HtmlImageLoadEventArgs)
            {
                try
                {
                    var img = args.Data as HtmlImageLoadEventArgs;

                    if (string.IsNullOrEmpty(img.Src)) return;

                    var src = await img.Src.GetImagePath();
                    if (!string.IsNullOrEmpty(src)) img.Callback(src);
                    img.Handled = true;
                    args.Handled = true;
                }
                catch (Exception e)
                {
                    e.Message.ShowMessageBox("ERROR");
                }
            }
        }

        private void IllustDesc_KeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
                {
                    if (IllustDesc.SelectedText.Length == 0)
                        Clipboard.SetDataObject(IllustDesc.Text.HtmlToText().HtmlDecode());
                    else
                        Clipboard.SetDataObject(IllustDesc.SelectedText);
                }
                else if (Keyboard.Modifiers == ModifierKeys.Shift && e.Key == Key.C)
                {
                    if (IllustDesc.SelectedHtml.Length == 0)
                        Clipboard.SetDataObject(IllustDesc.GetHtml());
                    else
                        Clipboard.SetDataObject(IllustDesc.SelectedHtml);
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception ) { }
#endif
        }

        private void IllustDownloaded_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (IllustDownloaded.Tag is string)
            {
                var fp = IllustDownloaded.Tag as string;
                if (!string.IsNullOrEmpty(fp) && File.Exists(fp))
                {
                    System.Diagnostics.Process.Start(fp);
                }
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
                        CommonHelper.Cmd_OpenWorkPreview.Execute(item);
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
            UpdateMark(true);

            if (sender == ActionCopyIllustTitle)
            {
                Clipboard.SetDataObject(IllustTitle.Text);
            }
            else if (sender == ActionCopyIllustAuthor)
            {
                Clipboard.SetDataObject(IllustAuthor.Text);
            }
            else if (sender == ActionCopyAuthorID)
            {
                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    Clipboard.SetDataObject(item.UserID);
                }
            }
            else if (sender == ActionCopyIllustID || sender == PreviewCopyIllustID)
            {
                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    Clipboard.SetDataObject(item.ID);
                }
            }
            else if (sender == ActionCopyIllustDate)
            {
                Clipboard.SetDataObject(ActionCopyIllustDate.Header.ToString());
            }
            else if (sender == ActionIllustWebPage)
            {
                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    if (item.Illust is Pixeez.Objects.Work)
                    {
                        //var href = $"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={item.ID}";
                        var href = $"https://www.pixiv.net/artworks/{item.ID}";
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
            else if (sender == ActionIllustNewWindow)
            {
                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    if (item.Illust is Pixeez.Objects.Work)
                    {
                        CommonHelper.Cmd_OpenIllust.Execute(item.Illust);
                    }
                }
            }
            else if (sender == ActionIllustWebLink)
            {
                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    if (item.Illust is Pixeez.Objects.Work)
                    {
                        var href = $"https://www.pixiv.net/artworks/{item.ID}";
                        Clipboard.SetDataObject(href);
                    }
                }
            }
        }

        private void ActionIllustAuthourInfo_Click(object sender, RoutedEventArgs e)
        {
            if (sender == ActionIllustAuthorInfo || sender == btnAuthorInfo)
            {
                if (DataObject is ImageItem)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        ActionRefreshAvator(DataObject as ImageItem);
                    else
                        CommonHelper.Cmd_OpenUser.Execute((DataObject as ImageItem).User);
                }
            }
            else if (sender == ActionIllustAuthorFollowing)
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

        private void ActionOpenIllust_Click(object sender, RoutedEventArgs e)
        {
            if (sender == PreviewOpenDownloaded || (sender is MenuItem && (sender as MenuItem).Uid.Equals("ActionOpenDownloaded", StringComparison.CurrentCultureIgnoreCase)))
            {
                if (SubIllusts.SelectedItems.Count == 0)
                {
                    CommonHelper.Cmd_OpenDownloaded.Execute(DataObject as ImageItem);
                }
                else
                {
                    foreach (ImageItem item in SubIllusts.SelectedItems)
                    {
                        CommonHelper.Cmd_OpenDownloaded.Execute(item);
                    }
                }
            }
            else if (sender == PreviewOpen)
            {
                if (SubIllusts.Items.Count() <= 0)
                {
                    if (DataObject is ImageItem)
                    {
                        var item = DataObject as ImageItem;
                        CommonHelper.Cmd_OpenWorkPreview.Execute(item);
                    }
                }
                else
                {
                    if (SubIllusts.SelectedItems == null || SubIllusts.SelectedItems.Count <= 0)
                        SubIllusts.SelectedIndex = 0;
                    CommonHelper.Cmd_OpenIllust.Execute(SubIllusts);
                }
            }
        }

        private void ActionRefreshPreview_Click(object sender, RoutedEventArgs e)
        {
            if (DataObject is ImageItem)
            {
                var ua = new Action(async () =>
                {
                    try
                    {
                        PreviewWait.Show();

                        var idx = -1;
                        var item = DataObject as ImageItem;
                        if (SubIllusts.SelectedItem is ImageItem)
                        {
                            idx = SubIllusts.SelectedIndex;
                            item = SubIllusts.SelectedItem as ImageItem;
                        }

                        lastSelectionItem = item;
                        lastSelectionChanged = DateTime.Now.ToFileTime();

                        var img = await item.Illust.GetPreviewUrl(item.Index).LoadImageFromUrl();
                        if (img == null || img.Width < 360)
                        {
                            var large = await item.Illust.GetPreviewUrl(item.Index, true).LoadImageFromUrl();
                            if (large != null) img = large;
                        }
                        if (img != null)
                        {
                            if(SubIllusts.SelectedItem is ImageItem)
                            {
                                if(SubIllusts.SelectedItem == item && SubIllusts.SelectedItem.IsSameIllust(img.GetHashCode()))
                                    Preview.Source = img;
                            }
                            else
                            {
                                if ((DataObject as ImageItem).Illust.Id.IsSameIllust(img.GetHashCode()) ||
                                        (DataObject as ImageItem).IsSameIllust(img.GetHashCode()))
                                    Preview.Source = img;
                            }
                        }
                    }
                    catch (Exception) { }
                    finally
                    {
                        if (Preview.Source != null) Preview.Show();
                        PreviewWait.Hide();
                    }
                }).InvokeAsync();
            }
        }
        
        private void ActionRefreshAvator(ImageItem item)
        {
            var ua = new Action(async () =>
             {
                 IllustAuthorAvator.Source = await item.User.GetAvatarUrl().LoadImageFromUrl();
                 if (IllustAuthorAvator.Source != null) IllustAuthorAvatorWait.Hide();
             }).InvokeAsync();
        }
        #endregion

        #region Following User / Bookmark Illust routines
        private void IllustActions_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
            UpdateMark(true);
        }

        private void ActionIllust_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            UpdateMark(true);

            if (sender == BookmarkIllust)
                BookmarkIllust.ContextMenu.IsOpen = true;
            else if (sender == FollowAuthor)
                FollowAuthor.ContextMenu.IsOpen = true;
            else if (sender == IllustActions)
            {
                if (Window.GetWindow(this) is ContentWindow)
                    ActionIllustNewWindow.Visibility = Visibility.Collapsed;
                else
                    ActionIllustNewWindow.Visibility = Visibility.Visible;
                IllustActions.ContextMenu.IsOpen = true;
            }
        }

        private async void ActionBookmarkIllust_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            string uid = (sender as dynamic).Uid;

            if (uid.Equals("ActionLikeIllust", StringComparison.CurrentCultureIgnoreCase) ||
                uid.Equals("ActionLikeIllustPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                uid.Equals("ActionUnLikeIllust", StringComparison.CurrentCultureIgnoreCase))
            {
                IList<ImageItem> items = new List<ImageItem>();
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == RelativeIllusts || host == RelativeIllustsExpander) items = RelativeIllusts.SelectedItems;
                else if (host == FavoriteIllusts || host == FavoriteIllustsExpander) items = FavoriteIllusts.SelectedItems;
                try
                {
                    if (uid.Equals("ActionLikeIllust", StringComparison.CurrentCultureIgnoreCase))
                    {
                        items.LikeIllust();
                    }
                    else if (uid.Equals("ActionLikeIllustPrivate", StringComparison.CurrentCultureIgnoreCase))
                    {
                        items.LikeIllust(false);
                    }
                    else if (uid.Equals("ActionUnLikeIllust", StringComparison.CurrentCultureIgnoreCase))
                    {
                        items.UnLikeIllust();
                    }
                }
                catch (Exception) { }
            }
            else
            {
                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    var result = false;
                    try
                    {
                        if (sender == ActionBookmarkIllustPublic)
                        {
                            result = await item.LikeIllust();
                        }
                        else if (sender == ActionBookmarkIllustPrivate)
                        {
                            result = await item.LikeIllust(false);
                        }
                        else if (sender == ActionBookmarkIllustRemove)
                        {
                            result = await item.UnLikeIllust();
                        }

                        if (item.IsSameIllust(DataObject as ImageItem))
                        {
                            BookmarkIllust.Tag = result ? PackIconModernKind.Heart : PackIconModernKind.HeartOutline;
                            ActionBookmarkIllustRemove.IsEnabled = result;
                            item.IsFavorited = result;
                        }
                    }
                    catch (Exception) { }
                }
            }
        }

        private async void ActionFollowAuthor_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            string uid = (sender as dynamic).Uid;

            if (uid.Equals("ActionLikeUser", StringComparison.CurrentCultureIgnoreCase) ||
                uid.Equals("ActionLikeUserPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                uid.Equals("ActionUnLikeUser", StringComparison.CurrentCultureIgnoreCase))
            {
                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                IList<ImageItem> items = new List<ImageItem>();
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == RelativeIllusts || host == RelativeIllustsExpander) items = RelativeIllusts.SelectedItems;
                else if (host == FavoriteIllusts || host == FavoriteIllustsExpander) items = FavoriteIllusts.SelectedItems;
                try
                {
                    if (uid.Equals("ActionLikeUser", StringComparison.CurrentCultureIgnoreCase))
                    {
                        items.LikeUser();
                    }
                    else if (uid.Equals("ActionLikeUserPrivate", StringComparison.CurrentCultureIgnoreCase))
                    {
                        items.LikeUser(false);
                    }
                    else if (uid.Equals("ActionUnLikeUser", StringComparison.CurrentCultureIgnoreCase))
                    {
                        items.UnLikeUser();
                    }
                }
                catch (Exception) { }
            }
            else
            {
                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    var result = false;
                    try
                    {
                        if (sender == ActionFollowAuthorPublic)
                        {
                            result = await item.LikeUser();
                        }
                        else if (sender == ActionFollowAuthorPrivate)
                        {
                            result = await item.LikeUser(false);
                        }
                        else if (sender == ActionFollowAuthorRemove)
                        {
                            result = await item.UnLikeUser();
                        }

                        if (item.IsSameIllust(DataObject as ImageItem))
                        {
                            FollowAuthor.Tag = result ? PackIconModernKind.Check : PackIconModernKind.Add;
                            ActionFollowAuthorRemove.IsEnabled = result;
                            if (item.ItemType == ImageItemType.User) item.IsFavorited = result;
                        }
                    }
                    catch (Exception) { }
                }
                else if (DataObject is Pixeez.Objects.UserBase)
                {
                    var item = DataObject as Pixeez.Objects.UserBase;
                    var result = false;
                    try
                    {
                        if (sender == ActionFollowAuthorPublic)
                        {
                            result = await item.Like();
                        }
                        else if (sender == ActionFollowAuthorPrivate)
                        {
                            result = await item.Like(false);
                        }
                        else if (sender == ActionFollowAuthorRemove)
                        {
                            result = await item.UnLike();
                        }

                        FollowAuthor.Tag = result ? PackIconModernKind.Check : PackIconModernKind.Add;
                        ActionFollowAuthorRemove.IsEnabled = result;
                    }
                    catch (Exception) { }
                }
            }
        }
        #endregion

        #region Illust Multi-Pages related routines
        private void SubIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (SubIllusts.Items.Count() <= 1)
            {
                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    ShowIllustPages(item);
                }
            }
        }

        private void SubIllustsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            //IllustDetailWait.Hide();
        }

        long lastSelectionChanged = 0;
        ImageItem lastSelectionItem = null;
        private void SubIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
#if DEBUG
            Console.WriteLine($"{DateTime.Now.ToFileTime() - lastSelectionChanged}, {sender}, {e.Handled}, {e.RoutedEvent}, {e.OriginalSource}, {e.Source}");
#endif
            e.Handled = false;

            if (SubIllusts.SelectedItem is ImageItem && SubIllusts.SelectedItems.Count == 1)
            {
                if (DateTime.Now.ToFileTime() - lastSelectionChanged < 500000)
                {
                    SubIllusts.SelectedItem = lastSelectionItem;
                    return;
                }
                SubIllusts.UpdateTilesDaownloadStatus(false);
                UpdateDownloadedMark(SubIllusts.SelectedItem);

                if (DataObject is ImageItem)
                {
                    (DataObject as ImageItem).IsDownloaded = (DataObject as ImageItem).Illust.IsPartDownloaded();
                }
                UpdateMark();

                //IllustDetailViewer
                e.Handled = true;

                ActionRefreshPreview_Click(sender, e);
            }
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

        private void SubIllustPagesNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender == btnSubIllustPrevPages || sender == btnSubIllustNextPages)
            {
                var btn = sender as Button;
                if (btn.Tag is int)
                {
                    var start = (int)btn.Tag;
                    if (start < 0) return;

                    if (DataObject is ImageItem)
                    {
                        var item = DataObject as ImageItem;
                        if (start < item.Count)
                            ShowIllustPages(item, start);
                    }
                }
            }
        }

        private void ActionPrevSubIllustPage_Click(object sender, RoutedEventArgs e)
        {
            var btn = btnSubIllustPrevPages;
            if (btn.Tag is int)
            {
                var start = (int)btn.Tag;
                if (start < 0) return;

                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    if (start < item.Count)
                        ShowIllustPages(item, start);
                }
            }
        }

        private void ActionNextSubIllustPage_Click(object sender, RoutedEventArgs e)
        {
            var btn = btnSubIllustNextPages;
            if (btn.Tag is int)
            {
                var start = (int)btn.Tag;
                if (start < 0) return;

                if (DataObject is ImageItem)
                {
                    var item = DataObject as ImageItem;
                    if (start < item.Count)
                        ShowIllustPages(item, start);
                }
            }
        }

        private void ActionSaveIllust_Click(object sender, RoutedEventArgs e)
        {
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
                        url.SaveImage(pages.GetThumbnailUrl(), dt, is_meta_single_page);

                        idx++;
                        progress.Report((int)((double)idx / total * 100));
                    }
                }
                else if (illust is Pixeez.Objects.NormalWork)
                {
                    var url = illust.GetOriginalUrl();
                    var illustset = illust as Pixeez.Objects.NormalWork;
                    var is_meta_single_page = illust.PageCount==1 ? true : false;
                    if (is_meta_single_page)
                    {
                        url.SaveImage(illust.GetThumbnailUrl(), dt, is_meta_single_page);
                    }
                    else
                    {
                        illust = await illust.RefreshIllust();
                        if (illust is Pixeez.Objects.Work && illust.Metadata != null && illust.Metadata.Pages != null)
                        {
                            foreach (var p in illust.Metadata.Pages)
                            {
                                var u = p.GetOriginalUrl();
                                u.SaveImage(p.GetThumbnailUrl(), dt, is_meta_single_page);
                            }
                        }
                    }
                }
                IllustsSaveProgress.Value = 100;
                IllustsSaveProgress.Visibility = Visibility.Collapsed;
                SystemSounds.Beep.Play();
            }
        }
        #endregion

        #region Relative Panel related routines
        private void RelativeIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (DataObject is ImageItem)
            {
                var item = DataObject as ImageItem;
                ShowRelativeInline(item);
            }
            else if (DataObject is Pixeez.Objects.UserBase)
            {
                var user = DataObject as Pixeez.Objects.UserBase;
                ShowUserWorksInline(user);
            }
        }

        private void RelativeIllustsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            IllustDetailWait.Hide();
        }

        private void ActionOpenRelative_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_OpenIllust.Execute(RelativeIllusts);
        }

        private void ActionCopyRelativeIllustID_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_CopyIllustIDs.Execute(RelativeIllusts);
        }

        private void RelativeIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = false;
            RelativeIllusts.UpdateTilesDaownloadStatus();
            UpdateMark();
            e.Handled = true;
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

        private void RelativeNextPage_Click(object sender, RoutedEventArgs e)
        {
            var next_url = string.Empty;
            if (RelativeIllustsExpander.Tag is string)
            {
                next_url = RelativeIllustsExpander.Tag as string;
                if (string.IsNullOrEmpty(next_url))
                    RelativeNextPage.Visibility = Visibility.Collapsed;
                else
                    RelativeNextPage.Visibility = Visibility.Visible;
            }

            if (DataObject is ImageItem)
            {
                var item = DataObject as ImageItem;
                ShowRelativeInline(item, next_url);
            }
            else if (DataObject is Pixeez.Objects.UserBase)
            {
                var user = DataObject as Pixeez.Objects.UserBase;
                ShowUserWorksInline(user, next_url);
            }
        }
        #endregion

        #region Author Favorite routines
        private void FavoriteIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (DataObject is ImageItem)
            {
                var user = (DataObject as ImageItem).User;
                ShowFavoriteInline(user);
            }
            else if (DataObject is Pixeez.Objects.UserBase)
            {
                var user = DataObject as Pixeez.Objects.UserBase;
                ShowFavoriteInline(user);
            }
        }

        private void FavoriteIllustsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            FavoriteIllustsExpander.Header = "Favorite";
            IllustDetailWait.Hide();
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
            e.Handled = false;
            FavoriteIllusts.UpdateTilesDaownloadStatus();
            UpdateMark();
            e.Handled = true;
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

        private void FavoriteNextPage_Click(object sender, RoutedEventArgs e)
        {
            var next_url = string.Empty;
            if (FavoriteIllustsExpander.Tag is string)
            {
                next_url = FavoriteIllustsExpander.Tag as string;
                if (string.IsNullOrEmpty(next_url))
                    FavoriteNextPage.Visibility = Visibility.Collapsed;
                else
                    FavoriteNextPage.Visibility = Visibility.Visible;
            }

            if (DataObject is ImageItem)
            {
                var item = DataObject as ImageItem;
                var user = item.Illust.User;
                ShowFavoriteInline(user, next_url);
            }
            else if (DataObject is Pixeez.Objects.UserBase)
            {
                var user = DataObject as Pixeez.Objects.UserBase;
                ShowFavoriteInline(user, next_url);
            }
        }
        #endregion

        #region Illust Comments related routines
        private async void CommentsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (DataObject is ImageItem)
            {
                IllustDetailWait.Show();

                var item = DataObject as ImageItem;
                //ShowCommentsInline(tokens, item);
                //CommentsViewer.Language = System.Windows.Markup.XmlLanguage.GetLanguage("zh");
                CommentsViewer.NavigateToString("about:blank");

                //.Document = string.Empty;
                var result = await tokens.GetIllustComments(item.ID, "0", true);
                foreach (var comment in result.comments)
                {
                    //comment.
                }
                //CommentsViewer.NavigateToString(comm

                IllustDetailWait.Hide();
            }
        }

        private void CommentsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            IllustDetailWait.Hide();
        }

        private void CommentsPrevPage_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CommentsNextPage_Click(object sender, RoutedEventArgs e)
        {

        }

        #endregion

        #region Common ImageListGrid Context Menu
        private void ActionMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu)
            {
                var menu = sender as ContextMenu;
                var host = menu.PlacementTarget;
                if (host == SubIllustsExpander || host == SubIllusts)
                {
                    var start = SubIllustsExpander.Tag is int ? (int)(SubIllustsExpander.Tag) : 0;
                    var count = (DataObject as ImageItem).Count;
                    foreach (dynamic item in (sender as ContextMenu).Items)
                    {
                        try
                        {
                            if (item.Uid.Equals("ActionPrevPage", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (start > 0) item.Visibility = Visibility.Visible;
                                else item.Visibility = Visibility.Collapsed;
                            }
                            else if (item.Uid.Equals("ActionNextPage", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (count - start > 30) item.Visibility = Visibility.Visible;
                                else item.Visibility = Visibility.Collapsed;
                            }
                            else if (item.Uid.Equals("ActionNavPageSeparator", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (count <= 30) item.Visibility = Visibility.Collapsed;
                                else item.Visibility = Visibility.Visible;
                            }

                            else if (item.Uid.Equals("ActionLikeIllustSeparator", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionLikeIllust", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionLikeIllustPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionUnLikeIllust", StringComparison.CurrentCultureIgnoreCase))
                                item.Visibility = Visibility.Collapsed;

                            else if (item.Uid.Equals("ActionLikeUserSeparator", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionLikeUser", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionLikeUserPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionUnLikeUser", StringComparison.CurrentCultureIgnoreCase))
                                item.Visibility = Visibility.Collapsed;

                            else if (item.Uid.Equals("ActionSaveIllusts", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save Selected Pages";
                            else if (item.Uid.Equals("ActionSaveIllustsAll", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save All Pages";
                        }
                        catch (Exception) { continue; }
                    }
                }
                else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                {
                    foreach (dynamic item in (sender as ContextMenu).Items)
                    {
                        try
                        {
                            if (item.Uid.Equals("ActionPrevPage", StringComparison.CurrentCultureIgnoreCase))
                            {
                                item.Visibility = Visibility.Collapsed;
                            }
                            else if (item.Uid.Equals("ActionNavPageSeparator", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionNextPage", StringComparison.CurrentCultureIgnoreCase))
                            {
                                var next_url = RelativeIllustsExpander.Tag as string;
                                if (string.IsNullOrEmpty(next_url))
                                    item.Visibility = Visibility.Collapsed;
                                else
                                    item.Visibility = Visibility.Visible;
                            }
                            else if (item.Uid.Equals("ActionLikeIllustSeparator", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionLikeIllust", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionLikeIllustPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionUnLikeIllust", StringComparison.CurrentCultureIgnoreCase))
                                item.Visibility = Visibility.Visible;

                            else if (item.Uid.Equals("ActionLikeUserSeparator", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionLikeUser", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionLikeUserPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionUnLikeUser", StringComparison.CurrentCultureIgnoreCase))
                                item.Visibility = Visibility.Visible;

                            else if (item.Uid.Equals("ActionSaveIllusts", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save Selected Illusts (Default Page)";
                            else if (item.Uid.Equals("ActionSaveIllustsAll", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save Selected Illusts (All Pages)";
                        }
                        catch (Exception) { continue; }
                    }
                }
                else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                {
                    foreach (dynamic item in (sender as ContextMenu).Items)
                    {
                        try
                        {
                            if (item.Uid.Equals("ActionPrevPage", StringComparison.CurrentCultureIgnoreCase))
                            {
                                item.Visibility = Visibility.Collapsed;
                            }
                            else if (item.Uid.Equals("ActionNavPageSeparator", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionNextPage", StringComparison.CurrentCultureIgnoreCase))
                            {
                                var next_url = FavoriteIllustsExpander.Tag as string;
                                if (string.IsNullOrEmpty(next_url))
                                    item.Visibility = Visibility.Collapsed;
                                else
                                    item.Visibility = Visibility.Visible;
                            }
                            else if (item.Uid.Equals("ActionLikeIllustSeparator", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionLikeIllust", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionLikeIllustPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionUnLikeIllust", StringComparison.CurrentCultureIgnoreCase))
                                item.Visibility = Visibility.Visible;

                            else if (item.Uid.Equals("ActionLikeUserSeparator", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionLikeUser", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionLikeUserPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionUnLikeUser", StringComparison.CurrentCultureIgnoreCase))
                                item.Visibility = Visibility.Visible;

                            else if (item.Uid.Equals("ActionSaveIllusts", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save Selected Illusts (Default Page)";
                            else if (item.Uid.Equals("ActionSaveIllustsAll", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save Selected Illusts (All Pages)";
                        }
                        catch (Exception) { continue; }
                    }
                }
                else if (host == CommentsExpander || host == CommentsViewer)
                {
                    foreach (dynamic item in (sender as ContextMenu).Items)
                    {
                        try
                        {
                            if (item.Uid.Equals("ActionPrevPage", StringComparison.CurrentCultureIgnoreCase))
                            {
                                item.Visibility = Visibility.Visible;
                            }
                        }
                        catch (Exception) { continue; }
                    }
                }
            }
        }

        private void ActionCopyIllustID_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
            {
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == SubIllustsExpander || host == SubIllusts)
                {
                    if (DataObject is ImageItem)
                    {
                        var item = DataObject as ImageItem;
                        Clipboard.SetText(item.ID);
                    }
                }
                else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                {
                    CommonHelper.Cmd_CopyIllustIDs.Execute(RelativeIllusts);
                }
                else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                {
                    CommonHelper.Cmd_CopyIllustIDs.Execute(FavoriteIllusts);
                }
                else if (host == CommentsExpander || host == CommentsViewer)
                {

                }
            }
        }

        private void ActionOpenSelectedIllust_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
            {
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == SubIllustsExpander || host == SubIllusts)
                {
                    CommonHelper.Cmd_OpenIllust.Execute(SubIllusts);
                }
                else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                {
                    CommonHelper.Cmd_OpenIllust.Execute(RelativeIllusts);
                }
                else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                {
                    CommonHelper.Cmd_OpenIllust.Execute(FavoriteIllusts);
                }
                else if (host == CommentsExpander || host == CommentsViewer)
                {

                }
            }
        }

        private void ActionPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
            {
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == SubIllustsExpander || host == SubIllusts)
                {
                    ActionPrevSubIllustPage_Click(sender, e);
                }
                else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                {

                }
                else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                {

                }
                else if (host == CommentsExpander || host == CommentsViewer)
                {

                }
            }
        }

        private void ActionNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
            {
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == SubIllustsExpander || host == SubIllusts)
                {
                    ActionNextSubIllustPage_Click(sender, e);
                }
                else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                {
                    RelativeNextPage_Click(RelativeIllusts, e);
                }
                else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                {
                    FavoriteNextPage_Click(FavoriteIllusts, e);
                }
                else if (host == CommentsExpander || host == CommentsViewer)
                {

                }
            }
        }

        private void ActionSaveIllusts_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                var m = sender as MenuItem;
                var host = (m.Parent as ContextMenu).PlacementTarget;
                if (m.Uid.Equals("ActionSaveIllusts", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (host == SubIllustsExpander || host == SubIllusts)
                    {
                        foreach (ImageItem item in SubIllusts.SelectedItems)
                        {
                            CommonHelper.Cmd_SaveIllust.Execute(item);
                        }
                    }
                    else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                    {
                        foreach (ImageItem item in RelativeIllusts.SelectedItems)
                        {
                            CommonHelper.Cmd_SaveIllust.Execute(item);
                        }
                    }
                    else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                    {
                        foreach (ImageItem item in FavoriteIllusts.SelectedItems)
                        {
                            CommonHelper.Cmd_SaveIllust.Execute(item);
                        }
                    }
                }
            }
        }

        private void ActionSaveIllustsAll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                var m = sender as MenuItem;
                var host = (m.Parent as ContextMenu).PlacementTarget;
                if (m.Uid.Equals("ActionSaveIllustsAll", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (host == SubIllustsExpander || host == SubIllusts)
                    {
                        CommonHelper.Cmd_SaveIllustAll.Execute(DataObject as ImageItem);
                    }
                    else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                    {
                        foreach (ImageItem item in RelativeIllusts.SelectedItems)
                        {
                            CommonHelper.Cmd_SaveIllustAll.Execute(item);
                        }
                    }
                    else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                    {
                        foreach (ImageItem item in FavoriteIllusts.SelectedItems)
                        {
                            CommonHelper.Cmd_SaveIllustAll.Execute(item);
                        }
                    }
                }
            }
        }

        private void ActionOpenDownloaded_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                var m = sender as MenuItem;
                var host = (m.Parent as ContextMenu).PlacementTarget;
                if (m.Uid.Equals("ActionOpenDownloaded", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (host == SubIllustsExpander || host == SubIllusts)
                    {
                        foreach (ImageItem item in SubIllusts.SelectedItems)
                        {
                            CommonHelper.Cmd_OpenDownloaded.Execute(item);
                        }
                    }
                    else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                    {
                        foreach (ImageItem item in RelativeIllusts.SelectedItems)
                        {
                            CommonHelper.Cmd_OpenDownloaded.Execute(item);
                        }
                    }
                    else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                    {
                        foreach (ImageItem item in FavoriteIllusts.SelectedItems)
                        {
                            CommonHelper.Cmd_OpenDownloaded.Execute(item);
                        }
                    }
                }
            }
        }

        private void ActionRefreshIllusts_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                var m = sender as MenuItem;
                var host = (m.Parent as ContextMenu).PlacementTarget;
                if (m.Uid.Equals("ActionRefresh", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (host == SubIllustsExpander || host == SubIllusts)
                    {
                        if (DataObject is ImageItem)
                        {
                            var item = DataObject as ImageItem;
                            ShowIllustPages(item, SubIllusts.Tag is int ? (int)(SubIllusts.Tag) : 0);
                        }
                    }
                    else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                    {
                        if (DataObject is ImageItem)
                        {
                            var item = DataObject as ImageItem;
                            ShowRelativeInline(item, RelativeIllustsExpander.Tag is string ? RelativeIllustsExpander.Tag as string : string.Empty);
                        }
                        else if (DataObject is Pixeez.Objects.UserBase)
                        {
                            var user = DataObject as Pixeez.Objects.UserBase;
                            ShowUserWorksInline(user, RelativeIllustsExpander.Tag is string ? RelativeIllustsExpander.Tag as string : string.Empty);
                        }
                    }
                    else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                    {
                        if (DataObject is ImageItem)
                        {
                            var user = (DataObject as ImageItem).User;
                            ShowFavoriteInline(user, FavoriteIllustsExpander.Tag is string ? FavoriteIllustsExpander.Tag as string : string.Empty);
                        }
                        else if (DataObject is Pixeez.Objects.UserBase)
                        {
                            var user = DataObject as Pixeez.Objects.UserBase;
                            ShowFavoriteInline(user, FavoriteIllustsExpander.Tag is string ? FavoriteIllustsExpander.Tag as string : string.Empty);
                        }
                    }
                }
            }
        }
        #endregion

    }

}
