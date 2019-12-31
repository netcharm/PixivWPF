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

        private SpeechSynthesizer synth = null;
        private string voice_default = string.Empty;
        //private bool SPEECH_AUTO = false;
        private bool SPEECH_SLOW = false;
        private string SPEECH_TEXT = string.Empty;

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

        public void Play(string text, CultureInfo locale = null, bool auto = true)
        {
            if (!(synth is SpeechSynthesizer)) return;

            if (synth.State == SynthesizerState.Paused)
            {
                synth.Resume();
                return;
            }

            List<string> lang_cn = new List<string>() { "zh-hans", "zh-cn", "zh" };
            List<string> lang_tw = new List<string>() { "zh-hant", "zh-tw" };
            List<string> lang_jp = new List<string>() { "ja-jp", "ja", "jp" };
            List<string> lang_en = new List<string>() { "en-us", "us", "en" };

            try
            {
                if (!(locale is CultureInfo)) locale = Culture;

                synth.SelectVoice(voice_default);
                string lang = auto ? "unk" : locale.IetfLanguageTag;
                if (lang.Equals("unk", StringComparison.CurrentCultureIgnoreCase))
                {
                    lang = CultureInfo.CurrentCulture.IetfLanguageTag;
                    //
                    // 中文：[\u4e00-\u9fcc, \u3400-\u4db5, \u20000-\u2a6d6, \u2a700-\u2b734, \u2b740-\u2b81d, \uf900-\ufad9, \u2f800-\u2fa1d]
                    // 日文：[\u0800-\u4e00] [\u3041-\u31ff]
                    // 韩文：[\uac00-\ud7ff]
                    //
                    //var m_jp = Regex.Matches(text, @"([\u0800-\u4e00])", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    //var m_zh = Regex.Matches(text, @"([\u4e00-\u9fbb])", RegexOptions.Multiline | RegexOptions.IgnoreCase);

                    if (Regex.Matches(text, @"[\u3041-\u31ff]", RegexOptions.Multiline).Count > 0)
                    {
                        lang = "ja";
                    }
                    else if (Regex.Matches(text, @"[\u4e00-\u9fbb]", RegexOptions.Multiline).Count > 0)
                    {
                        lang = "zh";
                    }
                }

                // Initialize a new instance of the SpeechSynthesizer.
                foreach (InstalledVoice voice in synth.GetInstalledVoices())
                {
                    VoiceInfo info = voice.VoiceInfo;
                    var vl = info.Culture.IetfLanguageTag;

                    if (lang_cn.Contains(vl.ToLower()) &&
                        lang.StartsWith("zh", StringComparison.CurrentCultureIgnoreCase) &&
                        voice.VoiceInfo.Name.ToLower().Contains("huihui"))
                    {
                        synth.SelectVoice(voice.VoiceInfo.Name);
                        break;
                    }
                    else if (lang_jp.Contains(vl.ToLower()) &&
                        lang.StartsWith("ja", StringComparison.CurrentCultureIgnoreCase) &&
                        voice.VoiceInfo.Name.ToLower().Contains("haruka"))
                    {
                        synth.SelectVoice(voice.VoiceInfo.Name);
                        break;
                    }
                    else if (lang_en.Contains(vl.ToLower()) &&
                        lang.StartsWith("en", StringComparison.CurrentCultureIgnoreCase) &&
                        voice.VoiceInfo.Name.ToLower().Contains("zira"))
                    {
                        synth.SelectVoice(voice.VoiceInfo.Name);
                        break;
                    }
                }

                //synth.Volume = 100;  // 0...100
                //synth.Rate = 0;     // -10...10
                if (text.Equals(SPEECH_TEXT, StringComparison.CurrentCultureIgnoreCase))
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
            }
#if DEBUG
            catch (Exception ex)
            {
                ex.Message.Log();
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
