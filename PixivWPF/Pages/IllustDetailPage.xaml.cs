using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

using MahApps.Metro.Controls;
using PixivWPF.Common;

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
        private DispatcherTimer SubIllustUpdateTimer = null;

        private const string SymbolIcon_Image = "\uEB9F"; //
        private const string SymbolIcon_Star = "\uE113"; //, ⭐🌟
        private const string SymbolIcon_Eye = "\u1F441"; //👁
        private const string SymbolIcon_Heart = "\u1F497"; //💗
        private const string SymbolIcon_HeartBreak = "\u1F494"; //💔
        private const string SymbolIcon_HeartFill = "\u1F9E1"; //🧡
        private const string SymbolIcon_HeartOutline = "\u1F90D"; //🤍

        private const string SymbolIcon_Followed = "\uE113";
        private const string SymbolIcon_UnFollowed = "\uE734";
        private const string SymbolIcon_Favorited = "\uEB52";
        private const string SymbolIcon_UnFavorited = "\uEB51";

        private const int PAGE_ITEMS = CommonHelper.ImagesPerPage;
        private string waiting = "Waiting ...";
        private int page_count = 0;
        private int page_number = 0;
        private int page_index = 0;

        private List<string> _urls_ = new List<string>();
        private string PreviewImageUrl { get; set; } = string.Empty;
        private string PreviewImagePath { get; set; } = string.Empty;
        private string AvatarImageUrl { get; set; } = string.Empty;

        private string CurrentRelatedURL { get; set; } = string.Empty;
        private string RelatedNextURL { get; set; } = string.Empty;

        private string CurrentFavoriteURL { get; set; } = string.Empty;
        private string NextFavoriteURL { get; set; } = string.Empty;

        #region Popup Helper
        private Popup PreviewPopup = null;
        private Rectangle PreviewPopupBackground = null;
        private IList<Button> PreviewPopupToolButtons = new List<Button>();
        private DispatcherTimer PreviewPopupTimer = null;

        private ContextMenu ContextMenuBookmarkActions = null;
        private ContextMenu ContextMenuFollowActions = null;
        private ContextMenu ContextMenuIllustActions = null;
        private Dictionary<string, MenuItem> ContextMenuActionItems = new Dictionary<string, MenuItem>();

        private void InitPopupTimer(ref DispatcherTimer timer)
        {
            if (timer == null)
            {
                timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(5), IsEnabled = false };
                timer.Tick += PreviewPopupTimer_Tick;
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
                if (hitResultsList.Count >= 1)
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
                                    $"{Contents.ID}, {tag} : {trans_match.Replace("'", "\"")}".DEBUG("TagTranslate");
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
                        desc.AppendLine($"<div class=\"section\">");
                        desc.AppendLine($"{nuser.Account} / <a href=\"{nuser.Id.ToString().ArtistLink()}\">{nuser.Id}</a> / {nuser.Name} <br/>");
                        desc.AppendLine($"</div>");
                        desc.AppendLine($"<b>Stat:</b><br/> ");
                        desc.AppendLine($"<div class=\"section\">");
                        desc.AppendLine($"{nprof.total_illust_bookmarks_public} Bookmarked / {nprof.total_follower} Following / {nprof.total_follow_users} Follower /<br/>");
                        desc.AppendLine($"{nprof.total_illusts} Illust / {nprof.total_manga} Manga / {nprof.total_novels} Novels /<br/> {nprof.total_mypixiv_users} MyPixiv User <br/>");
                        desc.AppendLine($"</div>");

                        desc.AppendLine($"<hr/>");
                        desc.AppendLine($"<b>Profile:</b><br/>");
                        desc.AppendLine($"<div class=\"section\">");
                        desc.AppendLine($"{nprof.gender} / {nprof.birth} / {nprof.region} / {nprof.job} <br/>");
                        desc.AppendLine($"</div>");
                        desc.AppendLine($"<b>Contacts:</b><br/>");
                        desc.AppendLine($"<div class=\"section\">");
                        desc.AppendLine($"<span class=\"twitter\" title=\"Twitter\"></span><a href=\"{nprof.twitter_url}\">@{nprof.twitter_account}</a><br/>");
                        desc.AppendLine($"<span class=\"web\" title=\"Website\"></span><a href=\"{nprof.webpage}\">{nprof.webpage}</a><br/>");
                        desc.AppendLine($"<span class=\"mail\" title=\"Email\"></span><a href=\"mailto:{nuser.Email}\">{nuser.Email}</a><br/>");
                        desc.AppendLine($"</div>");

                        desc.AppendLine($"<hr/>");
                        desc.AppendLine($"<b>Workspace Device:</b><br/> ");
                        desc.AppendLine($"<div class=\"section\">");
                        desc.AppendLine($"{nworks.pc} / {nworks.monitor} / {nworks.tablet} / {nworks.mouse} / {nworks.printer} / {nworks.scanner} / {nworks.tool} <br/>");
                        desc.AppendLine($"</div>");
                        desc.AppendLine($"<b>Workspace Environment:</b><br/>");
                        desc.AppendLine($"<div class=\"section\">");
                        desc.AppendLine($"{nworks.desk} / {nworks.chair} / {nworks.desktop} / {nworks.music} / {nworks.comment} <br/>");
                        desc.AppendLine($"</div>");

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
                        if (!string.IsNullOrEmpty(contents) && browser is System.Windows.Forms.WebBrowser)
                        {
                            if (browser.IsBusy) browser.Stop();
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
            // The WebBrowser control is checking the Uri 
            else if (e.Url.ToString() != "Place your url string here") //ex: "http://stackoverflow.com"
            {
                // Uri is not the same so it cancels the process
                e.Cancel = true;
                return;
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
                    if (document is System.Windows.Forms.HtmlDocument)
                    {
                        try
                        {
                            //document.Click -= new System.Windows.Forms.HtmlElementEventHandler(WebBrowser_Document_Click);
                            //document.Click += new System.Windows.Forms.HtmlElementEventHandler(WebBrowser_Document_Click);
                            document.MouseDown -= new System.Windows.Forms.HtmlElementEventHandler(WebBrowser_Document_MouseDown);
                            document.MouseDown += new System.Windows.Forms.HtmlElementEventHandler(WebBrowser_Document_MouseDown);
                        }
                        catch { }

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

        private void WebBrowser_Document_Click(object sender, System.Windows.Forms.HtmlElementEventArgs e)
        {
            //some code
            if (sender is System.Windows.Forms.HtmlDocument)
            {
                var doc = sender as System.Windows.Forms.HtmlDocument;
                System.Windows.Forms.WebBrowser browser = null;
                if (doc == IllustTagsHtml.Document) browser = IllustTagsHtml;
                else if (doc == IllustDescHtml.Document) browser = IllustDescHtml;
                if (browser is System.Windows.Forms.WebBrowser)
                {
                    if (e.MouseButtonsPressed == System.Windows.Forms.MouseButtons.Middle)
                    {
                        var text = browser.GetText();
                        if (sender == IllustTagsHtml) text = text.Replace("#", " ").Trim();
                        if (!string.IsNullOrEmpty(text)) Commands.CopyText.Execute(text);
                    }
                }
            }
        }

        private void WebBrowser_Document_MouseDown(object sender, System.Windows.Forms.HtmlElementEventArgs e)
        {
            //some code
            if (sender is System.Windows.Forms.HtmlDocument)
            {
                var doc = sender as System.Windows.Forms.HtmlDocument;
                System.Windows.Forms.WebBrowser browser = null;
                if (doc == IllustTagsHtml.Document) browser = IllustTagsHtml;
                else if (doc == IllustDescHtml.Document) browser = IllustDescHtml;
                if (browser is System.Windows.Forms.WebBrowser)
                {
                    if (e.MouseButtonsPressed == System.Windows.Forms.MouseButtons.Middle)
                    {
                        var text = browser.GetText();
                        if (browser == IllustTagsHtml) text = text.Replace("#", " ").Trim();
                        if (!string.IsNullOrEmpty(text)) Commands.CopyText.Execute(text);
                    }
                    e.BubbleEvent = false;
                }
            }
        }
        #endregion

        #region Illust/User info related methods
        public void UpdateIllustTitle()
        {
            try
            {
                if (Contents.IsWork())
                {
                    string trans_match = string.Empty;
                    var trans = IllustTitle.Text.TranslatedText(out trans_match);
#if DEBUG
                    IllustTitle.ToolTip = trans + (string.IsNullOrEmpty(trans_match) ? string.Empty : $"{Environment.NewLine}Matched: {trans_match}");
#else
                    IllustTitle.ToolTip = trans;
#endif
                    if (!string.IsNullOrEmpty(trans_match)) $"{Contents.ID}, Trans => {trans}, Match => {trans_match}".DEBUG("TitleTranslate");
                }
            }
            catch (Exception ex) { ex.ERROR("UpdateTitle"); }
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
            catch (Exception ex) { ex.ERROR("UpdateTags"); }
        }

        public void UpdateIllustDesc()
        {
            try
            {
                RefreshHtmlRender(IllustDescHtml);
            }
            catch (Exception ex) { ex.ERROR("UpdateDesc"); }
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
                ugoira.MakeUgoiraConcatFile(fp);
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
                        tips.Add($"Preview  : {file_p} [{(await ugoira_info.GetUgoiraUrl(preview: true).QueryImageFileSize() ?? -1).SmartFileSize()}]");
                        tips.Add($"Original : {file_o} [{(await ugoira_info.GetUgoiraUrl(preview: false).QueryImageFileSize() ?? -1).SmartFileSize()}]");
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
                        IllustUgoiraDownloaded.ToolTip = string.IsNullOrEmpty(tooltip) ? null : tooltip;
                    }
                    else
                    {
                        var tp = string.Join(Environment.NewLine, new string[] { tooltip, fp }).Trim();
                        IllustUgoiraDownloaded.Tag = fp;
                        IllustUgoiraDownloaded.ToolTip = string.IsNullOrEmpty(tp) ? null : tp;
                    }
                    IllustUgoiraDownloaded.IsEnabled = is_ugoira;
                    if (is_ugoira) MakeUgoiraConcatFile(file: fp);
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
                foreach (var illusts in new List<ImageListGrid>() { SubIllusts, RelatedItems, FavoriteItems })
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

        private void UpdateDownloadedMark(PixivItem item, bool? exists = null, bool? downloaded = null)
        {
            try
            {
                if (item.IsWork())
                {
                    string fp = string.Empty;
                    var index = item.Index;
                    var count = item.Count;
                    if (count > 1 && index < 0)
                    {
                        var download = downloaded ?? item.Illust.IsPartDownloadedAsync(out fp, touch: exists ?? false);
                        item.IsDownloaded = download;
                        if (download)
                        {
                            IllustDownloaded.Show();
                            IllustDownloaded.Tag = fp;
                            IllustDownloaded.ToolTip = fp;
                            //ToolTipService.SetToolTip(IllustDownloaded, fp);
                            Contents.DownloadedFilePath = fp;
                        }
                        else
                        {
                            IllustDownloaded.Hide();
                            IllustDownloaded.Tag = null;
                            IllustDownloaded.ToolTip = string.Empty;
                            //ToolTipService.SetToolTip(IllustDownloaded, null);
                            Contents.DownloadedFilePath = string.Empty;
                        }
                    }
                    else
                    {
                        var download = downloaded ?? item.Illust.GetOriginalUrl(item.Index).IsDownloadedAsync(out fp, (item.Illust.PageCount ?? 0) <= 1, touch: exists ?? false);
                        item.IsDownloaded = download;
                        if (download)
                        {
                            IllustDownloaded.Show();
                            IllustDownloaded.Tag = fp;
                            IllustDownloaded.ToolTip = fp;
                            //ToolTipService.SetToolTip(IllustDownloaded, fp);
                            Contents.DownloadedFilePath = fp;
                        }
                        else
                        {
                            IllustDownloaded.Hide();
                            IllustDownloaded.Tag = null;
                            IllustDownloaded.ToolTip = string.Empty;
                            //ToolTipService.SetToolTip(IllustDownloaded, null);
                            Contents.DownloadedFilePath = string.Empty;
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
                var old_state = BookmarkIllust.Tag;
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
                //if (!(BookmarkIllust.Tag as string).Equals(old_state as string))
                //    Commands.TouchMeta.Execute(illust);
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
                        if (illustid == -1 || illustid == work_id) UpdateFavMark(Contents.Illust);
                    }
                }
                foreach (var illusts in new List<ImageListGrid>() { SubIllusts, RelatedItems, FavoriteItems })
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
                if (RelatedItems.IsKeyboardFocusWithin)
                    Commands.ChangeIllustLikeState.Execute(RelatedItems);
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
                if (RelatedItems.IsKeyboardFocusWithin)
                    Commands.ChangeUserLikeState.Execute(RelatedItems);
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
                if (RelatedItems.IsKeyboardFocusWithin)
                    Commands.OpenUser.Execute(RelatedItems);
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
                if (RelatedItems.IsKeyboardFocusWithin || RelatedItemsExpander.IsKeyboardFocusWithin)
                    Commands.OpenDownloaded.Execute(RelatedItems);
                else if (FavoriteItems.IsKeyboardFocusWithin || FavoriteItemsExpander.IsKeyboardFocusWithin)
                    Commands.OpenDownloaded.Execute(FavoriteItems);
                else if (RelatedItems.SelectedItems.FirstOrDefault() != null && RelatedItems.SelectedItems.FirstOrDefault().IsFocused)
                    Commands.OpenDownloaded.Execute(RelatedItems);
                else if (FavoriteItems.SelectedItems.FirstOrDefault() != null && FavoriteItems.SelectedItems.FirstOrDefault().IsFocused)
                    Commands.OpenDownloaded.Execute(FavoriteItems);
                else if (Contents.IsWork())
                {
                    if (SubIllusts.Items.Count <= 0)
                    {
                        if (Contents.IsDownloaded)
                            Commands.OpenDownloaded.Execute(Contents);
                        else if (setting.OpenPreviewForNotDownloaded)
                            Commands.OpenWorkPreview.Execute(Contents);
                    }
                    else if (SubIllusts.SelectedItems.Count > 0)
                    {
                        foreach (var item in SubIllusts.GetSelected())
                        {
                            if (item.IsDownloaded)
                                Commands.OpenDownloaded.Execute(item);
                            else if (setting.OpenPreviewForNotDownloaded)
                                Commands.OpenWorkPreview.Execute(item);
                        }
                    }
                    else
                    {
                        if (SubIllusts.Items[0].IsDownloaded)
                            Commands.OpenDownloaded.Execute(SubIllusts.Items[0]);
                        else if (setting.OpenPreviewForNotDownloaded)
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
                if (RelatedItems.IsKeyboardFocusWithin || RelatedItemsExpander.IsKeyboardFocusWithin)
                    Commands.OpenCachedImage.Execute(RelatedItems);
                else if (FavoriteItems.IsKeyboardFocusWithin || FavoriteItemsExpander.IsKeyboardFocusWithin)
                    Commands.OpenCachedImage.Execute(FavoriteItems);
                else if (RelatedItems.SelectedItems.FirstOrDefault() != null && RelatedItems.SelectedItems.FirstOrDefault().IsFocused)
                    Commands.OpenCachedImage.Execute(RelatedItems);
                else if (FavoriteItems.SelectedItems.FirstOrDefault() != null && FavoriteItems.SelectedItems.FirstOrDefault().IsFocused)
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
                if (RelatedItems.IsKeyboardFocusWithin || RelatedItemsExpander.IsKeyboardFocusWithin)
                    Commands.OpenFileProperties.Execute(RelatedItems);
                else if (FavoriteItems.IsKeyboardFocusWithin || FavoriteItemsExpander.IsKeyboardFocusWithin)
                    Commands.OpenFileProperties.Execute(FavoriteItems);
                else if (RelatedItems.SelectedItems.FirstOrDefault() != null && RelatedItems.SelectedItems.FirstOrDefault().IsFocused)
                    Commands.OpenFileProperties.Execute(RelatedItems);
                else if (FavoriteItems.SelectedItems.FirstOrDefault() != null && FavoriteItems.SelectedItems.FirstOrDefault().IsFocused)
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
                                Commands.OpenFileProperties.Execute(Contents.Illust.GetPreviewUrl(large: setting.ShowLargePreview).GetImageCachePath());
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
                if (RelatedItems.IsKeyboardFocusWithin || RelatedItemsExpander.IsKeyboardFocusWithin)
                    Commands.SaveIllust.Execute(RelatedItems);
                else if (FavoriteItems.IsKeyboardFocusWithin || FavoriteItemsExpander.IsKeyboardFocusWithin)
                    Commands.SaveIllust.Execute(FavoriteItems);
                else if (RelatedItems.SelectedItems.FirstOrDefault() != null && RelatedItems.SelectedItems.FirstOrDefault().IsFocused)
                    Commands.SaveIllust.Execute(RelatedItems);
                else if (FavoriteItems.SelectedItems.FirstOrDefault() != null && FavoriteItems.SelectedItems.FirstOrDefault().IsFocused)
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
                if (RelatedItems.IsKeyboardFocusWithin || RelatedItemsExpander.IsKeyboardFocusWithin)
                    Commands.SaveIllustAll.Execute(RelatedItems);
                else if (FavoriteItems.IsKeyboardFocusWithin || FavoriteItemsExpander.IsKeyboardFocusWithin)
                    Commands.SaveIllustAll.Execute(FavoriteItems);
                else if (RelatedItems.SelectedItems.FirstOrDefault() != null && RelatedItems.SelectedItems.FirstOrDefault().IsFocused)
                    Commands.SaveIllustAll.Execute(RelatedItems);
                else if (FavoriteItems.SelectedItems.FirstOrDefault() != null && FavoriteItems.SelectedItems.FirstOrDefault().IsFocused)
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
                    Commands.CopyImage.Execute(PreviewImageUrl.GetImageCacheFile());
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
            else if (RelatedItems.IsFocused || RelatedItems.IsKeyboardFocusWithin || RelatedItems.IsKeyboardFocused)
            {
                List<string> info = new List<string>();
                foreach (var item in RelatedItems.GetSelected(WithSelectionOrder: false, NonForAll: true))
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
                    if (state == TaskStatus.RanToCompletion ||
                        state == TaskStatus.Faulted ||
                        state == TaskStatus.Canceled ||
                        (state != TaskStatus.WaitingForChildrenToComplete && percent >= 100))
                        UpdateThumb(prefetching: false);
                },
                ReportProgress = (percent, tooltip, state) =>
                {
                    if (ParentWindow is MainWindow) (ParentWindow as MainWindow).SetPrefetchingProgress(percent, tooltip, state);
                    if (ParentWindow is ContentWindow) (ParentWindow as ContentWindow).SetPrefetchingProgress(percent, tooltip, state);
                    if (state == TaskStatus.RanToCompletion ||
                        state == TaskStatus.Faulted ||
                        state == TaskStatus.Canceled ||
                        (state != TaskStatus.WaitingForChildrenToComplete && percent >= 100))
                        UpdateThumb(prefetching: false);
                }
            };
        }

        private void InitSubIllustUpdateTimer()
        {
            if (SubIllustUpdateTimer == null)
            {
                SubIllustUpdateTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(150), IsEnabled = false };
                SubIllustUpdateTimer.Tick += (o, e) =>
                {
                    try
                    {
                        if (Contents.HasPages())
                        {
                            SubIllustsExpander.Show();
                            if (SubIllustsExpander.IsExpanded)
                                ShowIllustPagesAsync(Contents);
                            else
                                SubIllustsExpander.IsExpanded = true;
                        }
                        SubIllustUpdateTimer.Stop();
                    }
                    catch (Exception ex) { ex.ERROR("SubIllustUpdateTimer"); }
                };
            }
        }

        private void SetIllustStateInfo(bool querysize = true)
        {
            string tip = null;
            if (Contents.IsWork())
            {
                var Illust = Contents.Illust;
                string stat_viewed = "????";
                string stat_favorited = "????";
                var stat_tip = new List<string>();
                if (Illust is Pixeez.Objects.IllustWork)
                {
                    var illust = Illust as Pixeez.Objects.IllustWork;
                    stat_viewed = $"{illust.total_view}";
                    stat_favorited = $"{illust.total_bookmarks}";
                    stat_tip.Add($"Viewed    : {illust.total_view}");
                    stat_tip.Add($"Favorited : {illust.total_bookmarks}");
                }
                if (Contents.Illust.Stats != null)
                {
                    stat_viewed = $"{Illust.Stats.ViewsCount}";
                    stat_favorited = $"{Illust.Stats.FavoritedCount.Public} / {Illust.Stats.FavoritedCount.Private}";
                    stat_tip.Add($"Scores    : {Illust.Stats.Score}");
                    stat_tip.Add($"Viewed    : {Illust.Stats.ViewsCount}");
                    stat_tip.Add($"Scored    : {Illust.Stats.ScoredCount}");
                    stat_tip.Add($"Comments  : {Illust.Stats.CommentedCount}");
                    stat_tip.Add($"Favorited : {Illust.Stats.FavoritedCount.Public} / {Illust.Stats.FavoritedCount.Private}");
                }
                stat_tip.Add($"Size      : {Illust.Width}x{Illust.Height}");
                tip = string.Join(Environment.NewLine, stat_tip).Trim();

                IllustViewed.Text = stat_viewed;
                IllustFavorited.Text = stat_favorited;

                IllustStatInfo.ToolTip = string.IsNullOrEmpty(tip) ? null : tip;
                UpdateIllustStateInfo(querysize);
            }
        }

        private async void UpdateIllustStateInfo(bool querysize = true)
        {
            if (Contents.IsWork())
            {
                if (IllustStatInfo.ToolTip is string)
                {
                    try
                    {
                        var file = IllustDownloaded.ToolTip as string;
                        var is_down = !string.IsNullOrEmpty(file) && IllustDownloaded.IsShown();
                        var size = querysize ? $" [{(await Contents.Illust.GetOriginalUrl(Contents.Index).QueryImageFileSize() ?? -1).SmartFileSize()}]" : string.Empty;
                        var original = $"Original  : {Contents.Illust.GetOriginalUrl(Contents.Index).GetImageName(Contents.Count <= 1)}{size}";
                        var quality = is_down   ? $"Quality   : {file.GetImageQualityInfo()}" : string.Empty;
                        var diskusage = is_down ? $"FileSize  : {new System.IO.FileInfo(file).Length.SmartFileSize()}" : string.Empty;
                        var tips = (IllustStatInfo.ToolTip as string).Split(Application.Current.GetLineBreak(), StringSplitOptions.RemoveEmptyEntries).ToList();

                        var found_orig = false;
                        var found_size = false;
                        var found_imgq = false;
                        for (int i = tips.Count - 1; i >= 0; i--)
                        {
                            if (tips[i].StartsWith("Original  :"))
                            {
                                found_orig = true;
                                tips[i] = original;
                            }
                            else if (tips[i].StartsWith("Quality   :"))
                            {
                                found_imgq = true;
                                if (string.IsNullOrEmpty(quality)) tips.RemoveAt(i);
                                else tips[i] = quality;
                            }
                            else if (tips[i].StartsWith("FileSize  :"))
                            {
                                found_size = true;
                                if (string.IsNullOrEmpty(diskusage)) tips.RemoveAt(i);
                                else tips[i] = diskusage;
                            }
                        }
                        if (!found_orig) tips.Add(original);
                        if (!found_imgq && !string.IsNullOrEmpty(quality)) tips.Add(quality);
                        if (!found_size && !string.IsNullOrEmpty(diskusage)) tips.Add(diskusage);

                        IllustStatInfo.ToolTip = string.Join(Environment.NewLine, tips).Trim();
                    }
                    catch (Exception ex) { ex.ERROR("UpdateIllustStateInfo"); }
                }
            }
        }

        internal void SetUserFullListedState(PixivItem item = null, bool remove = false)
        {
            try
            {
                if (IsLoaded)
                {
                    if (item == null) item = Contents;
                    if (remove)
                    {
                        item.UserID.SetFullListedUserState(remove: true);
                        UserFullListedFlag.IsEnabled = false;
                        UserFullListedFlag.ToolTip = $"Update Date: Unknown";
                        UserFullListedFlag.Show(show: false);
                    }
                    else
                    {
                        var fulllisted = item.UserID.GetFullListedUserState();
                        var is_fulllisted = !string.IsNullOrEmpty(fulllisted);
                        UserFullListedFlag.IsEnabled = is_fulllisted ? true : false;
                        UserFullListedFlag.ToolTip = $"Update Date: {(is_fulllisted ? fulllisted : "Unknown")}";
                        UserFullListedFlag.Show(show: is_fulllisted);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("SetUserFullListedState"); }
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
            catch (Exception ex) { ex.ERROR("UpdateTheme"); }
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
            catch (Exception ex) { ex.ERROR("UpdateContentsThumbnail"); }
        }

        public async void UpdateThumb(bool full = false, bool overwrite = false, bool prefetching = true, bool wating = true)
        {
            try
            {
                overwrite = Keyboard.Modifiers == ModifierKeys.Alt ? true : overwrite;
                if (Contents.HasUser())
                {
                    if (!(cancelDownloading is CancellationTokenSource) || cancelDownloading.IsCancellationRequested)
                        cancelDownloading = new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout));

                    #region Update gallery thumbnail
                    if (full)
                    {
                        SubIllusts.UpdateTilesImage(overwrite, touch: false);
                        RelatedItems.UpdateTilesImage(overwrite);
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
                            SubIllusts.UpdateTilesImage(overwrite, touch: false);
                        else if (RelatedItemsExpander.IsKeyboardFocusWithin || RelatedItems.IsKeyboardFocusWithin)
                            RelatedItems.UpdateTilesImage(overwrite);
                        else if (FavoriteItemsExpander.IsKeyboardFocusWithin || FavoriteItems.IsKeyboardFocusWithin)
                            FavoriteItems.UpdateTilesImage(overwrite);
                        else
                            UpdateThumb(true, prefetching: false);
                    }
                    UpdateContentsThumbnail(overwrite: overwrite);
                    #endregion

                    #region Create prefetching task
                    InitPrefetchingTask();
                    setting = Application.Current.LoadSetting();
                    if (prefetching && setting.PrefetchingPreview && ParentWindow is ContentWindow && PrefetchingImagesTask is PrefetchingTask)
                    {
                        #region Delay for prefetching preview/thumbnail
                        SemaphoreSlim UpdateThumbDelay = new SemaphoreSlim(0, 1);
                        var IsCanceled = false;
                        if (await UpdateThumbDelay.WaitAsync(setting.PrefetchingDownloadDelay, cancelDownloading.Token))
                        {
                            IsCanceled = false;
                        }
                        if (IsCanceled || cancelDownloading.IsCancellationRequested) return;
                        #endregion

                        var items = new List<PixivItem>();
                        if (Contents.Count <= 1 || Contents.IsUser()) items.Add(Contents);
                        else if (Contents.Count <= 30) items.AddRange(SubIllusts.Items.Where(p => p.Index != Contents.Index));
                        else items.AddRange((await Contents.Illust.PageItems(touch: true)).Where(p => p.Index != Contents.Index));
                        items = items.Union(RelatedItems.FiltedList).Union(FavoriteItems.FiltedList).Union(RelatedItems.Items).Union(FavoriteItems.Items).ToList();
                        if (items.Count > 0)
                        {
                            PrefetchingImagesTask.Items = items;
                            PrefetchingImagesTask.Start(overwrite: overwrite);
                        }
                    }
                    #endregion
                }
            }
            catch (Exception ex) { ex.ERROR("UPATETHUMB"); }
            finally
            {
                IllustDetailWait.Hide();
                this.DoEvents();
            }
        }

        internal async void UpdateDetail(PixivItem item)
        {
            try
            {
                page_count = 0;
                page_number = 0;
                page_index = 0;

                lastMouseDown = Environment.TickCount;

                if (loading_related is SemaphoreSlim) { if (loading_related.CanRelease()) loading_related.Release(); }
                else loading_related = new SemaphoreSlim(1);
                if (loading_favorite is SemaphoreSlim) { if (loading_favorite.CanRelease()) loading_favorite.Release(); }
                else loading_favorite = new SemaphoreSlim(1);

                if (Contents.IsWork() && _urls_ is List<string>)
                {
                    foreach (var url in _urls_)
                    {
                        try { if (!string.IsNullOrEmpty(url)) url.GetImageCachePath().CleenLastDownloaded(); }
                        catch { }
                    }
                }

                var tooltip = string.Empty;
                var force = ModifierKeys.Control.IsModified();
                if (item.IsWork())
                {
                    this.Invoke(() => { PreviewBadge.Opacity = PreviewBadge.IsMouseOver || PreviewBadge.IsMouseDirectlyOver ? 0.75 : 0.33; });

                    PrefetchingImagesTask.Name = $"IllustPagePrefetching_{item.ID}";
                    Contents = item;
                    tooltip = $"{item.ID}, {item.Illust.Title}";
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
                    tooltip = $"{item.User.Id}, {item.User.Name}";
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
                if (ParentWindow is ContentWindow && !string.IsNullOrEmpty(tooltip))
                {
                    (ParentWindow as ContentWindow).SetToolTip("CommandRefresh", tooltip, append: true, append_seprate: Environment.NewLine);
                    (ParentWindow as ContentWindow).SetToolTip("CommandRefreshThumb", tooltip, append: true, append_seprate: Environment.NewLine);
                }
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

                if (item.User == null) item.User = item.Illust.User;
                if (string.IsNullOrEmpty(item.UserID) && item.User is Pixeez.Objects.UserBase) item.UserID = $"{item.User.Id}";

                UpdateUg(item.IsUgoira());

                IllustSize.Text = $"{item.Illust.Width}x{item.Illust.Height}";

                IllustStatInfo.Show();
                IllustStatInfo.ToolTip = waiting;
                SetIllustStateInfo(querysize: false);

                SetUserFullListedState(item);

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

                IllustAiInfo.ToolTip = item.IsAI() ? $"AI Type: {item.AIType}" : null;
                if (item.IsAI()) IllustAiInfo.Show();
                else IllustAiInfo.Hide();

                var dt = item.Illust.GetDateTime();
                var local = CultureInfo.CurrentCulture.ThreeLetterWindowsLanguageName.ToUpper();
                var japan = CultureInfo.GetCultureInfo("ja-JP").ThreeLetterWindowsLanguageName.ToUpper();
                var local_dt = dt.ToString("yyyy-MM-dd HH:mm:sszzz");
                var japan_dt = new DateTimeOffset(dt).ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm:sszzz");

                IllustDate.Text = dt.ToString("yyyy-MM-dd HH:mm:ss");
                IllustDateInfo.ToolTip = $"{local}: {local_dt}{Environment.NewLine}{japan}: {japan_dt}";
                IllustDateInfo.Show();

                if (ContextMenuActionItems.ContainsKey("ActionCopyIllustDate"))
                    ContextMenuActionItems["ActionCopyIllustDate"].Header = item.Illust.GetDateTime().ToString("yyyy-MM-dd HH:mm:sszzz");

                if (ContextMenuActionItems.ContainsKey("ActionCopyIllustID"))
                    ContextMenuActionItems["ActionCopyIllustID"].Header = $"Copy Illust ID : {item.Illust.Id}";
                if (ContextMenuActionItems.ContainsKey("ActionCopyAuthorID"))
                    ContextMenuActionItems["ActionCopyAuthorID"].Header = $"Copy Author ID : {item.User.Id}";

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
                    UpdateIllustTitle();

                    IllustTagsHtml.DocumentText = string.Empty;
                    IllustTagExpander.Hide();
                    IllustTagPedia.Hide();
                }

                if (!string.IsNullOrEmpty(item.Illust.Caption) && item.Illust.Caption.Length > 0)
                {
                    UpdateIllustDesc();

                    if (setting.AutoExpand == AutoExpandMode.AUTO ||
                        setting.AutoExpand == AutoExpandMode.ON ||
                        (setting.AutoExpand == AutoExpandMode.SINGLEPAGE && (item.Illust.PageCount ?? 0) <= 1))
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
                item.Index = -1;
                if (item.IsDownloaded) Commands.TouchMeta.Execute(item);
                if (item.Count <= 1 || item.Index < 0) UpdateDownloadedMark();

                SubIllustUpdateTimer.Stop();
                PreviewBadge.Badge = $"1 / {item.Count}";
                if (item.HasPages())
                {
                    var total = item.Count;
                    page_count = total / PAGE_ITEMS + (total % PAGE_ITEMS > 0 ? 1 : 0);

                    item.Index = 0;
                    PreviewBadge.Show();
                    SubIllustUpdateTimer.Start();
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

                RelatedItems.ClearAsync(setting.BatchClearThumbnails);
                RelatedItemsExpander.Header = "Related Illusts";
                RelatedItemsExpander.IsExpanded = false;
                RelatedItemsExpander.Show();
                RelatedNextPage.Hide();
                RelatedRefresh.Hide();

                FavoriteItems.ClearAsync(setting.BatchClearThumbnails);
                FavoriteItemsExpander.Header = "Author Favorite";
                FavoriteItemsExpander.IsExpanded = false;
                FavoriteItemsExpander.Show();
                FavoriteNextPage.Hide();
                FavoriteRefresh.Hide();
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

                IllustTagRefresh.ContextMenu = null;

                IllustSizeIcon.Text = SymbolIcon_Image;
                IllustSize.Text = $"{nprof.total_illusts + nprof.total_manga}";
                IllustViewedIcon.Text = SymbolIcon_Star;
                IllustViewed.Text = $"{nprof.total_follow_users}";
                IllustFavorited.Text = $"{nprof.total_illust_bookmarks_public}";
                IllustStatInfo.Show();

                SetUserFullListedState(item);

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
                    if (string.IsNullOrEmpty(item.UserID)) item.UserID = $"{nuser.Id ?? -1}";
                    if (item.HasUser())
                    {
                        if (!item.User.Id.HasValue) item.User.Id = nuser.Id ?? -1;
                        if (string.IsNullOrEmpty(item.User.GetAvatarUrl())) nuser.GetAvatarUrl();
                    }

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

                RelatedItemsExpander.Header = "Illusts";
                RelatedItemsExpander.Show();
                RelatedNextPage.Hide();
                RelatedItemsExpander.IsExpanded = false;

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

        #region Subillusts/Related illusts/Favorite illusts helper
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
                        if (subset.PageCount > 1 && subset.meta_pages == null)
                        {
                            var meta_pages = await subset.GetMetaPages();
                            if (meta_pages is List<Pixeez.Objects.MetaPages>) subset.meta_pages = meta_pages.ToArray();
                        }
                        if (subset.meta_pages is IEnumerable<Pixeez.Objects.MetaPages> && subset.meta_pages.Count() > 1)
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
                        if ((subset.PageCount ?? 0) >= 1 && subset.Metadata == null)
                        {
                            item.Illust.Metadata = await subset.GetMetaData();
                            //var illust = await item.Illust.RefreshIllust();
                            //if (illust is Pixeez.Objects.Work) item.Illust = illust;
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
                    if (ParentWindow is MainWindow) SubIllusts.UpdateTilesImage(touch: false);
                    else if (ParentWindow is ContentWindow) UpdateThumb(prefetching: PrefetchingImagesTask.State == TaskStatus.Created);
                }
            }
            catch (Exception ex)
            {
                ex.ERROR(Name ?? "SubIllusts");
            }
            finally
            {
                //UpdateGalleryTooltip(SubIllusts);
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

        private SemaphoreSlim loading_related = new SemaphoreSlim(1);
        private SemaphoreSlim loading_favorite = new SemaphoreSlim(1);
        private CancellationTokenSource cancel_related = new CancellationTokenSource();
        private CancellationTokenSource cancel_favorite = new CancellationTokenSource();

        private void CancelRelatedLoading()
        {
            if (loading_related.CanRelease() && cancel_related is CancellationTokenSource) cancel_related.Cancel();
        }

        private void CancelFavoriteLoading()
        {
            if (loading_favorite.CanRelease() && cancel_favorite is CancellationTokenSource) cancel_favorite.Cancel();
        }

        private List<long?> related_illusts = new List<long?>();
        private async Task ShowRelatedInline(PixivItem item, string next_url = "", bool append = false)
        {
            if (!(loading_related is SemaphoreSlim)) loading_related = new SemaphoreSlim(1);
            if (await loading_related.WaitAsync(50))
            {
                try
                {
                    RelatedItems.Wait();
                    if (!(related_illusts is List<long?>)) related_illusts = new List<long?>();
                    if (!append)
                    {
                        RelatedItems.Clear(setting.BatchClearThumbnails);
                        related_illusts.Clear();
                    }

                    var tokens = await CommonHelper.ShowLogin();
                    if (tokens == null) return;

                    var lastUrl = next_url;
                    if (string.IsNullOrEmpty(next_url)) FavoriteNextPage.ToolTip = null;
                    var related = string.IsNullOrEmpty(next_url) ? await tokens.GetRelatedWorks(item.Illust.Id.Value) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);

                    if (related is Pixeez.Objects.Illusts && related.illusts is Array)
                    {
                        next_url = related.next_url ?? string.Empty;

                        RelatedNextPage.Show(!next_url.Equals(lastUrl, StringComparison.CurrentCultureIgnoreCase));

                        if (!append)
                        {
                            RelatedItemsExpander.Tag = lastUrl;
                            CurrentRelatedURL = lastUrl;
                        }
                        RelatedNextURL = next_url;
                        RelatedNextPage.Tag = next_url;
                        RelatedNextPage.ToolTip = next_url.CalcUrlPageHint(0, RelatedNextPage.ToolTip is string ? RelatedNextPage.ToolTip as string : null);
                        RelatedNextPage.Show(show: !string.IsNullOrEmpty(CurrentRelatedURL) || !string.IsNullOrEmpty(next_url));
                        RelatedNextAppend.Show(show: !string.IsNullOrEmpty(CurrentRelatedURL) || !string.IsNullOrEmpty(next_url));

                        foreach (var illust in related.illusts)
                        {
                            if (related_illusts.Contains(illust.Id)) continue;
                            related_illusts.Add(illust.Id);
                            illust.Cache();
                            illust.AddTo(RelatedItems.Items, related.next_url);

                            if (Contents.HasUser() && Contents.IsSameUser(illust))
                            {
                                if (string.IsNullOrEmpty(Contents.UserAvatarUrl) ||
                                    string.IsNullOrEmpty(Contents.User.GetAvatarUrl()) ||
                                    !Contents.UserAvatarUrl.Equals(illust.GetAvatarUrl()))
                                {
                                    illust.User.Cache();
                                    Contents.User = illust.User;
                                    Contents.UserAvatarUrl = illust.GetAvatarUrl();
                                    $"Update User : {Contents.User.Name} / {Contents.User.Id}, {Contents.UserAvatarUrl}".DEBUG("ShowRelatedInline");
                                }
                            }
                            this.DoEvents();
                        }
                        this.DoEvents();
                        //RelatedItems.UpdateTilesImage();
                        if (related.illusts.Count() <= 0) "No Result".ShowToast("INFO", tag: "ShowRelated");
                        else UpdateThumb();
                    }
                }
                catch (Exception ex)
                {
                    ex.ERROR(this.Name ?? "RelatedItems");
                }
                finally
                {
                    RelatedNextAppend.ToolTip = RelatedItemsExpander.ToolTip;
                    //UpdateGalleryTooltip(RelatedItems);
                    RelatedItems.Ready();
                    if (RelatedItems.Items.Count > 0) { RelatedRefresh.Show(); RelatedCompare.Show(); }
                    else { RelatedRefresh.Hide(); RelatedCompare.Hide(); }
                    if (loading_related is SemaphoreSlim && loading_related.CanRelease()) loading_related.Release();
                }
            }
        }

        private async void ShowRelatedInlineAsync(PixivItem item, string next_url = "", bool append = false)
        {
            await new Action(async () =>
            {
                await ShowRelatedInline(item, next_url, append);
            }).InvokeAsync();
        }

        private async Task ShowUserWorksInline(Pixeez.Objects.UserBase user, string next_url = "", bool append = false, bool append_all = false)
        {
            Func<int> GetTotalIllust = () =>
            {
                var result = -1;
                if (user is Pixeez.Objects.UserBase && user.Id != null && user.Id.HasValue)
                {
                    var prof = user.FindUserInfo();
                    if(prof is Pixeez.Objects.UserInfo)
                    {
                        result = prof.profile.total_illusts + prof.profile.total_manga;
                    }
                }
                return (result);
            };

            if (!(loading_related is SemaphoreSlim)) loading_related = new SemaphoreSlim(1);
            if (await loading_related.WaitAsync(50))
            {
                try
                {
                    if (!(cancel_related is CancellationTokenSource)) cancel_related = new CancellationTokenSource();

                    if (user is Pixeez.Objects.UserBase && user.Id != null && user.Id.HasValue)
                    {
                        int total = string.IsNullOrEmpty(IllustSize.Text) ? GetTotalIllust() : Convert.ToInt32(IllustSize.Text);
                        var offset = string.IsNullOrEmpty(CurrentRelatedURL) ? 0 : CurrentRelatedURL.CalcPageOffset();
                        if (append && total >= 0 && offset + RelatedItems.Items.Count >= total) return;

                        StopPrefetching();
#if DEBUG
                        if (!string.IsNullOrEmpty(next_url)) next_url.DEBUG("ShowUserWorksInline");
#endif
                        RelatedItems.Wait();
                        if (!(related_illusts is List<long?>)) related_illusts = new List<long?>();
                        if (!append)
                        {
                            RelatedItems.Clear(setting.BatchClearThumbnails);
                            related_illusts.Clear();
                        }

                        var tokens = await CommonHelper.ShowLogin();
                        if (tokens == null) return;

                        do
                        {
                            var lastUrl = next_url;
                            var related = string.IsNullOrEmpty(next_url) ? await tokens.GetUserWorksAsync(user.Id.Value) : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(next_url);

                            if (cancel_related is CancellationTokenSource && cancel_related.IsCancellationRequested) break;
                            if (related is Pixeez.Objects.Illusts && related.illusts is Array)
                            {
                                next_url = related.next_url ?? string.Empty;

                                RelatedNextPage.Show(!next_url.Equals(lastUrl, StringComparison.CurrentCultureIgnoreCase));

                                if (!append)
                                {
                                    RelatedItemsExpander.Tag = lastUrl;
                                    CurrentRelatedURL = lastUrl;

                                    RelatedPrevPage.Tag = string.IsNullOrEmpty(CurrentRelatedURL) ? Contents.MakeUserWorkNextUrl().CalcPrevUrl(totals: IllustSize.Text) : CurrentRelatedURL.CalcPrevUrl(totals: IllustSize.Text);
                                }

                                RelatedNextURL = next_url;

                                RelatedPrevPage.ToolTip = RelatedNextPage.ToolTip;
                                RelatedPrevPage.Show(show: IllustSize.Text.CalcTotalPages() > 1);

                                RelatedNextPage.Tag = next_url;
                                RelatedNextPage.ToolTip = CurrentRelatedURL.CalcUrlPageHint(IllustSize.Text);
                                RelatedNextPage.Show(show: IllustSize.Text.CalcTotalPages() > 1);

                                RelatedNextAppend.ToolTip = RelatedItemsExpander.ToolTip;
                                RelatedNextAppend.Show(show: IllustSize.Text.CalcTotalPages() > 1);

                                foreach (var illust in related.illusts)
                                {
                                    if (related_illusts.Contains(illust.Id)) continue;
                                    related_illusts.Add(illust.Id);
                                    illust.Cache();
                                    illust.AddTo(RelatedItems.Items, related.next_url);

                                    if (Contents.HasUser() && Contents.IsSameUser(illust))
                                    {
                                        if (string.IsNullOrEmpty(Contents.UserAvatarUrl) ||
                                            string.IsNullOrEmpty(Contents.User.GetAvatarUrl()) ||
                                            !Contents.UserAvatarUrl.Equals(illust.GetAvatarUrl()))
                                        {
                                            illust.User.Cache();
                                            Contents.User = illust.User;
                                            Contents.UserAvatarUrl = illust.GetAvatarUrl();
                                            $"Update User : {Contents.User.Name} / {Contents.User.Id}, {Contents.UserAvatarUrl}".DEBUG("ShowUserWorksInline");
                                        }
                                    }
                                    this.DoEvents();
                                }
                                this.DoEvents();
                                //RelatedItems.UpdateTilesImage();
                                if (related.illusts.Count() <= 0) { "No Result".ShowToast("INFO", tag: "ShowUserWorks"); break; }
                                else UpdateThumb(prefetching: !append_all);
                            }
                            else { "Excepted Abort".ShowToast("INFO", tag: "ShowUserWorks"); break; };

                            if (cancel_related is CancellationTokenSource && cancel_related.IsCancellationRequested) break;
                        } while (append_all && RelatedItems.Items.Count < total && !string.IsNullOrEmpty(next_url));

                        if (RelatedItems.Items.Count >= total)
                        {
                            user.Id.ToString().SetFullListedUserState();
                            Application.Current.SetUserFullListedState();
                        }
                    }
                    else throw new WarningException($"ShowUserWorksInline_NullOfUserId");
                }
                catch (WarningException ex) { ex.WARN(Name ?? "UserWorks"); }
                catch (Exception ex) { ex.ERROR(Name ?? "UserWorks", no_stack: ex is WarningException); }
                finally
                {
                    UpdateThumb(prefetching: !append_all || (append_all && setting.PrefetchingPreviewAfterAppendAll));
                    this.DoEvents();

                    RelatedNextAppend.ToolTip = RelatedItemsExpander.ToolTip;
                    //UpdateGalleryTooltip(RelatedItems);
                    RelatedItems.Ready();
                    if (RelatedItems.Items.Count > 0) RelatedRefresh.Show();
                    else RelatedRefresh.Hide();
                    if (loading_related is SemaphoreSlim && loading_related.CanRelease()) loading_related.Release();
                    cancel_related = new CancellationTokenSource();
                }
            }
        }

        private async void ShowUserWorksInlineAsync(Pixeez.Objects.UserBase user, string next_url = "", bool append = false, bool append_all = false)
        {
            await new Action(async () =>
            {
                await ShowUserWorksInline(user, next_url, append, append_all);
            }).InvokeAsync();
        }

        private string last_restrict = string.Empty;
        private List<long?> favorite_illusts = new List<long?>();
        private async Task ShowFavoriteInline(Pixeez.Objects.UserBase user, string next_url = "", bool append = false, bool append_all = false)
        {
            if (!(loading_related is SemaphoreSlim)) loading_related = new SemaphoreSlim(1);
            if (await loading_favorite.WaitAsync(50))
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

                    if (favorites is Pixeez.Objects.Illusts && favorites.illusts is Array)
                    {
                        next_url = favorites.next_url ?? string.Empty;
                        last_restrict = restrict;

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

                        FavoriteNextAppend.ToolTip = FavoriteItemsExpander.ToolTip;
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
                    FavoriteNextAppend.ToolTip = FavoriteItemsExpander.ToolTip;
                    //UpdateGalleryTooltip(FavoriteItems);
                    FavoriteItems.Ready();
                    if (FavoriteItems.Items.Count > 0) { FavoriteRefresh.Show(); FavoriteCompare.Show(); }
                    else { FavoriteItems.Hide(); FavoriteCompare.Hide(); }
                    if (loading_favorite is SemaphoreSlim && loading_favorite.CurrentCount == 0) loading_favorite.Release();
                }
            }
        }

        private async void ShowFavoriteInlineAsync(Pixeez.Objects.UserBase user, string next_url = "", bool append = false, bool append_all = false)
        {
            await new Action(async () =>
            {
                await ShowFavoriteInline(user, next_url, append, append_all);
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
            if (RelatedItems.IsKeyboardFocusWithin)
            {
                RelatedItems.MoveCurrentToFirst();
                RelatedItems.ScrollIntoView(RelatedItems.SelectedItem);
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
            if (RelatedItems.IsKeyboardFocusWithin)
            {
                RelatedItems.MoveCurrentToLast();
                RelatedItems.ScrollIntoView(RelatedItems.SelectedItem);
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
            catch (Exception ex) { ex.ERROR("PrevIllustPage"); }
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
                catch (Exception ex) { ex.ERROR("NextIllustPage"); }
            }
        }

        public void SetFilter(string filter)
        {
            RelatedItems.SetFilter(filter);
            FavoriteItems.SetFilter(filter);
        }

        public void SetFilter(FilterParam filter)
        {
            RelatedItems.SetFilter(filter);
            FavoriteItems.SetFilter(filter);
        }

        public dynamic GetTilesCount()
        {
            List<string> tips = new List<string>();
            tips.Add($"Related : {RelatedItems.ItemsCount}({RelatedItems.SelectedItems.Count}) of {RelatedItems.Items.Count}");
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
            SubIllusts.Cancel();
            RelatedItems.Cancel();
            FavoriteItems.Cancel();
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

                        #region Clear downloading cache for this illust.
                        if (Contents.IsWork() && _urls_ is List<string>)
                        {
                            foreach (var url in _urls_)
                            {
                                try { if (!string.IsNullOrEmpty(url)) url.GetImageCachePath().ClearDownloading(); }
                                catch { }
                            }
                        }
                        //if (Contents.IsWork())
                        //{
                        //    var previews = new List<string>();
                        //    var illust = Contents.Illust;
                        //    if (Contents.HasPages())
                        //    {
                        //        if (illust is Pixeez.Objects.IllustWork)
                        //        {
                        //            var subset = illust as Pixeez.Objects.IllustWork;
                        //            if (subset.meta_pages.Count() > 1)
                        //            {
                        //                foreach (var page in subset.meta_pages)
                        //                {
                        //                    try { previews.Add(page.GetPreviewUrl()); }
                        //                    catch { }
                        //                }
                        //            }
                        //        }
                        //        else if (illust is Pixeez.Objects.NormalWork)
                        //        {
                        //            var subset = illust as Pixeez.Objects.NormalWork;
                        //            if (illust.Metadata is Pixeez.Objects.Metadata)
                        //            {
                        //                foreach (var page in illust.Metadata.Pages)
                        //                {
                        //                    try { previews.Add(page.GetPreviewUrl()); }
                        //                    catch { }
                        //                }
                        //            }
                        //        }
                        //    }
                        //    previews.Add(illust.GetPreviewUrl(large: setting.ShowLargePreview));
                        //    foreach (var url in previews)
                        //    {
                        //        try { if (!string.IsNullOrEmpty(url)) url.GetImageCacheFile().CleenLastDownloaded(); }
                        //        catch { }
                        //    }
                        //}
                        #endregion

                        SubIllusts.Clear(batch: false, force: true);
                        this.DoEvents();
                        RelatedItems.Clear(batch: false, force: true);
                        this.DoEvents();
                        FavoriteItems.Clear(batch: false, force: true);
                        this.DoEvents();

                        if (PreviewPopupTimer is DispatcherTimer)
                        {
                            PreviewPopupTimer.Stop();
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

            RelatedItems.Columns = 5;
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
            IllustTagSearchInFile.MouseOverAction();
            IllustTagSpeech.MouseOverAction();
            IllustTagRefresh.MouseOverAction();

            IllustDescSpeech.MouseOverAction();
            IllustDescRefresh.MouseOverAction();

            SubIllustPrevPages.MouseOverAction();
            SubIllustNextPages.MouseOverAction();
            SubIllustRefresh.MouseOverAction();
            SubIllustCompare.MouseOverAction();

            RelatedPrevPage.MouseOverAction();
            RelatedNextPage.MouseOverAction();
            RelatedNextAppend.MouseOverAction();
            RelatedRefresh.MouseOverAction();
            RelatedCompare.MouseOverAction();
            RelatedCompare.Hide();

            FavoritePrevPage.MouseOverAction();
            FavoriteNextPage.MouseOverAction();
            FavoriteNextAppend.MouseOverAction();
            FavoriteRefresh.MouseOverAction();
            FavoriteCompare.MouseOverAction();
            FavoriteCompare.Hide();
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

            InitSubIllustUpdateTimer();

            #region Prefetching
            InitPrefetchingTask();
            #endregion

            IllustStatInfo.ToolTipOpening += (obj, evt) =>
            {
                var setting = Application.Current.LoadSetting();
                if (IllustStatInfo.ToolTip is string && (IllustStatInfo.ToolTip as string).Equals(waiting, StringComparison.CurrentCultureIgnoreCase))
                    SetIllustStateInfo(querysize: setting.QueryOriginalImageSize);
                else UpdateIllustStateInfo(querysize: setting.QueryOriginalImageSize);
            };
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
                    else if (e.XButton1 == MouseButtonState.Pressed && e.LeftButton == MouseButtonState.Pressed)
                    {
                        (this.ParentWindow is Window ? this.ParentWindow : this as UIElement).AllowDrop = false;
                        IllustDetailViewer.AllowDrop = false;
                        PreviewRect.AllowDrop = false;
                        e.Handled = true;
                        this.DragOut(PreviewImageUrl.GetImageCacheFile());
                    }
                }
                else
                {
                    if (e.XButton1 == MouseButtonState.Pressed && e.LeftButton == MouseButtonState.Released)
                    {
                        e.Handled = true;
                        if (setting.ReverseMouseXButton) NextIllustPage();
                        else PrevIllustPage();
                    }
                    else if (e.XButton2 == MouseButtonState.Pressed && e.LeftButton == MouseButtonState.Released)
                    {
                        e.Handled = true;
                        if (setting.ReverseMouseXButton) PrevIllustPage();
                        else NextIllustPage();
                    }
                    else if (e.XButton1 == MouseButtonState.Pressed && e.LeftButton == MouseButtonState.Pressed)
                    {
                        (this.ParentWindow is Window ? this.ParentWindow : this as UIElement).AllowDrop = false;
                        IllustDetailViewer.AllowDrop = false;
                        PreviewRect.AllowDrop = false;
                        e.Handled = true;
                        this.DragOut(PreviewImageUrl.GetImageCacheFile());
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("IllustDetailPreviewMouseDown"); }
            finally { PreviewRect.AllowDrop = true; IllustDetailViewer.AllowDrop = true; (this.ParentWindow is Window ? this.ParentWindow : this as UIElement).AllowDrop = true; }
        }

        private void PreviewBadge_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Invoke(() => { PreviewBadge.Opacity = 0.75; });
        }

        private void PreviewBadge_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Invoke(() => { PreviewBadge.Opacity = 0.25; });
        }

        private void DownloadAction_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu && Contents.IsWork())
            {
                var down_list = new string[]
                {
                    "ActionConvertIllustJpegSep",
                    "ActionConvertIllustJpeg", "ActionConvertIllustJpegAll",
                    "ActionReduceIllustJpeg", "ActionReduceIllustJpegAll", "ActionReduceJpegSizeTo",
                    "ActionDownloadedSep",
                    "ActionCompareDownloaded", "ActionShowDownloadedMeta", "ActionTouchDownloadedMeta",
                    "ActionOpenDownloaded", "ActionOpenDownloadedProperties"
                };
                var conv_list = new string[]
                {
                    //"ActionConvertIllustJpegSep",
                    "ActionConvertIllustJpeg", "ActionConvertIllustJpegAll",
                    //"ActionReduceIllustJpeg", "ActionReduceIllustJpegAll", "ActionReduceJpegSizeTo",
                };
                var jpeg_list = new string[]
                {
                    "ActionSaveIllustJpeg", "ActionSaveIllustJpegAll",
                };
                var ugoira_list = new string[] { "SepratorUgoira", "ActionSavePreviewUgoiraFile", "ActionSaveOriginalUgoiraFile", "ActionOpenUgoiraFile" };

                var single = Contents.Count <= 1;
                var file = Contents.Illust.GetOriginalUrl(Contents.Index);
                var is_jpg = System.IO.Path.GetExtension(file).Equals(".jpg", StringComparison.CurrentCultureIgnoreCase);
                var is_down = Contents.IsDownloaded;
                var fpath = Contents.DownloadedFilePath;
                var menus = sender as ContextMenu;
                var items = menus.FindChildren<UIElement>();
                foreach (UIElement item in items)
                {
                    if (item is MenuItem || item is Separator)
                    {
                        var uid = item.GetUid();
                        if (!string.IsNullOrEmpty(uid))
                        {
                            if (!is_down && down_list.Contains(uid))
                            {
                                item.Hide();
                            }
                            else
                            {
                                if (is_jpg && conv_list.Contains(uid)) item.Hide();
                                //else if (is_jpg && jpeg_list.Contains(uid)) item.Hide();
                                else if (item is MenuItem && jpeg_list.Contains(uid))
                                {
                                    var all = uid.EndsWith("All") ? " All " : " ";
                                    (item as MenuItem).Header = is_jpg ? $"Save{all}Illust And Reduce It" : $"Save{all}Illust As JPEG";
                                    if (single && uid.EndsWith("All")) item.Hide();
                                }
                                else if (!Contents.IsUgoira() && ugoira_list.Contains(uid)) item.Hide();
                                else if (single && uid.Contains("All")) item.Hide();
                                else (item as UIElement).Show();
                            }

                            if (uid.Equals("ActionReduceJpegSizeTo"))
                            {
                                if (item is MenuItem)
                                {
                                    var menu = item as MenuItem;
                                    if (menu.Tag == null) menu.Tag = Application.Current.GetDefaultReduceData();
                                    if (is_down && menu.Tag is App.MenuItemSliderData)
                                    {
                                        var data = menu.Tag as App.MenuItemSliderData;
                                        var q = fpath.GetImageQualityInfo();
                                        if (q > 0 && q != data.Value)
                                        {
                                            data.Value = q;
                                            menu.Tag = null;
                                            menu.Tag = data;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        #region Preview Popup
        private async void PreviewPopupTimer_Tick(object sender, EventArgs e)
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
            if (PreviewPopup is Popup && PreviewPopupTimer is DispatcherTimer)
            {
                PreviewPopupTimer.Start();
            }
        }

        private void PreviewPopup_Closed(object sender, EventArgs e)
        {
            if (PreviewPopup is Popup && PreviewPopupTimer is DispatcherTimer)
            {
                PreviewPopupTimer.Stop();
            }
        }

        private void PreviewPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            if (PreviewPopup is Popup && PreviewPopup.IsOpen && PreviewPopupTimer is DispatcherTimer)
            {
                PreviewPopupTimer.Stop();
            }
        }

        private void PreviewPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            if (PreviewPopup is Popup && PreviewPopup.IsOpen && PreviewPopupTimer is DispatcherTimer)
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
                if (uid.Equals("ActionCopyIllustTitle", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        Commands.CopyText.Execute($"{IllustTitle.Text}");
                    else
                        Commands.CopyText.Execute($"title:{IllustTitle.Text}");
                }
                else if (uid.Equals("ActionCopyIllustID", StringComparison.CurrentCultureIgnoreCase) || sender == PreviewCopyIllustID)
                {
                    if (Contents.IsWork()) Commands.CopyArtworkIDs.Execute(Contents);
                }
                else if (uid.Equals("PreviewCopyImage", StringComparison.CurrentCultureIgnoreCase) || sender == PreviewCopyImage)
                {
                    CopyPreview();
                }
                else if (uid.Equals("ActionCopyIllustDate", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (ContextMenuActionItems.ContainsKey(uid)) Commands.CopyText.Execute(ContextMenuActionItems[uid].Header);
                }
                else if (uid.Equals("ActionCopyIllustJson", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (Contents.IsWork()) Commands.CopyJson.Execute(Contents);
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
                else if (new object[] { IllustTitle, IllustAuthor, IllustDateInfo, IllustDate }.Contains(sender))
                {
                    ActionCopySelectedText_Click(sender, e);
                }
                e.Handled = true;
            }
            catch (Exception ex) { ex.ERROR("IllustActions"); }
        }

        private async void ActionAuthourInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var uid = sender.GetUid();
                if (uid.Equals("ActionAuthorInfo", StringComparison.CurrentCultureIgnoreCase) || sender == btnAuthorAvatar)
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
                    else if (Contents.IsWork())
                    {
                        Commands.OpenUser.Execute(Contents);
                    }
                }
                else if (uid.Equals("ActionAuthorWebPage", StringComparison.CurrentCultureIgnoreCase) || sender == btnAuthorAvatar)
                {
                    if (Contents.HasUser())
                    {
                        var href = Contents.UserID.ArtistLink();
                        href.OpenUrlWithShell();
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
                else if (uid.Equals("ActionCopyAuthor", StringComparison.CurrentCultureIgnoreCase) || sender == IllustAuthor)
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
                else if (sender == AuthorAvatarWait)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        ActionRefreshAvatar();
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        ActionRefreshAvatar(true);
                }
                e.Handled = true;
            }
            catch (Exception ex) { ex.ERROR("IllustActions"); }
        }

        private void ActionShowIllustPages_Click(object sender, RoutedEventArgs e)
        {
            SubIllustsExpander.IsExpanded = !SubIllustsExpander.IsExpanded;
        }

        private void ActionShowRelated_Click(object sender, RoutedEventArgs e)
        {
            if (!RelatedItemsExpander.IsExpanded) RelatedItemsExpander.IsExpanded = true;
        }

        private void ActionShowFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (!FavoriteItemsExpander.IsExpanded) FavoriteItemsExpander.IsExpanded = true;
        }

        private void ActionOpenIllust_Click(object sender, RoutedEventArgs e)
        {
            var uid = sender.GetUid();
            //if (sender == PreviewOpenDownloaded || (sender is MenuItem && (sender as MenuItem).Uid.Equals("ActionOpenDownloaded", StringComparison.CurrentCultureIgnoreCase)))
            if (sender == PreviewOpenDownloaded || uid.Equals("ActionOpenDownloaded", StringComparison.CurrentCultureIgnoreCase))
            {
                var shift = Keyboard.Modifiers == ModifierKeys.Shift;
                if (Contents.Count <= 1 || SubIllusts.SelectedItems.Count == 0)
                {
                    if (shift) Commands.CopyDownloadedPath.Execute(Contents);
                    else Commands.OpenDownloaded.Execute(Contents);
                }
                else
                {
                    if (shift) Commands.CopyDownloadedPath.Execute(SubIllusts);
                    else Commands.OpenDownloaded.Execute(SubIllusts);
                }
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
                Commands.OpenCachedImage.Execute(string.IsNullOrEmpty(PreviewImagePath) ? Contents.Illust.GetPreviewUrl(large: setting.ShowLargePreview).GetImageCacheFile() : PreviewImagePath);
            }
            else if (sender == PreviewOpenDownloadedProperties || uid.Equals("ActionOpenDownloadedProperties", StringComparison.CurrentCultureIgnoreCase))
            {
                if (Contents.Count <= 1 || SubIllusts.SelectedItems.Count == 0)
                    Commands.OpenFileProperties.Execute(Contents);
                else
                    Commands.OpenFileProperties.Execute(SubIllusts);
            }
            else if (uid.Equals("ActionCompareDownloaded", StringComparison.CurrentCultureIgnoreCase))
            {
                if ((Contents.Count <= 1 || SubIllusts.SelectedItems.Count == 0) && !string.IsNullOrEmpty(Contents.DownloadedFilePath))
                    Commands.Compare.Execute(Contents.DownloadedFilePath);
                else
                {
                    var items = SubIllusts.GetSelected().Where(i => !string.IsNullOrEmpty(i.DownloadedFilePath)).Select(i => i.DownloadedFilePath).ToList();
                    if (items.Count > 0) Commands.Compare.Execute(items);
                }
            }
            else if (sender == ActionShowDownloadedMeta || uid.Equals("ActionShowDownloadedMeta", StringComparison.CurrentCultureIgnoreCase))
            {
                if (Contents.Count <= 1 || SubIllusts.SelectedItems.Count == 0)
                    Commands.ShowMeta.Execute(Contents);
                else
                    Commands.ShowMeta.Execute(SubIllusts);
            }
            else if (sender == ActionTouchDownloadedMeta || uid.Equals("ActionTouchDownloadedMeta", StringComparison.CurrentCultureIgnoreCase))
            {
                if (Contents.Count <= 1 || SubIllusts.SelectedItems.Count == 0)
                    Commands.TouchMeta.Execute(Contents);
                else
                    Commands.TouchMeta.Execute(SubIllusts);
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
                    cancelDownloading = new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout));

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
                        long id = 0;
                        if ((Contents.Illust.Id ?? -1) <= 0 && long.TryParse(Contents.ID, out id)) Contents.Illust.Id = id;
                        if (string.IsNullOrEmpty(AvatarImageUrl) && (Contents.Illust.Id ?? -1) > 0)
                        {
                            var illusts = await (Contents.Illust.Id ?? -1).SearchIllustById(null);
                            if (illusts is List<Pixeez.Objects.Work> && illusts.Count > 0) Contents.Illust.ImageUrls = illusts.First().ImageUrls;
                            //throw new WarningException("Preview URLs is NULL");
                            "Preview URLs is NULL".WARN("ActionRefreshPreview");
                        }
                        if (_urls_ is List<string>) _urls_.Add(PreviewImageUrl);
                        if (Keyboard.Modifiers == ModifierKeys.Control) { PreviewImageUrl.GetImageCachePath().ClearDownloading(); }

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
                                    //SetIllustStateInfo();
                                    UpdateIllustStateInfo();
                                }
                                else PreviewWait.Fail();
                            }
                        }
                    }
                    catch (Exception ex) { ex.ERROR("ActionRefreshPreview", no_stack: ex is WarningException); PreviewWait.Fail(); }
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
                            cancelDownloading = new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout));

                        AuthorAvatarWait.Show();
                        btnAuthorAvatar.Show(AuthorAvatarWait.IsFail);

                        var c_item = Contents;
                        Contents.User = Contents.UserID.FindUser();
                        AvatarImageUrl = Contents.User.GetAvatarUrl();
                        if (string.IsNullOrEmpty(AvatarImageUrl)) AvatarImageUrl = Contents.UserAvatarUrl;
                        if (string.IsNullOrEmpty(AvatarImageUrl))
                        {
                            long uid = -1;
                            if ((Contents.User.Id ?? -1) <= 0 && long.TryParse(Contents.UserID, out uid)) Contents.User.Id = uid;
                            if (uid >= 0)
                            {
                                var users = await Contents.Illust.User.Id.Value.SearchUserById(null);
                                if (users.Count > 0) Contents.User = users.First();
                                else if (Contents.IsWork()) Contents.User = Contents.Illust.User.Id.FindUser();
                                else if (Contents.IsUser()) Contents.User = Contents.UserID.FindUser();
                                "User Avatar URLs is NULL".WARN("ActionRefreshAvatar");
                                //throw new WarningException("User Avatar URLs is NULL");
                            }
                            else "User ID is NULL".WARN("ActionRefreshAvatar");
                        }
                        if (_urls_ is List<string>) _urls_.Add(AvatarImageUrl);
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
                    catch (Exception ex) { ex.ERROR("ActionRefreshAvatar", no_stack: ex is WarningException); AuthorAvatarWait.Fail(); btnAuthorAvatar.Show(); }
                    finally
                    {
                        if (IllustAuthorAvatar.Source == null) AuthorAvatarWait.Fail();
                        btnAuthorAvatar.Show(AuthorAvatarWait.IsFail);
                        AvatarImageUrl.DEBUG("RefreshAvatar");
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
                else if (sender == IllustSanityInfo)
                    text = $"R{IllustSanity.Text}";
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

                    var host = mi.GetContextMenuHost();
                    if (host == IllustTagSpeech) { is_tag = true; text = IllustTagsHtml.GetText(); }
                    else if (host == IllustDescSpeech) text = IllustDescHtml.GetText();
                    else if (host == IllustAuthor) text = IllustAuthor.Text;
                    else if (host == IllustTitle) text = IllustTitle.Text;
                    else if (host == IllustDateInfo || host == IllustDate) text = IllustDate.Text;
                    else if (host == IllustSanityInfo) text = $"R{IllustSanity.Text}";
                    else if (host == SubIllustsExpander || host == SubIllusts) text = IllustTitle.Text;
                    else if (host == RelatedItemsExpander || host == RelatedItems)
                    {
                        List<string> lines = new List<string>();
                        foreach (PixivItem item in RelatedItems.GetSelected())
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
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception ex) { ex.ERROR("ActionSpeech"); }
#endif
            if (is_tag)
                text = string.Join(Environment.NewLine, text.Trim().Split(Speech.TagBreak, StringSplitOptions.RemoveEmptyEntries));
            else
                text = string.Join(Environment.NewLine, text.Trim().Split(Speech.LineBreak, StringSplitOptions.RemoveEmptyEntries));

            if (!string.IsNullOrEmpty(text)) text.Normalizing().Play(culture);
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

                    var host = mi.GetContextMenuHost();
                    if (host == IllustTagSpeech) { is_tag = true; text = IllustTagsHtml.GetText(); }
                    else if (host == IllustDescSpeech) text = IllustDescHtml.GetText();
                    else if (host == IllustAuthor) text = IllustAuthor.Text;
                    else if (host == IllustTitle) text = IllustTitle.Text;
                    else if (host == IllustDateInfo || host == IllustDate) text = IllustDate.Text;
                    else if (host == SubIllustsExpander || host == SubIllusts) text = IllustTitle.Text;
                    else if (host == RelatedItemsExpander || host == RelatedItems)
                    {
                        List<string> lines = new List<string>();
                        foreach (PixivItem item in RelatedItems.GetSelected())
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

        private void ActionSearchSelectedText_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Empty;
            var scope = StorageSearchScope.None;
            var mode = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? StorageSearchMode.And : StorageSearchMode.Or;
            var fuzzy = !Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            var searching_web = false;
            var is_tag = false;
            try
            {
                if (sender == IllustTagSpeech || sender == IllustTagSearchInFile)
                { is_tag = true; text = IllustTagsHtml.GetText(); scope |= StorageSearchScope.Tag; }
                else if (sender == IllustDescSpeech || sender == IllustDescSearchInFile)
                { text = fuzzy ? IllustDescHtml.GetText() : $"={IllustDescHtml.GetText()}"; scope |= StorageSearchScope.Description; }
                else if (sender == IllustTitle)
                { text = fuzzy ? IllustTitle.Text : $"={IllustTitle.Text}"; scope |= StorageSearchScope.Title; }
                else if (sender == IllustAuthor)
                { text = fuzzy ? $"{IllustAuthor.Text}{Environment.NewLine}{Contents.UserID}" : $"=uid:{Contents.UserID}"; scope |= StorageSearchScope.Author; }
                else if (sender == IllustDate || sender == IllustDateInfo)
                { text = IllustDate.Text.Split().First(); scope |= StorageSearchScope.Date; }
                else if (sender == IllustSanityInfo)
                { text = $"R-{IllustSanity.Text}{Environment.NewLine}R{IllustSanity.Text}"; scope |= StorageSearchScope.Title | StorageSearchScope.Tag; }
                else if (sender is MenuItem)
                {
                    var mi = sender as MenuItem;
                    var miu = mi.GetUid();
                    if (!string.IsNullOrEmpty(miu) && miu.Equals("SearchTextInFiles", StringComparison.CurrentCultureIgnoreCase)) searching_web = false;
                    else if (!string.IsNullOrEmpty(miu) && miu.Equals("SearchTextInWeb", StringComparison.CurrentCultureIgnoreCase)) searching_web = true;

                    var host = mi.GetContextMenuHost();
                    if (host == IllustTagSpeech) { is_tag = true; text = IllustTagsHtml.GetText(); scope |= StorageSearchScope.Tag; }
                    else if (host == IllustDescSpeech) { text = fuzzy ? IllustDescHtml.GetText() : $"={IllustDescHtml.GetText()}"; scope |= StorageSearchScope.Description; }
                    else if (host == IllustAuthor) { text = fuzzy ? $"{IllustAuthor.Text}{Environment.NewLine}{Contents.UserID}" : $"=uid:{Contents.UserID}"; scope |= StorageSearchScope.Author; }
                    else if (host == IllustTitle) { text = fuzzy ? IllustTitle.Text : $"={IllustTitle.Text}"; scope |= StorageSearchScope.Title; }
                    else if (host == IllustDateInfo || host == IllustDate)
                    {
                        DateTime d = DateTime.Now;
                        DateTime.TryParse(IllustDate.Text, out d);
                        text = setting.SearchQueryUsingLongDate ? d.ToLongDateString() : d.ToShortDateString();
                        scope |= StorageSearchScope.Date;
                    }
                    else if (host == IllustSanityInfo) { text = $"R-{IllustSanity.Text}{Environment.NewLine}R{IllustSanity.Text}"; scope |= StorageSearchScope.Title | StorageSearchScope.Tag; }
                    else if (host == SubIllustsExpander || host == SubIllusts) { text = fuzzy ? $"={IllustTitle.Text}" : IllustTitle.Text; scope |= StorageSearchScope.Title; }
                    else if (host == RelatedItemsExpander || host == RelatedItems)
                    {
                        List<string> lines = new List<string>();
                        foreach (PixivItem item in RelatedItems.GetSelected())
                        {
                            lines.Add(fuzzy ? item.Illust.Title : $"={item.Illust.Title}");
                        }
                        text = string.Join($",{Environment.NewLine}", lines);
                        scope |= StorageSearchScope.Title;
                    }
                    else if (host == FavoriteItemsExpander || host == FavoriteItems)
                    {
                        List<string> lines = new List<string>();
                        foreach (PixivItem item in FavoriteItems.GetSelected())
                        {
                            lines.Add(fuzzy ? item.Illust.Title : $"={item.Illust.Title}");
                        }
                        text = string.Join($",{Environment.NewLine}", lines);
                        scope |= StorageSearchScope.Title;
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

            if (!string.IsNullOrEmpty(text))
            {
                var id = Contents.IsWork() && Contents.IsDownloaded ? Contents.ID : null;
                if (searching_web) Commands.SearchInWeb.Execute(new SearchObject(text, scope: scope, mode: mode, fuzzy: fuzzy, highlight: id));
                else Commands.SearchInStorage.Execute(new SearchObject(text, scope: scope, mode: mode, fuzzy: fuzzy, highlight: id));
            }
        }

        private void ActionSendToInstance_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Empty;
            try
            {
                if (sender is MenuItem)
                {
                    var mi = sender as MenuItem;
                    var host = mi.GetContextMenuHost();
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

        private async void ActionRefresh_Click(object sender, RoutedEventArgs e)
        {
            var text = string.Empty;
            try
            {
                if (sender is MenuItem)
                {
                    var mi = sender as MenuItem;
                    var host = mi.GetContextMenuHost();
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
                        var item = Contents;
                        if (item.IsWork())
                        {
                            var illust = Contents.ID.FindIllust();
                            if (illust.IsWork()) item = illust.WorkItem();
                        }
                        else if (item.IsUser())
                        {
                            var user = Contents.UserID.FindUser();
                            if (user is Pixeez.Objects.UserBase) item = user.UserItem();
                        }
                        if (Contents.HasUser()) UpdateDetail(Contents);
                    }
                    else if (mi.Uid.Equals("ActionRefreshIllust", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var item = Contents;
                        if (item.IsWork())
                        {
                            var illust = Contents.ID.FindIllust();
                            if (illust.IsWork()) item = illust.WorkItem();
                        }
                        else if (item.IsUser())
                        {
                            var user = Contents.UserID.FindUser();
                            if (user is Pixeez.Objects.UserBase) item = user.UserItem();
                        }
                        UpdateDetail(item);
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
                else if (sender == IllustTagRefresh)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                    {
                        RefreshHtmlRender(IllustTagsHtml);
                        UpdateIllustTitle();
                    }
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
                        var illust = Contents.ID.FindIllust();
                        if (illust.IsWork())
                        {
                            if (!illust.HasMetadata()) illust.Metadata = await illust.GetMetaData(null);
                            Contents = illust.WorkItem();
                            ShowIllustPagesAsync(Contents, force: true);
                        }
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.None)
                        SubIllusts.UpdateTilesImage(overwrite: false, touch: false);
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        SubIllusts.UpdateTilesImage(overwrite: true, touch: false);
                }
                else if (sender == RelatedRefresh)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        RelatedItems.UpdateTilesImage();
                    else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        RelatedItems.UpdateTilesImage(true);
                    else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                        RelatedItemsExpander_Expanded(sender, e);
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

        private int lastMouseDown = Environment.TickCount;
        private void IllustInfo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is UIElement) (sender as UIElement).Focus();

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
                else if (Keyboard.Modifiers == ModifierKeys.None && IsElement(btnSubPagePrev, e) && btnSubPagePrev.IsVisible && btnSubPagePrev.IsEnabled)
                    PrevIllustPage();
                else if (Keyboard.Modifiers == ModifierKeys.None && IsElement(btnSubPageNext, e) && btnSubPageNext.IsVisible && btnSubPageNext.IsEnabled)
                    NextIllustPage();
                else if (setting.EnabledMiniToolbar && Keyboard.Modifiers == ModifierKeys.None && PreviewPopup is Popup)
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
            try
            {
                if (Contents.HasPages())
                {
                    if ((Keyboard.Modifiers == ModifierKeys.Shift || e.XButton1 == MouseButtonState.Pressed) && e.LeftButton == MouseButtonState.Pressed)
                    {
                        (this.ParentWindow is Window ? this.ParentWindow : this as UIElement).AllowDrop = false;
                        e.Handled = true;
                        this.DragOut(PreviewImageUrl.GetImageCacheFile());
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.None && IsElement(btnSubPagePrev, e) && btnSubPagePrev.IsVisible && btnSubPagePrev.IsEnabled)
                    {
                        btnSubPagePrev.MinWidth = 48;
                        btnSubPageNext.MinWidth = 32;
                        e.Handled = true;
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.None && IsElement(btnSubPageNext, e) && btnSubPageNext.IsVisible && btnSubPageNext.IsEnabled)
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
                else if (Contents.IsWork())
                {
                    if (IsElement(PreviewRect, e) && (Keyboard.Modifiers == ModifierKeys.Shift || e.XButton1 == MouseButtonState.Pressed) && e.LeftButton == MouseButtonState.Pressed)
                    {
                        (this.ParentWindow is Window ? this.ParentWindow : this as UIElement).AllowDrop = false;
                        e.Handled = true;
                        this.DragOut(PreviewImageUrl.GetImageCacheFile());
                    }
                }
            }
            finally { (this.ParentWindow is Window ? this.ParentWindow : this as UIElement).AllowDrop = true; }
        }

        private void AuthorAvatar_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                e.Handled = true;

                var scope = StorageSearchScope.None | StorageSearchScope.Author;
                var mode = StorageSearchMode.Or; // Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? StorageSearchMode.And : StorageSearchMode.Or;
                var fuzzy = false; //!Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                var text = fuzzy ? $"{IllustAuthor.Text}{Environment.NewLine}{Contents.UserID}" : $"=uid:{Contents.UserID}";
                var id = Contents.IsWork() && Contents.IsDownloaded ? Contents.ID : null;

                text = string.Join(Environment.NewLine, text.Trim().Split(Speech.LineBreak, StringSplitOptions.RemoveEmptyEntries));
                if (!string.IsNullOrEmpty(text)) Commands.SearchInStorage.Execute(new SearchObject(text, scope: scope, mode: mode, fuzzy: fuzzy, highlight: id));
            }
        }

        private void IllustDownloaded_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
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

        private void UserFullListedFlag_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                Application.Current.SetUserFullListedState();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                Application.Current.RemoveUserFullListedState();
            }
        }

        private void IllustSizeInfo_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                (sender as UIElement).AllowDrop = false;
                var shift = Keyboard.Modifiers == ModifierKeys.Shift || sender == IllustSizeIcon;
                var df = IllustDownloaded.ToolTip as string;
                if (shift && e.LeftButton == MouseButtonState.Pressed && !string.IsNullOrEmpty(df))
                {
                    e.Handled = true;
                    this.DragOut(df);
                }
                else if (e.LeftButton == MouseButtonState.Pressed)
                {
                    e.Handled = true;
                    this.DragOut(PreviewImageUrl.GetImageCacheFile());
                }
            }
            finally { (sender as UIElement).AllowDrop = true; }
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
                try
                {
                    if (sender is MenuItem)
                    {
                        var host = (sender as MenuItem).GetContextMenuHost();
                        if (host is UIElement)
                        {
                            IList<PixivItem> items = new List<PixivItem>();
                            if (host == RelatedItems || host == RelatedItemsExpander) items = RelatedItems.GetSelected();
                            else if (host == FavoriteItems || host == FavoriteItemsExpander) items = FavoriteItems.GetSelected();

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

                try
                {
                    if (sender is MenuItem)
                    {
                        var host = (sender as MenuItem).GetContextMenuHost();
                        if (host is UIElement)
                        {
                            IList<PixivItem> items = new List<PixivItem>();
                            if (host == RelatedItems || host == RelatedItemsExpander) items = RelatedItems.GetSelected();
                            else if (host == FavoriteItems || host == FavoriteItemsExpander) items = FavoriteItems.GetSelected();

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
                    }
                }
                catch (Exception ex) { ex.ERROR("FOLLOW"); }
            }
            else if (uid.Equals("ActionFollowAuthorPublic", StringComparison.CurrentCultureIgnoreCase) ||
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
                    SubIllusts.UpdateTilesImage(touch: false);
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
                        //UpdateIllustStateInfo();
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
                    page_number = Contents.Index / PAGE_ITEMS;
                    //page_number = Contents.Index % PAGE_ITEMS;
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

        private void ActionSaveIllust_Click(object sender, RoutedEventArgs e)
        {
            setting = Application.Current.LoadSetting();
            var uid = sender.GetUid();
            var type = DownloadType.None;
            //var cq = (int)(ReduceToQuality is Slider ? ReduceToQuality.Value : setting.DownloadRecudeJpegQuality);
            var cq = sender is MenuItem && (sender as MenuItem).Tag is App.MenuItemSliderData ? ((sender as MenuItem).Tag as App.MenuItemSliderData).Value : setting.DownloadRecudeJpegQuality;

            if (Keyboard.Modifiers == ModifierKeys.Shift) type |= DownloadType.ConvertKeepName;
            if (sender == PreviewSave) type |= DownloadType.None;
            else if (uid.Equals("ActionSaveIllust")) type |= DownloadType.Original;
            else if (uid.Equals("ActionSaveIllustJpeg")) type |= DownloadType.AsJPEG;
            else if (uid.Equals("ActionReduceJpegSizeTo")) type |= DownloadType.AsJPEG;
            else if (uid.Equals("ActionSaveIllustPreview")) type |= DownloadType.UseLargePreview;

            if (SubIllusts.SelectedItems != null && SubIllusts.SelectedItems.Count > 0)
            {
                var items = new KeyValuePair<ImageListGrid, DownloadType>(SubIllusts, type);
                if (uid.Equals("ActionConvertIllustJpeg"))
                    Commands.ConvertToJpeg.Execute(items);
                else if (uid.Equals("ActionReduceIllustJpeg"))
                    Commands.ReduceJpeg.Execute(items);
                else if (uid.Equals("ActionReduceJpegSizeTo"))
                    Commands.ReduceJpeg.Execute(new Tuple<ImageListGrid, DownloadType, int>(SubIllusts, type, cq));
                else
                    Commands.SaveIllust.Execute(items);
            }
            else if (SubIllusts.SelectedItem.IsWork())
            {
                var item = new KeyValuePair<PixivItem, DownloadType>(SubIllusts.SelectedItem, type);
                if (uid.Equals("ActionConvertIllustJpeg"))
                    Commands.ConvertToJpeg.Execute(item);
                else if (uid.Equals("ActionReduceIllustJpeg"))
                    Commands.ReduceJpeg.Execute(item);
                else if (uid.Equals("ActionReduceJpegSizeTo"))
                    Commands.ReduceJpeg.Execute(new Tuple<PixivItem, DownloadType, int>(SubIllusts.SelectedItem, type, cq));
                else
                    Commands.SaveIllust.Execute(item);
            }
            else if (Contents.IsWork())
            {
                var item = new KeyValuePair<PixivItem, DownloadType>(Contents, type);
                if (uid.Equals("ActionConvertIllustJpeg"))
                    Commands.ConvertToJpeg.Execute(item);
                else if (uid.Equals("ActionReduceIllustJpeg"))
                    Commands.ReduceJpeg.Execute(item);
                else if (uid.Equals("ActionReduceJpegSizeTo"))
                    Commands.ReduceJpeg.Execute(new Tuple<PixivItem, DownloadType, int>(Contents, type, cq));
                else
                    Commands.SaveIllust.Execute(item);
            }
            UpdateIllustStateInfo(querysize: false);
        }

        private void ActionSaveAllIllust_Click(object sender, RoutedEventArgs e)
        {
            var uid = sender.GetUid();
            var type = DownloadType.None;

            if (Keyboard.Modifiers == ModifierKeys.Shift) type |= DownloadType.ConvertKeepName;
            if (uid.Equals("ActionSaveIllustAll")) type |= DownloadType.Original;
            else if (uid.Equals("ActionSaveIllustJpegAll")) type |= DownloadType.AsJPEG;
            else if (uid.Equals("ActionSaveIllustPreviewAll")) type |= DownloadType.UseLargePreview;

            if (Contents.IsWork() && Contents.Count > 0)
            {
                var item = new KeyValuePair<PixivItem, DownloadType>(Contents.Illust.WorkItem(), type);
                if (uid.Equals("ActionConvertIllustJpegAll"))
                    Commands.ConvertToJpeg.Execute(item);
                else if (uid.Equals("ActionReduceIllustJpegAll"))
                    Commands.ReduceJpeg.Execute(item);
                else
                    Commands.SaveIllustAll.Execute(item);
            }
            UpdateIllustStateInfo(querysize: false);
        }
        #endregion

        #region Related Panel related routines
        private void RelatedItemsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Contents.HasUser())
            {
                var append_all = Keyboard.Modifiers == ModifierKeys.Control;
                var shift = Keyboard.Modifiers == ModifierKeys.Shift;
                if (shift && RelatedItems.Items.Count > 0) return;

                CancelRelatedLoading();

                if (Contents.IsWork())
                    ShowRelatedInlineAsync(Contents);
                else if (Contents.IsUser())
                    ShowUserWorksInlineAsync(Contents.User, append: append_all, append_all: append_all);
            }
        }

        private void RelatedItemsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            IllustDetailWait.Hide();
        }

        private void RelatedItemsExpander_MouseDown(object sender, MouseButtonEventArgs e)
        {
            RelatedItems.Focusable = true;
            RelatedItems.Focus();
            Keyboard.Focus(RelatedItems);
            var item = RelatedItems.SelectedItems.FirstOrDefault();
            if (item != null)
            {
                item.Focusable = true;
                item.Focus();
                Keyboard.Focus(item);
            }
        }

        private void RelatedNextAppend_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            RelatedNextAppend.ToolTip = RelatedItemsExpander.ToolTip;
            //RelatedNextAppend.ToolTip = RelatedItems.ToolTip;
        }

        private void ActionOpenRelated_Click(object sender, RoutedEventArgs e)
        {
            Commands.Open.Execute(RelatedItems);
        }

        private void ActionCopyRelatedIllustID_Click(object sender, RoutedEventArgs e)
        {
            Commands.CopyArtworkIDs.Execute(RelatedItems);
        }

        private void RelatedItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            e.Handled = false;
            RelatedItems.UpdateTilesState();
            //UpdateLikeState();
            if (RelatedItems.SelectedItem.IsWork())
            {
                int id = -1;
                int.TryParse(RelatedItems.SelectedItem.ID, out id);
                false.UpdateLikeStateAsync(id);
                RelatedItems.SelectedItem.Focus();
            }
            e.Handled = true;
        }

        private void RelatedItems_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void RelatedItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed) Commands.OpenWork.Execute(RelatedItems);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        private void RelatedItems_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Commands.Open.Execute(RelatedItems);
            }
        }

        private void RelatedPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (Contents.HasUser())
            {
                var prev_url = RelatedPrevPage.Tag is string ? RelatedPrevPage.Tag as string : string.Empty;

                if (Contents.IsWork())
                    ShowRelatedInlineAsync(Contents, prev_url);
                else if (Contents.IsUser())
                    ShowUserWorksInlineAsync(Contents.User, prev_url);
            }
        }

        private void RelatedNextPage_Click(object sender, RoutedEventArgs e)
        {
            if (Contents.HasUser())
            {
                CancelRelatedLoading();

                var append = sender == RelatedNextAppend ? true : false;
                var append_all = Keyboard.Modifiers == ModifierKeys.Control;
                var next_url = RelatedNextPage.Tag is string ? RelatedNextPage.Tag as string : string.Empty;

                if (Contents.IsWork())
                    ShowRelatedInlineAsync(Contents, next_url, append);
                else if (Contents.IsUser())
                    ShowUserWorksInlineAsync(Contents.User, next_url, append, append_all);
            }
        }

        private void RelatedNextAppend_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            CancelRelatedLoading();
        }
        #endregion

        #region Author Favorite routines
        private void FavoriteItemsExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (Contents.HasUser())
            {
                var shift = Keyboard.Modifiers == ModifierKeys.Shift;
                if (shift && FavoriteItems.Items.Count > 0) return;

                ShowFavoriteInlineAsync(Contents.User);
            }
        }

        private void FavoriteItemsExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            FavoriteItemsExpander.Header = "Favorite";
            IllustDetailWait.Hide();
        }

        private void FavoriteItemsExpander_MouseDown(object sender, MouseButtonEventArgs e)
        {
            FavoriteItems.Focusable = true;
            FavoriteItems.Focus();
            Keyboard.Focus(FavoriteItems);
            var item = FavoriteItems.SelectedItems.FirstOrDefault();
            if (item != null)
            {
                item.Focusable = true;
                item.Focus();
                Keyboard.Focus(item);
            }
        }

        private void FavoriteNextAppend_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            FavoriteNextAppend.ToolTip = FavoriteItemsExpander.ToolTip;
            //FavoriteNextAppend.ToolTip = FavoriteItems.ToolTip;
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
                var append_all = Keyboard.Modifiers == ModifierKeys.Control;
                var next_url = FavoriteNextPage.Tag is string ? FavoriteNextPage.Tag as string : string.Empty;
                ShowFavoriteInlineAsync(Contents.User, next_url, append, append_all);
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
                    var items = (sender as ContextMenu).FindChildren<UIElement>();
                    foreach (dynamic item in items)
                    //foreach (dynamic item in (sender as ContextMenu).Items)
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
                            else if (item.Uid.Equals("ActionSaveIllustsJpeg", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save Selected Pages As JPEG";
                            else if (item.Uid.Equals("ActionSaveIllustsJpegAll", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save All Pages As JPEG";
                            else if (item.Uid.Equals("ActionSaveIllustsPreview", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save Selected Pages Large Preview";
                            else if (item.Uid.Equals("ActionSaveIllustsPreviewAll", StringComparison.CurrentCultureIgnoreCase))
                                item.Header = "Save All Pages Large Preview";

                            if (item.Uid.Equals("ActionReduceIllustsJpegSizeTo"))
                            {
                                if (item is MenuItem && (item as MenuItem).Tag == null)
                                {
                                    (item as MenuItem).Tag = Application.Current.GetDefaultReduceData();
                                }
                            }
                        }
                        catch (Exception ex) { ex.ERROR(); continue; }
                    }
                }
                else if (host == RelatedItemsExpander || host == RelatedItems || host == FavoriteItemsExpander || host == FavoriteItems)
                {
                    var target = host == RelatedItemsExpander || host == RelatedItems ? RelatedItemsExpander : FavoriteItemsExpander;
                    var items = (sender as ContextMenu).FindChildren<UIElement>();
                    foreach (dynamic item in items)
                    //foreach (dynamic item in (sender as ContextMenu).Items)
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

                            if (item.Uid.Equals("ActionReduceIllustsJpegSizeTo"))
                            {
                                if (item is MenuItem && (item as MenuItem).Tag == null)
                                {
                                    (item as MenuItem).Tag = Application.Current.GetDefaultReduceData();
                                }
                            }
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
                if (sender is MenuItem)
                {
                    var host = (sender as MenuItem).GetContextMenuHost();
                    if (host == SubIllustsExpander || host == SubIllusts)
                    {
                        if (Contents.IsWork())
                        {
                            Commands.CopyArtworkIDs.Execute(Contents);
                        }
                    }
                    else if (host == RelatedItemsExpander || host == RelatedItems)
                    {
                        Commands.CopyArtworkIDs.Execute(RelatedItems);
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
                var host = (sender as MenuItem).GetContextMenuHost();
                if (host == SubIllustsExpander || host == SubIllusts || host == PreviewBox)
                    target = SubIllusts;
                else if (host == RelatedItemsExpander || host == RelatedItems)
                    target = RelatedItems;
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

        private void ActionCopyIllustInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem)
                {
                    var host = (sender as MenuItem).GetContextMenuHost();
                    if (host == SubIllustsExpander || host == SubIllusts)
                    {
                        if (Contents.IsWork()) Commands.CopyJson.Execute(Contents);
                    }
                    else if (host == RelatedItemsExpander || host == RelatedItems)
                    {
                        Commands.CopyJson.Execute(RelatedItems);
                    }
                    else if (host == FavoriteItemsExpander || host == FavoriteItems)
                    {
                        Commands.CopyJson.Execute(FavoriteItems);
                    }
                    else if (host == CommentsExpander || host == IllustCommentsHost)
                    {

                    }
                }
            }
            catch (Exception ex) { ex.ERROR("ActionCopyIllustInfo"); }
        }

        private void ActionOpenSelected_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem)
                {
                    var host = (sender as MenuItem).GetContextMenuHost();
                    if (host == SubIllustsExpander || host == SubIllusts || host == PreviewBox)
                    {
                        Commands.OpenWorkPreview.Execute(SubIllusts);
                    }
                    else if (host == RelatedItemsExpander || host == RelatedItems)
                    {
                        Commands.OpenWork.Execute(RelatedItems);
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
                if (sender is MenuItem)
                {
                    var host = (sender as MenuItem).GetContextMenuHost();
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
                    else if (host == RelatedItemsExpander || host == RelatedItems)
                    {
                        if (uid.Equals("ActionSendIllustToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (Keyboard.Modifiers == ModifierKeys.None)
                                Commands.SendToOtherInstance.Execute(RelatedItems);
                            else
                                Commands.ShellSendToOtherInstance.Execute(RelatedItems);
                        }
                        else if (uid.Equals("ActionSendAuthorToInstance", StringComparison.CurrentCultureIgnoreCase))
                        {
                            var ids = new List<string>();
                            foreach (var item in RelatedItems.GetSelected())
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

        private void ActionCompare_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var shift = Keyboard.Modifiers == ModifierKeys.Shift;
                var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
                if (sender is MenuItem)
                {
                    var mi = sender as MenuItem;
                    var host = mi.GetContextMenuHost();
                    if (mi.Uid.Equals("ActionCompare", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            Commands.Compare.Execute(SubIllusts);
                        }
                        else if (host == RelatedItemsExpander || host == RelatedItems)
                        {
                            Commands.Compare.Execute(RelatedItems);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            Commands.Compare.Execute(FavoriteItems);
                        }
                        else if (Contents.IsWork())
                        {
                            if (Contents.Count > 1) Commands.Compare.Execute(SubIllusts);
                            else Commands.Compare.Execute(Contents);
                        }
                    }
                    else if (mi.Uid.Equals("ActionCompareDownloaded", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts || Contents.IsWork())
                        {
                            if ((Contents.Count <= 1 || SubIllusts.SelectedItems.Count == 0) && !string.IsNullOrEmpty(Contents.DownloadedFilePath))
                                Commands.Compare.Execute(Contents.DownloadedFilePath);
                            else
                            {
                                var items = new List<string>();
                                foreach (var item in SubIllusts.GetSelected()) items.AddRange(item.GetDownloadedFiles());
                                if (items.Count > 0) Commands.Compare.Execute(items.Take(2));
                            }
                        }
                        else if (host == RelatedItemsExpander || host == RelatedItems)
                        {
                            //var items = RelatedItems.GetSelected().Where(i => !string.IsNullOrEmpty(i.DownloadedFilePath)).Select(i => i.DownloadedFilePath).ToList();
                            var items = RelatedItems.GetSelected().Select(i => i.GetDownloadedFiles().FirstOrDefault()).ToList();
                            if (items.Count > 0) Commands.Compare.Execute(items);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            //var items = FavoriteItems.GetSelected().Where(i => !string.IsNullOrEmpty(i.DownloadedFilePath)).Select(i => i.DownloadedFilePath).ToList();
                            var items = FavoriteItems.GetSelected().Select(i => i.GetDownloadedFiles().FirstOrDefault()).ToList();
                            if (items.Count > 0) Commands.Compare.Execute(items);
                        }
                    }
                }
                else if (sender == SubIllustCompare)
                {
                    if (shift || ctrl)
                    {
                        var items = new List<string>();
                        foreach (var item in SubIllusts.GetSelected()) items.AddRange(item.GetDownloadedFiles());
                        if (items.Count > 0) Commands.Compare.Execute(items.Take(2));
                    }
                    else Commands.Compare.Execute(SubIllusts);
                }
                else if (sender == RelatedCompare)
                {
                    if (shift || ctrl)
                    {
                        var items = RelatedItems.GetSelected().Select(i => i.GetDownloadedFiles().FirstOrDefault()).ToList();
                        if (items.Count > 0) Commands.Compare.Execute(items);
                    }
                    else Commands.Compare.Execute(RelatedItems);
                }
                else if (sender == FavoriteCompare)
                {
                    if (shift || ctrl)
                    {
                        var items = FavoriteItems.GetSelected().Select(i => i.GetDownloadedFiles().FirstOrDefault()).ToList();
                        if (items.Count > 0) Commands.Compare.Execute(items);
                    }
                    else Commands.Compare.Execute(FavoriteItems);
                }
                else if (sender == PreviewCompare)
                {
                    if (Contents.Count > 1) Commands.Compare.Execute(SubIllusts);
                    else Commands.Compare.Execute(Contents);
                }
            }
            catch (Exception ex) { ex.ERROR("Compare"); }
        }

        private void ActionPrevPage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem)
                {
                    var host = (sender as MenuItem).GetContextMenuHost();
                    if (host == SubIllustsExpander || host == SubIllusts)
                    {
                        SubIllustPagesNav_Click(SubIllustPrevPages, e);
                    }
                    else if (host == RelatedItemsExpander || host == RelatedItems)
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
                if (sender is MenuItem)
                {
                    var mi = sender as MenuItem;
                    var append = mi.GetUid().Equals("ActionNextAppend", StringComparison.CurrentCultureIgnoreCase) ? true : false;
                    var host = mi.GetContextMenuHost();
                    if (host == SubIllustsExpander || host == SubIllusts)
                    {
                        SubIllustPagesNav_Click(SubIllustNextPages, e);
                    }
                    else if (host == RelatedItemsExpander || host == RelatedItems)
                    {
                        RelatedNextPage_Click(append ? RelatedNextAppend : RelatedNextPage, e);
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
                    var uid = sender.GetUid();
                    var type = DownloadType.None;
                    var cq = sender is MenuItem && (sender as MenuItem).Tag is App.MenuItemSliderData ? ((sender as MenuItem).Tag as App.MenuItemSliderData).Value : setting.DownloadRecudeJpegQuality;

                    if (Keyboard.Modifiers == ModifierKeys.Shift) type |= DownloadType.ConvertKeepName;
                    if (uid.Equals("ActionSaveIllusts") || uid.Equals("ActionSaveIllustsAll")) type |= DownloadType.Original;
                    else if (uid.Equals("ActionSaveIllustsJpeg") || uid.Equals("ActionSaveIllustsJpegAll")) type |= DownloadType.AsJPEG;
                    else if (uid.Equals("ActionSaveIllustsPreview")) type |= DownloadType.UseLargePreview;

                    var mi = sender as MenuItem;
                    var host = mi.GetContextMenuHost();
                    if (mi.Uid.Equals("ActionSaveIllusts", StringComparison.CurrentCultureIgnoreCase) ||
                        mi.Uid.Equals("ActionSaveIllustsJpeg", StringComparison.CurrentCultureIgnoreCase) ||
                        mi.Uid.Equals("ActionSaveIllustsPreview", StringComparison.CurrentCultureIgnoreCase) ||
                        mi.Uid.Equals("ActionConvertIllustsJpeg", StringComparison.CurrentCultureIgnoreCase) ||
                        mi.Uid.Equals("ActionReduceIllustsJpeg", StringComparison.CurrentCultureIgnoreCase) ||
                        mi.Uid.Equals("ActionReduceIllustsJpegSizeTo", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            var items = new KeyValuePair<ImageListGrid, DownloadType>(SubIllusts, type);
                            if (uid.Equals("ActionConvertIllustsJpeg"))
                                Commands.ConvertToJpeg.Execute(items);
                            else if (uid.Equals("ActionReduceIllustsJpeg"))
                                Commands.ReduceJpeg.Execute(items);
                            else if (uid.Equals("ActionReduceIllustsJpegSizeTo"))
                                Commands.ReduceJpeg.Execute(new Tuple<ImageListGrid, DownloadType, int>(SubIllusts, type, cq));
                            else
                                Commands.SaveIllust.Execute(items);
                        }
                        else if (host == RelatedItemsExpander || host == RelatedItems)
                        {
                            var items = new KeyValuePair<ImageListGrid, DownloadType>(RelatedItems, type);
                            if (uid.Equals("ActionConvertIllustsJpeg"))
                                Commands.ConvertToJpeg.Execute(items);
                            else if (uid.Equals("ActionReduceIllustsJpeg"))
                                Commands.ReduceJpeg.Execute(items);
                            else if (uid.Equals("ActionReduceIllustsJpegSizeTo"))
                                Commands.ReduceJpeg.Execute(new Tuple<ImageListGrid, DownloadType, int>(RelatedItems, type, cq));
                            else
                                Commands.SaveIllust.Execute(items);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            var items = new KeyValuePair<ImageListGrid, DownloadType>(FavoriteItems, type);
                            if (uid.Equals("ActionConvertIllustsJpeg"))
                                Commands.ConvertToJpeg.Execute(items);
                            else if (uid.Equals("ActionReduceIllustsJpeg"))
                                Commands.ReduceJpeg.Execute(items);
                            else if (uid.Equals("ActionReduceIllustsJpegSizeTo"))
                                Commands.ReduceJpeg.Execute(new Tuple<ImageListGrid, DownloadType, int>(FavoriteItems, type, cq));
                            else
                                Commands.SaveIllust.Execute(items);
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("SaveIllusts"); }
        }

        private void ActionSaveIllustsAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem)
                {
                    var uid = sender.GetUid();
                    var type = DownloadType.None;

                    if (Keyboard.Modifiers == ModifierKeys.Shift) type |= DownloadType.ConvertKeepName;
                    if (uid.Equals("ActionSaveIllustAll")) type |= DownloadType.Original;
                    else if (uid.Equals("ActionSaveIllustsJpegAll")) type |= DownloadType.AsJPEG;
                    else if (uid.Equals("ActionSaveIllustsPreviewAll")) type |= DownloadType.UseLargePreview;

                    var mi = sender as MenuItem;
                    var host = mi.GetContextMenuHost();
                    if (mi.Uid.Equals("ActionSaveIllustsAll", StringComparison.CurrentCultureIgnoreCase) ||
                        mi.Uid.Equals("ActionSaveIllustsJpegAll", StringComparison.CurrentCultureIgnoreCase) ||
                        mi.Uid.Equals("ActionSaveIllustsPreviewAll", StringComparison.CurrentCultureIgnoreCase) ||
                        mi.Uid.Equals("ActionConvertIllustsJpegAll", StringComparison.CurrentCultureIgnoreCase) ||
                        mi.Uid.Equals("ActionReduceIllustsJpegAll", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            var items = new KeyValuePair<PixivItem, DownloadType>(Contents.Illust.WorkItem(), type);
                            if (uid.Equals("ActionConvertIllustsJpegAll"))
                                Commands.ConvertToJpeg.Execute(items);
                            else if (uid.Equals("ActionReduceIllustsJpegAll"))
                                Commands.ReduceJpeg.Execute(items);
                            else
                                Commands.SaveIllustAll.Execute(items);
                        }
                        else if (host == RelatedItemsExpander || host == RelatedItems)
                        {
                            var items = new KeyValuePair<ImageListGrid, DownloadType>(RelatedItems, type);
                            if (uid.Equals("ActionConvertIllustsJpegAll"))
                                Commands.ConvertToJpeg.Execute(items);
                            else if (uid.Equals("ActionReduceIllustsJpegAll"))
                                Commands.ReduceJpeg.Execute(items);
                            else
                                Commands.SaveIllustAll.Execute(items);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            var items = new KeyValuePair<ImageListGrid, DownloadType>(FavoriteItems, type);
                            if (uid.Equals("ActionConvertIllustsJpegAll"))
                                Commands.ConvertToJpeg.Execute(items);
                            else if (uid.Equals("ActionReduceIllustsJpegAll"))
                                Commands.ReduceJpeg.Execute(items);
                            else
                                Commands.SaveIllustAll.Execute(items);
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("SaveIllustsAll"); }
        }

        private void ActionUgoiraGet_Click(object sender, RoutedEventArgs e)
        {
            if (Contents.IsUgoira() && sender is MenuItem)
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
                    var shift = Keyboard.Modifiers == ModifierKeys.Shift;
                    var mi = sender as MenuItem;
                    var host = mi.GetContextMenuHost();
                    if (mi.Uid.Equals("ActionOpenDownloaded", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            if (shift)
                                Commands.CopyDownloadedPath.Execute(SubIllusts);
                            else
                                Commands.OpenDownloaded.Execute(SubIllusts);
                        }
                        else if (host == RelatedItemsExpander || host == RelatedItems)
                        {
                            if (shift)
                                Commands.CopyDownloadedPath.Execute(RelatedItems);
                            else
                                Commands.OpenDownloaded.Execute(RelatedItems);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            if (shift)
                                Commands.CopyDownloadedPath.Execute(FavoriteItems);
                            else
                                Commands.OpenDownloaded.Execute(FavoriteItems);
                        }
                    }
                    else if (mi.Uid.Equals("ActionOpenDownloadedProperties", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            Commands.OpenFileProperties.Execute(SubIllusts);
                        }
                        else if (host == RelatedItemsExpander || host == RelatedItems)
                        {
                            Commands.OpenFileProperties.Execute(RelatedItems);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            Commands.OpenFileProperties.Execute(FavoriteItems);
                        }
                    }
                    else if (mi.Uid.Equals("ActionShowDownloadedMeta", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            Commands.ShowMeta.Execute(SubIllusts);
                        }
                        else if (host == RelatedItemsExpander || host == RelatedItems)
                        {
                            Commands.ShowMeta.Execute(RelatedItems);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            Commands.ShowMeta.Execute(FavoriteItems);
                        }
                    }
                    else if (mi.Uid.Equals("ActionTouchDownloadedMeta", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            Commands.TouchMeta.Execute(SubIllusts);
                        }
                        else if (host == RelatedItemsExpander || host == RelatedItems)
                        {
                            Commands.TouchMeta.Execute(RelatedItems);
                        }
                        else if (host == FavoriteItemsExpander || host == FavoriteItems)
                        {
                            Commands.TouchMeta.Execute(FavoriteItems);
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
                    var mi = sender as MenuItem;
                    var append = mi.Uid.Equals("ActionNextAppend", StringComparison.CurrentCultureIgnoreCase) ? true : false;
                    var host = mi.GetContextMenuHost();
                    if (mi.Uid.Equals("ActionRefresh", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            if (Contents.IsWork())
                            {
                                ShowIllustPagesAsync(Contents, page_index, page_number);
                            }
                        }
                        else if (host == RelatedItemsExpander || host == RelatedItems)
                        {
                            if (Contents.HasUser())
                            {
                                var current_url = RelatedItemsExpander.Tag is string ? RelatedItemsExpander.Tag as string : string.Empty;
                                if (Contents.IsWork())
                                    ShowRelatedInlineAsync(Contents, current_url);
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
                    else if (mi.Uid.Equals("ActionRefreshThumb", StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (host == SubIllustsExpander || host == SubIllusts)
                        {
                            SubIllusts.UpdateTilesImage(touch: false);
                        }
                        else if (host == RelatedItemsExpander || host == RelatedItems)
                        {
                            RelatedItems.UpdateTilesImage();
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
