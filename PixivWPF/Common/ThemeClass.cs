using MahApps.Metro;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PixivWPF.Common
{
    public static class Theme
    {
        private static List<string> accents = new List<string>() {
                //"BaseDark","BaseLight",
                "Amber","Blue","Brown","Cobalt","Crimson","Cyan", "Emerald","Green",
                "Indigo","Lime","Magenta","Mauve","Olive","Orange", "Pink",
                "Purple","Red","Sienna","Steel","Taupe","Teal","Violet","Yellow"
        };

        private static Setting setting = Application.Current.Setting();

        public static Color ToColor(this Brush b, bool prefixsharp = true)
        {
            if (b is SolidColorBrush)
            {
                return (b as SolidColorBrush).Color;
            }
            else
            {
                try
                {
                    var hc = b.ToString();//.Replace("#", "");
                    var c = System.Drawing.ColorTranslator.FromHtml(hc);
                    var rc = Color.FromArgb(c.A, c.R, c.G, c.B);
                    return (rc);
                }
                catch (Exception) { return (default(Color)); }
            }
        }

        public static Brush ToBrush(this Color c)
        {
            return (new SolidColorBrush(c));
        }

        public static string ToHtml(this Brush b, bool prefixsharp = true)
        {
            if (prefixsharp)
                return (b.ToString());
            else
                return (b.ToString().Replace("#", ""));
        }

        public static string ToHtml(this Color c, bool alpha = true, bool prefixsharp = true)
        {
            string result = string.Empty;

            if (alpha)
                result = string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", c.A, c.R, c.G, c.B);
            else
                result = string.Format("{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);

            if (prefixsharp)
                result = $"#{result}";

            return (result);
        }

        public static string ToRGB(this Color c, bool alpha = true, bool prefix = true)
        {
            string result = string.Empty;

            if (alpha)
                result = string.Format("{1}, {2}, {3}, {0}", c.A, c.R, c.G, c.B);
            else
                result = string.Format("{0}, {1}, {2}", c.R, c.G, c.B);

            if (prefix)
            {
                var func = alpha ? "rgba" : "rgb";
                result = $"{func}({result})";
            }

            return (result);
        }

        public static IList<string> Accents
        {
            get { return accents; }
        }

        public static void Toggle()
        {
            Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
            var appTheme = appStyle.Item1;
            var appAccent = appStyle.Item2;

            var target = ThemeManager.GetInverseAppTheme(appTheme);
            ThemeManager.ChangeAppStyle(Application.Current, appAccent, target);
            if (setting.Theme != target.Name)
            {
                setting.Theme = target.Name;
                setting.Save();
            }
        }

        public static string CurrentAccent
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return appAccent.Name;
            }
            set
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                ThemeManager.ChangeAppStyle(Application.Current, ThemeManager.GetAccent(value), appTheme);
                if (setting.Accent != value)
                {
                    setting.Accent = value;
                    setting.Save();
                }
            }
        }

        public static string CurrentTheme
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return appTheme.Name;
            }
            set
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                ThemeManager.ChangeAppStyle(Application.Current, appAccent, ThemeManager.GetAppTheme(value));
                if (setting.Theme != value)
                {
                    setting.Theme = value;
                    setting.Save();
                }
            }
        }

        #region MahApps Resource Keys
        /// Theme Color/Brush
        /// ===================================================================
        /// "BlackBrush"
        /// "BlackColor"
        /// "BlackColorBrush"
        /// "ButtonMouseOverBorderBrush"
        /// "ButtonMouseOverInnerBorderBrush"
        /// "ComboBoxMouseOverBorderBrush"
        /// "ComboBoxMouseOverInnerBorderBrush"
        /// "ContextMenuBackgroundBrush"
        /// "ContextMenuBorderBrush"
        /// "ControlBackgroundBrush"
        /// "DisabledMenuItemForeground"
        /// "DisabledMenuItemGlyphPanel"
        /// "DisabledWhiteBrush"
        /// "FlatButtonPressedBackgroundBrush"
        /// "FlyoutBackgroundBrush"
        /// "FlyoutColor"
        /// "FlyoutForegroundBrush"
        /// "Gray1"
        /// "Gray2"
        /// "Gray3"
        /// "Gray4"
        /// "Gray5"
        /// "Gray6"
        /// "Gray7"
        /// "Gray8"
        /// "Gray9"
        /// "Gray10"
        /// "GrayBrush1"
        /// "GrayBrush2"
        /// "GrayBrush7"
        /// "GrayBrush8"
        /// "GrayBrush10"
        /// "GrayHover"
        /// "GrayHoverBrush"
        /// "GrayNormal"
        /// "GrayNormalBrush"
        /// "LabelTextBrush"
        /// "MahApps.Metro.Brushes.Badged.DisabledBackgroundBrush"
        /// "MahApps.Metro.Brushes.ToggleSwitchButton.OffBorderBrush.Win10"
        /// "MahApps.Metro.Brushes.ToggleSwitchButton.OffDisabledBorderBrush.Win10"
        /// "MahApps.Metro.Brushes.ToggleSwitchButton.OffMouseOverBorderBrush.Win10"
        /// "MahApps.Metro.Brushes.ToggleSwitchButton.OnSwitchDisabledBrush.Win10"
        /// "MahApps.Metro.Brushes.ToggleSwitchButton.PressedBrush.Win10"
        /// "MahApps.Metro.Brushes.ToggleSwitchButton.ThumbIndicatorBrush.Win10"
        /// "MahApps.Metro.Brushes.ToggleSwitchButton.ThumbIndicatorDisabledBrush.Win10"
        /// "MahApps.Metro.Brushes.ToggleSwitchButton.ThumbIndicatorMouseOverBrush.Win10"
        /// "MahApps.Metro.Brushes.ToggleSwitchButton.ThumbIndicatorPressedBrush.Win10"
        /// "MenuBackgroundBrush"
        /// "MenuItemBackgroundBrush"
        /// "MenuItemSelectionFill"
        /// "MenuItemSelectionStroke"
        /// "MenuShadowColor"
        /// "MetroDataGrid.DisabledHighlightBrush"
        /// "SliderThumbDisabled"
        /// "SliderTrackDisabled"
        /// "SliderTrackHover"
        /// "SliderTrackNormal"
        /// "SliderValueDisabled"
        /// "SubMenuBackgroundBrush"
        /// "SubMenuBorderBrush"
        /// "TextBoxFocusBorderBrush"
        /// "TextBoxMouseOverInnerBorderBrush"
        /// "TextBrush"
        /// "TopMenuItemPressedFill"
        /// "TopMenuItemPressedStroke"
        /// "TopMenuItemSelectionStroke"
        /// "WhiteBrush"
        /// "WhiteColor"
        /// "WhiteColorBrush"
        /// "WindowBackgroundBrush"
        /// {ControlTextBrush}
        /// {MenuTextBrush}
        /// {WindowBrush}
        /// ===================================================================
        /// 
        /// Accent Color/Brush
        /// ===================================================================
        /// "AccentBaseColor"
        /// "AccentBaseColorBrush"
        /// "AccentColor"
        /// "AccentColor2"
        /// "AccentColor3"
        /// "AccentColor4"
        /// "AccentColorBrush"
        /// "AccentColorBrush2"
        /// "AccentColorBrush3"
        /// "AccentColorBrush4"
        /// "AccentSelectedColorBrush"
        /// "CheckmarkFill"
        /// "HighlightBrush"
        /// "HighlightColor"
        /// "IdealForegroundColor"
        /// "IdealForegroundColorBrush"
        /// "IdealForegroundDisabledBrush"
        /// "MahApps.Metro.Brushes.ToggleSwitchButton.OnSwitchBrush.Win10"
        /// "MahApps.Metro.Brushes.ToggleSwitchButton.OnSwitchMouseOverBrush.Win10"
        /// "MahApps.Metro.Brushes.ToggleSwitchButton.ThumbIndicatorCheckedBrush.Win10"
        /// "MetroDataGrid.FocusBorderBrush"
        /// "MetroDataGrid.HighlightBrush"
        /// "MetroDataGrid.HighlightTextBrush"
        /// "MetroDataGrid.InactiveSelectionHighlightBrush"
        /// "MetroDataGrid.InactiveSelectionHighlightTextBrush"
        /// "MetroDataGrid.MouseOverHighlightBrush"
        /// "ProgressBrush"
        /// "RightArrowFill"
        /// "WindowTitleColorBrush"
        /// ===================================================================
        #endregion

        public static Color TransparentColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["WhiteColorBrush"] as Brush).ToColor();
            }
        }

        public static Brush TransparentBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["TransparentBrush"] as Brush);
            }
        }

        public static Color WhiteColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["WhiteColorBrush"] as Brush).ToColor();
            }
        }

        public static Brush WhiteBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["WhiteBrush"] as Brush);
            }
        }

        public static Color BlackColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["BlackColorBrush"] as Brush).ToColor();
            }
        }

        public static Brush BlackBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["BlackBrush"] as Brush);
            }
        }

        public static Color AccentColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (Color)appAccent.Resources["AccentColor"];
            }
        }

        public static Brush AccentBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return appAccent.Resources["AccentColorBrush"] as Brush;
            }
        }

        public static Color AccentColor2
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (Color)appAccent.Resources["AccentColor2"];
            }
        }

        public static Brush AccentBrush2
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return appAccent.Resources["AccentColorBrush2"] as Brush;
            }
        }

        public static Color AccentColor3
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (Color)appAccent.Resources["AccentColor3"];
            }
        }
        
        public static Brush AccentBrush3
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return appAccent.Resources["AccentColorBrush3"] as Brush;
            }
        }

        public static Color AccentColor4
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (Color)appAccent.Resources["AccentColor4"];
            }
        }

        public static Brush AccentBrush4
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return appAccent.Resources["AccentColorBrush4"] as Brush;
            }
        }

        public static Color AccentBaseColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appAccent.Resources["AccentBaseColorBrush"] as Brush).ToColor();
            }
        }

        public static Brush AccentBaseBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return appAccent.Resources["AccentBaseColorBrush"] as Brush;
            }
        }

        public static Color TextColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["TextBrush"] as Brush).ToColor();
            }
        }

        public static Brush TextBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["TextBrush"] as Brush);
            }
        }

        public static Color LabelTextColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["LabelTextBrush"] as Brush).ToColor();
            }
        }

        public static Brush LabelTextBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["LabelTextBrush"] as Brush);
            }
        }

        public static Color IdealForegroundColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (Color)appAccent.Resources["IdealForegroundColor"];
            }
        }

        public static Brush IdealForegroundBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return appAccent.Resources["IdealForegroundColorBrush"] as Brush;
            }
        }

        public static Color IdealForegroundDisableColor
        {
            get
            {
                return(IdealForegroundDisableBrush.ToColor());
            }
        }

        public static Brush IdealForegroundDisableBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return appAccent.Resources["IdealForegroundDisabledBrush"] as Brush;
            }
        }

        public static Color WindowColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["WindowBrush"] as Brush).ToColor();
            }
        }

        public static Brush WindowBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["WindowBrush"] as Brush);
            }
        }

        public static Color WindowTitleColor
        {
            get
            {
                return WindowTitleBrush.ToColor();
            }
        }

        public static Brush WindowTitleBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appAccent.Resources["WindowTitleColorBrush"] as Brush);
            }
        }

        public static Color GrayColors(int index)
        {
            Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
            AppTheme appTheme = appStyle.Item1;
            Accent appAccent = appStyle.Item2;
            if (index < 1) index = 1;
            else if (index > 10) index = 10;
            return (Color)appTheme.Resources[$"Gray{index}"];
        }

        public static Brush GrayBrushs(int index)
        {
            return GrayColors(index).ToBrush();
        }

        public static Color GrayColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["GrayNormal"] as Brush).ToColor();
            }
        }

        public static Brush GrayBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["GrayNormalBrush"] as Brush);
            }
        }

        public static Color SemiTransparentGreyColor
        {
            get
            {
                return(SemiTransparentGreyBrush.ToColor());
            }
        }

        public static Brush SemiTransparentGreyBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["SemiTransparentGreyBrush"] as Brush);
            }
        }

        public static Color SemiTransparentWhiteColor
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["SemiTransparentWhiteBrush"] as Brush).ToColor();
            }
        }

        public static Brush SemiTransparentWhiteBrush
        {
            get
            {
                Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
                AppTheme appTheme = appStyle.Item1;
                Accent appAccent = appStyle.Item2;
                return (appTheme.Resources["SemiTransparentWhiteBrush"] as Brush);
            }
        }


    }

}
