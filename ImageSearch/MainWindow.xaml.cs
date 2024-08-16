using CompactExifLib;
using ImageSearch.Search;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ImageSearch
{
#pragma warning disable IDE0063

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly List<string> _log_ = [];

        private readonly ObservableCollection<ImageResultGallery> GalleryList = [];

        private Settings settings = new();

        private Similar? similar = null;

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

        private void ReportBatch(BatchProgressInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                if (info is not null)
                {
                    //Cursor = info.State == TaskStatus.Running ? Cursors.Wait : Cursors.Arrow;
                    var msg = $"{info.FileName}, [{info.Current}/{info.Total}][{info.Percentage:P}]";
                    _log_.Add(msg);
                    edResult.Text = _log_.Count > 1000 ? string.Join(Environment.NewLine, _log_.TakeLast(1000)) : string.Join(Environment.NewLine, _log_);
                    edResult.ScrollToEnd();
                    edResult.SelectionStart = edResult.Text.Length;
                    LatestMessage.Text = msg;
                }
            });
        }

        private void ReportMessage(string info, TaskStatus state = TaskStatus.Created)
        {
            Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(info))
                {
                    //Cursor = state == TaskStatus.Running ? Cursors.Wait : Cursors.Arrow;
                    if (progress.Value == 100) progress.IsIndeterminate = state == TaskStatus.Running;

                    _log_.Add($"{info}");
                    edResult.Text = _log_.Count > 1000 ? string.Join(Environment.NewLine, _log_.TakeLast(1000)) : string.Join(Environment.NewLine, _log_);
                    edResult.ScrollToEnd();
                    edResult.SelectionStart = edResult.Text.Length;
                    LatestMessage.Text = info.Split(Environment.NewLine).FirstOrDefault();
                }
            });
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
                            //pattern.Negate(Channels.RGB);
                            //pattern.Opaque(MagickColors.Black, new MagickColor("#202020"));
                            //pattern.Opaque(MagickColors.White, new MagickColor("#303030"));
                            opacity = 1.0;
                        }
                        var checkerboard = new ImageBrush(ToBitmapSource(skb)) { TileMode = TileMode.Tile, Opacity = opacity, ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 32, 32) };
                        SimilarViewer.Background = checkerboard;
                        SimilarViewer.InvalidateVisual();
                        CompareViewer.Background = checkerboard;
                        CompareViewer.InvalidateVisual();
                        SimilarResultGallery.Foreground = new SolidColorBrush(Colors.Silver);
                    }
                }
            }
            catch (Exception ex) { ReportMessage(ex.Message); }
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
                    catch (Exception ex) { ReportMessage(ex.Message); }
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

                            //var frame = BitmapFrame.Create(ms);
                            //frame.Freeze();

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
                        catch (Exception ex) { ReportMessage(ex.Message); }
                    }
                }
            }
            return (result);
        }

        private static (BitmapSource?, SKBitmap?) LoadImageFromStream(Stream? stream)
        {
            BitmapImage? bmp = null;
            SKBitmap? skb = null;
            if (stream is not null && stream.CanSeek && stream.CanRead)
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
            }
            return ((bmp, skb));
        }

        private static (BitmapSource?, SKBitmap?) LoadImageFromFile(string file)
        {
            BitmapSource? bmp = null;
            SKBitmap? skb = null;
            if (File.Exists(file))
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(file)))
                {
                    (bmp, skb) = LoadImageFromStream(ms);
                }
            }
            return ((bmp, skb));
        }

        private async Task<(BitmapSource?, SKBitmap?)> LoadImageFromWeb(Uri uri)
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
                                (bmp, skb) = LoadImageFromStream(ms);
                            }
                        }
                    }
                    catch (Exception ex) { ReportMessage(ex.Message); }
                }
            }
            return ((bmp, skb));
        }

        private async Task<List<(BitmapSource?, SKBitmap?)>> LoadImageFromDataObject(IDataObject dataPackage)
        {
            List<(BitmapSource?, SKBitmap?)> imgs = [];
            if (dataPackage is DataObject)
            {
                var supported_fmts = new string[] { "PNG", "image/png", "image/tif", "image/tiff", "image/webp", "image/xpm", "image/ico", "image/cur", "image/jpg", "image/jpeg", "img/jfif", "image/bmp", "DeviceIndependentBitmap", "image/wbmp", "FileDrop", "Text" };
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
                                    if (files.Length > 1)
                                    {
                                        imgs.Add(LoadImageFromFile(files[0]));
                                        imgs.Add(LoadImageFromFile(files[1]));
                                    }
                                    else if (files.Length > 0)
                                    {
                                        imgs.Add(LoadImageFromFile(files[0]));
                                    }
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
                IDataObject dataPackage = System.Windows.Clipboard.GetDataObject();
                var imgs = await LoadImageFromDataObject(dataPackage);
                if (imgs.Count > 0) (bmp, skb) = imgs.FirstOrDefault();
            }
            catch (Exception ex) { ReportMessage($"{ex.Message}"); }
            return ((bmp, skb));
        }

        private async Task LoadFeatureDB()
        {
            InitSimilar();

            await Dispatcher.Invoke(async () =>
            {
                var storage = new Storage();
                var folder = string.Empty;
                var feature_db = string.Empty;

                if (FolderList.SelectedIndex >= 0)
                {
                    storage = (FolderList.SelectedItem as ComboBoxItem).DataContext as Storage;
                    folder = storage.ImageFolder;
                    feature_db = storage.DatabaseFile;
                }

                if (AllFolders.IsChecked ?? false)
                    await similar.LoadFeatureData(similar.StorageList);
                else
                    await similar.LoadFeatureData(storage);
            });
        }

        private void InitSimilar()
        {
            similar ??= new Similar
            {
                ModelLocation = settings.ModelFile,
                ModelInputColumnName = settings.ModelInput,
                ModelOutputColumnName = settings.ModelOutput,

                StorageList = _storages_,

                ReportProgressBar = progress,
                BatchReportAction = new Action<BatchProgressInfo>(ReportBatch),
                MessageReportAction = new Action<string, TaskStatus>(ReportMessage)
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
                else
                {

                }
            }
        }

        private void ShellCompare(string[] files)
        {
            if (files is not null && files.Length > 1)
            {
                files = files.Where(f => File.Exists(f)).Select(f => $"{f}").Take(2).ToArray();
                if (!string.IsNullOrEmpty(settings.ImageCompareCmd) && File.Exists(settings.ImageCompareCmd))
                    Process.Start(settings.ImageCompareCmd, [settings.ImageCompareOpt, files.First(), files.Last()]);
            }
        }

        private void ShellOpen(string[] files, bool openwith = false, bool viewinfo = false)
        {
            if (files is not null && files.Length > 0)
            {
                files = files.Where(f => File.Exists(f)).Select(f => $"{f}").ToArray();

                foreach (var file in files)
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
                            if (string.IsNullOrEmpty(settings.ImageViewerCmd) || !File.Exists(settings.ImageViewerCmd))
                                Process.Start("explorer.exe", file);
                            else
                                Process.Start(settings.ImageViewerCmd, [settings.ImageViewerOpt, file]);
                        }
                    }
                    catch (Exception ex) { ReportMessage(ex.Message); }
                }
            }
        }

        private void CopyText(string text)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                }
                catch (Exception ex) { ReportMessage(ex.Message); };
            });
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
            var setting_file = GetAbsolutePath($"{GetAppName()}.settings");
            try
            {
                settings = Settings.Load(setting_file) ?? new Settings();

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

                QueryResultLimit.ItemsSource = new int[] { 5, 10, 12, 15, 18, 20, 24, 25, 30, 35, 40, 45, 50, 60 };
                QueryResultLimit.SelectedIndex = QueryResultLimit.Items.IndexOf(settings.ResultLimit);


            }
            catch (Exception ex) { ReportMessage(ex.Message); }
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
                catch (Exception ex) { e.Cancel = true; ReportMessage(ex.Message); }
            }
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
                        var files = SimilarResultGallery.SelectedItems.OfType<ImageResultGallery>().Select(item => item.FullName);
                        ShellOpen(files.ToArray(), openwith: shift, viewinfo: ctrl);
                    }
                }
                else if (e.Key == Key.V && ctrl)
                {
                    if (e.Source == TabSimilar || Tabs.SelectedItem == TabSimilar)
                    {
                        e.Handled = true;
                        QueryImage_Click(QueryClip, e);
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
                        var files = SimilarResultGallery.SelectedItems.OfType<ImageResultGallery>().Select(item => item.Tooltip);
                        if (files.Any()) Clipboard.SetText((sep + string.Join(sep, files) + sep).Trim());
                    }
                }
                else if (e.Key == Key.C && ctrl)
                {
                    if (e.Source == TabSimilar || Tabs.SelectedItem == TabSimilar)
                    {
                        e.Handled = true;
                        var files = SimilarResultGallery.SelectedItems.OfType<ImageResultGallery>().Select(item => item.FullName);
                        if (files.Any()) Clipboard.SetText(string.Join(Environment.NewLine, files));
                    }
                }
            }
            catch (Exception ex) { ReportMessage(ex.Message); }
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
                var similar_target = new object[]{ TabSimilar, SimilarViewer, SimilarResultGallery, SimilarResultGallery, SimilarSrc };
                var compare_target = new object[]{ TabCompare, CompareViewer, CompareBoxL, CompareBoxR, CompareL, CompareR };
                if (similar_target.Contains(e.Source))
                    QueryImage_Click(sender, e);
                else if (compare_target.Contains(compare_target))
                {
                    CompareImage_Click(sender, e);
                }
            }
        }

        private void DarkBG_Click(object sender, RoutedEventArgs e)
        {
            if (settings is Settings)
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

        private async void BtnMakeDB_Click(object sender, RoutedEventArgs e)
        {
            InitSimilar();

            if (Keyboard.Modifiers == ModifierKeys.Alt) similar.CancelCreateFeatureData();
            if (_storages_ is not null && _storages_.Count > 0)
            {
                if (AllFolders.IsChecked ?? false)
                {
                    similar.CreateFeatureData(_storages_);
                }
                else if (FolderList.SelectedIndex >= 0)
                {
                    var storage = (FolderList.SelectedItem as ComboBoxItem).DataContext as Storage;
                    if (storage is not null) await similar.CreateFeatureData(storage);
                }
            }
        }

        private async void BtnCleanDB_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Will clean features database records", "Caution!", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                InitSimilar();

                var all_db = AllFolders.IsChecked ?? false;
                if (all_db) await similar.CleanImageFeature();
                else
                {
                    if (FolderList.SelectedIndex >= 0)
                    {
                        var storage = (FolderList.SelectedItem as ComboBoxItem).DataContext as Storage;
                        await similar.CleanImageFeature(storage);
                    }
                }
            }
        }

        private void ClipImage_Click(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedItem == TabSimilar)
            {
                QueryImage_Click(QueryImage, e);
            }
            else if (Tabs.SelectedItem == TabCompare)
            {
                CompareImage_Click(CompareImage, e);
            }
        }

        private async void QueryImage_Click(object sender, RoutedEventArgs e)
        {
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

            if (e is DragEventArgs)
            {
                var imgs = await LoadImageFromDataObject((e as DragEventArgs).Data);
                if (imgs.Count > 0)
                {
                    (var bmp, var skb) = imgs.FirstOrDefault();
                    if (bmp is not null) SimilarSrc.Source = bmp;
                    if (skb is not null) SimilarSrc.Tag = skb;
                }
            }
            else if (Clipboard.ContainsImage())
            {
                (var bmp, var skb) = await LoadImageFromClipboard();
                if (bmp is not null) SimilarSrc.Source = bmp;
                if (skb is not null) SimilarSrc.Tag = skb;
            }
            else if (!string.IsNullOrEmpty(EditQueryFile.Text))
            {
                var file = EditQueryFile.Text.Trim();

                if (!System.IO.Path.IsPathRooted(file))
                {
                    file = System.IO.Path.Combine(folder, file);
                }

                (var bmp, var skb) = LoadImageFromFile(GetAbsolutePath(file));

                if (bmp is not null) SimilarSrc.Source = bmp;
                if (skb is not null) SimilarSrc.Tag = skb;
            }

            GalleryList.Clear();

            if (SimilarSrc.Tag is SKBitmap)
            {
                if (!int.TryParse(QueryResultLimit.Text, out int limit)) limit = 10;
                var show_in_shell = OpenInShell.IsChecked ?? false;
                var skb_src = SimilarSrc.Tag as SKBitmap;

                await Task.Run(async () =>
                {
                    await LoadFeatureDB();

                    var imlist = await similar.QueryImageScore(skb_src, feature_db, limit: limit);

                    if (imlist.Count > 0)
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
                        SimilarResultGallery.Dispatcher.Invoke(() =>
                        {
                            GalleryList.Clear();
                            foreach (var im in imlist)
                            {
                                using (var skb = similar.LoadImage(im.Key))
                                {
                                    if (skb is not null)
                                    {
                                        var ratio = Math.Max(skb.Width, skb.Height) / 240.0;

                                        using (var skb_thumb = skb.Resize(new SKSizeI((int)Math.Ceiling(skb.Width / ratio), (int)Math.Ceiling(skb.Height / ratio)), SKFilterQuality.High))
                                        {
                                            using (var sk_data = skb_thumb.Encode(SKEncodedImageFormat.Png, 100))
                                            {
                                                using (var ms = new MemoryStream())
                                                {
                                                    try
                                                    {
                                                        sk_data.SaveTo(ms);
                                                        ms.Seek(0, SeekOrigin.Begin);

                                                        (var bmp, _) = LoadImageFromStream(ms);

                                                        var tooltips = new List<string>();

                                                        FileInfo fi = new (im.Key);
                                                        tooltips.Add($"Full Name  : {GetAbsolutePath(im.Key)}");
                                                        tooltips.Add($"File Size  : {fi.Length:N0} Bytes");
                                                        tooltips.Add($"File Date  : {fi.LastWriteTime:yyyy/MM/dd HH:mm:ss zzz}");

                                                        try
                                                        {
                                                            var exif = new ExifData(im.Key);
                                                            if (exif != null)
                                                            {
                                                                exif.GetDateDigitized(out var date_digital);
                                                                exif.GetDateTaken(out var date_taken);
                                                                exif.GetTagValue(ExifTag.XpAuthor, out string author, StrCoding.Utf16Le_Byte);
                                                                exif.GetTagValue(ExifTag.XpSubject, out string subject, StrCoding.Utf16Le_Byte);
                                                                exif.GetTagValue(ExifTag.XpTitle, out string title, StrCoding.Utf16Le_Byte);
                                                                exif.GetTagValue(ExifTag.XpKeywords, out string tags, StrCoding.Utf16Le_Byte);
                                                                exif.GetTagValue(ExifTag.XpComment, out string comments, StrCoding.Utf16Le_Byte);
                                                                exif.GetTagValue(ExifTag.Copyright, out string copyrights, StrCoding.Utf8);

                                                                tooltips.Add($"Taken Date : {date_taken:yyyy/MM/dd HH:mm:ss zzz}");
                                                                tooltips.Add($"Title      : {title.Trim()}");
                                                                tooltips.Add($"Subject    : {subject.Trim()}");
                                                                tooltips.Add($"Authors    : {author.Trim().TrimEnd(';') + ';'}");
                                                                tooltips.Add($"Copyrights : {copyrights.Trim().TrimEnd(';') + ';'}");
                                                                tooltips.Add($"Tags       : {string.Join(" ", tags.Split(';').Select(t => $"#{t.Trim()}"))}");
                                                                //tooltips.Add($"Commants   : {comments}");
                                                            }
                                                        }
                                                        catch (Exception ex) { ReportMessage(ex.Message); }

                                                        GalleryList.Add(new ImageResultGallery()
                                                        {
                                                            Source = bmp,
                                                            FullName = GetAbsolutePath(im.Key),
                                                            FileName = System.IO.Path.GetFileName(im.Key),
                                                            Similar = $"{im.Value:F4}",
                                                            Tooltip = string.Join(Environment.NewLine, tooltips),
                                                        });
                                                    }
                                                    catch (Exception ex) { ReportMessage(ex.Message); }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            SimilarResultGallery.ItemsSource = GalleryList;
                            if (GalleryList.Count > 0) SimilarResultGallery.ScrollIntoView(GalleryList.First());
                        });
                        #endregion
                    }
                });
            }
        }

        private async void CompareImage_Click(object sender, RoutedEventArgs e)
        {
            InitSimilar();

            if (Tabs.SelectedItem == TabSimilar)
            {
                var files = SimilarResultGallery.SelectedItems.OfType<ImageResultGallery>().Select(item => item.FullName);
                if (files.Any()) ShellCompare(files.ToArray());
            }
            else if (Tabs.SelectedItem == TabCompare)
            {
                if (e is DragEventArgs)
                {
                    var imgs = await LoadImageFromDataObject((e as DragEventArgs).Data);
                    if (imgs.Count > 1)
                    {
                        (var bmp0, var skb0) = imgs[0];
                        (var bmp1, var skb1) = imgs[1];
                        CompareL.Source = bmp0;
                        CompareL.Tag = skb0;
                        CompareR.Source = bmp1;
                        CompareR.Tag = skb1;
                    }
                    else if (imgs.Count > 0)
                    {
                        (var bmp, var skb) = imgs[0];
                        if (e.Source == CompareL || CompareL.IsMouseOver)
                        {
                            CompareL.Source = bmp;
                            CompareL.Tag = skb;
                        }
                        else if (e.Source == CompareR || CompareR.IsMouseOver)
                        {
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
                        (var bmp, var skb) = imgs[0];
                        if (CompareL.Source is null || CompareL.IsMouseOver)
                        {
                            CompareL.Source = bmp;
                            CompareL.Tag = skb;
                        }
                        else if (CompareR.Source is null || CompareR.IsMouseOver)
                        {
                            CompareR.Source = bmp;
                            CompareR.Tag = skb;
                        }
                    }
                }

                if (CompareL.Source != null && CompareL.Tag is SKBitmap && CompareR.Source != null && CompareR.Tag is SKBitmap)
                {
                    var skb0 = CompareL.Tag as SKBitmap;
                    var skb1 = CompareR.Tag as SKBitmap;

                    var score = await similar.CompareImage(skb0, skb1);
                    ToolTipService.SetToolTip(TabCompare, $"{score:F4}");
                }
            }
        }

        private void SimilarResultGallery_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var shift = Keyboard.Modifiers == ModifierKeys.Shift;
            var alt = Keyboard.Modifiers == ModifierKeys.Alt;

            var files = SimilarResultGallery.SelectedItems.OfType<ImageResultGallery>().Select(item => item.FullName);
            if (files.Any()) ShellOpen(files.ToArray(), openwith: shift, viewinfo: alt || e.ChangedButton == MouseButton.Right);
        }

    }

    public class ImageResultGallery
    {
        public ImageSource? Source { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Similar { get; set; } = string.Empty;
        public string Tooltip { get; set; } = string.Empty;
    }

}