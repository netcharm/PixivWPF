using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using Newtonsoft.Json;
using PixivWPF.Common;

namespace PixivWPF.Common
{
    class CacheImage
    {
        private Setting setting = Application.Current.LoadSetting();
        private char[] trimchars = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        private ConcurrentDictionary<int, string> loadedImageHashTable = new ConcurrentDictionary<int, string>();
        private ConcurrentDictionary<int, string> loadedImageFileTable = new ConcurrentDictionary<int, string>();

        private ConcurrentDictionary<string, string> _caches = new ConcurrentDictionary<string, string>();
        private string _CacheFolder = string.Empty;

        public CacheImage()
        {
            setting = Application.Current.LoadSetting();
            _CacheFolder = Path.Combine(setting.APP_PATH, "cache");
        }

        public bool IsSameIllust(int hash, string id)
        {
            bool result = true;
            if (loadedImageHashTable.ContainsKey(hash))
            {
                result = string.Equals(id, loadedImageHashTable[hash], StringComparison.CurrentCultureIgnoreCase) ||
                         string.Equals(id, loadedImageFileTable[hash], StringComparison.CurrentCultureIgnoreCase);
            }
            return (result);
        }

        public string GetImagePath(string url)
        {
            string result = null;
            var trimchars = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

            var unc = new Uri(url);
            var file = unc.IsFile ? unc.AbsolutePath : Regex.Replace(url, @"http(s)*://.*?\.((pixiv\..*?)|(pximg\..*?))/", $"", RegexOptions.IgnoreCase);
            file = file.Replace("/", "\\").TrimStart(trimchars);
            file = Regex.Replace(file, @"(.*?)(([\?|\*].*)*)", "$1", RegexOptions.IgnoreCase);
            if (!Path.IsPathRooted(file)) file = Path.Combine(_CacheFolder, file);
            result = file;

            return (result);
        }

        public bool IsCached(string url)
        {
            bool result = false;
            try
            {
                var value = string.Empty;
                var local = _caches.ContainsKey(url) ? Path.Combine(_CacheFolder, _caches[url].TrimStart(trimchars)) : GetImagePath(url);
                if (File.Exists(local)) _caches[url] = local.Replace(_CacheFolder, "").TrimStart(trimchars);
                else _caches.TryRemove(url, out value);
                result = _caches.ContainsKey(url);
            }
            catch (Exception ex) { ex.ERROR("CACHE"); }
            return (result);
        }

        public string GetCacheFile(string url, bool overwrite = false)
        {
            var file = string.Empty;
            if (url.IsCached() && !overwrite)
            {
                file = Path.Combine(_CacheFolder, _caches[url].TrimStart(trimchars));
            }
            return (file);
        }

        public async Task<CustomImageSource> GetImage(string url, bool overwrite = false, bool login = false, Size size = default(Size), Action<double, double> progressAction = null)
        {
            CustomImageSource result = new CustomImageSource();
            var file = GetCacheFile(url, overwrite);
            var fp = string.Empty;
            var id = url.GetIllustId();
            var fn = url.GetImageId();

            if (!string.IsNullOrEmpty(file))
            {
                result = await file.LoadImageFromFile(size);
            }
            else
            {
                file = GetImagePath(url);
                if (file.IsDownloading() && await file.WaitDownloading(timeout: TimeSpan.FromSeconds(30))) result = await file.LoadImageFromFile(size);
                if (result.Source == null || string.IsNullOrEmpty(result.SourcePath))
                {
                    var success = login ? await url.SaveImage(await CommonHelper.ShowLogin(), file, overwrite) : await url.SaveImage(file, overwrite, progressAction);
                    if (success) result = await file.LoadImageFromFile(size);
                    file.ClearDownloading();
                }
            }

            if (!(result.Source is ImageSource) && (url.IsDownloadedAsync(out fp, true) || url.IsDownloadedAsync(out fp)))
            {
                result = await fp.LoadImageFromFile(size);
            }

            if (result.Source is ImageSource && !string.IsNullOrEmpty(id))
            {
                var hash = result.GetHashCode();
                loadedImageHashTable[hash] = id;
                loadedImageFileTable[hash] = fn;
            }
            return (result);
        }

        public async Task<string> DownloadImage(string url, bool overwrite = false, bool login = false)
        {
            string result = string.Empty;
            var file = GetCacheFile(url, overwrite);
            if (string.IsNullOrEmpty(file))
            { 
                file = GetImagePath(url);
                if (file.IsDownloading() && await file.WaitDownloading(timeout: TimeSpan.FromSeconds(30))) result = file;
                if (string.IsNullOrEmpty(result))
                {
                    var success = login ? await url.SaveImage(await CommonHelper.ShowLogin(), file, overwrite) : await url.SaveImage(file, overwrite);
                    if (success) result = file;
                    file.ClearDownloading();
                }
            }
            return (result);
        }

    }
}
