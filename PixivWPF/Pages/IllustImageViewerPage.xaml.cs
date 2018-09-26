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
        internal object DataType = null;

        public async void UpdateDetail(ImageItem item)
        {
            try
            {
                PreviewWait.Visibility = Visibility.Visible;

                DataType = item;
                if (item.Illust is Pixeez.Objects.Work)
                {
                    var illust = item.Illust as Pixeez.Objects.Work;
                    var url = illust.GetPreviewUrl(item.Index);

                    var tokens = await CommonHelper.ShowLogin();
                    Preview.Source = await url.LoadImage(tokens);
                    if (Preview.Source != null && Preview.Source.Width < 450)
                    {
                        url = illust.GetOriginalUrl(item.Index);
                        Preview.Source = await url.LoadImage(tokens);
                    }
                }
                //if (item.Illust is Pixeez.Objects.IllustWork)
                //{
                //    var illust = item.Illust as Pixeez.Objects.IllustWork;
                //    var url = illust.GetPreviewUrl(item.Index);

                //    var tokens = await CommonHelper.ShowLogin();
                //    Preview.Source = await url.LoadImage(tokens);
                //    if (Preview.Source != null && Preview.Source.Width < 450)
                //    {
                //        url = illust.GetOriginalUrl(item.Index);
                //        Preview.Source = await url.LoadImage(tokens);
                //    }
                //}
                //else if (item.Illust is Pixeez.Objects.NormalWork)
                //{
                //    var illust = item.Illust as Pixeez.Objects.NormalWork;
                //    var url = illust.GetPreviewUrl(item.Index);

                //    var tokens = await CommonHelper.ShowLogin();
                //    Preview.Source = await url.LoadImage(tokens);
                //    if (Preview.Source != null && Preview.Source.Width < 450)
                //    {
                //        url = illust.GetOriginalUrl(item.Index);
                //        Preview.Source = await url.LoadImage(tokens);
                //    }
                //}
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

            if (DataType is ImageItem)
            {
                var item = DataType as ImageItem;
                if (item.Illust is Pixeez.Objects.Work)
                {

                    var Illust = item.Illust;
                    var idx = item.Count-1;
                    var url = Illust.GetOriginalUrl(idx);
                    var dt = Illust.GetDateTime();

                    if (!string.IsNullOrEmpty(url))
                    {
                        //await url.ToImageFile(tokens);
                        var is_meta_single_page = Illust.PageCount==1 ? true : false;
                        await url.ToImageFile(tokens, dt, is_meta_single_page);
                        SystemSounds.Beep.Play();
                    }
                }
            }
        }
    }
}
