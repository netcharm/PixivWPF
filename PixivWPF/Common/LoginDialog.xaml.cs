using MahApps.Metro.Controls;
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
using System.Windows.Shapes;

namespace PixivWPF.Common
{
    /// <summary>
    /// LoginDialog.xaml 的交互逻辑
    /// </summary>
    public partial class PixivLoginDialog : MetroWindow
    {
        public string AccessToken = string.Empty;
        public string RefreshToken = string.Empty;
        public Pixeez.Tokens Tokens = null;

        public void UpdateTheme()
        {
            CommonHelper.UpdateTheme(this);
        }

        public PixivLoginDialog()
        {
            InitializeComponent();

            var login = new Pages.LoginPage() { ParentWindow = this };
            new Action(() =>
            {
                MinHeight = login.MinHeight;
                MaxHeight = login.MaxHeight;
                MinWidth = login.Width;
                MaxWidth = login.Width;
                Content = login;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                this.DoEvents();
            }).Invoke(async: false);

            Topmost = true;
            this.DoEvents();

            UpdateTheme();
            this.DoEvents();
        }
    }
}
