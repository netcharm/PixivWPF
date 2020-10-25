using Newtonsoft.Json;
using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PixivWPF.Common
{
    class CacheImage
    {
        private Setting setting = Application.Current.LoadSetting();
        private char[] trimchars = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        private Dictionary<int, string> loadedImageHashTable = new Dictionary<int, string>();
        private Dictionary<int, string> loadedImageFileTable = new Dictionary<int, string>();

        private Dictionary<string, string> _caches = new Dictionary<string, string>();
        private string _CacheFolder = string.Empty;
        private string _CacheDB = string.Empty;

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
                var local = _caches.ContainsKey(url) ? Path.Combine(_CacheFolder, _caches[url].TrimStart(trimchars)) : GetImagePath(url);
                if (File.Exists(local)) _caches[url] = local.Replace(_CacheFolder, "").TrimStart(trimchars);
                else _caches.Remove(url);
                result = _caches.ContainsKey(url);
            }
            catch (Exception) { }
            return (result);
        }

        public string GetCacheFile(string url)
        {
            var file = string.Empty;
            if (url.IsCached())
            {
                file = Path.Combine(_CacheFolder, _caches[url].TrimStart(trimchars));
            }
            return (file);
        }

        public async Task<CustomImageSource> GetImage(string url, bool login = false)
        {
            if (login)
            {
                var tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return (new CustomImageSource(null, string.Empty));

                return (await GetImageLogin(url, tokens));
            }
            else
            {
                return (await GetImageDirect(url));
            }
        }

        public async Task<CustomImageSource> GetImageDirect(string url)
        {
            CustomImageSource result = new CustomImageSource();
            var file = GetCacheFile(url);
            var fp = string.Empty;
            var id = url.GetIllustId();
            var fn = url.GetImageId();

            if (!string.IsNullOrEmpty(file))
            {
                result = await file.LoadImageFromFile();
            }
            else if (url.IsDownloadedAsync(out fp, true) || url.IsDownloadedAsync(out fp))
            {
                result = await fp.LoadImageFromFile();
                if (result.Source is ImageSource) _caches[url] = fp.TrimStart(trimchars);
            }
            else
            {
                file = GetImagePath(url);
                var success = await url.SaveImage(file, false);
                if (success)
                {
                    result = await file.LoadImageFromFile();
                    _caches[url] = file.Replace(_CacheFolder, "").TrimStart(trimchars);
                }
            }
            if (result.Source is ImageSource && !string.IsNullOrEmpty(id))
            {
                loadedImageHashTable[result.GetHashCode()] = id;
                loadedImageFileTable[result.GetHashCode()] = fn;
            }
            return (result);
        }

        public async Task<CustomImageSource> GetImageLogin(string url, Pixeez.Tokens tokens)
        {
            CustomImageSource result = new CustomImageSource();
            var file = GetCacheFile(url);
            var fp = string.Empty;
            var id = url.GetIllustId();
            var fn = url.GetImageId();

            if (!string.IsNullOrEmpty(file))
            {
                result = await file.LoadImageFromFile();
            }
            else if (url.IsDownloadedAsync(out fp, true) || url.IsDownloadedAsync(out fp))
            {
                result = await fp.LoadImageFromFile();
                if (result.Source is ImageSource) _caches[url] = fp.TrimStart(trimchars);
            }
            else
            {
                tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return (result);

                file = GetImagePath(url);
                var success = await url.SaveImage(tokens, file, false);
                if (success)
                {
                    result = await file.LoadImageFromFile();
                    _caches[url] = file.Replace(_CacheFolder, "").TrimStart(trimchars);
                }
            }
            if (result.Source is ImageSource && !string.IsNullOrEmpty(id))
            {
                loadedImageHashTable[result.GetHashCode()] = id;
                loadedImageFileTable[result.GetHashCode()] = fn;
            }
            return (result);
        }
    }
}
