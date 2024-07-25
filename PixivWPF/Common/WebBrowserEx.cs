using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace PixivWPF.Common
{
    public class WebBrowserEx : System.Windows.Forms.WebBrowser
    {
        internal new void Dispose(bool disposing)
        {
            // call WebBrower.Dispose(bool)
            base.Dispose(disposing);
        }

        private bool ignore_all_error = false;
        public bool IgnoreAllError
        {
            get { return (ignore_all_error); }
            set
            {
                ignore_all_error = value;
                if (value) SuppressedAllError();
            }
        }

        /// <summary>
        /// code from -> https://stackoverflow.com/a/13788814/1842521
        /// </summary>
        private void SuppressedAllError()
        {
            ScriptErrorsSuppressed = true;

            try
            {
                FieldInfo field = typeof(WebBrowserEx).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    object axIWebBrowser2 = field.GetValue(this);
                    if (axIWebBrowser2 != null) axIWebBrowser2.GetType().InvokeMember("Silent", BindingFlags.SetProperty, null, axIWebBrowser2, new object[] { true });
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public string GetText(bool html = false, bool all_without_selection = true)
        {
            string result = string.Empty;
            try
            {
                if (this is System.Windows.Forms.WebBrowser &&
                    Document is System.Windows.Forms.HtmlDocument &&
                    Document.DomDocument is mshtml.IHTMLDocument2)
                {
                    StringBuilder sb = new StringBuilder();
                    mshtml.IHTMLDocument2 document = Document.DomDocument as mshtml.IHTMLDocument2;
                    mshtml.IHTMLSelectionObject currentSelection = document.selection;
                    if (currentSelection != null && currentSelection.type.Equals("Text", StringComparison.CurrentCultureIgnoreCase))
                    {
                        mshtml.IHTMLTxtRange range = currentSelection.createRange() as mshtml.IHTMLTxtRange;
                        if (range != null)
                        {
                            mshtml.IHTMLElement root = range.parentElement();
                            if (root.tagName.Equals("html", StringComparison.CurrentCultureIgnoreCase))
                            {
                                var bodies = Document.GetElementsByTagName("body");
                                foreach (System.Windows.Forms.HtmlElement body in bodies)
                                {
                                    sb.AppendLine(html ? body.InnerHtml : body.InnerText);
                                }
                            }
                            else
                                sb.AppendLine(html ? range.htmlText : range.text);
                        }
                    }
                    else if (all_without_selection)
                    {
                        var bodies = Document.GetElementsByTagName("body");
                        foreach (System.Windows.Forms.HtmlElement body in bodies)
                        {
                            sb.AppendLine(html ? body.InnerHtml : body.InnerText);
                        }
                    }
                    result = sb.Length > 0 ? sb.ToString().Trim().KatakanaHalfToFull() : string.Empty;
                }
            }
            catch (Exception ex) { ex.ERROR("GetBrowserText"); }
            return (result);
        }

        public static implicit operator WebBrowser(WebBrowserEx v)
        {
            return (v);
        }
    }
}
