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
        string AppPath = System.IO.Path.GetDirectoryName(Application.ResourceAssembly.CodeBase.ToString());
        MetroWindow window = (Application.Current.MainWindow as MetroWindow);

        public PageLogin()
        {
            InitializeComponent();

            Tuple<AppTheme, Accent> appStyle = ThemeManager.DetectAppStyle(Application.Current);
            var appTheme = appStyle.Item1;
            var appAccent = appStyle.Item2;

            headerUser.Foreground = appAccent.Resources["AccentColorBrush"] as Brush;
            headerPass.Foreground = headerUser.Foreground;
            headerProxy.Foreground = headerUser.Foreground;
            chkUseProxy.Foreground = headerUser.Foreground;


            var logo = System.IO.Path.Combine(AppPath, "Assets", "pixiv-logo.png");
            var uri = new Uri(logo);
            if (uri.IsAbsoluteUri && System.IO.File.Exists(uri.AbsolutePath))
            {
                Logo.Source = new BitmapImage(uri);
            }

            var setting = Setting.Load();
            edProxy.Text = setting.Proxy;
            chkUseProxy.IsChecked = setting.UsingProxy;
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
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
                    
                    var tokens = string.IsNullOrEmpty(accesstoken) ? await Pixeez.Auth.AuthorizeAsync(user, pass, proxyserver, useproxy) : Pixeez.Auth.AuthorizeWithAccessToken(accesstoken, proxy, useproxy);

                    if(!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
                    {
                        var setting = Setting.Load();
                        setting.AccessToken = tokens.AccessToken;
                        setting.Proxy = proxy;
                        setting.UsingProxy = useproxy;
                        setting.Save();
                    }

                    if(tokens is Pixeez.Tokens && !string.IsNullOrEmpty(tokens.AccessToken))
                    {
                        if(Tag is Frame)
                        {
                            var frame = Tag as Frame;
                            if(frame.Tag is Common.LoginDialog)
                            {
                                var win = frame.Tag as Common.LoginDialog;
                                win.AccessToken = tokens.AccessToken;
                                win.Close();
                            }
                        }
                    }
                    //var works = await tokens.GetWorksAsync(51796422);
                    //var users = await tokens.GetUsersAsync(11972);
                }
                catch (Exception ex)
                {
                    await window.ShowMessageAsync("Error", ex.Message);
                }
            }
        }
    }
}
