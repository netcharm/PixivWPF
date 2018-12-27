using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PixivWPF.Common
{
    /// <summary>
    /// ImageViewerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ContentWindow : MetroWindow
    {

        //public object Content
        //{
        //    get { return ViewerContent.Content; }
        //    set { ViewerContent.Content = value; }
        //}

        public ContentWindow()
        {
            InitializeComponent();

            //Topmost = true;
            ShowActivated = true;
            //Activate();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (this.Content is Pages.DownloadManagerPage) return;
            if (this.Tag is Pages.DownloadManagerPage) return;

            e.Handled = true;
            if (e.Key == Key.Escape) Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            var fmts = e.Data.GetFormats(true);
            if (new List<string>(fmts).Contains("Text"))
            {
                e.Effects = DragDropEffects.Link;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            var fmts = new List<string>(e.Data.GetFormats(true));

            if (fmts.Contains("text/html"))
            {
                using (var ms = (System.IO.MemoryStream)e.Data.GetData("text/html"))
                {
                    List<string> links = new List<string>();

                    var html = Encoding.Unicode.GetString(ms.ToArray());
                    if(Regex.IsMatch(html, @"href=.*?illust_id=\d+"))
                    {
                        foreach (Match m in Regex.Matches(html, @"href=""(https:\/\/www.pixiv.net\/member_illust\.php\?mode=.*?illust_id=\d+.*?)"""))
                        {
                            var link = m.Groups[1].Value;
                            if (!string.IsNullOrEmpty(link))
                            {
                                if (!links.Contains(link))
                                {
                                    links.Add(link);
                                    CommonHelper.Cmd_Search.Execute(link);
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (Match m in Regex.Matches(html, @"href=""(https:\/\/www.pixiv.net\/member\.php\?id=\d+)"""))
                        {
                            var link = m.Groups[1].Value;
                            if (!string.IsNullOrEmpty(link))
                            {
                                if (!links.Contains(link))
                                {
                                    links.Add(link);
                                    CommonHelper.Cmd_Search.Execute(link);
                                }
                            }
                        }
                    }
                }
            }
            else if (fmts.Contains("Text"))
            {
                var link = (string)e.Data.GetData("Text");
                if(new Uri(link) != null)
                {
                    CommonHelper.Cmd_Search.Execute(link);
                }
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e.ChangedButton == MouseButton.XButton1)
            {
                if (Title.Equals("DropBox", StringComparison.CurrentCultureIgnoreCase))
                {
                    //Hide();
                }
                else if (Title.Equals("Download Manager", StringComparison.CurrentCultureIgnoreCase))
                {
                    Hide();
                }
                else
                {
                    Close();
                }
                e.Handled = true;
            }
        }
    }
}
