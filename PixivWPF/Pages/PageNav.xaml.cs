using MahApps.Metro;
using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        private Setting setting = Setting.Load();
        private PixivPage page = PixivPage.Recommanded;

        public MahApps.Metro.Controls.Flyout NavFlyout = null;

        private Brush ToggleOnBG = new SolidColorBrush(Colors.Transparent);
        private Brush ToggleOffBG = new SolidColorBrush(Colors.Transparent);

        internal void CheckPage()
        {
            ToggleButton[] btns = new ToggleButton[] {
                btnGotoRecommended, btnGotoLatest,
                btnGotoFollowing, btnGotoFollowingPrivate,
                btnGotoFavorite, btnGotoFavoritePrivate,
                btnGotoRankingDaily, btnGotoRankingWeekly,
                btnGotoRankingMonthly, btnGotoRankingYearly
            };

            var sender = btnGotoRecommended;
            if (page == PixivPage.Latest)
            {
                sender = btnGotoLatest;
            }
            else if (page == PixivPage.Follow)
            {
                sender = btnGotoFollowing;
            }
            else if (page == PixivPage.FollowPrivate)
            {
                sender = btnGotoFollowingPrivate;
            }
            else if (page == PixivPage.Favorite)
            {
                sender = btnGotoFavorite;
            }
            else if (page == PixivPage.FavoritePrivate)
            {
                sender = btnGotoFavoritePrivate;
            }
            else if (page == PixivPage.RankingDaily)
            {
                sender = btnGotoRankingDaily;
            }
            else if (page == PixivPage.RankingWeekly)
            {
                sender = btnGotoRankingWeekly;
            }
            else if (page == PixivPage.RankingMonthly)
            {
                sender = btnGotoRankingMonthly;
            }
            else if (page == PixivPage.RankingYearly)
            {
                sender = btnGotoRankingYearly;
            }
            else
            {
                sender = btnGotoRecommended;
            }

            foreach (var btn in btns)
            {
                var b = btn as ToggleButton;
                if (b == sender)
                {
                    b.IsChecked = true;
                    b.Background = ToggleOnBG;
                }
                else
                {
                    b.IsChecked = false;
                    b.Background = ToggleOffBG;
                }
            }

            if (NavFlyout is MahApps.Metro.Controls.Flyout)
            {
                NavFlyout.Theme = MahApps.Metro.Controls.FlyoutTheme.Adapt;
                NavFlyout.Theme = MahApps.Metro.Controls.FlyoutTheme.Accent;
                NavFlyout.DataContext = page;
                //NavFlyout.Opacity = 0.95;
            }
        }

        public PageNav()
        {
            InitializeComponent();

            cbAccent.Items.Clear();
            cbAccent.ItemsSource = Theme.Accents;
            cbAccent.SelectedIndex = cbAccent.Items.IndexOf(Theme.CurrentAccent);

            ToggleOnBG = Theme.AccentBrush;

            CheckPage();
        }

        private void NavItem_Click(object sender, RoutedEventArgs e)
        {
            PageTiles targetPage = null;
            bool IsAppend = false;
            if (this.Tag is PageTiles)
                targetPage = this.Tag as PageTiles;
            else
                return;

            if (sender == btnGotoRecommended)
            {
                page = PixivPage.Recommanded;
            }
            else if (sender == btnGotoLatest)
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
                page = PixivPage.RankingDaily;
            }
            else if (sender == btnGotoRankingWeekly)
            {
                page = PixivPage.RankingWeekly;
            }
            else if (sender == btnGotoRankingMonthly)
            {
                page = PixivPage.RankingMonthly;
            }
            else if (sender == btnGotoRankingYearly)
            {
                page = PixivPage.RankingYearly;
            }
            else
            {
                page = PixivPage.Recommanded;
            }
            CheckPage();
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
            //if (string.IsNullOrEmpty(accesstoken))
            //{
                var dlgLogin = new PixivLoginDialog() { AccessToken=string.Empty};
                var ret = dlgLogin.ShowDialog();
                //if (ret == true)
                {
                    //var value = dlgLogin.Value
                    accesstoken = dlgLogin.AccessToken;
                    Setting.Token(accesstoken);
                }
            //}
        }

        private void Accent_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(cbAccent.SelectedIndex>=0 && cbAccent.SelectedIndex < cbAccent.Items.Count)
            {
                if(cbAccent.SelectedValue is string)
                {
                    var accent = cbAccent.SelectedValue as string;
                    Theme.CurrentAccent = accent;
                    ToggleOnBG = Theme.AccentBrush;
                    CheckPage();
                }
            }
        }

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            Theme.Toggle();
            ToggleOnBG = Theme.AccentBrush;
            CheckPage();
        }

    }
}
