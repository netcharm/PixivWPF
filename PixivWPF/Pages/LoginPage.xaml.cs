﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using PixivWPF.Common;
using MahApps.Metro;

namespace PixivWPF.Pages
{
    /// <summary>
    /// PageLogin.xaml 的交互逻辑
    /// </summary>
    public partial class LoginPage : Page, IDisposable
    {
        public PixivLoginDialog ParentWindow { get; internal set; } = null;

        private Setting setting = Application.Current.LoadSetting();
        private Pixeez.Tokens tokens = null;

        private void CloseWindow()
        {
            try
            {
                LoginWait.Hide();
                if (ParentWindow is Window)
                    ParentWindow.Close();
                else
                    Application.Current.Shutdown();
            }
            catch (Exception ex) { ex.ERROR("CloseLoginDialog"); }
        }

        public LoginPage()
        {
            InitializeComponent();
        }

        private void LoginUI_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ParentWindow = Window.GetWindow(this) as PixivLoginDialog;
                setting = Application.Current.LoadSetting();

                Title = Application.Current.LoginTitle();
                WindowTitle = Application.Current.LoginTitle();

#if DEBUG
                var logo = System.IO.Path.Combine(Application.Current.GetRoot(), "Assets", "pixiv-logo.png");
                var uri = new Uri(logo);
                if (uri.IsAbsoluteUri && System.IO.File.Exists(uri.AbsolutePath))
                {
                    Logo.Source = new BitmapImage(uri);
                }
#else
                var uri = new Uri(@"pack://application:,,,/PixivWPF;component/Resources/pixiv-logo.png");
                if (uri.IsAbsoluteUri && System.IO.File.Exists(uri.AbsolutePath))
                {
                    if (Logo.Source == null) Logo.Source = new BitmapImage(uri);
                }

#endif
                new Action(() =>
                {                    
                    edUser.Text = setting.User;
                    edPass.Password = setting.Pass;
                    edProxy.Text = setting.Proxy;
                    chkUseProxy.IsChecked = setting.UsingProxy;
                    chkUseProxyDown.IsChecked = setting.DownloadUsingProxy;
                }).Invoke(async: true);
                this.DoEvents();
            }
            catch (Exception ex) { ex.ERROR("LoginLoaded"); }
            finally
            {
                if (string.IsNullOrEmpty(edUser.Text)) edUser.Focus();
                else if(string.IsNullOrEmpty(edPass.Password)) edPass.Focus();
                else btnLogin.Focus();
            }
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            if (ParentWindow == null) ParentWindow = Window.GetWindow(this) as PixivLoginDialog;

            setting = Application.Current.LoadSetting();
            Pixeez.Auth.TimeOut = setting.DownloadHttpTimeout;

            var user = edUser.Text.Trim();
            var pass = edPass.Password.Trim();
            var proxy = edProxy.Text.Trim();
            bool useproxy = chkUseProxy.IsChecked == true ? true : false;
            bool useproxydown = chkUseProxyDown.IsChecked == true ? true : false;

            if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
            {
                try
                {
                    LoginWait.Show();
                    if (ParentWindow is PixivLoginDialog)
                    {
                        //ParentWindow.DialogResult = false;
                    }

                    btnLogin.IsEnabled = false;
                    btnCancel.IsEnabled = false;
                    edUser.IsEnabled = false;
                    edPass.IsEnabled = false;
                    edProxy.IsEnabled = false;
                    chkUseProxy.IsEnabled = false;
                    chkUseProxyDown.IsEnabled = false;

                    // Create Tokens
                    var proxyserver = proxy;
                    if (!useproxy) proxyserver = string.Empty;
                    var accesstoken = Application.Current.AccessToken();
                    var refreshtoken = Application.Current.RefreshToken();

                    //var deviceId = Application.Current.GetDeviceId();
                    tokens = Pixeez.Auth.AuthorizeWithAccessToken(accesstoken, proxy, setting.ProxyBypass, useproxy);
                    var result = await Pixeez.Auth.AuthorizeAsync(user, pass, refreshtoken, proxyserver, setting.ProxyBypass, useproxy);
                    if (!string.IsNullOrEmpty(result.Authorize.AccessToken))
                    {
                        accesstoken = result.Authorize.AccessToken;
                        tokens = result.Tokens;

                        setting.AccessToken = tokens.AccessToken;
                        setting.RefreshToken = tokens.RefreshToken;
                        setting.ExpTime = result.Key.KeyExpTime.ToLocalTime();
                        setting.ExpiresIn = result.Authorize.ExpiresIn.Value;
                        setting.Update = DateTime.Now.ToFileTime().FileTimeToSecond();
                        setting.UsingProxy = useproxy;
                        setting.DownloadUsingProxy = useproxydown;
                        setting.Proxy = proxy;
                        await Task.Delay(1);
                        this.DoEvents();
                        setting.User = user;
                        setting.Pass = pass;
                        setting.MyInfo = result.Authorize.User;
                        setting.Save();
                    }

                    if (tokens is Pixeez.Tokens && !string.IsNullOrEmpty(tokens.AccessToken))
                    {
                        if (ParentWindow is PixivLoginDialog)
                        {
                            ParentWindow.AccessToken = tokens.AccessToken;
                            ParentWindow.RefreshToken = tokens.RefreshToken;
                            ParentWindow.Tokens = tokens;
                            ParentWindow.DialogResult = true;
                            CloseWindow();
                        }
                    }
                }
                catch (Exception ex)
                {
                    //await ex.Message.ShowMessageBoxAsync("ERROR");
                    ex.Message.ShowMessageBox("ERROR");
                }
                finally
                {
                    btnLogin.IsEnabled = true;
                    btnCancel.IsEnabled = true;
                    edUser.IsEnabled = true;
                    edPass.IsEnabled = true;
                    edProxy.IsEnabled = true;
                    chkUseProxy.IsEnabled = true;
                    chkUseProxyDown.IsEnabled = true;
                    LoginWait.Hide();
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            CloseWindow();
        }

        private void chkUseProxy_Clicked(object sender, RoutedEventArgs e)
        {
            setting = Application.Current.LoadSetting();
            var proxy = edProxy.Text.Trim();
            if (sender == chkUseProxy)
            {
                bool useproxy = chkUseProxy.IsChecked == true ? true : false;
                setting.UsingProxy = useproxy;
            }
            else if (sender == chkUseProxyDown)
            {
                bool useproxy = chkUseProxyDown.IsChecked == true ? true : false;
                setting.DownloadUsingProxy = useproxy;
            }
            setting.Proxy = proxy;
        }

        public void Dispose()
        {

        }
    }
}
