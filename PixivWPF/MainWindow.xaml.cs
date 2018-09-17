using MahApps.Metro.Controls;
using PixivWPF.Common;
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

namespace PixivWPF
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public Frame MainContent = null;

        public MainWindow()
        {
            InitializeComponent();

            MainContent = ContentFrame;

            NavFrame.Content = new Pages.PageNav() { Tag = NavFrame };
            //ContentFrame.Content = new Pages.PageLogin() { Tag = ContentFrame };
            ContentFrame.Content = new Pages.PageTiles() { Tag = ContentFrame };

            var setting = Setting.Load();
        }
    }
}
