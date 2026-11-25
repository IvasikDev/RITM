using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Speech.Synthesis;
using System.Windows.Threading;

namespace RhythmTyping
{
    internal static class Verify2
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void Require(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
        }

        private static void Click(Window window, string name)
        {
            var button = (Button)window.FindName(name);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        }

        private static string Text(Window window, string name)
        {
            return ((TextBlock)window.FindName(name)).Text;
        }

        private static void PumpFor(int milliseconds)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        private static void Render(Window window, string path, int width, int height)
        {
            var root = (Grid)window.FindName("Root");
            root.Background = window.Background;
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();
            PumpFor(40);
            root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            using (var file = File.Create(path))
            {
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(bitmap));
                png.Save(file);
            }
        }

        private static void VerifyCourse(Window window, TrainerWindow trainer, Course course)
        {
            Click(window, course.English ? "EnglishTab" : "RussianTab");
            var keys = (IDictionary)typeof(TrainerWindow).GetField("keys", Instance).GetValue(trainer);
            var shifted = (IDictionary)typeof(TrainerWindow).GetField("shiftedKeys", Instance).GetValue(trainer);
            int count = course.English ? 24 : 25;
            Require(course.Lessons.Length == count && course.Lessons.Select(lesson => lesson.Id).Distinct().Count() == count, "Unique original and poetry lessons");
            Require(Enumerable.Range(0, 20).All(id => course.Lessons.Any(lesson => lesson.Id == id.ToString())), "Legacy lesson identities retained");
            Require(((StackPanel)window.FindName("LessonList")).Children.Count == count, "Navigable lesson buttons");

            var learned = new HashSet<char> { ' ' };
            for (int index = 0; index <= course.Lessons.Length; index++)
            {
                Lesson lesson = index == course.Lessons.Length ? course.FreePractice : course.Lessons[index];
                string label = course.Code + " " + lesson.Title;
                if (lesson.IsPoetry)
                {
                    VerifyPoetry(lesson, keys, shifted);
                    continue;
                }
                Require(lesson.Units.Length >= 95 && lesson.Units.Distinct().Count() == lesson.Units.Length &&
                    lesson.Units.All(unit => unit.Length > 0 && unit.Length <= PassageGenerator.MaxUnitLength), label + " enlarged, deduplicated deck");
                foreach (char character in lesson.Units.SelectMany(unit => unit))
                    Require(keys.Contains(character) || shifted.Contains(character), label + " key " + character);
                if (index < course.Lessons.Length && lesson.Section == LessonSection.Letters && lesson.Id != (course.English ? "15" : "17"))
                {
                    foreach (char character in lesson.Keys.ToLowerInvariant()) learned.Add(character);
                    var untaught = lesson.Units.SelectMany(unit => unit).Where(character => !learned.Contains(character)).Distinct().ToArray();
                    Require(untaught.Length == 0, label + " untaught keys " + new string(untaught));
                }

                var exercise = new Exercise(lesson, new Random(index), PracticeMode.Lesson, false);
                var variant = new Exercise(lesson, new Random(index + 1000), PracticeMode.Lesson, false);
                Require(exercise.Passage != variant.Passage, label + " generated variation");
                Require(exercise.Type(exercise.Current.Value) == InputResult.Ignored, label + " starts on explicit action");
                exercise.Start();
                Require(exercise.Type('\u2603') == InputResult.Incorrect && exercise.Position == 0, label + " wrong key");
                while (exercise.Phase == Phase.Running) exercise.Type(exercise.Current.Value);
                Require(exercise.Page == 3 && exercise.Position == exercise.Passage.Length && exercise.Typed >= 140 && exercise.Mistakes == 1, label + " longer lesson");

                var demo = new Exercise(lesson, new Random(index), PracticeMode.TenMinutes, true);
                demo.Start();
                Require(demo.Type(demo.Current.Value) == InputResult.Ignored && !demo.IsTimed, label + " demo independence");
                while (demo.Phase == Phase.Running) demo.DemoStep();
                Require(demo.Page == 1 && demo.Typed == demo.Passage.Length && demo.Mistakes == 0, label + " demo completion");
            }
            Require(course.Lessons.Where(lesson => lesson.Id == "20").Single().Units.All(word => word.Length >= 2 && word.Length <= 4), "Short word drill");
            Require(course.Lessons.Where(lesson => lesson.Id == "21").Single().Units.All(word => word.Length >= 8), "Long word drill");
            Require(course.Lessons.Where(lesson => lesson.Id == "22").Single().Units.SelectMany(unit => unit).All(character => !Char.IsLetter(character)),
                "Dedicated symbol combinations");
            int words = course.Lessons.Where(lesson => lesson.Section == LessonSection.Words).SelectMany(lesson => lesson.Units)
                .Where(unit => unit.All(Char.IsLetter)).Distinct().Count();
            Require(words >= 650 && course.FreePractice.Units.Length >= 1200, "Substantial word and sentence pools");
            Console.WriteLine("{0}: {1} distinct words, {2} free-practice entries, {3} total lesson entries.", course.Code, words,
                course.FreePractice.Units.Length, course.Lessons.Sum(lesson => lesson.Units.Length));
        }

        private static void VerifyPoetry(Lesson lesson, IDictionary keys, IDictionary shifted)
        {
            Require(lesson.Section == LessonSection.Texts, "Poetry belongs to Texts");
            foreach (Poem poem in lesson.Poems)
            {
                var sample = new Lesson(lesson.Id, lesson.Title, new[] { poem });
                var exercise = new Exercise(sample, new Random(1), PracticeMode.Lesson, false);
                exercise.Start();
                var lines = new List<string>();
                while (exercise.Phase == Phase.Running)
                {
                    Require(exercise.Verse.Title == poem.Title && exercise.Passage.Length <= 118, "Verse page title and limit");
                    lines.AddRange(exercise.Verse.Lines);
                    int page = exercise.Page;
                    while (exercise.Page == page && exercise.Phase == Phase.Running)
                    {
                        char expected = exercise.Current.Value;
                        Require(keys.Contains(expected) || shifted.Contains(expected), "Poetry character available on keyboard: " + expected);
                        exercise.Type(expected);
                    }
                }
                Require(lines.SequenceEqual(poem.Lines), "Complete poem is typed in its original line order");
            }
        }

        private static void VerifyContinuous()
        {
            var exercise = new Exercise(Catalog.English.FreePractice, new Random(42), PracticeMode.Unlimited, false);
            Require(!exercise.FinishUnlimited(), "Cannot finish before starting");
            exercise.Start();
            var pages = new HashSet<string>();
            while (exercise.Page <= 150)
            {
                int page = exercise.Page;
                Require(exercise.Passage.EndsWith(" "), "Page separator");
                pages.Add(exercise.Passage);
                while (exercise.Page == page) exercise.Type(exercise.Current.Value);
            }
            Require(pages.Count >= 145 && exercise.Typed > 10000 && exercise.Phase == Phase.Running, "Continuous variety and counters");
            exercise.Pause();
            Require(exercise.FinishUnlimited() && exercise.Phase == Phase.Finished && !exercise.FinishUnlimited(), "Finish unlimited while paused");
        }

        private static void VerifyTimed(Window window, TrainerWindow trainer, ProgressStore store)
        {
            Click(window, "FreeButton");
            Require(((Button)window.FindName("LessonMode")).Visibility == Visibility.Collapsed, "Free practice has three modes");
            Click(window, "FiveMode");
            Require(Text(window, "TimeValue") == "05:00", "Five-minute control");
            Click(window, "TenMode");
            Require(Text(window, "TimeValue") == "10:00", "Ten-minute control");

            var field = typeof(TrainerWindow).GetField("exercise", Instance);
            var submit = typeof(TrainerWindow).GetMethod("Submit", Instance);
            var timed = new Exercise(Catalog.English.FreePractice, new Random(5), PracticeMode.TenMinutes, false, TimeSpan.FromMilliseconds(240));
            field.SetValue(trainer, timed);
            typeof(TrainerWindow).GetMethod("BuildPassage", Instance).Invoke(trainer, null);
            Click(window, "ActionButton");
            submit.Invoke(trainer, new object[] { timed.Current.Value });
            Click(window, "ActionButton");
            TimeSpan remaining = timed.Remaining;
            Thread.Sleep(270);
            Require(timed.Remaining == remaining && timed.Phase == Phase.Paused, "Timer freezes on pause");
            Click(window, "ActionButton");
            PumpFor(500);
            Require(timed.Phase == Phase.Finished && timed.Remaining == TimeSpan.Zero && timed.Typed == 1, "Dispatcher ends on timeout and preserves typed count");
            Require(Text(window, "TimeValue") == "00:00" && Text(window, "StatusLabel").StartsWith("Время вышло."), "Window reports timeout");
            Require(((Border)window.FindName("ReportPanel")).Visibility == Visibility.Visible && Text(window, "ReportSummary") == "Ошибок нет.",
                "Timed completion opens a report even with no errors");
            Require(store.For("en:free:TenMinutes").Attempts == 1, "Timer result has a separate record");

            Click(window, "UnlimitedMode");
            Click(window, "ActionButton");
            var unlimited = (Exercise)field.GetValue(trainer);
            submit.Invoke(trainer, new object[] { unlimited.Current.Value });
            Click(window, "FinishButton");
            Require(unlimited.Phase == Phase.Finished && store.For("en:free:Unlimited").Attempts == 1, "Finish button records unlimited practice");
        }

        private static void VerifyLessonWindow(Window window, TrainerWindow trainer, ProgressStore store)
        {
            var list = (StackPanel)window.FindName("LessonList");
            var first = (Button)list.Children[0];
            first.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, first));
            Click(window, "ActionButton");
            var field = typeof(TrainerWindow).GetField("exercise", Instance);
            var submit = typeof(TrainerWindow).GetMethod("Submit", Instance);
            var session = (Exercise)field.GetValue(trainer);
            int lastPage = 1;
            while (session.Phase == Phase.Running)
            {
                submit.Invoke(trainer, new object[] { session.Current.Value });
                if (session.Page == lastPage) continue;
                lastPage = session.Page;
                Require(Text(window, "PositionLabel").StartsWith("Фрагмент " + lastPage + " из 3"), "Page changes without stopping the session");
            }
            Require(session.Typed > 250 && store.For("en:0").Attempts == 1 && Text(window, "CourseCount") == "1 / 24", "Completed lesson writes course progress");
            Click(window, "ActionButton");
            Require(Text(window, "LessonTitle") == Catalog.English.Lessons[1].Title, "Next lesson action");
        }

        private sealed class RecordingSpeech : IWordSpeech
        {
            internal readonly List<string> Prompts = new List<string>();
            internal int ErrorSignals;
            internal bool Available = true;
            internal bool English;
            public bool Prepare(bool english) { English = english; return Available; }
            public bool Speak(string text) { Prompts.Add(text); return Available; }
            public void SignalError() { ErrorSignals++; }
            public void Stop() { }
            public void Dispose() { }
        }

        private static void VerifyDictation(Window window, TrainerWindow trainer, ProgressStore store, RecordingSpeech speech, string directory)
        {
            Click(window, "RussianTab");
            Click(window, "WordsSection");
            Click(window, "UnlimitedMode");
            var toggle = (ToggleButton)window.FindName("AudioToggle");
            var field = typeof(TrainerWindow).GetField("exercise", Instance);
            var exercise = (Exercise)field.GetValue(trainer);
            string originalPassage = exercise.Passage;
            Require(toggle.Visibility == Visibility.Visible && toggle.IsChecked == false, "Audio icon starts off beside the guides");
            int prior = speech.Prompts.Count;
            toggle.IsChecked = true;
            Require(ReferenceEquals(exercise, field.GetValue(trainer)) && originalPassage == exercise.Passage && speech.Prompts.Count == prior + 1 &&
                exercise.Phase == Phase.Ready, "Audio previews the opening group without starting the timer");
            var passage = (StackPanel)window.FindName("PassageBox");
            var label = (TextBlock)((Border)((StackPanel)((WrapPanel)passage.Children[0]).Children[0]).Children[0]).Child;
            Require(exercise.IsDictation && label.Text == "•" && AutomationProperties.GetName(passage) == "Скрытый текст для набора на слух",
                "Dictation hides the answer in UI and accessibility");
            var keyboard = (ToggleButton)window.FindName("KeyboardToggle");
            var fingers = (ToggleButton)window.FindName("FingersToggle");
            var keyboardDeck = (Border)window.FindName("KeyboardDeck");
            var hands = (Border)window.FindName("HandsPanel");
            Require(keyboardDeck.Visibility == Visibility.Visible && hands.Visibility == Visibility.Visible && !speech.English,
                "The enabled guides remain visible in Russian dictation");
            int initial = speech.Prompts.Count;
            Click(window, "ActionButton");
            string first = WordSpeech.SentencePrompt(exercise.TextToDictate(), false);
            Require(speech.Prompts.Count == initial + 1 && speech.Prompts.Last() == first && first.Split(' ').Length == 6,
                "An unpunctuated drill starts with a short group of words");
            keyboard.IsChecked = false;
            Require(keyboardDeck.Visibility == Visibility.Collapsed && hands.Visibility == Visibility.Visible, "Keyboard can be hidden independently during dictation");
            fingers.IsChecked = false;
            keyboard.IsChecked = true;
            Require(keyboardDeck.Visibility == Visibility.Visible && hands.Visibility == Visibility.Collapsed, "Hands can be hidden independently during dictation");
            fingers.IsChecked = true;
            var submit = typeof(TrainerWindow).GetMethod("Submit", Instance);
            Action<char> type = character => submit.Invoke(trainer, new object[] { character });
            int signals = speech.ErrorSignals;
            type('!');
            Require(speech.ErrorSignals == signals + 1 && exercise.Position == 0 && speech.Prompts.Count == initial + 1,
                "A dictation typo signals immediately and waits for the correct letter");
            while (exercise.Current != ' ') type(exercise.Current.Value);
            type(' ');
            Require(speech.Prompts.Count == initial + 1 && speech.ErrorSignals == signals + 1, "An internal space does not start a new dictation prompt");
            Click(window, "RepeatSentence");
            Require(speech.Prompts.Count == initial + 2 && speech.Prompts.Last() == first, "Repeat starts the current group from the beginning");
            PumpFor(300);
            Require(speech.Prompts.Count == initial + 2, "Dictation waits for the current group to be entered");
            int end = exercise.DictationEnd;
            while (exercise.Position < end) type(exercise.Current.Value);
            Require(speech.Prompts.Count == initial + 3 && speech.Prompts.Last() == WordSpeech.SentencePrompt(exercise.TextToDictate(), false),
                "Finishing the group triggers the next prompt once");
            Render(window, Path.Combine(directory, "Rhythm0213-Dictation.png"), 900, 628);
            double buttonRight = toggle.TranslatePoint(new Point(toggle.ActualWidth, 0), (Grid)window.FindName("Root")).X;
            Require(toggle.ActualWidth >= 25 && buttonRight <= 900, "Audio icon fits in the compact toolbar");
            Click(window, "FinishButton");
            var report = (SessionReport)typeof(TrainerWindow).GetField("report", Instance).GetValue(trainer);
            Require(report.ToMarkdown().Contains("activity: \"dictation\"") && store.For("ru:20:Unlimited:dictation").Attempts == 1,
                "Dictation has marked reports and separate records");

            Click(window, "ResetButton");
            var waitAfterError = (ToggleButton)window.FindName("WaitAfterError");
            waitAfterError.IsChecked = false;
            exercise = (Exercise)field.GetValue(trainer);
            Click(window, "ActionButton");
            end = exercise.DictationEnd;
            while (exercise.Position < end - 1) type(exercise.Current.Value);
            initial = speech.Prompts.Count;
            signals = speech.ErrorSignals;
            int space = exercise.Position;
            type('!');
            Require(speech.ErrorSignals == signals + 1 && exercise.Position == space + 1 && speech.Prompts.Count == initial,
                "Skipping a mistyped separator signals before the next spoken group");
            PumpFor(400);
            Require(speech.Prompts.Count == initial + 1 && speech.Prompts.Last() == WordSpeech.SentencePrompt(exercise.TextToDictate(), false),
                "Dictation then speaks the next group exactly once");
            Click(window, "ResetButton");
            waitAfterError.IsChecked = true;

            var lessonList = (StackPanel)window.FindName("LessonList");
            var poem = lessonList.Children.Cast<Button>().First(button => Catalog.Russian.Lessons[(int)button.Tag].IsPoetry);
            Require(poem.Visibility == Visibility.Collapsed, "Poem is absent from Words");
            Require(store.Record("ru:23", 170, 98), "Existing poetry record uses its stable ID");
            Click(window, "TextsSection");
            Require(poem.Visibility == Visibility.Visible && Text(window, "SectionCount") == "1 / 4", "Texts lists the poem and its existing result");
            poem.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, poem));
            exercise = (Exercise)field.GetValue(trainer);
            Require(toggle.Visibility == Visibility.Visible && exercise.IsDictation && passage.Children.Count > 1, "Poetry can also be dictated while preserving its lines");
            Click(window, "ActionButton");
            submit.Invoke(trainer, new object[] { exercise.Current.Value });
            int beforeToggle = exercise.Processed;
            toggle.IsChecked = false;
            Require(exercise.Phase == Phase.Running && exercise.Processed == beforeToggle && ReferenceEquals(exercise, field.GetValue(trainer)) &&
                ((Border)window.FindName("KeyboardPanel")).Visibility == Visibility.Visible, "Turning off audio restores hints without interrupting typing");
            submit.Invoke(trainer, new object[] { exercise.Current.Value });
            Click(window, "FinishButton");
            report = (SessionReport)typeof(TrainerWindow).GetField("report", Instance).GetValue(trainer);
            Require(report.ToMarkdown().Contains("activity: \"mixed\"") && store.For("ru:23:Unlimited:mixed").Attempts == 1, "Switching mid-attempt records mixed practice");
            Click(window, "EnglishTab");
            Click(window, "SymbolsSection");
            speech.Available = false;
            toggle.IsChecked = true;
            Click(window, "ActionButton");
            exercise = (Exercise)field.GetValue(trainer);
            Require(exercise.Phase == Phase.Ready && speech.English && Text(window, "StatusLabel").Contains("Английский голос"), "Missing system voice blocks start with a clear message");
            speech.Available = true;
            toggle.IsChecked = false;
            Click(window, "FreeButton");
            Click(window, "ActionButton");
            exercise = (Exercise)field.GetValue(trainer);
            initial = speech.Prompts.Count;
            toggle.IsChecked = true;
            Require(exercise.IsDictation && exercise.Phase == Phase.Running && speech.Prompts.Count == initial + 1, "Toggling during free practice immediately plays the current sentence");
            toggle.IsChecked = false;
            Click(window, "ActionButton");

            Require(WordSpeech.Prompt("09:15.", false) == "0, 9, двоеточие, 1, 5, точка", "Numeric dictation gives exact symbols");
            Require(WordSpeech.Prompt("Can't!", true) == "start with a capital letter, Can't, apostrophe after letter 3, exclamation mark", "Dictation explains case and punctuation");
        }

        private static void VerifySentenceBoundaries()
        {
            string[][] passages = {
                new[] { "Утром было ровно 3.5 градуса и шёл лёгкий дождь.", "Ты видел этот дождь?!" },
                new[] { "It was exactly 3.5 degrees and raining in the morning.", "Did you see the rain?!" }
            };
            foreach (string[] sentences in passages)
            {
                var lesson = new Lesson("test", "Test", "", "", String.Join(" ", sentences) + "|" + String.Join(" ", sentences));
                var exercise = new Exercise(lesson, new Random(1), PracticeMode.Unlimited, false, isDictation: true);
                Require(exercise.TextToDictate() == sentences[0], "Dictation keeps a full sentence, including a decimal and more than six words");
                exercise.Start();
                int end = exercise.DictationEnd;
                while (exercise.Position < end - 1) exercise.Type(exercise.Current.Value);
                Require(exercise.Current == ' ' && exercise.TextToDictate() == sentences[0], "Repeat still targets the same sentence on its separating space");
                exercise.Type(' ');
                Require(exercise.TextToDictate() == sentences[1], "Typing the separator selects the next full sentence");
                while (exercise.Page == 1) exercise.Type(exercise.Current.Value);
                Require(exercise.Position == 0 && exercise.TextToDictate() == sentences[0], "Loading a page resets sentence boundaries");
            }
        }

        private static void VerifySystemVoices()
        {
            foreach (bool english in new[] { false, true })
            {
                using (var synth = new SpeechSynthesizer())
                using (var wave = new MemoryStream())
                {
                    VoiceInfo voice = WordSpeech.FindVoice(synth, english);
                    Require(voice != null, "Matching installed Windows voice");
                    synth.SelectVoice(voice.Name);
                    synth.SetOutputToWaveStream(wave);
                    synth.Speak(english ? "keyboard" : "клавиатура");
                    synth.SetOutputToNull();
                    Require(wave.Length > 1000, "Voice produces audio samples");
                    Console.WriteLine("Synthesized {0} audio with {1}: {2} bytes.", voice.Culture.Name, voice.Name, wave.Length);
                }
            }
        }

        private static void VerifySections(Window window, TrainerWindow trainer, string directory)
        {
            string[] names = { "LettersSection", "WordsSection", "SymbolsSection", "TextsSection" };
            foreach (Course course in new[] { Catalog.Russian, Catalog.English })
            {
                Click(window, course.English ? "EnglishTab" : "RussianTab");
                for (int section = 0; section < names.Length; section++)
                {
                    Click(window, names[section]);
                    var list = (StackPanel)window.FindName("LessonList");
                    int expected = course.Lessons.Count(lesson => lesson.Section == (LessonSection)section);
                    var visible = list.Children.Cast<Button>().Where(button => button.Visibility == Visibility.Visible).ToArray();
                    Require(expected >= 2 && visible.Length == expected && visible.All(item => course.Lessons[(int)item.Tag].Section == (LessonSection)section),
                        "Section shows only its own lessons");
                    Require(Text(window, "SectionCount").EndsWith("/ " + expected), "Section count uses visible lessons");
                    Require(Text(window, "LessonTitle") == course.Lessons[(int)visible[0].Tag].Title || section == 0,
                        "New section opens its first unfinished lesson");
                    string groupTitle = ((Button)window.FindName(names[section])).Content.ToString();
                    Require(Text(window, "LessonNumber").Contains(groupTitle.ToUpperInvariant()), "Current section appears in the lesson header");
                }
            }

            Click(window, "WordsSection");
            var lessonList = (StackPanel)window.FindName("LessonList");
            var last = lessonList.Children.Cast<Button>().Last(button => button.Visibility == Visibility.Visible);
            last.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, last));
            Click(window, "ActionButton");
            var session = (Exercise)typeof(TrainerWindow).GetField("exercise", Instance).GetValue(trainer);
            var submit = typeof(TrainerWindow).GetMethod("Submit", Instance);
            while (session.Phase == Phase.Running) submit.Invoke(trainer, new object[] { session.Current.Value });
            Require(((Button)window.FindName("ActionButton")).Content.ToString() == "Следующий раздел", "Last lesson offers the next section");
            Click(window, "ActionButton");
            Require(Text(window, "LessonNumber").Contains("ЦИФРЫ И ЗНАКИ"), "Next action opens the next skill group");
            Click(window, "RussianTab");
            Require(Text(window, "LessonNumber").Contains("РУССКАЯ  /  ЦИФРЫ И ЗНАКИ"), "Language switch preserves the skill group");
            var symbols = ((StackPanel)window.FindName("LessonList")).Children.Cast<Button>().First(button => Catalog.Russian.Lessons[(int)button.Tag].Id == "22");
            symbols.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, symbols));
            Render(window, Path.Combine(directory, "Rhythm23-RU-Symbols.png"), 1194, 776);
            Render(window, Path.Combine(directory, "Rhythm23-RU-Symbols-Compact.png"), 900, 628);
            Click(window, "LettersSection");
            Click(window, "EnglishTab");
        }

        private static void VerifyHands(Window window, TrainerWindow trainer, string directory)
        {
            Click(window, "RussianTab");
            var left = (Canvas)window.FindName("LeftHand");
            var right = (Canvas)window.FindName("RightHand");
            Require(((TextBlock)left.Children[9]).Text == "А" && ((TextBlock)right.Children[9]).Text == "О", "Russian home position labels");
            var keys = (IDictionary)typeof(TrainerWindow).GetField("keys", Instance).GetValue(trainer);
            Click(window, "HomePositionButton");
            string home = HandGuide.HomeKeys(false);
            for (int i = 0; i < home.Length; i++)
                Require(CapFill(keys[home[i]]) == HandGuide.RestTint(i), "Custom Russian home key " + home[i]);
            Require(CapFill(keys[' ']) == HandGuide.RestTint(HandGuide.Thumb), "Both thumbs rest above space");
            Render(window, Path.Combine(directory, "Rhythm22-RU-Home.png"), 1194, 776);

            var field = typeof(TrainerWindow).GetField("exercise", Instance);
            var source = new Lesson("test", "Test", "", "", "Ф а|Ф а");
            var session = new Exercise(source, new Random(5), PracticeMode.Lesson, false);
            field.SetValue(trainer, session);
            typeof(TrainerWindow).GetMethod("BuildPassage", Instance).Invoke(trainer, null);
            Click(window, "ActionButton");
            Require(Text(window, "FingerLabel").Contains("правый Shift"), "Opposite Shift guidance");
            Require(((System.Windows.Shapes.Path)left.Children[0]).Fill == HandGuide.BrightColor(0) &&
                ((System.Windows.Shapes.Path)right.Children[0]).Fill == HandGuide.BrightColor(7), "Both mapped fingers highlight on Shift");
            Click(window, "HomePositionButton");
            Require(session.Phase == Phase.Paused && session.Position == 0 && Text(window, "FingerLabel").StartsWith("Ф Ы В А"), "Home guide pauses and preserves position");
            Click(window, "ActionButton");
            var submit = typeof(TrainerWindow).GetMethod("Submit", Instance);
            submit.Invoke(trainer, new object[] { 'a' });
            Require(((System.Windows.Shapes.Path)left.Children[0]).Fill == HandGuide.ErrorFill, "Wrong keystroke marks the same finger");
            submit.Invoke(trainer, new object[] { 'Ф' });
            Require(((System.Windows.Shapes.Path)left.Children[4]).Fill == HandGuide.BrightColor(HandGuide.Thumb) &&
                ((System.Windows.Shapes.Path)right.Children[4]).Fill == HandGuide.BrightColor(HandGuide.Thumb), "Space highlights either thumb");

            Click(window, "EnglishTab");
            Click(window, "HomePositionButton");
            Require(((TextBlock)left.Children[9]).Text == "F" && ((TextBlock)right.Children[9]).Text == "J" &&
                ((TextBlock)right.Children[6]).Text == ";", "US QWERTY home position, including semicolon");
            Require(Text(window, "FingerLabel").StartsWith("A S D F") && Text(window, "Instruction").Contains("F и J"), "English guidance updates on layout switch");
            Render(window, Path.Combine(directory, "Rhythm22-EN-Home-Compact.png"), 900, 628);
            Click(window, "HomePositionButton");

            Click(window, "DemoTab");
            var demo = new Exercise(new Lesson("test-demo", "Test", "", "", "F j|F j"), new Random(5), PracticeMode.Lesson, true);
            field.SetValue(trainer, demo);
            typeof(TrainerWindow).GetMethod("BuildPassage", Instance).Invoke(trainer, null);
            Click(window, "ActionButton");
            Require(Text(window, "FingerLabel").Contains("правый Shift"), "Demo shows the opening gesture");
            PumpFor(550);
            Require(demo.Position == 0 && Text(window, "FingerLabel").Contains("правый Shift"), "English demo keeps the opening gesture visible");
            PumpFor(550);
            Require(demo.Position == 1 && Text(window, "FingerLabel").StartsWith("Пробел"), "Demo changes the guide together with the passage");
            Click(window, "ActionButton");
            Click(window, "PracticeTab");
        }

        private static Brush CapFill(object key)
        {
            return ((Border)key.GetType().GetField("Surface", Instance).GetValue(key)).Background;
        }

        private static void VerifyReport(Window window, TrainerWindow trainer, string directory)
        {
            Click(window, "RussianTab");
            Click(window, "WordsSection");
            Click(window, "UnlimitedMode");
            var sample = new Lesson("report-test", "Fixture", "", "", "Жук в саду.|Жук в саду.");
            var exercise = new Exercise(sample, new Random(10), PracticeMode.Unlimited, false);
            typeof(TrainerWindow).GetField("exercise", Instance).SetValue(trainer, exercise);
            typeof(TrainerWindow).GetMethod("BuildPassage", Instance).Invoke(trainer, null);
            var submit = typeof(TrainerWindow).GetMethod("Submit", Instance);
            Action<char> type = character => submit.Invoke(trainer, new object[] { character });
            Click(window, "ActionButton");
            int firstPage = exercise.Passage.Length;
            type('ж');
            type('x');
            while (exercise.Current != ' ') type(exercise.Current.Value);
            type('|');
            while (exercise.Page == 1) type(exercise.Current.Value);
            type('Ш');
            type('Ш');
            type(exercise.Current.Value);
            Click(window, "FinishButton");

            var report = (SessionReport)typeof(TrainerWindow).GetField("report", Instance).GetValue(trainer);
            var entries = report.Errors;
            Require(entries.Count == 5 && report.ErrorPositions == 3 && exercise.Mistakes == 5, "Repeated mistakes are kept separately from unique positions");
            Require(entries[0].Page == 1 && entries[0].Position == 1 && entries[0].Expected == 'Ж' && entries[0].Entered == 'ж', "First typo includes expected and entered characters");
            Require(entries[1].GlobalPosition == 1 && entries[2].ExpectedLabel == "Пробел" && entries[2].Context.Contains("[␣]"), "Same position and visible whitespace");
            Require(entries[3].Page == 2 && entries[3].Position == 1 && entries[3].GlobalPosition == firstPage + 1 && entries[3].Number == 4,
                "Positions remain accurate across generated pages");
            Require(report.Recommendations.Any(item => item.Contains("раскладка")) && report.Recommendations.Any(item => item.Contains("Shift")) &&
                report.Recommendations.Any(item => item.Contains("«Ж»")), "Recommendations reflect the typing error patterns");
            Require(((Border)window.FindName("ReportPanel")).Visibility == Visibility.Visible && ((DataGrid)window.FindName("MistakeTable")).Items.Count == 5 &&
                ((Border)window.FindName("KeyboardPanel")).Visibility == Visibility.Collapsed, "Completion presents the readable error report");
            Require(Text(window, "FooterVersion") == "Версия " + App.Version, "Pre-release version appears in the app");
            Render(window, Path.Combine(directory, "Rhythm024-Report.png"), 1194, 776);
            Render(window, Path.Combine(directory, "Rhythm024-Report-Compact.png"), 900, 628);

            string file = Path.Combine(directory, "Rhythm024-Sample.md");
            report.SaveMarkdown(file);
            string contents = File.ReadAllText(file);
            Require(contents.StartsWith("---") && contents.Contains("schema: rhythm.practice.v1") && contents.Contains("app_version: \"" + App.Version + "\"") &&
                contents.Contains("mode: \"unlimited\"") && contents.Contains("section: \"words\"") && contents.Contains("errors: 5") &&
                contents.Contains("error_positions: 3"), "Markdown YAML metadata supports automation");
            Require(contents.Contains("U+007C") && contents.Contains("&#124;") && contents.Contains("## Где были ошибки") &&
                contents.Contains("Жук"), "Markdown represents literal table delimiters and retains source text");
            report.SaveMarkdown(file);
            Require(contents == File.ReadAllText(file), "Re-export preserves attempt identity and content");

            var empty = new Exercise(sample, new Random(1), PracticeMode.Unlimited, false);
            empty.Start();
            empty.FinishUnlimited();
            var noInput = new SessionReport(empty, Catalog.Russian, Catalog.Russian.FreePractice);
            Require(!noInput.HasInput && noInput.ToMarkdown().Contains("accuracy_percent: null") && noInput.ToMarkdown().Contains("section: \"free_practice\""),
                "Empty sessions cannot be mistaken for perfect accuracy");
            Click(window, "ResetButton");
            Require(((Border)window.FindName("ReportPanel")).Visibility == Visibility.Collapsed && ((StackPanel)window.FindName("PracticeDetails")).Visibility == Visibility.Visible,
                "Starting a new exercise clears old report state");
        }

        private static void VerifyGuideToggles(Window window, TrainerWindow trainer, string preferences, string directory)
        {
            Click(window, "RussianTab");
            Click(window, "LettersSection");
            Click(window, "FiveMode");
            var keyboard = (ToggleButton)window.FindName("KeyboardToggle");
            var fingers = (ToggleButton)window.FindName("FingersToggle");
            var keyboardDeck = (Border)window.FindName("KeyboardDeck");
            var hands = (Border)window.FindName("HandsPanel");
            var panel = (Border)window.FindName("KeyboardPanel");
            var fingerLabel = (TextBlock)window.FindName("FingerLabel");
            var field = typeof(TrainerWindow).GetField("exercise", Instance);
            var exercise = (Exercise)field.GetValue(trainer);
            var submit = typeof(TrainerWindow).GetMethod("Submit", Instance);
            Require(keyboard.IsChecked == true && fingers.IsChecked == true && keyboardDeck.Visibility == Visibility.Visible &&
                hands.Visibility == Visibility.Visible, "Both guides default to visible");
            Click(window, "ActionButton");
            submit.Invoke(trainer, new object[] { exercise.Current.Value });
            Render(window, Path.Combine(directory, "Rhythm025-Both.png"), 1194, 776);

            keyboard.IsChecked = false;
            Require(keyboardDeck.Visibility == Visibility.Collapsed && hands.Visibility == Visibility.Visible && Grid.GetColumn(hands) == 0 &&
                fingerLabel.Visibility == Visibility.Visible, "Hands and finger label remain useful without the keyboard");
            Render(window, Path.Combine(directory, "Rhythm025-Fingers.png"), 1194, 776);
            fingers.IsChecked = false;
            Require(panel.Visibility == Visibility.Collapsed && !new GuidePreferences(preferences).KeyboardVisible &&
                !new GuidePreferences(preferences).FingersVisible, "Both hints can be removed and the choice persists");
            Require(keyboard.ToolTip.ToString().StartsWith("Показать") && fingers.ToolTip.ToString().StartsWith("Показать"), "Icons offer restoration when off");
            Render(window, Path.Combine(directory, "Rhythm025-Neither.png"), 900, 628);
            submit.Invoke(trainer, new object[] { exercise.Current.Value });

            keyboard.IsChecked = true;
            Require(keyboardDeck.Visibility == Visibility.Visible && hands.Visibility == Visibility.Collapsed && fingerLabel.Visibility == Visibility.Collapsed &&
                ((TextBlock)window.FindName("Instruction")).Visibility == Visibility.Collapsed &&
                ((Button)window.FindName("HomePositionButton")).Visibility == Visibility.Collapsed, "Keyboard can be used alone without finger assistance");
            Require(Object.ReferenceEquals(field.GetValue(trainer), exercise) && exercise.Typed == 2 && exercise.Phase == Phase.Running &&
                exercise.Elapsed > TimeSpan.Zero, "Changing hints preserves the active timed session and typed input");
            fingers.IsChecked = true;
            Require(hands.Visibility == Visibility.Visible && Grid.GetColumn(hands) == 2 && new GuidePreferences(preferences).FingersVisible,
                "Both guides can be restored immediately");
            Click(window, "ActionButton");
            Click(window, "FreeButton");
        }

        private static void VerifyAdvanceOnError(Window window, TrainerWindow trainer, RecordingSpeech speech, string directory)
        {
            Require(window.WindowState == WindowState.Maximized, "Application starts maximized");
            Require(Text(window, "FooterAuthor") == "Автор: Ивасик", "Author shown in footer");
            Click(window, "RussianTab");
            Click(window, "LettersSection");
            Click(window, "LessonMode");
            var toggle = (ToggleButton)window.FindName("WaitAfterError");
            var field = typeof(TrainerWindow).GetField("exercise", Instance);
            var exercise = (Exercise)field.GetValue(trainer);
            Require(toggle.IsChecked == true && !exercise.AdvanceOnError && AutomationProperties.GetName(toggle) == "После ошибки тренажёр ждёт правильного знака",
                "Blue correction switch starts on and is labeled with its current behavior");
            var submit = typeof(TrainerWindow).GetMethod("Submit", Instance);
            Action<char> type = character => submit.Invoke(trainer, new object[] { character });
            toggle.IsChecked = false;
            Require(exercise.AdvanceOnError && ReferenceEquals(exercise, field.GetValue(trainer)) &&
                AutomationProperties.GetName(toggle) == "После ошибки курсор переходит к следующему знаку", "Dark switch advances on mistakes");
            Click(window, "ActionButton");
            int firstPageLength = exercise.Passage.Length;
            int signals = speech.ErrorSignals;
            type('я');
            Require(exercise.Position == 1 && exercise.Processed == 1 && exercise.Typed == 0 && exercise.Mistakes == 1 && exercise.WasSkipped(0),
                "Wrong character advances and is marked without counting as correct");
            Require(speech.ErrorSignals == signals + 1, "A visual practice typo also signals immediately");
            Require(!toggle.IsEnabled && Text(window, "StatusLabel").Contains("следующего знака"), "Active session explains skipped error");
            type(exercise.Current.Value);
            Require(exercise.Accuracy == 50 && exercise.Processed == 2, "Accuracy includes the skipped mistake");
            Render(window, Path.Combine(directory, "Rhythm027-Advance.png"), 1194, 776);
            while (exercise.Position < firstPageLength - 1) type(exercise.Current.Value);
            type('я');
            Require(exercise.Page == 2 && exercise.Position == 0 && !exercise.WasSkipped(0), "Wrong final character moves to the next page");
            type('я');
            Require(exercise.ErrorLog[2].GlobalPosition == firstPageLength + 1 && exercise.ErrorLog[2].Page == 2, "Error report keeps the true position after skips");
            while (exercise.Page != exercise.PageCount || exercise.Position < exercise.Passage.Length - 1) type(exercise.Current.Value);
            type('я');
            Require(exercise.Phase == Phase.Finished && exercise.Mistakes == 4 && exercise.Processed == exercise.Typed + 4, "Wrong final character completes the lesson");
            Require(speech.ErrorSignals == signals + 4, "Every error signals, including the last character");
            var report = (SessionReport)typeof(TrainerWindow).GetField("report", Instance).GetValue(trainer);
            Require(report.Errors.Count == 4 && report.ToMarkdown().Contains("error_policy: \"advance\"") &&
                report.ToMarkdown().Contains("processed_chars: " + exercise.Processed), "Markdown identifies the selected error policy");
            Click(window, "ResetButton");
            toggle.IsChecked = true;
        }

        [STAThread]
        private static void Main(string[] args)
        {
            string directory = Path.Combine(args[0], Guid.NewGuid().ToString());
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, "course-progress.xml");
            string legacy = Path.Combine(directory, "progress.xml");
            string preferences = Path.Combine(directory, "guides.xml");
            string original = "<rhythm><lesson id='2' attempts='3' speed='125' accuracy='97'/></rhythm>";
            File.WriteAllText(legacy, original);
            var store = new ProgressStore(file, legacy);
            var other = new ProgressStore(file, legacy);
            Require(store.For("ru:2").Attempts == 3 && store.For("en:2").Attempts == 0, "Migrate by lesson identity, without mixing layouts");
            var application = new Application();
            using (Stream layout = Assembly.GetExecutingAssembly().GetManifestResourceStream("RhythmTyping.MainWindow.xaml"))
            {
                Window window = (Window)XamlReader.Load(layout);
                var speech = new RecordingSpeech();
                var trainer = new TrainerWindow(window, store, new GuidePreferences(preferences), speech);
                var brandIcon = (Image)window.FindName("BrandIcon");
                Require(brandIcon.Source == window.Icon && ((BitmapSource)window.Icon).PixelWidth == 128, "Window and header use the embedded brand image");
                Require(window.Background is RadialGradientBrush && ((Button)window.FindName("ActionButton")).Background is LinearGradientBrush,
                    "Gradient desktop theme is active");
                Require(Text(window, "CourseCount") == "1 / 25", "Legacy course progress appears in the window");
                VerifyCourse(window, trainer, Catalog.Russian);
                Render(window, Path.Combine(args[0], "Rhythm2-RU.png"), 900, 628);
                VerifyCourse(window, trainer, Catalog.English);
                Require(Text(window, "CourseCount") == "0 / 24", "English progress starts separately");
                VerifyContinuous();
                VerifyLessonWindow(window, trainer, store);
                VerifyTimed(window, trainer, store);
                Click(window, "TenMode");
                VerifySections(window, trainer, args[0]);
                VerifyHands(window, trainer, args[0]);
                VerifyReport(window, trainer, directory);
                VerifyGuideToggles(window, trainer, preferences, directory);
                VerifyAdvanceOnError(window, trainer, speech, directory);
                VerifyDictation(window, trainer, store, speech, directory);
                VerifySentenceBoundaries();
                Click(window, "RussianTab");
                Click(window, "LettersSection");
                typeof(TrainerWindow).GetMethod("SelectLesson", Instance).Invoke(trainer, new object[] { 0 });
                Click(window, "DemoTab");
                Click(window, "ActionButton");
                PumpFor(550);
                Require(Text(window, "PassageLabel") == "Показ набора" && Text(window, "PositionLabel").StartsWith("0 /"), "Russian demo holds the first letter for the slower interval");
                PumpFor(550);
                Require(Text(window, "PositionLabel").StartsWith("1 /"), "Russian demo advances one position at the slower pace");
                Click(window, "ActionButton");
                PumpFor(1000);
                Require(Text(window, "PositionLabel").StartsWith("1 /"), "Pausing the demo holds the current position");
            }
            Require(other.Record("ru:2", 100, 95), "Merge another instance's new records");
            var restored = new ProgressStore(file, legacy);
            Require(restored.For("ru:2").Attempts == 4 && restored.For("ru:2").BestSpeed == 125 && restored.For("ru:2").BestAccuracy == 97, "Restore and preserve legacy best");
            Require(restored.For("en:0").Attempts == 1 && restored.For("en:free:TenMinutes").Attempts == 1 &&
                restored.For("en:free:Unlimited").Attempts == 1 && File.ReadAllText(legacy) == original,
                "Reload separate sessions and leave the original file intact");
            Console.WriteLine("Maximized startup, author credit, advance-on-error and retry, both keyboards, timing, reports, Markdown, and migration passed.");
            using (var tone = Assembly.GetExecutingAssembly().GetManifestResourceStream(WordSpeech.ErrorSoundResource))
            using (var player = new SoundPlayer(tone))
            {
                player.Load();
                Require(tone != null && player.IsLoadCompleted && tone.Length > 1000, "Windows loads the embedded hazard warning clip");
            }
            Console.WriteLine("Sentence dictation, guide toggles, and the embedded error clip passed.");
            if (args.Contains("--system-voices")) VerifySystemVoices();
            GC.KeepAlive(application);
        }
    }
}
