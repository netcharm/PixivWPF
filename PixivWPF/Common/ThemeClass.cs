using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

using MahApps.Metro;
using ControlzEx.Theming;

namespace PixivWPF.Common
{
    public class SimpleAccent
    {
        public string AccentName { get; set; } //= Theme.CurrentAccent;
        public string AccentStyle { get; set; } //= Theme.CurrentStyle;
        public Color AccentColor { get; set; } //= Theme.AccentColor;
        public Brush AccentBrush { get; set; } //= Theme.AccentBrush;
    }

    public static class Theme
    {
        private static List<string> accents = new List<string>() {
            //"BaseDark","BaseLight",
            "Amber", "Blue", "Brown", "Cobalt", "Crimson", "Cyan", "Emerald", "Green",
            "Indigo", "Lime", "Magenta", "Mauve", "Olive", "Orange", "Pink",
            "Purple", "Red", "Sienna", "Steel", "Taupe", "Teal", "Violet", "Yellow"
        };

        private static Setting setting = Application.Current.LoadSetting();

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
            get { return ThemeManager.Current.ColorSchemes.OrderBy(cs => cs).ToList(); }
        }

        public static IList<string> Styles
        {
            get { return ThemeManager.Current.BaseColors; }
        }

        public static IList<string> Themes
        {
            get
            {
                var themes = new List<string>();
                foreach(var theme in ThemeManager.Current.Themes)
                {
                    themes.Add(theme.Name);
                }
                return (themes);
            }
        }

        private static Dictionary<string, Color> accent_colors = new Dictionary<string, Color>();
        public static Dictionary<string, Color> AllAccentColors
        {
            get
            {
                if (accent_colors.Count != ThemeManager.Current.Themes.Count)
                {
                    accent_colors.Clear();
                    foreach (var theme in ThemeManager.Current.Themes)
                    {
                        accent_colors[theme.ColorScheme] = theme.PrimaryAccentColor;
                    }
                }
                return (accent_colors);
            }
        }

        private static List<SimpleAccent> accent_color_list = new List<SimpleAccent>();
        public static IList<SimpleAccent> AccentColorList
        {
            get
            {
                if (accent_color_list.Count == 0)
                {
                    accent_color_list.Clear();
                    foreach (var theme in ThemeManager.Current.Themes)
                    {
                        if (accent_color_list.Where(a => a.AccentName.Equals(theme.ColorScheme)).Count() <= 0)
                        {
                            accent_color_list.Add(new SimpleAccent()
                            {
                                AccentName = theme.ColorScheme,
                                AccentStyle = theme.BaseColorScheme,
                                AccentColor = theme.PrimaryAccentColor,
                                AccentBrush = theme.PrimaryAccentColor.ToBrush()
                            });
                        }
                    }
                    accent_color_list = accent_color_list.OrderBy(ac => ac.AccentName).ToList();
                }
                return (accent_color_list);
            }
        }

        public static void SetSyncMode(ThemeSyncMode mode = ThemeSyncMode.SyncWithAppMode)
        {
            ThemeManager.Current.ThemeSyncMode = mode;
        }

        public static async void Sync(ThemeSyncMode mode = ThemeSyncMode.SyncWithAppMode)
        {
            await new Action(() =>
            {
                ThemeManager.Current.ThemeSyncMode = mode;
                ThemeManager.Current.SyncTheme(mode);
            }).InvokeAsync();
        }

        public static void Toggle()
        {
            var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
            var target = ThemeManager.Current.GetInverseTheme(appTheme);
            ThemeManager.Current.ChangeTheme(Application.Current, target);
            setting = Application.Current.LoadSetting();
            if (setting.CurrentTheme != target.BaseColorScheme || setting.CurrentAccent != target.ColorScheme)
            {
                setting.CurrentTheme = target.BaseColorScheme;
                setting.CurrentAccent = target.ColorScheme;
                setting.Save();
            }
        }

        public static void Change(string style = "", string accent = "")
        {
            if (string.IsNullOrEmpty(style)) style = CurrentStyle;
            if (string.IsNullOrEmpty(accent)) accent = CurrentAccent;
            if (!style.Equals(CurrentStyle, StringComparison.CurrentCultureIgnoreCase) || 
                !accent.Equals(CurrentAccent, StringComparison.CurrentCultureIgnoreCase))
            {
                ThemeManager.Current.ChangeTheme(Application.Current, style, accent);
                setting = Application.Current.LoadSetting();
                if (setting.CurrentAccent.Equals(accent, StringComparison.CurrentCultureIgnoreCase) || 
                    setting.CurrentTheme.Equals(style, StringComparison.CurrentCultureIgnoreCase))
                {
                    setting.CurrentAccent = accent;
                    setting.CurrentTheme = style;
                    setting.Save();
                }
            }
        }

        public static void Change(string theme = "")
        {
            if(!string.IsNullOrEmpty(theme))
            {
                CurrentTheme = theme;
            }
        }

        public static string CurrentTheme
        {
            get { return(ThemeManager.Current.DetectTheme(Application.Current).Name); }
            set
            {
                if (Themes.Contains(value))
                {
                    ThemeManager.Current.ChangeTheme(Application.Current, value);
                    var theme = ThemeManager.Current.DetectTheme(Application.Current);
                    var color = theme.ColorScheme;
                    var style = theme.BaseColorScheme;
                    setting = Application.Current.LoadSetting();
                    if (setting.CurrentAccent.Equals(color, StringComparison.CurrentCultureIgnoreCase) ||
                        setting.CurrentTheme.Equals(style, StringComparison.CurrentCultureIgnoreCase))
                    {
                        setting.CurrentAccent = color;
                        setting.CurrentTheme = style;
                        setting.Save();
                    }
                }
            }
        }

        public static string CurrentAccent
        {
            get { return (ThemeManager.Current.DetectTheme(Application.Current).ColorScheme); }
            set
            {
                if (Accents.Contains(value))
                {
                    ThemeManager.Current.ChangeThemeColorScheme(Application.Current, value);
                    setting = Application.Current.LoadSetting();
                    if (setting.CurrentAccent.Equals(value, StringComparison.CurrentCultureIgnoreCase))
                    {
                        setting.CurrentAccent = value;
                        setting.Save();
                    }
                }
            }
        }

        public static string CurrentStyle
        {
            get { return (ThemeManager.Current.DetectTheme(Application.Current).BaseColorScheme); }
            set
            {
                if (Styles.Contains(value))
                {
                    ThemeManager.Current.ChangeThemeBaseColor(Application.Current, value);
                    setting = Application.Current.LoadSetting();
                    if (setting.CurrentTheme.Equals(value, StringComparison.CurrentCultureIgnoreCase))
                    {
                        setting.CurrentTheme = value;
                        setting.Save();
                    }
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
        /// "MahApps.Brushes.Text"
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
        /// DEFAULT COMMON CONTROL COLORS
        /// 
        /// MahApps.Colors.SystemAltHigh
        /// MahApps.Colors.SystemAltLow
        /// MahApps.Colors.SystemAltMedium
        /// MahApps.Colors.SystemAltMediumHigh
        /// MahApps.Colors.SystemAltMediumLow
        /// MahApps.Colors.SystemBaseHigh
        /// MahApps.Colors.SystemBaseLow
        /// MahApps.Colors.SystemBaseMedium
        /// MahApps.Colors.SystemBaseMediumHigh
        /// MahApps.Colors.SystemBaseMediumLow
        /// MahApps.Colors.SystemChromeAltLow
        /// MahApps.Colors.SystemChromeBlackHigh
        /// MahApps.Colors.SystemChromeBlackLow
        /// MahApps.Colors.SystemChromeBlackMediumLow
        /// MahApps.Colors.SystemChromeBlackMedium
        /// MahApps.Colors.SystemChromeDisabledHigh
        /// MahApps.Colors.SystemChromeDisabledLow
        /// MahApps.Colors.SystemChromeHigh
        /// MahApps.Colors.SystemChromeLow
        /// MahApps.Colors.SystemChromeMedium
        /// MahApps.Colors.SystemChromeMediumLow
        /// MahApps.Colors.SystemChromeWhite
        /// MahApps.Colors.SystemChromeGray
        /// MahApps.Colors.SystemListLow
        /// MahApps.Colors.SystemListMedium
        /// MahApps.Colors.SystemErrorText
        /// 
        /// MahApps.Brushes.SystemControlBackgroundAccent
        /// MahApps.Brushes.SystemControlBackgroundAltHigh
        /// MahApps.Brushes.SystemControlBackgroundAltMedium
        /// MahApps.Brushes.SystemControlBackgroundAltMediumHigh
        /// MahApps.Brushes.SystemControlBackgroundAltMediumLow
        /// MahApps.Brushes.SystemControlBackgroundBaseHigh
        /// MahApps.Brushes.SystemControlBackgroundBaseLow
        /// MahApps.Brushes.SystemControlBackgroundBaseMedium
        /// MahApps.Brushes.SystemControlBackgroundBaseMediumHigh
        /// MahApps.Brushes.SystemControlBackgroundBaseMediumLow
        /// MahApps.Brushes.SystemControlBackgroundChromeBlackHigh
        /// MahApps.Brushes.SystemControlBackgroundChromeBlackLow
        /// MahApps.Brushes.SystemControlBackgroundChromeBlackMedium
        /// MahApps.Brushes.SystemControlBackgroundChromeBlackMediumLow
        /// MahApps.Brushes.SystemControlBackgroundChromeMedium
        /// MahApps.Brushes.SystemControlBackgroundChromeMediumLow
        /// MahApps.Brushes.SystemControlBackgroundChromeWhite
        /// MahApps.Brushes.SystemControlBackgroundListLow
        /// MahApps.Brushes.SystemControlBackgroundListMedium
        /// MahApps.Brushes.SystemControlDisabledAccent
        /// MahApps.Brushes.SystemControlDisabledBaseHigh
        /// MahApps.Brushes.SystemControlDisabledBaseLow
        /// MahApps.Brushes.SystemControlDisabledBaseMediumLow
        /// MahApps.Brushes.SystemControlDisabledChromeDisabledHigh
        /// MahApps.Brushes.SystemControlDisabledChromeDisabledLow
        /// MahApps.Brushes.SystemControlDisabledChromeHigh
        /// MahApps.Brushes.SystemControlDisabledChromeMediumLow
        /// MahApps.Brushes.SystemControlDisabledListMedium
        /// MahApps.Brushes.SystemControlDisabledTransparent
        /// MahApps.Brushes.SystemControlErrorTextForeground
        /// MahApps.Brushes.SystemControlFocusVisualPrimary
        /// MahApps.Brushes.SystemControlFocusVisualSecondary
        /// MahApps.Brushes.SystemControlForegroundAccent
        /// MahApps.Brushes.SystemControlForegroundAltHigh
        /// MahApps.Brushes.SystemControlForegroundAltMediumHigh
        /// MahApps.Brushes.SystemControlForegroundBaseHigh
        /// MahApps.Brushes.SystemControlForegroundBaseLow
        /// MahApps.Brushes.SystemControlForegroundBaseMedium
        /// MahApps.Brushes.SystemControlForegroundBaseMediumHigh
        /// MahApps.Brushes.SystemControlForegroundBaseMediumLow
        /// MahApps.Brushes.SystemControlForegroundChromeBlackHigh
        /// MahApps.Brushes.SystemControlForegroundChromeBlackMedium
        /// MahApps.Brushes.SystemControlForegroundChromeBlackMediumLow
        /// MahApps.Brushes.SystemControlForegroundChromeDisabledLow
        /// MahApps.Brushes.SystemControlForegroundChromeGray
        /// MahApps.Brushes.SystemControlForegroundChromeHigh
        /// MahApps.Brushes.SystemControlForegroundChromeMedium
        /// MahApps.Brushes.SystemControlForegroundChromeWhite
        /// MahApps.Brushes.SystemControlForegroundListLow
        /// MahApps.Brushes.SystemControlForegroundListMedium
        /// MahApps.Brushes.SystemControlForegroundTransparent
        /// MahApps.Brushes.SystemControlHighlightAccent
        /// MahApps.Brushes.SystemControlHighlightAltAccent
        /// MahApps.Brushes.SystemControlHighlightAltAltHigh
        /// MahApps.Brushes.SystemControlHighlightAltAltMediumHigh
        /// MahApps.Brushes.SystemControlHighlightAltBaseHigh
        /// MahApps.Brushes.SystemControlHighlightAltBaseLow
        /// MahApps.Brushes.SystemControlHighlightAltBaseMedium
        /// MahApps.Brushes.SystemControlHighlightAltBaseMediumHigh
        /// MahApps.Brushes.SystemControlHighlightAltBaseMediumLow
        /// MahApps.Brushes.SystemControlHighlightAltChromeWhite
        /// MahApps.Brushes.SystemControlHighlightAltListAccentHigh
        /// MahApps.Brushes.SystemControlHighlightAltListAccentLow
        /// MahApps.Brushes.SystemControlHighlightAltListAccentMedium
        /// MahApps.Brushes.SystemControlHighlightAltTransparent
        /// MahApps.Brushes.SystemControlHighlightBaseHigh
        /// MahApps.Brushes.SystemControlHighlightBaseLow
        /// MahApps.Brushes.SystemControlHighlightBaseMedium
        /// MahApps.Brushes.SystemControlHighlightBaseMediumHigh
        /// MahApps.Brushes.SystemControlHighlightBaseMediumLow
        /// MahApps.Brushes.SystemControlHighlightChromeAltLow
        /// MahApps.Brushes.SystemControlHighlightChromeHigh
        /// MahApps.Brushes.SystemControlHighlightChromeWhite
        /// MahApps.Brushes.SystemControlHighlightListAccentHigh
        /// MahApps.Brushes.SystemControlHighlightListAccentLow
        /// MahApps.Brushes.SystemControlHighlightListAccentMedium
        /// MahApps.Brushes.SystemControlHighlightListLow
        /// MahApps.Brushes.SystemControlHighlightListMedium
        /// MahApps.Brushes.SystemControlHighlightTransparent
        /// MahApps.Brushes.SystemControlHyperlinkBaseHigh
        /// MahApps.Brushes.SystemControlHyperlinkBaseMedium
        /// MahApps.Brushes.SystemControlHyperlinkBaseMediumHigh
        /// MahApps.Brushes.SystemControlHyperlinkText
        /// MahApps.Brushes.SystemControlPageBackgroundAltHigh
        /// MahApps.Brushes.SystemControlPageBackgroundAltMedium
        /// MahApps.Brushes.SystemControlPageBackgroundBaseLow
        /// MahApps.Brushes.SystemControlPageBackgroundBaseMedium
        /// MahApps.Brushes.SystemControlPageBackgroundChromeLow
        /// MahApps.Brushes.SystemControlPageBackgroundChromeMediumLow
        /// MahApps.Brushes.SystemControlPageBackgroundListLow
        /// MahApps.Brushes.SystemControlPageBackgroundMediumAltMedium
        /// MahApps.Brushes.SystemControlPageBackgroundTransparent
        /// MahApps.Brushes.SystemControlPageTextBaseHigh
        /// MahApps.Brushes.SystemControlPageTextBaseMedium
        /// MahApps.Brushes.SystemControlPageTextChromeBlackMediumLow
        /// MahApps.Brushes.SystemControlRevealFocusVisual
        /// MahApps.Brushes.SystemControlTransientBorder
        /// MahApps.Brushes.SystemControlTransparent
        /// MahApps.Brushes.SystemControlDescriptionTextForeground
        /// 
        /// 
        /// 
        /// Accent Color/Brush
        /// ===================================================================
        /// "AccentBaseColor"
        /// "AccentBaseColorBrush"
        /// "MahApps.Colors.Accent"
        /// "MahApps.Colors.Accent2"
        /// "MahApps.Colors.Accent3"
        /// "MahApps.Colors.Accent4"
        /// "MahApps.Brushes.Accent"
        /// "MahApps.Brushes.Accent2"
        /// "MahApps.Brushes.Accent3"
        /// "MahApps.Brushes.Accent4"
        /// "AccentSelectedColorBrush"
        /// "CheckmarkFill"
        /// "HighlightBrush"
        /// "HighlightColor"
        /// "MahApps.Colors.IdealForeground"
        /// "MahApps.Brushes.IdealForeground"
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

        #region Custom Colors/Brushes
        public static Color SucceedColor
        {
            get
            {
                return (Color.FromScRgb(1.0f, AccentColor.ScR, AccentColor.ScG, AccentColor.ScB));
            }
        }

        public static Brush SucceedBrush
        {
            get
            {
                return (SucceedColor.ToBrush());
            }
        }

        public static Color WarningColor
        {
            get
            {
                var color = WarningBrush.ToColor();
                return (Color.FromScRgb(1.0f, color.ScR, color.ScG, color.ScB));
            }
        }

        public static Brush WarningBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Control.Validation"] as Brush);
            }
        }

        public static Color FailedColor
        {
            get
            {
                return (Color.FromScRgb(1.0f, ErrorColor.ScR, ErrorColor.ScG, ErrorColor.ScB));
            }
        }

        public static Brush FailedBrush
        {
            get
            {
                return (FailedColor.ToBrush());
            }
        }

        public static Color ErrorColor
        {
            get
            {
                var color = ErrorBrush.ToColor();
                return (Color.FromScRgb(1.0f, color.ScR, color.ScG, color.ScB));
            }
        }

        public static Brush ErrorBrush
        {
            get
            {
                //return (ErrorColor.ToBrush());
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.SystemControlErrorTextForeground"] as Brush);
            }
        }
        #endregion

        #region ACCENT COLORS
        public static Color HighlightColor
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Color)appTheme.Resources["MahApps.Colors.Highlight"];
            }
        }

        public static Color AccentBaseColor
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Color)appTheme.Resources["MahApps.Colors.AccentBase"];
            }
        }

        public static Color AccentColor
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Color)appTheme.Resources["MahApps.Colors.Accent"];
            }
        }

        public static Color Accent2Color
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Color)appTheme.Resources["MahApps.Colors.Accent2"];
            }
        }

        public static Color Accent3Color
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Color)appTheme.Resources["MahApps.Colors.Accent3"];
            }
        }
        
        public static Color Accent4Color
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Color)appTheme.Resources["MahApps.Colors.Accent4"];
            }
        }

        public static Color IndexOfAccentColor(int index)
        {
            var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
            if (index < 1) index = 1;
            else if (index > 4) index = 4;
            if (index == 1)
                return (Color)appTheme.Resources["MahApps.Colors.Accent"];
            else
                return (Color)appTheme.Resources[$"MahApps.Colors.Accent{index}"];
        }
        #endregion

        #region BASE COLORS
        public static Color ThemeForegroundColor
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Color)appTheme.Resources["MahApps.Colors.ThemeForeground"];
            }
        }

        public static Color ThemeBackgroundColor
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Color)appTheme.Resources["MahApps.Colors.ThemeBackground"];
            }
        }

        public static Color IdealForeground
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Color)appTheme.Resources["MahApps.Colors.IdealForeground"];
            }
        }

        public static Color GrayColors(int index)
        {
            var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
            if (index < 0) index = 0;
            else if (index > 10) index = 10;
            if (index == 0)
                return (Color)appTheme.Resources["MahApps.Colors.Gray"];
            else
                return (Color)appTheme.Resources[$"MahApps.Colors.Gray{index}"];
        }

        public static Color Gray1Color
        {
            get
            {
                return (GrayColors(1));
            }
        }

        public static Color Gray2Color
        {
            get
            {
                return (GrayColors(2));
            }
        }

        public static Color Gray3Color
        {
            get
            {
                return (GrayColors(3));
            }
        }

        public static Color Gray4Color
        {
            get
            {
                return (GrayColors(4));
            }
        }

        public static Color Gray5Color
        {
            get
            {
                return (GrayColors(5));
            }
        }

        public static Color Gray6Color
        {
            get
            {
                return (GrayColors(6));
            }
        }

        public static Color Gray7Color
        {
            get
            {
                return (GrayColors(7));
            }
        }

        public static Color Gray8Color
        {
            get
            {
                return (GrayColors(8));
            }
        }

        public static Color Gray9Color
        {
            get
            {
                return (GrayColors(9));
            }
        }

        public static Color Gray10Color
        {
            get
            {
                return (GrayColors(10));
            }
        }

        public static Color GrayColor
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Colors.Gray"] as Brush).ToColor();
            }
        }

        public static Color GrayMouseOverColor
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Colors.Gray.MouseOver"] as Brush).ToColor();
            }
        }

        public static Color GraySemiTransparentColor
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Colors.Gray.SemiTransparent"] as Brush).ToColor();
            }
        }

        public static Color FlyoutColor
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Colors.Flyout"] as Brush).ToColor();
            }
        }

        public static Brush FlyoutBrush
        {
            get
            {
                return (FlyoutColor.ToBrush());
            }
        }
        #endregion

        #region Common Colors
        public static Color SystemAccentColor
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Colors.SystemAccent"] as Brush).ToColor();
            }
        }

        public static Color SystemErrorTextColor
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Colors.SystemErrorText"] as Brush).ToColor();
            }
        }
        #endregion

        #region CORE CONTROL COLORS
        public static Color ProgressIndeterminateColors(int index)
        {
            var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
            if (index < 1) index = 1;
            else if (index > 4) index = 4;
            return (Color)appTheme.Resources[$"MahApps.Colors.ProgressIndeterminate{index}"];
        }

        public static Brush ProgressIndeterminateBrushs(int index)
        {
            return ProgressIndeterminateColors(index).ToBrush();
        }

        public static Color ProgressIndeterminate1Color
        {
            get
            {
                return (ProgressIndeterminateColors(1));
            }
        }

        public static Brush ProgressIndeterminate1Brush
        {
            get
            {
                return (ProgressIndeterminateBrushs(1));
            }
        }

        public static Color ProgressIndeterminate2Color
        {
            get
            {
                return (ProgressIndeterminateColors(2));
            }
        }

        public static Brush ProgressIndeterminate2Brush
        {
            get
            {
                return (ProgressIndeterminateBrushs(2));
            }
        }

        public static Color ProgressIndeterminate3Color
        {
            get
            {
                return (ProgressIndeterminateColors(3));
            }
        }

        public static Brush ProgressIndeterminate3Brush
        {
            get
            {
                return (ProgressIndeterminateBrushs(3));
            }
        }

        public static Color ProgressIndeterminate4Color
        {
            get
            {
                return (ProgressIndeterminateColors(4));
            }
        }

        public static Brush ProgressIndeterminate4Brush
        {
            get
            {
                return (ProgressIndeterminateBrushs(4));
            }
        }
        #endregion

        #region PROJECT TEMPLATE BRUSHES

        #endregion

        #region  UNIVERSAL CONTROL BRUSHES
        public static Brush ThemeForegroundBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Brush)appTheme.Resources["MahApps.Brushes.ThemeForeground"];
            }
        }

        public static Brush ThemeBackgroundBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Brush)appTheme.Resources["MahApps.Brushes.ThemeBackground"];
            }
        }

        public static Color TextColor
        {
            get
            {
                return TextBrush.ToColor();
            }
        }

        public static Brush TextBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Brush)appTheme.Resources["MahApps.Brushes.Text"];
            }
        }

        public static Brush IdealForegroundBrush
        {
            get
            {
                //return (MahApps.Colors.IdealForeground.ToBrush());
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (Brush)appTheme.Resources["MahApps.Brushes.IdealForeground"];
            }
        }

        public static Color IdealForegroundDisableColor
        {
            get
            {
                return (IdealForegroundDisableBrush.ToColor());
            }
        }

        public static Brush IdealForegroundDisableBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return appTheme.Resources["MahApps.Brushes.IdealForegroundDisabled"] as Brush;
            }
        }

        public static Color SelectedForegroundColor
        {
            get
            {
                return (SelectedForegroundBrush.ToColor());
            }
        }

        public static Brush SelectedForegroundBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return appTheme.Resources["MahApps.Brushes.Selected.Foreground"] as Brush;
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
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.WindowTitle"] as Brush);
            }
        }

        public static Color WindowTitleNonActiveColor
        {
            get
            {
                return WindowTitleNonActiveBrush.ToColor();
            }
        }

        public static Brush WindowTitleNonActiveBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.WindowTitle.NonActive"] as Brush);
            }
        }

        public static Color BorderNonActiveColor
        {
            get
            {
                return BorderNonActiveBrush.ToColor();
            }
        }

        public static Brush BorderNonActiveBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Border.NonActive"] as Brush);
            }
        }

        public static Brush HighlightBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Highlight"] as Brush);
            }
        }

        public static Color TransparentColor
        {
            get
            {
                return (TransparentBrush.ToColor());
            }
        }

        public static Brush TransparentBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Transparent"] as Brush);
            }
        }

        public static Color SemiTransparentColor
        {
            get
            {
                return (SemiTransparentBrush.ToColor());
            }
        }

        public static Brush SemiTransparentBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.SemiTransparent"] as Brush);
            }
        }

        public static Brush AccentBaseBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.AccentBase"] as Brush);
            }
        }

        public static Brush AccentBrush
        {
            get
            {
                return (AccentBrushs(1));
            }
        }

        public static Brush Accent2Brush
        {
            get
            {
                return (AccentBrushs(2));
            }
        }

        public static Brush Accent3Brush
        {
            get
            {
                return (AccentBrushs(3));
            }
        }

        public static Brush Accent4Brush
        {
            get
            {
                return (AccentBrushs(4));
            }
        }

        public static Brush AccentBrushs(int index)
        {
            if (index < 1) index = 1;
            else if (index > 4) index = 4;
            var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
            if (index == 1)
                return (appTheme.Resources["MahApps.Brushes.Accent"] as Brush);
            else
                return (appTheme.Resources[$"MahApps.Brushes.Accent{index}"] as Brush);
        }

        public static Brush GrayBrushs(int index)
        {
            if (index < 0) index = 0;
            else if (index > 10) index = 10;
            var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
            if (index == 0)
                return (appTheme.Resources["MahApps.Brushes.Gray"] as Brush);
            else
                return (appTheme.Resources[$"MahApps.Brushes.Gray{index}"] as Brush);
        }

        public static Brush Gray1Brush
        {
            get
            {
                return (GrayBrushs(1));
            }
        }

        public static Brush Gray2Brush
        {
            get
            {
                return (GrayBrushs(2));
            }
        }

        public static Brush Gray3Brush
        {
            get
            {
                return (GrayBrushs(3));
            }
        }

        public static Brush Gray4Brush
        {
            get
            {
                return (GrayBrushs(4));
            }
        }

        public static Brush Gray5Brush
        {
            get
            {
                return (GrayBrushs(5));
            }
        }

        public static Brush Gray6Brush
        {
            get
            {
                return (GrayBrushs(6));
            }
        }

        public static Brush Gray7Brush
        {
            get
            {
                return (GrayBrushs(7));
            }
        }

        public static Brush Gray8Brush
        {
            get
            {
                return (GrayBrushs(8));
            }
        }

        public static Brush Gray9Brush
        {
            get
            {
                return (GrayBrushs(9));
            }
        }

        public static Brush Gray10Brush
        {
            get
            {
                return (GrayBrushs(10));
            }
        }

        public static Brush GrayBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Gray"] as Brush);
            }
        }

        public static Brush GrayMouseOverBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Gray.MouseOver"] as Brush);
            }
        }

        public static Brush GraySemiTransparentBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Gray.SemiTransparent"] as Brush);
            }
        }

        public static Color TextBoxBorderColor
        {
            get { return (TextBoxBorderBrush.ToColor()); }
        }

        public static Brush TextBoxBorderBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.TextBox.Border"] as Brush);
            }
        }

        public static Color TextBoxBorderFocusColor
        {
            get { return (TextBoxBorderFocusBrush.ToColor()); }
        }

        public static Brush TextBoxBorderFocusBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.TextBox.Border.Focus"] as Brush);
            }
        }

        public static Color TextBoxBorderMouseOverColor
        {
            get { return (TextBoxBorderMouseOverBrush.ToColor()); }
        }

        public static Brush TextBoxBorderMouseOverBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.TextBox.Border.MouseOver"] as Brush);
            }
        }

        public static Color ControlBackgroundColor
        {
            get { return (ControlBackgroundBrush.ToColor()); }
        }

        public static Brush ControlBackgroundBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Control.Background"] as Brush);
            }
        }

        public static Color ControlBorderColor
        {
            get { return (ControlBorderBrush.ToColor()); }
        }

        public static Brush ControlBorderBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Control.Border"] as Brush);
            }
        }

        public static Color ControlDisabledColor
        {
            get { return (ControlDisabledBrush.ToColor()); }
        }

        public static Brush ControlDisabledBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Control.Disabled"] as Brush);
            }
        }

        public static Color ControlValidationColor
        {
            get { return (ControlValidationBrush.ToColor()); }
        }

        public static Brush ControlValidationBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Control.Validation"] as Brush);
            }
        }

        public static Color ButtonBorderColor
        {
            get { return (ButtonBorderBrush.ToColor()); }
        }

        public static Brush ButtonBorderBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Button.Border"] as Brush);
            }
        }

        public static Color ButtonBorderFocusColor
        {
            get { return (ButtonBorderFocusBrush.ToColor()); }
        }

        public static Brush ButtonBorderFocusBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Button.Border.Focus"] as Brush);
            }
        }

        public static Color ButtonBorderMouseOverColor
        {
            get { return (ButtonBorderMouseOverBrush.ToColor()); }
        }

        public static Brush ButtonBorderMouseOverBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Button.Border.MouseOver"] as Brush);
            }
        }

        public static Color ComboBoxPopupBorderColor
        {
            get { return (ComboBoxPopupBorderBrush.ToColor()); }
        }

        public static Brush ComboBoxPopupBorderBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.Button.Border"] as Brush);
            }
        }

        public static Color ComboBoxBorderFocusColor
        {
            get { return (ComboBoxBorderFocusBrush.ToColor()); }
        }

        public static Brush ComboBoxBorderFocusBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.ComboBox.Border.Focus"] as Brush);
            }
        }

        public static Color ComboBoxBorderMouseOverColor
        {
            get { return (ComboBoxBorderMouseOverBrush.ToColor()); }
        }

        public static Brush ComboBoxBorderMouseOverBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.ComboBox.Border.MouseOver"] as Brush);
            }
        }


        #endregion

        public static Color WindowBackgroundColor
        {
            get
            {
                return (WindowBackgroundBrush.ToColor());
            }
        }

        public static Brush WindowBackgroundBrush
        {
            get
            {
                var appTheme = ThemeManager.Current.DetectTheme(Application.Current);
                return (appTheme.Resources["MahApps.Brushes.WindowBackground"] as Brush);
            }
        }

        public static Color WhiteColor
        {
            get
            {
                return (ThemeBackgroundColor);
            }
        }

        public static Brush WhiteBrush
        {
            get
            {
                return (ThemeBackgroundBrush);
            }
        }

        public static Color BlackColor
        {
            get
            {
                return (ThemeForegroundColor);
            }
        }

        public static Brush BlackBrush
        {
            get
            {
                return (ThemeForegroundBrush);
            }
        }

    }

}
