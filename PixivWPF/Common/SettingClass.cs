using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PixivWPF.Common
{
    // Custom comparer for the Product class
    class StorageTypeComparer : IEqualityComparer<StorageType>
    {
        // Products are equal if their names and product numbers are equal.
        public bool Equals(StorageType x, StorageType y)
        {

            //Check whether the compared objects reference the same data.
            if (ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (ReferenceEquals(x, null) || ReferenceEquals(y, null)) return false;

            //Check whether the products' properties are equal.
            return (x.Folder.Equals(y.Folder) && x.Cached == y.Cached && x.IncludeSubFolder == y.IncludeSubFolder);
        }

        // If Equals() returns true for a pair of objects 
        // then GetHashCode() must return the same value for these objects.

        public int GetHashCode(StorageType storage)
        {
            //Check whether the object is null
            if (ReferenceEquals(storage, null)) return 0;

            //Get hash code for the Name field if it is not null.
            int hashStorageName = string.IsNullOrEmpty(storage.Folder) ? 0 : storage.Folder.GetHashCode();

            //Calculate the hash code for the Storage.
            return hashStorageName;
        }

    }

    [JsonObject(MemberSerialization.OptOut)]
    public class Setting
    {
        #region Application base
        //private static string AppPath = Path.GetDirectoryName(Application.ResourceAssembly.CodeBase.ToString()).Replace("file:\\", "");
        private static string AppPath = Application.Current.GetRoot();
        [JsonIgnore]
        public string APP_PATH { get { return AppPath; } }

        private static Setting Cache = null;// Load(config);
        [JsonIgnore]
        public static Setting Instance { get { return (Cache); } }

        [JsonIgnore]
        public static bool StartUp { get; internal set; } = false;

        private bool single_instance = true;
        public bool SingleInstance
        {
            get { return (Cache is Setting ? Cache.single_instance : single_instance); }
            set
            {
                single_instance = value;
                if (Cache is Setting) Cache.single_instance = single_instance;
            }
        }

        private bool calc_system_memory_usage = false;
        public bool CalcSystemMemoryUsage
        {
            get { return (Cache is Setting ? Cache.calc_system_memory_usage : calc_system_memory_usage); }
            set
            {
                calc_system_memory_usage = value;
                if (Cache is Setting) Cache.calc_system_memory_usage = calc_system_memory_usage;
            }
        }

        private bool confirm_exit = true;
        public bool ConfirmExit
        {
            get { return (Cache is Setting ? Cache.confirm_exit : confirm_exit); }
            set
            {
                confirm_exit = value;
                if (Cache is Setting) Cache.confirm_exit = confirm_exit;
            }
        }

        private bool confirm_restart = true;
        public bool ConfirmRestart
        {
            get { return (Cache is Setting ? Cache.confirm_restart : confirm_restart); }
            set
            {
                confirm_restart = value;
                if (Cache is Setting) Cache.confirm_restart = confirm_restart;
            }
        }

        private bool confirm_upgrade = true;
        public bool ConfirmUpgrade
        {
            get { return (Cache is Setting ? Cache.confirm_upgrade : confirm_upgrade); }
            set
            {
                confirm_upgrade = value;
                if (Cache is Setting) Cache.confirm_upgrade = confirm_upgrade;
            }
        }

        private string upgrade_app = string.Empty;
        public string UpgradeLaunch
        {
            get { return (Cache is Setting ? Cache.upgrade_app : upgrade_app); }
            set
            {
                upgrade_app = value;
                if (Cache is Setting) Cache.upgrade_app = upgrade_app;
            }
        }

        private List<string> upgrade_files = new List<string>();
        public List<string> UpgradeFiles
        {
            get { return (Cache is Setting ? Cache.upgrade_files : upgrade_files); }
            internal set
            {
                upgrade_files = value;
                if (Cache is Setting) Cache.upgrade_files = upgrade_files;
            }
        }
        #endregion

        #region Config load/save relative
        private static SemaphoreSlim CanConfigRead = new SemaphoreSlim(1, 1);
        private static SemaphoreSlim CanConfigWrite = new SemaphoreSlim(1, 1);
        [JsonIgnore]
        public static bool IsConfigBusy
        {
            get { return (IsConfigLoad || IsConfigSave ? true : false); }
        }

        [JsonIgnore]
        public static bool IsConfigLoad
        {
            get { return (CanConfigRead.CurrentCount <= 0 ? true : false); }
        }

        [JsonIgnore]
        public static bool IsConfigSave
        {
            get { return (CanConfigWrite.CurrentCount <= 0 ? true : false); }
        }

        private static SemaphoreSlim TagsReadWrite = new SemaphoreSlim(1, 1);
        [JsonIgnore]
        public static bool IsTagsBusy
        {
            get { return (TagsReadWrite.CurrentCount <= 0 ? true : false); }
        }

        private static SemaphoreSlim CustomTagsReadWrite = new SemaphoreSlim(1, 1);
        [JsonIgnore]
        public static bool IsCustomTagsBusy
        {
            get { return (CustomTagsReadWrite.CurrentCount <= 0 ? true : false); }
        }

        private static SemaphoreSlim CustomWildcardTagsReadWrite = new SemaphoreSlim(1, 1);
        [JsonIgnore]
        public static bool IsCustomWildcardTagsBusy
        {
            get { return (CustomWildcardTagsReadWrite.CurrentCount <= 0 ? true : false); }
        }

        private static SemaphoreSlim ContentsTemplateReadWrite = new SemaphoreSlim(1, 1);
        [JsonIgnore]
        public static bool IsContentsTemplateBusy
        {
            get { return (ContentsTemplateReadWrite.CurrentCount <= 0 ? true : false); }
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
                if (IsConfigBusy) return (tagsfile);
                else return (Path.IsPathRooted(tagsfile) ? tagsfile : Path.Combine(AppPath, tagsfile));
            }
            set
            {
                tagsfile = Path.GetFileName(value);
            }
        }

        private static string tagsfile_t2s = "tags_t2s.json";
        public string CustomTagsFile
        {
            get
            {
                if (IsConfigBusy) return (tagsfile_t2s);
                else return (Path.IsPathRooted(tagsfile_t2s) ? tagsfile_t2s : Path.Combine(AppPath, tagsfile_t2s));
            }
            set { tagsfile_t2s = Path.GetFileName(value); }
        }

        private static string tagsfile_t2s_widecard = "tags_t2s_widecard.json";
        public string CustomWildcardTagsFile
        {
            get
            {
                if (IsConfigBusy) return (tagsfile_t2s_widecard);
                else return (Path.IsPathRooted(tagsfile_t2s_widecard) ? tagsfile_t2s_widecard : Path.Combine(AppPath, tagsfile_t2s_widecard));
            }
            set { tagsfile_t2s_widecard = Path.GetFileName(value); }
        }

        private string last_opened = "last_opened.json";
        public string LastOpenedFile
        {
            get
            {
                if (IsConfigBusy) return (last_opened);
                else return (Path.IsPathRooted(last_opened) ? last_opened : Path.Combine(AppPath, last_opened));
            }
            set { last_opened = Path.GetFileName(value); }
        }

        private int last_opened_autosave_frequency = 30;
        public int LastOpenedFileAutoSaveFrequency
        {
            get { return (Cache is Setting ? Cache.last_opened_autosave_frequency : last_opened_autosave_frequency); }
            set
            {
                last_opened_autosave_frequency = value;
                if (Cache is Setting) Cache.last_opened_autosave_frequency = last_opened_autosave_frequency;
            }
        }

        private string custom_template_file = "contents-template.html";
        public string ContentsTemplateFile
        {
            get
            {
                if (IsConfigBusy) return (custom_template_file);
                else return (Path.IsPathRooted(custom_template_file) ? custom_template_file : Path.Combine(AppPath, custom_template_file));
            }
            set
            {
                custom_template_file = Path.GetFileName(value);
                if (Cache is Setting) Cache.custom_template_file = custom_template_file;
            }
        }

        public void Save(bool full, string configfile = "")
        {
            if (!IsConfigBusy && CanConfigWrite.Wait(1))
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

                        Cache.UpgradeFiles = Cache.UpgradeFiles.Distinct().ToList();
                        Cache.ProxyBypass = Cache.ProxyBypass.Distinct().ToList();

                        Cache.LocalStorage = Cache.LocalStorage.Distinct(new StorageTypeComparer()).ToList();
                        if(!IsContentsTemplateBusy) UpdateContentsTemplete();

                        var text = JsonConvert.SerializeObject(Cache, Formatting.Indented);
                        configfile.WaitFileUnlock();
                        File.WriteAllText(configfile, text, new UTF8Encoding(true));

                        SaveTags();

                        if (full)
                        {
                            Commands.SaveOpenedWindows.Execute(true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ex.Message.ShowMessageBox("ERROR");
                }
                finally
                {
                    if (CanConfigWrite is SemaphoreSlim && CanConfigWrite.CurrentCount <= 0) CanConfigWrite.Release();
                }
            }
        }

        public void Save(string configfile = "")
        {
            Save(false, configfile);
        }

        private static DateTime lastConfigUpdate = default(DateTime);
        public static Setting Load(bool force = false, bool loadtags = true, string configfile = "")
        {
            Setting result = Cache is Setting ? Cache : new Setting();
            if (!IsConfigBusy && CanConfigRead.Wait(0))
            {
                try
                {
                    if (string.IsNullOrEmpty(configfile)) configfile = Cache is Setting ? Instance.ConfigFile : config;
                    var exists = File.Exists(configfile);
                    var filetime = configfile.GetFileTime("m");
                    if (!exists) filetime = lastConfigUpdate + TimeSpan.FromSeconds(1);

                    if (!(Cache is Setting) || (force && lastConfigUpdate.DeltaMilliseconds(filetime) > 250))
                    {
                        lastConfigUpdate = filetime;
                        if (exists && configfile.WaitFileUnlock())
                        {
                            var dso = new JsonSerializerSettings(){ Error = (se, ev) => ev.ErrorContext.Handled = true };
                            var text = File.ReadAllText(configfile);
                            if (Cache is Setting && text.Length > 20)
                            {
                                var cache = JsonConvert.DeserializeObject<Setting>(text, dso);
                                //UpdateCache(cache);
                            }
                            else
                            {
                                if (text.Length < 20)
                                    Cache = new Setting();
                                else
                                    Cache = JsonConvert.DeserializeObject<Setting>(text, dso);
                            }
                            Cache.LocalStorage = Cache.LocalStorage.Distinct(new StorageTypeComparer()).ToList();

                            if (Cache.LocalStorage.Count <= 0 && !string.IsNullOrEmpty(Cache.SaveFolder))
                                Cache.LocalStorage.Add(new StorageType(Cache.SaveFolder, true));

                            if (Cache.LocalStorage.Count <= 0 && !string.IsNullOrEmpty(Cache.LastFolder))
                                Cache.LocalStorage.Add(new StorageType(Cache.LastFolder, true));

                            Cache.LocalStorage.InitDownloadedWatcher();

                            Cache.UpgradeFiles = Cache.UpgradeFiles.Distinct().ToList();
                            Cache.ProxyBypass = Cache.ProxyBypass.Distinct().ToList();

                            #region Setup UI font
                            if (Cache.UseCustomFont && !string.IsNullOrEmpty(Cache.FontName))
                            {
                                try
                                {
                                    Cache.fontfamily = new FontFamily(Cache.FontName);
                                }
                                catch (Exception ex)
                                {
                                    ex.ERROR();
                                    Cache.fontfamily = SystemFonts.MessageFontFamily;
                                }
                            }
                            #endregion

                            #region Update Contents Template
                            UpdateContentsTemplete();
                            #endregion

                            #region Setting Speech TTS culture/play setting
                            Speech.CustomNames = Cache.SpeechPreferList;
                            Speech.AltPlayMixedCulture = Cache.SpeechAltPlayMixedCulture;
                            Speech.SimpleCultureDetect = Cache.SpeechSimpleDetectCulture;
                            Speech.AutoChangeSpeechSpeed = Cache.SpeechAutoChangeSpeedWhenRepeatPlay;
                            Speech.ChineseSimplifiedPrefer = Cache.SpeechChineseSimplifiedPrefer;
                            Speech.PlayMixedCultureInline = Cache.SpeechPlayMixedCultureInline;
                            #endregion

                            #region Update Theme
#if DEBUG
                            Application.Current.SetTheme(Cache.CurrentTheme, Cache.CurrentAccent);
#else
                            Application.Current.SetAccent(Cache.CurrentAccent);
                            Application.Current.SetStyle(Cache.CurrentTheme);
#endif
                            #endregion
                            result = Cache;

                            if (StartUp) "Config Setting Reloaded".ShowToast("INFO");
                        }
                        if (loadtags) LoadTags(true, true);
                    }
                }
#if DEBUG
                catch (Exception ex) { ex.Message.ShowToast("ERROR"); }
#else
                catch (Exception ex) { ex.ERROR(); }
#endif
                finally
                {
                    StartUp = true;
                    if (CanConfigRead is SemaphoreSlim && CanConfigRead.CurrentCount <= 0) CanConfigRead.Release();
                }
            }
            return (result);
        }
        #endregion

        #region Load/Save tag relative
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
                            tagsfile.WaitFileUnlock();
                            File.WriteAllText(tagsfile, tags, new UTF8Encoding(true));
                        }
                        catch (Exception ex) { ex.ERROR(); }
                    }
                    //if (File.Exists(tagsfile_t2s))
                    //{
                    //    try
                    //    {
                    //        var tags_t2s = File.ReadAllText(tagsfile_t2s);
                    //        CommonHelper.TagsT2S = JsonConvert.DeserializeObject<Dictionary<string, string>>(tags_t2s);
                    //    }
                    //    catch (Exception ex) { ex.ERROR(); }
                    //}
                }
                catch (Exception ex) { ex.ERROR(); }
                finally
                {
                    if (TagsReadWrite is SemaphoreSlim && TagsReadWrite.CurrentCount <= 0) TagsReadWrite.Release();
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

                    force = force || CommonHelper.TagsCache.Count <= 0;
                    var exists = File.Exists(default_tags);
                    var filetime = exists ? default_tags.GetFileTime("m") : lastTagsUpdate + TimeSpan.FromSeconds(1);

                    if (force && lastTagsUpdate.DeltaMilliseconds(filetime) > 5)
                    {
                        lastTagsUpdate = filetime;

                        bool tags_changed = false;
                        if (all && exists && default_tags.WaitFileUnlock())
                        {
                            try
                            {
                                var tags = File.ReadAllText(default_tags);
                                var t2s = JsonConvert.DeserializeObject<ConcurrentDictionary<string, string>>(tags);
                                var keys = t2s.Keys.ToList();
                                CommonHelper.TagsCache.Clear();
                                foreach (var k in keys)
                                {
                                    CommonHelper.TagsCache[k.Trim()] = t2s[k].Trim();
                                }
                                tags_changed = true;
                            }
                            catch (Exception ex) { ex.ERROR("LoadTags"); }
                        }

                        LoadCustomTags(force);

                        LoadCustomWidecardTags(force);

                        if (tags_changed) CommonHelper.UpdateIllustTagsAsync();
                    }
                }
                catch (Exception ex) { ex.ERROR("LoadTags"); }
                finally
                {
                    if (TagsReadWrite is SemaphoreSlim && TagsReadWrite.CurrentCount <= 0) TagsReadWrite.Release();
                }
            }
        }

        private static DateTime lastCustomTagsUpdate = default(DateTime);
        public static void LoadCustomTags(bool force = false)
        {
            if (CustomTagsReadWrite.Wait(0))
            {
                try
                {
                    var custom_tags = Cache is Setting ? Cache.CustomTagsFile : tagsfile_t2s;

                    force = force || CommonHelper.TagsT2S.Count <= 0;
                    var exists = File.Exists(custom_tags);
                    var filetime = exists ? custom_tags.GetFileTime("m") : lastCustomTagsUpdate + TimeSpan.FromSeconds(1);

                    if (force && lastCustomTagsUpdate.DeltaMilliseconds(filetime) > 5)
                    {
                        lastCustomTagsUpdate = filetime;
                        bool tags_changed = false;
                        if (exists && custom_tags.WaitFileUnlock())
                        {
                            try
                            {
                                var tags_t2s = File.ReadAllText(custom_tags);
                                var t2s = JsonConvert.DeserializeObject<ConcurrentDictionary<string, string>>(tags_t2s);
                                var keys = t2s.Keys.ToList();
                                CommonHelper.TagsT2S.Clear();
                                foreach (var k in keys)
                                {
                                    CommonHelper.TagsT2S[k.Trim()] = t2s[k].Trim();
                                }
                                tags_changed = true;
                            }
                            catch (Exception ex)
                            {
                                $"Custom Translation Tags Loading Error:\n{ex.Message}".ShowToast("ERROR[Tags]");
                            }
                        }
                        else
                        {
                            if (CommonHelper.TagsT2S is ConcurrentDictionary<string, string>)
                            {
                                CommonHelper.TagsT2S.Clear();
                                tags_changed = true;
                            }
                        }

                        if (tags_changed)
                        {
                            CommonHelper.UpdateIllustTagsAsync();
                            if (StartUp) "Custom Translation Tags Reloaded".ShowToast("INFO");
                        }
                    }
                }
                catch (Exception ex) { ex.ERROR("LoadCustomTags"); }
                finally
                {
                    if (CustomTagsReadWrite is SemaphoreSlim && CustomTagsReadWrite.CurrentCount <= 0) CustomTagsReadWrite.Release();
                }
            }
        }

        private static DateTime lastCustomWildcardTagsUpdate = default(DateTime);
        public static void LoadCustomWidecardTags(bool force = false)
        {
            if (CustomWildcardTagsReadWrite.Wait(0))
            {
                try
                {
                    var custom_widecard_tags = Cache is Setting ? Cache.CustomWildcardTagsFile : tagsfile_t2s_widecard;

                    force = force || CommonHelper.TagsWildecardT2S.Count <= 0;
                    var exists = File.Exists(custom_widecard_tags);
                    var filetime = exists ? custom_widecard_tags.GetFileTime("m") : lastCustomWildcardTagsUpdate + TimeSpan.FromSeconds(1);

                    if (force && lastCustomWildcardTagsUpdate.DeltaMilliseconds(filetime) > 5)
                    {
                        lastCustomWildcardTagsUpdate = filetime;
                        bool tags_changed = false;
                        if (exists && custom_widecard_tags.WaitFileUnlock())
                        {
                            try
                            {
                                var tags_t2s_widecard = File.ReadAllText(custom_widecard_tags);

                                var t2s_new = JsonConvert.DeserializeObject<OrderedDictionary>(tags_t2s_widecard);
                                var t2s_new_keys = t2s_new.Keys.Cast<string>().ToList();

                                var t2s_old = CommonHelper.TagsWildecardT2S.Cast<DictionaryEntry>().ToDictionary(k => (string)k.Key, v => (string)v.Value);
                                var t2s_old_keys = CommonHelper.TagsWildecardT2S.Keys.Cast<string>().ToList();
                                foreach (var entry in t2s_old)
                                {
                                    if(!t2s_new.Contains(entry.Key)) entry.Key.TagWildcardCacheUpdate();
                                }

                                CommonHelper.TagsWildecardT2S.Clear();
                                foreach (DictionaryEntry entry in t2s_new)
                                {
                                    var k = entry.Key is string ? (entry.Key as string).Trim() : string.Empty;
                                    var v = entry.Value is string ? (entry.Value as string).Trim() : string.Empty;                                    
                                    if (!t2s_old.ContainsKey(k) || !t2s_old[k].Equals(v) || t2s_new_keys.IndexOf(k) != t2s_old_keys.IndexOf(k)) entry.TagWildcardCacheUpdate();
                                    if (CommonHelper.TagsWildecardT2S.Contains(k))
                                    {
                                        $"Custom Translation Wildcard Tags Loading Error: key {k} exists".DEBUG("LoadCustomWidecardTags");
                                        continue;
                                    }
                                    CommonHelper.TagsWildecardT2S[k] = v;
                                }
                                t2s_old.Clear();
                                tags_changed = true;
                            }
                            catch (Exception ex)
                            {
                                $"Custom Translation Wildcard Tags Loading Error:\n{ex.Message}".ShowToast("ERROR[Tags]");
                            }
                        }
                        else
                        {
                            if (CommonHelper.TagsWildecardT2S is OrderedDictionary)
                            {
                                CommonHelper.TagsWildecardT2S.Clear();
                                tags_changed = true;
                            }
                        }

                        if (tags_changed)
                        {
                            CommonHelper.UpdateIllustTagsAsync();
                            if (StartUp) "Custom Translation Wildcard Tags Reloaded".ShowToast("INFO");
                        }
                    }
                }
                catch (Exception ex) { ex.ERROR("LoadCustomWidecardTags"); }
                finally
                {
                    if (CustomWildcardTagsReadWrite is SemaphoreSlim && CustomWildcardTagsReadWrite.CurrentCount <= 0) CustomWildcardTagsReadWrite.Release();
                }
            }
        }
        #endregion

        #region Pixiv account relative
        private string accesstoken = string.Empty;
        public string AccessToken
        {
            get { return (Cache is Setting ? Cache.accesstoken : accesstoken); }
            set
            {
                if (!IsConfigBusy)
                {
                    username = User.AesEncrypt(value);
                    password = Pass.AesEncrypt(value);
                }
                if (myinfo is Pixeez.Objects.User)
                {
                    UID = myinfo.Id.Value.ToString();
                }
                accesstoken = value;
                if (Cache is Setting)
                {
                    Cache.accesstoken = accesstoken;
                    Cache.username = username;
                    Cache.password = password;
                    Cache.uid = uid;
                }
            }
        }

        private string refreshtoken = string.Empty;
        public string RefreshToken
        {
            get { return (Cache is Setting ? Cache.refreshtoken : refreshtoken); }
            set
            {
                refreshtoken = value;
                if (Cache is Setting) Cache.refreshtoken = refreshtoken;
            }
        }

        private bool save_user_pass = false;
        public bool SaveUserPass
        {
            get { return (Cache is Setting ? Cache.save_user_pass : save_user_pass); }
            set
            {
                save_user_pass = value;
                if (Cache is Setting) Cache.save_user_pass = save_user_pass;
            }
        }
        [JsonProperty(nameof(User))]
        public string Username
        {
            get
            {
                if (SaveUserPass && IsConfigBusy) return Cache is Setting ? Cache.username : username;
                else return (string.Empty);
            }
            set
            {
                if (SaveUserPass)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        username = value;
                        if (Cache is Setting) Cache.username = username;
                    }
                }
            }
        }

        [JsonProperty(nameof(Pass))]
        public string Password
        {
            get
            {
                if (SaveUserPass && IsConfigBusy) return Cache is Setting ? Cache.password : password;
                else return (string.Empty);
            }
            set
            {
                if (SaveUserPass)
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        password = value;
                        if (Cache is Setting) Cache.password = password;
                    }
                }
            }
        }

        private string username = string.Empty;
        [JsonIgnore]
        public string User
        {
            get { return (Cache is Setting ? Cache.username.AesDecrypt(accesstoken) : username.AesDecrypt(accesstoken)); }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    username = value.AesEncrypt(accesstoken);
                    if (Cache is Setting) Cache.username = username;
                }
            }
        }

        private string password = string.Empty;
        [JsonIgnore]
        public string Pass
        {
            get { return (Cache is Setting ? Cache.password.AesDecrypt(accesstoken) : password.AesDecrypt(accesstoken)); }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    password = value.AesEncrypt(accesstoken);
                    if (Cache is Setting) Cache.password = password;
                }
            }
        }

        private Pixeez.Objects.User myinfo = null;
        [JsonIgnore]
        public Pixeez.Objects.User MyInfo
        {
            get { return (Cache is Setting ? Cache.myinfo : myinfo); }
            set
            {
                myinfo = value;
                if (value is Pixeez.Objects.User)
                {
                    UID = myinfo.Id.Value.ToString();
                }
                if (Cache is Setting)
                {
                    Cache.myinfo = myinfo;
                    Cache.uid = uid;
                }
            }
        }

        private string uid = string.Empty;
        public string UID
        {
            get { return (Cache is Setting ? Cache.uid.AesDecrypt(accesstoken) : uid.AesDecrypt(accesstoken)); }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    uid = value.AesEncrypt(accesstoken);
                    if (Cache is Setting) Cache.uid = uid;
                }
            }
        }

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
            var ret = long.TryParse(UID, out luid);
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
            get { return (Cache is Setting ? Cache.update : update); }
            set
            {
                update = value;
                if (Cache is Setting) Cache.update = update;
            }
        }

        private DateTime exptime = DateTime.Now;
        public DateTime ExpTime
        {
            get { return (Cache is Setting ? Cache.exptime : exptime); }
            set
            {
                exptime = value;
                if (Cache is Setting) Cache.exptime = exptime;
            }
        }

        private int expdurtime = 3600;
        public int ExpiresIn
        {
            get { return (Cache is Setting ? Cache.expdurtime : expdurtime); }
            set
            {
                expdurtime = value;
                if (Cache is Setting) Cache.expdurtime = expdurtime;
            }
        }
        #endregion

        #region UI theme/font relative
        [JsonIgnore]
        private FontFamily fontfamily = SystemFonts.MessageFontFamily;
        [JsonIgnore]
        public FontFamily FontFamily { get { return (fontfamily); } }

        private bool using_custom_font = false;
        public bool UseCustomFont
        {
            get { return (Cache is Setting ? Cache.using_custom_font : using_custom_font); }
            set
            {
                using_custom_font = value;
                if (Cache is Setting) Cache.using_custom_font = using_custom_font;
            }
        }

        private string custom_fontname = string.Empty;
        public string FontName
        {
            get { return (Cache is Setting ? Cache.custom_fontname : custom_fontname); }
            set
            {
                custom_fontname = value;
                if (Cache is Setting) Cache.custom_fontname = custom_fontname;
            }
        }

        private string theme = string.Empty;
        [JsonProperty("Theme")]
        public string CurrentTheme
        {
            get { return (Cache is Setting ? Cache.theme : theme); }
            set
            {
                theme = value;
                if (Cache is Setting) Cache.theme = theme;
            }
        }

        private string accent = string.Empty;
        [JsonProperty("Accent")]
        public string CurrentAccent
        {
            get { return (Cache is Setting ? Cache.accent : accent); }
            set
            {
                accent = value;
                if (Cache is Setting) Cache.accent = accent;
            }
        }
        #endregion

        #region network relative
        private string proxy = string.Empty;
        public string Proxy
        {
            get { return (Cache is Setting ? Cache.proxy : proxy); }
            set
            {
                proxy = value;
                if (Cache is Setting) Cache.proxy = proxy;
            }
        }

        private List<string> proxy_bypass = new List<string>() { "127.0.0.1", "localhost", "0.0.0.0", "192.168.1.*", "10.0.0.*" };
        public List<string> ProxyBypass
        {
            get { return (Cache is Setting ? Cache.proxy_bypass : proxy_bypass); }
            set
            {
                proxy_bypass = value;
                if (Cache is Setting) Cache.proxy_bypass = proxy_bypass;
            }
        }

        private bool using_proxy = false;
        public bool UsingProxy
        {
            get { return (Cache is Setting ? Cache.using_proxy : using_proxy); }
            set
            {
                using_proxy = value;
                if (Cache is Setting) Cache.using_proxy = using_proxy;
            }
        }
        #endregion

        #region Download relative
        private bool download_by_api = true;
        public bool DownloadByAPI
        {
            get { return (Cache is Setting ? Cache.download_by_api : download_by_api); }
            set
            {
                download_by_api = value;
                if (Cache is Setting) Cache.download_by_api = download_by_api;
            }
        }

        private bool dowanload_using_proxy = false;
        public bool DownloadUsingProxy
        {
            get { return (Cache is Setting ? Cache.dowanload_using_proxy : dowanload_using_proxy); }
            set
            {
                dowanload_using_proxy = value;
                if (Cache is Setting) Cache.dowanload_using_proxy = dowanload_using_proxy;
            }
        }

        private int download_tasks_max_simultaneous = 20;
        public int DownloadMaxSimultaneous
        {
            get { return (Cache is Setting ? Cache.download_tasks_max_simultaneous : download_tasks_max_simultaneous); }
            set
            {
                download_tasks_max_simultaneous = Math.Min(50, Math.Max(1, value));
                if (Cache is Setting) Cache.download_tasks_max_simultaneous = download_tasks_max_simultaneous;
            }
        }

        private int download_tasks_simultaneous = 10;
        public int DownloadSimultaneous
        {
            get { return (Cache is Setting ? Cache.download_tasks_simultaneous : download_tasks_simultaneous); }
            set
            {
                download_tasks_simultaneous = Math.Min(50, Math.Max(1, value));
                if (Cache is Setting) Cache.download_tasks_simultaneous = download_tasks_simultaneous;
            }
        }

        private bool download_resume = true;
        public bool DownloadWithFailResume
        {
            get { return (Cache is Setting ? Cache.download_resume : download_resume); }
            set
            {
                download_resume = value;
                if (Cache is Setting) Cache.download_resume = download_resume;
            }
        }

        private int download_buffer_update_frequency = 30;
        public int DownloadBufferUpdateFrequency
        {
            get { return (Cache is Setting ? Cache.download_buffer_update_frequency : download_buffer_update_frequency); }
            set
            {
                download_buffer_update_frequency = value;
                if (Cache is Setting) Cache.download_buffer_update_frequency = download_buffer_update_frequency;
            }
        }

        private int download_http_timeout = 30;
        public int DownloadHttpTimeout
        {
            get { return (Cache is Setting ? Cache.download_http_timeout : download_http_timeout); }
            set
            {
                download_http_timeout = value;
                if (Cache is Setting) Cache.download_http_timeout = download_http_timeout;
            }
        }

        private int download_http_stream_block_size = 16384;
        public int DownloadHttpStreamBlockSize
        {
            get { return (Cache is Setting ? Cache.download_http_stream_block_size : download_http_stream_block_size); }
            set
            {
                download_http_stream_block_size = value;
                if (Cache is Setting) Cache.download_http_stream_block_size = download_http_stream_block_size;
            }
        }

        private int download_waiting_time = 5000;
        public int DownloadWaitingTime
        {
            get { return (Cache is Setting ? Cache.download_waiting_time : download_waiting_time); }
            set
            {
                download_waiting_time = value;
                if (Cache is Setting) Cache.download_waiting_time = download_waiting_time;
            }
        }

        private int download_timespan = 750;
        public int DownloadTimeSpan
        {
            get { return (Cache is Setting ? Cache.download_timespan : download_timespan); }
            set
            {
                download_timespan = value;
                if (Cache is Setting) Cache.download_timespan = download_timespan;
            }
        }

        private bool download_completed_toast = true;
        public bool DownloadCompletedToast
        {
            get { return (Cache is Setting ? Cache.download_completed_toast : download_completed_toast); }
            set
            {
                download_completed_toast = value;
                if (Cache is Setting) Cache.download_completed_toast = download_completed_toast;
            }
        }

        private bool download_completed_sound = true;
        public bool DownloadCompletedSound
        {
            get { return (Cache is Setting ? Cache.download_completed_sound : download_completed_sound); }
            set
            {
                download_completed_sound = value;
                if (Cache is Setting) Cache.download_completed_sound = download_completed_sound;
            }
        }

        private int download_completed_sound_elapsed = 60;
        public int DownloadCompletedSoundForElapsedSeconds
        {
            get { return (Cache is Setting ? Cache.download_completed_sound_elapsed : download_completed_sound_elapsed); }
            set
            {
                download_completed_sound_elapsed = value;
                if (Cache is Setting) Cache.download_completed_sound_elapsed = download_completed_sound_elapsed;
            }
        }

        private bool download_attach_metainfo = false;
        public bool DownloadAttachMetaInfo
        {
            get { return (Cache is Setting ? Cache.download_attach_metainfo : download_attach_metainfo); }
            set
            {
                download_attach_metainfo = value;
                if (Cache is Setting) Cache.download_attach_metainfo = download_attach_metainfo;
            }
        }

        [JsonIgnore]
        private string lastfolder = string.Empty;
        [JsonIgnore]
        public string LastFolder
        {
            get { return (Cache is Setting ? Cache.lastfolder : lastfolder); }
            set
            {
                lastfolder = value;
                if (Cache is Setting) Cache.lastfolder = lastfolder;
                if (LocalStorage.Count(o => o.Folder.Equals(lastfolder)) <= 0)
                {
                    LocalStorage.Add(new StorageType(lastfolder, true));
                    lastfolder.AddDownloadedWatcher();
                }
            }
        }
        #endregion

        #region History relative
        private int history_max = 150;
        public int HistoryMax
        {
            get { return (Cache is Setting ? Cache.history_max : history_max); }
            set
            {
                history_max = value;
                if (Cache is Setting) Cache.history_max = history_max;
            }
        }

        private int history_limit = 100;
        public int HistoryLimit
        {
            get
            {
                history_limit = Math.Min(history_limit, history_max);
                if (history_limit < 0) history_limit = 100;
                return (history_limit);
            }
            set
            {
                if (value > 0) history_limit = Math.Min(value, history_max);
                else history_limit = 100;
                if (Cache is Setting) Cache.history_limit = history_limit;
            }
        }

        private int most_recents = 15;
        public int MostRecents
        {
            get { return (Cache is Setting ? Cache.most_recents : most_recents); }
            set
            {
                most_recents = value;
                if (Cache is Setting) Cache.most_recents = most_recents;
            }
        }
        #endregion

        #region Viewing relative
        private PixivPage default_page = PixivPage.Recommanded;
        [JsonConverter(typeof(StringEnumConverter))]
        public PixivPage DefaultPage
        {
            get { return (Cache is Setting ? Cache.default_page : default_page); }
            set
            {
                default_page = value;
                if (Cache is Setting) Cache.default_page = default_page;
            }
        }

        private AutoExpandMode auto_expand = AutoExpandMode.AUTO;
        public AutoExpandMode AutoExpand
        {
            get { return (Cache is Setting ? Cache.auto_expand : auto_expand); }
            set
            {
                auto_expand = value;
                if (Cache is Setting) Cache.auto_expand = auto_expand;
            }
        }

        private bool auto_convert_dpi = true;
        public bool AutoConvertDPI
        {
            get { return (Cache is Setting ? Cache.auto_convert_dpi : auto_convert_dpi); }
            set
            {
                auto_convert_dpi = value;
                if (Cache is Setting) Cache.auto_convert_dpi = auto_convert_dpi;
            }
        }

        private bool seamless_view = false;
        public bool SeamlessViewInMainWindow
        {
            get { return (Cache is Setting ? Cache.seamless_view : seamless_view); }
            set
            {
                seamless_view = value;
                if (Cache is Setting) Cache.seamless_view = seamless_view;
            }
        }

        private bool batch_clear_thumb = false;
        public bool BatchClearThumbnails
        {
            get { return (Cache is Setting ? Cache.batch_clear_thumb : batch_clear_thumb); }
            set
            {
                batch_clear_thumb = value;
                if (Cache is Setting) Cache.batch_clear_thumb = batch_clear_thumb;
            }
        }

        private bool show_user_bgimage = false;
        public bool ShowUserBackgroundImage
        {
            get { return (Cache is Setting ? Cache.show_user_bgimage : show_user_bgimage); }
            set
            {
                show_user_bgimage = value;
                if (Cache is Setting) Cache.show_user_bgimage = show_user_bgimage;
            }
        }

        private bool smart_show_preview = true;
        public bool SmartPreview
        {
            get { return (Cache is Setting ? Cache.smart_show_preview : smart_show_preview); }
            set
            {
                smart_show_preview = value;
                if (Cache is Setting) Cache.smart_show_preview = smart_show_preview;
            }
        }

        private int preview_min_width = 360;
        public int PreviewUsingLargeMinWidth
        {
            get { return (Cache is Setting ? Cache.preview_min_width : preview_min_width); }
            set
            {
                preview_min_width = value;
                if (Cache is Setting) Cache.preview_min_width = preview_min_width;
            }
        }

        private int preview_min_height { get; set; } = 360;
        public int PreviewUsingLargeMinHeight
        {
            get { return (Cache is Setting ? Cache.preview_min_height : preview_min_height); }
            set
            {
                preview_min_height = value;
                if (Cache is Setting) Cache.preview_min_height = preview_min_height;
            }
        }

        private bool smart_mouse_response = true;
        public bool SmartMouseResponse
        {
            get { return (Cache is Setting ? Cache.smart_mouse_response : smart_mouse_response); }
            set
            {
                smart_mouse_response = value;
                if (Cache is Setting) Cache.smart_mouse_response = smart_mouse_response;
            }
        }

        private bool enabled_mini_toolbar = true;
        public bool EnabledMiniToolbar
        {
            get { return (Cache is Setting ? Cache.enabled_mini_toolbar : enabled_mini_toolbar); }
            set
            {
                enabled_mini_toolbar = value;
                if (Cache is Setting) Cache.enabled_mini_toolbar = enabled_mini_toolbar;
            }
        }

        private bool prefetch_preview = true;
        public bool PrefetchingPreview
        {
            get { return (Cache is Setting ? Cache.prefetch_preview : prefetch_preview); }
            set
            {
                prefetch_preview = value;
                if (Cache is Setting) Cache.prefetch_preview = prefetch_preview;
            }
        }

        private bool prefetch_pages_thumb = true;
        public bool PrefetchingPagesThumb
        {
            get { return (Cache is Setting ? Cache.prefetch_pages_thumb : prefetch_pages_thumb); }
            set
            {
                prefetch_pages_thumb = value;
                if (Cache is Setting) Cache.prefetch_pages_thumb = prefetch_pages_thumb;
            }
        }

        private bool prefetch_pages_preview = false;
        public bool PrefetchingPagesPreview
        {
            get { return (Cache is Setting ? Cache.prefetch_pages_preview : prefetch_pages_preview); }
            set
            {
                prefetch_pages_preview = value;
                if (Cache is Setting) Cache.prefetch_pages_preview = prefetch_pages_preview;
            }
        }

        private int prefetch_download_parallel = 5;
        public int PrefetchingDownloadParallel
        {
            get { return (Cache is Setting ? Cache.prefetch_download_parallel : prefetch_download_parallel); }
            set
            {
                prefetch_download_parallel = value;
                if (Cache is Setting) Cache.prefetch_download_parallel = prefetch_download_parallel;
            }
        }

        private bool parallel_prefetching = true;
        public bool ParallelPrefetching
        {
            get { return (Cache is Setting ? Cache.parallel_prefetching : parallel_prefetching); }
            set
            {
                parallel_prefetching = value;
                if (Cache is Setting) Cache.parallel_prefetching = parallel_prefetching;
            }
        }
        #endregion

        #region Favorite/Follow relative
        private bool private_fav_prefer = false;
        public bool PrivateFavPrefer
        {
            get { return (Cache is Setting ? Cache.private_fav_prefer : private_fav_prefer); }
            set
            {
                private_fav_prefer = value;
                if (Cache is Setting) Cache.private_fav_prefer = private_fav_prefer;
            }
        }

        private bool private_bookmark_prefer = false;
        public bool PrivateBookmarkPrefer
        {
            get { return (Cache is Setting ? Cache.private_bookmark_prefer : private_bookmark_prefer); }
            set
            {
                private_bookmark_prefer = value;
                if (Cache is Setting) Cache.private_bookmark_prefer = private_bookmark_prefer;
            }
        }

        private bool toggle_fav_bookmark_state = false;
        public bool ToggleFavBookmarkState
        {
            get { return (Cache is Setting ? Cache.toggle_fav_bookmark_state : toggle_fav_bookmark_state); }
            set
            {
                toggle_fav_bookmark_state = value;
                if (Cache is Setting) Cache.toggle_fav_bookmark_state = toggle_fav_bookmark_state;
            }
        }
        #endregion

        #region Selection behavior relative
        private bool open_with_selection_order = true;
        public bool OpenWithSelectionOrder
        {
            get { return (Cache is Setting ? Cache.open_with_selection_order : open_with_selection_order); }
            set
            {
                open_with_selection_order = value;
                if (Cache is Setting) Cache.open_with_selection_order = open_with_selection_order;
            }
        }

        private bool all_for_selection_none = false;
        public bool AllForSelectionNone
        {
            get { return (Cache is Setting ? Cache.all_for_selection_none : all_for_selection_none); }
            set
            {
                all_for_selection_none = value;
                if (Cache is Setting) Cache.all_for_selection_none = all_for_selection_none;
            }
        }
        #endregion

        #region Speech relative
        private Dictionary<string, string> speech_names = SpeechTTS.GetNames();
        public Dictionary<string, string> SpeechPreferList
        {
            get { return (Cache is Setting ? Cache.speech_names : speech_names); }
            set
            {
                speech_names = value;
                if (Cache is Setting) Cache.speech_names = speech_names;
            }
        }

        private bool speech_mixedinline_alt = false;
        public bool SpeechAltPlayMixedCulture
        {
            get { return (Cache is Setting ? Cache.speech_mixedinline_alt : speech_mixedinline_alt); }
            set
            {
                speech_mixedinline_alt = value;
                if (Cache is Setting) Cache.speech_mixedinline_alt = speech_mixedinline_alt;
            }
        }

        private bool speech_autoslowrepeat = false;
        public bool SpeechAutoChangeSpeedWhenRepeatPlay
        {
            get { return (Cache is Setting ? Cache.speech_autoslowrepeat : speech_autoslowrepeat); }
            set
            {
                speech_autoslowrepeat = value;
                if (Cache is Setting) Cache.speech_autoslowrepeat = speech_autoslowrepeat;
            }
        }

        private bool speech_mainlandfirst = true;
        public bool SpeechChineseSimplifiedPrefer
        {
            get { return (Cache is Setting ? Cache.speech_mainlandfirst : speech_mainlandfirst); }
            set
            {
                speech_mainlandfirst = value;
                if (Cache is Setting) Cache.speech_mainlandfirst = speech_mainlandfirst;
            }
        }

        private bool speech_simleculture = true;
        public bool SpeechSimpleDetectCulture
        {
            get { return (Cache is Setting ? Cache.speech_simleculture : speech_simleculture); }
            set
            {
                speech_simleculture = value;
                if (Cache is Setting) Cache.speech_simleculture = speech_simleculture;
            }
        }

        private bool speech_mixedinline = false;
        public bool SpeechPlayMixedCultureInline
        {
            get { return (Cache is Setting ? Cache.speech_mixedinline : speech_mixedinline); }
            set
            {
                speech_mixedinline = value;
                if (Cache is Setting) Cache.speech_mixedinline = speech_mixedinline;
            }
        }

        private int speech_volumn = 100;
        public int SpeechPlayVolume
        {
            get { return (Cache is Setting ? Cache.speech_volumn : speech_volumn); }
            set
            {
                speech_volumn = Math.Max(0, Math.Min(value, 100));
                if (Cache is Setting) Cache.speech_volumn = speech_volumn;
                Speech.PlayVolume = speech_volumn;
            }
        }

        private int speech_rate = 0;
        public int SpeechPlayNormalRate
        {
            get { return (Cache is Setting ? Cache.speech_rate : speech_rate); }
            set
            {
                speech_rate = Math.Max(-10, Math.Min(value, 10));
                if (Cache is Setting) Cache.speech_rate = speech_rate;
                Speech.PlayNormalRate = speech_rate;
            }
        }

        private int speech_rateslow = -5;
        public int SpeechPlaySlowRate
        {
            get { return (Cache is Setting ? Cache.speech_rateslow : speech_rateslow); }
            set
            {
                speech_rateslow = Math.Max(-10, Math.Min(value, 10));
                if (Cache is Setting) Cache.speech_rateslow = speech_rateslow;
                Speech.PlaySlowRate = speech_rateslow;
            }
        }
        #endregion

        #region Shell bridge relative
        private string shell_search_app = "PixivWPFSearch.exe";
        public string ShellSearchBridgeApplication
        {
            get { return (Cache is Setting ? Cache.shell_search_app : shell_search_app); }
            set
            {
                shell_search_app = value;
                if (Cache is Setting) Cache.shell_search_app = shell_search_app;
            }
        }

        private string shell_pedia_app = "nw.exe";
        public string ShellPixivPediaApplication
        {
            get { return (Cache is Setting ? Cache.shell_pedia_app : shell_pedia_app); }
            set
            {
                shell_pedia_app = value;
                if (Cache is Setting) Cache.shell_pedia_app = shell_pedia_app;
            }
        }

        private string shell_pedia_args = "--single-process --enable-node-worker --app-shell-host-window-size=1280x720";
        public string ShellPixivPediaApplicationArgs
        {
            get { return (Cache is Setting ? Cache.shell_pedia_args : shell_pedia_args); }
            set
            {
                shell_pedia_args = value;
                if (Cache is Setting) Cache.shell_pedia_args = shell_pedia_args;
            }
        }

        private string shell_image_viewer = string.Empty;
        public string ShellImageViewer
        {
            get { return (Cache is Setting ? Cache.shell_image_viewer : shell_image_viewer); }
            set
            {
                shell_image_viewer = value;
                if (Cache is Setting) Cache.shell_image_viewer = shell_image_viewer;
            }
        }
        private string shell_image_viewer_cmd = string.Empty;
        [JsonIgnore]
        public string ShellImageViewerCmd
        {
            get { return (Cache is Setting ? Cache.shell_image_viewer_cmd : shell_image_viewer_cmd); }
            set
            {
                shell_image_viewer_cmd = value;
                if (Cache is Setting) Cache.shell_image_viewer_cmd = shell_image_viewer_cmd;
            }
        }

        private string shell_image_viewer_params = string.Empty;
        public string ShellImageViewerParams
        {
            get { return (Cache is Setting ? Cache.shell_image_viewer_params : shell_image_viewer_params); }
            private set
            {
                shell_image_viewer_params = value;
                if (Cache is Setting) Cache.shell_image_viewer_params = shell_image_viewer_params;
            }
        }

        private bool shell_image_viewer_enabled  = false;
        public bool ShellImageViewerEnabled
        {
            get { return (Cache is Setting ? Cache.shell_image_viewer_enabled : shell_image_viewer_enabled); }
            set
            {
                shell_image_viewer_enabled = value;
                if (Cache is Setting) Cache.shell_image_viewer_enabled = shell_image_viewer_enabled;
            }
        }

        private string shell_log_viewer = string.Empty;
        public string ShellLogViewer
        {
            get { return (Cache is Setting ? Cache.shell_log_viewer : shell_log_viewer); }
            set
            {
                shell_log_viewer = value;
                if (Cache is Setting) Cache.shell_log_viewer = shell_log_viewer;
            }
        }

        private string shell_log_viewer_params = string.Empty;
        public string ShellLogViewerParams
        {
            get { return (Cache is Setting ? Cache.shell_log_viewer_params : shell_log_viewer_params); }
            set
            {
                shell_log_viewer_params = value;
                if (Cache is Setting) Cache.shell_log_viewer_params = shell_log_viewer_params;
            }
        }

        private string shell_text_viewer = string.Empty;
        public string ShellTextViewer
        {
            get { return (Cache is Setting ? Cache.shell_text_viewer : shell_text_viewer); }
            set
            {
                shell_text_viewer = value;
                if (Cache is Setting) Cache.shell_text_viewer = shell_text_viewer;
            }
        }

        private string shell_text_viewer_params = string.Empty;
        public string ShellTextViewerParams
        {
            get { return (Cache is Setting ? Cache.shell_text_viewer_params : shell_text_viewer_params); }
            set
            {
                shell_text_viewer_params = value;
                if (Cache is Setting) Cache.shell_text_viewer_params = shell_text_viewer_params;
            }
        }

        #endregion

        #region Window relative
        private Point dropbox_pos = new Point(0, 0);
        public Point DropBoxPosition
        {
            get { return (Cache is Setting ? Cache.dropbox_pos : dropbox_pos); }
            set
            {
                dropbox_pos = value;
                if (Cache is Setting) Cache.dropbox_pos = dropbox_pos;
            }
        }

        private Rect downloadmanager_pos = new Rect(0, 0, 0, 0);
        public Rect DownloadManagerPosition
        {
            get { return (Cache is Setting ? Cache.downloadmanager_pos : downloadmanager_pos); }
            set
            {
                downloadmanager_pos = value;
                if (Cache is Setting) Cache.downloadmanager_pos = downloadmanager_pos;
            }
        }

        private int toast_delay = 5;
        public int ToastShowTimes
        {
            get { return (Cache is Setting ? Cache.toast_delay : toast_delay); }
            set
            {
                toast_delay = Math.Max(1, Math.Min(value, 100));
                if (Cache is Setting) Cache.toast_delay = toast_delay;
            }
        }
        #endregion

        #region Template relative
        private DateTime contents_template_time = new DateTime(0);
        public DateTime ContentsTemplateTime
        {
            get { return (Cache is Setting ? Cache.contents_template_time : contents_template_time); }
            set
            {
                contents_template_time = value;
                if (Cache is Setting) Cache.contents_template_time = contents_template_time;
            }
        }

        private string contents_templete = string.Empty;
        public string ContentsTemplete
        {
            get { return (Cache is Setting ? Cache.contents_templete : contents_templete); }
            set
            {
                contents_templete = value;
                if (Cache is Setting) Cache.contents_templete = contents_templete;
            }
        }

        private string custom_contents_templete = string.Empty;
        [JsonIgnore]
        public string CustomContentsTemplete
        {
            get { return (Cache is Setting ? Cache.custom_contents_templete : custom_contents_templete); }
            set
            {
                custom_contents_templete = value;
                if (Cache is Setting) Cache.custom_contents_templete = custom_contents_templete;
            }
        }

        public static void UpdateContentsTemplete()
        {
            if (Cache is Setting && !IsConfigBusy && ContentsTemplateReadWrite.Wait(0))
            {
                try
                {
                    if (File.Exists(Cache.ContentsTemplateFile))
                    {
                        var ft = new FileInfo(Cache.ContentsTemplateFile);
                        if (ft.LastWriteTime.DeltaSeconds(Cache.ContentsTemplateTime) > 10 || ft.CreationTime.DeltaSeconds(Cache.ContentsTemplateTime) > 10)
                        {
                            if (Cache.ContentsTemplateFile.WaitFileUnlock())
                            {
                                Cache.CustomContentsTemplete = File.ReadAllText(Cache.ContentsTemplateFile);
                                Cache.ContentsTemplete = Cache.CustomContentsTemplete;
                                Cache.ContentsTemplateTime = ft.LastWriteTime;
                                Cache.Save();
                                CommonHelper.UpdateWebContentAsync();
                                "ContentsTemplete Reloaded".ShowToast("INFO");
                            }
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(Cache.CustomContentsTemplete)) Cache.CustomContentsTemplete = string.Empty;
                        Cache.ContentsTemplete = CommonHelper.GetDefaultTemplate();
                        Cache.ContentsTemplateTime = DateTime.Now;
                        Cache.Save();
                        CommonHelper.UpdateWebContentAsync();
                        "ContentsTemplete Reset To Default".ShowToast("INFO");
                    }
                }
                catch (Exception ex) { ex.ERROR("UpdateContentsTemplete"); }
                finally
                {
                    if (ContentsTemplateReadWrite is SemaphoreSlim && ContentsTemplateReadWrite.CurrentCount <= 0) ContentsTemplateReadWrite.Release();
                }
            }
        }
        #endregion

        #region Storage monitor relative
        private string save_folder = string.Empty;
        private string SaveFolder
        {
            get { return (Cache is Setting ? Cache.save_folder : save_folder); }
            set
            {
                save_folder = value;
                if (Cache is Setting) Cache.save_folder = save_folder;
            }
        }

        private List<StorageType> local_storage = new List<StorageType>();
        public List<StorageType> LocalStorage
        {
            get { return ((Cache is Setting ? Cache.local_storage : local_storage)); }
            set
            {
                local_storage = value;
                if (Cache is Setting) Cache.local_storage = local_storage;
            }
        }
        #endregion
    }
}
