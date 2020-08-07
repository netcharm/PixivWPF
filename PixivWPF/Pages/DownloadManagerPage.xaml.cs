using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using PixivWPF.Common;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace PixivWPF.Pages
{
    /// <summary>
    /// DownloadManagerPage.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadManagerPage : Page
    {
        private Window window = null;
        private Setting setting = Setting.Instance == null ? Setting.Load() : Setting.Instance;

        [DefaultValue(true)]
        public bool AutoStart { get; set; }

        [DefaultValue(5)]
        public uint MaxJobs { get; set; } = 10;

        private TimerCallback tcb = null;
        private Timer timer = null;
        //private bool IsIdle = true;
        private bool IsUpdating = false;

        public Point Pos { get; set; } = new Point(0, 0);

        private ObservableCollection<DownloadInfo> items = new ObservableCollection<DownloadInfo>();
        public ObservableCollection<DownloadInfo> Items
        {
            get { return items; }
        }

        private void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            foreach (var item in Items)
            {
                if (item is DownloadInfo)
                {
                    item.UpdateDownloadState(illustid, exists);
                }
            }
        }

        public async void UpdateDownloadStateAsync(int? illustid = null, bool? exists = false)
        {
            await Task.Run(() =>
            {
                UpdateDownloadState(illustid, exists);
            });
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
            var jobs_count = items.Where(i => i.State == DownloadState.Downloading).Count();
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
            try
            {
                if (IsUpdating) return;
                await new Action(() =>
                {
                    try
                    {
                        IsUpdating = true;
                        var remove = items.Where(o => o.State == DownloadState.Remove );
                        foreach (var i in remove) { items.Remove(i); }

                        var idle = items.Where(o => o.State == DownloadState.Idle );
                        var downloading = items.Where(o => o.State == DownloadState.Downloading);
                        var failed = items.Where(o => o.State == DownloadState.Failed );
                        var finished = items.Where(o => o.State == DownloadState.Finished );
                        var nonexists = items.Where(o => o.State == DownloadState.NonExists );

                        PART_DownloadState.Text = $"Total: {items.Count()}, Idle: {idle.Count()}, Downloading: {downloading.Count()}, Finished: {finished.Count()}, Failed: {failed.Count()}, Non-Exists: {nonexists.Count()}";
                    }
                    catch (Exception) { }
                    finally { IsUpdating = false; }
                }).InvokeAsync();
            }
            catch (Exception) { }
        }

        private bool IsExists(string url)
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

        internal void Add(DownloadInfo item)
        {
            if (item is DownloadInfo)
            {
                items.Add(item);
                DownloadItems.ScrollIntoView(item);
            }
        }

        internal async void Add(string url, string thumb, DateTime dt, bool is_meta_single_page = false, bool overwrite = true)
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

                var item = new DownloadInfo()
                {
                    AutoStart = AutoStart,
                    SingleFile = is_meta_single_page,
                    Overwrite = overwrite,
                    ThumbnailUrl = thumb,
                    Thumbnail = await thumb.LoadImageFromUrl(),
                    Url = url,
                    FileTime = dt
                };
                Add(item);
                //IsIdle = false;
            }
        }

        internal void Refresh()
        {
            DownloadItems.Items.Refresh();
        }

        internal void Start()
        {
            foreach (var item in items)
            {
                //this.button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                //item.PART_Download.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                //item.Start();
                //item.IsDownloading = true;
            }
        }

        private void DownloadItem_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if (e.Property != null)
            {
                if (e.Property.Name == "Tag" || e.Property.Name == "Value" || e.Property.Name == "StateChanged")
                {
                    UpdateStateInfo();
                    if (e.Source is DownloadItem)
                    {
                        (e.Source as DownloadItem).UpdateDownloadState();
                    }
                }
            }
        }

        private void PART_MaxJobs_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MaxJobs = Convert.ToUInt32(PART_MaxJobs.Value);
            PART_MaxJobs.ToolTip = $"Max Simultaneous Jobs: {MaxJobs}";
        }

        private async void PART_DownloadAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await new Action(() =>
                {
                    if (DownloadItems.SelectedItems is IEnumerable && DownloadItems.SelectedItems.Count > 1)
                    {
                        var targets = new List<DownloadInfo>();
                        foreach (var item in DownloadItems.SelectedItems)
                        {
                            if (item is DownloadInfo)
                                targets.Add(item as DownloadInfo);
                        }
                        var needUpdate = targets.Where(item => item.State != DownloadState.Downloading && item.State != DownloadState.Finished);
                        if (needUpdate.Count() > 0)
                        {
                            var opt = new ParallelOptions();
                            opt.MaxDegreeOfParallelism = 5;
                            var ret = Parallel.ForEach(needUpdate, opt, (item, loopstate, elementIndex) =>
                            {
                                item.IsStart = true;
                                item.State = DownloadState.Idle;
                            });
                        }
                    }
                    else
                    {
                        var needUpdate = items.Where(item => item.State != DownloadState.Downloading && item.State != DownloadState.Finished);
                        if (needUpdate.Count() > 0)
                        {
                            var opt = new ParallelOptions();
                            opt.MaxDegreeOfParallelism = 5;
                            var ret = Parallel.ForEach(needUpdate, opt, (item, loopstate, elementIndex) =>
                            {
                                item.IsStart = true;
                                item.State = DownloadState.Idle;
                            });
                        }
                    }
                }).InvokeAsync();
            }
            catch (Exception) { }
        }

        private async void PART_RemoveAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await new Action(() =>
                {
                    if (DownloadItems.SelectedItems is IEnumerable && DownloadItems.SelectedItems.Count > 1)
                    {
                        var targets = new List<DownloadInfo>();
                        foreach (var item in DownloadItems.SelectedItems)
                        {
                            if (item is DownloadInfo)
                                targets.Add(item as DownloadInfo);
                        }
                        var remove = targets.Where(o => o.State != DownloadState.Downloading);
                        foreach (var i in remove) { i.State = DownloadState.Remove; }
                    }
                    else
                    {
                        var remove = items.Where(o => o.State != DownloadState.Downloading);
                        foreach (var i in remove) { i.State = DownloadState.Remove; }
                    }
                }).InvokeAsync();
            }
            catch (Exception) { }
        }

        private void PART_ChangeFolder_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dlg = new CommonOpenFileDialog()
            {
                Title = "Select Folder",
                IsFolderPicker = true,
                InitialDirectory = setting.LastFolder,

                AddToMostRecentlyUsedList = false,
                AllowNonFileSystemItems = false,
                DefaultDirectory = setting.LastFolder,
                EnsureFileExists = true,
                EnsurePathExists = true,
                EnsureReadOnly = false,
                EnsureValidNames = true,
                Multiselect = false,
                ShowPlacesList = true
            };

            if (dlg.ShowDialog() == CommonFileDialogResult.Ok)
            {
                setting.LastFolder = dlg.FileName;
                // Do something with selected folder string
                setting.LastFolder.AddDownloadedWatcher();
            }
        }

        private async void PART_CopyID_Click(object sender, RoutedEventArgs e)
        {
            await new Action(() =>
            {
                var targets = new List<string>();
                var items = DownloadItems.SelectedItems is IEnumerable && DownloadItems.SelectedItems.Count > 1 ? DownloadItems.SelectedItems : DownloadItems.Items;
                foreach (var item in items)
                {
                    if (item is DownloadInfo)
                        targets.Add((item as DownloadInfo).FileName.ParseLink().ParseID());
                }
                Clipboard.SetText(string.Join(Environment.NewLine, targets));
            }).InvokeAsync();
        }

        private async void PART_CopyInfo_Click(object sender, RoutedEventArgs e)
        {
            await new Action(() =>
            {
                var targets = new List<string>();
                var items = DownloadItems.SelectedItems is IEnumerable && DownloadItems.SelectedItems.Count > 1 ? DownloadItems.SelectedItems : DownloadItems.Items;
                if (items.Count > 0)
                    targets.Add(@"--------------------------------------------------------------------------------------------");
                foreach (var item in items)
                {
                    if (item is DownloadInfo)
                    {
                        var di = item as DownloadInfo;
                        targets.Add($"URL    : {di.Url}");
                        targets.Add($"File   : {di.FileName}");
                        targets.Add($"Status : {di.Received / 1024.0:0.} KB / {di.Length / 1024.0:0.} KB ({di.Received} Bytes / {di.Length} Bytes)");
                        targets.Add(@"--------------------------------------------------------------------------------------------");
                    }
                }
                Clipboard.SetText(string.Join(Environment.NewLine, targets));
            }).InvokeAsync();
        }
    }
}
