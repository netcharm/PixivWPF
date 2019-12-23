using Newtonsoft.Json;
using PixivWPF.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PixivWPF.Common
{
    class CacheImage
    {
        private Setting setting = Setting.Instance == null ? Setting.Load() : Setting.Instance;
        private char[] trimchars = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        private Dictionary<int, string> loadedImageHashTable = new Dictionary<int, string>();
        private Dictionary<int, string> loadedImageFileTable = new Dictionary<int, string>();

        private Dictionary<string, string> _caches = new Dictionary<string, string>();
        private string _CacheFolder = string.Empty;
        private string _CacheDB = string.Empty;

        public CacheImage()
        {
            _CacheFolder = Path.Combine(setting.APP_PATH, "cache");
            _CacheDB = Path.Combine(setting.APP_PATH, "cache.json");

            Load();
        }

        public void Load()
        {
            if (File.Exists(_CacheDB))
            {
#if DEBUG
                var text = File.ReadAllText(_CacheDB);
                if (text.Length > 20)
                    _caches = JsonConvert.DeserializeObject<Dictionary<string, string>>(text);
#endif
            }
        }
        
        public void Save()
        {
#if DEBUG
            var text = JsonConvert.SerializeObject(_caches, Formatting.Indented);
            File.WriteAllText(_CacheDB, text, new UTF8Encoding(true));
#endif
        }

        public async Task<ImageSource> GetImage(string url)
        {
            var tokens = await CommonHelper.ShowLogin();
            if (tokens == null) return null;

            return (await GetImage(url));
        }

        public string GetCacheFile(string url)
        {
            var file = Regex.Replace(url, @"http(s)*://.*?\.((pixiv\..*?)|(pximg\..*?))/", $"", RegexOptions.IgnoreCase);
            file = file.Replace("/", "\\").TrimStart(trimchars);
            file = Path.Combine(_CacheFolder, file);
            return (file);
        }

        public async Task<ImageSource> GetImage(string url, Pixeez.Tokens tokens)
        {
            ImageSource result = null;
            var file = GetCacheFile(url);
            var fp = string.Empty;
            var id = url.GetIllustId();
            var fn = url.GetImageId();

            if (_caches.ContainsKey(url))
            {
                var fcache = _caches[url].TrimStart(trimchars);
                file = Path.Combine(_CacheFolder, fcache);
                if (File.Exists(file))
                {
                    result = await file.LoadImageFromFile();
                }
                else
                {
                    tokens = await CommonHelper.ShowLogin();
                    if (tokens == null) return null;

                    var success = await url.SaveImage(tokens, file, false);
                    if (success)
                    {
                        result = await file.LoadImageFromFile();
                    }
                }
            }
            else if (File.Exists(file))
            {
                result = await file.LoadImageFromFile();
                if (result is ImageSource)
                {
                    _caches[url] = file.Replace(_CacheFolder, "");
                    //Save();
                }
            }
            else if (url.IsDownloadedAsync(out fp, true) || url.IsDownloadedAsync(out fp))
            {
                result = await fp.LoadImageFromFile();
                if (result is ImageSource)
                {
                    _caches[url] = fp;
                }
            }
            else
            {
                tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return null;

                var success = await url.SaveImage(tokens, file, false);
                if (success)
                {
                    result = await file.LoadImageFromFile();
                    _caches[url] = file.Replace(_CacheFolder, "");
                    //Save();
                }
            }
            if (result is ImageSource && !string.IsNullOrEmpty(id))
            {
                loadedImageHashTable[result.GetHashCode()] = id;
                loadedImageFileTable[result.GetHashCode()] = fn;
            }
            return (result);
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

        public async Task<string> GetImagePath(string url, Pixeez.Tokens tokens)
        {
            string result = null;
            var trimchars = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            var file = Regex.Replace(url, @"http(s)*://.*?\.((pixiv\..*?)|(pximg\..*?))/", $"", RegexOptions.IgnoreCase);
            file = file.Replace("/", "\\").TrimStart(trimchars);
            file = Path.Combine(_CacheFolder, file);

            if (_caches.ContainsKey(url))
            {
                var fcache = _caches[url].TrimStart(trimchars);
                file = Path.Combine(_CacheFolder, fcache);
                if (File.Exists(file))
                {
                    result = file;
                }
                else
                {
                    tokens = await CommonHelper.ShowLogin();
                    if (tokens == null) return null;

                    var success = await url.SaveImage(tokens, file, false);
                    if (success)
                    {
                        result = file;
                    }
                }
            }
            else if (File.Exists(file))
            {
                result = file;
                if (!string.IsNullOrEmpty(result))
                {
                    _caches[url] = file.Replace(_CacheFolder, "");
                    //Save();
                }
            }
            else
            {
                tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return null;

                var success = await url.SaveImage(tokens, file, false);
                if (success)
                {
                    result = file;
                    _caches[url] = file.Replace(_CacheFolder, "");
                    //Save();
                }
            }
            return (result);
        }

    }
}
