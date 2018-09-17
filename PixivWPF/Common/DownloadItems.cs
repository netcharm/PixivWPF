using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PixivWPF.Common
{
    class DownloadItem
    {
        public bool UsingProxy { get; set; }
        public string Proxy { get; set; }

        public Uri uri { get; set; }
        public string LocalPath { get; set; }

        public double progress { get; }

        public DownloadItem(string url, string filename)
        {
            uri = new Uri(url);
            LocalPath = filename;
        }
    }
}
