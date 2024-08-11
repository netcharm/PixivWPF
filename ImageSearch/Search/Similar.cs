﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Printing;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.Trainers;
using Microsoft.ML.Transforms.Image;
using Microsoft.ML.Transforms.Onnx;
using Google.Protobuf.WellKnownTypes;

using NumSharp;
using PureHDF;
using SkiaSharp;

namespace ImageSearch.Search
{
    public class Storage
    {
        public string Description { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public bool Recurice { get; set; } = false;
        public string DatabaseFile { get; set; } = string.Empty;
        public string[] DatabaseFiles { get; set; } = [];
    }

    public class BatchProgressInfo
    {
        public bool MergeAllFeats { get; set; } = true;
        public bool Recurise { get; set; } = false;
        public ulong CheckPoint { get; set; } = 5000;
        public string DatabaseName { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public List<Storage> Folders { get; set; } = new List<Storage>();
        public string FileName { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public long Current { get; set; } = 0;
        public long Total { get; set; } = 0;
        public double Percentage { get { return (Total > 0 && Current >= 0 ? (double)Current / Total : 0); } }
        public TaskStatus State { get; set; } = TaskStatus.Created;
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
        public string[] Names { get; set; } = Array.Empty<string>();
        public bool Loaded { get; set; } = false;
    }

    internal class Similar
    {
        private static string[] exts = [ ".jpg", ".jpeg", ".bmp", ".png", ".tif", ".tiff", ".gif", ".webp" ];
        private static string[] tiff_exts = [ ".tif", ".tiff" ];

        private string GetAbsolutePath(string relativePath)
        {
            string fullPath = string.Empty;
            //FileInfo _dataRoot = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
            FileInfo _dataRoot = new FileInfo(new Uri(System.Reflection.Assembly.GetExecutingAssembly().Location).LocalPath);
            if (_dataRoot.Directory is DirectoryInfo)
            {
                string assemblyFolderPath = _dataRoot.Directory.FullName;

                fullPath = Path.Combine(assemblyFolderPath, relativePath);
            }
            return fullPath;
        }

        public Action<string, TaskStatus>? MessageReportAction;
        
        private void ReportMessage(string info, TaskStatus state = TaskStatus.Created)
        {
            if (MessageReportAction is Action<string, TaskStatus> && !string.IsNullOrEmpty(info))
            {
                Application.Current.Dispatcher.Invoke(MessageReportAction, priority: DispatcherPriority.Render, info, state);
                //MessageReportAction.Invoke(info, state);
            }
        }

        #region ProgressBar action
        public ProgressBar? progressbar { get; set; }

        //public Action<BatchProgressInfo> ProcessingAction;
        private IProgress<BatchProgressInfo>? progress;

        private void InitProgressBar()
        {
            progress = new Progress<BatchProgressInfo>(info =>
            {
                try
                {
                    var index = info.Current >= 0 ? info.Current : 0;
                    var total = info.Total >= 0 ? info.Total : 0;
                    var total_s = total.ToString();
                    var index_s = index.ToString().PadLeft(total_s.Length, '0');
                }
                catch { }
            });

            BatchReportAction = (info) =>
            {
                if (progress is IProgress<BatchProgressInfo>) progress.Report(info);
            };
        }
        #endregion

        private static int IMG_Width = 224;
        private static int IMG_HEIGHT = 224;
        private System.Drawing.Size IMG_SIZE = new System.Drawing.Size(IMG_Width, IMG_HEIGHT);

        private readonly string modelLocation = @"models\resnet50-v2-7.onnx";

        private readonly string featureslLocation = @"data\pixiv_224x224_resnet50v2.h5";

        // Create MLContext
        private readonly MLContext mlContext = new MLContext(seed: 1)
        {
            FallbackToCpu = true,
            GpuDeviceId = null//0
        };

        private ITransformer? _model_;

        private PredictionEngine<ModelInput, Prediction>? _predictionEngine_;

        private List<FeatureData> _features_ = new List<FeatureData>();

        public List<Storage> StorageList { get; set; } = new List<Storage>();

        #region background work thread
        //private CancellationTokenSource cancelSource;
        BackgroundWorker? BatchTask;

        public Action<BatchProgressInfo>? BatchReportAction;

        private void ReportBatch(BatchProgressInfo info)
        {
            if (BatchReportAction is Action<BatchProgressInfo> && info is BatchProgressInfo)
            {
                //BatchReportAction.DynamicInvoke(info);
                BatchReportAction.Invoke(info);
            }
        }

        private void InitBatchTask()
        {
            if (BatchTask is BackgroundWorker)
            {
                BatchTask.CancelAsync();
                BatchTask.Dispose();
            }

            BatchTask = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            BatchTask.DoWork += BatchTask_DoWork;
            BatchTask.ProgressChanged += BatchTask_ProgressChanged;
            BatchTask.RunWorkerCompleted += BatchTask_RunWorkerCompleted;
        }

        private void BatchTask_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            if (progressbar is ProgressBar)
            {
                progressbar.Dispatcher.Invoke(() =>
                {
                    progressbar.Value = 100;
                });
            }

            ReportMessage("Batch work finished!");
        }

        private void BatchTask_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {
            if (progressbar is ProgressBar)
            {
                progressbar.Dispatcher.Invoke(() =>
                {
                    progressbar.Value = e.ProgressPercentage;
                });
            }
        }

        private async void BatchTask_DoWork(object? sender, DoWorkEventArgs e)
        {
            if (e.Argument is BatchProgressInfo)
            {
                var sw = Stopwatch.StartNew();
                var info = e.Argument as BatchProgressInfo;
                if (info.Folders.Count() > 0 || Directory.Exists(GetAbsolutePath(info.FolderName)))
                {
                    BatchTask.ReportProgress(0);
                    try
                    {
                        ConcurrentDictionary<string, float[]> feats = new ConcurrentDictionary<string, float[]>();

                        if (info.Folders is List<Storage> && info.Folders.Count() <= 0)
                        {
                            info.Folders.Add(new Storage() { Folder = info.FolderName, Recurice = info.Recurise, DatabaseFile = info.DatabaseName });
                        }
                        foreach (var storage in info.Folders.Where(d => Directory.Exists(d.Folder)))
                        {
                            if (BatchTask.CancellationPending) break;

                            var folder = GetAbsolutePath(storage.Folder);
                            var feats_db = GetAbsolutePath(storage.DatabaseFile);
                            var dir_name = Path.GetFileName(folder);

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

                            ConcurrentDictionary<string, float[]> feats_list = new ConcurrentDictionary<string, float[]>();
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

                            try
                            {                                
                                var checkpoint = np.Load_Npz<float[]>($@"data\{dir_name}_checkpoint_latest.npz");
                                foreach (var kv in checkpoint) feats[kv.Key] = kv.Value;
                            }
                            catch (Exception ex) { ReportMessage(ex.Message); }

                            var option = storage.Recurice ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                            var files = Directory.GetFiles(folder, "*.*", option).Where(f => exts.Contains(Path.GetExtension(f).ToLower()));
                            var diffs = files.Except(feats_list.Keys).Except(feats.Keys).ToArray();
                            int count = diffs.Count();
                            uint index = 0;

                            foreach (var file in diffs)
                            {
                                index++;
                                if (string.IsNullOrEmpty(file)) continue;
                                //if (!feat_obj.Names.Contains(file))
                                {
                                    if (BatchTask.CancellationPending) break;
                                    var feat = await ExtractImaegFeature(file);
                                    if (!string.IsNullOrEmpty(file) && feat is float[]) feats[file] = feat;

                                    e.Result = (float)index / count;

                                    BatchTask.ReportProgress((int)Math.Ceiling((float)index / count * 100.0));

                                    var current = feats.Count();
                                    if (info.CheckPoint > 0 && current % (uint)info.CheckPoint == 0)
                                    {
                                        try
                                        {
                                            np.Save_Npz(feats.ToDictionary(kv => kv.Key, kv => kv.Value as Array), $@"data\{dir_name}_checkpoint_{current}.npz");
                                        }
                                        catch (Exception ex) { ReportMessage(ex.Message); }
                                    }

                                    info.FileName = file;
                                    info.Current = index;
                                    info.Total = count;
                                    ReportBatch(info);
                                }
                            }

                            if (feats.Count() > 0 && feat_obj is FeatureData)
                            {
                                np.Save_Npz(feats.ToDictionary(kv => kv.Key, kv => kv.Value as Array), $@"data\{dir_name}_checkpoint_latest.npz");

                                if (BatchTask.CancellationPending) return;

                                var feats_new = feats_list.UnionBy(feats, kv => kv.Key).ToDictionary();

                                feat_obj.Names = feats_new.Keys.ToArray();

                                feat_obj.Feats = new NDArray(feats_new.Values.ToArray());

                                if (BatchTask.CancellationPending) return;
                                await SaveFeatureData(feat_obj);
                            }
                        }
                        BatchTask.ReportProgress(100);
                    }
                    catch (Exception ex) { ReportMessage(ex.Message); }
                    finally
                    {
                        sw.Stop();
                        ReportMessage($"Create feature datas finished, Elapsed: {sw.Elapsed.ToString()}", TaskStatus.RanToCompletion);
                    }
                }
            }
        }
        #endregion

        private SemaphoreSlim ModelLoadedState = new SemaphoreSlim(1, 1);
        private SemaphoreSlim FeatureLoadedState = new SemaphoreSlim(1, 1);
        private SemaphoreSlim FeatureDBLoadedState = new SemaphoreSlim(1, 1);

        private class ModelInput
        {
            [VectorType(3 * 224 * 224)]
            [ColumnName("data")]
            public float[]? Data { get; set; }
        }

        private class Prediction
        {
            [VectorType(1000)]
            [ColumnName("resnetv24_dense0_fwd")]
            public float[]? Feature { get; set; }
        }

        public Similar()
        {
            InitBatchTask();
        }

        private IEnumerable<double> Norm(IEnumerable<double> array, bool zscore = false)
        {
            if (array == null) return (Array.Empty<double>());
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

        private IEnumerable<float> Norm(IEnumerable<float> array, bool zscore = false)
        {
            if (array == null) return (Array.Empty<float>());
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

        private double[] la_norm(double[] array)
        {
            var ss = Math.Sqrt(array.Select(a => a * a).Sum());
            return (array.Select(a => a / ss).ToArray());
        }

        private float[] la_norm(float[] array)
        {
            //var ss0 = np.sqrt(np.power(array, 2).sum()).GetValue();
            var ss = (float)Math.Sqrt(array.Select(a => a * a).Sum());
            return (array.Select(a => a / ss).ToArray());
        }

        public SKBitmap? FromImageSource(ImageSource src)
        {
            SKBitmap? result = null;
            if (src is ImageSource)
            {
                using (var ms = new MemoryStream())
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(src as BitmapSource));
                    encoder.Save(ms);

                    ms.Seek(0, SeekOrigin.Begin);
                    result = SKBitmap.Decode(ms);
                }
            }
            return (result);
        }

        public SKBitmap? FromImageSource(BitmapSource src)
        {
            SKBitmap? result = null;
            if (src is BitmapSource)
            {
                using (var ms = new MemoryStream())
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
            }
            return (result);
        }

        public BitmapSource? ToBitmapSource(SKBitmap src)
        {
            BitmapSource? result = null;
            if (src is SKBitmap)
            {
                using (var sk_data = src.Encode(SKEncodedImageFormat.Png, 100))
                {
                    using (var ms = new MemoryStream())
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
                }
            }
            return (result);
        }

        public SKBitmap LoadImage(string file)
        {
            SKBitmap? result = null;
            if (tiff_exts.Contains(Path.GetExtension(file).ToLower()))
            {
                var bytes = File.ReadAllBytes(file);
                using (var msi = new MemoryStream(bytes))
                {
                    using (var mso = new MemoryStream())
                    {
                        msi.Seek(0, SeekOrigin.Begin);

                        BitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(msi));
                        encoder.Save(mso);

                        mso.Seek(0, SeekOrigin.Begin);
                        result = SKBitmap.Decode(mso);
                    }
                }
            }
            else
            {
                result = SKBitmap.Decode(file);
            }
            return (result);
        }

        public async Task<ITransformer?> LoadModel(bool reload = false)
        {
            var model_file = GetAbsolutePath(modelLocation);
            if ((_model_ == null || reload) && File.Exists(model_file) && await ModelLoadedState.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                var sw = Stopwatch.StartNew();
                ReportMessage($"Loading Model from {model_file}", TaskStatus.Running);
                try
                {
                    // Create IDataView from empty list to obtain input data schema
                    var data = mlContext.Data.LoadFromEnumerable(new List<ModelInput>());

                    var pipeline = mlContext.Transforms.ApplyOnnxModel(modelFile: model_file, outputColumnNames: ["resnetv24_dense0_fwd"], inputColumnNames: ["data"]);

                    // Fit scoring pipeline
                    _model_ = pipeline.Fit(data);

                    _predictionEngine_ = mlContext.Model.CreatePredictionEngine<ModelInput, Prediction>(_model_);

                    //mlContext.Transforms.CalculateFeatureContribution(_model_);
                }
                finally { if (ModelLoadedState.CurrentCount == 0) ModelLoadedState.Release(); sw.Stop(); ReportMessage($"Loaded Model from {model_file}, Elapsed: {sw.Elapsed.TotalSeconds:F4}s"); }
            }
            return _model_;
        }

        public async Task CreateFeatureData(Storage storage)
        {
            if (BatchTask is BackgroundWorker && !BatchTask.IsBusy)
            {
                var feat_obj = _features_.Where(f => f.FeatureDB.Equals(GetAbsolutePath(storage.DatabaseFile))).FirstOrDefault();
                if (feat_obj == null && File.Exists(storage.DatabaseFile))
                {
                    await LoadFeatureData(storage);
                }

                var info = new BatchProgressInfo() { Folders = new List<Storage>(){ storage } };
                BatchTask.RunWorkerAsync(info);
            }
        }

        public async Task CreateFeatureData(string folder, string feature_db, bool recurise = false)
        {
            if (BatchTask is BackgroundWorker && !BatchTask.IsBusy)
            {
                var feat_obj = _features_.Where(f => f.FeatureDB.Equals(GetAbsolutePath(feature_db))).FirstOrDefault();
                if (feat_obj == null && File.Exists(GetAbsolutePath(feature_db)))
                {
                    await LoadFeatureData(feature_db);
                }

                var info = new BatchProgressInfo() { DatabaseName = feature_db ?? string.Empty, FolderName = folder, Recurise = recurise, MergeAllFeats = false };
                BatchTask.RunWorkerAsync(info);
            }
        }

        public void CreateFeatureData(List<Storage> storage)
        {
            if (BatchTask is BackgroundWorker && !BatchTask.IsBusy)
            {
                var info = new BatchProgressInfo() { Folders = storage, MergeAllFeats = true };
                BatchTask.RunWorkerAsync(info);
            }
        }

        public void CancelCreateFeatureData()
        {
            if (BatchTask is BackgroundWorker && BatchTask.IsBusy) BatchTask.CancelAsync();
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

                ReportMessage($"Loading Feature Database from {feature_db}", TaskStatus.Running);

                feature_db = GetAbsolutePath(feature_db);

                //var h5_option = new H5ReadOptions(){ IncludeClassFields = true, IncludeClassProperties = true, IncludeStructFields = true, IncludeStructProperties = true };
                ///var h5 =  H5File.Open(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, h5_option);
                var h5 = H5File.OpenRead(feature_db);
                try
                {
                    var h5_feats = h5.Dataset("feats");
                    var h5_names = h5.Dataset("names");

                    feats = h5_feats.Read<float[,]>();
                    names = h5_names.Read<string[]>();
                }
                finally { h5.Dispose(); sw.Stop(); ReportMessage($"Loaded Feature Database [{names.Length}, {feats.Length}] from {feature_db}, Elapsed: {sw.Elapsed.TotalSeconds:F4}s"); }
                return ((names, feats));
            }).WaitAsync(TimeSpan.FromSeconds(30));
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
                    float[,] feats;
                    string[] names;
                    (names, feats) = await LoadFeature(file);
                    if (names.Length > 0 && feats.Length > 0)
                    {
                        var feat_obj = _features_.Where(f => f.FeatureDB.Equals(feature_db)).FirstOrDefault();
                        if (feat_obj == null)
                        {
                            feat_obj = new FeatureData() { FeatureDB = feature_db };
                            _features_.Add(feat_obj);
                        }

                        feat_obj.Names = names;
                        feat_obj.Feats = new NDArray(feats);
                        feat_obj.Loaded = true;

                        ReportMessage($"Loaded Feature DataTable from {file}, {sw.Elapsed.TotalSeconds:F4}s");
                    }
                    else { ReportMessage($"Loading Feature DataTable from {file} failed!"); }
                }
                finally { sw.Stop(); }
            }
        }

        public async Task LoadFeatureData(Storage storage, bool reload = false)
        {
            if (storage is Storage)
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
                                ReportMessage($"Loaded Feature DataTable from {file}, {sw.Elapsed.TotalSeconds:F4}s");
                            }
                            else { ReportMessage($"Loading Feature DataTable from {file} failed!"); }
                        }
                    }
                    finally { sw.Stop(); }
                }
            }
        }

        public async Task LoadFeatureData(List<Storage> storages, bool reload = false)
        {
            if (storages is List<Storage> && storages.Count() > 0)
            {
                foreach (var storage in storages)
                {
                    await LoadFeatureData(storage, reload: reload);
                }
            }
        }

        public async Task SaveFeature(string feature_db, string[] names, Array feats)
        {
            var file = GetAbsolutePath(feature_db ?? featureslLocation);
            if (Directory.Exists(Path.GetDirectoryName(file)) && names is string[] && feats is Array)
            {
                await Task.Run(() =>
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        ReportMessage($"Saving Feature Data to {file}", TaskStatus.Running);
                        if (File.Exists(file)) File.Move(file, $"{file}.lastgood", true);
                        var h5 = new H5File()
                        {
                            //["my-group"] = new H5Group()
                            //{
                            ["names"] = names,
                            ["feats"] = feats,
                            Attributes = new()
                            {
                                ["folder"] = $"{feature_db}",
                                ["size"] = new int[] { 224, 224 }
                            }
                            //}
                        };
                        var option = new H5WriteOptions()
                        {
                            DefaultStringLength = 1024,
                            IncludeClassFields = true,
                            IncludeClassProperties = true,
                            IncludeStructFields = true,
                            IncludeStructProperties = true
                        };
                        h5.Write(file, option);
                        ReportMessage($"Saved Feature Data to {file}");
                    }
                    finally { sw.Stop(); ReportMessage($"Saved Feature Data to {file}, Elapsed: {sw.Elapsed.TotalSeconds:F4}s"); }
                });
            }
        }

        public async Task SaveFeatureData(FeatureData featuredata)
        {
            if (featuredata is FeatureData)
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
                        ReportMessage($"Changing Feature Data Folder from {old_folder} to {new_folder}", TaskStatus.Running);

                        float[,] feats;
                        string[] names;

                        (names, feats) = await LoadFeature(file);
                        names = names.Select(n => n.Replace(old_folder, new_folder)).ToArray();
                        await SaveFeature(file, names, feats);
                    }
                    finally { sw.Stop(); ReportMessage($"Changed Feature Data Folder from {old_folder} to {new_folder}, Elapsed: {sw.Elapsed.TotalSeconds:F4}s"); }
                });
            }
        }
        
        public async Task CleanImageFeature()
        {
            if (StorageList is List<Storage>)
            {
                foreach (var storage in StorageList)
                {
                    await CleanImageFeature(storage);
                }
            }
        }

        public async Task CleanImageFeature(Storage? storage)
        {
            if (storage is Storage)
            {
                var folder = storage.Folder;
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
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        ReportMessage($"Cleaning Feature Data from {file}", TaskStatus.Running);

                        float[,] feats;
                        string[] names;

                        var feat_obj = _features_.Where(f => f.FeatureDB.Equals(GetAbsolutePath(feature_db))).FirstOrDefault();

                        (names, feats) = await LoadFeature(file);
                        folder = GetAbsolutePath(folder);
                        var option = recuice ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                        var files = Directory.GetFiles(folder, "*.*", option).Where(f => exts.Contains(Path.GetExtension(f).ToLower()));
                        var diffs = names.Except(files).ToList(); //.Where(f => !string.IsNullOrEmpty(f)).ToList();
                        if (diffs.Count() > 0)
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

                            if (feat_obj is FeatureData && names_new.Length > 0 && feats_new.Length > 0)
                            {
                                feat_obj.Names = names_new;
                                feat_obj.Feats = new NDArray(feats_new);
                                feat_obj.Loaded = true;

                                ReportMessage($"Updated Feature DataTable to {file}, {sw.Elapsed.TotalSeconds:F4}s");
                            }
                        }
                    }
                    catch (Exception ex) { ReportMessage(ex.Message); }
                    finally { sw.Stop(); ReportMessage($"Cleaned Feature Data from {file}, Elapsed: {sw.Elapsed.TotalSeconds:F4}s"); }
                });
            }
        }

        public async Task<float[]?> ExtractImaegFeature(string file)
        {
            float[]? result = null;

            if (_model_ == null) await LoadModel();

            file = GetAbsolutePath(file);
            if (File.Exists(file) && _model_ is ITransformer && _predictionEngine_ != null)
            {
                try
                {
                    using (SKBitmap bmp_src = LoadImage(file))
                    {
                        ReportMessage($"Extract Feature of {file}");
                        result = await ExtractImaegFeature(bmp_src);
                    }
                }
                catch { }
            }

            return (result);
        }

        public async Task<float[]?> ExtractImaegFeature(SKBitmap? image)
        {
            float[]? result = null;
            if (image is SKBitmap)
            {
                ReportMessage($"Extracting Feature of Memory Image", TaskStatus.Running);
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

                        //if (result != null) result = Norm(result, zscore: false).ToArray();
                        if (result != null) result = la_norm(result);
                    }
                }
                catch (Exception ex) { ReportMessage(ex.Message); }
                finally { ReportMessage($"Extracted Feature of Memory Image"); }
            }
            return (result);
        }

        public async Task<List<KeyValuePair<string, double>>> QueryImageScore(string file, string? feature_db = null, int limit = 10, int padding = 0)
        {
            ReportMessage($"Query of {file}");
            return (await QueryImageScore(await ExtractImaegFeature(file), feature_db, limit, padding));
        }

        public async Task<List<KeyValuePair<string, double>>> QueryImageScore(SKBitmap? image, string? feature_db = null, int limit = 10, int padding = 0)
        {
            ReportMessage($"Query of Memory Image");
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
                    ReportMessage($"Quering of feature", TaskStatus.Running);

                    int count = 0;
                    limit = Math.Min(120, Math.Max(1, limit));
                    foreach (var feat_obj in string.IsNullOrEmpty(feature_db) ? _features_ : _features_.Where(f => f.FeatureDB.Equals(GetAbsolutePath(feature_db))).ToList())
                    {
                        if (!(feat_obj.Names is string[] && feat_obj.Names.Length > 0) || !(feat_obj.Feats is NDArray && feat_obj.Feats.shape[0] > 0)) await LoadFeatureData(feat_obj.FeatureDB);

                        count += limit;
                        var pad = new float[padding];

                        var feat_ = padding <= 0 ? new NDArray(feature) : new NDArray(feature.Concat(pad).ToArray());

                        if (feat_obj.Feats is NDArray && feat_ is NDArray)
                        {
                            var m_feat = np.zeros(1, feat_.shape[0]);
                            m_feat[0] = feat_;
                            var scores = np.dot(m_feat, feat_obj.Feats.T)[0];
                            var rank_ID = scores.argsort<float>(axis: 0)["::-1"];
                            var rank_score = scores[rank_ID].ToArray<double>();

                            for (int i = 0; i < feat_obj.Names.Length; i++)
                            {
                                var f_name = feat_obj.Names[rank_ID[i]];
                                if(string.IsNullOrEmpty(f_name)) continue;
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
                catch (Exception ex) { ReportMessage(ex.Message); }
                finally
                {
                    sw.Stop();
                    if (ModelLoadedState.CurrentCount == 0) ModelLoadedState.Release();
                    if (FeatureLoadedState.CurrentCount == 0) FeatureLoadedState.Release();
                    ReportMessage($"Queried of feature, Elapsed: {sw.Elapsed.TotalSeconds:F4}s");
                }
            }
            else ReportMessage("Model or Feature Database not loaded.");
            return (result);
        }

        public async Task<double> CompareImage(string file0, string file1, int padding = 0)
        {
            ReportMessage($"Comparing {file0}, {file1}");
            return (await CompareImage(await ExtractImaegFeature(file0), await ExtractImaegFeature(file1), padding));
        }

        public async Task<double> CompareImage(SKBitmap? image0, SKBitmap? image1, int padding = 0)
        {
            if (image0 is SKBitmap && image1 is SKBitmap)
            {
                ReportMessage($"Comparing Memory Images");
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
                    ReportMessage($"Compareing of feature", TaskStatus.Running);

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
                catch (Exception ex) { ReportMessage(ex.Message); }
                finally
                {
                    sw.Stop();
                    if (ModelLoadedState.CurrentCount == 0) ModelLoadedState.Release();
                    if (FeatureLoadedState.CurrentCount == 0) FeatureLoadedState.Release();
                    ReportMessage($"Compared of features : {result:F4}, Elapsed: {sw.Elapsed.TotalSeconds:F4}s");
                }
            }
            else ReportMessage("Model or Feature Database not loaded.");
            return (result);
        }
    }
}
