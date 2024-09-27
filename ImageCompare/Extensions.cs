using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

using ImageMagick;
using Xceed.Wpf.Toolkit;
using System.Text.RegularExpressions;
using System.IO;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace ImageCompare
{
    public static class Extensions
    {
        #region Locale Resource Helper
        private static CultureInfo resourceCulture = Properties.Resources.Culture ?? CultureInfo.CurrentCulture;
        private static System.Resources.ResourceManager resourceMan = Properties.Resources.ResourceManager;
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
            return (string.IsNullOrEmpty(t) ? null : Regex.Replace(t, @"(\n\r|\r\n|\n|\r)", Environment.NewLine));
        }

        public static string GetString(this string text, CultureInfo culture)
        {
            ChangeLocale(culture);
            return (GetString(text));
        }

        public static bool Contains(this string text)
        {
            return (resourceSet.GetString(text) == null ? false : true);
        }

        public static void Locale(this FrameworkElement element, IEnumerable<string> ignore_uids = null, IEnumerable<FrameworkElement> ignore_elements = null)
        {
            try
            {
                if (_be_locale_ == null) _be_locale_ = new Dictionary<FrameworkElement, bool>();
                if (_be_locale_.ContainsKey(element)) return;

                var element_name = element.Name ?? string.Empty;
                var elemet_uid = element.Uid ?? string.Empty;

                bool trans_uid = ignore_uids is IEnumerable<string> ? ignore_uids.Where(uid => uid.Equals(elemet_uid) || uid.Equals(element_name)).Count() <= 0 : true;
                bool trans_element = ignore_elements is IEnumerable<FrameworkElement> ? ignore_elements.Where(e => e == element).Count() <= 0 : true;

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
                    if (trans_uid && trans_element && !string.IsNullOrEmpty(ui.Uid) && ui.ToolTip is string)
                    {
                        var tip = $"{ui.Uid}.ToolTip".T();
                        if (!string.IsNullOrEmpty(tip)) ui.ToolTip = tip;
                    }
                    if (!_be_locale_.ContainsKey(element)) _be_locale_.Add(element, true);
                    if (ui.ContextMenu is ContextMenu) Locale(ui.ContextMenu);
                }
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show($"Locale : {element.Uid ?? element.ToString()} : {ex.Message}"); }
        }

        public static void Locale(this FrameworkElement element, CultureInfo culture, IEnumerable<string> ignore_uids = null, IEnumerable<FrameworkElement> ignore_elements = null)
        {
            try
            {
                ChangeLocale(culture);
                Locale(element, ignore_uids: ignore_uids, ignore_elements: ignore_elements);
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show($"Locale : {ex.Message}"); }
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show($"Locale : {ex.Message}"); }
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
            return(result);
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, ex.Message); }
            return (result);
        }

        public static int GetSystemColorDepth(this Application app)
        {
            var result = 0;
            try
            {
                result = GetColorDepth();
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, ex.Message); }
            return (result);
        }

        public static void ShowMessage(this string text, string prefix = "")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {

                    if (string.IsNullOrEmpty(prefix))
                        Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, text);
                    else
                        Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, text, prefix);
                }
                catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, ex.Message); }
            });
        }

        public static void ShowMessage(this Exception exception, string prefix = "")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {

                    var contents = $"{exception.Message}{Environment.NewLine}{exception.StackTrace}";
                    if (string.IsNullOrEmpty(prefix))
                        Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, contents);
                    else
                        Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, contents, prefix);

                }
                catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, ex.Message); }
            });
        }

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
                    //CanDoEvents.Release(max: 1);
                    if (CanDoEvents is SemaphoreSlim && CanDoEvents.CurrentCount <= 0) CanDoEvents.Release();
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
                if (element is FrameworkElement)
                {
                    element.DoEvents();
                    await Task.Delay(1);
                    await element.Dispatcher.InvokeAsync(action, realtime ? DispatcherPriority.Render : DispatcherPriority.Background);
                }
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
                        files.AddRange(Directory.EnumerateFiles(dir, "*.*").Where(f => AllSupportedExts.Contains(Path.GetExtension(f).ToLower())).NaturalSort());
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

        public static string SmartFileSize(this long v, double factor = 1, bool unit = true, int padleft = 0) { return (SmartFileSize((double)v, factor, unit, padleft: padleft)); }

        public static string SmartFileSize(this double v, double factor = 1, bool unit = true, bool trimzero = true, int padleft = 0)
        {
            string v_str = string.Empty;
            string u_str = string.Empty;
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

        private static byte[] ByteStringToBytes(string text, bool msb = false, int offset = 0)
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

        private static string BytesToUnicode(string text, bool msb = false, int offset = 0)
        {
            var result = text;
            if (!string.IsNullOrEmpty(text))
            {
                var bytes = ByteStringToBytes(text, msb);
                if (bytes.Length > offset) result = msb ? Encoding.BigEndianUnicode.GetString(bytes.Skip(offset).ToArray()) : Encoding.Unicode.GetString(bytes.Skip(offset).ToArray());
            }
            return (result);
        }

        private static string BytesToString(byte[] bytes, bool ascii = false, bool msb = false, Encoding encoding = null)
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

        private static string ByteStringToString(string text, Encoding encoding = default(Encoding), bool msb = false)
        {
            if (encoding == null) encoding = Encoding.UTF8;
            if (msb && encoding == Encoding.Unicode) encoding = Encoding.BigEndianUnicode;
            return (encoding.GetString(ByteStringToBytes(text)));
        }

        public static bool IsValidRead(this MagickImage image)
        {
            return (image is MagickImage && MagickFormatInfo.Create(image.Format).SupportsReading);
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
                        DateTime dt;
                        if (DateTime.TryParse(result, out dt)) result = dt.ToString("yyyy-MM-ddTHH:mm:sszzz");
                    }

                    if (!string.IsNullOrEmpty(result)) result = result.Replace("\0", string.Empty).TrimEnd('\0');
                    if (!string.IsNullOrEmpty(result) && Regex.IsMatch($"{result.TrimEnd().TrimEnd(',')},", @"((\d{1,3}) ?, ?){16,}", RegexOptions.IgnoreCase)) result = ByteStringToString(result);
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
            return (result);
        }
        #endregion

        #region Magick.Net Helper
        public static Dictionary<string, string> AllSupportedFormats { get; set; } = new Dictionary<string, string>();
        public static IList<string> AllSupportedExts { get; set; } = new List<string>();
        public static string AllSupportedFiles { get; set; } = string.Empty;
        public static string AllSupportedFilters { get; set; } = string.Empty;

        public static bool Valid(this MagickImage image)
        {
            return (image is MagickImage);
        }

        public static bool Invalided(this MagickImage image)
        {
            return (image == null);
        }

        public static Func<MagickImage, int> FuncTotalColors = (i)=> { return ((i is MagickImage) ? i.TotalColors : 0); };

        public static async Task<int> CalcTotalColors(this MagickImage image)
        {
            var result = 0;
            Func<int> GetColorsCount = () => { return ((image is MagickImage) ? image.TotalColors : 0);};
            result = await Application.Current.Dispatcher.InvokeAsync<int>(GetColorsCount, DispatcherPriority.Background);
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

        private static List<string> _auto_formats_ = new List<string>() { ".jpg", ".jpeg", ".png", ".bmp" };
        private static Dictionary<string, MagickFormat> _supported_formats_ = new Dictionary<string, MagickFormat>();
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
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show(Application.Current.MainWindow, ex.Message); }
        }

        public static IMagickGeometry CalcBoundingBox(this MagickImage image)
        {
            var result = image.BoundingBox;
            try
            {
                if (image is MagickImage)
                {
                    var diff = new MagickImage(image);
                    diff.ColorType = ColorType.Bilevel;
                    result = diff.BoundingBox;
                    diff.Dispose();
                }
            }
            catch (Exception ex) { ex.ShowMessage(); }
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
        #endregion

        #region Misc
        public static IList<string> NaturalSort(this IList<string> list, int padding = 16)
        {
            try
            {
                return (list is IList<string> ? list.OrderBy(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(padding, '0'))).ToList() : list);
            }
            catch (Exception ex) { ex.ShowMessage(); return (list); }
        }

        public static IEnumerable<string> NaturalSort(this IEnumerable<string> list, int padding = 16)
        {
            try
            {
                return (list is IEnumerable<string> ? list.OrderBy(x => Regex.Replace(x, @"\d+", m => m.Value.PadLeft(padding, '0'))) : list);
            }
            catch (Exception ex) { ex.ShowMessage(); return (list); }
        }
        #endregion

        public static ImageInformation GetInformation(this FrameworkElement element)
        {
            var result = new ImageInformation() { Tagetment = element };
            if (element is FrameworkElement)
            {
                element.Dispatcher.Invoke(() => 
                {
                    if (element.Tag is ImageInformation) result = element.Tag as ImageInformation;
                    else element.Tag = result;
                });

            }
            return (result);
        }
    }
}
