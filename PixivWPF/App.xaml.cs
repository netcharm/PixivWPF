using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

using PixivWPF.Common;

namespace PixivWPF
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        // Custom Submission Event handler
        private void Settings_CustomSubmissionEvent(object sender, NBug.Events.CustomSubmissionEventArgs e)
        {
            //your sumbmission code here...
            //.....
            try
            {
                this.SaveSetting(true);
            }
            catch (Exception ex) { ex.ERROR("NBug"); }
            //tell NBug if submission was successfull or not
            e.Result = true;
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                Current.StartLog();
                "------------------------ Application Starting now... ------------------------".NOTICE();

                NBug.Settings.Destinations.Add(new NBug.Core.Submission.Custom.Custom());
                NBug.Settings.CustomSubmissionEvent += Settings_CustomSubmissionEvent;

                //add handler on application load
                AppDomain.CurrentDomain.UnhandledException += NBug.Handler.UnhandledException;
                Current.DispatcherUnhandledException += NBug.Handler.DispatcherUnhandledException;

                Current.InitAppWatcher(Current.GetRoot());
                Current.BindingHotkeys();
            }
            catch(Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
            finally
            {
                var setting = this.LoadSetting(true);
                if (setting.SingleInstance && Current.Activate())
                {
                    var checkinstance = true;
                    if (e.Args.Length > 0)
                    {
                        foreach (var arg in e.Args)
                        {
                            var param = new string[] { "nocheckinstance", "restart"};
                            if (param.Contains(arg.Trim('"').ToLower()))
                            {
                                checkinstance = false;
                                break;
                            }
                        }
                    }
                    if (checkinstance) Current.Shutdown(-1);
                }
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            try
            {
                "======================== Application Shutdown now... ========================".NOTICE();
                this.SaveSetting(true);
            }
            catch (Exception ex) { ex.ERROR("APP_EXIT"); }
            finally
            {
                Current.StopLog();
            }
        }

        #region MenuItem with Slider
        public class MenuItemSliderData
        {
            public string ToolTip { get; set; } = string.Empty;
            public int Max { get; set; } = 100;
            public int Min { get; set; } = 0;
            public int Value { get; set; } = 0;
        }

        private void MenuItemSlider_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                if (sender is Slider)
                {
                    var slider = sender as Slider;
                    slider.Dispatcher.InvokeAsync(() =>
                    {                        
                        var value = slider.Value;
                        var m = value % slider.LargeChange;
                        var offset = 0.0;
                        if (e.Delta < 0)
                        {
                            m = m == 0 ? slider.LargeChange : slider.LargeChange - m;
                            offset = value + m;
                        }
                        else if (e.Delta > 0)
                        {
                            m = m == 0 ? slider.LargeChange : m;
                            offset = value - m;
                        }
                        slider.Value = offset;
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"{e.Delta}, {m}, {offset}");
#endif
                    });
                }
            }
            catch (Exception ex) { ex.ERROR("MenuItemSlider"); }
        }

        private void MenuItemSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                Grid MenuPanel = null;
                Slider MenuSlider = null;
                TextBlock MenuValue = null;

                Slider slider = sender is Slider ? sender as Slider : null;
                MenuItem menuitem = slider is Slider ? slider.TemplatedParent as MenuItem : null;
                if (slider is Slider && menuitem is MenuItem)
                {
                    slider.Dispatcher.InvokeAsync(() =>
                    {
                        var obj = TryFindResource("MenuItemWithSlider");
                        if (obj is ControlTemplate)
                        {
                            var template = obj as ControlTemplate;
                            var m_panel = template.FindName("PART_SliderPanel", menuitem);
                            var m_slider = template.FindName("PART_Slider", menuitem);
                            var m_label =  template.FindName("PART_SliderLabel", menuitem);
                            var m_value =  template.FindName("PART_SliderValue", menuitem);

                            if (m_panel is Grid) MenuPanel = m_panel as Grid;
                            if (m_slider is Slider) MenuSlider = m_slider as Slider;
                            if (m_value is TextBlock) MenuValue = m_value as TextBlock;
                        }

                        if (MenuSlider is Slider && MenuValue is TextBlock && MenuPanel is Grid)
                        {
                            var value = Convert.ToInt32(MenuSlider.Value);

                            if (menuitem.Tag is MenuItemSliderData)
                            {
                                var data = menuitem.Tag as MenuItemSliderData;
                                data.Value = value;
                                (menuitem as MenuItem).ToolTip = $"{string.Format(data.ToolTip, data.Value)}";
                                MenuSlider.ToolTip = $"{string.Format(data.ToolTip, data.Value)}";
                                menuitem.Tag = data;
                            }
                            else MenuSlider.ToolTip = $"{ value }";

                            MenuValue.Text = $"{ value }";
                            MenuValue.ToolTip = MenuSlider.ToolTip;
                            menuitem.ToolTip = MenuSlider.ToolTip;
                        }
                    });
                }
            }
            catch (Exception ex) { ex.ERROR("MenuItemSlider"); }
        }

        private void MenuItemSlider_TargetUpdated(object sender, System.Windows.Data.DataTransferEventArgs e)
        {
            if (sender is Slider && (sender as Slider).Tag != null && e.Property.Name.Equals("Tag"))
            {
                Slider slider = sender is Slider ? sender as Slider : null;
                if (slider.Tag is MenuItemSliderData)
                {
                    slider.Dispatcher.Invoke(() => 
                    {
                        var data = slider.Tag as MenuItemSliderData;
                        slider.Value = data.Value;
                        slider.Minimum = data.Min;
                        slider.Maximum = data.Max;
                    });
                }
            }
        }
        #endregion
    }
}
