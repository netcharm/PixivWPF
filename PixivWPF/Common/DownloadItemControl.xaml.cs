using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Text;
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
    public enum DownloadState { Idle, Downloading, Pause, Finished, Failed, Unkonown }

    /// <summary>
    /// DownloadItemControl.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadItem : UserControl, INotifyPropertyChanged
    {
        private Setting setting = Setting.Load();

        public bool UsingProxy { get; set; }
        public string Proxy { get; set; }

        private bool canceled = false;
        public bool Canceled
        {
            get { return canceled; }
            set { canceled = value; }
        }

        private string url = string.Empty;
        public string Url
        {
            get { return url; }
            set
            {
                url = value;

                if (!string.IsNullOrEmpty(url))
                {
                    var file = Path.GetFileName(url).Replace("_p", "_");
                    if (SingleFile)
                        file = file.Replace("_0.", ".");
                    filename = file;
                }

                if (string.IsNullOrEmpty(setting.LastFolder))
                {
                    SaveFileDialog dlgSave = new SaveFileDialog();
                    dlgSave.FileName = filename;
                    if (dlgSave.ShowDialog() == true)
                    {
                        filename = dlgSave.FileName;
                        setting.LastFolder = Path.GetDirectoryName(filename);
                    }
                    else
                    {
                        canceled = true;
                        //else FileName = string.Empty;
                        return;
                    }
                }
                filename = Path.Combine(setting.LastFolder, Path.GetFileName(FileName));

                //PART_FileURL.Text = url;
                //PART_FileName.Text = Path.GetFileName(filename);
                //PART_FileFolder.Text = Path.GetDirectoryName(filename);

                //Task.Run(async () => {
                //    Thumbnail = await url.LoadImage();
                //    //PART_Preview.Source = await url.LoadImage();
                //});
                //UpdateLayout();
            }
        }

        public ImageSource Thumbnail { get; set; }

        private string filename = string.Empty;
        public string FileName
        {
            get { return filename; }
            set
            {
                filename = value;

                PART_FileURL.Text = url;
                PART_FileName.Text = Path.GetFileName(filename);
                PART_FileFolder.Text = Path.GetDirectoryName(filename);
            }
        }

        public string FolderName
        {
            get { return Path.GetDirectoryName(filename); }
        }

        public bool AutoStart { get; set; }

        private DownloadState state = DownloadState.Unkonown;
        public DownloadState State
        {
            get { return state; }
        }

        private double _progress;
        public double Progress
        {
            get { return _progress; }
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    RaisePropertyChanged("Progress");
                }
            }
        }

        private long received = 0;
        public long Received
        {
            get { return received >= 0 ? received : 0; }
        }

        private long length = 0;
        public long Length
        {
            get { return length; }
        }

        [DefaultValue(true)]
        public bool Overwrite { get; set; }
        [DefaultValue(true)]
        public bool SingleFile { get; set; }
        //[DefaultValue(DateTime.Now)]
        public DateTime FileTime { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        internal IProgress<double> progress = null;
        public DownloadItem()
        {
            InitializeComponent();
            FileTime = DateTime.Now;

            progress = new Progress<double>(i => { PART_DownloadProgress.Value = i; });
            progress.Report(50);
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button)
            {
                var btn = sender as Button;
                if (btn.Tag is string)
                {
                    var image = Path.GetDirectoryName((string)btn.Tag);
                    System.Diagnostics.Process.Start(image);
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
                    System.Diagnostics.Process.Start(image);
                }
            }
        }

        public async Task<string> StartAsync()
        {
            string result = string.Empty;

            PART_OpenFile.IsEnabled = false;
            PART_OpenFolder.IsEnabled = false;

            progress.Report(.0);

            Pixeez.Tokens tokens = await CommonHelper.ShowLogin();
            using (var response = await tokens.SendRequestAsync(Pixeez.MethodType.GET, url))
            {
                if (response != null && response.Source.StatusCode == HttpStatusCode.OK)
                {
                    received = 0;
                    length = (long)response.Source.Content.Headers.ContentLength;
                    state = DownloadState.Downloading;

                    using (var cs = await response.Source.Content.ReadAsStreamAsync())
                    {
                        //await ProcessContentStream(totalBytes, contentStream);
                        using (var ms = new MemoryStream())
                        {
                            progress.Report(0);
                            do
                            {
                                byte[] bytes = new byte[65536];
                                var bytesread = await cs.ReadAsync(bytes, 0, 65536);
                                if (bytesread >= 0)
                                {
                                    received += bytesread;
                                    await ms.WriteAsync(bytes, 0, bytesread);

                                    progress.Report((double)received / length * 100);
                                    //Progress = (double)received / length * 100;
                                }
                            } while (received < length);
                            if (ms.Length == length)
                            {
                                File.WriteAllBytes(filename, ms.ToArray());
                                result = filename;
                            }
                        }
                        if (!string.IsNullOrEmpty(result))
                        {
                            progress.Report(100.0);
                            state = DownloadState.Finished;
                            File.SetCreationTime(FileName, FileTime);
                            File.SetLastWriteTime(FileName, FileTime);
                            File.SetLastAccessTime(FileName, FileTime);
                            $"{Path.GetFileName(filename)} is saved!".ShowToast("Successed", filename);
                            SystemSounds.Beep.Play();
                            PART_OpenFile.IsEnabled = true;
                            PART_OpenFolder.IsEnabled = true;
                        }
                    }

                    //using (var ms = new MemoryStream())
                    //{
                    //    while (received >= length)
                    //    {
                    //        var from = received;
                    //        var to = received + 1024 >= length ? received + length - received - 1 : received + 1024 - 1;
                    //        response.Source.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(from, to);
                    //        var bytes = await response.Source.Content.ReadAsByteArrayAsync();
                    //        await ms.WriteAsync(bytes, 0, bytes.Length);
                    //    }
                    //    File.WriteAllBytes(FileName, ms.ToArray());
                    //    result = FileName;
                    //}
                }
                else result = null;
            }
            return (result);
        }

        public void Start()
        {
            this.Dispatcher.BeginInvoke((Action)(async () =>
            {
                var ret = await StartAsync();

                if (!string.IsNullOrEmpty(ret))
                {
                    PART_OpenFile.IsEnabled = true;
                    PART_OpenFolder.IsEnabled = true;
                }

            }));

        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            if(sender == PART_Download)
            {
                var btn = (sender as Button);
                if(btn.Tag is string)
                {
                    if (string.IsNullOrEmpty(url))
                    {
                        Url = (string)btn.Tag;
                    }
                    Start();
                }
            }
        }
    }
}
