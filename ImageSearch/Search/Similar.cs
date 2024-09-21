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
using System.Globalization;

namespace ImageSearch.Search
{
#pragma warning disable IDE0063
#pragma warning disable IDE0305
#pragma warning disable SYSLIB1045

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
        public DateTime StartTime { get; internal set; } = DateTime.Now;
        public DateTime CurrentTime { get; internal set; } = DateTime.Now;
        public DateTime LastestTime { get; internal set; } = DateTime.Now;
        public TimeSpan ElapsedTime { get; internal set; }
        public TimeSpan RemainingTime { get; internal set; }
        public TimeSpan EstimateTime { get { return (ElapsedTime + RemainingTime); } }
    }

    public class ExtraStorage
    {
        public string ImageFolder { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DatabaseFile { get; set; } = string.Empty;
    }

    public class Storage
    {
        internal protected ITransformer? Model { get; set; } = null;
        public string ImageFolder { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string ModelName { get; set; } = string.Empty;
        public string ModelFile { get; set; } = string.Empty;
        public string ModelInput { get; set; } = string.Empty;
        public string ModelOutput { get; set; } = string.Empty;
        public bool ModelHasSoftMax { get; set; } = false;

        public bool Recurice { get; set; } = false;
        public bool UseFullPath { get; set; } = true;

        public string DatabaseFile { get; set; } = string.Empty;

        public List<ExtraStorage> ExtraStorages { get; set; } = [];

        public string[] IccludeFiles { get; set; } = [];
        public string[] ExcludeFiles { get; set; } = [];
        public string[] IccludeFolders { get; set; } = [];
        public string[] ExcludeFolders { get; set; } = [];
    }

    public class ExtraFeatureData
    {
        public bool Loaded { get; set; } = false;
        public ExtraStorage FeatureStore { get; set; } = new();
        public string FeatureDB { get; set; } = string.Empty;
        public NDArray? Feats { get; set; } = np.zeros(0);
        public string[] Names { get; set; } = [];
    }

    public class FeatureData
    {
        public bool Loaded { get; set; } = false;
        public Storage FeatureStore { get; set; } = new();
        public string FeatureDB { get; set; } = string.Empty;
        public NDArray? Feats { get; set; } = np.zeros(0);
        public string[] Names { get; set; } = [];
        public List<ExtraFeatureData> ExtraFeatureDatas { get; set; } = [];
    }

    public class ModelInput
    {
        [VectorType(3 * 224 * 224)]
        [ColumnName("ImageByteData")]
        public float[]? Data { get; set; }
    }

    public class Prediction
    {
        [VectorType(1000)]
        [ColumnName("PredictionFeature")]
        public float[]? Feature { get; set; }
    }

    public class LabeledObject
    {
        public string? Label { get; set; }
        public float? Confidence { get; set; }
    }

    public class SimilarResult
    {
        public List<KeyValuePair<string, double>>? Results { get; set; } = null;
        public LabeledObject[]? Labels { get; set; } = null;
    }

    internal class Similar
    {
        private static readonly string[] exts = [ ".jpg", ".jpeg", ".bmp", ".png", ".tif", ".tiff", ".gif", ".webp" ];
        private static readonly string[] tiff_exts = [ ".tif", ".tiff" ];
        private static string AppPath = string.Empty;
        public Settings? Setting { get; set; } = null;

        private static CultureInfo _culture_ = CultureInfo.CurrentCulture;
        private static int _lcid_ = _culture_.LCID;

        public bool ModelHasSoftMax { get; set; } = false;

        public static string GetAbsolutePath(string relativePath)
        {
            if (string.IsNullOrEmpty(AppPath))
            {
                //FileInfo _dataRoot = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location);
                FileInfo _dataRoot = new (new Uri(System.Reflection.Assembly.GetExecutingAssembly().Location).LocalPath);
                if (_dataRoot.Directory is not null) AppPath = _dataRoot.Directory.FullName;
            }
            string fullPath = Path.IsPathRooted(relativePath) ? relativePath : Path.Combine(AppPath, relativePath);
            return fullPath;
        }

        #region Report Action & Helper
        public Action<string, TaskStatus>? ReportMessageAction;

        private void ReportMessage(string info, TaskStatus state = TaskStatus.Created)
        {
            ReportMessageAction?.Invoke(info, state);
        }

        public Action<Exception, TaskStatus>? ReportExceptionAction;

        private void ReportMessage(Exception ex, TaskStatus state = TaskStatus.Created)
        {
            ReportExceptionAction?.Invoke(ex, state);
        }

        public Action<BatchTaskInfo>? ReportBatchTaskAction;

        private void ReportBatchTask(BatchTaskInfo info)
        {
            if (info is not null)
            {
                ReportBatchTaskAction?.Invoke(info);
            }
        }

        public Action<double>? ReportProgressAction;

        private void ReportProgress(double percentage)
        {
            ReportProgressAction?.Invoke(percentage);
        }
        #endregion

        private static readonly int IMG_Width = 224;
        private static readonly int IMG_HEIGHT = 224;
        private SKSizeI IMG_SIZE = new(IMG_Width, IMG_HEIGHT);

        public string ModelLocation { get; set; } = string.Empty;
        public string ModelInputColumnName { get; set; } = string.Empty;
        public string ModelOutputColumnName { get; set; } = string.Empty;

        public bool ModelUseGpu { get; set; } = false;
        public int ModelGpuDeviceId { get; set; } = 0;

        public int MaxLabelWidth { get; set; } = 0;

        public int ResultMax { get; set; } = 1000;

        // Create MLContext
        private readonly MLContext mlContext = new(seed: 1) { FallbackToCpu = true, GpuDeviceId = null };

        private ITransformer? _model_;

        private PredictionEngine<ModelInput, Prediction>? _predictionEngine_;

        private readonly List<FeatureData> _features_ = [];

        public List<Storage> StorageList { get; set; } = [];

        #region background work thread
        private readonly SemaphoreSlim BatchTaskIdle = new(1,1);
        private CancellationTokenSource BatchCancel = new();
        public TaskStatus RunningStatue { get { return (BatchTaskIdle?.CurrentCount > 0 ? TaskStatus.WaitingForActivation : TaskStatus.Running); } }

        private void InitBatchTask()
        {
            try
            {
            }
            catch (Exception ex) { ReportMessage(ex); }
        }

        private static bool SameFeatureDB(string db_src, string db_dst)
        {
            var src = new Uri(GetAbsolutePath(db_src));
            var dst = new Uri(GetAbsolutePath(db_dst));
            return (src.AbsolutePath.Equals(dst.AbsolutePath));
        }

        private FeatureData? GetFeatureData(string feats_db)
        {
            return (_features_?.Where(f => SameFeatureDB(f.FeatureDB, feats_db)).FirstOrDefault());
        }

        private async Task<bool> CreateFeatureDataAsync(BatchTaskInfo info, CancellationToken? cancelToken = null)
        {
            var result = true;

            if (info is not null && BatchTaskIdle?.CurrentCount <= 0)
            {
                if (_model_ == null) await LoadModel();

                SemaphoreSlim MultiTask = new(info.ParallelLimit, info.ParallelLimit);

                var sw = Stopwatch.StartNew();
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

                    foreach (var db in _features_) db.FeatureDB = GetAbsolutePath(db.FeatureDB);

                    foreach (var storage in info.Folders)
                    {
                        if (cancel.IsCancellationRequested) { result = false; break; }

                        ReportProgress(0);

                        var folder = GetAbsolutePath(storage.ImageFolder);
                        var feats_db = GetAbsolutePath(storage.DatabaseFile);
                        var dir_name = Path.GetFileName(folder);
                        var npz_file = $@"data\{dir_name}_checkpoint_latest.npz";

                        var feat_obj = GetFeatureData(feats_db);
                        if (feat_obj == null)
                        {
                            if (File.Exists(storage.DatabaseFile) && await LoadFeatureData(storage))
                            {
                                feat_obj = GetFeatureData(feats_db);
                            }
                            else
                            {
                                feat_obj = new FeatureData() { FeatureDB = storage.DatabaseFile };
                                _features_.Add(feat_obj);
                            }
                        }

                        #region Loading latest feats dataset npz
                        if (cancel.IsCancellationRequested) { result = false; break; }
                        var option = storage.Recurice ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                        var files = Directory.GetFiles(folder, "*.*", option).Where(f => exts.Contains(Path.GetExtension(f).ToLower()));

                        if (cancel.IsCancellationRequested) { result = false; break; }

                        ConcurrentDictionary<string, float[]>? feats_list = new();
                        if (np.size(feat_obj?.Feats) > 0)
                        {
                            ReportMessage($"Pre-Processing feature list");
                            var feats_a = feat_obj.Feats.ToJaggedArray<float>() as float[][];
                            var names_a = feat_obj.Names;
                            var losts = names_a.Except(files).ToArray();
                            for (var i = 0; i < feats_a?.GetLength(0); i++)
                            {
                                if (cancel.IsCancellationRequested) { result = false; break; }
                                if (string.IsNullOrEmpty(names_a[i]) || losts.Contains(names_a[i])) continue;
                                feats_list[names_a[i]] = feats_a[i];
                            }
                            ReportMessage($"Pre-Processed feature list");
                        }

                        if (cancel.IsCancellationRequested) { result = false; break; }

                        ConcurrentDictionary<string, float[]>? feats = new();
                        if (File.Exists(npz_file))
                        {
                            try
                            {
                                using (var npz_fs = new FileStream(npz_file, FileMode.Open, FileAccess.Read))
                                {
                                    ReportMessage($"Loading latest checkpoint file {npz_file}");
                                    var checkpoint = np.Load_Npz<float[]>(npz_fs);
                                    var losts = checkpoint.Keys.Except(files).ToArray();
                                    foreach (var kv in checkpoint)
                                    {
                                        if (losts.Contains(kv.Key)) continue;
                                        feats[kv.Key] = kv.Value;
                                    }
                                    ReportMessage($"Loaded latest checkpoint file {npz_file}");
                                }
                            }
                            catch (Exception ex) { ReportMessage(ex); }
                        }
                        #endregion

                        if (cancel.IsCancellationRequested) { result = false; break; }

                        var in_npz = feats.Keys.Except(feats_list.Keys);
                        var diffs = files.Except(feats_list.Keys).Except(feats.Keys).ToArray();
                        int count = diffs.Length;
                        int total = feats.Count;
                        uint index = 0;

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
                                    //(var feat, _) = await ExtractImaegFeature(file);
                                    (var feat, _) = ExtractImaegFeature(file).GetAwaiter().GetResult();
                                    if (feat is not null) feats[file] = feat;

                                    var percent = (int)Math.Ceiling((float)index / count * 100.0);
                                    ReportProgress(percent);
                                    #endregion

                                    if (cancel.IsCancellationRequested) { result = false; loopstate.Stop(); return; }

                                    #region Save feats dataset for every CheckPoint
                                    var current = feats.Count;
                                    if (info.CheckPoint > 0 && (ulong)current % (uint)info.CheckPoint == 0)
                                    {
                                        try
                                        {
                                            ReportMessage($"Saving checkpoint file to {npz_file}");
                                            var last_checkpoint = (ulong)current - info.CheckPoint;
                                            var cp_npz_file = $@"data\{dir_name}_checkpoint_{last_checkpoint:00000000}.npz";
                                            if (File.Exists(npz_file)) File.Move(npz_file, cp_npz_file, true);
                                            np.Save_Npz(feats.ToDictionary(kv => kv.Key, kv => kv.Value as Array), npz_file);
                                            ReportMessage($"Saved checkpoint file to {npz_file}, {sw?.Elapsed:dd\\.hh\\:mm\\:ss}");
                                        }
                                        catch (Exception ex) { ReportMessage(ex); }
                                    }
                                    #endregion

                                    info.FileName = file;
                                    info.Current = index;
                                    info.Total = count;
                                    info.ElapsedTime = sw.Elapsed;
                                    if (percent <= 0) info.RemainingTime = TimeSpan.FromSeconds(count * 10);
                                    else if (percent >= 100) info.RemainingTime = TimeSpan.FromSeconds(0);
                                    else info.RemainingTime = TimeSpan.FromSeconds((100 - percent) * Math.Ceiling(sw.Elapsed.TotalSeconds / percent));
                                    ReportBatchTask(info);
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
                                    (var feat, _) = await ExtractImaegFeature(file);
                                    if (feat is not null) feats[file] = feat;

                                    var percent = (int)Math.Ceiling((float)index / count * 100.0);
                                    ReportProgress(percent);
                                    #endregion

                                    if (cancel.IsCancellationRequested) { result = false; return; }

                                    #region Save feats dataset for every CheckPoint
                                    var current = feats.Count;
                                    if (info.CheckPoint > 0 && (ulong)current % (uint)info.CheckPoint == 0)
                                    {
                                        try
                                        {
                                            ReportMessage($"Saving checkpoint file to {npz_file}");
                                            var last_checkpoint = (ulong)current - info.CheckPoint;
                                            var cp_npz_file = $@"data\{dir_name}_checkpoint_{last_checkpoint:00000000}.npz";
                                            if (File.Exists(npz_file)) File.Move(npz_file, cp_npz_file, true);
                                            np.Save_Npz(feats.ToDictionary(kv => kv.Key, kv => kv.Value as Array), npz_file);
                                            ReportMessage($"Saved checkpoint file to {npz_file}, {sw?.Elapsed:dd\\.hh\\:mm\\:ss}");
                                        }
                                        catch (Exception ex) { ReportMessage(ex); }
                                    }
                                    #endregion

                                    info.FileName = file;
                                    info.Current = index;
                                    info.Total = count;
                                    info.ElapsedTime = sw.Elapsed;
                                    if (percent <= 0) info.RemainingTime = TimeSpan.FromSeconds(count * 10);
                                    else if (percent >= 100) info.RemainingTime = TimeSpan.FromSeconds(0);
                                    else info.RemainingTime = TimeSpan.FromSeconds((100 - percent) * Math.Ceiling(sw.Elapsed.TotalSeconds / percent));
                                    ReportBatchTask(info);
                                }
                                catch (Exception ex) { ReportMessage(ex); }
                            });
                        }
                        else if (info.ParallelMode == BatchRunningMode.MultiTask)
                        {
                            List<Task> tasks = [];
                            foreach (var file in diffs)
                            {
                                if (await MultiTask?.WaitAsync(info.ParallelTimeOut, cancel))
                                {
                                    tasks.Add(Task.Run(async () =>
                                    {
                                        try
                                        {
                                            if (cancel.IsCancellationRequested) { result = false; return; }

                                            index++;
                                            if (string.IsNullOrEmpty(file)) return;

                                            #region Extracting image feature and store it to feats dataset
                                            (var feat, _) = await ExtractImaegFeature(file);
                                            if (!string.IsNullOrEmpty(file) && feat is not null) feats[file] = feat;

                                            var percent = (int)Math.Ceiling((float)index / count * 100.0);
                                            ReportProgress(percent);
                                            #endregion

                                            if (cancel.IsCancellationRequested) { result = false; return; }

                                            #region Save feats dataset for every CheckPoint
                                            var current = feats.Count;
                                            if (info.CheckPoint > 0 && (ulong)current % (uint)info.CheckPoint == 0)
                                            {
                                                try
                                                {
                                                    ReportMessage($"Saving checkpoint file to {npz_file}");
                                                    var last_checkpoint = (ulong)current - info.CheckPoint;
                                                    var cp_npz_file = $@"data\{dir_name}_checkpoint_{last_checkpoint:00000000}.npz";
                                                    if (File.Exists(npz_file)) File.Move(npz_file, cp_npz_file, true);
                                                    np.Save_Npz(feats.ToDictionary(kv => kv.Key, kv => kv.Value as Array), npz_file);
                                                    ReportMessage($"Saved checkpoint file to {npz_file}, {sw?.Elapsed:dd\\.hh\\:mm\\:ss}");
                                                }
                                                catch (Exception ex) { ReportMessage(ex); }
                                            }
                                            #endregion

                                            info.FileName = file;
                                            info.Current = index;
                                            info.Total = count;
                                            info.ElapsedTime = sw.Elapsed;
                                            if (percent <= 0) info.RemainingTime = TimeSpan.FromSeconds(count * 10);
                                            else if (percent >= 100) info.RemainingTime = TimeSpan.FromSeconds(0);
                                            else info.RemainingTime = TimeSpan.FromSeconds((100 - percent) * Math.Ceiling(sw.Elapsed.TotalSeconds / percent));
                                            ReportBatchTask(info);
                                        }
                                        catch (Exception ex) { ReportMessage(ex); }
                                        finally { MultiTask?.Release(); tasks.RemoveAll(t => t.Status == TaskStatus.RanToCompletion); }
                                    }, cancel));
                                    await Task.Delay(20);
                                }
                            }
                            Task.WaitAll(tasks.ToArray(), cancel);
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
                                        (var feat, _) = await ExtractImaegFeature(file);
                                        if (!string.IsNullOrEmpty(file) && feat is not null) feats[file] = feat;

                                        var percent = (int)Math.Ceiling((float)index / count * 100.0);
                                        ReportProgress(percent);
                                        #endregion

                                        if (cancel.IsCancellationRequested) { result = false; break; }

                                        #region Save feats dataset for every CheckPoint
                                        var current = feats.Count;
                                        if (info.CheckPoint > 0 && (ulong)current % (uint)info.CheckPoint == 0)
                                        {
                                            try
                                            {
                                                ReportMessage($"Saving checkpoint file to {npz_file}");
                                                var last_checkpoint = (ulong)current - info.CheckPoint;
                                                var cp_npz_file = $@"data\{dir_name}_checkpoint_{last_checkpoint:00000000}.npz";
                                                if (File.Exists(npz_file)) File.Move(npz_file, cp_npz_file, true);
                                                np.Save_Npz(feats.ToDictionary(kv => kv.Key, kv => kv.Value as Array), npz_file);
                                                ReportMessage($"Saved checkpoint file to {npz_file}, {sw?.Elapsed:dd\\.hh\\:mm\\:ss}");
                                            }
                                            catch (Exception ex) { ReportMessage(ex); }
                                        }
                                        #endregion

                                        info.FileName = file;
                                        info.Current = index;
                                        info.Total = count;
                                        info.ElapsedTime = sw.Elapsed;
                                        if (percent <= 0) info.RemainingTime = TimeSpan.FromSeconds(count * 10);
                                        else if (percent >= 100) info.RemainingTime = TimeSpan.FromSeconds(0);
                                        else info.RemainingTime = TimeSpan.FromSeconds((100 - percent) * Math.Ceiling(sw.Elapsed.TotalSeconds / percent));
                                        ReportBatchTask(info);
                                    }
                                    catch (Exception ex) { ReportMessage(ex); }
                                    finally { MultiTask?.Release(); }
                                }
                            }
                        }

                        if ((!File.Exists(feat_obj.FeatureDB) || count > 0 || in_npz.Any()) && !feats.IsEmpty && feats?.Count <= (total + count) && feat_obj is not null)
                        {
                            if (cancel.IsCancellationRequested) { result = false; break; }

                            #region Save feats dataset for current worker
                            try
                            {
                                var last_checkpoint = (ulong)feats.Count - (ulong)feats.Count % info.CheckPoint;
                                var cp_npz_file = $@"data\{dir_name}_checkpoint_{last_checkpoint:00000000}.npz";

                                if (File.Exists(npz_file)) File.Move(npz_file, $"{npz_file}.lastgood", true);
                                using (var npz_fs = new FileStream(npz_file, FileMode.CreateNew, FileAccess.Write))
                                {
                                    ReportMessage($"Saving latest checkpoint file {npz_file}");
                                    np.Save_Npz(feats.ToDictionary(kv => kv.Key, kv => kv.Value as Array), npz_fs);
                                    ReportMessage($"Saved latest checkpoint file {npz_file}");
                                }
                                GC.Collect();
                                ReportMessage($"Cleaning intermediate checkpoint files");
                                var cp_npz_files = Directory.GetFiles("data", $@"{dir_name}_checkpoint_*.npz", SearchOption.TopDirectoryOnly);
                                foreach (var npz in cp_npz_files.Where(f => Regex.IsMatch(f, @"checkpoint_\d+\.npz", RegexOptions.IgnoreCase))) File.Delete(npz);
                                ReportMessage($"Cleaned intermediate checkpoint files");
                            }
                            catch (Exception ex) { ReportMessage(ex); }
                            #endregion

                            if (cancel.IsCancellationRequested) { result = false; break; }

                            #region Save feats to HDF5
                            ReportMessage($"Post-Processing feature list");
                            var feats_new = feats_list.UnionBy(feats, kv => kv.Key).ToDictionary();

                            feat_obj.FeatureStore = storage;

                            feat_obj.Names = feats_new.Keys.ToArray();

                            feat_obj.Feats = null;
                            feat_obj.Feats = new NDArray(feats_new.Values.ToArray());

                            feats_new?.Clear();
                            feats_new = null;
                            GC.Collect();
                            ReportMessage($"Post-Processed feature list");

                            if (cancel.IsCancellationRequested) { result = false; break; }
                            result &= await SaveFeatureData(feat_obj);
                            #endregion
                        }

                        feats_list?.Clear();
                        feats_list = null;

                        feats?.Clear();
                        feats = null;

                        in_npz = null;
                        diffs = null;

                        files = null;

                        GC.Collect();
                    }
                }
                catch (Exception ex) { ReportMessage(ex); }
                finally
                {
                    MultiTask?.Dispose();
                    BatchCancel.TryReset();
                    if (BatchTaskIdle?.CurrentCount <= 0) BatchTaskIdle?.Release();
                    sw?.Stop();
                    System.Media.SystemSounds.Beep.Play();
                    ReportMessage($"Create feature datas finished, Elapsed: {sw?.Elapsed:dd\\.hh\\:mm\\:ss}", result ? TaskStatus.RanToCompletion : TaskStatus.Canceled);
                    GC.Collect();
                }
            }
            return (result);
        }
        #endregion

        private readonly SemaphoreSlim ModelLoadedState = new(1, 1);
        private readonly SemaphoreSlim FeatureLoadedState = new(1, 1);

        public Similar()
        {
            InitBatchTask();
            H5Filter.Register(new Blosc2Filter());
            mlContext.GpuDeviceId = ModelUseGpu ? 0 : null;
            MaxLabelWidth = LabelMap.Labels.Select(x => x.Length).Max();
        }

        #region norm/softmax/meanstd function
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
        private static float[]? SoftMax(float[]? array)
        {
            if (array is null) return (null);
            var exp = array.Select(x => (float)Math.Exp(x));
            float sum = exp.Sum();
            return (exp.Select(x => x / sum).ToArray());
        }

        private static float[]? InvSoftMax(float[]? array)
        {
            if (array is null) return (null);
            float c = (float)Math.Log(array.Length);
            var log = array.Select(x => (float)Math.Log(x) + c);
            return log.ToArray();
        }

        private static float[]? LA_Norm(float[]? array)
        {
            if (array is null) return (null);
            var ss = (float)Math.Sqrt(array.Select(a => a * a).Sum());
            return (array.Select(a => a / ss).ToArray());
        }
#if DEBUG
        private float[]? MeanStd(float[]? array)
#else
        private static float[]? MeanStd(float[]? array)
#endif
        {
            if (array is null) return (null);
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            var mean = new[] { 0.485f, 0.456f, 0.406f };
            var stddev = new[] { 0.229f, 0.224f, 0.225f };

            var bgr = new float[224, 224, 3];
            Buffer.BlockCopy(array, 0, bgr, 0, array.Length * sizeof(float));

            var rgb = new float[3, 224,224];
            for (int y = 0; y < 224; y++)
            {
                for (int x = 0; x < 224; x++)
                {
                    rgb[0, y, x] = (bgr[y, x, 2] / 255f - mean[0]) / stddev[0];
                    rgb[1, y, x] = (bgr[y, x, 1] / 255f - mean[1]) / stddev[1];
                    rgb[2, y, x] = (bgr[y, x, 0] / 255f - mean[2]) / stddev[2];
                }
            }

            var result = new float[array.Length];
            Buffer.BlockCopy(rgb, 0, result, 0, result.Length * sizeof(float));
#if DEBUG
            ReportMessage($"Convert BGR Array to RGB plan array, Elapsed : {sw?.Elapsed.TotalSeconds:F4}s");
#endif
            return (result);
        }
        #endregion

        #region Image data format convertor
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
        #endregion

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

            if ((_model_ == null || reload) && await ModelLoadedState.WaitAsync(0))
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

        #region Create Feature Database
#if DEBUG
        private async Task<bool> CreateFeatureData(string folder, string feature_db, bool recurise = false)
        {
            var result = false;
            if (await BatchTaskIdle?.WaitAsync(0))
            {
                var info = new BatchTaskInfo()
                {
                    DatabaseName = feature_db ?? string.Empty,
                    FolderName = folder,
                    Recurise = recurise,

                    CancelToken = BatchCancel.Token,
                    ParallelMode = Setting.ParallelMode,
                    ParallelLimit = Setting.ParallelLimit,
                    ParallelTimeOut = TimeSpan.FromSeconds(Setting.ParallelTimeOut),
                    CheckPoint = Setting.ParallelCheckPoint
                };

                if (!BatchCancel.TryReset() && BatchCancel.IsCancellationRequested) BatchCancel = new CancellationTokenSource();
                result = await Task.Run(async () =>
                {
                    return (await CreateFeatureDataAsync(info, BatchCancel.Token));
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
            return (result);
        }
#endif
        public async Task<bool> CreateFeatureData(Storage storage)
        {
            var result = false;
            if (await BatchTaskIdle?.WaitAsync(0))
            {
                var info = new BatchTaskInfo()
                {
                    Folders = [storage],

                    CancelToken = BatchCancel.Token,
                    ParallelMode = Setting.ParallelMode,
                    ParallelLimit = Setting.ParallelLimit,
                    ParallelTimeOut = TimeSpan.FromSeconds(Setting.ParallelTimeOut),
                    CheckPoint = Setting.ParallelCheckPoint
                };

                if (!BatchCancel.TryReset() && BatchCancel.IsCancellationRequested) BatchCancel = new CancellationTokenSource();
                result = await Task.Run(async () =>
                {
                    return (await CreateFeatureDataAsync(info, BatchCancel.Token));
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
            return (result);
        }

        public async Task<bool> CreateFeatureData(List<Storage> storage)
        {
            var result = false;
            if (await BatchTaskIdle?.WaitAsync(0))
            {
                var info = new BatchTaskInfo()
                {
                    Folders = storage,

                    CancelToken = BatchCancel.Token,
                    ParallelMode = Setting.ParallelMode,
                    ParallelLimit = Setting.ParallelLimit,
                    ParallelTimeOut = TimeSpan.FromSeconds(Setting.ParallelTimeOut),
                    CheckPoint = Setting.ParallelCheckPoint
                };

                if (!BatchCancel.TryReset() && BatchCancel.IsCancellationRequested) BatchCancel = new CancellationTokenSource();
                result = await Task.Run(async () =>
                {
                    return (await CreateFeatureDataAsync(info, BatchCancel.Token));
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
            return (result);
        }
        #endregion

        public void CancelCreateFeatureData()
        {
            BatchCancel?.CancelAsync();
        }

        #region Load Feature Database
        private async Task<(string[], float[,])> LoadFeatureFile(string feature_db, ulong start = 0, ulong count = 0)
        {
            float[,] feats = new float[0,0];
            string[] names = [];

            if (await FeatureLoadedState.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                (names, feats) = await Task.Run<(string[], float[,])>(() =>
                {
                    var sw = Stopwatch.StartNew();

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

                        PureHDF.Selections.Selection selection = start >= 0 && count > 0 ? new PureHDF.Selections.HyperslabSelection(start, count) : new PureHDF.Selections.AllSelection();
                        feats = h5_feats.Read<float[,]>(fileSelection: selection);
                        if (h5_names.Type.Class == H5DataTypeClass.String)
                            names = h5_names.Read<string[]>(fileSelection: selection);
                        else if (h5_names.Type.Class == H5DataTypeClass.VariableLength)
                            names = h5_names.Read<byte[][]>(fileSelection: selection).Select(x => Encoding.UTF8.GetString(x)).ToArray();

                        ReportMessage($"Loaded Feature Database [{names.Length}, {feats.Length}] from {feature_db}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                    }
                    catch (Exception ex) { ReportMessage($"{ex.StackTrace?.Split().LastOrDefault()} : {ex.Message}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s"); }
                    finally
                    {
                        if (FeatureLoadedState?.CurrentCount == 0) FeatureLoadedState?.Release();
                        sw?.Stop();
                        h5?.Dispose();
                        GC.Collect();
                    }
                    return ((names, feats));
                }).WaitAsync(TimeSpan.FromSeconds(60));
            }

            return ((names, feats));
        }

        private async Task<bool> LoadExtraFeatureData(ExtraStorage storage, FeatureData feats_obj, bool reload = false)
        {
            var result = false;
            if (storage is not null)
            {
                var sw = Stopwatch.StartNew();
                var file = GetAbsolutePath(storage.DatabaseFile);
                if (File.Exists(file))
                {
                    try
                    {
                        var feat_obj = feats_obj.ExtraFeatureDatas.Where(x => x.Equals(storage.DatabaseFile)).FirstOrDefault();
                        if (feat_obj == null || !feat_obj.Loaded || reload)
                        {
                            float[,] feats;
                            string[] names;
                            (names, feats) = await LoadFeatureFile(file);
                            result = await Task.Run(() =>
                            {
                                var ret = false;
                                ReportMessage($"Loading Extra Feature DataTable from {file}", RunningStatue);
                                if (names.Length > 0 && feats.Length > 0)
                                {
                                    if (feat_obj == null)
                                    {
                                        feats_obj.ExtraFeatureDatas.Add(new ExtraFeatureData()
                                        {
                                            FeatureStore = storage,
                                            FeatureDB = file,
                                            Names = names.Select(x => Path.Combine(GetAbsolutePath(storage.ImageFolder), x)).ToArray(),
                                            Feats = new NDArray(feats),
                                            Loaded = true
                                        });
                                    }
                                    else
                                    {
                                        feat_obj.FeatureStore = storage;
                                        feat_obj.Names = names.Select(x => Path.Combine(GetAbsolutePath(storage.ImageFolder), x)).ToArray();
                                        feat_obj.Feats = new NDArray(feats);
                                    }
                                    feats = new float[0, 0];
                                    names = [];
                                    GC.Collect();
                                    ReportMessage($"Loaded Extra Feature DataTable from {file}, {sw?.Elapsed.TotalSeconds:F4}s");
                                    ret = true;
                                }
                                else { ReportMessage($"Loading Extra Feature DataTable from {file} failed!"); }
                                return (ret);
                            });
                        }
                    }
                    catch (Exception ex) { ReportMessage(ex); }
                    finally { sw?.Stop(); GC.Collect(); }
                }
            }
            return (result);
        }

        public async Task<bool> LoadFeatureData(Storage storage, bool reload = false)
        {
            var result = false;
            if (storage is not null)
            {
                var sw = Stopwatch.StartNew();
                var file = GetAbsolutePath(storage.DatabaseFile);
                if (File.Exists(file))
                {
                    try
                    {
                        var feat_obj = GetFeatureData(storage.DatabaseFile);

                        if (feat_obj == null || !feat_obj.Loaded || reload)
                        {
                            float[,] feats;
                            string[] names;
                            (names, feats) = await LoadFeatureFile(file);
                            result = await Task.Run(() =>
                            {
                                var ret = false;
                                ReportMessage($"Loading Feature DataTable from {file}", RunningStatue);
                                if (names.Length > 0 && feats.Length > 0)
                                {
                                    if (feat_obj == null)
                                    {
                                        _features_.Add(new FeatureData()
                                        {
                                            FeatureStore = storage,
                                            FeatureDB = file,
                                            Names = names.Select(x => Path.Combine(GetAbsolutePath(storage.ImageFolder), x)).ToArray(),
                                            Feats = new NDArray(feats),
                                            Loaded = true
                                        });
                                    }
                                    else
                                    {
                                        feat_obj.FeatureStore = storage;
                                        feat_obj.Names = names.Select(x => Path.Combine(GetAbsolutePath(storage.ImageFolder), x)).ToArray();
                                        feat_obj.Feats = new NDArray(feats);
                                    }
                                    feats = new float[0, 0];
                                    names = [];
                                    GC.Collect();
                                    ReportMessage($"Loaded Feature DataTable from {file}, {sw?.Elapsed.TotalSeconds:F4}s");
                                    ret = true;
                                }
                                else { ReportMessage($"Loading Feature DataTable from {file} failed!"); }
                                return (ret);
                            });
                        }
                    }
                    catch (Exception ex) { ReportMessage(ex); }
                    finally { sw?.Stop(); GC.Collect(); }
                }
            }
            return (result);
        }

        public async Task<bool> LoadFeatureData(List<Storage> storages, bool reload = false)
        {
            var result = false;
            if (storages is not null && storages.Count > 0)
            {
                foreach (var storage in storages)
                {
                    result &= await LoadFeatureData(storage, reload: reload);
                }
            }
            return (result);
        }
        #endregion

        #region Save Feature Database
        private async Task<bool> SaveFeatureFile(string feature_db, string[] names, Array feats, string imagefolder, bool fullpath = false)
        {
            var result = false;
            var file = GetAbsolutePath(feature_db);
            if (Directory.Exists(Path.GetDirectoryName(file)) && names is not null && feats is not null)
            {
                await Task.Run(() =>
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        ReportMessage($"Saving Feature Data to {file}", RunningStatue);

                        fullpath = fullpath && !string.IsNullOrEmpty(imagefolder) && Directory.Exists(imagefolder);

                        if (File.Exists(file)) File.Move(file, $"{file}.lastgood", true);
                        names = fullpath ? names : names.Select(x => x.Replace(GetAbsolutePath(imagefolder), "").TrimStart(['\\', '/', ' ', '\0'])).ToArray();
                        var h5 = new H5File()
                        {
                            //["my-group"] = new H5Group()
                            //{
                            ["names"] = names.Select(x => Encoding.UTF8.GetBytes(x)).ToArray(),
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
                                new H5Filter(Id: Blosc2Filter.Id, Options: new()
                                {
                                    [Blosc2Filter.COMPRESSION_LEVEL] = 9,
                                    [Blosc2Filter.COMPRESSOR_CODE] = "blosclz", // blosclz, lz4, lz4hc, zlib or zstd
                                }),
                            ]
                        };
                        h5?.Write(file, option);
                        h5?.Clear();
                        GC.Collect();
                        ReportMessage($"Saved Feature Data to {file}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                        result = true;
                    }
                    catch (Exception ex) { ReportMessage($"{ex.StackTrace?.Split().LastOrDefault()} : {ex.Message}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s"); }
                    finally { sw?.Stop(); GC.Collect(); }
                });
            }
            return (result);
        }

        private async Task<bool> SaveFeatureData(FeatureData featuredata)
        {
            var result = false;
            if (featuredata is not null)
            {
                var file = GetAbsolutePath(featuredata.FeatureDB);
                if (Directory.Exists(Path.GetDirectoryName(file)))
                {
                    result = await SaveFeatureFile(file, featuredata.Names, featuredata.Feats.ToMuliDimArray<float>(), featuredata.FeatureStore.ImageFolder, featuredata.FeatureStore.UseFullPath);
                }
            }
            return (result);
        }
        #endregion

        private async Task<bool> ChangeImageFolder(string feature_db, string old_folder, string new_folder)
        {
            var result = false;
            old_folder = GetAbsolutePath(old_folder);
            new_folder = GetAbsolutePath(new_folder);

            var file = GetAbsolutePath(feature_db);
            if (File.Exists(file))
            {
                result = await Task.Run(async () =>
                {
                    var ret = false;
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        ReportMessage($"Changing Feature Data Folder from {old_folder} to {new_folder}", RunningStatue);

                        float[,] feats;
                        string[] names;

                        (names, feats) = await LoadFeatureFile(file);
                        names = names.Select(n => n.Replace(old_folder, new_folder)).ToArray();
                        await SaveFeatureFile(file, names, feats, new_folder, true);
                        ret = true;
                    }
                    catch (Exception ex) { ReportMessage(ex); }
                    finally { sw?.Stop(); ReportMessage($"Changed Feature Data Folder from {old_folder} to {new_folder}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s"); }
                    return (ret);
                });
            }
            return (result);
        }

        #region Clean Feature Database
        private async Task<bool> CleanImageFeature(string feature_db, string folder, bool recuice)
        {
            var result = false;
            var file = GetAbsolutePath(feature_db);
            if (File.Exists(file))
            {
                result = await Task.Run(async () =>
                {
                    bool ret = false;
                    if (await BatchTaskIdle.WaitAsync(0))
                    {
                        var sw = Stopwatch.StartNew();
                        try
                        {
                            ReportMessage($"Cleaning Feature Data from {file}", RunningStatue);

                            float[,] feats;
                            string[] names;

                            var feat_obj = GetFeatureData(feature_db);

                            (names, feats) = await LoadFeatureFile(file);
                            folder = Path.GetFullPath(folder);
                            var option = recuice ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                            var files = Directory.GetFiles(folder, "*.*", option).Where(f => exts.Contains(Path.GetExtension(f).ToLower()));
                            var diffs = names.Except(files).ToList(); //.Where(f => !string.IsNullOrEmpty(f)).ToList();
                            if (diffs.Count > 0)
                            {
                                var feat_list = new Dictionary<string, float[]>();
                                for (var i = 0; i < feats.GetLength(0); i++)
                                {
                                    if (string.IsNullOrEmpty(names[i])) continue;
                                    //var row = new List<float>();
                                    var row = new float[feats.GetLength(1)];
                                    for (var j = 0; j < feats.GetLength(1); j++)
                                    {
                                        //    row.Add(feats[i, j]);
                                        row[j] = feats[i, j];
                                    }
                                    //feat_list.Add(names[i], row.ToArray());
                                    feat_list.Add(names[i], row);
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

                                ret &= await SaveFeatureFile(file, names_new, feats_new, folder, feat_obj.FeatureStore.UseFullPath);

                                if (feat_obj is not null && names_new.Length > 0 && feats_new.Length > 0)
                                {
                                    feat_obj.Names = names_new;
                                    feat_obj.Feats = new NDArray(feats_new);
                                    feat_obj.Loaded = true;
                                    GC.Collect();
                                    ReportMessage($"Updated Feature DataTable to {file}, {sw?.Elapsed.TotalSeconds:F4}s");
                                }
                                ret &= true;
                            }
                        }
                        catch (Exception ex) { ReportMessage(ex); }
                        finally
                        {
                            if (BatchTaskIdle?.CurrentCount <= 0) BatchTaskIdle?.Release();
                            sw?.Stop();
                            GC.Collect();
                            ReportMessage($"Cleaned Feature Data {file}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                        }
                    }
                    return (ret);
                });
            }
            return (result);
        }

        public async Task<bool> CleanImageFeature(Storage? storage)
        {
            var result = false;
            if (storage is not null)
            {
                result = await CleanImageFeature(feature_db: storage.DatabaseFile, folder: storage.ImageFolder, recuice: storage.Recurice);
            }
            return (result);
        }

        public async Task<bool> CleanImageFeature()
        {
            var result = false;
            if (StorageList is not null)
            {
                foreach (var storage in StorageList)
                {
                    result &= await CleanImageFeature(storage);
                }
            }
            return (result);
        }
        #endregion

        public async Task<bool> MergeImageFeature(Storage feature_src, Storage feature_dst)
        {
            var result = false;

            if (feature_src is null || feature_dst is null) return (result);

            result = await Task.Run(async () =>
            {
                bool ret = false;
                if (await BatchTaskIdle.WaitAsync(0))
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        var file_src = GetAbsolutePath(feature_src.DatabaseFile);
                        var file_dst = GetAbsolutePath(feature_dst.DatabaseFile);

                        ReportMessage($"Merging Feature Data from {feature_src.DatabaseFile} to {feature_dst.DatabaseFile}", RunningStatue);

                        float[,] feats_src;
                        string[] names_src;

                        float[,] feats_dst;
                        string[] names_dst;

                        var feat_obj_src = GetFeatureData(file_src);
                        var feat_obj_dst = GetFeatureData(file_dst);

                        var folder_src = Path.GetFullPath(feature_src.ImageFolder);
                        var folder_dst = Path.GetFullPath(feature_dst.ImageFolder);

                        var option_dst = feature_dst.Recurice ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                        var files_dst = Directory.GetFiles(feature_dst.ImageFolder, "*.*", option_dst).Where(f => exts.Contains(Path.GetExtension(f).ToLower()));

                        (names_dst, feats_dst) = await LoadFeatureFile(file_dst);
                        ReportMessage($"Pre=Processing {file_dst}");
                        ReportProgress(0);
                        var diffs_dst = names_dst.Except(files_dst).ToList();
                        var feat_list_dst = new Dictionary<string, float[]>();
                        for (var i = 0; i < feats_dst.GetLength(0); i++)
                        {
                            ReportProgress((double)i / feats_dst.GetLength(0));
                            if (string.IsNullOrEmpty(names_dst[i]) || diffs_dst.Contains(names_dst[i]))
                            {
                                ReportMessage($"Remove {names_dst[i]}");
                                continue;
                            }
                            var row = new List<float>();
                            for (var j = 0; j < feats_dst.GetLength(1); j++)
                            {
                                row.Add(feats_dst[i, j]);
                            }
                            feat_list_dst.Add(names_dst[i], row.ToArray());
                        }

                        (names_src, feats_src) = await LoadFeatureFile(file_src);
                        ReportMessage($"Pre=Processing {file_src}");
                        ReportProgress(0);
                        names_src = names_src.Select(f => f.Replace(folder_src, folder_dst)).ToArray();
                        var diffs_src = names_src.Except(files_dst).ToList();
                        var feat_list_src = new Dictionary<string, float[]>();
                        for (var i = 0; i < feats_src.GetLength(0); i++)
                        {
                            ReportProgress((double)i / feats_src.GetLength(0));
                            if (string.IsNullOrEmpty(names_src[i]) || diffs_src.Contains(names_src[i])) continue;
                            var row = new List<float>();
                            for (var j = 0; j < feats_src.GetLength(1); j++)
                            {
                                row.Add(feats_src[i, j]);
                            }
                            feat_list_src.Add(names_src[i], row.ToArray());
                        }

                        ReportMessage($"Processing Feature DataTables", RunningStatue);
                        var feat_list =  feat_list_dst.UnionBy(feat_list_src, f => f.Key).DistinctBy(f => f.Key);
                        var feats_dict = feat_list.ToDictionary();

                        var names_new = feats_dict.Keys.ToArray();
                        var values = feats_dict.Values.ToArray();

                        float[,] feats_new = new float[values.GetLength(0), feats_src.GetLength(1)];
                        for (var i = 0; i < feats_new.GetLength(0); i++)
                        {
                            for (var j = 0; j < feats_new.GetLength(1); j++)
                            {
                                feats_new[i, j] = values[i][j];
                            }
                        }

                        result &= await SaveFeatureFile(file_dst, names_new, feats_new, folder_dst, feat_obj_dst.FeatureStore.UseFullPath);

                        if (feat_obj_dst is not null && names_new.Length > 0 && feats_new.Length > 0)
                        {
                            ReportMessage($"Updating Feature DataTable from {feature_src.DatabaseFile} to {feature_dst.DatabaseFile}", RunningStatue);

                            feat_obj_dst.Names = names_new;
                            feat_obj_dst.Feats = new NDArray(feats_new);
                            feat_obj_dst.Loaded = true;
                            GC.Collect();

                            ReportMessage($"Updated Feature DataTable from {feature_src.DatabaseFile} to {feature_dst.DatabaseFile}, {sw?.Elapsed.TotalSeconds:F4}s");
                        }
                        result &= true;

                    }
                    catch (Exception ex) { ReportMessage(ex); }
                    finally
                    {
                        if (BatchTaskIdle?.CurrentCount <= 0) BatchTaskIdle?.Release();
                        sw?.Stop();
                        GC.Collect();
                        ReportMessage($"Merged Feature Data from {feature_src.DatabaseFile} to {feature_dst.DatabaseFile}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                    }
                }
                return (ret);
            });

            return (result);
        }

        #region Update Feature Database for given files
        public async Task<bool> UpdateImageFeature(List<Storage> storages, string[] files)
        {
            var result = false;
            if (storages is not null && storages.Count > 0)
            {
                foreach (var storage in storages)
                {
                    result &= await UpdateImageFeature(storage, files);
                }
            }
            return (result);
        }

        public async Task<bool> UpdateImageFeature(Storage feature, string[] files)
        {
            var result = false;

            if (feature is null || files == null || files.Length == 0) return (result);

            result = await Task.Run(async () =>
            {
                bool ret = false;
                if (await BatchTaskIdle.WaitAsync(0))
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        ReportMessage($"Updating Feature Data for files", RunningStatue);
                        var file_db = GetAbsolutePath(feature.DatabaseFile);
                        var folder = Path.GetFullPath(feature.ImageFolder);

                        var feature_files = files.Select(x => Path.IsPathRooted(x) ? x : Path.Combine(folder, x)).Where(x => x.StartsWith(folder));
                        var total = feature_files.Count();
                        if (total > 0)
                        {
                            ReportMessage($"Pre=Processing {file_db}");
                            var feat_obj = GetFeatureData(file_db);
                            if (feat_obj is null) result &= await LoadFeatureData(feature);
                            if (feat_obj is not null)
                            {
                                var updated = false;
                                var count = 0;
                                NDArray? feats = feat_obj.Feats;
                                string[] names = feat_obj.Names;
                                ReportProgress(0);
                                foreach (var file in feature_files)
                                {
                                    count++;
                                    var idx = Array.IndexOf(names, Path.IsPathRooted(file) ? file : Path.Combine(folder, file));
                                    if (idx >= 0)
                                    {
                                        (var feat, _) = await ExtractImaegFeature(file);
                                        feats[idx] = feat;
                                        updated = true;
                                    }
                                    ReportProgress(count / total);
                                }
                                ReportProgress(100);
                                ret |= !updated || await SaveFeatureData(feat_obj);
                            }
                        }
                        GC.Collect();
                    }
                    catch (Exception ex) { ReportMessage(ex); }
                    finally
                    {
                        if (BatchTaskIdle?.CurrentCount <= 0) BatchTaskIdle?.Release();
                        sw?.Stop();
                        GC.Collect();
                        ReportMessage($"Updated Feature Data of Files, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                    }
                }
                return (ret);
            });

            return (result);
        }
        #endregion

        #region Get Guessed Image Label
        private async Task<LabeledObject[]?> GetImageLabel(NDArray? feat, bool softmax = false)
        {
            return (await GetImageLabel(feat?.ToArray<float>(), softmax));
        }

        private async Task<LabeledObject[]?> GetImageLabel(float[]? feat, bool softmax = false)
        {
            var result = await Task.Run(() =>
            {
                try
                {
                    if (LabelMap.LabelCultures.TryGetValue(_lcid_, out string[]? labels_lcid))
                    {
                        if (softmax)
                            return (SoftMax(feat)?.Select((x, i) => new LabeledObject() { Label = $"{labels_lcid[i]}({LabelMap.Labels[i]})", Confidence = x }).OrderByDescending(x => x.Confidence).Take(10).ToArray());
                        else
                            return (feat?.Select((x, i) => new LabeledObject() { Label = $"{labels_lcid[i]}({LabelMap.Labels[i]})", Confidence = x }).OrderByDescending(x => x.Confidence).Take(10).ToArray());
                    }
                    else
                    {
                        if (softmax)
                            return (SoftMax(feat)?.Select((x, i) => new LabeledObject() { Label = LabelMap.Labels[i], Confidence = x }).OrderByDescending(x => x.Confidence).Take(10).ToArray());
                        else
                            return (feat?.Select((x, i) => new LabeledObject() { Label = LabelMap.Labels[i], Confidence = x }).OrderByDescending(x => x.Confidence).Take(10).ToArray());
                    }
                }
                catch(Exception ex) { ReportMessage(ex); return []; }
            });
            return result;
        }

        public async Task<LabeledObject[]?> GetImageLabel(SKBitmap? image)
        {
            LabeledObject[]? label = null;

            if (image is not null)
            {
                if (_model_ == null) await LoadModel();

                var sw = Stopwatch.StartNew();
                ReportMessage($"Quering Label for Memory Image", RunningStatue);
                try
                {
                    (_, label) = await ExtractImaegFeature(image, labels: true);
                }
                catch (Exception ex) { ReportMessage(ex); }
                finally
                {
                    sw?.Stop();
                    ReportMessage($"Quered Label for Memory Image, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                }
            }
            return (label?.Take(10).ToArray());
        }

        public async Task<LabeledObject[]?> GetImageLabel(string file)
        {
            LabeledObject[]? label = null;

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
                            ReportMessage($"Quering Label from {file}", RunningStatue);
                            label = await GetImageLabel(bmp_src);
                        }
                    }
                }
                catch (Exception ex) { ReportMessage(ex); }
            }
            return (label);
        }
        #endregion

        #region Extract Image Feature
        public async Task<(float[]?, LabeledObject[]?)> ExtractImaegFeature(string file, bool labels = false)
        {
            float[]? feature = null;
            LabeledObject[]? label = null;

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
                            ReportMessage($"Extracting Feature from {file}");
                            (feature, label) = await ExtractImaegFeature(bmp_src, labels);
                        }
                    }
                }
                catch (Exception ex) { ReportMessage(ex); }
            }

            return (feature, label);
        }

        public async Task<(float[]?, LabeledObject[]?)> ExtractImaegFeature(SKBitmap? image, bool labels = false)
        {
            float[]? feature = null;
            LabeledObject[]? label = null;

            if (image is not null)
            {
                if (_model_ == null) await LoadModel();

                var sw = Stopwatch.StartNew();
                ReportMessage($"Extracting Feature from Memory Image", RunningStatue);
                try
                {
                    SKBitmap bmp_thumb;
                    if (image.BytesPerPixel < 3)
                    {
                        using (var bmp_palete = image.Resize(IMG_SIZE, SKFilterQuality.Medium))
                        {
                            bmp_thumb = new SKBitmap(bmp_palete.Width, bmp_palete.Height);
                            using (var bmp_canvas = new SKCanvas(bmp_thumb))
                            {
                                bmp_canvas.DrawImage(SKImage.FromBitmap(bmp_palete), 0f, 0f);
                            }
                        }
                    }
                    else bmp_thumb = image.Resize(IMG_SIZE, SKFilterQuality.Medium);

                    using (var img = MLImage.CreateFromPixels(bmp_thumb.Width, bmp_thumb.Height, MLPixelFormat.Bgra32, bmp_thumb.Bytes))
                    {
                        var img_data = MeanStd(img.GetBGRPixels.Select(b => (float)b).ToArray());
                        feature = _predictionEngine_?.Predict(new ModelInput { Data = img_data }).Feature;
                        if (feature != null)
                        {
                            if (labels) label = await GetImageLabel(feature, !ModelHasSoftMax);
                            //if (!ModelHasSoftMax) feature = SoftMax(feature);
                            if (ModelHasSoftMax) feature = InvSoftMax(feature);
                            feature = LA_Norm(feature);
                        }
                        img_data = [];
                    }
                    bmp_thumb?.Dispose();
                }
                catch (Exception ex) { ReportMessage(ex); }
                finally
                {
                    sw?.Stop();
                    ReportMessage($"Extracted Feature from Memory Image, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                }
            }
            return (feature, label);
        }
        #endregion

        #region Comparing the similarity of two images
        private async Task<double> CompareImage(float[]? feature0, float[]? feature1, int padding = 0)
        {
            double result = 0;
            if (feature0 == null || feature1 == null) return (result);

            var sw = Stopwatch.StartNew();
            if (await ModelLoadedState?.WaitAsync(TimeSpan.FromSeconds(30)) && await FeatureLoadedState?.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                try
                {
                    ReportMessage($"Compareing feature", RunningStatue);

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
                    if (ModelLoadedState?.CurrentCount == 0) ModelLoadedState?.Release();
                    if (FeatureLoadedState?.CurrentCount == 0) FeatureLoadedState?.Release();
                    ReportMessage($"Compared Result of 2 features : {result:F4}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                }
            }
            else ReportMessage("Model or Feature Database not loaded.");
            return (result);
        }

        public async Task<double> CompareImage(SKBitmap? image0, SKBitmap? image1, int padding = 0)
        {
            if (image0 is not null && image1 is not null)
            {
                ReportMessage($"Comparing Memory Images", RunningStatue);
                return (await CompareImage((await ExtractImaegFeature(image0)).Item1, (await ExtractImaegFeature(image1)).Item1, padding));
            }
            return (0);
        }

        public async Task<double> CompareImage(string file0, string file1, int padding = 0)
        {
            ReportMessage($"Comparing {file0}, {file1}", RunningStatue);
            return (await CompareImage((await ExtractImaegFeature(file0)).Item1, (await ExtractImaegFeature(file1)).Item1, padding));
        }
        #endregion

        #region Query Image Score for Similarity
        private static List<KeyValuePair<string, double>> GetNeareatImages(NDArray? feat, NDArray? feats, string[]? names, double limit, int result_max = 500)
        {
            var result = new List<KeyValuePair<string, double>>();
            if (feat is not null && feats is not null && names is not null && double.IsNormal(limit))
            {
                var m_feat = np.zeros(1, feat.shape[0]);
                m_feat[0] = feat;

                var scores = np.dot(m_feat, feats.T)[0];
                var rank_ID = scores.argsort<float>(axis: 0)["::-1"];
                var rank_score = scores[rank_ID].ToArray<double>();

                for (int i = 0; i < rank_score.Length; i++)
                {
                    if (limit < 1 && rank_score[i] < limit) continue;
                    else if (result.Count > result_max) break;

                    var f_name = names[rank_ID[i]];
                    if (string.IsNullOrEmpty(f_name)) continue;
                    f_name = GetAbsolutePath(f_name);
                    if (File.Exists(f_name))
                    {
                        result.Add(new KeyValuePair<string, double>(f_name, rank_score[i]));
                        if (limit > 1 && result.Count >= limit) break;
                    }
                }
            }
            return (result);
        }
#if DEBUG
        private readonly Func<NDArray?, NDArray?, string[]?, double, int, List<KeyValuePair<string, double>>> GetNeareatImagesFunc = static (feat, feats, names, limit, result_max) =>
        {
            var result = new List<KeyValuePair<string, double>>();
            if(feat is not null && feats is not null && names is not null && double.IsNormal(limit))
            {
                var m_feat = np.zeros(1, feat.shape[0]);
                m_feat[0] = feat;

                var scores = np.dot(m_feat, feats.T)[0];
                var rank_ID = scores.argsort<float>(axis: 0)["::-1"];
                var rank_score = scores[rank_ID].ToArray<double>();

                for (int i = 0; i < rank_score.Length; i++)
                {
                    if (limit < 1 && rank_score[i] < limit) continue;
                    else if (result.Count > result_max) break;

                    var f_name = names[rank_ID[i]];
                    if (string.IsNullOrEmpty(f_name)) continue;
                    f_name = GetAbsolutePath(f_name);
                    if (File.Exists(f_name))
                    {
                        result.Add(new KeyValuePair<string, double>(f_name, rank_score[i]));
                        if (limit > 1 && result.Count >= limit) break;
                    }
                }
            }
            return(result);
        };
#endif

        private async Task<List<KeyValuePair<string, double>>> QueryImageScore(float[]? feature, string? feature_db = null, double limit = 10, int padding = 0)
        {
            var result = new List<KeyValuePair<string, double>>();
            if (feature == null) return (result);

            var sw = Stopwatch.StartNew();
            if (_features_.Count > 0 && await ModelLoadedState?.WaitAsync(TimeSpan.FromSeconds(30)) && await FeatureLoadedState?.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                try
                {
                    ReportMessage($"Quering feature", RunningStatue);

                    limit = limit >= 1 ? Math.Ceiling(Math.Min(120, Math.Max(1, limit))) : Math.Max(0.1, limit);

                    var pad = new float[padding];
                    var feat_ = padding <= 0 ? new NDArray(feature) : new NDArray(feature.Concat(pad).ToArray());

                    foreach (var feat_obj in string.IsNullOrEmpty(feature_db) ? _features_ : [GetFeatureData(feature_db)])
                    {
                        if (!(feat_obj.Names is not null && feat_obj.Names.Length > 0) || !(feat_obj.Feats is not null && feat_obj.Feats.shape[0] > 0)) await LoadFeatureData(feat_obj.FeatureStore);

                        if (feat_obj.Feats is not null && feat_ is not null)
                        {
                            result.AddRange(GetNeareatImages(feat_, feat_obj.Feats, feat_obj.Names, limit, ResultMax));
                            GC.Collect();
                        }
                        if (feat_obj.ExtraFeatureDatas is not null)
                        {
                            foreach (var extra in feat_obj.ExtraFeatureDatas)
                            {
                                if (!(extra.Names is not null && extra.Names.Length > 0) || !(extra.Feats is not null && extra.Feats.shape[0] > 0)) await LoadExtraFeatureData(extra.FeatureStore, feat_obj);
                                result.AddRange(GetNeareatImages(feat_, extra.Feats, extra.Names, limit, ResultMax));
                                GC.Collect();
                            }
                        }
                    }
                    result = result.OrderByDescending(r => r.Value).Take(limit > 1 ? (int)limit : ResultMax).ToList();
                }
                catch (Exception ex) { ReportMessage(ex); }
                finally
                {
                    sw?.Stop();
                    if (ModelLoadedState?.CurrentCount == 0) ModelLoadedState?.Release();
                    if (FeatureLoadedState?.CurrentCount == 0) FeatureLoadedState?.Release();
                    ReportMessage($"Queried feature, result count: {result?.Count}, Elapsed: {sw?.Elapsed.TotalSeconds:F4}s");
                    GC.Collect();
                }
            }
            else ReportMessage("Model or Feature Database not loaded.");
            return (result);
        }

        public async Task<SimilarResult> QueryImageScore(SKBitmap? image, string? feature_db = null, double limit = 10, int padding = 0, bool labels = false)
        {
            ReportMessage($"Quering Memory Image", RunningStatue);
            var features = await ExtractImaegFeature(image, labels);
            //var result = new SimilarResult(){ Labels = features.Item2?.Where(x => x.Confidence >= confidence).ToArray(), Results = await QueryImageScore(features.Item1, feature_db, limit, padding) };
            var result = new SimilarResult(){ Labels = features.Item2?.Take(10).ToArray(), Results = await QueryImageScore(features.Item1, feature_db, limit, padding) };
            return (result);
        }

        public async Task<SimilarResult> QueryImageScore(string file, string? feature_db = null, double limit = 10, int padding = 0, bool labels = false)
        {
            ReportMessage($"Quering {file}", RunningStatue);
            var features = await ExtractImaegFeature(file, labels);
            //var result = new SimilarResult(){ Labels = features.Item2?.Where(x => x.Confidence >= confidence).ToArray(), Results = await QueryImageScore(features.Item1, feature_db, limit, padding) };
            var result = new SimilarResult(){ Labels = features.Item2?.Take(10).ToArray(), Results = await QueryImageScore(features.Item1, feature_db, limit, padding) };
            return (result);
        }
#endregion
    }
}
