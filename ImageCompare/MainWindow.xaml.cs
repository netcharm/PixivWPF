using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.WindowsAPICodePack.Dialogs;
using ImageMagick;

namespace ImageCompare
{
    public enum ImageType { Source = 0, Target = 1, Result = 2 }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string AppPath = Path.GetDirectoryName(Application.ResourceAssembly.CodeBase.ToString()).Replace("file:\\", "");
        private static string CachePath =  Path.Combine(AppPath, "cache");

        private MagickImage SourceImage { get; set; }
        private MagickImage TargetImage { get; set; }
        private MagickImage ResultImage { get; set; }

        private double ImageDistance { get; set; } = 0;
        private double LastZoomRatio { get; set; } = 1;
        private bool LastOpIsCompose { get; set; } = false;

        private ErrorMetric ErrorMetricMode { get; set; } = ErrorMetric.Fuzz;
        private CompositeOperator CompositeMode { get; set; } = CompositeOperator.Difference;
        private IMagickColor<float> HighlightColor { get; set; } = MagickColors.Red;
        private IMagickColor<float> LowlightColor { get; set; } = null;
        private IMagickColor<float> MasklightColor { get; set; } = null;

        private bool FlipX_Source { get; set; } = false;
        private bool FlipY_Source { get; set; } = false;
        private int Rotate_Source { get; set; } = 0;
        private bool FlipX_Target { get; set; } = false;
        private bool FlipY_Target { get; set; } = false;
        private int Rotate_Target { get; set; } = 0;

        private bool ToggleSourceTarget { get { return (ImageToggle.IsChecked ?? false); } }

        private ContextMenu cm_compare_mode = null;
        private ContextMenu cm_compose_mode = null;

        private SemaphoreSlim _CanUpdate_ = new SemaphoreSlim(1, 1);

        private Point start;
        private Point origin;

        private double VALUE_GB = 1024 * 1024 * 1024;
        private double VALUE_MB = 1024 * 1024;
        private double VALUE_KB = 1024;

        private string SmartFileSize(long v, double factor = 1, bool unit = true, int padleft = 0) { return (SmartFileSize((double)v, factor, unit, padleft: padleft)); }

        private string SmartFileSize(double v, double factor = 1, bool unit = true, bool trimzero = true, int padleft = 0)
        {
            string v_str = string.Empty;
            string u_str = string.Empty;
            if (double.IsNaN(v) || double.IsInfinity(v) || double.IsNegativeInfinity(v) || double.IsPositiveInfinity(v)) { v_str = "0"; u_str = "B"; }
            else if (v >= VALUE_GB) { v_str = $"{v / factor / VALUE_GB:F2}"; u_str = "GB"; }
            else if (v >= VALUE_MB) { v_str = $"{v / factor / VALUE_MB:F2}"; u_str = "MB"; }
            else if (v >= VALUE_KB) { v_str = $"{v / factor / VALUE_KB:F2}"; u_str = "KB"; }
            else { v_str = $"{v / factor:F0}"; u_str = "B"; }
            var vs = trimzero && !u_str.Equals("B") ? v_str.Trim('0').TrimEnd('.') : v_str;
            return ((unit ? $"{vs} {u_str}" : vs).PadLeft(padleft));
        }

        private MagickImage GetImage(ImageType type)
        {
            MagickImage result = null;
            if (type != ImageType.Result)
            {
                bool source  = type == ImageType.Source ? true : false;
                result = source ^ ToggleSourceTarget ? SourceImage : TargetImage;
            }
            else
            {
                result = ResultImage;
            }
            return (result);
        }

        private void SetImage(ImageType type, MagickImage image)
        {
            if (type != ImageType.Result)
            {
                bool source  = type == ImageType.Source ? true : false;
                if (source ^ ToggleSourceTarget)
                {
                    if (SourceImage is MagickImage) SourceImage.Dispose();
                    SourceImage = image;
                    FlipX_Source = false;
                    FlipY_Source = false;
                    Rotate_Source = 0;
                }
                else
                {
                    if (TargetImage is MagickImage) TargetImage.Dispose();
                    TargetImage = image;
                    FlipX_Target = false;
                    FlipY_Target = false;
                    Rotate_Target = 0;
                }
            }
            else
            {
                if (ResultImage is MagickImage) ResultImage.Dispose();
                ResultImage = image;
            }
            UpdateImageViewer(assign: true, compose: LastOpIsCompose);
        }

        private void SetImage(ImageType type, IMagickImage<float> image)
        {
            try
            {
                SetImage(type, new MagickImage(image));
            }
            catch { }
        }

        private void GetExif(MagickImage image)
        {
            if (image is MagickImage)
            {
                var exif = image.GetExifProfile() ?? new ExifProfile();
                var tag = exif.GetValue(ExifTag.XPTitle);
                if (tag != null) { var text = Encoding.Unicode.GetString(tag.Value).Trim('\0').Trim(); }
#if DEBUG
                //System.Diagnostics.Debug.WriteLine(text);
#endif
            }
        }

        private string GetImageInfo(ImageType type)
        {
            string result = string.Empty;
            //var image = source ? ImageSource.Source : ImageTarget.Source;
            var image = GetImage(type);
            if (image != null)
            {
                var tip = new List<string>();
                tip.Add($"Dimention      = {image.Width:F0}x{image.Height:F0}x{image.ChannelCount * image.Depth:F0}");
                tip.Add($"Colors         = {image.TotalColors}");
                tip.Add($"Color Space    = {Path.GetFileName(image.ColorSpace.ToString())}");
                tip.Add($"Memory Usage   = {SmartFileSize(image.Width * image.Height * image.ChannelCount * image.Depth / 4)}");
                tip.Add($"Display Memory = {SmartFileSize(image.Width * image.Height * 4)}");
                if (!string.IsNullOrEmpty(image.FileName))
                    tip.Add($"FileName       = {Path.GetFileName(image.FileName)}");
                result = string.Join(Environment.NewLine, tip);
            }
            return (string.IsNullOrEmpty(result) ? null : result);
        }

        private void RotateImage(bool source, int value)
        {
            var action = false;
            if (source ^ ToggleSourceTarget)
            {
                if (SourceImage is MagickImage)
                {
                    SourceImage.Rotate(value);
                    Rotate_Source += value;
                    Rotate_Source %= 360;
                    action = true;
                }
            }
            else
            {
                if (TargetImage is MagickImage)
                {
                    TargetImage.Rotate(value);
                    Rotate_Target += value;
                    Rotate_Target %= 360;
                    action = true;
                }
            }
            if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
        }

        private void FlipImage(bool source)
        {
            var action = false;
            if (source ^ ToggleSourceTarget)
            {
                if (SourceImage is MagickImage)
                {
                    SourceImage.Flip();
                    FlipY_Source = !FlipY_Source;
                    action = true;
                }
            }
            else
            {
                if (TargetImage is MagickImage)
                {
                    TargetImage.Flip();
                    FlipY_Target = !FlipY_Target;
                    action = true;
                }
            }
            if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
        }

        private void FlopImage(bool source)
        {
            var action = false;
            if (source ^ ToggleSourceTarget)
            {
                if (SourceImage is MagickImage)
                {
                    SourceImage.Flop();
                    FlipX_Source = !FlipX_Source;
                    action = true;
                }
            }
            else
            {
                if (TargetImage is MagickImage)
                {
                    TargetImage.Flop();
                    FlipX_Target = !FlipX_Target;
                    action = true;
                }
            }
            if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
        }

        private void ResetImage(bool source)
        {
            var action = false;
            if (source ^ ToggleSourceTarget)
            {
                if (SourceImage is MagickImage)
                {
                    if (FlipX_Source)
                    {
                        SourceImage.Flop();
                        FlipX_Source = false;
                        action = true;
                    }
                    if (FlipY_Source)
                    {
                        SourceImage.Flip();
                        FlipY_Source = false;
                        action = true;
                    }
                    if (Rotate_Source % 360 != 0)
                    {
                        SourceImage.Rotate(-Rotate_Source);
                        Rotate_Source = 0;
                        action = true;
                    }
                }
            }
            else
            {
                if (TargetImage is MagickImage)
                {
                    if (FlipX_Target)
                    {
                        TargetImage.Flop();
                        FlipX_Target = false;
                        action = true;
                    }
                    if (FlipY_Target)
                    {
                        TargetImage.Flip();
                        FlipY_Target = false;
                        action = true;
                    }
                    if (Rotate_Target % 360 != 0)
                    {
                        TargetImage.Rotate(-Rotate_Target);
                        Rotate_Target = 0;
                        action = true;
                    }
                }
            }
            if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
        }

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

        private async Task<MagickImage> Compare(MagickImage source, MagickImage target, bool compose = false)
        {
            MagickImage result = null;
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (source is MagickImage && target is MagickImage)
                    {
                        source.ColorFuzz = new Percentage(Math.Min(Math.Max(ImageCompareFuzzy.Minimum, ImageCompareFuzzy.Value), ImageCompareFuzzy.Maximum));
                        var i_src = ToggleSourceTarget ? target : source;
                        var i_dst = ToggleSourceTarget ? source : target;

                        if (compose)
                        {
                            using (MagickImage diff = new MagickImage(i_dst.Clone()))
                            {
                                diff.Composite(i_src, CompositeMode);
                                result = new MagickImage(diff.Clone());
                            }
                        }
                        else
                        {
                            using (MagickImage diff = new MagickImage())
                            {
                                var setting = new CompareSettings()
                                {
                                    Metric = ErrorMetricMode,
                                    HighlightColor = HighlightColor,
                                    LowlightColor = LowlightColor,
                                    MasklightColor = MasklightColor
                                };
                                var distance = i_src.Compare(i_dst, setting, diff);
                                ImageCompare.ToolTip = $"Mode : {ErrorMetricMode.ToString()}\nDifference : {distance:F4}";
                                result = new MagickImage(diff.Clone());
                            }
                        }
                    }
                }
                catch { }
            }, System.Windows.Threading.DispatcherPriority.Render);
            return (result);
        }

        private async void UpdateImageViewer(bool compose = false, bool assign = false)
        {
            if (await _CanUpdate_.WaitAsync(TimeSpan.FromMilliseconds(200)))
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
#if DEBUG
                        System.Diagnostics.Debug.WriteLine("UpdateImageViewer");
#endif
                        ProcessStatus.IsIndeterminate = true;
                        await Task.Delay(1);
                        if (assign || ImageSource.Source == null || ImageTarget.Source == null)
                        {
                            try
                            {
                                if (ToggleSourceTarget)
                                {
                                    ImageSource.Source = TargetImage is MagickImage ? TargetImage.ToBitmapSource() : null;
                                    ImageTarget.Source = SourceImage is MagickImage ? SourceImage.ToBitmapSource() : null;
                                }
                                else
                                {
                                    ImageSource.Source = SourceImage is MagickImage ? SourceImage.ToBitmapSource() : null;
                                    ImageTarget.Source = TargetImage is MagickImage ? TargetImage.ToBitmapSource() : null;
                                }
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                await Task.Delay(1);
                            }
                            catch { }
                        }

                        ImageSource.ToolTip = GetImageInfo(ImageType.Source);
                        ImageTarget.ToolTip = GetImageInfo(ImageType.Target);

                        ImageResult.Source = null;
                        if (ResultImage is MagickImage) ResultImage.Dispose();
                        ResultImage = await Compare(SourceImage, TargetImage, compose: compose);
                        await Task.Delay(1);
                        if (ResultImage is MagickImage) ImageResult.Source = ResultImage.ToBitmapSource();
                        ImageResult.ToolTip = GetImageInfo(ImageType.Result);
                        CalcDisplay(set_ratio: false);
                        GetExif(SourceImage);
                    }
                    catch { }
                    finally
                    {
                        ProcessStatus.IsIndeterminate = false;
                        await Task.Delay(1);
                        if (_CanUpdate_ is SemaphoreSlim && _CanUpdate_.CurrentCount < 1) _CanUpdate_.Release();
                    }
                }, System.Windows.Threading.DispatcherPriority.Render);
            }
        }

        private void CleanImage()
        {
            if (SourceImage is MagickImage) SourceImage.Dispose();
            if (TargetImage is MagickImage) TargetImage.Dispose();
            if (ResultImage is MagickImage) ResultImage.Dispose();

            if (ImageSource.Source != null) ImageSource.Source = null;
            if (ImageTarget.Source != null) ImageTarget.Source = null;
            if (ImageResult.Source != null) ImageResult.Source = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();
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

        private async void LoadImageFromClipboard(bool source = true)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var supported_fmts = new string[] { "PNG", "image/png", "image/jpg", "image/jpeg", "image/tif", "image/tiff", "image/bmp", "DeviceIndependentBitmap", "image/wbmp", "image/webp", "Text" };
                    IDataObject dataPackage = Clipboard.GetDataObject();
                    var fmts = dataPackage.GetFormats();
                    foreach (var fmt in supported_fmts)
                    {
                        if (fmts.Contains(fmt))
                        {
                            if (fmt.Equals("Text", StringComparison.CurrentCultureIgnoreCase))
                            {
                                var text = dataPackage.GetData(fmt, true) as string;
                                var IsFile = !text.StartsWith("data:", StringComparison.CurrentCultureIgnoreCase) &&
                                              (text.Contains('.') || text.Contains('\\') || text.Contains(':'));
                                if (IsFile)
                                {
                                    try
                                    {
                                        var files = text.Split(new string[] { Environment.NewLine, "\r", "\n", " "}, StringSplitOptions.RemoveEmptyEntries);
                                        if (files.Length > 0) LoadImageFromFiles(files.Select(f => f.Trim('"').Trim()).ToArray());
                                    }
                                    catch { }
                                }
                                else
                                {
                                    try
                                    {
                                        SetImage(source ? ImageType.Source : ImageType.Target, MagickImage.FromBase64(Regex.Replace(text, @"^data:.*?;base64,", "", RegexOptions.IgnoreCase)));
                                    }
#if DEBUG
                                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.Message); }
#else
                                    catch { }
#endif
                                }
                            }
                            else
                            {
                                var exists = dataPackage.GetDataPresent(fmt, true);
                                if (exists)
                                {
                                    var obj = dataPackage.GetData(fmt, true);
                                    if (obj is MemoryStream)
                                    {
                                        var img = new MagickImage((obj as MemoryStream));
                                        if (source)
                                            SetImage(ImageType.Source, img);
                                        else
                                            SetImage(ImageType.Target, img);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }, System.Windows.Threading.DispatcherPriority.Render);
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
                                SetImage(ImageType.Source, new MagickImage(fs));
                            }
                            using (var fs = new FileStream(file_t, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                SetImage(ImageType.Target, new MagickImage(fs));
                            }
                        }
                        else
                        {
                            if (source)
                            {
                                file_s = files.First();
                                using (var fs = new FileStream(file_s, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    SetImage(ImageType.Source, new MagickImage(fs));
                                }
                            }
                            else
                            {
                                file_t = files.First();
                                using (var fs = new FileStream(file_t, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    SetImage(ImageType.Target, new MagickImage(fs));
                                }
                            }
                        }
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
                        var ext = Path.GetExtension(file);
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

        private void CreateImageOpMenu(FrameworkElement target)
        {
            bool source = target == ImageSource ? true : false;

            var item_fh = new MenuItem() { Header = "Flip Horizon", Tag = source };
            var item_fv = new MenuItem() { Header = "Flip Vertical", Tag = source };
            var item_r090 = new MenuItem() { Header = "Rotate +90", Tag = source };
            var item_r180 = new MenuItem() { Header = "Rotate 180", Tag = source };
            var item_r270 = new MenuItem() { Header = "Rotate -90", Tag = source };
            var item_reset = new MenuItem() { Header = "Reset", Tag = source };
            item_fh.Click += (obj, evt) =>
            {
                FlopImage((bool)(obj as MenuItem).Tag);
            };
            item_fv.Click += (obj, evt) =>
            {
                FlipImage((bool)(obj as MenuItem).Tag);
            };
            item_r090.Click += (obj, evt) =>
            {
                RotateImage((bool)(obj as MenuItem).Tag, 90);
            };
            item_r180.Click += (obj, evt) =>
            {
                RotateImage((bool)(obj as MenuItem).Tag, 180);
            };
            item_r270.Click += (obj, evt) =>
            {
                RotateImage((bool)(obj as MenuItem).Tag, 270);
            };
            item_reset.Click += (obj, evt) =>
            {
                ResetImage((bool)(obj as MenuItem).Tag);
            };
            var result = new ContextMenu() { PlacementTarget = target };
            result.Items.Add(item_fh);
            result.Items.Add(item_fv);
            result.Items.Add(new Separator());
            result.Items.Add(item_r090);
            result.Items.Add(item_r270);
            result.Items.Add(item_r180);
            result.Items.Add(new Separator());
            result.Items.Add(item_reset);
            target.ContextMenu = result;
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Icon = new BitmapImage(new Uri("pack://application:,,,/ImageCompare;component/Resources/Compare.ico"));

            if (!Directory.Exists(CachePath)) Directory.CreateDirectory(CachePath);
            if (Directory.Exists(CachePath)) MagickAnyCPU.CacheDirectory = CachePath;

            #region Create ErrorMetric Mode Selector
            cm_compare_mode = new ContextMenu() { PlacementTarget = ImageCompareFuzzy };
            foreach (var v in Enum.GetValues(typeof(ErrorMetric)))
            {
                var item = new MenuItem()
                {
                    Header = v.ToString(),
                    Tag = v,
                    IsChecked = ((ErrorMetric)v == ErrorMetric.Fuzz ? true : false)
                };
                item.Click += (obj, evt) =>
                {
                    var menu = obj as MenuItem;
                    foreach (MenuItem m in cm_compare_mode.Items) m.IsChecked = false;
                    menu.IsChecked = true;
                    ErrorMetricMode = (ErrorMetric)menu.Tag;
                    if (!LastOpIsCompose) UpdateImageViewer(compose: LastOpIsCompose);
                };
                cm_compare_mode.Items.Add(item);
            }
            ImageCompare.ContextMenu = cm_compare_mode;
            #endregion
            #region Create Compose Mode Selector
            cm_compose_mode = new ContextMenu() { PlacementTarget = ImageCompose };
            foreach (var v in Enum.GetValues(typeof(CompositeOperator)))
            {
                var item = new MenuItem()
                {
                    Header = v.ToString(),
                    Tag = v,
                    IsChecked = ((CompositeOperator)v == CompositeOperator.Difference ? true : false)
                };
                item.Click += (obj, evt) =>
                {
                    var menu = obj as MenuItem;
                    foreach (MenuItem m in cm_compose_mode.Items) m.IsChecked = false;
                    menu.IsChecked = true;
                    CompositeMode = (CompositeOperator)menu.Tag;
                    if (LastOpIsCompose) UpdateImageViewer(compose: LastOpIsCompose);
                };
                cm_compose_mode.Items.Add(item);
            }
            ImageCompose.ContextMenu = cm_compose_mode;
            #endregion

            #region Create Image Flip/Rotate Menu
            #region actions
            //Func<bool, MagickImage> GetImage = (source) => {
            //    MagickImage result = null;
            //    if (source)
            //        result = ToggleSourceTarget ? TargetImage : SourceImage;
            //    else
            //        result = ToggleSourceTarget ? SourceImage : TargetImage;
            //    return(result);
            //};
            //Action<bool, int> RotateImage = (source, value) =>
            //{
            //    if (source ^ ToggleSourceTarget)
            //    {
            //        SourceImage.Rotate(value);
            //        Rotate_Source += value;
            //    }
            //    else
            //    {
            //        TargetImage.Rotate(value);
            //        Rotate_Target += value;
            //    }
            //    UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            //};
            //Action<bool> FlipImage = (source) =>
            //{
            //    if (source ^ ToggleSourceTarget)
            //    {
            //        SourceImage.Flip();
            //        FlipY_Source = !FlipY_Source;
            //    }
            //    else
            //    {
            //        TargetImage.Flip();
            //        FlipY_Target = !FlipY_Target;
            //    }
            //    UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            //};
            //Action<bool> FlopImage = (source) =>
            //{
            //    if (source ^ ToggleSourceTarget)
            //    {
            //        SourceImage.Flop();
            //        FlipX_Source = !FlipX_Source;
            //    }
            //    else
            //    {
            //        TargetImage.Flop();
            //        FlipX_Target = !FlipX_Target;
            //    }
            //    UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            //};
            //Action<bool> ResetImage = (source) =>
            //{
            //    if (source ^ ToggleSourceTarget)
            //    {
            //        if(FlipX_Source) SourceImage.Flop();
            //        if(FlipY_Source) SourceImage.Flip();
            //        SourceImage.Rotate(-Rotate_Source);
            //        Rotate_Source = 0;
            //        FlipX_Source = false;
            //        FlipY_Source = false;
            //    }
            //    else
            //    {
            //        if(FlipX_Target) TargetImage.Flop();
            //        if(FlipY_Target) TargetImage.Flip();
            //        TargetImage.Rotate(-Rotate_Target);
            //        Rotate_Target = 0;
            //        FlipX_Target = false;
            //        FlipY_Target = false;
            //    }
            //    UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            //};
            //Action<FrameworkElement, bool> CreateImageOpMenu = (target, source) => {
            //    var item_fh = new MenuItem() { Header = "Flip Horizon", Tag = source };
            //    var item_fv = new MenuItem() { Header = "Flip Vertical", Tag = source };
            //    var item_r090 = new MenuItem() { Header = "Rotate +90", Tag = source };
            //    var item_r180 = new MenuItem() { Header = "Rotate 180", Tag = source };
            //    var item_r270 = new MenuItem() { Header = "Rotate -90", Tag = source };
            //    var item_reset = new MenuItem() { Header = "Reset", Tag = source };
            //    item_fh.Click += (obj, evt) => {
            //        FlopImage.Invoke((bool)(obj as MenuItem).Tag);
            //    };
            //    item_fv.Click += (obj, evt) => {
            //        FlipImage.Invoke((bool)(obj as MenuItem).Tag);
            //    };
            //    item_r090.Click += (obj, evt) => {
            //        RotateImage.Invoke((bool)(obj as MenuItem).Tag, 90);
            //    };
            //    item_r180.Click += (obj, evt) => {
            //        RotateImage.Invoke((bool)(obj as MenuItem).Tag, 180);
            //    };
            //    item_r270.Click += (obj, evt) => {
            //        RotateImage.Invoke((bool)(obj as MenuItem).Tag, 270);
            //    };
            //    item_reset.Click += (obj, evt) => {
            //        ResetImage.Invoke((bool)(obj as MenuItem).Tag);
            //    };
            //    var result = new ContextMenu() { PlacementTarget = target };
            //    result.Items.Add(item_fh);
            //    result.Items.Add(item_fv);
            //    result.Items.Add(new Separator());
            //    result.Items.Add(item_r090);
            //    result.Items.Add(item_r270);
            //    result.Items.Add(item_r180);
            //    result.Items.Add(new Separator());
            //    result.Items.Add(item_reset);
            //    target.ContextMenu = result;
            //};

            //CreateImageOpMenu.Invoke(ImageSource, true);
            //CreateImageOpMenu.Invoke(ImageTarget, false);
            #endregion actions
            CreateImageOpMenu(ImageSource);
            CreateImageOpMenu(ImageTarget);
            #endregion

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
                if (files is IEnumerable<string>)
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
            UpdateImageViewer(compose: LastOpIsCompose);
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
            else if (sender == ImageClean)
            {
                CleanImage();
            }
            else if (sender == ImageToggle)
            {
                UpdateImageViewer(assign: true, compose: LastOpIsCompose);
            }
            else if (sender == ImageCompose)
            {
                LastOpIsCompose = true;
                UpdateImageViewer(compose: true);
            }
            else if (sender == ImageCompare)
            {
                LastOpIsCompose = false;
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
