using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace RhythmTyping
{
    internal enum PracticeMode { Lesson, Unlimited, FiveMinutes, TenMinutes }
    internal enum Phase { Ready, Running, Paused, Finished }
    internal enum InputResult { Ignored, Incorrect, Correct, NextPage, Finished }
    internal enum InputActivity { Typing, Dictation, Mixed }

    internal sealed class PassageGenerator
    {
        private const int PageLength = 118;
        internal const int MaxUnitLength = PageLength - 1;
        private readonly string[] units;
        private readonly Random random;
        private int index;
        private string previous;

        internal PassageGenerator(string[] material, Random random)
        {
            units = (string[])material.Clone();
            this.random = random;
            index = units.Length;
        }

        internal string NextPage(bool hasNextPage)
        {
            var page = new StringBuilder();
            int available = PageLength - (hasNextPage ? 1 : 0);
            while (true)
            {
                ShuffleIfNeeded();
                string unit = units[index];
                if (page.Length > 0 && page.Length + unit.Length + 1 > available) break;
                if (page.Length > 0) page.Append(' ');
                page.Append(unit);
                previous = unit;
                index++;
            }
            if (hasNextPage) page.Append(' ');
            return page.ToString();
        }

        private void ShuffleIfNeeded()
        {
            if (index < units.Length) return;
            for (int i = units.Length - 1; i > 0; i--)
                Swap(i, random.Next(i + 1));
            if (units.Length > 1 && units[0] == previous)
                Swap(0, random.Next(1, units.Length));
            index = 0;
        }

        private void Swap(int first, int second)
        {
            string value = units[first];
            units[first] = units[second];
            units[second] = value;
        }
    }

    internal sealed class Exercise
    {
        private const int DrillWordsPerPrompt = 6;
        private static readonly Regex SentenceEnd = new Regex(@"[.!?]+[""')\]}]*(?= |$)");
        private readonly Stopwatch clock = new Stopwatch();
        private readonly PassageGenerator generator;
        private readonly VerseDeck verseDeck;
        private readonly List<TypingMistake> mistakes = new List<TypingMistake>();
        private readonly HashSet<int> skippedPositions = new HashSet<int>();
        private int[] dictationEnds;
        private bool usedText;
        private bool usedAudio;
        internal IReadOnlyList<TypingMistake> ErrorLog { get { return mistakes; } }
        internal string Passage { get; private set; }
        internal PracticeMode Mode { get; private set; }
        internal bool IsDemo { get; private set; }
        internal bool IsDictation { get; private set; }
        internal InputActivity Activity
        {
            get
            {
                if (usedAudio) return usedText ? InputActivity.Mixed : InputActivity.Dictation;
                return !usedText && IsDictation ? InputActivity.Dictation : InputActivity.Typing;
            }
        }
        internal VersePage Verse { get; private set; }
        internal Phase Phase { get; private set; }
        internal int Page { get; private set; }
        internal int PageCount { get; private set; }
        internal int Position { get; private set; }
        internal int Typed { get; private set; }
        internal int Processed { get; private set; }
        internal bool AdvanceOnError { get; private set; }
        internal int Mistakes { get { return mistakes.Count; } }
        internal DateTimeOffset StartedAt { get; private set; }
        internal DateTimeOffset FinishedAt { get; private set; }
        internal TimeSpan Limit { get; private set; }
        internal bool IsTimed { get { return Limit > TimeSpan.Zero; } }
        internal TimeSpan Elapsed { get { return IsTimed && clock.Elapsed > Limit ? Limit : clock.Elapsed; } }
        internal TimeSpan Remaining { get { return IsTimed ? Limit - Elapsed : TimeSpan.Zero; } }
        internal char? Current { get { return Phase != Phase.Finished ? (char?)Passage[Position] : null; } }

        internal Exercise(Lesson lesson, Random random, PracticeMode mode, bool isDemo, TimeSpan? timeLimit = null,
            bool advanceOnError = false, bool isDictation = false)
        {
            Mode = isDemo ? PracticeMode.Lesson : mode;
            IsDemo = isDemo;
            SetDictation(isDictation);
            SetErrorPolicy(advanceOnError);
            Limit = isDemo ? TimeSpan.Zero : timeLimit.GetValueOrDefault(TimeLimit(Mode));
            if (lesson.IsPoetry) verseDeck = new VerseDeck(lesson.Poems, random);
            else generator = new PassageGenerator(lesson.Units, random);
            PageCount = isDemo ? 1 : Mode == PracticeMode.Lesson ? (verseDeck == null ? 3 : verseDeck.FirstPoemPages) : 0;
            LoadNextPage();
        }

        internal static TimeSpan TimeLimit(PracticeMode mode)
        {
            if (mode == PracticeMode.FiveMinutes) return TimeSpan.FromMinutes(5);
            if (mode == PracticeMode.TenMinutes) return TimeSpan.FromMinutes(10);
            return TimeSpan.Zero;
        }

        private void LoadNextPage()
        {
            Page++;
            Position = 0;
            skippedPositions.Clear();
            dictationEnds = null;
            bool more = PageCount == 0 || Page < PageCount;
            if (verseDeck == null) Passage = generator.NextPage(more);
            else
            {
                Verse = verseDeck.NextPage();
                Passage = String.Join(" ", Verse.Lines) + (more ? " " : "");
            }
        }

        internal int DictationEnd
        {
            get
            {
                int part = FindDictationPart();
                return dictationEnds[part];
            }
        }

        internal string TextToDictate()
        {
            if (!IsDictation || !Current.HasValue) return null;
            int part = FindDictationPart();
            int start = part == 0 ? 0 : dictationEnds[part - 1];
            return Passage.Substring(start, dictationEnds[part] - start).TrimEnd();
        }

        private int FindDictationPart()
        {
            if (dictationEnds == null)
            {
                var ends = new List<int>();
                foreach (Match match in SentenceEnd.Matches(Passage))
                {
                    int end = match.Index + match.Length;
                    // Keep the separating space with the sentence being typed.
                    ends.Add(Math.Min(end + 1, Passage.Length));
                }

                int tailStart = ends.Count == 0 ? 0 : ends[ends.Count - 1];
                int words = 0;
                for (int index = tailStart; index < Passage.Length - 1; index++)
                    if (Passage[index] == ' ' && ++words % DrillWordsPerPrompt == 0) ends.Add(index + 1);
                if (ends.Count == 0 || ends[ends.Count - 1] != Passage.Length) ends.Add(Passage.Length);
                dictationEnds = ends.ToArray();
            }
            return Array.FindIndex(dictationEnds, end => end > Position);
        }

        internal void SetDictation(bool enabled)
        {
            if (!IsDemo && Phase != Phase.Finished) IsDictation = enabled;
        }

        internal void SetErrorPolicy(bool advance)
        {
            if (Phase == Phase.Ready && !IsDemo) AdvanceOnError = advance;
        }

        internal bool WasSkipped(int position)
        {
            return skippedPositions.Contains(position);
        }

        internal void Start()
        {
            if (Phase != Phase.Ready && Phase != Phase.Paused) return;
            if (Phase == Phase.Ready) StartedAt = DateTimeOffset.Now;
            Phase = Phase.Running;
            clock.Start();
        }

        internal void Pause()
        {
            if (Phase != Phase.Running) return;
            Phase = Phase.Paused;
            clock.Stop();
        }

        internal bool Tick()
        {
            if (Phase != Phase.Running || !IsTimed || Remaining > TimeSpan.Zero) return false;
            Finish();
            return true;
        }

        internal bool FinishUnlimited()
        {
            if (Mode != PracticeMode.Unlimited || (Phase != Phase.Running && Phase != Phase.Paused)) return false;
            Finish();
            return true;
        }

        internal InputResult Type(char input)
        {
            if (IsDemo || Phase != Phase.Running) return InputResult.Ignored;
            if (Tick()) return InputResult.Finished;
            if (IsDictation) usedAudio = true;
            else usedText = true;
            if (Current == input) return Advance(true);
            mistakes.Add(new TypingMistake(Mistakes + 1, Page, Position, Processed, Passage, input, Elapsed));
            if (!AdvanceOnError) return InputResult.Incorrect;
            skippedPositions.Add(Position);
            return Advance(false);
        }

        internal void DemoStep()
        {
            if (IsDemo && Phase == Phase.Running) Advance(true);
        }

        private InputResult Advance(bool correct)
        {
            Position++;
            Processed++;
            if (correct) Typed++;
            if (Position < Passage.Length) return correct ? InputResult.Correct : InputResult.Incorrect;
            if (Page == PageCount)
            {
                Finish();
                return InputResult.Finished;
            }
            LoadNextPage();
            return InputResult.NextPage;
        }

        private void Finish()
        {
            clock.Stop();
            FinishedAt = DateTimeOffset.Now;
            Phase = Phase.Finished;
        }

        internal int Accuracy
        {
            get
            {
                long attempts = (long)Typed + Mistakes;
                return attempts == 0 ? 100 : (int)Math.Round(Typed * 100.0 / attempts);
            }
        }

        internal int Speed
        {
            get { return (int)Math.Round(60.0 * Typed / Math.Max(1, Elapsed.TotalSeconds)); }
        }
    }

    internal sealed class LessonRecord
    {
        internal int Attempts;
        internal int BestSpeed;
        internal int BestAccuracy;
    }

    internal sealed class GuidePreferences
    {
        private readonly string path;
        internal bool KeyboardVisible { get; set; }
        internal bool FingersVisible { get; set; }

        internal GuidePreferences(string path = null)
        {
            this.path = path;
            KeyboardVisible = true;
            FingersVisible = true;
            if (path == null) return;
            try
            {
                XElement root = XElement.Load(path);
                if (root.Name != "guides") return;
                bool visible;
                if (Boolean.TryParse((string)root.Attribute("keyboard"), out visible)) KeyboardVisible = visible;
                if (Boolean.TryParse((string)root.Attribute("fingers"), out visible)) FingersVisible = visible;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Xml.XmlException) { }
        }

        internal void Save()
        {
            if (path == null) return;
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                new XElement("guides", new XAttribute("keyboard", KeyboardVisible), new XAttribute("fingers", FingersVisible)).Save(temporary);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            finally
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    internal sealed class ProgressStore
    {
        private readonly string path;
        private readonly string legacyPath;
        private Dictionary<string, LessonRecord> records;

        internal ProgressStore(string path, string legacyPath = null)
        {
            this.path = path;
            this.legacyPath = legacyPath;
            records = Read();
        }

        internal LessonRecord For(string key)
        {
            LessonRecord record;
            return records.TryGetValue(key, out record) ? record : new LessonRecord();
        }

        private Dictionary<string, LessonRecord> Read()
        {
            var result = new Dictionary<string, LessonRecord>(StringComparer.Ordinal);
            string source = File.Exists(path) ? path : legacyPath;
            if (source == null) return result;
            try
            {
                XElement root = XDocument.Load(source).Root;
                if (root == null || root.Name != "rhythm") return result;
                foreach (XElement item in root.Elements("lesson"))
                {
                    string key = (string)item.Attribute("key");
                    int oldId = ReadValue(item, "id", -1);
                    if (key == null && oldId >= 0 && oldId <= 7) key = "ru:" + oldId.ToString(CultureInfo.InvariantCulture);
                    if (String.IsNullOrWhiteSpace(key)) continue;
                    result[key] = new LessonRecord
                    {
                        Attempts = Math.Max(0, ReadValue(item, "attempts", 0)),
                        BestSpeed = Math.Max(0, ReadValue(item, "speed", 0)),
                        BestAccuracy = Math.Max(0, Math.Min(100, ReadValue(item, "accuracy", 0)))
                    };
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Xml.XmlException) { }
            return result;
        }

        private static int ReadValue(XElement item, string name, int fallback)
        {
            int number;
            return Int32.TryParse((string)item.Attribute(name), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out number) ? number : fallback;
        }

        internal bool Record(string key, int speed, int accuracy)
        {
            using (Mutex gate = new Mutex(false, @"Local\RhythmTyping.Progress.2"))
            {
                try
                {
                    if (!gate.WaitOne(TimeSpan.FromSeconds(2))) return false;
                }
                catch (AbandonedMutexException) { }

                string temporary = path + ".tmp";
                try
                {
                    var latest = Read();
                    LessonRecord record;
                    if (!latest.TryGetValue(key, out record)) latest[key] = record = new LessonRecord();
                    if (record.Attempts < Int32.MaxValue) record.Attempts++;
                    record.BestSpeed = Math.Max(record.BestSpeed, speed);
                    record.BestAccuracy = Math.Max(record.BestAccuracy, accuracy);
                    records = latest;

                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    new XDocument(new XElement("rhythm", latest.OrderBy(item => item.Key).Select(item => new XElement("lesson",
                        new XAttribute("key", item.Key), new XAttribute("attempts", item.Value.Attempts),
                        new XAttribute("speed", item.Value.BestSpeed), new XAttribute("accuracy", item.Value.BestAccuracy)))))
                        .Save(temporary);
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                    return true;
                }
                catch (IOException) { return false; }
                catch (UnauthorizedAccessException) { return false; }
                finally { gate.ReleaseMutex(); }
            }
        }
    }
}
