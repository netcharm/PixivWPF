using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using Newtonsoft.Json;

namespace PixivWPF.Common
{
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

        public bool NoConfirmExit { get; set; } = true;
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
            set { tagsfile = Path.GetFileName(value); }
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

        private static string last_opened = "last_opened.json";
        public string LastOpenedFile
        {
            get
            {
                if (IsConfigBusy) return (last_opened);
                else return (Path.IsPathRooted(last_opened) ? last_opened : Path.Combine(AppPath, last_opened));
            }
            set { last_opened = Path.GetFileName(value); }
        }

        private static string custom_template_file = "contents-template.html";
        public string ContentsTemplateFile
        {
            get
            {
                if (IsConfigBusy) return (custom_template_file);
                else return (Path.IsPathRooted(custom_template_file) ? custom_template_file : Path.Combine(AppPath, custom_template_file));
            }
            set { custom_template_file = Path.GetFileName(value); }
        }

        public static void UpdateCache(Setting new_setting)
        {
            if (Cache is Setting && new_setting is Setting && new_setting.Update > 0)
            {
                if (Cache.NoConfirmExit != new_setting.NoConfirmExit)
                    Cache.NoConfirmExit = new_setting.NoConfirmExit;

                if (Cache.AutoExpand != new_setting.AutoExpand)
                    Cache.AutoExpand = new_setting.AutoExpand;

                if (Cache.DownloadByAPI != new_setting.DownloadByAPI)
                    Cache.DownloadByAPI = new_setting.DownloadByAPI;
                if (Cache.DownloadUsingProxy != new_setting.DownloadUsingProxy)
                    Cache.DownloadUsingProxy = new_setting.DownloadUsingProxy;
                if (Cache.DownloadHttpTimeout != new_setting.DownloadHttpTimeout)
                    Cache.DownloadHttpTimeout = new_setting.DownloadHttpTimeout;
                if (Cache.DownloadHttpStreamBlockSize != new_setting.DownloadHttpStreamBlockSize)
                    Cache.DownloadHttpStreamBlockSize = new_setting.DownloadHttpStreamBlockSize;               
                if (Cache.DownloadTimeSpan != new_setting.DownloadTimeSpan)
                    Cache.DownloadTimeSpan = new_setting.DownloadTimeSpan;
                if (Cache.DownloadWaitingTime != new_setting.DownloadWaitingTime)
                    Cache.DownloadWaitingTime = new_setting.DownloadWaitingTime;
                if (Cache.DownloadCompletedToast != new_setting.DownloadCompletedToast)
                    Cache.DownloadCompletedToast = new_setting.DownloadCompletedToast;
                if (Cache.DownloadCompletedSound != new_setting.DownloadCompletedSound)
                    Cache.DownloadCompletedSound = new_setting.DownloadCompletedSound;
                if (Cache.DownloadCompletedSoundForElapsedSeconds != new_setting.DownloadCompletedSoundForElapsedSeconds)
                    Cache.DownloadCompletedSoundForElapsedSeconds = new_setting.DownloadCompletedSoundForElapsedSeconds;

                if (Cache.PrivateFavPrefer != new_setting.PrivateFavPrefer)
                    Cache.PrivateFavPrefer = new_setting.PrivateFavPrefer;
                if (Cache.PrivateBookmarkPrefer != new_setting.PrivateBookmarkPrefer)
                    Cache.PrivateBookmarkPrefer = new_setting.PrivateBookmarkPrefer;

                if (Cache.OpenWithSelectionOrder != new_setting.OpenWithSelectionOrder)
                    Cache.OpenWithSelectionOrder = new_setting.OpenWithSelectionOrder;
                if (Cache.AllForSelectionNone != new_setting.AllForSelectionNone)
                    Cache.AllForSelectionNone = new_setting.AllForSelectionNone;

                if (Cache.SaveUserPass != new_setting.SaveUserPass)
                    Cache.SaveUserPass = new_setting.SaveUserPass;

                if (Cache.UsingProxy != new_setting.UsingProxy)
                    Cache.UsingProxy = new_setting.UsingProxy;
                if (!Cache.Proxy.Equals(new_setting.Proxy, StringComparison.CurrentCultureIgnoreCase))
                    Cache.Proxy = new_setting.Proxy;

                if (!Cache.CurrentTheme.Equals(new_setting.CurrentTheme, StringComparison.CurrentCultureIgnoreCase))
                    Cache.CurrentTheme = new_setting.CurrentTheme;
                if (!Cache.CurrentAccent.Equals(new_setting.CurrentAccent, StringComparison.CurrentCultureIgnoreCase))
                    Cache.CurrentAccent = new_setting.CurrentAccent;

                if (!Cache.FontName.Equals(new_setting.FontName, StringComparison.CurrentCultureIgnoreCase))
                    Cache.FontName = new_setting.FontName;

                if (!Cache.ShellSearchBridgeApplication.Equals(new_setting.ShellSearchBridgeApplication, StringComparison.CurrentCultureIgnoreCase))
                    Cache.ShellSearchBridgeApplication = new_setting.ShellSearchBridgeApplication;
                if (!Cache.ShellPixivPediaApplication.Equals(new_setting.ShellPixivPediaApplication, StringComparison.CurrentCultureIgnoreCase))
                    Cache.ShellPixivPediaApplication = new_setting.ShellPixivPediaApplication;
                if (!Cache.ShellPixivPediaApplicationArgs.Equals(new_setting.ShellPixivPediaApplicationArgs, StringComparison.CurrentCultureIgnoreCase))
                    Cache.ShellPixivPediaApplicationArgs = new_setting.ShellPixivPediaApplicationArgs;
                if (!Cache.ShellImageViewer.Equals(new_setting.ShellImageViewer, StringComparison.CurrentCultureIgnoreCase))
                    Cache.ShellImageViewer = new_setting.ShellImageViewer;
                if (!Cache.ShellImageViewerEnabled == new_setting.ShellImageViewerEnabled)
                    Cache.ShellImageViewerEnabled = new_setting.ShellImageViewerEnabled;

                if (!Cache.ContentsTemplateFile.Equals(new_setting.ContentsTemplateFile, StringComparison.CurrentCultureIgnoreCase))
                    Cache.ContentsTemplateFile = new_setting.ContentsTemplateFile;

                Cache.DropBoxPosition = new_setting.DropBoxPosition;
                Cache.DownloadManagerPosition = new_setting.DownloadManagerPosition;

                if (Cache.ShowUserBackgroundImage != new_setting.ShowUserBackgroundImage)
                    Cache.ShowUserBackgroundImage = new_setting.ShowUserBackgroundImage;

                if (Cache.HistoryLimit != new_setting.HistoryLimit && new_setting.HistoryLimit >= 0)
                    Cache.HistoryLimit = new_setting.HistoryLimit;
                if (Cache.HistoryMax != new_setting.HistoryMax && new_setting.HistoryMax >= 0)
                    Cache.HistoryMax = new_setting.HistoryMax;

                if (Cache.SpeechPreferList != new_setting.SpeechPreferList)
                    Cache.SpeechPreferList = new_setting.SpeechPreferList;
                if (Cache.SpeechChineseSimplifiedPrefer != new_setting.SpeechChineseSimplifiedPrefer)
                    Cache.SpeechChineseSimplifiedPrefer = new_setting.SpeechChineseSimplifiedPrefer;
                if (Cache.SpeechSimpleDetectCulture != new_setting.SpeechSimpleDetectCulture)
                    Cache.SpeechSimpleDetectCulture = new_setting.SpeechSimpleDetectCulture;
                if (Cache.SpeechPlayMixedCultureInline != new_setting.SpeechPlayMixedCultureInline)
                    Cache.SpeechPlayMixedCultureInline = new_setting.SpeechPlayMixedCultureInline;

                if (Cache.SpeechAltPlayMixedCulture != new_setting.SpeechAltPlayMixedCulture)
                    Cache.SpeechAltPlayMixedCulture = new_setting.SpeechAltPlayMixedCulture;
                if (Cache.SpeechAutoChangeSpeedWhenRepeatPlay != new_setting.SpeechAutoChangeSpeedWhenRepeatPlay)
                    Cache.SpeechAutoChangeSpeedWhenRepeatPlay = new_setting.SpeechAutoChangeSpeedWhenRepeatPlay;

                if (Cache.LocalStorage != new_setting.LocalStorage)
                    Cache.LocalStorage = new_setting.LocalStorage;
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
                        UpdateCache(this);

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

                        if (full)
                        {
                            IList<string> titles = Application.Current.OpenedWindowTitles();
                            var links = JsonConvert.SerializeObject(titles, Formatting.Indented);
                            File.WriteAllText(Cache.LastOpenedFile, links, new UTF8Encoding(true));
                        }
                    }
                }
                catch (Exception ex)
                {
                    ex.Message.ShowMessageBox("ERROR");
                }
                finally
                {
                    CanConfigWrite.Release();
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
                    var filetime = configfile.GetFileTime("m");
                    if (!File.Exists(configfile)) filetime = lastConfigUpdate + TimeSpan.FromSeconds(1);

                    if (!(Cache is Setting) || (force && lastConfigUpdate.DeltaMillisecond(filetime) > 250))
                    {
                        lastConfigUpdate = filetime;
                        if (File.Exists(configfile))
                        {
                            var dso = new JsonSerializerSettings(){ Error = (se, ev) => ev.ErrorContext.Handled = true };
                            var text = File.ReadAllText(configfile);
                            if (Cache is Setting && text.Length > 20)
                            {
                                var cache = JsonConvert.DeserializeObject<Setting>(text, dso);
                                UpdateCache(cache);
                            }
                            else
                            {
                                if (text.Length < 20)
                                    Cache = new Setting();
                                else
                                    Cache = JsonConvert.DeserializeObject<Setting>(text, dso);
                            }

                            if (Cache.LocalStorage.Count <= 0 && !string.IsNullOrEmpty(Cache.SaveFolder))
                                Cache.LocalStorage.Add(new StorageType(Cache.SaveFolder, true));

                            if (Cache.LocalStorage.Count <= 0 && !string.IsNullOrEmpty(Cache.LastFolder))
                                Cache.LocalStorage.Add(new StorageType(Cache.LastFolder, true));

                            Cache.LocalStorage.InitDownloadedWatcher();

                            #region Setup UI font
                            if (Cache.UseCustomFont && !string.IsNullOrEmpty(Cache.FontName))
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

                            #region Update Contents Template
                            UpdateContentsTemplete();
                            #endregion

                            #region Setting Speech TTS culture/play setting
                            SpeechTTS.SetCustomNames(Cache.SpeechPreferList);
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
                        }
                        if (loadtags) LoadTags(true, true);
                    }
                }
#if DEBUG
                catch (Exception ex) { ex.Message.ShowToast("ERROR"); }
#else
                catch (Exception) { }
#endif
                finally
                {
                    CanConfigRead.Release();
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

                        bool tags_changed = false;
                        if (all && File.Exists(default_tags))
                        {
                            try
                            {
                                var tags = File.ReadAllText(default_tags);
                                CommonHelper.TagsCache = JsonConvert.DeserializeObject<Dictionary<string, string>>(tags);
                                tags_changed = true;
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
                                tags_changed = true;
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
                                    tags_changed = true;
                                }
                            }
                            catch (Exception) { }
                        }
                        if (tags_changed) CommonHelper.UpdateIllustTagsAsync();
                    }
                }
                catch (Exception) { }
                finally
                {
                    TagsReadWrite.Release();
                }
            }
        }
        #endregion

        #region UI theme/font relative
        public bool UseCustomFont { get; set; } = false;
        public string FontName { get; set; } = string.Empty;
        [JsonIgnore]
        private FontFamily fontfamily = SystemFonts.MessageFontFamily;
        [JsonIgnore]
        public FontFamily FontFamily { get { return (fontfamily); } }

        private string theme = string.Empty;
        [JsonProperty("Theme")]
        public string CurrentTheme
        {
            get { return (theme); }
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
            get { return (accent); }
            set
            {
                accent = value;
                if (Cache is Setting) Cache.accent = accent;
            }
        }
        #endregion

        #region network relative
        public string Proxy { get; set; } = string.Empty;

        public bool UsingProxy { get; set; } = false;
        #endregion

        #region Pixiv account relative
        private string accesstoken = string.Empty;
        public string AccessToken
        {
            get { return (accesstoken); }
            set
            {
                if (!IsConfigBusy)
                {
                    username = User.AesEncrypt(value);
                    password = Pass.AesEncrypt(value);
                }
                if (myinfo is Pixeez.Objects.User)
                {
                    UID = myinfo.Id.Value.ToString().AesEncrypt(value);
                }
                accesstoken = value;
                if(Cache is Setting)
                {
                    Cache.accesstoken = accesstoken;
                    Cache.username = username;
                    Cache.password = password;
                    Cache.UID = UID;
                }
            }
        }

        private string refreshtoken = string.Empty;
        public string RefreshToken
        {
            get { return (refreshtoken); }
            set { refreshtoken = value; }
        }

        public bool SaveUserPass { get; set; } = false;
        [JsonProperty(nameof(User))]
        public string Username
        {
            get
            {
                if (SaveUserPass && IsConfigBusy) return username;
                else return (string.Empty);
            }
            set { if (SaveUserPass) username = value; }
        }

        [JsonProperty(nameof(Pass))]
        public string Password
        {
            get
            {
                if (SaveUserPass && IsConfigBusy) return password;
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
        #endregion

        #region Download relative
        public bool DownloadByAPI { get; set; } = true;
        public bool DownloadUsingProxy { get; set; } = false;
        public int DownloadHttpTimeout { get; set; } = 30;
        public int DownloadHttpStreamBlockSize { get; set; } = 16384;
        public int DownloadWaitingTime { get; set; } = 5000;
        public int DownloadTimeSpan { get; set; } = 750;
        public bool DownloadCompletedToast { get; set; } = true;
        public bool DownloadCompletedSound { get; set; } = true;
        public int DownloadCompletedSoundForElapsedSeconds { get; set; } = 60;
        [JsonIgnore]
        private string lastfolder = string.Empty;
        [JsonIgnore]
        public string LastFolder
        {
            get { return lastfolder; }
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
        public int max_history = 100;
        public int HistoryMax { get; set; } = 150;
        public int HistoryLimit
        {
            get
            {
                max_history = Math.Min(max_history, HistoryMax);
                if (max_history < 0) max_history = 100;
                return (max_history);
            }
            set
            {
                if (value > 0) max_history = Math.Min(value, HistoryMax);
                else max_history = 100;
                if (Cache is Setting) Cache.max_history = max_history;
            }
        }
        #endregion

        #region Viewing relative
        public AutoExpandMode AutoExpand { get; set; } = AutoExpandMode.AUTO;
        public bool ShowUserBackgroundImage { get; set; } = false;
        #endregion

        #region Favorite/Follow relative
        public bool PrivateFavPrefer { get; set; } = false;
        public bool PrivateBookmarkPrefer { get; set; } = false;
        #endregion

        #region Selection behavior relative
        public bool OpenWithSelectionOrder { get; set; } = true;
        public bool AllForSelectionNone { get; set; } = false;
        #endregion

        #region Speech relative
        private Dictionary<string, string> speech_names = SpeechTTS.GetNames();
        public Dictionary<string, string> SpeechPreferList
        {
            get { return (speech_names); }
            set { speech_names = value; }
        }
        public bool SpeechAltPlayMixedCulture { get; set; } = false;
        public bool SpeechAutoChangeSpeedWhenRepeatPlay { get; set; } = false;
        public bool SpeechChineseSimplifiedPrefer { get; set; } = true;
        public bool SpeechSimpleDetectCulture { get; set; } = true;
        public bool SpeechPlayMixedCultureInline { get; set; } = false;
        #endregion

        #region Shell bridge relative
        public string ShellSearchBridgeApplication { get; set; } = "PixivWPFSearch.exe";
        public string ShellPixivPediaApplication { get; set; } = "nw.exe";
        public string ShellPixivPediaApplicationArgs { get; set; } = "--single-process --enable-node-worker --app-shell-host-window-size=1280x720";
        public string ShellImageViewer { get; set; } = string.Empty;
        public bool ShellImageViewerEnabled { get; set; } = false;
        #endregion

        #region Window relative
        private Point dropbox_pos = new Point(0, 0);
        public Point DropBoxPosition
        {
            get { return (dropbox_pos); }
            set
            {
                dropbox_pos = value;
                if (Cache is Setting) Cache.dropbox_pos = dropbox_pos;
            }
        }

        private Rect downloadmanager_pos = new Rect(0, 0, 0, 0);
        public Rect DownloadManagerPosition
        {
            get { return (downloadmanager_pos); }
            set
            {
                downloadmanager_pos = value;
                if (Cache is Setting) Cache.downloadmanager_pos = downloadmanager_pos;
            }
        }
        #endregion

        #region Template relative
        public DateTime ContentsTemplateTime { get; set; } = new DateTime(0);
        public string ContentsTemplete { get; set; } = string.Empty;
        [JsonIgnore]
        public string CustomContentsTemplete { get; set; } = string.Empty;

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
                        CommonHelper.UpdateWebContentAsync();
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(Cache.CustomContentsTemplete))
                    {
                        Cache.ContentsTemplete = CommonHelper.GetDefaultTemplate();
                        Cache.CustomContentsTemplete = string.Empty;
                        Cache.Save();
                        CommonHelper.UpdateWebContentAsync();
                    }
                }
            }
        }
        #endregion

        #region Storage monitor relative
        [JsonIgnore]
        private string SaveFolder { get; set; }
        public List<StorageType> LocalStorage { get; set; } = new List<StorageType>();
        #endregion
    }
}
