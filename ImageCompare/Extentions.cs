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

namespace ImageCompare
{
    public static class Extentions
    {
        #region Locale Resource Helper
        private static CultureInfo resourceCulture = Properties.Resources.Culture ?? CultureInfo.CurrentCulture;
        private static System.Resources.ResourceManager resourceMan = Properties.Resources.ResourceManager;
        private static System.Resources.ResourceSet resourceSet = resourceMan.GetResourceSet(resourceCulture, true, true);
        private static Dictionary<FrameworkElement, bool> _be_locale_ = null;

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
            return (string.IsNullOrEmpty(t) ? null : t.Replace("\\n", Environment.NewLine));
        }

        public static string GetString(this string text, CultureInfo culture)
        {
            ChangeLocale(culture);
            return (GetString(text));
        }

        public static void Locale(this FrameworkElement element)
        {
            try
            {
                if (_be_locale_ == null) _be_locale_ = new Dictionary<FrameworkElement, bool>();
                if (_be_locale_.ContainsKey(element)) return;

                if (!string.IsNullOrEmpty(element.Uid))
                {
#if DEBUG
                    Debug.WriteLine($"==> UID: {element.Uid}");
#endif
                    if (element is ButtonBase)
                    {
                        var ui = element as ButtonBase;
                        if (ui.Content is string)
                        {
                            var text = $"{ui.Uid}.Content".T();
                            if (!string.IsNullOrEmpty(text)) ui.Content = text;
                        }
                    }
                    else if (element is TextBlock)
                    {
                        var ui = element as TextBlock;
                        var text = $"{ui.Uid}.Text".T();
                        if (!string.IsNullOrEmpty(text)) ui.Text = text;
                    }
                    else if (element is MenuItem)
                    {
                        var ui = element as MenuItem;
                        if (ui.Header is string)
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
                        var text = $"{ui.Uid}.AdvancedTabHeader".T();
                        if (!string.IsNullOrEmpty(text)) ui.AdvancedTabHeader = text;
                        text = $"{ui.Uid}.StandardTabHeader".T();
                        if (!string.IsNullOrEmpty(text)) ui.StandardTabHeader = text;
                        text = $"{ui.Uid}.AvailableColorsHeader".T();
                        if (!string.IsNullOrEmpty(text)) ui.AvailableColorsHeader = text;
                        text = $"{ui.Uid}.StandardColorsHeader".T();
                        if (!string.IsNullOrEmpty(text)) ui.StandardColorsHeader = text;
                        text = $"{ui.Uid}.RecentColorsHeader".T();
                        if (!string.IsNullOrEmpty(text)) ui.RecentColorsHeader = text;
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
                    if (!string.IsNullOrEmpty(ui.Uid) && ui.ToolTip is string)
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

        public static void Locale(this FrameworkElement element, CultureInfo culture)
        {
            try
            {
                ChangeLocale(culture);
                //if (!IsRecursiveCall("Locale") && _be_locale_ is Dictionary<FrameworkElement, bool>) _be_locale_.Clear();

                Locale(element);
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
                //if (!IsRecursiveCall("Locale") && _be_locale_ is Dictionary<FrameworkElement, bool>) _be_locale_.Clear();

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

        #endregion

        #region Magick.Net Helper
        public static bool Valid(this MagickImage image)
        {
            return (image is MagickImage && !image.IsDisposed);
        }

        public static bool Invalided(this MagickImage image)
        {
            return (image == null || image.IsDisposed);
        }
        #endregion
    }
}
