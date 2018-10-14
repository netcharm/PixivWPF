using Microsoft.Win32;
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

namespace PixivWPF.Pages
{
    /// <summary>
    /// DownloadManagerPage.xaml 的交互逻辑
    /// </summary>
    public partial class DownloadManagerPage : Page
    {
        internal Window window = null;
        internal Setting setting = Setting.Load();

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
                var Canceled = false;
                if (string.IsNullOrEmpty(setting.LastFolder))
                {
                    if (!string.IsNullOrEmpty(url))
                    {
                        SaveFileDialog dlgSave = new SaveFileDialog();
                        var file = url.GetImageName(is_meta_single_page);
                        dlgSave.FileName = file;
                        if (dlgSave.ShowDialog() == true)
                        {
                            file = dlgSave.FileName;
                            setting.LastFolder = System.IO.Path.GetDirectoryName(file);
                        }
                        else
                        {
                            Canceled = true;
                            return;
                        }
                    }
                }
                if (Canceled) return;

                Pixeez.Tokens tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return;
                var item = new DownloadInfo()
                {
                    AutoStart = AutoStart,
                    SingleFile = is_meta_single_page,
                    Overwrite = overwrite,
                    Thumbnail = await thumb.LoadImage(tokens),
                    Url = url,
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

        private void DownloadItem_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            if (e.Property != null && (e.Property.Name == "Tag" || e.Property.Name == "Value"))
            {
                var idle = items.Where(o => o.State == DownloadState.Idle );
                var downloading = items.Where(o => o.State == DownloadState.Downloading );
                var failed = items.Where(o => o.State == DownloadState.Failed );
                var finished = items.Where(o => o.State == DownloadState.Finished );

                PART_DownloadState.Text = $"Total: {items.Count()}, Downloading: {downloading.Count()}, Finished: {finished.Count()}, Failed: {failed.Count()}";
            }
        }
    }
}
