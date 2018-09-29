using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    }
}
