using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PixivWPF.Common
{
    public enum DownloadState { Idle, Downloading, Paused, Finished, Failed, Writing, Deleted, NonExists, Remove, Unknown }

    public class DownloadStateMark : IDisposable
    {
        public string Mark { get; set; } = string.Empty;
        public Brush Foreground { get; set; } = Application.Current.GetForegroundBrush();
        public Brush Background { get; set; } = Application.Current.GetBackgroundBrush();
        public string Text { get; set; } = string.Empty;

        public void Dispose()
        {
            if (Foreground is Brush) Foreground = null;
            if (Background is Brush) Background = null;
        }
    }

    public class ProgressInfo
    {
        ulong Received { get; set; } = 0;
        ulong Length { get; set; } = 0;
        ulong CurrentRate { get; set; } = 0;
        ulong AverageRate { get; set; } = 0;
        ulong MaxRate { get; set; } = 0;
        ulong MinRate { get; set; } = 0;
    }

    public class DownloadInfo : INotifyPropertyChanged, IDisposable
    {
        private Setting setting = Application.Current.LoadSetting();

        public string Name { get; set; } = string.Empty;

        private string tooltip = string.Empty;
        public string ToolTip
        {
            get { return (tooltip); }
            set
            {
                tooltip = value;
                NotifyPropertyChanged("Tooltip");
            }
        }

        public DateTime AddedTimeStamp { get; set; } = DateTime.Now;

        [DefaultValue(false)]
        public bool UsingProxy { get; set; } = false;
        public string Proxy { get; set; } = string.Empty;

        [DefaultValue(false)]
        public bool AutoStart { get; set; } = false;
        [DefaultValue(false)]
        public bool Canceled { get; set; } = false;

        private DownloadState state = DownloadState.Idle;
        [DefaultValue(DownloadState.Idle)]
        public DownloadState State
        {
            get { return state; }
            set
            {
                state = value;
                NotifyPropertyChanged("StateChanged");
                NotifyPropertyChanged();
            }
        }

        private string url = string.Empty;
        public string Url
        {
            get { return (url); }
            set
            {
                if (string.IsNullOrEmpty(value)) return;
                url = value;
                UpdateLikeState();
                FileName = Application.Current.SaveTarget(url.GetImageName(singlefile));
                if (UseLargePreview)
                {
                    var id = url.GetIllustId();
                    var idx = url.GetIllustPageIndex();
                    var illust = id.FindIllust();
                    if (illust.IsWork())
                        FileName = illust.GetOriginalUrl(singlefile ? 0 : idx).GetImageName(singlefile);
                    else
                    {
                        FileName = Regex.Replace(FileName, @"_p(\d+)_marter.*?\.", "$1.");
                        if (singlefile) FileName = FileName.Replace("_0.", ".");
                    }
                    FileName = Application.Current.SaveTarget(FileName);
                }
                if (!string.IsNullOrEmpty(FileName)) Name = Path.GetFileNameWithoutExtension(FileName);
                NotifyPropertyChanged("UrlChanged");
            }
        }

        public bool UseLargePreview { get; set; } = false;
        public bool SaveAsJPEG { get; set; } = false;

        public string IllustSID { get { return (Url.GetIllustId()); } }
        public Pixeez.Objects.Work Illust { get { return (IllustSID.FindIllust()); } }
        public long IllustID
        {
            get
            {
                var id = -1L;
                long.TryParse(Url.GetIllustId(), out id);
                return (id);
            }
        }
        public long UserID
        {
            get
            {
                var id = -1L;
                if (Illust is Pixeez.Objects.Work && Illust.User is Pixeez.Objects.UserBase)
                {
                    id = Illust.User.Id ?? -1L;
                }
                return (id);
            }
        }

        public string FileName { get; set; } = string.Empty;
        public string FolderName { get { return string.IsNullOrEmpty(FileName) ? string.Empty : Path.GetDirectoryName(FileName); } }
        public DateTime FileTime { get; set; } = DateTime.Now;

        [DefaultValue(0)]
        public long Received { get; set; } = 0;
        [DefaultValue(0)]
        public long Length { get; set; } = 0;
        public double ProgressPercent { get { return Length > 0 ? Received / Length * 100 : 0; } }
        public Tuple<double, double> Progress
        {
            get { return Tuple.Create<double, double>(Received, Length); }
            set
            {
                Received = (long)value.Item1;
                Length = (long)value.Item2;
                NotifyPropertyChanged("ProgressChanged");
            }
        }
        [DefaultValue(true)]
        public bool Overwrite { get; set; } = true;

        private bool singlefile = true;
        [DefaultValue(true)]
        public bool SingleFile
        {
            get { return singlefile; }
            set
            {
                singlefile = value;
                if (!string.IsNullOrEmpty(FileName))
                {
                    if (value) FileName = FileName.Replace("_0.", ".");
                    else FileName = Regex.Replace(FileName, @"^(\d+)\.", "$1_0.");
                }
                NotifyPropertyChanged("SingleFile");
            }
        }

        public ImageSource Thumbnail { get; set; } = null;
        private string thumbnail_url = string.Empty;
        public string ThumbnailUrl
        {
            get { return (thumbnail_url); }
            set { thumbnail_url = value; RefreshThumbnail(); }
        }

        private bool is_fav = false;
        public bool IsFav
        {
            get { return (is_fav); }
            set { is_fav = value; NotifyPropertyChanged("IsFav"); }
        }

        private bool is_follow = false;
        public bool IsFollow
        {
            get { return (is_follow); }
            set { is_follow = value; NotifyPropertyChanged("IsFollow"); }
        }

        private bool start = false;
        [DefaultValue(false)]
        public bool IsStart
        {
            get { return start; }
            set
            {
                start = value;
                if (value) Start();
                NotifyPropertyChanged("IsStart");
            }
        }

        public async void Start()
        {
            if (Instance is DownloadItem)
            {
                try
                {
                    await new Action(() =>
                    {
                        setting = Application.Current.LoadSetting();
                        if (start && !Instance.IsDownloading) Instance.Start(setting.DownloadWithFailResume);
                    }).InvokeAsync();
                }
                catch (Exception ex) { ex.ERROR($"{GetType().Name}_{IllustID}_Start"); }
            }
        }

        public DownloadItem Instance { get; set; } = null;

        public string FailReason { get; set; } = string.Empty;

        public DateTime StartTime { get; internal set; } = DateTime.Now;
        public DateTime EndTime { get; internal set; } = DateTime.Now;
        public TimeSpan TotalElapsed { get; internal set; } = TimeSpan.FromSeconds(0);
        public TimeSpan LastTotalElapsed { get; internal set; }
        public TimeSpan LastElapsed { get; internal set; }
        public DateTime LastModified { get; internal set; }

        public double DownRateCurrent { get; set; } = 0;
        public double DownRateAverage { get; set; } = 0;

        public DownloadInfo()
        {
            setting = Application.Current.LoadSetting();
        }

        ~DownloadInfo()
        {
            Dispose(false);
        }

        public void Close()
        {
            Dispose();
        }

        private bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                if (Instance is DownloadItem)
                {
                    Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate
                    {
                        try
                        {
                            Instance.PART_ThumbnailWait.Hide();
                            Instance.PART_Preview.Dispose();
                            Instance.CleanBuffer();
                            Instance = null;
                        }
                        catch (Exception ex) { ex.ERROR($"{GetType().Name}_{IllustID}_Dispose"); }
                    }));
                }
                Thumbnail = null;
            }
            disposed = true;
        }

        public void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            if (ProgressPercent == 100)
            {
                if (FileName.IsDownloaded(touch: false))
                    State = DownloadState.Finished;
                else if (State == DownloadState.Finished)
                    State = DownloadState.NonExists;
                NotifyPropertyChanged("StateChanged");
                NotifyPropertyChanged();
            }
        }

        public void UpdateLikeState()
        {
            try
            {
                var illust = Url.GetIllustId().FindIllust();
                if (illust is Pixeez.Objects.Work)
                {
                    IsFav = illust.IsLiked();
                    IsFollow = illust.User.IsLiked();
                    NotifyPropertyChanged();
                }
            }
            catch (Exception ex) { ex.ERROR($"{GetType().Name}_{IllustID}_UpdateLikeState"); }
        }

        public void RefreshState()
        {
            NotifyPropertyChanged("StateChanged");
        }

        public async void RefreshThumbnail(bool overwrite = false)
        {
            await new Action(async () =>
            {
                try
                {
                    if (Instance is DownloadItem) Instance.PART_ThumbnailWait.Show();

                    using (var img = await ThumbnailUrl.LoadImageFromUrl(overwrite, size: Application.Current.GetDefaultThumbSize()))
                    {
                        if (img.Source != null)
                        {
                            Thumbnail = img.Source;
                            if (Instance is DownloadItem) Instance.PART_ThumbnailWait.Hide();
                        }
                        else if (Instance is DownloadItem) Instance.PART_ThumbnailWait.Fail();
                    }
                }
                catch (Exception ex) { ex.ERROR($"{GetType().Name}_{IllustID}_RefreshThumbnail"); if (Instance is DownloadItem) Instance.PART_ThumbnailWait.Fail(); }
                finally
                {
                    if (Thumbnail == null && Instance is DownloadItem) Instance.PART_ThumbnailWait.Fail();
                    NotifyPropertyChanged("Thumbnail");
                }
            }).InvokeAsync();
        }

        public void UpdateInfo()
        {
            if (Instance is DownloadItem) Instance.UpdateInfo();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void RaisePropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// DownloadItemControl.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadItem : UserControl, INotifyPropertyChanged, IDisposable
    {
        private Setting setting = Application.Current.LoadSetting();

        private DownloadInfo Info { get; set; }

        #region member properties
        public bool Canceling
        {
            get { return (Info is DownloadInfo ? Info.Canceled : false); }
            set { if (Info is DownloadInfo) Info.Canceled = value; }
        }

        public string FailReason
        {
            get { return (Info is DownloadInfo ? Info.FailReason : string.Empty); }
            set { if (Info is DownloadInfo) Info.FailReason = value; }
        }

        public string Url
        {
            get { return (Info is DownloadInfo ? Info.Url : string.Empty); }
            set { if (Info is DownloadInfo) Info.Url = value; }
        }

        public bool SaveAsJPEG
        {
            get { return (Info is DownloadInfo ? Info.SaveAsJPEG : false); }
            set { if (Info is DownloadInfo) Info.SaveAsJPEG = value; }
        }

        public ImageSource Thumbnail
        {
            get { return (Info is DownloadInfo ? Info.Thumbnail : null); }
            set { if (Info is DownloadInfo) Info.Thumbnail = value; }
        }
        public string ThumbnailUrl
        {
            get { return (Info is DownloadInfo ? Info.ThumbnailUrl : string.Empty); }
            set { if (Info is DownloadInfo) Info.ThumbnailUrl = value; }
        }

        public string FileName
        {
            get { return (Info is DownloadInfo ? Info.FileName : string.Empty); }
            set
            {
                if (Info is DownloadInfo)
                {
                    Info.FileName = value;

                    PART_FileName.Text = Info.FileName;
                    PART_FileFolder.Text = FolderName;
                }
            }
        }

        public string FolderName
        {
            get { return (Info is DownloadInfo && !string.IsNullOrEmpty(Info.FileName) ? Path.GetDirectoryName(Info.FileName) : string.Empty); }
        }

        [DefaultValue(true)]
        public bool AutoStart
        {
            get { return (Info is DownloadInfo ? Info.AutoStart : false); }
            set { if (Info is DownloadInfo) Info.AutoStart = value; }
        }

        [DefaultValue(DownloadState.Idle)]
        public DownloadState State
        {
            get { return (Info is DownloadInfo ? Info.State : DownloadState.Unknown); }
            set
            {
                if (Info is DownloadInfo)
                {
                    Info.State = value;
                    NotifyPropertyChanged("StateChanged");
                }
            }
        }

        public Tuple<double, double> Progress
        {
            get { return (Info is DownloadInfo ? Info.Progress : new Tuple<double, double>(Received, Length)); }
        }

        public long Received
        {
            get { return (Info is DownloadInfo && Info.Received >= 0 ? Info.Received : 0); }
            set { if (Info is DownloadInfo) Info.Received = value; }
        }

        public long Length
        {
            get { return (Info is DownloadInfo && Info.Length >= 0 ? Info.Length : 0); }
            set { if (Info is DownloadInfo) Info.Length = value; }
        }

        public DateTime LastModified
        {
            get { return (Info is DownloadInfo ? Info.LastModified : default(DateTime)); }
            set { if (Info is DownloadInfo) Info.LastModified = value; }
        }

        [DefaultValue(true)]
        public bool Overwrite
        {
            get { return (Info is DownloadInfo ? Info.Overwrite : false); }
            set { if (Info is DownloadInfo) Info.Overwrite = value; }
        }

        [DefaultValue(true)]
        public bool SingleFile
        {
            get { return (Info is DownloadInfo ? Info.SingleFile : false); }
            set { if (Info is DownloadInfo) Info.SingleFile = value; }
        }
        public DateTime FileTime
        {
            get { return (Info is DownloadInfo ? Info.FileTime : default(DateTime)); }
            set { if (Info is DownloadInfo) Info.FileTime = value; }
        }

        [DefaultValue(false)]
        public bool IsIdle
        {
            get
            {
                if (State == DownloadState.Idle) return true;
                else return false;
            }
        }

        [DefaultValue(false)]
        public bool IsStart
        {
            get { return (Info is DownloadInfo ? Info.IsStart : false); }
            set { if (Info is DownloadInfo) Info.IsStart = value; }
        }

        [DefaultValue(false)]
        public bool IsPaused
        {
            get
            {
                if (State == DownloadState.Paused) return true;
                else return false;
            }
        }

        [DefaultValue(false)]
        public bool IsFailed
        {
            get
            {
                if (State == DownloadState.Failed) return true;
                else return false;
            }
        }

        [DefaultValue(false)]
        public bool IsFinished
        {
            get
            {
                if (State == DownloadState.Finished) return true;
                else return false;
            }
        }

        [DefaultValue(false)]
        public bool IsCanceling
        {
            get { return ((cancelSource is CancellationTokenSource && cancelSource.IsCancellationRequested) || Canceling); }
        }

        [DefaultValue(true)]
        public bool CanDownload
        {
            get
            {
                //return (State == DownloadState.Idle || State == DownloadState.Failed || State == DownloadState.Finished || State == DownloadState.NonExists);
                return (State == DownloadState.Idle || State == DownloadState.Failed || State == DownloadState.NonExists);
            }
        }

        [DefaultValue(false)]
        public bool IsDownloading
        {
            get
            {
                return (State == DownloadState.Downloading || State == DownloadState.Writing || (Downloading is SemaphoreSlim && Downloading.CurrentCount <= 0));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Progress helper
        private DateTime StartTick
        {
            get { return (Info is DownloadInfo ? Info.StartTime : default(DateTime)); }
            set { if (Info is DownloadInfo) Info.StartTime = value; }
        }
        private DateTime EndTick
        {
            get { return (Info is DownloadInfo ? Info.EndTime : default(DateTime)); }
            set { if (Info is DownloadInfo) Info.EndTime = value; }
        }
        private DateTime lastTick = DateTime.Now;
        private TimeSpan TotalElapsed
        {
            get { return (Info is DownloadInfo ? Info.TotalElapsed : default(TimeSpan)); }
            set { if (Info is DownloadInfo) Info.TotalElapsed = value; }
        }
        private TimeSpan LastTotalElapsed
        {
            get { return (Info is DownloadInfo ? Info.LastTotalElapsed : default(TimeSpan)); }
            set { if (Info is DownloadInfo) Info.LastTotalElapsed = value; }
        }
        private TimeSpan LastElapsed
        {
            get { return (Info is DownloadInfo ? Info.LastElapsed : default(TimeSpan)); }
            set { if (Info is DownloadInfo) Info.LastElapsed = value; }
        }

        private int lastRatesCount = 5;
        private Queue<double> lastRates = new Queue<double>();
        private double lastRate = 0;
        private double lastRateA = 0;
        private long lastReceived = 0;
        private Tuple<double, double> finishedProgress;
        internal IProgress<Tuple<double, double>> progress = null;

        private void InitProgress()
        {
            progress = new Progress<Tuple<double, double>>(i =>
            {
                if ((State == DownloadState.Downloading && LastElapsed.TotalSeconds >= 0.2) ||
                     State == DownloadState.Writing ||
                     State == DownloadState.Finished ||
                     State == DownloadState.NonExists)
                {
                    var received = i.Item1 >= 0 ? i.Item1 : 0;
                    var total = i.Item2 >= 0 ? i.Item2 : 0;
                    try
                    {
                        #region Update ProgressBar & Progress Info Text
                        TotalElapsed = LastTotalElapsed + (EndTick - StartTick);
                        var deltaA = TotalElapsed.TotalSeconds;
                        var rateA = deltaA > 0 ? received / deltaA : 0;
                        if (lastReceived > 0)
                        {
                            var rateC = lastReceived / LastElapsed.TotalSeconds;
                            if (rateC > 0)
                            {
                                lastRates.Enqueue(rateC);
                                if (lastRates.Count > lastRatesCount) lastRates.Dequeue();
                                lastRate = lastRates.Average(o => double.IsNaN(o) || o < 0 ? 0 : o);
                            }
                        }

                        var percent = total > 0 ? received / total : 0;
                        PART_DownloadProgress.Value = percent * 100;
                        PART_DownloadProgressPercent.Text = $"{State.ToString()}: {PART_DownloadProgress.Value:0.0}%";
                        PART_DownInfo.Text = $"Status : {Info.Received.SmartFileSize()} / {Info.Length.SmartFileSize()}, {lastRate.SmartSpeedRate()} / {rateA.SmartSpeedRate()}";
                        #endregion

                        #region Update Progress Info Text Color Gradient
                        var factor = PART_DownloadProgress.ActualWidth / PART_DownloadProgressPercent.ActualWidth;
                        var offset = Math.Abs((factor - 1) / 2);
                        PART_ProgressInfoLinear.StartPoint = new Point(0 - offset, 0);
                        PART_ProgressInfoLinear.EndPoint = new Point(1 + offset, 0);
                        PART_ProgressInfoLinearLeft.Offset = percent;
                        PART_ProgressInfoLinearRight.Offset = percent;
                        #endregion

                        if (Info is DownloadInfo)
                        {
                            Info.DownRateCurrent = lastRate;
                            Info.DownRateAverage = rateA;
                            Info.ToolTip = string.Join(Environment.NewLine, Info.GetDownloadInfo());
                            ToolTip = Info.ToolTip;
                        }

                        lastRateA = rateA;
                        lastReceived = 0;
                        lastTick = EndTick;
                        LastElapsed = new TimeSpan();
                    }
                    catch (Exception ex) { ex.ERROR($"{this.Name ?? GetType().Name}_ReportProgress"); }
                }
            });
        }

        private void UpdateProgress(bool force = false)
        {
            if (force) LastElapsed = TimeSpan.FromSeconds(1);
            if (progress is IProgress<Tuple<double, double>>) progress.Report(Progress);
        }

        public void UpdateInfo()
        {
            new Action(() => { UpdateProgress(); }).Invoke(async: true);
        }
        #endregion

        #region Update state helper
        private Dictionary<DownloadState, DownloadStateMark> DownloadStatusMark = new Dictionary<DownloadState, DownloadStateMark>()
        {
            {DownloadState.Finished, new DownloadStateMark() { Mark = "\uE930", Foreground = Application.Current.GetSucceedBrush() }},
            {DownloadState.NonExists, new DownloadStateMark() { Mark = "\uE946", Foreground = Application.Current.GetNonExistsBrush() }},
            {DownloadState.Downloading, new DownloadStateMark() { Mark = "\uEB41", Foreground = Application.Current.GetBackgroundBrush() }},
            {DownloadState.Writing, new DownloadStateMark() { Mark = "\uE78C", Foreground = Application.Current.GetBackgroundBrush() }},
            {DownloadState.Idle, new DownloadStateMark() { Mark = "\uEA3A", Foreground = Application.Current.GetBackgroundBrush() }},
            {DownloadState.Failed, new DownloadStateMark() { Mark = "\uEA39", Foreground = Application.Current.GetFailedBrush() }},

            {DownloadState.Deleted, new DownloadStateMark() { Mark = "\uE107" }},
            {DownloadState.Paused, new DownloadStateMark() { Mark = "\uE103" }},
            {DownloadState.Remove, new DownloadStateMark() { Mark = "\uE108" }},
            {DownloadState.Unknown, new DownloadStateMark() { Mark = "\uE9CE" }},
        };

        private void CheckProperties()
        {
            if (Tag is DownloadInfo)
            {
                try
                {
                    Info = Tag as DownloadInfo;
                    Info.Instance = this;

                    UpdateProgress();

                    if (State == DownloadState.Finished)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //PART_SaveAsJPEG.IsEnabled = false;
                    }
                    else if (State == DownloadState.NonExists)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //PART_SaveAsJPEG.IsEnabled = true;
                    }
                    else if (State == DownloadState.Downloading)
                    {
                        miRemove.IsEnabled = false;
                        miStopDownload.IsEnabled = true;
                        //PART_SaveAsJPEG.IsEnabled = true;
                    }
                    else if (State == DownloadState.Writing)
                    {
                        miRemove.IsEnabled = false;
                        miStopDownload.IsEnabled = false;
                        //PART_SaveAsJPEG.IsEnabled = false;
                    }
                    else if (State == DownloadState.Idle)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //PART_SaveAsJPEG.IsEnabled = true;
                    }
                    else if (State == DownloadState.Failed)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //PART_SaveAsJPEG.IsEnabled = true;
                    }
                    else if (State == DownloadState.Remove)
                    {
                        PART_Preview.Dispose();
                    }
                    else
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //PART_SaveAsJPEG.IsEnabled = true;
                    }

                    if (DownloadStatusMark.ContainsKey(State))
                    {
                        PART_DownloadStatusMark.Text = DownloadStatusMark[State].Mark;
                        PART_DownloadStatusMark.Foreground = DownloadStatusMark[State].Foreground;
                        //PART_DownloadStatusMark.Background = DownloadStatusMark[State].Background;
                    }
                    else
                    {
                        PART_DownloadStatusMark.Text = string.Empty;
                    }

                    miOpenImage.IsEnabled = FileName.IsDownloaded(touch: false);
                    miOpenFolder.IsEnabled = true;

                    PART_DownloadProgressPercent.Text = $"{State.ToString()}: {PART_DownloadProgress.Value:0.0}%";

                    PART_CopyIllustID.IsEnabled = miCopyIllustID.IsEnabled;
                    PART_StopDownload.IsEnabled = miStopDownload.IsEnabled;
                    PART_Remove.IsEnabled = miRemove.IsEnabled;
                    PART_Download.IsEnabled = miDownload.IsEnabled;

                    PART_OpenIllust.IsEnabled = miOpenIllust.IsEnabled;
                    PART_OpenFile.IsEnabled = miOpenImage.IsEnabled;
                    PART_OpenFolder.IsEnabled = miOpenFolder.IsEnabled;

                    PART_SaveAsJPEG.IsEnabled = !PART_OpenFile.IsEnabled;
                }
                catch (Exception ex) { ex.ERROR($"{this.Name ?? GetType().Name}_CheckProperties"); }
            }
        }

        public void UpdateDownloadState()
        {
            CheckProperties();
        }

        public async void UpdateLikeState()
        {
            await new Action(() =>
            {
                if (Info is DownloadInfo)
                {
                    Info.UpdateLikeState();
                }
            }).InvokeAsync();
        }
        #endregion

        #region Downloading helper
        private int HTTP_STREAM_READ_COUNT = 8192;
        private int HTTP_TIMEOUT = 60;

        private CancellationTokenSource cancelSource = null;
        private CancellationToken cancelToken;

        private CancellationTokenSource cancelReadStreamSource = null;

        private SemaphoreSlim Downloading = new SemaphoreSlim(1, 1);

        //private MemoryStream _DownloadStream = null;
        private byte[] _DownloadBuffer = null;
        public void CleanBuffer()
        {
            try
            {
                if (_DownloadBuffer is byte[]) _DownloadBuffer.Dispose(ref _DownloadBuffer);
            }
            catch (Exception ex) { ex.ERROR($"{this.Name ?? GetType().Name}_CleanBuffer"); }
        }

        private HttpClient httpClient = null;
        private async Task<HttpResponseMessage> GetAsyncResponse(string Url, bool continuation = false)
        {
            var start = _DownloadBuffer is byte[] ? _DownloadBuffer.Length : 0;
            if (!continuation || start <= 0) start = 0;
            var request = Application.Current.GetHttpRequest(Url, range_start: start);
            //request.Headers.Add("Range", $"bytes={start}-");

            httpClient = Application.Current.GetHttpClient(continuation, is_download: true);
            return (await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead));
        }

        private async Task<string> DownloadStreamAsync(HttpResponseMessage response, bool continuation = true)
        {
            string result = string.Empty;
            if (response == null)
            {
                FailReason = "Response is NULL";
                return (string.Empty);
            }
            using (var ms = new MemoryStream())
            {
                try
                {
                    setting = Application.Current.LoadSetting();

                    var lastUpdateBuffer = DateTime.Now;
                    LastElapsed = TimeSpan.FromSeconds(0.1);

                    if (continuation && _DownloadBuffer is byte[])
                    {
                        await ms.WriteAsync(_DownloadBuffer, 0, _DownloadBuffer.Length);
                        await ms.FlushAsync();
                    }

                    UpdateProgress();
                    response.EnsureSuccessStatusCode();
                    var LastModifiedOffset = response.Content.Headers.LastModified ?? default(DateTimeOffset);
                    LastModified = LastModifiedOffset.DateTime.ToLocalTime();

                    string ce = response.Content.Headers.ContentEncoding.FirstOrDefault();
                    var length = response.Content.Headers.ContentLength ?? 0;
                    var range = response.Content.Headers.ContentRange ?? new System.Net.Http.Headers.ContentRangeHeaderValue(0, 0, length);
                    var pos = range.From ?? 0;
                    Length = range.Length ?? 0;
                    if (Length > 0 && length > 0)
                    {
                        ms.Seek(pos, SeekOrigin.Begin);

                        finishedProgress = new Tuple<double, double>(Length, Length);

                        using (var cs = ce != null && ce == "gzip" ? new System.IO.Compression.GZipStream(await response.Content.ReadAsStreamAsync(), System.IO.Compression.CompressionMode.Decompress) : await response.Content.ReadAsStreamAsync())
                        {
                            lastReceived = 0;
                            Received = ms.Length;
                            UpdateProgress();

                            byte[] bytes = new byte[HTTP_STREAM_READ_COUNT];
                            int bytesread = 0;
                            do
                            {
                                if (IsCanceling || State == DownloadState.Failed)
                                {
                                    throw new Exception($"Download {Path.GetFileName(FileName)} has be canceled!");
                                }

                                cancelReadStreamSource = new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout));
                                using (cancelReadStreamSource.Token.Register(() => cs.Close()))
                                {
                                    bytesread = await cs.ReadAsync(bytes, 0, HTTP_STREAM_READ_COUNT, cancelReadStreamSource.Token).ConfigureAwait(false);
                                    EndTick = DateTime.Now;
                                }

                                if (bytesread > 0 && bytesread <= HTTP_STREAM_READ_COUNT && Received < Length)
                                {
                                    lastReceived += bytesread;
                                    Received += bytesread;
                                    await ms.WriteAsync(bytes, 0, bytesread);
                                    if (EndTick.DeltaSeconds(lastUpdateBuffer) >= setting.DownloadBufferUpdateFrequency)
                                    {
                                        _DownloadBuffer = ms.ToArray();
                                        lastUpdateBuffer = EndTick;
                                    }
                                    LastElapsed = EndTick - lastTick;
                                    UpdateProgress();
                                }
                            } while (bytesread > 0 && Received < Length);

                            if (Received == Length && State == DownloadState.Downloading)
                            {
                                if (TotalElapsed.TotalSeconds == 0) TotalElapsed = LastTotalElapsed + (EndTick - StartTick);
                                if (LastElapsed.TotalSeconds == 0) LastElapsed = EndTick - StartTick;
                                if (lastReceived == 0) lastReceived = Received;
                                _DownloadBuffer = ms.ToArray();
                                result = await SaveFile(FileName, _DownloadBuffer);
                            }
                            else DownloadExceptionProcess();
                        }
                    }
                    else
                    {
                        result = await Url.DownloadImage(FileName);
                        Length = result.GetFileLength();
                        if (Length <= 0) throw new Exception($"Download {Path.GetFileName(FileName)} Failed! File not exists.");
                        Received = Length;
                        State = DownloadState.Finished;
                    }
                }
                catch (Exception ex)
                {
                    var stack = string.IsNullOrEmpty(ex.StackTrace) ? string.Empty : $"{Environment.NewLine}{ex.StackTrace}";
                    FailReason = $"{ex.Message}{stack}";
                }
                finally
                {
                    if (State != DownloadState.Finished)
                    {
                        if (FailReason.Contains("416"))
                            CleanBuffer();
                        else
                        {
                            _DownloadBuffer = ms.ToArray();
                            Received = _DownloadBuffer.Length;
                        }
                    }
                    ms.Close();
                    ms.Dispose();
                }
            }
            return (result);
        }

        private void DownloadPreProcess(bool restart = false)
        {
            if (string.IsNullOrEmpty(Url)) throw new Exception($"Download URL is unknown!");
            if (!CanDownload && !restart) throw new Exception($"Download task can't start now!");

            IsStart = false;
            Canceling = false;

            cancelSource = new CancellationTokenSource();
            cancelToken = cancelSource.Token;

            FailReason = string.Empty;
            State = DownloadState.Downloading;

            if (restart && _DownloadBuffer is byte[]) CleanBuffer();

            LastElapsed = TimeSpan.FromSeconds(0);
            if (_DownloadBuffer is byte[])
            {
                TotalElapsed = EndTick - StartTick;
                Received = _DownloadBuffer.Length;
                lastReceived = 0;
            }
            else
            {
                TotalElapsed = new TimeSpan();
                Received = 0;
                lastReceived = 0;
                Length = Received = 0;
            }
            lastRates.Clear();
            LastTotalElapsed = TotalElapsed;
            StartTick = DateTime.Now;
            UpdateProgress(force: true);
        }

        private void DownloadExceptionProcess()
        {
            if (Received != Length)
            {
                throw new Exception($"Download {Path.GetFileName(FileName)} Failed! File size ({Received} Bytes) not matched with server's size ({Length} Bytes).");
            }
            else if (State == DownloadState.Finished)
            {
                var fi = new FileInfo(FileName);
                if (!fi.Exists) State = DownloadState.NonExists;
                else if (fi.Length != Length)
                {
                    throw new Exception($"Download {Path.GetFileName(FileName)} Failed! File size ({fi.Length} Bytes) not matched with server's size ({Length} Bytes).");
                }
            }
            else if (State != DownloadState.Downloading)
            {
                throw new Exception($"Download {Path.GetFileName(FileName)} finished, but state[{State}] error!");
            }
        }

        private void DownloadFinally(out string result)
        {
            result = string.Empty;

            IsStart = false;
            Canceling = false;

            setting = Application.Current.LoadSetting();

            LastElapsed = TimeSpan.FromSeconds(0);
            lastReceived = 0;

            if (State == DownloadState.Finished)
            {
                result = FileName;
                EndTick = DateTime.Now;

                CleanBuffer();

                FileName.Touch(Url, meta: true, force: true);

                var state = "Succeed";
                $"{FileName} {state}.".INFO("Download");
                if (setting.DownloadCompletedToast)
                    $"{Path.GetFileName(FileName)} is saved!".ShowDownloadToast(state, ThumbnailUrl, FileName, state);
                if (setting.DownloadCompletedSound && StartTick.DeltaSeconds(EndTick) > setting.DownloadCompletedSoundForElapsedSeconds)
                    this.Sound();
            }
            else if (State == DownloadState.Downloading)
            {
                if (string.IsNullOrEmpty(FailReason))
                    FailReason = "Unkonwn failed reason when downloading.";
                State = DownloadState.Failed;
            }

            try
            {
                if (httpClient is HttpClient)
                {
                    httpClient.CancelPendingRequests();
                    //httpClient.Dispose();
                    //httpClient = null;
                }
            }
            catch (Exception ex) { ex.ERROR($"{this.Name ?? GetType().Name}_DownloadFinally"); }

            if (cancelSource is CancellationTokenSource) cancelSource.Dispose();
            cancelSource = null;
            if (cancelReadStreamSource is CancellationTokenSource) cancelReadStreamSource.Dispose();
            cancelReadStreamSource = null;

            if (Downloading is SemaphoreSlim && Downloading.CurrentCount <= 0) Downloading.Release();
        }

        private async Task<string> DownloadDirectAsync(bool continuation = true, bool restart = false)
        {
            string result = string.Empty;

            if (await Downloading.WaitAsync(setting.DownloadWaitingTime))
            {
                try
                {
                    DownloadPreProcess(restart);
                    using (var response = await GetAsyncResponse(Url, continuation))
                    {
                        EndTick = DateTime.Now;
                        await DownloadStreamAsync(response, continuation);
                    }
                }
                catch (Exception ex)
                {
                    FailReason = ex.Message;
                    ex.ERROR($"{Name ?? GetType().Name ?? Info.Name ?? Path.GetFileNameWithoutExtension(FileName)}_DownloadDirectAsync");
                }
                finally
                {
                    DownloadFinally(out result);
                    UpdateProgress();
                }
            }
            return (result);
        }

        private async Task<string> DownloadAsync(bool continuation = true, bool restart = false)
        {
            string result = string.Empty;

            if (await Downloading.WaitAsync(setting.DownloadWaitingTime))
            {
                try
                {
                    DownloadPreProcess(restart);

                    Pixeez.Tokens tokens = await CommonHelper.ShowLogin();
                    using (var async_response = await tokens.SendRequestAsync(Pixeez.MethodType.GET, Url))
                    {
                        if (async_response is Pixeez.AsyncResponse)
                        {
                            EndTick = DateTime.Now;
                            await DownloadStreamAsync(async_response.Source, continuation);
                        }
                        else
                        {
                            throw new Exception($"Download {Path.GetFileName(FileName)} Failed! Connection Failed!");
                        }
                    }
                }
                catch (Exception ex)
                {
                    FailReason = ex.Message;
                }
                finally
                {
                    DownloadFinally(out result);
                    UpdateProgress();
                }
            }
            return (result);
        }

        private async Task<string> DownloadFromFile(string file)
        {
            string result = string.Empty;

            try
            {
                IsStart = false;
                Canceling = false;
                StartTick = DateTime.Now;
                FailReason = string.Empty;
                State = DownloadState.Downloading;
                result = await SaveFile(FileName, file);
            }
            catch (Exception ex) { ex.ERROR($"{this.Name ?? GetType().Name}_DownloadFromFile"); }
            finally
            {
                DownloadFinally(out result);
                UpdateProgress();
            }

            return (result);
        }

        private async Task<string> SaveFile(string FileName, byte[] bytes)
        {
            var result = FileName;
            try
            {
                await new Action(() =>
                {
                    State = DownloadState.Writing;
                    UpdateProgress();
                    if (SaveAsJPEG)
                    {
                        var ret = bytes.ConvertImageTo("jpg");
                        if (ret is byte[] && ret.Length > 16)
                            File.WriteAllBytes(FileName, ret);
                    }
                    else
                        File.WriteAllBytes(FileName, bytes);

                    if (File.Exists(FileName))
                    {
                        File.SetCreationTime(FileName, FileTime);
                        File.SetLastWriteTime(FileName, FileTime);
                        File.SetLastAccessTime(FileName, FileTime);
                        PART_OpenFile.IsEnabled = true;
                        PART_OpenFolder.IsEnabled = true;
                        FailReason = $"downloaded from remote site.";
                        State = DownloadState.Finished;
                    }
                }).InvokeAsync(true);
            }
            catch (Exception ex)
            {
                FailReason = ex.Message;
                State = DownloadState.Failed;
                if (!IsCanceling) throw new Exception($"Download {Path.GetFileName(FileName)} Failed! Error is {FailReason}");
            }
            finally
            {
                UpdateProgress();
            }
            return (result);
        }

        private async Task<string> SaveFile(string FileName, string source)
        {
            var result = FileName;
            try
            {
                if (File.Exists(source))
                {
                    await new Action(() =>
                    {
                        StartTick = DateTime.Now;
                        State = DownloadState.Writing;
                        var fi = new FileInfo(source);
                        Length = Received = fi.Length;
                        finishedProgress = new Tuple<double, double>(Received, Length);

                        if (File.Exists(source))
                        {
                            if (SaveAsJPEG)
                            {
                                var ret = File.ReadAllBytes(source).ConvertImageTo("jpg");
                                File.WriteAllBytes(FileName, ret);
                            }
                            else
                                File.Copy(source, FileName, true);
                        }

                        if (File.Exists(FileName))
                        {
                            File.SetCreationTime(FileName, FileTime);
                            File.SetLastWriteTime(FileName, FileTime);
                            File.SetLastAccessTime(FileName, FileTime);
                            PART_OpenFile.IsEnabled = true;
                            PART_OpenFolder.IsEnabled = true;
                            FailReason = $"copied from cached image.";
                            State = DownloadState.Finished;
                        }
                    }).InvokeAsync(true);
                }
            }
            catch (Exception ex)
            {
                FailReason = ex.Message;
                State = DownloadState.Failed;
                if (!IsCanceling) throw new Exception($"Download {Path.GetFileName(FileName)} from {source} Failed! Error is {FailReason}");
            }
            finally
            {
                UpdateProgress();
            }
            return (result);
        }

        public async void Start(bool continuation = true, bool restart = false)
        {
            var _downManager = Application.Current.GetDownloadManager();
            if (!_downManager.CanStartDownload) return;

            setting = Application.Current.LoadSetting();
            HTTP_STREAM_READ_COUNT = setting.DownloadHttpStreamBlockSize;
            HTTP_TIMEOUT = setting.DownloadHttpTimeout > 5 ? setting.DownloadHttpTimeout : 5;

            IsStart = false;
            AutoStart = false;

            var basename = Path.GetFileName(FileName);
            var msg_title = $"Warnning ({basename})";
            var msg_content = "Overwrite exists?";
            if (msg_title.IsMessagePopup(msg_content)) { State = DownloadState.Finished; return; }

            bool delta = true;
            if (File.Exists(FileName))
            {
                delta = new FileInfo(FileName).CreationTime.DeltaNowMillisecond() > setting.DownloadTimeSpan ? true : false;
                if (!delta) return;
                if (!(await msg_content.ShowMessageDialog(msg_title, MessageBoxImage.Warning))) { State = DownloadState.Finished; return; }
                restart = true;
            }

            if (IsDownloading) await Cancel();
            CheckProperties();

            var target_file = string.Empty;
            string fc = Url.GetImageCachePath();
            if (File.Exists(fc))
            {
                await new Action(async () =>
                {
                    if (await Downloading.WaitAsync(setting.DownloadWaitingTime))
                    {
                        target_file = await DownloadFromFile(fc);
                    }
                }).InvokeAsync();
            }
            else
            {
                if (CanDownload || restart)
                {
                    await new Action(async () =>
                    {
                        Random rnd = new Random();
                        await Task.Delay(rnd.Next(20, 200));
                        this.DoEvents();

                        if (Application.Current.DownloadUsingToken())
                            target_file = await DownloadAsync(continuation, restart);
                        else
                            target_file = await DownloadDirectAsync(continuation, restart);
                    }).InvokeAsync();
                }
            }
        }

        private async Task Cancel()
        {
            if (!Canceling)
            {
                Canceling = true;
                if (cancelSource is CancellationTokenSource && !cancelSource.IsCancellationRequested)
                {
                    cancelSource.Cancel();
                    await Task.Delay(250);
                    this.DoEvents();
                }
                if (cancelReadStreamSource is CancellationTokenSource && !cancelReadStreamSource.IsCancellationRequested)
                {
                    cancelReadStreamSource.Cancel();
                    await Task.Delay(250);
                    this.DoEvents();
                }
                try
                {
                    if (httpClient is HttpClient)
                    {
                        httpClient.CancelPendingRequests();
                        //httpClient.Dispose();
                        //httpClient = null;
                    }
                }
                catch (Exception ex) { ex.ERROR($"{this.Name ?? GetType().Name}_Cancel"); Canceling = false; }
                finally
                {
                    FailReason = "Manual Canceled!";
                    State = DownloadState.Failed;
                    if (Downloading is SemaphoreSlim && Downloading.CurrentCount <= 0) Downloading.Release();
                }
            }
        }
        #endregion

        public DownloadItem()
        {
            InitializeComponent();
            setting = Application.Current.LoadSetting();

            Info = new DownloadInfo() { Instance = this };

            PART_SaveAsJPEG.IsOn = Info.SaveAsJPEG;

            InitProgress();

            CheckProperties();
        }

        public DownloadItem(string url, bool autostart = true, bool jpeg = false)
        {
            InitializeComponent();
            setting = Application.Current.LoadSetting();

            Info = new DownloadInfo() { Instance = this, SaveAsJPEG = jpeg };

            PART_SaveAsJPEG.IsOn = Info.SaveAsJPEG;

            InitProgress();

            AutoStart = autostart;
            Url = url;

            CheckProperties();
        }

        public DownloadItem(DownloadInfo info)
        {
            InitializeComponent();
            setting = Application.Current.LoadSetting();

            if (info is DownloadInfo)
                Info = info;
            else
                Info = new DownloadInfo() { Instance = this };

            Info.Instance = this;

            PART_SaveAsJPEG.IsOn = Info.SaveAsJPEG;

            InitProgress();

            CheckProperties();
        }

        ~DownloadItem()
        {
            //foreach (var c in this.GetChildren<Control>())
            //{
            //    try
            //    {
            //        if ((c as Control).ToolTip != null)
            //        {
            //            this.Dispatcher.Invoke(() => {
            //                ToolTipService.SetIsEnabled(c as Control, false);
            //                (c as Control).ToolTip = null;
            //            });
            //        }
            //    }
            //    catch(Exception ex) { ex.ERROR("DownloadItem_RemoveToolTip"); }
            //}

            Dispose(false);
        }

        public void Close()
        {
            Dispose();
        }

        private bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate
                {
                    try
                    {
                        if (PART_Preview.Source != null) PART_Preview.Source = null;
                        PART_Preview.UpdateLayout();
                    }
                    catch (Exception ex) { ex.ERROR($"{this.Name ?? GetType().Name}_~DownloadItem"); }
                }));
                PART_Preview = null;
            }
            disposed = true;
        }

        private void Download_Loaded(object sender, RoutedEventArgs e)
        {
            CheckProperties();
            setting = Application.Current.LoadSetting();

            //PART_DownloadProgress.IsEnabled = true;
            PART_SaveAsJPEG.IsOn = Info.SaveAsJPEG;

            if (AutoStart)
            {
                if (State == DownloadState.Finished || State == DownloadState.Downloading) return;
                else if (State == DownloadState.Idle) //|| State == DownloadState.Failed)
                {
                    if (string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(PART_FileURL.Text)) Url = PART_FileURL.Text;
                    if (IsStart && !IsDownloading) Start(setting.DownloadWithFailResume);
                }
            }

            if (Info is DownloadInfo && Info.Illust.IsUgoira() && !string.IsNullOrEmpty(Info.FileName))
            {
                new Action(async () =>
                {
                    (await Info.Illust.GetUgoiraMeta(ajax: true)).MakeUgoiraConcatFile(Info.FileName);
                }).Invoke(async: true);
            }
        }

        private void Download_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (Info is DownloadInfo)
            {
                Info.ToolTip = string.Join(Environment.NewLine, Info.GetDownloadInfo());
                ToolTip = Info.ToolTip;
            }
        }

        private void PART_Download_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            setting = Application.Current.LoadSetting();

            CheckProperties();
            if (sender == PART_Preview)
            {
                if (PART_Preview.Source == null) PART_ThumbnailWait.Show();
                else PART_ThumbnailWait.Hide();
            }
            if (IsStart && !IsDownloading) Start(setting.DownloadWithFailResume);
        }

        private async void miActions_Click(object sender, RoutedEventArgs e)
        {
            setting = Application.Current.LoadSetting();
            if ((sender == miCopyIllustID || sender == PART_CopyIllustID) && !string.IsNullOrEmpty(Url))
            {
                Commands.CopyArtworkIDs.Execute(Url);
            }
            else if (sender == miCopyDonwnloadInfo)
            {
                Commands.CopyDownloadInfo.Execute(Info);
            }
            else if (sender == miRefreshThumb || sender == PART_ThumbnailWait)
            {
                if (Info is DownloadInfo) Info.RefreshThumbnail();
            }
            else if ((sender == miOpenIllust || sender == PART_OpenIllust) && !string.IsNullOrEmpty(Url))
            {
                var illust = Url.GetIllustId().FindIllust();
                if (illust is Pixeez.Objects.Work)
                    Commands.OpenWork.Execute(illust);
                else
                    Commands.OpenWork.Execute(Url);
            }
            else if (sender == miDownload || sender == PART_Download)
            {
                var continuation = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? !setting.DownloadWithFailResume : setting.DownloadWithFailResume;
                var restart = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? true : false;
                Start(continuation, restart);
            }
            else if (sender == miDownloadRestart)
            {
                var continuation = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? !setting.DownloadWithFailResume : setting.DownloadWithFailResume;
                Start(continuation, true);
            }
            else if (sender == miRemove || sender == PART_Remove)
            {
                if (State != DownloadState.Downloading) State = DownloadState.Remove;
            }
            else if (sender == miStopDownload || sender == PART_StopDownload)
            {
                if (State == DownloadState.Downloading) await Cancel();
            }
            else if (sender == miOpenImage || sender == PART_OpenFile)
            {
                FileName.OpenFileWithShell();
            }
            else if (sender == miOpenFolder || sender == PART_OpenFolder)
            {
                FileName.OpenFileWithShell(true);
            }
            else if (sender == miOpenImageProperties)
            {
                FileName.OpenShellProperties();
            }
            else if (sender == miShowImageMeta)
            {
                Commands.ShowMeta.Execute(FileName);
            }
            else if (sender == miTouchImageMeta)
            {
                Commands.TouchMeta.Execute(FileName);
            }
            else if (sender == miConvertImageToJpeg)
            {
                Commands.ConvertToJpeg.Execute(FileName);
            }
            else if (sender == PART_SaveAsJPEG)
            {
                if (State == DownloadState.Finished)
                    PART_SaveAsJPEG.IsOn = SaveAsJPEG;
                else
                    SaveAsJPEG = PART_SaveAsJPEG.IsOn;
            }
        }
    }
}
