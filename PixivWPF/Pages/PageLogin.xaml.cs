using System;
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
    public partial class PageLogin : Page
    {
        private Window window = null;
        private string AppPath = System.IO.Path.GetDirectoryName(Application.ResourceAssembly.CodeBase.ToString());
        private Setting setting = Setting.Load();
        private Pixeez.Tokens tokens = null;

        private void CloseWindow()
        {
            if (Tag is Frame)
            {
                var frame = Tag as Frame;
                if (frame.Tag is PixivLoginDialog)
                {
                    var win = frame.Tag as PixivLoginDialog;
                    win.Close();
                }
            }
            else
            {
                //Application.Current.MainWindow.Close();
                Application.Current.Shutdown();
            }
        }

        public PageLogin()
        {
            InitializeComponent();

            window = this.GetActiveWindow();

            Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
            var appTheme = appStyle.Item1;
            var appAccent = appStyle.Item2;

            //headerUser.Foreground = appAccent.Resources["AccentColorBrush"] as Brush;
            //headerPass.Foreground = headerUser.Foreground;
            //headerProxy.Foreground = headerUser.Foreground;
            //chkUseProxy.Foreground = headerUser.Foreground;


            var logo = System.IO.Path.Combine(AppPath, "Assets", "pixiv-logo.png");
            var uri = new Uri(logo);
            if (uri.IsAbsoluteUri && System.IO.File.Exists(uri.AbsolutePath))
            {
                Logo.Source = new BitmapImage(uri);
            }

            edUser.Focus();

            edUser.Text = setting.User;
            edPass.Password = setting.Pass;
            edProxy.Text = setting.Proxy;
            chkUseProxy.IsChecked = setting.UsingProxy;
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            if(window == null) window = this.GetActiveWindow();

            btnLogin.IsEnabled = false;
            btnCancel.IsEnabled = false;
            edUser.IsEnabled = false;
            edPass.IsEnabled = false;
            edProxy.IsEnabled = false;
            chkUseProxy.IsEnabled = false;

            var user = edUser.Text.Trim();
            var pass = edPass.Password.Trim();
            var proxy = edProxy.Text.Trim();
            bool useproxy = chkUseProxy.IsChecked == true ? true : false;

            if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
            {
                try
                {
                    // Create Tokens
                    var proxyserver = proxy;
                    if (!useproxy) proxyserver = string.Empty;
                    var accesstoken = Setting.Token();

                    tokens = Pixeez.Auth.AuthorizeWithAccessToken(accesstoken, proxy, useproxy);
                    var deviceId = Setting.GetDeviceId();
                    var result = await Pixeez.Auth.AuthorizeAsync(user, pass, null, proxyserver, useproxy);
                    //var result = await Auth.AuthorizeAsync(user, pass, accesstoken, deviceId, proxyserver, useproxy);
                    if (!string.IsNullOrEmpty(result.Authorize.AccessToken))
                    {
                        accesstoken = result.Authorize.AccessToken;
                        tokens = result.Tokens;

                        setting.User = user;
                        setting.Pass = pass;
                        setting.AccessToken = tokens.AccessToken;
                        setting.RefreshToken = tokens.RefreshToken;
                        setting.Proxy = proxy;
                        setting.UsingProxy = useproxy;
                        setting.MyInfo = result.Authorize.User;
                        setting.Update = Convert.ToInt64(DateTime.Now.ToFileTime() / 10000000);
                        setting.Save();
                    }

                    if(tokens is Pixeez.Tokens && !string.IsNullOrEmpty(tokens.AccessToken))
                    {
                        if(Tag is Frame)
                        {
                            var frame = Tag as Frame;
                            if(frame.Tag is PixivLoginDialog)
                            {
                                var win = frame.Tag as PixivLoginDialog;
                                win.AccessToken = tokens.AccessToken;
                                win.Tokens = tokens;
                                win.Close();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await ex.Message.ShowMessageBoxAsync("ERROR");
                    CloseWindow();
                }
            }
            btnLogin.IsEnabled = true;
            btnCancel.IsEnabled = true;
            edUser.IsEnabled = true;
            edPass.IsEnabled = true;
            edProxy.IsEnabled = true;
            chkUseProxy.IsEnabled = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            CloseWindow();
        }
    }
}
