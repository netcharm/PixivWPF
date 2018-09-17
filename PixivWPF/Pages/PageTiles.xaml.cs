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

namespace PixivWPF.Pages
{
    /// <summary>
    /// PageTiles.xaml 的交互逻辑
    /// </summary>
    public partial class PageTiles : Page
    {
        internal List<ImageItem> Items = new List<ImageItem>();

        public PageTiles()
        {
            InitializeComponent();

            Items.Clear();

            ShowImages();
        }

        public async void ShowImages()
        {
            var accesstoken = Setting.Token();
            if (string.IsNullOrEmpty(accesstoken))
            {
                var dlgLogin = new LoginDialog() { AccessToken=string.Empty};
                var ret = dlgLogin.ShowDialog();
                //if (ret == true)
                {
                    //var value = dlgLogin.Value
                    accesstoken = dlgLogin.AccessToken;
                    Setting.Token(accesstoken);
                }
            }
            if (!string.IsNullOrEmpty(accesstoken))
            {
                var setting = Setting.Load();
                var tokens = Pixeez.Auth.AuthorizeWithAccessToken(accesstoken, setting.Proxy, setting.UsingProxy);
                var works = await tokens.GetMyFavoriteWorksAsync(1, 30);
                foreach (var work in works)
                {
                    long id = (long)work.Work.Id;
                    var illusts = await tokens.GetWorksAsync(id);
                    if (illusts.Count > 0)
                    {
                        var illust = illusts[0];
                        var url = illust.ImageUrls.Small;
                        var i = new ImageItem()
                        {
                            Source = new BitmapImage(new Uri(url)),
                            Title = illust.Title
                        };
                    }
                    //Items.Add(new )
                }
                //var users = await tokens.GetUsersAsync(11972);
            }
        }
    }

    public class ImageItem
    {
        public ImageSource Source { get; set; }
        public string Title { get; set; }
        public string UID { get; set; }
        public string ID { get; set; }
    }
}
