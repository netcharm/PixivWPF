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

namespace ImageCompare
{
    public enum ImageTarget { None, Source, Target, Result, All };
    public class ImageInformation
    {
        private MagickImage _original_ = null;
        public MagickImage Original
        {
            get { return (_original_); }
            set
            {
                if (_original_ is MagickImage && !_original_.IsDisposed) { _original_.Dispose(); _original_ = null; }
                _original_ = value;
                _OriginalModified_ = true;
                DenoiseCount = 0;
                DenoiseLevel = 0;
                if (ValidOriginal) _original_.FilterType = FilterType.CubicSpline;
                if (_OriginalModified_) Reload();
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

        private MagickImage _current_ = null;
        public MagickImage Current
        {
            get { return (_current_); }
            set
            {
                if (_current_ is MagickImage && !_current_.IsDisposed) { _current_.Dispose(); _current_ = null; }
                _current_ = value;
                _CurrentModified_ = true;
                if (ValidCurrent) _current_.FilterType = FilterType.CubicSpline;
            }
        }
        public Size CurrentSize { get { return (ValidCurrent ? new Size(Current.Width, Current.Height) : new Size(0, 0)); } }
        public long CurrentRealMemoryUsage { get {
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

        public string MemoryUsageMode
        {
            get
            {
#if Q16HDRI
                return ("Q16HDRI(32bits/Pixel, 4Bytes/Pixel)");
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

        public ImageSource Source { get { return (ValidCurrent ? Current.ToBitmapSource() : null); } }

        public bool ValidCurrent { get { return (Current is MagickImage && !Current.IsDisposed); } }
        public bool ValidOriginal { get { return (Original is MagickImage && !Original.IsDisposed); } }

        public bool OriginalIsFile { get { return (!string.IsNullOrEmpty(FileName) && File.Exists(LastFileName)); } }

        private bool ValidImage(MagickImage image)
        {
            return (image is MagickImage && !image.IsDisposed);
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

        public bool FlipX { get; set; } = false;
        public bool FlipY { get; set; } = false;
        public double Rotated { get; set; } = .0;

        public int DenoiseCount { get; set; } = 0;
        public int DenoiseLevel { get; set; } = 0;

        public void FixDPI(MagickImage image = null)
        {
            if (image == null) image = Current;
            if (ValidImage(image))
            {
                var dpi = Application.Current.GetSystemDPI();
                if (image.Density is Density && image.Density.X > 0 && image.Density.Y > 0)
                {
                    var unit = image.Density.ChangeUnits(DensityUnit.PixelsPerInch);
                    if (unit.X <= 0 || unit.Y <= 0)
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
                try
                {
                    var supported_fmts = new string[] { "PNG", "image/png", "image/jpg", "image/jpeg", "image/tif", "image/tiff", "image/bmp", "DeviceIndependentBitmap", "image/wbmp", "image/webp", "Text" };
                    IDataObject dataPackage = Clipboard.GetDataObject();
                    var fmts = dataPackage.GetFormats();
                    foreach (var fmt in supported_fmts)
                    {
                        if (fmts.Contains(fmt))
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
                                catch (Exception ex) { Debug.WriteLine(ex.Message); }
#else
                                catch (Exception ex) { ex.ShowMessage(); }
#endif
                            }
                            else
                            {
                                var exists = dataPackage.GetDataPresent(fmt, true);
                                if (exists)
                                {
                                    var obj = dataPackage.GetData(fmt, true);
                                    if (obj is MemoryStream)
                                    {
                                        try
                                        {
                                            Original = new MagickImage(obj as MemoryStream);
                                            FileName = string.Empty;
                                            OriginalModified = true;
                                            ret = true;
                                            break;
                                        }
                                        catch (Exception ex) { ex.ShowMessage(); }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { ex.ShowMessage();}
                return(ret);
            }, DispatcherPriority.Render);
            return (result);
        }

        public async Task<bool> LoadImageFromPrevFile()
        {
            var result = false;
            result = await Application.Current.Dispatcher.InvokeAsync(() =>
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
                            if (idx > 0) ret = LoadImageFromFile(files[idx - 1]);
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
            result = await Application.Current.Dispatcher.InvokeAsync(() =>
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
                            if (idx < files.Count - 1) ret = LoadImageFromFile(files[idx + 1]);
                        }
                    }
                }
                catch (Exception ex) { ex.ShowMessage(); }
                return (ret);
            });
            return (result);
        }

        public bool LoadImageFromFile(string file, bool update = false)
        {
            var result = false;
            if (File.Exists(file))
            {
                result = Application.Current.MainWindow.Dispatcher.Invoke(() =>
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
                            if (update && Tagetment is Image && ValidCurrent)
                                (Tagetment as Image).Source = Source;

                            ret = true;
                        }
                    }
                    catch (Exception ex) { ex.ShowMessage(); }
                    return (ret);
                });
            }
            return (result);
        }

        public bool LoadImageFromFile()
        {
            var result = false;
            try
            {
                var file_str = "AllSupportedImageFiles".T();
                var dlgOpen = new Microsoft.Win32.OpenFileDialog() { Multiselect = true, CheckFileExists = true, CheckPathExists = true, ValidateNames = true };
                //dlgOpen.Filter = $"{file_str}|{AllSupportedFiles}|{AllSupportedFilters}";
                dlgOpen.Filter = $"{file_str}|{Extensions.AllSupportedFiles}";
                if (dlgOpen.ShowDialog() ?? false)
                {
                    var file = dlgOpen.FileName;
                    result = new Func<bool>(() => { return (LoadImageFromFile(file)); }).Invoke();
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
                    var e = Path.GetExtension(file);
                    if (string.IsNullOrEmpty(e)) file = $"{file}{ext}";

                    FixDPI();

                    if (e.Equals(".png8", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Current.Write(Path.ChangeExtension(file, ".png"), MagickFormat.Png8);
                    }
                    else Current.Write(file, format);
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

                    using (var image = new MagickImage(Current))
                    {
                        FixDPI(image);
                        image.Format = format;
                        image.SetCompression(CompressionMethod.JPEG);
                        image.ColorType = ColorType.Palette;
                        image.ColorSpace = ColorSpace.Gray;
                        image.Depth = 8;

                        image.Write(file, format);
                    }
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
        }

        public void Save()
        {
            if (ValidCurrent)
            {
                try
                {
                    var file_str = "File".T();
                    var dlgSave = new Microsoft.Win32.SaveFileDialog() {  CheckPathExists = true, ValidateNames = true, DefaultExt = ".png" };
                    dlgSave.Filter = $"PNG {file_str}| *.png|PNG8 {file_str}| *.png|JPEG {file_str}|*.jpg;*.jpeg|TIFF {file_str}|*.tif;*.tiff|BITMAP {file_str}|*.bmp|Topaz Mask {file_str}|*.tiff";
                    dlgSave.FilterIndex = 1;
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
                        var fmt = filter.StartsWith("png8", StringComparison.CurrentCultureIgnoreCase) ? MagickFormat.Png8 : MagickFormat.Unknown;
                        var topaz = filter.StartsWith("Topaz", StringComparison.CurrentCultureIgnoreCase) ? true : false;
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

        public async void Denoise(int? order = null)
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
                //if (value <= 0)
                //    Current.ReduceNoise(3);
                //else
                //    Current.ReduceNoise(Math.Max(0, value));
                ////Current.AdaptiveBlur(0.25, 1.5);
                //Current.SelectiveBlur(5, 5, 16);
                Current.MedianFilter((value > 0 ? value : 3) * factor);
                await SetImage();
            }
        }

        public async void CopyToClipboard()
        {
            if (ValidCurrent)
            {
                await Application.Current.Dispatcher.InvokeAsync(async () =>
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
                        Clipboard.SetDataObject(dataPackage, true);
                    }
                    catch (Exception ex) { ex.ShowMessage(); }
                });
            }
        }

        public async Task<bool> SetImage()
        {
            var result = false;
            result = await Application.Current.Dispatcher.InvokeAsync<bool>(() =>
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

                    var depth = Current.Depth * Current.ChannelCount;
                    if (Current.ColorType == ColorType.Bilevel) depth = 2;
                    else if (Current.ColorType == ColorType.Grayscale) depth = 8;
                    else if (Current.ColorType == ColorType.GrayscaleAlpha) depth = 8 + 8;
                    else if (Current.ColorType == ColorType.Palette) depth = (int)Math.Ceiling(Math.Log(Current.ColormapSize, 2));
                    else if (Current.ColorType == ColorType.PaletteAlpha) depth = (int)Math.Ceiling(Math.Log(Current.ColormapSize, 2)) + 8;
                    else if (Current.ColorType == ColorType.TrueColor) depth = 24;
                    else if (Current.ColorType == ColorType.TrueColorAlpha) depth = 32;
                    else if (Current.ColorType == ColorType.ColorSeparation) depth = 24;
                    else if (Current.ColorType == ColorType.ColorSeparationAlpha) depth = 32;

                    var tip = new List<string>();
                    tip.Add($"{"InfoTipDimentionOriginal".T()} {OriginalSize.Width:F0}x{OriginalSize.Height:F0}x{depth:F0}, {(long)OriginalSize.Width * OriginalSize.Height / 1000000:F2}MP");
                    tip.Add($"{"InfoTipDimention".T()} {CurrentSize.Width:F0}x{CurrentSize.Height:F0}x{depth:F0}, {(long)CurrentSize.Width * CurrentSize.Height / 1000000:F2}MP");
                    if (Current.BoundingBox != null)
                        tip.Add($"{"InfoTipBounding".T()} {Current.BoundingBox.Width:F0}x{Current.BoundingBox.Height:F0}");
                    tip.Add($"{"InfoTipResolution".T()} {DPI_TEXT}");
                    
                    if (include_colorinfo) tip.Add(await GetTotalColors());

                    if (Current.AttributeNames != null)
                    {
                        var fi = OriginalIsFile ? new FileInfo(FileName) : null;
                        var exif = Current.HasProfile("exif") ? Current.GetExifProfile() : new ExifProfile();
                        tip.Add($"{"InfoTipAttributes".T()}");
                        foreach (var attr in Current.AttributeNames)
                        {
                            try
                            {
                                var value = Current.GetAttribute(attr);
                                if (string.IsNullOrEmpty(value)) continue;
                                if (attr.Contains("WinXP")) value = value.DecodeHexUnicode();
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
                                else if (attr.Equals("exif:ImageDescription"))
                                    value = exif.GetValue(ExifTag.ImageDescription) != null ? exif.GetValue(ExifTag.ImageDescription).Value : value;
                                else if (attr.Equals("exif:UserComment") && exif.GetValue(ExifTag.UserComment) != null)
                                {
                                    //var endian = BitConverter.IsLittleEndian ? Endian.LSB : Endian.MSB;
                                    //if (Current.Endian == Endian.MSB || endian == Endian.MSB)
                                    //    value = Encoding.BigEndianUnicode.GetString(exif.GetValue(ExifTag.UserComment).Value.Skip(8).ToArray());
                                    //else if (Current.Endian == Endian.LSB || endian == Endian.LSB)
                                    //    value = Encoding.UTF32.GetString(exif.GetValue(ExifTag.UserComment).Value.Skip(8).ToArray());
                                    value = Encoding.BigEndianUnicode.GetString(exif.GetValue(ExifTag.UserComment).Value.Skip(8).ToArray());
                                }
                                if (value.Length > 64) value = $"{value.Substring(0, 64)} ...";
                                var label = attr.PadRight(32, ' ');
                                tip.Add($"  {label}= {TextPadding(value, label, 4)}");
                            }
                            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, $"{attr} : {ex.Message}"); }
                        }
                    }
                    tip.Add($"{"InfoTipColorSpace".T()} {Current.ColorSpace.ToString()}");
                    if (Current.FormatInfo != null)
                        tip.Add($"{"InfoTipFormatInfo".T()} {Current.FormatInfo.Format.ToString()} ({Current.FormatInfo.Description}), mime:{Current.FormatInfo.MimeType}");
                    tip.Add($"{"InfoTipHasAlpha".T()} {(Current.HasAlpha ? "Included" : "NotIncluded").T()}");
                    tip.Add($"{"InfoTipColorMapsSize".T()} {Current.ColormapSize.ToString()}");
                    tip.Add($"{"InfoTipCompression".T()} {Current.Compression.ToString()}");
                    tip.Add($"{"InfoTipQuality".T()} {Current.Quality.ToString()}");
                    tip.Add($"{"InfoTipMemoryMode".T()} {MemoryUsageMode}");
                    tip.Add($"{"InfoTipIdealMemoryUsage".T()} {(ValidOriginal ? OriginalIdealMemoryUsage.SmartFileSize() : CurrentIdealMemoryUsage.SmartFileSize())}");
                    tip.Add($"{"InfoTipMemoryUsage".T()} {(ValidOriginal ? OriginalRealMemoryUsage.SmartFileSize() : CurrentRealMemoryUsage.SmartFileSize())}");
                    tip.Add($"{"InfoTipDisplayMemory".T()} {CurrentRealMemoryUsage.SmartFileSize()}");                    
                    if (!string.IsNullOrEmpty(FileName))
                    {
                        var FileSize = !string.IsNullOrEmpty(FileName) && File.Exists(FileName) ? new FileInfo(FileName).Length : -1;
                        tip.Add($"{"InfoTipFileSize".T()} {FileSize.SmartFileSize()}");
                        tip.Add($"{"InfoTipFileName".T()} {FileName}");
                    }
                    else if (ValidOriginal && !string.IsNullOrEmpty(Original.FileName))
                    {
                        var FileSize = !string.IsNullOrEmpty(Original.FileName) && File.Exists(Original.FileName) ? new FileInfo(Original.FileName).Length : -1;
                        tip.Add($"{"InfoTipFileSize".T()} {FileSize.SmartFileSize()}");
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

        public bool Reset(int size = -1)
        {
            return(Reload(size, reload: false, reset: true));
        }

        public bool Reload(bool reload = false, bool reset = false)
        {
            var result = false;
            try
            {
                if (ValidOriginal)
                {
                    if (reload && !string.IsNullOrEmpty(LastFileName)) LoadImageFromFile(LastFileName, update: false);
                    if (OriginalModified || (ValidOriginal && !ValidCurrent) || (reset && ValidOriginal))
                    {
                        if (ValidCurrent) { Current.Dispose(); Current = null; }
                        Current = new MagickImage(Original);
                    }
                    if (ValidCurrent)
                    {
                        ResetTransform();
                        _basesize_ = new Size(Current.Width, Current.Height);
                        _last_colorspace_ = Current.ColorSpace;
                    }
                    result = true;
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

        public bool Reload(MagickGeometry geo, bool reload = false, bool reset = false)
        {
            var result = false;
            try
            {
                if (ValidOriginal && geo is MagickGeometry)
                {
                    if (reload && !string.IsNullOrEmpty(LastFileName)) LoadImageFromFile(LastFileName, update: false);
                    if (OriginalModified || (ValidOriginal && !ValidCurrent) || (reset && ValidOriginal))
                    {
                        if (ValidCurrent) { Current.Dispose(); Current = null; }
                        Current = new MagickImage(Original);
                    }
                    if (ValidCurrent)
                    {
                        ResetTransform();
                        Current.Resize(geo);
                        Current.RePage();
                        _basesize_ = new Size(Current.Width, Current.Height);
                        _last_colorspace_ = Current.ColorSpace;
                    }
                    result = true;
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

        public bool Reload(int size, bool reload = false, bool reset = false)
        {
            if (size <= 0) return (Reload(reload, reset));
            else return (Reload(new MagickGeometry($"{size}x{size}>"), reload, reset));
        }

        public void Dispose()
        {
            if (ValidCurrent) { Current.Dispose(); Current = null; FileName = string.Empty; }
            if (ValidOriginal) { Original.Dispose(); Original = null; FileName = string.Empty; }
            ResetTransform();
        }
    }
}
