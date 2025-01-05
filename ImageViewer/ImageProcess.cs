using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

namespace ImageViewer
{
    public static class Extentions
    {
        #region Color Processing Routunes
        static public bool HasAlpha(this MagickColor color)
        {
            var result = false;
            if (color is MagickColor)
            {
                result = color.A < MagickColors.White.A;
            }
            return (result);
        }

        static public bool HasAlpha(this IMagickColor<float> color)
        {
            var result = false;
            if (color is MagickColor)
            {
                result = color.A < MagickColors.White.A;
            }
            return (result);
        }
        #endregion

    }

    public partial class MainWindow : Window
    {
        #region Image Processing Switch/Params
        private bool SimpleTrimCropBoundingBox { get; set; } = false;
        #endregion

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="state"></param>
        /// <param name="busy"></param>
        public void UpdateIndaicatorState(bool state, bool busy = false)
        {
            IsProcessingViewer = state;
            if (busy) IsBusy = state;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="state"></param>
        /// <param name="busy"></param>
        public void UpdateIndaicatorState(ImageType source, bool state, bool busy = false)
        {
            if      (source == ImageType.Source || source == ImageType.All) IsProcessingViewer = state;
            if (busy) IsBusy = state;
        }

        #region Image Processing Routines
        /// <summary>
        ///
        /// </summary>
        private void CleanImage()
        {
            CloseQualityChanger();
            ResetViewTransform(calcdisplay: false);

            IsProcessingViewer = true;

            ImageViewer.GetInformation().Dispose();

            Dispatcher?.InvokeAsync(() =>
            {
                if (ImageViewer.Source != null) { ImageViewer.Source = null; }

                if (ImageViewer.ToolTip is ToolTip) { (ImageViewer.ToolTip as ToolTip).IsOpen = false; (ImageViewer.ToolTip as ToolTip).Content = null; }

                ImageViewer.ToolTip = null;
            });

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();

            IsProcessingViewer = false;
            IsBusy = false;
        }

        /// <summary>
        /// 
        /// </summary>
        private void ClearImage()
        {
            Dispatcher?.InvokeAsync(() =>
            {
                if (ImageViewer.Source != null) { ImageViewer.Source = null; }

                if (ImageViewer.ToolTip is ToolTip) { (ImageViewer.ToolTip as ToolTip).IsOpen = false; (ImageViewer.ToolTip as ToolTip).Content = null; }
                
                ResetViewTransform(calcdisplay: false);
                ImageViewer.ToolTip = null;
                ImageInfoBox.ToolTip = null;
                ImageIndexBox.ToolTip = null;
            });

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private MagickImage GetImage(ImageType type)
        {
            return (ImageViewer.GetInformation().Current);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void CalcImageColors(bool source)
        {
            Task.Run(async () =>
            {
                try
                {
                    await UpdateImageToolTip(calc_colors: true);
                    UpdateIndaicatorState(false, true);
                    DoEvents();
                }
                catch (Exception ex) { ex.ShowMessage(); }
            });
        }

        /// <summary>
        ///
        /// </summary>
        private async void CopyImageInfo()
        {
            try
            {
                var src = ImageViewer;
                var tooltip = GetToolTip(src);

                if (string.IsNullOrEmpty(tooltip) || tooltip.StartsWith(WaitingString, StringComparison.CurrentCultureIgnoreCase))
                {
                    tooltip = await UpdateImageToolTip();
                }

                DataObject dataPackage = new DataObject();
                dataPackage.SetText(tooltip);
                await Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    //Clipboard.Clear();
                    Clipboard.SetDataObject(dataPackage, false);
                });
            }
            catch (Exception ex) { ex.ShowMessage(); }
            finally { UpdateIndaicatorState(false, true); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private async Task<bool> CopyImage()
        {
            var result = false;
            try
            {
                result = await CopyImageTo();
            }
            catch (Exception ex) { ex.ShowMessage(); }
            finally { UpdateIndaicatorState(false, true); }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void CreateColorImage()
        {
            try
            {
                var src = ImageViewer.GetInformation();

                src.Current = new MagickImage(MasklightColor ?? MagickColors.Transparent, MaxCompareSize, MaxCompareSize);

                UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void ChangeColorSpace(bool? source = null)
        {
            try
            {
                if (source == null)
                {
                    ImageViewer.GetInformation().ChangeColorSpace(true);
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void ResetImageTransform(bool source)
        {
            try
            {
                CloseQualityChanger();

                UpdateIndaicatorState(true, true);

                var action = ImageViewer.GetInformation().ResetTransform();

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
                else UpdateIndaicatorState(false, true);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private async void ResetImage(bool source)
        {
            try
            {
                CloseQualityChanger();

                UpdateIndaicatorState(true, true);

                var reload = ImageType.None;
                var action = false;
                var size = 0;
                if (source)
                {
                    reload = ImageViewer.GetInformation().IsRotated ? ImageType.All : ImageType.Source;
                    action = await ImageViewer.GetInformation().Reset(size);
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
                else UpdateIndaicatorState(false, true);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private async void ReloadImage(bool source, bool info_only = false)
        {
            try
            {
                CloseQualityChanger();

                UpdateIndaicatorState(true, true);

                var action = false;
                var size = 0;
                if (info_only)
                {
                    await UpdateImageToolTip();
                }
                else
                {
                    action = await ImageViewer.GetInformation().Reload(size, reload: true);
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
                else UpdateIndaicatorState(false, true);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="value"></param>
        private void RotateImage(bool source, int value)
        {
            try
            {
                CloseQualityChanger();
                //await Task.Delay(1);

                UpdateIndaicatorState(true, true);

                var action = false;
                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Rotate(value);
                    image.Rotated += value;
                    image.Rotated %= 360;
                    action = true;
                }
                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
                else UpdateIndaicatorState(false, true);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void FlipImage(bool source)
        {
            try
            {
                UpdateIndaicatorState(true, true);

                var action = false;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Flip();
                    image.FlipY = !image.FlipY;
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
                else UpdateIndaicatorState(false, true);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void FlopImage(bool source)
        {
            try
            {
                UpdateIndaicatorState(true, true);

                var action = false;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Flop();
                    image.FlipX = !image.FlipX;
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
                else UpdateIndaicatorState(false, true);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="assign"></param>
        /// <param name="reset"></param>
        /// <param name="align"></param>
        private async void ResizeToImage(bool source, bool assign = true, bool reset = false, Gravity align = Gravity.Center)
        {
            try
            {
                CloseQualityChanger();

                var action = false;

                var load_type = source ? ImageType.Source : ImageType.Target;

                var image_s = ImageViewer.GetInformation();
                if (image_s.ValidCurrent)
                {
                    if (reset) await image_s.Reset(size: 0);

                    var s_image = image_s.Current;

                    //if (s_image.Width == t_image.Width || s_image.Height == t_image.Height)
                    //    s_image.Extent(t_image.Width, t_image.Height, align, s_image.HasAlpha ? MagickColors.Transparent : MasklightColor ?? MagickColors.Transparent);
                    //else
                    //    s_image.Scale(t_image.Width, t_image.Height);
                    //s_image.ResetPage();

                    action = true;
                }

                if (action && assign) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false, reload_type: load_type);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="vertical"></param>
        /// <param name="sendto"></param>
        /// <param name="first"></param>
        private void SlicingImage(bool source, bool vertical, bool first = true)
        {
            try
            {
                CloseQualityChanger();
                //await Task.Delay(1);

                var action = false;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    var geometry = vertical ? new MagickGeometry(new Percentage(50), new Percentage(100)) : new MagickGeometry(new Percentage(100), new Percentage(50));
                    var result = image.Current.CropToTiles(geometry);
                    if (result.Count() >= 2)
                    {
                        if (first)
                            image.Current = new MagickImage(result.FirstOrDefault());
                        else
                            image.Current = new MagickImage(result.Skip(1).Take(1).FirstOrDefault());
                        image.Current.ResetPage();

                        action = true;
                    }
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="geomatry"></param>
        /// <param name="source"></param>
        private void ScaleImage(MagickGeometry geomatry = null)
        {
            try
            {
                CloseQualityChanger();

                var action = false;

                var image_s = ImageViewer.GetInformation();

                if (geomatry is MagickGeometry && image_s.ValidCurrent)
                {
                    image_s.Current.FilterType = FilterType.Lanczos2Sharp;
                    image_s.Current.FilterType = FilterType.MagicKernelSharp2021;

                    //image_s.Current.Scale(geomatry); image_s.Current.ResetPage(); action = true;
                    //image_s.Current.InterpolativeResize(geomatry, PixelInterpolateMethod.Bilinear); image_s.Current.ResetPage(); action = true;
                    image_s.Current.Resize(geomatry); image_s.Current.ResetPage(); action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="size"></param>
        private void ScaleImage(int size)
        {
            try
            {
                CloseQualityChanger();

                var action = false;

                var image_s = ImageViewer.GetInformation();

                image_s.Current.FilterType = FilterType.Lanczos2Sharp;
                image_s.Current.FilterType = FilterType.MagicKernelSharp2021;
                var w = (uint)Math.Max(1, image_s.Current.Width + size);
                var h = (uint)Math.Max(1, image_s.Current.Height + size);
                image_s.Current.Resize(w, h); 
                image_s.Current.ResetPage(); 
                action = true;

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="percentage"></param>
        private void ScaleImage(Percentage percentage)
        {
            try
            {
                var geomatry = new MagickGeometry(percentage, percentage);
                ScaleImage(geomatry);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="percentage"></param>
        private void ScaleImage(double size, bool percentage)
        {
            if (percentage)
                ScaleImage(new Percentage(Math.Max(0, 100 + size)));
            else
                ScaleImage((int)(size >= 0 ? Math.Ceiling(size) : Math.Floor(size)));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="geomatry"></param>
        /// <param name="source"></param>
        private async void LiquidScaleImage(MagickGeometry geomatry = null, bool? source = null)
        {
            try
            {
                CloseQualityChanger();

                var action = false;

                var image_s = ImageViewer.GetInformation();
                if (geomatry is MagickGeometry)
                {
                    if (image_s.ValidCurrent) { image_s.Current.Resize(geomatry); image_s.Current.ResetPage(); action = true; }
                }
                else
                {
                    action |= await image_s.Reload();
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void CropImage(bool source)
        {
            try
            {
                CloseQualityChanger();
                //await Task.Delay(1);

                var action = false;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    if (SimpleTrimCropBoundingBox)
                        image.Current.Trim();
                    else
                    {
                        var box = image.Current.CalcBoundingBox();
                        if (box == null)
                            image.Current.Trim();
                        else
                            image.Current.Crop(box);
                    }
                    image.Current.ResetPage();
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="align"></param>
        private void CropImageEdge(bool source, uint width = 0, uint height = 0, Gravity align = Gravity.Center)
        {
            try
            {
                CloseQualityChanger();
                //await Task.Delay(1);

                var action = false;
                var factor = WeakEffects ? 1u : 5u;
                var image_s = ImageViewer.GetInformation();
                if (image_s.ValidCurrent)
                {
                    switch (align)
                    {
                        case Gravity.Center: width *= 2; height *= 2; break;
                        case Gravity.North: width = 0; align = Gravity.South; break;
                        case Gravity.South: width = 0; align = Gravity.North; break;
                        case Gravity.East: height = 0; align = Gravity.West; break;
                        case Gravity.West: height = 0; align = Gravity.East; break;
                        case Gravity.Northeast: align = Gravity.Southwest; break;
                        case Gravity.Northwest: align = Gravity.Southeast; break;
                        case Gravity.Southeast: align = Gravity.Northwest; break;
                        case Gravity.Southwest: align = Gravity.Northeast; break;
                        default: break;
                    }
                    var box = new MagickGeometry(image_s.Current.Width, image_s.Current.Height)
                    {
                        IgnoreAspectRatio = true,
                        LimitPixels = true,
                        Width = Math.Max(1, Math.Min(image_s.Current.Width, image_s.Current.Width - width * factor)),
                        Height = Math.Max(1, Math.Min(image_s.Current.Height, image_s.Current.Height - height * factor)),
                    };
                    if (width > 0 || height > 0)
                    {
                        image_s.Current.Extent(box.Width, box.Height, align);
                        image_s.Current.ResetPage();
                        action = true;
                    }
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="align"></param>
        /// <param name="color"></param>
#if Q16HDRI
        private void ExtentImageEdge(bool source, uint width = 0, uint height = 0, Gravity align = Gravity.Center, IMagickColor<float> color = null)
#else
        private void ExtentImageEdge(bool source, uint width = 0, uint height = 0, Gravity align = Gravity.Center, IMagickColor<byte> color = null)
#endif
        {
            try
            {
                CloseQualityChanger();
                //await Task.Delay(1);

                var action = false;
                var factor = WeakEffects ? 1u : 5u;
                var image_s = ImageViewer.GetInformation();
                if (image_s.ValidCurrent)
                {
                    switch (align)
                    {
                        case Gravity.Center: width *= 2; height *= 2; break;
                        case Gravity.North: width = 0; align = Gravity.South; break;
                        case Gravity.South: width = 0; align = Gravity.North; break;
                        case Gravity.East: height = 0; align = Gravity.West; break;
                        case Gravity.West: height = 0; align = Gravity.East; break;
                        case Gravity.Northeast: align = Gravity.Southwest; break;
                        case Gravity.Northwest: align = Gravity.Southeast; break;
                        case Gravity.Southeast: align = Gravity.Northwest; break;
                        case Gravity.Southwest: align = Gravity.Northeast; break;
                        default: break;
                    }
                    var box = new MagickGeometry(image_s.Current.Width, image_s.Current.Height)
                    {
                        IgnoreAspectRatio = true,
                        LimitPixels = true,
                        Width = Math.Max(image_s.Current.Width, image_s.Current.Width + width * factor),
                        Height = Math.Max(image_s.Current.Height, image_s.Current.Height + height * factor),
                    };
                    if (width > 0 || height > 0)
                    {
                        image_s.Current.Extent(box.Width, box.Height, align, color ?? MagickColors.Transparent);
                        image_s.Current.ResetPage();
                        action = true;
                    }
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="align"></param>
        private void ExtentImageEdge(bool source, uint width = 0, uint height = 0, Gravity align = Gravity.Center)
        {
            ExtentImageEdge(source, width, height, align, MasklightColor);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="align"></param>
        private void PanImageEdge(bool source, uint width = 0, uint height = 0, Gravity align = Gravity.Center)
        {
            ExtentImageEdge(source, width, height, align, MagickColors.Transparent);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="align"></param>
        private void RollImageEdge(bool source, int width = 0, int height = 0, Gravity align = Gravity.Center)
        {
            try
            {
                //CloseQualityChanger();
                //await Task.Delay(1);

                var action = false;
                var factor = WeakEffects ? 1 : 5;
                var image_s = ImageViewer.GetInformation();
                if (image_s.ValidCurrent)
                {
                    switch (align)
                    {
                        case Gravity.Center: width *= 2; height *= 2; break;
                        case Gravity.North: width = 0; align = Gravity.South; break;
                        case Gravity.South: width = 0; align = Gravity.North; break;
                        case Gravity.East: height = 0; align = Gravity.West; break;
                        case Gravity.West: height = 0; align = Gravity.East; break;
                        case Gravity.Northeast: align = Gravity.Southwest; break;
                        case Gravity.Northwest: align = Gravity.Southeast; break;
                        case Gravity.Southeast: align = Gravity.Northwest; break;
                        case Gravity.Southwest: align = Gravity.Northeast; break;
                        default: break;
                    }

                    if (width > 0 || height > 0)
                    {
                        image_s.Current.Roll(Math.Max(0, width * factor), Math.Max(0, height * factor));
                        image_s.Current.ResetPage();
                        action = true;
                    }
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void GrayscaleImage(bool source)
        {
            try
            {
                var action = false;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent) { image.Current.Grayscale(GrayscaleMode); action = true; }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false, reload_type: source ? ImageType.Source : ImageType.Target);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void BlurImage(bool source)
        {
            try
            {
                var action = false;
                var radius = WeakBlur ? 5 : 10;
                var sigma = WeakBlur ? 0.75 : 1.5;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.GaussianBlur(radius, sigma);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false, reload_type: source ? ImageType.Source : ImageType.Target);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void SharpImage(bool source)
        {
            try
            {
                var action = false;
                var radius = 5;
                var sigma = WeakSharp ? 0.25 : 0.35;
                var amount = 15;
                var threshold = 0;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.UnsharpMask(radius, sigma, amount, threshold);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false, reload_type: source ? ImageType.Source : ImageType.Target);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void ReduceNoiseImage(bool source)
        {
            try
            {
                var action = false;
                var sigma = WeakSharp ? 5u : 10u;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.ReduceNoise(sigma);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false, reload_type: source ? ImageType.Source : ImageType.Target);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void EmbossImage(bool source)
        {
            try
            {
                var action = false;
                var radius = WeakBlur ? 5 : 10;
                var sigma = WeakBlur ? 0.75 : 1.5;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Emboss(radius, sigma);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        /// <param name="method"></param>
        /// <param name="kernel"></param>
        private void MorphologyImage(bool source, MorphologyMethod method = MorphologyMethod.Smooth, Kernel kernel = Kernel.Euclidean)
        {
            try
            {
                var action = false;
                var radius = WeakBlur ? 5 : 10;
                var sigma = WeakBlur ? 0.75 : 1.5;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    var setting = new MorphologySettings() { Kernel = Kernel.Euclidean, Method = MorphologyMethod.Smooth };
                    image.Current.Morphology(setting);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void EdgeImage(bool source)
        {
            try
            {
                var action = false;
                var radius = WeakBlur ? 5 : 10;
                var sigma = WeakBlur ? 0.75 : 1.5;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.CannyEdge(radius, sigma, new Percentage(3.0), new Percentage(15));
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void PencilImage(bool source)
        {
            try
            {
                var action = false;
                var radius = WeakEffects ? 3 : 7;
                var sigma = WeakEffects ? 0.33 : 0.66;
                var angle = WeakEffects ? 15 : 30;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    //image.Current.Sketch();
                    image.Current.Sketch(radius, sigma, angle);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void OilImage(bool source)
        {
            try
            {
                var action = false;
                var radius = WeakEffects ? 3 : 7;
                var sigma = WeakEffects ? 0.33 : 0.66;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.OilPaint(radius, sigma);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void CharcoalImage(bool source)
        {
            try
            {
                var action = false;
                var radius = WeakEffects ? 3 : 7;
                var sigma = WeakEffects ? 0.25 : 0.5;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Charcoal(radius, sigma);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void PolaroidImage(bool source)
        {
            try
            {
                var action = false;
                double angle = WeakEffects ? 3 : 5;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Polaroid("❤❤", angle, PixelInterpolateMethod.Spline);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void SolarizeImage(bool source)
        {
            try
            {
                var action = false;
                var sigma = WeakEffects ? 12.5 : 25;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    //image.Current.Solarize(new Percentage(sigma));
                    image.Current.Solarize();
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void PosterizeImage(bool source)
        {
            try
            {
                var action = false;
                var levels = WeakEffects ? 32 : 16;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Posterize(levels, DitherMethod.Riemersma);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void InvertImage(bool source)
        {
            try
            {
                var action = false;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Negate();
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void AutoEqualizeImage(bool source)
        {
            try
            {
                var action = false;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Equalize();
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void AutoEnhanceImage(bool source)
        {
            try
            {
                var action = false;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Enhance();
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void AutoWhiteBalanceImage(bool source)
        {
            try
            {
                var action = false;
                var enhance = new Percentage(10);

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    if (WeakEffects) image.Current.WhiteBalance();
                    else image.Current.WhiteBalance(enhance);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void AutoContrastImage(bool source)
        {
            try
            {
                var action = false;
                var enchance = !WeakEffects;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Contrast();
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void AutoLevelImage(bool source)
        {
            try
            {
                var action = false;
                var enchance = !WeakEffects;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    if (enchance)
                        image.Current.Level(new Percentage(-25), new Percentage(125));
                    else
                        image.Current.AutoLevel();
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void AutoGammaImage(bool source)
        {
            try
            {
                var action = false;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.AutoGamma();
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void AutoThresholdImage(bool source)
        {
            try
            {
                var action = false;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.AutoThreshold(AutoThresholdMethod.OTSU);
                    image.Current.ColorSpace = ColorSpace.Gray;
                    image.Current.ColorType = ColorType.Palette;
                    image.Current.Depth = 8;
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void BlueShiftImage(bool source)
        {
            try
            {
                var action = false;
                var radius = WeakEffects ? 0.75 : 1.05;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.BlueShift(radius);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void AutoVignetteImage(bool source)
        {
            try
            {
                var action = false;
                var radios = WeakEffects ? 50.0 : 150.0;
                var sigma = WeakEffects ? 64 : 64;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Vignette((new double[] { radios / 2.0, image.Current.Width, image.Current.Height }).Min(), sigma, 5, 5);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void HaldClutImage(bool source)
        {
            try
            {
                var action = false;
                var exists = !string.IsNullOrEmpty(LastHaldFile) && File.Exists(LastHaldFile);

                var image_s = ImageViewer.GetInformation();
                 if (image_s.ValidCurrent && exists)
                {
                    image_s.Current.HaldClut(new MagickImage(LastHaldFile));
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void MedianFilterImage(bool source)
        {
            try
            {
                var action = false;
                var radius = WeakEffects ? 3u : 5u;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.MedianFilter(radius);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void MeanShiftImage(bool source)
        {
            try
            {
                var action = false;
                var radius = WeakEffects ? 5u : 10u;
                var sigma = WeakEffects ? 5 : 10;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.MeanShift(radius, new Percentage(sigma));
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void KmeansImage(bool source)
        {
            try
            {
                var action = false;
                var sigma = WeakEffects ? 16 : 8;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Kmeans(new KmeansSettings() { Tolerance = sigma, NumberColors = 64, MaxIterations = 100 });
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void SegmentImage(bool source)
        {
            try
            {
                var action = false;
                var sigma = WeakEffects ? 200 : 100;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Segment(ColorSpace.RGB, sigma, sigma * 0.25);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void QuantizeImage(bool source)
        {
            try
            {
                var action = false;
                var sigma = WeakEffects ? 256u : 64u;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Quantize(new QuantizeSettings() { Colors = sigma, ColorSpace = ColorSpace.RGB, MeasureErrors = false });
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void FillOutBoundBoxImage(bool source)
        {
            try
            {
                var action = false;
                var radius = WeakEffects ? 5 : 10;
                var sigma = WeakEffects ? 0.25 : 0.5;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.BackgroundColor = MasklightColor ?? MagickColors.Transparent;
                    if (image.LastClickPos == null)
                    {
                        image.Current.FloodFill(MasklightColor ?? MagickColors.Transparent, 1, 1);
                        image.Current.FloodFill(MasklightColor ?? MagickColors.Transparent, (int)image.Current.Width - 2, 1);
                        image.Current.FloodFill(MasklightColor ?? MagickColors.Transparent, (int)image.Current.Width - 2, (int)image.Current.Height - 2);
                        image.Current.FloodFill(MasklightColor ?? MagickColors.Transparent, 1, (int)image.Current.Height - 2);
                    }
                    else
                    {
                        var pos = image.LastClickPos ?? image.DefaultOrigin;
                        image.Current.FloodFill(MasklightColor ?? MagickColors.Transparent, (int)pos.X, (int)pos.Y);
                    }
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void SetColorToAlphaImage(bool source)
        {
            try
            {
                var action = false;
                var radius = WeakEffects ? 5 : 10;
                var sigma = WeakEffects ? 0.25 : 0.5;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    if (image.Current.HasAlpha)
                    {
                        image.Current.ColorAlpha(MasklightColor ?? image.Current.BackgroundColor);
                        foreach (var attr in image.Attributes) image.Current.SetAttribute(attr.Key, attr.Value);
                        foreach (var profile in image.Profiles) image.Current.SetProfile(profile.Value);
                        action = true;
                    }
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void SetAlphaToColorImage(bool source)
        {
            try
            {
                var action = false;
                var radius = WeakEffects ? 5 : 10;
                var sigma = WeakEffects ? 0.25 : 0.5;

                var image = ImageViewer.GetInformation();
                if (image.ValidCurrent)
                {
                    var alpha = new MagickImage(MasklightColor ?? image.Current.BackgroundColor, image.Current.Width, image.Current.Height);
                    image.Current.Composite(alpha, CompositeOperator.ChangeMask);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="source"></param>
        private void CreateColorImage(bool source)
        {
            try
            {
                var action = false;

                var image_s = ImageViewer.GetInformation();
                if (image_s.ValidCurrent)
                {
                    image_s.Current = new MagickImage(MasklightColor ?? image_s.Current.MatteColor ?? image_s.Current.BackgroundColor, image_s.Current.Width, image_s.Current.Height);
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns></returns>
        private System.Drawing.Imaging.ImageCodecInfo GetEncoderInfo(string mimeType)
        {
            // Get image codecs for all image formats
            var codecs = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders();

            // Find the correct image codec
            for (int i = 0; i < codecs.Length; i++)
            {
                if (codecs[i].MimeType == mimeType) return codecs[i];
            }

            return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="fmt"></param>
        /// <param name="quality"></param>
        /// <param name="colordepth"></param>
        /// <returns></returns>
        private byte[] ConvertImageTo(byte[] buffer, string fmt, uint quality = 85, long colordepth = 0, Color? bgcolor = null)
        {
            byte[] result = null;
            try
            {
                if (buffer is byte[] && buffer.Length > 0)
                {
                    System.Drawing.Imaging.ImageFormat pFmt = System.Drawing.Imaging.ImageFormat.MemoryBmp;

                    fmt = fmt.ToLower();
                    if (fmt.Equals("png")) pFmt = System.Drawing.Imaging.ImageFormat.Png;
                    else if (fmt.Equals("jpg")) pFmt = System.Drawing.Imaging.ImageFormat.Jpeg;
                    else return (buffer);

                    using (var mi = new MemoryStream(buffer))
                    {
                        using (var mo = new MemoryStream())
                        {
                            var bmp = new System.Drawing.Bitmap(mi);
                            var codec_info = GetEncoderInfo("image/jpeg");
                            var qualityParam = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                            var colorDepth = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.ColorDepth, colordepth <= 0 ? 24L : colordepth);
                            var encoderParams = new System.Drawing.Imaging.EncoderParameters(2);

                            encoderParams.Param[0] = qualityParam;
                            encoderParams.Param[1] = colorDepth;
                            if (pFmt == System.Drawing.Imaging.ImageFormat.Jpeg)
                            {
                                var canvas = new System.Drawing.Bitmap(bmp.Width, bmp.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(canvas))
                                {
                                    if (mi.CanSeek) mi.Seek(0, SeekOrigin.Begin);
                                    var bg = bgcolor ?? Colors.White;
                                    g.Clear(System.Drawing.Color.FromArgb(bg.A, bg.R, bg.G, bg.B));
                                    g.DrawImage(bmp, 0, 0, new System.Drawing.Rectangle(new System.Drawing.Point(), bmp.Size), System.Drawing.GraphicsUnit.Pixel);
                                }
                                bmp.Save(mo, codec_info, encoderParams);
                            }
                            else
                                bmp.Save(mo, pFmt);
                            result = mo.ToArray();
                            bmp.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="image"></param>
        /// <param name="quality"></param>
        /// <returns></returns>
        private async Task<MagickImage> ChangeQuality(MagickImage image, uint quality, bool native_method = true)
        {
            var result = image;
            if (image is MagickImage)
            {
                result = await Task.Run(async () =>
                {
                    var ret = image;
                    try
                    {
                        var alpha = image.GuessAlpha();
                        if (!alpha)
                        {
                            using (MemoryStream mo = new MemoryStream())
                            {
                                var target = image.Clone();
                                if (native_method)
                                {
                                    target.Settings.SetDefine(MagickFormat.Jpeg, "sampling-factor", "4:2:0");
                                    target.Settings.SetDefine(MagickFormat.Jpeg, "dct-method", "float");
                                    target.Settings.AntiAlias = true;
                                    target.Settings.Endian = image.Endian;
                                    target.Settings.Interlace = Interlace.Plane;

                                    target.BackgroundColor = MasklightColor ?? target.BackgroundColor;
                                    target.MatteColor = MasklightColor ?? target.BackgroundColor;
                                    target.Density = image.Density;
                                    target.Format = image.Format;
                                    target.Endian = image.Endian;
                                    target.Quality = Math.Max(1, Math.Min(100, quality));
                                    foreach (var profile in image.ProfileNames) { if (image.HasProfile(profile)) target.SetProfile(image.GetProfile(profile)); }

                                    await target.WriteAsync(mo, MagickFormat.Jpg);
                                }
                                else
                                {
                                    var bg = (Color)ColorConverter.ConvertFromString(image.BackgroundColor.ToHexString());
                                    var bytes_i = target.ToByteArray(MagickFormat.Png);
                                    var bytes_o = ConvertImageTo(bytes_i, "jpg", quality, bgcolor: bg);
                                    await mo.WriteAsync(bytes_o, 0, bytes_o.Length);
                                }
                                await mo.FlushAsync();
                                if (mo.Length > 0)
                                {
                                    ret = new MagickImage(mo.ToArray());
                                    ret.SetArtifact($"filesize", $"{mo.Length.SmartFileSize()}");
                                    ret.SetArtifact($"quality", $"{ret.Quality()}");
                                }
                            }
                        }
                        else $"{"InfoTipHasAlpha".T()} {(alpha ? "Included" : "NotIncluded").T()}".ShowMessage();
                    }
                    catch (Exception ex) { ex.ShowMessage(); }
                    return (ret);
                });
            }
            return (new MagickImage(result));
        }
        #endregion
    }
}
