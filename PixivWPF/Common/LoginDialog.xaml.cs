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
        }

        public PixivLoginDialog()
        {
            InitializeComponent();

            ContentFrame.Tag = this;
            ContentFrame.Content = new Pages.PageLogin() { Tag = ContentFrame };
        }
    }
}
