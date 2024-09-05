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

        #region Image Processing Routines
        /// <summary>
        /// 
        /// </summary>
        private void CleanImage()
        {
            ImageSource.GetInformation().Dispose();
            ImageTarget.GetInformation().Dispose();
            ImageResult.GetInformation().Dispose();

            if (ImageSource.Source != null) { ImageSource.Source = null; }
            if (ImageTarget.Source != null) { ImageTarget.Source = null; }
            if (ImageResult.Source != null) { ImageResult.Source = null; }

            ImageSource.ToolTip = null;
            ImageTarget.ToolTip = null;
            ImageResult.ToolTip = null;

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
        private async void CalcImageColors(bool source)
        {
            try
            {
                var src = source ? ImageSource : ImageTarget;
                src.ToolTip = await src.GetInformation().GetImageInfo(include_colorinfo: true);
            }
            catch (Exception ex) { ex.ShowMessage(); }
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
                if (src.ToolTip is string && !string.IsNullOrEmpty(src.ToolTip as string))
                {
                    if ((src.ToolTip as string).StartsWith("Waiting".T(), StringComparison.CurrentCultureIgnoreCase))
                    {
                        src.ToolTip = await src.GetInformation().GetImageInfo();
                    }
                    Clipboard.SetText(src.ToolTip as string);
                }
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
                var src = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                var dst = source ? ImageTarget.GetInformation() : ImageSource.GetInformation();

                if (dst.ValidOriginal)
                    src.Original = new MagickImage(MasklightColor, dst.Original.Width, dst.Original.Height);
                else if (dst.ValidCurrent)
                    src.Current = new MagickImage(MasklightColor, dst.Current.Width, dst.Current.Height);
                else
                    src.Current = new MagickImage(MasklightColor, MaxCompareSize, MaxCompareSize);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private void CopyImage(bool source)
        {
            try
            {
                var src = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                src.CopyToClipboard();
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
                var action = false;
                if (source)
                    action = ImageSource.GetInformation().ResetTransform();
                else
                    action = ImageTarget.GetInformation().ResetTransform();

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
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
                var action = false;
                var size = UseSmallImage ? MaxCompareSize : -1;
                if (source)
                    action = await ImageSource.GetInformation().Reset(size);
                else
                    action = await ImageTarget.GetInformation().Reset(size);

                LastMatchedImage = ImageType.None;

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
            }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        private async void ReloadImage(bool source)
        {
            try
            {
                var action = false;
                var size = UseSmallImage ? MaxCompareSize : -1;
                if (source)
                    action = await ImageSource.GetInformation().Reload(size, reload: true);
                else
                    action = await ImageTarget.GetInformation().Reload(size, reload: true);

                LastMatchedImage = ImageType.None;

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
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
                var action = false;

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Flip();
                    image.FlipY = !image.FlipY;
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
        private void FlopImage(bool source)
        {
            try
            {
                var action = false;

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.Flop();
                    image.FlipX = !image.FlipX;
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
        private void GrayscaleImage(bool source)
        {
            try
            {
                var action = false;

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent) { image.Current.Grayscale(GrayscaleMode); action = true; }

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
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

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
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

                if (action) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
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
                var sigma = WeakSharp ? 5 : 10;

                var image = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                if (image.ValidCurrent)
                {
                    image.Current.ReduceNoise(sigma);
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
        /// <param name="assign"></param>
        /// <param name="reset"></param>
        /// <param name="align"></param>
        private async void ResizeToImage(bool source, bool assign = true, bool reset = false, Gravity align = Gravity.Center)
        {
            try
            {
                var action = false;

                var image_s = ImageSource.GetInformation();
                var image_t = ImageTarget.GetInformation();
                if (image_s.ValidCurrent && image_t.ValidCurrent)
                {
                    if (reset) await (source ? image_s : image_t).Reset(size: (CompareImageForceScale ? MaxCompareSize : -1));
                    LastMatchedImage = source ? ImageType.Target : ImageType.Source;

                    var s_image = source ? image_s.Current : image_t.Current;
                    var t_image = source ? image_t.Current : image_s.Current;

                    if (s_image.Width == t_image.Width || s_image.Height == t_image.Height)
                        s_image.Extent(t_image.Width, t_image.Height, align, s_image.HasAlpha ? MagickColors.Transparent : MasklightColor ?? MagickColors.Transparent);
                    else
                        s_image.Scale(t_image.Width, t_image.Height);
                    s_image.RePage();
                    action = true;
                }

                if (action && assign) UpdateImageViewer(compose: LastOpIsComposite, assign: true, reload: false);
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
                            image_s.Current.RePage();
                            image_t.Current = new MagickImage(result.Skip(1).Take(1).FirstOrDefault());
                            image_t.Current.RePage();
                        }
                        else
                        {
                            if (first)
                                image.Current = new MagickImage(result.FirstOrDefault());
                            else
                                image.Current = new MagickImage(result.Skip(1).Take(1).FirstOrDefault());
                            image.Current.RePage();
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
                var action = false;

                var image_s = ImageSource.GetInformation();
                var image_t = ImageTarget.GetInformation();

                if (image_s.ValidCurrent && image_t.ValidCurrent)
                {
                    var s_image = source ? image_s : image_t;
                    var t_image = source ? image_t : image_s;
                    var image = s_image.Current.Clone();
                    var target = t_image.Current.Clone();

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
                    result.RePage();

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
        /// <param name="geomatry"></param>
        /// <param name="source"></param>
        private async void ScaleImage(MagickGeometry geomatry = null, bool? source = null)
        {
            try
            {
                var action = false;

                var image_s = ImageSource.GetInformation();
                var image_t = ImageTarget.GetInformation();
                if (source == null)
                {
                    if (geomatry is MagickGeometry)
                    {
                        if (image_s.ValidCurrent) { image_s.Current.Resize(geomatry); image_s.Current.RePage(); action = true; }
                        if (image_t.ValidCurrent) { image_t.Current.Resize(geomatry); image_t.Current.RePage(); action = true; }
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
                        if (image_s.ValidCurrent) { image_s.Current.Resize(geomatry); image_s.Current.RePage(); action = true; }
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
                        if (image_t.ValidCurrent) { image_t.Current.Resize(geomatry); image_t.Current.RePage(); action = true; }
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
        /// <param name="geomatry"></param>
        /// <param name="source"></param>
        private async void LiquidScaleImage(MagickGeometry geomatry = null, bool? source = null)
        {
            try
            {
                var action = false;

                var image_s = ImageSource.GetInformation();
                var image_t = ImageTarget.GetInformation();
                if (source == null)
                {
                    if (geomatry is MagickGeometry)
                    {
                        if (image_s.ValidCurrent) { image_s.Current.Resize(geomatry); image_s.Current.RePage(); action = true; }
                        if (image_t.ValidCurrent) { image_t.Current.Resize(geomatry); image_t.Current.RePage(); action = true; }
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
                        if (image_s.ValidCurrent) { image_s.Current.LiquidRescale(geomatry); image_s.Current.RePage(); action = true; }
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
                        if (image_t.ValidCurrent) { image_t.Current.LiquidRescale(geomatry); image_t.Current.RePage(); action = true; }
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
                    image.Current.RePage();
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
        private void CropImageEdge(bool source, int width = 0, int height = 0, Gravity align = Gravity.Center)
        {
            try
            {
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
                        image_s.Current.RePage();
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
                    image.Current.Morphology(MorphologyMethod.Smooth, Kernel.Euclidean);
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
                var colors = WeakEffects ? 32768 : 65536;
                var dither = WeakEffects ? DitherMethod.No :  DitherMethod.Riemersma;
                var depth = WeakEffects ? 3 : 7;

                var image_s = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                var image_t = source ? ImageTarget.GetInformation() : ImageSource.GetInformation();
                if (image_s.ValidCurrent && image_t.ValidCurrent)
                {
                    var err = image_s.Current.Map(image_t.Current, new QuantizeSettings()
                    {
                        MeasureErrors = true,
                        Colors = colors,
                        ColorSpace = ColorSpace.sRGB,
                        DitherMethod = dither,
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
        private void HaldClutImage(bool source)
        {
            try
            {
                var action = false;
                var shift = Keyboard.Modifiers == ModifierKeys.Shift;
                var exists = File.Exists(LastHaldFile);

                var image_s = source ? ImageSource.GetInformation() : ImageTarget.GetInformation();
                var image_t = source ? ImageTarget.GetInformation() : ImageSource.GetInformation();
                if (image_s.ValidCurrent && image_t.ValidCurrent)
                {
                    if (shift || !exists)
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
                var radius = WeakEffects ? 3 : 5;

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
                var radius = WeakEffects ? 5 : 10;
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
                var sigma = WeakEffects ? 256 : 64;

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
                        image.Current.FloodFill(MasklightColor ?? MagickColors.Transparent, image.Current.Width - 2, 1);
                        image.Current.FloodFill(MasklightColor ?? MagickColors.Transparent, image.Current.Width - 2, image.Current.Height - 2);
                        image.Current.FloodFill(MasklightColor ?? MagickColors.Transparent, 1, image.Current.Height - 2);
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
                Func<MagickImage, IMagickImage<float>> ToGray = (im) =>
                {
                    var im_out = im.Clone();
                    im_out.Grayscale(GrayscaleMode);
                    im_out.MatteColor = MasklightColor;
                    im_out.ColorSpace = ColorSpace.scRGB;
                    im_out.ColorType = im.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor;
                    return(im_out);
                };

                Func<MagickImage, IMagickImage<float>> ToColor = (im) =>
                {
                    var im_out = im.Clone();
                    if (im_out.ColorSpace == ColorSpace.Gray || im_out.ColorSpace == ColorSpace.LinearGray ||
                        im_out.ColorType != ColorType.TrueColor || im_out.ColorType != ColorType.TrueColorAlpha)
                    {
                        im_out.ColorSpace = ColorSpace.scRGB;
                        im_out.ColorType = im_out.HasAlpha ? ColorType.TrueColorAlpha : ColorType.TrueColor;
                    }
                    return(im_out);
                };

                Action<IMagickImage<float>, int, int> NormSize = (im, w, h) =>
                {
                    im.Extent(w, h, DefaultMatchAlign, MagickColors.Transparent);
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
                            //Comment = "NetCharm Created",
                            //VirtualPixelMethod = VirtualPixelMethod.CheckerTile,
                            VirtualPixelMethod = VirtualPixelMethod.Transparent
                        };
                        result.SetArtifact("composite:align", $"{DefaultMatchAlign}");
                        result.SetArtifact("composite:channels", $"{CompareImageChannels}");
                        result.SetArtifact("composite:mode", $"{CompositeMode}");
                        result.SetArtifact("composite:args", $"{args}");

                        tip.Add($"{"ResultTipMode".T()} {CompositeMode}");
                        await Task.Delay(1);
                        DoEvents();
                    }
                    else
                    {
                        using (MagickImage diff = new MagickImage() { BorderColor = MagickColors.Transparent, BackgroundColor = MagickColors.Transparent, MatteColor = MagickColors.Transparent })
                        {
                            var setting = new CompareSettings()
                            {
                                Metric = ErrorMetricMode,
                                HighlightColor = HighlightColor,
                                LowlightColor = LowlightColor,
                                MasklightColor = MasklightColor
                            };

                            var source_x = CompareImageForceColor ? ToColor(source) : ToGray(source);
                            var target_x = CompareImageForceColor ? ToColor(target) : ToGray(target);

                            NormSize(source_x, max_w, max_h);
                            NormSize(target_x, max_w, max_h);

                            var distance = source_x.Compare(target_x, setting, diff, CompareImageChannels);

                            result = new MagickImage(diff)
                            {
                                ColorFuzz = fuzzy,
                                //Comment = "NetCharm Created",
                                //VirtualPixelMethod = VirtualPixelMethod.CheckerTile,
                                VirtualPixelMethod = VirtualPixelMethod.Transparent
                            };
                            result.SetArtifact("compare:align", $"{DefaultMatchAlign}");
                            result.SetArtifact("compare:channels", $"{CompareImageChannels}");
                            result.SetArtifact("compare:mode", $"{ErrorMetricMode}");
                            result.SetArtifact("compare:distance", $"{distance:F4}");

                            tip.Add($"{"ResultTipMode".T()} {ErrorMetricMode}");
                            tip.Add($"{"ResultTipDifference".T()} {distance:F4}");

                            //await Task.Delay(1);
                            //DoEvents();
                        }
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
                        ImageCompose.ToolTip = tip.Count > 1 ? $"{DefaultComposeToolTip}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, tip)}" : DefaultComposeToolTip;
                        ImageCompare.ToolTip = DefaultCompareToolTip;
                    }
                    else
                    {
                        ImageCompare.ToolTip = tip.Count > 1 ? $"{DefaultComposeToolTip}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, tip)}" : DefaultCompareToolTip;
                        ImageCompose.ToolTip = DefaultComposeToolTip;
                    }
                }, DispatcherPriority.Normal);
            }
            return (result);
        }
    }
}
