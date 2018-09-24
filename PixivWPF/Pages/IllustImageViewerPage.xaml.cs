using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
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
    /// IllustImageViewerPage.xaml 的交互逻辑
    /// </summary>
    public partial class IllustImageViewerPage : Page
    {

        public async void UpdateDetail(ImageItem item)
        {
            try
            {
                PreviewWait.Visibility = Visibility.Visible;

                Preview.Tag = item;
                if(item.Illust is Pixeez.Objects.IllustWork)
                {
                    var illust = item.Illust as Pixeez.Objects.IllustWork;
                    var pages = illust.meta_pages[item.Count-1];
                    var url = pages.ImageUrls.Large;
                    if (string.IsNullOrEmpty(url))
                    {
                        if (!string.IsNullOrEmpty(pages.ImageUrls.Original))
                        {
                            url = pages.ImageUrls.Original;
                        }
                        else if (!string.IsNullOrEmpty(pages.ImageUrls.Medium))
                        {
                            url = pages.ImageUrls.Large;
                        }
                        else if (!string.IsNullOrEmpty(pages.ImageUrls.Px480mw))
                        {
                            url = pages.ImageUrls.Px480mw;
                        }
                        else if (!string.IsNullOrEmpty(pages.ImageUrls.SquareMedium))
                        {
                            url = pages.ImageUrls.SquareMedium;
                        }
                    }
                    var tokens = await CommonHelper.ShowLogin();
                    Preview.Source = await url.LoadImage(tokens);
                }
                PreviewWait.Visibility = Visibility.Hidden;
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                PreviewWait.Visibility = Visibility.Hidden;
            }
        }

        public IllustImageViewerPage()
        {
            InitializeComponent();
        }

        private async void ActionSaveIllust_Click(object sender, RoutedEventArgs e)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (Preview.Tag is ImageItem)
            {
                var item = Preview.Tag as ImageItem;
                if (item.Illust is Pixeez.Objects.Work)
                {

                    var illust = item.Illust;
                    var images = illust.ImageUrls;
                    var url = images.Original;

                    var dt = DateTime.Now;

                    if (illust is Pixeez.Objects.IllustWork)
                    {
                        var illustset = illust as Pixeez.Objects.IllustWork;
                        if (illustset.meta_pages.Count() > 0)
                            url = illustset.meta_pages[0].ImageUrls.Original;
                        else if (illustset.meta_single_page is Pixeez.Objects.MetaSinglePage)
                            url = illustset.meta_single_page.OriginalImageUrl;

                        dt = illustset.CreatedTime;
                    }
                    else if (illust is Pixeez.Objects.NormalWork)
                    {
                        var illustset = illust as Pixeez.Objects.NormalWork;
                        dt = illustset.CreatedTime.UtcDateTime;
                    }
                    else if (!string.IsNullOrEmpty(illust.ReuploadedTime))
                    {
                        dt = DateTime.Parse(illust.ReuploadedTime);
                    }

                    if (string.IsNullOrEmpty(url))
                    {
                        if (!string.IsNullOrEmpty(images.Large))
                            url = images.Medium;
                        else if (!string.IsNullOrEmpty(images.Medium))
                            url = images.Medium;
                        else if (!string.IsNullOrEmpty(images.Px480mw))
                            url = images.Px480mw;
                        else if (!string.IsNullOrEmpty(images.SquareMedium))
                            url = images.SquareMedium;
                        else if (!string.IsNullOrEmpty(images.Px128x128))
                            url = images.Px128x128;
                        else if (!string.IsNullOrEmpty(images.Small))
                            url = images.Small;
                    }

                    if (!string.IsNullOrEmpty(url))
                    {
                        //await url.ToImageFile(tokens);
                        var is_meta_single_page = illust.PageCount==1 ? true : false;
                        await url.ToImageFile(tokens, dt, is_meta_single_page);
                        SystemSounds.Beep.Play();
                    }
                }
            }
        }
    }
}
