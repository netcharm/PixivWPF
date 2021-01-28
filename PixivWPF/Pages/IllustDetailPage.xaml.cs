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

using MahApps.Metro.IconPacks;
using PixivWPF.Common;
using System.Windows.Interop;
using System.Windows.Controls.Primitives;
using System.Windows.Shapes;

namespace PixivWPF.Pages
{
    /// <summary>
    /// IllustDetailPage.xaml 的交互逻辑
    /// </summary>
    public partial class IllustDetailPage : Page
    {
        private Setting setting = Application.Current.LoadSetting();

        public Window ParentWindow { get { return (Application.Current.GetActiveWindow()); } }
        public PixivItem Contents { get; set; } = null;

        private Popup PreviewPopup = null;
        private Rectangle PreviewPopupBackground = null;
        private IList<Button> PreviewPopupToolButtons = new List<Button>();
        private System.Timers.Timer PreviewPopupTimer = null;
        private void InitTaskTimer(ref System.Timers.Timer timer)
        {
            if (timer == null)
            {
                timer = new System.Timers.Timer(5000) { AutoReset = true, Enabled = false };
                timer.Elapsed += PreviewPopupTimer_Elapsed;
            }
        }

        private const int PAGE_ITEMS = 30;
        private int page_count = 0;
        private int page_number = 0;
        private int page_index = 0;

        private string PreviewImageUrl { get; set; } = string.Empty;
        private string PreviewImagePath { get; set; } = string.Empty;
        private string AvatarImageUrl { get; set; } = string.Empty;

        private string CurrentRelativeURL = string.Empty;
        private string NextRelativeURL = string.Empty;

        private string CurrentFavoriteURL = string.Empty;
        private string NextFavoriteURL = string.Empty;

        #region Popup Helper
        private void PopupOpen(Popup popup)
        {
            try
            {
                if (PreviewPopupBackground is Rectangle) PreviewPopupBackground.Fill = Theme.MenuBackgroundBrush;
                foreach (var button in PreviewPopupToolButtons) button.Foreground = Theme.AccentBrush;
                PreviewPopup.IsOpen = true;
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private void PopupOpen(ContextMenu menu)
        {
            try
            {
                menu.Background = Theme.MenuBackgroundBrush;
                var items = menu.GetChildren<MenuItem>();
                foreach (var item in items)
                {
                    item.Background = Theme.MenuBackgroundBrush;
                    item.Foreground = Theme.TextBrush;
                }
                //var seps = menu.GetChildren<Separator>();
                //foreach (var sep in seps)
                //{
                //    sep.Background = Theme.MenuBackgroundBrush;
                //    sep.Foreground = Theme.SeparatorBrush;
                //}
                menu.IsOpen = true;
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }
        #endregion

        #region WebBrowser Helper
        private bool bCancel = false;
        private WindowsFormsHostEx tagsHost;
        private WindowsFormsHostEx descHost;
        private WindowsFormsHostEx commentsHost;
        private WebBrowserEx IllustDescHtml;
        private WebBrowserEx IllustTagsHtml;
        private WebBrowserEx IllustCommentsHtml;

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
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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

        private System.Windows.Forms.Integration.WindowsFormsHost GetHtmlHost(System.Windows.Forms.WebBrowser browser)
        {
            System.Windows.Forms.Integration.WindowsFormsHost result = null;
            try
            {
                if (browser == IllustDescHtml)
                    result = descHost;
                else if (browser == IllustTagsHtml)
                    result = tagsHost;
                else if (browser == IllustCommentsHtml)
                    result = commentsHost;
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
            return (result);
        }

        private async void AdjustBrowserSize(System.Windows.Forms.WebBrowser browser)
        {
            try
            {
                if (browser is System.Windows.Forms.WebBrowser)
                {
                    await new Action(async () =>
                    {
                        try
                        {
                            int h_min = 96;
                            int h_max = 480;

                            var host = GetHtmlHost(browser);
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
                        catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
                    }).InvokeAsync();
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private string MakeIllustTagsHtml(PixivItem item)
        {
            var result = string.Empty;
            try
            {
                if (item.IsWork())
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
                                html.AppendLine($"<a href=\"https://www.pixiv.net/tags/{Uri.EscapeDataString(tag.Original)}/artworks?s_mode=s_tag\" class=\"tag\" title=\"{trans}\" data-tag=\"{tag.Original}\" data-trans=\"{tag.Translated}\" data-tooltip=\"{trans}\">#{tag.Original}</a>");
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
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
            return (result);
        }

        private string MakeIllustDescHtml(PixivItem item)
        {
            var result = string.Empty;
            try
            {
                if (item.IsWork())
                {
                    var contents = item.Illust.Caption.HtmlDecode();
                    contents = $"<div class=\"desc\">{Environment.NewLine}{contents.Trim()}{Environment.NewLine}</div>";
                    result = contents.GetHtmlFromTemplate(item.Illust.Title);
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
            return (result);
        }

        private string MakeUserDescHtml(Pixeez.Objects.UserInfo info)
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
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
            return (result);
        }

        private void InitHtmlRenderHost(out WindowsFormsHostEx host, System.Windows.Forms.WebBrowser browser, Panel panel)
        {
            try
            {
                host = new WindowsFormsHostEx()
                {
                    //CompositionMode = ,
                    AllowDrop = false,
                    MinHeight = 24,
                    MaxHeight = 480,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Child = browser
                };
                if (panel is Panel)
                {
                    panel.Children.Add(host);
                    AdjustBrowserSize(browser);
                }
            }
            catch (Exception) { host = null; }
        }

        private void InitHtmlRenderHost(out System.Windows.Forms.Integration.WindowsFormsHost host, System.Windows.Forms.WebBrowser browser, Panel panel)
        {
            try
            {
                host = new System.Windows.Forms.Integration.WindowsFormsHost()
                {
                    //CompositionMode = ,
                    AllowDrop = false,
                    MinHeight = 24,
                    MaxHeight = 480,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Child = browser
                };
                if (panel is Panel)
                {
                    panel.Children.Add(host);
                    AdjustBrowserSize(browser);
                }
            }
            catch (Exception) { host = null; }
        }

        private void InitHtmlRender(out WebBrowserEx browser)
        {
            browser = null;
            try
            {
                browser = new WebBrowserEx()
                {
                    DocumentText = string.Empty.GetHtmlFromTemplate(),
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    ScriptErrorsSuppressed = true,
                    IgnoreAllError = true,
                    WebBrowserShortcutsEnabled = false,
                    AllowNavigation = true,
                    AllowWebBrowserDrop = false
                };
                browser.Navigate("about:blank");
                browser.Document.Write(string.Empty);

                if (browser is System.Windows.Forms.WebBrowser)
                {
                    browser.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(WebBrowser_DocumentCompleted);
                    browser.Navigating += new System.Windows.Forms.WebBrowserNavigatingEventHandler(WebBrowser_Navigating);
                    browser.Navigated += new System.Windows.Forms.WebBrowserNavigatedEventHandler(WebBrowser_Navigated);
                    browser.ProgressChanged += new System.Windows.Forms.WebBrowserProgressChangedEventHandler(WebBrowser_ProgressChanged);
                    browser.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(WebBrowser_PreviewKeyDown);
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private void CreateHtmlRender()
        {
            try
            {
                InitHtmlRender(out IllustTagsHtml);
                InitHtmlRenderHost(out tagsHost, IllustTagsHtml, IllustTagsHost);
                InitHtmlRender(out IllustDescHtml);
                InitHtmlRenderHost(out descHost, IllustDescHtml, IllustDescHost);
                InitHtmlRender(out IllustCommentsHtml);
                InitHtmlRenderHost(out commentsHost, IllustCommentsHtml, IllustCommentsHost);

                UpdateTheme();
                UpdateLayout();
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private void DeleteHtmlRender()
        {
            try
            {
                var hosts = new System.Windows.Forms.Integration.WindowsFormsHost[] { tagsHost, descHost, commentsHost };
                var wbs = new WebBrowserEx[] { IllustTagsHtml, IllustDescHtml, IllustCommentsHtml };
                for (var i = 0; i < wbs.Length; i++)
                {
                    if (wbs[i] is WebBrowserEx)
                    {
                        wbs[i].DocumentCompleted -= new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(WebBrowser_DocumentCompleted);
                        wbs[i].Navigating -= new System.Windows.Forms.WebBrowserNavigatingEventHandler(WebBrowser_Navigating);
                        wbs[i].Navigated -= new System.Windows.Forms.WebBrowserNavigatedEventHandler(WebBrowser_Navigated);
                        wbs[i].ProgressChanged -= new System.Windows.Forms.WebBrowserProgressChangedEventHandler(WebBrowser_ProgressChanged);
                        wbs[i].PreviewKeyDown -= new System.Windows.Forms.PreviewKeyDownEventHandler(WebBrowser_PreviewKeyDown);

                        wbs[i].Dispose(true);
                        wbs[i] = null;
                    }
                }
                for (var i = 0; i < hosts.Length; i++)
                {
                    if (hosts[i] is WindowsFormsHostEx)
                    {
                        hosts[i].Dispose();
                        hosts[i] = null;
                    }
                }
            }
            catch { }
        }

        private async void RefreshHtmlRender(System.Windows.Forms.WebBrowser browser)
        {
            try
            {
                if (browser is System.Windows.Forms.WebBrowser)
                {
                    await new Action(() =>
                    {
                        var contents = string.Empty;
                        if (browser == IllustTagsHtml)
                        {
                            if (Contents.IsUser())
                                contents = MakeUserInfoHtml(UserInfo);
                            else if (Contents.IsWork())
                                contents = MakeIllustTagsHtml(Contents);
                        }
                        else if (browser == IllustDescHtml)
                        {
                            if (Contents.IsUser())
                                contents = MakeUserDescHtml(UserInfo);
                            else if (Contents.IsWork())
                                contents = MakeIllustDescHtml(Contents);
                        }
                        if (!string.IsNullOrEmpty(contents))
                        {
                            browser.DocumentText = contents;
                            browser.Document.Write(string.Empty);
                            AdjustBrowserSize(browser);
                        }
                        browser.WebBrowserShortcutsEnabled = false;
                    }).InvokeAsync();
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }
        #endregion

        #region Illust/User info relative methods
        public void UpdateIllustTags()
        {
            try
            {
                if (Contents is PixivItem && Contents.IsWork())
                {
                    IllustTitle.ToolTip = IllustTitle.Text.TranslatedTag();
                    RefreshHtmlRender(IllustTagsHtml);
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        public void UpdateIllustDesc()
        {
            try
            {
                RefreshHtmlRender(IllustDescHtml);
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        public void UpdateWebContent()
        {
            try
            {
                RefreshHtmlRender(IllustTagsHtml);
                RefreshHtmlRender(IllustDescHtml);
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }
        #endregion

        #region Illust/User state mark methods
        private void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            try
            {
                foreach (var illusts in new List<ImageListGrid>() { SubIllusts, RelativeItems, FavoriteItems })
                {
                    illusts.UpdateDownloadStateAsync(illustid, exists);
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        public async void UpdateDownloadStateAsync(int? illustid = null, bool? exists = null)
        {
            try
            {
                if (Contents is PixivItem)
                {
                    UpdateDownloadedMark(Contents);
                    SubIllusts.UpdateTilesState(false);
                }

                await Task.Run(() =>
                {
                    UpdateDownloadState(illustid, exists);
                });
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private void UpdateDownloadedMark()
        {
            try
            {
                if (Contents is PixivItem)
                {
                    UpdateDownloadedMark(Contents);
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private void UpdateDownloadedMark(PixivItem item, bool? exists = null)
        {
            try
            {
                if (item is PixivItem)
                {
                    string fp = string.Empty;
                    var index = item.Index;
                    if (index < 0)
                    {
                        var download = item.Illust.IsPartDownloadedAsync(out fp);
                        if (item.IsDownloaded != download) item.IsDownloaded = download;
                        if (item.IsDownloaded)
                        {
                            IllustDownloaded.Show();
                            IllustDownloaded.Tag = fp;
                            IllustDownloaded.ToolTip = fp;
                            //ToolTipService.SetToolTip(IllustDownloaded, fp);
                        }
                        else
                        {
                            IllustDownloaded.Hide();
                            IllustDownloaded.Tag = null;
                            IllustDownloaded.ToolTip = string.Empty;
                            //ToolTipService.SetToolTip(IllustDownloaded, null);
                        }
                    }
                    else
                    {
                        var download = item.Illust.GetOriginalUrl(item.Index).IsDownloadedAsync(out fp, item.Illust.PageCount<=1);
                        if (download)
                        {
                            IllustDownloaded.Show();
                            IllustDownloaded.Tag = fp;
                            IllustDownloaded.ToolTip = fp;
                            //ToolTipService.SetToolTip(IllustDownloaded, fp);
                        }
                        else
                        {
                            IllustDownloaded.Hide();
                            IllustDownloaded.Tag = null;
                            IllustDownloaded.ToolTip = string.Empty;
                            //ToolTipService.SetToolTip(IllustDownloaded, null);
                        }
                    }
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
                if (Contents is PixivItem)
                {
                    UpdateFollowMark(Contents.User);
                    if (Contents.IsWork()) UpdateFavMark(Contents.Illust);
                }
                if (SubIllusts.Items.Count > 0)
                {
                    SubIllusts.Items.UpdateLikeState(illustid, is_user);
                }
                if (RelativeItemsExpander.IsExpanded)
                {
                    RelativeItems.UpdateLikeState(illustid, is_user);
                }
                if (FavoriteItemsExpander.IsExpanded)
                {
                    FavoriteItems.UpdateLikeState(illustid, is_user);
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }
        #endregion

        #region Action methods
        public void ChangeIllustLikeState()
        {
            try
            {
                if (RelativeItems.IsKeyboardFocusWithin)
                    Commands.ChangeIllustLikeState.Execute(RelativeItems);
                else if (FavoriteItems.IsKeyboardFocusWithin)
                    Commands.ChangeIllustLikeState.Execute(FavoriteItems);
                else if (Contents.IsWork())
                    Commands.ChangeIllustLikeState.Execute(Contents);
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        public void ChangeUserLikeState()
        {
            try
            {
                if (RelativeItems.IsKeyboardFocusWithin)
                    Commands.ChangeUserLikeState.Execute(RelativeItems);
                else if (FavoriteItems.IsKeyboardFocusWithin)
                    Commands.ChangeUserLikeState.Execute(FavoriteItems);
                else if (Contents.HasUser())
                    Commands.ChangeUserLikeState.Execute(Contents);
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        public void OpenUser()
        {
            try
            {
                if (RelativeItems.IsKeyboardFocusWithin)
                    Commands.OpenUser.Execute(RelativeItems);
                else if (FavoriteItems.IsKeyboardFocusWithin)
                    Commands.OpenUser.Execute(FavoriteItems);
                else if (Contents.IsWork())
                    Commands.OpenUser.Execute(Contents);
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        public void OpenIllust()
        {
            try
            {
                if (RelativeItems.IsKeyboardFocusWithin)
                    Commands.OpenDownloaded.Execute(RelativeItems);
                else if (FavoriteItems.IsKeyboardFocusWithin)
                    Commands.OpenDownloaded.Execute(FavoriteItems);
                else if (Contents.IsWork())
                {
                    if (SubIllusts.Items.Count <= 0)
                    {
                        if (Contents.IsDownloaded)
                            Commands.OpenDownloaded.Execute(Contents);
                        else
                            Commands.OpenWorkPreview.Execute(Contents);
                    }
                    else if (SubIllusts.SelectedItems.Count > 0)
                    {
                        foreach (var item in SubIllusts.GetSelected())
                        {
                            if (item.IsDownloaded)
                                Commands.OpenDownloaded.Execute(item);
                            else
                                Commands.OpenWorkPreview.Execute(item);
                        }
                    }
                    else
                    {
                        if (SubIllusts.Items[0].IsDownloaded)
                            Commands.OpenDownloaded.Execute(SubIllusts.Items[0]);
                        else
                            Commands.OpenWorkPreview.Execute(SubIllusts.Items[0]);
                    }
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        public void OpenCachedImage()
        {
            try
            {
                if (RelativeItems.IsKeyboardFocusWithin)
                    Commands.OpenCachedImage.Execute(RelativeItems);
                else if (FavoriteItems.IsKeyboardFocusWithin)
                    Commands.OpenCachedImage.Execute(FavoriteItems);
                else if (Contents.IsWork())
                {
                    if (SubIllusts.Items.Count <= 0)
                    {
                        Commands.OpenCachedImage.Execute(Contents);
                    }
                    else if (SubIllusts.SelectedItems.Count > 0)
                    {
                        foreach (var item in SubIllusts.GetSelected())
                        {
                            Commands.OpenCachedImage.Execute(item);
                        }
                    }
                    else
                    {
                        Commands.OpenCachedImage.Execute(SubIllusts.Items[0]);
                    }
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        public void SaveIllust()
        {
            try
            {
                if (RelativeItems.IsKeyboardFocusWithin)
                    Commands.SaveIllust.Execute(RelativeItems);
                else if (FavoriteItems.IsKeyboardFocusWithin)
                    Commands.SaveIllust.Execute(FavoriteItems);
                else if (SubIllusts.Items.Count > 0)
                    Commands.SaveIllust.Execute(SubIllusts);
                else if (Contents.IsWork())
                    Commands.SaveIllust.Execute(Contents);
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        public void SaveIllustAll()
        {
            try
            {
                if (RelativeItems.IsKeyboardFocusWithin)
                    Commands.SaveIllustAll.Execute(RelativeItems);
                else if (FavoriteItems.IsKeyboardFocusWithin)
                    Commands.SaveIllustAll.Execute(FavoriteItems);
                else if (Contents.IsWork())
                    Commands.SaveIllustAll.Execute(Contents);
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }              
        
        public void CopyPreview()
        {
            if (!string.IsNullOrEmpty(PreviewImageUrl))
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                    Commands.CopyImage.Execute(PreviewImageUrl.GetImageCachePath());
                else
                    Commands.CopyImage.Execute(Preview);
            }
        }
        #endregion

        #region Theme/Thumb/Detail refresh methods
        public void UpdateTheme()
        {
            try
            {
                if (Contents is PixivItem)
                {
                    UpdateWebContent();
                    btnSubPagePrev.Enable(btnSubPagePrev.IsEnabled, btnSubPagePrev.IsVisible);
                    btnSubPageNext.Enable(btnSubPageNext.IsEnabled, btnSubPageNext.IsVisible);
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        public async void UpdateThumb(bool full = false, bool overwrite = false)
        {
            overwrite = Keyboard.Modifiers == ModifierKeys.Alt ? true : overwrite;
            await new Action(() =>
            {
                try
                {
                    if (Contents is PixivItem)
                    {
                        if (full)
                        {
                            SubIllusts.UpdateTilesImage(overwrite);
                            RelativeItems.UpdateTilesImage(overwrite);
                            FavoriteItems.UpdateTilesImage(overwrite);
                            if (Contents.IsWork())
                            {
                                ActionRefreshAvatar(overwrite);
                                ActionRefreshPreview(overwrite);
                            }
                            else if (Contents.IsUser())
                            {
                                ActionRefreshAvatar(overwrite);
                                UpdateUserBackground(overwrite);
                            }
                        }
                        else
                        {
                            if (Contents.IsWork() || Contents.IsUser())
                            {
                                if (SubIllusts.IsKeyboardFocusWithin)
                                    SubIllusts.UpdateTilesImage(overwrite);
                                else if (RelativeItems.IsKeyboardFocusWithin)
                                    RelativeItems.UpdateTilesImage(overwrite);
                                else if (FavoriteItems.IsKeyboardFocusWithin)
                                    FavoriteItems.UpdateTilesImage(overwrite);
                                else
                                {
                                    UpdateThumb(true);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
                finally
                {
                    IllustDetailWait.Hide();
                    Application.Current.DoEvents();
                }
            }).InvokeAsync();
        }

        internal async void UpdateDetail(PixivItem item)
        {
            try
            {
                page_count = 0;
                page_number = 0;
                page_index = 0;

                var force = ModifierKeys.Control.IsModified();
                if (item.IsWork())
                {
                    await new Action(async () =>
                    {
                        IllustDetailWait.Show();
                        if (force)
                        {
                            var illust = await item.ID.RefreshIllust();
                            if (illust is Pixeez.Objects.Work)
                                item.Illust = illust;
                            else
#if DEBUG
                                "Illust not exists or deleted".ShowMessageBox("ERROR[ILLUST]");
#else
                                "Illust not exists or deleted".ShowToast("ERROR[ILLUST]");
#endif
                        }
                        UpdateDetailIllust(item);
                    }).InvokeAsync(true);
                }
                else if (item.IsUser())
                {
                    await new Action(async () =>
                    {
                        IllustDetailWait.Show();
                        if (force)
                        {
                            var user = await item.UserID.RefreshUser();
                            if (user is Pixeez.Objects.User)
                                item.User = user;
                            else
#if DEBUG
                                "User not exists or deleted".ShowMessageBox("ERROR[USER]");
#else
                                "User not exists or deleted".ShowToast("ERROR[USER]");
#endif
                        }
                        UpdateDetailUser(item, force);
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

        private void UpdateDetailIllust(PixivItem item)
        {
            try
            {
                IllustDetailWait.Show();
                this.DoEvents();

                PreviewViewer.Show(true);
                PreviewBox.ToolTip = item.ToolTip;

                //Preview.Dispose();
                Preview.Source = Application.Current.GetNullPreview();
                PreviewImagePath = string.Empty;

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

                UpdateDownloadedMark();

                IllustSize.Text = $"{item.Illust.Width}x{item.Illust.Height}";
                IllustViewed.Text = stat_viewed;
                IllustFavorited.Text = stat_favorited;

                IllustStatInfo.Show();
                IllustStatInfo.ToolTip = string.Join("\r", stat_tip).Trim();

                IllustAuthor.Text = item.Illust.User.Name;
                IllustAuthorAvatar.Source = Application.Current.GetNullAvatar();
                AuthorAvatarWait.Show();

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
                    RefreshHtmlRender(IllustTagsHtml);

                    IllustTagExpander.Header = "Tags";
                    if (setting.AutoExpand == AutoExpandMode.AUTO ||
                        setting.AutoExpand == AutoExpandMode.ON ||
                        setting.AutoExpand == AutoExpandMode.SINGLEPAGE)
                    {
                        if (!IllustTagExpander.IsExpanded) IllustTagExpander.IsExpanded = true;
                    }
                    else IllustTagExpander.IsExpanded = false;
                    IllustTagExpander.Show();
                    IllustTagPedia.Show();
                }
                else
                {
                    IllustTagsHtml.DocumentText = string.Empty;
                    IllustTagExpander.Hide();
                    IllustTagPedia.Hide();
                }

                if (!string.IsNullOrEmpty(item.Illust.Caption) && item.Illust.Caption.Length > 0)
                {
                    RefreshHtmlRender(IllustDescHtml);

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

                SubIllusts.ClearAsync(false);
                PreviewBadge.Badge = item.Illust.PageCount;
                if (item.IsWork() && item.Illust.PageCount > 1)
                {
                    item.Index = 0;
                    PreviewBadge.Show();
                    SubIllustsExpander.Show();
                    SubIllustsExpander.IsExpanded = true;
                    SubIllustsExpander_Expanded(this, new RoutedEventArgs());
                    var total = item.Illust.PageCount;
                    page_count = (total / PAGE_ITEMS + (total % PAGE_ITEMS > 0 ? 1 : 0)).Value;
                }
                else
                {
                    SubIllustsExpander.IsExpanded = false;
                    SubIllustsExpander.Hide();
                    PreviewBadge.Hide();
                    page_count = 0;
                }
                UpdateSubPageNav();

                RelativeItemsExpander.Header = "Related Illusts";
                RelativeItemsExpander.IsExpanded = false;
                RelativeItemsExpander.Show();
                RelativeItems.ClearAsync(false);
                RelativeNextPage.Hide();

                FavoriteItemsExpander.Header = "Author Favorite";
                FavoriteItemsExpander.IsExpanded = false;
                FavoriteItemsExpander.Show();
                FavoriteItems.ClearAsync(false);
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
                ActionRefreshAvatar();
                if (!SubIllustsExpander.IsShown())
                    ActionRefreshPreview();
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                //item.Illust.AddToHistory();
                item.AddToHistory();
                Application.Current.DoEvents();
                IllustDetailWait.Hide();
                Preview.Focus();
            }
        }

        private string user_backgroundimage_url = string.Empty;
        public async void UpdateUserBackground(bool overwrite = false)
        {
            if (Contents.IsUser())
            {
                if (setting.ShowUserBackgroundImage && string.IsNullOrEmpty(user_backgroundimage_url))
                {
                    try
                    {
                        PreviewViewer.Show();
                        PreviewWait.Show();
                        var bg = await user_backgroundimage_url.LoadImageFromUrl(overwrite);
                        if (bg.Source == null) PreviewWait.Fail();
                        else PreviewWait.Hide();
                        Preview.Dispose();
                        Preview.Source = bg.Source;
                        bg.Source = null;
                    }
                    catch (Exception) { PreviewWait.Fail(); }
                }
                else
                {
                    PreviewWait.Hide();
                    PreviewViewer.Hide();
                    Preview.Dispose();
                }
            }
        }

        private Pixeez.Objects.UserInfo UserInfo = null;
        private async void UpdateDetailUser(PixivItem item, bool force = false)
        {
            try
            {
                IllustDetailWait.Show();
                this.DoEvents();

                Preview.Dispose();
                PreviewViewer.Hide();

                var user = item.User;
                UserInfo = user.FindUserInfo();

                if (force || UserInfo == null) UserInfo = await user.RefreshUserInfo();
                if (UserInfo == null) return;

                var nuser = UserInfo.user;
                var nprof = UserInfo.profile;
                var nworks = UserInfo.workspace;
                if (user.IsLiked() != nuser.IsLiked())
                {
                    user.is_followed = nuser.is_followed;
                    await user.RefreshUser();
                }

                IllustSizeIcon.Kind = PackIconModernKind.Image;
                IllustSize.Text = $"{nprof.total_illusts + nprof.total_manga}";
                IllustViewedIcon.Kind = PackIconModernKind.Check;
                IllustViewed.Text = $"{nprof.total_follow_users}";
                IllustFavorited.Text = $"{nprof.total_illust_bookmarks_public}";
                IllustStatInfo.Show();

                IllustTitle.Text = string.Empty;
                IllustAuthor.Text = nuser.Name;
                IllustAuthorAvatar.Source = Application.Current.GetNullAvatar();
                AuthorAvatarWait.Show();

                FollowAuthor.Show();
                UpdateFollowMark(nuser);

                BookmarkIllust.Hide();
                IllustActions.Hide();

                IllustTagPedia.Hide();

                if (nuser != null && nprof != null && nworks != null)
                {
                    RefreshHtmlRender(IllustTagsHtml);
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
                    RefreshHtmlRender(IllustDescHtml);
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

                SubIllusts.Clear();
                SubIllustsExpander.IsExpanded = false;
                SubIllustsExpander.Hide();
                PreviewBadge.Hide();

                RelativeItemsExpander.Header = "Illusts";
                RelativeItemsExpander.Show();
                RelativeNextPage.Hide();
                RelativeItemsExpander.IsExpanded = false;

                FavoriteItemsExpander.Header = "Favorite";
                FavoriteItemsExpander.Show();
                FavoriteNextPage.Hide();
                FavoriteItemsExpander.IsExpanded = false;

                await Task.Delay(1);
                this.DoEvents();
                ActionRefreshAvatar();
                await Task.Delay(1);
                this.DoEvents();
                user_backgroundimage_url = nprof.background_image_url is string ? nprof.background_image_url as string : nuser.GetPreviewUrl();
                UpdateUserBackground();
                await Task.Delay(1);
                this.DoEvents();
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[USER]");
            }
            finally
            {
                //item.User.AddToHistory();
                item.AddToHistory();
                Application.Current.DoEvents();
                IllustDetailWait.Hide();
            }
        }
        #endregion

        #region Subillusts/Relative illusts/Favorite illusts helper
        private async Task ShowIllustPages(PixivItem item, int index = 0, int page = 0, int count = -1)
        {
            try
            {
                SubIllusts.Wait();
                if (item.Illust is Pixeez.Objects.Work)
                {
                    if (count < 0) count = PAGE_ITEMS;

                    #region Update sub-pages nav button
                    if (page <= 0)
                    {
                        page_number = 0;
                        SubIllustPrevPages.Hide();
                    }
                    else
                        SubIllustPrevPages.Show();

                    if (page_number >= page_count - 1)
                    {
                        page_number = page_count - 1;
                        SubIllustNextPages.Hide();
                    }
                    else
                        SubIllustNextPages.Show();

                    this.DoEvents();
                    #endregion

                    SubIllusts.Clear(false);

                    var idx = page * count;
                    if (item.Illust is Pixeez.Objects.IllustWork)
                    {
                        var subset = item.Illust as Pixeez.Objects.IllustWork;
                        if (subset.meta_pages.Count() > 1)
                        {
                            var pages = subset.meta_pages.Skip(idx).Take(count).ToList();
                            for (var i = 0; i < pages.Count; i++)
                            {
                                var p = pages[i];
                                p.AddTo(SubIllusts.Items, item.Illust, i + idx, item.NextURL);
                            }
                            this.DoEvents();
                        }
                    }
                    else if (item.Illust is Pixeez.Objects.NormalWork)
                    {
                        var subset = item.Illust as Pixeez.Objects.NormalWork;
                        if (subset.PageCount >= 1 && subset.Metadata == null)
                        {
                            var illust = await item.Illust.RefreshIllust();
                            if (illust is Pixeez.Objects.Work) item.Illust = illust;
                        }
                        if (item.Illust.Metadata is Pixeez.Objects.Metadata)
                        {
                            var pages = item.Illust.Metadata.Pages.Skip(idx).Take(count).ToList();
                            for (var i = 0; i < pages.Count; i++)
                            {
                                var p = pages[i];
                                p.AddTo(SubIllusts.Items, item.Illust, i + idx, item.NextURL);
                            }
                            this.DoEvents();
                        }
                    }
                    //SubIllusts.UpdateLayout();
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
                SubIllusts.Ready();
                this.DoEvents();
            }
        }

        private async void ShowIllustPagesAsync(PixivItem item, int index = 0, int page = 0, int count = 30)
        {
            await new Action(async () =>
            {
                await ShowIllustPages(item, index, page, count);
            }).InvokeAsync();
        }

        private List<long?> relative_illusts = new List<long?>();
        private async Task ShowRelativeInline(PixivItem item, string next_url = "", bool append = false)
        {
            try
            {
                RelativeItems.Wait();
                if (!(relative_illusts is List<long?>)) relative_illusts = new List<long?>();
                if (!append)
                {
                    RelativeItems.Clear(false);
                    relative_illusts.Clear();
                }

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

                    if (!append)
                    {
                        RelativeItemsExpander.Tag = lastUrl;
                        CurrentRelativeURL = lastUrl;
                    }
                    RelativeNextPage.Tag = next_url;
                    NextRelativeURL = next_url;

                    foreach (var illust in relatives.illusts)
                    {
                        if (relative_illusts.Contains(illust.Id)) continue;
                        relative_illusts.Add(illust.Id);
                        illust.Cache();
                        illust.AddTo(RelativeItems.Items, relatives.next_url);
                        this.DoEvents();
                    }
                    this.DoEvents();
                    RelativeItems.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                RelativeItems.Ready();
                if (RelativeItems.Items.Count > 0) RelativeRefresh.Show();
                else RelativeRefresh.Hide();
            }
        }

        private async void ShowRelativeInlineAsync(PixivItem item, string next_url = "", bool append = false)
        {
            await new Action(async () =>
            {
                await ShowRelativeInline(item, next_url, append);
            }).InvokeAsync();
        }

        private async Task ShowUserWorksInline(Pixeez.Objects.UserBase user, string next_url = "", bool append = false)
        {
            try
            {
                RelativeItems.Wait();
                if (!(relative_illusts is List<long?>)) relative_illusts = new List<long?>();
                if (!append)
                {
                    RelativeItems.Clear(false);
                    relative_illusts.Clear();
                }

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

                    if (!append)
                    {
                        RelativeItemsExpander.Tag = lastUrl;
                        CurrentRelativeURL = lastUrl;
                    }
                    RelativeNextPage.Tag = next_url;
                    NextRelativeURL = next_url;

                    foreach (var illust in relatives.illusts)
                    {
                        if (relative_illusts.Contains(illust.Id)) continue;
                        relative_illusts.Add(illust.Id);
                        illust.Cache();
                        illust.AddTo(RelativeItems.Items, relatives.next_url);
                        this.DoEvents();
                    }
                    this.DoEvents();
                    RelativeItems.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                RelativeItems.Ready();
                if (RelativeItems.Items.Count > 0) RelativeRefresh.Show();
                else RelativeRefresh.Hide();
            }
        }

        private async void ShowUserWorksInlineAsync(Pixeez.Objects.UserBase user, string next_url = "", bool append = false)
        {
            await new Action(async () =>
            {
                await ShowUserWorksInline(user, next_url, append);
            }).InvokeAsync();
        }

        private string last_restrict = string.Empty;
        private List<long?> favorite_illusts = new List<long?>();
        private async Task ShowFavoriteInline(Pixeez.Objects.UserBase user, string next_url = "", bool append = false)
        {
            try
            {
                FavoriteItems.Wait();
                if (!(favorite_illusts is List<long?>)) favorite_illusts = new List<long?>();
                if (!append)
                {
                    FavoriteItems.Clear(false);
                    favorite_illusts.Clear();
                }

                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                var lastUrl = next_url;
                var restrict = Keyboard.Modifiers != ModifierKeys.None ? "private" : "public";
                if (!last_restrict.Equals(restrict, StringComparison.CurrentCultureIgnoreCase)) next_url = string.Empty;
                FavoriteItemsExpander.Header = $"Favorite ({CultureInfo.CurrentCulture.TextInfo.ToTitleCase(restrict)})";

                var favorites = string.IsNullOrEmpty(next_url) ? await tokens.GetUserFavoriteWorksAsync(user.Id.Value, restrict) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = favorites.next_url ?? string.Empty;
                last_restrict = restrict;

                if (favorites.illusts is Array)
                {
                    if (next_url.Equals(lastUrl, StringComparison.CurrentCultureIgnoreCase))
                        FavoriteNextPage.Hide();
                    else FavoriteNextPage.Show();

                    if (!append)
                    {
                        FavoriteItemsExpander.Tag = lastUrl;
                        CurrentFavoriteURL = lastUrl;
                    }
                    FavoriteNextPage.Tag = next_url;
                    NextFavoriteURL = next_url;

                    foreach (var illust in favorites.illusts)
                    {
                        if (favorite_illusts.Contains(illust.Id)) continue;
                        favorite_illusts.Add(illust.Id);
                        illust.Cache();
                        illust.AddTo(FavoriteItems.Items, favorites.next_url);
                        this.DoEvents();
                    }
                    this.DoEvents();
                    FavoriteItems.UpdateTilesImage();
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                FavoriteItems.Ready();
                if (FavoriteItems.Items.Count > 0) FavoriteItems.Show();
                else FavoriteItems.Hide();
            }
        }

        private async void ShowFavoriteInlineAsync(Pixeez.Objects.UserBase user, string next_url = "", bool append = false)
        {
            await new Action(async () =>
            {
                await ShowFavoriteInline(user, next_url, append);
            }).InvokeAsync();
        }
        #endregion

        #region Navgition methods
        private async void UpdateSubPageNav()
        {
            try
            {
                if (Contents is PixivItem)
                {
                    if (Contents.IsUser())
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
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        public void OpenInNewWindow()
        {
            if (Contents is PixivItem && !(Parent is ContentWindow))
            {
                Commands.Open.Execute(Contents);
            }
        }

        public void FirstIllust()
        {
            if (InSearching) return;
            if (RelativeItems.IsKeyboardFocusWithin)
            {
                RelativeItems.MoveCurrentToFirst();
                RelativeItems.ScrollIntoView(RelativeItems.SelectedItem);
            }
            else if (FavoriteItems.IsKeyboardFocusWithin)
            {
                FavoriteItems.MoveCurrentToFirst();
                FavoriteItems.ScrollIntoView(FavoriteItems.SelectedItem);
            }
        }

        public void LastIllust()
        {
            if (InSearching) return;
            if (RelativeItems.IsKeyboardFocusWithin)
            {
                RelativeItems.MoveCurrentToLast();
                RelativeItems.ScrollIntoView(RelativeItems.SelectedItem);
            }
            else if (FavoriteItems.IsKeyboardFocusWithin)
            {
                FavoriteItems.MoveCurrentToLast();
                FavoriteItems.ScrollIntoView(FavoriteItems.SelectedItem);
            }
        }

        public void PrevIllust()
        {
            if (InSearching) return;
            if (Parent is ContentWindow) PrevIllustPage();
            else Commands.PrevIllust.Execute(Application.Current.MainWindow);
        }

        public void NextIllust()
        {
            if (InSearching) return;
            if (Parent is ContentWindow) NextIllustPage();
            else Commands.NextIllust.Execute(Application.Current.MainWindow);
        }

        public bool IsFirstPage
        {
            get
            {
                return (Contents is PixivItem && Contents.Index == 0);
            }
        }

        public bool IsLastPage
        {
            get
            {
                return (Contents is PixivItem && Contents.Index == Contents.Count - 1);
            }
        }

        public bool IsMultiPages { get { return (Contents is PixivItem && Contents.HasPages()); } }
        public void PrevIllustPage()
        {
            if (InSearching) return;
            if (Contents is PixivItem)
            {
                setting = Application.Current.LoadSetting();
                if (Contents.Count > 1)
                {
                    if (Contents.Index > 0)
                        SubPageNav_Clicked(btnSubPagePrev, new RoutedEventArgs());
                    else if (setting.SeamlessViewInMainWindow)
                        PrevIllust();
                }
                else PrevIllust();
            }
        }

        public void NextIllustPage()
        {
            if (InSearching) return;
            if (Contents is PixivItem)
            {
                setting = Application.Current.LoadSetting();
                if (Contents.Count > 1)
                {
                    if (Contents.Index < Contents.Count - 1)
                        SubPageNav_Clicked(btnSubPageNext, new RoutedEventArgs());
                    else if (setting.SeamlessViewInMainWindow)
                        NextIllust();
                }
                else NextIllust();
            }
        }

        public void SetFilter(string filter)
        {
            try
            {
                RelativeItems.Filter = filter.GetFilter();
                FavoriteItems.Filter = filter.GetFilter();
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
                {
                    RelativeItems.Filter = filter.GetFilter();
                    FavoriteItems.Filter = filter.GetFilter();
                }
                else
                {
                    RelativeItems.Filter = null;
                    FavoriteItems.Filter = null;
                }
            }
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
        }

        public dynamic GetTilesCount()
        {
            List<string> tips = new List<string>();
            tips.Add($"Relative: {RelativeItems.ItemsCount} of {RelativeItems.Items.Count}");
            tips.Add($"Favorite: {FavoriteItems.ItemsCount} of {FavoriteItems.Items.Count}");
            return (string.Join(Environment.NewLine, tips));
        }
        
        public bool InSearching
        {
            get
            {
                var parent = ParentWindow;
                if (parent is MainWindow)
                    return ((parent as MainWindow).InSearching);
                else if (parent is ContentWindow)
                    return ((parent as ContentWindow).InSearching);
                else return (false);
            }
        }
        #endregion

        internal void Dispose()
        {
            try
            {
                PreviewPopupTimer.Stop();
                PreviewPopupTimer.Dispose();

                DeleteHtmlRender();
                IllustAuthorAvatar.Dispose();
                Preview.Dispose();
                SubIllusts.Clear();
                RelativeItems.Clear();
                FavoriteItems.Clear();
                Contents.Source = null;
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        public IllustDetailPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            RelativeItems.Columns = 5;
            FavoriteItems.Columns = 5;

            IllustDetailWait.Hide();
            btnSubPagePrev.Hide();
            btnSubPageNext.Hide();

            CreateHtmlRender();

            #region ToolButton MouseOver action
            IllustTagPedia.MouseOverAction();
            IllustTagSpeech.MouseOverAction();
            IllustTagRefresh.MouseOverAction();

            IllustDescSpeech.MouseOverAction();
            IllustDescRefresh.MouseOverAction();

            SubIllustPrevPages.MouseOverAction();
            SubIllustNextPages.MouseOverAction();
            SubIllustRefresh.MouseOverAction();

            RelativePrevPage.MouseOverAction();
            RelativeNextPage.MouseOverAction();
            RelativeNextAppend.MouseOverAction();
            RelativeRefresh.MouseOverAction();

            FavoritePrevPage.MouseOverAction();
            FavoriteNextPage.MouseOverAction();
            FavoriteNextAppend.MouseOverAction();
            FavoriteRefresh.MouseOverAction();
            #endregion

            try
            {
                PreviewPopup = (Popup)FindResource("PreviewPopup");
                if(PreviewPopup is Popup)
                {
                    PreviewPopupBackground = PreviewPopup.GetChildren<Rectangle>().FirstOrDefault();
                    PreviewPopupToolButtons = PreviewPopup.GetChildren<Button>();
                    foreach (var button in PreviewPopupToolButtons)
                        button.MouseOverAction();

                    InitTaskTimer(ref PreviewPopupTimer);
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }

            if (Contents is PixivItem) UpdateDetail(Contents);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private void Page_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var change_illust = Keyboard.Modifiers == ModifierKeys.Shift;
            if(PreviewPopup is Popup && PreviewPopup.IsOpen && e.LeftButton != MouseButtonState.Pressed)
            {
                PreviewPopup.IsOpen = false;
                PreviewPopupTimer.Stop();
            }

            if (change_illust && !(Parent is Frame))
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
            else
            {
                if (e.XButton1 == MouseButtonState.Pressed)
                {
                    NextIllustPage();
                    e.Handled = true;
                }
                else if (e.XButton2 == MouseButtonState.Pressed)
                {
                    PrevIllustPage();
                    e.Handled = true;
                }
            }
        }

        #region WebBrowser Events Handle
        private async void WebBrowser_LinkClick(object sender, System.Windows.Forms.HtmlElementEventArgs e)
        {
            bCancel = true;
            try
            {
                e.BubbleEvent = false;
                e.ReturnValue = false;

                if (e.EventType.Equals("click", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (sender is System.Windows.Forms.HtmlElement)
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
                            if (!e.AltKeyPressed && !e.CtrlKeyPressed && !e.ShiftKeyPressed)
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
            }
#if DEBUG
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
#else
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
                                            var img = await src.LoadImageFromUrl();
                                            if (!string.IsNullOrEmpty(img.SourcePath)) imgElemt.SetAttribute("src", new Uri(img.SourcePath).AbsoluteUri);
                                            img.Source = null;
                                        }
                                    }
#if DEBUG
                                    catch (Exception ex)
                                    {
                                        ex.Message.DEBUG();
                                    }
#else
                                    catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
                catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
            }
        }

        private void WebBrowser_Navigated(object sender, System.Windows.Forms.WebBrowserNavigatedEventArgs e)
        {
            try
            {
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
#endif
        }

        private void WebBrowser_PreviewKeyDown(object sender, System.Windows.Forms.PreviewKeyDownEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Forms.WebBrowser)
                {
                    var browser = sender as System.Windows.Forms.WebBrowser;
#if DEBUG
                    e.KeyCode.ToString().DEBUG("WEBBROWSER");
#endif

                    if (e.Control && e.KeyCode == System.Windows.Forms.Keys.C)
                    {
                        var text = browser.GetText();
                        if (sender == IllustTagsHtml) text = text.Replace("#", " ").Trim();
                        if (!string.IsNullOrEmpty(text)) Commands.CopyText.Execute(text);
                    }
                    else if (e.Shift && e.KeyCode == System.Windows.Forms.Keys.C)
                    {
                        var html = browser.GetText(true).Trim();
                        var text = browser.GetText(false).Trim();
                        if (sender == IllustTagsHtml) text = text.Replace("#", " ").Trim();
                        var data = new HtmlTextData() { Html = html, Text = text };
                        Commands.CopyText.Execute(data);
                    }
                    else if (e.Control && e.KeyCode == System.Windows.Forms.Keys.A)
                    {
                        browser.Document.ExecCommand("SelectAll", false, null);
                    }
                    else if (e.KeyCode == System.Windows.Forms.Keys.F5)
                    {
                        RefreshHtmlRender(browser);
                    }
                    //else
                    //{
                    //    Key key;
                    //    if (Enum.TryParse<Key>(e.KeyCode.ToString(), out key))
                    //    {
                    //        var source = new HwndSource(0, 0, 0, 0, 0, "", IntPtr.Zero); // dummy source
                    //        //var source = Keyboard.PrimaryDevice.ActiveSource;
                    //        var kevt = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(this), Environment.TickCount, key)
                    //        {
                    //            RoutedEvent = Keyboard.PreviewKeyDownEvent,
                    //            Source = Keyboard.PrimaryDevice.ActiveSource,
                    //            Handled = true
                    //        };
                    //        //this.RaiseEvent(kevt);
                    //        //Page_KeyUp(this, kevt);
                    //    }
                    //}
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR[BROWSER]"); }
#else
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
#endif
        }
        #endregion

        #region Preview Popup
        private async void PreviewPopupTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (PreviewPopup is Popup)
                {
                    await new Action(() =>
                    {
                        if (!PreviewPopup.IsMouseDirectlyOver)
                        {
                            PreviewPopup.IsOpen = false;
                        }
                    }).InvokeAsync(true);
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private void PreviewPopup_Closed(object sender, EventArgs e)
        {
            if (PreviewPopup is Popup && PreviewPopupTimer is System.Timers.Timer)
            {
                PreviewPopupTimer.Stop();
            }
        }

        private void PreviewPopup_Opened(object sender, EventArgs e)
        {
            if (PreviewPopup is Popup && PreviewPopupTimer is System.Timers.Timer)
            {
                PreviewPopupTimer.Start();
            }
        }

        private void PreviewPopup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button)
            {
                var icon = e.Source as dynamic;
                int _row = (int)icon.GetValue(Grid.RowProperty);
                int _col = (int)icon.GetValue(Grid.ColumnProperty);
                if (_row == 0 && _col == 0)
                {
                    ActionIllustInfo_Click(PreviewCopyIllustID, e);
                }
                else if (_row == 0 && _col == 1)
                {
                    ActionOpenIllust_Click(PreviewOpenDownloaded, e);
                }
                else if (_row == 0 && _col == 2)
                {
                    ActionIllustInfo_Click(PreviewCopyImage, e);
                }
                else if (_row == 1 && _col == 0)
                {
                    ActionRefreshPreview_Click(PreviewRefresh, e);
                }
                else if (_row == 1 && _col == 1)
                {
                    ActionOpenIllust_Click(PreviewOpen, e);
                }
                else if (_row == 1 && _col == 2)
                {
                    ActionOpenIllust_Click(PreviewCacheOpen, e);
                }
                else if (_row == 2 && _col == 0)
                {
                    ActionSendToOtherInstance_Click(PreviewSendIllustToInstance, e);
                }
                else if (_row == 2 && _col == 1)
                {
                    ActionSaveIllust_Click(PreviewSave, e);
                }
                else if (_row == 2 && _col == 2)
                {
                    ActionSendToOtherInstance_Click(PreviewSendAuthorToInstance, e);
                }

                if (PreviewPopup is Popup)
                {
                    PreviewPopupTimer.Stop();
                    PreviewPopup.IsOpen = false;
                }
                e.Handled = true;
            }
        }

        private void PreviewPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            if (PreviewPopup is Popup && PreviewPopup.IsOpen)
            {
                //PreviewPopupTimer.Stop();
            }
        }

        private void PreviewPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            if (PreviewPopup is Popup && PreviewPopup.IsOpen)
            {
                //PreviewPopupTimer.Start();
            }
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
                if (Contents is PixivItem)
                {
                    Commands.CopyArtistIDs.Execute(Contents);
                }
            }
            else if (sender == ActionCopyIllustID || sender == PreviewCopyIllustID)
            {
                if (Contents is PixivItem)
                {
                    Commands.CopyArtworkIDs.Execute(Contents);
                }
            }
            else if (sender == PreviewCopyImage)
            {
                CopyPreview();
            }
            else if (sender == ActionCopyIllustDate || sender == IllustDate || sender == IllustDateInfo)
            {
                Commands.CopyText.Execute(ActionCopyIllustDate.Header);
            }
            else if (sender == ActionIllustWebPage)
            {
                if (Contents is PixivItem)
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
                OpenInNewWindow();
            }
            else if (sender == ActionIllustWebLink || sender.GetUid().Equals("ActionIllustWebLink", StringComparison.CurrentCultureIgnoreCase))
            {
                if (Contents.IsWork())
                {
                    var href = Contents.ID.ArtworkLink();
                    Commands.CopyText.Execute(href);
                }
            }
            else if (sender == ActionAuthorWebLink || sender.GetUid().Equals("ActionAuthorWebLink", StringComparison.CurrentCultureIgnoreCase))
            {
                if (Contents.IsWork() || Contents.IsUser())
                {
                    var href = Contents.UserID.ArtistLink();
                    Commands.CopyText.Execute(href);
                }
            }
            else if (sender == ActionSendIllustToInstance)
            {
                if (Contents is PixivItem)
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
                if (Contents is PixivItem)
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
            if (sender == ActionIllustAuthorInfo || sender == btnAuthorAvatar)
            {
                if (Contents is PixivItem)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                        Commands.ShellSendToOtherInstance.Execute(Contents.User);
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        ActionRefreshAvatar();
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        ActionRefreshAvatar(true);
                    else if (Contents.IsWork())
                        Commands.OpenUser.Execute(Contents.User);
                }
            }
            else if (sender == AuthorAvatarWait)
            {
                if (Keyboard.Modifiers == ModifierKeys.None)
                    ActionRefreshAvatar();
                else if (Keyboard.Modifiers == ModifierKeys.Alt)
                    ActionRefreshAvatar(true);
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
            if (!RelativeItemsExpander.IsExpanded) RelativeItemsExpander.IsExpanded = true;
        }

        private void ActionShowFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (!FavoriteItemsExpander.IsExpanded) FavoriteItemsExpander.IsExpanded = true;
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
                    if (Contents is PixivItem)
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
            else if (sender == PreviewCacheOpen && Preview.Source != null)
            {
                Commands.OpenCachedImage.Execute(string.IsNullOrEmpty(PreviewImagePath) ? Contents.Illust.GetPreviewUrl().GetImageCachePath() : PreviewImagePath);
            }
        }

        private void ActionRefreshPreview_Click(object sender, RoutedEventArgs e)
        {
            var overwrite = Keyboard.Modifiers == ModifierKeys.Control ? true : false;
            ActionRefreshPreview(overwrite);
        }

        private async void ActionRefreshPreview(bool overwrite = false)
        {
            if (Contents is PixivItem)
            {
                setting = Application.Current.LoadSetting();

                Preview.Show();
                await new Action(async () =>
                {
                    try
                    {
                        var c_item = SubIllusts.SelectedItem is PixivItem ? SubIllusts.SelectedItem as PixivItem : Contents;
                        Contents.Index = c_item.Index;
                        lastSelectionItem = c_item;
                        lastSelectionChanged = DateTime.Now;

                        if (c_item.IsSameIllust(Contents)) PreviewWait.Show();

                        PreviewImageUrl = c_item.Illust.GetPreviewUrl(c_item.Index);
                        var img = await PreviewImageUrl.LoadImageFromUrl(overwrite);
                        if (setting.SmartPreview &&
                            (img.Source == null ||
                             img.Source.Width < setting.PreviewUsingLargeMinWidth ||
                             img.Source.Height < setting.PreviewUsingLargeMinHeight))
                        {
                            PreviewImageUrl = c_item.Illust.GetPreviewUrl(c_item.Index, true);
                            var large = await PreviewImageUrl.LoadImageFromUrl(overwrite);
                            if (large.Source != null) img = large;
                        }

                        if (c_item.IsSameIllust(Contents))
                        {
                            if (img.Source != null)
                            {
                                //Preview.Dispose();
                                Preview.Source = img.Source;
                                PreviewImagePath = img.SourcePath;
                                PreviewWait.Hide();
                            }
                            else PreviewWait.Fail();
                        }
                        img.Source = null;
                    }
                    catch (Exception) { PreviewWait.Fail(); }
                    finally
                    {
                        if (Preview.Source == null) PreviewWait.Fail();
                        this.DoEvents();
                    }
                }).InvokeAsync();
            }
        }

        private async void ActionRefreshAvatar(bool overwrite = false)
        {
            if (Contents is PixivItem)
            {
                await new Action(async () =>
                {
                    try
                    {
                        AuthorAvatarWait.Show();
                        btnAuthorAvatar.Show(AuthorAvatarWait.IsFail);

                        var c_item = Contents;
                        AvatarImageUrl = Contents.User.GetAvatarUrl();
                        var img =  await AvatarImageUrl.LoadImageFromUrl(overwrite, size:Application.Current.GetDefaultAvatarSize());
                        if (c_item.IsSameIllust(Contents))
                        {
                            if (img.Source != null)
                            {
                                //IllustAuthorAvatar.Dispose();
                                IllustAuthorAvatar.Source = img.Source;
                                AuthorAvatarWait.Hide();
                            }
                            else AuthorAvatarWait.Fail();
                        }
                        img.Source = null;
                    }
                    catch (Exception) { AuthorAvatarWait.Fail(); btnAuthorAvatar.Show(); }
                    finally
                    {
                        if (IllustAuthorAvatar.Source == null) AuthorAvatarWait.Fail();
                        btnAuthorAvatar.Show(AuthorAvatarWait.IsFail);
                        this.DoEvents();
                    }
                }).InvokeAsync();
            }
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
                if (sender == IllustTagSpeech)
                {
                    is_tag = true;
                    text = IllustTagsHtml.GetText();
                }
                else if (sender == IllustDescSpeech)
                    text = IllustDescHtml.GetText();
                else if (sender == IllustTitle)
                    text = IllustTitle.Text;
                else if (sender == IllustAuthor)
                    text = IllustAuthor.Text;
                else if (sender == IllustDate || sender == IllustDateInfo)
                    text = IllustDate.Text;
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
                        if (host == IllustTagSpeech) { is_tag = true; text = IllustTagsHtml.GetText(); }
                        else if (host == IllustDescSpeech) text = IllustDescHtml.GetText();
                        else if (host == IllustAuthor) text = IllustAuthor.Text;
                        else if (host == IllustTitle) text = IllustTitle.Text;
                        else if (host == IllustDateInfo || host == IllustDate) text = IllustDate.Text;
                        else if (host == SubIllustsExpander || host == SubIllusts) text = IllustTitle.Text;
                        else if (host == RelativeItemsExpander || host == RelativeItems)
                        {
                            List<string> lines = new List<string>();
                            foreach (PixivItem item in RelativeItems.GetSelected())
                            {
                                lines.Add(item.Illust.Title);
                            }
                            text = string.Join($",{Environment.NewLine}", lines);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            List<string> lines = new List<string>();
                            foreach (PixivItem item in FavoriteItems.GetSelected())
                            {
                                lines.Add(item.Illust.Title);
                            }
                            text = string.Join($",{Environment.NewLine}", lines);
                        }
                    }
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
#endif
            if (is_tag)
                text = string.Join(Environment.NewLine, text.Trim().Split(Speech.TagBreak, StringSplitOptions.RemoveEmptyEntries));
            else
                text = string.Join(Environment.NewLine, text.Trim().Split(Speech.LineBreak, StringSplitOptions.RemoveEmptyEntries));

            if (!string.IsNullOrEmpty(text)) text.Play(culture);
        }

        private void ActionCopySelectedText_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Empty;
            var is_tag = false;
            try
            {
                if (sender == IllustTagSpeech)
                {
                    is_tag = true;
                    text = IllustTagsHtml.GetText();
                }
                else if (sender == IllustDescSpeech)
                    text = IllustDescHtml.GetText();
                else if (sender == IllustTitle)
                    text = IllustTitle.Text;
                else if (sender == IllustAuthor)
                    text = IllustAuthor.Text;
                else if (sender == IllustDate || sender == IllustDateInfo)
                    text = IllustDate.Text;
                else if (sender is MenuItem)
                {
                    var mi = sender as MenuItem;

                    if (mi.Parent is ContextMenu)
                    {
                        var host = (mi.Parent as ContextMenu).PlacementTarget;
                        if (host == IllustTagSpeech) { is_tag = true; text = IllustTagsHtml.GetText(); }
                        else if (host == IllustDescSpeech) text = IllustDescHtml.GetText();
                        else if (host == IllustAuthor) text = IllustAuthor.Text;
                        else if (host == IllustTitle) text = IllustTitle.Text;
                        else if (host == IllustDateInfo || host == IllustDate) text = IllustDate.Text;
                        else if (host == SubIllustsExpander || host == SubIllusts) text = IllustTitle.Text;
                        else if (host == RelativeItemsExpander || host == RelativeItems)
                        {
                            List<string> lines = new List<string>();
                            foreach (PixivItem item in RelativeItems.GetSelected())
                            {
                                lines.Add(item.Illust.Title);
                            }
                            text = string.Join($",{Environment.NewLine}", lines);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            List<string> lines = new List<string>();
                            foreach (PixivItem item in FavoriteItems.GetSelected())
                            {
                                lines.Add(item.Illust.Title);
                            }
                            text = string.Join($",{Environment.NewLine}", lines);
                        }
                    }
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
#endif
            if (is_tag)
                text = string.Join(Environment.NewLine, text.Trim().Split(Speech.TagBreak, StringSplitOptions.RemoveEmptyEntries));
            else
                text = string.Join(Environment.NewLine, text.Trim().Split(Speech.LineBreak, StringSplitOptions.RemoveEmptyEntries));

            if (!string.IsNullOrEmpty(text)) Commands.CopyText.Execute(text);
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
                        if (host == IllustTagSpeech)
                            text = $"\"tag:{string.Join($"\"{Environment.NewLine}\"tag:", IllustTagsHtml.GetText().Trim().Trim('#').Split('#'))}\"";
                        else if (host == IllustDescSpeech)
                            text = $"\"{string.Join("\" \"", IllustDescHtml.GetText().ParseLinks().ToArray())}\"";
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
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
                        if (host == IllustTagSpeech)
                        {
                            if (Keyboard.Modifiers == ModifierKeys.None)
                                RefreshHtmlRender(IllustTagsHtml);
                            else if (Keyboard.Modifiers == ModifierKeys.Shift)
                                Application.Current.LoadTags(false, true);
                            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                                Application.Current.LoadSetting().CustomTagsFile.OpenFileWithShell();
                            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                                Application.Current.LoadSetting().CustomWildcardTagsFile.OpenFileWithShell();
                        }
                        else if (host == IllustDescSpeech)
                        {
                            if (Keyboard.Modifiers == ModifierKeys.None)
                                RefreshHtmlRender(IllustDescHtml);
                            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                                Application.Current.LoadSetting().ContentsTemplateFile.OpenFileWithShell();
                        }
                        else if (mi == ActionRefresh)
                        {
                            if (Contents is PixivItem) UpdateDetail(Contents);
                        }
                    }
                }
                else if (sender == IllustTagRefresh)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        RefreshHtmlRender(IllustTagsHtml);
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        Application.Current.LoadTags(false, true);
                    else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                        Application.Current.LoadSetting().CustomTagsFile.OpenFileWithShell();
                    else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                        Application.Current.LoadSetting().CustomWildcardTagsFile.OpenFileWithShell();
                }
                else if (sender == IllustDescRefresh)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        RefreshHtmlRender(IllustDescHtml);
                    else if (Keyboard.Modifiers == ModifierKeys.Shift)
                        Application.Current.LoadCustomTemplate();
                    else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                        Application.Current.LoadSetting().ContentsTemplateFile.OpenFileWithShell();
                }
                else if (sender == SubIllustRefresh)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        SubIllusts.UpdateTilesImage();
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        SubIllusts.UpdateTilesImage(true);
                }
                else if (sender == RelativeRefresh)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        RelativeItems.UpdateTilesImage();
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        RelativeItems.UpdateTilesImage(true);
                    else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                        RelativeItemsExpander_Expanded(sender, e);
                }
                else if (sender == FavoriteRefresh)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        FavoriteItems.UpdateTilesImage();
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        FavoriteItems.UpdateTilesImage(true);
                    else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                        FavoriteItemsExpander_Expanded(sender, e);
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
#endif
        }

        private void ActionOpenPedia_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender == IllustTagPedia)
                {
                    var links = Keyboard.Modifiers == ModifierKeys.Shift ? true : false;
                    var shell = Keyboard.Modifiers == ModifierKeys.Control ? true : false;
                    var tags = IllustTagsHtml.GetText().Trim().Trim('#').Split('#');
                    if (links)
                        Commands.CopyPediaLink.Execute(tags);
                    else if (shell)
                        Commands.ShellOpenPixivPedia.Execute(tags);
                    else
                        Commands.OpenPedia.Execute(tags);
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
#endif
        }

        private long lastMouseDown = Environment.TickCount;
        private void IllustInfo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Timestamp - lastMouseDown > 100)
            {
                lastMouseDown = e.Timestamp;
                if (e.ChangedButton == MouseButton.Middle)
                {
                    ActionIllustInfo_Click(sender, e);
                }
                else if (e.ChangedButton == MouseButton.Left)
                {
                    ActionSpeech_Click(sender, e);
                }
                e.Handled = true;
            }
        }

        private void PreviewRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                setting = Application.Current.LoadSetting();

                if (e.ClickCount >= 2)
                {
                    if (SubIllusts.Items.Count() <= 0)
                    {
                        if (Contents.IsWork()) Commands.OpenWorkPreview.Execute(Contents);
                    }
                    else
                    {
                        if (SubIllusts.SelectedItems == null || SubIllusts.SelectedItems.Count <= 0)
                            SubIllusts.SelectedIndex = 0;
                        Commands.OpenWorkPreview.Execute(SubIllusts);
                    }
                    e.Handled = true;
                }
                else if (IsElement(btnSubPagePrev, e) && btnSubPagePrev.IsVisible && btnSubPagePrev.IsEnabled)
                {
                    PrevIllustPage();
                    e.Handled = true;
                }
                else if (IsElement(btnSubPageNext, e) && btnSubPageNext.IsVisible && btnSubPageNext.IsEnabled)
                {
                    NextIllustPage();
                    e.Handled = true;
                }
                else
                {
                    if (setting.EnabledMiniToolbar && PreviewPopup is Popup)
                    {
                        PopupOpen(PreviewPopup);
                    }
                    e.Handled = true;
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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

        #region Following User / Bookmark Illust routines
        private void IllustActions_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = false;
            if (sender is Expander)
            {
                e.Handled = !((sender as Expander).IsExpanded);
            }
            else
            {
                e.Handled = true;
                UpdateLikeState();
            }
        }

        private void ActionIllust_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            bool is_user = false;

            if (sender == BookmarkIllust)
            {
                PopupOpen(BookmarkIllust.ContextMenu);
                is_user = true;
            }
            else if (sender == FollowAuthor)
            {
                PopupOpen(FollowAuthor.ContextMenu);
                is_user = true;
            }
            else if (sender == IllustActions)
            {
                if (Window.GetWindow(this) is ContentWindow)
                    ActionIllustNewWindow.Visibility = Visibility.Collapsed;
                else
                    ActionIllustNewWindow.Visibility = Visibility.Visible;
                PopupOpen(IllustActions.ContextMenu);
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
                IList<PixivItem> items = new List<PixivItem>();
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == RelativeItems || host == RelativeItemsExpander) items = RelativeItems.GetSelected();
                else if (host == FavoriteItems || host == FavoriteItemsExpander) items = FavoriteItems.GetSelected();
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
                catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
            }
            else
            {
                if (Contents is PixivItem)
                {
                    var item = Contents;
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

                        if (item.IsSameIllust(Contents))
                        {
                            BookmarkIllust.Tag = result ? PackIconModernKind.Heart : PackIconModernKind.HeartOutline;
                            ActionBookmarkIllustRemove.IsEnabled = result;
                            item.IsFavorited = result;
                        }
                    }
                    catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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

                IList<PixivItem> items = new List<PixivItem>();
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == RelativeItems || host == RelativeItemsExpander) items = RelativeItems.GetSelected();
                else if (host == FavoriteItems || host == FavoriteItemsExpander) items = FavoriteItems.GetSelected();
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
                catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
            }
            else
            {
                if (Contents is PixivItem)
                {
                    var item = Contents;
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

                        if (item.IsSameIllust(Contents))
                        {
                            FollowAuthor.Tag = result ? PackIconModernKind.Check : PackIconModernKind.Add;
                            ActionFollowAuthorRemove.IsEnabled = result;
                            if (item.IsUser()) item.IsFavorited = result;
                        }
                    }
                    catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
                }
            }
        }
        #endregion

        #region Illust Multi-Pages related routines
        private void SubIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Contents is PixivItem)
            {
                var count_list = Math.Min(Contents.Count - page_number * PAGE_ITEMS, PAGE_ITEMS);
                var count = SubIllusts.Items.Count();
                var same = count_list > 0 && count > 0 ? Contents.IsSameIllust(SubIllusts.Items.First()) : false;
                if (!same || count <= 0 || count != count_list)
                    ShowIllustPagesAsync(Contents, page_index, page_number);
                else
                    SubIllusts.UpdateTilesImage();
            }
        }

        private void SubIllustsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            //IllustDetailWait.Hide();
        }

        DateTime lastSelectionChanged = default(DateTime);
        PixivItem lastSelectionItem = null;
        private void SubIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = false;

            if (SubIllusts.SelectedItem is PixivItem && SubIllusts.SelectedItems.Count == 1)
            {
                if (lastSelectionChanged.DeltaMilliseconds(DateTime.Now) < 50)
                {
                    SubIllusts.SelectedItem = lastSelectionItem;
                    return;
                }
                SubIllusts.UpdateTilesState(false);
                UpdateDownloadedMark(SubIllusts.SelectedItem);

                if (Contents is PixivItem)
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
                Keyboard.Focus(SubIllusts.SelectedItem);
            }
        }

        private void SubIllusts_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void SubIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed) Commands.OpenWorkPreview.Execute(SubIllusts);
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
            if (sender == SubIllustPrevPages || sender == SubIllustNextPages)
            {
                var btn = sender as Button;
                if (Contents is PixivItem)
                {
                    var illust = Contents.Illust;
                    if (illust is Pixeez.Objects.Work)
                    {
                        if (btn == SubIllustPrevPages)
                        {
                            page_number -= 1;
                            page_index = PAGE_ITEMS - 1;
                        }
                        else if (btn == SubIllustNextPages)
                        {
                            page_number += 1;
                            page_index = 0;
                        }
                        ShowIllustPagesAsync(Contents, page_index, page_number);
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
            else if (SubIllusts.SelectedItem is PixivItem)
            {
                var item = SubIllusts.SelectedItem;
                Commands.SaveIllust.Execute(item);
            }
            else if (Contents is PixivItem)
            {
                Commands.SaveIllust.Execute(Contents);
            }
        }

        private void ActionSaveAllIllust_Click(object sender, RoutedEventArgs e)
        {
            if (Contents.IsWork() && Contents.Count > 0)
                Commands.SaveIllustAll.Execute(Contents);
        }

        private void SubPageNav_Clicked(object sender, RoutedEventArgs e)
        {
            var count = SubIllusts.Items.Count;
            if (count >= 1)
            {
                var change_illust = Keyboard.Modifiers == ModifierKeys.Shift && e.OriginalSource != null;
                if (sender == btnSubPagePrev)
                {
                    if (change_illust)
                    {
                        PrevIllust();
                    }
                    else
                    {
                        if (SubIllusts.SelectedIndex > 0)
                            SubIllusts.SelectedIndex -= 1;
                        else if (SubIllusts.SelectedIndex == 0 && SubIllustPrevPages.IsShown())
                            SubIllustPagesNav_Click(SubIllustPrevPages, e);
                    }
                }
                else if (sender == btnSubPageNext)
                {
                    if (change_illust)
                    {
                        NextIllust();
                    }
                    else
                    {
                        if (SubIllusts.SelectedIndex <= 0)
                            SubIllusts.SelectedIndex = 1;
                        else if (SubIllusts.SelectedIndex < count - 1 && count > 1)
                            SubIllusts.SelectedIndex += 1;
                        else if (SubIllusts.SelectedIndex == count - 1 && SubIllustNextPages.IsShown())
                            SubIllustPagesNav_Click(SubIllustNextPages, e);
                    }
                }
                if (SubIllusts.SelectedItem is PixivItem) SubIllusts.SelectedItem.Focus();
            }
        }
        #endregion

        #region Relative Panel related routines
        private void RelativeItemsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Contents is PixivItem)
            {
                if (Contents.IsWork())
                    ShowRelativeInlineAsync(Contents);
                else if (Contents.IsUser())
                    ShowUserWorksInlineAsync(Contents.User);
            }
        }

        private void RelativeItemsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            IllustDetailWait.Hide();
        }

        private void ActionOpenRelative_Click(object sender, RoutedEventArgs e)
        {
            Commands.Open.Execute(RelativeItems);
        }

        private void ActionCopyRelativeIllustID_Click(object sender, RoutedEventArgs e)
        {
            Commands.CopyArtworkIDs.Execute(RelativeItems);
        }

        private void RelativeItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = false;
            RelativeItems.UpdateTilesState();
            UpdateLikeState();
            if (RelativeItems.SelectedItem is PixivItem) RelativeItems.SelectedItem.Focus();
            e.Handled = true;
        }

        private void RelativeItems_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void RelativeItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed) Commands.OpenWork.Execute(RelativeItems);
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private void RelativeItems_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Commands.Open.Execute(RelativeItems);
            }
        }

        private void RelativePrevPage_Click(object sender, RoutedEventArgs e)
        {

        }

        private void RelativeNextPage_Click(object sender, RoutedEventArgs e)
        {
            var next_url = string.Empty;
            if (RelativeNextPage.Tag is string)
            {
                next_url = RelativeNextPage.Tag as string;
                if (string.IsNullOrEmpty(next_url))
                    RelativeNextPage.Hide();
                else
                    RelativeNextPage.Show();
            }

            if (Contents is PixivItem)
            {
                var append = sender == RelativeNextAppend ? true : false;
                if (Contents.IsWork())
                    ShowRelativeInlineAsync(Contents, next_url, append);
                else if (Contents.IsUser())
                    ShowUserWorksInlineAsync(Contents.User, next_url, append);
            }
        }
        #endregion

        #region Author Favorite routines
        private void FavoriteItemsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Contents is PixivItem)
            {
                ShowFavoriteInlineAsync(Contents.User);
            }
        }

        private void FavoriteItemsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            FavoriteItemsExpander.Header = "Favorite";
            IllustDetailWait.Hide();
        }

        private void ActionOpenFavorite_Click(object sender, RoutedEventArgs e)
        {
            Commands.Open.Execute(FavoriteItems);
        }

        private void ActionCopyFavoriteIllustID_Click(object sender, RoutedEventArgs e)
        {
            Commands.CopyArtworkIDs.Execute(FavoriteItems);
        }

        private void FavriteIllusts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = false;
            FavoriteItems.UpdateTilesState();
            UpdateLikeState();
            if (FavoriteItems.SelectedItem is PixivItem) FavoriteItems.SelectedItem.Focus();
            e.Handled = true;
        }

        private void FavriteIllusts_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void FavriteIllusts_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed) Commands.OpenWork.Execute(FavoriteItems);
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private void FavriteIllusts_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Commands.Open.Execute(FavoriteItems);
            }
        }

        private void FavoritePrevPage_Click(object sender, RoutedEventArgs e)
        {

        }

        private void FavoriteNextPage_Click(object sender, RoutedEventArgs e)
        {
            var next_url = string.Empty;
            if (FavoriteNextPage.Tag is string)
            {
                next_url = FavoriteNextPage.Tag as string;
                if (string.IsNullOrEmpty(next_url))
                    FavoriteNextPage.Hide();
                else
                    FavoriteNextPage.Show();
            }

            if (Contents is PixivItem)
            {
                var append = sender == FavoriteNextAppend ? true : false;
                ShowFavoriteInlineAsync(Contents.User, next_url, append);
            }
        }
        #endregion

        #region Illust Comments related routines
        private async void CommentsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (Contents.IsWork())
            {
                IllustDetailWait.Show();

                try
                {
                    IllustCommentsHtml.Navigate("about:blank");

                    var result = await tokens.GetIllustComments(Contents.ID, "0", true);
                    foreach (var comment in result.comments)
                    {
                        //comment.
                    }
                }
                catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }

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
        private void MenuGallaryAction_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu)
            {
                var menu = sender as ContextMenu;
                var host = menu.PlacementTarget;
                if (host == SubIllustsExpander || host == SubIllusts)
                {
                    var start = page_number * PAGE_ITEMS;
                    var count = Contents .Count;
                    foreach (dynamic item in (sender as ContextMenu).Items)
                    {
                        try
                        {
                            if (item.Uid.Equals("ActionPrevPage", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (start > 0) (item as UIElement).Show();
                                else (item as UIElement).Hide();
                            }
                            else if (item.Uid.Equals("ActionNextPage", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (count - start > PAGE_ITEMS) (item as UIElement).Show();
                                else (item as UIElement).Hide();
                            }
                            else if (item.Uid.Equals("ActionNavPageSeparator", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (count <= PAGE_ITEMS) (item as UIElement).Hide();
                                else (item as UIElement).Show();
                            }

                            else if (item.Uid.Equals("ActionSaveIllusts", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save Selected Pages";
                            else if (item.Uid.Equals("ActionSaveIllustsAll", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save All Pages";
                        }
                        catch (Exception) { continue; }
                    }
                }
                else if (host == RelativeItemsExpander || host == RelativeItems || host == FavoriteItemsExpander || host == FavoriteItems)
                {
                    var target = host == RelativeItemsExpander || host == RelativeItems ? RelativeItemsExpander : FavoriteItemsExpander;
                    foreach (dynamic item in (sender as ContextMenu).Items)
                    {
                        try
                        {
                            if (item.Uid.Equals("ActionPrevPage", StringComparison.CurrentCultureIgnoreCase))
                            {
                                (item as UIElement).Hide();
                            }
                            else if (item.Uid.Equals("ActionNavPageSeparator", StringComparison.CurrentCultureIgnoreCase) ||
                                     item.Uid.Equals("ActionNextPage", StringComparison.CurrentCultureIgnoreCase))
                            {
                                var next_url = target.Tag as string;
                                if (string.IsNullOrEmpty(next_url))
                                    (item as UIElement).Hide();
                                else
                                    (item as UIElement).Show();
                            }

                            else if (item.Uid.Equals("ActionSaveIllusts", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save Selected Illusts (Default Page)";
                            else if (item.Uid.Equals("ActionSaveIllustsAll", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save Selected Illusts (All Pages)";
                        }
                        catch (Exception) { continue; }
                    }
                }
                else if (host == CommentsExpander || host == IllustCommentsHost)
                {
                    foreach (dynamic item in (sender as ContextMenu).Items)
                    {
                        try
                        {
                            if (item.Uid.Equals("ActionPrevPage", StringComparison.CurrentCultureIgnoreCase))
                            {
                                (item as UIElement).Show();
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
                        if (Contents is PixivItem)
                        {
                            Commands.CopyArtworkIDs.Execute(Contents);
                        }
                    }
                    else if (host == RelativeItemsExpander || host == RelativeItems)
                    {
                        Commands.CopyArtworkIDs.Execute(RelativeItems);
                    }
                    else if (host == FavoriteItemsExpander || host == FavoriteItems)
                    {
                        Commands.CopyArtworkIDs.Execute(FavoriteItems);
                    }
                    else if (host == CommentsExpander || host == IllustCommentsHost)
                    {

                    }
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private void ActionCopyWeblink_Click(object sender, RoutedEventArgs e)
        {
            UpdateLikeState();

            if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
            {
                ImageListGrid target = null;
                var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                if (host == SubIllustsExpander || host == SubIllusts || host == PreviewBox)
                    target = SubIllusts;
                else if (host == RelativeItemsExpander || host == RelativeItems)
                    target = RelativeItems;
                else if (host == FavoriteItemsExpander || host == FavoriteItems)
                    target = FavoriteItems;
                else if (host == CommentsExpander || host == IllustCommentsHost)
                {
                }

                if (target is ImageListGrid)
                {
                    if (sender.GetUid().Equals("ActionIllustWebLink", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Commands.CopyArtworkWeblinks.Execute(target);
                    }
                    else if (sender.GetUid().Equals("ActionAuthorWebLink", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Commands.CopyArtistWeblinks.Execute(target);
                    }
                }
            }

            e.Handled = true;
        }

        private void ActionOpenSelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
                {
                    var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                    if (host == SubIllustsExpander || host == SubIllusts || host == PreviewBox)
                    {
                        Commands.OpenWorkPreview.Execute(SubIllusts);
                    }
                    else if (host == RelativeItemsExpander || host == RelativeItems)
                    {
                        Commands.OpenWork.Execute(RelativeItems);
                    }
                    else if (host == FavoriteItemsExpander || host == FavoriteItems)
                    {
                        Commands.OpenWork.Execute(FavoriteItems);
                    }
                    else if (host == CommentsExpander || host == IllustCommentsHost)
                    {

                    }
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private void ActionSendToOtherInstance_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(sender == PreviewSendIllustToInstance)
                {
                    if (Contents is PixivItem)
                    {
                        if (Keyboard.Modifiers == ModifierKeys.None)
                            Commands.SendToOtherInstance.Execute(Contents);
                        else
                            Commands.ShellSendToOtherInstance.Execute(Contents);
                    }
                }
                else if(sender == PreviewSendAuthorToInstance)
                {
                    if (Contents is PixivItem)
                    {
                        var id = $"uid:{Contents.UserID}";
                        if (Keyboard.Modifiers == ModifierKeys.None)
                            Commands.SendToOtherInstance.Execute(id);
                        else
                            Commands.ShellSendToOtherInstance.Execute(id);
                    }
                }
                if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
                {
                    var host = ((sender as MenuItem).Parent as ContextMenu).PlacementTarget;
                    var uid = (sender as MenuItem).Uid;
                    if (host == SubIllustsExpander || host == SubIllusts || host == PreviewBox)
                    {
                        if (uid.Equals("ActionSendIllustToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (Contents is PixivItem)
                            {
                                if (Keyboard.Modifiers == ModifierKeys.None)
                                    Commands.SendToOtherInstance.Execute(Contents);
                                else
                                    Commands.ShellSendToOtherInstance.Execute(Contents);
                            }
                        }
                        else if (uid.Equals("ActionSendAuthorToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (Contents is PixivItem)
                            {
                                var id = $"uid:{Contents.UserID}";
                                if (Keyboard.Modifiers == ModifierKeys.None)
                                    Commands.SendToOtherInstance.Execute(id);
                                else
                                    Commands.ShellSendToOtherInstance.Execute(id);
                            }
                        }
                    }
                    else if (host == RelativeItemsExpander || host == RelativeItems)
                    {
                        if (uid.Equals("ActionSendIllustToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (Keyboard.Modifiers == ModifierKeys.None)
                                Commands.SendToOtherInstance.Execute(RelativeItems);
                            else
                                Commands.ShellSendToOtherInstance.Execute(RelativeItems);
                        }
                        else if (uid.Equals("ActionSendAuthorToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var ids = new List<string>();
                            foreach (var item in RelativeItems.GetSelected())
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
                    else if (host == FavoriteItemsExpander || host == FavoriteItems)
                    {
                        if (uid.Equals("ActionSendIllustToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (Keyboard.Modifiers == ModifierKeys.None)
                                Commands.SendToOtherInstance.Execute(FavoriteItems);
                            else
                                Commands.ShellSendToOtherInstance.Execute(FavoriteItems);
                        }
                        else if (uid.Equals("ActionSendAuthorToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var ids = new List<string>();
                            foreach (var item in FavoriteItems.GetSelected())
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
                    else if (host == CommentsExpander || host == IllustCommentsHost)
                    {

                    }
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
                        SubIllustPagesNav_Click(SubIllustPrevPages, e);
                    }
                    else if (host == RelativeItemsExpander || host == RelativeItems)
                    {

                    }
                    else if (host == FavoriteItemsExpander || host == FavoriteItems)
                    {

                    }
                    else if (host == CommentsExpander || host == IllustCommentsHost)
                    {

                    }
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private void ActionNextPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem && (sender as MenuItem).Parent is ContextMenu)
                {
                    var menuitem = sender as MenuItem;
                    var append = sender.GetUid().Equals("ActionNextAppend", StringComparison.CurrentCultureIgnoreCase) ? true : false;
                    var host = (menuitem.Parent as ContextMenu).PlacementTarget;
                    if (host == SubIllustsExpander || host == SubIllusts)
                    {
                        SubIllustPagesNav_Click(SubIllustNextPages, e);
                    }
                    else if (host == RelativeItemsExpander || host == RelativeItems)
                    {
                        RelativeNextPage_Click(append ? RelativeNextAppend : RelativeNextPage, e);
                    }
                    else if (host == FavoriteItemsExpander || host == FavoriteItems)
                    {
                        FavoriteNextPage_Click(append ? FavoriteNextAppend : FavoriteNextPage, e);
                    }
                    else if (host == CommentsExpander || host == IllustCommentsHost)
                    {

                    }
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
                            Commands.SaveIllust.Execute(SubIllusts);
                        }
                        else if (host == RelativeItemsExpander || host == RelativeItems)
                        {
                            Commands.SaveIllust.Execute(RelativeItems);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            Commands.SaveIllust.Execute(FavoriteItems);
                        }
                    }
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
                        else if (host == RelativeItemsExpander || host == RelativeItems)
                        {
                            Commands.SaveIllustAll.Execute(RelativeItems);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            Commands.SaveIllustAll.Execute(FavoriteItems);
                        }
                    }
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
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
                            Commands.OpenDownloaded.Execute(SubIllusts);
                        }
                        else if (host == RelativeItemsExpander || host == RelativeItems)
                        {
                            Commands.OpenDownloaded.Execute(RelativeItems);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            Commands.OpenDownloaded.Execute(FavoriteItems);
                        }
                    }
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        private void ActionRefreshIllusts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem)
                {
                    var m = sender as MenuItem;
                    var append = m.Uid.Equals("ActionNextAppend", StringComparison.CurrentCultureIgnoreCase) ? true : false;
                    var host = (m.Parent as ContextMenu).PlacementTarget;
                    if (m.Uid.Equals("ActionRefresh", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            if (Contents is PixivItem)
                            {
                                ShowIllustPagesAsync(Contents, page_index, page_number);
                            }
                        }
                        else if (host == RelativeItemsExpander || host == RelativeItems)
                        {
                            if (Contents is PixivItem)
                            {
                                var current_url = RelativeItemsExpander.Tag is string ? RelativeItemsExpander.Tag as string : string.Empty;
                                if (Contents.IsWork())
                                    ShowRelativeInlineAsync(Contents, current_url);
                                else if (Contents.IsUser())
                                    ShowUserWorksInlineAsync(Contents.User, current_url);
                            }
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            if (Contents is PixivItem)
                            {
                                var current_url = FavoriteItemsExpander.Tag is string ? FavoriteItemsExpander.Tag as string : string.Empty;
                                ShowFavoriteInlineAsync(Contents.User, current_url);
                            }
                        }
                    }
                    else if (m.Uid.Equals("ActionRefreshThumb", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            SubIllusts.UpdateTilesImage();
                        }
                        else if (host == RelativeItemsExpander || host == RelativeItems)
                        {
                            RelativeItems.UpdateTilesImage();
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            FavoriteItems.UpdateTilesImage();
                        }
                    }
                }
            }
            catch (Exception ex) { $"{ex.Message}{Environment.NewLine}{ex.StackTrace}".DEBUG(); }
        }

        #endregion

    }

}
