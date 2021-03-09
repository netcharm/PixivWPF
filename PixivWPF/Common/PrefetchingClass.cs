using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PixivWPF.Common
{
    class PrefetchingClass : IDisposable
    {
        private ConcurrentDictionary<string, bool> PrefetchedList = new ConcurrentDictionary<string, bool>();
        private CancellationTokenSource PrefetchingTaskCancelTokenSource = new CancellationTokenSource();
        private SemaphoreSlim CanPrefetching = new SemaphoreSlim(1, 1);
        private BackgroundWorker PrefetchingTask = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };

        public IList<PixivItem> Items { get; set; } = new List<PixivItem>();

        private double percent = 0;
        public double Percentage { get { return (percent); } }

        private string info = string.Empty;
        public string Description { get { return (info); } }

        private TaskStatus state = TaskStatus.Created;
        public TaskStatus State { get { return (state); } }

        public Action<double, string, TaskStatus> ReportProgress { get; set; } = null;
        public Action ReportProgressSlim { get; set; } = null;

        public string Name { get; set; } = "PixivItems";

        private int CalcPagesThumbItems(IEnumerable<PixivItem> items)
        {
            int result = 0;
            var setting = Application.Current.LoadSetting();
            foreach (var item in items)
            {
                if (item.Count > 1) result += setting.PrefetchingPagesPreview ? item.Count * 2 : item.Count;
            }
            return (result);
        }

        private async Task<List<string>> GetPagesThumbItems(PixivItem item)
        {
            List<string> pages = new List<string>();
            try
            {
                var setting = Application.Current.LoadSetting();
                if (setting.PrefetchingPagesThumb)
                {
                    var illust = item.Illust;
                    if (illust is Pixeez.Objects.Work && illust.PageCount > 1)
                    {
                        if (illust is Pixeez.Objects.IllustWork)
                        {
                            var subset = illust as Pixeez.Objects.IllustWork;
                            if (subset.meta_pages.Count() > 1)
                            {
                                foreach (var page in subset.meta_pages)
                                {
                                    pages.Add(page.GetThumbnailUrl());
                                }
                            }
                        }
                        else if (illust is Pixeez.Objects.NormalWork)
                        {
                            var subset = illust as Pixeez.Objects.NormalWork;
                            if (subset.PageCount >= 1 && subset.Metadata == null)
                            {
                                illust = await illust.RefreshIllust();
                            }
                            if (illust != null && illust.Metadata is Pixeez.Objects.Metadata)
                            {
                                item.Illust = illust;
                                foreach (var page in illust.Metadata.Pages)
                                {
                                    pages.Add(page.GetThumbnailUrl());
                                }
                            }
                        }
                        pages = pages.Distinct().ToList();
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("PAGESCOUNTING"); }
            return (pages);
        }

        private async Task<List<string>> GetPagesPreviewItems(PixivItem item)
        {
            List<string> pages = new List<string>();
            try
            {
                var setting = Application.Current.LoadSetting();
                if (setting.PrefetchingPagesPreview)
                {
                    var illust = item.Illust;
                    if (illust is Pixeez.Objects.Work && illust.PageCount > 1)
                    {
                        if (illust is Pixeez.Objects.IllustWork)
                        {
                            var subset = illust as Pixeez.Objects.IllustWork;
                            if (subset.meta_pages.Count() > 1)
                            {
                                foreach (var page in subset.meta_pages)
                                {
                                    pages.Add(page.GetPreviewUrl());
                                }
                            }
                        }
                        else if (illust is Pixeez.Objects.NormalWork)
                        {
                            var subset = illust as Pixeez.Objects.NormalWork;
                            if (subset.PageCount >= 1 && subset.Metadata == null)
                            {
                                illust = await illust.RefreshIllust();
                            }
                            if (illust != null && illust.Metadata is Pixeez.Objects.Metadata)
                            {
                                item.Illust = illust;
                                foreach (var page in illust.Metadata.Pages)
                                {
                                    pages.Add(page.GetPreviewUrl());
                                }
                            }
                        }
                        pages = pages.Distinct().ToList();
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("PAGESCOUNTING"); }
            return (pages);
        }

        private bool GetPreviewItems(List<string> illusts, List<string> avatars, List<string> page_thumbs, List<string> page_previews)
        {
            bool result = false;
            try
            {
                var setting = Application.Current.LoadSetting();
                if (setting.PrefetchingPreview && Items.Count > 0)
                {
                    new Action(async () =>
                    {
                        setting = Application.Current.LoadSetting();
                        var items = Items.Reverse().ToList();
                        foreach (var item in items)
                        {
                            if (item.IsWork())
                            {
                                var preview = item.Illust.GetPreviewUrl();
                                if (!illusts.Contains(preview)) illusts.Add(preview);
                                var avatar = item.Illust.GetAvatarUrl();
                                if(!avatars.Contains(avatar)) avatars.Add(avatar);

                                if (setting.PrefetchingPagesThumb && item.Count > 1 && page_thumbs is List<string>)
                                {
                                    var count = page_thumbs.Count;
                                    var p_count = item.Count;
                                    page_thumbs.AddRange(await GetPagesThumbItems(item));
                                    if (page_thumbs.Count - count != p_count)
                                        Task.Delay(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                                    this.DoEvents();
                                }

                                if (setting.PrefetchingPagesPreview && item.Count > 1 && page_previews is List<string>)
                                {
                                    var count = page_previews.Count;
                                    var p_count = item.Count;
                                    page_previews.AddRange(await GetPagesPreviewItems(item));
                                    if (page_previews.Count - count != p_count)
                                        Task.Delay(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                                    this.DoEvents();
                                }
                            }
                        }
                        result = true;
                    }).Invoke(async: false);
                }
            }
            catch (Exception ex) { ex.ERROR("PREVIEWCOUNTING"); }
            return (result);
        }

        private void PrefetchingTask_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (Items.Count > 0)
            {
                //state = TaskStatus.RanToCompletion;
                //if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                //else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(percent, info, TaskStatus.RanToCompletion);
                if (CanPrefetching is SemaphoreSlim && CanPrefetching.CurrentCount < 1) CanPrefetching.Release();
            }
        }

        private void PrefetchingTask_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (Items.Count > 0)
            {
                if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(percent, info, state);
                if (CanPrefetching is SemaphoreSlim && CanPrefetching.CurrentCount < 1) CanPrefetching.Release();
            }
        }

        private void PrefetchingTask_DoWork(object sender, DoWorkEventArgs e)
        {
            var setting = Application.Current.LoadSetting();
            try
            {
                if (!setting.PrefetchingPreview) return;

                this.DoEvents();
                List<string> illusts = new List<string>();
                List<string> avatars = new List<string>();
                List<string> page_thumbs = new List<string>();
                List<string> page_previews = new List<string>();
                var pagesCount = CalcPagesThumbItems(Items);
                GetPreviewItems(illusts, avatars, page_thumbs, page_previews);
                if (pagesCount != page_thumbs.Count + page_previews.Count) { e.Cancel = true; return; }

                var total = illusts.Count + avatars.Count + page_thumbs.Count + page_previews.Count;
                if (total <= 0) { e.Cancel = true; return; }
                var count = total;
                percent = count == 0 ? 100 : 0;
                info = $"Calculating [ {count} / {total}, {illusts.Count} / {avatars.Count} / {page_thumbs.Count} / {page_previews.Count} ]";
                state = TaskStatus.WaitingToRun;
                if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(percent, info, state);

                var parallel = setting.PrefetchingDownloadParallel;
                if (setting.ParallelPrefetching)
                {
                    List<string> needUpdate = new List<string>();
                    needUpdate.AddRange(illusts);
                    needUpdate.AddRange(avatars);
                    needUpdate.AddRange(page_thumbs);
                    needUpdate.AddRange(page_previews);

                    foreach (var url in needUpdate.Where(url => File.Exists(url.GetImageCacheFile())))
                    {
                        PrefetchedList.AddOrUpdate(url, true, (k, v) => true);
                        //if (!PrefetchedList.TryAdd(url, true)) PrefetchedList.TryUpdate(url, true, false);
                    }
                    needUpdate = needUpdate.Where(url => !PrefetchedList.ContainsKey(url) || !PrefetchedList[url]).ToList();
                    count = needUpdate.Count;
                    percent = count == 0 ? 100 : (total - count) / (double)total * 100;
                    info = $"Prefetching [ {count} / {total}, {illusts.Count} / {avatars.Count} / {page_thumbs.Count} / {page_previews.Count}]";
                    state = TaskStatus.Running;
                    if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                    else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(percent, info, state);
                    this.DoEvents();

                    var opt = new ParallelOptions();
                    opt.MaxDegreeOfParallelism = parallel;
                    Parallel.ForEach(needUpdate, opt, (url, loopstate, urlIndex) =>
                    {
                        try
                        {
                            var file = url.GetImageCacheFile();
                            if (!string.IsNullOrEmpty(file))
                            {
                                if (File.Exists(file))
                                {
                                    PrefetchedList.AddOrUpdate(url, true, (k, v) => true);
                                    //if (!PrefetchedList.TryAdd(url, true)) PrefetchedList.TryUpdate(url, true, false);
                                    count = count - 1;
                                }
                                else
                                {
                                    file = url.DownloadCacheFile().GetAwaiter().GetResult();
                                    if (!string.IsNullOrEmpty(file))
                                    {
                                        PrefetchedList.AddOrUpdate(url, true, (k, v) => true);
                                        //if (!PrefetchedList.TryAdd(url, true)) PrefetchedList.TryUpdate(url, true, false);
                                        count = count - 1;
                                    }
                                }
                            }
                            if (PrefetchingTask.CancellationPending) { e.Cancel = true; loopstate.Stop(); }
                            percent = count == 0 ? 100 : (total - count) / (double)total * 100;
                            info = $"Prefetching [ {count} / {total}, {illusts.Count} / {avatars.Count} / {page_thumbs.Count} / {page_previews.Count}]";
                            state = TaskStatus.Running;
                            if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                            else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(percent, info, state);
                            this.DoEvents();
                        }
                        catch (Exception ex) { ex.ERROR("PREFETCHING"); }
                        finally { this.DoEvents(); Task.Delay(1).GetAwaiter().GetResult(); }
                    });
                }
                else
                {
                    SemaphoreSlim tasks = new SemaphoreSlim(parallel, parallel);
                    foreach (var urls in new List<string>[] { illusts, avatars, page_thumbs, page_previews })
                    {
                        if (PrefetchingTask.CancellationPending) { e.Cancel = true; break; }
                        foreach (var url in urls)
                        {
                            if (PrefetchingTask.CancellationPending) { e.Cancel = true; break; }
                            if (tasks.Wait(-1, PrefetchingTaskCancelTokenSource.Token))
                            {
                                new Action(async () =>
                                {
                                    try
                                    {
                                        var file = url.GetImageCacheFile();
                                        if (!string.IsNullOrEmpty(file))
                                        {
                                            if (File.Exists(file))
                                            {
                                                PrefetchedList.AddOrUpdate(url, true, (k, v) => true);
                                                //if (!PrefetchedList.TryAdd(url, true)) PrefetchedList.TryUpdate(url, true, false);
                                                count = count - 1;
                                            }
                                            else
                                            {
                                                file = await url.DownloadCacheFile();
                                                if (!string.IsNullOrEmpty(file))
                                                {
                                                    PrefetchedList.AddOrUpdate(url, true, (k, v) => true);
                                                    //if (!PrefetchedList.TryAdd(url, true)) PrefetchedList.TryUpdate(url, true, false);
                                                    count = count - 1;
                                                }
                                            }
                                        }
                                        if (PrefetchingTask.CancellationPending) { e.Cancel = true; return; }
                                        percent = count == 0 ? 100 : (total - count) / (double)total * 100;
                                        info = $"Prefetching [ {count} / {total}, {illusts.Count} / {avatars.Count} / {page_thumbs.Count} / {page_previews.Count} ]";
                                        state = TaskStatus.Running;
                                        if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                                        else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(percent, info, state);
                                        //await Task.Delay(10);
                                        this.DoEvents();
                                    }
                                    catch (Exception ex) { ex.ERROR("PREFETCHING"); }
                                    finally { if (tasks is SemaphoreSlim && tasks.CurrentCount <= parallel) tasks.Release(); this.DoEvents(); await Task.Delay(1); }
                                }).Invoke(async: false);
                            }
                        }
                        this.DoEvents();
                    }
                    this.DoEvents();
                }

                var name = e.Argument is string ? (string)e.Argument : string.Empty;
                if (PrefetchingTask.CancellationPending) { e.Cancel = true; return; }
                if (count >= 0 && total > 0)
                {
                    percent = count == 0 ? 100 : (total - count) / (double)total * 100;
                    info = $"Done [ {count} / {total}, {illusts.Count} / {avatars.Count} / {page_thumbs.Count} / {page_previews.Count} ]";
                    state = TaskStatus.RanToCompletion;
                    if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                    else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(percent, info, state);
                    this.DoEvents();
                    try
                    {
                        illusts.Clear();
                        avatars.Clear();
                        page_thumbs.Clear();
                        page_previews.Clear();
                    }
                    catch (Exception ex) { ex.ERROR("PREFETCHED"); }
                    $"Prefetching Previews, Avatars, Thumbnails : {Environment.NewLine}  {Description}".ShowToast("INFO", tag: name ?? GetType().Name);
                }
            }
            catch (Exception ex)
            {
                ex.ERROR("PREFETCHING");
                info = $"Failed {info}";
                state = TaskStatus.Faulted;
                if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(percent, info, state);
            }
            finally
            {
                if (CanPrefetching is SemaphoreSlim && CanPrefetching.CurrentCount < 1) CanPrefetching.Release();
            }
        }

        public void Start(IEnumerable<PixivItem> items, bool waitcancel = true)
        {
            Stop();
            if (items is IEnumerable<PixivItem>) { Items = items.ToList(); }
            Start(waitcancel);
        }

        public async void Start(bool waitcancel = true)
        {
            var setting = Application.Current.LoadSetting();
            if (!setting.PrefetchingPreview) return;
            Stop();
            if (waitcancel && await CanPrefetching.WaitAsync(-1))
            {
                if (!PrefetchingTask.IsBusy && !PrefetchingTask.CancellationPending)
                {
                    new Action(() =>
                    {
                        PrefetchingTaskCancelTokenSource = new CancellationTokenSource();
                        PrefetchingTask.RunWorkerAsync(this.Name ?? "PixivItems");
                    }).Invoke(async: false);
                }
            }
        }

        public void Stop()
        {
            if (PrefetchingTask.IsBusy || PrefetchingTask.CancellationPending)
            {
                PrefetchingTask.CancelAsync();
                if (PrefetchingTaskCancelTokenSource is CancellationTokenSource) PrefetchingTaskCancelTokenSource.Cancel();
            }
            if (!PrefetchingTask.IsBusy && !PrefetchingTask.CancellationPending && CanPrefetching is SemaphoreSlim && CanPrefetching.CurrentCount < 1) CanPrefetching.Release();
        }

        public PrefetchingClass()
        {
            if (PrefetchingTask == null) new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            if (PrefetchingTask is BackgroundWorker)
            {
                PrefetchingTask.WorkerReportsProgress = true;
                PrefetchingTask.WorkerSupportsCancellation = true;
                PrefetchingTask.RunWorkerCompleted += PrefetchingTask_RunWorkerCompleted;
                PrefetchingTask.ProgressChanged += PrefetchingTask_ProgressChanged;
                PrefetchingTask.DoWork += PrefetchingTask_DoWork;
            }
        }

        public PrefetchingClass(Action<double, string, TaskStatus> ReportAction = null)
        {
            if (PrefetchingTask == null) new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            if (PrefetchingTask is BackgroundWorker)
            {
                PrefetchingTask.WorkerReportsProgress = true;
                PrefetchingTask.WorkerSupportsCancellation = true;
                PrefetchingTask.RunWorkerCompleted += PrefetchingTask_RunWorkerCompleted;
                PrefetchingTask.ProgressChanged += PrefetchingTask_ProgressChanged;
                PrefetchingTask.DoWork += PrefetchingTask_DoWork;
                ReportProgress = ReportAction;
            }
        }

        public PrefetchingClass(Action ReportAction = null)
        {
            if (PrefetchingTask == null) new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            if (PrefetchingTask is BackgroundWorker)
            {
                PrefetchingTask.WorkerReportsProgress = true;
                PrefetchingTask.WorkerSupportsCancellation = true;
                PrefetchingTask.RunWorkerCompleted += PrefetchingTask_RunWorkerCompleted;
                PrefetchingTask.ProgressChanged += PrefetchingTask_ProgressChanged;
                PrefetchingTask.DoWork += PrefetchingTask_DoWork;
                ReportProgressSlim = ReportAction;
            }
        }

        public void Dispose()
        {
            Stop();
            if (CanPrefetching is SemaphoreSlim && CanPrefetching.CurrentCount < 1) CanPrefetching.Release();
            PrefetchingTask.Dispose();
            PrefetchedList.Clear();
            Items.Clear();
        }
    }
}
