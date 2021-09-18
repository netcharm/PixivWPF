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
    public class ImageInfomation
    {
        public MagickImage Current { get; set; } = null;
        public MagickImage Original { get; set; } = null;
        public FrameworkElement Tagetment { get; set; } = null;
        public string FileName { get; set; } = string.Empty;
        public bool Loaded { get; set; } = false;
        public bool FlipX { get; set; } = false;
        public bool FlipY { get; set; } = false;
        public double Rotate { get; set; } = 0;
    }

    public partial class MainWindow : Window
    {
        #region Image Processing Switch/Params
        private bool SimpleTrimCropBoundingBox { get; set; } = false;
        #endregion

        #region Image Processing Routines
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string> GetSupportedImageFormats()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            try
            {
                foreach (var fmt in MagickNET.SupportedFormats)
                {
                    if (fmt.IsReadable)
                    {
                        if (fmt.MimeType != null && fmt.MimeType.StartsWith("video", StringComparison.CurrentCultureIgnoreCase)) continue;
                        else if (fmt.Description.StartsWith("video", StringComparison.CurrentCultureIgnoreCase)) continue;
                        else if (fmt.MimeType != null && fmt.MimeType.StartsWith("image", StringComparison.CurrentCultureIgnoreCase))
                            result.Add(fmt.Format.ToString(), fmt.Description);
                        else result.Add(fmt.Format.ToString(), fmt.Description);
                    }
                }

                //var fmts = Enum.GetNames(typeof(MagickFormat));
                //foreach (var fmt in fmts)
                //{
                //    result.Add(fmt, "");
                //}
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
            return (result);
        }

        private List<string> _auto_formats_ = new List<string>() { ".jpg", ".jpeg", ".png", ".bmp" };
        private Dictionary<string, MagickFormat> _supported_formats_ = new Dictionary<string, MagickFormat>();
        private MagickFormat GetImageFileFormat(string ext)
        {
            var result = MagickFormat.Unknown;
            try
            {
                if (_supported_formats_.Count <= 0)
                {
                    foreach (var fmt in MagickNET.SupportedFormats) _supported_formats_.Add($".{fmt.Format.ToString().ToLower()}", fmt.Format);
                }
                if (_supported_formats_.ContainsKey(ext.ToLower()) && !_auto_formats_.Contains(ext.ToLower())) result = _supported_formats_[ext.ToLower()];
            }
            catch { }
            return (result);
        }

        private IMagickGeometry CalcBoundingBox(MagickImage image)
        {
            var result = image.BoundingBox;
            try
            {
                if (image is MagickImage && !image.IsDisposed)
                {
                    var diff = new MagickImage(image);
                    diff.ColorType = ColorType.Bilevel;
                    result = diff.BoundingBox;
                    diff.Dispose();
                }
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                result = source ^ ExchangeSourceTarget ? SourceImage : TargetImage;
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
                    bool source  = ExchangeSourceTarget ? (type == ImageType.Source ? false : true) : (type == ImageType.Source ? true : false);
                    if (source)
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private void CopyImageInfo(bool source)
        {
            if (source ^ ExchangeSourceTarget)
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
                    if ((source ?? false) ^ ExchangeSourceTarget)
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceOriginal is MagickImage && !SourceOriginal.IsDisposed)
                    {
                        SetImage(source ? ImageType.Source : ImageType.Target, SourceOriginal, update: false);
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
                        SetImage(source ? ImageType.Source : ImageType.Target, TargetOriginal, update: false);
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.GaussianBlur(radius, sigma);
                        //SourceImage.AdaptiveBlur(radius, sigma);
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage && TargetImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                        if (SourceImage.Width == TargetImage.Width || SourceImage.Height == TargetImage.Height)
                            SourceImage.Extent(TargetImage.Width, TargetImage.Height, Gravity.Center, MagickColors.Transparent);
                        else
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
                        if (SourceImage.Width == TargetImage.Width || SourceImage.Height == TargetImage.Height)
                            TargetImage.Extent(SourceImage.Width, SourceImage.Height, Gravity.Center, MagickColors.Transparent);
                        else
                            TargetImage.Resize(SourceImage.Width, SourceImage.Height);
                        TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
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
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        //SourceImage.Crop(SourceImage.BoundingBox);
                        if (SimpleTrimCropBoundingBox)
                            SourceImage.Trim();
                        else
                        {
                            var box = CalcBoundingBox(SourceImage);
                            if (box == null)
                                SourceImage.Trim();
                            else
                                SourceImage.Crop(box);
                        }
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
                        //TargetImage.Crop(TargetImage.BoundingBox);
                        if (SimpleTrimCropBoundingBox)
                            TargetImage.Trim();
                        else
                        {
                            var box = CalcBoundingBox(TargetImage);
                            if (box == null)
                                TargetImage.Trim();
                            else
                                TargetImage.Crop(box);
                        }
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.Polaroid("❤❤", angle, PixelInterpolateMethod.Spline);
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
                        TargetImage.Polaroid("❤❤", angle, PixelInterpolateMethod.Spline);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                var levels = WeakEffects ? 12 : 6;
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.Posterize(levels, DitherMethod.Riemersma, CompareImageChannels);
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
                        TargetImage.Posterize(levels, DitherMethod.Riemersma, CompareImageChannels);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.Negate(CompareImageChannels);
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
                        TargetImage.Negate(CompareImageChannels);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.Equalize();
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
                        TargetImage.Equalize();
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.Enhance();
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
                        TargetImage.Enhance();
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        if (WeakEffects) SourceImage.WhiteBalance();
                        else SourceImage.WhiteBalance(enhance);
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
                        if (WeakEffects) TargetImage.WhiteBalance();
                        else TargetImage.WhiteBalance(enhance);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.Contrast(enchance);
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
                        TargetImage.Contrast(enchance);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.AutoLevel(CompareImageChannels);
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
                        TargetImage.AutoLevel(CompareImageChannels);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.AutoGamma(CompareImageChannels);
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
                        TargetImage.AutoGamma(CompareImageChannels);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.AutoThreshold(AutoThresholdMethod.OTSU);
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
                        TargetImage.AutoThreshold(AutoThresholdMethod.OTSU);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.BlueShift(radius);
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
                        TargetImage.BlueShift(radius);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.Vignette((new double[] { radios / 2.0, SourceImage.Width, SourceImage.Height }).Min(), sigma, 5, 5);
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
                        TargetImage.Vignette((new double[] { radios / 2.0, TargetImage.Width, TargetImage.Height }).Min(), sigma, 5, 5);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                var colors = WeakEffects ? 32768 : 65536;
                var dither = WeakEffects ? DitherMethod.No :  DitherMethod.Riemersma;
                var depth = WeakEffects ? 3 : 7;
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        if (TargetImage is MagickImage)
                        {
                            var err = SourceImage.Map(TargetImage, new QuantizeSettings()
                            {
                                MeasureErrors = true,
                                Colors = colors,
                                ColorSpace = ColorSpace.sRGB,
                                DitherMethod = dither,
                                TreeDepth = depth
                            });
                            //SourceImage.RePage();
                            action = true;
                        }
                    }
                }
                else
                {
                    if (TargetImage is MagickImage)
                    {
                        if (SourceOriginal == null && SourceImage is MagickImage) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                        if (SourceImage is MagickImage)
                        {
                            var err = TargetImage.Map(TargetImage, new QuantizeSettings()
                            {
                                MeasureErrors = true,
                                Colors = colors,
                                ColorSpace = ColorSpace.sRGB,
                                DitherMethod = dither,
                                TreeDepth = depth
                            });
                            //TargetImage.RePage();
                            action = true;
                        }
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        if (TargetImage is MagickImage)
                        {
                            SourceImage.HaldClut(TargetImage);
                            //SourceImage.RePage();
                            action = true;
                        }
                    }
                }
                else
                {
                    if (TargetImage is MagickImage)
                    {
                        if (SourceOriginal == null && SourceImage is MagickImage) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                        if (SourceImage is MagickImage)
                        {
                            TargetImage.HaldClut(SourceImage);
                            //TargetImage.RePage();
                            action = true;
                        }
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                var radius = WeakEffects ? 3 : 5;
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.MedianFilter(radius);
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
                        TargetImage.MedianFilter(radius);
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        SourceImage.Kmeans(new KmeansSettings() { Tolerance = sigma, NumberColors = 64, MaxIterations = 100 });
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
                        TargetImage.Kmeans(new KmeansSettings() { Tolerance = sigma, NumberColors = 64, MaxIterations = 100 });
                        //TargetImage.RePage();
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                if (source ^ ExchangeSourceTarget)
                {
                    if (SourceImage is MagickImage)
                    {
                        if (SourceOriginal == null) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null && TargetImage is MagickImage) TargetOriginal = new MagickImage(TargetImage.Clone());
                        if (SourceImage.ColorFuzz.ToDouble() != ImageCompareFuzzy.Value) SourceImage.ColorFuzz = new Percentage(ImageCompareFuzzy.Value);
                        SourceImage.BackgroundColor = MasklightColor ?? MagickColors.Transparent;
                        SourceImage.FloodFill(MasklightColor ?? MagickColors.Transparent, 1, 1);
                        SourceImage.FloodFill(MasklightColor ?? MagickColors.Transparent, SourceImage.Width - 2, 1);
                        SourceImage.FloodFill(MasklightColor ?? MagickColors.Transparent, SourceImage.Width - 2, SourceImage.Height - 2);
                        SourceImage.FloodFill(MasklightColor ?? MagickColors.Transparent, 1, SourceImage.Height - 2);
                        action = true;
                    }
                }
                else
                {
                    if (TargetImage is MagickImage)
                    {
                        if (SourceOriginal == null && SourceImage is MagickImage) SourceOriginal = new MagickImage(SourceImage.Clone());
                        if (TargetOriginal == null) TargetOriginal = new MagickImage(TargetImage.Clone());
                        if (TargetImage.ColorFuzz.ToDouble() != ImageCompareFuzzy.Value) TargetImage.ColorFuzz = new Percentage(ImageCompareFuzzy.Value);
                        TargetImage.BackgroundColor = MasklightColor ?? MagickColors.Transparent;
                        TargetImage.FloodFill(MasklightColor ?? MagickColors.Transparent, 1, 1);
                        TargetImage.FloodFill(MasklightColor ?? MagickColors.Transparent, TargetImage.Width - 2, 1);
                        TargetImage.FloodFill(MasklightColor ?? MagickColors.Transparent, TargetImage.Width - 2, TargetImage.Height - 2);
                        TargetImage.FloodFill(MasklightColor ?? MagickColors.Transparent, 1, TargetImage.Height - 2);
                        action = true;
                    }
                }
                if (action) UpdateImageViewer(compose: LastOpIsCompose, assign: true);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
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
                        var fuzzy = Math.Min(Math.Max(ImageCompareFuzzy.Minimum, ImageCompareFuzzy.Value), ImageCompareFuzzy.Maximum);
                        if (source.ColorFuzz.ToDouble() != fuzzy) source.ColorFuzz = new Percentage(fuzzy);
                        if (target.ColorFuzz.ToDouble() != fuzzy) target.ColorFuzz = new Percentage(fuzzy);

                        var i_src = ExchangeSourceTarget ? target : source;
                        var i_dst = ExchangeSourceTarget ? source : target;

                        if (compose)
                        {
                            using (MagickImage diff = new MagickImage(i_dst.Clone()))
                            {
                                diff.Composite(i_src, CompositeMode, CompareImageChannels);
                                tip.Add($"{"ResultTipMode".T()} {CompositeMode.ToString()}");
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
                                tip.Add($"{"ResultTipMode".T()} {ErrorMetricMode.ToString()}");
                                tip.Add($"{"ResultTipDifference".T()} {distance:F4}");
                                result = new MagickImage(diff.Clone());
                                //result.Comment = "NetCharm Created";
                                await Task.Delay(1);
                                DoEvents();
                            }
                        }
                    }
                }
                catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(this, ex.Message); }
                finally
                {
                    st.Stop();
                    tip.Add($"{"ResultTipElapsed".T()} {TimeSpan.FromTicks(st.ElapsedTicks).TotalSeconds:F4} s");
                    if (compose)
                    {
                        ImageCompose.ToolTip = tip.Count > 1 ? string.Join(Environment.NewLine, tip) : DefaultComposeToolTip;
                        ImageCompare.ToolTip = DefaultCompareToolTip;
                    }
                    else
                    {
                        ImageCompare.ToolTip = tip.Count > 1 ? string.Join(Environment.NewLine, tip) : DefaultCompareToolTip;
                        ImageCompose.ToolTip = DefaultComposeToolTip;
                    }
                }
            }, DispatcherPriority.Render);
            return (result);
        }
        #endregion

    }
}
