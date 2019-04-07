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
        private Setting setting = Setting.Load();
        private char[] trimchars = new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

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
                var text = File.ReadAllText(_CacheDB);
                if (text.Length > 20)
                    _caches = JsonConvert.DeserializeObject<Dictionary<string, string>>(text);
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

            if (_caches.ContainsKey(url))
            {
                var fcache = _caches[url].TrimStart(trimchars);
                file = Path.Combine(_CacheFolder, fcache);
                if (File.Exists(file))
                {
                    result = await file.LoadImage();
                }
                else
                {
                    tokens = await CommonHelper.ShowLogin();
                    if (tokens == null) return null;

                    var success = await url.ToImageFile(tokens, file, false);
                    if (success)
                    {
                        result = await file.LoadImage();
                    }
                }
            }
            else if (File.Exists(file))
            {
                result = await file.LoadImage();
                if (result is ImageSource)
                {
                    _caches[url] = file.Replace(_CacheFolder, "");
                    Save();
                }
            }
            else
            {
                tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return null;

                var success = await url.ToImageFile(tokens, file, false);
                if (success)
                {
                    result = await file.LoadImage();
                    _caches[url] = file.Replace(_CacheFolder, "");
                    Save();
                }
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

                    var success = await url.ToImageFile(tokens, file, false);
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
                    Save();
                }
            }
            else
            {
                tokens = await CommonHelper.ShowLogin();
                if (tokens == null) return null;

                var success = await url.ToImageFile(tokens, file, false);
                if (success)
                {
                    result = file;
                    _caches[url] = file.Replace(_CacheFolder, "");
                    Save();
                }
            }
            return (result);
        }

    }
}
