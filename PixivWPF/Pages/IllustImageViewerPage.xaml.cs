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
        internal Window window = null;

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
                    if (Preview.Source == null || Preview.Source.Width < 450)
                    {
                        //var large = await item.Illust.GetOriginalUrl(item.Index).LoadImage(tokens);
                        var large = await illust.GetOriginalUrl(item.Index).LoadImage(tokens);
                        if (large != null) Preview.Source = large;
                    }
                    if (window == null)
                    {
                        window = this.GetActiveWindow();
                        if (window is Window)
                        {
                            //window.KeyUp += Preview_KeyUp;
                            window.PreviewKeyUp += Page_PreviewKeyUp;
                        }
                    }
                    window.Title = $"ID: {item.ID}, {item.Subject}";
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

        internal void ChangeIllustPage(int offset)
        {
            if (DataType is ImageItem)
            {
                var item = DataType as ImageItem;
                var illust = item.Illust;
                int index_p = item.Index;
                if (index_p < 0) index_p = 0;
                var index_n = item.Index+offset;
                if (index_n < 0) index_n = 0;
                if (index_n >= item.Count - 1) index_n = item.Count - 1;
                if (index_n == index_p) return;

                var i = new ImageItem()
                {
                    NextURL = item.NextURL,
                    Thumb = illust.GetThumbnailUrl(index_n),
                    Index = index_n,
                    Count = illust.PageCount.Value,
                    BadgeValue = (index_n + 1).ToString(),
                    ID = illust.Id.ToString(),
                    UserID = illust.User.Id.ToString(),
                    Subject = $"{illust.Title} - {index_n + 1}/{illust.PageCount}",
                    DisplayTitle = false,
                    Illust = illust,
                    Tag = item.Tag
                };
                UpdateDetail(i);
            }
        }

        internal async void SaveIllust()
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return;

            if (DataType is ImageItem)
            {
                var item = DataType as ImageItem;
                if (item.Illust is Pixeez.Objects.Work)
                {

                    var illust = item.Illust;
                    var idx = item.Index;
                    var url = illust.GetOriginalUrl(idx);
                    var dt = illust.GetDateTime();

                    if (!string.IsNullOrEmpty(url))
                    {
                        //await url.ToImageFile(tokens);
                        try
                        {
                            var is_meta_single_page = illust.PageCount==1 ? true : false;
                            //await url.ToImageFile(tokens, dt, is_meta_single_page);
                            //SystemSounds.Beep.Play();                            
                            url.ToImageFile(illust.GetThumbnailUrl(idx), dt, is_meta_single_page);
                        }
                        catch (Exception ex)
                        {
                            ex.Message.ShowMessageBox("ERROR");
                        }
                    }
                }
            }
        }

        public IllustImageViewerPage()
        {
            InitializeComponent();
            window = Window.GetWindow(this);
            if(window is Window)
            {
                //window.KeyUp += Preview_KeyUp;
                window.PreviewKeyUp += Page_PreviewKeyUp;
            }
        }

        private void ActionSaveIllust_Click(object sender, RoutedEventArgs e)
        {
            SaveIllust();
        }

        private void Preview_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            int offset = 0;
            int factor = 1;
            if(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)){
                factor = 10;
            }
            if (e.Delta < 0)
                offset = 1 * factor;
            else if (e.Delta > 0)
                offset = -1 * factor;
            ChangeIllustPage(offset);
        }

        private void Preview_KeyUp(object sender, KeyEventArgs e)
        {
            int offset = 0;
            int factor = 1;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                factor = 10;
            }
            if (e.Key == Key.Right || e.Key == Key.Down || e.Key == Key.PageDown)
                offset = 1 * factor;
            else if (e.Key == Key.Left || e.Key == Key.Up || e.Key == Key.PageUp)
                offset = -1 * factor;
            else if (e.Key == Key.Home)
                offset = -10000;
            else if (e.Key == Key.End)
                offset = 10000;
            else if (e.Key == Key.S && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                SaveIllust();

            ChangeIllustPage(offset);
        }

        private void Page_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            int offset = 0;
            int factor = 1;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                factor = 10;
            }
            if (e.Key == Key.Right || e.Key == Key.Down || e.Key == Key.PageDown)
                offset = 1 * factor;
            else if (e.Key == Key.Left || e.Key == Key.Up || e.Key == Key.PageUp)
                offset = -1 * factor;
            else if (e.Key == Key.Home)
                offset = -10000;
            else if (e.Key == Key.End)
                offset = 10000;
            else if (e.Key == Key.S && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
                SaveIllust();

            ChangeIllustPage(offset);
        }
    }
}
