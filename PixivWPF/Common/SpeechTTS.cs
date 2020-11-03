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
    public class SpeechTTS
    {
        #region Culture routines
        public CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;

        public bool ChineseSimplifiedPrefer { get; set; } = true;

        #region Regex Patterns for culture detect/slice
        //
        // 中文：[\u4e00-\u9fcc, \u3400-\u4db5, \u20000-\u2a6d6, \u2a700-\u2b734, \u2b740-\u2b81d, \uf900-\ufad9, \u2f800-\u2fa1d]
        // 繁体标点: [\u3000-\u3003, \u3008-\u300F, \u3010-\u3011, \u3014-\u3015, \u301C-\u301E]
        // BIG-5: [\ue000-\uf848]
        // 日文：[\u0800-\u4e00] [\u3041-\u31ff]
        // 韩文：[\uac00-\ud7ff]
        //
        //private string pattern_zh = @"[\u3000-\u303f\ufe00-\ufe4f\u3400-\u4db5\u4e00-\u9fcc\uf900-\ufad9\u20000-\u2cea1]+?";
        private string pattern_zhs = @"([\u3000-\u303f\ufe00-\ufe4f\uff01-\uffee\u3400-\u4db5\u4e00-\u9fcc\uf900-\ufad9]|([\ud840-\ud873][\udc00-\udfff]))+";
        private string pattern_zht = @"[\u3000-\u3003\u3008-\u300F\u3010-\u3011\u3014-\u3015\u301C-\u301E\u3105-\u31ba\ua140-\ua3bf\ua440-\uc67e\uc940-\uf9d5\ue000-\uf848]+";
        //private string pattern_ja = @"[\u0021-\u024f\u0250-\u02af\u0391-\u03d9\u1f00-\u1ffe\u1e00-\u1eff\u2010-\u205e\u2e00-\u2e3b\u2c60-\u2c7f\ua720-\ua7ff]+";
        private string pattern_ja = @"([\u3041-\u309f\u30a0-\u31ff\u3220-\u325f\u3280-\u32ff\u3300-\u4077]|(\ud83c[\ude01-\ude51]))+";
        private string pattern_ko = @"[\u1100-\u11ff\u3131-\u318e\u3200-\u321f\u3260-\u327f\ua960-\ua97c\uac00-\ud7a3]+";
        private string pattern_en = @"[\u0020-\u007e\u0080-\u02af\u0391-\u03d9\u1f00-\u1ffe\u1e00-\u1eff\u2010-\u205e\u2e00-\u2e3b\u2c60-\u2c7f\ua720-\ua7ff]+";
        private string pattern_dt = @"^[\d: tTzZ+\-\/\\]{4,}$";
        private string pattern_digit = @"^\d+$";
        //private string pattern_emoji = @"[\u2190-\u27bf\u3400-\u4dbf\u4dc0-\u4dff\uf900-\ufad9\u1d300-\u1d356\u1f000-\u1f02b\u1f030-\u1f093\u1f0a0-\u1f0f5\u1f300-\u1f5ff]+";
        private string pattern_emoji = @"([\u2190-\u27bf]|(\ud834[\udf00-\udf56])|(\ud83c[\udc00-\udfff]))+?";
        private string pattern_symbol = @"[\u0021-\u0040\u005b-\u0060\u007b-\u00ff\u2010-\u2e3b]+";
        #endregion

        #region Required Encoding types
        //var m_jp = Regex.Matches(text, @"([\u0800-\u4e00])", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        //var m_zh = Regex.Matches(text, @"([\u4e00-\u9fbb])", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private Encoding GBK = Encoding.GetEncoding("GBK");
        private Encoding BIG5 = Encoding.GetEncoding("BIG5");
        private Encoding JAP = Encoding.GetEncoding("Shift-JIS");
        private Encoding UTF8 = Encoding.UTF8;
        #endregion

        private CultureInfo FindCultureByName(string lang)
        {
            CultureInfo culture = null;
            if (string.IsNullOrEmpty(lang)) lang = "unk";
            else lang = lang.ToLower();

            CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.AllCultures & ~CultureTypes.NeutralCultures);
            CultureInfo[] specific = CultureInfo.GetCultures(CultureTypes.SpecificCultures &~ CultureTypes.UserCustomCulture);

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

                if (culture.EnglishName.StartsWith("unk", StringComparison.CurrentCultureIgnoreCase))
                    culture = null;
            }
            catch (Exception) { culture = null; }
            finally { }

            return (culture);
        }

        private CultureInfo DetectCulture(string text)
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

        private IList<KeyValuePair<string, CultureInfo>> SliceByCulture(string text, CultureInfo defautlCulture)
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
                catch (Exception) { }
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
        public static List<InstalledVoice> InstalledVoices { get; private set; } = null;
        private Queue<KeyValuePair<string, CultureInfo>> PlayQueue = new Queue<KeyValuePair<string, CultureInfo>>();

        public bool AutoChangeSpeechSpeed { get; set; } = true;
        public bool AltPlayMixedCulture { get; set; } = false;
        public bool PlayMixedCultureInline { get; set; } = false;
        public bool SimpleCultureDetect { get; set; } = true;

        public SynthesizerState State { get { return (synth is SpeechSynthesizer ? synth.State : SynthesizerState.Ready); } }

        public Action<StateChangedEventArgs> StateChanged { get; set; } = null;
        public Action<SpeakStartedEventArgs> SpeakStarted { get; set; } = null;
        public Action<SpeakProgressEventArgs> SpeakProgress { get; set; } = null;
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

        private async void Synth_StateChanged(object sender, StateChangedEventArgs e)
        {
            if (synth == null) return;

            if (synth.State == SynthesizerState.Paused)
            {

            }
            else if (synth.State == SynthesizerState.Speaking)
            {

            }
            else if (synth.State == SynthesizerState.Ready)
            {

            }
            if (StateChanged is Action<StateChangedEventArgs>) await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                StateChanged(e);
            }, DispatcherPriority.Background);
        }

        private async void Synth_SpeakStarted(object sender, SpeakStartedEventArgs e)
        {
            if (synth == null) return;

            if (SpeakStarted is Action<SpeakStartedEventArgs>) await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                SpeakStarted(e);
            }, DispatcherPriority.Background);
        }

        private async void Synth_SpeakProgress(object sender, SpeakProgressEventArgs e)
        {
            if (synth == null) return;

            if (SpeakProgress is Action<SpeakProgressEventArgs>) await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                SpeakProgress(e);
            }, DispatcherPriority.Background);
        }

        private async void Synth_SpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            if (synth == null) return;

            if (!e.Cancelled && PlayQueue.Count > 0)
            {
                var first = PlayQueue.Dequeue();
                Play(first.Key, first.Value);
            }
            else
            {
                if (SpeakCompleted is Action<SpeakCompletedEventArgs>) await Dispatcher.CurrentDispatcher.InvokeAsync(() =>
                {
                    SpeakCompleted(e);
                }, DispatcherPriority.Background);
            }
        }
        #endregion

        #region Play contents routines
        public void Play(string text, CultureInfo locale = null, bool async = true)
        {
            if (string.IsNullOrEmpty(text.Trim())) return;

            if (!(synth is SpeechSynthesizer)) return;

            if (synth.GetInstalledVoices().Count <= 0) return;

            if (synth.State == SynthesizerState.Paused)
            {
                synth.Resume();
                return;
            }

            try
            {
                Stop();

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
                ex.Message.DEBUG();
            }
#else
            catch (Exception) { }
#endif            
        }

        public void Play(string text, string locale, bool async = true)
        {
            Play(text, FindCultureByName(locale), async);
        }

        private Prompt lastPrompt = null;
        public void Play(PromptBuilder prompt, CultureInfo locale = null, bool async = true)
        {
            if (!(synth is SpeechSynthesizer)) return;

            if (synth.GetInstalledVoices().Count <= 0) return;
            if (!(prompt is PromptBuilder) || prompt.IsEmpty) return;

            if (synth.State == SynthesizerState.Paused)
            {
                synth.Resume();
                return;
            }

            try
            {
                Stop();

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
                ex.Message.DEBUG();
            }
#else
            catch (Exception) { }
#endif            
        }

        public void Play(PromptBuilder prompt, string locale, bool async = true)
        {
            Play(prompt, FindCultureByName(locale), async);
        }

        public void Play(IEnumerable<string> contents, CultureInfo locale = null)
        {
            if (AltPlayMixedCulture)
            {
                if (contents is IEnumerable<string>)
                {
                    PlayQueue.Clear();
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
                            var sentences = SliceByCulture(text, locale);
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
                PlayQueue.Clear();
                var prompt = new PromptBuilder();
                prompt.ClearContent();
                foreach (var text in contents)
                {
                    if (string.IsNullOrEmpty(text)) continue;
                    if (PlayMixedCultureInline)
                    {
                        var sentences = SliceByCulture(text, locale);
                        var culture = locale == null ? sentences.FirstOrDefault().Value : locale;
                        prompt.StartParagraph(culture);
                        foreach (var kv in sentences)
                        {
                            var new_text = kv.Key;
                            var new_culture = kv.Value;
                            if (string.IsNullOrEmpty(new_text)) continue;
                            prompt.StartVoice(GetCustomVoiceName(new_culture));
                            prompt.AppendText(new_text);
                            prompt.EndVoice();
                        }
                        prompt.EndParagraph();
                    }
                    else
                    {
                        var culture = locale == null ? DetectCulture(text) : locale;
                        prompt.StartParagraph(culture);
                        prompt.StartVoice(GetCustomVoiceName(culture));
                        prompt.AppendText(text);
                        prompt.EndVoice();
                        prompt.EndParagraph();
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
            catch (Exception) { }
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
            catch (Exception) { }
        }

        public void Stop()
        {
            try
            {
                if (synth is SpeechSynthesizer)
                {
                    if (synth.State != SynthesizerState.Ready)
                    {
                        if (lastPrompt is Prompt)
                        {
                            synth.SpeakAsyncCancel(lastPrompt);
                            Thread.Sleep(100);
                        }
                        synth.SpeakAsyncCancelAll();
                        Thread.Sleep(100);
                        synth.Resume();
                    }
                }
            }
            catch (Exception) { }
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
                synth.SpeakCompleted += Synth_SpeakCompleted;
                #endregion

                voice_default = synth.Voice.Name;
                InstalledVoices = synth.GetInstalledVoices().ToList();
            }
            catch (Exception) { synth = null; }
        }

        ~SpeechTTS()
        {
            try
            {
                if (synth is SpeechSynthesizer)
                {
                    Stop();
                    synth.Dispose();
                }
            }
            catch (Exception) { }
        }
        #endregion
    }

    public static class Speech
    {
        #region Speech Synthesizer events actions
        public static Action<StateChangedEventArgs> StateChanged
        {
            get { return (t2s is SpeechTTS ? t2s.StateChanged : null); }
            set { if (t2s is SpeechTTS) t2s.StateChanged = value; }
        }
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
        public static Action<SpeakCompletedEventArgs> SpeakCompleted
        {
            get { return (t2s is SpeechTTS ? t2s.SpeakCompleted : null); }
            set { if (t2s is SpeechTTS) t2s.SpeakCompleted = value; }
        }
        #endregion

        #region Speech Synthesizer properties
        public static Dictionary<string, string> CustomNames { set { SpeechTTS.SetCustomNames(value); } }
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

        public static bool ChineseSimplifiedPrefer
        {
            get { return (t2s is SpeechTTS ? t2s.ChineseSimplifiedPrefer : true); }
            set { if (t2s is SpeechTTS) t2s.ChineseSimplifiedPrefer = value; }
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
            catch (Exception) { culture = null; }
            finally { }
            return (culture);
        }

        public static string[] LineBreak = new string[] { Environment.NewLine, "\n\r", "\r\n", "\r", "\n", "<br/>", "<br />", "<br>", "</br>" };
        public static void Play(this string text, CultureInfo culture, bool async = true)
        {
            try
            {
                if (!(t2s is SpeechTTS))
                {
                    t2s = Init();
                }
                if (t2s is SpeechTTS)
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
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
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

        public static void Play(this IEnumerable<string> texts, CultureInfo culture, bool async = true)
        {
            try
            {
                if (!(t2s is SpeechTTS))
                {
                    t2s = Init();
                }
                if (t2s is SpeechTTS)
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
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
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
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static void Resume()
        {
            try
            {
                if (t2s is SpeechTTS) t2s.Resume();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        public static void Stop()
        {
            try
            {
                if (t2s is SpeechTTS) t2s.Stop();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }
        #endregion
    }
}
