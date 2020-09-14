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
    public partial class NavPage : Page
    {
        private Setting setting = Application.Current.LoadSetting();
        private PixivPage page = PixivPage.Recommanded;

        internal MahApps.Metro.Controls.Flyout NavFlyout = null;

        private Brush ToggleOnBG = new SolidColorBrush(Colors.Transparent);
        private Brush ToggleOffBG = new SolidColorBrush(Colors.Transparent);

        internal void CheckPage()
        {
            ToggleButton[] btns = new ToggleButton[] {
                btnGotoRecommended, btnGotoLatest, btnGotoTrendingTags,
                btnGotoMy, btnGotoMyFollowerUser, btnGotoMyFollowingUser, btnGotoMyFollowingUserPrivate, btnGotoMyPixivUser, btnGotoBlacklistUser,
                btnGotoFeeds, btnGotoFollowing, btnGotoFollowingPrivate,
                btnGotoFavorite, btnGotoFavoritePrivate,
                btnGotoRankingDay, btnGotoRankingDayMale, btnGotoRankingDayFemale, btnGotoRankingDayR18, btnGotoRankingDayMaleR18, btnGotoRankingDayFemaleR18,
                btnGotoRankingWeek, btnGotoRankingWeekOriginal, btnGotoRankingWeekRookie, btnGotoRankingWeekR18, btnGotoRankingWeekR18G,
                btnGotoRankingMonth, btnGotoRankingYear
            };

            var sender = btnGotoRecommended;

            if (page == PixivPage.Latest)
            {
                sender = btnGotoLatest;
            }
            else if (page == PixivPage.TrendingTags)
            {
                sender = btnGotoTrendingTags;
            }
            else if (page == PixivPage.My)
            {
                sender = btnGotoMy;
            }
            else if (page == PixivPage.MyFollowerUser)
            {
                sender = btnGotoMyFollowerUser;
            }
            else if (page == PixivPage.MyFollowingUser)
            {
                sender = btnGotoMyFollowingUser;
            }
            else if (page == PixivPage.MyFollowingUserPrivate)
            {
                sender = btnGotoMyFollowingUserPrivate;
            }
            else if (page == PixivPage.MyPixivUser)
            {
                sender = btnGotoMyPixivUser;
            }
            else if (page == PixivPage.MyBlacklistUser)
            {
                sender = btnGotoBlacklistUser;
            }
            else if (page == PixivPage.Feeds)
            {
                sender = btnGotoFeeds;
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
            else if (page == PixivPage.RankingDay)
            {
                sender = btnGotoRankingDay;
            }
            else if (page == PixivPage.RankingDayR18)
            {
                sender = btnGotoRankingDayR18;
            }
            else if (page == PixivPage.RankingDayMale)
            {
                sender = btnGotoRankingDayMale;
            }
            else if (page == PixivPage.RankingDayMaleR18)
            {
                sender = btnGotoRankingDayMaleR18;
            }
            else if (page == PixivPage.RankingDayFemale)
            {
                sender = btnGotoRankingDayFemale;
            }
            else if (page == PixivPage.RankingDayFemaleR18)
            {
                sender = btnGotoRankingDay;
            }
            else if (page == PixivPage.RankingWeek)
            {
                sender = btnGotoRankingWeek;
            }
            else if (page == PixivPage.RankingWeekR18)
            {
                sender = btnGotoRankingWeekR18;
            }
            else if (page == PixivPage.RankingWeekR18G)
            {
                sender = btnGotoRankingWeekR18G;
            }
            else if (page == PixivPage.RankingWeekOriginal)
            {
                sender = btnGotoRankingWeekOriginal;
            }
            else if (page == PixivPage.RankingWeekRookie)
            {
                sender = btnGotoRankingWeekRookie;
            }
            else if (page == PixivPage.RankingMonth)
            {
                sender = btnGotoRankingMonth;
            }
            else if (page == PixivPage.RankingYear)
            {
                sender = btnGotoRankingYear;
            }
            else
            {
                sender = btnGotoRecommended;
            }

            ToggleOnBG = Theme.AccentBrush;

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

        public NavPage()
        {
            InitializeComponent();

            cbAccent.Items.Clear();
            cbAccent.ItemsSource = Theme.Accents;
            cbAccent.SelectedIndex = cbAccent.Items.IndexOf(Theme.CurrentAccent);

            ToggleOnBG = Theme.AccentBrush;

#if DEBUG
            btnGotoFeeds.Visibility = Visibility.Visible;
#else
            btnGotoFeeds.Visibility = Visibility.Collapsed;
#endif
            CheckPage();
        }

        private void NavItem_Click(object sender, RoutedEventArgs e)
        {
            TilesPage targetPage = null;
            bool IsAppend = false;
            if (this.Tag is TilesPage)
                targetPage = this.Tag as TilesPage;
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
            else if (sender == btnGotoTrendingTags)
            {
                page = PixivPage.TrendingTags;
            }
            else if (sender == btnGotoMy)
            {
                page = PixivPage.My;
            }
            else if (sender == btnGotoMyFollowerUser)
            {
                page = PixivPage.MyFollowerUser;
            }
            else if (sender == btnGotoMyFollowingUser)
            {
                page = PixivPage.MyFollowingUser;
            }
            else if (sender == btnGotoMyFollowingUserPrivate)
            {
                page = PixivPage.MyFollowingUserPrivate;
            }
            else if (sender == btnGotoMyPixivUser)
            {
                page = PixivPage.MyPixivUser;
            }
            else if (sender == btnGotoBlacklistUser)
            {
                page = PixivPage.MyBlacklistUser;
            }
            else if (sender == btnGotoFeeds)
            {
                page = PixivPage.Feeds;
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
            else if (sender == btnGotoRankingDay)
            {
                page = PixivPage.RankingDay;
            }
            else if (sender == btnGotoRankingDayR18)
            {
                page = PixivPage.RankingDayR18;
            }
            else if (sender == btnGotoRankingDayMale)
            {
                page = PixivPage.RankingDayMale;
            }
            else if (sender == btnGotoRankingDayMaleR18)
            {
                page = PixivPage.RankingDayMaleR18;
            }
            else if (sender == btnGotoRankingDayFemale)
            {
                page = PixivPage.RankingDayFemale;
            }
            else if (sender == btnGotoRankingDayFemaleR18)
            {
                page = PixivPage.RankingDayFemaleR18;
            }
            else if (sender == btnGotoRankingWeek)
            {
                page = PixivPage.RankingWeek;
            }
            else if (sender == btnGotoRankingWeekR18)
            {
                page = PixivPage.RankingWeekR18;
            }
            else if (sender == btnGotoRankingWeekR18G)
            {
                return;
                //page = PixivPage.RankingWeekR18G;
            }
            else if (sender == btnGotoRankingWeekOriginal)
            {
                page = PixivPage.RankingWeekOriginal;
            }
            else if (sender == btnGotoRankingWeekRookie)
            {
                page = PixivPage.RankingWeekRookie;
            }
            else if (sender == btnGotoRankingMonth)
            {
                page = PixivPage.RankingMonth;
            }
            else if (sender == btnGotoRankingYear)
            {
                return;
                //page = PixivPage.RankingYear;
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
            TilesPage targetPage = null;
            if (this.Tag is TilesPage) targetPage = this.Tag as TilesPage;
            else return;

            if(sender == btnGotoNextPage)
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
                    CheckPage();
                }
            }
        }

        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            Theme.Toggle();
            CheckPage();
        }

    }
}
