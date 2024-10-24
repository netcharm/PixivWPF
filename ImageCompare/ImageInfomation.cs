﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using ImageMagick;

namespace ImageCompare
{
    public enum ImageTarget { None, Source, Target, Result, All };

    public class SourceParam
    {
        public MagickGeometry Geometry { get; set; } = null;
        public Gravity Align { get; set; } = Gravity.Undefined;
#if Q16HDRI
        public IMagickColor<float> FillColor { get; set; } = MagickColors.Transparent;
#else
        public IMagickColor<byte> FillColor { get; set; } = MagickColors.Transparent;
#endif
    }

    public class ImageInformation
    {
        public ImageType Type { get; set; } = ImageType.None;

        private MagickImage _original_ = null;
        public MagickImage Original
        {
            get { return (_original_); }
            set
            {
                if (_original_ is MagickImage) { _original_.Dispose(); _original_ = null; }
                _original_ = value;
                _OriginalModified_ = true;
                GetProfiles();
                DenoiseCount = 0;
                DenoiseLevel = 0;
                if (ValidOriginal) _original_.FilterType = FilterType.CubicSpline;
                //if (_OriginalModified_) Dispatcher.CurrentDispatcher.InvokeAsync(async () => { await Reload(); });
                if (_OriginalModified_) Dispatcher.CurrentDispatcher.Invoke(async () => { await Reload(); });
                //if (_OriginalModified_) Reload();
            }
        }
        public Size OriginalSize { get { return (ValidOriginal ? new Size(Original.Width, Original.Height) : new Size(0, 0)); } }
        public long OriginalRealMemoryUsage
        {
            get
            {
                if (ValidOriginal)
                {
#if Q16HDRI
                    return ((long)Original.Width * Original.Height * Original.ChannelCount * Original.Depth * 4 / 8);
#elif Q16
                    return ((long)Original.Width * Original.Height * Original.ChannelCount * Original.Depth * 2 / 8);
#else
                    return ((long)Original.Width * Original.Height * Original.ChannelCount * Original.Depth / 8);
#endif
                }
                else return (-1);
            }
        }
        public long OriginalDisplayMemoryUsage
        {
            get
            {
                if (ValidOriginal)
                {
                    return ((long)Original.Width * Original.Height * Application.Current.GetSystemColorDepth() / 8);
                }
                else return (-1);
            }
        }
        public long OriginalIdealMemoryUsage
        {
            get
            {
                if (ValidOriginal)
                {
                    return ((long)Original.Width * Original.Height * Original.ChannelCount * Original.Depth / 8);
                }
                else return (-1);
            }
        }
        public IMagickFormatInfo OriginalFormatInfo { get { return (ValidOriginal ? MagickFormatInfo.Create(_original_.Format) : null); } }

        private MagickImage _current_ = null;
        public MagickImage Current
        {
            get { return (_current_); }
            set
            {
                if (_current_ is MagickImage) { _current_.Dispose(); _current_ = null; }
                _current_ = value;
                _CurrentModified_ = true;
                GetProfiles();
                if (ValidCurrent)
                {
                    _current_.FilterType = FilterType.CubicSpline;
                    if (ValidOriginal && !string.IsNullOrEmpty(FileName))
                    {
                        if (_original_.Endian == Endian.Undefined) _original_.Endian = DetectFileEndian(FileName);
                        if (_current_.Endian == Endian.Undefined) _current_.Endian = _original_.Endian;
                    }
                    else
                    {
                        if (_current_.Endian == Endian.Undefined) _current_.Endian = BitConverter.IsLittleEndian ? Endian.LSB : Endian.MSB;
                    }
                }
            }
        }
        public Size CurrentSize { get { return (ValidCurrent ? new Size(Current.Width, Current.Height) : new Size(0, 0)); } }
        public long CurrentRealMemoryUsage
        {
            get
            {
                if (ValidCurrent)
                {
#if Q16HDRI
                    return ((long)Current.Width * Current.Height * Current.ChannelCount * Current.Depth * 4 / 8);
#elif Q16
                    return ((long)Current.Width * Current.Height * Current.ChannelCount * Current.Depth * 2 / 8);
#else
                    return ((long)Current.Width * Current.Height * Current.ChannelCount * Current.Depth / 8);
#endif
                }
                else return (-1);
            }
        }
        public long CurrentDisplayMemoryUsage
        {
            get
            {
                if (ValidCurrent)
                {
                    return ((long)Current.Width * Current.Height * Application.Current.GetSystemColorDepth() / 8);
                }
                else return (-1);
            }
        }
        public long CurrentIdealMemoryUsage
        {
            get
            {
                if (ValidCurrent)
                {
                    return ((long)Current.Width * Current.Height * Current.ChannelCount * Current.Depth / 8);
                }
                else return (-1);
            }
        }
        public IMagickFormatInfo CurrentFormatInfo { get { return (ValidCurrent ? MagickFormatInfo.Create(_current_.Format) : null); } }

#if Q16HDRI
        public IMagickColor<float> HighlightColor = MagickColors.Red;
        public IMagickColor<float> LowlightColor = null;
        public IMagickColor<float> MasklightColor = null;
#else
        public IMagickColor<byte> HighlightColor = MagickColors.Red;
        public IMagickColor<byte> LowlightColor = null;
        public IMagickColor<byte> MasklightColor = null;
#endif
        public ImageOpMode OpMode = ImageOpMode.None;
        public Percentage ColorFuzzy = new Percentage();

        public int ChannelCount { get { return (ValidOriginal ? Original.ChannelCount : (ValidCurrent ? Current.ChannelCount : -1)); } }
        public string MemoryUsageMode
        {
            get
            {
#if Q16HDRI
                return ($"Q16HDRI[({ChannelCount}x32bits)/Pixel, ({ChannelCount}x4Bytes)/Pixel]");
#elif Q16
                return ("Q16(16bits/Pixel, 2Bytes/Pixel)");
#else
                return ("Q8(8bits/Pixel, 1Byte/Pixel)");
#endif
            }
        }

        private Size _basesize_ = new Size(0, 0);
        public Size BaseSize { get { return (ValidCurrent ? _basesize_ : new Size(0, 0)); } }

        private ColorSpace _last_colorspace_ = ColorSpace.Undefined;

        public SourceParam SourceParams { get; set; } = new SourceParam();
        public ImageSource Source
        {
            get
            {
                ImageSource result = null;
                if (ValidCurrent)
                {
                    try
                    {
                        var image = new MagickImage(Current);
                        image.Extent(SourceParams.Geometry, SourceParams.Align, MagickColors.Transparent);
                        image.RePage();
                        result = image.ToBitmapSource();
                    }
                    catch (Exception ex) { ex.ShowMessage(); }
                }
                return (result);
            }
        }


        public bool ValidCurrent { get { return (Current is MagickImage); } }
        public bool ValidOriginal { get { return (Original is MagickImage); } }

        public bool OriginalIsFile { get { return (!string.IsNullOrEmpty(FileName) && File.Exists(LastFileName)); } }

        private bool ValidImage(MagickImage image)
        {
            return (image is MagickImage);
        }

        public FrameworkElement Tagetment { get; set; } = null;
        public string TagetmentTooltip { get; set; } = null;

        private string LastFileName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;

        public bool AutoScale { get; set; } = true;
        public int AutoScaleSize { get; set; } = 1024;

        public bool Loaded { get; set; } = false;
        public bool _OriginalModified_ = true;
        public bool OriginalModified { get { bool _value_ = _OriginalModified_; _OriginalModified_ = false; return (_value_); } set { _OriginalModified_ = value; } }
        public bool _CurrentModified_ = true;
        public bool CurrentModified { get { bool _value_ = _CurrentModified_; _CurrentModified_ = false; return (_value_); } set { _CurrentModified_ = value; } }

        public PointD DefaultOrigin { get; } = new PointD(0, 0);
        private PointD? _LastClickPos_ = null;
        public PointD? LastClickPos
        {
            get { return (_LastClickPos_); }
            set
            {
                if (ValidCurrent && value.HasValue)
                {
                    var point = value ?? DefaultOrigin;
                    //var x = Math.Max(0, Math.Min(Current.Width, point.X));
                    //var y = Math.Max(0, Math.Min(Current.Height, point.X));
                    var x = 0 <= point.X && point.X < Current.Width ? point.X : (_LastClickPos_ ?? DefaultOrigin).X;
                    var y = 0 <= point.Y && point.Y < Current.Height ? point.Y : (_LastClickPos_ ?? DefaultOrigin).Y;
                    _LastClickPos_ = new PointD(x, y);
                }
            }
        }

        public Dictionary<string, IImageProfile> Profiles { get; set; } = new Dictionary<string, IImageProfile>();
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();

        public bool FlipX { get; set; } = false;
        public bool FlipY { get; set; } = false;
        public double Rotated { get; set; } = .0;

        public int DenoiseCount { get; set; } = 0;
        public int DenoiseLevel { get; set; } = 0;

        public MagickGeometry CurrentGeometry { get; internal set; }

        public void FixDPI(MagickImage image = null, bool use_system = false)
        {
            if (image == null) image = Current;
            if (ValidImage(image))
            {
                var dpi = Application.Current.GetSystemDPI();
                if (image.Density is Density && image.Density.X > 0 && image.Density.Y > 0)
                {
                    var unit = image.Density.ChangeUnits(DensityUnit.PixelsPerInch);
                    if (use_system || unit.X <= 0 || unit.Y <= 0)
                        image.Density = new Density(dpi.X, dpi.Y, DensityUnit.PixelsPerInch);
                    else
                        image.Density = new Density(Math.Round(unit.X), Math.Round(unit.Y), DensityUnit.PixelsPerInch);
                }
                else Current.Density = new Density(dpi.X, dpi.Y, DensityUnit.PixelsPerInch);
            }
        }

        public async Task<bool> LoadImageFromClipboard()
        {
            var result = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var ret = false;
                var exceptions = new List<string>();
                try
                {
                    var supported_fmts = new string[] { "PNG", "image/png", "image/tif", "image/tiff", "image/webp", "image/xpm", "image/ico", "image/cur", "image/jpg", "image/jpeg", "image/bmp", "DeviceIndependentBitmap", "image/wbmp", "Text" };
                    IDataObject dataPackage = Clipboard.GetDataObject();
                    var fmts = dataPackage.GetFormats(true);
                    foreach (var fmt in supported_fmts)
                    {
                        if (fmts.Contains(fmt) && dataPackage.GetDataPresent(fmt, true))
                        {
                            if (fmt.Equals("Text", StringComparison.CurrentCultureIgnoreCase))
                            {
                                var text = dataPackage.GetData(fmt, true) as string;
                                try
                                {
                                    var image = MagickImage.FromBase64(Regex.Replace(text, @"^data:.*?;base64,", "", RegexOptions.IgnoreCase));
                                    Original = new MagickImage(image);
                                    image.Dispose();
                                    FileName = string.Empty;
                                    OriginalModified = true;
                                    ret = true;
                                    break;
                                }
#if DEBUG
                                catch (Exception ex) { exceptions.Add($"{fmt} : {ex.Message}"); Debug.WriteLine(ex.Message); }
#else
                                catch (Exception ex) { exceptions.Add($"{fmt} : {ex.Message}"); }
#endif
                            }
                            else
                            {
                                try
                                {
                                    var obj = dataPackage.GetData(fmt, true);
                                    if (obj is MemoryStream)
                                    {
                                        Original = new MagickImage(obj as MemoryStream);
                                        FileName = string.Empty;
                                        OriginalModified = true;
                                        ret = true;
                                        break;
                                    }
                                }
#if DEBUG
                                catch (Exception ex) { exceptions.Add($"{fmt} : {ex.Message}"); Debug.WriteLine(ex.Message); }
#else
                                catch (Exception ex) { exceptions.Add($"{fmt} : {ex.Message}"); }
#endif
                            }
                        }
                    }
                }
                catch (Exception ex) { ex.ShowMessage();}
                if(!ret) string.Join(Environment.NewLine, exceptions).ShowMessage();
                return(ret);
            }, DispatcherPriority.Render);
            return (result);
        }

        public async Task<bool> LoadImageFromPrevFile()
        {
            var result = false;
            result = await Application.Current.Dispatcher.Invoke(async () =>
            {
                bool ret = false;
                try
                {
                    if (!string.IsNullOrEmpty(FileName))
                    {
                        var file = FileName;
                        var files = file.GetFiles();
                        if (files.Count() > 0 && !string.IsNullOrEmpty(file))
                        {
                            var file_n = files.Where(f => f.EndsWith(file, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                            var idx = files.IndexOf(file_n);
                            if (idx > 0) ret = await LoadImageFromFile(files[idx - 1]);
                        }
                    }
                }
                catch (Exception ex) { ex.ShowMessage(); }
                return (ret);
            });
            return (result);
        }

        public async Task<bool> LoadImageFromNextFile()
        {
            var result = false;
            result = await Application.Current.Dispatcher.Invoke(async () =>
            {
                var ret = false;
                try
                {
                    if (!string.IsNullOrEmpty(FileName))
                    {
                        var file = FileName;
                        var files = file.GetFiles();
                        if (files.Count() > 0 && !string.IsNullOrEmpty(file))
                        {
                            var file_n = files.Where(f => f.EndsWith(file, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                            var idx = files.IndexOf(file_n);
                            if (idx < files.Count - 1) ret = await LoadImageFromFile(files[idx + 1]);
                        }
                    }
                }
                catch (Exception ex) { ex.ShowMessage(); }
                return (ret);
            });
            return (result);
        }

        public async Task<bool> LoadImageFromFile(string file)
        {
            var result = false;
            if (File.Exists(file))
            {
                //result = await Application.Current.Dispatcher.InvokeAsync(() =>
                result = await Task.Run(() =>
                {
                    var ret = false;
                    try
                    {
                        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            LastFileName = file;
                            FileName = file;

                            if (Path.GetExtension(file).Equals(".cube", StringComparison.CurrentCultureIgnoreCase))
                            {
                                Original = fs.Lut2Png();
                            }
                            else
                            {
                                try { Original = new MagickImage(fs, Path.GetExtension(file).GetImageFileFormat()); }
                                catch
                                {
                                    if (fs.CanSeek) fs.Seek(0, SeekOrigin.Begin);
                                    Original = new MagickImage(fs, MagickFormat.Unknown);
                                }
                            }

                            ret = true;
                        }
                    }
                    catch (Exception ex) { ex.ShowMessage(); }
                    return (ret);
                });
            }
            return (result);
        }

        public async Task<bool> LoadImageFromFile()
        {
            var result = false;
            try
            {
                var file_str = "AllSupportedImageFiles".T();
                var dlgOpen = new Microsoft.Win32.OpenFileDialog
                {
                    Multiselect = true,
                    CheckFileExists = true,
                    CheckPathExists = true,
                    ValidateNames = true,                 
                    //Filter = $"{file_str}|{AllSupportedFiles}|{AllSupportedFilters}";
                    Filter = $"{file_str}|{Extensions.AllSupportedFiles}"
                };
                if (dlgOpen.ShowDialog() ?? false)
                {
                    var file = dlgOpen.FileName;
                    result = await LoadImageFromFile(file);
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

        public void Save(string file, string ext = ".png", MagickFormat format = MagickFormat.Unknown)
        {
            if (ValidCurrent)
            {
                try
                {
                    var e = Path.GetExtension(file).ToLower();
                    if (string.IsNullOrEmpty(e)) file = $"{file}{ext}";

                    FixDPI();

                    if (e.Equals(".png8", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Current.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                        Current.Write(Path.ChangeExtension(file, ".png"), MagickFormat.Png8);
                    }
                    else
                    {
                        var fmt_no_alpha = new MagickFormat[] { MagickFormat.Jpg, MagickFormat.Jpeg, MagickFormat.Jpe, MagickFormat.Bmp2, MagickFormat.Bmp3 };
                        var ext_no_alpha = new string[] { ".jpg", ".jpeg", ".jpe", ".bmp" };
                        if (Current.HasAlpha && (fmt_no_alpha.Contains(format) || ext_no_alpha.Contains(e)))
                        {
                            var target = Current.Clone();
                            target.ColorAlpha(MasklightColor ?? target.BackgroundColor);
                            target.BackgroundColor = MasklightColor ?? target.BackgroundColor;
                            target.MatteColor = MasklightColor ?? target.BackgroundColor;
                            foreach (var profile in Current.ProfileNames) { if (Current.HasProfile(profile)) target.SetProfile(Current.GetProfile(profile)); }
                            target.Write(file, format);
                        }
                        else
                        {
                            if (format == MagickFormat.Tif || format == MagickFormat.Tiff || format == MagickFormat.Tiff64 || e.Equals(".tif") || e.Equals(".tiff"))
                            {
                                Current.SetCompression(CompressionMethod.Zip);
                                Current.Settings.Compression = CompressionMethod.Zip;
                            }
                            else if (format == MagickFormat.Gif || format == MagickFormat.Gif87 || e.Equals(".gif"))
                            {
                                Current.GifDisposeMethod = GifDisposeMethod.Background;
                                Current.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                            }
                            else if (format == MagickFormat.WebP || format == MagickFormat.WebM || e.Equals(".webp") || e.Equals(".webm"))
                            {
                                Current.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                            }
                            else if (format == MagickFormat.Bmp || e.Equals(".bmp"))
                            {
                                Current.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                            }
                            Current.Write(file, format);
                        }
                    }
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
        }

        public void SaveTopazMask(string file)
        {
            if (ValidCurrent)
            {
                try
                {
                    var format = MagickFormat.Tiff;
                    var fd = Path.GetDirectoryName(file);
                    var fn = Path.GetFileNameWithoutExtension(file);
                    var fe = Path.GetExtension(file);
                    var fm = "-mask";
                    file = fn.ToLower().Contains(fm) ? Path.Combine(fd, $"{fn}.tiff") : Path.Combine(fd, $"{fn}{fm}.tiff");

                    using (var image = new MagickImage(Current) { BackgroundColor = MagickColors.Black, MatteColor = new MagickColor(MasklightColor.R, MasklightColor.G, MasklightColor.B) })
                    {
                        var threshold_color = new MagickColor(HighlightColor.R, HighlightColor.G, HighlightColor.B);
                        FixDPI(image, use_system: true);
                        if (OpMode == ImageOpMode.Compose)
                        {
                            image.Grayscale();
                            image.LevelColors(MagickColors.White, MagickColors.Black);
                            if (LowlightColor.HasAlpha() || LowlightColor == null) image.ColorAlpha(MagickColors.White);
                            image.Threshold(new Percentage(100 - ColorFuzzy.ToDouble() - 5.0));
                        }
                        else if (HighlightColor.HasAlpha() || LowlightColor.HasAlpha() || MasklightColor.HasAlpha())
                        {
                            image.Grayscale();
                            image.ColorAlpha(MagickColors.White);
                            image.LevelColors(MagickColors.Black, MagickColors.White);
                            image.AutoThreshold(AutoThresholdMethod.OTSU);
                        }
                        else
                        {
                            image.ColorThreshold(threshold_color, threshold_color);
                            image.LevelColors(MagickColors.White, MagickColors.Black);
                        }
                        image.Format = format;
                        image.SetCompression(CompressionMethod.Zip);
                        image.Settings.Compression = CompressionMethod.Zip;
                        image.ColorType = ColorType.Palette;
                        image.ColorSpace = ColorSpace.Gray;
                        image.Depth = 8;

                        image.Write(file, format);
                    }
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
        }

        public void Save(bool overwrite = false)
        {
            if (ValidCurrent)
            {
                try
                {
                    if (overwrite && ValidOriginal && !string.IsNullOrEmpty(FileName) && File.Exists(FileName))
                    {
                        Save(FileName, format: Original.Format);
                    }
                    else
                    {
                        var file_str = "File".T();
                        var dlgSave = new Microsoft.Win32.SaveFileDialog
                        {
                            CheckPathExists = true,
                            ValidateNames = true,
                            DefaultExt = ".png",
                            Filter = $"PNG {file_str}| *.png|PNG8 {file_str}| *.png|JPEG {file_str}|*.jpg;*.jpeg|TIFF {file_str}|*.tif;*.tiff|BITMAP {file_str}|*.bmp|BITMAP With Alpha {file_str}|*.bmp|WEBP {file_str}|*.webp|Topaz Mask {file_str}|*.tiff",
                            FilterIndex = 1
                        };
                        if (dlgSave.ShowDialog() ?? false)
                        {
                            var file = dlgSave.FileName;
                            var ext = Path.GetExtension(file);
                            var filters = dlgSave.Filter.Split('|');
                            var filter = filters[(dlgSave.FilterIndex - 1) * 2];
                            if (string.IsNullOrEmpty(ext))
                            {
                                ext = filters[(dlgSave.FilterIndex - 1) * 2 + 1].Replace("*", "");
                                file = $"{file}{ext}";
                            }

                            var fmt = MagickFormat.Unknown;
                            if (filter.StartsWith("BITMAP With Alpha", StringComparison.CurrentCultureIgnoreCase)) fmt = MagickFormat.Bmp;
                            else if (filter.StartsWith("BITMAP", StringComparison.CurrentCultureIgnoreCase)) fmt = MagickFormat.Bmp3;
                            else if (filter.StartsWith("png8", StringComparison.CurrentCultureIgnoreCase)) fmt = MagickFormat.Png8;
                            else if (filter.StartsWith("png", StringComparison.CurrentCultureIgnoreCase)) fmt = MagickFormat.Png;
                            else if (filter.StartsWith("jpeg", StringComparison.CurrentCultureIgnoreCase)) fmt = MagickFormat.Jpeg;
                            else if (filter.StartsWith("tiff", StringComparison.CurrentCultureIgnoreCase)) fmt = MagickFormat.Tiff;
                            else if (filter.StartsWith("webp", StringComparison.CurrentCultureIgnoreCase)) fmt = MagickFormat.WebP;

                            var topaz = filter.StartsWith("Topaz", StringComparison.CurrentCultureIgnoreCase);
                            if (topaz)
                            {
                                SaveTopazMask(file);
                            }
                            else
                            {
                                Save(file, format: fmt);
                            }
                        }
                    }
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
        }

        private MagickFormat GetMagickFormat(string fmt)
        {
            var result = MagickFormat.Unknown;
            switch (fmt)
            {
                case "image/bmp":
                case "image/bitmap":
                case "CF_BITMAP":
                case "CF_DIB":
                case ".bmp":
                    result = MagickFormat.Bmp3;
                    break;
                case "image/gif":
                case "gif":
                case ".gif":
                    result = MagickFormat.Gif;
                    break;
                case "image/png":
                case "png":
                case "PNG":
                case ".png":
                    result = MagickFormat.Png;
                    break;
                case "image/jpg":
                case ".jpg":
                case "image/jpeg":
                case ".jpeg":
                    result = MagickFormat.Jpeg;
                    break;
                case "image/tif":
                case ".tif":
                case "image/tiff":
                case ".tiff":
                    result = MagickFormat.Tiff;
                    break;
                default:
                    result = MagickFormat.Unknown;
                    break;
            }
            return (result);
        }

        private void GetProfiles()
        {
            if (ValidOriginal)
            {
                foreach (var profile in Original.ProfileNames) { if (Original.HasProfile(profile)) Profiles[profile] = Original.GetProfile(profile); }
                foreach (var attr in Original.AttributeNames) { Attributes[attr] = Original.GetAttribute(attr); }
            }
            else if (ValidCurrent)
            {
                foreach (var profile in Current.ProfileNames) { if (Current.HasProfile(profile)) Profiles[profile] = Current.GetProfile(profile); }
                foreach (var attr in Current.AttributeNames) { Attributes[attr] = Current.GetAttribute(attr); }
            }
        }

        public async void Denoise(int? order = null, bool more = false)
        {
            if (ValidCurrent)
            {
                var value = order ?? 0;

                var factor = DenoiseLevel <= 0 ? 1 : DenoiseLevel;
                if (factor <= 1)
                {
                    if (DenoiseCount > 10) factor = 2;
                    else if (DenoiseCount > 20) factor = 4;
                    else if (DenoiseCount > 50) factor = 8;
                    else if (DenoiseCount > 100) factor = 16;
                }

                Current.MedianFilter((value > 0 ? value : 3) * (more ? factor * 4 : factor));
                await SetImage();
            }
        }

        public async void CopyToClipboard()
        {
            if (ValidCurrent)
            {
                await Task.Run(async () => 
                {
                    try
                    {
                        var bs = Current.ToBitmapSource();
                        //Current.ToByteArray(MagickFormat.Dib);

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
                                byte[] arr = Current.ToByteArray(MagickFormat.Bmp3);// await bs.ToBytes(fmt);
                                byte[] dib = arr.Skip(14).ToArray();
                                ms = new MemoryStream(dib);
                                dataPackage.SetData(fmt, ms);
                                await ms.FlushAsync();
                            }
                            else
                            {
                                var mfmt = GetMagickFormat(fmt);
                                if (mfmt != MagickFormat.Unknown)
                                {
                                    byte[] arr = Current.ToByteArray(mfmt); //await bs.ToBytes(fmt);
                                    ms = new MemoryStream(arr);
                                    dataPackage.SetData(fmt, ms);
                                    await ms.FlushAsync();
                                }
                            }
                        }
                        #endregion
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            Clipboard.SetDataObject(dataPackage, true);
                        });
                    }
                    catch (Exception ex) { ex.ShowMessage(); }
                });

            }
        }

        public async Task<bool> SetImage()
        {
            var result = false;
            result = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var ret = false;
                try
                {
                    if (ValidCurrent && Tagetment is Image)
                    {
                        (Tagetment as Image).Source = Source;
                        ret = true;
                    }
                }
                catch (Exception ex) { ex.ShowMessage(); }
                return (ret);
            });
            return (result);
        }

        public void ChangeColorSpace(bool force)
        {
            if (ValidCurrent && ValidOriginal)
            {
                if (Current.ColorSpace != Original.ColorSpace) _last_colorspace_ = Current.ColorSpace;
                var color = force ? ColorSpace.sRGB : _last_colorspace_;// Original.ColorSpace;
                if (color != Current.ColorSpace) Current.ColorSpace = color;
            }
        }

        public async Task<string> GetTotalColors()
        {
            var colors = new List<string>();
            if (ValidOriginal)
            {
                colors.Add($"{"InfoTipColorsOriginal".T()} {await Original.CalcTotalColors()}");
                colors.Add($"{"InfoTipBackgroundColorOriginal".T()} {Original.BackgroundColor.ToHexString()}");
                colors.Add($"{"InfoTipBorderColorOriginal".T()} {Original.BorderColor.ToHexString()}");
                colors.Add($"{"InfoTipMatteColorOriginal".T()} {Original.MatteColor.ToHexString()}");
            }
            if (ValidCurrent)
            {
                colors.Add($"{"InfoTipColors".T()} {await Current.CalcTotalColors()}");
                colors.Add($"{"InfoTipBackgroundColor".T()} {Current.BackgroundColor.ToHexString()}");
                colors.Add($"{"InfoTipBorderColor".T()} {Current.BorderColor.ToHexString()}");
                colors.Add($"{"InfoTipMatteColor".T()} {Current.MatteColor.ToHexString()}");
            }
            return (colors.Count > 0 ? string.Join(Environment.NewLine, colors) : string.Empty);
        }

        public string TextPadding(string text, string label, int offset = 0, char padding_char = ' ')
        {
            var count = Encoding.ASCII.GetByteCount(label);
            return (TextPadding(text, count, offset, padding_char));
        }

        public string TextPadding(string text, int count, int offset = 0, char padding = ' ')
        {
            return (Regex.Replace(text, @"(\n\r|\r\n|\n|\r)", $"{Environment.NewLine}{" ".PadLeft(count + offset, padding)}", RegexOptions.IgnoreCase));
        }

        public Endian DetectFileEndian(string file)
        {
            var result = Endian.Undefined;
            if (File.Exists(file))
            {
                var exif = new CompactExifLib.ExifData(file);
                if (exif is CompactExifLib.ExifData)
                {
                    if (exif.ByteOrder == CompactExifLib.ExifByteOrder.BigEndian) result = Endian.MSB;
                    else if (exif.ByteOrder == CompactExifLib.ExifByteOrder.LittleEndian) result = Endian.LSB;
                }
            }
            return (result);
        }

        public int CalcColorDepth(MagickImage image)
        {
            var result = 0;
            if (image is MagickImage)
            {
                result = image.Depth * image.ChannelCount;
                if (image.ColorType == ColorType.Bilevel) result = 2;
                else if (image.ColorType == ColorType.Grayscale) result = 8;
                else if (image.ColorType == ColorType.GrayscaleAlpha) result = 8 + 8;
                else if (image.ColorType == ColorType.Palette) result = (int)Math.Ceiling(Math.Log(image.ColormapSize, 2));
                else if (image.ColorType == ColorType.PaletteAlpha) result = (int)Math.Ceiling(Math.Log(image.ColormapSize, 2)) + 8;
                else if (image.ColorType == ColorType.TrueColor) result = 24;
                else if (image.ColorType == ColorType.TrueColorAlpha) result = 32;
                else if (image.ColorType == ColorType.ColorSeparation) result = 24;
                else if (image.ColorType == ColorType.ColorSeparationAlpha) result = 32;
            }
            return (result);
        }

        public async Task<string> GetImageInfo(bool include_colorinfo = false)
        {
            string result = string.Empty;
            try
            {
                if (ValidCurrent)
                {
                    var st = Stopwatch.StartNew();

                    var DPI_TEXT = string.Empty;
                    if (Current.Density == null || Current.Density.X <= 0 || Current.Density.Y <= 0)
                    {
                        var dpi = Application.Current.GetSystemDPI();
                        Current.Density = new Density(dpi.X, dpi.Y, DensityUnit.PixelsPerInch);
                    }
                    var DPI_UNIT = Current.Density.Units == DensityUnit.PixelsPerCentimeter ? "PPC" : (Current.Density.Units == DensityUnit.PixelsPerInch ? "PPI" : string.Empty);
                    DPI_TEXT = DPI_UNIT.Equals("PPC") ? $"{Current.Density.X:F2} {DPI_UNIT} x {Current.Density.Y:F2} {DPI_UNIT}" : $"{Current.Density.X:F0} {DPI_UNIT} x {Current.Density.Y:F0} {DPI_UNIT}";
                    if (Current.Density.Units != DensityUnit.PixelsPerInch)
                    {
                        var dpi = Current.Density.ChangeUnits(DensityUnit.PixelsPerInch);
                        var dpi_text = $"{dpi.X:F0} PPI x {dpi.Y:F0} PPI";
                        DPI_TEXT = $"{DPI_TEXT} [{dpi_text}]";
                    }

                    var original_depth = CalcColorDepth(Original);
                    var current_depth = CalcColorDepth(Current);

                    var tip = new List<string>
                    {
                        $"{"InfoTipDimentionOriginal".T()} {OriginalSize.Width:F0}x{OriginalSize.Height:F0}x{original_depth:F0}, {(long)OriginalSize.Width * OriginalSize.Height / 1000000:F2}MP",
                        $"{"InfoTipDimention".T()} {CurrentSize.Width:F0}x{CurrentSize.Height:F0}x{current_depth:F0}, {(long)CurrentSize.Width * CurrentSize.Height / 1000000:F2}MP"
                    };
                    if (Current.BoundingBox != null) tip.Add($"{"InfoTipBounding".T()} {Current.BoundingBox.Width:F0}x{Current.BoundingBox.Height:F0}");
                    tip.Add($"{"InfoTipOrientation".T()} {Original.Orientation}");
                    tip.Add($"{"InfoTipResolution".T()} {DPI_TEXT}");

                    if (include_colorinfo) tip.Add(await GetTotalColors());

                    if (Current.AttributeNames != null)
                    {
                        var fi = OriginalIsFile ? new FileInfo(FileName) : null;
                        if (fi is FileInfo && fi.Exists)
                        {
                            if (Original.Endian == Endian.Undefined) Original.Endian = DetectFileEndian(fi.FullName);
                            if (Current.Endian == Endian.Undefined) Current.Endian = Original.Endian;
                        }
                        else if (Type == ImageType.Result && Current.Endian == Endian.Undefined)
                            Current.Endian = BitConverter.IsLittleEndian ? Endian.LSB : Endian.MSB;

                        if (Current.ArtifactNames.Count() > 0)
                        {
                            tip.Add($"{"InfoTipArtifacts".T()}");
                            var artifacts = new List<string>();
                            foreach (var artifact in Current.ArtifactNames)
                            {
                                var label = artifact.PadRight(32, ' ');
                                var value = Current.GetArtifact(artifact);
                                if (string.IsNullOrEmpty(value)) continue;
                                artifacts.Add($"  {label}= {TextPadding(value, label, 4)}");
                            }
                            tip.AddRange(artifacts.OrderBy(a => a));
                        }

                        var exif = Current.HasProfile("exif") ? Current.GetExifProfile() : new ExifProfile();
                        tip.Add($"{"InfoTipAttributes".T()}");
                        var attrs = new List<string>();
                        var tags = new Dictionary<string, IExifValue>();
                        foreach (var tv in exif.Values)
                        {
                            tags[$"exif:{tv.Tag}"] = tv;
                        }
                        foreach (var attr in Current.AttributeNames.Union(new string[] { "exif:Rating", "exif:RatingPercent" }).Union(tags.Keys))
                        {
                            try
                            {
                                var label = attr.PadRight(32, ' ');
                                var value = Current.GetAttribute(attr);
                                if (string.IsNullOrEmpty(value) && !attr.Contains("Rating") && !tags.Keys.Contains(attr)) continue;
                                if (attr.Contains("WinXP")) value = Current.GetAttributes(attr);
                                else if (attr.StartsWith("date:", StringComparison.CurrentCultureIgnoreCase))
                                {
                                    var d = DateTime.Now;
                                    if (fi is FileInfo && attr.EndsWith(":create", StringComparison.CurrentCultureIgnoreCase))
                                        value = fi.CreationTime.ToLocalTime().ToString();
                                    else if (fi is FileInfo && attr.EndsWith(":modify", StringComparison.CurrentCultureIgnoreCase))
                                        value = fi.LastWriteTime.ToLocalTime().ToString();
                                    else if (DateTime.TryParse(value, out d))
                                        value = d.ToLocalTime().ToString();
                                }
                                else if (attr.Equals("exif:Artist"))
                                    value = exif.GetValue(ExifTag.Artist) != null ? exif.GetValue(ExifTag.Artist).Value : value;
                                else if (attr.Equals("exif:Copyright"))
                                    value = exif.GetValue(ExifTag.Copyright) != null ? exif.GetValue(ExifTag.Copyright).Value : value;
                                else if (attr.Equals("exif:ExifVersion"))
                                    value = exif.GetValue(ExifTag.ExifVersion) != null ? Encoding.UTF8.GetString(exif.GetValue(ExifTag.ExifVersion).Value) : value;
                                else if (attr.Equals("exif:ImageDescription"))
                                    value = exif.GetValue(ExifTag.ImageDescription) != null ? exif.GetValue(ExifTag.ImageDescription).Value : value;
                                else if (attr.Equals("exif:UserComment") && exif.GetValue(ExifTag.UserComment) != null)
                                    value = Current.GetAttributes(attr);
                                else if (attr.Equals("exif:ExtensibleMetadataPlatform") || attr.Equals("exif:XmpMetadata"))
                                {
                                    var xmp = exif.GetValue(ExifTag.XMP);
                                    if (xmp != null && xmp.IsArray) value = Regex.Replace(Encoding.UTF8.GetString(xmp.Value), @"(\n\r|\r\n|\n|\r|\t)", "", RegexOptions.IgnoreCase);
                                }
                                else if (attr.Equals("exif:Rating"))
                                {
                                    foreach (var tag in exif.Values.Where(v => v.Tag.Equals(ExifTag.Rating))) { value = tag.GetValue().ToString(); }
                                }
                                else if (attr.Equals("exif:RatingPercent"))
                                {
                                    foreach (var tag in exif.Values.Where(v => v.Tag.Equals(ExifTag.RatingPercent))) { value = tag.GetValue().ToString(); }
                                }
                                else if (attr.StartsWith("exif:GPSVersionID") && tags.ContainsKey(attr))
                                {
                                    var tag = tags[attr];
                                    var v = (byte[])tag.GetValue();
                                    value = string.Join("", v.Select(b => $"{b}"));
                                }
                                else if (attr.StartsWith("exif:GPS") && attr.Contains("itude"))
                                {
                                    var tag = exif.Values.Where(v => v.Tag.ToString().Equals(attr.Substring(5))).FirstOrDefault();
                                    if (tag is IExifValue)
                                    {
                                        if (attr.EndsWith("Ref"))
                                            value = tag.GetValue().ToString().Trim('\0');
                                        else
                                        {
                                            var arv = tag.GetValue() as Rational[];
                                            value = $"{arv[0].Numerator / arv[0].Denominator:F0}.{arv[1].Numerator / arv[1].Denominator:F0}'{arv[2].Numerator / (double)arv[2].Denominator}\"";
                                        }
                                    }
                                }
                                else if (attr.Equals("exif:59932")) { value = "Padding"; continue; }
                                else if (value != null && string.IsNullOrEmpty(value.Trim('.')) && tags.ContainsKey(attr) && !attr.StartsWith("exif:XP") && !attr.StartsWith("exif:XMP"))
                                {
                                    var tag = tags[attr];
                                    if (tag.DataType == ExifDataType.Short)
                                    {
                                        if (tag.IsArray) value = string.Join(", ", ((short[])tag.GetValue()).Select(s => $"{s}"));
                                        else value = $"{(short)tag.GetValue()}";
                                    }
                                    else if (tag.DataType == ExifDataType.Undefined)
                                    {
                                        if (tag.IsArray)
                                        {
                                            var v = (byte[])tag.GetValue();
                                            value = string.Join(",", v);
                                        }
                                        else value = $"{(byte)tag.GetValue()}";
                                    }
                                    else value = tag.ToString();
                                }
                                else if (tags.ContainsKey(attr) && value == null)
                                {
                                    var tag = tags[attr];
                                    if (tag.IsArray)
                                    {
                                        var rv = new List<double>();
                                        if (tag.DataType == ExifDataType.Rational)
                                        {
                                            foreach (var v in (Rational[])tag.GetValue())
                                            {
                                                rv.Add(v.Numerator / (double)v.Denominator);
                                            }
                                            if (rv.Count > 0) value = string.Join(", ", rv);
                                        }
                                        else if (tag.DataType == ExifDataType.SignedRational)
                                        {
                                            foreach (var v in (SignedRational[])tag.GetValue())
                                            {
                                                rv.Add(v.Numerator / (double)v.Denominator);
                                            }
                                            if (rv.Count > 0) value = string.Join(", ", rv);
                                        }
                                        else if (tag.DataType == ExifDataType.Short)
                                        {
                                            foreach (var v in (ushort[])tag.GetValue())
                                            {
                                                rv.Add(v);
                                            }
                                            if (rv.Count > 0) value = string.Join(", ", rv);
                                        }
                                        else if (tag.DataType == ExifDataType.SignedShort)
                                        {
                                            foreach (var v in (short[])tag.GetValue())
                                            {
                                                rv.Add(v);
                                            }
                                            if (rv.Count > 0) value = string.Join(", ", rv);
                                        }
                                    }
                                    else
                                    {
                                        if (tag.DataType == ExifDataType.Rational)
                                        {
                                            var rv = (Rational)tag.GetValue();
                                            value = $"{rv.Numerator / (double)rv.Denominator}";
                                        }
                                        else if (tag.DataType == ExifDataType.SignedRational)
                                        {
                                            var rv = (SignedRational)tag.GetValue();
                                            value = $"{rv.Numerator / (double)rv.Denominator}";
                                        }
                                        else if (tag.DataType == ExifDataType.Short)
                                        {
                                            value = $"{(ushort)tag.GetValue()}";
                                        }
                                        else if (tag.DataType == ExifDataType.SignedShort)
                                        {
                                            value = $"{(short)tag.GetValue()}";
                                        }
                                    }
                                }
                                if (string.IsNullOrEmpty(value)) continue;
                                if (attr.EndsWith("Keywords") || attr.EndsWith("Author") || attr.EndsWith("Artist") || attr.EndsWith("Copyright") || attr.EndsWith("Copyrights"))
                                {
                                    var keywords = value.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim()).ToList();
                                    for (var i = 5; i < keywords.Count; i += 5) { keywords[i] = $"{Environment.NewLine}{keywords[i]}"; }
                                    value = string.Join("; ", keywords) + ';';
                                }
                                else if (value.Length > 64) value = $"{value.Substring(0, 64)} ...";
                                attrs.Add($"  {label}= {TextPadding(value, label, 4)}");
                            }
                            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, $"{attr} : {ex.Message}"); }
                        }
                        tip.AddRange(attrs.OrderBy(a => a));
                    }
                    if (OriginalFormatInfo != null)
                        tip.Add($"{"InfoTipFormatInfo".T()} {OriginalFormatInfo.Format} ({OriginalFormatInfo.Description}), mime:{OriginalFormatInfo.MimeType}");
                    else if (CurrentFormatInfo != null)
                        tip.Add($"{"InfoTipFormatInfo".T()} {CurrentFormatInfo.Format} ({CurrentFormatInfo.Description}), mime:{CurrentFormatInfo.MimeType}");
                    tip.Add($"{"InfoTipColorSpace".T()} {Original.ColorSpace}");
                    tip.Add($"{"InfoTipColorChannelCount".T()} {ChannelCount}");
                    tip.Add($"{"InfoTipHasAlpha".T()} {(Original.HasAlpha ? "Included" : "NotIncluded").T()}");
                    if (Original.ColormapSize > 0) tip.Add($"{"InfoTipColorMapsSize".T()} {Original.ColormapSize}");
                    tip.Add($"{"InfoTipCompression".T()} {Original.Compression}");
                    tip.Add($"{"InfoTipQuality".T()} {(Original.Compression == CompressionMethod.JPEG || Original.Quality > 0 ? $"{Original.Quality}" : "Unknown")}");
                    tip.Add($"{"InfoTipMemoryMode".T()} {MemoryUsageMode}");
                    tip.Add($"{"InfoTipIdealMemoryUsage".T()} {(ValidOriginal ? OriginalIdealMemoryUsage.SmartFileSize() : CurrentIdealMemoryUsage.SmartFileSize())}");
                    tip.Add($"{"InfoTipMemoryUsage".T()} {(ValidOriginal ? OriginalRealMemoryUsage.SmartFileSize() : CurrentRealMemoryUsage.SmartFileSize())}");
                    tip.Add($"{"InfoTipDisplayMemory".T()} {CurrentDisplayMemoryUsage.SmartFileSize()}");
                    if (!string.IsNullOrEmpty(FileName))
                    {
                        var FileSize = !string.IsNullOrEmpty(FileName) && File.Exists(FileName) ? new FileInfo(FileName).Length : -1;
                        tip.Add($"{"InfoTipFileSize".T()} {FileSize.SmartFileSize()}, {Original.Endian}");
                        tip.Add($"{"InfoTipFileName".T()} {FileName}");
                    }
                    else if (ValidOriginal && !string.IsNullOrEmpty(Original.FileName))
                    {
                        var FileSize = !string.IsNullOrEmpty(Original.FileName) && File.Exists(Original.FileName) ? new FileInfo(Original.FileName).Length : -1;
                        tip.Add($"{"InfoTipFileSize".T()} {FileSize.SmartFileSize()}, {Original.Endian}");
                        tip.Add($"{"InfoTipFileName".T()} {Original.FileName}");
                    }
                    else if (!string.IsNullOrEmpty(Current.FileName))
                    {
                        var FileSize = !string.IsNullOrEmpty(Current.FileName) && File.Exists(Current.FileName) ? new FileInfo(Current.FileName).Length : -1;
                        tip.Add($"{"InfoTipFileSize".T()} {FileSize.SmartFileSize()}");
                        tip.Add($"{"InfoTipFileName".T()} {Current.FileName}");
                    }
                    result = string.Join(Environment.NewLine, tip);
                    st.Stop();
#if DEBUG
                    Debug.WriteLine($"{TimeSpan.FromTicks(st.ElapsedTicks).TotalSeconds:F4}s");
#endif
                    Current.GetExif();
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (string.IsNullOrEmpty(result) ? null : result);
        }

        public bool ResetTransform()
        {
            CurrentModified = false;
            if (ValidCurrent)
            {
                if (FlipX)
                {
                    Current.Flop();
                    FlipX = false;
                    CurrentModified = true;
                }
                if (FlipY)
                {
                    Current.Flip();
                    FlipY = false;
                    CurrentModified = true;
                }
                if (Rotated % 360 != 0)
                {
                    Current.Rotate(-Rotated);
                    Rotated = 0;
                    CurrentModified = true;
                }
            }
            else
            {
                FlipX = false;
                FlipY = false;
                Rotated = 0;
                CurrentModified = true;
            }
            return (CurrentModified);
        }

        public async Task<bool> Reset(int size = -1)
        {
            return (await Reload(size, reload: false, reset: true));
        }

        public async Task<bool> Reload(bool reload = false, bool reset = false)
        {
            var result = false;
            try
            {
                if (ValidOriginal)
                {
                    if (reload && !string.IsNullOrEmpty(LastFileName)) await LoadImageFromFile(LastFileName);
                    if (OriginalModified || (ValidOriginal && !ValidCurrent) || (reset && ValidOriginal))
                    {
                        if (ValidCurrent) { Current.Dispose(); Current = null; }
                        Current = new MagickImage(Original);
                    }
                    if (ValidCurrent)
                    {
                        FlipX = false;
                        FlipY = false;
                        Rotated = 0;
                        ResetTransform();
                        if (CurrentGeometry is MagickGeometry)
                        {
                            Current.AdaptiveResize(CurrentGeometry);
                            Current.RePage();
                        }
                        _basesize_ = new Size(Current.Width, Current.Height);
                        _last_colorspace_ = Current.ColorSpace;
                    }
                    result = true;
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

        public async Task<bool> Reload(MagickGeometry geo, bool reload = false, bool reset = false)
        {
            var result = false;
            try
            {
                if (ValidOriginal)
                {
                    if (reload && !string.IsNullOrEmpty(LastFileName)) await LoadImageFromFile(LastFileName);
                    if (OriginalModified || (ValidOriginal && !ValidCurrent) || (reset && ValidOriginal))
                    {
                        if (ValidCurrent) { Current.Dispose(); Current = null; }
                        Current = new MagickImage(Original);
                    }
                    if (ValidCurrent)
                    {
                        FlipX = false;
                        FlipY = false;
                        Rotated = 0;
                        ResetTransform();
                        if (geo is MagickGeometry)
                        {
                            Current.AdaptiveResize(geo);
                            Current.RePage();
                        }
                        _basesize_ = new Size(Current.Width, Current.Height);
                        _last_colorspace_ = Current.ColorSpace;
                    }
                    result = true;
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

        public async Task<bool> Reload(int size, bool reload = false, bool reset = false)
        {
            if (size <= 0) return (await Reload(reload, reset));
            else return (await Reload(new MagickGeometry($"{size}x{size}>"), reload, reset));
        }

        public void Dispose()
        {
            if (ValidCurrent) { Current.Dispose(); Current = null; FileName = string.Empty; }
            if (ValidOriginal) { Original.Dispose(); Original = null; FileName = string.Empty; }
            ResetTransform();
        }
    }
}
