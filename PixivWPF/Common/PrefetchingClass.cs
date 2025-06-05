using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PixivWPF.Common
{
    class PrefetchingOpts
    {
        public string Name { get; set; } = string.Empty;
        public int PrefetchingDownloadParallel { get; set; } = 5;
        public bool ParallelPrefetching { get; set; } = true;
        public bool PrefetchingPreview { get; set; } = true;
        public bool IncludePageThumb { get; set; } = true;
        public bool IncludePagePreview { get; set; } = true;
        public bool Overwrite { get; set; } = false;
        public bool ReverseOrder { get; set; } = false;
    }

    class PrefetchingTask : IDisposable
    {
        private CancellationTokenSource PrefetchingTaskCancelTokenSource = new CancellationTokenSource();
        private readonly SemaphoreSlim CanPrefetching = new SemaphoreSlim(1, 1);
        private BackgroundWorker PrefetchingBgWorker = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
        private PrefetchingOpts Options { get; set; } = new PrefetchingOpts();

        public string Name { get; set; } = "PixivItems";
        public DateTime LastStartTime { get; private set; } = DateTime.Now;
        public DateTime LastDoneTime { get; private set; } = DateTime.Now;
        public ConcurrentDictionary<string, bool> PrefetchedList { get; private set; } = new ConcurrentDictionary<string, bool>();

        private IList<PixivItem> _Items_ = new List<PixivItem>();
        public IList<PixivItem> Items
        {
            get { if (!(_Items_ is IList<PixivItem>)) _Items_ = new List<PixivItem>(); return (_Items_); }
            set { DownloadProgressActions.Clear(); _Items_ = value; }
        }

        public int Unfinished { get; private set; } = 0;
        public int Total { get { return (Items is IList<PixivItem> ? Items.Count : 0); } }
        public double Percentage { get; private set; } = 0;
        public string Comments { get; private set; } = string.Empty;
        public TaskStatus State { get; private set; } = TaskStatus.Created;
        public bool IsBusy { get { return (PrefetchingBgWorker is BackgroundWorker && (PrefetchingBgWorker.IsBusy || PrefetchingBgWorker.CancellationPending)); } }

        private ConcurrentDictionary<string, Action<double, double>> _DownloadProgressActions_ = new ConcurrentDictionary<string, Action<double, double>>();
        public ConcurrentDictionary<string, Action<double, double>> DownloadProgressActions
        {
            get
            {
                if (!(_DownloadProgressActions_ is ConcurrentDictionary<string, Action<double, double>>))
                    _DownloadProgressActions_ = new ConcurrentDictionary<string, Action<double, double>>();
                return (_DownloadProgressActions_);
            }
        }

        public Action<double, string, TaskStatus> ReportProgress { get; set; } = null;
        public Action ReportProgressSlim { get; set; } = null;

        private int CalcPagesThumbItems(IEnumerable<PixivItem> items)
        {
            int result = 0;
            foreach (var item in items)
            {
                if (item.IsPage() || item.IsPages())
                    result += Options.IncludePagePreview ? 2 : 1;
                else if (item.IsWork() && item.Count > 1)
                    result += Options.IncludePagePreview ? item.Count * 2 : item.Count;
            }
            return (result);
        }

        private async Task<List<string>> GetPagesThumbItems(PixivItem item)
        {
            List<string> pages = new List<string>();
            try
            {
                if (Options.IncludePageThumb)
                {
                    if (item.IsPage() || item.IsPages())
                    {
                        pages.Add(item.Illust.GetThumbnailUrl(item.Index));
                    }
                    else if (item.IsWork())
                    {
                        var illust = item.Illust;
                        if (illust is Pixeez.Objects.Work && (illust.PageCount ?? 0) > 1)
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
                                if ((subset.PageCount ?? 0) >= 1 && subset.Metadata == null)
                                {
                                    subset.Metadata = await subset.GetMetaData();
                                    //illust = await illust.RefreshIllust();
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
            }
            catch (Exception ex) { ex.ERROR("PAGESCOUNTING"); }
            return (pages);
        }

        private async Task<List<string>> GetPagesPreviewItems(PixivItem item)
        {
            List<string> pages = new List<string>();
            try
            {
                if (Options.IncludePagePreview)
                {
                    if (item.IsPage() || item.IsPages())
                    {
                        var setting = Application.Current.LoadSetting();
                        pages.Add(item.Illust.GetPreviewUrl(item.Index, large: setting.ShowLargePreview));
                    }
                    else if (item.IsWork())
                    {
                        var illust = item.Illust;
                        if (illust is Pixeez.Objects.Work && (illust.PageCount ?? 0) > 1)
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
                                if ((subset.PageCount ?? 0) >= 1 && subset.Metadata == null)
                                {
                                    subset.Metadata = await subset.GetMetaData();
                                    //illust = await illust.RefreshIllust();
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
            }
            catch (Exception ex) { ex.ERROR("PAGESCOUNTING"); }
            return (pages);
        }

        private async Task<List<string>> GetPagesOriginalItems(PixivItem item)
        {
            List<string> pages = new List<string>();
            try
            {
                if (Options.IncludePagePreview)
                {
                    if (item.IsPage() || item.IsPages())
                    {
                        var setting = Application.Current.LoadSetting();
                        pages.Add(item.Illust.GetOriginalUrl(item.Index));
                    }
                    else if (item.IsWork())
                    {
                        var illust = item.Illust;
                        if (illust is Pixeez.Objects.Work && (illust.PageCount ?? 0) > 1)
                        {
                            if (illust is Pixeez.Objects.IllustWork)
                            {
                                var subset = illust as Pixeez.Objects.IllustWork;
                                if (subset.meta_pages.Count() > 1)
                                {
                                    foreach (var page in subset.meta_pages)
                                    {
                                        pages.Add(page.GetOriginalUrl());
                                    }
                                }
                            }
                            else if (illust is Pixeez.Objects.NormalWork)
                            {
                                var subset = illust as Pixeez.Objects.NormalWork;
                                if ((subset.PageCount ?? 0) >= 1 && subset.Metadata == null)
                                {
                                    subset.Metadata = await subset.GetMetaData();
                                    //illust = await illust.RefreshIllust();
                                }
                                if (illust != null && illust.Metadata is Pixeez.Objects.Metadata)
                                {
                                    item.Illust = illust;
                                    foreach (var page in illust.Metadata.Pages)
                                    {
                                        pages.Add(page.GetOriginalUrl());
                                    }
                                }
                            }
                            pages = pages.Distinct().ToList();
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("PAGESCOUNTING"); }
            return (pages);
        }

        private bool GetPreviewItems(List<string> illusts, List<string> avatars, List<string> page_thumbs, List<string> page_previews, List<string> originals)
        {
            bool result = false;
            try
            {
                if (Options.PrefetchingPreview && Items.Count > 0)
                {
                    var setting = Application.Current.LoadSetting();
                    new Action(async () =>
                    {
                        var items = Items.ToList();
                        foreach (var item in items)
                        {
                            if (item.IsPage() || item.IsPages())
                            {
                                originals.Add(item.Illust.GetOriginalUrl(item.Index));
                                page_thumbs.Add(item.Illust.GetThumbnailUrl(item.Index, large: setting.ShowLargePreview));
                                page_previews.Add(item.Illust.GetPreviewUrl(item.Index, large: setting.ShowLargePreview));
                            }
                            else if (item.IsWork())
                            {
                                originals.Add(item.Illust.GetOriginalUrl(item.Index));
                                if (item.Count > 1) originals.AddRange(await GetPagesOriginalItems(item));

                                var preview = item.Illust.GetPreviewUrl(large: setting.ShowLargePreview);
                                if (!illusts.Contains(preview)) illusts.Add(preview);
                                var avatar = item.Illust.GetAvatarUrl();
                                if (!avatars.Contains(avatar)) avatars.Add(avatar);

                                if (Options.IncludePageThumb && item.Count > 1 && page_thumbs is List<string>)
                                {
                                    var count = page_thumbs.Count;
                                    var p_count = item.Count;
                                    page_thumbs.AddRange(await GetPagesThumbItems(item));
                                    if (page_thumbs.Count - count != p_count)
                                        Task.Delay(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                                    this.DoEvents();
                                }

                                if (Options.IncludePagePreview && item.Count > 1 && page_previews is List<string>)
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

        private bool GetOriginalImageSize(List<string> originals, DoWorkEventArgs e)
        {
            var result = false;

            var setting = Application.Current.LoadSetting();
            if (setting.QueryOriginalImageSize)
            {
                var args = e.Argument is PrefetchingOpts ? e.Argument as PrefetchingOpts : new PrefetchingOpts();
                if (!args.PrefetchingPreview) return (result);

                State = TaskStatus.WaitingForChildrenToComplete;

                var comments = Comments.Substring(0, Comments.IndexOf("]")+1);
                var count = originals.Count;
                bool paralllel = args.ParallelPrefetching;
                var parallels = args.PrefetchingDownloadParallel;
                try
                {
                    if (paralllel)
                    {
                        var opt = new ParallelOptions
                        {
                            MaxDegreeOfParallelism = parallels
                        };
                        if (PrefetchingTaskCancelTokenSource is CancellationTokenSource) opt.CancellationToken = PrefetchingTaskCancelTokenSource.Token;
                        Parallel.ForEach(originals, opt, (url, loopstate, urlIndex) =>
                        {
                            try
                            {
                                var size = url.QueryImageFileSize(cancelToken: PrefetchingTaskCancelTokenSource).GetAwaiter().GetResult();
                                if (size > 0)
                                {
                                    Comments = comments.Replace("]", $"] [ Q: {--count} / {originals.Count} ]");
                                    if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                                    else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke((double)Percentage, Comments, State);
                                    this.DoEvents();
                                }
                            }
                            catch (Exception ex) { ex.ERROR("PREFETCHING"); }
                            finally { this.DoEvents(); Task.Delay(1).GetAwaiter().GetResult(); }
                        });
                    }
                    else
                    {
                        SemaphoreSlim tasks = new SemaphoreSlim(parallels, parallels);
                        foreach (var url in originals)
                        {
                            if (PrefetchingBgWorker.CancellationPending) { e.Cancel = true; break; }
                            if (PrefetchingTaskCancelTokenSource.IsCancellationRequested) { e.Cancel = true; break; }
                            if (tasks.Wait(-1, PrefetchingTaskCancelTokenSource.Token))
                            {
                                new Action(async () =>
                                {
                                    try
                                    {
                                        var size = await url.QueryImageFileSize(cancelToken: PrefetchingTaskCancelTokenSource);
                                        if (size > 0)
                                        {
                                            Comments = comments.Replace("]", $"] [ Q: {--count} / {originals.Count} ]");
                                            if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                                            else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke((double)Percentage, Comments, State);
                                            this.DoEvents();
                                        }
                                    }
                                    catch (Exception ex) { ex.ERROR("PREFETCHING"); }
                                    finally { if (tasks is SemaphoreSlim && tasks.CurrentCount <= parallels) tasks.Release(); this.DoEvents(); await Task.Delay(1); }
                                }).Invoke(async: false);
                            }
                        }
                    }
                }
                catch (Exception ex) { ex.ERROR("GetOriginalImageSize"); }
                $"Query Original Imagee File Size : {Environment.NewLine}  Done [ {originals.Count} ]".ShowToast("INFO", tag: args.Name ?? Name ?? GetType().Name);
                State = count <= 0 ? TaskStatus.RanToCompletion : TaskStatus.Faulted;
                if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(Percentage, Comments, State);
                setting.SaveImageFileSizeData();
                result = true;
            }
            return (result);
        }

        private void PrefetchingTask_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (Items.Count > 0)
            {
                if (State == TaskStatus.Faulted || State == TaskStatus.Canceled)
                {
                    if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                    else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(Percentage, Comments, State);
                }
                //Application.Current.MergeToSystemPrefetchedList(PrefetchedList);
            }
            if (CanPrefetching is SemaphoreSlim && CanPrefetching.CurrentCount < 1) CanPrefetching.Release();
        }

        private void PrefetchingTask_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (Items.Count > 0)
            {
                if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(Percentage, Comments, State);
            }
        }

        private void PrefetchingTask_DoWork(object sender, DoWorkEventArgs e)
        {
            List<string> illusts = new List<string>();
            List<string> originals = new List<string>();
            List<string> avatars = new List<string>();
            List<string> page_thumbs = new List<string>();
            List<string> page_previews = new List<string>();
            List<string> needUpdate = new List<string>();
            try
            {
                var args = e.Argument is PrefetchingOpts ? e.Argument as PrefetchingOpts : new PrefetchingOpts();
                if (!args.PrefetchingPreview) return;

                LastStartTime = DateTime.Now;
                var pagesCount = CalcPagesThumbItems(Items);
                GetPreviewItems(illusts, avatars, page_thumbs, page_previews, originals);
                if (pagesCount != page_thumbs.Count + page_previews.Count) { e.Cancel = true; return; }

                var total = illusts.Count + avatars.Count + page_thumbs.Count + page_previews.Count;
                if (total <= 0) { e.Cancel = true; return; }
                var count = total;
                Percentage = count == 0 ? 100 : 0;
                Comments = $"Calculating [ {count} / {total}, I:{illusts.Count} / A:{avatars.Count} / T:{page_thumbs.Count} / P:{page_previews.Count} ]";
                State = TaskStatus.WaitingToRun;
                if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(Percentage, Comments, State);

                needUpdate.AddRange(args.ReverseOrder ? illusts.Reverse<string>() : illusts);
                needUpdate.AddRange(args.ReverseOrder ? avatars.Reverse<string>() : avatars);
                needUpdate.AddRange(args.ReverseOrder ? page_thumbs.Reverse<string>() : page_thumbs);
                needUpdate.AddRange(args.ReverseOrder ? page_previews.Reverse<string>() : page_previews);
                foreach (var url in needUpdate.Where(url => !string.IsNullOrEmpty(url) && !PrefetchedList.ContainsKey(url) && File.Exists(url.GetImageCachePath())))
                {
                    PrefetchedList.AddOrUpdate(url, true, (k, v) => true);
                    //if (!PrefetchedList.TryAdd(url, true)) PrefetchedList.TryUpdate(url, true, false);
                }
                needUpdate = needUpdate.Where(url => !string.IsNullOrEmpty(url) && (!PrefetchedList.ContainsKey(url) || !PrefetchedList[url])).ToList();
                count = needUpdate.Count;
                Percentage = count == 0 ? 100 : (total - count) / (double)total * 100;
                if (count == 0)
                {
                    Comments = $"Done [ {count} / {total}, I:{illusts.Count} / A:{avatars.Count} / T:{page_thumbs.Count} / P:{page_previews.Count} ]";
                    State = TaskStatus.RanToCompletion;
                }
                else
                {
                    Comments = $"Prefetching [ {count} / {total}, I:{illusts.Count} / A:{avatars.Count} / T:{page_thumbs.Count} / P:{page_previews.Count} ]";
                    State = TaskStatus.Running;
                }
                if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(Percentage, Comments, State);
                this.DoEvents();
                if (count == 0) return;

                var parallels = args.PrefetchingDownloadParallel;
                if (args.ParallelPrefetching)
                {
                    #region using Paralle.Foreach
                    var opt = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = parallels
                    };
                    if (PrefetchingTaskCancelTokenSource is CancellationTokenSource) opt.CancellationToken = PrefetchingTaskCancelTokenSource.Token;
                    Parallel.ForEach(needUpdate, opt, (url, loopstate, urlIndex) =>
                    {
                        try
                        {
                            var file = url.GetImageCachePath();
                            if (!string.IsNullOrEmpty(file))
                            {
                                if (File.Exists(file))
                                {
                                    PrefetchedList.AddOrUpdate(url, true, (k, v) => true);
                                    //if (!PrefetchedList.TryAdd(url, true)) PrefetchedList.TryUpdate(url, true, false);
                                    count--;
                                }
                                else
                                {
                                    var _downReport = DownloadProgressActions.ContainsKey(url) ? DownloadProgressActions[url] : null;
                                    var f_ret = url.DownloadCacheFile(args.Overwrite, progressAction: _downReport, cancelToken: PrefetchingTaskCancelTokenSource).GetAwaiter().GetResult();
                                    if (!string.IsNullOrEmpty(f_ret))
                                    {
                                        PrefetchedList.AddOrUpdate(url, true, (k, v) => true);
                                        //if (!PrefetchedList.TryAdd(url, true)) PrefetchedList.TryUpdate(url, true, false);
                                        count--;
                                    }
                                    else file.CleenLastDownloaded();
                                }
                            }
                            if (PrefetchingBgWorker != null && PrefetchingBgWorker.CancellationPending) { e.Cancel = true; State = TaskStatus.Canceled; loopstate?.Stop(); }
                            Percentage = count == 0 ? 100 : (total - count) / (double)total * 100;
                            Comments = $"Prefetching [ {count} / {total}, I:{illusts.Count} / A:{avatars.Count} / T:{page_thumbs.Count} / P:{page_previews.Count} ]";
                            State = TaskStatus.Running;
                            if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                            else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke((double)this.Percentage, Comments, State);
                            this.DoEvents();
                        }
                        catch (Exception ex) { ex.ERROR("PREFETCHING"); }
                        finally { this.DoEvents(); Task.Delay(1).GetAwaiter().GetResult(); }
                    });
                    #endregion
                }
                else
                {
                    #region using task & action 
                    SemaphoreSlim tasks = new SemaphoreSlim(parallels, parallels);
                    foreach (var url in needUpdate)
                    {
                        if (PrefetchingBgWorker.CancellationPending) { e.Cancel = true; break; }
                        if (PrefetchingTaskCancelTokenSource.IsCancellationRequested) { e.Cancel = true; break; }
                        if (tasks.Wait(-1, PrefetchingTaskCancelTokenSource.Token))
                        {
                            new Action(async () =>
                            {
                                try
                                {
                                    var file = url.GetImageCachePath();
                                    if (!string.IsNullOrEmpty(file))
                                    {
                                        if (File.Exists(file))
                                        {
                                            PrefetchedList.AddOrUpdate(url, true, (k, v) => true);
                                            //if (!PrefetchedList.TryAdd(url, true)) PrefetchedList.TryUpdate(url, true, false);
                                            count--;
                                        }
                                        else
                                        {
                                            var _downReport = DownloadProgressActions.ContainsKey(url) ? DownloadProgressActions[url] : null;
                                            var f_ret = await url.DownloadCacheFile(args.Overwrite, progressAction: _downReport, cancelToken: PrefetchingTaskCancelTokenSource);
                                            if (!string.IsNullOrEmpty(f_ret))
                                            {
                                                PrefetchedList.AddOrUpdate(url, true, (k, v) => true);
                                                //if (!PrefetchedList.TryAdd(url, true)) PrefetchedList.TryUpdate(url, true, false);
                                                count--;
                                            }
                                            else file.CleenLastDownloaded();
                                        }
                                    }
                                    if (PrefetchingBgWorker.CancellationPending) { e.Cancel = true; State = TaskStatus.Canceled; return; }
                                    Percentage = count == 0 ? 100 : (total - count) / (double)total * 100;
                                    Comments = $"Prefetching [ {count} / {total}, I:{illusts.Count} / A:{avatars.Count} / T:{page_thumbs.Count} / P:{page_previews.Count} ]";
                                    State = TaskStatus.Running;
                                    if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                                    else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(Percentage, Comments, State);
                                    //await Task.Delay(10);
                                    this.DoEvents();
                                }
                                catch (Exception ex) { ex.ERROR("PREFETCHING"); }
                                finally { if (tasks is SemaphoreSlim && tasks.CurrentCount <= parallels) tasks.Release(); this.DoEvents(); await Task.Delay(1); }
                            }).Invoke(async: false);
                        }
                    }
                    this.DoEvents();
                    #endregion
                }

                if (PrefetchingBgWorker.CancellationPending) { e.Cancel = true; State = TaskStatus.Canceled; return; }
                if (count >= 0 && total > 0)
                {
                    Percentage = count == 0 ? 100 : (total - count) / (double)total * 100;
                    Comments = $"Done [ {count} / {total}, I:{illusts.Count} / A:{avatars.Count} / T:{page_thumbs.Count} / P:{page_previews.Count} ]";
                    //State = TaskStatus.RanToCompletion;
                    //if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                    //else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(Percentage, Comments, State);
                    //this.DoEvents();
                    $"Prefetching Previews, Avatars, Thumbnails : {Environment.NewLine}  {Comments}".ShowToast("INFO", tag: args.Name ?? Name ?? GetType().Name);
                }
            }
            catch (Exception ex)
            {
                ex.ERROR("PREFETCHING");
                Comments = $"Failed {Comments}";
                State = TaskStatus.Faulted;
                if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(Percentage, Comments, State);
            }
            finally
            {
                try
                {
                    GetOriginalImageSize(originals, e);
                    if (ReportProgressSlim is Action) ReportProgressSlim.Invoke(async: false);
                    else if (ReportProgress is Action<double, string, TaskStatus>) ReportProgress.Invoke(Percentage, Comments, State);
                    this.DoEvents();
                    illusts.Clear();
                    avatars.Clear();
                    page_thumbs.Clear();
                    page_previews.Clear();
                    originals.Clear();
                    needUpdate.Clear();
                }
                catch (Exception ex) { ex.ERROR("PREFETCHED"); }
                if (CanPrefetching is SemaphoreSlim && CanPrefetching.CurrentCount < 1) CanPrefetching.Release();
                LastStartTime = DateTime.Now;
            }
        }

        public void Start(IEnumerable<PixivItem> items, bool include_page_thumb = true, bool include_page_preview = true, bool overwrite = false, bool reverse = false, bool force = true)
        {
            Stop();
            //PrefetchedList = Application.Current.MergeFromSystemPrefetchedList(PrefetchedList);
            if (items is IEnumerable<PixivItem>) { Items = items.ToList(); }
            Start(include_page_thumb, include_page_preview, overwrite, reverse, force);
        }

        public async void Start(bool include_page_thumb = true, bool include_page_preview = true, bool overwrite = false, bool reverse = false, bool force = false)
        {
            try
            {
                Stop();
                var setting = Application.Current.LoadSetting();
                Options = new PrefetchingOpts()
                {
                    Name = Name ?? GetType().Name ?? "PixivItems",
                    PrefetchingPreview = setting.PrefetchingPreview,
                    PrefetchingDownloadParallel = setting.PrefetchingDownloadParallel,
                    IncludePageThumb = include_page_thumb,
                    IncludePagePreview = include_page_preview,
                    ReverseOrder = reverse,
                    Overwrite = overwrite
                };
                if (!Options.PrefetchingPreview) return;
                if (!PrefetchingBgWorker.IsBusy && CanPrefetching is SemaphoreSlim && CanPrefetching.CurrentCount < 1) CanPrefetching.Release();
                if (force || await CanPrefetching.WaitAsync(-1))
                {
                    if (!PrefetchingBgWorker.IsBusy && !PrefetchingBgWorker.CancellationPending)
                    {
                        new Action(() =>
                        {
                            PrefetchingTaskCancelTokenSource = new CancellationTokenSource();
                            PrefetchingBgWorker.RunWorkerAsync(Options);
                        }).Invoke(async: false);
                    }
                }
            }
            catch(Exception ex) { ex.ERROR("PrefetchingTaskStart"); }
        }

        public void Stop(bool dispose = false)
        {
            try
            {
                if (PrefetchingTaskCancelTokenSource is CancellationTokenSource) PrefetchingTaskCancelTokenSource.Cancel();
                if (PrefetchingBgWorker is BackgroundWorker)
                {
                    if (PrefetchingBgWorker.IsBusy) PrefetchingBgWorker.CancelAsync();
                    if (!PrefetchingBgWorker.IsBusy && !PrefetchingBgWorker.CancellationPending && CanPrefetching is SemaphoreSlim && CanPrefetching.CurrentCount < 1) CanPrefetching.Release();
                    PrefetchingBgWorker.Dispose();
                    PrefetchingBgWorker = null;
                }
                if (!dispose) InitBgWorker();
            }
            catch (Exception ex) { ex.ERROR("PrefetchingTaskStop"); }
        }

        public void InitBgWorker()
        {
            try
            {
                if (!(PrefetchingBgWorker is BackgroundWorker)) PrefetchingBgWorker = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
                if (PrefetchingBgWorker is BackgroundWorker)
                {
                    PrefetchingBgWorker.WorkerReportsProgress = true;
                    PrefetchingBgWorker.WorkerSupportsCancellation = true;
                    PrefetchingBgWorker.RunWorkerCompleted += PrefetchingTask_RunWorkerCompleted;
                    PrefetchingBgWorker.ProgressChanged += PrefetchingTask_ProgressChanged;
                    PrefetchingBgWorker.DoWork += PrefetchingTask_DoWork;
                }
            }
            catch (Exception ex) { ex.ERROR("PrefetchingTaskInit"); }
        }

        public PrefetchingTask()
        {
            InitBgWorker();
        }

        public PrefetchingTask(Action<double, string, TaskStatus> ReportAction = null)
        {
            if (PrefetchingBgWorker == null) new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            if (PrefetchingBgWorker is BackgroundWorker)
            {
                PrefetchingBgWorker.WorkerReportsProgress = true;
                PrefetchingBgWorker.WorkerSupportsCancellation = true;
                PrefetchingBgWorker.RunWorkerCompleted += PrefetchingTask_RunWorkerCompleted;
                PrefetchingBgWorker.ProgressChanged += PrefetchingTask_ProgressChanged;
                PrefetchingBgWorker.DoWork += PrefetchingTask_DoWork;
                ReportProgress = ReportAction;
            }
        }

        public PrefetchingTask(Action ReportAction = null)
        {
            if (PrefetchingBgWorker == null) new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            if (PrefetchingBgWorker is BackgroundWorker)
            {
                PrefetchingBgWorker.WorkerReportsProgress = true;
                PrefetchingBgWorker.WorkerSupportsCancellation = true;
                PrefetchingBgWorker.RunWorkerCompleted += PrefetchingTask_RunWorkerCompleted;
                PrefetchingBgWorker.ProgressChanged += PrefetchingTask_ProgressChanged;
                PrefetchingBgWorker.DoWork += PrefetchingTask_DoWork;
                ReportProgressSlim = ReportAction;
            }
        }

        ~PrefetchingTask()
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
            Stop();
            if (PrefetchingBgWorker is BackgroundWorker) PrefetchingBgWorker.Dispose();
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
                Stop(dispose: true);
                if (CanPrefetching is SemaphoreSlim && CanPrefetching.Wait(TimeSpan.FromSeconds(5)))
                {
                    if (CanPrefetching is SemaphoreSlim && CanPrefetching.CurrentCount < 1) CanPrefetching.Release();
                }
                //int count = 100;                
                //while (PrefetchingBgWorker.IsBusy && count > 0) { Task.Delay(50).GetAwaiter().GetResult(); count--; this.DoEvents(); }
                //if (CanPrefetching is SemaphoreSlim && CanPrefetching.CurrentCount < 1) CanPrefetching.Release();
                if (PrefetchingBgWorker is BackgroundWorker) PrefetchingBgWorker.Dispose();
                PrefetchedList.Clear();
                Items.Clear();
            }
            disposed = true;
        }
    }
}
