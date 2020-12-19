using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PixivWPF.Common
{
    /// <summary>
    /// ProgressRingCloud.xaml 的交互逻辑
    /// </summary>
    public partial class ProgressRingCloud : UserControl, INotifyPropertyChanged
    {
        [Description("Active State"), Category("Behavior")]
        public bool IsActive
        {
            get { return ((bool)GetValue(IsActiveProperty)); }
            set { SetCurrentValue(IsActiveProperty, value); /*NotifyPropertyChanged("IsActive");*/ }
        }
        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
            "IsActive", typeof( bool ), typeof( ProgressRingCloud ), new PropertyMetadata( true )
        );

        [Description("Reload Enabled State"), Category("Behavior")]
        public bool ReloadEnabled
        {
            get { return ((bool)GetValue(ReloadEnabledProperty)); }
            set { SetCurrentValue(ReloadEnabledProperty, value); }
        }
        public static readonly DependencyProperty ReloadEnabledProperty = DependencyProperty.Register(
            "ReloadEnabled", typeof( bool ), typeof( ProgressRingCloud ), new PropertyMetadata( false )
        );
        public Action ReloadAction { get; set; } = null;

        public event RoutedEventHandler ReloadClick
        {
            //add { PART_Reload.AddHandler(ButtonBase.ClickEvent, value); }
            //remove { PART_Reload.RemoveHandler(ButtonBase.ClickEvent, value); }
            add { AddHandler(ButtonBase.ClickEvent, value); }
            remove { RemoveHandler(ButtonBase.ClickEvent, value); }
        }
        public static readonly RoutedEvent ReloadClickEvent = ButtonBase.ClickEvent.AddOwner(typeof(ProgressRingCloud));

        [Localizability(LocalizationCategory.None, Readability = Readability.Readable)]
        [TypeConverter(typeof(LengthConverter))]
        [Description("Size"), Category("Layout")]
        public double Size
        {
            get { return ((double)GetValue(SizeProperty)); }
            set
            {
                SetCurrentValue(SizeProperty, value);
                SetCurrentValue(WidthProperty, value);
                SetCurrentValue(HeightProperty, value);
                //Width = value;
                //Height = value;
                //NotifyPropertyChanged("Size");
            }
        }
        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            "Size", typeof( double ), typeof( ProgressRingCloud ), new PropertyMetadata( 64.0 )
        );

        [Description("Shadow Color"), Category("Appearance")]
        public Color ShadowColor
        {
            get { return ((Color)GetValue(ShadowColorProperty)); }
            set { SetCurrentValue(ShadowColorProperty, value); /*NotifyPropertyChanged("ShadowColor");*/ }
        }
        public static readonly DependencyProperty ShadowColorProperty = DependencyProperty.Register(
            "ShadowColor", typeof( Color ), typeof( ProgressRingCloud ), new PropertyMetadata( default(Color) )
        );

        [Description("Shadow Depth"), Category("Appearance")]
        public double ShadowDepth
        {
            get { return ((double)GetValue(ShadowDepthProperty)); }
            set { SetCurrentValue(ShadowDepthProperty, value); /*NotifyPropertyChanged("ShadowDepth");*/ }
        }
        public static readonly DependencyProperty ShadowDepthProperty = DependencyProperty.Register(
            "ShadowDepth", typeof( double ), typeof( ProgressRingCloud ), new PropertyMetadata( 0.0 )
        );

        [Description("Shadow Blur Radius"), Category("Appearance")]
        public double ShadowBlurRadius
        {
            get { return ((double)GetValue(ShadowBlurRadiusProperty)); }
            set { SetCurrentValue(ShadowBlurRadiusProperty, value); /*NotifyPropertyChanged("ShadowBlurRadius");*/ }
        }
        public static readonly DependencyProperty ShadowBlurRadiusProperty = DependencyProperty.Register(
            "ShadowBlurRadius", typeof( double ), typeof( ProgressRingCloud ), new PropertyMetadata( 5.0 )
        );

        [Description("Shadow Opacity"), Category("Appearance")]
        public double ShadowOpacity
        {
            get { return ((double)GetValue(ShadowOpacityProperty)); }
            set { SetCurrentValue(ShadowOpacityProperty, value); /*NotifyPropertyChanged("ShadowOpacity");*/ }
        }
        public static readonly DependencyProperty ShadowOpacityProperty = DependencyProperty.Register(
            "ShadowOpacity", typeof( double ), typeof( ProgressRingCloud ), new PropertyMetadata( 1.0 )
        );

        public bool IsShown { get { return (Visibility == Visibility.Visible); } }
        public bool IsWait { get { return (Visibility == Visibility.Visible && IsActive == true); } }
        public bool IsFail { get { return (Visibility == Visibility.Visible && IsActive == false); } }
        public bool IsReady { get { return (Visibility == Visibility.Collapsed && IsActive == false); } }

        public ProgressRingCloud()
        {
            InitializeComponent();
        }

        private void WaitRing_Loaded(object sender, RoutedEventArgs e)
        {
            //PART_Ring.Visibility = this.Visibility;
        }

        public void Show()
        {
            lock (this)
            {
                IsActive = true;
                //IsEnabled = true;
                Visibility = Visibility.Visible;
                PART_Reload.Visibility = Visibility.Collapsed;
                PART_Mark.Text = "\uEDE4";
            }
        }

        public void Hide()
        {
            lock (this)
            {
                IsActive = false;
                //IsEnabled = false;
                Visibility = Visibility.Collapsed;
                PART_Reload.Visibility = Visibility.Collapsed;
                PART_Mark.Text = "\uEDE4";
            }
        }

        public void Disable()
        {
            lock (this)
            {
                IsActive = false;
                //IsEnabled = false;
                Visibility = Visibility.Visible;
                PART_Reload.Visibility = ReloadEnabled ? Visibility.Visible : Visibility.Collapsed;
                PART_Mark.Text = ReloadEnabled ? "\uE149" : "\uEDE4";
            }
        }

        public void Wait()
        {
            Show();
        }

        public void Ready()
        {
            Hide();
        }

        public void Fail()
        {
            Disable();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void PART_Reload_Click(object sender, RoutedEventArgs e)
        {
            if (ReloadEnabled && ReloadAction is Action)
            {
                e.Handled = true;
                await ReloadAction.InvokeAsync();
            }
        }
    }
}
