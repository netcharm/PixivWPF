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
using System.Windows.Shapes;

namespace ImageCompare
{
    /// <summary>
    /// ImageCompareUI.xaml 的交互逻辑
    /// </summary>
    public partial class ImageCompareUI : Page
    {
        public ImageCompareUI()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            //this.Background = new ImageBrush() { ImageSource = new BitmapImage(new Uri("pack://application:,,,/ImageCompare;component/Resources/CheckboardPattern_32.png")) };
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {

        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {

        }

        private void ImageScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {

        }

        private void ImageBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void ImageBox_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void ImageBox_MouseMove(object sender, MouseEventArgs e)
        {

        }
    }
}
