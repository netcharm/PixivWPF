using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using System.Windows.Shapes;

using Microsoft.WindowsAPICodePack.Dialogs;
using PixivWPF.Common;
using System.Text.RegularExpressions;

namespace PixivWPF.Pages
{
    /// <summary>
    /// TouchFolderPage.xaml 的交互逻辑
    /// </summary>
    public partial class BatchProcessPage : Page, IDisposable
    {
        public Window ParentWindow { get; private set; } = null;
        public string Contents { get; set; } = string.Empty;
        public string Mode { get; set; } = "touch";
        public bool Recursion { get; set; } = false;
        public Action<BatchProgressInfo> ProcessingAction = null;

        private StringBuilder progressLog = new StringBuilder();
        public string ProgressLog { get { return (progressLog.ToString()); } }
        private IProgress<BatchProgressInfo> progress = null;
        private Action<BatchProgressInfo> reportAction = null;
        private CancellationTokenSource cancelSource = null;
        BackgroundWorker BatchProcessTask = null;

        private void BatchProcessTask_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            new Action(() =>
            {
                PART_TouchStart.IsEnabled = true;
                PART_TouchCancel.IsEnabled = false;
            }).Invoke();
        }

        private void BatchProcessTask_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {

        }

        private void BatchProcessTask_DoWork(object sender, DoWorkEventArgs e)
        {
            if (Directory.Exists(Contents))
            {
                List<string> ext_imgs = new List<string>() { ".png", ".jpg", ".tif", ".tiff", ".jpeg" };
                var test = e.Argument is bool ? (bool)e.Argument : false;
                var setting = Application.Current.LoadSetting();
                var search_opt = Recursion ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var folderinfo = new DirectoryInfo(Contents);
                var files = folderinfo.GetFiles("*.*", search_opt);
                var flist = files.Where(f => ext_imgs.Contains(f.Extension)).Distinct().NaturalSort().ToList();
                var parallel = setting.PrefetchingDownloadParallel;
                var rnd = new Random();
                cancelSource = new CancellationTokenSource();
                SemaphoreSlim tasks = new SemaphoreSlim(parallel, parallel);
                for (int i = 0; i < flist.Count; i++)
                {
                    if (BatchProcessTask.CancellationPending) { e.Cancel = true; break; }
                    if (cancelSource.IsCancellationRequested) { e.Cancel = true; break; }
                    if (tasks.Wait(-1, cancelSource.Token))
                    {
                        if (BatchProcessTask.CancellationPending) { e.Cancel = true; break; }
                        if (cancelSource.IsCancellationRequested) { e.Cancel = true; break; }
                        new Action(async () =>
                        {
                            var f = flist[i];
                            try
                            {
                                if (BatchProcessTask.CancellationPending) { e.Cancel = true; return; }
                                if (cancelSource.IsCancellationRequested) { e.Cancel = true; return; }

                                var current_info = new BatchProgressInfo()
                                {
                                    FolderName = folderinfo.FullName,
                                    FileName = f.Name,
                                    DisplayText = f.Name,
                                    Current = i + 1,
                                    Total = flist.Count(),
                                    State = TaskStatus.Running
                                };

                                int idx = -1;
                                var illust = await f.FullName.GetIllustId(out idx).GetIllust();
                                if (illust is Pixeez.Objects.Work)
                                {
                                    var url = idx >= 0 ? illust.GetOriginalUrl(idx) : illust.GetOriginalUrl();
                                    var dt = url.ParseDateTime();
                                    if (dt.Ticks > 0)
                                    {
                                        if (!test)
                                        {
                                            if (Mode.Equals("touch", StringComparison.CurrentCultureIgnoreCase)) f.Touch(url);
                                            else if (Mode.Equals("attach", StringComparison.CurrentCultureIgnoreCase)) f.AttachMetaInfo();
                                            else if (ProcessingAction is Action<BatchProgressInfo>) ProcessingAction.Invoke(current_info);
                                            await Task.Delay(rnd.Next(10, 200));
                                        }
                                        current_info.Result = $"{f.Name} processing successed";
                                    }
                                    else
                                    {
                                        current_info.Result = $"{f.Name} paesing date failed";
                                        $"{f.Name} => {url}".DEBUG("ParseDateTime");
                                    }
                                }
                                else
                                {
                                    current_info.Result = $"{f.Name} get work failed";
                                    f.Name.DEBUG("GetIllust");
                                }
                                current_info.LastestTime = current_info.CurrentTime;
                                current_info.CurrentTime = DateTime.Now;
                                if (i == flist.Count - 1) current_info.State = TaskStatus.RanToCompletion;
                                if (reportAction is Action<BatchProgressInfo>) reportAction.Invoke(current_info);
                                this.DoEvents();
                            }
                            catch (Exception ex) { ex.ERROR($"BatchProcessing_{f.Name}"); }
                            finally { if (tasks is SemaphoreSlim && tasks.CurrentCount <= parallel) tasks.Release(); this.DoEvents(); await Task.Delay(1); }
                        }).Invoke(async: false);
                    }
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 释放托管状态(托管对象)。
                    if (BatchProcessTask is BackgroundWorker)
                    {
                        if (cancelSource is CancellationTokenSource) cancelSource.Cancel();
                        if (BatchProcessTask.IsBusy) BatchProcessTask.CancelAsync();
                    }
                    if (progressLog is StringBuilder) progressLog.Clear();
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~TouchFolderPage() {
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

        public BatchProcessPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ParentWindow = Window.GetWindow(this);
            BatchProcessTask = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };
            BatchProcessTask.ProgressChanged += BatchProcessTask_ProgressChanged;
            BatchProcessTask.RunWorkerCompleted += BatchProcessTask_RunWorkerCompleted;
            BatchProcessTask.DoWork += BatchProcessTask_DoWork;
            progressLog.Clear();

            PART_TouchStart.IsEnabled = false;
            PART_TouchCancel.IsEnabled = false;

            progress = new Progress<BatchProgressInfo>(info =>
            {
                try
                {
                    var index = info.Current >= 0 ? info.Current : 0;
                    var total = info.Total >= 0 ? info.Total : 0;
                    var total_s = total.ToString();
                    var index_s = index.ToString().PadLeft(total_s.Length, '0');

                    PART_FileName.Text = info.FileName;

                    #region Update ProgressBar & Progress Info Text
                    var percent = total > 0 ? (double)index / total : 0;
                    var state = info.State == TaskStatus.Running && percent < 1 ? "Processed" : info.State == TaskStatus.RanToCompletion || percent == 1 ? "Finished" : "Idle";
                    PART_Progress.Value = percent >= 1 ? 100 : percent * 100;
                    PART_ProgressPercent.Text = $"{state} [ {index} / {total} ]: {PART_Progress.Value:0.0}%";
                    #endregion

                    #region Update Progress Info Text Color Gradient
                    var factor = PART_Progress.ActualWidth / PART_ProgressPercent.ActualWidth;
                    var offset = Math.Abs((factor - 1) / 2);
                    PART_ProgressLinear.StartPoint = new Point(0 - offset, 0);
                    PART_ProgressLinear.EndPoint = new Point(1 + offset, 0);
                    PART_ProgressLinearLeft.Offset = percent;
                    PART_ProgressLinearRight.Offset = percent;
                    #endregion

                    #region Update Logger
                    progressLog.AppendLine($"[{index_s} / {total_s}] {info.Result}");
                    PART_ProcessLog.Text = ProgressLog;
                    PART_ProcessLog.ScrollToEnd();
                    #endregion

                    if (info.State == TaskStatus.RanToCompletion || percent == 1)
                    {
                        PART_TouchStart.IsEnabled = true;
                        PART_TouchCancel.IsEnabled = false;
                    }
                }
                catch (Exception ex) { ex.ERROR($"{this.Name ?? GetType().Name}_ReportProgress"); }
            });

            reportAction = (info) =>
            {
                if (progress is IProgress<BatchProgressInfo>) progress.Report(info);
            };
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            reportAction = null;
            Dispose();
        }

        private void PART_SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var setting = Application.Current.LoadSetting();
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

            if (dlg.ShowDialog(ParentWindow) == CommonFileDialogResult.Ok)
            {
                Contents = dlg.FileName;
                PART_FolderName.Text = Contents;
                //Title = $"Touching {System.IO.Path.GetFileName(Contents)} ..";
                Title = $"Touching {Contents}";
                ParentWindow.Title = Title;
                Application.Current.UpdateContentWindows(ParentWindow as ContentWindow);
                PART_TouchStart.IsEnabled = true;
                PART_TouchCancel.IsEnabled = false;
            }
        }

        private void PART_TouchStart_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(Contents))
            {
                PART_TouchStart.IsEnabled = false;
                PART_TouchCancel.IsEnabled = true;
                Recursion = PART_Recursion.IsChecked.Value;
                BatchProcessTask.RunWorkerAsync(Keyboard.Modifiers == ModifierKeys.Shift ? true : false);
            }
        }

        private void PART_TouchCancel_Click(object sender, RoutedEventArgs e)
        {
            if (BatchProcessTask is BackgroundWorker)
            {
                if (cancelSource is CancellationTokenSource) cancelSource.Cancel();
                if (BatchProcessTask.IsBusy) BatchProcessTask.CancelAsync();
            }
        }

        private void PART_TouchClose_Click(object sender, RoutedEventArgs e)
        {
            Dispose();
            ParentWindow.Close();
        }

        private void PART_ClearLog_Click(object sender, RoutedEventArgs e)
        {
            if (progressLog is StringBuilder)
            {
                PART_ProcessLog.Clear();
                progressLog.Clear();
            }
        }
    }
}
