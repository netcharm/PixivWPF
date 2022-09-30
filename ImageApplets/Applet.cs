using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

using Mono.Options;
using CompactExifLib;
using System.ComponentModel;
using System.Threading;

namespace ImageApplets
{
    public enum CompareMode { EQ, NEQ, GT, LT, GE, LE, HAS, NONE, AND, OR, NOT, XOR, BEFORE, PREV, TODAY, NEXT, AFTER, VALUE };
    public enum DateCompareMode { BEFORE, PREV, TODAY, NEXT, AFTER };
    public enum DateUnit { DAY, WEEK, MONTH, SEASON, YEAR };

    public enum AppletCategory { FileOP, ImageType, ImageContent, ImageAttribure, Other, Unknown, None }
    public enum STATUS { All, Yes, No, None };
    public enum ReadMode { ALL, LINE };

    public class ExecuteResult
    {
        public FileInfo FileInfo { get; set; } = null;
        public string File { get; set; } = string.Empty;
        public bool State { get; set; } = false;
        public dynamic Result { get; set; } = null;
    }

    public interface IApplet
    {
        Applet GetApplet();
    }

    public abstract class Applet: IApplet
    {
        #region Background Execute Helper
        private SemaphoreSlim bgWorking = null;
        private class BackgroundExecuteParamter<T>
        {
            public IEnumerable<T> Files { get; set; } = new List<T>();
            public object[] Args { get; set; } = null;
            public IEnumerable<ExecuteResult> ResultList { get; set; } = new List<ExecuteResult>();
        }
        private BackgroundWorker bgExecuteTask = null;
        public Action<int, int, ExecuteResult, object, object> ReportProgress { get; set; } = null;
        #endregion

        private static bool show_help = false;
        static protected STATUS Status { get; set; } = STATUS.All;

        public string Help { get { return (GetHelp()); } }

        public virtual AppletCategory Category { get; internal protected set; } = AppletCategory.Unknown;

        static private ReadMode _ReadInputMode_ = ReadMode.LINE;
        public ReadMode ReadInputMode { get { return (_ReadInputMode_); } }

        static public string[] LINE_BREAK { get; set; } = new string[] { Environment.NewLine, "\n\r", "\r\n", "\n", "\r" };

        public string Name { get { return (this.GetType().Name); } }

        public int ValuePaddingLeft { get; set; } = 0;
        public int ValuePaddingRight { get; set; } = 0;

        static private bool _verbose_ = false;
        public bool Verbose { get { return (_verbose_); } }

        static private string _input_file_ = string.Empty;
        public string InputFile { get { return (_input_file_); } }

        static private string _output_file_ = string.Empty;
        public string OutputFile { get { return (_output_file_); } }

        public virtual OptionSet Options { get; set; } = new OptionSet()
        {
            { "t|y|true|yes", "Keep True Result", v => { Status = STATUS.Yes; } },
            { "f|n|false|no", "Keep False Result", v => { Status = STATUS.No; } },
            { "a|all", "Keep All", v => { Status = STATUS.All; } },
            { " " },
            { "verbose", "Output All When Redirected STDOUT", v => { _verbose_ = v != null ? true : false; } },
            { "input|filelist=", "Get Files From {FILE}", v => { if (v != null) _input_file_ = v; } },
            { "output=", "Output To {FILE}", v => { if (v != null) _output_file_ = v; } },
            { "read=", "Read Mode {<All|Line>} When Input Redirected", v => { if (v != null) Enum.TryParse(v.ToUpper(), out _ReadInputMode_); } },
        };

        public virtual void InitBackgroundExecute()
        {
            if (!(bgWorking is SemaphoreSlim)) bgWorking = new SemaphoreSlim(1, 1);
            if (!(bgExecuteTask is BackgroundWorker))
            {
                bgExecuteTask = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = true };

                bgExecuteTask.DoWork -= bgExecute_DoWork;
                bgExecuteTask.ProgressChanged -= bgExecute_ProgressChanged;
                bgExecuteTask.RunWorkerCompleted -= bgExecute_RunWorkerCompleted;

                bgExecuteTask.DoWork += bgExecute_DoWork;
                bgExecuteTask.ProgressChanged += bgExecute_ProgressChanged;
                bgExecuteTask.RunWorkerCompleted += bgExecute_RunWorkerCompleted;
            }
        }

        internal protected virtual void AppendOptions(OptionSet opts, int index = 1)
        {
            foreach (var opt in opts.Reverse())
            {
                try
                {
                    Options.Insert(index, opt);
                }
                catch (Exception ex) { ShowMessage(ex); }
            }
        }

        public Applet()
        {
            var opts = new OptionSet()
            {
                { $"{Name}" },
            };

            AppendOptions(opts, 0);
        }
       
        static public IEnumerable<string> GetApplets()
        {
            var result = new List<string>();
            Assembly applets = typeof(Applet).Assembly;
            foreach (Type type in applets.GetTypes())
            {
                if (type.BaseType == typeof(Applet))
                    result.Add(type.Name);
            }
            return (result);
        }

        public abstract Applet GetApplet();

        static public Applet GetApplet(string applet = null)
        {
            Applet result = null;
            if (string.IsNullOrEmpty(applet))
            {
                //result = new Applet();
            }
            else
            {
                Assembly applets = typeof(Applet).Assembly;
                foreach (Type type in applets.GetTypes())
                {
                    if (type.BaseType == typeof(Applet) && type.Name.Equals(applet, StringComparison.CurrentCultureIgnoreCase))
                    {
                        result = Assembly.GetAssembly(type).CreateInstance(type.FullName) as Applet;
                    }
                }
            }
            return (result);
        }

        public virtual string GetHelp(string indent = null)
        {
            var result = string.Empty;
            if (Options is OptionSet)
            {
                using (var sw = new StringWriter())
                {
                    if (base.GetType().BaseType == typeof(Applet))
                        Options.WriteOptionDescriptions(sw);
                    result = string.Join(Environment.NewLine, sw.ToString().Trim().Split(LINE_BREAK, StringSplitOptions.None).Select(l => $"{indent}{l}"));
                }
            }
            return (result);
        }

        public virtual List<string> ParseOptions(IEnumerable<string> args)
        {
            return (Options.Parse(args));
        }

        public virtual void ShowMessage(string text, string title = null)
        {
            if (string.IsNullOrEmpty(title))
                MessageBox.Show(text);
            else
                MessageBox.Show(text, title);
        }

        public virtual void ShowMessage(Exception ex, string title = null)
        {
            if (ex is ExifException)
            {
                switch ((ex as ExifException).ErrorCode)
                {
                    case ExifErrCode.ImageTypeIsNotSupported: break;
                    default:
                        if (string.IsNullOrEmpty(title))
                            MessageBox.Show($"{ex.Message}{Environment.NewLine}{ex.StackTrace}");
                        else
                            MessageBox.Show($"{ex.Message}{Environment.NewLine}{ex.StackTrace}", title);
                        break;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(title))
                    MessageBox.Show($"{ex.Message}{Environment.NewLine}{ex.StackTrace}");
                else
                    MessageBox.Show($"{ex.Message}{Environment.NewLine}{ex.StackTrace}", title);
            }
        }

        public virtual bool GetReturnValueByStatus(dynamic status)
        {
            var ret = true;
            if (status is bool)
            {
                switch (Status)
                {
                    case STATUS.Yes:
                        ret = status;
                        break;
                    case STATUS.No:
                        ret = !status;
                        break;
                    default:
                        ret = true;
                        break;
                }
            }
            return (ret);
        }

        public virtual bool Execute<T>(string file, out T result, params object[] args)
        {
            bool ret = false;
            result = default(T);
            if (File.Exists(file))
            {
                try
                {
                    var fi = new FileInfo(file);
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        ret = Execute<T>(fs, out result);
                    }
                }
                catch (ExifException ex)
                {
                    if (ex is ExifException)
                    {
                        ret = false;
                        switch ((ex as ExifException).ErrorCode)
                        {
                            case ExifErrCode.ImageTypeIsNotSupported: break;
                            default: MessageBox.Show(ex.Message); break;
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
            return (ret);
        }

        public virtual bool Execute<T>(Stream source, out T result, params object[] args)
        {
            bool ret = false;
            result = default(T);
            if (source is Stream && source.CanRead)
            {
                try
                {
                    if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                    var exif = new ExifData(source);
                    ret = Execute<T>(exif, out result);
                }
                catch (ExifException ex)
                {
                    if (ex is ExifException)
                    {
                        ret = false;
                        switch ((ex as ExifException).ErrorCode)
                        {
                            case ExifErrCode.ImageTypeIsNotSupported: break;
                            default: MessageBox.Show(ex.Message); break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            return (ret);
        }

        public virtual bool Execute<T>(ExifData exif, out T result, params object[] args)
        {
            bool ret = false;
            result = default(T);
            try
            {
                ret = true;
            }
            catch (ExifException ex)
            {
                if (ex is ExifException)
                {
                    ret = false;
                    switch ((ex as ExifException).ErrorCode)
                    {
                        case ExifErrCode.ImageTypeIsNotSupported: break;
                        default: MessageBox.Show(ex.Message); break;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            return (ret);
        }

        public virtual IEnumerable<ExecuteResult> Execute(IEnumerable<FileInfo> files, STATUS status = STATUS.Yes, params object[] args)
        {
            Status = status;
            var infolist = new List<ExecuteResult>();
            var index = 0;
            var total = files.Count();
            foreach (var file in files)
            {
                bool result = false;
                var ret = Execute(file.FullName, out result, args);
                if (ret) infolist.Add(new ExecuteResult() { File = file.FullName, FileInfo = file, State = ret, Result = result });
                if (ReportProgress is Action<int, int, ExecuteResult, object, object>) ReportProgress.Invoke(index, total, infolist.Last(), null, null);
            }
            return (infolist);
        }

        public virtual IEnumerable<ExecuteResult> Execute(IEnumerable<string> files, STATUS status = STATUS.Yes, params object[] args)
        {
            Status = status;
            var infolist = new List<ExecuteResult>();
            var index = 0;
            var total = files.Count();
            foreach (var file in files)
            {
                bool result = false;
                var ret = Execute(file, out result, args);
                if (ret) infolist.Add(new ExecuteResult() { File = file, FileInfo = new FileInfo(file), State = ret, Result = result });
                if (ReportProgress is Action<int, int, ExecuteResult, object, object>) ReportProgress.Invoke(index, total, infolist.Last(), null, null);
            }
            return (infolist);
        }

        #region Background Execute Helper
        public virtual void BackgroundExecuteRun(IEnumerable<FileInfo> files, STATUS status = STATUS.Yes, params object[] args)
        {
            InitBackgroundExecute();
            bgExecuteTask.RunWorkerAsync(new BackgroundExecuteParamter<FileInfo>() { Files = files, ResultList = new List<ExecuteResult>(), Args = args });
        }

        public virtual void BackgroundExecuteRun(IEnumerable<string> files, STATUS status = STATUS.Yes, params object[] args)
        {
            InitBackgroundExecute();
            bgExecuteTask.RunWorkerAsync(new BackgroundExecuteParamter<string>() { Files = files, ResultList = new List<ExecuteResult>(), Args = args });
        }

        public bool BackgroundExecuteCancel()
        {
            var result = false;
            if (bgExecuteTask is BackgroundWorker && bgExecuteTask.IsBusy && !bgExecuteTask.CancellationPending)
            {
                bgExecuteTask.CancelAsync();
                if (bgWorking.Wait(TimeSpan.FromSeconds(5)))
                {
                    bgWorking.Release();
                    result = true;
                }
            }
            return (result);
        }

        public async Task<bool> BackgroundExecuteCancelAsync()
        {
            var result = false;
            if (bgExecuteTask is BackgroundWorker && bgExecuteTask.IsBusy && !bgExecuteTask.CancellationPending)
            {
                bgExecuteTask.CancelAsync();
                if (await bgWorking.WaitAsync(TimeSpan.FromSeconds(5)))
                {
                    bgWorking.Release();
                    result = true;
                }
            }
            return (result);
        }

        private void bgExecute_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (bgExecuteTask is BackgroundWorker)
            {
                bgExecuteTask.ReportProgress(100);

                if (ReportProgress is Action<int, int, ExecuteResult, object, object>) ReportProgress.Invoke(100, 100, null, e.Result, e.Error);

                if (bgWorking is SemaphoreSlim && bgWorking.CurrentCount <= 1) bgWorking.Release();
            }
        }

        private void bgExecute_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (bgExecuteTask is BackgroundWorker)
            {

            }
        }

        private void bgExecute_DoWork(object sender, DoWorkEventArgs e)
        {
            if (bgExecuteTask is BackgroundWorker && bgWorking is SemaphoreSlim && bgWorking.Wait(TimeSpan.FromSeconds(5)))
            {
                try
                {
                    if (e.Argument is BackgroundExecuteParamter<FileInfo>)
                    {
                        var param = e.Argument as BackgroundExecuteParamter<FileInfo>;
                        var args = param.Args;
                        var files = param.Files is IEnumerable<FileInfo> ? param.Files : new List<FileInfo>();
                        var infolist = param.ResultList is IList<ExecuteResult> ? param.ResultList as IList<ExecuteResult> : new List<ExecuteResult>();
                        var index = 0;
                        var total = files.Count();
                        foreach (var file in files)
                        {
                            bool result = false;
                            var ret = Execute(file.FullName, out result, args);
                            if (ret) infolist.Add(new ExecuteResult() { File = file.FullName, FileInfo = file, State = ret, Result = result });
                            index++;
                            bgExecuteTask.ReportProgress((int)Math.Floor(total <= 0 ? 1.0 : index / total));
                            if (ReportProgress is Action<int, int, ExecuteResult, object, object>) ReportProgress.Invoke(index, total, infolist.Last(), e.Result, "");
                        }
                    }
                    else if (e.Argument is BackgroundExecuteParamter<string>)
                    {
                        var param = e.Argument as BackgroundExecuteParamter<string>;
                        var args = param.Args;
                        var files = param.Files is IEnumerable<string> ? param.Files : new List<string>();
                        var infolist = param.ResultList is IList<ExecuteResult> ? param.ResultList as IList<ExecuteResult> : new List<ExecuteResult>();
                        var index = 0;
                        var total = files.Count();
                        foreach (var file in files)
                        {
                            bool result = false;
                            var ret = Execute(file, out result, args);
                            if (ret) infolist.Add(new ExecuteResult() { File = file, FileInfo = new FileInfo(file), State = ret, Result = result });
                            index++;
                            bgExecuteTask.ReportProgress((int)Math.Floor(total <= 0 ? 1.0 : index / total));
                            if (ReportProgress is Action<int, int, ExecuteResult, object, object>) ReportProgress.Invoke(index, total, infolist.Last(), e.Result, "");
                        }
                    }
                }
                catch { }
                finally { if (bgWorking is SemaphoreSlim && bgWorking.CurrentCount <= 1) bgWorking.Release(); }
            }
        }
        #endregion
    }
}
