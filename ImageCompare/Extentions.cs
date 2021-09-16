using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

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
            return (resourceSet.GetString(text));
        }

        public static string GetString(this string text, CultureInfo culture)
        {
            ChangeLocale(culture);
            return (resourceSet.GetString(text));
        }

        public static void Locale(this FrameworkElement element)
        {
            try
            {
                if (_be_locale_ == null) _be_locale_ = new Dictionary<FrameworkElement, bool>();
                if (_be_locale_.ContainsKey(element)) return;

                if (!string.IsNullOrEmpty(element.Uid))
                {
                    if (element is Button)
                    {
                        var ui = element as Button;
                        ui.Content = $"{ui.Uid}.Content".T() ?? ui.Content;
                    }
                    else if (element is TextBlock)
                    {
                        var ui = element as TextBlock;
                        ui.Text = $"{ui.Uid}.Text".T() ?? ui.Text;
                    }
                    else if (element is MenuItem)
                    {
                        var ui = element as MenuItem;
                        ui.Header = $"{ui.Uid}.Header".T() ?? ui.Header;
                        if (ui.Items.Count > 1)
                            foreach (var mi in ui.Items) if (mi is FrameworkElement) (mi as FrameworkElement).Locale();
                    }
                    else if (element is MenuBase)
                    {
                        var ui = element as MenuBase;
                        foreach (var mi in ui.Items) if (mi is FrameworkElement) (mi as FrameworkElement).Locale();
                    }
                    else if (element is ColorPicker)
                    {
                        var ui = element as ColorPicker;
                        ui.AdvancedTabHeader = $"{ui.Uid}.AdvancedTabHeader".T() ?? ui.AdvancedTabHeader;
                        ui.StandardTabHeader = $"{ui.Uid}.StandardTabHeader".T() ?? ui.StandardTabHeader;
                        ui.AvailableColorsHeader = $"{ui.Uid}.AvailableColorsHeader".T() ?? ui.AvailableColorsHeader;
                        ui.StandardColorsHeader = $"{ui.Uid}.StandardColorsHeader".T() ?? ui.StandardColorsHeader;
                        ui.RecentColorsHeader = $"{ui.Uid}.RecentColorsHeader".T() ?? ui.RecentColorsHeader;
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
                    if (!string.IsNullOrEmpty(ui.Uid)) { ui.ToolTip = $"{ui.Uid}.ToolTip".T() ?? ui.ToolTip; }                   
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
            foreach(var element in elements)
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
