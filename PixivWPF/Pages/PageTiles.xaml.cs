using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Cache;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;

namespace PixivWPF.Pages
{
    /// <summary>
    /// PageTiles.xaml 的交互逻辑
    /// </summary>
    public partial class PageTiles : Page
    {
        MetroWindow window = Application.Current.MainWindow as MetroWindow;
        internal ObservableCollection<ImageItem> ImageList = new ObservableCollection<ImageItem>();
        Setting setting = Setting.Load();

        public PageTiles()
        {
            InitializeComponent();

            ImageList.Clear();
            ImageTiles.ItemsSource = ImageList;

            ShowImages();
        }

        public async void ShowImages()
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

            if (!string.IsNullOrEmpty(accesstoken))
            {
                var tokens = Pixeez.Auth.AuthorizeWithAccessToken(accesstoken, setting.Proxy, setting.UsingProxy);

                string nexturl = null;
                //var works = await tokens.GetMyFollowingWorksAsync("private");
                var root = nexturl == null ? await tokens.GetMyFollowingWorksAsync() : await tokens.AccessNewApiAsync<Pixeez.Objects.RecommendedRootobject>(nexturl);
                nexturl = root.next_url ?? string.Empty;

                if (root.illusts != null)
                {
                    //ImageList.Clear();

                    foreach (var illust in root.illusts)
                    {
                        try
                        {
                            var url = illust.ImageUrls.SquareMedium;
                            if (string.IsNullOrEmpty(url))
                            {
                                if (!string.IsNullOrEmpty(illust.ImageUrls.Small))
                                {
                                    url = illust.ImageUrls.Small;
                                }
                                else if (!string.IsNullOrEmpty(illust.ImageUrls.Px128x128))
                                {
                                    url = illust.ImageUrls.Px128x128;
                                }
                                else if (!string.IsNullOrEmpty(illust.ImageUrls.Px480mw))
                                {
                                    url = illust.ImageUrls.Px480mw;
                                }
                                else if (!string.IsNullOrEmpty(illust.ImageUrls.Medium))
                                {
                                    url = illust.ImageUrls.Medium;
                                }
                                else if (!string.IsNullOrEmpty(illust.ImageUrls.Large))
                                {
                                    url = illust.ImageUrls.Large;
                                }
                                else if (!string.IsNullOrEmpty(illust.ImageUrls.Original))
                                {
                                    url = illust.ImageUrls.Original;
                                }
                            }
                            if (!string.IsNullOrEmpty(url))
                            {
                                var tooltip = illust.Caption.ToLineBreak(72);
                                var i = new ImageItem()
                                {
                                    //Source = new BitmapImage(new Uri(url)),
                                    Thumb = url,
                                    //Source = await url.ToImageSource(tokens),
                                    ID = illust.Id.ToString(),
                                    UserID = illust.User.Id.ToString(),
                                    Subject = illust.Title,
                                    Caption = string.Join("\n", tooltip),
                                    Illust = illust
                                };
                                ImageList.Add(i);
                            }
                        }
                        catch (Exception ex)
                        {
                            await window.ShowMessageAsync("ERROR", ex.Message);
                        }
                    }

                    UpdateImageTile(tokens);

                    //new Thread(delegate ()
                    //{
                    //    var opt = new ParallelOptions();
                    //    opt.MaxDegreeOfParallelism = 5;
                    //    Parallel.ForEach(ImageList, opt, (item, loopstate, elementIndex) =>
                    //    {
                    //        item.Dispatcher.BeginInvoke(new Action(async () =>
                    //        {
                    //            try
                    //            {
                    //                item.Source = await item.Thumb.ToImageSource(tokens);
                    //                ImageTiles.Items.Refresh();
                    //            }
                    //            catch (Exception ex)
                    //            {
                    //                await window.ShowMessageAsync("ERROR", ex.Message);
                    //            }                            
                    //        }));
                    //    });
                    //}).Start();

                    //foreach (var item in ImageList)
                    //{
                    //    try
                    //    {
                    //        if (item.Source == null)
                    //        {
                    //            if (Application.Current.Dispatcher.CheckAccess())
                    //            {
                    //                item.Source = await item.Thumb.ToImageSource(tokens);
                    //            }
                    //            else
                    //            {
                    //                await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
                    //                {
                    //                    item.Source = await item.Thumb.ToImageSource(tokens);
                    //                }));
                    //            }
                    //            ImageTiles.Items.Refresh();
                    //            //ImageTiles.ItemsSource = null;
                    //            //ImageTiles.ItemsSource = ImageList;
                    //        }
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        await window.ShowMessageAsync("ERROR", ex.Message);
                    //    }
                    //}
                }
            }
        }

        public async void UpdateImageTile(Pixeez.Tokens tokens)
        {
            new Thread(delegate ()
            {
                var opt = new ParallelOptions();
                opt.MaxDegreeOfParallelism = 10;
                Parallel.ForEach(ImageList, opt, (item, loopstate, elementIndex) =>
                {
                    item.Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        try
                        {
                            item.Source = await item.Thumb.ToImageSource(tokens);
                            ImageTiles.Items.Refresh();
                        }
                        catch (Exception ex)
                        {
                            await window.ShowMessageAsync("ERROR", ex.Message);
                        }
                    }));
                });
            }).Start();
        }

        private async void ImageTiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var idx = ImageTiles.SelectedIndex;
                var item = ImageList[idx];
                var url = item.Illust.ImageUrls.Large;
                if (string.IsNullOrEmpty(url))
                {
                    url = item.Illust.ImageUrls.Medium;
                }
                PreviewWait.Visibility = Visibility.Visible;
                var tokens = Pixeez.Auth.AuthorizeWithAccessToken(item.AccessToken, setting.Proxy, setting.UsingProxy);
                Preview.Source = await url.ToImageSource(tokens);
                PreviewWait.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                await window.ShowMessageAsync("ERROR", ex.Message);
            }
        }
    }

    public class ImageItem : FrameworkElement//, INotifyPropertyChanged
    {
        //public static readonly DependencyProperty ImageProperty = DependencyProperty.Register("ImageItem", typeof(ImageSource), typeof(PageTiles));

        //public ImageSource Source
        //{
        //    get { return (ImageSource)GetValue(ImageProperty); }
        //    set { SetValue(ImageProperty, value); }
        //}

        public ImageSource Source { get; set; }
        public string Thumb { get; set; }
        public string Subject { get; set; }
        public string Caption { get; set; }
        public string UserID { get; set; }
        public string ID { get; set; }
        public Pixeez.Objects.IllustWork Illust { get; set; }
        public string AccessToken { get; set; }
    }
}
