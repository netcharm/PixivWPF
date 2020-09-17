using MahApps.Metro.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PixivWPF.Common
{
    [JsonObject(MemberSerialization.OptOut)]
    public class Setting
    {
        //private static string AppPath = Path.GetDirectoryName(Application.ResourceAssembly.CodeBase.ToString()).Replace("file:\\", "");
        private static string AppPath = Application.Current.GetRoot();

        private static SemaphoreSlim ConfigReadWrite = new SemaphoreSlim(1, 1);
        [JsonIgnore]
        public bool ConfigBusy
        {
            get { return (ConfigReadWrite.CurrentCount <= 0 ? true : false); }
        }

        private static SemaphoreSlim TagsReadWrite = new SemaphoreSlim(1, 1);
        [JsonIgnore]
        public bool TagsBusy
        {
            get { return (TagsReadWrite.CurrentCount <= 0 ? true : false); }
        }

        private static string config = "config.json";
        [JsonIgnore]
        public string ConfigFile
        {
            get { return (Path.IsPathRooted(config) ? config : Path.Combine(AppPath, config)); }
        }

        private static string tagsfile = "tags.json";
        public string TagsFile
        {
            get
            {
                if (ConfigBusy) return (tagsfile);
                else return (Path.IsPathRooted(tagsfile) ? tagsfile : Path.Combine(AppPath, tagsfile));
            }
            set { tagsfile = Path.GetFileName(value); }
        }

        private static string tagsfile_t2s = "tags_t2s.json";
        public string CustomTagsFile
        {
            get
            {
                if (ConfigBusy) return (tagsfile_t2s);
                else return (Path.IsPathRooted(tagsfile_t2s) ? tagsfile_t2s : Path.Combine(AppPath, tagsfile_t2s));
            }
            set { tagsfile_t2s = Path.GetFileName(value); }
        }

        [JsonIgnore]
        public string APP_PATH
        {
            get { return AppPath; }
        }

        private string accesstoken = string.Empty;
        public string AccessToken
        {
            get { return (accesstoken); }
            set
            {
                if (!ConfigBusy)
                {
                    username = User.AesEncrypt(value);
                    password = Pass.AesEncrypt(value);
                }
                if (myinfo is Pixeez.Objects.User)
                {
                    UID = myinfo.Id.Value.ToString().AesEncrypt(value);
                }
                accesstoken = value;
            }
        }

        private string refreshtoken = string.Empty;
        public string RefreshToken
        {
            get { return (refreshtoken); }
            set { refreshtoken = value; }
        }

        private string proxy = string.Empty;
        public string Proxy
        {
            get { return (proxy); }
            set { proxy = value; }
        }

        private bool useproxy = false;
        public bool UsingProxy
        {
            get { return (useproxy); }
            set { useproxy = value; }
        }

        private static Setting Cache = null;// Load(config);
        [JsonIgnore]
        public static Setting Instance
        {
            get { return (Cache); }
        }

        public bool SaveUserPass { get; set; } = false;
        [JsonProperty(nameof(User))]
        public string Username
        {
            get
            {
                if (SaveUserPass && !ConfigBusy) return username;
                else return (string.Empty);
            }
            set { if (SaveUserPass) username = value; }
        }

        [JsonProperty(nameof(Pass))]
        public string Password
        {
            get
            {
                if (SaveUserPass && !ConfigBusy) return password;
                else return (string.Empty);
            }
            set { if (SaveUserPass) password = value; }
        }

        [JsonIgnore]
        private string username = string.Empty;
        [JsonIgnore]
        public string User
        {
            get { return (username.AesDecrypt(accesstoken)); }
            set { username = value.AesEncrypt(accesstoken); }
        }

        [JsonIgnore]
        private string password = string.Empty;
        [JsonIgnore]
        public string Pass
        {
            get { return (password.AesDecrypt(accesstoken)); }
            set { password = value.AesEncrypt(accesstoken); }
        }

        [JsonIgnore]
        private Pixeez.Objects.User myinfo = null;
        [JsonIgnore]
        public Pixeez.Objects.User MyInfo
        {
            get { return myinfo; }
            set
            {
                myinfo = value;
                if (value is Pixeez.Objects.User)
                {
                    UID = myinfo.Id.Value.ToString().AesEncrypt(accesstoken);
                }
            }
        }

        public string UID { get; set; } = string.Empty;
        [JsonIgnore]
        public long MyID
        {
            get
            {
                if (myinfo is Pixeez.Objects.User)
                    return (myinfo.Id.Value);
                else
                    return (GetMyId());
            }
        }

        private long GetMyId()
        {
            long luid = 0;
            var suid = UID.AesDecrypt(accesstoken);
            var ret = long.TryParse(suid, out luid);
            if (!ret)
            {
                new Action(async () =>
                {
                    myinfo = await luid.RefreshUser();
                }).Invoke();
                return (myinfo.Id.Value);
            }
            else return (luid);
        }

        private long update = 0;
        public long Update
        {
            get { return update; }
            set { update = value; }
        }

        private DateTime exptime=DateTime.Now;
        public DateTime ExpTime
        {
            get { return exptime; }
            set { exptime = value; }
        }

        private int expdurtime=3600;
        public int ExpiresIn
        {
            get { return expdurtime; }
            set { expdurtime = value; }
        }

        [JsonIgnore]
        private string lastfolder = string.Empty;
        [JsonIgnore]
        public string LastFolder
        {
            get { return lastfolder; }
            set
            {
                lastfolder = value;
                if (LocalStorage.Count(o => o.Folder.Equals(lastfolder)) <= 0)
                {
                    LocalStorage.Add(new StorageType(lastfolder, true));
                    lastfolder.AddDownloadedWatcher();
                }
            }
        }

        public string FontName { get; set; } = string.Empty;
        [JsonIgnore]
        private FontFamily fontfamily = SystemFonts.MessageFontFamily;
        [JsonIgnore]
        public FontFamily FontFamily { get { return (fontfamily); } }

        private string theme = string.Empty;
        [JsonProperty("Theme")]
        public string CurrentTheme { get; set; }

        private string accent = string.Empty;
        [JsonProperty("Accent")]
        public string CurrentAccent { get; set; }

        public bool PrivateFavPrefer { get; set; } = false;
        public bool PrivateBookmarkPrefer { get; set; } = false;

        public AutoExpandMode AutoExpand { get; set; } = AutoExpandMode.AUTO;

        public string ShellSearchBridgeApplication { get; set; } = "PixivWPFSearch.exe";
        public string ShellPixivPediaApplication { get; set; } = "nw.exe";
        public string ShellPixivPediaApplicationArgs { get; set; } = "--single-process --enable-node-worker --app-shell-host-window-size=1280x720";

        private static string custom_template_file = "contents-template.html";
        public string ContentsTemplateFile
        {
            get
            {
                if (ConfigBusy) return (custom_template_file);
                else return (Path.IsPathRooted(custom_template_file) ? custom_template_file : Path.Combine(AppPath, custom_template_file));
            }
            set { custom_template_file = Path.GetFileName(value); }
        }

        public DateTime ContentsTemplateTime { get; set; } = new DateTime(0);
        public string ContentsTemplete { get; set; } = string.Empty;
        [JsonIgnore]
        public string CustomContentsTemplete { get; set; } = string.Empty;

        [JsonIgnore]
        public string SaveFolder { get; set; }

        public List<StorageType> LocalStorage { get; set; } = new List<StorageType>();

        public Point DropBoxPosition { get; set; } = new Point(0, 0);

        public static void UpdateContentsTemplete()
        {
            if (Cache is Setting)
            {
                if (File.Exists(Cache.ContentsTemplateFile))
                {
                    Cache.CustomContentsTemplete = File.ReadAllText(Cache.ContentsTemplateFile);
                    var ftc = File.GetCreationTime(Cache.ContentsTemplateFile);
                    var ftw = File.GetLastWriteTime(Cache.ContentsTemplateFile);
                    var fta = File.GetLastAccessTime(Cache.ContentsTemplateFile);
                    if (ftw > Cache.ContentsTemplateTime || ftc > Cache.ContentsTemplateTime || fta > Cache.ContentsTemplateTime)
                    {
                        Cache.ContentsTemplete = Cache.CustomContentsTemplete;
                        Cache.ContentsTemplateTime = DateTime.Now;
                        Cache.Save();
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(Cache.CustomContentsTemplete))
                    {
                        Cache.ContentsTemplete = CommonHelper.GetDefaultTemplate();
                        Cache.CustomContentsTemplete = string.Empty;
                        Cache.Save();
                    }
                }
                CommonHelper.UpdateWebContentAsync();
            }
        }

        public void Save(string configfile = "")
        {
            if (ConfigReadWrite.Wait(1))
            {
                try
                {
                    if (Cache is Setting)
                    {
                        if (string.IsNullOrEmpty(configfile)) configfile = config;

                        if (Cache.LocalStorage.Count(o => o.Folder.Equals(Cache.SaveFolder)) < 0 && !string.IsNullOrEmpty(Cache.SaveFolder))
                        {
                            Cache.LocalStorage.Add(new StorageType(Cache.SaveFolder, true));
                        }

                        if (Cache.LocalStorage.Count(o => o.Folder.Equals(Cache.LastFolder)) < 0 && !string.IsNullOrEmpty(Cache.LastFolder))
                        {
                            Cache.LocalStorage.Add(new StorageType(Cache.LastFolder, true));
                        }

                        UpdateContentsTemplete();

                        var text = JsonConvert.SerializeObject(Cache, Formatting.Indented);
                        File.WriteAllText(configfile, text, new UTF8Encoding(true));

                        SaveTags();
                    }
                }
                catch (Exception ex)
                {
                    ex.Message.ShowMessageDialog("ERROR");
                }
                finally
                {
                    ConfigReadWrite.Release();
                }
            }
        }

        private static DateTime lastConfigUpdate = default(DateTime);
        public static Setting Load(bool force = false, string configfile = "")
        {
            Setting result = Cache is Setting ? Cache : new Setting();
            if (ConfigReadWrite.Wait(0))
            {
                try
                {
                    if (string.IsNullOrEmpty(configfile)) configfile = Cache is Setting ? Instance.ConfigFile : config;
                    var filetime = configfile.GetFileTime("m");
                    if (!File.Exists(configfile)) filetime = lastConfigUpdate + TimeSpan.FromSeconds(1);

                    if (!(Cache is Setting) || (force && lastConfigUpdate.DeltaMillisecond(filetime) > 250))
                    {
                        lastConfigUpdate = filetime;
                        if (File.Exists(configfile))
                        {
                            var text = File.ReadAllText(configfile);
                            if (Cache is Setting && text.Length > 20)
                            {
                                var cache = JsonConvert.DeserializeObject<Setting>(text);
                                Cache.SaveUserPass = cache.SaveUserPass;
                                Cache.Proxy = cache.Proxy;
                                Cache.UsingProxy = cache.UsingProxy;
                                Cache.FontName = cache.FontName;
                                Cache.CurrentTheme = cache.CurrentTheme;
                                Cache.CurrentAccent = cache.CurrentAccent;
                                Cache.PrivateFavPrefer = cache.PrivateFavPrefer;
                                Cache.PrivateBookmarkPrefer = cache.PrivateBookmarkPrefer;
                                Cache.AutoExpand = cache.AutoExpand;
                                Cache.ShellSearchBridgeApplication = cache.ShellSearchBridgeApplication;
                                Cache.ShellPixivPediaApplication = cache.ShellPixivPediaApplication;
                                Cache.ShellPixivPediaApplicationArgs = cache.ShellPixivPediaApplicationArgs;
                                Cache.ContentsTemplateFile = cache.ContentsTemplateFile;
                                Cache.LocalStorage = cache.LocalStorage;
                            }
                            else
                            {
                                if (text.Length < 20)
                                    Cache = new Setting();
                                else
                                    Cache = JsonConvert.DeserializeObject<Setting>(text);
                            }

                            if (Cache.LocalStorage.Count <= 0 && !string.IsNullOrEmpty(Cache.SaveFolder))
                                Cache.LocalStorage.Add(new StorageType(Cache.SaveFolder, true));

                            Cache.LocalStorage.InitDownloadedWatcher();
#if DEBUG
                            #region Setup UI font
                            if (!string.IsNullOrEmpty(Cache.FontName))
                            {
                                try
                                {
                                    Cache.fontfamily = new FontFamily(Cache.FontName);
                                }
                                catch (Exception)
                                {
                                    Cache.fontfamily = SystemFonts.MessageFontFamily;
                                }
                            }
                            #endregion
#endif
                            #region Update Contents Template
                            UpdateContentsTemplete();
                            #endregion

                            #region Update Theme
                            if (!string.IsNullOrEmpty(Cache.CurrentTheme))
                                Theme.CurrentTheme = Cache.CurrentTheme;
                            if (!string.IsNullOrEmpty(Cache.CurrentAccent))
                                Theme.CurrentAccent = Cache.CurrentAccent;
                            #endregion
                            result = Cache;
                        }
                        LoadTags(true, true);
                    }
                }
#if DEBUG
                catch (Exception ex) { ex.Message.ShowToast("ERROR"); }
#else
                catch (Exception) { }
#endif
                finally
                {
                    ConfigReadWrite.Release();
                }
            }
            return (result);
        }

        public void SaveTags()
        {
            if (TagsReadWrite.Wait(1))
            {
                try
                {
                    if (CommonHelper.TagsCache.Count > 0)
                    {
                        try
                        {
                            var tags = JsonConvert.SerializeObject(CommonHelper.TagsCache, Formatting.Indented);
                            File.WriteAllText(tagsfile, tags, new UTF8Encoding(true));
                        }
                        catch (Exception) { }
                    }
                    //if (File.Exists(tagsfile_t2s))
                    //{
                    //    try
                    //    {
                    //        var tags_t2s = File.ReadAllText(tagsfile_t2s);
                    //        CommonHelper.TagsT2S = JsonConvert.DeserializeObject<Dictionary<string, string>>(tags_t2s);
                    //    }
                    //    catch (Exception) { }
                    //}
                }
                catch (Exception) { }
                finally
                {
                    TagsReadWrite.Release();
                }
            }
        }

        private static DateTime lastTagsUpdate = default(DateTime);
        public static void LoadTags(bool all = false, bool force = false)
        {
            if (TagsReadWrite.Wait(0))
            {
                try
                {
                    var default_tags = Cache is Setting ? Cache.TagsFile : tagsfile;
                    var custom_tags = Cache is Setting ? Cache.CustomTagsFile : tagsfile_t2s;
                    force = force || (CommonHelper.TagsCache.Count <= 0 && CommonHelper.TagsT2S.Count <= 0);
                    var filetime = custom_tags.GetFileTime("m");
                    if (!File.Exists(custom_tags)) filetime = lastTagsUpdate + TimeSpan.FromSeconds(1);

                    if (force && lastTagsUpdate.DeltaMillisecond(filetime) > 5)
                    {
                        lastTagsUpdate = filetime;

                        if (all && File.Exists(default_tags))
                        {
                            try
                            {
                                var tags = File.ReadAllText(default_tags);
                                CommonHelper.TagsCache = JsonConvert.DeserializeObject<Dictionary<string, string>>(tags);
                                CommonHelper.UpdateIllustTagsAsync();
                            }
                            catch (Exception) { }
                        }

                        if (File.Exists(custom_tags))
                        {
                            try
                            {
                                var tags_t2s = File.ReadAllText(custom_tags);
                                CommonHelper.TagsT2S = JsonConvert.DeserializeObject<Dictionary<string, string>>(tags_t2s);
                                var keys = CommonHelper.TagsT2S.Keys.ToList();
                                foreach (var k in keys)
                                {
                                    CommonHelper.TagsT2S[k.Trim()] = CommonHelper.TagsT2S[k].Trim();
                                }
                                CommonHelper.UpdateIllustTagsAsync();
                            }
                            catch (Exception) { }
                        }
                        else
                        {
                            try
                            {
                                if (CommonHelper.TagsT2S is Dictionary<string, string>)
                                {
                                    CommonHelper.TagsT2S.Clear();
                                    CommonHelper.UpdateIllustTagsAsync();
                                }
                            }
                            catch (Exception) { }
                        }
                    }
                }
                catch (Exception) { }
                finally
                {
                    TagsReadWrite.Release();
                }
            }
        }

        public static string ProxyServer()
        {
            string result = null;
            if (Cache is Setting) result = Cache.Proxy;
            return (result);
        }

        public static bool UseProxy()
        {
            bool result = false;
            if (Cache is Setting) result = Cache.UsingProxy;
            return (result);
        }

        public static string Token()
        {
            string result = string.Empty;
            if (Cache is Setting) result = Cache.AccessToken;
            return (result);
        }

        public static bool Token(string accesstoken)
        {
            bool result = false;
            if (Cache is Setting)
            {
                Cache.AccessToken = accesstoken;
                result = true;
            }
            return (result);
        }

        public static bool Token(string accesstoken, string refreshtoken)
        {
            bool result = false;
            if (Cache is Setting)
            {
                Cache.AccessToken = accesstoken;
                Cache.RefreshToken = refreshtoken;
                result = true;
            }
            return (result);
        }
    }
}
