﻿using System;
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
                Reload();
            }
        }
        public Size OriginalSize { get { return (ValidOriginal ? new Size(Original.Width, Original.Height) : new Size(0, 0)); } }
        private MagickImage _current_ = null;
        public MagickImage Current
        {
            get { return (_current_); }
            set
            {
                if (_current_ is MagickImage && !_current_.IsDisposed) { _current_.Dispose(); _current_ = null; }
                _current_ = value;
            }
        }
        public Size CurrentSize { get { return (ValidCurrent ? new Size(Current.Width, Current.Height) : new Size(0, 0)); } }

        private ColorSpace _last_colorspace_ = ColorSpace.Undefined;

        public ImageSource Source { get { return (ValidCurrent ? Current.ToBitmapSource() : null); } }

        public bool ValidCurrent { get { return (Current is MagickImage && !Current.IsDisposed); } }
        public bool ValidOriginal { get { return (Original is MagickImage && !Original.IsDisposed); } }

        public FrameworkElement Tagetment { get; set; } = null;

        public string FileName { get; set; } = string.Empty;

        public bool AutoScale { get; set; } = true;
        public int AutoScaleSize { get; set; } = 1024;

        public bool Loaded { get; set; } = false;
        public bool Modified { get; set; } = false;

        public bool FlipX { get; set; } = false;
        public bool FlipY { get; set; } = false;
        public double Rotated { get; set; } = .0;

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
                                    Modified = true;
                                    ret = true;
                                    break;
                                }
#if DEBUG
                                catch (Exception ex) { Debug.WriteLine(ex.Message); }
#else
                                catch (Exception ex) { ex.Message.ShowMessage(); }
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
                                        Original = new MagickImage((obj as MemoryStream), MagickFormat.Unknown);
                                        FileName = string.Empty;
                                        Modified = true;
                                        ret = true;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { ex.Message.ShowMessage(); }
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
                catch (Exception ex) { ex.Message.ShowMessage(); }
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
                catch (Exception ex) { ex.Message.ShowMessage(); }
                return (ret);
            });
            return (result);
        }

        public bool LoadImageFromFile(string file, bool update = true)
        {
            var result = false;
            result = Application.Current.MainWindow.Dispatcher.Invoke(() =>
            {
                var ret = false;
                try
                {
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        FileName = file;

                        if (Path.GetExtension(file).Equals(".cube", StringComparison.CurrentCultureIgnoreCase))
                        {
                            Original = fs.Lut2Png();
                        }
                        else
                        {
                            try { Original = new MagickImage(fs, Path.GetExtension(file).GetImageFileFormat()); }
                            catch { Original = new MagickImage(fs, MagickFormat.Unknown); }
                        }
                        if (update && Tagetment is Image && ValidCurrent)
                            (Tagetment as Image).Source = Source;

                        ret = true;
                    }
                }
                catch (Exception ex) { ex.Message.ShowMessage(); }
                return (ret);
            });
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
            catch (Exception ex) { ex.Message.ShowMessage(); }
            return (result);
        }

        public void Save(string file, string ext = ".png")
        {
            if (ValidCurrent)
            {
                try
                {
                    var e = Path.GetExtension(file);
                    if (string.IsNullOrEmpty(e)) file = $"{file}{ext}";
                    Current.Write(file);
                }
                catch (Exception ex) { ex.Message.ShowMessage(); }
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
                    dlgSave.Filter = $"PNG {file_str}| *.png|JPEG {file_str}|*.jpg;*.jpeg|TIFF {file_str}|*.tif;*.tiff|BITMAP {file_str}|*.bmp";
                    dlgSave.FilterIndex = 1;
                    if (dlgSave.ShowDialog() ?? false)
                    {
                        var file = dlgSave.FileName;
                        var ext = Path.GetExtension(file);
                        var filters = dlgSave.Filter.Split('|');
                        if (string.IsNullOrEmpty(ext))
                        {
                            ext = filters[(dlgSave.FilterIndex - 1) * 2].Replace("*", "");
                            file = $"{file}{ext}";
                        }
                        Save(file);
                    }
                }
                catch (Exception ex) { ex.Message.ShowMessage(); }
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
                    catch (Exception ex) { ex.Message.ShowMessage(); }
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
                catch (Exception ex) { ex.Message.ShowMessage(); }
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

#if DEBUG
        public async Task<string> GetImageInfo()
#else
        public string GetImageInfo()
#endif
        {
            string result = string.Empty;
            try
            {
                if (ValidCurrent)
                {
                    var st = Stopwatch.StartNew();
                    Current.Density.ChangeUnits(DensityUnit.PixelsPerInch);
                    if (Current.Density.X <= 0 || Current.Density.Y <= 0)
                    {
                        var dpi = Application.Current.GetSystemDPI();
                        Current.Density = new Density(dpi.X, dpi.Y, DensityUnit.PixelsPerInch);
                    }
                    var tip = new List<string>();
                    tip.Add($"{"InfoTipDimention".T()} {Current.Width:F0}x{Current.Height:F0}x{Current.ChannelCount * Current.Depth:F0}");
                    if (Current.BoundingBox != null)
                        tip.Add($"{"InfoTipBounding".T()} {Current.BoundingBox.Width:F0}x{Current.BoundingBox.Height:F0}");
                    tip.Add($"{"InfoTipResolution".T()} {Current.Density.X:F0} DPI x {Current.Density.Y:F0} DPI");
                    //tip.Add($"{"InfoTipColors".T()} {TotalColors.Invoke(image)}");
#if DEBUG
                    if (Keyboard.Modifiers == ModifierKeys.Alt)
                        tip.Add($"{"InfoTipColors".T()} {await Current.CalcTotalColors()}");
#endif
                    if (Current.AttributeNames != null)
                    {
                        tip.Add($"{"InfoTipAttributes".T()}");
                        foreach (var attr in Current.AttributeNames)
                        {
                            try
                            {
                                var value = Current.GetAttribute(attr);
                                if (string.IsNullOrEmpty(value)) continue;
                                if (attr.Contains("WinXP")) value = value.DecodeHexUnicode();
                                if (value.Length > 64) value = $"{value.Substring(0, 64)} ...";
                                tip.Add($"  {attr.PadRight(32, ' ')}= { value }");
                            }
                            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, $"{attr} : {ex.Message}"); }
                        }
                    }
                    tip.Add($"{"InfoTipColorSpace".T()} {Current.ColorSpace.ToString()}");
                    if (Current.FormatInfo != null)
                        tip.Add($"{"InfoTipFormatInfo".T()} {Current.FormatInfo.Format.ToString()}, {Current.FormatInfo.MimeType}");
#if Q16HDRI
                    tip.Add($"{"InfoTipMemoryUsage".T()} {((long)(Current.Width * Current.Height * Current.ChannelCount * Current.Depth * 4 / 8)).SmartFileSize()}");
#elif Q16
                tip.Add($"{"InfoTipMemoryUsage".T()} {SmartFileSize(image.Width * image.Height * image.ChannelCount * image.Depth * 2 / 8)}");
#else
                tip.Add($"{"InfoTipMemoryUsage".T()} {SmartFileSize(image.Width * image.Height * image.ChannelCount * image.Depth / 8)}");
#endif
                    tip.Add($"{"InfoTipDisplayMemory".T()} {((long)(Current.Width * Current.Height * 4)).SmartFileSize()}");
                    if (!string.IsNullOrEmpty(Current.FileName))
                        tip.Add($"{"InfoTipFileName".T()} {Current.FileName}");
                    else if (!string.IsNullOrEmpty(FileName))
                        tip.Add($"{"InfoTipFileName".T()} {FileName}");
                    result = string.Join(Environment.NewLine, tip);
                    st.Stop();
#if DEBUG
                    Debug.WriteLine($"{TimeSpan.FromTicks(st.ElapsedTicks).TotalSeconds:F4}s");
#endif
                    Current.GetExif();
                }
            }
            catch (Exception ex) { ex.Message.ShowMessage(); }
            return (string.IsNullOrEmpty(result) ? null : result);
        }

        public bool ResetTransform()
        {
            Modified = false;
            if (ValidCurrent)
            {
                if (FlipX)
                {
                    Current.Flop();
                    FlipX = false;
                    Modified = true;
                }
                if (FlipY)
                {
                    Current.Flip();
                    FlipY = false;
                    Modified = true;
                }
                if (Rotated % 360 != 0)
                {
                    Current.Rotate(-Rotated);
                    Rotated = 0;
                    Modified = true;
                }
            }
            else
            {
                FlipX = false;
                FlipY = false;
                Rotated = 0;
                Modified = true;
            }
            return (Modified);
        }

        public bool Reload()
        {
            var result = false;
            try
            {
                if (ValidOriginal)
                {
                    if (ValidCurrent) { Current.Dispose(); Current = null; }
                    ResetTransform();
                    Current = new MagickImage(Original);
                    Modified = true;
                    _last_colorspace_ = Current.ColorSpace;
                    result = true;
                }
            }
            catch (Exception ex) { ex.Message.ShowMessage(); }
            return (result);
        }

        public bool Reload(MagickGeometry geo)
        {
            var result = false;
            try
            {
                if (ValidOriginal && geo is MagickGeometry)
                {
                    if (ValidCurrent) { Current.Dispose(); Current = null; }
                    ResetTransform();
                    Current = new MagickImage(Original);
                    Current.Resize(geo);
                    Current.RePage();
                    _last_colorspace_ = Current.ColorSpace;
                    Modified = true;
                    result = true;
                }
            }
            catch (Exception ex) { ex.Message.ShowMessage(); }
            return (result);
        }

        public bool Reload(int size)
        {
            if (size <= 0) return (Reload());
            else return (Reload(new MagickGeometry($"{size}x{size}>")));
        }

        public void Dispose()
        {
            if (ValidCurrent) { Current.Dispose(); Current = null; FileName = string.Empty; }
            if (ValidOriginal) { Original.Dispose(); Original = null; FileName = string.Empty; }
            ResetTransform();
        }
    }
}
