using MahApps.Metro;
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

namespace PixivWPF.Pages
{
    /// <summary>
    /// PageNav.xaml 的交互逻辑
    /// </summary>
    public partial class PageNav : Page
    {
        public PageNav()
        {
            InitializeComponent();
        }

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
            var appTheme = appStyle.Item1;
            var appAccent = appStyle.Item2;

            ThemeManager.ChangeAppStyle(Application.Current, appAccent, ThemeManager.GetInverseAppTheme(appTheme));
        }
    }
}
