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

using Microsoft.WindowsAPICodePack.Dialogs;
using ImageMagick;
using Xceed.Wpf.Toolkit;

namespace ImageCompare
{
    public enum ImageType { Source = 0, Target = 1, Result = 2 }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string AppExec = Application.ResourceAssembly.CodeBase.ToString().Replace("file:///", "").Replace("/", "\\");
        private static string AppPath = Path.GetDirectoryName(AppExec);
        private static string AppName = Path.GetFileNameWithoutExtension(AppPath);
        private static string CachePath =  "cache";

        private string DefaultCompareToolTip { get; set; } = string.Empty;
        private string DefaultComposeToolTip { get; set; } = string.Empty;

        private int MaxCompareSize { get; set; } = 1024;
        private MagickGeometry CompareResizeGeometry { get; set; } = null;

        private MagickImage SourceOriginal { get; set; } = null;
        private MagickImage TargetOriginal { get; set; } = null;
        private bool SourceLoaded { get; set; } = false;
        private bool TargetLoaded { get; set; } = false;

        private MagickImage SourceImage { get; set; } = null;
        private MagickImage TargetImage { get; set; } = null;
        private MagickImage ResultImage { get; set; } = null;

        private double ImageDistance { get; set; } = 0;
        private double LastZoomRatio { get; set; } = 1;
        private bool LastOpIsCompose { get; set; } = false;

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

        private bool ToggleSourceTarget { get { return (ImageToggle.IsChecked ?? false); } }

        private ContextMenu cm_compare_mode = null;
        private ContextMenu cm_compose_mode = null;

        private Point start;
        private Point origin;

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
        #endregion

        #region Image Processing Routines
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

        private void SetImage(ImageType type, MagickImage image, bool update = true)
        {
            try
            {
                if (type != ImageType.Result)
                {
                    bool source  = type == ImageType.Source ? true : false;
                    if (source ^ ToggleSourceTarget)
                    {
                        if (image != SourceOriginal)
                        {
                            if (SourceOriginal is MagickImage && !SourceOriginal.IsDisposed) SourceOriginal.Dispose();
                            SourceOriginal = new MagickImage(image);
                        }

                        if (SourceImage is MagickImage && !SourceImage.IsDisposed) SourceImage.Dispose();
                        SourceImage = new MagickImage(image);
                        if (UseSmallerImage.IsChecked ?? false)
                        {
                            SourceImage.Resize(CompareResizeGeometry);
                            SourceImage.RePage();
                        }
                        FlipX_Source = false;
                        FlipY_Source = false;
                        Rotate_Source = 0;
                        SourceLoaded = true;
                    }
                    else
                    {
                        if (image != TargetOriginal)
                        {
                            if (TargetOriginal is MagickImage && !TargetOriginal.IsDisposed) TargetOriginal.Dispose();
                            TargetOriginal = new MagickImage(image);
                        }

                        if (TargetImage is MagickImage && !TargetImage.IsDisposed) TargetImage.Dispose();
                        TargetImage = new MagickImage(image);
                        if (UseSmallerImage.IsChecked ?? false)
                        {
                            TargetImage.Resize(CompareResizeGeometry);
                            TargetImage.RePage();
                        }
                        FlipX_Target = false;
                        FlipY_Target = false;
                        Rotate_Target = 0;
                        TargetLoaded = true;
                    }
                }
                else
                {
                    if (ResultImage is MagickImage) ResultImage.Dispose();
                    ResultImage = image;
                }
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
            if (update) UpdateImageViewer(assign: true, compose: LastOpIsCompose);
        }

        private void SetImage(ImageType type, IMagickImage<float> image, bool update = true)
        {
            try
            {
#if Q16HDRI
                SetImage(type, new MagickImage(image), update: update);
#endif
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
        }

        private void SetImage(ImageType type, IMagickImage<byte> image, bool update = true)
        {
            try
            {
#if Q16
                SetImage(type, new MagickImage(image), update: update);
#endif
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
        }

        private void GetExif(MagickImage image)
        {
            if (image is MagickImage)
            {
                var exif = image.GetExifProfile() ?? new ExifProfile();
                var tag = exif.GetValue(ExifTag.XPTitle);
                if (tag != null) { var text = Encoding.Unicode.GetString(tag.Value).Trim('\0').Trim(); }
#if DEBUG
                //Debug.WriteLine(text);
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
                //tip.Add($"Colors         = {image.TotalColors}");
                tip.Add($"Color Space    = {Path.GetFileName(image.ColorSpace.ToString())}");
#if Q16HDRI
                tip.Add($"Memory Usage   = {SmartFileSize(image.Width * image.Height * image.ChannelCount * image.Depth * 4 / 8)}");
#elif Q16
                tip.Add($"Memory Usage   = {SmartFileSize(image.Width * image.Height * image.ChannelCount * image.Depth * 2 / 8)}");
#else
                tip.Add($"Memory Usage   = {SmartFileSize(image.Width * image.Height * image.ChannelCount * image.Depth / 8)}");
#endif
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
                if (SourceImage is MagickImage && !SourceImage.IsDisposed)
                {
                    SourceImage.Rotate(value);
                    Rotate_Source += value;
                    Rotate_Source %= 360;
                    action = true;
                }
            }
            else
            {
                if (TargetImage is MagickImage && !TargetImage.IsDisposed)
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
                if (SourceImage is MagickImage && !SourceImage.IsDisposed)
                {
                    SourceImage.Flip();
                    FlipY_Source = !FlipY_Source;
                    action = true;
                }
            }
            else
            {
                if (TargetImage is MagickImage && !TargetImage.IsDisposed)
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
                if (SourceImage is MagickImage && !SourceImage.IsDisposed)
                {
                    SourceImage.Flop();
                    FlipX_Source = !FlipX_Source;
                    action = true;
                }
            }
            else
            {
                if (TargetImage is MagickImage && !TargetImage.IsDisposed)
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
                if (SourceImage is MagickImage && !SourceImage.IsDisposed)
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
                if (TargetImage is MagickImage && !TargetImage.IsDisposed)
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

        private void ResizeToImage(bool source)
        {
            var action = false;
            if (source ^ ToggleSourceTarget)
            {
                if (SourceImage is MagickImage && TargetImage is MagickImage)
                {
                    if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                    if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                    SourceImage.Resize(TargetImage.Width, TargetImage.Height);
                    SourceImage.RePage();
                    action = true;
                }
            }
            else
            {
                if (TargetImage is MagickImage && SourceImage is MagickImage)
                {
                    if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                    if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                    TargetImage.Resize(SourceImage.Width, SourceImage.Height);
                    TargetImage.RePage();
                    action = true;
                }
            }
            if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
        }

        private void SlicingImage(bool source, bool vertical)
        {
            var action = false;
            try
            {
                if (source ^ ToggleSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null && SourceImage is MagickImage) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());

                        var geometry = vertical ? new MagickGeometry(new Percentage(50), new Percentage(100)) : new MagickGeometry(new Percentage(100), new Percentage(50));
                        var result = SourceImage.CropToTiles(geometry);
                        if (result.Count() >= 2)
                        {
                            if (SourceImage != null) SourceImage.Dispose();
                            SourceImage = new MagickImage(result.FirstOrDefault());
                            SourceImage.RePage();
                            if (TargetImage != null) TargetImage.Dispose();
                            TargetImage = new MagickImage(result.Skip(1).Take(1).FirstOrDefault());
                            TargetImage.RePage();
                            action = true;
                        }
                    }
                }
                else
                {
                    if (TargetImage is MagickImage)
                    {
                        if (SourceOriginal == null && SourceImage is MagickImage) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());

                        var geometry = vertical ? new MagickGeometry(new Percentage(50), new Percentage(100)) : new MagickGeometry(new Percentage(100), new Percentage(50));
                        geometry.IgnoreAspectRatio = true;
                        geometry.FillArea = true;
                        geometry.Greater = true;
                        geometry.Less = true;
                        var result = TargetImage.CropToTiles(geometry);
                        if (result.Count() >= 2)
                        {
                            if (TargetImage != null) TargetImage.Dispose();
                            TargetImage = new MagickImage(result.FirstOrDefault());
                            TargetImage.RePage();
                            if (SourceImage != null) SourceImage.Dispose();
                            SourceImage = new MagickImage(result.Skip(1).Take(1).FirstOrDefault());
                            SourceImage.RePage();
                            action = true;
                        }
                    }
                }
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
            if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
        }

        private void ReloadImage(bool source)
        {
            var action = false;
            try
            {
                if (source ^ ToggleSourceTarget)
                {
                    if (SourceOriginal is MagickImage && !SourceOriginal.IsDisposed)
                    {
                        SetImage(ImageType.Source, SourceOriginal, update: false);
                        action = true;
                    }
                    else
                    {
                        if (SourceImage is MagickImage && !SourceImage.IsDisposed) SourceImage.Dispose(); SourceImage = null;
                        SourceOriginal = null;
                        action = true;
                    }
                }
                else
                {
                    if (TargetOriginal is MagickImage && !TargetOriginal.IsDisposed)
                    {
                        SetImage(ImageType.Target, TargetOriginal, update: false);
                        action = true;
                    }
                    else
                    {
                        if (TargetImage is MagickImage && !TargetImage.IsDisposed) TargetImage.Dispose(); TargetImage = null;
                        TargetOriginal = null;
                        action = true;
                    }
                }
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
            if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
        }

        private void CleanImage()
        {
            if (SourceImage is MagickImage && !SourceImage.IsDisposed) SourceImage.Dispose(); SourceImage = null;
            if (TargetImage is MagickImage && !TargetImage.IsDisposed) TargetImage.Dispose(); TargetImage = null;
            if (ResultImage is MagickImage && !ResultImage.IsDisposed) ResultImage.Dispose(); ResultImage = null;

            if (SourceOriginal is MagickImage && !SourceOriginal.IsDisposed) SourceOriginal.Dispose(); SourceOriginal = null;
            if (TargetOriginal is MagickImage && !TargetOriginal.IsDisposed) TargetOriginal.Dispose(); TargetOriginal = null;

            if (ImageSource.Source != null) { ImageSource.Source = null; }
            if (ImageTarget.Source != null) { ImageTarget.Source = null; }
            if (ImageResult.Source != null) { ImageResult.Source = null; }

            SourceLoaded = false;
            TargetLoaded = false;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();
        }

        private async Task<MagickImage> Compare(MagickImage source, MagickImage target, bool compose = false)
        {
            MagickImage result = null;
            await Dispatcher.InvokeAsync(async () =>
            {
                var st = Stopwatch.StartNew();
                var tip = new List<string>();
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
                                tip.Add($"Mode       : {CompositeMode.ToString()}");
                                result = new MagickImage(diff.Clone());
                                await Task.Delay(1);
                                DoEvents();
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
                                tip.Add($"Mode       : {ErrorMetricMode.ToString()}");
                                tip.Add($"Difference : {distance:F4}");
                                result = new MagickImage(diff.Clone());
                                await Task.Delay(1);
                                DoEvents();
                            }
                        }
                    }
                }
                catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
                finally
                {
                    st.Stop();
                    tip.Add($"Elapsed    : {TimeSpan.FromTicks(st.ElapsedTicks).TotalSeconds:F4} s");
                    if (compose)
                    {
                        ImageCompare.ToolTip = DefaultCompareToolTip;
                        ImageCompose.ToolTip = tip.Count > 1 ? string.Join(Environment.NewLine, tip) : null;
                    }
                    else
                    {
                        ImageCompare.ToolTip = tip.Count > 1 ? string.Join(Environment.NewLine, tip) : null;
                        ImageCompose.ToolTip = DefaultCompareToolTip;
                    }
                }
            }, DispatcherPriority.Render);
            return (result);
        }
        #endregion

        #region Image Display Routines
        private SemaphoreSlim _CanUpdate_ = new SemaphoreSlim(1, 1);

        private void CalcDisplay(bool set_ratio = true)
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

        private async void UpdateImageViewer(bool compose = false, bool assign = false)
        {
            if (await _CanUpdate_.WaitAsync(TimeSpan.FromMilliseconds(200)))
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
#if DEBUG
                        Debug.WriteLine("UpdateImageViewer");
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
                            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
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
                        GetExif(SourceImage);
                    }
                    catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
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
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
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
                                    }
                                    catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
                                }
                                else
                                {
                                    try
                                    {
                                        SetImage(source ? ImageType.Source : ImageType.Target, MagickImage.FromBase64(Regex.Replace(text, @"^data:.*?;base64,", "", RegexOptions.IgnoreCase)), update: false);
                                        action = true;
                                    }
#if DEBUG
                                    catch (Exception ex) { Debug.WriteLine(ex.Message); }
#else
                                    catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
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
                                            SetImage(ImageType.Source, img, update: false);
                                        else
                                            SetImage(ImageType.Target, img, update: false);
                                        action = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (action) UpdateImageViewer(assign: true, compose: LastOpIsCompose);
                }
                catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
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
                                action = true;
                            }
                            using (var fs = new FileStream(file_t, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                SetImage(ImageType.Target, new MagickImage(fs), update: false);
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
                                    action = true;
                                }
                            }
                            else
                            {
                                file_t = files.First();
                                using (var fs = new FileStream(file_t, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    SetImage(ImageType.Target, new MagickImage(fs), update: false);
                                    action = true;
                                }
                            }
                        }
                        if (action) UpdateImageViewer(assign: true, compose: LastOpIsCompose);
                    }
                }
                catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
            }, DispatcherPriority.Render);
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
                catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
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
                catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
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
                if (appSection.Settings.AllKeys.Contains("LowlightColor"))
                {
                    var value = appSection.Settings["LowlightColor"].Value;
                    if (!string.IsNullOrEmpty(value)) LowlightColor = new MagickColor(value);
                }
                if (appSection.Settings.AllKeys.Contains("MasklightColor"))
                {
                    var value = appSection.Settings["MasklightColor"].Value;
                    if (!string.IsNullOrEmpty(value)) MasklightColor = new MagickColor(value);
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
                if (appSection.Settings.AllKeys.Contains("MaxCompareSize"))
                {
                    var value = MaxCompareSize;
                    if (int.TryParse(appSection.Settings["MaxCompareSize"].Value, out value)) MaxCompareSize = value;
                }
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
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

                if (appSection.Settings.AllKeys.Contains("LowlightColor"))
                    appSection.Settings["LowlightColor"].Value = LowlightColor == null ? string.Empty : LowlightColor.ToHexString();
                else
                    appSection.Settings.Add("LowlightColor", LowlightColor == null ? string.Empty : LowlightColor.ToHexString());

                if (appSection.Settings.AllKeys.Contains("MasklightColor"))
                    appSection.Settings["MasklightColor"].Value = MasklightColor == null ? string.Empty : MasklightColor.ToHexString();
                else
                    appSection.Settings.Add("MasklightColor", MasklightColor == null ? string.Empty : MasklightColor.ToHexString());

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

                if (appSection.Settings.AllKeys.Contains("MaxCompareSize"))
                    appSection.Settings["MaxCompareSize"].Value = MaxCompareSize.ToString();
                else
                    appSection.Settings.Add("MaxCompareSize", MaxCompareSize.ToString());

                appCfg.Save();
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
        }
        #endregion

        private void CreateImageOpMenu(FrameworkElement target)
        {
            bool source = target == ImageSource ? true : false;

            var item_fh = new MenuItem()
            {
                Header = "Flip Horizontal",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE13C", FontSize = 16, FontFamily = new FontFamily("Segoe MDL2 Assets") }
            };
            var item_fv = new MenuItem()
            {
                Header = "Flip Vertical",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE174", FontSize = 16, FontFamily = new FontFamily("Segoe MDL2 Assets") }
            };
            var item_r090 = new MenuItem()
            {
                Header = "Rotate +90",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE14A", FontSize = 16, FontFamily = new FontFamily("Segoe MDL2 Assets") }
            };
            var item_r180 = new MenuItem()
            {
                Header = "Rotate 180",
                Tag = source,
                Icon = new TextBlock()
                {
                    Text = "\uE14A",
                    FontSize = 16,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    LayoutTransform = new RotateTransform(180)
                }
            };
            var item_r270 = new MenuItem()
            {
                Header = "Rotate -90",
                Tag = source,
                Icon = new TextBlock()
                {
                    Text = "\uE14A",
                    FontSize = 16,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    LayoutTransform = new ScaleTransform(-1, 1)
                }
            };
            var item_reset = new MenuItem()
            {
                Header = "Reset Transforms",
                Tag = source,
                Icon = new TextBlock() { Text = "\uE777", FontSize = 16, FontFamily = new FontFamily("Segoe MDL2 Assets") }
            };

            var item_same_size_source = new MenuItem()
            {
                Header = "Same To Source Size",
                Tag = source,
                Icon = new TextBlock() { Text = "\xE158", FontSize = 16, FontFamily = new FontFamily("Segoe MDL2 Assets") }
            };
            var item_same_size_target = new MenuItem()
            {
                Header = "Same To Target Size",
                Tag = source,
                Icon = new TextBlock() { Text = "\xE158", FontSize = 16, FontFamily = new FontFamily("Segoe MDL2 Assets") }
            };
            var item_slice_h = new MenuItem()
            {
                Header = "Slice Horizontal",
                Tag = source,
                Icon = new TextBlock() { Text = "\xE745", FontSize = 16, FontFamily = new FontFamily("Segoe MDL2 Assets") }
            };
            var item_slice_v = new MenuItem()
            {
                Header = "Slice Vertical",
                Tag = source,
                Icon = new TextBlock() { Text = "\xE746", FontSize = 16, FontFamily = new FontFamily("Segoe MDL2 Assets") }
            };
            var item_reload = new MenuItem()
            {
                Header = "Reload Image",
                Tag = source,
                Icon = new TextBlock() { Text = "\xE117", FontSize = 16, FontFamily = new FontFamily("Segoe MDL2 Assets") }
            };

            item_fh.Click += (obj, evt) => { FlopImage((bool)(obj as MenuItem).Tag); };
            item_fv.Click += (obj, evt) => { FlipImage((bool)(obj as MenuItem).Tag); };
            item_r090.Click += (obj, evt) => { RotateImage((bool)(obj as MenuItem).Tag, 90); };
            item_r180.Click += (obj, evt) => { RotateImage((bool)(obj as MenuItem).Tag, 180); };
            item_r270.Click += (obj, evt) => { RotateImage((bool)(obj as MenuItem).Tag, 270); };
            item_reset.Click += (obj, evt) => { ResetImage((bool)(obj as MenuItem).Tag); };

            item_same_size_source.Click += (obj, evt) => { ResizeToImage((bool)(obj as MenuItem).Tag); };
            item_same_size_target.Click += (obj, evt) => { ResizeToImage((bool)(obj as MenuItem).Tag); };
            item_slice_h.Click += (obj, evt) => { SlicingImage((bool)(obj as MenuItem).Tag, vertical: false); };
            item_slice_v.Click += (obj, evt) => { SlicingImage((bool)(obj as MenuItem).Tag, vertical: true); };

            item_reload.Click += (obj, evt) => { ReloadImage((bool)(obj as MenuItem).Tag); };

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
            result.Items.Add(item_same_size_source);
            result.Items.Add(item_same_size_target);
            result.Items.Add(item_slice_h);
            result.Items.Add(item_slice_v);
            result.Items.Add(new Separator());
            result.Items.Add(item_reload);

            target.ContextMenu = result;
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Icon = new BitmapImage(new Uri("pack://application:,,,/ImageCompare;component/Resources/Compare.ico"));

            LoadConfig();

            try
            {
                var magick_cache = Path.IsPathRooted(CachePath) ? CachePath : Path.Combine(AppPath, CachePath);
                if (!Directory.Exists(magick_cache)) Directory.CreateDirectory(magick_cache);
                if (Directory.Exists(magick_cache)) MagickAnyCPU.CacheDirectory = magick_cache;
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }

            CompareResizeGeometry = new MagickGeometry($"{MaxCompareSize}x{MaxCompareSize}>");

            DefaultCompareToolTip = ImageCompare.ToolTip as string;
            DefaultComposeToolTip = ImageCompose.ToolTip as string;

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

            ZoomFitAll.IsChecked = true;
            ImageActions_Click(ZoomFitAll, e);

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

                Debug.WriteLine($"Original : [{origin.X:F0}, {origin.Y:F0}], Start : [{start.X:F0}, {start.Y:F0}] => Move : [{offset_x:F0}, {offset_y:F0}]");
                //Debug.WriteLine($"Move Y: {offset_y}");
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
                //        catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
                //        finally { e.Handled = true; }
                //        //ActionZoomFitOp = false; }
                //    }).Invoke();
                //}
                e.Handled = true;
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
            //        catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
            //        finally { e.Handled = true; }
            //    }).Invoke();
            //}
            e.Handled = true;
            UpdateImageViewer(compose: LastOpIsCompose);
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
        }

        private void MaxCompareSizeValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                int value = MaxCompareSize;
                if (int.TryParse(MaxCompareSizeValue.Text, out value)) MaxCompareSize = Math.Max(0, Math.Min(2048, value));
            }
            catch(Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
        }

        private void LightColorPick_MouseDown(object sender, MouseEventArgs e)
        {
            //if (sender == HighlightColorPick)
            //{
            //    PickupColor(sender as UIElement);
            //}
            //else if (sender == LowlightColorPick)
            //{
            //    PickupColor(sender as UIElement);
            //}
            //else if (sender == MasklightColorPick)
            //{
            //    PickupColor(sender as UIElement);
            //}
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
    }
}
