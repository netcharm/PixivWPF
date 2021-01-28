using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

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
            catch (Exception) { }
            //tell NBug if submission was successfull or not
            e.Result = true;
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                NBug.Settings.Destinations.Add(new NBug.Core.Submission.Custom.Custom());
                NBug.Settings.CustomSubmissionEvent += Settings_CustomSubmissionEvent;

                //add handler on application load
                AppDomain.CurrentDomain.UnhandledException += NBug.Handler.UnhandledException;
                Current.DispatcherUnhandledException += NBug.Handler.DispatcherUnhandledException;

                Current.InitAppWatcher(Current.GetRoot());
                Current.BindHotkeys();
            }
            catch(Exception ex) { ex.Message.ShowMessageBox("ERROR"); }
            finally
            {
                var setting = this.LoadSetting(true);
                if (setting.SingleInstance && Current.Activate()) Current.Shutdown(-1);
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            try
            {
                this.SaveSetting(true);
                Current.UnbindHotkeys();
            }
            catch (Exception) { }
        }
    }
}
