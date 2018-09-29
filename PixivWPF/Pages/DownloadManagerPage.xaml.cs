using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace PixivWPF.Pages
{
    /// <summary>
    /// DownloadManagerPage.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadManagerPage : Page
    {
        internal Window window = null;

        private ObservableCollection<DownloadItem> items = new ObservableCollection<DownloadItem>();
        public ObservableCollection<DownloadItem> Items
        {
            get { return items; }
        }

        public DownloadManagerPage()
        {
            InitializeComponent();
            DownloadItems.ItemsSource = items;
            //DownloadItems.Items.Refresh();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            //DownloadItems.ItemsSource = items;
            DownloadItems.Items.Refresh();
            window = Window.GetWindow(this);
        }

        public void Add(DownloadItem item)
        {
            if(item is DownloadItem)
            {
                items.Add(item);
                //item.Start();
                //DownloadItems.Items.Refresh();
            }
        }

        public async void Add(string url, string thumb, DateTime dt, bool is_meta_single_page = false, bool overwrite = true)
        {
            if (!string.IsNullOrEmpty(url))
            {
                Pixeez.Tokens tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;
                var item = new DownloadItem()
                {
                    Url = url,
                    Thumbnail = await thumb.LoadImage(tokens),
                    SingleFile = is_meta_single_page,
                    Overwrite = true,
                    FileTime = dt
                };
                Add(item);
            }
        }

        public void Refresh()
        {
            //DownloadItems.ItemsSource = items;
            DownloadItems.Items.Refresh();
        }

    }
}
