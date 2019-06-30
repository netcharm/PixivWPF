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

namespace PixivWPF.Common
{
    /// <summary>
    /// DateTimePicker.xaml 的交互逻辑
    /// </summary>
    public partial class DateTimePicker : Page
    {
        public DateTimePicker()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            Width = DatePicker.ActualWidth;
            Height = DatePicker.ActualHeight;

            var win = this.GetActiveWindow();
            if(win is Window)
            {
                win.Width = Width;
                win.Height = Height;
            }

            DatePicker.DisplayMode = CalendarMode.Month;
            DatePicker.FirstDayOfWeek = DayOfWeek.Monday;
            DatePicker.IsTodayHighlighted = true;
            DatePicker.SelectedDate = CommonHelper.SelectedDate;
            DatePicker.DisplayDate = CommonHelper.SelectedDate;
            DatePicker.DisplayDateStart = new DateTime(2007, 09, 11);
            DatePicker.DisplayDateEnd = DateTime.Now;

            DatePicker.Language = System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentCulture.IetfLanguageTag);
            //DatePicker.Culture = System.Globalization.CultureInfo.CurrentCulture;
        }

        private void CommandDatePicker_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if(DatePicker.SelectedDate.HasValue && DatePicker.SelectedDate.Value<=DateTime.Now)
                CommonHelper.SelectedDate = DatePicker.SelectedDate.Value;
        }

        private void DatePicker_SelectedDateChanged(object sender, MahApps.Metro.Controls.TimePickerBaseSelectionChangedEventArgs<DateTime?> e)
        {
            if (DatePicker.SelectedDate.HasValue && DatePicker.SelectedDate.Value <= DateTime.Now)
                CommonHelper.SelectedDate = DatePicker.SelectedDate.Value;
        }

        private void DatePicker_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0) DatePicker.DisplayDate -= TimeSpan.FromDays(30);
            else if (e.Delta < 0) DatePicker.DisplayDate += TimeSpan.FromDays(30);
        }

        private void DatePicker_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (DatePicker.SelectedDate.HasValue && DatePicker.SelectedDate.Value <= DateTime.Now)
                {
                    CommonHelper.SelectedDate = DatePicker.SelectedDate.Value;
                    this.GetActiveWindow().Close();
                }
            }
            catch (Exception) { }
        }

        internal void Page_KeyUp(object sender, KeyEventArgs e)
        {
            if (this.Content is Pages.DownloadManagerPage) return;
            if (this.Tag is Pages.DownloadManagerPage) return;

            e.Handled = true;
            if (e.Key == Key.Escape) this.GetActiveWindow().Close();
        }

        internal void Page_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton1)
            {
                e.Handled = true;
                this.GetActiveWindow().Close();
            }
        }
    }
}
