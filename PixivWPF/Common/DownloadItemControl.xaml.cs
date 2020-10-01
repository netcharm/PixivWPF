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
using System.Net.Http;
using System.Net;
using System.Linq;

namespace PixivWPF.Common
{
    public enum DownloadState { Idle, Downloading, Paused, Finished, Failed, Writing, Deleted, NonExists, Remove, Unknown }

    public class DownloadInfo: INotifyPropertyChanged
    {
        private Setting setting = Application.Current.LoadSetting();

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

        public string FailReason { get; set; } = string.Empty;

        public DateTime StartTime { get; internal set; } = DateTime.Now;
        public DateTime EndTime { get; internal set; } = DateTime.Now;

        public DownloadInfo()
        {
            setting = Application.Current.LoadSetting();
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
        private Setting setting = Application.Current.LoadSetting();

        private Tuple<double, double> finishedProgress;

        private CancellationTokenSource cancelSource = null;
        private CancellationToken cancelToken;

        private SemaphoreSlim Downloading = new SemaphoreSlim(1, 1);

        private DownloadInfo Info { get; set; }

        public bool Canceling
        {
            get { return Info.Canceled; }
            set { Info.Canceled = value; }
        }

        public string FailReason
        {
            get { return Info.FailReason; }
            set { Info.FailReason = value; }
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

        [DefaultValue(false)]
        public bool IsCanceling
        {
            get
            {
                return ((cancelSource is CancellationTokenSource && cancelSource.IsCancellationRequested) || Canceling);
            }
        }

        [DefaultValue(true)]
        public bool CanDownload
        {
            get
            {
                return (State == DownloadState.Idle || State == DownloadState.Failed || State == DownloadState.Finished || State == DownloadState.NonExists);
            }
        }

        [DefaultValue(false)]
        public bool IsDownloading
        {
            get
            {
                return (State == DownloadState.Downloading || State == DownloadState.Writing || (Downloading is SemaphoreSlim && Downloading.CurrentCount == 0));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private DateTime StartTick
        {
            get { return (Info.StartTime); }
            set { Info.StartTime = value; }
        }
        private DateTime EndTick
        {
            get { return (Info.EndTime); }
            set { Info.EndTime = value; }
        }
        private DateTime lastTick = DateTime.Now;
        private int lastRatesCount = 5;
        private Queue<double> lastRates = new Queue<double>();
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
                        PART_DownInfo.Text = $"Status : {received / 1024.0:0.} KB / {total / 1024.0:0.} KB, {lastRate:0.00} / {rateA:0.00} KB/s";
                        #endregion

                        #region Update Progress Info Text Color Gradient
                        var factor = PART_DownloadProgress.ActualWidth / PART_DownloadProgressPercent.ActualWidth;
                        var offset = Math.Abs((factor - 1) / 2);
                        PART_ProgressInfoLinear.StartPoint = new Point(0 - offset, 0);
                        PART_ProgressInfoLinear.EndPoint = new Point(1 + offset, 0);
                        PART_ProgressInfoLinearLeft.Offset = percent;
                        PART_ProgressInfoLinearRight.Offset = percent;
                        #endregion

                        lastRateA = rateA;
                        lastReceived = 0;
                    }
                    catch (Exception) { }
                    lastTick = EndTick;
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

        public void Refresh()
        {
            CheckProperties();
        }

        private async Task Cancel()
        {
            if (!Canceling)
            {
                Canceling = true;
                if (cancelSource is CancellationTokenSource && !cancelSource.IsCancellationRequested)
                {
                    cancelSource.Cancel();
                    await Task.Delay(1000);
                    Application.Current.DoEvents();
                }
            }
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
                FailReason = $"downloaded from remote site.";
                State = DownloadState.Finished;
            }
            catch (Exception ex)
            {
                FailReason = ex.Message;
                State = DownloadState.Failed;
                if (!IsCanceling) throw new Exception($"Download {Path.GetFileName(FileName)} Failed!");
            }
            finally
            {
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
                }
            }
            catch (Exception ex)
            {
                FailReason = ex.Message;
                State = DownloadState.Failed;
                if (!IsCanceling) throw new Exception($"Download {Path.GetFileName(FileName)} Failed!");
            }
            finally
            {
            }
            return (result);
        }

        private async Task<HttpResponseMessage> GetAsyncResponse(string Url)
        {
            HttpClientHandler handler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 15,
                //SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
                Proxy = string.IsNullOrEmpty(setting.Proxy) ? null : new WebProxy(setting.Proxy, true, new string[] { "127.0.0.1", "localhost", "192.168.1" }),
                UseProxy = string.IsNullOrEmpty(setting.Proxy) || !setting.UsingProxy ? false : true
            };

            var httpClient = new HttpClient(handler, true) { Timeout = TimeSpan.FromSeconds(30) };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "PixivAndroidApp/5.0.64 (Android 6.0)");
            httpClient.DefaultRequestHeaders.Add("Referer", "https://app-api.pixiv.net/");
            return (await httpClient.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead));
        }

        private async Task<string> DownloadStreamAsync(HttpResponseMessage response)
        {
            string result = string.Empty;
            try
            {
                response.EnsureSuccessStatusCode();
                string vl = response.Content.Headers.ContentEncoding.FirstOrDefault();
                Length = response.Content.Headers.ContentLength ?? 0;
                if (Length > 0)
                {
                    progress.Report(Progress);
                    finishedProgress = new Tuple<double, double>(Length, Length);

                    using (var cs = vl != null && vl == "gzip" ? new System.IO.Compression.GZipStream(await response.Content.ReadAsStreamAsync(), System.IO.Compression.CompressionMode.Decompress) : await response.Content.ReadAsStreamAsync())
                    {
                        using (var ms = new MemoryStream())
                        {
                            byte[] bytes = new byte[HTTP_STREAM_READ_COUNT];
                            int bytesread = 0;
                            do
                            {
                                if (IsCanceling || State == DownloadState.Failed)
                                {
                                    throw new Exception($"Download {Path.GetFileName(FileName)} has be canceled!");
                                }

                                bytesread = await cs.ReadAsync(bytes, 0, HTTP_STREAM_READ_COUNT, cancelToken);
                                EndTick = DateTime.Now;

                                if (bytesread > 0 && bytesread <= HTTP_STREAM_READ_COUNT && Received < Length)
                                {
                                    lastReceived += bytesread;
                                    Received += bytesread;
                                    await ms.WriteAsync(bytes, 0, bytesread);
                                    progress.Report(Progress);
                                }
                            } while (bytesread > 0 && Received < Length);

                            if (Received == Length && State == DownloadState.Downloading)
                            {
                                result = SaveFile(FileName, ms.ToArray());
                            }
                            else
                            {
                                throw new Exception($"Download {Path.GetFileName(FileName)} Failed! File size not matched with server's size.");
                            }
                        }
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
            catch(Exception ex)
            {
                FailReason = ex.Message;
            }
            finally
            {

            }
            return (result);
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

                if (setting.DownloadCompletedToast)
                    $"{Path.GetFileName(FileName)} is saved!".ShowDownloadToast("Succeed", ThumbnailUrl, FileName);
                if (setting.DownloadCompletedSound && StartTick.DeltaSeconds(EndTick) > setting.DownloadCompletedSoundForElapsedSeconds)
                    this.Sound();
            }
            else if (State == DownloadState.Downloading)
            {
                FailReason = "Unkonwn failed reason when downloading.";
                State = DownloadState.Failed;
            }

            progress.Report(Progress);

            if (cancelSource is CancellationTokenSource) cancelSource.Dispose();
            cancelSource = null;
            if (Downloading is SemaphoreSlim) Downloading.Release();
        }

        private async Task<string> DownloadDirectAsync()
        {
            string result = string.Empty;

            if (await Downloading.WaitAsync(setting.DownloadWaitingTime))
            {
                try
                {
                    if (string.IsNullOrEmpty(Url)) throw new Exception($"Download URL is unknown!");
                    if (!CanDownload) throw new Exception($"Download task can't start now!");

                    IsStart = false;
                    Canceling = false;

                    cancelSource = new CancellationTokenSource();
                    cancelToken = cancelSource.Token;

                    StartTick = DateTime.Now;
                    FailReason = string.Empty;
                    State = DownloadState.Downloading;

                    using (var response = await GetAsyncResponse(Url))
                    {
                        EndTick = DateTime.Now;
                        lastReceived = 0;
                        Received = 0;

                        await DownloadStreamAsync(response);
                    }
                }
                catch (Exception ex)
                {
                    FailReason = ex.Message;
                }
                finally
                {
                    DownloadFinally(out result);
                }
            }
            return (result);
        }

        private async Task<string> DownloadAsync()
        {
            string result = string.Empty;

            if (await Downloading.WaitAsync(setting.DownloadWaitingTime))
            {
                try
                {
                    if (string.IsNullOrEmpty(Url)) throw new Exception($"Download URL is unknown!");
                    if (!CanDownload) throw new Exception($"Download task can't start now!");

                    IsStart = false;
                    Canceling = false;

                    cancelSource = new CancellationTokenSource();
                    cancelToken = cancelSource.Token;

                    StartTick = DateTime.Now;
                    FailReason = string.Empty;
                    State = DownloadState.Downloading;

                    Pixeez.Tokens tokens = await CommonHelper.ShowLogin();
                    using (var async_response = await tokens.SendRequestAsync(Pixeez.MethodType.GET, Url))
                    {
                        if(async_response is Pixeez.AsyncResponse)
                        {
                            EndTick = DateTime.Now;
                            lastReceived = 0;
                            Received = 0;

                            await DownloadStreamAsync(async_response.Source);
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
                }
            }
            return (result);
        }

        private async void Start()
        {
            setting = Application.Current.LoadSetting();
            bool delta = true;
            if (File.Exists(FileName))
            {
                delta = new FileInfo(FileName).CreationTime.DeltaNowMillisecond() > setting.DownloadTimeSpan ? true : false;
                if (!delta) return;
            }

            if (IsDownloading) await Cancel();
            CheckProperties();

            string fc = Url.GetImageCachePath();
            if (File.Exists(fc))
            {
                await new Action(async () =>
                {
                    if (await Downloading.WaitAsync(setting.DownloadWaitingTime))
                    {
                        try
                        {
                            IsStart = false;
                            Canceling = false;
                            StartTick = DateTime.Now;
                            FailReason = string.Empty;
                            State = DownloadState.Downloading;
                            SaveFile(FileName, fc);
                        }
                        catch (Exception) { }
                        finally
                        {
                            if (Downloading is SemaphoreSlim) Downloading.Release();
                        }
                    }
                }).InvokeAsync();
            }
            else
            {
                if (CanDownload)
                {
                    await new Action(async () =>
                    {
                        Random rnd = new Random();
                        await Task.Delay(rnd.Next(20, 200));
                        Application.Current.DoEvents();

                        if (string.IsNullOrEmpty(setting.AccessToken) || setting.ExpTime <= DateTime.Now)
                            await DownloadDirectAsync();
                        else
                            await DownloadAsync();
                    }).InvokeAsync();
                }
            }
        }

        public DownloadItem()
        {
            InitializeComponent();
            setting = Application.Current.LoadSetting();

            Info = new DownloadInfo();

            InitProgress();

            CheckProperties();
        }

        public DownloadItem(string url, bool autostart=true)
        {
            InitializeComponent();
            setting = Application.Current.LoadSetting();

            Info = new DownloadInfo();

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
            //PART_DownloadProgress.IsEnabled = true;
            if (AutoStart)
            {
                if (State == DownloadState.Finished || State == DownloadState.Downloading) return;
                else if (State == DownloadState.Idle) //|| State == DownloadState.Failed)
                {
                    if (string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(PART_FileURL.Text)) Url = PART_FileURL.Text;
                    if(IsStart && !IsDownloading) Start();
                }
            }
        }

        private void PART_Download_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            CheckProperties();
            if (IsStart && !IsDownloading) Start();
        }

        private async void miActions_Click(object sender, RoutedEventArgs e)
        {
            if ((sender == miCopyIllustID || sender == PART_CopyIllustID) && !string.IsNullOrEmpty(Url))
            {
                CommonHelper.Cmd_CopyIllustIDs.Execute(Url);
            }
            else if (sender == miCopyDonwnloadInfo)
            {
                CommonHelper.Cmd_CopyDownloadInfo.Execute(Info);
            }
            else if ((sender == miOpenIllust || sender == PART_OpenIllust) && !string.IsNullOrEmpty(Url))
            {
                var illust = Url.GetIllustId().FindIllust();
                if (illust is Pixeez.Objects.Work)
                    CommonHelper.Cmd_Open.Execute(illust);
                else
                    CommonHelper.Cmd_Open.Execute(Url);
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
