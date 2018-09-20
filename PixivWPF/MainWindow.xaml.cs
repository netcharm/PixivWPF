using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
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

            //ContentFrame.Content = new Pages.PageLogin() { Tag = ContentFrame };
            var pagetiles = new Pages.PageTiles() { Tag = ContentFrame };
            var pagenav = new Pages.PageNav() { Tag = pagetiles, NavFlyout = NavFlyout };

            ContentFrame.Content = pagetiles;
            NavFrame.Content = pagenav;

            NavFlyout.Content = pagenav;
            NavFlyout.Theme = FlyoutTheme.Adapt;
            NavFlyout.Theme = FlyoutTheme.Accent;
            NavFlyout.Opacity = 0.95;

            ContentFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
            NavFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
        }

        private async void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
#if !DEBUG
            e.Cancel = true;

            var opt = new MetroDialogSettings();
            opt.AffirmativeButtonText = "Yes";
            opt.NegativeButtonText = "No";
            opt.DefaultButtonFocus = MessageDialogResult.Affirmative;
            opt.DialogMessageFontSize = 24;
            opt.DialogResultOnCancel = MessageDialogResult.Canceled;

            var ret = await this.ShowMessageAsync("Confirm", "Continue Exit?", MessageDialogStyle.AffirmativeAndNegative, opt);
            if(ret == MessageDialogResult.Affirmative)
            {
                Application.Current.Shutdown();
            }
#endif
        }

        private async void CommandLogin_Click(object sender, RoutedEventArgs e)
        {
            var accesstoken = Setting.Token();
            var dlgLogin = new PixivLoginDialog() { AccessToken = accesstoken };
            var ret = dlgLogin.ShowDialog();
            accesstoken = dlgLogin.AccessToken;
            Setting.Token(accesstoken);
        }

        private void CommandNav_Click(object sender, RoutedEventArgs e)
        {
            NavFlyout.IsOpen = !NavFlyout.IsOpen;
        }

        private void CommandNavPrev_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CommandNavNext_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
