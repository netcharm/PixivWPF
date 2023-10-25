using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PixivWPF.Common
{
    public class SpeechTTS : IDisposable
    {
        #region Culture routines
        public CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;

        public static bool ChineseSimplifiedPrefer { get; set; } = true;

        #region Regex Patterns for culture detect/slice
        //
        // 中文：[\u4e00-\u9fcc, \u3400-\u4db5, \u20000-\u2a6d6, \u2a700-\u2b734, \u2b740-\u2b81d, \uf900-\ufad9, \u2f800-\u2fa1d]
        // 繁体标点: [\u3000-\u3003, \u3008-\u300F, \u3010-\u3011, \u3014-\u3015, \u301C-\u301E]
        // BIG-5: [\ue000-\uf848]
        // 日文：[\u0800-\u4e00] [\u3041-\u31ff]
        // 韩文：[\uac00-\ud7ff]
        //
        //private string pattern_zh = @"[\u3000-\u303f\ufe00-\ufe4f\u3400-\u4db5\u4e00-\u9fcc\uf900-\ufad9\u20000-\u2cea1]+?";
        private static string pattern_zhs = @"([\u3000-\u303f\ufe00-\ufe4f\uff01-\uffee\u3400-\u4db5\u4e00-\u9fcc\uf900-\ufad9\uff61-\uff64]|([\ud840-\ud873][\udc00-\udfff]))+";
        private static string pattern_zht = @"[\u3000-\u3003\u3008-\u300F\u3010-\u3011\u3014-\u3015\u301C-\u301E\u3105-\u31ba\ua140-\ua3bf\ua440-\uc67e\uc940-\uf9d5\ue000-\uf848]+";
        //private static string pattern_ja = @"[\u0021-\u024f\u0250-\u02af\u0391-\u03d9\u1f00-\u1ffe\u1e00-\u1eff\u2010-\u205e\u2e00-\u2e3b\u2c60-\u2c7f\ua720-\ua7ff]+";
        private static string pattern_ja = @"([\u3041-\u309f\u30a0-\u31ff\u3220-\u325f\u3280-\u32ff\u3300-\u4077\uff65-\uff9f]|(\ud83c[\ude01-\ude51]))+";
        private static string pattern_ko = @"[\u1100-\u11ff\u3131-\u318e\u3200-\u321f\u3260-\u327f\ua960-\ua97c\uac00-\ud7a3\uffa0-\uffdf]+";
        private static string pattern_en = @"[\u0020-\u007e\u0080-\u02af\u0391-\u03d9\u1f00-\u1ffe\u1e00-\u1eff\u2010-\u205e\u2e00-\u2e3b\u2c60-\u2c7f\ua720-\ua7ff]+";
        private static string pattern_dt = @"^[\d: tTzZ+\-\/\\]{4,}$";
        private static string pattern_digit = @"^\d+$";
        //private static string pattern_emoji = @"[\u2190-\u27bf\u3400-\u4dbf\u4dc0-\u4dff\uf900-\ufad9\u1d300-\u1d356\u1f000-\u1f02b\u1f030-\u1f093\u1f0a0-\u1f0f5\u1f300-\u1f5ff]+";
        private static string pattern_emoji = @"([\u2190-\u27bf]|(\ud834[\udf00-\udf56])|(\ud83c[\udc00-\udfff]))+?";
        private static string pattern_symbol = @"[\u0021-\u0040\u005b-\u0060\u007b-\u00ff\u2010-\u2e3b\uff61-\uff64\uffeb-\uffef]+";
        #endregion

        #region Required Encoding types
        //var m_jp = Regex.Matches(text, @"([\u0800-\u4e00])", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        //var m_zh = Regex.Matches(text, @"([\u4e00-\u9fbb])", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private static Encoding GBK = Encoding.GetEncoding("GBK");
        private static Encoding BIG5 = Encoding.GetEncoding("BIG5");
        private static Encoding JAP = Encoding.GetEncoding("Shift-JIS");
        private static Encoding UTF8 = Encoding.UTF8;
        #endregion

        internal protected static CultureInfo FindCultureByName(string lang)
        {
            CultureInfo culture = null;
            #region Microsoft Language define lite
            //{ "unk","AutoDetect"},
            //{ "zh-Hans","ChineseSimplified"},
            //{ "zh-Hant","ChineseTraditional"},
            //{ "cs","Czech"},
            //{ "da","Danish"},
            //{ "nl","Dutch"},
            //{ "en","English"},
            //{ "fi","Finnish"},
            //{ "fr","French"},
            //{ "de","German"},
            //{ "el","Greek"},
            //{ "hu","Hungarian"},
            //{ "it","Italian"},
            //{ "ja","Japanese"},
            //{ "ko","Korean"},
            //{ "nb","Norwegian"},
            //{ "pl","Polish"},
            //{ "pt","Portuguese"},
            //{ "ru","Russian"},
            //{ "es","Spanish"},
            //{ "sv","Swedish"},
            //{ "tr","Turkish"},
            //{ "ar","Arabic"},
            //{ "ro","Romanian"},
            //{ "sr-Cyrl","SerbianCyrillic"},
            //{ "sr-Latn","SerbianLatin"},
            //{ "sk","Slovak"}
            #endregion
            try
            {
                if (string.IsNullOrEmpty(lang)) lang = "unk";
                else lang = lang.ToLower();

                CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.AllCultures & ~CultureTypes.NeutralCultures);
                CultureInfo[] specific = CultureInfo.GetCultures(CultureTypes.SpecificCultures &~ CultureTypes.UserCustomCulture);

                if (!lang.Equals("unk", StringComparison.CurrentCultureIgnoreCase))
                {
                    var ret = cultures.Where(c => (
                        c.IetfLanguageTag.Contains("-") && (
                        (c.IetfLanguageTag.ToLower().Equals(lang) ||
                        c.IetfLanguageTag.ToLower().StartsWith(lang)||
                        c.IetfLanguageTag.ToLower().EndsWith(lang)))
                    ));
                    if (ret.Count() > 0) culture = ret.FirstOrDefault();
                    if (culture.Name.Equals("zh-Hant") || lang.Equals("zh-Hant"))
                        culture = CultureInfo.GetCultureInfo("zh-TW");
                    else if (culture.Name.Equals("zh-Hans") || lang.Equals("zh-Hans"))
                        culture = CultureInfo.GetCultureInfo("zh-CN");
                    else if (culture.Name.Equals("ja") || lang.Equals("ja"))
                        culture = CultureInfo.GetCultureInfo("ja-JP");
                    else if (culture.Name.Equals("ko") || lang.Equals("ko"))
                        culture = CultureInfo.GetCultureInfo("ko-KR");
                    else if (culture.Name.Equals("en") || lang.Equals("en"))
                        culture = CultureInfo.GetCultureInfo("en-US");
                }

                if (culture is CultureInfo && culture.EnglishName.StartsWith("unk", StringComparison.CurrentCultureIgnoreCase))
                    culture = null;
            }
            catch (Exception ex) { ex.ERROR("SPEECH"); culture = null; }

            return (culture);
        }

        internal protected static CultureInfo DetectCulture(string text)
        {
            CultureInfo result = CultureInfo.CurrentCulture;

            var regex_opt = RegexOptions.Multiline;
            //var regex_opt = RegexOptions.Multiline | RegexOptions.IgnoreCase;

            if (Regex.IsMatch(text, pattern_dt, regex_opt))
            {
                //Console.WriteLine("Datetime");
                result = CultureInfo.CurrentCulture;
            }
            else if (Regex.IsMatch(text, pattern_ko, regex_opt))
            {
                //Console.WriteLine("korea");
                result = CultureInfo.GetCultureInfoByIetfLanguageTag("ko-KR");
            }
            else if (Regex.IsMatch(text, pattern_ja, regex_opt))
            {
                //Console.WriteLine("Japan");
                result = CultureInfo.GetCultureInfoByIetfLanguageTag("ja-JP");
            }
            else if (Regex.IsMatch(text, $@"^{pattern_en}?$", regex_opt))
            {
                //Console.WriteLine("English");
                result = CultureInfo.GetCultureInfoByIetfLanguageTag("en-US");
            }
            else if (ChineseSimplifiedPrefer ? GBK.GetString(GBK.GetBytes(text)).Equals(text) : BIG5.GetString(BIG5.GetBytes(text)).Equals(text))
            {
                //Console.WriteLine("Taiwan");
                result = ChineseSimplifiedPrefer ? CultureInfo.GetCultureInfoByIetfLanguageTag("zh-CN") : CultureInfo.GetCultureInfoByIetfLanguageTag("zh-TW");
            }
            else if (ChineseSimplifiedPrefer ? BIG5.GetString(BIG5.GetBytes(text)).Equals(text) : GBK.GetString(GBK.GetBytes(text)).Equals(text))
            {
                //Console.WriteLine("Mainland");
                result = ChineseSimplifiedPrefer ? CultureInfo.GetCultureInfoByIetfLanguageTag("zh-TW") : CultureInfo.GetCultureInfoByIetfLanguageTag("zh-CN");
            }
            else if (ChineseSimplifiedPrefer ? Regex.IsMatch(text, pattern_zhs, regex_opt) : Regex.IsMatch(text, pattern_zht, regex_opt))
            {
                //Console.WriteLine("Mainland-zh");
                result = ChineseSimplifiedPrefer ? CultureInfo.GetCultureInfoByIetfLanguageTag("zh-CN") : CultureInfo.GetCultureInfoByIetfLanguageTag("zh-TW");
            }
            else if (ChineseSimplifiedPrefer ? Regex.IsMatch(text, pattern_zht, regex_opt) : Regex.IsMatch(text, pattern_zhs, regex_opt))
            {
                //Console.WriteLine("Mainland-tw");
                result = ChineseSimplifiedPrefer ? CultureInfo.GetCultureInfoByIetfLanguageTag("zh-TW") : CultureInfo.GetCultureInfoByIetfLanguageTag("zh-CN");
            }
            else if (Regex.IsMatch(text, $@"^{pattern_emoji}$", regex_opt))
            {
                //Console.WriteLine("Emoji");
                result = CultureInfo.CurrentCulture;
            }
            else if (Regex.IsMatch(text, $@"^{pattern_symbol}$", regex_opt))
            {
                //Console.WriteLine("Symbol");
                result = CultureInfo.CurrentCulture;
            }
            else
            {
                result = CultureInfo.CurrentCulture;
            }
            return (result);
        }

        private IList<KeyValuePair<string, CultureInfo>> SlicingByCulture(string text, CultureInfo defautlCulture)
        {
            var result = new List<KeyValuePair<string, CultureInfo>>();

            if (defautlCulture == null) defautlCulture = CultureInfo.CurrentCulture;

            text = Regex.Replace(text, @"([\u3000-\u303f\ufe00-\ufe4f\uff01-\uffee])", "$1 ");

            var regex_opt = RegexOptions.Multiline | RegexOptions.IgnoreCase;
            var pattern = $@"((?'datetime'{pattern_dt})|(?'digit'{pattern_digit})|(?'ko'{pattern_ko})|(?'ja'{pattern_ja})|(?'symbol'{pattern_symbol})|(?'en'{pattern_en})|(?'emoji'{pattern_emoji})|(?'zht'{pattern_zht})|(?'zhs'{pattern_zhs}))";
            var mr = Regex.Matches(text, pattern, regex_opt);
            CultureInfo lastCulture = defautlCulture;
            StringBuilder sb = new StringBuilder();
            foreach (Match m in mr)
            {
                try
                {
                    if (m.Length <= 0) continue;
                    var str = m.Value.ToString().Trim();
                    var culture = defautlCulture;
                    if (m.Value.Equals(m.Groups["ko"].Value)) culture = CultureInfo.GetCultureInfoByIetfLanguageTag("ko-KR");
                    else if (m.Value.Equals(m.Groups["ja"].Value)) culture = CultureInfo.GetCultureInfoByIetfLanguageTag("ja-JP");
                    else if (m.Value.Equals(m.Groups["en"].Value)) culture = CultureInfo.GetCultureInfoByIetfLanguageTag("en-US");
                    else if (m.Value.Equals(m.Groups["zhs"].Value)) culture = CultureInfo.GetCultureInfoByIetfLanguageTag("zh-CN");
                    else if (m.Value.Equals(m.Groups["zht"].Value)) culture = CultureInfo.GetCultureInfoByIetfLanguageTag("zh-TW");
                    else if (m.Value.Equals(m.Groups["digit"].Value)) culture = CultureInfo.CurrentCulture;
                    else if (m.Value.Equals(m.Groups["symbol"].Value)) culture = CultureInfo.CurrentCulture;
                    else if (m.Value.Equals(m.Groups["emoji"].Value)) culture = CultureInfo.CurrentCulture;
                    else if (m.Value.Equals(m.Groups["datetime"].Value)) culture = CultureInfo.CurrentCulture;

                    //if (culture.IetfLanguageTag.StartsWith("zh-")) culture = DetectCulture(str);

                    if (culture.IetfLanguageTag.Equals(lastCulture.IetfLanguageTag) || sb.Length == 0)
                        sb.Append(str);
                    else if (culture.IetfLanguageTag.StartsWith("zh-") && lastCulture.IetfLanguageTag.StartsWith("ja-") && str.Length <= 2)
                    {
                        culture = lastCulture;
                        sb.Append(str);
                    }
                    else
                    {
                        result.Add(new KeyValuePair<string, CultureInfo>(sb.ToString(), lastCulture));
                        sb.Clear();
                        sb.Append(str);
                    }
                    lastCulture = culture;
                }
                catch (Exception ex) { ex.ERROR(); }
            }
            if (result.Count > 0 && !result.Last().Key.Equals(sb.ToString()))
                result.Add(new KeyValuePair<string, CultureInfo>(sb.ToString(), lastCulture));
            else if (result.Count == 0) result.Add(new KeyValuePair<string, CultureInfo>(text, lastCulture));
            return (result);
        }
        #endregion

        #region Voice routines
        private InstalledVoice GetVoice(CultureInfo culture)
        {
            InstalledVoice result = null;
            if (culture is CultureInfo)
            {
                foreach (InstalledVoice voice in synth.GetInstalledVoices())
                {
                    VoiceInfo info = voice.VoiceInfo;
                    var vl = info.Culture.IetfLanguageTag;
                    if (vl.Equals(culture.IetfLanguageTag, StringComparison.CurrentCultureIgnoreCase))
                    {
                        result = voice;
                        break;
                    }
                }
            }
            return (result);
        }

        private string GetVoiceName(CultureInfo culture)
        {
            string result = string.Empty;
            if (culture is CultureInfo)
            {
                foreach (InstalledVoice voice in synth.GetInstalledVoices())
                {
                    VoiceInfo info = voice.VoiceInfo;
                    var vl = info.Culture.IetfLanguageTag;
                    if (vl.Equals(culture.IetfLanguageTag, StringComparison.CurrentCultureIgnoreCase))
                    {
                        result = voice.VoiceInfo.Name;
                        break;
                    }
                }
            }
            return (result);
        }

        private string GetCustomVoiceName(CultureInfo culture)
        {
            string result = string.Empty;
            var nvs = GetVoiceNames();
            if (nvs.ContainsKey(culture))
            {
                foreach (var n in nametable[culture])
                {
                    var found = false;
                    foreach (var nl in nvs[culture])
                    {
                        var nll = nl.ToLower();
                        if (nll.Contains(n))
                        {
                            result = nl;
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }
            }
            if (string.IsNullOrEmpty(result)) result = GetVoiceName(culture);
            return (result);
        }

        private Dictionary<CultureInfo, List<string>> GetVoiceNames()
        {
            Dictionary<CultureInfo, List<string>> result = new Dictionary<CultureInfo, List<string>>();
            foreach (InstalledVoice voice in synth.GetInstalledVoices())
            {
                VoiceInfo info = voice.VoiceInfo;
                if (result.ContainsKey(info.Culture))
                {
                    var lang = info.Name.Split('-');
                    result[info.Culture].Add(info.Name);
                    if (lang.Length > 0) result[info.Culture].Add(lang[0]);
                    if (lang.Length > 1) result[info.Culture].Add(lang[1]);
                    if (lang.Length > 2) result[info.Culture].Add(lang[2]);
                    result[info.Culture].Sort();
                }
                else
                    result[info.Culture] = new List<string>() { info.Name };
            }
            return (result);
        }
        #endregion

        #region Speech Synthesis routines
        public SemaphoreSlim CanPlay = new SemaphoreSlim(1, 1);
        public static List<InstalledVoice> InstalledVoices { get; private set; } = null;
        private Queue<KeyValuePair<string, CultureInfo>> PlayQueue = new Queue<KeyValuePair<string, CultureInfo>>();

        public bool AutoChangeSpeechSpeed { get; set; } = true;
        public bool AltPlayMixedCulture { get; set; } = false;
        public bool PlayMixedCultureInline { get; set; } = false;
        public bool SimpleCultureDetect { get; set; } = true;

        public SynthesizerState State { get { return (synth is SpeechSynthesizer ? synth.State : SynthesizerState.Ready); } }

        public Action<SpeakStartedEventArgs> SpeakStarted { get; set; } = null;
        public Action<SpeakProgressEventArgs> SpeakProgress { get; set; } = null;
        public Action<StateChangedEventArgs> StateChanged { get; set; } = null;
        public Action<VoiceChangeEventArgs> VoiceChange { get; set; } = null;
        public Action<BookmarkReachedEventArgs> BookmarkReached { get; set; } = null;
        public Action<PhonemeReachedEventArgs> PhonemeReached { get; set; } = null;
        public Action<VisemeReachedEventArgs> VisemeReached { get; set; } = null;
        public Action<SpeakCompletedEventArgs> SpeakCompleted { get; set; } = null;

        public int PlayNormalRate { get; internal set; } = 0;
        public int PlaySlowRate { get; internal set; } = -5;
        public int PlayVolume { get; internal set; } = 100;

        private static Dictionary<CultureInfo, List<string>> nametable = new Dictionary<CultureInfo, List<string>>() {
            { CultureInfo.GetCultureInfo("zh-CN"), new List<string>() { "huihui", "yaoyao", "lili", "kangkang" } },
            { CultureInfo.GetCultureInfo("zh-TW"), new List<string>() { "hanhan", "yating", "zhiwei" } },
            { CultureInfo.GetCultureInfo("ja-JP"), new List<string>() { "haruka", "ayumi", "sayaka", "ichiro" } },
            { CultureInfo.GetCultureInfo("ko-KR"), new List<string>() { "heami" } },
            { CultureInfo.GetCultureInfo("en-US"), new List<string>() { "david", "zira", "mark", "eva" } }
        };

        private SpeechSynthesizer synth = null;
        private string voice_default = string.Empty;
        //private bool SPEECH_AUTO = false;
        private bool SPEECH_SLOW = false;
        private string SPEECH_TEXT = string.Empty;
        private CultureInfo SPEECH_CULTURE = null;

        private SpeechSynthesizer slice_synth = null;
        private List<string> slice_words = new List<string>();
        private int last_speak_text_pos = 0;
        private int last_speak_text_len = 0;        
        public bool IsSlicing { get; private set; } = false;

        public Action<string, List<string>> SetVoice { get; } = new Action<string, List<string>>((locale, names) => {
            if (names is List<string> && names.Count > 0)
            {
                try
                {
                    var culture = CultureInfo.GetCultureInfo(locale);
                    if (culture is CultureInfo) nametable[culture] = names;
                }
                catch { }
            }
        });

        public Func<Dictionary<string, List<string>>> GetVoices { get; } = new Func<Dictionary<string, List<string>>>(() => {
            var result = new Dictionary<string, List<string>>();
            if (nametable is Dictionary<CultureInfo, List<string>>)
            {
                foreach (var kv in nametable) result[kv.Key.IetfLanguageTag] = kv.Value;
            }
            return (result);
        });

        public static Dictionary<string, string> GetNames()
        {
            var result = nametable.Select(n => new KeyValuePair<string, string>(n.Key.IetfLanguageTag, string.Join(", ", n.Value)));
            return (result.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        public static Dictionary<CultureInfo, List<string>> SetNames(Dictionary<string, string> names)
        {
            var result = names.Select(n => new KeyValuePair<CultureInfo, List<string>>(CultureInfo.GetCultureInfo(n.Key.Trim()), n.Value.Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim()).ToList()));
            return (result.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        public static void SetCustomNames(Dictionary<string, string> names)
        {
            nametable = SetNames(names);
        }

        private void Synth_SpeakStarted(object sender, SpeakStartedEventArgs e)
        {
            if (synth == null) return;

            if (sender == slice_synth) IsSlicing = true;
            slice_words.Clear();
            last_speak_text_pos = 0;
            last_speak_text_len = 0;
            
            if (CancelRequested) { synth.SpeakAsyncCancel(e.Prompt); synth.SpeakAsyncCancelAll(); PlayQueue.Clear(); return; }

            if (SpeakStarted is Action<SpeakStartedEventArgs>) SpeakStarted.Invoke(e);
        }

        private void Synth_SpeakProgress(object sender, SpeakProgressEventArgs e)
        {
            if (synth == null) return;

            if (e.CharacterPosition > last_speak_text_pos || e.CharacterCount != last_speak_text_len)
            {
                slice_words.Add(e.Text);
                last_speak_text_pos = e.CharacterPosition;
                last_speak_text_len = e.CharacterCount;
            }

            if (e.Cancelled || CancelRequested) { synth.SpeakAsyncCancel(e.Prompt); synth.SpeakAsyncCancelAll(); PlayQueue.Clear(); return; }

            if (SpeakProgress is Action<SpeakProgressEventArgs>) SpeakProgress.Invoke(e);
        }

        private void Synth_StateChanged(object sender, StateChangedEventArgs e)
        {
            if (synth == null) return;

            if (CancelRequested) { synth.SpeakAsyncCancelAll(); PlayQueue.Clear(); return; }

            if (synth.State == SynthesizerState.Paused)
            {

            }
            else if (synth.State == SynthesizerState.Speaking)
            {

            }
            else if (synth.State == SynthesizerState.Ready)
            {

            }
            if (StateChanged is Action<StateChangedEventArgs>) StateChanged.Invoke(e);
        }

        private void Synth_VoiceChange(object sender, VoiceChangeEventArgs e)
        {
            if (synth == null) return;

            if (e.Cancelled || CancelRequested) { synth.SpeakAsyncCancel(e.Prompt); synth.SpeakAsyncCancelAll(); PlayQueue.Clear(); return; }

            if (VoiceChange is Action<VoiceChangeEventArgs>) VoiceChange.Invoke(e);
        }

        private void Synth_BookmarkReached(object sender, BookmarkReachedEventArgs e)
        {
            if (synth == null) return;

            if (e.Cancelled || CancelRequested) { synth.SpeakAsyncCancel(e.Prompt); synth.SpeakAsyncCancelAll(); PlayQueue.Clear(); return; }

            if (BookmarkReached is Action<BookmarkReachedEventArgs>) BookmarkReached.Invoke(e);
        }

        private void Synth_PhonemeReached(object sender, PhonemeReachedEventArgs e)
        {
            if (synth == null) return;

            if (e.Cancelled || CancelRequested) { synth.SpeakAsyncCancel(e.Prompt); synth.SpeakAsyncCancelAll(); PlayQueue.Clear(); return; }

            if (PhonemeReached is Action<PhonemeReachedEventArgs>) PhonemeReached.Invoke(e);
        }

        private void Synth_VisemeReached(object sender, VisemeReachedEventArgs e)
        {
            if (synth == null) return;

            if (e.Cancelled || CancelRequested) { synth.SpeakAsyncCancel(e.Prompt); synth.SpeakAsyncCancelAll(); PlayQueue.Clear(); return; }

            if (VisemeReached is Action<VisemeReachedEventArgs>) VisemeReached.Invoke(e);
        }

        private void Synth_SpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            if (synth == null) return;
            if (sender == slice_synth) IsSlicing = false;

            if (CanPlay is SemaphoreSlim && CanPlay.CurrentCount < 1) CanPlay.Release();
            CancelRequested = false;

            if (!e.Cancelled && PlayQueue.Count > 0)
            {
                if (CancelRequested)
                {
                    synth.SpeakAsyncCancelAll(); PlayQueue.Clear();
                }
                else
                {
                    var first = PlayQueue.Dequeue();
                    Play(first.Key, first.Value);
                }
            }
            else
            {
                if (SpeakCompleted is Action<SpeakCompletedEventArgs>) SpeakCompleted.Invoke(e);
                lastPrompt = null;
            }
        }
        #endregion

        #region Play contents routines
        private Prompt lastPrompt = null;
        public async void Play(PromptBuilder prompt, CultureInfo locale = null, bool async = true)
        {
            if (!(synth is SpeechSynthesizer)) return;

            if (synth.GetInstalledVoices().Count <= 0) return;
            if (!(prompt is PromptBuilder) || prompt.IsEmpty) return;

            if (synth.State == SynthesizerState.Paused)
            {
                synth.Resume();
                return;
            }

            Stop();
            if (await CanPlay.WaitAsync(1000))
            {
                try
                {
                    CancelRequested = false;

                    if (!(locale is CultureInfo)) locale = prompt.Culture;

                    var voice = GetCustomVoiceName(locale);
                    if (string.IsNullOrEmpty(voice)) synth.SelectVoice(voice_default);
                    else synth.SelectVoice(voice);

                    synth.Volume = Math.Max(0, Math.Min(PlayVolume, 100));  // 0...100
                                                                            //synth.Rate = 0;     // -10...10
                    var prompt_xml = prompt.ToXml();
                    if (AutoChangeSpeechSpeed)
                    {
                        if (prompt_xml.Equals(SPEECH_TEXT, StringComparison.CurrentCultureIgnoreCase) &&
                            SPEECH_CULTURE.IetfLanguageTag.Equals(locale.IetfLanguageTag, StringComparison.CurrentCultureIgnoreCase))
                            SPEECH_SLOW = !SPEECH_SLOW;
                        else
                            SPEECH_SLOW = false;

                        if (SPEECH_SLOW) synth.Rate = Math.Max(-10, Math.Min(PlaySlowRate, 10));
                        else synth.Rate = Math.Max(-10, Math.Min(PlayNormalRate, 10));
                    }

                    // Configure the audio output. 
                    synth.SetOutputToDefaultAudioDevice();

                    if (async)
                        lastPrompt = synth.SpeakAsync(prompt);  // Asynchronous
                    else
                        synth.Speak(prompt);       // Synchronous

                    SPEECH_TEXT = prompt_xml;
                    SPEECH_CULTURE = prompt.Culture;
                }
#if DEBUG
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
#else
                catch (Exception ex) { ex.ERROR("SPEECH"); }
#endif 
            }
        }

        public async void Play(PromptBuilder prompt, string locale, bool async = true)
        {
            await Task.Delay(1);
            Play(prompt, FindCultureByName(locale), async);
        }

        public async void Play(string text, CultureInfo locale = null, bool async = true)
        {
            if (string.IsNullOrEmpty(text.Trim())) return;

            if (!(synth is SpeechSynthesizer)) return;

            if (synth.GetInstalledVoices().Count <= 0) return;

            if (synth.State == SynthesizerState.Paused)
            {
                synth.Resume();
                return;
            }

            Stop();
            if (await CanPlay.WaitAsync(1000))
            {
                try
                {
                    CancelRequested = false;

                    if (!(locale is CultureInfo)) locale = DetectCulture(text);

                    var voice = GetCustomVoiceName(locale);
                    if (string.IsNullOrEmpty(voice)) synth.SelectVoice(voice_default);
                    else synth.SelectVoice(voice);

                    synth.Volume = Math.Max(0, Math.Min(PlayVolume, 100));  // 0...100
                                                                            //synth.Rate = 0;     // -10...10
                    if (AutoChangeSpeechSpeed && (SimpleCultureDetect || !AltPlayMixedCulture))
                    {
                        if (text.Equals(SPEECH_TEXT, StringComparison.CurrentCultureIgnoreCase) &&
                            SPEECH_CULTURE.IetfLanguageTag.Equals(locale.IetfLanguageTag, StringComparison.CurrentCultureIgnoreCase))
                            SPEECH_SLOW = !SPEECH_SLOW;
                        else
                            SPEECH_SLOW = false;

                        if (SPEECH_SLOW) synth.Rate = Math.Max(-10, Math.Min(PlaySlowRate, 10));
                        else synth.Rate = Math.Max(-10, Math.Min(PlayNormalRate, 10));
                    }

                    // Configure the audio output. 
                    synth.SetOutputToDefaultAudioDevice();

                    if (async)
                        lastPrompt = synth.SpeakAsync(text);  // Asynchronous
                    else
                        synth.Speak(text);       // Synchronous

                    if (AutoChangeSpeechSpeed && (SimpleCultureDetect || !AltPlayMixedCulture))
                    {
                        SPEECH_TEXT = text;
                        SPEECH_CULTURE = locale;
                    }
                }
#if DEBUG
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
#else
                catch (Exception ex) { ex.ERROR("SPEECH"); }
#endif
            }

            //if (!(locale is CultureInfo)) locale = DetectCulture(text);
            //var prompt = new PromptBuilder(locale);
            //prompt.AppendText(text);
            //Play(prompt, locale, async: async);
        }

        public async void Play(string text, string locale, bool async = true)
        {
            await Task.Delay(1);
            this.DoEvents();
            Play(text, FindCultureByName(locale), async);
        }

        public void Play(IEnumerable<string> contents, CultureInfo locale = null)
        {
            PlayQueue.Clear();
            if (AltPlayMixedCulture)
            {
                if (contents is IEnumerable<string>)
                {
                    if (AutoChangeSpeechSpeed)
                    {
                        var speech_text = string.Join(Environment.NewLine, contents);
                        if (SPEECH_TEXT.Equals(speech_text))
                            SPEECH_SLOW = !SPEECH_SLOW;
                        else
                            SPEECH_SLOW = false;

                        if (SPEECH_SLOW) synth.Rate = Math.Max(-10, Math.Min(PlaySlowRate, 10));
                        else synth.Rate = Math.Max(-10, Math.Min(PlayNormalRate, 10));

                        SPEECH_TEXT = speech_text;
                    }
                    foreach (var text in contents)
                    {
                        if (string.IsNullOrEmpty(text)) continue;
                        if (PlayMixedCultureInline)
                        {
                            var sentences = SlicingByCulture(text, locale);
                            foreach (var s in sentences)
                                PlayQueue.Enqueue(s);
                        }
                        else
                        {
                            PlayQueue.Enqueue(new KeyValuePair<string, CultureInfo>(text, DetectCulture(text)));
                        }
                    }
                    if (PlayQueue.Count > 0)
                    {
                        Stop();
                        var first = PlayQueue.Dequeue();
                        Play(first.Key, first.Value);
                    }
                    else
                    {
                        SPEECH_TEXT = string.Empty;
                        SPEECH_SLOW = false;
                        synth.Rate = Math.Max(-10, Math.Min(PlayNormalRate, 10));
                    }
                }
            }
            else
            {
                var prompt = new PromptBuilder(CultureInfo.CurrentCulture);
                prompt.ClearContent();
                foreach (var text in contents)
                {
                    if (string.IsNullOrEmpty(text)) continue;
                    if (PlayMixedCultureInline)
                    {
                        var sentences = SlicingByCulture(text, locale);
                        var culture = locale == null ? sentences.FirstOrDefault().Value : locale;
                        prompt.StartStyle(new PromptStyle());
                        prompt.StartParagraph(culture);
                        foreach (var kv in sentences)
                        {
                            var new_text = kv.Key;
                            var new_culture = kv.Value;
                            if (string.IsNullOrEmpty(new_text)) continue;
                            prompt.StartVoice(GetCustomVoiceName(new_culture));
                            prompt.StartSentence(new_culture);
                            prompt.AppendText(new_text);
                            prompt.EndSentence();
                            prompt.EndVoice();
                        }
                        prompt.EndParagraph();
                        prompt.EndStyle();
                    }
                    else
                    {
                        var culture = locale == null ? DetectCulture(text) : locale;
                        prompt.StartStyle(new PromptStyle());
                        prompt.StartParagraph(culture);
                        prompt.StartVoice(GetCustomVoiceName(culture));
                        prompt.StartSentence(culture);
                        prompt.AppendText(text);
                        prompt.EndSentence();
                        prompt.EndVoice();
                        prompt.EndParagraph();
                        prompt.EndStyle();
                    }
                }
                Play(prompt, locale);
            }
        }

        public void Play(IEnumerable<string> contents, string locale)
        {
            Play(contents, locale.FindCultureByName());
        }

        public void Pause()
        {
            try
            {
                if (synth is SpeechSynthesizer && synth.State == SynthesizerState.Speaking)
                {
                    synth.Pause();
                }
            }
            catch (Exception ex) { ex.ERROR("SPEECH"); }
        }

        public void Resume()
        {
            try
            {
                if (synth is SpeechSynthesizer && synth.State == SynthesizerState.Paused)
                {
                    synth.Resume();
                }
            }
            catch (Exception ex) { ex.ERROR("SPEECH"); }
        }

        private bool CancelRequested = false;
        public async void Stop()
        {
            try
            {
                if (synth is SpeechSynthesizer)
                {
                    if (synth.State != SynthesizerState.Ready)
                    {
                        CancelRequested = true;
                        if (lastPrompt is Prompt) synth.SpeakAsyncCancel(lastPrompt);
                        synth.SpeakAsyncCancelAll();
                        await Task.Delay(1);
                    }
                }
            }
            catch (Exception ex) { ex.ERROR("SPEECH"); }
        }

        public IEnumerable<string> Slice(string text, CultureInfo locale = null, bool async = true)
        {
            var result = new List<string>();

            if (string.IsNullOrEmpty(text.Trim())) return (result);

            if (!(slice_synth is SpeechSynthesizer)) return (result);

            if (slice_synth.GetInstalledVoices().Count <= 0) return (result);

            //if (slice_synth.State != SynthesizerState.Ready) return (result);

            IsSlicing = true;
            if (slice_synth.State != SynthesizerState.Ready) slice_synth.SpeakAsyncCancelAll();

            try
            {
                if (!(locale is CultureInfo)) locale = DetectCulture(text);

                var voice = GetCustomVoiceName(locale);
                if (string.IsNullOrEmpty(voice)) slice_synth.SelectVoice(voice_default);
                else slice_synth.SelectVoice(voice);

                // Configure the audio output. 
                slice_synth.SetOutputToNull();
                slice_synth.Speak(text);       // Synchronous
                result.AddRange(slice_words);
            }
#if DEBUG
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
#else
            catch (Exception ex) { ex.ERROR("SPEECH"); }
#endif            
            finally { IsSlicing = false; }

            return (result);
        }

        public IEnumerable<string> Slice(string text, string locale, bool async = true)
        {
            return (Slice(text, FindCultureByName(locale), async));
        }
        #endregion

        #region Constructor & Finalizers/Destructors
        public SpeechTTS()
        {
            try
            {
                #region Synthesis
                synth = new SpeechSynthesizer();
                synth.SpeakStarted += Synth_SpeakStarted;
                synth.SpeakProgress += Synth_SpeakProgress;
                synth.StateChanged += Synth_StateChanged;
                synth.VoiceChange += Synth_VoiceChange;
                synth.BookmarkReached += Synth_BookmarkReached;
                synth.PhonemeReached += Synth_PhonemeReached;
                synth.VisemeReached += Synth_VisemeReached;
                synth.SpeakCompleted += Synth_SpeakCompleted;

                slice_synth = new SpeechSynthesizer();
                slice_synth.SpeakStarted += Synth_SpeakStarted;
                slice_synth.SpeakProgress += Synth_SpeakProgress;
                slice_synth.StateChanged += Synth_StateChanged;
                slice_synth.VoiceChange += Synth_VoiceChange;
                slice_synth.BookmarkReached += Synth_BookmarkReached;
                slice_synth.PhonemeReached += Synth_PhonemeReached;
                slice_synth.VisemeReached += Synth_VisemeReached;
                slice_synth.SpeakCompleted += Synth_SpeakCompleted;
                #endregion

                voice_default = synth.Voice.Name;
                InstalledVoices = synth.GetInstalledVoices().ToList();
            }
            catch (Exception ex) { ex.ERROR("SPEECH"); synth = null; }
        }

        ~SpeechTTS()
        {
            Dispose(false);
        }

        public void Close()
        {
            Dispose();
        }

        private bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
#if DEBUG            
            GC.SuppressFinalize(this);
#endif            
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                try
                {
                    if (synth is SpeechSynthesizer) synth.Dispose();
                    if (slice_synth is SpeechSynthesizer) slice_synth.Dispose();
                }
                catch (Exception ex) { ex.ERROR("SPEECH"); }
            }
            disposed = true;
        }
        #endregion
    }

    public static class Speech
    {
        public static char[] TagBreak { get; } = new char[] { '#', '@' };
        public static string[] LineBreak { get; } = new string[] { Environment.NewLine, "\n\r", "\r\n", "\r", "\n", "<br/>", "<br />", "<br>", "</br>" };

        #region Speech Synthesizer events actions
        public static Action<SpeakStartedEventArgs> SpeakStarted
        {
            get { return (t2s is SpeechTTS ? t2s.SpeakStarted : null); }
            set { if (t2s is SpeechTTS) t2s.SpeakStarted = value; }
        }
        public static Action<SpeakProgressEventArgs> SpeakProgress
        {
            get { return (t2s is SpeechTTS ? t2s.SpeakProgress : null); }
            set { if (t2s is SpeechTTS) t2s.SpeakProgress = value; }
        }
        public static Action<StateChangedEventArgs> StateChanged
        {
            get { return (t2s is SpeechTTS ? t2s.StateChanged : null); }
            set { if (t2s is SpeechTTS) t2s.StateChanged = value; }
        }
        public static Action<VoiceChangeEventArgs> VoiceChange
        {
            get { return (t2s is SpeechTTS ? t2s.VoiceChange : null); }
            set { if (t2s is SpeechTTS) t2s.VoiceChange = value; }
        }
        public static Action<BookmarkReachedEventArgs> BookmarkReached
        {
            get { return (t2s is SpeechTTS ? t2s.BookmarkReached : null); }
            set { if (t2s is SpeechTTS) t2s.BookmarkReached = value; }
        }
        public static Action<PhonemeReachedEventArgs> PhonemeReached
        {
            get { return (t2s is SpeechTTS ? t2s.PhonemeReached : null); }
            set { if (t2s is SpeechTTS) t2s.PhonemeReached = value; }
        }
        public static Action<VisemeReachedEventArgs> VisemeReached
        {
            get { return (t2s is SpeechTTS ? t2s.VisemeReached : null); }
            set { if (t2s is SpeechTTS) t2s.VisemeReached = value; }
        }
        public static Action<SpeakCompletedEventArgs> SpeakCompleted
        {
            get { return (t2s is SpeechTTS ? t2s.SpeakCompleted : null); }
            set { if (t2s is SpeechTTS) t2s.SpeakCompleted = value; }
        }
        #endregion

        #region Speech Synthesizer properties
        public static Dictionary<string, string> CustomNames { set { SpeechTTS.SetCustomNames(value); } }
        public static Action<string, List<string>> SetVoice { get { return (t2s is SpeechTTS ? t2s.SetVoice : null); } }
        public static Func<Dictionary<string, List<string>>> GetVoices { get { return (t2s is SpeechTTS ? t2s.GetVoices : null); } }
        public static bool AutoChangeSpeechSpeed
        {
            get { return (t2s is SpeechTTS ? t2s.AutoChangeSpeechSpeed : true); }
            set { if (t2s is SpeechTTS) t2s.AutoChangeSpeechSpeed = value; }
        }
        public static bool AltPlayMixedCulture
        {
            get { return (t2s is SpeechTTS ? t2s.AltPlayMixedCulture : true); }
            set { if (t2s is SpeechTTS) t2s.AltPlayMixedCulture = value; }
        }
        public static bool SimpleCultureDetect
        {
            get { return (t2s is SpeechTTS ? t2s.SimpleCultureDetect : true); }
            set { if (t2s is SpeechTTS) t2s.SimpleCultureDetect = value; }
        }
        public static bool PlayMixedCultureInline
        {
            get { return (t2s is SpeechTTS ? t2s.PlayMixedCultureInline : true); }
            set { if (t2s is SpeechTTS) t2s.PlayMixedCultureInline = value; }
        }
        public static int PlayNormalRate
        {
            get { return (t2s is SpeechTTS ? t2s.PlayNormalRate : 0); }
            set { if (t2s is SpeechTTS) t2s.PlayNormalRate = Math.Max(-10, Math.Min(value, 10)); }
        }
        public static int PlaySlowRate
        {
            get { return (t2s is SpeechTTS ? t2s.PlaySlowRate : -5); }
            set { if (t2s is SpeechTTS) t2s.PlaySlowRate = Math.Max(-10, Math.Min(value, 10)); }
        }
        public static int PlayVolume
        {
            get { return (t2s is SpeechTTS ? t2s.PlayVolume : 100); }
            set { if (t2s is SpeechTTS) t2s.PlayVolume = Math.Max(0, Math.Min(value, 100)); }
        }
        public static SynthesizerState State { get { return (t2s is SpeechTTS ? t2s.State : SynthesizerState.Ready); } }
        public static bool IsSlicing { get { return (t2s is SpeechTTS ? t2s.IsSlicing : false); } }

        public static bool ChineseSimplifiedPrefer
        {
            get { return (t2s is SpeechTTS ? SpeechTTS.ChineseSimplifiedPrefer : true); }
            set { if (t2s is SpeechTTS) SpeechTTS.ChineseSimplifiedPrefer = value; }
        }
        #endregion

        #region Init speech synthesizer instance
        private static SpeechTTS t2s = Init();
        private static SpeechTTS Init()
        {
            return (new SpeechTTS());
        }
        #endregion

        #region Synthesizer helper routines
        public static CultureInfo FindCultureByName(this string lang)
        {
            return (SpeechTTS.FindCultureByName(lang));
        }

        public static CultureInfo DetectCulture(this string text)
        {
            return (SpeechTTS.DetectCulture(text));
        }

        public static bool IsReady()
        {
            return (State == SynthesizerState.Ready);
        }

        public static bool IsBusy()
        {
            return (State == SynthesizerState.Speaking || State == SynthesizerState.Paused);
        }

        public static bool IsPaused()
        {
            return (State == SynthesizerState.Paused);
        }

        public static async void Play(this string text, CultureInfo culture, bool async = true)
        {
            try
            {
                if (!(t2s is SpeechTTS))
                {
                    t2s = Init();
                }
                if (t2s is SpeechTTS)
                {
                    await new Action(() =>
                    {
                        if (culture == null)
                        {
                            if (SimpleCultureDetect)
                                t2s.Play(text, "unk", async);
                            else
                            {
                                var tlist = text.Split(LineBreak, StringSplitOptions.RemoveEmptyEntries);
                                t2s.Play(tlist, culture);
                            }
                        }
                        else
                            t2s.Play(text, culture, async);
                    }).InvokeAsync(true);
                    Application.Current.DoEvents();
                }
            }
            catch (Exception ex) { ex.ERROR("SPEECH"); }
        }

        public static void Play(this string text, string lang, bool async = true)
        {
            Play(text, FindCultureByName(lang), async);
        }

        public static void Play(this string text)
        {
            CultureInfo culture = null;
            Play(text, culture, true);
        }

        public static async void Play(this IEnumerable<string> texts, CultureInfo culture, bool async = true)
        {
            try
            {
                if (!(t2s is SpeechTTS))
                {
                    t2s = Init();
                }
                if (t2s is SpeechTTS)
                {
                    await new Action(() =>
                    {
                        if (culture == null)
                        {
                            if (SimpleCultureDetect)
                                t2s.Play(string.Join(Environment.NewLine, texts), "unk", async);
                            else
                                t2s.Play(texts.ToList(), culture);
                        }
                        else
                            t2s.Play(string.Join(Environment.NewLine, texts), culture, async);
                    }).InvokeAsync(true);
                    Application.Current.DoEvents();
                }
            }
            catch (Exception ex) { ex.ERROR("SPEECH"); }
        }

        public static void Play(this IEnumerable<string> texts, string lang, bool async = true)
        {
            Play(texts, FindCultureByName(lang), async);
        }

        public static void Play(this IEnumerable<string> texts)
        {
            CultureInfo culture = null;
            Play(texts, culture, true);
        }

        public static void Pause()
        {
            try
            {
                if (t2s is SpeechTTS) t2s.Pause();
            }
            catch (Exception ex) { ex.ERROR("SPEECH"); }
        }

        public static void Resume()
        {
            try
            {
                if (t2s is SpeechTTS) t2s.Resume();
            }
            catch (Exception ex) { ex.ERROR("SPEECH"); }
        }

        public static void Stop()
        {
            try
            {
                if (t2s is SpeechTTS) t2s.Stop();
            }
            catch (Exception ex) { ex.ERROR("SPEECH"); }
        }

        public static IList<string> Slice(this string text, CultureInfo culture, bool async = true)
        {
            var result = new List<string>();

            if (!(t2s is SpeechTTS))
            {
                t2s = Init();
            }
            if (t2s is SpeechTTS)
            {
                if (culture == null)
                    result.AddRange(t2s.Slice(text, "unk"));
                else
                    result.AddRange(t2s.Slice(text, culture));
            }

            return (result);
        }

        public static IList<string> Slice(this string text, string lang, bool async = true)
        {
            return (Slice(text, FindCultureByName(lang), async));
        }

        public static IList<string> Slice(this string text)
        {
            CultureInfo culture = null;
            return (Slice(text, culture, true));
        }

        public static IList<string> Slice(this IEnumerable<string> texts, CultureInfo culture, bool async = true)
        {
            return (Slice(string.Join(Environment.NewLine, texts), culture, async));
        }

        public static IList<string> Slice(this IEnumerable<string> texts, string lang, bool async = true)
        {
            return (Slice(texts, FindCultureByName(lang), async));
        }

        public static IList<string> Slice(this IEnumerable<string> texts)
        {
            CultureInfo culture = null;
            return (Slice(texts, culture, true));
        }
        #endregion
    }
}
