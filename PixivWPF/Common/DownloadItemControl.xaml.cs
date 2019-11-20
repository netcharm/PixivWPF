using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace PixivWPF.Common
{
    public enum DownloadState { Idle, Downloading, Paused, Finished, Failed, Unknown }

    public class DownloadInfo: INotifyPropertyChanged
    {
        private Setting setting = Setting.Load();

        [DefaultValue(false)]
        public bool UsingProxy { get; set; }
        public string Proxy { get; set; }

        [DefaultValue(true)]
        public bool AutoStart { get; set; }
        [DefaultValue(false)]
        public bool Canceled { get; set; }

        private DownloadState state = DownloadState.Idle;
        [DefaultValue(DownloadState.Idle)]
        public DownloadState State
        {
            get { return state; }
            set
            {
                state = value;
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
                NotifyPropertyChanged();
            }
        }

        public string FileName { get; set; }
        public string FolderName { get { return string.IsNullOrEmpty(FileName) ? string.Empty : Path.GetDirectoryName(FileName); } }
        public DateTime FileTime { get; set; }
        public double ProgressPercent { get { return Length>0 ? Received/Length*100 : 0; } }
        public Tuple<double, double> Progress
        {
            get { return Tuple.Create<double, double>(Received, Length); }
            set
            {
                Received = (long)value.Item1;
                Length = (long)value.Item2;
                NotifyPropertyChanged();
            }
        }
        [DefaultValue(0)]
        public long Received { get; set; }
        [DefaultValue(0)]
        public long Length { get; set; }
        [DefaultValue(true)]
        public bool Overwrite { get; set; }

        private bool singlefile = false;
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
            }
        }

        public ImageSource Thumbnail { get; set; }
        public string ThumbnailUrl { get; set; }

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
                NotifyPropertyChanged();
            }
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
        private Setting setting = Setting.Load();

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
                NotifyPropertyChanged();
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
                Info.IsStart = true;
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

        private DateTime lastTick = DateTime.Now;
        private long lastReceived = 0;
        internal IProgress<Tuple<double, double>> progress = null;

        private void CheckProperties()
        {
            if(Tag is DownloadInfo)
            {
                Info = Tag as DownloadInfo;
                progress.Report(Info.Progress);
                if (IsForceStart) State = DownloadState.Idle;
                if(Info.State == DownloadState.Finished)
                {
                    PART_OpenFile.IsEnabled = true;
                    PART_OpenFolder.IsEnabled = true;
                }
                else
                {
                    PART_OpenFile.IsEnabled = false;
                    PART_OpenFolder.IsEnabled = false;
                }
            }
        }

        public void Refresh()
        {
            CheckProperties();
        }

        private async Task<string> StartAsync()
        {
            string result = string.Empty;
            if (string.IsNullOrEmpty(Info.Url)) return (result);

            PART_OpenFile.IsEnabled = false;
            PART_OpenFolder.IsEnabled = false;

            Pixeez.Tokens tokens = await CommonHelper.ShowLogin();
            using (var response = await tokens.SendRequestAsync(Pixeez.MethodType.GET, Info.Url))
            {
                if (response != null && response.Source.StatusCode == HttpStatusCode.OK)
                {
                    PART_DownloadProgress.IsIndeterminate = false;
                    PART_DownloadProgress.IsEnabled = true;
                    Info.Received = 0;
                    Info.Length = (long)response.Source.Content.Headers.ContentLength;
                    progress.Report(Progress);

                    State = DownloadState.Downloading;
                    using (var cs = await response.Source.Content.ReadAsStreamAsync())
                    {
                        //await ProcessContentStream(totalBytes, contentStream);
                        using (var ms = new MemoryStream())
                        {
                            byte[] bytes = new byte[32768];
                            progress.Report(Info.Progress);
                            try
                            {
                                do
                                {
                                    var bytesread = await cs.ReadAsync(bytes, 0, 32768);
                                    if (bytesread >= 0)
                                    {
                                        Info.Received += bytesread;
                                        await ms.WriteAsync(bytes, 0, bytesread);
                                        progress.Report(Info.Progress);
                                    }
                                } while (Info.Received < Info.Length);
                                //if (ms.Length == Info.Received && Info.Received == Info.Length)
                                if (Info.Received == Info.Length)
                                {
                                    File.WriteAllBytes(Info.FileName, ms.ToArray());
                                    State = DownloadState.Finished;
                                    result = Info.FileName;
                                    File.SetCreationTime(FileName, FileTime);
                                    File.SetLastWriteTime(FileName, FileTime);
                                    File.SetLastAccessTime(FileName, FileTime);
                                    progress.Report(Info.Progress);
                                    PART_OpenFile.IsEnabled = true;
                                    PART_OpenFolder.IsEnabled = true;
                                    $"{Path.GetFileName(Info.FileName)} is saved!".ShowToast("Successed", Info.ThumbnailUrl, Info.FileName);
                                    SystemSounds.Beep.Play();
                                }
                                else
                                {
                                    throw new Exception($"Download {Path.GetFileName(Info.FileName)} Failed!");
                                }
                            }
                            catch (Exception ex)
                            {
                                var ret = ex.Message;
                                State = DownloadState.Failed;
                                PART_DownloadProgress.IsIndeterminate = true;
                                PART_DownloadProgress.IsEnabled = false;
                            }
                            finally
                            {
                            }
                        }
                    }
                }
                else result = null;
            }
            return (result);
        }

        private void Start()
        {
            CheckProperties();

            if (State != DownloadState.Idle && State != DownloadState.Failed) return;

            this.Dispatcher.BeginInvoke((Action)(async () =>
            {
                lastTick = DateTime.Now;
                var ret = await StartAsync();
                if (!string.IsNullOrEmpty(ret))
                {
                    PART_OpenFile.IsEnabled = true;
                    PART_OpenFolder.IsEnabled = true;
                }
            }));
        }

        public DownloadItem()
        {
            InitializeComponent();
            Info = new DownloadInfo();

            progress = new Progress<Tuple<double, double>>(i => {
                var received = i.Item1 >= 0 ? i.Item1 : 0;
                var total = i.Item2 >= 0 ? i.Item2 : 0;
                var delta = (DateTime.Now.Ticks - lastTick.Ticks) / 10000000.0;
                var rate = delta>0 ? (received - lastReceived) / delta / 1024.0 : 0;
                //lastTick = DateTime.Now;
                //lastReceived = Convert.ToInt64(received);
                PART_DownloadProgress.Value = total > 0 ? received / total * 100 : 0;
                PART_DownInfo.Text = $"Downloading : {received / 1024.0:0.} KB / {total / 1024.0:0.} KB, {rate:0.00} KB/s";
                PART_DownloadProgressPercent.Text = $"{PART_DownloadProgress.Value:0.0}%";
                //PART_DownloadProgressPercent.Effect = new 
                //PART_DownloadProgressPercent.TextEffects = TextEffect.
            });

            CheckProperties();
        }

        public DownloadItem(string url, bool autostart=true)
        {
            InitializeComponent();

            Info = new DownloadInfo();

            progress = new Progress<Tuple<double, double>>(i => {
                PART_DownloadProgress.Value = i.Item2 > 0 ? i.Item1 / i.Item2 * 100 : 0;
                PART_DownInfo.Text = $"Downloading : {i.Item1 / 1024.0:0.} KB / {i.Item2 / 1024.0:0.} KB";
            });

            AutoStart = autostart;
            Info.Url = url;

            CheckProperties();
        }

        public DownloadItem(DownloadInfo info)
        {
            InitializeComponent();

            if (info is DownloadInfo)
                Info = info;
            else
                Info = new DownloadInfo();

            progress = new Progress<Tuple<double, double>>(i => {
                PART_DownloadProgress.Value = i.Item2 > 0 ? i.Item1 / i.Item2 * 100 : 0;
                PART_DownInfo.Text = $"Downloading : {i.Item1 / 1024.0:0.} KB / {i.Item2 / 1024.0:0.} KB";
            });

            CheckProperties();
        }

        private void Download_Loaded(object sender, RoutedEventArgs e)
        {
            CheckProperties();
            if (AutoStart)
            {
                if (State == DownloadState.Finished || State == DownloadState.Downloading) return;
                else if(State == DownloadState.Idle || State == DownloadState.Failed)
                {
                    if (string.IsNullOrEmpty(Info.Url) && !string.IsNullOrEmpty(PART_FileURL.Text)) Url = (string)PART_FileURL.Text;
                    Start();
                }
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button)
            {
                var btn = sender as Button;
                if (btn.Tag is string)
                {
                    var image = Path.GetDirectoryName((string)btn.Tag);
                    if (Directory.Exists(image))
                    {
                        System.Diagnostics.Process.Start(image);
                    }
                }
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button)
            {
                var btn = sender as Button;
                if (btn.Tag is string)
                {
                    var image = (string)btn.Tag;
                    if (File.Exists(image))
                    {
                        System.Diagnostics.Process.Start(image);
                    }
                }
            }
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            CheckProperties();

            if (sender == PART_Download)
            {
                State = DownloadState.Idle;
                Start();
            }
        }

        private void PART_Download_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            CheckProperties();
            if (Info.IsStart) Start();
        }

        private void miActions_Click(object sender, RoutedEventArgs e)
        {
            if(sender == miCopyIllustID && !string.IsNullOrEmpty(Url))
            {
                CommonHelper.Cmd_CopyIllustIDs.Execute(Url);
            }
            else if(sender == miOpenIllust && !string.IsNullOrEmpty(Url))
            {
                CommonHelper.Cmd_OpenIllust.Execute(Url);
            }
            else if (sender == miDownload)
            {
                CheckProperties();
                State = DownloadState.Idle;
                Start();
            }
            else if (sender == miOpenImage)
            {
                if (!string.IsNullOrEmpty(FileName) && File.Exists(FileName))
                {
                    System.Diagnostics.Process.Start(FileName);
                }
            }
            else if (sender == miOpenFolder)
            {
                if (!string.IsNullOrEmpty(FolderName) && Directory.Exists(FolderName))
                {
                    System.Diagnostics.Process.Start(FolderName);
                }
            }
        }
    }
}
