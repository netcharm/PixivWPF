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

        private void Toast_Loaded(object sender, RoutedEventArgs e)
        {
            parentWindow = Window.GetWindow(this);

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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if(parentWindow == null) parentWindow = Window.GetWindow(this);
            this.Visibility = Visibility.Hidden;
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
                    System.Diagnostics.Process.Start(image);
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
                    System.Diagnostics.Process.Start(image);
                }
            }            
        }

    }
}
