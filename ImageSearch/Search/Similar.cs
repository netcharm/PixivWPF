using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;

using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;
using Microsoft.ML.Transforms.Onnx;

using NumSharp;
using PureHDF;
using SkiaSharp;
using Microsoft.ML.Trainers;
using Google.Protobuf.WellKnownTypes;
using System.Windows.Documents;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace ImageSearch.Search
{
    public class Storage
    {
        public string Folder { get; set; } = string.Empty;
        public bool Recurice { get; set; } = false;
        public string DatabaseFile { get; set; } = string.Empty;
    }

    public class BatchProgressInfo
    {
        public bool Recurise { get; set; } = false; 
        public string DatabaseName { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public IEnumerable<Storage> Folders { get; set; } = new List<Storage>();
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

    internal class Similar
    {
        private static string[] exts = [ ".jpg", ".jpeg", ".bmp", ".png", ".tif", ".tiff", ".gif", ".webp" ];


        public Action<string>? MessageReportAction;
        
        private void ReportMessage(string info)
        {
            if (MessageReportAction is Action<string> && !string.IsNullOrEmpty(info))
            {
                //MessageReportAction.DynamicInvoke(info);
                MessageReportAction.Invoke(info);
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

        private NDArray _feats_ { get; set; } = np.zeros(0);
        private string[] _names_ { get; set; } = Array.Empty<string>();

        public List<Storage> StorageList { get; set; } = new List<Storage>();

        #region background work thread
        //private CancellationTokenSource cancelSource;
        BackgroundWorker? BatchTask;

        public Action<BatchProgressInfo>? BatchReportAction;

        private void BatchReport(BatchProgressInfo info)
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

            Application.Current.Dispatcher.Invoke(() => {            

            });
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

        private void BatchTask_DoWork(object? sender, DoWorkEventArgs e)
        {
            if (e.Argument is BatchProgressInfo)
            {
                var info = e.Argument as BatchProgressInfo;
                ConcurrentDictionary<string, float[]> feats = new ConcurrentDictionary<string, float[]>();
                if (Directory.Exists(Path.GetFullPath(info.FolderName)))
                {
                    BatchTask.ReportProgress(0);
                    try
                    {
                        if (info.Folders is IEnumerable<Storage> && info.Folders.Count() <= 0)
                        {
                            info.Folders.Append(new Storage() { Folder = info.FolderName, Recurice = info.Recurise, DatabaseFile = info.DatabaseName });
                        }
                        foreach (var storage in info.Folders.Where(d => Directory.Exists(d.Folder)))
                        {
                            var folder = Path.GetFullPath(storage.Folder);
                            var option = storage.Recurice ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                            var files = Directory.EnumerateFiles(folder, "*.*", option).Where(f => exts.Contains(Path.GetExtension(f).ToLower()));
                            int index = 0;
                            int count = files.Count();
                            foreach (var file in files)
                            {
                                index++;
                                if (!_names_.Contains(file))
                                {
                                    if (BatchTask.CancellationPending) break;
                                    var feat = ExtractImaegFeature(file);
                                    if (feat is float[]) feats[file] = feat;
                                    e.Result = (float)index / count;
                                    BatchTask.ReportProgress((int)Math.Ceiling((float)index / count * 100.0));

                                    info.FileName = file;
                                    info.Current = index;
                                    info.Total = count;
                                    BatchReport(info);
                                }
                            }

                            if (feats.Count() > 0)
                            {
                                if (_names_ == null || _names_.Length == 0) _names_ = [];
                                _names_ = _names_.Union(feats.Keys).ToArray();

                                if (_feats_ is NDArray && _feats_.shape[0] > 0 && _feats_.shape[1] == feats.First().Value.Length)
                                    _feats_ = np.concatenate([_feats_, new NDArray(feats.Values.ToArray())]);
                                //_feats_ = np.array<float>([_feats_.Clone(), new NDArray(feats.Values.ToArray())]);
                                else
                                    _feats_ = new NDArray(feats.Values.ToArray());

                                if (BatchTask.CancellationPending) return;
                                SaveFeatureData(storage.DatabaseFile);
                            }
                        }
                    }
                    finally
                    {
                        BatchTask.ReportProgress(100);
                    }
                }
            }
        }
        #endregion

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
            //InitProgressBar();
            LoadModel();
        }

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

        public void CreateFeatureData(string folder, bool recurise = false, string? feature_db = null)
        {
            if (BatchTask is BackgroundWorker && !BatchTask.IsBusy)
            {
                var info = new BatchProgressInfo() { DatabaseName = feature_db ?? string.Empty, FolderName = folder, Recurise = recurise };
                BatchTask.RunWorkerAsync(info);
            }
        }

        public void LoadFeatureData(string? feature_db = null)
        {
            var file = GetAbsolutePath(feature_db ?? featureslLocation);
            if (File.Exists(file))
            {
                try
                {
                    ReportMessage($"Loading Feature Data from {file}");

                    float[,] feats;
                    string[] names;
                    (names, feats) = LoadFeature(file);
                    _names_ = names;
                    _feats_ = new NDArray(feats);

                    ReportMessage($"Loaded Feature Data from {file}");             
                }
                finally
                {
                }
            }
        }

        public (string[], float[,]) LoadFeature(string feature_db)
        {
            float[,] feats;
            string[] names;

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

                ReportMessage($"Loaded Feature Data from {feature_db}");
            }
            finally { h5.Dispose(); }
            return ((names, feats));
        }

        public void SaveFeatureData(string? feature_db = null)
        {
            var file = GetAbsolutePath(feature_db ?? featureslLocation);
            if (Directory.Exists(Path.GetDirectoryName(file)))
            {
                if (File.Exists(file)) File.Move(file, $"{file}.lastgood", true);

                SaveFeature(file, _names_, _feats_.ToMuliDimArray<float>());
            }
        }

        public void SaveFeature(string feature_db, string[] names, Array feats)
        {
            var file = GetAbsolutePath(feature_db ?? featureslLocation);
            if (Directory.Exists(Path.GetDirectoryName(file)) && names is string[] && feats is Array)
            {
                try
                {
                    ReportMessage($"Saving Feature Data from {file}");
                    if (File.Exists(file)) File.Move(file, $"{file}.lastgood", true);
                    var h5 = new H5File()
                    {
                        //["my-group"] = new H5Group()
                        //{
                        ["names"] = names,
                        ["feats"] = feats,
                        Attributes = new()
                        {
                            ["folder"] = "",
                            ["size"] = new int[] { 224, 224 }
                        }
                        //}
                    };
                    var option = new H5WriteOptions()
                    {
                        IncludeClassFields = true,
                        IncludeClassProperties = true,
                        IncludeStructFields = true,
                        IncludeStructProperties = true
                    };
                    h5.Write(file, option);
                    ReportMessage($"Saved Feature Data from {file}");
                }
                finally
                {

                }
            }
        }

        public void ChangeImageFolder(string feature_db, string old_folder, string new_folder)
        {
            old_folder = GetAbsolutePath(old_folder);
            new_folder = GetAbsolutePath(new_folder);

            var file = GetAbsolutePath(feature_db);
            if (File.Exists(file))
            {
                try
                {
                    ReportMessage($"Changing Feature Data Folder from {old_folder} to {new_folder}");

                    float[,] feats;
                    string[] names;

                    (names, feats) = LoadFeature(file);
                    names = names.Select(n => n.Replace(old_folder, new_folder)).ToArray();
                    SaveFeature(file, names, feats);

                    ReportMessage($"Changed Feature Data Folder from {old_folder} to {new_folder}");
                }
                finally
                {
                }
            }
        }

        public void ClesnImageFeature(string feature_db, string folder, bool recuice)
        {
            var file = GetAbsolutePath(feature_db);
            if (File.Exists(file))
            {
                try
                {
                    ReportMessage($"Clesning Feature Data from {file}");

                    float[,] feats;
                    string[] names;

                    (names, feats) = LoadFeature(file);

                    folder = Path.GetFullPath(folder);
                    var option = recuice ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    var files = Directory.EnumerateFiles(folder, "*.*", option).Where(f => exts.Contains(Path.GetExtension(f).ToLower()));
                    var diffs = names.Except(files).ToList();
                    
                    var feats_list = Enumerable.Range(0, feats.GetLength(0)).Select(row => Enumerable.Range(0, feats.GetLength(1)).Select(col => feats[row, col]).ToArray());
                    var feats_dict = names.Zip(feats_list).ToDictionary().Where(kv => !diffs.Contains(kv.Key));
                    
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

                    SaveFeature(file, names_new, feats_new);

                    ReportMessage($"Clesned Feature Data from {file}");
                }
                finally
                {
                }
            }
        }

        public void LoadAllFeatureData()
        {
            //foreach(var folder)
        }

        public void SaveAllFeatureData()
        {
            //foreach(var folder)
        }

        public ITransformer? LoadModel()
        {
            var model_file = GetAbsolutePath(modelLocation);
            if (File.Exists(model_file))
            {
                ReportMessage($"Loading Model from {model_file}");

                // Create IDataView from empty list to obtain input data schema
                var data = mlContext.Data.LoadFromEnumerable(new List<ModelInput>());

                var pipeline = mlContext.Transforms.ApplyOnnxModel(modelFile: model_file, outputColumnNames: ["resnetv24_dense0_fwd"], inputColumnNames: ["data"]);

                // Fit scoring pipeline
                _model_ = pipeline.Fit(data);

                _predictionEngine_ = mlContext.Model.CreatePredictionEngine<ModelInput, Prediction>(_model_);

                //mlContext.Transforms.CalculateFeatureContribution(_model_);
            }
            return _model_;
        }

        private IEnumerable<double> Norm(IEnumerable<double> array, bool zscore = false)
        {
            if (array == null) return (Array.Empty<double>());
            if(zscore)
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

        public float[]? ExtractImaegFeature(string file)
        {
            float[]? result = null;

            if (_model_ == null) LoadModel();

            file = Path.GetFullPath(file);
            if (File.Exists(file) && _model_ is ITransformer && _predictionEngine_ != null)
            {
                try
                {
                    var size = new SKSizeI(IMG_SIZE.Width, IMG_SIZE.Height);
                    using (SKBitmap bmp_src = SKBitmap.Decode(file))
                    {
                        ReportMessage($"Extract Feature of {file}");
                        result = ExtractImaegFeature(bmp_src);
                    }
                }
                catch { }
            }

            return (result);
        }

        public float[]? ExtractImaegFeature(SKBitmap image)
        {
            float[]? result = null;
            if (image is SKBitmap)
            {
                ReportMessage($"Extract Feature of Memory Image");
                var size = new SKSizeI(IMG_SIZE.Width, IMG_SIZE.Height);

                var bmp_thumb = image.Resize(size, SKFilterQuality.Medium);
                using (var img = MLImage.CreateFromPixels(bmp_thumb.Width, bmp_thumb.Height, MLPixelFormat.Bgra32, bmp_thumb.Bytes))
                {
                    var img_data = img.GetBGRPixels.Select(b => (float)b).ToArray();
                    result = _predictionEngine_.Predict(new ModelInput { Data = img_data }).Feature;

                    //if (result != null) result = Norm(result, zscore: false).ToArray();
                    if (result != null) result = la_norm(result);
                }
            }
            return (result);
        }

        public List<KeyValuePair<string, double>> QueryImageScore(string file, int padding = 0)
        {
            ReportMessage($"Query of {file}");
            return (QueryImageScore(ExtractImaegFeature(file), padding));
        }

        public List<KeyValuePair<string, double>> QueryImageScore(SKBitmap image, int padding = 0)
        {
            ReportMessage($"Query of Memory Image");
            return (QueryImageScore(ExtractImaegFeature(image), padding));
        }

        public List<KeyValuePair<string, double>> QueryImageScore(float[]? feature, int padding = 0)
        {
            var result = new List<KeyValuePair<string, double>>();
            if (feature == null) return (result);
            try
            {
                if (!(_names_ is string[] && _names_.Length > 0) || !(_feats_ is NDArray && _feats_.shape[0] > 0)) LoadFeatureData();

                ReportMessage($"Query of feature");

                var pad = new float[padding];

                var feat_ = padding <= 0 ? new NDArray(feature) : new NDArray(feature.Concat(pad).ToArray());

                if (_feats_ is NDArray && feat_ is NDArray)
                {
                    var m_feat = np.zeros(1, feat_.shape[0]);
                    m_feat[0] = feat_;
                    var scores = np.dot(m_feat, _feats_.T)[0];
                    var rank_ID = scores.argsort<float>(axis: 0)["::-1"];
                    var rank_score = scores[rank_ID].ToArray<double>();

                    for (int i = 0; i < _names_.Length; i++)
                    {
                        var f_name = Path.GetFullPath(_names_[rank_ID[i]]);
                        if (File.Exists(f_name))
                        {
                            result.Add(new KeyValuePair<string, double>(f_name, rank_score[i]));
                            if (result.Count >= 10) break;
                        }
                    }
                }
            }
            catch { }
            return (result);
        }
    }
}
