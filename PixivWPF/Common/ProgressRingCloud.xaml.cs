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
            "IsActive", typeof( bool ), typeof( ProgressRingCloud ),
            new PropertyMetadata( true, new PropertyChangedCallback(OnPropertyChanged) )
        );

        [Description("Reload Enabled State"), Category("Behavior")]
        public bool ReloadEnabled
        {
            get { return ((bool)GetValue(ReloadEnabledProperty)); }
            set { SetCurrentValue(ReloadEnabledProperty, value); }
        }
        public static readonly DependencyProperty ReloadEnabledProperty = DependencyProperty.Register(
            "ReloadEnabled", typeof( bool ), typeof( ProgressRingCloud ),
            new PropertyMetadata( false, new PropertyChangedCallback(OnPropertyChanged) )
        );

        [Description("Reload Symbol"), Category("Appearance")]
        public string ReloadSymbol
        {
            get { return ((string)GetValue(ReloadSymbolProperty)); }
            set { SetCurrentValue(ReloadSymbolProperty, value); }
        }
        public static readonly DependencyProperty ReloadSymbolProperty = DependencyProperty.Register(
            "ReloadSymbol", typeof( string ), typeof( ProgressRingCloud ),
            new PropertyMetadata( "\uE149", new PropertyChangedCallback(OnPropertyChanged) )
        );

        [Description("Reload Symbol FontFamily"), Category("Appearance")]
        public FontFamily ReloadSymbolFontFamily
        {
            get { return ((FontFamily)GetValue(ReloadSymbolFontFamilyProperty)); }
            set { SetCurrentValue(ReloadSymbolFontFamilyProperty, value); }
        }
        public static readonly DependencyProperty ReloadSymbolFontFamilyProperty = DependencyProperty.Register(
            "ReloadSymbolFontFamily", typeof( FontFamily ), typeof( ProgressRingCloud ),
            new PropertyMetadata( new FontFamily("Segoe MDL2 Assets"), new PropertyChangedCallback(OnPropertyChanged) )
        );

        [Description("Wait Symbol"), Category("Appearance")]
        public string WaitSymbol
        {
            get { return ((string)GetValue(WaitSymbolProperty)); }
            set { SetCurrentValue(WaitSymbolProperty, value); }
        }
        public static readonly DependencyProperty WaitSymbolProperty = DependencyProperty.Register(
            "WaitSymbol", typeof( string ), typeof( ProgressRingCloud ),
            new PropertyMetadata( "\uEDE4", new PropertyChangedCallback(OnPropertyChanged) )
        );

        [Description("Wait Symbol FontFamily"), Category("Appearance")]
        public FontFamily WaitSymbolFontFamily
        {
            get { return ((FontFamily)GetValue(WaitSymbolFontFamilyProperty)); }
            set { SetCurrentValue(WaitSymbolFontFamilyProperty, value); }
        }
        public static readonly DependencyProperty WaitSymbolFontFamilyProperty = DependencyProperty.Register(
            "WaitSymbolFontFamily", typeof( FontFamily ), typeof( ProgressRingCloud ),
            new PropertyMetadata( new FontFamily("Segoe MDL2 Assets"), new PropertyChangedCallback(OnPropertyChanged) )
        );

        [Description("Wait/Task State"), Category("Behavior")]
        public TaskStatus State
        {
            get { return ((TaskStatus)GetValue(StateProperty)); }
            set { SetCurrentValue(StateProperty, value); /*NotifyPropertyChanged("State");*/ }
        }
        public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
            "State", typeof( TaskStatus ), typeof( ProgressRingCloud ),
            new PropertyMetadata( TaskStatus.RanToCompletion, new PropertyChangedCallback(OnPropertyChanged))
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
            "Size", typeof( double ), typeof( ProgressRingCloud ),
            new PropertyMetadata( 64.0, new PropertyChangedCallback(OnPropertyChanged) )
        );

        [Description("Shadow Color"), Category("Appearance")]
        public Color ShadowColor
        {
            get { return ((Color)GetValue(ShadowColorProperty)); }
            set { SetCurrentValue(ShadowColorProperty, value); /*NotifyPropertyChanged("ShadowColor");*/ }
        }
        public static readonly DependencyProperty ShadowColorProperty = DependencyProperty.Register(
            "ShadowColor", typeof( Color ), typeof( ProgressRingCloud ),
            new PropertyMetadata( default(Color), new PropertyChangedCallback(OnPropertyChanged) )
        );

        [Description("Shadow Depth"), Category("Appearance")]
        public double ShadowDepth
        {
            get { return ((double)GetValue(ShadowDepthProperty)); }
            set { SetCurrentValue(ShadowDepthProperty, value); /*NotifyPropertyChanged("ShadowDepth");*/ }
        }
        public static readonly DependencyProperty ShadowDepthProperty = DependencyProperty.Register(
            "ShadowDepth", typeof( double ), typeof( ProgressRingCloud ),
            new PropertyMetadata( 0.0, new PropertyChangedCallback(OnPropertyChanged) )
        );

        [Description("Shadow Blur Radius"), Category("Appearance")]
        public double ShadowBlurRadius
        {
            get { return ((double)GetValue(ShadowBlurRadiusProperty)); }
            set { SetCurrentValue(ShadowBlurRadiusProperty, value); /*NotifyPropertyChanged("ShadowBlurRadius");*/ }
        }
        public static readonly DependencyProperty ShadowBlurRadiusProperty = DependencyProperty.Register(
            "ShadowBlurRadius", typeof( double ), typeof( ProgressRingCloud ),
            new PropertyMetadata( 3.0, new PropertyChangedCallback(OnPropertyChanged) )
        );

        [Description("Shadow Opacity"), Category("Appearance")]
        public double ShadowOpacity
        {
            get { return ((double)GetValue(ShadowOpacityProperty)); }
            set { SetCurrentValue(ShadowOpacityProperty, value); /*NotifyPropertyChanged("ShadowOpacity");*/ }
        }
        public static readonly DependencyProperty ShadowOpacityProperty = DependencyProperty.Register(
            "ShadowOpacity", typeof( double ), typeof( ProgressRingCloud ),
            new PropertyMetadata( 1.0, new PropertyChangedCallback(OnPropertyChanged) )
        );

        public bool IsShown { get { return (Visibility == Visibility.Visible); } }
        public bool IsWait { get { return (Visibility == Visibility.Visible && IsActive == true); } }
        public bool IsFail { get { return (Visibility == Visibility.Visible && IsActive == false); } }
        public bool IsReady { get { return (Visibility == Visibility.Collapsed && IsActive == false); } }
        public string PercentageTooltip
        {
            get { return (PART_Percentage.ToolTip is string ? PART_Percentage.ToolTip as string : string.Empty); }
            set { PART_Percentage.ToolTip = value; }
        }

        public ProgressRingCloud()
        {
            InitializeComponent();

            PART_Percentage.Text = string.Empty;

            ReportPercentageSlim = (value) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(PART_Mark.Text)) PART_Mark.Text = string.Empty;
                    PART_Percentage.Text = $"{ Math.Floor(Math.Max(0, value)):F0}%";
                    this.DoEvents();
                });
            };

            ReportPercentage = (value, total) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(PART_Mark.Text)) PART_Mark.Text = string.Empty;
                    var percent = value <= 0 ? 0 : value / total * 100;
                    if (value == 0 && total == 0)
                        PART_Percentage.Text = $"...";
                    else
                        PART_Percentage.Text = $"{Math.Floor(percent):F0}%";
                    this.DoEvents();
                });
            };
        }

        public void Dispose()
        {
            Hide();
        }

        private void PART_Wait_Loaded(object sender, RoutedEventArgs e)
        {
            //PART_Ring.Visibility = this.Visibility;
        }

        private void _Show_()
        {
            //lock (this)
            {
                IsActive = true;
                Visibility = Visibility.Visible;
                PART_Ring.Visibility = Visibility.Visible;
                PART_Reload.Visibility = ReloadEnabled ? Visibility.Visible : Visibility.Collapsed;
                PART_Mark.Text = WaitSymbol;
                PART_Mark.FontFamily = WaitSymbolFontFamily;
                PART_Mark.Visibility = Visibility.Visible;
                PART_Percentage.Text = string.Empty;
                PART_Percentage.Visibility = Visibility.Visible;
            }
        }

        private void _Hide_()
        {
            //lock (this)
            {
                IsActive = false;
                Visibility = Visibility.Collapsed;
                PART_Ring.Visibility = Visibility.Collapsed;
                PART_Reload.Visibility = Visibility.Collapsed;
                PART_Mark.Text = WaitSymbol;
                PART_Mark.FontFamily = WaitSymbolFontFamily;
                PART_Mark.Visibility = Visibility.Collapsed;
                PART_Percentage.Visibility = Visibility.Collapsed;
            }
        }

        private void _Disable_()
        {
            //lock (this)
            {
                IsActive = false;
                Visibility = Visibility.Visible;
                PART_Ring.Visibility = Visibility.Collapsed;
                PART_Reload.Visibility = ReloadEnabled ? Visibility.Visible : Visibility.Collapsed;
                PART_Mark.Text = ReloadEnabled ? ReloadSymbol : string.IsNullOrEmpty(PART_Percentage.Text) ? string.Empty : WaitSymbol;
                PART_Mark.FontFamily = ReloadEnabled ? ReloadSymbolFontFamily : WaitSymbolFontFamily;
                PART_Mark.Visibility = Visibility.Visible;
                PART_Percentage.Visibility = Visibility.Visible;
            }
        }

        private void _Enable_()
        {
            //lock (this)
            {
                IsActive = true;
                Visibility = Visibility.Visible;
                PART_Ring.Visibility = Visibility.Visible;
                PART_Reload.Visibility = ReloadEnabled ? Visibility.Visible : Visibility.Collapsed;
                PART_Mark.Text = ReloadEnabled ? ReloadSymbol : WaitSymbol;
                PART_Mark.FontFamily = ReloadEnabled ? ReloadSymbolFontFamily : WaitSymbolFontFamily;
                PART_Mark.Visibility = Visibility.Visible;
                PART_Percentage.Visibility = Visibility.Visible;
            }
        }

        public void Show()
        {
            State = TaskStatus.Running;
            _Show_();
        }

        public void Hide()
        {
            State = TaskStatus.RanToCompletion;
            _Hide_();
        }

        public void Fail()
        {
            State = TaskStatus.Faulted;
            _Disable_();
        }

        public void UpdateState()
        {
            switch (State)
            {
                case TaskStatus.Created:
                    _Disable_();
                    break;
                case TaskStatus.WaitingToRun:
                    _Disable_();
                    break;
                case TaskStatus.Running:
                    _Show_();
                    break;
                case TaskStatus.RanToCompletion:
                    _Hide_();
                    break;
                case TaskStatus.Canceled:
                    _Hide_();
                    break;
                default:
                    _Disable_();
                    break;
            }
        }

        public Action<double> ReportPercentageSlim { get; } = null;
        public Action<double, double> ReportPercentage { get; } = null;

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ProgressRingCloud)
            {
                var ring = d as ProgressRingCloud;
                if (e.Property.Name.Equals("State"))
                {
                    ring.UpdateState();
                }
                else if (e.Property.Name.Equals("Size"))
                {
                    ring.Width = (double)e.NewValue;
                    ring.Height = (double)e.NewValue;
                }
            }
            //NotifyPropertyChanged(e.Property.Name);
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

        private void PART_Reload_MouseEnter(object sender, MouseEventArgs e)
        {
            if (IsWait)
            {
                PART_Mark.Text = ReloadEnabled ? ReloadSymbol : string.IsNullOrEmpty(PART_Percentage.Text) ? WaitSymbol : string.Empty;
                PART_Mark.FontFamily = ReloadEnabled ? ReloadSymbolFontFamily : WaitSymbolFontFamily;
            }
        }

        private void PART_Reload_MouseLeave(object sender, MouseEventArgs e)
        {
            if (IsFail)
            {
                PART_Mark.Text = ReloadEnabled ? ReloadSymbol : string.IsNullOrEmpty(PART_Percentage.Text) ? WaitSymbol : string.Empty;
                PART_Mark.FontFamily = ReloadEnabled ? ReloadSymbolFontFamily : WaitSymbolFontFamily;
            }
            else
            {
                PART_Mark.Text = string.IsNullOrEmpty(PART_Percentage.Text) ? WaitSymbol : string.Empty;
                PART_Mark.FontFamily = WaitSymbolFontFamily;
            }
        }
    }
}
