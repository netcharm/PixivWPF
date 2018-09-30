using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
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

        [DefaultValue(true)]
        public bool AutoStart { get; set; }

        private ObservableCollection<DownloadInfo> items = new ObservableCollection<DownloadInfo>();
        public ObservableCollection<DownloadInfo> Items
        {
            get { return items; }
        }

        public DownloadManagerPage()
        {
            InitializeComponent();
            DataContext = this;

            DownloadItems.ItemsSource = items;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            window = Window.GetWindow(this);
        }

        public void Add(DownloadInfo item)
        {
            if(item is DownloadInfo)
            {
                items.Add(item);
                //items.Insert(0, item);
            }
        }

        public async void Add(string url, string thumb, DateTime dt, bool is_meta_single_page = false, bool overwrite = true)
        {
            if (!string.IsNullOrEmpty(url))
            {
                Pixeez.Tokens tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;
                var item = new DownloadInfo()
                {
                    AutoStart = AutoStart,
                    Url = url,
                    Thumbnail = await thumb.LoadImage(tokens),
                    SingleFile = is_meta_single_page,
                    Overwrite = overwrite,
                    FileTime = dt
                };
                Add(item);
            }
        }

        public void Refresh()
        {
            DownloadItems.Items.Refresh();
        }

        public void Start()
        {
            foreach(var item in items)
            {
                //this.button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                //item.PART_Download.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                //item.Start();
                //item.IsDownloading = true;
            }
        }

        private void DownloadAll_Click(object sender, RoutedEventArgs e)
        {
            new Thread(() =>
            {
                var needUpdate = items.Where(item => item.State != DownloadState.Downloading && item.State != DownloadState.Finished );
                if (needUpdate.Count() > 0)
                {
                    using (DownloadItems.Items.DeferRefresh())
                    {
                        var opt = new ParallelOptions();
                        opt.MaxDegreeOfParallelism = 5;
                        var ret = Parallel.ForEach(needUpdate, opt, (item, loopstate, elementIndex) =>
                        {
                            item.IsStart = true;
                        });
                    }
                }
            }).Start();
        }
    }
}
