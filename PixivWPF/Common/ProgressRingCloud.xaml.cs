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

        [Localizability(LocalizationCategory.None, Readability = Readability.Unreadable)]
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
                IsEnabled = true;
                Visibility = Visibility.Visible;
            }
        }

        public void Hide()
        {
            lock (this)
            {
                IsActive = false;
                IsEnabled = false;
                Visibility = Visibility.Collapsed;
            }
        }

        public void Disable()
        {
            lock (this)
            {
                IsActive = false;
                IsEnabled = false;
                Visibility = Visibility.Visible;
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

    }
}
