using ImageSearch.Search;
using SkiaSharp;
using System.Diagnostics;
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

namespace ImageSearch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<string> _log_ = new();

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

        private void BatchReport(BatchProgressInfo info)
        {
            Dispatcher.Invoke(() =>
            {
                if (info is BatchProgressInfo)
                {
                    Cursor = info.State == TaskStatus.Running ? Cursors.Wait : Cursors.Arrow;

                    _log_.Add($"{info.FileName}, {info.Percentage:P}");
                    edResult.Text = _log_.Count > 1000 ? string.Join(Environment.NewLine, _log_.TakeLast(1000)) : string.Join(Environment.NewLine, _log_);
                    edResult.ScrollToEnd();
                }
            });
        }

        private void MessageReport(string info, TaskStatus state = TaskStatus.Created)
        {
            Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(info))
                {
                    Cursor = state == TaskStatus.Running ? Cursors.Wait : Cursors.Arrow;

                    _log_.Add($"{info}");
                    edResult.Text = _log_.Count > 1000 ? string.Join(Environment.NewLine, _log_.TakeLast(1000)) : string.Join(Environment.NewLine, _log_);
                    edResult.ScrollToEnd();
                }
            });
        }

        private void InitSimilar()
        {
            if (similar == null)
            {
                similar = new Similar
                {
                    progressbar = progress,
                    BatchReportAction = new Action<BatchProgressInfo>(BatchReport),
                    MessageReportAction = new Action<string, TaskStatus>(MessageReport)
                };

                similar.LoadFeatureData(@"data\test_224x224_resnet50v2.h5");
            }
        }

        private void btnQuery_Click(object sender, RoutedEventArgs e)
        {
            InitSimilar();

            var imlist = new List<KeyValuePair<string, double>>();

            if (sender == btnQueryClip && Clipboard.ContainsImage())
            {
                var image = Clipboard.GetImage();
                using (var ms = new MemoryStream())
                {
                    ////var stride = ((image.PixelWidth * image.Format.BitsPerPixel + 31) / 32) * 4;
                    //var stride = ((image.PixelWidth * image.Format.BitsPerPixel + 31) >> 5) << 2;
                    //byte[] buf = new byte[stride * (int)image.Height];
                    //image.CopyPixels(buf, stride, 0);

                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    encoder.Save(ms);

                    ms.Seek(0, SeekOrigin.Begin);
                    using (var bmp = SKBitmap.Decode(ms))
                    {
                        //imageSrc.Source = bmp.Resize()
                        using (SKCanvas canvas = new(bmp))
                        {
                            //canvasSrc.
                            //canvas.draw
                            //canvasSrc.dr
                        }
                        imlist = similar.QueryImageScore(bmp);
                    }
                }
            }
            else if (sender == btnQueryFile && !string.IsNullOrEmpty(edQueryFile.Text))
            {
                imlist = similar.QueryImageScore($@"images\{edQueryFile.Text.Trim()}");
            }

            if (imlist is List<KeyValuePair<string, double>> && imlist.Count() > 0)
            {
                MessageReport(string.Join(Environment.NewLine, imlist.Select(im => $"{im.Key}, {im.Value:F4}")));
                //edResult.Text += string.Join(Environment.NewLine, imlist.Select(im => $"{im.Key}, {im.Value:F4}")) + Environment.NewLine;

                var keys = imlist.Select(im => im.Key);
                var folders = keys.Select(k => System.IO.Path.GetDirectoryName(k)).Distinct();
                var query_words = keys.Select(k => System.IO.Path.GetFileName(k)).Take(5);
                var query = $"({string.Join(" OR ", query_words.Select(w => $"\"{w}\""))})";
                Clipboard.SetText(query);
                Search(Uri.EscapeDataString(query), folders);
            }
        }

        private void btnMakeDB_Click(object sender, RoutedEventArgs e)
        {
            InitSimilar();

            var folder = "images";
            similar.CreateFeatureData(feature_db: @$"data\{folder}_224x224_resnet50v2.h5", folder: folder);
        }

        private void btnCleanDB_Click(object sender, RoutedEventArgs e)
        {
            InitSimilar();

            var folder = "images";
            similar.CleanImageFeatureAsync(feature_db: @$"data\{folder}_224x224_resnet50v2.h5", folder: folder, recuice: false);
        }
    }
}