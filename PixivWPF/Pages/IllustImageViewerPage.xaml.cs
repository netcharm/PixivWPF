using MahApps.Metro.Controls;
using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
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
                PreviewWait.Show();

                DataType = item;
                if (item.Illust is Pixeez.Objects.Work)
                {
                    var illust = item.Illust as Pixeez.Objects.Work;
                    var url = illust.GetPreviewUrl(item.Index);

                    if(illust.PageCount > 1)
                    {
                        btnViewNextPage.Visibility = Visibility.Visible;
                        btnViewPrevPage.Visibility = Visibility.Visible;
                        ActionViewPrevPage.Visibility = Visibility.Visible;
                        ActionViewNextPage.Visibility = Visibility.Visible;
                        ActionViewPageSep.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        btnViewNextPage.Visibility = Visibility.Collapsed;
                        btnViewPrevPage.Visibility = Visibility.Collapsed;
                        ActionViewPrevPage.Visibility = Visibility.Collapsed;
                        ActionViewNextPage.Visibility = Visibility.Collapsed;
                        ActionViewPageSep.Visibility = Visibility.Collapsed;
                    }

                    var tokens = await CommonHelper.ShowLogin();
                    Preview.Source = await url.LoadImage(tokens);
                    if (Preview.Source != null)
                    {
                        var aspect = Preview.Source.AspectRatio();
                        PreviewSize.Text = $"{Preview.Source.Width:F0}x{Preview.Source.Height:F0}, {aspect.Item1:G5}:{aspect.Item2:G5}";
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
                    else
                    {
                        if (Regex.IsMatch(item.Subject, @" - \d+\/\d+$", RegexOptions.IgnoreCase))
                            window.Title = $"ID: {item.ID}, {item.Subject}";
                        else
                            window.Title = $"ID: {item.ID}, {item.Subject} - 1/1";
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }
            finally
            {
                PreviewWait.Hide();
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
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            window = Window.GetWindow(this);
            if (window is Window)
            {
                var titleheight = window is MetroWindow ? (window as MetroWindow).TitlebarHeight : 0;
                window.Width += window.BorderThickness.Left + window.BorderThickness.Right;
                window.Height -= window.BorderThickness.Top + window.BorderThickness.Bottom + (32 - titleheight % 32);
            }
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
            {
                SaveIllust();
                return;
            }
            ChangeIllustPage(offset);
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

        private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //int offset = 0;
            //int factor = 1;
            //if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            //    factor = 10;

            //if (e.ChangedButton == MouseButton.XButton1)
            //    offset = -1 * factor;
            //else if (e.ChangedButton == MouseButton.XButton2)
            //    offset = 1 * factor;

            //ChangeIllustPage(offset);
            //e.Handled = true;
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
            {
                SaveIllust();
                return;
            }              
            ChangeIllustPage(offset);
        }

        private void btnAction_MouseEnter(object sender, MouseEventArgs e)
        {
            if(sender is Button)
            {
                var btn = sender as Button;
                btn.BorderThickness = new Thickness(2);
            }
        }

        private void btnAction_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Button)
            {
                var btn = sender as Button;
                btn.BorderThickness = new Thickness(0);
            }
        }

        private void ActionIllustInfo_Click(object sender, RoutedEventArgs e)
        {
            CommonHelper.Cmd_CopyIllustIDs.Execute(DataType);
        }

        private void ActionViewPrevPage_Click(object sender, RoutedEventArgs e)
        {
            int offset = 0;
            int factor = 1;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                factor = 10;
            }
            offset = -1 * factor;
            ChangeIllustPage(offset);
        }

        private void ActionViewNextPage_Click(object sender, RoutedEventArgs e)
        {
            int offset = 0;
            int factor = 1;
            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                factor = 10;
            }
            offset = 1 * factor;
            ChangeIllustPage(offset);
        }

        private void ActionSaveIllust_Click(object sender, RoutedEventArgs e)
        {
            SaveIllust();
        }

        private async void ActionViewOriginalPage_Click(object sender, RoutedEventArgs e)
        {
            if(DataType is ImageItem)
            {
                PreviewWait.Visibility = Visibility.Visible;

                var item = DataType as ImageItem;
                if (item.Illust is Pixeez.Objects.Work)
                {
                    var illust = item.Illust as Pixeez.Objects.Work;
                    var tokens = await CommonHelper.ShowLogin();
                    var large = await illust.GetOriginalUrl(item.Index).LoadImage(tokens);
                    if (large != null) Preview.Source = large;
                    if (Preview.Source != null)
                    {
                        var aspect = Preview.Source.AspectRatio();
                        PreviewSize.Text = $"{Preview.Source.Width:F0}x{Preview.Source.Height:F0}, {aspect.Item1:G5}:{aspect.Item2:G5}";
                    }
                }

                PreviewWait.Visibility = Visibility.Hidden;
            }
        }
    }
}
