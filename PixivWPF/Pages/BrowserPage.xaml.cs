using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using System.Net;
using System.Reflection;

namespace PixivWPF.Pages
{
    /// <summary>
    /// WebviewPage.xaml 的交互逻辑
    /// </summary>
    public partial class BrowerPage : Page
    {
        private bool bCancel = false;
        private WindowsFormsHostEx webHost;
        private WebBrowserEx webHtml;
        private Uri currentUri = null;
        private string titleWord = string.Empty;

        private const int HTTP_STREAM_READ_COUNT = 65536;
        private Setting setting = Application.Current.LoadSetting();

        public string Contents { get; set; } = string.Empty;

        public void ReadText()
        {
            try
            {
                var text = webHtml.GetText();
                text = string.Join(Environment.NewLine, text.Trim().Split(Speech.LineBreak, StringSplitOptions.RemoveEmptyEntries));
                if (!string.IsNullOrEmpty(text)) text.Play();
            }
            catch (Exception) { }
        }

        internal void UpdateTheme()
        {
            if (webHtml is System.Windows.Forms.WebBrowser)
            {
            }
        }

        private void InitHtmlRenderHost(out WindowsFormsHostEx host, WebBrowserEx browser, Panel panel)
        {
            host = new WindowsFormsHostEx()
            {
                //IsRedirected = true,
                //CompositionMode = ,
                AllowDrop = false,
                Background = new SolidColorBrush(Colors.Transparent),
                MinHeight = 24,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = browser
            };
            if(panel is Panel) panel.Children.Add(host);
        }

        private void InitHtmlRender(out WebBrowserEx browser)
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

            try
            {
                if (browser is System.Windows.Forms.WebBrowser)
                {
                    browser.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(WebBrowser_DocumentCompleted);
                    browser.Navigating += new System.Windows.Forms.WebBrowserNavigatingEventHandler(WebBrowser_Navigating);
                    browser.ProgressChanged += new System.Windows.Forms.WebBrowserProgressChangedEventHandler(WebBrowser_ProgressChanged);
                    browser.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(WebBrowser_PreviewKeyDown);
                    TrySetSuppressScriptErrors(webHtml, true);
                }
            }
            catch (Exception) { }
        }

        private void CreateHtmlRender()
        {
            InitHtmlRender(out webHtml);
            InitHtmlRenderHost(out webHost, webHtml, BrowserHost);

            UpdateTheme();
            this.UpdateLayout();
        }

        private void DeleteHtmlRender()
        {
            try
            {
                if (webHtml is System.Windows.Forms.WebBrowser) webHtml.Dispose(true);
            }
            catch { }
            try
            {
                if (webHost is WindowsFormsHostEx) webHost.Dispose();
            }
            catch { }
        }

        private bool TrySetSuppressScriptErrors(System.Windows.Forms.WebBrowser webBrowser, bool value)
        {
            try
            {
                FieldInfo field = typeof(System.Windows.Forms.WebBrowser).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    object axIWebBrowser2 = field.GetValue(webBrowser);
                    if (axIWebBrowser2 != null)
                    {
                        axIWebBrowser2.GetType().InvokeMember("Silent", BindingFlags.SetProperty, null, axIWebBrowser2, new object[] { value });
                        return true;
                    }
                }
            }
            catch (Exception) { }
            return false;
        }

        private bool IsSkip(string url)
        {
            if (string.IsNullOrEmpty(url)) return (true);

            var result = false;
            var ul = url.ToLower();
            if (ul.Contains("/plugins/like.php")) result = true;
            else if (ul.Contains("/dnserrordiagoff.htm")) result = true;
            else if (ul.Contains("/embed/")) result = true;
            return (result);
        }

        private async void GetHtmlContents(string url)
        {
            await new Action(() =>
            {
                if (IsSkip(url)) return;

                currentUri = new Uri(currentUri, url);
                GetHtmlContents(currentUri);
            }).InvokeAsync();
        }

        private async void GetHtmlContents(Uri url)
        {
            if (IsSkip(url.AbsolutePath)) return;
            setting = Application.Current.LoadSetting();

            await new Action(async () =>
            {
                try
                {
                    BrowserWait.Show();

                    webHtml.Stop();
                    HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(currentUri);
                    if (setting.UsingProxy) myRequest.Proxy = new WebProxy(setting.Proxy, true, setting.ProxyBypass);
                    myRequest.Timeout = setting.DownloadHttpTimeout;
                    myRequest.KeepAlive = true;

                    HttpWebResponse myResponse = (HttpWebResponse)await myRequest.GetResponseAsync();
                    webHtml.DocumentStream = myResponse.GetResponseStream();
                }
                catch (Exception ex)
                {
                    if (webHtml.DocumentStream is Stream) webHtml.DocumentStream.Close();
                    if (ex.Message.Contains("404"))
                    {
                        if (webHtml.DocumentText.Length <= 1024)
                            webHtml.DocumentText = $"<p class='E404' alt='404 Not Found!'><span class='E404T'>{titleWord}</span></p>".GetHtmlFromTemplate(titleWord);
                        BrowserWait.Hide();
                    }
                    else
                    {
                        ex.Message.ShowToast("ERROR[BROWSER]!");
                        BrowserWait.Fail();
                    }
                }
            }).InvokeAsync();
        }

        internal async void UpdateDetail(string content)
        {
            if (webHtml is System.Windows.Forms.WebBrowser)
            {
                titleWord = content;
                await new Action(() =>
                {
                    if(content.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                        currentUri = new Uri(Uri.EscapeUriString(content.Replace("http://", "https://")));
                    else
                        currentUri = new Uri(Uri.EscapeUriString($"https://dic.pixiv.net/a/{content}/"));
                    GetHtmlContents(currentUri);
                }).InvokeAsync();
            }
        }
        
        private async void WebBrowserReplaceImageSource(System.Windows.Forms.WebBrowser browser)
        {
            try
            {
                if (browser is System.Windows.Forms.WebBrowser && browser.Document != null)
                {
                    var no_img = new Uri(System.IO.Path.Combine(Application.Current.GetRoot(), "no_image.png")).AbsoluteUri;
                    foreach (System.Windows.Forms.HtmlElement imgElemt in browser.Document.Images)
                    {
                        try
                        {
                            var src = imgElemt.GetAttribute("src");
                            if (string.IsNullOrEmpty(src) || src.ToLower().Contains("no_image_p.svg"))
                                imgElemt.SetAttribute("src", no_img);
                            else
                            {
                                await new Action(async () =>
                                {
                                    try
                                    {
                                        if (src.IsPixivImage())
                                        {
                                            var img = await src.LoadImageFromUrl();
                                            if (!string.IsNullOrEmpty(img.SourcePath) && !string.IsNullOrEmpty(img.SourcePath))
                                                imgElemt.SetAttribute("src", new Uri(img.SourcePath).AbsoluteUri);
                                        }
                                    }
                                    catch (Exception) { }
                                }).InvokeAsync();
                            }
                        }
#if DEBUG
                            catch (Exception ex)
                            {
                                ex.Message.DEBUG();
                                continue;
                            }
#else
                        catch (Exception) { continue; }
#endif
                    }
                }
            }
            catch (Exception) { }
        }

        private async void WebBrowser_LinkClick(object sender, System.Windows.Forms.HtmlElementEventArgs e)
        {
            bCancel = true;
            try
            {
                if (e.EventType.Equals("click", StringComparison.CurrentCultureIgnoreCase))
                {
                    e.BubbleEvent = false;
                    e.ReturnValue = false;

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
                                        Commands.OpenUser.Execute(user);
                                    }).InvokeAsync();
                                }
                                else
                                {
                                    user = await user_id.RefreshUser();
                                    if (user is Pixeez.Objects.User)
                                    {
                                        Commands.OpenUser.Execute(user);
                                    }
                                }
                            }
                            else if (href_lower.StartsWith("http", StringComparison.CurrentCultureIgnoreCase) && href_lower.Contains("dic.pixiv.net/"))
                            {
                                await new Action(() =>
                                {
                                    Commands.OpenPedia.Execute(href);
                                }).InvokeAsync();
                                //GetHtmlContents(href);
                            }
                            else if (href_lower.StartsWith("about:/a", StringComparison.CurrentCultureIgnoreCase))
                            {
                                href = href.Replace("about:/a", "https://dic.pixiv.net/a");
                                await new Action(() =>
                                {
                                    Commands.OpenPedia.Execute(href);
                                }).InvokeAsync();
                                //GetHtmlContents(href);
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
                            e.ReturnValue = false;
                        }
                    }
                    else
                    {
                        if (!e.AltKeyPressed && !e.CtrlKeyPressed && !e.ShiftKeyPressed)
                            Commands.OpenSearch.Execute($"Fuzzy Tag:{tag}");
                        else if (e.AltKeyPressed && !e.CtrlKeyPressed && !e.ShiftKeyPressed)
                            Commands.OpenSearch.Execute($"Tag:{tag}");
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

        private void WebBrowser_ProgressChanged(object sender, System.Windows.Forms.WebBrowserProgressChangedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Forms.WebBrowser)
                {
                    var browser = sender as System.Windows.Forms.WebBrowser;
                    WebBrowserReplaceImageSource(browser);
                }
            }
            catch (Exception) { }
        }

        private async void WebBrowser_Navigating(object sender, System.Windows.Forms.WebBrowserNavigatingEventArgs e)
        {
            try
            {
                if (bCancel == true)
                {
                    e.Cancel = true;
                    bCancel = false;
                }
                else
                {
                    if (e.Url.AbsolutePath != "blank")
                    {
                        if (IsSkip(e.Url.AbsolutePath)) return;
                        currentUri = new Uri(currentUri, e.Url.AbsolutePath);
                        if (currentUri.IsAbsoluteUri || currentUri.IsFile)
                        {
                            await new Action(() => {
                                GetHtmlContents(currentUri);
                            }).InvokeAsync();
                        }
                        e.Cancel = true;
                    }
                }
            }
            catch (Exception) { }
        }

        private void WebBrowser_DocumentCompleted(object sender, System.Windows.Forms.WebBrowserDocumentCompletedEventArgs e)
        {
            try
            {
                if (sender == webHtml)
                {
                    ((System.Windows.Forms.WebBrowser)sender).Document.Window.Error += new System.Windows.Forms.HtmlElementErrorEventHandler(Window_Error);

                    var browser = sender as System.Windows.Forms.WebBrowser;
                    foreach (System.Windows.Forms.HtmlElement link in browser.Document.Links)
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(link.GetAttribute("href"))) continue;
                            link.Click += WebBrowser_LinkClick;
                        }
                        catch (Exception) { continue; }
                    }
                    WebBrowserReplaceImageSource(browser);
                }
            }
#if DEBUG
            catch (Exception ex)
            {
                BrowserWait.Fail();
                ex.Message.DEBUG();
            }
#else
            catch (Exception) { BrowserWait.Fail(); }
#endif
            finally
            {
                BrowserWait.Hide();
            }
        }

        private void WebBrowser_PreviewKeyDown(object sender, System.Windows.Forms.PreviewKeyDownEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Forms.WebBrowser)
                {
                    var browser = sender as System.Windows.Forms.WebBrowser;
                    if (e.KeyCode == System.Windows.Forms.Keys.Escape)
                    {
                        browser.Stop();
                    }
                    else if (e.KeyCode == System.Windows.Forms.Keys.F5)
                    {
                        //browser.Refresh();
                    }
                    else if (e.Control && e.KeyCode == System.Windows.Forms.Keys.C)
                    {
                        var text = browser.GetText();
                        Commands.CopyText.Execute(text);
                    }
                    else if (e.Shift && e.KeyCode == System.Windows.Forms.Keys.C)
                    {
                        var html = browser.GetText(true).Trim();
                        var text = browser.GetText(false).Trim();
                        var data = new HtmlTextData() { Html = html, Text = text };
                        Commands.CopyText.Execute(data);
                        //browser.Document.ExecCommand("Copy", false, text);
                    }
                    else if (e.Control && e.KeyCode == System.Windows.Forms.Keys.V)
                    {
                        var text = Clipboard.GetText();
                        browser.Document.ExecCommand("Paste", false, text);
                    }
                    else if (e.Control && e.KeyCode == System.Windows.Forms.Keys.A)
                    {
                        browser.Document.ExecCommand("SelectAll", false, null);
                    }
                }
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR[BROWSER]"); }
#else
            catch (Exception) { }
#endif
        }

        private void Window_Error(object sender, System.Windows.Forms.HtmlElementErrorEventArgs e)
        {
            try
            {
                // Ignore the error and suppress the error dialog box. 
                e.Handled = true;
            }
            catch (Exception) { }
        }

        public BrowerPage()
        {
            InitializeComponent();

            setting = Application.Current.LoadSetting();
            CreateHtmlRender();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Contents)) UpdateDetail(Contents);
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            DeleteHtmlRender();
        }

        private void BrowserWait_ReloadClick(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Contents)) UpdateDetail(Contents);
        }
    }
}
