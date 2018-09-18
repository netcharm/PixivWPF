using MahApps.Metro;
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

namespace PixivWPF.Pages
{
    /// <summary>
    /// PageNav.xaml 的交互逻辑
    /// </summary>
    public partial class PageNav : Page
    {
        private PixivPage page = PixivPage.Recommanded;

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

        private void NavItem_Click(object sender, RoutedEventArgs e)
        {
            PageTiles targetPage = null;
            bool IsAppend = false;
            if (this.Tag is PageTiles)
                targetPage = this.Tag as PageTiles;
            else
                return;

            if(sender == btnGotoRecommended)
            {
                page = PixivPage.Recommanded;
            }
            else if(sender == btnGotoLatest)
            {
                page = PixivPage.Latest;
            }
            else if (sender == btnGotoFollowing)
            {
                page = PixivPage.Follow;
            }
            else if (sender == btnGotoFollowingPrivate)
            {
                page = PixivPage.FollowPrivate;
            }
            else if (sender == btnGotoFavorite)
            {
                page = PixivPage.Favorite;
            }
            else if (sender == btnGotoFavoritePrivate)
            {
                page = PixivPage.FavoritePrivate;
            }
            else if (sender == btnGotoRankingDaily)
            {
                page = PixivPage.DailyTop;
            }
            else if (sender == btnGotoRankingWeekly)
            {
                page = PixivPage.WeeklyTop;
            }
            else if (sender == btnGotoRankingMonthly)
            {
                page = PixivPage.MonthlyTop;
            }
            else
            {
                page = PixivPage.Recommanded;
            }
            targetPage.ShowImages(page, IsAppend);
        }

        private void NavPage_Click(object sender, RoutedEventArgs e)
        {
            PageTiles targetPage = null;
            if (this.Tag is PageTiles) targetPage = this.Tag as PageTiles;
            else return;

            if (sender == btnGotoPrevPage)
            {
                targetPage.ShowImages(page, "prev");
            }
            else if(sender == btnGotoNextPage)
            {
                //targetPage.ShowImages(page, "next");
                targetPage.ShowImages(page, true);
            }
        }

        private void NavLogin_Click(object sender, RoutedEventArgs e)
        {
            var accesstoken = Setting.Token();
            if (string.IsNullOrEmpty(accesstoken))
            {
                var dlgLogin = new PixivLoginDialog() { AccessToken=string.Empty};
                var ret = dlgLogin.ShowDialog();
                //if (ret == true)
                {
                    //var value = dlgLogin.Value
                    accesstoken = dlgLogin.AccessToken;
                    Setting.Token(accesstoken);
                }
            }
        }

    }
}
