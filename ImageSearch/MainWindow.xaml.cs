using ImageSearch.Search;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace ImageSearch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<ImageResultGallery> GalleryList = new ObservableCollection<ImageResultGallery>();

        private List<string> _log_ = new();

        private List<Storage> _storages_ = new List<Storage>();

        public static void Search(string query, IEnumerable<string?> folders)
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

        private Similar? similar = null;

        private void ReportBatch(BatchProgressInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                if (info is BatchProgressInfo)
                {
                    Cursor = info.State == TaskStatus.Running ? Cursors.Wait : Cursors.Arrow;

                    _log_.Add($"{info.FileName}, {info.Current/info.Total}[{info.Percentage:P}]");
                    edResult.Text = _log_.Count > 1000 ? string.Join(Environment.NewLine, _log_.TakeLast(1000)) : string.Join(Environment.NewLine, _log_);
                    edResult.ScrollToEnd();
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

                    _log_.Add($"{info}");
                    edResult.Text = _log_.Count > 1000 ? string.Join(Environment.NewLine, _log_.TakeLast(1000)) : string.Join(Environment.NewLine, _log_);
                    edResult.ScrollToEnd();
                }
            });
        }

        private (BitmapSource?, SKBitmap?) GetImageFromClipboard()
        {
            BitmapImage? bmp = null;
            SKBitmap? skb = null;

            var supported_fmts = new string[] { "PNG", "image/png", "image/tif", "image/tiff", "image/webp", "image/xpm", "image/ico", "image/cur", "image/jpg", "image/jpeg", "image/bmp", "DeviceIndependentBitmap", "image/wbmp", "Text" };
            IDataObject dataPackage = Clipboard.GetDataObject();
            var fmts = dataPackage.GetFormats(true);
            foreach (var fmt in supported_fmts)
            {
                if (fmts.Contains(fmt) && dataPackage.GetDataPresent(fmt, true))
                {
                    try
                    {
                        var obj = dataPackage.GetData(fmt, true);
                        if (obj is MemoryStream)
                        {
                            var ms = obj as MemoryStream;

                            ms.Seek(0, SeekOrigin.Begin);
                            bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = ms;
                            bmp.EndInit();
                            bmp.Freeze();

                            ms.Seek(0, SeekOrigin.Begin);
                            skb = SKBitmap.Decode(ms);
                            break;
                        }
                    }
                    catch (Exception ex) { ReportMessage($"{fmt} : {ex.Message}"); }
                }
            }
            return ((bmp, skb));
        }

        private void InitSimilar()
        {
            if (similar == null)
            {
                similar = new Similar
                {
                    progressbar = progress,
                    StorageList = _storages_,
                    BatchReportAction = new Action<BatchProgressInfo>(ReportBatch),
                    MessageReportAction = new Action<string, TaskStatus>(ReportMessage)
                };
            }
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _storages_.Add(new Storage() { Folder = @"images", DatabaseFile = @$"data\images_224x224_resnet50v2.h5", Recurice = false });
            _storages_.Add(new Storage() { Folder = @"D:\MyImages\Pixiv", DatabaseFile = @$"data\pixiv_224x224_resnet50v2.h5", Recurice = false });
            _storages_.Add(new Storage() { Folder = @"D:\Downloads\_firefox\mmd", DatabaseFile = @$"data\mmd_224x224_resnet50v2.h5", Recurice = false });
            if (_storages_ is List<Storage>)
            {
                foreach (var storage in _storages_)
                {
                    FolderList.Items.Add(new ComboBoxItem() { Content = storage.Folder, DataContext = storage } );
                }
                FolderList.SelectedIndex = 0;
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

            if (sender == btnQueryClip && Clipboard.ContainsImage())
            {
                (var bmp, var skb) = GetImageFromClipboard();
                if (bmp is BitmapSource) imageSrc.Source = bmp.Clone();
                if (skb is SKBitmap) imlist = await similar.QueryImageScore(skb, feature_db);

                //var image = Clipboard.GetImage();

                //using (var bmp = similar.FromImageSource(image))
                //{
                //    imageSrc.Source = image.Clone();
                //    if (bmp is SKBitmap) imlist = await similar.QueryImageScore(bmp, feature_db);
                //}
            }
            else if (sender == btnQueryFile && !string.IsNullOrEmpty(edQueryFile.Text))
            {
                var file = edQueryFile.Text.Trim();

                if (!System.IO.Path.IsPathRooted(file))
                {
                    file = System.IO.Path.Combine(folder, file);
                }

                imageSrc.Source = BitmapFrame.Create(new Uri(System.IO.Path.GetFullPath(file)));

                var skb = similar.FromImageSource(imageSrc.Source);
                if (skb is SKBitmap)
                    imlist = await similar.QueryImageScore(skb, feature_db);
                else
                    imlist = await similar.QueryImageScore(file, feature_db);
            }

            if (imlist is List<KeyValuePair<string, double>> && imlist.Count() > 0)
            {
                ReportMessage(string.Join(Environment.NewLine, imlist.Select(im => $"{im.Key}, {im.Value:F4}")));
                //edResult.Text += string.Join(Environment.NewLine, imlist.Select(im => $"{im.Key}, {im.Value:F4}")) + Environment.NewLine;

                if (OpenInShell.IsChecked ?? false)
                {
                    var keys = imlist.Select(im => im.Key);
                    var folders = keys.Select(k => System.IO.Path.GetDirectoryName(k)).Distinct();
                    var query_words = keys.Select(k => System.IO.Path.GetFileName(k)).Take(5);
                    var query = $"({string.Join(" OR ", query_words.Select(w => $"\"{w}\""))})";
                    Clipboard.SetText(query);
                    Search(Uri.EscapeDataString(query), folders);
                }

                GalleryList.Clear();
                foreach (var im in imlist)
                {
                    using (var skb = similar.LoadImage(im.Key))
                    {
                        var ratio = Math.Max(skb.Width, skb.Height) / 240.0;
                        using (var ms = new MemoryStream())
                        {
                            using (var skb_thumb = skb.Resize(new SKSizeI((int)Math.Ceiling(skb.Width / ratio), (int)Math.Ceiling(skb.Height / ratio)), SKFilterQuality.High))
                            {
                                using (var sk_data = skb_thumb.Encode(SKEncodedImageFormat.Png, 100))
                                {
                                    sk_data.SaveTo(ms);
                                    ms.Seek(0, SeekOrigin.Begin);
                                    
                                    //var frame = BitmapFrame.Create(ms);
                                    //frame.Freeze();

                                    var bmp = new BitmapImage();
                                    bmp.BeginInit();
                                    bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                    bmp.DecodePixelWidth = skb_thumb.Width;
                                    bmp.DecodePixelHeight = skb_thumb.Height;
                                    bmp.StreamSource = ms;
                                    bmp.EndInit();
                                    bmp.Freeze();

                                    FileInfo fi = new FileInfo(im.Key);
                                    GalleryList.Add(new ImageResultGallery()
                                    {
                                        Source = bmp.Clone(),
                                        FullName = System.IO.Path.GetFullPath(im.Key),
                                        FileName = System.IO.Path.GetFileName(im.Key),
                                        Similar = $"{im.Value:F4}",
                                        Tooltip = $"FullName : {System.IO.Path.GetFullPath(im.Key)}{Environment.NewLine}FileSize : {fi.Length}{Environment.NewLine}FileData : {fi.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss zzz")}",
                                    });
                                }
                            }
                        }
                    }
                }
                ImageGallery.ItemsSource = GalleryList;
            }
        }

        private void btnMakeDB_Click(object sender, RoutedEventArgs e)
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
                    if (storage is Storage) similar.CreateFeatureData(storage);
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

        private async void FolderList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                e.Handled = true;
                await LoadFeatureDB();
            }
        }

        private async void AllFolders_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                e.Handled = true;
                await LoadFeatureDB();
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