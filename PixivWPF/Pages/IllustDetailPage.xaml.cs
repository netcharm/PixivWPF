using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using PixivWPF.Common;
using MahApps.Metro.IconPacks;

namespace PixivWPF.Pages
{
    /// <summary>
    /// IllustDetailPage.xaml 的交互逻辑
    /// </summary>
    public partial class IllustDetailPage : Page
    {
        private Setting setting = Application.Current.LoadSetting();

        public ImageItem Contents { get; set; } = null;

        //private object DataObject = null;
        private string PreviewImageUrl = string.Empty;

        private bool bCancel = false;
        private WindowsFormsHostEx tagsHost;
        private WindowsFormsHostEx descHost;
        private System.Windows.Forms.WebBrowser IllustDescHtml;
        private System.Windows.Forms.WebBrowser IllustTagsHtml;

        private List<DependencyObject> hitResultsList = new List<DependencyObject>();
        // Return the result of the hit test to the callback.
        private HitTestResultBehavior MyHitTestResult(HitTestResult result)
        {
            var behavior = HitTestResultBehavior.Continue;
            try
            {
                // Add the hit test result to the list that will be processed after the enumeration.
                hitResultsList.Add(result.VisualHit);
            }
            catch (Exception) { }
            return (behavior);
        }

        private bool IsElement(FrameworkElement target, MouseEventArgs e)
        {
            bool result = false;

            try
            {
                FrameworkElement sender = e.Source is FrameworkElement ? (FrameworkElement)e.Source : this;
                var pt = e.GetPosition(sender);
                hitResultsList.Clear();
                // Perform the hit test against a given portion of the visual object tree.
                VisualTreeHelper.HitTest(PreviewBox, null, new HitTestResultCallback(MyHitTestResult), new PointHitTestParameters(pt));
                if (hitResultsList.Count > 1)
                {
                    // Perform action on hit visual object.
                    foreach (var element in hitResultsList)
                    {
                        if (element is FrameworkElement)
                        {
                            FrameworkElement parent = (FrameworkElement)((FrameworkElement)element).TemplatedParent;
                            if (parent == target || element == target)
                            {
                                result = true;
                                break;
                            }
                        }
                    }
                }
            }
            catch { }

            return (result);
        }

        private WindowsFormsHostEx GetHostEx(System.Windows.Forms.WebBrowser browser)
        {
            WindowsFormsHostEx result = null;
            try
            {
                if (browser == IllustDescHtml)
                    result = descHost;
                else if (browser == IllustTagsHtml)
                    result = tagsHost;
            }
            catch (Exception) { }
            return (result);
        }

        private async void AdjustBrowserSize(System.Windows.Forms.WebBrowser browser)
        {
            try
            {
                if (browser is System.Windows.Forms.WebBrowser)
                {
                    int h_min = 96;
                    int h_max = 480;

                    var host = GetHostEx(browser);
                    if (host is System.Windows.Forms.Integration.WindowsFormsHost)
                    {
                        h_min = (int)(host.MinHeight);
                        h_max = (int)(host.MaxHeight);
                    }
                    await Task.Delay(1);
                    var size = browser.Document.Body.ScrollRectangle.Size;
                    var offset = browser.Document.Body.OffsetRectangle.Top;
                    if (offset <= 0) offset = 16;
                    browser.Height = Math.Min(Math.Max(size.Height, h_min), h_max) + offset * 2;
                }
            }
            catch (Exception) { }
        }

        private string GetText(System.Windows.Forms.WebBrowser browser, bool html = false)
        {
            string result = string.Empty;
            try
            {
                if (browser is System.Windows.Forms.WebBrowser && browser.Document is System.Windows.Forms.HtmlDocument)
                {
                    mshtml.IHTMLDocument2 document = browser.Document.DomDocument as mshtml.IHTMLDocument2;
                    mshtml.IHTMLSelectionObject currentSelection = document.selection;
                    if (currentSelection != null && currentSelection.type.Equals("Text"))
                    {
                        mshtml.IHTMLTxtRange range = currentSelection.createRange() as mshtml.IHTMLTxtRange;
                        if (range != null)
                        {
                            if (html)
                                result = range.htmlText;
                            else
                                result = range.text;
                        }
                    }
                    else
                    {
                        var bodies = browser.Document.GetElementsByTagName("body");
                        foreach (System.Windows.Forms.HtmlElement body in bodies)
                        {
                            if (html)
                                result = body.InnerHtml;
                            else
                                result = body.InnerText;
                            break;
                        }
                    }
                }
            }
#if DEBUG
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
#else
            catch (Exception) { }
#endif
            return (result);
        }

        private string MakeIllustTagsHtml(ImageItem item)
        {
            var result = string.Empty;
            try
            {
                if (item.ItemType == ImageItemType.Work && item.Illust is Pixeez.Objects.Work)
                {
                    if (item.Illust.Tags.Count > 0)
                    {
                        var html = new StringBuilder();
                        if (item.Illust is Pixeez.Objects.IllustWork)
                        {
                            foreach (var tag in (item.Illust as Pixeez.Objects.IllustWork).MoreTags)
                            {
                                var trans = string.IsNullOrEmpty(tag.Translated) ? tag.Original : tag.Translated;
                                trans = tag.Original.TranslatedTag(tag.Translated);
                                html.AppendLine($"<a href=\"https://www.pixiv.net/tags/{Uri.EscapeDataString(tag.Original)}/artworks?s_mode=s_tag\" class=\"tag\" title=\"{trans}\" data-tag=\"{tag.Original}\" data-tooltip=\"{trans}\">#{tag.Original}</a>");
                            }
                        }
                        else
                        {
                            foreach (var tag in item.Illust.Tags)
                            {
                                var trans = tag.TranslatedTag();
                                html.AppendLine($"<a href=\"https://www.pixiv.net/tags/{Uri.EscapeDataString(tag)}/artworks?s_mode=s_tag\" class=\"tag\" title=\"{trans}\" data-tag=\"{tag}\" data-tooltip=\"{trans}\">#{tag}</a>");
                            }
                        }
                        html.AppendLine("<br/>");
                        result = html.ToString().Trim().GetHtmlFromTemplate(item.Illust.Title);
                    }
                }
            }
            catch (Exception) { }
            return (result);
        }

        private string MakeIllustDescHtml(ImageItem item)
        {
            var result = string.Empty;
            try
            {
                if (item.ItemType == ImageItemType.Work && item.Illust is Pixeez.Objects.Work)
                {
                    var contents = item.Illust.Caption.HtmlDecode();
                    contents = $"<div class=\"desc\">{Environment.NewLine}{contents.Trim()}{Environment.NewLine}</div>";
                    result = contents.GetHtmlFromTemplate(item.Illust.Title);
                }
            }
            catch (Exception) { }
            return (result);
        }

        private string MakeUserInfoHtml(Pixeez.Objects.UserInfo info)
        {
            var result = string.Empty;
            try
            {
                var nuser = info.user;
                var nprof = info.profile;
                var nworks = info.workspace;

                if (nuser != null && nprof != null && nworks != null)
                {
                    StringBuilder desc = new StringBuilder();
                    desc.AppendLine("<div class=\"desc\">");
                    desc.AppendLine($"<b>Account:</b><br/> ");
                    desc.AppendLine($"{nuser.Account} / {nuser.Id} / {nuser.Name} / {nuser.Email} <br/>");
                    desc.AppendLine($"<b>Stat:</b><br/> ");
                    desc.AppendLine($"{nprof.total_illust_bookmarks_public} Bookmarked / {nprof.total_follower} Following / {nprof.total_follow_users} Follower /<br/>");
                    desc.AppendLine($"{nprof.total_illusts} Illust / {nprof.total_manga} Manga / {nprof.total_novels} Novels /<br/> {nprof.total_mypixiv_users} MyPixiv User <br/>");

                    desc.AppendLine($"<hr/>");
                    desc.AppendLine($"<b>Profile:</b><br/>");
                    desc.AppendLine($"{nprof.gender} / {nprof.birth} / {nprof.region} / {nprof.job} <br/>");
                    desc.AppendLine($"<b>Contacts:</b><br/>");
                    desc.AppendLine($"<span class=\"twitter\" title=\"Twitter\"></span><a href=\"{nprof.twitter_url}\">@{nprof.twitter_account}</a><br/>");
                    desc.AppendLine($"<span class=\"web\" title=\"Website\"></span><a href=\"{nprof.webpage}\">{nprof.webpage}</a><br/>");
                    desc.AppendLine($"<span class=\"mail\" title=\"Email\"></span><a href=\"mailto:{nuser.Email}\">{nuser.Email}</a><br/>");

                    desc.AppendLine($"<hr/>");
                    desc.AppendLine($"<b>Workspace Device:</b><br/> ");
                    desc.AppendLine($"{nworks.pc} / {nworks.monitor} / {nworks.tablet} / {nworks.mouse} / {nworks.printer} / {nworks.scanner} / {nworks.tool} <br/>");
                    desc.AppendLine($"<b>Workspace Environment:</b><br/>");
                    desc.AppendLine($"{nworks.desk} / {nworks.chair} / {nworks.desktop} / {nworks.music} / {nworks.comment} <br/>");

                    if (!string.IsNullOrEmpty(nworks.workspace_image_url))
                    {
                        desc.AppendLine($"<hr/>");
                        desc.AppendLine($"<br/><b>Workspace Images:</b><br/>");
                        desc.AppendLine($"<img src=\"{nworks.workspace_image_url}\"/>");
                    }
                    desc.AppendLine("</div>");

                    result = desc.ToString().Trim().GetHtmlFromTemplate(nuser.Name);
                }
            }
            catch (Exception) { }
            return (result);
        }

        private string MakeUserCommentHtml(Pixeez.Objects.UserInfo info)
        {
            var result = string.Empty;
            try
            {
                var nuser = info.user;
                var nprof = info.profile;
                var nworks = info.workspace;

                var comment = nuser.comment;//.HtmlEncode();
                var contents = comment.HtmlDecode();
                contents = $"<div class=\"desc\">{Environment.NewLine}{contents.Trim()}{Environment.NewLine}</div>";
                result = contents.GetHtmlFromTemplate(IllustAuthor.Text);
            }
            catch (Exception) { }
            return (result);
        }

        public void UpdateIllustTags()
        {
            try
            {
                if (Contents is ImageItem &&
                   (Contents as ImageItem).ItemType != ImageItemType.User)
                {                    
                    WebBrowserRefresh(IllustTagsHtml);
                }
            }
            catch (Exception) { }
        }

        public void UpdateIllustDesc()
        {
            try
            {
                WebBrowserRefresh(IllustDescHtml);
            }
            catch (Exception) { }
        }

        public void UpdateWebContent()
        {
            try
            {
                WebBrowserRefresh(IllustTagsHtml);
                WebBrowserRefresh(IllustDescHtml);
            }
            catch (Exception) { }
        }

        internal void UpdateTheme()
        {
            try
            {
                if (Contents is ImageItem || Contents is ImageItem)
                {
                    UpdateWebContent();
                    btnSubPagePrev.Enable(btnSubPagePrev.IsEnabled, btnSubPagePrev.IsVisible);
                    btnSubPageNext.Enable(btnSubPageNext.IsEnabled, btnSubPageNext.IsVisible);
                }
            }
            catch (Exception) { }
        }

        private void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            var id = illustid ?? -1;
            try
            {
                foreach (var illusts in new List<ImageListGrid>() { SubIllusts, RelativeIllusts, FavoriteIllusts })
                {
                    foreach (var item in illusts.Items)
                    {
                        if (item.Illust is Pixeez.Objects.Work)
                        {
                            if (id == -1)
                                item.IsDownloaded = item.Illust.IsPartDownloadedAsync();
                            else if (id == (int)(item.Illust.Id))
                            {
                                if (illusts == SubIllusts)
                                    item.IsDownloaded = item.Illust.GetOriginalUrl(item.Index).IsDownloadedAsync();
                                else
                                {
                                    if (item.Count > 1)
                                        item.IsDownloaded = item.Illust.IsPartDownloadedAsync();
                                    else
                                        item.IsDownloaded = exists ?? item.Illust.IsPartDownloadedAsync();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        public async void UpdateDownloadStateAsync(int? illustid = null, bool? exists = null)
        {
            try
            {
                if (Contents is ImageItem)
                {
                    UpdateDownloadedMark(Contents);
                    SubIllusts.UpdateTilesState(false);
                }

                await Task.Run(() =>
                {
                    UpdateDownloadState(illustid, exists);
                });
            }
            catch(Exception) { }
        }

        private void UpdateDownloadedMark()
        {
            try
            {
                if (Contents is ImageItem)
                {
                    UpdateDownloadedMark(Contents);
                }
            }
            catch (Exception) { }
        }

        private void UpdateDownloadedMark(ImageItem item, bool? exists = null)
        {
            try
            {
                if (item is ImageItem)
                {
                    string fp = string.Empty;
                    var index = item.Index;
                    if (index < 0)
                    {
                        var download = item.Illust.IsPartDownloadedAsync(out fp);
                        if (item.IsDownloaded != download) item.IsDownloaded = download;
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
                        var download = item.Illust.GetOriginalUrl(item.Index).IsDownloadedAsync(out fp);
                        if (download)
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
            catch (Exception) { }
        }

        private void UpdateFavMark(Pixeez.Objects.Work illust)
        {
            try
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
            catch (Exception) { }
        }

        private void UpdateFollowMark(Pixeez.Objects.UserBase user)
        {
            try
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
            catch (Exception) { }
        }

        public async void UpdateLikeStateAsync(int illustid = -1, bool is_user = false)
        {
            await new Action(() =>
            {
                UpdateLikeState(illustid, is_user);
            }).InvokeAsync();
        }

        public void UpdateLikeState(int illustid = -1, bool is_user = false)
        {
            try
            {
                if (Contents is ImageItem)
                {
                    UpdateFollowMark(Contents.User);
                    if (Contents.ItemType != ImageItemType.User)
                        UpdateFavMark(Contents.Illust);
                }

                if (RelativeIllustsExpander.IsExpanded)
                {
                    RelativeIllusts.UpdateLikeState(illustid, is_user);
                }
                if (FavoriteIllustsExpander.IsExpanded)
                {
                    FavoriteIllusts.UpdateLikeState(illustid, is_user);
                }
            }
            catch (Exception) { }
        }

        private async void UpdateSubPageNav()
        {
            try
            {
                if (Contents is ImageItem)
                {
                    if (Contents.ItemType == ImageItemType.User)
                    {
                        btnSubPageNext.Hide();
                        btnSubPagePrev.Hide();
                    }
                    else
                    {
                        if (Contents.Count > 1)
                        {
                            btnSubPagePrev.Enable(Contents.Index > 0);
                            btnSubPageNext.Enable(Contents.Index < Contents.Count - 1);

                            if (SubIllusts.SelectedIndex < 0)
                            {
                                SubIllusts.SelectedIndex = 0;
                                await Task.Delay(1);
                            }
                        }
                        else
                        {
                            btnSubPageNext.Hide();
                            btnSubPagePrev.Hide();
                        }
                    }
                }
                await Task.Delay(1);
            }
            catch (Exception) { }
        }

        private void OpenDownloaded()
        {
            try
            {
                if (Contents is ImageItem)
                {
                    if (IllustDownloaded.Tag is string)
                    {
                        var fp = IllustDownloaded.Tag as string;
                        fp.OpenFileWithShell();
                    }
                }
            }
            catch (Exception) { }
        }

        public async void UpdateThumb(bool full = false)
        {
            await new Action(() => 
            {
                try
                {
                    if (Contents is ImageItem)
                    {
                        if (full)
                        {
                            SubIllusts.UpdateTilesImage();
                            RelativeIllusts.UpdateTilesImage();
                            FavoriteIllusts.UpdateTilesImage();
                            if (Contents.ItemType != ImageItemType.User)
                            {
                                ActionRefreshAvator(Contents);
                                ActionRefreshPreview_Click(this, new RoutedEventArgs());
                            }
                            else if (Contents.ItemType == ImageItemType.User)
                            {
                                ActionRefreshAvator(Contents);
                                UpdateUserBackground();
                            }
                        }
                        else
                        {
                            if (SubIllusts.IsKeyboardFocusWithin)
                                SubIllusts.UpdateTilesImage();
                            else if (RelativeIllusts.IsKeyboardFocusWithin)
                                RelativeIllusts.UpdateTilesImage();
                            else if (FavoriteIllusts.IsKeyboardFocusWithin)
                                FavoriteIllusts.UpdateTilesImage();
                            else
                            {
                                UpdateThumb(true);
                            }
                        }
                    }
                }
                catch (Exception) { }
                finally
                {
                    Application.Current.DoEvents();
                }
            }).InvokeAsync();
        }

        internal async void UpdateDetail(ImageItem item)
        {
            try
            {
                if (item.ItemType == ImageItemType.Work || item.ItemType == ImageItemType.Manga)
                {
                    await new Action(async () =>
                    {
                        IllustDetailWait.Show();
                        if (Keyboard.Modifiers == ModifierKeys.Control)
                        {
                            var illust = await item.ID.RefreshIllust();
                            if (illust is Pixeez.Objects.Work)
                            {
                                item.Illust = illust;
                                item.Illust.Cache();
                            }
                            else
                            {
                                "Illust not exists or deleted".ShowMessageBox("ERROR[ILLUST]");
                            }
                        }
                        UpdateDetailIllust(item);
                        IllustDetailWait.Hide();
                    }).InvokeAsync();
                }
                else if (item.ItemType == ImageItemType.User)
                {
                    await new Action(async () =>
                    {
                        IllustDetailWait.Show();
                        if (Keyboard.Modifiers == ModifierKeys.Control)
                        {
                            var user = await item.UserID.RefreshUser();
                            if (user is Pixeez.Objects.User)
                            {
                                item.User = user;
                                item.User.Cache();
                            }
                            else
                            {
                                "User not exists or deleted".ShowMessageBox("ERROR[USER]");
                            }
                        }
                        UpdateDetailUser(item.User);
                        IllustDetailWait.Hide();
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

        private async void UpdateDetailIllust(ImageItem item)
        {
            try
            {
                IllustDetailWait.Show();
                this.DoEvents();

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
                    item.Illust.IsPartDownloadedAsync(out fp);
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
                IllustTitle.ToolTip = IllustTitle.Text.TranslatedTag();

                if (item.Sanity.Equals("18+"))
                    IllustSanity.Text = "18";
                else if (item.Sanity.Equals("17+"))
                    IllustSanity.Text = "17";
                else if (item.Sanity.Equals("15+"))
                    IllustSanity.Text = "15";
                else
                    IllustSanity.Text = "";
                IllustSanityInfo.ToolTip = $"R[{item.Sanity}]";

                if (string.IsNullOrEmpty(IllustSanity.Text)) IllustSanityInfo.Hide();
                else IllustSanityInfo.Show();

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
                    WebBrowserRefresh(IllustTagsHtml);

                    IllustTagExpander.Header = "Tags";
                    if (setting.AutoExpand == AutoExpandMode.AUTO ||
                        setting.AutoExpand == AutoExpandMode.ON ||
                        setting.AutoExpand == AutoExpandMode.SINGLEPAGE)
                    {
                        if (!IllustTagExpander.IsExpanded) IllustTagExpander.IsExpanded = true;
                    }
                    else IllustTagExpander.IsExpanded = false;
                    IllustTagExpander.Show();
                    btnIllustTagPedia.Show();
                }
                else
                {
                    IllustTagsHtml.DocumentText = string.Empty;
                    IllustTagExpander.Hide();
                    btnIllustTagPedia.Hide();
                }

                if (!string.IsNullOrEmpty(item.Illust.Caption) && item.Illust.Caption.Length > 0)
                {
                    WebBrowserRefresh(IllustDescHtml);

                    if (setting.AutoExpand == AutoExpandMode.AUTO ||
                        setting.AutoExpand == AutoExpandMode.ON ||
                        (setting.AutoExpand == AutoExpandMode.SINGLEPAGE && item.Illust.PageCount <= 1))
                    {
                        if (!IllustDescExpander.IsExpanded) IllustDescExpander.IsExpanded = true;
                    }
                    else IllustDescExpander.IsExpanded = false;
                    IllustDescExpander.Show();
                }
                else
                {
                    IllustDescHtml.DocumentText = string.Empty;
                    IllustDescExpander.Hide();
                }

                SubIllusts.Tag = 0;
                SubIllustsExpander.IsExpanded = false;
                SubIllusts.Items.Clear();
                PreviewBadge.Badge = item.Illust.PageCount;
                if (item.Illust is Pixeez.Objects.Work && item.Illust.PageCount > 1)
                {
                    item.Index = 0;
                    PreviewBadge.Show();
                    SubIllustsExpander.Show();
                    SubIllustsExpander.IsExpanded = true;
                }
                else
                {
                    PreviewBadge.Hide();
                    SubIllustsExpander.Hide();
                }
                UpdateSubPageNav();

                RelativeIllustsExpander.Header = "Related Illusts";
                RelativeIllustsExpander.IsExpanded = false;
                RelativeIllustsExpander.Show();
                RelativeIllusts.Items.Clear();
                RelativeNextPage.Hide();

                FavoriteIllustsExpander.Header = "Author Favorite";
                FavoriteIllustsExpander.IsExpanded = false;
                FavoriteIllustsExpander.Show();
                FavoriteIllusts.Items.Clear();
                FavoriteNextPage.Hide();
#if DEBUG
                CommentsExpander.IsExpanded = false;
                CommentsNavigator.Hide();
                CommentsExpander.Show();
#else
                CommentsExpander.IsExpanded = false;
                CommentsNavigator.Hide();
                CommentsExpander.Hide();
#endif
                await Task.Delay(1);
                ActionRefreshAvator(item);
                if (!SubIllustsExpander.IsExpanded)
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
                item.Illust.AddToHistory();
                Application.Current.DoEvents();
                IllustDetailWait.Hide();
            }
        }

        private string user_backgroundimage_url = string.Empty;
        public async void UpdateUserBackground()
        {
            if (setting.ShowUserBackgroundImage)
            {
                if (string.IsNullOrEmpty(user_backgroundimage_url))
                {
                    Preview.Source = await user_backgroundimage_url.LoadImageFromUrl();
                    PreviewViewer.Show();
                    PreviewBox.Show();
                }
            }
        }

        private Pixeez.Objects.UserInfo UserInfo = null;
        private async void UpdateDetailUser(Pixeez.Objects.UserBase user)
        {
            try
            {
                IllustDetailWait.Show();
                this.DoEvents();

                var tokens = await CommonHelper.ShowLogin();

                UserInfo = await tokens.GetUserInfoAsync(user.Id.Value.ToString());
                var nuser = UserInfo.user;
                var nprof = UserInfo.profile;
                var nworks = UserInfo.workspace;

                PreviewWait.Hide();
                PreviewViewer.Hide();
                PreviewViewer.Height = 0;
                PreviewBox.Hide();
                PreviewBox.Height = 0;
                Preview.Source = null;

                user_backgroundimage_url = nprof.background_image_url is string ? nprof.background_image_url as string : nuser.GetPreviewUrl();
                UpdateUserBackground();

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

                btnIllustTagPedia.Hide();

                if (nuser != null && nprof != null && nworks != null)
                {
                    WebBrowserRefresh(IllustTagsHtml);
                    //IllustTagsHtml.DocumentText = MakeUserInfoHtml(UserInfo);
                    //AdjustBrowserSize(IllustTagsHtml);

                    IllustTagExpander.Header = "User Infomation";
                    if (setting.AutoExpand == AutoExpandMode.ON)
                        IllustTagExpander.IsExpanded = true;
                    else
                        IllustTagExpander.IsExpanded = false;
                    IllustTagExpander.Show();
                }
                else
                {
                    IllustTagExpander.Hide();
                }

                CommentsExpander.Hide();
                CommentsNavigator.Hide();

                if (nuser != null && !string.IsNullOrEmpty(nuser.comment) && nuser.comment.Length > 0)
                {
                    WebBrowserRefresh(IllustDescHtml);
                    //IllustDescHtml.DocumentText = MakeUserCommentHtml(UserInfo);
                    //AdjustBrowserSize(IllustDescHtml);

                    if (setting.AutoExpand == AutoExpandMode.ON ||
                        setting.AutoExpand == AutoExpandMode.AUTO)
                    {
                        if (!IllustDescExpander.IsExpanded) IllustDescExpander.IsExpanded = true;
                    }
                    else IllustDescExpander.IsExpanded = false;
                    IllustDescExpander.Show();
                }
                else
                {
                    IllustDescExpander.Hide();
                }

                SubIllusts.Items.Clear();
                SubIllustsExpander.IsExpanded = false;
                SubIllustsExpander.Hide();
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
                user.AddToHistory();
                Application.Current.DoEvents();
                IllustDetailWait.Hide();
            }
        }

        private async Task ShowIllustPages(ImageItem item, int index = 0, int start = 0, int count = 30)
        {
            try
            {
                IllustDetailWait.Show();

                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                if (item.Illust is Pixeez.Objects.Work)
                {
                    var total = item.Illust.PageCount;
                    var pageCount = (total / count + (total % count > 0 ? 1 : 0)).Value;
                    var pageNum = SubIllusts.Tag is int ? (int)(SubIllusts.Tag) : 0;

                    #region Update sub-pages nav button
                    if (pageNum <= 0)
                    {
                        pageNum = 0;
                        btnSubIllustPrevPages.Hide();
                    }
                    else
                        btnSubIllustPrevPages.Show();

                    if (pageNum >= pageCount - 1)
                    {
                        pageNum = pageCount - 1;
                        btnSubIllustNextPages.Hide();
                    }
                    else
                        btnSubIllustNextPages.Show();

                    SubIllusts.Tag = pageNum;
                    this.DoEvents();
                    #endregion

                    start = pageNum * count;
                    var end = start + count;

                    if (item.Illust is Pixeez.Objects.IllustWork)
                    {
                        var subset = item.Illust as Pixeez.Objects.IllustWork;
                        if (subset.meta_pages.Count() > 1)
                        {
                            total = subset.meta_pages.Count();
                            var pages = subset.meta_pages.Skip(start).Take(count).ToList();
                            SubIllusts.Items.Clear();
                            for (var i = start; i < end; i++)
                            {
                                if (i == total) break;
                                var p = pages[i-start];
                                p.AddTo(SubIllusts.Items, item.Illust, i, item.NextURL);
                                this.DoEvents();
                            }
                        }
                    }
                    else if (item.Illust is Pixeez.Objects.NormalWork)
                    {
                        var subset = item.Illust as Pixeez.Objects.NormalWork;
                        if (subset.PageCount >= 1 && subset.Metadata == null)
                        {
                            var illust = await item.Illust.RefreshIllust();
                            if (illust is Pixeez.Objects.Work)
                            {
                                item.Illust = illust;
                            }
                        }
                        if (item.Illust.Metadata is Pixeez.Objects.Metadata)
                        {
                            total = item.Illust.Metadata.Pages.Count();
                            var pages = item.Illust.Metadata.Pages.Skip(start).Take(count).ToList();
                            SubIllusts.Items.Clear();
                            for (var i = start; i < end; i++)
                            {
                                if (i == total) break;
                                var p = pages[i-start];
                                p.AddTo(SubIllusts.Items, item.Illust, i, item.NextURL);
                                this.DoEvents();
                            }
                        }
                    }
                    SubIllusts.UpdateTilesImage();
                    this.DoEvents();

                    if (index < 0) index = 0;
                    SubIllusts.SelectedIndex = index;
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                IllustDetailWait.Hide();
                this.DoEvents();
            }
        }

        private async void ShowIllustPagesAsync(ImageItem item, int index = 0, int start = 0, int count = 30)
        {
            await new Action(async () =>
            {
                await ShowIllustPages(item, index, start, count);
            }).InvokeAsync();
        }

        private async Task ShowRelativeInline(ImageItem item, string next_url = "")
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
                        this.DoEvents();
                    }
                    this.DoEvents();
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

        private async void ShowRelativeInlineAsync(ImageItem item, string next_url = "")
        {
            await new Action(async () =>
            {
                await ShowRelativeInline(item, next_url);
            }).InvokeAsync();
        }

        private async Task ShowUserWorksInline(Pixeez.Objects.UserBase user, string next_url = "")
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
                        this.DoEvents();
                    }
                    this.DoEvents();
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

        private async void ShowUserWorksInlineAsync(Pixeez.Objects.UserBase user, string next_url = "")
        {
            await new Action(async () =>
            {
                await ShowUserWorksInline(user, next_url);
            }).InvokeAsync();
        }

        private string last_restrict = string.Empty;
        private async Task ShowFavoriteInline(Pixeez.Objects.UserBase user, string next_url = "")
        {
            try
            {
                IllustDetailWait.Show();
                FavoriteIllusts.Items.Clear();

                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                var lastUrl = next_url;
                var restrict = Keyboard.Modifiers != ModifierKeys.None ? "private" : "public";
                if (!last_restrict.Equals(restrict, StringComparison.CurrentCultureIgnoreCase)) next_url = string.Empty;
                FavoriteIllustsExpander.Header = $"Favorite ({CultureInfo.CurrentCulture.TextInfo.ToTitleCase(restrict)})";

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
                        this.DoEvents();
                    }
                    this.DoEvents();
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

        private async void ShowFavoriteInlineAsync(Pixeez.Objects.UserBase user, string next_url = "")
        {
            await new Action(async () =>
            {
                await ShowFavoriteInline(user, next_url);
            }).InvokeAsync();
        }

        internal void KeyAction(KeyEventArgs e)
        {
            Page_KeyUp(this, e);
        }

        #region WebBrowsre helper routines
        private void InitHtmlRenderHost(out WindowsFormsHostEx host, System.Windows.Forms.WebBrowser browser, Panel panel)
        {
            try
            {
                host = new WindowsFormsHostEx()
                {
                    //IsRedirected = true,
                    //CompositionMode = ,
                    AllowDrop = false,
                    MinHeight = 24,
                    MaxHeight = 480,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Child = browser
                };
                if (panel is Panel) panel.Children.Add(host);
            }
            catch (Exception) { host = null; }
        }

        private void InitHtmlRender(out System.Windows.Forms.WebBrowser browser)
        {
            browser = new System.Windows.Forms.WebBrowser()
            {
                DocumentText = string.Empty.GetHtmlFromTemplate(),
                Dock = System.Windows.Forms.DockStyle.Fill,
                ScriptErrorsSuppressed = true,
                WebBrowserShortcutsEnabled = true,
                AllowWebBrowserDrop = false
            };
            browser.Navigate("about:blank");
            browser.Document.Write(string.Empty);

            try
            {
                if (browser is System.Windows.Forms.WebBrowser)
                {
                    browser.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(WebBrowser_DocumentCompleted);
                    browser.Navigating += new System.Windows.Forms.WebBrowserNavigatingEventHandler(WebBrowser_Navigating);
                    browser.Navigated += new System.Windows.Forms.WebBrowserNavigatedEventHandler(WebBrowser_Navigated);
                    browser.ProgressChanged += new System.Windows.Forms.WebBrowserProgressChangedEventHandler(WebBrowser_ProgressChanged);
                    browser.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(WebBrowser_PreviewKeyDown);
                }
            }
            catch (Exception) { }
        }

        private void CreateHtmlRender()
        {
            try
            {
                InitHtmlRender(out IllustTagsHtml);
                InitHtmlRender(out IllustDescHtml);
                InitHtmlRenderHost(out tagsHost, IllustTagsHtml, IllustTagsHost);
                InitHtmlRenderHost(out descHost, IllustDescHtml, IllustDescHost);

                UpdateTheme();
                this.UpdateLayout();
            }
            catch (Exception) { }
        }

        private void DeleteHtmlRender()
        {
            try
            {
                if (IllustTagsHtml is System.Windows.Forms.WebBrowser) IllustTagsHtml.Dispose();
            }
            catch { }
            try
            {
                if (IllustDescHtml is System.Windows.Forms.WebBrowser) IllustDescHtml.Dispose();
            }
            catch { }
            try
            {
                if (tagsHost is WindowsFormsHostEx) tagsHost.Dispose();
            }
            catch { }
            try
            {
                if (descHost is WindowsFormsHostEx) descHost.Dispose();
            }
            catch { }

            try
            {
                if (CommentsViewer is WebBrowser) CommentsViewer.Dispose();
            }
            catch { }
        }
        #endregion

        public IllustDetailPage()
        {
            InitializeComponent();

            RelativeIllusts.Columns = 5;
            FavoriteIllusts.Columns = 5;

            IllustDetailWait.Visibility = Visibility.Collapsed;
            btnSubPagePrev.Hide();
            btnSubPageNext.Hide();

            CreateHtmlRender();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if(Contents is ImageItem) UpdateDetail(Contents);
            else if(Tag is Pixeez.Objects.UserBase) UpdateDetail(Tag as Pixeez.Objects.UserBase);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            DeleteHtmlRender();
        }

        private long lastKeyUp = Environment.TickCount;
        private async void Page_KeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = false;
            if (e.Timestamp - lastKeyUp > 50)
            {
                lastKeyUp = e.Timestamp;
                var pub = setting.PrivateFavPrefer ? false : true;

                if (e.Key == Key.F3 || e.SystemKey == Key.F3)
                {
                    if (!(Parent is ContentWindow))
                    {
                        Commands.AppendPage.Execute(Application.Current.MainWindow);
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.F5 || e.SystemKey == Key.F5)
                {
                    if (!(Parent is ContentWindow)) Commands.RefreshPage.Execute(Application.Current.MainWindow);
                    if (Contents is ImageItem) UpdateDetail(Contents);
                    e.Handled = true;
                }
                else if (e.Key == Key.F6 || e.SystemKey == Key.F6)
                {
                    if (!(Parent is ContentWindow)) Commands.RefreshPageThumb.Execute(Application.Current.MainWindow);
                    UpdateThumb();
                    e.Handled = true;
                }
                else if (e.Key == Key.F7 || e.SystemKey == Key.F7)
                {
                    if (Contents is ImageItem)
                    {
                        if (Contents.ItemType != ImageItemType.User)
                        {
                            if (Keyboard.Modifiers == ModifierKeys.None)
                                await Contents.Illust.Like(pub);
                            else if (Keyboard.Modifiers == ModifierKeys.Shift)
                                await Contents.Illust.Like(!pub);
                            else if (Keyboard.Modifiers == ModifierKeys.Alt)
                                await Contents.Illust.UnLike();
                        }
                        else if (Contents.ItemType == ImageItemType.User)
                        {
                            IList<ImageItem> items = null;
                            if (RelativeIllusts.IsKeyboardFocusWithin) items = RelativeIllusts.GetSelected(true);
                            else if (FavoriteIllusts.IsKeyboardFocusWithin) items = FavoriteIllusts.GetSelected(true);
                            if (items is IList<ImageItem> && items.Count > 0)
                            {
                                if (Keyboard.Modifiers == ModifierKeys.None)
                                    items.LikeIllust(pub);
                                else if (Keyboard.Modifiers == ModifierKeys.Shift)
                                    items.LikeIllust(!pub);
                                else if (Keyboard.Modifiers == ModifierKeys.Alt)
                                    items.UnLikeIllust();
                            }
                        }
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.F8 || e.SystemKey == Key.F8)
                {
                    if (Contents is ImageItem)
                    {
                        if (Keyboard.Modifiers == ModifierKeys.None)
                            await Contents.User.Like(pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Shift)
                            await Contents.User.Like(!pub);
                        else if (Keyboard.Modifiers == ModifierKeys.Alt)
                            await Contents.User.UnLike();
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    Commands.SaveIllust.Execute(SubIllusts);
                    e.Handled = true;
                }
                else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    Commands.SaveIllustAll.Execute(Contents);
                    e.Handled = true;
                }
                else e.Handled = false;
            }
        }

        #region WebBrowser Events Handle
        private void WebBrowserRefresh(System.Windows.Forms.WebBrowser browser)
        {
            try
            {
                var contents = string.Empty;
                if (Contents is ImageItem)
                {
                    if (browser == IllustTagsHtml)
                    {
                        if (Contents.ItemType == ImageItemType.User)
                            contents = MakeUserInfoHtml(UserInfo);                        
                        else
                            contents = MakeIllustTagsHtml(Contents);
                    }
                    else if (browser == IllustDescHtml)
                    {
                        if (Contents.ItemType == ImageItemType.User)
                            contents = MakeUserCommentHtml(UserInfo);
                        else
                            contents = MakeIllustDescHtml(Contents);
                    }

                }
                if(!string.IsNullOrEmpty(contents))
                {
                    browser.DocumentText = contents;
                    browser.Document.Write(string.Empty);
                    AdjustBrowserSize(browser);
                }
            }
            catch (Exception) { }
        }

        private async void WebBrowser_LinkClick(object sender, System.Windows.Forms.HtmlElementEventArgs e)
        {
            bCancel = true;
            try
            {
                e.BubbleEvent = false;
                e.ReturnValue = false;

                if (e.EventType.Equals("click", StringComparison.CurrentCultureIgnoreCase))
                {
                    var from = e.FromElement;
                    var link = sender as System.Windows.Forms.HtmlElement;

                    var tag = link.GetAttribute("data-tag");
                    if (string.IsNullOrEmpty(tag))
                    {
                        var href = link.GetAttribute("href");
                        var href_lower = href.ToLower();
                        if (!string.IsNullOrEmpty(href))
                        {
                            if (href_lower.StartsWith("pixiv://illusts/", StringComparison.CurrentCultureIgnoreCase))
                            {
                                var illust_id = Regex.Replace(href, @"pixiv://illusts/(\d+)", "$1", RegexOptions.IgnoreCase);
                                if (!string.IsNullOrEmpty(illust_id))
                                {
                                    var illust = illust_id.FindIllust();
                                    if (illust is Pixeez.Objects.Work)
                                    {
                                        await new Action(() =>
                                        {
                                            Commands.Open.Execute(illust);
                                        }).InvokeAsync();
                                    }
                                    else
                                    {
                                        illust = await illust_id.RefreshIllust();
                                        if (illust is Pixeez.Objects.Work)
                                        {
                                            await new Action(() =>
                                            {
                                                Commands.Open.Execute(illust);
                                            }).InvokeAsync();
                                        }
                                    }
                                }
                            }
                            else if (href_lower.StartsWith("pixiv://users/", StringComparison.CurrentCultureIgnoreCase))
                            {
                                var user_id = Regex.Replace(href, @"pixiv://users/(\d+)", "$1", RegexOptions.IgnoreCase);
                                var user = user_id.FindUser();
                                if (user is Pixeez.Objects.User)
                                {
                                    await new Action(() =>
                                    {
                                        Commands.Open.Execute(user);
                                    }).InvokeAsync();
                                }
                                else
                                {
                                    user = await user_id.RefreshUser();
                                    if (user is Pixeez.Objects.User)
                                    {
                                        await new Action(() =>
                                        {
                                            Commands.Open.Execute(user);
                                        }).InvokeAsync();
                                    }
                                }
                            }
                            else if (href_lower.StartsWith("http", StringComparison.CurrentCultureIgnoreCase) && href_lower.Contains("dic.pixiv.net/"))
                            {
                                await new Action(() =>
                                {
                                    Commands.OpenPedia.Execute(href);
                                }).InvokeAsync();
                            }
                            else if (href_lower.StartsWith("about:/a", StringComparison.CurrentCultureIgnoreCase))
                            {
                                href = href.Replace("about:/a", "https://dic.pixiv.net/a");
                                await new Action(() =>
                                {
                                    Commands.OpenPedia.Execute(href);
                                }).InvokeAsync();
                            }
                            else if (href_lower.Contains("pixiv.net/") || href_lower.Contains("pximg.net/"))
                            {
                                await new Action(() =>
                                {
                                    Commands.OpenSearch.Execute(href);
                                }).InvokeAsync();
                            }
                            else
                            {
                                e.BubbleEvent = true;
                                e.ReturnValue = true;
                            }
                        }
                    }
                    else
                    {
                        var tag_tooltip = link.GetAttribute("data-tooltip");
                        if(!e.AltKeyPressed && !e.CtrlKeyPressed && !e.ShiftKeyPressed)
                            Commands.OpenSearch.Execute($"Fuzzy Tag:{tag}");
                        else if (e.AltKeyPressed && !e.CtrlKeyPressed && !e.ShiftKeyPressed)
                            Commands.OpenSearch.Execute($"Tag:{tag}");
                        else if (!e.AltKeyPressed && !e.CtrlKeyPressed && e.ShiftKeyPressed)
                            Commands.OpenPedia.Execute(tag);
                        else if (!e.AltKeyPressed && e.CtrlKeyPressed && !e.ShiftKeyPressed)
                            Commands.Speech.Execute(tag);
                        else if (!e.AltKeyPressed && e.CtrlKeyPressed && e.ShiftKeyPressed)
                            Commands.Speech.Execute(tag_tooltip);
                    }
                }
            }
#if DEBUG
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
#else
            catch (Exception) { }
#endif
        }

        private async void WebBrowser_ProgressChanged(object sender, System.Windows.Forms.WebBrowserProgressChangedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Forms.WebBrowser)
                {
                    var browser = sender as System.Windows.Forms.WebBrowser;

                    if (browser.Document != null)
                    {
                        foreach (System.Windows.Forms.HtmlElement imgElemt in browser.Document.Images)
                        {
                            var src = imgElemt.GetAttribute("src");
                            if (!string.IsNullOrEmpty(src))
                            {
                                await new Action(async () =>
                                {
                                    try
                                    {
                                        if (src.ToLower().Contains("no_image_p.svg"))
                                            imgElemt.SetAttribute("src", new Uri(System.IO.Path.Combine(Application.Current.GetRoot(), "no_image.png")).AbsoluteUri);
                                        else if (src.IsPixivImage())
                                        {
                                            var img = await src.GetImagePath();
                                            if (!string.IsNullOrEmpty(img)) imgElemt.SetAttribute("src", new Uri(img).AbsoluteUri);
                                        }
                                    }
#if DEBUG
                                    catch (Exception ex)
                                    {
                                        ex.Message.DEBUG();
                                    }
#else
                                    catch (Exception) { }
#endif
                                }).InvokeAsync();
                            }
                        }
                    }
                }
            }
#if DEBUG
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
#else
                catch (Exception) { }
#endif
        }

        private void WebBrowser_Navigating(object sender, System.Windows.Forms.WebBrowserNavigatingEventArgs e)
        {
            if (e.Url.OriginalString.StartsWith("about:")) return;
            //e.Cancel = true;
            if (bCancel == true)
            {
                try
                {
                    e.Cancel = true;
                    bCancel = false;
                }
                catch (Exception) { }
            }
        }

        private void WebBrowser_Navigated(object sender, System.Windows.Forms.WebBrowserNavigatedEventArgs e)
        {
            try
            {
            }
            catch (Exception) { }
        }

        private void WebBrowser_DocumentCompleted(object sender, System.Windows.Forms.WebBrowserDocumentCompletedEventArgs e)
        {
            try
            {
                if (sender == IllustDescHtml || sender == IllustTagsHtml)
                {
                    var browser = sender as System.Windows.Forms.WebBrowser;

                    var document = browser.Document;
                    foreach (System.Windows.Forms.HtmlElement link in document.Links)
                    {
                        try
                        {
                            link.Click += WebBrowser_LinkClick;
                        }
                        catch (Exception) { continue; }
                    }
                }
            }
#if DEBUG
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
#else
            catch (Exception) { }
#endif
        }

        private void WebBrowser_PreviewKeyDown(object sender, System.Windows.Forms.PreviewKeyDownEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Forms.WebBrowser)
                {
                    var browser = sender as System.Windows.Forms.WebBrowser;
                    if (e.Control && e.KeyCode == System.Windows.Forms.Keys.C)
                    {
                        var text = GetText(browser);
                        if (sender == IllustTagsHtml) text = text.Replace("#", " ");
                        if (!string.IsNullOrEmpty(text))
                            Commands.CopyText.Execute(text);
                    }
                    else if (e.Shift && e.KeyCode == System.Windows.Forms.Keys.C)
                    {
                        var data = new HtmlTextData()
                        {
                            Html = GetText(browser, true),
                            Text = GetText(browser, false)
                        };
                        Commands.CopyText.Execute(data);
                    }
                    else if (e.KeyCode == System.Windows.Forms.Keys.F5)
                    {
                        WebBrowserRefresh(browser);
                    }
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR[BROWSER]"); }
#else
            catch (Exception) { }
#endif
        }
        #endregion

        #region Illust Info relatice events/helper routines
        private void ActionSpeech_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Empty;
            CultureInfo culture = null;
            var is_tag = false;
            try
            {
                if (sender == btnIllustTagSpeech)
                {
                    is_tag = true;
                    text = GetText(IllustTagsHtml);
                }
                else if (sender == btnIllustDescSpeech)
                    text = GetText(IllustDescHtml);
                else if (sender == IllustTitle)
                    text = IllustTitle.Text;
                else if (sender == IllustAuthor)
                    text = IllustAuthor.Text;
                else if (sender is MenuItem)
                {
                    var mi = sender as MenuItem;

                    if (mi.Uid.Equals("SpeechAuto", StringComparison.CurrentCultureIgnoreCase))
                        culture = null;
                    else if (mi.Uid.Equals("SpeechChineseS", StringComparison.CurrentCultureIgnoreCase))
                        culture = CultureInfo.GetCultureInfoByIetfLanguageTag("zh-CN");
                    else if (mi.Uid.Equals("SpeechChineseT", StringComparison.CurrentCultureIgnoreCase))
                        culture = CultureInfo.GetCultureInfoByIetfLanguageTag("zh-TW");
                    else if (mi.Uid.Equals("SpeechJapaness", StringComparison.CurrentCultureIgnoreCase))
                        culture = CultureInfo.GetCultureInfoByIetfLanguageTag("ja-JP");
                    else if (mi.Uid.Equals("SpeechKorean", StringComparison.CurrentCultureIgnoreCase))
                        culture = CultureInfo.GetCultureInfoByIetfLanguageTag("ko-KR");
                    else if (mi.Uid.Equals("SpeechEnglish", StringComparison.CurrentCultureIgnoreCase))
                        culture = CultureInfo.GetCultureInfoByIetfLanguageTag("en-US");

                    if (mi.Parent is ContextMenu)
                    {
                        var host = (mi.Parent as ContextMenu).PlacementTarget;
                        if (host == btnIllustTagSpeech) { is_tag = true; text = GetText(IllustTagsHtml); }
                        else if (host == btnIllustDescSpeech) text = GetText(IllustDescHtml);
                        else if (host == IllustAuthor) text = IllustAuthor.Text;
                        else if (host == IllustTitle) text = IllustTitle.Text;
                        else if (host == SubIllustsExpander || host == SubIllusts) text = IllustTitle.Text;
                        else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                        {
                            foreach (ImageItem item in RelativeIllusts.GetSelected())
                            {
                                text += $"{item.Illust.Title},{Environment.NewLine}";
                            }
                        }
                        else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                        {
                            foreach (ImageItem item in FavoriteIllusts.GetSelected())
                            {
                                text += $"{item.Illust.Title},{Environment.NewLine}";
                            }
                        }
                    }
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception) { }
#endif
            if (culture == null)
            {
                if(is_tag)
                    text = string.Join(Environment.NewLine, text.Trim().Trim('#').Split('#'));
                else
                    text = string.Join(Environment.NewLine, text.Trim().Split());
            }
            if (!string.IsNullOrEmpty(text)) text.Play(culture);
        }

        private void ActionSendToInstance_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Empty;
            try
            {
                if (sender is MenuItem)
                {
                    var mi = sender as MenuItem;
                    if (mi.Parent is ContextMenu)
                    {
                        var host = (mi.Parent as ContextMenu).PlacementTarget;
                        if (host == btnIllustTagSpeech)
                            text = $"\"tag:{string.Join($"\"{Environment.NewLine}\"tag:", GetText(IllustTagsHtml).Trim().Trim('#').Split('#'))}\"";
                        else if (host == btnIllustDescSpeech)
                            text = $"\"{string.Join("\" \"", GetText(IllustDescHtml).ParseLinks().ToArray())}\"";
                        else if (host == IllustAuthor)
                            text = $"\"user:{IllustAuthor.Text}\"";
                        else if (host == IllustTitle)
                            text = $"\"title:{IllustTitle.Text}\"";
                    }
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception) { }
#endif
            if (!string.IsNullOrEmpty(text))
            {
                if (Keyboard.Modifiers == ModifierKeys.None)
                    Commands.SendToOtherInstance.Execute(text);
                else
                    Commands.ShellSendToOtherInstance.Execute(text);
            }
        }

        private void ActionRefresh_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Empty;
            try
            {
                if (sender is MenuItem)
                {
                    var mi = sender as MenuItem;
                    if (mi.Parent is ContextMenu)
                    {
                        var host = (mi.Parent as ContextMenu).PlacementTarget;
                        if (host == btnIllustTagSpeech)
                        {
                            if (Keyboard.Modifiers == ModifierKeys.None)
                                WebBrowserRefresh(IllustTagsHtml);
                            else if (Keyboard.Modifiers == ModifierKeys.Shift)
                                Application.Current.LoadTags(false, true);
                            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                                Application.Current.LoadSetting().CustomTagsFile.OpenFileWithShell();
                        }
                        else if (host == btnIllustDescSpeech)
                        {
                            if (Keyboard.Modifiers == ModifierKeys.None)
                                WebBrowserRefresh(IllustDescHtml);
                            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                                Application.Current.LoadSetting().ContentsTemplateFile.OpenFileWithShell();
                        }
                        else if (mi == ActionRefresh)
                        {
                            if (this is IllustDetailPage)
                            {
                                var page = this as IllustDetailPage;
                                if (page.Tag is ImageItem)
                                    page.UpdateDetail(page.Tag as ImageItem);
                                else if (page.Tag is Pixeez.Objects.UserBase)
                                    page.UpdateDetail(page.Tag as Pixeez.Objects.UserBase);
                            }
                        }
                    }
                }
                else if (sender == btnIllustTagRefresh)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        WebBrowserRefresh(IllustTagsHtml);
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        Application.Current.LoadTags(false, true);
                    else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                        Application.Current.LoadSetting().CustomTagsFile.OpenFileWithShell();
                }
                else if (sender == btnIllustDescRefresh)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        WebBrowserRefresh(IllustTagsHtml);
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        Application.Current.LoadCustomTemplate();
                    else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                        Application.Current.LoadSetting().ContentsTemplateFile.OpenFileWithShell();
                }
                else if (sender == btnSubIllustRefresh)
                {
                    SubIllusts.UpdateTilesImage();
                }
                else if (sender == btnRelativeRefresh)
                {
                    RelativeIllusts.UpdateTilesImage();
                }
                else if (sender == btnFavoriteRefresh)
                {
                    FavoriteIllusts.UpdateTilesImage();
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception) { }
#endif
        }

        private void ActionOpenPedia_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender == btnIllustTagPedia)
                {
                    var shell = Keyboard.Modifiers == ModifierKeys.Control ? true : false;
                    var tags = GetText(IllustTagsHtml).Trim().Trim('#').Split('#');
                    if (shell)
                        Commands.ShellOpenPixivPedia.Execute(tags);
                    else
                        Commands.OpenPedia.Execute(tags);
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception) { }
#endif
        }

        private void IllustInfo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(e.ChangedButton == MouseButton.Middle)
            {
                ActionIllustInfo_Click(sender, e);
            }
            else if (e.ClickCount == 2)
            {
                ActionIllustInfo_Click(sender, e);
            }
            else if (e.ClickCount == 1)
            {
                ActionSpeech_Click(sender, e);
            }
            e.Handled = true;
        }

        private void IllustDownloaded_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (IllustDownloaded.Tag is string)
                {
                    var fp = IllustDownloaded.Tag as string;
                    fp.OpenFileWithShell();
                }
            }
            catch (Exception) { }
        }

        private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    if (e.ClickCount >= 2)
                    {
                        if (SubIllusts.Items.Count() <= 0)
                        {
                            if (Contents is ImageItem)
                            {
                                Commands.OpenWorkPreview.Execute(Contents);
                            }
                        }
                        else
                        {
                            if (SubIllusts.SelectedItems == null || SubIllusts.SelectedItems.Count <= 0)
                                SubIllusts.SelectedIndex = 0;
                            Commands.Open.Execute(SubIllusts);
                        }
                        e.Handled = true;
                    }
                    else if (IsElement(btnSubPagePrev, e) && btnSubPagePrev.IsVisible && btnSubPagePrev.IsEnabled)
                    {
                        SubPageNav_Clicked(btnSubPagePrev, e);
                        e.Handled = true;
                    }
                    else if (IsElement(btnSubPageNext, e) && btnSubPageNext.IsVisible && btnSubPageNext.IsEnabled)
                    {
                        SubPageNav_Clicked(btnSubPageNext, e);
                        e.Handled = true;
                    }
                }
                else if (e.XButton1 == MouseButtonState.Pressed && SubIllusts.Items.Count > 0)
                {
                    SubPageNav_Clicked(btnSubPageNext, e);
                    e.Handled = true;
                }
                else if (e.XButton2 == MouseButtonState.Pressed && SubIllusts.Items.Count > 0)
                {
                    SubPageNav_Clicked(btnSubPagePrev, e);
                    e.Handled = true;
                }
            }
            catch (Exception) { }
        }

        private void IllustTagExpander_Expanded(object sender, RoutedEventArgs e)
        {
            AdjustBrowserSize(IllustTagsHtml);
        }

        private void IllustDescExpander_Expanded(object sender, RoutedEventArgs e)
        {
            AdjustBrowserSize(IllustDescHtml);
        }
        #endregion

        #region Illust Actions
        private async void ActionIllustInfo_Click(object sender, RoutedEventArgs e)
        {
            UpdateLikeState();

            if (sender == ActionCopyIllustTitle || sender == IllustTitle)
            {
                if (Keyboard.Modifiers == ModifierKeys.None)
                    Commands.CopyText.Execute($"{IllustTitle.Text}");
                else
                    Commands.CopyText.Execute($"title:{IllustTitle.Text}");
            }
            else if (sender == ActionCopyIllustAuthor || sender == IllustAuthor)
            {
                if (Keyboard.Modifiers == ModifierKeys.None)
                    Commands.CopyText.Execute($"{IllustAuthor.Text}");
                else
                    Commands.CopyText.Execute($"user:{IllustAuthor.Text}");
            }
            else if (sender == ActionCopyAuthorID)
            {
                if (Contents is ImageItem)
                {
                    Commands.CopyUserIDs.Execute(Contents);
                }
            }
            else if (sender == ActionCopyIllustID || sender == PreviewCopyIllustID)
            {
                if (Contents is ImageItem)
                {
                    Commands.CopyIllustIDs.Execute(Contents);
                }
            }
            else if (sender == PreviewCopyImage)
            {
                if (!string.IsNullOrEmpty(PreviewImageUrl))
                {
                    Commands.CopyImage.Execute(PreviewImageUrl.GetImageCachePath());
                }
            }
            else if (sender == ActionCopyIllustDate || sender == IllustDate)
            {
                Commands.CopyText.Execute(ActionCopyIllustDate.Header);
            }
            else if (sender == ActionIllustWebPage)
            {
                if (Contents is ImageItem)
                {
                    if (Contents.Illust is Pixeez.Objects.Work)
                    {
                        var href = Contents.ID.ArtworkLink();
                        href.OpenUrlWithShell();
                    }
                }
            }
            else if (sender == ActionIllustNewWindow)
            {
                if (Contents is ImageItem)
                {
                    if (Contents.Illust is Pixeez.Objects.Work)
                    {
                        await new Action(() =>
                        {
                            Commands.Open.Execute(Contents.Illust);
                        }).InvokeAsync();
                    }
                }
            }
            else if (sender == ActionIllustWebLink)
            {
                if (Contents is ImageItem)
                {
                    if (Contents.Illust is Pixeez.Objects.Work)
                    {
                        var href = Contents.ID.ArtworkLink();
                        Commands.CopyText.Execute(href);
                    }
                }
            }
            else if (sender == ActionSendIllustToInstance)
            {
                if (Contents is ImageItem)
                {
                    if (Contents.Illust is Pixeez.Objects.Work)
                    {
                        await new Action(() =>
                        {
                            if (Keyboard.Modifiers == ModifierKeys.None)
                                Commands.SendToOtherInstance.Execute(Contents);
                            else
                                Commands.ShellSendToOtherInstance.Execute(Contents);
                        }).InvokeAsync();
                    }
                }
            }
            else if (sender == ActionSendAuthorToInstance)
            {
                if (Contents is ImageItem)
                {
                    if (Contents.Illust is Pixeez.Objects.Work)
                    {
                        await new Action(() =>
                        {
                            if (Keyboard.Modifiers == ModifierKeys.None)
                                Commands.SendToOtherInstance.Execute($"uid:{Contents.UserID}");
                            else
                                Commands.ShellSendToOtherInstance.Execute($"uid:{Contents.UserID}");
                        }).InvokeAsync();
                    }
                }
            }
            e.Handled = true;
        }

        private void ActionIllustAuthourInfo_Click(object sender, RoutedEventArgs e)
        {
            if (sender == ActionIllustAuthorInfo || sender == btnAuthorInfo)
            {
                if (Contents is ImageItem)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        Commands.ShellSendToOtherInstance.Execute(Contents.User);
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        ActionRefreshAvator(Contents);
                    else
                        Commands.OpenUser.Execute(Contents.User);
                }
            }
            else if (sender == ActionIllustAuthorFollowing)
            {

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
                    IllustDownloaded.Visibility = Contents.IsDownloadedVisibility;
                    Commands.OpenDownloaded.Execute(Contents);
                }
                else
                { 
                    Commands.OpenDownloaded.Execute(SubIllusts);
                }
            }
            else if (sender == PreviewOpen)
            {
                if (SubIllusts.Items.Count() <= 0)
                {
                    if (Contents is ImageItem)
                    {
                        IllustDownloaded.Visibility = Contents.IsDownloadedVisibility;
                        Commands.OpenWorkPreview.Execute(Contents);
                    }
                }
                else
                {
                    if (SubIllusts.SelectedItems == null || SubIllusts.SelectedItems.Count <= 0)
                        SubIllusts.SelectedIndex = 0;
                    IllustDownloaded.Visibility = Contents.IsDownloadedVisibility;
                    Commands.OpenWorkPreview.Execute(SubIllusts);
                }
            }
        }

        private void ActionRefreshPreview_Click(object sender, RoutedEventArgs e)
        {
            if (Contents is ImageItem)
            {
                PreviewWait.Show();
                var ua = new Action(async () =>
                {
                    try
                    {
                        var idx = -1;
                        var illust = Contents;
                        var item = Contents;
                        if (SubIllusts.SelectedItem is ImageItem)
                        {
                            idx = SubIllusts.SelectedIndex;
                            item = SubIllusts.SelectedItem as ImageItem;
                            Contents.Index = item.Index;
                        }

                        lastSelectionItem = item;
                        lastSelectionChanged = DateTime.Now.ToFileTime();

                        PreviewImageUrl = item.Illust.GetPreviewUrl(item.Index);
                        var img = await PreviewImageUrl.LoadImageFromUrl();
                        if (img == null || img.Width < 360)
                        {
                            PreviewImageUrl = item.Illust.GetPreviewUrl(item.Index, true);
                            var large = await PreviewImageUrl.LoadImageFromUrl();
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
                                if (Contents.Illust.Id.IsSameIllust(img.GetHashCode()) || Contents.IsSameIllust(img.GetHashCode()))
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
                 try
                 {
                     IllustAuthorAvator.Source = await item.User.GetAvatarUrl().LoadImageFromUrl();
                     if (IllustAuthorAvator.Source != null) IllustAuthorAvatorWait.Hide();
                 }
                 catch(Exception) { }
            }).InvokeAsync();
        }

        private void ActionRefreshAvator(Pixeez.Objects.UserBase user)
        {
            var ua = new Action(async () =>
            {
                try
                {
                    IllustAuthorAvator.Source = await user.GetAvatarUrl().LoadImageFromUrl();
                    if (IllustAuthorAvator.Source != null) IllustAuthorAvatorWait.Hide();
                }
                catch(Exception) { }
            }).InvokeAsync();
        }
        #endregion

        #region Following User / Bookmark Illust routines
        private void IllustActions_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
            UpdateLikeState();
        }

        private void ActionIllust_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            bool is_user = false;

            if (sender == BookmarkIllust)
            {
                BookmarkIllust.ContextMenu.IsOpen = true;
                is_user = true;
            }
            else if (sender == FollowAuthor)
            {
                FollowAuthor.ContextMenu.IsOpen = true;
                is_user = true;
            }
            else if (sender == IllustActions)
            {
                if (Window.GetWindow(this) is ContentWindow)
                    ActionIllustNewWindow.Visibility = Visibility.Collapsed;
                else
                    ActionIllustNewWindow.Visibility = Visibility.Visible;
                IllustActions.ContextMenu.IsOpen = true;
            }
            UpdateLikeState(-1, is_user);
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
                if (host == RelativeIllusts || host == RelativeIllustsExpander) items = RelativeIllusts.GetSelected();
                else if (host == FavoriteIllusts || host == FavoriteIllustsExpander) items = FavoriteIllusts.GetSelected();
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
                if (Contents is ImageItem)
                {
                    var item = Contents;
                    var result = false;
                    try
                    {
                        if (sender == ActionBookmarkIllustPublic)
                        {
                            result = await item.Illust.Like();
                        }
                        else if (sender == ActionBookmarkIllustPrivate)
                        {
                            result = await item.Illust.Like(false);
                        }
                        else if (sender == ActionBookmarkIllustRemove)
                        {
                            result = await item.Illust.UnLike();
                        }

                        if (item.IsSameIllust(Contents))
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
                if (host == RelativeIllusts || host == RelativeIllustsExpander) items = RelativeIllusts.GetSelected();
                else if (host == FavoriteIllusts || host == FavoriteIllustsExpander) items = FavoriteIllusts.GetSelected();
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
                if (Contents is ImageItem)
                {
                    var item = Contents;
                    var result = false;
                    try
                    {
                        if (sender == ActionFollowAuthorPublic)
                        {
                            result = await item.User.Like();
                        }
                        else if (sender == ActionFollowAuthorPrivate)
                        {
                            result = await item.User.Like(false);
                        }
                        else if (sender == ActionFollowAuthorRemove)
                        {
                            result = await item.User.UnLike();
                        }

                        if (item.IsSameIllust(Contents))
                        {
                            FollowAuthor.Tag = result ? PackIconModernKind.Check : PackIconModernKind.Add;
                            ActionFollowAuthorRemove.IsEnabled = result;
                            if (item.ItemType == ImageItemType.User) item.IsFavorited = result;
                        }
                    }
                    catch (Exception) { }
                }
            }
        }
        #endregion

        #region Illust Multi-Pages related routines
        private void SubIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Contents is ImageItem)
            {
                if (SubIllusts.Items.Count() <= 0)
                    ShowIllustPagesAsync(Contents);
                else
                    SubIllusts.UpdateTilesImage();
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
            $"TimeDelta:{DateTime.Now.ToFileTime() - lastSelectionChanged}, {sender}, {e.Handled}, {e.RoutedEvent}, {e.OriginalSource}, {e.Source}".DEBUG();
#endif
            e.Handled = false;

            if (SubIllusts.SelectedItem is ImageItem && SubIllusts.SelectedItems.Count == 1)
            {
                if (DateTime.Now.ToFileTime() - lastSelectionChanged < 500000)
                {
                    SubIllusts.SelectedItem = lastSelectionItem;
                    return;
                }
                SubIllusts.UpdateTilesState(false);
                UpdateDownloadedMark(SubIllusts.SelectedItem);

                if (Contents is ImageItem)
                {
                    Contents.IsDownloaded = Contents.Illust.IsPartDownloadedAsync();
                    int idx = -1;
                    int.TryParse(SubIllusts.SelectedItem.BadgeValue, out idx);
                    Contents.Index = idx - 1;
                }
                UpdateLikeState();
                e.Handled = true;

                UpdateSubPageNav();

                ActionRefreshPreview_Click(sender, e);
            }
        }

        private void SubIllusts_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void SubIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Commands.Open.Execute(SubIllusts);
        }

        private void SubIllusts_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Commands.Open.Execute(SubIllusts);
            }
        }

        private void SubIllustPagesNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender == btnSubIllustPrevPages || sender == btnSubIllustNextPages)
            {
                var btn = sender as Button;
                if (Contents is ImageItem)
                {
                    var illust = Contents.Illust;
                    if (illust is Pixeez.Objects.Work)
                    {
                        var pageNum = SubIllusts.Tag is int ? (int)(SubIllusts.Tag) : 0;
                        var index = 0;
                        if (btn == btnSubIllustPrevPages)
                        {
                            pageNum -= 1;
                            index = 29;
                        }
                        else if (btn == btnSubIllustNextPages)
                        {
                            pageNum += 1;
                        }
                        SubIllusts.Tag = pageNum;

                        ShowIllustPagesAsync(Contents, index);
                    }
                }
            }
        }

        private void ActionSaveIllust_Click(object sender, RoutedEventArgs e)
        {
            if (sender == PreviewSave)
            {
                Commands.SaveIllust.Execute(Contents);
            }
            else if (SubIllusts.SelectedItems != null && SubIllusts.SelectedItems.Count > 0)
            {
                Commands.SaveIllust.Execute(SubIllusts);
            }
            else if (SubIllusts.SelectedItem is ImageItem)
            {
                var item = SubIllusts.SelectedItem;
                Commands.SaveIllust.Execute(item);
            }
            else if (Contents is ImageItem)
            {
                Commands.SaveIllust.Execute(Contents);
            }
        }

        private async void ActionSaveAllIllust_Click(object sender, RoutedEventArgs e)
        {
            Pixeez.Objects.Work illust = Contents is ImageItem && Contents.ItemType != ImageItemType.User ? Contents.Illust : null;

            if (illust != null)
            {
                var dt = illust.GetDateTime();

                if (illust is Pixeez.Objects.IllustWork)
                {
                    var illustset = illust as Pixeez.Objects.IllustWork;
                    var is_meta_single_page = illust.PageCount==1 ? true : false;
                    foreach (var pages in illustset.meta_pages)
                    {
                        var url = pages.GetOriginalUrl();
                        url.SaveImage(pages.GetThumbnailUrl(), dt, is_meta_single_page);
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
            }
        }

        private void SubPageNav_Clicked(object sender, RoutedEventArgs e)
        {
            var count = SubIllusts.Items.Count;
            if (count >= 1)
            {
                if (sender == btnSubPagePrev)
                {
                    if (SubIllusts.SelectedIndex > 0)
                        SubIllusts.SelectedIndex -= 1;
                    else if (SubIllusts.SelectedIndex == 0 && btnSubIllustPrevPages.IsShown())
                    {
                        SubIllustPagesNav_Click(btnSubIllustPrevPages, e);
                    }
                }
                else if (sender == btnSubPageNext)
                {
                    if (SubIllusts.SelectedIndex <= 0)
                        SubIllusts.SelectedIndex = 1;
                    else if (SubIllusts.SelectedIndex < count - 1 && count > 1)
                        SubIllusts.SelectedIndex += 1;
                    else if (SubIllusts.SelectedIndex == count - 1 && btnSubIllustNextPages.IsShown())
                    {
                        SubIllustPagesNav_Click(btnSubIllustNextPages, e);
                    }
                }
                if (SubIllusts.SelectedItem is ImageItem) SubIllusts.SelectedItem.Focus();
            }
        }
        #endregion

        #region Relative Panel related routines
        private void RelativeIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Contents is ImageItem)
            {
                if(Contents.ItemType != ImageItemType.User)
                    ShowRelativeInlineAsync(Contents);
                else if(Contents.ItemType == ImageItemType.User)
                    ShowUserWorksInlineAsync(Contents.User);
            }
        }

        private void RelativeIllustsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            IllustDetailWait.Hide();
        }

        private void ActionOpenRelative_Click(object sender, RoutedEventArgs e)
        {
            Commands.Open.Execute(RelativeIllusts);
        }

        private void ActionCopyRelativeIllustID_Click(object sender, RoutedEventArgs e)
        {
            Commands.CopyIllustIDs.Execute(RelativeIllusts);
        }

        private void RelativeIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = false;
            RelativeIllusts.UpdateTilesState();
            UpdateLikeState();
            if (RelativeIllusts.SelectedItem is ImageItem) RelativeIllusts.SelectedItem.Focus();
            e.Handled = true;
        }

        private void RelativeIllusts_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void RelativeIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Commands.Open.Execute(RelativeIllusts);
        }

        private void RelativeIllusts_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Commands.Open.Execute(RelativeIllusts);
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

            if (Contents is ImageItem)
            {
                if (Contents.ItemType != ImageItemType.User)
                    ShowRelativeInlineAsync(Contents, next_url);
                else if (Contents.ItemType == ImageItemType.User)
                    ShowUserWorksInlineAsync(Contents.User, next_url);
            }
        }
        #endregion

        #region Author Favorite routines
        private void FavoriteIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Contents is ImageItem)
            {
                ShowFavoriteInlineAsync(Contents.User);
            }
        }

        private void FavoriteIllustsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            FavoriteIllustsExpander.Header = "Favorite";
            IllustDetailWait.Hide();
        }

        private void ActionOpenFavorite_Click(object sender, RoutedEventArgs e)
        {
            Commands.Open.Execute(FavoriteIllusts);
        }

        private void ActionCopyFavoriteIllustID_Click(object sender, RoutedEventArgs e)
        {
            Commands.CopyIllustIDs.Execute(FavoriteIllusts);
        }

        private void FavriteIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = false;
            FavoriteIllusts.UpdateTilesState();
            UpdateLikeState();
            if (FavoriteIllusts.SelectedItem is ImageItem) FavoriteIllusts.SelectedItem.Focus();
            e.Handled = true;
        }

        private void FavriteIllusts_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void FavriteIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Commands.Open.Execute(FavoriteIllusts);
        }

        private void FavriteIllusts_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Commands.Open.Execute(FavoriteIllusts);
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

            if (Contents is ImageItem)
            {
                ShowFavoriteInlineAsync(Contents.User, next_url);
            }
        }
        #endregion

        #region Illust Comments related routines
        private async void CommentsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (Contents is ImageItem && Contents.ItemType != ImageItemType.User)
            {
                IllustDetailWait.Show();

                //ShowCommentsInline(tokens, Contents);
                //CommentsViewer.Language = System.Windows.Markup.XmlLanguage.GetLanguage("zh");
                CommentsViewer.NavigateToString("about:blank");

                //.Document = string.Empty;
                var result = await tokens.GetIllustComments(Contents.ID, "0", true);
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
                    var count = Contents .Count;
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
            try
            {
                if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
                {
                    var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                    if (host == SubIllustsExpander || host == SubIllusts)
                    {
                        if (Contents is ImageItem)
                        {
                            Commands.CopyIllustIDs.Execute(Contents);
                        }
                    }
                    else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                    {
                        Commands.CopyIllustIDs.Execute(RelativeIllusts);
                    }
                    else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                    {
                        Commands.CopyIllustIDs.Execute(FavoriteIllusts);
                    }
                    else if (host == CommentsExpander || host == CommentsViewer)
                    {

                    }
                }
            }
            catch (Exception) { }
        }

        private void ActionOpenSelectedIllust_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
                {
                    var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                    if (host == SubIllustsExpander || host == SubIllusts || host == PreviewBox)
                    {
                        Commands.Open.Execute(SubIllusts);
                    }
                    else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                    {
                        Commands.Open.Execute(RelativeIllusts);
                    }
                    else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                    {
                        Commands.Open.Execute(FavoriteIllusts);
                    }
                    else if (host == CommentsExpander || host == CommentsViewer)
                    {

                    }
                }
            }
            catch (Exception) { }
        }

        private void ActionSendToOtherInstance_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
                {
                    var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                    var uid = (sender as MenuItem).Uid;
                    if (host == SubIllustsExpander || host == SubIllusts || host == PreviewBox)
                    {
                        if (sender == PreviewSendIllustToInstance || uid.Equals("ActionSendIllustToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (Contents is ImageItem)
                            {
                                if (Keyboard.Modifiers == ModifierKeys.None)
                                    Commands.SendToOtherInstance.Execute(Contents);
                                else
                                    Commands.ShellSendToOtherInstance.Execute(Contents);
                            }
                        }
                        else if (sender == PreviewSendAuthorToInstance || uid.Equals("ActionSendAuthorToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (Contents is ImageItem)
                            {
                                var id = $"uid:{Contents.UserID}";
                                if (Keyboard.Modifiers == ModifierKeys.None)
                                    Commands.SendToOtherInstance.Execute(id);
                                else
                                    Commands.ShellSendToOtherInstance.Execute(id);
                            }
                        }
                    }
                    else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                    {
                        if (uid.Equals("ActionSendIllustToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (Keyboard.Modifiers == ModifierKeys.None)
                                Commands.SendToOtherInstance.Execute(RelativeIllusts);
                            else
                                Commands.ShellSendToOtherInstance.Execute(RelativeIllusts);
                        }
                        else if (uid.Equals("ActionSendAuthorToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var ids = new List<string>();
                            foreach (var item in RelativeIllusts.GetSelected())
                            {
                                var id = $"uid:{item.UserID}";
                                if (!ids.Contains(id)) ids.Add(id);
                            }

                            if (Keyboard.Modifiers == ModifierKeys.None)
                                Commands.SendToOtherInstance.Execute(ids);
                            else
                                Commands.ShellSendToOtherInstance.Execute(ids);
                        }
                    }
                    else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                    {
                        if (uid.Equals("ActionSendIllustToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (Keyboard.Modifiers == ModifierKeys.None)
                                Commands.SendToOtherInstance.Execute(FavoriteIllusts);
                            else
                                Commands.ShellSendToOtherInstance.Execute(FavoriteIllusts);
                        }
                        else if (uid.Equals("ActionSendAuthorToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var ids = new List<string>();
                            foreach (var item in FavoriteIllusts.GetSelected())
                            {
                                var id = $"uid:{item.UserID}";
                                if (!ids.Contains(id)) ids.Add(id);
                            }

                            if (Keyboard.Modifiers == ModifierKeys.None)
                                Commands.SendToOtherInstance.Execute(ids);
                            else
                                Commands.ShellSendToOtherInstance.Execute(ids);
                        }
                    }
                    else if (host == CommentsExpander || host == CommentsViewer)
                    {

                    }
                }
            }
            catch (Exception) { }
        }

        private void ActionPrevPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
                {
                    var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                    if (host == SubIllustsExpander || host == SubIllusts)
                    {
                        SubIllustPagesNav_Click(btnSubIllustPrevPages, e);
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
            catch (Exception) { }
        }

        private void ActionNextPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
                {
                    var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                    if (host == SubIllustsExpander || host == SubIllusts)
                    {
                        SubIllustPagesNav_Click(btnSubIllustNextPages, e);
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
            catch (Exception) { }
        }

        private void ActionSaveIllusts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem)
                {
                    var m = sender as MenuItem;
                    var host = (m.Parent as ContextMenu).PlacementTarget;
                    if (m.Uid.Equals("ActionSaveIllusts", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            foreach (ImageItem item in SubIllusts.GetSelected())
                            {
                                Commands.SaveIllust.Execute(item);
                            }
                        }
                        else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                        {
                            foreach (ImageItem item in RelativeIllusts.GetSelected())
                            {
                                Commands.SaveIllust.Execute(item);
                            }
                        }
                        else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                        {
                            foreach (ImageItem item in FavoriteIllusts.GetSelected())
                            {
                                Commands.SaveIllust.Execute(item);
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private void ActionSaveIllustsAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem)
                {
                    var m = sender as MenuItem;
                    var host = (m.Parent as ContextMenu).PlacementTarget;
                    if (m.Uid.Equals("ActionSaveIllustsAll", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            Commands.SaveIllustAll.Execute(Contents);
                        }
                        else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                        {
                            foreach (ImageItem item in RelativeIllusts.GetSelected())
                            {
                                Commands.SaveIllustAll.Execute(item);
                            }
                        }
                        else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                        {
                            foreach (ImageItem item in FavoriteIllusts.GetSelected())
                            {
                                Commands.SaveIllustAll.Execute(item);
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private void ActionOpenDownloaded_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem)
                {
                    var m = sender as MenuItem;
                    var host = (m.Parent as ContextMenu).PlacementTarget;
                    if (m.Uid.Equals("ActionOpenDownloaded", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            foreach (ImageItem item in SubIllusts.GetSelected())
                            {
                                Commands.OpenDownloaded.Execute(item);
                            }
                        }
                        else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                        {
                            foreach (ImageItem item in RelativeIllusts.GetSelected())
                            {
                                Commands.OpenDownloaded.Execute(item);
                            }
                        }
                        else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                        {
                            foreach (ImageItem item in FavoriteIllusts.GetSelected())
                            {
                                Commands.OpenDownloaded.Execute(item);
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private void ActionRefreshIllusts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem)
                {
                    var m = sender as MenuItem;
                    var host = (m.Parent as ContextMenu).PlacementTarget;
                    if (m.Uid.Equals("ActionRefresh", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            if (Contents is ImageItem)
                            {
                                ShowIllustPagesAsync(Contents, 0, SubIllusts.Tag is int ? (int)(SubIllusts.Tag) : 0);
                            }
                        }
                        else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                        {
                            if (Contents is ImageItem)
                            {
                                var next_url = RelativeIllustsExpander.Tag is string ? RelativeIllustsExpander.Tag as string : string.Empty;
                                if (Contents.ItemType != ImageItemType.User)
                                    ShowRelativeInlineAsync(Contents, next_url);
                                else if (Contents.ItemType == ImageItemType.User)
                                    ShowUserWorksInlineAsync(Contents.User, next_url);
                            }
                        }
                        else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                        {
                            if (Contents is ImageItem)
                            {
                                var next_url = FavoriteIllustsExpander.Tag is string ? FavoriteIllustsExpander.Tag as string : string.Empty;
                                ShowFavoriteInlineAsync(Contents.User, next_url);
                            }
                        }
                    }
                    else if (m.Uid.Equals("ActionRefreshThumb", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            SubIllusts.UpdateTilesImage();
                        }
                        else if (host == RelativeIllustsExpander || host == RelativeIllusts)
                        {
                            RelativeIllusts.UpdateTilesImage();
                        }
                        else if (host == FavoriteIllustsExpander || host == FavoriteIllusts)
                        {
                            FavoriteIllusts.UpdateTilesImage();
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        #endregion

    }

}
