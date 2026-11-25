using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;

namespace RhythmTyping
{
    internal interface IWordSpeech : IDisposable
    {
        bool Prepare(bool english);
        bool Speak(string text);
        void SignalError();
        void Stop();
    }

    internal sealed class WordSpeech : IWordSpeech
    {
        internal const int ErrorSoundMilliseconds = 224;
        internal const string ErrorSoundResource = "RhythmTyping.HazardWarning.wav";
        private const string Punctuation = ".,!?;:'\"-()[]{}+*/\\=<>%№@#$^&_|~`";
        private static readonly string[] RussianSigns = ("точка|запятая|восклицательный знак|вопросительный знак|точка с запятой|двоеточие|апостроф|кавычки|дефис|открывающая скобка|закрывающая скобка|открывающая квадратная скобка|закрывающая квадратная скобка|открывающая фигурная скобка|закрывающая фигурная скобка|плюс|звёздочка|косая черта|обратная косая черта|равно|меньше|больше|процент|знак номера|собака|решётка|доллар|крышка|амперсанд|подчёркивание|вертикальная черта|тильда|обратная кавычка").Split('|');
        private static readonly string[] EnglishSigns = ("period|comma|exclamation mark|question mark|semicolon|colon|apostrophe|quotation mark|hyphen|open parenthesis|close parenthesis|open square bracket|close square bracket|open brace|close brace|plus|asterisk|slash|backslash|equals sign|less than sign|greater than sign|percent sign|number sign|at sign|hash|dollar sign|caret|ampersand|underscore|vertical bar|tilde|backtick").Split('|');
        private static readonly Regex SpokenParts = new Regex(@"\p{L}+(?:['-]\p{L}+)*|.");
        private SpeechSynthesizer speaker;
        private string currentLanguage;
        private SoundPlayer errorPlayer;

        internal static string SentencePrompt(string text, bool english)
        {
            if (String.IsNullOrEmpty(text)) return null;
            return String.Join(" ", text.Split(' ').Select(token => Prompt(token, english)));
        }

        internal static string Prompt(string token, bool english)
        {
            if (String.IsNullOrEmpty(token)) return null;
            var parts = new List<string>();
            foreach (Match match in SpokenParts.Matches(token))
            {
                string part = match.Value;
                if (Char.IsLetter(part[0]))
                {
                    AddWordPrompt(parts, part, english);
                    continue;
                }
                int sign = Punctuation.IndexOf(part[0]);
                parts.Add(sign >= 0 ? (english ? EnglishSigns : RussianSigns)[sign] : part);
            }
            return String.Join(", ", parts);
        }

        private static void AddWordPrompt(List<string> parts, string word, bool english)
        {
            string capital = english ? "capital" : "заглавная";
            if (word.Skip(1).Any(Char.IsUpper))
            {
                foreach (char character in word)
                {
                    if (Char.IsUpper(character)) parts.Add(capital);
                    int sign = Punctuation.IndexOf(character);
                    parts.Add(sign >= 0 ? (english ? EnglishSigns : RussianSigns)[sign] : Char.ToLowerInvariant(character).ToString());
                }
                return;
            }

            if (Char.IsUpper(word[0])) parts.Add(english ? "start with a capital letter" : "с большой буквы");
            parts.Add(word);
            int letters = 0;
            foreach (char character in word)
            {
                if (Char.IsLetter(character))
                {
                    letters++;
                    continue;
                }
                string sign = (english ? EnglishSigns : RussianSigns)[Punctuation.IndexOf(character)];
                parts.Add(sign + (english ? " after letter " : " после буквы номер ") + letters.ToString(CultureInfo.InvariantCulture));
            }
        }

        internal static VoiceInfo FindVoice(SpeechSynthesizer engine, bool english)
        {
            string language = english ? "en" : "ru";
            string preferred = english ? "en-US" : "ru-RU";
            return engine.GetInstalledVoices().Where(item => item.Enabled)
                .Select(item => item.VoiceInfo).Where(voice => voice.Culture.TwoLetterISOLanguageName == language)
                .OrderByDescending(voice => voice.Culture.Name == preferred).FirstOrDefault();
        }

        public bool Prepare(bool english)
        {
            string language = english ? "en" : "ru";
            if (currentLanguage == language) return true;
            try
            {
                if (speaker == null) speaker = new SpeechSynthesizer { Rate = -2 };
                VoiceInfo voice = FindVoice(speaker, english);
                if (voice == null) return false;
                speaker.SelectVoice(voice.Name);
                speaker.SetOutputToDefaultAudioDevice();
                currentLanguage = language;
                return true;
            }
            catch (InvalidOperationException) { return false; }
            catch (ArgumentException) { return false; }
            catch (COMException) { return false; }
            catch (NullReferenceException) { return false; } // System.Speech can fail while resolving a missing audio device.
        }

        public bool Speak(string text)
        {
            if (speaker == null || String.IsNullOrEmpty(text)) return false;
            try
            {
                speaker.SpeakAsyncCancelAll();
                speaker.SpeakAsync(text);
                return true;
            }
            catch (InvalidOperationException) { return false; }
            catch (COMException) { return false; }
        }

        public void SignalError()
        {
            try
            {
                if (errorPlayer == null)
                    errorPlayer = new SoundPlayer(Assembly.GetExecutingAssembly().GetManifestResourceStream(ErrorSoundResource));
                errorPlayer.Play();
            }
            catch (InvalidOperationException) { }
            catch (TimeoutException) { }
        }

        public void Stop()
        {
            if (speaker == null) return;
            try { speaker.SpeakAsyncCancelAll(); }
            catch (InvalidOperationException) { }
            catch (COMException) { }
        }

        public void Dispose()
        {
            if (speaker != null) speaker.Dispose();
            if (errorPlayer != null)
            {
                if (errorPlayer.Stream != null) errorPlayer.Stream.Dispose();
                errorPlayer.Dispose();
            }
            speaker = null;
            currentLanguage = null;
            errorPlayer = null;
        }
    }
}
