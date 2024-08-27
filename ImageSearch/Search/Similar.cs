using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.Trainers;
using Microsoft.ML.Transforms;
using Microsoft.ML.Transforms.Image;
using Microsoft.ML.Transforms.Onnx;

using NumSharp;
using PureHDF;
using PureHDF.Filters;
using SkiaSharp;

namespace ImageSearch.Search
{
#pragma warning disable IDE0063
#pragma warning disable IDE0305
#pragma warning disable SYSLIB1045

    public class Storage
    {
        public ITransformer? Model { get; set; } = null;

        public string ImageFolder { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Recurice { get; set; } = false;
        public string ModelName { get; set; } = string.Empty;
        public string ModelFile { get; set; } = string.Empty;
        public string ModelInput { get; set; } = string.Empty;
        public string ModelOutput { get; set; } = string.Empty;
        public string DatabaseFile { get; set; } = string.Empty;
        public string[] DatabaseFiles { get; set; } = [];
        public string[] IccludeFiles { get; set; } = [];
        public string[] ExcludeFiles { get; set; } = [];
        public string[] IccludeFolders { get; set; } = [];
        public string[] ExcludeFolders { get; set; } = [];
    }

    public enum BatchRunningMode { Parallel, ParallelAsync, MultiTask, ForLoop };

    public class BatchTaskInfo
    {
        public BatchRunningMode ParallelMode { get; set; } = BatchRunningMode.ForLoop;
        public int ParallelLimit { get; set; } = 5;
        public TimeSpan ParallelTimeOut { get; set; } = TimeSpan.FromSeconds(5);
        public ulong CheckPoint { get; set; } = 1000;
        public CancellationToken? CancelToken { get; set; } = new CancellationToken();

        public bool MergeAllFeats { get; set; } = false;
        public bool Recurise { get; set; } = false;
        public string DatabaseName { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public List<Storage> Folders { get; set; } = [];
        public string FileName { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public long Current { get; set; } = 0;
        public long Total { get; set; } = 0;
        public double Percentage { get { return (Total > 0 && Current >= 0 ? (double)Current / Total : 0); } }
        public TaskStatus State { get; set; } = TaskStatus.Created;
        public Exception? Error { get; set; }
        public dynamic? Result { get; set; }
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime CurrentTime { get; set; } = DateTime.Now;
        public DateTime LastestTime { get; set; } = DateTime.Now;
        public TimeSpan ElapsedTime { get { return (CurrentTime - StartTime); } }
        public TimeSpan EstimateTime { get { return (Total > 0 && Current >= 0 ? TimeSpan.FromTicks((CurrentTime - LastestTime).Ticks * (Total - Current)) : TimeSpan.FromTicks(0)); } }
    }

    public class FeatureData
    {
        public Storage FeatureStore { get; set; } = new Storage();
        public string FeatureDB { get; set; } = string.Empty;
        public NDArray Feats { get; set; } = np.zeros(0);
        public string[] Names { get; set; } = [];
        public bool Loaded { get; set; } = false;
    }

    internal class Similar
    {
        private static readonly string[] exts = [ ".jpg", ".jpeg", ".bmp", ".png", ".tif", ".tiff", ".gif", ".webp" ];
        private static readonly string[] tiff_exts = [ ".tif", ".tiff" ];

        public Settings? Setting { get; set; } = null;

        private static string GetAbsolutePath(string relativePath)
        {
            string fullPath = string.Empty;
            //FileInfo _dataRoot = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            FileInfo _dataRoot = new (new Uri(System.Reflection.Assembly.GetExecutingAssembly().Location).LocalPath);
            if (_dataRoot.Directory is not null)
            {
                string assemblyFolderPath = _dataRoot.Directory.FullName;

                fullPath = Path.Combine(assemblyFolderPath, relativePath);
            }
            return fullPath;
        }

        public Action<string, TaskStatus>? MessageReportAction;

        private void ReportMessage(string info, TaskStatus state = TaskStatus.Created)
        {
            if (MessageReportAction is not null && !string.IsNullOrEmpty(info))
            {
                Application.Current.Dispatcher.Invoke(MessageReportAction, priority: DispatcherPriority.Normal, info, state);
                //MessageReportAction.Invoke(info, state);
            }
        }

        private void ReportMessage(Exception ex, TaskStatus state = TaskStatus.Created)
        {
            if (MessageReportAction is not null && ex is not null)
            {
                Application.Current.Dispatcher.Invoke(MessageReportAction, priority: DispatcherPriority.Normal, $"{ex.StackTrace?.Split().LastOrDefault()} : {ex.Message}", state);
                //MessageReportAction.Invoke(info, state);
            }
        }

        #region ProgressBar action
        public ProgressBar? ReportProgressBar { get; set; }
        #endregion

        private static readonly int IMG_Width = 224;
        private static readonly int IMG_HEIGHT = 224;
        private System.Drawing.Size IMG_SIZE = new(IMG_Width, IMG_HEIGHT);

        public string ModelLocation { get; set; } = string.Empty;
        public string ModelInputColumnName { get; set; } = string.Empty;
        public string ModelOutputColumnName { get; set; } = string.Empty;

        // Create MLContext
        private readonly MLContext mlContext = new(seed: 1)
        {
            FallbackToCpu = true,
            GpuDeviceId = null//0
        };

        private ITransformer? _model_;

        private PredictionEngine<ModelInput, Prediction>? _predictionEngine_;

        private readonly List<FeatureData> _features_ = [];

        public List<Storage> StorageList { get; set; } = [];

        #region background work thread
        private readonly SemaphoreSlim BatchTaskIdle = new(1,1);
        private CancellationTokenSource BatchCancel = new();
        public TaskStatus RunningStatue { get { return (BatchTaskIdle?.CurrentCount > 0 ? TaskStatus.WaitingForActivation : TaskStatus.Running); } }

        public Action<BatchTaskInfo>? BatchReportAction;

        private void ReportBatch(BatchTaskInfo info)
        {
            if (info is not null)
            {
                BatchReportAction?.Invoke(info);
            }
        }

        private void InitBatchTask()
        {
            try
            {
            }
            catch (Exception ex) { ReportMessage(ex); }
        }

        private void ReportProgress(double percentage)
        {
            ReportProgressBar?.Dispatcher.Invoke(() =>
            {
                ReportProgressBar.Value = (int)percentage;
            });
        }

        private async Task<bool> CreateFeatureDataAsync(BatchTaskInfo info, CancellationToken? cancelToken = null)
        {
            var result = true;

            if (info is not null && BatchTaskIdle?.CurrentCount <= 0)
            {
                var sw = Stopwatch.StartNew();
                ConcurrentDictionary<string, float[]> feats = new();
                try
                {
                    var cancel = cancelToken ?? info.CancelToken ?? new CancellationToken();

                    info.Folders ??= [];
                    if (info.Folders.Count <= 0 &&
                        !string.IsNullOrEmpty(info.DatabaseName) &&
                        !string.IsNullOrEmpty(info.FolderName) && Directory.Exists(info.FolderName))
                    {
                        info.Folders.Add(new Storage() { DatabaseFile = info.DatabaseName, ImageFolder = info.FolderName, Recurice = info.Recurise });
                    }

                    foreach (var storage in info.Folders)
                    {
                        if (cancel.IsCancellationRequested) { result = false; break; }

                        ReportProgress(0);

                        var folder = GetAbsolutePath(storage.ImageFolder);
                        var feats_db = GetAbsolutePath(storage.DatabaseFile);
                        var dir_name = Path.GetFileName(folder);
                        var npz_file = $@"data\{dir_name}_checkpoint_latest.npz";

                        feats.Clear();
                        var feat_obj = _features_.Where(f => f.FeatureDB.Equals(feats_db)).FirstOrDefault();
                        if (feat_obj == null)
                        {
                            if (File.Exists(storage.DatabaseFile))
                            {
                                await LoadFeatureData(storage);
                                feat_obj = _features_.Where(f => f.FeatureDB.Equals(feats_db)).FirstOrDefault();
                            }
                            else
                            {
                                feat_obj = new FeatureData() { FeatureDB = storage.DatabaseFile };
                                _features_.Add(feat_obj);
                            }
                        }

                        #region Loading latest feats dataset npz
                        if (cancel.IsCancellationRequested) { result = false; break; }

                        ConcurrentDictionary<string, float[]> feats_list = new();
                        if (np.size(feat_obj.Feats) > 0)
                        {
                            var feats_a = feat_obj.Feats.ToMuliDimArray<float>() as float[,];
                            var names_a = feat_obj.Names;
                            for (var i = 0; i < feats_a.GetLength(0); i++)
                            {
                                if (string.IsNullOrEmpty(names_a[i])) continue;
                                var row = new List<float>();
                                for (var j = 0; j < feats_a.GetLength(1); j++)
                                {
                                    row.Add(feats_a[i, j]);
                                }
                                feats_list[names_a[i]] = row.ToArray();
                            }
                        }

                        if (cancel.IsCancellationRequested) { result = false; break; }

                        if (File.Exists(npz_file))
                        {
                            try
                            {
                                using (var npz_fs = new FileStream(npz_file, FileMode.Open, FileAccess.Read))
                                {
                                    var checkpoint = np.Load_Npz<float[]>(npz_fs);
                                    foreach (var kv in checkpoint) feats[kv.Key] = kv.Value;
                                }
                            }
                            catch (Exception ex) { ReportMessage(ex); }
                        }
                        #endregion

                        if (cancel.IsCancellationRequested) { result = false; break; }

                        var option = storage.Recurice ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                        var files = Directory.GetFiles(folder, "*.*", option).Where(f => exts.Contains(Path.GetExtension(f).ToLower()));
                        var diffs = files.Except(feats_list.Keys).Except(feats.Keys).ToArray();
                        int count = diffs.Length;
                        uint index = 0;

                        SemaphoreSlim MultiTask = new(info.ParallelLimit, info.ParallelLimit);
                        var opt = new ParallelOptions
                        {
                            MaxDegreeOfParallelism = info.ParallelLimit,
                            CancellationToken = cancel
                        };

                        if (info.ParallelMode == BatchRunningMode.Parallel)
                        {
                            var ret = Parallel.ForEach(diffs, opt, (file, loopstate, elementIndex) =>
                            {
                                try
                                {
                                    if (cancel.IsCancellationRequested) { result = false; loopstate.Stop(); return; }

                                    index++;
                                    if (string.IsNullOrEmpty(file)) return;

                                    #region Extracting image feature and store it to feats dataset
                                    //var feat = await ExtractImaegFeature(file);
                                    var feat = ExtractImaegFeature(file).GetAwaiter().GetResult();
                                    if (feat is not null) feats[file] = feat;

                                    var percent = (int)Math.Ceiling((float)index / count * 100.0);
                                    ReportProgress(percent);
                                    #endregion

                                    if (cancel.IsCancellationRequested) { result = false; loopstate.Stop(); return; }

                                    #region Save feats dataset for every CheckPoint
                                    var current = feats.Count;
                                    if (info.CheckPoint > 0 && current % (uint)info.CheckPoint == 0)
                                    {
                                        try
                                        {
                                            var cp_npz_file = $@"data\{dir_name}_checkpoint_{current:00000000}.npz";
                                            np.Save_Npz(feats.ToDictionary(kv => kv.Key, kv => kv.Value as Array), cp_npz_file);
                                            ReportMessage($"{cp_npz_file} saved!");
                                        }
                                        catch (Exception ex) { ReportMessage(ex); }
                                    }
                                    #endregion

                                    info.FileName = file;
                                    info.Current = index;
                                    info.Total = count;
                                    ReportBatch(info);
                                }
                                catch (Exception ex) { ReportMessage(ex); }
                            });
                        }
                        else if (info.ParallelMode == BatchRunningMode.ParallelAsync)
                        {
                            await Parallel.ForEachAsync(diffs, opt, async (file, cancel) =>
                            {
                                try
                                {
                                    if (cancel.IsCancellationRequested) { result = false; return; }

                                    index++;
                                    if (string.IsNullOrEmpty(file)) return;

                                    #region Extracting image feature and store it to feats dataset
                                    var feat = await ExtractImaegFeature(file);
                                    if (feat is not null) feats[file] = feat;

                                    var percent = (int)Math.Ceiling((float)index / count * 100.0);
                                    ReportProgress(percent);
                                    #endregion

                                    if (cancel.IsCancellationRequested) { result = false; return; }

                                    #region Save feats dataset for every CheckPoint
                                    var current = feats.Count;
                                    if (info.CheckPoint > 0 && current % (uint)info.CheckPoint == 0)
                                    {
                                        try
                                        {
                                            var cp_npz_file = $@"data\{dir_name}_checkpoint_{current:00000000}.npz";
                                            np.Save_Npz(feats.ToDictionary(kv => kv.Key, kv => kv.Value as Array), cp_npz_file);
                                            ReportMessage($"{cp_npz_file} saved!");
                                        }
                                        catch (Exception ex) { ReportMessage(ex); }
                                    }
                                    #endregion

                                    info.FileName = file;
                                    info.Current = index;
                                    info.Total = count;
                                    ReportBatch(info);
                                }
                                catch (Exception ex) { ReportMessage(ex); }
                            });
                        }
                        else if (info.ParallelMode == BatchRunningMode.MultiTask)
                        {
                            var tasks = diffs.Select(async file =>
                            {
                                if (await MultiTask.WaitAsync(info.ParallelTimeOut, cancel))
                                {
                                    try
                                    {
                                        if (cancel.IsCancellationRequested) { result = false; return; }

                                        index++;
                                        if (string.IsNullOrEmpty(file)) return;

                                        #region Extracting image feature and store it to feats dataset
                                        var feat = await ExtractImaegFeature(file);
                                        if (!string.IsNullOrEmpty(file) && feat is not null) feats[file] = feat;

                                        var percent = (int)Math.Ceiling((float)index / count * 100.0);
                                        ReportProgress(percent);
                                        #endregion

                                        if (cancel.IsCancellationRequested) { result = false; return; }

                                        #region Save feats dataset for every CheckPoint
                                        var current = feats.Count;
                                        if (info.CheckPoint > 0 && current % (uint)info.CheckPoint == 0)
                                        {
                                            try
                                            {
                                                var cp_npz_file = $@"data\{dir_name}_checkpoint_{current:00000000}.npz";
                                                np.Save_Npz(feats.ToDictionary(kv => kv.Key, kv => kv.Value as Array), cp_npz_file);
                                                ReportMessage($"{cp_npz_file} saved!");
                                            }
                                            catch (Exception ex) { ReportMessage(ex); }
                                        }
                                        #endregion

                                        info.FileName = file;
                                        info.Current = index;
                                        info.Total = count;
                                        ReportBatch(info);
                                    }
                                    catch (Exception ex) { ReportMessage(ex); }
                                    finally { MultiTask?.Release(); }
                                }
                            });
                            await Task.WhenAll(tasks);
                        }
                        else if (info.ParallelMode == BatchRunningMode.ForLoop)
                        {
                            foreach (var file in diffs)
                            {
                                if (cancel.IsCancellationRequested) { result = false; break; }
                                if (await MultiTask?.WaitAsync(info.ParallelTimeOut, cancel))
                                {
                                    try
                                    {
                                        if (cancel.IsCancellationRequested) { result = false; break; }

                                        index++;
                                        if (string.IsNullOrEmpty(file)) break;

                                        #region Extracting image feature and store it to feats dataset
                                        var feat = await ExtractImaegFeature(file);
                                        if (!string.IsNullOrEmpty(file) && feat is not null) feats[file] = feat;

                                        var percent = (int)Math.Ceiling((float)index / count * 100.0);
                                        ReportProgress(percent);
                                        #endregion

                                        if (cancel.IsCancellationRequested) { result = false; break; }

                                        #region Save feats dataset for every CheckPoint
                                        var current = feats.Count;
                                        if (info.CheckPoint > 0 && current % (uint)info.CheckPoint == 0)
                                        {
                                            try
                                            {
                                                var cp_npz_file = $@"data\{dir_name}_checkpoint_{current:00000000}.npz";
                                                np.Save_Npz(feats.ToDictionary(kv => kv.Key, kv => kv.Value as Array), cp_npz_file);
                                                ReportMessage($"{cp_npz_file} saved!");
                                            }
                                            catch (Exception ex) { ReportMessage(ex); }
                                        }
                                        #endregion

                                        info.FileName = file;
                                        info.Current = index;
                                        info.Total = count;
                                        ReportBatch(info);
                                    }
                                    catch (Exception ex) { ReportMessage(ex); }
                                    finally { MultiTask?.Release(); }
                                }
                            }
                        }

                        if (count > 0 && !feats.IsEmpty && feats?.Count == count && feat_obj is not null)
                        {
                            if (cancel.IsCancellationRequested) { result = false; break; }

                            #region Save feats dataset for current worker
                            try
                            {
                                if (File.Exists(npz_file)) File.Move(npz_file, $"{npz_file}.lastgood", true);
                                using (var npz_fs = new FileStream(npz_file, FileMode.CreateNew, FileAccess.Write))
                                {
                                    np.Save_Npz(feats.ToDictionary(kv => kv.Key, kv => kv.Value as Array), npz_fs);
                                    ReportMessage($"{npz_file} saved!");
                                }
                                var cp_npz_files = Directory.GetFiles("data", $@"{dir_name}_checkpoint_*.npz", SearchOption.TopDirectoryOnly);
                                foreach (var npz in cp_npz_files.Where(f => Regex.IsMatch(f, @"checkpoint_\d+\.npz", RegexOptions.IgnoreCase))) File.Delete(npz);
                            }
                            catch (Exception ex) { ReportMessage(ex); }
                            #endregion

                            if (cancel.IsCancellationRequested) { result = false; break; }

                            #region Save feats to HDF5
                            var feats_new = feats_list.UnionBy(feats, kv => kv.Key).ToDictionary();

                            feat_obj.Names = feats_new.Keys.ToArray();

                            feat_obj.Feats = new NDArray(feats_new.Values.ToArray());

                            feats_new?.Clear();
                            feats_list?.Clear();

                            if (cancel.IsCancellationRequested) { result = false; break; }
                            await SaveFeatureData(feat_obj);
                            #endregion
                        }
                    }
                }
                catch (Exception ex) { ReportMessage(ex); }
                finally
                {
                    feats?.Clear();
                    BatchCancel.TryReset();
                    sw?.Stop();
                    if (BatchTaskIdle?.CurrentCount <= 0) BatchTaskIdle?.Release();
                    ReportMessage($"Create feature datas finished, Elapsed: {sw?.Elapsed:dd\\.hh\\:mm\\:ss}", result ? TaskStatus.RanToCompletion : TaskStatus.Canceled);
                }
            }
            return (result);
        }
        #endregion

        private readonly SemaphoreSlim ModelLoadedState = new(1, 1);
        private readonly SemaphoreSlim FeatureLoadedState = new(1, 1);

        private class ModelInput
        {
            [VectorType(3 * 224 * 224)]
            [ColumnName("ImageByteData")]
            public float[]? Data { get; set; }
        }

        private class Prediction
        {
            [VectorType(1000)]
            [ColumnName("PredictionFeature")]
            public float[]? Feature { get; set; }
        }

        public Similar()
        {
            InitBatchTask();
            H5Filter.Register(new Blosc2Filter());
        }
#if DEBUG
        private static double[] Norm(IEnumerable<double> array, bool zscore = false)
        {
            if (array == null) return ([]);
            if (zscore)
            {
                var mean = array.Average();
                var sd =array.Select(x => Math.Pow(x - mean, 2)).Sum();
                var std = Math.Sqrt(sd / array.Count());
                return (array.Select(a => (a - mean) / std).ToArray());
            }
            else
            {
                var min = array.Min();
                var max = array.Max();
                return (array.Select(a => (a - min) / (max - min)).ToArray());
            }
        }

        private static float[] Norm(IEnumerable<float> array, bool zscore = false)
        {
            if (array == null) return ([]);
            if (zscore)
            {
                var mean = array.Average();
                var sd = array.Select(x => (float)Math.Pow(x - mean, 2)).Sum();
                var std = (float)Math.Sqrt(sd / array.Count());
                return (array.Select(a => (a - mean) / std).ToArray());
            }
            else
            {
                var min = array.Min();
                var max = array.Max();
                return (array.Select(a => (a - min) / (max - min)).ToArray());
            }
        }

        private static double[] LA_Norm(double[] array)
        {
            var ss = Math.Sqrt(array.Select(a => a * a).Sum());
            return (array.Select(a => a / ss).ToArray());
        }
#endif
        private static float[] LA_Norm(float[] array)
        {
            //var ss0 = np.sqrt(np.power(array, 2).sum()).GetValue();
            var ss = (float)Math.Sqrt(array.Select(a => a * a).Sum());
            return (array.Select(a => a / ss).ToArray());
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

        public SKBitmap? FromImageSource(BitmapSource src)
        {
            SKBitmap? result = null;
            if (src is not null)
            {
                using (var ms = new MemoryStream())
                {
                    try
                    {
                        ////var stride = ((image.PixelWidth * image.Format.BitsPerPixel + 31) / 32) * 4;
                        //var stride = ((image.PixelWidth * image.Format.BitsPerPixel + 31) >> 5) << 2;
                        //byte[] buf = new byte[stride * (int)image.Height];
                        //image.CopyPixels(buf, stride, 0);

                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(src));
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
                        catch (Exception ex) { ReportMessage(ex); }
                    }
                }
            }
            return (result);
        }

        public SKBitmap? LoadImage(string file)
        {
            SKBitmap? result = null;
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            try
            {
                if (tiff_exts.Contains(Path.GetExtension(file).ToLower()))
                {
                    var bytes = File.ReadAllBytes(file);
                    using (var msi = new MemoryStream(bytes))
                    {
                        using (var mso = new MemoryStream())
                        {
                            try
                            {
                                msi.Seek(0, SeekOrigin.Begin);

                                BitmapEncoder encoder = new PngBitmapEncoder();
                                encoder.Frames.Add(BitmapFrame.Create(msi));
                                encoder.Save(mso);

                                mso.Seek(0, SeekOrigin.Begin);
                                result = SKBitmap.Decode(mso);
                            }
                            catch (Exception ex) { ReportMessage(ex); }
                        }
                    }
                }
                else
                {
                    result = SKBitmap.Decode(file);
                }
            }
            catch (Exception ex) { ReportMessage(ex); }
#if DEBUG
            ReportMessage($"Loaded image file {file}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
#endif
            return (result);
        }

        public async Task<ITransformer?> LoadModel(bool reload = false)
        {
            var model_file = GetAbsolutePath(ModelLocation);
            var model_in = ModelInputColumnName;
            var model_out = ModelOutputColumnName;

            if (string.IsNullOrEmpty(ModelLocation) || !File.Exists(model_file))
            {
                ReportMessage($"Machine Learning model not specified or model file not exists!");
                return (null);
            }
            else if (string.IsNullOrEmpty(ModelInputColumnName))
            {
                ReportMessage($"Machine Learning model input name not specified!");
                return (null);
            }
            else if (string.IsNullOrEmpty(ModelOutputColumnName))
            {
                ReportMessage($"Machine Learning model output name not specified!");
                return (null);
            }

            if ((_model_ == null || reload) && await ModelLoadedState.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                var sw = Stopwatch.StartNew();
                ReportMessage($"Loading Model from {model_file}", RunningStatue);
                try
                {
                    // Create IDataView from empty list to obtain input data schema
                    var data = mlContext.Data.LoadFromEnumerable(new List<ModelInput>());

                    //var pipeline = mlContext.Transforms.ApplyOnnxModel(modelFile: model_file, outputColumnNames: [model_out], inputColumnNames: [model_in]);

                    var pipeline = mlContext.Transforms.CopyColumns(ModelInputColumnName, "ImageByteData")
                        .Append(mlContext.Transforms.ApplyOnnxModel(modelFile: model_file, outputColumnNames: [model_out], inputColumnNames: [model_in]))
                        .Append(mlContext.Transforms.CopyColumns("PredictionFeature", ModelOutputColumnName));

                    // Fit scoring pipeline
                    _model_ = pipeline.Fit(data);

                    _predictionEngine_ = mlContext.Model.CreatePredictionEngine<ModelInput, Prediction>(_model_);

                    //mlContext.Transforms.CalculateFeatureContribution(_model_);
                }
                catch (Exception ex) { ReportMessage(ex); }
                finally
                {
                    sw?.Stop();
                    if (ModelLoadedState?.CurrentCount == 0) ModelLoadedState?.Release();
                    ReportMessage($"Loaded Model from {model_file}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                }
            }
            return _model_;
        }

        public async Task CreateFeatureData(Storage storage)
        {
            if (await BatchTaskIdle?.WaitAsync(0))
            {
                if (!BatchCancel.TryReset() && BatchCancel.IsCancellationRequested) BatchCancel = new CancellationTokenSource();

                var info = new BatchTaskInfo()
                {
                    Folders = [storage],

                    CancelToken = BatchCancel.Token,
                    ParallelMode = Setting.ParallelMode,
                    ParallelLimit = Setting.ResultLimit,
                    ParallelTimeOut = TimeSpan.FromSeconds(Setting.ParallelTimeOut),
                    CheckPoint = Setting.ParallelCheckPoint
                };
                var result = await Task.Run(async () =>
                {
                    return(await CreateFeatureDataAsync(info, BatchCancel.Token));
                }, BatchCancel.Token);

                if (result)
                {
                    ReportProgress(100);
                    ReportMessage("Batch work finished!");
                }
                else
                {
                    ReportProgress(100);
                    ReportMessage("Batch work canceled!");
                }
            }
        }

        public async Task CreateFeatureData(string folder, string feature_db, bool recurise = false)
        {
            if (await BatchTaskIdle?.WaitAsync(0))
            {
                var info = new BatchTaskInfo()
                {
                    DatabaseName = feature_db ?? string.Empty,
                    FolderName = folder,
                    Recurise = recurise,

                    CancelToken = BatchCancel.Token,
                    ParallelMode = Setting.ParallelMode,
                    ParallelLimit = Setting.ResultLimit,
                    ParallelTimeOut = TimeSpan.FromSeconds(Setting.ParallelTimeOut),
                    CheckPoint = Setting.ParallelCheckPoint
                };

                if (!BatchCancel.TryReset() && BatchCancel.IsCancellationRequested) BatchCancel = new CancellationTokenSource();
                var result = await Task.Run(async ()=>
                {
                    return(await CreateFeatureDataAsync(info, BatchCancel.Token));
                }, BatchCancel.Token);

                if (result)
                {
                    ReportProgress(100);
                    ReportMessage("Batch work finished!");
                }
                else
                {
                    ReportProgress(100);
                    ReportMessage("Batch work canceled!");
                }
            }
        }

        public async Task CreateFeatureData(List<Storage> storage)
        {
            if (await BatchTaskIdle?.WaitAsync(0))
            {
                var info = new BatchTaskInfo()
                {
                    Folders = storage,

                    CancelToken = BatchCancel.Token,
                    ParallelMode = Setting.ParallelMode,
                    ParallelLimit = Setting.ResultLimit,
                    ParallelTimeOut = TimeSpan.FromSeconds(Setting.ParallelTimeOut),
                    CheckPoint = Setting.ParallelCheckPoint
                };

                if (!BatchCancel.TryReset() && BatchCancel.IsCancellationRequested) BatchCancel = new CancellationTokenSource();
                var result = await Task.Run(async ()=>
                {
                    return(await CreateFeatureDataAsync(info, BatchCancel.Token));
                }, BatchCancel.Token);

                if (result)
                {
                    ReportProgress(100);
                    ReportMessage("Batch work finished!");
                }
                else
                {
                    ReportProgress(100);
                    ReportMessage("Batch work canceled!");
                }
            }
        }

        public void CancelCreateFeatureData()
        {
            BatchCancel?.CancelAsync();
        }

        public async Task<(string[], float[,])> LoadFeature(string feature_db)
        {
            float[,] feats;
            string[] names;

            var sw = Stopwatch.StartNew();
            (names, feats) = await Task.Run<(string[], float[,])>(() =>
            {
                float[,] feats = new float[0,0];
                string[] names = [];

                ReportMessage($"Loading Feature Database from {feature_db}", RunningStatue);

                feature_db = GetAbsolutePath(feature_db);

                var options = new H5ReadOptions()
                {
                    IncludeClassFields = true,
                    IncludeClassProperties = true,
                    IncludeStructFields = true,
                    IncludeStructProperties = true
                };

                var h5 = H5File.OpenRead(feature_db, options);
                try
                {
                    var h5_feats = h5.Dataset("feats");
                    var h5_names = h5.Dataset("names");

                    feats = h5_feats.Read<float[,]>();
                    names = h5_names.Read<string[]>();
                    //names = h5_names.Read<string[]>().Select(n => Path.GetFullPath(Path.Combine(image_folder, n))).ToArray();

                    ReportMessage($"Loaded Feature Database [{names.Length}, {feats.Length}] from {feature_db}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                }
                catch (Exception ex) { ReportMessage($"{ex.StackTrace?.Split().LastOrDefault()} : {ex.Message}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s"); }
                finally
                {
                    sw?.Stop();
                    h5?.Dispose();
                }
                return ((names, feats));
            }).WaitAsync(TimeSpan.FromSeconds(60));

            return ((names, feats));
        }

        public async Task LoadFeatureData(string feature_db, bool reload = false)
        {
            if (string.IsNullOrEmpty(feature_db)) return;

            var file = GetAbsolutePath(feature_db);
            if (File.Exists(file))
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var feat_obj = _features_.Where(f => f.FeatureDB.Equals(feature_db)).FirstOrDefault();
                    if (feat_obj == null || reload)
                    {
                        ReportMessage($"Loading Feature DataTable from {file}", RunningStatue);

                        feat_obj = new FeatureData() { FeatureDB = feature_db };
                        _features_.Add(feat_obj);

                        float[,] feats;
                        string[] names;
                        (names, feats) = await LoadFeature(file);
                        if (names.Length > 0 && feats.Length > 0)
                        {
                            feat_obj.Names = names;
                            feat_obj.Feats = new NDArray(feats);
                            feat_obj.Loaded = true;

                            ReportMessage($"Loaded Feature DataTable from {file}, {sw?.Elapsed.TotalSeconds:F4}s");
                        }
                        else { ReportMessage($"Loading Feature DataTable from {file} failed!"); }
                    }
                }
                catch (Exception ex) { ReportMessage(ex); }
                finally { sw?.Stop(); }
            }
        }

        public async Task LoadFeatureData(Storage storage, bool reload = false)
        {
            if (storage is not null)
            {
                var sw = Stopwatch.StartNew();
                var file = GetAbsolutePath(storage.DatabaseFile);
                if (File.Exists(file))
                {
                    try
                    {
                        var feat_obj =_features_.Where(f => GetAbsolutePath(f.FeatureDB).Equals(GetAbsolutePath(storage.DatabaseFile))).FirstOrDefault();

                        if (feat_obj == null || !feat_obj.Loaded || reload)
                        {
                            float[,] feats;
                            string[] names;
                            (names, feats) = await LoadFeature(file);
                            await Task.Run(() =>
                            {
                                ReportMessage($"Loading Feature DataTable from {file}", RunningStatue);
                                if (names.Length > 0 && feats.Length > 0)
                                {
                                    if (feat_obj == null)
                                    {
                                        _features_.Add(new FeatureData() { FeatureDB = file, Names = names, Feats = new NDArray(feats), Loaded = true });
                                    }
                                    else
                                    {
                                        feat_obj.Names = names;
                                        feat_obj.Feats = new NDArray(feats);
                                    }
                                    ReportMessage($"Loaded Feature DataTable from {file}, {sw?.Elapsed.TotalSeconds:F4}s");
                                }
                                else { ReportMessage($"Loading Feature DataTable from {file} failed!"); }
                            });
                        }
                    }
                    catch (Exception ex) { ReportMessage(ex); }
                    finally { sw?.Stop(); }
                }
            }
        }

        public async Task LoadFeatureData(List<Storage> storages, bool reload = false)
        {
            if (storages is not null && storages.Count > 0)
            {
                foreach (var storage in storages)
                {
                    await LoadFeatureData(storage, reload: reload);
                }
            }
        }

        public async Task SaveFeature(string feature_db, string[] names, Array feats)
        {
            var file = GetAbsolutePath(feature_db);
            if (Directory.Exists(Path.GetDirectoryName(file)) && names is not null && feats is not null)
            {
                await Task.Run(() =>
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        ReportMessage($"Saving Feature Data to {file}", RunningStatue);
                        //image_folder = (!string.IsNullOrEmpty(image_folder) && Directory.Exists(System.IO.Path.GetFullPath(image_folder)) ? 
                        if (File.Exists(file)) File.Move(file, $"{file}.lastgood", true);
                        var h5 = new H5File()
                        {
                            //["my-group"] = new H5Group()
                            //{
                            //["names"] = names.Select(n => n.Replace(image_folder, string.Empty)).ToArray(),
                            ["names"] = names,
                            ["feats"] = feats,
                            Attributes = new()
                            {
                                ["folder"] = $"{feature_db}",
                                ["size"] = new int[] { IMG_Width, IMG_HEIGHT }
                            }
                            //}
                        };
                        var option = new H5WriteOptions()
                        {
                            DefaultStringLength = 1024,
                            IncludeClassFields = true,
                            IncludeClassProperties = true,
                            IncludeStructFields = true,
                            IncludeStructProperties = true,
                            Filters = [ 
                                //Blosc2Filter.Id, 
                                new H5Filter(Id: Blosc2Filter.Id, Options: new(){
                                    [Blosc2Filter.COMPRESSION_LEVEL] = 9,
                                    [Blosc2Filter.COMPRESSOR_CODE] = "blosclz", // blosclz, lz4, lz4hc, zlib or zstd
                                }),
                            ]
                        };
                        h5?.Write(file, option);
                        ReportMessage($"Saved Feature Data to {file}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                    }
                    catch (Exception ex) { ReportMessage($"{ex.StackTrace?.Split().LastOrDefault()} : {ex.Message}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s"); }
                    finally { sw?.Stop(); }
                });
            }
        }

        public async Task SaveFeatureData(FeatureData featuredata)
        {
            if (featuredata is not null)
            {
                var file = GetAbsolutePath(featuredata.FeatureDB);
                if (Directory.Exists(Path.GetDirectoryName(file)))
                {
                    if (File.Exists(file)) File.Move(file, $"{file}.lastgood", true);

                    await SaveFeature(file, featuredata.Names, featuredata.Feats.ToMuliDimArray<float>());
                }
            }
        }

        public async Task ChangeImageFolder(string feature_db, string old_folder, string new_folder)
        {
            old_folder = GetAbsolutePath(old_folder);
            new_folder = GetAbsolutePath(new_folder);

            var file = GetAbsolutePath(feature_db);
            if (File.Exists(file))
            {
                await Task.Run(async () =>
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        ReportMessage($"Changing Feature Data Folder from {old_folder} to {new_folder}", RunningStatue);

                        float[,] feats;
                        string[] names;

                        (names, feats) = await LoadFeature(file);
                        names = names.Select(n => n.Replace(old_folder, new_folder)).ToArray();
                        await SaveFeature(file, names, feats);
                    }
                    catch (Exception ex) { ReportMessage(ex); }
                    finally { sw?.Stop(); ReportMessage($"Changed Feature Data Folder from {old_folder} to {new_folder}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s"); }
                });
            }
        }

        public async Task CleanImageFeature()
        {
            if (StorageList is not null)
            {
                foreach (var storage in StorageList)
                {
                    await CleanImageFeature(storage);
                }
            }
        }

        public async Task CleanImageFeature(Storage? storage)
        {
            if (storage is not null)
            {
                var folder = storage.ImageFolder;
                var db = storage.DatabaseFile;
                await CleanImageFeature(feature_db: db, folder: folder, recuice: storage.Recurice);
            }
        }

        public async Task CleanImageFeature(string feature_db, string folder, bool recuice)
        {
            var file = GetAbsolutePath(feature_db);
            if (File.Exists(file))
            {
                await Task.Run(async () =>
                {
                    if (await BatchTaskIdle.WaitAsync(0))
                    {
                        var sw = Stopwatch.StartNew();
                        try
                        {
                            ReportMessage($"Cleaning Feature Data from {file}", RunningStatue);

                            float[,] feats;
                            string[] names;

                            var feat_obj = _features_.Where(f => f.FeatureDB.Equals(GetAbsolutePath(feature_db))).FirstOrDefault();

                            (names, feats) = await LoadFeature(file);
                            folder = GetAbsolutePath(folder);
                            var option = recuice ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                            var files = Directory.GetFiles(folder, "*.*", option).Where(f => exts.Contains(Path.GetExtension(f).ToLower()));
                            var diffs = names.Except(files).ToList(); //.Where(f => !string.IsNullOrEmpty(f)).ToList();
                            if (diffs.Count > 0)
                            {
                                var feat_list = new Dictionary<string, float[]>();
                                for (var i = 0; i < feats.GetLength(0); i++)
                                {
                                    if (string.IsNullOrEmpty(names[i])) continue;
                                    var row = new List<float>();
                                    for (var j = 0; j < feats.GetLength(1); j++)
                                    {
                                        row.Add(feats[i, j]);
                                    }
                                    feat_list.Add(names[i], row.ToArray());
                                }

                                diffs = diffs.Where(f => !string.IsNullOrEmpty(f)).ToList();
                                var feats_dict = feat_list.Where(kv => string.IsNullOrEmpty(kv.Key) || !diffs.Contains(kv.Key)).ToDictionary();

                                var names_new = feats_dict.Select(kv => kv.Key).ToArray();
                                var values = feats_dict.Select(kv => kv.Value).ToArray();

                                float[,] feats_new = new float[values.GetLength(0), feats.GetLength(1)];
                                for (var i = 0; i < feats_new.GetLength(0); i++)
                                {
                                    for (var j = 0; j < feats_new.GetLength(1); j++)
                                    {
                                        feats_new[i, j] = values[i][j];
                                    }
                                }

                                await SaveFeature(file, names_new, feats_new);

                                if (feat_obj is not null && names_new.Length > 0 && feats_new.Length > 0)
                                {
                                    feat_obj.Names = names_new;
                                    feat_obj.Feats = new NDArray(feats_new);
                                    feat_obj.Loaded = true;

                                    ReportMessage($"Updated Feature DataTable to {file}, {sw?.Elapsed.TotalSeconds:F4}s");
                                }
                            }
                        }
                        catch (Exception ex) { ReportMessage(ex); }
                        finally
                        {
                            if (BatchTaskIdle?.CurrentCount <= 0) BatchTaskIdle?.Release();
                            sw?.Stop();
                            ReportMessage($"Cleaned Feature Data {file}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                        }
                    }
                });
            }
        }

        public async Task<float[]?> ExtractImaegFeature(string file)
        {
            float[]? result = null;

            if (_model_ == null) await LoadModel();

            file = GetAbsolutePath(file);
            if (File.Exists(file) && _model_ is not null && _predictionEngine_ != null)
            {
                try
                {
                    using (SKBitmap? bmp_src = LoadImage(file))
                    {
                        if (bmp_src is not null)
                        {
                            ReportMessage($"Extract Feature of {file}");
                            result = await ExtractImaegFeature(bmp_src);
                        }
                    }
                }
                catch (Exception ex) { ReportMessage(ex); }
            }

            return (result);
        }

        public async Task<float[]?> ExtractImaegFeature(SKBitmap? image)
        {
            float[]? result = null;
            if (image is not null)
            {
                var sw = Stopwatch.StartNew();
                ReportMessage($"Extracting Feature of Memory Image", RunningStatue);
                try
                {
                    if (_model_ == null) await LoadModel();

                    var size = new SKSizeI(IMG_SIZE.Width, IMG_SIZE.Height);

                    SKBitmap bmp_thumb;
                    if (image.BytesPerPixel == 1)
                    {
                        var bmp_palete = image.Resize(size, SKFilterQuality.Medium);
                        bmp_thumb = new SKBitmap(bmp_palete.Width, bmp_palete.Height);
                        using var bmp_canvas = new SKCanvas(bmp_thumb);
                        bmp_canvas.DrawImage(SKImage.FromBitmap(bmp_palete), 0f, 0f);
                    }
                    else bmp_thumb = image.Resize(size, SKFilterQuality.Medium);

                    using (var img = MLImage.CreateFromPixels(bmp_thumb.Width, bmp_thumb.Height, MLPixelFormat.Bgra32, bmp_thumb.Bytes))
                    {
                        var img_data = img.GetBGRPixels.Select(b => (float)b).ToArray();
                        result = _predictionEngine_.Predict(new ModelInput { Data = img_data }).Feature;

                        if (result != null) result = LA_Norm(result);
                    }
                }
                catch (Exception ex) { ReportMessage(ex); }
                finally
                {
                    sw?.Stop();
                    ReportMessage($"Extracted Feature of Memory Image, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                }
            }
            return (result);
        }

        public async Task<double> CompareImage(string file0, string file1, int padding = 0)
        {
            ReportMessage($"Comparing {file0}, {file1}", RunningStatue);
            return (await CompareImage(await ExtractImaegFeature(file0), await ExtractImaegFeature(file1), padding));
        }

        public async Task<double> CompareImage(SKBitmap? image0, SKBitmap? image1, int padding = 0)
        {
            if (image0 is not null && image1 is not null)
            {
                ReportMessage($"Comparing Memory Images", RunningStatue);
                return (await CompareImage(await ExtractImaegFeature(image0), await ExtractImaegFeature(image1), padding));
            }
            return (0);
        }

        public async Task<double> CompareImage(float[]? feature0, float[]? feature1, int padding = 0)
        {
            double result = 0;
            if (feature0 == null || feature1 == null) return (result);

            var sw = Stopwatch.StartNew();
            if (await ModelLoadedState.WaitAsync(TimeSpan.FromSeconds(30)) && await FeatureLoadedState.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                try
                {
                    ReportMessage($"Compareing of feature", RunningStatue);

                    var pad = new float[padding];

                    var feat_0 = padding <= 0 ? new NDArray(feature0) : new NDArray(feature0.Concat(pad).ToArray());
                    var feat_1= padding <= 0 ? new NDArray(feature1) : new NDArray(feature1.Concat(pad).ToArray());

                    var m_feat0 = np.zeros(1, feat_0.shape[0]);
                    m_feat0[0] = feat_0;
                    var m_feat1 = np.zeros(1, feat_1.shape[0]);
                    m_feat1[0] = feat_1;

                    var scores = np.dot(m_feat0, m_feat1.T)[0];
                    //result = scores.ToArray<double>()[0];// ["0"];
                    result = scores["0"];
                }
                catch (Exception ex) { ReportMessage(ex); }
                finally
                {
                    sw?.Stop();
                    if (ModelLoadedState.CurrentCount == 0) ModelLoadedState.Release();
                    if (FeatureLoadedState.CurrentCount == 0) FeatureLoadedState.Release();
                    ReportMessage($"Compared Result of 2 features : {result:F4}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                }
            }
            else ReportMessage("Model or Feature Database not loaded.");
            return (result);
        }

        public async Task<List<KeyValuePair<string, double>>> QueryImageScore(string file, string? feature_db = null, int limit = 10, int padding = 0)
        {
            ReportMessage($"Query of {file}", RunningStatue);
            return (await QueryImageScore(await ExtractImaegFeature(file), feature_db, limit, padding));
        }

        public async Task<List<KeyValuePair<string, double>>> QueryImageScore(SKBitmap? image, string? feature_db = null, int limit = 10, int padding = 0)
        {
            ReportMessage($"Query of Memory Image", RunningStatue);
            return (await QueryImageScore(await ExtractImaegFeature(image), feature_db, limit, padding));
        }

        public async Task<List<KeyValuePair<string, double>>> QueryImageScore(float[]? feature, string? feature_db = null, int limit = 10, int padding = 0)
        {
            var result = new List<KeyValuePair<string, double>>();
            if (feature == null) return (result);

            var sw = Stopwatch.StartNew();
            if (await ModelLoadedState.WaitAsync(TimeSpan.FromSeconds(30)) && await FeatureLoadedState.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                try
                {
                    ReportMessage($"Quering of feature", RunningStatue);

                    int count = 0;
                    limit = Math.Min(120, Math.Max(1, limit));
                    foreach (var feat_obj in string.IsNullOrEmpty(feature_db) ? _features_ : _features_.Where(f => f.FeatureDB.Equals(GetAbsolutePath(feature_db))).ToList())
                    {
                        if (!(feat_obj.Names is not null && feat_obj.Names.Length > 0) || !(feat_obj.Feats is not null && feat_obj.Feats.shape[0] > 0)) await LoadFeatureData(feat_obj.FeatureDB);

                        count += limit;
                        var pad = new float[padding];

                        var feat_ = padding <= 0 ? new NDArray(feature) : new NDArray(feature.Concat(pad).ToArray());

                        if (feat_obj.Feats is not null && feat_ is not null)
                        {
                            var m_feat = np.zeros(1, feat_.shape[0]);
                            m_feat[0] = feat_;
                            var scores = np.dot(m_feat, feat_obj.Feats.T)[0];
                            var rank_ID = scores.argsort<float>(axis: 0)["::-1"];
                            var rank_score = scores[rank_ID].ToArray<double>();

                            for (int i = 0; i < feat_obj.Names.Length; i++)
                            {
                                var f_name = feat_obj.Names[rank_ID[i]];
                                if (string.IsNullOrEmpty(f_name)) continue;
                                f_name = GetAbsolutePath(f_name);
                                if (File.Exists(f_name))
                                {
                                    result.Add(new KeyValuePair<string, double>(f_name, rank_score[i]));
                                    if (result.Count >= count) break;
                                }
                            }
                        }
                    }
                    result = result.OrderByDescending(r => r.Value).Take(limit).ToList();
                }
                catch (Exception ex) { ReportMessage(ex); }
                finally
                {
                    sw?.Stop();
                    if (ModelLoadedState.CurrentCount == 0) ModelLoadedState.Release();
                    if (FeatureLoadedState.CurrentCount == 0) FeatureLoadedState.Release();
                    ReportMessage($"Queried of feature, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                }
            }
            else ReportMessage("Model or Feature Database not loaded.");
            return (result);
        }

    }
}
