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
    public partial class MainWindow : Window
    {
        #region Image Processing Routines
        private Dictionary<string, string> GetSupportedImageFormats()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            try
            {
                var fmts = Enum.GetNames(typeof(MagickFormat));
                foreach (var fmt in fmts)
                {
                    result.Add(fmt, "");
                }
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
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

            ImageSource.ToolTip = null;
            ImageTarget.ToolTip = null;
            ImageResult.ToolTip = null;

            SourceLoaded = false;
            TargetLoaded = false;

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="image"></param>
        /// <param name="update"></param>
        private void SetImage(ImageType type, MagickImage image, bool update = true)
        {
            try
            {
                if (image == null || image.IsDisposed) return;
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
                        if (CompareImageForceScale)
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
                        if (CompareImageForceScale)
                        {
                            TargetImage.Resize(CompareResizeGeometry);
                            TargetImage.RePage();
                        }
                        FlipX_Target = false;
                        FlipY_Target = false;
                        Rotate_Target = 0;
                        TargetLoaded = true;
                    }
                    LastImageType = type;
                }
                else
                {
                    if (ResultImage is MagickImage) ResultImage.Dispose();
                    ResultImage = image;
                }
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
            if (update) UpdateImageViewer(assign: true, compose: LastOpIsCompose);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="image"></param>
        /// <param name="update"></param>
        private void SetImage(ImageType type, IMagickImage<float> image, bool update = true)
        {
            try
            {
#if Q16HDRI
                SetImage(type, new MagickImage(image), update: update);
#endif
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="image"></param>
        /// <param name="update"></param>
        private void SetImage(ImageType type, IMagickImage<byte> image, bool update = true)
        {
            try
            {
#if Q16
                SetImage(type, new MagickImage(image), update: update);
#endif
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private void CopyImageInfo(bool source)
        {
            if (source ^ ToggleSourceTarget)
            {
                if (ImageSource.ToolTip is string && !string.IsNullOrEmpty(ImageSource.ToolTip as string))
                    Clipboard.SetText(ImageSource.ToolTip as string);
            }
            else
            {
                if (ImageTarget.ToolTip is string && !string.IsNullOrEmpty(ImageTarget.ToolTip as string))
                    Clipboard.SetText(ImageTarget.ToolTip as string);
            }
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
                    if (SourceImage is MagickImage && !SourceImage.IsDisposed &&
                        SourceOriginal is MagickImage && !SourceOriginal.IsDisposed)
                    {
                        var color = CompareImageForceColor ? ColorSpace.sRGB : SourceOriginal.ColorSpace;
                        if (color != SourceImage.ColorSpace) SourceImage.ColorSpace = color;
                    }
                    if (TargetImage is MagickImage && !TargetImage.IsDisposed &&
                        TargetOriginal is MagickImage && !TargetOriginal.IsDisposed)
                    {
                        var color = CompareImageForceColor ? ColorSpace.sRGB : TargetOriginal.ColorSpace;
                        if (color != TargetImage.ColorSpace) TargetImage.ColorSpace = color;
                    }
                }
                else
                {
                    if ((source ?? false) ^ ToggleSourceTarget)
                    {
                        if (SourceImage is MagickImage && !SourceImage.IsDisposed &&
                            SourceOriginal is MagickImage && !SourceOriginal.IsDisposed)
                        {
                            var color = CompareImageForceColor ? ColorSpace.sRGB : SourceOriginal.ColorSpace;
                            if (color != SourceImage.ColorSpace) SourceImage.ColorSpace = color;
                        }
                    }
                    else
                    {
                        if (TargetImage is MagickImage && !TargetImage.IsDisposed &&
                            TargetOriginal is MagickImage && !TargetOriginal.IsDisposed)
                        {
                            var color = CompareImageForceColor ? ColorSpace.sRGB : TargetOriginal.ColorSpace;
                            if (color != TargetImage.ColorSpace) TargetImage.ColorSpace = color;
                        }
                    }
                }
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private void ResetImage(bool source)
        {
            try
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private void FlipImage(bool source)
        {
            try
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private void FlopImage(bool source)
        {
            try
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private void ReloadImage(bool source)
        {
            try
            {
                var action = false;
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
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
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
                if (source ^ ToggleSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.Grayscale();
                        //SourceImage.RePage();
                        action = true;
                    }
                }
                else
                {
                    if (TargetImage is MagickImage)
                    {
                        if (SourceOriginal == null && SourceImage is MagickImage) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                        TargetImage.Grayscale();
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
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
                if (source ^ ToggleSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        //SourceImage.GaussianBlur(radius, sigma);
                        SourceImage.AdaptiveBlur(radius, sigma);
                        //SourceImage.RePage();
                        action = true;
                    }
                }
                else
                {
                    if (TargetImage is MagickImage)
                    {
                        if (SourceOriginal == null && SourceImage is MagickImage) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                        TargetImage.GaussianBlur(radius, sigma);
                        //TargetImage.AdaptiveBlur(radius, sigma);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
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
                if (source ^ ToggleSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.UnsharpMask(radius, sigma, amount, threshold);
                        //SourceImage.AdaptiveSharpen(radius, 1, CompareImageChannels);
                        //SourceImage.RePage();
                        action = true;
                    }
                }
                else
                {
                    if (TargetImage is MagickImage)
                    {
                        if (SourceOriginal == null && SourceImage is MagickImage) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                        TargetImage.UnsharpMask(radius, sigma, amount, threshold);
                        //TargetImage.AdaptiveSharpen(radius, 1, CompareImageChannels);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private void ResizeToImage(bool source)
        {
            try
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="vertical"></param>
        private void SlicingImage(bool source, bool vertical)
        {
            try
            {
                var action = false;
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
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private void CropImage(bool source)
        {
            try
            {
                var action = false;
                if (source ^ ToggleSourceTarget)
                {
                    if (SourceImage is MagickImage && TargetImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.Crop(SourceImage.BoundingBox);
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
                        TargetImage.Crop(TargetImage.BoundingBox);
                        TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
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
                if (source ^ ToggleSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.OilPaint(radius, sigma);
                        //SourceImage.RePage();
                        action = true;
                    }
                }
                else
                {
                    if (TargetImage is MagickImage)
                    {
                        if (SourceOriginal == null && SourceImage is MagickImage) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                        TargetImage.OilPaint(radius, sigma);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
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
                var radius = WeakEffects ? 5 : 10;
                var sigma = WeakEffects ? 0.25 : 0.5;
                if (source ^ ToggleSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.Charcoal(radius, sigma);
                        //SourceImage.RePage();
                        action = true;
                    }
                }
                else
                {
                    if (TargetImage is MagickImage)
                    {
                        if (SourceOriginal == null && SourceImage is MagickImage) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                        TargetImage.Charcoal(radius, sigma);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
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
                var radius = WeakEffects ? 5 : 10;
                var sigma = WeakEffects ? 5 : 10;
                if (source ^ ToggleSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.MeanShift(radius, new Percentage(sigma));
                        //SourceImage.RePage();
                        action = true;
                    }
                }
                else
                {
                    if (TargetImage is MagickImage)
                    {
                        if (SourceOriginal == null && SourceImage is MagickImage) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                        TargetImage.MeanShift(radius, new Percentage(sigma));
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
        }
       
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
                                diff.Composite(i_src, CompositeMode, CompareImageChannels);
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
                                var distance = i_src.Compare(i_dst, setting, diff, CompareImageChannels);
                                tip.Add($"Mode       : {ErrorMetricMode.ToString()}");
                                tip.Add($"Difference : {distance:F4}");
                                result = new MagickImage(diff.Clone());
                                await Task.Delay(1);
                                DoEvents();
                            }
                        }
                    }
                }
                catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message); }
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

    }
}
