using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Dfust.Hotkeys;
using Microsoft.Win32;
using MahApps.Metro.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PixivWPF.Pages;

namespace PixivWPF.Common
{
    #region Hotkey
    public class HotKeyConfig
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DisplayDescription { get; set; } = string.Empty;
        [JsonConverter(typeof(StringEnumConverter))]
        public System.Windows.Forms.Keys Keys { get; set; } = default(System.Windows.Forms.Keys);
        //[JsonIgnore]
        [JsonConverter(typeof(ICommandTypeConverter<Prism.Commands.DelegateCommand>))]
        public ICommand Command { get; set; } = default(ICommand);
        public string CommandName { get; set; } = string.Empty;
        public dynamic CommandParams { get; set; } = null;
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
                ex.ERROR("SaveSetting");
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
                if (_watchers == null) _watchers = new ConcurrentDictionary<string, FileSystemWatcher>();

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
                _watchers.AddOrUpdate(folder, watcher, (k, v) => watcher);
                //_watchers[folder] = watcher;
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
                    catch (Exception ex) { ex.ERROR("ReleaseAppWatcher"); }
                }
                _watchers.Clear();
            }
        }
        #endregion

        #region Application Theme Helper
        private static Uri _IconUri = null;
        private static Uri IconUri { get { if (_IconUri == null) _IconUri = "Resources/pixiv-icon.ico".MakePackUri(); return (_IconUri); } }
        private static Image DefaultIcon = null;
        private static Image ThemedIcon = null;
        private static CustomImageSource ThemedIconSource = null;

        public static CustomImageSource GetThemedIcon(this Application app)
        {
            if (DefaultIcon == null) DefaultIcon = new Image() { Source = new BitmapImage(IconUri) };
            if (ThemedIconSource == null) ThemedIconSource = new CustomImageSource() { Source = IconUri.CreateThemedImage() };
            return (ThemedIconSource);
        }

        public static void RefreshThemedIcon(this Application app)
        {
            if (DefaultIcon == null) DefaultIcon = new Image() { Source = new BitmapImage(IconUri) };
            if (ThemedIconSource == null)
                ThemedIconSource = new CustomImageSource() { Source = IconUri.CreateThemedImage() };
            else
                ThemedIconSource.Source = IconUri.CreateThemedImage();
            if (ThemedIcon is Image) ThemedIcon.Dispose();
            ThemedIcon = new Image() { Source = ThemedIconSource.Source };
        }

        public static Image GetIcon(this Application app)
        {
            if (DefaultIcon == null) DefaultIcon = new Image() { Source = new BitmapImage(IconUri) };
            if (ThemedIcon == null) ThemedIcon = new Image() { Source = GetThemedIcon(app).Source };
            return (ThemedIcon);
        }

        public static Image GetDefalutIcon(this Application app)
        {
            if (DefaultIcon == null) DefaultIcon = new Image() { Source = new BitmapImage(IconUri) };
            return (DefaultIcon);
        }

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
            catch (Exception ex) { ex.ERROR("GetAccentIndex"); }
            return (result);
        }

        public static void SetAccent(this Application app, string accent)
        {
            try
            {
                Theme.CurrentAccent = accent;
                app.UpdateTheme();
            }
            catch (Exception ex) { ex.ERROR("SetAccent"); }
        }

        public static void SetStyle(this Application app, string style)
        {
            try
            {
                Theme.CurrentStyle = style;
                app.UpdateTheme();
            }
            catch (Exception ex) { ex.ERROR("SetStyle"); }
        }

        public static void SetTheme(this Application app, string theme)
        {
            try
            {
                Theme.Change(theme);
                app.UpdateTheme();
            }
            catch (Exception ex) { ex.ERROR("SetTheme"); }
        }

        public static void SetTheme(this Application app, string style, string accent)
        {
            try
            {
                Theme.Change(style, accent);
                app.UpdateTheme();
            }
            catch (Exception ex) { ex.ERROR("SetTheme"); }
        }

        public static void ToggleTheme(this Application app)
        {
            try
            {
                Theme.Toggle();
                app.UpdateTheme();
            }
            catch (Exception ex) { ex.ERROR("ToggleTheme"); }
        }

        public static void UpdateTheme(this Application app)
        {
            try
            {
                app.RefreshThemedIcon();
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
            catch (Exception ex) { ex.ERROR("SetThemeSync"); }
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
                    foreach (var hotkey in HotkeyConfig)
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
                        catch (Exception ex) { ex.ERROR("NAMEDPIPE_CMD"); }
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (result);
        }
        #endregion

        #region Application LOG Helper
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetCurrentMethod(this Application app)
        {
            var st = new StackTrace();
            var sf = st.GetFrame(1);

            return sf.GetMethod().Name;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string CurrentMethodName(this Application app)
        {
            return (MethodBase.GetCurrentMethod().Name);
        }

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
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG            
            Debug.WriteLine($"{prefix}{contents}");
#endif            
            new Action(() =>
            {
                logger.Trace($"{prefix}{contents}");
            }).Invoke(async: false);
        }

        public static void DEBUG(this string contents, string tag = "")
        {
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG
            Debug.WriteLine($"{prefix}{contents}");
#endif
            new Action(() =>
            {
                logger.Debug($"{prefix}{contents}");
            }).Invoke(async: false);
        }

        public static void INFO(this string contents, string tag = "")
        {
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG            
            Debug.WriteLine($"{prefix}{contents}");
#endif
            new Action(() =>
            {
                logger.Info($"{prefix}{contents}");
            }).Invoke(async: false);
        }

        public static void WARN(this string contents, string tag = "")
        {
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG            
            Debug.WriteLine($"{prefix}{contents}");
#endif
            new Action(() =>
            {
                logger.Warn($"{prefix}{contents}");
            }).Invoke(async: false);
        }

        public static void ERROR(this string contents, string tag = "")
        {
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG            
            Debug.WriteLine($"{prefix}{contents}");
#endif
            var main = Application.Current.GetMainWindow();
            if (main is MainWindow && main.IsShown())
            {
                new Action(() =>
                {
                    logger.Error($"{prefix}{contents}");
                }).Invoke(async: false);
            }
            else
            {
                new Action(() =>
                {
                    logger.Error($"{prefix}{contents}");
                }).Invoke();
            }
        }

        public static void FATAL(this string contents, string tag = "")
        {
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG            
            Debug.WriteLine($"{prefix}{contents}");
#endif
            new Action(() =>
            {
                logger.Fatal($"{prefix}{contents}");
            }).Invoke(async: false);
        }

        public static void NOTICE(this string contents, string tag = "")
        {
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
#if DEBUG            
            Debug.WriteLine($"{prefix}{contents}");
#endif
            new Action(() =>
            {
                logger.Info($"{prefix}{contents}");
                logger.Debug($"{prefix}{contents}");
                logger.Error($"{prefix}{contents}");
            }).Invoke(async: false);
        }

        public static void LOG(this string contents, string title = "", string tag = "")
        {
            if (logger == null) logger = NLog.LogManager.GetCurrentClassLogger();
            if (title.ToUpper().Contains("INFO")) contents.INFO(tag);
            else if (title.ToUpper().Contains("ERROR")) contents.ERROR(tag);
            else if (title.ToUpper().Contains("WARN")) contents.WARN(tag);
            else if (title.ToUpper().Contains("FATAL")) contents.FATAL(tag);
            else contents.INFO();
        }

        public static void TRACE(this Exception ex, string tag = "")
        {
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
            List<string> lines = new List<string>();
            lines.Add($"{ex.Message}");
            lines.Add($"{ex.StackTrace}");
            lines.Add($"  Inner  => {ex.InnerException}");
            lines.Add($"  Base   => {ex.GetBaseException()}");
            lines.Add($"  Root   => {ex.GetRootException()}");
            lines.Add($"  Method => {ex.TargetSite}");
            lines.Add($"  Source => {ex.Source}");
            lines.Add($"  Data   => {ex.Data}");
            var contents = string.Join(Environment.NewLine, lines);
            contents.TRACE(tag);
        }

        public static void DEBUG(this Exception ex, string tag = "")
        {
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
            List<string> lines = new List<string>();
            lines.Add($"{ex.Message}");
            lines.Add($"{ex.StackTrace}");
            lines.Add($"  Inner => {ex.InnerException}");
            lines.Add($"  Base  => {ex.GetBaseException()}");
            lines.Add($"  Root  => {ex.GetRootException()}");
            lines.Add($"  Data  => {ex.Data}");
            var contents = string.Join(Environment.NewLine, lines);
            contents.DEBUG(tag);
        }

        public static void INFO(this Exception ex, string tag = "")
        {
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
            var contents = $"{ex.Message}";
            contents.INFO(tag);
        }

        public static void WARN(this Exception ex, string tag = "")
        {
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
            var contents = $"{ex.Message}";
            contents.WARN(tag);
        }

        public static void ERROR(this Exception ex, string tag = "")
        {
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
            List<string> lines = new List<string>();
            lines.Add($"{ex.Message}");
            lines.Add($"{ex.StackTrace}");
            var contents = string.Join(Environment.NewLine, lines);
            contents.ERROR(tag);
        }

        public static void FATAL(this Exception ex, string tag = "")
        {
            if (logger == null) StartLog(null);
            var prefix = string.IsNullOrEmpty(tag) ? string.Empty : $"[{tag}]";
            List<string> lines = new List<string>();
            lines.Add($"{ex.Message}");
            lines.Add($"{ex.StackTrace}");
            lines.Add($"  Data => {ex.Data}");
            var contents = string.Join(Environment.NewLine, lines);
            contents.FATAL(tag);
        }

        public static void LOG(this Exception ex, string title = "ERROR", string tag = "")
        {
            if (logger == null) StartLog(null);
            if (title.ToUpper().Contains("INFO")) ex.INFO(tag);
            else if (title.ToUpper().Contains("ERROR")) ex.ERROR(tag);
            else if (title.ToUpper().Contains("WARN")) ex.WARN(tag);
            else if (title.ToUpper().Contains("FATAL")) ex.FATAL(tag);
            else ex.ERROR();
        }

        public static void LOG(this object obj, string contents, string title = "INFO")
        {
            if (logger == null) StartLog(null);
            if (obj != null)
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

        public static string GetLogsFolder(this Application app)
        {
            if (logger == null) StartLog(null);
            var logs = string.Empty;
            try
            {
                foreach (var target in NLog.LogManager.Configuration.ConfiguredNamedTargets)
                {
                    if (target is NLog.Targets.FileTarget)
                    {
                        var fileTarget = target as NLog.Targets.FileTarget;
                        var logEventInfo = new NLog.LogEventInfo() { Level = NLog.LogLevel.Info, TimeStamp = DateTime.Now };
                        string fileName = fileTarget.FileName.Render(logEventInfo);
                        logs = Path.GetDirectoryName(fileName);
                        if (string.IsNullOrEmpty(logs)) logs = Path.GetFullPath(".");
                        break;
                    }
                }
            }
            catch (Exception ex) { ex.ERROR(); }
            return (logs);
        }

        public static IList<string> GetLogs(this Application app)
        {
            if (logger == null) StartLog(null);
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
                            var fileName = Path.GetFullPath(fileTarget.FileName.Render(logEventInfo));
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
            if (logger == null) StartLog(null);
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

        public static void AddToast(this Application app, Window win)
        {
            InitTaskTimer();
            toast_list[win] = Environment.TickCount;
            CloseToastAsync();
        }

        private static async void CloseToastAsync()
        {
            await new Action(() =>
            {
                var setting = Application.Current.LoadSetting();
                var now = Environment.TickCount;
                long value = 0L;
                foreach (var kv in toast_list.ToList())
                {
                    var delta = Math.Abs(TimeSpan.FromMilliseconds(now - kv.Value).TotalSeconds);
                    if (delta >= setting.ToastShowTimes + 5)
                    {
                        try
                        {
                            if (kv.Key is Window) kv.Key.Close();
                            toast_list.TryRemove(kv.Key, out value);
                        }
                        catch (Exception ex) { ex.ERROR("CloseToastAsync"); }
                    }
                    else if (!(kv.Key is Window)) toast_list.TryRemove(kv.Key, out value);
                }
            }).InvokeAsync();
        }

        private static void InitTaskTimer()
        {
            try
            {
                if (autoTaskTimer == null)
                {
                    var setting = LoadSetting(Application.Current);
                    autoTaskTimer = new System.Timers.Timer(TimeSpan.FromSeconds(setting.ToastShowTimes).TotalMilliseconds) { AutoReset = true, Enabled = false };
                    autoTaskTimer.Elapsed += Timer_Elapsed;
                    autoTaskTimer.Enabled = true;
                }
            }
            catch (Exception ex) { ex.ERROR("InitTaskTimer"); }
        }

        private static void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (toast_list is ConcurrentDictionary<Window, long> && toast_list.Count > 0) CloseToastAsync();
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
            #region Application
            new HotKeyConfig() { Name = "RestartApplication", Command = Commands.RestartApplication,
                                 Keys = System.Windows.Forms.Keys.None },
            new HotKeyConfig() { Name = "UpgradeApplication", Command = Commands.UpgradeApplication,
                                 Keys = System.Windows.Forms.Keys.None },
            #endregion
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
            new HotKeyConfig() { Name = "OpenFileProperty", Command = Commands.OpenFileProperties,
                                 Keys = System.Windows.Forms.Keys.I | System.Windows.Forms.Keys.Control },
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
                        $"Description: {e.Description}, Keys: \"{ApplicationCulture.TextInfo.ToTitleCase(key_name)}\"".DEBUG("HotkeyPressed");
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

        public static void BindingHotkeys(this Application app, bool global = false)
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
                        $"Command \"{cmd_name}\" binding to hotkey \"{key}\"...".INFO();
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
        public static void ReleaseHotkeys(this Application app)
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
                    ReleaseHotkeys(app);
                    BindingHotkeys(app, global);
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

        #region Application Disk Caching
        public static ConcurrentDictionary<string, bool> PrefetchedList { get; private set; } = new ConcurrentDictionary<string, bool>();

        public static ConcurrentDictionary<string, bool> SystemPrefetchedList(this Application app)
        {
            if (!(PrefetchedList is ConcurrentDictionary<string, bool>)) PrefetchedList = new ConcurrentDictionary<string, bool>();
            return (PrefetchedList);
        }

        public static bool MergeToSystemPrefetchedList(this Application app, ConcurrentDictionary<string, bool> cache)
        {
            var result = false;
            try
            {
                lock (PrefetchedList)
                {
                    //PrefetchedList = new ConcurrentDictionary<string, bool>(PrefetchedList.Union(cache.Where(kv => !PrefetchedList.ContainsKey(kv.Key))));
                    foreach (var kv in cache)
                    {
                        try { PrefetchedList.TryAdd(kv.Key, kv.Value); } catch (Exception ex) { ex.ERROR("MergeToSystemPrefetchedList"); };
                    }
                    result = true;
                }
            }
            catch (Exception ex) { ex.ERROR("MergeToSystemPrefetchedList"); }
            return (result);
        }

        public static ConcurrentDictionary<string, bool> MergeFromSystemPrefetchedList(this Application app, ConcurrentDictionary<string, bool> cache)
        {
            ConcurrentDictionary<string, bool> result = new ConcurrentDictionary<string, bool>();
            try
            {
                result = new ConcurrentDictionary<string, bool>(PrefetchedList.Union(cache.Where(kv => !PrefetchedList.ContainsKey(kv.Key))));
            }
            catch (Exception ex) { ex.ERROR("MergeFromSystemPrefetchedList"); }
            return (result);
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
        private static ConcurrentDictionary<string, HttpClient> HttpClientList = new ConcurrentDictionary<string, HttpClient>();

        private static HttpClient CreateHttpClient(this Application app, bool continuation = false, long range_start = 0, long range_count = 0)
        {
            var setting = LoadSetting(app);
            var buffersize = 100 * 1024 * 1024;
            HttpClient httpClient = null;

            ///
            /// if httpclient throw exception of "send request error", maybe need add code like below line 
            ///
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls | SecurityProtocolType.Ssl3;
            try
            {
                HttpClientHandler handler = new HttpClientHandler()
                {
                    AllowAutoRedirect = true,
                    AutomaticDecompression = DecompressionMethods.Deflate,
                    UseCookies = true,
                    MaxAutomaticRedirections = 15,
                    //MaxConnectionsPerServer = 30,
                    MaxRequestContentBufferSize = buffersize,
                    //SslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12,
                    Proxy = string.IsNullOrEmpty(setting.Proxy) ? null : new WebProxy(setting.Proxy, true, setting.ProxyBypass.ToArray()),
                    UseProxy = string.IsNullOrEmpty(setting.Proxy) || !setting.DownloadUsingProxy ? false : true
                };

                //Maybe HttpClientFactory.Create() 
                httpClient = new HttpClient(handler, true)
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

                var start = !continuation || range_start <= 0 ? "0" : $"{range_start}";
                var end = range_count > 0 ? $"{range_count}" : string.Empty;
                httpClient.DefaultRequestHeaders.Add("Range", $"bytes={start}-{end}");
            }
            catch (Exception ex) { ex.ERROR("CreateHttpClient"); }
            return (httpClient);
        }

        public static void ReleaseHttpClient(this Application app)
        {
            if (HttpClientList is ConcurrentDictionary<string, HttpClient>)
            {
                foreach (var client in HttpClientList.Keys.ToList())
                {
                    try
                    {
                        HttpClient httpClient = null;
                        if (HttpClientList.TryRemove(client, out httpClient))
                        {
                            if (httpClient is HttpClient)
                            {
                                httpClient.CancelPendingRequests();
                                httpClient.Dispose();
                                httpClient = null;
                            }
                        }
                    }
                    catch (Exception ex) { ex.ERROR($"ReleaseHttpClient_{client}"); }
                }
            }
        }

        public static HttpClient GetHttpClient(this Application app, bool continuation = false, long range_start = 0, long range_count = 0, bool is_download = false)
        {
            var setting = LoadSetting(app);
            HttpClient httpClient = null;
            if ((setting.UsingProxy && !is_download) || (setting.DownloadUsingProxy && is_download))
            {
                if (!HttpClientList.TryGetValue(setting.Proxy, out httpClient) || !(httpClient is HttpClient))
                {
                    httpClient = CreateHttpClient(app, continuation, range_start, range_count);
                    HttpClientList.AddOrUpdate(setting.Proxy, httpClient, (k, v) => httpClient);
                }
            }
            else
            {
                if (!HttpClientList.TryGetValue("noproxy", out httpClient) || !(httpClient is HttpClient))
                {
                    httpClient = CreateHttpClient(app, continuation, range_start, range_count);
                    HttpClientList.AddOrUpdate("noproxy", httpClient, (k, v) => httpClient);
                }
            }
            return (httpClient);
        }

        public static WebRequest GetWebRequest(this Application app, bool continuation = false, long range_start = 0, long range_count = 0)
        {
            var setting = LoadSetting(app);

            var webRequest = WebRequest.Create(string.Empty);
            webRequest.Proxy = string.IsNullOrEmpty(setting.Proxy) ? null : new WebProxy(setting.Proxy, true, setting.ProxyBypass.ToArray());

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
                try
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
                catch (Exception ex) { ex.ERROR("Invoke"); }
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
            catch (Exception ex) { ex.ERROR("InvokeAsync"); }
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
            catch (Exception ex) { ex.ERROR("InvokeAsync"); }
        }

        public static async Task InvokeAsync(this Action action, DispatcherPriority priority)
        {
            try
            {
                Dispatcher dispatcher = action.AppDispatcher();
                await dispatcher.InvokeAsync(action, priority);
            }
            catch (Exception ex) { ex.ERROR("InvokeAsync"); }
        }

        public static async Task InvokeAsync(this Action action, DispatcherPriority priority, CancellationToken cancelToken)
        {
            try
            {
                Dispatcher dispatcher = action.AppDispatcher();
                await dispatcher.InvokeAsync(action, priority, cancelToken);
            }
            catch (Exception ex) { ex.ERROR("InvokeAsync"); }
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
                                    sw.Flush();
                                    sw.Close();
                                    sw.Dispose();
                                }
                                encrypt = Convert.ToBase64String(ms.ToArray());
                                cs.Close();
                                cs.Dispose();
                            }
                            ms.Close();
                            ms.Dispose();
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
                                cs.Close();
                                cs.Dispose();
                            }
                            encrypt = Convert.ToBase64String(ms.ToArray());
                            ms.Close();
                            ms.Dispose();
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
                                    sr.Close();
                                    sr.Dispose();
                                }
                            }
                            ms.Close();
                            ms.Dispose();
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
                                cs.Close();
                                cs.Dispose();
                            }
                            uni_text = Encoding.UTF8.GetString(ms.ToArray());
                            ms.Close();
                            ms.Dispose();
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

        public static bool IsModified(this System.Windows.Input.KeyEventArgs evt, IEnumerable<ModifierKeys> modifiers, bool all = false)
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

}
