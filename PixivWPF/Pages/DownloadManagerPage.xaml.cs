using Microsoft.Win32;
using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace PixivWPF.Pages
{
    /// <summary>
    /// DownloadManagerPage.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadManagerPage : Page
    {
        internal Window window = null;
        internal Setting setting = Setting.Load();

        [DefaultValue(true)]
        public bool AutoStart { get; set; }

        [DefaultValue(5)]
        public uint MaxJobs { get; set; } = 10;

        private TimerCallback tcb = null;
        private Timer timer = null;
        //private bool IsIdle = true;

        private ObservableCollection<DownloadInfo> items = new ObservableCollection<DownloadInfo>();
        public ObservableCollection<DownloadInfo> Items
        {
            get { return items; }
        }

        public DownloadManagerPage()
        {
            InitializeComponent();
            DataContext = this;

            tcb = timerCallback;
            timer = new Timer(tcb);
            timer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(1000));

            PART_MaxJobs.Value = MaxJobs;

            DownloadItems.ItemsSource = items;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            window = Window.GetWindow(this);
        }

        private void timerCallback(object stateInfo)
        {
            //if (IsIdle) return;

            //var states_job = new List<DownloadState>() { DownloadState.Downloading, DownloadState.Writing, DownloadState.Finished };

            var jobs_count = items.Where(i => i.State == DownloadState.Downloading).Count();
            //var pre_jobs = items.Where(i => i.State != DownloadState.Downloading && i.State != DownloadState.Writing && i.State != DownloadState.Finished);
            var pre_jobs = items.Where(i => i.State == DownloadState.Idle || i.State == DownloadState.Paused);//|| item.State == DownloadState.Failed)
            foreach (var item in pre_jobs)
            {
                if (jobs_count < MaxJobs)
                {
                    //if (states_job.Contains(item.State)) continue;
                    item.IsStart = true;
                    jobs_count++;
                }
            }
            UpdateStateInfo();
        }

        private async void UpdateStateInfo()
        {
            await new Action(() => {
                var idle = items.Where(o => o.State == DownloadState.Idle );
                var downloading = items.Where(o => o.State == DownloadState.Downloading || o.State == DownloadState.Writing );
                var failed = items.Where(o => o.State == DownloadState.Failed );
                var finished = items.Where(o => o.State == DownloadState.Finished );

                PART_DownloadState.Text = $"Total: {items.Count()}, Idle: {idle.Count()}, Downloading: {downloading.Count()}, Finished: {finished.Count()}, Failed: {failed.Count()}";
            }).InvokeAsync();
        }

        public bool IsExists(string url)
        {
            bool result = false;

            if (string.IsNullOrEmpty(url))
                result = true;
            else
            {
                foreach (var item in items)
                {
                    if (item.Url.Equals(url, StringComparison.Ordinal))
                    {
                        result = true;
                        if (item.State == DownloadState.Failed ||
                            item.State == DownloadState.Idle ||
                            item.State == DownloadState.Paused)
                            item.IsStart = true;
                        break;
                    }
                }
            }

            return (result);
        }

        public void Add(DownloadInfo item)
        {
            if(item is DownloadInfo)
            {
                items.Add(item);
                DownloadItems.ScrollIntoView(item);
            }
        }

        public async void Add(string url, string thumb, DateTime dt, bool is_meta_single_page = false, bool overwrite = true)
        {
            if (!IsExists(url))
            {
                var Canceled = false;
                if (string.IsNullOrEmpty(setting.LastFolder))
                {
                    if (!string.IsNullOrEmpty(url))
                    {
                        SaveFileDialog dlgSave = new SaveFileDialog();
                        var file = url.GetImageName(is_meta_single_page);
                        dlgSave.FileName = file;
                        if (dlgSave.ShowDialog() == true)
                        {
                            file = dlgSave.FileName;
                            setting.LastFolder = System.IO.Path.GetDirectoryName(file);
                        }
                        else
                        {
                            Canceled = true;
                            return;
                        }
                    }
                }
                if (Canceled) return;

                Pixeez.Tokens tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;
                var item = new DownloadInfo()
                {
                    AutoStart = AutoStart,
                    SingleFile = is_meta_single_page,
                    Overwrite = overwrite,
                    ThumbnailUrl = thumb,
                    Thumbnail = await thumb.LoadImage(tokens),
                    Url = url,
                    FileTime = dt
                };
                Add(item);
                //IsIdle = false;
            }
        }

        public void Refresh()
        {
            DownloadItems.Items.Refresh();
        }

        public void Start()
        {
            foreach(var item in items)
            {
                //this.button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                //item.PART_Download.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                //item.Start();
                //item.IsDownloading = true;
            }
        }

        private void DownloadAll_Click(object sender, RoutedEventArgs e)
        {
            new Task(() =>
            {
                var needUpdate = items.Where(item => item.State != DownloadState.Downloading && item.State != DownloadState.Finished);
                if (needUpdate.Count() > 0)
                {
                    using (DownloadItems.Items.DeferRefresh())
                    {
                        var opt = new ParallelOptions();
                        opt.MaxDegreeOfParallelism = 5;
                        var ret = Parallel.ForEach(needUpdate, opt, (item, loopstate, elementIndex) =>
                        {
                            item.IsStart = true;
                        });
                    }
                }
            }).Start();
        }

        private void DownloadItem_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if (e.Property != null)
            {
                if (e.Property.Name == "Tag" || e.Property.Name == "Value" || e.Property.Name == "StateChanged")
                {
                    UpdateStateInfo();
                }
            }
        }

        private void PART_MaxJobs_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MaxJobs = Convert.ToUInt32(PART_MaxJobs.Value);
            PART_MaxJobs.ToolTip = $"Max Simultaneous Jobs: {MaxJobs}";
        }
    }
}
