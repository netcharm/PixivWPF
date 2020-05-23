using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PixivWPF.Common
{
    class SpeechTTS
    {
        #region Speech Synthesis routines
        public static List<InstalledVoice> InstalledVoices { get; private set; } = null;

        private Dictionary<CultureInfo, List<string>> nametable = new Dictionary<CultureInfo, List<string>>() {
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

        private void Synth_StateChanged(object sender, StateChangedEventArgs e)
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
        }

        private void Synth_SpeakStarted(object sender, SpeakStartedEventArgs e)
        {

        }

        private void Synth_SpeakProgress(object sender, SpeakProgressEventArgs e)
        {
            //throw new NotImplementedException();
        }

        private async void Synth_SpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            if (IsCompleted is Action) await IsCompleted.InvokeAsync();
        }
        #endregion

        public CultureInfo Culture { get; set; } = CultureInfo.CurrentCulture;
        public Action IsCompleted { get; set; } = null;

        private CultureInfo DetectCulture(string text)
        {
            CultureInfo result = CultureInfo.CurrentCulture;

            //
            // 中文：[\u4e00-\u9fcc, \u3400-\u4db5, \u20000-\u2a6d6, \u2a700-\u2b734, \u2b740-\u2b81d, \uf900-\ufad9, \u2f800-\u2fa1d]
            // 繁体标点: [\u3000-\u3003, \u3008-\u300F, \u3010-\u3011, \u3014-\u3015, \u301C-\u301E]
            // BIG-5: [\ue000-\uf848]
            // 日文：[\u0800-\u4e00] [\u3041-\u31ff]
            // 韩文：[\uac00-\ud7ff]
            //
            //var m_jp = Regex.Matches(text, @"([\u0800-\u4e00])", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            //var m_zh = Regex.Matches(text, @"([\u4e00-\u9fbb])", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var GBK = Encoding.GetEncoding("GBK");
            var BIG5 = Encoding.GetEncoding("BIG5");
            var JAP = Encoding.GetEncoding("Shift-JIS");
            var UTF8 = Encoding.UTF8;

            if (Regex.Matches(text, @"[\u3041-\u31ff]", RegexOptions.Multiline).Count > 0)
            {
                result = CultureInfo.GetCultureInfoByIetfLanguageTag("ja-JP");
            }
            else if (Regex.Matches(text, @"[\uac00-\ud7ff]", RegexOptions.Multiline).Count > 0)
            {
                result = CultureInfo.GetCultureInfoByIetfLanguageTag("ko-KR");
            }
            else if (Regex.Matches(text, @"[\u3400-\u4dbf\u4e00-\u9fbb]", RegexOptions.Multiline).Count > 0)
            {
                result = CultureInfo.GetCultureInfoByIetfLanguageTag("zh-CN");
            }
            //else if (GBK.GetString(GBK.GetBytes(text)).Equals(text))
            //{
            //    result = CultureInfo.GetCultureInfoByIetfLanguageTag("zh-CN");
            //}
            else if (Regex.Matches(text, @"[\u3000-\u3003\u3008-\u300F\u3010-\u3011\u3014-\u3015\u301C-\u301E\ua140-\ua3bf\ua440-\uc67e\uc940-\uf9d5\ue000-\uf848]", RegexOptions.Multiline).Count > 0)
            {
                result = CultureInfo.GetCultureInfoByIetfLanguageTag("zh-TW");
            }
            else
            {
                result = CultureInfo.GetCultureInfoByIetfLanguageTag("en-US");
            }

            return (result);
        }

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
            string result = null;
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

        private Dictionary<CultureInfo, List<string>> GetVoiceNames()
        {
            Dictionary<CultureInfo, List<string>> result = new Dictionary<CultureInfo, List<string>>();
            foreach (InstalledVoice voice in synth.GetInstalledVoices())
            {
                VoiceInfo info = voice.VoiceInfo;
                if (result.ContainsKey(info.Culture))
                {
                    result[info.Culture].Add(info.Name);
                    result[info.Culture].Sort();
                }
                else
                    result[info.Culture] = new List<string>() { info.Name };
            }
            return (result);
        }

        public void Play(string text, CultureInfo locale = null)
        {
            if (!(synth is SpeechSynthesizer)) return;

            var voices = synth.GetInstalledVoices();
            if (voices.Count <= 0) return;

            if (synth.State == SynthesizerState.Paused)
            {
                synth.Resume();
                return;
            }

            try
            {
                synth.SpeakAsyncCancelAll();
                synth.Resume();

                synth.SelectVoice(voice_default);

                if (!(locale is CultureInfo))
                    locale = DetectCulture(text);

                var nvs = GetVoiceNames();
                if (nvs.ContainsKey(locale))
                {
                    //string[] ns = new string[] {"huihui", "yaoyao", "lili", "yating", "hanhan", "haruka", "ayumi", "heami", "david", "zira"};
                    foreach (var n in nametable[locale])
                    {
                        var found = false;
                        foreach (var nl in nvs[locale])
                        {
                            var nll = nl.ToLower();
                            if (nll.Contains(n))
                            {
                                synth.SelectVoice(nl);
                                found = true;
                                break;
                            }
                        }
                        if (found) break;
                    }
                }

                //synth.Volume = 100;  // 0...100
                //synth.Rate = 0;     // -10...10
                if (text.Equals(SPEECH_TEXT, StringComparison.CurrentCultureIgnoreCase) && 
                    SPEECH_CULTURE.IetfLanguageTag.Equals(locale.IetfLanguageTag, StringComparison.CurrentCultureIgnoreCase))
                    SPEECH_SLOW = !SPEECH_SLOW;
                else
                    SPEECH_SLOW = false;

                if (SPEECH_SLOW) synth.Rate = -5;
                else synth.Rate = 0;

                // Synchronous
                //synth.Speak( text );
                // Asynchronous
                synth.SpeakAsyncCancelAll();
                synth.Resume();
                synth.SpeakAsync(text);
                SPEECH_TEXT = text;
                SPEECH_CULTURE = locale;
            }
#if DEBUG
            catch (Exception ex)
            {
                ex.Message.DEBUG();
            }
#else
            catch (Exception ){}
#endif            
        }

        public void Pause()
        {
            if (!(synth is SpeechSynthesizer)) return;
            if (synth != null && synth.State == SynthesizerState.Speaking)
                synth.Pause();
        }

        public void Resume()
        {
            if (!(synth is SpeechSynthesizer)) return;
            if (synth.State == SynthesizerState.Paused)
                synth.Resume();
        }

        public void Stop()
        {
            if (!(synth is SpeechSynthesizer)) return;
            synth.SpeakAsyncCancelAll();
            synth.Resume();
        }

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
    }

}
