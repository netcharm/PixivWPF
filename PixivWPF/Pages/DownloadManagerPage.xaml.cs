using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

using PixivWPF.Common;

namespace PixivWPF.Pages
{
    public class DownloadParams
    {
        public string Url { get; set; } = string.Empty;
        public bool SaveAsJPEG { get; set; } = false;
        public string ThumbUrl { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = default(DateTime);
        public bool IsSinglePage { get; set; } = false;
        public bool OverwriteExists { get; set; } = true;
        public bool SaveLargePreview { get; set; } = false;
    }

    [Flags]
    public enum DownloadType { None = 0, AsJPEG = 1, UseLargePreview = 2, ConvertKeepName = 8192, Foece = 16384, Original = 32768, All = 0xFFFF };

    /// <summary>
    /// DownloadManagerPage.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadManagerPage : Page, IDisposable
    {
        public Window ParentWindow { get; internal set; } = null;
        public Point Pos { get; set; } = new Point(0, 0);

        private Setting setting = Application.Current.LoadSetting();

        #region Properties
        [DefaultValue(true)]
        public bool AutoStart { get; set; } = true;

        [DefaultValue(10)]
        public int SimultaneousJobs { get { return (setting.DownloadSimultaneous); } set { setting.DownloadSimultaneous = value; } }

        [DefaultValue(25)]
        public int MaxSimultaneousJobs { get { return (setting.DownloadMaxSimultaneous); } }

        public IEnumerable<DownloadInfo> CurrentJobs
        {
            get
            {
                if (!(items is ObservableCollection<DownloadInfo>)) items = new ObservableCollection<DownloadInfo>();
                return (items.Where(item => item.State == DownloadState.Downloading || item.State == DownloadState.Writing));
            }
        }

        public int CurrentJobsCount
        {
            get { return CurrentJobs.Count(); }
        }

        public IEnumerable<DownloadInfo> CurrentIdles
        {
            get
            {
                if (!(items is ObservableCollection<DownloadInfo>)) items = new ObservableCollection<DownloadInfo>();
                return (items.Where(item => item.State == DownloadState.Idle || item.State == DownloadState.Paused));
            }
        }

        public int CurrentIdlesCount
        {
            get { return CurrentIdles.Count(); }
        }

        public bool CanStartDownload { get { return (CurrentJobsCount < SimultaneousJobs); } }
        #endregion

        #region Time Checking
        private DispatcherTimer autoTaskTimer = null;

        private async void CheckJobStateAsync()
        {
            try
            {
                await new Action(() =>
                {
                    //if (IsLoaded && ParentWindow.IsVisible())
                    if (IsLoaded)
                    {
                        setting = Application.Current.LoadSetting();
                        if (PART_MaxJobs.Value != SimultaneousJobs) PART_MaxJobs.Value = SimultaneousJobs;
                        if (PART_MaxJobs.Maximum != MaxSimultaneousJobs) PART_MaxJobs.Maximum = MaxSimultaneousJobs;

                        var jobs_count = CurrentJobsCount;
                        foreach (var item in CurrentIdles)
                        {
                            if (jobs_count < SimultaneousJobs)
                            {
                                if (AutoStart || item.AutoStart) item.IsStart = true;
                                jobs_count++;
                            }
                        }
                        if (ParentWindow.IsVisible()) UpdateStateInfo();
                    }
                }).InvokeAsync();
            }
            catch (Exception ex) { ex.ERROR("CheckJobState"); }
        }

        private void InitTaskTimer()
        {
            try
            {
                if (autoTaskTimer == null)
                {
                    var setting = Application.Current.LoadSetting();
                    autoTaskTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(setting.ToastTimeout), IsEnabled = false };
                    autoTaskTimer.Interval = TimeSpan.FromSeconds(1);
                    autoTaskTimer.Tick += Timer_Tick;
                    autoTaskTimer.Start();
                }
            }
            catch (Exception ex) { ex.ERROR("InitTaskTimer"); }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            CheckJobStateAsync();
        }
        #endregion

        #region Update UI
        public void UpdateTheme()
        {
            UpdateDownloadStateAsync();
        }

        private async void UpdateDownloadState(int? illustid = null, bool? exists = null)
        {
            foreach (var item in Items.ToArray())
            {
                if (item is DownloadInfo)
                {
                    await new Action(() =>
                    {
                        item.UpdateDownloadState(illustid, exists);
                    }).InvokeAsync(true);
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

        public async void UpdateLikeState(long id = -1, bool is_user = false)
        {
            var needUpdate = is_user ? items.Where(i => i.UserID == id) : items.Where(i => i.IllustID == id);
            try
            {
                foreach (var item in needUpdate.ToArray())
                {
                    await new Action(() =>
                    {
                        item.UpdateLikeState();
                    }).InvokeAsync();
                }
            }
            catch (Exception ex) { ex.DEBUG("UpdateLikeState"); }
        }

        public async void UpdateLikeStateAsync(long illustid = -1, bool is_user = false)
        {
            await Task.Run(() =>
            {
                UpdateLikeState(illustid, is_user);
            });
        }

        private async void UpdateStateInfo()
        {
            if (ParentWindow is Window && ParentWindow.WindowState != WindowState.Minimized)
            {
                if (await CanUpdateState.WaitAsync(0))
                {
                    await new Action(() =>
                    {
                        try
                        {
                            var none = new List<DownloadInfo>();
                            var cats = items.GroupBy(i => i.State).ToDictionary(i => i.Key, i => i.ToList());
                            var idle = cats.ContainsKey(DownloadState.Idle) ? cats[DownloadState.Idle] : none;
                            var remove = cats.ContainsKey(DownloadState.Remove) ? cats[DownloadState.Remove] : none;
                            var failed = cats.ContainsKey(DownloadState.Failed) ? cats[DownloadState.Failed] : none;
                            var finished = cats.ContainsKey(DownloadState.Finished) ? cats[DownloadState.Finished] : none;
                            var nonexists = cats.ContainsKey(DownloadState.NonExists) ? cats[DownloadState.NonExists] : none;
                            var downloading = cats.ContainsKey(DownloadState.Downloading) ? cats[DownloadState.Downloading] : none;

                            var rates = downloading.Sum(o => o.DownRateCurrent);

                            PART_DownloadState.Text = $"Total: {items.Count() - remove.Count()}, Idle: {idle.Count()}, Downloading: {downloading.Count()}, Finished: {finished.Count()}, Failed: {failed.Count()}, Non-Exists: {nonexists.Count()}, Rate: {rates.SmartSpeedRate()}";

                            var remove_count = remove.Count();
                            if (remove_count > 0)
                            {
                                for (int i = remove_count - 1; i >= 0; i--)
                                {
                                    remove[i].Url.DEBUG("DM_REMOVE");
                                    remove[i].Dispose();
                                    items.Remove(remove[i]);
                                }
                                if (remove_count >= 30 && DownloadItems.HasItems)
                                {                                    
                                    //items = new ObservableCollection<DownloadInfo>(Items.ToList());
                                    DownloadItems.ItemsSource = items;
                                }
                            }
                        }
                        catch (Exception ex) { ex.ERROR("UpdateDownloadManagerStateInfo"); }
                        finally
                        {
                            if (CanUpdateState is SemaphoreSlim && CanUpdateState.CurrentCount <= 0) CanUpdateState.Release();
                        }
                    }).InvokeAsync(true);
                }
            }
        }
        #endregion

        #region Items Helper
        private SemaphoreSlim CanAddItem = new SemaphoreSlim(1, 1);
        private SemaphoreSlim CanUpdateState = new SemaphoreSlim(1, 1);

        private ObservableCollection<DownloadInfo> items = new ObservableCollection<DownloadInfo>();
        public ObservableCollection<DownloadInfo> Items
        {
            get { return items; }
        }

        internal void Refresh()
        {
            try
            {
                //DownloadItems.Items.DeferRefresh();
                DownloadItems.Items.Refresh();
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public IList<string> Unfinished()
        {
            List<string> result = new List<string>();
            Dispatcher.Invoke(() =>
            {
                var unfinished = items.Where(i => i.State != DownloadState.Finished).ToList();
                foreach (var item in unfinished)
                {
                    result.Add($"Downloading: {item.Url.ParseID()}");
                }
            });
            return (result);
        }

        public IList<DownloadInfo> GetDownloadInfo()
        {
            List<DownloadInfo> dis = new List<DownloadInfo>();
            var items = HasMultipleSelected() ? DownloadItems.SelectedItems : DownloadItems.Items;
            foreach (var item in DownloadItems.Items)
            {
                if (items.Contains(item)) dis.Add(item as DownloadInfo);
            }
            return (dis);
        }

        public IList<DownloadInfo> GetSelectedItems()
        {
            List<DownloadInfo> dis = new List<DownloadInfo>();
            var items = HasSelected() ? DownloadItems.SelectedItems : dis;
            foreach (var item in DownloadItems.Items)
            {
                if (items.Contains(item)) dis.Add(item as DownloadInfo);
            }
            return (dis);
        }

        public IList<DownloadInfo> GetOlderItems(int days = 1)
        {
            var result = new List<DownloadInfo>();
            try
            {
                foreach (var item in (DownloadItems.SelectedItems is IEnumerable && DownloadItems.SelectedItems.Count > 1 ? DownloadItems.SelectedItems : items))
                {
                    if (item is DownloadInfo) result.Add(item as DownloadInfo);
                }
                var results = result.Select(o => new KeyValuePair<DateTime, DownloadInfo>(o.Url.ParseDateTime(), o)).OrderByDescending(o => o.Key).ToList();
                if (results.Count() > 0)
                {
                    var now = TimeZoneInfo.ConvertTime(DateTime.Now, CommonHelper.TokoyTimeZone);
                    var ndays = now.Date - TimeSpan.FromDays(Math.Max(0, days - 1));
                    ndays = TimeZoneInfo.ConvertTime(ndays, CommonHelper.TokoyTimeZone, CommonHelper.LocalTimeZone);
                    result = results.Where(i => i.Key < ndays).Select(i => i.Value).ToList();
                }
            }
            catch(Exception ex) { ex.ERROR("GetOlderDownloadedItems[Older then {days}(s)]"); }
            return (result);
        }

        public bool HasSelected()
        {
            return (DownloadItems.SelectedItems is IEnumerable && DownloadItems.SelectedItems.Count > 0);
        }

        public bool HasMultipleSelected()
        {
            return (DownloadItems.SelectedItems is IEnumerable && DownloadItems.SelectedItems.Count > 1);
        }

        private bool IsExists(string url)
        {
            bool result = false;
            try
            {
                if (string.IsNullOrEmpty(url))
                    result = true;
                else
                    result = items.Where(i => i.Url.Equals(url, StringComparison.CurrentCultureIgnoreCase)).Count() > 0;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        private bool IsExists(DownloadInfo item)
        {
            bool result = false;
            try
            {
                //lock (items)
                {
                    if (string.IsNullOrEmpty(item.Url))
                        result = true;
                    else
                        result = items.Where(i => i.Url.Equals(item.Url, StringComparison.CurrentCultureIgnoreCase)).Count() > 0;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        internal void Add(DownloadInfo item)
        {
            if (item is DownloadInfo)// && !IsExists(item))
            {
                //lock (items)
                {
                    items.Add(item);
                    DownloadItems.ScrollIntoView(item);
                }
            }
        }

        internal async void Add(string url, string thumb, DateTime dt, bool is_meta_single_page = false, bool overwrite = true, bool jpeg = false, bool largepreview = false)
        {
            setting = Application.Current.LoadSetting();

            if (!IsExists(url))
            {
                if (await CanAddItem.WaitAsync(TimeSpan.FromSeconds(setting.DownloadHttpTimeout)))
                {
                    if (string.IsNullOrEmpty(setting.LastFolder))
                    {
                        Application.Current.SaveTarget();
                        //if (CanAddItem is SemaphoreSlim && CanAddItem.CurrentCount <= 0) CanAddItem.Release();
                    }

                    var item = new DownloadInfo()
                    {
                        AutoStart = AutoStart,
                        SaveAsJPEG = jpeg,
                        UseLargePreview = largepreview,
                        SingleFile = is_meta_single_page,
                        Overwrite = overwrite,
                        ThumbnailUrl = thumb,
                        Url = url,
                        FileTime = dt
                    };
                    Add(item);
                    if (CanAddItem is SemaphoreSlim && CanAddItem.CurrentCount <= 0) CanAddItem.Release();
                }
            }
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~DownloadManagerPage() {
        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }
        #endregion

        public DownloadManagerPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ParentWindow = Window.GetWindow(this);

            setting = Application.Current.LoadSetting();
            if (PART_MaxJobs.Value != SimultaneousJobs) PART_MaxJobs.Value = SimultaneousJobs;
            if (PART_MaxJobs.Maximum != MaxSimultaneousJobs) PART_MaxJobs.Maximum = MaxSimultaneousJobs;
            PART_MaxJobs.ToolTip = $"Max Simultaneous Jobs: {SimultaneousJobs} / {MaxSimultaneousJobs}";

            DownloadItems.ItemsSource = items;
            InitTaskTimer();
            UpdateStateInfo();
        }

        private void DownloadItem_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if (e.Property != null)
            {
                if (e.Property.Name == "Tag" || e.Property.Name == "Value" || e.Property.Name == "State")
                {
                    UpdateStateInfo();
                    if (e.Source is DownloadItem)
                    {
                        (e.Source as DownloadItem).UpdateDownloadState();
                    }
                }
            }
        }

        private void DownloadItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DownloadItems.SelectedItem is DownloadInfo)
            {
                var added = new List<DownloadInfo>();
                var removed = new List<DownloadInfo>();
                foreach (var i in e.AddedItems) { if (i is DownloadInfo) added.Add(i as DownloadInfo); }
                foreach (var i in e.AddedItems) { if (i is DownloadInfo) added.Add(i as DownloadInfo); }
                var diff = added.Except(removed);
                if (diff.Count() > 0) (DownloadItems.SelectedItem as DownloadInfo).UpdateInfo();
            }
        }

        private void DownloadItems_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                var shift = Keyboard.Modifiers == ModifierKeys.Alt || e.XButton1 == MouseButtonState.Pressed;
                if (shift && e.LeftButton == MouseButtonState.Pressed)
                {
                    if (DownloadItems.SelectedItems is IEnumerable && DownloadItems.SelectedItems.Count > 0)
                    {
                        e.Handled = true;
                        (sender as UIElement).AllowDrop = false;
                        //var items = DownloadItems.SelectedItems is IEnumerable && DownloadItems.SelectedItems.Count > 1 ? DownloadItems.SelectedItems : DownloadItems.Items;
                        var items = DownloadItems.SelectedItems.Cast<DownloadInfo>().Where(i => i.State == DownloadState.Finished);
                        var target = new List<string>();
                        foreach (var item in items)
                        {
                            if (item is DownloadInfo && (item as DownloadInfo).State == DownloadState.Finished) target.Add((item as DownloadInfo).FileName);
                        }
                        if (target.Count > 0) this.DragOut(target);
                    }
                }
            }
            finally
            {
                (sender as UIElement).AllowDrop = true;
            }
        }

        private void PART_ChangeFolder_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.SaveTarget(string.Empty);
        }

        private async void PART_MaxJobs_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                await new Action(() =>
                {
                    if (IsLoaded)
                    {
                        setting = Application.Current.LoadSetting();
                        SimultaneousJobs = Convert.ToInt32(PART_MaxJobs.Value);
                        PART_MaxJobs.ToolTip = $"Max Simultaneous Jobs: {SimultaneousJobs} / {MaxSimultaneousJobs}";
                    }
                }).InvokeAsync();
            }
            catch (Exception ex) { ex.ERROR(); }
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
                    {
                        var shift = Keyboard.Modifiers == ModifierKeys.Shift;
                        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
                        if (shift && (item as DownloadInfo).State == DownloadState.Finished) { targets.Add((item as DownloadInfo).FileName); }
                        else if (ctrl) { targets.Add((item as DownloadInfo).FileName); }
                        else targets.Add((item as DownloadInfo).FileName.ParseLink().ParseID());
                    }
                }
                Commands.CopyArtworkIDs.Execute(targets);
            }).InvokeAsync(true);
        }

        private async void PART_CopyInfo_Click(object sender, RoutedEventArgs e)
        {
            await new Action(() =>
            {
                Commands.CopyDownloadInfo.Execute(GetDownloadInfo());
            }).InvokeAsync(true);
        }

        private async void PART_Compare_Click(object sender, RoutedEventArgs e)
        {
            await new Action(() =>
            {
                if (DownloadItems.SelectedItems is IEnumerable && DownloadItems.SelectedItems.Count > 1)
                {
                    try
                    {
                        var items =  DownloadItems.SelectedItems.Cast<DownloadInfo>().Where(i => i.State == DownloadState.Finished).Select(i => i.FileName).Take(2).ToList();
                        Commands.Compare.Execute(items);
                    }
                    catch (Exception ex) { ex.ERROR("Compare"); }
                }
            }).InvokeAsync(true);

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
                        var needUpdate = targets.Where(item => item.State != DownloadState.Downloading && item.State != DownloadState.Finished && item.State != DownloadState.NonExists);
                        if (needUpdate.Count() > 0)
                        {
                            var opt = new ParallelOptions();
                            opt.MaxDegreeOfParallelism = (int)SimultaneousJobs;
                            var ret = Parallel.ForEach(needUpdate, opt, (item, loopstate, elementIndex) =>
                            {
                                item.State = DownloadState.Idle;
                            });
                        }
                    }
                    else
                    {
                        var needUpdate = items.Where(item => item.State != DownloadState.Downloading && item.State != DownloadState.Finished && item.State != DownloadState.NonExists);
                        if (needUpdate.Count() > 0)
                        {
                            var opt = new ParallelOptions();
                            opt.MaxDegreeOfParallelism = (int)SimultaneousJobs;
                            var ret = Parallel.ForEach(needUpdate, opt, (item, loopstate, elementIndex) =>
                            {
                                item.State = DownloadState.Idle;
                            });
                        }
                    }
                }).InvokeAsync();
            }
            catch (Exception ex) { ex.ERROR(); }
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
                        if (Commands.ParallelExecutionConfirm(remove))
                        {
                            foreach (var i in remove) { i.State = DownloadState.Remove; }
                        }
                    }
                    else
                    {
                        setting = Application.Current.LoadSetting();
                        var non_exists = items.Where(o => o.State == DownloadState.NonExists);
                        if (non_exists.Count() > 0)
                        {
                            foreach (var i in non_exists) { i.State = DownloadState.Remove; }
                        }
                        else if (GetOlderItems(setting.DownloadRemoveNDays).Count() > 0) PART_RemoveAll_Context_Click(PART_RemoveAll_NDays, e);
                        else if (GetOlderItems().Count() > 0) PART_RemoveAll_Context_Click(PART_RemoveAll_Old, e);
                        else
                        {
                            var remove = items.Where(o => o.State != DownloadState.Downloading);
                            if (Commands.ParallelExecutionConfirm(remove))
                            {
                                foreach (var i in remove) { i.State = DownloadState.Remove; }
                            }
                        }
                    }
                }).InvokeAsync();
            }
            catch (Exception ex) { ex.ERROR("DOWNLOADMANAGER"); }
        }

        private async void PART_RemoveAll_Context_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DownloadState state = DownloadState.Unknown;
                if (sender == PART_RemoveAll_NonExists)
                {
                    state = DownloadState.NonExists;
                }
                else if (sender == PART_RemoveAll_Failed)
                {
                    state = DownloadState.Failed;
                }
                else if (sender == PART_RemoveAll_Finished)
                {
                    state = DownloadState.Finished;
                }
                else if (sender == PART_RemoveAll_Idle)
                {
                    state = DownloadState.Idle;
                }
                else if (sender == PART_RemoveAll_Old)
                {
                    state = DownloadState.Older;
                }
                else if (sender == PART_RemoveAll_NDays)
                {
                    state = DownloadState.NDays;
                }
                else if (sender == PART_RemoveAll_All)
                {

                }
                await new Action(() =>
                {
                    var targets = new List<DownloadInfo>();
                    foreach (var item in (DownloadItems.SelectedItems is IEnumerable && DownloadItems.SelectedItems.Count > 1 ? DownloadItems.SelectedItems : items))
                    {
                        if (item is DownloadInfo) targets.Add(item as DownloadInfo);
                    }

                    if (state == DownloadState.Older)
                    {
                        targets = GetOlderItems().ToList();
                        state = DownloadState.Finished;
                    }
                    else if (state == DownloadState.NDays)
                    {
                        setting = Application.Current.LoadSetting();
                        targets = GetOlderItems(setting.DownloadRemoveNDays).ToList();
                        state = DownloadState.Finished;
                    }
                    $"Total {targets.Count} item(s) will be removed!".DEBUG("DOWNLOADMANAGER");

                    if (targets.Count > 0)
                    {
                        var remove = state == DownloadState.Unknown ? targets : targets.Where(o => o.State == state);
                        if (state == DownloadState.NonExists || Commands.ParallelExecutionConfirm(remove))
                            foreach (var i in remove) { i.State = DownloadState.Remove; }
                    }
                }).InvokeAsync();
            }
            catch (Exception ex) { ex.ERROR("DOWNLOADMANAGER"); }
        }

        private void PART_RemoveAll_ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            setting = Application.Current.LoadSetting();
            PART_RemoveAll_NDays.Header = $"Remove Before {setting.DownloadRemoveNDays} Days";
        }

    }
}
