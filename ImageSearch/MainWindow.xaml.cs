using ImageSearch.Search;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
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
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ImageSearch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<ImageResultGallery> GalleryList = new ObservableCollection<ImageResultGallery>();

        private List<string> _log_ = new();

        private Settings settings = new Settings();

        private Similar? similar = null;

        private List<Storage> _storages_ = new List<Storage>();

        private string GetAbsolutePath(string relativePath)
        {
            string fullPath = string.Empty;
            //FileInfo _dataRoot = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            FileInfo _dataRoot = new FileInfo(new Uri(System.Reflection.Assembly.GetExecutingAssembly().Location).LocalPath);
            if (_dataRoot.Directory is DirectoryInfo)
            {
                string assemblyFolderPath = _dataRoot.Directory.FullName;

                fullPath = System.IO.Path.Combine(assemblyFolderPath, relativePath);
            }
            return fullPath;
        }

        private string GetAppName()
        {
            //FileInfo _dataRoot = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            return (System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location));
        }

        private void ReportBatch(BatchProgressInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                if (info is BatchProgressInfo)
                {
                    //Cursor = info.State == TaskStatus.Running ? Cursors.Wait : Cursors.Arrow;

                    _log_.Add($"{info.FileName}, [{info.Current}/{info.Total}][{info.Percentage:P}]");
                    edResult.Text = _log_.Count > 1000 ? string.Join(Environment.NewLine, _log_.TakeLast(1000)) : string.Join(Environment.NewLine, _log_);
                    edResult.ScrollToEnd();
                    edResult.SelectionStart = edResult.Text.Length;
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

        private (BitmapSource?, SKBitmap?) LoadImageFromStream(Stream? stream)
        {
            BitmapImage? bmp = null;
            SKBitmap? skb = null;
            if (stream is Stream && stream.CanSeek && stream.CanRead)
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

        private (BitmapSource?, SKBitmap?) LoadImageFromFile(string file)
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

            using (HttpClient client = new HttpClient())
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
            List<(BitmapSource?, SKBitmap?)> imgs = new List<(BitmapSource?, SKBitmap?)> ();
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
                                if (files is string[])
                                {
                                    if (files.Count() > 1)
                                    {
                                        imgs.Add(LoadImageFromFile(files[0]));
                                        imgs.Add(LoadImageFromFile(files[1]));
                                    }
                                    else if (files.Count() > 0)
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
                IDataObject dataPackage = Clipboard.GetDataObject();
                var imgs = await LoadImageFromDataObject(dataPackage);
                if (imgs.Count > 0) (bmp, skb) = imgs.FirstOrDefault();
            }
            catch (Exception ex) { ReportMessage($"{ex.Message}"); }
            return ((bmp, skb));
        }

        private async Task LoadFeatureDB()
        {
            InitSimilar();

            var storage = new Storage();
            var folder = string.Empty;
            var feature_db = string.Empty;

            if (FolderList.SelectedIndex >= 0)
            {
                storage = (FolderList.SelectedItem as ComboBoxItem).DataContext as Storage;
                folder = storage.Folder;
                feature_db = storage.DatabaseFile;
            }

            if (AllFolders.IsChecked ?? false)
                await similar.LoadFeatureData(similar.StorageList);
            else
                await similar.LoadFeatureData(storage);
        }

        private void InitSimilar()
        {
            if (similar == null)
            {
                similar = new Similar
                {
                    ModelLocation = settings.Model,
                    StorageList = _storages_,
                    ReportProgressBar = progress,
                    BatchReportAction = new Action<BatchProgressInfo>(ReportBatch),
                    MessageReportAction = new Action<string, TaskStatus>(ReportMessage)
                };
            }
        }

        public static void ShellSearch(string query, IEnumerable<string?> folders)
        {
            if (!string.IsNullOrEmpty(query) && folders is IEnumerable<string>)
            {
                var targets = folders.Distinct().Where(d => Directory.Exists(d));
                if (targets.Count() > 0)
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

                _storages_ = settings.StorageList;
                AllFolders.IsChecked = settings.AllFolder;

                if (_storages_ is List<Storage>)
                {
                    foreach (var storage in _storages_)
                    {
                        FolderList.Items.Add(new ComboBoxItem() { Content = storage.Folder, DataContext = storage });
                    }
                    if (!string.IsNullOrEmpty(settings.LastImageFolder))
                    {
                        var idx = _storages_.Select(s => s.Folder).ToList().IndexOf(settings.LastImageFolder);
                        if (idx >= 0) FolderList.SelectedIndex = idx;
                    }
                    else FolderList.SelectedIndex = 0;
                }
                if (!File.Exists(setting_file)) settings.Save(setting_file);

                QueryResultLimit.ItemsSource = new int[] { 5, 10, 15, 20, 25, 30, 35, 40, 45, 50 };
                QueryResultLimit.SelectedIndex = QueryResultLimit.Items.IndexOf(settings.ResultLimit);
            }
            catch (Exception ex) { ReportMessage(ex.Message); }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data is DataObject)
            {
                if (e.Source == TabSimilar || e.Source == SimilarViewer || e.Source == SimilarResultGallery || e.Source == SimilarResultGallery)
                    btnQuery_Click(sender, e);
                else if (e.Source == TabCompare || e.Source == CompareViewer || e.Source == CompareBoxL || e.Source == CompareBoxR || e.Source == CompareL || e.Source == CompareR)
                {
                    btnCompareFile_Click(sender, e);
                }
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {

        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {

        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {

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

        private async void btnQuery_Click(object sender, RoutedEventArgs e)
        {
            InitSimilar();

            var imlist = new List<KeyValuePair<string, double>>();

            var storage = new Storage();
            var folder = string.Empty;
            var feature_db = string.Empty;
            var all_db = AllFolders.IsChecked ?? false;

            if (FolderList.SelectedIndex >= 0)
            {
                storage = (FolderList.SelectedItem as ComboBoxItem).DataContext as Storage;
                folder = storage.Folder;
                feature_db = all_db ? string.Empty : storage.DatabaseFile;
            }

            GalleryList.Clear();

            if (sender == btnQueryClip && Clipboard.ContainsImage())
            {
                (var bmp, var skb) = await LoadImageFromClipboard();
                if (bmp is BitmapSource) SimilarSrc.Source = bmp.Clone();
                if (skb is SKBitmap) SimilarSrc.Tag = skb;
            }
            else if (sender == btnQueryFile && !string.IsNullOrEmpty(edQueryFile.Text))
            {
                var file = edQueryFile.Text.Trim();

                if (!System.IO.Path.IsPathRooted(file))
                {
                    file = System.IO.Path.Combine(folder, file);
                }

                (var bmp, var skb) = LoadImageFromFile(GetAbsolutePath(file));

                if (bmp is BitmapSource) SimilarSrc.Source = bmp.Clone();
                if (skb is SKBitmap) SimilarSrc.Tag = skb;
            }
            else if (e is DragEventArgs)
            {
                var imgs = await LoadImageFromDataObject((e as DragEventArgs).Data);
                if (imgs.Count > 0)
                {
                    (var bmp, var skb) = imgs.FirstOrDefault();
                    if (bmp is BitmapSource) SimilarSrc.Source = bmp.Clone();
                    if (skb is SKBitmap) SimilarSrc.Tag = skb;
                }
            }

            if (SimilarSrc.Tag is SKBitmap)
            {
                await LoadFeatureDB();

                var skb_src = SimilarSrc.Tag as SKBitmap;
                imlist = await similar.QueryImageScore(skb_src, feature_db);

                if (imlist is List<KeyValuePair<string, double>> && imlist.Count() > 0)
                {
                    ReportMessage(string.Join(Environment.NewLine, imlist.Select(im => $"{im.Key}, {im.Value:F4}")));

                    if (OpenInShell.IsChecked ?? false)
                    {
                        var keys = imlist.Select(im => im.Key);
                        var folders = keys.Select(k => System.IO.Path.GetDirectoryName(k)).Distinct();
                        var query_words = keys.Select(k => System.IO.Path.GetFileName(k)).Take(5);
                        var query = $"({string.Join(" OR ", query_words.Select(w => $"\"{w}\""))})";
                        Clipboard.SetText(query);
                        ShellSearch(Uri.EscapeDataString(query), folders);
                    }

                    GalleryList.Clear();
                    foreach (var im in imlist)
                    {
                        using (var skb = similar.LoadImage(im.Key))
                        {
                            var ratio = Math.Max(skb.Width, skb.Height) / 240.0;

                            using (var skb_thumb = skb.Resize(new SKSizeI((int)Math.Ceiling(skb.Width / ratio), (int)Math.Ceiling(skb.Height / ratio)), SKFilterQuality.High))
                            {
                                using (var sk_data = skb_thumb.Encode(SKEncodedImageFormat.Png, 100))
                                {
                                    using (var ms = new MemoryStream())
                                    {
                                        sk_data.SaveTo(ms);
                                        ms.Seek(0, SeekOrigin.Begin);

                                        (var bmp, _) = LoadImageFromStream(ms);

                                        FileInfo fi = new FileInfo(im.Key);
                                        GalleryList.Add(new ImageResultGallery()
                                        {
                                            Source = bmp.Clone(),
                                            FullName = GetAbsolutePath(im.Key),
                                            FileName = System.IO.Path.GetFileName(im.Key),
                                            Similar = $"{im.Value:F4}",
                                            Tooltip = $"FullName : {GetAbsolutePath(im.Key)}{Environment.NewLine}FileSize : {fi.Length}{Environment.NewLine}FileData : {fi.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss zzz")}",
                                        });
                                    }
                                }
                            }
                        }
                    }
                    SimilarResultGallery.ItemsSource = GalleryList;
                    if (GalleryList.Count > 0) SimilarResultGallery.ScrollIntoView(GalleryList.First());
                }
            }
        }

        private async void btnCompareFile_Click(object sender, RoutedEventArgs e)
        {
            InitSimilar();

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
                    if (e.Source == CompareL)
                    {
                        CompareL.Source = bmp;
                        CompareL.Tag = skb;
                    }
                    else if (e.Source == CompareR)
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

        private async void btnMakeDB_Click(object sender, RoutedEventArgs e)
        {
            InitSimilar();

            if (Keyboard.Modifiers == ModifierKeys.Alt) similar.CancelCreateFeatureData();
            if (_storages_ is List<Storage> && _storages_.Count() > 0)
            {
                if (AllFolders.IsChecked ?? false)
                {
                    similar.CreateFeatureData(_storages_);
                }
                else if (FolderList.SelectedIndex >= 0)
                {
                    var storage = (FolderList.SelectedItem as ComboBoxItem).DataContext as Storage;
                    if (storage is Storage) await similar.CreateFeatureData(storage);
                }
            }
        }

        private async void btnCleanDB_Click(object sender, RoutedEventArgs e)
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

        private void ImageGallery_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var shift = Keyboard.Modifiers == ModifierKeys.Shift;
            if (SimilarResultGallery.SelectedItems.Count > 0)
            {
                try
                {
                    var item = SimilarResultGallery.SelectedItem as ImageResultGallery;
                    if (shift)
                        Process.Start("openwith.exe", item.FullName);
                    else
                    {
                        Process.Start("explorer.exe", item.FullName);
                    }
                }
                catch (Exception ex) { ReportMessage(ex.Message); }
            }
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