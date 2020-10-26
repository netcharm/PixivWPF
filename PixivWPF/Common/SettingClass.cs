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

        public static void UpdateCache(Setting new_setting)
        {
            if (Cache is Setting && new_setting is Setting && new_setting.Update > 0)
            {
                if (Cache.NoConfirmExit != new_setting.NoConfirmExit)
                    Cache.NoConfirmExit = new_setting.NoConfirmExit;

                if (Cache.AutoExpand != new_setting.AutoExpand)
                    Cache.AutoExpand = new_setting.AutoExpand;
                if (Cache.SeamlessViewInMainWindow != new_setting.SeamlessViewInMainWindow)
                    Cache.SeamlessViewInMainWindow = new_setting.SeamlessViewInMainWindow;
                if (Cache.ShowUserBackgroundImage != new_setting.ShowUserBackgroundImage)
                    Cache.ShowUserBackgroundImage = new_setting.ShowUserBackgroundImage;
                if (Cache.PreviewUsingLargeMinWidth != new_setting.PreviewUsingLargeMinWidth)
                    Cache.PreviewUsingLargeMinWidth = new_setting.PreviewUsingLargeMinWidth;
                if (Cache.PreviewUsingLargeMinHeight != new_setting.PreviewUsingLargeMinHeight)
                    Cache.PreviewUsingLargeMinHeight = new_setting.PreviewUsingLargeMinHeight;

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

                    if (!(Cache is Setting) || (force && lastConfigUpdate.DeltaMilliseconds(filetime) > 250))
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
                        }
                        if (loadtags) LoadTags(true, true);
                    }
                }
#if DEBUG
                catch (Exception ex) { ex.Message.ShowToast("ERROR"); }
#else
                catch (Exception ex) { ex.Message.DEBUG(); }
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

                    if (force && lastTagsUpdate.DeltaMilliseconds(filetime) > 5)
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
                            catch (Exception ex) { ex.Message.DEBUG(); }
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
                            catch (Exception ex) { ex.Message.DEBUG(); }
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
                            catch (Exception ex) { ex.Message.DEBUG(); }
                        }
                        if (tags_changed) CommonHelper.UpdateIllustTagsAsync();
                    }
                }
                catch (Exception ex) { ex.Message.DEBUG(); }
                finally
                {
                    TagsReadWrite.Release();
                }
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
                    username = value;
                    if (Cache is Setting) Cache.username = username;
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
                    password = value;
                    if (Cache is Setting) Cache.password = password;
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
                username = value.AesEncrypt(accesstoken);
                if (Cache is Setting) Cache.username = username;
            }
        }

        private string password = string.Empty;
        [JsonIgnore]
        public string Pass
        {
            get { return (Cache is Setting ? Cache.password.AesDecrypt(accesstoken) : password.AesDecrypt(accesstoken)); }
            set { password = value.AesEncrypt(accesstoken);
                if (Cache is Setting) Cache.password = password;
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
                uid = value.AesEncrypt(accesstoken);
                if (Cache is Setting) Cache.uid = uid;
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
        #endregion

        #region Viewing relative
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
            get { return (Cache is Setting ? Cache.local_storage : local_storage); }
            set
            {
                local_storage = value.Distinct().ToList();
                if (Cache is Setting) Cache.local_storage = local_storage;
            }
        }
        #endregion
    }
}
