using System;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

using Microsoft.Win32;
using System.Threading;
using System.Collections.Generic;

namespace PixivWPF.Common
{
    public enum DownloadState { Idle, Downloading, Paused, Finished, Failed, Writing, Deleted, NonExists, Remove, Unknown }

    public class DownloadInfo: INotifyPropertyChanged
    {
        private Setting setting = Setting.Instance == null ? Setting.Load() : Setting.Instance;

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

                FileName = url.GetImageName(singlefile);

                if (string.IsNullOrEmpty(setting.LastFolder))
                {
                    SaveFileDialog dlgSave = new SaveFileDialog();
                    dlgSave.FileName = FileName;
                    if (dlgSave.ShowDialog() == true)
                    {
                        FileName = dlgSave.FileName;
                        setting.LastFolder = Path.GetDirectoryName(FileName);
                        setting.LastFolder.AddDownloadedWatcher();
                    }
                    else
                    {
                        Canceled = true;
                        //else FileName = string.Empty;
                        return;
                    }
                }
                FileName = Path.Combine(setting.LastFolder, Path.GetFileName(FileName));
                NotifyPropertyChanged("UrlChanged");
            }
        }

        public string FileName { get; set; } = string.Empty;
        public string FolderName { get { return string.IsNullOrEmpty(FileName) ? string.Empty : Path.GetDirectoryName(FileName); } }
        public DateTime FileTime { get; set; } = DateTime.Now;
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
        [DefaultValue(0)]
        public long Received { get; set; } = 0;
        [DefaultValue(0)]
        public long Length { get; set; } = 0;
        [DefaultValue(true)]
        public bool Overwrite { get; set; } = true;

        private bool singlefile = true;
        [DefaultValue(true)]
        public bool SingleFile
        {
            get { return singlefile; }
            set {
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
        public string ThumbnailUrl { get; set; } = string.Empty;

        private bool forcestart = false;
        [DefaultValue(false)]
        public bool IsForceStart
        {
            get { return (forcestart); }
            set
            {
                if (value)
                {
                    State = DownloadState.Idle;
                    IsStart = value;
                }
                forcestart = value;
                NotifyPropertyChanged("IsForceStart");
            }
        }

        private bool start = false;
        [DefaultValue(false)]
        public bool IsStart
        {
            get
            {
                return start;
            }
            set
            {
                start = value;
                NotifyPropertyChanged("IsStart");
            }
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

        public void RefreshState()
        {
            NotifyPropertyChanged("StateChanged");
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
        private const int HTTP_STREAM_READ_COUNT = 65536;
        private Setting setting = Setting.Instance == null ? Setting.Load() : Setting.Instance;

        private Tuple<double, double> finishedProgress;

        private CancellationTokenSource cancelSource = null;
        private CancellationToken cancelToken;

        private SemaphoreSlim Downloading = new SemaphoreSlim(1, 1);

        private DownloadInfo Info { get; set; }

        public bool Canceled
        {
            get { return Info.Canceled; }
            set { Info.Canceled = value; }
        }

        public string Url
        {
            get { return Info.Url; }
            set { Info.Url = value; }
        }

        public ImageSource Thumbnail
        {
            get { return Info.Thumbnail; }
            set { Info.Thumbnail = value; }
        }
        public string ThumbnailUrl
        {
            get { return Info.ThumbnailUrl; }
            set { Info.ThumbnailUrl = value; }
        }

        public string FileName
        {
            get { return Info.FileName; }
            set
            {
                Info.FileName = value;

                PART_FileName.Text = Info.FileName;
                PART_FileFolder.Text = FolderName;
            }
        }

        public string FolderName
        {
            get { return string.IsNullOrEmpty(Info.FileName) ? string.Empty : Path.GetDirectoryName(Info.FileName); }
        }

        [DefaultValue(true)]
        public bool AutoStart
        {            
            get { return Info.AutoStart; }
            set { Info.AutoStart = value; }
        }

        [DefaultValue(DownloadState.Idle)]
        public DownloadState State
        {
            get { return Info.State; }
            set
            {
                Info.State = value;
                NotifyPropertyChanged("StateChanged");
            }
        }

        public Tuple<double, double> Progress
        {
            get { return Info.Progress; }
        }

        public long Received
        {
            get { return Info.Received >= 0 ? Info.Received : 0; }
            set { Info.Received = value; }
        }

        public long Length
        {
            get { return Info.Length; }
            set { Info.Length = value; }
        }

        [DefaultValue(true)]
        public bool Overwrite
        {
            get { return Info.Overwrite; }
            set { Info.Overwrite = value; }
        }

        [DefaultValue(true)]
        public bool SingleFile
        {
            get { return Info.SingleFile; }
            set { Info.SingleFile = value; }
        }
        public DateTime FileTime
        {
            get { return Info.FileTime; }
            set { Info.FileTime = value; }
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
        public bool IsForceStart
        {
            get { return Info.IsForceStart; }
            set
            {
                Info.IsForceStart = value;
                //Start();
            }
        }

        [DefaultValue(false)]
        public bool IsStart
        {
            get
            {
                return Info.IsStart;
            }
            set
            {
                Info.IsStart = value;
            }
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

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private DateTime startTick = DateTime.Now;
        private DateTime endTick = DateTime.Now;
        private DateTime lastTick = DateTime.Now;
        private double lastRate = 0;
        private double lastRateA = 0;
        private long lastReceived = 0;
        internal IProgress<Tuple<double, double>> progress = null;

        internal void UpdateState()
        {
            CheckProperties();
        }

        private void InitProgress()
        {
            progress = new Progress<Tuple<double, double>>(i => {
                if ((State == DownloadState.Downloading && endTick.Ticks - lastTick.Ticks >= 10000000.0) ||
                     State == DownloadState.Writing ||
                     State == DownloadState.Finished ||
                     State == DownloadState.NonExists)
                {
                    var received = i.Item1 >= 0 ? i.Item1 : 0;
                    var total = i.Item2 >= 0 ? i.Item2 : 0;
                    try
                    {
                        #region Update ProgressBar & Progress Info Text
                        var deltaA = (endTick.Ticks - startTick.Ticks) / 10000000.0;
                        var deltaC = (endTick.Ticks - lastTick.Ticks) / 10000000.0;
                        var rateA = deltaA > 0 ? received / deltaA / 1024.0 : 0;
                        var rateC = deltaC > 0 ? lastReceived / deltaC / 1024.0 : lastRate;
                        if (rateC > 0 || deltaC >= 5) lastRate = rateC;
                        var percent = total > 0 ? received / total : 0;
                        PART_DownloadProgress.Value = percent * 100;
                        PART_DownloadProgressPercent.Text = $"{State.ToString()}: {PART_DownloadProgress.Value:0.0}%";
                        PART_DownInfo.Text = $"Status : {received / 1024.0:0.} KB / {total / 1024.0:0.} KB, {lastRate:0.00} / {rateA:0.00} KB/s";
                        #endregion

                        #region Update Progress Info Text Color Gradient
                        var factor = PART_DownloadProgress.ActualWidth / PART_DownloadProgressPercent.ActualWidth;
                        var offset = Math.Abs((factor - 1) / 2);
                        PART_ProgressInfoLinear.StartPoint = new Point(0 - offset, 0);
                        PART_ProgressInfoLinear.EndPoint = new Point(1 + offset, 0);
                        PART_ProgressInfoLeft.Offset = percent;
                        PART_ProgressInfoRight.Offset = percent;
                        #endregion

                        lastRateA = rateA;
                        lastReceived = 0;
                    }
                    catch (Exception) { }
                    lastTick = endTick;
                }
            });
        }

        private void CheckProperties()
        {
            if (Tag is DownloadInfo)
            {
                try
                {
                    Info = Tag as DownloadInfo;

                    progress.Report(Progress);

                    if (IsForceStart)
                    {
                        State = DownloadState.Idle;
                        IsForceStart = false;
                    }

                    if (State == DownloadState.Finished || State == DownloadState.NonExists)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                    }
                    else if (State == DownloadState.Downloading)
                    {
                        miRemove.IsEnabled = false;
                        miStopDownload.IsEnabled = true;
                    }
                    else if (State == DownloadState.Idle)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                    }
                    else if (State == DownloadState.Failed)
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
                        //PART_DownloadProgress.IsIndeterminate = true;
                        //PART_DownloadProgressPercent.Foreground = Theme.AccentBrush;
                    }
                    else
                    {
                        miRemove.IsEnabled = true;
                        miStopDownload.IsEnabled = false;
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

        public void Refresh()
        {
            CheckProperties();
        }

        private string SaveFile(string FileName, byte[] bytes)
        {
            var result = FileName;
            try
            {
                State = DownloadState.Writing;
                progress.Report(Progress);
                File.WriteAllBytes(FileName, bytes);
                File.SetCreationTime(FileName, FileTime);
                File.SetLastWriteTime(FileName, FileTime);
                File.SetLastAccessTime(FileName, FileTime);
                PART_OpenFile.IsEnabled = true;
                PART_OpenFolder.IsEnabled = true;
                $"{Path.GetFileName(FileName)} is saved!".ShowDownloadToast("Succeed", ThumbnailUrl, FileName);
                State = DownloadState.Finished;
                progress.Report(finishedProgress);
                this.Sound();
            }
            catch (Exception)
            {
                State = DownloadState.Failed;
                if (!Canceled) throw new Exception($"Download {Path.GetFileName(FileName)} Failed!");
            }
            finally
            {
                IsStart = false;
                IsForceStart = false;

                if (State == DownloadState.Finished)
                {
                    progress.Report(finishedProgress);
                    result = FileName;
                }
                PART_DownloadProgress.IsEnabled = false;
                if (cancelSource is CancellationTokenSource) cancelSource.Dispose();
                cancelSource = null;
            }
            return (result);
        }

        private string SaveFile(string FileName, string source)
        {
            var result = FileName;
            try
            {
                if (File.Exists(source))
                {
                    State = DownloadState.Writing;
                    var fi = new FileInfo(source);
                    Length = Received = fi.Length;
                    progress.Report(Progress);
                    finishedProgress = new Tuple<double, double>(Received, Length);
                    File.Copy(source, FileName, true);
                    File.SetCreationTime(FileName, FileTime);
                    File.SetLastWriteTime(FileName, FileTime);
                    File.SetLastAccessTime(FileName, FileTime);
                    PART_OpenFile.IsEnabled = true;
                    PART_OpenFolder.IsEnabled = true;
                    $"{Path.GetFileName(FileName)} is saved!".ShowDownloadToast("Succeed", ThumbnailUrl, FileName);
                    State = DownloadState.Finished;
                    progress.Report(finishedProgress);
                    this.Sound();
                }
            }
            catch (Exception)
            {
                State = DownloadState.Failed;
                if (!Canceled) throw new Exception($"Download {Path.GetFileName(FileName)} Failed!");
            }
            finally
            {
                IsStart = false;
                IsForceStart = false;

                if (State == DownloadState.Finished)
                {
                    progress.Report(finishedProgress);
                    result = FileName;
                }
                PART_DownloadProgress.IsEnabled = false;
                if (cancelSource is CancellationTokenSource) cancelSource.Dispose();
                cancelSource = null;
            }
            return (result);
        }

        private async Task<string> DownloadAsync()
        {
            string result = string.Empty;
            if (string.IsNullOrEmpty(Url))
            {
                Downloading.Release();
                return (result);
            }
            if (cancelSource != null || State == DownloadState.Downloading)
            {
                return (result);
            }

            IsForceStart = false;
            IsStart = false;

            PART_OpenFile.IsEnabled = false;
            PART_OpenFolder.IsEnabled = false;

            State = DownloadState.Downloading;
            //PART_DownloadProgress.IsIndeterminate = false;
            PART_DownloadProgress.IsEnabled = true;
            Pixeez.Tokens tokens = await CommonHelper.ShowLogin();
            using (var response = await tokens.SendRequestAsync(Pixeez.MethodType.GET, Url))
            {
                try
                {
                    if (response != null && response.Source.IsSuccessStatusCode)// response.Source.StatusCode == HttpStatusCode.OK)
                    {
                        //cancelSource = new CancellationTokenSource(TimeSpan.FromMinutes(30));
                        cancelSource = new CancellationTokenSource();
                        cancelToken = cancelSource.Token;

                        endTick = DateTime.Now;
                        lastReceived = 0;
                        Received = 0;
                        Length = (long)response.Source.Content.Headers.ContentLength;
                        progress.Report(Progress);
                        finishedProgress = new Tuple<double, double>(Length, Length);

                        using (var cs = await response.Source.Content.ReadAsStreamAsync())
                        {
                            using (var ms = new MemoryStream())
                            {
                                byte[] bytes = new byte[HTTP_STREAM_READ_COUNT];
                                int bytesread = 0;
                                do
                                {
                                    if (Canceled || cancelSource.IsCancellationRequested || State == DownloadState.Failed)
                                    {
                                        State = DownloadState.Failed;
                                        break;
                                    }
                                    else if (State == DownloadState.Finished)
                                    {
                                        progress.Report(finishedProgress);
                                        break;
                                    }
#if DEBUG
                                    bytesread = await cs.ReadAsync(bytes, 0, HTTP_STREAM_READ_COUNT, cancelToken);
#else
                                            bytesread = await cs.ReadAsync(bytes, 0, HTTP_STREAM_READ_COUNT, cancelToken);
#endif
                                    endTick = DateTime.Now;

                                    if (Canceled || cancelSource.IsCancellationRequested || State == DownloadState.Failed)
                                    {
                                        State = DownloadState.Failed;
                                        break;
                                    }
                                    else if (State == DownloadState.Finished)
                                    {
                                        progress.Report(finishedProgress);
                                        break;
                                    }

                                    if (bytesread > 0 && bytesread <= HTTP_STREAM_READ_COUNT && Received < Length)
                                    {
                                        lastReceived += bytesread;
                                        Received += bytesread;
                                        await ms.WriteAsync(bytes, 0, bytesread);
                                        progress.Report(Progress);
                                    }
                                } while (bytesread > 0 && Received < Length);
                                //if (ms.Length == Received && Received == Length)
                                if (Received == Length && State == DownloadState.Downloading)
                                {
                                    SaveFile(FileName, ms.ToArray());
                                }
                                else
                                {
                                    State = DownloadState.Failed;
                                    if (!Canceled) throw new Exception($"Download {Path.GetFileName(FileName)} Failed!");
                                }
                            }
                        }
                    }
                    else result = null;
                }
                catch (Exception ex)
                {
                    var ret = ex.Message;
                    if (State == DownloadState.Downloading)
                    {
                        State = DownloadState.Failed;
                    }
                }
                finally
                {
                    if (State == DownloadState.Finished)
                    {
                        progress.Report(finishedProgress);
                        result = FileName;
                    }
                    PART_DownloadProgress.IsEnabled = false;
                    if (cancelSource is CancellationTokenSource) cancelSource.Dispose();
                    cancelSource = null;
                    Canceled = false;
                    if (Downloading is SemaphoreSlim) Downloading.Release();
                }
            }
            return (result);
        }

        private async void Start()
        {
            if (State == DownloadState.Downloading)
            {
                Cancel();
                //State = DownloadState.Idle;
            }
            CheckProperties();

            if (await Downloading.WaitAsync(15000))
            {
                if (State != DownloadState.Idle && State != DownloadState.Failed) return;

                string fc = Url.GetImageCachePath();
                if (File.Exists(fc))
                {
                    startTick = DateTime.Now;
                    SaveFile(FileName, fc);
                }
                else
                {
                    //await Dispatcher.BeginInvoke((Action)(async () =>
                    // {
                    //     startTick = DateTime.Now;
                    //     await DownloadAsync();
                    // }));
                    await new Action(async () =>
                    {
                        startTick = DateTime.Now;
                        await DownloadAsync();
                    }).InvokeAsync();
                }
            }
        }

        private void Cancel()
        {
            if (!Canceled)
            {
                Canceled = true;
                if (cancelSource is CancellationTokenSource && !cancelSource.IsCancellationRequested)
                    cancelSource.Cancel();
            }
            //if (State != DownloadState.Downloading || State != DownloadState.Writing) miRemove.IsEnabled = true;
        }

        public DownloadItem()
        {
            InitializeComponent();
            Info = new DownloadInfo();

            InitProgress();

            CheckProperties();
        }

        public DownloadItem(string url, bool autostart=true)
        {
            InitializeComponent();

            Info = new DownloadInfo();

            InitProgress();

            AutoStart = autostart;
            Url = url;

            CheckProperties();
        }

        public DownloadItem(DownloadInfo info)
        {
            InitializeComponent();

            if (info is DownloadInfo)
                Info = info;
            else
                Info = new DownloadInfo();

            InitProgress();

            CheckProperties();
        }

        public void UpdateDownloadState()
        {
            CheckProperties();
        }

        private void Download_Loaded(object sender, RoutedEventArgs e)
        {
            CheckProperties();
            if (AutoStart)
            {
                if (State == DownloadState.Finished || State == DownloadState.Downloading) return;
                else if (State == DownloadState.Idle) //|| State == DownloadState.Failed)
                {
                    if (string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(PART_FileURL.Text)) Url = PART_FileURL.Text;
                    Start();
                }
            }
        }

        private void PART_Download_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            CheckProperties();
            if (IsStart)
            {
                Start();
            }
        }

        private void miActions_Click(object sender, RoutedEventArgs e)
        {
            if((sender == miCopyIllustID || sender == PART_CopyIllustID) && !string.IsNullOrEmpty(Url))
            {
                CommonHelper.Cmd_CopyIllustIDs.Execute(Url);
            }
            else if(sender == miCopyDonwnloadInfo)
            {
                CommonHelper.Cmd_CopyDownloadInfo.Execute(Info);
            }
            else if((sender == miOpenIllust || sender == PART_OpenIllust) && !string.IsNullOrEmpty(Url))
            {
                var illust = Url.GetIllustId().FindIllust();
                if (illust is Pixeez.Objects.Work)
                    CommonHelper.Cmd_OpenIllust.Execute(illust);
                else
                    CommonHelper.Cmd_OpenIllust.Execute(Url);
            }
            else if (sender == miDownload || sender == PART_Download)
            {
                Start();
            }
            else if (sender == miRemove || sender == PART_Remove)
            {
                if (State != DownloadState.Downloading) State = DownloadState.Remove;
            }
            else if (sender == miStopDownload || sender == PART_StopDownload)
            {
                if (State == DownloadState.Downloading) Cancel();
            }
            else if (sender == miOpenImage || sender == PART_OpenFile)
            {
                FileName.OpenImageWithShell();
            }
            else if (sender == miOpenFolder || sender == PART_OpenFolder)
            {
                FileName.OpenImageWithShell(true);
            }
        }
    }
}
