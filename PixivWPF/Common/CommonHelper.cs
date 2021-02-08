using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Management;
using System.Media;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Dfust.Hotkeys;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using WPFNotification.Core.Configuration;
using WPFNotification.Model;
using WPFNotification.Services;
using PixivWPF.Pages;

namespace PixivWPF.Common
{
    #region Page enmu type
    public enum PixivPage
    {
        None,
        TrendingTags,
        WorkSet,
        Recommanded,
        Latest,
        My,
        MyFollowerUser,
        MyFollowingUser,
        MyFollowingUserPrivate,
        MyPixivUser,
        MyBlacklistUser,
        MyWork,
        User,
        UserWork,
        Feeds,
        Favorite,
        FavoritePrivate,
        Follow,
        FollowPrivate,
        Bookmark,
        MyBookmark,
        RankingDay,
        RankingDayMale,
        RankingDayFemale,
        RankingDayR18,
        RankingDayMaleR18,
        RankingDayFemaleR18,
        RankingDayManga,
        RankingWeek,
        RankingWeekOriginal,
        RankingWeekRookie,
        RankingWeekR18,
        RankingWeekR18G,
        RankingMonth,
        RankingYear,
        About
    }
    #endregion

    public enum ToastType { DOWNLOAD = 0, OK, OKCANCEL, YES, NO, YESNO };

    public enum AutoExpandMode { OFF = 0, ON, AUTO, SINGLEPAGE };

    public class WebBrowserEx : System.Windows.Forms.WebBrowser
    {
        internal new void Dispose(bool disposing)
        {
            // call WebBrower.Dispose(bool)
            base.Dispose(disposing);
        }

        private bool ignore_all_error = false;
        public bool IgnoreAllError
        {
            get { return (ignore_all_error); }
            set
            {
                ignore_all_error = value;
                if (value) SuppressedAllError();
            }
        }

        /// <summary>
        /// code from -> https://stackoverflow.com/a/13788814/1842521
        /// </summary>
        private void SuppressedAllError()
        {
            ScriptErrorsSuppressed = true;

            try
            {
                FieldInfo field = typeof(WebBrowserEx).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    object axIWebBrowser2 = field.GetValue(this);
                    axIWebBrowser2.GetType().InvokeMember("Silent", BindingFlags.SetProperty, null, axIWebBrowser2, new object[] { true });
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }
    }

    public class HtmlTextData
    {
        public string Html { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    #region DPI Helper
    public class DPI
    {
        public double ScaleX { get; } = 1.0;
        public double ScaleY { get; } = 1.0;

        public double X { get; } = 96.0;
        public double Y { get; } = 96.0;

        public double X15 { get; } = 144.0;
        public double Y15 { get; } = 144.0;

        public double X20 { get; } = 192.0;
        public double Y20 { get; } = 192.0;

        private static DPI dpi = new DPI();
        public static DPI Default
        {
            get { return (dpi); }
            set { dpi = value; }
        }

        public DPI()
        {
            var dpi = BySystemParameters();
            ScaleX = dpi.ScaleX;
            ScaleY = dpi.ScaleY;
            X = dpi.X;
            Y = dpi.Y;
            X15 = X * 1.5;
            Y15 = Y * 1.5;
            X20 = X * 2.0;
            Y20 = Y * 2.0;
        }

        public DPI(double x, double y, double scale_x = 1.0, double scale_y = 1.0)
        {
            ScaleX = scale_x;
            ScaleY = scale_y;
            X = x;
            Y = y;
            X15 = X * 1.5;
            Y15 = Y * 1.5;
            X20 = X * 2.0;
            Y20 = Y * 2.0;
        }

        public DPI(Visual visual)
        {
            try
            {
                dpi = GetDefault(visual);
                ScaleX = dpi.ScaleX;
                ScaleY = dpi.ScaleY;
                X = dpi.X;
                Y = dpi.Y;
                X15 = X * 1.5;
                Y15 = Y * 1.5;
                X20 = X * 2.0;
                Y20 = Y * 2.0;
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static DPI GetDefault(Visual visual)
        {
            var result = new DPI();
            try
            {
                var ds = VisualTreeHelper.GetDpi(visual);
                var x = ds.PixelsPerInchX;
                var y = ds.PixelsPerInchY;
                var sx = ds.DpiScaleX;
                var sy = ds.DpiScaleY;
                dpi = new DPI(x, y, sx, sy);
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static DPI FromVisual(Visual visual)
        {
            var source = PresentationSource.FromVisual(visual);
            var dpiX = 96.0;
            var dpiY = 96.0;
            var scaleX = 1.0;
            var scaleY = 1.0;
            try
            {
                if (source?.CompositionTarget != null)
                {
                    scaleX = source.CompositionTarget.TransformToDevice.M11;
                    scaleY = source.CompositionTarget.TransformToDevice.M22;
                    dpiX = 96.0 * scaleX;
                    dpiY = 96.0 * scaleY;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return new DPI(dpiX, dpiY, scaleX, scaleY);
        }

        public static DPI BySystemParameters()
        {
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            var dpiXProperty = typeof(SystemParameters).GetProperty("DpiX", flags);
            //var dpiYProperty = typeof(SystemParameters).GetProperty("DpiY", flags);
            var dpiYProperty = typeof(SystemParameters).GetProperty("Dpi", flags);
            var dpiX = 96.0;
            var dpiY = 96.0;
            var scaleX = 1.0;
            var scaleY = 1.0;
            try
            {
                if (dpiXProperty != null) { dpiX = (int)dpiXProperty.GetValue(null, null); }
                if (dpiYProperty != null) { dpiY = (int)dpiYProperty.GetValue(null, null); }
            }
            catch (Exception ex) { ex.ERROR(); }
            return new DPI(dpiX, dpiY, scaleX, scaleY);
        }
    }
    #endregion

    #region Custom storage type
    public class StorageType
    {
        [JsonProperty("Folder")]
        public string Folder { get; set; } = string.Empty;
        [JsonProperty("Cached")]
        public bool Cached { get; set; } = true;
        [JsonProperty("IncludeSubFolder")]
        public bool IncludeSubFolder { get; set; } = false;

        [JsonIgnore]
        public int Count { get; set; } = -1;

        public override string ToString()
        {
            return Folder;
        }

        public StorageType(string path, bool cached = false)
        {
            Folder = path;
            Cached = cached;
            Count = -1;
        }
    }
    #endregion

    #region CustomImage
    public class CustomImageSource
    {
        public ImageSource Source { get; set; } = null;
        public long Size { get; set; } = 0;
        public long ColorDepth { get; set; } = 0;
        public string SourcePath { get; set; } = string.Empty;

        ~CustomImageSource()
        {
            Source = null;
            SourcePath = string.Empty;
        }

        public CustomImageSource()
        {
        }

        public CustomImageSource(ImageSource source, string path)
        {
            Source = source;
            SourcePath = path;
        }
    }
    #endregion

    #region Hotkey
    public class HotKeyConfig
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DisplayDescription { get; set; } = string.Empty;
        [JsonConverter(typeof(StringEnumConverter))]
        public System.Windows.Forms.Keys Keys { get; set; } = default(System.Windows.Forms.Keys);
        [JsonIgnore]
        [JsonConverter(typeof(ICommandTypeConverter<Prism.Commands.DelegateCommand>))]
        public ICommand Command { get; set; } = default(ICommand);
    }
    #endregion

    public static class ApplicationExtensions
    {
        #region Application Setting Helper
        public static Setting CurrentSetting { get { return (Setting.Instance is Setting ? Setting.Instance : Setting.Load()); } }
        public static Setting LoadSetting(this Application app, bool force = false)
        {
            if (force) Setting.Load(force, force);
            return (CurrentSetting);
            //return (!force && Setting.Instance is Setting ? Setting.Instance : Setting.Load(force));
        }

        public static void SaveSetting(this Application app, bool full = false)
        {
            try
            {
                var setting = app.LoadSetting();
                if (setting is Setting) setting.Save(full);
            }
            catch (Exception ex)
            {
                ex.ERROR();
                if (Setting.Instance is Setting) Setting.Instance.Save(full);
            }
        }

        public static async void LoadTags(this Application app, bool all = false, bool force = false)
        {
            await new Action(() =>
            {
                Setting.LoadTags(all, force);
            }).InvokeAsync();
        }

        public static void SaveTags(this Application app)
        {
            if (Setting.Instance is Setting) Setting.Instance.SaveTags();
            return;
        }

        public static async void LoadCustomTemplate(this Application app)
        {
            await new Action(() =>
            {
                Setting.UpdateContentsTemplete();
            }).InvokeAsync();
        }

        public static string SaveTarget(this Application app, string file = "")
        {
            string result = file;

            result = CommonHelper.ChangeSaveTarget(file);

            return (result);
        }
        #endregion

        #region Application Information
        private static string root = string.Empty;
        public static string Root
        {
            get
            {
                if (string.IsNullOrEmpty(root)) root = GetRoot();
                return (root);
            }
        }

        public static string GetRoot()
        {
            return (Path.GetDirectoryName(Application.ResourceAssembly.CodeBase.ToString()).Replace("file:\\", ""));
        }

        public static string GetRoot(this Application app)
        {
            return (Root);
        }

        public static string Version(this Application app, bool alt = false)
        {
            var version = alt ? Assembly.GetCallingAssembly().GetName().Version : Assembly.GetExecutingAssembly().GetName().Version;
            return (version.ToString());
            //return (Application.ResourceAssembly.GetName().Version.ToString());
        }

        private static int pid = -1;
        public static int PID
        {
            get
            {
                if (pid < 0) pid = CurrentProcess.Id;
                return (pid);
            }
        }

        public static int GetPID(this Application app)
        {
            return (PID);
        }

        private static string processor_id = string.Empty;
        public static string ProcessorID
        {
            get
            {
                if (string.IsNullOrEmpty(processor_id)) processor_id = GetProcessorID();
                return (processor_id);
            }
        }

        public static string GetProcessorID()
        {
            string result = string.Empty;

            ManagementObjectSearcher mos = new ManagementObjectSearcher("select * from Win32_Processor");
            foreach (ManagementObject mo in mos.Get())
            {
                try
                {
                    result = mo["ProcessorId"].ToString();
                    break;
                }
                catch (Exception ex) { ex.ERROR(); continue; }

                //foreach (PropertyData p in mo.Properties)
                //{
                //    if(p.Name.Equals("ProcessorId", StringComparison.CurrentCultureIgnoreCase))
                //    {
                //        result = p.Value.ToString();
                //        break;
                //    }
                //}
                //if (string.IsNullOrEmpty(result)) break;
            }

            return (result);
        }

        public static string GetProcessorID(this Application app)
        {
            return (ProcessorID);
        }

        private static string machine_id = string.Empty;
        public static string MachineID
        {
            get
            {
                if (string.IsNullOrEmpty(machine_id)) machine_id = GetDeviceId();
                return (machine_id);
            }
        }

        public static string GetDeviceId()
        {
            var result = ProcessorID;
            try
            {
                string location = @"SOFTWARE\Microsoft\Cryptography";
                string name = "MachineGuid";

                var view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;
                using (RegistryKey localMachineX64View = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                {
                    using (RegistryKey rk = localMachineX64View.OpenSubKey(location))
                    {
                        if (rk == null)
                            throw new KeyNotFoundException(string.Format("Key Not Found: {0}", location));

                        object machineGuid = rk.GetValue(name);
                        if (machineGuid == null)
                            throw new IndexOutOfRangeException(string.Format("Index Not Found: {0}", name));

                        result = machineGuid.ToString().Replace("-", "");
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static string GetDeviceId(this Application app)
        {
            return (MachineID);
        }

        private static Process current_process = null;
        private static Process CurrentProcess
        {
            get
            {
                if (current_process == null) current_process = System.Diagnostics.Process.GetCurrentProcess();
                return (current_process);
            }
        }

        public static Process GetCurrentProcess(this Application app)
        {
            return (CurrentProcess);
        }

        public static long MemoryUsage(this Application app, bool is_private = false)
        {
            long result = -1;
            if (current_process == null) current_process = System.Diagnostics.Process.GetCurrentProcess();            
            try
            {
                using (PerformanceCounter PC = new PerformanceCounter())
                {
                    PC.CategoryName = "Process";
                    PC.CounterName = is_private ? "Private Bytes" : "Working Set"; // "Working Set - Private";
                    PC.InstanceName = current_process.ProcessName;
                    result = Convert.ToInt64(PC.NextValue());
                    //result = Convert.ToInt64(PC.RawValue);//.NextValue());
                    PC.Close();
                }
                if (result <= 0) result = is_private ? current_process.PrivateMemorySize64 : current_process.WorkingSet64;
            }
            catch (Exception ex) { ex.ERROR("MEMORYUSAGE"); }
            return (result);
        }

        public static void GC(this Application app, string name, bool wait = false, bool system_memory = false)
        {
            long mem_ws_before = 0, mem_pb_before = 0, mem_ws_after = 0, mem_pb_after = 0;
            if (system_memory)
            {
                mem_ws_before = Application.Current.MemoryUsage();// process.WorkingSet64;
                mem_pb_before = Application.Current.MemoryUsage(true);// process.PrivateMemorySize64;
            }

            double M = 1024.0 * 1024.0;
            var before = System.GC.GetTotalMemory(true);
            System.GC.Collect();
            if (wait) System.GC.WaitForPendingFinalizers();
            var after = System.GC.GetTotalMemory(true);
            $"Managed Memory Usage: {before / M:F2}M => {after / M:F2}M".DEBUG(name ?? string.Empty);

            if (system_memory)
            {
                mem_ws_after = Application.Current.MemoryUsage();// process.WorkingSet64;
                mem_pb_after = Application.Current.MemoryUsage(true);// process.PrivateMemorySize64;
                $"System Memory Usage (WS/PB): {mem_ws_before / M:F2}M/{mem_pb_before / M:F2}M => {mem_ws_after / M:F2}M/{mem_pb_after / M:F2}M".DEBUG(name ?? string.Empty);
            }
        }
        #endregion

        #region Application Config files Watchdog
        private static ConcurrentDictionary<string, FileSystemWatcher> _watchers = new ConcurrentDictionary<string, FileSystemWatcher>();
        //private static DateTime lastConfigEventTick = DateTime.Now;
        //private static string lastConfigEventFile = string.Empty;
        //private static WatcherChangeTypes lastConfigEventType = WatcherChangeTypes.All;

        private static void OnConfigChanged(object source, FileSystemEventArgs e)
        {
#if DEBUG
            // Specify what is done when a file is changed, created, or deleted.
            $"File: {e.FullPath} {e.ChangeType}".DEBUG();
#endif
            try
            {
                //if (e.ChangeType == lastConfigEventType &&
                //    e.FullPath == lastConfigEventFile &&
                //    lastConfigEventTick.Ticks.DeltaNowMillisecond() < 10) throw new Exception("Same config change event!");
                var fn = e.FullPath;
                if (e.ChangeType == WatcherChangeTypes.Created)
                {
                    if (File.Exists(e.FullPath))
                    {
                        if (fn.Equals(Application.Current.LoadSetting().ConfigFile, StringComparison.CurrentCultureIgnoreCase))
                        {
                            //Setting.Load(true, false);
                            //lastConfigEventTick = DateTime.Now;
                        }
                        else if (fn.Equals(Application.Current.LoadSetting().CustomTagsFile, StringComparison.CurrentCultureIgnoreCase))
                        {
                            Setting.LoadCustomTags(true);
                            //lastConfigEventTick = DateTime.Now;
                        }
                        else if (fn.Equals(Application.Current.LoadSetting().CustomWildcardTagsFile, StringComparison.CurrentCultureIgnoreCase))
                        {
                            Setting.LoadCustomWidecardTags(true);
                            //lastConfigEventTick = DateTime.Now;
                        }
                        else if (fn.Equals(Application.Current.LoadSetting().ContentsTemplateFile, StringComparison.CurrentCultureIgnoreCase))
                        {
                            Setting.UpdateContentsTemplete();
                            //lastConfigEventTick = DateTime.Now;
                        }
                    }
                }
                else if (e.ChangeType == WatcherChangeTypes.Changed)
                {
                    if (File.Exists(e.FullPath))
                    {
                        if (fn.Equals(Application.Current.LoadSetting().ConfigFile, StringComparison.CurrentCultureIgnoreCase))
                        {
                            Setting.Load(true, false);
                            //lastConfigEventTick = DateTime.Now;
                        }
                        else if (fn.Equals(Application.Current.LoadSetting().CustomTagsFile, StringComparison.CurrentCultureIgnoreCase))
                        {
                            //Setting.LoadTags(false, true);
                            Setting.LoadCustomTags(true);
                            //lastConfigEventTick = DateTime.Now;
                        }
                        else if (fn.Equals(Application.Current.LoadSetting().CustomWildcardTagsFile, StringComparison.CurrentCultureIgnoreCase))
                        {
                            Setting.LoadCustomWidecardTags(true);
                            //lastConfigEventTick = DateTime.Now;
                        }
                        else if (fn.Equals(Application.Current.LoadSetting().ContentsTemplateFile, StringComparison.CurrentCultureIgnoreCase))
                        {
                            Setting.UpdateContentsTemplete();
                            //lastConfigEventTick = DateTime.Now;
                        }
                    }
                }
                else if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    if (fn.Equals(Application.Current.LoadSetting().ConfigFile, StringComparison.CurrentCultureIgnoreCase))
                    {
                        //lastConfigEventTick = DateTime.Now;
                    }
                    else if (fn.Equals(Application.Current.LoadSetting().CustomTagsFile, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Setting.LoadCustomTags(true);
                        //lastConfigEventTick = DateTime.Now;
                    }
                    else if (fn.Equals(Application.Current.LoadSetting().CustomWildcardTagsFile, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Setting.LoadCustomWidecardTags(true);
                        //lastConfigEventTick = DateTime.Now;
                    }
                    else if (fn.Equals(Application.Current.LoadSetting().ContentsTemplateFile, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Setting.UpdateContentsTemplete();
                        //lastConfigEventTick = DateTime.Now;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("CONFIGWATCHER"); }
            finally
            {
                //lastConfigEventTick = DateTime.Now;
                //lastConfigEventFile = e.FullPath;
                //lastConfigEventType = e.ChangeType;
            }
        }

        private static void OnConfigRenamed(object source, RenamedEventArgs e)
        {
#if DEBUG
            // Specify what is done when a file is renamed.
            $"File: {e.OldFullPath} renamed to {e.FullPath}".DEBUG();
#endif
            try
            {
                //if (e.ChangeType == lastConfigEventType &&
                //    e.FullPath == lastConfigEventFile &&
                //    lastConfigEventTick.Ticks.DeltaNowMillisecond() < 10) throw new Exception("Same config change event!");

                var fn_o = e.OldFullPath;
                var fn_n = e.FullPath;
                if (e.ChangeType == WatcherChangeTypes.Renamed)
                {
                    if (fn_o.Equals(Application.Current.LoadSetting().ConfigFile, StringComparison.CurrentCultureIgnoreCase) ||
                        fn_n.Equals(Application.Current.LoadSetting().ConfigFile, StringComparison.CurrentCultureIgnoreCase))
                    {
                        //lastConfigEventTick = DateTime.Now;
                    }
                    else if (fn_o.Equals(Application.Current.LoadSetting().CustomTagsFile, StringComparison.CurrentCultureIgnoreCase) ||
                        fn_n.Equals(Application.Current.LoadSetting().CustomTagsFile, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Setting.LoadCustomTags(true);
                        //lastConfigEventTick = DateTime.Now;
                    }
                    else if (fn_o.Equals(Application.Current.LoadSetting().CustomWildcardTagsFile, StringComparison.CurrentCultureIgnoreCase) ||
                        fn_n.Equals(Application.Current.LoadSetting().CustomWildcardTagsFile, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Setting.LoadCustomWidecardTags(true);
                        //lastConfigEventTick = DateTime.Now;
                    }
                    else if (fn_o.Equals(Application.Current.LoadSetting().ContentsTemplateFile, StringComparison.CurrentCultureIgnoreCase) ||
                        fn_n.Equals(Application.Current.LoadSetting().ContentsTemplateFile, StringComparison.CurrentCultureIgnoreCase))
                    {
                        Setting.UpdateContentsTemplete();
                        //lastConfigEventTick = DateTime.Now;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("CONFIGWATCHER"); }
            finally
            {
                //lastConfigEventTick = DateTime.Now;
                //lastConfigEventFile = e.FullPath;
                //lastConfigEventType = e.ChangeType;
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void InitAppWatcher(this Application app, string folder)
        {
            try
            {
                Application.Current.ReleaseAppWatcher();
                var watcher = new FileSystemWatcher(folder, "*.*")
                {
                    //NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.DirectoryName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName, //| NotifyFilters.Size | NotifyFilters.DirectoryName,
                    IncludeSubdirectories = false
                };
                watcher.Changed += new FileSystemEventHandler(OnConfigChanged);
                watcher.Created += new FileSystemEventHandler(OnConfigChanged);
                watcher.Deleted += new FileSystemEventHandler(OnConfigChanged);
                watcher.Renamed += new RenamedEventHandler(OnConfigRenamed);
                //watcher.Changed += OnConfigChanged;
                //watcher.Created += OnConfigChanged;
                //watcher.Deleted += OnConfigChanged;
                //watcher.Renamed += OnConfigRenamed;

                // Begin watching.
                watcher.EnableRaisingEvents = true;
                _watchers[folder] = watcher;
            }
            catch (Exception ex) { ex.ERROR("CONFIGWATCHER"); }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void AddAppWatcher(this Application app, string folder, string filter = "*.*", bool IncludeSubFolder = false)
        {
            if (Directory.Exists(folder) && !_watchers.ContainsKey(folder))
            {
                folder.UpdateDownloadedListCacheAsync();
                var watcher = new FileSystemWatcher(folder, filter)
                {
                    //NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    IncludeSubdirectories = IncludeSubFolder
                };
                watcher.Changed += new FileSystemEventHandler(OnConfigChanged);
                watcher.Created += new FileSystemEventHandler(OnConfigChanged);
                watcher.Deleted += new FileSystemEventHandler(OnConfigChanged);
                watcher.Renamed += new RenamedEventHandler(OnConfigRenamed);
                // Begin watching.
                watcher.EnableRaisingEvents = true;

                _watchers[folder] = watcher;
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void ReleaseAppWatcher(this Application app)
        {
            if (_watchers is ConcurrentDictionary<string, FileSystemWatcher>)
            {
                foreach (var w in _watchers)
                {
                    try
                    {
                        if (w.Value is FileSystemWatcher)
                        {
                            w.Value.Dispose();
                        }
                    }
                    catch { }
                }
                _watchers.Clear();
            }
        }
        #endregion

        #region Application Theme Helper
        public static IList<string> GetAccents(this Application app)
        {
            return (Theme.Accents);
        }

        public static IList<SimpleAccent> GetAccentColorList(this Application app)
        {
            return (Theme.AccentColorList);
        }

        public static string CurrentAccent(this Application app)
        {
            return (Theme.CurrentAccent);
        }

        public static string CurrentStyle(this Application app)
        {
            return (Theme.CurrentStyle);
        }

        public static string CurrentTheme(this Application app)
        {
            return (Theme.CurrentTheme);
        }

        public static string GetAccent(this Application app)
        {
            return (Theme.CurrentAccent);
        }

        public static Color GetForegroundColor(this Application app)
        {
            return (Theme.ThemeForegroundColor);
        }

        public static Brush GetForegroundBrush(this Application app)
        {
            return (Theme.ThemeForegroundBrush);
        }

        public static Color GetBackgroundColor(this Application app)
        {
            return (Theme.ThemeBackgroundColor);
        }

        public static Brush GetBackgroundBrush(this Application app)
        {
            return (Theme.ThemeBackgroundBrush);
        }

        public static Color GetTextColor(this Application app)
        {
            return (Theme.TextColor);
        }

        public static Brush GetTextBrush(this Application app)
        {
            return (Theme.TextBrush);
        }

        public static Color GetIdealTextColor(this Application app)
        {
            return (Theme.IdealForeground);
        }

        public static Brush GetIdealTextBrush(this Application app)
        {
            return (Theme.IdealForegroundBrush);
        }

        public static Color GetSucceedColor(this Application app)
        {
            return (Theme.SucceedColor);
        }

        public static Brush GetSucceedBrush(this Application app)
        {
            return (Theme.SucceedBrush);
        }

        public static Color GetErrorColor(this Application app)
        {
            return (Theme.ErrorColor);
        }

        public static Brush GetErrorBrush(this Application app)
        {
            return (Theme.ErrorBrush);
        }

        public static Color GetWarningColor(this Application app)
        {
            return (Theme.WarningColor);
        }

        public static Brush GetWarningBrush(this Application app)
        {
            return (Theme.WarningBrush);
        }

        public static Color GetFailedColor(this Application app)
        {
            return (Theme.FailedColor);
        }

        public static Brush GetFailedBrush(this Application app)
        {
            return (Theme.FailedBrush);
        }

        public static Color GetNonExistsColor(this Application app)
        {
            return (Theme.Gray5Color);
        }

        public static Brush GetNonExistsBrush(this Application app)
        {
            return (Theme.Gray5Brush);
        }

        public static string GetStyle(this Application app)
        {
            return (Theme.CurrentStyle);
        }

        public static string GetTheme(this Application app)
        {
            return (Theme.CurrentTheme);
        }

        public static int GetAccentIndex(this Application app, string accent = "")
        {
            var result = 0;
            try
            {
                var acls = Application.Current.GetAccentColorList();
                var ca = string.IsNullOrEmpty(accent) ? Application.Current.CurrentAccent() : accent;
                var acl = acls.Where(a => a.AccentName.Equals(ca));
                if (acl.Count() > 0)
                    result = acls.IndexOf(acl.First());
                else
                    result = 0;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static void SetAccent(this Application app, string accent)
        {
            try
            {
                Theme.CurrentAccent = accent;
                app.UpdateTheme();
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void SetStyle(this Application app, string style)
        {
            try
            {
                Theme.CurrentStyle = style;
                app.UpdateTheme();
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void SetTheme(this Application app, string theme)
        {
            try
            {
                Theme.Change(theme);
                app.UpdateTheme();
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void SetTheme(this Application app, string style, string accent)
        {
            try
            {
                Theme.Change(style, accent);
                app.UpdateTheme();
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void ToggleTheme(this Application app)
        {
            try
            {
                Theme.Toggle();
                app.UpdateTheme();
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void UpdateTheme(this Application app)
        {
            try
            {
                CommonHelper.UpdateTheme();
            }
            catch (Exception ex) { ex.ERROR("UPDATETHEME"); }
        }

        public static void SetThemeSync(this Application app, string mode = "")
        {
            try
            {
                if (string.IsNullOrEmpty(mode)) mode = "app";
                else mode = mode.ToLower();

                ControlzEx.Theming.ThemeSyncMode sync = ControlzEx.Theming.ThemeSyncMode.DoNotSync;
                if (mode.Equals("app"))
                    sync = ControlzEx.Theming.ThemeSyncMode.SyncWithAppMode;
                else if (mode.Equals("all"))
                    sync = ControlzEx.Theming.ThemeSyncMode.SyncAll;
                else if (mode.Equals("accent"))
                    sync = ControlzEx.Theming.ThemeSyncMode.SyncWithAccent;
                else if (mode.Equals("highcontrast"))
                    sync = ControlzEx.Theming.ThemeSyncMode.SyncWithHighContrast;
                else
                    sync = ControlzEx.Theming.ThemeSyncMode.DoNotSync;

                Theme.SetSyncMode(sync);
            }
            catch (Exception ex) { ex.ERROR(); }
        }
        #endregion

        #region Application Window Helper
        private static string[] r15 = new string[] { "xxx", "r18", "r17", "r15", "18+", "17+", "15+" };
        private static string[] r17 = new string[] { "xxx", "r18", "r17", "18+", "17+", };
        private static string[] r18 = new string[] { "xxx", "r18", "18+"};

        public static MainWindow GetMainWindow(this Application app)
        {
            MainWindow result = null;
            try
            {
                //if (app.MainWindow is MainWindow)
                //    result = app.MainWindow as MainWindow;
                app.Dispatcher.Invoke(() =>
                {
                    var win = app.MainWindow;
                    if (win is MainWindow) result = win as MainWindow;
                });
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static Window GetActiveWindow(this Application app)
        {
            Window result = null;
            try
            {
                app.Dispatcher.Invoke(() =>
                {
                    foreach (var win in app.Windows)
                    {
                        if (win is MetroWindow && (win as MetroWindow).IsActive)
                        {
                            result = win as Window;
                            break;
                        }
                    }
                });
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static Window GetLatestWindow(this Application app)
        {
            Window result = null;
            try
            {
                app.Dispatcher.Invoke(() =>
                {
                    var wins = new List<Window>();
                    foreach (Window win in app.Windows)
                    {
                        if (win.Title.Equals("Dropbox", StringComparison.CurrentCultureIgnoreCase)) continue;
                        else if (win.Content is DownloadManagerPage) continue;
                        else if (win.Content is LoginPage) continue;
                        else if (win is ContentWindow) wins.Add(win);
                    }
                    result = wins.LastOrDefault();
                });
            }
            catch (Exception ex) { ex.ERROR("GETLASTESTWINDOW"); }
            return (result);
        }

        public static PixivLoginDialog GetLoginWindow(this Application app)
        {
            PixivLoginDialog result = null;
            try
            {
                app.Dispatcher.Invoke(() =>
                {
                    foreach (var win in app.Windows)
                    {
                        if (win is PixivLoginDialog)
                        {
                            result = win as PixivLoginDialog;
                            result.Topmost = true;
                            result.Show();
                            result.Activate();
                        }
                    }
                });
            }
            catch (Exception ex) { ex.ERROR("GETLOGINWINDOW"); }
            return (result);
        }

        public static IList<string> OpenedWindowTitles(this Application app)
        {
            List<string> titles = new List<string>();
            try
            {
                app.Dispatcher.Invoke(() =>
                {
                    foreach (Window win in app.Windows)
                    {
                        if (win is MainWindow) continue;
                        else if (win is ContentWindow)
                        {
                            if (win.Title.StartsWith("Download", StringComparison.CurrentCultureIgnoreCase))
                            {
                                if (win.Content is DownloadManagerPage)
                                {
                                    var dm = win.Content as DownloadManagerPage;
                                    titles.AddRange(dm.Unfinished());
                                }
                            }
                            //else if (win.Title.StartsWith("Search", StringComparison.CurrentCultureIgnoreCase)) continue;
                            //else if (win.Title.StartsWith("Preview", StringComparison.CurrentCultureIgnoreCase)) continue;
                            else if (win.Title.StartsWith("PIXIV Login", StringComparison.CurrentCultureIgnoreCase)) continue;
                            else if (win.Title.StartsWith("DropBox", StringComparison.CurrentCultureIgnoreCase)) continue;
                            else if (win.Title.StartsWith("PixivPedia", StringComparison.CurrentCultureIgnoreCase)) continue;
                            else if (win.Title.StartsWith("History", StringComparison.CurrentCultureIgnoreCase)) continue;
                            else titles.Add(win.Title);
                        }
                        else continue;
                    }
                });
            }
            catch (Exception ex) { ex.ERROR(); }
            return (titles);
        }

        public static void SetTitle(this Application app, string title)
        {
            try
            {
                app.Dispatcher.Invoke(() =>
                {
                    if (Application.Current.MainWindow is MetroWindow)
                    {
                        var win = Application.Current.MainWindow as MetroWindow;
                        win.Title = title;
                    }
                });
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        private static async void MinimizedWindow(MetroWindow win, PixivItem item, string condition)
        {
            await new Action(() =>
            {
                if (item.IsWork() && win.WindowState != WindowState.Minimized)
                {
                    if (r18.Contains(condition))
                    {
                        if (item.Sanity.Equals("18+")) win.WindowState = WindowState.Minimized;
                    }
                    else if (r17.Contains(condition))
                    {
                        if (item.Sanity.Equals("18+")) win.WindowState = WindowState.Minimized;
                        else if (item.Sanity.Equals("17+")) win.WindowState = WindowState.Minimized;
                    }
                    else if (r15.Contains(condition))
                    {
                        if (item.Sanity.Equals("18+")) win.WindowState = WindowState.Minimized;
                        else if (item.Sanity.Equals("17+")) win.WindowState = WindowState.Minimized;
                        else if (item.Sanity.Equals("15+")) win.WindowState = WindowState.Minimized;
                    }
                }
            }).InvokeAsync(true);
        }

        public static async void MinimizedWindows(this Application app, string condition = "")
        {
            if (string.IsNullOrEmpty(condition)) return;
            await new Action(async () =>
            {
                condition = condition.ToLower();
                foreach (Window win in Application.Current.Windows)
                {
                    var state = win.WindowState;
                    var active = win.IsActive;
                    try
                    {
                        if (win is ContentWindow)
                        {
                            if (win.Content is IllustDetailPage)
                            {
                                var page = win.Content as IllustDetailPage;
                                if (page.Contents.IsWork())
                                    MinimizedWindow(win as MetroWindow, page.Contents, condition);
                            }
                            else if (win.Content is IllustImageViewerPage)
                            {
                                var page = win.Content as IllustImageViewerPage;
                                if (page.Contents.IsWork())
                                    MinimizedWindow(win as MetroWindow, page.Contents, condition);
                            }
                        }
                        else if (win is MainWindow && win.Content is TilesPage)
                        {
                            var page = win.Content as TilesPage;
                            if (page.IllustDetail.Content is IllustDetailPage)
                            {
                                var detail = page.IllustDetail.Content as IllustDetailPage;
                                if (detail.Contents.IsWork())
                                    MinimizedWindow(win as MetroWindow, detail.Contents, condition);
                            }
                        }
                    }
                    catch (Exception ex) { ex.ERROR(); continue; }
                    finally
                    {
                        await Task.Delay(1);
                        DoEvents();
                        if (!active)
                        {
                            win.ShowActivated = false;
                            win.Topmost = false;
                        }
                    }
                }
            }).InvokeAsync(true);
        }

        public static bool IsLogin(this Application app)
        {
            return (GetLoginWindow(app) != null ? true : false);
        }

        public static bool InSearching(this Application app)
        {
            var win = GetActiveWindow(app);
            if (win is MainWindow)
                return ((win as MainWindow).InSearching);
            else if (win is ContentWindow)
                return ((win as ContentWindow).InSearching);
            else return (false);
        }

        public static bool InSearching(this Page page)
        {
            var win = GetActiveWindow(Application.Current);
            if (win is MainWindow)
                return ((win as MainWindow).InSearching);
            else if (win is ContentWindow)
                return ((win as ContentWindow).InSearching);
            else return (false);
        }

        public static bool InSearching(this Window win)
        {
            if (win is MainWindow)
                return ((win as MainWindow).InSearching);
            else if (win is ContentWindow)
                return ((win as ContentWindow).InSearching);
            else return (false);
        }

        public static async void Active(this Application app, string param = "")
        {
            try
            {
                if (string.IsNullOrEmpty(param) || $"{PID}".Equals(param))
                {
                    await new Action(() =>
                    {
                        var main = app.GetMainWindow();
                        if (main is Window)
                        {
                            main.Activate();
                        }
                    }).InvokeAsync(true);
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static bool Activate(this Application app)
        {
            bool result = false;
            try
            {
#if DEBUG
                var pat = $@"(.*?)PixivWPF-Search-Debug-(\d+)";
#else
                var pat = $@"(.*?)PixivWPF-Search-(\d+)";
#endif
                var pipes = Directory.GetFiles("\\\\.\\pipe\\", "PixivWPF*");
                foreach (var pipe in pipes)
                {
                    if (Regex.IsMatch(pipe, pat, RegexOptions.IgnoreCase))
                    {
                        result = true;
                        var pid = Regex.Replace(pipe, pat, "$2", RegexOptions.IgnoreCase);
                        var cmd = string.IsNullOrEmpty(pid) ? $"Cmd:Active" : $"Cmd:Active:{pid}";
                        Commands.SendToOtherInstance.Execute(cmd);
                        break;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }
        #endregion

        #region Application NamedPipe Helper
        private static string pipe_name = string.Empty;
        public static string PipeName
        {
            get
            {
                if (string.IsNullOrEmpty(pipe_name)) pipe_name = PipeServerName();
                return (pipe_name);
            }
        }

        public static string PipeServerName()
        {
#if DEBUG
            return ($"PixivWPF-Search-Debug-{Application.Current.GetPID()}");
#else
            return ($"PixivWPF-Search-{Application.Current.GetPID()}");
#endif
        }

        public static string PipeServerName(this Application app)
        {
            return (PipeName);
        }

        public static bool PipeExists(this Application app)
        {
            bool result = false;
            var pipes = Directory.GetFiles("\\\\.\\pipe\\", "PixivWPF*");
            foreach (var pipe in pipes)
            {
                if (Regex.IsMatch(pipe, $@"PixivWPF-Search-\d+", RegexOptions.IgnoreCase))
                {
                    result = true;
                    break;
                }
            }
            return (result);
        }

        public static bool ProcessCommand(this Application app, string command)
        {
            bool result = false;
            try
            {
                var kv = command.Substring(4).Split(new char[] { '-', '_', ':', '+', '=' });
                var action = kv[0];
                var param = kv.Length == 2 ? kv[1] : string.Empty;
                if (action.StartsWith("min", StringComparison.CurrentCultureIgnoreCase))
                {
                    Application.Current.MinimizedWindows(string.IsNullOrEmpty(param) ? "r18" : param);
                }
                else if (action.StartsWith("active", StringComparison.CurrentCultureIgnoreCase))
                {
                    Application.Current.Active(param);
                }
                else if (action.StartsWith("openlog", StringComparison.CurrentCultureIgnoreCase))
                {
                    Commands.OpenLogs.Execute(param);
                }
                else if (action.StartsWith("writelog", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(param)) Commands.WriteLogs.Execute(param);
                }
                else if (action.StartsWith("cleanlog", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(param)) Commands.CleanLogs.Execute(null);
                }
                else
                {
                    foreach(var hotkey in HotkeyConfig)
                    {
                        try
                        {
                            if (action.Equals(hotkey.Name, StringComparison.CurrentCultureIgnoreCase) ||
                                action.Equals(hotkey.DisplayName, StringComparison.CurrentCultureIgnoreCase) ||
                                action.Equals(hotkey.Description, StringComparison.CurrentCultureIgnoreCase))
                            {
                                new Action(() =>
                                {
                                    var win = Application.Current.GetActiveWindow();
                                    if (win == null) Application.Current.GetLatestWindow();
                                    if (win == null) win = Application.Current.GetMainWindow();
                                    if (win is Window && !win.InSearching()) hotkey.Command.Execute(win);
                                }).Invoke(true);
                                break;
                            }
                        }
                        catch(Exception ex) { ex.ERROR("NAMEDPIPE_CMD"); }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }
        #endregion

        #region Application LOG Helper
        private static bool IsConsole
        {
            get
            {
                try
                {
                    return (Environment.UserInteractive && Console.Title.Length > 0);
                }
                catch (Exception) { return (false); }
            }
        }

        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public static void TRACE(this string contents, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG            
            Debug.WriteLine($"{prefix}{contents}");
#endif            
            new Action(() => {
                logger.Trace($"{prefix}{contents}");
            }).Invoke(async: false);
        }

        public static void DEBUG(this string contents, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG
            Debug.WriteLine($"{prefix}{contents}");
#endif
            new Action(() => { 
                logger.Debug($"{prefix}{contents}");
            }).Invoke(async: false);
        }

        public static void INFO(this string contents, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG            
            Debug.WriteLine($"{prefix}{contents}");
#endif
            new Action(() => {
                logger.Info($"{prefix}{contents}");
            }).Invoke(async: false);           
        }

        public static void WARN(this string contents, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG            
            Debug.WriteLine($"{prefix}{contents}");
#endif
            new Action(() => {
                logger.Warn($"{prefix}{contents}");
            }).Invoke(async: false);            
        }

        public static void ERROR(this string contents, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG            
            Debug.WriteLine($"{prefix}{contents}");
#endif
            new Action(() => {
                 logger.Error($"{prefix}{contents}");
            }).Invoke(async: false);
        }

        public static void FATAL(this string contents, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG            
            Debug.WriteLine($"{prefix}{contents}");
#endif
            new Action(() => {
                logger.Fatal($"{prefix}{contents}");
            }).Invoke(async: false); 
        }

        public static void NOTICE(this string contents, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG            
            Debug.WriteLine($"{prefix}{contents}");
#endif
            new Action(() => {
                logger.Info($"{prefix}{contents}");
                logger.Debug($"{prefix}{contents}");
                logger.Error($"{prefix}{contents}");
            }).Invoke(async: false);
        }

        public static void LOG(this string contents, string title = "", string tag = "")
        {
            if (title.ToUpper().Contains("INFO")) contents.INFO(tag);
            else if (title.ToUpper().Contains("ERROR")) contents.ERROR(tag);
            else if (title.ToUpper().Contains("WARN")) contents.WARN(tag);
            else if (title.ToUpper().Contains("FATAL")) contents.FATAL(tag);
            else contents.INFO();
        }

        public static void TRACE(this Exception ex, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
            var contents = $"{ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}Source => {ex.Source}{Environment.NewLine}Base => {ex.GetBaseException()}";
            contents.TRACE(tag);
        }

        public static void DEBUG(this Exception ex, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
            var contents = $"{ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}Root => {ex.GetRootException()}{Environment.NewLine}Inner => {ex.InnerException}";
            contents.DEBUG(tag);
        }

        public static void INFO(this Exception ex, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
            var contents = $"{ex.Message}";
            contents.INFO(tag);
        }

        public static void WARN(this Exception ex, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
            var contents = $"{ex.Message}{Environment.NewLine}{ex.Source}";
            contents.WARN(tag);
        }

        public static void ERROR(this Exception ex, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
            var contents = $"{ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}Source => {ex.Source}";
            contents.ERROR(tag);
        }

        public static void FATAL(this Exception ex, string tag = "")
        {
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
            var contents = $"{ex.Message}{Environment.NewLine}{ex.StackTrace}";
            contents.FATAL(tag);
        }

        public static void LOG(this Exception ex, string title = "ERROR", string tag = "")
        {
            if (title.ToUpper().Contains("INFO")) ex.INFO(tag);
            else if (title.ToUpper().Contains("ERROR")) ex.ERROR(tag);
            else if (title.ToUpper().Contains("WARN")) ex.WARN(tag);
            else if (title.ToUpper().Contains("FATAL")) ex.FATAL(tag);
            else ex.ERROR();
        }

        public static void LOG(this object obj, string contents, string title = "INFO")
        {
            if(obj != null)
            {
                var log = NLog.LogManager.GetLogger(obj.GetType().Name);
                if (title.ToUpper().Contains("INFO")) log.Info(contents);
                else if (title.ToUpper().Contains("WARN")) log.Warn(contents);
                else if (title.ToUpper().Contains("DEBUG")) log.Debug(contents);
                else if (title.ToUpper().Contains("TRACE")) log.Trace(contents);
                else if (title.ToUpper().Contains("ERROR")) log.Error(contents);
                else if (title.ToUpper().Contains("FATAL")) log.Fatal(contents);
                else log.Info(contents);
            }
        }

        public static void StartLog(this Application app)
        {
            NLog.LogManager.AutoShutdown = true;
            NLog.LogManager.Configuration.DefaultCultureInfo = CultureInfo.CurrentCulture;
            if (logger == null) logger = NLog.LogManager.GetCurrentClassLogger();
        }

        public static void StopLog(this Application app)
        {
            if (logger is NLog.Logger) NLog.LogManager.Shutdown();
        }

        public static IList<string> GetLogs(this Application app)
        {
            var logs = new List<string>();
            try
            {
                //var fileTarget = (NLog.Targets.FileTarget) NLog.LogManager.Configuration.FindTargetByName("logfile");
                foreach (var target in NLog.LogManager.Configuration.ConfiguredNamedTargets)
                {
                    if (target is NLog.Targets.FileTarget)
                    {
                        var fileTarget = target as NLog.Targets.FileTarget;
                        // Need to set timestamp here if filename uses date. 
                        // For example - filename="${basedir}/logs/${shortdate}/trace.log"
                        foreach (var level in NLog.LogLevel.AllLoggingLevels)
                        {
                            var logEventInfo = new NLog.LogEventInfo() { Level = level, TimeStamp = DateTime.Now };
                            string fileName = fileTarget.FileName.Render(logEventInfo);
                            if (File.Exists(fileName)) logs.Add(fileName);
                        }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (logs);
        }

        public static void CleanLogs(this Application app)
        {
            var logs = GetLogs(app);
            foreach (var log in logs)
            {
                if (File.Exists(log)) File.Delete(log);
            }
        }
        #endregion

        #region Application Timed Tasks Helper
        private static System.Timers.Timer autoTaskTimer = null;
        private static ConcurrentDictionary<Window, long> toast_list = new ConcurrentDictionary<Window, long>();

        private static void InitTaskTimer()
        {
            try
            {
                var setting = LoadSetting(Application.Current);
                if (autoTaskTimer == null)
                {
                    autoTaskTimer = new System.Timers.Timer(setting.ToastShowTimes * 1000) { AutoReset = true, Enabled = false };
                    autoTaskTimer.Elapsed += Timer_Elapsed;
                    autoTaskTimer.Enabled = true;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void AddToast(this Application app, Window win)
        {
            InitTaskTimer();
            var now = Environment.TickCount;
            toast_list[win] = now;
            Timer_Elapsed(app, null);
        }

        private static async void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!(toast_list is ConcurrentDictionary<Window, long>) || toast_list.Count <= 0) return;
            await new Action(() =>
            {
                var setting = Application.Current.LoadSetting();
                var now = Environment.TickCount;
                foreach (var kv in toast_list)
                {
                    var delta = Math.Abs(TimeSpan.FromMilliseconds(now - kv.Value).TotalSeconds);
                    if (delta >= setting.ToastShowTimes + 5)
                    {
                        try
                        {
                            if (kv.Key is Window)
                                kv.Key.Close();
                            long value = 0L;
                            toast_list.TryRemove(kv.Key, out value);
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }
                }
            }).InvokeAsync();

            Commands.SaveOpenedWindows.Execute(null);
        }
        #endregion

        #region Application Visit History Helper
        private static ObservableCollection<PixivItem> history = new ObservableCollection<PixivItem>();
        public static ObservableCollection<PixivItem> History { get { return (HistorySource(null)); } }

        public static void HistoryAdd(this Application app, Pixeez.Objects.Work illust, ObservableCollection<PixivItem> source)
        {
            if (source is ObservableCollection<PixivItem>)
            {
                try
                {
                    var new_id = illust.Id ?? -1;
                    if (source.Count() > 0)
                    {
                        var last_item = source.First();
                        if (last_item.IsWork())
                        {
                            var last_id = last_item.Illust.Id ?? -1;
                            if (last_id == new_id) return;
                        }
                    }

                    var illusts = source.Where(i => i.IsWork()).Distinct();
                    var found = illusts.Where(i => i.Illust.Id == new_id);
                    if (found.Count() >= 1)
                    {
                        var i = found.FirstOrDefault();
                        i.Illust = illust;
                        i.User = illust.User;
                        i.IsFollowed = i.Illust.IsLiked();
                        i.IsFavorited = i.User.IsLiked();
                        i.IsDownloaded = i.Illust.IsPartDownloaded();
                        source.Move(source.IndexOf(i), 0);
                    }
                    else
                    {
                        source.Insert(0, illust.WorkItem());
                        var setting = app.LoadSetting();
                        if (source.Count > setting.HistoryLimit)
                        {
                            source.Last().State = TaskStatus.Canceled;
                            source.Last().Source = null;
                            Application.Current.DoEvents();
                            source.Remove(source.Last());
                        }
                    }
                    HistoryUpdate(app, source);
                }
                catch (Exception ex)
                {
                    ex.Message.ShowMessageBox("ERROR[HISTORY]");
                }
            }
        }

        public static void HistoryAdd(this Application app, Pixeez.Objects.UserBase user, ObservableCollection<PixivItem> source)
        {
            if (source is ObservableCollection<PixivItem>)
            {
                try
                {
                    var new_id = user.Id ?? -1;
                    if (source.Count() > 0)
                    {
                        var last_item = source.First();
                        if (last_item.IsUser())
                        {
                            var last_id = last_item.User.Id ?? -1;
                            if (last_id == new_id) return;
                        }
                    }
                    var users = source.Where(i => i.IsUser()).Distinct();
                    var found = users.Where(i => i.User.Id == new_id);
                    if (found.Count() >= 1)
                    {
                        var u = found.FirstOrDefault();
                        u.User = user;
                        u.IsFollowed = u.Illust.IsLiked();
                        u.IsFavorited = u.User.IsLiked();
                        source.Move(source.IndexOf(u), 0);
                    }
                    else
                    {
                        source.Insert(0, user.UserItem());
                        var setting = app.LoadSetting();
                        if (source.Count > setting.HistoryLimit)
                        {
                            source.Last().State = TaskStatus.Canceled;
                            source.Last().Source = null;
                            Application.Current.DoEvents();
                            source.Remove(source.Last());
                        }
                    }
                    HistoryUpdate(app, source);
                }
                catch (Exception ex)
                {
                    ex.Message.ShowMessageBox("ERROR[HISTORY]");
                }
            }
        }

        public static void HistoryAdd(this Application app, PixivItem item, ObservableCollection<PixivItem> source)
        {
            if (source is ObservableCollection<PixivItem>)
            {
                try
                {
                    long new_id = -1;
                    long.TryParse(item.ID, out new_id);
                    if (source.Count() > 0)
                    {
                        var last_item = source.First();
                        long last_id = -1;
                        long.TryParse(last_item.ID, out last_id);
                        if (last_id == new_id) return;
                    }
                    var items = source.Where(i => i.IsUser() || i.IsWork()).Distinct();
                    var found = items.Where(i => i.ID.Equals(new_id.ToString()));
                    if (found.Count() >= 1)
                    {
                        var i = found.FirstOrDefault();
                        i.User = item.User;
                        i.Illust = item.Illust;
                        i.IsFollowed = item.IsFollowed;
                        i.IsFavorited = item.IsFavorited;
                        i.IsDownloaded = item.IsDownloaded;
                        i.State = TaskStatus.RanToCompletion;
                        source.Move(source.IndexOf(i), 0);
                    }
                    else
                    {
                        source.Insert(0, item);
                        var setting = app.LoadSetting();
                        if (source.Count > setting.HistoryLimit)
                        {
                            source.Last().State = TaskStatus.Canceled;
                            source.Last().Source = null;
                            Application.Current.DoEvents();
                            source.Remove(source.Last());
                        }
                    }
                    HistoryUpdate(app, source);
                }
                catch (Exception ex)
                {
                    ex.Message.ShowMessageBox("ERROR[HISTORY]");
                }
            }
        }

        public static void HistoryAdd(this Application app, Pixeez.Objects.Work illust)
        {
            app.HistoryAdd(illust, history);
        }

        public static void HistoryAdd(this Application app, Pixeez.Objects.UserBase user)
        {
            app.HistoryAdd(user, history);
        }

        public static void HistoryAdd(this Application app, PixivItem item)
        {
            if (item.IsWork() || item.IsUser())
            {
                app.HistoryAdd(item, history);
            }
        }

        public static void HistoryAdd(this Application app, dynamic item)
        {
            if (item is Pixeez.Objects.Work) app.HistoryAdd(item as Pixeez.Objects.Work);
            else if (item is Pixeez.Objects.User) app.HistoryAdd(item as Pixeez.Objects.User);
            else if (item is Pixeez.Objects.UserBase) app.HistoryAdd(item as Pixeez.Objects.UserBase);
            else if (item is PixivItem) app.HistoryAdd(item as PixivItem);
        }

        public static void HistoryUpdate(this Application app, ObservableCollection<PixivItem> source = null)
        {
            if (source is ObservableCollection<PixivItem> && source != history)
            {
                if (history is ObservableCollection<PixivItem>)
                {
                    history.Clear();
                    history.AddRange(source);
                }
                else
                    history = new ObservableCollection<PixivItem>(source);
            }
            else
            {
                var win = "History".GetWindowByTitle();
                if (win is ContentWindow && win.Content is HistoryPage) (win.Content as HistoryPage).UpdateDetail();
            }
        }

        private static void UpdateHistoryFromCache(IEnumerable<PixivItem> items)
        {
            foreach (var item in items)
            {
                if (item.IsWork())
                {
                    var i = item.ID.FindIllust();
                    if (i is Pixeez.Objects.Work && i.User is Pixeez.Objects.UserBase)
                    {
                        item.Illust = i;
                        item.User = i.User;
                    }
                    item.IsFollowed = item.User.IsLiked();
                    item.IsFavorited = item.Illust.IsLiked();
                    item.IsDownloaded = item.Illust.IsPartDownloaded();
                }
                else if (item.IsUser())
                {
                    var u = item.ID.FindUser();
                    if (u is Pixeez.Objects.UserBase) item.User = u;
                    item.IsFollowed = item.User.IsLiked();
                }
            }
        }

        public static IEnumerable<PixivItem> HistoryList(this Application app, bool full_update = false)
        {
            var result = new List<PixivItem>();
            if (history is ObservableCollection<PixivItem>)
            {
                if (full_update)
                    UpdateHistoryFromCache(history);
                else
                {
                    history.UpdateLikeState();
                    history.UpdateDownloadState();
                }
                result = history.ToList();
            }
            return (result);
        }

        public static ObservableCollection<PixivItem> HistorySource(this Application app, bool full_update = false)
        {
            if (history is ObservableCollection<PixivItem>)
            {
                if (full_update)
                    UpdateHistoryFromCache(history);
                else
                {
                    history.UpdateLikeState();
                    history.UpdateDownloadState();
                }
            }
            return (history);
        }

        public static PixivItem HistoryRecent(this Application app, int index = 0)
        {
            if (history.Count > 0)
            {
                var idx = history.Count > index ? history.Count - index -1 : history.Count - 1;
                return (idx >= 0 ? history.Skip(idx).Take(1).FirstOrDefault() : null);
            }
            else return (null);
        }

        public static PixivItem HistoryRecentIllust(this Application app, int index = 0)
        {
            if (history.Count > 0)
            {
                var illusts = history.Where(h => h.IsWork());
                var idx = illusts.Count() > index ? illusts.Count() - index -1 : illusts.Count() - 1;
                return (idx >= 0 ? illusts.Skip(idx).Take(1).FirstOrDefault() : null);
            }
            else return (null);
        }

        public static PixivItem HistoryRecentUser(this Application app, int index = 0)
        {
            if (history.Count > 0)
            {
                var users = history.Where(h => h.IsUser());
                var idx = users.Count() > index ? users.Count() - index -1 : users.Count() - 1;
                return (idx >= 0 ? users.Skip(idx).Take(1).FirstOrDefault() : null);
            }
            else return (null);
        }

        public static IList<PixivItem> HistoryRecents(this Application app, int num = 1)
        {
            if (history.Count > 0)
            {
                var recents = history.Where(h => h.IsWork()||h.IsUser());
                return (recents.Take(num).ToList());
            }
            else return (null);
        }

        public static IList<PixivItem> HistoryRecentIllusts(this Application app, int num = 1)
        {
            if (history.Count > 0)
            {
                var illusts = history.Where(h => h.IsWork());
                return (illusts.Take(num).ToList());
            }
            else return (null);
        }

        public static IList<PixivItem> HistoryRecentUsers(this Application app, int num = 1)
        {
            if (history.Count > 0)
            {
                var users = history.Where(h => h.IsUser());
                return (users.Take(num).ToList());
            }
            else return (null);
        }
        #endregion

        #region Application Hotkey Helper
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private static string Key2String(System.Windows.Forms.Keys key)
        {
            var result = Dfust.Hotkeys.Util.Keys2String.KeyToString(key);
            result = ApplicationCulture.TextInfo.ToTitleCase(result);

            var keys = key.ToString().Split(',');
            if (string.IsNullOrEmpty(result)) result = key.ToString();
            else if (keys.Length > 1 && !result.Contains('+')) result = $"{keys.FirstOrDefault()}+{result}";
            else if (result.EndsWith("+")) result = $"{result}{keys.FirstOrDefault().Trim()}";
            result = result.Replace(" ", "").Replace("Next", "PageDown");
            //var keys = hotkey.Keys.ToString().Split(',').Reverse();
            //var key = string.Join("+", keys);

            return (result);
        }

        public static void ReleaseKeyboardModifiers(this Application app, bool all = true, bool updown = false)
        {
            var k = Keyboard.Modifiers;
            if (all || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                // SHIFT Key
                if (updown) keybd_event(0x10, 0x00, 0x0001, 0);
                keybd_event(0x10, 0x00, 0x0002, 0);
                // Left SHIFT Key
                if (updown) keybd_event(0xA0, 0x00, 0x0001, 0);
                keybd_event(0xA0, 0x00, 0x0002, 0);
                // Right SHIFT Key
                if (updown) keybd_event(0xA1, 0x00, 0x0001, 0);
                keybd_event(0xA1, 0x00, 0x0002, 0);
            }
            if (all || Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // CTRL Key
                if (updown) keybd_event(0x11, 0x00, 0x0001, 0);
                keybd_event(0x11, 0x00, 0x0002, 0);
                // Left CONTROL Key
                if (updown) keybd_event(0xA2, 0x00, 0x0001, 0);
                keybd_event(0xA2, 0x00, 0x0002, 0);
                // Right CONTROL Key
                if (updown) keybd_event(0xA3, 0x00, 0x0001, 0);
                keybd_event(0xA3, 0x00, 0x0002, 0);
            }
            if (all || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                // Alt Key
                if (updown) keybd_event(0x12, 0x00, 0x0001, 0);
                keybd_event(0x12, 0x00, 0x0002, 0);
                // Left MENU Key
                if (updown) keybd_event(0xA4, 0x00, 0x0001, 0);
                keybd_event(0xA4, 0x00, 0x0002, 0);
                // Right MENU Key
                if (updown) keybd_event(0xA5, 0x00, 0x0001, 0);
                keybd_event(0xA5, 0x00, 0x0002, 0);
            }
            if (all || Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
            {
                // Left Windows Key
                if (updown) keybd_event(0x5B, 0x00, 0x0001, 0);
                keybd_event(0x5B, 0x00, 0x0002, 0);
                // Right Windows Key
                if (updown) keybd_event(0x5C, 0x00, 0x0001, 0);
                keybd_event(0x5C, 0x00, 0x0002, 0);
            }
        }

        private static List<HotKeyConfig> HotkeyConfig = new List<HotKeyConfig>()
        {
            #region Illust Nav
            new HotKeyConfig() { Name = "IllustFirst", Command = Commands.FirstIllust,
                                 Keys = System.Windows.Forms.Keys.Home },
            new HotKeyConfig() { Name = "IllustLast", Command = Commands.LastIllust,
                                 Keys = System.Windows.Forms.Keys.End },
            new HotKeyConfig() { Name = "IllustPrev", Command = Commands.PrevIllust,
                                 Keys = System.Windows.Forms.Keys.OemOpenBrackets },
            new HotKeyConfig() { Name = "IllustNext", Command = Commands.NextIllust,
                                 Keys = System.Windows.Forms.Keys.OemCloseBrackets },
            new HotKeyConfig() { Name = "IllustPrev", Command = Commands.PrevIllust,
                                 Keys = System.Windows.Forms.Keys.XButton2 },
            new HotKeyConfig() { Name = "IllustNext", Command = Commands.NextIllust,
                                 Keys = System.Windows.Forms.Keys.XButton1 },
            new HotKeyConfig() { Name = "IllustPrevPage", Command = Commands.PrevIllustPage,
                                 Keys = System.Windows.Forms.Keys.OemOpenBrackets | System.Windows.Forms.Keys.Shift },
            new HotKeyConfig() { Name = "IllustNextPage", Command = Commands.NextIllustPage,
                                 Keys = System.Windows.Forms.Keys.OemCloseBrackets | System.Windows.Forms.Keys.Shift },
            new HotKeyConfig() { Name = "IllustPrevCategory", Command = Commands.PrevCategory,
                                 Keys = System.Windows.Forms.Keys.OemSemicolon },
            new HotKeyConfig() { Name = "IllustNextCategory", Command = Commands.NextCategory,
                                 Keys = System.Windows.Forms.Keys.OemQuotes },
            #endregion
            #region Scroll Tiles
            new HotKeyConfig() { Name = "TilesScrollPageUp", Command = Commands.ScrollPageUp,
                                 Keys = System.Windows.Forms.Keys.PageUp | System.Windows.Forms.Keys.Shift },
            new HotKeyConfig() { Name = "TilesScrollPageDown", Command = Commands.ScrollPageDown,
                                 Keys = System.Windows.Forms.Keys.PageDown | System.Windows.Forms.Keys.Shift },
            new HotKeyConfig() { Name = "TilesScrollPageTop", Command = Commands.ScrollPageFirst,
                                 Keys = System.Windows.Forms.Keys.PageUp | System.Windows.Forms.Keys.Control },
            new HotKeyConfig() { Name = "TilesScrollPageBottom", Command = Commands.ScrollPageLast,
                                 Keys = System.Windows.Forms.Keys.PageDown | System.Windows.Forms.Keys.Control },
            #endregion
            #region Update Tiles
            new HotKeyConfig() { Name = "TilesRefresh", Command = Commands.RefreshPage,
                                 Keys = System.Windows.Forms.Keys.F5 },
            new HotKeyConfig() { Name = "TilesAppend", Command = Commands.AppendTiles,
                                 Keys = System.Windows.Forms.Keys.F3 },
            new HotKeyConfig() { Name = "TilesRefreshThumbnail", Command = Commands.RefreshPageThumb,
                                 Keys = System.Windows.Forms.Keys.F6 },
            #endregion
            #region Info
            new HotKeyConfig() { Name = "OpenLogs", Command = Commands.OpenLogs,
                                 Keys = System.Windows.Forms.Keys.None },
            new HotKeyConfig() { Name = "CopyArtworkID", Command = Commands.CopyArtworkIDs,
                                 Keys = System.Windows.Forms.Keys.None },
            new HotKeyConfig() { Name = "CopyArtistID", Command = Commands.CopyArtistIDs,
                                 Keys = System.Windows.Forms.Keys.None },
            #endregion
            #region Copy
            new HotKeyConfig() { Name = "CopyPreview", Command = Commands.CopyImage,
                                 Keys = System.Windows.Forms.Keys.P | System.Windows.Forms.Keys.Control },
            new HotKeyConfig() { Name = "Copy", Command = Commands.Copy,
                                 Keys = System.Windows.Forms.Keys.None },
            #endregion
            #region Open
            new HotKeyConfig() { Name = "OpenHistory", Command = Commands.OpenHistory,
                                 Keys = System.Windows.Forms.Keys.H | System.Windows.Forms.Keys.Control },
            new HotKeyConfig() { Name = "OpenWork", Command = Commands.OpenWork,
                                 Keys = System.Windows.Forms.Keys.N | System.Windows.Forms.Keys.Control },
            new HotKeyConfig() { Name = "OpenUser", Command = Commands.OpenUser,
                                 Keys = System.Windows.Forms.Keys.U | System.Windows.Forms.Keys.Control },
            new HotKeyConfig() { Name = "OpenDownloaded", Command = Commands.OpenDownloaded,
                                 Keys = System.Windows.Forms.Keys.O | System.Windows.Forms.Keys.Control },
            new HotKeyConfig() { Name = "OpenCached", Command = Commands.OpenCachedImage,
                                 Keys = System.Windows.Forms.Keys.K | System.Windows.Forms.Keys.Control },
            #endregion
            #region Save
            new HotKeyConfig() { Name = "SaveIllust", Command = Commands.SaveIllust,
                                 Keys = System.Windows.Forms.Keys.S | System.Windows.Forms.Keys.Control },
            new HotKeyConfig() { Name = "SaveIllustAll", Command = Commands.SaveIllustAll,
                                 Keys = System.Windows.Forms.Keys.S | System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift },
            #endregion
            #region Change Like State
            new HotKeyConfig() { Name = "ChangeIllustLikeState", Command = Commands.ChangeIllustLikeState,
                                 Keys = System.Windows.Forms.Keys.F7 },
            new HotKeyConfig() { Name = "ChangeUserLikeState", Command = Commands.ChangeUserLikeState,
                                 Keys = System.Windows.Forms.Keys.F8 }
            #endregion
        };
        private static HotkeyCollection ApplicationHotKeys = new HotkeyCollection(Enums.Scope.Application);
        private static CultureInfo ApplicationCulture = CultureInfo.CurrentCulture;
        public static void BindHotkey(this Application app, string name, System.Windows.Forms.Keys key, ICommand command)
        {
            try
            {
                ApplicationHotKeys.RegisterHotkey(key, async (e) =>
                {
                    try
                    {
                        if (e.Description.Equals("OpenCached"))
                        {
                            Application.Current.ReleaseKeyboardModifiers();
                            Application.Current.DoEvents();
                        }
                        var key_name = string.IsNullOrEmpty(e.ChordName) ? string.Join("+", e.Keys.Select(k => k.ToString())) : e.ChordName;
                        $"Description: {e.Description}, Keys: {ApplicationCulture.TextInfo.ToTitleCase(key_name)}".DEBUG();
                        await new Action(() =>
                        {
                            var win = Application.Current.GetActiveWindow();
                            if (win is Window && !win.InSearching()) command.Execute(win);
                        }).InvokeAsync(true);
                    }
                    catch (Exception ex) { ex.Message.DEBUG("ERROR[HOTKEY]"); }
                }, name);
            }
            catch (Exception ex) { ex.Message.DEBUG("ERROR[HOTKEY]"); }
        }

        public static void BindHotkeys(this Application app, bool global = false)
        {
            if (global)
            {
                //BindHotkey(app, "PrevIllust", Key.OemOpenBrackets, ModifierKeys.None, OnPrevIllust);
            }
            else
            {
                if (ApplicationHotKeys == null) ApplicationHotKeys = new HotkeyCollection(global ? Enums.Scope.Global : Enums.Scope.Application);

                //BindHotkey(app, "IllustFirst", System.Windows.Forms.Keys.Home, Commands.FirstIllust);
                //BindHotkey(app, "IllustLast", System.Windows.Forms.Keys.End, Commands.LastIllust);
                //BindHotkey(app, "IllustPrev", System.Windows.Forms.Keys.OemOpenBrackets, Commands.PrevIllust);
                //BindHotkey(app, "IllustNext", System.Windows.Forms.Keys.OemCloseBrackets, Commands.NextIllust);
                //BindHotkey(app, "IllustPrevPage", System.Windows.Forms.Keys.OemOpenBrackets | System.Windows.Forms.Keys.Shift, Commands.PrevIllustPage);
                //BindHotkey(app, "IllustNextPage", System.Windows.Forms.Keys.OemCloseBrackets | System.Windows.Forms.Keys.Shift, Commands.NextIllustPage);

                //BindHotkey(app, "TilesScrollPageUp", System.Windows.Forms.Keys.PageUp | System.Windows.Forms.Keys.Shift, Commands.ScrollPageUp);
                //BindHotkey(app, "TilesScrollPageDown", System.Windows.Forms.Keys.PageDown | System.Windows.Forms.Keys.Shift, Commands.ScrollPageDown);
                //BindHotkey(app, "TilesScrollPageTop", System.Windows.Forms.Keys.PageUp | System.Windows.Forms.Keys.Control, Commands.ScrollPageFirst);
                //BindHotkey(app, "TilesScrollPageBottom", System.Windows.Forms.Keys.PageDown | System.Windows.Forms.Keys.Control, Commands.ScrollPageLast);

                //BindHotkey(app, "TilesRefresh", System.Windows.Forms.Keys.F5, Commands.RefreshPage);
                //BindHotkey(app, "TilesAppend", System.Windows.Forms.Keys.F3, Commands.AppendTiles);
                //BindHotkey(app, "TilesRefreshThumbnail", System.Windows.Forms.Keys.F6, Commands.RefreshPageThumb);

                //BindHotkey(app, "OpenHistory", System.Windows.Forms.Keys.H | System.Windows.Forms.Keys.Control, Commands.OpenHistory);
                //BindHotkey(app, "OpenWork", System.Windows.Forms.Keys.N | System.Windows.Forms.Keys.Control, Commands.OpenWork);
                //BindHotkey(app, "OpenUser", System.Windows.Forms.Keys.U | System.Windows.Forms.Keys.Control, Commands.OpenUser);
                //BindHotkey(app, "OpenDownloaded", System.Windows.Forms.Keys.O | System.Windows.Forms.Keys.Control, Commands.OpenDownloaded);
                //BindHotkey(app, "OpenCached", System.Windows.Forms.Keys.K | System.Windows.Forms.Keys.Control, Commands.OpenCachedImage);
                //BindHotkey(app, "CopyPreview", System.Windows.Forms.Keys.P | System.Windows.Forms.Keys.Control, Commands.CopyImage);

                //BindHotkey(app, "SaveIllust", System.Windows.Forms.Keys.S | System.Windows.Forms.Keys.Control, Commands.SaveIllust);
                //BindHotkey(app, "SaveIllustAll", System.Windows.Forms.Keys.S | System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift, Commands.SaveIllustAll);

                //BindHotkey(app, "ChangeIllustLikeState", System.Windows.Forms.Keys.F7, Commands.ChangeIllustLikeState);
                //BindHotkey(app, "ChangeUserLikeState", System.Windows.Forms.Keys.F8, Commands.ChangeUserLikeState);

                foreach (var hotkey in HotkeyConfig)
                {
                    var cmd_name = hotkey.Name ?? hotkey.DisplayName ?? hotkey.Description ?? hotkey.DisplayDescription ?? "UNKNOWN";
                    if (hotkey.Keys == System.Windows.Forms.Keys.None)
                    {
                        $"Command \"{cmd_name}\" not binding to any hotkey.".INFO();
                    }
                    else
                    {
                        var key = Key2String(hotkey.Keys);
                        $"Command \"{cmd_name}\" binding to hotkey \"{key}\" ......".INFO();
                        BindHotkey(app, cmd_name, hotkey.Keys, hotkey.Command);
                    }
                }
#if DEBUG
                ApplicationHotKeys.HotkeyTriggered += ApplicationHotKeys_HotkeyTriggered;
                ApplicationHotKeys.AllModifiersReleasedAfterHotkey += ApplicationHotKeys_AllModifiersReleasedAfterHotkey;
                ApplicationHotKeys.ChordStartRecognized += ApplicationHotKeys_ChordStartRecognized;
                var hotkey_config = Path.Combine(Root, "HotKeys.json");
                if (!File.Exists(hotkey_config) && HotkeyConfig is List<HotKeyConfig>)
                {
                    var settings = new JsonSerializerSettings();
                    settings.TypeNameHandling = TypeNameHandling.Objects;
                    var text = JsonConvert.SerializeObject(HotkeyConfig, Formatting.Indented);
                    File.WriteAllText(hotkey_config, text, new UTF8Encoding(true));
                }
#endif
            }
        }
#if DEBUG
        private static void ApplicationHotKeys_ChordStartRecognized(ChordStartRecognizedEventArgs e)
        {
            $"Hotkey_ChordStartRecognized: {e.ChordSubpath}, {e.Subpath}".DEBUG();
        }

        private static void ApplicationHotKeys_AllModifiersReleasedAfterHotkey(HotKeyEventArgs e)
        {
            var key_name = string.IsNullOrEmpty(e.ChordName) ? string.Join("+", e.Keys.Select(k => k.ToString())) : e.ChordName;
            $"Hotkey_AllModifiersReleased: {e.Description}, Keys: {ApplicationCulture.TextInfo.ToTitleCase(key_name)}".DEBUG();
        }

        private static void ApplicationHotKeys_HotkeyTriggered(HotKeyEventArgs e)
        {
            var key_name = string.IsNullOrEmpty(e.ChordName) ? string.Join("+", e.Keys.Select(k => k.ToString())) : e.ChordName;
            $"Hotkey_Triggered: {e.Description}, Keys: {ApplicationCulture.TextInfo.ToTitleCase(key_name)}".DEBUG();
        }
#endif
        public static void UnbindHotkeys(this Application app)
        {
            try
            {
                if (ApplicationHotKeys is HotkeyCollection)
                {
                    //ApplicationHotKeys.StopListening();
                    var keys = ApplicationHotKeys.GetHotkeys();
                    foreach (var key in keys)
                    {
                        ApplicationHotKeys.UnregisterHotkey(key);
                    }
                    ApplicationHotKeys.Dispose();
                    ApplicationHotKeys = null;
                }
            }
            catch (Exception ex) { ex.Message.DEBUG("ERROR[HOTKEY]"); }
        }

        public static void RebindHotKeys(this Application app, bool full = true, bool global = false)
        {
            try
            {
                if (full)
                {
                    UnbindHotkeys(app);
                    BindHotkeys(app, global);
                }
                else
                {
                    if (ApplicationHotKeys is HotkeyCollection)
                    {
                        ApplicationHotKeys.StopListening();
                        ApplicationHotKeys.StartListening();
                    }
                }
            }
            catch (Exception ex) { ex.Message.DEBUG("ERROR[HOTKEY]"); }
        }

        public static void StartListening(this Application app)
        {
            ApplicationHotKeys.StartListening();
        }

        public static void StopListening(this Application app)
        {
            ApplicationHotKeys.StopListening();
        }
        #endregion

        #region Maybe reduce UI frozen
        private static object ExitFrame(object state)
        {
            ((DispatcherFrame)state).Continue = false;
            return null;
        }

        private static SemaphoreSlim CanDoEvents = new SemaphoreSlim(1, 1);
        public static async void DoEvents()
        {
            if (await CanDoEvents.WaitAsync(0))
            {
                try
                {
                    if (Application.Current.Dispatcher.CheckAccess())
                    {
                        await Dispatcher.Yield(DispatcherPriority.Render);
                        //await System.Windows.Threading.Dispatcher.Yield();

                        //DispatcherFrame frame = new DispatcherFrame();
                        //await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(ExitFrame), frame);
                        //Dispatcher.PushFrame(frame);
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        if (Application.Current.Dispatcher.CheckAccess())
                        {
                            DispatcherFrame frame = new DispatcherFrame();
                            //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate { }));
                            //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate { }));

                            //await Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrame), frame);
                            //await Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(ExitFrame), frame);
                            await Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, new DispatcherOperationCallback(ExitFrame), frame);
                            Dispatcher.PushFrame(frame);
                        }
                    }
                    catch (Exception)
                    {
                        await Task.Delay(1);
                    }
                }
                finally
                {
                    if (CanDoEvents is SemaphoreSlim && CanDoEvents.CurrentCount <= 0) CanDoEvents.Release();
                }
            }
        }

        public static void DoEvents(this object obj)
        {
            DoEvents();
        }

        public static void Sleep(int ms)
        {
            //Task.Delay(ms);
            for (int i = 0; i < ms; i += 10)
            {
                //Task.Delay(5);
                Thread.Sleep(5);
                DoEvents();
            }
        }

        public static void Sleep(this UIElement obj, int ms)
        {
            Sleep(ms);
        }

        public static async void Delay(int ms)
        {
            await Task.Delay(ms);
        }

        public static async Task DelayAsync(int ms)
        {
            await Task.Delay(ms);
        }

        public static void Delay(this object obj, int ms)
        {
            Delay(ms);
        }

        public static async Task DelayAsync(this object obj, int ms)
        {
            await DelayAsync(ms);
        }
        #endregion

        #region Network Common Helper
        public static HttpClient GetHttpClient(this Application app, bool continuation = false, long range_start = 0, long range_count = 0)
        {
            var setting = LoadSetting(app);
            var buffersize = 100 * 1024 * 1024;
            HttpClientHandler handler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.Deflate,
                UseCookies = true,
                MaxAutomaticRedirections = 15,
                //MaxConnectionsPerServer = 30,
                MaxRequestContentBufferSize = buffersize,
                //SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
                Proxy = string.IsNullOrEmpty(setting.Proxy) ? null : new WebProxy(setting.Proxy, true, setting.ProxyBypass),
                UseProxy = string.IsNullOrEmpty(setting.Proxy) || !setting.DownloadUsingProxy ? false : true
            };

            var httpClient = new HttpClient(handler, true)
            {
                Timeout = TimeSpan.FromSeconds(setting.DownloadHttpTimeout),
                MaxResponseContentBufferSize = buffersize
            };
            //httpClient.DefaultRequestHeaders.Add("Content-Type", "application/octet-stream");
            httpClient.DefaultRequestHeaders.Add("App-OS", "ios");
            httpClient.DefaultRequestHeaders.Add("App-OS-Version", "12.2");
            httpClient.DefaultRequestHeaders.Add("App-Version", "7.6.2");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "PixivIOSApp/7.6.2 (iOS 12.2; iPhone9,1)");
            //httpClient.DefaultRequestHeaders.Add("User-Agent", "PixivAndroidApp/5.0.64 (Android 6.0)");
            httpClient.DefaultRequestHeaders.Add("Referer", "https://app-api.pixiv.net/");
            //httpClient.DefaultRequestHeaders.Add("Connection", "Close");
            httpClient.DefaultRequestHeaders.Add("Connection", "Keep-Alive");
            //httpClient.DefaultRequestHeaders.Add("Keep-Alive", "300");
            //httpClient.DefaultRequestHeaders.ConnectionClose = true;
            if (continuation && range_start > 0)
            {
                var start = $"{range_start}";
                var end = range_count > 0 ? $"{range_count}" : string.Empty;
                httpClient.DefaultRequestHeaders.Add("Range", $"bytes={start}-{end}");
            }

            ///
            /// if httpclient throw exception of "send request error", maybe need add code like below line 
            ///
            //System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;//（ | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;）

            return (httpClient);
        }

        public static WebRequest GetWebRequest(this Application app, bool continuation = false, long range_start = 0, long range_count = 0)
        {
            var setting = LoadSetting(app);

            var webRequest = WebRequest.Create(string.Empty);
            webRequest.Proxy = string.IsNullOrEmpty(setting.Proxy) ? null : new WebProxy(setting.Proxy, true, setting.ProxyBypass);

            //webRequest.ContentType = "application/octet-stream";
            //webRequest.Headers.Add("Content-Type", "application/octet-stream");
            webRequest.Headers.Add("App-OS", "ios");
            webRequest.Headers.Add("App-OS-Version", "12.2");
            webRequest.Headers.Add("App-Version", "7.6.2");
            webRequest.Headers.Add("User-Agent", "PixivIOSApp/7.6.2 (iOS 12.2; iPhone9,1)");
            //webRequest.Headers.Add("User-Agent", "PixivAndroidApp/5.0.64 (Android 6.0)");
            webRequest.Headers.Add("Referer", "https://app-api.pixiv.net/");
            //webRequest.Headers.Add("Connection", "Close");
            webRequest.Headers.Add("Connection", "Keep-Alive");
            //webRequest.Headers.Add("Keep-Alive", "300");
            if (continuation)
            {
                var start = $"{range_start}";
                var end = range_count > 0 ? $"{range_count}" : string.Empty;
                webRequest.Headers.Add("Range", $"bytes={start}-{end}");
            }

            return (webRequest);
        }

        public static async Task<WebResponse> GetWebResponse(this Application app, bool continuation = false, long range_start = 0, long range_count = 0)
        {
            var client = GetWebRequest(app, continuation, range_start, range_count);
            return (await client.GetResponseAsync());
        }
        #endregion

        #region Default Preview/Avatar
        private static WriteableBitmap NullPreview = null;
        private static WriteableBitmap NullAvatar = null;
        private static WriteableBitmap NullThumbnail = null;

        public static BitmapSource GetNullPreview(this Application app)
        {
            if (NullPreview == null)
            {
                NullPreview = new WriteableBitmap(300, 300, DPI.Default.X, DPI.Default.Y, PixelFormats.Bgra32, BitmapPalettes.WebPalette);
            }
            return (NullPreview);
        }

        public static BitmapSource GetNullAvatar(this Application app)
        {
            if (NullAvatar == null)
            {
                NullAvatar = new WriteableBitmap(64, 64, DPI.Default.X, DPI.Default.Y, PixelFormats.Bgra32, BitmapPalettes.WebPalette);
            }
            return (NullAvatar);
        }

        public static BitmapSource GetNullThumbnail(this Application app)
        {
            if (NullThumbnail == null)
            {
                NullThumbnail = new WriteableBitmap(1, 1, DPI.Default.X, DPI.Default.Y, PixelFormats.Gray2, BitmapPalettes.WebPalette);
            }
            return (NullThumbnail);
        }

        public static Size DefaultThumbSize { get; set; } = new Size(128, 128);
        public static Size GetDefaultThumbSize(this Application app)
        {
            return (DefaultThumbSize);
        }

        public static Size DefaultAvatarSize { get; set; } = new Size(64, 64);
        public static Size GetDefaultAvatarSize(this Application app)
        {
            return (DefaultAvatarSize);
        }
        #endregion

        #region Invoke/InvokeAsync
        public static Dispatcher Dispatcher = Application.Current is Application ? Application.Current.Dispatcher : Dispatcher.CurrentDispatcher;
        public static Dispatcher AppDispatcher(this object obj)
        {
            if (Application.Current is Application)
                return (Application.Current.Dispatcher);
            else
                return (Dispatcher.CurrentDispatcher);
        }

        public static async void Invoke(this Action action, bool async = false, bool realtime = false)
        {
            if (action is Action)
            {
                Dispatcher dispatcher = action.AppDispatcher();
                if (async)
                {
                    if (realtime)
                        await dispatcher.BeginInvoke(action, DispatcherPriority.Send);
                    else
                        await dispatcher.BeginInvoke(action, DispatcherPriority.Background);
                }
                else
                    dispatcher.Invoke(action);
            }
        }

        public static async Task InvokeAsync(this Action action, bool realtime = false)
        {
            try
            {
                Dispatcher dispatcher = action.AppDispatcher();
                if (realtime)
                    await dispatcher.InvokeAsync(action, DispatcherPriority.Send);
                else
                    await dispatcher.InvokeAsync(action, DispatcherPriority.Background);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static async Task InvokeAsync(this Action action, CancellationToken cancelToken, bool realtime = false)
        {
            try
            {
                Dispatcher dispatcher = action.AppDispatcher();
                if (realtime)
                    await dispatcher.InvokeAsync(action, DispatcherPriority.Send, cancelToken);
                else
                    await dispatcher.InvokeAsync(action, DispatcherPriority.Background, cancelToken);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static async Task InvokeAsync(this Action action, DispatcherPriority priority)
        {
            try
            {
                Dispatcher dispatcher = action.AppDispatcher();
                await dispatcher.InvokeAsync(action, priority);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static async Task InvokeAsync(this Action action, DispatcherPriority priority, CancellationToken cancelToken)
        {
            try
            {
                Dispatcher dispatcher = action.AppDispatcher();
                await dispatcher.InvokeAsync(action, priority, cancelToken);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        #endregion

        #region AES Encrypt/Decrypt helper
        public static string AesEncrypt(this string text, string skey, bool auto = true)
        {
            string encrypt = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(skey) && !string.IsNullOrEmpty(text))
                {
                    var uni_skey = $"{ProcessorID}{skey}";
                    var uni_text = $"{ProcessorID}{text}";

                    AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
                    MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
                    SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider();
                    aes.Key = sha256.ComputeHash(Encoding.UTF8.GetBytes(uni_skey));
                    aes.IV = md5.ComputeHash(Encoding.UTF8.GetBytes(uni_skey));

                    byte[] dataByteArray = Encoding.UTF8.GetBytes(uni_text);
                    if (auto)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(aes.Key, aes.IV), CryptoStreamMode.Write))
                            {
                                using (StreamWriter sw = new StreamWriter(cs))
                                {
                                    sw.Write(uni_text);
                                }
                                encrypt = Convert.ToBase64String(ms.ToArray());
                            }
                        }
                    }
                    else
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(aes.Key, aes.IV), CryptoStreamMode.Write))
                            {
                                cs.Write(dataByteArray, 0, dataByteArray.Length);
                                cs.FlushFinalBlock();
                            }
                            encrypt = Convert.ToBase64String(ms.ToArray());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[AES]");
            }
            return encrypt;
        }

        public static string AesDecrypt(this string text, string skey, bool auto = true)
        {
            string decrypt = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(skey) && !string.IsNullOrEmpty(text))
                {
                    var uni_skey = $"{ProcessorID}{skey}";
                    var uni_text = string.Empty;

                    AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
                    MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
                    SHA256CryptoServiceProvider sha256 = new SHA256CryptoServiceProvider();
                    aes.Key = sha256.ComputeHash(Encoding.UTF8.GetBytes(uni_skey));
                    aes.IV = md5.ComputeHash(Encoding.UTF8.GetBytes(uni_skey));

                    byte[] dataByteArray = Convert.FromBase64String(text);
                    if (auto)
                    {
                        using (MemoryStream ms = new MemoryStream(dataByteArray))
                        {
                            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(aes.Key, aes.IV), CryptoStreamMode.Read))
                            {
                                using (StreamReader sr = new StreamReader(cs))
                                {
                                    uni_text = sr.ReadToEnd();
                                }
                            }
                        }
                    }
                    else
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(aes.Key, aes.IV), CryptoStreamMode.Write))
                            {
                                cs.Write(dataByteArray, 0, dataByteArray.Length);
                                cs.FlushFinalBlock();
                            }
                            uni_text = Encoding.UTF8.GetString(ms.ToArray());
                        }
                    }
                    if (uni_text.StartsWith(ProcessorID)) decrypt = uni_text.Replace($"{ProcessorID}", "");
                }
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[AES]");
            }
            return decrypt;
        }
        #endregion

        #region Keyboard helper
        private static List<Key> Modifier = new List<Key>() { Key.LeftCtrl, Key.RightCtrl, Key.LeftShift, Key.RightShift, Key.LeftAlt, Key.RightAlt, Key.LWin, Key.RWin };

        public static bool IsModified(this Application app, Key key)
        {
            return (Modifier.Contains(key) ? true : false);
        }

        private static bool IsModifiers(bool Ctrl, bool Shift, bool Alt, bool Win)
        {
            var hasModifiers = true;
            hasModifiers = hasModifiers && !(Ctrl ^ Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            hasModifiers = hasModifiers && !(Shift ^ Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            hasModifiers = hasModifiers && !(Alt ^ Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
            hasModifiers = hasModifiers && !(Win ^ Keyboard.Modifiers.HasFlag(ModifierKeys.Windows));
            return (hasModifiers);
        }

        public static bool IsModifiers(this IEnumerable<ModifierKeys> modifiers)
        {
            bool ctrl = false;
            bool shift = false;
            bool alt = false;
            bool win = false;
            bool none = !(ctrl || shift || alt || win);
            foreach (var modifier in modifiers)
            {
                switch (modifier)
                {
                    case ModifierKeys.None:
                        ctrl = shift = alt = win = false;
                        break;
                    case ModifierKeys.Alt:
                        alt = true;
                        break;
                    case ModifierKeys.Control:
                        ctrl = true;
                        break;
                    case ModifierKeys.Shift:
                        shift = true;
                        break;
                    case ModifierKeys.Windows:
                        win = true;
                        break;
                    default:
                        ctrl = shift = alt = win = false;
                        break;
                }
                none = !(ctrl || shift || alt || win);
                if (none) break;
            }
            return (IsModifiers(ctrl, shift, alt, win));
        }

        public static bool IsModified(this ModifierKeys modifier, bool only = false)
        {
            bool result = false;
            if (only)
                result = Keyboard.Modifiers == modifier;
            else
                result = Keyboard.Modifiers.HasFlag(modifier);
            return (result);
        }

        public static bool IsModified(this IEnumerable<ModifierKeys> modifiers, bool all = false)
        {
            bool result = false;
            foreach (var mod in modifiers)
            {
                if (all)
                    result = result && Keyboard.Modifiers.HasFlag(mod);
                else
                    result = result || Keyboard.Modifiers.HasFlag(mod);
            }
            return (result);
        }

        public static bool IsModified(this KeyEventArgs evt, IEnumerable<ModifierKeys> modifiers, bool all = false)
        {
            return (IsModified(modifiers, all));
        }

        public static bool IsModified(this KeyEventArgs evt, ModifierKeys modifier, bool only = true)
        {
            return (IsModified(modifier, only));
        }

        public static bool IsKey(this KeyEventArgs evt, Key key)
        {
            return (evt.Key == key || evt.SystemKey == key);
        }

        public static bool IsKey(this KeyEventArgs evt, Key key, ModifierKeys modifier, bool only = true)
        {
            return ((evt.Key == key || evt.SystemKey == key) && (only ? Keyboard.Modifiers == modifier : Keyboard.Modifiers.HasFlag(modifier)));
        }

        public static bool IsKey(this KeyEventArgs evt, Key key, IEnumerable<ModifierKeys> modifiers, bool only = true)
        {
            return ((evt.Key == key || evt.SystemKey == key) && IsModifiers(modifiers));
        }

        public static bool IsKey(this KeyEventArgs evt, Key key, bool Ctrl, bool Shift, bool Alt, bool Win)
        {
            return ((evt.Key == key || evt.SystemKey == key) && IsModifiers(Ctrl, Shift, Alt, Win));
        }
        #endregion
    }

    public static class CommonHelper
    {
        private static Setting setting = Application.Current.LoadSetting();
        private static CacheImage cache = new CacheImage();
        public static ConcurrentDictionary<long?, Pixeez.Objects.Work> IllustCache = new ConcurrentDictionary<long?, Pixeez.Objects.Work>();
        public static ConcurrentDictionary<long?, Pixeez.Objects.UserBase> UserCache = new ConcurrentDictionary<long?, Pixeez.Objects.UserBase>();
        public static ConcurrentDictionary<long?, Pixeez.Objects.UserInfo> UserInfoCache = new ConcurrentDictionary<long?, Pixeez.Objects.UserInfo>();

        public static ConcurrentDictionary<string, string> TagsCache = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> TagsT2S = new ConcurrentDictionary<string, string>();
        public static ConcurrentDictionary<string, string> TagsWildecardT2S = new ConcurrentDictionary<string, string>();

        public static DateTime SelectedDate { get; set; } = DateTime.Now;

        private static List<string> ext_imgs = new List<string>() { ".png", ".jpg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".jpeg" };
        internal static char[] trim_char = new char[] { ' ', ',', '.', '/', '\\', '\r', '\n', ':', ';' };
        internal static string[] trim_str = new string[] { Environment.NewLine };
        private static string regex_img_ext = @"\.(png|jpg|jpeg|gif|bmp|zip|webp)";

        #region Pixiv Token Helper
        private static SemaphoreSlim CanRefreshToken = new SemaphoreSlim(1, 1);
        private static async Task<Pixeez.Tokens> RefreshToken()
        {
            Pixeez.Tokens result = null;
            if (await CanRefreshToken.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                try
                {
                    setting = Application.Current.LoadSetting();
                    var authResult = await Pixeez.Auth.AuthorizeAsync(setting.User, setting.Pass, setting.RefreshToken, setting.Proxy, setting.ProxyBypass, setting.UsingProxy);
                    setting.AccessToken = authResult.Authorize.AccessToken;
                    setting.RefreshToken = authResult.Authorize.RefreshToken;
                    setting.ExpTime = authResult.Key.KeyExpTime.ToLocalTime();
                    setting.ExpiresIn = authResult.Authorize.ExpiresIn.Value;
                    setting.Update = DateTime.Now.ToFileTime().FileTimeToSecond();
                    setting.MyInfo = authResult.Authorize.User;
                    setting.Save();
                    result = authResult.Tokens;
                }
                catch (Exception ex)
                {
                    if (!string.IsNullOrEmpty(setting.User) && !string.IsNullOrEmpty(setting.Pass))
                    {
                        try
                        {
                            setting = Application.Current.LoadSetting();
                            var authResult = await Pixeez.Auth.AuthorizeAsync(setting.User, setting.Pass, setting.Proxy, setting.ProxyBypass, setting.UsingProxy);
                            setting.AccessToken = authResult.Authorize.AccessToken;
                            setting.RefreshToken = authResult.Authorize.RefreshToken;
                            setting.ExpTime = authResult.Key.KeyExpTime.ToLocalTime();
                            setting.ExpiresIn = authResult.Authorize.ExpiresIn.Value;
                            setting.Update = DateTime.Now.ToFileTime().FileTimeToSecond();
                            setting.MyInfo = authResult.Authorize.User;
                            setting.Save();
                            result = authResult.Tokens;
                        }
                        catch (Exception exx)
                        {
                            var ret = exx.Message;
                            var tokens = await ShowLogin();
                        }
                    }
                    var rt = ex.Message;
                }
                finally
                {
                    if (CanRefreshToken is SemaphoreSlim && CanRefreshToken.CurrentCount <= 0) CanRefreshToken.Release();
                }
            }
            return (result);
        }

        private static SemaphoreSlim CanShowLogin = new SemaphoreSlim(1, 1);
        public static async Task<Pixeez.Tokens> ShowLogin(bool force = false)
        {
            Pixeez.Tokens result = null;
            if (await CanShowLogin.WaitAsync(TimeSpan.FromSeconds(30)))
            {
                try
                {
                    if (GetWindow<PixivLoginDialog>() is MetroWindow) return (result);
                    Application.Current.DoEvents();
                    await Task.Delay(1);

                    setting = Application.Current.LoadSetting();
                    if (!force && setting.ExpTime > DateTime.Now && !string.IsNullOrEmpty(setting.AccessToken))
                    {
                        result = Pixeez.Auth.AuthorizeWithAccessToken(setting.AccessToken, setting.RefreshToken, setting.Proxy, setting.ProxyBypass, setting.UsingProxy);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(setting.User) && !string.IsNullOrEmpty(setting.Pass) && !string.IsNullOrEmpty(setting.RefreshToken))
                        {
                            try
                            {
                                result = await RefreshToken();
                            }
                            catch (Exception ex)
                            {
                                ex.ERROR("SHOWLOGIN");
                                result = Pixeez.Auth.AuthorizeWithAccessToken(setting.AccessToken, setting.RefreshToken, setting.Proxy, setting.ProxyBypass, setting.UsingProxy);
                            }
                        }
                        else
                        {
                            "Show Login Dialog ......".INFO();
                            Application.Current.DoEvents();
                            var dlgLogin = new PixivLoginDialog() { AccessToken=setting.AccessToken, RefreshToken=setting.RefreshToken };
                            var ret = dlgLogin.ShowDialog();
                            result = dlgLogin.Tokens;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ex.Message.ShowMessageBox("ERROR");
                }
                finally
                {
                    if (result == null) "Request Token Error!".ShowToast("ERROR");
                    if (CanShowLogin is SemaphoreSlim && CanShowLogin.CurrentCount <= 0) CanShowLogin.Release();
                }
            }
            return (result);
        }

        public static string AccessToken(this Application app)
        {
            var result = string.Empty;
            try
            {
                setting = app.LoadSetting();
                result = setting.AccessToken;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static string RefreshToken(this Application app)
        {
            var result = string.Empty;
            try
            {
                setting = app.LoadSetting();
                result = setting.RefreshToken;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static bool DownloadUsingToken(this Application app)
        {
            var setting = Application.Current.LoadSetting();
            return (setting.DownloadByAPI && !string.IsNullOrEmpty(setting.AccessToken) && setting.ExpTime <= DateTime.Now);
        }
        #endregion

        #region WebBrowser helper
        public static string GetText(this System.Windows.Forms.WebBrowser browser, bool html = false)
        {
            string result = string.Empty;
            try
            {
                if (browser is System.Windows.Forms.WebBrowser &&
                    browser.Document is System.Windows.Forms.HtmlDocument &&
                    browser.Document.DomDocument is mshtml.IHTMLDocument2)
                {
                    StringBuilder sb = new StringBuilder();
                    mshtml.IHTMLDocument2 document = browser.Document.DomDocument as mshtml.IHTMLDocument2;
                    mshtml.IHTMLSelectionObject currentSelection = document.selection;
                    if (currentSelection != null && currentSelection.type.Equals("Text", StringComparison.CurrentCultureIgnoreCase))
                    {
                        mshtml.IHTMLTxtRange range = currentSelection.createRange() as mshtml.IHTMLTxtRange;
                        if (range != null)
                            sb.AppendLine(html ? range.htmlText : range.text);
                    }
                    else
                    {
                        var bodies = browser.Document.GetElementsByTagName("body");
                        foreach (System.Windows.Forms.HtmlElement body in bodies)
                        {
                            sb.AppendLine(html ? body.InnerHtml : body.InnerText);
                        }
                    }
                    result = sb.Length > 0 ? sb.ToString().Trim() : string.Empty;
                }
            }
#if DEBUG
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
#else
            catch (Exception ex) { ex.ERROR(); }
#endif
            return (result);
        }

        #endregion

        #region Link parsing/genaration helper
        public static bool IsFile(this string text)
        {
            var result = false;
            Uri unc = null;
            var invalid = new List<char> { '<', ':', '>' };
            try
            {
                if (!string.IsNullOrEmpty(text) && !invalid.Contains(text.FirstOrDefault()) && Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out unc))
                {
                    result = unc.IsFile;
                }
            }
            catch (Exception) { }
            return (result);
        }

        public static IList<string> ParseDragContent(this DragEventArgs e)
        {
            List<string> links = new List<string>();

            var fmts = new List<string>(e.Data.GetFormats(true));

            var str = fmts.Contains("System.String") ? (string)e.Data.GetData("System.String") : string.Empty;
            var text = fmts.Contains("Text") ? (string)e.Data.GetData("Text") : string.Empty;
            var unicode = fmts.Contains("UnicodeText") ? (string)e.Data.GetData("UnicodeText") : string.Empty;

            if (fmts.Contains("text/html"))
            {
                using (var ms = (MemoryStream)e.Data.GetData("text/html"))
                {
                    var bytes = ms.ToArray();
                    var IsUnicode = bytes.Length>=4 && bytes[1] == 0x00 && bytes[3] == 0x00;
                    if (IsUnicode)
                    {
                        var html = Encoding.Unicode.GetString(bytes).Trim().Trim('\0');
                        links = html.ParseLinks(true).ToList();
                    }
                    else
                    {
                        var html = Encoding.Unicode.GetString(bytes).Trim().Trim('\0');
                        if (!string.IsNullOrEmpty(text) && html.Contains(text))
                            links = html.ParseLinks(true).ToList();
                        else
                        {
                            html = Encoding.UTF8.GetString(ms.ToArray()).Trim().Trim('\0');
                            links = html.ParseLinks(true).ToList();
                        }
                    }
                }
            }
            else if (fmts.Contains("System.String"))
            {
                var html = ((string)e.Data.GetData("System.String")).Trim().Trim('\0');
                links = html.ParseLinks(false).ToList();
            }
            else if (fmts.Contains("UnicodeText"))
            {
                var html = ((string)e.Data.GetData("UnicodeText")).Trim().Trim('\0');
                links = html.ParseLinks(false).ToList();
            }
            else if (fmts.Contains("Text"))
            {
                var html = ((string)e.Data.GetData("Text")).Trim().Trim('\0');
                links = html.ParseLinks(false).ToList();
            }
            else if (fmts.Contains("FileDrop"))
            {
                var files = (string[])(e.Data.GetData("FileDrop"));
                links = string.Join(Environment.NewLine, files).ParseLinks(false).ToList();
            }
            return (links);
        }

        public static string ParseID(this string searchContent)
        {
            var patten =  @"((UserID)|(IllustID)|(User)|(Tag)|(Caption)|(Fuzzy)|(Fuzzy Tag)|(Downloading)):\s*(.*?)$";
            string result = searchContent;
            if (!string.IsNullOrEmpty(result))
            {
                result = Regex.Replace(result, patten, "$10", RegexOptions.IgnoreCase).Trim().Trim(trim_char);
            }
            return (result);
        }

        public static string ParseLink(this string link)
        {
            string result = link;

            if (!string.IsNullOrEmpty(link))
            {
                if (Regex.IsMatch(result, @"((UserID)|(IllustID)):( )*(\d+)", RegexOptions.IgnoreCase))
                    result = result.Trim();

                else if (Regex.IsMatch(result, @"(.*?/artworks/)(\d+)(.*)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?/artworks/)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?illust_id=)(\d+)(.*)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?illust_id=)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?/pixiv\.navirank\.com/id/)(\d+)(.*)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?/id/)(\d+)(.*)", "IllustID: $2", RegexOptions.IgnoreCase);

                else if (Regex.IsMatch(result, @"^(.*?\.pixiv.net/users/)(\d+)(.*)$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?\.pixiv.net/users/)(\d+)(.*)$", "UserID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"^(.*?\.pixiv.net/fanbox/creator/)(\d+)(.*)$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?\.pixiv.net/fanbox/creator/)(\d+)(.*)$", "UserID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"^(.*?\?id=)(\d+)(.*)$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?\?id=)(\d+)(.*)$", "UserID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?/pixiv\.navirank\.com/user/)(\d+)(.*)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?/user/)(\d+)(.*)", "UserID: $2", RegexOptions.IgnoreCase);

                else if (Regex.IsMatch(result, @"^(.*?tag_full&word=)(.*)$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?tag_full&word=)(.*)$", "Tag: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?\.pixiv\.net/tags/)(.*?){1}(/.*?)*$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?/tags/)(.*?){1}(/.*?)*", "Tag: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"(.*?/pixiv\.navirank\.com/tag/)(.*?)", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"(.*?/tag/)(.*?)", "Tag: $2", RegexOptions.IgnoreCase);

                else if (Regex.IsMatch(result, @"^(.*?/img-.*?/)(\d+)(_p\d+.*?" + regex_img_ext + ")$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?/img-.*?/)(\d+)(_p\d+.*?" + regex_img_ext + ")$", "IllustID: $2", RegexOptions.IgnoreCase);
                else if (Regex.IsMatch(result, @"^(.*?)/\d{4}/\d{2}/\d{2}/\d{2}/\d{2}/\d{2}/(\d+).*?" + regex_img_ext + "$", RegexOptions.IgnoreCase))
                    result = Regex.Replace(result, @"^(.*?)/\d{4}/\d{2}/\d{2}/\d{2}/\d{2}/\d{2}/(\d+).*?" + regex_img_ext + "$", "IllustID: $2", RegexOptions.IgnoreCase);


                else if (Regex.IsMatch(Path.GetFileNameWithoutExtension(result), @"^((\d+)(_((p)|(ugoira))*\d+)*)"))
                    result = Regex.Replace(Path.GetFileNameWithoutExtension(result), @"(.*?(\d+)(_((p)|(ugoira))*\d+)*.*)", "$2", RegexOptions.IgnoreCase);

                else if (!Regex.IsMatch(result, @"((UserID)|(User)|(IllustID)|(Tag)|(Caption)|(Fuzzy)|(Fuzzy Tag)):", RegexOptions.IgnoreCase))
                    result = $"Fuzzy: {result}";
            }

            return (result.Trim().Trim(trim_char).HtmlDecode());
        }

        public static IList<string> ParseLinks(this string html, bool is_src = false)
        {
            List<string> links = new List<string>();
            var href_prefix_0 = is_src ? @"href=""" : string.Empty;
            var href_prefix_1 = is_src ? @"src=""" : string.Empty;
            var href_suffix = is_src ? @"""" : @"";
            var cmd_sep = new char[] { ':', ' ', '=' };

            var opt = RegexOptions.IgnoreCase;// | RegexOptions.Multiline;

            var mr = new List<MatchCollection>();
            foreach (var text in html.Split(new string[] { Environment.NewLine, "\n", "\r", "\t", "<br/>", "<br>", "<br />", "><", "</a>" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var content = text.StartsWith("\"") && text.EndsWith("\"") ? text.Trim('"').Trim() : text.Trim();
                if (string.IsNullOrEmpty(content)) continue;
                else if (content.Equals("<a", StringComparison.CurrentCultureIgnoreCase)) continue;
                else if (content.Equals("<img", StringComparison.CurrentCultureIgnoreCase)) continue;
                else if (content.Equals(">", StringComparison.CurrentCultureIgnoreCase)) continue;

                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/artworks/\d+)" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/users/\d+)" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/member.*?\.php\?.*?illust_id=\d+).*?" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/member.*?\.php\?id=\d+).*?" + href_suffix, opt));

                mr.Add(Regex.Matches(content, href_prefix_0 + @"(.*?\.pximg\.net/img-.*?/\d+_p\d+" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(.*?\.pximg\.net/img-.*?/(\d+)_p\d+.*?" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(.*?\.pximg\.net/.*?/img(-(master|original))?/.*?/\d+_p\d+(_.*?)?" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://.*?\.pximg\.net/.*?/img/\d{4}/\d{2}/\d{2}/\d{2}/\d{2}/\d{2}/(\d+)_p\d+.*?" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_1 + @"(.*?\.pximg\.net/img-.*?/\d+_p\d+" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_1 + @"(.*?\.pximg\.net/img-.*?/(\d+)_p\d+.*?" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_1 + @"(.*?\.pximg\.net/.*?/img/.*?/\d+_p\d+" + regex_img_ext + ")" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_1 + @"(https?://.*?\.pximg\.net/.*?/img/\d{4}/\d{2}/\d{2}/\d{2}/\d{2}/\d{2}/(\d+)_p\d+.*?" + regex_img_ext + ")" + href_suffix, opt));

                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://www\.pixiv\.net/fanbox/creator/\d+).*?" + href_suffix, opt));

                mr.Add(Regex.Matches(content, href_prefix_0 + @"https?://.*?\.pixiv\.net/(tags/(.*?){1})(/.*?)*$" + href_suffix, opt));

                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://pixiv\.navirank\.com/id/\d+).*?" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://pixiv\.navirank\.com/user/\d+).*?" + href_suffix, opt));
                mr.Add(Regex.Matches(content, href_prefix_0 + @"(https?://pixiv\.navirank\.com/tag/.*?/)" + href_suffix, opt));

                mr.Add(Regex.Matches(content, @"[\\|/](background|workspace|user-profile)[\\|/].*?[\\|/]((\d+)(_.{10,}" + regex_img_ext + "))", opt));

                mr.Add(Regex.Matches(content, @"^(\d+)([_]*.*?)" + regex_img_ext + "$", opt));

                mr.Add(Regex.Matches(content, @"^((illust|illusts|artworks)/(\d+))", opt));
                mr.Add(Regex.Matches(content, @"^((users?)/(\d+))", opt));

                mr.Add(Regex.Matches(content, @"^((u?id):[ ]*(\d+)+)", opt));
                mr.Add(Regex.Matches(content, @"^((user|fuzzy|tag|title):[ ]*(.+)+)", opt));

                mr.Add(Regex.Matches(content, @"(Searching\s)(.*?)$", opt));

                mr.Add(Regex.Matches(content, @"(Preview\sID:\s)(\d+),(.*?)$", opt));

                mr.Add(Regex.Matches(content, @"((down(all)?|Downloading):\s?.*?)$", opt));

                if (!Regex.IsMatch(content, @"^((https?)|(<a)|(href=)|(src=)|(id:)|(uid:)|(tag:)|(user:)|(title:)|(fuzzy:)|(down(all|load(ing)?)?:)|(illust/)|(illusts/)|(artworks/)|(user/)|(users/)).*?", opt))
                {
                    try
                    {
                        if (content.IsFile())
                        {
                            var ap = Path.GetFullPath(content).Replace('\\', '/');
                            var root = Path.GetPathRoot(ap);
                            var IsFile = root.Length == 3 && string.IsNullOrEmpty(Path.GetExtension(ap)) ? false : true;
                            if (IsFile)
                            {
                                if (Regex.IsMatch(ap, @"[\\|/]((background)(workspace)|(user-profile))[\\|/].*?[\\|/]((\d+)(_.{10,}" + regex_img_ext + "))", opt))
                                    mr.Add(Regex.Matches(ap, @"[\\|/]((workspace)|(user-profile))[\\|/].*?[\\|/]((\d+)(_.{10,}" + regex_img_ext + "))", opt));
                                else
                                    mr.Add(Regex.Matches(Path.Combine(root, Path.GetFileName(content)), @"((\d+)((_((p)|(ugoira))*\d+)*(_((master)|(square))+\d+)*)*(\..+)*)", opt));
                            }
                            else
                                mr.Add(Regex.Matches(content, @"((\d+)((_((p)|(ugoira))*\d+)*(_((master)|(square))+\d+)*)*(\..+)*)", opt));
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.ERROR();
                        mr.Add(Regex.Matches(content, @"((\d+)((_((p)|(ugoira))*\d+)*(_((master)|(square)))*\d+)*(" + regex_img_ext + "))", opt));
                    }
                }
            }

            var download_links = new List<string>();
            foreach (var mi in mr)
            {
                if (mi.Count <= 0) continue;
                else if (mi.Count > 50)
                {
                    ShowMessageBox("There are too many links, which may cause the program to crash and cancel the operation.", "WARNING");
                    continue;
                }
                var linkexists = false;

                foreach (Match m in mi)
                {
                    var link = m.Groups[1].Value.Trim().Trim(trim_char);
                    var downloads = Application.Current.OpenedWindowTitles();
                    downloads = downloads.Concat(download_links).ToList();
                    foreach (var di in downloads)
                    {
                        if (di.Contains(link))
                        {
                            linkexists = true;
                            break;
                        }
                    }
                    if (linkexists) continue;

                    if (link.Equals("user-profile", StringComparison.CurrentCultureIgnoreCase)) break;
                    else if (link.Equals("background", StringComparison.CurrentCultureIgnoreCase) || link.Equals("workspace", StringComparison.CurrentCultureIgnoreCase))
                        link = $"uid:{m.Groups[5].Value.Trim().Trim(trim_char)}";

                    if (link.StartsWith("searching", StringComparison.CurrentCultureIgnoreCase))
                    {
                        link = m.Groups[2].Value.Trim();
                        if (!links.Contains(link)) links.Add(link);
                    }
                    else if (link.StartsWith("preview", StringComparison.CurrentCultureIgnoreCase))
                    {
                        link = m.Groups[2].Value.Trim().ArtworkLink();
                        if (!string.IsNullOrEmpty(link) && !links.Contains(link)) links.Add(link);
                    }
                    else if (link.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                    {
                        //link = Uri.UnescapeDataString(WebUtility.HtmlDecode(link));
                        if (Regex.IsMatch(link, @"(\d+)(_.*?)?" + regex_img_ext + "$", RegexOptions.IgnoreCase))
                        {
                            var id = Regex.Replace(link, @"^.*?/\d{2}/(\d+)(_.*?)?"+regex_img_ext+"$", "$1", RegexOptions.IgnoreCase);
                            link = id.ArtworkLink();
                        }
                        if (!links.Contains(link)) links.Add(link);
                    }
                    else if (link.StartsWith("id:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var id = link.Substring(3).Trim();
                        if (Regex.IsMatch(id, @"(\d+),\s(.+?)", opt))
                            id = Regex.Replace(id, @"(\d+),\s(.+?)", "$1", opt);
                        var a_link = id.ArtworkLink();
                        var a_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={id}";
                        if (!string.IsNullOrEmpty(a_link) && !links.Contains(a_link) && !links.Contains(a_link_o)) links.Add(a_link);
                    }
                    else if (link.StartsWith("illust/", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("illusts/", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("artworks/", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var id = Regex.Replace(link, @"(((illust)|(illusts)|(artworks))/(\d+))", "$6", opt).Trim();
                        var a_link = id.ArtworkLink();
                        var a_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={id}";
                        if (!string.IsNullOrEmpty(a_link) && !links.Contains(a_link) && !links.Contains(a_link_o)) links.Add(a_link);
                    }
                    else if (link.StartsWith("uid:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var id = link.Substring(4).Trim();
                        var u_link = id.ArtistLink();
                        var u_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&id={id}";
                        if (!string.IsNullOrEmpty(u_link) && !links.Contains(u_link) && !links.Contains(u_link_o)) links.Add(u_link);
                    }
                    else if (link.StartsWith("user/", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("users/", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var id = Regex.Replace(link, @"(((user)|(users))/(\d+))", "$5", opt).Trim();
                        var u_link = id.ArtistLink();
                        var u_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&id={id}";
                        if (!string.IsNullOrEmpty(u_link) && !links.Contains(u_link) && !links.Contains(u_link_o)) links.Add(u_link);
                    }
                    //(UserID)|(User)|(IllustID)|(Tag)|(Caption)|(Fuzzy)|(Fuzzy Tag)
                    else if (link.StartsWith("tag:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var tag = link.Substring(4).Trim();
                        var t_link = $"Tag:{tag}";
                        if (!links.Contains(t_link)) links.Add(t_link);
                    }
                    else if (link.StartsWith("tags/", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var tag = link.Substring(5).Trim();
                        var t_link = $"Tag:{Uri.UnescapeDataString(tag)}";
                        if (!links.Contains(t_link)) links.Add(t_link);
                    }
                    else if (link.StartsWith("user:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var user = link.Substring(5).Trim();
                        if (Regex.IsMatch(user, @"(.+)\s/\s(\d+)\s/\s(.+)", opt))
                        {
                            var uid = Regex.Replace(user, @"(.+)\s/\s(\d+)\s/\s(.+)", "$2", opt);
                            var u_link = uid.ArtistLink();
                            var u_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&id={uid}";
                            if (!string.IsNullOrEmpty(u_link) && !links.Contains(u_link) && !links.Contains(u_link_o)) links.Add(u_link);
                        }
                        else
                        {
                            var u_link = $"User:{user}";
                            if (!links.Contains(u_link)) links.Add(u_link);
                        }
                    }
                    else if (link.StartsWith("fuzzy:", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("title:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var fuzzy = link.Substring(6).Trim();
                        var f_link = $"Fuzzy:{fuzzy}";
                        if (!links.Contains(f_link)) links.Add(f_link);
                    }
                    else if (link.StartsWith("searching ", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("searching:", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("search ", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("search:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var search = Regex.Replace(link, @"search.*?[:\s]+(.*?)", "$1", RegexOptions.IgnoreCase).Trim().TrimEnd('.').Trim();
                        var s_link = $"{search}";
                        if (!links.Contains(s_link)) links.Add(s_link);
                    }
                    else if (link.StartsWith("down:", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("download:", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("downloading:", StringComparison.CurrentCultureIgnoreCase) ||
                             link.StartsWith("downloading ", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var down = Regex.Replace(link, @"down.*?[:\s]+(.*?)", "$1", RegexOptions.IgnoreCase).Trim().TrimEnd('.').Trim();
                        var exists = download_links.Where(l=>l.Contains(down)).Count();
                        if (exists <= 0) download_links.Add($"down:{down}");
                    }
                    else if (link.StartsWith("downall:", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var down = link.Substring(8).Trim().TrimEnd('.').Trim();
                        var exists = download_links.Where(l=>l.Contains(down)).Count();
                        if (exists <= 0) download_links.Add($"downall:{down}");
                    }
                    else if (link.Equals("downloading", StringComparison.CurrentCultureIgnoreCase)) continue;
                    else
                    {
                        var fn = m.Value.Trim().Trim(trim_char);
                        try
                        {
                            var sid = Regex.Replace(Path.GetFileNameWithoutExtension(fn), @"(.*?(\d+)(_((p)|(ugoira))*\d+)*.*)", "$2", RegexOptions.IgnoreCase);
                            var IsFile = string.IsNullOrEmpty(Path.GetExtension(fn)) ? false : true;
                            long id;
                            if (long.TryParse(sid, out id) && id > 100)
                            {
                                var a_link = id.ArtworkLink();
                                var a_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&illust_id={id}";
                                if (!links.Contains(a_link) && !links.Contains(a_link_o)) links.Add(a_link);

                                if (!IsFile)
                                {
                                    var u_link = id.ArtistLink();
                                    var u_link_o = $"https://www.pixiv.net/member_illust.php?mode=medium&id={id}";
                                    if (!links.Contains(u_link) && !links.Contains(u_link_o)) links.Add(u_link);
                                }
                            }
                        }
                        catch { }
                    }
                }
                if (linkexists) continue;
            }
            if (links.Count <= 0)
            {
                if (html.Split(Path.GetInvalidPathChars()).Length <= 1 && download_links.Count <= 0) links.Add($"Fuzzy:{html}");
                foreach (var dl in download_links) links.Add(dl);
            }
            return (links);
        }

        public static string ArtworkLink(this string id)
        {
            long iid = -1;
            return (string.IsNullOrEmpty(id) || !long.TryParse(id, out iid) || iid < 0 ? string.Empty : $"https://www.pixiv.net/artworks/{id}");
        }

        public static string ArtworkLink(this long id)
        {
            return (id < 0 ? string.Empty : $"https://www.pixiv.net/artworks/{id}");
        }

        public static string ArtistLink(this string id)
        {
            long uid = -1;
            return (string.IsNullOrEmpty(id) || !long.TryParse(id, out uid) || uid < 0 ? string.Empty : $"https://www.pixiv.net/users/{id}");
        }

        public static string ArtistLink(this long id)
        {
            return (id < 0 ? string.Empty : $"https://www.pixiv.net/users/{id}");
        }

        public static string TagLink(this string tag)
        {
            return (string.IsNullOrEmpty(tag) ? string.Empty : Uri.EscapeUriString($"https://www.pixiv.net/tags/{tag}"));
        }
        #endregion

        #region Text process routines
        public static string TranslatedText(this string src, string translated = default(string))
        {
            var result = src;
            try
            {
                src = string.IsNullOrEmpty(src) ? string.Empty : src.Trim();
                translated = string.IsNullOrEmpty(translated) ? string.Empty : translated.Trim();
                if (string.IsNullOrEmpty(src)) return (string.Empty);

                result = src;
                if (TagsCache is ConcurrentDictionary<string, string>)
                {
                    if (string.IsNullOrEmpty(translated) || src.Equals(translated, StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (TagsCache.ContainsKey(src))
                        {
                            var tag_t = TagsCache[src];
                            if (!string.IsNullOrEmpty(tag_t)) result = tag_t;
                        }
                    }
                    else
                    {
                        TagsCache[src] = translated;
                        result = translated;
                    }
                }

                if (TagsT2S is ConcurrentDictionary<string, string>)
                {
                    if (TagsT2S.ContainsKey(src)) result = TagsT2S[src];
                    else if (TagsT2S.ContainsKey(result)) result = TagsT2S[result];

                    var pattern = $@"/{src}/";
                    if (TagsT2S.ContainsKey(pattern))
                        result = Regex.Replace(result, src, TagsT2S[pattern], RegexOptions.IgnoreCase);
                }

                if (TagsWildecardT2S is ConcurrentDictionary<string, string>)
                {
                    //List<int> escapes = new List<int>();
                    //for(var i = 0; i<result.Length;i++)
                    //{
                    //    if (result[i] == '\\') escapes.Add(i);
                    //}
                    result = result.Replace("\\", "");

                    var alpha = Regex.IsMatch(result, @"^[\u0020-\u007E]*$", RegexOptions.IgnoreCase);
                    var text = alpha ? src : result;
                    foreach (var kv in TagsWildecardT2S)
                    {
                        var k = kv.Key.Replace(" ", "\\s");
                        var v = kv.Value;
                        if (text.IndexOf(v, 0, StringComparison.OrdinalIgnoreCase) < 0)
                            text = Regex.Replace(text, $@"{k.Trim('/')}", v, RegexOptions.IgnoreCase);

                        if (k.StartsWith("/") && k.EndsWith("/"))
                        {
                            text = Regex.Replace(text, $@"{k.Trim('/')}", v, RegexOptions.IgnoreCase);
                            result = Regex.Replace(result, $@"{k.Trim('/')}", v, RegexOptions.IgnoreCase);
                        }
                    }
                    result = alpha && !Regex.IsMatch(text, result, RegexOptions.IgnoreCase) ? $"{text}/{result}" : text;
                }
            }
            catch (Exception ex) { ex.ERROR("TRANSLATE"); }
            return (result);
        }

        public static string InsertLineBreak(this string text, int lineLength)
        {
            if (string.IsNullOrEmpty(text)) return (string.Empty);
            //return Regex.Replace(text, @"(.{" + lineLength + @"})", "$1" + Environment.NewLine);
            var t = text.HtmlFormatBreakLine(false);// Regex.Replace(text, @"[\n\r]", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            //t = Regex.Replace(t, @"<[^>]*>", "$1", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"(<br *?/>)", Environment.NewLine, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"(<a .*?>(.*?)</a>)|(<strong>(.*?)</strong>)", "$2", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"<.*?>(.*?)</.*?>", "$1", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            //return Regex.Replace(t, @"(.{" + lineLength + @"})", "$1" + Environment.NewLine, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var ts = t.Split(new string[]{Environment.NewLine}, StringSplitOptions.None);
            for (var i = 0; i < ts.Length; i++)
            {
                if (ts[i].Length > lineLength) ts[i] = Regex.Replace(ts[i], @"(.{" + lineLength + @"})", "$1" + Environment.NewLine, RegexOptions.IgnoreCase);
            }
            return (string.Join(Environment.NewLine, ts));
        }

        public static string HtmlEncode(this string text)
        {
            if (string.IsNullOrEmpty(text)) return (string.Empty);
            else return (WebUtility.HtmlEncode(text));
        }

        public static string HtmlDecode(this string text, bool br = true)
        {
            string result = text;
            if (!string.IsNullOrEmpty(result))
            {
                var patten = new Regex(@"&(amp;)?#(([0-9]{1,6})|(x([a-fA-F0-9]{1,5})));", RegexOptions.IgnoreCase);
                //result = WebUtility.UrlDecode(WebUtility.HtmlDecode(result));
                result = Uri.UnescapeDataString(WebUtility.HtmlDecode(result));
                foreach (Match match in patten.Matches(result))
                {
                    var v = Convert.ToInt32(match.Groups[2].Value);
                    if (v > 0xFFFF)
                        result = result.Replace(match.Value, char.ConvertFromUtf32(v));
                }
                result = result.HtmlFormatBreakLine(br);
            }
            return (result);
        }

        public static string HtmlFormatBreakLine(this string text, bool br = true)
        {
            var result = text.Replace("\r\n", "<br/>").Replace("\n\r", "<br/>").Replace("\r", "<br/>").Replace("\n", "<br/>");
            if (br) result = result.Replace("<br/>", $"<br/>{Environment.NewLine}");
            else result = result.Replace("<br/>", Environment.NewLine);
            return (result);
        }

        /// <summary>
        /// How To Convert HTML To Formatted Plain Text
        /// source: http://www.beansoftware.com/ASP.NET-Tutorials/Convert-HTML-To-Plain-Text.aspx
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        public static string HtmlToText(this string html)
        {
            string result = string.Copy(html);
            try
            {
                // Remove new lines since they are not visible in HTML
                result = result.HtmlFormatBreakLine(false);//.Replace("\n", " ");

                // Remove tab spaces
                result = result.Replace("\t", " ");

                // Remove multiple white spaces from HTML
                result = Regex.Replace(result, " +", " ");

                // Remove HEAD tag
                result = Regex.Replace(result, "<head.*?</head>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                // Remove any JavaScript
                result = Regex.Replace(result, "<script.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                // Replace special characters like &, <, >, " etc.
                StringBuilder sb = new StringBuilder(result);
                // Note: There are many more special characters, these are just
                // most common. You can add new characters in this arrays if needed
                string[] OldWords = {"&nbsp;", "&amp;", "&quot;", "&lt;", "&gt;", "&reg;", "&copy;", "&bull;", "&trade;"};
                string[] NewWords = {" ", "&", "\"", "<", ">", "®", "©", "•", "™"};
                for (int i = 0; i < OldWords.Length; i++)
                {
                    sb.Replace(OldWords[i], NewWords[i]);
                }

                // Check if there are line breaks (<br>) or paragraph (<p>)
                sb.Replace("<br>", "\n<br>");
                sb.Replace("<br ", "\n<br ");
                sb.Replace("<p ", "\n<p ");
                result = Regex.Replace(sb.ToString(), "<[^>]*>", "");
            }
            catch (Exception ex) { ex.ERROR(); result = html.HtmlDecode(false); }
            return result;
        }

        public static string GetDefaultTemplate()
        {
            var result = string.Empty;
            if (setting is Setting)
            {
                var template = string.IsNullOrEmpty(setting.ContentsTemplete) ? string.Empty : setting.ContentsTemplete;
                if (string.IsNullOrEmpty(template))
                {
                    if (string.IsNullOrEmpty(setting.CustomContentsTemplete))
                    {
                        StringBuilder html = new StringBuilder();
                        html.AppendLine("<!DOCTYPE html>");
                        html.AppendLine("<HTML>");
                        html.AppendLine("  <HEAD>");
                        html.AppendLine("    <TITLE>{% title %}</TITLE>");
                        html.AppendLine("    <META http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\" />");
                        html.AppendLine("    <META http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />");
                        html.AppendLine("    <STYLE>");
                        html.AppendLine("      :root { --accent: {% accentcolor_rgb %}; --text: {% textcolor_rgb %} }");
                        html.AppendLine("      *{font-family:\"等距更纱黑体 SC\", FontAwesome, \"Segoe UI Emoji\", \"Segoe MDL2 Assets\", \"Segoe UI\", Iosevka, \"Sarasa Mono J\", \"Sarasa Term J\", \"Sarasa Gothic J\", \"更纱黑体 SC\", 思源黑体, 思源宋体, 微软雅黑, 宋体, 黑体, 楷体, Consolas, \"Courier New\", Tahoma, Arial, Helvetica, sans-serif !important;}");
                        html.AppendLine("      body{background-color: {% backcolor %} !important;}");
                        html.AppendLine("      a:link{color:{% accentcolor %} !important;text-decoration:none !important;}");
                        html.AppendLine("      a:hover{color:{% accentcolor %} !important;text-decoration:none !important;}");
                        html.AppendLine("      a:active{color:{% accentcolor %} !important;text-decoration:none !important;}");
                        html.AppendLine("      a:visited{color:{% accentcolor %} !important;text-decoration:none !important;}");
                        html.AppendLine("      img{width:auto!important;height:auto!important;max-width:100%!important;max-height:100% !important;}");
                        html.AppendLine("      .tag{color:{% accentcolor %} !important;background-color:rgba(var(--accent), 10%);line-height:1.6em;padding:0 2px 0 1px;text-decoration:none;border:1px solid {% accentcolor %};border-left-width:5px;overflow-wrap:break-word;}");
                        html.AppendLine("      .tag.::before{ content: '#'; }");
                        html.AppendLine("      .desc{color:{% textcolor %} !important;text-decoration:none !important;width: 99% !important;word-wrap: break-word !important;overflow-wrap: break-word !important;white-space:normal !important;}");
                        html.AppendLine("      .twitter::before{font-family:FontAwesome; content:''; margin-left:3px; padding-right:4px; color: #1da1f2;}");
                        html.AppendLine("      .web::before{content:'🌐'; padding-right:3px; margin-left:-0px;}");
                        html.AppendLine("      .mail::before{content:'🖃'; padding-right:4px; margin-left:2px;}");
                        html.AppendLine("      .E404{display:block; min-height:calc(95vh); background-image:url('{% site %}/404.jpg'); background-position: center; background-attachment: fixed; background-repeat: no-repeat;}");
                        html.AppendLine("      .E404T{font-size:calc(2.5vw); color:gray; position:fixed; margin-left:calc(50vw); margin-top:calc(50vh);}");
                        html.AppendLine();
                        html.AppendLine("      @media screen and(-ms-high-contrast: active), (-ms-high-contrast: none) {");
                        html.AppendLine("      .tag{color:{% accentcolor %} !important;background-color:rgba({% accentcolor_rgb %}, 0.1);line-height:1.6em;padding:0 2px 0 1px;text-decoration:none;border:1px solid {% accentcolor %};border-left-width:5px;overflow-wrap:break-word;}");
                        html.AppendLine("      }");
                        html.AppendLine("    </STYLE>");
                        html.AppendLine("    <SCRIPT>");
                        html.AppendLine("      window.alert = function () { }");
                        html.AppendLine("    </SCRIPT>");
                        html.AppendLine("  </HEAD>");
                        html.AppendLine("<BODY>");
                        html.AppendLine("{% contents %}");
                        html.AppendLine("</BODY>");
                        html.AppendLine("</HTML>");

                        result = html.ToString();

                        File.WriteAllText(setting.ContentsTemplateFile, result);
                    }
                    else
                    {
                        result = setting.CustomContentsTemplete;
                    }

                    if (!setting.ContentsTemplete.Equals(result))
                    {
                        setting.ContentsTemplete = result;
                        setting.Save();
                    }
                }
                else
                {
                    result = template;
                }
            }
            return (result);
        }

        public static string GetHtmlFromTemplate(this string contents, string title = "")
        {
            var backcolor = Theme.WhiteColor.ToHtml();
            if (backcolor.StartsWith("#FF") && backcolor.Length > 6) backcolor = backcolor.Replace("#FF", "#");
            else if (backcolor.StartsWith("#00") && backcolor.Length > 6) backcolor = backcolor.Replace("#00", "#");
            var accentcolor = Theme.AccentBaseColor.ToHtml(false);
            var accentcolor_rgb = Theme.AccentBaseColor.ToRGB(false, false);
            var textcolor = Theme.TextColor.ToHtml(false);
            var textcolor_rgb = Theme.TextColor.ToRGB(false, false);

            contents = string.IsNullOrEmpty(contents) ? string.Empty : contents.Trim();
            title = string.IsNullOrEmpty(title) ? string.Empty : title.Trim();

            var template = GetDefaultTemplate();
            template = Regex.Replace(template, @"{%\s*?site\s*?%}", new Uri(Application.Current.GetRoot()).AbsoluteUri, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?title\s*?%}", title, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?backcolor\s*?%}", backcolor, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?accentcolor\s*?%}", accentcolor, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?accentcolor_rgb\s*?%}", accentcolor_rgb, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?textcolor\s*?%}", textcolor, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?textcolor_rgb\s*?%}", textcolor_rgb, RegexOptions.IgnoreCase);
            template = Regex.Replace(template, @"{%\s*?contents\s*?%}", contents, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            return (template.ToString());
        }

        public static async void UpdateIllustTagsAsync()
        {
            await new Action(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MainWindow)
                    {
                        var mw = win as MainWindow;
                        mw.UpdateIllustTagsAsync();
                    }
                    else if (win is ContentWindow)
                    {
                        var w = win as ContentWindow;
                        if (w.Content is IllustDetailPage)
                        {
                            (w.Content as IllustDetailPage).UpdateIllustTags();
                        }
                    }
                    else continue;
                    Task.Delay(1);
                    Application.Current.DoEvents();
                }
            }).InvokeAsync();
        }

        public static void UpdateIllustTags(this ConcurrentDictionary<string, string> tags)
        {
            UpdateIllustTagsAsync();
        }

        public static void UpdateIllustTags(this Application app)
        {
            UpdateIllustTagsAsync();
        }

        public static async void UpdateIllustDescAsync()
        {
            await new Action(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MainWindow)
                    {
                        var mw = win as MainWindow;
                        mw.UpdateIllustDescAsync();
                    }
                    else if (win is ContentWindow)
                    {
                        var w = win as ContentWindow;
                        if (w.Content is IllustDetailPage)
                        {
                            (w.Content as IllustDetailPage).UpdateIllustDesc();
                        }
                    }
                    else continue;
                }
            }).InvokeAsync();
        }

        public static void UpdateIllustDesc(this string content)
        {
            UpdateIllustDescAsync();
        }

        public static void UpdateIllustDesc(this Application app)
        {
            UpdateIllustDescAsync();
        }

        public static async void UpdateWebContentAsync()
        {
            await new Action(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MainWindow)
                    {
                        var mw = win as MainWindow;
                        mw.UpdateWebContentAsync();
                    }
                    else if (win is ContentWindow)
                    {
                        var w = win as ContentWindow;
                        if (w.Content is IllustDetailPage)
                        {
                            (w.Content as IllustDetailPage).UpdateWebContent();
                        }
                    }
                    else continue;
                }
            }).InvokeAsync();
        }

        public static void UpdateWebContent(this Pixeez.Objects.Work illust)
        {
            UpdateWebContentAsync();
        }

        public static void UpdateWebContent(this Application app)
        {
            UpdateWebContentAsync();
        }

        // To return an array of strings instead:
        public static string[] Slice(this string text, int lineLength)
        {
            if (string.IsNullOrEmpty(text)) return (new string[] { });
            //return Regex.Matches(text, @"(.{" + lineLength + @"})").Cast<Match>().Select(m => m.Value).ToArray();
            var t = Regex.Replace(text, @"[\n\r]", "", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"(<br *?/>)", Environment.NewLine, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"(<a .*?>(.*?)</a>)|(<strong>(.*?)</strong>)", "$2", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            t = Regex.Replace(t, @"<.*?>(.*?)</.*?>", "$1", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return Regex.Matches(t, @"(.{" + lineLength + @"})", RegexOptions.IgnoreCase | RegexOptions.Multiline).Cast<Match>().Select(m => m.Value).ToArray();
        }

        // MakePackUri is a utility method for computing a pack uri
        // for the given resource. 
        public static Uri MakePackUri(this string relativeFile)
        {
            Assembly a = typeof(ThresholdEffect).Assembly;

            // Extract the short name.
            string assemblyShortName = a.ToString().Split(',')[0];
            string uriString = $"pack://application:,,,/{assemblyShortName};component/{relativeFile}";

            return new Uri(uriString);
        }

        public static string[] Where(this string cmd)
        {
            var result = new List<string>();

            if (Path.IsPathRooted(cmd) && File.Exists(cmd)) result.Add(cmd);
            else
            {
                var cmd_name = Path.IsPathRooted(cmd) ? Path.GetFileName(cmd) : cmd;
                var search_list = Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator).ToList();
                search_list.Insert(0, Application.Current.GetRoot());
                foreach (var p in search_list)
                {
                    var c = Path.Combine(p, cmd);
                    if (File.Exists(c)) result.Add(c);
                }
            }
            return (result.ToArray());
        }

        public static string GetIllustId(this string url)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url))
            {
                var m = Regex.Match(Path.GetFileName(url), @"(\d+)(_((p)(ugoira))\d+.*?)", RegexOptions.IgnoreCase);
                if (m.Groups.Count > 0)
                {
                    result = m.Groups[1].Value;
                }

                if (string.IsNullOrEmpty(result))
                {
                    m = Regex.Match(Path.GetFileName(url), @"(\d+)", RegexOptions.IgnoreCase);
                    if (m.Groups.Count > 0)
                    {
                        result = m.Groups[1].Value;
                    }
                }
            }
            return (result);
        }

        public static string GetImageId(this string url)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url))
            {
                result = Path.GetFileNameWithoutExtension(url);
            }
            return (result);
        }

        public static string SanityAge(this string sanity)
        {
            string age = "all";

            int san = 2;
            if (int.TryParse(sanity, out san))
            {
                switch (sanity)
                {
                    case "3":
                        age = "12+";
                        break;
                    case "4":
                        age = "15+";
                        break;
                    case "5":
                        age = "17+";
                        break;
                    case "6":
                        age = "18+";
                        break;
                    default:
                        age = "all";
                        break;
                }
            }
            else
            {
                if (sanity.StartsWith("all")) age = "all";
                else if (sanity.StartsWith("12")) age = "12+";
                else if (sanity.StartsWith("15")) age = "15+";
                else if (sanity.StartsWith("17")) age = "17+";
                else if (sanity.StartsWith("18")) age = "18+";
                else age = "all";
            }
            return (age);
        }

        public static string FolderMacroReplace(this string text)
        {
            var result = text;
            result = MacroReplace(result, @"%id%", text.GetIllustId());
            return (result);
        }

        public static string FolderMacroReplace(this string text, string target)
        {
            var result = text;
            result = MacroReplace(result, @"%id%", target);
            return (result);
        }

        public static string MacroReplace(this string text, string macro, string target)
        {
            return (Regex.Replace(text, macro, target, RegexOptions.IgnoreCase));
        }

        public static void SendToOtherInstance(this IEnumerable<string> contents)
        {
            if (contents is IEnumerable<string> && contents.Count() > 0)
            {
                var sendData = string.Join(Environment.NewLine, contents.ToArray());
                SendToOtherInstance(sendData);
            }
        }

        public static void SendToOtherInstance(this string contents)
        {
            try
            {
                var sendData = contents.Trim();
                if (string.IsNullOrEmpty(sendData)) return;

                var pipes = Directory.GetFiles("\\\\.\\pipe\\", "PixivWPF*");
#if DEBUG
                if (pipes.Length > 0)
                {
                    $"Found {pipes.Length} PixivWPF-Search Bridge(s):".DEBUG();
                    foreach (var pipe in pipes)
                    {
                        $"  {pipe}".DEBUG();
                    }
                }
                else return;
#endif

                var current = Application.Current.PipeServerName();
                foreach (var pipe in pipes)
                {
                    try
                    {
                        var pipeName = pipe.Substring(9);
                        if (pipeName.Equals(current, StringComparison.CurrentCultureIgnoreCase) &&
                            Keyboard.Modifiers != ModifierKeys.Control) continue;

                        using (var pipeClient = new NamedPipeClientStream(".", pipeName,
                            PipeDirection.Out, PipeOptions.Asynchronous,
                            System.Security.Principal.TokenImpersonationLevel.Impersonation))
                        {
                            pipeClient.Connect(1000);
                            using (StreamWriter sw = new StreamWriter(pipeClient))
                            {
#if DEBUG
                                $"Sending [{sendData}] to {pipeName}".DEBUG();
#endif
                                sw.WriteLine(sendData);
                                sw.Flush();
                            }
                        }
                    }
#if DEBUG
                    catch (Exception ex)
                    {
                        ex.ToString().ShowMessageBox("ERROR", MessageBoxImage.Error);
                    }
#else
                    catch (Exception ex) { ex.ERROR(); }
#endif
                }
            }
            catch (Exception ex)
            {
                ex.ToString().ShowMessageBox("ERROR", MessageBoxImage.Error);
            }
        }

        public static void ShellSendToOtherInstance(this IEnumerable<string> contents)
        {
            if (contents is IEnumerable<string> && contents.Count() > 0)
            {
                var sendData = string.Join("\" \"", contents.ToArray());
                ShellSendToOtherInstance($"\"{sendData}\"");
            }
        }

        public static void ShellSendToOtherInstance(this string contents)
        {
            var shell = Path.Combine(Application.Current.GetRoot(), setting.ShellSearchBridgeApplication);
            if (File.Exists(shell))
            {
                Process.Start(shell, contents);
            }
        }

        public static void ShellOpenPixivPedia(this string contents)
        {
            if (string.IsNullOrEmpty(contents)) return;

            var currentUri = contents.StartsWith("http", StringComparison.CurrentCultureIgnoreCase) ? Uri.EscapeUriString(contents.Replace("http://", "https://")) : Uri.EscapeUriString($"https://dic.pixiv.net/a/{contents}/");

            var all = setting.ShellPixivPediaApplication.Where();
            var shell = all.Length > 0 ? all.First() : string.Empty;

            if (File.Exists(shell) && shell.EndsWith("\\nw.exe", StringComparison.CurrentCultureIgnoreCase))
            {
                var args = new List<string>() {
                    setting.ShellPixivPediaApplicationArgs,
                    $"--app=\"PixivPedia-{contents}\"",
                    $"--app-id=\"PixivPedia-{contents}\"",
                    $"--user-data-dir=\"{Path.Combine(Application.Current.GetRoot(), ".web")}\"",
                    $"--url=\"{currentUri}\""
                };
                Process.Start(shell, string.Join(" ", args));
            }
            else
            {
                Process.Start(currentUri);
            }
        }

        public static bool OpenUrlWithShell(this string url)
        {
            bool result = false;

            try
            {
                Process.Start(url);
                result = true;
            }
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR");
            }

            return (result);
        }

        private static bool Run(string FileName, string args = "")
        {
            bool result = false;
            try
            {
                Task.Run(() =>
                {
                    var process = new Process();
                    process.StartInfo.FileName = FileName;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.ErrorDialog = process.StartInfo.UseShellExecute ? true : false;
                    process.StartInfo.WorkingDirectory = Path.GetDirectoryName(FileName);
                    //process.StartInfo.ErrorDialogParentHandle = Application.Current.GetMainWindow();
                    process.Start();
                }).Start();
            }
            catch (Exception ex) { ex.Message.DEBUG("SHELL"); }
            return (result);
        }

        public static bool OpenFileWithShell(this string FileName, bool ShowFolder = false, string command = "")
        {
            bool result = false;
            try
            {
                var WinDir = Environment.GetEnvironmentVariable("WinDir");
                if (ShowFolder)
                {
                    if (!string.IsNullOrEmpty(FileName))
                    {
                        Application.Current.ReleaseKeyboardModifiers();
                        Application.Current.DoEvents();

                        var shell = string.IsNullOrEmpty(WinDir) ? "explorer.exe" : Path.Combine(WinDir, "explorer.exe");
                        if (File.Exists(FileName))
                        {
                            Process.Start(shell, $"/select,\"{FileName}\"");
                            result = true;
                        }
                        else
                        {
                            var folder = Path.GetDirectoryName(FileName);
                            if (Directory.Exists(folder))
                            {
                                Process.Start(shell, $"\"{folder}\"");
                                result = true;
                            }
                        }
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(FileName) && File.Exists(FileName))
                    {
                        var UsingOpenWith = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? true : false;
                        var SysDir = Path.Combine(WinDir, Environment.Is64BitOperatingSystem ? "SysWOW64" : "System32", "OpenWith.exe");
                        var OpenWith = string.IsNullOrEmpty(WinDir) ? string.Empty : SysDir;
                        var openwith_exists = File.Exists(OpenWith) ?  true : false;

                        Application.Current.ReleaseKeyboardModifiers();
                        Application.Current.DoEvents();

                        if (UsingOpenWith && openwith_exists)
                        {
                            Process.Start(OpenWith, FileName);
                        }
                        else
                        {
                            setting = Application.Current.LoadSetting();
                            var alt_viewer = (int)(Keyboard.Modifiers & (ModifierKeys.Alt | ModifierKeys.Control)) == 3 ? !setting.ShellImageViewerEnabled : setting.ShellImageViewerEnabled;
                            var IsImage = ext_imgs.Contains(Path.GetExtension(FileName).ToLower()) ? true : false;
                            if (alt_viewer && IsImage)
                            {
                                if (string.IsNullOrEmpty(setting.ShellImageViewerCmd) ||
                                    !setting.ShellImageViewerCmd.ToLower().Contains(setting.ShellImageViewer.ToLower()))
                                    setting.ShellImageViewerCmd = setting.ShellImageViewer;
                                if (!File.Exists(setting.ShellImageViewerCmd))
                                {
                                    var cmd_found = setting.ShellImageViewerCmd.Where();
                                    if (cmd_found.Length > 0) setting.ShellImageViewerCmd = cmd_found.First();
                                }
                                var args = $"{setting.ShellImageViewerParams} \"{FileName}\"";
                                if (string.IsNullOrEmpty(setting.ShellImageViewerCmd))
                                    Process.Start(FileName);
                                else
                                    Process.Start(setting.ShellImageViewerCmd, args);
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(command))
                                    Process.Start(FileName);
                                else
                                    Process.Start(command, $"{setting.ShellLogViewerParams} \"{FileName}\"");
                            }
                        }
                        result = true;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("SHELLRUN"); }
            finally
            {
                Application.Current.DoEvents();
            }
            return (result);
        }

        public static IEnumerable<string> GetDownloadInfo(this DownloadInfo item)
        {
            List<string> result = new List<string>();
            if (item is DownloadInfo)
            {
                var di = item as DownloadInfo;
                var fail = string.IsNullOrEmpty(di.FailReason) ? string.Empty : $", Reason: {di.FailReason.Replace(Environment.NewLine, $"\t{Environment.NewLine}")}".Trim();
                var delta = di.EndTime - di.StartTime;
                var rate = delta.TotalSeconds <= 0 ? 0 : di.Received / 1024.0 / delta.TotalSeconds;
                result.Add($"URL    : {di.Url}");
                result.Add($"File   : {di.FileName}, {di.FileTime.ToString("yyyy-MM-dd HH:mm:sszzz")}");
                result.Add($"State  : {di.State}{fail}");
                result.Add($"Elapsed: {di.StartTime.ToString("yyyy-MM-dd HH:mm:sszzz")} -> {di.EndTime.ToString("yyyy-MM-dd HH:mm:sszzz")}, {delta.Days * 24 + delta.Hours}:{delta.Minutes}:{delta.Seconds} s");
                result.Add($"Status : {di.Received / 1024.0:0.} KB / {di.Length / 1024.0:0.} KB ({di.Received} Bytes / {di.Length} Bytes), Rate ≈ {rate:0.00} KB/s");
            }
            return (result);
        }
        #endregion

        #region Get Illust Work DateTime
        private static TimeZoneInfo TokoyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
        private static TimeZoneInfo LocalTimeZone = TimeZoneInfo.Local;

        private static DateTime ParseDateTime(this string url)
        {
            var result = DateTime.FromFileTime(0);
            //https://i.pximg.net/img-original/img/2010/11/16/22/34/05/14611687_p0.png
            var ds = Regex.Replace(url, @"https?://i\.pximg\.net/.*?/(\d{4})/(\d{2})/(\d{2})/(\d{2})/(\d{2})/(\d{2})/\d+.*?" + regex_img_ext, "$1-$2-$3T$4:$5:$6+09:00", RegexOptions.IgnoreCase);
            DateTime.TryParse(ds, out result);
            return (result);
        }

        public static DateTime GetDateTime(this Pixeez.Objects.Work Illust, bool local = false)
        {
            var dt = DateTime.Now;
            if (Illust is Pixeez.Objects.IllustWork)
            {
                var illustset = Illust as Pixeez.Objects.IllustWork;
                dt = illustset.GetOriginalUrl().ParseDateTime();
                if (dt.Year <= 1601) dt = illustset.CreatedTime;
            }
            else if (Illust is Pixeez.Objects.NormalWork)
            {
                var illustset = Illust as Pixeez.Objects.NormalWork;
                dt = illustset.GetOriginalUrl().ParseDateTime();
                if (dt.Year <= 1601) dt = illustset.CreatedTime.LocalDateTime;
            }
            else if (!string.IsNullOrEmpty(Illust.ReuploadedTime))
            {
                DateTime.TryParse($"{Illust.ReuploadedTime}+09:00", out dt);
            }
            dt = new DateTime(dt.Ticks, DateTimeKind.Unspecified);
            if (local) return (TimeZoneInfo.ConvertTimeBySystemTimeZoneId(dt, TokoyTimeZone.Id, LocalTimeZone.Id));
            else return (dt);
        }

        public static void Touch(this FileInfo fileinfo, string url, bool local = false)
        {
            try
            {
                if (url.StartsWith("http", StringComparison.CurrentCultureIgnoreCase))
                {
                    var fdt = url.ParseDateTime();
                    if (fdt.Year <= 1601) return;
                    if (fileinfo.CreationTime.Ticks != fdt.Ticks) fileinfo.CreationTime = fdt;
                    if (fileinfo.LastWriteTime.Ticks != fdt.Ticks) fileinfo.LastWriteTime = fdt;
                    if (fileinfo.LastAccessTime.Ticks != fdt.Ticks) fileinfo.LastAccessTime = fdt;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void Touch(this string file, string url, bool local = false)
        {
            try
            {
                if (File.Exists(file))
                {
                    FileInfo fi = new FileInfo(file);
                    fi.Touch(url, local);
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void Touch(this string file, Pixeez.Objects.Work Illust, bool local = false)
        {
            file.Touch(Illust.GetOriginalUrl(), local);
        }
        #endregion

        #region Loading Image from InterNet/LocalCache routines
        public static async Task<MemoryStream> ToMemoryStream(this Pixeez.AsyncResponse response)
        {
            MemoryStream result = null;
            using (var stream = await response.GetResponseStreamAsync())
            {
                result = new MemoryStream();
                await stream.CopyToAsync(result);
            }
            return (result);
        }

        public static BitmapSource ConvertBitmapDPI(this BitmapSource source, double dpiX = 96, double dpiY = 96)
        {
            if (dpiX == source.DpiX || dpiY == source.DpiY) return (source);

            int width = source.PixelWidth;
            int height = source.PixelHeight;

            var palette = source.Palette;
            int stride = width * ((source.Format.BitsPerPixel + 31) / 32 * 4);
            byte[] pixelData = new byte[stride * height];
            source.CopyPixels(pixelData, stride, 0);

            BitmapSource result = source;
            try
            {
                var bmp = BitmapSource.Create(width, height, dpiX, dpiY, source.Format, palette, pixelData, stride);
                result = null;
                result = bmp;
                result.Freeze();
                bmp = null;
            }
            catch (Exception ex)
            {
                ex.ERROR("CONVERT");
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        var bmp = BitmapSource.Create(width, height, dpiX, dpiY, source.Format, palette, pixelData, stride);
                        PngBitmapEncoder pngEnc = new PngBitmapEncoder();
                        var fbmp = BitmapFrame.Create(bmp);
                        pngEnc.Frames.Add(fbmp);
                        pngEnc.Save(ms);
                        var pngDec = new PngBitmapDecoder(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                        result = pngDec.Frames[0];
                        result.Freeze();

                        pngEnc.Frames.Clear();
                        pngEnc = null;
                        pngDec = null;
                        fbmp = null;
                        bmp = null;
                    }
                }
                catch (Exception exx)
                {
                    exx.Message.ShowMessageBox("ERROR");
                }
            }
            finally
            {
                Array.Clear(pixelData, 0, pixelData.Length);
                Array.Resize(ref pixelData, 0);
                pixelData = null;
            }
            return result;
        }

        public static ImageSource ToImageSource(this Stream stream, Size size = default(Size))
        {
            setting = Application.Current.LoadSetting();
            var dpi = DPI.Default;

            BitmapSource result = null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                if (!size.Equals(default(Size)) && size.Width >= 0 && size.Height >= 0)
                {
                    bmp.DecodePixelWidth = (int)Math.Ceiling(size.Width * dpi.ScaleX);
                    bmp.DecodePixelHeight = (int)Math.Ceiling(size.Height * dpi.ScaleY);
                }
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.None;
                bmp.StreamSource = stream;
                bmp.EndInit();
                bmp.Freeze();

                result = bmp;
                result.Freeze();
                bmp = null;
            }
            catch (Exception ex)
            {
                var ret = ex.Message;
                try
                {
                    var bmp = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    result = null;
                    result = bmp;
                    result.Freeze();
                    bmp = null;
                }
                catch (Exception exx)
                {
                    var retx = exx.Message;
                }
            }
            finally
            {
                if (setting.AutoConvertDPI && result is ImageSource)
                {
                    try
                    {
                        if (result.DpiX != dpi.X || result.DpiY != dpi.Y)
                        //if (result.DpiX > dpi.X15 || result.DpiY > dpi.Y15)
                        {
                            var bmp = ConvertBitmapDPI(result, dpi.X, dpi.Y);
                            result = null;
                            result = bmp;
                            result.Freeze();
                            bmp = null;
                        }
                    }
                    catch (Exception ex) { ex.ERROR(); }
                }
            }
            return (result);
        }

        public static BitmapSource ToBitmapSource(this ImageSource source)
        {
            BitmapSource result = source is BitmapSource ? source as BitmapSource : null;
            try
            {
                if (result == null && source is ImageSource && source.Width > 0 && source.Height > 0)
                {
                    var dpi = DPI.Default;
                    RenderTargetBitmap target = new RenderTargetBitmap((int)(source.Width), (int)(source.Height), dpi.X, dpi.Y, PixelFormats.Pbgra32);
                    DrawingVisual drawingVisual = new DrawingVisual();
                    using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                    {
                        drawingContext.DrawImage(source, new Rect(0, 0, source.Width, source.Height));
                    }
                    target.Render(drawingVisual);

                    int width = target.PixelWidth;
                    int height = target.PixelHeight;
                    var palette = target.Palette;
                    int stride = width * ((target.Format.BitsPerPixel + 31) / 32 * 4);
                    byte[] pixelData = new byte[stride * height];
                    target.CopyPixels(pixelData, stride, 0);

                    result = BitmapSource.Create(width, height,
                                                target.DpiX, target.DpiY,
                                                target.Format, target.Palette,
                                                pixelData, stride);
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }
        #endregion

        #region Downloaded Cache routines
        private static ConcurrentDictionary<string, bool> _cachedDownloadedList = new ConcurrentDictionary<string, bool>();
        internal static void UpdateDownloadedListCache(this string folder, bool cached = true)
        {
            if (Directory.Exists(folder) && cached)
            {
                try
                {
                    if (!_cachedDownloadedList.ContainsKey(folder))
                    {
                        _cachedDownloadedList[folder] = cached;
                        var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories);
                        foreach (var f in files)
                        {
                            if (ext_imgs.Contains(Path.GetExtension(f).ToLower()))
                                _cachedDownloadedList[f] = cached;
                        }
                    }
                }
                catch (Exception ex) { ex.ERROR(); }
            }
        }

        internal static async void UpdateDownloadedListCacheAsync(this string folder, bool cached = true)
        {
            await Task.Run(() =>
            {
                UpdateDownloadedListCache(folder, cached);
            });
        }

        internal static void UpdateDownloadedListCache(this StorageType storage)
        {
            if (storage is StorageType)
            {
                storage.Folder.UpdateDownloadedListCacheAsync(storage.Cached);
            }
        }

        internal static async void UpdateDownloadedListCacheAsync(this StorageType storage)
        {
            await Task.Run(() =>
            {
                UpdateDownloadedListCache(storage);
            });
        }

        internal static bool DownoadedCacheExists(this string file)
        {
            return (_cachedDownloadedList.ContainsKey(file));
        }

        private static Func<string, bool> DownoadedCacheExistsFunc = x => DownoadedCacheExists(x);
        internal static bool DownoadedCacheExistsAsync(this string file)
        {
            return (DownoadedCacheExistsFunc(file));
        }

        internal static void DownloadedCacheAdd(this string file, bool cached = true)
        {
            try
            {
                _cachedDownloadedList[file] = cached;
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        internal static void DownloadedCacheRemove(this string file)
        {
            try
            {
                bool cached = false;
                if (_cachedDownloadedList.ContainsKey(file))
                    _cachedDownloadedList.TryRemove(file, out cached);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        internal static void DownloadedCacheUpdate(this string old_file, string new_file, bool cached = true)
        {
            try
            {
                if (_cachedDownloadedList.ContainsKey(old_file))
                {
                    old_file.DownloadedCacheRemove();
                }
                new_file.DownloadedCacheAdd(cached);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        // Define the event handlers.
        private static ConcurrentDictionary<string, FileSystemWatcher> _watchers = new ConcurrentDictionary<string, FileSystemWatcher>();
        private static DateTime lastDownloadEventTick = DateTime.Now;
        private static string lastDownloadEventFile = string.Empty;
        private static WatcherChangeTypes lastDownloadEventType = WatcherChangeTypes.All;

        private static void OnDownloadChanged(object source, FileSystemEventArgs e)
        {
#if DEBUG
            // Specify what is done when a file is changed, created, or deleted.
            $"File: {e.FullPath} {e.ChangeType}".DEBUG("DOWNLOADWATCHER");
#endif
            try
            {
                //if (e.ChangeType == lastDownloadEventType &&
                //    e.FullPath.Equals(lastDownloadEventFile, StringComparison.CurrentCultureIgnoreCase) &&
                //    lastDownloadEventTick.Ticks.DeltaNowMillisecond() < 10) throw new Exception("Same download event!");
                if (e.ChangeType == WatcherChangeTypes.Created)
                {
                    if (File.Exists(e.FullPath))
                    {
                        if (ext_imgs.Contains(Path.GetExtension(e.Name).ToLower()))
                        {
                            e.FullPath.DownloadedCacheAdd();
                            UpdateDownloadStateAsync(GetIllustId(e.Name), true);
                            lastDownloadEventTick = DateTime.Now;
                        }
                    }
                }
                else if (e.ChangeType == WatcherChangeTypes.Changed)
                {
                    //if (File.Exists(e.FullPath))
                    //{
                    //    e.FullPath.DownloadedCacheAdd();
                    //    UpdateDownloadStateAsync(GetIllustId(e.FullPath));
                    //    lastDownloadEventTick = DateTime.Now;
                    //}
                }
                else if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    if (ext_imgs.Contains(Path.GetExtension(e.Name).ToLower()))
                    {
                        e.FullPath.DownloadedCacheRemove();
                        UpdateDownloadStateAsync(GetIllustId(e.Name), false);
                        lastDownloadEventTick = DateTime.Now;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("DOWNLOADWATCHER"); }
            finally
            {
                //lastDownloadEventTick = DateTime.Now;
                lastDownloadEventFile = e.FullPath;
                lastDownloadEventType = e.ChangeType;
            }
        }

        private static void OnDownloadRenamed(object source, RenamedEventArgs e)
        {
#if DEBUG
            // Specify what is done when a file is renamed.
            $"File: {e.OldFullPath} renamed to {e.FullPath}".DEBUG("DOWNLOADWATCHER");
#endif
            try
            {
                //if (e.ChangeType == lastDownloadEventType &&
                //    e.FullPath.Equals(lastDownloadEventFile, StringComparison.CurrentCultureIgnoreCase) &&
                //    lastDownloadEventTick.Ticks.DeltaNowMillisecond() < 10) throw new Exception("Same download event!");
                if (e.ChangeType == WatcherChangeTypes.Renamed)
                {
                    e.OldFullPath.DownloadedCacheUpdate(e.FullPath);
                    if (ext_imgs.Contains(Path.GetExtension(e.Name).ToLower()))
                    {
                        UpdateDownloadStateAsync(GetIllustId(e.Name));
                        lastDownloadEventTick = DateTime.Now;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("DOWNLOADWATCHER"); }
            finally
            {
                //lastDownloadEventTick = DateTime.Now;
                lastDownloadEventFile = e.FullPath;
                lastDownloadEventType = e.ChangeType;
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void InitDownloadedWatcher(this IEnumerable<StorageType> storages)
        {
            ConcurrentDictionary<string, StorageType> items = new ConcurrentDictionary<string, StorageType>();
            foreach (var ls in storages)
            {
                var folder = Path.GetFullPath(ls.Folder.MacroReplace("%ID%", "")).TrimEnd('\\');
                var parent = storages.Where(o => folder.StartsWith(o.Folder, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault();
                if (items.ContainsKey(folder))
                {
                    if (parent is StorageType && parent.IncludeSubFolder) ls.Cached = true;
                    continue;
                }
                items.TryAdd(ls.Folder.TrimEnd('\\'), ls);
            }

            storages.ReleaseDownloadedWatcher();

            foreach (var i in items)
            {
                var folder = i.Key;
                var storage = i.Value;

                if (Directory.Exists(folder))
                {
                    folder.UpdateDownloadedListCacheAsync();
                    var watcher = new FileSystemWatcher(folder, "*.*")
                    {
                        NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                        IncludeSubdirectories = storage is StorageType ? storage.IncludeSubFolder : false
                    };
                    watcher.Changed += new FileSystemEventHandler(OnDownloadChanged);
                    watcher.Created += new FileSystemEventHandler(OnDownloadChanged);
                    watcher.Deleted += new FileSystemEventHandler(OnDownloadChanged);
                    watcher.Renamed += new RenamedEventHandler(OnDownloadRenamed);
                    // Begin watching.
                    watcher.EnableRaisingEvents = true;

                    _watchers[folder] = watcher;
                }
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void AddDownloadedWatcher(this string folder, bool IncludeSubFolder = false)
        {
            if (Directory.Exists(folder) && !_watchers.ContainsKey(folder))
            {
                folder.UpdateDownloadedListCacheAsync();
                var watcher = new FileSystemWatcher(folder, "*.*")
                {
                    NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    IncludeSubdirectories = IncludeSubFolder
                };
                watcher.Changed += new FileSystemEventHandler(OnDownloadChanged);
                watcher.Created += new FileSystemEventHandler(OnDownloadChanged);
                watcher.Deleted += new FileSystemEventHandler(OnDownloadChanged);
                watcher.Renamed += new RenamedEventHandler(OnDownloadRenamed);
                // Begin watching.
                watcher.EnableRaisingEvents = true;

                _watchers[folder] = watcher;
            }
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void ReleaseDownloadedWatcher(this IEnumerable<StorageType> storages)
        {
            if (_watchers is ConcurrentDictionary<string, FileSystemWatcher>)
            {
                foreach (var w in _watchers)
                {
                    try
                    {
                        if (w.Value is FileSystemWatcher)
                        {
                            w.Value.Dispose();
                        }
                    }
                    catch { }
                }
                _watchers.Clear();
            }
        }

        public static void UpdateDownloadStateAsync(string illustid = default(string), bool? exists = null)
        {
            int id = -1;
            int.TryParse(illustid, out id);
            UpdateDownloadStateAsync(id, exists);
        }

        public static async void UpdateDownloadStateAsync(int? illustid = null, bool? exists = null)
        {
            await new Action(() =>
            {
                foreach (var win in Application.Current.Windows)
                {
                    if (win is MainWindow)
                    {
                        var mw = win as MainWindow;
                        mw.UpdateDownloadState(illustid, exists);
                    }
                    else if (win is ContentWindow)
                    {
                        var w = win as ContentWindow;
                        if (w.Content is IllustDetailPage)
                            (w.Content as IllustDetailPage).UpdateDownloadStateAsync(illustid, exists);
                        else if (w.Content is IllustImageViewerPage)
                            (w.Content as IllustImageViewerPage).UpdateDownloadStateAsync(illustid, exists);
                        else if (w.Content is SearchResultPage)
                            (w.Content as SearchResultPage).UpdateDownloadStateAsync(illustid, exists);
                        else if (w.Content is HistoryPage)
                            (w.Content as HistoryPage).UpdateDownloadStateAsync(illustid, exists);
                        else if (w.Content is DownloadManagerPage)
                            (w.Content as DownloadManagerPage).UpdateDownloadStateAsync(illustid, exists);
                    }
                }
            }).InvokeAsync();
        }

        public static async void UpdateDownloadStateAsync(this ImageListGrid list, int? illustid = null, bool? exists = null)
        {
            await new Action(() =>
            {
                UpdateDownloadState(list, illustid, exists);
            }).InvokeAsync();
        }

        public static void UpdateDownloadState(this ImageListGrid list, int? illustid = null, bool? exists = null)
        {
            try
            {
                list.Items.UpdateDownloadState(illustid, exists);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void UpdateDownloadState(this ItemCollection items, int? illustid = null, bool? exists = null)
        {
            try
            {
                items.UpdateDownloadState(illustid, exists);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static async void UpdateDownloadStateAsync(this ObservableCollection<PixivItem> collection, int? illustid = null, bool? exists = null)
        {
            await new Action(() =>
            {
                UpdateDownloadState(collection, illustid, exists);
            }).InvokeAsync();
        }

        public static void UpdateDownloadState(this ObservableCollection<PixivItem> collection, int? illustid = null, bool? exists = null)
        {
            try
            {
                var id = illustid ?? -1;
                foreach (var item in collection)
                {
                    var part_down = item.Illust.IsPartDownloadedAsync();
                    if (item.IsPage() || item.IsPages())
                    {
                        item.IsDownloaded = item.Illust.GetOriginalUrl(item.Index).IsDownloadedAsync();
                        item.IsPartDownloaded = part_down;
                    }
                    else if (item.IsWork())
                    {
                        if (id == -1)
                            item.IsDownloaded = part_down;
                        else if (id == (int)(item.Illust.Id))
                        {
                            if (item.Count > 1)
                                item.IsDownloaded = part_down;
                            else
                                item.IsDownloaded = exists ?? part_down;
                        }
                        item.IsPartDownloaded = part_down;
                    }
                }
            }
            catch { }
        }
        #endregion

        #region Check Download State routines
        #region IsDownloaded
        private class DownloadState
        {
            public string Path { get; set; } = string.Empty;
            public bool Exists { get; set; } = false;
        }

        internal static bool IsDownloadedAsync(this Pixeez.Objects.Work illust, bool is_meta_single_page = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsDownloadedAsync(is_meta_single_page));
            else
                return (false);
        }

        internal static bool IsDownloaded(this Pixeez.Objects.Work illust, bool is_meta_single_page = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsDownloaded(is_meta_single_page));
            else
                return (false);
        }

        internal static bool IsDownloadedAsync(this Pixeez.Objects.Work illust, out string filepath, bool is_meta_single_page = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsDownloadedAsync(out filepath, is_meta_single_page));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsDownloaded(this Pixeez.Objects.Work illust, out string filepath, bool is_meta_single_page = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsDownloaded(out filepath, is_meta_single_page));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsDownloadedAsync(this Pixeez.Objects.Work illust, int index = -1)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloadedAsync());
            else
                return (false);
        }

        internal static bool IsDownloaded(this Pixeez.Objects.Work illust, int index = -1)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloaded());
            else
                return (false);
        }

        internal static bool IsDownloadedAsync(this Pixeez.Objects.Work illust, out string filepath, int index = -1, bool is_meta_single_page = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloadedAsync(out filepath, is_meta_single_page));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsDownloaded(this Pixeez.Objects.Work illust, out string filepath, int index = -1, bool is_meta_single_page = false)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl(index).IsDownloaded(out filepath, is_meta_single_page));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        private static Func<string, bool, bool> IsDownloadedFunc = (url, meta) => IsDownloaded(url, meta);
        internal static bool IsDownloadedAsync(this string url, bool is_meta_single_page = false)
        {
            return (IsDownloadedFunc(url, is_meta_single_page));
        }

        private static Func<string, string, bool, DownloadState> IsDownloadedFileFunc = (url, file, meta) =>
        {
            var state = new DownloadState();
            file = string.Empty;
            state.Exists = IsDownloaded(url, out file, meta);
            state.Path = file;
            return(state);
        };

        internal static bool IsDownloadedAsync(this string url, out string filepath, bool is_meta_single_page = false)
        {
            filepath = string.Empty;
            var result = IsDownloadedFileFunc(url, filepath, is_meta_single_page);
            filepath = result.Path;
            return (result.Exists); ;
        }

        internal static bool IsDownloaded(this string url, bool is_meta_single_page = false)
        {
            string fp = string.Empty;
            return (IsDownloaded(url, out fp, is_meta_single_page));
        }

        internal static bool IsDownloaded(this string url, out string filepath, bool is_meta_single_page = false)
        {
            bool result = false;
            filepath = string.Empty;

            var file = url.GetImageName(is_meta_single_page);
            foreach (var local in setting.LocalStorage)
            {
                if (string.IsNullOrEmpty(local.Folder)) continue;

                var folder = local.Folder.FolderMacroReplace(url.GetIllustId());
                if (Directory.Exists(folder))
                {
                    folder.UpdateDownloadedListCacheAsync(local.Cached);

                    var f = Path.Combine(folder, file);
                    if (local.Cached)
                    {
                        if (f.DownoadedCacheExistsAsync())
                        {
                            filepath = f;
                            f.Touch(url);
                            result = true;
                            break;
                        }
                    }
                    else
                    {
                        if (File.Exists(f))
                        {
                            filepath = f;
                            f.Touch(url);
                            result = true;
                            break;
                        }
                    }
                }
            }

            return (result);
        }
        #endregion

        #region IsPartDownloaded
        internal static bool IsPartDownloadedAsync(this PixivItem item)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloadedAsync());
            else
                return (false);
        }

        internal static bool IsPartDownloaded(this PixivItem item)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloaded());
            else
                return (false);
        }

        internal static bool IsPartDownloadedAsync(this PixivItem item, out string filepath)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloadedAsync(out filepath));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsPartDownloaded(this PixivItem item, out string filepath)
        {
            if (item.Illust is Pixeez.Objects.Work)
                return (item.Illust.GetOriginalUrl().IsPartDownloaded(out filepath));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsPartDownloadedAsync(this Pixeez.Objects.Work illust)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsPartDownloadedAsync());
            else
                return (false);
        }

        internal static bool IsPartDownloaded(this Pixeez.Objects.Work illust)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsPartDownloaded());
            else
                return (false);
        }

        internal static bool IsPartDownloadedAsync(this Pixeez.Objects.Work illust, out string filepath)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsPartDownloadedAsync(out filepath));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        internal static bool IsPartDownloaded(this Pixeez.Objects.Work illust, out string filepath)
        {
            if (illust is Pixeez.Objects.Work)
                return (illust.GetOriginalUrl().IsPartDownloaded(out filepath));
            else
            {
                filepath = string.Empty;
                return (false);
            }
        }

        private static Func<string, bool> IsPartDownloadedFunc = (url) => IsPartDownloaded(url);
        internal static bool IsPartDownloadedAsync(this string url)
        {
            return (IsPartDownloaded(url));
        }

        private static Func<string, string, DownloadState> IsPartDownloadedFileFunc = (url, file) =>
        {
            var state = new DownloadState();
            file = string.Empty;
            state.Exists = IsPartDownloaded(url, out file);
            state.Path = file;
            return(state);
        };

        internal static bool IsPartDownloadedAsync(this string url, out string filepath)
        {
            filepath = string.Empty;
            var result = IsPartDownloadedFileFunc(url, filepath);
            filepath = result.Path;
            return (result.Exists);
        }

        internal static bool IsPartDownloaded(this string url, out string filepath)
        {
            bool result = false;
            var file = url.GetImageName(true);

            filepath = string.Empty;
            foreach (var local in setting.LocalStorage)
            {
                if (string.IsNullOrEmpty(local.Folder)) continue;

                var folder = local.Folder.FolderMacroReplace(url.GetIllustId());
                if (Directory.Exists(folder))
                {
                    folder.UpdateDownloadedListCacheAsync(local.Cached);

                    var f = Path.Combine(folder, file);
                    if (local.Cached)
                    {
                        if (f.DownoadedCacheExistsAsync())
                        {
                            filepath = f;
                            f.Touch(url);
                            result = true;
                            break;
                        }
                    }
                    else
                    {
                        if (File.Exists(f))
                        {
                            filepath = f;
                            f.Touch(url);
                            result = true;
                            break;
                        }
                    }

                    var fn = Path.GetFileNameWithoutExtension(file);
                    var files = Directory.GetFiles(folder, $"{fn}_*.*");
                    if (files.Length > 0)
                    {
                        foreach (var fc in files)
                            fc.Touch(url);
                        filepath = files.First();
                        result = true;
                    }
                }
                if (result) break;
            }
            return (result);
        }

        internal static bool IsPartDownloaded(this string url)
        {
            string fp = string.Empty;
            return (IsPartDownloaded(url, out fp));
        }
        #endregion
        #endregion

        #region Load/Save Image routines
        public static string GetPixivLinkPattern(this string url)
        {
            return (@"http(s)*://.*?\.((pixiv\..*?)|(pximg\..*?))/");
        }

        public static bool IsPixivImage(this string url)
        {
            var pattern = @"http(s)*://.*?\.(pximg\.net/.*?)/";
            if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
                return (true);
            else
                return (false);
        }

        public static bool IsPixivLink(this string url)
        {
            var pattern = url.GetPixivLinkPattern();
            if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
                return (true);
            else
                return (false);
        }

        internal static bool IsFileReady(this string filename)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                if (!File.Exists(filename)) return true;

                using (FileStream inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                    return inputStream.Length > 0;
            }
            catch (Exception ex) { ex.ERROR(); return false; }
        }

        internal static void WaitForFile(this string filename)
        {
            //This will lock the execution until the file is ready
            //TODO: Add some logic to make it async and cancelable
            while (!IsFileReady(filename)) { }
        }

        public static bool IsCached(this string url)
        {
            bool result = false;
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = cache.IsCached(url);
            }
            return (result);
        }

        public static long GetFileLength(this string filename)
        {
            long result = -1;
            if (File.Exists(filename))
            {
                result = new FileInfo(filename).Length;
            }
            return (result);
        }

        public static FileInfo GetFileInfo(this string filename)
        {
            FileInfo result = null;
            if (File.Exists(filename))
            {
                result = new FileInfo(filename);
            }
            return (result);
        }

        public static DateTime GetFileTime(this string filename, string mode = "m")
        {
            DateTime result = default(DateTime);
            if (File.Exists(filename))
            {
                mode = mode.ToLower();
                if (mode.Equals("c"))
                    result = new FileInfo(filename).CreationTime;
                else if (mode.Equals("m"))
                    result = new FileInfo(filename).LastWriteTime;
                else if (mode.Equals("a"))
                    result = new FileInfo(filename).LastAccessTime;
            }
            return (result);
        }

        public static string GetImageName(this string url, bool is_meta_single_page)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url))
            {
                result = Path.GetFileName(url).Replace("_p", "_");
                if (is_meta_single_page) result = result.Replace("_0.", ".");
            }
            return (result);
        }

        public static string GetImageCachePath(this string url)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = cache.GetCacheFile(url);
            }
            return (result);
        }

        public static string GetImageCacheFile(this string url)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = cache.GetImagePath(url);
            }
            return (result);
        }

        public static async Task<string> DownloadCacheFile(this string url)
        {
            string result = string.Empty;
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = await cache.DownloadImage(url);
            }
            return (result);
        }

        public static CustomImageSource LoadImageFromFile(this string file, Size size = default(Size))
        {
            CustomImageSource result = new CustomImageSource();
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                using (Stream stream = new MemoryStream(File.ReadAllBytes(file)))
                {
                    result.Source = stream.ToImageSource(size);
                    result.SourcePath = file;
                    result.Size = stream.Length;
                    result.ColorDepth = result.Source is BitmapSource ? (result.Source as BitmapSource).Format.BitsPerPixel : 32;
                }
            }
            return (result);
        }

        public static async Task<CustomImageSource> LoadImageFromUrl(this string url, bool overwrite = false, bool login = false, Size size = default(Size))
        {
            CustomImageSource result = new CustomImageSource();
            if (!string.IsNullOrEmpty(url) && cache is CacheImage)
            {
                result = await cache.GetImage(url, overwrite, login, size);
            }
            return (result);
        }

        public static async Task<CustomImageSource> LoadImageFromUri(this Uri uri, bool overwrite = false, Pixeez.Tokens tokens = null, Size size = default(Size))
        {
            CustomImageSource result = new CustomImageSource();
            if (uri.IsUnc || uri.IsFile)
                result = LoadImageFromFile(uri.LocalPath, size);
            else if (!(uri.IsLoopback || uri.IsAbsoluteUri))
                result = await LoadImageFromUrl(uri.OriginalString, overwrite, false, size);
            return (result);
        }

        public static async Task<string> DownloadImage(this string url, string file, bool overwrite = true)
        {
            var result = string.Empty;
            if (!File.Exists(file) || overwrite || new FileInfo(file).Length <= 0)
            {
                setting = Application.Current.LoadSetting();
                using (var httpClient = Application.Current.GetHttpClient())
                {
                    try
                    {
                        using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                        {
                            try
                            {
                                response.EnsureSuccessStatusCode();
                                string vl = response.Content.Headers.ContentEncoding.FirstOrDefault();

                                using (var sr = vl != null && vl == "gzip" ? new System.IO.Compression.GZipStream(await response.Content.ReadAsStreamAsync(), System.IO.Compression.CompressionMode.Decompress) : await response.Content.ReadAsStreamAsync())
                                {
                                    using (var ms = new MemoryStream())
                                    {
                                        var target = Path.GetDirectoryName(file);
                                        if (!Directory.Exists(target)) Directory.CreateDirectory(target);
                                        await sr.CopyToAsync(ms);
                                        File.WriteAllBytes(file, ms.ToArray());
                                        result = file;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                ex.Message.DEBUG();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.Message.DEBUG();
                    }
                }
            }
            return (result);
        }

        public static async Task<string> DownloadImage(this string url, string file, Pixeez.Tokens tokens, bool overwrite = true)
        {
            var result = string.Empty;
            if (!File.Exists(file) || overwrite || new FileInfo(file).Length <= 0)
            {
                if (tokens == null) tokens = await ShowLogin();
                using (var response = await tokens.SendRequestAsync(Pixeez.MethodType.GET, url))
                {
                    if (response != null && response.Source.StatusCode == HttpStatusCode.OK)
                    {
                        using (var ms = await response.ToMemoryStream())
                        {
                            var target = Path.GetDirectoryName(file);
                            if (!Directory.Exists(target)) Directory.CreateDirectory(target);
                            File.WriteAllBytes(file, ms.ToArray());
                            result = file;
                        }
                    }
                    else result = string.Empty;
                }
            }
            return (result);
        }

        public static async Task<bool> SaveImage(this string url, string file, bool overwrite = true)
        {
            bool result = false;
            if (url.IndexOf("https://") > 1 || url.IndexOf("http://") > 1) return (result);

            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    var unc = file.IndexOf("file:\\\\\\");
                    if (unc > 0) file = file.Substring(0, unc - 1);
                    else if (unc == 0) file = file.Substring(8);

                    result = !string.IsNullOrEmpty(await url.DownloadImage(file, overwrite));
                }
                catch (Exception ex)
                {
                    if (ex is IOException)
                    {

                    }
                    else
                    {
                        ex.Message.ShowMessageBox("ERROR");
                    }
                }
            }
            return (result);
        }

        public static async Task<bool> SaveImage(this string url, Pixeez.Tokens tokens, string file, bool overwrite = true)
        {
            bool result = false;
            if (url.IndexOf("https://") > 1 || url.IndexOf("http://") > 1) return (result);

            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    var unc = file.IndexOf("file:\\\\\\");
                    if (unc > 0) file = file.Substring(0, unc - 1);
                    else if (unc == 0) file = file.Substring(8);

                    //if (string.IsNullOrEmpty(await url.DownloadImage(file, overwrite)))
                    result = !string.IsNullOrEmpty(await url.DownloadImage(file, tokens, overwrite));
                }
                catch (Exception ex)
                {
                    if (ex is IOException)
                    {

                    }
                    else
                    {
                        ex.Message.ShowMessageBox("ERROR");
                    }
                }
            }
            return (result);
        }

        public static async Task<string> SaveImage(this string url, Pixeez.Tokens tokens, bool is_meta_single_page = false, bool overwrite = true)
        {
            string result = string.Empty;

            var file = Application.Current.SaveTarget(url.GetImageName(is_meta_single_page));

            try
            {
                if (!string.IsNullOrEmpty(file))
                {
                    result = await url.DownloadImage(file, tokens, overwrite);
                }
            }
            catch (Exception ex)
            {
                if (ex is IOException)
                {

                }
                else
                {
                    ex.Message.ShowMessageBox("ERROR[SAVEIMAGE]");
                }
            }
            return (result);
        }

        public static async Task<string> SaveImage(this string url, Pixeez.Tokens tokens, DateTime dt, bool is_meta_single_page = false, bool overwrite = true)
        {
            var file = await url.SaveImage(tokens, is_meta_single_page, overwrite);
            var id = url.GetIllustId();

            if (!string.IsNullOrEmpty(file))
            {
                File.SetCreationTime(file, dt);
                File.SetLastWriteTime(file, dt);
                File.SetLastAccessTime(file, dt);
                var state = "Succeed";
                $"{Path.GetFileName(file)} is saved!".ShowDownloadToast(state, file, state);

                if (Regex.IsMatch(file, @"_ugoira\d+\.", RegexOptions.IgnoreCase))
                {
                    var ugoira_url = url.Replace("img-original", "img-zip-ugoira");
                    //ugoira_url = Regex.Replace(ugoira_url, @"(_ugoira)(\d+)(\..*?)", "_ugoira1920x1080.zip", RegexOptions.IgnoreCase);
                    ugoira_url = Regex.Replace(ugoira_url, @"_ugoira\d+\..*?$", "_ugoira1920x1080.zip", RegexOptions.IgnoreCase);
                    var ugoira_file = await ugoira_url.SaveImage(tokens, dt, true, overwrite);
                    if (!string.IsNullOrEmpty(ugoira_file))
                    {
                        File.SetCreationTime(ugoira_file, dt);
                        File.SetLastWriteTime(ugoira_file, dt);
                        File.SetLastAccessTime(ugoira_file, dt);
                        state = "Succeed";
                        $"{Path.GetFileName(ugoira_file)} is saved!".ShowDownloadToast(state, ugoira_file, state);
                    }
                    else
                    {
                        state = "Failed";
                        $"Save {Path.GetFileName(ugoira_url)} failed!".ShowDownloadToast(state, "", state);
                    }
                }
            }
            else
            {
                var state = "Failed";
                $"Save {Path.GetFileName(url)} failed!".ShowDownloadToast(state, "", state);
            }
            return (file);
        }

        public static async Task<List<string>> SaveImage(Dictionary<string, DateTime> files, Pixeez.Tokens tokens, bool is_meta_single_page = false)
        {
            List<string> result = new List<string>();

            foreach (var file in files)
            {
                var f = await file.Key.SaveImage(tokens, file.Value, is_meta_single_page);
                result.Add(f);
            }
            SystemSounds.Beep.Play();

            return (result);
        }

        public static void SaveImage(this string url, string thumb, DateTime dt, bool is_meta_single_page = false, bool overwrite = true)
        {
            Commands.AddDownloadItem.Execute(new DownloadParams()
            {
                Url = url,
                ThumbUrl = thumb,
                Timestamp = dt,
                IsSinglePage = is_meta_single_page,
                OverwriteExists = overwrite
            });
        }

        public static void SaveImages(Dictionary<Tuple<string, bool>, Tuple<string, DateTime>> files, bool overwrite = true)
        {
            foreach (var file in files)
            {
                var url = file.Key.Item1;
                var is_meta_single_page =  file.Key.Item2;
                var thumb = file.Value.Item1;
                var dt = file.Value.Item2;
                url.SaveImage(thumb, dt, is_meta_single_page, overwrite);
            }
            SystemSounds.Beep.Play();
        }

        public static async Task<ImageSource> GetImageFromURL(this string url)
        {
            ImageSource result = null;

            var ContentType = string.Empty;
            var ext = Path.GetExtension(url).ToLower();
            switch (ext)
            {
                case ".jpeg":
                case ".jpg":
                    ContentType = "image/jpeg";
                    break;
                case ".png":
                    ContentType = "image/png";
                    break;
                case ".bmp":
                    ContentType = "image/bmp";
                    break;
                case ".gif":
                    ContentType = "image/gif";
                    break;
                case ".webp":
                    ContentType = "image/webp";
                    break;
                case ".tiff":
                case ".tif":
                    ContentType = "image/tiff";
                    break;
                default:
                    ContentType = "application/octet-stream";
                    break;
            }
            try
            {
                using (HttpClient client = Application.Current.GetHttpClient())
                {
                    client.DefaultRequestHeaders.Add("Content-Type", ContentType);
                    HttpResponseMessage response = await client.GetAsync(url);
                    byte[] content = await response.Content.ReadAsByteArrayAsync();
                    //return "data:image/png;base64," + Convert.ToBase64String(content);
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    using (var ms = new MemoryStream(content))
                    {
                        image.StreamSource = ms;
                        image.EndInit();
                        image.Freeze();
                    }
                    result = image;
                }
            }
            catch (Exception ex) { ex.ERROR(); }

            return (result);
        }

        public static async Task<ImageSource> ToImageSource(this string url, Pixeez.Tokens tokens)
        {
            ImageSource result = null;
            //url = Regex.Replace(url, @"//.*?\.pixiv.net/", "//i.pximg.net/", RegexOptions.IgnoreCase);
            using (var response = await tokens.SendRequestAsync(Pixeez.MethodType.GET, url))
            {
                if (response.Source.StatusCode == HttpStatusCode.OK)
                    result = await response.ToImageSource();
                else
                    result = null;
            }
            return (result);
        }

        public static async Task<ImageSource> ToImageSource(this Pixeez.AsyncResponse response, Size size = default(Size))
        {
            ImageSource result = null;
            using (var stream = await response.GetResponseStreamAsync())
            {
                result = stream.ToImageSource(size);
            }
            return (result);
        }

        public static async Task<ImageSource> ToImageSource(this ImageSource source, double width, double height)
        {
            ImageSource result = source;
            try
            {
                if (source is BitmapSource && width > 0 && height > 0)
                {
                    await new Action(() =>
                    {
                        var scale = new ScaleTransform(width / source.Width, height / source.Height);
                        result = new TransformedBitmap(source as BitmapSource, scale);
                        result.Freeze();
                    }).InvokeAsync(true);
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        private static void Arrange(UIElement element, int width, int height)
        {
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();
        }

        public static Image GetThemedImage(this Uri uri)
        {
            Image result = new Image() { Source = new BitmapImage(uri) };
            try
            {
                new Action(() =>
                {
                    var dpi = new DPI();

                    var img = new BitmapImage(uri);
                    var src = new Image() { Source = img, Width = img.Width, Height = img.Height, Opacity = 0.8 };
                    src.Effect = new ThresholdEffect() { Threshold = 0.67, BlankColor = Theme.WindowTitleColor };
                    //img.Effect = new TranspranceEffect() { TransColor = Theme.WindowTitleColor };
                    //img.Effect = new TransparenceEffect() { TransColor = Color.FromRgb(0x00, 0x96, 0xfa) };
                    //img.Effect = new ReplaceColorEffect() { Threshold = 0.5, SourceColor = Color.FromArgb(0xff, 0x00, 0x96, 0xfa), TargetColor = Theme.MahApps.Colors.Accent };
                    //img.Effect = new ReplaceColorEffect() { Threshold = 0.5, SourceColor = Color.FromRgb(0x00, 0x96, 0xfa), TargetColor = Colors.Transparent };
                    //img.Effect = new ReplaceColorEffect() { Threshold = 0.5, SourceColor = Color.FromRgb(0x00, 0x96, 0xfa), TargetColor = Theme.WindowTitleColor };
                    //img.Effect = new ExcludeReplaceColorEffect() { Threshold = 0.05, ExcludeColor = Colors.White, TargetColor = Theme.WindowTitleColor };

                    Grid root = new Grid();
                    root.Background = Theme.WindowTitleBrush;
                    Arrange(root, (int)src.Width, (int)src.Height);
                    root.Children.Add(src);
                    Arrange(src, (int)src.Width, (int)src.Height);

                    RenderTargetBitmap bmp = new RenderTargetBitmap((int)(src.Width), (int)(src.Height), dpi.X, dpi.Y, PixelFormats.Pbgra32);
                    DrawingVisual drawingVisual = new DrawingVisual();
                    using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                    {
                        VisualBrush visualBrush = new VisualBrush(root);
                        drawingContext.DrawRectangle(visualBrush, null, new Rect(new Point(), new Size(src.Width, src.Height)));
                    }
                    bmp.Render(drawingVisual);
                    result.Source = bmp;
                }).Invoke(async: false);
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
#else
            catch (Exception ex) { ex.ERROR("THEME"); }
#endif
            return (result);
        }

        private static byte[] ClipboardBuffer = null;
        public static byte[] ToBytes(this string file)
        {
            if (string.IsNullOrEmpty(file)) return (null);

            byte[] result = null;

            if (File.Exists(file))
            {
                result = File.ReadAllBytes(file);
            }

            return (result);
        }

        public static async Task<byte[]> ToBytes(this BitmapSource bitmap, string fmt = "")
        {
            if (string.IsNullOrEmpty(fmt)) fmt = ".png";
            return ((await bitmap.ToMemoryStream(fmt)).ToArray());
        }

        public static async Task<byte[]> ToBytes(this byte[] buffer, string fmt = "")
        {
            if (string.IsNullOrEmpty(fmt)) fmt = ".png";
            var bitmap = await buffer.ToBitmapSource();
            return (await bitmap.ToBytes(fmt));
        }

        public static async Task<BitmapSource> ToBitmapSource(this byte[] buffer)
        {
            BitmapSource result = null;
            try
            {
                var ms = new MemoryStream(buffer);
                ms.Seek(0, SeekOrigin.Begin);
                result = BitmapFrame.Create(ms);
                await ms.FlushAsync();
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static async Task<MemoryStream> ToMemoryStream(this BitmapSource bitmap, string fmt = "")
        {
            MemoryStream result = new MemoryStream();
            try
            {
                if (string.IsNullOrEmpty(fmt)) fmt = ".png";
                dynamic encoder = null;
                switch (fmt)
                {
                    case "image/bmp":
                    case "image/bitmap":
                    case "CF_BITMAP":
                    case "CF_DIB":
                    case ".bmp":
                        encoder = new BmpBitmapEncoder();
                        break;
                    case "image/gif":
                    case "gif":
                    case ".gif":
                        encoder = new GifBitmapEncoder();
                        break;
                    case "image/png":
                    case "png":
                    case ".png":
                        encoder = new PngBitmapEncoder();
                        break;
                    case "image/jpg":
                    case ".jpg":
                        encoder = new JpegBitmapEncoder();
                        break;
                    case "image/jpeg":
                    case ".jpeg":
                        encoder = new JpegBitmapEncoder();
                        break;
                    case "image/tif":
                    case ".tif":
                        encoder = new TiffBitmapEncoder();
                        break;
                    case "image/tiff":
                    case ".tiff":
                        encoder = new TiffBitmapEncoder();
                        break;
                    default:
                        encoder = new PngBitmapEncoder();
                        break;
                }
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(result);
                await result.FlushAsync();
            }
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR[ENCODER]"); }
            return (result);
        }

        private static Dictionary<string, string[]> exts = new Dictionary<string, string[]>()
        {
            { ".png", new string[] { ".png", "image/png", "PNG" } },
            { ".bmp", new string[] { ".bmp", "image/bmp", "image/bitmap" } },
            { ".gif", new string[] { ".gif", "image/gif", "image/gif89" } },
            { ".tif", new string[] { ".tif", "image/tiff", "image/tif", ".tiff" } },
            { ".tiff", new string[] { ".tif", "image/tiff", "image/tif", ".tiff" } },
            { ".jpg", new string[] { ".jpg", "image/jpg", "image/jpeg", ".jpeg" } },
            { ".jpeg", new string[] { ".jpg", "image/jpg", "image/jpeg", ".jpeg" } },
        };

        public static async void CopyImage(this ImageSource source)
        {
            try
            {
                var bs = source.ToBitmapSource();

                DataObject dataPackage = new DataObject();
                MemoryStream ms = null;

                #region Copy Standard Bitmap date to Clipboard
                dataPackage.SetImage(bs);
                #endregion
                #region Copy other MIME format data to Clipboard
                string[] fmts = new string[] { "PNG", "image/png", "image/bmp", "image/jpg", "image/jpeg" };
                //string[] fmts = new string[] { };
                foreach (var fmt in fmts)
                {
                    if (fmt.Equals("CF_DIBV5", StringComparison.CurrentCultureIgnoreCase))
                    {
                        byte[] arr = await bs.ToBytes(fmt);
                        byte[] dib = arr.Skip(14).ToArray();
                        ms = new MemoryStream(dib);
                        dataPackage.SetData(fmt, ms);
                        await ms.FlushAsync();
                    }
                    else
                    {
                        byte[] arr = await bs.ToBytes(fmt);
                        ms = new MemoryStream(arr);
                        dataPackage.SetData(fmt, ms);
                        await ms.FlushAsync();
                    }
                }
                #endregion
                Clipboard.SetDataObject(dataPackage, true);
            }
            catch (Exception ex) { ex.Message.DEBUG(); }
        }

        public static async void CopyImage(this string file)
        {
            try
            {
                if (File.Exists(file))
                {
                    var ext = Path.GetExtension(file).ToLower();
                    ClipboardBuffer = file.ToBytes();
                    var bs = await ClipboardBuffer.ToBitmapSource();

                    DataObject dataPackage = new DataObject();
                    MemoryStream ms = null;

                    #region Copy Standard Bitmap date to Clipboard
                    dataPackage.SetImage(bs);
                    #endregion
                    #region Copy other MIME format data to Clipboard
                    string[] fmts = new string[] { "PNG", "image/png", "image/bmp", "image/jpg", "image/jpeg" };
                    //string[] fmts = new string[] { };
                    foreach (var fmt in fmts)
                    {
                        if (exts.ContainsKey(ext) && exts[ext].Contains(fmt))
                        {
                            ms = new MemoryStream(ClipboardBuffer);
                            dataPackage.SetData(fmt, ms);
                            await ms.FlushAsync();
                        }
                        else
                        {
                            if (fmt.Equals("CF_DIBV5", StringComparison.CurrentCultureIgnoreCase))
                            {
                                byte[] arr = await bs.ToBytes(fmt);
                                byte[] dib = arr.Skip(14).ToArray();
                                ms = new MemoryStream(dib);
                                dataPackage.SetData(fmt, ms);
                                await ms.FlushAsync();
                            }
                            else
                            {
                                byte[] arr = await bs.ToBytes(fmt);
                                ms = new MemoryStream(arr);
                                dataPackage.SetData(fmt, ms);
                                await ms.FlushAsync();
                            }
                        }
                    }
                    #endregion
                    Clipboard.SetDataObject(dataPackage, true);
                }
            }
#if DEBUG
            catch (Exception ex)
            {
                ex.Message.ShowMessageBox("ERROR[CLIPBOARD]");
            }
#else
            catch (Exception ex) { ex.ERROR(); }
#endif
        }

        #endregion

        #region Illust routines
        #region SameIllust
        public static bool IsSameIllust(this string id, int hash)
        {
            return (cache.IsSameIllust(hash, id));
        }

        public static bool IsSameIllust(this long id, int hash)
        {
            return (cache.IsSameIllust(hash, $"{id}"));
        }

        public static bool IsSameIllust(this long? id, int hash)
        {
            return (cache.IsSameIllust(hash, $"{id ?? -1}"));
        }

        public static bool IsSameIllust(this PixivItem item, int hash)
        {
            bool result = false;

            if (item.IsWork())
            {
                result = item.Illust.GetPreviewUrl(item.Index).GetImageId().IsSameIllust(hash) || item.Illust.GetOriginalUrl(item.Index).GetImageId().IsSameIllust(hash);
            }

            return (result);
        }

        public static bool IsSameIllust(this PixivItem item, long id)
        {
            bool result = false;

            try
            {
                result = long.Parse(item.ID) == id;
            }
            catch (Exception ex) { ex.ERROR(); }

            return (result);
        }

        public static bool IsSameIllust(this PixivItem item, long? id)
        {
            bool result = false;

            try
            {
                result = long.Parse(item.ID) == (id ?? -1);
            }
            catch (Exception ex) { ex.ERROR(); }

            return (result);
        }

        public static bool IsSameIllust(this PixivItem item, PixivItem item_now)
        {
            bool result = false;

            try
            {
                result = long.Parse(item.ID) == long.Parse(item_now.ID) && item.Index == item_now.Index;
            }
            catch (Exception ex) { ex.ERROR(); }

            return (result);
        }

        public static IList<PixivItem> GetSelected(this ImageListGrid gallery, bool WithSelectionOrder, bool NonForAll = false)
        {
            var result = new List<PixivItem>();
            try
            {
                if (Keyboard.Modifiers == ModifierKeys.Control) WithSelectionOrder = !WithSelectionOrder;
                var items = gallery.SelectedItems.Count <= 0 && NonForAll ? gallery.Items : gallery.SelectedItems;
                if (WithSelectionOrder)
                {
                    result = items.ToList();
                }
                else
                {
                    foreach (var item in gallery.Items)
                    {
                        if (items.Contains(item)) result.Add(item);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static IList<PixivItem> GetSelected(this ImageListGrid gallery, bool NonForAll)
        {
            setting = Application.Current.LoadSetting();
            return (GetSelected(gallery, setting.OpenWithSelectionOrder, NonForAll));
        }

        public static IList<PixivItem> GetSelected(this ImageListGrid gallery)
        {
            setting = Application.Current.LoadSetting();
            return (GetSelected(gallery, setting.OpenWithSelectionOrder, setting.AllForSelectionNone));
        }

        public static IList<PixivItem> GetSelected(this ImageListGrid gallery, PixivItemType type)
        {
            setting = Application.Current.LoadSetting();
            var selected = GetSelected(gallery, setting.OpenWithSelectionOrder, setting.AllForSelectionNone);
            return (selected.Where(i => i.ItemType == type).ToList());
        }

        public static IList<PixivItem> GetSelectedIllusts(this ImageListGrid gallery)
        {
            setting = Application.Current.LoadSetting();
            var selected = GetSelected(gallery, setting.OpenWithSelectionOrder, setting.AllForSelectionNone);
            return (selected.Where(i => i.IsWork()).ToList());
        }

        public static IList<PixivItem> GetSelectedUsers(this ImageListGrid gallery)
        {
            setting = Application.Current.LoadSetting();
            var selected = GetSelected(gallery, setting.OpenWithSelectionOrder, setting.AllForSelectionNone);
            return (selected.Where(i => i.IsUser()).ToList());
        }
        #endregion

        #region History routines
        public static void AddToHistory(this PixivItem item)
        {
            //Commands.AddToHistory.Execute(illust);
            var win = "History".GetWindowByTitle();
            if (win is ContentWindow && win.Content is HistoryPage)
                (win.Content as HistoryPage).AddToHistory(item);
            else
                Application.Current.HistoryAdd(item);
        }

        public static void AddToHistory(this Pixeez.Objects.Work illust)
        {
            //Commands.AddToHistory.Execute(illust);
            var win = "History".GetWindowByTitle();
            if (win is ContentWindow && win.Content is HistoryPage)
                (win.Content as HistoryPage).AddToHistory(illust);
            else
                Application.Current.HistoryAdd(illust);
        }

        public static void AddToHistory(this Pixeez.Objects.User user)
        {
            //Commands.AddToHistory.Execute(user);
            var win = "History".GetWindowByTitle();
            if (win is ContentWindow && win.Content is HistoryPage)
                (win.Content as HistoryPage).AddToHistory(user);
            else
                Application.Current.HistoryAdd(user);
        }

        public static void AddToHistory(this Pixeez.Objects.UserBase user)
        {
            //Commands.AddToHistory.Execute(user);
            var win = "History".GetWindowByTitle();
            if (win is ContentWindow && win.Content is HistoryPage)
                (win.Content as HistoryPage).AddToHistory(user);
            else
                Application.Current.HistoryAdd(user);
        }

        public static void ShowHistory(this Application app)
        {
            Commands.OpenHistory.Execute(null);
        }
        #endregion

        #region Refresh Illust/User Info
        public static async Task<Pixeez.Objects.Work> RefreshIllust(this Pixeez.Objects.Work Illust, Pixeez.Tokens tokens = null)
        {
            var result = Illust.Id != null ? await RefreshIllust(Illust.Id.Value, tokens) : Illust;
            try
            {
                if (Illust is Pixeez.Objects.IllustWork)
                {
                    var i = Illust as Pixeez.Objects.IllustWork;
                    if (result is Pixeez.Objects.IllustWork)
                    {
                        var r = result as Pixeez.Objects.IllustWork;
                        i.is_bookmarked = r.is_bookmarked;
                        i.is_muted = r.is_muted;
                        i.IsLiked = r.IsLiked;
                        i.IsManga = r.IsManga;
                    }
                    else if (result is Pixeez.Objects.NormalWork)
                    {
                        var r = result as Pixeez.Objects.NormalWork;
                        i.IsLiked = r.IsLiked;
                        i.IsManga = r.IsManga;
                        i.is_bookmarked = r.BookMarked;
                    }
                }
                else if (Illust is Pixeez.Objects.NormalWork)
                {
                    var i = Illust as Pixeez.Objects.NormalWork;
                    if (result is Pixeez.Objects.IllustWork)
                    {
                        var r = result as Pixeez.Objects.IllustWork;
                        i.IsLiked = r.IsLiked;
                        i.IsManga = r.IsManga;
                    }
                    else if (result is Pixeez.Objects.NormalWork)
                    {
                        var r = result as Pixeez.Objects.NormalWork;
                        i.IsLiked = r.IsLiked;
                        i.IsManga = r.IsManga;
                    }
                }

                if (string.IsNullOrEmpty(result.ImageUrls.Px128x128)) result.ImageUrls.Px128x128 = Illust.ImageUrls.Px128x128;
                if (string.IsNullOrEmpty(result.ImageUrls.Px480mw)) result.ImageUrls.Px480mw = Illust.ImageUrls.Px480mw;
                if (string.IsNullOrEmpty(result.ImageUrls.SquareMedium)) result.ImageUrls.SquareMedium = Illust.ImageUrls.SquareMedium;
                if (string.IsNullOrEmpty(result.ImageUrls.Small)) result.ImageUrls.Small = Illust.ImageUrls.Small;
                if (string.IsNullOrEmpty(result.ImageUrls.Medium)) result.ImageUrls.Medium = Illust.ImageUrls.Medium;
                if (string.IsNullOrEmpty(result.ImageUrls.Large)) result.ImageUrls.Large = Illust.ImageUrls.Large;
                if (string.IsNullOrEmpty(result.ImageUrls.Original)) result.ImageUrls.Original = Illust.ImageUrls.Original;
            }
            catch (Exception ex) { ex.ERROR("REFRESHILLUST"); }
            return (result);
        }

        public static async Task<Pixeez.Objects.Work> RefreshIllust(this string IllustID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.Work result = null;
            try
            {
                if (!string.IsNullOrEmpty(IllustID))
                    result = await RefreshIllust(Convert.ToInt32(IllustID), tokens);
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static async Task<Pixeez.Objects.Work> RefreshIllust(this long IllustID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.Work result = null;
            if (IllustID < 0) return result;
            if (tokens == null) tokens = await ShowLogin();
            if (tokens == null) return result;
            try
            {
                var illusts = await tokens.GetWorksAsync(IllustID);
                if (illusts is List<Pixeez.Objects.NormalWork>)
                {
                    foreach (var illust in illusts)
                    {
                        illust.Cache();
                        result = illust;
                        break;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("REFRESHILLUST"); }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserBase> RefreshUser(this Pixeez.Objects.Work Illust, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.UserBase result = Illust.User;
            try
            {
                var user = await Illust.User.RefreshUser(tokens);
                if (user.Id.Value == Illust.User.Id.Value)
                {
                    //Illust.User.is_followed = user.is_followed;
                    result = user;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserBase> RefreshUser(this Pixeez.Objects.UserBase User, Pixeez.Tokens tokens = null)
        {
            var user = await RefreshUser(User.Id.Value);
            User.is_followed = user.is_followed;
            try
            {
                if (User is Pixeez.Objects.User)
                {
                    var u = User as Pixeez.Objects.User;
                    u.IsFollowed = user.IsFollowed;
                    u.IsFollower = user.IsFollower;
                    u.IsFollowing = user.IsFollowing;
                    u.IsFriend = user.IsFriend;
                    u.IsPremium = user.IsFriend;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (user);
        }

        public static async Task<Pixeez.Objects.User> RefreshUser(this string UserID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.User result = null;
            if (!string.IsNullOrEmpty(UserID))
            {
                try
                {
                    result = await RefreshUser(Convert.ToInt32(UserID), tokens);
                }
                catch (Exception ex) { ex.ERROR(); }
            }
            return (result);
        }

        public static async Task<Pixeez.Objects.User> RefreshUser(this long UserID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.User result = null;
            if (UserID < 0) return (result);
            setting = Application.Current.LoadSetting();
            var force = UserID == 0 && !(setting.MyInfo is Pixeez.Objects.User) ? true : false;
            if (tokens == null) tokens = await ShowLogin(force);
            if (tokens == null) return (result);
            try
            {
                var users = await tokens.GetUsersAsync(UserID);
                foreach (var user in users)
                {
                    user.Cache();
                    if (user.Id.Value == UserID) result = user;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserInfo> RefreshUserInfo(this string UserID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.UserInfo result = null;
            if (!string.IsNullOrEmpty(UserID))
            {
                try
                {
                    long id = -1;
                    if (long.TryParse(UserID, out id)) result = await RefreshUserInfo(id, tokens);
                }
                catch (Exception ex) { ex.ERROR(); }
            }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserInfo> RefreshUserInfo(this long UserID, Pixeez.Tokens tokens = null)
        {
            Pixeez.Objects.UserInfo result = null;
            if (UserID < 0) return (result);
            setting = Application.Current.LoadSetting();
            var force = UserID == 0 && !(setting.MyInfo is Pixeez.Objects.User) ? true : false;
            if (tokens == null) tokens = await ShowLogin(force);
            if (tokens == null) return (result);
            try
            {
                var userinfo = await tokens.GetUserInfoAsync($"{UserID}");
                if (userinfo is Pixeez.Objects.UserInfo)
                {
                    userinfo.Cache();
                    if (userinfo.user.Id.Value == UserID) result = userinfo;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static async Task<Pixeez.Objects.UserInfo> RefreshUserInfo(this long? UserID, Pixeez.Tokens tokens = null)
        {
            return (await RefreshUserInfo(UserID.Value));
        }

        public static async Task<Pixeez.Objects.UserInfo> RefreshUserInfo(this Pixeez.Objects.UserBase User, Pixeez.Tokens tokens = null)
        {
            return (await RefreshUserInfo(User.Id));
        }
        #endregion

        #region Like helper routines
        public static bool IsLiked(this Pixeez.Objects.Work illust)
        {
            bool result = false;
            if (illust is Pixeez.Objects.Work && illust.User is Pixeez.Objects.UserBase)
            {
                if (!IllustCache.ContainsKey(illust.Id)) illust.Cache();
                result = IllustCache[illust.Id].IsBookMarked();
            }
            return (result);
        }

        public static bool IsLiked(this Pixeez.Objects.UserBase user)
        {
            bool result = false;
            if (user is Pixeez.Objects.UserBase)
            {
                if (!UserCache.ContainsKey(user.Id)) user.Cache();
                var u = UserCache[user.Id];
                if (u is Pixeez.Objects.User)
                {
                    var old_user = u as Pixeez.Objects.User;
                    result = old_user.is_followed ?? old_user.IsFollowing ?? old_user.IsFollowed ?? false;
                }
                else if (u is Pixeez.Objects.NewUser)
                {
                    var old_user = u as Pixeez.Objects.NewUser;
                    result = old_user.is_followed ?? false;
                }
            }
            return (result);
        }

        public static bool IsLiked(this PixivItem item)
        {
            var result = false;
            if (item.IsUser()) result = item.User.IsLiked();
            else if (item.IsWork()) result = item.Illust.IsLiked();
            return (result);
        }

        public static async Task<bool> Like(this PixivItem item, bool pub = true)
        {
            if (item.IsWork())
            {
                var result = item.Illust.IsLiked() ? true : await item.LikeIllust(pub);
                UpdateLikeStateAsync((int)(item.Illust.Id));
                return (result);
            }
            else if (item.IsUser())
            {
                var result = item.User.IsLiked() ? true : await item.LikeUser(pub);
                UpdateLikeStateAsync((int)(item.User.Id), true);
                return (result);
            }
            else return false;
        }

        public static async Task<bool> UnLike(this PixivItem item, bool pub = true)
        {
            if (item.IsWork())
            {
                var result = item.Illust.IsLiked() ? await item.UnLikeIllust(pub) : false;
                UpdateLikeStateAsync((int)(item.Illust.Id));
                return (result);
            }
            else if (item.IsUser())
            {
                var result = item.User.IsLiked() ? await item.UnLikeUser(pub) : false;
                item.IsFavorited = result;
                UpdateLikeStateAsync((int)(item.User.Id), true);
                return (result);
            }
            else return false;
        }
        #endregion

        #region Like/Unlike Illust helper routines
        public class BookmarkState
        {
            public bool State { get; set; } = false;
            public bool IsBookmarked { get; set; } = false;
            public string Restrict { get; set; } = string.Empty;
        }
        public static async Task<BookmarkState> RefreshIllustBookmarkState(this Pixeez.Objects.Work illust)
        {
            BookmarkState result = new BookmarkState();

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            var bookmarkstate = await tokens.GetBookMarkedDetailAsync(illust.Id??-1);
            if (bookmarkstate is Pixeez.Objects.BookmarkDetailRootobject && bookmarkstate.bookmark_detail is Pixeez.Objects.BookmarkDetail)
            {
                var is_bookmarked = bookmarkstate.bookmark_detail.is_bookmarked;
                var restrict = bookmarkstate.bookmark_detail.restrict;
                if (illust is Pixeez.Objects.IllustWork)
                {
                    var i = illust as Pixeez.Objects.IllustWork;
                    i.is_bookmarked = is_bookmarked;
                }
                else if (illust is Pixeez.Objects.NormalWork)
                {
                    var i = illust as Pixeez.Objects.NormalWork;
                    i.IsLiked = is_bookmarked;
                }
                result.State = true;
                result.Restrict = restrict;
                result.IsBookmarked = is_bookmarked;
            }

            return (result);
        }

        /// <summary>
        /// Like Illust Work
        /// </summary>
        /// <param name="illust"></param>
        /// <param name="pub"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, Pixeez.Objects.Work>> LikeIllust(this Pixeez.Objects.Work illust, bool pub = true)
        {
            Tuple<bool, Pixeez.Objects.Work> result = new Tuple<bool, Pixeez.Objects.Work>(illust.IsLiked(), illust);

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            try
            {
                var mode = pub ? "public" : "private";
                await tokens.AddMyFavoriteWorksAsync((long)illust.Id, illust.Tags, mode);
            }
            catch (Exception ex) { ex.ERROR("LIKEILLUST"); }
            finally
            {
                try
                {
                    illust = await illust.RefreshIllust();
                    if (illust != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.Work>(illust.IsLiked(), illust);
                        var info = "Liked";
                        var title = result.Item1 ? "Succeed" : "Failed";
                        var fail = result.Item1 ? "is" : "isn't";
                        var pub_like = pub ? "Public" : "Private";
                        $"Illust \"{illust.Title}\" {fail} {pub_like} {info}!".ShowToast($"{title}", illust.GetThumbnailUrl(), title, pub_like);
                    }
                }
                catch (Exception ex) { ex.ERROR("LIKEILLUST"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            }

            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.Work>> Like(this Pixeez.Objects.Work illust, bool pub = true)
        {
            var result = await illust.LikeIllust(pub);
            UpdateLikeStateAsync((int)(illust.Id.Value), false);
            return (result);
        }

        public static async Task<bool> LikeIllust(this PixivItem item, bool pub = true)
        {
            bool result = false;

            if (item.IsWork())
            {
                var ret = await item.Illust.Like(pub);
                result = ret.Item1;
                item.Illust = ret.Item2;
                item.IsFavorited = result;
                if (item.Source == null)
                {
                    var thumb = await item.Thumb.LoadImageFromUrl(size: Application.Current.GetDefaultThumbSize());
                    item.Source = thumb.Source;
                    item.State = TaskStatus.RanToCompletion;
                    thumb.Source = null;
                }
            }

            return (result);
        }

        public static void LikeIllust(this ObservableCollection<PixivItem> collection, bool pub = true)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            //var items = collection.Distinct();
            var items = collection.GroupBy(i => i.ID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item.IsWork())
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.LikeIllust(pub);
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }).InvokeAsync();
                }
            });
        }

        public static void LikeIllust(this IList<PixivItem> collection, bool pub = true)
        {
            LikeIllust(new ObservableCollection<PixivItem>(collection), pub);
        }

        /// <summary>
        /// Unlike Illust Work
        /// </summary>
        /// <param name="illust"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, Pixeez.Objects.Work>> UnLikeIllust(this Pixeez.Objects.Work illust)
        {
            Tuple<bool, Pixeez.Objects.Work> result = new Tuple<bool, Pixeez.Objects.Work>(false, illust);

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            try
            {
                await tokens.DeleteMyFavoriteWorksAsync((long)illust.Id);
                await tokens.DeleteMyFavoriteWorksAsync((long)illust.Id, "private");
            }
            catch (Exception ex) { ex.ERROR("UNLIKEILLUST"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            finally
            {
                try
                {
                    //var bs = await illust.RefreshIllustBookmarkState();
                    //result = new Tuple<bool, Pixeez.Objects.Work>(bs.IsBookmarked, illust);
                    //var info = "Unliked";
                    //var title = result.Item1 ? "Failed" : "Succeed";
                    //var fail = result.Item1 ?  "isn't" : "is";
                    //$"Illust \"{illust.Title}\" {fail} {info}!".ShowToast(title, illust.GetThumbnailUrl(), title);

                    illust = await illust.RefreshIllust();
                    if (illust != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.Work>(illust.IsLiked(), illust);
                        var info = "Unliked";
                        var title = result.Item1 ? "Failed" : "Succeed";
                        var fail = result.Item1 ?  "isn't" : "is";
                        $"Illust \"{illust.Title}\" {fail} {info}!".ShowToast(title, illust.GetThumbnailUrl(), title);
                    }
                }
                catch (Exception ex) { ex.ERROR("UNLIKEILLUST"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            }

            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.Work>> UnLike(this Pixeez.Objects.Work illust)
        {
            var result = await illust.UnLikeIllust();
            UpdateLikeStateAsync((int)(illust.Id.Value), false);
            return (result);
        }

        public static async Task<bool> UnLikeIllust(this PixivItem item, bool pub = true)
        {
            bool result = false;
            if (item.IsWork())
            {
                var ret = await item.Illust.UnLike();
                result = ret.Item1;
                item.Illust = ret.Item2;
                item.IsFavorited = result;
                if (item.Source == null)
                {
                    var thumb = await item.Thumb.LoadImageFromUrl(size: Application.Current.GetDefaultThumbSize());
                    item.Source = thumb.Source;
                    item.State = TaskStatus.RanToCompletion;
                    thumb.Source = null;
                }
            }
            return (result);
        }

        public static void UnLikeIllust(this ObservableCollection<PixivItem> collection)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            var items = collection.GroupBy(i => i.ID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item.IsWork())
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.UnLikeIllust();
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }).InvokeAsync();
                }
            });
        }

        public static void UnLikeIllust(this IList<PixivItem> collection)
        {
            UnLikeIllust(new ObservableCollection<PixivItem>(collection));
        }

        /// <summary>
        /// Toggle Illust Work Like State
        /// </summary>
        /// <param name="user"></param>
        /// <param name="pub"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, Pixeez.Objects.Work>> ToggleLikeIllust(this Pixeez.Objects.Work illust, bool pub = true)
        {
            var result = illust.IsLiked() ? await illust.UnLikeIllust() : await illust.LikeIllust(pub);
            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.Work>> ToggleLike(this Pixeez.Objects.Work illust, bool pub = true)
        {
            var result = await illust.ToggleLikeIllust(pub);
            UpdateLikeStateAsync((int)(illust.Id.Value), false);
            return (result);
        }

        public static async Task<bool> ToggleLikeIllust(this PixivItem item, bool pub = true)
        {
            bool result = false;
            if (item.IsWork())
            {
                var ret = await item.Illust.ToggleLike(pub);
                result = ret.Item1;
                item.Illust = ret.Item2;
                item.IsFavorited = result;
                if (item.Source == null) item.State = TaskStatus.RanToCompletion;
            }
            return (result);
        }

        public static void ToggleLikeIllust(this ObservableCollection<PixivItem> collection, bool pub = true)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            //var items = collection.Distinct();
            var items = collection.GroupBy(i => i.ID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item.IsWork())
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.ToggleLikeIllust(pub);
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }).InvokeAsync();
                }
            });
        }

        public static void ToggleLikeIllust(this IList<PixivItem> collection, bool pub = true)
        {
            ToggleLikeIllust(new ObservableCollection<PixivItem>(collection), pub);
        }
        #endregion

        #region Like/Unlike User helper routines
        /// <summary>
        /// Like user
        /// </summary>
        /// <param name="user"></param>
        /// <param name="pub"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> LikeUser(this Pixeez.Objects.UserBase user, bool pub = true)
        {
            Tuple<bool, Pixeez.Objects.UserBase> result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            try
            {
                var mode = pub ? "public" : "private";
                await tokens.AddFavouriteUser((long)user.Id, mode);
            }
            catch (Exception ex) { ex.ERROR("LIKEUSER"); }
            finally
            {
                try
                {
                    user = await user.RefreshUser();
                    if (user != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);
                        var info = "Liked";
                        var title = result.Item1 ? "Succeed" : "Failed";
                        var fail = result.Item1 ?  "is" : "isn't";
                        var pub_like = pub ? "Public" : "Private";
                        $"User \"{user.Name ?? string.Empty}\" {fail} {pub_like} {info}!".ShowToast(title, user.GetAvatarUrl(), title, pub_like);
                    }
                }
                catch (Exception ex) { ex.ERROR("LIKEUSER"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            }
            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> Like(this Pixeez.Objects.UserBase user, bool pub = true)
        {
            var result = await user.LikeUser(pub);
            UpdateLikeStateAsync((int)(user.Id.Value), true);
            return (result);
        }

        public static async Task<bool> LikeUser(this PixivItem item, bool pub = true)
        {
            bool result = false;

            if (item.HasUser())
            {
                try
                {
                    var user = item.User;
                    var ret = await user.Like(pub);
                    result = ret.Item1;
                    item.User = ret.Item2;
                    if (item.IsUser())
                    {
                        item.IsFavorited = result;
                    }
                    if (item.Source == null)
                    {
                        var thumb = await item.Thumb.LoadImageFromUrl(size: Application.Current.GetDefaultThumbSize());
                        item.Source = thumb.Source;
                        item.State = TaskStatus.RanToCompletion;
                        thumb.Source = null;
                    }
                }
                catch (Exception ex) { ex.ERROR("LIKEUSER"); }
            }

            return (result);
        }

        public static void LikeUser(this ObservableCollection<PixivItem> collection, bool pub = true)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            //var items = collection.Distinct();
            var items = collection.GroupBy(i => i.UserID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item.HasUser())
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.LikeUser(pub);
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }).InvokeAsync();
                }
            });
        }

        public static void LikeUser(this IList<PixivItem> collection, bool pub = true)
        {
            LikeUser(new ObservableCollection<PixivItem>(collection), pub);
        }

        /// <summary>
        /// Unlike user 
        /// </summary>
        /// <param name="item"></param>
        /// <param name="pub"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> UnLikeUser(this Pixeez.Objects.UserBase user)
        {
            Tuple<bool, Pixeez.Objects.UserBase> result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);

            var tokens = await ShowLogin();
            if (tokens == null) return (result);

            try
            {
                await tokens.DeleteFavouriteUser(user.Id.ToString());
                await tokens.DeleteFavouriteUser(user.Id.ToString(), "private");
            }
            catch (Exception ex) { ex.ERROR("UNLIKEUSER"); }
            finally
            {
                try
                {
                    user = await user.RefreshUser();
                    if (user != null)
                    {
                        result = new Tuple<bool, Pixeez.Objects.UserBase>(user.IsLiked(), user);
                        var info = "Unliked";
                        var title = result.Item1 ? "Failed" : "Succeed";
                        var fail = result.Item1 ?  "isn't" : "is";
                        $"User \"{user.Name ?? string.Empty}\" {fail} {info}!".ShowToast(title, user.GetAvatarUrl(), title);
                    }
                }
                catch (Exception ex) { ex.ERROR("UNLIKEUSER"); if (ex.Message.Contains("404")) ex.Message.ShowToast("INFO"); }
            }
            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> UnLike(this Pixeez.Objects.UserBase user)
        {
            var result = await user.UnLikeUser();
            UpdateLikeStateAsync((int)(user.Id.Value), true);
            return (result);
        }

        public static async Task<bool> UnLikeUser(this PixivItem item, bool pub = true)
        {
            bool result = false;

            if (item.HasUser())
            {
                try
                {
                    var user = item.User;
                    var ret = await user.UnLike();
                    result = ret.Item1;
                    item.User = ret.Item2;
                    if (item.IsUser())
                    {
                        item.IsFavorited = result;
                    }
                    if (item.Source == null)
                    {
                        var thumb = await item.Thumb.LoadImageFromUrl(size: Application.Current.GetDefaultThumbSize());
                        item.Source = thumb.Source;
                        item.State = TaskStatus.RanToCompletion;
                        thumb.Source = null;
                    }
                }
                catch (Exception ex) { ex.ERROR("UNLIKEUSER"); }
            }

            return (result);
        }

        public static void UnLikeUser(this ObservableCollection<PixivItem> collection)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            var items = collection.GroupBy(i => i.UserID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item.HasUser())
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.UnLikeUser();
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }).InvokeAsync();
                }
            });
        }

        public static void UnLikeUser(this IList<PixivItem> collection)
        {
            UnLikeUser(new ObservableCollection<PixivItem>(collection));
        }

        /// <summary>
        /// Toggle User Like State
        /// </summary>
        /// <param name="user"></param>
        /// <param name="pub"></param>
        /// <returns></returns>
        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> ToggleLikeUser(this Pixeez.Objects.UserBase user, bool pub = true)
        {
            var result = user.IsLiked() ? await user.UnLikeUser() : await user.LikeUser(pub);
            return (result);
        }

        public static async Task<Tuple<bool, Pixeez.Objects.UserBase>> ToggleLike(this Pixeez.Objects.UserBase user, bool pub = true)
        {
            var result = await user.ToggleLikeUser(pub);
            UpdateLikeStateAsync((int)(user.Id.Value), true);
            return (result);
        }

        public static async Task<bool> ToggleLikeUser(this PixivItem item, bool pub = true)
        {
            bool result = false;

            if (item.HasUser())
            {
                try
                {
                    var user = item.User;
                    var ret =  await user.ToggleLike(pub);
                    result = ret.Item1;
                    item.User = ret.Item2;
                    if (item.IsUser())
                    {
                        item.IsFavorited = result;
                    }
                    if (item.Source == null) item.State = TaskStatus.RanToCompletion;
                }
                catch (Exception ex) { ex.ERROR(); }
            }

            return (result);
        }

        public static void ToggleLikeUser(this ObservableCollection<PixivItem> collection, bool pub = true)
        {
            var opt = new ParallelOptions();
            opt.MaxDegreeOfParallelism = 5;
            //var items = collection.Distinct();
            var items = collection.GroupBy(i => i.UserID).Select(g => g.First()).ToList();
            var ret = Parallel.ForEach(items, opt, (item, loopstate, elementIndex) =>
            {
                if (item.HasUser())
                {
                    var ua = new Action(async()=>
                    {
                        try
                        {
                            var result = await item.ToggleLikeUser(pub);
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }).InvokeAsync();
                }
            });
        }

        public static void ToggleLikeUser(this IList<PixivItem> collection, bool pub = true)
        {
            ToggleLikeUser(new ObservableCollection<PixivItem>(collection), pub);
        }
        #endregion

        #region Update/Find Illust/User info cache
        public static void Cache(this Pixeez.Objects.UserBase user)
        {
            if (user is Pixeez.Objects.UserBase)
                UserCache[user.Id] = user;
        }

        public static void Cache(this Pixeez.Objects.Work illust)
        {
            if (illust is Pixeez.Objects.Work)
                IllustCache[illust.Id] = illust;
        }

        public static void Cache(this Pixeez.Objects.UserInfo userinfo)
        {
            if (userinfo is Pixeez.Objects.UserInfo)
                UserInfoCache[userinfo.user.Id] = userinfo;
        }

        public static Pixeez.Objects.Work FindIllust(this long id)
        {
            if (IllustCache.ContainsKey(id)) return (IllustCache[id]);
            else return (null);
        }

        public static Pixeez.Objects.Work FindIllust(this long? id)
        {
            if (id != null && IllustCache.ContainsKey(id)) return (IllustCache[id]);
            else return (null);
        }

        public static Pixeez.Objects.Work FindIllust(this string id)
        {
            long idv = 0;
            if (long.TryParse(id, out idv)) return (FindIllust(idv));
            else return (null);
        }

        public static Pixeez.Objects.Work FindIllust(this Pixeez.Objects.Work work)
        {
            return (FindIllust(work));
        }

        public static Pixeez.Objects.UserBase FindUser(this long id)
        {
            if (UserCache.ContainsKey(id)) return (UserCache[id]);
            else return (null);
        }

        public static Pixeez.Objects.UserBase FindUser(this long? id)
        {
            if (id != null && UserCache.ContainsKey(id)) return (UserCache[id]);
            else return (null);
        }

        public static Pixeez.Objects.UserBase FindUser(this string id)
        {
            long idv = 0;
            if (long.TryParse(id, out idv)) return (FindUser(idv));
            else return (null);
        }

        public static Pixeez.Objects.UserBase FindUser(this Pixeez.Objects.UserBase user)
        {
            return (FindUser(user.Id));
        }

        public static Pixeez.Objects.UserInfo FindUserInfo(this long id)
        {
            if (UserInfoCache.ContainsKey(id)) return (UserInfoCache[id]);
            else return (null);
        }

        public static Pixeez.Objects.UserInfo FindUserInfo(this long? id)
        {
            if (id != null && UserInfoCache.ContainsKey(id)) return (UserInfoCache[id]);
            else return (null);
        }

        public static Pixeez.Objects.UserInfo FindUserInfo(this string id)
        {
            long idv = 0;
            if (long.TryParse(id, out idv)) return (FindUserInfo(idv));
            else return (null);
        }

        public static Pixeez.Objects.UserInfo FindUserInfo(this Pixeez.Objects.UserBase user)
        {
            return (FindUserInfo(user.Id));
        }
        #endregion

        #region Sync Illust/User Like State
        public static void UpdateLikeStateAsync(string illustid = default(string), bool is_user = false)
        {
            int id = -1;
            int.TryParse(illustid, out id);
            UpdateLikeStateAsync(id);
        }

        public static async void UpdateLikeStateAsync(int illustid = -1, bool is_user = false)
        {
            await new Action(() =>
            {
                foreach (var win in Application.Current.Windows)
                {
                    if (win is MainWindow)
                    {
                        var mw = win as MainWindow;
                        mw.UpdateLikeState(illustid, is_user);
                    }
                    else if (win is ContentWindow)
                    {
                        var w = win as ContentWindow;
                        if (w.Content is IllustDetailPage)
                            (w.Content as IllustDetailPage).UpdateLikeStateAsync(illustid, is_user);
                        else if (w.Content is IllustImageViewerPage)
                            (w.Content as IllustImageViewerPage).UpdateLikeStateAsync(illustid, is_user);
                        else if (w.Content is SearchResultPage)
                            (w.Content as SearchResultPage).UpdateLikeStateAsync(illustid, is_user);
                        else if (w.Content is DownloadManagerPage)
                            (w.Content as DownloadManagerPage).UpdateLikeStateAsync(illustid, is_user);
                        else if (w.Content is HistoryPage)
                            (w.Content as HistoryPage).UpdateLikeStateAsync(illustid, is_user);
                    }
                }
            }).InvokeAsync();
        }

        public static void UpdateLikeState(this ImageListGrid list, int illustid = -1, bool is_user = false)
        {
            list.Items.UpdateLikeState(illustid, is_user);
        }

        public static void UpdateLikeState(this ObservableCollection<PixivItem> collection, int illustid = -1, bool is_user = false)
        {
            foreach (PixivItem item in collection)
            {
                int item_id = -1;
                int.TryParse(item.ID, out item_id);
                int user_id = -1;
                int.TryParse(item.UserID, out user_id);

                try
                {
                    if (is_user) item_id = user_id;
                    if (illustid == -1 || illustid == item_id)
                    {
                        if (item.IsUser())
                        {
                            item.IsFavorited = false;
                            item.IsFollowed = item.User.IsLiked();
                        }
                        else if (item.IsPage() || item.IsPages())
                        {
                            item.IsFavorited = false;
                            item.IsFollowed = false;
                        }
                        else if (item.IsWork())
                        {
                            item.IsFavorited = item.Illust.IsLiked();
                            item.IsFollowed = item.User.IsLiked();
                        }
                        else
                        {
                            item.IsFavorited = item.IsFollowed = false;
                        }
                    }
                }
                catch { }
            }
        }
        #endregion

        #endregion

        #region UI Element Show/Hide
        public static string GetUid(this object obj)
        {
            string result = string.Empty;

            if (obj is UIElement)
            {
                result = (obj as UIElement).Uid;
            }

            return (result);
        }

        public static void UpdateTheme(this Window win, Image icon = null)
        {
            try
            {
                new Action(() =>
                {
                    if (icon == null)
                        icon = "Resources/pixiv-icon.ico".MakePackUri().GetThemedImage();
                    win.Icon = icon.Source;

                    if (win is MainWindow)
                    {
                        (win as MainWindow).UpdateTheme();
                    }
                    else if (win is ContentWindow)
                    {
                        if (win.Content is IllustDetailPage)
                        {
                            var page = win.Content as IllustDetailPage;
                            page.UpdateTheme();
                        }
                        else if (win.Content is IllustImageViewerPage)
                        {
                            var page = win.Content as IllustImageViewerPage;
                            page.UpdateTheme();
                        }
                        else if (win.Content is DownloadManagerPage)
                        {
                            var page = win.Content as DownloadManagerPage;
                            page.UpdateTheme();
                        }
                        else if (win.Title.Equals("DropBox", StringComparison.CurrentCultureIgnoreCase))
                        {
                            win.Background = Theme.AccentBrush;
                            win.Content = icon;
                        }
                    }
                }).Invoke(async: false);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void UpdateTheme()
        {
            try
            {
                new Action(() =>
                {
                    var img = "Resources/pixiv-icon.ico".MakePackUri().GetThemedImage();
                    foreach (Window win in Application.Current.Windows)
                    {
                        if (win is MetroWindow) win.UpdateTheme(img);
                    }
                }).Invoke(async: false);
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static bool IsShown(this UIElement element)
        {
            return (element.Visibility == Visibility.Visible ? true : false);
        }

        public static bool IsHidden(this UIElement element)
        {
            return (element.Visibility != Visibility.Visible ? true : false);
        }

        public static void Show(this ProgressRing progress, bool show, bool active = true)
        {
            if (progress is ProgressRing)
            {
                if (show)
                {
                    progress.Visibility = Visibility.Visible;
                    progress.IsEnabled = true;
                    progress.IsActive = active;
                }
                else
                {
                    progress.Visibility = Visibility.Collapsed;
                    progress.IsEnabled = false;
                    progress.IsActive = false;
                }
            }
        }

        public static void Pause(this ProgressRing progress)
        {
            progress.IsActive = false;
        }

        public static void Resume(this ProgressRing progress)
        {
            progress.IsEnabled = true;
            progress.IsActive = true;
        }

        public static void Disable(this ProgressRing progress)
        {
            progress.IsEnabled = false;
            progress.IsActive = false;
        }

        public static void Show(this ProgressRing progress, bool active = true)
        {
            progress.Show(true, active);
        }

        public static void Hide(this ProgressRing progress)
        {
            progress.Show(false, false);
        }

        public static void Show(this UIElement element, bool show, bool parent = false)
        {
            if (element is UIElement)
            {
                if (show)
                    element.Visibility = Visibility.Visible;
                else
                    element.Visibility = Visibility.Collapsed;

                if (parent && element.GetParentObject() is UIElement)
                    (element.GetParentObject() as UIElement).Visibility = element.Visibility;
            }
        }

        public static void Show(this UIElement element, bool parent = false)
        {
            if (element is UIElement) (element as UIElement).Show(true, parent);
        }

        public static void Show(this object element, bool parent = false)
        {
            if (element is UIElement) element.Show(parent);
        }

        public static void Hide(this UIElement element, bool parent = false)
        {
            if (element is UIElement) element.Show(false, parent);
        }

        public static void Hide(this object element, bool parent = false)
        {
            if (element is UIElement) (element as UIElement).Hide(parent);
        }

        public static void Enable(this Control element, bool state, bool show = true)
        {
            if (element is Control)
            {
                element.IsEnabled = state;
                element.Foreground = state ? Theme.AccentBrush : Theme.GrayBrush;
                if (show)
                    element.Visibility = Visibility.Visible;
                else
                    element.Visibility = Visibility.Collapsed;
            }
        }

        public static void Enable(this Control element)
        {
            if (element is Control)
            {
                element.IsEnabled = true;
                element.Foreground = Theme.AccentBrush;
                element.Visibility = Visibility.Visible;
            }
        }

        public static void Disable(this Control element, bool state, bool show = true)
        {
            if (element is Control)
            {
                element.IsEnabled = !state;
                element.Foreground = state ? Theme.GrayBrush : Theme.AccentBrush;
                if (show)
                    element.Visibility = Visibility.Visible;
                else
                    element.Visibility = Visibility.Collapsed;
            }
        }

        public static void Disable(this Control element)
        {
            if (element is Control)
            {
                element.IsEnabled = false;
                element.Foreground = Theme.GrayBrush;
                element.Visibility = Visibility.Visible;
            }
        }
        #endregion

        #region Button MouseOver Action
        public static void MouseOverAction(this ButtonBase button)
        {
            if (button is ButtonBase)
            {
                try
                {
                    //button.IsMouseOver
                    button.BorderBrush = Theme.AccentBrush;
                    button.MouseEnter += ToolButton_MouseEnter;
                    button.MouseLeave += ToolButton_MouseLeave;

                    if (button is ToggleButton) MouseLeave(button);
                }
                catch (Exception ex) { ex.ERROR(); }
            }
        }

        public static void MouseEnter(this ButtonBase button)
        {
            try
            {
                if ((button.Parent is StackPanel) && (button.Parent as StackPanel).Name.Equals("ActionBar") && button.ActualWidth >= 32)
                    button.Foreground = Theme.IdealForegroundBrush;

                if ((button.Parent is Grid) && (button.Parent as Grid).Name.Equals("PopupContainer") && button.ActualWidth >= 24)
                    button.Foreground = Theme.IdealForegroundBrush;

                if (!(button is ToggleButton) || (button is ToggleButton && !(button as ToggleButton).IsChecked.Value))
                    button.Background = Theme.SemiTransparentBrush;
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void MouseLeave(this ButtonBase button)
        {
            try
            {
                if ((button.Parent is StackPanel) && (button.Parent as StackPanel).Name.Equals("ActionBar") && button.ActualWidth >= 32 && button.IsEnabled)
                    button.Foreground = Theme.AccentBrush;

                if ((button.Parent is Grid) && (button.Parent as Grid).Name.Equals("PopupContainer") && button.ActualWidth >= 24)
                    button.Foreground = Theme.AccentBrush;

                if (!(button is ToggleButton) || (button is ToggleButton && !(button as ToggleButton).IsChecked.Value))
                    button.Background = Theme.TransparentBrush;
                else if (button is ToggleButton && (button as ToggleButton).IsChecked.Value)
                {
                    var bg = new SolidColorBrush(Theme.SemiTransparentColor);
                    bg.Opacity = 0.4;
                    button.Background = bg;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void ToolButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is ButtonBase) MouseEnter(sender as ButtonBase);
        }

        public static void ToolButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is ButtonBase) MouseLeave(sender as ButtonBase);
        }
        #endregion

        #region SearchBox common routines
        private static ObservableCollection<string> auto_suggest_list = new ObservableCollection<string>() {};
        public static ObservableCollection<string> AutoSuggestList
        {
            get { return (auto_suggest_list); }
        }

        public static IEnumerable<string> GetSuggestList(this string text)
        {
            List<string> result = new List<string>();

            if (!string.IsNullOrEmpty(text))
            {
                if (Regex.IsMatch(text, @"^\d+$", RegexOptions.IgnoreCase))
                {
                    result.Add($"IllustID: {text}");
                    result.Add($"UserID: {text}");
                }
                result.Add($"User: {text}");
                result.Add($"Fuzzy: {text}");
                result.Add($"Tag: {text}");
                result.Add($"Fuzzy Tag: {text}");
                //result.Add($"Caption: {text}");
            }

            return (result);
        }

        public static void SearchBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox)
            {
                var SearchBox = sender as ComboBox;
                if (SearchBox.Text.Length > 0)
                {
                    auto_suggest_list.Clear();

                    var content = SearchBox.Text.ParseLink().ParseID();
                    if (!string.IsNullOrEmpty(content))
                    {
                        content.GetSuggestList().ToList().ForEach(t => auto_suggest_list.Add(t));
                        SearchBox.Items.Refresh();
                        SearchBox.IsDropDownOpen = true;
                    }

                    e.Handled = true;
                }
            }
        }

        public static void SearchBox_DropDownOpened(object sender, EventArgs e)
        {
            if (sender is ComboBox)
            {
                var SearchBox = sender as ComboBox;

                var textBox = Keyboard.FocusedElement as TextBox;
                if (textBox != null && textBox.Text.Length == 1 && textBox.SelectionLength == 1)
                {
                    textBox.SelectionLength = 0;
                    textBox.SelectionStart = 1;
                }
            }
        }

        public static void SearchBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox)
            {
                var SearchBox = sender as ComboBox;

                e.Handled = true;
                var items = e.AddedItems;
                if (items.Count > 0)
                {
                    var item = items[0];
                    if (item is string)
                    {
                        var query = (string)item;
                        Commands.OpenSearch.Execute(query);
                    }
                }
            }
        }

        public static void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is ComboBox)
            {
                var SearchBox = sender as ComboBox;

                if (e.Key == Key.Return)
                {
                    e.Handled = true;
                    Commands.OpenSearch.Execute(SearchBox.Text);
                }
            }
        }
        #endregion

        #region Window routines
        public static MetroWindow GetMainWindow()
        {
            return (Application.Current.MainWindow as MetroWindow);
        }

        public static MetroWindow GetMainWindow(this MetroWindow win)
        {
            return (Application.Current.MainWindow as MetroWindow);
        }

        public static MainWindow GetMainWindow(this Page page)
        {
            MainWindow result = null;
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    result = Application.Current.MainWindow as MainWindow;
                });
            }
            catch(Exception ex) { ex.ERROR("GETMAINWINDOW"); }
            return (result);
        }

        public static MetroWindow GetActiveWindow()
        {
            MetroWindow window = Application.Current.Windows.OfType<MetroWindow>().SingleOrDefault(x => x.IsActive || x.IsFocused);
            if (window == null) window = Application.Current.MainWindow as MetroWindow;
            return (window);
        }

        public static MetroWindow GetPrevWindow(this MetroWindow window)
        {
            return (window.GetWindow(-1));
        }

        public static MetroWindow GetNextWindow(this MetroWindow window)
        {
            return (window.GetWindow(1));
        }

        public static IList<MetroWindow> GetWindows<T>()
        {
            List<MetroWindow> result = new List<MetroWindow>();
            new Action(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is T && win is MetroWindow) result.Add(win as MetroWindow);
                }
            }).Invoke(async: false);
            return (result);
        }

        public static MetroWindow GetWindow<T>()
        {
            return (GetWindows<T>().FirstOrDefault());
        }

        public static MetroWindow GetWindow(this MetroWindow window, int index = 0, bool relative = true)
        {
            var wins = Application.Current.Windows.OfType<MetroWindow>().Where(w => !w.Title.Equals("DropBox", StringComparison.CurrentCultureIgnoreCase)).ToList();
            var active = window is MetroWindow ? window : wins.SingleOrDefault(x => x.IsActive);
            if (active == null) active = Application.Current.MainWindow as MetroWindow;

            var result = active;
            var current_index = wins.IndexOf(active);

            var next = relative ? current_index + index : index;
            if (next > 0)
            {
                if (next >= wins.Count) next = next % wins.Count;
                result = wins.ElementAtOrDefault(next);
            }
            else if (next < 0)
            {
                if (next < 0) next = wins.Count - (Math.Abs(next) % wins.Count);
                result = wins.ElementAtOrDefault(next);
            }
            else
            {
            }

            return (result);
        }

        public static MetroWindow GetWindowByTitle(this string title)
        {
            return (GetWindowsByTitle(title).FirstOrDefault());
        }

        public static IList<MetroWindow> GetWindowsByTitle(this string title)
        {
            List<MetroWindow> result = new List<MetroWindow>();
            new Action(() =>
            {
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is MetroWindow)
                    {
                        var win_title = (win as MetroWindow).Title;
                        if (win_title.Equals(title, StringComparison.CurrentCultureIgnoreCase))
                        {
                            result.Add(win as MetroWindow);
                        }
                    }
                }
            }).Invoke(async: false);
            return (result);
        }

        public static void AdjustWindowPos(this MetroWindow window)
        {
            if (window is ContentWindow)
            {
                //var dw = System.Windows.SystemParameters.MaximizedPrimaryScreenWidth;
                //var dh = System.Windows.SystemParameters.MaximizedPrimaryScreenHeight;
                //var dw = System.Windows.SystemParameters.WorkArea.Width;
                //var dh = System.Windows.SystemParameters.WorkArea.Height;

                var rect = System.Windows.Forms.Screen.GetWorkingArea(new System.Drawing.Point((int)window.Top, (int)window.Left));
                var dw = rect.Width;
                var dh = rect.Height;

                window.MaxWidth = Math.Min(window.MaxWidth, dw);
                window.MaxHeight = Math.Min(window.MaxHeight, dh);

                if (window.Left + window.Width > dw) window.Left = window.Left + window.Width - dw;
                if (window.Top + window.Height > dh) window.Top = window.Top + window.Height - dh;
            }
        }

        public static void AdjustWindowPos(this Window window)
        {
            if (window is ContentWindow)
            {
                AdjustWindowPos(window as ContentWindow);
            }
        }

        public static void Active(this MetroWindow window)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                try
                {
                    if (window is MainWindow)
                        (window as MainWindow).RestoreWindowState();
                    else if (window is ContentWindow)
                        (window as ContentWindow).RestoreWindowState();
                }
                catch (Exception ex)
                {
                    ex.ERROR();
                    window.WindowState = WindowState.Normal;
                }
            }
            window.Show();
            window.Activate();
        }

        public static async Task<bool> ActiveByTitle(this string title)
        {
            bool result = false;
            await new Action(() =>
            {
                var win  = GetWindowByTitle(title);
                if (win is MetroWindow) { result = true; win.Active(); }
                else if (win is Window) { result = true; win.Activate(); }
            }).InvokeAsync();
            return (result);
        }

        public static async Task<bool> ShowByTitle(this string title)
        {
            bool result = false;
            await new Action(() =>
            {
                var win  = GetWindowByTitle(title);
                if (win is Window) { result = true; win.Show(); }
            }).InvokeAsync();
            return (result);
        }

        public static Window GetActiveWindow(this Page page)
        {
            var window = Window.GetWindow(page);
            if (window == null) window = GetActiveWindow();
            return (window);
        }

        public static T GetActiveWindow<T>(this Page page) where T : Window
        {
            var window = Window.GetWindow(page);
            if (window == null) window = GetActiveWindow();
            if (window is T)
                return (window as T);
            else
                return (default(T));
        }

        public static dynamic GetWindowContent(this MetroWindow window)
        {
            dynamic result = null;
            try
            {
                if (window is MainWindow)
                {
                    if (window.Content is TilesPage)
                        result = window.Content;
                }
                else if (window is ContentWindow)
                {
                    if (window.Content is Page)
                        result = window.Content;
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static bool WindowExists(string title)
        {
            bool result = false;

            //HWND hDlgExists = FindWindow(0, "MyDialogTitle"); // hDlgExists will be NULL if dlg is not exist.

            return (result);
        }
        #endregion

        #region Dialog/MessageBox routines
        public static string ChangeSaveTarget(this string file)
        {
            return (ChangeSaveFolder(file));
        }

        public static string ChangeSaveFolder(string file = "")
        {
            var result = string.Empty;
            setting = Application.Current.LoadSetting();
            if (string.IsNullOrEmpty(file))
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

                Window dm = GetWindowByTitle("Download Manager");
                if (!(dm is ContentWindow)) dm = Application.Current.MainWindow;
                if (dlg.ShowDialog(dm) == CommonFileDialogResult.Ok)
                {
                    setting.LastFolder = dlg.FileName;
                    result = dlg.FileName;
                    // Do something with selected folder string                   
                }
            }
            else
            {
                if (string.IsNullOrEmpty(setting.LastFolder))
                {
                    SaveFileDialog dlgSave = new SaveFileDialog();
                    dlgSave.FileName = file;
                    if (dlgSave.ShowDialog() == true)
                    {
                        file = dlgSave.FileName;
                        setting.LastFolder = Path.GetDirectoryName(file);
                    }
                }
                result = Path.Combine(setting.LastFolder, Path.GetFileName(file));
            }
            return (result);
        }

        private static ConcurrentDictionary<string, string> _MessageDialogList = new ConcurrentDictionary<string, string>();
        public static bool IsMessagePopup(this string title, string content = "")
        {
            var result = _MessageDialogList.ContainsKey(title) && _MessageDialogList[title].Equals(content);
            return (result);
        }

        public static async void ShowMessageBox(this string content, string title, MessageBoxImage image = MessageBoxImage.Information)
        {
            content.LOG(title);

            await Task.Delay(1);
            _MessageDialogList[title] = content;
            MessageBox.Show(content, title, MessageBoxButton.OK, image);
            var value = string.Empty;
            _MessageDialogList.TryRemove(title, out value);
        }

        public static async Task<bool> ShowMessageDialog(this string content, string title, MessageBoxImage image = MessageBoxImage.Information)
        {
            content.LOG(title);

            await Task.Delay(1);
            _MessageDialogList[title] = content;
            var ret = MessageBox.Show(content, title, MessageBoxButton.OKCancel, image);
            var value = string.Empty;
            _MessageDialogList.TryRemove(title, out value);
            return (ret == MessageBoxResult.OK || ret == MessageBoxResult.Yes ? true : false);
        }

        public static async Task ShowMessageBoxAsync(this string content, string title, MessageBoxImage image = MessageBoxImage.Information)
        {
            await ShowMessageDialogAsync(content, title, image);
        }

        public static async Task ShowMessageDialogAsync(this string content, string title, MessageBoxImage image = MessageBoxImage.Information)
        {
            MetroWindow window = GetActiveWindow();
            await window.ShowMessageAsync(content, title);
        }

        public static async void ShowProgressDialog(object sender, RoutedEventArgs e)
        {
            var mySettings = new MetroDialogSettings()
            {
                NegativeButtonText = "Close now",
                AnimateShow = false,
                AnimateHide = false
            };

            MetroWindow window = GetActiveWindow();

            var controller = await window.ShowProgressAsync("Please wait...", "We are baking some cupcakes!", settings: mySettings);
            controller.SetIndeterminate();

            //await Task.Delay(5000);

            controller.SetCancelable(true);

            double i = 0.0;
            while (i < 6.0)
            {
                double val = (i / 100.0) * 20.0;
                controller.SetProgress(val);
                controller.SetMessage("Baking cupcake: " + i + "...");

                if (controller.IsCanceled)
                    break; //canceled progressdialog auto closes.

                i += 1.0;

                //await Task.Delay(2000);
            }

            await controller.CloseAsync();

            if (controller.IsCanceled)
            {

                await window.ShowMessageAsync("No cupcakes!", "You stopped baking!");
            }
            else
            {
                await window.ShowMessageAsync("Cupcakes!", "Your cupcakes are finished! Enjoy!");
            }
        }
        #endregion

        #region Toast routines
        private static string lastToastTitle = string.Empty;
        private static string lastToastContent = string.Empty;
        public async static void ShowDownloadToast(this string content, string title = "Pixiv", string imgsrc = "", string file = "", string state = "", string state_description = "", object tag = null)
        {
            try
            {
                if (title.Equals(lastToastTitle) && content.Equals(lastToastContent)) return;

                content.LOG(title);

                setting = Application.Current.LoadSetting();

                lastToastTitle = title;
                lastToastContent = content;

                INotificationDialogService _dialogService = new NotificationDialogService();
                NotificationConfiguration cfgDefault = NotificationConfiguration.DefaultConfiguration;
                NotificationConfiguration cfg = new NotificationConfiguration(
                    //new TimeSpan(0, 0, 30), 
                    TimeSpan.FromSeconds(setting.ToastShowTimes),
                    cfgDefault.Width+32, cfgDefault.Height,
                    "ToastTemplate",
                    //cfgDefault.TemplateName, 
                    cfgDefault.NotificationFlowDirection);

                var newNotification = new CustomToast()
                {
                    Type = ToastType.DOWNLOAD,
                    Title = title,
                    ImgURL = imgsrc,
                    Message = content,
                    Extra = string.IsNullOrEmpty(file) ? string.Empty : file,
                    State = state,
                    StateDescription = state_description,
                    Tag = tag
                };

                await new Action(() => {
                    _dialogService.ClearNotifications();
                    _dialogService.ShowNotificationWindow(newNotification, cfg);
                }).InvokeAsync(true);
            }
#if DEBUG
            catch (Exception ex) { ex.Message.ShowMessageBox("ERROR[TOAST]"); }
#else
            catch (Exception ex) { ex.ERROR(); }
#endif
        }

        public async static void ShowToast(this string content, string title, string imgsrc, string state = "", string state_description = "")
        {
            try
            {
                content.LOG(title);

                setting = Application.Current.LoadSetting();

                INotificationDialogService _dialogService = new NotificationDialogService();
                NotificationConfiguration cfgDefault = NotificationConfiguration.DefaultConfiguration;
                NotificationConfiguration cfg = new NotificationConfiguration(
                    //new TimeSpan(0, 0, 30), 
                    TimeSpan.FromSeconds(setting.ToastShowTimes),
                    cfgDefault.Width + 32, cfgDefault.Height,
                    "ToastTemplate",
                    //cfgDefault.TemplateName, 
                    cfgDefault.NotificationFlowDirection);

                var newNotification = new CustomToast()
                {
                    Type = ToastType.OK,
                    Title = title,
                    ImgURL = imgsrc,
                    Message = content,
                    State = state,
                    StateDescription = state_description,
                    Tag = null
                };

                await new Action(() => {
                    _dialogService.ClearNotifications();
                    _dialogService.ShowNotificationWindow(newNotification, cfg);
                }).InvokeAsync(true);

            }
            catch (Exception ex) { ex.ERROR("SHOWTOAST"); }
        }

        public async static void ShowToast(this string content, string title, bool messagebox = false)
        {
            try
            {
                if (messagebox) { content.ShowMessageBox(title); return; }

                content.LOG(title);

                setting = Application.Current.LoadSetting();

                INotificationDialogService _dialogService = new NotificationDialogService();
                NotificationConfiguration cfgDefault = NotificationConfiguration.DefaultConfiguration;
                NotificationConfiguration cfg = new NotificationConfiguration(
                    TimeSpan.FromSeconds(setting.ToastShowTimes),
                    cfgDefault.Width + 32, cfgDefault.Height,
                    "ToastTemplate",
                    //cfgDefault.TemplateName, 
                    cfgDefault.NotificationFlowDirection);

                var newNotification = new CustomToast()
                {
                    Title = title,
                    Message = content
                };

                await new Action(() => {
                    _dialogService.ClearNotifications();
                    _dialogService.ShowNotificationWindow(newNotification, cfg);
                }).InvokeAsync(true);
            }
            catch (Exception ex) { ex.ERROR("SHOWTOAST"); }
        }
        #endregion

        #region Drop Window routines
        private static void DropBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (sender is ContentWindow)
                {
                    var window = sender as ContentWindow;
                    window.DragMove();

                    var desktop = SystemParameters.WorkArea;
                    if (window.Left < desktop.Left) window.Left = desktop.Left;
                    if (window.Top < desktop.Top) window.Top = desktop.Top;
                    if (window.Left + window.Width > desktop.Left + desktop.Width) window.Left = desktop.Left + desktop.Width - window.Width;
                    if (window.Top + window.Height > desktop.Top + desktop.Height) window.Top = desktop.Top + desktop.Height - window.Height;
                    setting.DropBoxPosition = new Point(window.Left, window.Top);
                    //setting.Save();
                }
            }
            else if (e.ChangedButton == MouseButton.XButton1)
            {
                if (sender is ContentWindow)
                {
                    //var window = sender as ContentWindow;
                    //window.Hide();
                    e.Handled = true;
                }
            }
        }

        private static void DropBox_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (sender is ContentWindow && e.ClickCount == 1)
                {
                    setting = Application.Current.LoadSetting();

                    var window = sender as ContentWindow;

                    var desktop = SystemParameters.WorkArea;
                    if (window.Left < desktop.Left) window.Left = desktop.Left;
                    if (window.Top < desktop.Top) window.Top = desktop.Top;
                    if (window.Left + window.Width > desktop.Left + desktop.Width) window.Left = desktop.Left + desktop.Width - window.Width;
                    if (window.Top + window.Height > desktop.Top + desktop.Height) window.Top = desktop.Top + desktop.Height - window.Height;
                    setting.DropBoxPosition = new Point(window.Left, window.Top);
                    setting.Save();
                }
            }
        }

        private static void DropBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (sender is ContentWindow)
                {
                    var window = sender as ContentWindow;
                    // In maximum window state case, window will return normal state and continue moving follow cursor
                    if (window.WindowState == WindowState.Maximized)
                    {
                        window.WindowState = WindowState.Normal;
                        // 3 or any where you want to set window location affter return from maximum state
                        //Application.Current.MainWindow.Top = 3;
                    }
                    window.DragMove();
                }
            }
        }

        private static void DropBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 3)
            {
                if (sender is ContentWindow)
                {
                    var window = sender as ContentWindow;
                    window.Hide();
                    window.Close();
                    window = null;
                }
            }
        }

        private static void DropBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ContentWindow)
            {
                var window = sender as ContentWindow;
                window.Hide();
                window.Close();
                window = null;
            }
        }

        public static Window DropBoxExists(this Window window)
        {
            Window result = null;

            var win  = GetWindowByTitle("Dropbox");
            if (win is ContentWindow) result = win as ContentWindow;

            return (result);
        }

        public static void SetDropBoxState(this bool state)
        {
            new Action(() =>
            {
                var win = GetWindowByTitle("Dropbox");
                if (win is ContentWindow)
                    (win as ContentWindow).SetDropBoxState(state);
                else if (win is MainWindow)
                    (win as MainWindow).SetDropBoxState(state);
            }).Invoke(async: false);
        }

        public static bool ShowDropBox(this bool show)
        {
            var win = DropBoxExists(null);
            ContentWindow box = win == null ? null : (ContentWindow)win;

            if (box is ContentWindow)
            {
                box.Hide();
                box.Close();
                box = null;
            }
            else
            {
                box = new ContentWindow();
                box.MouseDown += DropBox_MouseDown;
                box.MouseUp += DropBox_MouseUp;
                ///box.MouseMove += DropBox_MouseMove;
                //box.MouseDoubleClick += DropBox_MouseDoubleClick;
                box.MouseLeftButtonDown += DropBox_MouseLeftButtonDown;
                box.Width = 48;
                box.Height = 48;
                box.MinWidth = 48;
                box.MinHeight = 48;
                box.MaxWidth = 48;
                box.MaxHeight = 48;

                box.Background = Theme.WindowTitleBrush;
                box.OverlayBrush = Theme.WindowTitleBrush;
                //box.OverlayOpacity = 0.8;

                box.Opacity = 0.85;
                box.AllowsTransparency = true;
                //box.SaveWindowPosition = true;
                //box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                box.AllowDrop = true;
                box.Topmost = true;
                box.ResizeMode = ResizeMode.NoResize;
                box.ShowInTaskbar = false;
                box.ShowIconOnTitleBar = false;
                box.ShowCloseButton = false;
                box.ShowMinButton = false;
                box.ShowMaxRestoreButton = false;
                box.ShowSystemMenuOnRightClick = false;
                box.ShowTitleBar = false;
                //box.WindowStyle = WindowStyle.None;
                box.Title = "DropBox";

                box.Content = "Resources/pixiv-icon.ico".MakePackUri().GetThemedImage();
                box.Icon = (box.Content as Image).Source;
                //box.Content = img;

                if (setting.DropBoxPosition != null)
                {
                    double x= setting.DropBoxPosition.X;
                    double y =setting.DropBoxPosition.Y;
                    if (x == 0 && y == 0)
                        box.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    else
                    {
                        box.Left = x;
                        box.Top = y;
                    }
                }

                box.Show();
                box.Activate();
            }

            var result = box is ContentWindow ? box.IsVisible : false;
            SetDropBoxState(result);
            return (result);
        }
        #endregion
    }

    #region Custom Toast 
    public class CustomToast : Notification
    {
        [Description("Get or Set Toast Type")]
        [Category("Common Properties")]
        public ToastType Type { get; set; } = ToastType.OK;

        [Description("Get or Set Extra Contents")]
        [Category("Common Properties")]
        public string Extra { get; set; } = string.Empty;

        [Description("Get or Set State")]
        [Category("Common Properties")]
        public string State { get; set; } = string.Empty;

        [Description("Get or Set State Description")]
        [Category("Common Properties")]
        public string StateDescription { get; set; } = string.Empty;

        //public string ImgURL { get; set; }
        //public string Message { get; set; }
        //public string Title { get; set; }
        public object Tag { get; set; }
    }
    #endregion

    public static class TaskWaitingExtensions
    {
        /// <summary>
        /// Async Task Wait
        /// </summary>
        /// <typeparam name="TResult">result</typeparam>
        /// <param name="task">task instance</param>
        /// <param name="timeout">milliseconds timeout</param>
        /// <returns></returns>
        public static async Task<TResult> WaitAsync<TResult>(this Task<TResult> task, int timeout)
        {
            return (await WaitAsync(task, TimeSpan.FromMilliseconds(timeout)));
        }

        public static async Task<TResult> WaitAsync<TResult>(this Task<TResult> task, TimeSpan timeout)
        {
            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                var delayTask = Task.Delay(timeout, timeoutCancellationTokenSource.Token);
                if (await Task.WhenAny(task, delayTask) == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;
                }
                throw new TimeoutException("The operation has timed out.");
            }
        }
    }

    public static class ExtensionMethods
    {
        #region Time Calc Helper
        public static long MillisecondToTicks(this int millisecond)
        {
            long result = 0;
            try
            {
                result = TimeSpan.TicksPerMillisecond * millisecond;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static long TicksToMillisecond(this long ticks)
        {
            long result = 0;
            try
            {
                result = ticks / TimeSpan.TicksPerMillisecond;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static long TicksToSecond(this long ticks)
        {
            return (ticks / TimeSpan.TicksPerSecond);
        }

        public static long SecondToTicks(this long second)
        {
            return (second * TimeSpan.TicksPerSecond);
        }

        public static long FileTimeToSecond(this long filetime)
        {
            return (TicksToSecond(filetime));
        }

        public static long SecondToFileTime(this long second)
        {
            return (SecondToTicks(second));
        }

        public static long DeltaTicks(this long ticks1, long ticks2, bool abs = true)
        {
            long result = 0;
            try
            {
                result = ticks2 - ticks1;
                if (abs) result = Math.Abs(result);
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static long DeltaMillisecond(this long ticks1, long ticks2, bool abs = true)
        {
            long result = 0;
            try
            {
                result = DeltaTicks(ticks1, ticks2, abs).TicksToMillisecond();
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static long DeltaMillisecond(this DateTime dt1, DateTime dt2, bool abs = true)
        {
            long result = 0;
            try
            {
                result = DeltaMillisecond(dt1.Ticks, dt2.Ticks, abs);
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static double DeltaMilliseconds(this DateTime dt1, DateTime dt2, bool abs = true)
        {
            var delta = (dt2 - dt1).TotalMilliseconds;
            if (abs) delta = Math.Abs(delta);
            return (delta);
        }

        public static double DeltaSeconds(this DateTime dt1, DateTime dt2, bool abs = true)
        {
            var delta = (dt2 - dt1).TotalSeconds;
            if (abs) delta = Math.Abs(delta);
            return (delta);
        }

        public static double DeltaMinutes(this DateTime dt1, DateTime dt2, bool abs = true)
        {
            var delta = (dt2 - dt1).TotalMinutes;
            if (abs) delta = Math.Abs(delta);
            return (delta);
        }

        public static double DeltaHours(this DateTime dt1, DateTime dt2, bool abs = true)
        {
            var delta = (dt2 - dt1).TotalHours;
            if (abs) delta = Math.Abs(delta);
            return (delta);
        }

        public static double DeltaDays(this DateTime dt1, DateTime dt2, bool abs = true)
        {
            var delta = (dt2 - dt1).TotalDays;
            if (abs) delta = Math.Abs(delta);
            return (delta);
        }

        public static TimeSpan Delta(this DateTime dt1, DateTime dt2)
        {
            TimeSpan result = TimeSpan.FromTicks(0);
            try
            {
                result = dt2 - dt1;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static long DeltaNowMillisecond(this long ticks, bool abs = true)
        {
            long result = 0;
            try
            {
                result = DeltaMillisecond(ticks, DateTime.Now.Ticks, abs);
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static long DeltaNowMillisecond(this DateTime dt, bool abs = true)
        {
            long result = 0;
            try
            {
                result = DeltaNowMillisecond(dt.Ticks, abs);
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static bool DeltaNowMillisecond(this long ticks, int millisecond, bool abs = true)
        {
            bool result = true;
            try
            {
                result = DeltaNowMillisecond(ticks, abs) > millisecond;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static bool DeltaNowMillisecond(this DateTime dt, int millisecond, bool abs = true)
        {
            bool result = true;
            try
            {
                result = DeltaNowMillisecond(dt, abs) > millisecond;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }
        #endregion

        #region Media Play
        public static async void Sound(this object obj, string mode = "")
        {
            try
            {
                await new Action(() =>
                {
                    Sound(mode);
                }).InvokeAsync();
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void Sound(string mode)
        {
            try
            {
                if (string.IsNullOrEmpty(mode))
                {
                    SystemSounds.Beep.Play();
                }
                else
                {
                    switch (mode.ToLower())
                    {
                        case "*":
                            SystemSounds.Asterisk.Play();
                            break;
                        case "!":
                            SystemSounds.Exclamation.Play();
                            break;
                        case "?":
                            SystemSounds.Question.Play();
                            break;
                        case "d":
                            SystemSounds.Hand.Play();
                            break;
                        case "h":
                            SystemSounds.Hand.Play();
                            break;
                        case "b":
                            SystemSounds.Beep.Play();
                            break;
                        default:
                            SystemSounds.Beep.Play();
                            break;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }
        #endregion

        #region Misc Helper
        public static void Dispose(this Image image)
        {
            try
            {
                if (image is Image)
                {
                    image.Source = null;
                    image.UpdateLayout();
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void Dispose<T>(this T[] array)
        {
            array.Clear();
            array = null;
        }

        public static void Dispose<T>(this T[] array, ref T[] target)
        {
            target.Clear(ref target);
            target = null;
        }

        public static void Clear<T>(this T[] array)
        {
            try
            {
                if (array is Array)
                {
                    Array.Clear(array, 0, array.Length);
                    Array.Resize<T>(ref array, 0);
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        public static void Clear<T>(this T[] array, ref T[] target)
        {
            try
            {
                if (array is Array)
                {
                    Array.Clear(array, 0, array.Length);
                    Array.Resize<T>(ref array, 0);
                }
            }
            catch (Exception ex) { ex.ERROR(); }
        }
        #endregion

        #region WPF UI Helper
        public static T FindByName<T>(this FrameworkElement element, string name) where T : FrameworkElement
        {
            T result = default(T);
            try
            {
                var ret = element.FindName(name);
                if (ret is T) result = (T)ret;
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }

        public static DependencyObject GetVisualChildFromTreePath(this DependencyObject dpo, int[] path)
        {
            if (path.Length == 0) return dpo;
            if (VisualTreeHelper.GetChildrenCount(dpo) == 0) return (dpo);
            List<int> newPath = new List<int>(path);
            newPath.RemoveAt(0);
            return VisualTreeHelper.GetChild(dpo, path[0]).GetVisualChildFromTreePath(newPath.ToArray());
        }

        public static childItem FindVisualChild<childItem>(DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem)
                    return (childItem)child;
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        public static childItem FindVisualChild<childItem>(this DependencyObject parent, DependencyObject obj) where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is childItem && child == obj)
                    return (childItem)child;
                else
                {
                    childItem childOfChild = child.FindVisualChild<childItem>(obj);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        public static List<T> GetVisualChildren<T>(this DependencyObject obj) where T : DependencyObject
        {
            List<T> childList = new List<T>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    childList.Add(child as T);

                var childOfChilds = child.GetVisualChildren<T>();
                if (childOfChilds != null)
                {
                    childList.AddRange(childOfChilds);
                }
            }

            if (childList.Count > 0)
                return childList;

            return null;
        }

        public static T GetVisualChild<T>(this Visual referenceVisual) where T : Visual
        {
            Visual child = null;
            for (Int32 i = 0; i < VisualTreeHelper.GetChildrenCount(referenceVisual); i++)
            {
                child = VisualTreeHelper.GetChild(referenceVisual, i) as Visual;
                if (child != null && child is T)
                {
                    break;
                }
                else if (child != null)
                {
                    child = GetVisualChild<T>(child);
                    if (child != null && child is T)
                    {
                        break;
                    }
                }
            }
            return child as T;
        }

        public static List<T> GetVisualChildren<T>(this Visual obj) where T : Visual
        {
            List<T> childList = new List<T>();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is T)
                    childList.Add(child as T);

                var childOfChilds = child.GetVisualChildren<T>();
                if (childOfChilds.Count > 0)
                {
                    childList.AddRange(childOfChilds);
                }
            }
            return childList;
        }

        public static List<T> GetChildren<T>(this Visual obj) where T : Visual
        {
            List<T> childList = new List<T>();
            if (obj is Visual)
            {
                var children = obj.GetChildObjects();
                foreach (var child in children)
                {
                    if (child is T) childList.Add(child as T);
                    if (child is Visual)
                    {
                        var childOfChilds = (child as Visual).GetChildren<T>();
                        if (childOfChilds.Count > 0)
                        {
                            childList.AddRange(childOfChilds);
                        }
                    }
                }
            }
            return childList;
        }

        //private static int current_deeper = 0;
        //public static bool IsVisiualChild(this DependencyObject obj, DependencyObject parent, int deeper = 0)
        //{
        //    return (IsVisiualChild(obj, parent, 0, deeper));
        //}

        public static bool IsVisiualChild(this DependencyObject obj, DependencyObject parent, int max_deeper = 0, int current_deeper = 0)
        {
            var result = false;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child != null && child == obj)
                {
                    result = true;
                    break;
                }

                if (current_deeper < max_deeper)
                {
                    current_deeper++;
                    result = obj.IsVisiualChild(child, max_deeper, current_deeper);
                }

                if (result) break;
            }

            current_deeper = 0;
            return (result);
        }
        #endregion

        #region Graphic Helper
        public static Tuple<double, double> AspectRatio(this ImageSource image)
        {
            double bestDelta = double.MaxValue;
            double i = 1;
            int j = 1;
            double bestI = 0;
            int bestJ = 0;

            var ratio = image.Width / image.Height;

            for (int iterations = 0; iterations < 100; iterations++)
            {
                double delta = i / j - ratio;

                // Optionally, quit here if delta is "close enough" to zero
                if (delta < 0) i += 0.1;
                else if (delta == 0)
                {
                    i = 1;
                    j = 1;
                }
                else j++;

                double newDelta = Math.Abs( i / j - ratio);
                if (newDelta < bestDelta)
                {
                    bestDelta = newDelta;
                    bestI = i;
                    bestJ = j;
                }
            }
            return (new Tuple<double, double>(bestI, bestJ));
        }
        #endregion
    }

}
