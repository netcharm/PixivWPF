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
    public enum DownloadItemState { Idle, Downloading, Paused, Finished, Failed, Writing, Deleted, NonExists, Remove, Older, NDays, Unknown }

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

        private DownloadItemState state = DownloadItemState.Idle;
        [DefaultValue(DownloadItemState.Idle)]
        public DownloadItemState State
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
                    {
                        //FileName = illust.GetOriginalUrl(singlefile ? 0 : idx).GetImageName(singlefile);
                        FileName = illust.GetPreviewUrl(singlefile ? 0 : idx, UseLargePreview).GetImageName(singlefile);
                        FileName = Application.Current.SaveTarget(FileName);
                        FolderName = Path.GetDirectoryName(FileName);
                    }
                }
                if (!string.IsNullOrEmpty(FileName)) Name = Path.GetFileNameWithoutExtension(FileName);
                NotifyPropertyChanged("UrlChanged");
            }
        }

        public bool UseLargePreview { get; set; } = false;
        public bool SaveAsJPEG { get; set; } = false;
        public int JPEGQuality { get; set; } = Application.Current.LoadSetting().DownloadConvertJpegQuality;

        public string IllustSID { get { return (Url.GetIllustId()); } }
        public Pixeez.Objects.Work Illust { get { return (IllustSID.FindIllust()); } }
        public long IllustID
        {
            get
            {
                var id = -1L;
                return (long.TryParse(Url.GetIllustId(), out id) ? id : -1L);
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
        public string FolderName { get { return string.IsNullOrEmpty(FileName) ? string.Empty : Path.GetDirectoryName(FileName); } set { } }
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
        public string ConvertReason { get; set; } = string.Empty;

        public DateTime StartTime { get; internal set; } = DateTime.Now;
        public DateTime EndTime { get; internal set; } = DateTime.Now;
        public TimeSpan TotalElapsed { get; internal set; } = TimeSpan.FromSeconds(0);
        public TimeSpan LastTotalElapsed { get; internal set; }
        public TimeSpan LastElapsed { get; internal set; }
        public DateTime LastModified { get; internal set; }

        public double DownRateCurrent { get; set; } = 0;
        public double DownRateAverage { get; set; } = 0;

        private object _tag_ = null;
        public object Tag
        {
            get { return (_tag_); }
            set { _tag_ = value; NotifyPropertyChanged("Tag"); }
        }

        public App.MenuItemSliderData CustomReduceQuality { get; set; } = Application.Current.GetDefaultReduceData();

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
#if DEBUG            
            GC.SuppressFinalize(this);
#endif            
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
                var fexist = File.Exists(FileName);
                if (!fexist && State == DownloadItemState.Finished)
                    State = DownloadItemState.NonExists;
                //else if (!(exists ?? true) && State == DownloadState.Finished)
                //    State = DownloadState.NonExists;
                //else if (FileName.IsDownloaded(touch: false))
                //    State = DownloadState.Finished;
                else if (fexist) State = DownloadItemState.Finished;

                if (State == DownloadItemState.NonExists) $"{FileName} has been deleted!".INFO("UpdateDownloadState");

                NotifyPropertyChanged("StateChanged");
                NotifyPropertyChanged();
            }
            $"{FileName} : {State}[{illustid}{(exists != null ? ", " : "")}{exists}]".DEBUG("UpdateDownloadState");
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

        public async void RefreshThumbnail(bool overwrite = false, bool force = false)
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
                            if (force) Thumbnail = null;
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

        public void SetSaveAsJPEG(bool? on)
        {
            if (Instance is DownloadItem) Instance.PART_SaveAsJPEG.IsOn = on ?? false;
            SaveAsJPEG = on ?? false;
        }

        public bool GetSaveAsJPEG()
        {
            return (Instance is DownloadItem ? Instance.PART_SaveAsJPEG.IsOn : SaveAsJPEG);
        }

        public void UpdateInfo(bool force = false)
        {
            if (Instance is DownloadItem) Instance.UpdateInfo(force);
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
            get { return (Info is DownloadInfo && Info.Canceled); }
            set { if (Info is DownloadInfo) Info.Canceled = value; }
        }

        public string FailReason
        {
            get { return (Info is DownloadInfo ? Info.FailReason : string.Empty); }
            set { if (Info is DownloadInfo) Info.FailReason = value; }
        }

        public string ConvertReason
        {
            get { return (Info is DownloadInfo ? Info.ConvertReason : string.Empty); }
            set { if (Info is DownloadInfo) Info.ConvertReason = value; }
        }

        public string Url
        {
            get { return (Info is DownloadInfo ? Info.Url : string.Empty); }
            set { if (Info is DownloadInfo) Info.Url = value; }
        }

        public bool SaveAsJPEG
        {
            get { return (Info is DownloadInfo && Info.SaveAsJPEG); }
            set { if (Info is DownloadInfo) Info.SaveAsJPEG = value; }
        }

        public int JPEGQuality
        {
            get { return (Info is DownloadInfo ? Info.JPEGQuality : setting.DownloadConvertJpegQuality); }
            set { if (Info is DownloadInfo) Info.JPEGQuality = value; }
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
            get { return (Info is DownloadInfo && Info.AutoStart); }
            set { if (Info is DownloadInfo) Info.AutoStart = value; }
        }

        [DefaultValue(DownloadItemState.Idle)]
        public DownloadItemState State
        {
            get { return (Info is DownloadInfo ? Info.State : DownloadItemState.Unknown); }
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
            get { return (Info is DownloadInfo ? Info.LastModified : default); }
            set { if (Info is DownloadInfo) Info.LastModified = value; }
        }

        [DefaultValue(true)]
        public bool Overwrite
        {
            get { return (Info is DownloadInfo && Info.Overwrite); }
            set { if (Info is DownloadInfo) Info.Overwrite = value; }
        }

        [DefaultValue(true)]
        public bool SingleFile
        {
            get { return (Info is DownloadInfo && Info.SingleFile); }
            set { if (Info is DownloadInfo) Info.SingleFile = value; }
        }
        public DateTime FileTime
        {
            get { return (Info is DownloadInfo ? Info.FileTime : default); }
            set { if (Info is DownloadInfo) Info.FileTime = value; }
        }

        [DefaultValue(false)]
        public bool IsIdle
        {
            get
            {
                if (State == DownloadItemState.Idle) return true;
                else return false;
            }
        }

        [DefaultValue(false)]
        public bool IsStart
        {
            get { return (Info is DownloadInfo && Info.IsStart); }
            set { if (Info is DownloadInfo) Info.IsStart = value; }
        }

        [DefaultValue(false)]
        public bool IsPaused
        {
            get
            {
                if (State == DownloadItemState.Paused) return true;
                else return false;
            }
        }

        [DefaultValue(false)]
        public bool IsFailed
        {
            get
            {
                if (State == DownloadItemState.Failed) return true;
                else return false;
            }
        }

        [DefaultValue(false)]
        public bool IsFinished
        {
            get
            {
                if (State == DownloadItemState.Finished) return true;
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
                return (State == DownloadItemState.Idle || State == DownloadItemState.Failed || State == DownloadItemState.NonExists);
            }
        }

        [DefaultValue(false)]
        public bool IsDownloading
        {
            get
            {
                return (State == DownloadItemState.Downloading || State == DownloadItemState.Writing || (Downloading is SemaphoreSlim && Downloading.CurrentCount <= 0));
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
            get { return (Info is DownloadInfo ? Info.StartTime : default); }
            set { if (Info is DownloadInfo) Info.StartTime = value; }
        }
        private DateTime EndTick
        {
            get { return (Info is DownloadInfo ? Info.EndTime : default); }
            set { if (Info is DownloadInfo) Info.EndTime = value > DateTime.Now ? DateTime.Now : value; }
        }
        private DateTime lastTick = DateTime.Now;
        private TimeSpan TotalElapsed
        {
            get { return (Info is DownloadInfo ? Info.TotalElapsed : default); }
            set { if (Info is DownloadInfo) Info.TotalElapsed = value; }
        }
        private TimeSpan LastTotalElapsed
        {
            get { return (Info is DownloadInfo ? Info.LastTotalElapsed : default); }
            set { if (Info is DownloadInfo) Info.LastTotalElapsed = value; }
        }
        private TimeSpan LastElapsed
        {
            get { return (Info is DownloadInfo ? Info.LastElapsed : default); }
            set { if (Info is DownloadInfo) Info.LastElapsed = value; }
        }

        private int lastRatesCount = 5;
        private Queue<double> lastRates = new Queue<double>();
        private double lastRate = 0;
        private double lastRateA = 0;
        private long lastReceived = 0;
        //private Tuple<double, double> finishedProgress;
        internal IProgress<Tuple<double, double>> progress = null;

        private void InitProgress()
        {
            progress = new Progress<Tuple<double, double>>(i =>
            {
                if ((State == DownloadItemState.Downloading && LastElapsed.TotalSeconds >= 0.25) ||
                     State == DownloadItemState.Writing ||
                     State == DownloadItemState.Finished ||
                     State == DownloadItemState.NonExists)
                {
                    var received = i.Item1 >= 0 ? i.Item1 : 0;
                    var total = i.Item2 >= 0 ? i.Item2 : 0;
                    try
                    {
                        #region Update ProgressBar & Progress Info Text
                        EndTick += LastTotalElapsed;
                        TotalElapsed = EndTick - StartTick;
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
                        var finished = State == DownloadItemState.Finished;
                        PART_DownloadProgress.Value = percent * 100;
                        PART_DownloadProgressPercent.Text = $"{State}: {PART_DownloadProgress.Value:0.0}%";
                        PART_DownInfo.Text = $"Status : {received.SmartFileSize(trimzero: finished)} / {total.SmartFileSize(trimzero: finished)}, {lastRate.SmartSpeedRate(trimzero: finished)} / {rateA.SmartSpeedRate(trimzero: finished)}";
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
            else if (LastElapsed.TotalMilliseconds < 250 && State == DownloadItemState.Downloading) return;
            if (progress is IProgress<Tuple<double, double>>) progress.Report(Progress);
        }

        public void UpdateInfo(bool force = false)
        {
            new Action(() => { UpdateProgress(force); }).Invoke(async: true, realtime: true);
        }
        #endregion

        #region Update state helper
        private Dictionary<DownloadItemState, DownloadStateMark> DownloadStatusMark = new Dictionary<DownloadItemState, DownloadStateMark>()
        {
            {DownloadItemState.Finished, new DownloadStateMark() { Mark = "\uE930", Foreground = Application.Current.GetSucceedBrush() }},
            {DownloadItemState.NonExists, new DownloadStateMark() { Mark = "\uE946", Foreground = Application.Current.GetNonExistsBrush() }},
            {DownloadItemState.Downloading, new DownloadStateMark() { Mark = "\uEB41", Foreground = Application.Current.GetBackgroundBrush() }},
            {DownloadItemState.Writing, new DownloadStateMark() { Mark = "\uE78C", Foreground = Application.Current.GetBackgroundBrush() }},
            {DownloadItemState.Idle, new DownloadStateMark() { Mark = "\uEA3A", Foreground = Application.Current.GetBackgroundBrush() }},
            {DownloadItemState.Failed, new DownloadStateMark() { Mark = "\uEA39", Foreground = Application.Current.GetFailedBrush() }},

            {DownloadItemState.Deleted, new DownloadStateMark() { Mark = "\uE107" }},
            {DownloadItemState.Paused, new DownloadStateMark() { Mark = "\uE103" }},
            {DownloadItemState.Remove, new DownloadStateMark() { Mark = "\uE108" }},
            {DownloadItemState.Unknown, new DownloadStateMark() { Mark = "\uE9CE" }},
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

                    if (State == DownloadItemState.Finished)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //PART_SaveAsJPEG.IsEnabled = false;
                    }
                    else if (State == DownloadItemState.NonExists)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //PART_SaveAsJPEG.IsEnabled = true;
                    }
                    else if (State == DownloadItemState.Downloading)
                    {
                        miRemove.IsEnabled = false;
                        miStopDownload.IsEnabled = true;
                        //PART_SaveAsJPEG.IsEnabled = true;
                    }
                    else if (State == DownloadItemState.Writing)
                    {
                        miRemove.IsEnabled = false;
                        miStopDownload.IsEnabled = false;
                        //PART_SaveAsJPEG.IsEnabled = false;
                    }
                    else if (State == DownloadItemState.Idle)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //PART_SaveAsJPEG.IsEnabled = true;
                    }
                    else if (State == DownloadItemState.Failed)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //PART_SaveAsJPEG.IsEnabled = true;
                    }
                    else if (State == DownloadItemState.Remove)
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

                    PART_DownloadProgressPercent.Text = $"{State}: {PART_DownloadProgress.Value:0.0}%";

                    PART_CopyIllustID.IsEnabled = miCopyIllustID.IsEnabled;
                    PART_StopDownload.IsEnabled = miStopDownload.IsEnabled;
                    PART_Remove.IsEnabled = miRemove.IsEnabled;
                    PART_Download.IsEnabled = miDownload.IsEnabled;

                    PART_OpenIllust.IsEnabled = miOpenIllust.IsEnabled;
                    PART_OpenFile.IsEnabled = miOpenImage.IsEnabled;
                    PART_OpenFolder.IsEnabled = miOpenFolder.IsEnabled;

                    PART_SaveAsJPEG.IsOn = Info.SaveAsJPEG;
                    PART_SaveAsJPEG.IsEnabled = !PART_OpenFile.IsEnabled || State == DownloadItemState.Downloading;

                    miSaveAsJPEG.IsChecked = PART_SaveAsJPEG.IsOn;
                    miSaveAsJPEG.IsEnabled = PART_SaveAsJPEG.IsEnabled;
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
                    LastElapsed = TimeSpan.FromSeconds(0.250);

                    if (continuation && _DownloadBuffer is byte[])
                    {
                        await ms.WriteAsync(_DownloadBuffer, 0, _DownloadBuffer.Length);
                        await ms.FlushAsync();
                    }

                    UpdateProgress();
                    response.EnsureSuccessStatusCode();
                    var LastModifiedOffset = response.Content.Headers.LastModified ?? default;
                    LastModified = LastModifiedOffset.DateTime.ToLocalTime();

                    string ce = response.Content.Headers.ContentEncoding.FirstOrDefault();
                    var length = response.Content.Headers.ContentLength ?? 0;
                    var range = response.Content.Headers.ContentRange ?? new System.Net.Http.Headers.ContentRangeHeaderValue(0, 0, length);
                    var pos = range.From ?? 0;
                    Length = range.Length ?? 0;
                    if (Length > 0 && length > 0)
                    {
                        //finishedProgress = new Tuple<double, double>(Length, Length);

                        lastReceived = 0;
                        Received = ms.Length;
                        UpdateProgress();

                        ms.Seek(pos, SeekOrigin.Begin);

                        using (var cs = ce != null && ce == "gzip" ? new System.IO.Compression.GZipStream(await response.Content.ReadAsStreamAsync(), System.IO.Compression.CompressionMode.Decompress) : await response.Content.ReadAsStreamAsync())
                        {
                            byte[] bytes = new byte[HTTP_STREAM_READ_COUNT];
                            int bytesread = 0;
                            do
                            {
                                if (IsCanceling || State == DownloadItemState.Failed)
                                {
                                    throw new Exception($"Download {Path.GetFileName(FileName)} has be canceled!");
                                }

                                cancelReadStreamSource = new CancellationTokenSource(TimeSpan.FromSeconds(setting.DownloadHttpTimeout));
                                using (cancelReadStreamSource.Token.Register(() => cs.Close()))
                                {
                                    bytesread = await cs.ReadAsync(bytes, 0, HTTP_STREAM_READ_COUNT, cancelReadStreamSource.Token).ConfigureAwait(false);
                                }

                                if (bytesread > 0 && bytesread <= HTTP_STREAM_READ_COUNT && Received < Length)
                                {
                                    EndTick = DateTime.Now;

                                    await ms.WriteAsync(bytes, 0, bytesread);
                                    if (EndTick.DeltaSeconds(lastUpdateBuffer) >= setting.DownloadBufferUpdateFrequency)
                                    {
                                        _DownloadBuffer = ms.ToArray();
                                        lastUpdateBuffer = EndTick;
                                    }
                                    
                                    lastReceived += bytesread;
                                    Received += bytesread;
                                    LastElapsed = EndTick - lastTick;
                                    if (LastElapsed.TotalMilliseconds >= 250) UpdateProgress();
                                }
                            } while (bytesread > 0 && Received < Length);

                            if (Received == Length && State == DownloadItemState.Downloading)
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
                        State = DownloadItemState.Finished;
                    }
                }
                catch (Exception ex)
                {
                    var stack = string.IsNullOrEmpty(ex.StackTrace) ? string.Empty : $"{Environment.NewLine}{ex.StackTrace}";
                    FailReason = ex.IsNetworkError() ? $"{ex.Message}" : $"{ex.Message}{stack}";
                }
                finally
                {
                    if (State != DownloadItemState.Finished)
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
            if (string.IsNullOrEmpty(Url)) throw new WarningException($"Download URL is unknown!");
            if (!CanDownload && !restart) throw new WarningException($"Download task can't start now!");

            IsStart = false;
            Canceling = false;

            cancelSource = new CancellationTokenSource();
            cancelToken = cancelSource.Token;

            FailReason = string.Empty;
            State = DownloadItemState.Downloading;

            if (restart && _DownloadBuffer is byte[]) CleanBuffer();

            LastElapsed = TimeSpan.FromSeconds(0);
            var force = false;
            if (_DownloadBuffer is byte[])
            {
                TotalElapsed = EndTick - StartTick;
                Received = _DownloadBuffer.Length;
                force = true;
            }
            else
            {
                TotalElapsed = new TimeSpan();
                Received = 0;
                Length = Received = 0;
                StartTick = DateTime.Now;
            }

            lastReceived = 0;
            lastRates.Clear();
            lastRate = 0;
            LastTotalElapsed = TotalElapsed;
            UpdateProgress(force: force);
        }

        private void DownloadExceptionProcess()
        {
            if (Received != Length)
            {
                throw new WarningException($"Download {Path.GetFileName(FileName)} Failed! File size ({Received} Bytes) not matched with server's size ({Length} Bytes).");
            }
            else if (State == DownloadItemState.Finished)
            {
                var fi = new FileInfo(FileName);
                if (!fi.Exists) State = DownloadItemState.NonExists;
                else if (fi.Length != Length)
                {
                    throw new WarningException($"Download {Path.GetFileName(FileName)} Failed! File size ({fi.Length} Bytes) not matched with server's size ({Length} Bytes).");
                }
            }
            else if (State != DownloadItemState.Downloading)
            {
                throw new WarningException($"Download {Path.GetFileName(FileName)} finished, but state[{State}] error!");
            }
        }

        private void DownloadFinally(out string result)
        {
            result = string.Empty;

            IsStart = false;
            Canceling = false;

            setting = Application.Current.LoadSetting();

            lastRates.Clear();
            lastRate = 0;
            LastElapsed = TimeSpan.FromSeconds(0);
            lastReceived = 0;

            if (State == DownloadItemState.Finished)
            {
                result = FileName;

                EndTick = DateTime.Now;

                CleanBuffer();

                FileName.Touch(Url, meta: true, force: true);

                var state = "Succeed";
                var as_jpeg = SaveAsJPEG ? $" As JPEG_Q<={setting.DownloadConvertJpegQuality} " : " ";
                $"{FileName}{as_jpeg}{state}.".INFO("Download");
                if (StartTick.DeltaSeconds(EndTick) > setting.DownloadCompletedSoundForElapsedSeconds)
                {
                    if (setting.DownloadCompletedToast) $"{Path.GetFileName(FileName)} is saved!".ShowDownloadToast(state, ThumbnailUrl, FileName, state);
                    if (setting.DownloadCompletedSound) this.Sound();
                }
            }
            else if (State == DownloadItemState.Downloading)
            {
                if (string.IsNullOrEmpty(FailReason))
                    FailReason = "Unkonwn failed reason when downloading.";
                State = DownloadItemState.Failed;

                EndTick = DateTime.Now;
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

            setting = Application.Current.LoadSetting();
            if (await Downloading.WaitAsync(setting.DownloadWaitingTime))
            {
                var retry = Math.Max(1, setting.DownloadFailAutoRetryCount) + 1;
                while (retry > 0)
                {
                    if (State == DownloadItemState.Finished) { retry = 0; break; }
                    try
                    {
                        retry--;
                        DownloadPreProcess(restart);
                        using (var response = await GetAsyncResponse(Url, continuation))
                        {
                            EndTick = DateTime.Now;
                            await DownloadStreamAsync(response, continuation);
                        }
                    }
                    catch (IOException ex)
                    {
                        if (retry > 0)
                        {
                            await Task.Delay(Application.Current.DownloadRetryDelay());
                            FailReason = $"{ex.Message}({ex.GetType()}) Retry = {retry}";
                            ex.ERROR($"DownloadDirectAsync_{Path.GetFileName(FileName)}", no_stack: true);
                        }
                        else
                        {
                            FailReason = $"{ex.Message}({ex.GetType()}) Retry Out!";
                            ex.ERROR($"DownloadDirectAsync_{Path.GetFileName(FileName)}", no_stack: true);
                        }
                    }
                    catch (WarningException ex)
                    {
                        retry = 0;
                        FailReason = $"{ex.Message}({ex.GetType()})";
                        ex.ERROR($"DownloadDirectAsync_{Path.GetFileName(FileName)}", no_stack: true);
                    }
                    catch (Exception ex)
                    {
                        if ((ex.IsNetworkError() || ex.IsCanceled(Canceling)) && retry > 0)
                        {
                            await Task.Delay(Application.Current.DownloadRetryDelay());
                            FailReason = $"{ex.Message}({ex.GetType()}) Retry = {retry}";
                            ex.ERROR($"DownloadDirectAsync_{Path.GetFileName(FileName)}", no_stack: true);
                        }
                        else
                        {
                            retry = 0;
                            FailReason = $"{ex.Message}({ex.GetType()})"; ;
                            ex.ERROR($"DownloadDirectAsync_{Path.GetFileName(FileName)}");
                        }
                    }
                    finally
                    {
                        DownloadFinally(out result);
                        UpdateProgress();
                    }
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
                catch (WarningException ex)
                {
                    FailReason = ex.Message;
                    ex.ERROR($"DownloadDirectAsync_{Info.Name ?? Path.GetFileNameWithoutExtension(FileName)}", no_stack: true);
                }
                catch (Exception ex)
                {
                    FailReason = ex.Message;
                    ex.ERROR($"DownloadDirectAsync_{Info.Name ?? Path.GetFileNameWithoutExtension(FileName)}");
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
                State = DownloadItemState.Downloading;
                result = await SaveFile(FileName, file);
            }
            catch (Exception ex) { ex.ERROR($"DownloadFromFile_{Info.Name ?? Path.GetFileNameWithoutExtension(FileName)}"); }
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
                    State = DownloadItemState.Writing;
                    UpdateProgress();
                    if (SaveAsJPEG)
                    {
                        setting = Application.Current.LoadSetting();
                        JPEGQuality = setting.DownloadConvertJpegQuality;
                        var reason = string.Empty;
                        var ret = bytes.ConvertImageTo("jpg", out reason);
                        ConvertReason += $" {reason}".Trim();
                        if (ret is byte[] && ret.Length > 16)
                        {
                            if (ret.Length >= bytes.Length)
                                File.WriteAllBytes(FileName, bytes);
                            else
                                File.WriteAllBytes(FileName, ret);
                        }
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
                        State = DownloadItemState.Finished;
                    }
                }).InvokeAsync(true);
            }
            catch (Exception ex)
            {
                FailReason = ex.Message;
                State = DownloadItemState.Failed;
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
                        //StartTick = DateTime.Now;
                        State = DownloadItemState.Writing;
                        var fi = new FileInfo(source);
                        Length = Received = fi.Length;
                        //finishedProgress = new Tuple<double, double>(Received, Length);

                        if (File.Exists(source))
                        {
                            if (SaveAsJPEG)
                            {
                                setting = Application.Current.LoadSetting();
                                JPEGQuality = setting.DownloadConvertJpegQuality;
                                var bytes = File.ReadAllBytes(source);
                                var reason = string.Empty;
                                var ret = bytes.ConvertImageTo("jpg", out reason);
                                ConvertReason += $" {reason}".Trim();
                                if (ret.Length >= bytes.Length)
                                    File.WriteAllBytes(FileName, bytes);
                                else
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
                            State = DownloadItemState.Finished;
                        }
                    }).InvokeAsync(true);
                }
            }
            catch (Exception ex)
            {
                FailReason = ex.Message;
                State = DownloadItemState.Failed;
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

            //var basename = Path.GetFileName(FileName);
            //var msg_title = $"Warnning ({basename})";
            //var msg_content = "Overwrite exists?";
            //if (msg_title.IsMessagePopup(msg_content)) { State = DownloadItemState.Finished; Received = Length; return; }

            //bool delta = true;
            //if (File.Exists(FileName) && (_DownloadBuffer == null || _DownloadBuffer.Length <= 0))
            //{
            //    delta = new FileInfo(FileName).CreationTime.DeltaNowMillisecond() > setting.DownloadTimeSpan ? true : false;
            //    if (!delta) return;
            //    if (!(await msg_content.ShowMessageDialog(msg_title, MessageBoxImage.Warning))) { State = DownloadItemState.Finished; return; }
            //    restart = true;
            //}

            IsStart = false;
            AutoStart = false;

            if (File.Exists(FileName) && IsDownloading) await Cancel();
            CheckProperties();

            if ((CanDownload || State == DownloadItemState.Finished) && Downloading is SemaphoreSlim && Downloading.CurrentCount <= 0) Downloading.Release();

            var target_file = string.Empty;
            string fc = Url.GetImageCacheFile();
            if (File.Exists(fc))
            {
                await Task.Run(async () =>
                {
                    if (await Downloading.WaitAsync(setting.DownloadWaitingTime))
                    {
                        target_file = await DownloadFromFile(fc);
                    }
                });
            }
            else
            {
                if (CanDownload || restart)
                {
                    if (restart) State = DownloadItemState.Idle;
                    await Task.Delay(Application.Current.Random(20, 200)).ContinueWith(async t =>
                    {
                        this.DoEvents();

                        if (Application.Current.DownloadUsingToken())
                            target_file = await DownloadAsync(continuation, restart);
                        else
                            target_file = await DownloadDirectAsync(continuation, restart);
                    });
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
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
                    State = DownloadItemState.Failed;
                    if (Downloading is SemaphoreSlim && Downloading.CurrentCount <= 0) Downloading.Release();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task<bool> OverwritePrompt()
        {
            var result = true;

            bool delta = true;
            var basename = Path.GetFileName(FileName);
            var msg_title = $"Warnning ({basename})";
            var msg_content = "Overwrite exists?";
            if (msg_title.IsMessagePopup(msg_content)) result = false; // { State = DownloadItemState.Finished; Received = Length; return; }
            else
            {
                if (File.Exists(FileName) && (_DownloadBuffer == null || _DownloadBuffer.Length <= 0))
                {
                    delta = new FileInfo(FileName).CreationTime.DeltaNowMillisecond() > setting.DownloadTimeSpan;
                    if (!delta) result = false;
                    else if (!(await msg_content.ShowMessageDialog(msg_title, MessageBoxImage.Warning))) { State = DownloadItemState.Finished; result = false; }
                }
            }

            return (result);
        }
        #endregion

        public DownloadItem()
        {
            InitializeComponent();

            setting = Application.Current.LoadSetting();

            Info = new DownloadInfo() { Instance = this, SaveAsJPEG = setting.DownloadAutoReduceToJpeg, Received = 0, Length = 0 };

            PART_SaveAsJPEG.IsOn = Info.SaveAsJPEG;
            miSaveAsJPEG.IsChecked = Info.SaveAsJPEG;

            InitProgress();

            CheckProperties();
        }

        public DownloadItem(string url, bool autostart = true, bool jpeg = false)
        {
            InitializeComponent();

            setting = Application.Current.LoadSetting();

            Info = new DownloadInfo() { Instance = this, SaveAsJPEG = jpeg, Received = 0, Length = 0 };

            PART_SaveAsJPEG.IsOn = Info.SaveAsJPEG;
            miSaveAsJPEG.IsChecked = Info.SaveAsJPEG;

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
                Info = new DownloadInfo() { Instance = this, SaveAsJPEG = setting.DownloadAutoReduceToJpeg, Received = 0, Length = 0 };

            Info.Instance = this;

            PART_SaveAsJPEG.IsOn = Info.SaveAsJPEG;
            miSaveAsJPEG.IsChecked = Info.SaveAsJPEG;

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
#if DEBUG            
            GC.SuppressFinalize(this);
#endif            
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
            try
            {
                IsEnabled = false;
                //PART_DownloadProgress.IsEnabled = true;
                PART_SaveAsJPEG.IsOn = Info.SaveAsJPEG;
                miSaveAsJPEG.IsChecked = Info.SaveAsJPEG;

                if (AutoStart)
                {
                    if (State == DownloadItemState.Finished || State == DownloadItemState.Downloading) return;
                    else if (State == DownloadItemState.Idle) //|| State == DownloadState.Failed)
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
            finally { IsEnabled = true; }
        }

        private void Download_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            if (Info is DownloadInfo)
            {
                UpdateProgress();
                var down_info = Info.GetDownloadInfo();
                var statu = down_info.Where(l => l.StartsWith("Status")).FirstOrDefault();
                Info.ToolTip = string.Join(Environment.NewLine, down_info);
                ToolTip = Info.ToolTip;

                //if (!string.IsNullOrEmpty(statu) && State != DownloadState.Finished)
                //{
                //    PART_DownInfo.Text = $"Status : {Info.Received.SmartFileSize()} / {Info.Length.SmartFileSize()}, {lastRate.SmartSpeedRate()} / {Info.DownRateAverage.SmartSpeedRate()}";
                //}
            }
        }

        private void Download_ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (miReduceJpegSizeTo.Tag == null)
            {
                miReduceJpegSizeTo.Tag = Info.CustomReduceQuality is App.MenuItemSliderData ? Info.CustomReduceQuality : Application.Current.GetDefaultReduceData();
            }
            if (Info.FileName.IsDownloaded() && miReduceJpegSizeTo.Tag is App.MenuItemSliderData)
            {
                var data = miReduceJpegSizeTo.Tag as App.MenuItemSliderData;
                var q = Info.FileName.GetImageQualityInfo();
                if (q > 0 && q != data.Value)
                {
                    data.Value = q;
                    miReduceJpegSizeTo.Tag = null;
                    miReduceJpegSizeTo.Tag = data;
                }
            }
            Info.CustomReduceQuality = miReduceJpegSizeTo.Tag as App.MenuItemSliderData;
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
            (sender as UIElement).IsEnabled = false;

            setting = Application.Current.LoadSetting();

            var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
            var shift = Keyboard.Modifiers == ModifierKeys.Shift;
            var multiple = Application.Current.DownloadManagerHasMultiSelected();
            var highlight_word = Info.State == DownloadItemState.Finished ? Info.IllustID.ToString() : null;

            if ((sender == miCopyIllustID || sender == PART_CopyIllustID) && !string.IsNullOrEmpty(Url))
            {
                if (shift)
                    Commands.CopyText.Execute(Info.FileName);
                else
                    Commands.CopyArtworkIDs.Execute(Url);
            }
            else if (sender == miCopyDonwnloadInfo)
            {
                if (shift)
                    Commands.CopyText.Execute(Info.FileName);
                else
                    Commands.CopyDownloadInfo.Execute(Info);
            }
            else if (sender == miRefreshThumb || sender == PART_ThumbnailWait)
            {
                //if (Info is DownloadInfo) Info.RefreshThumbnail();
                Action<DownloadInfo> action = (info) =>
                {
                    if (info is DownloadInfo) info.RefreshThumbnail(force: ctrl);
                };
                if (sender == PART_ThumbnailWait || !multiple) action.Invoke(Info);
                else Commands.RunDownloadItemAction.Execute(action);
            }
            else if ((sender == miOpenIllust || sender == PART_OpenIllust) && !string.IsNullOrEmpty(Url))
            {
                //var illust = Url.GetIllustId().FindIllust();
                //if (illust is Pixeez.Objects.Work)
                //    Commands.OpenWork.Execute(illust);
                //else
                //    Commands.OpenWork.Execute(Url);
                Action<DownloadInfo> action = (info) =>
                {
                    if (info is DownloadInfo)
                    {
                        var illust = info.Url.GetIllustId().FindIllust();
                        if (illust is Pixeez.Objects.Work)
                            Commands.OpenWork.Execute(illust);
                        else
                            Commands.OpenWork.Execute(info.Url);
                    }
                };
                if (sender == PART_OpenIllust || !multiple) action.Invoke(Info);
                else Commands.RunDownloadItemAction.Execute(action);
            }
            else if (sender == miDownload || sender == PART_Download)
            {
                var continuation = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? !setting.DownloadWithFailResume : setting.DownloadWithFailResume;
                var restart = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                restart |= await OverwritePrompt(); // Check if file exists and ask for overwrite
                Start(continuation, restart);
            }
            else if (sender == miDownloadRestart)
            {
                var continuation = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? !setting.DownloadWithFailResume : setting.DownloadWithFailResume;
                if (await OverwritePrompt()) Start(continuation, true); // Check if file exists and ask for overwrite
            }
            else if (sender == miSaveAsJPEG)
            {
                if (Info is DownloadInfo && !string.IsNullOrEmpty(Info.FileName))
                {
                    if (new[] { DownloadItemState.Failed, DownloadItemState.Idle, DownloadItemState.Downloading }.Contains(Info.State))
                    {
                        SaveAsJPEG = !SaveAsJPEG;
                        Info.SaveAsJPEG = SaveAsJPEG;
                        PART_SaveAsJPEG.IsOn = SaveAsJPEG;
                        miSaveAsJPEG.IsChecked = SaveAsJPEG;
                        Info.SetSaveAsJPEG(SaveAsJPEG);
                    }
                }
            }
            else if (sender == miRemove || sender == PART_Remove)
            {
                if (State != DownloadItemState.Downloading) State = DownloadItemState.Remove;
            }
            else if (sender == miStopDownload || sender == PART_StopDownload)
            {
                if (State == DownloadItemState.Downloading) await Cancel();
            }
            else if (sender == miOpenImage || sender == PART_OpenFile)
            {
                //FileName.OpenFileWithShell();
                Action<DownloadInfo> action = (info) =>
                {
                    if (info is DownloadInfo && !string.IsNullOrEmpty(info.FileName)) info.FileName.OpenFileWithShell();
                };
                if (sender == PART_OpenFile || !multiple) action.Invoke(Info);
                else Commands.RunDownloadItemAction.Execute(action);
            }
            else if (sender == miOpenFolder || sender == PART_OpenFolder)
            {
                FileName.OpenFileWithShell(ShowFolder: true);
                //Action<DownloadInfo> action = (info) =>
                //{
                //    if (info is DownloadInfo&& !string.IsNullOrEmpty(info.FileName)) info.FileName.OpenFileWithShell(ShowFolder: true);
                //};
                //if (sender == PART_OpenFolder || !multiple) action.Invoke(Info);
                //else Commands.RunDownloadItemAction.Execute(action);
            }
            else if (sender == miOpenImageProperties)
            {
                //FileName.OpenShellProperties();
                Action<DownloadInfo> action = (info) =>
                {
                    if (info is DownloadInfo&& !string.IsNullOrEmpty(info.FileName)) info.FileName.OpenShellProperties();
                };
                if (!multiple) action.Invoke(Info);
                else Commands.RunDownloadItemAction.Execute(action);
            }
            else if (sender == miSearchArtistInFiles)
            {
                if (Info is DownloadInfo && Info.Illust.IsWork())
                {
                    Commands.SearchInStorage.Execute(new SearchObject($"=uid:{Info.UserID}", scope: StorageSearchScope.Author, highlight: highlight_word));
                }
            }
            else if (sender == miSearchTagsInFiles)
            {
                if (Info is DownloadInfo && Info.Illust.IsWork())
                {
                    var mode = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? StorageSearchMode.And : StorageSearchMode.Or;
                    Commands.SearchInStorage.Execute(new SearchObject(string.Join(Environment.NewLine, Info.Illust.Tags), scope: StorageSearchScope.Tag, mode: mode, highlight: highlight_word));
                }
            }
            else if (sender == miSearchTitleInFiles)
            {
                if (Info is DownloadInfo && Info.Illust.IsWork())
                {
                    Commands.SearchInStorage.Execute(new SearchObject(Info.Illust.Title.KatakanaHalfToFull(), scope: StorageSearchScope.Title, highlight: highlight_word));
                }
            }
            else if (sender == miCompareDownloaded)
            {
                Commands.Compare.Execute(Application.Current.GetDownloadManager());
            }
            else if (sender == miShowImageMeta)
            {
                //Commands.ShowMeta.Execute(FileName);
                Action<DownloadInfo> action = (info) =>
                {
                    if (info is DownloadInfo&& !string.IsNullOrEmpty(info.FileName)) Commands.ShowMeta.Execute(info.FileName);
                };
                if (!multiple) action.Invoke(Info);
                else Commands.RunDownloadItemAction.Execute(action);
            }
            else if (sender == miTouchImageMeta)
            {
                //Commands.TouchMeta.Execute(FileName);
                Action<DownloadInfo> action = (info) =>
                {
                    if (info is DownloadInfo&& !string.IsNullOrEmpty(info.FileName)) Commands.TouchMeta.Execute(info.FileName);
                };
                if (!multiple) action.Invoke(Info);
                else Commands.RunDownloadItemAction.Execute(action);
            }
            else if (sender == miConvertImageToJpeg)
            {
                //Commands.ConvertToJpeg.Execute(FileName);
                //SaveAsJPEG = true;
                //PART_SaveAsJPEG.IsOn = SaveAsJPEG;
                Action<DownloadInfo> action = (info) =>
                {
                    if (info is DownloadInfo&& !string.IsNullOrEmpty(info.FileName))
                    {
                        Commands.ConvertToJpeg.Execute(info.FileName);
                        info.SaveAsJPEG = true;
                        info.SetSaveAsJPEG(info.SaveAsJPEG);
                    }
                };
                if (!multiple) action.Invoke(Info);
                else Commands.RunDownloadItemAction.Execute(action);
            }
            else if (sender == miReduceJpegSize)
            {
                Action<DownloadInfo> action = (info) =>
                {
                    if (info is DownloadInfo&& !string.IsNullOrEmpty(info.FileName))
                    {
                        Commands.ReduceJpeg.Execute(info.FileName);
                        info.SaveAsJPEG = true;
                        info.SetSaveAsJPEG(info.SaveAsJPEG);

                        PART_SaveAsJPEG.IsOn = info.SaveAsJPEG;
                        PART_SaveAsJPEG.IsEnabled = info.State != DownloadItemState.Finished;

                        miSaveAsJPEG.IsChecked = info.SaveAsJPEG;
                        miSaveAsJPEG.IsEnabled = info.State != DownloadItemState.Finished;
                    }
                };
                if (!multiple) action.Invoke(Info);
                else Commands.RunDownloadItemAction.Execute(action);
            }
            else if (sender == miReduceJpegSizeTo)
            {
                Action<DownloadInfo> action = (info) =>
                {
                    if (info is DownloadInfo&& !string.IsNullOrEmpty(info.FileName))
                    {
                        var cq = miReduceJpegSizeTo.Tag is App.MenuItemSliderData ? (int)(miReduceJpegSizeTo.Tag as App.MenuItemSliderData).Value : setting.DownloadRecudeJpegQuality;
                        Commands.ReduceJpeg.Execute(new Tuple<string, int>(info.FileName, cq));
                        info.SaveAsJPEG = true;
                        info.SetSaveAsJPEG(info.SaveAsJPEG);

                        PART_SaveAsJPEG.IsOn = info.SaveAsJPEG;
                        PART_SaveAsJPEG.IsEnabled = info.State != DownloadItemState.Finished;

                        miSaveAsJPEG.IsChecked = info.SaveAsJPEG;
                        miSaveAsJPEG.IsEnabled = info.State != DownloadItemState.Finished;
                    }
                };
                if (!multiple) action.Invoke(Info);
                else Commands.RunDownloadItemAction.Execute(action);
            }
            else if (sender == PART_SaveAsJPEG)
            {
                Action<DownloadInfo> action = (info) =>
                {
                    if (info is DownloadInfo)
                    {
                        if (info.State == DownloadItemState.Finished)
                            info.SetSaveAsJPEG(info.SaveAsJPEG);
                        else
                            info.SaveAsJPEG = info.GetSaveAsJPEG();

                        PART_SaveAsJPEG.IsOn = info.SaveAsJPEG;
                        PART_SaveAsJPEG.IsEnabled = info.State != DownloadItemState.Finished;

                        miSaveAsJPEG.IsChecked = info.SaveAsJPEG;
                        miSaveAsJPEG.IsEnabled = info.State != DownloadItemState.Finished;
                    }
                };
                if (!IsEnabled || !multiple) action.Invoke(Info);
                else Commands.RunDownloadItemAction.Execute(action);
            }

            (sender as UIElement).IsEnabled = true;
        }
    }
}
