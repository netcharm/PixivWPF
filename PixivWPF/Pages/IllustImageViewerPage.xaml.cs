using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using MahApps.Metro.Controls;
using PixivWPF.Common;

namespace PixivWPF.Pages
{
    /// <summary>
    /// IllustImageViewerPage.xaml 的交互逻辑
    /// </summary>
    public partial class IllustImageViewerPage : Page, IDisposable
    {
        public Window ParentWindow { get; private set; }
        public PixivItem Contents { get; set; } = null;

        private double LastZoomRatio = 1.0;
        private bool ActionZoomFitOp { get; set; } = false;

        private string PreviewImageUrl = string.Empty;
        private string OriginalImageUrl = string.Empty;
        private Func<bool> GetOriginalCheckState = null;
        private Func<bool> GetFullSizeCheckState = null;
        private bool IsOriginal
        {
            get
            {
                if (GetOriginalCheckState == null) GetOriginalCheckState = new Func<bool>(() => { return (btnViewOriginalPage.IsChecked ?? false); });
                //return (GetOriginalCheckState.Invoke());
                return (this.Invoke(GetOriginalCheckState));
            }
        }
        private bool IsFullSize
        {
            get
            {
                if (GetFullSizeCheckState == null) GetFullSizeCheckState = new Func<bool>(() => { return (btnViewFullSize.IsChecked ?? false); });
                //return (GetFullSizeCheckState.Invoke());
                return (this.Invoke(GetFullSizeCheckState));
            }
        }
        public CustomImageSource PreviewImage { get; private set; } = new CustomImageSource();

        internal void UpdateTheme()
        {
            btnViewPrevPage.Enable(btnViewPrevPage.IsEnabled, btnViewPrevPage.IsVisible);
            btnViewNextPage.Enable(btnViewNextPage.IsEnabled, btnViewNextPage.IsVisible);

            btnViewOriginalPage.Enable(btnViewOriginalPage.IsEnabled, btnViewOriginalPage.IsVisible);
            btnViewFullSize.Enable(btnViewFullSize.IsEnabled, btnViewFullSize.IsVisible);
            btnOpenCache.Enable(btnOpenCache.IsEnabled, btnOpenCache.IsVisible);
            btnSavePage.Enable(btnViewNextPage.IsEnabled, btnSavePage.IsVisible);
        }

        public void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            try
            {
                if (Contents.IsWork() && (Contents.Illust.Id == illustid || illustid == -1))
                {
                    var tooltip = InfoBar.ToolTip is string ? (string)InfoBar.ToolTip : string.Empty;
                    if (!string.IsNullOrEmpty(tooltip))
                    {
                        var tips = tooltip.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        for (var i = 0; i < tips.Length; i++)
                        {
                            if (tips[i].StartsWith("Downloaded")) tips[i] = $"Downloaded    = {Contents.IsDownloaded}";
                            else continue;
                        }
                        InfoBar.ToolTip = string.Join(Environment.NewLine, tips);
                        StatusBar.ToolTip = InfoBar.ToolTip;
                    }
                    StatusDownloaded.Show(show: Contents.IsDownloaded);
                }
            }
            catch (Exception ex) { ex.ERROR("DOWNLOADSTATE"); }
        }

        public async void UpdateDownloadStateAsync(int? illustid = null, bool? exists = null)
        {
            await new Action(() =>
            {
                UpdateDownloadState(illustid, exists);
            }).InvokeAsync();
        }

        public void UpdateLikeState(int illustid = -1, bool is_user = false)
        {
            try
            {
                if (Contents.HasUser())
                {
                    var tooltip = InfoBar.ToolTip is string ? (string)InfoBar.ToolTip : string.Empty;
                    if (!string.IsNullOrEmpty(tooltip))
                    {
                        var tips = tooltip.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                        for (var i = 0; i < tips.Length; i++)
                        {
                            if (tips[i].StartsWith("Artwork Liked")) tips[i] = $"Artwork Liked = {Contents.IsFavorited}";
                            else if (tips[i].StartsWith("Artist Liked")) tips[i] = $"Artist Liked  = {Contents.IsFollowed}";
                            else continue;
                        }
                        InfoBar.ToolTip = string.Join(Environment.NewLine, tips);
                        StatusBar.ToolTip = InfoBar.ToolTip;
                    }
                    StatusFollowed.Show(show: Contents.IsFollowed);
                    StatusFaorited.Show(show: Contents.IsFavorited);
                }
            }
            catch (Exception ex) { ex.ERROR("LIKESTATE"); }
        }

        public async void UpdateLikeStateAsync(int illustid = -1, bool is_user = false)
        {
            await new Action(() =>
            {
                UpdateLikeState(illustid, is_user);
            }).InvokeAsync();
        }

        private void ChangeIllustPage(int offset)
        {
            if (Contents.IsWork())
            {
                int factor = Keyboard.Modifiers == ModifierKeys.Shift ? 10 : 1;

                var illust = Contents.Illust;
                int index_p = Contents.Index;
                if (index_p < 0) index_p = 0;
                var index_n = Contents.Index + (offset * factor);
                if (index_n < 0) index_n = 0;
                if (index_n >= Contents.Count - 1) index_n = Contents.Count - 1;
                if (index_n == index_p) return;

                var i = illust.WorkItem();
                if (i.IsWork())
                {
                    i.IsFavorited = illust.IsLiked();
                    i.IsFollowed = illust.User.IsLiked();
                    i.IsDownloaded = illust.IsDownloaded(index_n);
                    i.NextURL = Contents.NextURL;
                    i.Thumb = illust.GetThumbnailUrl(index_n);
                    i.Index = index_n;
                    i.BadgeValue = (index_n + 1).ToString();
                    i.Subject = $"{illust.Title} - {index_n + 1}/{illust.PageCount}";
                    i.DisplayTitle = false;
                    i.Tag = Contents.Tag;
                }
                UpdateDetail(i);
            }
        }

        private const int down_rate_mv = 3;
        private TimeSpan down_totalelapsed = TimeSpan.FromSeconds(0);
        private TimeSpan down_lastelapsed = TimeSpan.FromSeconds(0);
        private DateTime down_last_report = DateTime.Now;
        private DateTime down_start = DateTime.Now;
        private double down_last_received = 0;
        private Queue<double> down_rate = new Queue<double>(down_rate_mv);
        private Action<double, double> reportProgress = null;
        private CancellationTokenSource cancelDownloading = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        private bool Rotated
        {
            get
            {
                var degrees = btnViewFullSize.IsChecked.Value ? ImageViewerRotate.Angle : ImageRotate.Angle;
                return (degrees % 180 != 0 ? true : false);
            }
        }

        private bool IsRotated()
        {
            return (Rotated);
        }

        private string GetFileName()
        {
            //var original = this.Invoke(GetOriginalCheckState);
            return (string.IsNullOrEmpty(IsOriginal ? OriginalImageUrl : PreviewImageUrl) ? string.Empty : $"{"File Name".PadRight(12, ' ')}: {System.IO.Path.GetFileName(IsOriginal ? OriginalImageUrl : PreviewImageUrl)}");
        }

        private void InitProgressAction()
        {
            var setting = Application.Current.LoadSetting();

            if (reportProgress == null)
            {
                // no progress ring display in below action
                reportProgress = (received, length) =>
                {
                    var down_now = DateTime.Now;
                    if (received == 0 && length > 0) { down_last_report = down_now; }
                    down_lastelapsed = down_now - down_last_report;
                    down_totalelapsed = down_now - down_start;
                    var diff_t = down_lastelapsed.TotalSeconds;
                    if (diff_t >= 0.1)
                    {
                        var diff_b = Math.Max(0, received - down_last_received);
                        var rate_n = diff_b / diff_t ;
                        if (diff_b > 0) down_rate.Enqueue(rate_n);
                        if (down_rate.Count > down_rate_mv) down_rate.Dequeue();
                        down_last_received = received;
                        down_last_report = down_now;
                        //System.Diagnostics.Debug.WriteLine($"{diff_b} b, {diff_t} s, {rate_n} b/s");
                    }
                    var rate_c = down_rate.Count > 0 ? down_rate.Average(o => double.IsNaN(o) || o < 0 ? 0 : o) : 0;
                    var rate_a = received / down_totalelapsed.TotalSeconds;
                    var rate_cs = rate_c.SmartSpeedRate();
                    var rate_as = rate_a.SmartSpeedRate();
                    if (rate_cs.Length > rate_as.Length) rate_as = rate_a.SmartSpeedRate(padleft: rate_cs.Length);
                    else if (rate_cs.Length < rate_as.Length) rate_cs = rate_c.SmartSpeedRate(padleft: rate_as.Length);
                    var speed = $"Speed (rts.): {rate_cs}{Environment.NewLine}Speed (avg.): {rate_as}";
                    var elapsed = $"Elapsed Time: {down_totalelapsed.SmartElapsed()} s";

                    var percent = length <= 0 ? 0 : received / length * 100;
                    var state = received == length ? TaskStatus.RanToCompletion : received < length ? TaskStatus.Running : TaskStatus.Faulted;
                    var state_info = "Idle";
                    if (state == TaskStatus.Running) state_info = "Downloading";
                    else if (state == TaskStatus.RanToCompletion || received >= length) state_info = "Finished";
                    else state_info = "Failed";
                    var info = $"{state_info.PadRight(12, ' ')}: {received} B / {length} B, {received.SmartFileSize(trimzero: false)} / {length.SmartFileSize(trimzero: false)}";
                    var filename = GetFileName();
                    var tooltip = string.Join(Environment.NewLine, new string[] { info, speed, elapsed, filename }).Trim();

                    PreviewWait.PercentageTooltip = setting.ShowPreviewProgressTooltip ? tooltip : null;

                    if (ParentWindow is ContentWindow) (ParentWindow as ContentWindow).SetPrefetchingProgress(percent, tooltip, state);
                    //if (PreviewWait.ReportPercentage is Action<double, double>) PreviewWait.ReportPercentage.Invoke(received, length);
                    if (PreviewWait.ReportPercentageSlim is Action<double>) PreviewWait.ReportPercentageSlim.Invoke(percent);
                };
            }
        }

        private async Task<CustomImageSource> GetPreviewImage(bool overwrite = false)
        {
            CustomImageSource img = new CustomImageSource();

            try
            {
                var setting = Application.Current.LoadSetting();

                if (!(cancelDownloading is CancellationTokenSource) || cancelDownloading.IsCancellationRequested)
                    cancelDownloading = new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout));

                down_rate.Clear();
                down_rate.Enqueue(0);
                down_totalelapsed = TimeSpan.FromSeconds(0);
                down_lastelapsed = TimeSpan.FromSeconds(0);
                down_last_received = 0;
                down_last_report = DateTime.Now;
                down_start = DateTime.Now;

                InitProgressAction();

                PreviewWait.Show();

                if (reportProgress is Action<double, double>) reportProgress.Invoke(0, 0);
                var c_item = Contents;
                if (IsOriginal)
                {
                    using (var original = await OriginalImageUrl.LoadImageFromUrl(overwrite, progressAction: reportProgress, cancelToken: cancelDownloading))
                    {
                        if (original.Source != null && !string.IsNullOrEmpty(original.SourcePath))
                        {
                            img.ColorDepth = original.ColorDepth;
                            img.Size = original.Size;
                            img.Source = original.Source;
                            img.SourcePath = original.SourcePath;
                        }
                    }
                }
                else
                {
                    using (var preview = await PreviewImageUrl.LoadImageFromUrl(overwrite, progressAction: reportProgress, cancelToken: cancelDownloading))
                    {
                        if (setting.SmartPreview &&
                            (preview.Source == null ||
                             preview.Source.Width < setting.PreviewUsingLargeMinWidth ||
                             preview.Source.Height < setting.PreviewUsingLargeMinHeight))
                        {
                            using (var original = await OriginalImageUrl.LoadImageFromUrl(progressAction: reportProgress, cancelToken: cancelDownloading))
                            {
                                if (original.Source != null && !string.IsNullOrEmpty(original.SourcePath))
                                {
                                    img.ColorDepth = original.ColorDepth;
                                    img.Size = original.Size;
                                    img.Source = original.Source;
                                    img.SourcePath = original.SourcePath;
                                }
                            }
                        }
                        else
                        {
                            img.ColorDepth = preview.ColorDepth;
                            img.Size = preview.Size;
                            img.Source = preview.Source;
                            if (!string.IsNullOrEmpty(preview.SourcePath)) img.SourcePath = preview.SourcePath;
                        }
                    }
                }

                if (c_item.IsSameIllust(Contents))
                {
                    if (img.Source != null)
                    {
                        if (img.Size == 0 && !string.IsNullOrEmpty(img.SourcePath)) img.Size = new FileInfo(img.SourcePath).Length;
                        if (img.ColorDepth == 0 && img.Source != null) img.ColorDepth = 32;

                        if (reportProgress is Action<double, double>) reportProgress.Invoke(img.Size, img.Size);
                        Preview.Source = img.Source;
                        var dpiX = DPI.Default.X;
                        var dpiY = DPI.Default.Y;
                        var width = img.Source is BitmapSource ? (img.Source as BitmapSource).PixelWidth : img.Source.Width;
                        var height = img.Source is BitmapSource ? (img.Source as BitmapSource).PixelHeight : img.Source.Height;
                        var aspect = Preview.Source.AspectRatio();
                        if (!setting.AutoConvertDPI && img.Source is BitmapSource)
                        {
                            var bmp = img.Source as BitmapSource;
                            width = bmp.PixelWidth;
                            height = bmp.PixelHeight;
                            dpiX = bmp.DpiX;
                            dpiY = bmp.DpiY;
                        }
                        PreviewSize.Text = $"{width:F0}x{height:F0}";
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"Artwork Liked = {Contents.IsFavorited}");
                        sb.AppendLine($"Artist Liked  = {Contents.IsFollowed}");
                        if (Contents.Index >= 0) sb.AppendLine($"Page Index    = {Contents.Index + 1} / {Contents.Count}");
                        sb.AppendLine($"Downloaded    = {Contents.IsDownloaded}");
                        sb.AppendLine($"Dimension     = {width:F0} x {height:F0}");
                        sb.AppendLine($"Aspect Rate   = {aspect.Item1:G5} : {aspect.Item2:G5}");
                        sb.AppendLine($"Resolution    = {dpiX:F0}DPI : {dpiY:F0}DPI");
                        sb.AppendLine($"Memory Usage  = {(width * height * img.ColorDepth / 8).SmartFileSize()}");
                        sb.AppendLine($"File Size     = {img.Size.SmartFileSize()}");
                        sb.AppendLine($"File Name     = {Path.GetFileName(img.SourcePath)}");
                        sb.AppendLine($"Original      = {Contents.Illust.GetOriginalUrl(Contents.Index).GetImageName(Contents.Count <= 1)} [{(await Contents.Illust.GetOriginalUrl(Contents.Index).QueryImageFileSize() ?? -1).SmartFileSize()}]");
                        InfoBar.ToolTip = sb.ToString().Trim();
                        StatusBar.ToolTip = InfoBar.ToolTip;

                        Page_SizeChanged(null, null);
                        PreviewWait.Hide();
                    }
                    else PreviewWait.Fail();
                }
                StatusFollowed.Show(show: Contents.IsFollowed);
                StatusFaorited.Show(show: Contents.IsFavorited);
                StatusDownloaded.Show(show: Contents.IsDownloaded);
            }
            catch (Exception ex) { ex.ERROR(System.Reflection.MethodBase.GetCurrentMethod().Name); }
            finally
            {
                if (reportProgress is Action<double, double> && img.Source != null) reportProgress.Invoke(img.Size, img.Size);
                img.Source = null;
                if (Preview.Source == null) PreviewWait.Fail();
            }
            return (img);
        }

        internal async void UpdateDetail(PixivItem item, bool overwrite = false)
        {
            try
            {
                if (item.IsWork())
                {
                    Contents = item;
                    var illust = Contents.Illust as Pixeez.Objects.Work;

                    PreviewImageUrl = illust.GetPreviewUrl(Contents.Index, true);
                    OriginalImageUrl = illust.GetOriginalUrl(Contents.Index);

                    if (illust.PageCount > 1)
                    {
                        PreviewBadge.Show();
                        PreviewBadge.Badge = $"{Contents.Index + 1} / {Contents.Count}";

                        ActionViewPrevPage.Show();
                        ActionViewNextPage.Show();
                        ActionViewPageSep.Show();

                        btnViewPrevPage.Enable(Contents.Index > 0);
                        btnViewNextPage.Enable(Contents.Index < Contents.Count - 1);
                    }
                    else
                    {
                        PreviewBadge.Hide();

                        btnViewNextPage.Hide();
                        btnViewPrevPage.Hide();

                        ActionViewPrevPage.Hide();
                        ActionViewNextPage.Hide();
                        ActionViewPageSep.Hide();
                    }

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"Favorited  = {Contents.Illust.IsLiked()}");
                    sb.AppendLine($"Page Index = {Contents.Index + 1} / {Contents.Count}");
                    sb.AppendLine($"Downloaded = {Contents.Illust.IsDownloaded(Contents.Index)}");
                    InfoBar.ToolTip = sb.ToString().Trim();
                    StatusBar.ToolTip = InfoBar.ToolTip;

                    PreviewImage = await GetPreviewImage(overwrite);

                    if (ParentWindow == null) ParentWindow = Window.GetWindow(this);
                    if (ParentWindow is ContentWindow)
                    {
                        Title = $"Preview ID: {Contents.ID}, {Contents.Subject}";
                        ParentWindow.Title = Title;
                        Application.Current.UpdateContentWindows(ParentWindow as ContentWindow);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("UpdatePreview"); }
            finally { Preview.Focus(); }
        }

        #region Common Actions
        public void ChangeIllustLikeState()
        {
            try
            {
                if (Contents.IsWork())
                {
                    Commands.ChangeIllustLikeState.Execute(Contents);
                }
            }
            catch (Exception ex) { ex.ERROR("ChangeIllustLikeState"); }
        }

        public void ChangeUserLikeState()
        {
            try
            {
                if (Contents.IsWork())
                {
                    Commands.ChangeUserLikeState.Execute(Contents);
                }
            }
            catch (Exception ex) { ex.ERROR("ChangeUserLikeState"); }
        }

        public void OpenUser()
        {
            try
            {
                if (Contents.IsWork())
                {
                    Commands.OpenUser.Execute(Contents);
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public void OpenIllust()
        {
            try
            {
                if (Contents.IsWork())
                {
                    if (Contents.IsDownloaded)
                        Commands.OpenDownloaded.Execute(Contents);
                    else
                        OpenCachedImage();
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public void OpenCachedImage()
        {
            try
            {
                if (Contents.IsWork())
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                        Commands.OpenCachedImage.Execute(IsOriginal ? OriginalImageUrl.GetImageCachePath() : PreviewImageUrl.GetImageCachePath());
                    else
                        Commands.OpenCachedImage.Execute(PreviewImage);
                }
            }
            catch (Exception ex) { ex.ERROR("OpenCachedImage"); }
        }

        public void OpenImageProperties()
        {
            try
            {
                if (Contents.IsWork())
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                        Commands.OpenFileProperties.Execute(IsOriginal ? OriginalImageUrl.GetImageCachePath() : PreviewImageUrl.GetImageCachePath());
                    else
                        Commands.OpenFileProperties.Execute(Contents);
                }
            }
            catch (Exception ex) { ex.ERROR("OpenImageProperties"); }
        }

        public void SaveIllust()
        {
            try
            {
                if (Contents.IsWork()) Commands.SaveIllust.Execute(Contents);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public void SaveIllustAll()
        {
            try
            {

                if (Contents.IsWork()) Commands.SaveIllustAll.Execute(Contents);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        internal void SaveUgoira()
        {
            //throw new NotImplementedException();
        }

        public void CopyPreview(bool loadfromfile = false)
        {
            if (!string.IsNullOrEmpty(PreviewImageUrl))
            {
                if (loadfromfile || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                    Commands.CopyImage.Execute(IsOriginal ? OriginalImageUrl.GetImageCachePath() : PreviewImageUrl.GetImageCachePath());
                else
                    Commands.CopyImage.Execute(PreviewImage);
            }
        }

        public void FirstIllust()
        {
            if (InSearching) return;
            ChangeIllustPage(-10000);
        }

        public void LastIllust()
        {
            if (InSearching) return;
            ChangeIllustPage(10000);
        }

        public void PrevIllust()
        {
            if (InSearching) return;
            ChangeIllustPage(-1);
        }

        public void NextIllust()
        {
            if (InSearching) return;
            ChangeIllustPage(1);
        }

        public bool IsFirstPage
        {
            get
            {
                return (Contents is PixivItem && Contents.Index == 0);
            }
        }

        public bool IsLastPage
        {
            get
            {
                return (Contents is PixivItem && Contents.Index == Contents.Count - 1);
            }
        }

        public void PrevIllustPage()
        {
            if (InSearching) return;
            ChangeIllustPage(-1);
        }

        public void NextIllustPage()
        {
            if (InSearching) return;
            ChangeIllustPage(1);
        }
        #endregion

        public bool InSearching
        {
            get
            {
                if (ParentWindow is MainWindow)
                    return ((ParentWindow as MainWindow).InSearching);
                else if (ParentWindow is ContentWindow)
                    return ((ParentWindow as ContentWindow).InSearching);
                else return (false);
            }
        }

        public void StopPrefetching()
        {
            if (cancelDownloading is CancellationTokenSource) cancelDownloading.Cancel();
        }

        public void Dispose()
        {
            try
            {
                StopPrefetching();

                Preview.Dispose();
                if (PreviewImage is CustomImageSource) PreviewImage.Source = null;
                Contents.Source = null;
                DataContext = null;
            }
            catch (Exception ex) { ex.ERROR("DisposePreview"); }
            finally { }
        }

        public IllustImageViewerPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ActionZoomFitOp = true;
                ParentWindow = Window.GetWindow(this);
                if (ParentWindow is Window)
                {
                    ZoomBar.Hide();

                    #region ToolButton MouseOver action
                    btnViewPrevPage.MouseOverAction();
                    btnViewNextPage.MouseOverAction();
                    btnViewOriginalPage.MouseOverAction();
                    btnViewFullSize.MouseOverAction();
                    btnOpenIllust.MouseOverAction();
                    btnOpenCache.MouseOverAction();
                    btnSavePage.MouseOverAction();

                    btnZoomFitHeight.MouseOverAction();
                    btnZoomFitWidth.MouseOverAction();

                    btnViewActionMore.MouseOverAction();
                    btnViewerActionFlipH.MouseOverAction();
                    btnViewerActionFlipV.MouseOverAction();
                    btnViewerActionRotate90L.MouseOverAction();
                    btnViewerActionRotate90R.MouseOverAction();
                    btnViewerActionReset.MouseOverAction();
                    #endregion

                    //PreviewWait.ReloadEnabled = true;
                    //PreviewWait.ReloadAction = new Action(() => {
                    //    UpdateDetail(Contents, Keyboard.Modifiers == ModifierKeys.Alt || Keyboard.Modifiers == ModifierKeys.Control);
                    //});

                    InitProgressAction();

                    var titleheight = ParentWindow is MetroWindow ? (ParentWindow as MetroWindow).TitleBarHeight : 0;
                    ParentWindow.Width += ParentWindow.BorderThickness.Left + ParentWindow.BorderThickness.Right;
                    ParentWindow.Height -= ParentWindow.BorderThickness.Top + ParentWindow.BorderThickness.Bottom + (32 - titleheight % 32);

                    if (Contents is PixivItem) UpdateDetail(Contents);
                }
            }
            catch (Exception ex) { ex.ERROR("ViewerLoaded"); }
            finally { ActionZoomFitOp = false; }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (Preview.Source is ImageSource && Preview.Source.Width > 0 && Preview.Source.Height > 0)
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
            }
            catch (Exception ex) { ex.ERROR("PageSizeChanged"); }
        }

        private void Preview_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            if (ViewerActionMore.IsOpen) return;
            ChangeIllustPage(-Math.Sign(e.Delta));
        }

        private Point start;
        private Point origin;

        private void Preview_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
            if (e.Device is MouseDevice)
            {
                if (sender == PreviewBox && e.ChangedButton == MouseButton.Left)
                {
                    if (e.ClickCount >= 2)
                    {
                        ActionViewOriginal_Click(ActionViewOriginal, e);
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
                        ChangeIllustPage(1);
                        e.Handled = true;
                    }
                }
                else if (e.ChangedButton == MouseButton.XButton2)
                {
                    if (e.ClickCount == 1)
                    {
                        ChangeIllustPage(-1);
                        e.Handled = true;
                    }
                }
            }
        }

        private void Preview_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender == PreviewBox && e.LeftButton == MouseButtonState.Pressed)
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

        private void ActionIllustInfo_Click(object sender, RoutedEventArgs e)
        {
            if (Contents is PixivItem)
            {
                if (sender == ActionCopyIllustID)
                    Commands.CopyArtworkIDs.Execute(Contents);
                else if (sender == ActionOpenIllust || sender == btnOpenIllust)
                    Commands.Open.Execute(Contents.Illust);
                else if (sender == ActionOpenAuthor)
                    Commands.OpenUser.Execute(Contents.User);
                else if (sender == ActionOpenCachedWith || sender == btnOpenCache)
                {
                    OpenCachedImage();
                }
                else if (sender == ActionCopyPreview)
                {
                    CopyPreview();
                }
                else if (sender == ActionSendIllustToInstance)
                {
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        Commands.SendToOtherInstance.Execute(Contents);
                    else
                        Commands.ShellSendToOtherInstance.Execute(Contents);
                }
                else if (sender == ActionSendAuthorToInstance)
                {
                    var id = $"uid:{Contents.UserID}";
                    if (Keyboard.Modifiers == ModifierKeys.None)
                        Commands.SendToOtherInstance.Execute(id);
                    else
                        Commands.ShellSendToOtherInstance.Execute(id);
                }
                else if (sender == ActionRefreshPreview || sender == PreviewWait)
                {
                    UpdateDetail(Contents, Keyboard.Modifiers == ModifierKeys.Alt || Keyboard.Modifiers == ModifierKeys.Control);
                }
                else if (sender == ActionOpenDownloaded)
                {
                    Commands.OpenDownloaded.Execute(Contents);
                }
                else if (sender == ActionOpenDownloadedProperties)
                {
                    Commands.OpenFileProperties.Execute(Contents);
                }
            }
        }

        private void ActionViewPrevPage_Click(object sender, RoutedEventArgs e)
        {
            PrevIllustPage();
        }

        private void ActionViewNextPage_Click(object sender, RoutedEventArgs e)
        {
            NextIllustPage();
        }

        private void ActionSaveIllust_Click(object sender, RoutedEventArgs e)
        {
            SaveIllust();
        }

        private void ActionViewFullSize_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ActionZoomFitOp = true;
                if (sender == ActionViewFullSize)
                {
                    btnViewFullSize.IsChecked = !btnViewFullSize.IsChecked.Value;
                    CommonHelper.MouseLeave(btnViewFullSize);
                }

                ZoomBar.Show(show: btnViewFullSize.IsChecked.Value);

                if (btnViewFullSize.IsChecked.Value)
                {
                    ImageViewerRotate.Angle = ImageRotate.Angle;
                    ImageViewerScale.ScaleX = ImageScale.ScaleX;
                    ImageViewerScale.ScaleY = ImageScale.ScaleY;

                    ImageScale.ScaleX = 1;
                    ImageScale.ScaleY = 1;
                    ImageRotate.Angle = 0;

                    PreviewBox.HorizontalAlignment = HorizontalAlignment.Center;
                    PreviewBox.VerticalAlignment = VerticalAlignment.Center;
                    PreviewBox.Stretch = Stretch.None;
                    PreviewBox.StretchDirection = StretchDirection.Both;
                    PreviewScroll.PanningMode = PanningMode.Both;
                    PreviewScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                    PreviewScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    InfoBar.Margin = new Thickness(16, 16, 16, 32);
                    StatusBar.Margin = new Thickness(16, 16, 16, 52);
                    ActionBar.Margin = new Thickness(0, 0, 16, 16);
                    ZoomRatio.Value = LastZoomRatio;
                }
                else
                {
                    ImageRotate.Angle = ImageViewerRotate.Angle;
                    ImageScale.ScaleX = ImageViewerScale.ScaleX;
                    ImageScale.ScaleY = ImageViewerScale.ScaleY;

                    ImageViewerScale.ScaleX = 1;
                    ImageViewerScale.ScaleY = 1;
                    ImageViewerRotate.Angle = 0;

                    LastZoomRatio = ZoomRatio.Value;
                    PreviewBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                    PreviewBox.VerticalAlignment = VerticalAlignment.Stretch;
                    PreviewBox.Stretch = Stretch.Uniform;
                    PreviewBox.StretchDirection = StretchDirection.DownOnly;
                    PreviewScroll.PanningMode = PanningMode.None;
                    PreviewScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
                    PreviewScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                    InfoBar.Margin = new Thickness(16);
                    StatusBar.Margin = new Thickness(16, 16, 16, 36);
                    ActionBar.Margin = new Thickness(0);
                    ZoomRatio.Value = 1.0;
                }
                //ActionViewFullSize.IsChecked = IsFullSize;
                Page_SizeChanged(null, null);
            }
            catch (Exception ex) { ex.ERROR("ViewFullSize"); }
            finally { ActionZoomFitOp = false; }
        }

        private async void ActionViewOriginal_Click(object sender, RoutedEventArgs e)
        {
            if (Contents.IsWork())
            {
                try
                {
                    if (sender == ActionViewOriginal)
                    {
                        btnViewOriginalPage.IsChecked = !btnViewOriginalPage.IsChecked.Value;
                        CommonHelper.MouseLeave(btnViewOriginalPage);
                    }
                    //ActionViewOriginal.IsChecked = IsOriginal;
                    PreviewImage = await GetPreviewImage();
                }
                catch (Exception ex) { ex.ERROR("ViewOriginal"); }
            }
        }

        private void ActionZoomFit_Click(object sender, RoutedEventArgs e)
        {
            if (Preview.Source != null)
            {
                new Action(() =>
                {
                    try
                    {
                        ActionZoomFitOp = true;

                        bool IsRotated = Rotated;

                        var targetX = IsRotated ?  Preview.Source.Height : Preview.Source.Width;
                        var targetY = IsRotated ?  Preview.Source.Width : Preview.Source.Height;

                        if (sender == btnZoomFitWidth)
                        {
                            var ratio = PreviewScroll.ActualWidth / targetX;
                            var delta = PreviewScroll.VerticalScrollBarVisibility == ScrollBarVisibility.Hidden || targetY * ratio <= PreviewScroll.ActualHeight ? 0 : 14;
                            ZoomRatio.Value = (PreviewScroll.ActualWidth - delta) / targetX;
                        }
                        else if (sender == btnZoomFitHeight)
                        {
                            var ratio = PreviewScroll.ActualHeight / targetY;
                            var delta = PreviewScroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Hidden || targetX * ratio <= PreviewScroll.ActualWidth ? 0 : 14;
                            ZoomRatio.Value = (PreviewScroll.ActualHeight - delta) / targetY;
                        }
                        if (btnViewFullSize.IsChecked.Value)
                        {
                            ImageViewerScale.ScaleX = ImageViewerScale.ScaleX >= 0 ? ZoomRatio.Value : -ZoomRatio.Value;
                            ImageViewerScale.ScaleY = ImageViewerScale.ScaleY >= 0 ? ZoomRatio.Value : -ZoomRatio.Value;
                        }
                        var offsetX = (PreviewScroll.ExtentWidth - PreviewScroll.ViewportWidth) / 2;
                        var offsetY = (PreviewScroll.ExtentHeight - PreviewScroll.ViewportHeight) / 2;
                        PreviewScroll.ScrollToHorizontalOffset(offsetX >= 0 ? offsetX : 0);
                        PreviewScroll.ScrollToVerticalOffset(offsetY >= 0 ? offsetY : 0);
                    }
                    catch (Exception ex) { ex.ERROR("ZoomFit"); }
                    finally { ActionZoomFitOp = false; }
                }).Invoke();
            }
        }

        private void ActionZoomRatio_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded && !ActionZoomFitOp)
            {
                var delta = e.NewValue - e.OldValue;
                if (Math.Abs(delta) >= 0.25) //ZoomRatio.LargeChange)
                {
                    new Action(() =>
                    {
                        try
                        {
                            ActionZoomFitOp = true;
                            var eq = Math.Round(e.NewValue);
                            if (delta > 0)
                            {
                                if (e.OldValue >= 0.25 && e.NewValue < 1.5) eq = 0.5;
                                else if (e.OldValue >= 0.5 && e.NewValue < 2.0) eq = 1;
                            }
                            else if (delta < 0)
                            {
                                if (e.OldValue >= 1.0 && e.NewValue < 1.0) eq = 0.5;
                                else if (e.OldValue >= 0.5 && e.NewValue < 0.5) eq = 0.25;
                            }
                            if (e.NewValue != eq) ZoomRatio.Value = eq;
                        }
                        catch (Exception ex) { ex.ERROR("ZoomRatioChanged"); }
                        finally { e.Handled = true; ActionZoomFitOp = false; }
                    }).Invoke();
                }
                if (btnViewFullSize.IsChecked.Value)
                {
                    ImageViewerScale.ScaleX = ImageViewerScale.ScaleX >= 0 ? ZoomRatio.Value : -ZoomRatio.Value;
                    ImageViewerScale.ScaleY = ImageViewerScale.ScaleY >= 0 ? ZoomRatio.Value : -ZoomRatio.Value;
                }
            }
        }

        private DispatcherTimer _popupTimer = null;
        private void ActionMore_Click(object sender, RoutedEventArgs e)
        {
            ViewerActionMore.PlacementTarget = btnViewActionMore;
            ViewerActionMore.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
            ViewerActionMore.IsOpen = true;

            if (_popupTimer == null)
            {
                _popupTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(5) };
                _popupTimer.Tick += (obj, evt) =>
                {
                    (_popupTimer as DispatcherTimer).Stop();
                    if (ViewerActionMore.IsOpen) ViewerActionMore.IsOpen = false;
                };
            }
            _popupTimer.Start();
        }

        private void ActionMoreOp_Click(object sender, RoutedEventArgs e)
        {
            double tsX = 1, tsY = 1, rs = 0;

            if (_popupTimer is DispatcherTimer)
            {
                _popupTimer.Stop();
                _popupTimer.Start();
            }

            bool IsRotated = Rotated;

            if (sender == btnViewerActionReset)
            {
                ImageScale.ScaleX = 1;
                ImageScale.ScaleY = 1;
                ImageRotate.Angle = 0;

                ImageViewerScale.ScaleX = 1;
                ImageViewerScale.ScaleY = 1;
                ImageViewerRotate.Angle = 0;
            }
            else if (sender == btnViewerActionFlipH) { if (IsRotated) tsY = -1; else tsX = -1; }
            else if (sender == btnViewerActionFlipV) { if (IsRotated) tsX = -1; else tsY = -1; }
            else if (sender == btnViewerActionRotate90L) { rs = -90; }
            else if (sender == btnViewerActionRotate90R) { rs = 90; }

            if (btnViewFullSize.IsChecked.Value)
            {
                ImageScale.ScaleX = 1;
                ImageScale.ScaleY = 1;
                ImageRotate.Angle = 0;

                ImageViewerScale.ScaleX *= tsX;
                ImageViewerScale.ScaleY *= tsY;
                ImageViewerRotate.Angle += rs;
            }
            else
            {
                ImageScale.ScaleX *= tsX;
                ImageScale.ScaleY *= tsY;
                ImageRotate.Angle += rs;

                ImageViewerScale.ScaleX = 1;
                ImageViewerScale.ScaleY = 1;
                ImageViewerRotate.Angle = 0;
            }
        }

        /// <summary>
        /// source from : https://stackoverflow.com/a/24526749/1842521
        /// </summary>        
        private Point rel = new Point(0, 0);
        private double CalculateOffset(double extent, double viewPort, double scrollWidth, double relBefore)
        {
            //calculate the new offset
            double offset = Math.Max(0, relBefore * extent - 0.5 * viewPort);
            //see if it is negative because of initial values
            if (offset < 0)
            {
                //center the content
                //this can be set to 0 if center by default is not needed
                offset = 0.5 * scrollWidth;
            }
            return offset;
        }

        private void PreviewScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ScrollViewer scroll = PreviewScroll;
            //see if the content size is changed
            if (e.ExtentWidthChange != 0 || e.ExtentHeightChange != 0)
            {
                //calculate and set accordingly
                scroll.ScrollToHorizontalOffset(CalculateOffset(e.ExtentWidth, e.ViewportWidth, scroll.ScrollableWidth, rel.X));
                scroll.ScrollToVerticalOffset(CalculateOffset(e.ExtentHeight, e.ViewportHeight, scroll.ScrollableHeight, rel.Y));
            }
            else
            {
                //store the relative values if normal scroll
                rel.X = (e.HorizontalOffset + 0.5 * e.ViewportWidth) / e.ExtentWidth;
                rel.Y = (e.VerticalOffset + 0.5 * e.ViewportHeight) / e.ExtentHeight;
            }
        }
    }
}
