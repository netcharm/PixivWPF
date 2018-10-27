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
using System.Windows.Navigation;

namespace PixivWPF.Common
{
    /// <summary>
    /// NotificationItem.xaml 的交互逻辑
    /// </summary>
    public partial class ToastItem : UserControl
    {
        private Window parentWindow = null;

        public ToastItem()
        {
            InitializeComponent();
        }

        internal void CheckImageSource()
        {
            if (Preview.Source == null)
            {
                Preview.Visibility = Visibility.Collapsed;
                OpenPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                Preview.Visibility = Visibility.Visible;
                OpenPanel.Visibility = Visibility.Visible;
            }
        }

        private void Toast_Loaded(object sender, RoutedEventArgs e)
        {
            parentWindow = Window.GetWindow(this);
            if (parentWindow is Window)
                parentWindow.Closing += Window_Closing;
            CheckImageSource();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if(parentWindow == null) parentWindow = Window.GetWindow(this);
            this.Visibility = Visibility.Hidden;
            Preview.Source = null;
            parentWindow.Close();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button)
            {
                var btn = sender as Button;
                if (btn.Tag is string)
                {
                    var image = System.IO.Path.GetDirectoryName((string)btn.Tag);
                    if (System.IO.Directory.Exists(image))
                    {
                        System.Diagnostics.Process.Start(image);
                    }
                }
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if(sender is Button)
            {
                var btn = sender as Button;
                if(btn.Tag is string)
                {
                    var image = (string)btn.Tag;
                    if (System.IO.File.Exists(image))
                    {
                        System.Diagnostics.Process.Start(image);
                    }
                }
            }            
        }

        private async void Preview_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if(sender == Preview)
            {
                if(e.Property.Name.Equals("Source", StringComparison.CurrentCultureIgnoreCase) ||
                   e.Property.Name.Equals("Tag", StringComparison.CurrentCultureIgnoreCase))
                {
                    var image = Preview as Image;
                    if(image.Tag is string)
                    {
                        var file = (string)image.Tag;
                        //if (!string.IsNullOrEmpty(file) && System.IO.File.Exists(file))
                        //{
                        //    Preview.Source = await file.LoadImage();
                        //    CheckImageSource();
                        //}
                        if(!string.IsNullOrEmpty(file))
                        {
                            Preview.Source = await file.GetLocalFile().LoadImage();
                            if(Preview.Source == null)
                            {
                                Pixeez.Tokens tokens = await CommonHelper.ShowLogin();
                                Preview.Source = await file.LoadImage(tokens);
                            }
                            CheckImageSource();
                        }
                    }
                    //else Preview.Source = 
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Preview.Source = null;
//            e.Cancel = 
        }
    }
}
