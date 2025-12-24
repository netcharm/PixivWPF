using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using ImageMagick;

namespace ImageViewer
{
#pragma warning disable IDE0079
#pragma warning disable IDE0039
#pragma warning disable IDE0060

    public enum ImageTarget { None, Source, Target, Result, All };
    public enum ListPosition { Current, First, Prev, Next, Last };

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
        public FilterType ResizeFilter { get; set; } = FilterType.Lanczos2Sharp;
        public bool AutoRotate { get; set; } = true;

        private MagickImage _original_ = null;
        public MagickImage Original
        {
            get { return (_original_); }
            set
            {
                CancelGetInfo?.Cancel();
                if (_original_ is not null) { _original_.Dispose(); _original_ = null; }
                _original_ = new MagickImage(value);
                FixDPI(_original_);
                _OriginalModified_ = true;
                DenoiseCount = 0;
                DenoiseLevel = 0;
                if (ValidOriginal)
                {
                    AutoRotate = Application.Current?.GetMainWindow()?.AutoRotate ?? AutoRotate;
                    if (AutoRotate) _original_.AutoOrient();
                    _original_thumb_ = CreateThumbnail(_original_, ThumbSize);
                    _original_?.FilterType = ResizeFilter;
                    Dispatcher.CurrentDispatcher.Invoke(async () => { await Reload(); });
                }
                GetProfiles();
            }
        }
        private MagickImage _original_thumb_ = null;
        public Size ThumbSize { get; set; } = new Size(250, 250);
        public MagickImage Thumbnail { get { return (_original_thumb_); } }
        public Size OriginalSize { get { return (ValidOriginal ? new Size((double)(Original?.Width), (double)(Original?.Height)) : new Size(0, 0)); } }
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
        public uint OriginalQuality => Original?.Quality() ?? 0;
        public uint OriginalDepth = 0;
        public Endian OriginalEndian = Endian.Undefined;
        public int Rating 
        { 
            get 
            {
                var result = -1;
                if (ValidOriginal)
                {
                    try
                    {
                        var exif = Original?.GetExifProfile();
                        if (exif is ExifProfile)
                        {
                            var tag_rating = new ExifTag[] { ExifTag.Rating, ExifTag.RatingPercent };
                            foreach (var tag in exif?.Values?.Where(v => tag_rating.Contains(v.Tag)))
                            {
                                if (tag.Tag == ExifTag.Rating) { result = (ushort)tag.GetValue(); break; }
                                else if (tag.Tag == ExifTag.RatingPercent )
                                {
                                    var rating_value = (ushort)tag.GetValue();
                                    if (rating_value >= 99) result = 5;
                                    else if (rating_value >= 75) result = 4;
                                    else if (rating_value >= 50) result = 3;
                                    else if (rating_value >= 25) result = 2;
                                    else if (rating_value >= 01) result = 1;
                                    else if (rating_value <= 00) result = 0;
                                    break;
                                }
                            }
                        }
                        else if (Original?.AttributeNames.Contains("xmp:Rating") ?? false)
                        {
                            var rating = Original?.GetAttribute("xmp:Rating");
                            if (int.TryParse(rating, out int rating_value)) result = rating_value;
                        }
                        else if (Original?.AttributeNames.Contains("MicrosoftPhoto:Rating") ?? false)
                        {
                            var rating = Original?.GetAttribute("MicrosoftPhoto:Rating");
                            if (int.TryParse(rating, out int rating_value))
                            {
                                if      (rating_value >= 99) result = 5;
                                else if (rating_value >= 75) result = 4;
                                else if (rating_value >= 50) result = 3;
                                else if (rating_value >= 25) result = 2;
                                else if (rating_value >= 01) result = 1;
                                else if (rating_value <= 00) result = 0;
                            }
                        }
                    }
                    catch { }
                }
                return (result);
            } 
        }
        
        private string _simple_info_ = string.Empty;
        public string SimpleInfo { get { return (_simple_info_); } }

        public MagickImage Current
        {
            get { return (_original_); }
            set
            {
                CancelGetInfo?.Cancel();
                if (_original_ is not null) { _original_?.Dispose(); _original_ = null; }
                if (value != null)
                {
                    _original_ = new MagickImage(value);
                    _CurrentModified_ = true;
                    if (_original_ is not null)
                    {
                        _original_?.FilterType = ResizeFilter;
                        if (ValidOriginal && !string.IsNullOrEmpty(FileName))
                        {
                            if (_original_.Endian == Endian.Undefined) _original_.Endian = DetectFileEndian(FileName);
                        }
                    }
                    GetProfiles();
                }
            }
        }
        public Size CurrentSize { get { return (ValidCurrent ? new Size(Current?.Width ?? 0, Current?.Height ?? 0) : new Size(0, 0)); } }
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
        public IMagickFormatInfo CurrentFormatInfo { get { return (ValidCurrent ? MagickFormatInfo.Create(_original_?.Format ?? MagickFormat.Unknown) : null); } }
        public uint CurrentQuality => Current?.Quality() ?? 0;

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
        public Percentage ColorFuzzy = new();

        public uint ChannelCount { get { return (ValidOriginal ? Original.ChannelCount : (ValidCurrent ? Current.ChannelCount : 0)); } }
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

        private Size _basesize_ = new(0, 0);
        public Size BaseSize { get { return (ValidCurrent ? _basesize_ : new Size(0, 0)); } }

        private ColorSpace _last_colorspace_ = ColorSpace.Undefined;

        public SourceParam SourceParams { get; set; } = new SourceParam();
        public BitmapSource Source
        {
            get
            {
                BitmapSource result = null;
                if (ValidCurrent)
                {
                    try
                    {
                        var image = new MagickImage(Current) { FilterType = ResizeFilter };
                        //result = image.ToBitmapSourceWithDensity();
                        result = image.ToBitmapSource();
                        image.Dispose();
                    }
                    catch (AccessViolationException) { }
                    catch (Exception ex) { ex.ShowMessage(); }
                }
                return (result);
            }
        }

        public bool ValidCurrent { get { return (Current?.IsValidRead() ?? false); } }
        public bool ValidOriginal { get { return (Original?.IsValidRead() ?? false); } }

        public bool OriginalIsFile { get { return (!string.IsNullOrEmpty(FileName) && File.Exists(LastFileName)); } }

        private bool ValidImage(MagickImage image)
        {
            return (image is not null);
        }

        public FrameworkElement Tagetment { get; set; } = null;
        public string TagetmentTooltip { get; set; } = null;

        private string LastFileName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        private FileInfo ImageFileInfo { get; set; } = null;

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

        public Dictionary<string, IImageProfile> Profiles { get; set; } = [];
        public Dictionary<string, string> Attributes { get; set; } = [];

        public bool FlipX { get; set; } = false;
        public bool FlipY { get; set; } = false;
        public double Rotated { get; set; } = .0;

        public bool IsFliped { get { return (FlipX || FlipY); } }
        public bool IsRotated { get { return (Rotated % 180 != 0); } }
        public bool IsTransformed { get { return (IsRotated || IsFliped); } }

        public uint DenoiseCount { get; set; } = 0;
        public uint DenoiseLevel { get; set; } = 0;

        public MagickGeometry CurrentGeometry { get; internal set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="image"></param>
        /// <param name="use_system"></param>
        public void FixDPI(MagickImage image = null, bool use_system = false)
        {
            image ??= Current ?? Original ?? null;
            if (ValidImage(image))
            {
                var dpi = Application.Current.GetSystemDPI();
                if (image?.Density?.X > 0 && image?.Density?.Y > 0 && image?.Density.Units != DensityUnit.PixelsPerInch)
                {
                    var unit = image?.Density?.ChangeUnits(DensityUnit.PixelsPerInch);
                    if (use_system || unit.X <= 0 || unit.Y <= 0)
                        image.Density = new Density(dpi.X, dpi.Y, DensityUnit.PixelsPerInch);
                    else
                        image.Density = new Density(Math.Round(unit.X), Math.Round(unit.Y), DensityUnit.PixelsPerInch);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public string GetDPI(MagickImage image = null)
        {
            var result = string.Empty;
            if (image.IsValidRead())
            {
                if (image?.Density == null || image?.Density?.X <= 0 || image?.Density?.Y <= 0)
                {
                    if (CancelGetInfo.IsCancellationRequested) return (result);
                    var dpi = Application.Current.GetSystemDPI();
                    image.Density = new Density(dpi.X, dpi.Y, DensityUnit.PixelsPerInch);
                }

                var DPI_UNIT = image?.Density?.Units == DensityUnit.PixelsPerCentimeter ? " PPC" : (image?.Density?.Units == DensityUnit.PixelsPerInch ? " PPI" : string.Empty);
                var DPI_TEXT = DPI_UNIT.Equals(" PPC") ? $"{image?.Density?.X:F2}{DPI_UNIT} x {image?.Density?.Y:F2}{DPI_UNIT}" : $"{image?.Density?.X:F0}x{image?.Density?.Y:F0}{DPI_UNIT}";
                if (image?.Density?.Units != DensityUnit.PixelsPerInch)
                {
                    var dpi = image?.Density?.ChangeUnits(DensityUnit.PixelsPerInch);
                    var dpi_text = $"{dpi.X:F0}x{dpi.Y:F0} PPI";
                    DPI_TEXT = $"{DPI_TEXT} [{dpi_text}]";
                }
                result = DPI_TEXT;
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string GetOriginalDPI()
        {
            return (GetDPI(Original));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string GetCurrentDPI()
        {
            return (GetDPI(Current));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="force"></param>
        public void ChangeColorSpace(bool force)
        {
            if (ValidCurrent && ValidOriginal)
            {
                if (Current.ColorSpace != Original.ColorSpace) _last_colorspace_ = Current.ColorSpace;
                var color = force ? ColorSpace.sRGB : _last_colorspace_;// Original.ColorSpace;
                if (color != Current.ColorSpace) Current.ColorSpace = color;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public Endian DetectFileEndian(string file)
        {
            var result = Endian.Undefined;
            if (File.Exists(file))
            {
                var exif = new CompactExifLib.ExifData(file);
                if (exif is not null)
                {
                    if (exif.ByteOrder == CompactExifLib.ExifByteOrder.BigEndian) result = Endian.MSB;
                    else if (exif.ByteOrder == CompactExifLib.ExifByteOrder.LittleEndian) result = Endian.LSB;
                }
            }
            return (result);
        }

        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public Endian DetectFileEndian(Stream stream)
        {
            var result = Endian.Undefined;
            if (stream is not null && stream.CanRead && stream.CanSeek)
            {
                var exif = new CompactExifLib.ExifData(stream);
                if (exif is not null)
                {
                    if (exif.ByteOrder == CompactExifLib.ExifByteOrder.BigEndian) result = Endian.MSB;
                    else if (exif.ByteOrder == CompactExifLib.ExifByteOrder.LittleEndian) result = Endian.LSB;
                }
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="endian"></param>
        public void FixEndian(MagickImage image, Endian endian)
        {
            if (image.Endian == Endian.Undefined && endian != Endian.Undefined) image.Endian = endian;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public MagickImage CreateThumbnail(MagickImage image, double size = 0.1f)
        {
            MagickImage result = null;
            if (image.IsValidRead() && size > 0 && size <= 1)
            {
                try
                {
                    result = new MagickImage(image);
                    result.Thumbnail(new Percentage(size * 100));
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public MagickImage CreateThumbnail(MagickImage image, Percentage size)
        {
            MagickImage result = null;
            if (image.IsValidRead() && size > new Percentage(0))
            {
                try
                {
                    result = new MagickImage(image);
                    result.Thumbnail(size);
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public MagickImage CreateThumbnail(MagickImage image, uint size = 250)
        {
            MagickImage result = null;
            if (image.IsValidRead() && size > 0 && size < image.Width && size < image.Height)
            {
                try
                {
                    result = new MagickImage(image);
                    result.Thumbnail(size, size);
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public MagickImage CreateThumbnail(MagickImage image, Size size)
        {
            MagickImage result = null;
            if (image.IsValidRead() && size.Width > 0 && size.Height > 0 && size.Width < image.Width && size.Height < image.Height)
            {
                try
                {
                    result = new MagickImage(image);
                    result.Thumbnail(new MagickGeometry($"{size.Width}x{size.Height}>"));
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public MagickImage CreateThumbnail(MagickImage image, MagickGeometry size)
        {
            MagickImage result = null;
            if (image.IsValidRead())
            {
                try
                {
                    result = new MagickImage(image);
                    result.Thumbnail(size);
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public uint CalcColorDepth(MagickImage image)
        {
            uint result = 0;
            if (image is not null)
            {
                result = image.Depth * image.ChannelCount;
                if (image.ColorType == ColorType.Bilevel) result = 2;
                else if (image.ColorType == ColorType.Grayscale) result = 8;
                else if (image.ColorType == ColorType.GrayscaleAlpha) result = 8 + 8;
                else if (image.ColorType == ColorType.Palette) result = (uint)Math.Ceiling(Math.Log(image.ColormapSize, 2));
                else if (image.ColorType == ColorType.PaletteAlpha) result = (uint)Math.Ceiling(Math.Log(image.ColormapSize, 2)) + 8;
                else if (image.ColorType == ColorType.TrueColor) result = 24;
                else if (image.ColorType == ColorType.TrueColorAlpha) result = 32;
                else if (image.ColorType == ColorType.ColorSeparation) result = 24;
                else if (image.ColorType == ColorType.ColorSeparationAlpha) result = 32;
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        private void GetProfiles()
        {
            try
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
            catch { }
        }

        /// <summary>
        ///
        /// </summary>
        private bool IsGetInfo = false;
        private readonly SemaphoreSlim _refresh_info_ = new(1);
        private CancellationTokenSource CancelGetInfo = new();

        private int? _last_file_index_ = null;
        private int? _last_file_count_ = null;
        private string[] _last_file_list_ = [];
        private readonly Dictionary<int, string> _last_file_cache_list_ = [];

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsFirstFile()
        {
            return (_last_file_index_ == 0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsLastFile()
        {
            return (_last_file_index_ == _last_file_count_);
        }

        public async Task<int> UpdateFileList()
        {
            int result = _last_file_index_ ?? 0;
            _last_file_list_ = [.. _last_file_list_.Where(File.Exists).Distinct().NaturalSort()];
            var idx = await IndexOf(FileName);
            _last_file_index_ = idx < 0 ? CalcFileIndex(ListPosition.Next) : idx;
            _last_file_count_ = _last_file_list_.Length;
            return (result);
        }

        public int CalcFileIndex(ListPosition position)
        {
            int result = -1;
            if (_last_file_index_.HasValue && _last_file_count_.HasValue)
            {
                switch (position)
                {
                    case ListPosition.Current:
                        result = _last_file_index_ ?? -1;
                        for (var i = _last_file_index_ ?? 0; i < _last_file_list_.Length; i++)
                        {
                            if (File.Exists(_last_file_list_[i])) { result = i; break; }
                        }
                        if (result == -1)
                        {
                            for (var i = _last_file_index_ ?? _last_file_list_.Length - 1; i >= 0; i--)
                            {
                                if (File.Exists(_last_file_list_[i])) { result = i; break; }
                            }
                        }
                        break;
                    case ListPosition.First:
                        result = 0;
                        break;
                    case ListPosition.Prev:
                        result = _last_file_index_ ?? -1;
                        for (var i = _last_file_index_ - 1 ?? _last_file_list_.Length - 1; i >= 0; i--)
                        {
                            if (File.Exists(_last_file_list_[i])) { result = i; break; }
                        }
                        if (result == -1)
                        {
                            for (var i = _last_file_index_ + 1 ?? 0; i < _last_file_list_.Length; i++)
                            {
                                if (File.Exists(_last_file_list_[i])) { result = i; break; }
                            }
                        }
                        break;
                    case ListPosition.Next:
                        result = _last_file_index_ ?? -1;
                        for (var i = _last_file_index_ + 1 ?? 0; i < _last_file_list_.Length; i++)
                        {
                            if (File.Exists(_last_file_list_[i])) { result = i; break; }
                        }
                        if (result == -1)
                        {
                            for (var i = _last_file_index_ - 1 ?? _last_file_list_.Length - 1; i >= 0; i--)
                            {
                                if (File.Exists(_last_file_list_[i])) { result = i; break; }
                            }
                        }
                        break;
                    case ListPosition.Last:
                        result = _last_file_list_.Length - 1;
                        break;
                }
                //_last_file_count_ = _last_file_list_.Length;
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<string[]> GetFileList()
        {
            if (FileName.IsUpdatingFileList() && _last_file_list_?.Length > 0) return (_last_file_list_);
            else return (await FileName.GetFileList());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<(int, int)> GetIndex()
        {
            int index = 0, count = 0;
            if (!string.IsNullOrEmpty(FileName))
            {
                if (FileName.IsUpdatingFileList())
                {
                    index = Array.IndexOf(_last_file_list_, FileName);
                    count = _last_file_list_.Length;
                    if (count > 0)
                    {
                        _last_file_index_ = index;
                        _last_file_count_ = count;

                        var range_s = Math.Max(index - 100, 0);
                        var range_e = Math.Min(index + 100, count - 1);
                        _last_file_cache_list_.Clear();
                        for (var i = range_s; i < range_e; i++)
                        {
                            _last_file_cache_list_[range_s] = _last_file_list_[range_s];
                        }
                    }
                }
                else
                {
                    var files = await FileName.GetFileList();
                    index = Array.IndexOf(files, FileName);
                    count = files.Count();
                    if (count > 0)
                    {
                        _last_file_index_ = index;
                        _last_file_count_ = count;

                        var range_s = Math.Max(index - 100, 0);
                        var range_e = Math.Min(index + 100, count - 1);
                        _last_file_cache_list_.Clear();
                        for (var i = range_s; i < range_e; i++)
                        {
                            _last_file_cache_list_[range_s] = files[range_s];
                        }
                        _last_file_list_ = new string[files.Length];
                        files.CopyTo(_last_file_list_, 0);
                    }
                }
            }
            return (index, count);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetIndexInfo()
        {
            var result = "1/1";
            if (!string.IsNullOrEmpty(FileName) && File.Exists(FileName))
            {
                int index, count;
                (index, count) = await GetIndex();
                if (count > 0) result = $"{index + 1}/{count}";
            }
            return (result);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetSimpleInfo(bool refresh = false)
        {
            if (!ValidCurrent) return (null);

            if (!string.IsNullOrEmpty(_simple_info_)) return (_simple_info_);

            var result = await Task.Run(async () =>
            {
                var ret = string.Empty;
                try
                {
                    var size = OriginalSize;
                    var depth = OriginalDepth;
                    var endian = OriginalEndian;
                    var fmt = Original.GetFormatInfo().MimeType;
                    var DPI_TEXT = GetCurrentDPI();
                    var has_q = Original.IsJPG() || Original.IsTIF();
                    var quality = OriginalQuality;
                    var rating = Rating;

                    if (refresh && await _refresh_info_.WaitAsync(25))
                    {
                        try
                        {
                            Debug.WriteLine($"=> refresh simple iamge info");
                            if (File.Exists(ImageFileInfo.FullName))
                            {
                                Debug.WriteLine($"=> updating simple iamge info");
                                var exif = new CompactExifLib.ExifData(ImageFileInfo.FullName);
                                size.Width = exif.Width;
                                size.Height = exif.Height;
                                depth = (uint)exif.ColorDepth;
                                endian = exif.ByteOrder == CompactExifLib.ExifByteOrder.BigEndian ? Endian.MSB : Endian.LSB;
                                fmt = exif.ImageMime;
                                has_q = exif.ImageType == CompactExifLib.ImageType.Jpeg || exif.ImageType == CompactExifLib.ImageType.Tiff;
                                quality = (uint)exif.JpegQuality;
                                var dpiX = exif.ResolutionX;
                                var dpiY = exif.ResolutionY;
                                DPI_TEXT = $"{dpiX:F0}x{dpiY:F0} PPI";
                                exif.GetTagValue(CompactExifLib.ExifTag.Rating, out rating);
                                await Task.Delay(100);
                            }
                        }
                        catch { }
                        finally { _refresh_info_.Release(); }
                    }

                    var info = new List<string>
                    {
                        $"{size.Width:F0}x{size.Height:F0}x{depth:F0} BPP",
                        $"{(long)size.Width * size.Height / 1000000:F2} MP",
                        $"{DPI_TEXT}",
                        fmt,
                        has_q ? $"Q:{quality}" : string.Empty,
                        $"{endian}"
                    };

                    if (ImageFileInfo is not null)
                    {
                        ImageFileInfo.Refresh();
                        var FileSize = ImageFileInfo.Exists && File.Exists(ImageFileInfo.FullName) ? ImageFileInfo.Length : -1;
                        var FileDate = ImageFileInfo.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss zzz dddd");
                        info.Add($"{FileSize.SmartFileSize()}");
                        info.Add($"{FileDate}");
                    }

                    info.Add(rating > 3 ? "Favorited".T() : string.Empty);

                    ret = string.Join(", ", info.Where(i => !string.IsNullOrWhiteSpace(i?.Trim())));
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex) { ex.ShowMessage(); }
                return(ret);
            });
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="include_colorinfo"></param>
        /// <returns></returns>
        public async Task<string> GetImageInfo(bool include_colorinfo = false)
        {
            if (IsGetInfo && CancelGetInfo != null) CancelGetInfo.Cancel();

            string result = string.Empty;
            if (ValidCurrent && !IsGetInfo && await _loading_image_.WaitAsync(0))
            {
                IsGetInfo = true;
                CancelGetInfo = new CancellationTokenSource();
                result = await Task.Run(async () =>
                {
                    var ret = string.Empty;
                    var st = Stopwatch.StartNew();
                    try
                    {
                        var DPI_TEXT = GetCurrentDPI();
                        if (CancelGetInfo.IsCancellationRequested) return (ret);

                        var original_depth = CalcColorDepth(Original);
                        var current_depth = CalcColorDepth(Current);

                        var tip = new List<string>
                        {
                            $"{"InfoTipDimentionOriginal".T()} {OriginalSize.Width:F0}x{OriginalSize.Height:F0}x{original_depth:F0}, {(long)OriginalSize.Width * OriginalSize.Height / 1000000:F2}MP",
                            $"{"InfoTipDimention".T()} {CurrentSize.Width:F0}x{CurrentSize.Height:F0}x{current_depth:F0}, {(long)CurrentSize.Width * CurrentSize.Height / 1000000:F2}MP",
                            $"{"InfoTipOrientation".T()} {Original?.Orientation}",
                            $"{"InfoTipResolution".T()} {DPI_TEXT}",
                        };
                        if (CancelGetInfo.IsCancellationRequested) return (ret);

                        if (include_colorinfo) tip.Add(await GetTotalColors(cancel: CancelGetInfo.Token));
                        if (CancelGetInfo.IsCancellationRequested) return (ret);

                        if (Current?.AttributeNames != null)
                        {
                            if (CancelGetInfo.IsCancellationRequested) return (ret);
                            var fi = OriginalIsFile ? new FileInfo(FileName) : null;
                            if (fi is not null && fi.Exists)
                            {
                                if (CancelGetInfo.IsCancellationRequested) return (ret);
                                if (Original?.Endian == Endian.Undefined) Original.Endian = DetectFileEndian(fi.FullName);
                                if (CancelGetInfo.IsCancellationRequested) return (ret);
                            }
                            else if (Type == ImageType.Result && Current?.Endian == Endian.Undefined)
                            {
                                Current.Endian = BitConverter.IsLittleEndian ? Endian.LSB : Endian.MSB;
                                if (CancelGetInfo.IsCancellationRequested) return (ret);
                            }

                            if (Current?.ArtifactNames?.Count() > 0)
                            {
                                if (CancelGetInfo.IsCancellationRequested) return (ret);
                                tip.Add($"{"InfoTipArtifacts".T()}");
                                var artifacts = new List<string>();
                                foreach (var artifact in Current?.ArtifactNames)
                                {
                                    var label = artifact.PadRight(32, ' ');
                                    var value = Current?.GetArtifact(artifact);
                                    if (string.IsNullOrEmpty(value)) continue;
                                    artifacts.Add($"  {label}= {value.TextPadding(label, 4)}");
                                }
                                tip.AddRange(artifacts.OrderBy(a => a));
                            }
                            if (CancelGetInfo.IsCancellationRequested) return (ret);
                            await Task.Delay(20);
                            var exif = Current.HasProfile("exif") ? Current?.GetExifProfile() : new ExifProfile();
                            tip.Add($"{"InfoTipAttributes".T()}");
                            var attrs = new List<string>();
                            var tags = new Dictionary<string, IExifValue>();
                            foreach (var tv in exif.Values) { tags[$"exif:{tv.Tag}"] = tv; }
                            if (CancelGetInfo.IsCancellationRequested) return (ret);
                            foreach (var attr in Current?.AttributeNames?.Union(["exif:Rating", "exif:RatingPercent"]).Union(tags.Keys))
                            {
                                try
                                {
                                    if (CancelGetInfo.IsCancellationRequested) break;

                                    var label = attr.PadRight(32, ' ');
                                    var value = Current.GetAttribute(attr);
                                    Debug.WriteLine($"==> {attr} -> {value}");
                                    if (string.IsNullOrEmpty(value) && !attr.Contains("Rating") && !tags.Keys.Contains(attr)) continue;
                                    if (attr.Contains("WinXP")) value = Current.GetAttributes(attr);
                                    else if (attr.StartsWith("date:", StringComparison.CurrentCultureIgnoreCase))
                                    {
                                        var d = DateTime.Now;
                                        if (fi is not null && attr.EndsWith(":create", StringComparison.CurrentCultureIgnoreCase))
                                            value = fi.CreationTime.ToLocalTime().ToString();
                                        else if (fi is not null && attr.EndsWith(":modify", StringComparison.CurrentCultureIgnoreCase))
                                            value = fi.LastWriteTime.ToLocalTime().ToString();
                                        else if (DateTime.TryParse(value, out d))
                                            value = d.ToLocalTime().ToString();
                                    }
                                    else if (attr.Equals("exif:Artist"))
                                        value = exif.GetValue(ExifTag.Artist) != null ? exif.GetValue(ExifTag.Artist).Value : value;
                                    else if (attr.Equals("exif:Copyright"))
                                        value = exif.GetValue(ExifTag.Copyright) != null ? exif.GetValue(ExifTag.Copyright).Value : value;
                                    else if (attr.Equals("exif:ExifVersion"))
                                    {
                                        value = exif.GetValue(ExifTag.ExifVersion) != null ? Encoding.UTF8.GetString(exif.GetValue(ExifTag.ExifVersion).Value) : value;
                                        if (value.IsByteString()) value = value.ByteStringToString();
                                    }
                                    else if (attr.Equals("exif:ImageDescription"))
                                        value = exif.GetValue(ExifTag.ImageDescription) != null ? exif.GetValue(ExifTag.ImageDescription).Value : value;
                                    else if (attr.Equals("exif:UserComment"))// && exif.GetValue(ExifTag.UserComment) != null)
                                    {
                                        if (exif.GetValue(ExifTag.UserComment) != null) value = Current.GetAttributes(attr);
                                        else if (value.IsByteString()) value = value.ByteStringToBytes().BytesToString(msb: Current.Endian == Endian.MSB);
                                    }
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
                                        if (tag is not null)
                                        {
                                            if (attr.EndsWith("Ref"))
                                                value = tag.GetValue().ToString().Trim('\0');
                                            else
                                            {
                                                if (tag.IsArray)
                                                {
                                                    var arv = tag.GetValue() as Rational[];
                                                    value = $"{arv[0].Numerator / arv[0].Denominator:F0}.{arv[1].Numerator / arv[1].Denominator:F0}'{arv[2].Numerator / (double)arv[2].Denominator}\"";
                                                }
                                                else
                                                {
                                                    var arv = (Rational)tag.GetValue();
                                                    value = $"{arv.Numerator / arv.Denominator:F0}";
                                                }
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
                                    if (CancelGetInfo.IsCancellationRequested) break;

                                    if (string.IsNullOrEmpty(value)) continue;
                                    if (attr.EndsWith("Keywords") || attr.EndsWith("Author") || attr.EndsWith("Artist") || attr.EndsWith("Copyright") || attr.EndsWith("Copyrights") || attr.EndsWith("artist") || attr.EndsWith("copyright"))
                                    {
                                        var keywords = value.Split([';'], StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim()).ToList();
                                        for (var i = 5; i < keywords.Count; i += 5) { keywords[i] = $"{Environment.NewLine}{keywords[i]}"; }
                                        value = string.Join("; ", keywords) + ';';
                                    }
                                    else if (value.Length > 64) value = $"{value.Substring(0, 64)} ...";
                                    attrs.Add($"  {label}= {value.TextPadding(label, 4)}");
                                    if (CancelGetInfo.IsCancellationRequested) break;
                                }
                                catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current?.GetMainWindow(), $"{attr} : {ex.Message}"); }
                                if (CancelGetInfo.IsCancellationRequested) break;
                            }
                            tip.AddRange(attrs.OrderBy(a => a));
                            if (CancelGetInfo.IsCancellationRequested) return (ret);
                        }

                        if (OriginalFormatInfo != null)
                            tip.Add($"{"InfoTipFormatInfo".T()} {OriginalFormatInfo.Format} ({OriginalFormatInfo.Description}), mime:{OriginalFormatInfo.MimeType}");
                        else if (CurrentFormatInfo != null)
                            tip.Add($"{"InfoTipFormatInfo".T()} {CurrentFormatInfo.Format} ({CurrentFormatInfo.Description}), mime:{CurrentFormatInfo.MimeType}");
                        tip.Add($"{"InfoTipColorSpace".T()} {Original?.ColorSpace}");
                        tip.Add($"{"InfoTipColorChannelCount".T()} {ChannelCount}");
                        tip.Add($"{"InfoTipHasAlpha".T()} {(Original?.HasAlpha ?? false ? "Included" : "NotIncluded").T()}");
                        if (Original?.ColormapSize > 0) tip.Add($"{"InfoTipColorMapsSize".T()} {Original?.ColormapSize}");
                        tip.Add($"{"InfoTipCompression".T()} {Original?.Compression}");
                        var quality = Original?.Compression == CompressionMethod.JPEG ? $"{Original?.Quality()}" : "Unknown";
                        tip.Add($"{"InfoTipQuality".T()} {quality}");
                        tip.Add($"{"InfoTipMemoryMode".T()} {MemoryUsageMode}");
                        tip.Add($"{"InfoTipIdealMemoryUsage".T()} {(ValidOriginal ? OriginalIdealMemoryUsage.SmartFileSize() : CurrentIdealMemoryUsage.SmartFileSize())}");
                        tip.Add($"{"InfoTipMemoryUsage".T()} {(ValidOriginal ? OriginalRealMemoryUsage.SmartFileSize() : CurrentRealMemoryUsage.SmartFileSize())}");
                        tip.Add($"{"InfoTipDisplayMemory".T()} {CurrentDisplayMemoryUsage.SmartFileSize()}");
                        if (CancelGetInfo.IsCancellationRequested) return (ret);

                        if (!string.IsNullOrEmpty(FileName))
                        {
                            if (ImageFileInfo is not null)
                            {
                                var FileSize = ImageFileInfo.Exists && File.Exists(ImageFileInfo.FullName) ? ImageFileInfo.Length : -1;
                                tip.Add($"{"InfoTipFileSize".T()} {FileSize.SmartFileSize()}, {Original?.Endian}");
                                tip.Add($"{"InfoTipFileName".T()} {ImageFileInfo.FullName}");
                            }
                            else
                            {
                                var FileSize = File.Exists(FileName) ? new FileInfo(FileName).Length : -1;
                                tip.Add($"{"InfoTipFileSize".T()} {FileSize.SmartFileSize()}, {Original?.Endian}");
                                tip.Add($"{"InfoTipFileName".T()} {FileName}");
                            }
                        }
                        else if (ValidOriginal && !string.IsNullOrEmpty(Original?.FileName))
                        {
                            var FileSize = File.Exists(Original?.FileName) ? new FileInfo(Original?.FileName).Length : -1;
                            tip.Add($"{"InfoTipFileSize".T()} {FileSize.SmartFileSize()}, {Original?.Endian}");
                            tip.Add($"{"InfoTipFileName".T()} {Original?.FileName}");
                        }
                        else if (!string.IsNullOrEmpty(Current?.FileName))
                        {
                            var FileSize = File.Exists(Current?.FileName) ? new FileInfo(Current?.FileName).Length : -1;
                            tip.Add($"{"InfoTipFileSize".T()} {FileSize.SmartFileSize()}");
                            tip.Add($"{"InfoTipFileName".T()} {Current?.FileName}");
                        }
                        ret = string.Join(Environment.NewLine, tip);
                        if (CancelGetInfo.IsCancellationRequested) return (ret);
                    }
                    catch (AccessViolationException) { }
                    catch (ObjectDisposedException) { }
                    catch (Exception ex) { ex.ShowMessage(); }
                    finally { _loading_image_.Release(); }
                    st.Stop();
#if DEBUG
                    Debug.WriteLine($"{TimeSpan.FromTicks(st.ElapsedTicks).TotalSeconds:F4}s");
#endif
                    return (ret);
                }, cancellationToken: CancelGetInfo.Token);
                Current?.GetExif();
            }
            IsGetInfo = false;
            return (string.IsNullOrEmpty(result) ? null : result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async Task<string> GetTotalColors(CancellationToken cancel = default)
        {
            var colors = new List<string>();
            if (ValidOriginal)
            {
                colors.Add($"{"InfoTipColorsOriginal".T()} {await Original?.CalcTotalColors(cancel: cancel)}");
                colors.Add($"{"InfoTipBackgroundColorOriginal".T()} {Original?.BackgroundColor.ToHexString()}");
                colors.Add($"{"InfoTipBorderColorOriginal".T()} {Original?.BorderColor.ToHexString()}");
                colors.Add($"{"InfoTipMatteColorOriginal".T()} {Original?.MatteColor.ToHexString()}");
            }
            if (ValidCurrent)
            {
                colors.Add($"{"InfoTipColors".T()} {await Current?.CalcTotalColors(cancel: cancel)}");
                colors.Add($"{"InfoTipBackgroundColor".T()} {Current?.BackgroundColor.ToHexString()}");
                colors.Add($"{"InfoTipBorderColor".T()} {Current?.BorderColor.ToHexString()}");
                colors.Add($"{"InfoTipMatteColor".T()} {Current?.MatteColor.ToHexString()}");
            }
            return (colors.Count > 0 ? string.Join(Environment.NewLine, colors) : string.Empty);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public async Task<int> IndexOf(string file)
        {
            var result = _last_file_index_ ?? -1;
            if (!string.IsNullOrEmpty(file) && File.Exists(file) && _last_file_list_?.Length > 0)
            {
                result = await Task.Run(() => { return(Array.IndexOf(_last_file_list_, file)); });
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async Task<bool> LoadImageFromClipboard()
        {
            IDataObject dataPackage = Application.Current?.Dispatcher?.Invoke(() => Clipboard.GetDataObject());
            return (await LoadImageFromClipboard(dataPackage));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dataPackage"></param>
        /// <returns></returns>
        public async Task<bool> LoadImageFromClipboard(IDataObject dataPackage)
        {
            if (dataPackage is null) return (false);
            var result = await Task.Run(async () =>
            {
                var ret = false;
                var exceptions = new List<string>();
                try
                {
                    var supported_fmts = new string[] { "PNG", "image/png", "image/tif", "image/tiff", "image/webp", "image/xpm", "image/ico", "image/cur", "image/jpg", "image/jpeg", "image/bmp", "DeviceIndependentBitmap", "image/wbmp", "Text" };
                    var fmts = await Application.Current?.Dispatcher?.InvokeAsync(() => dataPackage.GetFormats(true));
                    foreach (var fmt in supported_fmts)
                    {
                        if (fmts.Contains(fmt) && await Application.Current?.Dispatcher?.InvokeAsync(() => dataPackage.GetDataPresent(fmt, true)))
                        {
                            if (fmt.Equals("Text", StringComparison.CurrentCultureIgnoreCase))
                            {
                                var text = await Application.Current?.Dispatcher?.InvokeAsync(() => dataPackage.GetData(fmt, true) as string);
                                try
                                {
                                    if (Regex.IsMatch(text, @"^data:.*?;base64,", RegexOptions.IgnoreCase))
                                    {
                                        var image = MagickImage.FromBase64(Regex.Replace(text, @"^data:.*?;base64,", "", RegexOptions.IgnoreCase));
                                        Original = new MagickImage(image);
                                        image.Dispose();
                                        FileName = string.Empty;
                                        ImageFileInfo = null;
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
                            else
                            {
                                try
                                {
                                    var obj = await Application.Current?.Dispatcher?.InvokeAsync(() => dataPackage.GetData(fmt, true));
                                    if (obj is MemoryStream)
                                    {
                                        Original = new MagickImage(obj as MemoryStream);
                                        FileName = string.Empty;
                                        ImageFileInfo = null;
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
                if (!ret && exceptions.Any()) string.Join(Environment.NewLine, exceptions).ShowMessage();
                return(ret);
            });
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="refresh"></param>
        /// <returns></returns>
        public async Task<bool> LoadImageFromFirstFile(bool refresh = true)
        {
            var result = refresh ? await LoadImageFromIndex(ListPosition.First) : await LoadImageFromIndex(0, refresh: refresh);
            return (result);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="refresh"></param>
        /// <returns></returns>
        public async Task<bool> LoadImageFromPrevFile(bool refresh = true)
        {
            var result = refresh ? await LoadImageFromIndex(ListPosition.Prev) : await LoadImageFromIndex(_last_file_index_ - 1 ?? _last_file_count_ ?? int.MaxValue, refresh: refresh);
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="refresh"></param>
        /// <returns></returns>
        public async Task<bool> LoadImageFromNextFile(bool refresh = true)
        {
            var result = refresh ? await LoadImageFromIndex(ListPosition.Next) : await LoadImageFromIndex(_last_file_index_ + 1 ?? _last_file_count_ ?? int.MaxValue, refresh: refresh);
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="refresh"></param>
        /// <returns></returns>
        public async Task<bool> LoadImageFromLastFile(bool refresh = true)
        {
            var result = refresh ? await LoadImageFromIndex(ListPosition.Last) : await LoadImageFromIndex(_last_file_count_ ?? int.MaxValue, refresh: refresh);
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public async Task<bool> LoadImageFromIndex(ListPosition pos, bool refresh = true)
        {
            var idx = CalcFileIndex(pos);
            var rel = false; // pos == ListPosition.Prev || pos == ListPosition.Next;
            return (await LoadImageFromIndex(idx, relative: rel, refresh: refresh));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="relative"></param>
        /// <param name="refresh"></param>
        /// <returns></returns>
        public async Task<bool> LoadImageFromIndex(int index, bool relative = false, bool refresh = true)
        {
            var result = false;
            result = await Task.Run(async () =>
            {
                var ret = false;
                try
                {
                    if (!string.IsNullOrEmpty(FileName))
                    {
                        //var files = refresh || !FileName.IsUpdatingFileList() ? await FileName.GetFileList() : _last_file_list_ ?? await FileName.GetFileList();
                        //var files = _last_file_list_ ?? await FileName.GetFileList();
                        var files = await GetFileList();
                        if (files.Any())
                        {
                            if (!_last_file_index_.HasValue || index != _last_file_index_.Value)
                            {
                                ret = await LoadImageFromFile(files[index]);
                                if (ret) _last_file_index_ = index;
                            }
                            //if (refresh)
                            //{
                            //    var file_n = Path.IsPathRooted(FileName) ? FileName : files.Where(f => f.EndsWith(FileName, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                            //    var idx_o = Array.IndexOf(files, file_n);
                            //    if (idx_o < 0) idx_o = _last_file_index_ ?? int.MaxValue;
                            //    var idx_n = Math.Max(0, Math.Min(files.Length - 1, relative ? idx_o + index : index));
                            //    if (idx_n != idx_o) ret = await LoadImageFromFile(files[idx_n]);
                            //    if (ret) _last_file_index_ = idx_n;
                            //}
                            //else
                            //{
                            //    var idx_n = Math.Max(0, Math.Min(files.Length - 1, relative ? (_last_file_index_ + index) ?? int.MaxValue : index));
                            //    if (idx_n > _last_file_index_)
                            //        for (var i = _last_file_index_; i < files.Length; i++) { if (File.Exists(files[idx_n])) { _last_file_index_ = i; break; } }
                            //    else if (idx_n < _last_file_index_)
                            //        for (var i = _last_file_index_; i >= 0; i--) { if (File.Exists(files[idx_n])) { _last_file_index_ = i; break; } }
                            //    ret = await LoadImageFromFile(files[idx_n]);
                            //    if (ret) _last_file_index_ = idx_n;
                            //}
                        }
                    }
                }
                catch (Exception ex) { ex.ShowMessage(); }
                return (ret);
            });
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        private readonly SemaphoreSlim _loading_image_ = new(1);

        /// <summary>
        ///
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public async Task<bool> LoadImageFromFile(string file)
        {
            var result = false;
            if (!string.IsNullOrEmpty(file) && File.Exists(file) && await _loading_image_.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                _simple_info_ = string.Empty;

                result = await Task.Run(() =>
                {
                    var ret = false;
                    try
                    {
                        ImageFileInfo = new FileInfo(file);
                        //ImageFileInfo.Refresh();
                        LastFileName = ImageFileInfo.FullName;
                        FileName = ImageFileInfo.FullName;
                        var ext = Path.GetExtension(FileName).ToLower();

                        using var fs = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                        if (fs?.Length <= 0) return (false);

                        if (ext.Equals(".cube"))
                        {
                            Original = fs.Lut2Png();
                        }
                        else if (fs.Length > 0 && fs.CanRead && fs.CanSeek)
                        {
                            var exif = new CompactExifLib.ExifData(fs);
                            if (exif?.ImageType == CompactExifLib.ImageType.Unknown)
                            {
                                OriginalDepth = 0;
                                OriginalEndian = Endian.Undefined;
                            }
                            else
                            {
                                OriginalDepth = (uint)exif?.ColorDepth;
                                OriginalEndian = exif?.ByteOrder == CompactExifLib.ExifByteOrder.LittleEndian ? Endian.LSB : Endian.MSB;
                            }

                            fs.Seek(0, SeekOrigin.Begin);
                            try
                            {
                                var count = 0;
                                var image = new MagickImage(fs, ext.GetImageFileFormat());
                                if (image?.IsValidRead() ?? false)
                                {
                                    while ((image.MetaChannelCount > 0 || CalcColorDepth(image) < OriginalDepth) && count < 20)
                                    {
                                        fs.Seek(0, SeekOrigin.Begin);
                                        image = new MagickImage(fs, ext.GetImageFileFormat());
                                        count++;
                                    }
                                    FixEndian(image, OriginalEndian);
                                    Original = new MagickImage(image);
                                }
                                image?.Dispose();
                            }
                            catch
                            {
                                if (fs?.Length > 0 && fs.CanSeek && fs.CanRead)
                                {
                                    fs?.Seek(0, SeekOrigin.Begin);
                                    Original = new MagickImage(fs, MagickFormat.Unknown);
                                }
                            }
                        }
                        ret = Original.IsValidRead();
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("no decode"))
                            file.ShowMessage("The file is not a known image format!");
                        else if (ex.Message.Contains("Internal image structure is wrong!"))
                            file.ShowMessage("The image file data is corrupted!");
                        else ex.ShowMessage();
                    }
                    finally { _loading_image_.Release(); }
                    return (ret);
                });
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public async Task<bool> CopyToClipboard(MagickImage image)
        {
            var result = false;
            if (image.Valid())
            {
                result = await Task.Run(async () =>
                {
                    var ret = false;
                    try
                    {
                        DataObject dataPackage = new();
                        MemoryStream ms = null;

                        #region Copy Standard Bitmap date to Clipboard
                        var bs = image.ToBitmapSource();
                        dataPackage.SetImage(bs);
                        #endregion
                        #region Copy other MIME format data to Clipboard
                        string[] fmts = image.IsJPG() ? ["image/jpg", "image/jpeg", "CF_DIBV5"] : ["PNG", "image/png", "image/bmp", "image/jpg", "image/jpeg", "CF_DIBV5"];
                        foreach (var fmt in fmts)
                        {
                            if (fmt.Equals("CF_DIBV5", StringComparison.CurrentCultureIgnoreCase))
                            {
                                //if (image.ColorSpace == ColorSpace.scRGB) image.ColorSpace = ColorSpace.sRGB;
                                byte[] arr = image.ToByteArray(MagickFormat.Bmp3);
                                byte[] dib = [.. arr.Skip(14)];
                                ms = new MemoryStream(dib);
                                dataPackage.SetData(fmt, ms);
                                await ms.FlushAsync();
                            }
                            else
                            {
                                var mfmt = fmt.GetMagickFormat();
                                if (mfmt != MagickFormat.Unknown)
                                {
                                    //if (image.ColorSpace == ColorSpace.scRGB) image.ColorSpace = ColorSpace.sRGB;
                                    byte[] arr = image.ToByteArray(mfmt);
                                    ms = new MemoryStream(arr);
                                    dataPackage.SetData(fmt, ms);
                                    await ms.FlushAsync();
                                }
                            }
                        }
                        #endregion
                        await Application.Current?.Dispatcher?.InvokeAsync(() =>
                        {
                            //Clipboard.Clear();
                            Clipboard.SetDataObject(dataPackage, false);
                        });
                        ret = true;
                    }
                    catch (Exception ex) { ex.ShowMessage(); }
                    return (ret);
                });
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async Task<bool> CopyToClipboard()
        {
            var result = false;
            if (ValidCurrent)
            {
                result = await CopyToClipboard(Current);
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public async Task<bool> SetImage()
        {
            var result = false;
            result = await Application.Current?.Dispatcher?.InvokeAsync(() =>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="image"></param>
        /// <returns></returns>
        public bool SaveTopazMask(string file, MagickImage image)
        {
            var result = false;
            if (image.Valid())
            {
                try
                {
                    var format = MagickFormat.Tiff;
                    var fd = Path.GetDirectoryName(file);
                    var fn = Path.GetFileNameWithoutExtension(file);
                    var fe = Path.GetExtension(file);
                    var fm = "-mask";
                    file = fn.ToLower().Contains(fm) ? Path.Combine(fd, $"{fn}.tiff") : Path.Combine(fd, $"{fn}{fm}.tiff");

                    using var target = new MagickImage(image) { BackgroundColor = MagickColors.Black, MatteColor = new MagickColor(MasklightColor.R, MasklightColor.G, MasklightColor.B) };
                    var threshold_color = new MagickColor(HighlightColor.R, HighlightColor.G, HighlightColor.B);
                    FixDPI(target, use_system: true);
                    if (OpMode == ImageOpMode.Compose)
                    {
                        target.Grayscale();
                        target.LevelColors(MagickColors.White, MagickColors.Black);
                        if (LowlightColor.HasAlpha() || LowlightColor == null) target.ColorAlpha(MagickColors.White);
                        target.Threshold(new Percentage(100 - ColorFuzzy.ToDouble() - 5.0));
                    }
                    else if (HighlightColor.HasAlpha() || LowlightColor.HasAlpha() || MasklightColor.HasAlpha())
                    {
                        target.Grayscale();
                        target.ColorAlpha(MagickColors.White);
                        target.LevelColors(MagickColors.Black, MagickColors.White);
                        target.AutoThreshold(AutoThresholdMethod.OTSU);
                    }
                    else
                    {
                        target.ColorThreshold(threshold_color, threshold_color);
                        target.LevelColors(MagickColors.White, MagickColors.Black);
                    }
                    target.Format = format;
                    target.SetCompression(CompressionMethod.Zip);
                    target.Settings.Compression = CompressionMethod.Zip;
                    target.ColorType = ColorType.Palette;
                    target.ColorSpace = ColorSpace.Gray;
                    target.Depth = 8;

                    target.Write(file, format);
                    result = true;
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public bool SaveTopazMask(string file)
        {
            var result = false;
            if (ValidCurrent)
            {
                result = SaveTopazMask(file, Current);
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file"></param>
        /// <param name="image"></param>
        /// <param name="ext"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public bool Save(string file, MagickImage image, string ext = ".png", MagickFormat format = MagickFormat.Unknown)
        {
            var result = false;
            if (image.Valid())
            {
                try
                {
                    var e = Path.GetExtension(file).ToLower();
                    if (string.IsNullOrEmpty(e)) file = $"{file}{ext}";

                    FixDPI(image);

                    var target = image.Clone();
                    if (format == MagickFormat.Png8 || e.Equals(".png8", StringComparison.CurrentCultureIgnoreCase))
                    {
                        target.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                        target.ColormapSize = 256;
                        target.ColorType = ColorType.Palette;
                        target.SetCompression(CompressionMethod.Zip);
                        target.Write(Path.ChangeExtension(file, ".png"), MagickFormat.Png8);
                    }
                    else
                    {
                        var fmt_no_alpha = new MagickFormat[] { MagickFormat.Jpg, MagickFormat.Jpeg, MagickFormat.Jpe, MagickFormat.Bmp2, MagickFormat.Bmp3 };
                        var ext_no_alpha = new string[] { ".jpg", ".jpeg", ".jpe", ".bmp" };
                        if (image.HasAlpha && (fmt_no_alpha.Contains(format) || ext_no_alpha.Contains(e)))
                        {
                            target.Settings.SetDefine("bmp3:alpha", "true");
                            target.Settings.SetDefine("webp:alpha-compression", "1");
                            target.ColorAlpha(MasklightColor ?? target.BackgroundColor);
                        }
                        if (format.IsPNG() || e.StartsWith(".png"))
                        {
                            target.Settings.SetDefine("png:compression-level", "9");
                            target.SetCompression(CompressionMethod.Zip);
                            target.Settings.Compression = CompressionMethod.Zip;
                            //target.Settings.Interlace = image.Interlace;
                            //target.Settings.SetDefine(MagickFormat.Png, "png:IHDR.interlace_method", "0");
                            target.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                            target.Quality = image.Quality == 0 ? 100 : image.Quality;
                            target.Format = MagickFormat.Png;
                        }
                        else if (format.IsTIF() || e.StartsWith(".tif"))
                        {
                            target.Settings.SetDefine("tiff:preserve-compression", "true");
                            target.SetCompression(CompressionMethod.Zip);
                            target.Settings.Compression = CompressionMethod.Zip;
                            target.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                            target.Quality = image.Quality == 0 ? 100 : image.Quality;
                            target.Format = MagickFormat.Tiff;
                        }
                        else if (format.IsGIF() || e.StartsWith(".gif"))
                        {
                            target.GifDisposeMethod = GifDisposeMethod.Background;
                            target.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                            target.Format = MagickFormat.Gif;
                        }
                        else if (format.IsWEBP() || e.Equals(".webp") || e.Equals(".webm"))
                        {
                            target.Settings.SetDefine("webp:alpha-compression", "1");
                            target.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                            target.Quality = image.Quality == 0 ? 75 : image.Quality;
                            target.Format = MagickFormat.WebP;
                        }
                        else if (format.IsBMP() || e.Equals(".bmp"))
                        {
                            target.VirtualPixelMethod = VirtualPixelMethod.Transparent;
                            target.Format = MagickFormat.Bmp;
                        }
                        else if (format.IsJPG() || e.StartsWith(".jp"))
                        {
                            //target.Settings.SetDefine("jpeg:arithmetic-coding", "on");
                            target.Settings.SetDefine("jpeg:block-smoothing", "on");
                            target.Settings.SetDefine("jpeg:optimize-coding", "on");
                            target.Settings.SetDefine("sampling-factor", "4:2:0");
                            target.Settings.SetDefine("dct-method", "float");
                            target.Quality = image.Quality == 0 ? 75 : image.Quality;
                            target.Format = MagickFormat.Jpeg;
                        }

                        //if (image.ColorSpace == ColorSpace.scRGB) image.ColorSpace = ColorSpace.sRGB;
                        target.Settings.AntiAlias = true;
                        target.Settings.Endian = image.Endian;
                        //target.Settings.Interlace = Interlace.Plane;
                        target.BackgroundColor = MasklightColor ?? target.BackgroundColor;
                        target.MatteColor = MasklightColor ?? target.BackgroundColor;
                        target.Density = image.Density;
                        //target.Format = image.Format;
                        //target.Quality = image.Quality;
                        target.Endian = image.Endian;

                        var p_exif = image.GetExifProfile();
                        var p_iptc = image.GetIptcProfile();
                        var p_bim = image.Get8BimProfile();
                        var p_xmp = image.GetXmpProfile();
                        var p_color = image.GetColorProfile();

                        if (p_exif is ExifProfile) target.SetProfile(p_exif);
                        if (p_iptc is IptcProfile) target.SetProfile(p_iptc);
                        if (p_bim is EightBimProfile) target.SetProfile(p_bim);
                        if (p_xmp is XmpProfile) target.SetProfile(p_xmp);
                        if (p_color is ColorProfile) target.SetProfile(p_color);

                        foreach (var profile in image.ProfileNames) { if (image.HasProfile(profile)) target.SetProfile(image.GetProfile(profile)); }
                        foreach (var attr in image.AttributeNames) target.SetAttribute(attr, image.GetAttribute(attr));

                        if (!target.HasProfile("xmp") && target.AttributeNames.Contains("exif:ExtensibleMetadataPlatform"))
                        {
                            var exif = target.GetExifProfile();
                            if (exif is ExifProfile)
                            {
                                var xmp = exif.GetValue(ExifTag.XMP).Value;
                                if (xmp is not null && xmp.Length > 0) target.SetProfile(new XmpProfile(xmp));
                            }
                        }

                        target.Write(file, format);
                    }
                    result = true;
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="file"></param>
        /// <param name="ext"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        public bool Save(string file, string ext = ".png", MagickFormat format = MagickFormat.Unknown)
        {
            var result = false;
            if (ValidCurrent)
            {
                result = Save(file, Current, ext, format);
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="overwrite"></param>
        /// <returns></returns>
        public bool Save(MagickImage image, bool overwrite = false)
        {
            var result = false;
            if (image.Valid())
            {
               try
                {
                    if (overwrite && ValidOriginal && !string.IsNullOrEmpty(FileName) && File.Exists(FileName))
                    {
                        var fi = new FileInfo(FileName);
                        var dc = fi.CreationTime;
                        var dm = fi.LastWriteTime;
                        var da = fi.LastAccessTime;
                        result = Save(FileName, image, format: Original.Format);
                        if (result)
                        {
                            try
                            {
                                fi.CreationTime = dc;
                                fi.LastWriteTime = dm;
                                fi.LastAccessTime = da;
                            }
                            catch { }
                        }
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

                            var fi = new FileInfo(string.IsNullOrEmpty(FileName) ? file : FileName);
                            var dc = fi.Exists ? fi.CreationTime : DateTime.Now;
                            var dm = fi.Exists ? fi.LastWriteTime : DateTime.Now;
                            var da = fi.Exists ? fi.LastAccessTime : DateTime.Now;

                            if (topaz)
                            {
                                result = SaveTopazMask(file);
                            }
                            else
                            {
                                result = Save(file, image, format: fmt);
                            }

                            if (result && !string.IsNullOrEmpty(FileName) && file.Equals(FileName, StringComparison.CurrentCultureIgnoreCase))
                            {
                                try
                                {
                                    fi.CreationTime = dc;
                                    fi.LastWriteTime = dm;
                                    fi.LastAccessTime = da;
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="overwrite"></param>
        /// <returns></returns>
        public bool Save(bool overwrite = false)
        {
            var result = false;
            if (ValidCurrent)
            {
                result = Save(Current, overwrite);
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="order"></param>
        /// <param name="more"></param>
        /// <returns></returns>
        public async Task<bool> Denoise(uint? order = null, bool more = false)
        {
            var result = false;
            if (ValidCurrent)
            {
                try
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
                    result = await SetImage();
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        public bool ResetTransform(bool restore = true)
        {
            CurrentModified = false;
            if (restore && ValidCurrent)
            {
                if (FlipX)
                {
                    Current?.Flop();
                    FlipX = false;
                    CurrentModified = true;
                }
                if (FlipY)
                {
                    Current?.Flip();
                    FlipY = false;
                    CurrentModified = true;
                }
                if (Rotated % 360 != 0)
                {
                    Current?.Rotate(-Rotated);
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

        /// <summary>
        ///
        /// </summary>
        public bool RefreshImageFileInfo(string file = "")
        {
            var result = false;
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                FileName = file;
                ImageFileInfo = new FileInfo(FileName);
            }
            if (ImageFileInfo is null && !string.IsNullOrEmpty(FileName))
            {
                ImageFileInfo = new FileInfo(FileName);
            }
            if (ImageFileInfo is not null)
            {
                ImageFileInfo.Refresh();
                result = true;
            }
            return (result);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public async Task<bool> Reset(int size = -1)
        {
            return (await Reload(size, reload: false));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public async Task<bool> Reset(uint size = 0)
        {
            return (await Reload((int)size, reload: false));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="geo"></param>
        /// <param name="reload"></param>
        /// <param name="reset"></param>
        /// <returns></returns>
        public async Task<bool> Reload(MagickGeometry geo, bool reload = false)
        {
            var result = false;
            try
            {
                if (ValidOriginal)
                {
                    if (reload && !string.IsNullOrEmpty(LastFileName)) await LoadImageFromFile(LastFileName);
                    if (ValidCurrent)
                    {
                        FlipX = false;
                        FlipY = false;
                        Rotated = 0;
                        ResetTransform();
                        if (geo is not null)
                        {
                            Current.Resize(geo);
                            Current.ResetPage();
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

        /// <summary>
        ///
        /// </summary>
        /// <param name="reload"></param>
        /// <param name="reset"></param>
        /// <returns></returns>
        public async Task<bool> Reload(bool reload = false, bool reset = false)
        {
            return (await Reload(CurrentGeometry, reload));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="size"></param>
        /// <param name="reload"></param>
        /// <param name="reset"></param>
        /// <returns></returns>
        public async Task<bool> Reload(int size, bool reload = false)
        {
            if (size <= 0) return (await Reload(reload));
            else return (await Reload(new MagickGeometry($"{size}x{size}>"), reload));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="size"></param>
        /// <param name="reload"></param>
        /// <param name="reset"></param>
        /// <returns></returns>
        public async Task<bool> Reload(uint size, bool reload = false)
        {
            if (size <= 0) return (await Reload(reload));
            else return (await Reload(new MagickGeometry($"{size}x{size}>"), reload));
        }

        /// <summary>
        ///
        /// </summary>
        public void Dispose()
        {
            Current?.Dispose(); Current = null; FileName = string.Empty;
            Original?.Dispose(); Original = null; FileName = string.Empty;
            ResetTransform(restore: false);
        }
    }
}
