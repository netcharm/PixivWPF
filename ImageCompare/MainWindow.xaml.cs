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

using ImageMagick;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Threading;

namespace ImageCompare
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string AppPath = System.IO.Path.GetDirectoryName(Application.ResourceAssembly.CodeBase.ToString()).Replace("file:\\", "");
        private static string CachePath =  System.IO.Path.Combine(AppPath, "cache");

        private MagickImage SourceImage { get; set; }
        private MagickImage TargetImage { get; set; }
        private MagickImage ResultImage { get; set; }

        private double ImageDistance { get; set; } = 0;
        private double LastZoomRatio { get; set; } = 1;

        private SemaphoreSlim _CanUpdate_ = new SemaphoreSlim(1, 1);

        private Point start;
        private Point origin;

        private void CalcDisplay(bool set_ratio = true)
        {
            var fit = ZoomFitAll.IsChecked ?? false;
            if (fit)
            {
                if (SourceImage is MagickImage)
                {
                    ImageSourceBox.Width = ImageSourceScroll.ActualWidth;
                    ImageSourceBox.Height = ImageSourceScroll.ActualHeight;
                }
                if (TargetImage is MagickImage)
                {
                    ImageTargetBox.Width = ImageTargetScroll.ActualWidth;
                    ImageTargetBox.Height = ImageTargetScroll.ActualHeight;
                }
                if (ResultImage is MagickImage)
                {
                    ImageResultBox.Width = ImageResultScroll.ActualWidth;
                    ImageResultBox.Height = ImageResultScroll.ActualHeight;
                }
                if (set_ratio)
                {
                    LastZoomRatio = ZoomRatio.Value;
                    ZoomRatio.Value = 1;
                }
            }
            else
            {
                if (SourceImage is MagickImage)
                {
                    ImageSourceBox.Width = SourceImage.Width;
                    ImageSourceBox.Height = SourceImage.Height;
                }
                if (TargetImage is MagickImage)
                {
                    ImageTargetBox.Width = TargetImage.Width;
                    ImageTargetBox.Height = TargetImage.Height;
                }
                if (ResultImage is MagickImage)
                {
                    ImageResultBox.Width = ResultImage.Width;
                    ImageResultBox.Height = ResultImage.Height;
                }
                ZoomRatio.Value = LastZoomRatio;
            }

            if (ZoomFitNone.IsChecked ?? false) ZoomRatio.IsEnabled = true;
            else ZoomRatio.IsEnabled = false;

            CalcZoomRatio();
        }

        private void CalcZoomRatio()
        {
            if (SourceImage is MagickImage && TargetImage is MagickImage)
            {
                if (ZoomFitAll.IsChecked ?? false)
                {
                    //ZoomRatio.Value = 1;
                }
                else if (ZoomFitNone.IsChecked ?? false)
                {
                    //ZoomRatio.Value = 1;
                }
                else if (ZoomFitWidth.IsChecked ?? false)
                {
                    var targetX = SourceImage.Width;
                    var targetY = SourceImage.Height;
                    var ratio = ImageSourceScroll.ActualWidth / targetX;
                    var delta = ImageSourceScroll.VerticalScrollBarVisibility == ScrollBarVisibility.Hidden || targetY * ratio <= ImageSourceScroll.ActualHeight ? 0 : 14;
                    ZoomRatio.Value = (ImageSourceScroll.ActualWidth - delta) / targetX;
                }
                else if (ZoomFitHeight.IsChecked ?? false)
                {
                    var targetX = SourceImage.Width;
                    var targetY = SourceImage.Height;
                    var ratio = ImageSourceScroll.ActualHeight / targetY;
                    var delta = ImageSourceScroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Hidden || targetX * ratio <= ImageSourceScroll.ActualWidth ? 0 : 14;
                    ZoomRatio.Value = (ImageSourceScroll.ActualHeight - delta) / targetY;
                }
            }
        }

        public async Task<MemoryStream> ToMemoryStream(BitmapSource bitmap, string fmt = "")
        {
            MemoryStream result = new MemoryStream();
            try
            {
                if (string.IsNullOrEmpty(fmt)) fmt = ".png";
                dynamic encoder = null;
                switch (fmt)
                {
                    case "image/bmp":
                    case "image/bitmap":
                    case "CF_BITMAP":
                    case "CF_DIB":
                    case ".bmp":
                        encoder = new BmpBitmapEncoder();
                        break;
                    case "image/gif":
                    case "gif":
                    case ".gif":
                        encoder = new GifBitmapEncoder();
                        break;
                    case "image/png":
                    case "png":
                    case ".png":
                        encoder = new PngBitmapEncoder();
                        break;
                    case "image/jpg":
                    case ".jpg":
                        encoder = new JpegBitmapEncoder();
                        break;
                    case "image/jpeg":
                    case ".jpeg":
                        encoder = new JpegBitmapEncoder();
                        break;
                    case "image/tif":
                    case ".tif":
                        encoder = new TiffBitmapEncoder();
                        break;
                    case "image/tiff":
                    case ".tiff":
                        encoder = new TiffBitmapEncoder();
                        break;
                    default:
                        encoder = new PngBitmapEncoder();
                        break;
                }
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(result);
                await result.FlushAsync();
            }
            catch { }
            return (result);
        }

        public async Task<byte[]> ToBytes(BitmapSource bitmap, string fmt = "")
        {
            if (string.IsNullOrEmpty(fmt)) fmt = ".png";
            return ((await ToMemoryStream(bitmap, fmt)).ToArray());
        }

        private async void UpdateImageViewer()
        {
            if (await _CanUpdate_.WaitAsync(TimeSpan.FromMilliseconds(10)))
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        if (SourceImage is MagickImage) ImageSource.Source = SourceImage.ToBitmapSource();
                        if (TargetImage is MagickImage) ImageTarget.Source = TargetImage.ToBitmapSource();
                        ImageResult.Source = null;
                        ResultImage = await Compare(SourceImage, TargetImage);
                        if (ResultImage is MagickImage) ImageResult.Source = ResultImage.ToBitmapSource();
                        CalcDisplay(set_ratio: false);
                    }
                    catch { }
                    finally { if (_CanUpdate_ is SemaphoreSlim && _CanUpdate_.CurrentCount < 1) _CanUpdate_.Release(); }
                }, System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private async void LoadImageFromFiles(string[] files, bool source = true)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    files = files.Where(f => !string.IsNullOrEmpty(f)).ToArray();
                    var count = files.Length;
                    if (count > 0)
                    {
                        var file_s = string.Empty;
                        var file_t = string.Empty;
                        if (count >= 2)
                        {
                            file_s = files.First();
                            file_t = files.Skip(1).First();
                            using (var fs = new FileStream(file_s, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                SourceImage = new MagickImage(fs);
                                ImageSource.Source = SourceImage.ToBitmapSource();
                            }
                            using (var fs = new FileStream(file_t, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                TargetImage = new MagickImage(fs);
                                ImageTarget.Source = TargetImage.ToBitmapSource();
                            }
                        }
                        else
                        {
                            if (source)
                            {
                                file_s = files.First();
                                using (var fs = new FileStream(file_s, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    SourceImage = new MagickImage(fs);
                                    ImageSource.Source = SourceImage.ToBitmapSource();
                                }
                            }
                            else
                            {
                                file_t = files.First();
                                using (var fs = new FileStream(file_t, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    TargetImage = new MagickImage(fs);
                                    ImageTarget.Source = TargetImage.ToBitmapSource();
                                }
                            }
                        }
                        UpdateImageViewer();
                    }
                }
                catch { }
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private void LoadImageFromFile(bool source = true)
        {
            var dlgOpen = new CommonOpenFileDialog() { Multiselect = true, EnsureFileExists = true, EnsurePathExists = true, EnsureValidNames = true };
            if (dlgOpen.ShowDialog() == CommonFileDialogResult.Ok)
            {
                var files = dlgOpen.FileNames.ToArray();
                LoadImageFromFiles(files, source);
            }
        }

        private async void LoadImageFromClipboard(bool source = true)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var supported_fmts = new string[] { "PNG", "image/png", "image/jpg", "image/jpeg", "image/tif", "image/tiff", "image/bmp", "DeviceIndependentBitmap", "image/wbmp", "image/webp" };
                    IDataObject dataPackage = Clipboard.GetDataObject();
                    var fmts = dataPackage.GetFormats();
                    foreach (var fmt in supported_fmts)
                    {
                        if (fmts.Contains(fmt))
                        {
                            var exists = dataPackage.GetDataPresent(fmt, true);
                            if (exists)
                            {
                                var obj = dataPackage.GetData(fmt, true);
                                if (obj is MemoryStream)
                                {
                                    var img = new MagickImage((obj as MemoryStream));
                                    if (source) SourceImage = img;
                                    else TargetImage = img;
                                    UpdateImageViewer();
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }
            }, System.Windows.Threading.DispatcherPriority.Render);
        }

        private async void SaveResultToClipboard()
        {
            if (ResultImage is MagickImage)
            {
                try
                {
                    var bs = ResultImage.ToBitmapSource();

                    DataObject dataPackage = new DataObject();
                    MemoryStream ms = null;

                    #region Copy Standard Bitmap date to Clipboard
                    dataPackage.SetImage(bs);
                    #endregion
                    #region Copy other MIME format data to Clipboard
                    string[] fmts = new string[] { "PNG", "image/png", "image/bmp", "image/jpg", "image/jpeg" };
                    //string[] fmts = new string[] { };
                    foreach (var fmt in fmts)
                    {
                        if (fmt.Equals("CF_DIBV5", StringComparison.CurrentCultureIgnoreCase))
                        {
                            byte[] arr = await ToBytes(bs, fmt);
                            byte[] dib = arr.Skip(14).ToArray();
                            ms = new MemoryStream(dib);
                            dataPackage.SetData(fmt, ms);
                            await ms.FlushAsync();
                        }
                        else
                        {
                            byte[] arr = await ToBytes(bs, fmt);
                            ms = new MemoryStream(arr);
                            dataPackage.SetData(fmt, ms);
                            await ms.FlushAsync();
                        }
                    }
                    #endregion
                    Clipboard.SetDataObject(dataPackage, true);
                }
                catch { }
            }
        }

        private void SaveResultToFile()
        {
            if (ResultImage is MagickImage)
            {
                try
                {
                    var dlgSave = new CommonSaveFileDialog() { EnsurePathExists = true, EnsureValidNames = true };
                    dlgSave.Filters.Add(new CommonFileDialogFilter("PNG File", "*.png"));
                    dlgSave.Filters.Add(new CommonFileDialogFilter("JPEG File", "*.jpg"));
                    dlgSave.Filters.Add(new CommonFileDialogFilter("JPEG File", "*.jpeg"));
                    dlgSave.Filters.Add(new CommonFileDialogFilter("TIF File", "*.tif"));
                    dlgSave.Filters.Add(new CommonFileDialogFilter("TIFF File", "*.tiff"));
                    dlgSave.Filters.Add(new CommonFileDialogFilter("BITMAP File", "*.bmp"));
                    if (dlgSave.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        var file = dlgSave.FileName;
                        var ext = System.IO.Path.GetExtension(file);
                        if (string.IsNullOrEmpty(ext)) file = $"{file}.{dlgSave.Filters[dlgSave.SelectedFileTypeIndex].Extensions.FirstOrDefault()}";
                        using (var fs = new FileStream(dlgSave.FileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                        {
                            ResultImage.Write(fs);
                        }
                    }
                }
                catch { }
            }
        }

        private async Task<MagickImage> Compare(MagickImage source, MagickImage target)
        {
            MagickImage result = null;
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var setting = new CompareSettings() { Metric = ErrorMetric.Fuzz, HighlightColor = MagickColors.Red };
                    if (source is MagickImage && target is MagickImage)
                    {
                        MagickImage diff = new MagickImage();
                        source.ColorFuzz = new Percentage(Math.Min(Math.Max(ImageCompareFuzzy.Minimum, ImageCompareFuzzy.Value), ImageCompareFuzzy.Maximum));
                        var distance = source.Compare(target, setting, diff);
                        result = diff;
                    }
                }
                catch { }
            }, System.Windows.Threading.DispatcherPriority.Background);
            return (result);
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Icon = new BitmapImage(new Uri("pack://application:,,,/ImageCompare;component/Resources/Compare.ico"));

            if (!Directory.Exists(CachePath)) Directory.CreateDirectory(CachePath);
            if(Directory.Exists(CachePath)) MagickAnyCPU.CacheDirectory = CachePath;

            ZoomFitAll.IsChecked = true;
            ImageActions_Click(ZoomFitAll, e);

            var args = Environment.GetCommandLineArgs();
            LoadImageFromFiles(args.Skip(1).ToArray());
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {

        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CalcDisplay(set_ratio: true);
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            var fmts = e.Data.GetFormats(true);
#if DEBUG
            System.Diagnostics.Debug.WriteLine(string.Join(", ", fmts));
#endif
            if (new List<string>(fmts).Contains("FileDrop"))
            {
                e.Effects = DragDropEffects.Link;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            var fmts = e.Data.GetFormats(true);
            if (new List<string>(fmts).Contains("FileDrop"))
            {
                var files = e.Data.GetData("FileDrop");
                if(files is IEnumerable<string>)
                {
                    LoadImageFromFiles((files as IEnumerable<string>).ToArray(), e.Source == ImageSourceScroll || e.Source == ImageSource ? true : false);
                }
            }
        }

        private void ImageScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {

        }

        private void ImageBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {

        }

        private void ImageBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
            if (e.Device is MouseDevice)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    if (sender == ImageSourceBox)
                    {
                        start = e.GetPosition(ImageSourceScroll);
                        origin = new Point(ImageSourceScroll.HorizontalOffset, ImageSourceScroll.VerticalOffset);
                    }
                    else if (sender == ImageTargetBox)
                    {
                        start = e.GetPosition(ImageTargetScroll);
                        origin = new Point(ImageTargetScroll.HorizontalOffset, ImageTargetScroll.VerticalOffset);
                    }
                    else if (sender == ImageResultBox)
                    {
                        start = e.GetPosition(ImageResultScroll);
                        origin = new Point(ImageResultScroll.HorizontalOffset, ImageResultScroll.VerticalOffset);
                    }
                }
            }
        }

        private void ImageBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                double offset_x = -1, offset_y = -1;
                if (sender == ImageSourceBox)
                {
                    if (ImageSourceBox.Stretch == Stretch.None)
                    {
                        Point factor = new Point(ImageSourceScroll.ExtentWidth/ImageSourceScroll.ActualWidth, ImageSourceScroll.ExtentHeight/ImageSourceScroll.ActualHeight);
                        Vector v = start - e.GetPosition(ImageSourceScroll);
                        offset_x = origin.X + v.X * factor.X;
                        offset_y = origin.Y + v.Y * factor.Y;
                    }
                }
                else if (sender == ImageTargetBox)
                {
                    if (ImageTargetBox.Stretch == Stretch.None)
                    {
                        Point factor = new Point(ImageSourceScroll.ExtentWidth/ImageTargetScroll.ActualWidth, ImageTargetScroll.ExtentHeight/ImageTargetScroll.ActualHeight);
                        Vector v = start - e.GetPosition(ImageTargetScroll);
                        offset_x = origin.X + v.X * factor.X;
                        offset_y = origin.Y + v.Y * factor.Y;
                    }
                }
                else if (sender == ImageResultBox)
                {
                    if (ImageResultBox.Stretch == Stretch.None)
                    {
                        Point factor = new Point(ImageResultScroll.ExtentWidth/ImageResultScroll.ActualWidth, ImageResultScroll.ExtentHeight/ImageResultScroll.ActualHeight);
                        Vector v = start - e.GetPosition(ImageResultScroll);
                        offset_x = origin.X + v.X * factor.X;
                        offset_y = origin.Y + v.Y * factor.Y;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Original : [{origin.X:F0}, {origin.Y:F0}], Start : [{start.X:F0}, {start.Y:F0}] => Move : [{offset_x:F0}, {offset_y:F0}]");
                //System.Diagnostics.Debug.WriteLine($"Move Y: {offset_y}");
                if (offset_x >= 0)
                {
                    ImageSourceScroll.ScrollToHorizontalOffset(offset_x);
                    ImageTargetScroll.ScrollToHorizontalOffset(offset_x);
                    ImageResultScroll.ScrollToHorizontalOffset(offset_x);
                }
                if (offset_y >= 0)
                {
                    ImageSourceScroll.ScrollToVerticalOffset(offset_y);
                    ImageTargetScroll.ScrollToVerticalOffset(offset_y);
                    ImageResultScroll.ScrollToVerticalOffset(offset_y);
                }
            }
        }

        private void ZoomRatio_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded && (ZoomFitNone.IsChecked ?? false))
            {
                //var delta = e.NewValue - e.OldValue;
                //if (Math.Abs(delta) >= 0.25)
                //{
                //    new Action(() =>
                //    {
                //        try
                //        {
                //            //ActionZoomFitOp = true;
                //            var eq = Math.Round(e.NewValue);
                //            if (delta > 0)
                //            {
                //                if (e.OldValue >= 0.25 && e.NewValue < 1.5) eq = 0.5;
                //                else if (e.OldValue >= 0.5 && e.NewValue < 2.0) eq = 1;
                //            }
                //            else if (delta < 0)
                //            {
                //                if (e.OldValue >= 1.0 && e.NewValue < 1.0) eq = 0.5;
                //                else if (e.OldValue >= 0.5 && e.NewValue < 0.5) eq = 0.25;
                //            }
                //            if (e.NewValue != eq) ZoomRatio.Value = eq;
                //        }
                //        catch { }
                //        finally { e.Handled = true; }
                //        //ActionZoomFitOp = false; }
                //    }).Invoke();
                //}
                LastZoomRatio = ZoomRatio.Value;
            }
        }

        private void ImageCompareFuzzy_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            //var delta = e.NewValue - e.OldValue;
            //if (Math.Abs(delta) >= 0.25)
            //{
            //    new Action(() =>
            //    {
            //        try
            //        {
            //            var eq = Math.Round(e.NewValue);
            //            if (delta > 0)
            //            {
            //                if (e.OldValue >= 0.25 && e.NewValue < 1.5) eq = 0.5;
            //                else if (e.OldValue >= 0.5 && e.NewValue < 2.0) eq = 1;
            //            }
            //            else if (delta < 0)
            //            {
            //                if (e.OldValue >= 1.0 && e.NewValue < 1.0) eq = 0.5;
            //                else if (e.OldValue >= 0.5 && e.NewValue < 0.5) eq = 0.25;
            //            }
            //            if (e.NewValue != eq) ImageCompareFuzzy.Value = eq;
            //        }
            //        catch { }
            //        finally { e.Handled = true; }
            //    }).Invoke();
            //}
            e.Handled = true;
            UpdateImageViewer();
        }

        private void ImageActions_Click(object sender, RoutedEventArgs e)
        {
            if (sender == ImageOpenSource)
            {
                LoadImageFromFile(source: true);
            }
            else if (sender == ImageOpenTarget)
            {
                LoadImageFromFile(source: false);
            }
            else if (sender == ImagePasteSource)
            {
                LoadImageFromClipboard(source: true);
            }
            else if (sender == ImagePasteTarget)
            {
                LoadImageFromClipboard(source: false);
            }
            else if (sender == ImageCompare)
            {
                UpdateImageViewer();
            }
            else if (sender == ImageCopyResult)
            {
                SaveResultToClipboard();
            }
            else if (sender == ImageSaveResult)
            {
                SaveResultToFile();
            }
            else if (sender == ZoomFitNone)
            {
                if (ZoomFitNone.IsChecked ?? false)
                {
                    ImageSourceBox.Stretch = Stretch.None;
                    ImageTargetBox.Stretch = Stretch.None;
                    ImageResultBox.Stretch = Stretch.None;

                    ZoomFitAll.IsChecked = false;
                    ZoomFitWidth.IsChecked = false;
                    ZoomFitHeight.IsChecked = false;

                    CalcDisplay(set_ratio: true);
                }
            }
            else if (sender == ZoomFitAll)
            {
                var fit = ZoomFitAll.IsChecked ?? false;
                if (fit)
                {
                    ImageSourceBox.Stretch = Stretch.Uniform;
                    ImageTargetBox.Stretch = Stretch.Uniform;
                    ImageResultBox.Stretch = Stretch.Uniform;

                    ZoomFitNone.IsChecked = false;
                    ZoomFitWidth.IsChecked = false;
                    ZoomFitHeight.IsChecked = false;

                    CalcDisplay(set_ratio: true);
                }
            }
            else if (sender == ZoomFitWidth)
            {
                if (ZoomFitWidth.IsChecked ?? false)
                {
                    ImageSourceBox.Stretch = Stretch.None;
                    ImageTargetBox.Stretch = Stretch.None;
                    ImageResultBox.Stretch = Stretch.None;

                    ZoomFitNone.IsChecked = false;
                    ZoomFitAll.IsChecked = false;
                    ZoomFitHeight.IsChecked = false;

                    CalcDisplay(set_ratio: true);
                }
            }
            else if (sender == ZoomFitHeight)
            {
                if (ZoomFitHeight.IsChecked ?? false)
                {
                    ImageSourceBox.Stretch = Stretch.None;
                    ImageTargetBox.Stretch = Stretch.None;
                    ImageResultBox.Stretch = Stretch.None;

                    ZoomFitNone.IsChecked = false;
                    ZoomFitAll.IsChecked = false;
                    ZoomFitWidth.IsChecked = false;

                    CalcDisplay(set_ratio: true);
                }
            }
        }
    }
}
