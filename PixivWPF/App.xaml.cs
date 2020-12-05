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
        void Settings_CustomSubmissionEvent(object sender, NBug.Events.CustomSubmissionEventArgs e)
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
            var setting = this.LoadSetting(true);

            //add handler on application load
            NBug.Settings.CustomSubmissionEvent += Settings_CustomSubmissionEvent;
            AppDomain.CurrentDomain.UnhandledException += NBug.Handler.UnhandledException;
            Application.Current.DispatcherUnhandledException += NBug.Handler.DispatcherUnhandledException;
        }    
    }
}
