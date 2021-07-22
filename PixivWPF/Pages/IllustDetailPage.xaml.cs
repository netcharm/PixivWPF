using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

using MahApps.Metro.Controls;
using MahApps.Metro.IconPacks;
using PixivWPF.Common;
using System.Threading;

namespace PixivWPF.Pages
{
    /// <summary>
    /// IllustDetailPage.xaml 的交互逻辑
    /// </summary>
    public partial class IllustDetailPage : Page, IDisposable
    {
        private Setting setting = Application.Current.LoadSetting();

        public Window ParentWindow { get; private set; } = null;
        public PixivItem Contents { get; set; } = null;
        private PrefetchingTask PrefetchingImagesTask = null;
        private CancellationTokenSource cancelDownloading = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        private const string SymbolIcon_Followed = "\uE113";
        private const string SymbolIcon_UnFollowed = "\uE734";
        private const string SymbolIcon_Favorited = "\uEB52";
        private const string SymbolIcon_UnFavorited = "\uEB51";

        private const int PAGE_ITEMS = 30;
        private int page_count = 0;
        private int page_number = 0;
        private int page_index = 0;

        private string PreviewImageUrl { get; set; } = string.Empty;
        private string PreviewImagePath { get; set; } = string.Empty;
        private string AvatarImageUrl { get; set; } = string.Empty;

        private string CurrentRelativeURL { get; set; } = string.Empty;
        private string NextRelativeURL { get; set; } = string.Empty;

        private string CurrentFavoriteURL { get; set; } = string.Empty;
        private string NextFavoriteURL { get; set; } = string.Empty;

        #region Popup Helper
        private Popup PreviewPopup = null;
        private Rectangle PreviewPopupBackground = null;
        private IList<Button> PreviewPopupToolButtons = new List<Button>();
        private System.Timers.Timer PreviewPopupTimer = null;

        private ContextMenu ContextMenuBookmarkActions = null;
        private ContextMenu ContextMenuFollowActions = null;
        private ContextMenu ContextMenuIllustActions = null;
        private Dictionary<string, MenuItem> ContextMenuActionItems = new Dictionary<string, MenuItem>();

        private void InitPopupTimer(ref System.Timers.Timer timer)
        {
            if (timer == null)
            {
                timer = new System.Timers.Timer(5000) { AutoReset = true, Enabled = false };
                timer.Elapsed += PreviewPopupTimer_Elapsed;
            }
        }

        private void PopupOpen(Popup popup)
        {
            try
            {
                if (PreviewBox.ContextMenu is ContextMenu && PreviewBox.ContextMenu.IsOpen) PreviewBox.ContextMenu.IsOpen = false;

                if (PreviewPopupBackground is Rectangle) PreviewPopupBackground.Fill = Theme.MenuBackgroundBrush;
                foreach (var button in PreviewPopupToolButtons) button.Foreground = Theme.AccentBrush;

                PreviewPopup.IsOpen = true;
            }
            catch (Exception ex) { ex.ERROR("ShowPopup"); }
        }

        private void PopupOpen(object sender, ContextMenu menu)
        {
            try
            {
                if (sender is UIElement)
                {
                    menu.PlacementTarget = sender as UIElement;
                    menu.Placement = PlacementMode.Bottom;
                    menu.VerticalOffset = sender == IllustActions ? 0 : 4;
                }
                else
                {
                    menu.Placement = PlacementMode.Mouse;
                    menu.VerticalOffset = 0;
                }
                menu.IsOpen = true;
            }
            catch (Exception ex) { ex.ERROR("ShowContextMenu"); }
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
            catch (Exception ex) { ex.ERROR("MyHitTestResult"); }
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
            catch (Exception ex) { ex.ERROR("IsElement"); }

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
            catch (Exception ex) { ex.ERROR("GetHtmlHost"); }
            return (result);
        }

        private async void AdjustBrowserSize(System.Windows.Forms.WebBrowser browser)
        {
            try
            {
                if (browser is System.Windows.Forms.WebBrowser)
                {
                    await new Action(() =>
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
                            this.DoEvents();
                            if (browser is System.Windows.Forms.WebBrowser && 
                                browser.Document is System.Windows.Forms.HtmlDocument &&
                                browser.Document.Body is System.Windows.Forms.HtmlElement)
                            {
                                var size = browser.Document.Body.ScrollRectangle.Size;
                                var offset = browser.Document.Body.OffsetRectangle.Top;
                                if (offset <= 0) offset = 16;
                                browser.Height = Math.Min(Math.Max(size.Height, h_min), h_max) + offset;// * 2;
                            }
                        }
                        catch (Exception ex) { ex.ERROR("WEBBROWSER"); }
                    }).InvokeAsync();
                }
            }
            catch (Exception ex) { ex.ERROR("WEBBROWSER"); }
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
                        html.AppendLine($"<div class=\"tags\" data-illust=\"{item.Illust.GetType().Name}\">");
                        if (item.Illust is Pixeez.Objects.IllustWork)
                        {
                            foreach (var tag in (item.Illust as Pixeez.Objects.IllustWork).MoreTags)
                            {
                                var trans = string.IsNullOrEmpty(tag.Translated) ? tag.Original : tag.Translated;
                                string trans_match = string.Empty;
                                trans = tag.Original.TranslatedText(out trans_match, tag.Translated);
                                html.AppendLine($"<a href=\"https://www.pixiv.net/tags/{Uri.EscapeDataString(tag.Original)}/artworks?s_mode=s_tag\" class=\"tag\" title=\"{trans}\" data-tag=\"{tag.Original}\" data-trans=\"{tag.Translated}\" data-match=\"{trans_match}\" data-tooltip=\"{trans}\">#{tag.Original}</a>");
                                if (!string.IsNullOrEmpty(trans_match))
                                    $"{Contents.ID}, {tag.Original} -> {tag.Translated} : {trans_match.Replace("'", "\"")}".DEBUG("IllustTagTranslate");
                            }
                        }
                        else
                        {
                            foreach (var tag in item.Illust.Tags)
                            {
                                string trans_match = string.Empty;
                                var trans = tag.TranslatedText(out trans_match);
                                html.AppendLine($"<a href=\"https://www.pixiv.net/tags/{Uri.EscapeDataString(tag)}/artworks?s_mode=s_tag\" class=\"tag\" title=\"{trans}\" data-tag=\"{tag}\" data-match=\"{trans_match}\" data-tooltip=\"{trans}\">#{tag}</a>");
                                if (!string.IsNullOrEmpty(trans_match))
                                    $"{Contents.ID}, {tag} : {trans_match.Replace("'", "\"")}".DEBUG("IllustTagTranslate");
                            }
                        }
                        html.AppendLine("</div>");
                        result = html.ToString().Trim().GetHtmlFromTemplate(item.Illust.Title);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("ILLUSTTAGS"); }
            return (result);
        }

        private string MakeIllustDescHtml(PixivItem item)
        {
            var result = string.Empty;
            try
            {
                if (item.IsWork() && !string.IsNullOrEmpty(item.Illust.Caption))
                {
                    var contents = item.Illust.Caption.HtmlDecode();
                    contents = $"<div class=\"desc\">{Environment.NewLine}{contents.Trim()}{Environment.NewLine}</div>";
                    result = contents.GetHtmlFromTemplate(item.Illust.Title);
                }
            }
            catch (Exception ex) { ex.ERROR("ILLUSTDESC"); }
            return (result);
        }

        private string MakeUserInfoHtml(Pixeez.Objects.UserInfo info)
        {
            var result = string.Empty;
            try
            {
                if (info is Pixeez.Objects.UserInfo)
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
            }
            catch (Exception ex) { ex.ERROR("USERINFO"); }
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
            catch (Exception ex) { ex.ERROR("USERDESC"); }
            return (result);
        }

        private void InitHtmlRenderHost(out WindowsFormsHostEx host, WebBrowserEx browser, Panel panel)
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
                    //AdjustBrowserSize(browser);
                }
            }
            catch (Exception ex) { ex.ERROR(); host = null; }
        }

        private void InitHtmlRender(out WebBrowserEx browser)
        {
            browser = null;
            try
            {
                var bg = Theme.WhiteColor;
                browser = new WebBrowserEx()
                {
                    BackColor = System.Drawing.Color.FromArgb(0xFF, bg.R, bg.G, bg.B),
                    Height = 0,
                    DocumentText = string.Empty.GetHtmlFromTemplate(),
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    ScriptErrorsSuppressed = true,
                    IgnoreAllError = true,
                    WebBrowserShortcutsEnabled = false,
                    AllowNavigation = true,
                    AllowWebBrowserDrop = false
                };
                //browser.Navigate("about:blank");
                //browser.Document.Write(string.Empty);

                if (browser is WebBrowserEx)
                {
                    browser.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(WebBrowser_DocumentCompleted);
                    browser.Navigating += new System.Windows.Forms.WebBrowserNavigatingEventHandler(WebBrowser_Navigating);
                    browser.Navigated += new System.Windows.Forms.WebBrowserNavigatedEventHandler(WebBrowser_Navigated);
                    browser.ProgressChanged += new System.Windows.Forms.WebBrowserProgressChangedEventHandler(WebBrowser_ProgressChanged);
                    browser.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(WebBrowser_PreviewKeyDown);
                }
            }
            catch (Exception ex) { ex.ERROR("CREATEBROWSER"); }
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
            catch (Exception ex) { ex.ERROR("CreateHtmlRender"); }
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
            catch (Exception ex) { ex.ERROR("DeleteHtmlRender"); }
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
                            if (Contents.IsUser() && UserInfo is Pixeez.Objects.UserInfo)
                                contents = MakeUserInfoHtml(UserInfo);
                            else if (Contents.IsWork())
                                contents = MakeIllustTagsHtml(Contents);
                        }
                        else if (browser == IllustDescHtml)
                        {
                            if (Contents.IsUser() && UserInfo is Pixeez.Objects.UserInfo)
                                contents = MakeUserDescHtml(UserInfo);
                            else if (Contents.IsWork())
                                contents = MakeIllustDescHtml(Contents);
                        }
                        if (!string.IsNullOrEmpty(contents))
                        {
                            browser.DocumentText = contents;
                            browser.Document.Write(string.Empty);
                            //AdjustBrowserSize(browser);
                        }
                        browser.WebBrowserShortcutsEnabled = false;
                    }).InvokeAsync();
                }
            }
            catch (Exception ex) { ex.ERROR("RefreshHtmlRender"); }
        }
        #endregion

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
            catch (Exception ex) { ex.ERROR("BROWSER"); }
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
                                    var url = string.Empty;
                                    try
                                    {
                                        if (src.ToLower().Contains("no_image_p.svg"))
                                        {
                                            url = System.IO.Path.Combine(Application.Current.GetRoot(), "no_image.png");
                                            imgElemt.SetAttribute("src", new Uri(url).AbsoluteUri);
                                        }
                                        else if (src.IsPixivImage())
                                        {
                                            using (var img = await src.LoadImageFromUrl())
                                            {
                                                url = img.SourcePath;
                                                if (!string.IsNullOrEmpty(url)) imgElemt.SetAttribute("src", new Uri(url).AbsoluteUri);
                                            }
                                        }
                                    }
                                    catch (Exception ex) { ex.ERROR("BROWSER"); url.ERROR("BROWSER"); }
                                }).InvokeAsync();
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("BROWSER"); }
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
                catch (Exception ex) { ex.ERROR("BROWSER"); }
            }
        }

        private void WebBrowser_Navigated(object sender, System.Windows.Forms.WebBrowserNavigatedEventArgs e)
        {
            try
            {
            }
            catch (Exception ex) { ex.ERROR("BROWSER"); }
        }

        private void WebBrowser_DocumentCompleted(object sender, System.Windows.Forms.WebBrowserDocumentCompletedEventArgs e)
        {
            try
            {
                if (sender == IllustDescHtml || sender == IllustTagsHtml)
                {
                    var browser = sender as System.Windows.Forms.WebBrowser;
                    //browser.Document.MouseDown += new System.Windows.Forms.HtmlElementEventHandler(WebBrowserDocument_MouseDown);

                    var document = browser.Document;
                    foreach (System.Windows.Forms.HtmlElement link in document.Links)
                    {
                        try
                        {
                            link.Click += WebBrowser_LinkClick;
                        }
                        catch (Exception ex) { ex.ERROR(); continue; }
                    }
                    AdjustBrowserSize(browser);
                }
            }
            catch (Exception ex) { ex.ERROR("WEBBROWSER"); }
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
                    else if (e.KeyCode == System.Windows.Forms.Keys.XButton1)
                    {
                        NextIllust();
                    }
                    else if (e.KeyCode == System.Windows.Forms.Keys.XButton2)
                    {
                        PrevIllust();
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("BROWSER"); }
        }
        #endregion

        #region Illust/User info relative methods
        public void UpdateIllustTitle()
        {
            try
            {
                if (Contents.IsWork())
                {
                    string trans_match = string.Empty;
#if DEBUG
                    IllustTitle.ToolTip = IllustTitle.Text.TranslatedText(out trans_match) + (string.IsNullOrEmpty(trans_match) ? string.Empty : $"{Environment.NewLine}Matched: {trans_match}");
#else
                    IllustTitle.ToolTip = IllustTitle.Text.TranslatedText(out trans_match);
#endif
                    if (!string.IsNullOrEmpty(trans_match)) $"{Contents.ID}, {trans_match}".DEBUG("IllustTitleTranslate");
                }
            }
            catch (Exception ex) { ex.ERROR("UpdateIllustTags"); }
        }

        public void UpdateIllustTags()
        {
            try
            {
                if (Contents.IsWork())
                {
                    RefreshHtmlRender(IllustTagsHtml);
                    UpdateIllustTitle();
                }
            }
            catch (Exception ex) { ex.ERROR("UpdateIllustTags"); }
        }

        public void UpdateIllustDesc()
        {
            try
            {
                RefreshHtmlRender(IllustDescHtml);
            }
            catch (Exception ex) { ex.ERROR("UpdateIllustDesc"); }
        }

        public void UpdateWebContent()
        {
            try
            {
                RefreshHtmlRender(IllustTagsHtml);
                RefreshHtmlRender(IllustDescHtml);
            }
            catch (Exception ex) { ex.ERROR("UpdateWebContent"); }
        }
        #endregion

        #region Illust/User state mark methods
        private async void MakeUgoiraConcatFile(Pixeez.Objects.UgoiraInfo ugoira_info = null, string file = null)
        {
            if (Contents.IsUgoira())
            {
                var ugoira = ugoira_info ?? Contents.Ugoira;
                var fp =  file ?? await Contents.GetUgoiraFile();
                if (!string.IsNullOrEmpty(fp) && ugoira != null)
                {
                    var fn = System.IO.Path.ChangeExtension(fp, ".txt");
                    if (!System.IO.File.Exists(fn))
                    {
                        List<string> lines = new List<string>();
                        foreach (var frame in Contents.Ugoira.Frames)
                        {
                            lines.Add($"file '{frame.File}'");
                            lines.Add($"duration {Math.Max(0.04, frame.Delay / 1000.0):F2}");
                        }
                        System.IO.File.WriteAllLines(fn, lines);
                    }
                }
            }
        }

        private void UpdateUg(bool? ugoira = null)
        {
            var is_ugoira = ugoira ?? Contents.IsUgoira();
            new Action(async () =>
            {
                IllustUgoiraDownloaded.Show(show: is_ugoira);
                IllustUgoiraDownloaded.IsEnabled = false;

                string tooltip = string.Empty;
                if (is_ugoira)
                {
                    var ugoira_info = Contents.Ugoira != null ? Contents.Ugoira : await Contents.GetUgoiraMeta(ajax: true);
                    if (ugoira_info != null)
                    {
                        if (Contents.Ugoira == null) Contents.Ugoira = ugoira_info;

                        var frame = ugoira_info.Frames.Count;
                        var delay = frame > 0 ? ugoira_info.Frames.Sum(f => f.Delay) / frame : 0;
                        var file_p = System.IO.Path.GetFileName(ugoira_info.GetUgoiraUrl(preview: true));
                        var file_o = System.IO.Path.GetFileName(ugoira_info.GetUgoiraUrl(preview: false));
                        List<string> tips = new List<string>();
                        tips.Add($"Preview  : {file_p}");
                        tips.Add($"Original : {file_o}");
                        tips.Add($"Frames   : {frame}");
                        tips.Add($"Times    : {delay * frame / 1000.0:F2} s");
                        tooltip = string.Join(Environment.NewLine, tips);                        
                    }
                }

                if (ContextMenuIllustActions is ContextMenu && ContextMenuIllustActions.HasItems)
                {
                    foreach (var item in ContextMenuIllustActions.Items)
                    {
                        if (item is UIElement && (item as UIElement).Uid.Equals("SepratorUgoira"))
                        {
                            (item as UIElement).Show(show: is_ugoira);
                            break;
                        }
                    }
                }

                if (ContextMenuActionItems.ContainsKey("ActionOpenUgoiraFile"))
                {
                    var mi = ContextMenuActionItems["ActionOpenUgoiraFile"];
                    var fp = is_ugoira ? await Contents.GetUgoiraFile() : string.Empty;
                    mi.Show(show: is_ugoira);
                    mi.IsEnabled = is_ugoira && !string.IsNullOrEmpty(fp);
                    mi.ToolTip = string.IsNullOrEmpty(fp) ? tooltip : fp;
                    if (string.IsNullOrEmpty(fp))
                    {
                        IllustUgoiraDownloaded.Tag = null;
                        IllustUgoiraDownloaded.ToolTip = tooltip;
                    }
                    else
                    {
                        IllustUgoiraDownloaded.Tag = fp;
                        IllustUgoiraDownloaded.ToolTip = string.Join(Environment.NewLine, new string[] { tooltip, fp });
                    }
                    IllustUgoiraDownloaded.IsEnabled = is_ugoira;
                    if(is_ugoira) MakeUgoiraConcatFile(file: fp);
                }
                if (ContextMenuActionItems.ContainsKey("ActionSavePreviewUgoiraFile"))
                {
                    var mi = ContextMenuActionItems["ActionSavePreviewUgoiraFile"];
                    mi.Show(show: is_ugoira);
                    mi.ToolTip = is_ugoira ? tooltip : null;
                }
                if (ContextMenuActionItems.ContainsKey("ActionSaveOriginalUgoiraFile"))
                {
                    var mi = ContextMenuActionItems["ActionSaveOriginalUgoiraFile"];
                    mi.Show(show: is_ugoira);
                    mi.ToolTip = is_ugoira ? tooltip : null;
                }
            }).Invoke();
        }

        private void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            try
            {
                if (Contents.IsWork())
                {
                    int work_id = -1;
                    int.TryParse(Contents.ID, out work_id);
                    if (illustid == -1 || illustid == work_id) UpdateDownloadedMark(Contents, exists);
                    UpdateUg();
                }
                foreach (var illusts in new List<ImageListGrid>() { SubIllusts, RelativeItems, FavoriteItems })
                {
                    if (illusts.Items.Count > 0)
                    {                        
                        illusts.UpdateTilesState(id: illustid);
                        this.DoEvents();
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("DOWNLOADSTATE"); }
        }

        public async void UpdateDownloadStateAsync(int? illustid = null, bool? exists = null)
        {
            try
            {
                if (Contents.IsWork())
                {
                    UpdateDownloadedMark(Contents);
                    SubIllusts.UpdateTilesState();
                    this.DoEvents();
                }

                await new Action(() =>
                {
                    UpdateDownloadState(illustid, exists);
                }).InvokeAsync();
            }
            catch (Exception ex) { ex.ERROR("UpdateDownloadState"); }
        }

        private void UpdateDownloadedMark()
        {
            try
            {
                if (Contents.IsWork())
                {
                    UpdateDownloadedMark(Contents);
                }
            }
            catch (Exception ex) { ex.ERROR("UpdateDownloadedMark"); }
        }

        private void UpdateDownloadedMark(PixivItem item, bool? exists = null, bool? downloaded=null)
        {
            try
            {
                if (item.IsWork())
                {
                    string fp = string.Empty;
                    var index = item.Index;
                    if (index < 0)
                    {
                        var download = downloaded ?? item.Illust.IsPartDownloadedAsync(out fp, touch: true);
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
                        var download = downloaded ?? item.Illust.GetOriginalUrl(item.Index).IsDownloadedAsync(out fp, item.Illust.PageCount <= 1, touch: true);
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
            catch (Exception ex) { ex.ERROR("DOWNLOADMARK"); }
            finally { UpdateContentsThumbnail(item); }
        }

        private void UpdateFavMark(Pixeez.Objects.Work illust)
        {
            try
            {
                if (illust.IsLiked())
                {
                    //BookmarkIllustAlt.Tag = SymbolIcon_Favorited; // &#xEB52;
                    BookmarkIllust.Tag = SymbolIcon_Favorited; // &#xEB52;
                    if (ContextMenuActionItems.ContainsKey("ActionBookmarkIllustRemove"))
                        ContextMenuActionItems["ActionBookmarkIllustRemove"].IsEnabled = true;
                }
                else
                {
                    //BookmarkIllustAlt.Tag = SymbolIcon_UnFavorited; // &#xEB51;
                    BookmarkIllust.Tag = SymbolIcon_UnFavorited; // &#xEB51;
                    if (ContextMenuActionItems.ContainsKey("ActionBookmarkIllustRemove"))
                        ContextMenuActionItems["ActionBookmarkIllustRemove"].IsEnabled = false;
                }
                this.DoEvents();
            }
            catch (Exception ex) { ex.ERROR("FAVMARK"); }
        }

        private void UpdateFollowMark(Pixeez.Objects.UserBase user)
        {
            try
            {
                if (user.IsLiked())
                {
                    FollowAuthor.Tag = SymbolIcon_Followed; // &#xE113;
                    if (ContextMenuActionItems.ContainsKey("ActionFollowAuthorRemove"))
                        ContextMenuActionItems["ActionFollowAuthorRemove"].IsEnabled = true;
                }
                else
                {
                    FollowAuthor.Tag = SymbolIcon_UnFollowed; // &#xE734;
                    if (ContextMenuActionItems.ContainsKey("ActionFollowAuthorRemove"))
                        ContextMenuActionItems["ActionFollowAuthorRemove"].IsEnabled = false;
                }
                this.DoEvents();
            }
            catch (Exception ex) { ex.ERROR("FOLLOWMARK"); }
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
                if (Contents.HasUser())
                {
                    int user_id = -1;
                    int.TryParse(Contents.UserID, out user_id);
                    UpdateFollowMark(Contents.User);
                    if (Contents.IsWork())
                    {
                        int work_id = -1;
                        int.TryParse(Contents.ID, out work_id);
                        if(illustid == -1 || illustid == work_id) UpdateFavMark(Contents.Illust);
                    }
                }
                foreach (var illusts in new List<ImageListGrid>() { SubIllusts, RelativeItems, FavoriteItems })
                {
                    if (illusts.Items.Count > 0)
                    {
                        illusts.UpdateLikeState(illustid, is_user);
                        this.DoEvents();
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("LIKESTATE"); }
            finally
            {
                UpdateContentsThumbnail(Contents);
            }
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
            catch (Exception ex) { ex.ERROR("ChangeIllustLikeState"); }
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
            catch (Exception ex) { ex.ERROR("ChangeUserLikeState"); }
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
            catch (Exception ex) { ex.ERROR("OpenUser"); }
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
            catch (Exception ex) { ex.ERROR("OpenIllust"); }
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
                        if (string.IsNullOrEmpty(PreviewImagePath))
                            Commands.OpenCachedImage.Execute(Contents);
                        else
                            Commands.OpenCachedImage.Execute(PreviewImagePath);
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
                        if (string.IsNullOrEmpty(PreviewImagePath))
                            Commands.OpenCachedImage.Execute(SubIllusts.Items[0]);
                        else
                            Commands.OpenCachedImage.Execute(PreviewImagePath);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("OpenCachedImage"); }
        }

        public void OpenImageProperties()
        {
            try
            {
                if (RelativeItems.IsKeyboardFocusWithin)
                    Commands.OpenFileProperties.Execute(RelativeItems);
                else if (FavoriteItems.IsKeyboardFocusWithin)
                    Commands.OpenFileProperties.Execute(FavoriteItems);
                else if (Contents.IsWork())
                {
                    var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

                    if (SubIllusts.Items.Count <= 0)
                    {
                        if (alt)
                            Commands.OpenFileProperties.Execute(PreviewImagePath);
                        else
                            Commands.OpenFileProperties.Execute(Contents);
                    }
                    else if (SubIllusts.SelectedItems.Count > 0)
                    {
                        foreach (var item in SubIllusts.GetSelected())
                        {
                            if (alt)
                                Commands.OpenFileProperties.Execute(Contents.Illust.GetPreviewUrl(large: setting.ShowLargePreview).GetImageCacheFile());
                            else
                                Commands.OpenFileProperties.Execute(Contents);
                        }
                    }
                    else
                    {
                        if (alt)
                            Commands.OpenFileProperties.Execute(PreviewImagePath);
                        else
                            Commands.OpenFileProperties.Execute(SubIllusts.Items[0]);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("OpenImageProperties"); }
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
            catch (Exception ex) { ex.ERROR("SaveIllust"); }
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
            catch (Exception ex) { ex.ERROR("SaveIllustAll"); }
        }

        internal void SaveUgoira()
        {
            //throw new NotImplementedException();
        }

        public void CopyPreview(bool loadfromfile = false)
        {
            if (!string.IsNullOrEmpty(PreviewImageUrl))
            {
                if (loadfromfile || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                    Commands.CopyImage.Execute(PreviewImageUrl.GetImageCachePath());
                else
                    Commands.CopyImage.Execute(Preview);
            }
        }

        public void Copy()
        {
            var sep = @"--------------------------------------------------------------------------------------------";
            if (IllustTagsHtml.Focused) Commands.CopyText.Execute(IllustTagsHtml.GetText());
            else if (IllustDescHtml.Focused) Commands.CopyText.Execute(IllustDescHtml.GetText());
            else if (Preview.IsFocused || Preview.IsKeyboardFocusWithin || Preview.IsKeyboardFocusWithin ||
                    PreviewBox.IsFocused || PreviewBox.IsKeyboardFocusWithin || PreviewBox.IsKeyboardFocusWithin ||
                    PreviewRect.IsFocused || PreviewRect.IsKeyboardFocusWithin || PreviewRect.IsKeyboardFocusWithin ||
                    PreviewViewbox.IsFocused || PreviewViewbox.IsKeyboardFocusWithin || PreviewViewbox.IsKeyboardFocusWithin ||
                    PreviewViewer.IsFocused || PreviewViewer.IsKeyboardFocusWithin || PreviewViewer.IsKeyboardFocusWithin)
            {
                CopyPreview(loadfromfile: true);
            }
            else if (RelativeItems.IsFocused || RelativeItems.IsKeyboardFocusWithin || RelativeItems.IsKeyboardFocused)
            {
                List<string> info = new List<string>();
                foreach (var item in RelativeItems.GetSelected(WithSelectionOrder: false, NonForAll: true))
                {
                    info.Add($"ID:{item.ID}, UID:{item.UserID}");
                    info.Add($"INFO: {item.ToolTip}");
                    info.Add(sep);
                }
                if (info.Count > 0) info.Insert(0, sep);
                Commands.CopyText.Execute(string.Join(Environment.NewLine, info));
            }
            else if (FavoriteItems.IsFocused || FavoriteItems.IsKeyboardFocusWithin || FavoriteItems.IsKeyboardFocused)
            {
                List<string> info = new List<string>();
                foreach (var item in FavoriteItems.GetSelected(WithSelectionOrder: false, NonForAll: true))
                {
                    info.Add($"ID:{item.ID}, UID:{item.UserID}");
                    info.Add($"INFO: {item.ToolTip}");
                    info.Add(sep);
                }
                if (info.Count > 0) info.Insert(0, sep);
                Commands.CopyText.Execute(string.Join(Environment.NewLine, info));
            }
            else
            {
                if (Contents.IsWork())
                {
                    List<string> info = new List<string>();
                    info.Add($"ID:{Contents.ID}, UID:{Contents.UserID}");
                    info.Add($"INFO: {Contents.ToolTip}");
                    Commands.CopyText.Execute(string.Join(Environment.NewLine, info));
                }
                else if (Contents.IsUser())
                {
                    List<string> info = new List<string>();
                    info.Add(IllustTagsHtml.GetText());
                    info.Add(IllustDescHtml.GetText());
                    Commands.CopyText.Execute(string.Join(Environment.NewLine, info));
                }
            }
        }
        #endregion

        #region Theme/Thumb/Detail refresh methods
        private void InitPrefetchingTask()
        {
            if (PrefetchingImagesTask == null) PrefetchingImagesTask = new PrefetchingTask()
            {
                Name = "DetailPagePrefetching",
                ReportProgressSlim = () =>
                {
                    var percent = PrefetchingImagesTask.Percentage;
                    var tooltip = PrefetchingImagesTask.Comments;
                    var state = PrefetchingImagesTask.State;
                    if (ParentWindow is MainWindow) (ParentWindow as MainWindow).SetPrefetchingProgress(percent, tooltip, state);
                    if (ParentWindow is ContentWindow) (ParentWindow as ContentWindow).SetPrefetchingProgress(percent, tooltip, state);
                    if (state == TaskStatus.RanToCompletion || state == TaskStatus.Faulted || state == TaskStatus.Canceled) UpdateThumb(prefetching: false);
                },
                ReportProgress = (percent, tooltip, state) =>
                {
                    if (ParentWindow is MainWindow) (ParentWindow as MainWindow).SetPrefetchingProgress(percent, tooltip, state);
                    if (ParentWindow is ContentWindow) (ParentWindow as ContentWindow).SetPrefetchingProgress(percent, tooltip, state);
                    if (state == TaskStatus.RanToCompletion || state == TaskStatus.Faulted || state == TaskStatus.Canceled) UpdateThumb(prefetching: false);
                }
            };
        }

        public void UpdateTheme()
        {
            try
            {
                if (Contents.IsWork())
                {
                    UpdateWebContent();
                    btnSubPagePrev.Enable(btnSubPagePrev.IsEnabled, btnSubPagePrev.IsVisible);
                    btnSubPageNext.Enable(btnSubPageNext.IsEnabled, btnSubPageNext.IsVisible);
                }
                else if (Contents.IsUser() && UserInfo is Pixeez.Objects.UserInfo)
                {
                    UpdateWebContent();
                }

                FollowAuthor.BorderBrush = Theme.AccentBrush;
                FollowAuthor.Foreground = Theme.AccentBrush;
                BookmarkIllust.BorderBrush = Theme.AccentBrush;
                BookmarkIllust.Foreground = Theme.AccentBrush;
                IllustActions.BorderBrush = Theme.AccentBrush;
                IllustActions.Foreground = Theme.AccentBrush;
            }
            catch (Exception ex) { ex.ERROR(System.Reflection.MethodBase.GetCurrentMethod().Name); }
        }

        private async void UpdateContentsThumbnail(PixivItem item = null, bool overwrite = false)
        {
            try
            {
                if (item == null) item = Contents;
                if (item.Source == null)
                {
                    using (var thumb = await item.Thumb.LoadImageFromUrl(size: Application.Current.GetDefaultThumbSize()))
                    {
                        if (thumb.Source != null)
                        {
                            item.Source = thumb.Source;
                            item.State = TaskStatus.RanToCompletion;
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(System.Reflection.MethodBase.GetCurrentMethod().Name); }
        }

        public async void UpdateThumb(bool full = false, bool overwrite = false, bool prefetching = true)
        {
            overwrite = Keyboard.Modifiers == ModifierKeys.Alt ? true : overwrite;
            await new Action(async () =>
            {
                try
                {
                    if (Contents.HasUser())
                    {
                        InitPrefetchingTask();
                        if (prefetching && ParentWindow is ContentWindow && PrefetchingImagesTask is PrefetchingTask)
                        {
                            var items = new List<PixivItem>();
                            if (Contents.Count <= 1 || Contents.IsUser()) items.Add(Contents);
                            else if(Contents.Count <= 30) items.AddRange(SubIllusts.Items.Where(p => p.Index != Contents.Index));
                            else items.AddRange((await Contents.Illust.PageItems(touch: true)).Where(p => p.Index != Contents.Index));
                            items.AddRange(RelativeItems.Items);
                            items.AddRange(FavoriteItems.Items);
                            items = items.Distinct().ToList();
                            if (items.Count > 0)
                            {
                                PrefetchingImagesTask.Items = items;
                                PrefetchingImagesTask.Start(overwrite: overwrite);
                            }
                        }

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
                            if (SubIllustsExpander.IsKeyboardFocusWithin || SubIllusts.IsKeyboardFocusWithin)
                                SubIllusts.UpdateTilesImage(overwrite);
                            else if (RelativeItemsExpander.IsKeyboardFocusWithin || RelativeItems.IsKeyboardFocusWithin)
                                RelativeItems.UpdateTilesImage(overwrite);
                            else if (FavoriteItemsExpander.IsKeyboardFocusWithin || FavoriteItems.IsKeyboardFocusWithin)
                                FavoriteItems.UpdateTilesImage(overwrite);
                            else
                                UpdateThumb(true, prefetching: false);
                        }
                        UpdateContentsThumbnail(overwrite: overwrite);
                    }
                }
                catch (Exception ex) { ex.ERROR("UPATETHUMB"); }
                finally
                {
                    IllustDetailWait.Hide();
                    this.DoEvents();
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
                    PrefetchingImagesTask.Name = $"IllustPagePrefetching_{item.ID}";
                    Contents = item;
                    await new Action(async () =>
                    {
                        //IllustDetailWait.Show();
                        if (force)
                        {
                            var illust = await item.ID.RefreshIllust();
                            if (illust is Pixeez.Objects.Work)
                                item.Illust = illust;
                            else
                                "Illust not exists or deleted".ShowToast("INFO");
                        }
                        UpdateDetailIllust(item);
                    }).InvokeAsync(true);
                }
                else if (item.IsUser())
                {
                    PrefetchingImagesTask.Name = $"UserPagePrefetching_{item.ID}";
                    Contents = item;
                    await new Action(async () =>
                    {
                        //IllustDetailWait.Show();
                        if (force)
                        {
                            var user = await item.UserID.RefreshUser();
                            if (user is Pixeez.Objects.User)
                                item.User = user;
                            else
                                "User not exists or deleted".ShowToast("INFO");
                        }
                        UpdateDetailUser(item, force);
                    }).InvokeAsync();
                }
                IllustDetailViewer.ScrollToTop();
            }
            catch (Exception ex)
            {
                ex.ERROR(System.Reflection.MethodBase.GetCurrentMethod().Name);
                IllustDetailWait.Hide();
            }
            finally
            {
                //UpdateContentsThumbnail();
                //if (Contents.HasUser()) Application.Current.GC(this.Name ?? "IllustDetailPage");
            }
        }

        private void UpdateDetailIllust(PixivItem item)
        {
            try
            {
                //IllustDetailWait.Show();
                //this.DoEvents();
                item.AddToHistory();
                this.DoEvents();

                PreviewViewer.Show(true);
                PreviewBox.ToolTip = item.ToolTip;

                //Preview.Dispose();
                Preview.Source = Application.Current.GetNullPreview();
                PreviewImagePath = string.Empty;

                UpdateUg(item.IsUgoira());

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

                if (item.Count <= 1) UpdateDownloadedMark();

                IllustSize.Text = $"{item.Illust.Width}x{item.Illust.Height}";
                IllustViewed.Text = stat_viewed;
                IllustFavorited.Text = stat_favorited;

                IllustStatInfo.Show();
                IllustStatInfo.ToolTip = string.Join("\r", stat_tip).Trim();

                IllustAuthor.Text = item.Illust.User.Name;
                IllustAuthor.ToolTip = item.UserID.ArtistLink();
                IllustAuthorAvatar.Source = Application.Current.GetNullAvatar();
                AuthorAvatarWait.Show();

                IllustTitle.Text = $"{item.Illust.Title}";

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

                if (ContextMenuActionItems.ContainsKey("ActionCopyIllustDate"))
                    ContextMenuActionItems["ActionCopyIllustDate"].Header = item.Illust.GetDateTime().ToString("yyyy-MM-dd HH:mm:sszzz");

                FollowAuthor.Show();
                UpdateFollowMark(item.Illust.User);

                //BookmarkIllustAlt.Show();
                BookmarkIllust.Show();
                UpdateFavMark(item.Illust);

                IllustActions.Show();

                if (item.Illust.Tags.Count > 0)
                {
                    UpdateIllustTags();

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
                    UpdateIllustDesc();

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

                PreviewBadge.Badge = $"1 / {item.Count}";
                if (item.IsWork() && item.Illust.PageCount > 1)
                {
                    var total = item.Illust.PageCount;
                    page_count = (total / PAGE_ITEMS + (total % PAGE_ITEMS > 0 ? 1 : 0)).Value;

                    item.Index = 0;
                    PreviewBadge.Show();
                    SubIllustsExpander.Show();
                    if (SubIllustsExpander.IsExpanded)
                        ShowIllustPagesAsync(item);
                    else
                        SubIllustsExpander.IsExpanded = true;
                }
                else
                {
                    SubIllusts.ClearAsync(setting.BatchClearThumbnails);
                    SubIllustsExpander.IsExpanded = false;
                    SubIllustsExpander.Hide();
                    PreviewBadge.Hide();
                    page_count = 0;
                }
                UpdateSubPageNav();

                RelativeItems.ClearAsync(setting.BatchClearThumbnails);
                RelativeItemsExpander.Header = "Related Illusts";
                RelativeItemsExpander.IsExpanded = false;
                RelativeItemsExpander.Show();
                RelativeNextPage.Hide();

                FavoriteItems.ClearAsync(setting.BatchClearThumbnails);
                FavoriteItemsExpander.Header = "Author Favorite";
                FavoriteItemsExpander.IsExpanded = false;
                FavoriteItemsExpander.Show();
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
            catch (OperationCanceledException ex) { ex.ERROR(System.Reflection.MethodBase.GetCurrentMethod().Name); }
            catch (ObjectDisposedException ex) { ex.ERROR(System.Reflection.MethodBase.GetCurrentMethod().Name); }
            catch (Exception ex) { ex.ERROR(System.Reflection.MethodBase.GetCurrentMethod().Name); }
            finally
            {
                this.DoEvents();
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
                        using (var bg = await user_backgroundimage_url.LoadImageFromUrl(overwrite))
                        {
                            if (bg.Source == null) PreviewWait.Fail();
                            else PreviewWait.Hide();
                            Preview.Dispose();
                            Preview.Source = bg.Source;
                        }
                    }
                    catch (Exception ex) { ex.ERROR(); PreviewWait.Fail(); }
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
                item.AddToHistory();
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
                IllustViewedIcon.Kind = PackIconModernKind.Star;
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

                SubIllusts.Clear(setting.BatchClearThumbnails);
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
                this.DoEvents();
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
                if (item.HasPages())
                {
                    SubIllusts.Clear(setting.BatchClearThumbnails);
                    this.DoEvents();

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
                            if (illust is Pixeez.Objects.Work) item.Illust = illust;
                        }
                        if (item.Illust.Metadata is Pixeez.Objects.Metadata)
                        {
                            var pages = item.Illust.Metadata.Pages.Skip(idx).Take(count).ToList();
                            for (var i = 0; i < pages.Count; i++)
                            {
                                var p = pages[i];
                                p.AddTo(SubIllusts.Items, item.Illust, i + idx, item.NextURL);
                                this.DoEvents();
                            }
                        }
                    }
                    this.DoEvents();
                    index = Math.Max(0, Math.Min(index, SubIllusts.ItemsCount - 1));
                    if (SubIllusts.SelectedIndex != index) SubIllusts.SelectedIndex = index;
                    UpdateDownloadedMark(SubIllusts.SelectedItem);

                    this.DoEvents();
                    if (ParentWindow is MainWindow) SubIllusts.UpdateTilesImage();
                    else if (ParentWindow is ContentWindow) UpdateThumb();
                }
            }
            catch (Exception ex)
            {
                ex.ERROR(this.Name ?? "SubIllusts");
            }
            finally
            {
                SubIllusts.Ready();
                this.DoEvents();
            }
        }

        private async void ShowIllustPagesAsync(PixivItem item, int index = 0, int page = 0, int count = 30, bool force = false)
        {
            await new Action(async () =>
            {
                if (Contents.IsSameIllust(item))
                {
                    if (await SubIllusts.CanAdd(force)) await ShowIllustPages(item, index, page, count);
                }
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
                    RelativeItems.Clear(setting.BatchClearThumbnails);
                    relative_illusts.Clear();
                }

                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                var lastUrl = next_url;
                if (string.IsNullOrEmpty(next_url)) FavoriteNextPage.ToolTip = null;
                var relatives = string.IsNullOrEmpty(next_url) ? await tokens.GetRelatedWorks(item.Illust.Id.Value) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = relatives.next_url ?? string.Empty;

                if (relatives.illusts is Array)
                {
                    RelativeNextPage.Show(!next_url.Equals(lastUrl, StringComparison.CurrentCultureIgnoreCase));

                    if (!append)
                    {
                        RelativeItemsExpander.Tag = lastUrl;
                        CurrentRelativeURL = lastUrl;
                    }
                    NextRelativeURL = next_url;
                    RelativeNextPage.Tag = next_url;
                    RelativeNextPage.ToolTip = next_url.CalcUrlPageHint(0, RelativeNextPage.ToolTip is string ? RelativeNextPage.ToolTip as string : null);
                    RelativeNextPage.Show(show: !string.IsNullOrEmpty(CurrentRelativeURL) || !string.IsNullOrEmpty(next_url));
                    RelativeNextAppend.Show(show: !string.IsNullOrEmpty(CurrentRelativeURL) || !string.IsNullOrEmpty(next_url));

                    foreach (var illust in relatives.illusts)
                    {
                        if (relative_illusts.Contains(illust.Id)) continue;
                        relative_illusts.Add(illust.Id);
                        illust.Cache();
                        illust.AddTo(RelativeItems.Items, relatives.next_url);
                        this.DoEvents();
                    }
                    this.DoEvents();
                    //RelativeItems.UpdateTilesImage();
                    if (relatives.illusts.Count() <= 0) "No Result".ShowToast("INFO", tag: "ShowRelative");
                    else UpdateThumb();
                }
            }
            catch (Exception ex)
            {
                ex.ERROR(this.Name ?? "RelativeItems");
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
                    RelativeItems.Clear(setting.BatchClearThumbnails);
                    relative_illusts.Clear();
                }

                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                var lastUrl = next_url;
                var relatives = string.IsNullOrEmpty(next_url) ? await tokens.GetUserWorksAsync(user.Id.Value) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = relatives.next_url ?? string.Empty;

                if (relatives.illusts is Array)
                {
                    RelativeNextPage.Show(!next_url.Equals(lastUrl, StringComparison.CurrentCultureIgnoreCase));

                    if (!append)
                    {
                        RelativeItemsExpander.Tag = lastUrl;
                        CurrentRelativeURL = lastUrl;
                    }
                    NextRelativeURL = next_url;
                    RelativeNextPage.Tag = next_url;
                    RelativeNextPage.ToolTip = CurrentRelativeURL.CalcUrlPageHint(IllustSize.Text);
                    RelativePrevPage.ToolTip = RelativeNextPage.ToolTip;
                    if (!append)
                    {
                        RelativePrevPage.Tag = string.IsNullOrEmpty(CurrentRelativeURL) ? Contents.MakeUserWorkNextUrl().CalcPrevUrl(totals: IllustSize.Text) : CurrentRelativeURL.CalcPrevUrl(totals: IllustSize.Text);
                    }
                    RelativePrevPage.Show(show: IllustSize.Text.CalcTotalPages() > 1);
                    RelativeNextPage.Show(show: IllustSize.Text.CalcTotalPages() > 1);
                    RelativeNextAppend.Show(show: IllustSize.Text.CalcTotalPages() > 1);

                    foreach (var illust in relatives.illusts)
                    {
                        if (relative_illusts.Contains(illust.Id)) continue;
                        relative_illusts.Add(illust.Id);
                        illust.Cache();
                        illust.AddTo(RelativeItems.Items, relatives.next_url);
                        this.DoEvents();
                    }
                    this.DoEvents();
                    //RelativeItems.UpdateTilesImage();
                    if (relatives.illusts.Count() <= 0) "No Result".ShowToast("INFO", tag: "ShowUserWorks");
                    else UpdateThumb();
                }
            }
            catch (Exception ex)
            {
                ex.ERROR(this.Name ?? "UserWorks");
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
                    FavoriteItems.Clear(setting.BatchClearThumbnails);
                    favorite_illusts.Clear();
                }

                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;

                var lastUrl = next_url;
                if (string.IsNullOrEmpty(next_url)) FavoriteNextPage.ToolTip = null;
                var restrict = Keyboard.Modifiers != ModifierKeys.None ? "private" : "public";
                if (!last_restrict.Equals(restrict, StringComparison.CurrentCultureIgnoreCase)) next_url = string.Empty;
                FavoriteItemsExpander.Header = $"Favorite ({CultureInfo.CurrentCulture.TextInfo.ToTitleCase(restrict)})";

                var favorites = string.IsNullOrEmpty(next_url) ? await tokens.GetUserFavoriteWorksAsync(user.Id.Value, restrict) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);
                next_url = favorites.next_url ?? string.Empty;
                last_restrict = restrict;

                if (favorites.illusts is Array)
                {
                    FavoriteNextPage.Show(!next_url.Equals(lastUrl, StringComparison.CurrentCultureIgnoreCase));

                    if (!append)
                    {
                        FavoriteItemsExpander.Tag = lastUrl;
                        CurrentFavoriteURL = lastUrl;
                    }
                    NextFavoriteURL = next_url;
                    FavoriteNextPage.Tag = next_url;
                    FavoriteNextPage.ToolTip = next_url.CalcUrlPageHint(0, FavoriteNextPage.ToolTip is string ? FavoriteNextPage.ToolTip as string : null);
                    FavoriteNextPage.Show(show: !string.IsNullOrEmpty(CurrentFavoriteURL) || !string.IsNullOrEmpty(next_url));
                    FavoriteNextAppend.Show(show: !string.IsNullOrEmpty(CurrentFavoriteURL) || !string.IsNullOrEmpty(next_url));

                    foreach (var illust in favorites.illusts)
                    {
                        if (favorite_illusts.Contains(illust.Id)) continue;
                        favorite_illusts.Add(illust.Id);
                        illust.Cache();
                        illust.AddTo(FavoriteItems.Items, favorites.next_url);
                        this.DoEvents();
                    }
                    this.DoEvents();
                    //FavoriteItems.UpdateTilesImage();
                    if (favorites.illusts.Count() <= 0) "No Result".ShowToast("INFO", tag: "ShowFavorite");
                    else UpdateThumb();
                }
            }
            catch (Exception ex)
            {
                ex.ERROR(this.Name ?? "FavoriteItems");
            }
            finally
            {
                FavoriteItems.Ready();
                if (FavoriteItems.Items.Count > 0) FavoriteRefresh.Show();
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
        private void UpdateSubPageNav()
        {
            try
            {
                if (Contents.IsUser())
                {
                    btnSubPageNext.Hide();
                    btnSubPagePrev.Hide();
                }
                else if (Contents.IsWork())
                {
                    if (Contents.Count > 1)
                    {
                        btnSubPagePrev.Enable(Contents.Index > 0);
                        btnSubPageNext.Enable(Contents.Index < Contents.Count - 1);

                        if (SubIllusts.SelectedIndex < 0)
                            SubIllusts.SelectedIndex = 0;
                    }
                    else
                    {
                        btnSubPageNext.Hide();
                        btnSubPagePrev.Hide();
                    }
                }
                this.DoEvents();
            }
            catch (Exception ex) { ex.ERROR("SubPagesNavi"); }
        }

        public void OpenInNewWindow()
        {
            if (Contents.HasUser() && !(Parent is ContentWindow))
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
            if (Contents.IsWork())
            {
                if (ParentWindow is ContentWindow && Contents.Count > 1) PrevIllustPage();
                else if (ParentWindow is MainWindow) Commands.PrevIllust.Execute(Application.Current.MainWindow);
            }
        }

        public void NextIllust()
        {
            if (InSearching) return;
            if (Contents.IsWork())
            {
                if (ParentWindow is ContentWindow && Contents.Count > 1) NextIllustPage();
                else if (ParentWindow is MainWindow) Commands.NextIllust.Execute(Application.Current.MainWindow);
            }
        }

        public bool IsFirstPage { get { return (Contents.IsWork() && Contents.Index == 0); } }

        public bool IsLastPage { get { return (Contents.IsWork() && Contents.Index == Contents.Count - 1); } }

        public bool IsMultiPages { get { return (Contents.HasPages()); } }
        public void PrevIllustPage()
        {
            try
            {
                if (Contents.IsWork())
                {
                    if (Contents.Count > 1)
                    {
                        if (InSearching) return;
                        setting = Application.Current.LoadSetting();
                        if (Contents.Index > 0)
                            SubPageNav_Clicked(btnSubPagePrev, new RoutedEventArgs());
                        else if (setting.SeamlessViewInMainWindow)
                            PrevIllust();
                    }
                    else PrevIllust();
                }
            }
            catch(Exception ex) { ex.ERROR("PrevIllustPage"); }
        }

        public void NextIllustPage()
        {
            if (Contents.IsWork())
            {
                try
                {
                    if (Contents.Count > 1)
                    {
                        if (InSearching) return;
                        setting = Application.Current.LoadSetting();
                        if (Contents.Index < Contents.Count - 1)
                            SubPageNav_Clicked(btnSubPageNext, new RoutedEventArgs());
                        else if (setting.SeamlessViewInMainWindow)
                            NextIllust();
                    }
                    else NextIllust();
                }
                catch(Exception ex) { ex.ERROR("NextIllustPage"); }
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
                ex.ERROR("SetFilter");
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
                ex.ERROR("SetFilter");
            }
        }

        public dynamic GetTilesCount()
        {
            List<string> tips = new List<string>();
            tips.Add($"Relative: {RelativeItems.ItemsCount}({RelativeItems.SelectedItems.Count}) of {RelativeItems.Items.Count}");
            tips.Add($"Favorite: {FavoriteItems.ItemsCount}({FavoriteItems.SelectedItems.Count}) of {FavoriteItems.Items.Count}");
            return (string.Join(Environment.NewLine, tips));
        }

        public bool InSearching
        {
            get
            {
                if (ParentWindow is MainWindow)
                    return ((ParentWindow as MainWindow).InSearching);
                else if (ParentWindow is ContentWindow)
                    return ((ParentWindow as ContentWindow).InSearching);
                else return (false);
            }
        }

        public void StopPrefetching()
        {
            if (PrefetchingImagesTask is PrefetchingTask) PrefetchingImagesTask.Stop();
            if (cancelDownloading is CancellationTokenSource) cancelDownloading.Cancel();
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                    try
                    {
                        StopPrefetching();

                        if (PrefetchingImagesTask is PrefetchingTask) PrefetchingImagesTask.Dispose();

                        SubIllusts.Clear(batch: false, force: true);
                        this.DoEvents();
                        RelativeItems.Clear(batch: false, force: true);
                        this.DoEvents();
                        FavoriteItems.Clear(batch: false, force: true);
                        this.DoEvents();

                        if (PreviewPopupTimer is System.Timers.Timer)
                        {
                            PreviewPopupTimer.Stop();
                            PreviewPopupTimer.Dispose();
                        }
                        PreviewPopup = null;

                        foreach (var button in PreviewPopupToolButtons)
                        {
                            if (button is Button)
                            {
                                button.MouseEnter -= PreviewPopup_MouseEnter;
                                button.MouseLeave -= PreviewPopup_MouseLeave;
                            }
                        }
                        PreviewPopupToolButtons.Clear();

                        DeleteHtmlRender();
                        IllustAuthorAvatar.Dispose();
                        Preview.Dispose();
                        Contents.Source = null;
                    }
                    catch (Exception ex) { ex.ERROR("DisposeIllustDetail"); }
                    finally { }
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~IllustDetailPage() {
        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }
        #endregion

        public IllustDetailPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ParentWindow = Window.GetWindow(this);

            RelativeItems.Columns = 5;
            FavoriteItems.Columns = 5;

            IllustDetailWait.Hide();
            btnSubPagePrev.Hide();
            btnSubPageNext.Hide();

            CreateHtmlRender();

            #region ToolButton MouseOver action
            BookmarkIllust.MouseOverAction();
            FollowAuthor.MouseOverAction();
            IllustActions.MouseOverAction();

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

            #region Preview Popup
            try
            {
                PreviewPopup = (Popup)TryFindResource("PreviewPopup");
                if (PreviewPopup is Popup)
                {
                    PreviewPopup.PlacementTarget = Preview;
                    PreviewPopupBackground = PreviewPopup.GetChildren<Rectangle>().FirstOrDefault();
                    PreviewPopupToolButtons = PreviewPopup.GetChildren<Button>();
                    foreach (var button in PreviewPopupToolButtons)
                    {
                        button.MouseOverAction();
                        button.MouseEnter += PreviewPopup_MouseEnter;
                        button.MouseLeave += PreviewPopup_MouseLeave;
                    }

                    InitPopupTimer(ref PreviewPopupTimer);
                }
            }
            catch (Exception ex) { ex.ERROR("PreviewPopupLoaded"); }
            #endregion

            #region Bookmark/Follow/IllustActions ContextMenu
            ContextMenuBookmarkActions = (ContextMenu)TryFindResource("ActionBookmarkIllust");
            ContextMenuFollowActions = (ContextMenu)TryFindResource("ActionFollowAuthor");
            ContextMenuIllustActions = (ContextMenu)TryFindResource("ActionIllust");
            if (ContextMenuBookmarkActions is ContextMenu && ContextMenuBookmarkActions.HasItems)
            {
                foreach (var item in ContextMenuBookmarkActions.Items)
                {
                    //ContextMenuBookmarkActions.Items.Remove(item);
                    //BookmarkIllustAlt.Items.Add(item);
                    if (item is MenuItem)
                    {
                        var menu = item as MenuItem;
                        ContextMenuActionItems[menu.Uid] = menu;
                    }
                }
            }
            if (ContextMenuFollowActions is ContextMenu && ContextMenuFollowActions.HasItems)
            {
                foreach (var item in ContextMenuFollowActions.Items)
                {
                    if (item is MenuItem)
                    {
                        var menu = item as MenuItem;
                        ContextMenuActionItems[menu.Uid] = menu;
                    }
                }
            }
            if (ContextMenuIllustActions is ContextMenu && ContextMenuIllustActions.HasItems)
            {
                foreach (var item in ContextMenuIllustActions.Items)
                {
                    if (item is MenuItem)
                    {
                        var menu = item as MenuItem;
                        ContextMenuActionItems[menu.Uid] = menu;
                    }
                }
            }
            #endregion

            #region Prefetching
            InitPrefetchingTask();
            #endregion

            if (Contents.HasUser()) UpdateDetail(Contents);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            //Dispose();
        }

        private void Page_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
            var change_illust = Keyboard.Modifiers == ModifierKeys.Shift;
            try
            {
                if (ParentWindow is MainWindow) (ParentWindow as MainWindow).InSearching = false;
                else if (ParentWindow is ContentWindow) (ParentWindow as ContentWindow).InSearching = false;

                if (PreviewPopup is Popup && PreviewPopup.IsOpen && e.LeftButton != MouseButtonState.Pressed)
                {
                    PreviewPopup.IsOpen = false;
                    PreviewPopupTimer.Stop();
                }

                setting = Application.Current.LoadSetting();
                if (change_illust && ParentWindow is ContentWindow)
                {
                    if (e.XButton1 == MouseButtonState.Pressed)
                    {
                        e.Handled = true;
                        if (setting.ReverseMouseXButton) NextIllust();
                        else PrevIllust();
                    }
                    else if (e.XButton2 == MouseButtonState.Pressed)
                    {
                        e.Handled = true;
                        if (setting.ReverseMouseXButton) PrevIllust();
                        else NextIllust();
                    }
                }
                else
                {
                    if (e.XButton1 == MouseButtonState.Pressed)
                    {
                        e.Handled = true;
                        if (setting.ReverseMouseXButton) NextIllustPage();
                        else PrevIllustPage();
                    }
                    else if (e.XButton2 == MouseButtonState.Pressed)
                    {
                        e.Handled = true;
                        if (setting.ReverseMouseXButton) PrevIllustPage();
                        else NextIllustPage();
                    }
                }
            }
            catch(Exception ex) { ex.ERROR("IllustDetailPreviewMouseDown"); }
        }

        #region Preview Popup
        private async void PreviewPopupTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if (PreviewPopup is Popup)
                {
                    await new Action(() =>
                    {
                        if (!PreviewPopup.IsMouseDirectlyOver || !PreviewPopup.IsHitTestVisible)
                        {
                            PreviewPopup.IsOpen = false;
                        }
                    }).InvokeAsync(true);
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        private void PreviewPopup_Opened(object sender, EventArgs e)
        {
            if (PreviewPopup is Popup && PreviewPopupTimer is System.Timers.Timer)
            {
                PreviewPopupTimer.Start();
            }
        }

        private void PreviewPopup_Closed(object sender, EventArgs e)
        {
            if (PreviewPopup is Popup && PreviewPopupTimer is System.Timers.Timer)
            {
                PreviewPopupTimer.Stop();
            }
        }

        private void PreviewPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            if (PreviewPopup is Popup && PreviewPopup.IsOpen && PreviewPopupTimer is System.Timers.Timer)
            {
                PreviewPopupTimer.Stop();
            }
        }

        private void PreviewPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            if (PreviewPopup is Popup && PreviewPopup.IsOpen && PreviewPopupTimer is System.Timers.Timer)
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
        #endregion

        #region Illust Actions
        private async void ActionIllustInfo_Click(object sender, RoutedEventArgs e)
        {
            UpdateLikeState();
            try
            {
                var uid = sender.GetUid();
                if (uid.Equals("ActionCopyIllustTitle", StringComparison.CurrentCultureIgnoreCase) || sender == IllustTitle)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        Commands.CopyText.Execute($"{IllustTitle.Text}");
                    else
                        Commands.CopyText.Execute($"title:{IllustTitle.Text}");
                }
                else if (uid.Equals("ActionCopyIllustAuthor", StringComparison.CurrentCultureIgnoreCase) || sender == IllustAuthor)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        Commands.CopyText.Execute($"{IllustAuthor.Text}");
                    else
                        Commands.CopyArtistIDs.Execute(Contents);
                }
                else if (uid.Equals("ActionCopyAuthorID", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (Contents.HasUser()) Commands.CopyArtistIDs.Execute(Contents);
                }
                else if (uid.Equals("ActionCopyIllustID", StringComparison.CurrentCultureIgnoreCase) || sender == PreviewCopyIllustID)
                {
                    if (Contents.IsWork()) Commands.CopyArtworkIDs.Execute(Contents);
                }
                else if (uid.Equals("PreviewCopyImage", StringComparison.CurrentCultureIgnoreCase) || sender == PreviewCopyImage)
                {
                    CopyPreview();
                }
                else if (uid.Equals("ActionCopyIllustDate", StringComparison.CurrentCultureIgnoreCase) || sender == IllustDate || sender == IllustDateInfo)
                {
                    if (ContextMenuActionItems.ContainsKey(uid)) Commands.CopyText.Execute(ContextMenuActionItems[uid].Header);
                }
                else if (uid.Equals("ActionIllustWebPage", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (Contents.IsWork())
                    {
                        var href = Contents.ID.ArtworkLink();
                        href.OpenUrlWithShell();
                    }
                }
                else if (uid.Equals("ActionIllustNewWindow", StringComparison.CurrentCultureIgnoreCase))
                {
                    OpenInNewWindow();
                }
                else if (uid.Equals("ActionIllustWebLink", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (Contents.IsWork())
                    {
                        var href = Contents.ID.ArtworkLink();
                        Commands.CopyText.Execute(href);
                    }
                }
                else if (uid.Equals("ActionAuthorWebLink", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (Contents.HasUser())
                    {
                        var href = Contents.UserID.ArtistLink();
                        Commands.CopyText.Execute(href);
                    }
                }
                else if (uid.Equals("ActionSendIllustToInstance", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (Contents.IsWork())
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
                else if (uid.Equals("ActionSendAuthorToInstance", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (Contents.HasUser())
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
                e.Handled = true;
            }
            catch(Exception ex) { ex.ERROR("IllustActions"); }
        }

        private void ActionIllustAuthourInfo_Click(object sender, RoutedEventArgs e)
        {
            var uid = sender.GetUid();
            if (uid.Equals("ActionIllustAuthorInfo", StringComparison.CurrentCultureIgnoreCase) || sender == btnAuthorAvatar)
            {
                if (Contents.HasUser())
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
                if (Contents.Count <= 1 || SubIllusts.SelectedItems.Count == 0)
                    Commands.OpenDownloaded.Execute(Contents);
                else
                    Commands.OpenDownloaded.Execute(SubIllusts);
            }
            else if (sender == PreviewOpen)
            {
                if (Contents.Count <= 1 || SubIllusts.SelectedItems.Count == 0)
                    Commands.OpenWorkPreview.Execute(Contents);
                else
                    Commands.OpenWorkPreview.Execute(SubIllusts);
            }
            else if (sender == PreviewCacheOpen && Preview.Source != null)
            {
                Commands.OpenCachedImage.Execute(string.IsNullOrEmpty(PreviewImagePath) ? Contents.Illust.GetPreviewUrl(large: setting.ShowLargePreview).GetImageCachePath() : PreviewImagePath);
            }
            else if(sender == PreviewOpenDownloadedProperties)
            {
                if (Contents.Count <= 1 || SubIllusts.SelectedItems.Count == 0)
                    Commands.OpenFileProperties.Execute(Contents);
                else
                    Commands.OpenFileProperties.Execute(SubIllusts);
            }
        }

        private void ActionRefreshPreview_Click(object sender, RoutedEventArgs e)
        {
            ActionRefreshPreview();
        }

        private async void ActionRefreshPreview(bool overwrite = false)
        {
            overwrite = Keyboard.Modifiers == ModifierKeys.Control ? true : overwrite;
            if (Contents.IsWork())
            {
                setting = Application.Current.LoadSetting();

                if (!(cancelDownloading is CancellationTokenSource) || cancelDownloading.IsCancellationRequested)
                    cancelDownloading = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                Preview.Show();
                await new Action(async () =>
                {
                    try
                    {
                        var c_item = Contents.HasPages() && SubIllusts.SelectedItem.IsPages() ? SubIllusts.SelectedItem : Contents;
                        Contents.Index = c_item.Index;
                        lastSelectionItem = c_item;
                        lastSelectionChanged = DateTime.Now;

                        if (c_item.IsSameIllust(Contents)) PreviewWait.Show();

                        PreviewImageUrl = c_item.Illust.GetPreviewUrl(c_item.Index, large: setting.ShowLargePreview);
                        using (var img = await PreviewImageUrl.LoadImageFromUrl(overwrite, progressAction: PreviewWait.ReportPercentage, cancelToken: cancelDownloading))
                        {
                            if (!setting.ShowLargePreview && setting.SmartPreview &&
                                (img.Source == null ||
                                 img.Source.Width < setting.PreviewUsingLargeMinWidth ||
                                 img.Source.Height < setting.PreviewUsingLargeMinHeight))
                            {
                                PreviewImageUrl = c_item.Illust.GetPreviewUrl(c_item.Index, true);
                                using (var large = await PreviewImageUrl.LoadImageFromUrl(overwrite, progressAction: PreviewWait.ReportPercentage, cancelToken: cancelDownloading))
                                {
                                    if (large.Source != null)
                                    {
                                        img.Source = large.Source;
                                        img.Size = large.Size;
                                        img.SourcePath = large.SourcePath;
                                        img.ColorDepth = large.ColorDepth;
                                    }
                                }
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
                        }
                    }
                    catch (Exception ex) { ex.ERROR("ActionRefreshPreview"); PreviewWait.Fail(); }
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
            if (Contents.HasUser())
            {
                await new Action(async () =>
                {
                    try
                    {
                        if (!(cancelDownloading is CancellationTokenSource) || cancelDownloading.IsCancellationRequested)
                            cancelDownloading = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                        AuthorAvatarWait.Show();
                        btnAuthorAvatar.Show(AuthorAvatarWait.IsFail);

                        var c_item = Contents;
                        AvatarImageUrl = Contents.User.GetAvatarUrl();
                        using (var img = await AvatarImageUrl.LoadImageFromUrl(overwrite, size: Application.Current.GetDefaultAvatarSize(), cancelToken: cancelDownloading))
                        {
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
                        }
                    }
                    catch (Exception ex) { ex.ERROR("ActionRefreshAvatar"); AuthorAvatarWait.Fail(); btnAuthorAvatar.Show(); }
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
            catch (Exception ex) { ex.ERROR(); }
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
            catch (Exception ex) { ex.ERROR(); }
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
            catch (Exception ex) { ex.ERROR(); }
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
                        else if (host == IllustTitle)
                        {
                            if (Contents.IsWork())
                            {
                                Contents.Illust.Title.TagWildcardCacheClear();
                                UpdateIllustTitle();
                            }
                        }
                        else if (mi.Uid.Equals("ActionRefresh", StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (Contents.HasUser()) UpdateDetail(Contents);
                        }
                        else if (mi == ActionClearTagsCache)
                        {
                            if (Contents.IsWork())
                            {
                                Contents.Illust.Title.TagWildcardCacheClear();
                                Contents.Illust.Tags.TagWildcardCacheClear();
                            }
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
                    if (Contents.HasPages() && SubIllusts.Items.Count == 0)
                    {
                        ShowIllustPagesAsync(Contents, force: true);
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.None)
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
            catch (Exception ex) { ex.ERROR("ActionRefresh"); }
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
            catch (Exception ex) { ex.ERROR(); }
#endif
        }

        private void ActionOpenTagsFile_Click(object sender, RoutedEventArgs e)
        {
            setting = Application.Current.LoadSetting();
            var tag_type = string.Empty;
            if (sender == ActionOpenPixivTags) tag_type = setting.TagsFile;
            else if (sender == ActionOpenCustomTags) tag_type = setting.CustomTagsFile;
            else if (sender == ActionOpenCustomWildTags) tag_type = setting.CustomWildcardTagsFile;
            else if (sender == ActionOpenTagsFolder) tag_type = "folder";
            Commands.OpenTags.Execute(tag_type);
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

                e.Handled = true;
                if (ParentWindow is Window && !ParentWindow.IsActive)
                    ParentWindow.Activate();
                else if (e.ClickCount >= 2)
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
                }
                else if (IsElement(btnSubPagePrev, e) && btnSubPagePrev.IsVisible && btnSubPagePrev.IsEnabled)
                    PrevIllustPage();
                else if (IsElement(btnSubPageNext, e) && btnSubPageNext.IsVisible && btnSubPageNext.IsEnabled)
                    NextIllustPage();
                else if (setting.EnabledMiniToolbar && PreviewPopup is Popup)
                    PopupOpen(PreviewPopup);
                else
                    e.Handled = false;
            }
            catch (Exception ex) { ex.ERROR("PreviewClick"); }
        }

        private void PreviewRect_MouseLeave(object sender, MouseEventArgs e)
        {
            btnSubPagePrev.MinWidth = 32;
            btnSubPageNext.MinWidth = 32;
            e.Handled = true;
        }

        private void PreviewRect_MouseMove(object sender, MouseEventArgs e)
        {
            if (Contents.HasPages())
            {
                if (IsElement(btnSubPagePrev, e) && btnSubPagePrev.IsVisible && btnSubPagePrev.IsEnabled)
                {
                    btnSubPagePrev.MinWidth = 48;
                    btnSubPageNext.MinWidth = 32;
                    e.Handled = true;
                }
                else if (IsElement(btnSubPageNext, e) && btnSubPageNext.IsVisible && btnSubPageNext.IsEnabled)
                {
                    btnSubPagePrev.MinWidth = 32;
                    btnSubPageNext.MinWidth = 48;
                    e.Handled = true;
                }
                else
                {
                    btnSubPagePrev.MinWidth = 32;
                    btnSubPageNext.MinWidth = 32;
                    e.Handled = true;
                }
            }
        }

        private void IllustDownloaded_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e.ChangedButton == MouseButton.Left)
            {
                e.Handled = true;
                if (IllustDownloaded.Tag is string)
                    Commands.ShellOpenFile.Execute(IllustDownloaded.Tag);
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                e.Handled = true;
                if (IllustDownloaded.Tag is string)
                    Commands.OpenFileProperties.Execute(IllustDownloaded.Tag);
            }
            else if (e.ChangedButton == MouseButton.Middle)
            {
                e.Handled = true;
                if (IllustDownloaded.Tag is string)
                    Commands.CopyText.Execute(IllustDownloaded.Tag);
            }
        }

        private void IllustUgoiraDownloaded_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                e.Handled = true;
                if (IllustUgoiraDownloaded.Tag is string)
                    Commands.ShellOpenUgoira.Execute(IllustUgoiraDownloaded.Tag);
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                e.Handled = true;
                if (IllustUgoiraDownloaded.Tag is string)
                    Commands.OpenFileProperties.Execute(IllustUgoiraDownloaded.Tag);
            }
            else if (e.ChangedButton == MouseButton.Middle)
            {
                e.Handled = true;
                if (IllustUgoiraDownloaded.Tag is string)
                    Commands.CopyText.Execute(IllustUgoiraDownloaded.Tag);
            }
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
                //e.Handled = true;
                UpdateLikeState();
            }
        }

        private void ActionIllust_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            bool is_user = false;

            if (sender == BookmarkIllust)
            {
                PopupOpen(sender, BookmarkIllust.ContextMenu);
                is_user = false;
            }
            else if (sender == FollowAuthor)
            {
                PopupOpen(sender, FollowAuthor.ContextMenu);
                is_user = true;
            }
            else if (sender == IllustActions)
            {
                if (ContextMenuActionItems.ContainsKey("ActionIllustNewWindow"))
                {
                    if (Window.GetWindow(this) is ContentWindow)
                        ContextMenuActionItems["ActionIllustNewWindow"].Visibility = Visibility.Collapsed;
                    else
                        ContextMenuActionItems["ActionIllustNewWindow"].Visibility = Visibility.Visible;
                }
                PopupOpen(sender, IllustActions.ContextMenu);
            }
            //UpdateLikeState(-1, is_user);
            int id = -1;
            int.TryParse(Contents.ID, out id);
            if (Contents.HasUser()) is_user.UpdateLikeStateAsync(id);
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
                catch (Exception ex) { ex.ERROR("BOOKMARK"); }
            }
            else if (uid.Equals("ActionBookmarkIllustPublic", StringComparison.CurrentCultureIgnoreCase) ||
                     uid.Equals("ActionBookmarkIllustPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                     uid.Equals("ActionBookmarkIllustRemove", StringComparison.CurrentCultureIgnoreCase))
            {
                if (Contents.IsWork())
                {
                    var item = Contents;
                    var result = false;
                    try
                    {
                        if (uid.Equals("ActionBookmarkIllustPublic", StringComparison.CurrentCultureIgnoreCase))
                        {
                            result = await item.LikeIllust();
                        }
                        else if (uid.Equals("ActionBookmarkIllustPrivate", StringComparison.CurrentCultureIgnoreCase))
                        {
                            result = await item.LikeIllust(false);
                        }
                        else if (uid.Equals("ActionBookmarkIllustRemove", StringComparison.CurrentCultureIgnoreCase))
                        {
                            result = await item.UnLikeIllust();
                        }

                        if (item.IsSameIllust(Contents))
                        {
                            BookmarkIllust.Tag = result ? SymbolIcon_Favorited : SymbolIcon_UnFavorited;
                            if (ContextMenuActionItems.ContainsKey("ActionBookmarkIllustRemove"))
                                ContextMenuActionItems["ActionBookmarkIllustRemove"].IsEnabled = result;
                            item.IsFavorited = result;
                        }
                    }
                    catch (Exception ex) { ex.ERROR("BOOKMARK"); }
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
                catch (Exception ex) { ex.ERROR("FOLLOW"); }
            }
            else if(uid.Equals("ActionFollowAuthorPublic", StringComparison.CurrentCultureIgnoreCase) ||
                    uid.Equals("ActionFollowAuthorPrivate", StringComparison.CurrentCultureIgnoreCase) ||
                    uid.Equals("ActionFollowAuthorRemove", StringComparison.CurrentCultureIgnoreCase))
            {
                if (Contents.HasUser())
                {
                    var item = Contents;
                    var result = false;
                    try
                    {
                        if (uid.Equals("ActionFollowAuthorPublic", StringComparison.CurrentCultureIgnoreCase))
                        {
                            result = await item.LikeUser();
                        }
                        else if (uid.Equals("ActionFollowAuthorPrivate", StringComparison.CurrentCultureIgnoreCase))
                        {
                            result = await item.LikeUser(false);
                        }
                        else if (uid.Equals("ActionFollowAuthorRemove", StringComparison.CurrentCultureIgnoreCase))
                        {
                            result = await item.UnLikeUser();
                        }

                        if (item.IsSameIllust(Contents))
                        {
                            FollowAuthor.Tag = result ? SymbolIcon_Followed : SymbolIcon_UnFollowed;
                            if (ContextMenuActionItems.ContainsKey("ActionFollowAuthorRemove"))
                                ContextMenuActionItems["ActionFollowAuthorRemove"].IsEnabled = result;
                            if (item.IsUser()) item.IsFavorited = result;
                        }
                    }
                    catch (Exception ex) { ex.ERROR("FOLLOW"); }
                }
            }
        }
        #endregion

        #region Illust Multi-Pages related routines
        private void SubIllustsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Contents.IsWork())
            {
                var count_list = Math.Min(Contents.Count - page_number * PAGE_ITEMS, PAGE_ITEMS);
                var count = SubIllusts.Items.Count();
                var same = count_list > 0 && count > 0 ? Contents.IsSameIllust(SubIllusts.Items.First()) : false;
                if (!same || count <= 0 || count != count_list)
                    ShowIllustPagesAsync(Contents, page_index, page_number, force: true);
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
            if (Contents.HasPages())
            {                
                //if (SubIllusts.IsBusy) return;
                if (e.AddedItems.Count == 1 && e.RemovedItems.Count == 1 && (e.AddedItems[0] as PixivItem).Index == (e.RemovedItems[0] as PixivItem).Index) return;

                if (SubIllusts.SelectedItem.IsPages() && SubIllusts.SelectedItems.Count == 1)
                {
                    if (lastSelectionChanged.DeltaMilliseconds(DateTime.Now) < 50)
                    {
                        SubIllusts.SelectedItem = lastSelectionItem;
                        return;
                    }
                    SubIllusts.UpdateTilesState(touch: false);

                    var part_down = SubIllusts.Items.Where(i => i.IsDownloaded).Count() > 0;
                    if (Contents.IsDownloaded != part_down) Contents.IsDownloaded = part_down; // Contents.Illust.IsPartDownloadedAsync();

                    int idx = -1;
                    int.TryParse(SubIllusts.SelectedItem.BadgeValue, out idx);
                    if (idx - 1 != Contents.Index)
                    {
                        Contents.Index = idx - 1;
                        PreviewBadge.Badge = $"{idx} / {Contents.Count}";
                        UpdateLikeState();
                        UpdateDownloadedMark(SubIllusts.SelectedItem);
                    }
                    e.Handled = true;

                    UpdateSubPageNav();

                    ActionRefreshPreview_Click(sender, e);
                    Keyboard.Focus(SubIllusts.SelectedItem);
                }
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
            catch (Exception ex) { ex.ERROR(); }
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
                if (Contents.IsWork())
                {
                    var illust = Contents.Illust;
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
            else if (SubIllusts.SelectedItem.IsWork())
            {
                var item = SubIllusts.SelectedItem;
                Commands.SaveIllust.Execute(item);
            }
            else if (Contents.IsWork())
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
                if (SubIllusts.SelectedItem.IsPages()) SubIllusts.SelectedItem.Focus();
            }
        }
        #endregion

        #region Relative Panel related routines
        private void RelativeItemsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Contents.HasUser())
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
            //UpdateLikeState();
            if (RelativeItems.SelectedItem.IsWork())
            {
                int id = -1;
                int.TryParse(RelativeItems.SelectedItem.ID, out id);
                false.UpdateLikeStateAsync(id);
                RelativeItems.SelectedItem.Focus();
            }
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
            catch (Exception ex) { ex.ERROR(); }
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
            if (Contents.HasUser())
            {
                var prev_url = RelativePrevPage.Tag is string ? RelativePrevPage.Tag as string : string.Empty;

                if (Contents.IsWork())
                    ShowRelativeInlineAsync(Contents, prev_url);
                else if (Contents.IsUser())
                    ShowUserWorksInlineAsync(Contents.User, prev_url);
            }
        }

        private void RelativeNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (Contents.HasUser())
            {
                var append = sender == RelativeNextAppend ? true : false;
                var next_url = RelativeNextPage.Tag is string ? RelativeNextPage.Tag as string : string.Empty;

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
            if (Contents.HasUser())
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
            //UpdateLikeState();
            if (FavoriteItems.SelectedItem.IsWork())
            {
                int id = -1;
                int.TryParse(FavoriteItems.SelectedItem.ID, out id);
                false.UpdateLikeStateAsync(id);
                FavoriteItems.SelectedItem.Focus();
            }
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
            catch (Exception ex) { ex.ERROR(); }
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
            if (Contents.HasUser())
            {
                var prev_url = FavoritePrevPage.Tag is string ? FavoritePrevPage.Tag as string : string.Empty;
                ShowFavoriteInlineAsync(Contents.User, prev_url);
            }
        }

        private void FavoriteNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (Contents.HasUser())
            {
                var append = sender == FavoriteNextAppend ? true : false;
                var next_url = FavoriteNextPage.Tag is string ? FavoriteNextPage.Tag as string : string.Empty;
                ShowFavoriteInlineAsync(Contents.User, next_url, append);
            }
        }
        #endregion

        #region Illust Comments related routines
        private async void CommentsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Contents.IsWork() && IllustCommentsHtml is WebBrowserEx)
            {
                //var tokens = await CommonHelper.ShowLogin();
                //if (tokens == null) return;                
                //IllustDetailWait.Show();
                try
                {
                    //IllustCommentsHtml.Navigate("about:blank");
                    //var result = await tokens.GetIllustComments(Contents.ID, "0", true);
                    //foreach (var comment in result.comments)
                    //{
                    //    //comment.
                    //}
                    await Task.Delay(1);
                    this.DoEvents();
                }
                catch (Exception ex) { ex.ERROR("IllustComments"); }
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
                        catch (Exception ex) { ex.ERROR(); continue; }
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
                        catch (Exception ex) { ex.ERROR(); continue; }
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
                        catch (Exception ex) { ex.ERROR(); continue; }
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
                        if (Contents.IsWork())
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
            catch (Exception ex) { ex.ERROR(); }
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
            catch (Exception ex) { ex.ERROR(); }
        }

        private void ActionSendToOtherInstance_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender == PreviewSendIllustToInstance)
                {
                    if (Contents.HasUser())
                    {
                        if (Keyboard.Modifiers == ModifierKeys.None)
                            Commands.SendToOtherInstance.Execute(Contents);
                        else
                            Commands.ShellSendToOtherInstance.Execute(Contents);
                    }
                }
                else if (sender == PreviewSendAuthorToInstance)
                {
                    if (Contents.HasUser())
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
                            if (Contents.HasUser())
                            {
                                if (Keyboard.Modifiers == ModifierKeys.None)
                                    Commands.SendToOtherInstance.Execute(Contents);
                                else
                                    Commands.ShellSendToOtherInstance.Execute(Contents);
                            }
                        }
                        else if (uid.Equals("ActionSendAuthorToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (Contents.HasUser())
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
            catch (Exception ex) { ex.ERROR(); }
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
            catch (Exception ex) { ex.ERROR(); }
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
            catch (Exception ex) { ex.ERROR(); }
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
            catch (Exception ex) { ex.ERROR(); }
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
            catch (Exception ex) { ex.ERROR(); }
        }

        private void ActionUgoiraGet_Click(object sender, RoutedEventArgs e)
        {
            if(Contents.IsUgoira() && sender is MenuItem)
            {
                var mi = sender as MenuItem;
                if (mi.Uid.Equals("ActionSavePreviewUgoiraFile"))
                {
                    Commands.SavePreviewUgoira.Execute(Contents);
                }
                else if (mi.Uid.Equals("ActionSaveOriginalUgoiraFile"))
                {
                    Commands.SaveOriginalUgoira.Execute(Contents);
                }
                else if (mi.Uid.Equals("ActionOpenUgoiraFile"))
                {
                    Commands.ShellOpenUgoira.Execute(Contents);
                }
            }
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
                    else if (m.Uid.Equals("ActionOpenFileProperties", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            Commands.OpenFileProperties.Execute(SubIllusts);
                        }
                        else if (host == RelativeItemsExpander || host == RelativeItems)
                        {
                            Commands.OpenFileProperties.Execute(RelativeItems);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            Commands.OpenFileProperties.Execute(FavoriteItems);
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(); }
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
                            if (Contents.IsWork())
                            {
                                ShowIllustPagesAsync(Contents, page_index, page_number);
                            }
                        }
                        else if (host == RelativeItemsExpander || host == RelativeItems)
                        {
                            if (Contents.HasUser())
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
                            if (Contents.HasUser())
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
            catch (Exception ex) { ex.ERROR(); }
        }
        #endregion
    }

}
