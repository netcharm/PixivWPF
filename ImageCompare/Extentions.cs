using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using ImageMagick;
using System.Windows.Controls;
using Xceed.Wpf.Toolkit;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace ImageCompare
{
    public static class Extentions
    {
        private static CultureInfo resourceCulture = Properties.Resources.Culture ?? CultureInfo.CurrentCulture;
        private static System.Resources.ResourceManager resourceMan = Properties.Resources.ResourceManager;
        private static System.Resources.ResourceSet resourceSet = resourceMan.GetResourceSet(resourceCulture, true, true);

        public static string _(this string text)
        {            
            return (resourceMan.GetString(text));
        }

        public static string T(this string text)
        {
            return (resourceMan.GetString(text));
        }

        public static string GetString(this string text)
        {
            return (resourceMan.GetString(text));
        }

        private static Dictionary<UIElement, bool> _be_locale_ = null;
        public static void Locale(this UIElement element)
        {
            try
            {
                if(_be_locale_ == null) _be_locale_ = new Dictionary<UIElement, bool>();
                if (_be_locale_.ContainsKey(element)) return;

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
                }
                else if (element is MenuBase)
                {
                    var ui = element as MenuBase;
                    foreach (var mi in ui.Items) if (mi is UIElement) (mi as UIElement).Locale();
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
                //else
                {
                    var child_count = VisualTreeHelper.GetChildrenCount(element);
                    if (child_count > 0)
                    {
                        for (int i = 0; i < child_count; i++)
                        {
                            var child = VisualTreeHelper.GetChild(element, i);
                            if (child is UIElement) (child as UIElement).Locale();
                        }
                    }
                    else
                    {
                        var childs = LogicalTreeHelper.GetChildren(element);
                        foreach (var child in childs)
                        {
                            if (child is UIElement) (child as UIElement).Locale();
                        }
                    }
                }
                if (element is FrameworkElement)
                {
                    var ui = element as FrameworkElement;
                    ui.ToolTip = $"{ui.Uid}.ToolTip".T() ?? ui.ToolTip;
                    if (!_be_locale_.ContainsKey(element)) _be_locale_.Add(element, true);
                }
            }
            catch (Exception ex) { Xceed.Wpf.Toolkit.MessageBox.Show($"{element.Uid ?? element.ToString()} : {ex.Message}"); }
        }

        public static bool Valid(this MagickImage image)
        {
            return (image is MagickImage && !image.IsDisposed);
        }

        public static bool Invalided(this MagickImage image)
        {
            return (image == null || image.IsDisposed);
        }
    }
}
