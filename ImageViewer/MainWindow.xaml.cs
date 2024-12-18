﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using ImageMagick;
using Xceed.Wpf.Toolkit;
using Xceed.Wpf.Toolkit.PropertyGrid;

namespace ImageViewer
{
#pragma warning disable IDE0039
#pragma warning disable IDE0044
#pragma warning disable IDE0051
#pragma warning disable IDE0052
#pragma warning disable IDE0060

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
        //private static readonly string AppName = Path.GetFileNameWithoutExtension(AppPath);
        //private static readonly string AppFullName = Application.ResourceAssembly.FullName.Split(',').First().Trim();
        private static string CachePath =  "cache";

        private bool Ready => Dispatcher?.Invoke(() => IsLoaded) ?? false;

        private double TaskTimeOutSeconds = 60;
        private double CountDownTimeOut = 500;


        /// <summary>
        ///
        /// </summary>
        private bool AutoSaveConfig
        {
            get { return (AutoSaveOptions?.Dispatcher?.Invoke(() => AutoSaveOptions.IsChecked ?? true) ?? false); }
            set { AutoSaveOptions?.Dispatcher?.Invoke(() => AutoSaveOptions.IsChecked = value); }
        }

        /// <summary>
        ///
        /// </summary>
        private bool DarkTheme
        {
            get { return (DarkBackground?.Dispatcher?.Invoke(() => DarkBackground.IsChecked ?? true) ?? false); }
            set { DarkBackground?.Dispatcher?.Invoke(() => DarkBackground.IsChecked = value); }
        }

        private readonly FontFamily CustomMonoFontFamily = new FontFamily();
        private readonly FontFamily CustomIconFontFamily = new FontFamily();

        private string DefaultWindowTitle = string.Empty;

        private Gravity DefaultMatchAlign = Gravity.Center;

        private Rect LastPositionSize = new Rect();
        private System.Windows.WindowState LastWinState = System.Windows.WindowState.Normal;
        //private Screen screens = Screen..AllScreens;

        private CultureInfo DefaultCultureInfo = CultureInfo.CurrentCulture;
        #endregion

        #region DoEvent Helper
        private static object ExitFrame(object state)
        {
            ((DispatcherFrame)state).Continue = false;
            return null;
        }

        private static readonly SemaphoreSlim CanDoEvents = new SemaphoreSlim(1, 1);
        public static async void DoEvents()
        {
            if (await CanDoEvents?.WaitAsync(0))
            {
                try
                {
                    if (Application.Current?.Dispatcher?.CheckAccess() ?? false)
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
                        if (Application.Current?.Dispatcher?.CheckAccess() ?? false)
                        {
                            DispatcherFrame frame = new DispatcherFrame();
                            //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate { }));
                            //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate { }));

                            //await Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrame), frame);
                            //await Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(ExitFrame), frame);
                            await Application.Current?.Dispatcher?.BeginInvoke(DispatcherPriority.Send, new DispatcherOperationCallback(ExitFrame), frame);
                            Dispatcher.PushFrame(frame);
                        }
                    }
                    catch (Exception)
                    {
                        //await Task.Delay(1);
                    }
                }
                finally
                {
                    //CanDoEvents.Release(max: 1);
                    if (CanDoEvents?.CurrentCount <= 0) CanDoEvents?.Release();
                }
            }
        }
        #endregion

        #region Render Background Worker Routines
        private Action LastAction { get; set; } = null;
        private BackgroundWorker RenderWorker = null;

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
                            action.Invoke();
                        });
                        LastAction = action;
                    }
                };
            }
        }

        private void RenderRun(Action action, object sender = null)
        {
            InitRenderWorker();
            if (!(RenderWorker?.IsBusy ?? true) && !IsBusy && action is Action)
            {
                IsBusy = true;
                IsProcessingViewer = true;
                RenderWorker.RunWorkerAsync(action);
            }
        }
        #endregion

        #region Magick.Net Settings
        private uint MaxCompareSize = 1024;
        private MagickGeometry CompareResizeGeometry = null;

        private double LastZoomRatio = 1;
        private readonly bool LastOpIsComposite = false;
        //private ImageType LastImageType = ImageType.Result;
        //private double ImageDistance = 0;

        //private double LastCompositeBlendRatio = 50;

        private PixelIntensityMethod GrayscaleMode = PixelIntensityMethod.Undefined;

#if Q16HDRI
        private IMagickColor<float> HighlightColor = MagickColors.Red;
        private IMagickColor<float> LowlightColor = MagickColors.White;
        private IMagickColor<float> MasklightColor = MagickColors.Transparent;
#else
        private IMagickColor<byte> HighlightColor = MagickColors.Red;
        private IMagickColor<byte> LowlightColor = MagickColors.White;
        private IMagickColor<byte> MasklightColor = MagickColors.Transparent;
#endif

        private string LastHaldFolder { get; set; } = string.Empty;
        private string LastHaldFile { get; set; } = string.Empty;

        private bool WeakBlur { get { return (UseWeakBlur.Dispatcher?.Invoke(() => UseWeakBlur.IsChecked ?? false) ?? true); } }
        private bool WeakSharp { get { return (UseWeakSharp.Dispatcher?.Invoke(() => UseWeakSharp.IsChecked ?? false) ?? true); } }
        private bool WeakEffects { get { return (UseWeakEffects.Dispatcher?.Invoke(() => UseWeakEffects.IsChecked ?? false) ?? true); } }

        private const ulong GB = 1024 * 1024 * 1024;
        private const ulong MB = 1024 * 1024;
        private const ulong KB = 1024;

        /// <summary>
        ///
        /// </summary>
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
            Extensions.AllSupportedExts = Extensions.AllSupportedFormats.Keys.Skip(4).Select(ext => $".{ext.ToLower()}").Where(ext => !ext.Equals(".txt")).ToList();
            var exts = Extensions.AllSupportedExts.Select(ext => $"*{ext}");
            Extensions.AllSupportedFiles = string.Join(";", exts);
            Extensions.AllSupportedFilters = string.Join("|", Extensions.AllSupportedFormats.Select(f => $"{f.Value}|*.{f.Key}"));

            CompareResizeGeometry = new MagickGeometry($"{MaxCompareSize}x{MaxCompareSize}>");
            #endregion
        }
        #endregion

        #region Image Display Helper
        private static readonly string DefaultWaitingString = "Waiting";
        private string WaitingString = DefaultWaitingString;

        private bool? AutoHideToolTip { get; set; } = false;
        private int ToolTipDuration { get; set; } = 5000;

        private double ImageMagnifierZoomFactor { get; set; } = 0.25;
        private double ImageMagnifierRadius { get; set; } = 100;
        private double ImageMagnifierBorderThickness { get; set; } = 1;
        private Color ImageMagnifierBorderBrush { get; set; } = Colors.Silver;

        private readonly SemaphoreSlim _CanUpdate_ = new SemaphoreSlim(1, 1);
        private readonly Dictionary<Color, string> ColorNames = new Dictionary<Color, string>();

        private Point? _last_viewer_pos_ = null;

        private Point mouse_start;
        private Point mouse_origin;

        private Point mouse_start_birdseye;
        private Point mouse_origin_birdseye;

        private double ZoomMin = 0.1;
        private double ZoomMax = 10.0;

        /// <summary>
        ///
        /// </summary>
        private void GetColorNames()
        {
            typeof(Colors).GetProperties();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        private string ColorToHex(Color color)
        {
            return ($"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="picker"></param>
        /// <returns></returns>
        private IList<Color> GetRecentColors(ColorPicker picker)
        {
            var result = new List<Color>();
            if (picker is ColorPicker)
            {
                result.AddRange(picker.RecentColors.Select(c => c.Color ?? Colors.Transparent).ToList().Distinct());
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="picker"></param>
        /// <returns></returns>
        private IList<string> GetRecentHexColors(ColorPicker picker)
        {
            var result = new List<string>();
            if (picker is ColorPicker)
            {
                result.AddRange(picker.RecentColors.Select(c => ColorToHex(c.Color ?? Colors.Transparent)).Distinct());
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="picker"></param>
        /// <param name="colors"></param>
        private void SetRecentColors(ColorPicker picker, IEnumerable<Color> colors)
        {
            if (colors is IEnumerable<Color> && colors.Count() > 0)
            {
                Dispatcher?.Invoke(() =>
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

        /// <summary>
        ///
        /// </summary>
        /// <param name="picker"></param>
        /// <param name="colors"></param>
        private void SetRecentColors(ColorPicker picker, IEnumerable<string> colors)
        {
            if (colors is IEnumerable<string> && colors.Count() > 0)
            {
                SetRecentColors(picker, colors.Select(c => (Color)ColorConverter.ConvertFromString(c)));
            }
        }

        /// <summary>
        ///
        /// </summary>
        private void SyncColorLighting()
        {
            if (Ready)
            {
                ImageViewer?.Dispatcher?.Invoke(() =>
                {
                    if (ImageViewer.Tag == null) ImageViewer.Tag = new ImageInformation() { Tagetment = ImageViewer, HighlightColor = HighlightColor, LowlightColor = LowlightColor, MasklightColor = MasklightColor };
                    else if (ImageViewer.Tag is ImageInformation) { var info = ImageViewer.Tag as ImageInformation; info.Tagetment = ImageViewer; info.HighlightColor = HighlightColor; info.LowlightColor = LowlightColor; info.MasklightColor = MasklightColor; }
                });
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        private Size? GetImageSize(Image element)
        {
            Size? result = null;
            if (element is Image)
            {
                var size = element?.Dispatcher?.Invoke(() => element?.RenderSize);
                if (size?.Width > 0 && size?.Height > 0) result = size;
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        private Image GetImageControl(FrameworkElement element)
        {
            Image result = null;
            if (element == ImageViewer || element == ImageViewerBox || element == ImageViewerScroll)
            {
                result = ImageViewer;
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        private bool IsImageNull(Image element)
        {
            var result = false;
            if (Ready)
            {
                result = element?.Dispatcher?.Invoke(() => element.Source == null) ?? false;
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        /// <param name="image"></param>
        private async void SetImageSource(Image element, ImageSource image)
        {
            if (Ready && image is ImageSource)
            {
                await element?.Dispatcher?.InvokeAsync(() => { try { element.Source = image; } catch { } });
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        /// <param name="image"></param>
        private async void SetImageSource(Image element, ImageInformation image, bool fit = false)
        {
            if (Ready && image is ImageInformation)
            {
                try
                {
                    await element?.Dispatcher?.InvokeAsync(async () =>
                    {
                        try
                        {
                            element.Source = image.Source;
                            if (fit && element.Source != null) FitView();
                            await UpdateImageTooltip();
                        }
                        catch { }
                    });
                }
                catch { }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="calc_colors"></param>
        /// <returns></returns>
        private async Task<string> UpdateImageTooltip(bool calc_colors = false)
        {
            var result = string.Empty;
            if (Ready && ImageViewer is Image)
            {
                var image = ImageViewer.GetInformation();
                UpdateInfoBox(image);
                var tooltip = image.ValidCurrent ? await image.GetImageInfo(include_colorinfo: calc_colors) : null;
                SetToolTip(ImageInfoBox, tooltip);
                //SetToolTip(ImageViewer, tooltip);
                result = tooltip;
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        private async Task<bool> UpdateImageViewerFinished(double timeout = 60)
        {
            var result = false;
            if (_CanUpdate_ is SemaphoreSlim && await _CanUpdate_.WaitAsync(TimeSpan.FromSeconds(timeout)))
            {
                _CanUpdate_.Release();
                result = true;
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="compose"></param>
        /// <param name="assign"></param>
        /// <param name="reload"></param>
        /// <param name="reload_type"></param>
        /// <param name="autocompare"></param>
        private async void UpdateImageViewer(bool compose = false, bool assign = false, bool reload = true, ImageType reload_type = ImageType.All, bool autocompare = false)
        {
            if (Ready && await _CanUpdate_.WaitAsync(TimeSpan.FromMilliseconds(CountDownTimeOut)))
            {
                try
                {
#if DEBUG
                    Debug.WriteLine("---> UpdateImageViewer <---");
#endif
                    IsBusy = true;

                    ImageInformation image_s = ImageViewer.GetInformation();
                    bool? source = null;

                    source = IsImageNull(ImageViewer);

                    var ShowGeometry = new MagickGeometry();

                    if (assign || (source ?? false))
                    {
                        try
                        {
                            if (reload_type == ImageType.All || reload_type == ImageType.Source) IsLoadingViewer = true;

                            if (reload)
                            {
                                if (reload_type == ImageType.All || reload_type == ImageType.Source)
                                {
                                    if (image_s.CurrentSize.Width != image_s.OriginalSize.Width || image_s.CurrentSize.Height != image_s.OriginalSize.Height)
                                        await image_s.Reload(reset: true);
                                }
                            }

                            foreach (var image in new ImageInformation[] { image_s })
                            {
                                image.SourceParams.Geometry = ShowGeometry;
                                image.SourceParams.Align = DefaultMatchAlign;
                                image.SourceParams.FillColor = MasklightColor;
                            }

                            //var size_source = GetImageSize(ImageViewer);

                            if (reload_type == ImageType.All || reload_type == ImageType.Source)
                            {
                                SetImageSource(ImageViewer, image_s);
                                IsLoadingViewer = false;
                            }
                        }
                        catch (Exception ex) { ex.ShowMessage(); }
                    }

                    CalcDisplay();
                }
                catch (Exception ex) { ex.ShowMessage(); }
                finally
                {
                    GC.Collect();

                    IsLoadingViewer = false;
                    IsBusy = false;

                    DoEvents();

                    if (_CanUpdate_?.CurrentCount < 1) _CanUpdate_?.Release();
                }
            }
        }
        #endregion

        #region Image Load/Save Helper
        /// <summary>
        ///
        /// </summary>
        /// <param name="hald"></param>
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
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private async Task<bool> CopyImageToClipboard()
        {
            var result = false;
            IsProcessingViewer = true;
            result = await Task.Run(async () =>
            {
                var ret = false;
                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    if (IsQualityChanger)
                    {
                        var quality = QualityChangerValue?.Dispatcher.Invoke(() => QualityChangerValue?.Value) ?? image.CurrentQuality;
                        using (var target = new MagickImage(image.Current) { Quality = quality })
                        {
                            ret = await image.CopyToClipboard(target).ContinueWith(t => { UpdateIndaicatorState(false, true); return (t.Result); });
                        }
                    }
                    else ret = await image.CopyToClipboard().ContinueWith(t => { UpdateIndaicatorState(false, true); return (t.Result); });
                }
                return (ret);
            });
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private async Task<bool> CopyImageTo()
        {
            var result = await CopyImageToClipboard();
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private async Task<bool> SaveImageAs()
        {
            var ctrl = this.IsCtrlPressed(exclude: true);
            var result = await SaveImageAs(overwrite: ctrl);
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="overwrite"></param>
        /// <returns></returns>
        private async Task<bool> SaveImageAs(bool overwrite = false)
        {
            var result = false;
            IsSavingViewer = true;
            result = await Task.Run(() =>
            {
                var ret = false;
                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    if (IsQualityChanger)
                    {
                        var quality = QualityChangerValue?.Dispatcher.Invoke(() => QualityChangerValue?.Value) ?? image.CurrentQuality;
                        using (var target = new MagickImage(image.Current) { Quality = quality })
                        {
                            ret = image.Save(target, overwrite: overwrite);
                        }
                    }
                    else ret = image.Save(overwrite: overwrite);
                }
                IsSavingViewer = false;
                return (ret);
            });
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private async Task<bool> LoadImageFromClipboard()
        {
            var action = false;
            try
            {
                CloseQualityChanger();
                ResetViewTransform(calcdisplay: false);

                IsLoadingViewer = true;

                IDataObject dataPackage = Dispatcher?.Invoke(() => Clipboard.GetDataObject());
                if (dataPackage != null)
                {
                    var fmts = await Dispatcher?.InvokeAsync(() => dataPackage.GetFormats(true));
                    if (fmts.Contains("Text"))
                    {
                        var text = await Dispatcher?.InvokeAsync(() => dataPackage.GetData("Text", true) as string);
                        if (Regex.IsMatch(text, @"^data:.*?;base64,", RegexOptions.IgnoreCase))
                        {
                            var image = ImageViewer.GetInformation();
                            action |= await image.LoadImageFromClipboard(dataPackage);
                        }
                        else
                        {
                            var files = text.Split(new string[]{ Environment.NewLine, "\n\r", "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
                            files = files.Where(f => File.Exists(f)).ToArray();
                            if (files.Any())
                            {
                                await LoadImageFromFiles(files);
                            }
                        }
                    }
                    else
                    {
                        var image = ImageViewer.GetInformation();
                        action |= await image.LoadImageFromClipboard(dataPackage);
                    }
                }
                if (action)
                {
                    RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true));
                }
                else IsLoadingViewer = false;
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (action);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private async Task<bool> LoadImageFromFirstFile()
        {
            var ret = false;
            try
            {
                CloseQualityChanger();
                IsLoadingViewer = true;

                var image =  ImageViewer.GetInformation();
                ret = await image.LoadImageFromFirstFile();
                if (ret) ClearImage();
                if (ret) ResetViewTransform(calcdisplay: false);
                if (ret) SetTitle(image.FileName);
                if (ret) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true));
                //if (ret && await UpdateImageViewerFinished()) FitView();
                else IsLoadingViewer = false;
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (ret);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private async Task<bool> LoadImageFromPrevFile()
        {
            var ret = false;
            try
            {
                CloseQualityChanger();
                IsLoadingViewer = true;

                var image =  ImageViewer.GetInformation();
                ret = await image.LoadImageFromPrevFile();
                if (ret) ClearImage();
                if (ret) ResetViewTransform(calcdisplay: false);
                if (ret) SetTitle(image.FileName);
                if (ret) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true));
                //if (ret && await UpdateImageViewerFinished()) FitView();
                else IsLoadingViewer = false;
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (ret);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private async Task<bool> LoadImageFromNextFile()
        {
            var ret = false;
            try
            {
                CloseQualityChanger();
                IsLoadingViewer = true;

                var image = ImageViewer.GetInformation();
                ret = await image.LoadImageFromNextFile();
                
                if (ret) ClearImage();
                if (ret) ResetViewTransform(calcdisplay: false);
                if (ret) SetTitle(image.FileName);
                if (ret) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true));
                else IsLoadingViewer = false;
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (ret);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private async Task<bool> LoadImageFromLastFile()
        {
            var ret = false;
            try
            {
                CloseQualityChanger();
                IsLoadingViewer = true;

                var image =  ImageViewer.GetInformation();
                ret = await image.LoadImageFromLastFile();
                if (ret) ClearImage();
                if (ret) ResetViewTransform(calcdisplay: false);
                if (ret) SetTitle(image.FileName);
                if (ret) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true));
                //if (ret && await UpdateImageViewerFinished()) FitView();
                else IsLoadingViewer = false;
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (ret);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private async Task<bool> LoadImageFromFile() => (await LoadImageFromFiles(new string[] { }));

        /// <summary>
        ///
        /// </summary>
        /// <param name="files"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        private async Task<bool> LoadImageFromFiles(string[] files)
        {
            var ret = false;
            try
            {
                files = files.Select(f => f.Trim().Trim('\"')).Where(f => f.IsSupportedExt()).Where(f => !string.IsNullOrEmpty(f) && File.Exists(f)).ToArray();
                if (files.Length > 0)
                {
                    CloseQualityChanger();
                    IsLoadingViewer = true;

                    _ = Task.Run(async () => await files.InitFileList());

                    var image  = ImageViewer.GetInformation();
                    ret |= await image.LoadImageFromFile(files.First());
                    if (ret) ClearImage();
                    if (ret) ResetViewTransform(calcdisplay: false);
                    if (ret) SetTitle(image.FileName);
                    if (ret) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: true));
                    else IsLoadingViewer = false;
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (ret);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="files"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        protected internal async Task<bool> LoadImageFromFiles(IEnumerable<string> files)
        {
            return (await LoadImageFromFiles(files.ToArray()));
        }
        #endregion

        #region UI Indicator Helper
        /// <summary>
        ///
        /// </summary>
        private bool IsBusy
        {
            get => BusyNow?.Dispatcher?.Invoke(() => { return (BusyNow.IsIndeterminate); }) ?? false;
            set => BusyNow?.Dispatcher?.Invoke(() =>
            {
                BusyNow.IsIndeterminate = value;
                BusyNow.Value = value ? 0 : 100;
                DoEvents();
            });
        }

        private string LoadingWaitingStr = string.Empty;
        private string SavingWaitingStr = string.Empty;
        private string ProcessingWaitingStr = string.Empty;

        /// <summary>
        ///
        /// </summary>
        private bool IsLoadingViewer
        {
            get => BusyNow?.Dispatcher?.Invoke(() => { return (IndicatorViewer.IsBusy); }) ?? false;
            set => BusyNow?.Dispatcher?.Invoke(() => { IndicatorViewer.BusyContent = LoadingWaitingStr; IndicatorViewer.IsBusy = value; DoEvents(); });
        }

        /// <summary>
        ///
        /// </summary>
        private bool IsSavingViewer
        {
            get => BusyNow?.Dispatcher?.Invoke(() => { return (IndicatorViewer.IsBusy); }) ?? false;
            set => BusyNow?.Dispatcher?.Invoke(() => { IndicatorViewer.BusyContent = SavingWaitingStr; IndicatorViewer.IsBusy = value; DoEvents(); });
        }

        /// <summary>
        ///
        /// </summary>
        private bool IsProcessingViewer
        {
            get => BusyNow?.Dispatcher?.Invoke(() => { return (IndicatorViewer.IsBusy); }) ?? false;
            set => BusyNow?.Dispatcher?.Invoke(() => { IndicatorViewer.BusyContent = ProcessingWaitingStr; IndicatorViewer.IsBusy = value; DoEvents(); });
        }

        /// <summary>
        ///
        /// </summary>
        private bool IsMagnifier
        {
            get => ImageMagnifier?.Dispatcher?.Invoke(() => { return (ImageMagnifier.IsEnabled); }) ?? false;
            set => ImageMagnifier?.Dispatcher?.Invoke(() => { ImageMagnifier.IsEnabled = value; });
        }

        /// <summary>
        ///
        /// </summary>
        private bool IsQualityChanger
        {
            get => QualityChanger?.Dispatcher?.Invoke(() => { return (QualityChanger.IsVisible); }) ?? false;
        }
        #endregion

        #region UI Helper
        /// <summary>
        ///
        /// </summary>
        /// <param name="topmost"></param>
        public void TopMostWindow(bool? topmost)
        {
            if (Ready) Topmost = topmost ?? false;
        }

        /// <summary>
        ///
        /// </summary>
        public void ChangeTheme()
        {
            try
            {
                var source = new Uri(@"pack://application:,,,/ImageViewer;component/Resources/CheckboardPattern_32.png", UriKind.RelativeOrAbsolute);
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

        /// <summary>
        ///
        /// </summary>
        /// <param name="font"></param>
        /// <param name="fonts"></param>
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

        /// <summary>
        ///
        /// </summary>
        private void RestoreWindowLocationSize()
        {
            Top = LastPositionSize.Top;
            Left = LastPositionSize.Left;
            Width = Math.Min(MaxWidth, Math.Max(MinWidth, LastPositionSize.Width));
            Height = Math.Min(MaxHeight, Math.Max(MinHeight, LastPositionSize.Height));
        }

        /// <summary>
        ///
        /// </summary>
        private void RestoreWindowState()
        {
            if (Ready && LastWinState == System.Windows.WindowState.Maximized) WindowState = LastWinState;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="culture"></param>
        private void LocaleUI(CultureInfo culture = null)
        {
            Title = $"{Uid}.Title".T(culture) ?? Title;
            ImageToolBar.Locale();
            ImageViewerBox.Locale();

            DefaultWindowTitle = Title;

            LoadingWaitingStr = "LoadingIndicatorTitle".T(culture);
            SavingWaitingStr = "SavingIndicatorTitle".T(culture);
            ProcessingWaitingStr = "ProcessingIndicatorTitle".T(culture);

            ZoomRatio.ToolTip = $"{"Zoom Ratio".T(culture)}: {ZoomRatio.Value:F2}X";

            WaitingString = DefaultWaitingString.T(culture);
            ImageViewerBox.ToolTip = null;
            SetToolTipState(ImageViewerBox, false);

            #region Create Image Flip/Rotate/Effects Menu
            CreateImageOpMenu(ImageViewerScroll);
            //CreateImageOpMenu(ImageTargetScroll);
            #endregion
        }
        #endregion

        #region Context Menu Helper
        private ContextMenu cm_grayscale_mode = null;

        private readonly List<FrameworkElement> cm_image_viewer = new List<FrameworkElement>();
        private readonly List<FrameworkElement> cm_image_target = new List<FrameworkElement>();

        /// <summary>
        ///
        /// </summary>
        /// <param name="target"></param>
        private void CreateImageOpMenu(FrameworkElement target)
        {
            bool source = true;
            var effect_blur = new BlurEffect() { Radius = 2, KernelType = KernelType.Gaussian };

            var items = cm_image_viewer;
            if (items != null) items.Clear();
            else items = new List<FrameworkElement>();

            Func<object, bool> MenuHost = (obj) => Dispatcher?.Invoke(() => (bool)(obj as MenuItem).Tag) ?? false;

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
                item_fh.Click += (obj, evt) => FlopView();
                item_fv.Click += (obj, evt) => FlipView();
                item_r090.Click += (obj, evt) => RotateView(90);
                item_r180.Click += (obj, evt) => RotateView(180);
                item_r270.Click += (obj, evt) => RotateView(270);
                item_reset_transform.Click += (obj, evt) => ResetViewTransform();

                item_gray.Click += (obj, evt) => { RenderRun(() => { GrayscaleImage(MenuHost(obj)); }, target); };
                item_blur.Click += (obj, evt) => { RenderRun(() => { BlurImage(MenuHost(obj)); }, target); };
                item_sharp.Click += (obj, evt) => { RenderRun(() => { SharpImage(MenuHost(obj)); }, target); };

                item_size_crop.Click += (obj, evt) => { RenderRun(() => { CropImage(MenuHost(obj)); }, target); };
                item_size_cropedge.Click += (obj, evt) => { RenderRun(() => { CropImageEdge(MenuHost(obj), 1, 1, DefaultMatchAlign); }, target); };
                item_size_extentedge.Click += (obj, evt) => { RenderRun(() => { ExtentImageEdge(MenuHost(obj), 1, 1, DefaultMatchAlign); }, target); };
                item_size_panedge.Click += (obj, evt) => { RenderRun(() => { PanImageEdge(MenuHost(obj), 1, 1, DefaultMatchAlign); }, target); };

                item_load_prev.Click += (obj, evt) => { RenderRun(async () => { await LoadImageFromPrevFile(); }, target); };
                item_load_next.Click += (obj, evt) => { RenderRun(async () => { await LoadImageFromNextFile(); }, target); };

                //item_reset_image.Click += (obj, evt) => { RenderRun(() => { ResetImage(MenuHost(obj)); }, target); };
                item_reload.Click += (obj, evt) => { var shift = Keyboard.Modifiers == ModifierKeys.Shift; RenderRun(() => { ReloadImage(MenuHost(obj), info_only: shift); }, target); };

                item_colorcalc.Click += (obj, evt) => { RenderRun(() => { CalcImageColors(MenuHost(obj)); }, target); };
                item_copyinfo.Click += (obj, evt) => { RenderRun(() => { CopyImageInfo(); }, target); };
                item_copyimage.Click += (obj, evt) => { RenderRun(async () => { await CopyImage(); }, target); };
                item_saveas.Click += async (obj, evt) => await SaveImageAs();
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
                items.Add(new Separator());
                items.Add(item_load_prev);
                items.Add(item_load_next);
                items.Add(new Separator());
                //items.Add(item_reset_image);
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
                item_more_oil.Click += (obj, evt) => { RenderRun(() => { OilImage(MenuHost(obj)); }, target); };
                item_more_charcoal.Click += (obj, evt) => { RenderRun(() => { CharcoalImage(MenuHost(obj)); }, target); };
                item_more_pencil.Click += (obj, evt) => { RenderRun(() => { PencilImage(MenuHost(obj)); }, target); };
                item_more_edge.Click += (obj, evt) => { RenderRun(() => { EdgeImage(MenuHost(obj)); }, target); };
                item_more_emboss.Click += (obj, evt) => { RenderRun(() => { EmbossImage(MenuHost(obj)); }, target); };
                item_more_morph.Click += (obj, evt) => { RenderRun(() => { MorphologyImage(MenuHost(obj)); }, target); };

                item_more_autoequalize.Click += (obj, evt) => { RenderRun(() => { AutoEqualizeImage(MenuHost(obj)); }, target); };
                item_more_autoreducenoise.Click += (obj, evt) => { RenderRun(() => { ReduceNoiseImage(MenuHost(obj)); }, target); };
                item_more_autoenhance.Click += (obj, evt) => { RenderRun(() => { AutoEnhanceImage(MenuHost(obj)); }, target); };
                item_more_autolevel.Click += (obj, evt) => { RenderRun(() => { AutoLevelImage(MenuHost(obj)); }, target); };
                item_more_autocontrast.Click += (obj, evt) => { RenderRun(() => { AutoContrastImage(MenuHost(obj)); }, target); };
                item_more_autowhitebalance.Click += (obj, evt) => { RenderRun(() => { AutoWhiteBalanceImage(MenuHost(obj)); }, target); };
                item_more_autogamma.Click += (obj, evt) => { RenderRun(() => { AutoGammaImage(MenuHost(obj)); }, target); };

                item_more_autovignette.Click += (obj, evt) => { RenderRun(() => { AutoVignetteImage(MenuHost(obj)); }, target); };
                item_more_invert.Click += (obj, evt) => { RenderRun(() => { InvertImage(MenuHost(obj)); }, target); };
                item_more_solarize.Click += (obj, evt) => { RenderRun(() => { SolarizeImage(MenuHost(obj)); }, target); };
                item_more_polaroid.Click += (obj, evt) => { RenderRun(() => { PolaroidImage(MenuHost(obj)); }, target); };
                item_more_posterize.Click += (obj, evt) => { RenderRun(() => { PosterizeImage(MenuHost(obj)); }, target); };
                item_more_medianfilter.Click += (obj, evt) => { RenderRun(() => { MedianFilterImage(MenuHost(obj)); }, target); };

                item_more_blueshift.Click += (obj, evt) => { RenderRun(() => { BlueShiftImage(MenuHost(obj)); }, target); };
                item_more_autothreshold.Click += (obj, evt) => { RenderRun(() => { AutoThresholdImage(MenuHost(obj)); }, target); };
                item_more_haldclut.Click += (obj, evt) => { var shift = Keyboard.Modifiers == ModifierKeys.Shift; RenderRun(() => { HaldClutImage(MenuHost(obj), shift); }, target); };

                item_more_meanshift.Click += (obj, evt) => { RenderRun(() => { MeanShiftImage(MenuHost(obj)); }, target); };
                item_more_kmeans.Click += (obj, evt) => { RenderRun(() => { KmeansImage(MenuHost(obj)); }, target); };
                item_more_segment.Click += (obj, evt) => { RenderRun(() => { SegmentImage(MenuHost(obj)); }, target); };
                item_more_quantize.Click += (obj, evt) => { RenderRun(() => { QuantizeImage(MenuHost(obj)); }, target); };

                item_more_fillflood.Click += (obj, evt) => { RenderRun(() => { FillOutBoundBoxImage(MenuHost(obj)); }, target); };
                item_more_setalphatocolor.Click += (obj, evt) => { RenderRun(() => { SetAlphaToColorImage(MenuHost(obj)); }, target); };
                item_more_setcolortoalpha.Click += (obj, evt) => { RenderRun(() => { SetColorToAlphaImage(MenuHost(obj)); }, target); };
                item_more_createcolorimage.Click += (obj, evt) => { var shift = Keyboard.Modifiers == ModifierKeys.Shift; RenderRun(() => { CreateColorImage(MenuHost(obj)); }, target); };
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
                item_more.Items.Add(item_more_posterize);
                item_more.Items.Add(item_more_polaroid);
                item_more.Items.Add(new Separator());
                item_more.Items.Add(item_more_autovignette);
                item_more.Items.Add(item_more_blueshift);
                item_more.Items.Add(item_more_autothreshold);
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
                    var image = ImageViewer;
                    if (image.Source == null) { evt.Handled = true; return; }

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
            }
            else
            {
                var result = new ContextMenu() { IsTextSearchEnabled = true, PlacementTarget = target, Tag = source };
                target.ContextMenu = result;
            }
            items.Locale();
            target.ContextMenu.ItemsSource = new ObservableCollection<FrameworkElement>(items);
        }
        #endregion

        #region Image Viewer Helper
        /// <summary>
        /// 
        /// </summary>
        private ZoomFitMode CurrentZoomFitMode
        {
            get
            {
                return (Dispatcher?.Invoke(() =>
                {
                    var value = ZoomFitMode.All;
                    if (ZoomFitNone.IsChecked ?? false) value = ZoomFitMode.None;
                    else if (ZoomFitAll.IsChecked ?? false) value = ZoomFitMode.All;
                    else if (ZoomFitWidth.IsChecked ?? false) value = ZoomFitMode.Width;
                    else if (ZoomFitHeight.IsChecked ?? false) value = ZoomFitMode.Height;
                    return (value);
                }) ?? ZoomFitMode.All);
            }
            set
            {
                Dispatcher?.Invoke(() =>
                {
                    if (value == ZoomFitMode.None)
                    {
                        ZoomFitNone.IsChecked = true; ZoomFitAll.IsChecked = false; ZoomFitWidth.IsChecked = false; ZoomFitHeight.IsChecked = false;
                        ImageViewerBox.Stretch = Stretch.None;
                    }
                    else if (value == ZoomFitMode.All)
                    {
                        ZoomFitNone.IsChecked = false; ZoomFitAll.IsChecked = true; ZoomFitWidth.IsChecked = false; ZoomFitHeight.IsChecked = false;
                        ImageViewerBox.Stretch = Stretch.Uniform;
                    }
                    else if (value == ZoomFitMode.Width)
                    {
                        ZoomFitNone.IsChecked = false; ZoomFitAll.IsChecked = false; ZoomFitWidth.IsChecked = true; ZoomFitHeight.IsChecked = false;
                        ImageViewerBox.Stretch = Stretch.None;
                    }
                    else if (value == ZoomFitMode.Height)
                    {
                        ZoomFitNone.IsChecked = false; ZoomFitAll.IsChecked = false; ZoomFitWidth.IsChecked = false; ZoomFitHeight.IsChecked = true;
                        ImageViewerBox.Stretch = Stretch.None;
                    }
                    CalcDisplay();
                    //CenterViewer();
                });
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private FrameworkElement CurrentZoomControl
        {
            get
            {
                FrameworkElement result = null;

                var mode  = CurrentZoomFitMode;
                if (mode == ZoomFitMode.None) result = ZoomFitNone;
                else if (mode == ZoomFitMode.All) result = ZoomFitAll;
                else if (mode == ZoomFitMode.Width) result = ZoomFitWidth;
                else if (mode == ZoomFitMode.Height) result = ZoomFitHeight;

                return (result);
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="set_ratio"></param>
        private void CalcDisplay(Size? size = null)
        {
            if (!Ready) return;
            try
            {
                Dispatcher?.InvokeAsync(() =>
                {
                    using (var d = Dispatcher?.DisableProcessing())
                    {
                        #region Re-Calc Scroll Viewer Size
                        var nw = size?.Width;
                        var nh = size?.Height;
                        var cw = ImageCanvas.ActualWidth;
                        var ch = ImageCanvas.ActualHeight - ImageToolBar.ActualHeight;
                        ImageViewerPanel.MaxWidth = cw;
                        ImageViewerPanel.MaxHeight = ch;
                        ImageViewerPanel.MinWidth = cw;
                        ImageViewerPanel.MinHeight = ch;
                        ImageViewerPanel.RenderSize = new Size(cw, ch);
                        ImageViewerPanel.UpdateLayout();

                        ImageViewerScroll.Width = cw;
                        ImageViewerScroll.Height = ch;
                        ImageViewerScroll.MaxWidth = cw;
                        ImageViewerScroll.MaxHeight = ch;
                        ImageViewerScroll.UpdateLayout();
                        #endregion

                        if (ZoomFitNone.IsChecked ?? false) ZoomRatio.IsEnabled = true;
                        else ZoomRatio.IsEnabled = false;
                        ZoomRatioValue.IsEnabled = ZoomRatio.IsEnabled;

                        CalcZoomRatio();
                        DoEvents();
                    }
                });
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        /// 
        /// </summary>
        private void CalcZoomRatio(Size? size = null)
        {
            try
            {
                var image_s = ImageViewer.GetInformation();
                if (image_s.ValidCurrent)
                {
                    var scroll  = ImageViewerScroll;
                    var image  = image_s.Current;

                    var rotate = ImageViewerRotate.Angle % 180 != 0;
                    var width = rotate ? image.Height : image.Width;
                    var height = rotate ? image.Width : image.Height;

                    if (CurrentZoomFitMode == ZoomFitMode.None)
                    {
                        if (ZoomRatio.Value != LastZoomRatio) ZoomRatio.Value = LastZoomRatio;
                        ZoomRatio.Minimum = LastZoomRatio < ZoomMin ? LastZoomRatio : ZoomMin;
                        ImageViewerBox.MaxWidth = LastZoomRatio * width;
                        ImageViewerBox.MaxHeight = LastZoomRatio * height;
                    }
                    else if (CurrentZoomFitMode == ZoomFitMode.All)
                    {
                        var scale = Math.Min(scroll.ActualWidth / width, scroll.ActualHeight / height);
                        ZoomRatio.Value = scale;
                        ZoomRatio.Minimum = scale < ZoomMin ? scale : ZoomMin;
                        ImageViewerBox.MaxWidth = scale * width;
                        ImageViewerBox.MaxHeight = scale * height;
                    }
                    else if (CurrentZoomFitMode == ZoomFitMode.Width)
                    {
                        var ratio = scroll.ActualWidth / width;
                        var delta = scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Hidden || height * ratio <= scroll.ActualHeight ? 0 : SystemParameters.VerticalScrollBarWidth;
                        var scale = (scroll.ActualWidth - delta) / width;
                        var scale_w = scale * width;
                        var scale_h = scale * height;
                        if (ZoomRatio.Value != scale) ZoomRatio.Value = scale;
                        ZoomRatio.Minimum = scale < ZoomMin ? scale : ZoomMin;
                        ImageViewerBox.MaxWidth = scroll.ActualWidth;
                        ImageViewerBox.MaxHeight = scale_h <= scroll.ActualHeight ? scroll.ActualHeight : scale_h - delta;
                    }
                    else if (CurrentZoomFitMode == ZoomFitMode.Height)
                    {
                        var ratio = scroll.ActualHeight / height;
                        var delta = scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Hidden || width * ratio <= scroll.ActualWidth ? 0 : SystemParameters.HorizontalScrollBarHeight;
                        var scale = (scroll.ActualHeight - delta) / height;
                        var scale_w = scale * width;
                        var scale_h = scale * height;
                        if (ZoomRatio.Value != scale) ZoomRatio.Value = scale;
                        ZoomRatio.Minimum = scale < ZoomMin ? scale : ZoomMin;
                        ImageViewerBox.MaxWidth = scale_w <= scroll.ActualWidth ? scroll.ActualWidth : scale_w - delta;
                        ImageViewerBox.MaxHeight = scroll.ActualHeight;
                    }
                    ImageViewer.UpdateLayout();
                    ImageViewerBox.UpdateLayout();
                    ImageViewerScroll.UpdateLayout();

                    var outbox = ImageViewerBox.ActualWidth > ImageViewerScroll.ActualWidth || ImageViewerBox.ActualHeight > ImageViewerScroll.ActualHeight;
                    SetBirdView(outbox);
                    UpdateBirdView();
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        private Point GetScrollOffset(FrameworkElement sender)
        {
            double offset_x = -1, offset_y = -1;
            if (sender == ImageViewerBox || sender == ImageViewerScroll)
            {
                if (ImageViewerBox.Stretch == Stretch.None)
                {
                    offset_x = ImageViewerScroll.HorizontalOffset;
                    offset_y = ImageViewerScroll.VerticalOffset;
                }
            }
            return (new Point(offset_x, offset_y));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        private Point CalcScrollOffset(FrameworkElement sender, MouseEventArgs e)
        {
            double offset_x = -1, offset_y = -1;
            try
            {
                if (sender == ImageViewerBox || sender == ImageViewerScroll)
                {
                    if (ImageViewerBox.Stretch == Stretch.None && ImageViewer.GetInformation().ValidCurrent)
                    {
                        try
                        {
                            Point factor = new Point(ImageViewerScroll.ExtentWidth/ImageViewerScroll.ViewportWidth, ImageViewerScroll.ExtentHeight/ImageViewerScroll.ViewportHeight);
                            Vector v = mouse_start - e.GetPosition(ImageViewerScroll);
                            offset_x = Math.Max(0, mouse_origin.X + v.X * factor.X);
                            offset_y = Math.Max(0, mouse_origin.Y + v.Y * factor.Y);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (new Point(offset_x, offset_y));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="offset"></param>
        private void SyncScrollOffset(Point offset)
        {
            if (offset.X < 0 || offset.Y < 0) return;
            Dispatcher?.Invoke(() =>
            {
                if (offset.X >= 0 && ImageViewerScroll.HorizontalOffset != offset.X)
                {
                    ImageViewerScroll.ScrollToHorizontalOffset(offset.X);
                    //mouse_start.X = offset.X;
                }
                if (offset.Y >= 0 && ImageViewerScroll.VerticalOffset != offset.Y)
                {
                    ImageViewerScroll.ScrollToVerticalOffset(offset.Y);
                    //mouse_start.Y = offset.Y;
                }
#if DEBUG
                Debug.WriteLine($"Scroll : [{ImageViewerScroll.HorizontalOffset:F0}, {ImageViewerScroll.VerticalOffset:F0}], Offset : [{offset.X:F0}, {offset.Y:F0}]");
                //Debug.WriteLine($"Move Y: {offset_y}");
#endif
                UpdateBirdViewArea();
            });
        }

        /// <summary>
        ///
        /// </summary>
        private void CenterViewer()
        {
            Dispatcher?.Invoke(() =>
            {
                ImageViewerScroll.ScrollToVerticalOffset(ImageViewerScroll.ScrollableHeight / 2);
                ImageViewerScroll.ScrollToHorizontalOffset(ImageViewerScroll.ScrollableWidth / 2);
                DoEvents();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        private void FitView()
        {
            ImageViewerBox?.Dispatcher?.Invoke(() =>
            {
                if (ImageViewer?.Source != null)
                {
                    var iw = ImageViewer?.Source?.Width;
                    var ih = ImageViewer?.Source?.Height;

                    var vw = ImageViewerPanel?.ActualWidth;
                    var vh = ImageViewerPanel?.ActualHeight;

                    if (iw >= vw || ih >= vh)
                    {
                        var r_x = vw / iw;
                        var r_y = vh / ih;
                        ZoomRatio.Value = Math.Max(r_x ?? 1, r_y ?? 1);
                    }
                    else
                    {
                        ZoomRatio.Value = 1;
                    }
                }
            });
        }
        
        /// <summary>
        /// 
        /// </summary>
        private void FlipView()
        {
            if (IsImageNull(ImageViewer)) return;
            ImageViewerScale?.Dispatcher?.InvokeAsync(() => ImageViewerScale.ScaleY *= -1);
        }

        /// <summary>
        /// 
        /// </summary>
        private void FlopView()
        {
            if (IsImageNull(ImageViewer)) return;
            ImageViewerScale?.Dispatcher?.InvokeAsync(() => ImageViewerScale.ScaleX *= -1);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="angle"></param>
        private void RotateView(double  angle)
        {
            if (IsImageNull(ImageViewer)) return;
            ImageViewerScale?.Dispatcher?.InvokeAsync(() => ImageViewerRotate.Angle = (ImageViewerRotate.Angle + angle) % 360);
            CalcDisplay();
        }

        /// <summary>
        /// 
        /// </summary>
        private void ResetViewTransform(bool calcdisplay = true)
        {
            ImageViewerScale?.Dispatcher?.InvokeAsync(() =>
            {
                var scale = ZoomRatio.Value;
                ImageViewerRotate.Angle = 0;
                ImageViewerScale.ScaleX = scale;
                ImageViewerScale.ScaleY = scale;
                ZoomRatio.Value = scale;
                if (calcdisplay) CalcDisplay();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="e"></param>
        public void UpdateInfoBox(ImageInformation image = null, EventArgs e = null)
        {
            if (Ready)
            {
                Dispatcher?.InvokeAsync(async () =>
                {
                    ImageIndexBox.Text = "-/-";
                    if (image is null) image = ImageViewer.GetInformation();
                    if (e is RenamedEventArgs && (e as RenamedEventArgs).OldName.Equals(image.FileName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        image.RefreshImageFileInfo((e as RenamedEventArgs).Name);
                    }
                    ImageInfoBox.Text = await image.GetSimpleInfo();
                    ImageIndexBox.Text = await image.GetIndexInfo();
                });
            }
        }
        #endregion

        #region Quality Changer Helper
        private DispatcherTimer QualityChangerDelay = null;

        /// <summary>
        ///
        /// </summary>
        private void InitCoutDownTimer()
        {
            if (QualityChangerDelay == null)
            {
                QualityChangerDelay = new DispatcherTimer(DispatcherPriority.Normal) { IsEnabled = false, Interval = TimeSpan.FromMilliseconds(CountDownTimeOut) };
                QualityChangerDelay.Tick += QualityChangerDelay_Tick;
            }
        }

        private ImageInformation _quality_temp_ = null;

        /// <summary>
        ///
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void QualityChangerDelay_Tick(object sender, EventArgs e)
        {
            QualityChangerDelay.Stop();
            if (IsQualityChanger)
            {
                //var source = (ImageType)(QualityChanger.Tag);
                var quality = (uint)(QualityChangerSlider.Value);

                RenderRun(async () =>
                {
                    try
                    {
                        IsProcessingViewer = true;

                        var info = ImageViewer.GetInformation();
                        var image = new MagickImage(info.Current);
                        var quality_o = info.OriginalQuality;

                        var image_s = _quality_temp_ ?? new ImageInformation();

                        var result = quality < quality_o ? await ChangeQuality(image, quality) : new MagickImage(image);
                        image_s.Original = new MagickImage(result);
                        SetImageSource(ImageViewer, image_s, fit: false);
                        result.Dispose();

                        if (info.ValidCurrent)
                        {
                            var size = image_s.Current?.GetArtifact("filesize");
                            SetQualityChangerTitle(string.IsNullOrEmpty(size) ? null : $"{image_s.CurrentQuality}, {"ResultTipSize".T()} {size}");
                        }
                    }
                    catch (Exception ex) { ex.ShowMessage(); }
                    finally { IsProcessingViewer = false; }
                });
            }
        }

        private string QualityChangeerTitle = string.Empty;

        /// <summary>
        ///
        /// </summary>
        /// <param name="info"></param>
        private void OpenQualityChanger(ImageType source = ImageType.None)
        {
            if (Ready && !IsQualityChanger)
            {
                InitCoutDownTimer();
                QualityChanger?.Dispatcher?.InvokeAsync(() =>
                {
                    var info = ImageViewer.GetInformation();
                    if (info.ValidCurrent)
                    {
                        var image = info?.Current;
                        var quality = image.Quality();
                        var quality_str = quality > 0  ? $"{quality}" : "Unknown";
                        QualityChangeerTitle = $"{"InfoTipQuality".T().Trim('=').Trim()} : {quality_str}";
                        QualityChanger.Caption = QualityChangeerTitle;
                        QualityChanger.FocusedElement = QualityChangerSlider;
                        QualityChangerSlider.Maximum = quality > 0 ? quality : 100;
                        QualityChangerSlider.Width = 360;
                        QualityChangerSlider.IsSnapToTickEnabled = true;
                        QualityChangerSlider.Ticks = new DoubleCollection() { 10, 25, 30, 35, 55, 60, 65, 70, 75, 85, 95 };
                        QualityChangerSlider.TickPlacement = System.Windows.Controls.Primitives.TickPlacement.Both;
                        QualityChangerSlider.LargeChange = 5;
                        QualityChangerSlider.SmallChange = 1;
                        QualityChangerSlider.Value = quality > 0 ? quality : 100;
                        QualityChanger.FocusedElement = QualityChangerSlider;
                        QualityChanger.Show();
                        //QualityChanger.UpdateLayout();

                        DoEvents();

                        QualityChangerSlider.Focusable = true;
                        QualityChangerSlider.Focus();

                        if (QualityChangerDelay is DispatcherTimer)
                        {
                            QualityChangerDelay.IsEnabled = true;
                            QualityChangerDelay.Stop();
                        }
                    }
                });
            }
        }

        /// <summary>
        ///
        /// </summary>
        private void CloseQualityChanger(bool restore = false, ImageType source = ImageType.All)
        {
            if (Ready && IsQualityChanger)
            {
                QualityChanger?.Dispatcher?.InvokeAsync(() =>
                {
                    if (restore)
                        QualityChanger_CloseButtonClicked(QualityChanger, null);
                    else
                        QualityChangerSlider.Tag = null;

                    QualityChanger.Close();
                });
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="title"></param>
        private void SetQualityChangerTitle(string title = null)
        {
            QualityChanger?.Dispatcher?.InvokeAsync(() =>
            {
                title = title?.Trim();
                QualityChanger.Caption = string.IsNullOrEmpty(title) ? $"{QualityChangeerTitle}" : $"{QualityChangeerTitle} => {title}";
            });
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="diff"></param>
        private void UpdateQualityChangerTitle(string diff = null)
        {
            if (IsQualityChanger && !string.IsNullOrEmpty(diff))
            {
                QualityChanger?.Dispatcher?.InvokeAsync(() =>
                {
                    if (Regex.IsMatch(QualityChanger.Caption, $"{"ResultTipDifference".T()}", RegexOptions.IgnoreCase))
                    {
                        QualityChanger.Caption = Regex.Replace(QualityChanger.Caption, $"{"ResultTipDifference".T()}.*?$", $"{"ResultTipDifference".T()} {diff}", RegexOptions.IgnoreCase);
                    }
                });
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="diff"></param>
        private void UpdateQualityChangerTitle(double? diff = null)
        {
            if (IsQualityChanger && diff != null && diff != double.NaN)
            {
                QualityChanger?.Dispatcher?.InvokeAsync(() =>
                {
                    if (Regex.IsMatch(QualityChanger.Caption, $"{"ResultTipDifference".T()}", RegexOptions.IgnoreCase))
                    {
                        QualityChanger.Caption = Regex.Replace(QualityChanger.Caption, $"{"ResultTipDifference".T()}.*?$", $"{"ResultTipDifference".T()} {diff:P2}", RegexOptions.IgnoreCase);
                    }
                });
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private ImageType GetQualityChangerSource()
        {
            var result = ImageType.None;
            result = QualityChanger?.Dispatcher?.Invoke(() =>
            {
                var source = ImageType.None;
                if (IsQualityChanger && QualityChanger.Tag is ImageType && QualityChangerSlider.Tag is MagickImage)
                {
                    source = (ImageType)(QualityChanger.Tag ?? ImageType.None);
                }
                return (source);
            }) ?? ImageType.None;
            return (result);
        }
        #endregion

        #region Bird View Helper

        private void ShowBirdView()
        {
            BirdViewPanel.Dispatcher.Invoke(() => BirdViewPanel.Visibility = Visibility.Visible);
        }

        private void HideBirdView()
        {
            BirdViewPanel.Dispatcher.Invoke(() => BirdViewPanel.Visibility = Visibility.Hidden);
        }

        private void SetBirdView(bool state)
        {
            if (state) ShowBirdView();
            else HideBirdView();
        }

        private void UpdateBirdViewArea()
        {
            BirdViewPanel.Dispatcher.InvokeAsync(() =>
            {
                var src = ImageViewer;
                var scroll = ImageViewerScroll;
                var ratio = Math.Min(250f / (src.DesiredSize.Width), 250f / (src.DesiredSize.Height));

                var aw = (src.DesiredSize.Width < scroll.ViewportWidth ? src.DesiredSize.Width : scroll.ViewportWidth) * ratio;
                var ah = (src.DesiredSize.Height < scroll.ViewportHeight ? src.DesiredSize.Height : scroll.ViewportHeight) * ratio;
                var tx = scroll.HorizontalOffset * ratio;
                var ty = scroll.VerticalOffset * ratio;
                BirdViewArea.Width = aw;
                BirdViewArea.Height = ah;
                Canvas.SetLeft(BirdViewArea, tx);
                Canvas.SetTop(BirdViewArea, ty);
            });
        }

        private void UpdateBirdView()
        {
            BirdViewPanel.Dispatcher.InvokeAsync(() => 
            {
                if (ImageViewer.Source == null) BirdView.Source = null;
                else
                {
                    try
                    {
                        var src = ImageViewer;
                        var iw = src.DesiredSize.Width;
                        var ih = src.DesiredSize.Height;
                        var ratio = Math.Min(250f / iw, 250f / ih);
                        var tw = iw * ratio;
                        var th = ih * ratio;
                        BirdViewPanel.Width = tw;
                        BirdViewPanel.Height = th;
                        BirdViewBorder.Width = tw;
                        BirdViewBorder.Height = th;
                        BirdView.Source = ImageViewer.ToBitmapSource(new Size(tw, th));
                        
                        UpdateBirdViewArea();
                    }
                    catch (Exception ex) { ex.ShowMessage(); }
                }
            });
        }

        private Point CalcBirdViewOffset(FrameworkElement sender, MouseEventArgs e)
        {
            double offset_x = -1, offset_y = -1;
            if (sender == BirdView || sender == BirdViewCanvas || sender == BirdViewArea)
            {
                if (BirdView.Source != null)
                {
                    try
                    {
                        var src = ImageViewer;
                        var ratio = Math.Max(src.DesiredSize.Width / 250f, src.DesiredSize.Height / 250f);
                        var pos = e.GetPosition(BirdView);
                        var aw = BirdViewArea.DesiredSize.Width;
                        var ah = BirdViewArea.DesiredSize.Height;
                        offset_x = Math.Max(0, (pos.X - aw / 2f) * ratio);
                        offset_y = Math.Max(0, (pos.Y - ah / 2f) * ratio);
                    }
                    catch { }
                }
            }
            return (new Point(offset_x, offset_y));
        }
        #endregion

        #region ToolTip Helper
        /// <summary>
        ///
        /// </summary>
        /// <param name="change_state"></param>
        private void ToggleMagnifier(bool? state = null, bool change_state = false)
        {
            ImageMagnifier?.Dispatcher?.Invoke(() =>
            {
                if (ImageViewer.Source == null) return;

                var mag = ImageMagnifier.IsEnabled;
                if (state == null) { mag = !mag; }
                else mag = state ?? false;

                if (change_state) MagnifierMode.IsChecked = mag;
                ToolTipService.SetIsEnabled(ImageViewer, !mag);
                if (mag)
                {
                    CloseToolTip(ImageViewer);
                }
                ImageMagnifier.IsEnabled = mag;
                ImageMagnifier.Visibility = mag ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        private bool IsToolTipOpen(FrameworkElement element)
        {
            var result = false;
            try
            {
                if (element?.ToolTip is string)
                {
                    result = Dispatcher?.Invoke(() => (element?.ToolTip as ToolTip).IsOpen) ?? false;
                }
                else if (element?.ToolTip is ToolTip && (element?.ToolTip as ToolTip).Content is string)
                {
                    result = Dispatcher?.Invoke(() => (element?.ToolTip as ToolTip).IsOpen) ?? false;
                }
            }
            catch { }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        private string GetToolTip(FrameworkElement element)
        {
            var result = Dispatcher?.Invoke(() =>
            {
                var ret = string.Empty;
                if (element?.ToolTip is string)
                {
                    ret = element?.ToolTip as string;
                }
                else if (element?.ToolTip is ToolTip && (element?.ToolTip as ToolTip).Content is string)
                {
                    ret = (element?.ToolTip as ToolTip).Content as string;
                }
                return(ret);
            });
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        /// <param name="tooltip"></param>
        private void SetToolTip(FrameworkElement element, string tooltip)
        {
            Dispatcher?.Invoke(() =>
            {
                if (element?.ToolTip is string)
                {
                    element.ToolTip = tooltip;
                }
                else if (element?.ToolTip is ToolTip)
                {
                    (element.ToolTip as ToolTip).Content = tooltip;
                }
                else if (element?.ToolTip is null && !string.IsNullOrEmpty(tooltip))
                {
                    element.ToolTip = new ToolTip() { Content = tooltip };
                }
                else if (string.IsNullOrEmpty(tooltip))
                {
                    element.ToolTip = null;
                }
                SetToolTipState(ImageViewer, ShowImageInfo.IsChecked ?? false);

                DoEvents();
                ToolTipService.SetShowDuration(element, AutoHideToolTip ?? false ? ToolTipDuration : int.MaxValue);
            });
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        private void OpenToolTip(FrameworkElement element)
        {
            element?.Dispatcher?.InvokeAsync(() =>
            {
                try
                {
                    if (element?.ToolTip is string)
                    {
                        (element?.ToolTip as ToolTip).IsOpen = true;
                    }
                    else if (element?.ToolTip is ToolTip && (element?.ToolTip as ToolTip).Content is string)
                    {
                        (element?.ToolTip as ToolTip).IsOpen = true;
                    }
                }
                catch { }
            });
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        private void CloseToolTip(FrameworkElement element)
        {
            element?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    if (element?.ToolTip is string)
                    {
                        (element?.ToolTip as ToolTip).IsOpen = false;
                    }
                    else if (element?.ToolTip is ToolTip && (element?.ToolTip as ToolTip).Content is string)
                    {
                        (element?.ToolTip as ToolTip).IsOpen = false;
                    }
                }
                catch { }
            });
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        private void ToggleToolTip(FrameworkElement element)
        {
            var show = !IsToolTipOpen(element);
            if (show) OpenToolTip(element);
            else CloseToolTip(element);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        private void ShowToolTip(FrameworkElement element)
        {
            element?.Dispatcher?.InvokeAsync(() =>
            {
                if (element?.ToolTip is string)
                {
                    (element?.ToolTip as ToolTip).Visibility = Visibility.Visible;
                }
                else if (element?.ToolTip is ToolTip && (element?.ToolTip as ToolTip).Content is string)
                {
                    (element?.ToolTip as ToolTip).Visibility = Visibility.Visible;
                }
            });
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        private void HideToolTip(FrameworkElement element)
        {
            element?.Dispatcher?.InvokeAsync(() =>
            {
                if (element?.ToolTip is string)
                {
                    (element?.ToolTip as ToolTip).Visibility = Visibility.Collapsed;
                }
                else if (element?.ToolTip is ToolTip && (element?.ToolTip as ToolTip).Content is string)
                {
                    (element?.ToolTip as ToolTip).Visibility = Visibility.Collapsed;
                }
            });
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="element"></param>
        /// <param name="state"></param>
        private void SetToolTipState(FrameworkElement element, bool state)
        {
            if (state) ShowToolTip(element);
            else HideToolTip(element);
        }

        /// <summary>
        ///
        /// </summary>
        private void ToggleToolTipState()
        {
            ShowImageInfo?.Dispatcher?.InvokeAsync(() =>
            {
                try
                {
                    var info = ShowImageInfo.IsChecked ?? false;
                    if (!info)
                    {
                        CloseToolTip(ImageInfoBox);
                    }
                    SetToolTipState(ImageInfoBox, info);
                }
                catch { }
            });
        }
        #endregion

        #region Set Busy Cursor
        /// <summary>
        ///
        /// </summary>
        /// <param name="cursor"></param>
        private void SetCursor(Cursor cursor)
        {
            try
            {
                Dispatcher?.Invoke(() =>
                {
                    Cursor = cursor;
                    DoEvents();
                });
            }
            catch { }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private Cursor SetBusy()
        {
            var result = Cursor;
            SetCursor(Cursors.Wait);
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private Cursor SetIdle()
        {
            var result = Cursor;
            SetCursor(Cursors.Arrow);
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="text"></param>
        private void SetTitle(string text = null)
        {
            Dispatcher?.InvokeAsync(() =>
            {
                if (string.IsNullOrEmpty(text))
                {
                    Title = DefaultWindowTitle;
                }
                else
                {
                    Title = $"{DefaultWindowTitle} - {text}";
                }
            });
        }
        #endregion

        #region Config Load/Save Helper
        /// <summary>
        ///
        /// </summary>
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

                if (appSection.Settings.AllKeys.Contains("TaskTimeOutSeconds"))
                {
                    var value = TaskTimeOutSeconds;
                    if (double.TryParse(appSection.Settings["TaskTimeOutSeconds"].Value, out value)) TaskTimeOutSeconds = value;
                }
                if (appSection.Settings.AllKeys.Contains("CountDownTimeOut"))
                {
                    var value = CountDownTimeOut;
                    if (double.TryParse(appSection.Settings["CountDownTimeOut"].Value, out value)) CountDownTimeOut = value;
                }

                if (appSection.Settings.AllKeys.Contains("AutoHideToolTip"))
                {
                    var value = AutoHideToolTip ?? true;
                    if (bool.TryParse(appSection.Settings["AutoHideToolTip"].Value, out value)) AutoHideToolTip = value;
                }
                if (appSection.Settings.AllKeys.Contains("ToolTipDuration"))
                {
                    var value = ToolTipDuration;
                    if (int.TryParse(appSection.Settings["ToolTipDuration"].Value, out value)) ToolTipDuration = value;
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

                if (appSection.Settings.AllKeys.Contains("ImageMagnifierZoomFactor"))
                {
                    var value = ImageMagnifierZoomFactor;
                    if (double.TryParse(appSection.Settings["ImageMagnifierZoomFactor"].Value, out value)) ImageMagnifierZoomFactor = value;
                }
                if (appSection.Settings.AllKeys.Contains("ImageMagnifierRadius"))
                {
                    var value = ImageMagnifierRadius;
                    if (double.TryParse(appSection.Settings["ImageMagnifierRadius"].Value, out value)) ImageMagnifierRadius = value;
                }
                if (appSection.Settings.AllKeys.Contains("ImageMagnifierBorderBrush"))
                {
                    try
                    {
                        ImageMagnifierBorderBrush = (Color)ColorConverter.ConvertFromString(appSection.Settings["ImageMagnifierBorderBrush"].Value);
                    }
                    catch { }
                }
                if (appSection.Settings.AllKeys.Contains("ImageMagnifierBorderThickness"))
                {
                    var value = ImageMagnifierBorderThickness;
                    if (double.TryParse(appSection.Settings["ImageMagnifierBorderThickness"].Value, out value)) ImageMagnifierBorderThickness = value;
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
                    if (uint.TryParse(appSection.Settings["MaxCompareSize"].Value, out value)) MaxCompareSize = value;
                }

                if (appSection.Settings.AllKeys.Contains("SimpleTrimCropBoundingBox"))
                {
                    var value = SimpleTrimCropBoundingBox;
                    if (bool.TryParse(appSection.Settings["SimpleTrimCropBoundingBox"].Value, out value)) SimpleTrimCropBoundingBox = value;
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="force"></param>
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
                    if (appSection.Settings.AllKeys.Contains("TaskTimeOutSeconds"))
                        appSection.Settings["TaskTimeOutSeconds"].Value = TaskTimeOutSeconds.ToString();
                    else
                        appSection.Settings.Add("TaskTimeOutSeconds", TaskTimeOutSeconds.ToString());

                    if (appSection.Settings.AllKeys.Contains("CountDownTimeOut"))
                        appSection.Settings["CountDownTimeOut"].Value = CountDownTimeOut.ToString();
                    else
                        appSection.Settings.Add("CountDownTimeOut", CountDownTimeOut.ToString());

                    if (appSection.Settings.AllKeys.Contains("AutoHideToolTip"))
                        appSection.Settings["AutoHideToolTip"].Value = AutoHideToolTip.ToString();
                    else
                        appSection.Settings.Add("AutoHideToolTip", AutoHideToolTip.ToString());

                    if (appSection.Settings.AllKeys.Contains("ImageMagnifierZoomFactor"))
                        appSection.Settings["ImageMagnifierZoomFactor"].Value = ImageMagnifierZoomFactor.ToString();
                    else
                        appSection.Settings.Add("ImageMagnifierZoomFactor", ImageMagnifierZoomFactor.ToString());

                    if (appSection.Settings.AllKeys.Contains("ImageMagnifierRadius"))
                        appSection.Settings["ImageMagnifierRadius"].Value = ImageMagnifierRadius.ToString();
                    else
                        appSection.Settings.Add("ImageMagnifierRadius", ImageMagnifierRadius.ToString());

                    if (appSection.Settings.AllKeys.Contains("ImageMagnifierBorderBrush"))
                        appSection.Settings["ImageMagnifierBorderBrush"].Value = ImageMagnifierBorderBrush.ToString();
                    else
                        appSection.Settings.Add("ImageMagnifierBorderBrush", ImageMagnifierBorderBrush.ToString());

                    if (appSection.Settings.AllKeys.Contains("ImageMagnifierBorderThickness"))
                        appSection.Settings["ImageMagnifierBorderThickness"].Value = ImageMagnifierBorderThickness.ToString();
                    else
                        appSection.Settings.Add("ImageMagnifierBorderThickness", ImageMagnifierBorderThickness.ToString());

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

                    if (appSection.Settings.AllKeys.Contains("MasklightColor"))
                        appSection.Settings["MasklightColor"].Value = MasklightColor == null ? string.Empty : MasklightColor.ToHexString();
                    else
                        appSection.Settings.Add("MasklightColor", MasklightColor == null ? string.Empty : MasklightColor.ToHexString());

                    if (appSection.Settings.AllKeys.Contains("MasklightColorRecents"))
                        appSection.Settings["MasklightColorRecents"].Value = string.Join(", ", GetRecentHexColors(MasklightColorPick));
                    else
                        appSection.Settings.Add("MasklightColorRecents", string.Join(", ", GetRecentHexColors(MasklightColorPick)));

                    if (appSection.Settings.AllKeys.Contains("GrayscaleMode"))
                        appSection.Settings["GrayscaleMode"].Value = GrayscaleMode.ToString();
                    else
                        appSection.Settings.Add("GrayscaleMode", GrayscaleMode.ToString());

                    if (appSection.Settings.AllKeys.Contains("ZoomFitMode"))
                        appSection.Settings["ZoomFitMode"].Value = CurrentZoomFitMode.ToString();
                    else
                        appSection.Settings.Add("ZoomFitMode", CurrentZoomFitMode.ToString());

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

            InitCoutDownTimer();

            InitMagickNet();

            #region Some Default UI Settings
            Icon = new BitmapImage(new Uri("pack://application:,,,/ImageViewer;component/Resources/Image.ico"));
            ToolTipService.SetShowOnDisabled(ImageViewer, false);
            UILanguage.ContextMenu.PlacementTarget = UILanguage;
            ShowImageInfo.IsChecked = false;
            BusyNow.Opacity = 0.66;
            //IndicatorViewer.DisplayAfter = TimeSpan.FromMilliseconds(50);
            HideBirdView();
            ChangeTheme();
            QualityChangerSlider.MouseWheel += Slider_MouseWheel;
            //ImageViewerScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            //ImageViewerScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            #endregion

            #region Default Zoom Ratio
            ZoomMin = ZoomRatio.Minimum;
            ZoomMax = ZoomRatio.Maximum;
            ZoomRatio.Value = 1.0;
            ZoomRatio.MouseWheel += Slider_MouseWheel;
            #endregion

            LocaleUI(DefaultCultureInfo);

            #region Create Grayscale Mode Selector
            cm_grayscale_mode = new ContextMenu() { IsTextSearchEnabled = true, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom, PlacementTarget = GrayMode };
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
            GrayMode.Click += (obj, evt) => { cm_grayscale_mode.IsOpen = true; };
            #endregion

            #region Result Color Defaults Value
            if (MasklightColor != null)
            {
                var cm = MasklightColor.ToByteArray();
                MasklightColorPick.SelectedColor = Color.FromArgb(cm[3], cm[0], cm[1], cm[2]);
            }
            #endregion

            #region Magnifier Init
            ImageMagnifier.IsEnabled = false;
            ImageMagnifier.IsUsingZoomOnMouseWheel = false;
            //ImageMagnifier.FrameType = FrameType.Rectangle;
            ImageMagnifier.Visibility = Visibility.Collapsed;
            ImageMagnifier.Radius = ImageMagnifierRadius;
            ImageMagnifier.BorderBrush = new SolidColorBrush(ImageMagnifierBorderBrush);
            ImageMagnifier.BorderThickness = new Thickness(ImageMagnifierBorderThickness);
            ImageMagnifier.ZoomFactor = ImageMagnifierZoomFactor;
            #endregion

            SyncColorLighting();
            DoEvents();

            var opts = this.GetCmdLineOpts();
            var args = opts.Args.ToArray();
            if (args.Length > 0) Dispatcher?.InvokeAsync(async () => await LoadImageFromFiles(args));
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            this.ReleaseWatcher();

            LastWinState = WindowState == System.Windows.WindowState.Maximized ? System.Windows.WindowState.Maximized : System.Windows.WindowState.Normal;
            if (WindowState == System.Windows.WindowState.Normal)
                LastPositionSize = new Rect(Left, Top, Width, Height);
            SaveConfig();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (!Ready) return;
            if (WindowState == System.Windows.WindowState.Normal)
                LastPositionSize = new Rect(Left, Top, Width, Height);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Normal)
                LastPositionSize = new Rect(Left, Top, e.NewSize.Width, e.NewSize.Height);
            else
                LastPositionSize = new Rect(Left, Top, LastPositionSize.Width, LastPositionSize.Height);
            if (Ready) CalcDisplay(size: e.NewSize);
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (!Ready) return;
            LastWinState = WindowState;
            LastPositionSize = new Rect(Left, Top, LastPositionSize.Width, LastPositionSize.Height);
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (!Ready) return;
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
            if (!Ready) return;
            var fmts = e.Data.GetFormats(true);
            if (e.Data.GetDataPresent("FileDrop"))
            {
                var files = e.Data.GetData("FileDrop");
                if (files is IEnumerable<string>)
                {
                    if (sender == ImageLoadHaldLut)
                        LoadHaldLutFile((files as IEnumerable<string>).Where(f => File.Exists(f)).First());
                    else
                        Dispatcher?.InvokeAsync(async () => await LoadImageFromFiles((files as IEnumerable<string>).ToArray()));
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
                        Dispatcher?.InvokeAsync(async () => await LoadImageFromFiles(files as IEnumerable<string>));
                }
            }
            e.Handled = true;
        }

        private Key _last_key_ = Key.None;
        private DateTime _last_key_time_ = DateTime.Now;
        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!Ready) return;
            if (e.IsDown)
            {
                try
                {
                    e.Handled = true;
                    var km = this.GetModifier();
                    if (km.OnlyCtrl && (e.Key == Key.W || e.SystemKey == Key.W))
                    {
                        Close();
                    }
                    else if (km.OnlyAlt && (e.Key == Key.T || e.SystemKey == Key.T))
                    {
                        if (ImageViewerScroll.ContextMenu != null) ImageViewerScroll.ContextMenu.IsOpen = true;
                    }
                    else if (e.Key == Key.Escape || e.SystemKey == Key.Escape)
                    {
                        if (IsMagnifier)
                        {
                            ToggleMagnifier(state: false, change_state: true);
                        }
                        else if (IsQualityChanger)
                        {
                            CloseQualityChanger(restore: true);
                        }
                        else if (_last_key_ == Key.Escape && (DateTime.Now - _last_key_time_).TotalMilliseconds < 200)
                        {
                            Close();
                        }
                    }
                    else if (e.Key == Key.F1 || e.SystemKey == Key.F1)
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                            await LoadImageFromPrevFile();
                        else if (Keyboard.Modifiers == ModifierKeys.Control)
                            await LoadImageFromNextFile();
                        else if (Keyboard.Modifiers == ModifierKeys.Alt)
                            CreateColorImage(true);
                        else
                            ImageActions_Click(ImageOpen, e);
                    }
                    else if (e.Key == Key.F3 || e.SystemKey == Key.F3)
                    {
                        ImageActions_Click(ImagePaste, e);
                    }
                    else if (e.Key == Key.F9 || e.SystemKey == Key.F9)
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            if      (CurrentZoomFitMode == ZoomFitMode.Height) CurrentZoomFitMode = ZoomFitMode.Width;
                            else if (CurrentZoomFitMode == ZoomFitMode.Width) CurrentZoomFitMode = ZoomFitMode.All;
                            else if (CurrentZoomFitMode == ZoomFitMode.All) CurrentZoomFitMode = ZoomFitMode.None;
                            else if (CurrentZoomFitMode == ZoomFitMode.None) CurrentZoomFitMode = ZoomFitMode.Height;
                        }
                        else
                        {
                            if (CurrentZoomFitMode == ZoomFitMode.None) CurrentZoomFitMode = ZoomFitMode.All;
                            else if (CurrentZoomFitMode == ZoomFitMode.All) CurrentZoomFitMode = ZoomFitMode.Width;
                            else if (CurrentZoomFitMode == ZoomFitMode.Width) CurrentZoomFitMode = ZoomFitMode.Height;
                            else if (CurrentZoomFitMode == ZoomFitMode.Height) CurrentZoomFitMode = ZoomFitMode.None;
                        }
                    }
                    else if (km.OnlyCtrl && (e.Key == Key.C || e.SystemKey == Key.C))
                    {
                        await CopyImageToClipboard();
                    }
                    else if (km.OnlyCtrl && (e.Key == Key.V || e.SystemKey == Key.V))
                    {
                        await LoadImageFromClipboard();
                    }
                    else if (e.Key == Key.I || e.SystemKey == Key.I)
                    {
                        ToggleToolTip(ImageInfoBox);
                    }
                    else if (e.Key == Key.M || e.SystemKey == Key.M)
                    {
                        ToggleMagnifier(change_state: true);
                    }
                    else if (e.Key == Key.R || e.SystemKey == Key.R)
                    {
                        if (Keyboard.Modifiers == ModifierKeys.Shift)
                        {
                            ResetImage(true);
                        }
                        else if (Keyboard.Modifiers == ModifierKeys.Control)
                        {
                            ReloadImage(true);
                        }
                        else if (Keyboard.Modifiers == ModifierKeys.Alt)
                        {
                            ReloadImage(true, info_only: true);
                        }
                        else RenderRun(LastAction);
                    }
                    else if (e.Key == Key.Q || e.SystemKey == Key.Q)
                    {
                        OpenQualityChanger(ImageType.Source);
                    }

                    else if (e.Key == Key.Home || e.SystemKey == Key.Home)
                    {
                        if (!IsQualityChanger) await LoadImageFromFirstFile();
                    }
                    else if (e.Key == Key.Left || e.SystemKey == Key.Left)
                    {
                        if (!IsQualityChanger) await LoadImageFromPrevFile();
                    }
                    else if (e.Key == Key.Right || e.SystemKey == Key.Right)
                    {
                        if (!IsQualityChanger) await LoadImageFromNextFile();
                    }
                    else if (e.Key == Key.End || e.SystemKey == Key.End)
                    {
                        if (!IsQualityChanger) await LoadImageFromLastFile();
                    }

                    else if (e.Key == Key.Add || e.SystemKey == Key.Add || e.Key == Key.OemPlus || e.SystemKey == Key.OemPlus)
                    {
                        CurrentZoomFitMode = ZoomFitMode.None;
                        ZoomRatio.Value += ZoomRatio.SmallChange;
                    }
                    else if (e.Key == Key.Subtract || e.SystemKey == Key.Subtract || e.Key == Key.OemMinus || e.Key == Key.OemMinus)
                    {
                        CurrentZoomFitMode = ZoomFitMode.None;
                        ZoomRatio.Value -= ZoomRatio.SmallChange;
                    }
                    else if (e.Key == Key.Multiply || e.Key == Key.Multiply)
                    {
                        CurrentZoomFitMode = ZoomFitMode.None;
                        ZoomRatio.Value = 1f;
                    }
                    else if (e.Key == Key.Divide || e.Key == Key.Divide)
                    {
                        CurrentZoomFitMode = ZoomFitMode.All;
                    }
                    
                    else e.Handled = false;
                    _last_key_ = e.Key;
                    _last_key_time_ = DateTime.Now;
                    //Debug.WriteLine(e.Key);
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
            if (!Ready) return;
            if (e.ChangedButton == MouseButton.Middle && e.XButton1 == MouseButtonState.Pressed)
            {
                e.Handled = true;
                if (e.ClickCount >= 1) CenterViewer();
            }
            else if (e.ChangedButton == MouseButton.Middle && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                Close();
            }
            else if (e.ChangedButton == MouseButton.XButton1 && e.ClickCount >= 2)
            {
                e.Handled = true;
                var action = await LoadImageFromNextFile();
                if (action) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true));
            }
            else if (e.ChangedButton == MouseButton.XButton2 && e.ClickCount >= 2)
            {
                e.Handled = true;
                var action = await LoadImageFromPrevFile();
                if (action) RenderRun(() => UpdateImageViewer(compose: LastOpIsComposite, assign: true));
            }
            //else ImageBox_MouseDown(sender, e);
        }
        #endregion

        #region ImageBox Events
        private async void ImageBox_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!Ready || IsImageNull(ImageViewer) || e.Delta == 0) return;

            if (CurrentZoomFitMode == ZoomFitMode.None && Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
                var zoom_old = ZoomRatio.Value;
                var zoom_new = zoom_old + (e.Delta < 0 ? -0.033 : 0.033);
                ZoomRatio.Value = zoom_new;

                if (sender is Viewbox || sender is ScrollViewer)
                {
                    SyncScrollOffset(GetScrollOffset(sender as FrameworkElement));
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                if (e.Delta > 0) await LoadImageFromPrevFile();
                else if (e.Delta < 0) await LoadImageFromNextFile();
            }
        }

        private long _last_timestamp_ = 0;
        private void ImageBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!Ready) return;
            e.Handled = false;
            try
            {
                if (e.Device is MouseDevice)
                {
                    if (e.ChangedButton == MouseButton.Left && e.XButton1 == MouseButtonState.Pressed)
                    {
                        e.Handled = true;
                        if (e.ClickCount == 1)
                        {
                            if (CurrentZoomFitMode == ZoomFitMode.None)
                            {
                                CurrentZoomFitMode = ZoomFitMode.All;
                            }
                            else if (CurrentZoomFitMode == ZoomFitMode.All)
                            {
                                ZoomRatio.Value = 1f;
                                CurrentZoomFitMode = ZoomFitMode.None;
                            }
                        }
                    }
                    else if (e.ChangedButton == MouseButton.Left && e.ClickCount >= 2)
                    {
                        e.Handled = true;
                        ToggleMagnifier(change_state: true);
                    }
                    else if (e.ChangedButton == MouseButton.Left)
                    {
                        if (sender == ImageViewerBox || sender == ImageViewerScroll)
                        {
                            e.Handled = true;
                            var pos = e.GetPosition(ImageViewerScroll);
                            ImageViewer.GetInformation().LastClickPos = new PointD(pos.X, pos.Y);
                        }
                    }
                    _last_timestamp_ = e.Timestamp;
                }
            }
            catch (Exception ex) { ex.ShowMessage("MouseClick"); }
        }

        private void ImageBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (!Ready) return;
            try
            {
                if (e.XButton1 == MouseButtonState.Pressed)
                {
                    if (CurrentZoomFitMode == ZoomFitMode.None)
                    {
                        e.Handled = true;
                        var pos = e.GetPosition(ImageViewerScroll);
                        if (_last_viewer_pos_ is null) _last_viewer_pos_ = pos;
                        else
                        {
                            var dx = pos.X - _last_viewer_pos_.Value.X;
                            if(dx != 0)
                            {
                                var zoom_old = ZoomRatio.Value;
                                var zoom_new = zoom_old + (dx < 0 ? -0.033 : 0.033);
                                ZoomRatio.Value = zoom_new;
                            }
                        }
                        _last_viewer_pos_ = pos;
                    }
                }
                else if (e.LeftButton == MouseButtonState.Pressed)
                {
                    //e.Handled = true;
                    var offset = sender is Viewbox || sender is ScrollViewer ? CalcScrollOffset(sender as FrameworkElement, e) : new Point(-1, -1);
#if DEBUG
                    Debug.WriteLine($"Original : [{mouse_origin.X:F0}, {mouse_origin.Y:F0}], Start : [{mouse_start.X:F0}, {mouse_start.Y:F0}] => Move : [{offset.X:F0}, {offset.Y:F0}]");
                    //Debug.WriteLine($"Move Y: {offset_y}");
#endif
                    SyncScrollOffset(offset);
                }
                else
                {
                    mouse_start = e.GetPosition(ImageViewerScroll);
                    mouse_origin = new Point(ImageViewerScroll.HorizontalOffset, ImageViewerScroll.VerticalOffset);
                }
            }
            catch (Exception ex) { ex.ShowMessage("MouseMove"); }
        }

        private void ImageBox_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!Ready) return;
            try
            {
                //if (e.LeftButton == MouseButtonState.Pressed)
                //{
                //    if (sender == ImageViewerBox || sender == ImageViewerScroll)
                //    {
                //        if (ImageViewer.GetInformation().ValidCurrent)
                //        {
                //            e.Handled = true;
                //            mouse_start = e.GetPosition(ImageViewerScroll);
                //            mouse_origin = new Point(ImageViewerScroll.HorizontalOffset, ImageViewerScroll.VerticalOffset);
                //        }
                //    }
                //}
            }
            catch (Exception ex) { ex.ShowMessage("MouseEnter"); }
        }

        private void ImageBox_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!Ready) return;
            try
            {
                //if (e.LeftButton == MouseButtonState.Pressed)
                //{
                //    e.Handled = true;
                //    var offset = sender is Viewbox || sender is ScrollViewer ? CalcScrollOffset(sender as FrameworkElement, e) : new Point(-1, -1);
                //}
            }
            catch (Exception ex) { ex.ShowMessage("MouseLeave"); }
        }
        #endregion

        #region Birds Eye View
        private void BirdView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!Ready) return;
        }

        private void BirdView_MouseMove(object sender, MouseEventArgs e)
        {
            if (!Ready) return;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                e.Handled = true;
                var offset = CalcBirdViewOffset(sender as FrameworkElement, e);
                SyncScrollOffset(offset);
            }
            else
            {
                mouse_start_birdseye = e.GetPosition(BirdView);
                mouse_origin_birdseye = new Point(ImageViewerScroll.HorizontalOffset, ImageViewerScroll.VerticalOffset);
            }
        }

        private void BirdView_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!Ready) return;
            //if (e.LeftButton == MouseButtonState.Pressed)
            //{
            //    if (sender == BirdView || sender == BirdViewCanvas || sender == BirdViewArea)
            //    {
            //        e.Handled = true;
            //        mouse_start_birdseye = e.GetPosition(BirdView);
            //        mouse_origin_birdseye = new Point(ImageViewerScroll.HorizontalOffset, ImageViewerScroll.VerticalOffset);
            //    }
            //}
        }

        private void BirdView_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!Ready) return;
            //try
            //{
            //    if (e.LeftButton == MouseButtonState.Pressed)
            //    {
            //        e.Handled = true;
            //        var offset = CalcBirdViewOffset(sender as FrameworkElement, e);
            //        //SyncScrollOffset(offset);
            //    }
            //}
            //catch (Exception ex) { ex.ShowMessage("MouseLeave"); }
        }
        #endregion

        #region Image & ContextMenu Events
        private async void Image_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (Ready && e.Source is FrameworkElement)
            {
                try
                {
                    Image image = GetImageControl(e.Source as FrameworkElement);
                    if (image is Image && image.Source != null)
                    {
                        var tooltip = GetToolTip(image);
                        if (string.IsNullOrEmpty(tooltip) || tooltip.StartsWith(WaitingString, StringComparison.CurrentCultureIgnoreCase))
                        {
                            await UpdateImageTooltip();
                        }
                    }
                    else SetToolTipState(e.Source as FrameworkElement, false);
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
        }

        private void ImageActions_Click(object sender, RoutedEventArgs e)
        {
            if (!Ready) return;

            e.Handled = true;
            if (sender == UILanguage)
            {
                if (UILanguage.ContextMenu is ContextMenu) { UILanguage.ContextMenu.IsOpen = true; }
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

            else if (sender == AlwaysOnTop)
            {
                TopMostWindow(AlwaysOnTop.IsChecked);
            }
            else if (sender == DarkBackground)
            {
                ChangeTheme();
            }
            else if (sender == AutoSaveOptions)
            {
                SaveConfig(force: true);
            }

            else if (sender == ImageOpen)
            {
                RenderRun(async () => await LoadImageFromFile());
            }
            else if (sender == CreateImageWithColorSource)
            {
                RenderRun(new Action(() =>
                {
                    IsLoadingViewer = true;
                    CreateColorImage(true);
                }));
            }

            else if (sender == ImagePaste)
            {
                RenderRun(async () => await LoadImageFromClipboard());
            }

            else if (sender == RepeatLastAction)
            {
                RenderRun(LastAction);
            }

            else if (sender == MagnifierMode)
            {
                ToggleMagnifier();
            }
            else if (sender == ShowImageInfo)
            {
                ToggleToolTipState();
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

            else if (sender == ImageLoadHaldLut)
            {
                LoadHaldLutFile();
            }
        }
        #endregion

        #region Misc UI Control Events
        private void Slider_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!Ready) return;
            if (sender is Slider)
            {
                var slider = sender as Slider;
                slider?.Dispatcher?.Invoke(() =>
                {
                    if (e.Delta < 0) slider.Value -= slider.SmallChange;
                    if (e.Delta > 0) slider.Value += slider.SmallChange;
                });
            }
        }

        private void ZoomRatio_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!Ready) return;
            try
            {
                using (var d = Dispatcher?.DisableProcessing())
                {
                    if (Ready && CurrentZoomFitMode == ZoomFitMode.None && LastZoomRatio != e.NewValue)
                    {
                        e.Handled = true;
                        var zoom_old = LastZoomRatio;
                        var zoom_new = e.NewValue;
                        zoom_new = Math.Min(ZoomMax, Math.Max(ZoomMin, zoom_new));
                        if (zoom_new - zoom_old <= ZoomRatio.SmallChange && ((zoom_new < 1 && zoom_old > 1) || (zoom_new > 1 && zoom_old < 1)))
                            zoom_new = 1f;

                        ZoomRatio.ToolTip = $"{"Zoom Ratio".T(DefaultCultureInfo)}: {zoom_new:F2}X";
                        LastZoomRatio = zoom_new;

                        if (!IsImageNull(ImageViewer))
                        {
                            var zw = zoom_new * ImageViewer.Source.Width;
                            var zh = zoom_new * ImageViewer.Source.Height;
                            ImageViewerBox.MaxWidth = zw <= ImageViewerScroll.ActualWidth ? ImageViewerScroll.ActualWidth : zw;
                            ImageViewerBox.MaxHeight = zh <= ImageViewerScroll.ActualHeight ? ImageViewerScroll.ActualHeight : zh;
                            ImageViewerBox.Width = zw <= ImageViewerScroll.ActualWidth ? ImageViewerScroll.ActualWidth : zw;
                            ImageViewerBox.Height = zh <= ImageViewerScroll.ActualHeight ? ImageViewerScroll.ActualHeight : zh;
                        }
                    }
                    if (Ready)
                    {
                        ImageViewerScale.ScaleX = ZoomRatio.Value;
                        ImageViewerScale.ScaleY = ZoomRatio.Value;
                        CalcDisplay();
                    }
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        private void ColorPick_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (Ready && sender == MasklightColorPick)
            {
                var c = (sender as ColorPicker).SelectedColor ?? null;
                MasklightColor = c == null || c == Colors.Transparent ? null : MagickColor.FromRgba(c.Value.R, c.Value.G, c.Value.B, c.Value.A);
                SyncColorLighting();
            }
        }

        private DateTime _last_quality_change = default;
        private void QualityChangerSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Ready && IsQualityChanger)
            {
                try
                {
                    var delta = (DateTime.Now - _last_quality_change).TotalMilliseconds;
                    _last_quality_change = DateTime.Now;
                    if (delta < CountDownTimeOut) QualityChangerDelay.Stop();

                    var image_s = ImageViewer.GetInformation();
                    if (image_s.ValidCurrent)
                    {
                        var quality_n = (uint)e.NewValue;
                        var quality_o = image_s.OriginalQuality;

                        if (e.NewValue != e.OldValue && quality_n <= quality_o && !IsBusy)
                        {
                            e.Handled = true;
                            SetQualityChangerTitle($"{quality_n}");
                            QualityChangerDelay.Start();
                        }
                    }
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Key Interval: {delta}ms");
#endif
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
        }

        private void QualityChanger_CloseButtonClicked(object sender, RoutedEventArgs e)
        {
            if (!Ready) return;
            try
            {
                var image_s = ImageViewer.GetInformation();
                if (image_s.ValidCurrent)
                {
                    IsProcessingViewer = true;
                    SetImageSource(ImageViewer, image_s, fit: false);
                }
                QualityChangerSlider.Tag = null;
            }
            catch (Exception ex) { ex.ShowMessage(); }
            finally { IsProcessingViewer = false; }
        }
        #endregion


    }
}
