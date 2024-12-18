﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using ImageMagick;
using ImageMagick.Drawing;
using Mono.Options;
using Xceed.Wpf.Toolkit;

namespace ImageViewer
{
#pragma warning disable IDE0039
#pragma warning disable IDE0044
#pragma warning disable IDE0060

    public static class Extensions
    {
        #region Locale Resource Helper
        private static CultureInfo resourceCulture = Properties.Resources.Culture ?? CultureInfo.CurrentCulture;
        private static readonly System.Resources.ResourceManager resourceMan = Properties.Resources.ResourceManager;
        private static System.Resources.ResourceSet resourceSet = resourceMan.GetResourceSet(resourceCulture, true, true);
        private static Dictionary<FrameworkElement, bool> _be_locale_ = null;

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        public static bool IsRecursiveCall(string method_name)
        {
            // get call stack
            StackTrace stackTrace = new StackTrace();
            var last_method_name = stackTrace.GetFrame(2).GetMethod().Name;
            // get calling method name
#if DEBUG
            Debug.WriteLine(last_method_name);
#endif
            return (last_method_name.Equals(method_name));
        }

        public static string _(this string text)
        {
            return (GetString(text));
        }

        public static string _(this string text, CultureInfo culture)
        {
            return (GetString(text, culture));
        }

        public static string T(this string text)
        {
            return (GetString(text));
        }

        public static string T(this string text, CultureInfo culture)
        {
            return (GetString(text, culture));
        }

        public static string GetString(this string text)
        {
            var t = resourceSet.GetString(text);
            //return (string.IsNullOrEmpty(t) ? null : t.Replace("\\n", Environment.NewLine));
            return (string.IsNullOrEmpty(t) ? null : Regex.Replace(t, @"(\\n\\r|\\r\\n|\n\r|\r\n|\\n|\\r|\n|\r)", Environment.NewLine));
        }

        public static string GetString(this string text, CultureInfo culture)
        {
            ChangeLocale(culture);
            return (GetString(text));
        }

        public static bool Contains(this string text)
        {
            return (resourceSet.GetString(text) != null);
        }

        public static void Locale(this FrameworkElement element, IEnumerable<string> ignore_uids = null, IEnumerable<FrameworkElement> ignore_elements = null)
        {
            try
            {
                if (_be_locale_ == null) _be_locale_ = new Dictionary<FrameworkElement, bool>();
                if (_be_locale_.ContainsKey(element)) return;

                var element_name = element.Name ?? string.Empty;
                var elemet_uid = element.Uid ?? string.Empty;

                bool trans_uid = !(ignore_uids is IEnumerable<string>) || ignore_uids.Where(uid => uid.Equals(elemet_uid) || uid.Equals(element_name)).Count() <= 0;
                bool trans_element = !(ignore_elements is IEnumerable<FrameworkElement>) || ignore_elements.Where(e => e == element).Count() <= 0;

                if (!string.IsNullOrEmpty(elemet_uid))
                {
#if DEBUG
                    Debug.WriteLine($"==> UID: {element.Uid}");
#endif

                    if (trans_uid && trans_element)
                    {
                        if (element is ButtonBase)
                        {
                            var ui = element as ButtonBase;
                            if (ui.Content is string && !string.IsNullOrEmpty(ui.Content as string))
                            {
                                var text = $"{ui.Uid}.Content".T();
                                if (!string.IsNullOrEmpty(text)) ui.Content = text;
                            }
                        }
                        else if (element is TextBlock)
                        {
                            var ui = element as TextBlock;
                            if (!string.IsNullOrEmpty(ui.Text))
                            {
                                var text = $"{ui.Uid}.Text".T();
                                if (!string.IsNullOrEmpty(text)) ui.Text = text;
                            }
                        }
                        else if (element is MenuItem)
                        {
                            var ui = element as MenuItem;
                            if (ui.Header is string && !string.IsNullOrEmpty(ui.Header as string))
                            {
                                var text = $"{ui.Uid}.Header".T();
                                if (!string.IsNullOrEmpty(text)) ui.Header = text;
                            }
                            if (ui.Items.Count > 1)
                                foreach (var i in ui.Items) if (i is FrameworkElement) (i as FrameworkElement).Locale();
                        }
                        else if (element is MenuBase)
                        {
                            var ui = element as MenuBase;
                            foreach (var i in ui.Items) if (i is FrameworkElement) (i as FrameworkElement).Locale();
                        }
                        else if (element is ItemsControl)
                        {
                            var ui = element as ItemsControl;
                            foreach (var i in ui.Items) if (i is FrameworkElement) (i as FrameworkElement).Locale();
                        }
                        else if (element is ColorPicker)
                        {
                            var ui = element as ColorPicker;
                            if (!string.IsNullOrEmpty(ui.AdvancedTabHeader))
                            {
                                var text = $"{ui.Uid}.AdvancedTabHeader".T();
                                if (!string.IsNullOrEmpty(text)) ui.AdvancedTabHeader = text;
                            }
                            if (!string.IsNullOrEmpty(ui.AdvancedTabHeader))
                            {
                                var text = $"{ui.Uid}.StandardTabHeader".T();
                                if (!string.IsNullOrEmpty(text)) ui.StandardTabHeader = text;
                            }
                            if (!string.IsNullOrEmpty(ui.AdvancedTabHeader))
                            {
                                var text = $"{ui.Uid}.AvailableColorsHeader".T();
                                if (!string.IsNullOrEmpty(text)) ui.AvailableColorsHeader = text;
                            }
                            if (!string.IsNullOrEmpty(ui.AdvancedTabHeader))
                            {
                                var text = $"{ui.Uid}.StandardColorsHeader".T();
                                if (!string.IsNullOrEmpty(text)) ui.StandardColorsHeader = text;
                            }
                            if (!string.IsNullOrEmpty(ui.AdvancedTabHeader))
                            {
                                var text = $"{ui.Uid}.RecentColorsHeader".T();
                                if (!string.IsNullOrEmpty(text)) ui.RecentColorsHeader = text;
                            }
                        }
                        else if (element is BusyIndicator)
                        {
                            var ui = element as BusyIndicator;
                            if (ui.BusyContent is string && !string.IsNullOrEmpty(ui.BusyContent as string))
                            {
                                var text = $"{ui.Uid}.BusyContent".T();
                                if (!string.IsNullOrEmpty(text)) ui.BusyContent = text;
                            }
                        }
                    }
                }

                var child_count = VisualTreeHelper.GetChildrenCount(element);
                if (child_count > 0)
                {
                    for (int i = 0; i < child_count; i++)
                    {
                        var child = VisualTreeHelper.GetChild(element, i);
                        if (child is FrameworkElement) (child as FrameworkElement).Locale();
                    }
                }
                else
                {
                    var childs = LogicalTreeHelper.GetChildren(element);
                    foreach (var child in childs)
                    {
                        if (child is FrameworkElement) (child as FrameworkElement).Locale();
                    }
                }

                if (element is FrameworkElement)
                {
                    var ui = element as FrameworkElement;
                    if (trans_uid && trans_element && !string.IsNullOrEmpty(ui.Uid))
                    {
                        var tip = $"{ui.Uid}.ToolTip".T();
                        if (!string.IsNullOrEmpty(tip))
                        {
                            if (ui.ToolTip is string) ui.ToolTip = tip;
                            else if (ui.ToolTip is ToolTip && (ui.ToolTip as ToolTip).Content is string) (ui.ToolTip as ToolTip).Content = tip;
                        }
                    }
                    if (!_be_locale_.ContainsKey(element)) _be_locale_.Add(element, true);
                    if (ui.ContextMenu is ContextMenu) Locale(ui.ContextMenu);
                }
            }
            catch (Exception ex) { $"Locale : {element.Uid ?? element.ToString()} : {ex.Message}".ShowMessage(); }
        }

        public static void Locale(this FrameworkElement element, CultureInfo culture, IEnumerable<string> ignore_uids = null, IEnumerable<FrameworkElement> ignore_elements = null)
        {
            try
            {
                ChangeLocale(culture);
                Locale(element, ignore_uids: ignore_uids, ignore_elements: ignore_elements);
            }
            catch (Exception ex) { $"Locale : {ex.Message}".ShowMessage(); }
        }

        public static void Locale(this IEnumerable<FrameworkElement> elements)
        {
            foreach (var element in elements)
            {
                Locale(element);
            }
        }

        public static void Locale(this IEnumerable<FrameworkElement> elements, CultureInfo culture)
        {
            try
            {
                ChangeLocale(culture);
                Locale(elements);
            }
            catch (Exception ex) { $"Locale : {ex.Message}".ShowMessage(); }
        }

        public static void ChangeLocale(this CultureInfo culture)
        {
            if (culture is CultureInfo && resourceCulture != culture)
            {
                resourceSet = resourceMan.GetResourceSet(culture, true, true);
                //Properties.Resources.Culture = culture;
                resourceCulture = culture;
                if (_be_locale_ == null) _be_locale_ = new Dictionary<FrameworkElement, bool>();
                else _be_locale_.Clear();
            }
        }
        #endregion

        #region Application Helper
        public class MyOptions
        {
            public bool Singleton { get; set; } = false;
            public bool DualOpen { get; set; } = false;
            public bool RunAs32Bits { get; set; } = false;
            public bool RunAs64Bits { get; set; } = false;
            public List<string> Args = new List<string>();
        }

        private static bool singleton = false;
        private static bool dualopen = false;
        private static bool runas64 = false;
        private static bool runas32 = false;

        private static MyOptions myoptions = null;

        public static OptionSet Options { get; set; } = new OptionSet()
        {
            { "s|single", "Single Instance Mode", v => { singleton = v != null; } },
            //{ "d|dual", "Dual Open One Image Mode", v => { dualopen = v != null; } },
            //{ "32", "Run As 64Bits Application", v => { runas32 = v != null; } },
            //{ "64", "Run As 64Bits Application", v => { runas64 = v != null; } },
        };

        public static MyOptions GetCmdLineOpts(this Application app)
        {
            if (myoptions == null)
            {
                var args = Environment.GetCommandLineArgs().Skip(1).ToList();
                var opts = Options.Parse(args);
                if (dualopen && opts.Count > 0) opts.Insert(0, opts.FirstOrDefault());
                myoptions = new MyOptions() { DualOpen = dualopen, Singleton = singleton, RunAs32Bits = runas32, RunAs64Bits = runas64, Args = opts };
            }
            return (myoptions ?? new MyOptions());
        }

        public static MyOptions GetCmdLineOpts(this Window app)
        {
            return (GetCmdLineOpts(Application.Current));
        }

        public static MyOptions GetCmdLineOpts(this MainWindow app)
        {
            return (GetCmdLineOpts(Application.Current));
        }

        public static bool IsRunAs32Bits(this Application app)
        {
            return (System.Environment.Is64BitProcess || IntPtr.Size == 4);
        }

        public static bool IsRunAs32Bits(this Window app)
        {
            return (IsRunAs32Bits(Application.Current));
        }

        public static bool IsRunAs32Bits(this MainWindow app)
        {
            return (IsRunAs32Bits(Application.Current));
        }

        private static Point GetDpi()
        {
            var result = new Point(96, 96);
            IntPtr desktopWnd = IntPtr.Zero;
            IntPtr dc = GetDC(desktopWnd);
            const int LOGPIXELSX = 88;
            const int LOGPIXELSY = 90;
            try
            {
                result.X = GetDeviceCaps(dc, LOGPIXELSX);
                result.Y = GetDeviceCaps(dc, LOGPIXELSY);
            }
            finally
            {
                ReleaseDC(desktopWnd, dc);
            }
            return (result);
        }

        private static int GetColorDepth()
        {
            var result = 0;
            IntPtr desktopWnd = IntPtr.Zero;
            IntPtr dc = GetDC(desktopWnd);
            const int BITSPIXEL = 12;
            try
            {
                result = GetDeviceCaps(dc, BITSPIXEL);
            }
            finally
            {
                ReleaseDC(desktopWnd, dc);
            }
            return (result);
        }

        public static Point GetSystemDPI(this Application app)
        {
            var result = new Point(96, 96);
            try
            {
                //result = GetDpi();
                System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                var dpiXProperty = typeof(SystemParameters).GetProperty("DpiX", flags);
                //var dpiYProperty = typeof(SystemParameters).GetProperty("DpiY", flags);
                var dpiYProperty = typeof(SystemParameters).GetProperty("Dpi", flags);
                if (dpiXProperty != null) { result.X = (int)dpiXProperty.GetValue(null, null); }
                if (dpiYProperty != null) { result.Y = (int)dpiYProperty.GetValue(null, null); }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

        public static int GetSystemColorDepth(this Application app)
        {
            var result = 0;
            try
            {
                result = GetColorDepth();
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

        public static void ShowMessage(this string text, string prefix = "")
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    (Application.Current.MainWindow as MainWindow)?.UpdateIndaicatorState(ImageType.All, state: false, busy: true);

                    if (Application.Current.MainWindow.IsVisible)
                    {
                        if (string.IsNullOrEmpty(prefix))
                            Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, text);
                        else
                            Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, text, prefix);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(prefix))
                            Xceed.Wpf.Toolkit.MessageBox.Show(text);
                        else
                            Xceed.Wpf.Toolkit.MessageBox.Show(text, prefix);
                    }
                }
                catch (Exception ex)
                {
                    if (Application.Current.MainWindow.IsVisible)
                    {
                        Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, ex.Message);
                    }
                    else
                    {
                        Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message);
                    }
                }
            });
        }

        public static void ShowMessage(this Exception exception, string prefix = "")
        {
            if (exception == null) return;
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    (Application.Current.MainWindow as MainWindow)?.UpdateIndaicatorState(ImageType.All, state: false, busy: true);

                    var contents = $"{exception.Message}{Environment.NewLine}{exception.StackTrace}";
                    if (Application.Current.MainWindow.IsVisible)
                    {
                        if (string.IsNullOrEmpty(prefix))
                            Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, contents);
                        else
                            Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, contents, prefix);
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(prefix))
                            Xceed.Wpf.Toolkit.MessageBox.Show(contents);
                        else
                            Xceed.Wpf.Toolkit.MessageBox.Show(prefix);
                    }
                }
                catch (Exception ex)
                {
                    if (Application.Current.MainWindow.IsVisible)
                    {
                        Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, ex.Message);
                    }
                    else
                    {
                        Xceed.Wpf.Toolkit.MessageBox.Show(ex.Message);
                    }
                }
            });
        }

        private static object ExitFrame(object state)
        {
            ((DispatcherFrame)state).Continue = false;
            return null;
        }

        private static readonly SemaphoreSlim CanDoEvents = new SemaphoreSlim(1, 1);
        public static async void DoEvents()
        {
            if (await CanDoEvents?.WaitAsync(0))
            {
                try
                {
                    if (Application.Current?.Dispatcher?.CheckAccess() ?? false)
                    {
                        //await Dispatcher.Yield(DispatcherPriority.Render);
                        await Dispatcher.Yield(DispatcherPriority.Normal);
                        //await System.Windows.Threading.Dispatcher.Yield();

                        //DispatcherFrame frame = new DispatcherFrame();
                        //await Application.Current?.Dispatcher?.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(ExitFrame), frame);
                        //Dispatcher.PushFrame(frame);
                    }
                }
                catch (Exception)
                {
                    try
                    {
                        if (Application.Current?.Dispatcher?.CheckAccess() ?? false)
                        {
                            DispatcherFrame frame = new DispatcherFrame();
                            //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate { }));
                            //Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate { }));

                            //await Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrame), frame);
                            //await Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new DispatcherOperationCallback(ExitFrame), frame);
                            await Application.Current?.Dispatcher?.BeginInvoke(DispatcherPriority.Send, new DispatcherOperationCallback(ExitFrame), frame);
                            Dispatcher.PushFrame(frame);
                        }
                    }
                    catch (Exception)
                    {
                        //await Task.Delay(1);
                    }
                }
                finally
                {
                    //CanDoEvents.Release(max: 1);
                    if (CanDoEvents?.CurrentCount <= 0) CanDoEvents?.Release();
                }
            }
        }

        public static void DoEvents(this FrameworkElement element)
        {
            try
            {
                DoEvents();
            }
            catch { }
        }

        public static async void InvokeAsync(this FrameworkElement element, Action action, bool realtime = false)
        {
            try
            {
                element?.DoEvents();
                //await Task.Delay(1);
                await element?.Dispatcher?.InvokeAsync(action, realtime ? DispatcherPriority.Normal : DispatcherPriority.Background);
            }
            catch { }
        }

        public static IList<string> GetFiles(this string file)
        {
            var files = new List<string>();
            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    var dir = Path.GetDirectoryName(Path.IsPathRooted(file) ? file : Path.Combine(Directory.GetCurrentDirectory(), file));
                    if (Directory.Exists(dir))
                    {
                        files.AddRange(Directory.EnumerateFiles(dir, "*.*").Where(f => SupportedExt(f)).NaturalSort());
                    }
                }
                catch (Exception ex) { ex.ShowMessage(); }
            }
            return (files.Distinct().ToList());
        }
        #endregion

        #region Text Format Helper
        public static double VALUE_GB = 1024 * 1024 * 1024;
        public static double VALUE_MB = 1024 * 1024;
        public static double VALUE_KB = 1024;

        public static bool IsByteString(this string text)
        {
            var result = !string.IsNullOrEmpty(text) && Regex.IsMatch(text, @"(0x\d\d,?)+", RegexOptions.IgnoreCase);
            return (result);
        }

        public static string SmartFileSize(this long v, double factor = 1, bool unit = true, int padleft = 0) { return (SmartFileSize((double)v, factor, unit, padleft: padleft)); }

        public static string SmartFileSize(this double v, double factor = 1, bool unit = true, bool trimzero = true, int padleft = 0)
        {
            string v_str;
            string u_str;
            if (double.IsNaN(v) || double.IsInfinity(v) || double.IsNegativeInfinity(v) || double.IsPositiveInfinity(v)) { v_str = "0"; u_str = "B"; }
            else if (v >= VALUE_GB) { v_str = $"{v / factor / VALUE_GB:F2}"; u_str = "GB"; }
            else if (v >= VALUE_MB) { v_str = $"{v / factor / VALUE_MB:F2}"; u_str = "MB"; }
            else if (v >= VALUE_KB) { v_str = $"{v / factor / VALUE_KB:F2}"; u_str = "KB"; }
            else { v_str = $"{v / factor:F0}"; u_str = "B"; }
            var vs = trimzero && !u_str.Equals("B") ? v_str.Trim('0').TrimEnd('.') : v_str;
            return ((unit ? $"{vs} {u_str}" : vs).PadLeft(padleft));
        }

        public static string DecodeHexUnicode(this string text)
        {
            var result = text;
            foreach (Match m in Regex.Matches(text, @"((\d{1,3}, ?){2,}\d{1,3})"))
            {
                List<byte> bytes = new List<byte>();
                var values = m.Groups[1].Value.Split(',').Select(s => s.Trim()).ToList();
                foreach (var value in values)
                {
                    if (int.Parse(value) > 255) continue;
                    bytes.Add(byte.Parse(value));
                }
                if (bytes.Count > 0) result = result.Replace(m.Groups[1].Value, Encoding.Unicode.GetString(bytes.ToArray()).TrimEnd('\0'));
            }
            return (result);
        }

        public static string DecodeWinXP(this string text)
        {
            var result = text;

            return (result);
        }

        public static byte[] ByteStringToBytes(this string text, bool msb = false, int offset = 0)
        {
            byte[] result = null;
            if (!string.IsNullOrEmpty(text))
            {
                List<byte> bytes = new List<byte>();
                foreach (Match m in Regex.Matches($"{text.TrimEnd().TrimEnd(',')},", @"(0x[0-9,a-f]{1,2}|\d{1,4}),"))
                {
                    var value = m.Groups[1].Value;//.Trim().TrimEnd(',');
                    if (string.IsNullOrEmpty(value)) continue;
                    if (value.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var v = value.Substring(2);
                        if (v.Length <= 0 || v.Length > 2) continue;
                        bytes.Add(byte.Parse(v, NumberStyles.HexNumber));
                    }
                    else
                    {
                        if (int.Parse(value) > 255)
                        {
                            if (msb && BitConverter.IsLittleEndian)
                                bytes.AddRange(BitConverter.GetBytes(int.Parse(value)).Reverse().SkipWhile(b => b == 0));
                            else if (!msb && BitConverter.IsLittleEndian)
                                bytes.AddRange(BitConverter.GetBytes(int.Parse(value)).Reverse().SkipWhile(b => b == 0).Reverse());
                            else if (msb && !BitConverter.IsLittleEndian)
                                bytes.AddRange(BitConverter.GetBytes(int.Parse(value)).SkipWhile(b => b == 0));
                            else if (!msb && !BitConverter.IsLittleEndian)
                                bytes.AddRange(BitConverter.GetBytes(int.Parse(value)).SkipWhile(b => b == 0).Reverse());
                        }
                        else bytes.Add(byte.Parse(value));
                    }

                }
                result = bytes.Count > offset ? bytes.Skip(offset).ToArray() : bytes.ToArray();
            }
            return (result);
        }

        public static string BytesToUnicode(this string text, bool msb = false, int offset = 0)
        {
            var result = text;
            if (!string.IsNullOrEmpty(text))
            {
                var bytes = ByteStringToBytes(text, msb);
                if (bytes.Length > offset) result = msb ? Encoding.BigEndianUnicode.GetString(bytes.Skip(offset).ToArray()) : Encoding.Unicode.GetString(bytes.Skip(offset).ToArray());
            }
            return (result);
        }

        public static string BytesToString(this byte[] bytes, bool ascii = false, bool msb = false, Encoding encoding = null)
        {
            var result = string.Empty;
            if (bytes is byte[] && bytes.Length > 0)
            {
                if (ascii) result = Encoding.ASCII.GetString(bytes);
                else
                {
                    if (bytes.Length > 8)
                    {
                        var idcode_bytes = bytes.Take(8).ToArray();
                        var idcode_name = Encoding.ASCII.GetString(idcode_bytes).TrimEnd().TrimEnd('\0').TrimEnd();
                        if ("UNICODE".Equals(idcode_name, StringComparison.CurrentCultureIgnoreCase))
                        {
                            if (msb)
                                result = Encoding.BigEndianUnicode.GetString(bytes.Skip(8).ToArray());
                            else
                                result = Encoding.Unicode.GetString(bytes.Skip(8).ToArray());
                        }
                        else if ("Ascii".Equals(idcode_name, StringComparison.CurrentCultureIgnoreCase))
                        {
                            result = Encoding.ASCII.GetString(bytes.Skip(8).ToArray());
                        }
                        else if ("Default".Equals(idcode_name, StringComparison.CurrentCultureIgnoreCase))
                        {
                            result = Encoding.Default.GetString(bytes.Skip(8).ToArray());
                        }
                        else if (idcode_bytes.Where(b => b == 0).Count() == 8)
                        {
                            result = Encoding.Default.GetString(bytes.Skip(8).ToArray());
                        }
                        else
                        {
                            if (msb)
                                result = Encoding.BigEndianUnicode.GetString(bytes);
                            else
                                result = Encoding.Unicode.GetString(bytes);
                        }
                    }

                    if (string.IsNullOrEmpty(result))
                    {
                        var bytes_text = bytes.Select(c => ascii ? $"{Convert.ToChar(c)}" : $"{c}");
                        if (encoding == null)
                        {
                            result = string.Join(", ", bytes_text);
                            if (bytes.Length > 4)
                            {
                                var text = BytesToUnicode(result);
                                if (!result.StartsWith("0,") && !string.IsNullOrEmpty(text)) result = text;
                            }
                        }
                        else result = encoding.GetString(bytes);
                    }
                }
            }
            return (result);
        }

        public static string ByteStringToString(this string text, Encoding encoding = default, bool msb = false)
        {
            if (encoding == null) encoding = Encoding.UTF8;
            if (msb && encoding == Encoding.Unicode) encoding = Encoding.BigEndianUnicode;
            return (encoding.GetString(ByteStringToBytes(text)));
        }

        public static string GetAttributes(this MagickImage image, string attr)
        {
            string result = null;
            try
            {
                if (image is MagickImage && IsValidRead(image))
                {
                    var is_msb = image.Endian == Endian.MSB;

                    var exif = image.HasProfile("exif") ? image.GetExifProfile() : new ExifProfile();
                    var iptc = image.HasProfile("iptc") ? image.GetIptcProfile() : new IptcProfile();
                    Type exiftag_type = typeof(ImageMagick.ExifTag);

                    result = image.GetAttribute(attr);
                    if (attr.Contains("WinXP"))
                    {
                        var tag_name = $"XP{attr.Substring(11)}";
                        dynamic tag_property = exiftag_type.GetProperty(tag_name) ?? exiftag_type.GetProperty($"{tag_name}s") ?? exiftag_type.GetProperty(tag_name.Substring(0, tag_name.Length-1));
                        if (tag_property != null)
                        {
                            IExifValue tag_value = exif.GetValue(tag_property.GetValue(exif));
                            if (tag_value != null)
                            {
                                if (tag_value.DataType == ExifDataType.String)
                                    result = tag_value.GetValue() as string;
                                else if (tag_value.DataType == ExifDataType.Byte)
                                    result = Encoding.Unicode.GetString(tag_value.GetValue() as byte[]);
                            }
                        }
                    }
                    else if (attr.StartsWith("exif:") && !attr.Contains("WinXP"))
                    {
                        var tag_name = attr.Substring(5);
                        if (tag_name.Equals("FlashPixVersion")) tag_name = "FlashpixVersion";
                        dynamic tag_property = exiftag_type.GetProperty(tag_name) ?? exiftag_type.GetProperty($"{tag_name}s") ?? exiftag_type.GetProperty(tag_name.Substring(0, tag_name.Length-1));
                        if (tag_property != null)
                        {
                            IExifValue tag_value = exif.GetValue(tag_property.GetValue(exif));
                            if (tag_value != null)
                            {
                                if (tag_value.DataType == ExifDataType.String)
                                    result = tag_value.GetValue() as string;
                                else if (tag_value.DataType == ExifDataType.Rational && tag_value.IsArray)
                                {
                                    var rs = (Rational[])(tag_value.GetValue());
                                    var rr = new List<string>();
                                    foreach (var r in rs)
                                    {
                                        var ri = r.Numerator == 0 ? 0 : r.Numerator / r.Denominator;
                                        var rf = r.ToDouble();
                                        rr.Add(ri == rf ? $"{ri}" : (rf > 0 ? $"{rf:F8}" : $"{r.Numerator}/{r.Denominator}"));
                                    }
                                    result = string.Join(", ", rr);
                                }
                                else if (tag_value.DataType == ExifDataType.Rational)
                                {
                                    var r = (Rational)(tag_value.GetValue());
                                    var ri = r.Numerator == 0 ? 0 : r.Numerator / r.Denominator;
                                    var rf = r.ToDouble();
                                    result = ri == rf ? $"{ri}" : (rf > 0 ? $"{rf:F1}" : $"{r.Numerator}/{r.Denominator}");
                                }
                                else if (tag_value.DataType == ExifDataType.SignedRational && tag_value.IsArray)
                                {
                                    var rs = (SignedRational[])(tag_value.GetValue());
                                    var rr = new List<string>();
                                    foreach (var r in rs)
                                    {
                                        var ri = r.Numerator == 0 ? 0 : r.Numerator / r.Denominator;
                                        var rf = r.ToDouble();
                                        rr.Add(ri == rf ? $"{ri}" : (rf > 0 ? $"{rf:F8}" : $"{r.Numerator}/{r.Denominator}"));
                                    }
                                    result = string.Join(", ", rr);
                                }
                                else if (tag_value.DataType == ExifDataType.SignedRational)
                                {
                                    var r = (SignedRational)(tag_value.GetValue());
                                    var ri = r.Numerator == 0 ? 0 : r.Numerator / r.Denominator;
                                    var rf = r.ToDouble();
                                    result = ri == rf ? $"{ri}" : (rf > 0 ? $"{rf:F0}" : $"{r.Numerator}/{r.Denominator}");
                                }
                                else if (tag_value.DataType == ExifDataType.Undefined && tag_value.IsArray)
                                {
                                    if (tag_value.Tag == ImageMagick.ExifTag.ExifVersion)
                                        result = BytesToString(tag_value.GetValue() as byte[], true, is_msb);
                                    else if (tag_value.Tag == ImageMagick.ExifTag.GPSProcessingMethod || tag_value.Tag == ImageMagick.ExifTag.MakerNote)
                                        result = Encoding.UTF8.GetString(tag_value.GetValue() as byte[]).TrimEnd('\0').Trim();
                                    else if (tag_value.Tag == ImageMagick.ExifTag.UserComment)
                                        result = BytesToString(tag_value.GetValue() as byte[], false, is_msb);
                                    else
                                        result = BytesToString(tag_value.GetValue() as byte[], false, is_msb);
                                }
                                else if (tag_value.DataType == ExifDataType.Byte && tag_value.IsArray)
                                {
                                    result = BytesToString(tag_value.GetValue() as byte[], msb: is_msb);
                                }
                                else if (tag_value.DataType == ExifDataType.Unknown && tag_value.IsArray)
                                {
                                    var is_ascii = tag_value.Tag.ToString().Contains("Version");
                                    result = BytesToString(tag_value.GetValue() as byte[], is_ascii, is_msb);
                                }
                            }
                            else if (!string.IsNullOrEmpty(result))
                            {
                                if (tag_name.Equals("UserComment") && Regex.IsMatch(result, @"(0x\d{2,2},){2,}", RegexOptions.IgnoreCase))
                                {
                                    result = BytesToUnicode(result, offset: 8);
                                }
                            }
                        }
                        else if (attr.Equals("exif:ExtensibleMetadataPlatform"))
                        {
                            var xmp_tag = exif.Values.Where(t => t.Tag == ImageMagick.ExifTag.XMP);
                            if (xmp_tag.Count() > 0)
                            {
                                var bytes = xmp_tag.First().GetValue() as byte[];
                                result = Encoding.UTF8.GetString(bytes);
                            }
                        }
                    }
                    else if (attr.StartsWith("iptc:"))
                    {
                        Type tag_type = typeof(IptcTag);
                        var tag_name = attr.Substring(5);
                        dynamic tag_property = tag_type.GetProperty(tag_name);
                        if (tag_property != null)
                        {
                            IEnumerable<IIptcValue> iptc_values = iptc.GetAllValues(tag_property);
                            var values = new List<string>();
                            foreach (var tag_value in iptc_values)
                            {
                                if (tag_value != null) values.Add(tag_value.Value as string);
                            }
                            result = string.Join("; ", values);
                        }
                    }

                    if (attr.StartsWith("date:"))
                    {
                        if (DateTime.TryParse(result, out DateTime dt)) result = dt.ToString("yyyy-MM-ddTHH:mm:sszzz");
                    }

                    if (!string.IsNullOrEmpty(result)) result = result.Replace("\0", string.Empty).TrimEnd('\0');
                    if (!string.IsNullOrEmpty(result) && Regex.IsMatch($"{result.TrimEnd().TrimEnd(',')},", @"((\d{1,3}) ?, ?){16,}", RegexOptions.IgnoreCase)) result = ByteStringToString(result);
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

        public static string TextPadding(this string text, string label, int offset = 0, char padding_char = ' ')
        {
            var count = Encoding.ASCII.GetByteCount(label);
            return (TextPadding(text, count, offset, padding_char));
        }

        public static string TextPadding(this string text, int count, int offset = 0, char padding = ' ')
        {
            return (Regex.Replace(text, @"(\n\r|\r\n|\n|\r)", $"{Environment.NewLine}{" ".PadLeft(count + offset, padding)}", RegexOptions.IgnoreCase));
        }
        #endregion

        #region Magick.Net Helper
        public static Dictionary<string, string> AllSupportedFormats { get; set; } = new Dictionary<string, string>();
        public static IList<string> AllSupportedExts { get; set; } = new List<string>();
        public static string AllSupportedFiles { get; set; } = string.Empty;
        public static string AllSupportedFilters { get; set; } = string.Empty;

        public static Func<string, bool> SupportedFormat = ext => AllSupportedFormats.Keys.Select(e => $".{e.ToLower()}").Contains(Path.GetExtension(ext).ToLower());

        public static bool IsSupportedFormat(this string fmt)
        {
            return (SupportedFormat(fmt));
        }

        public static Func<string, bool> SupportedExt = ext => AllSupportedExts.Contains(Path.GetExtension(ext).ToLower());

        public static bool IsSupportedExt(this string ext)
        {
            return (SupportedExt(ext));
        }

        public static IMagickFormatInfo GetFormatInfo(this MagickImage image)
        {
            try { return (MagickFormatInfo.Create(image.Format)); }
            catch { return (MagickFormatInfo.Create(MagickFormat.Unknown)); }
        }

        public static IMagickFormatInfo GetFormatInfo(this MagickFormat format)
        {
            try { return (MagickFormatInfo.Create(format)); }
            catch { return (MagickFormatInfo.Create(MagickFormat.Unknown)); }
        }

        public static bool IsPNG(this MagickFormat format)
        {
            var fmts = new MagickFormat[] { MagickFormat.APng, MagickFormat.Png, MagickFormat.Png8, MagickFormat.Png00, MagickFormat.Png24, MagickFormat.Png32, MagickFormat.Png48, MagickFormat.Png64, MagickFormat.Pnm };
            var result = fmts.Contains(format);
            return (result);
        }

        public static bool IsPNG(this MagickImage image)
        {
            var result = false;
            if (image is MagickImage)
            {
                result = IsPNG(image.Format) || GetFormatInfo(image).MimeType.Equals("image/png", StringComparison.CurrentCultureIgnoreCase) || image.Format.ToString().StartsWith("png", StringComparison.CurrentCultureIgnoreCase);
            }
            return (result);
        }

        public static bool IsBMP(this MagickFormat format)
        {
            var fmts = new MagickFormat[] { MagickFormat.Bmp, MagickFormat.Bmp2, MagickFormat.Bmp3 };
            var result = fmts.Contains(format);
            return (result);
        }

        public static bool IsBMP(this MagickImage image)
        {
            var result = false;
            if (image is MagickImage)
            {
                result = IsBMP(image.Format) || GetFormatInfo(image).MimeType.Equals("image/bmp", StringComparison.CurrentCultureIgnoreCase) || image.Format.ToString().StartsWith("bmp", StringComparison.CurrentCultureIgnoreCase);
            }
            return (result);
        }

        public static bool IsTIF(this MagickFormat format)
        {
            var fmts = new MagickFormat[] { MagickFormat.Tif, MagickFormat.Tiff, MagickFormat.Tiff64 };
            var result = fmts.Contains(format);
            return (result);
        }

        public static bool IsTIF(this MagickImage image)
        {
            var result = false;
            if (image is MagickImage)
            {
                result = IsTIF(image.Format) || GetFormatInfo(image).MimeType.StartsWith("image/tif", StringComparison.CurrentCultureIgnoreCase) || image.Format.ToString().StartsWith("tif", StringComparison.CurrentCultureIgnoreCase);
            }
            return (result);
        }

        public static bool IsJPG(this MagickFormat format)
        {
            var fmts = new MagickFormat[] { MagickFormat.J2c, MagickFormat.J2k, MagickFormat.Jng, MagickFormat.Jp2, MagickFormat.Jpc, MagickFormat.Jpe, MagickFormat.Jpeg, MagickFormat.Jpg, MagickFormat.Jpm, MagickFormat.Jps, MagickFormat.Jpt };
            var result = fmts.Contains(format);
            return (result);
        }

        public static bool IsJPG(this MagickImage image)
        {
            var result = false;
            if (image is MagickImage)
            {
                result = IsJPG(image.Format) || GetFormatInfo(image).MimeType.Equals("image/jpeg", StringComparison.CurrentCultureIgnoreCase) || image.Format.ToString().StartsWith("jp");
            }
            return (result);
        }

        public static bool Valid(this MagickImage image)
        {
            return (image.IsValidRead() && image.Width > 0 && image.Height > 0);
        }

        public static bool Invalided(this MagickImage image)
        {
            return (image == null || image.Width == 0 || image.Height == 0);
        }

        public static bool IsValidRead(this MagickImage image)
        {
            return (image is MagickImage && MagickFormatInfo.Create(image.Format).SupportsReading);
        }

        public static Func<MagickImage, uint> FuncTotalColors = (i) => { return ((i is MagickImage) ? i.TotalColors : 0); };

        public static async Task<uint> CalcTotalColors(this MagickImage image, CancellationToken cancel = default)
        {
            var result = 0u;
            Func<uint> GetColorsCount = () => { return ((image is MagickImage) ? image.TotalColors : 0); };
            result = await Task.Run(GetColorsCount, cancellationToken: cancel);
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
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

        public static async Task<byte[]> ToBytes(this BitmapSource bitmap, string fmt = "")
        {
            if (string.IsNullOrEmpty(fmt)) fmt = ".png";
            return ((await ToMemoryStream(bitmap, fmt)).ToArray());
        }

        public static MagickImage Lut2Png(MagickImage lut)
        {
            MagickImage png = null;
            try
            {
                using (var ms = new MemoryStream())
                {
                    lut.Write(ms, MagickFormat.Png);
                    ms.Seek(0, SeekOrigin.Begin);
                    png = new MagickImage(ms);
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (png);
        }

        public static MagickImage Lut2Png(this Stream lut)
        {
            MagickImage png = null;
            try
            {
                using (var ms = new MemoryStream())
                {
                    var cube = new MagickImage(lut, MagickFormat.Cube);
                    cube.Write(ms, MagickFormat.Png);
                    ms.Seek(0, SeekOrigin.Begin);
                    png = new MagickImage(ms);
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (png);
        }

        private static readonly List<string> _auto_formats_ = new List<string>() { ".jpg", ".jpeg", ".png", ".bmp" };
        private static readonly Dictionary<string, MagickFormat> _supported_formats_ = new Dictionary<string, MagickFormat>();
        public static MagickFormat GetImageFileFormat(this string ext)
        {
            var result = MagickFormat.Unknown;
            try
            {
                if (_supported_formats_.Count <= 0)
                {
                    foreach (var fmt in MagickNET.SupportedFormats) _supported_formats_.Add($".{fmt.Format.ToString().ToLower()}", fmt.Format);
                }
                if (_supported_formats_.ContainsKey(ext.ToLower()) && !_auto_formats_.Contains(ext.ToLower())) result = _supported_formats_[ext.ToLower()];
            }
            catch { }
            return (result);
        }

        public static void GetExif(this MagickImage image)
        {
            try
            {
                if (image is MagickImage)
                {
                    //var exif = image.GetExifProfile() ?? new ExifProfile();
                    //var tag = exif.GetValue(ExifTag.XPTitle);
                    //if (tag != null) { var text = Encoding.Unicode.GetString(tag.Value).Trim('\0').Trim(); }
#if DEBUG
                    //Debug.WriteLine(text);
#endif
                    var profiles = new Dictionary<string, IImageProfile>();
                    foreach (var pn in image.ProfileNames)
                    {
                        if (image.HasProfile(pn)) profiles[pn] = image.GetProfile(pn);
                        if (pn.Equals("exif", StringComparison.CurrentCultureIgnoreCase))
                            profiles[pn] = image.GetExifProfile();
                        else if (pn.Equals("iptc", StringComparison.CurrentCultureIgnoreCase))
                            profiles[pn] = image.GetIptcProfile();
                        else if (pn.Equals("xmp", StringComparison.CurrentCultureIgnoreCase))
                            profiles[pn] = image.GetXmpProfile();
#if DEBUG
                        var profile = profiles[pn];
                        if (profile is ExifProfile)
                        {
                            var exif = profile as ExifProfile;
                            Debug.WriteLine(exif.GetValue(ExifTag.XPTitle));
                            Debug.WriteLine(exif.GetValue(ExifTag.XPAuthor));
                            Debug.WriteLine(exif.GetValue(ExifTag.XPKeywords));
                            Debug.WriteLine(exif.GetValue(ExifTag.XPComment));
                        }
                        else if (profile is IptcProfile)
                        {
                            var iptc = profile as IptcProfile;
                            Debug.WriteLine(iptc.GetValue(IptcTag.Title));
                            Debug.WriteLine(iptc.GetValue(IptcTag.Byline));
                            Debug.WriteLine(iptc.GetValue(IptcTag.BylineTitle));
                            Debug.WriteLine(iptc.GetValue(IptcTag.CopyrightNotice));
                            Debug.WriteLine(iptc.GetValue(IptcTag.Caption));
                            Debug.WriteLine(iptc.GetValue(IptcTag.CaptionWriter));
                        }
                        else if (profile is XmpProfile)
                        {
                            var xmp = profile as XmpProfile;
                            var xml = Encoding.UTF8.GetString(xmp.ToByteArray());
                            //image.SetAttribute()
                        }
#endif
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { ex.ShowMessage(); }
        }

        public static MagickFormat GetMagickFormat(this string fmt)
        {
            MagickFormat result;
            switch (fmt)
            {
                case "image/bmp":
                case "image/bitmap":
                case "CF_BITMAP":
                case "CF_DIB":
                case ".bmp":
                    result = MagickFormat.Bmp3;
                    break;
                case "image/gif":
                case "gif":
                case ".gif":
                    result = MagickFormat.Gif;
                    break;
                case "image/png":
                case "png":
                case "PNG":
                case ".png":
                    result = MagickFormat.Png;
                    break;
                case "image/jpg":
                case ".jpg":
                case "image/jpeg":
                case ".jpeg":
                    result = MagickFormat.Jpeg;
                    break;
                case "image/tif":
                case ".tif":
                case "image/tiff":
                case ".tiff":
                    result = MagickFormat.Tiff;
                    break;
                default:
                    result = MagickFormat.Unknown;
                    break;
            }
            return (result);
        }

        public static IMagickGeometry CalcBoundingBox(this MagickImage image)
        {
            var result = image?.BoundingBox;
            try
            {
                if (image is MagickImage)
                {
                    var diff = new MagickImage(image) { ColorType = ColorType.Bilevel };
                    result = diff.BoundingBox;
                    diff.Dispose();
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

        public static uint Quality(this MagickImage image)
        {
            uint result = 0;
            if (image is MagickImage)
            {
                result = image?.Quality ?? 0;
                var zip = new CompressionMethod[] { CompressionMethod.Zip, CompressionMethod.ZipS, CompressionMethod.Zstd, CompressionMethod.BZip, CompressionMethod.LZMA, CompressionMethod.LZW, CompressionMethod.RLE, CompressionMethod.NoCompression, CompressionMethod.LosslessJPEG };
                if (image is MagickImage && result == 0)
                {
                    if (image?.Compression == CompressionMethod.JPEG) result = 75;
                    else if (zip.Contains(image?.Compression ?? CompressionMethod.Undefined)) result = 100;
                }
            }
            return (result);
        }

        public static Dictionary<string, string> GetSupportedImageFormats()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            try
            {
                foreach (var fmt in MagickNET.SupportedFormats)
                {
                    if (fmt.SupportsReading)
                    {
                        if (fmt.MimeType != null && fmt.MimeType.StartsWith("video", StringComparison.CurrentCultureIgnoreCase)) continue;
                        else if (fmt.Description.StartsWith("video", StringComparison.CurrentCultureIgnoreCase)) continue;
                        else if (fmt.MimeType != null && fmt.MimeType.StartsWith("image", StringComparison.CurrentCultureIgnoreCase))
                            result.Add(fmt.Format.ToString(), fmt.Description);
                        else result.Add(fmt.Format.ToString(), fmt.Description);
                    }
                }

                result.Add("spa", "MikuMikuDance SPA File");
                result.Add("sph", "MikuMikuDance SPH File");

                //var fmts = Enum.GetNames(typeof(MagickFormat));
                //foreach (var fmt in fmts)
                //{
                //    result.Add(fmt, "");
                //}
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }

#if Q16HDRI
        private static MagickColor[] GetMatrix(this IPixelCollection<float> pixels, uint x, uint y, uint w, uint h)
#else
        private static MagickColor[] GetMatrix(this IPixelCollection<byte> pixels, uint x, uint y, uint w, uint h)
#endif
        {
            var ret = new List<MagickColor>();
#if Q16HDRI
            if (pixels is IPixelCollection<float>)
#else
            if (pixels is IPixelCollection<byte>)
#endif
            {
                var area = pixels.GetArea((int)x, (int)y, w, h);
                for (var i = 0; i < w; i++)
                {
                    for (var j = 0; j < h; j++)
                    {
                        var pixel = pixels.GetPixel(i, j);
                        if (pixel.Channels == 3)
                        {
                            var color = new MagickColor(pixel.GetChannel(0), pixel.GetChannel(1), pixel.GetChannel(2));
                            if (i < w && j < h) ret.Add(color);
                        }
                        else if (pixel.Channels == 4)
                        {
                            var color = new MagickColor(pixel.GetChannel(0), pixel.GetChannel(1), pixel.GetChannel(2), pixel.GetChannel(3));
                            if (i < w && j < h) ret.Add(color);
                        }
                    }
                }
            }
            return (ret.ToArray());
        }

        private static MagickColor[] GetMatrix(this MagickImage image, uint x, uint y, uint w, uint h)
        {
            var ret = new List<MagickColor>();
            if (image is MagickImage && image.IsValidRead())
            {
                var pixels = image.GetPixels();
                ret.AddRange(GetMatrix(pixels, x, y, w, h));
            }
            return (ret.ToArray());
        }

        public static bool GuessAlpha(this MagickImage image, uint window = 3, int threshold = 255)
        {
            var result = false;
            try
            {
                if (image is MagickImage && image.IsValidRead())
                {
                    var status = image?.HasAlpha ?? false;
                    if (status || image.IsPNG() || image.IsTIF() || image.IsBMP())
                    {
                        var w = image.Width;
                        var h = image.Height;
                        var m = window;
                        var mt = Math.Ceiling(m * m / 2.0);
                        var pixels = image.GetPixels();
                        var lt = GetMatrix(pixels, 0, 0, m, m).Count(c => c.A < threshold);
                        var rt = GetMatrix(pixels, w - m, 0, m, m).Count(c => c.A < threshold);
                        var lb = GetMatrix(pixels, 0, h - m, m, m).Count(c => c.A < threshold);
                        var rb = GetMatrix(pixels, w - m, h - m, m, m).Count(c => c.A < threshold);
                        var ct = GetMatrix(pixels, (uint)(w / 2.0 - m / 2.0) , (uint)(h / 2.0 - m / 2.0), m, m).Count(c => c.A < threshold);
                        status = lt > mt || rt > mt || lb > mt || rb > mt || ct > mt;
                    }
                    result = status;
                }
            }
            catch (Exception ex) { ex.ShowMessage("GuessAlpha"); }
            return (result);
        }

        private static System.Drawing.Color[] GetMatrix(System.Drawing.Bitmap bmp, int x, int y, int w, int h)
        {
            var ret = new List<System.Drawing.Color>();
            if (bmp is System.Drawing.Bitmap)
            {
                //var data = bmp.LockBits(new Rectangle(x, y, w, h), ImageLockMode.ReadOnly, bmp.PixelFormat);
                for (var i = x; i < x + w; i++)
                {
                    for (var j = y; j < y + h; j++)
                    {
                        if (i < bmp.Width && j < bmp.Height)
                            ret.Add(bmp.GetPixel(i, j));
                    }
                }
                //bmp.UnlockBits(data);
            }
            return (ret.ToArray());
        }

        public static bool GuessAlpha(this Stream source, int window = 3, int threshold = 255)
        {
            var result = false;
            try
            {
                if (source is Stream && source.CanRead)
                {
                    var status = false;
                    if (source.CanSeek) source.Seek(0, SeekOrigin.Begin);
                    using (System.Drawing.Image image = System.Drawing.Image.FromStream(source))
                    {
                        if (image is System.Drawing.Image && (
                            image.RawFormat.Guid.Equals(System.Drawing.Imaging.ImageFormat.Png.Guid) ||
                            image.RawFormat.Guid.Equals(System.Drawing.Imaging.ImageFormat.Bmp.Guid) ||
                            image.RawFormat.Guid.Equals(System.Drawing.Imaging.ImageFormat.Tiff.Guid)))
                        {
                            if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb) { status = true; }
                            else if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppPArgb) { status = true; }
                            else if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format16bppArgb1555) { status = true; }
                            else if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format64bppArgb) { status = true; }
                            else if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Format64bppPArgb) { status = true; }
                            else if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.PAlpha) { status = true; }
                            else if (image.PixelFormat == System.Drawing.Imaging.PixelFormat.Alpha) { status = true; }
                            else if (System.Drawing.Image.IsAlphaPixelFormat(image.PixelFormat)) { status = true; }

                            if (status)
                            {
                                var bmp = new System.Drawing.Bitmap(image);
                                var w = bmp.Width;
                                var h = bmp.Height;
                                var m = window;
                                var mt = Math.Ceiling(m * m / 2.0);
                                var lt = GetMatrix(bmp, 0, 0, m, m).Count(c => c.A < threshold);
                                var rt = GetMatrix(bmp, w - m, 0, m, m).Count(c => c.A < threshold);
                                var lb = GetMatrix(bmp, 0, h - m, m, m).Count(c => c.A < threshold);
                                var rb = GetMatrix(bmp, w - m, h - m, m, m).Count(c => c.A < threshold);
                                var ct = GetMatrix(bmp, (int)(w / 2.0 - m / 2.0) , (int)(h / 2.0 - m / 2.0), m, m).Count(c => c.A < threshold);
                                status = lt > mt || rt > mt || lb > mt || rb > mt || ct > mt;
                            }
                        }
                    }
                    result = status;
                }
            }
            catch (Exception ex) { ex.ShowMessage("GuessAlpha"); }
            return (result);
        }

        public static bool GuessAlpha(this byte[] buffer, int window = 3, int threshold = 255)
        {
            var result = false;
            if (buffer is byte[] && buffer.Length > 0)
            {
                using (var ms = new MemoryStream(buffer))
                {
                    result = GuessAlpha(ms, window, threshold);
                }
            }
            return (result);
        }

        public static bool GuessAlpha(this string file, int window = 3, int threshold = 255)
        {
            var result = false;

            if (File.Exists(file))
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(file)))
                {
                    result = GuessAlpha(ms, window, threshold);
                }
            }
            return (result);
        }

        public static ImageInformation GetInformation(this FrameworkElement element)
        {
            var result = new ImageInformation() { Tagetment = element };
            element?.Dispatcher.Invoke(() =>
            {
                if (element.Tag is ImageInformation) result = element.Tag as ImageInformation;
                else element.Tag = result;
            });
            return (result);
        }
        #endregion

        #region Keyboard Modifier
        /// <summary>
        /// 
        /// </summary>
        public class Modifier
        {
            public bool Shift { get; set; } = false;
            public bool Ctrl { get; set; } = false;
            public bool Alt { get; set; } = false;
            public bool Win { get; set; } = false;

            public bool OnlyShift { get; set; } = false;
            public bool OnlyCtrl { get; set; } = false;
            public bool OnlyAlt { get; set; } = false;
            public bool OnlyWin { get; set; } = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        public static async Task<Modifier> GetModifierAsync(this Application app)
        {
            var result = new Modifier();
            var modifiers = await app.Dispatcher.InvokeAsync(() => Keyboard.Modifiers);
            result.Shift = modifiers.HasFlag(ModifierKeys.Shift);
            result.Ctrl = modifiers.HasFlag(ModifierKeys.Control);
            result.Alt = modifiers.HasFlag(ModifierKeys.Alt);
            result.Win = modifiers.HasFlag(ModifierKeys.Windows);
            result.OnlyShift = modifiers == ModifierKeys.Shift;
            result.OnlyCtrl = modifiers == ModifierKeys.Control;
            result.OnlyAlt = modifiers == ModifierKeys.Alt;
            result.OnlyWin = modifiers == ModifierKeys.Windows;
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        public static Modifier GetModifier(this FrameworkElement element)
        {
            var result = new Modifier();
            var modifiers = element?.Dispatcher.Invoke(() => Keyboard.Modifiers) ?? ModifierKeys.None;
            result.Shift = modifiers.HasFlag(ModifierKeys.Shift);
            result.Ctrl = modifiers.HasFlag(ModifierKeys.Control);
            result.Alt = modifiers.HasFlag(ModifierKeys.Alt);
            result.Win = modifiers.HasFlag(ModifierKeys.Windows);
            result.OnlyShift = modifiers == ModifierKeys.Shift;
            result.OnlyCtrl = modifiers == ModifierKeys.Control;
            result.OnlyAlt = modifiers == ModifierKeys.Alt;
            result.OnlyWin = modifiers == ModifierKeys.Windows;
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        /// <param name="key"></param>
        /// <param name="exclude"></param>
        /// <returns></returns>
        public static bool IsModifierPressed(this FrameworkElement element, ModifierKeys key, bool exclude = true)
        {
            var result = false;
            var keys = GetModifier(element);
            if (exclude)
            {
                if (key == ModifierKeys.Shift) result = keys.OnlyShift; // keys.Shift && !keys.Alt && !keys.Ctrl && !keys.Win;
                else if (key == ModifierKeys.Alt) result = keys.OnlyAlt; // !keys.Shift && keys.Alt && !keys.Ctrl && !keys.Win;
                else if (key == ModifierKeys.Control) result = keys.OnlyCtrl; // !keys.Shift && !keys.Alt && keys.Ctrl && !keys.Win;
                else if (key == ModifierKeys.Windows) result = keys.OnlyWin; // !keys.Shift && !keys.Alt && !keys.Ctrl && keys.Win;
            }
            else
            {
                if (key == ModifierKeys.Shift) result = keys.Shift;
                else if (key == ModifierKeys.Alt) result = keys.Alt;
                else if (key == ModifierKeys.Control) result = keys.Ctrl;
                else if (key == ModifierKeys.Windows) result = keys.Win;
            }
            return (result);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <param name="exclude"></param>
        /// <returns></returns>
        public static bool IsShiftPressed(this FrameworkElement element, bool exclude = true)
        {
            return (IsModifierPressed(element, ModifierKeys.Shift, exclude));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <param name="exclude"></param>
        /// <returns></returns>
        public static bool IsCtrlPressed(this FrameworkElement element, bool exclude = true)
        {
            return (IsModifierPressed(element, ModifierKeys.Control, exclude));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="element"></param>
        /// <param name="exclude"></param>
        /// <returns></returns>
        public static bool IsAltPressed(this FrameworkElement element, bool exclude = true)
        {
            return (IsModifierPressed(element, ModifierKeys.Alt, exclude));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        /// <param name="exclude"></param>
        /// <returns></returns>
        public static bool IsWinPressed(this FrameworkElement element, bool exclude = true)
        {
            return (IsModifierPressed(element, ModifierKeys.Windows, exclude));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="child"></param>
        /// <returns></returns>
        //public static ModifierKeys GetModifiderKeys(this Application app)
        //{
        //    var result = Application.Current.Dispatcher.BeginInvoke(new Func<ModifierKeys>(delegate
        //    {
        //        return (Keyboard.Modifiers);
        //    }));
        //   return (result.Dispatcher.re);
        //}
        #endregion

        #region Misc
        public static T FindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            //get parent item
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            //we've reached the end of the tree
            if (parentObject == null) return null;

            //check if the parent matches the type we're looking for
            if (parentObject is T parent)
                return parent;
            else
                return FindParent<T>(parentObject);
        }

        public static UIElement GetContextMenuHost(this UIElement item)
        {
            UIElement result = null;
            if (item is MenuItem)
            {
                var parent = FindParent<ContextMenu>(item);
                if (parent is ContextMenu)
                {
                    result = parent.PlacementTarget;
                }
            }
            return (result);
        }

        public static IList<string> NaturalSort(this IList<string> list, int padding = 16, bool ascendin = true)
        {
            try
            {
                if (ascendin)
                    return (list is IList<string> ? list.OrderBy(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(padding, '0'))).ToList() : list);
                else
                    return (list is IList<string> ? list.OrderByDescending(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(padding, '0'))).ToList() : list);
            }
            catch (Exception ex) { ex.ShowMessage(); return (list); }
        }

        public static IEnumerable<string> NaturalSort(this IEnumerable<string> list, int padding = 16, bool ascendin = true)
        {
            try
            {
                if (ascendin)
                    return (list is IEnumerable<string> ? list.OrderBy(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(padding, '0'))) : list);
                else
                    return (list is IEnumerable<string> ? list.OrderByDescending(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(padding, '0'))) : list);
            }
            catch (Exception ex) { ex.ShowMessage(); return (list); }
        }
        #endregion

        #region File List && File System Watcher
        private static List<string> ext_imgs = new List<string>() { ".png", ".jpg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".jpeg", ".heif", ".heic" };
        private static List<string> ext_movs = new List<string>() { ".webm", ".mp4", ".mov", ".ogv", ".ogg",".gif" };

        private static ConcurrentDictionary<string, DateTime?> _file_list_storage_ = new ConcurrentDictionary<string, DateTime?>();
        private static List<string> _file_list_ = new List<string>();
        private static SemaphoreSlim _file_list_updating_ = new SemaphoreSlim(1);

        public static async Task<List<string>> GetFileList(this object file)
        {
            var result = new List<string>();
            var ret = (_file_list_.Count == 0 || (_file_list_.Any() && _file_list_.Count() != _file_list_storage_.Count)) ? await UpdateFileList() : true;
            if (await _file_list_updating_.WaitAsync(TimeSpan.FromSeconds(10)) && ret)
            {
                try
                {
                    // result = _FS_Change_Type_ == WatcherChangeTypes.All ? _file_list_ : _file_list_.Where(f => File.Exists(f)).ToList();
                    result = _file_list_;
                }
                finally { _file_list_updating_.Release(); }
            }
            return (result);
        }

        private static async Task<bool> UpdateFileList()
        {
            var result = false;
            if (await _file_list_updating_.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                result = await  Task.Run(() =>
                {
                    var ret = false;
                    try
                    {
                        _file_list_ = _file_list_storage_.Keys.Distinct().NaturalSort().ToList();
                        ret = true;
                    }
                    finally { _file_list_updating_.Release(); }
                    return (ret);
                });
            }
            return(result);
        }

        private static void UpdateInfoBox(EventArgs e = null)
        {
            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                var mainwindow = Application.Current.MainWindow as MainWindow;
                mainwindow?.UpdateInfoBox(e: e);
            });
        }

        private static List<string> _path_list_ = new List<string>();
        private static List<FileSystemWatcher> _file_watcher_ = new List<FileSystemWatcher>();
        private static WatcherChangeTypes _FS_Change_Type_ = WatcherChangeTypes.All;

        public static async Task<bool> InitFileList(this string path)
        {
            var result = false;
            if (await _file_list_updating_.WaitAsync(TimeSpan.FromSeconds(1)))
            {
                result = await Task.Run(async () =>
                {
                    var ret = false;
                    try
                    {
                        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                        {
                            _file_list_.Clear();
                            _file_list_storage_.Clear();
                            foreach (var file in Directory.GetFiles(path, "*.*").Where(f => SupportedExt(f)))
                            //foreach (var file in Directory.EnumerateFiles(path, "*.*").Where(f => SupportedExt(f)))
                            {
                                _file_list_storage_[file] = null;//File.GetLastWriteTime(file);
                            }
                            ret = true;
                        }
                    }
                    finally { _file_list_updating_.Release(); }
                    return (ret);
                });
            }
            result &= await UpdateFileList();
            result &= await InitWatcher(path);
            return (result);
        }

        public static async Task<bool> InitFileList(this IEnumerable<string> files)
        {
            var result = false;
            if (files?.Any() ?? false)
            {
                var paths = files.Select(f => Path.GetDirectoryName(Path.GetFullPath(f))).Distinct().OrderBy(d => d);
                if (await _file_list_updating_.WaitAsync(TimeSpan.FromSeconds(1)))
                {
                    result = await Task.Run(async () =>
                    {
                        var ret = false;
                        try
                        {
                            ReleaseWatcher(null);
                            _file_list_.Clear();
                            _file_list_storage_.Clear();
                            _FS_Change_Type_ = files.Count() > 1 ? WatcherChangeTypes.Renamed | WatcherChangeTypes.Deleted : WatcherChangeTypes.All;
                            foreach (var file in files) _file_list_storage_[file] = null;
                            foreach (var path in paths)
                            {
                                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                                {
                                    //foreach (var file in Directory.EnumerateFiles(path, "*.*").Where(f => SupportedExt(f)))
                                    foreach (var file in Directory.GetFiles(path, "*.*").Where(f => SupportedExt(f)))
                                    {
                                        _file_list_storage_[file] = null;//File.GetLastWriteTime(file);
                                    }
                                    ret = true;
                                }
                            }
                            ret = true;
                        }
                        finally { _file_list_updating_.Release(); }
                        return (ret);
                    });
                }
                result &= await UpdateFileList();
                result &= await InitWatcher(paths);
            }
            return (result);
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static async Task<bool> InitWatcher(string path)
        {
            var result = false;
            result = await Task.Run(() =>
            {
                var ret = false;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    var _watcher_ = new FileSystemWatcher(path, "*.*")
                    {
                        EnableRaisingEvents = true,
                        IncludeSubdirectories = false,
                        NotifyFilter = NotifyFilters.Size | NotifyFilters.FileName,
                    };
                    if (_FS_Change_Type_ == WatcherChangeTypes.All) _watcher_.Created += OnFSChanged;
                    _watcher_.Changed += OnFSChanged;
                    _watcher_.Deleted += OnFSChanged;
                    _watcher_.Renamed += OnFSRenamed;
                    _file_watcher_.Add(_watcher_);
                    ret = true;
                }
                return (ret);
            });
            return (result);
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static async Task<bool> InitWatcher(IEnumerable<string> paths)
        {
            var result = false;
            result = await Task.Run(() =>
            {
                var ret = false;
                if (_file_watcher_ is List<FileSystemWatcher>) ReleaseWatcher(null);
                foreach (var path in paths.Where(d => Directory.Exists(d)))
                {
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    {
                        var _watcher_ = new FileSystemWatcher(path, "*.*")
                        {
                            EnableRaisingEvents = true,
                            IncludeSubdirectories = false,
                            NotifyFilter = NotifyFilters.Size | NotifyFilters.FileName,
                        };
                        if (_FS_Change_Type_ == WatcherChangeTypes.All) _watcher_.Created += OnFSChanged;
                        _watcher_.Changed += OnFSChanged;
                        _watcher_.Deleted += OnFSChanged;
                        _watcher_.Renamed += OnFSRenamed;
                        _file_watcher_.Add(_watcher_);
                    }
                    ret = true;
                }
                return (ret);
            });
            return (result);
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public static void ReleaseWatcher(this FrameworkElement element)
        {
            foreach (var watcher in _file_watcher_)
            {
                watcher?.Dispose();
            }
            _file_watcher_.Clear();
        }

        private static async void OnFSChanged(object source, FileSystemEventArgs e)
        {
            try
            {
                if (e.ChangeType == WatcherChangeTypes.Created)
                {
                    if (_FS_Change_Type_ != WatcherChangeTypes.All) return;
                    if (File.Exists(e.FullPath))
                    {
                        var ext = Path.GetExtension(e.Name).ToLower();
                        if (ext_imgs.Contains(ext) || ext_movs.Contains(ext))
                        {
                            if (!_file_list_storage_.ContainsKey(e.FullPath)) _file_list_storage_[e.FullPath] = null;// File.GetLastWriteTime(e.FullPath);
                            if (await UpdateFileList()) UpdateInfoBox();
                        }
                    }
                }
                else if (e.ChangeType == WatcherChangeTypes.Changed)
                {
                    UpdateInfoBox();
                }
                else if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    var ext = Path.GetExtension(e.Name).ToLower();
                    if (ext_imgs.Contains(ext) || ext_movs.Contains(ext))
                    {
                        if (_file_list_storage_.ContainsKey(e.FullPath)) _file_list_storage_.TryRemove(e.FullPath, out DateTime? _);
                        if (await UpdateFileList()) UpdateInfoBox();
                    }
                }
            }
            catch (Exception ex) { ex.ShowMessage("FileSystemWatcher"); }
            finally
            {
            }
        }

        private static async void OnFSRenamed(object source, RenamedEventArgs e)
        {
            try
            {
                if (e.ChangeType == WatcherChangeTypes.Renamed)
                {
                    var ext = Path.GetExtension(e.Name).ToLower();
                    if (ext_imgs.Contains(ext) || ext_movs.Contains(ext))
                    {
                        if (_file_list_storage_.ContainsKey(e.OldFullPath))
                        {
                            _file_list_storage_[e.FullPath] = null;// File.GetLastWriteTime(e.FullPath);
                            _file_list_storage_.TryRemove(e.OldFullPath, out DateTime? _);
                        }
                        if (await UpdateFileList()) UpdateInfoBox(e);
                    }
                }
            }
            catch (Exception ex) { ex.ShowMessage("FilesySystemWather"); }
            finally
            {
            }
        }
        #endregion

    }
}
