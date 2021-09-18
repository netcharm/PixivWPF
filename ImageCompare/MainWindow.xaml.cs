﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
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
    public partial class MainWindow : Window, IDisposable
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

        private Rect LastPositionSize { get; set; } = new Rect();

        private CultureInfo DefaultCultureInfo { get; set; } = CultureInfo.CurrentCulture;
        #endregion

        #region Magick.Net Settings
        private int MaxCompareSize { get; set; } = 1024;
        private MagickGeometry CompareResizeGeometry { get; set; } = null;

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
        private bool WeakBlur { get { return (UseWeakBlur.IsChecked ?? false); } }
        private bool WeakSharp { get { return (UseWeakSharp.IsChecked ?? false); } }
        private bool WeakEffects { get { return (UseWeakEffects.IsChecked ?? false); } }
        #endregion

        //private bool ExchangeSourceTarget { get { return (ImageExchange.IsChecked ?? false); } }

        private ContextMenu cm_compare_mode = null;
        private ContextMenu cm_compose_mode = null;

        private List<FrameworkElement> cm_image_source = new List<FrameworkElement>();
        private List<FrameworkElement> cm_image_target = new List<FrameworkElement>();

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

        #region Image Display Routines
        private SemaphoreSlim _CanUpdate_ = new SemaphoreSlim(1, 1);
        private Dictionary<Color, string> ColorNames = new Dictionary<Color, string>();
        private Point mouse_start;
        private Point mouse_origin;

        private void GetColorNames()
        {
            var cpl = (typeof(Colors) as Type).GetProperties();
        }

        private Point GetSystemDPI()
        {
            var result = new Point(96, 96);
            try
            {
                System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                var dpiXProperty = typeof(SystemParameters).GetProperty("DpiX", flags);
                //var dpiYProperty = typeof(SystemParameters).GetProperty("DpiY", flags);
                var dpiYProperty = typeof(SystemParameters).GetProperty("Dpi", flags);
                if (dpiXProperty != null) { result.X = (int)dpiXProperty.GetValue(null, null); }
                if (dpiYProperty != null) { result.Y = (int)dpiYProperty.GetValue(null, null); }
            }
            catch (Exception ex) { ex.Message.ShowMessage(); }
            return (result);
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
                    catch (Exception ex) { ex.Message.ShowMessage(); continue; }
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
                    var image_s = ImageSource.GetInformation();
                    var image_t = ImageTarget.GetInformation();
                    var image_r = ImageResult.GetInformation();
                    if (image_s.ValidCurrent)
                    {
                        ImageSourceBox.Width = image_s.Current.Width;
                        ImageSourceBox.Height = image_s.Current.Height;
                    }
                    if (image_t.ValidCurrent)
                    {
                        ImageTargetBox.Width = image_t.Current.Width;
                        ImageTargetBox.Height = image_t.Current.Height;
                    }
                    if (image_r.ValidCurrent)
                    {
                        ImageResultBox.Width = image_r.Current.Width;
                        ImageResultBox.Height = image_r.Current.Height;
                    }
                    ZoomRatio.Value = LastZoomRatio;
                }

                if (ZoomFitNone.IsChecked ?? false) ZoomRatio.IsEnabled = true;
                else ZoomRatio.IsEnabled = false;

                CalcZoomRatio();
            }
            catch (Exception ex) { ex.Message.ShowMessage(); }
        }

        private void CalcZoomRatio()
        {
            try
            {
                var image_s = ImageSource.GetInformation();
                var image_t = ImageTarget.GetInformation();
                var image_r = ImageResult.GetInformation();

                if (image_s.ValidCurrent || image_t.ValidCurrent)
                {
                    var scroll  = image_s.ValidCurrent ? ImageSourceScroll : ImageTargetScroll;
                    var image  = image_s.ValidCurrent ? image_s.Current : image_t.Current;

                    var width = image.Width;
                    var height = image.Height;

                    if (image_s.ValidCurrent && image_t.ValidCurrent)
                    {
                        width = Math.Max(image_s.Current.Width, image_t.Current.Width);
                        height = Math.Max(image_s.Current.Height, image_t.Current.Height);
                    }


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
                        var targetX = width;
                        var targetY = image.Height;
                        var ratio = scroll.ActualWidth / targetX;
                        var delta = scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Hidden || targetY * ratio <= scroll.ActualHeight ? 0 : 14;
                        ZoomRatio.Value = (scroll.ActualWidth - delta) / targetX;
                    }
                    else if (ZoomFitHeight.IsChecked ?? false)
                    {
                        var targetX = image.Width;
                        var targetY = height;
                        var ratio = scroll.ActualHeight / targetY;
                        var delta = scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Hidden || targetX * ratio <= scroll.ActualWidth ? 0 : 14;
                        ZoomRatio.Value = (scroll.ActualHeight - delta) / targetY;
                    }
                }
            }
            catch (Exception ex) { ex.Message.ShowMessage(); }
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

                        var image_s = ImageSource.GetInformation();
                        var image_t = ImageTarget.GetInformation();
                        var image_r = ImageResult.GetInformation();

                        if (assign || ImageSource.Source == null || ImageTarget.Source == null)
                        {
                            try
                            {
                                if (CompareImageForceScale)
                                {
                                    if (image_s.CurrentSize.Width > MaxCompareSize || image_s.CurrentSize.Height > MaxCompareSize)
                                        image_s.Reload(CompareResizeGeometry);
                                    if (image_t.CurrentSize.Width > MaxCompareSize && image_t.CurrentSize.Height > MaxCompareSize)
                                        image_t.Reload(CompareResizeGeometry);
                                }
                                else
                                {
                                    if (image_s.CurrentSize.Width != image_s.OriginalSize.Width && image_s.CurrentSize.Height != image_s.OriginalSize.Height)
                                        image_s.Reload();
                                    if (image_t.CurrentSize.Width != image_t.OriginalSize.Width && image_t.CurrentSize.Height != image_t.OriginalSize.Height)
                                        image_t.Reload();
                                }
                                ImageSource.Source = image_s.Source;
                                ImageTarget.Source = image_t.Source;

                                await Task.Delay(1);
                                DoEvents();
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                                await Task.Delay(1);
                                DoEvents();
                            }
                            catch (Exception ex) { ex.Message.ShowMessage(); }
                        }

                        ImageSource.ToolTip = "Waiting".T();
                        ImageTarget.ToolTip = "Waiting".T();

                        ImageResult.Source = null;
                        if (image_r.ValidCurrent)
                        {
                            image_r.Dispose();
                            await Task.Delay(1);
                            DoEvents();
                        }
                        image_s.ChangeColorSpace(CompareImageForceColor);
                        image_t.ChangeColorSpace(CompareImageForceColor);

                        image_r.Current = await Compare(image_s.Current, image_t.Current, compose: compose);

                        await Task.Delay(1);
                        DoEvents();
                        ImageResult.Source = image_r.Source;
                        await Task.Delay(1);
                        DoEvents();

                        ImageResult.ToolTip = "Waiting".T();
                        CalcDisplay(set_ratio: false);
                    }
                    catch (Exception ex) { ex.Message.ShowMessage(); }
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
        private async void CopyImageToOther(bool source = true)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var action = false;
                    var image_s = ImageSource.GetInformation();
                    var image_t = ImageTarget.GetInformation();
                    if (source && image_s.ValidCurrent)
                    {
                        image_t.Current = new MagickImage(image_s.Current);
                        action = true;
                    }
                    else if(image_t.ValidCurrent)
                    {
                        image_s.Current = new MagickImage(image_t.Current);
                        action = true;
                    }
                    if (action) UpdateImageViewer(assign: true, compose: LastOpIsCompose);
                }
                catch (Exception ex) { ex.Message.ShowMessage(); }
            }, DispatcherPriority.Render);
        }

        private async void LoadImageFromPrevFile(bool source = true)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                var ret = false;
                try
                {
                    var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                    ret = await image.LoadImageFromPrevFile();
                    if (ret) UpdateImageViewer(assign: true);
                }
                catch (Exception ex) { ex.Message.ShowMessage(); }
                return (ret);
            });
        }

        private async void LoadImageFromNextFile(bool source = true)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                var ret = false;
                try
                {
                    var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                    ret = await image.LoadImageFromNextFile();
                    if(ret) UpdateImageViewer(assign: true);
                }
                catch (Exception ex) { ex.Message.ShowMessage(); }
                return (ret);
            });
        }

        private async void LoadImageFromFiles(string[] files, bool source = true)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var action = false;
                    files = files.Where(f => !string.IsNullOrEmpty(f)).Where(f => Extensions.AllSupportedFormats.Keys.ToList().Select(e => $".{e.ToLower()}").ToList().Contains(Path.GetExtension(f).ToLower())).ToArray();
                    var count = files.Length;
                    var image_s = ImageSource.GetInformation();
                    var image_t = ImageTarget.GetInformation();
                    if (count > 0)
                    {
                        var file_s = string.Empty;
                        var file_t = string.Empty;
                        if (count >= 2)
                        {
                            file_s = files.First();
                            file_t = files.Skip(1).First();

                            action |= image_s.LoadImageFromFile(file_s, false);
                            action |= image_t.LoadImageFromFile(file_t, false);
                        }
                        else
                        {
                            var image  = source ? image_s : image_t;
                            file_s = files.First();
                            action |= image.LoadImageFromFile(file_s, false);
                        }
                        if (action) UpdateImageViewer(assign: true, compose: LastOpIsCompose);
                    }
                }
                catch (Exception ex) { ex.Message.ShowMessage(); }
            }, DispatcherPriority.Render);
        }

        private void SaveImageAs(bool source)
        {
            if (source)
                ImageSource.GetInformation().Save();
            else
                ImageTarget.GetInformation().Save();
        }
        #endregion

        #region Config Load/Save Routines
        private void LoadConfig()
        {
            Configuration appCfg =  ConfigurationManager.OpenExeConfiguration(AppExec);
            AppSettingsSection appSection = appCfg.AppSettings;
            try
            {
                if (appSection.Settings.AllKeys.Contains("WindowPosition"))
                {
                    var value = appSection.Settings["WindowPosition"].Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        try
                        {
                            var rect = Rect.Parse(value);
                            Top = rect.Top;
                            Left = rect.Left;
                            Width = Math.Min(MaxWidth, Math.Max(MinWidth, rect.Width));
                            Height = Math.Min(MaxHeight, Math.Max(MinHeight, rect.Height));
                        }
                        catch { }
                    }
                }

                if (appSection.Settings.AllKeys.Contains("CachePath"))
                {
                    var value = appSection.Settings["CachePath"].Value;
                    if (!string.IsNullOrEmpty(value)) CachePath = value;
                }

                if (appSection.Settings.AllKeys.Contains("UILanguage"))
                {
                    var value = appSection.Settings["UILanguage"].Value;
                    DefaultCultureInfo = CultureInfo.GetCultureInfoByIetfLanguageTag(value);
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

                if (appSection.Settings.AllKeys.Contains("SimpleTrimCropBoundingBox"))
                {
                    var value = SimpleTrimCropBoundingBox;
                    if (bool.TryParse(appSection.Settings["SimpleTrimCropBoundingBox"].Value, out value)) SimpleTrimCropBoundingBox = value;
                }

            }
            catch (Exception ex) { ex.Message.ShowMessage(); }
        }

        private void SaveConfig()
        {
            try
            {
                Configuration appCfg =  ConfigurationManager.OpenExeConfiguration(AppExec);
                AppSettingsSection appSection = appCfg.AppSettings;

                var rect = new Rect(
                        LastPositionSize.Top, LastPositionSize.Left,
                        Math.Min(MaxWidth, Math.Max(MinWidth, LastPositionSize.Width)),
                        Math.Min(MaxHeight, Math.Max(MinHeight, LastPositionSize.Height))
                    );
                if (appSection.Settings.AllKeys.Contains("WindowPosition"))
                    appSection.Settings["WindowPosition"].Value = rect.ToString();
                else
                    appSection.Settings.Add("WindowPosition", rect.ToString());

                if (appSection.Settings.AllKeys.Contains("CachePath"))
                    appSection.Settings["CachePath"].Value = CachePath;
                else
                    appSection.Settings.Add("CachePath", CachePath);

                if (appSection.Settings.AllKeys.Contains("CachePath"))
                    appSection.Settings["CachePath"].Value = CachePath;
                else
                    appSection.Settings.Add("CachePath", CachePath);

                if (appSection.Settings.AllKeys.Contains("UILanguage"))
                    appSection.Settings["UILanguage"].Value = DefaultCultureInfo.IetfLanguageTag;
                else
                    appSection.Settings.Add("UILanguage", DefaultCultureInfo.IetfLanguageTag);

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

                if (appSection.Settings.AllKeys.Contains("SimpleTrimCropBoundingBox"))
                    appSection.Settings["SimpleTrimCropBoundingBox"].Value = SimpleTrimCropBoundingBox.ToString();
                else
                    appSection.Settings.Add("SimpleTrimCropBoundingBox", SimpleTrimCropBoundingBox.ToString());


                appCfg.Save();
            }
            catch (Exception ex) { ex.Message.ShowMessage(); }
        }
        #endregion

        private void CreateImageOpMenu(FrameworkElement target)
        {
            bool source = target == ImageSource ? true : false;
            var color_gray = new SolidColorBrush(Colors.Gray);
            var effect_blur = new System.Windows.Media.Effects.BlurEffect() { Radius = 2, KernelType = System.Windows.Media.Effects.KernelType.Gaussian };

            var items = source ? cm_image_source : cm_image_target;
            if (items != null) items.Clear();
            else items = new List<FrameworkElement>();

            if (items.Count <= 0)
            {
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
                    Icon = new TextBlock() { Text = "\uE14A", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, LayoutTransform = new RotateTransform(180) }
                };
                var item_r270 = new MenuItem()
                {
                    Header = "Rotate -90",
                    Uid = "Rotate270",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE14A", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, LayoutTransform = new ScaleTransform(-1, 1) }
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
                    Icon = new TextBlock() { Text = "\uF570", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = color_gray }
                };
                var item_blur = new MenuItem()
                {
                    Header = "Gaussian Blur",
                    Uid = "GaussianBlur",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE878", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = color_gray, Effect = effect_blur }
                };
                var item_sharp = new MenuItem()
                {
                    Header = "Unsharp Mask",
                    Uid = "UsmSharp",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE879", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = color_gray }
                };
                var item_more = new MenuItem()
                {
                    Header = "More Effects",
                    Uid = "MoreEffects",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE712", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = color_gray }
                };

                var item_size_crop = new MenuItem()
                {
                    Header = "Crop BoundingBox",
                    Uid = "CropBoundingBox",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\xE123", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = color_gray }
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
                    Icon = new TextBlock() { Text = "\uE745", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = color_gray }
                };
                var item_slice_v = new MenuItem()
                {
                    Header = "Slicing Vertical",
                    Uid = "SlicingY",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE746", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = color_gray }
                };

                var item_copyto_source = new MenuItem()
                {
                    Header = "Copy Image To Source",
                    Uid = "CopyToSource",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE16F", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = color_gray }
                };
                var item_copyto_target = new MenuItem()
                {
                    Header = "Copy Image To Target",
                    Uid = "CopyToTarget",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE16F", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = color_gray }
                };

                var item_load_prev = new MenuItem()
                {
                    Header = "Load Prev Image File",
                    Uid = "LoadPrevImageFile",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE1A5", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = color_gray }
                };
                var item_load_next = new MenuItem()
                {
                    Header = "Load Next Image File",
                    Uid = "LoadNextImageFile",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE1A5", FontSize = DefaultFontSize, FontFamily = DefaultFontFamily, Foreground = color_gray }
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
                item_fh.Click += (obj, evt) => { this.InvokeAsync(() => { FlopImage((bool)(obj as MenuItem).Tag); }); };
                item_fv.Click += (obj, evt) => { this.InvokeAsync(() => { FlipImage((bool)(obj as MenuItem).Tag); }); };
                item_r090.Click += (obj, evt) => { this.InvokeAsync(() => { RotateImage((bool)(obj as MenuItem).Tag, 90); }); };
                item_r180.Click += (obj, evt) => { this.InvokeAsync(() => { RotateImage((bool)(obj as MenuItem).Tag, 180); }); };
                item_r270.Click += (obj, evt) => { this.InvokeAsync(() => { RotateImage((bool)(obj as MenuItem).Tag, 270); }); };
                item_reset.Click += (obj, evt) => { this.InvokeAsync(() => { ResetImage((bool)(obj as MenuItem).Tag); }); };

                item_gray.Click += (obj, evt) => { this.InvokeAsync(() => { GrayscaleImage((bool)(obj as MenuItem).Tag); }); };
                item_blur.Click += (obj, evt) => { this.InvokeAsync(() => { BlurImage((bool)(obj as MenuItem).Tag); }); };
                item_sharp.Click += (obj, evt) => { this.InvokeAsync(() => { SharpImage((bool)(obj as MenuItem).Tag); }); };

                item_size_crop.Click += (obj, evt) => { this.InvokeAsync(() => { CropImage((bool)(obj as MenuItem).Tag); }); };
                item_size_to_source.Click += (obj, evt) => { this.InvokeAsync(() => { ResizeToImage(false); }); };
                item_size_to_target.Click += (obj, evt) => { this.InvokeAsync(() => { ResizeToImage(true); }); };

                item_slice_h.Click += (obj, evt) => { this.InvokeAsync(() => { SlicingImage((bool)(obj as MenuItem).Tag, vertical: false); }); };
                item_slice_v.Click += (obj, evt) => { this.InvokeAsync(() => { SlicingImage((bool)(obj as MenuItem).Tag, vertical: true); }); };

                item_copyto_source.Click += (obj, evt) => { this.InvokeAsync(() => { CopyImageToOther(source); }); };
                item_copyto_target.Click += (obj, evt) => { this.InvokeAsync(() => { CopyImageToOther(source); }); };

                item_load_prev.Click += (obj, evt) => { this.InvokeAsync(() => { LoadImageFromPrevFile((bool)(obj as MenuItem).Tag); }); };
                item_load_next.Click += (obj, evt) => { this.InvokeAsync(() => { LoadImageFromNextFile((bool)(obj as MenuItem).Tag); }); };

                item_reload.Click += (obj, evt) => { this.InvokeAsync(() => { ReloadImage((bool)(obj as MenuItem).Tag); }); };

                item_copyinfo.Click += (obj, evt) => { this.InvokeAsync(() => { CopyImageInfo((bool)(obj as MenuItem).Tag); }); };
                item_saveas.Click += (obj, evt) => { SaveImageAs((bool)(obj as MenuItem).Tag); };
                #endregion
                #region Add MenuItems to ContextMenu
                items.Add(item_fh);
                items.Add(item_fv);
                items.Add(new Separator());
                items.Add(item_r090);
                items.Add(item_r270);
                items.Add(item_r180);
                items.Add(new Separator());
                items.Add(item_reset);
                items.Add(new Separator());
                items.Add(item_gray);
                items.Add(item_blur);
                items.Add(item_sharp);
                items.Add(item_more);
                items.Add(new Separator());
                items.Add(item_size_crop);
                items.Add(item_size_to_source);
                items.Add(item_size_to_target);
                items.Add(new Separator());
                items.Add(item_slice_h);
                items.Add(item_slice_v);
                items.Add(new Separator());
                items.Add(item_copyto_source);
                items.Add(item_copyto_target);
                items.Add(item_load_prev);
                items.Add(item_load_next);
                items.Add(new Separator());
                items.Add(item_reload);
                items.Add(new Separator());
                items.Add(item_copyinfo);
                items.Add(item_saveas);
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
                var item_more_autolevel = new MenuItem()
                {
                    Header = "Auto Level",
                    Uid = "AutoLevel",
                    Tag = source
                };
                var item_more_autocontrast = new MenuItem()
                {
                    Header = "Auto Contrast",
                    Uid = "AutoContrast",
                    Tag = source
                };
                var item_more_autowhitebalance = new MenuItem()
                {
                    Header = "Auto White Balance",
                    Uid = "AutoWhiteBalance",
                    Tag = source
                };
                var item_more_autoenhance = new MenuItem()
                {
                    Header = "Auto Enhance",
                    Uid = "AutoEnhance",
                    Tag = source
                };
                var item_more_autoequalize = new MenuItem()
                {
                    Header = "Auto Equalize",
                    Uid = "AutoEqualize",
                    Tag = source
                };
                var item_more_autogamma = new MenuItem()
                {
                    Header = "Auto Gamma",
                    Uid = "AutoGamma",
                    Tag = source
                };
                var item_more_autothreshold = new MenuItem()
                {
                    Header = "Auto Threshold",
                    Uid = "AutoThreshold",
                    Tag = source
                };
                var item_more_autovignette = new MenuItem()
                {
                    Header = "Auto Vignette",
                    Uid = "AutoVignette",
                    Tag = source
                };

                var item_more_blueshift = new MenuItem()
                {
                    Header = "Blue Shift",
                    Uid = "BlueShift",
                    Tag = source
                };
                var item_more_remap = new MenuItem()
                {
                    Header = "Re-Map Color",
                    Uid = "ReMapColor",
                    Tag = source
                };
                var item_more_haldclut = new MenuItem()
                {
                    Header = "Hald Clut",
                    Uid = "HaldClut",
                    Tag = source
                };

                var item_more_medianfilter = new MenuItem()
                {
                    Header = "Median Filter",
                    Uid = "MedianFilter",
                    Tag = source
                };
                var item_more_invert = new MenuItem()
                {
                    Header = "Invert",
                    Uid = "Invert",
                    Tag = source
                };
                var item_more_posterize = new MenuItem()
                {
                    Header = "Posterize",
                    Uid = "Posterize",
                    Tag = source
                };
                var item_more_polaroid = new MenuItem()
                {
                    Header = "Polaroid",
                    Uid = "Polaroid",
                    Tag = source
                };

                var item_more_meanshift = new MenuItem()
                {
                    Header = "Mean Shift",
                    Uid = "MeanShift",
                    Tag = source
                };
                var item_more_kmeans = new MenuItem()
                {
                    Header = "K-Means Cluster",
                    Uid = "KmeansCluster",
                    Tag = source
                };

                var item_more_fillflood = new MenuItem()
                {
                    Header = "Fill BoundingBox",
                    Uid = "FillBoundingBox",
                    Tag = source
                };
                #endregion
                #region MoreEffects MenuItem Click event handles
                item_more_oil.Click += (obj, evt) => { this.InvokeAsync(() => { OilImage((bool)(obj as MenuItem).Tag); }); };
                item_more_charcoal.Click += (obj, evt) => { this.InvokeAsync(() => { CharcoalImage((bool)(obj as MenuItem).Tag); }); };

                item_more_autoequalize.Click += (obj, evt) => { this.InvokeAsync(() => { AutoEqualizeImage((bool)(obj as MenuItem).Tag); }); };
                item_more_autoenhance.Click += (obj, evt) => { this.InvokeAsync(() => { AutoEnhanceImage((bool)(obj as MenuItem).Tag); }); };
                item_more_autolevel.Click += (obj, evt) => { this.InvokeAsync(() => { AutoLevelImage((bool)(obj as MenuItem).Tag); }); };
                item_more_autocontrast.Click += (obj, evt) => { this.InvokeAsync(() => { AutoContrastImage((bool)(obj as MenuItem).Tag); }); };
                item_more_autowhitebalance.Click += (obj, evt) => { this.InvokeAsync(() => { AutoWhiteBalanceImage((bool)(obj as MenuItem).Tag); }); };
                item_more_autogamma.Click += (obj, evt) => { this.InvokeAsync(() => { AutoGammaImage((bool)(obj as MenuItem).Tag); }); };

                item_more_autovignette.Click += (obj, evt) => { this.InvokeAsync(() => { AutoVignetteImage((bool)(obj as MenuItem).Tag); }); };
                item_more_invert.Click += (obj, evt) => { this.InvokeAsync(() => { InvertImage((bool)(obj as MenuItem).Tag); }); };
                item_more_polaroid.Click += (obj, evt) => { this.InvokeAsync(() => { PolaroidImage((bool)(obj as MenuItem).Tag); }); };
                item_more_posterize.Click += (obj, evt) => { this.InvokeAsync(() => { PosterizeImage((bool)(obj as MenuItem).Tag); }); };
                item_more_medianfilter.Click += (obj, evt) => { this.InvokeAsync(() => { MedianFilterImage((bool)(obj as MenuItem).Tag); }); };

                item_more_blueshift.Click += (obj, evt) => { this.InvokeAsync(() => { BlueShiftImage((bool)(obj as MenuItem).Tag); }); };
                item_more_autothreshold.Click += (obj, evt) => { this.InvokeAsync(() => { AutoThresholdImage((bool)(obj as MenuItem).Tag); }); };
                item_more_remap.Click += (obj, evt) => { this.InvokeAsync(() => { RemapImage((bool)(obj as MenuItem).Tag); }); };
                item_more_haldclut.Click += (obj, evt) => { this.InvokeAsync(() => { HaldClutImage((bool)(obj as MenuItem).Tag); }); };

                item_more_meanshift.Click += (obj, evt) => { this.InvokeAsync(() => { MeanShiftImage((bool)(obj as MenuItem).Tag); }); };
                item_more_kmeans.Click += (obj, evt) => { this.InvokeAsync(() => { KmeansImage((bool)(obj as MenuItem).Tag); }); };

                item_more_fillflood.Click += (obj, evt) => { this.InvokeAsync(() => { FillOutBoundBoxImage((bool)(obj as MenuItem).Tag); }); };
                #endregion
                #region Add MoreEffects MenuItems to MoreEffects
                item_more.Items.Add(item_more_autoequalize);
                item_more.Items.Add(item_more_autoenhance);
                item_more.Items.Add(item_more_autolevel);
                item_more.Items.Add(item_more_autocontrast);
                item_more.Items.Add(item_more_autowhitebalance);
                item_more.Items.Add(item_more_autogamma);
                item_more.Items.Add(new Separator());
                item_more.Items.Add(item_more_oil);
                item_more.Items.Add(item_more_charcoal);
                item_more.Items.Add(item_more_invert);
                item_more.Items.Add(item_more_posterize);
                item_more.Items.Add(item_more_polaroid);
                item_more.Items.Add(new Separator());
                item_more.Items.Add(item_more_autovignette);
                item_more.Items.Add(item_more_blueshift);
                item_more.Items.Add(item_more_autothreshold);
                item_more.Items.Add(item_more_remap);
                item_more.Items.Add(item_more_haldclut);
                item_more.Items.Add(new Separator());
                item_more.Items.Add(item_more_medianfilter);
                item_more.Items.Add(item_more_meanshift);
                item_more.Items.Add(item_more_kmeans);
                item_more.Items.Add(new Separator());
                item_more.Items.Add(item_more_fillflood);
                #endregion
                target.ContextMenuOpening += (obj, evt) =>
                {
                    item_saveas.Visibility = Keyboard.Modifiers == ModifierKeys.Shift ? Visibility.Visible : Visibility.Collapsed;
                    item_copyto_source.Visibility = source ? Visibility.Collapsed : Visibility.Visible;
                    item_copyto_target.Visibility = source ? Visibility.Visible : Visibility.Collapsed;
                    var show_load = false;
                    if (obj is Image) { show_load = !string.IsNullOrEmpty((obj as Image).GetInformation().FileName); }
                    item_load_prev.Visibility = show_load ? Visibility.Visible : Visibility.Collapsed;
                    item_load_next.Visibility = show_load ? Visibility.Visible : Visibility.Collapsed;
                };
            }

            if (target.ContextMenu is ContextMenu)
            {
                foreach (var item in target.ContextMenu.Items)
                {
                    if (item is MenuItem) (item as MenuItem).Items.Clear();
                }
            }
            else
            {
                var result = new ContextMenu() { PlacementTarget = target, Tag = source };
                target.ContextMenu = result;
            }
            items.Locale();
            target.ContextMenu.ItemsSource = new ObservableCollection<FrameworkElement>(items);
        }

        private void LocaleUI(CultureInfo culture = null)
        {
            Title = $"{Uid}.Title".T(culture) ?? Title;
            ImageToolBar.Locale();
            if (ImageSource.ContextMenu is ContextMenu) CreateImageOpMenu(ImageSource);
            if (ImageTarget.ContextMenu is ContextMenu) CreateImageOpMenu(ImageTarget);
            ImageSource.ToolTip = ImageSource.GetInformation().GetImageInfo();
            ImageTarget.ToolTip = ImageTarget.GetInformation().GetImageInfo();
            ImageResult.ToolTip = ImageResult.GetInformation().GetImageInfo();
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                    if (_CanUpdate_ is SemaphoreSlim)
                    {
                        if (_CanUpdate_.CurrentCount < 1) _CanUpdate_.Release();
                        _CanUpdate_.Dispose();
                    }
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~MainWindow() {
        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;
            LocaleUI(DefaultCultureInfo);

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
                ImageMagick.ResourceLimits.Thread = 4;
                //ImageMagick.ResourceLimits.Area = 4096 * 4096;
                //ImageMagick.ResourceLimits.Throttle = 
            }
            catch (Exception ex) { ex.Message.ShowMessage(); }

            Extensions.AllSupportedFormats = Extensions.GetSupportedImageFormats();
            Extensions.AllSupportedExts = Extensions.AllSupportedFormats.Keys.ToList().Skip(4).Select(ext => $".{ext.ToLower()}").Where(ext => !ext.Equals(".txt")).ToList();
            var exts = Extensions.AllSupportedExts.Select(ext => $"*{ext}");
            Extensions.AllSupportedFiles = string.Join(";", exts);
            Extensions.AllSupportedFilters = string.Join("|", Extensions.AllSupportedFormats.Select(f => $"{f.Value}|*.{f.Key}"));

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

            if (ImageSource.Tag == null) ImageSource.Tag = new ImageInformation() { Tagetment = ImageSource };
            if (ImageTarget.Tag == null) ImageTarget.Tag = new ImageInformation() { Tagetment = ImageTarget };
            if (ImageResult.Tag == null) ImageResult.Tag = new ImageInformation() { Tagetment = ImageResult };

            var args = Environment.GetCommandLineArgs();
            LoadImageFromFiles(args.Skip(1).ToArray());
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if(WindowState == System.Windows.WindowState.Normal)
                LastPositionSize = new Rect(Top, Left, Width, Height);
            SaveConfig();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Normal)
                LastPositionSize = new Rect(Top, Left, Width, Height);
            CalcDisplay(set_ratio: true);
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState != System.Windows.WindowState.Normal)
                LastPositionSize = new Rect(Top, Left, Width, Height);
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

        private Key _last_key_ = Key.None;
        private DateTime _last_key_time_ = DateTime.Now;
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsDown)
            {
                try
                {
                    e.Handled = false;
                    if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.W || e.SystemKey == Key.W))
                    {
                        e.Handled = true;
                        Close();
                    }
                    else if ((e.Key == Key.Escape || e.SystemKey == Key.Escape) && _last_key_ == Key.Escape)
                    {
                        if ((DateTime.Now - _last_key_time_).TotalMilliseconds < 150)
                        {
                            e.Handled = true;
                            Close();
                        }
                    }
                    else if (e.Key == Key.F1 || e.SystemKey == Key.F1)
                    {
                        e.Handled = true;
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                            LoadImageFromPrevFile(true);
                        else if (Keyboard.Modifiers == ModifierKeys.Control)
                            LoadImageFromNextFile(true);
                        else
                            ImageActions_Click(ImageOpenSource, e);
                    }
                    else if (e.Key == Key.F2 || e.SystemKey == Key.F2)
                    {
                        e.Handled = true;
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                            LoadImageFromPrevFile(false);
                        else if (Keyboard.Modifiers == ModifierKeys.Control)
                            LoadImageFromNextFile(false);
                        else
                            ImageActions_Click(ImageOpenTarget, e);
                    }
                    else if (e.Key == Key.F3 || e.SystemKey == Key.F3)
                    {
                        e.Handled = true;
                        ImageActions_Click(ImagePasteSource, e);
                    }
                    else if (e.Key == Key.F4 || e.SystemKey == Key.F4)
                    {
                        e.Handled = true;
                        ImageActions_Click(ImagePasteTarget, e);
                    }
                    else if (e.Key == Key.F5 || e.SystemKey == Key.F5)
                    {
                        e.Handled = true;
                        if (Keyboard.Modifiers == ModifierKeys.Shift && ImageCompare.ContextMenu is ContextMenu)
                            ImageCompare.ContextMenu.IsOpen = true;
                        else
                            ImageActions_Click(ImageCompare, e);
                    }
                    else if (e.Key == Key.F6 || e.SystemKey == Key.F6)
                    {
                        e.Handled = true;
                        if (Keyboard.Modifiers == ModifierKeys.Shift && ImageCompose.ContextMenu is ContextMenu)
                            ImageCompose.ContextMenu.IsOpen = true;
                        else
                            ImageActions_Click(ImageCompose, e);
                    }
                    else if (e.Key == Key.F7 || e.SystemKey == Key.F7)
                    {
                        e.Handled = true;
                        ImageActions_Click(ImageCopyResult, e);
                    }
                    else if (e.Key == Key.F8 || e.SystemKey == Key.F8)
                    {
                        e.Handled = true;
                        ImageActions_Click(ImageSaveResult, e);
                    }
                    else if (e.Key == Key.F9 || e.SystemKey == Key.F9)
                    {
                        e.Handled = true;
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            if (ZoomFitNone.IsChecked ?? false) { ZoomFitHeight.IsChecked = true; ImageActions_Click(ZoomFitHeight, e); }
                            else if (ZoomFitAll.IsChecked ?? false) { ZoomFitNone.IsChecked = true; ImageActions_Click(ZoomFitNone, e); }
                            else if (ZoomFitWidth.IsChecked ?? false) { ZoomFitAll.IsChecked = true; ImageActions_Click(ZoomFitAll, e); }
                            else if (ZoomFitHeight.IsChecked ?? false) { ZoomFitWidth.IsChecked = true; ImageActions_Click(ZoomFitWidth, e); }
                        }
                        else
                        {
                            if (ZoomFitNone.IsChecked ?? false) { ZoomFitAll.IsChecked = true; ImageActions_Click(ZoomFitAll, e); }
                            else if (ZoomFitAll.IsChecked ?? false) { ZoomFitWidth.IsChecked = true; ImageActions_Click(ZoomFitWidth, e); }
                            else if (ZoomFitWidth.IsChecked ?? false) { ZoomFitHeight.IsChecked = true; ImageActions_Click(ZoomFitHeight, e); }
                            else if (ZoomFitHeight.IsChecked ?? false) { ZoomFitNone.IsChecked = true; ImageActions_Click(ZoomFitNone, e); }
                        }
                    }
                    else if (e.Key == Key.F10 || e.SystemKey == Key.F10)
                    {
                        e.Handled = true;
                        ImageExchange.IsChecked = !ImageExchange.IsChecked;
                        ImageActions_Click(ImageExchange, e);
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Alt && (e.Key == Key.S || e.SystemKey == Key.S))
                    {
                        e.Handled = true;
                        if (ImageSource.Source != null && ImageSource.ContextMenu != null) ImageSource.ContextMenu.IsOpen = true;
                    }
                    else if (Keyboard.Modifiers == ModifierKeys.Alt && (e.Key == Key.T || e.SystemKey == Key.T))
                    {
                        e.Handled = true;
                        if (ImageTarget.Source != null && ImageTarget.ContextMenu != null) ImageTarget.ContextMenu.IsOpen = true;
                    }
                    _last_key_ = e.Key;
                    _last_key_time_ = DateTime.Now;
                }
                catch (Exception ex) { ex.Message.ShowMessage(); }
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

        private async void ImageBox_MouseDown(object sender, MouseButtonEventArgs e)
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
                else if (e.ChangedButton == MouseButton.Middle)
                {
                    Close();
                }
                else if (e.ChangedButton == MouseButton.XButton1)
                {
                    var action = false;
                    if (sender == ImageSourceBox) action |= await ImageSource.GetInformation().LoadImageFromNextFile();
                    else if (sender == ImageTargetBox) action |= await ImageTarget.GetInformation().LoadImageFromNextFile();
                    if (action) UpdateImageViewer(assign: true);
                }
                else if (e.ChangedButton == MouseButton.XButton2)
                {
                    var action = false;
                    if (sender == ImageSourceBox) action |= await ImageSource.GetInformation().LoadImageFromPrevFile();
                    else if (sender == ImageTargetBox) action |= await ImageTarget.GetInformation().LoadImageFromPrevFile();
                    if (action) UpdateImageViewer(assign: true);
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
                try
                {
                    e.Handled = true;
                    LastZoomRatio = ZoomRatio.Value;
                }
                catch (Exception ex) { ex.Message.ShowMessage(); }
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
                if (int.TryParse(MaxCompareSizeValue.Text, out value))
                {
                    MaxCompareSize = Math.Max(0, Math.Min(2048, value));
                    CompareResizeGeometry = new MagickGeometry($"{MaxCompareSize}x{MaxCompareSize}>");
                }
            }
            catch (Exception ex) { ex.Message.ShowMessage(); }
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

        private void Image_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            try
            {
                if (sender is Image)
                {
#if DEBUG
                    this.InvokeAsync(async () =>
#else
                    this.InvokeAsync(() =>
#endif
                    {
                        try
                        {
                            var image = sender as Image;
                            if (image.ToolTip is string && (image.ToolTip as string).Equals("Waiting".T(), StringComparison.CurrentCultureIgnoreCase))
                            {
#if DEBUG
                                image.ToolTip = await image.GetInformation().GetImageInfo();
#else
                                image.ToolTip = image.GetInformation().GetImageInfo();
#endif
                            }
                        }
                        catch (Exception ex) { ex.Message.ShowMessage(); }
                    });
                }
            }
            catch (Exception ex) { ex.Message.ShowMessage(); }
        }

        private async void ImageActions_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender == UILanguage)
            {
                if (UILanguage.ContextMenu is ContextMenu) UILanguage.ContextMenu.IsOpen = true;
            }
            else if (sender == UILanguageEn)
            {
                DefaultCultureInfo = CultureInfo.GetCultureInfo("en");
                LocaleUI(DefaultCultureInfo);
            }
            else if (sender == UILanguageCn)
            {
                DefaultCultureInfo = CultureInfo.GetCultureInfo("zh-Hans");
                LocaleUI(DefaultCultureInfo);
            }
            else if (sender == UILanguageTw)
            {
                DefaultCultureInfo = CultureInfo.GetCultureInfo("zh-Hant");
                LocaleUI(DefaultCultureInfo);
            }
            else if (sender == UILanguageJa)
            {
                DefaultCultureInfo = CultureInfo.GetCultureInfo("ja-JP");
                LocaleUI(DefaultCultureInfo);
            }
            else if (sender == ImageOpenSource)
            {
                var action = ImageSource.GetInformation().LoadImageFromFile();
                if (action) UpdateImageViewer(assign: true, compose: LastOpIsCompose);
            }
            else if (sender == ImageOpenTarget)
            {
                var action = ImageTarget.GetInformation().LoadImageFromFile();
                if (action) UpdateImageViewer(assign: true, compose: LastOpIsCompose);
            }
            else if (sender == ImagePasteSource)
            {
                var action = await ImageSource.GetInformation().LoadImageFromClipboard();
                if (action) UpdateImageViewer(assign: true, compose: LastOpIsCompose);
            }
            else if (sender == ImagePasteTarget)
            {
                var action = await ImageTarget.GetInformation().LoadImageFromClipboard();
                if(action) UpdateImageViewer(assign: true, compose: LastOpIsCompose);
            }
            else if (sender == ImageClear)
            {
                CleanImage();
            }
            else if (sender == ImageExchange)
            {
                var st = ImageSource.Tag;
                var tt = ImageTarget.Tag;
                ImageSource.Tag = tt;
                ImageTarget.Tag = st;
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
                ImageResult.GetInformation().CopyToClipboard();
                //SaveResultToClipboard();
            }
            else if (sender == ImageSaveResult)
            {
                ImageResult.GetInformation().Save();
                //SaveResultToFile();
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
                UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            else if (sender == UseColorImage)
            {
                //ChangeColorSpace();
                UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            else if (sender == UsedChannels)
            {
                UsedChannels.ContextMenu.IsOpen = true;
            }
        }

    }
}
