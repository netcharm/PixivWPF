using System;
using System.Configuration;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Threading;

using CompactExifLib;
using SkiaSharp;
using ImageSearch.Search;
using NumSharp.Utilities;


namespace ImageSearch
{
#pragma warning disable IDE0063

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private WindowState LastWinState = WindowState.Normal;
        private readonly List<string> _log_ = [];

        private readonly ObservableCollection<ImageResultGalleryItem> GalleryList = [];

        private Settings settings = new();

        private Similar? similar = null;

        //private System.Windows.Media.Brush? DefaultTextBrush { get; set; } = null;

        private List<Storage> _storages_ = [];

        private static string GetAbsolutePath(string relativePath)
        {
            string fullPath = string.Empty;
            FileInfo _root_ = new (new Uri(System.Reflection.Assembly.GetExecutingAssembly().Location).LocalPath);
            if (_root_.Directory is not null)
            {
                string assemblyFolderPath = _root_.Directory.FullName;

                fullPath = System.IO.Path.Combine(assemblyFolderPath, relativePath);
            }
            return fullPath;
        }

        private static string GetAppName()
        {
            //FileInfo _dataRoot = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            return (System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location));
        }

        private static readonly SemaphoreSlim CanDoEvents = new(1, 1);

        private static object? ExitFrame(object state)
        {
            ((DispatcherFrame)state).Continue = false;
            return null;
        }

        public static async void DoEvents()
        {
            if (Application.Current is not null && await CanDoEvents.WaitAsync(0))
            {
                try
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        await Dispatcher.Yield(DispatcherPriority.Render);
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        if (Application.Current.Dispatcher.CheckAccess())
                        {
                            DispatcherFrame frame = new();
                            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, new DispatcherOperationCallback(ExitFrame), frame);
                            Dispatcher.PushFrame(frame);
                        }
                    }
                    catch (Exception)
                    {
                        await Task.Delay(1);
                    }
                }
                finally
                {
                    if (CanDoEvents?.CurrentCount <= 0) CanDoEvents?.Release();
                }
            }
        }

        private void ShowLog()
        {
            edResult?.Dispatcher.Invoke(() =>
            {
                if (WindowState != WindowState.Minimized && _log_.Count > 0)
                {
                    edResult.Text = string.Join(Environment.NewLine, _log_.TakeLast(settings.LogLines));
                    edResult.ScrollToEnd();
                    edResult.SelectionStart = edResult.Text.Length;
                }
                else if (_log_.Count == 0) edResult.Text = string.Empty;
                LatestMessage.Text = _log_.LastOrDefault();
                DoEvents();
            }, DispatcherPriority.Normal);
        }

        private void ReportBatch(BatchTaskInfo info)
        {
            if (info is not null)
            {
                var msg = $"{info.FileName}, [{info.Current}/{info.Total}, {info.Percentage:P}], Remaining≈{info.RemainingTime}";
                _log_.Add($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}] {msg}");
                ShowLog();
                DoEvents();
            }
        }

        private void ReportProgress(double percentage)
        {
            progress?.Dispatcher.Invoke(() =>
            {
                percentage = double.IsNaN(percentage) ? 0 : percentage;
                if (progress.IsIndeterminate && percentage > 0 && percentage < 100) progress.IsIndeterminate = false;
                progress.Value = (int)Math.Min(100, Math.Max(0, percentage));
                DoEvents();
            }, DispatcherPriority.Normal);
        }

        private void ReportMessage(string info, TaskStatus state = TaskStatus.Created)
        {
            if (!string.IsNullOrEmpty(info))
            {
                progress?.Dispatcher.Invoke(() =>
                {
                    if (progress.Value <= 0 || progress.Value >= 100)
                    {
                        var state_old = progress.IsIndeterminate;
                        var state_new = state == TaskStatus.Running || state == TaskStatus.WaitingForActivation || state == TaskStatus.Canceled;
                        if (state_new != state_old) progress.IsIndeterminate = state_new;
                    }
                }, DispatcherPriority.Normal);

                var lines = info.Split(Environment.NewLine).Select(l => $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}] {l}");
                _log_.AddRange(lines);
                //var log = _log_.Count > settings.LogLines ? _log_.TakeLast(settings.LogLines) : lines;
                ShowLog();
                DoEvents();
            }
        }

        public void ReportMessage(Exception ex, TaskStatus state = TaskStatus.Created)
        {
            if (ex is not null)
            {
                ReportMessage($"{ex?.StackTrace?.Split().TakeLast(3)} : {ex.Message}", state);
            }
        }

        public void ChangeTheme(bool dark = false)
        {
            try
            {
                var source = new Uri(@"pack://application:,,,/ImageSearch;component/Resources/checkerboard.png", UriKind.RelativeOrAbsolute);
                var sri = Application.GetResourceStream(source);
                if (sri is not null && sri.ContentType.Equals("image/png") && sri.Stream is not null && sri.Stream.CanRead && sri.Stream.Length > 0)
                {
                    var opacity = 0.1;
                    var text_brush = Foreground;
                    using (var skb = SKBitmap.Decode(sri.Stream))
                    {
                        if (dark)
                        {
                            using (var skcf = SKColorFilter.CreateBlendMode(new SKColor(0x20, 0x20, 0x20), SKBlendMode.Multiply))
                            {
                                using (var canvas = new SKCanvas(skb))
                                {
                                    using (var paint = new SKPaint())
                                    {
                                        paint.ColorFilter = skcf;
                                        canvas.DrawBitmap(skb, 0, 0, paint);
                                    }
                                }
                            }
                            opacity = 1.0;
                            text_brush = new SolidColorBrush(Colors.Silver);
                        }
                        var checkerboard = new ImageBrush(ToBitmapSource(skb)) { TileMode = TileMode.Tile, Opacity = opacity, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 32, 32) };
                        SimilarViewer.Background = checkerboard;
                        SimilarViewer.InvalidateVisual();
                        CompareViewer.Background = checkerboard;
                        CompareViewer.InvalidateVisual();

                        SimilarResultGallery.Foreground = text_brush;
                    }
                }
            }
            catch (Exception ex) { ReportMessage(ex); }
        }

        public SKBitmap? FromImageSource(ImageSource src)
        {
            SKBitmap? result = null;
            if (src is not null)
            {
                using (var ms = new MemoryStream())
                {
                    try
                    {
                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(src as BitmapSource));
                        encoder.Save(ms);

                        ms.Seek(0, SeekOrigin.Begin);
                        result = SKBitmap.Decode(ms);
                    }
                    catch (Exception ex) { ReportMessage(ex); }
                }
            }
            return (result);
        }

        public BitmapSource? ToBitmapSource(SKBitmap src)
        {
            BitmapSource? result = null;
            if (src is not null)
            {
                using (var sk_data = src.Encode(SKEncodedImageFormat.Png, 100))
                {
                    using (var ms = new MemoryStream())
                    {
                        try
                        {
                            sk_data.SaveTo(ms);
                            ms.Seek(0, SeekOrigin.Begin);

                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.DecodePixelWidth = src.Width;
                            bmp.DecodePixelHeight = src.Height;
                            bmp.StreamSource = ms;
                            bmp.EndInit();
                            bmp.Freeze();

                            result = bmp.Clone();
                        }
                        catch (Exception ex) { ReportMessage(ex); }
                    }
                }
            }
            return (result);
        }

        private (BitmapSource?, SKBitmap?, string?) LoadImageFromStream(Stream? stream)
        {
            BitmapImage? bmp = null;
            SKBitmap? skb = null;
            if (stream is not null && stream.CanSeek && stream.CanRead && stream.Length > 0)
            {
                stream.Seek(0, SeekOrigin.Begin);
                bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();

                stream.Seek(0, SeekOrigin.Begin);
                skb = SKBitmap.Decode(stream);

                if (bmp is not null && skb is null) skb = FromImageSource(bmp);
                if (bmp is null && skb is not null) bmp = (BitmapImage?)ToBitmapSource(skb);
            }
            return ((bmp, skb, null));
        }

        private (BitmapSource?, SKBitmap?, string?) LoadImageFromFile(string file)
        {
            BitmapSource? bmp = null;
            SKBitmap? skb = null;
            if (File.Exists(file))
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(file)))
                {
                    (bmp, skb, _) = LoadImageFromStream(ms);
                }
            }
            return ((bmp, skb, file));
        }

        public void LoadImageFromFiles(string[]? files, bool query = false, bool scope_all = false)
        {
            files = files?.Where(f => File.Exists(f)).Take(2).ToArray();
            if (files.Length > 0)
            {
                Task.Run(async () =>
                {
                    (var bmp, var skb, var file) = LoadImageFromFile(files[0]);
                    await SimilarSrc.Dispatcher.InvokeAsync(async () =>
                    {
                        if (bmp is not null) SimilarSrc.Source = bmp;
                        if (skb is not null) SimilarSrc.Tag = skb;
                        ToolTipService.SetToolTip(SimilarSrcBox, await GetImageInfo(file));
                        if (query && !IsQuering)
                        {
                            AllFolders.IsChecked = scope_all;
                            QueryImage_Click(QueryImage, new RoutedEventArgs());
                        }
                    });
                });
            }
        }

        private async Task<(BitmapSource?, SKBitmap?, string?)> LoadImageFromWeb(Uri uri)
        {
            BitmapSource? bmp = null;
            SKBitmap? skb = null;

            using (HttpClient client = new())
            {
                using (HttpResponseMessage response = await client.GetAsync(uri))
                {
                    try
                    {
                        if (response.StatusCode == HttpStatusCode.OK) //Make sure the URL is not empty and the image is there
                        {
                            using (var ms = await response.Content.ReadAsStreamAsync())
                            {
                                (bmp, skb, _) = LoadImageFromStream(ms);
                            }
                        }
                    }
                    catch (Exception ex) { ReportMessage(ex); }
                }
            }
            return ((bmp, skb, uri.AbsolutePath));
        }

        private async Task<List<(BitmapSource?, SKBitmap?, string?)>> LoadImageFromDataObject(IDataObject dataPackage)
        {
            List<(BitmapSource?, SKBitmap?, string?)> imgs = [];
            if (dataPackage is DataObject)
            {
                var supported_fmts = new string[] { "PNG", "image/png", "image/tif", "image/tiff", "image/webp", "image/xpm", "image/ico", "image/cur", "image/jpg", "image/jpeg", "img/jfif", "image/bmp", "DeviceIndependentBitmap", "Format17", "image/wbmp", "FileDrop", "Text" };
                var fmts = dataPackage.GetFormats(true);
                foreach (var fmt in supported_fmts)
                {
                    if (fmts.Contains(fmt) && dataPackage.GetDataPresent(fmt, true))
                    {
                        try
                        {
                            if (fmt.Equals("FileDrop"))
                            {
                                var files = dataPackage.GetData(fmt, true) as string[];
                                if (files is not null)
                                {
                                    files = files.Where(f => File.Exists(f)).ToArray();
                                    if (files.Length > 1)
                                    {
                                        imgs.Add(LoadImageFromFile(files[0]));
                                        imgs.Add(LoadImageFromFile(files[1]));
                                    }
                                    else if (files.Length > 0)
                                    {
                                        imgs.Add(LoadImageFromFile(files[0]));
                                    }
                                    break;
                                }
                            }
                            else if (fmt.Equals("Text"))
                            {
                                var file = dataPackage.GetData(fmt, true) as string;
                                if (!string.IsNullOrEmpty(file))
                                {
                                    var uri = new Uri(file);
                                    if (file.StartsWith("http"))
                                    {
                                        imgs.Add(await LoadImageFromWeb(uri));
                                    }
                                    else if (File.Exists(file))
                                    {
                                        imgs.Add(LoadImageFromFile(file));
                                    }
                                    break;
                                }
                            }
                            else if (fmt.Equals("DeviceIndependentBitmap") || fmt.Equals("Format17"))
                            {
                                //
                                // https://en.wikipedia.org/wiki/BMP_file_format
                                //
                                var obj = dataPackage.GetData(fmt, true);
                                if (obj is MemoryStream)
                                {
                                    var dib = (obj as MemoryStream).ToArray();
                                    byte[] bh = [
                                        0x42, 0x4D,
                                        0x00, 0x00, 0x00, 0x00,
                                        0x00, 0x00,
                                        0x00, 0x00,
                                        0x36, 0x00, 0x00, 0x00, //0x28
                                    ];
                                    var bs = (uint)dib.Length + bh.Length;
                                    var bsb = BitConverter.GetBytes(bs);
                                    bh[2] = bsb[0];
                                    bh[3] = bsb[1];
                                    bh[4] = bsb[2];
                                    bh[5] = bsb[3];
                                    //if (fmt.Equals("Format17")) bh[13] = 0x28;
                                    using (var s = new MemoryStream())
                                    {
                                        s.Write(bh, 0, bh.Length);
                                        s.Write(dib, 0, dib.Length);
                                        imgs.Add(LoadImageFromStream(s));
                                    }
                                    bh = [];
                                    dib = [];
                                    break;
                                }

                            }
                            else
                            {
                                var obj = dataPackage.GetData(fmt, true);
                                if (obj is MemoryStream)
                                {
                                    var ms = obj as MemoryStream;
                                    imgs.Add(LoadImageFromStream(ms));
                                    break;
                                }
                            }
                        }
                        catch (Exception ex) { ReportMessage($"{fmt} : {ex.Message}"); }
                    }
                }
            }
            return (imgs);
        }

        private async Task<(BitmapSource?, SKBitmap?)> LoadImageFromClipboard()
        {
            BitmapSource? bmp = null;
            SKBitmap? skb = null;
            try
            {
                IDataObject dataPackage = Clipboard.GetDataObject();
                var imgs = await LoadImageFromDataObject(dataPackage);
                if (imgs.Count > 0) (bmp, skb, _) = imgs.FirstOrDefault();
            }
            catch (Exception ex) { ReportMessage(ex); }
            return ((bmp, skb));
        }

        private async Task LoadFeatureDB(bool reload = false)
        {
            InitSimilar();

            var storage = new Storage();
            var folder = string.Empty;
            var feature_db = string.Empty;

            var all = await Dispatcher.InvokeAsync(() =>
            {
                if (FolderList.SelectedIndex >= 0)
                {
                    storage = (FolderList.SelectedItem as ComboBoxItem).DataContext as Storage;
                    folder = storage.ImageFolder;
                    feature_db = storage.DatabaseFile;
                }
                return (AllFolders.IsChecked ?? false);
            }, DispatcherPriority.Normal);

            await similar?.LoadModel();
            if (all)
                await similar?.LoadFeatureData(similar.StorageList, reload: reload);
            else
                await similar?.LoadFeatureData(storage, reload: reload);
        }

        private void InitSimilar()
        {
            similar ??= new Similar
            {
                Setting = settings,

                ModelLocation = settings.ModelFile,
                ModelInputColumnName = settings.ModelInput,
                ModelOutputColumnName = settings.ModelOutput,

                ModelUseGpu = settings.ModelUseGpu,
                ModelGpuDeviceId = settings.ModelGpuDeviceId,

                StorageList = _storages_,

                ResultMax = settings.ResultLimitMax,

                ReportProgressAction = new Action<double>(ReportProgress),
                ReportBatchTaskAction = new Action<BatchTaskInfo>(ReportBatch),
                ReportMessageAction = new Action<string, TaskStatus>(ReportMessage),
                ReportExceptionAction = new Action<Exception, TaskStatus>(ReportMessage),
            };
        }

        private static void ShellSearch(string query, IEnumerable<string?> folders)
        {
            if (!string.IsNullOrEmpty(query) && folders is not null)
            {
                var targets = folders.Distinct().Where(d => Directory.Exists(d));
                if (targets.Any())
                {
                    var location = string.Join("&", targets.Select(d => $"crumb=location:{d}"));
                    var cmd = "explorer.exe";
                    var cmd_param = $"/root,\"search-ms:{location}&query={query}\"";
                    Task.Run(() =>
                    {
                        try
                        {
                            Process.Start(cmd, cmd_param);
                        }
                        catch { }
                    });
                }
            }
        }

        private void ShellCompare(string[] files)
        {
            if (files is not null && files.Length > 0)
            {
                files = files.Where(f => File.Exists(f)).Select(f => $"{f}").Take(2).ToArray();
                if (!string.IsNullOrEmpty(settings.ImageCompareCmd) && File.Exists(settings.ImageCompareCmd))
                {
                    Task.Run(() =>
                    {
                        if (files.Length > 1) Process.Start(settings.ImageCompareCmd, [settings.ImageCompareOpt, files[0], files[1]]);
                        else if (files.Length > 0) Process.Start(settings.ImageCompareCmd, [settings.ImageCompareOpt, files[0]]);
                    });
                }
            }
        }

        private void ShellOpen(string[] files, bool system = false, bool openwith = false, bool viewinfo = false)
        {
            if (files is not null && files.Length > 0)
            {
                files = files.Where(f => File.Exists(f)).Select(f => $"{f}").ToArray();

                foreach (var file in files)
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            if (openwith) Process.Start("openwith.exe", file);
                            else if (viewinfo)
                            {
                                if (string.IsNullOrEmpty(settings.ImageInfoViewerCmd) || !File.Exists(settings.ImageInfoViewerCmd))
                                    Process.Start("explorer.exe", file);
                                else
                                    Process.Start(settings.ImageInfoViewerCmd, [settings.ImageInfoViewerOpt, file]);
                            }
                            else
                            {
                                if (system || string.IsNullOrEmpty(settings.ImageViewerCmd) || !File.Exists(settings.ImageViewerCmd))
                                    Process.Start("explorer.exe", file);
                                else
                                    Process.Start(settings.ImageViewerCmd, [settings.ImageViewerOpt, file]);
                            }
                        }
                        catch (Exception ex) { ReportMessage(ex); }
                    });
                }
            }
        }

        private void CopyText(string text)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    Clipboard.SetText(text);
                }
                catch (Exception ex) { ReportMessage(ex); };
            });
        }

        private static string PaddingLines(string text, int padding)
        {
            if (string.IsNullOrEmpty(text)) return (text);
            var lines = text.Split(["\n\r", "\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries);
            return (string.Join(Environment.NewLine, lines.Select(l => $"{"".PadLeft(padding)}{l}")).Trim());
        }

        private async Task<string> GetImageInfo(string? img_file)
        {
            var result = new List<string>();
            if (img_file is not null && !string.IsNullOrEmpty(img_file) && File.Exists(img_file))
            {
                result = await Task.Run(() =>
                {
                    var infos = new List<string>();

                    #region Add File info to tooltip
                    FileInfo fi = new (img_file);
                    infos.Add($"Full Name   : {GetAbsolutePath(img_file)}");
                    infos.Add($"File Size   : {fi.Length:N0} Bytes");
                    infos.Add($"File Date   : {fi.LastWriteTime:yyyy/MM/dd HH:mm:ss zzz}");
                    #endregion

                    #region Add EXIF info to tooltip
                    try
                    {
                        var exif = new ExifData(img_file);
                        if (exif != null && exif.ImageType != ImageType.Unknown)
                        {
                            #region Get Taken date
                            exif.GetDateDigitized(out var date_digital);
                            exif.GetDateTaken(out var date_taken);

                            if (date_taken.Ticks > 0)
                                infos.Add($"Taken Date  : {date_taken:yyyy/MM/dd HH:mm:ss zzz}");
                            #endregion

                            #region Image Info
                            exif.GetTagValue(ExifTag.Rating, out int ranking);
                            exif.GetTagValue(ExifTag.RatingPercent, out int rating);

                            var quality = exif.ImageType == ImageType.Jpeg && exif.JpegQuality > 0 ? exif.JpegQuality : -1;
                            var endian = exif.ByteOrder == ExifByteOrder.BigEndian ? "Big Endian" : "Little Endian";
                            var fmomat = exif.PixelFormat.ToString().Replace("Format", "");
                            infos.Add($"Image Info  : {exif.ImageType}, {exif.ImageMime}, {exif.ByteOrder}, Q={(quality > 0 ? quality : "???")}, Rank={ranking}");
                            infos.Add($"Image Size  : {exif.Width}x{exif.Height}x{exif.ColorDepth} ({exif.Width * exif.Height / 1000.0 / 1000.0:0.##} MP), {fmomat}, DPI={exif.ResolutionX}x{exif.ResolutionY}");
                            #endregion

                            #region Get Camera Info
                            exif.GetTagValue(ExifTag.Make, out string maker, StrCoding.Utf8);
                            exif.GetTagValue(ExifTag.Model, out string model, StrCoding.Utf8);
                            exif.GetTagValue(ExifTag.FNumber, out ExifRational fNumber);
                            exif.GetTagValue(ExifTag.FocalLength, out ExifRational focalLength);
                            exif.GetTagValue(ExifTag.FocalLengthIn35mmFilm, out ExifRational focalLength35);
                            exif.GetTagValue(ExifTag.ApertureValue, out ExifRational aperture);
                            exif.GetTagValue(ExifTag.IsoSpeed, out ExifRational isoSpeed);
                            exif.GetTagValue(ExifTag.IsoSpeedRatings, out int isoSpeedRating);
                            exif.GetTagValue(ExifTag.ExposureTime, out ExifRational exposureTime);
                            exif.GetTagValue(ExifTag.ExposureBiasValue, out ExifRational exposureBias);
                            exif.GetTagValue(ExifTag.ShutterSpeedValue, out ExifRational shutterSpeed);
                            exif.GetTagValue(ExifTag.PhotographicSensitivity, out int photoSens);

                            var camera = new List<string>();
                            if (!string.IsNullOrEmpty(maker)) camera.Add($"{maker.Trim()}");
                            if (!string.IsNullOrEmpty(model)) camera.Add($"{model.Replace(maker, "").Trim()}");
                            if (isoSpeedRating > 0) camera.Add($"ISO{isoSpeedRating}");
                            if (fNumber.Denom != 0) camera.Add($"f/{fNumber.Numer / (double)fNumber.Denom:F1}");
                            if (exposureTime.Denom != 0) camera.Add($"{(exposureTime.Numer < exposureTime.Denom ? $"1/{exposureTime.Denom / exposureTime.Numer:F0}" : $"{exposureTime.Numer / (double)exposureTime.Denom:0.####}")}s");
                            if (exposureBias.Denom != 0 && exposureBias.Numer != 0) camera.Add($"{(exposureBias.Sign ? '+' : '-')}{exposureBias.Numer / (double)exposureBias.Denom:0.#}");
                            if (exposureBias.Denom != 0) camera.Add($"{focalLength.Numer / (double)focalLength.Denom:F0}mm");
                            if (camera.Count > 0) infos.Add($"Camera Info : {string.Join(", ", camera)}");
                            #endregion

                            #region Get GPS Geo Info
                            var gps = new List<string>();
                            if (exif.GetGpsLongitude(out GeoCoordinate gpsLon))
                                gps.Add($"{gpsLon.CardinalPoint} {gpsLon.Degree}.{gpsLon.Minute}'{gpsLon.Second}\"".Trim(['\0', ' ', '\n', '\r', '\t']));
                            if (exif.GetGpsLatitude(out GeoCoordinate gpsLat))
                                gps.Add($"{gpsLat.CardinalPoint} {gpsLat.Degree}.{gpsLat.Minute}'{gpsLat.Second}\"".Trim(['\0', ' ', '\n', '\r', '\t']));
                            if (exif.GetGpsAltitude(out decimal gpsAlt))
                                gps.Add($"{gpsAlt:0.##} m".Trim(['\0', ' ', '\n', '\r', '\t']));
                            if (exif.GetGpsDateTimeStamp(out DateTime gdts))
                                gps.Add($"{gdts:yyyy/MM/ddTHH:mm:dd.fffzzz}");
                            if (gps.Count > 0) infos.Add($"GPS         : {string.Join(", ", gps)}");
                            #endregion

                            #region Get Windows Image Info
                            exif.GetTagValue(ExifTag.XpAuthor, out string author, StrCoding.Utf16Le_Byte);
                            exif.GetTagValue(ExifTag.XpSubject, out string subject, StrCoding.Utf16Le_Byte);
                            exif.GetTagValue(ExifTag.XpTitle, out string title, StrCoding.Utf16Le_Byte);
                            exif.GetTagValue(ExifTag.XpKeywords, out string tags, StrCoding.Utf16Le_Byte);
                            exif.GetTagValue(ExifTag.XpComment, out string comments, StrCoding.Utf16Le_Byte);
                            exif.GetTagValue(ExifTag.Copyright, out string copyrights, StrCoding.Utf8);

                            if (!string.IsNullOrEmpty(title))
                                infos.Add($"Title       : {title.Trim()}");
                            if (!string.IsNullOrEmpty(subject))
                                infos.Add($"Subject     : {subject.Trim()}");
                            if (!string.IsNullOrEmpty(author))
                                infos.Add($"Authors     : {author.Trim().TrimEnd(';') + ';'}");
                            if (!string.IsNullOrEmpty(copyrights))
                                infos.Add($"Copyrights  : {copyrights.Trim().TrimEnd(';') + ';'}");
                            if (!string.IsNullOrEmpty(tags))
                                infos.Add($"Tags        : {string.Join(" ", tags.Split(';').Select(t => $"#{t.Trim()}"))}");
                            if (!string.IsNullOrEmpty(comments))
                                infos.Add($"Commants    : {PaddingLines(comments, 14)}");
                            #endregion
                        }
                    }
                    catch (Exception ex) { ReportMessage(ex); }
                    #endregion
                    return (infos);
                });
            }
            return (string.Join(Environment.NewLine, result).Trim());
        }

        private static string GetLabelString(LabeledObject[] items)
        {
            var result = string.Empty;
            if (items is not null && items.Length > 0)
            {
                var padding = items.Select(x => x.Label.Length).Max();
                result = string.Join(Environment.NewLine, items.Select(x => $"Confidence  : {x.Label.PadRight(padding)} ≈ {x.Confidence:F6}"));
            }
            return (result);
        }

        public static Predicate<object>? GetFilter(string filter)
        {
            Predicate<object>? result = null;
            if (!string.IsNullOrEmpty(filter))
            {
                var action = new Func<object, bool>(obj =>
                {
                    var ret = true;
                    if(obj is ImageResultGalleryItem)
                    {
                        var item = obj as ImageResultGalleryItem;
                        if (item is not null && item.Tooltip is not null && !string.IsNullOrEmpty(item.Tooltip))
                        {
                            if(filter.StartsWith('!'))
                                ret = !item.Tooltip.Contains(filter[1..], StringComparison.CurrentCultureIgnoreCase);
                            else
                                ret = item.Tooltip.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
                        }
                    }
                    return (ret);
                });
                result = new Predicate<object>(action);
            }
            return (result);
        }

        public void LoadSetting()
        {
            var setting_file = GetAbsolutePath($"{GetAppName()}.settings");
            settings = Settings.Load(setting_file) ?? new Settings();
            settings.SettingFile = setting_file;

            if (settings.DarkBackground)
            {
                DarkBG.IsChecked = settings.DarkBackground;
                ChangeTheme(settings.DarkBackground);
            }

            _storages_ = settings.StorageList;
            AllFolders.IsChecked = settings.AllFolder;

            if (_storages_ is not null)
            {
                FolderList.ItemsSource = _storages_.Select(s => new ComboBoxItem() { Content = s.ImageFolder, DataContext = s, ToolTip = s.Description });
                if (!string.IsNullOrEmpty(settings.LastImageFolder))
                {
                    var idx = _storages_.Select(s => s.ImageFolder).ToList().IndexOf(settings.LastImageFolder);
                    if (idx >= 0) FolderList.SelectedIndex = idx;
                }
                else FolderList.SelectedIndex = 0;
            }
            if (!File.Exists(setting_file)) settings.Save(setting_file);

            QueryResultLimit.ItemsSource = settings.ResultLimitList;
            QueryResultLimit.SelectedIndex = QueryResultLimit.Items.IndexOf(settings.ResultLimit);
            if (!double.TryParse(QueryResultLimit.Text, out double _)) { QueryResultLimit.Items.IndexOf(settings.ResultLimitList.FirstOrDefault()); };
        }

        public void SaveSetting()
        {
            if (!string.IsNullOrEmpty(settings?.SettingFile))
            {
                settings.Save(settings.SettingFile);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            Style = (Style)FindResource(typeof(Window));
            MinWidth = 1024;
            MinHeight = 720;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadSetting();

                var ww = SystemParameters.PrimaryScreenWidth;
                var wh = SystemParameters.PrimaryScreenHeight;
                var ratio = Width / ww;
                Height = wh * ratio;

                SimilarResultGallery.ItemsSource = GalleryList;

                edResult.IsReadOnly = true;
                edResult.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

                var args = Environment.GetCommandLineArgs();
                if (args.Length > 1 && File.Exists(args[1]))
                {
                    LoadImageFromFiles(args.Skip(1).ToArray());
                }
            }
            catch (Exception ex) { ReportMessage(ex); }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
            if (ctrl)
            {
                try
                {
                    var setting_file = GetAbsolutePath($"{GetAppName()}.settings");
                    settings.AllFolder = AllFolders.IsChecked ?? false;
                    if (int.TryParse(QueryResultLimit.Text, out int limit)) settings.ResultLimit = limit;
                    settings.LastImageFolder = FolderList.Text;

                    settings.Save(setting_file);
                }
                catch (Exception ex) { e.Cancel = true; ReportMessage(ex); }
            }
            else similar?.CancelCreateFeatureData();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (IsLoaded && WindowState != WindowState.Minimized && LastWinState == WindowState.Minimized) ShowLog();
            LastWinState = WindowState;
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
            var win = Keyboard.Modifiers.HasFlag(ModifierKeys.Windows);
            try
            {
                if (e.Key == Key.Enter)
                {
                    if (Tabs.SelectedItem == TabSimilar)
                    {
                        e.Handled = true;
                        var files = SimilarResultGallery.SelectedItems.OfType<ImageResultGalleryItem>().Select(item => item.FullName);
                        ShellOpen(files.ToArray(), openwith: shift, viewinfo: ctrl);
                    }
                }
                else if (e.Key == Key.Delete)
                {
                    if (Tabs.SelectedItem == TabSimilar)
                    {
                    }
                    else if (Tabs.SelectedItem == TabCompare)
                    {
                        foreach (var compare in new System.Windows.Controls.Image[] { CompareL, CompareR })
                        {
                            if (compare.Source is not null) { compare.Source = null; }
                            if (compare.Tag is SKBitmap) (compare.Tag as SKBitmap).Dispose();
                            if (compare.ToolTip is not null) ToolTipService.SetToolTip(compare, null);
                        }
                    }
                }
                else if (e.Key == Key.V && ctrl)
                {
                    if (e.Source == TabSimilar || Tabs.SelectedItem == TabSimilar)
                    {
                        e.Handled = true;
                        QueryImage_Click(QueryImage, e);
                    }
                    else if (e.Source == TabCompare || Tabs.SelectedItem == TabCompare)
                    {
                        e.Handled = true;
                        CompareImage_Click(CompareImage, e);
                    }
                }
                else if (e.Key == Key.C && shift)
                {
                    if (e.Source == TabSimilar || Tabs.SelectedItem == TabSimilar)
                    {
                        e.Handled = true;
                        string sep = $"{Environment.NewLine}================================================================================{Environment.NewLine}";
                        var files = SimilarResultGallery.SelectedItems.OfType<ImageResultGalleryItem>().Select(item => item.Tooltip);
                        if (files.Any()) Clipboard.SetText((sep + string.Join(sep, files) + sep).Trim());
                    }
                }
                else if (e.Key == Key.C && ctrl)
                {
                    if (e.Source == TabSimilar || Tabs.SelectedItem == TabSimilar)
                    {
                        e.Handled = true;
                        var files = SimilarResultGallery.SelectedItems.OfType<ImageResultGalleryItem>().Select(item => item.FullName);
                        if (files.Any()) Clipboard.SetText(string.Join(Environment.NewLine, files));
                    }
                }
            }
            catch (Exception ex) { ReportMessage(ex); }
        }

        private void Window_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle) //e.MiddleButton == MouseButtonState.Released)
            {
                WindowState = WindowState.Minimized;
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {

        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {

        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {

        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data is DataObject)
            {
                if (InDrop && MessageBox.Show("Query will be replaced?", "Continue?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No) return;

                var similar_target = new object[]{ TabSimilar, SimilarViewer, SimilarResultGallery, SimilarResultGallery, SimilarSrc };
                var compare_target = new object[]{ TabCompare, CompareViewer, CompareBoxL, CompareBoxR, CompareL, CompareR };
                if (similar_target.Contains(e.Source))
                    QueryImage_Click(sender, e);
                else if (compare_target.Contains(e.Source))
                    CompareImage_Click(sender, e);
            }
        }

        private void DarkBG_Click(object sender, RoutedEventArgs e)
        {
            if (settings is not null)
            {
                settings.DarkBackground = DarkBG.IsChecked ?? false;
                ChangeTheme(settings.DarkBackground);
            }
        }

        private void AllFolders_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                e.Handled = true;
            }
        }

        private void FolderList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                e.Handled = true;
            }
        }

        private void DBTools_Click(object sender, RoutedEventArgs e)
        {
            if (sender == OpenConfig)
            {
                if (!string.IsNullOrEmpty(settings?.SettingFile))
                {
                    ShellOpen([settings.SettingFile], system: true);
                }
            }
            else if (sender == LoadConfig)
            {
                LoadSetting();
            }
            else if (sender == SaveConfig)
            {
                SaveSetting();
            }
            else if (sender == ClearLog)
            {
                _log_.Clear();
                ShowLog();
            }
            else if (sender == DBLoad)
            {
                var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
                InitSimilar();
                Task.Run(async () => { await LoadFeatureDB(reload: ctrl); });
            }
            else if (sender == DBMake)
            {
                InitSimilar();

                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    e.Handled = true;
                    similar.CancelCreateFeatureData();
                    return;
                }
                if (_storages_?.Count > 0)
                {
                    if (MessageBox.Show($"Will update/create features database, continue it?", "Continue?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (AllFolders.IsChecked ?? false)
                        {
                            Task.Run(async () => { await similar.CreateFeatureData(_storages_); });
                        }
                        else if (FolderList.SelectedIndex >= 0)
                        {
                            var storage = (FolderList.SelectedItem as ComboBoxItem).DataContext as Storage;
                            if (storage is not null) Task.Run(async () => { await similar.CreateFeatureData(storage); });
                        }
                    }
                }
            }
            else if (sender == DBUpdate)
            {
                InitSimilar();
                var items = SimilarResultGallery.SelectedItems.OfType<ImageResultGalleryItem>();
                var files = items.Select(x => x.FullName).ToArray();
                if (_storages_?.Count > 0 && files.Length > 0)
                {
                    if (MessageBox.Show($"Will update features of selected image(s) in database, continue it?", "Continue?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (AllFolders.IsChecked ?? false)
                        {
                            Task.Run(async () => { await similar.UpdateImageFeature(_storages_, files); });
                        }
                        else if (FolderList.SelectedIndex >= 0)
                        {
                            var storage = (FolderList.SelectedItem as ComboBoxItem).DataContext as Storage;
                            if (storage is not null) Task.Run(async () => { await similar.UpdateImageFeature(storage, files); });
                        }
                    }
                }
            }
            else if (sender == DBCancel)
            {
                InitSimilar();
                similar.CancelCreateFeatureData();
            }
            else if (sender == DBClean)
            {
                if (MessageBox.Show("Will clean features database records, continue it?", "Caution!", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    InitSimilar();

                    var all_db = AllFolders.IsChecked ?? false;
                    if (all_db) Task.Run(async () => { await similar.CleanImageFeature(); });
                    else if (FolderList.SelectedIndex >= 0)
                    {
                        var storage = (FolderList.SelectedItem as ComboBoxItem).DataContext as Storage;
                        Task.Run(async () => { await similar.CleanImageFeature(storage); });
                    }
                }
            }
            else if (sender == DBChangePath)
            {

            }
            else if (sender == DBMerge)
            {

            }
            else if (sender == DBRemove)
            {

            }
            else if (sender == DBAdd)
            {

            }
        }

        private async void CompareImage_Click(object sender, RoutedEventArgs e)
        {
            InitSimilar();

            var compare_l_target = new object[]{ CompareBoxL, CompareL };
            var compare_r_target = new object[]{ CompareBoxR, CompareR };

            if (Tabs.SelectedItem == TabSimilar)
            {
                var files = SimilarResultGallery.SelectedItems.OfType<ImageResultGalleryItem>().Select(item => item.FullName);
                if (files.Any()) ShellCompare(files.ToArray());
            }
            else if (Tabs.SelectedItem == TabCompare)
            {
                if (e is DragEventArgs)
                {
                    var imgs = await LoadImageFromDataObject((e as DragEventArgs).Data);
                    if (imgs.Count > 1)
                    {
                        (var bmp0, var skb0, var file0) = imgs[0];
                        (var bmp1, var skb1, var file1) = imgs[1];
                        ToolTipService.SetToolTip(CompareBoxL, await GetImageInfo(file0));
                        CompareL.Source = bmp0;
                        CompareL.Tag = skb0;
                        ToolTipService.SetToolTip(CompareBoxR, await GetImageInfo(file1));
                        CompareR.Source = bmp1;
                        CompareR.Tag = skb1;
                    }
                    else if (imgs.Count > 0)
                    {
                        var pt = (e as DragEventArgs).GetPosition(CompareViewer);
                        var in_compare_l = pt.X < CompareViewer.ActualWidth / 2.0;
                        var in_compare_r = pt.X > CompareViewer.ActualWidth / 2.0;

                        (var bmp, var skb, var file) = imgs[0];
                        if (compare_l_target.Contains(e.Source) || in_compare_l || CompareBoxL.IsMouseOver)
                        {
                            ToolTipService.SetToolTip(CompareBoxL, await GetImageInfo(file));
                            CompareL.Source = bmp;
                            CompareL.Tag = skb;
                        }
                        else if (compare_r_target.Contains(e.Source) || in_compare_r || CompareBoxR.IsMouseOver)
                        {
                            ToolTipService.SetToolTip(CompareBoxR, await GetImageInfo(file));
                            CompareR.Source = bmp;
                            CompareR.Tag = skb;
                        }
                    }
                }
                else if (Clipboard.ContainsImage())
                {
                    var imgs = await LoadImageFromDataObject(Clipboard.GetDataObject());
                    if (imgs.Count > 0)
                    {
                        var pt = Mouse.GetPosition(CompareViewer);
                        var in_compare_l = pt.X < CompareViewer.ActualWidth / 2.0;
                        var in_compare_r = pt.X > CompareViewer.ActualWidth / 2.0;

                        (var bmp, var skb, _) = imgs[0];
                        if (CompareL.Source is null || in_compare_l || CompareL.IsMouseOver)
                        {
                            ToolTipService.SetToolTip(CompareBoxL, null);
                            CompareL.Source = bmp;
                            CompareL.Tag = skb;
                        }
                        else if (CompareR.Source is null || in_compare_r || CompareR.IsMouseOver)
                        {
                            ToolTipService.SetToolTip(CompareBoxR, null);
                            CompareR.Source = bmp;
                            CompareR.Tag = skb;
                        }
                    }
                }

                if (CompareL.Source != null && CompareL.Tag is SKBitmap && CompareR.Source != null && CompareR.Tag is SKBitmap)
                {
                    var skb0 = CompareL.Tag as SKBitmap;
                    var skb1 = CompareR.Tag as SKBitmap;

                    var score = await Task.Run(async () => { return(await similar.CompareImage(skb0, skb1)); });
                    ToolTipService.SetToolTip(TabCompare, $"{score:F4}");
                }
            }
            GC.Collect();
        }

        private bool IsQuering = false;
        private async void QueryImage_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedItem != TabSimilar) Tabs.SelectedItem = TabSimilar;

            InitSimilar();

            var storage = new Storage();
            var folder = string.Empty;
            var feature_db = string.Empty;
            var all_db = AllFolders.IsChecked ?? false;

            if (FolderList.SelectedIndex >= 0)
            {
                storage = (FolderList.SelectedItem as ComboBoxItem).DataContext as Storage;
                folder = storage.ImageFolder;
                feature_db = all_db ? string.Empty : storage.DatabaseFile;
            }

            #region Pre-processing query source
            if (e is DragEventArgs)
            {
                var imgs = await LoadImageFromDataObject((e as DragEventArgs).Data);
                if (imgs.Count > 0)
                {
                    (var bmp, var skb, var file) = imgs.FirstOrDefault();
                    if (bmp is not null) SimilarSrc.Source = bmp;
                    if (skb is not null) SimilarSrc.Tag = skb;
                    ToolTipService.SetToolTip(SimilarSrcBox, await GetImageInfo(file));
                }
            }
            else if (Clipboard.ContainsImage())
            {
                (var bmp, var skb) = await LoadImageFromClipboard();
                if (bmp is not null) SimilarSrc.Source = bmp;
                if (skb is not null) SimilarSrc.Tag = skb;
                ToolTipService.SetToolTip(SimilarSrcBox, null);
            }
            else if (!string.IsNullOrEmpty(EditQueryFile.Text))
            {
                var file = EditQueryFile.Text.Trim();

                if (!System.IO.Path.IsPathRooted(file))
                {
                    file = System.IO.Path.Combine(folder, file);
                }

                (var bmp, var skb, _) = LoadImageFromFile(GetAbsolutePath(file));

                if (bmp is not null) SimilarSrc.Source = bmp;
                if (skb is not null) SimilarSrc.Tag = skb;
                ToolTipService.SetToolTip(SimilarSrcBox, await GetImageInfo(file));
            }
            #endregion

            if (SimilarSrc.Tag is SKBitmap)
            {
                if (!double.TryParse(QueryResultLimit.Text, out double limit)) limit = 10;
                var show_in_shell = OpenInShell.IsChecked ?? false;
                var skb_src = SimilarSrc.Tag as SKBitmap;

                //QueryImage.IsEnabled = false;

                GalleryList.Clear();
                await Task.Run(async () =>
                {
                    IsQuering = true;
                    await LoadFeatureDB();

                    var queries = await similar.QueryImageScore(skb_src, feature_db, limit: limit, labels: true);
                    var imlist = queries?.Results;
                    var labels = queries?.Labels;
                    if (labels is not null)
                    {
                        var similar_tips = GetLabelString(labels);
                        if (!string.IsNullOrEmpty(similar_tips))
                        {
                            ReportMessage(similar_tips);
                            SimilarSrcBox.Dispatcher.Invoke(() =>
                            {
                                string? tips = null;
                                var tips_old = SimilarSrcBox.ToolTip is string && !string.IsNullOrEmpty(SimilarSrcBox.ToolTip as string) ? SimilarSrcBox.ToolTip as string : string.Empty;
                                if (string.IsNullOrEmpty(tips_old)) tips = similar_tips;
                                else
                                {
                                    var tips_lines = tips_old.Split(["\n\r", "\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries);
                                    tips = string.Join(Environment.NewLine, tips_lines.Where(l => !l.StartsWith("Confidence  :")).Append(similar_tips));
                                }
                                ToolTipService.SetToolTip(SimilarSrcBox, tips);
                            });
                        }
                    }

                    if (imlist?.Count > 0)
                    {
                        ReportMessage(string.Join(Environment.NewLine, imlist.Select(im => $"{im.Key}, {im.Value:F4}")));

                        #region Open result in explorer by using search-ms protocol
                        if (show_in_shell)
                        {
                            var keys = imlist.Select(im => im.Key);
                            var folders = keys.Select(k => System.IO.Path.GetDirectoryName(k)).Distinct();
                            var query_words = keys.Select(k => System.IO.Path.GetFileName(k)).Take(5);
                            var query = $"({string.Join(" OR ", query_words.Select(w => $"\"{w}\""))})";
                            CopyText(query);
                            ShellSearch(Uri.EscapeDataString(query), folders);
                        }
                        #endregion

                        #region show results
                        foreach (var im in imlist)
                        {
                            using (var skb = similar.LoadImage(im.Key))
                            {
                                if (skb is null) continue;

                                var ratio = Math.Max(skb.Width, skb.Height) / 240.0;

                                using (var skb_thumb = skb.Resize(new SKSizeI((int)Math.Ceiling(skb.Width / ratio), (int)Math.Ceiling(skb.Height / ratio)), SKFilterQuality.High))
                                {
                                    try
                                    {
                                        var tooltip = await GetImageInfo(im.Key);
                                        #region Add result item to Gallery
                                        await SimilarResultGallery.Dispatcher.InvokeAsync(() =>
                                        {
                                            GalleryList.Add(new ImageResultGalleryItem()
                                            {
                                                Source = ToBitmapSource(skb_thumb),
                                                FullName = GetAbsolutePath(im.Key),
                                                FileName = Path.GetFileName(im.Key),
                                                Similar = $"{im.Value:F4}",
                                                Tooltip = tooltip,
                                                HasExifTag = tooltip.Contains("Tags        :"),
                                                Favoriteed = tooltip.Contains("Rank=4") || tooltip.Contains("Rank=5"),
                                            });
                                        }, System.Windows.Threading.DispatcherPriority.Normal);
                                        #endregion
                                    }
                                    catch (Exception ex) { ReportMessage(ex); }
                                }
                            }
                        }

                        await SimilarResultGallery.Dispatcher.InvokeAsync(() =>
                        {
                            SimilarResultGallery.ItemsSource ??= GalleryList;
                            if (SimilarResultGallery.Items?.Count > 0)
                            {
                                SimilarResultGallery.ScrollIntoView(SimilarResultGallery.Items?[0]);
                            }

                            var info_items = new List<string>
                            {
                                $"Displayed : {$"{SimilarResultGallery.Items?.Count}",6}",
                                $"Selected  : {$"{SimilarResultGallery.SelectedItems?.Count}", 6}",
                                $"Total     : {$"{GalleryList?.Count}", 6}",
                            };
                            TabSimilar.ToolTip = string.Join(Environment.NewLine, info_items);

                            QueryImage.IsEnabled = true;

                            IsQuering = false;
                            System.Media.SystemSounds.Beep.Play();
                        }, System.Windows.Threading.DispatcherPriority.Normal);
                        #endregion
                    }

                    GC.Collect();
                });

            }
        }

        private void QueryImageLabel_Click(object sender, RoutedEventArgs e)
        {
            InitSimilar();

            if (Tabs.SelectedItem == TabSimilar)
            {
                var items = SimilarResultGallery.SelectedItems.OfType<ImageResultGalleryItem>();
                if (items.Any())
                {
                    Task.Run(async () =>
                    {
                        var sw = Stopwatch.StartNew();
                        ReportMessage("Quering Image Labels", TaskStatus.Running);
                        foreach (var item in items)
                        {
                            if (item.Labels is null)
                            {
                                item.Labels = await similar.GetImageLabel(item.FullName);
                                if (item.Labels?.Length > 0)
                                {
                                    item.Tooltip += Environment.NewLine + GetLabelString(item.Labels);
                                    item.UpdateToolTip();
                                }
                            }
                        }
                        sw?.Stop();
                        ReportMessage($"Quered Image Labels. Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                        GC.Collect();
                    });
                }
            }
            else if (Tabs.SelectedItem == TabCompare)
            {
                var items = new Dictionary<System.Windows.Controls.Image, Viewbox>{ { CompareL, CompareBoxL }, { CompareR, CompareBoxR } };
                Task.Run(() =>
                {
                    foreach (var item in items)
                    {
                        item.Key?.Dispatcher.InvokeAsync(async () =>
                        {
                            if (item.Key?.Tag is SKBitmap)
                            {
                                var value = ToolTipService.GetToolTip(item.Value);
                                var tooltip = value is string ? value as string : string.Empty;
                                if (string.IsNullOrEmpty(tooltip) || !tooltip.Contains("Confidence  :"))
                                {
                                    var Labels = await similar.GetImageLabel(item.Key?.Tag as SKBitmap);
                                    if (Labels?.Length > 0)
                                    {
                                        tooltip += Environment.NewLine + GetLabelString(Labels);
                                        ToolTipService.SetToolTip(item.Value, tooltip.Trim());
                                    }
                                }
                            }
                        });
                    }
                    GC.Collect();
                });
            }
        }

        private void ResultFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            SimilarResultGallery.Dispatcher.InvokeAsync(() =>
            {
                SimilarResultGallery.Items.Filter = GetFilter(ResultFilter.Text);
            });
        }

        private bool InDrop = false;
        private void SimilarResultGallery_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.MiddleButton == MouseButtonState.Pressed || e.XButton1 == MouseButtonState.Pressed)
                {
                    var files = new StringCollection();
                    foreach (var item in SimilarResultGallery.SelectedItems)
                    {
                        if (item is ImageResultGalleryItem)
                        {
                            var image_result = item as ImageResultGalleryItem;
                            files.Add(image_result.FullName);
                        }
                    }
                    if (files.Count > 0)
                    {
                        InDrop = true;
                        var dp = new DataObject();
                        dp.SetFileDropList(files);
                        dp.SetData("text/uri-list", files);
                        //dp.SetData("text/plain", file);
                        //dp.SetData("text/html", file);
                        //dp.SetData("Text", file);
                        dp.SetData("FileName", files[0]);
                        dp.SetData("FileNameW", files[0]);
                        dp.SetData("UsingDefaultDragImage", true);
                        //dp.SetData("DragImageBits", null);
                        //dp.SetData("DragContext", null);
                        DragDrop.DoDragDrop(SimilarResultGallery, dp, DragDropEffects.Copy);
                    }
                }
            }
            catch (Exception ex) { ReportMessage(ex); }
            finally { InDrop = false; }
        }

        private void SimilarResultGallery_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            List<string> names = [ "PART_IMAGEGRID", "PART_IMAGEBOX", "PART_IMAGE", "PART_IMAGEFILE", "PART_IMAGESIMILAR", "PART_", "Bd" ];
            if (e.Source is ListView && names.Contains((e.OriginalSource as FrameworkElement).Name))
            {
                if (e.ChangedButton == MouseButton.XButton1) { }
                var shift = Keyboard.Modifiers == ModifierKeys.Shift;
                var alt = Keyboard.Modifiers == ModifierKeys.Alt;

                var files = SimilarResultGallery.SelectedItems.OfType<ImageResultGalleryItem>().Select(item => item.FullName);
                if (files.Any()) ShellOpen(files.ToArray(), openwith: shift, viewinfo: alt || e.ChangedButton == MouseButton.Right);
            }
            e.Handled = true;
        }

        private void SimilarResultGallery_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SimilarResultGallery.Dispatcher.InvokeAsync(() =>
            {
                var info_items = new List<string>
                {
                    $"Displayed : {$"{SimilarResultGallery.Items?.Count}", 6}",
                    $"Selected  : {$"{SimilarResultGallery.SelectedItems?.Count}", 6}",
                    $"Total     : {$"{GalleryList?.Count}", 6}",
                };
                TabSimilar.ToolTip = string.Join(Environment.NewLine, info_items);
            });
        }

    }

    public class ImageResultGalleryItem : INotifyPropertyChanged, IDisposable
    {
        public ImageSource? Source { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Similar { get; set; } = string.Empty;
        public string Tooltip { get; set; } = string.Empty;
        public bool HasExifTag { get; set; } = false;
        public bool Favoriteed { get; set; } = false;
        public bool Deleteed { get; set; } = false;
        public LabeledObject[]? Labels { get; set; } = null;

        public void UpdateToolTip()
        {
            NotifyPropertyChanged(nameof(Tooltip));
        }

        #region Dispose Helper
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Properties Handler
        public event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

}