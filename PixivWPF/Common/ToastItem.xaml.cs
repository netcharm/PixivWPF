using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using MahApps.Metro.IconPacks;
using WPFNotification.Model;

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

        public bool HasImage { get; set; } = true;

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
                if(IsLoaded) SetButton(value);
            }
        }

        private CustomToast toast = null;

        public CustomButton ButtonOk;
        public CustomButton ButtonCancel;
        public CustomButton ButtonOpenFile;
        public CustomButton ButtonOpenFolder;

        private Setting setting = Application.Current.LoadSetting();

        private void SetButton(ToastType type)
        {
            try
            {
                if (IsLoaded)
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
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        private void SetState(string state, string desc)
        {
            if (IsLoaded)
            {
                #region Show Toast State Mark
                if (string.IsNullOrEmpty(state))
                {
                    State.Text = string.Empty;
                    State.Hide();
                }
                else if (state.Equals("Successed", StringComparison.CurrentCultureIgnoreCase) ||
                         state.Equals("Successes", StringComparison.CurrentCultureIgnoreCase) ||
                         state.Equals("Succeed", StringComparison.CurrentCultureIgnoreCase))
                {
                    State.Text = "\uE10B";
                    State.Foreground = Theme.AccentBrush;
                    State.Show();
                }
                else if (state.Equals("Failed", StringComparison.CurrentCultureIgnoreCase))
                {
                    State.Text = "\uE10A";
                    State.Foreground = Theme.ErrorBrush;
                    State.Show();
                }
                else
                {
                    State.Text = string.Empty;
                    State.Hide();
                }
                #endregion

                #region Show Toast State Description Mark
                if (string.IsNullOrEmpty(desc))
                {
                    StateDescription.Text = string.Empty;
                    StateDescription.Hide();
                }
                else if (desc.Equals("Public", StringComparison.CurrentCultureIgnoreCase))
                {
                    StateDescription.Text = "\uE1F7";
                    StateDescription.Foreground = State.Foreground;
                    StateDescription.Show();
                }
                else if (desc.Equals("Private", StringComparison.CurrentCultureIgnoreCase))
                {
                    StateDescription.Text = "\uE1F6";
                    StateDescription.Foreground = State.Foreground;
                    StateDescription.Show();
                }
                else
                {
                    StateDescription.Text = string.Empty;
                    StateDescription.Hide();
                }
                #endregion

                if (string.IsNullOrEmpty(state) && string.IsNullOrEmpty(desc))
                    StateMark.Hide();
                else
                    StateMark.Show();
            }
        }

        private async void CheckImageSource()
        {
            if (Preview.Tag is string)
            {
                var url = (string)(Preview.Tag);
                if (!string.IsNullOrEmpty(url))
                {
                    if (Preview.Source == null)
                    {
                        var img = await url.LoadImageFromUrl(size:Application.Current.GetDefaultThumbSize());
                        Preview.Source = img.Source;
                        img.Source = null;
                    }
                }
            }
        }

        public ToastItem()
        {
            InitializeComponent();
        }

        private void Toast_Loaded(object sender, RoutedEventArgs e)
        {
            setting = Application.Current.LoadSetting();

            try
            {
                parentWindow = Window.GetWindow(this);
                if (parentWindow is Window)
                {
                    parentWindow.Closing += Window_Closing;
                    Application.Current.AddToast(parentWindow);
                }
            }
            catch (Exception ex) { ex.ERROR(); }

            try
            {
                ButtonOk = new CustomButton() { Button = OK, Kind = ButtonOKIcon, Text = ButtonOKLabel };
                ButtonCancel = new CustomButton() { Button = CANCEL, Kind = ButtonCancelIcon, Text = ButtonCancelLabel };
                ButtonOpenFile = new CustomButton() { Button = OpenFile, Kind = ButtonOpenFileIcon, Text = ButtonOpenFileLabel };
                ButtonOpenFolder = new CustomButton() { Button = OpenFolder, Kind = ButtonOpenFolderIcon, Text = ButtonOpenFolderLabel };
            }
            catch (Exception ex) { ex.ERROR(); }

            try
            {
                if (Tag is Notification)
                {
                    var toast = Tag as Notification;
                    if (string.IsNullOrEmpty(toast.ImgURL)) Preview.Hide();
                    else Preview.Show();
                }
            }
            catch (Exception ex) { ex.ERROR(); }

            try
            {
                StateMark.Hide();
                if (Tag is CustomToast)
                {
                    toast = Tag as CustomToast;
                    SetState(toast.State, toast.StateDescription);
                }
            }
            catch (Exception ex) { ex.ERROR(); }

            try
            {
                SetButton(ItemType);
            }
            catch (Exception ex) { ex.ERROR(); }

            try
            {
                CheckImageSource();
            }
            catch (Exception ex) { ex.ERROR(); }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (parentWindow is Window)
            {
                Preview.Dispose();
                parentWindow = null;
            }
            if (Preview is Image) Preview.Source = null;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if(parentWindow == null) parentWindow = Window.GetWindow(this);
            if (parentWindow is Window) parentWindow.Close();
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
                    var FileName = (string)btn.Tag;
                    FileName.OpenFileWithShell(true);
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
                    var FileName = (string)btn.Tag;
                    FileName.OpenFileWithShell();
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
    }
}
