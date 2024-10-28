using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using ImageMagick;
using ImageMagick.Configuration;
using Xceed.Wpf.Toolkit;

namespace ImageCompare
{
    public enum ImageType { All = 0, Source = 1, Target = 2, Result = 3, None = 255 }
    public enum ZoomFitMode { None = 0, All = 1, Width = 2, Height = 3 }
    public enum ImageOpMode { None = 0, Compare = 1, Compose = 2 }
    public enum ImageScaleMode { Independence = 0, Relative = 1 }

    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        #region Application Infomations
        private static readonly string AppExec = Application.ResourceAssembly.CodeBase.ToString().Replace("file:///", "").Replace("/", "\\");
        private static readonly string AppPath = Path.GetDirectoryName(AppExec);
        private static readonly string AppName = Path.GetFileNameWithoutExtension(AppPath);
        private static string CachePath =  "cache";

        private bool AutoSaveConfig
        {
            get { return (AutoSaveOptions.Dispatcher.Invoke(() => AutoSaveOptions.IsChecked ?? true)); }
            set { AutoSaveOptions.Dispatcher.Invoke(() => AutoSaveOptions.IsChecked = value); }
        }

        private bool AutoComparing
        {
            get { return (AutoCompare.Dispatcher.Invoke(() => AutoCompare.IsChecked ?? true)); }
            set { AutoCompare.Dispatcher.Invoke(() => AutoCompare.IsChecked = value); }
        }

        private bool DarkTheme
        {
            get { return (DarkBackground.Dispatcher.Invoke(() => DarkBackground.IsChecked ?? true)); }
            set { DarkBackground.Dispatcher.Invoke(() => DarkBackground.IsChecked = value); }
        }

        private readonly FontFamily CustomMonoFontFamily = new FontFamily();
        private readonly FontFamily CustomIconFontFamily = new FontFamily();

        private string DefaultWindowTitle = string.Empty;
        private string DefaultCompareToolTip = string.Empty;
        private string DefaultComposeToolTip = string.Empty;
        private string DefaultFuzzySliderToolTip = string.Empty;


        private Percentage DefaultColorFuzzy
        {
            get
            {
                if (Dispatcher.Invoke(() => IsLoaded))
                {
                    var value = ImageCompareFuzzy.Dispatcher.Invoke(() => Math.Min(Math.Max(ImageCompareFuzzy.Minimum, ImageCompareFuzzy.Value), ImageCompareFuzzy.Maximum));
                    return (new Percentage(value));
                }
                else return (new Percentage());
            }
        }
        private Gravity DefaultMatchAlign = Gravity.Center;
        private ImageType LastMatchedImage = ImageType.None;

        private Rect LastPositionSize = new Rect();
        private System.Windows.WindowState LastWinState = System.Windows.WindowState.Normal;
        //private Screen screens = Screen..AllScreens;

        private CultureInfo DefaultCultureInfo = CultureInfo.CurrentCulture;

        private bool IsBusy
        {
            get => BusyNow.Dispatcher.Invoke(() => { return (BusyNow.IsBusy); });
            set => BusyNow.Dispatcher.Invoke(() =>
            {
                BusyNow.IsBusy = value;
                ProcessStatus.IsIndeterminate = value;
                ProcessStatus.Value = value ? 0 : 100;
                DoEvents();
            });
        }

        private string LoadingWaitingStr = string.Empty;
        private string SavingWaitingStr = string.Empty;
        private string ProcessingWaitingStr = string.Empty;

        private bool IsLoadingSource
        {
            get => BusyNow.Dispatcher.Invoke(() => { return (IndicatorSource.IsBusy); });
            set => BusyNow.Dispatcher.Invoke(() => { IndicatorSource.BusyContent = LoadingWaitingStr; IndicatorSource.IsBusy = value; DoEvents(); });
        }

        private bool IsLoadingTarget
        {
            get => BusyNow.Dispatcher.Invoke(() => { return (IndicatorTarget.IsBusy); });
            set => BusyNow.Dispatcher.Invoke(() => { IndicatorTarget.BusyContent = LoadingWaitingStr; IndicatorTarget.IsBusy = value; DoEvents(); });
        }

        private bool IsSavingSource
        {
            get => BusyNow.Dispatcher.Invoke(() => { return (IndicatorSource.IsBusy); });
            set => BusyNow.Dispatcher.Invoke(() => { IndicatorSource.BusyContent = SavingWaitingStr; IndicatorSource.IsBusy = value; DoEvents(); });
        }

        private bool IsSavingTarget
        {
            get => BusyNow.Dispatcher.Invoke(() => { return (IndicatorTarget.IsBusy); });
            set => BusyNow.Dispatcher.Invoke(() => { IndicatorTarget.BusyContent = SavingWaitingStr; IndicatorTarget.IsBusy = value; DoEvents(); });
        }

        private bool IsProcessingSource
        {
            get => BusyNow.Dispatcher.Invoke(() => { return (IndicatorSource.IsBusy); });
            set => BusyNow.Dispatcher.Invoke(() => { IndicatorSource.BusyContent = ProcessingWaitingStr; IndicatorSource.IsBusy = value; DoEvents(); });
        }

        private bool IsProcessingTarget
        {
            get => BusyNow.Dispatcher.Invoke(() => { return (IndicatorTarget.IsBusy); });
            set => BusyNow.Dispatcher.Invoke(() => { IndicatorTarget.BusyContent = ProcessingWaitingStr; IndicatorTarget.IsBusy = value; DoEvents(); });
        }

        private bool IsExchanged
        {
            get => ImageExchange.Dispatcher.Invoke(() => { return (ImageExchange.IsChecked ?? false); });
            set => ImageExchange.Dispatcher.Invoke(() => { ImageExchange.IsChecked = value; });
        }
        #endregion

        #region Magick.Net Settings
        private int MaxCompareSize = 1024;
        private MagickGeometry CompareResizeGeometry = null;

        private double LastZoomRatio = 1;
        private bool LastOpIsComposite = false;
        //private ImageType LastImageType = ImageType.Result;
        //private double ImageDistance = 0;

        //private double LastCompositeBlendRatio = 50;

        private Channels CompareImageChannels = Channels.All;
        private bool CompareImageAutoMatchSize { get { return (AutoMatchSize.Dispatcher.Invoke(() => AutoMatchSize.IsChecked ?? false)); } }
        private bool CompareImageForceScale { get { return (UseSmallerImage.Dispatcher.Invoke(() => UseSmallerImage.IsChecked ?? false)); } }
        private bool CompareImageForceColor { get { return (UseColorImage.Dispatcher.Invoke(() => UseColorImage.IsChecked ?? false)); } }
        private ErrorMetric ErrorMetricMode = ErrorMetric.Fuzz;
        private CompositeOperator CompositeMode = CompositeOperator.Difference;
        private PixelIntensityMethod GrayscaleMode = PixelIntensityMethod.Undefined;

        private bool UseSmallImage { get { return (UseSmallerImage.Dispatcher.Invoke(() => UseSmallerImage.IsChecked ?? false)); } }

        private ImageScaleMode ScaleMode = ImageScaleMode.Independence;

#if Q16HDRI
        private IMagickColor<float> HighlightColor = MagickColors.Red;
        private IMagickColor<float> LowlightColor = null;
        private IMagickColor<float> MasklightColor = null;
#else
        private IMagickColor<byte> HighlightColor = MagickColors.Red;
        private IMagickColor<byte> LowlightColor = null;
        private IMagickColor<byte> MasklightColor = null;
#endif

        private string LastHaldFolder { get; set; } = string.Empty;
        private string LastHaldFile { get; set; } = string.Empty;

        private bool WeakBlur { get { return (UseWeakBlur.Dispatcher.Invoke(() => UseWeakBlur.IsChecked ?? false)); } }
        private bool WeakSharp { get { return (UseWeakSharp.Dispatcher.Invoke(() => UseWeakSharp.IsChecked ?? false)); } }
        private bool WeakEffects { get { return (UseWeakEffects.Dispatcher.Invoke(() => UseWeakEffects.IsChecked ?? false)); } }
        #endregion

        private ContextMenu cm_compare_mode = null;
        private ContextMenu cm_compose_mode = null;
        private ContextMenu cm_grayscale_mode = null;

        private readonly List<FrameworkElement> cm_image_source = new List<FrameworkElement>();
        private readonly List<FrameworkElement> cm_image_target = new List<FrameworkElement>();

        #region DoEvent Helper
        private static object ExitFrame(object state)
        {
            ((DispatcherFrame)state).Continue = false;
            return null;
        }

        private static readonly SemaphoreSlim CanDoEvents = new SemaphoreSlim(1, 1);
        public static async void DoEvents()
        {
            if (await CanDoEvents.WaitAsync(0))
            {
                try
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        await Dispatcher.Yield(DispatcherPriority.Normal);
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

        #region Render Background Worker Routines
        private Action LastAction { get; set; } = null;
        private BackgroundWorker RenderWorker = null;

        public T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            //get parent item
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            //we've reached the end of the tree
            if (parentObject == null) return null;

            //check if the parent matches the type we're looking for
            if (parentObject is T parent)
                return parent;
            else
                return FindParent<T>(parentObject);
        }

        public UIElement GetContextMenuHost(UIElement item)
        {
            UIElement result = null;
            if (item is MenuItem)
            {
                var parent = FindParent<ContextMenu>(item);
                if (parent is ContextMenu)
                {
                    result = parent.PlacementTarget;
                }
            }
            return (result);
        }

        private ImageType GetImageType(object sender)
        {
            var result = ImageType.None;
            try
            {
                var host = sender is MenuItem ? GetContextMenuHost(sender as MenuItem) : sender as UIElement;
                if (host is UIElement)
                {
                    var ui_source = new UIElement[] { ImageSourceScroll, ImageSourceBox, ImageSource };
                    var ui_target = new UIElement[] { ImageTargetScroll, ImageTargetBox, ImageTarget };
                    var ui_result = new UIElement[] { ImageResultScroll, ImageResultBox, ImageResult };
                    if (ui_source.Contains(host)) result = ImageType.Source;
                    else if (ui_target.Contains(host)) result = ImageType.Target;
                    else if (ui_result.Contains(host)) result = ImageType.Result;
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

        private void InitRenderWorker()
        {
            if (RenderWorker == null)
            {
                RenderWorker = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
                RenderWorker.ProgressChanged += (o, e) => { IsBusy = true; };
                RenderWorker.RunWorkerCompleted += (o, e) => { IsBusy = false; };
                RenderWorker.DoWork += async (o, e) =>
                {
                    if (e.Argument is Action)
                    {
                        var action = e.Argument as Action;
                        await Task.Run(() =>
                        {
                            IsBusy = true;
                            action.Invoke();
                            IsBusy = false;
                        });
                        LastAction = action;
                    }
                };
            }
        }

        private void RenderRun(Action action)
        {
            InitRenderWorker();
            if (RenderWorker is BackgroundWorker && !RenderWorker.IsBusy && !IsBusy && action is Action)
            {
                RenderWorker.RunWorkerAsync(action);
            }
        }
        #endregion

        #region Image Display Helper
        private static string DefaultWaitingString = "Waiting";
        private string WaitingString = DefaultWaitingString;

        private Orientation CurrentImageLayout
        {
            get { return (ViewerPanel.Orientation); }
            set
            {
                Dispatcher.Invoke(() =>
                {
                    ViewerPanel.Orientation = value;
                    ImageLayout.IsChecked = value == Orientation.Vertical;
                });
            }
        }
        private ZoomFitMode CurrentZoomFitMode
        {
            get
            {
                return (Dispatcher.Invoke(() =>
                {
                    var value = ZoomFitMode.All;
                    if (ZoomFitNone.IsChecked ?? false) value = ZoomFitMode.None;
                    else if (ZoomFitAll.IsChecked ?? false) value = ZoomFitMode.All;
                    else if (ZoomFitWidth.IsChecked ?? false) value = ZoomFitMode.Width;
                    else if (ZoomFitHeight.IsChecked ?? false) value = ZoomFitMode.Height;
                    return (value);
                }));
            }
            set
            {
                Dispatcher.Invoke(() =>
                {
                    if (value == ZoomFitMode.None)
                    {
                        ZoomFitNone.IsChecked = true; ZoomFitAll.IsChecked = false; ZoomFitWidth.IsChecked = false; ZoomFitHeight.IsChecked = false;
                        ImageSourceBox.Stretch = Stretch.None; ImageTargetBox.Stretch = Stretch.None; ImageResultBox.Stretch = Stretch.None;
                    }
                    else if (value == ZoomFitMode.All)
                    {
                        ZoomFitNone.IsChecked = false; ZoomFitAll.IsChecked = true; ZoomFitWidth.IsChecked = false; ZoomFitHeight.IsChecked = false;
                        ImageSourceBox.Stretch = Stretch.Uniform; ImageTargetBox.Stretch = Stretch.Uniform; ImageResultBox.Stretch = Stretch.Uniform;
                    }
                    else if (value == ZoomFitMode.Width)
                    {
                        ZoomFitNone.IsChecked = false; ZoomFitAll.IsChecked = false; ZoomFitWidth.IsChecked = true; ZoomFitHeight.IsChecked = false;
                        ImageSourceBox.Stretch = Stretch.None; ImageTargetBox.Stretch = Stretch.None; ImageResultBox.Stretch = Stretch.None;
                    }
                    else if (value == ZoomFitMode.Height)
                    {
                        ZoomFitNone.IsChecked = false; ZoomFitAll.IsChecked = false; ZoomFitWidth.IsChecked = false; ZoomFitHeight.IsChecked = true;
                        ImageSourceBox.Stretch = Stretch.None; ImageTargetBox.Stretch = Stretch.None; ImageResultBox.Stretch = Stretch.None;
                    }
                    CalcDisplay(set_ratio: true);
                });
            }
        }
        private readonly SemaphoreSlim _CanUpdate_ = new SemaphoreSlim(1, 1);
        private readonly Dictionary<Color, string> ColorNames = new Dictionary<Color, string>();
        private Point mouse_start;
        private Point mouse_origin;
        private double ZoomMin = 0.1;
        private double ZoomMax = 10.0;

        public void ChangeTheme()
        {
            try
            {
                var source = new Uri(@"pack://application:,,,/ImageCompare;component/Resources/CheckboardPattern_32.png", UriKind.RelativeOrAbsolute);
                var sri = Application.GetResourceStream(source);
                if (sri is System.Windows.Resources.StreamResourceInfo && sri.ContentType.Equals("image/png") && sri.Stream is Stream && sri.Stream.CanRead && sri.Stream.Length > 0)
                {
                    //var bg = ImageCanvas.Background;
                    var opacity = 0.1;
                    var pattern = new MagickImage(sri.Stream);
                    if (DarkTheme)
                    {
                        pattern.Negate(Channels.RGB);
                        pattern.Opaque(MagickColors.Black, new MagickColor("#202020"));
                        pattern.Opaque(MagickColors.White, new MagickColor("#303030"));
                        opacity = 1.0;
                    }
                    ImageCanvas.Background = new ImageBrush(pattern.ToBitmapSource()) { TileMode = TileMode.Tile, Opacity = opacity, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 32, 32) };
                    ImageCanvas.InvalidateVisual();
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        public void ChangeResourceFonts(FontFamily font = null, string fonts = "")
        {
            var customfamilies = new Dictionary<string, FontFamily>() {
                { "MonoSpaceFamily", CustomMonoFontFamily },
                { "SegoeIconFamily", CustomIconFontFamily },
            };

            if (font == null) { font = new FontFamily(); }

            foreach (var family in font is FontFamily ? customfamilies.Where(f => f.Value.Equals(font)) : customfamilies)
            {
                var old_family = FindResource(family.Key);
                if (old_family is FontFamily && font is FontFamily && !string.IsNullOrEmpty(fonts))
                {
                    try
                    {
                        font = new FontFamily(fonts);
                        var old_fonts = (old_family as FontFamily).Source.Trim().Trim('"').Split(',').Select(f => f.Trim());
                        var new_fonts = fonts.Trim().Trim('"').Split(',').Select(f => f.Trim());
                        var source = new_fonts.Union(old_fonts);
                        var new_family = new FontFamily(string.Join(", ", source));
                        Resources.Remove(family.Key);
                        Resources.Add(family.Key, new_family);
                    }
                    catch (Exception ex) { ex.ShowMessage(); }
                }
            }
        }

        private void GetColorNames()
        {
            typeof(Colors).GetProperties();
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
            catch (Exception ex) { ex.ShowMessage(); }
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
                Dispatcher.Invoke(() =>
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
                        catch (Exception ex) { ex.ShowMessage(); continue; }
                    }
                });
            }
        }

        private void SetRecentColors(ColorPicker picker, IEnumerable<string> colors)
        {
            if (colors is IEnumerable<string> && colors.Count() > 0)
            {
                SetRecentColors(picker, colors.Select(c => (Color)ColorConverter.ConvertFromString(c)));
            }
        }

        private void SyncColorLighting()
        {
            if (IsLoaded)
            {
                if (ImageSource is Image)
                {
                    if (ImageSource.Tag == null) ImageSource.Tag = new ImageInformation() { Tagetment = ImageSource, HighlightColor = HighlightColor, LowlightColor = LowlightColor, MasklightColor = MasklightColor };
                    else if (ImageSource.Tag is ImageInformation) { var info = ImageSource.Tag as ImageInformation; info.Tagetment = ImageSource; info.HighlightColor = HighlightColor; info.LowlightColor = LowlightColor; info.MasklightColor = MasklightColor; }
                }
                if (ImageTarget is Image)
                {
                    if (ImageTarget.Tag == null) ImageTarget.Tag = new ImageInformation() { Tagetment = ImageTarget, HighlightColor = HighlightColor, LowlightColor = LowlightColor, MasklightColor = MasklightColor };
                    else if (ImageTarget.Tag is ImageInformation) { var info = ImageTarget.Tag as ImageInformation; info.Tagetment = ImageTarget; info.HighlightColor = HighlightColor; info.LowlightColor = LowlightColor; info.MasklightColor = MasklightColor; }
                }
                if (ImageResult is Image)
                {
                    if (ImageResult.Tag == null) ImageResult.Tag = new ImageInformation() { Tagetment = ImageResult, HighlightColor = HighlightColor, LowlightColor = LowlightColor, MasklightColor = MasklightColor };
                    else if (ImageResult.Tag is ImageInformation) { var info = ImageResult.Tag as ImageInformation; info.Tagetment = ImageResult; info.HighlightColor = HighlightColor; info.LowlightColor = LowlightColor; info.MasklightColor = MasklightColor; }
                }
            }
        }

        private Point GetScrollOffset(FrameworkElement sender)
        {
            double offset_x = -1, offset_y = -1;
            if (sender == ImageSourceBox || sender == ImageSourceScroll)
            {
                if (ImageSourceBox.Stretch == Stretch.None)
                {
                    offset_x = ImageSourceScroll.HorizontalOffset;
                    offset_y = ImageSourceScroll.VerticalOffset;
                }
            }
            else if (sender == ImageTargetBox || sender == ImageTargetScroll)
            {
                if (ImageTargetBox.Stretch == Stretch.None)
                {
                    offset_x = ImageTargetScroll.HorizontalOffset;
                    offset_y = ImageTargetScroll.VerticalOffset;
                }
            }
            else if (sender == ImageResultBox || sender == ImageResultScroll)
            {
                if (ImageResultBox.Stretch == Stretch.None)
                {
                    offset_x = ImageResultScroll.HorizontalOffset;
                    offset_y = ImageResultScroll.VerticalOffset;
                }
            }
            return (new Point(offset_x, offset_y));
        }

        private Point CalcScrollOffset(FrameworkElement sender, MouseEventArgs e)
        {
            double offset_x = -1, offset_y = -1;
            if (sender == ImageSourceBox || sender == ImageSourceScroll)
            {
                if (ImageSourceBox.Stretch == Stretch.None && ImageSource.GetInformation().ValidCurrent)
                {
                    Point factor = new Point(ImageSourceScroll.ExtentWidth/ImageSourceScroll.ActualWidth, ImageSourceScroll.ExtentHeight/ImageSourceScroll.ActualHeight);
                    Vector v = mouse_start - e.GetPosition(ImageSourceScroll);
                    offset_x = mouse_origin.X + v.X * factor.X;
                    offset_y = mouse_origin.Y + v.Y * factor.Y;
                }
            }
            else if (sender == ImageTargetBox || sender == ImageTargetScroll)
            {
                if (ImageTargetBox.Stretch == Stretch.None && ImageTarget.GetInformation().ValidCurrent)
                {
                    Point factor = new Point(ImageTargetScroll.ExtentWidth/ImageTargetScroll.ActualWidth, ImageTargetScroll.ExtentHeight/ImageTargetScroll.ActualHeight);
                    Vector v = mouse_start - e.GetPosition(ImageTargetScroll);
                    offset_x = mouse_origin.X + v.X * factor.X;
                    offset_y = mouse_origin.Y + v.Y * factor.Y;
                }
            }
            else if (sender == ImageResultBox || sender == ImageResultScroll)
            {
                if (ImageResultBox.Stretch == Stretch.None && ImageResult.GetInformation().ValidCurrent)
                {
                    Point factor = new Point(ImageResultScroll.ExtentWidth/ImageResultScroll.ActualWidth, ImageResultScroll.ExtentHeight/ImageResultScroll.ActualHeight);
                    Vector v = mouse_start - e.GetPosition(ImageResultScroll);
                    offset_x = mouse_origin.X + v.X * factor.X;
                    offset_y = mouse_origin.Y + v.Y * factor.Y;
                }
            }
            return (new Point(offset_x, offset_y));
        }

        private void SyncScrollOffset(Point offset)
        {
            Dispatcher.Invoke(() =>
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
            });
        }

        private void CalcDisplay(bool set_ratio = true)
        {
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    #region Re-Calc Scroll Viewer Size
                    ViewerPanel.MaxWidth = ImageCanvas.ActualWidth;
                    ViewerPanel.MaxHeight = ImageCanvas.ActualHeight - ImageToolBar.ActualHeight;
                    ViewerPanel.MinWidth = ImageCanvas.ActualWidth;
                    ViewerPanel.MinHeight = ImageCanvas.ActualHeight - ImageToolBar.ActualHeight;
                    ViewerPanel.RenderSize = new Size(ViewerPanel.MaxWidth, ViewerPanel.MaxHeight);

                    var w = ViewerPanel.ActualWidth;
                    var h = ViewerPanel.ActualHeight;
                    if (ViewerPanel.Orientation == Orientation.Horizontal)
                    {
                        w = ViewerPanel.ActualWidth / 3.0;
                    }
                    else if (ViewerPanel.Orientation == Orientation.Vertical)
                    {
                        h = ViewerPanel.ActualHeight / 3.0;
                    }
                    ImageSourceScroll.RenderSize = new Size(w, h);
                    ImageTargetScroll.RenderSize = new Size(w, h);
                    ImageResultScroll.RenderSize = new Size(w, h);

                    ImageSourceScroll.MinWidth = w;
                    ImageSourceScroll.MinHeight = h;
                    ImageTargetScroll.MinWidth = w;
                    ImageTargetScroll.MinHeight = h;
                    ImageResultScroll.MinWidth = w;
                    ImageResultScroll.MinHeight = h;

                    ImageSourceScroll.MaxWidth = w;
                    ImageSourceScroll.MaxHeight = h;
                    ImageTargetScroll.MaxWidth = w;
                    ImageTargetScroll.MaxHeight = h;
                    ImageResultScroll.MaxWidth = w;
                    ImageResultScroll.MaxHeight = h;

                    //if (w <= 0 || h <= 0)
                    //{
                    //    LoadingSource.HorizontalAlignment = HorizontalAlignment.Center;
                    //    LoadingSource.VerticalAlignment = VerticalAlignment.Center;
                    //    LoadingSource.Margin = new Thickness(0);
                    //    LoadingTarget.HorizontalAlignment = HorizontalAlignment.Center;
                    //    LoadingTarget.VerticalAlignment = VerticalAlignment.Center;
                    //    LoadingTarget.Margin = new Thickness(0);
                    //}
                    //else
                    //{
                    //    LoadingSource.HorizontalAlignment = HorizontalAlignment.Left;
                    //    LoadingSource.VerticalAlignment = VerticalAlignment.Top;
                    //    LoadingSource.Margin = new Thickness((w - LoadingSource.ActualWidth) / 2, (h - LoadingSource.ActualHeight) / 2, 0, 0);
                    //    LoadingTarget.HorizontalAlignment = HorizontalAlignment.Left;
                    //    LoadingTarget.VerticalAlignment = VerticalAlignment.Top;
                    //    LoadingTarget.Margin = new Thickness((w - LoadingTarget.ActualWidth) / 2, (h - LoadingTarget.ActualHeight) / 2, 0, 0);
                    //}

                    #endregion

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
                        if (image_s.ValidCurrent && ImageSource.Source != null)
                        {
                            ImageSourceBox.Width = ImageSource.Source.Width;
                            ImageSourceBox.Height = ImageSource.Source.Height;
                        }
                        if (image_t.ValidCurrent && ImageTarget.Source != null)
                        {
                            ImageTargetBox.Width = ImageTarget.Source.Width;
                            ImageTargetBox.Height = ImageTarget.Source.Height;
                        }
                        if (image_r.ValidCurrent && ImageResult.Source != null)
                        {
                            ImageResultBox.Width = ImageResult.Source.Width;
                            ImageResultBox.Height = ImageResult.Source.Height;
                        }
                        ZoomRatio.Value = LastZoomRatio;
                    }

                    if (ZoomFitNone.IsChecked ?? false) ZoomRatio.IsEnabled = true;
                    else ZoomRatio.IsEnabled = false;
                    ZoomRatioValue.IsEnabled = ZoomRatio.IsEnabled;

                    CalcZoomRatio();
                });
            }
            catch (Exception ex) { ex.ShowMessage(); }
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
                        ZoomRatio.Minimum = ZoomMin;
                        //if (scroll.ActualHeight < height && scroll.ActualWidth < width)
                        //{
                        //    ZoomRatioValue.Text = $"{Math.Min(scroll.ActualHeight / height, scroll.ActualWidth / width):F2}X";
                        //}
                        //else ZoomRatioValue.Text = $"{ZoomRatio.Value:F2}X";
                    }
                    else if (ZoomFitNone.IsChecked ?? false)
                    {
                        //ZoomRatio.Value = 1;
                        ZoomRatio.Minimum = ZoomMin;
                        //if (scroll.ActualHeight < height && scroll.ActualWidth < width)
                        //{
                        //    ZoomRatioValue.Text = $"{Math.Min(scroll.ActualHeight / height, scroll.ActualWidth / width):F2}X";
                        //}
                        //else ZoomRatioValue.Text = $"{ZoomRatio.Value:F2}";
                    }
                    else if (ZoomFitWidth.IsChecked ?? false)
                    {
                        if (scroll.ActualWidth > width)
                        {
                            //ZoomRatioValue.Text = $"{Math.Min(scroll.ActualHeight / height, scroll.ActualWidth / width):F2}X";
                            ZoomRatio.Minimum = ZoomMin;
                            ZoomRatio.Value = 1;
                        }
                        else
                        {
                            var targetX = width;
                            var targetY = image.Height;
                            var ratio = scroll.ActualWidth / targetX;
                            var delta = scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Hidden || targetY * ratio <= scroll.ActualHeight ? 0 : 14;
                            var value = (scroll.ActualWidth - delta) / targetX;
                            ZoomRatio.Minimum = value < ZoomMin ? value : ZoomMin;
                            ZoomRatio.Value = value;
                        }
                    }
                    else if (ZoomFitHeight.IsChecked ?? false)
                    {
                        if (scroll.ActualHeight > height)
                        {
                            //ZoomRatioValue.Text = $"{Math.Min(scroll.ActualHeight / height, scroll.ActualWidth / width):F2}X";
                            ZoomRatio.Minimum = ZoomMin;
                            ZoomRatio.Value = 1;
                        }
                        else
                        {
                            var targetX = image.Width;
                            var targetY = height;
                            var ratio = scroll.ActualHeight / targetY;
                            var delta = scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Hidden || targetX * ratio <= scroll.ActualWidth ? 0 : 14;
                            var value = (scroll.ActualHeight - delta) / targetY;
                            ZoomRatio.Minimum = value < ZoomMin ? value : ZoomMin;
                            ZoomRatio.Value = value;
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        private void MatchImageSize(ImageInformation src, ImageInformation dst, Gravity align = Gravity.Center, MagickColor mask = null)
        {
            if (src is ImageInformation && dst is ImageInformation && src.ValidCurrent && dst.ValidCurrent)
            {
                var resized = false;
                if ((src.CurrentSize.Width > dst.CurrentSize.Width) || (src.CurrentSize.Height > dst.CurrentSize.Height))
                { ResizeToImage(false, assign: false, reset: true, align: align); resized = true; }
                else if ((src.CurrentSize.Width < dst.CurrentSize.Width) || (src.CurrentSize.Height < dst.CurrentSize.Height))
                { ResizeToImage(true, assign: false, reset: true, align: align); resized = true; }

                if (resized)
                {
                    var offset = new PointD(src.CurrentSize.Width - dst.CurrentSize.Width, src.CurrentSize.Height - dst.CurrentSize.Height);
                    if (offset.X != 0 || offset.Y != 0)
                    {
                        var w_s = (int)Math.Max(src.CurrentSize.Width, src.CurrentSize.Width - offset.X);
                        var h_s = (int)Math.Max(src.CurrentSize.Height, src.CurrentSize.Height - offset.Y);
                        var w_t = (int)Math.Max(dst.CurrentSize.Width, dst.CurrentSize.Width + offset.X);
                        var h_t = (int)Math.Max(dst.CurrentSize.Height, dst.CurrentSize.Height + offset.Y);

                        src.Current.Extent(w_s, h_s, align, src.Current.HasAlpha ? MagickColors.Transparent : src.Current.BackgroundColor);
                        dst.Current.Extent(w_t, h_t, align, dst.Current.HasAlpha ? MagickColors.Transparent : dst.Current.BackgroundColor);

                        src.Current.RePage();
                        dst.Current.RePage();
                    }
                }
                else
                {
                    var offset = new PointD(src.BaseSize.Width - dst.BaseSize.Width, src.BaseSize.Height - dst.BaseSize.Height);
                    if (offset.X != 0 || offset.Y != 0)
                    {
                        var w_s = (int)Math.Max(src.BaseSize.Width, src.BaseSize.Width - offset.X);
                        var h_s = (int)Math.Max(src.BaseSize.Height, src.BaseSize.Height - offset.Y);
                        var w_t = (int)Math.Max(dst.BaseSize.Width, dst.BaseSize.Width + offset.X);
                        var h_t = (int)Math.Max(dst.BaseSize.Height, dst.BaseSize.Height + offset.Y);

                        src.Current.Extent(w_s, h_s, align, src.Current.HasAlpha ? MagickColors.Transparent : src.Current.BackgroundColor);
                        dst.Current.Extent(w_t, h_t, align, dst.Current.HasAlpha ? MagickColors.Transparent : dst.Current.BackgroundColor);

                        src.Current.RePage();
                        dst.Current.RePage();
                    }
                }
            }
        }

        private async void UpdateImageViewer(bool compose = false, bool assign = false, bool reload = true, ImageType reload_type = ImageType.All, bool autocompare = false)
        {
            var loaded = await Dispatcher.InvokeAsync(() => IsLoaded);
            if (loaded && await _CanUpdate_.WaitAsync(TimeSpan.FromMilliseconds(200)))
            {
                try
                {
#if DEBUG
                    Debug.WriteLine("---> UpdateImageViewer <---");
#endif
                    IsBusy = true;

                    ImageInformation image_s = ImageSource.GetInformation();
                    ImageInformation image_t = ImageTarget.GetInformation();
                    ImageInformation image_r = ImageResult.GetInformation();
                    bool? source = null;
                    bool? target = null;
                    autocompare |= AutoComparing;
                    var exchanged = IsExchanged;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        source = ImageSource.Source == null;
                        target = ImageTarget.Source == null;
                    }, DispatcherPriority.Normal);

                    var ShowGeometry = new MagickGeometry();

                    if (assign || (source ?? false) || (target ?? false))
                    {
                        try
                        {
                            if (reload)
                            {
                                image_s.CurrentGeometry = CompareImageForceScale ? CompareResizeGeometry : null;
                                image_t.CurrentGeometry = CompareImageForceScale ? CompareResizeGeometry : null;

                                if (CompareImageForceScale)
                                {
                                    if (reload_type == ImageType.All || reload_type == ImageType.Source)
                                    {
                                        if (image_s.OriginalSize.Width > MaxCompareSize || image_s.OriginalSize.Height > MaxCompareSize)
                                            await image_s.Reload(CompareResizeGeometry, reset: true);
                                        else await image_s.Reload(reload: image_s.CurrentSize.Width != image_s.OriginalSize.Width || image_s.CurrentSize.Height != image_s.OriginalSize.Height);
                                    }
                                    if (reload_type == ImageType.All || reload_type == ImageType.Target)
                                    {
                                        if (image_t.OriginalSize.Width > MaxCompareSize || image_t.OriginalSize.Height > MaxCompareSize)
                                            await image_t.Reload(CompareResizeGeometry, reset: true);
                                        else await image_t.Reload(reload: image_t.CurrentSize.Width != image_t.OriginalSize.Width || image_t.CurrentSize.Height != image_t.OriginalSize.Height);
                                    }
                                }
                                else
                                {
                                    if (reload_type == ImageType.All || reload_type == ImageType.Source)
                                    {
                                        if (image_s.CurrentSize.Width != image_s.OriginalSize.Width || image_s.CurrentSize.Height != image_s.OriginalSize.Height)
                                            await image_s.Reload(reset: true);
                                    }
                                    if (reload_type == ImageType.All || reload_type == ImageType.Target)
                                    {
                                        if (image_t.CurrentSize.Width != image_t.OriginalSize.Width || image_t.CurrentSize.Height != image_t.OriginalSize.Height)

                                            await image_t.Reload(reset: true);
                                    }
                                }

                                if (CompareImageAutoMatchSize)
                                {
                                    MatchImageSize(image_s, image_t, DefaultMatchAlign);
                                }
                                else
                                {
                                    if (image_s.ValidCurrent && image_t.ValidCurrent)
                                    {
                                        var offset = new PointD(image_s.BaseSize.Width - image_t.BaseSize.Width, image_s.BaseSize.Height - image_t.BaseSize.Height);
                                        if (offset.X != 0 || offset.Y != 0)
                                        {
                                            if (ScaleMode == ImageScaleMode.Independence)
                                            {
                                                image_s.Current.Crop((int)image_s.BaseSize.Width, (int)image_s.BaseSize.Height, DefaultMatchAlign);
                                                image_t.Current.Crop((int)image_t.BaseSize.Width, (int)image_t.BaseSize.Height, DefaultMatchAlign);
                                            }
                                            else if (ScaleMode == ImageScaleMode.Relative)
                                            {
                                                var swc = exchanged ? image_t.CurrentSize.Width : image_s.CurrentSize.Width;
                                                var twc = exchanged ? image_s.CurrentSize.Width : image_t.CurrentSize.Width;
                                                var shc = exchanged ? image_t.CurrentSize.Height : image_s.CurrentSize.Height;
                                                var thc = exchanged ? image_s.CurrentSize.Height : image_t.CurrentSize.Height;

                                                var factor_s = image_s.OriginalSize.Width / image_s.BaseSize.Width;
                                                var factor_t = image_t.OriginalSize.Width / image_t.BaseSize.Width;

                                                var factor = Math.Max(factor_s, factor_t);
                                                if (factor_s < factor_t)
                                                    image_s.Current.Resize((int)(image_s.OriginalSize.Width / factor), (int)(image_s.OriginalSize.Height / factor));
                                                else if (factor_s > factor_t)
                                                    image_t.Current.Resize((int)(image_t.OriginalSize.Width / factor), (int)(image_t.OriginalSize.Height / factor));
                                            }
                                            image_s.Current.RePage();
                                            image_t.Current.RePage();
                                        }
                                    }
                                }
                                if (exchanged) (image_s.Current, image_t.Current) = (new MagickImage(image_t.Current), new MagickImage(image_s.Current));
                            }

                            if (image_s.ValidCurrent && image_t.ValidCurrent)
                            {
                                ShowGeometry = new MagickGeometry()
                                {
                                    Width = Math.Max(image_s.Current.Width, image_t.Current.Width),
                                    Height = Math.Max(image_s.Current.Height, image_t.Current.Height),
                                    FillArea = true,
                                };
                            }

                            foreach (var image in new ImageInformation[] { image_s, image_t })
                            {
                                image.SourceParams.Geometry = ShowGeometry;
                                image.SourceParams.Align = DefaultMatchAlign;
                                image.SourceParams.FillColor = MasklightColor;
                            }

                            await Dispatcher.InvokeAsync(() =>
                            {
                                ImageSource.Source = image_s.Source;
                                IsLoadingSource = false;
                                ImageTarget.Source = image_t.Source;
                                IsLoadingTarget = false;

                                var tooltip_s = image_s.ValidCurrent ? WaitingString : null;
                                if (ImageSource.ToolTip is string) ImageSource.ToolTip = tooltip_s;
                                else if (ImageSource.ToolTip is ToolTip) (ImageSource.ToolTip as ToolTip).Content = tooltip_s;
                                else if (ImageSource.ToolTip is null) ImageSource.ToolTip = new ToolTip() { Content = tooltip_s };

                                var tooltip_t = image_t.ValidCurrent ? WaitingString : null;
                                if (ImageTarget.ToolTip is string) ImageTarget.ToolTip = tooltip_t;
                                else if (ImageTarget.ToolTip is ToolTip) (ImageTarget.ToolTip as ToolTip).Content = tooltip_t;
                                else if (ImageTarget.ToolTip is null) ImageTarget.ToolTip = new ToolTip() { Content = tooltip_t };

                                if (autocompare) ImageResult.Source = null;
                            }, DispatcherPriority.Normal);
                        }
                        catch (Exception ex) { ex.ShowMessage(); }
                    }

                    if (autocompare)
                    {
                        if (image_r.ValidCurrent) image_r.Dispose();

                        image_r.Original = await Compare(image_s.Current, image_t.Current, compose: compose);
                        image_r.Type = ImageType.Result;
                        image_r.OpMode = LastOpIsComposite ? ImageOpMode.Compose : ImageOpMode.Compare;
                        image_r.ColorFuzzy = DefaultColorFuzzy;
                        image_r.SourceParams.Geometry = ShowGeometry;
                        image_r.SourceParams.Align = DefaultMatchAlign;
                        image_r.SourceParams.FillColor = MasklightColor;

                        await Dispatcher.InvokeAsync(() =>
                        {
                            ImageResult.Source = image_r.Source;
                            IsBusy = false;

                            var tooltip_r = image_r.ValidCurrent ? WaitingString : null;
                            if (ImageResult.ToolTip is string) ImageResult.ToolTip = tooltip_r;
                            else if (ImageResult.ToolTip is ToolTip) (ImageResult.ToolTip as ToolTip).Content = tooltip_r;
                            else if (ImageResult.ToolTip is null) ImageResult.ToolTip = new ToolTip() { Content = tooltip_r };
                        }, DispatcherPriority.Normal);
                    }
                    CalcDisplay(set_ratio: false);
                }
                catch (Exception ex) { ex.ShowMessage(); }
                finally
                {
                    GC.Collect();

                    IsLoadingSource = false;
                    IsLoadingTarget = false;
                    IsBusy = false;

                    if (_CanUpdate_ is SemaphoreSlim && _CanUpdate_.CurrentCount < 1) _CanUpdate_.Release();
                }
            }
        }
        #endregion

        #region Image Load/Save Helper
        private void LoadHaldLutFile(string hald = null)
        {
            if (string.IsNullOrEmpty(hald))
            {
                try
                {
                    var file_str = "AllSupportedImageFiles".T();
                    var dlgOpen = new Microsoft.Win32.OpenFileDialog
                    {
                        Multiselect = true,
                        CheckFileExists = true,
                        CheckPathExists = true,
                        ValidateNames = true,                     
                        //Filter = $"{file_str}|{AllSupportedFiles}|{AllSupportedFilters}",
                        Filter = $"{file_str}|{Extensions.AllSupportedFiles}"
                    };
                    if (Directory.Exists(LastHaldFolder)) dlgOpen.InitialDirectory = LastHaldFolder;
                    if (dlgOpen.ShowDialog() ?? false) hald = dlgOpen.FileName;
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }

            if (File.Exists(hald))
            {
                LastHaldFolder = Path.GetDirectoryName(hald);
                LastHaldFile = hald;
                //using (var img = new MagickImage())
                //{                   
                //}
            }
        }

        private void CopyImageFromResult(bool source = true)
        {
            try
            {
                var action = false;
                var image_s = ImageSource.GetInformation();
                var image_t = ImageTarget.GetInformation();
                var image_r = ImageResult.GetInformation();
                if (image_r.ValidCurrent)
                {
                    if (source)
                        image_s.Current = new MagickImage(image_r.Current);
                    else
                        image_t.Current = new MagickImage(image_r.Current);
                    action = true;
                }
                if (action) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false));
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        private void CopyImageToOpposite(bool source = true)
        {
            try
            {
                var action = false;
                var image_s = (source ? ImageSource : ImageTarget).GetInformation();
                var image_t = (source ? ImageTarget : ImageSource).GetInformation();
                if (image_s.ValidCurrent)
                {
                    if (!image_t.ValidOriginal && image_s.ValidOriginal)
                    {
                        image_t.Original = new MagickImage(image_s.Original);
                        if (image_s.OriginalIsFile) image_t.FileName = image_s.FileName;
                    }
                    image_t.Current = new MagickImage(image_s.Current);
                    action = true;
                }
                if (action) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false));
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        private async Task<bool> LoadImageFromPrevFile(bool source = true)
        {
            var ret = false;
            try
            {
                if (source) IsLoadingSource = true;
                else IsLoadingTarget = true;

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                ret = await image.LoadImageFromPrevFile();
                if (ret) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true, reload_type: source ? ImageType.Source : ImageType.Target));
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (ret);
        }

        private async Task<bool> LoadImageFromNextFile(bool source = true)
        {
            var ret = false;
            try
            {
                if (source) IsLoadingSource = true;
                else IsLoadingTarget = true;

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                ret = await image.LoadImageFromNextFile();
                if (ret) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true, reload_type: source ? ImageType.Source : ImageType.Target));
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (ret);
        }

        private void LoadImageFromFiles(IEnumerable<string> files, bool source = true)
        {
            LoadImageFromFiles(files.ToArray(), source);
        }

        private async void LoadImageFromFiles(string[] files, bool source = true)
        {
            try
            {
                var action = false;
                files = files.Select(f => f.Trim()).Where(f => !string.IsNullOrEmpty(f)).Where(f => Extensions.AllSupportedFormats.Keys.ToList().Select(e => $".{e.ToLower()}").ToList().Contains(Path.GetExtension(f).ToLower())).ToArray();
                var count = files.Length;
                if (count > 0)
                {
                    var load_type = ImageType.None;

                    var image_s = ImageSource.GetInformation();
                    var image_t = ImageTarget.GetInformation();

                    var file_s = string.Empty;
                    var file_t = string.Empty;
                    if (count >= 2)
                    {
                        IsLoadingSource = true;
                        IsLoadingTarget = true;

                        file_s = files.First();
                        file_t = files.Skip(1).First();

                        action |= await image_s.LoadImageFromFile(file_s);
                        action |= await image_t.LoadImageFromFile(file_t);
                        load_type = ImageType.All;
                    }
                    else
                    {
                        if (source) IsLoadingSource = true;
                        else IsLoadingTarget = true;

                        var image  = source ? image_s : image_t;
                        file_s = files.First();
                        action |= await image.LoadImageFromFile(file_s);
                        load_type = source ? ImageType.Source : ImageType.Target;
                    }
                    if (action) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true, reload_type: load_type));
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        private void SaveImageAs(bool source)
        {
            var ret = false;
            var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
            if (source)
            {
                IsSavingSource = true;
                ret = ImageSource.GetInformation().Save(overwrite: ctrl);
                IsSavingSource = false;
            }
            else
            {
                IsSavingTarget = true;
                ret = ImageTarget.GetInformation().Save(overwrite: ctrl);
                IsSavingTarget = true;
            }
        }
        #endregion

        #region Config Load/Save Helper
        private void LoadConfig()
        {
            Configuration appCfg =  ConfigurationManager.OpenExeConfiguration(AppExec);
            AppSettingsSection appSection = appCfg.AppSettings;
            try
            {

                if (appSection.Settings.AllKeys.Contains("AutoSaveOptions"))
                {
                    var value = AutoSaveOptions.IsChecked ?? true;
                    if (bool.TryParse(appSection.Settings["AutoSaveOptions"].Value, out value)) AutoSaveConfig = value;
                }

                if (appSection.Settings.AllKeys.Contains("DarkTheme"))
                {
                    var value = DarkBackground.IsChecked ?? true;
                    if (bool.TryParse(appSection.Settings["DarkTheme"].Value, out value)) DarkTheme = value;
                }

                if (appSection.Settings.AllKeys.Contains("CustomMonoFontFamily"))
                {
                    var value = appSection.Settings["CustomMonoFontFamily"].Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        try
                        {
                            ChangeResourceFonts(CustomMonoFontFamily, value);
                        }
                        catch { }
                    }
                }
                if (appSection.Settings.AllKeys.Contains("CustomIconFontFamily"))
                {
                    var value = appSection.Settings["CustomIconFontFamily"].Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        try
                        {
                            ChangeResourceFonts(CustomIconFontFamily, value);
                        }
                        catch { }
                    }
                }

                if (appSection.Settings.AllKeys.Contains("WindowPosition"))
                {
                    var value = appSection.Settings["WindowPosition"].Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        try
                        {
                            LastPositionSize = Rect.Parse(value);
                            RestoreWindowLocationSize();
                        }
                        catch { }
                    }
                }

                if (appSection.Settings.AllKeys.Contains("WindowState"))
                {
                    var value = appSection.Settings["WindowState"].Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        try
                        {
                            Enum.TryParse(value, out LastWinState);
                            //RestoreWindowState();
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
                    if (Enum.TryParse(appSection.Settings["ErrorMetricMode"].Value, out value)) ErrorMetricMode = value;
                }
                if (appSection.Settings.AllKeys.Contains("CompositeMode"))
                {
                    var value = CompositeMode;
                    if (Enum.TryParse(appSection.Settings["CompositeMode"].Value, out value)) CompositeMode = value;
                }
                if (appSection.Settings.AllKeys.Contains("GrayscaleMode"))
                {
                    var value = GrayscaleMode;
                    if (Enum.TryParse(appSection.Settings["GrayscaleMode"].Value, out value)) GrayscaleMode = value;
                }
                if (appSection.Settings.AllKeys.Contains("ZoomFitMode"))
                {
                    var value = CurrentZoomFitMode;
                    if (Enum.TryParse(appSection.Settings["ZoomFitMode"].Value, out value)) CurrentZoomFitMode = value;
                }
                if (appSection.Settings.AllKeys.Contains("ImageLayout"))
                {
                    var value = CurrentImageLayout;
                    if (Enum.TryParse(appSection.Settings["ImageLayout"].Value, out value)) CurrentImageLayout = value;
                }

                if (appSection.Settings.AllKeys.Contains("AutoMatchSize"))
                {
                    var value = AutoMatchSize.IsChecked ?? true;
                    if (bool.TryParse(appSection.Settings["AutoMatchSize"].Value, out value)) AutoMatchSize.IsChecked = value;
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

                if (appSection.Settings.AllKeys.Contains("LastHaldFolder"))
                {
                    var value = appSection.Settings["LastHaldFolder"].Value;
                    if (!string.IsNullOrEmpty(value)) LastHaldFolder = value;
                }
                if (appSection.Settings.AllKeys.Contains("LastHaldFile"))
                {
                    var value = appSection.Settings["LastHaldFile"].Value;
                    if (!string.IsNullOrEmpty(value)) LastHaldFile = value;
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
            catch (Exception ex) { ex.ShowMessage(); }
        }

        private void SaveConfig(bool force = false)
        {
            try
            {
                Configuration appCfg =  ConfigurationManager.OpenExeConfiguration(AppExec);
                AppSettingsSection appSection = appCfg.AppSettings;

                if (appSection.Settings.AllKeys.Contains("AutoSaveOptions"))
                    appSection.Settings["AutoSaveOptions"].Value = AutoSaveConfig.ToString();
                else
                    appSection.Settings.Add("AutoSaveOptions", AutoSaveConfig.ToString());

                if (AutoSaveConfig)
                {
                    var rect = new Rect(
                        LastPositionSize.Left, LastPositionSize.Top,
                        Math.Min(MaxWidth, Math.Max(MinWidth, LastPositionSize.Width)),
                        Math.Min(MaxHeight, Math.Max(MinHeight, LastPositionSize.Height))
                    );
                    if (appSection.Settings.AllKeys.Contains("WindowPosition"))
                        appSection.Settings["WindowPosition"].Value = rect.ToString();
                    else
                        appSection.Settings.Add("WindowPosition", rect.ToString());

                    if (appSection.Settings.AllKeys.Contains("WindowState"))
                        appSection.Settings["WindowState"].Value = LastWinState.ToString();
                    else
                        appSection.Settings.Add("WindowState", LastWinState.ToString());

                    if (appSection.Settings.AllKeys.Contains("CachePath"))
                        appSection.Settings["CachePath"].Value = CachePath;
                    else
                        appSection.Settings.Add("CachePath", CachePath);

                    if (appSection.Settings.AllKeys.Contains("CachePath"))
                        appSection.Settings["CachePath"].Value = CachePath;
                    else
                        appSection.Settings.Add("CachePath", CachePath);

                    if (appSection.Settings.AllKeys.Contains("DarkTheme"))
                        appSection.Settings["DarkTheme"].Value = DarkTheme.ToString();
                    else
                        appSection.Settings.Add("DarkTheme", DarkTheme.ToString());

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

                    if (appSection.Settings.AllKeys.Contains("GrayscaleMode"))
                        appSection.Settings["GrayscaleMode"].Value = GrayscaleMode.ToString();
                    else
                        appSection.Settings.Add("GrayscaleMode", GrayscaleMode.ToString());

                    if (appSection.Settings.AllKeys.Contains("ZoomFitMode"))
                        appSection.Settings["ZoomFitMode"].Value = CurrentZoomFitMode.ToString();
                    else
                        appSection.Settings.Add("ZoomFitMode", CurrentZoomFitMode.ToString());

                    if (appSection.Settings.AllKeys.Contains("ImageLayout"))
                        appSection.Settings["ImageLayout"].Value = CurrentImageLayout.ToString();
                    else
                        appSection.Settings.Add("ImageLayout", CurrentImageLayout.ToString());

                    if (appSection.Settings.AllKeys.Contains("ImageLayout"))
                    {
                        var value = Orientation.Horizontal;
                        if (Enum.TryParse(appSection.Settings["ImageLayout"].Value, out value)) ImageLayout.IsChecked = value == Orientation.Vertical;
                    }

                    if (appSection.Settings.AllKeys.Contains("AutoMatchSize"))
                        appSection.Settings["AutoMatchSize"].Value = AutoMatchSize.IsChecked.ToString();
                    else
                        appSection.Settings.Add("AutoMatchSize", AutoMatchSize.IsChecked.Value.ToString());

                    if (appSection.Settings.AllKeys.Contains("UseSmallerImage"))
                        appSection.Settings["UseSmallerImage"].Value = UseSmallerImage.IsChecked.ToString();
                    else
                        appSection.Settings.Add("UseSmallerImage", UseSmallerImage.IsChecked.Value.ToString());

                    if (appSection.Settings.AllKeys.Contains("UseColorImage"))
                        appSection.Settings["UseColorImage"].Value = UseColorImage.IsChecked.Value.ToString();
                    else
                        appSection.Settings.Add("UseColorImage", UseColorImage.IsChecked.Value.ToString());

                    if (appSection.Settings.AllKeys.Contains("LastHaldFolder"))
                        appSection.Settings["LastHaldFolder"].Value = LastHaldFolder;
                    else
                        appSection.Settings.Add("LastHaldFolder", LastHaldFolder);
                    if (appSection.Settings.AllKeys.Contains("LastHaldFile"))
                        appSection.Settings["LastHaldFile"].Value = LastHaldFile;
                    else
                        appSection.Settings.Add("LastHaldFile", LastHaldFile);

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
                }
                if (AutoSaveConfig || force) appCfg.Save();
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }
        #endregion

        #region UI Helper
        private void RestoreWindowLocationSize()
        {
            Top = LastPositionSize.Top;
            Left = LastPositionSize.Left;
            Width = Math.Min(MaxWidth, Math.Max(MinWidth, LastPositionSize.Width));
            Height = Math.Min(MaxHeight, Math.Max(MinHeight, LastPositionSize.Height));
        }

        private void RestoreWindowState()
        {
            if (IsLoaded && LastWinState == System.Windows.WindowState.Maximized) WindowState = LastWinState;
        }

        private void LocaleUI(CultureInfo culture = null)
        {
            var lang = (culture ?? CultureInfo.CurrentCulture).IetfLanguageTag;
            Language = System.Windows.Markup.XmlLanguage.GetLanguage(lang);

            Title = $"{Uid}.Title".T(culture) ?? Title;
            ImageToolBar.Locale();
            ImageSourceGrid.Locale();
            ImageTargetGrid.Locale();
            ImageResultGrid.Locale();

            DefaultWindowTitle = Title;
            DefaultCompareToolTip = ImageCompare.ToolTip as string;
            DefaultComposeToolTip = ImageCompose.ToolTip as string;

            LoadingWaitingStr = "LoadingIndicatorTitle".T(culture);
            SavingWaitingStr = "SavingIndicatorTitle".T(culture);
            ProcessingWaitingStr = "ProcessingIndicatorTitle".T(culture);

            ImageCompareFuzzy.ToolTip = $"{"Tolerances".T(culture)}: {ImageCompareFuzzy.Value:F1}%";
            ImageCompositeBlend.ToolTip = $"{"Blend".T(culture)}: {ImageCompositeBlend.Value:F0}%";
            ZoomRatio.ToolTip = $"{"Zoom Ratio".T(culture)}: {ZoomRatio.Value:F2}X";

            WaitingString = DefaultWaitingString.T(culture);
            ImageSource.ToolTip = new ToolTip() { Content = WaitingString };
            ImageTarget.ToolTip = new ToolTip() { Content = WaitingString };
            ImageResult.ToolTip = new ToolTip() { Content = WaitingString };

            #region Create Image Flip/Rotate/Effects Menu
            CreateImageOpMenu(ImageSourceScroll);
            CreateImageOpMenu(ImageTargetScroll);
            #endregion
        }

        private void CreateImageOpMenu(FrameworkElement target)
        {
            //bool source = target == ImageSource ? true : false;
            bool source = target == ImageSourceScroll;
            var effect_blur = new System.Windows.Media.Effects.BlurEffect() { Radius = 2, KernelType = System.Windows.Media.Effects.KernelType.Gaussian };

            var items = source ? cm_image_source : cm_image_target;
            if (items != null) items.Clear();
            else items = new List<FrameworkElement>();

            Func<object, bool> MenuHost = (obj) => Dispatcher.Invoke(() => (bool)(obj as MenuItem).Tag);

            if (items.Count <= 0)
            {
                var style = FindResource("MenuItemIcon") as Style;
                #region Create MenuItem
                var item_fh = new MenuItem()
                {
                    Header = "Flip Horizontal",
                    Uid = "FlipX",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE13C", Style = style }
                };
                var item_fv = new MenuItem()
                {
                    Header = "Flip Vertical",
                    Uid = "FlipY",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE174", Style = style }
                };
                var item_r090 = new MenuItem()
                {
                    Header = "Rotate +90",
                    Uid = "Rotate090",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE14A", Style = style }
                };
                var item_r180 = new MenuItem()
                {
                    Header = "Rotate 180",
                    Uid = "Rotate180",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE14A", Style = style, LayoutTransform = new RotateTransform(180) }
                };
                var item_r270 = new MenuItem()
                {
                    Header = "Rotate -90",
                    Uid = "Rotate270",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE14A", Style = style, LayoutTransform = new ScaleTransform(-1, 1) }
                };
                var item_reset_transform = new MenuItem()
                {
                    Header = "Reset Transforms",
                    Uid = "ResetTransforms",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE777", Style = style }
                };

                var item_gray = new MenuItem()
                {
                    Header = "Grayscale",
                    Uid = "Grayscale",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uF570", Style = style }
                };
                var item_blur = new MenuItem()
                {
                    Header = "Gaussian Blur",
                    Uid = "GaussianBlur",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uEB42", Style = style, Effect = effect_blur }
                };
                var item_sharp = new MenuItem()
                {
                    Header = "Unsharp Mask",
                    Uid = "UsmSharp",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE879", Style = style }
                };

                var item_more = new MenuItem()
                {
                    Header = "More Effects",
                    Uid = "MoreEffects",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE712", Style = style }
                };

                var item_size_crop = new MenuItem()
                {
                    Header = "Crop BoundingBox",
                    Uid = "CropBoundingBox",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\xE123", Style = style }
                };
                var item_size_cropedge = new MenuItem()
                {
                    Header = "Crop Image Edge",
                    Uid = "CropImageEdge",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\xE123", Style = style }
                };
                var item_size_extentedge = new MenuItem()
                {
                    Header = "Extend Image Edge",
                    Uid = "ExtentImageEdge",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\xE123", Style = style }
                };
                var item_size_panedge = new MenuItem()
                {
                    Header = "Add Offset To Image Edge",
                    Uid = "PanImageEdge",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\xE123", Style = style }
                };
                var item_size_to_source = new MenuItem()
                {
                    Header = "Match Source Size",
                    Uid = "MatchSourceSize",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE799", Style = style }
                };
                var item_size_to_target = new MenuItem()
                {
                    Header = "Match Target Size",
                    Uid = "MatchTargetSize",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE799", Style = style }
                };

                var item_slice_h = new MenuItem()
                {
                    Header = "Slicing Horizontal",
                    Uid = "SlicingX",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE745", Style = style }
                };
                var item_slice_v = new MenuItem()
                {
                    Header = "Slicing Vertical",
                    Uid = "SlicingY",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE746", Style = style }
                };
                var item_merge_h = new MenuItem()
                {
                    Header = "Merge Horizontal",
                    Uid = "MergeX",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uF614", Style = style }
                };
                var item_merge_v = new MenuItem()
                {
                    Header = "Merge Vertical",
                    Uid = "MergeY",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uF615", Style = style }
                };

                var item_copyfrom_result = new MenuItem()
                {
                    Header = "Copy Image From Result",
                    Uid = "CopyFromResult",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE16F", Style = style }
                };
                var item_copyto_source = new MenuItem()
                {
                    Header = "Copy Image To Source",
                    Uid = "CopyToSource",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE16F", Style = style }
                };
                var item_copyto_target = new MenuItem()
                {
                    Header = "Copy Image To Target",
                    Uid = "CopyToTarget",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE16F", Style = style }
                };

                var item_load_prev = new MenuItem()
                {
                    Header = "Load Prev Image File",
                    Uid = "LoadPrevImageFile",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE1A5", Style = style }
                };
                var item_load_next = new MenuItem()
                {
                    Header = "Load Next Image File",
                    Uid = "LoadNextImageFile",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE1A5", Style = style }
                };

                var item_reset_image = new MenuItem()
                {
                    Header = "Reset Image",
                    Uid = "ResetImage",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE117", Style = style }
                };
                var item_reload = new MenuItem()
                {
                    Header = "Reload Image",
                    Uid = "ReloadImage",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE117", Style = style }
                };
                var item_colorcalc = new MenuItem()
                {
                    Header = "Calc Image Colors",
                    Uid = "CalcImageColors",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE1D0", Style = style }
                };
                var item_copyinfo = new MenuItem()
                {
                    Header = "Copy Image Info",
                    Uid = "CopyImageInfo",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE16F", Style = style }
                };
                var item_copyimage = new MenuItem()
                {
                    Header = "Copy Image",
                    Uid = "CopyImage",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE16F", Style = style }
                };
                var item_saveas = new MenuItem()
                {
                    Header = "Save As ...",
                    Uid = "SaveAs",
                    Tag = source,
                    Icon = new TextBlock() { Text = "\uE105", Style = style }
                };
                #endregion
                #region Create MenuItem Click event handles
                item_fh.Click += (obj, evt) => { RenderRun(() => { FlopImage(MenuHost(obj)); }); };
                item_fv.Click += (obj, evt) => { RenderRun(() => { FlipImage(MenuHost(obj)); }); };
                item_r090.Click += (obj, evt) => { RenderRun(() => { RotateImage(MenuHost(obj), 90); }); };
                item_r180.Click += (obj, evt) => { RenderRun(() => { RotateImage(MenuHost(obj), 180); }); };
                item_r270.Click += (obj, evt) => { RenderRun(() => { RotateImage(MenuHost(obj), 270); }); };
                item_reset_transform.Click += (obj, evt) => { RenderRun(() => { ResetImageTransform(MenuHost(obj)); }); };

                item_gray.Click += (obj, evt) => { RenderRun(() => { GrayscaleImage(MenuHost(obj)); }); };
                item_blur.Click += (obj, evt) => { RenderRun(() => { BlurImage(MenuHost(obj)); }); };
                item_sharp.Click += (obj, evt) => { RenderRun(() => { SharpImage(MenuHost(obj)); }); };

                item_size_crop.Click += (obj, evt) => { RenderRun(() => { CropImage(MenuHost(obj)); }); };
                item_size_cropedge.Click += (obj, evt) => { RenderRun(() => { CropImageEdge(MenuHost(obj), 1, 1, DefaultMatchAlign); }); };
                item_size_extentedge.Click += (obj, evt) => { RenderRun(() => { ExtentImageEdge(MenuHost(obj), 1, 1, DefaultMatchAlign); }); };
                item_size_panedge.Click += (obj, evt) => { RenderRun(() => { PanImageEdge(MenuHost(obj), 1, 1, DefaultMatchAlign); }); };
                item_size_to_source.Click += (obj, evt) => { RenderRun(() => { ResizeToImage(false, reset: false, align: DefaultMatchAlign); }); };
                item_size_to_target.Click += (obj, evt) => { RenderRun(() => { ResizeToImage(true, reset: false, align: DefaultMatchAlign); }); };

                item_slice_h.Click += (obj, evt) =>
                {
                    var sendto = Keyboard.Modifiers == ModifierKeys.None;
                    var first = Keyboard.Modifiers == ModifierKeys.Shift || (Keyboard.Modifiers != ModifierKeys.Control);
                    RenderRun(() => { SlicingImage(MenuHost(obj), vertical: false, sendto: sendto, first: first); });
                };
                item_slice_v.Click += (obj, evt) =>
                {
                    var sendto = Keyboard.Modifiers == ModifierKeys.None;
                    var first = Keyboard.Modifiers == ModifierKeys.Shift || (Keyboard.Modifiers != ModifierKeys.Control);
                    RenderRun(() => { SlicingImage(MenuHost(obj), vertical: true, sendto: sendto, first: first); });
                };
                item_merge_h.Click += (obj, evt) =>
                {
                    var sendto = Keyboard.Modifiers == ModifierKeys.Shift;
                    var first = Keyboard.Modifiers == ModifierKeys.Shift || (Keyboard.Modifiers != ModifierKeys.Control);
                    RenderRun(() => { MergeImage(MenuHost(obj), vertical: false, sendto: sendto, first: first); });
                };
                item_merge_v.Click += (obj, evt) =>
                {
                    var sendto = Keyboard.Modifiers == ModifierKeys.Shift;
                    var first = Keyboard.Modifiers == ModifierKeys.Shift || (Keyboard.Modifiers != ModifierKeys.Control);
                    RenderRun(() => { MergeImage(MenuHost(obj), vertical: true, sendto: sendto, first: first); });
                };

                item_copyfrom_result.Click += (obj, evt) => { RenderRun(() => { CopyImageFromResult(source); }); };
                item_copyto_source.Click += (obj, evt) => { RenderRun(() => { CopyImageToOpposite(source); }); };
                item_copyto_target.Click += (obj, evt) => { RenderRun(() => { CopyImageToOpposite(source); }); };

                item_load_prev.Click += (obj, evt) => { RenderRun(() => { LoadImageFromPrevFile(MenuHost(obj)); }); };
                item_load_next.Click += (obj, evt) => { RenderRun(() => { LoadImageFromNextFile(MenuHost(obj)); }); };

                item_reset_image.Click += (obj, evt) => { RenderRun(() => { ResetImage(MenuHost(obj)); }); };
                item_reload.Click += (obj, evt) => { RenderRun(() => { ReloadImage(MenuHost(obj)); }); };

                item_colorcalc.Click += (obj, evt) => { RenderRun(() => { CalcImageColors(MenuHost(obj)); }); };
                item_copyinfo.Click += (obj, evt) => { RenderRun(() => { CopyImageInfo(MenuHost(obj)); }); };
                item_copyimage.Click += (obj, evt) => { RenderRun(() => { CopyImage(MenuHost(obj)); }); };
                item_saveas.Click += (obj, evt) => { SaveImageAs(MenuHost(obj)); };
                #endregion
                #region Add MenuItems to ContextMenu
                items.Add(item_fh);
                items.Add(item_fv);
                items.Add(new Separator());
                items.Add(item_r270);
                items.Add(item_r090);
                items.Add(item_r180);
                items.Add(new Separator());
                items.Add(item_reset_transform);
                items.Add(new Separator());
                items.Add(item_gray);
                items.Add(item_blur);
                items.Add(item_sharp);
                items.Add(item_more);
                items.Add(new Separator());
                items.Add(item_size_crop);
                items.Add(item_size_cropedge);
                items.Add(item_size_extentedge);
                items.Add(item_size_panedge);
                items.Add(item_size_to_source);
                items.Add(item_size_to_target);
                items.Add(new Separator());
                items.Add(item_slice_h);
                items.Add(item_slice_v);
                items.Add(item_merge_h);
                items.Add(item_merge_v);
                items.Add(new Separator());
                items.Add(item_copyfrom_result);
                items.Add(item_copyto_source);
                items.Add(item_copyto_target);
                items.Add(item_load_prev);
                items.Add(item_load_next);
                items.Add(new Separator());
                items.Add(item_reset_image);
                items.Add(item_reload);
                items.Add(new Separator());
                items.Add(item_colorcalc);
                items.Add(item_copyinfo);
                items.Add(item_copyimage);
                items.Add(item_saveas);
                #endregion
                #region MoreEffects MenuItem
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
                var item_more_autoreducenoise = new MenuItem()
                {
                    Header = "Auto Reduce Noise",
                    Uid = "AutoReduceNoise",
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
                var item_more_pencil = new MenuItem()
                {
                    Header = "Pencil Paint",
                    Uid = "PencilPaint",
                    Tag = source
                };
                var item_more_solarize = new MenuItem()
                {
                    Header = "Solarize",
                    Uid = "Solarize",
                    Tag = source
                };
                var item_more_edge = new MenuItem()
                {
                    Header = "Edge",
                    Uid = "Edge",
                    Tag = source
                };
                var item_more_emboss = new MenuItem()
                {
                    Header = "Emboss",
                    Uid = "Emboss",
                    Tag = source
                };
                var item_more_morph = new MenuItem()
                {
                    Header = "Morphology",
                    Uid = "Morphology",
                    Tag = source
                };
                var item_more_stereo = new MenuItem()
                {
                    Header = "Stereo (Fake 3D)",
                    Uid = "Stereo3D",
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
                var item_more_clut = new MenuItem()
                {
                    Header = "Clut",
                    Uid = "CLUT",
                    Tag = source
                };
                var item_more_haldclut = new MenuItem()
                {
                    Header = "Hald Clut",
                    Uid = "HaldClut",
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
                var item_more_segment = new MenuItem()
                {
                    Header = "Segment",
                    Uid = "Segment",
                    Tag = source
                };
                var item_more_quantize = new MenuItem()
                {
                    Header = "Quantize",
                    Uid = "Quantize",
                    Tag = source
                };

                var item_more_setalphatocolor = new MenuItem()
                {
                    Header = "Set Color To Alpha",
                    Uid = "SetColorToAlpha",
                    Tag = source
                };
                var item_more_setcolortoalpha = new MenuItem()
                {
                    Header = "Set Alpha To Color",
                    Uid = "SetAlphaToColor",
                    Tag = source
                };
                var item_more_createcolorimage = new MenuItem()
                {
                    Header = "Create Image By Color",
                    Uid = "CreateColorImage",
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
                item_more_oil.Click += (obj, evt) => { RenderRun(() => { OilImage(MenuHost(obj)); }); };
                item_more_charcoal.Click += (obj, evt) => { RenderRun(() => { CharcoalImage(MenuHost(obj)); }); };
                item_more_pencil.Click += (obj, evt) => { RenderRun(() => { PencilImage(MenuHost(obj)); }); };
                item_more_edge.Click += (obj, evt) => { RenderRun(() => { EdgeImage(MenuHost(obj)); }); };
                item_more_emboss.Click += (obj, evt) => { RenderRun(() => { EmbossImage(MenuHost(obj)); }); };
                item_more_morph.Click += (obj, evt) => { RenderRun(() => { MorphologyImage(MenuHost(obj)); }); };

                item_more_autoequalize.Click += (obj, evt) => { RenderRun(() => { AutoEqualizeImage(MenuHost(obj)); }); };
                item_more_autoreducenoise.Click += (obj, evt) => { RenderRun(() => { ReduceNoiseImage(MenuHost(obj)); }); };
                item_more_autoenhance.Click += (obj, evt) => { RenderRun(() => { AutoEnhanceImage(MenuHost(obj)); }); };
                item_more_autolevel.Click += (obj, evt) => { RenderRun(() => { AutoLevelImage(MenuHost(obj)); }); };
                item_more_autocontrast.Click += (obj, evt) => { RenderRun(() => { AutoContrastImage(MenuHost(obj)); }); };
                item_more_autowhitebalance.Click += (obj, evt) => { RenderRun(() => { AutoWhiteBalanceImage(MenuHost(obj)); }); };
                item_more_autogamma.Click += (obj, evt) => { RenderRun(() => { AutoGammaImage(MenuHost(obj)); }); };

                item_more_autovignette.Click += (obj, evt) => { RenderRun(() => { AutoVignetteImage(MenuHost(obj)); }); };
                item_more_invert.Click += (obj, evt) => { RenderRun(() => { InvertImage(MenuHost(obj)); }); };
                item_more_solarize.Click += (obj, evt) => { RenderRun(() => { SolarizeImage(MenuHost(obj)); }); };
                item_more_polaroid.Click += (obj, evt) => { RenderRun(() => { PolaroidImage(MenuHost(obj)); }); };
                item_more_posterize.Click += (obj, evt) => { RenderRun(() => { PosterizeImage(MenuHost(obj)); }); };
                item_more_medianfilter.Click += (obj, evt) => { RenderRun(() => { MedianFilterImage(MenuHost(obj)); }); };

                item_more_stereo.Click += (obj, evt) => { RenderRun(() => { StereoImage(MenuHost(obj)); }); };
                item_more_blueshift.Click += (obj, evt) => { RenderRun(() => { BlueShiftImage(MenuHost(obj)); }); };
                item_more_autothreshold.Click += (obj, evt) => { RenderRun(() => { AutoThresholdImage(MenuHost(obj)); }); };
                item_more_remap.Click += (obj, evt) => { RenderRun(() => { RemapImage(MenuHost(obj)); }); };
                item_more_clut.Click += (obj, evt) => { RenderRun(() => { ClutImage(MenuHost(obj)); }); };
                item_more_haldclut.Click += (obj, evt) => { var shift = Keyboard.Modifiers == ModifierKeys.Shift; RenderRun(() => { HaldClutImage(MenuHost(obj), shift); }); };

                item_more_meanshift.Click += (obj, evt) => { RenderRun(() => { MeanShiftImage(MenuHost(obj)); }); };
                item_more_kmeans.Click += (obj, evt) => { RenderRun(() => { KmeansImage(MenuHost(obj)); }); };
                item_more_segment.Click += (obj, evt) => { RenderRun(() => { SegmentImage(MenuHost(obj)); }); };
                item_more_quantize.Click += (obj, evt) => { RenderRun(() => { QuantizeImage(MenuHost(obj)); }); };

                item_more_fillflood.Click += (obj, evt) => { RenderRun(() => { FillOutBoundBoxImage(MenuHost(obj)); }); };
                item_more_setalphatocolor.Click += (obj, evt) => { RenderRun(() => { SetAlphaToColorImage(MenuHost(obj)); }); };
                item_more_setcolortoalpha.Click += (obj, evt) => { RenderRun(() => { SetColorToAlphaImage(MenuHost(obj)); }); };
                item_more_createcolorimage.Click += (obj, evt) => { var shift = Keyboard.Modifiers == ModifierKeys.Shift; RenderRun(() => { CreateColorImage(MenuHost(obj), shift); }); };
                #endregion
                #region Add MoreEffects MenuItems to MoreEffects
                item_more.Items.Add(item_more_autoenhance);
                item_more.Items.Add(item_more_autoreducenoise);
                item_more.Items.Add(item_more_autoequalize);
                item_more.Items.Add(new Separator());
                item_more.Items.Add(item_more_autolevel);
                item_more.Items.Add(item_more_autocontrast);
                item_more.Items.Add(item_more_autowhitebalance);
                item_more.Items.Add(item_more_autogamma);
                item_more.Items.Add(new Separator());
                item_more.Items.Add(item_more_oil);
                item_more.Items.Add(item_more_edge);
                item_more.Items.Add(item_more_emboss);
                item_more.Items.Add(item_more_solarize);
                item_more.Items.Add(item_more_morph);
                item_more.Items.Add(item_more_charcoal);
                item_more.Items.Add(item_more_pencil);
                item_more.Items.Add(item_more_invert);
                item_more.Items.Add(item_more_stereo);
                item_more.Items.Add(item_more_posterize);
                item_more.Items.Add(item_more_polaroid);
                item_more.Items.Add(new Separator());
                item_more.Items.Add(item_more_autovignette);
                item_more.Items.Add(item_more_blueshift);
                item_more.Items.Add(item_more_autothreshold);
                item_more.Items.Add(item_more_remap);
                item_more.Items.Add(item_more_clut);
                item_more.Items.Add(item_more_haldclut);
                item_more.Items.Add(new Separator());
                item_more.Items.Add(item_more_segment);
                item_more.Items.Add(item_more_quantize);
                item_more.Items.Add(item_more_medianfilter);
                item_more.Items.Add(item_more_meanshift);
                item_more.Items.Add(item_more_kmeans);
                item_more.Items.Add(new Separator());
                item_more.Items.Add(item_more_fillflood);
                item_more.Items.Add(item_more_createcolorimage);
                item_more.Items.Add(item_more_setalphatocolor);
                item_more.Items.Add(item_more_setcolortoalpha);
                #endregion

                target.ContextMenuOpening += (obj, evt) =>
                {
                    var is_source = evt.Source == ImageSourceScroll || evt.Source == ImageSourceBox || evt.Source == ImageSource;
                    var is_target = evt.Source == ImageTargetScroll || evt.Source == ImageTargetBox || evt.Source == ImageTarget;
                    var is_result = evt.Source == ImageResultScroll || evt.Source == ImageResultBox || evt.Source == ImageResult;
                    var image = is_source ? ImageSource : (is_target ? ImageTarget : ImageResult);
                    if (image.Source == null) { evt.Handled = true; return; }

                    item_copyto_source.Visibility = is_source ? Visibility.Collapsed : Visibility.Visible;
                    item_copyto_target.Visibility = is_target ? Visibility.Collapsed : Visibility.Visible;
                    var show_load = false;
                    if (image is Image) { show_load = !string.IsNullOrEmpty(image.GetInformation().FileName); }
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
                //target.ContextMenu.Items.Clear();
            }
            else
            {
                var result = new ContextMenu() { IsTextSearchEnabled = true, PlacementTarget = target, Tag = source };
                target.ContextMenu = result;
            }
            items.Locale();
            target.ContextMenu.ItemsSource = new ObservableCollection<FrameworkElement>(items);
        }

        private void ChangeLayout(Orientation orientation)
        {
            switch (orientation)
            {
                case Orientation.Horizontal: break;
                case Orientation.Vertical: break;
                default: break;
            }
        }

        private void SetCursor(Cursor cursor)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    Cursor = cursor;
                    DoEvents();
                });
            }
            catch { }
        }

        private Cursor SetBusy()
        {
            var result = Cursor;
            SetCursor(Cursors.Wait);
            return (result);
        }

        private Cursor SetIdle()
        {
            var result = Cursor;
            SetCursor(Cursors.Arrow);
            return (result);
        }
        #endregion

        private const ulong GB = 1024 * 1024 * 1024;
        private const ulong MB = 1024 * 1024;
        private const ulong KB = 1024;

        private void InitMagickNet()
        {
            #region Magick.Net Default Settings
            try
            {
                var magick_cache = Path.IsPathRooted(CachePath) ? CachePath : Path.Combine(AppPath, CachePath);
                //if (!Directory.Exists(magick_cache)) Directory.CreateDirectory(magick_cache);
                //MagickAnyCPU.CacheDirectory = Directory.Exists(magick_cache) ? magick_cache : AppPath;
                //MagickAnyCPU.HasSharedCacheDirectory = true;
                OpenCL.IsEnabled = true;
                if (Directory.Exists(magick_cache)) OpenCL.SetCacheDirectory(magick_cache);
#if DEBUG
                Debug.WriteLine(string.Join(", ", OpenCL.Devices.Select(d => d.Name)));
#endif
                ResourceLimits.MaxMemoryRequest = 4 * GB;
                ResourceLimits.Memory = 4 * GB;
                ResourceLimits.LimitMemory(new Percentage(10));
                ResourceLimits.Thread = 4;
                //ResourceLimits.Area = 4096 * 4096;
                //ResourceLimits.Throttle = 


                //<policymap>
                //  <policy domain=""delegate"" rights=""none"" pattern=""*"" />
                //  <policy domain=""coder"" rights=""none"" pattern=""*"" />
                //  <policy domain=""coder"" rights=""read|write"" pattern=""{GIF,JPEG,PNG,WEBP}"" />
                //</policymap>

                //                var magick_config = ConfigurationFiles.Default;
                //                magick_config.Policy.Data = @"
                //<policymap>
                //  <policy domain=""delegate"" rights=""none"" pattern=""*"" />
                //  <policy domain=""coder"" rights=""none"" pattern=""*"" />
                //</policymap>";
                //                MagickNET.Initialize();
            }
            catch (Exception ex) { ex.ShowMessage(); }

            Extensions.AllSupportedFormats = Extensions.GetSupportedImageFormats();
            Extensions.AllSupportedExts = Extensions.AllSupportedFormats.Keys.ToList().Skip(4).Select(ext => $".{ext.ToLower()}").Where(ext => !ext.Equals(".txt")).ToList();
            var exts = Extensions.AllSupportedExts.Select(ext => $"*{ext}");
            Extensions.AllSupportedFiles = string.Join(";", exts);
            Extensions.AllSupportedFilters = string.Join("|", Extensions.AllSupportedFormats.Select(f => $"{f.Value}|*.{f.Key}"));

            CompareResizeGeometry = new MagickGeometry($"{MaxCompareSize}x{MaxCompareSize}>");
            #endregion
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

        #region Window Events
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RestoreWindowLocationSize();
            RestoreWindowState();

            //System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;
            InitMagickNet();

            #region Some Default UI Settings
            ImageCompositeBlend.Value = 50;

            ProcessStatus.Opacity = 0.66;
            Icon = new BitmapImage(new Uri("pack://application:,,,/ImageCompare;component/Resources/Compare.ico"));
            ChangeTheme();
            #endregion

            #region Default Zoom Ratio
            //ZoomFitAll.IsChecked = true;
            //ImageActions_Click(ZoomFitAll, e);
            ZoomMin = ZoomRatio.Minimum;
            ZoomMax = ZoomRatio.Maximum;
            #endregion

            LocaleUI(DefaultCultureInfo);

            #region Create ErrorMetric Mode Selector
            cm_compare_mode = new ContextMenu() { IsTextSearchEnabled = true, PlacementTarget = ImageCompare };
            foreach (var v in Enum.GetValues(typeof(ErrorMetric)))
            {
                var item = new MenuItem()
                {
                    Header = v.ToString(),
                    Tag = v,
                    IsChecked = (ErrorMetric)v == ErrorMetricMode
                };
                item.Click += (obj, evt) =>
                {
                    var menu = obj as MenuItem;
                    foreach (MenuItem m in cm_compare_mode.Items) m.IsChecked = false;
                    menu.IsChecked = true;
                    ErrorMetricMode = (ErrorMetric)menu.Tag;
                    if (!LastOpIsComposite) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite));
                };
                cm_compare_mode.Items.Add(item);
            }
            ImageCompare.ContextMenu = cm_compare_mode;
            #endregion
            #region Create Compose Mode Selector
            var mi_count = 0;
            cm_compose_mode = new ContextMenu() { IsTextSearchEnabled = true, PlacementTarget = ImageCompose };
            foreach (var v in Enum.GetValues(typeof(CompositeOperator)))
            {
                mi_count++;
                var item = new MenuItem()
                {
                    Header = v.ToString(),
                    Tag = v,
                    IsChecked = (CompositeOperator)v == CompositeMode
                };
                item.Click += (obj, evt) =>
                {
                    var menu = obj as MenuItem;
                    foreach (MenuItem m in cm_compose_mode.Items) m.IsChecked = false;
                    menu.IsChecked = true;
                    CompositeMode = (CompositeOperator)menu.Tag;
                    if (LastOpIsComposite) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite));
                };
                cm_compose_mode.Items.Add(item);
            }
            ImageCompose.ContextMenu = cm_compose_mode;
            #endregion
            #region Create Channels Selector
            var names = new string[] { "Undefined", "All", "-", "RGB", "RGBA", "Red", "Green", "Blue", "-", "CMYK", "CMYKA", "Cyan", "Magenta", "Yellow", "Black", "-", "Alpha", "Opacity", "Gray", "Index", "TrueAlpha", "Composite" };
            var rgb = new string[] { "Red", "Green", "Blue", "RGB", "RGBA" };
            var cmyk = new string[] { "Cyan", "Magenta", "Yellow", "Black", "CMYK", "CMYKA" };
            var gray = new string[] { "Gray" };
            var cm_channels_mode = new ContextMenu() { IsTextSearchEnabled = true, PlacementTarget = UsedChannels };
            //foreach (string v in Enum.GetNames(typeof(Channels)))
            foreach (string v in names)
            {
                try
                {
                    dynamic item = null;
                    if (v.Equals("-")) item = new Separator();
                    else
                    {
                        item = new MenuItem()
                        {
                            Header = v,
                            Tag = v.Equals("-") ? null : Enum.Parse(typeof(Channels), v, true),
                            IsChecked = v.Equals("All"),
                        };
                        (item as MenuItem).Click += (obj, evt) =>
                        {
                            foreach (var m in cm_channels_mode.Items) { if (m is MenuItem) (m as MenuItem).IsChecked = false; }
                            if (obj is MenuItem)
                            {
                                var menu = obj as MenuItem;
                                menu.IsChecked = true;
                                CompareImageChannels = (Channels)menu.Tag;
                                RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite));
                            }
                        };
                    }
                    //if (cm_channels_mode.Items.Cast<MenuItem>().Where(i => i.Header.Equals(item.Header)).Count() < 1)
                    cm_channels_mode.Items.Add(item);
                }
                catch (Exception ex) { ex.ShowMessage($"CreateChannelsSelector: {v}"); }
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
            #region Create Grayscale Mode Selector
            cm_grayscale_mode = new ContextMenu() { IsTextSearchEnabled = true, PlacementTarget = UseColorImage };
            foreach (var v in Enum.GetValues(typeof(PixelIntensityMethod)))
            {
                var item = new MenuItem()
                {
                    Header = v.ToString(),
                    Tag = v,
                    IsChecked = (PixelIntensityMethod)v == GrayscaleMode
                };
                item.Click += (obj, evt) =>
                {
                    var menu = obj as MenuItem;
                    foreach (MenuItem m in cm_grayscale_mode.Items) m.IsChecked = false;
                    menu.IsChecked = true;
                    GrayscaleMode = (PixelIntensityMethod)menu.Tag;
                    if (!LastOpIsComposite) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite));
                };
                cm_grayscale_mode.Items.Add(item);
            }
            UseColorImage.ContextMenu = cm_grayscale_mode;
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

            BusyNow.Visibility = Visibility.Collapsed;

            SyncColorLighting();
            DoEvents();

            var args = Environment.GetCommandLineArgs();
            LoadImageFromFiles(args.Skip(1).ToArray());
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            LastWinState = WindowState == System.Windows.WindowState.Maximized ? System.Windows.WindowState.Maximized : System.Windows.WindowState.Normal;
            if (WindowState == System.Windows.WindowState.Normal)
                LastPositionSize = new Rect(Left, Top, Width, Height);
            SaveConfig();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (!IsLoaded) return;
            if (WindowState == System.Windows.WindowState.Normal)
                LastPositionSize = new Rect(Left, Top, Width, Height);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //if (!IsLoaded) return;
            if (WindowState == System.Windows.WindowState.Normal)
                LastPositionSize = new Rect(Left, Top, e.NewSize.Width, e.NewSize.Height);
            else
                LastPositionSize = new Rect(Left, Top, LastPositionSize.Width, LastPositionSize.Height);
            CalcDisplay(set_ratio: true);
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (!IsLoaded) return;
            LastWinState = WindowState;
            LastPositionSize = new Rect(Left, Top, LastPositionSize.Width, LastPositionSize.Height);
            //if (WindowState != System.Windows.WindowState.Normal)
            //    LastPositionSize = new Rect(Top, Left, Width, Height);
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            var fmts = e.Data.GetFormats(true);
#if DEBUG
            Debug.WriteLine(string.Join(", ", fmts));
#endif
            if (fmts.Contains("FileDrop") || fmts.Contains("Text"))
            {
                e.Effects = DragDropEffects.Copy;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            var fmts = e.Data.GetFormats(true);
            if (e.Data.GetDataPresent("FileDrop"))
            {
                var files = e.Data.GetData("FileDrop");
                if (files is IEnumerable<string>)
                {
                    if (sender == ImageLoadHaldLut)
                        LoadHaldLutFile((files as IEnumerable<string>).Where(f => File.Exists(f)).First());
                    else
                        LoadImageFromFiles((files as IEnumerable<string>).ToArray(), e.Source == ImageSourceScroll || e.Source == ImageSource);
                }
            }
            else if (e.Data.GetDataPresent("Text"))
            {
                var files = (e.Data.GetData("Text") as string).Split();
                if (files is IEnumerable<string>)
                {
                    if (sender == ImageLoadHaldLut)
                        LoadHaldLutFile(files.Where(f => File.Exists(f)).First());
                    else
                        LoadImageFromFiles(files as IEnumerable<string>, e.Source == ImageSourceScroll || e.Source == ImageSource);
                }
            }
            e.Handled = true;
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
                        else if (Keyboard.Modifiers == ModifierKeys.Alt)
                            CreateColorImage(true);
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
                        else if (Keyboard.Modifiers == ModifierKeys.Alt)
                            CreateColorImage(false);
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
                    else if (e.Key == Key.R || e.SystemKey == Key.R)
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            if (ImageSourceScroll.IsMouseDirectlyOver) ResetImage(true);
                            else if (ImageTargetScroll.IsMouseDirectlyOver) ResetImage(false);
                        }
                        else if (Keyboard.Modifiers == ModifierKeys.Control)
                        {
                            if (ImageSourceScroll.IsMouseDirectlyOver) ReloadImage(true);
                            else if (ImageTargetScroll.IsMouseDirectlyOver) ReloadImage(false);
                        }
                        else RenderRun(LastAction);
                    }
                    _last_key_ = e.Key;
                    _last_key_time_ = DateTime.Now;
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
        }
        #endregion

        #region ImageScroll Events
        private void ImageScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {

        }

        private async void ImageScroll_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                e.Handled = true;
                Close();
            }
            else if (e.ChangedButton == MouseButton.XButton1)
            {
                e.Handled = true;
                var action = false;
                var reload_type = GetImageType(sender);
                if (reload_type == ImageType.Source)
                    action |= await LoadImageFromNextFile(true);
                else if (reload_type == ImageType.Target)
                    action |= await LoadImageFromNextFile(false);

                if (action) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload_type: reload_type));
            }
            else if (e.ChangedButton == MouseButton.XButton2)
            {
                e.Handled = true;
                var action = false;
                var reload_type = GetImageType(sender);
                if (reload_type == ImageType.Source)
                    action |= await LoadImageFromPrevFile(true);
                else if (reload_type == ImageType.Target)
                    action |= await LoadImageFromPrevFile(false);

                if (action) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload_type: reload_type));
            }
            else ImageBox_MouseDown(sender, e);
        }
        #endregion

        #region ImageBox Events
        private void ImageBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ZoomFitNone.IsChecked ?? false && (ImageSource.Source != null || ImageTarget.Source != null))
            {
                e.Handled = true;
                ZoomRatio.Value += e.Delta < 0 ? -1 * ZoomRatio.SmallChange : ZoomRatio.SmallChange;
                if (sender is Viewbox || sender is ScrollViewer)
                {
                    SyncScrollOffset(GetScrollOffset(sender as FrameworkElement));
                }
            }
        }

        private void ImageBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
            try
            {
                if (e.Device is MouseDevice)
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        var image_s = ImageSource.GetInformation();
                        var image_t = ImageTarget.GetInformation();
                        var image_r = ImageResult.GetInformation();
                        if (sender == ImageSourceBox || sender == ImageSourceScroll)
                        {
                            e.Handled = true;
                            mouse_start = e.GetPosition(ImageSourceScroll);
                            mouse_origin = new Point(ImageSourceScroll.HorizontalOffset, ImageSourceScroll.VerticalOffset);
                            var pos = e.GetPosition(ImageSource);
                            ImageSource.GetInformation().LastClickPos = new PointD(pos.X, pos.Y);
                        }
                        else if (sender == ImageTargetBox || sender == ImageTargetScroll)
                        {
                            e.Handled = true;
                            mouse_start = e.GetPosition(ImageTargetScroll);
                            mouse_origin = new Point(ImageTargetScroll.HorizontalOffset, ImageTargetScroll.VerticalOffset);
                            var pos = e.GetPosition(ImageTarget);
                            ImageTarget.GetInformation().LastClickPos = new PointD(pos.X, pos.Y);
                        }
                        else if (sender == ImageResultBox || sender == ImageResultScroll)
                        {
                            e.Handled = true;
                            mouse_start = e.GetPosition(ImageResultScroll);
                            mouse_origin = new Point(ImageResultScroll.HorizontalOffset, ImageResultScroll.VerticalOffset);
                            var pos = e.GetPosition(ImageResult);
                            ImageResult.GetInformation().LastClickPos = new PointD(pos.X, pos.Y);
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ShowMessage("MouseClick"); }
        }

        private void ImageBox_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    e.Handled = true;
                    var offset = sender is Viewbox || sender is ScrollViewer ? CalcScrollOffset(sender as FrameworkElement, e) : new Point(-1, -1);
#if DEBUG
                    Debug.WriteLine($"Original : [{mouse_origin.X:F0}, {mouse_origin.Y:F0}], Start : [{mouse_start.X:F0}, {mouse_start.Y:F0}] => Move : [{offset.X:F0}, {offset.Y:F0}]");
                    //Debug.WriteLine($"Move Y: {offset_y}");
#endif
                    SyncScrollOffset(offset);
                }
            }
            catch (Exception ex) { ex.ShowMessage("MouseMove"); }
        }

        private void ImageBox_MouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    if (sender == ImageSourceBox || sender == ImageSourceScroll)
                    {
                        if (ImageSource.GetInformation().ValidCurrent)
                        {
                            e.Handled = true;
                            mouse_start = e.GetPosition(ImageSourceScroll);
                            mouse_origin = new Point(ImageSourceScroll.HorizontalOffset, ImageSourceScroll.VerticalOffset);
                        }
                    }
                    else if (sender == ImageTargetBox || sender == ImageTargetScroll)
                    {
                        if (ImageTarget.GetInformation().ValidCurrent)
                        {
                            e.Handled = true;
                            mouse_start = e.GetPosition(ImageTargetScroll);
                            mouse_origin = new Point(ImageTargetScroll.HorizontalOffset, ImageTargetScroll.VerticalOffset);
                        }
                    }
                    else if (sender == ImageResultBox || sender == ImageResultScroll)
                    {
                        if (ImageResult.GetInformation().ValidCurrent)
                        {
                            e.Handled = true;
                            mouse_start = e.GetPosition(ImageResultScroll);
                            mouse_origin = new Point(ImageResultScroll.HorizontalOffset, ImageResultScroll.VerticalOffset);
                        }
                    }
                }
                else if (sender is FrameworkElement)
                {
                    var tooltip_opened = false;
                    foreach (var element in new FrameworkElement[] { ImageSource, ImageTarget, ImageResult })
                    {
                        if (element.ToolTip is ToolTip && (element.ToolTip as ToolTip).IsOpen)
                        {
                            (element.ToolTip as ToolTip).IsOpen = false;
                            tooltip_opened = true;
                            break;
                        }
                    }
                    if (tooltip_opened)
                    {
                        if ((sender == ImageSource || sender == ImageSourceBox || sender == ImageSourceScroll) && ImageSource.ToolTip is ToolTip)
                        {
                            (ImageSource.ToolTip as ToolTip).IsOpen = true;
                        }
                        else if ((sender == ImageTarget || sender == ImageTargetBox || sender == ImageTargetScroll) && ImageTarget.ToolTip is ToolTip)
                        {
                            (ImageTarget.ToolTip as ToolTip).IsOpen = true;
                        }
                        else if ((sender == ImageResult || sender == ImageResultBox || sender == ImageResultScroll) && ImageResult.ToolTip is ToolTip)
                        {
                            (ImageResult.ToolTip as ToolTip).IsOpen = true;
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ShowMessage("MouseEnter"); }
        }

        private void ImageBox_MouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    e.Handled = true;
                    var offset = sender is Viewbox || sender is ScrollViewer ? CalcScrollOffset(sender as FrameworkElement, e) : new Point(-1, -1);
                }
            }
            catch (Exception ex) { ex.ShowMessage("MouseLeave"); }
        }
        #endregion

        #region Image & ContextMenu Events
        private void Image_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement)
                {
                    this.InvokeAsync(async () =>
                    {
                        try
                        {
                            //var image = sender as Image;
                            Image image = null;
                            var is_source = e.Source == ImageSourceScroll || e.Source == ImageSourceBox || e.Source == ImageSource;
                            var is_target = e.Source == ImageTargetScroll || e.Source == ImageTargetBox || e.Source == ImageTarget;
                            var is_result = e.Source == ImageResultScroll || e.Source == ImageResultBox || e.Source == ImageResult;
                            if (is_source) image = ImageSource;
                            else if (is_target) image = ImageTarget;
                            else if (is_result) image = ImageResult;

                            var element = sender as FrameworkElement;
                            if (element.ToolTip is string && (element.ToolTip as string).StartsWith(WaitingString, StringComparison.CurrentCultureIgnoreCase))
                            {
                                element.ToolTip = await image.GetInformation().GetImageInfo();
                            }
                            else if (element.ToolTip is ToolTip && (element.ToolTip as ToolTip).Content is string && ((element.ToolTip as ToolTip).Content as string).StartsWith(WaitingString, StringComparison.CurrentCultureIgnoreCase))
                            {
                                (element.ToolTip as ToolTip).Content = await image.GetInformation().GetImageInfo();
                            }
                        }
                        catch (Exception ex) { ex.ShowMessage(); }
                    });
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        private void ImageActions_Click(object sender, RoutedEventArgs e)
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

            else if (sender == DarkBackground)
            {
                ChangeTheme();
            }
            else if (sender == AutoSaveOptions)
            {
                SaveConfig(force: true);
            }

            else if (sender == ImageOpenSource)
            {
                RenderRun(new Action(async () =>
                {
                    var action = await ImageSource.GetInformation().LoadImageFromFile();
                    if (action) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true));
                }));
            }
            else if (sender == ImageOpenTarget)
            {
                RenderRun(new Action(async () =>
                {
                    var action = await ImageTarget.GetInformation().LoadImageFromFile();
                    if (action) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true));
                }));
            }
            else if (sender == CreateImageWithColorSource)
            {
                RenderRun(new Action(() =>
                {
                    CreateColorImage(true);
                    UpdateImageViewer(compose: LastOpIsComposite, assign: true);
                }));
            }
            else if (sender == CreateImageWithColorTarget)
            {
                RenderRun(new Action(() =>
                {
                    CreateColorImage(false);
                    UpdateImageViewer(compose: LastOpIsComposite, assign: true);
                }));
            }

            else if (sender == ImagePasteSource)
            {
                RenderRun(new Action(async () =>
                {
                    var action = await ImageSource.GetInformation().LoadImageFromClipboard();
                    if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true);
                }));
            }
            else if (sender == ImagePasteTarget)
            {
                RenderRun(new Action(async () =>
                {
                    var action = await ImageTarget.GetInformation().LoadImageFromClipboard();
                    if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true);
                }));
            }
            else if (sender == ImageClear)
            {
                RenderRun(() => CleanImage());
            }
            else if (sender == ImageExchange)
            {
                var st = ImageSource.GetInformation();
                var tt = ImageTarget.GetInformation();
                (st.Current, tt.Current) = (new MagickImage(tt.Current), new MagickImage(st.Current));
                RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false, autocompare: true));
            }
            else if (sender == RepeatLastAction)
            {
                RenderRun(LastAction);
            }
            else if (sender == ImageCompose)
            {
                RenderRun(new Action(() =>
                {
                    LastOpIsComposite = true;
                    UpdateImageViewer(compose: true, autocompare: true);
                }));
            }
            else if (sender == ImageCompare)
            {
                RenderRun(new Action(() =>
                {
                    LastOpIsComposite = false;
                    UpdateImageViewer(compose: false, autocompare: true);
                }));
            }
            else if (sender == ImageDenoiseResult)
            {
                var shift = Keyboard.Modifiers == ModifierKeys.Shift;
                RenderRun(new Action(() =>
                {
                    ImageResult.GetInformation().Denoise(WeakEffects ? 3 : 5, more: shift);
                    ImageResult.GetInformation().DenoiseCount++;
                }));
            }
            else if (sender == ImageCopyResult)
            {
                ImageResult.GetInformation().CopyToClipboard();
            }
            else if (sender == ImageSaveResult)
            {
                var InfoResult = ImageResult.GetInformation();
                if (InfoResult is ImageInformation)
                {
                    if (InfoResult.HighlightColor == null) InfoResult.HighlightColor = HighlightColor;
                    if (InfoResult.LowlightColor == null) InfoResult.LowlightColor = LowlightColor;
                    if (InfoResult.MasklightColor == null) InfoResult.MasklightColor = MasklightColor;
                    InfoResult.Save();
                }
            }
            else if (sender == ImageLayout)
            {
                if (ImageLayout.IsChecked ?? false)
                    ViewerPanel.Orientation = Orientation.Vertical;
                else
                    ViewerPanel.Orientation = Orientation.Horizontal;
                CalcDisplay();
            }
            else if (sender == ZoomFitNone)
            {
                CurrentZoomFitMode = ZoomFitMode.None;
            }
            else if (sender == ZoomFitAll)
            {
                CurrentZoomFitMode = ZoomFitMode.All;
            }
            else if (sender == ZoomFitWidth)
            {
                CurrentZoomFitMode = ZoomFitMode.Width;
            }
            else if (sender == ZoomFitHeight)
            {
                CurrentZoomFitMode = ZoomFitMode.Height;
            }
            else if (sender == AutoMatchSize)
            {
                RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true));
            }
            else if (sender == UseSmallerImage)
            {
                SmallSizeModeIndep.IsChecked = true;
                SmallSizeModeLink.IsChecked = false;
                RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true));
            }
            else if (sender == SmallSizeModeIndep)
            {
                ScaleMode = ImageScaleMode.Independence;
                SmallSizeModeIndep.IsChecked = true;
                SmallSizeModeLink.IsChecked = false;
                RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true));
            }
            else if (sender == SmallSizeModeLink)
            {
                ScaleMode = ImageScaleMode.Relative;
                SmallSizeModeIndep.IsChecked = false;
                SmallSizeModeLink.IsChecked = true;
                RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true));
            }
            else if (sender == UseColorImage)
            {
                RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: false, reload: true));
            }
            else if (sender == UsedChannels)
            {
                UsedChannels.ContextMenu.IsOpen = true;
            }
            else if (sender == ImageLoadHaldLut)
            {
                LoadHaldLutFile();
            }
        }

        private void MatchSizeAlign_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem)
            {
                var old_align = DefaultMatchAlign;
                var menu = sender as MenuItem;
                foreach (var m in MatchSizeAlign.Items) { if (m is MenuItem) (m as MenuItem).IsChecked = false; }
                menu.IsChecked = true;
                if (menu == MatchSizeAlignTL) { DefaultMatchAlign = Gravity.Northwest; }
                else if (menu == MatchSizeAlignTC) { DefaultMatchAlign = Gravity.North; }
                else if (menu == MatchSizeAlignTR) { DefaultMatchAlign = Gravity.Northeast; }

                else if (menu == MatchSizeAlignCL) { DefaultMatchAlign = Gravity.West; }
                else if (menu == MatchSizeAlignCC) { DefaultMatchAlign = Gravity.Center; }
                else if (menu == MatchSizeAlignCR) { DefaultMatchAlign = Gravity.East; }

                else if (menu == MatchSizeAlignBL) { DefaultMatchAlign = Gravity.Southwest; }
                else if (menu == MatchSizeAlignBC) { DefaultMatchAlign = Gravity.South; }
                else if (menu == MatchSizeAlignBR) { DefaultMatchAlign = Gravity.Southeast; }

                e.Handled = true;

                if (!CompareImageAutoMatchSize && !old_align.Equals(DefaultMatchAlign))
                {
                    RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false));
                    //switch (LastMatchedImage)
                    //{
                    //    case ImageType.Source: RenderRun(() => { ResizeToImage(false, reset: true, align: DefaultMatchAlign); }); break;
                    //    case ImageType.Target: RenderRun(() => { ResizeToImage(true, reset: true, align: DefaultMatchAlign); }); break;
                    //    default: break;
                    //}
                }
                else if (!LastOpIsComposite && CompareImageAutoMatchSize) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true));
            }
        }
        #endregion

        #region Misc UI Control Events
        private void ZoomRatio_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (IsLoaded && (ZoomFitNone.IsChecked ?? false) && e.OldValue != e.NewValue)
                {
                    e.Handled = true;
                    ZoomRatio.ToolTip = $"{"Zoom Ratio".T(DefaultCultureInfo)}: {e.NewValue:F2}X";
                    LastZoomRatio = e.NewValue;
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        private void ImageCompareFuzzy_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            e.Handled = true;
            if (IsLoaded)
            {
                ImageCompareFuzzy.ToolTip = $"{"Tolerances".T(DefaultCultureInfo)}: {e.NewValue:F1}%";
                RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite));
            }
        }

        private void ImageCompositeBlend_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            e.Handled = true;
            if (IsLoaded)
            {
                ImageCompositeBlend.ToolTip = $"{"Blend".T(DefaultCultureInfo)}: {e.NewValue:F0}%";
                if (LastOpIsComposite) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite));
            }
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
                    var image_s = ImageSource.GetInformation();
                    var image_t = ImageTarget.GetInformation();
                    image_s.CurrentGeometry = CompareResizeGeometry;
                    image_t.CurrentGeometry = CompareResizeGeometry;
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        private void ColorPick_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (sender == HighlightColorPick)
            {
                var c = (sender as ColorPicker).SelectedColor ?? null;
                HighlightColor = c == null || c == Colors.Transparent ? null : MagickColor.FromRgba(c.Value.R, c.Value.G, c.Value.B, c.Value.A);
                SyncColorLighting();
            }
            else if (sender == LowlightColorPick)
            {
                var c = (sender as ColorPicker).SelectedColor ?? null;
                LowlightColor = c == null || c == Colors.Transparent ? null : MagickColor.FromRgba(c.Value.R, c.Value.G, c.Value.B, c.Value.A);
                SyncColorLighting();
            }
            else if (sender == MasklightColorPick)
            {
                var c = (sender as ColorPicker).SelectedColor ?? null;
                MasklightColor = c == null || c == Colors.Transparent ? null : MagickColor.FromRgba(c.Value.R, c.Value.G, c.Value.B, c.Value.A);
                SyncColorLighting();
            }
        }
        #endregion

    }
}
