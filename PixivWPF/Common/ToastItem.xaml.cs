using MahApps.Metro.IconPacks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace PixivWPF.Common
{
    public partial class CustomButton
    {
        [Description("Get or Set Button Text")]
        [Category("Common Properties")]
        [DefaultValue("OK")]
        public string Label
        {
            get { return (Text is TextBlock ? Text.Text : string.Empty); }
            set { if(Text is TextBlock) Text.Text = value; }
        }

        [Description("Get or Set Button Icon")]
        [Category("Common Properties")]
        [DefaultValue("OK")]
        public string IconKind
        {
            get { return(Kind is PackIconModern ? Kind.Kind.ToString() : string.Empty); }
            set
            {
                if (Kind is PackIconModern)
                {
                    PackIconModernKind kind = PackIconModernKind.Check;
                    Enum.TryParse(value, true, out kind);
                    Kind.Kind = kind;
                }
            }
        }

        [Description("Get or Set Button Visible")]
        [Category("Common Properties")]
        [DefaultValue(true)]
        public bool Visiable
        {
            get { return (Button is Button ? (Button.Visibility == Visibility.Visible ? true : false) : false); }
            set
            {
                if (Button is Button)
                {
                    if (value) Button.Visibility = Visibility.Visible;
                    else Button.Visibility = Visibility.Collapsed;
                }
            }
        }

        public Button Button { get; set; }
        public PackIconModern Kind { get; set; }
        public TextBlock Text { get; set; }
    }

    /// <summary>
    /// NotificationItem.xaml 的交互逻辑
    /// </summary>
    public partial class ToastItem : UserControl
    {
        private Window parentWindow = null;

        [Description("Get or Set Toast Type")]
        [Category("Common Properties")]
        [DefaultValue(ToastType.OK)]
        private ToastType toasttype = ToastType.OK;
        public ToastType ItemType
        {
            get { return (toasttype); }
            set
            {
                toasttype = value;
                SetButton(value);
            }
        }

        public CustomButton ButtonOk;
        public CustomButton ButtonCancel;
        public CustomButton ButtonOpenFile;
        public CustomButton ButtonOpenFolder;

        public ToastItem()
        {
            InitializeComponent();

            ButtonOk = new CustomButton() { Button = OK, Kind = ButtonOKIcon, Text = ButtonOKLabel };
            ButtonCancel = new CustomButton() { Button = CANCEL, Kind = ButtonCancelIcon, Text = ButtonCancelLabel };
            ButtonOpenFile = new CustomButton() { Button = OpenFile, Kind = ButtonOpenFileIcon, Text = ButtonOpenFileLabel };
            ButtonOpenFolder = new CustomButton() { Button = OpenFolder, Kind = ButtonOpenFolderIcon, Text = ButtonOpenFolderLabel };

            //ItemType = ToastType.OK;
        }

        private void SetButton(ToastType type)
        {
            switch (type)
            {
                case ToastType.DOWNLOAD:
                    ButtonOk.Visiable = false;
                    ButtonCancel.Visiable = false;
                    ButtonOpenFile.Visiable = true;
                    ButtonOpenFolder.Visiable = true;
                    break;
                case ToastType.OK:
                    ButtonOk.Visiable = true;
                    ButtonCancel.Visiable = false;
                    ButtonOpenFile.Visiable = false;
                    ButtonOpenFolder.Visiable = false;
                    break;
                case ToastType.OKCANCEL:
                    ButtonOk.Visiable = true;
                    ButtonCancel.Visiable = true;
                    ButtonOpenFile.Visiable = false;
                    ButtonOpenFolder.Visiable = false;
                    break;
                case ToastType.YES:
                    ButtonOk.Label = "Yes";
                    ButtonOk.Visiable = true;
                    ButtonCancel.Visiable = false;
                    ButtonOpenFile.Visiable = false;
                    ButtonOpenFolder.Visiable = false;
                    break;
                case ToastType.YESNO:
                    ButtonOk.Label = "Yes";
                    ButtonOk.Visiable = true;
                    ButtonCancel.Label = "No";
                    ButtonCancel.Visiable = true;
                    ButtonOpenFile.Visiable = false;
                    ButtonOpenFolder.Visiable = false;
                    break;
                default:
                    ButtonOk.Visiable = true;
                    ButtonCancel.Visiable = false;
                    ButtonOpenFile.Visiable = false;
                    ButtonOpenFolder.Visiable = false;
                    break;
            }

        }

        private async void CheckImageSource()
        {
            if (Preview.Tag is string)
            {
                var file = (string)(Preview.Tag);
                if (!string.IsNullOrEmpty(file))
                {
                    if (Preview.Source == null)
                        Preview.Source = await file.GetFilePath().LoadImageFromFile();
                    if (Preview.Source == null)
                        Preview.Source = await file.LoadImageFromUrl();
                    if (Preview.Source == null)
                    {
                        Preview.Visibility = Visibility.Collapsed;
                        OpenPanel.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        Preview.Visibility = Visibility.Visible;
                        OpenPanel.Visibility = Visibility.Visible;                       
                    }
                }
            }
        }

        private void Toast_Loaded(object sender, RoutedEventArgs e)
        {
            parentWindow = Window.GetWindow(this);
            if (parentWindow is Window)
                parentWindow.Closing += Window_Closing;
            CheckImageSource();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if(parentWindow == null) parentWindow = Window.GetWindow(this);
            this.Visibility = Visibility.Hidden;
            Preview.Source = null;
            parentWindow.Close();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            CloseButton_Click(sender, e);
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button)
            {
                var btn = sender as Button;
                if (btn.Tag is string)
                {
                    var image = System.IO.Path.GetDirectoryName((string)btn.Tag);
                    if (System.IO.Directory.Exists(image))
                    {
                        System.Diagnostics.Process.Start(image);
                    }
                }
            }
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if(sender is Button)
            {
                var btn = sender as Button;
                if(btn.Tag is string)
                {
                    var image = (string)btn.Tag;
                    if (System.IO.File.Exists(image))
                    {
                        System.Diagnostics.Process.Start(image);
                    }
                }
            }            
        }

        private void Preview_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if(sender == Preview)
            {
                if(e.Property.Name.Equals("Source", StringComparison.CurrentCultureIgnoreCase) ||
                   e.Property.Name.Equals("Tag", StringComparison.CurrentCultureIgnoreCase))
                {
                    CheckImageSource();
                }
            }
            else if(sender == border)
            {
                if (e.Property.Name.Equals("Tag", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (border.Tag is ToastType)
                    {
                        ItemType = (ToastType)(border.Tag);
                        CheckImageSource();
                    }
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Preview.Source = null;
//            e.Cancel = 
        }

    }
}
