﻿using System;
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

namespace ImageCompare
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
        public void UpdateIndaicatorState(bool source, bool state, bool busy = false)
        {
            if (source) IsProcessingSource = state;
            else IsProcessingTarget = state;
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
            if      (source == ImageType.Source || source == ImageType.All) IsProcessingSource = state;
            else if (source == ImageType.Target || source == ImageType.All) IsProcessingTarget = state;
            else if (source == ImageType.Result || source == ImageType.All) IsProcessingResult = state;
            if (busy) IsBusy = state;
        }

        #region Image Processing Routines
        /// <summary>
        /// 
        /// </summary>
        private void CleanImage()
        {
            _last_loading_ = ImageType.None;
            CloseQualityChanger();

            IsProcessingSource = true;
            IsProcessingTarget = true;
            IsProcessingResult = true;

            ImageSource.GetInformation().Dispose();
            ImageTarget.GetInformation().Dispose();
            ImageResult.GetInformation().Dispose();

            Dispatcher.InvokeAsync(() =>
            {
                if (ImageSource.Source != null) { ImageSource.Source = null; }
                if (ImageTarget.Source != null) { ImageTarget.Source = null; }
                if (ImageResult.Source != null) { ImageResult.Source = null; }

                if (ImageSource.ToolTip is ToolTip) { (ImageSource.ToolTip as ToolTip).IsOpen = false; (ImageSource.ToolTip as ToolTip).Content = null; }
                if (ImageTarget.ToolTip is ToolTip) { (ImageTarget.ToolTip as ToolTip).IsOpen = false; (ImageTarget.ToolTip as ToolTip).Content = null; }
                if (ImageResult.ToolTip is ToolTip) { (ImageResult.ToolTip as ToolTip).IsOpen = false; (ImageResult.ToolTip as ToolTip).Content = null; }

                ImageSource.ToolTip = null;
                ImageTarget.ToolTip = null;
                ImageResult.ToolTip = null;
            });

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();

            IsProcessingSource = false;
            IsProcessingTarget = false;
            IsProcessingResult = false;
            IsBusy = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private MagickImage GetImage(ImageType type)
        {
            MagickImage result = null;
            if (type != ImageType.Result)
            {
                bool source  = type == ImageType.Source;
                result = source ? ImageSource.GetInformation().Current : ImageTarget.GetInformation().Current;
            }
            else
            {
                result = ImageResult.GetInformation().Current;
            }
            return (result);
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
                    var src = source ? ImageSource : ImageTarget;
                    var tooltip = await src.GetInformation().GetImageInfo(include_colorinfo: true);
                    if (!string.IsNullOrEmpty(tooltip)) SetToolTip(src, tooltip);

                    UpdateIndaicatorState(source, false, true);

                    DoEvents();
                }
                catch (Exception ex) { ex.ShowMessage(); }
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private async void CopyImageInfo(bool source)
        {
            try
            {
                var src = source ? ImageSource : ImageTarget;
                var tooltip = GetToolTip(src);

                if (string.IsNullOrEmpty(tooltip) || tooltip.StartsWith(WaitingString, StringComparison.CurrentCultureIgnoreCase))
                {
                    tooltip = await src.GetInformation().GetImageInfo();
                    if (!string.IsNullOrEmpty(tooltip)) SetToolTip(src, tooltip);
                }

                DataObject dataPackage = new DataObject();
                dataPackage.SetText(tooltip);
                await Application.Current.Dispatcher.InvokeAsync(() => 
                {
                    //Clipboard.Clear(); 
                    Clipboard.SetDataObject(dataPackage, false); 
                });                
            }
            catch (Exception ex) { ex.ShowMessage(); }
            finally { UpdateIndaicatorState(source, false, true); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private async Task<bool> CopyImage(bool source)
        {
            var result = false;
            try
            {
                result = await CopyImageTo(source);
            }
            catch (Exception ex) { ex.ShowMessage(); }
            finally { UpdateIndaicatorState(source, false, true); }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private void CreateColorImage(bool source)
        {
            try
            {
                var src = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                var dst = source ? ImageTarget.GetInformation() : ImageSource.GetInformation();

                if (dst.ValidOriginal)
                    src.Original = new MagickImage(MasklightColor ?? MagickColors.Transparent, dst.Original.Width, dst.Original.Height);
                else if (dst.ValidCurrent)
                    src.Current = new MagickImage(MasklightColor ?? MagickColors.Transparent, dst.Current.Width, dst.Current.Height);
                else
                    src.Current = new MagickImage(MasklightColor ?? MagickColors.Transparent, MaxCompareSize, MaxCompareSize);

                UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false, reload_type: source ? ImageType.Source : ImageType.Target);
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
                    ImageSource.GetInformation().ChangeColorSpace(CompareImageForceColor);
                    ImageTarget.GetInformation().ChangeColorSpace(CompareImageForceColor);
                }
                else if (source ?? false)
                {
                    ImageSource.GetInformation().ChangeColorSpace(CompareImageForceColor);
                }
                else
                {
                    ImageTarget.GetInformation().ChangeColorSpace(CompareImageForceColor);
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
                CloseQualityChanger(source: source ? ImageType.Source : ImageType.Target);
                UpdateIndaicatorState(source, true, true);

                var action = false;
                if (source)
                    action = ImageSource.GetInformation().ResetTransform();
                else
                    action = ImageTarget.GetInformation().ResetTransform();

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
                else UpdateIndaicatorState(source, false, true);
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
                CloseQualityChanger(source: source ? ImageType.Source : ImageType.Target);
                UpdateIndaicatorState(source, true, true);

                var reload = ImageType.None;
                var action = false;
                var size = CompareImageForceScale ? MaxCompareSize : 0;
                if (source)
                {
                    reload = ImageSource.GetInformation().IsRotated ? ImageType.All : ImageType.Source;
                    action = await ImageSource.GetInformation().Reset(size);
                }
                else
                {
                    reload = ImageTarget.GetInformation().IsRotated ? ImageType.All : ImageType.Target;
                    action = await ImageTarget.GetInformation().Reset(size);
                }

                LastMatchedImage = ImageType.None;

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: ScaleMode == ImageScaleMode.Relative, reload_type: reload);
                else UpdateIndaicatorState(source, false, true);
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
                CloseQualityChanger(source: source ? ImageType.Source : ImageType.Target);
                UpdateIndaicatorState(source, true, true);

                var reload = ImageType.None;
                var action = false;
                var size = CompareImageForceScale ? MaxCompareSize : 0;
                if (source)
                {
                    if (info_only)
                    {
                        ImageSource.GetInformation().RefreshImageFileInfo();
                        var tooltip = await ImageSource.GetInformation().GetImageInfo();
                        if (!string.IsNullOrEmpty(tooltip)) SetToolTip(ImageSource, tooltip);
                    }
                    else
                    {
                        reload = ImageSource.GetInformation().IsRotated ? ImageType.All : ImageType.Source;
                        action = await ImageSource.GetInformation().Reload(size, reload: true);
                    }
                }
                else
                {
                    if (info_only)
                    {
                        ImageTarget.GetInformation().RefreshImageFileInfo();
                        var tooltip = await ImageTarget.GetInformation().GetImageInfo();
                        if (!string.IsNullOrEmpty(tooltip)) SetToolTip(ImageTarget, tooltip);
                    }
                    else
                    {
                        reload = ImageTarget.GetInformation().IsRotated ? ImageType.All : ImageType.Target;
                        action = await ImageTarget.GetInformation().Reload(size, reload: true);
                    }
                }

                LastMatchedImage = ImageType.None;

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false, reload_type: reload);
                else UpdateIndaicatorState(source, false, true);
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
                CloseQualityChanger(source: source ? ImageType.Source : ImageType.Target);
                UpdateIndaicatorState(source, true, true);

                var action = false;
                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Rotate(value);
                    image.Rotated += value;
                    image.Rotated %= 360;
                    action = true;
                }
                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
                else UpdateIndaicatorState(source, false, true);
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
                UpdateIndaicatorState(source, true, true);

                var action = false;

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Flip();
                    image.FlipY = !image.FlipY;
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
                else UpdateIndaicatorState(source, false, true);
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
                UpdateIndaicatorState(source, true, true);

                var action = false;

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Flop();
                    image.FlipX = !image.FlipX;
                    action = true;
                }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
                else UpdateIndaicatorState(source, false, true);
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
                CloseQualityChanger(source: source ? ImageType.Source : ImageType.Target);

                var action = false;

                var load_type = source ? ImageType.Source : ImageType.Target;

                var image_s = ImageSource.GetInformation();
                var image_t = ImageTarget.GetInformation();
                if (image_s.ValidCurrent && image_t.ValidCurrent)
                {
                    if (reset) await (source ? image_s : image_t).Reset(size: (CompareImageForceScale ? MaxCompareSize : 0));
                    LastMatchedImage = source ? ImageType.Target : ImageType.Source;

                    var s_image = source ? image_s.Current : image_t.Current;
                    var t_image = source ? image_t.Current : image_s.Current;

                    if (s_image.Width == t_image.Width || s_image.Height == t_image.Height)
                        s_image.Extent(t_image.Width, t_image.Height, align, s_image.HasAlpha ? MagickColors.Transparent : MasklightColor ?? MagickColors.Transparent);
                    else
                        s_image.Scale(t_image.Width, t_image.Height);
                    //s_image.Resize(t_image.Width, t_image.Height);
                    s_image.ResetPage();
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
        private void SlicingImage(bool source, bool vertical, bool sendto = true, bool first = true)
        {
            try
            {
                CloseQualityChanger(source: sendto ? ImageType.Source : ImageType.Target);

                var action = false;

                var image_s = ImageSource.GetInformation();
                var image_t = ImageTarget.GetInformation();
                var image = source ? image_s : image_t;
                if (image.ValidCurrent)
                {
                    var geometry = vertical ? new MagickGeometry(new Percentage(50), new Percentage(100)) : new MagickGeometry(new Percentage(100), new Percentage(50));
                    var result = image.Current.CropToTiles(geometry);
                    if (result.Count() >= 2)
                    {
                        if (sendto)
                        {
                            image_s.Current = new MagickImage(result.FirstOrDefault());
                            image_s.Current.ResetPage();
                            image_t.Current = new MagickImage(result.Skip(1).Take(1).FirstOrDefault());
                            image_t.Current.ResetPage();
                        }
                        else
                        {
                            if (first)
                                image.Current = new MagickImage(result.FirstOrDefault());
                            else
                                image.Current = new MagickImage(result.Skip(1).Take(1).FirstOrDefault());
                            image.Current.ResetPage();
                        }
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
        /// <param name="vertical"></param>
        /// <param name="sendto"></param>
        /// <param name="first"></param>
        private void MergeImage(bool source, bool vertical, bool sendto = true, bool first = true)
        {
            try
            {
                CloseQualityChanger(source: sendto ? ImageType.Source : ImageType.Target);

                var action = false;

                var image_s = ImageSource.GetInformation();
                var image_t = ImageTarget.GetInformation();

                if (image_s.ValidCurrent && image_t.ValidCurrent)
                {
                    var s_image = source ? image_s : image_t;
                    var t_image = source ? image_t : image_s;
                    var image = new MagickImage(s_image.Current);
                    var target = new MagickImage(t_image.Current);

                    var offset_x = image.Width == target.Width ? 0 : (image.Width - target.Width) / 2;
                    var offset_y = image.Height == target.Height ? 0 : (image.Height - target.Height) / 2;

                    var geo = new MagickGeometry(image.Width, image.Height)
                    {
                        FillArea = false,
                        Greater = true,
                        Less = false,
                        IgnoreAspectRatio = false
                    };
                    //geo_.AspectRatio = true;

                    if (vertical)
                    {
                        geo.Width = image.Width;
                        geo.Height = target.Height;
                        if (offset_x > 0)
                            target.Extent(geo, Gravity.Center, image.HasAlpha || target.HasAlpha ? MagickColors.Transparent : target.BackgroundColor);
                        else if (offset_x < 0)
                            target.Resize(geo);
                    }
                    else
                    {
                        geo.Width = target.Width;
                        geo.Height = image.Height;
                        if (offset_y > 0)
                            target.Extent(geo, Gravity.Center, image.HasAlpha || target.HasAlpha ? MagickColors.Transparent : target.BackgroundColor);
                        else if (offset_y < 0)
                            target.Resize(geo);
                    }

                    var collection = new MagickImageCollection { image, target };
                    var result = vertical ? collection.AppendVertically() : collection.AppendHorizontally();
                    result.ResetPage();

                    if (sendto)
                        t_image.Current = new MagickImage(result);
                    else
                        s_image.Current = new MagickImage(result);
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
        /// <param name="geomatry"></param>
        private async void ScaleImage(bool? source = null, MagickGeometry geomatry = null)
        {
            try
            {
                CloseQualityChanger(source: source is null ? ImageType.All : source ?? false ? ImageType.Source : ImageType.Target);

                var action = false;

                var image_s = ImageSource.GetInformation();
                var image_t = ImageTarget.GetInformation();
                if (source == null)
                {
                    if (geomatry is MagickGeometry)
                    {
                        if (image_s.ValidCurrent) { image_s.Current.Resize(geomatry); image_s.Current.ResetPage(); action = true; }
                        if (image_t.ValidCurrent) { image_t.Current.Resize(geomatry); image_t.Current.ResetPage(); action = true; }
                    }
                    else
                    {
                        action |= await image_s.Reload();
                        action |= await image_t.Reload();
                    }
                }
                else if (source ?? false)
                {
                    if (geomatry is MagickGeometry)
                    {
                        if (image_s.ValidCurrent) { image_s.Current.Resize(geomatry); image_s.Current.ResetPage(); action = true; }
                    }
                    else
                    {
                        action |= await image_s.Reload();
                    }
                }
                else
                {
                    if (geomatry is MagickGeometry)
                    {
                        if (image_t.ValidCurrent) { image_t.Current.Resize(geomatry); image_t.Current.ResetPage(); action = true; }
                    }
                    else
                    {
                        action |= await image_t.Reload();
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
        /// <param name="size"></param>
        private void ScaleImage(bool source, int size)
        {
            try
            {
                CloseQualityChanger();

                var action = false;

                var image_s = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();

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
        /// <param name="source"></param>
        /// <param name="percentage"></param>
        private void ScaleImage(bool source, Percentage percentage)
        {
            try
            {
                var geomatry = new MagickGeometry(percentage, percentage);
                ScaleImage(source, geomatry);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="size"></param>
        /// <param name="percentage"></param>
        private void ScaleImage(bool source, double size, bool percentage)
        {
            if (percentage)
                ScaleImage(source, new Percentage(Math.Max(0, 100 + size)));
            else
                ScaleImage(source, (int)(size >= 0 ? Math.Ceiling(size) : Math.Floor(size)));
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
                CloseQualityChanger(source: source is null ? ImageType.All : source ?? false ? ImageType.Source : ImageType.Target);

                var action = false;

                var image_s = ImageSource.GetInformation();
                var image_t = ImageTarget.GetInformation();
                if (source == null)
                {
                    if (geomatry is MagickGeometry)
                    {
                        if (image_s.ValidCurrent) { image_s.Current.Resize(geomatry); image_s.Current.ResetPage(); action = true; }
                        if (image_t.ValidCurrent) { image_t.Current.Resize(geomatry); image_t.Current.ResetPage(); action = true; }
                    }
                    else
                    {
                        action |= await image_s.Reload();
                        action |= await image_t.Reload();
                    }
                }
                else if (source ?? false)
                {
                    if (geomatry is MagickGeometry)
                    {
                        if (image_s.ValidCurrent) { image_s.Current.LiquidRescale(geomatry); image_s.Current.ResetPage(); action = true; }
                    }
                    else
                    {
                        action |= await image_s.Reload();
                    }
                }
                else
                {
                    if (geomatry is MagickGeometry)
                    {
                        if (image_t.ValidCurrent) { image_t.Current.LiquidRescale(geomatry); image_t.Current.ResetPage(); action = true; }
                    }
                    else
                    {
                        action |= await image_t.Reload();
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
        private void CropImage(bool source)
        {
            try
            {
                CloseQualityChanger(source: source ? ImageType.Source : ImageType.Target);

                var action = false;

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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
                CloseQualityChanger(source: source ? ImageType.Source : ImageType.Target);

                var action = false;
                var factor = WeakEffects ? 1u : 5u;
                var image_s = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                var image_t = source ? ImageTarget.GetInformation() : ImageSource.GetInformation();
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
        /// <param name="percentage"></param>
        /// <param name="align"></param>
        private void CropImageEdge(bool source, Percentage percentage, Gravity align = Gravity.Center)
        {
            try
            {
                CloseQualityChanger();
                //await Task.Delay(1);

                var action = false;
                var factor = WeakEffects ? 1u : 5u;
                var image_s = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image_s.ValidCurrent)
                {
                    var px = percentage.ToDouble() - 100;
                    var py = percentage.ToDouble() - 100;
                    switch (align)
                    {
                        case Gravity.Center: px *= 2; py *= 2; break;
                        case Gravity.North: px = 0; align = Gravity.South; break;
                        case Gravity.South: px = 0; align = Gravity.North; break;
                        case Gravity.East: py = 0; align = Gravity.West; break;
                        case Gravity.West: py = 0; align = Gravity.East; break;
                        case Gravity.Northeast: align = Gravity.Southwest; break;
                        case Gravity.Northwest: align = Gravity.Southeast; break;
                        case Gravity.Southeast: align = Gravity.Northwest; break;
                        case Gravity.Southwest: align = Gravity.Northeast; break;
                        default: break;
                    }
                    var dx = Math.Floor(image_s.BaseSize.Width / 100f);
                    var dy =  Math.Floor(image_s.BaseSize.Height / 100f);
                    var box = new MagickGeometry((uint)image_s.BaseSize.Width, (uint)image_s.BaseSize.Height)
                    {
                        IgnoreAspectRatio = px == 0 || py == 0,
                        LimitPixels = true,
                        Width = (uint)(image_s.Current.Width + px * dx),
                        Height = (uint)(image_s.Current.Height + py * dy),
                    };
                    if (percentage.ToDouble() != 0)
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
        /// <param name="size"></param>
        /// <param name="percentage"></param>
        /// <param name="align"></param>
        private void CropImageEdge(bool source, double size, bool percentage, Gravity align = Gravity.Center)
        {
            if (percentage)
                CropImageEdge(source, new Percentage(Math.Max(0, 100 + size)), align);
            else
            {
                var value = (uint)Math.Floor(Math.Abs(size));
                CropImageEdge(source, value, value, align);
            }
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
                CloseQualityChanger(source: source ? ImageType.Source : ImageType.Target);

                var action = false;
                var factor = WeakEffects ? 1u : 5u;
                var image_s = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                var image_t = source ? ImageTarget.GetInformation() : ImageSource.GetInformation();
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
        /// <param name="percentage"></param>
        /// <param name="align"></param>
        /// <param name="color"></param>
#if Q16HDRI
        private void ExtentImageEdge(bool source, Percentage percentage, Gravity align = Gravity.Center, IMagickColor<float> color = null)
#else
        private void ExtentImageEdge(bool source, Percentage percentage, Gravity align = Gravity.Center, IMagickColor<byte> color = null)
#endif
        {
            try
            {
                CloseQualityChanger();
                //await Task.Delay(1);

                var action = false;
                var factor = WeakEffects ? 1u : 5u;
                var image_s = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image_s.ValidCurrent)
                {
                    var px = percentage.ToDouble() - 100;
                    var py = percentage.ToDouble() - 100;
                    switch (align)
                    {
                        case Gravity.Center: px *= 2; py *= 2; break;
                        case Gravity.North: px = 0; align = Gravity.South; break;
                        case Gravity.South: px = 0; align = Gravity.North; break;
                        case Gravity.East: py = 0; align = Gravity.West; break;
                        case Gravity.West: py = 0; align = Gravity.East; break;
                        case Gravity.Northeast: align = Gravity.Southwest; break;
                        case Gravity.Northwest: align = Gravity.Southeast; break;
                        case Gravity.Southeast: align = Gravity.Northwest; break;
                        case Gravity.Southwest: align = Gravity.Northeast; break;
                        default: break;
                    }
                    var dx = Math.Floor(image_s.BaseSize.Width / 100f);
                    var dy =  Math.Floor(image_s.BaseSize.Height / 100f);
                    var box = new MagickGeometry((uint)image_s.BaseSize.Width, (uint)image_s.BaseSize.Height)
                    {
                        IgnoreAspectRatio = px == 0 || py == 0,
                        LimitPixels = true,
                        Width = (uint)(image_s.Current.Width + px * dx),
                        Height = (uint)(image_s.Current.Height + py * dy),
                    };
                    if (percentage.ToDouble() != 0)
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
        /// <param name="percentage"></param>
        /// <param name="align"></param>
        private void ExtentImageEdge(bool source, Percentage percentage, Gravity align = Gravity.Center)
        {
            ExtentImageEdge(source, percentage, align, MasklightColor);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="size"></param>
        /// <param name="percentage"></param>
        private void ExtentImageEdge(bool source, double size, bool percentage, Gravity align = Gravity.Center)
        {
            if (percentage)
                ExtentImageEdge(source, new Percentage(Math.Max(0, 100 + size)), align);
            else
            {
                var value = (uint)Math.Floor(Math.Abs(size));
                ExtentImageEdge(source, value, value, align);
            }
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
                //CloseQualityChanger(source: source ? ImageType.Source : ImageType.Target);

                var action = false;
                var factor = WeakEffects ? 1 : 5;
                var image_s = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                var image_t = source ? ImageTarget.GetInformation() : ImageSource.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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
        private void StereoImage(bool source)
        {
            try
            {
                var action = false;
                double angle = WeakEffects ? 3 : 5;

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                var target = source ? ImageTarget.GetInformation() : ImageSource.GetInformation();
                if (image.ValidCurrent && target.ValidCurrent)
                {
                    var image_ref = new MagickImage(target.Current);
                    image_ref.Extent(image.Current.Width, image.Current.Height);
                    image.Current.Stereo(image_ref);
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Posterize(levels, DitherMethod.Riemersma, CompareImageChannels);
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Negate(CompareImageChannels);
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    if (enchance)
                        image.Current.Level(new Percentage(-25), new Percentage(125));
                    else
                        image.Current.AutoLevel(CompareImageChannels);
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.AutoGamma(CompareImageChannels);
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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
        private void RemapImage(bool source)
        {
            try
            {
                var action = false;
                var colors = WeakEffects ? 32768u : 65536u;
                var dither = WeakEffects ? DitherMethod.FloydSteinberg :  DitherMethod.Riemersma;
                var depth = WeakEffects ? 3u : 7u;

                var image_s = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                var image_t = source ? ImageTarget.GetInformation() : ImageSource.GetInformation();
                if (image_s.ValidCurrent && image_t.ValidCurrent)
                {
                    var ref_img = new MagickImage(image_t.Current);
                    ref_img.Quantize(new QuantizeSettings() { Colors = colors, ColorSpace = ColorSpace.sRGB, DitherMethod = dither, MeasureErrors = true, TreeDepth = depth });
                    var err = image_s.Current.Remap(ref_img, new QuantizeSettings()
                    {
                        Colors = colors,
                        ColorSpace = ColorSpace.sRGB,
                        DitherMethod = dither,
                        MeasureErrors = true,
                        TreeDepth = depth
                    });
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
        private void ClutImage(bool source)
        {
            try
            {
                var action = false;

                var image_s = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                var image_t = source ? ImageTarget.GetInformation() : ImageSource.GetInformation();
                if (image_s.ValidCurrent && image_t.ValidCurrent)
                {
                    image_s.Current.Clut(image_t.Current, PixelInterpolateMethod.Spline, CompareImageChannels);
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
        private void HaldClutImage(bool source, bool opposite)
        {
            try
            {
                var action = false;
                var exists = File.Exists(LastHaldFile);

                var image_s = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                var image_t = source ? ImageTarget.GetInformation() : ImageSource.GetInformation();
                if (image_s.ValidCurrent && image_t.ValidCurrent)
                {
                    if (opposite || !exists)
                        image_s.Current.HaldClut(image_t.Current);
                    else if (exists)
                        image_s.Current.HaldClut(new MagickImage(LastHaldFile));
                    action = true;
                }
                else if (image_s.ValidCurrent && exists)
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    if (image.Current.ColorFuzz != DefaultColorFuzzy) image.Current.ColorFuzz = DefaultColorFuzzy;
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    if (image.Current.ColorFuzz != DefaultColorFuzzy) image.Current.ColorFuzz = DefaultColorFuzzy;
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

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    if (image.Current.ColorFuzz != DefaultColorFuzzy) image.Current.ColorFuzz = DefaultColorFuzzy;
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
        /// <param name="sendto"></param>
        private void CreateColorImage(bool source, bool sendto = false)
        {
            try
            {
                var action = false;

                var image_s = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                var image_t = source ? ImageTarget.GetInformation() : ImageSource.GetInformation();
                if (image_s.ValidCurrent)
                {
                    if (image_s.Current.ColorFuzz != DefaultColorFuzzy) image_s.Current.ColorFuzz = DefaultColorFuzzy;
                    if (sendto)
                        image_t.Current = new MagickImage(MasklightColor ?? image_s.Current.MatteColor ?? image_s.Current.BackgroundColor,
                                                          image_s.Current.Width, image_s.Current.Height);
                    else
                        image_s.Current = new MagickImage(MasklightColor ?? image_s.Current.MatteColor ?? image_s.Current.BackgroundColor,
                                                          image_s.Current.Width, image_s.Current.Height);
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
                        if (!image.GuessAlpha())
                        {
                            using (MemoryStream mo = new MemoryStream())
                            {
                                var target = image.Clone();
                                if (native_method)
                                {
                                    target.Quality = Math.Max(1, Math.Min(100, quality));
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
                        else $"{"InfoTipHasAlpha".T()} {(image?.HasAlpha ?? false ? "Included" : "NotIncluded").T()}".ShowMessage();
                    }
                    catch (Exception ex) { ex.ShowMessage(); }
                    return (ret);
                });
            }
            return (result);
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="compose"></param>
        /// <returns></returns>
        private async Task<MagickImage> Compare(MagickImage source, MagickImage target, bool compose = false)
        {
            MagickImage result = null;
            var st = Stopwatch.StartNew();
            var tip = new List<string>();
            try
            {
                Func<MagickImage, MagickImage> ToGray = (im) =>
                {
                    var im_out = new MagickImage(im);
                    im_out.Grayscale(GrayscaleMode);
                    im_out.MatteColor = MasklightColor;
                    im_out.ColorSpace = ColorSpace.scRGB;
                    im_out.ColorType = im.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor;
                    return(im_out);
                };

                Func<MagickImage, MagickImage> ToColor = (im) =>
                {
                    var im_out = new MagickImage(im);
                    if (im_out.ColorSpace == ColorSpace.Gray || im_out.ColorSpace == ColorSpace.LinearGray ||
                        im_out.ColorType != ColorType.TrueColor || im_out.ColorType != ColorType.TrueColorAlpha)
                    {
                        im_out.ColorSpace = ColorSpace.scRGB;
                        im_out.ColorType = im_out.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor;
                    }
                    return(im_out);
                };

                Action<IMagickImage<float>, uint, uint> NormSize = (im, w, h) =>
                {
                    im.Extent(w, h, DefaultMatchAlign);
                };

                if (source is MagickImage && target is MagickImage)
                {
                    var fuzzy = DefaultColorFuzzy;
                    if (source.ColorFuzz != fuzzy) source.ColorFuzz = fuzzy;
                    if (target.ColorFuzz != fuzzy) target.ColorFuzz = fuzzy;

                    var max_w = Math.Max(source.Width, target.Width);
                    var max_h = Math.Max(source.Height, target.Height);

                    if (compose)
                    {
                        var source_x = source.Clone();
                        var target_x = target.Clone();

                        NormSize(source_x, max_w, max_h);
                        NormSize(target_x, max_w, max_h);

                        var blend = ImageCompositeBlend.Dispatcher.Invoke(() => ImageCompositeBlend.Value);
                        var args = $"{blend:F0},{100-blend:F0}";
                        target_x.Composite(source_x, DefaultMatchAlign, CompositeMode, args, CompareImageChannels);

                        result = new MagickImage(target_x)
                        {
                            ColorFuzz = fuzzy,
                            //ColorSpace = ColorSpace.sRGB,
                            //Comment = "NetCharm Created",
                            //VirtualPixelMethod = VirtualPixelMethod.CheckerTile,
                            VirtualPixelMethod = VirtualPixelMethod.Transparent
                        };
                        result.SetArtifact("composite:align", $"{DefaultMatchAlign}");
                        result.SetArtifact("composite:channels", $"{CompareImageChannels}");
                        result.SetArtifact("composite:mode", $"{CompositeMode}");
                        result.SetArtifact("composite:args", $"{args}");

                        tip.Add($"{"ResultTipMode".T()} {CompositeMode}");

                        source_x.Dispose();
                        target_x.Dispose();
                    }
                    else
                    {
                        var setting = new CompareSettings(ErrorMetricMode)
                        {
                            HighlightColor = HighlightColor,
                            LowlightColor = LowlightColor,
                            MasklightColor = MasklightColor
                        };

                        var source_x = CompareImageForceColor ? ToColor(source) : ToGray(source);
                        var target_x = CompareImageForceColor ? ToColor(target) : ToGray(target);

                        NormSize(source_x, max_w, max_h);
                        NormSize(target_x, max_w, max_h);

                        var diff = source_x.Compare(target_x, setting, CompareImageChannels, out var distance);

                        result = new MagickImage(diff)
                        {
                            ColorFuzz = fuzzy,
                            ColorSpace = ColorSpace.sRGB,
                            //Comment = "NetCharm Created",
                            //VirtualPixelMethod = VirtualPixelMethod.CheckerTile,
                            VirtualPixelMethod = VirtualPixelMethod.Transparent
                        };
                        result.SetArtifact("compare:align", $"{DefaultMatchAlign}");
                        result.SetArtifact("compare:channels", $"{CompareImageChannels}");
                        result.SetArtifact("compare:mode", $"{ErrorMetricMode}, {(CompareImageForceColor ? "Color" : "Gray")}");
                        result.SetArtifact("compare:fuzzy", $"{fuzzy:P2}");
                        result.SetArtifact("compare:distance", $"{distance:F4}");
                        result.SetArtifact("compare:difference", $"{distance:P2}");
                        result.SetArtifact("compare:similarity", $"{1 - distance:P2}");

                        tip.Add($"{"ResultTipMode".T()} {ErrorMetricMode}");
                        tip.Add($"{"ResultTipDifference".T()} {distance:F4}");

                        source_x.Dispose();
                        target_x.Dispose();
                    }
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            finally
            {
                st?.Stop();
                tip.Add($"{"ResultTipElapsed".T()} {TimeSpan.FromTicks(st.ElapsedTicks).TotalSeconds:F4} s");
                await Dispatcher.InvokeAsync(() =>
                {
                    if (compose)
                    {
                        ImageCompare.ToolTip = DefaultCompareToolTip;
                        ImageCompose.ToolTip = tip.Count > 1 ? $"{DefaultComposeToolTip}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, tip)}" : DefaultComposeToolTip;
                    }
                    else
                    {
                        ImageCompare.ToolTip = tip.Count > 1 ? $"{DefaultCompareToolTip}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, tip)}" : DefaultCompareToolTip;
                        ImageCompose.ToolTip = DefaultComposeToolTip;
                    }
                }, DispatcherPriority.Normal);
            }
            try { if (result?.BoundingBox == null || result?.BoundingBox?.Width <= 0 || result?.BoundingBox?.Height <= 0) result?.ResetPage(); } catch { }
            return (result);
        }
    }
}
