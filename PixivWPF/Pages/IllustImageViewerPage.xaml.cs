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
    /// IllustImageViewerPage.xaml 的交互逻辑
    /// </summary>
    public partial class IllustImageViewerPage : Page
    {
        private object DataType = null;
        private Window window = null;

        internal async void UpdateDetail(ImageItem item)
        {
            try
            {
                PreviewWait.Show();

                DataType = item;
                if (item.Illust is Pixeez.Objects.Work)
                {
                    var illust = item.Illust as Pixeez.Objects.Work;

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

                    var img = await illust.GetPreviewUrl(item.Index, true).LoadImageFromUrl();
                    if (img == null || img.Width < 360)
                    {
                        var large = await item.Illust.GetOriginalUrl(item.Index).LoadImageFromUrl();
                        if (large != null) img = large;
                    }
                    Preview.Source = img;

                    if (Preview.Source != null)
                    {
                        var aspect = Preview.Source.AspectRatio();
                        PreviewSize.Text = $"{Preview.Source.Width:F0}x{Preview.Source.Height:F0}, {aspect.Item1:G5}:{aspect.Item2:G5}";
                        Page_SizeChanged(null, null);
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
                            window.Title = $"Preview ID: {item.ID}, {item.Subject}";
                        else
                            window.Title = $"Preview ID: {item.ID}, {item.Subject}";// - 1/1";
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

        private void ChangeIllustPage(int offset)
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

                var i = illust.IllustItem();
                if(i is ImageItem)
                {
                    i.NextURL = item.NextURL;
                    i.Thumb = illust.GetThumbnailUrl(index_n);
                    i.Index = index_n;
                    i.BadgeValue = (index_n + 1).ToString();
                    i.Subject = $"{illust.Title} - {index_n + 1}/{illust.PageCount}";
                    i.DisplayTitle = false;
                    i.Tag = item.Tag;
                }

                UpdateDetail(i);
            }
        }

        private void SaveIllust()
        {
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
                        try
                        {
                            var is_meta_single_page = illust.PageCount==1 ? true : false;
                            url.SaveImage(illust.GetThumbnailUrl(idx), dt, is_meta_single_page);
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

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (btnViewFullSize.IsChecked.Value)
            {
                PreviewBox.Width = Preview.Source.Width;
                PreviewBox.Height = Preview.Source.Height;
            }
            else
            {
                PreviewBox.Width = PreviewScroll.ActualWidth;
                PreviewBox.Height = PreviewScroll.ActualHeight;
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

        private Point start;
        private Point origin;

        private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
            if (e.Device is MouseDevice)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    if (e.ClickCount >= 2)
                    {
                        ActionViewOriginalPage_Click(sender, e);
                        e.Handled = true;
                    }
                    else
                    {
                        start = e.GetPosition(PreviewScroll);
                        origin = new Point(PreviewScroll.HorizontalOffset, PreviewScroll.VerticalOffset);
                    }
                }
                else if (e.ChangedButton == MouseButton.XButton1)
                {
                    if (e.ClickCount == 1)
                    {
                        Preview_MouseWheel(sender, new MouseWheelEventArgs(e.Device as MouseDevice, 0, -60));
                        e.Handled = true;
                    }
                }
                else if (e.ChangedButton == MouseButton.XButton2)
                {
                    if (e.ClickCount == 1)
                    {
                        Preview_MouseWheel(sender, new MouseWheelEventArgs(e.Device as MouseDevice, 0, 60));
                        e.Handled = true;
                    }
                }
            }
        }

        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (PreviewBox.Stretch == Stretch.None)
                {
                    Point factor = new Point(PreviewScroll.ExtentWidth/PreviewScroll.ActualWidth, PreviewScroll.ExtentHeight/PreviewScroll.ActualHeight);
                    Vector v = start - e.GetPosition(PreviewScroll);
                    PreviewScroll.ScrollToHorizontalOffset(origin.X + v.X * factor.X);
                    PreviewScroll.ScrollToVerticalOffset(origin.Y + v.Y * factor.Y);
                }
            }
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
            if(sender is ButtonBase)
            {
                var btn = sender as ButtonBase;
                btn.BorderThickness = new Thickness(2);
            }
        }

        private void btnAction_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is ButtonBase)
            {
                var btn = sender as ButtonBase;
                btn.BorderThickness = new Thickness(0);
            }
        }

        private void ActionIllustInfo_Click(object sender, RoutedEventArgs e)
        {
            if (DataType is ImageItem)
            {
                var item = DataType as ImageItem;
                if (sender == ActionCopyIllustID)
                    CommonHelper.Cmd_CopyIllustIDs.Execute(item);
                else if (sender == ActionOpenIllust)
                    CommonHelper.Cmd_OpenIllust.Execute(item.Illust);
                else if (sender == ActionOpenAuthor)
                    CommonHelper.Cmd_OpenUser.Execute(item.User);
            }
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

        private void ActionViewFullSize_Click(object sender, RoutedEventArgs e)
        {
            if (btnViewFullSize.IsChecked.Value)
            {
                PreviewBox.HorizontalAlignment = HorizontalAlignment.Center;
                PreviewBox.VerticalAlignment = VerticalAlignment.Center;
                PreviewBox.Stretch = Stretch.None;
                PreviewScroll.PanningMode = PanningMode.Both;
                PreviewScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                PreviewScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                InfoBar.Margin = new Thickness(16, 16, 16, 32);
                ActionBar.Margin = new Thickness(0, 0, 16, 16);
            }
            else
            {
                PreviewBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                PreviewBox.VerticalAlignment = VerticalAlignment.Stretch;
                PreviewBox.Stretch = Stretch.Uniform;
                PreviewScroll.PanningMode = PanningMode.None;
                PreviewScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
                PreviewScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                InfoBar.Margin = new Thickness(16);
                ActionBar.Margin = new Thickness(0);
            }
            Page_SizeChanged(null, null);
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
                    var large = await illust.GetOriginalUrl(item.Index).LoadImageFromUrl();
                    if (large != null) Preview.Source = large;
                    if (Preview.Source != null)
                    {
                        var aspect = Preview.Source.AspectRatio();
                        PreviewSize.Text = $"{Preview.Source.Width:F0}x{Preview.Source.Height:F0}, {aspect.Item1:G5}:{aspect.Item2:G5}";

                        Page_SizeChanged(null, null);
                    }
                }

                PreviewWait.Visibility = Visibility.Hidden;
            }
        }
    }
}
