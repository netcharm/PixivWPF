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

    public class ProgressInfo
    {
        ulong Received { get; set; } = 0;
        ulong Length { get; set; } = 0;
        ulong CurrentRate { get; set; } = 0;
        ulong AverageRate { get; set; } = 0;
        ulong MaxRate { get; set; } = 0;
        ulong MinRate { get; set; } = 0;
    }

    public class DownloadInfo : INotifyPropertyChanged
    {
        private Setting setting = Application.Current.LoadSetting();

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
                NotifyPropertyChanged("UrlChanged");
            }
        }

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
            set { is_fav = value;  NotifyPropertyChanged("IsFav"); }
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
                Start();
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
                catch (Exception) { }
            }
        }

        public DownloadItem Instance { get; set; } = null;

        public string FailReason { get; set; } = string.Empty;

        public DateTime StartTime { get; internal set; } = DateTime.Now;
        public DateTime EndTime { get; internal set; } = DateTime.Now;
        public DateTime LastModified { get; internal set; }

        public double DownRateCurrent { get; set; } = 0;
        public double DownRateAverage { get; set; } = 0;

        public DownloadInfo()
        {
            setting = Application.Current.LoadSetting();
        }

        public void Dispose()
        {
            if (Instance is DownloadItem)
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate
                {
                    try
                    {
                        Instance.PART_Preview.Dispose();
                        Instance.CleanBuffer();
                        Instance = null;
                    }
                    catch (Exception) { }
                }));
            }
            Thumbnail = null;
        }

        public void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            if (ProgressPercent == 100)
            {
                if (FileName.IsDownloaded())
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
            catch (Exception) { }
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

                    var img = await ThumbnailUrl.LoadImageFromUrl(overwrite, size:Application.Current.GetDefaultThumbSize());
                    if (img.Source != null)
                    {
                        Thumbnail = img.Source;
                        if (Instance is DownloadItem) Instance.PART_ThumbnailWait.Hide();
                    }
                    else if (Instance is DownloadItem) Instance.PART_ThumbnailWait.Fail();
                    img.Source = null;
                }
                catch (Exception) { if (Instance is DownloadItem) Instance.PART_ThumbnailWait.Fail(); }
                finally
                {
                    if (Thumbnail == null && Instance is DownloadItem) Instance.PART_ThumbnailWait.Fail();
                    NotifyPropertyChanged("Thumbnail");
                }
            }).InvokeAsync();
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
    public partial class DownloadItem : UserControl, INotifyPropertyChanged
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
                if ((State == DownloadState.Downloading && EndTick.DeltaSeconds(lastTick) >= 1) ||
                     State == DownloadState.Writing ||
                     State == DownloadState.Finished ||
                     State == DownloadState.NonExists)
                {
                    var received = i.Item1 >= 0 ? i.Item1 : 0;
                    var total = i.Item2 >= 0 ? i.Item2 : 0;
                    try
                    {
                        #region Update ProgressBar & Progress Info Text
                        var deltaA = (EndTick - StartTick).TotalSeconds;
                        var deltaC = (EndTick - lastTick).TotalSeconds;
                        var rateA = deltaA > 0 ? received / deltaA / 1024.0 : 0;
                        var rateC = deltaC > 0 ? lastReceived / deltaC / 1024.0 : lastRate;
                        lastRates.Enqueue(rateC);
                        if (lastRates.Count > lastRatesCount) lastRates.Dequeue();
                        if (rateC > 0 || deltaC >= 1) lastRate = lastRates.Average();

                        var percent = total > 0 ? received / total : 0;
                        PART_DownloadProgress.Value = percent * 100;
                        PART_DownloadProgressPercent.Text = $"{State.ToString()}: {PART_DownloadProgress.Value:0.0}%";
                        PART_DownInfo.Text = $"Status : {Info.Received / 1024.0:0.} KB / {Info.Length / 1024.0:0.} KB, {lastRate:0.00} / {rateA:0.00} KB/s";
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
                    }
                    catch (Exception) { }
                    lastTick = EndTick;
                }
            });
        }

        private void UpdateProgress()
        {
            if (progress is IProgress<Tuple<double, double>>) progress.Report(Progress);
        }
        #endregion

        #region Update state helper
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
                        //this.Background = Application.Current.GetSucceedBrush();
                        PART_DownloadStatusMark.Foreground = Application.Current.GetSucceedBrush();
                        PART_DownloadStatusMark.Text = "\uE930";
                    }
                    else if (State == DownloadState.NonExists)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //this.Background = Application.Current.GetNonExistsBrush();
                        PART_DownloadStatusMark.Foreground = Application.Current.GetNonExistsBrush();
                        PART_DownloadStatusMark.Text = "\uE946";
                    }
                    else if (State == DownloadState.Downloading)
                    {
                        miRemove.IsEnabled = false;
                        miStopDownload.IsEnabled = true;
                        //this.Background = Application.Current.GetBackgroundBrush();
                        PART_DownloadStatusMark.Foreground = Application.Current.GetBackgroundBrush();
                        PART_DownloadStatusMark.Text = "\uEB41";
                    }
                    else if (State == DownloadState.Writing)
                    {
                        miRemove.IsEnabled = false;
                        miStopDownload.IsEnabled = false;
                        //this.Background = Application.Current.GetBackgroundBrush();
                        PART_DownloadStatusMark.Foreground = Application.Current.GetBackgroundBrush();
                        PART_DownloadStatusMark.Text = "\uE78C";
                    }
                    else if (State == DownloadState.Idle)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //this.Background = Application.Current.GetBackgroundBrush();
                        PART_DownloadStatusMark.Foreground = Application.Current.GetBackgroundBrush();
                        PART_DownloadStatusMark.Text = "\uEA3A";
                    }
                    else if (State == DownloadState.Failed)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //this.Background = Application.Current.GetFailedBrush();
                        PART_DownloadStatusMark.Foreground = Application.Current.GetFailedBrush();
                        PART_DownloadStatusMark.Text = "\uEA39";
                    }
                    else if(State == DownloadState.Remove)
                    {
                        PART_Preview.Dispose();
                    }
                    else
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //this.Background = Application.Current.GetBackgroundBrush();
                        PART_DownloadStatusMark.Foreground = Application.Current.GetBackgroundBrush();
                        PART_DownloadStatusMark.Text = "";
                    }
                    miOpenImage.IsEnabled = FileName.IsDownloaded();
                    miOpenFolder.IsEnabled = true;

                    PART_DownloadProgressPercent.Text = $"{State.ToString()}: {PART_DownloadProgress.Value:0.0}%";

                    PART_CopyIllustID.IsEnabled = miCopyIllustID.IsEnabled;
                    PART_StopDownload.IsEnabled = miStopDownload.IsEnabled;
                    PART_Remove.IsEnabled = miRemove.IsEnabled;
                    PART_Download.IsEnabled = miDownload.IsEnabled;

                    PART_OpenIllust.IsEnabled = miOpenIllust.IsEnabled;
                    PART_OpenFile.IsEnabled = miOpenImage.IsEnabled;
                    PART_OpenFolder.IsEnabled = miOpenFolder.IsEnabled;
                }
                catch (Exception) { }
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
            catch (Exception) { }
        }

        private HttpClient httpClient = null;
        private async Task<HttpResponseMessage> GetAsyncResponse(string Url, bool continuation = false)
        {
            var start = _DownloadBuffer is byte[] ? _DownloadBuffer.Length : 0;
            httpClient = Application.Current.GetHttpClient(continuation, start);
            return (await httpClient.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead));
        }

        private async Task<string> DownloadStreamAsync(HttpResponseMessage response, bool continuation = true)
        {
            string result = string.Empty;
            using (var ms = new MemoryStream())
            {
                try
                {
                    var lastUpdateBuffer = DateTime.Now;

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
                            lastReceived = ms.Length;
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
                                    UpdateProgress();
                                }
                            } while (bytesread > 0 && Received < Length);

                            if (Received == Length && State == DownloadState.Downloading)
                            {
                                _DownloadBuffer = ms.ToArray();
                                result = await SaveFile(FileName, _DownloadBuffer);
                            }
                            else DownloadExceptionProcess();
                        }
                    }
                    else
                    {
                        result = await Url.DownloadImage(FileName);
                        result.Touch(Url);
                        Length = result.GetFileLength();
                        if (Length <= 0) throw new Exception($"Download {Path.GetFileName(FileName)} Failed! File not exists.");
                        Received = Length;
                        State = DownloadState.Finished;
                    }
                }
                catch (Exception ex)
                {
                    var stack = string.IsNullOrEmpty(ex.StackTrace) ? string.Empty : $"\n{ex.StackTrace}";
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

            if (_DownloadBuffer is byte[])
            {
                lastReceived = _DownloadBuffer.Length;
                Received = lastReceived;
            }
            else
            {
                StartTick = DateTime.Now;
                lastReceived = 0;
                Length = Received = 0;
            }
            UpdateProgress();
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

            if (State == DownloadState.Finished)
            {
                result = FileName;
                EndTick = DateTime.Now;

                CleanBuffer();

                var state = "Succeed";
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
                    httpClient.Dispose();
                    httpClient = null;
                }
            }
            catch (Exception) { }

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
            catch (Exception) { }
            finally
            {
                if (Downloading is SemaphoreSlim && Downloading.CurrentCount <= 0) Downloading.Release();
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
                    File.WriteAllBytes(FileName, bytes);
                    File.SetCreationTime(FileName, FileTime);
                    File.SetLastWriteTime(FileName, FileTime);
                    File.SetLastAccessTime(FileName, FileTime);
                    PART_OpenFile.IsEnabled = true;
                    PART_OpenFolder.IsEnabled = true;
                    FailReason = $"downloaded from remote site.";
                    State = DownloadState.Finished;
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
                        File.Copy(source, FileName, true);
                        File.SetCreationTime(FileName, FileTime);
                        File.SetLastWriteTime(FileName, FileTime);
                        File.SetLastAccessTime(FileName, FileTime);
                        PART_OpenFile.IsEnabled = true;
                        PART_OpenFolder.IsEnabled = true;
                        FailReason = $"copied from cached image.";
                        State = DownloadState.Finished;
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
            setting = Application.Current.LoadSetting();
            HTTP_STREAM_READ_COUNT = setting.DownloadHttpStreamBlockSize;
            HTTP_TIMEOUT = setting.DownloadHttpTimeout > 5 ? setting.DownloadHttpTimeout : 5;

            IsStart = false;
            AutoStart = false;

            var basename = Path.GetFileName(FileName);
            var msg_title = $"Warnning ({basename})";
            var msg_content = "Overwrite exists?";
            if (msg_title.IsMessagePopup(msg_content)) return;

            bool delta = true;
            if (File.Exists(FileName))
            {
                delta = new FileInfo(FileName).CreationTime.DeltaNowMillisecond() > setting.DownloadTimeSpan ? true : false;
                if (!delta) return;
                if (!(await msg_content.ShowMessageDialog(msg_title, MessageBoxImage.Warning))) return;
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
                        Application.Current.DoEvents();

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
                    Application.Current.DoEvents();
                }
                if (cancelReadStreamSource is CancellationTokenSource && !cancelReadStreamSource.IsCancellationRequested)
                {
                    cancelReadStreamSource.Cancel();
                    await Task.Delay(250);
                    Application.Current.DoEvents();
                }
                try
                {
                    if (httpClient is HttpClient)
                    {
                        httpClient.CancelPendingRequests();
                        httpClient.Dispose();
                        httpClient = null;
                    }
                }
                catch (Exception) { Canceling = false; }
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

            InitProgress();

            CheckProperties();
        }

        public DownloadItem(string url, bool autostart = true)
        {
            InitializeComponent();
            setting = Application.Current.LoadSetting();

            Info = new DownloadInfo() { Instance = this };

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

            InitProgress();

            CheckProperties();
        }

        ~DownloadItem()
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(delegate
            {
                try
                {
                    if (PART_Preview.Source != null) PART_Preview.Source = null;
                    PART_Preview.UpdateLayout();
                }
                catch (Exception) { }
            }));           
            PART_Preview = null;
        }

        private void Download_Loaded(object sender, RoutedEventArgs e)
        {
            CheckProperties();
            setting = Application.Current.LoadSetting();
            //PART_DownloadProgress.IsEnabled = true;
            if (AutoStart)
            {
                if (State == DownloadState.Finished || State == DownloadState.Downloading) return;
                else if (State == DownloadState.Idle) //|| State == DownloadState.Failed)
                {
                    if (string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(PART_FileURL.Text)) Url = PART_FileURL.Text;
                    if (IsStart && !IsDownloading) Start(setting.DownloadWithFailResume);
                }
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
                if(Info is DownloadInfo ) Info.RefreshThumbnail();
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
        }
    }
}
