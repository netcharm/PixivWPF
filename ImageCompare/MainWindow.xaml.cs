using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
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
using System.Windows.Threading;

using ImageMagick;
using Xceed.Wpf.Toolkit;

namespace ImageCompare
{
    public enum ImageType { All = 0, Source = 1, Target = 2, Result = 3, None = 255 }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Application Infomations
        private static string AppExec = Application.ResourceAssembly.CodeBase.ToString().Replace("file:///", "").Replace("/", "\\");
        private static string AppPath = Path.GetDirectoryName(AppExec);
        private static string AppName = Path.GetFileNameWithoutExtension(AppPath);
        private static string CachePath =  "cache";

        private string DefaultFontFamilyName { get; set; } = "Segoe MDL2 Assets";
        private FontFamily DefaultFontFamily { get; set; } = null;
        private int DefaultFontSize { get; set; } = 16;

        private string DefaultWindowTitle { get; set; } = string.Empty;
        private string DefaultCompareToolTip { get; set; } = string.Empty;
        private string DefaultComposeToolTip { get; set; } = string.Empty;
        #endregion

        #region Magick.Net Settings
        private string SupportedFiles { get; set; } = string.Empty;

        private int MaxCompareSize { get; set; } = 1024;
        private MagickGeometry CompareResizeGeometry { get; set; } = null;

        private MagickImage SourceOriginal { get; set; } = null;
        private MagickImage TargetOriginal { get; set; } = null;
        private bool SourceLoaded { get; set; } = false;
        private bool TargetLoaded { get; set; } = false;

        private string SourceFile { get; set; } = string.Empty;
        private string TargetFile { get; set; } = string.Empty;

        private MagickImage SourceImage { get; set; } = null;
        private MagickImage TargetImage { get; set; } = null;
        private MagickImage ResultImage { get; set; } = null;

        private double ImageDistance { get; set; } = 0;
        private double LastZoomRatio { get; set; } = 1;
        private bool LastOpIsCompose { get; set; } = false;
        private ImageType LastImageType { get; set; } = ImageType.Result;

        private Channels CompareImageChannels { get; set; } = Channels.Default;
        private bool CompareImageForceScale { get { return (UseSmallerImage.IsChecked ?? false); } }
        private bool CompareImageForceColor { get { return (UseColorImage.IsChecked ?? false); } }
        private ErrorMetric ErrorMetricMode { get; set; } = ErrorMetric.Fuzz;
        private CompositeOperator CompositeMode { get; set; } = CompositeOperator.Difference;
#if Q16HDRI
        private IMagickColor<float> HighlightColor { get; set; } = MagickColors.Red;
        private IMagickColor<float> LowlightColor { get; set; } = null;
        private IMagickColor<float> MasklightColor { get; set; } = null;
#else
        private IMagickColor<byte> HighlightColor { get; set; } = MagickColors.Red;
        private IMagickColor<byte> LowlightColor { get; set; } = null;
        private IMagickColor<byte> MasklightColor { get; set; } = null;
#endif
        private bool FlipX_Source { get; set; } = false;
        private bool FlipY_Source { get; set; } = false;
        private int Rotate_Source { get; set; } = 0;
        private bool FlipX_Target { get; set; } = false;
        private bool FlipY_Target { get; set; } = false;
        private int Rotate_Target { get; set; } = 0;

        private bool WeakBlur { get { return (UseWeakBlur.IsChecked ?? false); } }
        private bool WeakSharp { get { return (UseWeakSharp.IsChecked ?? false); } }
        private bool WeakEffects { get { return (UseWeakEffects.IsChecked ?? false); } }
        #endregion

        private bool ToggleSourceTarget { get { return (ImageToggle.IsChecked ?? false); } }

        private ContextMenu cm_compare_mode = null;
        private ContextMenu cm_compose_mode = null;

        #region DoEvent Helper
        private static object ExitFrame(object state)
        {
            ((DispatcherFrame)state).Continue = false;
            return null;
        }

        private static SemaphoreSlim CanDoEvents = new SemaphoreSlim(1, 1);
        public static async void DoEvents()
        {
            if (await CanDoEvents.WaitAsync(0))
            {
                try
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        await Dispatcher.Yield(DispatcherPriority.Render);
                        //await System.Windows.Threading.Dispatcher.Yield();

                        //DispatcherFrame frame = new DispatcherFrame();
                        //await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(ExitFrame), frame);
                        //Dispatcher.PushFrame(frame);
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        if (Application.Current.Dispatcher.CheckAccess())
                        {
                            DispatcherFrame frame = new DispatcherFrame();
                            //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate { }));
                            //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate { }));

                            //await Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrame), frame);
                            //await Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(ExitFrame), frame);
                            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, new DispatcherOperationCallback(ExitFrame), frame);
                            Dispatcher.PushFrame(frame);
                        }
                    }
                    catch (Exception)
                    {
                        await Task.Delay(1);
                    }
                }
                finally
                {
                    //CanDoEvents.Release(max: 1);
                    if (CanDoEvents is SemaphoreSlim && CanDoEvents.CurrentCount <= 0) CanDoEvents.Release();
                }
            }
        }
        #endregion

        #region Text Format Helper
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

        private string DecodeHexUnicode(string text)
        {
            var result = text;
            foreach (Match m in Regex.Matches(text, @"((\d{1,3}, ?){2,}\d{1,3})"))
            {
                List<byte> bytes = new List<byte>();
                var values = m.Groups[1].Value.Split(',').Select(s => s.Trim()).ToList();
                foreach (var value in values)
                {
                    if (int.Parse(value) > 255) continue;
                    bytes.Add(byte.Parse(value));
                }
                if (bytes.Count > 0) result = result.Replace(m.Groups[1].Value, Encoding.Unicode.GetString(bytes.ToArray()).TrimEnd('\0'));
            }
            return (result);
        }
        #endregion

        #region Image Display Routines
        private SemaphoreSlim _CanUpdate_ = new SemaphoreSlim(1, 1);
        private Dictionary<Color, string> ColorNames = new Dictionary<Color, string>();
        private Point mouse_start;
        private Point mouse_origin;

        private void GetColorNames()
        {
            var cpl = (typeof(Colors) as Type).GetProperties();
        }

        private string ColorToHex(Color color)
        {
            return ($"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
        }

        private IList<Color> GetRecentColors(ColorPicker picker)
        {
            var result = new List<Color>();
            if (picker is ColorPicker)
            {
                result.AddRange(picker.RecentColors.Select(c => c.Color ?? Colors.Transparent).ToList().Distinct());
            }
            return (result);
        }

        private IList<string> GetRecentHexColors(ColorPicker picker)
        {
            var result = new List<string>();
            if (picker is ColorPicker)
            {
                result.AddRange(picker.RecentColors.Select(c => ColorToHex(c.Color ?? Colors.Transparent)).Distinct());
            }
            return (result);
        }

        private void SetRecentColors(ColorPicker picker, IEnumerable<Color> colors)
        {
            if (colors is IEnumerable<Color> && colors.Count() > 0)
            {
                picker.RecentColors.Clear();
                var ct = Colors.Transparent;
                foreach (var color in colors)
                {
                    try
                    {
                        var ci = picker.AvailableColors.Where(c => c.Color.Equals(color)).FirstOrDefault();
                        if (ci != null && !picker.RecentColors.Contains(ci)) picker.RecentColors.Add(ci);
                        else if (color.A == ct.A && color.R == ct.R && color.G == ct.G && color.B == ct.B) continue;
                        else if (picker.RecentColors.Where(c => c.Color.Equals(color)).Count() <= 0)
                            picker.RecentColors.Add(new ColorItem(color, ColorToHex(color)));
                    }
                    catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); continue; }
                }
            }
        }

        private void SetRecentColors(ColorPicker picker, IEnumerable<string> colors)
        {
            if (colors is IEnumerable<string> && colors.Count() > 0)
            {
                picker.RecentColors.Clear();
                SetRecentColors(picker, colors.Select(c => (Color)ColorConverter.ConvertFromString(c)));
            }
        }

        private void GetExif(MagickImage image)
        {
            try
            {
                if (image is MagickImage)
                {
                    //var exif = image.GetExifProfile() ?? new ExifProfile();
                    //var tag = exif.GetValue(ExifTag.XPTitle);
                    //if (tag != null) { var text = Encoding.Unicode.GetString(tag.Value).Trim('\0').Trim(); }
#if DEBUG
                    //Debug.WriteLine(text);
#endif
                    var profiles = new Dictionary<string, IImageProfile>();
                    foreach (var pn in image.ProfileNames)
                    {
                        if (image.HasProfile(pn)) profiles[pn] = image.GetProfile(pn);
                        if (pn.Equals("exif", StringComparison.CurrentCultureIgnoreCase))
                            profiles[pn] = image.GetExifProfile();
                        else if (pn.Equals("iptc", StringComparison.CurrentCultureIgnoreCase))
                            profiles[pn] = image.GetIptcProfile();
                        else if (pn.Equals("xmp", StringComparison.CurrentCultureIgnoreCase))
                            profiles[pn] = image.GetXmpProfile();

                        var profile = profiles[pn];
                        if (profile is ExifProfile)
                        {
                            var exif = profile as ExifProfile;
                            Debug.WriteLine(exif.GetValue(ExifTag.XPTitle));
                            Debug.WriteLine(exif.GetValue(ExifTag.XPAuthor));
                            Debug.WriteLine(exif.GetValue(ExifTag.XPKeywords));
                            Debug.WriteLine(exif.GetValue(ExifTag.XPComment));
                        }
                        else if (profile is IptcProfile)
                        {
                            var iptc = profile as IptcProfile;
                            Debug.WriteLine(iptc.GetValue(IptcTag.Title));
                            Debug.WriteLine(iptc.GetValue(IptcTag.Byline));
                            Debug.WriteLine(iptc.GetValue(IptcTag.BylineTitle));
                            Debug.WriteLine(iptc.GetValue(IptcTag.CopyrightNotice));
                            Debug.WriteLine(iptc.GetValue(IptcTag.Caption));
                            Debug.WriteLine(iptc.GetValue(IptcTag.CaptionWriter));
                        }
                        else if (profile is XmpProfile)
                        {
                            var xmp = profile as XmpProfile;
                            var xml = Encoding.UTF8.GetString(xmp.GetData());
                            //image.SetAttribute()
                        }
                    }
                }
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        private string GetImageInfo(ImageType type)
        {
            string result = string.Empty;
            try
            {
                var image = GetImage(type);
                if (image != null)
                {
                    var file = type == ImageType.Source ? SourceFile : type == ImageType.Target ? TargetFile : string.Empty;
                    var st = Stopwatch.StartNew();
                    image.Density.ChangeUnits(DensityUnit.PixelsPerInch);
                    var tip = new List<string>();
                    tip.Add($"Dimention      = {image.Width:F0}x{image.Height:F0}x{image.ChannelCount * image.Depth:F0}");
                    tip.Add($"Resolution     = {image.Density.X:F0} DPI x {image.Density.Y:F0} DPI");
                    //tip.Add($"Colors         = {image.TotalColors}");
                    tip.Add($"Attributes");
                    foreach (var attr in image.AttributeNames)
                    {
                        try
                        {
                            var value = image.GetAttribute(attr);
                            if (string.IsNullOrEmpty(value)) continue;
                            if (attr.Contains("WinXP")) value = DecodeHexUnicode(value);
                            if (value.Length > 64) value = $"{value.Substring(0, 64)} ...";
                            tip.Add($"  {attr.PadRight(32, ' ')}= { value }");
                        }
                        catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show($"{attr} : {ex.Message}"); }
                    }
                    tip.Add($"Color Space    = {Path.GetFileName(image.ColorSpace.ToString())}");
                    tip.Add($"Format Info    = {image.FormatInfo.Format.ToString()}, {image.FormatInfo.MimeType}");
#if Q16HDRI
                    tip.Add($"Memory Usage   = {SmartFileSize(image.Width * image.Height * image.ChannelCount * image.Depth * 4 / 8)}");
#elif Q16
                tip.Add($"Memory Usage   = {SmartFileSize(image.Width * image.Height * image.ChannelCount * image.Depth * 2 / 8)}");
#else
                tip.Add($"Memory Usage   = {SmartFileSize(image.Width * image.Height * image.ChannelCount * image.Depth / 8)}");
#endif
                    tip.Add($"Display Memory = {SmartFileSize(image.Width * image.Height * 4)}");
                    if (!string.IsNullOrEmpty(image.FileName))
                        tip.Add($"FileName       = {image.FileName}");
                    else if (!string.IsNullOrEmpty(file))
                        tip.Add($"FileName       = {file}");
                    result = string.Join(Environment.NewLine, tip);
                    st.Stop();
                    Debug.WriteLine($"{TimeSpan.FromTicks(st.ElapsedTicks).TotalSeconds:F4}s");
                    GetExif(image);
                }
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
            return (string.IsNullOrEmpty(result) ? null : result);
        }

        private Point CalcOffset(Viewbox viewer, MouseEventArgs e)
        {
            var result = new Point(0, 0);
            double offset_x = -1, offset_y = -1;
            if (viewer == ImageSourceBox)
            {
                if (ImageSourceBox.Stretch == Stretch.None)
                {
                    Point factor = new Point(ImageSourceScroll.ExtentWidth/ImageSourceScroll.ActualWidth, ImageSourceScroll.ExtentHeight/ImageSourceScroll.ActualHeight);
                    Vector v = mouse_start - e.GetPosition(ImageSourceScroll);
                    offset_x = mouse_origin.X + v.X * factor.X;
                    offset_y = mouse_origin.Y + v.Y * factor.Y;
                }
            }
            else if (viewer == ImageTargetBox)
            {
                if (ImageTargetBox.Stretch == Stretch.None)
                {
                    Point factor = new Point(ImageSourceScroll.ExtentWidth/ImageTargetScroll.ActualWidth, ImageTargetScroll.ExtentHeight/ImageTargetScroll.ActualHeight);
                    Vector v = mouse_start - e.GetPosition(ImageTargetScroll);
                    offset_x = mouse_origin.X + v.X * factor.X;
                    offset_y = mouse_origin.Y + v.Y * factor.Y;
                }
            }
            else if (viewer == ImageResultBox)
            {
                if (ImageResultBox.Stretch == Stretch.None)
                {
                    Point factor = new Point(ImageResultScroll.ExtentWidth/ImageResultScroll.ActualWidth, ImageResultScroll.ExtentHeight/ImageResultScroll.ActualHeight);
                    Vector v = mouse_start - e.GetPosition(ImageResultScroll);
                    offset_x = mouse_origin.X + v.X * factor.X;
                    offset_y = mouse_origin.Y + v.Y * factor.Y;
                }
            }
            return (new Point(offset_x, offset_y));
        }

        private Point GetOffset(Viewbox viewer)
        {
            double offset_x = -1, offset_y = -1;
            if (viewer == ImageSourceBox)
            {
                if (ImageSourceBox.Stretch == Stretch.None)
                {
                    offset_x = ImageSourceScroll.HorizontalOffset;
                    offset_y = ImageSourceScroll.VerticalOffset;
                }
            }
            else if (viewer == ImageTargetBox)
            {
                if (ImageTargetBox.Stretch == Stretch.None)
                {
                    offset_x = ImageTargetScroll.HorizontalOffset;
                    offset_y = ImageTargetScroll.VerticalOffset;
                }
            }
            else if (viewer == ImageResultBox)
            {
                if (ImageResultBox.Stretch == Stretch.None)
                {
                    offset_x = ImageResultScroll.HorizontalOffset;
                    offset_y = ImageResultScroll.VerticalOffset;
                }
            }

            return (new Point(offset_x, offset_y));
        }

        private void SyncOffset(Point offset)
        {
            if (offset.X >= 0)
            {
                ImageSourceScroll.ScrollToHorizontalOffset(offset.X);
                ImageTargetScroll.ScrollToHorizontalOffset(offset.X);
                ImageResultScroll.ScrollToHorizontalOffset(offset.X);
            }
            if (offset.Y >= 0)
            {
                ImageSourceScroll.ScrollToVerticalOffset(offset.Y);
                ImageTargetScroll.ScrollToVerticalOffset(offset.Y);
                ImageResultScroll.ScrollToVerticalOffset(offset.Y);
            }
        }

        private void CalcDisplay(bool set_ratio = true)
        {
            try
            {
                if (ZoomFitAll.IsChecked ?? false)
                {
                    ImageSourceBox.Width = ImageSourceScroll.ActualWidth;
                    ImageSourceBox.Height = ImageSourceScroll.ActualHeight;

                    ImageTargetBox.Width = ImageTargetScroll.ActualWidth;
                    ImageTargetBox.Height = ImageTargetScroll.ActualHeight;

                    ImageResultBox.Width = ImageResultScroll.ActualWidth;
                    ImageResultBox.Height = ImageResultScroll.ActualHeight;

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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        private void CalcZoomRatio()
        {
            try
            {
                if (SourceImage is MagickImage || TargetImage is MagickImage)
                {
                    var scroll  = SourceImage is MagickImage ? ImageSourceScroll : ImageTargetScroll;
                    var image  = SourceImage is MagickImage ? SourceImage : TargetImage;
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
                        var targetX = image.Width;
                        var targetY = image.Height;
                        var ratio = scroll.ActualWidth / targetX;
                        var delta = scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Hidden || targetY * ratio <= scroll.ActualHeight ? 0 : 14;
                        ZoomRatio.Value = (scroll.ActualWidth - delta) / targetX;
                    }
                    else if (ZoomFitHeight.IsChecked ?? false)
                    {
                        var targetX = image.Width;
                        var targetY = image.Height;
                        var ratio = scroll.ActualHeight / targetY;
                        var delta = scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Hidden || targetX * ratio <= scroll.ActualWidth ? 0 : 14;
                        ZoomRatio.Value = (scroll.ActualHeight - delta) / targetY;
                    }
                }
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
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
                        Debug.WriteLine("---> UpdateImageViewer <---");
#endif
                        ProcessStatus.IsIndeterminate = true;
                        await Task.Delay(1);
                        DoEvents();
                        if (assign || ImageSource.Source == null || ImageTarget.Source == null)
                        {
                            try
                            {
                                if (ToggleSourceTarget)
                                {
                                    ImageSource.Source = TargetImage is MagickImage && !TargetImage.IsDisposed ? TargetImage.ToBitmapSource() : null;
                                    ImageTarget.Source = SourceImage is MagickImage && !SourceImage.IsDisposed ? SourceImage.ToBitmapSource() : null;
                                }
                                else
                                {
                                    ImageSource.Source = SourceImage is MagickImage && !SourceImage.IsDisposed ? SourceImage.ToBitmapSource() : null;
                                    ImageTarget.Source = TargetImage is MagickImage && !TargetImage.IsDisposed ? TargetImage.ToBitmapSource() : null;
                                }
                                await Task.Delay(1);
                                DoEvents();
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                await Task.Delay(1);
                                DoEvents();
                            }
                            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
                        }

                        ImageSource.ToolTip = GetImageInfo(ImageType.Source);
                        ImageTarget.ToolTip = GetImageInfo(ImageType.Target);

                        ImageResult.Source = null;
                        if (ResultImage is MagickImage)
                        {
                            ResultImage.Dispose();
                            await Task.Delay(1);
                            DoEvents();
                        }
                        ChangeColorSpace();
                        ResultImage = await Compare(SourceImage, TargetImage, compose: compose);
                        await Task.Delay(1);
                        DoEvents();
                        if (ResultImage is MagickImage)
                        {
                            ImageResult.Source = ResultImage.ToBitmapSource();
                            await Task.Delay(1);
                            DoEvents();
                        }
                        ImageResult.ToolTip = GetImageInfo(ImageType.Result);
                        CalcDisplay(set_ratio: false);
                    }
                    catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
                    finally
                    {
                        ProcessStatus.IsIndeterminate = false;
                        await Task.Delay(1);
                        DoEvents();
                        if (_CanUpdate_ is SemaphoreSlim && _CanUpdate_.CurrentCount < 1) _CanUpdate_.Release();
                    }
                }, DispatcherPriority.Render);
            }
        }
        #endregion

        #region Image Load/Save Routines
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
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
                    var action = false;
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
                                        break;
                                    }
                                    catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
                                }
                                else
                                {
                                    try
                                    {
                                        SetImage(source ? ImageType.Source : ImageType.Target, MagickImage.FromBase64(Regex.Replace(text, @"^data:.*?;base64,", "", RegexOptions.IgnoreCase)), update: false);
                                        if (source) SourceFile = string.Empty;
                                        else TargetFile = string.Empty;
                                        action = true;
                                        break;
                                    }
#if DEBUG
                                    catch (Exception ex) { Debug.WriteLine(ex.Message); }
#else
                                    catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
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
                                        {
                                            SetImage(ImageType.Source, img, update: false);
                                            SourceFile = string.Empty;
                                        }
                                        else
                                        {
                                            SetImage(ImageType.Target, img, update: false);
                                            TargetFile = string.Empty;
                                        }
                                        action = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (action) UpdateImageViewer(assign: true, compose: LastOpIsCompose);
                }
                catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
            }, DispatcherPriority.Render);
        }

        private async void LoadImageFromFiles(string[] files, bool source = true)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var action = false;
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
                                SetImage(ImageType.Source, new MagickImage(fs), update: false);
                                SourceFile = file_s;
                                action = true;
                            }
                            using (var fs = new FileStream(file_t, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                SetImage(ImageType.Target, new MagickImage(fs), update: false);
                                TargetFile = file_t;
                                action = true;
                            }
                        }
                        else
                        {
                            if (source)
                            {
                                file_s = files.First();
                                using (var fs = new FileStream(file_s, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    SetImage(ImageType.Source, new MagickImage(fs), update: false);
                                    SourceFile = file_s;
                                    action = true;
                                }
                            }
                            else
                            {
                                file_t = files.First();
                                using (var fs = new FileStream(file_t, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    SetImage(ImageType.Target, new MagickImage(fs), update: false);
                                    TargetFile = file_t;
                                    action = true;
                                }
                            }
                        }
                        if (action) UpdateImageViewer(assign: true, compose: LastOpIsCompose);
                    }
                }
                catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
            }, DispatcherPriority.Render);
        }

        private void LoadImageFromFile(bool source = true)
        {
            var dlgOpen = new Microsoft.Win32.OpenFileDialog() { Multiselect = true, CheckFileExists = true, CheckPathExists = true, ValidateNames = true };
            dlgOpen.Filter = $"All Supported Image Files|{SupportedFiles}";
            if (dlgOpen.ShowDialog() ?? false)
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
                catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
            }
        }

        private void SaveResultToFile()
        {
            try
            {
                if (ResultImage is MagickImage && !ResultImage.IsDisposed) SaveImageToFile(ResultImage);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        private void SaveImageAs(bool source)
        {
            if (source ^ ToggleSourceTarget)
            {
                SaveImageToFile(SourceImage);
            }
            else
            {
                SaveImageToFile(TargetImage);
            }
        }

        private void SaveImageToFile(MagickImage image)
        {
            if (image is MagickImage && !image.IsDisposed)
            {
                try
                {
                    var dlgSave = new Microsoft.Win32.SaveFileDialog() {  CheckPathExists = true, ValidateNames = true, DefaultExt = ".png" };
                    dlgSave.Filter = "PNG File|*.png|JPEG File|*.jpg;*.jpeg|TIFF File|*.tif;*.tiff|BITMAP File|*.bmp";
                    dlgSave.FilterIndex = 1;
                    if (dlgSave.ShowDialog() ?? false)
                    {
                        var file = dlgSave.FileName;
                        var ext = Path.GetExtension(file);
                        var filters = dlgSave.Filter.Split('|');
                        if (string.IsNullOrEmpty(ext))
                        {
                            ext = filters[(dlgSave.FilterIndex - 1) * 2].Replace("*", "");
                            file = $"{file}{ext}";
                        }
                        image.Write(file);
                    }
                }
                catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
            }
        }
        #endregion

        #region Config Load/Save Routines
        private void LoadConfig()
        {
            Configuration appCfg =  ConfigurationManager.OpenExeConfiguration(AppExec);
            AppSettingsSection appSection = appCfg.AppSettings;
            try
            {
                if (appSection.Settings.AllKeys.Contains("CachePath"))
                {
                    var value = appSection.Settings["CachePath"].Value;
                    if (!string.IsNullOrEmpty(value)) CachePath = value;
                }

                if (appSection.Settings.AllKeys.Contains("HighlightColor"))
                {
                    var value = appSection.Settings["HighlightColor"].Value;
                    if (!string.IsNullOrEmpty(value)) HighlightColor = new MagickColor(value);
                }
                if (appSection.Settings.AllKeys.Contains("HighlightColorRecents"))
                {
                    var value = appSection.Settings["HighlightColorRecents"].Value;
                    if (!string.IsNullOrEmpty(value)) SetRecentColors(HighlightColorPick, value.Split(',').Select(v => v.Trim()));
                }
                if (appSection.Settings.AllKeys.Contains("LowlightColor"))
                {
                    var value = appSection.Settings["LowlightColor"].Value;
                    if (!string.IsNullOrEmpty(value)) LowlightColor = new MagickColor(value);
                }
                if (appSection.Settings.AllKeys.Contains("LowlightColorRecents"))
                {
                    var value = appSection.Settings["LowlightColorRecents"].Value;
                    if (!string.IsNullOrEmpty(value)) SetRecentColors(LowlightColorPick, value.Split(',').Select(v => v.Trim()));
                }
                if (appSection.Settings.AllKeys.Contains("MasklightColor"))
                {
                    var value = appSection.Settings["MasklightColor"].Value;
                    if (!string.IsNullOrEmpty(value)) MasklightColor = new MagickColor(value);
                }
                if (appSection.Settings.AllKeys.Contains("MasklightColorRecents"))
                {
                    var value = appSection.Settings["MasklightColorRecents"].Value;
                    if (!string.IsNullOrEmpty(value)) SetRecentColors(MasklightColorPick, value.Split(',').Select(v => v.Trim()));
                }
                if (appSection.Settings.AllKeys.Contains("ImageCompareFuzzy"))
                {
                    var value = ImageCompareFuzzy.Value;
                    if (double.TryParse(appSection.Settings["ImageCompareFuzzy"].Value, out value)) ImageCompareFuzzy.Value = Math.Max(0, Math.Min(100, value));
                }

                if (appSection.Settings.AllKeys.Contains("ErrorMetricMode"))
                {
                    var value = ErrorMetricMode;
                    if (Enum.TryParse<ErrorMetric>(appSection.Settings["ErrorMetricMode"].Value, out value)) ErrorMetricMode = value;
                }
                if (appSection.Settings.AllKeys.Contains("CompositeMode"))
                {
                    var size = CompositeMode;
                    if (Enum.TryParse<CompositeOperator>(appSection.Settings["CompositeMode"].Value, out size)) CompositeMode = size;
                }
                if (appSection.Settings.AllKeys.Contains("UseSmallerImage"))
                {
                    var value = UseSmallerImage.IsChecked ?? true;
                    if (bool.TryParse(appSection.Settings["UseSmallerImage"].Value, out value)) UseSmallerImage.IsChecked = value;
                }
                if (appSection.Settings.AllKeys.Contains("UseColorImage"))
                {
                    var value = UseColorImage.IsChecked ?? true;
                    if (bool.TryParse(appSection.Settings["UseColorImage"].Value, out value)) UseColorImage.IsChecked = value;
                }
                if (appSection.Settings.AllKeys.Contains("UseWeakBlur"))
                {
                    var value = UseWeakBlur.IsChecked ?? true;
                    if (bool.TryParse(appSection.Settings["UseWeakBlur"].Value, out value)) UseWeakBlur.IsChecked = value;
                }
                if (appSection.Settings.AllKeys.Contains("UseWeakSharp"))
                {
                    var value = UseWeakSharp.IsChecked ?? true;
                    if (bool.TryParse(appSection.Settings["UseWeakSharp"].Value, out value)) UseWeakSharp.IsChecked = value;
                }
                if (appSection.Settings.AllKeys.Contains("MaxCompareSize"))
                {
                    var value = MaxCompareSize;
                    if (int.TryParse(appSection.Settings["MaxCompareSize"].Value, out value)) MaxCompareSize = value;
                }
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        private void SaveConfig()
        {
            try
            {
                Configuration appCfg =  ConfigurationManager.OpenExeConfiguration(AppExec);
                AppSettingsSection appSection = appCfg.AppSettings;
                if (appSection.Settings.AllKeys.Contains("CachePath"))
                    appSection.Settings["CachePath"].Value = CachePath;
                else
                    appSection.Settings.Add("CachePath", CachePath);

                if (appSection.Settings.AllKeys.Contains("HighlightColor"))
                    appSection.Settings["HighlightColor"].Value = HighlightColor == null ? string.Empty : HighlightColor.ToHexString();
                else
                    appSection.Settings.Add("HighlightColor", HighlightColor == null ? string.Empty : HighlightColor.ToHexString());

                if (appSection.Settings.AllKeys.Contains("HighlightColorRecents"))
                    appSection.Settings["HighlightColorRecents"].Value = string.Join(", ", GetRecentHexColors(HighlightColorPick));
                else
                    appSection.Settings.Add("HighlightColorRecents", string.Join(", ", GetRecentHexColors(HighlightColorPick)));

                if (appSection.Settings.AllKeys.Contains("LowlightColor"))
                    appSection.Settings["LowlightColor"].Value = LowlightColor == null ? string.Empty : LowlightColor.ToHexString();
                else
                    appSection.Settings.Add("LowlightColor", LowlightColor == null ? string.Empty : LowlightColor.ToHexString());

                if (appSection.Settings.AllKeys.Contains("LowlightColorRecents"))
                    appSection.Settings["LowlightColorRecents"].Value = string.Join(", ", GetRecentHexColors(LowlightColorPick));
                else
                    appSection.Settings.Add("LowlightColorRecents", string.Join(", ", GetRecentHexColors(LowlightColorPick)));

                if (appSection.Settings.AllKeys.Contains("MasklightColor"))
                    appSection.Settings["MasklightColor"].Value = MasklightColor == null ? string.Empty : MasklightColor.ToHexString();
                else
                    appSection.Settings.Add("MasklightColor", MasklightColor == null ? string.Empty : MasklightColor.ToHexString());

                if (appSection.Settings.AllKeys.Contains("MasklightColorRecents"))
                    appSection.Settings["MasklightColorRecents"].Value = string.Join(", ", GetRecentHexColors(MasklightColorPick));
                else
                    appSection.Settings.Add("MasklightColorRecents", string.Join(", ", GetRecentHexColors(MasklightColorPick)));

                if (appSection.Settings.AllKeys.Contains("ImageCompareFuzzy"))
                    appSection.Settings["ImageCompareFuzzy"].Value = ImageCompareFuzzy.Value.ToString();
                else
                    appSection.Settings.Add("ImageCompareFuzzy", ImageCompareFuzzy.Value.ToString());

                if (appSection.Settings.AllKeys.Contains("ErrorMetricMode"))
                    appSection.Settings["ErrorMetricMode"].Value = ErrorMetricMode.ToString();
                else
                    appSection.Settings.Add("ErrorMetricMode", ErrorMetricMode.ToString());

                if (appSection.Settings.AllKeys.Contains("CompositeMode"))
                    appSection.Settings["CompositeMode"].Value = CompositeMode.ToString();
                else
                    appSection.Settings.Add("CompositeMode", CompositeMode.ToString());

                if (appSection.Settings.AllKeys.Contains("UseSmallerImage"))
                    appSection.Settings["UseSmallerImage"].Value = UseSmallerImage.IsChecked.ToString();
                else
                    appSection.Settings.Add("UseSmallerImage", UseSmallerImage.IsChecked.Value.ToString());

                if (appSection.Settings.AllKeys.Contains("UseColorImage"))
                    appSection.Settings["UseColorImage"].Value = UseColorImage.IsChecked.Value.ToString();
                else
                    appSection.Settings.Add("UseColorImage", UseColorImage.IsChecked.Value.ToString());

                if (appSection.Settings.AllKeys.Contains("UseWeakBlur"))
                    appSection.Settings["UseWeakBlur"].Value = UseWeakBlur.IsChecked.Value.ToString();
                else
                    appSection.Settings.Add("UseWeakBlur", UseWeakBlur.IsChecked.Value.ToString());

                if (appSection.Settings.AllKeys.Contains("UseWeakSharp"))
                    appSection.Settings["UseWeakSharp"].Value = UseWeakSharp.IsChecked.Value.ToString();
                else
                    appSection.Settings.Add("UseWeakSharp", UseWeakSharp.IsChecked.Value.ToString());

                if (appSection.Settings.AllKeys.Contains("MaxCompareSize"))
                    appSection.Settings["MaxCompareSize"].Value = MaxCompareSize.ToString();
                else
                    appSection.Settings.Add("MaxCompareSize", MaxCompareSize.ToString());

                appCfg.Save();
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }
        #endregion

        private void CreateImageOpMenu(FrameworkElement target)
        {
            bool source = target == ImageSource ? true : false;
            #region Create MenuItem
            var item_fh = new MenuItem()
            {
                Header = "Flip Horizontal",
                Uid = "FlipX",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE13C", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily }
            };
            var item_fv = new MenuItem()
            {
                Header = "Flip Vertical",
                Uid = "FlipY",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE174", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily }
            };
            var item_r090 = new MenuItem()
            {
                Header = "Rotate +90",
                Uid = "Rotate090",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE14A", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily }
            };
            var item_r180 = new MenuItem()
            {
                Header = "Rotate 180",
                Uid = "Rotate180",
                Tag = source,
                Icon = new TextBlock()
                {
                    Text = "\uE14A",
                    FontSize = DefaultFontSize,
                    FontFamily = DefaultFontFamily,
                    LayoutTransform = new RotateTransform(180)
                }
            };
            var item_r270 = new MenuItem()
            {
                Header = "Rotate -90",
                Uid = "Rotate270",
                Tag = source,
                Icon = new TextBlock()
                {
                    Text = "\uE14A",
                    FontSize = DefaultFontSize,
                    FontFamily = DefaultFontFamily,
                    LayoutTransform = new ScaleTransform(-1, 1)
                }
            };
            var item_reset = new MenuItem()
            {
                Header = "Reset Transforms",
                Uid = "ResetTransforms",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE777", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily }
            };

            var item_gray = new MenuItem()
            {
                Header = "Grayscale",
                Uid = "Grayscale",
                Tag = source,
                Icon = new TextBlock()
                {
                    Text = "\uF570",
                    FontSize = DefaultFontSize,
                    FontFamily = DefaultFontFamily,
                    Foreground = new SolidColorBrush(Colors.Gray)
                }
            };
            var item_blur = new MenuItem()
            {
                Header = "Gaussian Blur",
                Uid = "GaussianBlur",
                Tag = source,
                Icon = new TextBlock()
                {
                    Text = "\uE878",
                    FontSize = DefaultFontSize,
                    FontFamily = DefaultFontFamily,
                    Foreground = new SolidColorBrush(Colors.Gray),
                    Effect = new System.Windows.Media.Effects.BlurEffect() { Radius = 2, KernelType = System.Windows.Media.Effects.KernelType.Gaussian }
                }
            };
            var item_sharp = new MenuItem()
            {
                Header = "Unsharp Mask",
                Uid = "UsmSharp",
                Tag = source,
                Icon = new TextBlock()
                {
                    Text = "\uE879",
                    FontSize = DefaultFontSize,
                    FontFamily = DefaultFontFamily,
                    Foreground = new SolidColorBrush(Colors.Gray)
                }
            };
            var item_more = new MenuItem()
            {
                Header = "More Effects",
                Uid = "MoreEffects",
                Tag = source,                
                Icon = new TextBlock()
                {
                    Text = "\uE712",
                    FontSize = DefaultFontSize,
                    FontFamily = DefaultFontFamily,
                    Foreground = new SolidColorBrush(Colors.Gray)
                }
            };

            var item_size_to_source = new MenuItem()
            {
                Header = "Match Source Size",
                Uid = "MathcSourceSize",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE158", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily }
            };
            var item_size_to_target = new MenuItem()
            {
                Header = "Match Target Size",
                Uid = "MathcTargetSize",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE158", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily }
            };

            var item_slice_h = new MenuItem()
            {
                Header = "Slicing Horizontal",
                Uid = "SlicingX",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE745", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = new SolidColorBrush(Colors.Gray) }
            };
            var item_slice_v = new MenuItem()
            {
                Header = "Slicing Vertical",
                Uid = "SlicingY",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE746", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = new SolidColorBrush(Colors.Gray) }
            };
            var item_reload = new MenuItem()
            {
                Header = "Reload Image",
                Uid = "ReloadImage",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE117", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily }
            };
            var item_copyinfo = new MenuItem()
            {
                Header = "Copy Image Info",
                Uid = "CopyImageInfo",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE16F", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily }
            };
            var item_saveas = new MenuItem()
            {
                Header = "Save As ...",
                Uid = "SaveAs",
                Tag = source,
                Visibility = Visibility.Collapsed,
                Icon = new TextBlock() { Text = "\uE105", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily }
            };
            #endregion
            #region Create MenuItem Click event handles
            item_fh.Click += (obj, evt) => { FlopImage((bool)(obj as MenuItem).Tag); };
            item_fv.Click += (obj, evt) => { FlipImage((bool)(obj as MenuItem).Tag); };
            item_r090.Click += (obj, evt) => { RotateImage((bool)(obj as MenuItem).Tag, 90); };
            item_r180.Click += (obj, evt) => { RotateImage((bool)(obj as MenuItem).Tag, 180); };
            item_r270.Click += (obj, evt) => { RotateImage((bool)(obj as MenuItem).Tag, 270); };
            item_reset.Click += (obj, evt) => { ResetImage((bool)(obj as MenuItem).Tag); };

            item_gray.Click += (obj, evt) => { GrayscaleImage((bool)(obj as MenuItem).Tag); };
            item_blur.Click += (obj, evt) => { BlurImage((bool)(obj as MenuItem).Tag); };
            item_sharp.Click += (obj, evt) => { SharpImage((bool)(obj as MenuItem).Tag); };
            item_more.Click += (obj, evt) => { };

            item_size_to_source.Click += (obj, evt) => { ResizeToImage(false); };
            item_size_to_target.Click += (obj, evt) => { ResizeToImage(true); };

            item_slice_h.Click += (obj, evt) => { SlicingImage((bool)(obj as MenuItem).Tag, vertical: false); };
            item_slice_v.Click += (obj, evt) => { SlicingImage((bool)(obj as MenuItem).Tag, vertical: true); };

            item_reload.Click += (obj, evt) => { ReloadImage((bool)(obj as MenuItem).Tag); };

            item_copyinfo.Click += (obj, evt) => { CopyImageInfo((bool)(obj as MenuItem).Tag); };
            item_saveas.Click += (obj, evt) => { SaveImageAs((bool)(obj as MenuItem).Tag); };
            #endregion
            #region Add MenuItems to ContextMenu
            var result = new ContextMenu() { PlacementTarget = target };
            result.Items.Add(item_fh);
            result.Items.Add(item_fv);
            result.Items.Add(new Separator());
            result.Items.Add(item_r090);
            result.Items.Add(item_r270);
            result.Items.Add(item_r180);
            result.Items.Add(new Separator());
            result.Items.Add(item_reset);
            result.Items.Add(new Separator());
            result.Items.Add(item_gray);
            result.Items.Add(item_blur);
            result.Items.Add(item_sharp);
            result.Items.Add(item_more);
            result.Items.Add(new Separator());
            result.Items.Add(item_size_to_source);
            result.Items.Add(item_size_to_target);
            result.Items.Add(new Separator());
            result.Items.Add(item_slice_h);
            result.Items.Add(item_slice_v);
            result.Items.Add(new Separator());
            result.Items.Add(item_reload);
            result.Items.Add(new Separator());
            result.Items.Add(item_copyinfo);
            result.Items.Add(item_saveas);
            #endregion
            #region MoreEffects MenuItem
            var item_more_oil = new MenuItem()
            {
                Header = "Oil Paint",
                Uid = "OilPaint",
                Tag = source
            };
            var item_more_charcoal = new MenuItem()
            {
                Header = "Charcoal",
                Uid = "Charcoal",
                Tag = source
            };
            var item_more_meanshift = new MenuItem()
            {
                Header = "Mean Shift",
                Uid = "MeanShift",
                Tag = source
            };
            #endregion
            #region MoreEffects MenuItem Click event handles
            item_more_oil.Click += (obj, evt) => { OilImage((bool)(obj as MenuItem).Tag); };
            item_more_charcoal.Click += (obj, evt) => { CharcoalImage((bool)(obj as MenuItem).Tag); };
            item_more_meanshift.Click += (obj, evt) => { MeanShiftImage((bool)(obj as MenuItem).Tag); };
            #endregion
            #region Add MoreEffects MenuItems to MoreEffects
            item_more.Items.Add(item_more_oil);
            item_more.Items.Add(item_more_charcoal);
            item_more.Items.Add(new Separator());
            item_more.Items.Add(item_more_meanshift);
            #endregion
            target.ContextMenu = result;
            target.ContextMenuOpening += (obj, evt) =>
            {
                item_saveas.Visibility = Keyboard.Modifiers == ModifierKeys.Shift ? Visibility.Visible : Visibility.Collapsed;
            };
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();

            #region Some Default UI Settings
            Icon = new BitmapImage(new Uri("pack://application:,,,/ImageCompare;component/Resources/Compare.ico"));
            DefaultWindowTitle = Title;
            DefaultCompareToolTip = ImageCompare.ToolTip as string;
            DefaultComposeToolTip = ImageCompose.ToolTip as string;

            if (DefaultFontFamily == null) DefaultFontFamily = new FontFamily(DefaultFontFamilyName);
            #endregion

            #region Magick.Net Default Settings
            try
            {
                var magick_cache = Path.IsPathRooted(CachePath) ? CachePath : Path.Combine(AppPath, CachePath);
                if (!Directory.Exists(magick_cache)) Directory.CreateDirectory(magick_cache);
                if (Directory.Exists(magick_cache)) MagickAnyCPU.CacheDirectory = magick_cache;
                ImageMagick.OpenCL.IsEnabled = true;
                ImageMagick.OpenCL.SetCacheDirectory(magick_cache);
                ImageMagick.ResourceLimits.Memory = 256 * 1024 * 1024;
                ImageMagick.ResourceLimits.LimitMemory(new Percentage(5));
                ImageMagick.ResourceLimits.Thread = 2;
                //ImageMagick.ResourceLimits.Area = 4096 * 4096;
                //ImageMagick.ResourceLimits.Throttle = 
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }

            var fmts = GetSupportedImageFormats().Keys.ToList().Skip(4).Select(f => $"*.{f}");
            SupportedFiles = string.Join(";", fmts);

            CompareResizeGeometry = new MagickGeometry($"{MaxCompareSize}x{MaxCompareSize}>");
            #endregion

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
            #region Create Channels Selector
            var names = new string[] { "Default", "All", "None", "-", "RGB", "Red", "Green", "Blue", "-", "CMYK", "Cyan", "Magenta", "Yellow", "Black", "-", "Grays", "Gray", "-", "Alpha", "Opacity", "TrueAlpha", "Composite", "Index", "Sync" };
            var rgb = new string[] { "Red", "Green", "Blue", "RGB" };
            var cmyk = new string[] { "Cyan", "Magenta", "Yellow", "Black", "CMYK" };
            var gray = new string[] { "Grays, Gray" };
            var cm_channels_mode = new ContextMenu() { PlacementTarget = UsedChannels };
            //foreach (string v in Enum.GetNames(typeof(Channels)))
            foreach (string v in names)
            {
                dynamic item = null;
                if (v.Equals("-")) item = new Separator();
                else
                {
                    item = new MenuItem()
                    {
                        Header = v,
                        Tag = v.Equals("-") ? null : Enum.Parse(typeof(Channels), v, true),
                        IsChecked = (v.Equals("Default") ? true : false),
                    };
                    (item as MenuItem).Click += (obj, evt) =>
                    {
                        foreach (var m in cm_channels_mode.Items) { if (m is MenuItem) (m as MenuItem).IsChecked = false; }
                        if (obj is MenuItem)
                        {                           
                            var menu = obj as MenuItem;
                            menu.IsChecked = true;
                            CompareImageChannels = (Channels)menu.Tag;
                            UpdateImageViewer(compose: LastOpIsCompose);
                        }
                    };
                }
                //if (cm_channels_mode.Items.Cast<MenuItem>().Where(i => i.Header.Equals(item.Header)).Count() < 1)
                cm_channels_mode.Items.Add(item);
            }
            cm_channels_mode.Items.LiveGroupingProperties.Add("Header");
            cm_channels_mode.Items.LiveGroupingProperties.Add("Tag");
            cm_channels_mode.Items.LiveGroupingProperties.Add("RGB");
            cm_channels_mode.Items.LiveGroupingProperties.Add("CMYK");
            cm_channels_mode.Items.LiveGroupingProperties.Add("Gray");
            cm_channels_mode.Items.LiveGroupingProperties.Add("Common");
            cm_channels_mode.Items.IsLiveGrouping = true;
            UsedChannels.ContextMenu = cm_channels_mode;
            #endregion

            #region Create Image Flip/Rotate/Effects Menu
            CreateImageOpMenu(ImageSource);
            CreateImageOpMenu(ImageTarget);
            #endregion

            #region Result Color Defaults Value
            //UseSmallerImage.IsChecked = true;
            if (HighlightColor != null)
            {
                var ch = HighlightColor.ToByteArray();
                HighlightColorPick.SelectedColor = Color.FromArgb(ch[3], ch[0], ch[1], ch[2]);
            }
            if (LowlightColor != null)
            {
                var cl = LowlightColor.ToByteArray();
                LowlightColorPick.SelectedColor = Color.FromArgb(cl[3], cl[0], cl[1], cl[2]);
            }
            if (MasklightColor != null)
            {
                var cm = MasklightColor.ToByteArray();
                MasklightColorPick.SelectedColor = Color.FromArgb(cm[3], cm[0], cm[1], cm[2]);
            }
            #endregion

            #region Default Zoom Ratio
            ZoomFitAll.IsChecked = true;
            ImageActions_Click(ZoomFitAll, e);
            #endregion

            var args = Environment.GetCommandLineArgs();
            LoadImageFromFiles(args.Skip(1).ToArray());
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            SaveConfig();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CalcDisplay(set_ratio: true);
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            var fmts = e.Data.GetFormats(true);
#if DEBUG
            Debug.WriteLine(string.Join(", ", fmts));
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
            if (ZoomFitNone.IsChecked ?? false && (ImageSource.Source != null || ImageTarget.Source != null))
            {
                ZoomRatio.Value += e.Delta < 0 ? -1 * ZoomRatio.SmallChange : ZoomRatio.SmallChange;
                if (sender is Viewbox)
                {
                    SyncOffset(GetOffset(sender as Viewbox));
                }
            }
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
                        mouse_start = e.GetPosition(ImageSourceScroll);
                        mouse_origin = new Point(ImageSourceScroll.HorizontalOffset, ImageSourceScroll.VerticalOffset);
                    }
                    else if (sender == ImageTargetBox)
                    {
                        mouse_start = e.GetPosition(ImageTargetScroll);
                        mouse_origin = new Point(ImageTargetScroll.HorizontalOffset, ImageTargetScroll.VerticalOffset);
                    }
                    else if (sender == ImageResultBox)
                    {
                        mouse_start = e.GetPosition(ImageResultScroll);
                        mouse_origin = new Point(ImageResultScroll.HorizontalOffset, ImageResultScroll.VerticalOffset);
                    }
                }
            }
        }

        private void ImageBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var offset = sender is Viewbox ? CalcOffset(sender as Viewbox, e) : new Point(-1, -1);
#if DEBUG
                Debug.WriteLine($"Original : [{mouse_origin.X:F0}, {mouse_origin.Y:F0}], Start : [{mouse_start.X:F0}, {mouse_start.Y:F0}] => Move : [{offset.X:F0}, {offset.Y:F0}]");
                //Debug.WriteLine($"Move Y: {offset_y}");
#endif
                SyncOffset(offset);
            }
        }

        private void ZoomRatio_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded && (ZoomFitNone.IsChecked ?? false))
            {
                e.Handled = true;
                LastZoomRatio = ZoomRatio.Value;
            }
        }

        private void ImageCompareFuzzy_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            e.Handled = true;
            UpdateImageViewer(compose: LastOpIsCompose);
        }

        private void MaxCompareSizeValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                int value = MaxCompareSize;
                if (int.TryParse(MaxCompareSizeValue.Text, out value)) MaxCompareSize = Math.Max(0, Math.Min(2048, value));
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        private void ColorPick_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (sender == HighlightColorPick)
            {
                var c = (sender as ColorPicker).SelectedColor ?? null;
                HighlightColor = c == null || c == Colors.Transparent ? null : MagickColor.FromRgba(c.Value.R, c.Value.G, c.Value.B, c.Value.A);
            }
            else if (sender == LowlightColorPick)
            {
                var c = (sender as ColorPicker).SelectedColor ?? null;
                LowlightColor = c == null || c == Colors.Transparent ? null : MagickColor.FromRgba(c.Value.R, c.Value.G, c.Value.B, c.Value.A);
            }
            else if (sender == MasklightColorPick)
            {
                var c = (sender as ColorPicker).SelectedColor ?? null;
                MasklightColor = c == null || c == Colors.Transparent ? null : MagickColor.FromRgba(c.Value.R, c.Value.G, c.Value.B, c.Value.A);
            }
        }

        private void ImageActions_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
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
                if (ZoomFitAll.IsChecked ?? false)
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
            else if (sender == UseSmallerImage)
            {
                SetImage(ImageType.Source, SourceOriginal, update: false);
                SetImage(ImageType.Target, TargetOriginal, update: false);
                UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            else if (sender == UseColorImage)
            {
                ChangeColorSpace();
                UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            else if(sender == UsedChannels)
            {
                UsedChannels.ContextMenu.IsOpen = true;
            }
        }

    }
}
